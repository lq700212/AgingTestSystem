---
name: agingtest-regression
description: AgingTestSystem 项目专属的最终测试验证技能：一键完成"构建 → 真机冒烟测试 → 全量回归测试用例"。回归 harness 覆盖 PasswordHasher/UserManager 登录权限/配置归一化/IO 映射解析/配方存储/双日志器/面板布局锚定联动/工艺策略/项目档案热更删除/终结器释放/关窗竞态/MES映射上报/规则表达式/工艺策略窗/电流报表画面/软件激活(HJVision同源MD5三件套)等全部核心逻辑类（1878 断言）。当用户要求"跑测试、冒烟测试、回归验证、测一遍、发布前验证、改完代码验证一下"或修完 bug/加完功能需要验证时使用；新增测试用例也必须沉淀到本 skill 的 tests/TestRunner.cs 中。
---

# AgingTestSystem 回归测试套件（冒烟 + 用例一体）

本项目（WinForms/.NET Framework 4.x）**没有单元测试框架**，本 skill 用
"csc 编译独立 harness + 自研 Check 断言"的方式实现可离线自动化的全量回归，
是项目的**最终测试验证手段**。UI 像素级问题另见全局技能 `winforms-ui-debug`（两者分工：
本管逻辑正确性，那管视觉正确性）。

> 开工前先读 `AGENTS.md`（UTF-8 编码、改后必构建、文档同步等红线全部适用）。

## 一、快速用法

```powershell
# 一键全跑：构建 → 冒烟(真机 exe 存活) → 全量回归用例（发布/大重构用，默认）
powershell -ExecutionPolicy Bypass -File ".opencode\skills\agingtest-regression\scripts\build_and_test.ps1"

# 按影响面跑：构建 → 冒烟 → 只跑 git 改动命中的模块（日常小改用，最常用）
powershell -ExecutionPolicy Bypass -File ".opencode\skills\agingtest-regression\scripts\build_and_test.ps1" -Affected

# 只冒烟（已构建好的 exe 启动存活 18s 判定；设备连接超时导致真实启动要 10~15s）
powershell -ExecutionPolicy Bypass -File ".opencode\skills\agingtest-regression\scripts\smoke_test.ps1"

# 只跑回归用例（不重新构建，引用 bin\Debug 现有产物；-Modules 可指定子集）
powershell -ExecutionPolicy Bypass -File ".opencode\skills\agingtest-regression\scripts\run_unit_tests.ps1" -Modules "UiStyleV172_1,ThemeManager"

# 查有哪些模块 / 看某次改动会命中哪些模块（不执行，只分析）
powershell -ExecutionPolicy Bypass -File ".opencode\skills\agingtest-regression\scripts\run_unit_tests.ps1" -Modules "list"
powershell -ExecutionPolicy Bypass -Command "& '.opencode\skills\agingtest-regression\scripts\get_affected_modules.ps1' -Files @('AgingTestSystem\Dialogs\CommonParameterForm.cs')"
```

## 一点五、分级回归策略（V1.72.4：小改不跑全量）

- **日常小改**（单个窗体/单个服务/单个模型）：`build_and_test.ps1 -Affected`，
  按 `git diff` 自动算模块子集（如公共参数窗间距只跑 `UiStyleV172_1`，17 条秒过）。
- **必须全量**：大重构、发布前、改了骨架（csproj/Interfaces/用例自身/scripts）。
  映射不到的新文件/接口改动会自动兜底全量（fail-safe，宁多跑不漏测）。
- **映射表**在 `scripts/get_affected_modules.ps1` 的 `$Map`：
  **新增产品 .cs 文件必须在此登记**（否则每次改它都付全量代价）；
  登记时连带"断言交互"的模块一起写（如 TestEventLogger 改格式→全部 DeviceManager* 都读 CSV）。
- 判定标准：退出码 0 = 全绿；输出末尾 `PASS = n FAIL = 0 / ALL PASS`。
  失败时会打印每条失败断言的名称与实际值明细。

## 一点六、发版（混淆包）流水线（V1.88.10：调试期也要能发混淆包）

- **一键发版**（版本缺省读 `BuildWatermark.ReleaseLabel`，工作区脏默认拒绝）：
  `powershell -ExecutionPolicy Bypass -File ".opencode\skills\agingtest-regression\scripts\obfuscated_release.ps1"`
  六步全自动：前置检查 → Release 构建（临时翻 `IsObfuscatedBuild` 为 true，打完 finally 还原，
  日常 Debug 包不受影响）→ Obfuscar 混淆 → 组包 → 三项验收 → 还原校验。
  看到 `发版成功 + 验收全过` 才算成；退出码 1=构建/混淆挂、2=验收挂、3=环境缺、4=工作区脏。
  调试期加 `-DebugPackage` 打调试包（不翻标记不混淆，产物 `release/<版本>-dbg/`，其余同路；
  构建一律 Rebuild，增量 Build 会残留上次的标记）。
- **产物**（`release/<版本>-obf/`，gitignore 永不入库）：`包/`（混淆 exe＋依赖 dll＋exe.config＋
  部署说明.txt，无 pdb/源码/旧数据，整包拷工控机）＋`归档/`（Mapping.txt＋全包 MD5＋pdb＋
  本次配置＋版本.txt 含 git 号与当时工作区diff，客户堆栈反解全靠它）。
- **三项验收**（挂任何一项都不许发）：A 行为 12 项（`tests/ObfuscationAcceptance.cs` behavior
  模式：水印版本＋混淆版标记/日志落盘/策略反射全命中/JSON 往返/规则/激活/两窗联想字段，
  其中 A12 专抓 Skip 写法错）；B 元数据对账（dump 两边 Models 清单逐行 diff，一字不差）；
  C 真机冒烟（混淆 exe 活 22s＋AppLog 首行水印正确＋零 Crash 文件）。
- **验收跑器双进程是强制的**：同名同版本程序集在同一 AppDomain 只能存在一份
  （执行态和反射态都按 identity 归一），对账必须"各 dump 一份文本再 diff"，
  合进程必报"已从其他位置加载"（实测）。
- **前置依赖**：`dotnet tool install -g Obfuscar.GlobalTool`（命令 `obfuscar.console`，
  发版脚本缺了会自动装）；混淆配置模板 `tools/obfuscation/obfuscar.xml`
  （`ExtraFrameworkFolders` 指 v4.7.2 引用目录，升级目标框架同步改；InPath/OutPath
  发版脚本按绝对路径生成，Obfuscar 新版只认绝对路径）。
- **改了发版链要知道**：`obfuscated_release.ps1` 与 `tests/ObfuscationAcceptance.cs`
  都在 `$FullPatterns` 覆盖下（scripts/tests 改动兜底全量回归）；新增 ps1 必须带
  UTF-8 BOM（PS5.1 无 BOM 解析中文直接 ParserError，实测）；XML 属性读写走
  SelectSingleNode＋SetAttribute（`$_.value=` 适配器写法在 `-File` 下抛
  XmlNodeSetShouldBeAString，交互式却正常，原因未明，显式 DOM 最稳）。

## 二、目录结构与职责

```
agingtest-regression/
├── SKILL.md                  ← 本文件（套路与踩坑沉淀，改完必读必更新）
├── scripts/
│   ├── build_and_test.ps1    ← 一键流水线（构建→冒烟→回归），退出码 1/2/3 区分阶段
│   ├── smoke_test.ps1        ← 冒烟：启动真 exe → 轮询存活 → Stop-Process
│   ├── run_unit_tests.ps1    ← 拷贝产物到 %TEMP% 隔离 run 目录 → csc 编译 harness → 运行
│   └── obfuscated_release.ps1 ← 一键混淆发版（构建→混淆→组包→三项验收→还原标记），见一点六
└── tests/
    ├── TestRunner.cs         ← 全部测试用例源码（加用例就改这里）
    └── ObfuscationAcceptance.cs ← 混淆验收跑器（behavior/dump 双模式，见一点六）
```

