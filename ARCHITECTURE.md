
# 气压表监控系统 - 架构设计文档

## 1. 项目概述

本项目是一个基于 .NET Framework 4.7.2 的 WinForm 桌面应用程序，用于监控和管理多个气压表设备（当前需求为72个）。系统提供实时数据采集、状态监控、参数配置等功能，并支持设备数量的动态扩展。

### 1.1 功能需求

| 需求项 | 描述 | 当前状态 |
| :--- | :--- | :--- |
| 气压表数据采集 | 实时读取72个气压表的真空压力数据 | 已实现（Mock） |
| IO输入监控 | 监控72个IO输入点状态 | 预留接口 |
| IO输出控制 | 控制144个IO输出点状态 | 预留接口 |
| 动态面板显示 | 根据配置动态创建气压表面板 | 已实现 |
| 参数配置 | 配方管理、延时设置等 | 预留功能 |
| 数据记录 | LOG记录功能 | 预留功能 |

### 1.2 技术栈

- **框架**: .NET Framework 4.7.2
- **UI框架**: WinForms
- **开发语言**: C#
- **开发工具**: Visual Studio 2017+

---

## 2. 项目架构

### 2.1 整体架构图

```
┌─────────────────────────────────────────────────────────────────────┐
│                          MainForm (主视图)                          │
│  ┌─────────────┐  ┌──────────────────────────────┐  ┌─────────────┐ │
│  │   菜单栏    │  │     BarometerPanelView × N    │  │   操作面板  │ │
│  │ (6个按钮)   │  │        (子视图/动态加载)       │  │ (5个按钮)   │ │
│  │  ↓ 下拉菜单 │  └──────────────────────────────┘  └─────────────┘ │
│  └──────┬──────┘                                                    │
└─────────┼───────────────────────────────────────────────────────────┘
          │ 弹出
          ▼
┌─────────────────────────────────────────────────────────────────────┐
│                 Dialogs (对话框窗体层)                              │
│  CommunicationSettingForm │ CommonParameterForm │ RecipeManagerForm │
│  HistoryRecordForm        │ ScanSimulationForm                     │
└─────────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      DeviceManager (服务层)                         │
│  ┌─────────────────────┐  ┌─────────────────────┐                  │
│  │ IBarometerReader    │  │ IIoController       │                  │
│  │ (气压表数据读取)     │  │ (IO输入输出控制)     │                  │
│  │ └─ MockBarometer... │  │ └─ MockIoController │                  │
│  └─────────────────────┘  └─────────────────────┘                  │
└─────────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│                          Models (数据模型层)                         │
│  BarometerData │ IoStatus │ DeviceConfig │ RecipeConfig            │
└─────────────────────────────────────────────────────────────────────┘
```

### 2.2 分层说明

| 层级 | 名称 | 职责 | 关键文件 |
| :--- | :--- | :--- | :--- |
| **视图层** | Views | 负责主UI展示和用户交互 | MainForm.cs, BarometerPanelView.cs |
| **对话框层** | Dialogs | 菜单按钮弹出的子窗体 | CommunicationSettingForm.cs, RecipeManagerForm.cs 等 |
| **服务层** | Services | 负责业务逻辑和硬件通信 | DeviceManager.cs, MockBarometerReader.cs, MockIoController.cs |
| **接口层** | Interfaces | 定义硬件通信标准接口 | IBarometerReader.cs, IIoController.cs |
| **模型层** | Models | 定义数据结构和配置参数 | BarometerData.cs, IoStatus.cs, DeviceConfig.cs, RecipeConfig.cs |

---

## 3. 核心组件详解

### 3.1 视图层 (Views)

#### 3.1.1 MainForm（主窗体）

**职责**: 整个软件的界面框架，包含菜单栏、状态栏、气压表显示区域和操作面板。

**布局结构**:

```
┌─────────────────────────────────────────────────────────────────┐
│ 标题栏: 老化测试系统V1.00 | 权限: 操作员 | PLC状态: 已连接        │
├─────────────────────────────────────────────────────────────────┤
│ 菜单按钮: 用户权限 | 通信设置 | 参数设置 | LOG记录 | TEST | 关于  │
├──────────────────────────────────────┬──────────────────────────┤
│                                      │ 运行状态                 │
│         气压表显示区域               │ 监视(温度)               │
│      (9列 × 8行 = 72个面板)         │ 操作按钮                 │
│                                      │ LOG输出                 │
├──────────────────────────────────────┴──────────────────────────┤
│ 状态栏: 设备数量 | 采集间隔 | 当前时间                          │
└─────────────────────────────────────────────────────────────────┘
```

**关键方法**:

| 方法 | 功能 |
| :--- | :--- |
| `CreateBarometerPanels()` | 动态创建气压表显示面板 + 行全选按钮 Set(SEL_N)，清空前先 Dispose 旧控件（修复 H5） |
| `DeviceManager_OnBatchDataUpdated()` | 处理批量数据更新事件，使用 BeginInvoke 异步切换到 UI 线程（修复 H1/M2） |
| `UpdateAllPanels(allData)` | 一次调用完成所有面板更新，减少 UI 线程切换次数 |
| `BtnSelectRow_Click()` | 行全选按钮点击事件，切换该行所有面板选中状态 |
| `ShowDropdownPopup()` | 在主按钮下方显示下拉菜单（无边框Form + Button列表，尺寸和主按钮一致） |
| `TryLoginAndSwitchPermission(role)` | 【新增】弹出 LoginForm 登录窗体，校验通过后切换权限 |
| `UpdateButtonPermissionStates()` | 【新增】根据当前权限启用/禁用通讯设置和参数设置按钮 |
| `WriteLog()` | 写入日志到右侧 LOG 文本框（带时间戳，限制最大长度避免 GDI 耗尽，修复 M8） |
| `UpdateStatusBar()` | 更新底部状态栏 |
| `UpdateConnectionStatus()` | 更新 PLC 连接状态显示（修复 H1，含 IsDisposed 检查和异常捕获） |
| `LoadConfig()` | 从 App.config 加载所有配置项（修复 H7，含一致性校验） |

#### 3.1.2 BarometerPanelView（气压表显示面板）

**职责**: 单个气压表的显示窗口，展示真空压力、序列号、配方、IO状态等信息。

**布局结构**:

```
┌──────────────────────────────┐
│ NO.1                    空闲 │
│ ┌──────┐ ┌──────┐ ┌──────┐ │
│ │ L1_1 │ │OP1_1 │ │OP1_3 │ │  ← IO状态显示（绿=导通）
│ ├──────┤ ├──────┤ ├──────┤ │
│ │INT1_1│ │ L1_2 │ │OP1_4 │ │
│ └──────┘ └──────┘ └──────┘ │
│ 真空压力: -52300 Pa         │
│ SN:      SN0001             │
│ 配方:    配方1              │
│ 延时开启: 00:10:30  [Set]   │
│ 延时到达: 00:20:15  [Set]   │
└──────────────────────────────┘
```

**关键方法**:

