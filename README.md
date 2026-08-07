
# 气压表监控系统 - 架构设计文档

## 1. 项目概述

本项目是一个基于 .NET Framework 4.7.2 的 WinForm 桌面应用程序，用于监控和管理多个气压表设备（当前需求为72个）。系统提供实时数据采集、状态监控、参数配置等功能，并支持设备数量的动态扩展。

### 1.1 功能需求

| 需求项 | 描述 | 当前状态 |
| :--- | :--- | :--- |
| 气压表数据采集 | 实时读取72个气压表的真空压力数据 | 已实现（Modbus RTU，Mock/真实可切换） |
| IO输入监控 | 监控72个IO输入点状态(NPN, X000~X107) | 已接入显耀IO表（Modbus TCP，Mock/真实可切换） |
| IO输出控制 | 控制144个IO输出点状态(PNP, Y000~Y217) | 已接入显耀IO表（Modbus TCP，Mock/真实可切换） |
| IO点映射表 | 内部编号与三菱PLC物理地址的映射 | 已实现（IoMapBuilder） |
| 动态面板显示 | 根据配置动态创建气压表面板 | 已实现 |
| 冷却送风机控制 | 定值启动/停止 + 温度/湿度监视 | 已实现（V1.15，Modbus TCP） |
| 老化测试业务 | 启动运行 → 真空确认 → 老化计时 → 自动停止 | 已实现（V1.15） |
| 报警联动 | 压力越限/真空建立失败/通讯失联 → 关阀+断电 | 已实现（V1.12/V1.15） |
| 用户权限管理 | 操作员/技术员/管理员登录与权限控制 | 已实现 |
| 参数配置 | 采集间隔、报警阈值、配方管理等 | 部分实现（配置持久化待完成） |
| 数据记录 | 事件日志 CSV 落盘 + 历史记录查询 | 已实现（V1.15，TestEventLogger） |

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
│                          MainForm (主视图)                           │
│  ┌─────────────┐  ┌──────────────────────────────┐  ┌─────────────┐ │
│  │   菜单栏     │  │     BarometerPanelView × N   │  │   操作面板    │ │
│  │ (6个按钮)    │  │        (子视图/动态加载)       │   │ (9个按钮)    │ │
│  │  ↓ 下拉菜单  │  └──────────────────────────────┘   └─────────────┘ │
│  └──────┬──────┘                                                    │
└─────────┼───────────────────────────────────────────────────────────┘
          │ 弹出
          ▼
┌─────────────────────────────────────────────────────────────────────┐
│                 Dialogs (对话框窗体层)                                │
│  CommonParameterForm │ RecipeManagerForm │ HistoryRecordForm   │
│  ScanSimulationForm │ DeviceManualForm (V1.15)                 │
└─────────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      DeviceManager (服务层)                          │
│  ┌──────────────────┐ ┌──────────────────┐ ┌──────────────────────┐ │
│  │ IBarometerReader │ │ IIoController    │ │ IFanController       │ │
│  │ (气压表读取)      │ │ (IO输入输出控制)   │ │ (送风机定值启停)      │ │
│  │ └MockBarometer…  │ │ └MockIoController│ │ └MockFanController   │ │
│  └──────────────────┘ └──────────────────┘ └──────────────────────┘ │
│  └─ TestEventLogger：测试事件写 CSV，供历史记录查询 (V1.15)            │
└─────────────────────────────────────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│                          Models (数据模型层)                          │
│  BarometerData │ FanData(V1.15) │ IoStatus │ DeviceConfig │ Recipe   │
└─────────────────────────────────────────────────────────────────────┘
```

### 2.2 分层说明

| 层级 | 名称 | 职责 | 关键文件 |
| :--- | :--- | :--- | :--- |
| **视图层** | Views | 负责主UI展示和用户交互 | MainForm.cs, BarometerPanelView.cs |
| **对话框层** | Dialogs | 菜单按钮弹出的子窗体 | RecipeManagerForm.cs, DeviceManualForm.cs 等 |
| **服务层** | Services | 负责业务逻辑和硬件通信（气压表 / IO / 送风机） | DeviceManager.cs, ModbusRtuBarometerReader.cs, ModbusTcpIoController.cs, FanControllerClient.cs, MockFanController.cs, TestEventLogger.cs 等 |
| **接口层** | Interfaces | 定义硬件通信标准接口 | IBarometerReader.cs, IIoController.cs, IFanController.cs |
| **模型层** | Models | 定义数据结构和配置参数 | BarometerData.cs, FanData.cs, IoStatus.cs, DeviceConfig.cs, RecipeConfig.cs |

---

## 3. 核心组件详解

### 3.1 视图层 (Views)

#### 3.1.1 MainForm（主窗体）

**职责**: 整个软件的界面框架，包含菜单栏、状态栏、气压表显示区域和操作面板。

**布局结构**:

```
┌─────────────────────────────────────────────────────────────────┐
│ 标题栏: 老化测试系统V1.15 | 权限: 操作员 | PLC状态: 已连接           │
├─────────────────────────────────────────────────────────────────┤
│ 菜单按钮: 用户权限 | 参数设置 | LOG记录 | TEST | 关于              │
├──────────────────────────────────────┬──────────────────────────┤
│                                      │ 运行状态                  │
│         气压表显示区域                  │ 送风机监视                  │
│      (9列 × 8行 = 72个面板)            │ (状态/当前温度/当前湿度/设定) │
│       每个面板带 Set 按钮               │ 操作                      │
│                                      │ (送风机启停/开启真空/启动/   │
│                                      │  停止/复位/急停/配方/批号)  │
│                                      │ LOG输出                   │
├──────────────────────────────────────┴──────────────────────────┤
│ 状态栏: 设备数量 | 采集间隔 | 测试中 | 在线 | 当前时间                │
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
| `UpdateButtonPermissionStates()` | 【新增】根据当前权限启用/禁用参数设置按钮 |
| `WriteLog()` | 写入日志到右侧 LOG 文本框（带时间戳，限制最大长度避免 GDI 耗尽，修复 M8） |
| `UpdateStatusBar()` | 更新底部状态栏 |
| `UpdateConnectionStatus()` | 更新 PLC 连接状态显示（修复 H1，含 IsDisposed 检查和异常捕获） |
| `LoadConfig()` | 从 App.config 加载所有配置项（修复 H7，含一致性校验） |
| `DeviceManager_OnFanDataUpdated()` | 处理送风机数据更新事件（BeginInvoke 异步切到 UI 线程） |
| `UpdateFanDisplay(data)` | 更新送风机监视区显示（状态/温度/湿度/设定值） |
| `UpdateRunStatusSummary()` | 更新状态栏"测试中/在线"统计 |
| `GetSelectedDeviceIds()` | 获取选中的设备编号列表，未选中时弹出提示 |

#### 3.1.2 BarometerPanelView（气压表显示面板）

**职责**: 单个气压表的显示窗口，展示真空压力、序列号、配方、IO状态等信息。

**布局结构**:

