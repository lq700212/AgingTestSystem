# 烧屏测试控制中心（AgingTestSystem）

> 给显示屏做"高温通电老化"的车间软件：一屏看住 **72 台真空负压表 + 72 路真空阀 + 72 路载台电源**，
> 外加冷却送风机与扫码枪，实现**上料 → 抽真空 → 上电老化 → 下料判定 → 追溯**的业务闭环。
> Mock 与真实硬件一键切换，不接线也能演示全流程。

![软件主界面：72 工位一屏铺满](docs/images/01-main.png)

| 想找什么 | 去这里 |
| :--- | :--- |
| 车间怎么用（开机→生产→报警处理） | [`docs/车间操作员使用说明.md`](docs/车间操作员使用说明.md)（真实截图，另有同名 PDF） |
| 换型/配方/排障/授权（客户口径） | [`docs/客户技术工艺使用说明.md`](docs/客户技术工艺使用说明.md)（另有同名 PDF） |
| 内部交付/权限/排障清单 | [`docs/内部培训文档.md`](docs/内部培训文档.md)（另有同名 PDF） |
| 二次开发（架构/通讯/画布/业务子系统） | [`docs/二次开发指导手册.md`](docs/二次开发指导手册.md) |
| 通讯协议、寄存器、IO 映射、坑点 | [`docs/通讯接入.md`](docs/通讯接入.md)（**唯一协议文档**） |
| 改代码前必读的约定与红线 | [`AGENTS.md`](AGENTS.md) |
| 改动历史 | [`CHANGELOG.md`](CHANGELOG.md)（唯一出处，最新在前） |

---

## 1. 软件长什么样（图文导览）

### 1.1 主界面：72 台一屏铺满，无滚动条

中间 8×9 工位面板自适应铺满窗口（面板允许宽扁拉伸、字取窄边不变形、小字一律加粗保证可读）；
右侧是运行状态 / 送风机监视 / 操作区 / 日志四段式布局；每块面板点空白即选中，点【设置】即开该台配置。

![软件主界面：72 工位一屏铺满](docs/images/01-main.png)

- 面板一眼读完一台的状态：真空压力、SN、配方、延时/烧屏计时、上电/真空/下电三块灯、【设置】按钮。
- 右侧操作区：批量设置配方 / 录入批号 / 启动运行 / 停止运行 / 报警复位 / 下料判定 / 全部停止（急停）。
- 顶栏只有一行（项目 / 权限 / 通讯 + 4 个菜单按钮），状态栏实时显示在线数与测试中台数。

### 1.2 开机生产三步：登录 → 批号绑定 → 下配方启动

登录按角色进（操作员 / 技术员 / 管理员），密码 PBKDF2 哈希存储，明文不落盘。

![登录窗口](docs/images/02-login.png)

第一步录入批号，第二步把工位和产品 SN 绑上（支持扫码枪自动填充），第三步批量下配方并加入队列，
最后在主界面勾选工位点【启动运行】。

![录入批号窗口](docs/images/03-inputlot.png)

![工位与SN绑定窗口](docs/images/04-idbinding.png)

![批量设置配方窗口](docs/images/06-batchrecipe.png)

单台要微调就点面板上的绿色【设置】，只改这一台，不影响勾选集；批量改多台只走右侧操作区的批量入口。

![单台工位设置窗口](docs/images/07-stationsettings.png)

做到时自动下电关阀，面板变蓝"已完成·待取料"；判定口径为"待判定"时，在此人工录 PASS / FAIL（FAIL 必填不良代码）。

![下料判定窗口](docs/images/11-unload.png)

### 1.3 工艺配置：配方 → 策略 → 系统设置 → 项目

配方管"一台怎么做"（延时/烧屏时长/极限温度/负压阈值），左侧列表、右侧编辑，增删改即自动落盘。

![配方管理窗口](docs/images/05-recipe.png)

工艺策略是"整线怎么跑"的驾驶舱：拓扑固定、点节点改配置，启动开阀、抽真空、上电老化、完成下电、
报警联动、下料判定、断电恢复、MES 上报每个环节一个节点；预置 A 常用 / B 标准 / C 宽松 / D 严格一键套用，
下拉切档即预览，保存与系统设置走同一条落盘路。

![工艺策略窗口](docs/images/10-policy.png)

系统设置是全部配置项的表格（按业务分类，悬停有说明）：改完点【保存设置】即生效，
连接参数自动重连，只有设备数量/布局/模拟开关等结构型配置需重启。

![系统设置窗口](docs/images/09-settings.png)

公共参数（批量写 72 台气压表设备阈值 0x0010，并同步软件报警阈值，两边一起）与项目切换（配方/策略/工位设置跟项目走，切项目即换工艺，无需重启）。

![公共参数窗口](docs/images/15-commonparam.png)

