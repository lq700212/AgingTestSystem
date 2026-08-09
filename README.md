# 老化测试系统（AgingTestSystem）

WinForms 桌面程序（.NET Framework 4.7.2 / C#）：监控 72 台气压表真空压力，控制 72 路真空电磁阀 + 72 路载台上电，
接入冷却送风机与扫码枪，实现**老化测试业务闭环 + 报警联动**。

## 0. 快速定位（AI / 新人从这里进）

| 想知道 | 去这里 |
| :--- | :--- |
| 设备通讯协议、寄存器、坑点、排障 | [`docs/通讯接入.md`](docs/通讯接入.md)（**唯一协议文档**） |
| 各文件职责、代码在哪 | 下文「3. 目录结构与职责」 |
| 配置项含义 | 下文「5. 配置项速查」 |
| 业务流（测试/报警/送风机） | 下文「4. 核心业务流」 |
| 改动历史 | 下文「8. 版本历史」+ `CHANGELOG.md` |
| 现场调试 | 主程序"关于"菜单：**通讯测试 / 送风机测试**（技术员及以上；复用主程序共享连接，不自建连接） |

> 原独立测试工程（`ModbusRtuBarometerTest` / `ModbusTcpIoControllerTest` / `ModbusTCPFanControllerTest` / `SerialScannerTest`）
> 已删除，其测试逻辑已合并进主程序与 `docs/通讯接入.md`。

## 1. 技术栈与设备规模

| 设备 | 数量 | 协议 / 接入 | 关键实现 |
| :--- | :---: | :--- | :--- |
| 真空负压表（气压表） | 72 | Modbus RTU，RS485→CH340 USB，**19200 8N1**，从站=设备号 1~72 | `ModbusRtuBarometerReader` |
| IO 耦合器 | 1 | 显耀 GX-CL140，Modbus TCP `192.168.1.20:502`，80DI/160DO | `ModbusTcpIoController` |
| 冷却送风机 | 1 | 厂商控制屏，Modbus TCP 端口 **50000**，定值启停 + 温湿度监视 | `FanControllerClient` |
| 扫码枪 | 1 | Honeywell Xenon 1902 虚拟串口 **115200**，WMI 自动识别串口 | `ScannerService` |

Mock 与真实实现由 `UseMockCommunication` 一键切换，接现场硬件只改配置、不改代码。

## 2. 架构与分层

```
MainForm / WorkstationPanelView×72（Views 视图层）
        ↓ 弹出
Dialogs（窗体层：CommonParameterForm / SettingsForm / StationSettingsForm /
          CommunicationTestForm / FanTestForm / InputLotForm / IdBindingForm ...）
        ↓ 调用
DeviceManager（服务层核心编排：采集/测试状态机/报警联动/送风机生命周期）
  ├─ IBarometerReader  (MockBarometerReader / ModbusRtuBarometerReader)
  ├─ IIoController     (MockIoController    / ModbusTcpIoController + IoMapBuilder)
  ├─ IFanController    (MockFanController   / FanControllerClient)
  ├─ ScannerService（扫码枪，独立）   UserManager（用户/权限，Users.json）
  └─ TestEventLogger（测试事件落盘 Logs\TestLog_yyyyMMdd.csv）
        ↓
Models（BarometerData / FanData / IoStatus / DeviceConfig / RecipeConfig / StationInfo）
```

- **IO 映射**：内部用十进制连续编号（输入 1~80、输出 81~240），`IoMapBuilder` 换算成三菱八进制物理地址（X/Y），
  每台气压表 = 1 输入（报警触点 DI）+ 2 输出（真空阀 Y + 载台上电 Y）。八进制：X007 后是 X010，Y107 后是 Y110。
- **事件**：`DeviceManager` 一次性触发 `OnBatchDataUpdated`（72 台数据数组，避免逐条 72 次 UI 切换）；
  `OnConnectionStatusChanged` / `OnFanDataUpdated` / `OnDiagnostic`。

## 3. 目录结构与职责（定位用）

