
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
| 扫码枪读码 | 真实扫码枪接入（串口读码，工位号/SN 自动识别填充） | 已实现（V1.16，ScannerService） |

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
│  │   菜单栏     │  │    WorkstationPanelView × N  │  │   操作面板    │ │
│  │ (6个按钮)    │  │        (子视图/动态加载)       │   │ (9个按钮)    │ │
│  │  ↓ 下拉菜单  │  └──────────────────────────────┘   └─────────────┘ │
│  └──────┬──────┘                                                    │
└─────────┼───────────────────────────────────────────────────────────┘
          │ 弹出
          ▼
┌─────────────────────────────────────────────────────────────────────┐
│                 Dialogs (对话框窗体层)                                │
│  CommonParameterForm │ RecipeManagerForm │ HistoryRecordForm   │
│  InputLotForm │ IdBindingForm │ DeviceManualForm (V1.15)       │
│  StationSettingsForm (V1.18) │ SettingsForm (V1.17)           │
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
| **视图层** | Views | 负责主UI展示和用户交互 | MainForm.cs, WorkstationPanelView.cs |
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
│ 标题栏: 老化测试系统V1.16 | 权限: 操作员 | 通讯连接: 未连接          │
├─────────────────────────────────────────────────────────────────┤
│ 菜单按钮: 用户权限 | 参数设置 | 日志记录 | 关于              │
├──────────────────────────────────────┬──────────────────────────┤
│                                      │ 运行状态                  │
│         气压表显示区域                  │ 送风机监视                  │
│      (9列 × 8行 = 72个面板)            │ (状态/设置温度/当前温度)      │
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
| `CreateWorkstationPanels()` | 动态创建工位显示面板（V1.16 更名）+ 行全选按钮（V1.18 更名：Set(SEL_N)→全选），清空前先 Dispose 旧控件（修复 H5） |
| `DeviceManager_OnBatchDataUpdated()` | 处理批量数据更新事件，使用 BeginInvoke 异步切换到 UI 线程（修复 H1/M2） |
| `UpdateAllPanels(allData)` | 一次调用完成所有面板更新，减少 UI 线程切换次数 |
| `BtnSelectRow_Click()` | 行全选按钮点击事件，选中该行所有面板（V1.19.2 起选中仅通过右上角选中指示体现，不再改变背景色/工作状态），按钮文本随之在"全选"/"取消"间切换（V1.18.1）；V1.19.1 改为实时反映该行选中状态（全部选中→取消，任一台被单独取消→全选） |
| `UpdateRowSelectButton(rowIndex)` | 【V1.19.1】刷新指定行"全选/取消"按钮文字：该行所有面板全部选中→"取消"，否则→"全选"（在单个面板选中状态变化时由 IsSelectedChanged 事件触发） |
| `UpdateSelectionBoxVisibility()` | 【V1.19.5】刷新所有面板选中框显示/隐藏：有任一工位被选中→全部显示（选中项=绿底白✓，其它项=空心框），全部未选中→全部隐藏（V1.19.6：整表唯一选中项被切换为未选中时即进入"全部未选中"→全部隐藏；在单个面板选中状态变化时由 IsSelectedChanged 事件触发） |
| `ShowDropdownPopup()` | 在主按钮下方显示下拉菜单（无边框Form + Button列表，尺寸和主按钮一致） |
| `TryLoginAndSwitchPermission(role)` | 【新增】弹出 LoginForm 登录窗体，校验通过后切换权限 |
| `UpdateButtonPermissionStates()` | 【新增】根据当前权限启用/禁用参数设置按钮 |
| `WriteLog()` | 写入日志到右侧 LOG 文本框（带时间戳，限制最大长度避免 GDI 耗尽，修复 M8） |
| `UpdateStatusBar()` | 更新底部状态栏 |
| `UpdateConnectionStatus()` | 更新通讯连接状态显示（V1.16.1：只反映 IO 耦合器是否连接；修复 H1，含 IsDisposed 检查和异常捕获） |
| `LoadConfig()` | 从 App.config 加载所有配置项（修复 H7，含一致性校验） |
| `DeviceManager_OnFanDataUpdated()` | 处理送风机数据更新事件（BeginInvoke 异步切到 UI 线程） |
| `UpdateFanDisplay(data)` | 更新送风机监视区显示（V1.16.1：状态/设置温度/当前温度，状态带颜色；V1.16.2：当前温度颜色按设置温度对比——高于设置温度→红、不高于→绿，超告警上限的安全日志保留；V1.16.3：温度显示控件由 ReadOnly TextBox 改为 Label（lblUpperTemp/lblSetTemp），避免文本框选中态覆盖 ForeColor，颜色一定生效） |
| `UpdateRunStatusSummary()` | 更新状态栏"测试中/在线"统计 |
| `GetSelectedDeviceIds()` | 获取选中的设备编号列表，未选中时弹出提示 |
| `Scanner_OnBarcodeScanned()` | 【V1.16】扫码完成事件处理，扫码结果写 LOG 日志 |
| `Scanner_OnStatusChanged()` | 【V1.16】扫码枪连接状态变化处理（连接成功/未找到端口/错误），写 LOG 日志 |

#### 3.1.2 WorkstationPanelView（工位显示面板）

**职责**: 单个工位的显示窗口（V1.16 更名自 BarometerPanelView：本质是 72 个工位，每个工位配一台气压表）。
展示真空压力、上电状态、真空开启状态、工作状态、序列号、配方、延时等信息；右上角有选中指示（标识工位是否被选中，供批量操作）。

**布局结构（V1.16 重设计）**:

```
┌──────────────────────────────┐
│ NO.1                  [□✓]   │  ← 设备编号 + 选中指示（V1.19.4 起"有选中才显示"）
│ 上电   [状态灯]                │  ← 标题与下方各标题左对齐；状态灯与内容列左对齐（V1.19.3）
│ 真空压力 [值] [真空开] [工作状态]   │  ← 压力值只读（V1.19.10 加宽）；真空开/关=文字+颜色；工作状态文字带色（V1.19.4）
│ SN:    [SN值 Label]                 │  ← V1.19.3：内容改为 Label 显示
│ 配方:  [配方值 Label]               │
│ 延时开启 [__:__:__]      ┌────┐      │
│ 延时到达 [__:__:__]      │设置│      │  ← V1.18：点击弹出工位设置窗口
│                          └────┘      │
└──────────────────────────────┘
```

