# CHANGELOG

## [2026-08-08] 映射提示弹窗改为非模态悬浮窗（V1.22 提示体验）

### 改动（CommunicationTestForm.cs）
- 原"通道映射提示"用 `UIMessageBox.Show`（模态），会阻塞代码流程并强制抢占焦点。
- 改为新增的 `RemapNoticeForm`（私有嵌套 UIForm）：
  - `.Show()` 非模态弹出，不阻塞遍历/其它代码执行；
  - `CreateParams` 加 `WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`，弹窗**不激活、不抢焦点**，
    不进任务栏/AltTab，用户可保持弹窗打开继续点击/操作其他窗体；
  - `TopMost` 置顶 + 屏幕右上角定位，多次触发只更新同一实例文本（不重复弹窗），
    用户可随时点"知道了"或标题栏"×"关闭；窗体关闭时自动释放。
- 手动点击与一键遍历两处触发均走该非模态提示，日志 `[映射]` 记录不变。

## [2026-08-08] 通讯测试窗体备用通道映射补齐（V1.22 映射完善）

### 问题
- 通讯测试窗体映射写入时，备用目标寄存器 0x2009 是**整寄存器直写**，会覆盖另一测试
  拥有的映射目标位（例如负压阀映射占 0x2009 bit10、载台电映射占 bit11，一边写就把
  另一边的位清掉了）——与主项目 ModbusTcpIoController 的逐通道读-改-写不一致。
- 一键遍历点亮被映射通道时没有任何提示，现场看不出该通道实际输出到了哪个备用通道。

### 改动（CommunicationTestForm.cs）
**1) 备用目标寄存器 0x2009 改为读-改-写（对齐主项目逐通道 RMW）**
- 新增 `ChannelGrid.ComputeRemapTargetMask()`：按"源通道是否属于本网格拥有的物理通道
  （OwnedMask）"累加本网格在 0x2009 拥有的目标位掩码。
- 新增 `WriteBackupRegister(grid, remapValue)`（须在 _modbusLock 内）：读回 0x2009 现值，
  只改写本网格掩码覆盖的位、保留另一测试的位，再写回。
- `WriteRegister`（手动点击）与 `WriteSweepRegisters`（一键遍历）的 0x2009 下发统一改用它。
- 效果：负压阀测试与载台上电测试可同时使用映射，各占 0x2009 的不同位互不干扰。

**2) 一键遍历点亮被映射通道时弹窗告知**
- `SweepStepWorker` 的 UI 回调整理阶段：当前点亮的通道若命中映射表（`IsRemapSource`），
  复用 `ShowRemapNotice` 弹窗告知"旧通道名 → 实际输出寄存器第几路"；按钮仍显示旧通道名
  （RowIoNames 不变），只增加弹窗提示 + 日志 `[映射]` 记录。

### 说明
- 手动点击被映射通道的弹窗（V1.21 已有）不变；本次补齐一键遍历场景的弹窗与 0x2009 并发安全。
- 读取跟随映射（`SetButtonsFromRegisters` 读目标寄存器位）与写入跟随映射（源位剔除、
  信号汇总 0x2009）逻辑不变，现与主项目行为一致。

## [2026-08-08] 通讯测试窗体：自动连接 + 心跳断连提醒 + 遍历实时刷新（V1.22 补强）

### 问题
- 通讯测试窗体打开后还要手动点"连接测试"，不符合生产环境"打开即自动连接"的习惯。
- 测试中途网线/设备掉线没有感知，按钮点击、写入失败才发现，现场排查慢。
- 一键遍历在 UI 线程同步写/读寄存器，设备响应慢时会卡界面；且按钮状态只在本地改，
  看不出实际通断（接线异常时灯照样亮，误判正常）。

### 改动（CommunicationTestForm.cs）
**1) 打开即自动连接 + 后台静默重连（与生产环境一致）**
- 新增 `OnShown`：启动心跳/重连定时器并立即 `AutoConnect()`（后台线程 `TryConnectSilent`）。
- 原 `Connect()` 拆分为：
  - `TryConnectSilent()`：静默连接，不弹窗、只更新状态指示与日志，供自动连接/后台重连复用；
  - `ConnectAsync()`：手动"连接测试"按钮，后台连接完成后弹窗反馈（不再阻塞 UI）。
- `ReconnectTimer_Tick`：未连接时每 3s 后台尝试一次静默重连，成功自动恢复、全程不打扰。

**2) 心跳机制及时发现中途断连**
- `HeartbeatTimer_Tick`：已连接时每 1s 后台读一次 0x2000（`_heartbeatBusy` 防重入）；
  读失败/超时 → `HandleDisconnect()`：置断开、顶部变红、**弹窗提醒一次**（`_disconnectNotified`
  防刷屏，重连成功后复位）、停掉正在运行的遍历，随后由重连定时器自动恢复。

**3) 一键遍历后台线程执行 + 按钮实时反映真实通断（性能关键）**
- 遍历单拍移到后台线程 `SweepStepWorker`（`StartSweepStep` 用 `Task.Run` + `_sweepStepBusy`
  防重入：上一拍没做完就跳过，宁可放慢也不卡界面）：
  1. `ChannelGrid.ComputeSweepRegisters(row, col)` 纯计算"全灭+只亮当前通道"的寄存器值（不碰 UI）；
  2. `WriteSweepRegisters` 加锁整体写回（读-改-写保留共享字节，每寄存器一次，不逐条写日志）；
  3. 写后**读回 10 个寄存器**，切回 UI 线程 `SetButtonsFromRegisters` 刷新圆形灯——
     按钮状态与实际通断一致，接线异常时该路灯不亮、一眼可辨。
- 原 `SweepStep`（同步版）与 `SweepOnly` 删除，由后台版替代。

**4) 多线程安全**
- 新增 `_modbusLock`：连接 / 心跳 / 遍历 / 手动读写全部加锁串行化（NModbus 的 TcpClient
  非线程安全）；跨线程 UI 更新统一走 `RunOnUi`（BeginInvoke，窗体释放时安全忽略）。
- 跨线程标志（`_connected`/`_sweepActive`/`_sweepStepBusy`/`_heartbeatBusy`/`_reconnectBusy`）
  声明为 `volatile` 保证可见性。
- `OnFormClosed` 停止并释放全部三个定时器（遍历/心跳/重连）。

### 说明
- 手动"连接测试"保留弹窗确认；自动连接/后台重连一律静默（只写日志），不打扰操作员。
- 断连弹窗一次只弹一个（重连成功后再次断连才会再弹），避免掉线期间疯狂弹窗。
- 遍历期间读回刷新按钮 = 每 500ms 约 5 写 + 1 读（本地 Modbus TCP，开销极小）。

## [2026-08-08] 通讯测试窗体新增"一键遍历"通断跑马灯（V1.22）

### 问题
- 现场需要快速检查 72 路 DO 输出（负压阀 / 载台上电）每路接线是否通断正常，
  手动一路一路点击 72 个圆形灯按钮太慢，且看不出"通断"连贯性。

### 改动
**1) 底部新增"一键遍历"按钮（CommunicationTestForm.cs / Designer.cs）**
- `btnReadStatus`（读取状态，蓝）右侧新增 `btnSweep`（"一键遍历"，紫色，140×38，x=470）。
- 点击后对**当前页签**的测试做**通断跑马灯**检测：
  - `StartSweep`：按 `tabControl.SelectedIndex` 锁定负压开关测试（_vacuumGrid）或
    载台上电测试（_carrierGrid），按钮变红"停止遍历"，启动 500ms 定时器并立即亮第一路。
  - `SweepTimer_Tick` / `SweepStep`：每 500ms 只点亮一路通道、其余 71 路全部熄灭
    （`ChannelGrid.SweepOnly(row, col)`），72 路循环往复形成跑马灯；每完成一整圈日志提示一次，
    避免刷屏；连接中途断开自动停止。
  - 再次点击（或运行中切换页签）→ `StopSweep`：停定时器、按钮恢复"一键遍历"紫色，
    并把该网格全部通道熄灭写回设备（`SweepAllOff`），输出不悬在半亮状态。
- `ChannelGrid` 新增遍历支撑方法：
  - `SweepOnly(row, col)`：全灭后只点亮目标通道，重算 5 个业务寄存器 + 备用映射 0x2009，
    经 `WriteAllOwnedRegisters` 逐个写回（读-改-写保留共享 0x2004 对方字节，每寄存器只写一次）。
  - `SweepAllOff()`：全部熄灭并写 0。
  - `WriteAllOwnedRegisters()`：未连接时只更新本地状态、不写设备（避免警告刷屏）。