![项目切换窗口](docs/images/12-projectswitch.png)

### 1.4 追溯运维：历史 → 调试 → 账号 → 授权

历史记录读 CSV 落盘事件，起止日期一查即出，【导出】按项目报表列配置生成 xlsx。

![历史记录窗口](docs/images/08-history.png)

通讯测试（IO 点灯/一键遍历/备用通道可视化连线）与送风机测试（定值启停/温湿度）都复用主程序共享连接，
不自建连接，状态与主界面实时一致；现场单点点动阀只在这里做，不进测试态。

![通讯测试窗口](docs/images/16-comtest.png)

![送风机测试窗口](docs/images/17-fantest.png)

用户管理（操作员/技术员/管理员三级，另有隐藏 dev 最高权限账号做交付兜底）与软件授权
（与 HJVision 同源：CPU 序列号 + MD5 码，同一套《获取激活码》工具通用；30 天/永久两档，不阻断生产）。

![用户管理窗口](docs/images/14-usermgmt.png)

![软件授权窗口](docs/images/13-activation.png)

---

## 2. 技术栈与设备规模

| 设备 | 数量 | 协议 / 接入 | 关键实现 |
| :--- | :---: | :--- | :--- |
| 真空负压表（气压表） | 72 | Modbus RTU，RS485→CH340 USB，**19200 8N1**，从站=设备号 1~72 | `ModbusRtuBarometerReader` |
| IO 耦合器 | 1 | GX-CL140，Modbus TCP `192.168.1.20:502`，80DI/160DO | `ModbusTcpIoController` |
| 冷却送风机 | 1 | 厂商控制屏，Modbus TCP 端口 **50000**，定值启停 + 温湿度监视 | `FanControllerClient` |
| 扫码枪 | 1 | Honeywell Xenon 1902 虚拟串口 **115200**，WMI 自动识别串口 | `ScannerService` |

Mock 与真实实现由 `UseMockCommunication` 一键切换，接现场硬件只改配置、不改代码。

## 3. 架构与分层

```
MainForm / WorkstationGridView（Views 视图层，单窗口自绘大画布）
        ↓ 弹出
Dialogs（窗体层：CommonParameterForm / SettingsForm / StationSettingsForm /
          CommunicationTestForm / FanTestForm / InputLotForm / IdBindingForm ...）
        ↓ 调用
DeviceManager（服务层核心编排：采集/测试状态机/报警联动/送风机生命周期）
  ├─ IBarometerReader  (MockBarometerReader / ModbusRtuBarometerReader)
  ├─ IIoController     (MockIoController    / ModbusTcpIoController + IoMapBuilder)
  ├─ IFanController    (MockFanController   / FanControllerClient)
  ├─ ScannerService（扫码枪，独立）   UserManager（用户/权限，Users.json，密码 PBKDF2 哈希）+ PasswordHasher
  ├─ TestEventLogger（测试事件落盘 Logs\TestLog_yyyyMMdd.csv，供历史记录窗体）
  ├─ AppLogFileWriter（主窗体 UI 操作日志落盘 Logs\AppLog_yyyyMMdd.log，与文本框逐行一致）
  ├─ BuildWatermark（启动第一行版本水印：版本/构建时间/混淆标记，远程先看这行定版本）
  └─ CrashLogWriter（全局异常一崩一文件 Logs\Crash_*.log，含水印+全堆栈，请客户发回此文件）
        ↓
Models（BarometerData / FanData / IoStatus / DeviceConfig / RecipeConfig / StationInfo）
```

- **IO 映射**：内部用十进制连续编号（输入 1~80、输出 81~240），`IoMapBuilder` 换算成三菱八进制物理地址（X/Y），
  每台气压表 = 1 输入（报警触点 DI）+ 2 输出（真空阀 Y + 载台上电 Y）。八进制：X007 后是 X010，Y107 后是 Y110。
- **事件**：`DeviceManager` 一次性触发 `OnBatchDataUpdated`（72 台数据数组，避免逐条 72 次 UI 切换）；
  慢轮询时另节流触发 `OnScanProgress`（在线数实时爬，首轮"扫描中"秒出数，不等整轮）；
  `OnConnectionStatusChanged` / `OnFanDataUpdated` / `OnDiagnostic`。

## 4. 目录结构与职责（定位用）