**状态约定（V1.18.1 文字改中文：空闲/选中/繁忙/故障）**:
- 上电状态灯：纯色无文字（绿=ON，灰=OFF），鼠标悬停有 ToolTip 说明。
- 真空开启显示（V1.19.10 起带文字+颜色）：真空开=绿底白字；真空关=红底白字（原纯色绿/灰，V1.19.10 改为文字+颜色，关闭用红色更醒目），鼠标悬停有 ToolTip 说明。
- 选中指示（右上角，V1.19）：**V1.19.5 起**选中=绿底（ForestGreen）+ 白色"✓"；未选中=空心方框（黑框白底，无文字）。（V1.19.2：**选中状态仅靠此指示体现**，面板背景色/工作状态文字不再随选中变化）
- 选中框显示规则（V1.19.5）：**平时全部隐藏，只要有任一工位被选中→所有面板同时显示框**（选中项=绿底白✓，其它项=空心白框）；全部取消选中→全部隐藏。
- 选中交互（V1.19.5 替换 V1.18 的点击切换选中；V1.19.6 单击改"切换"）：在面板**空白区域长按约 0.8 秒**（按住不松手）即选中该工位；选中框显示时（已有任一工位被选中），**单击**空白区域或点击选中框即**切换**该工位"选中/未选中"。例外：整表**唯一**选中的工位被切换为未选中时 → 全表无选中，所有面板选中框自动隐藏。
- 工作状态：空闲且未上电=空闲；空闲但已上电=选中；测试中=繁忙；故障=故障。（V1.19.2：去掉"已选中→选中"规则）
- 工作状态配色（V1.19.4 统一"信号灯"色系，鼠标悬停有 ToolTip 说明）：空闲=浅灰底+黑字；选中（已上电待测试）=橙底+白字；繁忙（测试中）=绿底+白字；故障=**红底+白字**（最醒目，面板本身浅粉打底）。

**关键方法**:

| 方法 | 功能 |
| :--- | :--- |
| `UpdateData(data)` | 更新面板显示数据（压力/SN/配方/延时 + 状态灯 + 工作状态 + 选中指示；V1.19.3：SN/配方为 Label 显示，上电灯前有"上电"标题） |
| `UpdateStatusLight(ctrl, isActive)` | 更新纯色状态灯颜色（绿=开，灰=关；目前仅上电灯 boxPower 使用） |
| `UpdateVacuumOpenDisplay(vacuumOpen)` | 【V1.19.10】更新真空开启显示（文字+颜色：真空开=绿底白字，真空关=红底白字） |
| `UpdateWorkState(status, carrierPower)` | 更新工作状态文字与颜色（空闲/选中/繁忙/故障，V1.19.4 配色：浅灰/橙/绿/红底） |
| `UpdateStatusColor(status)` | 根据状态更新背景色（空闲=白/测试中=浅黄/故障=浅粉） |
| `UpdateSelectionStyle()` | 刷新选中框样式（V1.19.5：按自身选中状态填充绿底白✓/空心白框，并按主窗体协调结果显示/隐藏） |
| `SetSelectionBoxVisible(visible)` | 【V1.19.5】主窗体统一协调选中框显示/隐藏（有任一选中→全部显示，全未选中→全部隐藏） |
| `WirePanelLongPressSelect()` | 【V1.19.5】给面板及非按钮子控件挂接"长按选中"鼠标事件（长按约0.8秒选中；选中框显示时单击切换，V1.19.6） |
| `btnSet_Click` | "设置"按钮点击事件（V1.18 更名：Set→设置），触发 OnSetClicked 通知主窗体打开工位设置窗口 |
| `btnSelect_Click` | 选中框点击事件（V1.19：原 btnPower 上电/下电按钮改为选中指示；V1.19.6：单击切换选中/未选中，整表唯一选中项被取消时全部隐藏） |

**关键事件（V1.16 新增）**:

| 事件 | 说明 |
| :--- | :--- |
| `OnSetClicked(int)` | 点击"设置"按钮 → 主窗体打开工位设置窗口（V1.18：由单台手动控制改为工位设置窗口） |
| `IsSelectedChanged` | 【V1.19.1】`IsSelected` 值实际变化时触发（V1.19.5：空白处长按选中 / 行全选按钮；V1.19.6：选中框显示时单击空白处或点击选中框切换）；主窗体订阅后按所在行刷新"全选/取消"按钮文字，并刷新所有面板选中框的显示/隐藏 |

**关键属性**:

| 属性 | 类型 | 说明 |
| :--- | :--- | :--- |
| `DeviceId` | int | 设备（工位）编号（从1开始，运行时赋值） |
| `IsSelected` | bool | 是否被选中（行全选按钮 / 面板空白处长按约0.8秒选中，V1.19；V1.19.6 起选中框显示时单击空白处或点击选中框切换；V1.19.5 选中框=绿底白✓、有选中才显示，V1.19.2 起不再改变面板背景色）；V1.19.1：值实际变化时触发 `IsSelectedChanged` 事件 |

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

**4个菜单按钮的下拉菜单项**:

| 菜单按钮 | 下拉菜单项 | 触发的窗体/动作 | 权限要求 |
| :--- | :--- | :--- | :--- |
| 用户权限 | 操作员 / 技术员 / 管理员 / 用户管理* | 弹出 LoginForm 输入用户名密码后切换权限；*用户管理仅管理员可见 | 任意权限 |
| 参数设置 | 公共参数 / 配方管理 | 分别弹出 CommonParameterForm / RecipeManagerForm | 技术员或管理员 |
| 日志记录 | 历史记录 | 弹出 HistoryRecordForm | 任意权限 |
| 关于 | 设置* / 通讯测试** / 版本说明 | "设置"→弹出 SettingsForm（单页按分类标题条查看/编辑 App.config 全部配置项，写回 exe.config 重启生效）；"通讯测试"→弹出 CommunicationTestForm（IO 耦合器 DO 输出通道手动测试，V1.20 新增、V1.21 SunnyUI 重构，详见 3.1.4）；"版本说明"→弹出版本信息 MessageBox（V1.19.12：菜单项"关于"改"版本说明"） | *设置仅管理员可见（非管理员隐藏）；**通讯测试仅技术员及以上权限可见（操作员不可见）；版本说明任意权限 |

**关键方法**（MainForm.cs）:

| 方法 | 功能 |
| :--- | :--- |
| `ShowDropdownPopup(hostButton, items)` | 在主按钮下方显示无边框下拉菜单（Form + TableLayoutPanel + Button 列表，尺寸和主按钮一致） |
| `MenuHelpSettings_Click()` | 【V1.17】"关于→设置"：弹出 SettingsForm（表格查看/编辑 App.config 全部配置项） |
| `MenuHelpVersionInfo_Click()` | 【V1.19.12 更名】"关于→版本说明"：弹出版本信息对话框（原 MenuHelpAbout_Click） |
| `TryLoginAndSwitchPermission(role)` | 【新增】弹出 LoginForm 登录窗体，校验通过后切换权限 |
| `UpdateButtonPermissionStates()` | 【新增】根据当前权限启用/禁用按钮 |
| `WriteLog(message)` | 写入日志到右侧 LOG 文本框 |

