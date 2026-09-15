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
5. **代码注释精简但把话说全（V1.89 用户原则）**：关键方法/流程/边界条件/配置依赖写清"做什么 + 为什么这么写 + 怎么改"，让小白能看懂学会；**禁止写版本流水账**（`【V1.xx …】`标签、"原来…现在…"、"血泪/实锤"叙事一律不进代码，历史包袱只留 `CHANGELOG.md`）。允许的一句话出处（如"Sunny 标签缺省 AutoSize=false"）保留，杜绝只写变量名的废话注释（如 `i++ // 自增`）。参考 `WorkstationGridView.cs` / `RecipeManagerForm.cs` 头部与关键方法。

## 代码约定

- 类、方法、属性用 PascalCase；私有字段 `_camelCase`；接口前缀 `I`；常量全大写或 PascalCase 跟随现有风格。
- 界面/控件命名：`btn`/`txt`/`nud`/`cmb`/`grid`/`pnl` 等匈牙利前缀（跟随 Designer 风格）。
- 枚举与配置值的存储约定（**改动串口/配置相关必须先读此处**）：
  - `StopBits` 存字符串 `1` / `15`（=1.5）/ `2`；校验位 `Parity` 存标准枚举名 `None`/`Odd`/`Even`/`Mark`/`Space`。读写两端大小写兼容（ModbusRtu 用 `Enum.TryParse(…, true)`，ScannerService 用 `ToLowerInvariant()` 匹配）。
  - 备用通道号只认 `0x00`~`0x0F`（单个寄存器 16 个 bit；V1.62 血泪：文档曾写 0x1F，0x10+ 在执行侧静默失效）。解析层直接拒绝 0x10+ 并进 error；编辑弹窗微调框最大值同步 0x0F；执行侧 `MapOutputChannel` 对非法目标保持原通道（绝不写坏掩码）。
  - **可视化端口映射三层分工（V1.81）**：`IoRemapValidator`（新建拦截唯一口：源唯一/目标独占/自环/越界 + 唯一序列化口 `Serialize`，与 `ParseAll` 互逆）+ `IoRemapCatalog`（点位池唯一口：源=全部输出/目标=预留或全空闲，点位来自 `IoMapBuilder` 不手写）+ `IoRemapGraphControl`/`IoRemapVisualForm`（只管画与选，不管落盘；实时/草稿两种提交由调用方定）。新增映射入口（右键/表格/连线）只调这三层，不各写一份规则；目标独占只拦新建，老配置多源同目标照常加载执行。
  - **自绘画布三不（V1.81.2~3 血泪）**：①不用 `DoubleBuffered`（逼全部 `TextRenderer` 走离屏慢路径，每处 2.2ms，见上性能红线）——直画屏幕 DC + 禁默认擦除 + 按裁剪自填底；②`TextRenderer` 下不挂任何变换矩阵（V1.51 栽过 Scale，位移也不赌），滚动偏移手工加到矩形上；③徽标/标题/截断全放布局态预计算，`Paint` 里不量字不扫表不拼串。滚动过的无缓冲画布 `PrintWindow` 会丢 GDI 文字（框在字无），视觉证据走置顶真屏截图。
  - **连线裁剪按包围盒判交（V1.81.4 血泪）**：贝塞尔中段穿屏、两端屏外的长线，"两端点判交"会整条裁掉，滚屏后新露出的条带只有白底没有线（平时有线、一滚就断）；改控制点包围盒判交（曲线必在盒内，无漏网），箭头仍只在目标可见时画；细连线开抗锯齿（文字/边框不动）。**通用铁律（下次一步到位）**：裁剪判交的对象是图元的实际覆盖范围，不是定义点——以后画布加任何新图元（线/区/徽标），布局时就把它的包围盒/可见性规则一起定掉；修裁剪类 bug 必须配"两端出屏+中段穿屏"场景用例，并对旧条件做反向验证（精准 FAIL 才算找到病，38号坑）。
  - **自适应画布改 Size 必须锁死滚动范围（V1.88.15 血泪）**：内容 `Size` 跳变后
  指望布局引擎重算 `DisplayRectangle`/滚动 `Maximum` 会卡在旧值（拖 Splitter 改宽后
  纵向滑块卡 80% 拉不到底，2 秒不收敛是稳定脏态不是时序慢，harness 五轮实锤）。
  三不许：①不许在 `Resize` 里指望同步布局必跑（正撞上布局挂起、被静默忽略，
  手动调一次立刻全好就是证据）；②不许在 `Layout` 事件里压 `HorizontalScroll.Visible`
  （布局引擎在事件之后还会覆盖，注定失败）；③不许只改 `Size` 不管滚动位置
  （旧值超新范围即卡死）。正确三件套（只动自家容器）：设完 `Size` 同步设
  `AutoScrollMinSize=Size`（值语义驱动范围，不赌子控件布局时序）＋按旧滚动比例恢复位置并钳制＋
  `BeginInvoke` 在布局彻底完成后跑校正（再 `PerformLayout`＋压横向条＋钳位置，
  释放检查防重建串扰）。验证走 harness"滚到底→加宽→减窄→再到底"
 （到底值==理论最大＋横向条无＋2 秒稳定；回归无句柄跑不了 AutoScroll，不进回归，harness 即证据）。
   - **72 站自适应只剩双向铺满一屏（V1.88.24 删回单路；V1.88.22 双模式已删）**：
  `ComputeFitZoom` 是唯一自适应纯函数（`zoomX=可用宽/内容宽、zoomY=可用高/内容高` 独立，
  画布精确等于显示区、无滚动条；面板允许宽扁拉伸，字取窄边不变形）。
  绘制/命中按 `ScaledX`/`ScaledY` 分流（禁单 `Scaled(int)`，宽扁拉伸下点选必错位，
  编译器会把漏改的 int 调用全揪出来）；字体取窄边、下限 4pt（V1.88.21：1280×1024小屏跟随缩小不挤叠，见 WorkstationGridView.MinFontSize）、
  正文一律加粗（V1.88.25：4~5pt 常规体发虚，加粗 A/B 黑像素 +34% 才落改）；
  显示区再小也照算（`MinZoom` 钳制与滚动条兜底同步删除，72 站永远一屏）。
  外层容器 `AutoScroll=false`（V1.88.15 的 MinSize/比例恢复/BeginInvoke 校正三件套已作废，
  `UpdateCanvasSize` 只设 `Size`；以后谁再加滚动条，先读 V1.88.24 删除清单再动手）。
  面板尺寸是缩放比的杠杆：值框/按钮能省则省（148→130、60×50→50×42；V1.90 再压 170→128：
  标题并入第一行＋状态块 20→18＋值框 18→16＋按钮 42→36＋正文 9→10pt，纵向缩放 0.526→0.703；
  V1.91 标签列 56→65 装四字长标签、值列右移 65→74、按钮 50→46 保"设置"完整显示；
  V1.92 下电左缘对齐值框列（双端锚定宽 49）、选中框 16→14、时间值单独 9pt（72 框最紧，"00:00:00" 10pt 会截断），
  省出的每像素都换成字号；V1.95 反向加高 128→140（行内太挤：上链间隙各 +2、交接缝 3→9，
  zoomY 0.75→0.69 仍高于 zoomX，字号不动、状态栏不动——纵向余量探针说了算，不是感觉）；
  值框能缩则缩（SN 序列号最长保持最宽一档，压力/配方/时间串短，
  缩了给标签列让位）；SN 之外的长文本一律走省略号，不撑布局。
  选中框边长取缩放后较小边（恒正方形，`GetSelectBoxRect`/`GetSelectBoxLocalRect` 绘制命中同源）。
  显示区最小保护两处：`MainForm.ClampRightPanelWidthForWorkstation` 保 Panel1≥640
  （右侧 600 在小屏上压回来）＋`SplitterDistance` try/catch。
  工位面板布局是纯代码缺省（V1.91 起删掉 `PanelLayout.json` 文件自定义：项目未上线，
  改布局直接改 `PanelLayoutConfig` 缺省值重编译，不留"文件覆盖代码"的第二套口径；
  也不写任何迁移分支，改干净）。
  主页布局同样纯代码固定（V1.92 起删掉 `HomeLayout.json`＋可视化编辑器＋ about 菜单入口＋设置表行：
  顶栏/状态栏 30 常量、分栏按窗口比例、分隔条 `IsSplitterFixed` 锁死不可拖）。
  - **自绘命中按内容 bounds 判交，禁止"整除即命中"（V1.88.15 血泪）**：行列整除会把面板
  之间的缝隙算进上一格，点缝隙误翻选上一个面板（触摸屏 fat-finger 更易中招）。
  `TryHitPanel` 类命中函数返回前必须验 local 落在内容矩形内，缝隙一律不命中（悬停同步消失）；
  回归锁"行/列缝不命中＋底边内仍命中"。**通用铁律**：凡整除分格的命中，先问"缝隙归谁"。
  - **Dock 布局 Fill 必须最后布局（V1.82 血泪）**：WinForms 按 z 序从后往前布局 Dock，
  后面的先占边（Top/Bottom），`Fill` 必须在 z 序最前、最后布局，吃剩余区；
  `Controls.Add` 插到最前（新加的最靠前），所以正常顺序 Add 下来 Fill 天然最后。
  禁在分开建面板的方法里各调一次 `BringToFront`——后建的面板会把 Fill 挤到后面
  先布局，Fill 一把梭占满整个客户区，边栏只是浮盖在上面：滚到两端内容滑进面板
  底下，首尾永远看不全（连线页实锤：画布 685 高、本该 405）。
  往 Dock 窗加面板：边栏正常 Add，完了统一把 Fill `BringToFront()` 一次并写清注释。
  - **列表画布缩放平移抄 `IoRemapGraphControl`（V1.82）**：缩放并进布局基准
  （s = DPI × zoom，行列/命中全是 s 的函数，不碰变换矩阵）+ 字体按 zoom 重建
  （FlowCanvas 口径，下限 6pt）+ 宿主窗 `IMessageFilter` 预过滤滚轮
  （`OnShown` 注册/`OnFormClosed` 摘除，悬停即缩）；中键拖动走 `AutoScroll`，
  双击复位自己按 `DoubleClickTime/DoubleClickSize` 在 `MouseDown` 里判定
  （Panel 双击风格对中键不可靠）；`AutoScrollMinSize` 给完整虚宽（给"超出量"横向出不来）。
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
- **运行时文件跟项目还是跟机器（V1.67）**：配方/工位设置/策略跟项目
  （`ProjectProfile.ResolveDataPath(file, true)`，`Projects/<项目>/`）；用户/快照/
  日志/连接参数跟机器（程序目录；主页布局/工位面板布局是纯代码固定值，不跟文件）。改存储路径必须 rg 全仓扫字面文件名，
  老用例的字面路径会批量红（V1.67 实锤）；`Projects/` 住运行目录（bin/ 下，天然 gitignore）。
