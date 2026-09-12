# CHANGELOG

> 精简版改动历史（最新在前）。只保留有维护价值的功能/修复要点；细微 UI 调整不重复记录。
> 详细上下文可查 git 历史。协议/寄存器类改动同时已同步到 [`docs/通讯接入.md`](docs/通讯接入.md)。

## V1.72.11 — 项目切换窗加"删除项目"（2026-09-12，用户点名）

### 改动范围
- `Services/ProjectProfile.cs` 新增 `DeleteProfile`（删非当前项目整个目录；
  空名/当前项目/不存在一律 false，当前项目先切走再删，删别的项目在测也可）。
- `Dialogs/ProjectSwitchForm.cs` — 操作行一排三按钮（切换并生效/删除项目/关闭，
  各 120 宽），删除走二次确认（带项目名防手滑），删完只刷新列表（顶栏/数据不动，
  无需热加载）；头部 ASCII 图与规则注释同步。

### 为什么这么改
- 用户要亲手验证热更（A 建配方改策略→切 B→切回看隔离），空项目看不出效果；
  验证完测试项目必须能删掉，否则列表留垃圾。之前只有新建/切换，没有删除口。

### 验证
- `build_and_test.ps1`（用例改动兜底全量）：构建 + 冒烟（存活 18s）+ **1220 全绿**
  （1211 + 9：删不存在/空名被拒、可建、切入、当前禁删、切回、删除、列表干净、指针不变）。

## V1.72.10 — 项目切换热更免重启 + Default 改干净（2026-09-12，用户点名）

### 改动范围
- 项目切换免重启（热更）：`Dialogs/ProjectSwitchForm.cs` — 按钮"切换并重启"→
  "切换并生效"，删 `Application.Restart` 重启确认框，成功带回 `SwitchedProjectName`；
  `Views/MainForm.cs` 新增 `ReloadActiveProject`（在测复查→暂停主采集→
  `LoadConfig` 重读新项目 Policy 叠加→`DeviceConfig.CopyFrom` 就地换血→
  `StationSettingsCache.Reload`→`DeviceManager.ClearProjectScopedState`→
  `LoadRecipes`→`ApplyHomeLayout`+顶栏刷新→恢复采集+立即刷一帧，失败提示重启兜底）。
  硬件连接不断（串口/耦合器/送风机/扫码枪跟机器），在测禁切保留。
- 新增热更支撑：`DeviceConfig.CopyFrom`（反射全量拷，_config 是 readonly 引用，
  就地换血持别名的 DeviceManager/MES 才都生效）、`StationSettingsCache.Reload`、
  `DeviceManager.PauseCollection/ResumeCollection`（只停主采集，送风机不动）+
  `ClearProjectScopedState`（清旧工位指派+规则计时，不清压力缓存、不碰硬件，
  区别于 StopAll 的急停语义）。
- Default 改干净：`ProjectProfile.CleanupLegacyDefault`（启动幂等：正主不在则
  Default 整体改名保数据，正主在则补拷独有文件后删除，重名不覆盖；清不掉记日志
  下次再试）+ 缺省名收拢为 `DefaultProfileName` 常量；`README` 的 ActiveProject
  缺省/菜单行为、App.config 指针注释、两处 Designer/MainForm 注释同步。
  本地 `bin/Debug/Projects/Default` 空目录已删，构建产物 exe.config 已是烧屏测试。

### 为什么这么改
- 用户体验：每次切项目重启（等设备超时 10~15s + 重进登录）太烦；项目未上线，
  无需兼容"重启生效"老习惯，一步到位热更。
- 根因：跟项目走的四份数据启动时一次性进内存，旧逻辑只改指针不换内存，
  不重启必读劈叉——热更就是把启动加载原样重放一遍（暂停采集防半新半旧）。
- Default 阴魂不散：V1.72.9 只改了缺省名，老版本跑过的机器上空 Default 目录还在，
  列表照样能看到；启动自愈保证"列表里永远没有 Default"。

### 验证
- `build_and_test.ps1`（用例改动兜底全量）：构建 + 冒烟（存活 18s）+ **1211 全绿**
  （1188 + 23：热更指针往返 12、Default 自愈 3、CopyFrom 4、清状态+暂停 4）。
- 待现场手工：参数设置→项目切换→新建一个项目切过去，看"即时生效"提示+顶栏
  项目名+配方列表是否当场换（harness 覆盖了逻辑层，窗体跳转靠人眼）。

## V1.72.9 — 缺省项目改"烧屏测试" + 状态字下移（2026-09-12，用户点名）

### 改动范围
- 缺省项目名 `Default`→`烧屏测试`（项目未上线，无老包袱，改干净不做迁移）：
  `Services/ProjectProfile.cs`（缺省返回 + 3 处注释）、`App.config` 的
  `ActiveProject` 初值、`MainForm` 顶栏兜底与 Designer 初值、
  `ProjectSwitchForm` 头部 ASCII 图。回归 +1 条"缺省项目名=烧屏测试"（全量 1187→1188）。
- `Views/MainForm.Designer.cs` — `lblRunStatus` Y 30→44（Sunny 组框标题占顶部
  约 30px，原来贴着标题边；组框高 84，截图确认留白正常）。
- 顶栏行内垂直居中 + 字号回到默认：通讯两标签改 `Dock=Fill` + 左中对齐
  （原来贴顶），权限两标签上边距 6px；五段字体设置全删（中途试过统一 9pt，
  用户纠正要与加 lblProject 之前的默认大小一致），harness 断言五段全 12pt
  默认、中线互差 ≤1px，截图四段同高。
- 项目切换权限：中途按用户要求放开给操作员过一次，随后用户改主意恢复
  "仅管理员"原逻辑（`MenuParamProject_Click` 的 Administrator 检查原样回退，
  无行为变化，不单独记版本）。

### 验证
- harness 直启主窗：顶栏回填"当前项目：烧屏测试"，状态组截图"空闲"离上缘正常。
- 踩坑：bin 里残留旧探针 `UiProbe*.exe.config`（含 ActiveProject=Default）会污染
  `ConfigurationManager` 读数导致假 FAIL——探针跑前先清 `UiProbe*.exe.config`。
- `build_and_test.ps1 -Affected`（用例改动兜底全量）：构建 + 冒烟 + **1188 全绿**。

## V1.72.8 — 顶栏标题删繁就简 + 运行状态字体统一（2026-09-12，用户点名）

### 改动范围
- `Views/MainForm.Designer.cs` — 删 `lblTitle`（"老化测试系统V1.16"与窗口标题栏
  重复多余；new/属性/挂接/列/声明五处同步删，配对扫描通过）：`lblProject`
  移到第 1 列（列宽回到 40/25/20/15），顶栏=项目 + 权限 + 通讯标签 + 通讯状态；
  权限/通讯状态两组保留不动。版本说明发版提醒注释同步（标题只剩窗体标题栏）。
- `Views/MainForm.Designer.cs` — `lblRunStatus` 删单写的微软雅黑 10F，
  与 `lblFanState` 同走默认字体（同源不分叉，V1.72 血泪）；`lblFanState`
  X 100→112，与"送风机状态"标签间隙 2→14px。

### 验证
- harness 直启主窗：`lblTitle` 字段已不存在，顶栏截图"当前项目：Default +
  当前操作权限：操作员 + 通讯连接状态：未连接"三段无挤压、着色正常。
- `build_and_test.ps1 -Affected` 构建 + 冒烟 + 回归子集 206 断言全绿。

## V1.72.7 — 顶栏显示当前项目 + 两弹窗标签输入防叠（2026-09-12，用户点名）

### 改动范围
- `Views/MainForm.Designer.cs` + `Views/MainForm.cs` — 顶栏 `tableLayoutPanelTop`
  4 列→5 列（标题 30% + 项目 22% + 权限 20% + 通讯标签 15% + 通讯状态 13%），
  新增 `lblProject`（Sunny UILabel，Dock=Fill + AutoEllipsis，9pt 加粗与标题同风格）；
  构造里 `EnsureActiveProfile()` 后 `UpdateProjectDisplay` 回填"当前项目：XXX"
  （切换项目必须重启，故只需设一次；切错项目=跑错工艺，首屏可见防呆）。
- `Dialogs/IdBindingForm.Designer.cs` — 标签"工位编号："实宽约 83px 与 X=85
  输入框重叠 13px：输入列三行统一右移 25（X 85→110），窗加宽 30
  （750→780，左侧 40% 列跟着放宽，输入右缘不顶边；标签列 X=15 不动）。
- `Dialogs/InputLotForm.Designer.cs` — "批号："擦边重叠 1px：输入框右移 10
  （X 90→100）+ 宽度缩 10（280→270，右缘 370 不动，右边距不变，窗体不加宽）。

### 验证
- harness 直启三窗：ID 绑定三行间隙 40/7/56、批号窗间隙 5（≥5px 口径），
  主窗顶栏回填"当前项目：Default"；PrintWindow 截图目检顶栏 5 列无挤压、
  两弹窗标签输入分离。
- `MainForm.Designer.cs` 声明/实例化配对扫描通过；`build_and_test.ps1 -Affected`
  构建 + 冒烟 + 回归子集 206 断言全绿。

## V1.72.6 — 输入框加高防压扁 + 批量窗显示模式行复活（2026-09-12，用户点名）

### 改动范围
- 5 个窗体 14 个 Sunny 输入框高度 21→29（Sunny 单行标准高；21 高时上下边框被压扁显示不全）：
  `RecipeManagerForm`（txtRecipeName/txtDisplayMode）、`BatchRecipeForm`
  （txtRecipeName/txtLimitTemp/txtNegativePressure/txtDisplayMode，其中 txtDisplayMode
  用户清单漏列但同病顺手一起改）、`IdBindingForm`（txtLot/txtStationNo/txtSn）、
  `InputLotForm`（txtLot）、`StationSettingsForm`（txtState/txtSN/txtRecipe/txtDisplayMode）。
- 行内上下居中：绝对定位的加高框 Y 上移 5px（框中心与标签文本中心对齐）；
  `BatchRecipeForm` 的 TableLayoutPanel 行自动居中，Temp/Pressure 容器 26→34
  让 24 高框在 40 行里上下各 8px。
- 行隙拉开（用户复检"每行都要上下居中对齐"后追加）：`RecipeManagerForm`
  负压/显示两行下移（最小行隙 4→8px）；`StationSettingsForm` 窗高 350→370
  （MinimumSize 同步），负压行下移 6、显示行下移 17，末两行行隙 6→12/15px。
- `BatchRecipeForm` 批量窗 6 个原生 nud：`Dock=Fill` 实际高度由容器定，
  Delay/Start 子 panel 高 26→30（nud 实高 20→26）。
- `BatchRecipeForm` 窗高 320→360（MinimumSize 同步）：内容区 280 时 5×40 行占满，
  第 6 行（显示模式，Percent）被挤到 0 高、整行消失（harness 截图抓获：改前窗底
  直接是"加入队列"按钮）；加高 40 后显示行回到 40，Percent 行判据见 AGENTS。

### 验证
- harness 直启 5 窗：反射打印全部目标控件实高 + PrintWindow 截图目检边框完整、
  标签输入上下居中、行隙无重叠。
- `build_and_test.ps1 -Affected`：构建 + 冒烟 + 回归子集 483 断言全绿。

## V1.72.5 — 测试窗屏幕居中 + 配方窗标签输入防叠（2026-09-12，用户点名）

### 改动范围
- `Dialogs/FanTestForm.Designer.cs`、`Dialogs/CommunicationTestForm.Designer.cs` —
  `StartPosition` 由 `CenterParent` 改为 `CenterScreen`（两窗均 `Show(this)` 非模态打开，
  原来相对主窗居中，用户要求打开即屏幕居中）。
- `Dialogs/RecipeManagerForm.Designer.cs` — 右侧输入列整体右移 25px
  （首列 X 95→120，冒号/单位/后序列同步平移，内部 2~3px 间距不变）：
  左侧标签 `AutoSize=true` 且未定字体，Sunny UILabel 默认 12pt 宋体下
  "配方名称："类 5 字标签实宽约 80px（右边缘 ~110+内边距），与 X=95 的输入框
  重叠约 15px；右移后间隙约 7px（≥5px 口径，V1.72.3 同款血泪）。
  最宽的显示模式框右边缘 320 ＜ 面板宽 376，无溢出。

### 验证
- 构建一次通过（仅一条既有 MesReporter 警告，与本次无关）。
- `build_and_test.ps1 -Affected`：构建 + 冒烟（存活 18s）+ 回归子集 150 断言全绿。

## V1.72.4 — 分级回归：小改只测影响面（2026-09-12，无产品改动，纯测试基建）

### 改动范围
- `tests/TestRunner.cs` — `Main()` 的 37 个 `Module` 硬编码改为 `allModules`
  查表 + `args[0]` 模块过滤（逗号分隔、大小写不敏感；`list` 打印清单；
  未知模块 exit 2，防拼错导致"零模块全绿"误报）。断言数不变（1187）。
- 新增 `scripts/get_affected_modules.ps1` — 产品文件→测试模块映射表（`$Map`，
  保守登记直接覆盖 + 交互模块），`git diff` 改动自动算子集；
  认不出的文件/骨架改动兜底 FULL，纯文档改动返回 NONE。
- `run_unit_tests.ps1` 加 `-Modules` 透传；`build_and_test.ps1` 加
  `-Modules` / `-Affected`（NONE 时跳过回归，构建+冒烟已过即绿）。

### 为什么这么改
- 用户点名：V1.72.3 一个 UI 间距调整跑了 1187 条全量，浪费资源。
  以后日常小改 `build_and_test.ps1 -Affected`（如公共参数窗只跑
  `UiStyleV172_1`，17 条秒过）；大重构/发布前仍走全量。
- 修了两个实现期抓获的脚本坑：①新 ps1 无 BOM，中文 `、` 的 UTF-8 尾字节
  在 GBK 下吞掉后续英文引号致解析雪崩——三个脚本统一加 UTF-8 BOM 根治；
  ②`$affected` 与开关 `$Affected` 同名（PS 变量大小写不敏感），赋值炸
  SwitchParameter 转换错——改名 `$affectedResult`（二分三次才定位，已进 skill 踩坑 #25）。

### 验证
- 映射单测：公共参数窗 2 文件→`UiStyleV172_1`；纯文档→NONE；
  DeviceManager→7 模块；未知新文件→FULL；未知模块名 exit 2。
- 子集 `UiStyleV172_1` 17/17 绿；`-Affected` 端到端（工作区含用例改动→兜底
  FULL）构建 + 冒烟 + **1187 回归全绿**。

## V1.72.3 — 公共参数标签输入框防叠（2026-09-12）

### 改动范围
- `Dialogs/CommonParameterForm.cs` — `CenterControls` 标签宽度口径修正：
  `Max(实测文本宽, AutoSize 实际宽)`，间距 8→10px。
- `Dialogs/CommonParameterForm.Designer.cs` — 初始 X 按运行真值摆
 （lbl 54→36、nud 167→192），所见即所得。
- 回归 +1 条（全量 1186→1187）：`公共参数标签输入框无重叠`
  （`nud.Left - lbl.Right >= 8`，直接锁视觉间距）。

### 为什么这么改
- 用户报 lblThreshold 与 nudThreshold 距离太近/重叠。
  根因：旧口径只用 `MeasureText` 纯文本宽（143px）估标签宽，
  但标签是 AutoSize 的 Sunny UILabel（默认宋体 12pt + 自带内边距），
  实占 146px——按小了算整组宽度，输入框偏左 3px，只剩 5px 缝。
- 附带坑：Designer 残留 `Size 107` 是旧字体过期值，
  拿它算居中会得出错误的 56/173（运行真值是 36/192），已按探针实测纠正；
  以后算居中一律以运行时实测为准，勿用 Designer 残留 Size。

### 验证
- 探针实测：旧算术视觉间距 5px（新断言下红，锁有效）、
  新算术 10px（绿）；构建 + 冒烟 + **1187 回归全绿**。

## V1.72.2 — 老配方 0 值语义锁（2026-09-12，无产品改动，纯回归）

### 改动范围
- 新增 `LegacyRecipeGuard` 用例 10 条（`TestRunner.cs`，全量 1176→1186）。
- 锁死四条语义：老文件缺字段读出 `NegativePressure=0`/`DisplayMode=null`；
  下发 0 = 定格 0（0≠全局）、null = 保持、清空名回全局；
  批量窗新建框默认=全局阈值；老配方回填框显示"0"待人工复核。

### 为什么这么改（澄清此前排查结论）
- V1.72.1 称"V1.59 三窗无此框、下发 null 走全局"是不准确的：
  实查 V1.59 代码，批量窗与工位窗当年就原样下发 `recipe.NegativePressure`
  （老配方恒 0 → 定格 0），只有配方管理窗不碰下发。
  所以 0 值 bypass 真空保护是 **V1.59 遗留**，不是 V1.66 引入；
  V1.66 只是让它可见（框里显示 0）+ 新建默认全局。
- 现场 `Recipes.json` 里 `NegativePressure: 0` 的配方，上站前必须逐条复核改成实际工艺值；
  改完本用例即是"已复核"的锁：以后谁动下发语义必须先过这里。

## V1.72.1 — 弹窗主按钮统一蓝 + 历史日期防叠 + 公共参数所见即所得 + V1.71 通讯无损验证（2026-09-12）

### 改动范围
- 5 个弹窗主按钮绿改风格蓝（DodgerBlue + Custom + 白字，与 V1.72 登录确认同色）：
  改密窗 btnOK、批号窗 btnOK、批量配方 btnAddToQueue、公共参数 btnSave、ID 绑定 btnSave；
  取消/关闭保持 Sunny 灰不动；登录窗确认按钮注释"语义绿"顺手纠为"主按钮蓝"。
- 历史记录窗顶部查询条重排防叠防裁：日期框宽 130→150，dtpStart X 80→100、
  lblEnd X 225→265、dtpEnd X 290→350、查询 X 440→520、导出 X 530→610；
  根因是 Sunny UILabel 默认宋体 12pt 实测"开始时间:"宽 79px，原 65px 间隙必叠。
- 公共参数窗所见即所得：Designer 里 lbl/nud Y 30/27 改为运行值 65/62
  （含 35px 自绘标题区），CenterControls 改为只居中 X、不动 Y，
  以后改纵向位置只改 Designer，两边永不分叉。
- V1.71 重构通讯影响核查：`git diff V1.70..HEAD --name-only` 确认
  Modbus/Scanner/Fan/DeviceManager/SerialPort/IoMap/Mock 零文件改动；
  有改动的仅 UI 类型替换（Form→UIForm 等）+ ThemeManager Sunny 分支 +
  RecipeAutoComplete 泛化 Control + PersistChanges 提 public + 驾驶舱 Designer 拆分。

### 为什么这么改
- 用户点名：弹窗确认还绿着，与登录蓝不一致；历史窗日期叠在一起且显示不全；
  公共参数设计器看离标题太近、跑起来又正常（CenterControls 运行时搬 Y）。
- V1.71 touches 16 窗，用户担心动到设备通讯，必须拿 diff + 全量 mock 回归自证无损。

### 验证
- 构建 + 冒烟 + **1176 回归全绿**（V1.72 计 1160 + 新增 16：UiStyleV172_1
  主按钮蓝 5 + 公共参数 Y 锁 4 + 历史布局 5 + 深浅蓝保留 2）；
  新用例先红后绿：首版历史 X=85 仍叠（探针实测标签 79px 抓获），改 X=100/350 后绿。
- P5 破空 CSV 曾出现一次偶发红（2s 轮询超时，重跑即绿，属负载抖动非产品 bug）。
- 通讯侧：ModbusConvert/ScannerParse/FanParse 纯函数 + DeviceManagerIntegration/
  Extended/Policy/Mes/Rules 全套 Fake 端到端（MockIo/Mock气压/Mock风机）全绿，
  真串口/真设备按 skill 边界仍靠现场联调。

## V1.72 — 驾驶舱边框 Designer 化 + 操作组/监视区避标题 + 登录确认换蓝（2026-09-12）

### 改动范围
- 主窗体"操作"组内 7 按钮整列下移 16px（首按钮 Y 18→34，避开组框标题区；间距/分组不变）。
- 登录窗确认按钮绿改风格蓝（DodgerBlue，与启动运行等主按钮同色），取消保持 Sunny 灰。
- 流程驾驶舱 Designer 化（只搬边框）：右栏空壳/状态条/窗体属性进
  FlowCockpitForm.Designer.cs（csproj 已登记）；自绘画布（要吃真实参数）+
  动态编辑器 + 定时器仍在代码里；新增无参构造（只装边框，构造永不抛）；
  lambda 处理器落袋为 BtnResetLayout_Click/BtnClose_Click 命名方法。
- 主窗监视区：三行下移（首行 Y 24→34，避开组框标题）+ 删 lblFanState 显式字体
  （根因：名标签继承 Sunny 样式字 12，值标签单写 9Bold 必分叉；删后同源一致）。

### 为什么这么改
- 用户目检：首按钮与区域上边缘重叠；Sunny UIGroupBox 标题约占顶部 30px，
  内容首控件 Y 须≥34（已进 AGENTS.md SunnyUI 约定）。
- 登录确认与"默认灰主按钮走 Sunny 蓝"家规对齐。

### 验证
- 构建 + 冒烟 + 1160 回归全绿（V1.71 计 1158 + 新增 2：驾驶舱无参构造不抛/边框7件）；
  harness 截图目检：主窗操作组间隙、登录蓝按钮、驾驶舱拆分前后像素级一致。

## V1.71 — 全窗 SunnyUI 小清新：UIForm 蓝标题 + 语义色保留（2026-09-11）

### 改动范围
- **16 个窗体 + 主窗体**：Form→UIForm（蓝标题），Label→UILabel，Button→UIButton，
  TextBox→UITextBox，ComboBox→UIComboBox，GroupBox→UIGroupBox，
  DataGridView→UIDataGridView（配方管理/历史记录）；日志框/CheckBox/NumericUpDown/
  ListBox/状态条保持原生（参考 HJVision：日志多行滚动条行为不确定）。
- **语义色一律保留**：绿确认/红急停删除/蓝动作经 Style=Custom+FillColor；
  默认灰主按钮走 Sunny 蓝，取消关闭走 Sunny 灰；停止/复位/下料判定走 Gray 档，
  深色经 ApplyOperationButtonsTheme 照旧 DimGray。
- **标题禁区**：UIForm 自绘标题占 35px 客户区——绝对布局整体下移 35px + 加高，
  Dock 布局加顶 Pad(38)；绝对窗加 MinimumSize=ClientSize 防缩坏；
  Dock 窗 ID 绑定加高 35（否则保存按钮被挤出，harness 实测抓获）。
- **ThemeManager**：Sunny 自绘控件按类型名走分支（UIButton 进按钮不动分支，
  UITextBox/UIComboBox 进输入分支；UILabel 天然命中 Label 分支）；
  新增 ApplyButtonColors（原生走 BackColor，Sunny 走 Custom+FillColor）与
  GetEffectiveButtonColors（下拉菜单继承宿主显示色）。
- **附带清理**：公共参数保存按钮改语义绿，V1.60.4 DimGray 特例与
  GetSaveButtonThemeColors 删除（绿两边都清晰）；配方检索框 provider 泛化为
  Control（原生/Sunny 通吃，光标定位走反射）；保存逻辑抽 PersistChanges
  （驾驶舱共用，落盘语义一份——抽取时行为逐行对过，零回归）。

### 为什么这么改
- 客户更容易接受：全软件同一套蓝标题 + 白底 + 蓝按钮，现场演示不露怯。
  有意不做的：字体统一换雅黑（动 AutoScaleDimensions，DPI 风险大，下次）、
  MessageBox 换肤（系统弹窗管不着，家规）、自绘大画布/圆形灯/预览画布（自己管颜色）。

### 验证
- `build_and_test.ps1` 全绿（构建 + 冒烟 + **1158 回归**，0 失败；V1.70 的 1134 + 新增 24：
  ThemeManager Sunny 分支 + PersistChanges 统一路 + 驾驶舱构造/检索框泛化）。
- harness 像素目检 16 窗：主窗深/浅 + 14 弹窗全截帧，布局/颜色/语义逐一过；
  抓到真 bug 2 个：MainForm 27 行块编辑吞掉 3 个 `new` 行（构造即 NRE，
  已补 + 全仓扫声明/实例化配对）；ID 绑定保存按钮被标题区挤出（已加高）。
  教训已沉淀：多行块编辑必须逐行核对（Edit 工具会模糊匹配吞间隔行）。

## V1.70 — 流程驾驶舱：固定拓扑可视化 + 点节点改配置（2026-09-11）

### 改动范围
- **为什么不是通用连线编辑器**：参考过 HJVision 的 mFormFlowEdit（串行站点流水线，
  每次部署拓扑都不同，所以要拖节点+连线+生成代码）。老化是并行时间状态机，
  拓扑被物理锁死（不上真空不能上电），通用编辑器会让客户能删掉安全联锁，
  违反"规则只能加严"家规。所以拓扑画死、节点可拖（纯视图），点谁改谁的真实配置。
- **画布**：7 节点 8 连线，节点副标题显示真实配置值 + 实时台数（每秒刷新）；
  滚轮缩放（25%~400%，鼠标为中心，悬停即缩）+ 中键平移 + 节点拖拽（位置存
  FlowLayout.json）；点击节点切右栏编辑器，连线只读（条件在端点改）；
  深色跟随主题。自绘+DPI 手动换算（家规：禁 ScaleTransform）。
- **右栏编辑**：按节点挂 key（启动 2 / 抽真空 3 / 上电老化 2 / 完成下电 3 /
  报警联动 6 / 断电恢复 1，下料纯展示）；布尔/枚举下拉，数字文本框，
  规则表多行框；切节点脏了先问存不存。
- **同一条保存路**：`SettingsForm.PersistChanges` 从保存按钮抽成静态方法
  （组合校验 + MES 就绪 + 分流写文件 + 热回写），驾驶舱共用——落盘语义只有一份。
  `ValidateValue` private → internal（同程序集复用）。
- **实时台数**：`DeviceManager.GetPhaseCounts`（抽真空/老化/完成/故障/空闲；
  双锁分开取，不持双锁防死锁）。

### 为什么这么改
- 客户更容易接受：流程图上就是他家的数（阈值/时长/策略全在节点上），
  点哪里改哪里，不用在设置表里翻 100 行。有意不做的：节点增删/连线改拓扑
  （物理锁死）、边条件自由表达式（安全联锁只认内置，表达式只进规则表）。

### 验证
- `build_and_test.ps1` 全绿（构建 + 冒烟 + **1134 回归**，0 失败；V1.69 的 1095 + 新增 39：
  FlowCockpitV170 拓扑/文本/布局约 35 + R4 台数 4）。
- 重构零回归：PersistChanges 抽取后旧用例全绿（保存按钮行为逐行对过，无改动）。

## V1.69 — 三期规则表达式 + 阶段流：沙盒引擎 + 自定义报警/完成条件/跳过抽真空（2026-09-11）