## 三、测试覆盖范围（46 个模块，1878 断言）

| 模块 | 覆盖点 |
| --- | --- |
| PasswordHasher | PBKDF2 格式自描述、盐随机性、正反例、损坏串静默失败、中文/超长密码 |
| UserManager | 默认账号、登录边界（空值/Trim/大小写/角色错配）、AddAccount 唯一性与保底、RemoveAccount 保护、改密链路、HasPermission 权限矩阵、记住登录往返、Users.json 损坏回退重建、缺角色自动补齐、手改双管理员防呆；V1.64 起 dev 最高权限 J 组 34 条（种子+隐藏登录+注册保护不可用+删改矩阵+老文件自愈+手改保留 dev+第一业务管理员） |
| SettingsForm.Normalize | StopBits(1/15/2) 与 Parity(None/Odd/Even/Mark/Space) 归一映射（反射调私有静态方法），含中文/缩写/非法值兜底 |
| IoOutputChannelRemap | 多组解析、中英文分号/箭头、0X 大小写、脏项跳过汇总 error、源=目标、通道 0x00~0x0F（V1.62 起 0x10+ 直接拒绝）、缺前缀；V1.81 可视化连线 26 条（Validator 新建拦截：源唯一/目标独占/自环/越界、老配置多源同目标照常加载、删查序列化往返；Catalog 缺省池源160/备用16/全空闲160、首点/载台落址/分组、非法配置空池不抛）；V1.81.1 失焦守卫 2 条（表格弹窗失焦自关保留/连线中豁免）；V1.81.2 结构 5 条（窗体屏中/双缓冲关闭/Ellipsize 装得下截断零宽）；V1.81.3 预计算 3 条（徽标有无/176行装框/超长名截断，间隙点击 harness 探针）；V1.81.4 包围盒裁剪 4 条（两端可见/中段穿屏画、全屏外/水平无交不画，长连线反向验证精准 FAIL）；V1.82 缩放 10 条（步进×1.2/上下钳/锚定跟随·零点·不负，Dock 遮挡与交互走真窗 harness 截图+断言） |
| DeviceConfig.ParseFanIpCandidates | 中英文分隔符、非法过滤、去重保序、IPv6、空输入族 |
| RecipeStorage | Load/Save 往返全字段、损坏 json 返 null、空数组、"null"字面量、SaveWithDuplicateCheck 新增分支、删中间配方后 Max+1 不撞号(V1.62)、DisplayMode 往返(V1.66) |
| TestEventLogger | CsvEscape 转义（逗号/引号翻倍/换行/回车 V1.62）、表头（V1.76 共 11 列：时间/批号/SN/配方/设备/事件/结果/详情/压力/温度/电流）、落盘字段格式、null 字段 11 列、温度一位小数、电流有数两位小数/NaN 记空不写字样、结构化列序（SN001/R-A/PASS）、整机行身份结果记空、20×5 并发零丢失、删目录自重建 |
| AppLogFileWriter | UTF-8 追加、空串忽略、8 线程×5 行并发一条不少（lock 生效） |
| PanelLayoutConfig | 默认布局基准坐标（V1.88.17 紧凑 204×170：按钮145,120,50,42/选中框181,2,18/SN宽130/压力宽130右缘同界）、ResolveAnchors 幂等零漂移、高度+10 下链跟随+上链锁定+交接缝吸收（V1.77：SN/压力定高位现76/54）、宽度+10 右锚定组随动、颜色解析钳位/回退、SaveDefault→重载零差异、电流行几何（V1.77：缺省关/有效高188行高200/电流行65,74,71,18/SN94配方116按钮138/压力54不动/标签76/SN标签96/开关往返零漂移）、工作状态块删除（V1.88.16：真空块上第一行26当基准/下电跟随/删除三处无残留锁） |
| HomeLayoutConfig | 默认值、Save→Load 往返、范围约束、损坏文件回退默认、越界文件按Range钳（V1.88.17：5000高/负宽进不了主窗）、并单行（V1.88.23：HeaderHeight默认36/范围34~100/老双键文件读36/主窗单行字段锁/编辑器三输入框+顶栏量程） |
| ModelRoundtrip | RecipeConfig/UserAccount JSON 往返（含特殊字符）、StationInfo/FanData Clone 深拷贝互不影响 |
| AgingSequencer | ShouldPowerOn(压力×延时双条件)、ShouldComplete(0=不限时长)、IsVacuumBuildFailed(到位即不失败) 边界族、IsPressureOutOfRange 双方向+恰等不越限(V1.62)、负时间语义锁、BuildStartWarningText 0时长/空SN 警告文案(V1.66)、IsFanOverTempShutdown 开关+上限+边界(V1.66) |
| TestSessionStore | 快照往返全字段、损坏 json 静默 null、Clear 幂等、空清单视为无任务、Stations:null 与"null"字面量、Save(null)=false |
| AgingBusinessModel | DeviceStatus.Completed 枚举与 BarometerData 往返、LastTestResult 默认值/Clone、AgingPhase 三值、StationInfo.RecipeNegativePressure |
| ThemeManager | Parse 大小写/空格兼容与乱写兜底浅色、双向映射表往返精确（容器底/文字/单元格/输入底，V1.62 补齐剩余分支）、语义色保留（红/绿不动）、SetMode 内存切换、Panel+Label+TextBox+Button+DataGridView 整树着色冒烟（STA harness 直接 new 控件不断言弹窗；注意 Label/Button 的 Fore/Back 地 getter 在 Empty 时返回父容器值，断言要写"跟父一致"而非具体值，见 TestRunner 注释）、Sunny 分支(V1.71：UIButton 不动/FillColor 保留、UITextBox/UIComboBox 输入映射、ApplyButtonColors 原生/Sunny 双写、GetEffectiveButtonColors 读 FillColor) |
| **DeviceManagerIntegration** | **端到端状态机**（Fake 气压表+Fake IO 经注入构造驱动真实 DeviceManager，30ms 采集秒级跑完生命周期）：正常全流程(启动只开阀→到位+延时上电→配方时长完成→Completed·PASS→阀电全关)、真空建立失败(超时报警+全程不带电+FAIL)、通讯失联(设备异常≠FAIL)、手动中止(回空闲不计结果)、断电恢复(快照落盘→重启询问→整台重测/放弃关阀)、扫码重绑清完成态、配方阈值优先于全局 |
| IoMapBuilder(V1.62) | 八进制编址(X000/X007/X010/Y110/Y217)、预留点、非法四抛、编号公式、兼容重载；V1.80 预留页同源锁（80/160 配置预留 DI=8 路 X110 起/DO=16 路 Y220 起、无预留两区为空）+ SpareGrid 保位掩码/RMW 合并纯函数 |
| MockDevices(V1.62) | 三 Mock 未连接约定/越界/副本隔离、气压两档区间千次采样、风机启停守卫与漂移界 |
| StationCache(V1.62) | 往返全字段、覆盖语义、副本双向隔离、脏文件三态、非法编号过滤（反射重置静态缓存+隔离目录） |
| ModelDefaults(V1.62) | DeviceConfig 全构造默认值、风机枚举寄存器值、FanData/BarometerData Clone 全字段与数组深拷贝、LoginResult 工厂、角色值、快照与配方构造默认 |
| SettingsValidate(V1.62) | ValidateValue 全类型矩阵、TryParseUShort、范围表抽查+默认值落界、布尔键一致（V1.74：16 项含 UsePowerMeter）、连接键契约、CreateValueCell 全分发、分类/说明键对齐（构造真窗体不断言弹窗）、PersistChanges 统一路（V1.71：矛盾/MES 拦截、策略落盘+热回写、机器键落盘、空改动、null 不抛，运行目录隔离+备份还原）、三非模态弹窗关闭即释放（V1.72.13：走生产挂接反射调 ShowXxxPopup→OpenForms 找窗→Close→IsDisposed）、报表列/显示字典校验分支（V1.74） |
| ScannerParse(V1.62) | JoinPorts、ParseParity/ParseStopBits、与设置窗 NormalizeStopBits 跨文件 15 口径 |
| ModbusConvert(V1.62) | 气压/阈值换算纯函数、IsPortLevelFailure 中英文关键字、未连接约定、串口参数解析 |
| FanParse(V1.62) | 寄存器解析(/100 全字段)、不足 6 个、非法枚举透传、未连接约定、Connect(null) |
| StationTime(V1.62) | 时分秒组合、25 小时不截断(V1.62 修复锁)、超 99 钳制、文本格式、Clamp |
| HistoryCsv(V1.62) | CSV 解析边角、与 TestEventLogger 互逆 11 列（V1.76：SN/配方/结果结构化+报表格映射锁）、报表列设置按钮权限门与布局锁（V1.75：默认无权限/按钮五字/无重叠/不出右界/宽容五字/管理员有权限） |
| UiPureHelpers(V1.62) | 批号去空格、配方查找(ignoreCase)+25h 不截断、工位温度读取(V1.63 数字框恒合法+回填钳制)、IP 合法、数字格钳制、网格命中/边界/四色、位值→通道、风机中文(V1.63 对齐主窗)、CH340 谓词/串口参数钳制(V1.63)、右侧宽度比例 ComputeRightPanelWidth(V1.65：0.234 常量/护栏/兜底/自定义优先 8 条)、配方窗负压/显示模式框回填(V1.66)、反射 as-cast 跟随控件换型（V1.71：TextBox→UITextBox 两处）、两窗tooltip+破空显隐纯函数（V1.73）、配方管理窗6项tooltip全覆盖+换行锁（V1.86.5）、参数入口无权限提示3条（V1.88：有权限放行/无权限含权限不够/指明去用户权限）、时间名统一（V1.88.12：两窗tooltip锁"对应工位面板延时时间/烧屏时间"+配方窗标签"延时时间：/烧屏时间："）、真空块三色（V1.88.12：ApplyData反射阀关灰/阀开到位绿/阀开未到位红+红绿异色+下电逻辑不变）、选中框常显签名锁（V1.88.12：DrawPanel去anySelected+旧调度已删+IsAnySelected门控保留）、单击点选（V1.88.14：长按整套删除IsAnySelected/ClearAllSelection/_longPressTimer/Tick+ToggleSelect无门槛翻转反射+按宽自适应ComputeFitZoom宽口径/zoom并进Scaled/字体下限6pt/AutoFit默认开）、延时0填0阀电同开tooltip锁（V1.88.14：三窗"同时开"）、双向精确铺满（V1.88.17：ComputeFitZoomBoth独立/非法回1,1/MinMax0.15/4/ScaledX/ScaledY/矩形双向/挂载双轴+铺满±1px/选中框正方形+命中同源/保Panel1宽640七条）、小屏字号（V1.88.21：MinFontSize6→4/1280真实zoom跟随缩小+标签时间装槽两条）、双模式大字（V1.88.22：ComputeFitZoom单轴捞回/默认FitWidth/双模式挂载+字号等比9条）、删滚动单路（V1.88.24：枚举/FitMode/单轴/MinMax/拖拽Tick/校正删除锁+挂载精确铺满/窄边4pt/面板加粗+顶栏分区字段/SetFontSizePt三锁）、顶栏列宽高度（V1.88.26：默认30/范围28/回退老文件30/量程28/钳-5→28/纯函数常规封顶非法三锁；V1.88.27：权限区Panel类型锁，与项目区同构Dock居中） |
| **DeviceManagerExtended(V1.62)** | 状态口/在线数/启动错误、批量 SN、配方名负压联动、副本隔离、非法电池、连接与间隔热生效、批量阈值+定时器恢复、反方向报警端到端、全局时长回退、定格隔离、清理回全局、不限时、2s 延时门、空闲容错、自愈计数、报警驻留、边沿单次(CSV 计数，V1.76 列序4/5)、快照全字段+双台+批号、急停、停止再启动、风机生命周期(MockFan，V1.88.14 追加 E26b：两台到时完成→风机停，省电回归锁)、超长数组与错 id 防火墙、脏快照恢复、显示模式下发/保持/清空+叠加采集可见+GetTestingDeviceIds(V1.66) |
| PolicyV167(V1.67) | BuildStartBlockText 阻断文案、MapAlarmResult 责任映射、ComputeResumeDuration 剩余/跑超/回拨、ValidatePolicyCombination 矛盾锁、ParseValue 大小写/非法、PolicyKeys↔DeviceConfig↔下拉选项三处同步锁、DeviceConfig 缺省=现状锁（含身份口径RecordTime）、身份口径解析+ResolveEventIdentity五态（现值/定格/无快照回退/半快照/null转空，V1.76）、快照新字段缺省锁、ValidateValue 策略分支+点位、NormalizePolicyValue 脏值兜底、WrapTooltip 40字换行、ProjectProfile 非法名/重复/切换拒绝/路径分流、Policy.json 存取往返、热更往返12条(V1.72.10：切A/切B/切回指针路径缓存跟人走+finally恢复)、Default自愈3条(正主在删+补拷+重名不覆盖/正主不在整体改名)、DeviceConfig.CopyFrom引用不变全量拷脱钩、ClearProjectScopedState清指派+Pause/Resume不擅自启动、DeleteProfile删不存在空名被拒切入当前禁删切回删除列表干净指针不变(V1.72.11)、ApplyLoadedRecipes空null清空替换引用不变(V1.72.12)、ValidatePolicyCombination无阀分支+布尔键15项(V1.73)；V1.84 补齐：Delete/Switch 路径穿越拒绝+野目录不算项目（空backup过滤）、IsValidProfileName名单、原子写（临时文件无残留+内容完整）、密码迭代 DoS 防护（巨量超界判失败）；V1.84.1：策略缓存手改即生效（三元指纹）+破空阀碰撞纯函数5判（阀区/电区拦/预留/输入区放行/0不拦）+原子二次覆盖走Replace |
| **DeviceManagerPolicy(V1.67)** | 治具责任端到端(装夹异常+CSV)、待判定完成+下料录入(收/跳过/null)+CSV明细、失压保持(不停机+边沿单条不刷屏)、续跑(快照阶段/上电时刻+剩余60s+重抽真空)、泄压(破空阀开+CSV+复位关阀不残留) |
| MesV168(V1.68) | 触发器解析(空全开/中英文分隔/未知进错/去重/命中)、字段映射(合法/未知本站/坏组/坏MES名/重复覆盖/大小写)、静态字段(坏组/空值)、组包(直通/改名/静态合并覆盖)、ParseValue字符串直通、PolicyKeys含MES三key、MES缺省锁(零行为)、ValidateValue鉴权/触发/映射/静态/布尔/整数分支、NormalizeMesAuthType兜底None、上报器Fake传输(发出/映射/静态/地址/开关零发送/触发器零发送/Mock只写CSV/全灭落盘/恢复补发清盘)、DPAPI往返/前缀/明文兼容/篡改回null、自定义头解析与鉴权优先、分地址解析与命中回退 |
| **DeviceManagerMes(V1.68)** | Fake抓包端到端：启动/完成(PASS+映射+静态+SN)/下料判定(不良代码)/报警(FAIL)四触发器各一条+发往配置地址 |
| RuleExprV169(V1.69) | 四则优先级/括号/负号/取模/字面量、比较逻辑与或非、变量大小写、短路跳过除零、除零模零未知变量错、语法错位置、NaN恒false、规则表行格式/行号/上限20、执行器持续计时(假时钟/中断复位/同配置不清/换配置清/非在测复位/立即/求值错)、完成表达式(空禁用/到点/求值错)、缺省锁、ValidateValue规则分支；V1.84 补齐：表达式含`||`分隔识别/多`||`/严格变量保存即拦/非严格运行时容错/深嵌套300层被拦+浅嵌套过（防栈溢出）；V1.84.1：深度只计真递归（括号+一元符分支，ParseOr本体不计）+100连写!被拦+浅取反过 |
| **DeviceManagerRules(V1.69)** | 自定义报警端到端(首轮触发FAIL+CSV规则名)、完成表达式提前完成(CSV原因)、跳过抽真空(直接上电+常压不误报+快照Aging+CSV)、各阶段台数R4(抽真空1/老化1/空闲2) |
| **DeviceManagerIdentity(V1.76)** | 身份口径端到端：现值模式启动→完成11列E2E（启动行SN/配方有值+结果空+详情无SN字串/完成行结果PASS）、定格模式中途重绑不断测+报警归属启动SN+结果FAIL、现值模式报警归属重绑后SN（读真实落盘CSV，与HistoryCsv互逆成写读闭环） |
| **DeviceManagerSweep(V1.84 复查补齐)** | Fake写失败注入+短数组+反射锁编排核心修复：上电失败回滚（尝试过+仍在测+没带电+记事件+恢复自动补上电）、停止先写后清（抛+状态不清+恢复停干净）、短数组尾部按失联标Fault（关阀断电+记通讯故障+头部不受牵连）+广播仍按总数、SkipVacuum运行中翻开关不改定格、超时/越限/DI原因文案（反射三判+真跑超时行记原因）、下料连判第二次进skipped、热更6↔4台数组跟上（旧SN绑定清）、急停关破空阀+风机失败记账照做仍抛、坏订阅不影响好订阅、负延时钳零、真空到位标记（V1.88.12：常压false→到位true+Clone携带，面板三色数据源）、延时0阀电同开（V1.88.14：启动双开+进Aging+计时起点+宽限保留与关闭+超时断电+启动带过电对照；Ceiling秒口径防0.5s误判；E4/场景2/S1/R4改非零延时保原场景）、停风机失败补停（V1.88.14 S15：边沿一条+保持运行态）、面板间隙不命中（V1.88.15：行/列缝不命中+底边内仍命中） |
| ProcessPolicyV170(V1.70 建图，V1.73 随窗改名) | 拓扑锁(8节点8边+端点全已知+节点挂key+key全真属性，MES上报是无连线纯配置节点)、缺省文本锁、策略切换文本变、台数进文本、布局存取往返/钳制/损坏回空、工艺策略窗构造不断言弹窗(V1.71)、检索框 SetCaretToEnd 原生/Sunny 双过(V1.71)、工艺策略窗无参构造不抛+边框7件(V1.72 Designer 拆分)、右栏重建释放走 ControlDisposeHelper 快照(V1.72.16，旧 foreach 跳过实锤)、宽松缺省布局锁(V1.72.18：左右列同X/列距≥130/行距≥40/节点不重叠/power-alarm与done-unload中心对齐/最小210×100，旧版文件回缺省)、MES节点变文案+右列最下行距(V1.73)、报警节点DI开关+无阀藏点位行(V1.73) |
| UiStyleV172_1(V1.72.1) | 弹窗主按钮蓝5窗(DodgerBlue+Custom+白字)、公共参数设计Y锁(lbl65/nud62/btn110)+CenterControls不动Y、公共参数标签输入框无重叠(V1.72.3：锁视觉间距≥8px；MeasureText比AutoSize实占小3px是根因，Designer残留Size 107过期勿用)、历史日期宽150+实测文本宽防叠、深浅下蓝保留 |
| UiFinalizerV172_14(V1.72.14) | 关窗竞态静默丢弃（Comm/Fan _closed+句柄双查+BeginInvoke；无句柄/关后日志不炸；RemapNoticeForm自释反射存在）、判定窗预览（无参构造+_lblCode/_lblDisp具名+处置选项数+空快照文案+两按钮）、关于SunnyUI（反射调internal static：UIForm+只读多行+Y≥35+版本版权文案+确认蓝+Accept）；V1.72.15 追加全仓锁 14 条（公共参数/ID绑定/设置/主窗 _closed/_mainClosing 标记、关后完成/扫码/写寄存器/控制命令/补全释放过滤静默丢弃）、切换窗tooltip+在测禁用轮询（V1.73）；V1.88.13 配方下拉 20 条（Provider 类型/两窗字段已删双锁 + 两窗 DropDownList/选项=库名/默认选中态 + 批量空格库名回填 + 工位窗脏数据追加显示/选回正常值清脏项 + 亲手换选回填参数 + 开态开窗显示模式回填；V1.88.13 复查补救：用例块内 ResetStationCache + 冷僻工位号 991/992，与静态缓存去耦） |
| LegacyRecipeGuard(V1.72.2) | V1.59老配方0值语义锁：缺字段读出0/null、下发0=定格0(0≠全局)/null=保持/清空回全局、批量窗新建默认全局、老配方回填显示0待人工复核（只构造不启采集） |
| DesignerStabilityV172_16(V1.72.16) | 快照释放（helper全释放/null安全/旧foreach红证据/驾驶舱真方法反射释放）、布局窗量程字面值（四量程=Range/340拖动同步/越界钳制/构造期越界）、三窗AutoScale=None锁（判定/批量/布局，防Font+Zoom混搭回潮） |
| PowerReportV174(V1.74) | 缺省锁（不用电表/报表空/字典空/电流NaN/Clone带电流）、Mock电表（连接/72路0.05~0.60A/断开/释放）、真实桩（连不上/全NaN不断追溯/重连失败）、编排接线（开关管创建/未Start不连，反射验_powerMeter）、规则 current 变量（解析/求值/NaN恒false）、报表列（预设11列/身份事件列序/自定义保序/未知丢弃/空合法/脏拦截/全错兜底预设）、显示字典（预设8项/重复提醒/空清空过/规范写法/字典外拦报选项/无配置走预设/配置优先/遗留追加/ValidateValue三态）、三窗显示下拉（配方窗预设8项+工位/批量窗单选非空，DropDownList锁）、报表列弹窗（预设11行回写/自定义保序/生产路径弹出释放+弹窗格分发）、显示字典弹窗（预设8行回写/自定义保序/生产路径弹出释放+弹窗格分发）、维度开关（缺省关/真值显示/三窗收缩317/330/323与开态不变+隐藏恒空+布尔键17项）、电流行直显开关冒烟（V1.77：缺省关/置位关回） |
| SoftActivation(V1.87 HJVision同源) | Encrypt标准向量pin算法（RFC1321空串/a前15字节hex）+公式关系式（设备ID码=ID+A/设备码=ID+1/30天码=设备码+30/永久码=设备码+ALL/永久标记=ID+ALL/30天起点=ID+0）+激活比对（先永久后30天，错码/空静默）+设备绑定（对上/错位/空=新设备）+计数格（0/767/839找到，840外/乱串找不到，768分界，0格30天/24格29天）+综合判定（新设备/永久/试用/768过期/找不到过期）+四档文案+ini隔离往返（双键/缺文件读空/推进一格）+激活窗构造（无参三框/设备码方程/状态行/错码静默，构造不Show直接调handler）+空模板（缺文件建出/两键读空/空=新设备/已有不覆盖） |
| PolicyPresetV185(V1.85) | 预置3个试用顺序A/B/C、管辖12开关全是PolicyKeys成员+中文名齐、每预置完整12项、标题场景换挡文案非空、值全可解析+枚举值全在下拉选项、套用探测往返、缺省=自定义/改一项即自定义、未知与空参四不抛、取值副本隔离、三预置不泄压（无阀）不跳真空、A/B联停关C开+上限0拦60过+A零门槛过、存盘口径三态、UI预置行4件+下拉4项+无参回显自定义禁用/B配置回显选中B/管理员可用/场景说明（反射读Items/SelectedIndex，禁as原生类型见坑41）、下拉列表拉宽+悬停全文+关窗释放 |
| PolicyNodeComboV1851(V1.85.1) | 节点选项框按预置下拉口径统一：全节点16下拉数=全部Bool/Enum key、下拉不比框窄、逐项独立实测无截断、悬停恒=选中全文、旧口径必截断反向验证（最长需389>270）、切节点旧提示清表、改选同步、关窗两提示皆释放；标题tooltip 5条（V1.88：key全有说明同源/换行每行≤40/缺key回空/UI层报警节点每项标题有换行提示/反射口径）；harness第二证据：最宽项真实点开展示截图无截断 |
| DeployDiagV188_9(V1.88.9 调试部署诊断) | 启动水印纯函数（版本/程序集/构建时间/调试版文案/位数关键字、混淆版mapping提示、MinValue与空输入兜底未知、GetStartupLine/GetBuildTime不抛、调试期IsObfuscatedBuild恒false锁）＋崩溃日志（文件名确定性/自动尾格式、正文水印线程类型消息堆栈关键字、空堆栈占位、null与非Exception对象兜底落盘、真实Write路径在Logs下内容全、连写两次不互盖） |