- `OnFormClosed` 关闭窗体时同步停止并释放遍历定时器。

### 说明
- 负压开关测试 / 载台上电测试 两个页签共用这一个按钮，作用于按下时所在的页签；
  运行中切换页签自动停止遍历，防止误控另一测试的输出。
- 遍历未连接时提示"请先点击连接测试建立通讯"，禁止在无连接下启动。
- 其余手动点击按钮 toggle、全部关闭、读取状态、备用映射提示逻辑不变。

## [2026-08-08] 通讯测试窗体 SunnyUI 重构 + 备用通道映射点击提示（V1.21）

### 问题
- 通讯测试窗体（CommunicationTestForm）整体还是原生 WinForms 控件（默认灰色按钮、
  默认页签、默认标题栏、默认文本框），观感与其他 SunnyUI 风格窗体（系统设置等）不一致，较丑。
- 现场工位如果对某个 DQ 通道做了备用通道映射（烧毁后用备用通道），测试时点击该通道
  没有任何提示，容易误以为原通道仍在工作，看不出实际输出到了哪个备用通道。

### 改动
**1) 通讯测试窗体整体改用 SunnyUI 控件重构（CommunicationTestForm.cs / Designer.cs）**
- 窗体基类 `System.Windows.Forms.Form` → `Sunny.UI.UIForm`（蓝色标题栏，ShowTitle）。
- 新增顶部状态条（pnlHeader）：`UILedBulb` 连接指示灯 + `UILabel` 连接状态，
  连接成功→绿灯"已连接"，断开/失败→红灯"未连接"（`SetConnected` 统一更新）。
- 中部页签：`Sunny.UI.UITabControl` + 两个 `UIPage`（负压开关测试 / 载台上电测试），
  页内网格容器用 `Sunny.UI.UIPanel`（白色背景，替换原生 Panel）。
- 底部按钮：`Sunny.UI.UIButton`（连接测试=绿 / 全部关闭=橙 / 读取状态=蓝 / 关闭窗口=灰）；
  日志框用 `Sunny.UI.UITextBox`（只读多行，ScrollToCaret 自动滚动 + 最多保留 200 行）。
- 弹窗统一改用 `Sunny.UI.UIMessageBox`（错误=红 / 警告=橙 / 提示=绿 / 通道映射=蓝）。
- 逻辑（9×8 圆形灯网格、寄存器读写、读-改-写、备用映射、日志）全部保留不变。

**2) 备用通道映射点击提示（V1.21，承接 V1.20 的备用映射接入）**
- `ChannelGrid.OnCircleClick`：点击按钮时先用 `IsRemapSource` 判断该通道是否命中映射表，
  命中则先弹窗再 toggle。
- 新增 `ShowRemapNotice`：弹窗说明"通道 X（寄存器 0xXXXX 第 N 通道）已做备用通道映射，
  实际输出通道：寄存器 0xXXXX 第 N 通道（第 N 路）"，并在日志追加 `[映射]` 记录。
- 仅当 `IoBackupChannelMappingEnabled=true` 且该通道在映射表中时触发，未启用映射的工位行为完全不变。

### 说明
- 圆形灯按钮（CircleButton）仍为自绘控件（ON=亮绿+金边，OFF=深灰），不依赖 SunnyUI 样式，
  保持"指示灯"观感与点击交互不变。
- 通讯测试入口不变：主菜单"关于"下拉 →"通讯测试"（技术员及以上权限）。
- 窗体打开方式改为**非模态**（MainForm 用 `Show` 替代 `ShowDialog`）：打开测试窗体的同时
  仍可点击操作主窗体及其它窗体，测试窗体关闭时自动 Dispose 释放资源。
- 窗体高度加大（ClientSize 780×1000 + MinimumSize 780×1000）：9 排×8 列通道按钮一屏显示完整，
  页面不再出现滚动条。

## [2026-08-08] 工位面板高度减小 + 主菜单"帮助"更名"关于"（V1.19.12）

### 问题
- 工位面板（WorkstationPanelView）高度 225px，内容最低点（"延时到达"输入框、设置按钮）在
  y≈189~195，底部约 30px 空白过大。
- 主菜单第 4 个按钮叫"帮助"，但其下拉里只有"设置 / 关于"，且已有"关于"字样在菜单项里，
  按钮语义与用户习惯不符：按钮应叫"关于"，菜单项才是"版本说明"。

### 改动
**1) 工位面板高度减小（WorkstationPanelView.Designer.cs / MainForm.cs）**
- 面板 Size 由 240×225 改为 240×205（底部空白由约 30px 减为约 10px）。
- 网格行高 PanelRowHeight 由 245 同步改为 225（保持"面板高 + 上下边距 20px"关系）。

**2) 主菜单"帮助"按钮更名"关于"（MainForm.Designer.cs / MainForm.cs）**
- 控件 `btnHelp` → `btnAbout`（字段声明 / 创建 / Controls.Add / 事件全部更名），
  按钮文字 "帮助" → "关于"。
- 点击弹出下拉菜单项更名：**"关于" → "版本说明"**（V1.19.12）。
- 处理函数更名：`btnHelp_Click` → `btnAbout_Click`、`MenuHelpAbout_Click` → `MenuHelpVersionInfo_Click`。
- 下拉菜单项为运行时动态创建的 Button（无持久控件名），只需更名事件处理函数。

### 说明
- 下拉菜单结构不变：设置（仅管理员可见）/ 版本说明（所有权限可见）。

## [2026-08-08] 工位 SN/配方/延时关联 + 日志记录按钮更名（V1.19.11）

### 问题
- 真实气压表（ModbusRtuBarometerReader）只上报压力，BarometerData 的
  SN / 配方 / 延时开启 / 延时到达 字段在采集层恒为空，工位面板上永远显示不了
  现场绑定的 SN，也无法关联配方与延时。
- ID 绑定（扫码枪扫码或手动输入）此前只导出 Excel，绑定结果没有回写到
  工位数据，面板 SN 与绑定脱节。
- 主菜单"LOG记录"按钮文案与界面中文风格不一致。

### 改动
**1) 新增工位静态信息存储并叠加到采集数据（DeviceManager.cs / StationInfo.cs）**
- 新增模型 `StationInfo`（Models/StationInfo.cs）：DeviceId / SerialNumber /
  RecipeName / DelayStartTime / DelayArriveTime。
- DeviceManager 新增 `_stationInfo` 字典（按工位编号存储）+ `_stationInfoLock`。
- 新增方法：`GetStationInfo` / `SetStationSerialNumber` / `SetStationRecipeName` /
  `SetStationDelayTimes` / `SetStationSerialNumbers`（批量）/ `ApplyStationInfo`（叠加）。
- `CollectData` 在 IO 回填后、报警判定前调用 `ApplyStationInfo`，把工位静态信息
  覆盖到采集数据上（仅覆盖已配置字段），使面板 SN / 配方 / 延时显示与绑定/设置一致。

**2) 工位设置窗口"保存"实现（StationSettingsForm.cs）**
- 保存按钮原来只是 TODO 空实现；现在把 SN / 配方 / 延时开启 / 延时到达 写入
  DeviceManager 工位静态信息，保存后工位面板同步更新。
- 延时格式 时:分:秒 校验（非法格式提示，不保存）；空白视为清空。
- 回显补充"启动时间"（延时到达）字段。
- 极限温度（txtTemp）暂不处理：BarometerData / 工位面板无对应字段，留待配方表接入。

**3) ID 绑定把 SN 关联到工位（IdBindingForm.cs / InputLotForm.cs / MainForm.cs）**
- IdBindingForm 构造函数新增可选 `DeviceManager` 参数；保存时遍历绑定列表，
  调用 `SetStationSerialNumbers` 把"工位 → SN"写入设备管理器。
- 纯手动输入（未启用扫码枪）同样生效：工位编号 + SN 录入列表即可关联。
- InputLotForm / MainForm 逐级传递 `_deviceManager`。

**4) 主菜单按钮更名（MainForm.Designer.cs）**
- btnLog 文本 "LOG记录" → "日志记录"。

### 说明
- SN / 配方 / 延时为"工位配置"类静态信息，不随设备采集变化；写入后由采集线程
  每次叠加到该工位数据上，因此所有显示 SN/配方/延时 的地方（工位面板、工位设置窗口）
  都会与绑定/设置结果保持一致。