| 方法 | 功能 |
| :--- | :--- |
| `UpdateData(data)` | 更新面板显示数据 |
| `UpdateIoStatus(data)` | 更新IO状态显示 |
| `UpdateStatusColor(status)` | 根据状态更新背景色 |
| `UpdateSelectionStyle()` | 根据选中状态更新背景色（选中=浅蓝，未选中=状态色） |
| `btnSet_Click` | Set 按钮点击事件（合并后），触发 OnSetClicked 通知主窗体 |

**关键属性**:

| 属性 | 类型 | 说明 |
| :--- | :--- | :--- |
| `DeviceId` | int | 设备编号（从1开始，运行时赋值） |
| `IsSelected` | bool | 是否被选中（由主窗体行全选按钮控制，选中时浅蓝高亮） |

**Set 按钮合并说明**:
- 原设计中"延时开启"和"延时到达"各有一个 Set 按钮
- 现合并为一个 Set 按钮，跨两行高度（顶部对齐"延时开启"输入框，底部对齐"延时到达"输入框）
- 点击合并后的 Set 按钮后，主窗体可弹出一个统一的参数设置窗体，让用户选择设置项

#### 3.1.3 顶部菜单按钮下拉菜单

**实现方式**: 使用 `Button` + 无边框弹出窗体（`Form`）组合，点击主按钮时在按钮下方显示下拉菜单。

**为何不使用 ContextMenuStrip**：
- ContextMenuStrip 的菜单项高度由系统绘制，无法和主按钮尺寸对齐
- 改用无边框 Form + TableLayoutPanel + Button 列表方案，每个菜单项都是独立 Button
- 菜单项宽度 = 主按钮宽度，高度 = 主按钮高度，样式（背景色/文字色/字体）完全继承主按钮

**关键方法**: `ShowDropdownPopup(hostButton, items)`
- 创建无边框 Form 作为弹出容器
- 内部用 TableLayoutPanel 垂直排列菜单项按钮
- 点击任意菜单项后自动关闭弹出窗体
- 失去焦点时自动关闭（点击窗体外任何地方）
- 按 Esc 键关闭

**为什么不用 MenuStrip？**
- 保持原有界面的绿色按钮视觉风格
- 不改变原有布局结构
- ContextMenuStrip 灵活控制显示位置

**6个菜单按钮的下拉菜单项**:

| 菜单按钮 | 下拉菜单项 | 触发的窗体/动作 | 权限要求 |
| :--- | :--- | :--- | :--- |
| 用户权限 | 操作员 / 技术员 / 管理员 / 用户管理* | 弹出 LoginForm 输入用户名密码后切换权限；*用户管理仅管理员可见 | 任意权限 |
| 通信设置 | PLC通讯设置 | 弹出 CommunicationSettingForm | 技术员或管理员 |
| 参数设置 | 公共参数 / 配方管理 | 分别弹出 CommonParameterForm / RecipeManagerForm | 技术员或管理员 |
| LOG记录 | 历史记录 | 弹出 HistoryRecordForm | 任意权限 |
| TEST | 扫码模拟 | 弹出 ScanSimulationForm | 任意权限 |
| 关于 | 版本说明 | 弹出版本信息 MessageBox | 任意权限 |

**关键方法**（MainForm.cs）:

| 方法 | 功能 |
| :--- | :--- |
| `InitializeMenuDropdowns()` | 初始化6个下拉菜单及其菜单项 |
| `ShowMenuBelowButton(menu, button)` | 在按钮下方显示下拉菜单 |
| `TryLoginAndSwitchPermission(role)` | 【新增】弹出 LoginForm 登录窗体，校验通过后切换权限 |
| `UpdateButtonPermissionStates()` | 【新增】根据当前权限启用/禁用按钮 |
| `WriteLog(message)` | 写入日志到右侧 LOG 文本框 |

#### 3.1.4 对话框窗体（Dialogs）

所有对话框窗体位于 `Dialogs/` 目录，采用 partial class 拆分（.cs + .Designer.cs）。

| 窗体 | 功能 | 已实现 | 预留项 |
| :--- | :--- | :--- | :--- |
| `CommunicationSettingForm` | PLC通讯设置 | IP/端口/协议/串口参数配置 | 测试连接、参数持久化 |
| `CommonParameterForm` | 公共参数设置 | 采集间隔配置 | 报警阈值等参数需现场确认 |
| `RecipeManagerForm` | 配方管理 | 配方列表显示 | 新增/编辑/删除/持久化 |
| `HistoryRecordForm` | 历史记录查询 | 日期范围查询、Mock日志数据（7天10种事件） | 实际查询逻辑、日志存储路径、导出 |
| `ScanSimulationForm` | 扫码模拟 | 条码输入并触发事件 | 真实扫码枪接入 |
| `LoginForm` | 用户登录 | 用户名/密码输入、登录验证、Enter/Esc 键支持 | 密码哈希存储（当前明文） |
| `UserManagementForm` | 用户账号管理（仅管理员） | 修改操作员/技术员的用户名和密码 | 用户持久化、新增/删除账号 |

**ScanSimulationForm 事件**:
- `OnScanCompleted` 事件：扫码完成时触发，主窗体订阅此事件处理扫码结果。

---

### 3.2 服务层 (Services)

#### 3.2.1 DeviceManager（设备管理器）

**职责**: 管理所有设备的连接、数据采集和状态更新，是系统的核心服务类。

**核心功能**:

1. **设备连接管理**: 连接/断开气压表读取器和IO控制器
2. **定时数据采集**: 通过定时器定期采集所有气压表数据
3. **数据缓存**: 维护所有设备的最新数据
4. **事件通知**: 数据更新和连接状态变更时触发事件

**关键方法**:

| 方法 | 功能 |
| :--- | :--- |
| `Start()` | 启动设备管理器 |
| `Stop()` | 停止设备管理器 |
| `CollectData()` | 执行数据采集 |
| `GetBarometerData(deviceId)` | 获取指定设备的数据 |

**事件**:

| 事件 | 触发时机 |
| :--- | :--- |
| `OnBatchDataUpdated` | 一次采集周期完成时触发一次，参数为所有气压表数据数组（修复 M2，避免逐条触发 72 次事件） |
| `OnConnectionStatusChanged` | 连接状态变更时（Dispose 期间不触发，修复 H3） |

---

### 3.3 接口层 (Interfaces)

#### 3.3.1 IBarometerReader（气压表数据读取接口）

**定义**:

```csharp
public interface IBarometerReader
{
    bool IsConnected { get; }
    bool Connect(DeviceConfig config);
    void Disconnect();
    BarometerData ReadData(int deviceId);
    BarometerData[] ReadAllData();
    event EventHandler<string> OnError;
}
```

**【预留说明】**

当前使用 `MockBarometerReader` 作为模拟实现。实际使用时需要根据现场气压表的通信协议（如 Modbus、RS232 自定义协议等）实现具体的读取类。

**接入方式**:

1. 创建新类实现 `IBarometerReader` 接口（如 `ModbusBarometerReader`）
2. 在 `DeviceManager` 的构造函数中替换 `MockBarometerReader`
3. 根据实际协议实现 `Connect`、`ReadData`、`Disconnect` 方法

#### 3.3.2 IIoController（IO控制器接口）

**定义**:

