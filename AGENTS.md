# AGENTS.md — AgingTestSystem 项目指南

> 本文件是 AI 助手在操作本项目前的**强制前置阅读**。开工前先读本文件，明确角色、约定与红线。
> 优先级：本文档 > 项目已有代码风格 > 通用最佳实践。

## 项目角色

你是本项目（Windows 窗体 C#/.NET Framework 应用）的**资深维护工程师**，负责按用户需求改代码、修 bug、沉淀约定。改动必须**可编译、可运行、风格统一**，并在关键改动后更新 `CHANGELOG.md`。

## 技术栈

- .NET Framework 4.x WinForms（非 .NET Core/.NET 5+，勿引入其语法/API）
- C#，语言版本取决于编译器（VS2019/2022 默认），以现有代码风格为准
- 关键库：SunnyUI（界面）、NModbus（Modbus 通讯）、Newtonsoft.Json（序列化）、System.Management（WMI/串口识别）
- 构建：`MSBuild.exe AgingTestSystem/AgingTestSystem.csproj`（见下方"构建命令"）
- 仓库：github.com/lq700212/AgingTestSystem，主分支 `main`，提交信息用中文，风格参考 `git log`

## 铁律（违反即返工）

1. **文件编码必须是 UTF-8**（无 BOM 或带 BOM 均可，跟随同目录文件）。
   - **禁止**用 PowerShell `Add-Content` / `Out-File` 默认编码写含中文的文件 —— 会写成 GBK 导致乱码。
   - 写文件用 write 工具；仅追加/改一行用 edit 工具。
   - 新增中文文件后自查：`[IO.File]::ReadAllText(path, UTF8).Contains("预期中文")` 能命中。
2. **不提交运行时数据与机密**：`Users.json`（密码为 PBKDF2 哈希，明文不落盘，见 `PasswordHasher.cs`）、`Recipes.json`、`StationSettings.json` 等程序运行生成的 json 一律 gitignore，绝不入库。
3. **改动后必须构建验证**，禁止提交编译不过的代码。
4. **不主动 commit/push**，除非用户明确要求；提交前先 `git status` + `git diff` 确认只包含预期改动。
5. **代码注释要详细，让小白能看懂学会**：关键方法/流程/边界条件/配置依赖必须写清"做什么 + 为什么这么写 + 怎么改"，杜绝只写变量名的废话注释（如 `i++ // 自增`）。允许的详细注释样式参考 `WorkstationGridView.cs` / `RecipeManagerForm.cs` 头部与关键方法。

## 代码约定

- 类、方法、属性用 PascalCase；私有字段 `_camelCase`；接口前缀 `I`；常量全大写或 PascalCase 跟随现有风格。
- 界面/控件命名：`btn`/`txt`/`nud`/`cmb`/`grid`/`pnl` 等匈牙利前缀（跟随 Designer 风格）。
- 枚举与配置值的存储约定（**改动串口/配置相关必须先读此处**）：
  - `StopBits` 存字符串 `1` / `15`（=1.5）/ `2`；校验位 `Parity` 存标准枚举名 `None`/`Odd`/`Even`/`Mark`/`Space`。读写两端大小写兼容（ModbusRtu 用 `Enum.TryParse(…, true)`，ScannerService 用 `ToLowerInvariant()` 匹配）。
  - 备用通道号只认 `0x00`~`0x0F`（单个寄存器 16 个 bit；V1.62 血泪：文档曾写 0x1F，0x10+ 在执行侧静默失效）。解析层直接拒绝 0x10+ 并进 error；编辑弹窗微调框最大值同步 0x0F；执行侧 `MapOutputChannel` 对非法目标保持原通道（绝不写坏掩码）。
  - 界面可显示中文/友好文案，但**存到 App.config 的值必须经过归一化映射**（见 `SettingsForm.NormalizeParity` / `NormalizeStopBits`），禁止把非规范字符写进配置。
- 配置项编辑控件统一在 `SettingsForm.CreateValueCell` 按 key 分发（布尔/串口/波特率/数据位/停止位/校验位/数字/文本）。新增串口类配置项时，**气压表与扫码枪两套 key（如 `PortName`+`ScannerPort`）都要覆盖**，共用同一套映射逻辑。
- **新增 App.config 配置项五处同步（V1.66 血泪）**：`DeviceConfig` 属性 + `App.config` key（含中文注释）+
  `MainForm` 读取 + `SettingsForm`（`_boolKeys` **和** `ValidateValue` 布尔分支 **两个名单都要进** +
  `_descriptions` + `_categories`）。`ValidateValue` 的布尔分支是硬编码 switch，
  只进 `_boolKeys` 会导致"YES 也能过"——回归里"布尔键一致"锁（逐项 true/false 过、YES 不过）
  就是防这个的，改完看它变绿才算完。
