# GX-CL140 Modbus TCP 通讯接入开发文档

> 本文件是 **GX-CL140 插片式 I/O 耦合器（Modbus TCP）** 的通讯接入参考文档，内容全部来自
> `ModbusTcpIoControllerTest` 测试工程（[MainForm.cs](../MainForm.cs) / [PowerOnTestForm.cs](../PowerOnTestForm.cs)）
> 的**实测结果**，并结合 `显耀IO.xlsx` 与上位机工程 `BarometerWinform`（.NET Framework 4.7.2）的接入现状整理。
>
> 文档目标：让开发者 / AI 在 5 分钟内掌握"怎么连、读什么、写什么、映射是什么、有哪些坑"，并能直接照着写接入代码。

---

## 0. 三十秒速览（TL;DR）

| 项目 | 值 |
| ---- | -- |
| 设备 | 显耀 GX-CL140 插片式 I/O 耦合器 + 扩展模块 |
| 协议 | Modbus TCP（MBAP 报头，无 CRC） |
| IP / 端口 | `192.168.1.20` / `502`（出厂默认） |
| 从站地址 UnitId | `0x01` |
| DI（数字量输入） | **功能码 0x04** 读 Input Register，起始地址 `0x1000`，16 点/寄存器 |
| DO（数字量输出） | **功能码 0x06** 写单寄存器、**0x03** 读 Holding Register，起始地址 `0x2000`，16 点/寄存器 |
| 现场规模 | 80 DI / 160 DO（业务用 72 DI / 144 DO） |
| 业务设备 | 气压表(报警触点→DI)、电磁阀(DO→控制负压)、继电器(DO→载台上电) |
| 常用 C# 库 | EasyModbusTCP 5.6.0（测试工程）、NModbus 3.0.83（上位机工程） |
| 目标框架 | .NET Framework 4.7.2 |

**读 1 路 DI**：`ReadInputRegisters(0x1000, 1)` → 取返回值的 bit0~bit15（bit0=第1路）。
**写 1 路 DO**：先 `ReadHoldingRegisters` 读回所在寄存器 → 只改目标 bit → `WriteSingleRegister` 写回（**读-改-写**，禁止直接整字覆写，否则误伤同寄存器其它通道）。

---

## 1. 硬件架构

```
上位机 PC (.NET 4.7.2)
      │  Modbus TCP (IP:192.168.1.20:502, UnitId=1)
      ▼
┌──────────────┐   右侧总线扩展（最多 64 个模块）
│  GX-CL140    │─────────────────────────────────────────────┐
│ 耦合器(主脑) │                                              │
└──────────────┘                                              │
  SP: 系统电源 24V（必备，不接耦合器不启动，SYS 不亮）          │
  FP: 现场电源 24V（给 IO 模块回路供电，必备）                  │
                                                              │
   ├─ 输入模块：2×DI50N-S + 1×DI40N-S = 80 路 DI  ──┐         │
   │       每 16 路打包 1 个寄存器：0x1000~0x1004    │         │
   │       └─ 气压表报警触点 → DI 通道（NPN）        │         │
   │                                                ▼         │
   └─ 输出模块：5×DQ50P-S = 160 路 DO  ──► 每个模块 32 路（00~0F + 10~1F）
           每 16 路打包 1 个寄存器：0x2000~0x2009
           ├─ 真空电磁阀（PNP，控制负压）  → Y000~Y107
           └─ 继电器（PNP，控制载台上电）  → Y110~Y217
```

### 1.1 关键接线要点（实测）
- **SP 与 FP 都要接 24V**：SP 给耦合器本体供电（SYS 不亮=没电），FP 给 IO 模块输入/输出回路供电（FP 断则 AL 红灯闪）。两者缺一不可，且按硬件要求独立/并联供电，不能混接。
- **网络**：PC 网卡 IP 需与耦合器同网段（如 `192.168.1.100/24`），网线插 X1 或 X2 均可。**网线接触不良**会表现为 ST 绿灯闪烁但无法通讯，务必插紧。
- **模块安装顺序**：GX-CL140 → 输入模块 → 输出模块，沿 DIN 导轨滑动卡入。