- Mock 读取器生成的模拟值仅在工位静态信息未配置时保留原值，已配置的工位以
  静态信息为准。

## [2026-08-08] 工位面板真空开启显示文字化 + 压力框加宽（V1.19.10）

### 问题
- 真空开启灯原来是"纯色无文字"（绿=开启，灰=关闭），操作员需对照颜色判断，观感不直观。
- 真空压力值框偏窄（58px），负压值位数较多（如 -100.0 kPa）时可能显示不全。

### 改动
**1) 真空开启显示改为"文字 + 颜色"（WorkstationPanelView.cs / WorkstationPanelView.Designer.cs）**
- 真空开启（真空电磁阀输出 ON）：绿底白字"真空开"。
- 真空关闭（真空电磁阀输出 OFF）：红底白字"真空关"（关闭用红色，与工作状态"故障"红色呼应，更醒目）。
- 新增 `UpdateVacuumOpenDisplay(vacuumOpen)` 方法；`UpdateStatusLight` 现在只用于上电灯 boxPower。
- ToolTip 说明同步更新。

**2) 真空压力框加宽、状态框微缩（WorkstationPanelView.Designer.cs）**
- txtPressure 宽度 58 → 78（保证 -100.0 kPa 等数值完整显示，比右侧两个状态框都宽）。
- boxVacuumOpen 宽度 55 → 48（配合"真空开/关"两字文本）。
- boxWorkState 宽度 60 → 46（配合"空闲/选中/繁忙/故障"两字文本）。

### 说明
- 布局仍在同一行（y=67，高 21），三个框位置依次右移，不改变面板整体尺寸（240×225）。
- README 同步更新 ASCII 布局图、状态约定与关键方法表。

## [2026-08-08] 报警阈值与界面单位统一为 kPa，故障显示逻辑修复（V1.19.9）

### 问题
- 真空压力/报警阈值单位不一致：真实读取器按 kPa 返回压力（寄存器 -950 / 小数位 1 → -95.0，
  与界面输入的 -95 同单位），但软件报警阈值配置却是 Pa（-95000），
  导致压力判定 `-95.0 > -95000` 恒成立，生产环境所有台都误报"故障"。
- 非测试中的台（阀关着，压力为常压 ~0kPa）：读取器按"压力越限"误标 Fault，
  DeviceManager 未覆盖，面板错误显示"故障"。

### 改动
**1) 软件报警阈值改为 kPa 并跟随"公共参数窗口"输入（DeviceManager.cs / CommonParameterForm.cs）**
- `DeviceConfig.AlarmPressureThresholdPa`（默认 -95000）→ `AlarmPressureThresholdKPa`（默认 -95，单位 kPa）。
- App.config 配置项同步更名 `AlarmPressureThresholdKPa`，MainForm / SettingsForm 同步更新。
- 新增 `DeviceManager.UpdateAlarmPressureThresholdKPa(...)`：公共参数窗口保存负压值时
  同步更新软件报警阈值，使软件报警判定与界面输入（kPa）一致。

**2) 非测试台故障误判修复（DeviceManager.cs）**
- `CollectData` 状态赋值补 `else` 分支：非测试且未报警的台强制置为 Idle，
  覆盖读取器对常压（~0kPa）的"压力越限"误判。

**3) 界面单位统一为 kPa（相关界面补充单位）**
- WorkstationPanelView：真空压力显示 `Pa` → `kPa`。
- DeviceManualForm：当前压力显示 `Pa` → `kPa`。
- CommonParameterForm：标签"负压值设定：" → "负压值设定(kPa)："。
- SettingsForm：报警压力阈值说明改为"kPa，如 -95"。
- MockBarometerReader：模拟压力值由 Pa 改为 kPa（良好 -96~-100 / 较差 -1~-90），与真实读取器一致。
- 日志（真空已建立 / 真空压力越限）与各处注释单位同步为 kPa。

### 说明
- 读取器采集层的基础报警判断仍保留（用于 UI 先提示），最终状态以 DeviceManager 业务逻辑为准。
## [2026-08-08] 用户管理窗体：删除操作提示 + 当前角色中文显示（V1.19.8）

### 改动
**1) 删除 lblTip 并缩小窗体（UserManagementForm.Designer.cs）**
- 移除底部灰色提示标签"提示：留空的字段不修改；新密码和确认密码必须一致。"。
- 窗体 ClientSize 由 400x365 缩小为 **400x330**（最低控件为按钮，底部余量约 20px）。

**2) 当前用户名 → 当前角色（中文显示 + 着色）**
- 左侧标签 "当前用户名:" 改为 "当前角色:"（值已不再显示用户名）。
- `lblCurrentUsernameValue` 改为显示角色中文名并按角色着色（新增 `UpdateRoleDisplay`）：
  - 技术员 → 蓝色（Blue）
  - 操作员 → 绿色（Green）
  - 管理员 → 红色（Red，防御性分支，正常不可选）
- 原"显示该角色用户名"逻辑移除；修改成功后刷新处同步改为 `UpdateRoleDisplay(targetRole)`，
  不再覆盖为用户名。
- 默认文本改为 "操作员"（绿色）。

### 说明
- 角色下拉框选项仍为"操作员/技术员"（与 MainForm 保持一致），仅"当前角色"值按需求显示
  "操作员"。

## [2026-08-08] 权限显示角色名按角色着色（V1.19.7）

### 改动
**1) 单一权限标签 → "前缀 + 角色名"两个标签（MainForm.Designer.cs）**
- 拆为 `panelPermission`（FlowLayoutPanel，占原权限单元格）+ `lblPermissionPrefix`（固定
  前缀"当前操作权限: "）+ `lblPermissionRole`（角色名）。FlowLayoutPanel 水平排列、无边框、
  WrapContents=false、背景色与顶栏一致，观感与原来单个标签相同。
- 字段/创建行/Controls.Add/布局块同步改名。

**2) 角色名着色逻辑（MainForm.cs）**
- 新增 `UpdatePermissionDisplay(roleName)`：直接设置 `lblPermissionRole.Text` 与
  `ForeColor`，无需 RTF，颜色可靠生效——
  - 管理员 → 红色（Red）
  - 技术员 → 天蓝色（SkyBlue）
  - 操作员 → 绿色（Green）
  - 未知角色 → 默认文字色
- 前缀标签始终默认黑字；启动时初始化（MainForm 构造函数）与登录成功切换权限后
  （TryLoginAndSwitchPermission）两处调用点均改为 `UpdatePermissionDisplay(...)`。

### 说明
- 曾尝试用 RichTextBox + RTF（cf1/cf2 颜色表）实现局部着色，但缺 fonttbl 时颜色表未被
  严格解析器接受，颜色不生效，故改为拆两个 Label 的最简可靠方案。

### 使用说明
- 顶部"当前操作权限: xxx"中，角色名按权限着色，一眼可辨当前身份：
  红色=管理员（最高权限）、天蓝色=技术员、绿色=操作员（默认）。

## [2026-08-08] 选中框显示时单击改"切换"选中状态（V1.19.6）

### 问题
- V1.19.5 中"单击空白区域/选中框 = 取消选中"，只能在空白处长按选中、单击取消，
  已有选中时想"点一下取消、再点一下选中"来回切换需要反复长按，操作不够顺手。

### 改动
**1) 单击改为"切换"选中状态（WorkstationPanelView.cs）**
- `Panel_MouseUp`（空白区域单击）：V1.19.5 的"单击取消"改为——仅当选中框显示
  （`_selectionBoxVisible`，即已有任一工位被选中）时执行 `IsSelected = !IsSelected` 切换；
  选中框隐藏（全表未选中）时单击不动作，避免绕过"长按选中"直接点选。
- `btnSelect_Click`（点击选中框）：由"单击取消"改为 `IsSelected = !IsSelected` 切换
  （选中框只在有选中时可见，天然满足"显示时才切换"）。
- 首次/新增选中仍为空白区域"长按约 0.8 秒"。

**2) "唯一选中项被取消→全表隐藏"例外（MainForm.cs 已覆盖）**
- 整表只有该工位处于选中状态时，把它切换为未选中 → `IsSelectedChanged` 触发主窗体
  `UpdateSelectionBoxVisibility`：此时 `anySelected=false`，所有面板选中框全部隐藏。
- 无需额外逻辑：现有"有任一选中→全部显示，全未选中→全部隐藏"规则天然覆盖该例外。