- **新增工艺策略 key 六处同步（V1.67，枚举型配置走另一套）**：`PolicyEnums` 枚举（0 值=现状行为，
  只能追加禁改序）+ `DeviceConfig` 属性（缺省=现状）+ `ProjectPolicyStore.PolicyKeys`
  （**唯一名单**，分流/校验/叠加全认它）+ `EnumOptions` 下拉选项（中文显示/英文存储，
  显示改文案不影响已存）+ `App.config` key（机器缺省）+ `MainForm` 读取
  （`ParsePolicyEnum` 非法兜底现状）+ `SettingsForm`（`_descriptions` + `_categories`
  + `ValidateValue` 名单分支 + `CreateValueCell` 下拉分支——枚举不进 `_boolKeys`，
  不归一化就"脏值也能存"）。回归里"三处同步锁"（PolicyKeys↔属性↔选项可解析）看绿才算完。
- **判定类分支先写 AgingSequencer 纯函数（V1.67 收紧）**：策略执行侧一律先加纯函数
  （`BuildStartBlockText`/`MapAlarmResult`/`ComputeResumeDurationSeconds`/
  `ValidatePolicyCombination`）并同步用例，`DeviceManager` 只做"调用决策 + IO + 日志"。
  快照加字段时同步列"哪些状态变迁写快照"（上电边沿漏一次，P4 用例红过）。
- **运行时文件跟项目还是跟机器（V1.67）**：配方/工位设置/主页布局/策略跟项目
  （`ProjectProfile.ResolveDataPath(file, true)`，`Projects/<项目>/`）；用户/快照/
  面板布局/日志/连接参数跟机器（程序目录）。改存储路径必须 rg 全仓扫字面文件名，
  老用例的字面路径会批量红（V1.67 实锤）；`Projects/` 住运行目录（bin/ 下，天然 gitignore）。
- **项目切换热更免重启（V1.72.10）**：`SwitchTo` 只写指针，换装走
  `MainForm.ReloadActiveProject` 八步（在测复查→暂停主采集→LoadConfig 重读→
  `CopyFrom` 就地换血→`StationSettingsCache.Reload`→`ClearProjectScopedState`→
  `LoadRecipes`→`ApplyHomeLayout`+顶栏+刷帧，失败提示重启）。
  铁律：`_config` 是 readonly 引用一律 `CopyFrom`（换引用服务侧不生效）；
  静态内存（工位缓存）切项目必须 `Reload`；清状态禁调 `StopAll`
 （那是写 IO 全 OFF 的急停语义）；历史项目目录的改名/删除收进
  `EnsureActiveProfile` 启动自愈，列表里永远干净。
- **设置表 tooltip 全覆盖（V1.67 用户原则）**：配置项说明悬停可见，超 40 字按
  `WrapTooltip` 换行（断点优先标点，不断英文单词）；新增配置项的 `_descriptions`
  写清"现状是什么/改了会怎样"，别只写名字。
- **MES 映射类配置约定（V1.68）**：触发器/字段映射/静态字段是自由文本，
  校验走 `MesMapping` 纯函数（vocabulary 改只改它；SettingsForm/上报器/用例三方共用），
  脏输入保存时拦（报出哪一组错）、上报时跳过（不带病全停）。`MesReporter.Transport`
  静态缝只许测试赋值，生产走真实 HTTP；用例必须 try/finally 复位。
  开关开了没配地址保存即拦（`MesEnabled` + 空 `MesEndpoint` 矛盾）。
  密钥（token/密码）显示解密/保存加密走 `MesCrypto`（DPAPI + 前缀；内存明文、文件密文）；
  自定义头名只认 ASCII（`IsAsciiLetter`，中文在 .NET 里算 Letter 的坑）。
- **规则表达式约定（V1.69）**：表达式引擎手写递归下降（不引脚本引擎，现场配错不能卡死采集）；
  变量 vocabulary 冻结 12 个（加变量要同步改组变量+注释+说明+用例四处）；
  NaN 比较恒 false（fail-safe，非 IEEE）；`&&` `||` 短路；规则只能加严不能松绑
  （自定义报警只多报、完成表达式 OR 只能提前、内置联锁动不了）；
  规则表解析 `RuleEngine.ParseRuleList`（行号报错，上限 20 条）；持续计时状态变迁
  （启动/复位/停止/急停）必须调 `ResetStation`，否则旧计时带到新任务。