### 1.2 指示灯含义（现场排障速查）

| 灯 | 正常 | 异常 |
| -- | ---- | ---- |
| SP | 绿常亮 | 不亮→24V/正负接错 |
| FP | 绿常亮 | AL 红灯闪→FP 未接/接触不良 |
| SYS | 绿常亮(运行) | 红闪=等待网络连接；恢复出厂可长按复位键 >6s |
| AL | 熄灭 | 红灯闪→FP 电源或 IO 模块未插牢 |
| ST | 闪绿→常绿 | 闪绿=待连线，常绿=数据交换中 |

---

## 2. Modbus TCP 协议说明

- **传输**：TCP/IP，默认端口 502，MBAP 报文头 7 字节 + PDU，**无 CRC**（与 RTU 不同）。
- **从站地址**：UnitId = `0x01`（代码中 `modbusClient.UnitIdentifier = 0x01`）。
- **字节序**：大端（高字节在前），寄存器为 16 位（ushort）。
- **寄存器区域表**（依据测试工程实测结果整理）：

| 数据类型 | 起始地址 | 地址空间 | 读/写 | 功能码（读/写） |
| -------- | -------- | -------- | ----- | --------------- |
| 数字量输入 DI | `0x1000` | 512 Words | 只读 | `0x03` 或 `0x04` |
| 数字量输出 DO | `0x2000` | 512 Words | 读/写 | `0x03` / `0x06` 或 `0x10` |
| 模拟量输入 AI | `0x3000` | 512 Words | 只读 | `0x03` 或 `0x04` |
| 模拟量输出 AO | `0x4000` | 512 Words | 读/写 | `0x03` / `0x06` 或 `0x10` |
| 错误码信息 | `0xE000` | 6 Words | 只读 | `0x03` 或 `0x04` |

> **功能码选型（实测结论）**：DI 用 `0x04`（Read Input Registers）更规范；DO 读用 `0x03`（Read Holding Registers）、写用 `0x06`（Write Single Register）。现场已验证这三种组合全部可用。

---

## 3. 寄存器 ↔ 通道 映射表

一个寄存器 = 16 个通道，**bit0=第1路，bit15=第16路**。

### 3.1 DI 输入（80 路，只读）

| 寄存器 | 通道 | 对应显耀 X 地址（八进制） |
| ------ | ---- | ------------------------- |
| `0x1000` | DI 1~16 | X000~X017 |
| `0x1001` | DI 17~32 | X020~X037 |
| `0x1002` | DI 33~48 | X040~X057 |
| `0x1003` | DI 49~64 | X060~X077 |
| `0x1004` | DI 65~80 | X100~X117（65~72 为气压表，73~80 预留） |

### 3.2 DO 输出（160 路，读写）

| 寄存器 | 通道 | 对应显耀 Y 地址（八进制） | 业务 |
| ------ | ---- | ------------------------- | ---- |
| `0x2000` | DO 1~16 | Y000~Y017 | 真空电磁阀 1~16 |
| `0x2001` | DO 17~32 | Y020~Y037 | 真空电磁阀 17~32 |
| `0x2002` | DO 33~48 | Y040~Y057 | 真空电磁阀 33~48 |
| `0x2003` | DO 49~64 | Y060~Y077 | 真空电磁阀 49~64 |
| `0x2004` | DO 65~80 | Y100~Y117 | **低字节=电磁阀65~72，高字节=载台上电1~8** |
| `0x2005` | DO 81~96 | Y120~Y137 | 载台上电 9~24（低9~16 / 高17~24） |
| `0x2006` | DO 97~112 | Y140~Y157 | 载台上电 25~40（低25~32 / 高33~40） |
| `0x2007` | DO 113~128 | Y160~Y177 | 载台上电 41~56（低41~48 / 高49~56） |
| `0x2008` | DO 129~144 | Y200~Y217 | 载台上电 57~72（低57~64 / 高65~72） |
| `0x2009` | DO 145~160 | Y220~Y237 | 预留 |

