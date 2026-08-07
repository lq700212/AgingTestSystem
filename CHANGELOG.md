# CHANGELOG

## [2026-08-07] 公共参数窗体改为设置所有气压表负压阈值

### 问题
- 原"公共参数"窗体误做成"采集间隔 + 软件报警阈值（Pa）"的通用参数设置，
  与实际需求（设置所有气压表的设备负压阈值）不符，界面冗余。

### 改动（CommonParameterForm.cs / CommonParameterForm.Designer.cs / MainForm.cs）
- 窗体简化为"负压阈值设置"，界面全部居中显示：负压值设定标签 + 输入框 + 保存设置按钮。
- 保存逻辑参考 ModbusRtuBarometerTest Demo 的 `BatchSetThreshold`：
  - 校验输入（非空、必须是数字）；
  - 后台线程调用 `DeviceManager.SetAllBarometerThresholds` 批量写入所有气压表阈值寄存器（Holding Register 0x0010，功能码 0x06，驱动设备硬件报警触点）；
  - 写入完成切回 UI 线程汇总：全部成功 → 提示后关闭；部分失败 → 列出失败台号，窗口保持打开便于重试。
- 构造函数由 `CommonParameterForm(DeviceConfig)` 改为 `CommonParameterForm(DeviceManager)`（MainForm 同步更新调用与日志）。

### 使用说明
- 主界面 参数设置 → 公共参数：输入负压值（默认 -95，单位与气压表读数一致）→ 点击"保存设置"，
  等待后台批量写入完成，查看成功/失败台数汇总。

## [2026-08-07] 修复ID绑定Excel导出样式（仅表头加粗）

### 问题
- 保存ID绑定文档时，Excel 里"内容加粗、列名没加粗"，与期望（列名加粗、内容普通）相反。

### 原因（IdBindingForm.cs `CreateHeaderFormat`）
- 原实现只创建了 1 个"加粗"单元格格式并返回 `Count()`=1：
  - 表头行引用了样式索引 1，但实际只有索引 0，索引 1 不存在；
  - 数据行不指定样式，默认命中索引 0，恰好是那个"加粗"格式。
- 结果就是内容加粗、列名没加粗。

### 改动（IdBindingForm.cs）
- 按 OOXML 规范重建样式表，含两套单元格格式：
  - **格式 0：普通样式**（数据行用，不加粗、无填充）
  - **格式 1：表头样式**（列名用，加粗 + 居中换行，不加灰底）
- 补齐规范要求的 字体/填充/边框/单元格样式引用（cellStyleXfs） 及各项 count。
- 数据行 `CreateRow(..., 0)` 显式用普通格式，表头行用 `CreateHeaderFormat()` 返回的索引 1。
- 方法注释补充"原实现为何加粗反了"的说明，便于后续维护。

### 使用说明
- 保存ID绑定Excel后，第一行列名加粗并居中换行（白色背景、无灰底），下方数据行内容为普通字体。

## [2026-08-07] 录入批号支持扫码自动识别工位号与产品SN（防错机制）

### 问题
- 此前 ID 绑定的扫码只处理"产品SN"：工位编号需要手动输入（扫码时若工位编号未填，
  只把条码当 SN 填入并聚焦工位编号让用户补录）。
- 现场实际是"工位编号"和"产品SN"都由扫码枪扫入，且工人可能不按
  "先扫工位号、后扫SN"的固定顺序操作，需要防错机制保证乱序也能正常录入。

### 改动（IdBindingForm.cs / InputLotForm.cs 注释）
- 扫码结果按格式自动区分条码类型：
  - **恰好 2 位数字**（现场实测 01~72）→ 判断为**工位号** → 填入"工位编号"输入框
  - **其他内容**（产品SN一般不止 2 位）→ 判断为**产品SN** → 填入"SN"输入框
- 工位号和 SN 都扫齐后，自动调用"加入产品列表"（等效按回车）。
  原有逻辑保留：加入前检查重复工位号（重复则覆盖确认）→ 加入列表 →
  清空工位号/SN 输入框 → 聚焦工位编号。
- 防错效果：不区分先后顺序，先扫工位号或先扫 SN 都能正确配对录入。
- 新增 `IsStationNumber()` 静态辅助方法（恰好2位且均为数字）。

### 使用说明
- ID 绑定窗口打开后直接用扫码枪扫即可：工位号（2位数字）、产品SN（非2位数字）
  各扫一次，两条都齐后自动加入产品列表。
- 同一工位号重复绑定会弹"覆盖确认"对话框，选"是"则用新 SN 覆盖。
- 注意：若产品SN恰好只有 2 位数字，会被识别为工位号（现场 SN 一般不止 2 位，
  该判断规则是按现场实际情况设计的防错机制）。

