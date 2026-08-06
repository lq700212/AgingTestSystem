# 气压表 Modbus RTU 通讯接入开发文档

> 本文件是**真空负压表（气压表）**串口通讯接入参考文档，内容来自 `ModbusRtuBarometerTest` 测试工程
> （[Form1.cs](../Form1.cs) / [SerialPortHelper.cs](../SerialPortHelper.cs)）的**实测代码**，并结合上位机工程
> `BarometerWinform`（.NET Framework 4.7.2）的接入现状（[ModbusRtuBarometerReader.cs](../../BarometerWinform/Services/ModbusRtuBarometerReader.cs)）整理。
>
> 文档目标：让开发者 / AI 在 5 分钟内掌握"怎么连、读什么、写什么、怎么换算、有哪些坑"，并能直接照着写接入代码。
>
> ⚠️ **协议声明**：本气压表通讯走的是 **Modbus RTU（RS485 串口）**，不是 Modbus TCP。工程名虽为 "Rtu"，库用的是
> `NModbus.Serial`，主站创建方式是 `factory.CreateRtuMaster(_port)`。若后续需求改走 TCP，需换 `CreateMaster(TcpClient)`。
>
> ✅ **协议口径（已定论）**：通讯细节**以本 Demo 实测为准**。上位机生产实现 `ModbusRtuBarometerReader`
> 在 V1.15 已按本 Demo **修正并保持一致**：压力读 Input Register 0x0001~0x0002（0x04）、波特率 19200、
> 除以 10^小数位，并新增 `SetThreshold / SetAllThresholds` 设备阈值写入能力，详见 [第 7 节](#7-接入-net-framework-472-上位机barometerwinform)。

---

## 0. 三十秒速览（TL;DR）

| 项目 | 值 |
| ---- | -- |
| 设备 | 真空负压表（气压表）× 72 台，**每台 = 1 个 Modbus RTU 从站**（地址 1~72） |
| 传输 | RS485 → **CH340 USB 转串口** → COM 口 |
| 串口参数 | **19200 baud，8 数据位，无校验，1 停止位（8N1）**，读写超时 3000ms |
| 主站库 | NModbus 3.0.83 + NModbus.Serial（RTU 主站） |
| 读压力 | 功能码 **0x04**（Read Input Registers）：`ReadInputRegisters(slaveId, 0x0001, 2)` |
| 写阈值 | 功能码 **0x06**（Write Single Register）：`WriteSingleRegister(slaveId, 0x0010, value)` |
| 压力换算 | `(short)raw / 10^小数位`，小数位从寄存器 0x0002 动态读（0~4，无效则默认 1） |
| 目标框架 | .NET Framework 4.7.2 |

**读 1 台压力**：`ReadInputRegisters(slaveId, 0x0001, 2)` → `reg[0]`=压力原始值（按有符号 short 解释），`reg[1]`=小数位数。
**写 1 台阈值**：先把浮点阈值 `× 10^小数位` 取整，再 `WriteSingleRegister(slaveId, 0x0010, 值)`。

> ✅ **以本 Demo 为准**。生产实现 `ModbusRtuBarometerReader` 在 **V1.15 已按本 Demo 修正**（压力寄存器地址/功能码、小数位处理、波特率均一致），接入时无需再改，详见 [第 8 节](#8-关键坑点与待现场确认项)。

---

## 1. 硬件架构

```
上位机 PC (.NET 4.7.2)
      │  USB
      ▼
┌─────────────┐       RS485 总线（A/B 双线）
│  CH340 芯片 │ ──────────────────────────────┬──────────────┬──────────────┬── ...
│  (USB转串口)│                               │              │              │
└─────────────┘                         ┌──────────┐  ┌──────────┐  ┌──────────┐
     ↑                                    │ 气压表 #1 │  │ 气压表 #2 │  │ 气压表 #72│
     │ Modbus RTU 主站                    │ 从站地址1 │  │ 从站地址2 │  │ 从站地址72│
     │ 一主多从：主站轮询 1~72 个从站       └──────────┘  └──────────┘  └──────────┘
```

- **一主多从**：上位机是 Modbus 主站，72 台气压表是从站，从站地址 = deviceId（1~72）。
- **CH340 自动识别**：[SerialPortHelper.cs](../SerialPortHelper.cs) 用 WMI 查询 `Win32_PnPEntity`，按
  `VID_1A86`（CH340 厂商）+ `PID_7523`（CH340 产品）双重匹配，再从 Caption 里解析出 `COMx`。
- **RS485 接线要点**：A/B 双线并联所有从站；共地；总线两端建议加 120Ω 终端电阻；距离长/干扰大时降低波特率。

---

## 2. 协议说明

### 2.1 物理层与数据链路
- **传输**：串口（RS485 转 USB 后表现为 COM 口）。
- **串口参数**（[Form1.cs:50-55](../Form1.cs#L50-L55)）：`19200 / 8 / None / 1`，读写超时 3000ms。
- **帧格式**：Modbus RTU —— 地址(1B) + 功能码(1B) + 数据(NB) + CRC16(2B)。**带 CRC 校验**（与 TCP 的 MBAP 无 CRC 不同）。

### 2.2 用到的功能码

| 功能码 | 名称 | 用途 | 说明 |
| ------ | ---- | ---- | ---- |
| `0x04` | Read Input Registers | 读压力原始值 + 小数位 | 一次读 2 个寄存器（0x0001 起） |
| `0x06` | Write Single Register | 写阈值 | 写单个保持寄存器（0x0010） |

### 2.3 从站地址
- 每台气压表一个从站地址，**slaveId = deviceId（1~72）**。
- 现场若实际地址不是这个规则，需改成"固定从站地址 + 不同寄存器偏移"（生产实现里保留了此 TODO）。

---

## 3. 寄存器映射表（demo 实测）

| 寄存器 | 寄存器类型 | 读/写 | 功能码 | 含义 |
| ------ | ---------- | ----- | ------ | ---- |
| `0x0001` | Input Register | 只读 | `0x04` | **压力原始值**（按**有符号 short** 解释，支持负压） |
| `0x0002` | Input Register | 只读 | `0x04` | **小数位数**（合法 0~4；读到非法值则按默认 1 处理） |
| `0x0010` | Holding Register | 只写 | `0x06` | **阈值寄存器**（demo 写入；⚠️ 见下方冲突说明） |

> ✅ **以 Demo 为准（已定论）**：
> - 压力在 **`0x0001`（Input Register，功能码 0x04）**，同时读 `0x0002` 取小数位；
> - 阈值写到 **`0x0010`（Holding Register，功能码 0x06）**。
> - 🔴 生产实现 `ModbusRtuBarometerReader` 用 `ReadHoldingRegisters(deviceId, 0x0010, 1)` **读压力是错的**——把"阈值地址"当压力读，寄存器类型/功能码都不对，接入时须按本表修正。

---

## 4. 数据转换（核心）

### 4.1 压力值：原始值 → 实际压力
[Form1.cs:114-130](../Form1.cs#L114-L130) 的逻辑：

```csharp
ushort[] data = _master.ReadInputRegisters(slaveId, 0x0001, 2);

short  rawSigned = (short)data[0];   // 0xFFFE → -2（有符号强转，支持负压）
int    rawValue  = rawSigned;

int decimalPos;                       // 小数位数：读 0x0002
if (data[1] >= 0 && data[1] <= 4)     // 合法范围 0~4 才用，否则用默认值
    decimalPos = data[1];
else
    decimalPos = 1;                   // DefaultDecimalPlaces

float pressure = rawValue / (float)Math.Pow(10, decimalPos);
```

**换算公式**：`压力 = (short)原始值 / 10^小数位`
- 例：`0x0001 = 0xFFCE` → `(short)0xFFCE = -50`，`0x0002 = 1` → `-50 / 10 = -5.0`
- **单位**代码里没定义（Pa？kPa？），需按设备说明书确认。

### 4.2 阈值：浮点数 → 寄存器值
[Form1.cs:200-207](../Form1.cs#L200-L207) 的逻辑：

```csharp
int multiplier = (int)Math.Pow(10, decimalPos);      // 小数位对齐
int intValue   = (int)Math.Round(thresholdFloat * multiplier);
ushort regVal  = (ushort)intValue;                    // 负值会按补码回绕，见坑点 8
```

**换算公式**：`寄存器值 = (ushort)round(阈值浮点数 × 10^小数位)`
- demo 批量/单个写阈值时**小数位固定为 1**（与读取默认一致，[Form1.cs:254](../Form1.cs#L254)）。
- 例：阈值 `-95.0`、小数位 1 → `round(-950) = -950` → `(ushort)(-950) = 0xFC5A`（设备按有符号 short 解释回读即 -950）。

---

## 5. Demo 功能与 UI 说明

窗体 [Form1.cs](../Form1.cs)（SunnyUI 风格），控件见 [Form1.Designer.cs](../Form1.Designer.cs)：

| 控件 | 行为 | 关键代码 |
| ---- | ---- | -------- |
| 「真空压力读取」`btnReadPressure` | 读**从站 1** 压力，按实际小数位 `F{decimalPos}` 显示 | [Form1.cs:227-240](../Form1.cs#L227-L240) |
| 「设置1号表气压阈值」`btnSetThreshold` | 对**从站 1** 写阈值（`0x0010`） | [Form1.cs:383-412](../Form1.cs#L383-L412) |
| 「批量设置气压阈值」`btnBatchSetThreshold` | 对**从站 1~72** 写同一阈值，每次写入后延时 50ms；**不再逐台弹窗**，失败设备最后统一汇总显示（成功 N 台 + 失败名单） | [Form1.cs:246-288](../Form1.cs#L246-L288) |
| 「批量读取压力」`btnBatchRead` | 扫描全部从站，报告**离线/无响应**的设备（排查"哪台写入超时"用） | [Form1.cs:321-374](../Form1.cs#L321-L374) |
| 阈值输入框 `tbThreshold` | 浮点数输入（如 -95.0） | — |
| 压力/SN 显示 `llPressure`/`llSN` | 大字号显示 | — |
| 「扫码测试：获取SN」`btnGetSN` | **无 Click 事件处理（死按钮）**，勿参考 | [Form1.Designer.cs:71-79](../Form1.Designer.cs#L71-L79) |

**读取重试机制**（[Form1.cs:108-144](../Form1.cs#L108-L144)）：最多重试 2 次，第一次失败 `Thread.Sleep(150)` 后重试，第二次失败抛出。
（⚠️ demo 在 UI 线程里 `Thread.Sleep`，会卡界面，仅测试用。）

> 🛠 **批量写失败时的正确排查姿势**：先点「批量读取压力」扫一遍，离线名单里出现哪几台，
> 那几台就是**设备问题**（断电/掉线/地址拨错/损坏），不是程序 bug——Modbus 对不响应的从站只能等超时。
>
> 📄 **扫描/写入会落盘 CSV 日志**（[Form1.cs](../Form1.cs) 的 `AppendScanLog`）：
> 每次「批量读取 / 批量设置」把每一台的 `时间戳,操作,从站号,结果,耗时ms,备注` 追加到
> exe 同目录 `Logs\BarometerScan_yyyyMMdd.csv`（按天分文件）。
> **多扫几遍后把 CSV 汇总到 Excel**，按从站号+时间排序：
> - **永远失败且耗时≈满超时(约3000ms)** → 永久离线，查供电/接线/设备本身；
> - **时好时坏（这次成功下次超时）** → 间歇性，重点查总线干扰、供电不稳、接头接触不良。
> 这份客观数据可直接交给电工辅助定位。

---

## 6. C# 实现示例（NModbus RTU）

NuGet：`Install-Package NModbus -Version 3.0.83` + `Install-Package NModbus.Serial -Version 3.0.83`

```csharp
using System.IO.Ports;
using NModbus;
using NModbus.Serial;

// ---- 打开串口 + 创建 RTU 主站（demo 方式）----
var port = new SerialPort("COM3") { BaudRate = 19200, DataBits = 8,
                                    Parity = Parity.None, StopBits = StopBits.One,
                                    ReadTimeout = 3000, WriteTimeout = 3000 };
port.Open();
var master = new ModbusFactory().CreateRtuMaster(port);

byte slaveId = 1;

// ---- 读压力（功能码 0x04，读 2 个寄存器）----
ushort[] data = master.ReadInputRegisters(slaveId, 0x0001, 2);
short rawSigned = (short)data[0];
int decimalPos  = (data[1] <= 4) ? data[1] : 1;
float pressure  = rawSigned / (float)Math.Pow(10, decimalPos);

// ---- 写阈值（功能码 0x06，写 Holding Register 0x0010）----
int  regVal = (int)Math.Round(-95.0f * 10);   // 小数位=1
master.WriteSingleRegister(slaveId, 0x0010, (ushort)regVal);

// ---- 资源释放 ----
master.Dispose();
port.Close();
```

---

## 7. 接入 .NET Framework 4.7.2 上位机（BarometerWinform）

### 7.1 现状（已完成的部分）
生产工程 `BarometerWinform` 中（**V1.15 已与 Demo 保持一致**）：
- 接口：[IBarometerReader.cs](../../BarometerWinform/Interfaces/IBarometerReader.cs) — `Connect/Disconnect/ReadData/ReadAllData/SetThreshold/SetAllThresholds/OnError`
- 实现：[ModbusRtuBarometerReader.cs](../../BarometerWinform/Services/ModbusRtuBarometerReader.cs) — 用 NModbus RTU 主站，串口对象加 `_syncRoot` 互斥锁防并发，读 72 台逐台轮询；
  读压力为 **Input Register 0x0001~0x0002（0x04）+ 除以 10^小数位**，波特率 **19200**；
  **V1.15 新增** `SetThreshold / SetAllThresholds`（写设备阈值 0x0010，0x06），与 Demo 写入逻辑一致
- 编排：[DeviceManager.cs](../../BarometerWinform/Services/DeviceManager.cs) — 定时采集（默认 1000ms）+ 报警边沿联动（关电磁阀 + 断载台上电）；
  **V1.15 新增**透传方法 `SetBarometerThreshold / SetAllBarometerThresholds`

> ✅ **无需再改**：早期文档曾提示生产实现读压力方式与 Demo 不符，该问题在 V1.15 已修正（见 [第 8 节](#8-关键坑点与待现场确认项) 1~3 的"现状"标注）。

### 7.2 配置项（App.config / DeviceConfig）
[DeviceConfig.cs](../../BarometerWinform/Models/DeviceConfig.cs) 中与气压表相关：

```xml
<add key="PortName" value="COM1" />        <!-- 串口号 -->
<add key="BaudRate" value="19200" />       <!-- ✅ 与 Demo 一致（V1.15 起生产默认已是 19200） -->
<add key="DataBits" value="8" />
<add key="StopBits" value="1" />
<add key="Parity" value="None" />
<add key="BarometerPressureRegisterAddress" value="0x0001" />  <!-- ✅ 与 Demo 一致（Input Register 0x0001；V1.15 起生产默认已是 0x0001） -->
<add key="BarometerPressureScale" value="1" />                <!-- 缩放系数，默认 1；V1.15 起已补读小数位 0x0002 并除以 10^n -->
<add key="AlarmPressureThresholdPa" value="-95000" />         <!-- 软件报警阈值（见坑点 4），不写设备 -->
<add key="AlarmWhenPressureHigherThanThreshold" value="true" />
<add key="UseMockCommunication" value="false" />              <!-- false=真实通讯 -->
```

> 📌 写设备阈值（0x0010）的值不是上面这个 Pa 值，而是"设备单位"值，需现场确认（见坑点 4）。

### 7.3 新增接入步骤（供后续开发参考）
1. 复用 `ModbusRtuBarometerReader`（V1.15 起已与 Demo 一致），按 [第 4 节](#4-数据转换核心) 换算读压力。
2. 在 `DeviceManager` 中切换真实/模拟实现（`UseMockCommunication`）。
3. **报警判定用读回来的压力值**（生产实现），在 `DeviceManager.IsAlarm` 做阈值比较，不依赖 demo 的 0x0010 阈值寄存器。
4. **单台读取失败不抛异常**（返回 null），保证 72 台里坏 1 台不影响其余（`ReadAllData` 已按此设计）。
5. **需要写设备阈值时**：调 `_deviceManager.SetAllBarometerThresholds(设备单位阈值)`（逐台失败不中断，返回失败名单）；
   设备单位未确认前不要写，且批量写应在后台线程执行（72 台连写 + 坏设备会阻塞较久）。

---

## 8. 关键坑点与现场确认项

> ✅ 早期版本曾存在"demo 与生产实现冲突"，该问题在 **V1.15 已全部修正**：
> 生产实现与 Demo 现在完全一致（读 0x0001/0x04 + 0x0002 小数位 + 19200）。
> 以下 1~3 保留"曾经的错误 + 现状"供回溯，**无需再改代码**。

1. **寄存器地址/功能码（现状：已一致）**：
   - 正确：压力在 **0x0001**（Input Register，**0x04**），并读 **0x0002** 取小数位；阈值写到 **0x0010**（Holding Register，0x06）。
   - 曾经错误：生产实现用 `ReadHoldingRegisters(deviceId, 0x0010, 1)`（Holding Register，**0x03**）读压力。
   - 现状：V1.15 已改为 `ReadInputRegisters(deviceId, 0x0001, 2)`，与 Demo 一致。

2. **波特率（现状：已一致）**：串口 **19200**；V1.15 起生产 `DeviceConfig` 默认已是 19200。

3. **小数位处理（现状：已一致）**：V1.15 起生产实现读 0x0002 取小数位（0~4，非法默认 1），压力 = `(short)raw / 10^小数位`；`BarometerPressureScale` 保留作额外缩放。

4. **两种"阈值"别混淆**：
   - **软件阈值**（生产，`AlarmPressureThresholdPa=-95000`）：上位机自己用压力值比较判报警，触发关阀/断电。**不写设备。**
   - **设备阈值**（demo / 生产 `SetThreshold`，写 `0x0010`）：写进气压表内部的报警点，让气压表的**硬件报警触点（→GX-CL140 的 DI）**在该压力下闭合。
   - 两个阈值互不相干，是否都要设、设成什么值，属工艺决策。设备阈值的单位需与压力读数一致（**不是 Pa 软件阈值**），按说明书确认后再写。

5. **有符号强转**：`(short)data[0]` 必须显式强转，否则 `0xFFFE` 会被当 65534 而不是 -2，负压就反了。

6. **串口非线程安全**：`SerialPort` 与 NModbus Master 都不支持并发读写。生产实现用 `_syncRoot` 锁串行化（[ModbusRtuBarometerReader.cs:158](../../BarometerWinform/Services/ModbusRtuBarometerReader.cs#L158)）。多线程接入时务必加锁。

7. **批量写延时**：demo 写 72 台阈值，每台后 `Thread.Sleep(50)`，全程至少 ~3.6s，期间 UI 卡死（demo 仅测试用）。V1.15 起批量写**不再逐台弹窗**，失败设备最后统一汇总。生产批量操作应异步化或按需分批。

8. **阈值负值回绕**：`(ushort)(-950)` = `0xFC5A`，靠设备按有符号 short 解释才成立。若设备按无符号解释，负阈值会变成巨大正数。写入格式需按说明书确认。

9. **死按钮勿参考**：「扫码测试：获取SN」无任何事件处理。

10. **批量写某台/某几台超时 = 设备问题（现场实测 2026-08）**：
    - 现象：批量写 1~72 台时，某台（如第 32 台）一直超时。
    - 真相：Modbus 主站对"不响应的从站"只能等读超时。实测 72 台连读，**地址 32 完全不响应**（读写都超时），
      地址 30 偶发一次写超时后自行恢复——即**设备掉线 / 断电 / 从站地址拨错 / 损坏**，不是程序 bug。
    - 排查：点 demo 的「批量读取压力」扫一遍，离线名单即故障设备；修好硬件后重跑批量写即可。
    - 程序加固：批量写失败不再逐台弹窗卡流程，最后统一汇总"成功 N 台 + 失败哪几台"。

---

## 9. 调试与排障速查

| 现象 | 排查方向 |
| ---- | -------- |
| 打开串口报"未找到 CH340" | USB 未插 / 驱动未装 / 非 CH340（VID_1A86 PID_7523 才认） |
| 串口打开失败 Access denied | 串口被其它软件占用（调试助手等），先关闭占用程序 |
| 读取失败（重试后仍失败） | 从站地址错 / 波特率不符 / 寄存器地址错 / 接线 A/B 反了 / 共地缺失 |
| 读到数值明显不对（如巨大正数） | 没做 `(short)` 有符号强转，或小数位/缩放配错 |
| 只有部分从站能读 | 该台从站地址与物理地址不符，或该台损坏 |
| 批量写某台/某几台一直超时 | **先点「批量读取压力」扫一遍**——离线名单里的就是故障设备（断电/掉线/地址错/损坏），修硬件后重跑批量写；每次扫描/写入的逐台结果+耗时已落盘 `Logs\BarometerScan_yyyyMMdd.csv`，多扫几遍汇总 Excel 可区分"永久离线"还是"间歇掉线" |
| 数据偶尔 CRC/超时错 | 串口并发读写（需加锁）/ 干扰大（降波特率或加终端电阻）；偶发一次失败后可自行恢复属正常总线抖动 |
| 数值单位对不上 | 确认压力单位（Pa/kPa）与小数位，配 `BarometerPressureScale` |

---

## 10. 参考资料与文件索引

| 文件 | 说明 |
| ---- | ---- |
| [Form1.cs](../Form1.cs) | 读压力（带重试）/ 单个与批量写阈值 / 批量读取（离线扫描）的测试实现 |
| [SerialPortHelper.cs](../SerialPortHelper.cs) | CH340 串口自动识别（WMI + VID/PID 匹配） |
| [ModbusRtuBarometerReader.cs](../../BarometerWinform/Services/ModbusRtuBarometerReader.cs) | 上位机生产实现（含串口锁、批量轮询、SetThreshold/SetAllThresholds） |
| [IBarometerReader.cs](../../BarometerWinform/Interfaces/IBarometerReader.cs) | 气压表读取/写阈值 接口抽象 |
| [DeviceConfig.cs](../../BarometerWinform/Models/DeviceConfig.cs) | 串口/寄存器/缩放/报警阈值 配置项 |
| [DeviceManager.cs](../../BarometerWinform/Services/DeviceManager.cs) | 定时采集 + 报警联动编排 + SetBarometerThreshold 透传 |
| [通讯接入说明.md](../../通讯接入说明.md) | 整体链路（气压表 RTU + IO TCP）联动说明 |
| [GX-CL140 Modbus TCP 通讯接入开发文档.md](../../ModbusTcpIoControllerTest/docs/GX-CL140%20Modbus%20TCP%20通讯接入开发文档.md) | IO 耦合器（DI 报警触点 / DO 电磁阀+继电器）接入文档 |

---

*文档版本：1.2（2026-08）｜基于 ModbusRtuBarometerTest 实测代码整理，供后续 .NET Framework 4.7.2 上位机接入开发参考。V1.2 更新：批量读取/批量写入新增 CSV 日志落盘（`Logs\BarometerScan_yyyyMMdd.csv`，逐台结果+耗时），用于区分"永久离线"与"间歇掉线"、辅助电工定位。V1.1 更新：生产实现 V1.15 已与 Demo 一致；批量写改为失败统一汇总 + 新增批量读取（离线扫描）按钮；补充"某台批量写超时 = 设备问题"实测排查经验。*