**不在覆盖范围**（明确边界）：真串口/真设备通讯（ModbusRtuBarometerReader /
ScannerService / FanControllerClient / ModbusTcpIoController，靠现场联调）、
UI 弹窗分支（如配方同名覆盖确认框，靠界面手工测试）、像素级渲染（走全局技能 winforms-ui-debug）。

## 三点五、终结器跨线程排查标准流程（V1.72.13 固化，下次不犯）

报错长这样（堆栈终点 `ResetAutoComplete←Dispose←Finalize`，控件 Name 全空，
案发时机看 GC、"报错时正在干什么"全是巧合），按本节走：

1. **先定罪再动手**：harness（Mock 采集+可疑窗+模拟操作）常规路径大概率干净，
   没有用户堆栈不硬修。拿到堆栈看终点帧——`UITextBox.Dispose` 系=孤儿输入控件；
   若释放路径全对仍炸，看是不是"关窗竞态"（后台回调在关闭前后脚碰已销毁句柄，
   "关 A 开 B 必炸"是关 A 尾巴被开 B 的 GC 赶出来）。
2. **跑自动审计**（改 UI 代码后必跑，HIGH>0 拦提交）：
   `powershell -ExecutionPolicy Bypass -File scripts\audit_finalizer_risk.ps1`
   - R1 `Controls.Clear()` 前 15 行无 `ControlDisposeHelper` → HIGH
     （V1.72.16 收紧：`foreach` 直释会跳过，一律快照 helper，白名单已删）；
   - R2 非模态 `.Show(`（排除 ShowDialog）→ 方法体/配对方法无 Dispose → HIGH；
   - R3 `Controls.Remove(` 后 10 行无 Dispose → HIGH；
   - R4 非 Designer 里 new 输入/表格控件 → INFO（逐条人工定罪，随树/释放才安全）；
   - R5 UI 文件同步 `Control.Invoke(` → HIGH（一律 `BeginInvoke`+关窗守卫；
     `?.Invoke` 事件触发与反射 `MethodInfo.Invoke` 不在列）；
   - R6 非 Designer 里 Timer 字段无同文件 `Dispose()` → HIGH
     （`(components)` 随容器跳过；`?.Dispose` 算数）；
   - R7 `_deviceManager`/`_scanner` 的 `On*` 事件 `+=` 无同文件 `-=` → HIGH
     （退订拦不住已排队 Post，handler 入口另需 `_closed` 自拦）；
   - R8 Designer 可序列化锁 → HIGH（R8a `AddRange(裸标识符)` 只许 `new` 数组；
     R8b `= xxx.Range.Min/Max` 改字面值；R8c `ZoomScaleRect`+`Font` 混搭改 `None`；
     注释行不扫）。