### 改动范围
- **规则表达式引擎 `RuleExpr`**（手写递归下降，不引脚本引擎）：
  12 个冻结变量（pressure/temp/tempset/hum/device/delaysecs/vacsecs/agesecs/
  duration/threshold/di0/hour）+ 四则比较逻辑括号；`&&` `||` 短路（右分支除零可跳过）；
  除零/模零/未知变量→error；NaN（传感器离线）参与比较一律 false（死传感器不触发，
  fail-safe，非 IEEE，特此说明）。解析失败带字符位置，保存时拦。
- **规则执行器 `RuleEngine`**：编译缓存（配置不变不重编）；持续计时（成立满 N 秒才触发，
  防毛刺）；完成表达式 OR 语义（只能提前完成——烧屏架少点亮更安全，拖不成无限老化）；
  时钟可注入（回归假时钟）。求值错按 false + LastError 边沿日志。
- **执行侧挂接**：自定义报警（与内置走同一边沿，记 FAIL，非真空类 Q19 不改它）；
  跳过抽真空 `SkipVacuum`（启动即上电 + 压力报警同步豁免，机械夹具专用，
  启动日志大写警告）；规则表走弹窗编辑器（多行文本 + 实时校验 + 变量速查）。
- **guardrail**：规则只能加严不能松绑（内置联锁动不了）；三项全空/关=零行为变化；
  规则进 `PolicyKeys` 跟项目走。

### 为什么这么改
- 三期收官"万物可配"：报警条件/完成条件/阶段跳过全部可配，出差改文本不动代码。
  有意不做的：规则改完成口径（PASS/FAIL 判定权仍在 Q22 策略，不进表达式）、
  规则联动外部输出（只报警不断新动作，防配错乱写 DO）、可视化拖拽编排
  （流程三年没变过，文本+校验已够，造引擎不值）。

### 验证
- `build_and_test.ps1` 全绿（构建 + 冒烟 + **1095 回归**，0 失败；V1.68 的 1013 + 新增 82：
  RuleExprV169 解析/求值/NaN/规则表/执行器约 65 + DeviceManagerRules 端到端 3 场景约 15 +
  布尔锁 +1）。
- 回归红过两次，都是测试写法问题（短路断言把"值"当"无错"查；同步锁名单漏新 key），
  产品代码零修改——引擎一次写对。

## V1.68 — 二期 MES 映射层可配：HTTP 上报 + 触发器/字段映射/离线缓存（2026-09-11）

### 改动范围
- **传输层（代码）**：`MesReporter` 后台线程串行 POST JSON（单入口 `MesEndpoint`，
  事件类型在 event 字段区分）；鉴权 None/Bearer/Basic；失败按 `MesRetryCount`/
  `MesRetryIntervalMs` 重试，全灭进离线缓存 `MesQueue.json`（上限 5000 丢最旧），
  下次成功顺带补发。Report 组包+入队毫秒返回，失败永不阻断生产（只记日志+进缓存）。
- **映射层（可配）**：触发器 `MesTriggers`（Start/Complete/Alarm/UnloadJudge，
  留空=全开）+ 字段映射 `MesFieldMap`（MES名=本站名，如 eqId=device；留空=直通）+
  静态字段 `MesStaticFields`（如 line=L5，原样并入每次）。本站 vocabulary 15 个
  （time/lot/device/event/sn/recipe/result/detail/pressure/temp/duration/
  displayMode/project/disposition/defectCode），解析纯函数 `MesMapping` 三方共用。
- **跟机器还是跟项目**：开关/URL/超时/鉴权/重试/Mock 跟机器（App.config）；
  触发器/映射/静态跟项目（Policy.json，`PolicyKeys` +3）。系统设置新增"MES 对接"分类，
  脏映射保存即拦（报出哪一组错）；开了开关没配地址同样拦截。
- **执行侧挂接**：启动/完成/报警/下料判定四处 `Report`（完成带时长+判定结果，
  报警带压力值，下料带不良代码+处置）。`MesMockEnabled=true` 时不发 HTTP，
  只写" MES上报(Mock)"事件到 CSV（含完整 JSON）——MES 没好也能端到端验格式。
- **回归测试缝**：`MesReporter.Transport` 静态函数缝，Fake 抓包零外网。
- **三件补齐（V1.68 同版追加，未推送前 fold 进来；项目未上线，不兼容，改干净）**：
  - 密钥 DPAPI 加密：`MesCrypto`（LocalMachine scope + "DPAPI:" 前缀；无前缀的明文
    一律拒收按空处理，不兼容——PasswordHasher 同先例；解密失败同样按空+记日志）。
    SettingsForm 显示解密/保存加密/加密失败明示；MainForm 读取解密进内存。
  - 自定义 HTTP 头 `MesCustomHeaders`（头名=头值；头名纯 ASCII token；
    与鉴权头同名时鉴权优先，顶不掉）。
  - 按事件分地址 `MesEndpointMap`（触发器=URL；没配的回退默认地址；URL 必须 http(s) 开头）。
  - 三者全走"保存时校验 + 上报时跳过"双保险（MesMapping 纯函数）。

### 为什么这么改
- MES 全可视化是伪命题（每家握手/事务不同），映射层可配已覆盖 90% 现场差异；
  出差带 Mock 先验格式，MES 一就绪填地址即上线。
- 有意不做的：~~token/密码加密（明文存，工控机物理隔离；客户要求再做 DPAPI）~~、
  自定义 HTTP 头、按事件分地址（单入口已够，MES 侧按 event 分流）。
  （注：用户一句话推翻——"做了又怎么样，反正都是可配置的"，三件已补，见上。）

### 验证
- `build_and_test.ps1` 全绿（构建 + 冒烟 + **1013 回归**，0 失败；V1.67 的 918 + 新增 95：
  MesV168 映射/组包/上报器 + DeviceManagerMes 端到端 + 三件补齐的加密/头/分地址）。
- 回归红过两次，抓的都是真 bug："布尔键一致"锁（+2 key 后 11→13）；
  中文头名误放行（`char.IsLetter` 认中文，HTTP 头名只认 ASCII，已收紧 + 锁用例）。

## V1.67 — 一期"万物可配"：7 个待确认点全部策略化 + 项目档案切换（2026-09-11）

### 改动范围
- **工艺策略 L2 层（8 枚举 + 2 配套 key，缺省=现状行为）**：`ZeroDurationPolicy`/
  `EmptySnPolicy`（Q13：Warn 只警告现状 / Block 硬拦截，连确认框都不进）；
  `FanDisconnectPolicy`（Q16 前半：LogOnly 照跑现状 / BlockStart 风机未连阻断启动，只管启动那一下）；
  `VacuumFailKind`（Q19：ProductFail 记 FAIL 现状 / FixtureAlarm 记"装夹异常"可重测，只管真空类，
  DI 与失联口径不变）；`CompletionJudgePolicy`（Q22：AutoPass 现状 / PendingReview 到时标"待判定"）；
  `PowerLossPolicy`（Q21①：RestartFull 整台重测现状 / ResumeRemaining 重抽真空+补足剩余时长，
  断电期间不计）；`AgingPressureLossPolicy`（Q11：StopOnLoss 现状 / KeepRunning 只记事件继续老化，
  只管 Aging 阶段，抽真空失败永远报警）；`CompletionAction` + `VentValveDoPoint`
  （Q6/Q15：PowerOffOnly 现状 / 蜂鸣 / 破空泄压 / 都要；点位 0=未接硬件只记日志跳过，绝不写坏通道）。
  超温联停沿用 V1.66 `FanTempShutdownEnabled`（Q16 后半，全机单探头只有全线一种动作）。
- **策略存项目文件**：`Projects/<项目>/Policy.json` 覆盖 App.config 机器缺省；
  系统设置新增"工艺策略"分类（下拉中文显示、存英文名，大小写兼容，脏值兜底现状+保存时拦截）；
  矛盾组合保存即拦（联停开但上限 0 / 泄压选但点位 0，直接告诉用户先填哪个，
  纯函数 `AgingSequencer.ValidatePolicyCombination`）。
- **项目档案切换**：`Projects/<项目>/` 下放配方/工位设置/主页布局/策略；用户/快照/日志跟机器。
  参数设置下拉新增"项目切换"（仅管理员；新建=以当前为模板复制；切换必重启；在测禁切）。
  首跑自动建 Default 并把老文件搬进去，老用户无感迁移。
- **下料判定**：操作区新增"下料判定（选中台）"按钮 + `UnloadJudgeForm`
  （PASS/FAIL + 不良代码 + 处置重测/报废/降级/让步；只收 Completed 台 → 写 CSV 追溯 → 回空闲；
  AutoPass 下点它只提示）。判定结果以 CSV/历史查询为准（LastTestResult 本来就只活到复位）。
- **tooltip 全覆盖**：设置表全部单元格悬停显示说明，超 40 字自动换行（`WrapTooltip`，断点优先标点）。
- **执行侧分支**：报警分类 `ClassifyAlarm`（IsAlarm 转调，口径一份）；上电边沿补快照
  （Phase + 上电时刻，续跑就靠它）；破空阀复位/启动/急停统一关，不残留输出。

### 为什么这么改
- 出差目标：不等客户确认——客户 A 要拦截、B 要放行，改配置 30 秒生效，不动代码。
  7 个确认点全部收敛为策略 key，默认值=现状行为，老项目零变化；新项目建档案即隔离。
- 有意不做的（见问题清单 §四落子）：业务流可视化编排（三期，流程稳定不值得造引擎）、
  MES 全可视化（只做映射层，二期等真需求）、阈值类进档案（二期候选，先稳一期）、
  定格改实时跟随（Q18，不接受=大改，仍等签字）。

### 验证
- `build_and_test.ps1` 全绿（构建 + 冒烟 + **918 回归**，0 失败；V1.66 的 828 + 新增 90：
  PolicyV167 模块策略纯函数/名单同步锁/tooltip/档案 + DeviceManagerPolicy 端到端 5 场景）。
- 回归抓到真问题 1 个：上电边沿没落快照 → 续跑快照 Phase 恒为 Vacuuming，
  P4 用例红 → 产品代码补 `SaveSessionSnapshot()`（边沿一次）→ 绿。

## V1.66 — 烧屏锚免确认四项：配方负压必填+显示模式+启动警告+超温联停开关（2026-09-11）

### 改动范围
- **配方负压阈值必填（P0-❶，真 bug）**：配方管理/批量设置/工位设置三窗各加"负压阈值(kPa)"输入
  （±9999/1 位小数）+ 存取 + 回填，新建默认=全局 `AlarmPressureThresholdKPa`，
  存什么启动定格什么。以前三窗没这个框，新建配方恒 0，下发后阈值≈0（负压域里≈永远到位），
  等于悄悄关掉真空保护。项目未上线、无老配方包袱，不做"0=回全局"魔法，所见即所得。
- **配方显示模式全链路（P2-❼记录部分）**：`RecipeConfig.DisplayMode` 自由文本（白场/RGB循环/棋盘格…），
  三窗录入 → `Recipes.json` → `SetStationRecipe` 下发 → `StationInfo` → 采集叠加到
  `BarometerData.DisplayMode` → 启动日志携带。只追溯不判定（画面由治具/PG 产生，老化架发不出）；
  面板显示暂不加（布局改动大，等现场确认画面方案后与 PG 控制一起做）。
- **启动确认框追加风险警告（Q13 非阻断版）**：0 时长（不限时长、永不到时完成）与空 SN
  （完成后无法追溯）的工位号直接拼进现有确认框，点"是"照跑、点"否"取消，无新弹窗、
  不改流程。文案纯函数 `AgingSequencer.BuildStartWarningText`。
- **超温全线联停开关（Q16 软件地基，默认关=零行为变化）**：`FanTempShutdownEnabled`
  （App.config/系统设置"老化测试业务"，默认 false）+ 上限沿用 `FanTempAlarmLimitC`；
  打开后送风机超温自动停全部在测工位（边沿触发一次，回温自动复位；`GetTestingDeviceIds`
  新口）。全机只有一个探头，做不到单台联停——停单台还是全线等 Q16 拍板前保持关闭。
- **SetStationRecipe 签名加显示模式参数**（4 参），两处产品调用 + 10 处集成用例同步；
  清空配方名时负压与显示模式一并清（防残留旧工艺）。

### 为什么这么改
- 客户锚定 JBD MicroLED 微显示烧屏架（见问题清单 §四）：72 小工位真空吸附+上电点亮，
  24h 通电是常态。0 时长=无限点亮、温度只显示不停机，在此场景下是火灾/过老化级风险；
  配方无负压=真空保护形同虚设；烧什么画面不记录=整批数据不可比。
- 四项都不碰工艺口径（警告可继续、开关默认关、记录不判定），无需现场确认先做；
  真空失败责任/自动 PASS/续跑/错峰/三色灯/电流/MES 留问题清单 Q19~Q22 等签字。

### 验证
- `build_and_test.ps1` 全绿（构建 + 冒烟 18s + **828 回归**，0 失败；新增 20 条：
  警告文案 5 + 联停判定 4 + 存储往返 2 + 模型克隆 1 + 窗框回填 2 + 下发/叠加/在测 id 6）。
- 回归顺手抓到真遗漏：`SettingsForm.ValidateValue` 布尔分支是硬编码名单，
  新 key 只进了 `_boolKeys` 没进该分支——"布尔键一致"锁报警，已补（11 项）。

## V1.65 — 主窗体右侧改按比例自适应 + 窗体最小宽适配低分辨率屏（2026-09-11）

### 改动范围
- **比例自适应（`Views/MainForm.cs`）**：右侧宽度不再写死 300px，改为分隔容器宽 ×
  `RightPanelRatio = 0.234`（就是现状比例 326÷1394，正常屏上看起来和以前一模一样），
  180~340 钳制（小屏不挤坏按钮文字，大屏不浪费网格空间）；窗口缩放由
  `splitContainerMain.Resize` 自动跟进（只认总宽变化，用户手动拖分隔条不被覆盖）；
  计算抽成纯函数 `ComputeRightPanelWidth`（无 json=比例、有 json=文件绝对值优先）。
  操作按钮宽度本来就是运行时自适应的（`ResizeOperationButtons`），零改动直接受益。
- **低分辨率适配（`Views/MainForm.Designer.cs`）**：窗体级最小尺寸 1400×900 → 1150×800
  （原来 1400 宽在 1366 工控机上一最大化就出横向滚动条，就差 34px），设计宽 1400 → 1280，
  右侧/按钮/日志框设计值同步按比例换算（298/256/280，运行时会被重算覆盖，只保证设计视图不错位）。
- **入口对齐**："关于 → 主页区域调整"与系统设置"主页区域"行在无 json 时显示当前比例生效值
  （以前显示写死的 300/260，进门就和主界面对不上）；`SettingsForm` 构造新增可选参数
  `effectiveRightWidth`（唯一调用方主窗体传入）；编辑器点保存即定格为绝对值，
  想恢复跟随删 `HomeLayout.json` 重启（已写进注释）。
- **`Models/HomeLayoutConfig.cs`**：类默认 260 → 240（只作编辑器"恢复默认"基准）；
  预览控件逻辑坐标系 1400 → 1280（与主窗体设计宽保持一致）。
- **测试**：`TestRunner` 新增 8 条（比例常量/护栏、1394→326 现状锁定、1366 屏→318、
  1274→298、大屏钳 340、小屏钳 180、宽非法兜底 328、自定义优先），旧 2 条默认值断言
  260 → 240 同步；全量 800 → 808 绿。SKILL.md 计数与覆盖表同步。

### 为什么这么改
- 工控机 1366 宽低分辨率屏，主窗体一屏装不下、必须左右拖滚动条才能看全右侧。
  用户明确两点：①右侧按钮区不用那么大；②不要写死像素，要按百分比——换台设备又不行。
- 网格 8 列物理宽 1896px 任何小屏都塞不下（面板内容是业务死的），用户已接受网格内部滚动；
  本次只解决"窗体级一屏"，不动行列配置。

### 验证
- `build_and_test.ps1` 全绿（构建 + 冒烟 18s + 808 回归；新 8 条全过）。
- 未做 harness 像素验证：SplitterDistance/按钮宽全是运行时计算值，文本断言即决定性验证
  （同 V1.63.2 约定：逻辑分支全覆盖即可）。

## V1.64.4 — 问题确认清单补问11~18落文档（2026-09-11，纯文档）

- 改动范围：`docs/问题确认清单.md`新增"补问 11~18"小节（老化中失压/超时失联实测/
  时长>0与空SN拦截/复位人/徒手取料/风机断连超温/DI触点/定格语义，每条带现状一句话），
  §三确认稿结论 11 项→19 项同步，参数表已有的超时/次数/不限时长说明保持对应。
- 为什么这么改：用户要求上一轮列的 11~18 条全部落进文档，现场逐条过、回来照单排期。
- 验证：纯文档改动，UTF-8中文自查命中，不跑回归。

## V1.64.3 — 清零构建警告CS0108（2026-09-11）

- 改动范围：`Dialogs/HomeLayoutEditorForm.cs`的`HomeLayoutPreviewControl.Layout`属性加`new`
  显式声明有意隐藏基类`Control.Layout`事件（类内18处`Layout.`全指本属性，基类事件从未使用，
  不改名是为少动调用方）+注释说明。
- 为什么这么改：Debug构建长期带1个CS0108警告，用户要求清掉；隐藏是故意的，缺的只是`new`声明。
- 验证：MSBuild Debug一次通过、零警告零错误；UTF-8中文自查命中；逻辑零改动，不跑全量回归。

## V1.64.2 — 纠正三处过时注释+附图补到100%对应（2026-09-11）

- 改动范围：
  `Models/DeviceConfig.cs`的`MaxTestDurationSeconds`旧注“后续可扩展按配方”删掉，
  改为V1.59现实：配方StartTime优先、全局兜底、都为0=不限时长、启动瞬间定格；
  `Services/DeviceManager.cs`的V1.10历史块加“已被V1.59覆盖”标注（启动只开阀/配方时长/完成标PASS），
  防后人按旧注释改错；`Dialogs/StationSettingsForm.cs`的`GetStateText`补`Completed→已完成`分支
  （以前完成台误显示为空闲）+注释同步；`docs/问题确认清单.md`附图补老化中失压/通讯失联两分支、
  停止vs复位区别、关阀≠泄压、等于阈值算到位、定格语义，参数表加超时/失败次数/不限时长说明。
- 为什么这么改：用户指出“注释不准确不能一直错”；附图上次少画两条报警边+两句简化语
  （手动停止=回空闲/取走=回空闲）会误导操作员培训。
- 验证：MSBuild Debug一次通过（仅HomeLayoutEditorForm CS0108旧警告）；UTF-8中文自查命中；
  纯注释+文档改动，不跑全量回归（逻辑零改动，GetStateText多一支返回不影响编排）。

## V1.64.1 — 现场版行动清单（2026-09-11 下午，纯文档）

- `docs/现场业务预研Plan.md` 按"下午访谈能直接用"重写：删方法论/硬件表/推断长文/
  八步分步表/缺口全表/远程提纲/物料单/风险表（事实已并入必问清单的"不同答案改什么"）；
  只留三节——按对象组织的必问 10 题、真机验证 tick、当场填的《确认稿》模板
  （结论 11 项 + 工艺参数表 + 改造清单 + 签认栏），回来照单排期改造。
- 不碰代码，不跑回归（纯文档改动）。
- 现场版措辞去 AI 化：客户可见文档不出现 AI 字眼（"回来 AI 开工"→"排期施工"）。
- 现场版追加"业务串联逻辑示意图"（单台工位视角 ASCII 图，与 AgingSequencer/DeviceManager 实现逐条对过：双条件上电/15s 超时/边沿报警/风机跟随/完成标蓝/判定口径），先看图再问§一。

## V1.64 — dev 最高权限账号 + 深色入口收进关于（仅 dev）+ 两份现场文档精简（2026-09-11）

### 改动范围
- **dev 账号（`Services/UserManager.cs`）**：新增系统保留名 `dev`（归属 Administrator 角色，
  种子密码 dev123），走"用户权限→管理员"登录框输入即进，界面无提示（隐藏入口）。
  dev 可删改业务管理员（删/改名/重置密码）；普通管理员只能管操作员/技术员；
  dev 名注册/改名（大小写变体同样）一律回"该账号名不可用"；dev 自身不允许改名/删除。
  自愈：新装自动种子；老 Users.json 无 dev 加载时自动补上且不碰已存密码；
  手改文件保留"dev + 第一个业务管理员"。
- **登录框隐藏（`Dialogs/LoginForm.cs`）**：管理员登录下拉框不再列出 dev，记住登录不存/不回填 dev。
- **用户管理窗（`Dialogs/UserManagementForm.cs`）**：dev 登录时角色下拉多出"管理员"，
  可删改业务管理员；普通管理员的管理员组三处拦截保留（UserManager 里还有第二道）。
- **深色入口（`Views/MainForm(.cs/.Designer.cs)`）**：顶部独立"深色模式"按钮删除
  （菜单 5 列→4 列），切换项收进"关于"下拉、仅 dev 登录时可见；dev 登录后顶栏显示
  红色"最高权限(dev)"。
- **文档精简**：`docs/通讯接入.md` 删版本考古注与重复表述（寄存器/换算/坑点/排障原样保留，
  另修正 0x2000~0x2003 阀编号 Y000~Y077）；`docs/现场业务预研Plan.md` 标题去"待 Review"、
  §2/§3 现状列刷新到 V1.59+ 现实、§6 清单重排 P0（已实现项改为确认口径）、删除被头部
  表格取代的 §8。
- **测试**：`TestRunner` UserManager 新增 J 组 34 条（种子/隐藏登录/注册保护/删改矩阵/
  老文件自愈/手改保留规则），全量 766→800 绿。

### 为什么这么改
- 现场要一个能管管理员的最高权限，但不能让普通人从界面上发现入口：
  藏进管理员登录（无提示）+ 藏进关于下拉（按身份显隐），两处都是"知道才有"。
- 深色按钮放顶栏人人可见，与"隐藏"思路冲突，一并收进关于下拉只给 dev。
- 两份现场文档越长越没人看：实现状态分散在 §2/§3/§8 三处且互相打架（§2 现状列还停在
  V1.59 前），砍到"一处事实 + 行动清单"，现场才翻得动。

### 验证
- `build_and_test.ps1` 全绿（构建 + 冒烟 + 800 回归；J 组 34 条全过，含老文件自愈与反向保留）。
- `winforms-ui-debug` harness 直启主窗：登录下拉无 dev；关于菜单 admin/未登录无深色项、
  dev 有；用户管理窗 dev 才有"管理员"角色；顶栏 dev 显示"最高权限(dev)"。
- 未给登录下拉/菜单显隐加 TestRunner 用例：纯 UI 接线，harness 反射即决定性验证
  （同 V1.63.2 约定）。

## V1.63.2 — 修通讯测试页空白：UIPage 改回 AddPage 挂接（2026-09-11）

### 改动范围
- **`Dialogs/CommunicationTestForm.Designer.cs`**：删掉手写的 `TabPage` 包裹
  （`tabPage1/tabPage2` 字段与全部相关行），负压/上电两个 `UIPage` 改回
  `tabControl.AddPage(page)` 挂接（SunnyUI 专用，内部建 TabPage + Dock=Fill +
  绑定 + Show()）；删掉 `Visible=false` 与 `TabPage=...` 绑定行；文件头注释补
  AddPage 红线。产品代码（`.cs`）一行未动。

### 为什么这么改
- V1.21 原用 `AddPage` 一直正常；8 月"优化布局"提交（a1cc599）改成手写
  `TabPage` 包裹 + `Controls.Add`，但漏掉了关键的 `Show()`，两页
  `Visible=false` 至今：72 个圆形灯按钮其实全建好了（harness 数出 89 控件/
  页），只是整条显示链不可见。底部按钮在页签之外、连接走共享连接，
  所以现场现象恰好是"页上没图标、但连接测试正常"。
- 寄存器/协议/业务逻辑无任何变化，`README`/`docs/通讯接入.md` 的功能描述
  依然准确，无需同步。

### 验证
- `winforms-ui-debug` harness 直启（Mock 连接）：修前 `pageVacuum.Visible=False`、
  `tabControl.GetPage(guid)=null`、首行像素 `darkBtn=0`；修后 `Visible=True`、
  双页 `GetPage` 均 OK、`darkBtn=52`，PrintWindow 截图 9×8 灯（Y000~Y107）全现，
  切到"载台上电测试"页同样显示。
- `build_and_test.ps1` 全绿（构建 + 冒烟 + 766 回归）。
- 未给 `TestRunner` 加用例：本 bug 是 Designer 接线问题（Visible 状态），
  构造即定、无逻辑分支，harness 反射 + 截图即决定性验证，硬塞文本断言只有
  脆弱没有价值。

## V1.63.1 — 现场前文档同步（2026-09-11）

### 改动范围
- **`docs/通讯接入.md`**：§4.1/§4.2 风机状态中文"程式启动"改"程式运行中"（V1.63 落下的文档尾巴）；
  §2.5/§5 排障补 V1.63 串口钳制说明（气压表+扫码枪 6 项越界回退 + `Debug` 日志明示）。
- **`docs/现场业务预研Plan.md`**：§2 现状列按 V1.59 更新（④双条件上电/⑥自动下电标完成/⑦判定口径/⑧整台重测），
  §8 逐项标注已完成/未做，避免拿旧表去现场问错话。
- **`README.md`**：§10 待完善细化（工位缓存已有 `StationSettingsCache`，差的是批号落盘+生产记录关联；破空挂 G2 确认点）。
- **skill 头**：`SKILL.md` 首行 246+ 改 766+，与正文覆盖表对齐。

### 为什么这么改
- 明天去现场，文档漂移比代码 bug 更误事：通讯接入写错文案会在联调时对不上号，Plan 旧表会把已实现的问成没实现。
  纯文档改动，不碰产品代码。

### 验证
- `rg` 全仓扫"程式启动"确认仅剩 CHANGELOG 历史条目与程序注释（按惯例不回改）；
  `build_and_test.ps1` 全绿（文档改动仍跑一遍兜底）。

## V1.63 — 三个 deferred 项落地（2026-09-10）

### 改动范围
- **工位温度改数字框（1B）**：`StationSettingsForm.txtTemp` 文本框改为 `nudTemp`
  （1 位小数/步进 0.5/范围 0~300，与配方管理窗 `nudLimitTemp` 对齐；位置尺寸不动，
  布局图仍然有效）。`ParseTemperature` 改为直读 `Value`（恒合法）；
  配方/缓存回填加钳制。非法输入从输入端消除，"非法存 0"不再可能。
- **加载钳制串口参数（2B）**：`SerialPortHelper` 新增 `ClampDataBits/ClampBaudRate/
  ClampTimeoutMs` 纯函数；`MainForm.LoadConfig` 对气压表/扫码枪两套
  波特率/数据位/串口超时共 6 项钳制 + `Debug` 警告（与 TotalInputs 纠错同模式）。
  手改配错不再表现为"连不上"误导，启动日志明写回退值。停止位/校验位下游
  自带兜底（default→1/None），无需处理；TCP/风机超时走各自重连语义，保持不动。