- **项目切换热更免重启（V1.72.10）**：`SwitchTo` 只写指针，换装走
  `MainForm.ReloadActiveProject` 八步（在测复查→暂停主采集→LoadConfig 重读→
  `CopyFrom` 就地换血→`StationSettingsCache.Reload`→`ClearProjectScopedState`→
  `LoadRecipes`→`ApplyHomeLayout`+顶栏+刷帧，失败提示重启）。
  铁律：`_config` 是 readonly 引用一律 `CopyFrom`（换引用服务侧不生效）；
  静态内存（工位缓存）切项目必须 `Reload`；清状态禁调 `StopAll`
 （那是写 IO 全 OFF 的急停语义）；历史项目目录的改名/删除收进
  `EnsureActiveProfile` 启动自愈，列表里永远干净。
- **设置表 tooltip 全覆盖（V1.67 用户原则，V1.73 固化全仓）**：配置项说明悬停可见，超 40 字按
  `WrapTooltip` 换行（断点优先标点，不断英文单词）；新增配置项的 `_descriptions`
  写清"现状是什么/改了会怎样"，别只写名字。
  **换行只调 `SettingsForm.WrapTooltip`（唯一入口，不许手写截断）**；
  新增带说明的控件（录入窗设置项、按钮动作说明、状态解释）一律挂 `ToolTip`，
  标签+输入框两边都挂（下拉框悬停是选中项全文时不动它，说明看标题行即可，
  V1.88 策略窗先例）；容器管理的 (`new ToolTip(components)`) 随窗体自动释放，
  无容器的手写 `Dispose`（项目切换窗先例）；动态文案（可用/禁用两套话）随状态同步换。
  跨窗体复用说明只走 `SettingsForm.GetDescription`（与设置表同源，禁各写一份文案，
  V1.88：`_descriptions` 已转 static 即为此）。
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
  - 去系统标题栏套路（V1.89 主窗先例，其它项目复用照抄）：基类保持 UIForm，只加
    `ShowTitle=false`（Dock 内容不再被顶 35px、Y&lt;35 不再搬家，harness 实锤），Padding 顶清零，
    `Resizable=false` 维持；三按钮（最小化/最大化/关闭）用原生 Button 进顶栏最右，
    GDI 线条自绘字形（禁 emoji/Unicode 符号，老工控机字体回退显示方块），悬停底自管
    （ThemeManager 跳过按钮类，换肤后手动 Invalidate）；`WndProc` 先调 base 再改写
    `WM_NCHITTEST`（Normal 下边缘 6px 回缩放码、顶栏非按钮区回 `HTCAPTION`，
    按钮经类型判定放行保可点），`WM_NCLBUTTONDBLCLK` 必须拦在 base 之前切完返回
    （DefWindowProc 也会切一次，两次抵消，harness 实锤）；任务栏标题带版本号走
    `BuildWatermark.ReleaseLabel`（禁 Designer 写死旧版本）。验证走真窗 Show＋PrintWindow＋
    NCHITTEST 探针（边缘/顶栏/按钮各一）＋三按钮点击真实路径，缺一不可。
  - UIGroupBox 内容首控件 Y≥34：Sunny 组框标题约占顶部 30px，首按钮 Y=18 会上半
    压进标题区（V1.72 用户目检：主窗操作组整列下移 16px 解决，间距/分组不动）。
  - Sunny 控件默认 Style=Inherited，吃样式字体：名/值两套标签要么都不写 Font
    （同源永不分叉），要么两边写死同一套——只写一边必大小眼（V1.72 实锤：
    lblFanState 单写 9Bold，与继承 12 的名标签对不上，删字解决）。
    只加粗不动字号时走代码（构造里按控件当前字号或 Bold，V1.78 先例），不进 Designer
   （Designer 写死字号即与继承字号分叉）。
  - `is Button/TextBox/ComboBox` 认不出 Sunny 自绘控件（UIButton/UITextBox/
    UIComboBox 不是原生子类；UILabel 是 Label 子类无碍）：类型判断改 Sunny 类型，
    ThemeManager 按类型名走分支；按钮改色走 `ApplyButtonColors`，读显示色走
    `GetEffectiveButtonColors`（FillColor 才是显示色）。
  - UITextBox 有 AppendText/Clear/Lines/PasswordChar/Multiline（无 ScrollBars/
    WordWrap，用 ShowScrollBar/WordWarp）；UIComboBox 有 Items/SelectedIndex/
    SelectedValue/DropDownWidth，DropDownStyle 换 `UIDropDownStyle` 枚举；
    UIButton 实现 IButtonControl（AcceptButton/CancelButton 照用）；删 FormBorderStyle
    行（UIForm 自己管边框）；字体不动（动 AutoScaleDimensions 是 DPI 红线）。
  - Sunny UILabel 缺省 `AutoSize=false`（原生 Label 缺省 true，反直觉！V1.88.24 实锤：
    项目名 Fill 标签关着 AutoSize 只报 0 宽，AutoSize 列被压成前缀宽、名字常年看不见）。
    AutoSize 列的填充子必须显式开 AutoSize；排查先 dump 各控件 `AutoSize` 属性值。
    前缀类 Dock=Left 定宽标签保持关闭（只取首选高度+垂直居中，不参与列宽）。
    垂直居中一律 Panel＋Dock 全高＋MiddleLeft，禁 FlowLayoutPanel＋Padding.Top 硬垫
    （V1.88.27 血泪：硬垫用 RowStyle 高算，实际容器扣掉表头边距小 6px，多垫 2px 反偏下，
    且 Flow 子 TopLeft 与 MiddleLeft 混用；顶栏项目/权限/通讯三段同构 Panel，Bounds 中心恒等容器中心）。
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
- **工艺策略窗约定（V1.70 建图，V1.73 由"流程驾驶舱"改名）**：拓扑画死（物理锁死，通用连线编辑器会让客户删掉安全联锁，
  参考 HJVision mFormFlowEdit 后否决）；节点只挂真实 key（ key 必须全是 DeviceConfig
  真属性，回归锁），连线只读；保存走 `SettingsForm.PersistChanges`（与设置表同一条路，
  落盘语义只有一份）；节点位置纯视图存 `PolicyLayout.json`（跟机器，缩放/平移不存）；
  缩放以鼠标为中心（IMessageFilter 预过滤滚轮），中键平移注意 AutoScrollPosition
  存取符号坑（setter 取正值）；双锁分开取防死锁（`GetPhaseCounts` 先状态后缓存）。
  MES上报是无连线纯配置节点（上报正交于流程，画连线误导）；无阀本机藏破空点位行
  （`VentValveEnabled=false` 时，开关本身照常显示）。