3. **修法**：动态重建走 `ControlDisposeHelper.DisposeAllAndClear`
   （V1.72.16：`foreach` 直释枚举中集合被改会跳过，快照后释放才对）；
   非模态弹窗 `FormClosed` 里 `finally { popup.Dispose(); }`（先回写再释放）；
   关窗竞态三件套——`_closed` 首行置位 + `IsDisposed/Disposing/IsHandleCreated`
   三查 + 日志 `BeginInvoke`（禁同步 `Invoke`），排队回调入口自拦，
   关后硬件写停手（在途遍历/启停整拍丢弃）；长事件源退订 + handler 自拦双保险。
4. **白名单登记**：修完在脚本 `$SafeShowKeys/$SafeRemoveFiles`
   登记（方法|文件[|配对方法|reuse:字段]），R2 是行为检查（验方法体真含 Dispose），
   登记了但释放被删照样报警——反向验证（注掉一处重跑必须 HIGH）是脚本改动后的
   必做项（`$SafeClearFiles` V1.72.16 已删，R1 只认 helper 行为，不认名单）。
5. **用例双锁**：动态重建锁"旧控件 IsDisposed"（V1.72.16 起锁 helper 快照释放，
   另附"旧 foreach 复现抛异常/漏释放"红证据）；弹窗锁"走生产挂接
   （反射 ShowXxxPopup→OpenForms 找窗→Close→IsDisposed）"，裸 Show/Close 恒绿假绿。