- **风机测试窗文案对齐（3A）**：`ProgramRunning` 显示由"程式启动"改"程式运行中"，
  与主窗体（V1.16.1 定版）一致；未知态保持 "--"（主窗的"已连接"偏 misleading，不跟）。
- **测试**：UiPureHelpers 同步（温度框断言/配方回填钳制/钳制矩阵）；风机中文断言更新。

### 为什么这么改
- V1.62 审查时 defer 的三项，用户拍板后落地。温度是"将来超温保护的地基"，
  现在把脏数据入口堵死；串口钳制把"配错→连不上误导"变成"配错→日志明示"。

### 验证
- `build_and_test.ps1` 全绿。

## V1.62 — 全仓测试补齐 + 修 8 个实锤 bug（2026-09-10）

### 改动范围
- **修高危：备用通道号越界静默失效**——文档写通道 0x00~0x1F、示例用 0x10/0x11，
  但执行侧 bit 恒 0~15：0x10+ 配了永远匹配不上（源）或掩码归零读写恒错（目标），
  且全程无报错。`IoOutputChannelRemap` 解析收紧到 0x00~0x0F 并明示 error；
  `ModbusTcpIoController.MapOutputChannel` 加非法目标兜底（保持原通道读写一致）；
  编辑弹窗微调框最大值同步 0x0F；README/通讯接入/App.config/设置窗文案与示例全改。
- **修采集防火墙两处**：读取器返回超长数组会先索引越界（越界检查在后）；
  上报 DeviceId 与轮询位置不一致会导致 IsAlarm 查错台状态、缓存按错键落盘。
  现循环取 `Min(长度,总数)` + id 不一致本轮跳过 + 缓存只收合法编号。
- **修快照恢复 null 台空引用**：`RecoverSession` 的 ConvertAll 未过滤 null（循环体有判空，
  ConvertAll 没有），脏快照直接 NRE。改为只收有效台编号。
- **修 CSV 回车断行**：`CsvEscape` 补 `\r` 包裹（串口/扫码字符串常带 `\r\n`）。
- **修配方 Id 撞号**：新增用 `Count+1`，删中间配方后会重号；改 `Max(Id)+1`。
- **修 Mock 三处与真实现分叉**：`MockFanController` 未连接启停恒 true（现返 false 不改状态）、
  从未 Connect 就重连成功（现 _config null 返 false）；`MockIoController.Connect(null)` 空引用（现拒绝）。
- **修工位时间 24h 截断**：`StationSettingsForm.SetTimeInputs/GetTimeText` 用 `Hours` 分量，
  25 小时回填/显示成 1；改 `TotalHours` 与 `RecipeManagerForm` 对齐。
- **修串口识别大小写**：CH340 描述匹配改为忽略大小写，并抽 `IsCh340Device` 纯函数。
- **判定与换算收拢为纯函数**（家规落地）：`AgingSequencer.IsPressureOutOfRange`（两处私有判定转调）、
  `ModbusRtuBarometerReader.ConvertRawToPressureKPa/ConvertThresholdToRegister`、
  `FanControllerClient.ParseFanRegisters`（三处转调，行为逐字一致）。
- **卫生**：5 个状态清理点同步把阈值定格清回全局；IsAlarm 误导注释修正。
- **测试**：回归 355→759 断言（+12 模块：IoMapBuilder/MockDevices/StationCache/ModelDefaults/
  SettingsValidate/ScannerParse/ModbusConvert/FanParse/StationTime/HistoryCsv/UiPureHelpers/
  DeviceManagerExtended，含集成 E1~E30 扩展场景）；harness 编译加 SunnyUI 引用；
  skill 覆盖表与踩坑清单同步（+3 条）。

### 为什么这么改
- 用户要求全面审查、绝对稳定：四个审计方向过完，纯逻辑能测的全测，不能测的（真串口/
  真设备/UI 弹窗/10s 时间窗过期分支）明确列边界。修的全是"静默错"类别——不抛异常但
  行为错（通道配了不生效、CSV 错位丢行、Id 撞号），单靠现场联调很难发现。

### 验证
- `build_and_test.ps1` 全绿（构建 + 冒烟 18s + 759 断言）；中途 4 个新用例失败定位到
  3 处用例写法问题 + 1 处场景串扰（E30 留下的配方污染 E23），修用例后全绿，
  产品代码零回退。

## V1.61 — 负压阈值默认改 -5kPa（2026-09-10）

### 改动范围
- 项目从未上线，-95kPa 系早期随意填的值，全仓统一改 -5kPa，只留两处不动：
  `app.manifest` 的 Windows 系统 GUID（含 95bb，非阈值、改不得）、`CHANGELOG` 历史条目（记录当时代码真实状态，回改即失真）。
- 功能默认值：公共参数窗 `nudThreshold`（`CommonParameterForm.Designer.cs`）、`App.config` 的
  `AlarmPressureThresholdKPa`、`DeviceConfig` 同名属性、`MockBarometerReader` 模拟两档
  （良好 -6~-10kPa / 较差 0~-4kPa，保持"良好低于阈值、较差高于阈值"）。
- 注释示例同步：`StationInfo` 吸附举例、`ModbusRtuBarometerReader` 三处小数位换算举例
  （-5 写错成寄存器 -5→仪表显示 -0.5，差 10 倍逻辑不变）、各接口参数示例。
- 集成测试夹具等比镜像：全局 -95→-5，配方 R1 -60→-3、压力 -70→-4（仍满足"配方下到位、全局下越限"，
  配方优先断言继续有效），R2 -95→-5、压力 -96→-6；`TestRunner` 夹具配方名同步。
- 文档同步：`README` 三处、`docs/通讯接入.md`（含阈值换算示例更正 -5.0→0xFFCE）、
  预研 Plan 能力现状行 + 1.2 节推断重写（旧"接近极限真空→强吸附"推断系基于随意值，已注明作废，治具/气密用途仍待现场确认）。

### 为什么这么改
- 现场工艺按 -5kPa 判定：新装机/配置丢失回退时软阈值与设备阈值双双偏严，Mock 演示也会因区间错位频繁误报警。
  没上线就没有历史包袱，一次改干净比留两套值让后人猜要省心得多。

### 验证
- 回归补 2 条（`DeviceConfig` 默认阈值 -5kPa + 默认报警方向）；`build_and_test.ps1` 全绿。

## V1.60.3 — 深色下电/真空关改深灰底白字（2026-09-07）

### 改动范围
- 工位面板"下电/真空关"块：深色下底色改 `DimGray`、文字改白色（参考主窗体停止/
  复位按钮，跟各窗"取消"按钮同款）；浅色仍用配置灰底（默认 LightGray）黑字。
- `WorkstationGridView.GetOffBlockThemeColors` 纯函数（深/浅各一套，可回归断言）；
  `GridItem` 新增 `CarrierPower`/`VacuumOpen` 开关记忆，切主题时 `RefreshOffBlockColors`
  当场重算全部块色（不等下一轮 1s 采集），`Configure` 初始化同样按当前主题走。
- 公共参数窗"保存设置"按钮：深色下 `DimGray` 底白字（`CommonParameterForm.ApplyTheme`，
  纯函数 `GetSaveButtonThemeColors` 可断言；浅色保持原生默认样式不动）。
- 主页区域调整窗预览画布：深色下纯黑底（`HomeLayoutEditorForm.ApplyTheme`，
  纯函数 `GetPreviewBackColor` 可断言；各区域色块自带浅底+块内文字，黑底安全）。
- 版本说明：`MessageBox` 换成自定义窗体（`MainForm.BuildVersionInfoDialog`），
  文本框跟主题走深、"确定"按钮深色 `DimGray` 底白字；内容与旧版一致。

### 为什么这么改
- 浅灰底（211）在深面板上太跳，用户指定参考停止按钮改深；开块（绿）/故障（红）等
  饱和语义色深浅两边都清晰，不动。系统 MessageBox 跟不了主题，只能换自定义窗。

### 验证
- 回归补 6 条（关块/保存钮/预览底深浅配色断言）；`build_and_test.ps1` 全绿；
  深色网格离屏渲染下电/真空关=105,105,105，公共参数/布局预览/版本说明三窗截图目检。

## V1.60.1 — 深色下停止/复位两按钮改深灰底白字（2026-09-07）

### 改动范围
- 主窗体"停止运行（选中台）/报警复位（选中台）"两按钮：深色模式下底色改
  `DimGray`、文字改白色（跟登录窗/改密窗"取消"按钮同款）；浅色下保持系统默认灰底黑字不变。
- 配色决策抽成 `MainForm.GetOperationButtonThemeColors(bool, out, out)` 纯函数，
  启动与主题切换后由 `ApplyOperationButtonsTheme()` 应用（切换入口只有主题按钮一处）。

### 为什么这么改
- 它俩浅色是"无语义的默认灰"，深色下黑字在深界面里偏弱；其余绿/蓝/红按钮本身就是
  语义色、深浅两边都清晰，所以只动它俩（用户指定）。

### 验证
- 回归补 2 条（深/浅配色断言）；`build_and_test.ps1` 全绿；深色主窗体截图目检 + 按钮底像素采样。

## V1.60 — 全界面深色/浅色主题切换（2026-09-07）

### 改动范围
- 新增 `Services/ThemeManager.cs`（静态主题服务）：App.config 存 `AppTheme`（Light/Dark，
  大小写兼容、写错兜底浅色）；`Toggle()` 切换 + 保存；`ApplyTo(窗体)` 递归着色；
  `ApplyToAllOpenForms()` 切换时全刷已打开窗体（含非模态通讯测试/送风机测试）。
- 主窗体菜单栏 4 列→5 列，"关于"右侧新增"深色模式"按钮（绿底白字与其余菜单一致；
  文字永远表示下一次点击的目标：浅色下显示"深色模式"，深色下显示"浅色模式"）。
- `Views/WorkstationGridView.cs` 新增 `SetDarkMode(bool)`：面板底/值框/文字/边框/
  行选按钮走深色档，上电绿/故障红/繁忙黄等语义状态色两边不动；切主题重算全部面板底。
- 全部子窗体打开前 `ApplyTo`（主窗体 13 处 + 录入批号→ID绑定/设置→布局编辑器/
  用户管理小窗/设置窗两个编辑弹窗/通讯测试映射提示窗）；下拉弹出窗与"连接中"提示窗同理。
- App.config 新增 `AppTheme=Light`（默认浅色，跟现行界面一致；设置窗保存只动已知 key，
  主题值不会被洗掉，已验证保存路径）。

### 为什么这么改
- 现场暗光车间/夜班长时间盯屏，白色界面刺眼易疲劳；深色模式是通用诉求，一次做全
  （主窗体 + 14 个子窗/弹窗 + 自绘画布），不留"一半深一半浅"的半成品。
- 着色用"双向映射表 + 语义色保留"而不用"快照恢复"：运行时状态色（通讯红/绿、
  权限红/蓝/绿、按钮绿/蓝/红/灰）两边都不在表里，切来切去永远不动；
  按钮一律不动（全是业务语义色）；自绘圆形灯/布局预览画布跳过（自绘，不管硬改坏事）。

### 验证
- 回归新增 `ThemeManager` 模块 30 条（Parse/映射往返/内存切换/整树着色冒烟），全量 345 绿；
- winforms-ui-debug 探针：菜单 5 列 + 按钮列位断言、登录/设置窗深色截图、网格离屏渲染
  像素采样（面板底 45,45,48 / 值框 37,37,38 / 行选 62,62,66），THEME-PROBE ALL PASS；
- 全量 `build_and_test.ps1` 通过（含真机冒烟）。

## V1.59.2 — 项目内 winforms-ui-debug skill 并入全局版本（2026-09-05）

### 改动范围
- 删除 `.opencode/skills/winforms-ui-debug/`（内容已合并进全局技能 `winforms-ui-debug`，
  含本项目方法论底稿与血泪 1〜12）；
  `AGENTS.md` 与 `agingtest-regression/SKILL.md` 中的 skill 引用改指全局版本。
- **为什么这么改**：四项目（AgingTestSystem/CommandCenter/HuaJiVision/Kaleidoscope）
  的 UI 调试 skill 合并为同一个全局版本，防分叉；本项目构建命令与对照窗体已收录进
  全局 skill 附录 A 项目档案。
- 验证：全局 skill 复用，无代码改动。
- 附带机制：AGENTS 加"改构建输出同步改全局 skill 附录 A"一行（连同 skill 内§十二/AGENTS 保鲜约定，三处互为备份，防表漂移）。

## V1.59.1 — 主界面操作区精简：删除 3 个冗余按钮（2026-09-05）

### 改动范围
- 删除右侧"操作"区三个按钮：送风机定值启动（btnTemperatureControl）、
  送风机定值停止（btnFanStop）、开启真空（btnVacuum），含 Designer 声明/布局与
  MainForm.cs 三个 Click 事件处理方法；剩余 6 按钮按业务流程顺序（选配方→录批号→
  启动→停止→复位→急停）自上而下重排。
- `DeviceManager.StartFan/StopFan` 保留（自动生命周期 `UpdateFanLifecycle` 仍在用）。

### 为什么这么改
- 对照 docs/现场业务预研Plan.md 行业通识八步流程 + V1.59 自动化闭环梳理操作入口：
  - **送风机手动启停**：送风机生命周期已全自动（任一在测保持运行、全停才停机），
    手动启动重复；手动停止在测试期间会被采集循环立即重启（按钮实际无效），
    反而误导操作员以为能停风机；
  - **开启真空**：V1.59 三阶段状态机下，启动运行已自动开阀；手动"只开阀不进测试态"
    会造成阀已开但无计时/无监控/不报警的危险游离态。现场单点点动调试用
    "关于→通讯测试(IO)"即可，不需要主界面常驻入口。
- 保留的 6 个按钮完整覆盖八步流程：批量设置配方(③)、录入批号(②)、启动运行(④)、
  停止运行(⑥人工中止)、报警复位(⑧异常处置)、全部停止(⑧急停)。

### 优化点
- 操作区从 9 按钮减至 6 按钮，排列顺序即操作流程顺序，新操作员按顺序点即完成标准作业；
- 消除"手动开阀游离态"与"风机停了又自动开"两类现场困惑点；
- 维护/联调入口不变：IO 点动在"通讯测试(IO)"、风机维护在"送风机测试"窗口。

## V1.59 — 业务串联完善：三阶段老化状态机 + PASS/FAIL 结果判定 + 断电恢复（2026-08-26）

### 改动范围
- **时序安全改造（落实"未吸附固定不通电"）**：`StartTesting` 不再"开阀+载台上电"
  同时下发，改为启动只开真空阀；载台上电由采集循环在「真空压力到位 且 配方延时
  开启到」两者满足时补发。真空建立失败（`VacuumConfirmTimeoutMs` 宽限内压力始终
  不到位）则报警切断——该台全程不会带电。旧实现与 DeviceConfig 注释里的安全意图
  自相矛盾，本次修正。
- **配方参数接入编排（原来只用于显示）**：
  - 老化时长 = 工位配方"启动时间 StartTime"，未绑定/为 0 回退全局
    `MaxTestDurationSeconds`（旧版只用全局值且默认 0=不限时长，"到时完成"实际不生效）；
  - 上电前置等待 = 配方"延时开启 DelayTime"；
  - 真空到位判定/报警阈值 = 配方负压值 `NegativePressure` 优先（经工位设置/
    批量设配方窗口下发），未配置回退全局 -95kPa；
  - 全部参数在启动瞬间**定格**进本次任务，中途改配方不影响进行中的测试。
- **三阶段状态机**：新增 `AgingPhase(None/Vacuuming/Aging)` 子阶段与
  `Services/AgingSequencer.cs` 纯函数决策器（ShouldPowerOn / ShouldComplete /
  IsVacuumBuildFailed），DeviceManager 只负责执行 IO 与日志，逻辑与副作用分离。
- **完成语义与结果判定**：
  - 到时自动下电关阀 → 新状态 `DeviceStatus.Completed`（已完成·待取料，
    面板皇家蓝状态块+淡钢蓝背景，颜色可在 PanelLayout.json 覆盖）→ 日志记
    "完成·待取料(PASS)"；
  - 报警按责任分类：压力越限/真空建立失败/DI 触点=产品相关记 FAIL；
    气压表通讯失联=设备异常（标 Fault 但不判产品不合格，不拉低直通率）；
  - 手动停止=中止（回空闲、不计判定）；人工复位或重新扫码绑定后回空闲
    （扫码重绑自动清完成态，流水作业少一步操作）；
  - PASS/FAIL/设备异常标记随采集周期延续显示（修复"新数据覆盖缓存丢标记"问题），
    直到复位清除。
- **断电恢复（TestSession.json）**：有在测任务期间把定格参数快照持久化（新增
  `Services/TestSessionStore.cs` + `Models/TestSession.cs`，已 gitignore）；
  重启后弹窗询问——恢复=按快照原参数**整台重测**（断电期间产品状态未知，
  续跑无质量意义），放弃=安全关闭涉及工位的阀与电源并删快照。急停直接清快照；
  正常退出保留（耦合器 DO 保持态，物理上测试还在跑）。
- **UI**：主界面启动确认框文案按新时序重写；"报警复位"按钮升级为兼容完成态的
  "复位"（确认取件）；启动完成后自动检查待恢复任务并询问。
- **回归测试**：TestRunner 新增 4 个模块共 69 条断言（315 条全绿）：AgingSequencer
  边界族、TestSessionStore 往返/损坏容错/Clear 幂等、Completed/LastTestResult/
  RecipeNegativePressure 序列化与 Clone、**DeviceManager 端到端集成测试**（新增
  `tests/DeviceManagerIntegrationTests.cs`，Fake 气压表+Fake IO 经依赖注入构造驱动
  真实状态机，30ms 采集秒级跑完"启动→抽真空→上电→完成/报警→恢复"全生命周期）。
- **集成测试抓出并修复 2 个真实缺陷**：
  1. `IsAlarm` 调 `IsVacuumBuildFailed` 时把"是否越限"当"是否到位"传入（语义相反），
     导致真空建立超时**永不报警**——产品会卡在抽真空阶段且载台永不上电、也不提示；
     （单测用正确语义字面量无法暴露，端到端一跑即现。）
  2. "完成/报警轮"的广播数据在状态分支时还是旧状态，本轮末尾批量写缓存会把刚写入
     缓存的 Completed/Fault/PASS/FAIL 冲掉——面板"已完成""故障"闪一帧即逝、结果
     标记丢失（V1.10 以来的既有瑕疵）。修法：动作执行后同步修正本轮广播对象，
     且非测试台延续 Fault 显示直到复位。
- DeviceManager 新增依赖注入构造函数（生产入口行为不变），为编排层可测性开放。

### 为什么这么改
- 出差前按行业通识把老化业务闭环补齐（对应《docs/现场业务预研Plan.md》缺口
  G3/G4/G5/G6/G11），现场只需验证与微调，不再从零确认流程。

### 验证
- `build_and_test.ps1` 全绿：构建通过 → 冒烟存活 → 回归 PASS=315 FAIL=0
  （含 DeviceManager 端到端集成测试 34 条）。

## V1.58.23 — 测试体系落地：新增项目专属测试验证技能 agingtest-regression（冒烟 + 246 条回归用例）（2026-08-25）

### 改动范围
- 新增 `.opencode/skills/agingtest-regression/` 项目专属最终测试验证技能：
  - `scripts/build_and_test.ps1`：一键流水线"构建 → 真机冒烟 → 全量回归"，退出码区分阶段
    （1=构建失败 / 2=冒烟失败 / 3=回归失败）；
  - `scripts/smoke_test.ps1`：真机冒烟——启动 bin\Debug 的 exe，轮询存活 18 秒
    （设备连接超时导致真实启动需 10~15s）判定未崩溃，输出 CPU/内存并正常关闭；
  - `scripts/run_unit_tests.ps1`：把构建产物拷到 `%TEMP%\opencode\agingtest-run` 隔离
    run 目录（清掉运行时 json），csc 编译 harness 后运行，退出码透传；
  - `tests/TestRunner.cs`：自研 Check/CheckThrows/Module 断言框架 + **246 条断言**，
    覆盖 11 个模块：PasswordHasher（PBKDF2 格式/盐随机/损坏串静默）、UserManager
    （登录边界/账号管理保底/改密链/权限矩阵/记住登录/损坏回退重建/缺角色补齐/双管理员防呆）、
    SettingsForm 配置归一化（StopBits/Parity 反射测私有方法，含中文与非法值兜底）、
    IoOutputChannelRemap（脏输入逐项跳过汇总）、ParseFanIpCandidates、RecipeStorage 往返容错、
    TestEventLogger（CsvEscape/表头/字段格式）、AppLogFileWriter（UTF-8/8 线程并发一条不少）、
    PanelLayoutConfig（默认基准坐标/ResolveAnchors 幂等零漂移/宽高锚定联动推导/SaveDefault 重载零差异）、
    HomeLayoutConfig、模型 JSON 往返与 Clone 深拷贝；
  - `SKILL.md`：用法、覆盖范围表、加用例步骤与 10 条踩坑清单。
- 明确覆盖边界（SKILL.md 写明）：真串口/真设备通讯与 UI 弹窗分支不在自动化范围，
  像素级渲染走 winforms-ui-debug 技能。

### 为什么这么改
- 此前项目无任何可重复执行的自动化测试，验证只靠"构建通过 + 手工点界面"，回归成本高、
  无法保证"绝对稳定"的交付要求；
- 把冒烟与用例沉淀成脚本+技能后，每次改动一键验证全绿才交付，且新用例有强制沉淀位置，
  测试资产可持续积累而不是散落在临时目录里。

### 验证过程中发现的"bug"均定性为用例自身设计错误并已修正（产品代码零改动）
- UserManager 用例身份串扰（Login 成功顶掉 CurrentUser 身份 / 实例内存快照不同步需 new 新实例）；
- CsvEscape 期望字符串少写一个翻倍引号；CSV 可选字段全空行尾应为 ",,"；
- AppLog 文件读取须 FileShare.ReadWrite（产品 StreamWriter 常驻句柄是设计行为）；
- 右对齐锚定期望值算错（正确公式 X = 目标右缘 − 自身宽）。以上全部沉淀进 SKILL.md 踩坑清单。

### 配套约定（写入 AGENTS.md）
- 改完代码必须跑 build_and_test.ps1 全绿才能交付；修 bug 必先加复现用例（红→修→绿）；
  新用例/冒烟一律回流 agingtest-regression 技能目录，禁止散落别处。

## V1.58.22 — 用户密码哈希存储：新增 PasswordHasher（PBKDF2），Users.json 不再明文（2026-08-10）

### 改动范围
- 新增 `Services/PasswordHasher.cs`：静态密码哈希工具（PBKDF2-HMAC-SHA256，.NET Framework
  自带 `Rfc2898DeriveBytes`，无第三方依赖）。
  - 存储格式 `PBKDF2$迭代次数$盐(Base64)$哈希(Base64)`，盐（16 字节随机）与迭代次数
    （10 万次）随哈希自描述保存，同一密码每次哈希结果不同；
  - `Verify` 恒定时间比较（`FixedTimeEquals`）；项目未上线、无旧版明文，存储串非哈希格式一律判失败。
- `Services/UserManager.cs`：
  - 登录 `Login`、修改密码 `ChangeOwnPassword`（验证旧密码 + "新旧相同"判断）、管理员改密
    `UpdatePassword`、新增账号 `AddAccount`、默认/兜底账号全部改为走哈希（`Hash` 写入、`Verify` 比对）。
- `Models/UserAccount.cs`：`Password` 字段注释改为"PBKDF2 哈希字符串，非明文"。

### 为什么这么改
- 此前 Users.json 存明文密码，文件一旦泄露所有账号密码直接暴露，且复制给他人即可登录。
- 裸哈希（MD5/SHA1/SHA256）对 `123456` 这类弱密码可被彩虹表/字典秒破；PBKDF2 用随机盐 +
  10 万次迭代把暴力破解成本拉高到不可接受，是 .NET Framework 下的标准做法。

### 优化点
- "记住密码"功能（RememberedLogin.json）必须能回填明文去填充登录框，仍用 Base64 可逆编码，
  与 Users.json 的不可逆哈希是两条独立路径（该文件本就 gitignore、仅存本机）。
- 迭代次数作为哈希字符串的明文段保存，未来想提升强度只需改常量，旧密码仍可验证。
- 未上线、无需新旧兼容：代码不含明文迁移/回退分支，逻辑更简。

### 验证
- 构建通过（仅既存无关 warning CS0108）。
- 独立 harness 单测 ALL PASS：哈希不可逆、盐随机（同密码两次结果不同）、对错密码判定、
  非哈希格式存储串一律判定失败、损坏哈希串静默失败、中文/特殊字符密码。
- 集成单测 ALL PASS：新目录实例化 UserManager 生成哈希版 Users.json（`PBKDF2$`、无明文）；
  登录正确密码成功、错误密码失败；新增账号也存哈希。
- 冒烟测试（真实 exe）：启动约 10~15s（设备连接超时所致）后生成哈希版 Users.json。

## V1.58.21 — 主窗体操作日志持久化：新增 AppLogFileWriter（2026-08-10）

### 改动范围
- 新增 `Services/AppLogFileWriter.cs`：静态类，把主窗体 UI 日志（MainForm.WriteLog 输出的所有消息：
  设备启动失败/连接状态/扫码/温度告警/行全选/设置变更等）追加写入本地文件。
  - 目录：程序运行目录\Logs\；文件名：`AppLog_yyyyMMdd.log`（每天一个文件，跨天自动切换）；
  - 格式：与 UI 文本框逐行一致（`[时间戳] 消息`），UTF-8 编码；
  - 线程安全：`lock` 串行化多线程写；缓存 StreamWriter 复用、每次写入后立即 `Flush` 落盘
    （程序崩溃/断电不丢已写日志）；写失败静默不影响主流程。
- `Views/MainForm.cs` `WriteLog`：追加调用 `AppLogFileWriter.Write(logLine)`，替换原 TODO 预留点位。
- `AgingTestSystem.csproj`：新增 `<Compile Include="Services\AppLogFileWriter.cs" />`。

### 为什么这么改
- 此前 UI 日志只写 `txtLog` 文本框（纯内存），重启即丢失；设备启动失败、连接异常、扫码、告警等
  操作记录无法追溯。
- 测试事件日志（TestEventLogger → Logs\TestLog_*.csv）原本就持久化，操作日志补齐后 Logs 目录
  成为完整的本地日志落点（结构化的测试事件 CSV + 自由文本的操作日志），与"历史记录查询窗体"
  各司其职。

### 验证
- 构建通过（仅既存无关 warning CS0108）。
- 冒烟测试：启动 exe → 生成 `Logs\AppLog_20260810.log`，内容含"气压表连接失败 / IO 连接失败"等
  启动日志，UTF-8 带 BOM、中文读取正常。
- 日志写失败静默（catch），不影响主流程。

## V1.58.20 — 工位面板内容居中 + 选中框与空闲块间距加大（2026-08-10）