- **配方加字段三窗同步（V1.66）**：`RecipeConfig` 加字段 → 三个录入窗
  （`RecipeManagerForm`/`BatchRecipeForm`/`StationSettingsForm`：输入框 + 保存 + 回填 +
  头部 ASCII 图）→ `SetStationRecipe` 下发 → `StationInfo`（+`Clone`）→
  `ApplyStationInfo` 叠加 → `BarometerData`（+`Clone`）。漏一处就是"存了用不上/下了传不到"，
  对照此链逐项打勾；`StationSettingsCache` 只在窗口需要回填时才加。
  **配方名只许下拉选（V1.88.13）**：`BatchRecipeForm.cmbRecipeName` / `StationSettingsForm.cmbRecipe`
  是 `UIComboBox + DropDownList`（禁手输），选项=配方库名；选中回填参数走
  `CmbRecipeName_SelectedIndexChanged`/`CmbRecipe_SelectedIndexChanged`（工位窗带
  `_fillingRecipeCombo` 守卫，程序回填不触发）。新建/改名走配方管理窗；工位窗脏数据
  （库中已无）追加显示 + 保存时 `CommitConfig` 拦停，选回正常值时 `RemoveDirtyRecipeItems`
  顺手清脏（脏项只为让脏值看得见）。**禁再引回输入框或自动检索 Provider**
  （手输错名会静默回退全局配置=串配方）。
  配方名比较口径（V1.88.13 复查补救血泪）：`Trim + 忽略大小写`，**两边都要 Trim**
  （批量窗 handler/工位窗 `FindRecipe` 与 `FindDuplicateIndex` 三处对齐；老配方文件
  手改可能留前后空格，单边 Trim 会"看得见选不中"，还会被当脏项清掉）。
  工位窗构造顺序（V1.75 血泪，V1.88.13 复查捞出）：`_displayModeShown` 先定死
  （含隐藏收缩）再 `LoadStationData`，否则开态下开窗显示模式回填被守卫吞掉、
  点保存还会连带清空（缺省关态恒空，回归不红，靠开态用例锁）。
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
- **深色/浅色主题约定（V1.60 起，V1.64 改入口）**：主题状态只认 `Services/ThemeManager`（App.config 存 `AppTheme`=Light/Dark，大小写兼容、写错兜底浅色）。新增窗体/弹窗必须在打开前调一次 `ThemeManager.ApplyTo(form)`（打开点与窗体构造解耦，动态内容在 Show 时已建完才刷得全）；切换入口只有"关于"下拉内的深浅项（`MenuThemeToggle_Click`+`ApplyToAllOpenForms`），**仅 dev 登录可见**。**按钮颜色一律不动**（全是业务语义色）；自绘控件自己管换肤（如 `WorkstationGridView.SetDarkMode`），禁止在 ThemeManager 里硬改自绘颜色；运行时状态色（红/绿/蓝）靠"双向映射表查不到就保留"自动豁免，不要另写白名单。**例外（用户指定的深灰底白字）**：主窗体"停止运行/报警复位/下料判定"（V1.60.1，`MainForm.GetOperationButtonThemeColors`，V1.71 走 Sunny Gray 档 + `ApplyButtonColors`）、版本说明"确定"——浅色无语义默认灰的按钮深色才动；系统 `MessageBox` 跟不了主题，要换肤必须换自定义窗（V1.60.4 版本说明先例）。
- **界面文件头注释必须带 ASCII 布局图**：所有 View/Dialog（`Views/*.cs`、`Dialogs/*.cs`）的类 XML 注释里都要有一段用 `┌─┐│└┘` 画出的界面布局图（参考 `RecipeManagerForm.cs` / `WorkstationGridView.cs` 头部注释），框内标注控件名与关键交互点。AI 无法看图，改界面全靠这段文本图，故**每次新增/修改界面文件都补画或同步更新该图**，且要和实际控件布局一致（坐标、控件名、按钮文字都对上）。
- **自绘控件（WorkstationGridView 等）的坐标类常量一律外部化**：不写死像素常量，放到布局配置模型（如 `Models/PanelLayoutConfig.cs` 纯代码缺省，V1.91 起不走文件），并把坐标标注进头部注释的 ASCII 图里，便于改缺省值微调间距/颜色/字号。
- **自绘控件坐标一律用锚定，禁止"孤岛绝对坐标"（V1.58.13~1.58.17 沉淀）**：面板内元素通过锚定字段声明与"面板边缘"或"其他元素"的相对关系，加载时统一解析，改面板尺寸/基准元素时自动联动，不用手改一串坐标。字段：矩形 `RightMargin/TopMargin/RightAlignTo/VerticalAlignTo/LeftAlignTo/RightToLeftAlignTo`（双端锚定=LeftAlignTo+RightToLeftAlignTo 自动定宽）、标签 `LeftMargin/TopMargin/Width/RightToLeftAlignTo/LeftAlignTo`。**三步解析顺序铁律**（`ResolveAnchors`）：①面板边缘锚定 → ②元素间锚定（下链设置按钮→配方；上链选中框→真空→SN→下电→压力→电流→SN 终解→延时）→ ③标签锚定，顺序错会取到目标旧值致错位。**全表/依赖链/调整指南/坑（标签 Width 依赖字体、字段互斥）都在 `PanelLayoutConfig.cs` 类头注释**，改坐标前必读；改完同步 WorkstationGridView 头部 ASCII 图与回归用例。
- **高 DPI 适配约定（V1.55 起）**：
  - 标准控件窗体用 `AutoScaleMode.Font`，WinForms 自动缩放，前提是 `app.manifest` 声明 `PerMonitorV2` **且** `App.config` 配 `Switch.System.Windows.Forms.DpiAwareness=PerMonitorV2`（两个缺一不可）。
  - **纯代码窗体（无 Designer）的高 DPI 三要素**（V1.58.4 实测血泪）：
    1. 必须显式设 `AutoScaleDimensions = new SizeF(6F, 12F)`，只设 `AutoScaleMode.Font` 会以 96DPI 为基准不缩放；
    2. **必须用 `SuspendLayout()` 包裹全部控件创建、在末尾 `ResumeLayout(false)`**——若未挂起布局时逐次 `Controls.Add`，WinForms 会在每次 Add 触发 PerformAutoScale 时把 AutoScaleDimensions 固化成当前 DPI 值（144 DPI 下变 9×18），导致"设计基准==运行基准"、缩放因子恒为 1、窗体永不放大。Designer 窗体天生带 SuspendLayout 所以正常，纯代码必须手动补齐；
    3. 验证时注意：纯代码 harness 需配 `app.manifest(PerMonitorV2)` + `.exe.config(AppContextSwitchOverrides)` 才能真正走 PerMonitorV2 缩放路径，只调 `SetProcessDPIAware()` 是 system-aware、AutoScaleDimensions 会被覆盖、测不出缩放。
  - 自绘控件（AutoScaleMode.None）坐标是 96DPI 逻辑像素，必须**内部手动乘 `_dpiScale = CreateGraphics().DpiX / 96`** 做 DPI 缩放（字体保持 pt 自动放大）；**禁用 `Graphics.ScaleTransform`**（TextRenderer 走 GDI 不认坐标变换，V1.51 踩坑）。
  - 获取实际 DPI 用 `CreateGraphics().DpiX`，**不要用 `Control.DeviceDpi`**（PerMonitorV2 下句柄刚创建时返回 96，实测不可靠）。
  - 新增自绘控件/改自绘坐标时，记得同步缩放命中检测（鼠标坐标是物理像素）、tooltip、局部重绘矩形，漏一处点击/重绘就错位。