## 四、怎么加测试用例（铁律：改代码必同步补用例）

1. 打开 `tests/TestRunner.cs`，找到对应模块的 `XxxTests()` 方法；
2. 加一行 `Check("用例名(说清预期)", 条件, "可选失败详情");`
   - 异常也是预期行为时用 `CheckThrows<TEx>("名字", () => ...)`；
   - 新模块就写 `private static void XxxTests()` 并在 `Main()` 的 `allModules`
     字典里加一行（选中逻辑与 SKILL 覆盖表自动跟随，无需改别处）；
   - 过滤参数：`TestRunner.exe [模块A,模块B]` 跑子集（大小写不敏感），
     `list` 打印清单，未知模块名 exit 2（防拼错导致"零模块全绿"误报）。
3. 重跑 `run_unit_tests.ps1` 必须全绿；若新用例暴露了产品 bug → 先修产品代码再回来；
4. 若踩了新坑，把结论追加到本文件第六节。

## 五、harness 工作原理（理解了才能扩展）

- **隔离运行**：脚本把 bin\Debug 全部产物拷到 `%TEMP%\opencode\agingtest-run` 并清掉
  Users.json 等运行时文件再编译运行。因为 UserManager/RecipeStorage 用相对路径读写 json、
  日志类写到 `AppDomain.CurrentDomain.BaseDirectory\Logs`——在 run 目录里跑，
  测试产生的数据全部落在临时目录，绝不污染仓库和真实 bin。
- **编译命令要点**：csc 引用 `$runDir\AgingTestSystem.exe` + Newtonsoft.Json.dll 等；
  **必须 `/codepage:65001`**（源文件含中文且无 BOM，csc 默认按 GBK 读会乱码）。
  V1.62 起加 `/r:SunnyUI.dll`（UiPureHelpers 等用例直接 new SunnyUI 派生窗体，
  编译期须解析 UIForm 基类；dll 随 bin\Debug 产物一起拷到 run 目录）。