### 改动范围
- `Models/PanelLayoutConfig.cs`（默认布局，锚定自动联动，只改 3 个边距值）：
  - **内容水平居中**：编号/标签列 `LeftMargin` 3→9（左留白 9）、设置按钮 `RightMargin` 17→9（右留白 222-213=9），
    左右对称 → 面板内内容整体居中。中间元素全走锚定自动联动：空闲/真空关右缘 213、SN/配方右缘 213、
    压力框双端定宽 85（右缘 150 与真空关左缘 153 留 3px）、下电左缘对齐压力框、延时两框左缘对齐 SN、
    "真空压力"标签右缘贴压力框左缘（65-56=9）。
  - **选中框上移**：`TopMargin` 4→2（Y=2），底缘 25 与工作状态块"空闲"上缘 29 的垂直间隔 2px→4px。
  - 各字段注释同步最终坐标（153/65/9 等）。
- `Views/WorkstationGridView.cs`：头部 ASCII 图标注与"标注说明"段更新为 V1.58.20 居中布局坐标。
- `bin/Debug/PanelLayout.json`：由 `SaveDefault()` 重新生成（完整锚定字段 + 居中坐标），覆盖现场旧版
  240 宽无锚定 json。

### 为什么这么改
- 现场界面内容偏左（标签贴左 3px、设置按钮右缘 205、右侧留白 17px），左右留白不一致观感差。
- 选中框右上角与下方"空闲"块仅相距 2px，视觉拥挤。

### 优化点
- 居中不写死坐标：左边界（编号/标签 LeftMargin=9）与右边界（设置按钮 RightMargin=9）对称锚定，
  以后改 `PanelInnerWidth` 左右各留 9px 自动保持居中。
- 现场 json 从落后多个版本的旧格式升级为 V1.58.19 完整锚定格式（垂直链/双端锚定全部生效）。

### 验证
- 构建通过（仅既存无关 warning CS0108）。
- harness 解析校验 PASS：左留白 9 = 右留白 9（设置按钮右缘 213）；选中框 Y=2、底缘 25、空闲块上缘 29、间隔 4px。
- 像素级验证（144DPI 离屏渲染）PASS：设置按钮右缘物理 x=322、右留白物理 14px（=9 逻辑）；选中框与空闲块绿底间隔物理 9px。
- 冒烟测试进程存活。

## V1.58.19 — 垂直锚定（自下而上链，只声明关系、布局零变化）：设置按钮下缘锚定面板 + 下排元素以基准间距叠加 + 延时两行居中（2026-08-10）

### 改动范围
- `Models/PanelLayoutConfig.cs`：
  - `ElementRect` 新增 4 个垂直锚定字段（可空，兼容旧配置）：
    - `BottomMargin`：下缘距面板下缘 → Y = 面板高 - BottomMargin - 高（设置按钮设 10，保持原 Y=145）。
    - `BottomToTopAlignTo` + `BottomToTopGap`：下缘位于目标元素上边缘上方 Gap 处 → Y = 目标.Y - 自身高 - Gap（Gap=0 才紧贴；取当前实际空隙即可保持布局不变，构成自下而上垂直链）。
    - `VerticalCenterAlignTo` + `CenterOffsetY`：垂直居中对齐目标，可加偏移（正下负上）用于对称分布。
    - `RightToLeftGap`：右缘与目标左缘的间隙 px（配合双端锚定/单独 RightToLeftAlignTo，默认 0 紧贴）。
  - `ElementPoint`（标签）新增 `VerticalCenterAlignTo` + `VerticalCenterOffset`：文字垂直居中对齐目标框，用新增 `LabelTextHeight`（默认 12，依赖 9pt 微软雅黑字体）算文字高。
  - `ResolveRight` 支持 `BottomMargin`；`AlignSelf` 支持 `BottomToTopAlignTo`（含 Gap）/`VerticalCenterAlignTo`/`RightToLeftGap`；`ResolveLabel` 支持标签垂直居中。
  - `ResolveElementAlign` 重排为"垂直链优先"：设置按钮 → 配方 → SN → 真空关/压力框 → 空闲/下电 → 延时两行。
  - 类头锚定总览改写为"横纵双向链"，并强调**核心原则：锚定只改声明、不改位置**（间距/边距取当前实际空隙，解析结果与 V1.58.18 完全一致）。
- 默认布局：**坐标全部与 V1.58.18 相同，未移动任何元素**，只补锚定声明：
  - 设置按钮 (145,145)：BottomMargin=10（下缘 195 距面板底 10px）。
  - 配方框 (57,118)：下缘以设置按钮上缘为基准、BottomToTopGap=6。
  - SN 框 (57,93)：下缘以配方框上缘为基准、BottomToTopGap=4。
  - 真空关/真空压力框 (145,67)/(57,67)：下缘以 SN 框上缘为基准、BottomToTopGap=5；**真空压力框补
    RightToLeftGap=3 恢复 V1.58.9 的 3px 间隙**（V1.58.15 双端锚定把宽算成 88 紧贴真空关左缘，现改回 85、
    右缘 142 与真空关左 145 留 3px）。
  - 延时开启/到达值框 (57,147)/(57,172)：以设置按钮中心为基准，CenterOffsetY=-12/+13 对称分布（因整数除法截断 0.5px，两偏移相差 1 才与手工坐标完全一致）。
  - 五标签 Y=70/96/121/150/175 不变：以各自框中心为基准、VerticalCenterOffset=-1。
- `Views/WorkstationGridView.cs`：头部 ASCII 图与坐标标注更新为"V1.58.19 加锚定后坐标不变"，V1.58.19 说明段落改写。
- `bin/Debug/PanelLayout.json`：同步全部新字段，坐标保持 V1.58.18 值。

### 为什么这么改
- 之前 Y 方向仍是一堆绝对坐标（V1.58.6~1.58.9 手工对齐），改面板高度要逐行重算。
- 现在用"以某元素为基准 + 间距"声明垂直关系：设置按钮锚定面板下缘（BottomMargin），配方/SN/真空关/压力逐级锚定上一级上缘（Gap 保留原空隙），延时两行锚定按钮中心——**当前界面零变化**，但改 `PanelInnerHeight` 时整条纵链按各自间距自动联动（实测 205→215 全部 +10 下移，间距恒定）。
- 延时两行从"手工 Y=147/172"改为"以设置按钮中心对称分布"，以后调按钮位置两行自动跟随居中。

### 验证
- 构建通过（仅既存无关 warning CS0108）。
- harness 解析校验：全部矩形/标签坐标与 de19706（V1.58.18）基准**逐项相等**（Y=67/93/118/147/172/145、标签 70/96/121/150/175）；联动测试 PanelInnerHeight 205→215 纵链全链 +10 同步、间距不变。
- 冒烟测试通过。

### 现场升级提示
- **现场若已有旧版 PanelLayout.json**：其中没有 BottomMargin/BottomToTopGap 等新字段（反序列化为 null）→ 垂直锚定不生效（但布局照旧，无副作用）。需同步本版本 json 或删除该文件让程序重新导出默认配置（见 V1.51 起 `LoadOrDefault` 回退逻辑）。

## V1.58.18 — 锚定机制文档补全（2026-08-10）

### 改动范围
- `Models/PanelLayoutConfig.cs` 类头注释：新增**【锚定机制总览】**大段——锚定字段总表（ElementRect 6 项 + ElementPoint 5 项）、三步解析流程（ResolveRight→ResolveElementAlign→ResolveLabelAnchors）与依赖顺序铁律、当前完整锚定依赖链（View 右缘→设置按钮→右对齐组→压力框→下电→标签列）、调整指南、注意事项/常见坑（标签 Width 依赖字体、字段互斥、json 与代码默认一致、Y 方向底部锚定待补等）。
- `Views/WorkstationGridView.cs` 头部注释：补充锚定机制指引（字段摘要 + 指引到 PanelLayoutConfig 类头总览）。
- `AGENTS.md`：新增"自绘控件坐标一律用锚定，禁止孤岛绝对坐标"约定（含字段清单、三步解析铁律、改坐标前必读指引），供后续 AI 自动遵守。

### 为什么这么改
- 锚定机制经 V1.58.13~1.58.17 连续演进已较复杂（6+5 个锚定字段、三步解析、链式依赖），代码注释分散在各字段/方法里，缺一份"总览级"说明，后续维护（含 AI 改布局）极易顺序错/字段配错导致错位且难查。
- 按项目"注释要详细到小白能看懂 + 新约定沉淀 AGENTS.md"规范，把机制集中成可读文档。

### 验证
- 纯文档改动，构建通过，无逻辑变更。

## V1.58.17 — 边缘锚定补齐：编号左上、选中框右上、延时值框左缘（2026-08-10）

### 改动范围
- `Models/PanelLayoutConfig.cs`：
  - `ElementRect` 新增 `TopMargin`（Y=距面板上缘距离，配合 RightMargin 构成右上角锚定）；`ElementPoint` 新增 `LeftMargin`/`TopMargin`（左上角锚定，如编号）。
  - `ResolveRight` 扩展处理 `TopMargin`；`ResolveLabel` 增加 `LeftMargin`/`TopMargin` 解析（先边缘锚定，再被 RightToLeftAlignTo/LeftAlignTo 覆盖），`ResolveLabelAnchors` 纳入编号。
  - 锚定关系：编号 `TitlePosition` 加 `LeftMargin=3`+`TopMargin=4`；选中框加 `TopMargin=4`（配 RightMargin=5）；延时开启/到达值框补 `LeftAlignTo="SNValue"`（此前完全未锚定，值框列移动时不跟随）。
- `bin/Debug/PanelLayout.json`：同步字段。
- `Views/WorkstationGridView.cs`：头部 ASCII 图与说明同步。

### 为什么这么改
- 用户要求编号锚定 View 左/上边缘、选中框锚定 View 右/上边缘，并全面检查是否所有控件都已锚定、能随 View 尺寸自适应。
- 全量盘点：X 方向仅"延时开启/到达值框"未锚定（已补）；Y 方向此前全部元素固定 Y。本次补齐编号/选中框边缘锚定，使左上角与右上角元素在面板尺寸变化时保持相对位置。

### 验证
- harness 自检：编号 X=3/Y=4（左上）✓；选中框右缘=面板宽-5、Y=4（右上）✓；延时框左缘==SN 左缘 ✓；面板宽 260 时设置按钮右=243、选中框右=255、空闲/SN 右=243、编号 X 仍 3、延时框左缘跟随 SN、压力框右缘=真空关左缘 ✓。
- 构建通过，冒烟测试进程存活。

> 遗留：中部/底部元素（设置按钮 Y=145、延时到达 Y=172 等）Y 仍为固定坐标，未做底部锚定；面板高(205)当前固定无需处理。若日后需面板高可调，再补 `BottomMargin`（设置按钮/延时到达锚下缘 + 延时两行保持垂直居中）。

## V1.58.16 — 标签锚定："真空压力"右缘贴压力框、其余标签左缘对齐它（2026-08-10）

### 改动范围
- `Models/PanelLayoutConfig.cs`：
  - `ElementPoint` 新增 `Width`（标签文字固定宽，配合右缘锚定）、`RightToLeftAlignTo`（右缘贴合目标左缘）、`LeftAlignTo`（左缘对齐目标）。
  - `ResolveAnchors` 增加第三步 `ResolveLabelAnchors()`；新增 `GetAnchorX`（矩形/标签统一取左缘 X）、`GetLabelByName`（标签名映射）。
  - 锚定关系：`LabelPressurePosition` 设 `Width=56` + `RightToLeftAlignTo="PressureValue"`（右缘贴压力框左缘，X=57-56=1）；`LabelSnPosition`/`LabelRecipePosition`/`LabelDelayStartPosition`/`LabelDelayArrivePosition` 设 `LeftAlignTo="LabelPressure"`（左缘对齐"真空压力"标签）。
- `bin/Debug/PanelLayout.json`：同步标签锚定字段。
- `Views/WorkstationGridView.cs`：头部 ASCII 图与说明同步。

### 为什么这么改
- 用户要求：真空压力名称右边缘锚定真空压力显示框左边缘（名称文字宽固定）；SN/配方/延时开启/延时到达名称左边缘对齐"真空压力"名称左边缘。
- 这样压力框左缘（双端锚定自 SN/真空关）一旦变化，真空压力标签自动右缘贴齐，其余标签左缘自动跟随，标签列与值框列始终对齐。

### 验证
- harness：真空压力标签右缘==压力框左缘、其余四标签左缘==真空压力标签左缘，全 True；压力框左缘变化时标签自动跟随（X=75-56=19）✓。
- 构建通过，冒烟测试进程存活。

> 提示：LabelPressurePosition.Width=56 依赖字体（微软雅黑 9pt），若改字体需同步该值。

## V1.58.15 — 双端锚定：真空压力框两端跟随、下电左缘对齐压力框（2026-08-10）

### 改动范围
- `Models/PanelLayoutConfig.cs`：
  - `ElementRect` 新增 `LeftAlignTo`（左缘对齐目标左缘）、`RightToLeftAlignTo`（右缘贴合目标左缘）。
  - `AlignSelf` 扩展：`LeftAlignTo`+`RightToLeftAlignTo` 同时设置时构成**双端锚定**，宽度自动推导（宽 = 右锚定目标左缘 - 左锚定目标左缘）；仅 `RightToLeftAlignTo` 时 X = 目标左缘 - 自身宽。
  - `ResolveElementAlign` 调整依赖顺序：设置按钮 → 右对齐组（空闲/真空关/SN/配方）→ 压力框（依赖 SN+真空关）→ 下电（依赖压力框）。
  - 锚定关系：`RcPressureValue` 改 `LeftAlignTo="SNValue"` + `RightToLeftAlignTo="VacuumOpen"`（左缘=SN 框左缘、右缘=真空关左缘，宽自动 145-57=88）；`RcPower` 新增 `LeftAlignTo="PressureValue"`（左缘对齐压力框）。
- `bin/Debug/PanelLayout.json`：同步锚定字段。
- `Views/WorkstationGridView.cs`：头部 ASCII 图与说明同步。

### 为什么这么改
- 用户要求：真空压力显示框左缘锚定 SN 框左缘、右缘锚定真空关左缘；下电左缘锚定压力框左缘。
- 真空关/SN 均右缘跟随设置按钮，压力框双端锚定后：真空关变宽/位置移动时压力框右缘与宽度自动调整，下电左缘始终与压力框左缘对齐，形成完整联动链，后续调布局无需手改。

### 验证
- harness：压力框左缘==SN 左缘、右缘==真空关左缘（间距 0）、下电左缘==压力框左缘，全 True；真空关 W 60→80 后压力框右缘自动贴合新左缘、宽度 88→68 自动缩短，下电左缘保持跟随，全 True。
- 构建通过，冒烟测试进程存活。

## V1.58.14 — 链式锚定：空闲/真空关/SN/配方右缘跟随设置按钮、下电上下边缘跟随空闲（2026-08-10）

### 改动范围
- `Models/PanelLayoutConfig.cs`：
  - `ElementRect` 新增两个可空锚定字段：`RightAlignTo`（右缘对齐到目标矩形）、`VerticalAlignTo`（Y 与 Height 取目标矩形，上下边缘对齐）。
  - `ResolveRightAnchors()` 升级为 `ResolveAnchors()`，两步解析：① `RightMargin` 直接锚定面板右缘；② `ResolveElementAlign()` 按 `RightAlignTo`/`VerticalAlignTo` 元素间对齐（含 `GetRectByName` 名称映射）。
  - 锚定关系调整：设置按钮 `RightMargin=17`（锚定 View 右缘）；空闲/真空关/SN/配方由 `RightMargin` 改 `RightAlignTo="SetButton"`（右缘跟随设置按钮）；下电新增 `VerticalAlignTo="WorkState"`（上下边缘跟随空闲）。
- `bin/Debug/PanelLayout.json`：同步锚定字段。
- `Views/WorkstationGridView.cs`：头部 ASCII 图与说明同步。

### 为什么这么改
- 用户要求更合理的层级锚定：设置按钮直接锚定 View 右缘，其余右对齐元素（空闲/真空关/SN/配方）锚定"设置按钮右缘"而非各自锚定面板——这样改设置按钮尺寸/位置时，它们自动保持右侧对齐；下电与空闲上下对齐。
- 链式锚定让"右侧对齐组"由基准元素（设置按钮）统一驱动，后续调布局只动基准，跟随元素自动联动。

### 验证
- harness：初始四者右缘均 = 设置按钮右缘(205)、下电 Y/H = 空闲(29/23) ✓；模拟面板宽 240 + 设置按钮宽 80 → 设置按钮右缘 223，空闲/真空关/SN/配方右缘自动跟随为 223、下电仍跟随空闲 ✓。
- 构建通过，冒烟测试进程存活。

## V1.58.13 — 坐标右侧锚定（RightMargin）：面板右缘元素随宽度自适应（2026-08-10）

### 改动范围
- `Models/PanelLayoutConfig.cs`：
  - `ElementRect` 新增可空字段 `RightMargin`（右侧锚定边距 px）。
  - 新增 `ResolveRightAnchors()`：把配置了 `RightMargin` 的矩形按 `X = PanelInnerWidth - RightMargin - Width` 重算 X；未设置者保持绝对 X（兼容旧配置）。
  - `LoadOrDefault()` 加载后（JSON 与内置默认均）调用 `ResolveRightAnchors()`。
  - 右缘元素改为锚定：`RcSelectBox`=5、`RcWorkState`/`RcVacuumOpen`/`RcSNValue`/`RcRecipeValue`/`RcSetButton`=17。
- `bin/Debug/PanelLayout.json`：同步补 `RightMargin` 字段。
- `Views/WorkstationGridView.cs`：头部 ASCII 图与说明同步。

### 为什么这么改
- 此前面板内坐标全是相对面板左上角的绝对像素：V1.58.11 缩面板（240→222）导致右空隙失控、V1.58.12 选中框溢出面板，每次都要手算一串 X 修正，难维护。
- 引入轻量版 WinForms Anchor：右缘元素声明"离右缘固定距离"（RightMargin），面板宽度变化时由加载期一次性解析自动跟随，绘制与命中检测逻辑零改动、运行期零开销。

### 验证
- harness：面板宽 240 时锚定元素 X 自动变为 选中框 212/SN 75/设置 163（=240-RightMargin-Width）✓；还原 222 后回到 194/57/145 ✓；各对齐（右205/左145/行全选/延时）不受影响 ✓。
- 构建通过，冒烟测试进程存活。

> 后续：如需面板高度变化自适应，可仿照加 `BottomMargin`（当前面板高 205 未变，暂不启用）。

## V1.58.12 — 选中框左移回界（2026-08-10）

### 改动范围
- `Models/PanelLayoutConfig.cs` / `bin/Debug/PanelLayout.json`：`RcSelectBox` X 212→**194**（Y=4、W=23 不变）。
- `Views/WorkstationGridView.cs`：头部 ASCII 图坐标标注同步。

### 为什么这么改
- V1.58.11 面板内容宽由 240 缩至 222 后，选中框原 X=212（右缘 235）有 13px 超出面板右侧边缘。
- 左移至 X=194，右缘 = 217，与面板右边距保持 5px（与缩面板前的观感一致），不再出界。

### 验证
- harness：选中框右缘 217 ≤ 面板内容宽 222，不超界 ✓；其余布局与对齐（V1.58.6~11）不受影响。
- 构建通过，冒烟测试进程存活。

## V1.58.11 — 撤销整体居中，改缩面板宽度减小右空隙（2026-08-10）

### 改动范围
- `Models/PanelLayoutConfig.cs` 默认值（已同步 `bin/Debug/PanelLayout.json`）：
  - **撤销 V1.58.10 居中**：所有 X 还原为 V1.58.9 布局（标签列 X=3、值框/状态块 X=57、右对齐元素 X=145，编号 X=3、选中框 X=212 不变）。
  - **缩小面板宽度**：`PanelInnerWidth` 240→**222**、`PanelColumnWidth` 245→**227**（差保持 5 = 左右边距各 2 + 边框余量 1）。
- `Views/WorkstationGridView.cs`：头部 ASCII 图与坐标标注同步。

### 为什么这么改
- 用户反馈整体居中方案不好看，指出根因不是"坐标偏左"，而是**面板本身太宽、右侧留白太多**。
- 内容实际占用 X=3~205，原面板内容宽 240 → 右空隙 35px，观感空旷偏左；把面板缩到 222 → 右空隙 **17px**，整体紧凑协调。
- 只动面板整体宽度，元素坐标（V1.58.9 布局）与相对关系零改动，命中检测、绘制逻辑不受影响。

### 验证
- harness 反射读取运行时配置：内容范围 3~205、面板内容宽 222、右空隙 17（原 35）✓；X 已还原(57/145/3) ✓；网格总宽 = 8×227+80 = 1896 ✓；五者右对齐(205)、空闲/真空关/设置左对齐(145) 均 True ✓；行全选按钮含边框 y=2~206 与面板内容对齐 ✓；编号/选中框不动 ✓。
- 构建通过，冒烟测试进程存活。

## V1.58.10 — 工位面板主内容区整体水平居中（2026-08-10）

### 改动范围
- `Models/PanelLayoutConfig.cs` 默认值（已同步 `bin/Debug/PanelLayout.json`）：除左上角编号 `TitlePosition(X=3)`、右上角选中框 `RcSelectBox(X=212)` 外，其余全部元素 **X 右移 16px**：
  - 标签列 5 个：X 3→**19**；状态块/值框/设置按钮：X 57→**73**、X 145→**161**。
  - 涉及：RcPower / RcWorkState / RcVacuumOpen / RcPressureValue / RcSNValue / RcRecipeValue / RcDelayStartValue / RcDelayArriveValue / RcSetButton / LabelPressurePosition / LabelSnPosition / LabelRecipePosition / LabelDelayStartPosition / LabelDelayArrivePosition。
- `Views/WorkstationGridView.cs`：头部 ASCII 图与坐标标注同步。

### 为什么这么改
- 用户反馈面板内容整体偏左。实测内容范围为 X=3~205，面板宽 240：左边距仅 3px、右边距 35px，视觉明显偏左。
- 内容宽 202，水平居中应左右各留 (240-202)/2=19px → 整体右移 16px（3+16=19，右缘 205+16=221，右边距 240-221=19）。
- 通过只改 X 坐标实现整体平移，元素间相对布局、绘制与命中检测逻辑零改动，V1.58.6~1.58.9 各项对齐全部保持（右对齐线 205→221、左对齐线 145→161 同步平移）。

### 验证
- harness 反射读取运行时配置：内容范围 19~221、宽 202、左/右边距均 19、居中=True ✓；编号 X=3、选中框 X=212 不动 ✓；五者右对齐(221)、空闲/真空关/设置左对齐(161) 均 True ✓；行全选按钮对齐、延时垂直居中等此前调整不受影响 ✓。
- 构建通过，冒烟测试进程存活。

## V1.58.9 — 空闲/真空关左边缘与设置按钮对齐：三者左右同界 + 下电加宽（2026-08-10）

### 改动范围
- `Models/PanelLayoutConfig.cs` 默认值（已同步 `bin/Debug/PanelLayout.json`）：
  - `RcWorkState`（空闲/繁忙/故障）：X 153→**145**、W 52→**60**（左右边缘 145/205 与设置按钮完全重合）；
  - `RcVacuumOpen`（真空开/关）：X 153→**145**、W 52→**60**（同上）；
  - `RcPower`（下电）：W 52→**60**（与空闲/真空关宽度一致，X 保持 57）；
  - `RcPressureValue`（真空压力框）：W 93→**85**（右边缘=142，与真空关左边缘 145 保持 3px 间距，避免真空关左移后重叠）。
- `Views/WorkstationGridView.cs`：头部 ASCII 图与坐标标注同步。

### 为什么这么改
- 用户要求"空闲、真空关的左边缘与设置按钮的左边缘对齐"。三者左边缘对齐且右边缘（V1.58.7 已统一 205）不变，则空闲/真空关宽度必须从 52 加到 **60**，同时 X 从 153 左移到 145，最终空闲/真空关/设置按钮三者左右边界完全一致（145~205）。
- 下电宽度与空闲/真空关保持一致（52→60），用户显式要求。
- 真空关左移 8px 后与真空压力框（原右缘 150）重叠，故压力框缩窄至 85（右缘 142），维持 3px 相邻间距。

### 验证
- harness 反射读取运行时配置：空闲/真空关左=145==设置左 ✓、右=205==设置右 ✓、W 三者均 60 ✓、下电 W=60 一致 ✓、压力框右缘 142 与真空关左缘 145 间距 3 ✓；V1.58.7 五者右对齐、V1.58.8 行全选按钮对齐不受影响 ✓。
- 构建通过，冒烟测试进程存活。

## V1.58.8 — 行全选按钮上下边缘与工作站显示框对齐（2026-08-10）

### 改动范围
- `Views/WorkstationGridView.cs` `OnPaint` 行全选按钮列：
  - 按钮高度由 `PanelRowHeight - 4`(=221) 改为 `PanelInnerHeight - 1`(=204)，Y 仍为 `row*行高+2`。
  - 配合 `DrawRectangle` 黑色边框（画在矩形下边界，底 = Y+高度），含边框后按钮范围 = 2~206，与面板内容（工作站显示框 240×205，y=2~206）上下边缘完全对齐。
  - 头部 ASCII 图与说明同步。

### 为什么这么改
- 用户要求每行"全选"按钮的上下边缘与每行工作站显示框对齐。原按钮高 221 比面板内容高 205 多 16px，底部明显凸出。
- 直接改 205 仍差 1px：`DrawRectangle` 边框线画在矩形下边界（2+205=207），按钮含边框底部会比面板底(206)多 1px。故高度取 204（边框底=2+204=206）才与面板完全重合——这是自绘边框"底边线画在 Y+Height"的像素级修正。

### 验证
- harness 离屏渲染 + 像素扫描：按钮（含黑边框）y=2~206，面板内容 y=2~206，顶部/底部对齐均 True；按钮上/下边框线像素均为黑色(0)且与面板内容边界重合。
- 命中检测 `TryHitRowButton` 按整行判断，不受按钮高度影响；构建通过，冒烟测试进程存活。

## V1.58.7 — 工位面板右上区右对齐：空闲/真空关/SN框/配方框/设置按钮右边缘统一 205（2026-08-10）

### 改动范围
- `Models/PanelLayoutConfig.cs` 默认值（已同步 `bin/Debug/PanelLayout.json` 运行时配置）：
  - `RcWorkState`（空闲/繁忙/故障）：X 138→**153**（右移 15px，右边缘=205）；
  - `RcVacuumOpen`（真空开/关）：Width 48→**52**（与空闲一致）、X 138→**153**（右边缘=205）；
  - `RcSNValue` / `RcRecipeValue`：Width 140→**148**（右边缘=205）；
  - `RcPressureValue`（真空压力框）：Width 78→**93**（右边缘=150，与真空关左边缘 153 保持 3px）；
  - `RcSetButton`（设置按钮）不动；`RcPower`（下电）不动。
- `Views/WorkstationGridView.cs`：头部 ASCII 图与坐标标注同步。

### 为什么这么改
- 用户要求"空闲/真空关/SN框/配方框/设置按钮"五者右边缘对齐，并确认对齐基准为设置按钮右边缘 205；
- 真空关原宽 48 与空闲 52 不一致，按用户要求统一为 52；真空关右移后与左侧真空压力框间距变大，按用户授权把压力框加宽至 93 补齐间距（保持 3px）；
- 校验：下电块(52×23)与空闲块(52×23)尺寸本就一致，无需改动。