- **SunnyUI 换肤约定（V1.71，全窗已切）**：Form→UIForm（蓝标题）/
  Label→UILabel / Button→UIButton / TextBox→UITextBox / ComboBox→UIComboBox /
  GroupBox→UIGroupBox / DataGridView→UIDataGridView；日志框/CheckBox/NumericUpDown/
  ListBox/状态条保持原生。语义色经 Style=Custom+FillColor/RectColor（原生 BackColor
  在自绘按钮上画不出来）；默认灰主按钮走 Sunny 蓝，取消关闭走 Sunny 灰。
  （V1.72.1 收敛：弹窗确认/保存/加入队列一律 DodgerBlue + 白字 + Custom，
  与登录确认同色；主窗启动蓝/批量绿等操作语义色不动。）
  - UIForm 自绘标题占 35px 客户区：绝对布局整体下移 35px + 窗体加高 + MinimumSize
    锁缩小；Dock 布局加顶 Pad(38)；Dock 窗内容高度不够时窗体加高（ID 绑定血泪：
    保存按钮被挤出）。Y&lt;35 的控件 Add 时被静默搬到 35（harness 实测）。
  - UIGroupBox 内容首控件 Y≥34：Sunny 组框标题约占顶部 30px，首按钮 Y=18 会上半
    压进标题区（V1.72 用户目检：主窗操作组整列下移 16px 解决，间距/分组不动）。
  - Sunny 控件默认 Style=Inherited，吃样式字体：名/值两套标签要么都不写 Font
    （同源永不分叉），要么两边写死同一套——只写一边必大小眼（V1.72 实锤：
    lblFanState 单写 9Bold，与继承 12 的名标签对不上，删字解决）。
  - `is Button/TextBox/ComboBox` 认不出 Sunny 自绘控件（UIButton/UITextBox/
    UIComboBox 不是原生子类；UILabel 是 Label 子类无碍）：类型判断改 Sunny 类型，
    ThemeManager 按类型名走分支；按钮改色走 `ApplyButtonColors`，读显示色走
    `GetEffectiveButtonColors`（FillColor 才是显示色）。
  - UITextBox 有 AppendText/Clear/Lines/PasswordChar/Multiline（无 ScrollBars/
    WordWrap，用 ShowScrollBar/WordWarp）；UIComboBox 有 Items/SelectedIndex/
    SelectedValue/DropDownWidth，DropDownStyle 换 `UIDropDownStyle` 枚举；
    UIButton 实现 IButtonControl（AcceptButton/CancelButton 照用）；删 FormBorderStyle
    行（UIForm 自己管边框）；字体不动（动 AutoScaleDimensions 是 DPI 红线）。
  - 多行块编辑必须逐行核对：Edit 工具会模糊匹配吞掉间隔行（V1.71 实锤：27 行块吞掉
    3 个 `new`，构造即 NRE）。改完 Designer 必跑"声明/实例化配对"扫描 +
    harness 构造一次（NRE 当场现形）+ 截图目检。
  - 运行时居中只调 X 不动 Y（V1.72.1 公共参数血泪）：Designer 的 Y 即运行 Y
    （已含 35px 标题区），CenterControls 只算水平居中；连 Y 一起写会导致
    "设计器看离标题太近、跑起来正常"，所见非所得。回归锁 CenterControls 调后 Y 不变。
  - Sunny UILabel 默认宋体 12pt，"开始时间:"实测 79px（V1.72.1 历史窗血泪）：
    标签 + 日期框间距按实测文本宽（`TextRenderer.MeasureText`，与生产同口径）留 ≥5px，
    不要按 Designer 的 Width/肉眼估；未显示时 Width 未布局，断言走实测宽。
    另注意 AutoSize 标签实占（Width/PreferredSize，含内边距）比 MeasureText 纯文本宽大
    （V1.72.3 公共参数窗实测大 3px）：居中算组宽取 `Max(实测文本宽, AutoSize 实宽)`，
    视觉间距断言直接锁 `nud.Left - lbl.Right ≥ 8`（不与生产同口径，防循环自证）；
    Designer 残留 Size（如 107）是旧字体过期值，算坐标一律以运行时探针实测为准，
    设计器初始坐标按运行真值摆（V1.72.3：lbl 36/nud 192）。
- **流程驾驶舱约定（V1.70）**：拓扑画死（物理锁死，通用连线编辑器会让客户删掉安全联锁，
  参考 HJVision mFormFlowEdit 后否决）；节点只挂真实 key（ key 必须全是 DeviceConfig
  真属性，回归锁），连线只读；保存走 `SettingsForm.PersistChanges`（与设置表同一条路，
  落盘语义只有一份）；节点位置纯视图存 `FlowLayout.json`（跟机器，缩放/平移不存）；
  缩放以鼠标为中心（IMessageFilter 预过滤滚轮），中键平移注意 AutoScrollPosition
  存取符号坑（setter 取正值）；双锁分开取防死锁（`GetPhaseCounts` 先状态后缓存）。