- **私有方法测试**：NormalizeStopBits/NormalizeParity/CsvEscape 都是 private static，
  反射 `GetMethod(..., NonPublic | Static)` 后 Invoke。
- 断言框架就三个方法：Check（计数+打印）/ CheckThrows（异常断言）/ Module（模块包裹，
  单模块抛异常不会中断整个套件）。Main 返回失败数作退出码。

## 六、踩坑清单（每次新坑必沉淀到这里）

1. **csc 无 BOM 中文源文件必须 `/codepage:65001`**：否则按系统 GBK 解析，中文字符串
   乱码甚至编译错。（csproj 由 MSBuild 处理不受影响，仅裸 csc 有此坑。）
2. **.NET Framework 没有 `Convert.TryFromBase64String`**（那是 .NET Core 2.1+ API），
   用 try-catch 包 `Convert.FromBase64String` 等价实现。
3. **读 AppLog 文件必须共享读**：AppLogFileWriter 的 StreamWriter 常驻不关（设计如此：
   句柄复用+每次 Flush），句柄 share=Read；`File.ReadAllLines` 以 FileShare.Read 打开
   （不含 Write 共享）会被 Windows 拒绝抛 IOException。要用
   `new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)`。
   外部工具（记事本等）都用 ReadWrite 共享打开，现场无碍。
4. **UserManager/RecipeStorage 用相对路径** → 测试必须先 `Environment.CurrentDirectory`
   切进一次性临时目录（EnterCleanDir），用例组之间互不依赖默认密码/账号时各自开新目录。
5. **Login 成功会顶掉实例身份**：同一 UserManager 实例先用 admin 登录、再用 operator
   登录成功后 CurrentUser 就变成 operator 了，后续管理员操作全部"权限不足"。
   验证性登录一律开独立实例。
6. **实例间内存快照不同步**：A 实例改数据并落盘后，B 实例（之前 new 的）内存里还是旧
   数据；要验证"重启后生效"必须 `new UserManager()` 一个全新实例，不能复用旧的。
7. **右对齐锚定公式是 X = 目标右缘 − 自身宽**，不是目标 X − 自身宽。推期望值先算目标
   右缘（如设置按钮 X=163、W=60 → 右缘 223 → SN.X = 223−148 = 75）。双端锚定元素两端
   都随动时宽度保持不变、间距恒定——这是锚定机制的设计意图，不是 bug。
8. **CSV 可选字段全空时行尾必然 `,,`**：TestEventLogger 每个字段后面都跟逗号，
   pressure/temperature 缺省时两个空列连着，别把 EndsWith(",") 和 !EndsWith(",,")
   组合当成"留空"判据。
9. **PowerShell 5.1 对无 BOM 的 ps1 按 ANSI 解析**，中文字符串/注释会乱码甚至语法错：
    最阴的是中文标点（如 `、` U+3001）的 UTF-8 尾字节在 GBK 下是悬空前导字节，
    会吞掉后面的英文引号，引发连锁解析错。**根治：本 skill 的 ps1 一律带 UTF-8 BOM**
    （V1.72.4 起三个脚本已加；edit 工具改 BOM 文件会保留 BOM，改完用字节头复查）。
    输出中文仍由被调用的 C# 程序打印（脚本里设 `[Console]::OutputEncoding = UTF8`）。
10. **管道捕获中文显示残缺不影响判定**：通过 bash/管道转发时控制台编码仍可能花屏，
    但 PASS/FAIL/ALL PASS/退出码始终可靠，以它们为准。
11. **集成测试必须给 Fake 气压表设初始读数**：DeviceManager 有"连续 N 次读失败→失联报警"
    防呆（30ms 采集间隔下 ~90ms 即触发），Fake 台返回 null 会抢先报警关阀，
    盖过要测的场景。BuildTestManager 里先给全部台 SetPressure(0)（常压）。
12. **状态机"完成/报警轮"的广播数据会被旧分支覆盖**（V1.59 实测抓出的产品 bug 类别）：
    CollectData 里 data.Status 在状态分支时赋值，而完成/报警动作发生在其后的
    ProcessTestingProgress/HandleAlarm——本轮末尾批量写缓存会用旧值冲掉刚写入的
    Completed/Fault 标记，面板"闪一帧即逝"。修法是动作执行后同步修正本轮 data。
    **教训：同一轮内"先定状态、后改状态"的流水线，动作后必须回写广播对象。**
13. **布尔传参语义反转只有集成测试能抓到**：IsVacuumBuildFailed(是否到位) 被传入
     PressureOutOfRange(是否越限)——两者互为反义，单测用正确语义的字面量全绿，
     端到端一跑"真空建立超时永不报警"。**教训：谓词函数做参数传递时在调用点写
     `!pressureAlarm` 并加注释说明取反原因；新状态机必须有端到端用例兜底。**
     V1.62 已把两处私有判定收拢为 `AgingSequencer.IsPressureOutOfRange` 唯一口径，
     此类"改一处漏一处"从机制上消灭。
14. **测试场景顺序不能让"Dispose 了的对象"继续被后续场景使用**：断电恢复场景会
     Dispose 主 DeviceManager，必须放在所有依赖它的场景之后，否则后面"启动不上电"
     这类灵异失败其实是采集定时器已被停掉。
15. **Fake 与真实现的口径差要在用例注释里写明**：FakeIoController 输出数组按全局
     编号 sizing（81~240 共 240），而真实现返回 TotalOutputs（160）——GetAllOutputs
     长度断言按 Fake 口径写，别跟真实现搞混。以后 Fake 与真实现行为分叉时，
     优先修 Fake 对齐真实现（如 MockFan 未连接启停），修不了的在用例里注明。
16. **在线数 10s 窗口只测 happy-path**：GetOnlineCount 的过期分支要等 10s，
     整窗等待太慢不做；用"启动前在线 0 / 启动后在线全满"覆盖计数两边，
     过期语义靠 _lastGoodTimes 写入逻辑（每次成功读数刷新，CollectData 内一行）
     的代码审查保证。
17. **新用例引用新程序集要同步改编译脚本**：UiPureHelpers 直接 new SunnyUI 派生窗体
    后，harness 编译报 CS0012（UIForm 基类未引用）——在 run_unit_tests.ps1 的
    csc 参数里加 `/r:$runDir\SunnyUI.dll` 解决（dll 随产物拷贝，不用装 SDK）。
    以后用例用到 NModbus 等类型同理。
18. **运行时文件改道后老用例的字面路径全失效**（V1.67：Recipes/StationSettings/
    HomeLayout 改走 `ProjectProfile.ResolveDataPath` 进 `Projects/<项目>/`）：
     symptom 是"保存后文件存在/损坏回退"类用例批量红。修法是把用例里的字面量
    同步改成 ResolveDataPath 调用（BaseDirectory 即 run 隔离目录，隔离性不变）。
    **教训：改存储路径必须 rg 全仓扫字面文件名**（docs/CHANGELOG 历史条目除外）。
19. **快照字段与写入时机要一起加**（V1.67：TestSessionStation 加 Phase/PowerOnTime
    后，P4 用例仍红——SaveSessionSnapshot 只在启动/完成/报警时落盘，上电边沿
    没写，快照 Phase 恒为 Vacuuming）。修法是在上电边沿补一次快照。
    **教训：给快照加字段时，把"哪些状态变迁会写快照"列一遍，变迁与落盘要对齐。**