### 验证
- harness 反射读取运行时配置：五者右边缘全部 = 205 ✓；真空关 W(52)==空闲 W(52) ✓；下电与空闲尺寸一致 ✓；压力框右缘 150 与真空关左缘 153 间距 3 ✓；延时两行与设置按钮垂直居中对齐（V1.58.6）不受影响 ✓。
- 构建通过，冒烟测试进程存活。

## V1.58.6 — 工位面板延时两行下移：与"设置"按钮垂直居中（2026-08-10）

### 改动范围
- `Models/PanelLayoutConfig.cs` 默认值（同时已同步 `bin/Debug/PanelLayout.json` 运行时配置）：
  - `RcDelayStartValue.Y` 143→**147**、`RcDelayArriveValue.Y` 168→**172**（两行各下移 4px）；
  - `LabelDelayStartPosition.Y` 146→**150**、`LabelDelayArrivePosition.Y` 171→**175**（标签与值框同步下移）。
- `Views/WorkstationGridView.cs`：头部 ASCII 图与坐标标注同步更新（含对齐关系说明）。

### 为什么这么改
- 用户反馈工位面板里"延时开启/延时到达"两行与右侧"设置"按钮相比整体偏上、不齐。
- 像素实测：两行整体中心 = (153.5+178.5)/2 = **166**，"设置"按钮中心 = 145+25 = **170**，差 4px —— 两行确实偏高。
- 下移 4px 后两行中心=设置按钮中心=170，视觉居中对齐；延时到达值框底 193 < 面板高 205，不溢出。

### 验证
- harness 离屏渲染 + 像素扫描：下移后两行中心=170.0、设置按钮中心=170.0、差=0.0 ✓；各标签与值框自身偏差仍为 0.5px（与其余行一致）；值框不越界 ✓。
- 构建通过，冒烟测试进程存活。

> 现场提示：若现场程序目录已有旧版 `PanelLayout.json`（会覆盖代码默认值），需同步修改其中 Y 值或删除该文件让程序重新导出默认配置。

## V1.58.5 — 版本说明对话框内容更新（2026-08-10）

### 改动范围
- `Views/MainForm.cs` `MenuHelpVersionInfo_Click`：重写"关于 → 版本说明"对话框文本。
  - 版本号由过时的 **V1.16** 更新为 **V1.58.4**（与 CHANGELOG 对齐）。
  - 内容按商用软件"关于/版本说明"通用规范重组：软件全称 → 版本号(Build 日期) → 用途简介 → 运行环境（精简为一行）→ 功能特性（监控与控制 / 老化测试业务 / 生产与工程 三分类）→ 版权声明。
  - 功能列表补齐最新能力：扫码枪自动扫码、工位↔SN 绑定、配方管理/批量下发、IO 耦合器控制、送风机温湿度监视与 IP 自动识别、通讯/送风机测试工具、配置与布局可视化定制等。
  - 实现方式改为 `string.Join("\n", 字符串数组)`，便于后续维护增删。

### 为什么这么改
- 原版本说明长期停留在 V1.16，与实际版本（V1.58.4）严重脱节，功能列表也缺大量后续新增能力，现场追溯版本/排查时会产生误导。
- 商用软件版本说明通常包含"产品用途 + 运行环境 + 功能概要 + 版权法律信息"四要素，按此规范重组使内容完整且便于阅读。

### 验证
- 构建通过（仅文本改动，无逻辑/控件变更）。

> 备注：主窗体标题 `lblTitle` 与窗体标题 `this.Text`（MainForm.Designer.cs）仍显示"老化测试系统V1.16"，尚未同步，待用户确认后一并处理。

## V1.58.4 — 主页区域编辑器：修复 150% 高分屏下窗体不缩放（2026-08-10）

### 改动范围
- `Dialogs/HomeLayoutEditorForm.cs` 构造函数：
  - 补上 `AutoScaleDimensions = new SizeF(6F, 12F)`（此前只设了 `AutoScaleMode.Font`，WinForms 默认以 96DPI 为基准不缩放，高分屏下整体偏小）。
  - 用 `SuspendLayout()` / `ResumeLayout(false)` 包裹全部控件创建，并在 `ResumeLayout(false)` 前完成 `AutoScaleDimensions` + `AutoScaleMode` 的设置——**这是本次修复的关键**（详见下）。

### 为什么这么改
- **根因（实测定位）**：纯代码窗体若在未挂起布局（`SuspendLayout`）时逐次 `Controls.Add`，每次添加都会触发 `PerformLayout → PerformAutoScale`，而此时 `AutoScaleDimensions` 尚未按实际 DPI 计算生效，WinForms 会把它固化成当前 DPI 值（144 DPI 下变成 9×18 = 6×12×1.5），导致"设计基准 == 运行基准"、缩放因子恒为 1，窗体永远不放大。Designer 窗体（如 SettingsForm）天生带 `SuspendLayout`，所以一直正常；纯代码窗体必须手动补齐。
- 加上 `AutoScaleDimensions(6F,12F)` 后，WinForms 在 `ResumeLayout` 时才以 96DPI 设计基准与运行 DPI 比较，144 DPI 下缩放 1.5 倍，窗体、输入面板、预览区、按钮全部等比放大，与 `app.manifest`/`App.config` 的 PerMonitorV2 配合。

### 验证
- harness（PerMonitorV2 manifest + App.config 开关，系统 144 DPI / 150%）：
  - 修复前：ClientSize=640×520 不缩放（构造后 AutoScaleDimensions 被固化为 9×18，与 Current 相同）。
  - 修复后：ClientSize=960×780（640×1.5）、pnlValues=222（148×1.5）、preview=441，全部正确放大 1.5 倍。
  - 96 DPI 下缩放因子=1，窗体保持 640×520 不缩放（由缩放公式保证，逻辑一致）。
- 构建通过。

## V1.58.3 — 主页区域编辑器：修复底部行被裁剪 + 权限放开为所有用户（2026-08-10）

### 改动范围
- `Dialogs/HomeLayoutEditorForm.cs`：数值输入面板 `pnlValues` 高度 120→**148**，并显式设置 4 行 `RowStyle(Absolute, 34)`——修复第 4 行（"状态栏高"输入框）控件底部被面板裁剪的问题（实测修复前 Y=111+28=139 > 120 面板高，数字输入框只显示上半截）。
- `Views/MainForm.cs` `btnAbout_Click`：移除"主页区域调整"菜单项的管理员判断，所有登录用户可见；`MenuHelpHomeLayout_Click` 同步移除管理员兜底校验。

### 为什么这么改
- **裁剪根因**：pnlValues 是 4 行 TableLayoutPanel，每行需容纳 Label(≈26px)+NumericUpDown(≈28px)+上下 margin，行高至少 34px，4 行共需 148px；原高度 120px（每行 30px）装不下，第 4 行溢出被裁剪。显式行高还避免不同字体/DPI 下 AutoSize 差异导致再次溢出。
- **权限放开**：布局微调（右侧宽度/行高/状态栏）属非关键个性化操作，现场操作员也可能需要按自己习惯调整，故与用户确认后放开为所有用户，与"系统设置"（仍仅管理员）区分开。

### 验证
- harness（PrintWindow 截图 + 控件矩形映射扫描）：修复后第 4 行 NumericUpDown 完整位于面板内（内容行 first=3/last=22 于高度 28 内，未裁剪）；4 个输入框全部在 pnlValues(148) 内。
- 构建通过，冒烟测试进程存活。

## V1.58.2 — 右侧宽度默认值改由 MainForm 写死，HomeLayoutConfig 恢复原样（2026-08-10）

### 改动范围
- `Models/HomeLayoutConfig.cs`：**恢复默认值**（`RightPanelWidth=260`），不再承担右侧宽度的"默认值"职责——默认值改由 MainForm 负责。
- `Views/MainForm.cs`：
  - 新增常量 `public const int DefaultRightPanelWidth = 300`，写死在 MainForm 里（右侧状态按钮区宽度的默认值，现场嫌 260 太小，调大到 300）。
  - `AdjustRightPanelWidth()`：默认用 `DefaultRightPanelWidth`；**仅当现场保存过 HomeLayout.json 时**（`File.Exists(GetConfigPath())`）才读配置文件的 `RightPanelWidth`（用户自定义优先）。
  - `MenuHelpHomeLayout_Click`：打开编辑器前，若未配置过 json，则把 `layout.RightPanelWidth` 补成 `DefaultRightPanelWidth`，避免编辑器显示 260、主界面实际 300 的偏差。
- `Dialogs/SettingsForm.cs`：新增 `GetEffectiveHomeLayout()` 辅助方法——未配置 json 时右侧宽补成 `MainForm.DefaultRightPanelWidth`（300）；"主页区域"行的摘要显示、点击编辑、编辑器初始化三处统一走该方法。
- `Dialogs/HomeLayoutEditorForm.cs`：`BtnRestore_Click` 恢复默认时，右侧宽恢复为 `MainForm.DefaultRightPanelWidth`（300）而非 `HomeLayoutConfig` 类默认（260），与主窗体未配置时的生效值一致。

### 为什么这么改
- 用户明确要求：右侧宽度的默认值不要放 HomeLayoutConfig.cs，直接写死在 MainForm（调整时只需改一个数字）。
- HomeLayoutConfig.cs 恢复为"纯配置文件模型"：类默认 260 只作为反序列化兜底，真正生效默认由 MainForm 常量控制。编辑器/设置里三处入口统一走"配置存在→配置值，否则→MainForm 默认"的逻辑，杜绝显示值与实际值不一致。

### 验证
- 场景1（无 json）：MainForm Panel2.Width=300；编辑器打开右侧输入框经入口修正后 300；恢复默认=300；SettingsForm 摘要入口=300。
- 场景2（有 json RightPanelWidth=250）：MainForm Panel2.Width=250（配置优先）。
- HomeLayoutConfig 类默认保持 260，不干扰。
- 冒烟测试进程存活。

## V1.58.1 — 主页布局默认尺寸调大（标题栏/菜单栏/状态栏增高）（2026-08-10）

### 改动范围
- `Models/HomeLayoutConfig.cs`：顶部标题栏/菜单栏/状态栏默认高度调大——30→**40**、40→**50**、25→**30**（右侧宽度默认 260 维持原值，V1.58.2 起右侧默认改由 MainForm 常量 300 负责，见下）；Range 上限同步放宽（标题栏 80、菜单栏 100、状态栏 60），保证默认值在可拖范围内仍有上调余量。
- `Views/MainForm.cs` `ApplyHomeLayout()`：菜单栏加高后，4 个菜单按钮高度同步填满菜单栏（按钮高 = 菜单栏高 − 12，上下各留 3px 边距），避免按钮仍是固定 28px、底部留空白。

### 为什么这么改
- 现场反馈"标题栏和顶部标题栏太小"——主界面顶部信息行（标题/权限/通讯状态）和菜单栏（用户权限/参数/日志/关于）默认高度偏低，按钮和文字显得挤、不易点按。
- 可视化编辑器（HomeLayoutEditorForm）本身设计不变：四个区域都仍可拖边缘定制；本次只是把"未配置 HomeLayout.json 时"的默认值调大，让现场开箱即用更舒适。

### 验证
- harness：默认配置 TopBar=40/Menu=50/Right=300/Status=30；MainForm 应用后 Row0=40、Row1=50、Row3=30、Panel2.Width=300；菜单按钮高 38（50−12）恰好填满菜单栏（44=50−6 面板边距）。
- 冒烟测试进程存活。

## V1.58 — 主页布局外部化：右侧区域可配置 + 可视化拖拽编辑器（2026-08-10）

### 改动范围
- 新增 `Models/HomeLayoutConfig.cs`：主页布局配置模型（标题栏高/菜单栏高/右侧区宽/状态栏高），默认 `TopBarHeight=30`、`MenuHeight=40`、`RightPanelWidth=260`、`StatusBarHeight=25`；`LoadOrDefault`（缺文件/损坏回退默认）、`Save`（UTF-8 写 `HomeLayout.json`，程序目录）、各项带 Range 约束（标题栏 15~60、菜单栏 25~80、右侧区 180~600、状态栏 15~50），超范围钳制。
- 新增 `Dialogs/HomeLayoutEditorForm.cs`：**"主页区域调整"可视化编辑器**（管理员入口）——
  - `HomeLayoutEditorForm`：预览控件 + 四个 NumericUpDown（值双向同步）、恢复默认/保存/取消按钮；保存写 `HomeLayout.json`，`DialogResult=OK`。
  - `HomeLayoutPreviewControl`：自绘预览控件（固定 1400×900 逻辑坐标系，按视口等比缩放居中），四个区域用不同底色矩形块显示（标题栏灰/菜单栏蓝/主体白/状态栏橙），四条边缘可拖（标题栏下边/菜单栏下边/右侧区左边缘/状态栏上边），命中容差 6 逻辑像素，拖动时实时回写配置并刷新。自绘坐标全部按 `_dpiScale` 缩放，符合 V1.55 高 DPI 约定。
- `Views/MainForm.cs`：
  - `AdjustRightPanelWidth()` 改为配置驱动：`SplitterDistance = splitContainerMain.Width − RightPanelWidth − SplitterWidth`，不再写死；默认 260 使右侧区域（运行状态/监视/操作/日志）整体比原 326 缩窄。
  - 新增 `ApplyHomeLayout()`：按配置设置 `tableLayoutPanelMain` 三个行高（标题栏/菜单栏/状态栏）+ 调 `AdjustRightPanelWidth()`；保存配置后热生效。
  - 新增 `ResizeOperationButtons()`：操作区按钮宽度自适应 `groupBoxOperation` 宽度（−30，下限 80），并订阅 `groupBoxOperation.SizeChanged`（右侧区缩窄后操作按钮不溢出）。
  - "关于"下拉菜单新增"主页区域调整"项（管理员可见，与"系统设置"同级），`MenuHelpHomeLayout_Click` 打开编辑器，保存后 `ApplyHomeLayout()` + 写操作日志。
- `Dialogs/SettingsForm.cs`：新增"主页区域"分类 + key `HomeLayout`（该值存 `HomeLayout.json`，非 App.config），`GetEffectiveValue`/`CreateValueCell`/`Grid_CellClick` 支持该 key 展示摘要（右侧区域 260px | 标题栏 30px | 菜单栏 40px | 状态栏 25px），点击弹"主页区域调整"编辑器；`btnSave_Click` 跳过该 key（不污染 App.config）；新增 `HomeLayoutChanged` 属性，`MenuHelpSettings_Click` 打开设置保存后据此在 MainForm 重新应用布局。

### 为什么这么改
- 现场希望右侧区域（运行状态/监视/操作/日志）整体按比例缩小，多让出空间给工位网格；但各现场对标题栏/菜单栏/状态栏高度的偏好不同，硬编码尺寸每次都要改代码重编译。
- 参考 V1.51 `PanelLayoutConfig`（工位网格布局外部化到 `PanelLayout.json`）的成功套路，把主界面四个区域尺寸也做成"配置文件 + 可视化编辑器"，现场无需重编译即可微调布局。
- 编辑器用"矩形块 + 拖边缘"而非纯数字输入：现场操作员对"拉边界"比"填数字"更直觉，且拖拽同时看到整体比例变化。

### 优化点
- 右侧区默认宽 260（原约 326），工位网格可视面积扩大约 20%。
- 操作按钮自适应宽度，右侧区缩窄后按钮不挤占、不溢出（最小 80px 保证可点）。
- 布局全部数据驱动：`ApplyHomeLayout`/`AdjustRightPanelWidth`/`ResizeOperationButtons` 不再含硬编码像素。

### 验证
- harness 验证：默认 `RightPanelWidth=260`（<326）✓；`HitTest` 命中右边缘=3 ✓；MainForm `ApplyHomeLayout` 后 Panel2.Width≈240、三行高 30/45/28 ✓；操作按钮宽 204 自适应 ✓；右侧分组完整 ✓。
- 编辑器像素验证（PrintWindow 截图 + 像素扫描）：工作区浅绿 `235,250,235` ✓、状态区浅橙 `255,244,230` ✓。
- 编辑器真实交互（SendInput 真实拖拽）：按住右边缘拖 20px → `RightPanelWidth` 260→204（精确 −56 逻辑，映射比正确）✓，右侧宽输入框同步=204 ✓。
- SettingsForm：'主页区域'分类行 + HomeLayout 行摘要显示 ✓；编辑器保存写 `HomeLayout.json`（RightPanelWidth=250、MenuHeight=48 读回一致）✓。
- 冒烟测试进程存活。

## V1.57.3 — 回退画布缓存：修复 V1.57.2 引发的整软件卡死与面板"连成一片"（2026-08-10）

### 改动范围
- `Views/WorkstationGridView.cs`：**废弃 V1.57.2 的"离屏画布缓存"方案**（`_canvas`、`EnsureCanvas`、`RenderToCanvas`、`RenderPanelToCanvas` 全部删除），恢复旧版"OnPaint 只重绘可见区"；`UpdateAll`/`UpdateSingle`/`InvalidateAfterSelectionChange`/`ClearAllSelection`/`ToggleRow` 回到仅 Invalidate 的旧实现。
- **保留 V1.57.2 中仍然有效的两项优化**：① 16ms 拖拽滚动合并定时器 `_dragScrollTimer`（MouseMove 只记目标、定时器统一应用 AutoScrollPosition）；② 画刷/画笔缓存字段（`_penBorder`、`_brushValueBox`、`_brushRowSelect`、`_brushSetButton`、`_brushSelectChecked`、`_brushSelectUnchecked`）。

### 为什么这么改（V1.57.2 的教训）
- V1.57.2 把网格整体预渲染到离屏 Bitmap 想加速滚动，但实测**离屏大图（2040×2025）上 `TextRenderer.DrawText` 每处约 2.2ms**（屏幕 DC 上近 0ms），全量 72 面板渲染一次高达 **2247ms**。
- 而 `UpdateAll`（1Hz 采集刷新）每次都触发全量渲染 → **整个软件每 1 秒卡死一次**；选中/行全选等操作也走全量渲染 → 点击后"卡住不动"。这就是用户反馈的卡死根因。
- 另因 `RenderToCanvas` 里 `g.Clear(_normalColor)` 把整幅画布刷成白色，面板之间 2px 间隙（原本显示浅灰 `Control` 底色、形成"一个一个"的分隔感）被填白 → 面板看起来"连成一片"。回退后间隙恢复浅灰底色。
- 结论：离屏 TextRenderer 慢是 .NET GDI+ 固有行为，画布缓存方案在此场景不可行；屏幕 DC 直接绘制可见区本就流畅，滚动卡顿应靠"节流+少重绘"解决而非"预渲染"。

### 验证
- 性能（真实屏幕 DC + 真实滚动容器）：`UpdateAll` 全量刷新 2247ms→**23ms/次**；选中翻转 23ms；非翻转选中 4ms；真实滚动帧 22ms（45FPS，恢复至 V1.57.1 基线）。
- 正确性（像素验证）：面板间隙 = 浅灰 `Control`（"连成一片"修复）；选中 NO.1=绿✓、NO.2=空心白框、取消后消失 全部通过。
- 端到端回归（SendInput 真实按键+移动）：右拖滚动量减小、反向拖增大、长按 800ms 选中 全部通过。
- 冒烟测试进程存活。

## V1.57.2 — 拖拽滚动性能优化：画布缓存 + 滚动节流 + 画刷复用（2026-08-10）【已回退，见 V1.57.3】

> ⚠️ 本节方案（离屏画布缓存）因离屏 TextRenderer 性能灾难已由 V1.57.3 整体回退，保留记录供日后避免重蹈覆辙。滚动节流与画刷复用两项有效优化已并入 V1.57.3。

### 改动范围
- `Views/WorkstationGridView.cs`，三个优化叠加，拖动帧耗时由 **24.55ms→5.02ms（约 5 倍）**：
  1. **画布缓存（核心）**：新增 `_canvas` 离屏 Bitmap，整幅网格预先渲染进缓存，`OnPaint` 只做 `DrawImage` 可见区位图拷贝（GDI 硬件加速，实测 1000ms→1.86ms）。数据更新 `UpdateAll`/`UpdateSingle`、选中变化 `InvalidateAfterSelectionChange`/`ClearAllSelection`/`ToggleRow` 改为先把面板画进缓存再 Invalidate 对应区域。滚动时不再逐面板重绘十几处 `TextRenderer`。
  2. **滚动节流**：鼠标回报率（常见 125~1000Hz）远高于屏幕刷新率（60Hz），此前每次 MouseMove 都直接 `AutoScrollPosition` setter（含布局+滚动条更新，约 2.9ms）。改为 MouseMove 只记录 `_dragTargetScroll`，由新增 16ms 的 `_dragScrollTimer` 统一应用（60FPS 合并）；MouseUp 时立即应用最终目标，保证松手停位准确。
  3. **画刷/画笔复用**：边框 `_penBorder`、值框底 `_brushValueBox`、行选按钮 `_brushRowSelect`、设置按钮 `_brushSetButton`、选中框绿/白底 `_brushSelectChecked`/`_brushSelectUnchecked` 缓存为字段，替换原每面板 `new SolidBrush/Pen` 的数百次/帧分配，减轻 GC 压力。

### 为什么这么改
- 卡顿根因经测量确认：滚动容器在 `AutoScrollPosition` 变化时会让子控件重绘**整个可见区**（harness 实测 clip=100% 可见区，而非"新暴露条带"），每帧重画 12 个可见面板的十几处文本 → 单帧 24.55ms，远超 60FPS 的 16.7ms 预算。三层优化分别解决"画得太贵""画得太频繁""分配太多"。

### 验证
- 性能 harness（离屏渲染 + 真实滚动 100 帧）：滚动帧 24.55ms→5.02ms；OnPaint 1000ms→1.86ms；setter 2.88ms→0.62ms；完整拖拽帧 37.9ms→5.4ms。
- 正确性 harness：无选中/选中1台/行全选/取消全部 四种状态在画布缓存下渲染像素全部正确（绿✓、空心白框、消失均验证）。
- 端到端回归（SendInput 真实按键+移动）：右拖滚动量减小、反向拖增大、长按 800ms 选中，全部通过。
- 冒烟测试进程存活。

## V1.57 — 主界面工位列表支持鼠标拖拽滚动（2026-08-10）

### 改动范围
- `Views/MainForm.Designer.cs`：`groupBoxLog` 标题由 "LOG" 改为 "日志"
- `Views/WorkstationGridView.cs`：新增**按住鼠标左键拖动滚动**功能——
  - 按下左键时记录拖拽起点与外层滚动容器（`Panel.AutoScroll`）的当前滚动位置，并捕获鼠标（`Capture=true`），保证指针移出网格后拖动仍不中断；
  - 移动超过 `DragScrollThreshold`(10px) 判定为拖拽，进入拖拽后按位移量持续更新外层容器的 `AutoScrollPosition`，实现列表左右/上下跟随鼠标滑动；
  - 拖拽期间停止长按计时并隐藏悬停提示，抬起时若为拖拽结束则不计为点击（不影响原有面板点击/长按选中交互）；
  - 未超过阈值（单击/双击）行为与之前完全一致。
  - **长按选中兼容（V1.57 强化）**：拖拽启动阈值设为 10px，特意**大于**长按取消阈值(8px)——长按时手指轻微抖动（≤8px）不会进入拖拽，800ms 计时照常触发选中；只有明显拖动（&gt;10px）才进入滚动。鼠标捕获（Capture）只保证拖动不中断，不影响长按计时。实测长按选中仍正常。

### 为什么这么改
- 工位网格自绘画布可达 3060×3038 物理像素（V1.55 高 DPI 缩放后），超出主界面可视区域，滚轮滚动在大量工位下操作较繁琐；用户在操作工位列表时有"按住拖动整体滑动"的直觉需求。
- 拖拽方向换算的关键是 `AutoScrollPosition` 的 WinForms 语义：**getter 返回负值**（内容偏移取反）、**setter 接收正值**（滚动量）。因此内容跟随鼠标移动时，新滚动量 = 起点滚动量 − 鼠标位移，即 `AutoScrollPosition = (−起点X − dx, −起点Y − dy)`。
- 阈值设计上让"长按优先、拖动滞后"：长按取消是 8px，拖拽启动是 10px，两个判定互不抢占，是两者兼容的核心。

### 验证
- harness（`DragReal2`，SendInput 真实按键 + 真实鼠标移动，走完整 Windows 消息链）：初始滚到 (800,600) → 右/下拖 200,150 → `AutoScrollPosition` 由 `{-800,-600}` 变为 `{-600,-450}`（滚动量随鼠标位移减小，内容跟随，方向正确）；反向拖动 getter 变小（滚动量增大）；单击位置不变；**长按 800ms 仍触发选中**。四项全过。
- 冒烟测试进程存活；单击/长按选中等原有交互不受影响。

### V1.57.1 — 修复：选中指示框渲染残留（右上角框不消失 / 其他工位未选中框不显示）
- **Bug 现象**：① 长按选中一台工位后，只有它显示绿✓，其他工位的"空心未选中框"不显示；② 多选后再取消全部选中，右上角框停在"空心未选中"状态不消失（期望：一个都没选时框完全消失）。全选按钮无此问题。
- **根因**：`SetSelected`/`ToggleSelect` 只做**局部重绘**（`Invalidate(GetPanelBounds)`），但选中框画不画取决于**全局** `IsAnySelected`——任一选中时所有面板都要画框、一个没选时全部不画。局部重绘导致：第一次选中时其他面板没重绘（残留"无框"旧画面）；取消到最后一个时其他面板没重绘（残留"空心框"旧画面）。全选按钮走 `ToggleRow` 全量 `Invalidate()`，所以一直正常。
- **修复**：新增 `InvalidateAfterSelectionChange(deviceId, anyBefore)`：修改选中后比较全局状态，**有/无选中翻转时全量 `Invalidate()`**（所有面板框一起显示/隐藏）；仍处于有选中状态（只在已选集合内增删）时只需局部重绘当前面板。
- **验证**：离屏渲染整幅画布逐像素扫描选中框区域——无选中时框区=面板底色(255,255,255) ✓；选中 1 台时该面板=绿底+黑✓、其他面板=白底+黑框空心 ✓；取消全部后全部恢复底色、框消失 ✓。冒烟测试进程存活。

## V1.56 — 高 DPI 适配推广到全部页面：SunnyUI 全局 DPIScale 开关（2026-08-10）

### 改动范围
- `Program.cs`：程序启动时（`Application.Run` 之前）设置 `Sunny.UI.UIStyles.DPIScale = true`，等价于 SunnyUI 官方"主窗体放 UIStyleManager 控件并勾选 DPIScale"的推荐做法