- **配方加字段三窗同步（V1.66）**：`RecipeConfig` 加字段 → 三个录入窗
  （`RecipeManagerForm`/`BatchRecipeForm`/`StationSettingsForm`：输入框 + 保存 + 回填 +
  自动检索回调 + 头部 ASCII 图）→ `SetStationRecipe` 下发 → `StationInfo`（+`Clone`）→
  `ApplyStationInfo` 叠加 → `BarometerData`（+`Clone`）。漏一处就是"存了用不上/下了传不到"，
  对照此链逐项打勾；`StationSettingsCache` 只在窗口需要回填时才加。
  **负压 0 值语义（V1.72.2 血泪）**：`SetStationRecipe` 只认 null = 保持/回退全局，
  下发 0 就是定格 0（阈值 0≈永远到位，真空保护形同虚设）；老配方文件缺字段读出 0，
  回填框显示"0"待人工复核，不设"0=全局"魔法回退。上站前逐条复核 `Recipes.json` 的 0 值。
- **SunnyUI 页签挂接红线（V1.63.2 血泪）**：`UITabControl` + `UIPage` 必须用
  `tabControl.AddPage(page)` 挂接（内部建 TabPage + Dock=Fill + 绑定 + Show()），
  禁止手写 `TabPage` 包裹 + `Controls.Add`——漏掉 `Show()` 会导致页面 `Visible=false`、
  控件全建好但整页空白（现场实锤：通讯测试页 72 灯全有但看不见，连接测试还正常）。
  判据：`tabControl.GetPage(page.PageGuid) != null` 且 `page.Visible == true`；
  验证走 `winforms-ui-debug` harness 直启 + 反射 + 截图，不要靠肉眼。
- **dev 最高权限账号约定（V1.64 起）**：`UserManager.DevUsername = "dev"`（种子密码 dev123，
  归属 Administrator 角色，走管理员登录框进入，界面无提示=隐藏入口）。
  规则：dev 可删改业务管理员；普通管理员只管操作员/技术员；dev 名注册/改名
  （大小写变体同样）一律"该账号名不可用"；dev 自身禁改名/禁删除；
  老 Users.json 自愈补 dev（不碰已存密码）；手改文件保留"dev + 第一个业务管理员"。
  登录下拉/记住登录永不出现 dev；用户管理窗"管理员"角色项只给 dev 加；
  关于下拉的深浅切换项只给 dev 看。Users.json 含 dev 哈希（PBKDF2，非明文）但仍属
  运行时数据，gitignore 绝不入库（已有红线，dev 不例外）。