```csharp
public interface IIoController
{
    bool IsConnected { get; }
    bool Connect(DeviceConfig config);
    void Disconnect();
    bool ReadInput(int inputId);
    bool[] ReadAllInputs();
    void WriteOutput(int outputId, bool state);
    void WriteOutputs(int[] outputIds, bool[] states);
    bool ReadOutput(int outputId);
    bool[] ReadAllOutputs();
    event EventHandler<string> OnError;
}
```

**【预留说明】**

当前使用 `MockIoController` 作为模拟实现。现场有72个IO输入和144个IO输出需要接入。

**IO点编号规则**:

| 类型 | 编号范围 | 说明 |
| :--- | :--- | :--- |
| 输入点 | 1-72 | 共72个输入通道 |
| 输出点 | 73-216 | 共144个输出通道（72+144=216） |

**每个气压表对应**:
- 2个输入点：用于检测传感器信号
- 4个输出点：用于控制执行器

**接入方式**:

1. 创建新类实现 `IIoController` 接口（如 `PlcIoController`）
2. 在 `DeviceManager` 的构造函数中替换 `MockIoController`
3. 根据实际PLC或IO采集卡的通信协议实现各方法

---

### 3.4 模型层 (Models)

#### 3.4.1 BarometerData（气压表数据模型）

| 属性 | 类型 | 说明 |
| :--- | :--- | :--- |
| `DeviceId` | int | 气压表编号（1-72） |
| `VacuumPressure` | decimal | 真空压力值（Pa） |
| `SerialNumber` | string | 设备序列号 |
| `RecipeName` | string | 当前配方名称 |
| `Status` | DeviceStatus | 设备状态（空闲/测试中/故障） |
| `DelayStartTime` | TimeSpan | 延时开启时间 |
| `DelayArriveTime` | TimeSpan | 延时到达时间 |
| `CollectTime` | DateTime | 采集时间戳 |
| `InputStatus` | bool[] | IO输入状态（2个） |
| `OutputStatus` | bool[] | IO输出状态（4个） |

#### 3.4.2 DeviceConfig（设备配置模型）

| 属性 | 类型 | 默认值 | 说明 |
| :--- | :--- | :--- | :--- |
| `TotalBarometers` | int | 72 | 气压表总数 |
| `TotalInputs` | int | 72 | IO输入总数（编号 1 ~ TotalInputs） |
| `TotalOutputs` | int | 144 | IO输出总数（编号 TotalInputs+1 ~ TotalInputs+TotalOutputs） |
| `PortName` | string | COM1 | 通信端口 |
| `BaudRate` | int | 9600 | 波特率 |
| `DataBits` | int | 8 | 数据位 |
| `StopBits` | int | 1 | 停止位 |
| `Parity` | string | None | 校验位 |
| `PlcAddress` | string | 192.168.1.100 | PLC连接地址（预留，待确认协议） |
| `PlcPort` | int | 502 | PLC通讯端口（默认502为Modbus TCP标准端口） |
| `CollectInterval` | int | 1000 | 采集间隔（ms） |
| `PanelColumns` | int | 9 | 面板列数 |
| `PanelRows` | int | 8 | 面板行数 |

#### 3.4.3 RecipeConfig（配方配置模型）

| 属性 | 类型 | 说明 |
| :--- | :--- | :--- |
| `Id` | int | 配方编号 |
| `Name` | string | 配方名称 |
| `NegativePressure` | decimal | 负压值设定 |
| `DelayStartTime` | TimeSpan | 延时开启时间 |
| `DelayArriveTime` | TimeSpan | 延时到达时间 |
| `LimitTemperature` | decimal | 极限温度 |

#### 3.4.4 UserRole（用户角色枚举）

| 枚举值 | 数值 | 说明 |
| :--- | :--- | :--- |
| `Operator` | 0 | 操作员（基础权限，未登录状态） |
| `Technician` | 1 | 技术员（可操作通讯设置和配方参数） |
| `Administrator` | 2 | 管理员（最高权限，可管理其他用户账号） |

**权限规则**：数值越大权限越高，`HasPermission(requiredRole)` 通过 `CurrentUser.Role >= requiredRole` 判断。

#### 3.4.5 UserAccount / LoginResult（用户账号 / 登录结果）

| 类 | 属性/方法 | 说明 |
| :--- | :--- | :--- |
| `UserAccount` | `Username` | 用户名 |
| `UserAccount` | `Password` | 密码（明文，仅演示用） |
| `UserAccount` | `Role` | 角色（UserRole 枚举） |
| `LoginResult` | `Success` | 是否登录成功 |
| `LoginResult` | `User` | 登录成功时返回的账号 |
| `LoginResult` | `ErrorMessage` | 失败原因 |
| `LoginResult` | `Ok(user)` | 静态工厂方法，构造成功结果 |
| `LoginResult` | `Fail(message)` | 静态工厂方法，构造失败结果 |

---

### 3.5 用户权限系统

#### 3.5.1 UserManager（用户管理服务）

**职责**: 维护用户账号、提供登录验证、密码修改、权限校验功能。

**默认账号**（程序启动时初始化，内存中维护）：

| 角色 | 用户名 | 密码 |
| :--- | :--- | :--- |
| 操作员 | operator | 123456 |
| 技术员 | technician | 123456 |
| 管理员 | admin | 123456 |

**关键方法**:

| 方法 | 功能 | 权限要求 |
| :--- | :--- | :--- |
| `Login(targetRole, username, password)` | 校验账号密码，并验证账号是否属于目标角色 | 任意 |
| `Logout()` | 退出登录，恢复未登录状态 | 任意 |
| `UpdateUsername(targetRole, newUsername)` | 修改指定角色的用户名 | 仅管理员 |
| `UpdatePassword(targetRole, newPassword)` | 修改指定角色的密码 | 仅管理员 |
| `GetAccount(role)` | 获取指定角色的账号信息（用于用户管理窗体显示） | 任意 |
| `HasPermission(requiredRole)` | 判断当前用户是否拥有指定权限 | 任意 |

**安全提示**：当前密码以明文存储，实际项目应使用 SHA256/BCrypt 等哈希算法加密保存。

#### 3.5.2 权限登录流程

```
┌──────────────────────────────────────────────────────────────────┐
│ 用户点击"用户权限"按钮                                            │
│           ↓                                                       │
│ 弹出下拉菜单：操作员 / 技术员 / 管理员 / 用户管理*               │
│           ↓                                                       │
│ 用户选择角色（例如"管理员"）                                      │
│           ↓                                                       │
│ 弹出 LoginForm 登录窗体                                           │
│ ┌──────────────────────────────┐                                  │
│ │  切换为 管理员 权限          │                                  │
│ │  用户名: [_______________]   │                                  │
│ │  密  码: [_______________]   │                                  │
│ │       [确认]    [取消]       │                                  │
│ └──────────────────────────────┘                                  │
│           ↓                                                       │
│ 用户输入用户名密码并点击"确认"                                    │
│           ↓                                                       │
│ UserManager.Login() 校验账号密码                                  │
│   ├─ 成功 → 更新 lblPermission，启用/禁用相关按钮，写入日志       │
│   └─ 失败 → 弹出错误提示窗口，用户可重新输入                     │
│                                                                   │
│ *用户管理选项仅管理员可见，点击后弹出 UserManagementForm          │
└──────────────────────────────────────────────────────────────────┘
```