| 文件 | 职责 |
| :--- | :--- |
| `Services/DeviceManager.cs` | **业务编排核心**：Start/Stop、采集定时（1s）、测试状态机（StartTesting/StopTesting/ResetDevices/StopAll）、报警边沿联动（IsAlarm→HandleAlarm 关阀+断电）、送风机生命周期 UpdateFanLifecycle、工位静态信息 StationInfo、共享连接门面（EnsureIoConnected/ReadHoldingRegisters/ReconnectFan/GetFanData/StartFan/StopFan） |
| `Services/ModbusRtuBarometerReader.cs` | 气压表 RTU：读压力（0x04 @0x0001~0002，`(short)值/10^小数位`）、写设备阈值（0x06 @0x0010）、72 台轮询、`_syncRoot` 串口锁 |
| `Services/SerialPortHelper.cs` | CH340 串口自动识别（WMI VID_1A86/PID_7523）+ 端口缓存 BarometerPort.cache |
| `Services/ModbusTcpIoController.cs` | IO 耦合器：DI 读 0x04 @0x1000、DO 写 0x06 / 读 0x03 @0x2000，读-改-写单点，备用通道映射，共享原始寄存器 API（ReadHoldingRegisters/WriteSingleRegister，供测试窗体复用同一连接） |
| `Services/IoMapBuilder.cs` | 内部编号 ↔ 三菱八进制 X/Y 地址映射 |
| `Services/FanControllerClient.cs` | 送风机 TCP：定值启停、读状态/温湿度，断线重连节流，IP 自动识别 + FanLastIp.cache 工控机记忆 |
| `Services/ScannerService.cs` | 扫码枪：WMI 识别串口、串口读码、断线心跳重连 |
| `Services/TestEventLogger.cs` | 测试事件 CSV 落盘（启动/停止/报警/复位/急停/真空建立） |
| `Services/UserManager.cs` | 用户/登录/权限，Users.json 持久化 |
| `Services/RecipeStorage.cs` | 配方列表持久化（Recipes.json，启动加载/操作即写盘；SaveWithDuplicateCheck 同名覆盖保存，V1.25/1.26） |
| `Services/StationSettingsCache.cs` | 工位配置缓存（StationSettings.json，按工位缓存 SN/配方/延时/极限温度，设置窗口下次打开自动回填，V1.26） |
| `Services/Mock*.cs` | Mock 实现（免接线演示） |
| `Views/MainForm.cs` | 主窗体：面板区（9×8）、菜单下拉、状态栏（"在线"全部离线标红，V1.24）、权限控制、扫码事件、操作区按钮 |
| `Views/WorkstationPanelView.cs` | 单个工位面板：压力/SN/配方/延时/状态灯/选中指示（长按约 0.8s 选中，已有选中时长按取消全部选中）；"设置"按钮按选中数量智能分流（V1.24） |
| `Dialogs/CommunicationTestForm.cs` | 通讯测试窗体（IO 耦合器 DO 输出测试，负压阀/载台上电两页 9×8 灯按钮 + 一键遍历） |
| `Dialogs/FanTestForm.cs` | 送风机测试窗体（定值启停 + 温湿度显示） |
| `Dialogs/SettingsForm.cs` | 系统设置（管理员，按分类编辑 App.config 全部配置项，写回 exe.config 保存即生效，连接参数自动重连；仅设备数量/布局/模拟开关等结构型配置重启生效） |
| `Dialogs/StationSettingsForm.cs` | 工位设置（SN/配方/延时/启动时间 写入 StationInfo；延时/启动时间三 NumericUpDown 冒号分隔，V1.28；保存=应用+缓存+存配方、加入对列=应用+存配方、下电=关闭载台上电） |
| `Dialogs/RecipeManagerForm.cs` | 配方管理窗口（左侧列表可滚动 + 右侧可编辑输入，延时/启动时间冒号分隔三 NumericUpDown，V1.28；添加/更新/删除操作即自动落盘 Recipes.json，V1.27 起无"保存设置"按钮） |
| `Dialogs/BatchRecipeForm.cs` | 批量设置配方窗口（配方名称/延时时间/启动时间/极限温度；延时/启动时间均三 NumericUpDown 冒号分隔，V1.28 删"延时时间2"，两个时间都写入配方：延时→延时开启、启动→延时到达；加入队列=保存配方+应用到选中工位，无选中先保存配方并提示选择） |
| `Dialogs/IdBindingForm.cs` / `InputLotForm.cs` | 录入批号 + 工位↔SN 绑定（扫码枪自动识别填充，生成 Excel） |
| `Models/` | BarometerData / FanData(+FanRunState) / IoStatus / DeviceConfig / RecipeConfig / StationInfo / 用户模型 |