20. **老式 csproj 新文件必须手工登记**（V1.67：PolicyEnums/ProjectProfile/
    ProjectPolicyStore/UnloadJudgeForm/ProjectSwitchForm 加完编译报 CS0246 找不到类型）。
    修法是在 csproj 的 Compile Include 里补 5 行（纯代码窗体用 SubType Form 即可，
    无需 Designer/resx）。**教训：write 新 .cs 后先查 csproj 有没有通配，
    没有就地登记再编译。**
21. **静态传输缝的测试必须 finally 复位**（V1.68：MesReporter.Transport 是进程级静态，
    Fake 赋值后若不断言 finally 置 null，后续模块/下次运行会继续走 Fake 抓包，
    表现为"生产代码不发包"的灵异失败）。**教训：进程级测试缝一律 try/finally 复位，
    离线缓存文件同理删干净。**
22. **后台线程的用例必须轮询等待**（V1.68：MesReporter 入队后后台线程 5s 内发送，
    直接断言会抢跑）。修法是 WaitFor 轮询（50ms 步进）+ 宽超时；入队→signal 实时唤醒，
    实际几百毫秒即到，宽超时只防 CI 抖动。**教训：凡涉后台线程/定时器的断言一律轮询，
    不写 Sleep 硬等（硬等要么 flaky 要么慢）。**
23. **char.IsLetter 认中文，HTTP 头名校验必须手写 ASCII 范围**（V1.68：自定义头名
    "中文头"误放行，用例红）。修法是 IsAsciiLetter（A-Z/a-z/0-9/-/_/.）。
    **教训：凡"协议层字符集"（HTTP 头、URL、串口关键词）一律按 ASCII 白名单写，
    别用 .NET 的 Unicode 字符分类。**
24. **表达式引擎一次写对的关键：短路语义先定再写测试**（V1.69：`&&` 短路用例把
    "值false"当"出错"断言，红；引擎本身是对的——`||` 对照组绿即证明）。
    **教训：短路/惰性语义的用例必须把"值断言"和"无错断言"分开写（evalErr==null
    且 eval==期望值），混在一起红了都不知道哪边错。**
25. **PowerShell 变量大小写不敏感，局部变量别撞参数名**（V1.72.4：
    build_and_test 里 `$affected = & ...` 给 `[switch]$Affected` 赋值，
    炸"无法将 String 转 SwitchParameter"，定位花了三次二分）。
    **教训：开关叫 `-Affected`，局部变量就叫 `$affectedResult`；
    凡 param 块有名，函数体内禁用其大小写变体。**
26. **ps1 的 param 块必须是第一条语句**（V1.72.4：`$ErrorActionPreference` 写在
    param 前面，报"无法将 param 识别为 cmdlet"）。注释可以放前面，代码不行。
27. **harness 里调 SwitchTo 会写运行目录的 TestRunner.exe.config**
    （V1.72.10：SwitchTo 成功即改指针+刷 appSettings 缓存，进程级副作用）。
    用例必须 try/finally 切回原项目、删掉 tmp 目录、再 `StationSettingsCache.Reload()`
    指回原项目，否则后模块读到脏指针+脏缓存，红得莫名其妙。
    **教训：凡改进程级状态（config 指针/静态缓存/静态传输缝）的用例，
    一律 finally 三件套：指针恢复 + 文件清理 + 缓存重载。**
28. **动态 Sunny 控件重建必须先 Dispose 再 Clear，否则终结器线程跨线程崩溃**
    （V1.72.12：驾驶舱右栏 `Controls.Clear()` 只摘不放，旧 UITextBox 进终结器
    Dispose，Sunny 内部读原生 TextBox.Handle 即炸，堆栈终点
    `TextBox.ResetAutoComplete←Dispose←Finalize`，控件 Name 全空是特征）。
    **教训有三**：①凡 `Controls.Clear()` 摘动态控件处一律先逐个 Dispose
    （主窗 H5 早有先例，漏网必炸）；②跨线程错 Name="" + 时机随机（看 GC）
    + "报错时正在干别的事"三特征齐了先查 Clear；③没有用户堆栈不硬修跨线程
    bug——harness（Mock 采集+双窗+17 轮模拟拖动）常规路径零异常，
    定罪全靠堆栈。    用例锁"重建后旧控件 IsDisposed"（反射调两次 RebuildEditors）。
29. **非模态 Form.Close() 不释放窗体，三编辑弹窗是第二案发现场**
    （V1.72.13：修完驾驶舱用户现场又炸，同堆栈——全仓排查 Designer/modal/
    using/测试窗全有释放，只剩设置窗 IP/IO/规则三 popup 的 FormClosed 只回写
    不 Dispose）。修法是 handler 里 `finally { popup.Dispose(); }`（先回写再释放）。
    **用例必须是真回归**：裸 `new popup→Show→Close` 测的是框架行为（恒绿假绿），
    必须反射调生产 `ShowXxxPopup` → `Application.OpenForms` 按类型找窗 →
    Close → IsDisposed（修前 false、修后 true）。
30. **窗体没 Show 时 Control.Visible 读恒 false，显隐断言改测纯函数**
    （V1.73：工位窗"有阀破空按钮显示"用例红——按钮 Visible=true 已赋值，
    但父窗体从没 Show，getter 照样 false）。修法是显隐条件抽
    `internal static ShouldShowXxx(config)` 纯函数，构造只调它，用例测它
    （无阀/false/空三态 + 有阀 true）。**教训：harness 里凡断言"看不看得见"，
    先问窗体 Show 了没——没 Show 就别读 Visible。**
31. **Designer 里没放过 ToolTip 类控件的窗，components 容器是 null**
    （V1.73：BatchRecipeForm 挂 tooltip 用 `new ToolTip(this.components)`，
    构造即炸 ArgumentNullException，连带三个模块级异常）。
    修法是 SetupTooltips 开头 `if (components == null) components = new Container()`，
    后续 Dispose 照走容器。**教训：用 `this.components` 前先看 Designer 有没有
    `new Container()` 那行，没有就自己补（StationSettingsForm 有，Batch 没有）。**
    （V1.74 追认：RecipeManagerForm 同病，构造 tooltip 前同样补建容器。）
32. **用例文件尾部追加新模块，大括号失衡先看插入点上下文**
    （V1.74：新模块插到文件尾，编译报一串 CS1519/CS1022——尾部多了个孤儿
    `finally+}`，原因是多行 edit 的 oldString 在尾部误匹配/复写）。
    修法是插完先读尾部 30 行数括号，编译不过先看尾。**教训：文件尾是 edit
    工具最容易吞行的地方（与 V1.71 的 Designer 27 行块吞 3 个 new 同类），
    大块追加后必做"尾部复读 + 立即编译"。**
33. **OpenXml 的 `Elements<T>().Count()` 要 `using System.Linq`**
    （V1.74：照抄 ID 绑定窗样式表代码到历史窗，CS1061——源文件有 Linq，
    目标文件没有）。**教训：跨文件抄套路时把 using 一起抄，编译第一个错
    先看缺 using。**
34. **CSV 加一列，表头/行尾/互逆三处断言必改**
    （V1.74：TestLog 加电流列，TestEventLoggerTests 的表头断言、
    两处 EndsWith 行尾断言、HistoryCsvTests 的互逆列数 7→8 全红）。
    修法是改前 rg 全仓扫旧表头字面量（测试+注释里的列格式说明一起改）。
    **教训：CSV 列是"写入器+解析器+断言"三方契约，加列=三方同步。**
    （V1.76 升级：测试 helper 里手写 `cols[2]/cols[3]` 数列的（如
    `CountAlarmLines` 按旧列序数报警行）同样是契约——红的不是产品代码，
    是数错列的 helper。以后 CSV 加列，rg 必带 `cols[` 一起扫。）