| 文件 | 职责 |
| :--- | :--- |
| `Services/DeviceManager.cs` | **业务编排核心**：Start/Stop、采集定时（1s）、测试状态机、报警边沿联动（关阀+断电）、送风机生命周期、工位静态信息、共享连接门面（测试窗体复用同一连接） |
| `Services/ModbusRtuBarometerReader.cs` | 气压表 RTU：读压力、写设备阈值、72 台轮询、串口锁 |
| `Services/SerialPortHelper.cs` | CH340 串口自动识别（WMI VID_1A86/PID_7523）+ 端口缓存 BarometerPort.cache |
| `Services/ModbusTcpIoController.cs` | IO 耦合器：DI 读 / DO 读-改-写单点 / 备用通道映射 / 共享原始寄存器 API |
| `Services/IoMapBuilder.cs` | 内部编号 ↔ 三菱八进制 X/Y 地址映射 |
| `Services/FanControllerClient.cs` | 送风机 TCP：定值启停、读状态/温湿度，断线重连节流，IP 自动识别 + FanLastIp.cache 工控机记忆 |
| `Services/ScannerService.cs` | 扫码枪：WMI 识别串口、串口读码、断线心跳重连 |
| `Services/TestEventLogger.cs` | 测试事件 CSV 落盘（启动/停止/报警/复位/急停/真空建立/MES上报联调事件） |
| `Services/AppLogFileWriter.cs` | 主窗体 UI 操作日志落盘（Logs\AppLog_yyyyMMdd.log，按日期分文件，与文本框逐行一致，写失败静默） |
| `Services/BuildWatermark.cs` | 启动版本水印：主窗体构造第一行写入 AppLog（对外版本＋程序集/文件版本＋exe 构建时间＋混淆标记＋进程位数＋OS）；崩溃日志嵌同一份水印 |
| `Services/CrashLogWriter.cs` | 崩溃落盘：一崩一文件 Logs\Crash_yyyyMMdd_HHmmssfff_xx.log（含水印＋线程＋类型＋消息＋全堆栈；null/非 Exception 兜底，绝不抛） |
| `Services/UserManager.cs` | 用户/登录/权限，Users.json 持久化（密码哈希）；含 dev 最高权限账号（可删改业务管理员，dev 名系统保留） |
| `Services/PasswordHasher.cs` | 密码哈希（PBKDF2-HMAC-SHA256，随机盐 + 10 万次迭代，`PBKDF2$迭代$盐$哈希` 自描述格式） |
| `Services/SoftwareActivation.cs` + `Dialogs/SoftActivation.cs` | 软件激活：与 HJVision 同源同口径（CPU 序列号 + MD5 30 字符码，同一套《获取激活码》工具通用）；30天（768 运行小时格）/永久两档；`MainSetting.ini [RunHash]` 存双键（gitignore），主窗 1 小时 Timer 提醒，新设备/过期只置灰用户权限入口，不阻断启动与生产；激活成功关闭弹窗即重查解灰；工控机现场一键激活走 `tools/auto_activate.py`（免手工抄码，随维护人员走不发客户；另有 PowerShell 版 `tools/auto_activate.ps1`，双击 `tools/auto_activate.bat` 即跑） |
| `Services/RecipeStorage.cs` | 配方列表持久化（跟项目走 `Projects/<项目>/Recipes.json`；同名覆盖保存带重复检查） |
| `Services/StationSettingsCache.cs` | 工位配置缓存（跟项目走 `Projects/<项目>/StationSettings.json`：SN/配方/延时/极限温度/负压阈值/显示模式，设置窗口下次打开自动回填） |
| `Services/ThemeManager.cs` | 深色/浅色主题服务：App.config 存 AppTheme（Light/Dark），双向映射表递归着色（语义色保留、按钮不动），打开窗体前 ApplyTo、切换时 ApplyToAllOpenForms |
| `Services/MesMapping.cs` / `Services/MesReporter.cs` | MES 对接：映射解析纯函数（触发器/字段映射/静态字段 vocabulary 唯一出处）+ 上报器（后台 POST JSON/鉴权/重试/离线缓存 MesQueue.json；Transport 测试缝；Mock 只写 CSV） |
| `Services/RuleExpr.cs` / `Services/RuleEngine.cs` | 规则表达式：沙盒解析求值（12 变量冻结）+ 执行器（编译缓存/持续计时/完成表达式 OR，只能加严不能松绑） |
| `Controls/RuleListEditorPopup.cs` | 规则表编辑弹窗（多行文本+实时校验+变量速查） |
| `Views/ProcessPolicyForm.cs` / `Views/PolicyGraph.cs` | 工艺策略窗：固定拓扑画布，节点显示有效配置（预览??真实）+实时台数，点节点改配置走与设置表同一条保存路；滚轮缩放/中键平移/节点拖拽；预置 A 常用/B 标准/C 宽松/D 严格一键套用，下拉末项"自定义"（独立槽，初始=A快照）；下拉切档画布+右栏一起预览，关窗时预览未套用弹确认框；导出可选任一源装包，导入一律进自定义槽；入口=参数设置下拉 |
| `Services/Mock*.cs` | Mock 实现（免接线演示） |
| `Views/MainForm.cs` | 主窗体：无系统标题栏（Sunny 标题藏掉，最小化/最大化/关闭自绘进顶栏最右）；工位区 8×9 一屏铺满；顶栏单行 30px（项目/权限/通讯＋4 按钮）；状态栏（"在线"全部离线标红）；权限控制、扫码事件、操作区按钮；深色切换收进"关于"下拉，仅 dev 可见；主界面布局纯代码固定（顶栏/状态栏 30，右侧按窗口 16% 比例，分隔条锁死不可拖） |
| `Views/WorkstationGridView.cs` | 工位网格自绘大画布：1 个 UserControl 画全部面板 + 行全选列（竖排大字）；布局纯代码缺省（改布局改代码重编译）；坐标命中实现单击选中/设置按钮/选中框/行全选/悬停提示；只留双向铺满一屏（面板允许宽扁拉伸，字取窄边不变形，下限 4pt，正文一律加粗）；跟随全局主题（语义状态色不动） |
| `Models/PanelLayoutConfig.cs` | 工位面板布局配置模型：面板网格尺寸/面板内各元素坐标/字体/颜色/按钮与提示文字；纯代码缺省；全部元素"锚定"解析（改面板宽高自动联动），字段全表见类头注释 |
| `Dialogs/CommunicationTestForm.cs` | 通讯测试窗体（IO 耦合器 DO 输出测试，负压阀/载台上电两页 9×8 灯按钮 + 预留点位页 + 一键遍历 + 通道右键端口映射可视化连线页，保存走设置表同一条路即时生效） |
| `Dialogs/FanTestForm.cs` | 送风机测试窗体（定值启停 + 温湿度显示） |
| `Dialogs/SettingsForm.cs` | 系统设置（管理员，按分类编辑 App.config 全部配置项 + "工艺策略"分类，策略存项目 Policy.json；写回保存即生效，连接参数自动重连；仅设备数量/布局/模拟开关等结构型配置重启生效；说明悬停 tooltip 超 40 字换行） |
| `Dialogs/StationSettingsForm.cs` | 工位设置（SN/配方/延时时间/烧屏时间/极限温度/负压阈值/显示模式写入 StationInfo；配方名下拉单选禁手输、库中已无则保存拦停；两时间三 NumericUpDown 冒号分隔；保存=应用+缓存+存配方、加入队列=应用+存配方、下电=关闭载台上电） |
| `Dialogs/RecipeManagerForm.cs` | 配方管理窗口（左侧列表可滚动 + 右侧可编辑输入；添加/更新/删除操作即自动落盘，无"保存设置"按钮） |
| `Dialogs/BatchRecipeForm.cs` | 批量设置配方窗口（配方名下拉单选禁手输；两时间三 NumericUpDown 冒号分隔，都写入配方；加入队列=保存配方+应用到选中工位，无选中先保存配方并提示选择） |
| `Dialogs/IdBindingForm.cs` / `InputLotForm.cs` | 录入批号 + 工位↔SN 绑定（扫码枪自动识别填充，生成 Excel） |
| `Models/` | BarometerData / FanData(+FanRunState) / IoStatus / DeviceConfig / RecipeConfig / StationInfo / PanelLayoutConfig / PolicyEnums（工艺策略枚举） / 用户模型 |
| `Services/ProjectProfile.cs` / `Services/ProjectPolicyStore.cs` | 项目档案：`Projects/<项目>/` 路径解析/迁移/切换（配方/工位设置/策略跟项目，用户/快照/日志跟机器；主页布局是纯代码固定值，不跟文件）；策略分流读写 Policy.json（PolicyKeys 唯一名单）；切换即时生效无需重启，在测禁切 |
| `Dialogs/UnloadJudgeForm.cs` / `Dialogs/ProjectSwitchForm.cs` | 下料判定窗（完成判定=待判定时用）/ 项目切换窗（仅管理员，可新建/切换/删除，当前项目禁删） |
| `.opencode/skills/agingtest-regression/` | 项目最终测试验证技能：一键"构建→冒烟→回归断言"，用例源码 `tests/TestRunner.cs`，新测试用例一律沉淀于此（用法见其 SKILL.md） |
| `Resources/app.ico` / `Resources/app.png` | 软件图标（ico=exe/桌面图标，csproj `ApplicationIcon` 编进 exe；任务栏图标是窗口图标，主窗构造从自身 exe 文件提取绑定，换 ico 重编即同步；png=同款大图只入库备用） |