### 为什么这么改
- V1.55 已适配主界面（自绘网格手动 DPI 缩放 + 标准控件走 WinForms AutoScaleMode）。排查其余页面后发现还有 3 个**继承 SunnyUI.UIForm 的窗体**（FanTestForm 送风机测试、CommunicationTestForm 通讯测试、其内部 RemapNoticeForm）在 150% 缩放下"文字溢出/偏大"：
  - 它们 Designer 里是 `AutoScaleMode.None`（SunnyUI 设计如此，`UIBaseForm.OnShown` 还会强制把 Font 模式改回 None，所以 V1.55 曾尝试改 `AutoScaleMode.Font` 完全无效，实测 `CurrentAutoScale={0,0}`）
  - SunnyUI 官方的 DPI 方案是：`AutoScaleMode.None + app.manifest dpiAware + UIStyles.DPIScale=true` 三件套。DPIScale 开启后，`UIForm.OnShown` 会遍历窗体所有 SunnyUI 控件调用 `SetDPIScale()`，把字体大小除以缩放系数（DPI/96=1.5），使控件字体在高分屏下保持设计时的物理大小
- 不用逐个改窗体：`UIStyles.DPIScale` 是全局静态开关，只在 **UIForm 子类**的 `OnShown` 路径生效；普通 Form（MainForm/SettingsForm/LoginForm/RecipeManagerForm 等）不走该路径，仍然只靠 WinForms 的 `AutoScaleMode.Font` 缩放，二者互不干扰（实测 SettingsForm 开/关 DPIScale 字体与尺寸完全一致）

### 优化点
- 一行代码覆盖全部 UIForm，零侵入各窗体
- 验证（144 DPI harness 实测）：FanTestForm 控件字体 10.5pt→7.0pt、CommunicationTestForm 同步缩小，物理渲染尺寸恢复设计值；SettingsForm/LoginForm/RecipeManagerForm 的 `ClientSize` 缩放与字体不受影响
- UIForm 窗体尺寸本身不放大（AutoScaleMode.None 行为），CommunicationTestForm 高度 1100 不会超出 1600 物理屏高，无超屏风险

## V1.55 — 高 DPI（150% 缩放）屏幕主界面适配：自绘工位网格按 DPI 放大（2026-08-10）

### 改动范围
- `Views/WorkstationGridView.cs`：**给自绘工位网格增加 DPI 缩放支持**，适配高分辨率屏幕（如 2560×1600 @150% 缩放，实际 DPI 144）：
  - 新增字段 `_dpiScale`（缩放因子 = 实际 DPI / 96，150% 缩放下 = 1.5）
  - 新增 `OnHandleCreated` + `UpdateDpiScale`：句柄创建后计算缩放因子，并按缩放后的尺寸重新设置画布 `Size`
  - 新增 `Scaled(int/Point/Rectangle)` 辅助方法：所有绘制/命中坐标统一从"96DPI 逻辑像素"换算成"物理像素"
  - `OnPaint`、`DrawPanel`、`DrawValueBox`（含值框文字左内边距）、命中检测（`TryHitPanel`/`TryHitRowButton`）、悬停提示（`GetTooltipText`）、局部重绘（`GetPanelBounds`）、行全选按钮列全部改为经 `Scaled()` 放大
- `App.config`：`<runtime>` 增加 `AppContextSwitchOverrides value="Switch.System.Windows.Forms.DpiAwareness=PerMonitorV2"`（与已有 `app.manifest` 的 PerMonitorV2 声明配套，让 WinForms 在 DPI 变化时自动缩放标准控件布局）

### 为什么这么改
- 用户新电脑分辨率高（2560×1600 @150%），主界面"很多 UI 显示不正常"。根因：自绘网格是 `AutoScaleMode.None`，画布尺寸与坐标是 96DPI 逻辑像素**不随 DPI 放大**，而 pt 字体会自动放大 1.5 倍 → 文字溢出格子/重叠，且与周围被 AutoScaleMode.Font 放大的标准控件比例失调
- 不能用 `Graphics.ScaleTransform`（TextRenderer 走 GDI 不认坐标系变换，V1.51 已踩坑），只能手动把每个坐标乘缩放因子；字体保持 pt 单位自动放大，两者同步放大后比例与 96DPI 完全一致
- 踩坑：`Control.DeviceDpi` 在 PerMonitorV2 下返回 96（句柄刚创建时 DPI 上下文未生效），实测 `CreateGraphics().DpiX` 才是真实值 144，故以 CreateGraphics 为准

### 优化点
- 96DPI（100% 缩放）老电脑上 `_dpiScale = 1.0`，坐标与历史完全一致，零回归
- 像素级验证：画布 2040×2025 → 3060×3038（×1.5）；离屏渲染后扫描 7 个关键点（标题文字/状态块浅灰/边框黑线/行全选列位置颜色）全部命中
- 冒烟测试进程存活；本版本先只适配主界面，其余页面待用户确认效果后按同思路推广

## V1.54j — 设置窗口数据行最右边缘恢复竖线（分组标题行仍无）（2026-08-09）

### 改动范围
- `Dialogs/SettingsForm.cs`：
  - 新增私有字段 `_gridScrollBar`（保存表格自带垂直滚动条 UIScrollBar 引用）
  - `CreateGrid`：关掉 `ShowLeftLine` 后把滚动条引用存进 `_gridScrollBar`（为 RowPostPaint 定位竖线 X 坐标用）
  - `Grid_RowPostPaint`：非分组行（普通配置行）改为调用新增的 `DrawDataRowRightBorder`，在滚动条左边缘左侧 1px（逻辑 X=1376）补画一条 `_grid.GridColor`（104,173,255）竖线；分组标题行仍走原"色带填充 + 蓝色下边线"逻辑，不画竖线

### 为什么这么改
- V1.54h 关闭滚动条 `ShowLeftLine` 后，数据行最右侧也失去了表格右边界竖线，观感上"表格右边没有封口"
- 用户要求：**数据行最右边缘要有竖线（表格右边界），但分组标题行（区域标题行）不要**——不能直接恢复 ShowLeftLine（它会把分组标题行也画上线，回到 V1.54g 老 bug），所以改为在 RowPostPaint 里只对非分组行补画

### 优化点
- 竖线颜色与数据行 cell border（GridColor=104,173,255）完全一致，视觉统一
- X 坐标动态取 `_gridScrollBar.Bounds.Left - 1`，不写死像素，表格尺寸变化也能自动跟随
- 像素级验证：所有数据行 X=1376 为 `104,173,255`，所有分组标题行仍为标题浅蓝 `237,243,253`（无竖线）

## V1.54i — 设置窗口去掉"搜索配置项："标题，搜索框与分组标题文字严格左对齐（2026-08-09）

### 改动范围
- `Dialogs/SettingsForm.cs` — **删除搜索框左侧的"搜索配置项："文字标题**（多余，输入框自带占位符"输入关键字过滤配置项"已足够示意），并让搜索框左边缘与下面"基础配置"等分组标题文字左边缘**逐像素对齐**：
  - 删除 `SetupSearchBox` 里的 `lblSearch` 控件；`_txtSearch` 左移并保持右边缘 450 不变（X=8, 宽 442），清除按钮 X=454 不动、与文本框间距 4px 不变
  - **对齐推导**（实测 pnlScroll.Padding.Left=18，注释里旧的 12 是错的）：分组标题文字左边缘在 pnlScroll 客户区 = Padding.Left + 8（Grid_CellPainting 给 colKey 文字留 8px 左内边距）；pnlSearch 为 Dock=Top，其左边缘 = Padding.Left；故 `_txtSearch.X = Padding.Left + 8 - Padding.Left = 8`（恒等，与 Padding 无关）
  - 若沿用旧 X=20 会右偏 12px（输入框左边缘在客户区 38 vs 文字 26）

### 为什么这么改
- 用户反馈"搜索配置项"标题多余，直接留输入框即可；且要求输入框左边缘与"基础配置"文本左边缘对齐

### 优化点
- 界面更简洁，少一个标签；搜索框与下方分类标题形成统一左边界线（像素级对齐已验证 diff=0）

## V1.54h — 设置窗口"分组标题行最右侧竖线"根治：关闭滚动条左侧线（2026-08-09）

### 改动范围
- `Dialogs/SettingsForm.cs` — **找到 V1.54f/g 一直没生效的根因并真正修掉"标题行最右侧竖线"**：
  - 之前的认知是"cell border 画在 `_grid.Right` 那一像素，RowPostPaint 覆盖宽度差 1px"，于是反复调覆盖右边界（V1.54f 用 `pnlScroll.ClientSize.Width`、V1.54g 用 `_grid.Width + 1`），但用户仍能看到竖线
  - **真凶**：SunnyUI `UIDataGridView` 内置一个 `UIScrollBar` **子控件**，覆盖在表格右边缘（最后一列右边界 ~ 表格右边界，约 X=918~936 逻辑像素）。它默认 `ShowLeftLine = True`，在 X=918 画一条 `80,160,255` 的蓝色竖线，横跨整张表格**每一行**——分组标题行的浅蓝色带最右侧也被切出一条竖线
  - 为什么之前盖不住：`RowPostPaint` 的 Graphics 被裁剪在行显示矩形内（X < 919），`_grid.Width+1` 的填充根本画不到那一像素；且 `UIScrollBar` 是子控件，永远绘制在表格内容**之上**，RowPostPaint/CellPainting 都压不过它
  - **修复**：`CreateGrid` 里找到该 `UIScrollBar` 子控件并 `ShowLeftLine = false`（该子控件随表格创建即存在，无需等 HandleCreated）。竖线彻底消失，标题色带一路延伸到滚动条处
  - 顺手把 `Grid_RowPostPaint` 里 V1.54g 那段"扩 1 像素"的误导性注释改写为真实机制说明

### 为什么这么改
- 用户反馈（V1.54f 时代起一直存在）："基础配置 这行最右侧还有表格的竖线，下面的很多区域标题的右侧都有这个问题"。之前几版都以为是 cell border 覆盖不彻底，实际是滚动条子控件的左侧线，方向就找错了
- 关闭 `ShowLeftLine` 是最小改动：不动列宽/覆盖逻辑，只去掉那条多余的装饰竖线

### 优化点
- 分组标题行最右侧不再有竖线，色带视觉上横跨整表、真正"不在表格内"
- 数据行右侧也更干净（原滚动条左缘的蓝色竖线一并消失，只剩浅蓝轨道），观感统一
- 通过像素级验证：修复前 X=1377（物理像素）为 `80,160,255` 竖线，修复后变为滚动条轨道 `243,249,255`

## V1.54c — 设置窗口分组标题行去掉表格分割线（2026-08-09）

### 改动范围
- `Dialogs/SettingsForm.cs` — **分组标题行去掉列间垂直线、保留上下水平线**：
  - **首发版（未生效）**：仅在 `CellPainting` 自绘分组标题行 + `e.Handled = true`，但 SunnyUI `UIDataGridView` 内部在 `CellPainting` 返回后还会自己补画 cell border / gridline，导致列间垂直线仍可见
  - **二次修复（去掉全部线）**：再补 `RowPostPaint` 用浅蓝覆盖整行矩形 + 上下 1px，但用户反馈"上下那条横线还是要的，不然整个表格看起来好丑"
  - **本版（最终）**：在 `RowPostPaint` 阶段用浅蓝覆盖**整行矩形但上下边各内缩 1px**——只抹掉列间垂直线、保留 DataGridView 默认的上下水平线
  - **最终调整（横线改蓝色）**：用户反馈默认的黑色/灰色水平线"很丑"，改为在 `RowPostPaint` 里**自己用蓝色 (`214,229,255`，与表头边框同系) 重画**上下两条水平线，不再依赖 DataGridView 默认 cell border

## V1.54g — 标题行右侧竖线 / 上边叠色深度修复（2026-08-09）

### 改动范围
- `Dialogs/SettingsForm.cs` — **V1.54f 修复未生效 + 上边横线叠色修复**：
  - **V1.54f 没生效的根因**：之前用 `pnlScroll.ClientSize.Width` 作覆盖右边界，但 `RowPostPaint` 的 Graphics 坐标系实际是 `_grid` 控件的，`pnlScroll.ClientSize.Width` 值超出 `_grid` 实际可绘范围；且 cell border 画在 X=`_grid.Width` 那一像素（半开区间），之前覆盖矩形 Width=`_grid.Width` 漏掉那一像素
  - **修复**：覆盖右边界改为 `_grid.Width + 1` 像素（Graphics 坐标系下），正好覆盖到 `_grid.Right` 位置那条 cell border
  - **上边叠色修复**：之前同时画上下两条蓝色线，**上边线**与 DataGridView 在标题行顶画的 cell border 叠在同一像素 → 颜色叠加"深很多"。改为**只画下边线**（用户明确要求"标题下面的横线要蓝色"），上边线直接用 DataGridView 默认 cell border（与数据行颜色一致）

### 为什么这么改
- 用户反馈 V1.54f "没生效" + "基础配置这些区域标题上面的表格横线好像多画了一次是不是？颜色明显深很多"

### 优化点
- 标题行最右侧的竖线真正消失（覆盖到 _grid.Right 那一像素）
- 上边横线与数据行 cell border 同色（无叠色），下边横线保持深蓝（色带延伸）
- Graphics 坐标系明确为 _grid 控件，不再误用 pnlScroll 坐标系

## V1.54f — 标题行最右侧残留垂直线修复（2026-08-09）

### 改动范围
- `Dialogs/SettingsForm.cs` — **覆盖范围扩展到 pnlScroll.ClientSize.Width**：
  - 之前 `Grid_RowPostPaint` 里 `rowRect.Width = _grid.Width`，只覆盖到 `_grid.Right`
  - DataGridView 在 `_grid` 右边缘外侧还会画一条 cell border（垂直线），导致"基础配置"等每个标题行最右侧仍有一条竖线
  - 改为 `coverRight = pnlScroll.ClientSize.Width`，覆盖矩形和上下水平线都延伸到 pnlScroll 右边缘

### 为什么这么改
- 用户反馈"基础配置 这行最右侧还有表格的竖线，去掉，下面的很多区域标题的右侧都有这个问题"
- RowPostPaint 的 Graphics 是 pnlScroll 级别的（不是 _grid 级别），所以可以画到 pnlScroll 整个宽度

### 优化点
- 标题色带现在横跨整个 pnlScroll 宽度（从 X=0 到 pnlScroll.Right），右侧不再有残留竖线
- 上下蓝色水平线也跟着延长

## V1.54e — 搜索框与分组标题对齐 + 标题横线改为深蓝（2026-08-09）

### 改动范围
- `Dialogs/SettingsForm.cs` — **搜索框与表格分组标题对齐 + 字号一致**：
  - `lblSearch` X 14 → 20（pnlScroll.Padding.Left=12 + colKey 文字左内边距 8 = 20），与下面"基础配置"等分组标题完美左对齐
  - `lblSearch` 字体 11F Bold → **10F Bold**（与分组标题行同款），视觉上"搜索配置项："和"基础配置"是同一族标题
  - `_txtSearch` 文本框 X 124 → 130（左移 6px 保持与 label 起点对齐），字体 11F → 10F
  - `_btnClearSearch` 清除按钮 X 448 → 454（左移 6px 保持与文本框的相对间距）
- `Dialogs/SettingsForm.cs` — **标题行上下水平线颜色修正**：
  - 之前用 `214,229,255`（极浅蓝，视觉接近白），用户反馈"颜色不对"
  - 改为 `48,119,238`（与标题文字同色，深蓝），视觉上与"基础配置"等标题字同色，是"标题带"的延伸

### 为什么这么改
- 用户反馈"搜索配置项 和 下面的 基础配置 左侧对齐，字体大小一样" + "标题下面的横线要蓝色"
- 之前搜索 label 在 X=14（pnlScroll 容器内）、表格第一列 colKey 在 pnlScroll.Padding.Left+8=20（容器内 X 坐标），错位 6px；且字号 11F vs 10F 不一致
- 横线 `214,229,255` 太浅，视觉上几乎看不到，不算"蓝色"

### 优化点
- 搜索框与表格标题完美对齐（X 20 + 字号 10F Bold），整张设置窗口视觉一致
- 标题横线改为与标题文字同色（深蓝 `48,119,238`），与标题色带融为一体

## V1.54d — 设置窗口表格与窗口边缘留出左右空隙（2026-08-09）

### 改动范围
- `Dialogs/SettingsForm.Designer.cs` — **`pnlScroll.Padding = (12,0,12,0)`**：让内部 `_grid`（仍 `Dock=Fill`）相对 pnlScroll 边缘左右各缩 12px，从而表格与窗口左右各留约 12px 空白带
- 之前 `pnlScroll` 无 padding、`_grid` Dock=Fill 撑满，表格左右边缘几乎贴窗口边缘，缺空隙；现在右侧已有 pnlScroll RectColor 边框做分隔，左侧也补上对称空隙

### 为什么这么改
- 用户反馈"表格的左边缘离整个窗口的左边缘太近了，稍微有点空隙吧，就像右边那样"
- 用 Padding 是最小侵入：不动表格列宽、不动控件结构，仅容器留白

### 优化点
- 表格左右对称，视觉上"居中悬浮"在中部

### 为什么这么改
- 用户反馈 V1.54c 首发版"目前还没有搞好"
- SunnyUI UIDataGridView 走自己的 OnPaint，单纯靠 e.Handled=true 拦不住；用 RowPostPaint 主动覆盖是 SunnyUI 自定义控件的标准做法

### 优化点
- 标题行从视觉上完全脱离表格样式（无列间垂直线、无上下水平线）
- 不改变数据行任何样式（RowPostPaint 跳过非分组行）
- 文字重画在覆盖层之后，保证标题文字仍清晰可见

## V1.54b — IO 备用通道映射弹窗箭头加粗 + 弹窗适度加宽（2026-08-09）

### 改动范围
- `Controls/IoMappingEditorPopup.cs` — **箭头列字符加粗 + 弹窗适度加宽**：
  - **加粗（V1.54 续）**：V1.54 首发版表头与数据行箭头都用"微软雅黑 12F, Regular"，但用户反馈"和下面还是有点差别"。根因是"→"是细线条字符、微软雅黑 Regular 视觉偏纤细，**数据行与表头都改 12F, Bold**，像素级一致；自绘路径（`Dgv_CellPainting`）与数据列 `DefaultCellStyle.Font` 同步更新
  - **加宽**：弹窗 `ClientSize` 560×268 → **640×268**；表格宽 536 → **616**；列宽 148+92+56+148+92=536 → 172+104+60+172+104=612。**根因**：原 5 列总宽 536 等于 `dgv.Width`，最右侧"新通道"列十六进制微调框按钮被裁切；现表格宽 616（多 4px 余量），刚好不裁切
  - 提示 `lblHint` 宽度 536 → 616 同步；取消/确定按钮重新靠右（398/466 → 476/546）

### 为什么这么改
- 用户反馈之前加太多（720 太宽），只需要刚好看到右侧边缘即可

### 优化点
- 弹窗从 720 缩回 640，刚好够用不臃肿
- 箭头表头与数据行像素级一致（加粗、12F、48,48,48、居中）

## V1.54 — IO 备用通道映射弹窗表头箭头样式自绘修复（2026-08-09）

### 改动范围
- `Controls/IoMappingEditorPopup.cs` — **表头箭头与数据行箭头样式彻底一致（自绘）**：
  - V1.53 通过 `_dgv.Columns["colArrow"].HeaderCell.Style.Font/ForeColor/Alignment` 子属性赋值想让表头箭头"→"与数据行一致（微软雅黑 12F、未加粗、黑色 48,48,48、居中），但 SunnyUI `UIDataGridView` 下 `HeaderCell.Style` 仍会被 `ColumnHeadersDefaultCellStyle`（9 号**加粗**、左对齐）覆盖——子属性赋值不能整段替换父样式，且加粗属性必须显式 `FontStyle.Regular` 才会去除。效果上表头"→"还是 9 号加粗，与下方数据行的 12 号未加粗箭头观感割裂
  - 改为 **`CellPainting` 自绘该列表头**：用 `e.Paint(CellBounds, Background|Border|SelectionBackground)` 走默认表头背景/边框，再用 `TextRenderer.DrawText` 以"微软雅黑 12F、`FontStyle.Regular`、黑色 48,48,48、居中"画"→"；表头与数据行像素级一致

### 为什么这么改
- 试过把 `ColumnHeadersDefaultCellStyle.Font` 直接改成 12F 也会同时影响"原寄存器/原通道/新寄存器/新通道"四列表头（这些列要求 9 号加粗，标题文字与数据列对齐明确），不可行
- 试过 `colArrow.HeaderCell.Style.Font = new Font(..., 12F, FontStyle.Regular)` 仍被 `ColumnHeadersDefaultCellStyle`（9F,Bold）继承下来（实测表头依然加粗、9 号）
- 自绘只针对箭头这一列的表头单元格，最小侵入、不污染其他列表头

### 优化点
- 表头箭头与数据行箭头视觉完全一致（字号/字色/居中/未加粗）
- 仍然走 SunnyUI 默认表头背景色（浅蓝 237,243,253）与边框，与其他表头保持风格统一
- 注释里把"V1.53 为什么没生效"也写清楚，后续如果要把同样套路迁到其他列可直接参考

## V1.53 — 系统设置窗口滚动卡顿根治 + 复制气泡 ToolTip 修复（2026-08-09）

### 改动范围
- `Dialogs/SettingsForm.cs` + `SettingsForm.Designer.cs` — **设置窗口滚动卡顿根治**：
  - 原实现把 8 个业务分类做成 **8 个独立 UIDataGridView**，放进 `pnlScroll`（SunnyUI UIPanel + AutoScroll）整页滚动。内容总高数千像素，滚动时 WinForms 要逐帧**物理移动 8 个重量级表格窗口并整块重绘**，必然卡顿（与主视图 V1.50 把 72 面板合并为单画布同理）
  - 改为**合并为 1 个 UIDataGridView**：分类标题不再用 UILine 分隔条，改用表格内的"分组标题行"（浅蓝底深蓝粗体，新增 `AddGroupRowStyle`）；表格 `Dock=Fill` 撑满 pnlScroll，滚动由 DataGridView 自身处理（**虚拟化，只重绘可见行**），pnlScroll 关闭 AutoScroll。搜索过滤（整组隐藏）/保存遍历（分组行跳过）/长按复制/IP·IO 映射弹窗全部适配单表格
- `Dialogs/SettingsForm.cs` — **长按复制气泡 ToolTip 不显示修复**：
  - 原用无坐标重载 `Show(text, window, duration)`，会把气泡定位到窗口默认位置（首次可能落到屏幕角落）；且 `CellMouseUp` 里同步 Show 会被 DataGridView 刚释放的鼠标捕获干扰，导致"已复制"气泡看不到
  - 改为**带坐标重载** `Show(text, Point, 1500)`（与主视图悬停提示同一可靠路径，锚点 = 光标右下 12px），`MouseUp` 里用 `BeginInvoke` 延迟到消息链处理完再弹，`OnShown` 预激活也改带坐标版本，并设 `ShowAlways=true`（模态对话框内也可靠显示）
  - **再次修复（宿主窗口改为窗体自身）**：首版气泡挂在 DataGridView 上，其内部窗口结构复杂，Show 的弹窗消息可能被表格窗口干扰导致仍不渲染；改为宿主窗口用 `SettingsForm` 本身（this，与主视图悬停提示一致），锚点取 `this.PointToClient(Cursor.Position)+12px`，Show 前先 `Hide` 清掉 ToolTip 残留显示状态
  - **提示时机（长按即弹，无需等松开）**：气泡由"松开鼠标后弹出"改为"长按到点（700ms，行业常规 500~800ms）复制成功即弹"；松开鼠标时再做一次兜底调用（`_pendingCopyTip` 已消费则空转，不会重复弹），气泡停留时长 1500ms→2500ms，保证按住时也能看清
- `Controls/IoMappingEditorPopup.cs` — **IO 备用通道映射弹窗箭头列样式一致**：表格中间箭头列（→）表头原本走全局表头样式（9 号加粗、左对齐），与数据行箭头（12 号黑色居中）字号/加粗/位置都不一致；单独覆盖该列 `HeaderCell.Style` 为与数据箭头完全一致（微软雅黑 12F、黑 48,48,48、居中），数据行箭头颜色同步统一为黑色（与正文同色）

### 为什么这么改
- 用户反馈设置窗口上下滑动很卡顿、ToolTip 不显示；两者都源于"多重量级控件 + 不可靠的 ToolTip 重载"两个架构层问题
- 单表格虚拟化滚动是 WinForms 大数据量滚动的标准做法，与项目 V1.50 主界面"单画布"优化思路一致

### 优化点
- 滚动只重绘可见行，彻底消除 8 表格整块重绘卡顿；分组标题行固定 30px、数据行按内容换行自动定高，长说明文本仍完整显示
- 搜索过滤、保存校验、IP/IO 映射弹窗、长按复制等行为与 V1.52 完全一致，仅实现层面迁移到单表格
- 复制气泡改为与主视图相同的可靠 ToolTip 路径，模态窗口内也能正常弹出

## V1.52 — 工位网格值框文字内边距 + View 界面注释补齐（2026-08-09）

### 改动范围
- `Models/PanelLayoutConfig.cs` + `bin\Debug\PanelLayout.json` — **值框文字左内边距**：新增 `ValueTextLeftPadding`（默认 6px），值框坐标不变，仅让框内文字与左边框留出间隔（解决"文字贴边/紧贴"观感）；该值可在 PanelLayout.json 中自行微调
- `Views/WorkstationGridView.cs` — `DrawValueBox` 绘制文字时矩形左移内边距（宽度同步缩短，文字不溢出右边框）；类头部注释新增**界面 ASCII 图**（整体网格 + 单面板内容布局 + 坐标标注 + 状态块配色说明），后续改界面可直接把该注释贴给 AI
- `Dialogs/CommunicationTestForm.cs` / `FanTestForm.cs` / `LoginForm.cs` / `ChangePasswordForm.cs` / `UserManagementForm.cs` / `HistoryRecordForm.cs` / `SettingsForm.cs` — 头部注释**补齐界面 ASCII 图**（原来只有文字描述或无布局说明），与 RecipeManagerForm 风格统一

### 为什么这么改
- 用户反馈值框内文字"紧贴左边界"；**首版方案（右移值框 X 57→70）会连带整个框移动，观感是"文本框左边界跟着文本移动"，被用户否决**，改为只加文字内边距、值框本身不动
- 界面图注释让 AI 助手（无法看图）能凭文本理解各窗体布局，便于后续直接按注释改界面

### 优化点
- 文字内边距在配置文件中可调，现场无需重新编译
- 值框坐标与 V1.51 完全一致，无布局错位风险

## V1.51 — 工位网格文字"糊成一坨"修复 + 布局外部化（2026-08-09）

### 改动范围
- `Views/WorkstationGridView.cs` — **重写绘制逻辑**：
  - **修复文字模糊/叠糊**：原实现 `OnPaint` 用 `g.TranslateTransform` 平移坐标系后再调 `TextRenderer.DrawText` 绘制文字，TextRenderer 走 GDI 绘制路径，与 Graphics 坐标变换叠加时位置/尺寸错乱，文字溢出到相邻元素上互相叠加（"糊成一坨"）。现在全部元素（背景/状态块/值框/标签/按钮/文字）一律改为**绝对坐标**绘制（元素坐标 = 面板左上角 + 设计坐标），彻底去掉 Transform 与 GDI 文字混用
  - **字体显式创建**：正文改用独立的 `_panelFont`（取自布局配置，默认微软雅黑 9pt），不再用 `this.Font`（后者继承主窗体 AutoScale 缩放字体，与固定像素矩形不匹配导致文字溢出）
  - **布局外部化**：面板坐标/颜色/字号/按钮与提示文字全部改读 `PanelLayoutConfig`，不再写死常量；`Configure` 首次运行时自动向程序目录导出默认 `PanelLayout.json`，现场改配置文件即可微调界面，无需重新编译