#### 3.1.4 对话框窗体（Dialogs）

所有对话框窗体位于 `Dialogs/` 目录，采用 partial class 拆分（.cs + .Designer.cs）。

| 窗体 | 功能 | 已实现 | 预留项 |
| :--- | :--- | :--- | :--- |
| `CommonParameterForm` | 公共参数窗口（负压阈值设置） | 【V1.16】标题"公共参数窗口"，所有控件居中显示：负压值设定输入框 + 保存设置按钮；后台线程批量写入所有气压表阈值寄存器（0x0010，参考 Demo 的 BatchSetThreshold，写入期间暂停采集防串口争抢、每台 50ms 间隔），写入完成汇总成功/失败台数（失败列出台号，窗口保持打开便于重试；串口未连接时提示"未连接任何气压表"）。【V1.16.1】阈值换算固定 1 位小数（不再读设备 0x0002，实测该寄存器不可靠，否则设 -95 会写成 -9.5） | — |
| `RecipeManagerForm` | 配方管理 | 左右分栏布局：左侧配方列表（序号+配方名称），右侧配方设置（配方名称、延时时间、启动时间、极限温度），底部添加/更新/删除按钮和保存设置按钮 | 新增/编辑/删除/持久化待实现 |
| `HistoryRecordForm` | 历史记录查询 | 【V1.15】日期范围查询 + 读取 Logs\TestLog_*.csv 真实事件日志（自动跨 CSV 解析、跳过表头），"导出"按钮打开 Logs 文件夹 | Mock 数据已移除 |
| `LoginForm` | 用户登录 | 用户名/密码输入、登录验证、Enter/Esc 键支持 | 密码哈希存储（当前明文） |
| `UserManagementForm` | 用户账号管理（仅管理员） | 修改操作员/技术员的用户名和密码，用户数据持久化到 Users.json 文件 | 新增/删除账号 |
| `BatchRecipeForm` | 批量设置配方 | 配方名称、延时时间1/2（时:分:秒）、启动时间（时:分:秒）、极限温度输入，加入队列功能，配方队列管理 | 配方批量应用到选中面板待实现 |
| `InputLotForm` | 录入批号 | 批号输入框、红色背景注释提示、确定/取消按钮、Enter键支持、输入校验，确定后弹出ID绑定界面 | 批号持久化、关联生产记录待实现 |
| `IdBindingForm` | ID绑定 | 批号显示（只读）、工位编号输入框、SN输入框、红色背景注释说明、产品列表显示（带滚动条）、保存按钮、重复工位覆盖确认、Enter键支持、Excel文档生成（命名规则：批号_日期_时间.xlsx）；【V1.16】扫码枪自动识别工位号（恰好2位数字）/产品SN 填入对应输入框，两条都齐后自动加入产品列表（乱序扫码也能正确配对）；【V1.19.11】保存时把"工位 → SN"写入 DeviceManager 工位静态信息，工位面板 SN 与绑定结果同步（未启用扫码枪时手动输入工位号+SN 同样关联） | ID绑定数据持久化待实现 |
| `StationSettingsForm` | 【V1.18】工位设置窗口 | 点击工位面板"设置"按钮弹出；标题"工位设置窗口 NO XX"；左侧一列 6 个设置项（设置项名+输入框，均左对齐、整列居中）：状态、SN、配方、延时时间、启动时间、极限温度（V1.18.1：状态标题只要"状态"两字，状态值显示中文 空闲/选中/繁忙/故障）；右侧一列按钮：破空 / 下电 / 保存 / 加入对列 / 关闭窗口；打开时从采集缓存回显当前工位数据（状态/SN/配方/延时时间/启动时间，启动时间即延时到达，极限温度属配方配置数据中无对应字段留空）；【V1.19.11】"保存"按钮已实现：把 SN/配方/延时开启/延时到达 写入 DeviceManager 工位静态信息并同步到工位面板，延时格式 时:分:秒 校验（非法提示不保存，空白清空），回显补充启动时间（延时到达）字段 | 右侧按钮（破空/下电/加入对列）具体业务功能待确认（代码留 TODO 标记）；极限温度待配方表接入 |
| `SettingsForm` | 【V1.17】系统设置（仅管理员） | 单页纵向展示 App.config 全部配置项（不用选项卡），按业务分类用标题分隔条隔开（基础配置 / 气压表串口通讯 / IO耦合器（Modbus TCP）/ 气压表寄存器 / 报警参数 / 冷却送风机 / 老化测试业务 / 扫码枪），每类一个表格三列：设置名称（key，只读）/ 说明（中文，只读）/ 设置值（可编辑输入）；页面可滚动，界面用 SunnyUI 控件（UILine 标题 / UIDataGridView 表格 / UIButton 按钮）；"保存设置"前按类型校验（整数/小数/布尔/十六进制寄存器地址，如 0x1000），不合法项整批拦截并列出；保存用 ConfigurationManager.OpenExeConfiguration 写回 exe.config 并刷新 appSettings 缓存，提示重启程序后生效 | 配置修改后需重启生效（设备参数启动时一次性加载） |
| `CommunicationTestForm` | 【V1.20】通讯测试（技术员及以上） | IO 耦合器（GX-CL140，Modbus TCP）DO 输出通道手动测试窗体：两个页签分别测 72 路负压阀输出（Y000~Y107，寄存器 0x2000~0x2004）与 72 路载台上电输出（Y110~Y217，寄存器 0x2004~0x2008），每页 9×8 圆形灯按钮点击 toggle ON/OFF（读-改-写不覆盖共享的 0x2004 字节），底部 连接测试/全部关闭/读取状态/关闭窗口 四个按钮 + 日志框；复用生产工程 NModbus 独立连接，不影响采集线程；复用 App.config 备用通道映射（IoBackupChannelMappingEnabled / IoBackupChannelMappings，V1.20）。【V1.21】整体改用 SunnyUI 控件重构（UIForm 标题栏 + UITabControl/UIPage 页签 + UIPanel 面板 + UIButton 按钮 + UITextBox 日志 + UILedBulb 连接状态灯，顶部状态条实时显示已连接/未连接）；点击被映射到备用通道的通道时，先弹窗（UIMessageBox）告知"该通道已做备用通道映射、实际输出通道是哪个寄存器第几路"并在日志追加映射记录 | — |

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
| `SetBarometerThreshold(deviceId, v)` | 【V1.15 新增】写单台**设备阈值**（透传 IBarometerReader.SetThreshold，设备单位，非 kPa 软件阈值） |
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