```
┌──────────────────────────────┐
│ NO.1                    空闲  │
│ ┌──────┐ ┌──────┐ ┌──────┐   │
│ │ L1_1 │ │OP1_1 │ │OP1_3 │   │  ← IO状态显示（绿=导通）
│ ├──────┤ ├──────┤ ├──────┤   │
│ │INT1_1│ │ L1_2 │ │OP1_4 │   │
│ └──────┘ └──────┘ └──────┘   │
│ 真空压力: -52300 Pa            │
│ SN:      SN0001              │
│ 配方:    配方1                 │
│                   ┌──────┐   │
│ 延时开启: 00:10:30  │ Set  │   │
│ 延时到达: 00:20:15  │      │   │
│                   └──────┘   │
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

**5个菜单按钮的下拉菜单项**:

| 菜单按钮 | 下拉菜单项 | 触发的窗体/动作 | 权限要求 |
| :--- | :--- | :--- | :--- |
| 用户权限 | 操作员 / 技术员 / 管理员 / 用户管理* | 弹出 LoginForm 输入用户名密码后切换权限；*用户管理仅管理员可见 | 任意权限 |
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
| `CommonParameterForm` | 公共参数设置 | 采集间隔 + 报警压力阈值（写回内存配置） | 参数持久化待实现 |
| `RecipeManagerForm` | 配方管理 | 左右分栏布局：左侧配方列表（序号+配方名称），右侧配方设置（配方名称、延时时间、启动时间、极限温度），底部添加/更新/删除按钮和保存设置按钮 | 新增/编辑/删除/持久化待实现 |
| `HistoryRecordForm` | 历史记录查询 | 【V1.15】日期范围查询 + 读取 Logs\TestLog_*.csv 真实事件日志（自动跨 CSV 解析、跳过表头），"导出"按钮打开 Logs 文件夹 | Mock 数据已移除 |
| `ScanSimulationForm` | 扫码模拟 | 条码输入并触发事件 | 真实扫码枪接入 |
| `LoginForm` | 用户登录 | 用户名/密码输入、登录验证、Enter/Esc 键支持 | 密码哈希存储（当前明文） |
| `UserManagementForm` | 用户账号管理（仅管理员） | 修改操作员/技术员的用户名和密码，用户数据持久化到 Users.json 文件 | 新增/删除账号 |
| `BatchRecipeForm` | 批量设置配方 | 配方名称、延时时间1/2（时:分:秒）、启动时间（时:分:秒）、极限温度输入，加入队列功能，配方队列管理 | 配方批量应用到选中面板待实现 |
| `InputLotForm` | 录入批号 | 批号输入框、红色背景注释提示、确定/取消按钮、Enter键支持、输入校验，确定后弹出ID绑定界面 | 批号持久化、关联生产记录待实现 |
| `IdBindingForm` | ID绑定 | 批号显示（只读）、工位编号输入框、SN输入框、红色背景注释说明、产品列表显示（带滚动条）、保存按钮、重复工位覆盖确认、Enter键支持、Excel文档生成（命名规则：批号_日期_时间.xlsx） | ID绑定数据持久化待实现 |

**ScanSimulationForm 事件**:
- `OnScanCompleted` 事件：扫码完成时触发，主窗体订阅此事件处理扫码结果。

**BatchRecipeForm 事件**:
- `OnRecipeAdded` 事件：配方加入队列时触发，主窗体订阅此事件记录日志。

**InputLotForm 事件**:
- `OnLotInputCompleted` 事件：批号录入完成（ID绑定保存成功）时触发，主窗体订阅此事件获取录入的批号。

**IdBindingForm 事件**:
- `OnBindingCompleted` 事件：ID绑定保存时触发，参数为 `Tuple<string, List<ProductBinding>>`（批号 + 产品绑定列表）。
- `ProductBinding` 类：包含 `StationNo`（工位编号）、`Sn`（产品SN）、`RecipeName`（配方名称）、`DelayTime`（延时时间）、`StartTime`（启动时间）属性。
- Excel文档生成：保存时自动生成Excel文档，命名规则为 `批号_日期_时间.xlsx`，包含批号、工位号、SN、配方名称、延时时间、启动时间列。

---

### 3.2 服务层 (Services)

#### 3.2.1 DeviceManager（设备管理器）

**职责**: 管理所有设备的连接、数据采集和状态更新，是系统的核心服务类。

**核心功能**:

1. **设备连接管理**: 连接/断开气压表读取器、IO控制器和送风机控制器
2. **定时数据采集**: 定时采集气压表数据（采集间隔 CollectInterval）；送风机用独立定时器轮询（2s），互不阻塞
3. **数据缓存**: 维护所有设备的最新数据
4. **事件通知**: 数据更新、连接状态变更、送风机数据更新时触发事件
5. **测试状态机**: StartTesting（开真空+载台上电+送风机定值启动+进测试+真空确认）/ StopTesting / ResetDevices / StopAll（急停）
6. **报警联动**: 压力越限 / 真空建立超时 / 通讯失联 / DI触点(可选) → 边沿触发关阀+断电+标故障
7. **送风机全局生命周期**: 有任一台在测试 → 保持运行；全部停止 → 才允许停机

**关键方法**:

| 方法 | 功能 |
| :--- | :--- |
| `Start()` | 启动设备管理器（连接气压表/IO/送风机，启动采集定时器） |
| `Stop()` | 停止设备管理器（停止定时器、断开连接） |
| `CollectData()` | 执行一轮气压表数据采集与报警判定 |
| `GetBarometerData(deviceId)` | 获取指定设备的数据（Clone 深拷贝） |
| `GetAllBarometerData()` | 获取全部设备数据快照 |
| `StartTesting(deviceIds)` | 启动选中台老化测试（开真空+载台上电+送风机定值启动+进测试+真空确认） |
| `StopTesting(deviceIds)` | 停止选中台测试（关阀+断电，末台时送风机自动停止） |
| `ResetDevices(deviceIds)` | 报警复位，清除故障状态，可重新测试 |
| `StopAll()` | 急停：全关阀 + 全断电 + 停送风机 |
| `StartFan()` / `StopFan()` | 手动定值启动/停止送风机 |
| `GetFanData()` | 获取送风机最新状态（缓存） |
| `SetOutput(outputId, state)` | 写单个 IO 输出（供单台手动控制等） |
| `GetOutput(outputId)` / `GetInput(inputId)` | 读单个 IO 输出/输入状态 |
| `SetBarometerThreshold(deviceId, v)` | 【V1.15 新增】写单台**设备阈值**（透传 IBarometerReader.SetThreshold，设备单位，非 Pa） |
| `SetAllBarometerThresholds(v)` | 【V1.15 新增】批量写全部**设备阈值**，返回失败名单（透传 SetAllThresholds，应在后台线程调用） |
| `GetTestingCount()` / `GetOnlineCount()` | 统计测试中/在线台数（状态栏显示） |

**事件**:

| 事件 | 触发时机 |
| :--- | :--- |
| `OnBatchDataUpdated` | 一次采集周期完成时触发一次，参数为所有气压表数据数组（修复 M2，避免逐条触发 72 次事件） |
| `OnConnectionStatusChanged` | 连接状态变更时（Dispose 期间不触发，修复 H3） |
| `OnFanDataUpdated` | 送风机每轮轮询完成时触发，参数为 FanData |

#### 3.2.2 IoMapBuilder（IO映射表构建器）【V1.09 新增】

**职责**: 依据现场"显耀IO表"，建立内部连续编号与三菱PLC物理地址之间的映射关系。

**核心功能**:
1. **构建完整IO映射表**: 按"输入→真空电磁阀输出→载台上电输出"顺序生成所有IO点定义
2. **获取设备IO映射**: 获取指定气压表的1输入+2输出映射，供面板显示使用
3. **八进制地址转换**: 将十进制数值转为三菱PLC八进制地址（如 0→X000, 72→Y110, 143→Y217）

**关键方法**:

| 方法 | 功能 |
| :--- | :--- |
| `Build(totalBarometers)` | 构建完整IO映射表（totalBarometers×3 个点） |
| `GetDeviceMapping(deviceId, totalBarometers)` | 获取指定设备的1输入+2输出映射 |

**八进制编址说明**:

三菱PLC的 X/Y 点采用八进制编号（每位数字仅 0~7），与十进制不同：
- X007 的下一个是 X010（不是 X008）
- X077 的下一个是 X100
- Y107（真空电磁阀-72）之后，载台上电从 Y110 开始（八进制 110 = 十进制 72）

**地址映射规则**:

| 设备 | 内部编号 | 物理地址 | 转换公式 |
| :--- | :--- | :--- | :--- |
| 真空负压表-N | N (1~72) | X + octal(N-1) | N=1→X000, N=72→X107 |
| 真空电磁阀-N | 72+N (73~144) | Y + octal(N-1) | N=1→Y000, N=72→Y107 |
| 载台上电-N | 144+N (145~216) | Y + octal(72+N-1) | N=1→Y110, N=72→Y217 |

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
    bool SetThreshold(int deviceId, decimal thresholdValue);            // 【V1.15 新增】写单台设备阈值（0x0010，0x06）
    Dictionary<int, bool> SetAllThresholds(decimal thresholdValue);     // 【V1.15 新增】批量写设备阈值，返回失败名单
    event EventHandler<string> OnError;
}
```

