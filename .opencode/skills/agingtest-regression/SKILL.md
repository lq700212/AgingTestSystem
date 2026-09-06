---
name: agingtest-regression
description: AgingTestSystem 项目专属的最终测试验证技能：一键完成"构建 → 真机冒烟测试 → 全量回归测试用例"。回归 harness 覆盖 PasswordHasher/UserManager 登录权限/配置归一化/IO 映射解析/配方存储/双日志器/面板布局锚定联动等全部核心逻辑类（246+ 断言）。当用户要求"跑测试、冒烟测试、回归验证、测一遍、发布前验证、改完代码验证一下"或修完 bug/加完功能需要验证时使用；新增测试用例也必须沉淀到本 skill 的 tests/TestRunner.cs 中。
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

## 三、测试覆盖范围（16 个模块，345+ 断言）

| 模块 | 覆盖点 |
| --- | --- |
| PasswordHasher | PBKDF2 格式自描述、盐随机性、正反例、损坏串静默失败、中文/超长密码 |
| UserManager | 默认账号、登录边界（空值/Trim/大小写/角色错配）、AddAccount 唯一性与保底、RemoveAccount 保护、改密链路、HasPermission 权限矩阵、记住登录往返、Users.json 损坏回退重建、缺角色自动补齐、手改双管理员防呆 |
| SettingsForm.Normalize | StopBits(1/15/2) 与 Parity(None/Odd/Even/Mark/Space) 归一映射（反射调私有静态方法），含中文/缩写/非法值兜底 |
| IoOutputChannelRemap | 多组解析、中英文分号、0X 大小写、脏项跳过汇总 error、源=目标、通道越界、缺前缀 |
| DeviceConfig.ParseFanIpCandidates | 中英文分隔符、非法过滤、去重保序、IPv6、空输入族 |
| RecipeStorage | Load/Save 往返全字段、损坏 json 返 null、空数组、SaveWithDuplicateCheck 新增分支 |
| TestEventLogger | CsvEscape 转义（逗号/引号翻倍/换行）、表头、落盘字段格式 |
| AppLogFileWriter | UTF-8 追加、空串忽略、8 线程×5 行并发一条不少（lock 生效） |
| PanelLayoutConfig | 默认布局基准坐标、ResolveAnchors 幂等零漂移、高度+10 纵链全链跟随、宽度+10 右锚定组随动、颜色解析钳位/回退、SaveDefault→重载零差异 |
| HomeLayoutConfig | 默认值、Save→Load 往返 |
| ModelRoundtrip | RecipeConfig/UserAccount JSON 往返（含特殊字符）、StationInfo/FanData Clone 深拷贝互不影响 |
| AgingSequencer | ShouldPowerOn(压力×延时双条件)、ShouldComplete(0=不限时长)、IsVacuumBuildFailed(到位即不失败) 边界族 |
| TestSessionStore | 快照往返全字段、损坏 json 静默 null、Clear 幂等、空清单视为无任务 |
| AgingBusinessModel | DeviceStatus.Completed 枚举与 BarometerData 往返、LastTestResult 默认值/Clone、AgingPhase 三值、StationInfo.RecipeNegativePressure |
| ThemeManager | Parse 大小写/空格兼容与乱写兜底浅色、双向映射表往返精确（容器底/文字/单元格/输入底）、语义色保留（红/绿不动）、SetMode 内存切换、Panel+Label+TextBox+Button+DataGridView 整树着色冒烟（STA harness 直接 new 控件不断言弹窗；注意 Label/Button 的 Fore/Back 地 getter 在 Empty 时返回父容器值，断言要写"跟父一致"而非具体值，见 TestRunner 注释） |
| **DeviceManagerIntegration** | **端到端状态机**（Fake 气压表+Fake IO 经注入构造驱动真实 DeviceManager，30ms 采集秒级跑完生命周期）：正常全流程(启动只开阀→到位+延时上电→配方时长完成→Completed·PASS→阀电全关)、真空建立失败(超时报警+全程不带电+FAIL)、通讯失联(设备异常≠FAIL)、手动中止(回空闲不计结果)、断电恢复(快照落盘→重启询问→整台重测/放弃关阀)、扫码重绑清完成态、配方阈值优先于全局 |

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
14. **测试场景顺序不能让"Dispose 了的对象"继续被后续场景使用**：断电恢复场景会
    Dispose 主 DeviceManager，必须放在所有依赖它的场景之后，否则后面"启动不上电"
    这类灵异失败其实是采集定时器已被停掉。