- **区域宽度按比例自适应，禁止写死像素（V1.65 用户原则）**：主界面各区域宽度（如右侧状态按钮区）一律用"占父容器百分比 + 上下限钳制"（见 `MainForm.RightPanelRatio/RightPanelMinWidth/RightPanelMaxWidth` 与纯函数 `ComputeRightPanelWidth`），窗口 `Resize` 时重算；写死像素在设计屏上正好、换台工控机就溢出/留白。分栏分隔条 `IsSplitterFixed` 锁死（V1.92 用户点名：鼠标拖不动，比例永远按窗口走）；计算逻辑抽纯函数并锁回归用例。
  **例外：顶栏高度锁死（V1.88.28 用户点名）**：顶栏 30px 是与 9pt 字/18px 按钮互相咬合的一套，
  可调只会调出坏结果——`MainForm.HeaderHeight` 是唯一值，主窗直接取常量（项目未上线，不写迁移）。
- **下拉选项尺寸按文本实测、字体与主按钮同源（V1.88.28）**：`ShowDropdownPopup` 的选项格
  `ComputePopupItemSize`（主按钮尺寸只当下限＋文本 `MeasureText`＋纵/横内边距），`Font = hostButton.Font`
  不另起字号——原生 Button chrome 比 Sunny 厚，等尺寸硬套 18px 行装 9pt 字即上下顶格（6 倍放大实锤）。
  改顶栏字号只改 `ApplyHeaderFonts` 的一处常量，弹窗自动跟。