**【V1.12 更新 —— 真实实现已内置】**

- Mock 实现：`MockBarometerReader`（`UseMockCommunication=true` 时使用，免接线演示）
- 真实实现：`ModbusRtuBarometerReader`（`UseMockCommunication=false` 时使用，Modbus RTU / RS485→USB，19200）
- 由 `DeviceManager` 依据 `UseMockCommunication` 配置自动切换，无需改代码

**【V1.15 更新 —— 设备阈值写入】**

- 新增 `SetThreshold` / `SetAllThresholds`，与 ModbusRtuBarometerTest Demo 写入逻辑一致（写 Holding Register 0x0010，值 = round(阈值 × 10^小数位)）。
- **单位注意**：`thresholdValue` 是"设备单位"（与压力读数同单位同小数位），**不是**软件报警阈值 `AlarmPressureThresholdPa`（Pa）。单位未按说明书确认前不要写。
- `SetAllThresholds` 逐台失败不中断、返回失败名单；72 台连写 + 坏设备会阻塞较久，应在后台线程调用。
- 实测经验：批量写某台超时通常表示该台设备掉线/损坏（Demo 已提供「批量读取压力」按钮用于定位离线设备）。

**接入其他协议（如需）**:

1. 创建新类实现 `IBarometerReader` 接口（含 `SetThreshold`/`SetAllThresholds`，无设备阈值能力可返回 false/空字典）
2. 参考 `ModbusRtuBarometerReader` 实现 `Connect`、`ReadData`、`Disconnect`
3. 在 `DeviceManager` 构造函数中按需替换实现

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

**【V1.09 更新 —— 显耀IO表接入】**

依据现场"显耀IO表"，IO配置已明确：

| 类型 | 电气特性 | 数量 | 物理地址 | 设备名 | 内部编号 |
| :--- | :--- | :--- | :--- | :--- | :--- |
| 输入 | NPN（漏型） | 72 | X000~X107（八进制） | 真空负压表-1~72 | 1~72 |
| 输出 | PNP（源型） | 72 | Y000~Y107（八进制） | 真空电磁阀-1~72 | 73~144 |
| 输出 | PNP（源型） | 72 | Y110~Y217（八进制） | 载台上电-1~72 | 145~216 |

**每个气压表对应**: 1个输入（真空负压表）+ 2个输出（真空电磁阀 + 载台上电）

**IO点编号规则**:
- 输入点：1 ~ TotalInputs（默认 1 ~ 72）
- 输出点：TotalInputs+1 ~ TotalInputs+TotalOutputs（默认 73 ~ 216）

**物理地址映射**：内部编号与三菱PLC物理地址的转换由 `IoMapBuilder` 服务完成（详见 3.2.2 节）。

**电气特性说明**:
- **输入 NPN（漏型）**：传感器导通时将信号拉低到 0V，IO模块内部上拉后识别为"导通"。适合 NPN 型接近开关、光电传感器。
- **输出 PNP（源型）**：输出导通时输出 +24V 高电平，向外提供电流。适合直接驱动中间继电器线圈（继电器另一端接 0V），再由继电器触点控制大功率负载（电磁阀、载台电源）。

**【V1.12 更新 —— 真实实现已内置】**

- Mock 实现：`MockIoController`；真实实现：`ModbusTcpIoController`（GX-CL140，Modbus TCP）
- 由 `DeviceManager` 依据 `UseMockCommunication` 自动切换，无需改代码
- 使用 `IoMapBuilder.GetDeviceMapping(deviceId)` 获取物理地址进行通信
- 现场实测已确认：DI 从 0x1000 读（Input Register 0x04），DO 从 0x2000 写（Holding Register），每 16 路 1 个寄存器（详见通讯接入说明.md 第 2.2/4 节）

#### 3.3.3 IFanController（冷却送风机控制接口）【V1.15 新增】

**定义**:

```csharp
public interface IFanController : IDisposable
{
    bool IsConnected { get; }
    bool Connect(DeviceConfig config);
    void Disconnect();
    FanData ReadStatus();        // 读取状态（状态/温度/湿度/设定值），失败返回 null
    bool StartFixedValue();      // 定值启动（写入 0x0001 = 0x0003）
    bool Stop();                 // 定值停止（写入 0x0001 = 0x0002）
    event EventHandler<string> OnError;
}
```

**业务说明**：冷却送风机（厂商自带控制屏）的自动控温已由厂商集成，上位机只需"定值启动/定值停止"并周期读状态用于显示。

**实现**：
- Mock：`MockFanController`（`FanEnabled=true` 且无设备时演示用）
- 真实：`FanControllerClient`（Modbus TCP，同步版，带锁 + 断线重连节流 + 连接超时）
- 送风机是"可选设备"，连接失败不影响整机启动；用独立定时器轮询（2s），不阻塞 72 台气压表采集

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
| `InputStatus` | bool[1] | IO输入状态（1个：真空负压表，NPN） |
| `OutputStatus` | bool[2] | IO输出状态（2个：真空电磁阀+载台上电，PNP） |