- **深色/浅色主题约定（V1.60 起，V1.64 改入口）**：主题状态只认 `Services/ThemeManager`（App.config 存 `AppTheme`=Light/Dark，大小写兼容、写错兜底浅色）。新增窗体/弹窗必须在打开前调一次 `ThemeManager.ApplyTo(form)`（打开点与窗体构造解耦，动态内容在 Show 时已建完才刷得全）；切换入口只有"关于"下拉内的深浅项（`MenuThemeToggle_Click`+`ApplyToAllOpenForms`），**仅 dev 登录可见**。**按钮颜色一律不动**（全是业务语义色）；自绘控件自己管换肤（如 `WorkstationGridView.SetDarkMode`），禁止在 ThemeManager 里硬改自绘颜色；运行时状态色（红/绿/蓝）靠"双向映射表查不到就保留"自动豁免，不要另写白名单。**例外（用户指定的深灰底白字）**：主窗体"停止运行/报警复位/下料判定"（V1.60.1，`MainForm.GetOperationButtonThemeColors`，V1.71 走 Sunny Gray 档 + `ApplyButtonColors`）、版本说明"确定"——浅色无语义默认灰的按钮深色才动；布局预览画布深色走纯黑（V1.60.4，`HomeLayoutEditorForm.GetPreviewBackColor`，色块自带底所以安全）；系统 `MessageBox` 跟不了主题，要换肤必须换自定义窗（V1.60.4 版本说明先例）。
- **界面文件头注释必须带 ASCII 布局图**：所有 View/Dialog（`Views/*.cs`、`Dialogs/*.cs`）的类 XML 注释里都要有一段用 `┌─┐│└┘` 画出的界面布局图（参考 `RecipeManagerForm.cs` / `WorkstationGridView.cs` 头部注释），框内标注控件名与关键交互点。AI 无法看图，改界面全靠这段文本图，故**每次新增/修改界面文件都补画或同步更新该图**，且要和实际控件布局一致（坐标、控件名、按钮文字都对上）。
- **自绘控件（WorkstationGridView 等）的坐标类常量一律外部化**：不写死像素常量，放到布局配置模型（如 `Models/PanelLayoutConfig.cs`，可被 `PanelLayout.json` 覆盖），并把坐标标注进头部注释的 ASCII 图里，便于现场改配置微调间距/颜色/字号。
- **自绘控件坐标一律用锚定，禁止"孤岛绝对坐标"（V1.58.13~1.58.17 沉淀）**：面板内元素通过锚定字段声明与"面板边缘"或"其他元素"的相对关系，加载时统一解析，改面板尺寸/基准元素时自动联动，不用手改一串坐标。字段：矩形 `RightMargin/TopMargin/RightAlignTo/VerticalAlignTo/LeftAlignTo/RightToLeftAlignTo`（双端锚定=LeftAlignTo+RightToLeftAlignTo 自动定宽）、标签 `LeftMargin/TopMargin/Width/RightToLeftAlignTo/LeftAlignTo`。**三步解析顺序铁律**（`ResolveAnchors`）：①面板边缘锚定 → ②元素间锚定（设置按钮→右对齐组→压力框→下电）→ ③标签锚定，顺序错会取到目标旧值致错位。**全表/依赖链/调整指南/坑（标签 Width 依赖字体、字段互斥、json 与代码默认一致）都在 `PanelLayoutConfig.cs` 类头注释**，改坐标前必读；改完同步 `bin/Debug/PanelLayout.json` 与 WorkstationGridView 头部 ASCII 图。
- **高 DPI 适配约定（V1.55 起）**：
  - 标准控件窗体用 `AutoScaleMode.Font`，WinForms 自动缩放，前提是 `app.manifest` 声明 `PerMonitorV2` **且** `App.config` 配 `Switch.System.Windows.Forms.DpiAwareness=PerMonitorV2`（两个缺一不可）。
  - **纯代码窗体（无 Designer）的高 DPI 三要素**（V1.58.4 实测血泪）：
    1. 必须显式设 `AutoScaleDimensions = new SizeF(6F, 12F)`，只设 `AutoScaleMode.Font` 会以 96DPI 为基准不缩放；
    2. **必须用 `SuspendLayout()` 包裹全部控件创建、在末尾 `ResumeLayout(false)`**——若未挂起布局时逐次 `Controls.Add`，WinForms 会在每次 Add 触发 PerformAutoScale 时把 AutoScaleDimensions 固化成当前 DPI 值（144 DPI 下变 9×18），导致"设计基准==运行基准"、缩放因子恒为 1、窗体永不放大。Designer 窗体天生带 SuspendLayout 所以正常，纯代码必须手动补齐；
    3. 验证时注意：纯代码 harness 需配 `app.manifest(PerMonitorV2)` + `.exe.config(AppContextSwitchOverrides)` 才能真正走 PerMonitorV2 缩放路径，只调 `SetProcessDPIAware()` 是 system-aware、AutoScaleDimensions 会被覆盖、测不出缩放。
  - 自绘控件（AutoScaleMode.None）坐标是 96DPI 逻辑像素，必须**内部手动乘 `_dpiScale = CreateGraphics().DpiX / 96`** 做 DPI 缩放（字体保持 pt 自动放大）；**禁用 `Graphics.ScaleTransform`**（TextRenderer 走 GDI 不认坐标变换，V1.51 踩坑）。
  - 获取实际 DPI 用 `CreateGraphics().DpiX`，**不要用 `Control.DeviceDpi`**（PerMonitorV2 下句柄刚创建时返回 96，实测不可靠）。
  - 新增自绘控件/改自绘坐标时，记得同步缩放命中检测（鼠标坐标是物理像素）、tooltip、局部重绘矩形，漏一处点击/重绘就错位。