> ⚠️ **重点**：`0x2004~0x2008` 这 5 个寄存器都是"**低字节一个业务 + 高字节一个业务**"共用的（0x2004 甚至跨了电磁阀与载台上电两个设备族）。**任何单点写都必须读-改-写整个 16 位寄存器**，否则会覆盖同寄存器其它通道。详见第 7 节坑点。

---

## 4. 业务设备 ↔ IO 点映射（显耀IO.xlsx）

三个业务设备全部通过 GX-CL140 耦合器接入：

| 业务设备 | 电气 | 显耀地址 | 数量 | 通道类型 | 通讯内容 |
| -------- | ---- | -------- | ---- | -------- | -------- |
| 真空负压表（气压表） | NPN | `X000~X107` | 72 | **DI 输入** | 报警触点→DI（硬件冗余信号，UI 显示）；压力数值另走 RS485(RTU) 供软件判报警 |
| 真空电磁阀（控制负压） | PNP | `Y000~Y107` | 72 | **DO 输出** | 上位机开/关电磁阀 → 控制负压 |
| 继电器（载台上电） | PNP | `Y110~Y217` | 72 | **DO 输出** | 上位机控制载台 9×8 逐点/批量上电 |

- 每个气压表对应 **1 输入 + 2 输出**（气压表报警 + 真空阀 + 载台上电）。
- **八进制编址**（三菱风格）：X007 后是 X010（不是 X008），X077 后是 X100。第 n 个点的地址 = `X/Y + 八进制(n-1)`。

> ⚠️ **气压表是"双信号"接入，两条链路别混淆**：
> - **信号①（判报警用）**：压力值走 **RS485 串口（Modbus RTU）** 读回，上位机用阈值（默认 -95000 Pa）软件判报警。**"关电磁阀 + 断载台上电"的联动动作只由这条链路触发**（`DeviceManager.IsAlarm` → `HandleAlarm`）。
> - **信号②（硬件报警触点→DI）**：报警触点接到 GX-CL140 的 DI（X000~X107），读进 UI 显示（`InputStatus[0]`），是**冗余的硬件信号**，当前**不参与**报警联动。现场若见"DI 没动作但程序已关阀/断电"，属正常——触发来自 RTU 压力值。
> - DI 为 NPN 低有效：触点闭合读到 1 还是 0，取决于线制与 `InvertInputs` 配置，需现场实测确认。

### 4.1 内部连续编号（上位机工程用法）

上位机 `BarometerWinform` 内部不直接用 X/Y 地址，而是用**十进制连续编号 IoId**（便于数组索引），再经 `IoMapBuilder` 换算成物理地址：

| 类别 | 内部编号 | 物理地址规律 | 举例 |
| ---- | -------- | ------------ | ---- |
| 输入（真空负压表 n） | `1 ~ TotalInputs`（默认 1~80） | `X + octal(n-1)` | 1→X000，72→X107 |
| 输出-真空阀（n） | `TotalInputs + n`（默认 81~152） | `Y + octal(n-1)` | 1→Y000，72→Y107 |
| 输出-载台上电（n） | `TotalInputs + TotalBarometers + n`（默认 153~224） | `Y + octal(72+n-1)` | 1→Y110，72→Y217 |

> 注：`TotalInputs`/`TotalOutputs` 可在 App.config 调整。现场默认 **80 DI / 160 DO**（业务用 72/144，多出的 8 DI + 16 DO 预留）。

---

## 5. 载台上电（继电器）72 路详细映射

PowerOnTestForm 把载台上电 72 路按 **9 排 × 8 列** 排布（与上位机 8×9 面板一致），分布在
寄存器 `0x2004~0x2008`（5 个字）。**0x2004 只用高字节，低字节属于电磁阀 65~72**。