**【V1.09 更新】** 依据显耀IO表，每个气压表的IO分配从"2输入+4输出"调整为"1输入+2输出":
- `InputStatus[0]`: 真空负压表输入状态（X地址，NPN）
- `OutputStatus[0]`: 真空电磁阀输出状态（Y地址，PNP）
- `OutputStatus[1]`: 载台上电输出状态（Y地址，PNP）

#### 3.4.2 FanData（冷却送风机数据模型）【V1.15 新增】

| 属性 | 类型 | 说明 |
| :--- | :--- | :--- |
| `RunState` | FanRunState | 运行状态（Unknown/程式停止/程式启动/定值停止/定值启动） |
| `Temperature` | float | 当前温度（°C，寄存器值/100） |
| `Humidity` | float | 当前湿度（%RH，寄存器值/100） |
| `TempSetpoint` | float | 温度设定值（°C，厂商控制屏设定，只读） |
| `HumSetpoint` | float | 湿度设定值（%RH，只读） |
| `IsOnline` | bool | 本次是否成功读到数据（false=通讯失败，字段为默认值） |
| `CollectTime` | DateTime | 采集时间戳 |

`FanRunState` 枚举值与寄存器 0x0001 实测对应：0x0000=程式停止、0x0001=程式启动、0x0002=定值停止、0x0003=定值启动；`Unknown(-1)` 为本程序自定义哨兵值（读失败/未初始化）。`Clone()` 提供深拷贝，避免外部修改污染缓存。

#### 3.4.3 DeviceConfig（设备配置模型）

> 默认值以 App.config 为准（完整配置清单见 4.1 节）。

| 属性 | 类型 | 默认值 | 说明 |
| :--- | :--- | :--- | :--- |
| `TotalBarometers` | int | 72 | 气压表总数 |
| `TotalInputs` | int | 80 | IO输入总数（GX-CL140 接 3 个输入模块，80DI） |
| `TotalOutputs` | int | 160 | IO输出总数（GX-CL140 接 5 个输出模块，160DO） |
| `PortName` | string | COM1 | 串口（气压表 RTU，RS485→USB） |
| `BaudRate` | int | 19200 | 波特率（ModbusRtuBarometerTest Demo 实测） |
| `DataBits` | int | 8 | 数据位 |
| `StopBits` | int | 1 | 停止位 |
| `Parity` | string | None | 校验位 |
| `UseMockCommunication` | bool | false | true=Mock 免接线演示；false=真实通讯（App.config 默认 false） |
| `SerialReadTimeoutMs` | int | 1000 | 串口读取超时（毫秒） |
| `SerialWriteTimeoutMs` | int | 1000 | 串口写入超时（毫秒） |
| `TcpSendTimeoutMs` | int | 3000 | TCP 发送超时（毫秒） |
| `TcpReceiveTimeoutMs` | int | 3000 | TCP 接收超时（毫秒） |
| `InvertInputs` / `InvertOutputs` | bool | false | 输入/输出逻辑取反（NPN/PNP 现场差异） |
| `IoUnitId` | byte | 1 | IO 耦合器从站地址（UnitId） |
| `IoInputRegisterStartAddress` | ushort | 0x1000 | DI 起始寄存器（Input Register 0x04） |
| `IoOutputRegisterStartAddress` | ushort | 0x2000 | DO 起始寄存器（Holding Register） |
| `IoBackupChannelMappingEnabled` | bool | false | 备用通道映射总开关（DQ 通道烧毁时置 true，多数工作台默认关） |
| `IoBackupChannelMappings` | string | `0x2000@0->0x2009@10;0x2008@0->0x2009@11` | 备用通道映射表（源寄存器@源通道->目标寄存器@目标通道，分号分隔） |
| `BarometerPressureRegisterAddress` | ushort | 0x0001 | 压力寄存器（0x0002 为小数位） |
| `BarometerDefaultDecimalPlaces` | int | 1 | 小数位默认值 |
| `BarometerPressureScale` | decimal | 1 | 压力缩放系数 |
| `AlarmPressureThresholdPa` | decimal | -95000 | 报警压力阈值（Pa） |
| `AlarmWhenPressureHigherThanThreshold` | bool | true | 压力高于阈值报警 |
| `PlcAddress` | string | 192.168.1.20 | GX-CL140 IP |
| `PlcPort` | int | 502 | GX-CL140 端口（Modbus TCP） |
| `CollectInterval` | int | 1000 | 气压表采集间隔（毫秒） |
| `PanelColumns` | int | 8 | 面板列数 |
| `PanelRows` | int | 9 | 面板行数 |
| `FanEnabled` | bool | true | 是否启用冷却送风机（可选设备，连接失败不影响启动） |
| `FanIpAddress` | string | 192.168.1.220 | 送风机控制屏主 IP（自动识别时优先尝试） |
| `FanAutoDetectEnabled` | bool | true | 送风机 IP 自动识别开关（按顺序尝试 FanIpAddress + FanIpCandidates，第一个连上的即设备地址） |
| `FanIpCandidates` | string | 192.168.1.220,192.168.1.221,192.168.1.222 | 送风机候选 IP 列表（逗号/分号分隔，**配几个就能识别几个**；仅自动识别开启时生效） |
| `FanPort` | int | 50000 | 送风机端口（厂商控制屏实测） |
| `FanUnitId` | byte | 1 | 送风机从站地址 |
| `FanTimeoutMs` | int | 3000 | 送风机通讯超时（毫秒） |
| `VacuumConfirmTimeoutMs` | int | 15000 | 真空建立确认超时（毫秒） |
| `CommunicationLossAlarmCount` | int | 3 | 通讯失联报警阈值（连续失败次数） |
| `MaxTestDurationSeconds` | int | 0 | 老化测试最大时长（秒，0=不限） |
| `UseDiAlarmContact` | bool | false | DI 报警触点并入判定（需现场确认电平后开） |
| `FanTempAlarmLimitC` | float | 0 | 送风机温度告警上限（°C，0=不启用） |

#### 3.4.4 RecipeConfig（配方配置模型）

| 属性 | 类型 | 说明 |
| :--- | :--- | :--- |
| `Id` | int | 配方编号 |
| `Name` | string | 配方名称 |
| `NegativePressure` | decimal | 负压值设定 |
| `DelayStartTime` | TimeSpan | 延时开启时间 |
| `DelayArriveTime` | TimeSpan | 延时到达时间 |
| `LimitTemperature` | decimal | 极限温度 |

#### 3.4.5 UserRole（用户角色枚举）

| 枚举值 | 数值 | 说明 |
| :--- | :--- | :--- |
| `Operator` | 0 | 操作员（基础权限，未登录状态） |
| `Technician` | 1 | 技术员（可操作参数设置和配方参数） |
| `Administrator` | 2 | 管理员（最高权限，可管理其他用户账号） |

**权限规则**：数值越大权限越高，`HasPermission(requiredRole)` 通过 `CurrentUser.Role >= requiredRole` 判断。

#### 3.4.6 UserAccount / LoginResult（用户账号 / 登录结果）

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

**职责**: 维护用户账号、提供登录验证、密码修改、权限校验功能，用户数据持久化到 JSON 文件。

**默认账号**（程序首次启动时初始化，数据持久化到 Users.json）：

