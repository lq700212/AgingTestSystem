# 冷却送风机 Modbus TCP 通信接口说明文档

## 1. 概述

本文档描述了一个用于与 **冷却送风机控制器** 进行 Modbus TCP 通信的 C# 类库接口。该接口封装了所有底层通信细节，提供简单的异步方法用于读取设备状态、温度、湿度等参数，以及发送启动/停止控制命令。

本组件基于 **NModbus** 库（版本 3.0.x）开发，支持 .NET Framework 4.7.2 及以上环境。

---

## 2. 通信协议

### 物理层
- **传输方式**：TCP/IP（以太网）
- **默认端口**：`50000`（可根据设备实际端口修改，标准 Modbus TCP 端口为 502）

### 协议层
- **协议标准**：Modbus TCP（带 MBAP 报头，**无** CRC 校验）
- **从站地址**：默认为 `1`（可在构造函数中修改）
- **字节序**：大端（寄存器值高字节在前）

### 报文结构（由 NModbus 自动处理）
| 字段                   | 长度（字节） | 说明                                       |
| ---------------------- | ------------ | ------------------------------------------ |
| 事务处理标识符         | 2            | 请求/响应匹配，自动递增                    |
| 协议标识符             | 2            | 固定为 `00 00`（Modbus 协议）              |
| 后续字节数             | 2            | 从单元标识符开始的字节数                   |
| 单元标识符（从站地址） | 1            | 设备地址                                   |
| 功能码                 | 1            | `03`（读保持寄存器），`06`（写单个寄存器） |
| 数据                   | 变长         | 寄存器地址、数量或写入值                   |

---

## 3. 寄存器映射表

| 寄存器地址（十六进制） | 功能       | 读写 | 数据类型 | 说明                                                         |
| ---------------------- | ---------- | ---- | -------- | ------------------------------------------------------------ |
| `0x0000`               | 组合状态   | 只读 | `ushort` | **未使用**（忽略该值）                                       |
| `0x0001`               | 控制/状态  | 读写 | `ushort` | **读取**：`0x0002`=定值停止，`0x0003`=定值启动，`0x0001`=程式启动，`0x0000`=程式停止<br>**写入**：`0x0003`=定值启动，`0x0002`=停止 |
| `0x0002`               | 当前温度   | 只读 | `ushort` | 实际值 = 寄存器值 / 100 （单位：°C）                         |
| `0x0003`               | 当前湿度   | 只读 | `ushort` | 实际值 = 寄存器值 / 100 （单位：%RH）                        |
| `0x0004`               | 温度设定值 | 只读 | `ushort` | 实际值 = 寄存器值 / 100 （单位：°C）                         |
| `0x0005`               | 湿度设定值 | 只读 | `ushort` | 实际值 = 寄存器值 / 100 （单位：%RH）                        |

> **注意**：批量读取时建议一次读取 6 个寄存器（`0x0000` ~ `0x0005`），避免多次通讯。

---

## 4. 类库结构

### 4.1 `FanCommand` 枚举（命令值）
```csharp
public enum FanCommand : ushort
{
    Stop = 0x0002,           // 定值停止
    FixedValueStart = 0x0003 // 定值启动
}
```

> 若需要程式模式，可自行扩展 `ProgramStart = 0x0001` 和 `ProgramStop = 0x0000`。

### 4.2 `FanControllerClient` 类（核心通信类）

#### 构造函数

```csharp
public FanControllerClient(
    string ip,
    int port = 50000,
    byte slaveId = 1,
    int timeoutMs = 3000,
    Action<string> logAction = null
)
```

- **ip**：设备 IP 地址（必填）
- **port**：端口号（默认 50000）
- **slaveId**：从站地址（默认 1）
- **timeoutMs**：通讯超时（毫秒，默认 3000）
- **logAction**：日志回调委托（可选），用于接收调试日志

#### 主要方法（异步）

| 方法                                                         | 说明                                      |
| :----------------------------------------------------------- | :---------------------------------------- |
| `Task StartFixedValueAsync()`                                | 发送定值启动命令                          |
| `Task StopAsync()`                                           | 发送停止命令                              |
| `Task<FanCommand> ReadCurrentStateAsync()`                   | 读取当前运行状态                          |
| `Task<float> ReadTemperatureAsync()`                         | 读取当前温度（°C）                        |
| `Task<float> ReadHumidityAsync()`                            | 读取当前湿度（%RH）                       |
| `Task<float> ReadTemperatureSetpointAsync()`                 | 读取温度设定值（°C）                      |
| `Task<float> ReadHumiditySetpointAsync()`                    | 读取湿度设定值（%RH）                     |
| `Task<(FanCommand State, float Temperature, float Humidity, float TempSetpoint, float HumSetpoint)> ReadAllParametersAsync()` | **推荐** 一次性读取所有参数，减少通讯次数 |

#### 资源释放

```csharp
public void Dispose()
```

调用后关闭 TCP 连接并释放资源。

------

## 5. 使用示例

### 5.1 基本集成（控制台示例）

