# CHANGELOG

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
