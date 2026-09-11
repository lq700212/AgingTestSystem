---
name: agingtest-regression
description: AgingTestSystem 项目专属的最终测试验证技能：一键完成"构建 → 真机冒烟测试 → 全量回归测试用例"。回归 harness 覆盖 PasswordHasher/UserManager 登录权限/配置归一化/IO 映射解析/配方存储/双日志器/面板布局锚定联动/工艺策略/项目档案等全部核心逻辑类（918+ 断言）。当用户要求"跑测试、冒烟测试、回归验证、测一遍、发布前验证、改完代码验证一下"或修完 bug/加完功能需要验证时使用；新增测试用例也必须沉淀到本 skill 的 tests/TestRunner.cs 中。
---

# AgingTestSystem 回归测试套件（冒烟 + 用例一体）

本项目（WinForms/.NET Framework 4.x）**没有单元测试框架**，本 skill 用
"csc 编译独立 harness + 自研 Check 断言"的方式实现可离线自动化的全量回归，
是项目的**最终测试验证手段**。UI 像素级问题另见全局技能 `winforms-ui-debug`（两者分工：
本管逻辑正确性，那管视觉正确性）。

> 开工前先读 `AGENTS.md`（UTF-8 编码、改后必构建、文档同步等红线全部适用）。

## 一、快速用法

```powershell
# 一键全跑：构建 → 冒烟(真机 exe 存活) → 全量回归用例（最常用）
powershell -ExecutionPolicy Bypass -File ".opencode\skills\agingtest-regression\scripts\build_and_test.ps1"

# 只冒烟（已构建好的 exe 启动存活 18s 判定；设备连接超时导致真实启动要 10~15s）
powershell -ExecutionPolicy Bypass -File ".opencode\skills\agingtest-regression\scripts\smoke_test.ps1"

# 只跑回归用例（不重新构建，引用 bin\Debug 现有产物）
powershell -ExecutionPolicy Bypass -File ".opencode\skills\agingtest-regression\scripts\run_unit_tests.ps1"
```

判定标准：退出码 0 = 全绿；输出末尾 `PASS = n FAIL = 0 / ALL PASS`。
失败时会打印每条失败断言的名称与实际值明细。

## 二、目录结构与职责

```
agingtest-regression/
├── SKILL.md                  ← 本文件（套路与踩坑沉淀，改完必读必更新）
├── scripts/
│   ├── build_and_test.ps1    ← 一键流水线（构建→冒烟→回归），退出码 1/2/3 区分阶段
│   ├── smoke_test.ps1        ← 冒烟：启动真 exe → 轮询存活 → Stop-Process
│   └── run_unit_tests.ps1    ← 拷贝产物到 %TEMP% 隔离 run 目录 → csc 编译 harness → 运行
└── tests/
    └── TestRunner.cs         ← 全部测试用例源码（加用例就改这里）
```

## 三、测试覆盖范围（30 个模块，918+ 断言）