35. **DataGridViewComboBoxColumn 没有 DropDownStyle 属性**
    （V1.75：照 WinForms 直觉写上限死，CS0117——那是 ComboBox 控件的属性，
    列没有）。修法是删行 + Confirm 里按 vocabulary 归一校验（下拉选的天然合法，
    手输的拦）。    **教训：列级控件与窗体控件 API 不通用，抄之前先看成员列表。**
36. **harness 编译报新类找不到，先看主程序产物新不新**
    （V1.75：新弹窗文件建于上次构建之后，run_unit_tests 直接 csc harness，
    引的是旧产物）。修法是先 MSBuild 再跑（build_and_test 自带构建则无此坑）。
    **教训：单独跑 run_unit_tests 前先确认产物包含新文件。**
37. **历史窗 Designer 声明了 components 但从未赋值，同样是 null**
    （V1.75：历史窗 Designer 有 `IContainer components` 声明，InitializeComponent
    里却没 `new Container()`——从没放过组件类控件，VS 就不生成那行）。
    现象与 Batch/RecipeManagerForm 一字不差（`new ToolTip(null)` 构造即炸）。
    **教训升级：判据不是"有没有声明"，是"InitializeComponent 里有没有
    `new Container()` 那行"——以后挂 ToolTip 前 rg 那行，没有就补。**
38. **未 Show 窗体的 Visible 读恒 false，Fill 守卫改认 bool 字段**
    （V1.75：显示模式隐藏态 Fill 读 `cmb.Visible` 做守卫，结果开关开也填不进项，
    三窗下拉全空——Visible=true 刚赋上，父窗没 Show，读回还是 false，
    与 #30 同根）。修法是构造时把开关结论存 `readonly bool` 字段，
    Fill/回填只认字段。**教训：#30 的"别读 Visible"不仅是用例，将生产代码
    也算上——运行时未 Show 前读 Visible 同样撒谎。**
39. **锚定解析顺序错只在"改面板尺寸"时现形，缺省尺寸全绿是假安全**
    （V1.77：SN 排在压力框后，压力框双端 X 读到 SN 旧 X——缺省宽 222 下
    缺省值恰好正确，全部旧断言照绿，只有"宽度+10"红）。
    修法是被依赖者先解（SN→压力框），手改间隙的场景再终解一次 SN 兜底。
    **教训：改 ResolveElementAlign 顺序后，必须跑宽/高双向联动用例
    （只看缺省坐标等于没测）；新链路形式的断言（如交接缝间距）要同步加，
    否则下次调序又靠运气。**
40. **弹窗失焦/布局预计算语义不用真 Show 也能进回归**
    （V1.81.1~3：`OnDeactivate` 失焦守卫反射直调（设旗/不设旗各一次）；
    自绘布局 `SetData→ComputeLayout→FitRows` 全是纯数学+内存 DC 量字，
    无句柄无弹窗，runner 里安全；`Invalidate` 无句柄是空操作不建句柄）。
    **教训：只有"真 Show 真点真截图"（模态 Timer 关窗、真屏深色像素计数）
    才留临时 harness；凡反射+内存 DC 够得着的语义，一律沉淀进 TestRunner，
    别以"UI 测不了"为由只留 harness。**
41. **Sunny 下拉/按钮不是原生 ComboBox/Button 子类，as-cast 静默 null**
    （V1.85：预置行 UI 断言 `as ComboBox` 全红 6 条——UIComboBox/UIButton
    不是原生子类，只有 UILabel 是 Label 子类无碍；产品 bug 是零，纯用例写法错）。
    修法是一律 `as Control` + 反射读 `Items`（转 IList 数 Count）/
    `SelectedIndex`。**教训：AGENTS"is Button 认不出 Sunny"的铁律，用例里
    同样要守——WinForms 直觉的亲缘判断在 Sunny 自绘控件上失效，断言前先看
    继承链；且这类"构造成功但断言全红"先查 cast 再查产品。**
42. **PS5.1 里 @() 最后一个哈希表后禁尾逗号**
    （V1.87.1：get_affected_modules.ps1 的 $Map 末条带尾逗号，整本报
    MissingExpression，-Affected 从 V1.72.4 起就没跑通过，HEAD 版同样红——
    改动前即坏，最小复现 @(@{...}, ) 红、去逗号绿）。**教训：ps1 数组末条
    永远不加尾逗号；改完跑一遍解析器（parsecheck 探针）再收工。）
43. **powershell -File 传数组只认首个、其余静默丢弃**
    （V1.87.1：-Files @('a','b') 在 -File 下 Count=1（showparam 探针实测），
    脚本侧无从察觉=静默漏测）。修法是多文件点测改传单串分号式
    （-Files 'a.cs;b.cs'，脚本内拆分）；& 调用传真数组不受影响。
    **教训：凡"只测了第一个文件"的子集绿，先查参数是怎么传进来的。）
44. **后台定时器用例禁固定 Sleep＋短超时，违者满负载必飘红**
    （V1.88.20：P3 失压策略全量跑红“边沿两条”/单跑绿——30ms 采集在满负载下
    饿死数秒，Sleep(600)+2 秒超时全不够；修法三件套：①刺激-等待-断言按
    可观测前提排队（等上电→等相位落定→等消费→等边沿，WaitUntil 轮询）；
    ②超时按饿死量级给（15 秒），不按快乐路径给；③状态断言放边沿之后读稳态
    （恒定输入下保持运行恒 Testing、报警锁存 Fault，瞬态读是赌博）。
    **教训：#22“后台断言一律轮询”再加半句——轮询的前提本身也要可观测。**
45. **等待谓词必须用“刺激后才出现”的值，否则等待瞬间通过（空证明）**
    （V1.88.20 血泪：消费证明写“缓存==0”，而初始全 0——等待 0ms 通过，
    紧接着读到启动首采的初始缓存 Idle 即红，比 Sleep 版还脆。
    改掉压值 0→-1（同样越限，刺激前不可能出现），等待才非空。
    **教训：写 WaitUntil 前先问“这个条件在刺激前成不成立”——成立即空证明，
    换刺激后唯一的指纹值；读状态前先问“机器收敛了吗”，没收敛的读数是瞬态。**
46. **弹窗 OpenForms 枚举必须轮询＋重开，失焦自杀守卫会吃窗体**
    （V1.88.20：IO 映射弹窗全量跑红“已弹出”/单跑绿——五个编辑弹窗全带
    OnDeactivate 失焦自杀（生产正确行为，不动产品），Show/Activate 间隙被
    焦点切换抢占即自杀，单次 DoEvents 后枚举正撞枪口。修法：
    PollOpenFormByType 轮询 1.5 秒＋找不到重调一次同一生产入口；
    真坏了（抛异常/静默无窗）两次都找不到照样红，不掩盖故障）。
    **教训：凡“单跑绿全量红”的 UI 用例，先查被测对象有没有自杀/自隐逻辑，
    再怀疑环境。**
47. **新加固的用例必须做双向反向验证，否则加固本身可能是假绿**
    （V1.88.20：P3 改缺省政策跑→4 行为红 3 前提绿（定向爆破，证明测的是政策）；
    弹窗首查改不存在类型→重开分支救回全绿（证明分支活着），重开也改不存在
    类型→5 个“已弹出”精准红（证明不掩盖真故障）。三次 revert 后才收工。
48. **P3 抖动再现（2026-09-15）：全量偶发红 3 条 P3 边沿/刷屏，零改动立即重跑即绿**
    （判据：两次运行之间无任何代码改动＋第二次 1874 全绿=环境抖动，不是回归；
    不要追着改产品/用例，重跑确认即可。若连续两次红才按真故障查）。
    **教训：#38 反向验证铁律同样管用例加固——一次绿可能是等出来的，
    定向爆破红一次才算数。**