> WinForms 视图均拆 `.cs` + `.Designer.cs` 两个 partial；**所有 .cs 必须 UTF-8 with BOM 编码**（否则设计器报"无法设计基类 System.Void"）。

## 4. 核心业务流

### 4.1 老化测试单台流程
```
[准备] 录入批号 → 绑定工位↔SN → 设配方
[启动] 开真空阀 + 载台上电 + 送风机定值启动 → 真空确认(VacuumConfirmTimeoutMs 默认15s 内压力进入正常区间，
        超时→真空建立失败报警：关阀+断电+标故障)
[老化] 真空确认后计时 → 达 MaxTestDurationSeconds 自动停止该台
[监控] 压力越限 / 通讯失联(连续失败≥CommunicationLossAlarmCount) / DI触点(可选) → 报警联动
[停止] 关阀+断电（末台时送风机自动停止）
[复位] 人工报警复位后回空闲，可重测
[急停] 全部停止：全关阀+全断电+停送风机（带防误触确认）
```

### 4.2 报警来源（DeviceManager.IsAlarm）
1. **压力越限**：真空压力 > `AlarmPressureThresholdKPa`（默认 -95kPa，即真空变差）
2. **真空建立超时**：开阀后 15s 压力未进正常区间
3. **通讯失联**：某台连续读取失败 ≥ `CommunicationLossAlarmCount` 次
4. **DI 报警触点**（可选）：`UseDiAlarmContact=true` 时启用，默认关

联动动作：**报警进入边沿触发一次**（不是周期重复）→ 关该台真空阀 + 断载台上电 + 标故障；结果写 CSV 日志。

> 气压表是"双信号"：压力值走 RTU 供软件判报警（**联动只由此触发**）；报警硬件触点走 DI（X000~X107，NPN）只读进 UI 显示，不参与联动。

### 4.3 送风机生命周期（72 台共用，不能随某台停机）
- 有任一台在测试 → 保持运行；全部停止 → 才允许停机。`UpdateFanLifecycle` 里"只下发一次命令"状态记忆防重复写。
- 手动定值启/停按钮在无台测试时仍可用。
- **IP 自动识别**：连接顺序 = FanLastIp.cache（上次成功）→ FanIpAddress → FanIpCandidates；候选列表配几个识别几个，设备换 IP 自动找到并更新缓存。

### 4.4 主界面操作入口（右侧"操作"区）
开启真空 / 启动运行 / 停止运行 / 报警复位 / 全部停止(急停) / 送风机定值启停 / 面板"设置" / 行"全选"。

> **面板"设置"按钮（V1.24 智能分流）**：点击时若按钮所在工位未选中，先将其加入选中集合；
> 然后只选中 **1 个工位** → 弹出该工位的工位设置窗口（`StationSettingsForm`）；
> 选中 **2 个及以上** → 弹出批量设置配方窗口（`BatchRecipeForm`）。

> **面板选中交互（V1.19.5~6 / V1.24）**：空白处"长按约 0.8s"选中该工位（选中框平时全隐藏，有选中才全部显示）；
> 已有选中时长按空白处 = **取消全部选中并隐藏所有选中框**；选中框显示时单击空白处/选中框 = 切换该工位选中状态。

## 5. 配置项速查（App.config，可在"关于→设置"管理员界面编辑；保存后大部分配置立即生效，连接参数自动重连，仅结构型配置重启生效）