| 模块 | 覆盖点 |
| --- | --- |
| PasswordHasher | PBKDF2 格式自描述、盐随机性、正反例、损坏串静默失败、中文/超长密码 |
| UserManager | 默认账号、登录边界（空值/Trim/大小写/角色错配）、AddAccount 唯一性与保底、RemoveAccount 保护、改密链路、HasPermission 权限矩阵、记住登录往返、Users.json 损坏回退重建、缺角色自动补齐、手改双管理员防呆；V1.64 起 dev 最高权限 J 组 34 条（种子+隐藏登录+注册保护不可用+删改矩阵+老文件自愈+手改保留 dev+第一业务管理员） |
| SettingsForm.Normalize | StopBits(1/15/2) 与 Parity(None/Odd/Even/Mark/Space) 归一映射（反射调私有静态方法），含中文/缩写/非法值兜底 |
| IoOutputChannelRemap | 多组解析、中英文分号/箭头、0X 大小写、脏项跳过汇总 error、源=目标、通道 0x00~0x0F（V1.62 起 0x10+ 直接拒绝）、缺前缀 |
| DeviceConfig.ParseFanIpCandidates | 中英文分隔符、非法过滤、去重保序、IPv6、空输入族 |
| RecipeStorage | Load/Save 往返全字段、损坏 json 返 null、空数组、"null"字面量、SaveWithDuplicateCheck 新增分支、删中间配方后 Max+1 不撞号(V1.62)、DisplayMode 往返(V1.66) |
| TestEventLogger | CsvEscape 转义（逗号/引号翻倍/换行/回车 V1.62）、表头、落盘字段格式、null 字段 7 列、温度一位小数、20×5 并发零丢失、删目录自重建 |
| AppLogFileWriter | UTF-8 追加、空串忽略、8 线程×5 行并发一条不少（lock 生效） |
| PanelLayoutConfig | 默认布局基准坐标、ResolveAnchors 幂等零漂移、高度+10 纵链全链跟随、宽度+10 右锚定组随动、颜色解析钳位/回退、SaveDefault→重载零差异 |
| HomeLayoutConfig | 默认值、Save→Load 往返、范围约束、损坏文件回退默认 |
| ModelRoundtrip | RecipeConfig/UserAccount JSON 往返（含特殊字符）、StationInfo/FanData Clone 深拷贝互不影响 |
| AgingSequencer | ShouldPowerOn(压力×延时双条件)、ShouldComplete(0=不限时长)、IsVacuumBuildFailed(到位即不失败) 边界族、IsPressureOutOfRange 双方向+恰等不越限(V1.62)、负时间语义锁、BuildStartWarningText 0时长/空SN 警告文案(V1.66)、IsFanOverTempShutdown 开关+上限+边界(V1.66) |
| TestSessionStore | 快照往返全字段、损坏 json 静默 null、Clear 幂等、空清单视为无任务、Stations:null 与"null"字面量、Save(null)=false |
| AgingBusinessModel | DeviceStatus.Completed 枚举与 BarometerData 往返、LastTestResult 默认值/Clone、AgingPhase 三值、StationInfo.RecipeNegativePressure |
| ThemeManager | Parse 大小写/空格兼容与乱写兜底浅色、双向映射表往返精确（容器底/文字/单元格/输入底，V1.62 补齐剩余分支）、语义色保留（红/绿不动）、SetMode 内存切换、Panel+Label+TextBox+Button+DataGridView 整树着色冒烟（STA harness 直接 new 控件不断言弹窗；注意 Label/Button 的 Fore/Back 地 getter 在 Empty 时返回父容器值，断言要写"跟父一致"而非具体值，见 TestRunner 注释） |
| **DeviceManagerIntegration** | **端到端状态机**（Fake 气压表+Fake IO 经注入构造驱动真实 DeviceManager，30ms 采集秒级跑完生命周期）：正常全流程(启动只开阀→到位+延时上电→配方时长完成→Completed·PASS→阀电全关)、真空建立失败(超时报警+全程不带电+FAIL)、通讯失联(设备异常≠FAIL)、手动中止(回空闲不计结果)、断电恢复(快照落盘→重启询问→整台重测/放弃关阀)、扫码重绑清完成态、配方阈值优先于全局 |
| IoMapBuilder(V1.62) | 八进制编址(X000/X007/X010/Y110/Y217)、预留点、非法四抛、编号公式、兼容重载 |
| MockDevices(V1.62) | 三 Mock 未连接约定/越界/副本隔离、气压两档区间千次采样、风机启停守卫与漂移界 |
| StationCache(V1.62) | 往返全字段、覆盖语义、副本双向隔离、脏文件三态、非法编号过滤（反射重置静态缓存+隔离目录） |
| ModelDefaults(V1.62) | DeviceConfig 全构造默认值、风机枚举寄存器值、FanData/BarometerData Clone 全字段与数组深拷贝、LoginResult 工厂、角色值、快照与配方构造默认 |
| SettingsValidate(V1.62) | ValidateValue 全类型矩阵、TryParseUShort、范围表抽查+默认值落界、布尔键一致、连接键契约、CreateValueCell 全分发、分类/说明键对齐（构造真窗体不断言弹窗） |
| ScannerParse(V1.62) | JoinPorts、ParseParity/ParseStopBits、与设置窗 NormalizeStopBits 跨文件 15 口径 |
| ModbusConvert(V1.62) | 气压/阈值换算纯函数、IsPortLevelFailure 中英文关键字、未连接约定、串口参数解析 |
| FanParse(V1.62) | 寄存器解析(/100 全字段)、不足 6 个、非法枚举透传、未连接约定、Connect(null) |
| StationTime(V1.62) | 时分秒组合、25 小时不截断(V1.62 修复锁)、超 99 钳制、文本格式、Clamp |
| HistoryCsv(V1.62) | CSV 解析边角、与 TestEventLogger 互逆 7 列 |
| UiPureHelpers(V1.62) | 批号去空格、配方查找(ignoreCase)+25h 不截断、工位温度读取(V1.63 数字框恒合法+回填钳制)、IP 合法、数字格钳制、网格命中/边界/四色、位值→通道、风机中文(V1.63 对齐主窗)、CH340 谓词/串口参数钳制(V1.63)、右侧宽度比例 ComputeRightPanelWidth(V1.65：0.234 常量/护栏/兜底/自定义优先 8 条)、配方窗负压/显示模式框回填(V1.66) |
| **DeviceManagerExtended(V1.62)** | 状态口/在线数/启动错误、批量 SN、配方名负压联动、副本隔离、非法电池、连接与间隔热生效、批量阈值+定时器恢复、反方向报警端到端、全局时长回退、定格隔离、清理回全局、不限时、2s 延时门、空闲容错、自愈计数、报警驻留、边沿单次(CSV 计数)、快照全字段+双台+批号、急停、停止再启动、风机生命周期(MockFan)、超长数组与错 id 防火墙、脏快照恢复、显示模式下发/保持/清空+叠加采集可见+GetTestingDeviceIds(V1.66) |
| PolicyV167(V1.67) | BuildStartBlockText 阻断文案、MapAlarmResult 责任映射、ComputeResumeDuration 剩余/跑超/回拨、ValidatePolicyCombination 矛盾锁、ParseValue 大小写/非法、PolicyKeys↔DeviceConfig↔下拉选项三处同步锁、DeviceConfig 缺省=现状锁、快照新字段缺省锁、ValidateValue 策略分支+点位、NormalizePolicyValue 脏值兜底、WrapTooltip 40字换行、ProjectProfile 非法名/重复/切换拒绝/路径分流、Policy.json 存取往返 |
| **DeviceManagerPolicy(V1.67)** | 治具责任端到端(装夹异常+CSV)、待判定完成+下料录入(收/跳过/null)+CSV明细、失压保持(不停机+边沿单条不刷屏)、续跑(快照阶段/上电时刻+剩余60s+重抽真空)、泄压(破空阀开+CSV+复位关阀不残留) |

**不在覆盖范围**（明确边界）：真串口/真设备通讯（ModbusRtuBarometerReader /
ScannerService / FanControllerClient / ModbusTcpIoController，靠现场联调）、
UI 弹窗分支（如配方同名覆盖确认框，靠界面手工测试）、像素级渲染（走全局技能 winforms-ui-debug）。

## 四、怎么加测试用例（铁律：改代码必同步补用例）

1. 打开 `tests/TestRunner.cs`，找到对应模块的 `XxxTests()` 方法；
2. 加一行 `Check("用例名(说清预期)", 条件, "可选失败详情");`
   - 异常也是预期行为时用 `CheckThrows<TEx>("名字", () => ...)`；
   - 新模块就写 `private static void XxxTests()` 并在 `Main()` 里挂一行 `Module("名字", XxxTests);`
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
9. **PowerShell 5.1 对无 BOM 的 ps1 按 ANSI 解析**，中文字符串/注释会乱码甚至语法错。
    本 skill 的 ps1 一律纯 ASCII 英文内容；中文输出统一由被调用的 C# 程序打印
    （脚本里设 `[Console]::OutputEncoding = UTF8`）。
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