## 5. 核心业务流

### 5.1 老化测试单台流程（三阶段状态机）
```
[准备] 录入批号 → 绑定工位↔SN → 设配方（延时时间/烧屏时间/负压值/显示模式随配方下发，显示模式仅记录）
[启动] 只开真空阀 + 送风机定值启动；任务参数(时长/延时/阈值)此刻定格
[抽真空] 等「真空到位」且「距开阀≥配方延时时间」两者满足（判定阈值=配方负压值优先，全局-5kPa兜底；
         VacuumConfirmTimeoutMs 默认15s 内始终不到位→真空建立失败报警：关阀断电标故障，全程不带电；
         设 0=关闭（不限时等，不判建立超时，只靠老化失压报警+人工停止；跟项目走，机器缺省 15000 不动）；
         面板真空灯：阀没开灰 / 开了到位绿 / 开了没吸住红，下电灯只看载台电）
[上电] 条件满足自动载台上电 → 进入老化计时（延时=0 时阀电同开直接计时）
[老化] 计时时长=配方"烧屏时间"(>0)，否则回退 MaxTestDurationSeconds(0=不限时长手动停；启动框对 0 时长/空 SN 工位追加警告，可继续；策略可切硬拦截）
[完成] 到时自动下电+关阀 → 状态"已完成·待取料"(面板蓝) → 日志记 PASS（策略=待判定时记"待判定"，下料时人工录 PASS/FAIL+不良代码+处置，进 CSV 追溯）→ 人工复位/重新扫码/下料判定回空闲
[监控] 压力越限 / 真空建立失败 / 通讯失联 / DI触点(可选) / 送风机超温全线联停(可选，默认关) / 老化中失压(策略：停机报警，或只记事件继续老化）→ 报警联动
[停止] 手动停止=中止(回空闲,不计判定)；末台时送风机自动停止
[急停] 全部停止：全关阀+全断电+停送风机+清任务快照（带防误触确认）
[断电恢复] 异常退出后再启动：检测到 TestSession.json 快照 → 弹窗选"恢复测试"或"放弃并安全关闭阀与电源"（策略：整台重测，或重抽真空+补足剩余时长，断电期间不计）
[MES上报] 启动/完成/报警/下料判定四事件按触发器后台 POST JSON 到 MesEndpoint（映射/静态可配；Mock 只写 CSV；失败重试+离线缓存，永不阻断生产）
[规则] 自定义报警规则成立即报警记FAIL（与内置同一边沿）；完成表达式成立即提前完成（只能提前）；跳过抽真空=启动即上电+压力豁免（机械夹具，启动大写警告）；规则变量含电流 `current`（无表=NaN恒false，只追溯不判定）
[报表] 历史记录窗导出按钮按 `ReportColumns` 列配置生成 xlsx（留空=缺省预设11列：时间/批号/SN/配方/工位/事件/结果/详情/压力/温度/电流，跟项目走；设置表该行点出表格弹窗配列，不用手写文本；历史窗导出旁有"报表列设置"按钮，仅管理员，同一份配置；身份口径开关 `EventIdentityMode`：记录现值/启动定格，跟项目走）
[配方] 三窗显示模式下拉框按 `DisplayModes` 字典单选（字典外选不进来，老值追加可见存时拦；`DisplayModeEnabled` 开关默认隐藏该行+布局收缩，当前项目零打扰；批量/工位窗在测下发提示"仅对新启动生效"——定格语义，启动瞬间定格）
```

