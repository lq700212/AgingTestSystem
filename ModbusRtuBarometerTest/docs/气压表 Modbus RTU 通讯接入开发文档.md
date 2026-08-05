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
> 目前与 Demo **不一致（属错误）**——压力寄存器地址/功能码、小数位处理、波特率均需按本 Demo 修正，详见 [第 8 节](#8-关键坑点与待现场确认项)。

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

> ✅ **以本 Demo 为准**。生产实现 `ModbusRtuBarometerReader` 目前的**压力寄存器地址/功能码、小数位处理、波特率**都与 Demo 不符（属错误），接入时必须按本 Demo 修正，详见 [第 8 节](#8-关键坑点与待现场确认项)。

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
| 「真空压力读取」`btnReadPressure` | 读**从站 1** 压力，按实际小数位 `F{decimalPos}` 显示 | [Form1.cs:214-227](../Form1.cs#L214-L227) |
| 「设置1号表气压阈值」`btnSetThreshold` | 对**从站 1** 写阈值（`0x0010`） | [Form1.cs:279-314](../Form1.cs#L279-L314) |
| 「批量设置气压阈值」`btnBatchSetThreshold` | 对**从站 1~72** 写同一阈值，每次写入后延时 50ms | [Form1.cs:233-274](../Form1.cs#L233-L274) |
| 阈值输入框 `tbThreshold` | 浮点数输入（如 -95.0） | — |
| 压力/SN 显示 `llPressure`/`llSN` | 大字号显示 | — |
| 「扫码测试：获取SN」`btnGetSN` | **无 Click 事件处理（死按钮）**，勿参考 | [Form1.Designer.cs:71-79](../Form1.Designer.cs#L71-L79) |

**读取重试机制**（[Form1.cs:109-144](../Form1.cs#L109-L144)）：最多重试 2 次，第一次失败 `Thread.Sleep(150)` 后重试，第二次失败抛出。
（⚠️ demo 在 UI 线程里 `Thread.Sleep`，会卡界面，仅测试用。）

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
生产工程 `BarometerWinform` 中：
- 接口：[IBarometerReader.cs](../../BarometerWinform/Interfaces/IBarometerReader.cs) — `Connect/Disconnect/ReadData/ReadAllData/OnError`
- 实现：[ModbusRtuBarometerReader.cs](../../BarometerWinform/Services/ModbusRtuBarometerReader.cs) — 用 NModbus RTU 主站，串口对象加 `_syncRoot` 互斥锁防并发，读 72 台逐台轮询
- 编排：[DeviceManager.cs](../../BarometerWinform/Services/DeviceManager.cs) — 定时采集（默认 1000ms）+ 报警边沿联动（关电磁阀 + 断载台上电）

> 🔴 **接入必改**：`ModbusRtuBarometerReader` 当前的读压力方式（Holding 0x0010 / 0x03 / 无小数位处理）与 Demo 不符，
> 需改为 **Input Register 0x0001~0x0002（0x04）+ 除以 10^小数位**；波特率配置改为 **19200**。修正点详见 [第 8 节](#8-关键坑点与待现场确认项)。

### 7.2 配置项（App.config / DeviceConfig）
[DeviceConfig.cs](../../BarometerWinform/Models/DeviceConfig.cs) 中与气压表相关：

```xml
<add key="PortName" value="COM1" />        <!-- 串口号 -->
<add key="BaudRate" value="19200" />       <!-- ✅ 以 demo 为准（生产默认 9600 是错的，需改为 19200） -->
<add key="DataBits" value="8" />
<add key="StopBits" value="1" />
<add key="Parity" value="None" />
<add key="BarometerPressureRegisterAddress" value="0x0001" />  <!-- ✅ 以 demo 为准（Input Register 0x0001；生产默认 0x0010 是错的） -->
<add key="BarometerPressureScale" value="1" />                <!-- 缩放系数，默认 1（生产实现还需补读小数位并除以 10^n） -->
<add key="AlarmPressureThresholdPa" value="-95000" />         <!-- 软件报警阈值（见坑点 4） -->
<add key="AlarmWhenPressureHigherThanThreshold" value="true" />
<add key="UseMockCommunication" value="false" />              <!-- false=真实通讯 -->
```

### 7.3 新增接入步骤（供后续开发参考）
1. 实现 `IBarometerReader`（或复用 `ModbusRtuBarometerReader`），按 [第 4 节](#4-数据转换核心) 的换算读压力。
2. 在 `DeviceManager` 中替换为真实实现（`UseMockCommunication=false` 切换）。
3. **报警判定用读回来的压力值**（生产实现），在 `DeviceManager.IsAlarm` 做阈值比较，不依赖 demo 的 0x0010 阈值寄存器。
4. **单台读取失败不抛异常**（返回 null），保证 72 台里坏 1 台不影响其余（`ReadAllData` 已按此设计）。

---

## 8. 关键坑点与待现场确认项

> 🔴 本节是全文档最重要的部分——**demo 与生产实现有真实冲突，必须先定夺再写码**。

1. **寄存器地址/功能码（以 Demo 为准，生产实现是错的）**：
   - ✅ 正确：压力在 **0x0001**（Input Register，**0x04**），并读 **0x0002** 取小数位；阈值写到 **0x0010**（Holding Register，0x06）。
   - ❌ 错误：生产 `ModbusRtuBarometerReader` 用 `ReadHoldingRegisters(deviceId, 0x0010, 1)`（Holding Register，**0x03**）读压力——把"阈值地址"当压力读，寄存器类型/功能码都不对。
   - 修正：改读 `ReadInputRegisters(deviceId, 0x0001, 2)`。

2. **波特率（以 Demo 为准）**：串口是 **19200**；生产 `DeviceConfig` 默认 **9600 是错的**，把配置改为 19200。

3. **小数位处理（以 Demo 为准，生产实现缺失）**：
   - ✅ 正确：读 0x0002 取小数位（0~4，非法则默认 1），压力 = `(short)raw / 10^小数位`。
   - ❌ 错误：生产实现直接 `(short)raw × BarometerPressureScale`（默认 scale=1），**没有读小数位、也没有除以 10^n**，压力数值会差 10 的幂次。
   - 修正：生产实现补上"读 0x0002 + 除以 10^n"的逻辑（`BarometerPressureScale` 可保留作额外缩放）。

4. **两种"阈值"别混淆**：
   - **软件阈值**（生产，`AlarmPressureThresholdPa=-95000`）：上位机自己用压力值比较判报警，触发关阀/断电。**不写设备。**
   - **设备阈值**（demo，写 `0x0010`）：写进气压表内部的报警点，让气压表的**硬件报警触点（→GX-CL140 的 DI）**在该压力下闭合。
   - 两个阈值互不相干，是否都要设、设成什么值，属工艺决策。

5. **有符号强转**：`(short)data[0]` 必须显式强转，否则 `0xFFFE` 会被当 65534 而不是 -2，负压就反了。

6. **串口非线程安全**：`SerialPort` 与 NModbus Master 都不支持并发读写。生产实现用 `_syncRoot` 锁串行化（[ModbusRtuBarometerReader.cs:158](../../BarometerWinform/Services/ModbusRtuBarometerReader.cs#L158)）。多线程接入时务必加锁。

7. **批量写延时**：demo 写 72 台阈值，每台后 `Thread.Sleep(50)`，全程至少 ~3.6s，期间 UI 卡死（demo 仅测试用）。生产批量操作应异步化或按需分批。

8. **阈值负值回绕**：`(ushort)(-950)` = `0xFC5A`，靠设备按有符号 short 解释才成立。若设备按无符号解释，负阈值会变成巨大正数。写入格式需按说明书确认。

9. **死按钮勿参考**：「扫码测试：获取SN」无任何事件处理。

---

## 9. 调试与排障速查

| 现象 | 排查方向 |
| ---- | -------- |
| 打开串口报"未找到 CH340" | USB 未插 / 驱动未装 / 非 CH340（VID_1A86 PID_7523 才认） |
| 串口打开失败 Access denied | 串口被其它软件占用（调试助手等），先关闭占用程序 |
| 读取失败（重试后仍失败） | 从站地址错 / 波特率不符 / 寄存器地址错 / 接线 A/B 反了 / 共地缺失 |
| 读到数值明显不对（如巨大正数） | 没做 `(short)` 有符号强转，或小数位/缩放配错 |
| 只有部分从站能读 | 该台从站地址与物理地址不符，或该台损坏 |
| 数据偶尔 CRC/超时错 | 串口并发读写（需加锁）/ 干扰大（降波特率或加终端电阻） |
| 数值单位对不上 | 确认压力单位（Pa/kPa）与小数位，配 `BarometerPressureScale` |

---

## 10. 参考资料与文件索引

| 文件 | 说明 |
| ---- | ---- |
| [Form1.cs](../Form1.cs) | 读压力（带重试）/ 单个与批量写阈值 的测试实现 |
| [SerialPortHelper.cs](../SerialPortHelper.cs) | CH340 串口自动识别（WMI + VID/PID 匹配） |
| [ModbusRtuBarometerReader.cs](../../BarometerWinform/Services/ModbusRtuBarometerReader.cs) | 上位机生产实现（含串口锁、批量轮询） |
| [IBarometerReader.cs](../../BarometerWinform/Interfaces/IBarometerReader.cs) | 气压表读取接口抽象 |
| [DeviceConfig.cs](../../BarometerWinform/Models/DeviceConfig.cs) | 串口/寄存器/缩放/报警阈值 配置项 |
| [DeviceManager.cs](../../BarometerWinform/Services/DeviceManager.cs) | 定时采集 + 报警联动编排 |
| [通讯接入说明.md](../../通讯接入说明.md) | 整体链路（气压表 RTU + IO TCP）联动说明 |
| [GX-CL140 Modbus TCP 通讯接入开发文档.md](../../ModbusTcpIoControllerTest/docs/GX-CL140%20Modbus%20TCP%20通讯接入开发文档.md) | IO 耦合器（DI 报警触点 / DO 电磁阀+继电器）接入文档 |

---

*文档版本：1.0（2026-08）｜基于 ModbusRtuBarometerTest 实测代码整理，供后续 .NET Framework 4.7.2 上位机接入开发参考。*