#### 3.5.3 按钮权限控制

| 按钮 | 权限要求 | 行为 |
| :--- | :--- | :--- |
| `btnCommunication`（通信设置） | 技术员或管理员 | 无权限时按钮变灰（Enabled=false） |
| `btnParameter`（参数设置） | 技术员或管理员 | 无权限时按钮变灰（Enabled=false） |
| 其他按钮 | 任意权限 | 始终可用 |

**实现方法**: `UpdateButtonPermissionStates()` 在每次权限切换后调用，根据 `_userManager.HasPermission(UserRole.Technician)` 设置按钮的 `Enabled` 属性。

#### 3.5.4 用户管理窗体（仅管理员）

管理员登录后，下拉菜单会额外显示"用户管理"选项。点击后弹出 `UserManagementForm`：

```
┌──────────────────────────────────────────┐
│           用户账号管理                    │
├──────────────────────────────────────────┤
│ 角色:        [操作员 ▼]                  │
│ 当前用户名:  operator                    │
│ 新用户名:    [____________________]     │
│ 新密码:      [____________________]     │
│ 确认密码:    [____________________]     │
├──────────────────────────────────────────┤
│          [应用修改]    [关闭]            │
└──────────────────────────────────────────┘
```

**校验规则**：
- 留空的字段不修改
- 新密码和确认密码必须一致
- 用户名至少 2 个字符，密码至少 4 个字符
- 用户名不能与其他角色重复
- 不允许修改管理员账号

---

### 3.6 自适应分辨率与滚动条

#### 3.6.1 布局结构

主窗体采用 **根滚动容器 + 锚定布局** 方案实现自适应分辨率：

```
MainForm (WindowState=Maximized, MinimumSize=800×600)
└── rootScrollPanel (Dock=Fill, AutoScroll=true, AutoScrollMinSize=1400×900)
    └── tableLayoutPanelMain (Dock=None, Anchor=Top|Bottom|Left|Right, MinimumSize=1400×900)
        ├── 顶部信息栏（30px 固定高度）
        ├── 菜单按钮栏（40px 固定高度）
        ├── splitContainerMain（100% 占剩余空间）
        │   ├── Panel1: 气压表显示区域（动态加载）
        │   └── Panel2: 右侧操作面板
        └── 底部状态栏（25px 固定高度）
```

#### 3.6.2 工作原理

| 场景 | 行为 |
| :--- | :--- |
| 启动 | 窗体最大化铺满屏幕，内容自动拉伸填满 |
| 窗体放大 | 内容随之放大（Anchor=四方向锚定） |
| 窗体缩小（大于 1400×900） | 内容随之缩小 |
| 窗体缩小（小于 1400×900） | 内容保持最小尺寸 1400×900，自动显示水平和垂直滚动条 |

**关键技术点**：
- `rootScrollPanel.AutoScroll = true`：内容超出可见区域时自动显示滚动条
- `rootScrollPanel.AutoScrollMinSize = (1400, 900)`：声明内容最小尺寸
- `tableLayoutPanelMain.Dock = None` + `Anchor = Top|Bottom|Left|Right`：让布局跟随父容器大小变化
- `tableLayoutPanelMain.MinimumSize = (1400, 900)`：内容不会缩小到低于此尺寸
- `MainForm.WindowState = Maximized`：启动时最大化
- `MainForm.MinimumSize = (800, 600)`：允许用户缩小窗体的最小尺寸

---

## 4. 配置说明

### 4.1 App.config 配置项

配置文件位于 `BarometerWinform/App.config`，支持以下配置项：

| 配置项 | 说明 | 默认值 |
| :--- | :--- | :--- |
| `TotalBarometers` | 气压表总数 | 72 |
| `TotalInputs` | IO输入总数 | 72 |
| `TotalOutputs` | IO输出总数 | 144 |
| `CollectInterval` | 数据采集间隔（毫秒） | 1000 |
| `PanelColumns` | 主视图面板列数 | 9 |
| `PanelRows` | 主视图面板行数 | 8 |
| `PortName` | 通信端口 | COM1 |
| `BaudRate` | 波特率 | 9600 |
| `DataBits` | 数据位 | 8 |
| `StopBits` | 停止位 | 1 |
| `Parity` | 校验位 | None |
| `PlcAddress` | PLC连接地址（预留） | 192.168.1.100 |
| `PlcPort` | PLC通讯端口（修复 L3 补全） | 502 |

### 4.2 动态扩展说明

**增加/减少气压表数量**:

1. 修改 `App.config` 中的 `TotalBarometers` 值
2. 调整 `PanelColumns` 和 `PanelRows` 以适应新的数量（列数×行数≥设备总数）
3. 重新启动应用程序

**示例**: 如果需要100个气压表：
```xml
<add key="TotalBarometers" value="100" />
<add key="PanelColumns" value="10" />
<add key="PanelRows" value="10" />
```

---

## 5. 预留接口与待完善项

### 5.1 待确定的协议/接口

| 项 | 状态 | 说明 |
| :--- | :--- | :--- |
| 气压表通信协议 | **待确定** | 需要根据实际硬件确定（Modbus/RS232/自定义协议） |
| IO通信协议 | **待确定** | 需要根据PLC或IO采集卡确定 |
| PLC连接方式 | **待确定** | 以太网/串口，具体协议待确认 |
| 数据存储方案 | **待确定** | 数据库类型和表结构设计 |

### 5.2 预留的功能接口