### 使用说明
- 空白处长按约 0.8 秒 → 选中（框变绿✓）；
- 已有选中时：单击空白处或点击绿勾/空框 → 选中状态来回切换；
- 整表只剩一台被选中时再把它点掉 → 全部选中框隐藏，界面回归简洁。

## [2026-08-08] 选中交互改"长按选中 + 有选中才显示绿✓框"（V1.19.5）

### 问题
- 右上角选中框（btnSelect）常驻每个面板，未选中时是空心白框，看起来像摆设、不美观；
- 点击切换选中的方式容易误触，且选中框样式（浅蓝底绿勾）与整体配色不搭。

### 改动
**1) 选中交互改为"长按选中 / 单击取消"（WorkstationPanelView.cs）**
- 移除 V1.18 的"点击切换选中"（WirePanelClickToSelect / AttachSelectionClick / Panel_ClickToSelect）。
- 新增 `WirePanelLongPressSelect()`：给面板及所有非按钮子控件挂接 MouseDown/MouseUp/MouseMove/MouseLeave。
  - **长按约 0.8 秒**（LongPressMilliseconds，按住不松手）→ 选中该工位（LongPressTimer_Tick）；
  - **单击**（松手时间未达长按时长）→ 取消选中（Panel_MouseUp）；
  - 长按期间鼠标移动超阈值（LongPressMoveThreshold=8px）视为拖动，取消计时；
  - 鼠标离开控件（未触发长按）取消计时；已触发长按后松手/离开不再误取消。
- `btnSelect_Click`：由"切换选中"改为"单击取消选中"。

**2) 选中框"平时全隐藏，有选中才显示"（WorkstationPanelView.cs / .Designer.cs / MainForm.cs）**
- Designer：`btnSelect.Visible = false`（默认隐藏）。
- 新增 `SetSelectionBoxVisible(bool)`：由主窗体统一协调显示/隐藏。
- 新增 `UpdateSelectionBoxVisibility()`（MainForm）：遍历所有面板，只要有任一工位被选中
  → 所有面板都显示选中框；全部未选中 → 全部隐藏。在面板 `IsSelectedChanged` 时与
  `UpdateRowSelectButton` 一同触发；创建完所有面板后初始调用一次。
- 效果：长按选中任一台 / 行全选选中整行 → 该行项显示"绿底白✓"，其余所有项显示"空心白框"。

**3) 选中框样式更新（WorkstationPanelView.cs）**
- 选中：由"浅蓝底 + 绿色✓"改为 **绿底（ForestGreen）+ 白色✓**（与整体绿色系协调，白勾对比度更好）；
- 未选中：空心方框（黑框白底，无文字）保持不变；
- ToolTip 文案同步更新为长按选中说明。

### 使用说明
- 操作员在面板空白处按住约 0.8 秒即选中该台（框变绿勾）；单击空白处或点击绿勾框即取消。
- 只要还有一台被选中，全部面板都会显示各自的框（选中的绿、其余白框），便于看清哪些被选中；
  全部取消后框全部消失，界面回归简洁。

## [2026-08-08] 行全选按钮改浅灰 + 工作状态配色统一为"信号灯"色系（V1.19.4）

### 改动
**1) 行全选按钮背景色改浅灰（MainForm.cs）**
- 每行最右侧"全选/取消"按钮背景色由深灰（Gray）改为**浅灰（LightGray）**，
  与工位面板上电状态灯（boxPower）关闭时的灰色一致，观感更轻、不再笨重。
- 因浅灰底 + 白字对比度不足，按钮文字色同步改为**黑色**。

**2) 工作状态显示（boxWorkState）配色统一为"信号灯"色系（WorkstationPanelView.cs / .Designer.cs）**
- 空闲：浅灰底（LightGray）+ 黑字 —— 中性，与 boxPower 关闭灰色一致。
- 选中（空闲但已上电，待测试）：橙底（Orange）+ 白字 —— 暖色提醒。
- 繁忙（测试中）：绿底（LimeGreen）+ 白字 —— 与系统绿色按钮/状态灯一致。
- 故障：**红底（Red）+ 白字** —— 由原"浅粉底+红字"改为醒目红底，故障最突出
  （面板整体背景仍保持浅粉打底）。
- 为 boxWorkState 增加 ToolTip，悬停时说明四种配色含义，方便操作员理解。

### 说明
- 配色沿用现有惯例：绿=运行/开启、橙=就绪提醒、红=故障报警、浅灰=空闲，一眼可辨。
- 工作状态文字规则不变（V1.18 中文：空闲/选中/繁忙/故障；V1.19.2 起不受选中状态影响）。

## [2026-08-08] 面板布局微调：上电标题 + SN/配方改 Label（V1.19.3）

### 改动
**1) 上电状态灯前加"上电"标题并右移对齐（WorkstationPanelView.Designer.cs）**
- 新增标签 `lblPower`（"上电"），位置 (3, 38)，与下方各标题（真空压力/SN/配方/延时...）**左对齐**（x=3）。
- 上电状态灯 `boxPower` 位置由 (6, 26) 右移到 **(57, 26)**，与各内容显示的**左侧对齐**（x=57），
  大小不变（55×36）。布局与其它行一致：标题在左、内容在右。

**2) SN / 配方内容显示改为 Label（WorkstationPanelView.Designer.cs / .cs）**
- `txtSN` / `txtRecipe`（只读 TextBox）改为标签 `lblSNValue` / `lblRecipeValue`：
  白底 + FixedSingle 边框 + 固定 140×21 + 左对齐，观感与只读文本框一致，
  但不参与焦点/光标（纯展示控件）。
- `UpdateData` 中赋值处同步改名（`lblSNValue.Text` / `lblRecipeValue.Text`）。

### 说明
- 两个文件均保持 UTF-8 BOM；控件字段改名的所有引用点（Designer 布局 / .cs 赋值）已同步更新。
- 标签同样参与"点击面板选中"（非按钮控件，WirePanelClickToSelect 会挂接）。

## [2026-08-08] 选中状态仅由右上角选中指示体现 + 行全选按钮改灰色（V1.19.2）

### 问题
- 现场指出：工位面板选中/取消选中时整个面板变蓝（浅蓝高亮 + 工作状态文字变"选中"）
  过于抢眼，且与"繁忙（绿）/故障（粉）"等状态色叠加后难以辨识。
- 选中状态只需通过右上角选中指示（btnSelect ✓ / 空心方框）体现即可。

### 改动
**1) 移除面板整体选中高亮（WorkstationPanelView.cs）**
- `UpdateStatusColor`：面板背景色只反映设备状态（空闲/测试中/故障），不再叠加浅蓝高亮。
- `UpdateWorkState`：工作状态不再受 IsSelected 影响——去掉"空闲但已选中→选中"规则，
  只保留"空闲但已上电→选中"；选中某台不再改变其工作状态文字。
- `UpdateSelectionStyle`：只刷新右上角选中指示（btnSelect），不再改背景色/工作状态文字。
- `_selectedColor` 仅用于选中指示按钮打底（不再用于面板整体背景）。

**2) 行全选按钮改为灰色（MainForm.cs）**
- 每行最右侧"全选/取消"按钮背景色由道奇蓝（DodgerBlue）改为灰色（Gray，白字）。

### 使用说明
- 工位面板是否选中：只看右上角选中指示（✓=选中，□=未选中）；
- 面板背景色/工作状态文字始终反映设备真实状态，不随选中状态变化；
- 每行最右侧按钮为灰色"全选/取消"。

## [2026-08-08] 行全选按钮文字改为实时反映该行选中状态（V1.19.1）

### 问题
- 原"全选/取消"按钮文字只在点击按钮时按"本次点击取反"切换：
  若整行全部选中（按钮显示"取消"）后，操作员**单独取消**该行某一台，
  按钮仍停留在"取消"，与"该行并非全部选中"的实际状态不符。

### 改动
**1) 按钮文字改为实时计算（MainForm.cs `UpdateRowSelectButton`）**
- 新增 `UpdateRowSelectButton(rowIndex)`：遍历该行所有面板，**全部选中**才显示"取消"；
  只要有一台未选中（含被单独取消）立即恢复显示"全选"。
- 每行"全选/取消"按钮引用保存到 `_rowSelectButtons` 字典（行索引 → 按钮）。

**2) 面板选中状态变化即时刷新（WorkstationPanelView.cs / MainForm.cs）**
- WorkstationPanelView 新增 `IsSelectedChanged` 事件，在 `IsSelected` 值**实际变化**时触发
  （点击面板本身 / 点击右上角选中指示 / 行全选按钮均会触发）。
