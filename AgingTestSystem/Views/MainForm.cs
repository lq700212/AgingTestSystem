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
    /// │ 老化测试系统V1.00  │ 当前操作权限: 操作员 │ PLC连接状态: 已连接 │
    /// ├─────────────────────────────────────────────────────────┤
        /// │ [用户权限] [参数设置] [日志记录] [关于] │
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
    /// │                              │ 操作                    │
    /// │                              │ [温控操作(D4203)]      │
    /// │                              │ [开启真空(VAC_1)]      │
    /// │                              │ [批量设置配方]          │
    /// │                              │ [录入批号]             │
    /// │                              │ [启动运行(D4202)]      │
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
    public partial class MainForm : System.Windows.Forms.Form
    {
        /// <summary>
        /// 【V1.58.1】右侧状态按钮区宽度的默认值（写死在本窗体，不放在 HomeLayoutConfig）。
        ///
        /// 设计约定：
        /// - 现场未保存过 HomeLayout.json（即没用"主页区域调整"编辑器改过）时，
        ///   MainForm 直接用它作为右侧面板宽度，调整只需改这一个数字；
        /// - 一旦现场在编辑器里保存过配置，则以 HomeLayout.json 里的
        ///   <see cref="HomeLayoutConfig.RightPanelWidth"/> 为准（用户自定义优先）。
        /// </summary>
        public const int DefaultRightPanelWidth = 300;

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
        /// 【新增】用户管理器
        /// 负责用户登录验证、用户名密码修改等功能
        /// 默认账号：
        /// - 管理员: admin / 123456
        /// - 技术员: technician / 123456
        /// - 操作员: operator / 123456
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
        /// 通讯连接状态（V1.16 更名：现场无 PLC，改为通讯连接状态）
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

            // 【V1.49】主窗体开启双缓冲，与工位面板/网格双缓冲配合，消除滚动撕裂
            this.DoubleBuffered = true;

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
        /// 自适应调整右侧操作面板（tableLayoutPanelRight）的宽度
        ///
        /// 【V1.58 改造】原实现用"临时 AutoSize 测量内容最小宽度"来自动定右侧宽度；
        /// 现在右侧宽度由 <see cref="HomeLayoutConfig.RightPanelWidth"/> 驱动（可在
        /// "关于 → 主页区域调整"编辑器里拖边缘调整）。本方法改为：
        /// 1. 设置 splitContainerMain.SplitterDistance，使 Panel2 宽度 = 配置的右侧宽度；
        /// 2. 同步缩放"操作"分组里的按钮宽度（按钮原设计宽 300，若右侧被调窄则按比例缩，
        ///    避免按钮溢出分组框）。
        /// FixedPanel=Panel2（已在 Designer 中设置）确保窗口缩放时右侧宽度不变。
        ///
        /// 【默认值来源（V1.58.1 调整）】
        /// 右侧宽度的默认值写死在 MainForm（<see cref="DefaultRightPanelWidth"/>，300），
        /// 不放在 HomeLayoutConfig 里——只有现场在编辑器里保存过 HomeLayout.json，
        /// 才用配置文件里的宽度覆盖默认值。判断依据是"配置文件是否存在"。
        ///
        /// 【注意】右侧宽度并非随便能调：若窄到按钮文字放不下会被截断，编辑器里有
        /// 180~600 的下限保护；现场不满意可在编辑器里再拖回来。
        /// </summary>
        private void AdjustRightPanelWidth()
        {
            // 默认宽度写死在 MainForm（300），与 HomeLayoutConfig 的类默认值（260）解耦。
            // 若现场保存过 HomeLayout.json（编辑器里改过），则以文件里的值为准。
            int targetRight = DefaultRightPanelWidth;
            if (System.IO.File.Exists(HomeLayoutConfig.GetConfigPath()))
            {
                var layout = HomeLayoutConfig.LoadOrDefault();
                targetRight = layout.RightPanelWidth;
            }

            // 1. 设置 SplitterDistance，让 Panel2 宽度 = 目标右侧宽度
            //    Panel2 宽度 = splitContainerMain 总宽 - SplitterDistance - 分隔条宽度
            //    => SplitterDistance = 总宽 - 右侧宽度 - 分隔条宽度
            int distance = splitContainerMain.Width - targetRight - splitContainerMain.SplitterWidth;
            if (distance > 0)
            {
                splitContainerMain.SplitterDistance = distance;
            }

            // 2. 同步缩放"操作"分组里的按钮宽度（按钮 X=15、宽 300 是设计值，
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
                if (ctl is Button btn)
                {
                    btn.Height = layout.MenuHeight - 12;
                }
            }

            // 右侧区域宽度（由 AdjustRightPanelWidth 内部读同一配置设置 SplitterDistance）
            AdjustRightPanelWidth();
        }

        /// <summary>
        /// 【V1.58】把"操作"分组里的 9 个按钮宽度同步为分组可用宽度 - 左右边距。
        /// 原设计按钮宽 300（groupBox 宽 320）；右侧区域可调后，分组框宽度随之变化，
        /// 若仍用 300 固定宽会导致按钮溢出分组框或被截断。宽度 = 分组客户区宽 - 30（左右各 15）。
        /// </summary>
        private void ResizeOperationButtons()
        {
            if (groupBoxOperation == null) return;

            // 分组客户区宽度（已扣除分组框边框），按钮左右各留 15px 边距
            int buttonWidth = groupBoxOperation.ClientSize.Width - 30;
            if (buttonWidth < 80) buttonWidth = 80;   // 下限保护：按钮不能窄到没法看

            foreach (Control ctl in groupBoxOperation.Controls)
            {
                // 只处理操作按钮（都是普通 Button；若以后加入非按钮控件需排除）
                if (ctl is Button btn && btn != null)
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
        private void ShowDropdownPopup(Button hostButton, (string Text, EventHandler ClickHandler)[] items)
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
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];

                var btn = new Button
                {
                    Text = item.Text,                          // 菜单项文本
                    Dock = DockStyle.Fill,                     // 填满单元格
                    Margin = new Padding(0),                   // 无外边距，紧贴相邻项
                    BackColor = hostButton.BackColor,          // 继承主按钮背景色（绿色）
                    ForeColor = hostButton.ForeColor,          // 继承主按钮文字色（白色）
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
            };

            // ===== 7. 显示弹出窗体（非模态，不阻塞主窗体） =====
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
                config.TotalBarometers = count;
            }

            // 尝试从配置文件读取采集间隔
            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["CollectInterval"], out int interval))
            {
                config.CollectInterval = interval;
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
                config.BaudRate = baudRate;
            }

            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["DataBits"], out int dataBits))
            {
                config.DataBits = dataBits;
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
                config.SerialReadTimeoutMs = serialReadTimeoutMs;
            }

            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["SerialWriteTimeoutMs"], out int serialWriteTimeoutMs))
            {
                config.SerialWriteTimeoutMs = serialWriteTimeoutMs;
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

            // 备用通道映射表：格式 "0x2000@0x00->0x2009@0x10;0x2008@0x00->0x2009@0x11"
            // （寄存器@通道均为十六进制，与设置界面显示一致）
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
                config.ScannerBaudRate = scannerBaudRate;
            }

            if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings["ScannerDataBits"], out int scannerDataBits))
            {
                config.ScannerDataBits = scannerDataBits;
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

            // 【启动优化】原逻辑在 UI 线程同步执行 _deviceManager.Start()（连接气压表串口、
            // IO 耦合器、送风机 + 首次同步轮询全部 72 台气压表，串口/网线异常时可能耗时数秒）
            // 和 _scanner.Start()，导致窗体显示前明显卡顿。现改为后台线程执行：
            // - 界面立即显示，采集/扫码枪在后台陆续就绪；
            // - DeviceManager 内部定时器为 System.Timers.Timer（线程池触发），
            //   所有事件已自行 BeginInvoke 封送，后台调用 Start() 线程安全。
            StartDevicesInBackground();

            // 启动定时器更新状态栏时间显示
            timerTime.Start();
        }

        /// <summary>
        /// 后台启动设备管理器与扫码枪（【启动优化】）
        /// - _deviceManager.Start()：连接气压表/IO耦合器/送风机 + 首次采集，耗时步骤放后台；
        /// - _scanner.Start()：扫码枪自动识别串口并连接（可选设备，内部已做未启用跳过）。
        /// 完成后切回 UI 线程刷新顶部"通讯连接状态"与状态栏"扫码枪"状态。
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

                // 【V1.16.1】顶部"通讯连接状态"只反映 IO 耦合器（阀/载台电控制）是否连接，
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
            });
        }

        /// <summary>切换到 UI 线程写日志（后台线程调用时使用，避免跨线程访问控件）</summary>
        private void WriteLogOnUi(string message)
        {
            RunOnUi(() => WriteLog(message));
        }

        /// <summary>切换到 UI 线程执行（窗体已释放时安全跳过）</summary>
        private void RunOnUi(Action action)
        {
            if (IsDisposed || Disposing) return;
            try
            {
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
        /// - 外层用 Panel.AutoScroll 容器托管，内容超出时出现滚动条；
        /// - 滚动时系统只需移动 1 个窗口（而非 V1.49 的 72 个），无撕裂。
        /// 【注意】不能放在 FlowLayoutPanel 中，因为 FlowLayoutPanel
        /// 不尊重子控件的 Dock=Fill 属性。
        /// </summary>
        private void CreateWorkstationPanels()
        {
            // 【修复 H5】清空前先 Dispose 旧控件，避免控件资源泄漏
            // Controls.Clear() 只移除父子关系，不会释放控件资源
            // 旧控件（含子控件）会成为孤儿，等待 GC 回收，可能耗尽 GDI 句柄
            foreach (Control c in splitContainerMain.Panel1.Controls)
            {
                c.Dispose();
            }
            // 清空左侧面板容器
            splitContainerMain.Panel1.Controls.Clear();

            // 外层滚动容器：网格画布尺寸=内容总尺寸，由本容器托管滚动条
            var scrollContainer = new Panel();
            scrollContainer.Dock = DockStyle.Fill;      // 填满整个左侧区域
            scrollContainer.AutoScroll = true;          // 内容超出时显示滚动条
            // 【V1.50】滚动容器开启双缓冲，配合自绘网格消除滚动撕裂/闪烁
            EnableDoubleBuffering(scrollContainer);

            // 自绘工位网格（1 个 UserControl 画全部面板 + 行全选按钮列）
            _gridView = new WorkstationGridView();
            _gridView.Configure(_config.PanelColumns, _config.PanelRows, _config.TotalBarometers);

            // 订阅"设置"按钮点击事件（V1.18：打开工位设置窗口；V1.24：按选中数量分流）
            _gridView.OnSetClicked += Panel_OnSetClicked;

            // 订阅网格内部动作日志（如行全选/取消全选），写入主窗体 LOG
            _gridView.OnLog += (sender, message) => WriteLog(message);

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
            if (this.IsDisposed || this.Disposing) return;

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
            if (this.IsDisposed || this.Disposing) return;

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
            if (this.IsDisposed || data == null) return;

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
            if (this.IsDisposed || allData == null) return;

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
            if (this.IsDisposed || this.Disposing) return;

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

        /// <summary>
        /// 已记录过的送风机温度告警阈值（避免重复写日志）
        /// </summary>
        private float _fanTempAlarmLoggedThreshold = 0f;

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
            if (this.IsDisposed || this.Disposing) return;

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
        /// 更新顶部"通讯连接状态"显示
        /// 【V1.16.1】语义 = IO 耦合器（阀 / 载台电控制）是否连接，不再反映气压表串口。
        /// 数据来源 _commConnected（由 DeviceManager.OnConnectionStatusChanged 事件驱动）。
        /// 【修复 H1】增加 IsDisposed 检查，使用 BeginInvoke 异步切换
        /// </summary>
        private void UpdateConnectionStatus()
        {
            // 窗体已释放或正在释放时直接返回
            if (this.IsDisposed || this.Disposing) return;

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
            if (_connectingForm != null) return;

            _connectingForm = new Form
            {
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                ControlBox = false,           // 不显示关闭按钮（连接期间不可取消）
                ShowInTaskbar = false,
                ClientSize = new Size(300, 80),
                Text = "连接中"
            };
            _connectingForm.Controls.Add(new Label
            {
                Text = $"正在连接{deviceName}，请稍候...",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            });

            // 禁用主窗体，防止连接期间重复点击（连接只影响自身，后台采集照常进行）
            this.Enabled = false;
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
                _connectingForm.Dispose();
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
                MessageBox.Show("耦合器未连接，请先连接（阀/载台上电等操作暂不可用）", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
        /// 菜单项：公共参数 / 配方管理
        /// </summary>
        private void btnParameter_Click(object sender, EventArgs e)
        {
            ShowDropdownPopup(btnParameter, new (string, EventHandler)[]
            {
                ("公共参数", MenuParamCommon_Click),
                ("配方管理", MenuParamRecipe_Click)
            });
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

            ShowDropdownPopup(btnAbout, items.ToArray());
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
                DialogResult result = loginForm.ShowDialog(this);

                if (result == DialogResult.OK)
                {
                    // 登录成功，更新权限显示
                    string roleName = GetRoleDisplayName(targetRole);
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
        /// 更新权限显示（【V1.19.7】）
        /// 拆为"前缀 + 角色名"两个标签（panelPermission 内 FlowLayoutPanel 水平排列）：
        /// 前缀 lblPermissionPrefix 固定默认黑字；角色名 lblPermissionRole 按权限设置 ForeColor：
        /// - 管理员 → 红色（Red）
        /// - 技术员 → 深蓝色（RoyalBlue，V1.47 起由天蓝加深，更醒目）
        /// - 操作员 → 绿色（Green）
        /// - 未知角色 → 默认文字色
        /// </summary>
        /// <param name="roleName">角色中文名（操作员/技术员/管理员）</param>
        private void UpdatePermissionDisplay(string roleName)
        {
            Color roleColor;
            switch (roleName)
            {
                case "管理员":
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
        /// 【新增】根据当前权限更新按钮可用状态
        ///
        /// 【权限规则】
        /// - 参数设置（btnParameter）：技术员或管理员可操作（包含配方管理）
        /// - 其他按钮：所有权限均可操作
        ///
        /// 【视觉效果】
        /// - 不可用时：按钮变灰（Enabled=false）
        /// - 可用时：按钮正常显示（Enabled=true）
        /// </summary>
        private void UpdateButtonPermissionStates()
        {
            // 检查是否拥有技术员及以上权限
            bool canAccessSettings = _userManager.HasPermission(UserRole.Technician);

            // 参数设置按钮（包含配方管理）
            btnParameter.Enabled = canAccessSettings;

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
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    WriteLog("所有气压表负压阈值设置完成");
                }
            }
        }

        /// <summary>
        /// 配方管理 → 弹出配方管理窗体
        /// </summary>
        private void MenuParamRecipe_Click(object sender, EventArgs e)
        {
            using (var form = new RecipeManagerForm(_recipes))
            {
                form.ShowDialog(this);
            }
        }

        /// <summary>
        /// 加载本地持久化的配方列表（Recipes.json）
        /// 由"配方管理"窗体的"保存设置"写入；文件不存在或加载失败时保持空列表
        /// </summary>
        private void LoadRecipes()
        {
            var loaded = RecipeStorage.Load();
            if (loaded != null && loaded.Count > 0)
            {
                _recipes.Clear();
                _recipes.AddRange(loaded);
            }
        }

        #endregion

        #region LOG记录菜单项

        /// <summary>
        /// 历史记录 → 弹出历史记录查询窗体
        /// </summary>
        private void MenuLogHistory_Click(object sender, EventArgs e)
        {
            using (var form = new HistoryRecordForm())
            {
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

            using (var form = new SettingsForm(_config))
            {
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
            // 若现场从未保存过 HomeLayout.json，则当前生效的就是本窗体的默认值
            // （DefaultRightPanelWidth=300），编辑器里应显示这个值而不是
            // HomeLayoutConfig 的类默认（260），否则会出现"编辑器里显示 260、
            // 主界面实际 300"的偏差。因此这里手动把未配置项补成 MainForm 默认。
            var layout = HomeLayoutConfig.LoadOrDefault();
            if (!System.IO.File.Exists(HomeLayoutConfig.GetConfigPath()))
            {
                layout.RightPanelWidth = DefaultRightPanelWidth;
            }

            using (var form = new HomeLayoutEditorForm(layout))
            {
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
        /// </summary>
        private void MenuHelpCommunicationTest_Click(object sender, EventArgs e)
        {
            var form = new Dialogs.CommunicationTestForm(_deviceManager);
            form.FormClosed += (s, args) => form.Dispose();
            form.Show(this);
        }

        /// <summary>
        /// 送风机测试 → 弹出冷却送风机通讯测试窗体（技术员及以上权限）
        /// 用于手动测试送风机控制屏的 Modbus TCP 通讯与定值启动/停止（直接读写设备寄存器）
        /// 非模态（Show 替代 ShowDialog），打开测试窗体的同时仍可点击操作主窗体。
        /// </summary>
        private void MenuHelpFanTest_Click(object sender, EventArgs e)
        {
            var form = new Dialogs.FanTestForm(_deviceManager);
            form.FormClosed += (s, args) => form.Dispose();
            form.Show(this);
        }

        /// <summary>
        /// 版本说明 → 弹出版本信息对话框（V1.19.12 更名：MenuHelpAbout_Click → MenuHelpVersionInfo_Click，菜单项"关于"改"版本说明"）
        /// </summary>
        private void MenuHelpVersionInfo_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
                "老化测试系统 V1.16\n\n" +
                "运行环境: .NET Framework 4.7.2\n" +
                "开发框架: WinForms\n\n" +
                "功能特性:\n" +
                "- 支持72个气压表实时监控（Modbus RTU）\n" +
                "- GX-CL140 IO 耦合器（Modbus TCP）：真空阀 + 载台上电\n" +
                "- 冷却送风机接入（Modbus TCP）：定值启动/停止 + 温度湿度监视\n" +
                "- 老化测试业务流程：启动/停止/报警复位/急停\n" +
                "- 报警联动：真空越限/通讯失联/真空未建立 → 关阀断电\n" +
                "- 老化计时自动停止 + 事件 CSV 落盘追溯\n" +
                "- 用户权限管理（操作员/技术员/管理员）\n\n" +
                "版权所有 © 2024",
                "版本说明",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        #endregion

        #endregion

        #region 右侧操作按钮事件处理

        /// <summary>
        /// 送风机定值启动按钮点击（【V1.10】由原"温控操作"按钮改造）
        /// 让送风机按控制屏设定温度运行（厂商自动控温）
        /// </summary>
        private async void btnTemperatureControl_Click(object sender, EventArgs e)
        {
            // 【V1.16.2】送风机未连时先异步按需重连（弹"连接中"），连不上弹窗提示
            if (_deviceManager.IsFanEnabled && !_deviceManager.IsFanConnected)
            {
                if (!await EnsureFanReadyAsync())
                {
                    MessageBox.Show("送风机未连接，请先连接（定值启动失败）", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            // 已连上，命令直接下发（StartFan 内部已连接则不再重复连接）
            bool ok = _deviceManager.StartFan();
            if (!ok)
            {
                MessageBox.Show("送风机未连接，请先连接（定值启动失败）", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            WriteLog("送风机定值启动命令已发送");
        }

        /// <summary>
        /// 送风机定值停止按钮点击（【V1.10 新增】）
        /// 【注意】如果有任何一台正在测试，采集循环会自动重新启动送风机
        /// （送风机是环境设备，测试期间必须保持运行）。
        /// </summary>
        private async void btnFanStop_Click(object sender, EventArgs e)
        {
            // 【V1.16.2】送风机未连时先异步按需重连（弹"连接中"），连不上弹窗提示
            if (_deviceManager.IsFanEnabled && !_deviceManager.IsFanConnected)
            {
                if (!await EnsureFanReadyAsync())
                {
                    MessageBox.Show("送风机未连接，请先连接（定值停止失败）", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            // 已连上，命令直接下发（StopFan 内部已连接则不再重复连接）
            bool ok = _deviceManager.StopFan();
            if (!ok)
            {
                MessageBox.Show("送风机未连接，请先连接（定值停止失败）", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            WriteLog("送风机定值停止命令已发送");
        }

        /// <summary>
        /// 开启真空按钮点击（【V1.10】接真实业务）
        /// 对选中的面板打开真空电磁阀（只做单动作，供预检/手动使用；
        /// "启动运行"是开真空 + 载台上电的组合快捷入口）
        /// </summary>
        private async void btnVacuum_Click(object sender, EventArgs e)
        {
            int[] ids = GetSelectedDeviceIds();
            if (ids == null) return;

            // 【V1.16.2】开真空阀需要耦合器：先异步连接（弹"连接中"），连不上弹窗提示
            if (!await EnsureIoReadyAsync()) return;

            foreach (int deviceId in ids)
            {
                // 真空电磁阀内部编号 = TotalInputs + deviceId
                _deviceManager.SetOutput(_config.TotalInputs + deviceId, true);
            }
            WriteLog($"开启真空（{ids.Length} 台）");
        }

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
                    MessageBox.Show("扫码枪未连接，请先连接（不影响手动输入批号/SN）", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                DialogResult result = form.ShowDialog(this);

                // 用户关闭窗口后，处理录入结果
                if (result == DialogResult.OK)
                {
                    string lotNumber = form.GetLotNumber();
                    WriteLog($"[录入批号] 批号录入成功: {lotNumber}");
                    MessageBox.Show(
                        $"批号录入成功！\n\n录入的批号: {lotNumber}\n\n【预留】批号将用于标识当前生产批次，便于后续追溯和数据分析。",
                        "录入批号",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    WriteLog("[录入批号] 用户取消了批号录入");
                }
            }
        }

        /// <summary>
        /// 启动运行按钮点击（【V1.10】接真实业务）
        /// 对选中的面板执行：开真空阀 + 载台上电 + 进入测试中 + 送风机定值启动（首台）
        /// 【V1.16.2】异步：连接耦合器/送风机时弹"连接中"，不卡界面
        /// </summary>
        private async void btnStartRun_Click(object sender, EventArgs e)
        {
            int[] ids = GetSelectedDeviceIds();
            if (ids == null) return;

            DialogResult r = MessageBox.Show(
                $"确认启动 {ids.Length} 台老化测试？\n\n" +
                "将执行：\n" +
                "1. 开启真空电磁阀（建立负压固定产品）\n" +
                "2. 载台上电（给产品供电）\n" +
                "3. 送风机定值启动（保持环境温控）\n\n" +
                "注：开阀后若真空长时间未建立会自动报警断电。",
                "启动运行",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;

            // 【V1.16.2】启动测试需要耦合器（开阀+载台上电）：先异步连接（弹"连接中"），连不上弹窗提示
            if (!await EnsureIoReadyAsync()) return;

            // 【V1.16.2】启动测试依赖送风机保持温控：送风机没连上时给一次异步按需重连
            //（弹"连接中"），仍连不上则提示（不阻断测试，但操作员要知道没有温控）。
            if (_deviceManager.IsFanEnabled && !_deviceManager.IsFanConnected)
            {
                bool fanOk = await EnsureFanReadyAsync();
                if (!fanOk)
                {
                    MessageBox.Show("送风机未连接，请先连接（测试仍会启动，但老化过程没有环境温控）", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

            DialogResult r = MessageBox.Show(
                $"确认停止 {ids.Length} 台的运行？\n\n将执行：\n1. 关闭真空电磁阀\n2. 断开载台上电",
                "停止运行",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;

            // 【V1.16.2】停止测试需要耦合器（关阀+断载台电）：先异步连接，连不上弹窗提示
            if (!await EnsureIoReadyAsync()) return;

            _deviceManager.StopTesting(ids);
            WriteLog($"停止运行（{ids.Length} 台）");
        }

        /// <summary>
        /// 报警复位按钮点击（【V1.10 新增】）
        /// 对选中的报警/故障面板执行人工复位：清除故障标记，回到空闲，可重新启动。
        /// 【设计说明】报警后不自动恢复，必须人工确认（防止真空失效原因未确认就重启）。
        /// </summary>
        private void btnResetAlarm_Click(object sender, EventArgs e)
        {
            int[] ids = GetSelectedDeviceIds();
            if (ids == null) return;

            DialogResult r = MessageBox.Show(
                $"确认复位 {ids.Length} 台的报警状态？\n\n" +
                "将清除故障标记，设备回到空闲状态，可重新启动老化测试。",
                "报警复位",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;

            _deviceManager.ResetDevices(ids);
            WriteLog($"报警复位（{ids.Length} 台）");
        }

        /// <summary>
        /// 全部停止（急停）按钮点击（【V1.10 新增】）
        /// 一键关闭所有真空阀 + 断开所有载台上电 + 停止送风机，带防误触确认。
        /// </summary>
        private async void btnStopAll_Click(object sender, EventArgs e)
        {
            DialogResult r = MessageBox.Show(
                "确认【全部停止】？\n\n" +
                "将执行：\n" +
                "1. 关闭所有 72 路真空电磁阀\n" +
                "2. 断开所有 72 路载台上电\n" +
                "3. 停止送风机\n\n" +
                "此操作不可撤销，请确认现场安全！",
                "全部停止（急停）",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;

            // 【V1.16.2】急停需要耦合器（关阀+断载台电）：先异步连接；连不上要明确告诉
            // 操作员，否则可能误以为阀门已关闭（安全提示）。
            if (!await EnsureIoReadyAsync()) return;

            _deviceManager.StopAll();
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
                MessageBox.Show("请先在气压表区域选中要操作的设备\n（点击面板或用行全选按钮）",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            WriteLog($"[扫码枪] {message}");

            // 【V1.16.2】扫码枪连接状态变化 → 刷新状态栏显示（已连接/未连接/未启用）
            RefreshScannerStatus();
        }

        /// <summary>
        /// 刷新状态栏"扫码枪"连接状态（【V1.16.2 新增】）
        /// 让操作员在底部状态栏一眼看到扫码枪当前连接状态：
        /// - 已连接 = 绿；未连接 = 红；未启用（App.config 关掉）= 灰。
        /// 与顶部"通讯连接状态"（耦合器）、送风机状态标签一起，
        /// 构成完整的设备连接状态总览（四个设备断了哪个一眼可见）。
        /// </summary>
        private void RefreshScannerStatus()
        {
            if (this.IsDisposed || this.Disposing) return;

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
        /// 【预留说明】
        /// 当前日志仅在UI界面显示，未持久化到文件。
        /// 实际项目中应：
        /// 1. 同时写入日志文件（按日期分文件）
        /// 2. 支持日志级别（INFO/WARN/ERROR）
        /// </summary>
        /// <param name="message">日志消息</param>
        private void WriteLog(string message)
        {
            // 拼接时间戳，格式：[2024-01-01 12:00:00] 消息内容
            string logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\r\n";

            // 【修复 M8】限制日志文本框最大字符数，避免长时间运行后 GDI 句柄耗尽或卡顿
            // 超过上限时裁剪掉旧内容，只保留最近一半
            if (txtLog.TextLength > MaxLogTextLength)
            {
                txtLog.Text = txtLog.Text.Substring(txtLog.TextLength - LogTrimKeepLength);
            }

            // 追加到日志文本框
            txtLog.AppendText(logLine);

            // 【预留】持久化到日志文件
            // TODO: System.IO.File.AppendAllText(logFilePath, logLine);
        }

        /// <summary>
        /// 窗体关闭事件
        /// 释放设备管理器资源（停止定时器、断开硬件连接）
        /// DeviceManager 实现了 IDisposable，调用 Dispose 释放所有资源
        /// </summary>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
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
        }
    }
}