| 排（行） | 寄存器 | 字节 | 位值 | IO 编号（8 列） |
| -------- | ------ | ---- | ---- | --------------- |
| 第 1 排 | `0x2004` | 高字节 | 0x0100~0x8000 | Y110~Y117 |
| 第 2 排 | `0x2005` | 低字节 | 0x0001~0x0080 | Y120~Y127 |
| 第 3 排 | `0x2005` | 高字节 | 0x0100~0x8000 | Y130~Y137 |
| 第 4 排 | `0x2006` | 低字节 | 0x0001~0x0080 | Y140~Y147 |
| 第 5 排 | `0x2006` | 高字节 | 0x0100~0x8000 | Y150~Y157 |
| 第 6 排 | `0x2007` | 低字节 | 0x0001~0x0080 | Y160~Y167 |
| 第 7 排 | `0x2007` | 高字节 | 0x0100~0x8000 | Y170~Y177 |
| 第 8 排 | `0x2008` | 低字节 | 0x0001~0x0080 | Y200~Y207 |
| 第 9 排 | `0x2008` | 高字节 | 0x0100~0x8000 | Y210~Y217 |

**位值规律**：偶数排(2/4/6/8)=低字节，从 `0x0001` 每次左移 1 位；奇数排(1/3/5/7/9)=高字节，从 `0x0100` 每次左移 1 位。

**写入示例**（与现场直觉一致）：
- 只点第 1 排第 1 个 → 写 `0x2004 = 0x0100`
- 第 1 排第 1、2 个同时 → 写 `0x2004 = 0x0100 | 0x0200 = 0x0300`
- 再取消第 1 个 → 写 `0x2004 = 0x0200`

---

## 6. 已验证通讯报文（SSCOM / 调试助手 HEX 直发）

工具：SSCOM（或任意网络调试助手），`TCPClient` 模式，目标 `192.168.1.20:502`，HEX 发送。

### 6.1 读 DI（功能码 0x04）
- 读 0x1000 的 1 个寄存器（DI 1~16）：
  ```
  发送：00 04 00 00 00 06 01 04 10 00 00 01
  回复：00 04 00 00 00 05 01 04 02 00 00     ← 全部 OFF
  回复：00 04 00 00 00 05 01 04 02 00 01     ← 第 1 路 ON（bit0=1）
  ```
- 读 0x1000~0x1001 共 2 个寄存器（32 路）：
  ```
  发送：00 0B 00 00 00 06 01 04 10 00 00 02
  回复：00 0B 00 00 00 07 01 04 04 00 01 00 00   ← 第1寄存器bit0=1，第17~32全OFF
  ```

### 6.2 写 DO（功能码 0x06，回复为原样回显）
```
关闭第 1 路：00 07 00 00 00 06 01 06 20 00 00 00
打开第 1 路：00 06 00 00 00 06 01 06 20 00 00 01
打开第 2 路：00 08 00 00 00 06 01 06 20 00 00 02
同时开1、2：00 09 00 00 00 06 01 06 20 00 00 03
```
> 常用 DO 值速查：`0x0001`=第1路、`0x0002`=第2路、`0x0004`=第3路……（bit 对应关系同 3.2 节）。
> 写入成功返回**请求原样回显**；若返回 `0x80` 开头则为错误码（检查地址/长度/模块支持）。

---

## 7. C# 实现指南（两套库）

### 7.1 测试工程：EasyModbusTCP 5.6.0

NuGet：`Install-Package EasyModbusTCP -Version 5.6.0`

```csharp
using EasyModbus;

// ---- 连接 ----
var client = new ModbusClient("192.168.1.20", 502);
client.ConnectionTimeout = 5000;
client.Connect();                        // 成功后 client.Connected == true

// ---- 读 DI（功能码 0x04）----
client.UnitIdentifier = 0x01;
int[] di = client.ReadInputRegisters(0x1000, 1);   // 读 DI 1~16
bool ch1On = (di[0] & 0x0001) != 0;               // bit0 = 第1路

// ---- 写 DO（功能码 0x06，读-改-写 单点）----
int addr = 0x2004;                                // 例：载台上电第 1 排所在寄存器
int cur  = client.ReadHoldingRegisters(addr, 1)[0]; // 功能码 0x03
int next = (cur & ~0x0100) | (on ? 0x0100 : 0);     // 只改 bit8（Y110）
client.WriteSingleRegister(addr, next);
```