- MainForm 为每个面板订阅该事件，按面板所在行调用 `UpdateRowSelectButton` 刷新按钮文字。
- `BtnSelectRow_Click` 末尾调用 `UpdateRowSelectButton` 兜底刷新。

### 使用说明
- 整行全部选中 → 行尾按钮显示"取消"（点击整行取消选中）；
- 单独取消该行任意一台 → 按钮**立即**变回"全选"（点击整行重新全部选中）。

## [2026-08-08] 工位面板"上电/下电"按钮改为"选中指示"（V1.19）

### 问题
- 现场指出工位面板的 btnPower 不是"上电/下电"控制按钮，而是第一个标识——
  用于标识当前工位是否被选中（供批量操作使用）。

### 改动
**1) btnPower → btnSelect（选中指示，右上角）**
- 位置移到面板**右上角**（NO.x 右侧），样式改为复选框：
  - 选中：浅蓝底 + 绿色"✓"；未选中：空心方框（黑框白底，无文字）。
- 点击按钮本身即可切换选中状态（与点击面板本身选中一致）。
- 移除原来的"上电/下电"文字与"测试中/故障时禁用"逻辑。

**2) 清理上电控制死代码**
- 移除 WorkstationPanelView 的 `OnPowerToggled` 事件。
- 移除 MainForm 的 `Panel_OnPowerToggled` 处理器及订阅
  （载台上电由"启动运行 / 停止运行"批量流程及工位设置窗口"下电"按钮控制）。

### 使用说明
- 每个工位面板右上角显示"选中指示"：✓=已选中（浅蓝底），□=未选中（空心方框）。
- 点击指示或点击面板本身均可选中/取消；行"全选"按钮仍可整行选中。

## [2026-08-08] 工位设置窗口细节调整（V1.18.1）：按钮文本切换 / 点击面板选中 / 中文状态

### 问题
- 行"全选"按钮点击选中后，按钮文本仍显示"全选"，操作员看不出当前已全部选中、可再点取消。
- 现场希望直接**点击工位面板本身**即可选中该工位，而不只依赖每行的"全选"按钮。
- 工位设置窗口"状态"标题带括号说明（状态(IDLE/SELECT/BUSY)），现场要求标题只要"状态"两字。
- IDLE/SELECT/BUSY 为英文状态，现场要求统一为中文：空闲 / 选中 / 繁忙。

### 改动
**1) 行全选按钮文本随选中状态切换（MainForm.cs `BtnSelectRow_Click`）**
- 点击选中该行全部面板后，按钮文本由 **"全选"** 变为 **"取消"**（提示可再点取消全选）；
  再次点击取消后恢复显示 **"全选"**。

**2) 点击工位面板本身即可选中（WorkstationPanelView.cs）**
- 新增 `WirePanelClickToSelect()`：给面板及其所有**非按钮**子控件挂接"点击切换选中"事件
  （WinForms 子控件点击不冒泡到父控件，需逐个挂接）。
- 点击面板任意空白/文字区域即切换 `IsSelected`，工作状态同步变为 **选中**（再点取消恢复空闲）。
- 按钮（上电/设置）已有专属功能，跳过不参与选中切换。

**3) 工位设置窗口"状态"标题精简（StationSettingsForm.Designer.cs）**
- 标签文字由 "状态(IDLE/SELECT/BUSY):" 改为 **"状态:"**，只要"状态"两字。

**4) 状态文字由英文改为中文（WorkstationPanelView.cs / StationSettingsForm.cs）**
- 工位面板工作状态：IDLE → **空闲**、SELECT → **选中**、BUSY → **繁忙**、FAULT → **故障**。
- 工位设置窗口状态回显同步改为中文（空闲 / 选中 / 繁忙 / 故障）。

### 使用说明
- 主界面工位区域每一行最右侧点 **全选** → 该行所有面板被选中（背景浅蓝 + 工作状态"选中"），
  按钮文本变为 **取消**；再点取消则全部恢复"空闲"，按钮文本回到 **全选**。
- 直接**点击某个工位面板**（空白或文字区域）也可选中该工位（状态变"选中"），再点一次取消。
- 工位设置窗口标题显示为"状态:"，值为中文（空闲 / 选中 / 繁忙 / 故障）。

## [2026-08-07] 帮助菜单（设置 / 关于）+ 系统设置窗口（按分类展示，仅管理员）（V1.17）

### 问题
- App.config 配置项多达 50 个（设备数量 / 串口 / Modbus 地址 / 送风机 / 扫码枪 / 老化业务参数等），
  现场人员调整参数只能手动编辑 XML，不直观、容易写错格式或值。
- 系统参数属于高权限操作，不能让操作员/技术员随意改动，必须限制为管理员可用。

### 改动
**1) 主窗体"关于"按钮更名"帮助"（MainForm.Designer.cs）**
- 控件 `btnAbout` → `btnHelp`（字段声明 / 布局 / 事件全部更名），按钮文字 "关于" → "帮助"。

**2) 帮助下拉菜单改为两个条目 + 权限控制（MainForm.cs）**
- `btnHelp_Click` 弹出下拉菜单：**设置**（新增）/ **关于**（原"版本说明"逻辑，处理函数更名 `MenuHelpAbout_Click`）。
- **权限控制**：`btnHelp_Click` 里先判断 `_userManager.HasPermission(UserRole.Administrator)`，
  只有管理员才把"设置"加进菜单，**非管理员该选项直接隐藏**（只显示"关于"）。
- 新增 `MenuHelpSettings_Click`：弹出系统设置窗口；入口处再做一次管理员兜底校验
  （防止权限刚降级、窗口仍被打开），非管理员弹提示并返回。

**3) 新增系统设置窗口（Dialogs/SettingsForm.cs + SettingsForm.Designer.cs）——单页分类展示（不使用选项卡）**
- **单页纵向布局**：不用选项卡切换，所有分类在一个可滚动面板（pnlScroll，AutoScroll）里
  从上到下依次排列，一眼看全；页面超高时自动出现滚动条。
- 每个分类用一条**标题分隔线**（SunnyUI `UILine`，蓝色文字 + 水平线）隔开，其下紧跟
  该分类的配置表格（SunnyUI `UIDataGridView`，蓝色主题、斑马纹）。
- 分类与 key 顺序集中在 `_categories` 维护；`SetupSections()` 动态创建
  "标题条 + 表格"，`LayoutSections()` 按 Y 坐标纵向排布、按行数自动计算表格高度，
  并设置滚动内容总高。新增配置项只需在 `_descriptions` 加说明 + 在 `_categories`
  对应分类加 key，无需改界面布局。
- 每个表格三列：
  - 设置名称（配置项 key，只读）
  - 说明（每个配置项的中文含义，只读）
  - 设置值（可直接编辑输入）
- 顶部提示条、底部按钮栏也用 SunnyUI 控件（`UIPanel` / `UIButton`），
  与主程序风格一致、观感更佳。
- 值来源为运行时的 `ConfigurationManager.AppSettings`（与程序启动加载取值一致），
  配置缺失时用内存中 `DeviceConfig` 的属性值兜底。
- **保存前按类型校验**：整数（TotalBarometers/BaudRate/超时等）、字节（IoUnitId/FanUnitId）、
  十六进制寄存器地址（IoInputRegisterStartAddress/IoOutputRegisterStartAddress/
  BarometerPressureRegisterAddress，支持 `0x1000` 与十进制两种写法）、小数（压力阈值/缩放系数）、
  布尔（Mock/取反/开关类）——不合法项整批拦截并列出，避免写坏配置文件。
- **保存写回 exe.config**：`ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None)`
  改写 appSettings 对应键值并 `Save(ConfigurationSaveMode.Modified)`，随后
  `RefreshSection("appSettings")`；提示"设置已保存，重启程序后生效"。
- 窗体顶部提示条提示"以下为 App.config 中的全部配置项（按业务分类排列）……
  保存后重启生效"，底部 [保存设置] / [关闭] 按钮（SunnyUI UIButton）。

**4) 工程文件（BarometerWinform.csproj）**
- 注册 `Dialogs/SettingsForm.cs`（SubType=Form）与 `Dialogs/SettingsForm.Designer.cs`（DependentUpon）。
- 新文件统一保存为 UTF-8 with BOM（满足 VS 设计器编码要求）。