| 角色 | 用户名 | 密码 |
| :--- | :--- | :--- |
| 操作员 | operator | 123456 |
| 技术员 | technician | 123456 |
| 管理员 | admin | 123456 |

**数据持久化方案**：使用 JSON 文件（Users.json）存储用户数据

**选择 JSON 而非 XML 的原因**：

| 对比项 | JSON | XML |
| :--- | :--- | :--- |
| 文件体积 | 轻量级，键值对结构简洁，无冗余标签 | 冗余标签多，文件体积大 |
| 可读性 | 直观的键值对格式，易于人工查看和修改 | 标签嵌套复杂，阅读体验差 |
| 序列化 | Newtonsoft.Json API 简单，一行代码即可完成 | 需要处理命名空间、声明等，相对复杂 |
| 学习成本 | 低，现代开发人员普遍熟悉 | 高，语法繁琐 |
| 主流趋势 | 当前数据交换的标准格式 | 逐渐被 JSON 取代 |

**数据存储说明**：
- 用户数据存储在程序运行目录下的 `Users.json` 文件中
- 程序启动时自动加载用户数据
- 用户数据变更（修改用户名/密码）时自动保存到文件
- 文件不存在时使用默认账号并自动创建文件
- 文件损坏或格式错误时使用默认账号并重建文件

**关键方法**:

| 方法 | 功能 | 权限要求 |
| :--- | :--- | :--- |
| `Login(targetRole, username, password)` | 校验账号密码，并验证账号是否属于目标角色 | 任意 |
| `Logout()` | 退出登录，恢复未登录状态 | 任意 |
| `UpdateUsername(targetRole, newUsername)` | 修改指定角色的用户名，修改后自动保存到文件 | 仅管理员 |
| `UpdatePassword(targetRole, newPassword)` | 修改指定角色的密码，修改后自动保存到文件 | 仅管理员 |
| `GetAccount(role)` | 获取指定角色的账号信息（用于用户管理窗体显示） | 任意 |
| `HasPermission(requiredRole)` | 判断当前用户是否拥有指定权限 | 任意 |

**安全提示**：当前密码以明文存储，实际项目应使用 SHA256/BCrypt 等哈希算法加密保存。

#### 3.5.2 权限登录流程

```
┌──────────────────────────────────────────────────────────────────┐
│ 用户点击"用户权限"按钮                                                │
│           ↓                                                       │
│ 弹出下拉菜单：操作员 / 技术员 / 管理员 / 用户管理*                        │
│           ↓                                                       │
│ 用户选择角色（例如"管理员"）                                           │
│           ↓                                                       │
│ 弹出 LoginForm 登录窗体                                             │
│ ┌──────────────────────────────┐                                  │
│ │  切换为 管理员 权限             │                                  │
│ │  用户名: [_______________]    │                                  │
│ │  密  码: [_______________]    │                                  │
│ │       [确认]    [取消]         │                                  │
│ └──────────────────────────────┘                                  │
│           ↓                                                       │
│ 用户输入用户名密码并点击"确认"                                         │
│           ↓                                                       │
│ UserManager.Login() 校验账号密码                                    │
│   ├─ 成功 → 更新 lblPermission，启用/禁用相关按钮，写入日志             │
│   └─ 失败 → 弹出错误提示窗口，用户可重新输入                             │
│                                                                   │
│ *用户管理选项仅管理员可见，点击后弹出 UserManagementForm                │
└──────────────────────────────────────────────────────────────────┘
```

#### 3.5.3 按钮权限控制

| 按钮 | 权限要求 | 行为 |
| :--- | :--- | :--- |
| `btnParameter`（参数设置） | 技术员或管理员 | 无权限时按钮变灰（Enabled=false） |
| 其他按钮 | 任意权限 | 始终可用 |

**实现方法**: `UpdateButtonPermissionStates()` 在每次权限切换后调用，根据 `_userManager.HasPermission(UserRole.Technician)` 设置按钮的 `Enabled` 属性。

#### 3.5.4 用户管理窗体（仅管理员）

管理员登录后，下拉菜单会额外显示"用户管理"选项。点击后弹出 `UserManagementForm`：