| 配置项 | 默认值 | 说明 |
| :--- | :--- | :--- |
| `TotalBarometers` | 72 | 气压表总数 |
| `TotalInputs` / `TotalOutputs` | 80 / 160 | IO 总数（业务用 72/144） |
| `CollectInterval` | 1000 | 气压表采集间隔(ms) |
| `PortName` / `BaudRate` | COM9 / 19200 | 气压表串口（连接成功缓存 BarometerPort.cache 优先复用） |
| `UseMockCommunication` | false | true=Mock 免接线 |
| `InvertInputs` / `InvertOutputs` | false | 输入/输出逻辑取反（NPN/PNP 现场差异，灯亮软件读 OFF 时试 true） |
| `IoUnitId` | 1 | IO 耦合器从站 |
| `IoInputRegisterStartAddress` / `IoOutputRegisterStartAddress` | 0x1000 / 0x2000 | DI / DO 起始寄存器 |
| `IoBackupChannelMappingEnabled` / `IoBackupChannelMappings` | false / `0x2000@0x00->0x2009@0x10;0x2008@0x00->0x2009@0x11` | **备用通道映射**（DQ 通道烧毁时启用，源寄存器@通道->目标，寄存器/通道均十六进制，通道 0x00~0x1F，写/读 DO 自动重定向） |
| `BarometerPressureRegisterAddress` | 0x0001 | 压力寄存器（0x0002 为小数位，实测不可靠不再使用） |
| `BarometerDefaultDecimalPlaces` | 1 | 小数位（压力读取与阈值写入统一用，换气压表改这里） |
| `BarometerPressureScale` | 1 | 压力额外缩放 |
| `AlarmPressureThresholdKPa` / `AlarmWhenPressureHigherThanThreshold` | -95 / true | 软件报警阈值(不是设备阈值) |
| `PlcAddress` / `PlcPort` | 192.168.1.20 / 502 | IO 耦合器 |
| `FanEnabled` / `FanIpAddress` / `FanPort` | true / 192.168.1.220 / 50000 | 送风机（可选设备，连不上不影响启动） |
| `FanAutoDetectEnabled` / `FanIpCandidates` | true / .220,.221,.222 | 送风机 IP 自动识别 |
| `FanUnitId` / `FanTimeoutMs` | 1 / 3000 | 送风机从站/超时 |
| `VacuumConfirmTimeoutMs` | 15000 | 真空建立确认超时(ms) |
| `CommunicationLossAlarmCount` | 3 | 通讯失联报警阈值(连续失败次数) |
| `MaxTestDurationSeconds` | 0 | 老化最大时长(0=不限) |
| `UseDiAlarmContact` | false | DI 报警触点并入判定（需现场确认电平） |
| `FanTempAlarmLimitC` | 0 | 送风机温度告警上限(0=不启用) |
| `ScannerEnabled` / `ScannerPort` | false / 空 | 扫码枪开关 / 固定串口（空=WMI 自动识别） |
| `ScannerDeviceKeyword` / `ScannerBaudRate` | Xenon 1902 / 115200 | 扫码枪识别关键词 / 波特率 |

## 6. 菜单与权限

| 按钮 | 下拉项 | 权限 |
| :--- | :--- | :--- |
| 用户权限 | 操作员 / 技术员 / 管理员 / 用户管理* | *仅管理员 |
| 参数设置 | 公共参数（批量写气压表阈值）/ 配方管理 | 技术员+ |
| 日志记录 | 历史记录（读 CSV） | 任意 |
| 关于 | 设置* / 通讯测试** / 送风机测试** / 版本说明 | *仅管理员；**技术员+ |

默认账号（Users.json）：operator / technician / admin，密码均 123456（明文，生产应哈希）。

## 7. 关键设计决策与坑点（排障/新功能必读）

1. **共享连接（V1.23）**：通讯测试/送风机测试窗体**不自己建 TCP 连接**，复用 `DeviceManager` 的共享连接
   （`EnsureIoConnected`/`ReadHoldingRegisters`/`ReconnectFan`/`GetFanData`），连接状态与主界面实时一致。
   测试窗体读送风机状态走缓存（零额外报文）。
