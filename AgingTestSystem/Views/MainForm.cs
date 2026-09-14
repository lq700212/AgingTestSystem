using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using AgingTestSystem.Dialogs;
using AgingTestSystem.Models;
using AgingTestSystem.Services;

// 【说明】
// 本文件新增了"用户权限管理"功能：
// - 引入 UserManager（用户管理服务）
// - 引入 UserRole（用户角色枚举）
// - 引入 LoginForm 和 UserManagementForm（对话框）
// 实现：
// - 点击"用户权限"按钮 → 显示下拉菜单（操作员/技术员/管理员）
// - 选择菜单项 → 弹出 LoginForm 输入用户名密码
// - 登录成功 → 切换权限，更新顶部标签
// - 登录失败 → 弹出错误提示窗口
// - 管理员权限下，下拉菜单额外显示"用户管理"选项
// - 参数设置按钮需要技术员或管理员权限才能操作

namespace AgingTestSystem.Views
{
    /// <summary>
    /// 主窗体（主视图）—— 业务逻辑部分
    /// 整个软件的界面框架
    ///
    /// 【说明】
    /// 本文件只包含业务逻辑（设备管理、数据更新、事件处理）。
    /// 界面控件的创建和布局代码在 <see cref="MainForm.Designer.cs"/> 文件中，
    /// 由 Visual Studio 设计器自动维护，请勿手动修改 Designer.cs 中的控件布局。
    ///
    /// 窗体布局说明：
    /// ┌─────────────────────────────────────────────────────────┐
    /// │ 当前项目:烧屏测试 │ 当前操作权限: 操作员 │ 通讯模块状态: 已连接/未连接 │
    /// │ (前缀常规+名加粗)  │ (前缀常规+角色名加粗) │ (标签常规+状态值加粗，=IO耦合器，V1.16.1) │
    /// ├─────────────────────────────────────────────────────────┤
        /// │ [用户权限] [参数设置] [日志记录] [关于] │（V1.64：深色按钮从关于右侧收进关于下拉，仅 dev 可见）
    /// ├──────────────────────────────┬──────────────────────────┤
    /// │                              │ 运行状态                 │
    /// │                              │ ┌────────────────────┐   │
    /// │   工位显示区域               │ │ 空闲/测试中(D4204)  │   │
    /// │   (动态加载72个工位面板)      │ └────────────────────┘   │
    /// │                              │                         │
    /// │   自绘大画布                 │ 监视                    │
    /// │   WorkstationGridView       │ 设置温度: [D4700]       │
    /// │   (9列 × 8行布局)           │ 上部温度: [D4702]       │
    /// │   (V1.50 单窗口滚动容器)    │ 下部温度: [D4704]       │
    /// │                              │                         │
    /// │                              │ 操作（V1.59.1 精简版，V1.67 加下料判定）  │
    /// │                              │ [批量设置配方]          │
    /// │                              │ [录入批号]             │
    /// │                              │ [启动运行（选中台）]    │
    /// │                              │ [停止运行（选中台）]    │
    /// │                              │ [报警复位（选中台）]    │
    /// │                              │ [下料判定（选中台）]    │
    /// │                              │ [全部停止（急停）]      │
    /// ├──────────────────────────────┴──────────────────────────┤
    /// │ 状态栏：设备数量: 72 | 采集间隔: 1s | 当前时间          │
    /// └─────────────────────────────────────────────────────────┘
    /// </summary>
    /// <remarks>
    /// 【修复 H10】设计器报错"未能加载基类 System.Windows.Forms.Form"
    ///
    /// 【问题原因】
    /// 与 WorkstationPanelView.cs 同样的问题：.cs 文件包含中文字符但没有 UTF-8 BOM，
    /// VS 设计器的 CodeDom 解析器无法正确识别文件编码，
    /// 导致中文注释乱码，进而无法正确解析类声明和 using 语句，
    /// 设计器找不到 System.Windows.Forms.Form 基类，报"未能加载基类"错误。
    ///
    /// 【修复方法】
    /// 1. 将基类声明从 `: Form` 改为 `: System.Windows.Forms.Form`
    ///    使用完整命名空间路径，避免设计器因 using 语句解析失败而找不到基类
    /// 2. 将所有 .cs 文件保存为 UTF-8 with BOM 编码（见编码转换脚本）
    /// </remarks>
    public partial class MainForm : Sunny.UI.UIForm
    {
        /// <summary>
        /// 【V1.58.1】右侧状态按钮区宽度的兜底绝对值（写死在本窗体，不放在 HomeLayoutConfig）。
        ///
        /// 【V1.65】右侧宽度改由 <see cref="RightPanelRatio"/> 按比例自适应（默认跟随窗口，
        /// 只有现场在"主页区域调整"编辑器里保存过 HomeLayout.json 才用文件里的绝对值）。
        /// 本常量退为两处兜底：①窗体构造极早期分隔容器宽还不可读时；②编辑器"恢复默认"
        /// 按钮的右侧值。平时不要直接拿它当右侧宽度，用 <see cref="ComputeRightPanelWidth"/>
        /// 算出来的才是当前生效值。
        /// </summary>
        public const int DefaultRightPanelWidth = 240;

        /// <summary>
        /// 【V1.65】右侧状态按钮区宽度占分隔容器（splitContainerMain）总宽的比例。
        ///
        /// 为什么用比例而不用写死像素（用户要求，血泪原则）：
        /// - 写死 300px 在 1400 宽设计屏上正好（占 23.4%），到 1366 宽工控机上右侧纹丝不动，
        ///   全靠左侧网格区压缩，换一台设备就得改一次代码；
        /// - 0.234 就是现状比例（326 ÷ 1394），按此比例换算：1366 屏右侧≈318、1080p 屏≈448→
        ///   被 <see cref="RightPanelMaxWidth"/> 钳到 340。正常屏上看起来和以前一模一样，
        ///   只是分母从"写死的设计宽"变成了"窗口实际宽"，换设备自动等比。
        /// </summary>
        public const double RightPanelRatio = 0.234;

        /// <summary>【V1.65】比例算出的右侧宽度下限：再窄操作按钮文字（"启动运行（选中台）"）放不下。与编辑器钳制下限对齐。</summary>
        public const int RightPanelMinWidth = 180;

        /// <summary>【V1.65】比例算出的右侧宽度上限：大屏上按 23.4% 会算出 400+，右侧用不了那么多，省给左侧网格。</summary>
        public const int RightPanelMaxWidth = 340;

        /// <summary>
        /// 设备配置
        /// 从 App.config 加载，包含设备数量、通信参数等
        /// </summary>
        private readonly DeviceConfig _config;

        /// <summary>
        /// 设备管理器
        /// 负责管理所有气压表和IO设备的连接、数据采集
        /// </summary>
        private readonly DeviceManager _deviceManager;

        /// <summary>
        /// 主窗正在关闭标记（V1.72.15 新增，全仓关窗竞态排查：退出瞬间后台采集/扫码/重连线程的
        /// BeginInvoke 排队回调会在句柄销毁后执行。FormClosing 首行置位，RunOnUi 与各事件入口双查，
        /// 退出期回调静默丢弃；volatile 保证采集线程立即可见）。
        /// </summary>
        private volatile bool _mainClosing;

        /// <summary>
        /// 【新增】用户管理器
        /// 负责用户登录验证、用户名密码修改等功能
        /// 默认账号：
        /// - 管理员: admin / 123456
        /// - 技术员: technician / 123456
        /// - 操作员: operator / 123456
        /// - 最高权限: dev / dev123（V1.64：走"用户权限→管理员"登录框进入，界面无提示，隐藏入口）
        /// </summary>
        private readonly UserManager _userManager = new UserManager();

        /// <summary>
        /// 【V1.16 新增】扫码枪服务（真实扫码枪接入）
        /// 参考 SerialScannerTest Demo 实现：WMI 自动识别串口 + 串口读码。
        /// 作用：
        /// - 扫码结果写入 LOG 日志
        /// - ID绑定窗体（IdBindingForm）打开时，扫码结果自动填充 SN 输入框
        /// 说明：扫码枪是可选设备，App.config 里 ScannerEnabled=false 时不连接。
        /// </summary>
        private ScannerService _scanner;

        /// <summary>
        /// 工位网格（自绘大画布，【V1.50】）。
        /// 整个工位区域 = 1 个自绘 UserControl（含全部面板 + 行全选按钮列），
        /// 由 <see cref="CreateWorkstationPanels"/> 创建并放入 AutoScroll 滚动容器。
        /// </summary>
        private WorkstationGridView _gridView;

        /// <summary>
        /// 当前操作权限（中文显示名：操作员/技术员/管理员）
        /// 初始为"操作员"（未登录状态），登录成功后更新为对应角色名
        /// </summary>
        private string _currentPermission = "操作员";

        /// <summary>
        /// 通讯模块状态（V1.16 更名：现场无 PLC，改为通讯连接状态；V1.80 再更名：用户点名）
        /// true=已连接，false=未连接
        /// 语义：气压表主链路是否连通（耦合器/送风机断开时单独在 LOG 诊断，不影响本状态）
        /// </summary>
        private bool _commConnected = false;

        /// <summary>
        /// 运行状态文本
        /// 用于显示在右侧"运行状态"分组中
        /// </summary>
        private string _runStatus = "空闲";

        /// <summary>
        /// 配方列表（内存中维护，启动时从 Recipes.json 加载，保存设置时写回）
        /// 通过"配方管理"窗体维护
        /// </summary>
        private readonly List<RecipeConfig> _recipes = new List<RecipeConfig>();

        // 注意：原 ContextMenuStrip 字段已删除
        // 改用 ShowDropdownPopup 方法在按钮点击时动态创建无边框弹出窗体
        // 这样下拉菜单项的尺寸可以和主按钮完全一致

        /// <summary>日志文本框的最大字符数，超过时自动裁剪旧内容（修复 M8）</summary>
        private const int MaxLogTextLength = 100_000;
        /// <summary>日志裁剪后保留的字符数（保留最近一半内容）</summary>
        private const int LogTrimKeepLength = MaxLogTextLength / 2;

        /// <summary>
        /// 初始化主窗体
        /// </summary>
        public MainForm()
        {
            // 1. 先初始化界面控件（Designer.cs 中的 InitializeComponent）
            //    必须最先调用，否则其他代码访问控件会报空引用
            InitializeComponent();

            // 【V1.78】顶栏/状态区加粗（用户点名：项目/权限/通讯/运行状态/监视全加粗）。
            // 放构造里按当前字号原样加粗，详见 ApplyHeaderBoldFonts 注释（为什么不动 Designer）。
            ApplyHeaderBoldFonts();

            // 【V1.79】项目前缀标签定宽：Designer 里 AutoSize=false + Dock=Left（撑满高度居中），
            // 宽度这里按 PreferredWidth 收——"当前项目："五个字刚好包住，跟运行字号/DPI 走，
            // 写死像素换字号就夹字、多留又挤项目名。必须在加粗之后收（粗体比常规体宽）。
            lblProjectPrefix.Width = lblProjectPrefix.PreferredWidth;

            // 【V1.60 深色/浅色主题】读出上次保存的主题并给主窗体着色（按钮等语义色原样保留，
            // 详见 ThemeManager 类头"配色约定"）。
            // 工位大画布在 MainForm_Load → CreateWorkstationPanels 里同步主题；
            // 各子窗体在每次打开前 ApplyTo（见各 ShowDialog/Show 调用处），切换时 ApplyToAllOpenForms 全刷。
            ThemeManager.LoadFromConfig();
            ThemeManager.ApplyTo(this);
            ApplyOperationButtonsTheme();

            // 【V1.49】主窗体开启双缓冲，与工位面板/网格双缓冲配合，消除滚动撕裂
            this.DoubleBuffered = true;

            // 【V1.67】项目档案就位（必须在 ApplyHomeLayout/LoadConfig/LoadRecipes 之前：
            // HomeLayout/配方/策略文件的路径都依赖当前项目目录）。
            // 首跑自动建"烧屏测试"；【V1.68 改干净】不认程序目录下的散文件（无老用户，不搬家）；
            // 【V1.72.10】EnsureActiveProfile 顺手清掉遗留的 Default 目录（列表里不再有 Default）。
            string activeProject = ProjectProfile.EnsureActiveProfile();
            System.Diagnostics.Debug.WriteLine($"[项目档案] 当前项目: {activeProject}");
            UpdateProjectDisplay(activeProject);

            // 1.5 应用主页布局（从 HomeLayout.json 读取各区域尺寸；文件不存在则用内置默认）
            // 【V1.58】原来这里调用 AdjustRightPanelWidth 按内容自动算右侧宽度，
            // 改为布局配置驱动：右侧宽度 / 顶部标题栏高 / 菜单栏高 / 状态栏高都可在
            // "关于 → 主页区域调整"可视化编辑器里拖动矩形块边缘调整。
            ApplyHomeLayout();

            // 【V1.58】右侧面板被用户手动拖动分隔条改宽/改窄时，"操作"分组里的按钮
            // 宽度也要跟着缩放，否则按钮会溢出分组框。订阅 SizeChanged 每次自动同步。
            groupBoxOperation.SizeChanged += (s, e) => ResizeOperationButtons();

            // 2. 加载配置（从 App.config 读取设备数量、采集间隔等）
            _config = LoadConfig();

            // 2.5 【配方持久化】启动时从本地 Recipes.json 加载配方列表
            LoadRecipes();

            // 3. 初始化设备管理器（连接硬件、启动数据采集）
            _deviceManager = new DeviceManager(_config);

            // 【V1.88.9 调试部署】启动水印必须是第一行日志：版本/构建时间/混淆标记/进程位数/OS，
            // 客户拷回 Logs 时先看这行定版本（调试期一天可能发多个包），崩溃堆栈反解也靠它对 mapping。
            // WriteLog 同时进 UI 文本框和 AppLog 文件；构造期 txtLog 已建好（InitializeComponent 在前），可安全调用。
            WriteLog(Services.BuildWatermark.GetStartupLine());

            // 【V1.68】MES 状态首屏可见（出差联调第一眼：开没开、往哪发、Mock 还是真发）
            if (_config.MesEnabled)
            {
                string mesWhere = string.IsNullOrWhiteSpace(_config.MesEndpoint)
                    ? "（未配地址，只记日志不发）" : ("→ " + _config.MesEndpoint.Trim());
                WriteLog("[MES] 上报已启用" + (_config.MesMockEnabled ? "（Mock：只记 CSV 不发 HTTP）" : mesWhere));
            }

            // 4. 订阅设备管理器事件（批量数据更新、连接状态变更、送风机数据更新）
            // 【修复 M2】改为订阅批量数据更新事件，一次更新所有面板
            _deviceManager.OnBatchDataUpdated += DeviceManager_OnBatchDataUpdated;
            // 【V1.30】订阅 IO 触发后快速跟踪增量更新事件（只刷新触发的那台面板）
            _deviceManager.OnQuickTrackDataUpdated += DeviceManager_OnQuickTrackDataUpdated;
            _deviceManager.OnConnectionStatusChanged += DeviceManager_OnConnectionStatusChanged;
            // 【V1.10 新增】订阅送风机数据更新事件（独立定时器触发）
            _deviceManager.OnFanDataUpdated += DeviceManager_OnFanDataUpdated;

            // 【V1.16 新增】订阅启动/连接诊断事件（气压表串口、耦合器、送风机连接结果）
            // 后台线程触发，处理器内部用 BeginInvoke 切回 UI 线程写 LOG
            _deviceManager.OnDiagnostic += DeviceManager_OnDiagnostic;

            // 4.5 【V1.16 新增】初始化扫码枪服务（真实扫码枪接入，参考 SerialScannerTest Demo）
            // 注意：必须在 UI 线程创建，这样扫码事件会自动封送到 UI 线程，订阅者可直接更新控件
            _scanner = new ScannerService(_config);
            // 扫码完成 → 写日志；ID绑定窗体打开时由其自行订阅同一服务（见 IdBindingForm）
            _scanner.OnBarcodeScanned += Scanner_OnBarcodeScanned;
            // 连接状态变化（连接成功/未找到端口/错误）→ 写日志
            _scanner.OnStatusChanged += Scanner_OnStatusChanged;

            // 5. 更新权限显示（V1.19.7：角色名着色——管理员=红/技术员=蓝/操作员=绿；V1.47 技术员蓝色加深）
            UpdatePermissionDisplay(_currentPermission);

            // 6. 更新状态栏信息
            UpdateStatusBar();

            // 【新增】7. 初始化按钮权限状态
            // 默认未登录（操作员权限），参数设置按钮不可用
            UpdateButtonPermissionStates();

            // 注意：下拉菜单不再需要预先初始化
            // 改为在按钮点击事件中动态创建弹出窗体（见 ShowDropdownPopup 方法）
        }

        /// <summary>
        /// 计算右侧操作面板的目标宽度（【V1.65】纯函数，可单元测试，不碰任何控件）。
        ///
        /// 规则：
        /// - 现场保存过 HomeLayout.json（hasCustomLayout=true）→ 用户自定义优先，原样返回
        ///   文件里的绝对值，不做比例换算（用户在编辑器里拖的就是像素，所见即所得）；
        /// - 否则 → 分隔容器宽 × <see cref="RightPanelRatio"/>（0.234，现状比例），再用
        ///   <see cref="RightPanelMinWidth"/>（180）/ <see cref="RightPanelMaxWidth"/>（340）
        ///   钳制：小屏不挤坏按钮文字，大屏不浪费网格空间。
        /// </summary>
        /// <param name="containerWidth">分隔容器当前总宽（像素）；≤0 时按设计宽 1400 兜底</param>
        /// <param name="hasCustomLayout">现场是否保存过 HomeLayout.json</param>
        /// <param name="customWidth">文件里的 RightPanelWidth（仅 hasCustomLayout=true 时有效）</param>
        public static int ComputeRightPanelWidth(int containerWidth, bool hasCustomLayout, int customWidth)
        {
            if (hasCustomLayout) return customWidth;
            int baseWidth = containerWidth > 0 ? containerWidth : 1400;
            int target = (int)Math.Round(baseWidth * RightPanelRatio);
            if (target < RightPanelMinWidth) target = RightPanelMinWidth;
            if (target > RightPanelMaxWidth) target = RightPanelMaxWidth;
            return target;
        }

        /// <summary>
        /// 窗口宽度变化记忆（SplitContainer.Resize 防重复入口）：
        /// - 只有总宽变了才按比例重算右侧（高度变化不重算，避免无谓抖动）；
        /// - 用户手动拖分隔条只改 SplitterDistance、不改总宽，不会触发重算，
        ///   手动拖动不会被比例覆盖；但下次窗口宽度变化时仍会按比例重算
        ///   （想永久固定就去"主页区域调整"编辑器保存，json 绝对值优先）。
        /// </summary>
        private int _lastSplitWidth = -1;

        /// <summary>
        /// 分隔容器尺寸变化 → 窗口宽度变了且无自定义 json 时，按比例重算右侧宽度。
        /// 有 json 时直接返回（绝对值优先，窗口缩放右侧保持不动，符合 FixedPanel=Panel2 语义）。
        /// </summary>
        private void SplitContainerMain_Resize(object sender, EventArgs e)
        {
            if (splitContainerMain.Width == _lastSplitWidth) return;
            _lastSplitWidth = splitContainerMain.Width;
            if (System.IO.File.Exists(HomeLayoutConfig.GetConfigPath())) return;
            AdjustRightPanelWidth();
        }

        /// <summary>
        /// 自适应调整右侧操作面板（tableLayoutPanelRight）的宽度
        ///
        /// 【V1.58 改造】原实现用"临时 AutoSize 测量内容最小宽度"来自动定右侧宽度；
        /// 现在右侧宽度由 <see cref="HomeLayoutConfig.RightPanelWidth"/> 驱动（可在
        /// "关于 → 主页区域调整"编辑器里拖边缘调整）。本方法改为：
        /// 1. 设置 splitContainerMain.SplitterDistance，使 Panel2 宽度 = 配置的右侧宽度；
        /// 2. 同步缩放"操作"分组里的按钮宽度（按钮原设计宽 256，若右侧被调窄则按比例缩，
        ///    避免按钮溢出分组框）。
        /// FixedPanel=Panel2（已在 Designer 中设置）确保窗口缩放时右侧宽度不变。
        ///
        /// 【V1.65】"配置的右侧宽度"语义变化：无 json 时不再是写死的
        /// <see cref="DefaultRightPanelWidth"/>，而是 <see cref="ComputeRightPanelWidth"/>
        /// 按窗口实际宽度 × 0.234 算出的比例值（180~340 钳制），窗口缩放由
        /// <see cref="SplitContainerMain_Resize"/> 自动跟进。有 json 时仍是文件绝对值优先。
        ///
        /// 【注意】右侧宽度并非随便能调：若窄到按钮文字放不下会被截断，编辑器里有
        /// 180~600 的下限保护；现场不满意可在编辑器里再拖回来。
        /// </summary>
        private void AdjustRightPanelWidth()
        {
            // 无 json → 按窗口比例算；有 json → 文件绝对值优先（用户自定义）。
            // 构造极早期分隔容器宽不可读（≤0）时，ComputeRightPanelWidth 内部按设计宽 1400 兜底。
            bool hasCustom = System.IO.File.Exists(HomeLayoutConfig.GetConfigPath());
            int custom = 0;
            if (hasCustom)
            {
                custom = HomeLayoutConfig.LoadOrDefault().RightPanelWidth;
            }
            int targetRight = ComputeRightPanelWidth(splitContainerMain.Width, hasCustom, custom);
            _lastSplitWidth = splitContainerMain.Width;

            // 1. 设置 SplitterDistance，让 Panel2 宽度 = 目标右侧宽度
            //    Panel2 宽度 = splitContainerMain 总宽 - SplitterDistance - 分隔条宽度
            //    => SplitterDistance = 总宽 - 右侧宽度 - 分隔条宽度
            int distance = splitContainerMain.Width - targetRight - splitContainerMain.SplitterWidth;
            if (distance > 0)
            {
                splitContainerMain.SplitterDistance = distance;
            }

            // 2. 同步缩放"操作"分组里的按钮宽度（按钮 X=15、宽 256 是设计值，
            //    右侧变窄后按分组可用宽度重新计算，保证按钮不溢出、文字尽量完整）
            ResizeOperationButtons();
        }