```
┌──────────────────────────────────────────┐
│           用户账号管理                     │
├──────────────────────────────────────────┤
│ 角色:        [操作员 ▼]                    │
│ 当前用户名:  operator                      │
│ 新用户名:    [____________________]        │
│ 新密码:      [____________________]       │
│ 确认密码:    [____________________]        │
├──────────────────────────────────────────┤
│          [应用修改]    [关闭]              │
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
| `TotalInputs` | IO输入总数（现场 80DI） | 80 |
| `TotalOutputs` | IO输出总数（现场 160DO） | 160 |
| `CollectInterval` | 数据采集间隔（毫秒） | 1000 |
| `PanelColumns` | 主视图面板列数 | 8 |
| `PanelRows` | 主视图面板行数 | 9 |
| `PortName` | 串口（气压表 RTU） | COM1 |
| `BaudRate` | 波特率（Demo 实测 19200） | 19200 |
| `DataBits` | 数据位 | 8 |
| `StopBits` | 停止位 | 1 |
| `Parity` | 校验位 | None |
| `UseMockCommunication` | true=Mock（免接线），false=真实通讯 | false |
| `InvertInputs` / `InvertOutputs` | 输入/输出逻辑取反（NPN/PNP 现场差异） | false |
| `IoUnitId` | IO 耦合器从站地址 | 1 |
| `IoInputRegisterStartAddress` | DI 起始寄存器（Input Register 0x04） | 0x1000 |
| `IoOutputRegisterStartAddress` | DO 起始寄存器（Holding Register） | 0x2000 |
| `IoBackupChannelMappingEnabled` | 备用通道映射总开关（DQ 通道烧毁时置 true） | false |
| `IoBackupChannelMappings` | 备用通道映射表（源@通道->目标@通道，分号分隔） | `0x2000@0->0x2009@10;0x2008@0->0x2009@11` |
| `BarometerPressureRegisterAddress` | 压力寄存器（0x0001，0x0002 为小数位） | 0x0001 |
| `BarometerDefaultDecimalPlaces` | 小数位默认值 | 1 |
| `BarometerPressureScale` | 压力缩放系数 | 1 |
| `AlarmPressureThresholdPa` | 报警压力阈值（Pa） | -95000 |
| `AlarmWhenPressureHigherThanThreshold` | 压力高于阈值报警 | true |
| `PlcAddress` | GX-CL140 IP | 192.168.1.20 |
| `PlcPort` | GX-CL140 端口 | 502 |
| `FanEnabled` | 是否启用冷却送风机（可选设备） | true |
| `FanIpAddress` | 送风机控制屏主 IP（自动识别时优先尝试） | 192.168.1.220 |
| `FanAutoDetectEnabled` | 送风机 IP 自动识别开关 | true |
| `FanIpCandidates` | 送风机候选 IP 列表（逗号/分号分隔，配几个识别几个） | 192.168.1.220,192.168.1.221,192.168.1.222 |
| `FanPort` | 送风机端口（实测 50000） | 50000 |
| `FanUnitId` | 送风机从站地址 | 1 |
| `FanTimeoutMs` | 送风机通讯超时（毫秒） | 3000 |
| `VacuumConfirmTimeoutMs` | 真空建立确认超时（毫秒） | 15000 |
| `CommunicationLossAlarmCount` | 通讯失联报警阈值（连续失败次数） | 3 |
| `MaxTestDurationSeconds` | 老化测试最大时长（秒，0=不限） | 0 |
| `UseDiAlarmContact` | 是否把 DI 报警触点并入报警判定（需现场确认电平后开） | false |
| `FanTempAlarmLimitC` | 送风机温度告警上限（°C，0=不启用） | 0 |

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

### 5.1 协议/接口确认状态

| 项 | 状态 | 说明 |
| :--- | :--- | :--- |
| 气压表通信协议 | 已确认 | Modbus RTU / RS485→USB，19200（ModbusRtuBarometerTest Demo 实测） |
| IO通信协议 | 已确认 | Modbus TCP，GX-CL140（192.168.1.20:502，ModbusTCPTest Demo 实测） |
| 送风机通信协议 | 已确认 | Modbus TCP，厂商控制屏（192.168.1.220:50000，ModbusTCPFanControllerTest Demo 实测） |
| PLC连接方式 | 已确认 | 以太网 Modbus TCP |
| 数据存储方案 | 部分确定 | 事件日志已落盘 CSV（Logs\TestLog_*.csv）；数据库方案待定 |

### 5.2 预留的功能接口

| 功能 | 状态 | 文件位置 | 说明 |
| :--- | :--- | :--- | :--- |
| 用户权限管理 | 已实现 | MainForm.cs / UserManager.cs / LoginForm.cs / UserManagementForm.cs | 下拉菜单（操作员/技术员/管理员）+ 登录窗体 + 用户管理（管理员修改他人账号）；用户数据持久化到 Users.json（V1.13） |
| 参数设置-公共参数 | 部分实现 | MainForm.cs / CommonParameterForm | 公共参数窗体已实现，参数项需现场确认补充 |
| 参数设置-配方管理 | 部分实现 | MainForm.cs / RecipeManagerForm | 配方列表显示已实现，新增/编辑/删除逻辑待实现 |
| LOG记录-历史记录 | 已实现（V1.15） | MainForm.cs / HistoryRecordForm | 读取 Logs\TestLog_*.csv 真实事件日志，按日期查询 |
| TEST-扫码模拟 | 部分实现 | MainForm.cs / ScanSimulationForm | 扫码模拟窗体已实现，真实扫码枪接入待实现 |
| 关于-版本说明 | 已实现 | MainForm.cs | 版本信息弹窗已实现 |
| 行全选按钮 Set(SEL_N) | 已实现 | MainForm.cs | 点击切换该行所有面板选中状态（浅蓝高亮） |
| 面板批量操作 | 已实现（V1.15） | MainForm.cs / BarometerPanelView | 选中面板后执行：开启真空 / 启动运行 / 停止运行 / 报警复位 |
| 送风机定值启动 | 已实现（V1.15） | MainForm.cs / FanControllerClient.cs | 送风机 Modbus TCP 接入，定值启动/停止 + 温度湿度监视 |
| 送风机定值停止 | 已实现（V1.15） | MainForm.cs / FanControllerClient.cs | 手动停止；有台测试时自动保持运行 |
| 开启真空（选中台） | 已实现（V1.15） | MainForm.cs | 对选中面板打开真空电磁阀（单动作，预检用） |
| 启动运行（选中台） | 已实现（V1.15） | MainForm.cs / DeviceManager.cs | 开真空+载台上电+送风机定值启动+进测试，真空确认+老化计时 |
| 停止运行（选中台） | 已实现（V1.15） | MainForm.cs / DeviceManager.cs | 关阀+断电+退出测试（末台时送风机自动停止） |
| 报警复位（选中台） | 已实现（V1.15） | MainForm.cs / DeviceManager.cs | 人工解除故障状态，可重新测试 |
| 全部停止（急停） | 已实现（V1.15） | MainForm.cs / DeviceManager.cs | 一键全关阀+全断电+停送风机，带防误触确认 |
| 单台手动控制 | 已实现（V1.15） | Dialogs/DeviceManualForm.cs | 面板 Set 按钮打开，点动阀/载台电 + 实时 DI 状态 |
| 批量设置配方 | 已实现 | MainForm.cs / BatchRecipeForm.cs | 批量设置配方窗口已实现，支持配方名称、延时时间1/2、启动时间、极限温度输入，以及配方队列管理；配方批量应用到选中面板待实现 |
| 录入批号 | 已实现 | MainForm.cs / InputLotForm.cs | 录入批号窗口已实现，支持手动输入批号、输入校验、Enter键确认；确定后弹出ID绑定界面；批号写入 DeviceManager 供日志追溯 |
| ID绑定 | 已实现 | InputLotForm.cs / IdBindingForm.cs | ID绑定窗口已实现，支持工位编号和SN输入、产品列表显示、重复工位覆盖确认、保存功能；保存时自动生成Excel文档（命名规则：批号_日期_时间.xlsx），包含批号、工位号、SN、配方名称、延时时间、启动时间列；ID绑定数据持久化待实现 |
| 老化计时自动停止 | 已实现（V1.15） | DeviceManager.cs | 真空确认后开始计时，到达 MaxTestDurationSeconds 自动停止并记日志 |
| 报警事件落盘 | 已实现（V1.15） | TestEventLogger.cs | 启动/停止/报警/复位/急停/真空建立 写入 Logs\TestLog_yyyyMMdd.csv |
| 日志持久化 | 部分实现（V1.15） | TestEventLogger.cs | 事件日志已落盘 CSV；界面 LOG 文本框仍未写文件 |
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
│   │   ├── FanData.cs                      # 【V1.15新增】送风机数据模型 + 运行状态枚举
│   │   ├── IoStatus.cs                     # IO状态模型 + IoType/IoFunction/ElectricalType 枚举
│   │   ├── IoPointDefinition.cs            # 【V1.09新增】IO点定义模型 + 设备IO映射集合
│   │   ├── DeviceConfig.cs                 # 设备配置模型
│   │   ├── RecipeConfig.cs                 # 配方配置模型
│   │   ├── UserRole.cs                     # 【新增】用户角色枚举（操作员/技术员/管理员）
│   │   └── UserAccount.cs                  # 【新增】用户账号模型 + 登录结果
│   ├── Interfaces/                         # 接口层
│   │   ├── IBarometerReader.cs             # 气压表读取接口
│   │   ├── IIoController.cs                # IO控制器接口（V1.09更新注释:显耀IO表映射）
│   │   └── IFanController.cs               # 【V1.15新增】送风机控制接口（定值启动/停止/读状态）
│   ├── Services/                           # 服务层
│   │   ├── MockBarometerReader.cs          # 气压表模拟读取器
│   │   ├── ModbusRtuBarometerReader.cs     # 气压表 Modbus RTU 真实读取（RS485→USB）
│   │   ├── MockIoController.cs             # IO模拟控制器
│   │   ├── ModbusTcpIoController.cs        # IO Modbus TCP 真实读写（GX-CL140）
│   │   ├── IoMapBuilder.cs                 # 【V1.09新增】IO映射表构建器（八进制地址转换）
│   │   ├── FanControllerClient.cs          # 【V1.15新增】送风机 Modbus TCP 真实实现
│   │   ├── MockFanController.cs            # 【V1.15新增】送风机 Mock
│   │   ├── TestEventLogger.cs              # 【V1.15新增】测试事件 CSV 落盘
│   │   ├── DeviceManager.cs                # 设备管理器（业务编排核心）
│   │   └── UserManager.cs                  # 【新增】用户管理服务（登录/改密/权限校验）
│   ├── Views/                              # 视图层
│   │   ├── MainForm.cs                     # 主窗体（业务逻辑）
│   │   ├── MainForm.Designer.cs            # 主窗体（设计器代码，含 rootScrollPanel 滚动容器）
│   │   ├── BarometerPanelView.cs           # 气压表显示面板（业务逻辑）
│   │   └── BarometerPanelView.Designer.cs  # 气压表显示面板（设计器代码）
│   └── Dialogs/                            # 对话框窗体层（菜单按钮弹出窗体）
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
│       ├── UserManagementForm.Designer.cs
│       ├── BatchRecipeForm.cs                      # 【新增】批量设置配方窗体
│       ├── BatchRecipeForm.Designer.cs
│       ├── InputLotForm.cs                         # 【新增】录入批号窗体
│       ├── InputLotForm.Designer.cs
│       ├── IdBindingForm.cs                        # 【新增】ID绑定窗体（批号绑定工位和SN）
│       ├── IdBindingForm.Designer.cs
│       ├── DeviceManualForm.cs                     # 【V1.15新增】单台手动控制（面板 Set 按钮打开）
│       └── DeviceManualForm.Designer.cs
└── README.md                                 # 本文档（使用/架构说明）
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

**【V1.12/V1.15 更新】** 真实通讯实现已内置，接入现场硬件 = 配置 `App.config` + 关闭 Mock，无需改代码：

1. **接好线**：气压表 RS485→USB 串口；GX-CL140 用网线连上位机；送风机控制屏用网线连上位机（如可选）
2. **改配置**（App.config）：串口 `PortName/BaudRate(19200)`、IO `PlcAddress(192.168.1.20)/PlcPort(502)`、送风机 `FanIpAddress(192.168.1.220)/FanPort(50000)`（送风机默认开 IP 自动识别，候选 IP 在 `FanIpCandidates` 里配，几个都行）
3. **关闭 Mock**：`UseMockCommunication=false`
4. **启动程序**：DeviceManager 会自动使用真实实现（`ModbusRtuBarometerReader` + `ModbusTcpIoController` + `FanControllerClient`）
5. **按序验证**：先用各测试 Demo 验证单条链路，再整机联动（详见通讯接入说明.md 第 6 节"推荐通线验证顺序"）

**如换用其他协议的硬件**（如三菱 MC 协议、其他品牌气压表），才需要新写实现类：

```csharp
// 气压表：实现 IBarometerReader
public class MyBarometerReader : IBarometerReader
{
    public bool Connect(DeviceConfig config) { /* 按新协议实现 */ }
    public BarometerData ReadData(int deviceId) { /* 按新协议实现 */ }
    // ... 其他成员
}