### 5.2 报警来源（DeviceManager.IsAlarm）
1. **压力越限**：真空压力 > `AlarmPressureThresholdKPa`（默认 -5kPa，即真空变差）
2. **真空建立超时**：开阀后确认超时内压力未进正常区间（设 0=关闭此项）
3. **通讯失联**：某台连续读取失败 ≥ `CommunicationLossAlarmCount` 次
4. **DI 报警触点**（可选）：`UseDiAlarmContact=true` 时启用，默认关

联动动作：**报警进入边沿触发一次**（不是周期重复）→ 关该台真空阀 + 断载台上电 + 标故障；结果写 CSV 日志。

> 气压表是"双信号"：压力值走 RTU 供软件判报警（**联动只由此触发**）；报警硬件触点走 DI（X000~X107，NPN）只读进 UI 显示，不参与联动。

### 5.3 送风机生命周期（72 台共用，不能随某台停机）
- 有任一台在测试 → 保持运行；全部停止 → 才允许停机。`UpdateFanLifecycle` 里"只下发一次命令"状态记忆防重复写。
- 主界面不提供手动定值启/停按钮（与自动生命周期管理重复且误导），维护调试请用"关于→送风机测试"。
- **IP 自动识别**：连接顺序 = FanLastIp.cache（上次成功）→ FanIpAddress → FanIpCandidates；候选列表配几个识别几个，设备换 IP 自动找到并更新缓存。

### 5.4 主界面操作入口（右侧"操作"区）
批量设置配方 / 录入批号 / 启动运行 / 停止运行 / 报警复位 / 下料判定（完成判定=待判定时用，AutoPass 下点它只提示） / 全部停止(急停) / 面板"设置" / 行"全选"。

> 现场单点点动（只开阀不进入测试态）请用"关于→通讯测试(IO)"；送风机维护停机（全停状态下）请用送风机测试窗口。