- `Models/PanelLayoutConfig.cs` — **新增布局配置模型**：面板网格尺寸、面板内各元素坐标（RcPower/RcWorkState/RcVacuumOpen/RcPressureValue/RcSNValue/RcRecipeValue/RcDelayStart*/RcSetButton/RcSelectBox/标签位置）、字体（字体名/正文字号/标题字号/标题加粗）、颜色（"R,G,B"字符串，覆盖背景/状态块/按钮/边框等全部配色）、提示文字；提供 `LoadOrDefault`（文件缺失/损坏回退默认值）与 `SaveDefault`（导出配置）；配套 `ElementRect`/`ElementPoint` 可序列化坐标类
- `Views/WorkstationGridView.Designer.cs` — Dispose 补充释放 `_panelFont`
- `AgingTestSystem.csproj` — 注册 `Models/PanelLayoutConfig.cs`

### 为什么这么改
- 用户反馈自绘画布上文字"看不清、糊成一坨"：根因是 TextRenderer（GDI）与 `g.TranslateTransform`（GDI+ 变换）混用导致文字绘制错乱，叠加到相邻元素上
- 同时把用户多次要求的"改坐标/改色/改字号"诉求从改代码升级为改配置文件，降低现场维护门槛

### 优化点
- 默认配置值与历史版本布局完全一致，升级后界面无感变化
- 布局配置只解析一次（构造时），绘制热路径零字符串解析开销

## V1.50 — 主界面工位网格滚动撕裂彻底解决：单窗口自绘大画布（2026-08-09）

### 改动范围
- `Views/WorkstationGridView.cs` + `WorkstationGridView.Designer.cs` — **新增自绘大画布控件**：整个网格（8列×9行面板 + 行全选按钮列）合并为 **1 个 UserControl**，尺寸 = 内容总尺寸（8×245+80 宽、9×225 高），由 `OnPaint` 按坐标绘制全部面板内容与行全选按钮；`UpdateAll`/`UpdateSingle` 只改内存字段 + `Invalidate`，并支持可见区域局部重绘（按 ClipRectangle 计算行列范围）；交互（长按选中/设置按钮/选中框/行全选/悬停提示）全部用坐标命中实现
- `Views/MainForm.cs` — `CreateWorkstationPanels` 重写：删除 TableLayoutPanel + 72 个面板 + 行全选按钮的创建逻辑，改为创建 1 个 `WorkstationGridView` 放入外层 `Panel.AutoScroll` 滚动容器（容器反射开启双缓冲）；`_panelViews`/`_rowSelectButtons` 两个字典字段及 `BtnSelectRow_Click`/`UpdateRowSelectButton`/`UpdateSelectionBoxVisibility`/`Panel_ClearAllSelectionRequested` 等方法整体删除（逻辑已内聚到 GridView 内部）；数据更新改走 `_gridView.UpdateAll`/`UpdateSingle`；`Panel_OnSetClicked` 改用 GridView 的 `SetSelected`/`GetSelectedDeviceIds`；`ShowBatchRecipeForm`/`GetSelectedDeviceIds` 改读 GridView 选中集合；GridView 行全选动作通过 `OnLog` 事件通知主窗体写日志
- `Views/WorkstationPanelView.cs` + `.Designer.cs` + `.resx` — **删除**：功能完全被 WorkstationGridView 替代，项目内已无任何实例化调用（对齐 V1.44/V1.45 移除死代码惯例）；csproj 相应移除三个文件的项目引用
- `AgingTestSystem.csproj` — 注册 `WorkstationGridView.cs` / `WorkstationGridView.Designer.cs`，移除 WorkstationPanelView 三件套
- `README.md` — 目录结构表把 WorkstationPanelView 条目替换为 WorkstationGridView

### 为什么这么改
- V1.49 把每面板内部 13+ 子控件改为自绘，滚动时仍需逐帧移动 72 个面板窗口，拖动滚动条仍有撕裂/卡顿
- V1.50 改为 RecyclerView 同源的"单窗口大画布"：滚动时系统只需移动 **1 个窗口**（内存 BitBlt 移动位图），72 个面板全部由 OnPaint 按坐标绘制且只重绘可见区域 → 无撕裂、无卡顿

### 优化点
- **完全不影响 1Hz 全量实时刷新与监控**：`UpdateAll` 只改内存字段 + Invalidate，1Hz 全量刷新开销极小
- 行全选/选中交互/悬停提示与 V1.49 行为完全一致，只是实现从"多控件事件"迁到"坐标命中"
- 后续若表数量继续增加（数百台），可按相同思路再做"按屏虚拟化"（只保留可见面板），当前 72 台规模不需要

## V1.49 — 主界面工位网格滚动撕裂优化：面板自绘重构（2026-08-09）

### 改动范围
- `Views/WorkstationPanelView.cs` + `WorkstationPanelView.Designer.cs` — **面板自绘重构**：原每面板 13+ 个子控件（Label/TextBox/Button/选中指示）全部删除，改为单个 UserControl 由 `OnPaint` 按坐标自绘全部内容（设备编号/上电灯/工作状态/真空压力/SN/配方/延时/设置按钮/选中框）；"设置按钮、选中框"等交互改用坐标命中（hit-testing），长按选中/单击切换/状态块悬停提示（ToolTip）逻辑全部保留；面板开启双缓冲
- `Views/MainForm.cs` — 主窗体与工位网格 `TableLayoutPanel` 开启双缓冲（TableLayoutPanel 的 `DoubleBuffered` 受保护，通过反射开启）

### 为什么这么改
- 原实现 72 面板 × 13+ 控件 ≈ 936 个控件窗口。WinForms 滚动时 ScrollableControl 要逐帧移动这些子控件窗口（MoveWindow），开销巨大，拖动滚动条必然撕裂/卡顿——双缓冲只能减轻"重绘闪烁"，救不了"移动窗口"本身，因此第一阶段"TextBox 换 Label + 三层双缓冲"现场实测无改善
- 自绘后 72 面板 = 72 个顶层控件，滚动时只需移动 72 个窗口，性能提升一个数量级，撕裂基本消除

### 优化点
- **完全不影响 1Hz 全量实时刷新与监控**：`UpdateData` 只改内存字段 + `Invalidate()`，72 面板全量刷新开销极小；无任何"滚动暂停刷新"逻辑（该方案会滞后数据，已弃用）
- 后续若表数量继续增加（数百台），可按相同思路再做"按屏虚拟化"（只保留可见面板 + 滚动回收复用），当前 72 台规模不需要

## V1.48 — 主界面技术员权限字体颜色加深（2026-08-09）

### 改动范围
- `Views/MainForm.cs` — `UpdatePermissionDisplay` 中技术员角色名颜色由 `Color.SkyBlue`（天蓝，偏浅）改为 `Color.RoyalBlue`（深蓝，更醒目）；相关注释同步更新
- `Views/MainForm.Designer.cs` — 两处"天蓝"注释改为"蓝"

### 为什么这么改
- 天蓝在浅色顶栏上对比度低，不够醒目；深蓝既有辨识度又与管理员红色、操作员绿色区分明显

## V1.47 — 用户管理窗体"用户名"改为可编辑下拉框，默认修改已有账号（2026-08-09）

### 改动范围
- `Dialogs/UserManagementForm.Designer.cs` — 删除独立的"账号"下拉框（cboAccount）与"新用户名"文本框（txtNewUsername），合并为可编辑下拉框 `cboUsername`，标签由"新用户名"改为"用户名"；窗体高度相应收窄，密码输入行上移
- `Dialogs/UserManagementForm.cs` — 账号列表直接填入 `cboUsername`（点击展开列出当前角色全部账号）；`GetSelectedAccount` 改为：下拉选中项优先 → 文本匹配已有账号兜底 → 回退到上次下拉选中的账号（作为改用户名目标）；新增 `_lastSelectedAccount` 记忆字段；"应用修改"默认场景是改已有账号密码，用户名框保持原样即只改密码，输入新名则改名

### 为什么这么改
- 原布局"账号下拉框 + 新用户名文本框"职责重叠且易混淆：此窗体默认用途就是修改已有账号（新增账号有独立"添加账号"按钮），"新用户名"叫法会让管理员误以为在此新增账号
- 改为可编辑下拉框后：下拉即"选要改哪个账号"，保持原样点应用=只改密码，直接输入新名=改名，一个控件承载两种操作，交互更直观

### 优化点
- 切换角色时下拉框尽量保持上次选中的用户名；应用修改成功后保持选中该账号，便于连续操作
- 删除/添加账号后列表自动刷新，新添加账号自动填入下拉框

## V1.46 — 操作员/技术员/管理员均可修改自己的密码（2026-08-09）

### 改动范围
- `Services/UserManager.cs` — 新增 `ChangeOwnPassword(oldPassword, newPassword)`：任意已登录角色修改自己密码，须验证当前密码（防止他人在无人值守时篡改）；新密码至少4字符、不得与当前密码相同；修改成功后自动清除该角色记住的登录信息（避免下次自动填充旧密码导致登录失败）。原 `UpdatePassword`（管理员改他人密码，无需验旧密码，用于忘记密码重置）保持不变
- `Dialogs/ChangePasswordForm.cs` + `ChangePasswordForm.Designer.cs` — 新增修改密码对话框：显示当前登录用户，输入当前密码/新密码/确认密码，逐项校验后调用 `ChangeOwnPassword`；Esc 取消
- `Views/MainForm.cs` — 用户权限下拉菜单在任意角色已登录时追加"修改密码"菜单项（未登录不显示），成功后写日志
- `AgingTestSystem.csproj` — 注册新增的两个窗体文件

### 为什么这么改
- 原设计仅管理员能改密码，操作员/技术员密码遗忘或需要定期更换时只能等管理员重置，流程繁琐
- 符合工业软件通行做法：所有用户可改自己密码（验旧密码）；管理员可重置其他账号密码（不验旧密码），同时管理员也能改自己的密码

### 优化点
- 改自己密码必须验证当前密码，避免机器无人值守时被他人随意改密
- 改密后自动清除记住密码，杜绝旧密码残留导致自动填充登录失败

## V1.45 — 移除未被使用的单台手动控制窗体 DeviceManualForm（2026-08-09）

### 改动范围
- 删除 `Dialogs/DeviceManualForm.cs`、`DeviceManualForm.Designer.cs` 两个文件（项目内从未实例化调用，属死代码）
- `AgingTestSystem.csproj` — 移除上述文件的项目引用
- `Services/IoMapBuilder.cs` — 注释里对 DeviceManualForm 的说明已过时，顺手去掉

## V1.44 — 移除已停用的启动 Splash 页面及其图片资源（2026-08-09）

### 改动范围
- `Program.cs` — 删除 `ShowSplashScreen()` 方法及其相关注释（含类注释里修复 L3 说明、Main 里的调用注释），并移除随之不再使用的 `using System.Threading`
- 删除 `Views/mForm_Progress.cs`、`mForm_Progress.Designer.cs`、`mForm_Progress.resx` 三个文件
- `AgingTestSystem.csproj` — 移除上述文件与 `Resources` 下两张启动图（`华际光电(1)(1).png`、`华际光电(1)(1)(1).png`）的项目引用
- `Properties/Resources.resx` / `Resources.Designer.cs` — 移除两张图片的嵌入资源与对应访问属性

### 为什么这么改
- 启动流程早已改为直接进入主界面（Splash 调用被注释），此功能不再使用，属历史遗留死代码，彻底删除避免维护困惑

## V1.43 — 公共参数窗口负压值输入框改为数值框（支持正负数）（2026-08-09）

### 改动范围
- `Dialogs/CommonParameterForm.Designer.cs` — `txtThreshold`（TextBox）改为 `nudThreshold`（NumericUpDown）：范围 -9999~9999（支持正负数）、保留 1 位小数、步进 1、默认 -95
- `Dialogs/CommonParameterForm.cs` — 保存逻辑去掉"非空/非数字"文本校验（NumericUpDown 天然保证输入为有效数字），直接读取 `Value`；居中定位代码同步改引 `nudThreshold`

### 为什么这么改
- 原 TextBox 需手输数字且无法防止非法字符；改为数值框后可点上下箭头调整、输入更直观，且天然限制范围，杜绝误输非法值

### 优化点
- 支持正负数（负压现场为负值，但需求要求正负数都允许）
- 保留 1 位小数（如 -95.5），与 `SetAllBarometerThresholds` 接收 decimal 的能力一致

## V1.42 — 登录窗体新增"记住密码"功能（2026-08-09）

### 改动范围
- `Dialogs/LoginForm.Designer.cs` — 密码框与按钮之间新增"记住密码"复选框，按钮/窗体高度相应下移
- `Dialogs/LoginForm.cs` — 窗体加载时读取该角色记住的登录信息并自动填充用户名、密码、勾选复选框；登录成功后按勾选状态保存或清除记住信息
- `Services/UserManager.cs` — 新增记住密码的读写：`GetRememberedLogin` / `SaveRememberedLogin` / `ClearRememberedLogin`，按角色分别存储，持久化到程序运行目录的 `RememberedLogin.json`；密码以 Base64 混淆存储（仅演示，非安全加密）
- `.gitignore` — 新增忽略 `RememberedLogin.json`（含密码的运行时用户数据，不入库）

### 为什么这么改
- 现场操作员/技术员反复切换权限登录，每次手动输入账号密码繁琐且易输错；记住密码后登录只需确认或输入密码即可，操作更快

### 优化点
- 记住信息按角色分别保存：切换为操作员/技术员/管理员时各自自动填充，互不干扰
- 未勾选"记住密码"登录成功时会自动清除该角色已记住的信息，避免残留
- 用户名自动匹配到下拉框已有账号则高亮选中，账号已删除时回退为手动输入值

## V1.41 — 用户账号支持多账号：登录下拉选择 + 管理界面增删改（2026-08-09）

### 改动范围
- `Services/UserManager.cs` — 数据模型由"每角色单账号"扩展为"每角色账号列表"（操作员/技术员支持多账号，管理员仍仅一个账号）；登录改为在目标角色的账号列表中匹配用户名+密码；`UpdateUsername`/`UpdatePassword` 改为针对指定账号对象修改；新增 `AddAccount`（添加账号，管理员唯一性校验）、`RemoveAccount`（删除账号，每角色至少保留一个、管理员账号禁止删除）、`GetAccounts`（获取角色下全部账号，供下拉框使用）；加载 `Users.json` 时按角色分组，管理员仅保留第一个账号（防止手改出多个）
- `Dialogs/LoginForm.cs` + `LoginForm.Designer.cs` — 用户名输入框由 TextBox 改为下拉框（ComboBox），加载时自动列出该角色已有账号并默认选中，仍可手动输入；登录校验兼容下拉选择值
- `Dialogs/UserManagementForm.cs` + `UserManagementForm.Designer.cs` — 新增"账号"下拉框列出该角色全部已有账号；新增"添加账号"/"删除账号"按钮（添加时弹出用户名/密码输入窗口）；"应用修改"改为针对当前选中账号执行；切换角色/账号时自动刷新列表并重置输入框

### 为什么这么改
1. **原实现每角色只有一个账号**：用户管理窗体只能改一个账号，多人共用同一角色时只能共用一套用户名密码，不便于区分操作记录。
2. **需求明确要求登录时"读取已有账号、下拉选择"**：把用户名输入改为下拉选择已有账号，减少手输错误；同时管理员账号有且仅有一个。

### 优化点
- 用户名在全部角色账号内保持唯一；删除账号有确认提示且每角色至少保留一个；若删除的是当前登录账号会自动登出
- 登录下拉框默认选中第一个账号（管理员登录即选中 admin），操作路径更短
- `Users.json` 格式不变（仍是 `List<UserAccount>`），旧数据无缝兼容，管理员重复账号在加载时自动去重

## V1.40 — 系统设置保存后热生效，无需重启（结构型配置除外）（2026-08-09）

### 改动范围
- `Dialogs/SettingsForm.cs` — 保存成功后把非结构型配置**就地回写内存中的 DeviceConfig 实例**（主窗体传入的同一引用），并把本次保存的配置项 key 集合暴露给主窗体（`SavedKeys`）；弹窗改为分层提示：只有结构型配置改动时才提示"需重启程序后生效"，其余提示"已即时生效"。新增结构型 / 各连接参数分类集合（`StructuralKeys` / `BarometerConnectionKeys` / `IoConnectionKeys` / `FanConnectionKeys` / `ScannerConnectionKeys`），新增 `ApplyChangesToConfig` / `ConvertConfigValue`（反射按属性类型转换，支持 0x 十六进制地址、IO 映射表、候选 IP 列表）
- `Views/MainForm.cs` — 设置窗口关闭后调用 `ApplySettingsHotReload`：采集间隔更新主定时器；气压表串口 / 耦合器 / 送风机 / 扫码枪连接参数改动时在后台线程触发重连
- `Services/DeviceManager.cs` — 新增 `ReconnectBarometerReader`（Connect 内部先断开旧串口再按新参数连接）、`ReconnectIo` / `ForceReconnectFan`（即使已连接也先断开，用新 IP/端口/候选列表重连）、`UpdateCollectInterval`
- `Dialogs/SettingsForm.Designer.cs` — 顶部提示文案更新为"保存后立即生效（连接参数自动重连），仅结构型配置需重启"

### 为什么这么改
1. **原实现所有配置改动都要重启才生效**：服务在启动时一次性把 AppSettings 读进 DeviceConfig 后就不再更新，设置窗口只写 exe.config 不回写内存，导致现场改个寄存器地址 / IO 映射都要重启软件，体验差。
2. **各服务每次读写都实时访问 `_config.xxx`**（同一 DeviceConfig 实例）：只要保存后就地回写该实例，业务逻辑类配置（寄存器地址 / IO 映射 / 取反 / 小数位 / 缩放 / 报警阈值 / 老化参数等）立即生效；连接参数（串口 / IP / 端口 / 超时）在回写后触发对应设备重连即生效。
3. **结构型配置不能热改**：设备数量 / 面板布局 / Mock 开关 / 送风机启用影响运行期一次性建立的结构（状态数组、UI 面板、Reader/Controller 实现），热改会造成越界或状态不一致，因此不回写内存、照常写入配置文件并弹窗提示重启生效。

### 优化点
- 弹窗按改动内容区分文案，避免误导（不再一律说"重启程序后生效"）
- 重连均在后台线程执行（`Task.Run`），不阻塞 UI；复用各控制器已有的同步锁，避免与采集线程并发冲突
- 保存弹窗仍只弹一次：结构型提醒已并入设置窗口的保存提示，主窗体不再重复弹

## V1.39 — IO 备用通道映射界面/配置全链路十六进制统一（2026-08-09）

### 改动范围
- `Models/IoOutputChannelRemap.cs` — 通道解析统一为十六进制（带 0x 前缀，如 `@0x0A`），去掉旧的十进制位号分支；内部解析成 0~31 十进制位号供位运算
- `Controls/IoMappingEditorPopup.cs` — 通道微调框加 `0x` 前缀（显示 `0x00~0x1F`，与寄存器风格统一）；保存时通道按十六进制输出（`0x2000@0x0A->...`），配置与界面所见即所得；列宽与提示文案同步调整
- `Controls/DataGridViewNumericUpDownCell.cs` — `DataGridViewHexNumericUpDownCell.ShowPrefix` 注释更新（寄存器/通道均显示前缀）
- `Dialogs/SettingsForm.cs` — `IoBackupChannelMappings` 说明文案更新为"寄存器@通道均十六进制"
- `Views/MainForm.cs` — 备用通道映射解析处注释更新为十六进制格式
- `App.config` — 默认 `IoBackupChannelMappings` 及注释改为十六进制通道（如 `0x2000@0x00->0x2009@0x01`）

### 为什么这么改
1. **界面与配置显示不一致易引发误会**：原实现界面用十六进制微调框（`00~1F`），但保存时把通道转回十进制位号（`@10`），用户输入 `0A` 打开配置文件却看到 `@10`，会怀疑自己输错或软件有 bug。
2. **统一为"界面=配置=十六进制，换算只在代码内部"**：寄存器、通道在配置与界面均以十六进制出现（所见即所得），`IoOutputChannelRemap` 解析时内部再转成 0~31 十进制位号做位运算，对外无感。

### 优化点
- 弹窗提示明确"保存后配置与界面显示一致，无需换算"，消除用户困惑
- 通道显示加 `0x` 前缀后与寄存器风格完全统一，不易把十六进制误读成十进制
- 通道解析只认 `0x` 前缀十六进制（0x00~0x1F），其它写法直接报错，杜绝歧义

## V1.38 — IO 备用通道映射可视化编辑 + 修复设置名称长按复制（2026-08-09）

### 改动范围
- `Controls/IoMappingEditorPopup.cs`（新增）— IO 备用通道映射编辑器：一行一条映射，五列"原寄存器 → 原通道 → 新寄存器 → 新通道"，寄存器（0x0000~0xFFFF）与通道（00~1F）均为十六进制微调框，支持修改/添加/删除
- `Controls/DataGridViewNumericUpDownCell.cs` — 新增十六进制数字单元格 `DataGridViewHexNumericUpDownCell`（编辑时弹出 Hexadecimal NumericUpDown 微调框），支持 `HexDigits`（显示位数）与 `ShowPrefix`（0x 前缀）配置，寄存器与通道共用
- `Models/IoOutputChannelRemap.cs` — 通道号合法范围由 0~15 放宽到 0~31
- `Controls/IpListEditorPopup.cs` — 单元格类由 `DataGridViewIpListCell` 更名/泛化为 `DataGridViewPopupEditCell`（点击弹编辑器），供 IP 列表与 IO 映射共用
- `SettingsForm.cs` — IoBackupChannelMappings 改用弹窗编辑器；修复"设置名称长按复制"气泡不显示
- `AgingTestSystem.csproj` — 注册新控件文件

### 为什么这么改
1. **IO 映射手输易错**：原 IoBackupChannelMappings 是自由文本，需手输"寄存器@位->寄存器@位"（如 `0x2000@0->0x2009@10`），格式/寄存器偏移极易写错。改为弹窗可视化编辑：一行一条映射、左右各显示"寄存器 + 通道"两个十六进制微调框，保存时自动换回原配置格式，既直观又杜绝非法项。
2. **界面与配置同构、无需换算**：界面四列（原寄存器/原通道/新寄存器/新通道）与配置格式"寄存器@位"一一对应，只是把配置里的十进制位号（0~31）显示成两位十六进制（00~1F），保存时再转回十进制位号，与 `IoOutputChannelRemap.ParseAll` 解析格式完全一致，不再依赖起始寄存器地址换算。通道号放宽到 0~31（00~1F），兼容 32 点/模块。
3. **长按复制气泡不显示（根因）**：
   - 长按计时到点（鼠标仍按住、被表格捕获）时调用 `ToolTip.Show`，气泡被鼠标捕获盖住不渲染。改为 Timer 到点只负责复制、先暂存提示内容，等松开鼠标（MouseUp）后再弹出气泡。
   - `ToolTip.Show(..., Point, ...)` 的 Point 是**控件客户端坐标**（内部实现 `windowRect.left + point.X`），原实现误用 `RectangleToScreen` 传了屏幕坐标，气泡被移到屏幕外看不到；且点坐标版首次调用时原生窗口尚未建好会**直接不显示**。改为 `OnShown` 里先空转一次预激活气泡，并改用 `Show(text, window, duration)`（光标定位版，走与悬停提示相同的 SemiAbsolute 路径）——松开鼠标时光标就在单元格上，气泡自然落在单元格附近。
   - 松开鼠标时目标单元格行列被 Timer 重置成了 -1，气泡定位失败直接返回。改为在 MouseDown 时把行列记到独立的 `_pressTooltipRow/_pressTooltipCol`，与复制计时用的 `_pressRow/_pressCol` 分开。

### 优化点
- IO 映射弹窗：原/新寄存器用 4 位十六进制（带 0x 前缀）、原/新通道用 2 位十六进制微调框防误输；中间箭头列只读固定显示 "→"；支持 Delete 键删行；添加按钮直接新增一行空白映射
- `DataGridViewNumericUpDownCell` / `DataGridViewHexNumericUpDownCell` 重写 `Clone()` 带出自定义属性（`Maximum`/`HexDigits`/`ShowPrefix` 等），否则 CellTemplate 克隆成实际单元格时会回落默认值——修复寄存器只显示 2 位、无 0x 前缀的问题
- 删除行逻辑沿用"先收集索引再删"，避免 DataGridView 删除当前行时误删全部
- 设置名称长按复制改为 Timer 触发，按住 700ms 即复制，松开鼠标后气泡提示（气泡定位到单元格正下方）

## V1.37 — 候选 IP 弹窗去掉顶部蓝色标题栏（2026-08-09）

### 改动范围
- `Controls/IpListEditorPopup.cs` — 移除弹窗顶部的蓝色标题栏，改为无标题栏 + 浅灰描边，内容区整体上移紧凑排列

### 为什么这么改
- 蓝色标题栏与弹窗整体白底轻量风格不协调，视觉偏重；去掉后更贴合设置窗口的简洁观感

### 优化点
- 弹窗高度由 306 压缩到 268，无边框窗体用浅灰边框勾边，仍保持边界清晰

## V1.36 — 候选 IP 弹窗修复删除全清 bug + 界面改版（2026-08-09）

### 改动范围
- `Controls/IpListEditorPopup.cs` — 修复"删除最后一个 IP 导致全部被删"的 bug；界面改为与系统设置一致的 SunnyUI 蓝主题风格
- 弹窗新增蓝色标题栏、蓝/橙/灰样式按钮、水印输入框、操作提示文字，支持表格内按 Delete 键删除

### 为什么这么改
1. **删除最后一项会清空全部**：原删除逻辑在反向遍历中边删边判断 `Rows[i].Selected`。DataGridView 删除"当前行"时会自动把选区重设为"锚点行 → 新的当前行"之间的区间，若删的是最后一行，新区间覆盖所有剩余行，导致后续遍历把所有行都判为选中并全部删掉。改为先收集选中行索引、再倒序删除，并从根上不受删除过程中的选区变化影响。
2. **弹窗太朴素**：原弹窗是默认 WinForms 控件拼装，与程序整体 SunnyUI 蓝主题风格不搭。改用 UIDataGridView / UIButton / UITextBox，加蓝色标题栏与提示文字，视觉与系统设置窗口统一。

### 优化点
- 删除逻辑不再依赖删除过程中的选区状态，行为稳定可靠
- 表格内按 Delete 键即可删除选中行，操作更顺手

## V1.35 — 系统设置：FanIpCandidates 下拉编辑 + 设置名称长按复制（2026-08-09）