- **全仓绿统一 ForestGreen（V1.88.29 用户点名）**：绿底白字的按钮/标签（工位设置/上电/真空开/选中✓、
  主窗批量按钮、四个窗的保存类绿按钮）一律 `ForestGreen`（亮绿＋白字对比度仅 2:1，小字 wash 到看不清，
  深绿约 4.6:1，仍是绿色语义）。Designer 只能写字面值（`Color.ForestGreen`，禁成员表达式是 R8b 红线），
  配置写 `"34,139,34"`（同一色，改一边必须对另一边）；缺显式白字的绿按钮钉死 `ForeColor=White`。
  改颜色/字号缺省后重编译即生效（布局无文件覆盖，不用删 bin 文件；项目未上线，不写迁移分支）。
- **动态控件重建必须先 Dispose 再 Clear（V1.72.12 血泪）**：`Controls.Clear()` 只摘父子关系，
  孤儿 Sunny 控件（UITextBox/UIComboBox，内部包原生 TextBox）进终结器线程 Dispose，
  内部读 Handle 即跨线程崩溃（堆栈终点 `ResetAutoComplete←Dispose←Finalize`，Name 全空、
  时机随机是三特征）。动态重建处一律先逐个 `Dispose()` 再 `Clear()`（主窗 H5 先例），
  用例锁"重建后旧控件 IsDisposed"。
  **快照后释放（V1.72.16 血泪：V1.72.12 只修对一半）**：`foreach` 直接枚举 `Controls`
  逐个 `Dispose()` 是错的——`Dispose()` 会把自己从父集合摘除，枚举器下标错位跳过一个，
  被跳过的 `Clear()` 后变孤儿，终结器线程照炸（驾驶舱拖业务框实锤：炸在拖时、漏在切节点时）。
  一律走 `Services/ControlDisposeHelper.DisposeAllAndClear`（`CopyTo` 快照数组后释放，
  最后 `Clear`），禁手写 `foreach`；R1 只认 helper（白名单已删，靠行为检查）。
  **非模态弹窗同罪（V1.72.13）**：`Form.Close()` 不释放非模态窗体，设置窗 IP/IO/规则
  三 popup 的 FormClosed 只回写不释放就是第二案发现场——handler 里
  `finally { popup.Dispose(); }`，用例走生产挂接（反射 ShowXxxPopup→OpenForms 找窗→
  Close→IsDisposed），裸 Show/Close 是恒绿假绿。
  **失焦自杀弹窗开模态子窗（V1.81.1 血泪）**："失焦即关"的无边框 popup（`OnDeactivate→Close`）
  若用 `ShowDialog(this)` 开模态子窗，子窗激活瞬间父弹窗失焦自杀，Windows 连带销毁 owned
  的子窗——现象是"点了进不去"，无异常无日志。开子窗前后 `try/finally` 置 `_visualOpen` 守卫，
  `OnDeactivate` 首行 `if (_closing || _visualOpen) return;`；用例反射直调 `OnDeactivate`
  （设旗/不设旗各一次），不用真 Show 真弹窗。
  **关窗竞态同罪（V1.72.14）**：窗体释放路径对（FormClosed→Dispose）仍会炸——后台拍在
  关窗前后脚 `Invoke/BeginInvoke` 进已销毁句柄，或排队回调关后执行直碰已释放 label/txt。
  "关 A 开 B 必炸"是关 A 尾巴被开 B 的 GC 赶出来。对策：非模态测试窗一律 `_closed`
  首行置位 + `IsDisposed/Disposing/IsHandleCreated` 三查 + 日志 `BeginInvoke`（禁同步 Invoke）
  + `SetConnected/UpdateStatus` 入口自拦；提示窗自带 `FormClosed→Dispose`（reuse 白名单只保
  "主窗在时不进终结"，先×提示窗的窗口期仍漏）；用例锁"关后三件套静默丢弃"。
  **关窗竞态全仓四模式（V1.72.15 血泪，新增后台/定时/订阅一律照此四条自查）**：
  ①后台 `Task` 投递（公共参数批量写/测试窗遍历/重连）：投递点与完成入口双查 `_closed`，
  关后硬件写停手（在途整拍丢弃，不残留半拍）；②有延迟的 UI `Timer`（设置窗长按 700ms/
  补全 200/100ms 延迟隐藏）：字段定时器必须手停手放（`OnFormClosed/Dispose` 里 Stop+Dispose，
  不在 components 容器不会自动停），延迟 Tick 入口查释放，待触发的一次性 Timer 集中登记释放时排空；
  ③经 `Post` 排队的事件（扫码枪 `_syncContext.Post`）：退订拦不住已排队回调，handler 入口必须
  `_closed` 自拦；④活得比窗久的服务事件源（`_deviceManager`/`_scanner` 的 `On*`）：
  `OnFormClosed/FormClosing` 先置位再退订（退订包 try），handler 入口自拦双保险。
  主窗（活到退出）用 `_mainClosing` 同款（`FormClosing` 首行置位，`WriteLog` 改 UI 丢弃文件照写）。
  **审计 R5/R6/R7 就是这四条的机器版**：R5 禁 UI 文件同步 `Control.Invoke(`、
  R6 锁 Timer 字段同文件 `Dispose()`、R7 锁长生命周期 `On*` 事件同文件 `-=`，HIGH 拦提交。
  **审计 R8 是 Designer 可序列化锁（V1.72.16）**：R8a 禁 Designer 里 `AddRange(裸标识符)`
  （只许 `new` 数组；静态字段引用 CodeDom 认不出，判定窗 `Dispositions` 实锤加载失败，
  改构造代码填）、R8b 禁 `= xxx.Range.Min/Max` 成员表达式（存盘整行删，量程写字面值，
  改常量同步改 Designer，主页布局 340 越界实锤）、R8c 禁 `ZoomScaleRect`+
  `AutoScaleMode.Font` 混搭（改 `None` 并删 `AutoScaleDimensions`，终值本来就是 `None`）。
  **Designer 三条军规（V1.72.16，两次被 VS 重写后沉淀）**：
  ①`InitializeComponent` 方法体内禁写任何注释（VS 重写整段再生全删，说明写文件头/.cs）；
  ②手写 Designer 与 VS 口径不一致（坐标/字体/模式）时，以 VS 重写版为 canonical 基线接受，
  不手改回去，否则每次预览都脏；③`.resx` 别手删（VS 预览建的空模板也留着入库，
  删了下次重建 + csproj 加条目更脏）。
  **纯代码窗设计器可预览（V1.72.14）**：只有带参构造的窗 VS 预览报"没有无参数构造函数"——
  补公有无参构造（空快照占位，执行键加 null-manager 守卫）；静态文本禁 `var` 局部，
  一律具名字段（设计器序列化认字段，局部下次存盘即丢；Name 全空也是终结器案发的辨认特征）。
  **叠放控件层级三锁（V1.86 血泪，授权窗眼睛按钮）**：`Controls.Add` 是追加沉底
  （后加的 `GetChildIndex` 更大、层级更靠后——"后 Add 压前面"是错觉），叠在框里的按钮
  必须第一个 Add 抢最前，否则无框无字、鼠标点不到（截图里凭空消失，`BringToFront` 才现形；
  `PerformClick` 照调得通，值断言全绿也发现不了——只读属性不断言像素，叠放必真窗截图）。
  回归锁三件：几何框内 + `GetChildIndex` 差值（小值在上）+ 真窗截图；改 Add 顺序后必重截。
  更稳的替代（授权窗最终方案）：**把覆盖件做成宿主的子控件**（EyeIcon 挂进输入框
  `Controls`，子控件天然浮在父之上，压根不进窗体 z 序战场，无需抢 Add，随宿主缩放）。
  **自绘图标控件用 GDI+ 画线，不用 emoji/Unicode 符号（V1.86 授权窗眼睛）**：
  emoji 在老工控机/精简字体下字体回退显示方块（HJVision V4.4.3 同坑）；眼睛图标
  用"椭圆轮廓 + 实心瞳孔（明文）/ 空心瞳孔+斜线（掩码）"两个态翻绘制，任何环境都稳。
  **Sunny UITextBox 运行时切 PasswordChar 不重绘（V1.86 同案）**：内外值都在、框空白，
  `Invalidate(true)+Update` 也刷不出来——文本重推一次（先清空破相等守卫）逼 WM_SETTEXT；
  复制/导出读外层缓存不受影响。新增"切显示态"的 Sunny 输入框一律走"改值+重推+截图"三件套。
  **未 Show 窗体上 PerformClick 是空操作（V1.86 同案）**：`CanSelect=false` 不触发事件，
  回归"不 Show 不弹框"惯例不能破——直接反射调私有 handler（与点击同路）。
  **改 UI 代码后必跑终结器审计**：`scripts/audit_finalizer_risk.ps1`
  （R1 快照释放/R2 非模态 Show/R3 Remove/R4 动态创建/R5 同步 Invoke/R6 Timer 释放/
  R7 长事件退订/R8 Designer 可序列化，HIGH 拦提交）；
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
| `AgingTestSystem/Views/ProcessPolicyForm.cs` | 工艺策略窗（V1.70 建图；固定拓扑画布+点节点改配置+缩放平移拖拽） |
| `AgingTestSystem/Views/PolicyGraph.cs` | 工艺策略图静态数据（V1.70；拓扑/文本/布局存取，纯静态可单测） |
| `AgingTestSystem/Services/TestSessionStore.cs` | 在测任务快照持久化（TestSession.json，断电恢复用，gitignore） |
| `AgingTestSystem/Services/ThemeManager.cs` | 深色/浅色主题服务（V1.60；AppTheme 配置 + 双向映射表着色；新窗体打开前 ApplyTo） |
| `AgingTestSystem/Dialogs/SettingsForm.cs` | 系统设置（配置项编辑、校验、保存） |
| `AgingTestSystem/Services/SoftwareActivation.cs` + `Dialogs/SoftActivation.cs` | 软件授权（V1.87；与 HJVision 同源同口径，细化约定见下方"软件授权铁律"） |
| `AgingTestSystem/Controls/DataGridViewNumericUpDownCell.cs` | 数字/下拉单元格控件 |
| `.opencode/skills/agingtest-regression/` | 项目最终测试验证技能：冒烟 + 全量回归用例（tests/TestRunner.cs 为用例源码，新用例一律沉淀于此） |
| `CHANGELOG.md` | 版本改动记录（最新在前，V1.xx 小节） |