> **面板"设置"按钮（点哪台开哪台）**：点哪个工位的绿色【设置】按钮，
> 就弹该工位的工位设置窗口，不看页面上勾选了几个、也不改勾选；
> 批量改多台只走右侧"操作"区的【批量设置配方】按钮（按当前选中集下发）。

> **面板选中交互**：每个面板右上角永远有个选中框（选中=绿底白✓，未选中=空心框）；
> **点选中框或点面板空白处即切换该工位选中**（鼠标与触摸屏都是点一下，无需长按）；
> 取消选中逐台点回，或用每行右侧"全选/取消"按钮整行切换。

> **72 站自适应（只剩双向铺满一屏，列数 8×9 不动）**：中间工位区多大、
> 72 站就双向缩放到精确铺满（无任何滚动条；面板允许宽扁拉伸，字取窄边不变形；
> 小屏跟随缩小不挤叠，正文一律加粗保证小字清楚）。
> 顶栏锁死 30px；每行最右"全选"按钮竖排大字。

## 6. 配置项速查（App.config + 项目 Policy.json，可在"关于→设置"管理员界面编辑；保存后大部分配置立即生效，连接参数自动重连，仅结构型配置重启生效；策略跟项目走 `Projects/<项目>/Policy.json`，切项目即换策略）