## [2026-08-07] 移除 TEST 菜单按钮与扫码模拟窗体（ScanSimulationForm）

### 问题
- 真实扫码枪已接入（ScannerService，V1.16），扫码模拟窗体只用于"无硬件时手动模拟"，
  现场实际扫码流程是：主窗体 LOG 看读码结果 / 打开"录入批号 → ID绑定"扫 SN 自动填充，
  模拟窗体没有留着的意义。

### 改动（删除按钮 + 窗体 + 相关逻辑）
- 删除 `Dialogs/ScanSimulationForm.cs`、`Dialogs/ScanSimulationForm.Designer.cs`
  两个文件及其 [csproj](file:///e:/Project/BarometerWinform/BarometerWinform/BarometerWinform.csproj) 编译项。
- [MainForm.Designer.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Views/MainForm.Designer.cs)
  — 删除菜单栏 `btnTest` 按钮（控件声明 / 布局列 / 属性配置 / 字段声明），
  菜单容器 `tableLayoutPanelMenu` 由 5 列调整为 4 列（每个按钮均分 25%），
  关于按钮由第 4 列顺延到第 3 列。
- [MainForm.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Views/MainForm.cs)
  — 删除 `btnTest_Click` 与 `MenuTestScan_Click` 事件处理方法及 "TEST菜单项" 区域；
  同步更新头部布局注释（菜单栏去掉 [TEST]）。
- [ScannerService.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/ScannerService.cs)
  — 注释去掉对扫码模拟窗体的引用。

### 使用说明
- 主菜单栏现在是 4 个按钮：用户权限 / 参数设置 / LOG记录 / 关于。
- 扫码枪测试用真实扫码枪：启动后 LOG 显示"扫码枪已连接: COMx"，扫码后 LOG 记录
  "读码成功"；或打开"录入批号 → ID绑定"扫 SN 自动填充。
- 没有扫码枪时不再有模拟窗体，可把 `ScannerEnabled` 设为 `false` 跳过扫码功能。

## [2026-08-07] 启动流程暂时去掉 mForm_Progress 启动进度页

### 改动
- [Program.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Program.cs)：`Main()` 中注释掉 `ShowSplashScreen()` 调用，
  程序启动直接进入主界面，加快启动速度（省去 2 秒模拟加载等待）。
- **控件代码保留未删**：`Views/mForm_Progress.cs` / `mForm_Progress.Designer.cs` 及
  `ShowSplashScreen()` 方法原样保留，后续需要恢复启动进度页时，取消 `Main()` 中对应注释即可。

### 使用说明
- 当前启动流程：直接显示主窗体，无启动进度页。
- 需要恢复时：取消 `Program.cs` 中 `// ShowSplashScreen();` 的注释。

## [2026-08-07] 接入真实扫码枪（ScannerService，参考 SerialScannerTest Demo）

### 问题
- 此前扫码功能只有"扫码模拟窗体"（ScanSimulationForm，手动输入条码），
  真实扫码枪接入一直是预留项（Demo 已验证 Honeywell Xenon 1902 串口读码可行）。
- 现场 ID 绑定流程需要"扫描产品 SN"，需要真实扫码枪把条码读进来并自动填入 SN。

### 改动（新增扫码枪服务 + 业务接入 + 配置）
- 新增 [ScannerService.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/ScannerService.cs)
  （真实扫码枪服务，参考 SerialScannerTest Demo）：
  - WMI 查询 `Win32_PnPEntity` 按关键词（默认 `Xenon 1902`）自动识别串口；
    配置了固定端口（`ScannerPort`）时优先用固定端口。
  - 串口读码（115200/8/N/1，ASCII），按换行符把串口数据切分成一条条完整条码。
  - 未插入/掉线时 UI 定时器每 3 秒自动重连，现场无需手动重开。
  - 扫码/状态事件用 `SynchronizationContext` 封送到 UI 线程，订阅者可直接更新控件。
- [DeviceConfig.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Models/DeviceConfig.cs)
  / [App.config](file:///e:/Project/BarometerWinform/BarometerWinform/App.config)
  / [MainForm.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Views/MainForm.cs) `LoadConfig`
  新增扫码枪配置项：`ScannerEnabled`（默认 false=不连接）、`ScannerPort`（留空=自动识别）、
  `ScannerDeviceKeyword`（默认 Xenon 1902）、`ScannerBaudRate`（115200）、
  `ScannerDataBits`（8）、`ScannerStopBits`（1）、`ScannerParity`（None）。
- [MainForm.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Views/MainForm.cs)：
  - 构造函数创建 `ScannerService`，`MainForm_Load` 启动，`MainForm_FormClosing` 释放。
  - 订阅 `OnBarcodeScanned` / `OnStatusChanged` → 扫码结果与连接状态写 LOG 日志。
  - "录入批号"按钮把扫码枪服务传入 `InputLotForm`。
- [InputLotForm.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Dialogs/InputLotForm.cs)
  / [IdBindingForm.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Dialogs/IdBindingForm.cs)：
  - 扫码枪服务一路传入 ID 绑定窗体。
  - 扫码结果自动填充 SN 输入框：工位编号已输入 → 自动加入产品列表（等效按回车）；
    工位编号未输入 → 先填 SN、聚焦工位编号提示补录。
  - 窗体关闭（`OnFormClosed`）时退订扫码事件，避免回调已销毁窗体。
- 版本号 V1.15 → V1.16（主窗体标题、关于对话框、README）。

### 使用说明
- 现场有扫码枪：把 `App.config` 的 `ScannerEnabled` 置为 `true`，确认
  `ScannerDeviceKeyword` 与设备管理器里的名称一致（默认 `Xenon 1902`），
  程序启动后自动识别串口并连接，LOG 日志会显示"扫码枪已连接: COMx"。
- WMI 识别不到时：可在 `ScannerPort` 里直接填端口（如 `COM10`）用固定端口。
- 没有扫码枪：保持 `ScannerEnabled=false`（默认），扫码功能不影响整机启动。
- 扫码流程：主窗体 LOG 记录每次读码；打开"录入批号 → ID绑定"后扫码，
  SN 自动填充，工位编号已录入则自动加入产品列表。

## [2026-08-06] 移除"通信设置"按钮与 PLC 通讯设置窗体（项目无 PLC，改用 GX-CL140-S 耦合器通讯）

### 问题
- 现场没有 PLC，整个项目是通过 **GX-CL140-S 耦合器**（Modbus TCP）替代 PLC 进行通讯的。
- 主窗体菜单栏的"通信设置"按钮只弹出一个 **PLC 通讯设置窗体**（CommunicationSettingForm），
  该窗体配置的是 PLC 的 IP/端口/串口参数，对当前项目毫无用处，属于遗留死功能。

### 改动（删除按钮 + 相关逻辑）
- [MainForm.Designer.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Views/MainForm.Designer.cs)
  — 删除菜单栏 `btnCommunication` 按钮（控件声明 / 布局列 / 属性配置 / 字段声明），
  菜单容器 `tableLayoutPanelMenu` 由 6 列调整为 5 列（每个按钮均分 20%），其余按钮 TabIndex 顺延。
- [MainForm.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Views/MainForm.cs)
  — 删除 `btnCommunication_Click` 与 `MenuCommPlc_Click` 两个事件处理方法；
  删除"通讯设置菜单项"区域；`UpdateButtonPermissionStates` 中移除通讯设置按钮的权限控制；
  同步更新头部布局注释与权限注释。
- 删除 `Dialogs/CommunicationSettingForm.cs`、`Dialogs/CommunicationSettingForm.Designer.cs`
  两个文件及其 [csproj](file:///e:/Project/BarometerWinform/BarometerWinform/BarometerWinform.csproj) 编译项。
- **保留不动**：`DeviceConfig.PlcAddress/PlcPort/PortName/BaudRate` 等配置项及其
  App.config 读取逻辑——`PlcAddress/PlcPort` 仍被
  [ModbusTcpIoController.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/ModbusTcpIoController.cs)
  用来连接 GX-CL140-S 耦合器，`PortName/BaudRate` 仍被气压表串口读取使用。

### 使用说明
- 主界面菜单栏现在为：`用户权限 | 参数设置 | LOG记录 | TEST | 关于` 5 个按钮。
- 通讯参数的修改不再通过界面窗体进行，直接在 `App.config` 中改
  `PlcAddress`（耦合器 IP）/ `PlcPort`（默认 502）等即可。

## [2026-08-06] 送风机 IP 自动识别（候选 IP 动态可配置 + 工控机缓存记忆）

### 问题
- 现场冷却送风机控制器的 IP 可能是 `192.168.1.220` / `.221` / `.222` 中的任意一个
  （换工作台、换控制器都会变），端口都是 50000。
- 如果 IP 写死，每次换现场都要改配置/改代码；需要"识别监控多个 IP"的能力。
- 首次连接如果从候选列表开头试，可能白等超时；希望**记住本工控机上次连上的控制器 IP**，
  下次启动优先直接连，连不上再回落配置列表。

### 改动（自动识别 + 磁盘缓存，配几个识别几个）
- [DeviceConfig.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Models/DeviceConfig.cs)
  — 新增 `FanAutoDetectEnabled`（自动识别开关，默认 true）与 `FanIpCandidates`
  （候选 IP 列表，逗号/分号分隔，**配置多少个就能识别多少个**）；
  新增静态解析方法 `ParseFanIpCandidates`（自动过滤非法 IP 并去重）。
- [App.config](file:///e:/Project/BarometerWinform/BarometerWinform/App.config) —
  新增 `FanAutoDetectEnabled=true` 与 `FanIpCandidates=192.168.1.220,192.168.1.221,192.168.1.222`。
- [MainForm.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Views/MainForm.cs)
  — LoadConfig 读取并解析新配置项。
- [FanControllerClient.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/FanControllerClient.cs)
  — ① `ConnectInternal` 按顺序逐个尝试候选 IP（`BuildCandidateIps`：上次成功/缓存 IP → FanIpAddress → 候选列表），
  第一个连接成功的即为设备真实地址；新增 `ActiveIp` 属性供界面显示实际连上的地址。
  ② **磁盘缓存记忆**：连接成功后把 IP 写入程序目录下的 `FanLastIp.cache`（`SaveCachedIp`），
  下次启动 `LoadCachedIp` 优先恢复该地址直接连，连不上再回落配置列表（每台工控机各自记忆）。
- 测试工程 ModbusTCPFanControllerTest：
  - [FanControllerClient.cs](file:///e:/Project/BarometerWinform/ModbusTCPFanControllerTest/FanControllerClient.cs)
    — 新增候选 IP 列表构造函数与内置默认候选 `DefaultCandidateIps`，`EnsureConnectedAsync` 逐个尝试候选 IP
    （`BuildCandidateOrder`：缓存的上次成功 IP 优先），新增 `ConnectedIp` 属性；连接成功写 `FanLastIp.cache`。
  - [FanTestForm.cs](file:///e:/Project/BarometerWinform/ModbusTCPFanControllerTest/FanTestForm.cs)
    — 候选 IP = 界面填的 IP + 配置文件 `FanIpCandidates`（读 Demo 自己的 App.config，配几个识别几个）；
    连接成功日志显示实际连上的 IP。
  - [App.config](file:///e:/Project/BarometerWinform/ModbusTCPFanControllerTest/App.config)
    — 新增 appSettings 段与 `FanIpCandidates` 配置；csproj 补 System.Configuration 引用。

### 使用说明
- 生产工程：默认开启自动识别，`FanIpCandidates` 配几个 IP 就能识别几个；
  某工作台想固定单 IP，把 `FanAutoDetectEnabled=false` 或清空候选列表即可。
- 工控机记忆：连接成功后自动生成 `FanLastIp.cache`（在 exe 同目录），下次启动优先用它；
  设备换地址后，连不上缓存会自动回落配置列表找到新地址并更新缓存。
- 测试 Demo：候选 IP 在 `ModbusTCPFanControllerTest/App.config` 的 `FanIpCandidates` 里配，
  连接顺序为：缓存的上次成功 IP → 界面输入的 IP → 配置候选，逐个尝试。

## [2026-08-06] IO 输出备用通道映射（DQ 通道烧毁时启用，开关默认关闭）

### 问题
- 现场继电器问题导致 DQ 模块两个输出通道烧毁 / 电压不足（输出仅 16V，低于 24V），无法使用：
  - 寄存器 0x2000 的 00 通道（= 真空电磁阀-1，单通道信号值 0x0001）
  - 寄存器 0x2008 的 00 通道（= 载台上电-57 / Y200，单通道信号值 0x0001）
- 客户改用备用通道：**按顺序映射到 寄存器 0x2009 的 10 通道 和 11 通道**。
- 程序会复用到多个工作台，**多数工作台没有烧通道**，必须留开关（默认关闭，不影响正常现场）。

### 改动（开关 + 映射表，默认关闭）
- [Models/IoOutputChannelRemap.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Models/IoOutputChannelRemap.cs)
  — 新增映射模型 + 配置字符串解析（格式 `0x2000@0->0x2009@10;0x2008@0->0x2009@11`，
  寄存器十六进制、通道 0~15；非法项自动跳过并提示）。
- [DeviceConfig.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Models/DeviceConfig.cs) /
  [App.config](file:///e:/Project/BarometerWinform/BarometerWinform/App.config) /
  [MainForm.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Views/MainForm.cs)
  — 新增 `IoBackupChannelMappingEnabled`（总开关，默认 false）与 `IoBackupChannelMappings`（映射表）。
- [ModbusTcpIoController.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/ModbusTcpIoController.cs)
  — 写 DO / 读 DO / 批量读 DO 时按映射表重定向物理通道；批量读自动扩展寄存器范围以覆盖映射目标（如 0x2009）。
  业务输出编号、UI 显示、报警联动完全不变。
- 测试工程 ModbusTcpIoControllerTest：
  - [OutputChannelRemap.cs](file:///e:/Project/BarometerWinform/ModbusTcpIoControllerTest/OutputChannelRemap.cs)
    — 新增同语义配置工具（读测试工程 App.config）。
  - [MainForm.cs](file:///e:/Project/BarometerWinform/ModbusTcpIoControllerTest/MainForm.cs)
    — 写 DO 测试 / 循环写 DO 测试应用映射（观察目标通道指示灯即可验证）。
  - [PowerOnTestForm.cs](file:///e:/Project/BarometerWinform/ModbusTcpIoControllerTest/PowerOnTestForm.cs)
    — 载台上电测试支持把被映射通道（如 Y200）改写到 0x2009，行标签显示备用映射提示。
  - App.config / csproj — 新增开关配置与 System.Configuration 引用。

### 使用说明
- 默认关闭，正常工作台完全不受影响。
- 该现场启用时：把生产工程与测试工程各一份 App.config 里的
  `IoBackupChannelMappingEnabled` 改为 `true`，并按实际烧毁位置修改映射表即可。

## [2026-08-06] 送风机 Demo 连接防呆（IP/端口可编辑 + 连接超时，避免用错 IP 假死/报错）

### 问题
- 现场真实设备 IP 是 `192.168.1.220`，但 Demo / 工程默认值此前是 `192.168.1.221`（错误地址）。
- 用错 IP 时，.NET Framework 的 `TcpClient.ConnectAsync` **不受超时设置约束**，系统 TCP 连接默认要等
  约 20 秒才超时——期间界面像"假死"，也放大了"连接测试 vs 定时刷新"并发竞态的触发窗口
  （即此前 `EnsureConnectedAsync` 报 NullReferenceException 的诱因）。

### 防呆（本次改动）
- [FanControllerClient.cs](file:///e:/Project/BarometerWinform/ModbusTCPFanControllerTest/FanControllerClient.cs)
  — `EnsureConnectedAsync` 用 `Task.WhenAny` **自设连接超时**（timeoutMs=3000ms），连不上立即抛
  `TimeoutException`（"连接超时...请检查设备 IP/端口是否正确"），不再傻等 20 秒；
  重连 / 失败时同时清理旧的 `_master`，保证从干净状态重连。
- [FanTestForm.cs](file:///e:/Project/BarometerWinform/ModbusTCPFanControllerTest/FanTestForm.cs)
  — IP/端口改成**可编辑输入框**（默认 `192.168.1.220:50000`），设备地址变了直接在界面改、点【连接测试】应用，
  不用改代码重新编译；点【连接测试】前先做**格式校验**（合法 IPv4 + 端口 1~65535，填错立刻提示原因）；
  启动/停止前检测"地址已修改未应用"，给明确提示而不是发到旧地址。
- [FanTestForm.Designer.cs](file:///e:/Project/BarometerWinform/ModbusTCPFanControllerTest/FanTestForm.Designer.cs)
  — 新增「设备 IP / 端口」输入框控件。
- [App.config](file:///e:/Project/BarometerWinform/BarometerWinform/App.config) /
  [DeviceConfig.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Models/DeviceConfig.cs)
  — 生产工程送风机默认 IP `192.168.1.221` → `192.168.1.220`。

### 说明
- 生产工程（BarometerWinform）连接/断开本就全程 `lock(_syncRoot)` 串行化 + 手动连接超时，本次仅同步默认 IP。
- Demo 的连接竞态修复（Dispose 加锁 + 停表）沿用上一节，与本次防呆不冲突。

## [2026-08-06] 送风机 Demo 连接竞态修复（EnsureConnectedAsync 报 NullReferenceException）

### 问题
- ModbusTCPFanControllerTest 运行时，`EnsureConnectedAsync` 中 `await _tcpClient.ConnectAsync(_ip, _port);`
  报 `System.NullReferenceException: 未将对象引用设置到对象的实例。`

### 根因
- Demo 里 2 秒自动刷新定时器（`TimerRefresh_Tick`）和「连接测试」按钮（`BtnConnect_Click`）**共用同一个 `_client` 实例**。
- 点击「连接测试」会执行 `_client.Dispose()` + 重新 new 客户端，而此刻定时刷新可能正在旧实例里连接/读写；
  旧的 `Dispose()` **不拿 `_connectLock` 锁**，直接把正在连接的 `TcpClient` 半途关掉，
  底层连接被拆 → 抛 NullReferenceException（同一竞态有时表现为 SocketException）。
- 生产版（BarometerWinform）用 `Disconnect()` 全程持锁，无此问题；仅 Demo 存在。

### 修正
- [FanControllerClient.cs](file:///e:/Project/BarometerWinform/ModbusTCPFanControllerTest/FanControllerClient.cs)
  — `Dispose()` 改为拿 `_connectLock` 锁（最多等 2 秒，超时不卡 UI），销毁后清空 `_tcpClient/_master` 字段；
  `ConnectAsync` 前加防御性判空，极端并发下抛明确 `InvalidOperationException` 而非裸 NRE。
- [FanTestForm.cs](file:///e:/Project/BarometerWinform/ModbusTCPFanControllerTest/FanTestForm.cs)
  — `Form1_Load` 首次刷新、以及「连接测试/定值启动/定值停止」三个按钮操作期间统一**停掉自动刷新定时器**，
  消除「定时刷新 vs 按钮操作」并发碰同一个 `_client` 的竞态。

### 说明
- 生产工程（BarometerWinform）的连接/断开全程 `lock(_syncRoot)` 串行化，无需改动，本次仅加固 Demo。

### 问题
- ModbusTCPFanControllerTest 的 `txtState` 显示为**数字**（如 `1`）而不是"定值启动/定值停止"。

### 根因
- 寄存器 0x0001 读取时设备会回 4 个值（`0x0000`=程式停止、`0x0001`=程式启动、`0x0002`=定值停止、`0x0003`=定值启动），
  但 [FanCommand.cs](file:///e:/Project/BarometerWinform/ModbusTCPFanControllerTest/FanCommand.cs) 枚举只定义了 `0x0002/0x0003` 两个。
  设备回 `0x0000/0x0001` 时强转出的枚举"没有名字"，`state.ToString()` 打印裸数字。
  生产工程（BarometerWinform）早已用完整 `FanRunState` 枚举 + switch 映射中文，Demo 缺失了这步。

### 修正
- [FanCommand.cs](file:///e:/Project/BarometerWinform/ModbusTCPFanControllerTest/FanCommand.cs) — 枚举补全 0x0000~0x0003 四值（程式停止/程式启动/定值停止/定值启动），含中文注释。
- [FanTestForm.cs](file:///e:/Project/BarometerWinform/ModbusTCPFanControllerTest/FanTestForm.cs) — 新增 `GetStateText`：状态→中文显式映射 + 未知值兜底 `未知(0xXXXX)`，替换原残缺三元判断。
- 通信接口说明文档 v1.1：4.1 枚举示例补全、5.2 示例改为映射方式、版本历史新增 1.1。

### 说明
- 生产工程（BarometerWinform）状态映射已正确（`MainForm.cs` 的 FanRunState switch），无需改动，本次仅对齐 Demo。

## [2026-08-06] 气压表批量写阈值修复 + 设备阈值写入能力（V1.15 补充）

### 问题
- ModbusRtuBarometerTest「批量设置气压阈值」时，某台（如第 32 台）一直提示超时。

### 排查结论（实机验证）
- 用诊断工具在真实串口上扫描 72 台：**地址 32 读/写完全无响应**（断电 / 掉线 / 从站地址拨错 / 损坏），地址 30 偶发一次写超时后自行恢复。
- **这是设备问题，不是程序 bug**：Modbus 主站对"不响应的从站"只能等读超时（NModbus 默认重试 3 次，一台坏设备约阻塞 12s）。

### Demo 程序加固
- [Form1.cs](file:///e:/Project/BarometerWinform/ModbusRtuBarometerTest/Form1.cs) — 批量写阈值**不再逐台弹 MessageBox**（会卡住批量流程，失败几十台要点几十次确认），改为失败设备最后统一汇总："成功 N 台 + 失败名单"。
- [Form1.cs](file:///e:/Project/BarometerWinform/ModbusRtuBarometerTest/Form1.cs) — 新增**「批量读取压力」**按钮：扫描 1~72 台，直接定位离线/无响应的设备（排查"哪台写超时"用）。
- [Form1.cs](file:///e:/Project/BarometerWinform/ModbusRtuBarometerTest/Form1.cs) — 批量读取/批量写入新增 **CSV 日志落盘**（exe 同目录 `Logs\BarometerScan_yyyyMMdd.csv`，逐台 `时间戳,操作,从站号,结果,耗时ms,备注`），多扫几遍汇总 Excel 即可区分**永久离线**（每次满超时≈3s → 查供电/接线/设备）与**间歇掉线**（时好时坏 → 查干扰/供电不稳/接头接触），客观数据可直接交给电工。

### 上位机同步（BarometerWinform）
- [IBarometerReader.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Interfaces/IBarometerReader.cs) — 新增 `SetThreshold` / `SetAllThresholds` 接口（写设备阈值）。
- [ModbusRtuBarometerReader.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/ModbusRtuBarometerReader.cs) — 实现设备阈值写入（Holding Register 0x0010 / 0x06，值与 Demo 换算一致，含有符号范围越界保护）。
- [MockBarometerReader.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/MockBarometerReader.cs) — 同步实现接口。
- [DeviceManager.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/DeviceManager.cs) — 新增透传方法 `SetBarometerThreshold` / `SetAllBarometerThresholds`。

### 文档
- 气压表 Modbus RTU 通讯接入开发文档 更新至 v1.1：生产实现 V1.15 已与 Demo 一致；批量写失败汇总 + 离线扫描；补充"某台批量写超时 = 设备问题"实测排查经验。
- README / 通讯接入说明 同步补充设备阈值写入能力与排查提示。

### 待现场确认项
- 设备阈值（0x0010）的**单位/数值**需按气压表说明书确认后再写（不是 -95000Pa 那个软件报警阈值）。
- 排查地址 32 那台气压表的供电 / RS485 接线 / 从站地址拨码，修好后重跑批量写即可全成功。

## [2026-08-06] V1.15 业务串联 + 冷却送风机接入（老化测试业务流程闭环）

### 改动范围
- **新增** [FanData.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Models/FanData.cs) — 送风机数据模型 + 运行状态枚举
- **新增** [IFanController.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Interfaces/IFanController.cs) — 送风机控制接口（定值启动/定值停止/读状态）
- **新增** [FanControllerClient.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/FanControllerClient.cs) — 送风机 Modbus TCP 实现（同步版，移植自 ModbusTCPFanControllerTest Demo，带锁 + 断线重连节流 + 连接超时）
- **新增** [MockFanController.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/MockFanController.cs) — 送风机 Mock（无设备可演示）
- **新增** [TestEventLogger.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/TestEventLogger.cs) — 测试事件 CSV 落盘（Logs\TestLog_yyyyMMdd.csv）
- **新增** [DeviceManualForm.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Dialogs/DeviceManualForm.cs) — 单台手动控制对话框（面板 Set 按钮打开，点动阀/载台电 + 实时 DI）
- [DeviceManager.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/DeviceManager.cs) — 送风机接入（独立定时器轮询，不阻塞 72 台采集）、送风机全局生命周期（首台启动/末台停止）、测试状态机（StartTesting/StopTesting/ResetDevices/StopAll）、真空建立确认、通讯失联报警、老化计时自动停止、事件落盘
- [DeviceConfig.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Models/DeviceConfig.cs) / [App.config](file:///e:/Project/BarometerWinform/BarometerWinform/App.config) / [MainForm.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Views/MainForm.cs) — 新增送风机配置（FanEnabled/FanIpAddress/FanPort/FanUnitId/FanTimeoutMs）与业务参数（VacuumConfirmTimeoutMs/CommunicationLossAlarmCount/MaxTestDurationSeconds/UseDiAlarmContact/FanTempAlarmLimitC）
- [MainForm.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Views/MainForm.cs) / [MainForm.Designer.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Views/MainForm.Designer.cs) — 右侧面板改造：送风机监视区（状态/温度/湿度/设定值）、送风机定值启动/停止、启动运行/停止运行/报警复位/全部停止（急停）按钮全部接真实业务、状态栏新增"测试中/在线"统计
- [MockBarometerReader.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/MockBarometerReader.cs) — 模拟压力偏向"真空良好"，状态改由 DeviceManager 统一驱动
- [HistoryRecordForm.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Dialogs/HistoryRecordForm.cs) — 历史记录由 Mock 数据改为读取真实 CSV 日志文件

### 为什么这么改
- 需求：72 台气压表 + GX-CL140 IO + 冷却送风机（厂商控制屏，只做定值启动/停止）三条链路要在上位机里串成完整的老化测试业务。
- 设计评审（对抗性审查）修正了 4 个关键点：① 送风机是 72 台共享的环境设备，必须"首台启动时定值启动、最后一台停止时才停止"，不能随单台停止而停机；② 开阀后必须等真空建立（真空确认宽限窗口），否则刚开阀的常压会被误判报警；③ 气压表通讯失联必须报警（防止旧值假正常）；④ 报警后需人工复位。

### 优化点
- 送风机用独立定时器轮询（2s），通讯问题不拖累 72 台气压表采集与报警判定。
- 报警/启动/停止/复位/急停全部写入 CSV 日志，历史记录窗体可直接查询，支持追溯。
- 新增"全部停止（急停）"一键兜底：全关阀 + 全断电 + 停送风机。

### 待现场确认项
- 送风机实际 IP/端口**已确认**：`192.168.1.220:50000`（旧文档写的 221 是错误地址，已在 2026-08-06 防呆修复中同步）。
- UseDiAlarmContact（DI 报警触点并入判定）默认关闭，需现场确认触点电平后开启。
- 电磁阀阀型（2 位 3 通带排气 / 2 位 2 通）影响停机后泄压，现场确认。



### 改动范围
- [ModbusRtuBarometerReader.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/ModbusRtuBarometerReader.cs) — 新增气压表 Modbus RTU 读取实现（RS485→USB）
- [ModbusTcpIoController.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/ModbusTcpIoController.cs) — 新增 IO Modbus TCP 读写实现（GX-CL140 方式）
- [DeviceManager.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/DeviceManager.cs) — 增加 Mock/真实通讯切换、合并 IO 状态、报警边沿触发输出联动
- [DeviceConfig.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Models/DeviceConfig.cs) / [App.config](file:///e:/Project/BarometerWinform/BarometerWinform/App.config) / [MainForm.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Views/MainForm.cs) — 新增并加载通讯/阈值相关配置项
- [CommonParameterForm.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Dialogs/CommonParameterForm.cs) — 报警阈值写回内存配置（仍未做持久化）
- [BarometerWinform.csproj](file:///e:/Project/BarometerWinform/BarometerWinform/BarometerWinform.csproj) — 移除已合并的串口通讯 Demo 编译项

### 为什么这么改
- 现场目标是“气压阈值报警 → 输出控制(关阀/断电)”，必须把串口 RTU 与 TCP IO 打通到主程序主链路，而不是停留在测试 Demo。
- 原有 DeviceManager 固定使用 Mock，导致即使配置了串口/TCP 参数也无法进入真实通讯路径。

### 优化点
- 通过 `UseMockCommunication` 开关实现“无设备可跑 Mock，有设备可切真实”的开发/交付模式。
- IO 采用批量读（ReadAllInputs/ReadAllOutputs）并回填到每个 BarometerData，UI 面板能显示真实 IO 状态。
- 报警采用“边沿触发”避免持续报警时每周期重复下发 DO。

### 踩坑记录
- 气压表寄存器单位/缩放、IO 寄存器位序/是否取反等信息必须以现场说明书为准，已在代码中用 TODO 留口待通线验证。

## [2026-08-03] 适配 GX-CL140 80DI/160DO 模块容量

### 改动范围
- [DeviceConfig.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Models/DeviceConfig.cs) / [App.config](file:///e:/Project/BarometerWinform/BarometerWinform/App.config) / [MainForm.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Views/MainForm.cs) — 默认 IP 改为 192.168.1.20:502，默认 IO 通道数改为 80DI/160DO，并增加 InvertInputs/InvertOutputs 配置读取与一致性校验
- [ModbusTcpIoController.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/ModbusTcpIoController.cs) — 增加输入/输出逻辑取反配置支持（适配 NPN 低有效等现场差异）
- [IoMapBuilder.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/IoMapBuilder.cs) / [BarometerPanelView.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Views/BarometerPanelView.cs) — 当 TotalInputs != TotalBarometers 时，仍能正确计算输出点内部编号，并扩展“预留点”映射
- [通讯接入说明.md](file:///e:/Project/BarometerWinform/通讯接入说明.md) — 增补 80DI/160DO 与显耀IO表(72DI/144DO) 的关系说明

### 为什么这么改
- 现场 GX-CL140 实际接入的 IO 模块容量为 80DI/160DO，而显耀IO表仅定义了 72DI/144DO；程序需要既能按业务正确联动，也能容纳现场多出的预留通道，避免内部编号计算错位。

## [2026-08-03] 固化 GX-CL140 IO 寄存器位序（现场实测）

### 改动范围
- [ModbusTcpIoController.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/ModbusTcpIoController.cs) — 移除“位序/寄存器类型待确认”的 TODO，补充现场确认后的地址段与 bit 位序说明
- [通讯接入说明.md](file:///e:/Project/BarometerWinform/通讯接入说明.md) — 补充 0x1000/0x2000 对应通道范围（80DI/160DO）的表格化说明

### 为什么这么改
- 你在现场已通过 ModbusTCPTest 对 GX-CL140 + DQ50P-S 输出模块做了循环写入测试，确认了：
  - DO 起始 0x2000，每寄存器 16 路，bit0=第1路
  - 5 个 DQ50P-S（160DO）对应 0x2000~0x2009 共 10 个寄存器
  - DI 起始 0x1000，读取 Input Register（0x04），bit0=第1路

## [2026-08-03] 主视图面板布局调整为 8 列 × 9 行

### 改动范围
- [DeviceConfig.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Models/DeviceConfig.cs) / [App.config](file:///e:/Project/BarometerWinform/BarometerWinform/App.config) — PanelColumns/PanelRows 默认值调整为 8/9
- [MainForm.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Views/MainForm.cs) — 更新 TableLayoutPanel 布局说明注释（8×9 + 行全选按钮列）
- [README.md](file:///e:/Project/BarometerWinform/README.md) — 更新配置项默认值说明

### 为什么这么改
- 现场需求为 72 个气压表面板，但希望以“8列×9行”的方式排列显示，便于视觉对齐与操作习惯统一。