### 7.2 上位机工程：NModbus 3.0.83

NuGet：`Install-Package NModbus -Version 3.0.83`

```csharp
using System.Net.Sockets;
using NModbus;

var client = new TcpClient();
client.Connect("192.168.1.20", 502);
var master = new ModbusFactory().CreateMaster(client);
master.Transport.ReadTimeout  = 3000;
master.Transport.WriteTimeout = 3000;
byte unitId = 1;

// ---- 读 DI（功能码 0x04）----
ushort[] di = master.ReadInputRegisters(unitId, 0x1000, 1);

// ---- 读/写 DO（功能码 0x03 / 0x06）----
ushort[] cur = master.ReadHoldingRegisters(unitId, 0x2004, 1);
ushort next = (ushort)((cur[0] & ~0x0100) | 0x0100);
master.WriteSingleRegister(unitId, 0x2004, next);
```

### 7.3 通用要点
- **批量优于逐点**：读整段（如一次读 5 个 DI 寄存器 / 10 个 DO 寄存器）再按位拆解，避免 72 次网络往返。
- **多线程串行化**：上位机采集线程与 UI 手动写 DO 会同时访问 TCP，须用 `lock` 串行化（生产代码 `ModbusTcpIoController` 里的 `_syncRoot` 即为此）。
- **轮询模型**：Modbus TCP 是请求-响应，设备不主动推送，必须按 `CollectInterval`（默认 1000ms）轮询。

---

## 8. 接入 .NET Framework 4.7.2 上位机（BarometerWinform）

### 8.1 现状（已完成的部分）
生产工程 `BarometerWinform` 中：
- 接口：[IIoController.cs](../../BarometerWinform/Interfaces/IIoController.cs) — `ReadInput/ReadAllInputs/WriteOutput/WriteOutputs/ReadOutput/ReadAllOutputs`
- 实现：[ModbusTcpIoController.cs](../../BarometerWinform/Services/ModbusTcpIoController.cs) — 用 **NModbus**，内部做"连续编号→寄存器+bit"换算 + 读-改-写
- 映射：[IoMapBuilder.cs](../../BarometerWinform/Services/IoMapBuilder.cs) — 内部 IoId ↔ 显耀 X/Y 地址
- 编排：[DeviceManager.cs](../../BarometerWinform/Services/DeviceManager.cs) — 定时采集 + 报警边沿联动（进入报警时关真空阀 + 断载台上电）

### 8.2 配置项（App.config）
```xml
<add key="PlcAddress" value="192.168.1.20" />
<add key="PlcPort" value="502" />
<add key="IoUnitId" value="1" />
<add key="IoInputRegisterStartAddress" value="0x1000" />
<add key="IoOutputRegisterStartAddress" value="0x2000" />
<add key="InvertInputs" value="false" />   <!-- NPN 低有效，必要时取反 -->
<add key="InvertOutputs" value="false" />  <!-- PNP 输出，必要时取反 -->
<add key="TotalInputs" value="80" />
<add key="TotalOutputs" value="160" />
<add key="UseMockCommunication" value="false" />  <!-- false=真实通讯 -->
```

### 8.3 新增接入步骤（供后续开发参考）
1. 实现 `IIoController`（或复用 `ModbusTcpIoController`），把"寄存器+bit"换算抽成与 `IoMapBuilder` 一致的映射。
2. 在 `DeviceManager` 中替换为真实实现（已由 `UseMockCommunication=false` 切换）。
3. 单点写 DO 一律**读-改-写**，且合并同寄存器全部通道状态后整字写回。
4. 报警联动（`HandleAlarm`）关闭指定设备的真空阀 + 载台上电，注意与测试工程一致——**只清对应位，不要整寄存器写 0**（0x2004 低字节是电磁阀 65~72）。