#### 3.2.3 ScannerService（扫码枪服务）【V1.16 新增】

**职责**: 接入真实扫码枪（Honeywell Xenon 1902 等，虚拟串口模式），自动识别串口并读取条码。

**实现来源**: 参考 `SerialScannerTest` 测试 Demo 的串口读码逻辑，并按照本项目的服务模式封装。

**核心功能**:

1. **WMI 自动识别串口**: 通过 `System.Management` 查询 `Win32_PnPEntity`，筛选设备名称包含 `"COM"` 和关键词（默认 `Xenon 1902`）的设备，自动定位端口
2. **串口读码**: 监听 `SerialPort.DataReceived`，按换行符把串口数据切分成一条条完整条码（兼容 CR/LF/CRLF 结尾）
3. **断线自动重连（静默心跳）**: 未插入/中途掉线时，UI 线程定时器每 3 秒后台静默重试连接（【V1.16.2】不再"重试几次就放弃"，失败过程不刷日志，只在连上/断开边沿各提示一次）；打开"录入批号"窗口时按需重连（`TryReconnectNow`），仍连不上弹窗提示"扫码枪未连接，请先连接"
4. **端口存活心跳（V1.16.3 重写：动态搜索确认设备是否真在）**: 拔掉 USB 虚拟串口时 `SerialPort` 的 `ErrorReceived`/`DataReceived` 事件在端口安静时【不一定触发】；原方案查 `GetPortNames()`（注册表 COM 列表在应用持有句柄时可能残留）和 `ReadExisting()` 探测（多数 USB 转串口驱动被拔后静默返回空串、不抛异常）都不可靠。V1.16.3 改为：重连定时器每次 Tick 先执行 `CheckConnectionAlive()`，动态识别模式下**重新跑一遍连接建立时那套 WMI 设备关键词搜索**（`FindMatchingPorts()`）——设备被拔掉后 PnP 节点消失、WMI 搜不到该串口 → 判定断连（WMI 反映物理设备是否真在，不受注册表残留/打开句柄影响）；WMI 查询失败时退回 I/O 探测兜底，不误判断连。判定成立即断连、提示一次"已断开"，随后静默重连恢复
5. **线程安全事件**: 扫码/状态事件通过 `SynchronizationContext` 封送到 UI 线程，订阅者可直接更新控件
6. **配置化**: `ScannerEnabled` 关闭时不连接（现场没装扫码枪时不影响整机启动）

**事件**:

| 事件 | 触发时机 |
| :--- | :--- |
| `OnBarcodeScanned` | 扫到一条完整条码时触发，参数为条码内容（已在 UI 线程） |
| `OnStatusChanged` | 连接成功/未找到端口/连接失败/串口错误时触发，参数为中文状态文本（已在 UI 线程） |

**关键方法**:

| 方法 | 功能 |
| :--- | :--- |
| `Start()` | 启动服务（未启用则跳过；启用则启动重连定时器并立即尝试连接） |
| `Stop()` | 停止服务（停定时器 + 关串口） |
| `FindScannerPort()` | WMI 按关键词自动定位扫码枪 COM 口（基于 `FindMatchingPorts()` 取第一个匹配） |
| `FindMatchingPorts()` | WMI 按关键词搜索匹配串口列表（null=查询失败 / 空列表=设备已不在 / 非空=设备还在）；连接建立与心跳断连判定共用 |
| `CheckConnectionAlive()` | 心跳：重新跑 WMI 动态搜索确认扫码枪设备是否真在，判定断连并提示一次（V1.16.3） |
| `SerialPort_DataReceived()` | 串口数据接收，按行切分成条码并触发事件 |
| `Dispose()` | 释放资源（停定时器 + 关串口 + 清事件引用） |

**业务接入点**:
- 主窗体 `MainForm_Load` 时启动；`MainForm_FormClosing` 时释放
- 扫码结果由主窗体写 LOG 日志；ID绑定窗体（`IdBindingForm`）打开时订阅同一服务，扫码自动识别"工位号"（恰好2位数字，如 01~72）/"产品SN" 并填入对应输入框，两条都齐后自动加入产品列表（乱序扫码也能正确配对，V1.16 更新支持工位号扫码）

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
- 【V1.16.1 修复】小数位**固定用配置 `BarometerDefaultDecimalPlaces`（=1）**，不再读设备 0x0002 ——
  现场实测 72 台中 47 台的 0x0002 返回 0（不可靠），旧逻辑按 0 位小数换算会把 -95 写成寄存器 -95，
  仪表（实际 1 位小数）显示 -9.5。压力读取 `ReadData` 同样固定 1 位小数，两处与仪表显示完全一致。
- **单位注意**：`thresholdValue` 是"设备单位"（与压力读数同单位同小数位），**不是**软件报警阈值 `AlarmPressureThresholdKPa`（kPa）。单位未按说明书确认前不要写。
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
- 【V1.16.2】断线后后台静默持续重连（10 秒节流，失败过程不刷日志，只在连上/断开边沿提示一次）；定值启动/停止按钮按需重连（`ReconnectNow`），连不上弹窗提示"送风机未连接，请先连接"
- 送风机是"可选设备"，连接失败不影响整机启动；用独立定时器轮询（2s），不阻塞 72 台气压表采集

---

### 3.4 模型层 (Models)

#### 3.4.1 BarometerData（气压表数据模型）