2. **IO 写 DO 必须读-改-写**：0x2004~0x2008 低/高字节被不同业务共用（如 0x2004 低字节=电磁阀65~72、高字节=载台上电1~8），
   整字直写会误伤其它通道。载台上电"全关"时也只清高字节、保留低字节。
3. **BeginInvoke 传数组参数必须 `new object[]{arg}` 包装**：`BarometerData[]` 会被协变成 params 展开 → TargetParameterCountException。
4. **UTF-8 with BOM**：所有 .cs 必须带 BOM，否则 WinForms 设计器"无法设计基类 System.Void"。
5. **两种阈值别混淆**：`AlarmPressureThresholdKPa`（软件判报警，kPa，不写设备）≠ 设备阈值（写气压表 0x0010，单位与压力读数一致）。
6. **小数位固定 1 位**（`BarometerDefaultDecimalPlaces`）：0x0002 小数位寄存器现场 47/72 台返回 0 不可靠，压力读取与阈值写入统一固定。
7. **有符号强转**：压力原始值须 `(short)` 强转，否则 0xFFFE 被当 65534，负压反号。
8. **批量写某台超时 = 设备问题**（掉线/断电/地址错/损坏），不是程序 bug；用批量读取/扫描 CSV（`Logs\BarometerScan_*.csv`）定位离线台。
9. **扫码枪断连判定用 WMI 动态搜索**（设备拔出 PnP 节点消失即判断连），不能用注册表/ReadExisting（都不可靠）。
10. **送风机状态枚举要 4 值**（0x0000~0x0003）+ UI 显式 switch 中文，不能 ToString（程式模式回 0/1 会显示裸数字）。
11. **送风机是可选设备**：连接失败不影响整机启动；用独立 2s 定时器轮询，不阻塞气压表采集。
12. **事件处理一律 IsDisposed 检查 + BeginInvoke**：避免窗体释放后 ObjectDisposedException（历史 H1/H3）。

## 8. 版本历史（详见 CHANGELOG.md）