### 使用说明
- 权限：只有**管理员**登录后，点 **帮助** 才会看到"设置"项；操作员/技术员只看到"关于"。
- 主界面点 **帮助 → 设置**：在单页里按分类标题条找到对应配置（可直接滚动画面向下找），
  → 点【保存设置】→ 提示"重启程序后生效"。改错类型（如布尔项填了非 true/false）会整批列出不合法项，不会写坏配置。
- 主界面点 **帮助 → 关于**：弹出版本信息（与原"版本说明"一致）。

## [2026-08-07] 扫码枪断连检测再补强：多消息类型 + 周期"关句柄重搜"兜底（V1.16.6）

**问题**
- V1.16.4/5 只监听 `DBT_DEVICEREMOVECOMPLETE`，但拔掉"正被应用占用"的 USB 串口时，
  很多驱动【只发 `DBT_DEVICEREMOVEPENDING`、不发 `REMOVECOMPLETE`】（移除一直挂起，
  直到句柄关闭才完成）→ 消息收不到，断连识别不到。
- 且 WMI/系统串口列表在应用还握着打开句柄时会被驱动残留骗过（鬼设备），
  心跳两路信号都误判"设备还在"。

**改动（ScannerService.cs）**
- **设备消息覆盖更多类型**：`DBT_DEVICEREMOVEPENDING`(0x8003) 与 `REMOVECOMPLETE`(0x8004)
  都当断连处理；`DBT_DEVNODES_CHANGED`(0x0007)（任何设备树变化都触发，最泛最可靠）
  收到就延迟刷新一次连接状态（已连则检查、未连则试连）。
- **新增周期"关闭-重搜-重开"兜底探测**（每 4 次心跳 ≈ 12 秒）：
  `Disconnect()` 关掉句柄 → 释放鬼设备残留 → `TryConnect()` 重新按关键词搜索。
  设备真没了 → 保持未连接；还在（或拔的是别的设备）→ 重开新句柄，用户无感。
  正在收数据（BytesToRead>0）时自动延后，避免把条码读丢。
- 设备消息里仍只做不碰 COM 的 `Disconnect`，重连（WMI 查询）一律 `PostDeferred`
  推迟到消息处理完后执行（V1.16.5 的 DisconnectedContext 修复保持不变）。

**效果**：拔掉扫码枪 → 设备消息到达时立即断连；消息没到 → 最迟约 12 秒内
由周期探测确认断连（状态变红），插回后自动恢复。

## [2026-08-07] 修复设备插拔消息里直接发 WMI 查询报 DisconnectedContext（V1.16.5）

**问题**
- V1.16.4 监听 `WM_DEVICECHANGE` 后，插入扫码枪时 `FindMatchingPorts()` 报
  托管调试助手 `DisconnectedContext`：`RPC_E_CANTCALLOUT_ININPUTSYNCCALL`
  （应用程序正在发送输入同步呼叫，不能执行传出呼叫）。
- 根因：`WM_DEVICECHANGE` 消息处理期间系统正处于输入同步呼叫中，
  此时发起任何传出 COM 调用（WMI 查询）被禁止。实机运行时该异常被
  `FindMatchingPorts` 的 catch 吞掉返回 null，表现为"插入扫码枪后连不上"。

**改动（ScannerService.cs）**
- `HandleDeviceChangeMessage` 里：`Disconnect()`（只关句柄，不碰 COM）直接执行；
  `TryConnect()`（内部要发 WMI 查询）改用新增的 `PostDeferred` 推迟到
  消息队列末尾、等 `WM_DEVICECHANGE` 处理完脱离该上下文后再执行（仍留在 UI 线程）。
- 若多个插拔消息连续到达，推迟的重连因 `TryConnect()` 开头 `if (_isConnected) return`
  短路，不会重复打开串口。

**效果**：插入/拔出扫码枪不再报错；插入后立即自动连接，拔出后立即变"未连接"。

## [2026-08-07] 扫码枪断连检测补强：监听系统 USB 插拔消息（V1.16.4）

**问题**
- V1.16.3 心跳改为"WMI 重新搜索 + 系统串口列表"两路判定，但现场拔掉扫码枪仍可能识别不到。
- 根因：心跳依赖"拔掉后 PnP 节点 / 注册表条目消失"，可应用还握着串口**打开句柄**时，
  部分 USB 转串口驱动（CH340/PL2303/CP210x）会让节点和 `SERIALCOMM` 条目残留，
  两路信号都误判"设备还在"，永远判不出断连。

**改动（ScannerService.cs）**
- **新增系统设备消息监听**：用 `NativeWindow` 建一个隐藏顶层窗口接收 `WM_DEVICECHANGE`。
  扫码枪被物理拔出时 Windows 会向所有顶层窗口广播 `DBT_DEVICEREMOVECOMPLETE`——
  这是操作系统层面的权威通知，不依赖驱动是否清理残留。
  - 收到"设备移除完成"→ 立即 `Disconnect()`（关闭句柄，残留才真正清除）
    再 `TryConnect()` 重新按关键词搜索：扫码枪真没了 → 保持未连接；
    是别的设备被拔 → 自动重连成功，用户无感。不用再等心跳 3 秒。
  - 收到"设备插入"（`DBT_DEVICEARRIVAL`）→ 未连接时立即试连一次。
  - 窗口在 UI 线程创建，消息处理同线程，无跨线程问题；创建失败不致命，心跳轮询仍兜底。
- **修正 COM 口提取**：`FindMatchingPorts()` 原来用字符串 `Contains` 比对端口名，
  "COM10" 会被误匹配出 "COM1"；改为正则 `(COM\d{1,4})(?!\d)` 精确提取。
- V1.16.3 的 WMI 重新搜索心跳保留，作为 WM_DEVICECHANGE 收不到时的兜底。

**效果**：拔掉扫码枪 → 立即（不再等 3 秒心跳）状态变"未连接"（红）并提示一次；
插回 → 立即自动恢复。

## [2026-08-07] 扫码枪心跳改为"动态搜索串口名"判定断连 + 送风机温度显示控件改 Label（V1.16.3）

### 一、扫码枪断连检测重写（动态搜索确认设备是否真在）

**问题**
- 扫码枪拔掉后底部状态栏仍显示绿色"已连接"，且插回去也不自动恢复。
- 根因：原心跳 `CheckConnectionAlive()` 的两种判定在扫码枪上都不可靠——
  ① `GetPortNames()` 读注册表 `SERIALCOMM`，应用还握着打开句柄时拔掉 USB，
  COM 条目常残留，误判"端口还在"；② `ReadExisting()` 探测期望失效句柄抛异常，
  但多数 USB 转串口驱动（CH340/PL2303/CP210x）被拔后只是静默返回空串。
  结果 `_isConnected` 永远为 true，`TryConnect()` 开头短路，无法重连恢复。

**改动（ScannerService.cs）**
- 连接建立本就是按设备关键词（默认 `Xenon 1902`）用 WMI 动态搜索串口；
  心跳也改成**重新跑一遍同一套动态搜索**：设备被拔掉后 PnP 设备节点消失，
  WMI 就搜不到该串口 → 判定断连（WMI 反映物理设备是否真在，不受注册表残留 / 打开句柄影响）。
- 抽出 `FindMatchingPorts()`：返回匹配串口列表，并区分三种情况——
  null=WMI 查询失败（不误判断连，交给 I/O 探测兜底）、空列表=设备已不在、
  非空=设备还在；`FindScannerPort()`（连接建立用）基于它取第一个匹配。
- 固定串口模式（`ScannerPort` 配了固定值）：没有关键词可搜，回落
  "端口名是否还在系统列表 + I/O 探测"。
- 效果：拔掉扫码枪 → 3 秒内心跳判定"动态搜索不到该串口" → 状态变"未连接"（红）
  并提示一次；插回 → 后台静默重连自动恢复。

### 二、送风机温度显示控件 TextBox → Label（保证颜色生效）

**问题**
- `UpdateFanDisplay` 里"当前温度高于设置温度 → 红、不高于 → 绿"的颜色设置
  在 `txtUpperTemp`（ReadOnly TextBox）上不生效。
- 根因：ReadOnly 文本框获得焦点、文字处于选中状态时，WinForms 优先绘制
  "选中高亮色"覆盖 `ForeColor`，所以红/绿看不到。

**改动（MainForm.cs / MainForm.Designer.cs）**
- `txtUpperTemp` → `lblUpperTemp`、`txtSetTemp` → `lblSetTemp`（TextBox → Label，
  自动调整大小、去掉 ReadOnly）。