        /// <summary>
        /// 【V1.58】应用主页布局：按 HomeLayoutConfig 设置顶部标题栏高 / 菜单栏高 /
        /// 右侧区域宽 / 状态栏高。入口有二：
        /// - 程序启动（构造函数调用，读取 json 或默认值）；
        /// - "主页区域调整"编辑器保存后调用，让新布局立即生效。
        /// </summary>
        private void ApplyHomeLayout()
        {
            var layout = HomeLayoutConfig.LoadOrDefault();

            // 顶部标题栏高度（tableLayoutPanelMain 第 0 行）
            tableLayoutPanelMain.RowStyles[0].Height = layout.TopBarHeight;
            // 菜单栏高度（第 1 行）
            tableLayoutPanelMain.RowStyles[1].Height = layout.MenuHeight;
            // 底部状态栏高度（第 3 行；第 2 行是 splitContainerMain，用 Percent 自动占剩余）
            tableLayoutPanelMain.RowStyles[3].Height = layout.StatusBarHeight;

            // 【V1.58】菜单栏加高后，4 个菜单按钮高度同步填满（上下各留 3px 边距），
            // 否则按钮仍是固定的 28px 高、底部留一条空白，视觉上不协调。
            // 算法：菜单栏行高 - tableLayoutPanelMenu 上下 Margin(3×2) - 按钮上下 Margin(3×2)。
            foreach (Control ctl in tableLayoutPanelMenu.Controls)
            {
                // 【V1.71】菜单按钮已换 Sunny UIButton（不是原生 Button 子类，is Button 认不出）
                if (ctl is Sunny.UI.UIButton btn)
                {
                    btn.Height = layout.MenuHeight - 12;
                }
            }

            // 右侧区域宽度（由 AdjustRightPanelWidth 内部读同一配置设置 SplitterDistance）
            AdjustRightPanelWidth();
        }

        /// <summary>
        /// 【V1.58】把"操作"分组里的 7 个按钮宽度同步为分组可用宽度 - 左右边距（V1.67 加下料判定）。
        /// 原设计按钮宽 256（groupBox 宽 292）；右侧区域可调后，分组框宽度随之变化，
        /// 若仍用固定宽会导致按钮溢出分组框或被截断。宽度 = 分组客户区宽 - 30（左右各 15）。
        /// </summary>
        private void ResizeOperationButtons()
        {
            if (groupBoxOperation == null) return;

            // 分组客户区宽度（已扣除分组框边框），按钮左右各留 15px 边距
            int buttonWidth = groupBoxOperation.ClientSize.Width - 30;
            if (buttonWidth < 80) buttonWidth = 80;   // 下限保护：按钮不能窄到没法看

            foreach (Control ctl in groupBoxOperation.Controls)
            {
                // 只处理操作按钮（【V1.71】已换 Sunny UIButton；若以后加入非按钮控件需排除）
                if (ctl is Sunny.UI.UIButton btn && btn != null)
                {
                    btn.Width = buttonWidth;
                }
            }
        }

        /// <summary>
        /// 在指定主按钮下方显示下拉菜单（使用无边框弹出窗体实现）
        ///
        /// 【设计说明】
        /// 不使用 ContextMenuStrip（系统菜单项高度由系统绘制，无法和主按钮对齐），
        /// 改用无边框 Form + TableLayoutPanel + Button 列表方案：
        /// - 每个菜单项是一个独立的 Button
        /// - 菜单项宽度 = 主按钮宽度
        /// - 菜单项高度 = 主按钮高度
        /// - 菜单项样式（背景色、文字色、字体）和主按钮完全一致
        /// - 点击任意菜单项后自动关闭弹出窗体
        /// - 失去焦点时自动关闭（点击窗体外任何地方）
        /// - 按 Esc 键关闭
        /// </summary>
        /// <param name="hostButton">触发下拉的主按钮，菜单将显示在按钮下方</param>
        /// <param name="items">菜单项数组，每项包含文本和点击处理程序</param>
        private void ShowDropdownPopup(Control hostButton, (string Text, EventHandler ClickHandler)[] items)
        {
            // ===== 1. 创建弹出窗体（无边框） =====
            var popup = new Form
            {
                FormBorderStyle = FormBorderStyle.None,        // 无边框
                StartPosition = FormStartPosition.Manual,      // 手动指定位置
                ShowInTaskbar = false,                         // 不在任务栏显示
                KeyPreview = true                              // 允许接收按键事件（按 Esc 关闭）
            };

            // ===== 2. 计算弹出窗体尺寸 =====
            // 宽度 = 主按钮宽度，高度 = 主按钮高度 × 菜单项数量
            int itemWidth = hostButton.Width;
            int itemHeight = hostButton.Height;
            popup.ClientSize = new Size(itemWidth, itemHeight * items.Length);

            // ===== 3. 创建 TableLayoutPanel 用于垂直排列菜单项按钮 =====
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = items.Length,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };

            // 设置列宽（绝对值，等于主按钮宽度）
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, itemWidth));

            // 设置每行行高（绝对值，等于主按钮高度）
            for (int i = 0; i < items.Length; i++)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, itemHeight));
            }

            // ===== 4. 创建每个菜单项按钮（样式和主按钮一致） =====
            // 【V1.71】宿主已换 Sunny UIButton：BackColor 读出来是底衬不是显示色，
            // 用 ThemeManager.GetEffectiveButtonColors 读真实显示色（FillColor）。
            Color hostBack;
            Color hostFore;
            ThemeManager.GetEffectiveButtonColors(hostButton, out hostBack, out hostFore);
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];

                var btn = new Button
                {
                    Text = item.Text,                          // 菜单项文本
                    Dock = DockStyle.Fill,                     // 填满单元格
                    Margin = new Padding(0),                   // 无外边距，紧贴相邻项
                    BackColor = hostBack,                      // 继承主按钮显示底色
                    ForeColor = hostFore,                      // 继承主按钮文字色
                    FlatStyle = FlatStyle.Flat,                // 扁平化样式
                    Cursor = Cursors.Hand,                     // 鼠标悬停显示手型
                    Font = hostButton.Font                     // 继承主按钮字体
                };

                // 点击菜单项：先关闭弹出窗体，再执行处理程序
                // 【注意】捕获当前循环变量到局部变量，避免闭包捕获问题
                var capturedItem = item;
                btn.Click += (s, e) =>
                {
                    popup.Close();                  // 1. 关闭弹出窗体
                    capturedItem.ClickHandler(s, e); // 2. 执行菜单项处理逻辑
                };

                // 添加到布局面板的第 i 行
                layout.Controls.Add(btn, 0, i);
            }

            // 把布局面板加入弹出窗体
            popup.Controls.Add(layout);

            // ===== 5. 设置弹出位置：主按钮下方左对齐 =====
            Point screenPos = hostButton.PointToScreen(new Point(0, hostButton.Height));
            popup.Location = screenPos;

            // ===== 6. 注册自动关闭事件 =====
            // 失去焦点时自动关闭（点击弹出窗体外任何地方都会触发 Deactivate）
            EventHandler deactivateHandler = null;
            deactivateHandler = (s, e) =>
            {
                popup.Close();
            };
            popup.Deactivate += deactivateHandler;

            // 按 Esc 键关闭弹出窗体
            KeyEventHandler keyDownHandler = null;
            keyDownHandler = (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    popup.Close();
                }
            };
            popup.KeyDown += keyDownHandler;

            // 【修复 M9】窗体关闭时取消事件订阅，避免闭包持有 popup 引用导致内存泄漏
            popup.FormClosed += (s, e) =>
            {
                popup.Deactivate -= deactivateHandler;
                popup.KeyDown -= keyDownHandler;
                // 【V1.72.13】非模态关闭后释放（与设置窗三 popup 同病根：Close 不释放
                // 非模态窗体；这里虽是原生 Button 不炸跨线程，不释放就是纯泄漏，顺手收掉）
                popup.Dispose();
            };

            // ===== 7. 显示弹出窗体（非模态，不阻塞主窗体） =====
            // 【V1.60】弹出窗体跟随当前主题（菜单项按钮继承主按钮绿配色不动，只换窗体底）
            ThemeManager.ApplyTo(popup);
            popup.Show(this);
        }

        /// <summary>
        /// 加载配置
        /// 从 App.config 读取设备配置参数，读取失败时使用默认值
        /// </summary>
        /// <returns>设备配置对象</returns>
        private DeviceConfig LoadConfig()
        {
            var config = new DeviceConfig();

            // 尝试从配置文件读取气压表总数
            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["TotalBarometers"], out int count))
            {
                // 【大扫荡】总数必须 >0：手改 0/负数以前直通构造，零长数组后面全越界。
                // 回缺省 72（与 DeviceConfig 缺省一致，V1.63 钳制同模式）。
                if (count <= 0)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"配置警告: TotalBarometers({count}) 非法，已回退 72");
                }
                else
                {
                    config.TotalBarometers = count;
                }
            }

            // 尝试从配置文件读取采集间隔
            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["CollectInterval"], out int interval))
            {
                // 【大扫荡】采集间隔必须 ≥50ms：0/负数 Timer 构造即抛；太小打爆串口。
                if (interval < 50)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"配置警告: CollectInterval({interval}) 非法，已回退 1000");
                }
                else
                {
                    config.CollectInterval = interval;
                }
            }

            // 【修复 H7】补全所有配置项的读取，避免 App.config 修改不生效
            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["PanelColumns"], out int panelCols))
            {
                config.PanelColumns = panelCols;
            }

            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["PanelRows"], out int panelRows))
            {
                config.PanelRows = panelRows;
            }

            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["TotalInputs"], out int totalInputs))
            {
                config.TotalInputs = totalInputs;
            }

            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["TotalOutputs"], out int totalOutputs))
            {
                config.TotalOutputs = totalOutputs;
            }

            // 读取 PLC 通讯参数
            string plcAddress = System.Configuration.ConfigurationManager.AppSettings["PlcAddress"];
            if (!string.IsNullOrWhiteSpace(plcAddress))
            {
                config.PlcAddress = plcAddress;
            }

            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["PlcPort"], out int plcPort))
            {
                config.PlcPort = plcPort;
            }

            string portName = System.Configuration.ConfigurationManager.AppSettings["PortName"];
            if (!string.IsNullOrWhiteSpace(portName))
            {
                config.PortName = portName;
            }

            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["BaudRate"], out int baudRate))
            {
                // 【V1.63】手改配置可能写出 0/负数（设置窗下拉造不出来，但记事本手改能绕过校验）：
                // SerialPort 赋值时直接抛异常，被 Connect 的 try/catch 吃成"连不上"，排查方向误导。
                // 这里钳制 + 记警告，非法值启动日志里一眼可见（与下方 TotalInputs 纠错同模式）。
                int clampedBaud = SerialPortHelper.ClampBaudRate(baudRate, config.BaudRate);
                if (clampedBaud != baudRate)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"配置警告: BaudRate({baudRate}) 非法，已回退 {clampedBaud}");
                }
                config.BaudRate = clampedBaud;
            }

            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["DataBits"], out int dataBits))
            {
                // 【V1.63】DataBits 只认 5~8（同上，非法配了就是"连不上"误导）。
                int clampedBits = SerialPortHelper.ClampDataBits(dataBits);
                if (clampedBits != dataBits)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"配置警告: DataBits({dataBits}) 非法，已回退 {clampedBits}");
                }
                config.DataBits = clampedBits;
            }

            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["StopBits"], out int stopBits))
            {
                config.StopBits = stopBits;
            }

            string parity = System.Configuration.ConfigurationManager.AppSettings["Parity"];
            if (!string.IsNullOrWhiteSpace(parity))
            {
                config.Parity = parity;
            }

            if (bool.TryParse(System.Configuration.ConfigurationManager.AppSettings["UseMockCommunication"], out bool useMock))
            {
                config.UseMockCommunication = useMock;
            }

            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["SerialReadTimeoutMs"], out int serialReadTimeoutMs))
            {
                // 【V1.63】超时必须为正数（同上，非法配了也是"连不上"误导）。
                int clampedReadTimeout = SerialPortHelper.ClampTimeoutMs(serialReadTimeoutMs, config.SerialReadTimeoutMs);
                if (clampedReadTimeout != serialReadTimeoutMs)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"配置警告: SerialReadTimeoutMs({serialReadTimeoutMs}) 非法，已回退 {clampedReadTimeout}");
                }
                config.SerialReadTimeoutMs = clampedReadTimeout;
            }

            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["SerialWriteTimeoutMs"], out int serialWriteTimeoutMs))
            {
                int clampedWriteTimeout = SerialPortHelper.ClampTimeoutMs(serialWriteTimeoutMs, config.SerialWriteTimeoutMs);
                if (clampedWriteTimeout != serialWriteTimeoutMs)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"配置警告: SerialWriteTimeoutMs({serialWriteTimeoutMs}) 非法，已回退 {clampedWriteTimeout}");
                }
                config.SerialWriteTimeoutMs = clampedWriteTimeout;
            }

            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["TcpSendTimeoutMs"], out int tcpSendTimeoutMs))
            {
                config.TcpSendTimeoutMs = tcpSendTimeoutMs;
            }

            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["TcpReceiveTimeoutMs"], out int tcpReceiveTimeoutMs))
            {
                config.TcpReceiveTimeoutMs = tcpReceiveTimeoutMs;
            }

            if (bool.TryParse(System.Configuration.ConfigurationManager.AppSettings["InvertInputs"], out bool invertInputs))
            {
                config.InvertInputs = invertInputs;
            }

            if (bool.TryParse(System.Configuration.ConfigurationManager.AppSettings["InvertOutputs"], out bool invertOutputs))
            {
                config.InvertOutputs = invertOutputs;
            }

            if (byte.TryParse(System.Configuration.ConfigurationManager.AppSettings["IoUnitId"], out byte ioUnitId))
            {
                config.IoUnitId = ioUnitId;
            }

            if (TryParseUShortFromAppSettings("IoInputRegisterStartAddress", out ushort ioInputStart))
            {
                config.IoInputRegisterStartAddress = ioInputStart;
            }

            if (TryParseUShortFromAppSettings("IoOutputRegisterStartAddress", out ushort ioOutputStart))
            {
                config.IoOutputRegisterStartAddress = ioOutputStart;
            }

            // 【备用通道映射】总开关：现场某个 DQ 通道烧毁后，把该通道信号改写到备用通道。
            // 默认 false（多数工作台正常，不受影响）；只有需要的现场才置 true。
            if (bool.TryParse(System.Configuration.ConfigurationManager.AppSettings["IoBackupChannelMappingEnabled"], out bool ioBackupEnabled))
            {
                config.IoBackupChannelMappingEnabled = ioBackupEnabled;
            }

            // 备用通道映射表：格式 "0x2000@0x00->0x2009@0x00;0x2008@0x00->0x2009@0x01"
            // （寄存器@通道均为十六进制，通道 0x00~0x0F，与设置界面显示一致）
            // 解析失败的项会跳过（不影响合法项）；若有解析问题且开关为 true，额外记一条告警日志方便排查。
            string ioBackupMappingsRaw = System.Configuration.ConfigurationManager.AppSettings["IoBackupChannelMappings"];
            if (!string.IsNullOrWhiteSpace(ioBackupMappingsRaw))
            {
                config.IoBackupChannelMappings = AgingTestSystem.Models.IoOutputChannelRemap.ParseAll(ioBackupMappingsRaw, out string parseError);
                if (config.IoBackupChannelMappingEnabled && !string.IsNullOrEmpty(parseError))
                {
                    System.Diagnostics.Trace.TraceWarning($"IoBackupChannelMappings 配置存在未解析项：{parseError}");
                }
            }

            if (TryParseUShortFromAppSettings("BarometerPressureRegisterAddress", out ushort barometerPressureReg))
            {
                config.BarometerPressureRegisterAddress = barometerPressureReg;
            }

            if (decimal.TryParse(System.Configuration.ConfigurationManager.AppSettings["BarometerPressureScale"], out decimal barometerPressureScale))
            {
                config.BarometerPressureScale = barometerPressureScale;
            }

            if (decimal.TryParse(System.Configuration.ConfigurationManager.AppSettings["AlarmPressureThresholdKPa"], out decimal alarmThresholdKPa))
            {
                config.AlarmPressureThresholdKPa = alarmThresholdKPa;
            }

            if (bool.TryParse(System.Configuration.ConfigurationManager.AppSettings["AlarmWhenPressureHigherThanThreshold"], out bool alarmHigher))
            {
                config.AlarmWhenPressureHigherThanThreshold = alarmHigher;
            }

            // ===== 冷却送风机配置读取（V1.10 新增） =====
            if (bool.TryParse(System.Configuration.ConfigurationManager.AppSettings["FanEnabled"], out bool fanEnabled))
            {
                config.FanEnabled = fanEnabled;
            }

            string fanIpAddress = System.Configuration.ConfigurationManager.AppSettings["FanIpAddress"];
            if (!string.IsNullOrWhiteSpace(fanIpAddress))
            {
                config.FanIpAddress = fanIpAddress;
            }

            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["FanPort"], out int fanPort))
            {
                config.FanPort = fanPort;
            }

            if (byte.TryParse(System.Configuration.ConfigurationManager.AppSettings["FanUnitId"], out byte fanUnitId))
            {
                config.FanUnitId = fanUnitId;
            }

            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["FanTimeoutMs"], out int fanTimeoutMs))
            {
                config.FanTimeoutMs = fanTimeoutMs;
            }

            // 送风机 IP 自动识别（V1.12 新增）：
            // FanAutoDetectEnabled=true 时，连接送风机按顺序尝试 FanIpAddress + FanIpCandidates，
            // 第一个连上的就是设备真实地址，现场换控制器/IP 不用改配置。
            if (bool.TryParse(System.Configuration.ConfigurationManager.AppSettings["FanAutoDetectEnabled"], out bool fanAutoDetect))
            {
                config.FanAutoDetectEnabled = fanAutoDetect;
            }

            // 候选 IP 列表（逗号/分号分隔，自动过滤非法 IP 并去重）
            string fanIpCandidates = System.Configuration.ConfigurationManager.AppSettings["FanIpCandidates"];
            config.FanIpCandidates = DeviceConfig.ParseFanIpCandidates(fanIpCandidates);

            // ===== 老化测试业务参数读取（V1.10 新增） =====
            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["VacuumConfirmTimeoutMs"], out int vacuumConfirmTimeoutMs))
            {
                config.VacuumConfirmTimeoutMs = vacuumConfirmTimeoutMs;
            }

            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["CommunicationLossAlarmCount"], out int commLossCount))
            {
                config.CommunicationLossAlarmCount = commLossCount;
            }

            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["MaxTestDurationSeconds"], out int maxTestDurationSeconds))
            {
                config.MaxTestDurationSeconds = maxTestDurationSeconds;
            }

            if (bool.TryParse(System.Configuration.ConfigurationManager.AppSettings["UseDiAlarmContact"], out bool useDiAlarmContact))
            {
                config.UseDiAlarmContact = useDiAlarmContact;
            }

            if (float.TryParse(System.Configuration.ConfigurationManager.AppSettings["FanTempAlarmLimitC"], out float fanTempAlarmLimitC))
            {
                config.FanTempAlarmLimitC = fanTempAlarmLimitC;
            }

            // 【V1.66】超温全线联停开关：默认 false（只记日志不停机=现状），现场确认后置 true。
            if (bool.TryParse(System.Configuration.ConfigurationManager.AppSettings["FanTempShutdownEnabled"], out bool fanTempShutdown))
            {
                config.FanTempShutdownEnabled = fanTempShutdown;
            }

            // ===== 扫码枪配置读取（V1.16 新增，参考 SerialScannerTest Demo） =====
            // 扫码枪是可选设备：默认关闭，现场需要扫码（如 ID 绑定扫 SN）时在 App.config 打开
            if (bool.TryParse(System.Configuration.ConfigurationManager.AppSettings["ScannerEnabled"], out bool scannerEnabled))
            {
                config.ScannerEnabled = scannerEnabled;
            }

            // 固定串口：留空表示按关键词 WMI 自动识别
            string scannerPort = System.Configuration.ConfigurationManager.AppSettings["ScannerPort"];
            if (!string.IsNullOrWhiteSpace(scannerPort))
            {
                config.ScannerPort = scannerPort.Trim();
            }

            // 设备识别关键词（设备管理器显示的名称关键字，默认 Honeywell Xenon 1902）
            string scannerDeviceKeyword = System.Configuration.ConfigurationManager.AppSettings["ScannerDeviceKeyword"];
            if (!string.IsNullOrWhiteSpace(scannerDeviceKeyword))
            {
                config.ScannerDeviceKeyword = scannerDeviceKeyword.Trim();
            }

            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["ScannerBaudRate"], out int scannerBaudRate))
            {
                // 【V1.63】扫码枪串口同气压表串口：非法值钳制 + 记警告（回退 115200）。
                int clampedScannerBaud = SerialPortHelper.ClampBaudRate(scannerBaudRate, config.ScannerBaudRate);
                if (clampedScannerBaud != scannerBaudRate)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"配置警告: ScannerBaudRate({scannerBaudRate}) 非法，已回退 {clampedScannerBaud}");
                }
                config.ScannerBaudRate = clampedScannerBaud;
            }

            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["ScannerDataBits"], out int scannerDataBits))
            {
                int clampedScannerBits = SerialPortHelper.ClampDataBits(scannerDataBits);
                if (clampedScannerBits != scannerDataBits)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"配置警告: ScannerDataBits({scannerDataBits}) 非法，已回退 {clampedScannerBits}");
                }
                config.ScannerDataBits = clampedScannerBits;
            }

            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["ScannerStopBits"], out int scannerStopBits))
            {
                config.ScannerStopBits = scannerStopBits;
            }

            string scannerParity = System.Configuration.ConfigurationManager.AppSettings["ScannerParity"];
            if (!string.IsNullOrWhiteSpace(scannerParity))
            {
                config.ScannerParity = scannerParity;
            }

            // 【V1.16.3】扫码枪心跳调试日志开关（排查"断连识别不到"用）
            if (bool.TryParse(System.Configuration.ConfigurationManager.AppSettings["ScannerDebugLog"], out bool scannerDebugLog))
            {
                config.ScannerDebugLog = scannerDebugLog;
            }

            // 【V1.67】工艺策略（机器级缺省；随后 ProjectPolicyStore.ApplyOverlay 用
            // 当前项目的 Policy.json 覆盖——项目没配过的项保持这里的缺省=现状行为）。
            // 枚举非法/缺省一律回"现状值"，不抛异常（手改配置文件写错也不炸，见 ParsePolicyEnum）。
            config.ZeroDurationPolicy = ParsePolicyEnum(
                System.Configuration.ConfigurationManager.AppSettings["ZeroDurationPolicy"], ZeroDurationPolicy.Warn);
            config.EmptySnPolicy = ParsePolicyEnum(
                System.Configuration.ConfigurationManager.AppSettings["EmptySnPolicy"], EmptySnPolicy.Warn);
            config.FanDisconnectPolicy = ParsePolicyEnum(
                System.Configuration.ConfigurationManager.AppSettings["FanDisconnectPolicy"], FanDisconnectPolicy.LogOnly);
            config.VacuumFailKind = ParsePolicyEnum(
                System.Configuration.ConfigurationManager.AppSettings["VacuumFailKind"], VacuumFailKind.ProductFail);
            config.CompletionJudgePolicy = ParsePolicyEnum(
                System.Configuration.ConfigurationManager.AppSettings["CompletionJudgePolicy"], CompletionJudgePolicy.AutoPass);
            config.PowerLossPolicy = ParsePolicyEnum(
                System.Configuration.ConfigurationManager.AppSettings["PowerLossPolicy"], PowerLossPolicy.RestartFull);
            config.AgingPressureLossPolicy = ParsePolicyEnum(
                System.Configuration.ConfigurationManager.AppSettings["AgingPressureLossPolicy"], AgingPressureLossPolicy.StopOnLoss);
            config.CompletionAction = ParsePolicyEnum(
                System.Configuration.ConfigurationManager.AppSettings["CompletionAction"], CompletionAction.PowerOffOnly);
            // 【V1.76】事件行SN/配方取值（Q8 追溯口径；随后 Policy.json 可按项目覆盖）
            config.EventIdentityMode = ParsePolicyEnum(
                System.Configuration.ConfigurationManager.AppSettings["EventIdentityMode"], EventIdentityMode.RecordTime);
            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["VentValveDoPoint"], out int ventPoint))
            {
                config.VentValveDoPoint = Math.Max(0, ventPoint);
            }
            // 【V1.73】本机破空阀开关（无阀=false 现状：手动按钮隐藏+泄压保存即拦）
            if (bool.TryParse(System.Configuration.ConfigurationManager.AppSettings["VentValveEnabled"], out bool ventEnabled))
            {
                config.VentValveEnabled = ventEnabled;
            }
            // 【V1.74】载台电流总开关（无表=false 现状：不断任何线、不读数，电流恒 NaN）
            if (bool.TryParse(System.Configuration.ConfigurationManager.AppSettings["UsePowerMeter"], out bool usePower))
            {
                config.UsePowerMeter = usePower;
            }

            // 【V1.68】MES 对接（机器级：开关/URL/超时/鉴权/重试；触发器/映射/静态字段跟项目，
            // 由下面的 ApplyOverlay 从 Policy.json 覆盖——非法字符串同样兜底缺省）。
            if (bool.TryParse(System.Configuration.ConfigurationManager.AppSettings["MesEnabled"], out bool mesEnabled))
            {
                config.MesEnabled = mesEnabled;
            }
            if (bool.TryParse(System.Configuration.ConfigurationManager.AppSettings["MesMockEnabled"], out bool mesMock))
            {
                config.MesMockEnabled = mesMock;
            }
            string mesEndpoint = System.Configuration.ConfigurationManager.AppSettings["MesEndpoint"];
            if (mesEndpoint != null) config.MesEndpoint = mesEndpoint.Trim();
            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["MesTimeoutMs"], out int mesTimeout))
            {
                config.MesTimeoutMs = Math.Max(500, mesTimeout);
            }
            string mesAuth = System.Configuration.ConfigurationManager.AppSettings["MesAuthType"];
            if (!string.IsNullOrWhiteSpace(mesAuth))
            {
                string a = mesAuth.Trim();
                config.MesAuthType = (a.Equals("Bearer", StringComparison.OrdinalIgnoreCase)
                    || a.Equals("Basic", StringComparison.OrdinalIgnoreCase)) ? a : "None";
            }
            string mesToken = System.Configuration.ConfigurationManager.AppSettings["MesAuthToken"];
            if (mesToken != null) config.MesAuthToken = mesToken;
            string mesUser = System.Configuration.ConfigurationManager.AppSettings["MesAuthUser"];
            if (mesUser != null) config.MesAuthUser = mesUser;
            string mesPass = System.Configuration.ConfigurationManager.AppSettings["MesAuthPassword"];
            if (mesPass != null) config.MesAuthPassword = mesPass;
            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["MesRetryCount"], out int mesRetry))
            {
                config.MesRetryCount = Math.Max(0, mesRetry);
            }
            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["MesRetryIntervalMs"], out int mesRetryIv))
            {
                config.MesRetryIntervalMs = Math.Max(0, mesRetryIv);
            }
            // 【V1.68】自定义头 + 按事件分地址（机器级，自由文本；格式错误保存时拦，
            // 这里只做"读得到"，脏值上报时跳过——MesMapping 兜底）
            string mesHeaders = System.Configuration.ConfigurationManager.AppSettings["MesCustomHeaders"];
            if (mesHeaders != null) config.MesCustomHeaders = mesHeaders;
            string mesEpMap = System.Configuration.ConfigurationManager.AppSettings["MesEndpointMap"];
            if (mesEpMap != null) config.MesEndpointMap = mesEpMap;
            // 【V1.68】密钥解密进内存（文件里必须是 DPAPI 密文；明文=配错了，
            // 解密失败按空处理 + 记日志，绝不带着密文去当 token 发）
            string rawToken = System.Configuration.ConfigurationManager.AppSettings["MesAuthToken"];
            string rawPass = System.Configuration.ConfigurationManager.AppSettings["MesAuthPassword"];
            config.MesAuthToken = DecryptMesSecret("MesAuthToken", rawToken);
            config.MesAuthPassword = DecryptMesSecret("MesAuthPassword", rawPass);

            // 【V1.69】规则流程（机器缺省；项目 Policy.json 随后叠加覆盖）。
            // 字符串默认空（=禁用）；SkipVacuum 默认 false（=现状三阶段）。
            if (bool.TryParse(System.Configuration.ConfigurationManager.AppSettings["SkipVacuum"], out bool skipVacuum))
            {
                config.SkipVacuum = skipVacuum;
            }

            // 【V1.75】显示模式维度开关（机器缺省 false=三窗隐藏；项目 Policy.json 随后叠加）。
            // DisplayModes 字典本身无机器读取（与 MesTriggers 同口径：缺省空+叠加）。
            if (bool.TryParse(System.Configuration.ConfigurationManager.AppSettings["DisplayModeEnabled"], out bool dmEnabled))
            {
                config.DisplayModeEnabled = dmEnabled;
            }

            // 【V1.67】项目策略叠加（Projects/<当前项目>/Policy.json 覆盖同名机器缺省）
            ProjectPolicyStore.ApplyOverlay(config);

            if (config.TotalInputs < config.TotalBarometers)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"配置警告: TotalInputs({config.TotalInputs}) < TotalBarometers({config.TotalBarometers})，将自动把 TotalInputs 纠正为 {config.TotalBarometers}");
                config.TotalInputs = config.TotalBarometers;
            }

            int minOutputs = config.TotalBarometers * 2;
            if (config.TotalOutputs < minOutputs)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"配置警告: TotalOutputs({config.TotalOutputs}) < TotalBarometers×2({minOutputs})，将自动把 TotalOutputs 纠正为 {minOutputs}");
                config.TotalOutputs = minOutputs;
            }

            // 配置项一致性校验：TotalBarometers 应等于 PanelRows × PanelColumns
            // 不匹配时记录警告（不阻止启动，但布局可能错位）
            int expectedTotal = config.PanelRows * config.PanelColumns;
            if (config.TotalBarometers != expectedTotal)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"配置警告: TotalBarometers({config.TotalBarometers}) != PanelRows({config.PanelRows}) × PanelColumns({config.PanelColumns})={expectedTotal}");
            }

            return config;
        }

        /// <summary>
        /// 解密 MES 密钥配置（【V1.68 新增】 MesAuthToken/MesAuthPassword 专用）：
        /// DPAPI 密文（"DPAPI:" 前缀）→ 明文进内存；无前缀的明文=配错，按空处理
        /// （项目未上线，不兼容明文——PasswordHasher 同先例）；
        /// 解密失败 → "" + Debug 日志（带着密文当 token 发一定 401，不如空着让问题浮现）。
        /// </summary>
        private static string DecryptMesSecret(string key, string raw)
        {
            if (raw == null) return "";
            if (!MesCrypto.IsProtected(raw))
            {
                if (raw.Length > 0)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[MES密钥] {key} 非密文格式（明文不兼容），已按空处理，请重填后保存");
                }
                return "";
            }
            string dec = MesCrypto.Unprotect(raw);
            if (dec == null)
            {
                System.Diagnostics.Debug.WriteLine($"[MES密钥] {key} 解密失败，已按空处理（请重填后保存）");
                return "";
            }
            return dec;
        }

        /// <summary>
        /// 解析工艺策略枚举（【V1.67 新增】）：
        /// 大小写不敏感（"warn"/"Warn" 都认）；空/非法/未定义值一律回 fallback
        /// （= 现状行为）——手改配置文件写错也不炸，SettingsForm 下拉选项保证正常路径全合法。
        /// </summary>
        private static T ParsePolicyEnum<T>(string raw, T fallback) where T : struct
        {
            if (string.IsNullOrWhiteSpace(raw)) return fallback;
            T parsed;
            if (Enum.TryParse<T>(raw.Trim(), true, out parsed)
                && Enum.IsDefined(typeof(T), parsed))
            {
                return parsed;
            }
            System.Diagnostics.Debug.WriteLine($"[工艺策略] 非法值 \"{raw}\" 已兜底为 {fallback}（{typeof(T).Name}）");
            return fallback;
        }

        /// <summary>
        /// 从 App.config 的 appSettings 读取 ushort
        /// 
        /// 给新手的说明：
        /// - 有些寄存器地址习惯用十六进制表示（例如 0x1000）
        /// - 但 ConfigurationManager 读出来一定是字符串，所以这里同时支持：
        ///   1) "4096" 这种十进制写法
        ///   2) "0x1000" 这种十六进制写法
        /// </summary>
        private bool TryParseUShortFromAppSettings(string key, out ushort value)
        {
            value = 0;
            string raw = System.Configuration.ConfigurationManager.AppSettings[key];
            if (string.IsNullOrWhiteSpace(raw)) return false;

            if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return ushort.TryParse(raw.Substring(2), System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out value);
            }

            return ushort.TryParse(raw, out value);
        }

        /// <summary>
        /// 窗体加载完成事件
        /// 在窗体显示之前完成动态控件的创建；设备连接改到后台线程，避免首屏卡顿
        /// </summary>
        private void MainForm_Load(object sender, EventArgs e)
        {
            // 动态创建工位显示面板（根据配置的设备数量）
            CreateWorkstationPanels();

            // 【V1.65】窗口宽度变化时右侧按比例跟进（无自定义 json 才跟进，有则保持绝对值）。
            // 挂在分隔容器 Resize 上：只关心总宽变化（见 SplitContainerMain_Resize 内部防重复），
            // 用户手动拖分隔条不触发重算。先记当前宽为基准。
            _lastSplitWidth = splitContainerMain.Width;
            splitContainerMain.Resize += SplitContainerMain_Resize;
            // Load 时窗口可能还没最大化：主动按当前实际宽度对齐一次右侧，
            // 后续最大化/还原/拖边框都由 Resize 事件自动跟进。
            AdjustRightPanelWidth();

            // 【启动优化】原逻辑在 UI 线程同步执行 _deviceManager.Start()（连接气压表串口、
            // IO 耦合器、送风机 + 首次同步轮询全部 72 台气压表，串口/网线异常时可能耗时数秒）
            // 和 _scanner.Start()，导致窗体显示前明显卡顿。现改为后台线程执行：
            // - 界面立即显示，采集/扫码枪在后台陆续就绪；
            // - DeviceManager 内部定时器为 System.Timers.Timer（线程池触发），
            //   所有事件已自行 BeginInvoke 封送，后台调用 Start() 线程安全。
            StartDevicesInBackground();

            // 启动定时器更新状态栏时间显示
            timerTime.Start();

            // 【V1.87】授权计时器（与 HJVision 的 HashTimer 一致：1 小时一格，
            // 新设备/过期弹框 + 置灰用户权限入口，不阻断启动与生产）。
            // 先补空模板（缺文件才建，已有激活绝不覆盖）：厂商只填值，不用记文件名。
            Services.SoftwareActivation.EnsureIniTemplate();
            hashTimer.Start();
        }

        /// <summary>
        /// 后台启动设备管理器与扫码枪（【启动优化】）
        /// - _deviceManager.Start()：连接气压表/IO耦合器/送风机 + 首次采集，耗时步骤放后台；
        /// - _scanner.Start()：扫码枪自动识别串口并连接（可选设备，内部已做未启用跳过）。
        /// 完成后切回 UI 线程刷新顶部"通讯模块状态"与状态栏"扫码枪"状态。
        /// </summary>
        private void StartDevicesInBackground()
        {
            Task.Run(() =>
            {
                // 启动设备管理器（开始数据采集）
                // 【V1.16】Start 只要求"气压表串口"连通；耦合器/送风机断开不影响压力采集，
                // 具体哪一步连不上会通过 OnDiagnostic 事件写进 LOG。
                bool started = false;
                try
                {
                    started = _deviceManager.Start();
                }
                catch (Exception ex)
                {
                    WriteLogOnUi($"设备启动异常：{ex.Message}");
                }

                if (!started)
                {
                    // 启动失败（气压表串口没连上）：把原因写到 LOG，方便现场排查
                    WriteLogOnUi($"设备启动失败：{_deviceManager.LastStartupError}");
                }

                // 【V1.16 新增】启动扫码枪服务（自动识别串口并连接；未启用/未插入时定时重连）
                // 扫码枪是可选设备，内部已做"ScannerEnabled=false 直接跳过"处理，不影响整机启动。
                // 注意：ScannerService 的 Start() 必须在 UI 线程执行——它内部会创建
                // System.Windows.Forms.Timer（重连/心跳）和 DeviceChangeWindow（NativeWindow，
                // 用于接收 WM_DEVICECHANGE 热插拔消息），两者都依赖 UI 消息泵；
                // 在 Task.Run 后台线程执行会导致定时器与热插拔监听失效，扫码枪无法自动重连。
                RunOnUi(() =>
                {
                    if (IsDisposed || Disposing) return;
                    try
                    {
                        _scanner?.Start();
                    }
                    catch (Exception ex)
                    {
                        WriteLog($"扫码枪启动异常：{ex.Message}");
                    }
                });

                // 【V1.16.1】顶部"通讯模块状态"只反映 IO 耦合器（阀/载台电控制）是否连接，
                // 不再用"气压表串口是否连上"冒充。Start() 内部已同步触发
                // OnConnectionStatusChanged 事件（数据源 = 耦合器），这里再按实际状态兜底刷新一次。
                // 同时刷新状态栏"扫码枪"连接状态（已连接/未连接/未启用）。
                RunOnUi(() =>
                {
                    if (IsDisposed || Disposing) return;
                    _commConnected = _deviceManager.IsIoConnected;
                    UpdateConnectionStatus();
                    RefreshScannerStatus();
                });

                // 【V1.59】断电恢复：启动完成后检查有没有上次未完成的老化任务，
                // 有则切回 UI 线程弹窗询问"整台重测 / 放弃"。
                RunOnUi(() =>
                {
                    if (IsDisposed || Disposing) return;
                    CheckPendingSessionOnStartup();
                });
            });
        }

        /// <summary>
        /// 断电恢复检查（【V1.59 新增】，UI 线程调用）
        ///
        /// 【场景】异常断电/程序崩溃时，在测工位的阀与电源在耦合器上还保持最后状态，
        /// 任务参数快照存在 TestSession.json。重启后在这里发现快照并询问操作员：
        /// - 选"是"：对每台按中断前的定格参数【整台重测】（重新开阀→抽真空→延时→
        ///   上电→满时长老化）。老化讲究连续性，断电期间产品状态未知，不续跑剩余时长；
        /// - 选"否"：安全关闭这些工位的阀与电源并删除快照（不能放着不管——
        ///   耦合器 DO 不会随程序退出自动复位）。
        /// </summary>
        private void CheckPendingSessionOnStartup()
        {
            TestSession session = _deviceManager.LoadPendingSession();
            if (session?.Stations == null || session.Stations.Count == 0) return;

            var idList = string.Join("、", session.Stations.ConvertAll(s => s.DeviceId));
            // 【V1.67】恢复文案跟随 PowerLossPolicy：续跑（重抽真空+补剩余）/ 整台重测
            bool resume = (_config.PowerLossPolicy == PowerLossPolicy.ResumeRemaining);
            string resumeLine = resume
                ? "【是】恢复测试 —— 这些台将重抽真空，按中断时刻的剩余时长补足老化（断电期间不计）\n"
                : "【是】恢复测试 —— 这些台将按原参数整台重新老化（推荐，老化要求连续性）\n";
            DialogResult r = MessageBox.Show(
                $"检测到上次退出时有 {session.Stations.Count} 台工位的老化测试未完成：\n\n" +
                $"批号：{(string.IsNullOrEmpty(session.LotNumber) ? "（无批号）" : session.LotNumber)}\n" +
                $"工位：{idList}\n\n" +
                resumeLine +
                "【否】放弃任务 —— 关闭这些工位的真空阀与载台电",
                "断电恢复",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1);

            if (r == DialogResult.Yes)
            {
                _deviceManager.RecoverSession(session);
                WriteLog(resume
                    ? $"[断电恢复] {session.Stations.Count} 台已投入续跑（重抽真空+补足剩余时长）"
                    : $"[断电恢复] {session.Stations.Count} 台已重新投入测试（整台重测）");
            }
            else
            {
                _deviceManager.DiscardSession(session);
                WriteLog($"[断电恢复] 已放弃 {session.Stations.Count} 台未完成任务，阀与电源已关闭");
            }
        }

        /// <summary>切换到 UI 线程写日志（后台线程调用时使用，避免跨线程访问控件）</summary>
        private void WriteLogOnUi(string message)
        {
            RunOnUi(() => WriteLog(message));
        }

        /// <summary>切换到 UI 线程执行（窗体已释放时安全跳过）
        /// 【V1.72.15】加 _mainClosing + 句柄查：退出期排队回调此时最密，旧版只查释放状态，
        /// BeginInvoke 进销毁中句柄仍抛 InvalidOperationException（catch 能吞，但高频刷异常埋单；
        /// 先查直接丢弃，一个异常都不抛）。action 本体包 try，防投递后关闭的竞态。</summary>
        private void RunOnUi(Action action)
        {
            if (action == null) return;
            if (_mainClosing || IsDisposed || Disposing) return;
            try
            {
                if (!IsHandleCreated) return;
                if (InvokeRequired) BeginInvoke(action);
                else action();
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        /// <summary>
        /// 创建工位网格（【V1.50】自绘大画布替代 72 面板 + TableLayoutPanel）
        ///
        /// 【布局说明】
        /// - 整个工位区域（8列×9行面板 + 行全选按钮列）合并为 1 个自绘
        ///   <see cref="WorkstationGridView"/>，尺寸 = 内容总尺寸；
        /// - 外层用 Panel.AutoScroll 容器托管，AutoFit 开时 72 站按本容器尺寸一屏显示全
        ///   （关 AutoFit 或内容超出时出现滚动条）；
        /// - 滚动时系统只需移动 1 个窗口（而非 V1.49 的 72 个），无撕裂。
        /// 【注意】不能放在 FlowLayoutPanel 中，因为 FlowLayoutPanel
        /// 不尊重子控件的 Dock=Fill 属性。
        /// </summary>
        private void CreateWorkstationPanels()
        {
            // 【修复 H5】清空前先 Dispose 旧控件，避免控件资源泄漏
            // Controls.Clear() 只移除父子关系，不会释放控件资源
            // 旧控件（含子控件）会成为孤儿，等待 GC 回收，可能耗尽 GDI 句柄
            // 【V1.72.16】foreach 直接枚举逐个 Dispose 是错的：Dispose 会把自己从父集合摘除，
            // 枚举中集合被改会导致跳过（漏释放→孤儿→终结器跨线程炸，工艺策略窗已实锤），
            // 一律走 ControlDisposeHelper（快照数组后释放，最后 Clear），不要手写 foreach。
            ControlDisposeHelper.DisposeAllAndClear(splitContainerMain.Panel1.Controls);

            // 外层滚动容器：网格画布尺寸=内容总尺寸，由本容器托管滚动条
            var scrollContainer = new Panel();
            scrollContainer.Dock = DockStyle.Fill;      // 填满整个左侧区域
            scrollContainer.AutoScroll = true;          // 内容超出时显示滚动条
            // 【V1.88.14】只要纵向滚动条：网格 AutoFit 按宽顶满，横向永远不超宽，
            // 横向条出来（启动瞬间/取整抖动）看着很怪，直接禁掉；上下滑动看。
            scrollContainer.HorizontalScroll.Enabled = false;
            scrollContainer.HorizontalScroll.Visible = false;
            // 【V1.50】滚动容器开启双缓冲，配合自绘网格消除滚动撕裂/闪烁
            EnableDoubleBuffering(scrollContainer);

            // 自绘工位网格（1 个 UserControl 画全部面板 + 行全选按钮列）
            _gridView = new WorkstationGridView();
            _gridView.Configure(_config.PanelColumns, _config.PanelRows, _config.TotalBarometers);
            // 【V1.77】电流行直绘开关：UsePowerMeter 开=每面板压力框下方加"电流："行
            // （面板 205→226、行 225→246，下游下移 21 间距不变）；关=原来布局逐像素不动。
            // 结构型开关（改后重启生效），这里 startup 装配一次即可（_config 已 LoadConfig 就绪）。
            _gridView.ShowCurrentRow = _config.UsePowerMeter;

            // 【V1.60】自绘画布跟随全局主题（浅色=PanelLayout.json 原色，深色=深灰系；
            // 上电绿/故障红等语义状态色两边都不动，见 WorkstationGridView.SetDarkMode 注释）
            _gridView.SetDarkMode(ThemeManager.IsDark);

            // 订阅"设置"按钮点击事件（V1.18：打开工位设置窗口；V1.24：按选中数量分流）
            _gridView.OnSetClicked += Panel_OnSetClicked;

            // 订阅网格内部动作日志（如行全选/取消全选），写入主窗体 LOG
            // 【大扫荡】具名方法（以前匿名 lambda 永不可退订；网格重建时旧画布被事件拽住）。
            _gridView.OnLog += GridView_OnLog;

            scrollContainer.Controls.Add(_gridView);
            splitContainerMain.Panel1.Controls.Add(scrollContainer);
        }

        /// <summary>
        /// 通过反射给控件开启双缓冲（OptimizedDoubleBuffer + AllPaintingInWmPaint）（【V1.49】）
        /// Control.DoubleBuffered 是受保护属性，TableLayoutPanel 等 ScrollableControl 子类
        /// 无法直接访问，故用反射统一开启。开启后控件绘制先在离屏缓冲完成，再一次性
        /// 复制到屏幕，消除滚动/重绘时的闪烁与撕裂。
        /// 【V1.50】网格自身的行全选按钮列、选中交互、按钮文字刷新已全部移入
        /// <see cref="WorkstationGridView"/> 内部（OnLog 通知主窗体写日志），
        /// 本方法仅保留给外层 AutoScroll 滚动容器开启双缓冲。
        /// </summary>
        /// <param name="control">目标控件</param>
        private static void EnableDoubleBuffering(Control control)
        {
            if (control == null) return;
            var prop = typeof(Control).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(control, true, null);
            }
        }

        /// <summary>
        /// 设备管理器批量数据更新事件处理
        /// 当一次采集周期完成时触发，参数为本次采集的所有数据数组
        ///
        /// 【修复 H1】增加 IsDisposed/Disposing 检查，避免窗体释放时 Invoke 抛异常
        /// 【修复 M2】改为批量事件，一次更新所有面板，避免 72 次单条事件触发的性能问题
        ///
        /// 【注意】此方法由后台线程（定时器）调用，不能直接更新UI控件，
        /// 必须使用 BeginInvoke 异步切换到 UI 线程执行
        /// 使用 BeginInvoke 而非 Invoke，避免阻塞后台采集线程
        /// </summary>
        private void DeviceManager_OnBatchDataUpdated(object sender, BarometerData[] allData)
        {
            // 窗体已释放或正在释放时直接返回，避免 Invoke 抛 ObjectDisposedException
            // 【V1.72.15】加 _mainClosing：退出期排队最密，先拦少抛异常。
            if (_mainClosing || this.IsDisposed || this.Disposing) return;

            // 防御性检查：数据为空时直接返回
            if (allData == null || allData.Length == 0) return;

            try
            {
                // 使用 BeginInvoke 异步投递到 UI 线程（不阻塞后台采集线程）
                //
                // 【修复 H9】TargetParameterCountException 参数计数不匹配
                //
                // 【问题原因】
                // Control.BeginInvoke 的签名是：BeginInvoke(Delegate method, params object[] args)
                // 当传入的第二个参数 allData 是 BarometerData[] 类型时，
                // 由于数组协变规则（BarometerData[] 可隐式转换为 object[]），
                // 编译器会把 allData 当作 params object[] 的展开值直接传入，
                // 即把数组中的每个 BarometerData 元素都当作委托的一个参数。
                // 这导致委托被调用时实际收到 N 个参数（N=allData.Length，如72个），
                // 而 Action<BarometerData[]> 只接受 1 个参数（一个 BarometerData[]），
                // 参数个数不匹配 → 抛出 TargetParameterCountException。
                //
                // 【修复方法】
                // 显式构造一个 object[] 数组，把 allData 作为它的唯一元素传入：
                //   new object[] { allData }
                // 这样 BeginInvoke 内部调用 DynamicInvoke 时，
                // 会用 object[0]（即 allData 本身）作为委托的唯一参数，
                // 与 Action<BarometerData[]> 的签名匹配。
                //
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(
                        new Action<BarometerData[]>(UpdateAllPanels),
                        new object[] { allData });
                }
                else
                {
                    UpdateAllPanels(allData);
                }
            }
            catch (ObjectDisposedException)
            {
                // 窗体已释放，忽略此异常
                // 【注意】ObjectDisposedException 继承自 InvalidOperationException，
                // 必须先 catch 子类异常，否则会被父类 catch 提前捕获（CS0160 编译错误）
            }
            catch (InvalidOperationException)
            {
                // 窗体在 BeginInvoke 前刚好释放，忽略此异常
            }
        }

        /// <summary>
        /// 单台快速跟踪增量更新事件处理（【V1.30 新增】）
        /// IO 触发后高频补读指定工位，每读到一次触发一次。
        /// 【注意】此方法由快速跟踪定时器的后台线程调用，必须用 BeginInvoke
        /// 切到 UI 线程更新对应面板；仅刷新该台，不影响其它面板。
        /// </summary>
        private void DeviceManager_OnQuickTrackDataUpdated(object sender, BarometerData data)
        {
            // 窗体已释放或正在释放时直接返回，避免 Invoke 抛 ObjectDisposedException
            // 【V1.72.15】加 _mainClosing（同 BatchData）。
            if (_mainClosing || this.IsDisposed || this.Disposing) return;

            // 防御性检查：数据为空时直接返回
            if (data == null) return;

            try
            {
                if (this.InvokeRequired)
                {
                    // 显式包成 object[]，避免 H9 参数展开陷阱
                    this.BeginInvoke(
                        new Action<BarometerData>(UpdateSinglePanel),
                        new object[] { data });
                }
                else
                {
                    UpdateSinglePanel(data);
                }
            }
            catch (ObjectDisposedException)
            {
                // 窗体已释放，忽略此异常
            }
            catch (InvalidOperationException)
            {
                // 窗体在 BeginInvoke 前刚好释放，忽略此异常
            }
        }

        /// <summary>
        /// 更新单个面板显示（【V1.30 新增】，快速跟踪专用）
        /// 按设备编号找到对应面板并调用其 UpdateData 方法，只刷新触发 IO 的那台。
        /// </summary>
        /// <param name="data">该工位最新数据</param>
        private void UpdateSinglePanel(BarometerData data)
        {
            // 窗体已释放则不更新
            // 【V1.72.15】加 _mainClosing/Disposing（排队回调关后丢弃）。
            if (_mainClosing || this.IsDisposed || this.Disposing || data == null) return;

            if (_gridView != null)
            {
                _gridView.UpdateSingle(data);
            }
        }

        /// <summary>
        /// 批量更新所有面板数据显示
        /// 一次调用完成所有面板更新，减少 UI 线程切换次数
        /// </summary>
        /// <param name="allData">本次采集的所有气压表数据</param>
        private void UpdateAllPanels(BarometerData[] allData)
        {
            // 窗体已释放则不更新
            // 【V1.72.15】加 _mainClosing/Disposing（同上）。
            if (_mainClosing || this.IsDisposed || this.Disposing || allData == null) return;

            if (_gridView != null)
            {
                _gridView.UpdateAll(allData);
            }

            // 【V1.10 新增】顺便更新右侧整机状态汇总（测试中 N 台 / 在线 M / 报警 Z）
            UpdateRunStatusSummary();
        }

        /// <summary>
        /// 送风机数据更新事件处理（【V1.10 新增】）
        ///
        /// 【线程安全】此事件在送风机独立定时器的后台线程触发，
        /// 必须用 BeginInvoke 切到 UI 线程更新控件。
        ///
        /// 【易踩的坑（H9）】BeginInvoke(Delegate, params object[] args) 会把参数数组展开。
        /// 如果直接传 data（FanData 类型），会按数组协变规则被当成 object[] 展开，
        /// 导致"参数个数不匹配"异常。必须显式包成 new object[] { data }。
        /// </summary>
        private void DeviceManager_OnFanDataUpdated(object sender, FanData data)
        {
            // 窗体已释放或正在释放时直接返回
            // 【V1.72.15】加 _mainClosing（同 BatchData）。
            if (_mainClosing || this.IsDisposed || this.Disposing) return;

            try
            {
                if (this.InvokeRequired)
                {
                    // 用 new object[] { data } 包裹，避免 H9 参数展开陷阱
                    this.BeginInvoke(
                        new Action<FanData>(UpdateFanDisplay),
                        new object[] { data });
                }
                else
                {
                    UpdateFanDisplay(data);
                }
            }
            catch (ObjectDisposedException)
            {
                // 窗体已释放，忽略
            }
            catch (InvalidOperationException)
            {
                // 窗体在 BeginInvoke 前刚好释放，忽略
            }
        }

        /// <summary>
        /// 更新送风机监视区显示（【V1.10 新增】【V1.16 调整显示项】【V1.16.1 再调整】）
        /// 显示：运行状态 / 设置温度（控制屏设定值） / 当前温度（控制屏当前温度，唯一探头）。
        /// 下部温度已按需求删除（后续加装下部探头再加）。送风机这边不关注湿度。
        /// data 为 null 表示通讯失败/离线。
        ///
        /// 【运行状态文字颜色约定（V1.16.1）】
        /// - 未连接（通讯失败/离线）= 红
        /// - 定值启动 / 程式运行中 / 已连接 = 绿（在转/在线都是绿色）
        /// - 定值停止 / 程式停止 = 灰
        /// - 未启用（配置关掉送风机）= 灰
        /// </summary>
        private void UpdateFanDisplay(FanData data)
        {
            if (this.IsDisposed) return;

            // 未启用送风机（配置 FanEnabled=false）
            if (!_deviceManager.IsFanEnabled)
            {
                lblFanState.Text = "未启用";
                lblFanState.ForeColor = Color.Gray;
                lblSetTemp.Text = "---";
                lblUpperTemp.Text = "---";
                return;
            }

            // 通讯失败 / 离线 → "未连接"（红）
            if (data == null)
            {
                lblFanState.Text = "未连接";
                lblFanState.ForeColor = Color.Red;
                lblSetTemp.Text = "---";
                lblUpperTemp.Text = "---";
                return;
            }

            // 运行状态文本 + 颜色（V1.16.1：按用户需求统一为 定值启动/定值停止/已连接 等）
            string stateText;
            Color stateColor;
            switch (data.RunState)
            {
                case FanRunState.FixedValueRunning:
                    stateText = "定值启动";     // 定值运行中 → 显示"定值启动"（绿）
                    stateColor = Color.Green;
                    break;
                case FanRunState.ProgramRunning:
                    stateText = "程式运行中";   // 程式模式运行（绿，与"在转=绿色"一致）
                    stateColor = Color.Green;
                    break;
                case FanRunState.FixedValueStopped:
                    stateText = "定值停止";     // 定值停止（灰）
                    stateColor = Color.Gray;
                    break;
                case FanRunState.ProgramStopped:
                    stateText = "程式停止";     // 程式模式停止（灰）
                    stateColor = Color.Gray;
                    break;
                default:
                    stateText = "已连接";       // 在线但状态未知 → 归为"已连接"（绿）
                    stateColor = Color.Green;
                    break;
            }
            lblFanState.Text = stateText;
            lblFanState.ForeColor = stateColor;

            // ===== 两项温度显示（V1.16.1：下部温度已删除） =====
            // 设置温度 = 控制屏的温度设定值（厂商控制屏设定，上位机只读）
            lblSetTemp.Text = $"{data.TempSetpoint:F2} °C";

            // 当前温度 = 控制屏当前温度（目前唯一探头，数据源就是设备的当前温度寄存器）
            lblUpperTemp.Text = $"{data.Temperature:F2} °C";

            // 【V1.16.3】当前温度颜色按"与设置温度的偏差"显示（控件由 TextBox 改为 Label，
            // 避免 ReadOnly 文本框获得焦点/文字选中时 ForeColor 被高亮色覆盖而不生效）：
            // 高于设置温度（lblSetTemp）→ 红（偏热，风扇需加强降温）；不高于 → 绿（正常/已到温）。
            // 原来按固定告警上限 FanTempAlarmLimitC 判断，现场更关心相对"设置温度"的高低。
            if (data.Temperature > data.TempSetpoint)
            {
                lblUpperTemp.ForeColor = Color.Red;
            }
            else
            {
                lblUpperTemp.ForeColor = Color.Green;
            }

            // 【V1.10 保留】送风机温度安全告警：超过配置上限 FanTempAlarmLimitC →
            // 仅记日志提示（不覆盖上面按设置温度显示的颜色）。
            // 【大扫荡】探头失效（NaN）边沿记"传感器无效"：以前 NaN 比较恒 false，
            // 超温保护静默致盲，全程无任何一行说明。NaN 不当超温（误停产线更糟），
            // 但必须明示"保护已盲"，恢复有效即复位边沿。
            if (float.IsNaN(data.Temperature))
            {
                if (!_fanTempInvalidLogged)
                {
                    _fanTempInvalidLogged = true;
                    WriteLog("[送风机] 温度传感器无效（读数 NaN）" +
                        (_config.FanTempShutdownEnabled
                            ? "，超温联停已盲！请检查送风机通讯/探头"
                            : "，温度告警暂停，请检查送风机通讯/探头"));
                }
            }
            else
            {
                _fanTempInvalidLogged = false;
                if (_config.FanTempAlarmLimitC > 0 && data.Temperature > _config.FanTempAlarmLimitC)
                {
                    if (data.Temperature > _fanTempAlarmLoggedThreshold)
                    {
                        // 只在第一次超过新阈值时记日志，避免每秒重复记录
                        _fanTempAlarmLoggedThreshold = data.Temperature;
                        WriteLog($"[送风机] 上部温度 {data.Temperature:F1}°C 超过告警上限 {_config.FanTempAlarmLimitC:F1}°C");
                    }
                }
            }

            // 【V1.66】超温全线联停：烧屏架 72 台 24h 点亮，超温是火灾级风险。
            // 开关 FanTempShutdownEnabled 默认 false = 现状（只记日志）；现场答完 Q16 打开即生效，
            // 不用二次开发。边沿触发：停过一次后必须回温（≤上限）才允许再停，避免每秒重复停；
            // StopTesting 空数组是 no-op，无在测时只记一行日志。
            if (AgingSequencer.IsFanOverTempShutdown(
                data.Temperature, _config.FanTempAlarmLimitC, _config.FanTempShutdownEnabled))
            {
                if (!_fanShutdownTriggered)
                {
                    _fanShutdownTriggered = true;
                    int[] testing = _deviceManager.GetTestingDeviceIds();
                    _deviceManager.StopTesting(testing);
                    WriteLog($"[送风机] 上部温度 {data.Temperature:F1}°C 超限，全线联停（{testing.Length} 台，" +
                        "手动复位/重启动前请先排查温控）");
                }
            }
            else
            {
                _fanShutdownTriggered = false;
            }
        }

        /// <summary>
        /// 已记录过的送风机温度告警阈值（避免重复写日志）
        /// </summary>
        private float _fanTempAlarmLoggedThreshold = 0f;

        /// <summary>
        /// 温度传感器无效边沿锁（【大扫荡】NaN 记一次，恢复有效即复位）。
        /// </summary>
        private bool _fanTempInvalidLogged = false;

        /// <summary>
        /// 超温全线联停边沿锁（【V1.66】）：true=本次超温已停过，回温（≤上限）后自动复位 false。
        /// 避免超温期间每秒重复执行 StopTesting + 刷屏写日志。
        /// </summary>
        private bool _fanShutdownTriggered = false;

        /// <summary>
        /// 更新右侧整机状态汇总（【V1.10 新增】）
        /// 在批量数据更新后调用（UI 线程）：
        /// - 顶部运行状态：空闲 / 测试中(N台) / 有报警
        /// - 状态栏：测试中 N 台、在线 M/72 台
        /// </summary>
        private void UpdateRunStatusSummary()
        {
            if (this.IsDisposed) return;

            // 从设备管理器获取聚合数据
            bool[] testingStates = _deviceManager.GetTestingStates();
            int testingCount = 0;
            int alarmCount = 0;
            for (int i = 0; i < testingStates.Length; i++)
            {
                if (testingStates[i]) testingCount++;
            }

            BarometerData[] allData = _deviceManager.GetAllBarometerData();
            if (allData != null)
            {
                foreach (var d in allData)
                {
                    if (d != null && d.Status == DeviceStatus.Fault) alarmCount++;
                }
            }

            int onlineCount = _deviceManager.GetOnlineCount();

            // 顶部运行状态文本
            if (alarmCount > 0)
            {
                _runStatus = $"报警 {alarmCount} 台";
                lblRunStatus.Text = _runStatus;
                lblRunStatus.ForeColor = Color.Red;
            }
            else if (testingCount > 0)
            {
                _runStatus = $"测试中 {testingCount} 台";
                lblRunStatus.Text = _runStatus;
                lblRunStatus.ForeColor = Color.DarkOrange;
            }
            else
            {
                _runStatus = "空闲";
                lblRunStatus.Text = _runStatus;
                lblRunStatus.ForeColor = Color.Green;
            }

            // 状态栏统计
            toolStripStatusLabelTesting.Text = $"测试中: {testingCount}";
            toolStripStatusLabelOnline.Text = $"在线: {onlineCount}/{_config.TotalBarometers}";

            // 【V1.24】全部离线（在线 0/N）时"在线"文本标红，其余情况恢复默认颜色
            toolStripStatusLabelOnline.ForeColor = onlineCount == 0
                ? Color.Red
                : SystemColors.ControlText;
        }

        /// <summary>
        /// 设备管理器连接状态变更事件处理
        /// </summary>
        private void DeviceManager_OnConnectionStatusChanged(object sender, bool isConnected)
        {
            // 【V1.72.15】退出期丢弃（此回调无 try，关后 UpdateConnectionStatus 虽自拦，
            // 但 _commConnected 写脏无妨，直接早退最干净）。
            if (_mainClosing || this.IsDisposed || this.Disposing) return;
            _commConnected = isConnected;
            UpdateConnectionStatus();
        }

        /// <summary>
        /// 设备管理器启动/连接诊断事件处理（【V1.16 新增】）
        /// 把连接诊断（实际气压表串口、耦合器连接结果、送风机连接结果、自动重连成功等）
        /// 写到 LOG 面板，让现场一眼看到"到底哪一步连不上"。
        /// 【线程安全】后台线程触发，用 BeginInvoke 切回 UI 线程写日志。
        /// </summary>
        private void DeviceManager_OnDiagnostic(object sender, string message)
        {
            // 窗体已释放或正在释放时直接返回
            // 【V1.72.15】加 _mainClosing（同 BatchData）。
            if (_mainClosing || this.IsDisposed || this.Disposing) return;

            try
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action<string>(WriteLog), message);
                }
                else
                {
                    WriteLog(message);
                }
            }
            catch (ObjectDisposedException)
            {
                // 窗体已释放，忽略
            }
            catch (InvalidOperationException)
            {
                // 窗体在 BeginInvoke 前刚好释放，忽略
            }
        }

        /// <summary>
        /// 更新顶部"通讯模块状态"显示
        /// 【V1.16.1】语义 = IO 耦合器（阀 / 载台电控制）是否连接，不再反映气压表串口。
        /// 数据来源 _commConnected（由 DeviceManager.OnConnectionStatusChanged 事件驱动）。
        /// 【修复 H1】增加 IsDisposed 检查，使用 BeginInvoke 异步切换
        /// </summary>
        private void UpdateConnectionStatus()
        {
            // 窗体已释放或正在释放时直接返回
            // 【V1.72.15】加 _mainClosing（排队回调关后丢弃，与通讯窗 SetConnected 同因）。
            if (_mainClosing || this.IsDisposed || this.Disposing) return;

            try
            {
                // 跨线程安全检查
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action(UpdateConnectionStatus));
                    return;
                }

                lblCommStatus.Text = _commConnected ? "已连接" : "未连接";
                lblCommStatus.ForeColor = _commConnected ? Color.Green : Color.Red;
            }
            catch (ObjectDisposedException)
            {
                // 窗体已释放，忽略
                // 【注意】ObjectDisposedException 继承自 InvalidOperationException，
                // 必须先 catch 子类异常，否则会被父类 catch 提前捕获（CS0160 编译错误）
            }
            catch (InvalidOperationException)
            {
                // 窗体释放中，忽略
            }
        }

        /// <summary>
        /// 更新状态栏信息
        /// 显示设备数量和采集间隔
        /// </summary>
        private void UpdateStatusBar()
        {
            toolStripStatusLabelDeviceCount.Text = $"设备数量: {_config.TotalBarometers}";
            toolStripStatusLabelInterval.Text = $"采集间隔: {_config.CollectInterval}ms";
        }

        /// <summary>
        /// 面板"设置"按钮点击事件处理（【V1.18】由单台手动控制改为工位设置窗口）
        /// 【V1.24 优化】点击"设置"按钮时：
        /// 1. 若被点击按钮所在的工位未被选中，先将其加入选中集合（确保点击的工位必被选中）；
        /// 2. 再按当前选中数量决定弹出窗口：
        ///    - 选中 2 个及以上工位：弹出批量设置配方窗口（BatchRecipeForm）；
        ///    - 只选中 1 个工位：弹出该选中工位的工位设置窗口（StationSettingsForm）。
        /// 【V1.50】选中状态统一由 <see cref="WorkstationGridView"/> 内部维护。
        /// </summary>
        private void Panel_OnSetClicked(object sender, int deviceId)
        {
            // 确保被点击"设置"按钮所在的工位被选中
            _gridView.SetSelected(deviceId, true);

            int[] selectedIds = _gridView.GetSelectedDeviceIds();

            // 选中 2 个及以上工位 → 批量设置配方窗口
            if (selectedIds.Length >= 2)
            {
                ShowBatchRecipeForm();
                return;
            }

            // 只选中 1 个工位（此时必为被点击的工位）→ 打开该工位的设置窗口
            // 传入共享配方列表 _recipes，供"保存/加入对列"把当前配方写入本地配方存储
            int selectedDeviceId = selectedIds.Length == 1 ? selectedIds[0] : deviceId;
            using (var form = new StationSettingsForm(_deviceManager, _config, _recipes, selectedDeviceId))
            {
                // 【V1.60】子窗体打开前按当前主题着色（以下各 ShowDialog/Show 处同，不再重复解释）
                ThemeManager.ApplyTo(form);
                form.ShowDialog(this);
            }
        }

        /// <summary>
        /// "连接中..."提示窗体（【V1.16.2 新增】）
        /// 异步按需重连期间显示：告诉操作员正在连接哪个设备，同时禁用主窗体，
        /// 防止连接期间重复点击其它按钮造成并发连接。
        /// </summary>
        private Form _connectingForm;

        /// <summary>
        /// 显示"连接中..."提示并禁用主窗体（【V1.16.2 新增】）
        /// 仅在异步重连开始时调用；结束时由 <see cref="HideConnecting"/> 恢复。
        /// </summary>
        /// <param name="deviceName">设备名（如"耦合器"/"送风机"），用于提示文案</param>
        private void ShowConnecting(string deviceName)
        {
            // 【V1.72.15】退出中不再建提示窗（异步重连收尾与退出并行时，建了即孤儿）。
            if (_mainClosing || this.IsDisposed || this.Disposing) return;
            if (_connectingForm != null) return;

            _connectingForm = new Sunny.UI.UIForm
            {
                // 【屏幕居中】以前 CenterParent 跟着主窗走，主窗拖到副屏角落时
                // 提示框也贴边；现改 CenterScreen，永远在屏幕正中，现场一眼看到。
                StartPosition = FormStartPosition.CenterScreen,
                // UIForm 自己管边框，不设 FormBorderStyle（家规，设了白设）。
                ControlBox = false,           // 不显示关闭按钮（连接期间不可取消）
                ShowInTaskbar = false,
                // 【高度含标题】UIForm 自绘蓝标题占顶部约 35px 客户区：
                // 原生窗 80 高刚好，Sunny 窗要加高到 115，文字才不顶标题。
                ClientSize = new Size(300, 115),
                Text = "连接中"
            };
            _connectingForm.Controls.Add(new Sunny.UI.UILabel
            {
                Text = $"正在连接{deviceName}，请稍候...",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            });

            // 禁用主窗体，防止连接期间重复点击（连接只影响自身，后台采集照常进行）
            this.Enabled = false;
            // 【V1.60】提示窗跟随当前主题
            ThemeManager.ApplyTo(_connectingForm);
            _connectingForm.Show(this);
        }

        /// <summary>
        /// 关闭"连接中..."提示并恢复主窗体（【V1.16.2 新增】）
        /// 在 async 方法的 finally 中调用，保证无论成功失败都恢复。
        /// </summary>
        private void HideConnecting()
        {
            if (_connectingForm != null)
            {
                try { _connectingForm.Close(); } catch { /* 窗体已关闭则忽略 */ }
                // 【V1.72.15】Dispose 包 try：Close 竞态后 Dispose 再抛会连带炸 finally 链。
                try { _connectingForm.Dispose(); } catch { }
                _connectingForm = null;
            }

            // 主窗体可能正在被关闭（用户点了退出），此时不能再操作
            if (!this.IsDisposed && !this.Disposing)
            {
                this.Enabled = true;
            }
        }

        /// <summary>
        /// 确保 IO 耦合器已连接（【V1.16.2】完全异步版，替代原同步 EnsureIoReady）
        /// 用户操作需要耦合器时先调用：未连接则后台异步重连一次，
        /// 期间弹"正在连接耦合器..."提示（不卡界面）；连不上则弹窗提示并返回 false。
        /// </summary>
        /// <returns>true = 耦合器可用，可继续执行操作</returns>
        private async Task<bool> EnsureIoReadyAsync()
        {
            // 已连接直接可用（无任何等待/提示）
            if (_deviceManager.IsIoConnected) return true;

            ShowConnecting("耦合器");
            bool ok;
            try
            {
                ok = await Task.Run(() => _deviceManager.EnsureIoConnected());
            }
            catch (Exception ex)
            {
                // 防御：连接实现内部已捕获异常，正常情况下不会走到这里
                WriteLog($"耦合器连接异常: {ex.Message}");
                ok = false;
            }
            finally
            {
                HideConnecting();
            }

            if (!ok)
            {
                // 【SunnyUI 弹窗】操作区提示统一 Sunny 蓝标题风格，警告类用 Orange。
                Sunny.UI.UIMessageBox.Show("耦合器未连接，请先连接（阀/载台上电等操作暂不可用）", "提示",
                    Sunny.UI.UIStyle.Orange, Sunny.UI.UIMessageBoxButtons.OK, true, 0);
            }
            return ok;
        }

        /// <summary>
        /// 确保送风机已连接（【V1.16.2】异步版）
        /// 需要送风机的操作（定值启动/停止、启动测试）先调用：未连接则后台异步重连一次，
        /// 期间弹"正在连接送风机..."提示；连不上返回 false（由调用方决定是否阻断/提示）。
        /// </summary>
        /// <returns>true = 送风机可用</returns>
        private async Task<bool> EnsureFanReadyAsync()
        {
            // 未启用送风机（App.config FanEnabled=false）：不弹"连接中"，直接按不可用处理
            if (!_deviceManager.IsFanEnabled) return false;
            if (_deviceManager.IsFanConnected) return true;

            ShowConnecting("送风机");
            bool ok;
            try
            {
                ok = await Task.Run(() => _deviceManager.ReconnectFan());
            }
            catch (Exception ex)
            {
                WriteLog($"送风机连接异常: {ex.Message}");
                ok = false;
            }
            finally
            {
                HideConnecting();
            }
            return ok;
        }

        #region 顶部菜单按钮事件处理（显示下拉菜单）

        /// <summary>
        /// 用户权限按钮点击 → 显示用户权限下拉菜单
        /// 菜单项：操作员 / 技术员 / 管理员
        /// 【新增】如果当前是管理员权限，额外显示"用户管理"菜单项
        /// </summary>
        private void btnUserPermission_Click(object sender, EventArgs e)
        {
            // 构建菜单项列表
            var items = new List<(string Text, EventHandler ClickHandler)>
            {
                ("操作员", MenuPermissionOperator_Click),
                ("技术员", MenuPermissionTechnician_Click),
                ("管理员", MenuPermissionAdmin_Click)
            };

            // 【新增】如果当前已登录管理员，追加"用户管理"选项
            if (_userManager.CurrentUser != null &&
                _userManager.CurrentUser.Role == UserRole.Administrator)
            {
                items.Add(("用户管理", MenuPermissionUserManagement_Click));
            }

            // 【新增】任意已登录角色可修改自己的密码（操作员/技术员/管理员）
            if (_userManager.CurrentUser != null)
            {
                items.Add(("修改密码", MenuPermissionChangePassword_Click));
            }

            ShowDropdownPopup(btnUserPermission, items.ToArray());
        }

        /// <summary>
        /// 参数设置按钮点击 → 显示参数设置下拉菜单
        /// 菜单项：公共参数 / 配方管理 / 项目切换（【V1.67 新增】管理员限定，见 MenuParamProject_Click）
        ///
        /// 【无权限点后提示】按钮常亮可点（见 UpdateButtonPermissionStates）：
        /// 操作员点了不直接弹菜单，先拦一道"权限不够"明示去哪提权，
        /// 现场不再是"点了没反应、以为卡死"。下拉里的敏感项（项目切换仅管理员）
        /// 各自二次校验，不因入口放行而漏防。
        /// </summary>
        private void btnParameter_Click(object sender, EventArgs e)
        {
            string denied = GetParameterDeniedMessage(_userManager.HasPermission(UserRole.Technician));
            if (!string.IsNullOrEmpty(denied))
            {
                // 【SunnyUI 弹窗】全软件弹窗统一 Sunny 蓝标题风格（通讯测试窗先例）：
                // 警告类用 Orange（与"请先点击连接测试"同级），不走系统 MessageBox。
                Sunny.UI.UIMessageBox.Show(denied, "提示",
                    Sunny.UI.UIStyle.Orange, Sunny.UI.UIMessageBoxButtons.OK, true, 0);
                return;
            }
            ShowDropdownPopup(btnParameter, new (string, EventHandler)[]
            {
                ("公共参数", MenuParamCommon_Click),
                ("配方管理", MenuParamRecipe_Click),
                ("工艺策略", MenuParamPolicy_Click),
                ("项目切换", MenuParamProject_Click)
            });
        }

        /// <summary>
        /// 参数设置入口权限文案（【新增】纯函数，可单测，不碰任何控件）。
        ///
        /// 【为什么抽出来】权限判定本身只有一句话（有技术员及以上放行），
        /// 但"无权限时说什么"值得锁死：必须含"权限不够"四字（用户原话），
        /// 并指明去【用户权限】提权，现场才知道下一步干什么。
        /// 有权限返回 ""（放行），无权限返回提示语（调用方弹框）。
        /// </summary>
        /// <param name="hasTechnicianPermission">是否有技术员及以上权限（HasPermission(Technician) 结果）</param>
        /// <returns>""=放行；否则=弹框文案</returns>
        public static string GetParameterDeniedMessage(bool hasTechnicianPermission)
        {
            if (hasTechnicianPermission) return "";
            return "权限不够，参数设置需要技术员及以上权限，请先在【用户权限】中切换。";
        }

        /// <summary>
        /// 工艺策略 → 弹出可视化工艺配置窗体（【V1.70 新增为"流程驾驶舱"，V1.73 改名】点节点改配置，
        /// 与系统设置同一条保存路； savedKeys 非空走同样的热生效分发）。
        /// 入口挂在参数设置下拉下（技术员及以上可见；编辑限管理员——
        /// 只读看图所有人可看，改配置与系统设置同级，不开后门）。
        /// </summary>
        private void MenuParamPolicy_Click(object sender, EventArgs e)
        {
            bool canEdit = _userManager.HasPermission(UserRole.Administrator);
            using (var form = new ProcessPolicyForm(_config, _deviceManager, canEdit))
            {
                ThemeManager.ApplyTo(form);
                if (form.ShowDialog(this) == DialogResult.OK &&
                    form.SavedKeys != null && form.SavedKeys.Count > 0)
                {
                    ApplySettingsHotReload(form.SavedKeys);
                }
            }
        }

        /// <summary>
        /// 项目切换 → 弹出项目档案窗体（【V1.67 新增】仅管理员：项目=工艺归属，
        /// 切错项目=跑错工艺，所以与系统设置同级管控；技术员/操作员点此直接提示）。
        /// 【V1.72.10 热更】窗体只改指针并带回项目名，真正的换装在这里做
        /// （ReloadActiveProject：配方/工位设置/策略/布局即时生效，无需重启）。
        /// </summary>
        private void MenuParamProject_Click(object sender, EventArgs e)
        {
            if (!_userManager.HasPermission(UserRole.Administrator))
            {
                MessageBox.Show("项目切换仅管理员可用，请先在【用户权限】中切换为管理员权限。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            using (var form = new ProjectSwitchForm(() => _deviceManager.GetTestingDeviceIds().Length))
            {
                ThemeManager.ApplyTo(form);
                if (form.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(form.SwitchedProjectName))
                {
                    ReloadActiveProject(form.SwitchedProjectName.Trim());
                }
            }
        }

        /// <summary>
        /// 项目热加载（【V1.72.10 新增】切换项目后即时生效，无需重启）。
        ///
        /// 【为什么以前必须重启】跟项目走的四个文件（配方/工位设置/主页布局/策略）
        /// 在启动时一次性读进内存（_recipes 列表/StationSettingsCache/DeviceConfig/
        /// 当前布局），运行中只改 ActiveProject 指针，内存还是旧项目的数据，
        /// 继续跑就会"一半读旧目录一半读新目录"。本方法把启动时那一套加载动作
        /// 原样重放一遍，数据源换成新项目：
        /// 1) 在测复查（双保险：切换窗已拦，这里再查一次，防竞态）；
        /// 2) 暂停主采集（拷配置时不让采集线程读到"半新半旧"）；
        /// 3) LoadConfig 重读（App.config 机器缺省 + 新项目 Policy.json 叠加）
        ///    → CopyFrom 就地换血（_config 是 readonly 引用，DeviceManager/MES
        ///    持同一别名，就地拷才都生效；换引用只换自己手里的）；
        /// 4) StationSettingsCache.Reload（丢掉旧项目回填，读新项目文件）；
        /// 5) DeviceManager.ClearProjectScopedState（清旧工位指派 + 规则计时）；
        /// 6) LoadRecipes（同一 List 就地换数据，自动完成源引用不变）；
        /// 7) ApplyHomeLayout（新项目布局立即重排）+ 顶栏项目名刷新；
        /// 8) 恢复采集 + 立即刷一帧面板（不等下个 1 秒周期，SN/配方当场清空）。
        /// 【不动什么】硬件连接不断（串口/耦合器/送风机/扫码枪是跟机器的）；
        /// 结构型配置（设备数/Mock 等）本就跟机器，LoadConfig 读出来与原来一致。
        /// 失败兜底：任何一步抛异常 → 恢复采集 + 提示"热加载失败请重启"，
        /// 不让程序停在"采集停了但数据没换完"的半截状态。
        /// </summary>
        /// <param name="newName">刚切换到的项目名（ProjectSwitchForm.SwitchedProjectName）</param>
        private void ReloadActiveProject(string newName)
        {
            // 1) 在测复查：跑中任务禁切（窗体里拦过，这里防"选完到点切换之间刚好点了启动"）
            int testing = 0;
            try { testing = _deviceManager.GetTestingDeviceIds().Length; }
            catch { testing = 0; }
            if (testing > 0)
            {
                MessageBox.Show($"指针已指到项目 [{newName}]，但当前有 {testing} 台在测，" +
                    "热加载已取消（防跑中任务读劈叉）。\n请等待完成/停止并复位后重新切换。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                WriteLog($"[项目切换] 热加载取消：在测 {testing} 台，指针={newName}");
                return;
            }

            // 2) 暂停主采集（记住原状态，finally 按原样恢复）
            bool wasRunning = false;
            try { wasRunning = _deviceManager.PauseCollection(); }
            catch { wasRunning = false; }

            try
            {
                // 3) 配置换血：重读（含新项目 Policy 叠加）→ 就地拷进 _config
                int oldStations = _config.TotalBarometers;
                DeviceConfig fresh = LoadConfig();
                _config.CopyFrom(fresh);

                // 4) 工位回填缓存：读新项目 StationSettings.json
                StationSettingsCache.Reload();

                // 5) 编排层项目状态：清旧指派 + 规则计时（idle 台，无硬件动作）
                _deviceManager.ClearProjectScopedState();

                // 【大扫荡 5.5】工位数变了→重建状态数组+规则计时+采集间隔：
                // 以前只 CopyFrom 换血，数组还是旧尺寸——调大采集越界、调小尾部够不到。
                // （TotalBarometers 跟机器，正常切项目不变；防手改 App.config 后切项目。）
                if (_config.TotalBarometers != oldStations)
                {
                    _deviceManager.RebuildStationArrays();
                    WriteLog($"[项目切换] 工位数 {oldStations}→{_config.TotalBarometers}，状态数组已重建");
                }

                // 【大扫荡 5.6】策略组合重验：组合键分两文件（开关跟项目/限值跟机器），
                // 切项目可拼出"联停开但上限0"脏组合；保存时拦过，热更这里只告警不断线。
                try
                {
                    string comboWarn = Dialogs.SettingsForm.CheckPolicyCombination(_config, null);
                    if (!string.IsNullOrEmpty(comboWarn))
                        WriteLog("[项目切换] 策略组合告警（新项目与机器限值矛盾，请检查）：" + comboWarn);
                }
                catch { /* 校验本身永不阻断热更 */ }

                // 6) 配方列表：同一 List 就地换（自动完成源/各录入窗引用不变）
                LoadRecipes();

                // 7) 主页布局 + 顶栏项目名
                ApplyHomeLayout();
                UpdateProjectDisplay(newName);

                // 8) 立即刷一帧（SN/配方当场换新，不等下个采集周期）
                try { UpdateAllPanels(_deviceManager.GetAllBarometerData()); }
                catch { /* 刷帧失败不影响切换，下个周期自动刷 */ }

                WriteLog($"[项目切换] 已热加载项目 [{newName}]（配方/工位设置/策略/布局即时生效，无需重启）");
                MessageBox.Show($"已切换到项目 [{newName}]，配方/策略/布局已即时生效，无需重启。",
                    "项目切换", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                WriteLog($"[项目切换] 热加载项目 [{newName}] 失败：{ex.Message}（采集已恢复，建议重启程序）");
                MessageBox.Show($"切换到项目 [{newName}] 后热加载失败：{ex.Message}\n" +
                    "为防新旧数据混用，请重启程序。",
                    "项目切换", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                try { _deviceManager.ResumeCollection(wasRunning); }
                catch { /* 恢复失败下个周期自愈（定时器自启逻辑在 DeviceManager 内部） */ }
            }
        }

        /// <summary>
        /// 日志记录按钮点击 → 显示日志记录下拉菜单
        /// 菜单项：历史记录
        /// </summary>
        private void btnLog_Click(object sender, EventArgs e)
        {
            ShowDropdownPopup(btnLog, new (string, EventHandler)[]
            {
                ("历史记录", MenuLogHistory_Click)
            });
        }

        /// <summary>
        /// 关于按钮点击 → 显示下拉菜单（V1.19.12 更名：btnHelp_Click → btnAbout_Click）
        /// 菜单项：
        /// - 设置：仅管理员可见（V1.17 权限控制，非管理员自动隐藏）
        /// - 版本说明：所有权限可见（V1.19.12 更名：关于 → 版本说明）
        /// - 深浅模式切换：仅 dev 最高权限可见（V1.64 起从顶部独立按钮收进这里）
        /// </summary>
        private void btnAbout_Click(object sender, EventArgs e)
        {
            var items = new List<(string Text, EventHandler ClickHandler)>();

            // 【V1.17 权限控制】"系统设置"只对管理员开放，非管理员时该菜单项直接隐藏
            if (_userManager.HasPermission(UserRole.Administrator))
            {
                items.Add(("设置", MenuHelpSettings_Click));
            }

            // 【V1.58】"主页区域调整"：可视化拖动矩形块边缘调整主界面各区域尺寸。
            // 【V1.58.3】权限放开：所有登录用户可见可用（布局微调属非关键操作，
            // 现场操作员也可能需要按自己习惯微调右侧宽度/行高，故不再限制管理员）。
            items.Add(("主页区域调整", MenuHelpHomeLayout_Click));

            // 【通讯测试】仅技术员及以上权限可见（操作员不可见）
            if (_userManager.HasPermission(UserRole.Technician))
            {
                items.Add(("通讯测试", MenuHelpCommunicationTest_Click));
            }

            // 【送风机测试】仅技术员及以上权限可见（操作员不可见）
            if (_userManager.HasPermission(UserRole.Technician))
            {
                items.Add(("送风机测试", MenuHelpFanTest_Click));
            }

            items.Add(("版本说明", MenuHelpVersionInfo_Click));

            // 【V1.83】软件授权：所有人可看状态，导入仅管理员（窗体内二次限）。
            items.Add(("软件授权", MenuHelpLicense_Click));

            // 【V1.64】深浅模式切换收进"关于"下拉，只给 dev 看：
            // 普通用户菜单里根本没这一项（隐藏效果）；文字永远表示"下一次去哪"，
            // 菜单每次打开现拼，天然就是最新状态，不用像以前的顶部按钮那样同步文字
            if (_userManager.IsDevLoggedIn)
            {
                items.Add((ThemeManager.IsDark ? "浅色模式" : "深色模式", MenuThemeToggle_Click));
            }

            ShowDropdownPopup(btnAbout, items.ToArray());
        }

        /// <summary>
        /// 深色/浅色主题切换（【V1.64】入口从顶部独立按钮收进"关于"下拉，仅 dev 可见）。
        ///
        /// 【流程】ThemeManager.Toggle（内存切换 + 写 App.config 下次启动接着用）
        /// → ApplyToAllOpenForms（主窗体 + 所有已打开的子窗体/自绘画布当场换肤）
        /// → 停止/复位按钮配色跟上 → 写 LOG。
        /// 注意：各子窗体打开前本来就 ApplyTo 过（见各 ShowDialog/Show 处），
        /// 这里全刷一次是为了"非模态窗（通讯测试/送风机测试）开着时切换也能即时跟上"。
        /// </summary>
        private void MenuThemeToggle_Click(object sender, EventArgs e)
        {
            AppThemeMode mode = ThemeManager.Toggle();
            ThemeManager.ApplyToAllOpenForms();
            ApplyOperationButtonsTheme();
            WriteLog(mode == AppThemeMode.Dark ? "已切换为深色模式" : "已切换为浅色模式");
        }

        /// <summary>
        /// 停止/复位两按钮的主题配色（【V1.60.1】纯函数，方便回归直接断言）。
        /// 这两个按钮浅色下是系统默认灰底黑字；深色下按需求走深灰底白字
        /// （DimGray + White，跟登录窗/改密窗"取消"按钮同款，全软件统一）。
        /// 其它语义按钮（绿/蓝/红）两边都不动，不走这里。
        /// </summary>
        /// <param name="dark">true=深色配色，false=浅色配色</param>
        /// <param name="back">按钮底色</param>
        /// <param name="fore">按钮文字色</param>
        public static void GetOperationButtonThemeColors(bool dark, out Color back, out Color fore)
        {
            back = dark ? Color.DimGray : SystemColors.Control;
            fore = dark ? Color.White : SystemColors.ControlText;
        }

        /// <summary>
        /// 把主题配色应用到"停止运行/报警复位/下料判定"三个无语义灰按钮（【V1.60.1】，V1.67 加下料判定）。
        /// 为什么只有它俩特殊：ThemeManager 对按钮一律不动（语义色保护），
        /// 但它俩浅色是"无语义的默认灰"，深色下黑字偏弱，才单独提出来处理。
        /// 启动与每次主题切换后调用（切换入口只有 MenuThemeToggle_Click 一处，不会漏）。
        /// </summary>
        private void ApplyOperationButtonsTheme()
        {
            Color back;
            Color fore;
            GetOperationButtonThemeColors(ThemeManager.IsDark, out back, out fore);
            if (btnStopRun != null)
            {
                ThemeManager.ApplyButtonColors(btnStopRun, back, fore);
            }
            if (btnResetAlarm != null)
            {
                ThemeManager.ApplyButtonColors(btnResetAlarm, back, fore);
            }
            // 【V1.67】下料判定同为无语义默认灰，随它俩一起换肤
            if (btnUnloadJudge != null)
            {
                // 【V1.71】按钮已换 Sunny：BackColor 画不出来，走 ApplyButtonColors。
                ThemeManager.ApplyButtonColors(btnUnloadJudge, back, fore);
            }
        }

        #endregion

        #region 下拉菜单项点击事件处理

        #region 用户权限菜单项

        /// <summary>
        /// 切换为操作员权限
        /// 【实现】弹出 LoginForm 让用户输入操作员账号密码
        /// </summary>
        private void MenuPermissionOperator_Click(object sender, EventArgs e)
        {
            TryLoginAndSwitchPermission(UserRole.Operator);
        }

        /// <summary>
        /// 切换为技术员权限
        /// 【实现】弹出 LoginForm 让用户输入技术员账号密码
        /// </summary>
        private void MenuPermissionTechnician_Click(object sender, EventArgs e)
        {
            TryLoginAndSwitchPermission(UserRole.Technician);
        }

        /// <summary>
        /// 切换为管理员权限
        /// 【实现】弹出 LoginForm 让用户输入管理员账号密码
        /// </summary>
        private void MenuPermissionAdmin_Click(object sender, EventArgs e)
        {
            TryLoginAndSwitchPermission(UserRole.Administrator);
        }

        /// <summary>
        /// 【新增】用户管理菜单项点击事件
        /// 仅管理员可见，弹出 UserManagementForm 修改操作员/技术员账号
        /// </summary>
        private void MenuPermissionUserManagement_Click(object sender, EventArgs e)
        {
            using (var form = new UserManagementForm(_userManager))
            {
                ThemeManager.ApplyTo(form);
                form.ShowDialog(this);
            }
        }

        /// <summary>
        /// 【新增】修改密码菜单项点击事件
        /// 任意已登录角色可见，弹出 ChangePasswordForm 修改当前用户自己的密码
        /// </summary>
        private void MenuPermissionChangePassword_Click(object sender, EventArgs e)
        {
            using (var form = new ChangePasswordForm(_userManager))
            {
                ThemeManager.ApplyTo(form);
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    // 密码修改成功，若已记住该角色登录信息则已被自动清除，写日志提示
                    WriteLog($"用户 {_userManager.CurrentUser?.Username} 修改了自己的密码");
                }
            }
        }

        /// <summary>
        /// 【新增】尝试登录并切换权限
        ///
        /// 【流程】
        /// 1. 弹出 LoginForm 让用户输入用户名和密码
        /// 2. 用户点击"确认"后，UserManager 校验账号密码
        /// 3. 校验成功：切换权限标签，更新按钮可用状态，写入日志
        /// 4. 校验失败：LoginForm 内部弹出错误提示，用户可重试
        /// 5. 用户点击"取消"：不做任何操作
        ///
        /// 【特别说明 - 操作员权限】
        /// 操作员权限允许任意用户切换（视为"注销当前用户"），
        /// 因此选择"操作员"时不强制要求登录，直接弹出登录框但允许取消。
        /// 如果用户取消，则恢复为未登录状态（操作员权限）。
        /// </summary>
        /// <param name="targetRole">目标角色</param>
        private void TryLoginAndSwitchPermission(UserRole targetRole)
        {
            using (var loginForm = new LoginForm(_userManager, targetRole))
            {
                ThemeManager.ApplyTo(loginForm);
                DialogResult result = loginForm.ShowDialog(this);

                if (result == DialogResult.OK)
                {
                    // 登录成功，更新权限显示
                    string roleName = GetRoleDisplayName(targetRole);
                    // 【V1.64】dev 登录成功后顶栏明示最高身份（红色），和普通管理员一眼区分
                    if (_userManager.IsDevLoggedIn)
                    {
                        roleName = "最高权限(dev)";
                    }
                    _currentPermission = roleName;
                    // V1.19.7：角色名着色（管理员=红/技术员=蓝/操作员=绿；V1.47 技术员蓝色加深）
                    UpdatePermissionDisplay(roleName);

                    // 更新按钮可用状态（根据权限启用/禁用）
                    UpdateButtonPermissionStates();

                    // 写入日志
                    WriteLog($"权限切换为: {roleName}（用户: {_userManager.CurrentUser?.Username}）");
                }
                else
                {
                    // 用户点击取消，不切换权限
                    WriteLog($"取消切换为 {GetRoleDisplayName(targetRole)} 权限");
                }
            }
        }

        /// <summary>
        /// 【新增】获取角色的中文显示名
        /// </summary>
        /// <param name="role">角色枚举</param>
        /// <returns>中文名（操作员/技术员/管理员）</returns>
        private string GetRoleDisplayName(UserRole role)
        {
            switch (role)
            {
                case UserRole.Operator:
                    return "操作员";
                case UserRole.Technician:
                    return "技术员";
                case UserRole.Administrator:
                    return "管理员";
                default:
                    return role.ToString();
            }
        }

        /// <summary>
        /// 顶栏与右侧状态区文本加粗（【V1.78 新增】用户点名：顶栏项目/权限/通讯、
        /// 运行状态组、监视组全部加粗，更醒目）。
        ///
        /// 【为什么放代码里而不写 Designer】Sunny 控件默认 Style=Inherited，吃样式字体：
        /// 名/值两套标签要么都不写 Font（同源永不分叉），要么两边写死同一套——只写一边必大小眼
        /// （V1.72.8 血泪：lblRunStatus 单写微软雅黑 10F，删字解决；V1.72.9 顶栏五段字体全删回默认）。
        /// 这里按各控件"当前实际字号"原样加粗（字族/字号一个不动，只或上 Bold），
        /// 成对的名/值标签永远同源同尺寸，不会分叉。ThemeManager 不碰 Font，加粗一次永久有效，
        /// 深色/浅色切换不影响粗细。
        /// </summary>
        private void ApplyHeaderBoldFonts()
        {
            // 顶栏：项目名（值加粗） + 权限角色名 + 通讯状态值。
            // 【V1.79】三个前缀/标签（lblProjectPrefix/lblPermissionPrefix/lblCommStatusLabel）
            // 保持常规体——"前缀常规、值加粗"，主次分明（用户点名）。
            SetBold(lblProject);
            SetBold(lblPermissionRole);
            SetBold(lblCommStatus);
            // 运行状态组：分组标题"运行状态" + 状态文本
            SetBold(groupBoxStatus);
            SetBold(lblRunStatus);
            // 监视组：分组标题"监视" + 名/值三对（当前温度/设置温度/送风机状态）
            SetBold(groupBoxMonitor);
            SetBold(lblUpperTempLabel);
            SetBold(lblUpperTemp);
            SetBold(lblSetTempLabel);
            SetBold(lblSetTemp);
            SetBold(lblFanStateLabel);
            SetBold(lblFanState);
        }

        /// <summary>
        /// 单个控件按原字号加粗（幂等：已是粗体不再重复创建字体对象）。
        /// 空控件/已释放时静默跳过（构造早期调用，防空引用拖垮启动）。
        /// </summary>
        /// <param name="c">要加粗的控件（Label/GroupBox 均可，Font 是 Control 基类属性）</param>
        private static void SetBold(Control c)
        {
            if (c == null || c.IsDisposed) return;
            if (c.Font == null) return;
            if ((c.Font.Style & FontStyle.Bold) != 0) return;
            c.Font = new Font(c.Font, c.Font.Style | FontStyle.Bold);
        }

        /// <summary>
        /// 更新权限显示（【V1.19.7】）
        /// 拆为"前缀 + 角色名"两个标签（panelPermission 内 FlowLayoutPanel 水平排列）：
        /// 前缀 lblPermissionPrefix 固定默认黑字；角色名 lblPermissionRole 按权限设置 ForeColor：
        /// - 管理员 → 红色（Red）
        /// - 最高权限(dev) → 红色（Red，V1.64：dev 登录后的顶栏身份，和普通管理员区分）
        /// - 技术员 → 深蓝色（RoyalBlue，V1.47 起由天蓝加深，更醒目）
        /// - 操作员 → 绿色（Green）
        /// - 未知角色 → 默认文字色
        /// </summary>
        /// <param name="roleName">角色中文名（操作员/技术员/管理员/最高权限(dev)）</param>
        private void UpdatePermissionDisplay(string roleName)
        {
            Color roleColor;
            switch (roleName)
            {
                case "管理员":
                case "最高权限(dev)":
                    roleColor = Color.Red;
                    break;
                case "技术员":
                    roleColor = Color.RoyalBlue;
                    break;
                case "操作员":
                    roleColor = Color.Green;
                    break;
                default:
                    roleColor = SystemColors.ControlText;
                    break;
            }

            lblPermissionRole.Text = roleName;
            lblPermissionRole.ForeColor = roleColor;
        }

        /// <summary>
        /// 顶栏显示当前项目（【V1.72.7 新增】切错项目=跑错工艺，首屏可见防呆）。
        /// 【V1.79】拆为"前缀 + 项目名"两个标签（pnlProject 内横排：lblProjectPrefix 固定"当前项目："
        /// 常规体，lblProject 只装项目名、加粗；与权限"前缀常规、值加粗"同口径）。
        /// panelPermission 用 FlowLayoutPanel 够用（两段都 AutoSize），这里必须用普通 Panel +
        /// 前缀 Dock=Left + 项目名 Dock=Fill——项目名要 AutoEllipsis，流式布局给不出约束宽度。
        /// 构造时设一次 + 每次热加载后刷新一次（两处调用，无需订阅事件）。
        /// 超长项目名由 lblProject.AutoEllipsis 省略号收尾不断行。
        /// </summary>
        /// <param name="projectName">生效的项目名（EnsureActiveProfile/热加载传入，null 兜底烧屏测试）</param>
        private void UpdateProjectDisplay(string projectName)
        {
            if (lblProject == null) return;
            lblProject.Text = string.IsNullOrWhiteSpace(projectName) ? "烧屏测试" : projectName.Trim();
        }

        /// <summary>
        /// 【新增】根据当前权限更新按钮可用状态
        ///
        /// 【权限规则】
        /// - 参数设置（btnParameter）：技术员或管理员可操作（包含配方管理）
        /// - 其他按钮：所有权限均可操作
        ///
        /// 【视觉效果→改为常亮可点】
        /// 以前操作员下 Enabled=false：禁用的 Sunny 按钮吞掉 Click，点了零反馈，
        /// 现场以为程序卡死。现改为常亮（Enabled=true），无权限点后弹"权限不够"
        /// （见 btnParameter_Click 首行拦截），有明确反馈；敏感子项各自二次校验。
        /// </summary>
        private void UpdateButtonPermissionStates()
        {
            // 常亮可点，无权限在点击时提示（原来 Enabled=false 点了没反应）。
            // 注意：不要再按权限置灰——置灰即回到"点了没反应"的老坑。
            btnParameter.Enabled = true;

            // 【预留】其他需要权限控制的按钮可在此处添加
            // TODO: 根据业务需求补充其他按钮的权限控制
        }

        #endregion

        #region 参数设置菜单项

        /// <summary>
        /// 公共参数窗口 → 弹出"设置所有气压表负压阈值"窗口
        ///
        /// 【V1.16 更新】公共参数窗口从"采集间隔+报警阈值"简化为"设置所有气压表负压阈值"：
        /// 传入 _deviceManager，由窗体在后台线程逐台写入气压表阈值寄存器（0x0010），
        /// 写入期间 DeviceManager 会暂停主采集定时器（避免与批量写争抢串口总线），
        /// 写入完成汇总成功/失败台数后返回。
        /// </summary>
        private void MenuParamCommon_Click(object sender, EventArgs e)
        {
            using (var form = new CommonParameterForm(_deviceManager))
            {
                // 【V1.60.4】走窗体自己的 ApplyTheme：整窗着色 + 保存按钮深色换灰底白字
                form.ApplyTheme();
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    WriteLog("所有气压表负压阈值设置完成");
                }
            }
        }

        /// <summary>
        /// 配方管理 → 弹出配方管理窗体
        /// 【V1.75】把生效配置传进去：显示模式开关与字典走它（开关关=该行隐藏）
        /// </summary>
        private void MenuParamRecipe_Click(object sender, EventArgs e)
        {
            using (var form = new RecipeManagerForm(_recipes, _config.AlarmPressureThresholdKPa, _config))
            {
                ThemeManager.ApplyTo(form);
                form.ShowDialog(this);
            }
        }

        /// <summary>
        /// 加载本地持久化的配方列表（Recipes.json）
        /// 由"配方管理"窗体的"保存设置"写入；文件不存在或加载失败时保持空列表
        /// </summary>
        private void LoadRecipes()
        {
            ApplyLoadedRecipes(_recipes, RecipeStorage.Load());
        }

        /// <summary>
        /// 把刚从文件读到的配方列表换装进内存（【V1.72.12 纯函数】启动与热加载共用）。
        ///
        /// 【修什么 bug】原来这里是 `loaded != null && loaded.Count > 0` 才换：
        /// 用户在 B 项目把配方删光（文件变成空数组 `[]`，Load 返回"非 null 空列表"），
        /// 切到别的项目再切回，空列表被条件拦掉、内存还留着上个项目的数据——
        /// "删掉的配方切一圈又回来了"。策略没这毛病（CopyFrom 全量覆盖无条件）。
        /// 【语义】内存永远等于文件：文件无/损坏（null）= 空项目，同样清空，
        /// 不留旧项目残留。target 为 null 防御性返回（调用方恒传 _recipes）。
        /// </summary>
        /// <param name="target">内存配方列表（就地换，引用不变，自动完成源不受影响）</param>
        /// <param name="loaded">刚读到的文件内容（null=无文件/损坏）</param>
        public static void ApplyLoadedRecipes(List<RecipeConfig> target, List<RecipeConfig> loaded)
        {
            if (target == null) return;
            target.Clear();
            if (loaded != null) target.AddRange(loaded);
        }

        #endregion

        #region LOG记录菜单项

        /// <summary>
        /// 历史记录 → 弹出历史记录查询窗体
        /// 【V1.75】报表列按钮按管理员权限传入（与系统设置同口径：操作员看不见按钮）
        /// </summary>
        private void MenuLogHistory_Click(object sender, EventArgs e)
        {
            bool canConfigure = false;
            try { canConfigure = _userManager.HasPermission(UserRole.Administrator); }
            catch { canConfigure = false; }
            using (var form = new HistoryRecordForm(canConfigure))
            {
                ThemeManager.ApplyTo(form);
                form.ShowDialog(this);
            }
        }

        #endregion

        #region 关于下拉菜单项（V1.19.12 更名：帮助 → 关于）

        /// <summary>
        /// 设置 → 弹出"系统设置"窗口，查看并编辑 App.config 中的全部配置项
        ///
        /// 【V1.17 权限控制】仅管理员可打开。菜单项在非管理员下已隐藏，
        /// 这里再加一道兜底校验，防止权限被绕过（如权限刚降级时窗口仍在）。
        /// </summary>
        private void MenuHelpSettings_Click(object sender, EventArgs e)
        {
            if (!_userManager.HasPermission(UserRole.Administrator))
            {
                MessageBox.Show("系统设置仅管理员可用，请先在【用户权限】中切换为管理员权限。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 【V1.65】把当前生效的右侧宽度传给设置窗体，供"主页区域"行显示/编辑用
            // （无 json 时为按窗口比例算出的值，有 json 时为文件绝对值，与主界面一致）。
            bool hasHomeCustom = System.IO.File.Exists(HomeLayoutConfig.GetConfigPath());
            int homeCustomWidth = hasHomeCustom ? HomeLayoutConfig.LoadOrDefault().RightPanelWidth : 0;
            int effectiveRightWidth = ComputeRightPanelWidth(splitContainerMain.Width, hasHomeCustom, homeCustomWidth);
            using (var form = new SettingsForm(_config, effectiveRightWidth))
            {
                ThemeManager.ApplyTo(form);
                if (form.ShowDialog(this) == DialogResult.OK &&
                    form.SavedKeys != null && form.SavedKeys.Count > 0)
                {
                    ApplySettingsHotReload(form.SavedKeys);
                }

                // 【V1.58】若在设置里改了主页布局（HomeLayout.json 已更新），立即重新应用
                if (form.HomeLayoutChanged)
                {
                    ApplyHomeLayout();
                    WriteLog("主页区域调整已保存并应用");
                }
            }
        }

        /// <summary>
        /// 主页区域调整 → 弹出可视化编辑器，拖动矩形块边缘调整主界面各区域尺寸。
        ///
        /// 【V1.58】
        /// - 编辑器基于当前 HomeLayout.json（或默认值）创建，拖动/输入实时改内存配置；
        /// - 点击【保存】后 HomeLayout.json 已写入，这里调用 ApplyHomeLayout 让新布局
        ///   立即生效（无需重启，工作站列表/右侧区域/菜单栏/状态栏当场重排）。
        /// - 【V1.58.3】权限放开：所有登录用户可用（菜单项不再限制管理员）。
        /// </summary>
        private void MenuHelpHomeLayout_Click(object sender, EventArgs e)
        {
            // 若现场从未保存过 HomeLayout.json，则当前生效的是按窗口比例算出的值
            // （ComputeRightPanelWidth，约 23.4%），编辑器里应显示这个值而不是
            // HomeLayoutConfig 的类默认（240），否则会出现"编辑器里显示 240、
            // 主界面实际 320"的偏差。因此这里手动把未配置项补成当前生效值。
            // 注意：编辑器里点【保存】会把该值写成 json 绝对值并定格（此后不再跟随窗口）；
            // 想恢复跟随，删掉程序目录下的 HomeLayout.json 并重启。
            var layout = HomeLayoutConfig.LoadOrDefault();
            if (!System.IO.File.Exists(HomeLayoutConfig.GetConfigPath()))
            {
                layout.RightPanelWidth = ComputeRightPanelWidth(splitContainerMain.Width, false, 0);
            }

            using (var form = new HomeLayoutEditorForm(layout))
            {
                // 【V1.60.4】走窗体自己的 ApplyTheme：整窗着色 + 预览画布深色换纯黑底
                form.ApplyTheme();
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    // 保存成功：重新应用布局，让调整立即生效
                    ApplyHomeLayout();
                    WriteLog("主页区域调整已保存并应用");
                }
            }
        }

        /// <summary>
        /// 系统设置保存后热生效（V1.40）：
        /// SettingsForm 已把非结构型配置就地回写内存 _config 实例，各服务实时读取自动生效；
        /// 这里只处理"需要额外动作"的部分：
        /// - CollectInterval：更新主采集定时器间隔
        /// - 连接参数类：触发对应设备重连（气压表串口 / 耦合器 / 送风机 / 扫码枪）
        /// - 结构型配置（设备数量/布局/Mock/送风机启用）：设置窗体已提示需重启，无需重复提醒
        /// </summary>
        private void ApplySettingsHotReload(HashSet<string> keys)
        {
            // 采集间隔：A 类，直接更新定时器间隔（状态栏随之刷新）
            if (keys.Contains("CollectInterval"))
            {
                _deviceManager.UpdateCollectInterval(_config.CollectInterval);
                UpdateStatusBar();
            }

            // 气压表串口参数：重连（Connect 内部先断开旧串口，再按新参数连接）
            if (keys.Overlaps(Dialogs.SettingsForm.BarometerConnectionKeys))
            {
                System.Threading.Tasks.Task.Run(() => _deviceManager.ReconnectBarometerReader());
            }

            // IO 耦合器连接参数：强制重连 TCP（IP/端口/超时变化需断开重连才生效）
            if (keys.Overlaps(Dialogs.SettingsForm.IoConnectionKeys))
            {
                System.Threading.Tasks.Task.Run(() => _deviceManager.ReconnectIo());
            }

            // 送风机连接参数：强制重连
            if (keys.Overlaps(Dialogs.SettingsForm.FanConnectionKeys))
            {
                System.Threading.Tasks.Task.Run(() => _deviceManager.ForceReconnectFan());
            }

            // 扫码枪：启用开关走 Start/Stop（内部按最新 ScannerEnabled 决定是否连接）；
            // 其余参数改动走立即重连一次（用最新串口参数）。串口/WMI 操作放后台线程，避免卡 UI。
            bool scannerEnabledChanged = keys.Contains("ScannerEnabled");
            bool scannerParamChanged = keys.Overlaps(Dialogs.SettingsForm.ScannerConnectionKeys) && !scannerEnabledChanged;
            if (scannerEnabledChanged || scannerParamChanged)
            {
                System.Threading.Tasks.Task.Run(() =>
                {
                    if (scannerEnabledChanged)
                    {
                        if (_config.ScannerEnabled) _scanner?.Start();
                        else _scanner?.Stop();
                    }
                    else
                    {
                        _scanner?.TryReconnectNow();
                    }
                });
            }
        }

        /// <summary>
        /// 通讯测试 → 弹出通讯测试窗体（技术员及以上权限）
        /// 用于手动测试负压开关与载台上电的 Modbus TCP 输出（直接操作 PLC DO 寄存器）
        /// V1.21：改为非模态（Show 替代 ShowDialog），打开测试窗体的同时仍可点击操作主窗体
        /// 及其它窗体（测试窗体关闭时自动 Dispose 释放资源）。
        /// 【V1.72.14】FormClosed 里包 try：Dispose 偶发竞态抛一次即够炸框，吞掉保主窗不连带。
        /// </summary>
        private void MenuHelpCommunicationTest_Click(object sender, EventArgs e)
        {
            var form = new Dialogs.CommunicationTestForm(_deviceManager);
            form.FormClosed += (s, args) => { try { form.Dispose(); } catch { } };
            ThemeManager.ApplyTo(form);
            form.Show(this);
        }

        /// <summary>
        /// 送风机测试 → 弹出冷却送风机通讯测试窗体（技术员及以上权限）
        /// 用于手动测试送风机控制屏的 Modbus TCP 通讯与定值启动/停止（直接读写设备寄存器）
        /// 非模态（Show 替代 ShowDialog），打开测试窗体的同时仍可点击操作主窗体。
        /// 【V1.72.14】同上，释放包 try。
        /// </summary>
        private void MenuHelpFanTest_Click(object sender, EventArgs e)
        {
            var form = new Dialogs.FanTestForm(_deviceManager);
            form.FormClosed += (s, args) => { try { form.Dispose(); } catch { } };
            ThemeManager.ApplyTo(form);
            form.Show(this);
        }

        /// <summary>
        /// 版本说明 → 弹出版本信息对话框
        /// （V1.19.12 更名：MenuHelpAbout_Click → MenuHelpVersionInfo_Click，菜单项"关于"改"版本说明"；
        ///  V1.58.4 起按商用软件"关于/版本说明"通用规范重写内容：
        ///  软件全称 → 版本号 → 用途简介 → 运行环境 → 功能特性（按业务分类）→ 版权声明）
        /// 【发版提醒】版本号 V1.58.4 与窗体标题一致后，这里也要同步手改，
        ///  否则版本说明会与实际版本脱节（曾长期停留在 V1.16 的教训）。
        ///  （V1.72.8 顶栏 lblTitle 已删，标题只剩窗体标题栏，不用两处同步了）
        /// 【V1.60.4】MessageBox 换成普通窗体：系统弹窗跟不了深色主题，自定义窗才能 ApplyTo。
        /// 【V1.72.14】普通窗再换 SunnyUI：UIForm 蓝标题 + UITextBox + UIButton 确认蓝，
        /// 与全窗 SunnyUI 换肤同口径（用户点名"关于按钮弹窗也要 SunnyUI 风格"）。
        /// </summary>
        private void MenuHelpVersionInfo_Click(object sender, EventArgs e)
        {
            using (Form dlg = BuildVersionInfoDialog())
            {
                ThemeManager.ApplyTo(dlg);
                dlg.ShowDialog(this);
            }
        }

        /// <summary>
        /// 软件授权 → 弹出激活窗（【V1.87】与 HJVision 一致：设备ID/设备码/激活码，
        /// 同一套《获取激活码》工具通用；激活无需权限，人人可开）。
        /// 【V1.88.1】付费即恢复：弹窗里输对一次码关闭后，重读一次 ini，
        /// 状态有效就把用户权限按钮解灰，不用重启（打开看看/输错不触发重查）。
        /// </summary>
        private void MenuHelpLicense_Click(object sender, EventArgs e)
        {
            bool activated;
            using (var form = new SoftActivation())
            {
                ThemeManager.ApplyTo(form);
                form.ShowDialog(this);
                activated = form.ActivatedSuccessfully;
            }
            if (!activated) return;
            RefreshActivationPermissionState();
        }

        /// <summary>
        /// 激活成功后重查并解灰（【V1.88.1 新增】付费即恢复的落点）。
        /// <para>做什么：重读 RunHash 双键 + 重算状态，有效（永久/试用中）就解灰用户权限按钮。</para>
        /// <para>为什么这么写：计时器置灰后本轮不自动恢复，之前要重启；
        /// 这里只认重算出的状态（不认弹窗的标记本身），新设备即使写过 RunHash2
        /// 也依然是新设备、不会被误解灰；过期机用 30 天码回到试用中则正常解灰。</para>
        /// <para>怎么改：恢复规则只认 <see cref="Services.SoftwareActivation.ShouldRestoreUserPermission"/>，
        /// 不要在这里另写一套状态判断；失败一律静默（不弹框，计时器下轮还会说话）。</para>
        /// </summary>
        private void RefreshActivationPermissionState()
        {
            try
            {
                if (IsDisposed || Disposing) return;
                string runHash1;
                string runHash2;
                Services.SoftwareActivation.ReadRunHash(out runHash1, out runHash2);
                string cpuId = Services.SoftwareActivation.GetCpuSerialNumber();
                int slot;
                int daysLeft;
                Services.SoftwareActivation.ActivationStatus status =
                    Services.SoftwareActivation.ComputeStatus(runHash1, runHash2, cpuId, out slot, out daysLeft);
                if (!Services.SoftwareActivation.ShouldRestoreUserPermission(status)) return;
                try { btnUserPermission.Enabled = true; }
                catch { }
            }
            catch { /* 重查永不拖垮主窗 */ }
        }

        /// <summary>
        /// 授权计时器（【V1.87】与 HJVision 的 HashTimer_Tick 对齐：每小时推一格）。
        /// 先对 RunHash1（设备不对=新设备），再看 RunHash2（永久跳过，否则在
        /// 0..839 格里找当前格：找到且小于 768 就写下一格；大于等于 768 或
        /// 找不到=过期）。新设备/过期只弹框 + 置灰用户权限入口（= HJVision 置灰
        /// 它的口令按钮 button4），不阻断启动、不拦生产；计时器内不自动恢复
        /// （与 HJVision 一致），付费成功由激活窗关闭后的重查即时解灰（V1.88.1）。
        /// </summary>
        private void HashTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (IsDisposed || Disposing) return;
                string runHash1;
                string runHash2;
                Services.SoftwareActivation.ReadRunHash(out runHash1, out runHash2);
                // CPU 序列号在整个校验过程中不会变，提到循环外只查一次 WMI
                //（HJVision 同款优化：旧版在循环内调用导致 840 次 WMI 卡 UI）。
                string cpuId = Services.SoftwareActivation.GetCpuSerialNumber();
                if (!Services.SoftwareActivation.IsDeviceBound(runHash1, cpuId))
                {
                    try { btnUserPermission.Enabled = false; }
                    catch { }
                    MessageBox.Show("新设备请联系厂商激活", "软件授权",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (runHash2 == Services.SoftwareActivation.PermanentMark(cpuId)) return;
                int slot = Services.SoftwareActivation.FindSlot(runHash2, cpuId);
                if (Services.SoftwareActivation.IsSlotValid(slot))
                {
                    try
                    {
                        Services.SoftwareActivation.WriteRunHash2(
                            Services.SoftwareActivation.Encrypt(cpuId + (slot + 1).ToString()));
                    }
                    catch { }
                    return;
                }
                try { btnUserPermission.Enabled = false; }
                catch { }
                MessageBox.Show("软件已过期，请联系厂商", "软件授权",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch { /* 计时器永不拖垮主窗 */ }
        }

        /// <summary>
        /// 构建版本说明对话框（【V1.60.4 新增】，纯界面搭建，方便探针反射直调验证）。
        /// 【V1.72.14】原生 Form+TextBox+Button 改 SunnyUI：UIForm 蓝标题 + UITextBox 只读多行 +
        /// UIButton 确认蓝（DodgerBlue 白字 Custom，与登录确认/各弹窗确认同色，V1.72.1 收敛）。
        /// 布局：UIForm 自绘标题占 35px，内容从 y=47 起排（txt 12→47，btn 386→421，窗高 430→465），
        /// MinimumSize=ClientSize 锁缩小；只读框 TabStop=false，焦点落"确定"上，不全文蓝底选中。
        /// 【可测性】不碰任何实例状态，特意做成 internal static，回归可直调断言 SunnyUI 风格，
        /// 不必 new 整个主窗（主窗构造会建 DeviceManager/扫码枪，测试里太重）。
        /// </summary>
        internal static Form BuildVersionInfoDialog()
        {
            // 用 string.Join("\n", ...) 组织多行文本：比字符串逐行 + 拼接更易读、易增删，避免拼错换行。
            string[] lines =
            {
                "老化测试系统（AgingTestSystem）",
                "版本 V1.58.4（Build 2026-08-10）",
                "",
                "真空老化产线专用上位机监控软件：实时监控 72 路真空压力，控制真空电磁阀与",
                "载台上电，完成老化测试全流程及报警联动保护。",
                "",
                "运行环境：Windows 7 及以上 / .NET Framework 4.7.2",
                "",
                "主要功能：",
                "[监控与控制]",
                "- 72 路气压表真空压力实时监控（Modbus RTU）",
                "- 72 路真空电磁阀 + 载台上电控制（IO 耦合器，Modbus TCP）",
                "- 冷却送风机接入：定值启停 + 温湿度监视 + IP 自动识别",
                "[老化测试业务]",
                "- 启动运行 / 停止运行 / 报警复位 / 急停全流程",
                "- 真空建立确认、老化计时自动停止、测试事件 CSV 落盘追溯",
                "- 报警联动：压力越限 / 通讯失联 / 真空建立失败 → 自动关阀断电",
                "[生产与工程]",
                "- 工位↔SN 绑定、批号录入、扫码枪自动扫码",
                "- 配方管理 / 批量下发 / 工位参数缓存",
                "- 三级权限（操作员 / 技术员 / 管理员）",
                "- 通讯测试 / 送风机测试工具、配置与布局可视化定制",
                "",
                "版权所有 © 2024-2026。保留所有权利。",
            };

            var dlg = new Sunny.UI.UIForm
            {
                Text = "版本说明",
                Name = "VersionInfoDialog",
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowIcon = false,
                ShowInTaskbar = false,
                Font = new Font("微软雅黑", 9F),
                ClientSize = new Size(560, 465),
                MinimumSize = new Size(560, 465),
                Style = Sunny.UI.UIStyle.Custom,
                TitleFont = new Font("微软雅黑", 12F, FontStyle.Bold),
                EscClose = true,
                // 【大扫荡 R8c】纯代码坐标已是终值：AutoScaleMode=None，不跟 Font 缩放双算
                //（ZoomScaleRect 与 Font 混搭高 DPI 布局偏移，已删；判定/批量/布局三窗同口径）。
                AutoScaleMode = AutoScaleMode.None
            };
            var txt = new Sunny.UI.UITextBox
            {
                Name = "txtVersionInfo",
                Multiline = true,
                ReadOnly = true,
                ShowScrollBar = true,
                ShowText = false,
                Location = new Point(12, 47),
                Size = new Size(536, 364),
                Font = new Font("微软雅黑", 9F),
                TabStop = false, // 只读展示框不抢焦点：打开时焦点落在"确定"上，避免全文蓝底选中
                Text = string.Join(Environment.NewLine, lines)
            };
            var btnOk = new Sunny.UI.UIButton
            {
                Name = "btnOk",
                Text = "确定",
                DialogResult = DialogResult.OK,
                Location = new Point(232, 421),
                Size = new Size(96, 32),
                Font = new Font("微软雅黑", 10F, FontStyle.Bold),
                Style = Sunny.UI.UIStyle.Custom,
                FillColor = Color.DodgerBlue,
                RectColor = Color.DodgerBlue,
                ForeColor = Color.White
            };
            dlg.Controls.Add(txt);
            dlg.Controls.Add(btnOk);
            dlg.AcceptButton = btnOk;
            dlg.CancelButton = btnOk;
            return dlg;
        }

        #endregion

        #endregion

        #region 右侧操作按钮事件处理

        /// <summary>
        /// 批量设置配方按钮点击
        /// 弹出批量设置配方窗口，允许用户配置配方参数并加入队列
        /// </summary>
        private void btnBatchRecipe_Click(object sender, EventArgs e)
        {
            ShowBatchRecipeForm();
        }

        /// <summary>
        /// 弹出批量设置配方窗口（【V1.24】抽取为公共方法，供"批量设置配方"按钮与
        /// 面板"设置"按钮多选时共用；【V1.26】加入队列=保存配方+应用到选中工位）
        ///
        /// 【V1.26 说明】
        /// - 传入当前选中的工位编号（允许为 0 个）：若一个工位都没选中，
        ///   "加入队列"时批量窗口先把配方保存到本地配方列表，再提示用户先选择工位；
        /// - 传入共享配方列表 _recipes 与设备管理器，供批量窗口保存配方 / 应用到选中工位。
        /// </summary>
        private void ShowBatchRecipeForm()
        {
            // 收集当前选中的工位编号（允许为 0 个：是否满足"至少选中一个"由批量窗口判断并提示，
            // 因此这里不弹提示、也不返回 null）
            int[] selectedArray = _gridView != null ? _gridView.GetSelectedDeviceIds() : new int[0];
            var selectedIds = new List<int>(selectedArray);

            using (var form = new BatchRecipeForm(_deviceManager, _recipes, selectedIds))
            {
                // 显示窗口（模态对话框，阻塞主窗口直到关闭）
                ThemeManager.ApplyTo(form);
                form.ShowDialog(this);
            }
        }

        /// <summary>
        /// 录入批号按钮点击事件
        /// 弹出录入批号窗口，允许用户手动输入产品批号
        /// </summary>
        private void btnInputLot_Click(object sender, EventArgs e)
        {
            // 【V1.16.1】扫码枪按需重连：打开"录入批号"前重连一次；
            // 仍连不上则提示（扫码枪是可选设备，不影响手动输入批号/SN）。
            // 【V1.16.2】重连后刷新状态栏扫码枪状态。
            if (_config.ScannerEnabled && _scanner != null && !_scanner.IsConnected)
            {
                bool scannerOk = _scanner.TryReconnectNow();
                RefreshScannerStatus();
                if (!scannerOk)
                {
                    // 【SunnyUI 弹窗】警告类用 Orange（与参数无权限提示同口径），不走系统 MessageBox。
                    Sunny.UI.UIMessageBox.Show("扫码枪未连接，请先连接（不影响手动输入批号/SN）", "提示",
                        Sunny.UI.UIStyle.Orange, Sunny.UI.UIMessageBoxButtons.OK, true, 0);
                }
            }

            // 【V1.16】传入扫码枪服务：ID绑定窗体打开时，扫码结果自动填充 SN 输入框
            // 【V1.19.11】传入设备管理器：ID绑定保存时把"工位 → SN"写入工位静态信息，工位面板 SN 同步显示
            using (var form = new InputLotForm(_scanner, _deviceManager))
            {
                // 订阅批号录入完成事件：记录日志 + 通知设备管理器（用于事件落盘追溯）
                form.OnLotInputCompleted += (sender2, lotNumber) =>
                {
                    // 【V1.10】批号写入设备管理器，后续启动/报警/停止日志都会带上批号
                    _deviceManager.CurrentLotNumber = lotNumber;
                    WriteLog($"[录入批号] 用户录入批号: {lotNumber}");
                };

                // 显示窗口（模态对话框，阻塞主窗口直到关闭）
                ThemeManager.ApplyTo(form);
                DialogResult result = form.ShowDialog(this);

                // 用户关闭窗口后，处理录入结果
                if (result == DialogResult.OK)
                {
                    string lotNumber = form.GetLotNumber();
                    WriteLog($"[录入批号] 批号录入成功: {lotNumber}");
                    // 【SunnyUI 弹窗】成功类用 Green。
                    Sunny.UI.UIMessageBox.Show(
                        $"批号录入成功！\n\n录入的批号: {lotNumber}\n\n【预留】批号将用于标识当前生产批次，便于后续追溯和数据分析。",
                        "录入批号",
                        Sunny.UI.UIStyle.Green, Sunny.UI.UIMessageBoxButtons.OK, true, 0);
                }
                else
                {
                    WriteLog("[录入批号] 用户取消了批号录入");
                }
            }
        }

        /// <summary>
        /// 启动运行按钮点击（【V1.10】接真实业务）
        /// 对选中的面板执行（V1.59 三阶段状态机）：开真空阀 → 真空到位+延时时间到自动载台上电
        /// （【V1.88.14】延时=0 的台启动时阀电同开、直接计时并保持常开）
        /// → 按配方烧屏时间老化计时（到时自动下电关阀标完成）；送风机由生命周期自动定值启动（首台）
        /// 【V1.16.2】异步：连接耦合器/送风机时弹"连接中"，不卡界面
        /// </summary>
        private async void btnStartRun_Click(object sender, EventArgs e)
        {
            int[] ids = GetSelectedDeviceIds();
            if (ids == null) return;

            // 【V1.66】启动前风险提示（烧屏场景）：0 时长=不限时长永不自动完成（无限点亮），
            // 空 SN=完成后无法追溯到单体。只警告不拦截——点"是"照跑、点"否"取消，
            // 是否允许是现场工艺权，软件只负责把丑话说在前面（文案见 AgingSequencer）。
            var zeroDurationIds = new List<int>();
            var emptySnIds = new List<int>();
            foreach (int id in ids)
            {
                BarometerData data = _deviceManager.GetBarometerData(id);
                if (data == null) continue;
                if (string.IsNullOrWhiteSpace(data.SerialNumber)) emptySnIds.Add(id);
                double burnInSecs = data.BurnInTime.TotalSeconds;
                int effectiveSecs = burnInSecs > 0 ? (int)burnInSecs : _config.MaxTestDurationSeconds;
                if (effectiveSecs <= 0) zeroDurationIds.Add(id);
            }
            string riskWarning = AgingSequencer.BuildStartWarningText(
                zeroDurationIds.ToArray(), emptySnIds.ToArray());

            // 【V1.67】Q13 硬拦截：策略=Block 且命中 0 时长/空 SN 工位时直接阻断，
            // 连确认框都不进（Warn 走上面的警告拼框，点"是"照跑）。
            string blockText = AgingSequencer.BuildStartBlockText(
                zeroDurationIds.ToArray(), emptySnIds.ToArray(),
                _config.ZeroDurationPolicy, _config.EmptySnPolicy);
            if (!string.IsNullOrEmpty(blockText))
            {
                // 【SunnyUI 弹窗】阻断/失败类用 Red。
                Sunny.UI.UIMessageBox.Show("启动已被工艺策略阻断：\n\n" + blockText +
                    "\n\n（如现场允许放行，到系统设置 → 工艺策略 改回\"只警告\"）",
                    "启动阻断", Sunny.UI.UIStyle.Red, Sunny.UI.UIMessageBoxButtons.OK, true, 0);
                return;
            }

            // 【SunnyUI 确认框】是/否确认走 OKCancel（确定=True 继续，取消=False 返回，
            // 与原来 `r != DialogResult.Yes → return` 同语义）。
            // 注意不用 YesNoCancel：本仓 SunnyUI 3.9.8 的 YesNoCancel 只画出单个"确定"
            //（Gitee 官方 issue 同款 bug，截图实证），确认框会丢掉"否"路；
            // OKCancel 经截图验证是"确定+取消"双键。返回值语义经 IL 实证：
            // 确定键置 DialogResult.OK（Show 返回 True），取消/X 置 None（返回 False）。
            // 【文案压短】同急停确认：UIMessageBox 限高不可调，基本文案压到 6 短行一次看全；
            // riskWarning（0 时长/空 SN 警告，有才拼）超长时仍可能出滚动条，
            // 那是极少数台的异常提示，非常态，可接受。
            if (!Sunny.UI.UIMessageBox.Show(
                $"确认启动 {ids.Length} 台老化测试？\n" +
                "1. 开真空阀建立负压固定产品\n" +
                "2. 到位+延时时间到自动载台上电\n" +
                "   （延时=0 的台阀电同开直接计时）\n" +
                "3. 按配方计时，到时下电标完成\n" +
                "4. 送风机定值启动\n" +
                "注：真空久未建立自动报警断电。" +
                riskWarning,
                "启动运行",
                Sunny.UI.UIStyle.Orange, Sunny.UI.UIMessageBoxButtons.OKCancel, true, 0)) return;

            // 【V1.16.2】启动测试需要耦合器（开阀+载台上电）：先异步连接（弹"连接中"），连不上弹窗提示
            if (!await EnsureIoReadyAsync()) return;

            // 【V1.16.2】启动测试依赖送风机保持温控：送风机没连上时给一次异步按需重连
            //（弹"连接中"），仍连不上则提示（不阻断测试，但操作员要知道没有温控）。
            // 【V1.67】Q16：FanDisconnectPolicy=BlockStart 时改为阻断启动（先修风机再点火）。
            if (_deviceManager.IsFanEnabled && !_deviceManager.IsFanConnected)
            {
                bool fanOk = await EnsureFanReadyAsync();
                if (!fanOk)
                {
                    if (_config.FanDisconnectPolicy == FanDisconnectPolicy.BlockStart)
                    {
                        Sunny.UI.UIMessageBox.Show("送风机未连接，工艺策略要求阻断启动：\n\n" +
                            "请先连上送风机（测试需要环境温控），\n" +
                            "或到系统设置 → 工艺策略 改回\"只提示\"。",
                            "启动阻断", Sunny.UI.UIStyle.Red, Sunny.UI.UIMessageBoxButtons.OK, true, 0);
                        return;
                    }
                    Sunny.UI.UIMessageBox.Show("送风机未连接，请先连接（测试仍会启动，但老化过程没有环境温控）", "提示",
                        Sunny.UI.UIStyle.Orange, Sunny.UI.UIMessageBoxButtons.OK, true, 0);
                }
            }

            _deviceManager.StartTesting(ids);
            WriteLog($"启动老化测试（{ids.Length} 台）");
        }

        /// <summary>
        /// 停止运行按钮点击（【V1.10 新增】）
        /// 对选中的面板执行：关真空阀 + 断载台上电 + 退出测试中
        /// （最后一台停止时送风机自动停止）
        /// </summary>
        private async void btnStopRun_Click(object sender, EventArgs e)
        {
            int[] ids = GetSelectedDeviceIds();
            if (ids == null) return;

            // 【SunnyUI 确认框】同启动确认：OKCancel，确定=True 继续（YesNoCancel 在 3.9.8 只出单键，见上）。
            if (!Sunny.UI.UIMessageBox.Show(
                $"确认停止 {ids.Length} 台的运行？\n\n将执行：\n1. 关闭真空电磁阀\n2. 断开载台上电",
                "停止运行",
                Sunny.UI.UIStyle.Orange, Sunny.UI.UIMessageBoxButtons.OKCancel, true, 0)) return;

            // 【V1.16.2】停止测试需要耦合器（关阀+断载台电）：先异步连接，连不上弹窗提示
            if (!await EnsureIoReadyAsync()) return;

            // 【大扫荡】下发失败明示（async void 不接住即崩溃；状态不清=台还显示在测）。
            try
            {
                _deviceManager.StopTesting(ids);
            }
            catch (Exception ex)
            {
                WriteLog("[停止] 下发失败：" + ex.Message + "（选中台可能还开着，请检查耦合器后重试）");
                Sunny.UI.UIMessageBox.Show("停止下发失败：\n\n" + ex.Message +
                    "\n\n选中工位可能仍在运行！请检查耦合器连接后重试停止。",
                    "停止失败", Sunny.UI.UIStyle.Red, Sunny.UI.UIMessageBoxButtons.OK, true, 0);
                return;
            }
            WriteLog($"停止运行（{ids.Length} 台）");
        }

        /// <summary>
        /// 报警复位按钮点击（【V1.10 新增】【V1.59 兼容完成态】）
        /// 对选中的报警/故障/已完成·待取料面板执行人工复位：清除故障标记，
        /// 回到空闲，可重新启动。
        /// 【设计说明】报警后不自动恢复，必须人工确认（防止真空失效原因未确认就重启）；
        /// "已完成·待取料"由本按钮确认取件，或重新扫码绑定时自动复位。
        /// </summary>
        private void btnResetAlarm_Click(object sender, EventArgs e)
        {
            int[] ids = GetSelectedDeviceIds();
            if (ids == null) return;

            // 【SunnyUI 确认框】同启动确认：OKCancel，确定=True 继续（YesNoCancel 在 3.9.8 只出单键，见上）。
            if (!Sunny.UI.UIMessageBox.Show(
                $"确认复位 {ids.Length} 台？\n\n" +
                "将清除故障标记或确认取件完毕，设备回到空闲状态，可重新启动老化测试。",
                "复位",
                Sunny.UI.UIStyle.Orange, Sunny.UI.UIMessageBoxButtons.OKCancel, true, 0)) return;

            _deviceManager.ResetDevices(ids);
            WriteLog($"复位（{ids.Length} 台：报警/完成态已清除）");
        }

        /// <summary>
        /// 下料判定按钮点击（【V1.67 新增】Q22 PendingReview 配套）。
        /// 选中已完成·待取料的台 → 弹窗录 PASS/FAIL + 不良代码 + 处置 → 写 CSV 追溯 → 回空闲。
        /// AutoPass 模式下点它只提示（无需判定），不做任何事——操作员误触零风险。
        /// </summary>
        private void btnUnloadJudge_Click(object sender, EventArgs e)
        {
            if (_config.CompletionJudgePolicy != CompletionJudgePolicy.PendingReview)
            {
                // 【SunnyUI 弹窗】中性说明类用 Blue。
                Sunny.UI.UIMessageBox.Show("当前完成判定 = 自动PASS，无需下料判定。\n\n" +
                    "如需人工判定，到系统设置 → 工艺策略 把完成判定切到\"待判定\"。",
                    "下料判定", Sunny.UI.UIStyle.Blue, Sunny.UI.UIMessageBoxButtons.OK, true, 0);
                return;
            }

            int[] ids = GetSelectedDeviceIds();
            if (ids == null) return;

            using (var form = new UnloadJudgeForm(ids, _deviceManager))
            {
                ThemeManager.ApplyTo(form);
                form.ShowDialog(this);
                WriteLog($"下料判定窗口已关闭（{ids.Length} 台送判，明细见历史查询 CSV）");
            }
        }

        /// <summary>
        /// 全部停止（急停）按钮点击（【V1.10 新增】）
        /// 一键关闭所有真空阀 + 断开所有载台上电 + 停止送风机，带防误触确认。
        /// </summary>
        private async void btnStopAll_Click(object sender, EventArgs e)
        {
            // 【SunnyUI 确认框】同启动确认：OKCancel，确定=True 继续（YesNoCancel 在 3.9.8 只出单键，见上）。
            // 【文案压到 5 短行】UIMessageBox 限高不可调（内部滚动+末行会被按钮区裁掉，
            // 真屏截图实证）：去空行去"将执行"头，每行不超 15 字不换行，5 行一次看全。
            if (!Sunny.UI.UIMessageBox.Show(
                "确认【全部停止】？\n" +
                "1. 关闭所有 72 路真空阀\n" +
                "2. 断开所有 72 路载台上电\n" +
                "3. 停止送风机\n" +
                "此操作不可撤销，请确认现场安全！",
                "全部停止（急停）",
                Sunny.UI.UIStyle.Orange, Sunny.UI.UIMessageBoxButtons.OKCancel, true, 0)) return;

            // 【V1.16.2】急停需要耦合器（关阀+断载台电）：先异步连接；连不上要明确告诉
            // 操作员，否则可能误以为阀门已关闭（安全提示）。
            if (!await EnsureIoReadyAsync()) return;

            // 【大扫荡】急停下发失败必须明示：StopAll 写失败抛异常（状态不清、台还显示在测），
            // async void 里不接住即崩溃；接住弹框，操作员看得见"没停掉"。
            try
            {
                _deviceManager.StopAll();
            }
            catch (Exception ex)
            {
                WriteLog("[急停] 下发失败：" + ex.Message + "（部分阀/电可能还开着，请检查耦合器后重试）");
                Sunny.UI.UIMessageBox.Show("急停下发失败：\n\n" + ex.Message +
                    "\n\n部分阀门/载台电可能还开着！请检查耦合器连接后重试急停。",
                    "急停失败", Sunny.UI.UIStyle.Red, Sunny.UI.UIMessageBoxButtons.OK, true, 0);
                return;
            }
            WriteLog("已执行全部停止（急停）");
        }

        #endregion

        /// <summary>
        /// 获取当前选中的设备编号数组（【V1.10 新增】）
        /// 从工位网格读取所有选中工位。
        /// 一个都没选时弹提示并返回 null。
        /// </summary>
        /// <returns>选中的设备编号数组；未选择时返回 null</returns>
        private int[] GetSelectedDeviceIds()
        {
            int[] ids = _gridView != null ? _gridView.GetSelectedDeviceIds() : new int[0];

            if (ids.Length == 0)
            {
                // 【SunnyUI 弹窗】中性说明类用 Blue（操作区 7 按钮共用的选台提示）。
                Sunny.UI.UIMessageBox.Show("请先在气压表区域选中要操作的设备\n（点击面板或用行全选按钮）",
                    "提示", Sunny.UI.UIStyle.Blue, Sunny.UI.UIMessageBoxButtons.OK, true, 0);
                return null;
            }

            return ids;
        }

        /// <summary>
        /// 时间更新定时器 Tick 事件
        /// 每秒更新状态栏的当前时间显示
        /// </summary>
        private void timerTime_Tick(object sender, EventArgs e)
        {
            // 【大扫荡】关窗守卫：全仓他窗 Tick 都有 _closed，此处独漏；
            // 关闭瞬间 Tick 与 Dispose 竞态即碰已释放 toolStrip。
            if (_mainClosing || this.IsDisposed || this.Disposing || !this.IsHandleCreated) return;
            if (toolStripStatusLabelTime == null || toolStripStatusLabelTime.IsDisposed) return;
            toolStripStatusLabelTime.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        // ===================== 扫码枪事件处理（V1.16 新增） =====================

        /// <summary>
        /// 扫码完成事件处理（已封送到 UI 线程，可直接操作控件）
        /// 扫码结果写入 LOG 日志，供操作追溯
        /// </summary>
        /// <param name="sender">扫码枪服务</param>
        /// <param name="barcode">扫到的条码内容</param>
        private void Scanner_OnBarcodeScanned(object sender, string barcode)
        {
            // 【V1.72.15】退出期丢弃（WriteLog 虽自拦，早退少一次排队）。
            if (_mainClosing || this.IsDisposed || this.Disposing) return;
            // 写日志（条码内容可能含敏感字符，仅记录内容即可）
            WriteLog($"[扫码枪] 读码成功: {barcode}");

            // 【预留】如需根据扫码内容匹配设备/配方，可在这里扩展业务
            // TODO: 根据扫码内容匹配设备/配方，触发对应业务流程
        }

        /// <summary>
        /// 扫码枪连接状态变化事件处理（已封送到 UI 线程）
        /// 把连接成功/未找到端口/错误等状态写入 LOG 日志
        /// </summary>
        /// <param name="sender">扫码枪服务</param>
        /// <param name="message">状态描述文本</param>
        private void Scanner_OnStatusChanged(object sender, string message)
        {
            // 【V1.72.15】退出期丢弃（同上）。
            if (_mainClosing || this.IsDisposed || this.Disposing) return;
            WriteLog($"[扫码枪] {message}");

            // 【V1.16.2】扫码枪连接状态变化 → 刷新状态栏显示（已连接/未连接/未启用）
            RefreshScannerStatus();
        }

        /// <summary>
        /// 刷新状态栏"扫码枪"连接状态（【V1.16.2 新增】）
        /// 让操作员在底部状态栏一眼看到扫码枪当前连接状态：
        /// - 已连接 = 绿；未连接 = 红；未启用（App.config 关掉）= 灰。
        /// 与顶部"通讯模块状态"（耦合器）、送风机状态标签一起，
        /// 构成完整的设备连接状态总览（四个设备断了哪个一眼可见）。
        /// </summary>
        private void RefreshScannerStatus()
        {
            // 【V1.72.15】加 _mainClosing（排队回调关后丢弃）。
            if (_mainClosing || this.IsDisposed || this.Disposing) return;

            try
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action(RefreshScannerStatus));
                    return;
                }

                if (!_config.ScannerEnabled || _scanner == null)
                {
                    toolStripStatusLabelScanner.Text = "扫码枪: 未启用";
                    toolStripStatusLabelScanner.ForeColor = Color.Gray;
                }
                else if (_scanner.IsConnected)
                {
                    toolStripStatusLabelScanner.Text = "扫码枪: 已连接";
                    toolStripStatusLabelScanner.ForeColor = Color.Green;
                }
                else
                {
                    toolStripStatusLabelScanner.Text = "扫码枪: 未连接";
                    toolStripStatusLabelScanner.ForeColor = Color.Red;
                }
            }
            catch (ObjectDisposedException)
            {
                // 窗体已释放，忽略
            }
            catch (InvalidOperationException)
            {
                // 窗体释放中，忽略
            }
        }

        /// <summary>
        /// 写入日志到右侧 LOG 文本框
        /// 自动添加时间戳，最新日志显示在末尾并自动滚动
        ///
        /// 【V1.58.21 持久化】
        /// 日志不只显示在 UI 文本框（内存）里，同时由 <see cref="AgingTestSystem.Services.AppLogFileWriter"/>
        /// 追加写入本地文件 Logs\AppLog_yyyyMMdd.log（按日期分文件）。程序重启后日志不丢失，
        /// 可离线追溯。写文件失败静默处理，不影响界面显示。
        /// </summary>
        /// <param name="message">日志消息</param>
        private void WriteLog(string message)
        {
            // 【V1.72.15】退出/释放期 UI 丢弃、文件照写：此方法此前零守卫，退出瞬间排队回调
            // 直碰 txtLog 即炸（UI 线程 ObjectDisposedException 弹框）；文件写无窗口依赖，照写。
            string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\r\n";
            try
            {
                if (!_mainClosing && !this.IsDisposed && !this.Disposing
                    && txtLog != null && !txtLog.IsDisposed)
                {
                    // 【修复 M8】限制日志文本框最大字符数，避免长时间运行后 GDI 句柄耗尽或卡顿
                    // 超过上限时裁剪掉旧内容，只保留最近一半
                    if (txtLog.TextLength > MaxLogTextLength)
                    {
                        txtLog.Text = txtLog.Text.Substring(txtLog.TextLength - LogTrimKeepLength);
                    }

                    // 追加到日志文本框
                    txtLog.AppendText(logLine);
                }
            }
            catch { }

            // 【V1.58.21 持久化】同一行日志同时写入本地文件（Logs\AppLog_yyyyMMdd.log），
            // 与 UI 显示内容一致；内部有 lock 线程安全 + 写失败静默，不影响主流程
            AgingTestSystem.Services.AppLogFileWriter.Write(logLine);
        }

        /// <summary>
        /// 窗体关闭事件
        /// 释放设备管理器资源（停止定时器、断开硬件连接）
        /// DeviceManager 实现了 IDisposable，调用 Dispose 释放所有资源
        /// </summary>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 【V1.72.15】首行置退出标记：后台采集/扫码/重连线程的排队回调即刻开始丢弃，
            // 再退订事件、再 Dispose（退订拦不住已排队的 Post，靠入口 _mainClosing 拦）。
            _mainClosing = true;
            // 【修复 H4】先取消事件订阅，避免 Dispose 过程中事件回调到已释放的 UI
            // 顺序很重要：必须先取消订阅，再 Dispose
            if (_deviceManager != null)
            {
                _deviceManager.OnBatchDataUpdated -= DeviceManager_OnBatchDataUpdated;
                // 【V1.30】退订快速跟踪增量更新事件
                _deviceManager.OnQuickTrackDataUpdated -= DeviceManager_OnQuickTrackDataUpdated;
                _deviceManager.OnConnectionStatusChanged -= DeviceManager_OnConnectionStatusChanged;
                // 【V1.10】退订送风机数据更新事件
                _deviceManager.OnFanDataUpdated -= DeviceManager_OnFanDataUpdated;
                // 【V1.16】退订启动/连接诊断事件
                _deviceManager.OnDiagnostic -= DeviceManager_OnDiagnostic;
            }

            // 释放设备管理器（内部会调用 Stop 停止定时器和断开连接）
            _deviceManager?.Dispose();

            // 【V1.16 新增】释放扫码枪服务（停止重连定时器 + 关闭串口）
            // 顺序很重要：先取消事件订阅，再 Dispose，避免回调到已释放的 UI
            if (_scanner != null)
            {
                _scanner.OnBarcodeScanned -= Scanner_OnBarcodeScanned;
                _scanner.OnStatusChanged -= Scanner_OnStatusChanged;
                _scanner.Dispose();
                _scanner = null;
            }

            // 注意：下拉菜单改为动态创建的弹出窗体（Form），点击菜单项或失去焦点后自动关闭
            // 不需要在这里手动释放，Form.Close 会触发 Dispose

            // 【大扫荡】网格事件退订：主窗单例本次只建一次掩盖了问题，一旦重建网格
            //（热更扩列/测试），旧画布被 OnSetClicked/OnLog 拽住不释放。
            if (_gridView != null)
            {
                _gridView.OnSetClicked -= Panel_OnSetClicked;
                _gridView.OnLog -= GridView_OnLog;
            }
        }

        /// <summary>
        /// 网格内部动作日志转发主窗 LOG（具名方法，可退订；见装配处与 FormClosing）。
        /// </summary>
        private void GridView_OnLog(object sender, string message)
        {
            WriteLog(message);
        }
    }
}