| 版本 | 要点 |
| :--- | :--- |
| V1.30 | IO 触发后气压表压力值快速刷新：写输出成功（开/关阀、上/断电、启动/停止测试）对目标工位启动独立 250ms 高频补读，压力变化 ≤0.5 秒可见，跟踪 12s 后自动退出恢复正常轮询，不影响 72 台全量采集性能 |
| V1.29 | 移除时间字段 [JsonProperty] 兼容（JSON 键名直接用 DelayTime/StartTime，旧 Recipes.json/StationSettings.json 需删除重建）+ 工位面板配色微调：boxPower 下电 / boxVacuumOpen 真空关由红底白字改浅灰(LightGray)底黑字（与行全选按钮同色），红仅保留给工作状态"故障" |
| V1.28 | 时间输入样式统一：配方管理/批量设置/工位设置三窗口延时与启动时间均改三个 NumericUpDown + 冒号分隔（时:分:秒，时0-99/分0-59/秒0-59），命名统一 nudDelay*/nudStart*，字段映射对齐（延时→延时开启 DelayTime、启动→延时到达 StartTime；字段名由 DelayStartTime/DelayArriveTime 统一）；批量设置配方窗口删除"延时时间2"，且原被丢弃的"启动时间"修复为写入配方；工位设置窗口 txtDelay/txtStart 改 NumericUpDown 并调整读取（GetTimeSpan）/回填（SetTimeInputs 钳制）/提示（GetTimeText）逻辑 |
| V1.27 | 配方管理移除"保存设置"按钮：添加/更新/删除操作即自动落盘 Recipes.json（原仅"保存设置"落盘，现三个操作按钮每次成功后自动持久化，改动重启不丢失） |
| V1.26 | 批量设置配方"加入队列"=保存配方到本地+应用到选中工位（无选中先保存配方并提示选择工位）；工位设置"保存"=应用面板+缓存(StationSettings.json自动回填)+存配方、"加入对列"=应用面板+存配方、"下电"=关闭载台上电输出；RecipeStorage 新增 SaveWithDuplicateCheck 同名覆盖保存 |
| V1.25 | 配方管理窗口编辑化（名称输入框、时/分/秒分拆、极限温度）+ 列表选中同步显示 + 添加防重名询问更新 + 删除二次确认 + Recipes.json 持久化启动加载 + 配方列表垂直滚动条；批量设置配方窗口左侧标签与右侧输入框垂直居中对齐 |
| V1.24 | 面板"设置"按钮智能分流：点击工位未选中先加入选中；仅选 1 台弹工位设置窗口，选≥2 台弹批量设置配方窗口（`ShowBatchRecipeForm` 抽取复用）；长按空白处取消全部选中并隐藏选中框；状态栏"在线"全部离线标红、默认"未连接"标红；工位面板配色优化（真空关默认红底、测试中=黄灯色、空闲=LimeGreen、上电灯未上电=红） |
| V1.23 | 通讯测试/送风机测试窗体共享主程序连接（不自建 TCP）；DeviceManager/ModbusTcpIoController 新增共享原始寄存器 API |
| V1.22 | 通讯测试窗体：一键遍历通断跑马灯、打开自动连接+心跳断连提醒+遍历后台执行、0x2009 映射读-改-写、非模态映射提示窗 |
| V1.21 | 通讯测试窗体 SunnyUI 重构 + 备用通道映射点击提示 |
| V1.20 | 新增通讯测试窗体（IO DO 输出通道手动测试） |
| V1.19.x | 工位面板选中交互/配色系列优化、工位 SN/配方/延时关联（StationInfo）、真空显示文字化、报警阈值统一 kPa、权限角色着色、菜单"帮助"→"关于" |
| V1.18.x | 新增工位设置窗口 StationSettingsForm、行"全选"按钮、状态中文显示 |
| V1.17 | 系统设置窗口 SettingsForm（按分类编辑 App.config，仅管理员） |
| V1.16.x | 接入真实扫码枪 ScannerService、CH340 串口自动识别+端口缓存、连接心跳静默自愈/按需重连、小数位统一修复、工位面板更名重设计、IO 耦合器断网不影响气压表采集 |
| V1.15 | 老化测试业务闭环 + 送风机接入、真空确认/失联报警/老化计时/送风机生命周期、事件 CSV 落盘、设备阈值写入、送风机 IP 自动识别、备用通道映射 |
| V1.14 | 接入真实通讯链路（RTU+TCP），报警边沿联动 |
| V1.13 | 用户数据持久化 Users.json |
| V1.12 | 配方管理窗口左右分栏 |
| V1.09 | 接入显耀 IO 表（IoMapBuilder 八进制映射，1 输入+2 输出） |
| V1.08 | 用户权限系统（登录/用户管理/权限按钮）+ 自适应分辨率滚动条 |
| ≤V1.07 | 早期：下拉菜单、修复记录、初始 Mock 架构 |

## 9. 常见问题排查

| 现象 | 排查方向 |
| :--- | :--- |
| 气压表数据不更新 | 串口设置/CH340 驱动/从站地址/波特率 19200 |
| 只有部分台能读 / 批量写某台超时 | 该台掉线或损坏（地址错/断电），先扫描定位 |
| 现场灯亮软件读 OFF / 软件写 ON 灯灭 | `InvertInputs` / `InvertOutputs` 置 true |
| IO 通讯失败 | 网线插紧（ST 闪绿=接触不良）、同网段、SP+FP 都要供 24V |
| 面板显示不全 | PanelColumns×PanelRows ≥ 设备总数 |
| 设计器"无法设计基类" | .cs 存 UTF-8 with BOM；删 obj/bin/.vs 重建 |
| 启动弹 TargetParameterCountException | BeginInvoke 数组参数未包 `new object[]{...}` |
| 送风机连不上 | FanIpAddress/FanIpCandidates 配置、端口 50000、FanEnabled=true |
| 程序卡顿 | 增大 CollectInterval；确认采集/写操作在后台线程 |

## 10. 待完善项

- ID 绑定数据持久化、批号关联生产记录
- 用户密码明文（应哈希）
- 工位设置窗口"破空"按钮业务待确认（下电/保存/加入对列已实现，V1.26）
- 界面 LOG 文本框仍未写文件