---

## 9. 关键坑点与最佳实践（实测总结）

1. **读保持寄存器返回负值（EasyModbus 特有）**：`ReadHoldingRegisters` 返回 `int[]`，当 bit15=1（值 ≥0x8000）时会被**符号扩展成 32 位负数**（如 0xFFFF → -1）。**必须 `value & 0xFFFF`** 还原为 16 位无符号值，再按位判断。NModbus 返回 `ushort[]` 无此问题。
2. **写单点 DO 必须读-改-写**：`0x2004~0x2008` 全部被两个业务共用（低/高字节），直接整字写会误伤同寄存器其它通道。错误示例：只点第 3 排 Y134 时若只写 `0x2005=0x1000`，会把第 2 排已写入的低字节 `0x00FF` 覆盖成 0，导致 Y120~Y127 全部断电。
3. **必须按寄存器维护状态，而不是按排**：共享同一寄存器的两排按钮状态要一起 OR 合并后写入（PowerOnTestForm 的 `RecomputeRegValue` 即此逻辑）。
4. **0x2004 低字节属于电磁阀**：上位机"载台上电全部关闭"时，若对 0x2004 整字写 0，会连电磁阀 65~72 一起关掉。应只清高字节、保留低字节。
5. **DI 用 0x04、DO 读用 0x03 / 写用 0x06**：别用错功能码（`ReadHoldingRegisters` 读 DI 也是能读的，但 0x04 更规范）。
6. **接线**：SP、FP 都要供 24V；网线同网段且插紧（ST 闪绿但不通=接触不良）。
7. **NPN/PNP 逻辑取反**：若"现场输入灯亮但软件读到 OFF"→ `InvertInputs=true`；"软件写 ON 但现场输出灯灭"→ `InvertOutputs=true`。是否取反只能现场实测确认。
8. **采集间隔**：建议 ≥500ms，避免频繁通讯给设备增加负担。

---

## 10. 调试与排障速查

| 现象 | 排查方向 |
| ---- | -------- |
| SP 不亮 | 24V 未接 SP 端子 / 正负极接反 |
| SYS 红灯常亮/闪烁 | 网络未通 / IP 不同网段；长按复位键 >6s 恢复出厂 |
| AL 红灯闪烁 | FP 电源未接 / IO 模块未插牢 |
| ST 闪绿但通讯失败 | 网线接触不良 / IP 不在同网段 |
| 发送无回复 | 调试助手是否 TCPClient 模式、目标 IP:502 是否正确 |
| 写入返回 0x80 开头错误码 | 地址超限 / 数据长度超限 / 模块不支持该操作 |
| 现场灯亮软件读 OFF | `InvertInputs=true` 试试 |
| 软件写 ON 现场灯灭 | `InvertOutputs=true` 试试 |

---

## 11. 参考资料与文件索引

| 文件 | 说明 |
| ---- | ---- |
| [MainForm.cs](../MainForm.cs) | 连接/读 DI/写 DO/批量写验证 的测试代码 |
| [PowerOnTestForm.cs](../PowerOnTestForm.cs) | 载台上电 9×8 继电器测试窗体（含完整位映射与读-改-写实现） |
| [显耀IO.xlsx](../../显耀IO.xlsx) | 气压表/电磁阀/载台上电 的 X/Y 点位表（原始来源） |
| [通讯接入说明.md](../../通讯接入说明.md) | 整体链路（气压表 RTU + IO TCP）联动说明 |
| [ModbusTcpIoController.cs](../../BarometerWinform/Services/ModbusTcpIoController.cs) | 上位机生产实现（NModbus） |
| [IoMapBuilder.cs](../../BarometerWinform/Services/IoMapBuilder.cs) | 内部编号 ↔ 显耀 X/Y 地址映射 |

---

*文档版本：1.0（2026-08）｜基于 ModbusTcpIoControllerTest 实测结果整理，供后续 .NET Framework 4.7.2 上位机接入开发参考。*