## 软件授权铁律（V1.87 重写：与 HJVision 同源，同一套《获取激活码》工具通用；涉及授权的改动必读）

- **公式逐字节照抄，改一字工具就对不上**：`Encrypt` = MD5 取前 15 字节 hex（30 字符，
  出处 HJVision `MainForm.Encrypt`）；设备ID = WMI 第一块 CPU 的 ProcessorId；
  设备ID码 = `Encrypt(ID+"A")` / 设备码 = `Encrypt(ID+"1")` / 30天码 =
  `Encrypt(设备码+"30")` / 永久码 = `Encrypt(设备码+"ALL")` /
  `RunHash2` = `Encrypt(ID+i)`（i=0..839，<768 有效≈30天）/
  永久 = `Encrypt(ID+"ALL")`。输入全是 ASCII，`Encoding.Default` 在各系统下结果一致。
- **设备码恒 "1" 是照抄不是 bug**：HJVision 源码写 `Encrypt(ID+currentTime.Day)`，
  但 `currentTime` 从初始提交就没赋值过（恒 0001-01-01），现场设备码恒定；
  本项目直接写 `"1"`，行为一字不差。**不许"顺手修成当天"**——改了两边设备码分叉，
  厂商按 HJVision 经验报的码就对不上了。
- **无密钥、无试用、无启动闸**：新机无 ini 即"新设备"，每小时提醒一次；
  不阻断启动、不拦生产（失败只置灰用户权限按钮 = HJVision 置灰口令按钮，
  计时器内不自动恢复；V1.88.1 起激活成功关窗即重查解灰，不用重启）。
  不要加试用/宽限/项目/点数/到期——加了工具发不出，
  破坏"同一套工具"的统一。