- 颜色规则不变：`data.Temperature > data.TempSetpoint` → 红，否则 → 绿；
  离线 / 未启用显示 "---"。

### 使用说明
- 拔掉扫码枪：底部状态栏 3 秒内变红"未连接"，日志提示"扫码枪已被拔掉"；
  插回后自动恢复"已连接"。
- 送风机监视区温度文字颜色现在一定生效：高于设置温度红色、不高于绿色。

## [2026-08-07] 连接心跳机制（静默自愈）：断连及时提醒 + 后台自动重连（V1.16.2）

### 问题
- 设备"连上后"中途掉线（网线被拔 / 适配器被拔 / 控制器断电）时，状态更新不及时，
  操作员不知道"是哪个设备断了"。
- 上一版的"重试几次就放弃"虽然不刷屏，但设备恢复后不会自动连回，体验一般；
  而"一直重试"又怕日志刷屏。两者需要调和。

### 改动（统一解法：心跳感知 + 静默持续重连 + 日志只记边沿）
**1) 心跳感知断连（1~3 秒内状态更新）**
- 耦合器 / 气压表：随 1s 采集周期心跳（读失败/端口级异常自动置断开），顶部"通讯连接状态"
  与"在线 X/72"实时更新；
- 送风机：独立 2s 轮询心跳，断开后状态标签立即"未连接"（红）；
- 气压表新增**串口级故障识别**（`ModbusRtuBarometerReader.IsPortLevelFailure`）：
  把"RS485 适配器被拔出（端口级异常）"和"单台设备无响应（正常离线）"区分开，
  只有端口级异常才判定串口断开——不会因为个别表不响应就误报整条串口断。

**2) 断连提醒"哪个设备断了"（只记一次，不刷屏）**
- 四台设备统一：只在"已连接 → 断开"边沿记一条日志，如
  "IO耦合器已断开（通讯异常），正在后台自动重连..." / "气压表串口已断开..." /
  "冷却送风机已断开..." / "扫码枪已断开..."，重连成功再记一条"已重连"。
- 连续失败的中间过程【静默】，不再每 3~10 秒刷一条失败日志。

**3) 后台静默持续重连（替代上一版"重试 5 次就放弃"）**
- 扫码枪 / 耦合器 / 送风机 / 气压表：连接失败后按各自节流（3~10 秒）在后台一直重试，
  设备恢复后自动连回，全程不打扰操作员；不再有"自动重连已停止"提示。
- 按需重连 + 弹窗兜底保留：用户操作需要某设备且仍连不上时，弹窗"xxx未连接，请先连接"。
- 测试期间送风机没连：明确提示"送风机未连接，无法启动环境温控（测试期间温度不受控！）"，
  恢复后送风机自动重新启动。

**4) 按需重连完全异步（点了先弹"连接中..."，不卡界面）**
- 涉及耦合器的操作（上电 / 开真空 / 启动 / 停止 / 急停）和送风机操作（定值启动 / 停止 /
  启动测试）：设备没连时，点击按钮立即弹"正在连接耦合器/送风机，请稍候..."（主窗体临时禁用
  防止重复点击），在后台线程完成重连后再继续操作；连不上才弹"xxx未连接，请先连接"。
- 按钮处理统一改为 async/await + `Task.Run`，连接最坏 3 秒超时不再卡住 UI（原来同步阻塞）。

**5) 性能与互不干扰**
- 所有重连（含按需重连）都在后台线程（Task.Run / 独立定时器）执行，互不阻塞；
  气压表串口断开时返回"全 null 数组"，让逐台失败计数继续触发"通讯故障"关阀断电安全兜底，
  采集主链路（耦合器 / 送风机）不受影响。

**6) UI 补充**
- 底部状态栏新增"扫码枪"连接状态（已连接=绿 / 未连接=红 / 未启用=灰），
  与顶部"通讯连接状态"（耦合器）、送风机状态标签一起构成四设备状态总览。
- 新增"连接中..."提示窗体（MainForm.ShowConnecting/HideConnecting），异步重连期间显示。
- 送风机"上部温度（当前温度）"颜色改为**按设置温度对比**：高于设置温度（txtSetTemp）→ 红、
  不高于 → 绿（原来按固定告警上限 FanTempAlarmLimitC 判断）；超过告警上限的安全日志保留，
  但不再覆盖按设置温度显示的颜色。

**修复：扫码枪断连状态不更新（USB 拔出时串口事件不触发）**
- 现象：扫码枪 USB 虚拟串口被拔掉后，底部状态栏一直显示绿色"已连接"，也不会自动重连。
- 根因：断连检测完全依赖 SerialPort 的 `ErrorReceived` / `DataReceived` 事件，但 Windows 上
  端口安静（无数据流动）时拔掉 USB 设备不一定会触发任何事件，`_isConnected` 一直保持 true，
  重连定时器里 `TryConnect()` 开头 `if(_isConnected) return` 短路，永远无法恢复。
- 修复：重连定时器新增**端口存活心跳** `CheckConnectionAlive()`（双重判定）——每 3 秒先核对
  当前端口是否仍在系统串口列表（`SerialPort.GetPortNames()`，USB 拔出后通常从列表消失）；再对
  已打开的串口句柄做一次主动读取探测（`BytesToRead<=0` 时 `ReadExisting()`，避免抢数据；句柄
  已失效时该读取会抛异常——部分驱动在应用持有句柄时拔出 USB 仍保留 COM 条目，仅查列表查不到）。
  任一判定成立即"边沿"提示一次"扫码枪已断开"并断开，再交给后台静默重连自动恢复。

### 使用说明
- 设备中途掉线：状态标签 / 底部状态栏 1~3 秒内更新为"未连接"，日志提示一次"哪个设备断了"；
  把设备恢复后，后台自动重连并提示"已重连"，无需重启程序。
- 不再需要手动"重新连接"：后台一直在静默重试，操作时若还没连上才会弹"连接中..."再弹"未连接"提示。
- 点操作按钮时设备没连：按钮不卡顿，先弹"正在连接xxx，请稍候..."，几秒内自动继续或提示。

> 说明：本条目取代同日晚些时候的"重试几次就放弃"策略（`MaxReconnectAttempts` / 失败计数已移除），
> 改为静默持续重连；"用到时按需重连 + 弹窗兜底"的部分保留不变。

## [2026-08-07] 连接失败重试策略优化 + 气压表小数位通用化

### 问题
- 扫码枪 / 耦合器 / 送风机对连不上的设备**无限重试**（每 3~10 秒一次），
  网线/串口没插时后台一直空转、日志刷屏。
- 操作时设备没连上只会悄悄失败（写输出无反应），操作员不知道要"先连接"。

### 改动
**1) 统一"重试几次就放弃 + 用到时按需重连 + 弹窗提示"**
- 扫码枪（`ScannerService`）：自动重连连续失败 5 次后停止（每 3 秒一次），状态"未连接"；
  新增 `TryReconnectNow()`，打开"录入批号"窗口时按需重连，仍连不上弹窗"扫码枪未连接，请先连接"。
- 耦合器（`DeviceManager.TryReconnectIo`）：自动重连连续失败 5 次（约 25 秒）后停止；
  新增 `EnsureIoConnected()`，上电 / 开真空 / 启动停止测试 / 急停等操作前按需重连，
  连不上弹窗"耦合器未连接，请先连接"。
- 气压表（`ModbusRtuBarometerReader.SetAllThresholds`）：串口未连接时先按需重连一次，
  仍失败则公共参数窗口弹窗"气压表未连接，请先连接"。
- 送风机（`FanControllerClient`）：自动重连连续失败 5 次后放弃（`EnsureConnected` 不再尝试）；
  新增 `ReconnectNow()`，定值启动 / 停止按钮按需重连，连不上弹窗"送风机未连接，请先连接"；
  启动测试时送风机没连也提示（不阻断，但告知老化过程无环境温控）。

**2) 气压表小数位通用化（配置驱动，换表改配置即可）**
- 气压表小数位由 `BarometerDefaultDecimalPlaces`（App.config）统一控制，压力读取与阈值写入同源，
  不读设备 0x0002（实测不可靠）。换气压表 / 换型号时按新表实际小数位改这一个配置即可，无需改代码。
- 公共参数窗口失败提示中增加"若仪表显示值与设定值差 10 倍，核对小数位配置"的指引。