```csharp
using System;
using System.Threading.Tasks;

namespace YourProject
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // 1. 创建客户端实例
            var client = new FanControllerClient(
                ip: "192.168.1.221",
                port: 50000,
                slaveId: 1,
                timeoutMs: 3000,
                logAction: msg => Console.WriteLine($"[LOG] {msg}")
            );

            try
            {
                // 2. 读取所有参数
                var (state, temp, humidity, tempSet, humSet) = await client.ReadAllParametersAsync();
                Console.WriteLine($"状态: {state}");
                Console.WriteLine($"温度: {temp:F2} °C");
                Console.WriteLine($"湿度: {humidity:F2} %");
                Console.WriteLine($"温度设定: {tempSet:F2} °C");
                Console.WriteLine($"湿度设定: {humSet:F2} %");

                // 3. 发送启动命令
                await client.StartFixedValueAsync();
                Console.WriteLine("定值启动指令已发送");

                // 4. 等待并再次读取状态
                await Task.Delay(2000);
                var newState = await client.ReadCurrentStateAsync();
                Console.WriteLine($"当前状态: {newState}");

                // 5. 停止
                await client.StopAsync();
                Console.WriteLine("停止指令已发送");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"错误: {ex.Message}");
            }
            finally
            {
                client.Dispose();
            }
        }
    }
}
```

### 5.2 WinForm 集成（简单 UI）

在窗体中，创建 `FanControllerClient` 实例，并使用定时器定期调用 `ReadAllParametersAsync()` 刷新显示。

```csharp
private FanControllerClient _client;
private readonly string _ip = "192.168.1.221";
private readonly int _port = 50000;

public Form1()
{
    InitializeComponent();
    _client = new FanControllerClient(_ip, _port, logAction: AppendLog);
}

private async void TimerRefresh_Tick(object sender, EventArgs e)
{
    timerRefresh.Enabled = false;
    var (state, temp, humidity, tempSet, humSet) = await _client.ReadAllParametersAsync();
    // 更新UI控件
    lblState.Text = state == FanCommand.FixedValueStart ? "运行中" : "已停止";
    lblTemp.Text = $"{temp:F2} °C";
    // ...
    timerRefresh.Enabled = true;
}

private async void BtnStart_Click(object sender, EventArgs e)
{
    await _client.StartFixedValueAsync();
}
```

------

## 6. 依赖项

### NuGet 包

- **NModbus**（版本 3.0.83 或兼容版本）

安装命令：

```text
Install-Package NModbus -Version 3.0.83
```

**注意**：不需要安装 `NModbus.Serial`，该包仅用于串口通信，不适用于 TCP。

### 目标框架

- .NET Framework 4.7.2 及以上
- .NET Core / .NET 5+ 也兼容

------

## 7. 注意事项

1. **端口号**：根据实际设备配置修改（本例使用 50000，若为标准 Modbus TCP 则使用 502）。
2. **超时设置**：网络不稳定时可适当增大 `timeoutMs`（如 5000ms）。
3. **线程安全**：`FanControllerClient` 内部使用 `SemaphoreSlim` 保证连接过程的线程安全，可在多线程环境中共享同一个实例。
4. **异常处理**：所有方法可能抛出 `SocketException`、`TimeoutException` 或 `InvalidOperationException`，调用方应捕获并处理。
5. **日志回调**：建议传入日志委托以便调试，生产环境可记录到文件或日志系统。
6. **寄存器地址**：请严格遵循寄存器映射表，若设备固件升级后地址变化需相应调整。
7. **通讯间隔**：避免过于频繁读取（建议 >= 500ms），以免增加设备负担。

------

## 8. 扩展指南

若需增加新的读取参数（如报警信息），可按以下步骤扩展：

1. 在 `FanControllerClient` 中添加新的方法，例如 `ReadAlarmStatusAsync()`。
2. 调用 `ReadHoldingRegistersAsync(startAddress, quantity)` 读取相应的寄存器。
3. 按设备协议解析数据并返回。

若需要写入多个寄存器（如参数设置），可使用 `WriteMultipleRegistersAsync` 方法（功能码 `0x10`），NModbus 已支持。

------

## 9. 版本历史

| 版本 | 日期    | 说明                                    |
| :--- | :------ | :-------------------------------------- |
| 1.0  | 2026-08 | 初始版本，基于实测数据，支持 Modbus TCP |

------

## 附录：通信测试指令（供调试参考）

- **读取全部参数**（6 个寄存器）：

  text

  ```
  00 00 00 00 00 06 01 03 00 00 00 06
  ```

  

- **响应示例**（含数据）：

  text

  ```
  00 00 00 00 00 0F 01 03 0C 10 06 00 02 0C 8F 1D F2 0D AC 13 88
  ```

  

  解析：

  - `00 02` → 定值停止
  - `0C 8F` (3215) → 32.15°C
  - `1D F2` (7666) → 76.66%
  - `0D AC` (3500) → 35.00°C（设定）
  - `13 88` (5000) → 50.00%（设定）

------

**文档结束**
该接口可直接复用于任何需要与上述冷却送风机通信的 C# 项目。