// IO：实现 IIoController
public class MyIoController : IIoController
{
    public bool Connect(DeviceConfig config) { /* 按新协议实现 */ }
    public bool ReadInput(int inputId) { /* 按新协议实现 */ }
    // ... 其他成员
}
```

然后在 `DeviceManager` 构造函数中按需替换即可。

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

- 当前版本: V1.15
- 更新日志:
  - V1.15 (2026-08-06): 业务串联 + 冷却送风机接入。新增送风机接口/实现/Mock/数据模型（Modbus TCP，定值启动/停止 + 温度湿度监视）；DeviceManager 增加测试状态机（启动/停止/报警复位/全部停止）、真空建立确认、通讯失联报警、老化计时自动停止、送风机全局生命周期（首台启动/末台停止）；新增事件 CSV 落盘（TestEventLogger）+ 历史记录读真实日志；新增单台手动控制对话框；右侧面板新增送风机监视区与业务操作按钮。详见 CHANGELOG.md
  - V1.14 (2026-08-03): 接入真实通讯链路（气压表 Modbus RTU + IO Modbus TCP），DeviceManager 支持 UseMockCommunication 切换；新增报警阈值参数并实现“报警边沿→关阀/断载台电”联动；新增 CHANGELOG.md 与 通讯接入说明.md 文档
  - V1.13 (2026-07-24): 用户数据持久化到 JSON 文件（Users.json）；新增 LoadUsersFromFile/SaveUsersToFile 方法；程序启动时自动加载用户数据，修改用户名/密码后自动保存；UserAccount 新增无参构造函数用于 JSON 反序列化；添加 Newtonsoft.Json NuGet 包引用；文档更新持久化方案说明
  - V1.12 (2026-07-24): 配方管理窗口样式调整为左右分栏布局；左侧DataGridView只显示序号和配方名称两列；右侧显示选中配方的详细信息（配方名称、延时时间、启动时间、极限温度）；底部添加/更新/删除按钮和保存设置按钮；添加表格点击事件更新右侧显示
  - V1.09 (2026-07-22): 接入客户"显耀IO表"IO配置；新增 IoPointDefinition 模型和 IoMapBuilder 服务（内部编号↔三菱PLC八进制物理地址映射）；新增 IoFunction/ElectricalType 枚举（NPN输入/PNP输出电气特性）；BarometerData 调整为1输入+2输出（真空负压表/真空电磁阀/载台上电）；BarometerPanelView IO状态框改为3个并显示功能名+物理地址；IIoController/MockIoController/DeviceConfig 注释更新实际IO映射
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

### 9.1 IO点映射表（依据显耀IO表，V1.09 更新）

**每个气压表对应: 1个输入(NPN, X地址) + 2个输出(PNP, Y地址)**

| 气压表编号 | 输入(NPN) | 输入物理地址 | 输出1-真空电磁阀(PNP) | 输出1物理地址 | 输出2-载台上电(PNP) | 输出2物理地址 |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| 1 | 真空负压表-1 | X000 | 真空电磁阀-1 | Y000 | 载台上电-1 | Y110 |
| 2 | 真空负压表-2 | X001 | 真空电磁阀-2 | Y001 | 载台上电-2 | Y111 |
| 8 | 真空负压表-8 | X007 | 真空电磁阀-8 | Y007 | 载台上电-8 | Y117 |
| 9 | 真空负压表-9 | X010 | 真空电磁阀-9 | Y010 | 载台上电-9 | Y120 |
| 64 | 真空负压表-64 | X077 | 真空电磁阀-64 | Y077 | 载台上电-64 | Y177 |
| 65 | 真空负压表-65 | X100 | 真空电磁阀-65 | Y100 | 载台上电-65 | Y200 |
| 72 | 真空负压表-72 | X107 | 真空电磁阀-72 | Y107 | 载台上电-72 | Y217 |

**地址为三菱PLC八进制编址**（每位数字仅0~7，X007之后是X010，X077之后是X100）。

**内部编号映射**:
- 输入(N): 内部编号 = N (1~72)
- 真空电磁阀(N): 内部编号 = 72 + N (73~144)
- 载台上电(N): 内部编号 = 144 + N (145~216)

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
| H6 | NumericUpDown 赋值未做范围校验，抛 ArgumentOutOfRangeException | CommonParameterForm.cs | 新增 ClampToNumericRange 辅助方法 |
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
| README.md | 文档同步更新 |

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

---

## 12. V1.09 修复记录（显耀IO表接入）

本章记录 V1.09 版本接入客户"显耀IO表"的详细变更。

### 12.1 IO配置来源

客户提供的"显耀IO.xlsx"文件，包含3个工作表（Sheet1为有效数据，Sheet2/Sheet3为空）。
Sheet1 布局为5列：A列=输入设备名、B列=输入地址、C列=分隔、D列=输出设备名、E列=输出地址。

### 12.2 显耀IO表实际配置

| 类别 | 电气特性 | 数量 | 设备名 | 物理地址(三菱八进制) |
| :--- | :--- | :--- | :--- | :--- |
| 输入 | NPN（漏型） | 72 | 真空负压表-1~72 | X000~X007, X010~X017, ..., X100~X107 |
| 输出 | PNP（源型） | 72 | 真空电磁阀-1~72 | Y000~Y007, Y010~Y017, ..., Y100~Y107 |
| 输出 | PNP（源型） | 72 | 载台上电-1~72 | Y110~Y117, Y120~Y127, ..., Y210~Y217 |

**每个气压表对应**: 1输入（真空负压表）+ 2输出（真空电磁阀 + 载台上电）

### 12.3 电气特性说明

#### NPN 输入（漏型/灌入式）
- 传感器导通时将IO输入信号拉低到 0V（低电平有效）
- IO模块内部提供上拉电阻，NPN传感器导通时拉低电平，模块识别为"导通"
- 适合 NPN 型接近开关、光电传感器等
- 接线方式：+V → 传感器 → IO输入；传感器公共端接 0V

#### PNP 输出（源型/拉出式）
- 输出导通时IO输出端输出 +24V 高电平，向外提供电流
- 适合直接驱动中间继电器线圈（继电器另一端接 0V）
- 再由继电器触点控制大功率负载（电磁阀、载台电源）
- 接线方式：IO输出 → 继电器线圈 → 0V；继电器常开触点串联在负载回路中

### 12.4 代码变更清单

| 文件 | 变更内容 |
| :--- | :--- |
| Models/IoPointDefinition.cs | 【新增】IO点定义模型 + DeviceIoMapping 设备IO映射集合 |
| Models/IoStatus.cs | 【更新】新增 PhysicalAddress/Function/Electrical 字段；新增 IoFunction/ElectricalType 枚举 |
| Models/BarometerData.cs | 【更新】InputStatus 由 bool[2] 改为 bool[1]；OutputStatus 由 bool[4] 改为 bool[2] |
| Models/DeviceConfig.cs | 【更新】TotalInputs/TotalOutputs 注释补充显耀IO表物理地址说明 |
| Services/IoMapBuilder.cs | 【新增】IO映射表构建器，含八进制地址转换 Convert.ToString(value, 8) |
| Services/MockBarometerReader.cs | 【更新】生成1输入+2输出数据（真空负压表/真空电磁阀/载台上电） |
| Services/MockIoController.cs | 【更新】类注释补充显耀IO表映射说明 |
| Interfaces/IIoController.cs | 【更新】接口注释补充实际IO映射/NPN输入/PNP输出说明 |
| Views/BarometerPanelView.cs | 【更新】IO状态框改为3个，构造函数调用 IoMapBuilder 设置功能名+物理地址文本 |
| Views/BarometerPanelView.Designer.cs | 【更新】移除 boxInput2/boxOutput3/boxOutput4，保留3个框(各2行显示) |
| BarometerWinform.csproj | 【更新】添加 IoPointDefinition.cs 和 IoMapBuilder.cs 编译项 |
| README.md | 【更新】3.2.2/3.3.2/3.4.1/9.1/8.3 章节 + 本章新增 |

### 12.5 八进制地址转换验证

三菱PLC的X/Y点采用八进制编址（每位数字仅0~7），与十进制不同。
`IoMapBuilder.ToOctal()` 使用 `Convert.ToString(value, 8).PadLeft(3, '0')` 转换：

| 设备 | 十进制值 | 八进制地址 | 验证 |
| :--- | :--- | :--- | :--- |
| 真空负压表-1 | 0 | X000 | ✓ |
| 真空负压表-8 | 7 | X007 | ✓ |
| 真空负压表-9 | 8 | X010 | ✓（非X008） |
| 真空负压表-64 | 63 | X077 | ✓ |
| 真空负压表-65 | 64 | X100 | ✓（非X080） |
| 真空负压表-72 | 71 | X107 | ✓ |
| 真空电磁阀-1 | 0 | Y000 | ✓ |
| 真空电磁阀-72 | 71 | Y107 | ✓ |
| 载台上电-1 | 72 | Y110 | ✓（72十进制=110八进制） |
| 载台上电-8 | 79 | Y117 | ✓ |
| 载台上电-56 | 127 | Y177 | ✓ |
| 载台上电-57 | 128 | Y200 | ✓（八进制跳变: 177→200） |
| 载台上电-64 | 135 | Y207 | ✓ |
| 载台上电-65 | 136 | Y210 | ✓（八进制跳变: 207→210） |
| 载台上电-72 | 143 | Y217 | ✓（143十进制=217八进制） |

### 12.6 预留业务逻辑说明

> **注**：本章为 V1.09 接入显耀IO表时的历史状态。下述"仅显示状态/预留"项已随后续版本实现——
> 真空阈值判定与报警联动（V1.12）、开阀/断电时序、故障联锁、老化测试状态机（V1.15），详见 5.2 节。

| 预留项 | V1.09 当时状态 | 后续实现 |
| :--- | :--- | :--- |
| 真空负压表信号处理 | 仅显示状态 | V1.12 读取压力值并按 AlarmPressureThresholdPa 判定；V1.15 真空建立超时报警 |
| 真空电磁阀控制 | 仅显示状态 | V1.12/V1.15 开启真空/启动运行开阀，停止/报警/急停关阀 |
| 载台上电控制 | 仅显示状态 | V1.12/V1.15 启动运行上电，停止/报警/急停断电 |
| 真空启动流程 | 预留 | V1.15 "开启真空（选中台）" → DeviceManager 真空确认 |
| 载台上电流程 | 预留 | V1.15 并入"启动运行"流程，随报警/停止联动断电 |
| IO通信协议 | 待确定 | V1.12 ModbusTcpIoController（GX-CL140，Modbus TCP） |
| 故障联锁 | 预留 | V1.12/V1.15 报警边沿→关阀+断电+标故障，人工报警复位后恢复 |

### 12.7 编译验证

- 修复后编译结果：**0 警告，0 错误**
- 编译命令：`dotnet build BarometerWinform.sln --configuration Debug`
- 所有新增/修改的 .cs 文件已转换为 UTF-8 with BOM 编码
