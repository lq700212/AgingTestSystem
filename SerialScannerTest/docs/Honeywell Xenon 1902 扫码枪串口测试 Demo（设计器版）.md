# Honeywell Xenon 1902 扫码枪串口测试 Demo（设计器版）

## 1. 概述
这是一个基于 **.NET Framework 4.7.2** 的 Windows Forms 应用程序，使用窗体设计器布局控件。程序启动后自动识别并连接 Honeywell Xenon 1902 无线扫码枪（虚拟串口模式），接收扫码数据并以 ASCII 格式实时显示。方便开发者快速验证硬件通讯或集成到主项目。

## 2. 技术栈
- 开发框架：.NET Framework 4.7.2
- 界面技术：Windows Forms（设计器生成）
- 串口通讯：`System.IO.Ports`
- 设备识别：`System.Management`（WMI 查询 Win32_PnPEntity）

## 3. 核心功能
- **自动匹配串口**：通过 WMI 查询设备名称中包含 `"Xenon 1902"` 的 COM 口，无需用户手动选择。
- **自动连接**：窗体加载时立即打开串口并开始监听。
- **实时显示**：接收到的数据（ASCII 字符串）逐条追加到文本框，并自动滚动到底部。
- **状态反馈**：底部标签显示当前连接状态。
- **清空显示**：提供清空按钮，方便重复测试。

## 4. 通讯参数
| 参数     | 值     |
| -------- | ------ |
| 波特率   | 115200 |
| 数据位   | 8      |
| 停止位   | 1      |
| 校验位   | None   |
| 流控制   | None   |
| 数据格式 | ASCII  |

## 5. 文件结构
项目包含两个核心文件：

### Form1.Designer.cs
- 定义所有控件的声明（`lblTitle`、`txtReceive`、`lblStatus`、`btnClear`）。
- 在 `InitializeComponent()` 中设置控件的属性、位置、事件绑定。
- **你无需手动修改此文件**，可在设计器中拖拽调整布局。

### Form1.cs
- 包含业务逻辑：串口连接、数据接收、线程安全的 UI 更新。
- 事件处理：`Form1_Load`（自动连接）、`Form1_FormClosing`（释放资源）、`BtnClear_Click`（清空显示）。

## 6. 关键代码说明

### 6.1 设备识别（`FindScannerPort`）
使用 WMI 查询 `Win32_PnPEntity`，筛选 `Name` 字段同时包含 `"COM"` 和 `"Xenon 1902"` 的设备，然后从名称中提取端口号（如 `COM10`）。  
```csharp
using (var searcher = new ManagementObjectSearcher(
    $"SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%COM%' AND Name LIKE '%{_targetDeviceKeyword}%'"))
{
    // 遍历结果并匹配端口名
}
```

> **注意**：若无法识别，请检查设备管理器中显示的设备名称是否包含 `"Xenon 1902"`，如需调整关键字可修改 `_targetDeviceKeyword` 字段。

### 6.2 数据接收（`SerialPort_DataReceived`）

当串口有数据到达时，在后台线程触发事件，通过 `ReadExisting()` 一次性读取所有可用数据（ASCII 文本），并通过 `Invoke` 安全更新 UI。

```csharp
private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
{
    string data = _serialPort.ReadExisting();
    if (!string.IsNullOrEmpty(data))
        AppendText(data);
}
```

### 6.3 线程安全的 UI 更新

所有 UI 更新均通过 `Invoke` 封送到主线程执行，避免跨线程操作异常。

## 7. 使用步骤（基于 Visual Studio）

1. **新建项目**：创建 **Windows Forms 应用 (.NET Framework)**，目标框架选择 **4.7.2**。
2. **添加引用**：在“解决方案资源管理器”中右键“引用” → “添加引用” → 勾选 `System.Management`（用于 WMI 查询）。
3. **替换代码文件**：
   - 将上面的 `Form1.Designer.cs` 内容覆盖到项目中的同名文件（若不存在则新建）。
   - 将上面的 `Form1.cs` 内容覆盖到项目中的同名文件。
   - 如果项目中已有默认的 `Form1` 类，请确保命名空间一致（默认为 `SerialScannerDemo`，可按需调整）。
4. **编译运行**：按 F5 启动，程序将自动连接扫码枪。
5. **测试**：扫描任意条码，数据应实时显示在文本框内。

## 8. 集成到主项目的指引

- **核心模块复用**：将 `AutoConnect`、`FindScannerPort`、`SerialPort_DataReceived` 等方法提取到一个独立的类（如 `ScannerService`）中，与界面分离。
- **数据解析**：在 `SerialPort_DataReceived` 中可对收到的字符串进行预处理（如去除首尾换行符），并触发自定义事件通知上层业务逻辑。
- **错误处理**：增加重连机制（如串口意外断开时自动重试）。
- **配置化**：将波特率、关键字等参数改为从配置文件读取。

## 9. 常见问题与解决

| 问题                       | 解决方法                                                     |
| :------------------------- | :----------------------------------------------------------- |
| 找不到端口                 | 确认扫码枪已设置为 **虚拟串口模式**（需扫描对应的配置码）。  |
| 打开串口失败（访问被拒绝） | 检查该端口是否被其他程序（如调试助手）占用，关闭后重试。     |
| 接收乱码                   | 检查扫码枪输出格式是否为 ASCII（默认），或调整 `ReadExisting` 后的编码处理。 |
| WMI 查询无结果             | 可降级为手动选择端口，或直接使用固定端口（如 `COM10`）进行测试。 |
| 设计器打不开               | 确保两个文件的命名空间和类名一致，且 `Form1.Designer.cs` 中包含了所有控件声明。 |

## 10. 版权与备注

本 Demo 仅供测试与学习使用，不包含任何商业授权。如需在生产环境使用，请确保设备驱动安装正确并遵循 Honeywell 官方文档。

```text
---

现在你只需在 Visual Studio 中新建项目，替换这两个文件，即可获得一个完全基于设计器的测试工具。如果设计器显示异常，请检查两个文件的命名空间是否匹配（示例中均为 `SerialScannerDemo`），并确保 `Form1` 类为 `partial`。有任何问题欢迎继续交流！
```