| 属性 | 类型 | 说明 |
| :--- | :--- | :--- |
| `DeviceId` | int | 气压表编号（1-72） |
| `VacuumPressure` | decimal | 真空压力值（kPa） |
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
| `PortName` | string | COM9 | 串口（气压表 RTU，RS485→USB；V1.16 起连接成功会缓存到 BarometerPort.cache，下次优先用缓存端口，缓存失效自动重新识别 CH340） |
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
| `BarometerPressureRegisterAddress` | ushort | 0x0001 | 压力寄存器（0x0002 为小数位，实测不可靠，转换不再使用） |
| `BarometerDefaultDecimalPlaces` | int | 1 | 小数位默认值（压力读取与阈值写入统一使用，不再读设备 0x0002；换气压表时按新表实际小数位改这里即可） |
| `BarometerPressureScale` | decimal | 1 | 压力缩放系数 |
| `AlarmPressureThresholdKPa` | decimal | -95 | 报警压力阈值（kPa，V1.19.9 由 Pa 改为 kPa；公共参数窗口保存负压值时实时同步） |
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
| `NegativePressure` | decimal | 负压值设定（kPa） |
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
│   ├─ 成功 → 更新权限显示（V1.19.7 起角色名着色：管理员=红/技术员=天蓝/操作员=绿），启用/禁用相关按钮，写入日志             │
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
| `PortName` | 串口（气压表 RTU；V1.16 起连接成功缓存到 BarometerPort.cache，下次优先用缓存端口） | COM9 |
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
| `BarometerPressureRegisterAddress` | 压力寄存器（0x0001，0x0002 为小数位，实测不可靠，转换不再使用） | 0x0001 |
| `BarometerDefaultDecimalPlaces` | 小数位默认值（压力读取与阈值写入统一使用，不读 0x0002；换气压表时按新表改这里） | 1 |
| `BarometerPressureScale` | 压力缩放系数 | 1 |
| `AlarmPressureThresholdKPa` | 报警压力阈值（kPa，如 -95） | -95 |
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
| `ScannerEnabled` | 【V1.16】是否启用扫码枪（false=不连接，现场没装扫码枪时用） | false |
| `ScannerPort` | 【V1.16】扫码枪固定串口（留空=按关键词 WMI 自动识别） | 空 |
| `ScannerDeviceKeyword` | 【V1.16】扫码枪设备识别关键词（设备管理器显示的名称关键字） | Xenon 1902 |
| `ScannerBaudRate` | 【V1.16】扫码枪串口波特率（Demo 实测 115200） | 115200 |
| `ScannerDataBits` / `ScannerStopBits` / `ScannerParity` | 【V1.16】扫码枪串口数据位/停止位/校验位 | 8 / 1 / None |

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
| 送风机通信协议 | 已确认 | Modbus TCP，厂商控制屏（192.168.1.220:50000，ModbusTCPFanControllerTest Demo 实测；自动识别候选 .220/.221/.222） |
| IO耦合器连接方式 | 已确认 | 以太网 Modbus TCP，GX-CL140 模组（现场无 PLC；V1.16 起耦合器断开不影响气压表采集；V1.16.2 断线后后台静默持续重连，只在连上/断开边沿提示一次，操作时按需重连并弹窗提示） |
| 数据存储方案 | 部分确定 | 事件日志已落盘 CSV（Logs\TestLog_*.csv）；数据库方案待定 |

### 5.2 预留的功能接口