- **存储与 Timer**：程序目录 `MainSetting.ini [RunHash] RunHash1/RunHash2`
  （与 HJVision 同名同结构，kernel32 INI API 读写，gitignore 绝不入库，
  出厂厂商按设备ID手写两键）；主窗 `hashTimer` 1 小时一格（挂 components 自动释放，
  `MainForm_Load` 启动，`HashTimer_Tick` 与 HJVision 同分支：先设备→永久跳过→
  命中有效格写下一格→否则过期）；缺文件自动建空模板（`EnsureIniTemplate` 只建不覆盖，
  两键留空+注释填法，空值=新设备语义）；判定逻辑只进 `SoftwareActivation` 纯函数
  （`VerifyActivationCode`/`FindSlot`/`ComputeStatus`），Timer 与激活窗共用。
- **回归锁**：`SoftActivation` 模块（RFC1321 标准向量 pin 算法 + 公式关系式 +
  激活比对 + 计数格 + ini 隔离往返 + 窗构造 + V1.88.1 付费即恢复判定/成功位）；ini 测试走显式 path，不碰真实文件；
  激活窗只构造不 Show（错码调 handler 不断言弹窗）。

## 全局健壮性铁律（V1.84 大扫荡沉淀，涉及落盘/路径/解析/账号/自绘一律先读）

- **运行时 json 一律 `AtomicFile.WriteAllText`（临时文件+改名），禁止裸 `File.WriteAllText`**：
  写半截断电=下次启动 Load 吞异常回空（配方被清空/策略全回缺省按错误工艺跑）。已统一：快照/配方/
  策略/工位缓存/用户/记住密码。新增持久化类文件先想原子写。
  （V1.87：激活 ini 走 kernel32 INI API，与 HJVision 同文件，不进 AtomicFile。）
- **运行时文件路径一律 `BaseDirectory` 绝对路径**（除非确为相对=产品 cwd 恒定）：快照/MES 缓存以前
  裸文件名跟 CWD 走，快捷方式起始位置一变写散、重启找不到=整批任务静默丢失。测试隔离走 `BaseDirOverride`
  静态测试缝（finally 复位），不要靠切 cwd 自欺。
- **规则表达式解析三防护**：名称/表达式/持续秒用 `|` 分隔但**表达式内允许 `||`**（`ParseRuleList`
  枚举切分点取首个全合法组合）；保存/校验走**严格变量模式**（`TryParse(…, strictVars:true)`，未知变量
  当场拦，别让死规则过了保存运行时恒 false）；解析加**长度/嵌套上限**（防 5000 层括号 StackOverflow
  不可捕获直接杀进程）。
- **路径穿越防御只认一个名单**：`ProjectProfile.IsValidProfileName` 是 Create/Delete/Switch/ActiveProfile
  唯一口径；Delete/Switch 同样先验名（不能只拦 Create）。新建项目原子化 + 写哨兵，ListProfiles 过滤野目录。
- **热更/数组/跨度三坑**：按 `TotalBarometers` 定长数组，热更改工位数必须走
  `DeviceManager.RebuildStationArrays()`（数组+`RuleEngine.Resize`+采集间隔一起）；IO 写失败一律
  **先写后清**（清了状态写失败=软硬分叉，UI 弹框明示）；时钟类作差（重连节流/计时）负跨度钳零，
  防时钟拨慢饿死。
- **状态清单一律十倍自检**：新加按工位的逐台状态数组，检查"启动定格/停止/复位/急停/完成/报警/热更重建"
  七处清理点是否都清了（V1.84 清 `_sessionSkipVacuum` 就是自查捞出来的）；删死数组前全仓扫 6 处读写。
- **账号/列表对外只吐副本**：`UserManager.CurrentUser`/`GetAccounts`/`LoginResult.User` 一律 Clone，
  防拿引用强转改密码绕过哈希/校验；内部写操作按值定位内部对象（`FindInternal`）。记住密码用
  MesCrypto DPAPI（禁 Base64）。
- **后台即时写失败必须留痕**：写阈值/启动/停止/急停/报警下发失败，除 UI 弹框明示外记一条事件
  （CSV/LOG），追溯链不能无声断裂。
- **自绘控件销毁放 GDI**：WorkstationGridView 的 6 个缓存画笔/画刷在 `Designer.Dispose` 释放
  （光靠 RebuildThemeBrushes 覆盖只堵了换主题，堵不住控件销毁泄漏）。

## 复查补齐铁律（V1.84.1，大扫荡提交后逐文件复查沉淀）

- **缓存必须带"脏读指纹"**：路径键缓存只比路径=手改文件读脏。指纹=路径+长度+写时间
  （`ProjectPolicyStore.StampCache/CacheStillFresh`），Save 后刷新；手改即生效是老行为，缓存不能丢。
- **原子写用 `File.Replace` 不用"删+搬"**：删搬窗口读方靠重试兜，崩溃恰在窗内仍读空；
  同目录=同卷无跨卷坑；崩溃残留 tmp 顺手清（`AtomicFile` 唯一口）。