### 改动范围
- `Controls/IpListEditorPopup.cs`（新增）— 候选 IP 列表编辑弹窗：一行一个 IP 的可编辑表格，支持直接修改、输入新增、选中删除；确定前逐行校验 IPv4 格式
- `Controls/IpListEditorPopup.cs` — 新增 `DataGridViewIpListCell` 单元格（只读 + 右侧下拉箭头提示）
- `SettingsForm.cs` — FanIpCandidates 改用 IP 列表单元格，点击弹出编辑；设置名称列支持鼠标左键长按（≥700ms）复制到剪贴板并气泡提示
- `AgingTestSystem.csproj` — 注册新控件文件

### 为什么这么改
1. **FanIpCandidates 手输易错**：原为自由文本，需手输逗号分隔的 IP 列表，格式/非法 IP 易出错。改为点击弹出编辑器：一行一个 IP、可逐个修改/新增/删除，提交前逐行校验 IPv4，既直观又杜绝非法 IP 入库。
2. **设置名称无法复制**：设置名称列是只读的，用户想复制某个配置项的 key（用于向技术人员反馈/查配置）只能手抄。增加左键长按 ≥700ms 复制到剪贴板，附带"已复制"气泡提示。

### 优化点
- 弹窗支持回车快速新增、非法/重复 IP 即时拦截提示、点击弹窗外区域自动取消
- IP 单元格右侧绘制下拉箭头，视觉上提示"这是可点击编辑的下拉列表"
- 弹窗自动定位到单元格正下方，超出屏幕底部时自动改到上方显示

## V1.34 — 系统设置搜索框与表格滑块修复（2026-08-09）

### 改动范围
- `SettingsForm.cs` — 配置表格去掉无意义的垂直滑块；搜索框改用 SunnyUI UITextBox、放大字号与尺寸

### 为什么这么改
1. **表格右侧出现无意义滑块**：每个配置表格的高度是按"可见行行高之和"自动算好、恰好完整显示全部内容的（外层滚动面板负责整页滚动），本不需要表格自带的垂直滚动条。`ScrollBars.Vertical` 在搜索过滤后仍显示滑块，且清除搜索后部分表格滑块残留（DataGridView 滚动条状态刷新不稳定）。改为 `ScrollBars.None` 后从根上消除。
2. **搜索框字体过小**：原输入框字号 9F（旁边标题 11F 加粗），22px 高，视觉不协调。改为 SunnyUI UITextBox（与 FanTestForm/CommunicationTestForm 输入风格一致），字号 11F、加宽加高，并带"输入关键字过滤配置项"水印提示。

### 优化点
- 搜索框类型由 WinForms TextBox 换成 Sunny.UI.UITextBox，风格与主程序统一
- 表格固定不出现垂直滑块，搜索/清除搜索后布局始终干净一致

## V1.33 — 系统设置窗口防错输入（2026-08-09）

### 改动范围
- `SettingsForm.cs` — 设置值列按配置项类型自动切换编辑控件；串口通讯参数改为下拉选择；修复数值框显示问题
- `Controls/DataGridViewNumericUpDownCell.cs` — 数字单元格控件（NumericUpDown 编辑），修复非编辑态不显示数字的 bug
- `Controls/DataGridViewStrictComboBoxCell.cs` / `DataGridViewEditableComboBoxCell.cs` — 新增只读/可手输下拉单元格
- `ModbusRtuBarometerReader.cs` — 停止位 15（=1.5）解析支持

### 为什么这么改
1. **布尔项乱输**：原 true/false 类配置项是可自由输入的文本框，用户手输 "ture"、"TRUE " 等非法值，保存校验虽会拦截但体验割裂。改为下拉框只允许选 true / false，从源头杜绝。
2. **串口名乱输**：PortName 需手动输入，记错/打错端口名导致气压表连不上。改为下拉框列出系统当前检测到的所有串口，直接选择；保留"留空=自动识别"语义（空值不强制选第一个）。
3. **数字项乱输**：波特率、超时、寄存器等数字项自由输入，易输入越界/小数位数错误。改用 NumericUpDown 微调控件，按配置项限制上下限、步进、小数位。
4. **串口通讯参数手输易错**：波特率/数据位/停止位/校验位直接手输，容易填错枚举名（如 Parity 写 "Odd" 与 ScannerService 的 switch 大小写约定不一致导致解析为 None）。统一改为下拉选择，显示中文/标准值、存储内部约定的枚举值。

### 优化点
- 布尔下拉：当前值是 true 时下拉只有 false（反之亦然），即一键切换
- 串口下拉：实时读取系统已装串口；当前值不在列表时也保留，避免已配置但未插的端口被误清（气压表 PortName 与扫码枪 ScannerPort 共用同一套下拉）
- 波特率下拉：列出低速/中速/高速常用档位（110~921600），且**支持手输自定义波特率**（可编辑下拉，输新值自动补入）
- 数据位下拉：5 / 6 / 7 / 8
- 停止位下拉：1 / 1.5 / 2（配置存 1/15/2，15 表示 1.5，与 ScannerService 约定一致，ModbusRtu 同步支持）
- 校验位下拉：无校验(NONE) / 奇校验(ODD) / 偶校验(EVEN) / 1校验(MARK) / 空格校验(SPACE)。界面显示中文，存值经映射归一为标准枚举名 None/Odd/Even/Mark/Space，兼容历史小写/中文/缩写写法，任何非法字符一律归为 None，杜绝配置里出现非法校验位导致解析失败
- 数字微调：每项独立配置范围（如 SerialReadTimeoutMs 10~60000、AlarmPressureThresholdKPa ±200 两位小数），保存前仍做二次校验
- 修复 DataGridViewNumericUpDownCell 非编辑态空白不显示数字的问题（FormattedValueType 改为 string + 重写 GetFormattedValue）
- 单元格统一样式：白底深字 + 主题蓝选中色，下拉框/数字框不再出现系统灰色底

## V1.31 — 配置搜索 + 配方操作优化 + 配方自动检索（2026-08-09）

### 改动范围
- `SettingsForm.cs` — 新增搜索框，支持按关键字快速过滤配置项
- `RecipeManagerForm.cs` — btnUpdate 找不到同名配方时直接添加，btnAdd 提示文案优化
- `BatchRecipeForm.cs` / `StationSettingsForm.cs` — 配方名称输入框新增自动检索，输入时弹出模糊匹配列表，选中后自动填写配方名称、延时时间、启动时间、极限温度
- `RecipeAutoCompleteProvider.cs` — 新增配方自动检索辅助类，支持防抖、键盘导航、鼠标选择
- `BatchRecipeForm.Designer.cs` / `StationSettingsForm.Designer.cs` — Dispose 方法增加自动检索资源释放

### 为什么这么改
1. **配置搜索**：SettingsForm 配置项多达 40+ 个并按 8 个分类分布，每次查找特定配置项需要逐行滚动扫描，效率低。增加搜索框后输入关键字即可快速过滤显示匹配项，无需记忆配置项所在分类。
2. **配方操作优化**：btnUpdate 原来找不到同名配方时仅弹窗提示"列表中不存在"，用户需要再点"添加"才能完成操作，流程割裂。改为找不到时自动添加，减少操作步骤。btnAdd 提示语增加"覆盖"二字，语义更清晰。
3. **配方自动检索**：用户在批量设置/工位设置窗口输入配方名称时，需要手动记忆已存在的配方名称并逐个字符输入，无法快速复用已有配方配置。增加自动检索后，输入时弹出模糊匹配列表，选中即可自动填充所有参数，大幅提升操作效率。

### 优化点
- SettingsForm 搜索支持对"设置名称"和"说明"两列同时匹配，匹配结果实时过滤并重新布局，无匹配的分类自动隐藏
- 配方自动检索使用 300ms 防抖定时器，避免频繁刷新列表导致界面卡顿
- 支持键盘上下键导航、Enter 确认、Escape 关闭，交互与主流 IDE 一致
- 点击下拉框外部区域（含窗体空白处、其他控件）时收起匹配列表，文本框内容保持原样（视为未选择），不再清空
- 用户从匹配列表选中配方回填后不再重复弹出匹配列表，只有产生新的输入时才继续匹配
- 下拉框失焦收起改为消息级过滤（`IMessageFilter` 监听鼠标左键），点击不可获得焦点的区域也能正常收起

## V1.32 — 系统设置窗口排版与换行优化（2026-08-09）

### 改动范围
- `SettingsForm.cs` — 设置列表内容自动换行、行高按内容计算、搜索框样式优化、搜索过滤排版修复

### 为什么这么改
1. **内容显示不全**：设置列表行高固定 24px，"说明""设置值"列的长文本（如 IO 备用通道映射表）换行后被截断显示不全。改为三列均启用换行，行高按内容（TextRenderer 测量换行高度）逐行计算，保证内容全部显示。
2. **搜索过滤排版错乱**：搜索"映射"等关键字时，被过滤隐藏的分类仍占用页面排布空间、网格高度仍按全部行数计算，导致结果与搜索框之间出现大片空白；且窗体尚未显示时控件 `Visible` 恒为 false，若用控件可见性判断分类是否参与排布，会直接导致初始布局全部错乱。
3. **搜索框样式**：原"搜索配置项"标签 9pt 偏小不醒目，加大并加粗、主题蓝着色。

### 优化点
- 设置列表三列（设置名称 / 说明 / 设置值）均启用自动换行，行高按每行内容测量计算（`ComputeRowHeight`），长文本全部显示不被截断，最小行高保持 24
- 搜索过滤排版修复：隐藏分类用独立 `_sectionVisible` 状态数组标记（不依赖控件 `Visible`），不参与排布；网格高度仅统计可见行
- 搜索框标签"搜索配置项："加大加粗（11pt Bold）、主题蓝着色，搜索框与清除按钮位置同步右移

## V1.30 — IO 触发后气压表压力值快速刷新（快速跟踪，2026-08-08）
- **需求**：触发 IO（开/关真空阀、上/断电、启动/停止测试）后，气压表压力值更新有约 1~3 秒延时——72 台气压表逐台串行 Modbus RTU 轮询，一轮全量采集耗时决定了刷新周期，IO 写后必须等下一轮轮询才读到压力变化。
- **实现**：写输出成功后对目标工位启动**独立高频补读定时器（250ms/次）**，只读这几台压力 + IO 状态，立即广播刷新对应面板，**压力变化 ≤0.5 秒可见**；跟踪窗口 12 秒（覆盖真空建立 15s 内从常压抽到目标负压）后自动退出，恢复正常全量轮询。
- **埋点**：`DeviceManager.SetOutput`（统一覆盖主窗体"开启真空"、设备手动窗体开/关阀与上/断电、工位设置"载台下电"）+ `StartTesting`（跟踪真空建立）+ `StopTesting`（跟踪压力回落）；报警联动 / 老化到时 / 人工复位**不触发**（面板已即时标"故障/空闲"状态色，压力回落非紧急）。
- **线程安全**：快速跟踪集合受锁保护、定时器防重入（`Monitor.TryEnter`，对齐 `_collectLock` 模式）；单台串口读复用 `ReadData` 内部 `_syncRoot` 与全量轮询互斥，单台仅 ~10~20ms 开销，不影响 72 台正常采集。

## V1.29 — 移除 JSON 兼容 + 工位面板待机配色调整（2026-08-08）
- **移除 `[JsonProperty]` 兼容**：`RecipeConfig` / `StationCacheEntry` 的时间字段不再用 `[JsonProperty]` 保留旧 JSON 键名（`DelayStartTime`/`DelayArriveTime`），JSON 键名直接用新属性名（`DelayTime`/`StartTime`）；旧 Recipes.json / StationSettings.json 数据已不兼容，需删除重建（用户已清空旧配方，不保留兼容代码）。
- **工位面板配色微调**：`boxPower` 下电（原红底白字）与 `boxVacuumOpen` 真空关（原红底白字）改为浅灰(LightGray)底黑字，与每行最右侧全选按钮同色，降低待机状态的视觉刺激；红色仅保留给工作状态"故障"（`boxWorkState`）。

## V1.28 — 时间输入样式统一：冒号分隔 + NumericUpDown（2026-08-08）
- **配方管理窗口**：延时时间 / 启动时间由"时/分/秒"三个带单位标签的数字框，改为 `时:分:秒` **冒号分隔**显示（单位标签改为 ":"，移除"秒"单位标签）。
- **批量设置配方窗口**：删除"延时时间2"，仅保留"延时时间"与"启动时间"；两者均由三个 TextBox 改为三个 `NumericUpDown`（时0-99/分0-59/秒0-59，冒号分隔），控件命名同步改为 `nudDelayHours/Minutes/Seconds`、`nudStartHours/Minutes/Seconds`。
- **工位设置窗口**：`txtDelay` / `txtStart` 改为与配方管理窗口一致的三框 `NumericUpDown` 冒号分隔样式（`nudDelayHours/Minutes/Seconds`、`nudStartHours/Minutes/Seconds`），相关逻辑同步调整——读取用 `GetTimeSpan` 组合三框，回填用 `SetTimeInputs`（越界钳制到控件范围），成功提示时间文本用 `GetTimeText`。
- 说明：三个窗口的时间输入样式 / 命名自此统一，字段映射对齐——**延时时间 → `DelayTime`（工位面板"延时开启"），启动时间 → `StartTime`（工位面板"延时到达"）**；字段名同步统一（原 `DelayStartTime`/`DelayArriveTime` 改名为 `DelayTime`/`StartTime`）；批量设置窗口原"启动时间"输入被丢弃（校验后不保存）的缺陷一并修复，现在两个时间都写入配方。（注：V1.28 提交时曾用 `[JsonProperty]` 兼容旧 JSON 键名，V1.29 已移除，见下条。）

## V1.27 — 配方管理移除"保存设置"按钮：操作即自动落盘（2026-08-08）
- **配方管理窗口**：移除底部"保存设置"按钮（按钮、底部面板及窗口高度一并调整）。
- **操作即持久化**：添加 / 更新 / 删除 每次操作成功后自动把整个配方列表写入 Recipes.json
  （新增 `PersistRecipes()` 统一落盘，失败弹窗提示），关闭窗口不再需要手动保存，改动重启程序不丢失。
- 说明：原"保存设置"是唯一落盘入口（添加/更新/删除仅改内存），故先让三个操作按钮自动落盘，再移除该按钮。

## V1.26 — 配方应用打通：批量/工位设置保存配方 + 工位下电（2026-08-08）
- **批量设置配方"加入队列"**（重写）：先把当前配置的配方保存到本地配方列表（Recipes.json，有同名弹窗询问是否覆盖更新）；
  再判断是否选中了工位面板——一个都没选中 → 提示"请先选择工位"（配方已保存，可关闭窗口选好工位后重新打开本窗口点击加入队列，
  或在「参数设置 → 配方管理」中选用该配方）；有选中 → 把配方名称 / 延时开启 / 延时到达应用到所有选中的工位面板。
- **工位设置窗口"保存"**：应用配置到本工位面板（写 DeviceManager 工位静态信息 → 采集叠加 → 面板更新）+
  缓存配置（新增 `StationSettingsCache`，`StationSettings.json`，下次点击该工位"设置"按钮自动回填上一次缓存）+
  保存配方到本地配方列表（同名询问覆盖更新）。
- **工位设置窗口"加入对列"**：与"保存"一致——把配置加载到对应工位的 WorkstationPanelView + 保存配方到本地配方列表。
- **工位设置窗口"下电"**（实现）：关闭当前工位载台上电输出
  （内部编号 = `TotalInputs + TotalBarometers + deviceId`，已下电则仅提示）；"破空"业务待确认，保留 TODO。
- **`RecipeStorage.SaveWithDuplicateCheck`**：批量/工位窗体共用的"界面配方 → 共享列表 + 落盘"方法，
  同名配方弹窗询问覆盖更新，新增配方自动分配编号。
- 批量设置配方窗口改构造 `(DeviceManager, 共享配方列表, 选中工位)`，移除旧队列/事件（OnRecipeAdded/GetRecipeQueue）逻辑；
  工位设置窗口改构造 `(DeviceManager, DeviceConfig, 共享配方列表, deviceId)`，主窗体传 `_recipes` 使新配方即时进入配方管理列表。

## V1.25 — 配方管理编辑化 + 持久化 + 批量设置窗口对齐（2026-08-08）
- **配方管理窗口设置区改可编辑**：配方名称改输入框；延时时间 / 启动时间按 **时、分、秒** 三个数字框分拆输入（`NumericUpDown`）；极限温度数字框（℃）。左侧列表选中某配方时，设置区自动同步显示该配方的设置内容。
- **添加防重名**：点击"添加"时若名称已存在，弹窗"已存在XXX配方，是否更新配方"（确定=走与"更新"相同逻辑）；"更新"按当前名称定位列表中配方并覆盖；"删除"按当前名称定位，弹出二次确认（确定/取消）。
- **配方持久化**：新增 `RecipeStorage`（程序目录 `Recipes.json`）；"保存设置"把整个配方列表及每项设置落盘，主窗体启动时 `LoadRecipes()` 自动加载恢复（V1.12 配方管理窗口自此闭环）。
- **配方列表滚动**：左侧列表固定行高并启用垂直滚动条（`ScrollBars.Vertical`），条目过多时区域右侧出现滑块可下滑查看。
- **批量设置配方窗口对齐**：修复左侧标签与右侧输入框垂直错位——标签由"靠单元格顶部"改为 `Dock=Fill + MiddleLeft` 垂直居中，与右侧输入框中心对齐。

## V1.24 — 面板"设置"智能分流 + 长按取消全选 + 离线/配色优化（2026-08-08）
- 面板"设置"按钮点击逻辑优化（`Panel_OnSetClicked`）：点击时若按钮所在工位未被选中，先将其加入选中集合（选中框同步显示）；
  再按选中数量分流——只选中 1 个工位 → 弹出该工位的 `StationSettingsForm`；选中 2 个及以上 → 弹出 `BatchRecipeForm`。
- 批量设置配方窗口打开逻辑抽为公共方法 `ShowBatchRecipeForm()`，供"批量设置配方"菜单与面板多选场景复用（队列处理/日志一致）。
- 长按空白处**取消全部选中**：全表未选中时长按仍为"选中该工位"；已有选中（选中框可见）时长按空白处 → 取消全部选中并隐藏所有选中框
  （`ClearAllSelectionRequested` 事件 → 主窗体 `Panel_ClearAllSelectionRequested` 统一置 `IsSelected=false` + `UpdateSelectionBoxVisibility`）。
- **离线/未加载状态标红**：状态栏"在线"统计全部离线（`在线: 0/N`）时 `toolStripStatusLabelOnline` 文字变红（默认即红，其余情况恢复默认色）；
  默认显示"未连接"的 `lblFanState`/`lblCommStatus` 设计时默认字体改红色，数据未加载时直观告警。
- **工位面板配色**：`boxVacuumOpen` 默认"真空关"改为红底白字（与加载后一致）；`boxWorkState` 测试中=繁忙改红绿灯**黄灯色**（Gold，原绿），空闲改 **LimeGreen**（原浅灰）；`boxPower` 上电状态灯未上电由灰色改 **红色**（上电仍为 LimeGreen）。

## V1.23 — 通讯测试窗体共享主程序连接（2026-08-08）
- 通讯测试/送风机测试窗体**不再自建 TCP 连接**，全部复用 `DeviceManager` 的共享连接，消除"连接数翻倍、IP 改了测试窗体能连主程序连不上"的割裂。
- `ModbusTcpIoController` 新增线程安全 `ReadHoldingRegisters`/`WriteSingleRegister`（与采集线程共用同一条连接 + `_syncRoot` 串行化）。
- `DeviceManager` 新增门面：`Config`、`ReadHoldingRegisters`、`WriteSingleRegister`。
- `CommunicationTestForm`：移除自建 TcpClient/IModbusMaster/心跳/重连定时器，连接状态按 `IsIoConnected` 1s 刷新（不发报文）；构造函数改 `(DeviceManager)`。
- `FanTestForm`：移除自建 FanControllerClient，走 `ReconnectFan`/`GetFanData`（读缓存零报文）/`StartFan`/`StopFan`；IP/端口只读显示主程序配置。
- **修复：扫码枪启动改回 UI 线程**。`MainForm` 启动优化曾把 `_scanner.Start()` 放进 `Task.Run`，而 `ScannerService` 内部依赖 UI 消息泵（`System.Windows.Forms.Timer` 重连/心跳 + `NativeWindow` 监听 `WM_DEVICECHANGE` 热插拔），后台线程下定时重连与热插拔监听全部失效，扫码枪拔插后检测不到。改为 `RunOnUi` 封送回 UI 线程执行。

## V1.22 — 通讯测试窗体补强（2026-08-08）
- 新增**一键遍历**：每 500ms 点亮一路、其余全灭，72 路通断跑马灯检测 DO 接线；单拍后台线程执行（写寄存器+读回真实状态），按钮状态实时反映真实通断。
- 打开即**后台自动连接**（3s 静默重试）+ **心跳**（1s 读 0x2000 探测，断连弹窗提醒）；所有 Modbus 读写共用锁串行化。
- 备用目标寄存器 0x2009 写入改**读-改-写**（`WriteBackupRegister`+`ComputeRemapTargetMask`，各测试只动自己映射位，对齐主项目 RMW）。
- 映射提示窗改**非模态悬浮窗**（WS_EX_NOACTIVATE 不抢焦点，单实例更新文本）。

## V1.21 — 通讯测试窗体 SunnyUI 重构 + 映射点击提示（2026-08-08）
- 原生控件改 SunnyUI（UIForm 标题栏 + UITabControl + UILedBulb 状态灯等）；点击被映射通道弹提示告知实际输出通道。

## V1.20 — 新增通讯测试窗体（2026-08-08）
- IO 耦合器 DO 输出手动测试：负压阀（Y000~Y107 @0x2000~2004）与载台上电（Y110~Y217 @0x2004~2008）两页 9×8 灯按钮，读-改-写不覆盖共享 0x2004；复用 App.config 备用通道映射。

## V1.19.x — 主界面工位交互与显示系列（2026-08-08）
- **V1.19.11** 工位 SN/配方/延时关联打通：新增 `StationInfo` + DeviceManager 每工位静态信息，采集时叠加到工位数据；工位设置窗口"保存"实现；ID 绑定把"工位→SN"写入；菜单"LOG记录"→"日志记录"。
- **V1.19.9** 报警阈值与界面单位统一为 kPa；**V1.19.10** 真空开启显示文字化（开=绿底白字/关=红底白字）+ 压力框加宽。
- **V1.19.5~6** 选中交互改"长按约 0.8s 选中 + 有选中才显示绿✓框"，选中框显示时单击切换；**V1.19.1~4** 行全选按钮文字实时反映行选中、工作状态配色统一信号灯色系、面板布局微调（SN/配方改 Label）。
- **V1.19.7~8** 权限显示角色名着色（管理员红/技术员天蓝/操作员绿）、用户管理窗体优化。
- **V1.19.12** 面板高度减小；主菜单"帮助"→"关于"，"关于"→"版本说明"。

## V1.18.x — 工位设置窗口 + 行全选（2026-08-08）
- 新增 `StationSettingsForm`（面板"设置"按钮打开，SN/配方/延时/启动时间/极限温度 + 破空/下电/保存/加入对列）。
- 行"全选/取消"按钮替代 Set(SEL_N)；状态文字改中文（空闲/选中/繁忙/故障）。

## V1.17 — 系统设置窗口（2026-08-07）
- "关于→设置"（仅管理员）弹出 `SettingsForm`：单页按分类（基础/串口/IO耦合器/气压表寄存器/报警/送风机/老化业务/扫码枪）表格编辑 App.config 全部配置项，保存前按类型校验，写回 exe.config 重启生效。

## V1.16.x — 扫码枪 + 连接自愈（2026-08-07）
- 接入真实扫码枪 `ScannerService`（WMI 识别串口 + 串口读码 + 断线重连）；ID 绑定窗体扫码自动识别"工位号(2位数字)/SN"。
- CH340 串口自动识别（`SerialPortHelper`，VID_1A86/PID_7523）+ 端口缓存 `BarometerPort.cache`（与送风机 FanLastIp.cache 同款工控机记忆）。
- 连接心跳机制（静默自愈）：断连 1~3s 内提示一次 → 后台静默持续重连（只在连上/断开边沿各提示一次），操作时按需重连+弹窗兜底；耦合器/送风机断开不再拖垮气压表采集。
- **V1.16.1** 四项现场修复：①负压阈值 -95 写成 -9.5（0x0002 小数位寄存器 47/72 台不可靠返回 0，压力读取与阈值写入统一固定 `BarometerDefaultDecimalPlaces`=1）；②顶部通讯状态只判断 IO 耦合器；③送风机监视区"上部温度"→"当前温度"；④送风机状态文字+颜色。
- **V1.16.3~6** 扫码枪断连判定演进：监听 USB 插拔消息 → 心跳重跑 WMI 动态搜索（PnP 节点消失即判定断连，注册表/ReadExisting 均不可靠）→ 多消息类型 + 关句柄重搜兜底。
- 工位面板更名重设计（BarometerPanelView → WorkstationPanelView）；移除 TEST 菜单与 ScanSimulationForm；公共参数窗体改为批量写气压表阈值（后台线程 + 失败汇总）。

## V1.15 — 老化测试业务闭环 + 送风机接入（2026-08-06）
- 新增送风机接入（Modbus TCP 50000）：定值启停 + 温湿度监视 + 全局生命周期（首台启动/末台停止）+ **IP 自动识别**（候选列表 + FanLastIp.cache）+ 连接防呆/竞态修复。
- DeviceManager 测试状态机：启动/停止/报警复位/全部停止（急停）、真空建立确认、通讯失联报警、老化计时自动停止。
- 设备阈值写入能力（气压表 0x0010）；IO 输出**备用通道映射**（DQ 通道烧毁时启用）；事件 CSV 落盘（`TestEventLogger`）+ 历史记录读真实日志；新增单台手动控制对话框。

## V1.14 — 真实通讯链路接入（2026-08-03）
- 气压表 Modbus RTU + IO Modbus TCP 真实实现，`UseMockCommunication` 切换；报警边沿→关阀/断载台电联动；新增 CHANGELOG 与通讯接入文档。

## V1.13 — 用户数据持久化（2026-07-24）
- 用户账号持久化 Users.json（启动加载/修改即存/损坏重建）。

## V1.12 — 配方管理窗口（2026-07-24）
- 左右分栏：左侧列表（序号+名称），右侧详情（配方名称/延时/启动时间/极限温度）+ 添加/更新/删除。

## V1.09 — IO 分配表接入（2026-07-22）
- 新增 IoMapBuilder（内部编号↔三菱八进制 X/Y 映射）与 IoPointDefinition；气压表 IO 调整为 1 输入+2 输出（真空负压表/电磁阀/载台上电）。

## V1.08 — 用户权限系统 + 自适应布局（2026-07-21）
- 登录/用户管理/权限按钮控制；主窗体自适应分辨率（rootScrollPanel + Anchor，缩小时出滚动条）。

## ≤V1.07 — 早期（2026-07-21）
- 下拉菜单改无边框弹出窗体（V1.05）；partial class 拆分 + UTF-8 with BOM 修复设计器（V1.01/07）；代码审查 33 项修复（V1.06）；初始 Mock 架构（V1.00~V1.04）。