- **区域宽度按比例自适应，禁止写死像素（V1.65 用户原则）**：主界面各区域宽度（如右侧状态按钮区）一律用"占父容器百分比 + 上下限钳制"（见 `MainForm.RightPanelRatio/RightPanelMinWidth/RightPanelMaxWidth` 与纯函数 `ComputeRightPanelWidth`），窗口 `Resize` 时重算；写死像素在设计屏上正好、换台工控机就溢出/留白。用户手动保存的配置文件（`HomeLayout.json`）是绝对值、优先级高于比例；计算逻辑抽纯函数并锁回归用例。
- **动态控件重建必须先 Dispose 再 Clear（V1.72.12 血泪）**：`Controls.Clear()` 只摘父子关系，
  孤儿 Sunny 控件（UITextBox/UIComboBox，内部包原生 TextBox）进终结器线程 Dispose，
  内部读 Handle 即跨线程崩溃（堆栈终点 `ResetAutoComplete←Dispose←Finalize`，Name 全空、
  时机随机是三特征）。动态重建处一律先逐个 `Dispose()` 再 `Clear()`（主窗 H5 先例），
  用例锁"重建后旧控件 IsDisposed"。
  **非模态弹窗同罪（V1.72.13）**：`Form.Close()` 不释放非模态窗体，设置窗 IP/IO/规则
  三 popup 的 FormClosed 只回写不释放就是第二案发现场——handler 里
  `finally { popup.Dispose(); }`，用例走生产挂接（反射 ShowXxxPopup→OpenForms 找窗→
  Close→IsDisposed），裸 Show/Close 是恒绿假绿。
  **改 UI 代码后必跑终结器审计**：`scripts/audit_finalizer_risk.ps1`
  （R1 Clear/R2 非模态 Show/R3 Remove/R4 动态创建，HIGH 拦提交）；
  新增非模态弹窗在 `$SafeShowKeys` 登记（方法|文件|配对），R2 验行为不认空登记。
- **自绘性能大坑（V1.57.3 血泪教训）**：**禁止用"离屏 Bitmap 整幅预渲染 + OnPaint DrawImage 拷贝"来优化自绘控件**。实测离屏大图（2040×2025）上 `TextRenderer.DrawText` 每处约 **2.2ms**（屏幕 DC 上近 0ms），全量渲染 72 面板一次高达 2247ms，而 `UpdateAll` 每秒全量刷新 → 整个软件每 1 秒卡死。且 `g.Clear(白色)` 会把面板间隙刷白导致"面板连成一片"。**正确做法**：OnPaint 只重绘可见区面板（`e.ClipRectangle` 算行列范围），数据/选中变化仅 `Invalidate`；滚动卡顿用"16ms 定时器节流 AutoScrollPosition + 画刷/画笔缓存字段"解决，不要预渲染。判断优化效果务必用**真实屏幕 DC**（`CreateGraphics`）测，离屏 Graphics 的 TextRenderer 慢是 GDI+ 固有行为、不代表真实帧速。

## 关键文件导航