### 使用说明
- 设备连不上：最多自动重试几次后停止，界面状态更新为"未连接"，不会无限空转。
- 操作时设备未连接：程序会先尝试重连一次，仍失败则弹窗"xxx未连接，请先连接"。
- 换气压表：按新表实际小数位改 App.config 的 `BarometerDefaultDecimalPlaces`。

## [2026-08-07] 四项现场问题修复（阈值精度 / 通讯状态 / 温度标签 / 送风机状态色）

### 问题
- 公共参数窗口写入负压阈值：设 -95，很多台气压表实际阈值变成了 -9.5（差 10 倍），少数台写入失败。
- 顶部"通讯连接状态"显示"已连接"，但现场耦合器（IO）根本没连接。
- 送风机监视区"上部温度 / 下部温度"标签与需求不符（要"当前温度"，下部温度行删掉）。
- 送风机状态文字无颜色区分（要 未连接=红 / 定值启动·已连接=绿 / 定值停止=灰）。

### 改动
**1) 阈值 -95 写成 -9.5 根因修复（ModbusRtuBarometerReader）**
- 现场实测（写→读回验证工具）：72 台中 47 台的 0x0002（小数位寄存器）返回 0，而仪表实际按 1 位小数显示
  （Demo 注释早有"0x0002 可能无效"警告）。
- `SetThreshold` 写阈值不再读设备 0x0002，固定用配置 `BarometerDefaultDecimalPlaces`（=1，与 Demo 硬编码一致）：
  -95 → 寄存器 -950（旧代码对 0x0002=0 的台写寄存器 -95 → 仪表显示 -9.5）。
- `ReadData` 读压力同样不再信任 0x0002，固定 1 位小数（否则真空状态下压力会显示错 10 倍：
  仪表显示 -95.0 = 寄存器 -950，程序会显示 -950）。
- 实测验证：写 -95 → 寄存器 0xFC4A(-950) → 读回 -950 = -95.0 ✅

**2) 通讯连接状态改为只判断耦合器（DeviceManager / ModbusTcpIoController / MainForm）**
- `OnConnectionStatusChanged` 事件语义从"气压表串口"改为"IO 耦合器是否连接"；
  `MainForm_Load` 不再用气压表启动结果冒充耦合器状态。
- 新增 `DeviceManager.IsIoConnected`；`TryReconnectIo` 每采集周期做状态边沿检测，
  耦合器连上/断开都及时刷新顶部"通讯连接状态"。
- IO 控制器读/写遇到连接层异常（Socket / IO / 超时）自动置 `IsConnected=false`，
  避免断网后状态一直卡在"已连接"。

**3) 送风机监视区标签调整（MainForm.Designer）**
- `lblUpperTempLabel` "上部温度"→"当前温度"；删除"下部温度"行（lblLowerTempLabel/txtLowerTemp
  及全部引用移除），监视分组高度同步缩小。后续加装下部探头再加。

**4) 送风机状态文字 + 颜色（MainForm.UpdateFanDisplay）**
- 未连接（通讯失败/离线）= 红；定值启动 / 程式运行中 / 已连接 = 绿；
  定值停止 / 程式停止 = 灰；未启用（配置关闭）= 灰。

### 使用说明
- 公共参数窗口写阈值后，可在气压表本机显示屏确认显示为 -95（不再是 -9.5）。
- 顶部"通讯连接状态"现在反映的是耦合器（IO）是否连上；气压表串口是否连通看 LOG 诊断与压力数据是否刷新。
- 送风机监视区显示"送风机状态 / 设置温度 / 当前温度"三行。

## [2026-08-07] 工位面板更名重设计 + 通讯连接修复 + 送风机监视区调整 + 控件更名

### 问题
- 现场实测：主界面"整个界面数据不刷新"（顶部红色"未连接"），公共参数窗口"保存失败"，送风机"无法连接"。
- 排查根因：App.config 气压表串口写死 COM1（现场实际是 CH340 的 COM9，COM1 不存在）→
  `DeviceManager.Start()` 要求"气压表 + IO 耦合器全部成功"才启动采集；耦合器（GX-CL140）当前未连通
  → 整体启动失败，把气压表和送风机也一并回滚、定时器不启动 → 三个现象同时出现。
- 另外：公共参数批量写阈值与 1s 采集定时器争抢同一条 RS485 串口总线，会造成大量写超时。

### 改动
**1) 通讯连接修复（根因）**
- 新增 `Services/SerialPortHelper.cs`：CH340 串口自动识别（WMI 查 VID_1A86/PID_7523，参考 Demo）。
- `ModbusRtuBarometerReader`：端口识别顺序 = **上次连接成功端口缓存（`BarometerPort.cache`，优先直接连、省去重新搜索）**
  → 配置端口 PortName → CH340 自动识别。**连上就缓存、缓存失效（设备被拔/换 USB 口）自动重新识别**
  ——与送风机 `FanLastIp.cache` 同款"工控机记忆"机制；逐个候选端口尝试，失败自动换下一个。
  新增 `CurrentPortName` 属性记录实际使用的端口。
- `DeviceManager.Start()` **门禁解耦**：只要"气压表串口"连通就启动采集；IO 耦合器/送风机是可选设备，
  断开不再拖垮整机；新增 `OnDiagnostic` 事件逐步上报连接结果（实际串口/耦合器/送风机 IP），
  `LastStartupError` 记录失败原因，MainForm 写进 LOG。
- `ModbusTcpIoController.Connect`：同步 `Connect` 改为 BeginConnect+手动超时（避免 IP 错时卡 20 秒）；
  Connect/Disconnect 加锁与后台重连线程安全。
- IO 耦合器自动重连：每 5 秒后台尝试，重启耦合器后自动恢复阀/载台电控制。

**2) 公共参数窗口批量写修复**
- `DeviceManager.SetAllBarometerThresholds`：批量写期间暂停主采集定时器（try/finally 恢复），避免与采集争抢串口总线。
- `ModbusRtuBarometerReader.SetAllThresholds`：串口未连接时返回空字典（UI 显示"未连接任何气压表"而非 72 台全失败）；
  每写一台加 50ms 间隔（与 Demo 对齐）。Mock 实现同步对齐。

**3) 送风机监视区改为三项温度（不关心湿度）**
- `groupBoxMonitor` 三个标签改为：设置温度 / 上部温度 / 下部温度。
- `UpdateFanDisplay`：设置温度=控制屏设定值（TempSetpoint）；上部温度=控制屏当前温度（Temperature，唯一探头）；
  下部温度=保留项暂显"---"（后续加装探头后改数据源即可）；温度告警标红改到"上部温度"。

**4) 控件更名（现场无 PLC，用 GX-CL140 ModbusTCP 模组）**
- `lblPlcStatusLabel`→`lblCommStatusLabel`、`lblPlcStatus`→`lblCommStatus`、`_plcConnected`→`_commConnected`，
  文字"PLC连接状态"→"通讯连接状态"。

**5) 工位面板更名 + 重设计（BarometerPanelView → WorkstationPanelView）**
- 文件/类更名为 `WorkstationPanelView`（显示的是 72 个工位，每个工位配一台气压表）。
- 界面按现场草图重设计：NO.x 编号、上电状态灯（绿/灰纯色）、上电/下电按钮（显示"要执行的动作"，
  测试中/故障禁用）、真空压力（只读）、真空开启灯（绿/灰纯色）、工作状态（IDLE/SELECT/BUSY/FAULT）、
  SN、配方、延时开启/到达、Set 按钮。
- 新增 `OnPowerToggled` 事件：MainForm 据此切换该工位载台上电输出。
- csproj Compile 项、MainForm 引用、列宽/行高常量（245）同步。

### 使用说明
- 程序启动后 LOG 会显示诊断：实际使用的气压表串口（如"已自动识别 CH340 → COM9"）、耦合器连接结果、送风机连接结果。
- 气压表串口：连接成功后程序 exe 目录生成 `BarometerPort.cache`（一行端口名，如 COM9），下次启动优先用它直接连；
  设备被拔 / 换 USB 插口导致端口失效时，程序自动重新识别 CH340 并覆盖缓存（LOG 可看到实际使用的端口）。
- 耦合器未连通时：气压表压力数据照常刷新，送风机照常运行；耦合器恢复后几秒内自动重连（LOG 有提示）。
- 参数设置 → 公共参数（公共参数窗口）：输入负压值 → 保存，采集临时暂停、批量写后恢复。

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
- 设备阈值（0x0010）的**单位/数值**需按气压表说明书确认后再写（不是 -95kPa 那个软件报警阈值）。
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