| 功能 | 状态 | 文件位置 | 说明 |
| :--- | :--- | :--- | :--- |
| 用户权限管理 | 已实现 | MainForm.cs / UserManager.cs / LoginForm.cs / UserManagementForm.cs | 下拉菜单（操作员/技术员/管理员）+ 登录窗体 + 用户管理（管理员修改他人账号）；用户持久化待实现 |
| 通信设置 | 部分实现 | MainForm.cs / CommunicationSettingForm | PLC通讯设置窗体已实现，参数持久化与测试连接待实现 |
| 参数设置-公共参数 | 部分实现 | MainForm.cs / CommonParameterForm | 公共参数窗体已实现，参数项需现场确认补充 |
| 参数设置-配方管理 | 部分实现 | MainForm.cs / RecipeManagerForm | 配方列表显示已实现，新增/编辑/删除逻辑待实现 |
| LOG记录-历史记录 | Mock实现 | MainForm.cs / HistoryRecordForm | Mock日志数据已实现（7天10种事件），实际查询与导出待实现 |
| TEST-扫码模拟 | 部分实现 | MainForm.cs / ScanSimulationForm | 扫码模拟窗体已实现，真实扫码枪接入待实现 |
| 关于-版本说明 | 已实现 | MainForm.cs | 版本信息弹窗已实现 |
| 行全选按钮 Set(SEL_N) | 部分实现 | MainForm.cs | 按钮已实现，点击切换该行所有面板选中状态（浅蓝高亮）；批量操作逻辑待实现 |
| 面板批量操作 | 预留 | MainForm.cs / BarometerPanelView | IsSelected 属性已实现，批量设置配方/启动等操作待业务流程明确后实现 |
| 温控操作 | 预留 | MainForm.cs | 操作面板按钮已创建，逻辑待实现 |
| 开启真空 | 预留 | MainForm.cs | 操作面板按钮已创建，逻辑待实现 |
| 批量设置配方 | 预留 | MainForm.cs | 操作面板按钮已创建，逻辑待实现 |
| 录入批号 | 预留 | MainForm.cs | 操作面板按钮已创建，逻辑待实现 |
| 启动运行 | 预留 | MainForm.cs | 操作面板按钮已创建，逻辑待实现 |
| 日志持久化 | 预留 | MainForm.cs | 当前仅显示在界面，未写入文件 |
| 配置持久化 | 预留 | Dialogs/*Form.cs | 各设置窗体的配置仅内存生效，未保存到文件 |

### 5.3 硬件接入待确认项

| 项 | 当前假设 | 需要确认 |
| :--- | :--- | :--- |
| 气压表型号 | 通用气压表 | 具体型号和通信协议 |
| IO模块类型 | PLC或IO采集卡 | 具体品牌和型号 |
| 通信接口 | RS232/RS485 | 实际接口类型 |
| 接线方式 | 标准接线 | 具体接线图 |
| 供电方式 | 外部供电 | 是否需要软件控制电源 |

---

## 6. 代码结构

```
BarometerWinform/
├── BarometerWinform.sln                    # 解决方案文件
├── BarometerWinform/
│   ├── BarometerWinform.csproj             # 项目文件
│   ├── App.config                          # 配置文件
│   ├── app.manifest                        # 应用程序清单（DPI感知，修复 L2）
│   ├── Program.cs                          # 入口类（含全局异常处理，修复 L1）
│   ├── Properties/                         # 程序集属性
│   │   └── AssemblyInfo.cs                 # 程序集元数据（设计器依赖）
│   ├── Models/                             # 数据模型层
│   │   ├── BarometerData.cs                # 气压表数据模型
│   │   ├── IoStatus.cs                     # IO状态模型
│   │   ├── DeviceConfig.cs                 # 设备配置模型
│   │   ├── RecipeConfig.cs                 # 配方配置模型
│   │   ├── UserRole.cs                     # 【新增】用户角色枚举（操作员/技术员/管理员）
│   │   └── UserAccount.cs                  # 【新增】用户账号模型 + 登录结果
│   ├── Interfaces/                         # 接口层
│   │   ├── IBarometerReader.cs             # 气压表读取接口
│   │   └── IIoController.cs                # IO控制器接口
│   ├── Services/                           # 服务层
│   │   ├── MockBarometerReader.cs          # 气压表模拟读取器
│   │   ├── MockIoController.cs             # IO模拟控制器
│   │   ├── DeviceManager.cs                # 设备管理器
│   │   └── UserManager.cs                  # 【新增】用户管理服务（登录/改密/权限校验）
│   ├── Views/                              # 视图层
│   │   ├── MainForm.cs                     # 主窗体（业务逻辑）
│   │   ├── MainForm.Designer.cs            # 主窗体（设计器代码，含 rootScrollPanel 滚动容器）
│   │   ├── BarometerPanelView.cs           # 气压表显示面板（业务逻辑）
│   │   └── BarometerPanelView.Designer.cs  # 气压表显示面板（设计器代码）
│   └── Dialogs/                            # 对话框窗体层（菜单按钮弹出窗体）
│       ├── CommunicationSettingForm.cs            # PLC通讯设置窗体
│       ├── CommunicationSettingForm.Designer.cs
│       ├── CommonParameterForm.cs                 # 公共参数设置窗体
│       ├── CommonParameterForm.Designer.cs
│       ├── RecipeManagerForm.cs                   # 配方管理窗体
│       ├── RecipeManagerForm.Designer.cs
│       ├── HistoryRecordForm.cs                   # 历史记录查询窗体
│       ├── HistoryRecordForm.Designer.cs
│       ├── ScanSimulationForm.cs                  # 扫码模拟窗体
│       ├── ScanSimulationForm.Designer.cs
│       ├── LoginForm.cs                           # 【新增】用户登录窗体
│       ├── LoginForm.Designer.cs
│       ├── UserManagementForm.cs                   # 【新增】用户账号管理窗体（仅管理员）
│       └── UserManagementForm.Designer.cs
└── ARCHITECTURE.md                         # 架构文档（本文档）
```

### 6.1 视图层文件拆分说明（重要）

WinForms 视图层采用 **partial class（分部类）** 机制，每个窗体/控件拆分为两个文件：

| 文件 | 职责 | 说明 |
| :--- | :--- | :--- |
| `MainForm.cs` | 业务逻辑 | 事件处理、数据绑定、业务方法 |
| `MainForm.Designer.cs` | 设计器代码 | 控件创建、布局属性、Dispose 方法 |
| `BarometerPanelView.cs` | 业务逻辑 | 数据更新、状态切换、事件处理 |
| `BarometerPanelView.Designer.cs` | 设计器代码 | 控件创建、布局属性、Dispose 方法 |

**为什么需要拆分？**

1. **设计器支持**：Visual Studio 设计器只解析 `.Designer.cs` 文件中的 `InitializeComponent` 方法来渲染设计视图。如果不拆分，设计器会报错（如"无法设计基类 System.Void"、"未能加载基类 System.Windows.Forms.Form"）。
2. **代码分离**：界面布局与业务逻辑分离，便于维护。
3. **团队协作**：设计器修改界面不会影响业务逻辑代码。

**注意事项**：
- `.Designer.cs` 文件由设计器自动维护，**请勿手动修改**
- 所有业务逻辑代码请放在 `.cs` 文件中
- 控件字段声明在 `.Designer.cs` 中，两个文件共享这些字段

---

## 7. 开发指南

### 7.1 硬件接入步骤

**步骤1: 实现气压表读取器**

```csharp
public class ModbusBarometerReader : IBarometerReader
{
    public bool Connect(DeviceConfig config)
    {
        // 实现Modbus连接逻辑
        // TODO: 根据实际协议实现
    }

    public BarometerData ReadData(int deviceId)
    {
        // 实现数据读取逻辑
        // TODO: 根据实际协议实现
    }

    // ... 其他方法
}
```

**步骤2: 实现IO控制器**

```csharp
public class PlcIoController : IIoController
{
    public bool Connect(DeviceConfig config)
    {
        // 实现PLC连接逻辑
        // TODO: 根据实际协议实现
    }

    public bool ReadInput(int inputId)
    {
        // 实现输入读取逻辑
        // TODO: 根据实际协议实现
    }

    // ... 其他方法
}
```

**步骤3: 更新DeviceManager**

```csharp
// 在DeviceManager构造函数中替换实现
_barometerReader = new ModbusBarometerReader();  // 替换 MockBarometerReader
_ioController = new PlcIoController();           // 替换 MockIoController
```

### 7.2 新增功能开发流程

1. **需求分析**: 明确功能需求和接口设计
2. **模型设计**: 在 `Models/` 目录下创建数据模型
3. **接口定义**: 在 `Interfaces/` 目录下定义接口（如需要）
4. **服务实现**: 在 `Services/` 目录下实现业务逻辑
5. **UI开发**: 在 `Views/` 目录下添加界面和交互逻辑
6. **测试验证**: 运行程序验证功能正确性

---

## 8. 维护说明

### 8.1 常见问题排查

| 问题 | 可能原因 | 解决方法 |
| :--- | :--- | :--- |
| 气压表数据不更新 | 连接失败 | 检查串口设置和硬件连接 |
| IO状态异常 | 协议不匹配 | 确认IO控制器实现是否正确 |
| 面板显示不全 | 配置错误 | 检查PanelColumns和PanelRows配置 |
| 程序运行缓慢 | 采集间隔过短 | 增大CollectInterval配置值 |
| 设计器无法预览 | 缺少.Designer.cs文件 | 确保视图层按partial class拆分为.cs和.Designer.cs |
| 设计器报"无法设计基类 System.Void" | 缺少无参构造函数 / .cs 文件无 UTF-8 BOM | 为UserControl添加无参数构造函数；将 .cs 文件保存为 UTF-8 with BOM 编码 |
| 设计器报"未能加载基类 System.Windows.Forms.Form" | .cs 文件无 UTF-8 BOM / obj 缓存损坏 / 缺少 AssemblyInfo.cs | 将 .cs 文件保存为 UTF-8 with BOM 编码；删除 obj/bin 目录重新生成；确保 Properties\AssemblyInfo.cs 已包含在 csproj 中 |
| 程序启动弹窗 TargetParameterCountException | BeginInvoke 传入数组参数被当作 params 展开 | 用 `new object[] { arg }` 显式包装数组参数，详见 H9 修复说明 |

### 8.2 设计器预览问题排查步骤

如果设计器仍然无法预览，请按以下步骤操作：

1. **关闭 Visual Studio**
2. **删除以下目录**（这些是编译缓存，VS会自动重新生成）：
   - `BarometerWinform\obj\`
   - `BarometerWinform\bin\`
   - `.vs\`（VS的缓存目录，隐藏文件夹）
3. **重新打开 Visual Studio**
4. **执行"生成"→"重新生成解决方案"**（快捷键：Ctrl+Shift+B）
5. **双击 MainForm.cs 或 BarometerPanelView.cs 打开设计器**

### 8.2.1 关于 .cs 文件编码（重要）

**所有 .cs 文件必须保存为 UTF-8 with BOM 编码**。

**原因**：
- WinForms 设计器使用 CodeDom 解析 .cs 文件
- 当 .cs 文件包含中文注释但没有 UTF-8 BOM 时，CodeDom 解析器可能错误识别文件编码
- 导致中文注释乱码，进而无法正确解析类声明
- 设计器找不到基类（如 `UserControl`、`Form`），会回退到默认基类 `System.Void`
- 最终报"无法设计基类 System.Void"或"未能加载基类 System.Windows.Forms.Form"错误

**如何检查文件编码**：
- 用 Visual Studio 打开 .cs 文件
- 点击"文件"→"另存为"
- 点击"保存"按钮右侧的下拉箭头
- 选择"编码保存..." → 查看"编码"是否为 `Unicode (UTF-8 带签名) - 代码页 65001`
- 如果不是，选择该编码后保存

**如何批量转换文件编码**（PowerShell 脚本）：
```powershell
$projectRoot = "你的项目路径"
$utf8WithBom = New-Object System.Text.UTF8Encoding($true)
Get-ChildItem -Path $projectRoot -Recurse -Filter "*.cs" -File |
    Where-Object { $_.FullName -notmatch "\\obj\\|\\bin\\" } |
    ForEach-Object {
        $content = [System.IO.File]::ReadAllText($_.FullName, [System.Text.Encoding]::UTF8)
        [System.IO.File]::WriteAllText($_.FullName, $content, $utf8WithBom)
        Write-Host "Converted: $($_.Name)"
    }
```

**已修复的基类声明**：
为增强设计器兼容性，以下文件的基类声明已改为使用完整命名空间路径：
- `BarometerPanelView.cs`：`: UserControl` → `: System.Windows.Forms.UserControl`
- `MainForm.cs`：`: Form` → `: System.Windows.Forms.Form`

### 8.3 版本更新

- 当前版本: V1.08
- 更新日志:
  - V1.08 (2026-07-21): 新增用户权限管理系统（登录窗体 LoginForm + 用户管理窗体 UserManagementForm + UserManager 服务）；主窗体支持自适应屏幕分辨率，缩小时显示水平和垂直滚动条（rootScrollPanel + Anchor 布局）；通讯设置和参数设置按钮需要技术员或管理员权限才能操作；管理员可修改操作员和技术员的用户名密码
  - V1.07 (2026-07-21): 修复设计器无法预览问题（.cs 文件编码）和运行时 TargetParameterCountException 异常，详见第 11 章"V1.07 修复记录"
  - V1.06 (2026-07-21): 全面代码审查与修复（33 项），详见第 10 章"代码审查修复记录"
  - V1.05 (2026-07-21): 下拉菜单改用无边框弹出窗体（Form + TableLayoutPanel + Button 列表）替代 ContextMenuStrip，菜单项尺寸和主按钮完全一致（同宽同高、同背景色、同字体）
  - V1.04 (2026-07-21): BarometerPanelView 合并两个 Set 按钮为一个；新增行全选按钮 Set(SEL_1)~Set(SEL_N)（动态按 PanelRows 生成）；BarometerPanelView 新增 IsSelected 属性（选中时浅蓝高亮）；HistoryRecordForm 新增 Mock 日志数据（最近 7 天，10 种事件类型）
  - V1.03 (2026-07-21): 完善6个菜单按钮的下拉菜单功能，新增 Dialogs 对话框窗体层（PLC通讯设置、公共参数、配方管理、历史记录、扫码模拟）；DeviceConfig 新增 PlcPort 字段；MainForm 新增 WriteLog 日志辅助方法
  - V1.02 (2026-07-21): 修复设计器加载基类失败问题，添加AssemblyInfo.cs和AppDesignerFolder配置；修复DeviceManager线程安全和资源释放问题
  - V1.01 (2026-07-21): 修复设计器无法预览问题，按WinForms标准拆分partial class
  - V1.00: 初始版本，实现基础架构和Mock数据采集

---

## 9. 附录

### 9.1 IO点映射表（参考）

| 气压表编号 | 输入点1 | 输入点2 | 输出点1 | 输出点2 | 输出点3 | 输出点4 |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| 1 | I1_1 | I1_2 | O1_1 | O1_2 | O1_3 | O1_4 |
| 2 | I2_1 | I2_2 | O2_1 | O2_2 | O2_3 | O2_4 |
| ... | ... | ... | ... | ... | ... | ... |
| 72 | I72_1 | I72_2 | O72_1 | O72_2 | O72_3 | O72_4 |

### 9.2 设备状态说明

| 状态 | 显示颜色 | 含义 |
| :--- | :--- | :--- |
| 空闲 | 绿色 | 设备未进行测试 |
| 测试中 | 橙色 | 设备正在进行老化测试 |
| 故障 | 红色 | 设备出现故障 |

### 9.3 快捷键（预留）

| 快捷键 | 功能 | 状态 |
| :--- | :--- | :--- |
| Ctrl+S | 保存配置 | 预留 |
| Ctrl+R | 刷新数据 | 预留 |
| F5 | 启动/停止采集 | 预留 |

---

## 10. 代码审查修复记录（V1.06）

本章记录 V1.06 版本的全面代码审查结果，共修复 33 项问题（8 项高危 + 14 项中等 + 11 项低危）。
所有修复均保留注释说明，标注修复编号（H/M/L + 序号）。

### 10.1 高危修复（8 项）

| 编号 | 问题 | 文件 | 修复方式 |
| :--- | :--- | :--- | :--- |
| H1 | 跨线程 Invoke 未检查 IsDisposed，窗体释放时抛 ObjectDisposedException | MainForm.cs | 增加 IsDisposed/Disposing 检查，改用 BeginInvoke，捕获 ObjectDisposedException/InvalidOperationException |
| H2 | DeviceManager.Start() 部分连接失败时资源泄漏 | DeviceManager.cs | 连接失败时回滚已建立的连接（Disconnect） |
| H3 | Dispose 期间触发事件回调到已释放的 UI | DeviceManager.cs | _disposed 改为 volatile，Dispose 时先置 true 再 Stop，Stop 中检查 ! disposed 跳过事件 |
| H4 | 事件订阅未取消，导致内存泄漏 | MainForm.cs | FormClosing 中先 -= 取消订阅再 Dispose |
| H5 | Controls.Clear() 不 Dispose 旧控件，GDI 句柄泄漏 | MainForm.cs | Clear 前先 foreach Dispose 所有子控件 |
| H6 | NumericUpDown 赋值未做范围校验，抛 ArgumentOutOfRangeException | CommonParameterForm.cs / CommunicationSettingForm.cs | 新增 ClampToNumericRange 辅助方法 |
| H7 | LoadConfig 未读取所有 App.config 配置项 | MainForm.cs | 补全 PanelColumns/Rows/Inputs/Outputs/PlcAddress/Port/PlcPort 等读取 + 一致性校验 |
| H8 | 选中色遮蔽故障告警（故障应始终红色） | BarometerPanelView.cs | UpdateStatusColor 中故障状态优先级最高，直接返回红色 |

### 10.2 中危修复（14 项）

| 编号 | 问题 | 文件 | 修复方式 |
| :--- | :--- | :--- | :--- |
| M1 | Start() 未在定时器启动前采集首次数据 | DeviceManager.cs | Start() 中先调用 CollectData() 再 Start 定时器 |
| M2 | 逐条触发 72 次 OnDataUpdated 事件导致 UI 卡顿 | DeviceManager.cs / MainForm.cs | 改为 OnBatchDataUpdated 批量事件，一次采集只触发一次 |
| M3 | GetBarometerData 返回缓存引用，外部修改污染缓存 | DeviceManager.cs / BarometerData.cs | 返回 Clone() 深拷贝（含数组字段深拷贝） |
| M4 | 错误回调用 lambda，无法取消订阅 | DeviceManager.cs | 改为命名方法 BarometerReader_OnError / IoController_OnError |
| M5 | Random 非线程安全，并发访问返回 0 或抛异常 | MockBarometerReader.cs / MockIoController.cs | 新增 _randomLock，所有 _random.Next 调用包裹 lock |
| M6 | ReadAllData 未检查 _config 是否为 null | MockBarometerReader.cs | 增加 _config == null 判断，返回空数组并触发 OnError |
| M7 | MockIoController 硬编码 73/216 范围，不随配置变化 | MockIoController.cs | 改为动态计算 outputStart = TotalInputs+1, outputEnd = TotalInputs+TotalOutputs |
| M8 | WriteLog 无长度限制，长时间运行 GDI 句柄耗尽 | MainForm.cs | 新增 MaxLogTextLength 常量（10万），超过时裁剪保留最近一半 |
| M9 | 弹出窗体事件未取消订阅，闭包持有引用泄漏 | MainForm.cs | FormClosed 中 -= Deactivate/KeyDown 事件 |
| M10 | flowLayoutPanelPanels 僵尸字段（Designer 创建后被运行时 Clear 替换） | MainForm.Designer.cs | 移除 flowLayoutPanelPanels 的创建、属性、字段声明 |
| M11 | 无参构造函数缺少"设计器专用"警告注释 | BarometerPanelView.cs | 增强注释说明运行时勿用 |
| M12 | RecipeManagerForm null 参数静默创建新列表，修改不反映外部 | RecipeManagerForm.cs | 改为 throw ArgumentNullException |
| M13 | HistoryRecordForm 每次生成日志都创建新数组 | HistoryRecordForm.cs | 提取 PermissionNames/RecipeNames 为 static readonly |
| M14 | txtBarcode_KeyDown 调用 btnScan_Click(sender, e) 把 KeyEventArgs 当 EventArgs | ScanSimulationForm.cs | 抽取 DoScan() 方法，两处都调用 DoScan() |

### 10.3 低危修复（11 项）

| 编号 | 问题 | 文件 | 修复方式 |
| :--- | :--- | :--- | :--- |
| L1 | 缺少全局异常处理，未捕获异常导致静默崩溃 | Program.cs | 注册 Application.ThreadException 和 AppDomain.UnhandledException |
| L2 | 未启用 DPI 感知，高 DPI 屏幕界面模糊 | app.manifest / .csproj | 新增 app.manifest 声明 dpiAware + PerMonitorV2，.csproj 引用 |
| L3 | App.config 缺少 PlcPort 配置项 | App.config | 新增 PlcPort=502 |
| L4 | DeviceConfig 注释与值矛盾（说2个输入实际1个） | DeviceConfig.cs | 更正注释为与配置值一致 |
| L5 | TextBox 字段命名为 lbl 前缀（应为 txt） | MainForm.Designer.cs | lblLowerTemp/UpperTemp/SetTemp 重命名为 txt 前缀 |
| L6 | 代码中散落魔法数字（220、80、100000 等） | MainForm.cs | 提取为 const 常量（PanelRowHeight/RowSelectButtonColumnWidth/MaxLogTextLength） |
| L7 | 颜色对象每个面板实例都持有一份（72 份副本） | BarometerPanelView.cs | 改为 static readonly，所有实例共享 |
| L8 | UpdateStatusColor 和 UpdateStatusColorOnly 重复 switch | BarometerPanelView.cs | 抽取 GetStatusBackColor 公共方法，删除 UpdateStatusColorOnly |
| L9 | ReadData 未校验 deviceId 边界 | MockBarometerReader.cs | 增加 deviceId ∈ [1, TotalBarometers] 校验 |
| L10 | switch 缺少 default 分支 | BarometerPanelView.cs | 添加 default 返回空闲色 |
| L11 | 冗余 using 语句 | 多文件 | 编译验证通过，各文件 using 均有实际引用 |

### 10.4 编译验证

- 修复后编译结果：**0 警告，0 错误**
- 编译命令：`dotnet build BarometerWinform.sln --configuration Debug`
- 清理操作：删除 obj 目录后重新编译，确保无缓存干扰

### 10.5 修复涉及的文件清单

| 文件 | 修复编号 |
| :--- | :--- |
| Services/DeviceManager.cs | H2, H3, M1, M2, M3, M4 |
| Views/MainForm.cs | H1, H4, H5, H7, M2, M8, M9, L6 |
| Views/MainForm.Designer.cs | L5, M10 |
| Views/BarometerPanelView.cs | H8, L7, L8, L10, M11 |
| Dialogs/CommonParameterForm.cs | H6 |
| Dialogs/CommunicationSettingForm.cs | H6 |
| Dialogs/RecipeManagerForm.cs | M12 |
| Dialogs/HistoryRecordForm.cs | M13 |
| Dialogs/ScanSimulationForm.cs | M14 |
| Services/MockBarometerReader.cs | M5, M6, L9 |
| Services/MockIoController.cs | M5, M7 |
| Models/BarometerData.cs | M3（新增 Clone 方法） |
| Models/DeviceConfig.cs | L4 |
| Program.cs | L1, L2 |
| app.manifest（新增） | L2 |
| BarometerWinform.csproj | L2 |
| App.config | L3 |

---

## 11. V1.07 修复记录

本章记录 V1.07 版本修复的 3 个紧急问题（2 项高危 + 1 项运行时崩溃）。

### 11.1 问题描述

V1.06 版本编译通过后，发现以下两个严重问题：

1. **设计器无法预览**：
   - `BarometerPanelView.cs` 报错："无法设计基类 System.Void"
   - `MainForm.cs` 报错："未能加载基类 System.Windows.Forms.Form。请确保已引用该程序集并已生成所有项目"

2. **运行时崩溃**：
   - 程序启动后立即弹窗报错：
     ```
     程序发生异常(UI线程):
     TargetParameterCountException参数计数不匹配。
     在 System.Reflection.RuntimeMethodInfo.InvokeArgumentsCheck(Object obj, BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture)
     在 System.Delegate.DynamicInvokeImpl(Object[] args)
     在 System.Windows.Forms.Control.InvokeMarshaledCallbackDo(ThreadMethodEntry me)
     ...
     ```

### 11.2 修复明细

#### H9 - 运行时 TargetParameterCountException 参数计数不匹配

**问题文件**：[Views/MainForm.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Views/MainForm.cs)

**问题代码**：
```csharp
this.BeginInvoke(new Action<BarometerData[]>(UpdateAllPanels), allData);
```

**问题原因**：
- `Control.BeginInvoke` 的签名是 `BeginInvoke(Delegate method, params object[] args)`
- 当第二个参数 `allData`（`BarometerData[]` 类型）传入时，由于数组协变规则（`BarometerData[]` 可隐式转换为 `object[]`），编译器把 `allData` 当作 `params object[]` 的展开值直接传入
- 即把数组中的每个 `BarometerData` 元素都当作委托的一个参数
- 委托被调用时实际收到 N 个参数（N=allData.Length，如72个），但 `Action<BarometerData[]>` 只接受 1 个参数
- 参数个数不匹配 → 抛出 `TargetParameterCountException`

**修复方法**：
```csharp
this.BeginInvoke(
    new Action<BarometerData[]>(UpdateAllPanels),
    new object[] { allData });  // 显式包装为 object[]，长度为1
```

**修复原理**：
- 显式构造 `new object[] { allData }`，长度为 1
- `BeginInvoke` 内部调用 `DynamicInvoke(new object[] { allData })`
- 取 `object[0]`（即 `allData` 本身）作为委托的唯一参数
- 与 `Action<BarometerData[]>` 签名匹配，参数计数正确

**注意事项**：
- 任何使用 `Control.Invoke`/`BeginInvoke` 传递**数组类型参数**时，都必须用 `new object[] { ... }` 显式包装
- 这是 WinForms 跨线程调用的常见陷阱，编译器不会报错，但运行时会抛 `TargetParameterCountException`

#### H10 - 设计器报"无法设计基类 System.Void" / "未能加载基类"

**问题文件**：
- [Views/BarometerPanelView.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Views/BarometerPanelView.cs)
- [Views/MainForm.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Views/MainForm.cs)

**问题原因**：
- 所有 .cs 文件保存为 **UTF-8 without BOM** 编码
- VS WinForms 设计器使用 CodeDom 解析 .cs 文件
- 当 .cs 文件包含中文字符但无 UTF-8 BOM 时，CodeDom 解析器无法正确识别文件编码
- 导致中文注释乱码，进而无法正确解析类声明和 using 语句
- 设计器找不到基类（`UserControl`/`Form`），回退到默认基类 `System.Void`
- 最终报"无法设计基类 System.Void"或"未能加载基类"错误

**修复方法**：
1. **将所有 25 个 .cs 文件转换为 UTF-8 with BOM 编码**
   - 使用 PowerShell 脚本批量转换（脚本见第 8.2.1 节）
   - 转换前：所有文件无 BOM
   - 转换后：所有文件均带 UTF-8 BOM（`EF BB BF`）

2. **将基类声明改为完整命名空间路径**（增强设计器兼容性）
   - `BarometerPanelView.cs`：`: UserControl` → `: System.Windows.Forms.UserControl`
   - `MainForm.cs`：`: Form` → `: System.Windows.Forms.Form`
   - 即使 using 语句解析失败，设计器仍能通过完整路径找到基类

3. **清理设计器缓存**
   - 删除 `obj/Debug/DesignTimeResolveAssemblyReferences.cache`
   - 删除 `obj/Debug/DesignTimeResolveAssemblyReferencesInput.cache`
   - 删除 `obj/Debug/BarometerWinform.csproj.AssemblyReference.cache`
   - 删除 `obj/Debug/BarometerWinform.csproj.CoreCompileInputs.cache`
   - VS 重新打开设计器时会自动重新生成这些缓存

### 11.3 修复涉及的文件清单

| 文件 | 修复内容 |
| :--- | :--- |
| Views/MainForm.cs | H9: 修复 BeginInvoke 参数传递；H10: 基类改为完整命名空间 |
| Views/BarometerPanelView.cs | H10: 基类改为完整命名空间 |
| 所有 .cs 文件（25个） | H10: 转换为 UTF-8 with BOM 编码 |
| obj/Debug/*.cache | 清理设计器缓存文件 |
| ARCHITECTURE.md | 文档同步更新 |

### 11.4 编译验证

- 修复后编译结果：**0 警告，0 错误**
- 编译命令：`MSBuild BarometerWinform.sln /t:Rebuild /p:Configuration=Debug /v:minimal`
- 验证步骤：
  1. 用 PowerShell 检查所有 .cs 文件前 3 字节是否为 `EF BB BF` ✓
  2. 重新编译项目，确认无错误无警告 ✓
  3. 用户需在 VS 中关闭 → 重新打开 MainForm.cs / BarometerPanelView.cs 验证设计器可预览

### 11.5 验证清单（用户操作）

修复已应用，但用户需在 VS 中完成以下操作才能完全生效：

1. **关闭 Visual Studio**
2. **删除缓存目录**（VS 会自动重新生成）：
   - `BarometerWinform\obj\`
   - `BarometerWinform\bin\`
   - `.vs\`（隐藏文件夹）
3. **重新打开 Visual Studio**，加载解决方案
4. **重新生成解决方案**：Ctrl+Shift+B
5. **双击打开设计器**：
   - `Views\MainForm.cs` - 应能正常显示主窗体设计视图
   - `Views\BarometerPanelView.cs` - 应能正常显示气压表面板设计视图
6. **运行程序**：F5 启动，确认不再弹窗 `TargetParameterCountException`