| 文件 | 作用 |
| --- | --- |
| `AgingTestSystem/Views/MainForm.cs` | 主窗体、启动装配、配置加载 |
| `AgingTestSystem/Models/DeviceConfig.cs` | 设备配置模型 |
| `AgingTestSystem/Services/ModbusRtuBarometerReader.cs` | 气压表 Modbus RTU 读取 |
| `AgingTestSystem/Services/ScannerService.cs` | 扫码枪识别/读取 |
| `AgingTestSystem/Services/UserManager.cs` | 用户/权限（Users.json） |
| `AgingTestSystem/Services/PasswordHasher.cs` | 密码哈希（PBKDF2-SHA256，Users.json 落盘前转换；改密码/登录/迁移入口全在 UserManager） |
| `AgingTestSystem/Services/DeviceManager.cs` | 业务编排核心（采集/报警联动/**三阶段老化状态机 V1.59**：Vacuuming 抽真空→Aging 计时→Completed 待取料） |
| `AgingTestSystem/Services/AgingSequencer.cs` | 老化时序纯函数决策器（ShouldPowerOn/ShouldComplete/IsVacuumBuildFailed）；**改编排时序逻辑先改这里并同步用例**，保持 DeviceManager 只做执行 |
| `AgingTestSystem/Models/PolicyEnums.cs` | 工艺策略枚举（V1.67；0 值=现状行为，只能追加） |
| `AgingTestSystem/Services/ProjectProfile.cs` | 项目档案（Projects/&lt;项目&gt;/ 路径解析/迁移/切换；跟项目 vs 跟机器见上） |
| `AgingTestSystem/Services/ProjectPolicyStore.cs` | 项目策略存储（Policy.json 分流/叠加；PolicyKeys 唯一名单；EnumOptions 下拉） |
| `AgingTestSystem/Dialogs/UnloadJudgeForm.cs` | 下料判定窗（V1.67；Q22 待判定配套，纯代码窗体） |
| `AgingTestSystem/Dialogs/ProjectSwitchForm.cs` | 项目切换窗（V1.67；仅管理员，纯代码窗体） |
| `AgingTestSystem/Services/MesMapping.cs` | MES 映射解析纯函数（V1.68；触发器/字段映射/静态字段/自定义头/分地址 vocabulary 唯一出处） |
| `AgingTestSystem/Services/MesReporter.cs` | MES 上报器（V1.68；后台 POST+重试+离线缓存；Transport 测试缝） |
| `AgingTestSystem/Services/MesCrypto.cs` | MES 密钥 DPAPI 加解密（V1.68；前缀+内存明文/文件密文） |
| `AgingTestSystem/Services/RuleExpr.cs` | 规则表达式引擎（V1.69；沙盒解析求值，变量冻结 12 个） |
| `AgingTestSystem/Services/RuleEngine.cs` | 规则执行器（V1.69；编译缓存+持续计时+完成表达式 OR） |
| `AgingTestSystem/Controls/RuleListEditorPopup.cs` | 规则表编辑弹窗（V1.69；多行文本+实时校验） |
| `AgingTestSystem/Views/FlowCockpitForm.cs` | 流程驾驶舱（V1.70；固定拓扑画布+点节点改配置+缩放平移拖拽） |
| `AgingTestSystem/Views/FlowGraph.cs` | 流程图静态数据（V1.70；拓扑/文本/布局存取，纯静态可单测） |
| `AgingTestSystem/Services/TestSessionStore.cs` | 在测任务快照持久化（TestSession.json，断电恢复用，gitignore） |
| `AgingTestSystem/Services/RecipeAutoCompleteProvider.cs` | 配方名称自动检索 |
| `AgingTestSystem/Services/ThemeManager.cs` | 深色/浅色主题服务（V1.60；AppTheme 配置 + 双向映射表着色；新窗体打开前 ApplyTo） |
| `AgingTestSystem/Dialogs/SettingsForm.cs` | 系统设置（配置项编辑、校验、保存） |
| `AgingTestSystem/Controls/DataGridViewNumericUpDownCell.cs` | 数字/下拉单元格控件 |
| `.opencode/skills/agingtest-regression/` | 项目最终测试验证技能：冒烟 + 全量回归用例（tests/TestRunner.cs 为用例源码，新用例一律沉淀于此） |
| `CHANGELOG.md` | 版本改动记录（最新在前，V1.xx 小节） |

## 构建与验证命令

```powershell
# 构建（若提示找不到 MSBuild，先定位：Get-ChildItem 'C:\Program Files*\Microsoft Visual Studio' -Recurse -Filter MSBuild.exe | Select -First 1）
& "D:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe" AgingTestSystem/AgingTestSystem.csproj /p:Configuration=Debug /p:Platform=AnyCPU /t:Build /nologo /v:m
```

- 构建成功标准：输出 `AgingTestSystem -> ...\bin\Debug\AgingTestSystem.exe` 且无 error。
- **新增 .cs 文件必须手工在 csproj 登记**（老式项目无通配，漏登记报 CS0246）：
  在 `<Compile Include="...">` 段按目录加一行（纯代码窗体加 `<SubType>Form</SubType>` 即可，
  无需 Designer/resx）。V1.67 实锤：5 个新文件漏登记编译全红。
- **最终测试验证手段（V1.58.23 起）**：一键跑 `powershell -ExecutionPolicy Bypass -File .opencode\skills\agingtest-regression\scripts\build_and_test.ps1`，自动完成"构建 → 真机冒烟（exe 启动存活）→ 全量回归用例（1158+ 断言，覆盖 PasswordHasher/UserManager/配置归一化/IO 映射/配方存储/双日志器/面板布局锚定联动/工艺策略/项目档案/MES映射上报/规则表达式/流程驾驶舱/编排扩展场景等核心逻辑类）"。也可单独跑同目录 `smoke_test.ps1`（只冒烟）/ `run_unit_tests.ps1`（只回归）。退出码 0 = 全绿。
- **界面像素级 bug（竖线/横线/颜色/叠色/裁剪/滚动条）**：调用全局技能 `winforms-ui-debug`——编译独立 harness 直接 new 目标窗体（指哪打哪，绕过登录/主流程），用反射探私有字段 + PrintWindow 截图 + 像素扫描定位根因并验证修复。含可复用的 csc 编译命令、坐标映射、色值字典与踩坑清单。
- **调试完自动沉淀技能**：每次用 `winforms-ui-debug` 排查成功（尤其是"一次性改对"的高光案例）后，**主动把可复用的新套路/新踩坑/新型探针代码回写到全局技能 `winforms-ui-debug` 的 SKILL.md**（新增/补充小节、追加踩坑条目），不用等用户提醒。价值标准：换个人靠这份 skill 能更快解决同类问题。
- 改构建输出（csproj 路径/bin 目录/主 exe 名）时，同步改全局技能 `winforms-ui-debug` 附录 A 的 AgingTestSystem 行（防开工查表拿到旧值）。

## 回归测试与用例沉淀铁律（V1.58.23 起，勿等用户提醒）

**V1.59 补充（业务编排可测性约定）**：老化时序的"判定类"逻辑（该不该上电/该不该完成/真空建立是否超时）
一律写成 `AgingSequencer` 纯函数并同步边界用例；DeviceManager 只做"调用决策 + IO + 日志"。
**采集缓存跨周期标记必须主动延续**（Completed/LastTestResult 每轮会被新数据覆盖，CollectData
else 分支已做叠加——改采集/状态显示时勿破坏此机制）。断电恢复策略=整台重测（评审结论），快照参数定格于启动时刻。

项目专属测试验证技能为 `.opencode/skills/agingtest-regression/`（SKILL.md 含用法、覆盖范围表、加用例步骤与踩坑清单；用例源码在 `tests/TestRunner.cs`，脚本在 `scripts/`）。以下三条为强制约定：

1. **改完代码必须验证（V1.72.4 分级回归）**：日常小改跑
   `build_and_test.ps1 -Affected`（按 git 改动自动算模块子集，只测影响面；
   交互模块已含在映射里，如改 CSV 格式会连带全部 DeviceManager*）；
   大重构/发布前/改骨架（csproj/Interfaces/用例自身/scripts）跑全量
   `build_and_test.ps1`（默认）；映射不到的新文件自动兜底全量。
   **新增产品 .cs 文件必须在 `get_affected_modules.ps1` 的 `$Map` 登记**，
   否则每次改它都付全量代价。
2. **修 bug 必补用例**：每修复一个 bug，先在 `TestRunner.cs` 对应模块加一条能复现该 bug 的 `Check` 用例（红→修产品代码→绿），防止回归；新增功能同理补正向+边界用例。
3. **新用例/新冒烟必须回流 skill**：凡是本次工作中新写的测试用例、冒烟步骤、验证脚本，一律直接写进 `agingtest-regression` 的 tests/scripts 目录并在 SKILL.md 补记覆盖点；**禁止散落在临时目录或只留在对话里**。新踩的坑追加进 SKILL.md 踩坑清单。全部完成后重跑全绿才算收尾。

## 文档同步（每次任务完成必做，逐条核对，**不等用户提醒**）

- **交付前主动全量排查，禁止"只改用户点名的那份文档"**：改完代码后必须自己过一遍下面全部文档，
  逐份判断是否需要同步；并用 `rg` 全仓扫描被删/被改的功能名、控件名、按钮文字等关键词，
  确认无残留引用（合法语境除外，如 CHANGELOG 历史条目按惯例不回改、确认框流程文案）：
  - `CHANGELOG.md`（新增/更新当前版本小节）
  - `README.md`（目录结构、业务流、操作入口、构建方式）
  - `docs/通讯接入.md`（寄存器/协议/串口参数/IO 映射/业务流程时序/测试入口）
  - `docs/现场业务预研Plan.md`（软件能力现状、已实现清单）
  - 代码头部注释与 ASCII 布局图（界面文件）、方法 XML 注释
- **`CHANGELOG.md`**：功能/修复完成后必须在顶部新增或更新当前版本小节，写明"改动范围、为什么这么改、优化点"三部分（参考既有 V1.xx 小节格式）。改动再小也要记，防止现场追溯不到。
- **`README.md`**：若改动了目录结构、新增/删除文件、核心业务流、构建方式，同步更新对应章节（如"目录结构表"、`WorkstationGridView` 等条目），保持与实际代码一致。
- **`docs/通讯接入.md`**：寄存器/寄存器地址/Modbus 协议/串口参数/IO 映射等通讯类改动，必须同步到该文档，并写明对应版本号。
- **`AGENTS.md` 自身**：若本次工作中发现了新的约定、红线、套路（如"界面注释要画 ASCII 图"、"坐标要外部化到配置"），立刻沉淀进本文件，让下次任务自动遵守。
- **代码注释**：改动处的代码注释要详细到小白能看懂（做什么 + 为什么 + 怎么改），样式参考 `WorkstationGridView.cs` / `RecipeManagerForm.cs`；新文件/新方法尤其要写清头部说明。
- 注释里的中文请保持 UTF-8，写完后自查编码：`[IO.File]::ReadAllText(path, UTF8).Contains("预期中文")` 能命中。
- **提交前自检**：`git status` + `git diff` 确认改动范围与文档同步都完成后再交付；用户不要求 commit 时只留工作区改动即可。