| 配置项 | 默认值 | 说明 |
| :--- | :--- | :--- |
| `TotalBarometers` | 72 | 气压表总数 |
| `TotalInputs` / `TotalOutputs` | 80 / 160 | IO 总数（业务用 72/144） |
| `PanelColumns` / `PanelRows` | 8 / 9 | 工位区列数/行数（8×9=72；改动需与 TotalBarometers 一致，否则启动报配置警告） |
| `CollectInterval` | 1000 | 气压表采集间隔(ms) |
| `PortName` / `BaudRate` | COM9 / 19200 | 气压表串口（连接成功缓存 BarometerPort.cache 优先复用） |
| `UseMockCommunication` | false | true=Mock 免接线 |
| `InvertInputs` / `InvertOutputs` | false | 输入/输出逻辑取反（NPN/PNP 现场差异，灯亮软件读 OFF 时试 true） |
| `IoUnitId` | 1 | IO 耦合器从站 |
| `IoInputRegisterStartAddress` / `IoOutputRegisterStartAddress` | 0x1000 / 0x2000 | DI / DO 起始寄存器 |
| `IoBackupChannelMappingEnabled` / `IoBackupChannelMappings` | false / `0x2000@0x00->0x2009@0x01;0x2000@0x01->0x2009@0x02;0x2008@0x00->0x2009@0x08` | **备用通道映射**（DQ 通道烧毁时启用，源寄存器@通道->目标，寄存器/通道均十六进制，通道 0x00~0x0F，写/读 DO 自动重定向；开关默认关，映射串只是出厂示例） |
| `BarometerPressureRegisterAddress` | 0x0001 | 压力寄存器（0x0002 为小数位，实测不可靠不再使用） |
| `BarometerDefaultDecimalPlaces` | 1 | 小数位（压力读取与阈值写入统一用，换气压表改这里） |
| `BarometerPressureScale` | 1 | 压力额外缩放 |
| `AlarmPressureThresholdKPa` / `AlarmWhenPressureHigherThanThreshold` | -5 / true | 软件报警阈值(不是设备阈值) |
| `PressureAlarmEnabled` | true | 压力报警总开关：false=阈值越限与真空建立超时全不报，负压阀保持常开；DI/失联/自定义规则不受影响；跟项目走，预置A默认关闭 |
| `MuteAllAlarms` | false | 报警全关·最高级：true=阈值/超时/DI/失联/规则/超温联停全不报，压住压力报警开关；四预置默认全关，只走手动/自定义；跟项目走 |
| `PlcAddress` / `PlcPort` | 192.168.1.20 / 502 | IO 耦合器 |
| `FanEnabled` / `FanIpAddress` / `FanPort` | true / 192.168.1.220 / 50000 | 送风机（可选设备，连不上不影响启动） |
| `FanAutoDetectEnabled` / `FanIpCandidates` | true / .220,.221,.222 | 送风机 IP 自动识别 |
| `FanUnitId` / `FanTimeoutMs` | 1 / 3000 | 送风机从站/超时 |
| `VacuumConfirmTimeoutMs` | 15000 | 真空建立确认超时(ms；0=关闭不限时等，跟项目走 Policy.json，机器缺省不动） |
| `CommunicationLossAlarmCount` | 3 | 通讯失联报警阈值(连续失败次数) |
| `MaxTestDurationSeconds` | 0 | 老化最大时长(0=不限) |
| `UseDiAlarmContact` | false | DI 报警触点并入判定（需现场确认电平） |
| `FanTempAlarmLimitC` | 0 | 送风机温度告警上限(0=不启用) |
| `FanTempShutdownEnabled` | false | 超温全线联停开关（默认关=只记日志；开=超温自动停全部在测工位） |
| `ZeroDurationPolicy` / `EmptySnPolicy` | Warn | 0时长/空SN启动策略（Warn=警告可继续 / Block=硬拦截） |
| `FanDisconnectPolicy` | LogOnly | 送风机断连策略（LogOnly=提示后照跑 / BlockStart=阻断启动） |
| `VacuumFailKind` | ProductFail | 真空失败责任（ProductFail=记FAIL / FixtureAlarm=记装夹异常） |
| `CompletionJudgePolicy` | AutoPass | 完成判定口径（AutoPass=自动PASS / PendingReview=待判定+下料人工录） |
| `PowerLossPolicy` | RestartFull | 断电恢复（RestartFull=整台重测 / ResumeRemaining=续跑剩余） |
| `AgingPressureLossPolicy` | StopOnLoss | 老化中失压（StopOnLoss=停机报警 / KeepRunning=只记不停） |
| `CompletionAction` / `VentValveDoPoint` | PowerOffOnly / 0 | 完成动作（蜂鸣/破空泄压；点位0=未接硬件，选泄压只记日志） |
| `ActiveProject` | 烧屏测试 | 当前项目指针（机器级；配方/策略/布局跟项目走 `Projects/<项目>/`；切换即时生效无需重启） |
| `MesEnabled` / `MesMockEnabled` | false / false | MES 上报总开关（默认零行为；Mock=只写 CSV 不发 HTTP） |
| `MesEndpoint` / `MesTimeoutMs` | 空 / 5000 | MES 接收 URL（单入口；留空不发）/ HTTP 超时 ms |
| `MesAuthType` / `MesAuthToken` / `MesAuthUser` / `MesAuthPassword` | None / 空 / 空 / 空 | 鉴权（None/Bearer/Basic；token 与密码 DPAPI 加密落盘） |
| `MesCustomHeaders` / `MesEndpointMap` | 空 / 空 | 自定义头/按事件分地址（头名纯 ASCII；鉴权优先；分地址回退默认） |
| `MesRetryCount` / `MesRetryIntervalMs` | 3 / 2000 | 失败重试次数/间隔（全灭进离线缓存，下次成功补发） |
| `MesTriggers` / `MesFieldMap` / `MesStaticFields` | 空 / 空 / 空 | 触发器/字段映射/静态字段（跟项目走 Policy.json；留空=全开/直通/无） |
| `SkipVacuum` / `CompleteExpression` / `CustomAlarmRules` | false / 空 / 空 | 规则流程（跳过抽真空/完成表达式/自定义报警规则；全空=零行为） |
| `UsePowerMeter` | false | 载台电流回采总开关（默认零行为；开=面板电流行直显+CSV+规则变量current，电表到货即插即用；关=原来布局） |
| `ReportColumns` | 空 | 报表列配置（跟项目走；留空=缺省预设11列，历史窗导出xlsx按此列） |
| `EventIdentityMode` | RecordTime | 事件行SN/配方取值（跟项目走；RecordTime=记录现值/StartSnapshot=启动定格） |
| `DisplayModes` | 空 | 显示模式字典（跟项目走；留空=缺省8项，三窗下拉单选；设置表该行点出列表弹窗编辑） |
| `DisplayModeEnabled` | false | 显示模式维度开关（跟项目走；默认三窗隐藏该行+布局收缩，当前项目零打扰） |
| `ScannerEnabled` / `ScannerPort` | true / 空 | 扫码枪开关 / 固定串口（空=WMI 自动识别；出厂已开，代码缺省 false） |
| `ScannerDeviceKeyword` / `ScannerBaudRate` | Xenon 1902 / 115200 | 扫码枪识别关键词 / 波特率 |

## 7. 菜单与权限

| 按钮 | 下拉项 | 权限 |
| :--- | :--- | :--- |
| 用户权限 | 操作员 / 技术员 / 管理员 / 用户管理* | *仅管理员（dev 登录时用户管理多出"管理员"角色，可删改业务管理员） |
| 参数设置 | 公共参数（批量写气压表阈值）/ 配方管理 / 工艺策略（点节点改配置，只读看图人人可看，改配置限管理员）/ 项目切换*（新建/切换/删除项目档案；在测禁切，当前项目禁删） | 技术员+（*仅管理员） |
| 日志记录 | 历史记录（读 CSV） | 任意 |
| 关于 | 设置* / 通讯测试** / 送风机测试** / 版本说明 / 深浅模式切换*** | *仅管理员；**技术员+；***仅 dev |

默认账号（Users.json）：operator / technician / admin，密码均 123456（PBKDF2 哈希存储，明文不落盘，见 `Services/PasswordHasher.cs`）。
最高权限账号 dev / dev123：走"用户权限→管理员"登录框输入即进，界面无任何提示（隐藏入口）；dev 可删改业务管理员（用户管理窗），dev 名注册/改名一律回"该账号名不可用"，dev 自身不允许改名/删除。

## 8. 关键设计决策与坑点（排障/新功能必读）

1. **共享连接**：通讯测试/送风机测试窗体**不自己建 TCP 连接**，复用 `DeviceManager` 的共享连接，
   连接状态与主界面实时一致。测试窗体读送风机状态走缓存（零额外报文）。
2. **IO 写 DO 必须读-改-写**：0x2004~0x2008 低/高字节被不同业务共用（如 0x2004 低字节=电磁阀65~72、高字节=载台上电1~8），
   整字直写会误伤其它通道。载台上电"全关"时也只清高字节、保留低字节。
3. **BeginInvoke 传数组参数必须 `new object[]{arg}` 包装**：`BarometerData[]` 会被协变成 params 展开 → TargetParameterCountException。
4. **UTF-8 with BOM**：所有 .cs 必须带 BOM，否则 WinForms 设计器"无法设计基类 System.Void"。
5. **两种阈值别混淆**：`AlarmPressureThresholdKPa`（软件判报警，kPa，不写设备）≠ 设备阈值（写气压表 0x0010，单位与压力读数一致）。
6. **小数位固定 1 位**（`BarometerDefaultDecimalPlaces`）：小数位寄存器现场部分台返回 0 不可靠，压力读取与阈值写入统一固定。
7. **有符号强转**：压力原始值须 `(short)` 强转，否则 0xFFFE 被当 65534，负压反号。
8. **批量写某台超时 = 设备问题**（掉线/断电/地址错/损坏），不是程序 bug；用批量读取/扫描 CSV（`Logs\BarometerScan_*.csv`）定位离线台。
9. **扫码枪断连判定用 WMI 动态搜索**（设备拔出 PnP 节点消失即判断连），不能用注册表/ReadExisting（都不可靠）。
10. **送风机状态枚举要 4 值**（0x0000~0x0003）+ UI 显式 switch 中文，不能 ToString（程式模式回 0/1 会显示裸数字）。
11. **送风机是可选设备**：连接失败不影响整机启动；用独立 2s 定时器轮询，不阻塞气压表采集。
12. **事件处理一律 IsDisposed 检查 + BeginInvoke**：避免窗体释放后 ObjectDisposedException。

## 9. 构建与验证

```powershell
# 构建（若提示找不到 MSBuild，先定位：Get-ChildItem 'C:\Program Files*\Microsoft Visual Studio' -Recurse -Filter MSBuild.exe | Select -First 1）
& "D:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe" AgingTestSystem/AgingTestSystem.csproj /p:Configuration=Debug /p:Platform=AnyCPU /t:Build /nologo /v:m
```

- 构建成功标准：输出 `AgingTestSystem -> ...\bin\Debug\烧屏测试控制中心.exe` 且无 error。
- **新增 .cs 文件必须手工在 csproj 登记**（老式项目无通配，漏登记报 CS0246）。
- **所有 .cs 必须 UTF-8 with BOM 编码**（见上文第 8 节第 4 条）。
- 一键验证：`powershell -ExecutionPolicy Bypass -File .opencode\skills\agingtest-regression\scripts\build_and_test.ps1`
  （自动完成"构建 → 真机冒烟 → 全量回归"；日常小改可加 `-Affected` 只测影响面；用法与覆盖范围见 `.opencode/skills/agingtest-regression/SKILL.md`）。
- 运行时文件不入库：配方/工位设置/策略跟项目走（`Projects/<项目>/`，位于运行目录 bin/ 下）；
  用户/快照/日志/连接缓存/激活文件跟机器（`Users.json` / `TestSession.json` / `Logs/` / `*.cache` / `MainSetting.ini`，见 `.gitignore`）。
- 界面截图来源：`docs/images/`（培训三件套共用同一套截图；README 门面图即取自该目录）。

## 10. 常见问题排查

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

## 11. 待完善项

- 批号档案缺独立落盘（现状：批号只活在内存 `DeviceManager.CurrentLotNumber`，
  随每行测试事件 CSV 与断电快照 `TestSession.json` 落盘，`StationSettingsCache` 不存批号；
  启动不校验批号、批号不在逐台任务快照里定格。后果：空批号可直接启动（CSV 批号列留空）；
  中途改批号会让同一台的"启动"行与"完成"行批号不一致；正常退出后快照自动清、
  重启批号归空，同一批次跨重启追溯断裂；批号↔SN↔配方关系只在手动导出 xlsx 时落盘，
  无批次级汇总与换批防错）