- **递归防护数"真递归"**：平坦路过不消耗预算（规则解析器：括号分支+一元符分支才计数，
  ParseOr 本体不计）；只盖一半的防护不如不写——复查要问"另一条递归路呢"。
- **守卫收敛唯一入口**：5 处 popup 回写守卫收成 `TryWritePopupCell`（守卫不过直接 return，
  连 `LayoutSections` 都别碰）；散写 5 份=改 1 漏 4 的温床。
- **纯函数约定无例外**：判定逻辑（破空阀碰撞）直接写 UI 层=单测够不着，必须搬进
  `AgingSequencer` 纯函数 + 用例（V1.67 约定，复查也要执行）。
- **急停记账与抛异常分开**：风机停失败→快照 Clear + 急停事件照做，然后原堆栈重抛
  （`ExceptionDispatchInfo`，`throw ex` 抹堆栈）；"没停掉"看得见，"已停"不留尾巴。
- **热更清状态带上静态信息**：`RebuildStationArrays` 清数组/缓存时 `_stationInfo` 同步清
  （持 `_stationInfoLock`，不与 `_stateLock` 嵌套；加锁前全仓核查反向持锁顺序）。
- **用例假绿三查**：①反射探针先验"探针本身有效"（`Disposed` 是事件不是属性、`nativeBrush`
  在基类、`??true` 恒绿）；②存在性断言改前后差值（旧运行残留行即绿）；③新修逻辑无用例=
  没修（DeviceManager 行为 tests 进 `DeviceManagerSweep` 模块，Fake 注入+短数组+反射）。
- **文案方向错也是 bug**："多于 64 列"写成"≤64列"、注释称"含容差"实际严格——复查要把
  注释/报错文案当代码读；缺省值两处手抄即分叉，收敛唯一工厂（`DefaultRcCurrentValue`）。

## 部署诊断与混淆约定（V1.88.9，客户工控机调试期沉淀）

- **调试期轻保护、稳定后再混淆**：调试期只做 Release + 不发 pdb + 软件激活绑机器，
  保证远程可排错；稳定（一两周无改动）后按 `tools/obfuscation/obfuscar.xml` 打混淆包。
  该 xml 不接入构建（手动跑），发版归档缺一不可：mapping + 混淆后 exe MD5 + 对应 pdb +
  `BuildWatermark.ReleaseLabel`（客户发回堆栈靠"水印版本→这套归档"反解）。
- **日志正文不怕混淆，怕的是字符串反射**：AppLog/TestLog/Crash 记的是中文文案 +
  `ex.Message`，重命名不影响可读；但 `GetProperty/GetMethod/GetField` 传字面量的地方
  （DeviceConfig 全属性 / `SetDarkMode` / Json 模型属性）
  改名即静默失效。新增模型类或字面量反射必须同步补 `obfuscar.xml` 的 Skip 行
  （grep `GetProperty(|GetMethod(|GetField(` 全仓扫一遍）。
- **发版改 `BuildWatermark.ReleaseLabel`**（与 CHANGELOG 顶部小节同值）：主窗构造首行水印
  + 崩溃正文嵌同一份；`IsObfuscatedBuild` 只在打混淆包时翻 true（回归里有恒 false 锁，
  翻时同步改用例）。崩溃处理路径（`Program.cs` 两口 + `CrashLogWriter`）一律绝不抛异常，
  落盘失败只在弹框里如实写"写入失败请截图"。
- **发版只走一键脚本**（`.opencode/skills/agingtest-regression/scripts/obfuscated_release.ps1`，
  构建→混淆→组包→三项验收→还原标记；产物 `release/<版本>-obf/` gitignore 永不入库）：
  Obfuscar 的 Skip 三元素必须 `type="类型全名" name="成员名"` 分开写，
  `name="类型.成员"` 会被静默忽略（V1.88.10 验收 A12 实锤）；新增 ps1 必须带 UTF-8 BOM
  （PS5.1 无 BOM 解析中文直接 ParserError）；脚本里 XML 属性读写走显式 DOM
  （`$_.value=` 适配器写法 `-File` 下抛错、交互式却正常）。

## 构建与验证命令

```powershell
# 构建（若提示找不到 MSBuild，先定位：Get-ChildItem 'C:\Program Files*\Microsoft Visual Studio' -Recurse -Filter MSBuild.exe | Select -First 1）
& "D:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe" AgingTestSystem/AgingTestSystem.csproj /p:Configuration=Debug /p:Platform=AnyCPU /t:Build /nologo /v:m
```

- 构建成功标准：输出 `AgingTestSystem -> ...\bin\Debug\AgingTestSystem.exe` 且无 error。
- **新增 .cs 文件必须手工在 csproj 登记**（老式项目无通配，漏登记报 CS0246）：
  在 `<Compile Include="...">` 段按目录加一行（纯代码窗体加 `<SubType>Form</SubType>` 即可，
  无需 Designer/resx）。V1.67 实锤：5 个新文件漏登记编译全红。
- **最终测试验证手段（V1.58.23 起）**：一键跑 `powershell -ExecutionPolicy Bypass -File .opencode\skills\agingtest-regression\scripts\build_and_test.ps1`，自动完成"构建 → 真机冒烟（exe 启动存活）→ 全量回归用例（1722 断言，覆盖 PasswordHasher/UserManager/配置归一化/IO 映射/配方存储/双日志器/面板布局锚定联动/工艺策略/项目档案/MES映射上报/规则表达式/流程驾驶舱/编排扩展场景/软件激活等核心逻辑类）"。也可单独跑同目录 `smoke_test.ps1`（只冒烟）/ `run_unit_tests.ps1`（只回归）。退出码 0 = 全绿。
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
- **代码注释**：改动处的代码注释精简但把话说全（做什么 + 为什么 + 怎么改，小白能看懂；
  禁版本流水账，见铁律 5），样式参考 `WorkstationGridView.cs` / `RecipeManagerForm.cs`；新文件/新方法尤其要写清头部说明。
- 注释里的中文请保持 UTF-8，写完后自查编码：`[IO.File]::ReadAllText(path, UTF8).Contains("预期中文")` 能命中。
- **提交前自检**：`git status` + `git diff` 确认改动范围与文档同步都完成后再交付；用户不要求 commit 时只留工作区改动即可。