| 功能 | 状态 | 文件位置 | 说明 |
| :--- | :--- | :--- | :--- |
| 用户权限管理 | 已实现 | MainForm.cs / UserManager.cs / LoginForm.cs / UserManagementForm.cs | 下拉菜单（操作员/技术员/管理员）+ 登录窗体 + 用户管理（管理员修改他人账号）；用户数据持久化到 Users.json（V1.13） |
| 参数设置-公共参数 | 已实现（V1.16） | MainForm.cs / CommonParameterForm | 公共参数窗口：输入负压值 → 后台线程批量写入所有气压表阈值寄存器（0x0010，写入期间暂停采集防串口争抢，阈值换算固定 1 位小数）→ 汇总成功/失败台数；串口未连接时提示"未连接任何气压表" |
| 参数设置-配方管理 | 部分实现 | MainForm.cs / RecipeManagerForm | 配方列表显示已实现，新增/编辑/删除逻辑待实现 |
| 日志记录-历史记录 | 已实现（V1.15） | MainForm.cs / HistoryRecordForm | 读取 Logs\TestLog_*.csv 真实事件日志，按日期查询 |
| 关于-设置 | 已实现（V1.17，仅管理员） | MainForm.cs / SettingsForm | 管理员登录后"关于"下拉菜单才显示"设置"（V1.19.12 按钮名由"帮助"改"关于"）；弹出系统设置窗口，单页纵向按业务分类标题条隔开展示 App.config 全部配置项（设置名称/说明/设置值），可直接编辑并写回 exe.config（重启生效），保存前按类型校验 |
| 关于-版本说明 | 已实现 | MainForm.cs | 版本信息弹窗已实现（V1.19.12：菜单项"关于"改"版本说明"，处理函数更名 MenuHelpVersionInfo_Click） |
| 行全选按钮 | 已实现（V1.18 更名） | MainForm.cs | 每行最右侧"全选"按钮（原名 Set(SEL_N)，V1.19.2 起灰色背景，V1.19.4 改浅灰 LightGray、与上电状态灯同色），点击选中该行所有面板（V1.19.5 起选中行面板显示绿底白✓框，其它行面板显示空心白框；V1.19.2 起面板背景色/工作状态不再变化）；V1.19.1 起按钮文字实时反映该行选中状态：整行全部选中→"取消"（点击整行取消），任一台被单独取消（长按选中/单击空白处或点击选中框取消）→按钮立即变回"全选" |
| 面板批量操作 | 已实现（V1.15） | MainForm.cs / WorkstationPanelView | 选中面板后执行：开启真空 / 启动运行 / 停止运行 / 报警复位 |
| 送风机定值启动 | 已实现（V1.15） | MainForm.cs / FanControllerClient.cs | 送风机 Modbus TCP 接入，定值启动/停止 + 温度湿度监视 |
| 送风机定值停止 | 已实现（V1.15） | MainForm.cs / FanControllerClient.cs | 手动停止；有台测试时自动保持运行 |
| 开启真空（选中台） | 已实现（V1.15） | MainForm.cs | 对选中面板打开真空电磁阀（单动作，预检用） |
| 启动运行（选中台） | 已实现（V1.15） | MainForm.cs / DeviceManager.cs | 开真空+载台上电+送风机定值启动+进测试，真空确认+老化计时 |
| 停止运行（选中台） | 已实现（V1.15） | MainForm.cs / DeviceManager.cs | 关阀+断电+退出测试（末台时送风机自动停止） |
| 报警复位（选中台） | 已实现（V1.15） | MainForm.cs / DeviceManager.cs | 人工解除故障状态，可重新测试 |
| 全部停止（急停） | 已实现（V1.15） | MainForm.cs / DeviceManager.cs | 一键全关阀+全断电+停送风机，带防误触确认 |
| 工位设置窗口 | 已实现（V1.18） | MainForm.cs / Dialogs/StationSettingsForm.cs | 面板"设置"按钮打开，左侧 6 个设置项（状态/SN/配方/延时时间/启动时间/极限温度），右侧按钮列（破空/下电/保存/加入对列/关闭窗口）；【V1.19.11】"保存"已实现：SN/配方/延时开启/延时到达 写入 DeviceManager 工位静态信息并同步面板（破空/下电/加入对列 仍待确认） |
| 单台手动控制 | 已实现（V1.15） | Dialogs/DeviceManualForm.cs | 面板"设置"按钮打开（V1.18 起由工位设置窗口替代），点动阀/载台电 + 实时 DI 状态 |
| 批量设置配方 | 已实现 | MainForm.cs / BatchRecipeForm.cs | 批量设置配方窗口已实现，支持配方名称、延时时间1/2、启动时间、极限温度输入，以及配方队列管理；配方批量应用到选中面板待实现 |
| 录入批号 | 已实现 | MainForm.cs / InputLotForm.cs | 录入批号窗口已实现，支持手动输入批号、输入校验、Enter键确认；确定后弹出ID绑定界面；批号写入 DeviceManager 供日志追溯 |
| ID绑定 | 已实现 | InputLotForm.cs / IdBindingForm.cs | ID绑定窗口已实现，支持工位编号和SN输入、产品列表显示、重复工位覆盖确认、保存功能；【V1.16】扫码枪自动识别工位号（恰好2位数字）/产品SN 填入对应输入框，两条都齐后自动加入产品列表（乱序扫码正确配对）；【V1.19.11】保存时把"工位 → SN"写入 DeviceManager，工位面板 SN 与绑定同步（手动输入同样关联）；保存时自动生成Excel文档（命名规则：批号_日期_时间.xlsx），包含批号、工位号、SN、配方名称、延时时间、启动时间列；ID绑定数据持久化待实现 |
| 老化计时自动停止 | 已实现（V1.15） | DeviceManager.cs | 真空确认后开始计时，到达 MaxTestDurationSeconds 自动停止并记日志 |
| 报警事件落盘 | 已实现（V1.15） | TestEventLogger.cs | 启动/停止/报警/复位/急停/真空建立 写入 Logs\TestLog_yyyyMMdd.csv |
| 日志持久化 | 部分实现（V1.15） | TestEventLogger.cs | 事件日志已落盘 CSV；界面 LOG 文本框仍未写文件 |
| 配置持久化 | 部分实现（V1.17） | Dialogs/SettingsForm.cs | "关于→设置"可查看/编辑 App.config 全部配置项并写回 exe.config（重启生效）；其余设置窗体（配方管理等）的配置仍仅内存生效 |

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
│   │   ├── ScannerService.cs               # 【V1.16新增】扫码枪服务（WMI识别串口+串口读码+重连）
│   │   ├── TestEventLogger.cs              # 【V1.15新增】测试事件 CSV 落盘
│   │   ├── DeviceManager.cs                # 设备管理器（业务编排核心）
│   │   └── UserManager.cs                  # 【新增】用户管理服务（登录/改密/权限校验）
│   ├── Views/                              # 视图层
│   │   ├── MainForm.cs                     # 主窗体（业务逻辑）
│   │   ├── MainForm.Designer.cs            # 主窗体（设计器代码，含 rootScrollPanel 滚动容器）
│   │   ├── WorkstationPanelView.cs         # 工位显示面板（业务逻辑，V1.16 更名）
│   │   └── WorkstationPanelView.Designer.cs# 工位显示面板（设计器代码）
│   └── Dialogs/                            # 对话框窗体层（菜单按钮弹出窗体）
│       ├── CommonParameterForm.cs                 # 公共参数设置窗体
│       ├── CommonParameterForm.Designer.cs
│       ├── RecipeManagerForm.cs                   # 配方管理窗体
│       ├── RecipeManagerForm.Designer.cs
│       ├── HistoryRecordForm.cs                   # 历史记录查询窗体
│       ├── HistoryRecordForm.Designer.cs
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
│       ├── DeviceManualForm.cs                     # 【V1.15新增】单台手动控制（面板"设置"按钮打开，V1.18 起由工位设置窗口替代）
│       ├── DeviceManualForm.Designer.cs
│       ├── SettingsForm.cs                         # 【V1.17新增】系统设置（关于→设置，编辑 App.config 全部配置项）
│       ├── SettingsForm.Designer.cs
│       ├── StationSettingsForm.cs                  # 【V1.18新增】工位设置窗口（面板"设置"按钮打开）
│       └── StationSettingsForm.Designer.cs
└── README.md                                 # 本文档（使用/架构说明）
```

### 6.1 视图层文件拆分说明（重要）

WinForms 视图层采用 **partial class（分部类）** 机制，每个窗体/控件拆分为两个文件：

| 文件 | 职责 | 说明 |
| :--- | :--- | :--- |
| `MainForm.cs` | 业务逻辑 | 事件处理、数据绑定、业务方法 |
| `MainForm.Designer.cs` | 设计器代码 | 控件创建、布局属性、Dispose 方法 |
| `WorkstationPanelView.cs` | 业务逻辑 | 数据更新、状态切换、事件处理（V1.16 更名自 BarometerPanelView） |
| `WorkstationPanelView.Designer.cs` | 设计器代码 | 控件创建、布局属性、Dispose 方法 |

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
5. **双击 MainForm.cs 或 WorkstationPanelView.cs 打开设计器**

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
- `WorkstationPanelView.cs`（原 BarometerPanelView.cs）：`: UserControl` → `: System.Windows.Forms.UserControl`
- `MainForm.cs`：`: Form` → `: System.Windows.Forms.Form`

### 8.3 版本更新

- 当前版本: V1.19.8
- 更新日志:
  - V1.19.8 (2026-08-08): 用户管理窗体优化。删除底部灰色操作提示 lblTip，窗体高度 365→330；"当前用户名"行改为"当前角色"行，值显示角色中文名并按角色着色：**技术员=蓝色、操作员=绿色**（管理员=红色为防御分支），不再显示用户名（新增 `UpdateRoleDisplay`）。详见 CHANGELOG.md
  - V1.19.7 (2026-08-08): 权限显示角色名按角色着色。右上角"当前操作权限"由单个标签拆为"前缀 + 角色名"两个标签（panelPermission 内 FlowLayoutPanel 水平排列，观感不变），角色名 `lblPermissionRole` 运行时按权限设 ForeColor：**管理员=红色、技术员=天蓝色、操作员=绿色**，前缀"当前操作权限: "保持默认色（新增 `UpdatePermissionDisplay`，替换原 `lblPermission.Text` 赋值）。详见 CHANGELOG.md
  - V1.19.6 (2026-08-08): 选中框显示时单击改"切换"选中状态。选中框显示期间（已有任一工位被选中），单击面板空白区域或点击选中框 = **切换**该工位"选中/未选中"（此前为"单击取消"）；例外：整表**唯一**选中的工位被切换为未选中时 → 全表无选中，所有面板选中框自动隐藏（主窗体 `UpdateSelectionBoxVisibility` 已覆盖该规则）。首次/新增选中仍需在空白区域长按约 0.8 秒。详见 CHANGELOG.md
   - V1.19.5 (2026-08-08): 选中交互改为"长按选中 + 有选中才显示绿✓框"。移除右上角选中框常驻显示与"点击切换选中"：改为在面板空白区域**长按约0.8秒**选中该工位、**单击**空白区域或点击选中框取消选中（长按期间移动超阈值视为拖动取消计时）；选中框平时全部隐藏，只要有任一工位被选中→所有面板同时显示框（选中项=绿底ForestGreen+白色✓，其它项=空心白框），全部取消→全部隐藏（主窗体 `UpdateSelectionBoxVisibility` 统一协调）；选中框样式由浅蓝底绿勾改为绿底白勾。详见 CHANGELOG.md
   - V1.19.12 (2026-08-08): 工位面板高度减小 + 主菜单"帮助"更名"关于"。工位面板 Size 240×225→240×205（内容最低点 y≈189~195，底部空白约30px→约10px），网格行高 PanelRowHeight 245→225；主菜单第4个按钮 `btnHelp`（"帮助"）→ `btnAbout`（"关于"），点击下拉菜单项"关于"→"版本说明"，处理函数更名 `btnHelp_Click`→`btnAbout_Click`、`MenuHelpAbout_Click`→`MenuHelpVersionInfo_Click`。详见 CHANGELOG.md
   - V1.19.11 (2026-08-08): 工位 SN/配方/延时关联打通。真实气压表只上报压力，新增 `StationInfo`（Models/StationInfo.cs）模型与 DeviceManager 每工位静态信息存储（`_stationInfo` 字典），采集时在 `CollectData` 叠加到该工位数据上（`ApplyStationInfo`，仅覆盖已配置字段），工位面板 SN/配方/延时显示与设置/绑定一致；工位设置窗口"保存"按钮实现（SN/配方/延时开启/延时到达 写入 DeviceManager，延时格式 时:分:秒 校验、空白清空，回显补充启动时间即延时到达，极限温度待配方表接入）；ID绑定保存时把"工位 → SN"写入 DeviceManager（`SetStationSerialNumbers`），手动输入工位号+SN 与扫码枪扫码等效关联；MainForm 把 `_deviceManager` 逐级传入 InputLotForm/IdBindingForm；主菜单"LOG记录"按钮更名"日志记录"。详见 CHANGELOG.md
  - V1.19.4 (2026-08-08): 行全选按钮改浅灰 + 工作状态配色统一为"信号灯"色系。行"全选/取消"按钮背景色由深灰（Gray）改为浅灰（LightGray，与上电状态灯 boxPower 同色）、文字改黑色；工作状态（boxWorkState）配色：空闲=浅灰底黑字 / 选中(已上电待测试)=橙底白字 / 繁忙(测试中)=绿底白字 / 故障=红底白字（原浅粉底红字改醒目红底），并加 ToolTip 说明配色。详见 CHANGELOG.md
  - V1.19.3 (2026-08-08): 面板布局微调。上电状态灯（boxPower）前加"上电"标题（标题与下方各标题左对齐、灯与内容列左对齐，x=57）；SN/配方内容显示由只读 TextBox 改为 Label（lblSNValue/lblRecipeValue，白底 + 边框，观感与只读框一致）。详见 CHANGELOG.md
  - V1.19.2 (2026-08-08): 选中状态仅由右上角选中指示体现 + 行全选按钮改灰色。移除面板整体选中高亮（`UpdateStatusColor` 不再叠加浅蓝背景、`UpdateWorkState` 去掉"已选中→选中"规则、`UpdateSelectionStyle` 只刷新选中指示）；行全选按钮背景色由道奇蓝改为灰色。详见 CHANGELOG.md
  - V1.19.1 (2026-08-08): 行全选按钮文字改为**实时反映该行选中状态**。新增 `UpdateRowSelectButton(rowIndex)`（该行所有面板全部选中→显示"取消"，任一台被单独取消→立即恢复"全选"）；WorkstationPanelView 新增 `IsSelectedChanged` 事件，`IsSelected` 实际变化时触发（点击面板本身 / 点击右上角选中指示 / 行全选按钮），MainForm 订阅后按所在行刷新按钮文字。详见 CHANGELOG.md
  - V1.19 (2026-08-08): 工位面板"上电/下电"按钮改为"选中指示"（btnPower → btnSelect）。原理解有误：btnPower 不是上电控制按钮，而是第一个标识——标识当前工位是否被选中。改动：移到面板右上角（NO.x 右侧），选中=浅蓝底+绿色"✓"，未选中=空心方框（黑框白底，无文字）；点击指示可切换选中（与点击面板本身一致）；移除原"上电/下电"文字与"测试中/故障时禁用"逻辑；清理死代码（移除 WorkstationPanelView.OnPowerToggled 事件、MainForm.Panel_OnPowerToggled 处理器及订阅；载台上电仍由"启动运行/停止运行"批量流程及工位设置窗口"下电"按钮控制）。详见 CHANGELOG.md
  - V1.18.1 (2026-08-08): 工位设置窗口细节调整。行"全选"按钮点击选中后文本变为"取消"（再点取消全选恢复"全选"）；点击工位面板本身即可选中该工位（状态变"选中"，再点取消）；工位设置窗口"状态"标题精简为"状态"两字；状态文字由英文改中文（IDLE→空闲 / SELECT→选中 / BUSY→繁忙 / FAULT→故障，工位面板与工位设置窗口同步）。详见 CHANGELOG.md
  - V1.18 (2026-08-08): 新增工位设置窗口（StationSettingsForm）。点击工位面板"设置"按钮（原 Set，V1.18 更名）弹出，标题"工位设置窗口 NO XX"；左侧一列 6 个设置项（设置项名+输入框，均左对齐、整列居中）：状态(IDLE/SELECT/BUSY)、SN、配方、延时时间、启动时间、极限温度；右侧一列按钮：破空 / 下电 / 保存 / 加入对列 / 关闭窗口（除"关闭窗口"外业务功能待确认，代码留 TODO 标记）；打开时从采集缓存回显当前工位数据。行全选按钮更名：每行最右侧按钮由 Set(SEL_N) 改为"全选"，点击选中该行所有工位面板、工作状态变为 SELECT（再次点击取消全选）；面板工作状态 SELECT 语义扩展：空闲且被选中（行全选）或已上电均显示 SELECT。详见 CHANGELOG.md
  - V1.17 (2026-08-07): 主窗体"关于"按钮更名"帮助"（控件 btnAbout → btnHelp），点击下拉菜单为【设置 / 关于】："设置"仅管理员可见（非管理员隐藏，并在入口再做兜底校验），弹出新增的系统设置窗口（SettingsForm，单页纵向按业务分类标题条隔开展示 App.config 全部配置项——基础配置 / 气压表串口通讯 / IO耦合器 / 气压表寄存器 / 报警参数 / 冷却送风机 / 老化测试业务 / 扫码枪，每类一个表格：设置名称/中文说明/设置值，可直接编辑，保存前按类型校验——整数/小数/布尔/十六进制寄存器地址，写回 exe.config 并刷新缓存、重启生效；界面使用 SunnyUI 控件呈现）；"关于"保留原版本信息弹窗。详见 CHANGELOG.md
  - V1.16 (2026-08-07): 接入真实扫码枪（ScannerService，参考 SerialScannerTest Demo）。WMI 自动识别串口（Honeywell Xenon 1902）+ 串口读码 + 断线自动重连；扫码结果写入 LOG 日志；ID绑定窗体打开时扫码自动识别"工位号"（恰好2位数字，如 01~72）/"产品SN" 并填入对应输入框，两条都齐后自动加入产品列表（乱序扫码也能正确配对）；修复ID绑定Excel导出样式（仅表头列名加粗+居中换行，不加灰底，数据行普通字体）；公共参数窗体简化为"负压阈值设置"（居中界面：负压值设定输入框 + 保存设置按钮，后台线程批量写入所有气压表阈值寄存器 0x0010，参考 ModbusRtuBarometerTest Demo 的 BatchSetThreshold，汇总成功/失败台数）；新增扫码枪配置项（ScannerEnabled/ScannerPort/ScannerDeviceKeyword 等，默认关闭）；移除 TEST 菜单按钮及扫码模拟窗体（ScanSimulationForm），扫码枪测试用真实扫码枪（LOG 看结果）；【同日】通讯连接修复：新增 CH340 串口自动识别（SerialPortHelper，气压表 RS485 适配器免配端口，App.config 端口改为实测 COM9，连接成功后端口缓存到 BarometerPort.cache、下次启动优先用缓存端口、缓存失效再自动重新识别——与送风机 FanLastIp.cache 同款"工控机记忆"机制）、DeviceManager 启动门禁解耦（只要气压表串口连通就启动采集，IO 耦合器/送风机断开不再拖垮整机）、新增启动/连接诊断写 LOG（OnDiagnostic）、IO 耦合器后台自动重连、ModbusTcpIoController 连接超时修复、公共参数批量写期间暂停采集防串口争抢 + 串口未连接明确提示；送风机监视区改为三项温度（设置温度/上部温度/下部温度，不显示湿度，上部温度=控制屏当前温度，下部温度为保留项）；顶部"通讯连接状态"控件更名（lblCommStatusLabel/lblCommStatus，现场无 PLC，用 GX-CL140 ModbusTCP 模组）；工位面板更名+重设计（BarometerPanelView → WorkstationPanelView：NO.x、上电状态灯、上电/下电按钮、真空压力、真空开启灯、工作状态 IDLE/SELECT/BUSY/FAULT、SN、配方、延时、Set 按钮，新增 OnPowerToggled 上电控制）；【同日V1.16.1】现场四项修复：①负压阈值 -95 写成 -9.5（0x0002 小数位寄存器实测 47/72 台不可靠返回 0，压力读取与阈值写入统一固定 1 位小数，写→读回实测验证 -95→寄存器-950→读回-95.0）；②顶部通讯连接状态改为只判断 IO 耦合器是否连接（OnConnectionStatusChanged 事件语义调整，耦合器断网读/写失败自动识别断开、自动重连后刷新）；③送风机监视区"上部温度"改"当前温度"、删除"下部温度"行；④送风机状态文字+颜色（未连接=红 / 定值启动·已连接=绿 / 定值停止=灰）；【同日V1.16.1】连接失败重试策略优化：扫码枪/耦合器/送风机自动重连连续失败 5 次后停止（不再无限空转），需要操作时按需重连（扫码枪 TryReconnectNow / 耦合器 EnsureIoConnected / 送风机 ReconnectNow / 气压表 SetAllThresholds 自动按需 Connect），仍连不上弹窗"xxx未连接，请先连接"；气压表小数位由配置 BarometerDefaultDecimalPlaces 统一控制（压力读取与阈值写入同源、不读 0x0002，换气压表改配置即可，写→读回实测验证通过）；【同日V1.16.2】连接心跳机制（静默自愈）：断连后状态 1~3 秒内更新并在日志提示一次"哪个设备断了"（耦合器/气压表随 1s 采集心跳、送风机 2s 轮询心跳、气压表新增串口级故障识别 IsPortLevelFailure——把"RS485 适配器被拔"和"单台表无响应"区分开、只有端口级异常才判定串口断开）、后台静默持续重连（取代上一版"重试 5 次就放弃"，失败过程不再刷日志，只在连上/断开边沿各提示一次）、操作时按需重连+弹窗兜底保留、测试期间送风机未连明确提示"温度不受控"；按需重连完全异步（耦合器/送风机按钮点下去先弹"连接中..."提示窗体、后台重连不卡界面，MainForm 改用 async/await + Task.Run）；底部状态栏新增"扫码枪"连接状态（已连接=绿/未连接=红/未启用=灰），与顶部"通讯连接状态"（耦合器）、送风机状态标签构成四设备状态总览；修复扫码枪断连状态不更新（拔掉 USB 虚拟串口时 SerialPort 的 ErrorReceived/DataReceived 在端口安静时不一定触发，重连定时器新增端口存活心跳 CheckConnectionAlive 双重判定——每 3 秒①核对当前端口是否仍在系统串口列表 GetPortNames、②对已打开句柄主动 ReadExisting() 探测【句柄失效时读取会抛异常，覆盖驱动在应用持有句柄时保留 COM 条目的情况】，任一判定成立即断连、提示一次"已断开"并静默恢复）；送风机"当前温度（上部温度）"颜色改为按设置温度对比（高于设置温度→红、不高于→绿，原按固定告警上限 FanTempAlarmLimitC 判断，超上限安全日志保留但不再覆盖颜色）
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
- [Views/WorkstationPanelView.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Views/WorkstationPanelView.cs)（原 BarometerPanelView.cs，V1.16 更名）
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
| 真空负压表信号处理 | 仅显示状态 | V1.12 读取压力值并按 AlarmPressureThresholdKPa 判定；V1.15 真空建立超时报警 |
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
