using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using BarometerWinform.Dialogs;
using BarometerWinform.Models;
using BarometerWinform.Services;

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

namespace BarometerWinform.Views
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
    /// │   WorkstationPanelView × 72    │ 监视                    │
    /// │   (9列 × 8行布局)           │ 设置温度: [D4700]       │
    /// │                              │ 上部温度: [D4702]       │
    /// │                              │ 下部温度: [D4704]       │
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
        /// 存储所有工位显示面板
        /// Key: 设备编号，Value: 显示面板控件
        /// 用于快速查找指定设备的面板进行数据更新
        /// </summary>
        private readonly Dictionary<int, WorkstationPanelView> _panelViews = new Dictionary<int, WorkstationPanelView>();

        /// <summary>
        /// 每行的"全选/取消"按钮（行索引 → 按钮，【V1.19 新增】）
        /// 供 UpdateRowSelectButton 在任意单个面板选中状态变化时刷新按钮文字：
        /// 仅当该行所有面板都选中时按钮才显示"取消"，否则显示"全选"。
        /// 行索引从 0 开始，与 _panelViews 按 (deviceId-1)/cols 换算的行号一致。
        /// </summary>
        private readonly Dictionary<int, Button> _rowSelectButtons = new Dictionary<int, Button>();

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
        /// 配方列表（内存中维护）
        /// 实际项目中应替换为持久化存储（数据库/文件）
        /// 当前为空列表，通过"配方管理"窗体维护
        /// </summary>
        private readonly List<RecipeConfig> _recipes = new List<RecipeConfig>();

        // 注意：原 ContextMenuStrip 字段已删除
        // 改用 ShowDropdownPopup 方法在按钮点击时动态创建无边框弹出窗体
        // 这样下拉菜单项的尺寸可以和主按钮完全一致

        // ===== 布局相关常量（修复 L6：避免魔法数字散落代码各处） =====
        /// <summary>行全选按钮列的固定宽度（像素）</summary>
        private const int RowSelectButtonColumnWidth = 80;
        /// <summary>工位面板的行高（像素），包含面板高度和上下边距（V1.16 加高容纳新布局；V1.19.12 随面板高度 225→205 同步减小）</summary>
        private const int PanelRowHeight = 225;
        /// <summary>
        /// 工位面板的列宽（像素）= 面板设计宽度240 + 左右边距4 + 边框余量1
        /// 【说明】使用绝对列宽而非百分比，确保每个单元格足够宽容纳面板内容，
        ///        避免窗口缩小时面板被压缩导致内容显示不全。
        ///        窗口宽度不够时由 TableLayoutPanel.AutoScroll 显示水平滚动条。
        ///        V1.16 工位面板重新设计后加宽到 245。
        /// </summary>
        private const int PanelColumnWidth = 245;
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

            // 1.5 自适应调整右侧操作面板宽度（不写死, 根据内容自动计算）
            AdjustRightPanelWidth();

            // 2. 加载配置（从 App.config 读取设备数量、采集间隔等）
            _config = LoadConfig();

            // 3. 初始化设备管理器（连接硬件、启动数据采集）
            _deviceManager = new DeviceManager(_config);

            // 4. 订阅设备管理器事件（批量数据更新、连接状态变更、送风机数据更新）
            // 【修复 M2】改为订阅批量数据更新事件，一次更新所有面板
            _deviceManager.OnBatchDataUpdated += DeviceManager_OnBatchDataUpdated;
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

            // 5. 更新权限显示（V1.19.7：角色名着色——管理员=红/技术员=天蓝/操作员=绿）
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
        /// 【原理】
        /// 1. 临时将 tableLayoutPanelRight 切换为 AutoSize + Dock=None，
        ///    让 WinForms 自动计算包裹所有内容所需的最小宽度
        /// 2. 读取自动计算的宽度值
        /// 3. 恢复 Dock=Fill，让面板重新填满 Panel2
        /// 4. 设置 SplitterDistance 使 Panel2 宽度 = 内容最小宽度
        /// 5. FixedPanel=Panel2（已在 Designer 中设置）确保窗口缩放时右侧宽度不变
        ///
        /// 【优点】
        /// - 不写死宽度数字，内容变化时自动适应
        /// - 最终效果为 Dock=Fill，无空白区域
        /// </summary>
        private void AdjustRightPanelWidth()
        {
            // 1. 临时切换为 AutoSize 模式，让 TableLayoutPanel 自动计算内容所需宽度
            tableLayoutPanelRight.Dock = DockStyle.None;
            tableLayoutPanelRight.AutoSize = true;
            tableLayoutPanelRight.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            // 2. 触发布局计算，让 AutoSize 立即生效
            tableLayoutPanelRight.PerformLayout();

            // 3. 读取自动计算的宽度（刚好包裹内容的最小宽度）
            int contentWidth = tableLayoutPanelRight.Width;

            // 4. 恢复 Dock=Fill，让 TableLayoutPanel 重新填满 Panel2
            tableLayoutPanelRight.AutoSize = false;
            tableLayoutPanelRight.AutoSizeMode = AutoSizeMode.GrowOnly;
            tableLayoutPanelRight.Dock = DockStyle.Fill;

            // 5. 设置 SplitterDistance，让 Panel2 宽度 = 内容最小宽度
            //    Panel2 宽度 = splitContainerMain 总宽 - SplitterDistance - 分隔条宽度
            //    => SplitterDistance = 总宽 - 内容宽度 - 分隔条宽度
            splitContainerMain.SplitterDistance =
                splitContainerMain.Width - contentWidth - splitContainerMain.SplitterWidth;
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

            // 备用通道映射表：格式 "0x2000@0->0x2009@10;0x2008@0->0x2009@11"
            // 解析失败的项会跳过（不影响合法项）；若有解析问题且开关为 true，额外记一条告警日志方便排查。
            string ioBackupMappingsRaw = System.Configuration.ConfigurationManager.AppSettings["IoBackupChannelMappings"];
            if (!string.IsNullOrWhiteSpace(ioBackupMappingsRaw))
            {
                config.IoBackupChannelMappings = BarometerWinform.Models.IoOutputChannelRemap.ParseAll(ioBackupMappingsRaw, out string parseError);
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
        /// 在窗体显示之前完成动态控件的创建和设备启动
        /// </summary>
        private void MainForm_Load(object sender, EventArgs e)
        {
            // 动态创建工位显示面板（根据配置的设备数量）
            CreateWorkstationPanels();

            // 启动设备管理器（开始数据采集）
            // 【V1.16】Start 只要求"气压表串口"连通；耦合器/送风机断开不影响压力采集，
            // 具体哪一步连不上会通过 OnDiagnostic 事件写进 LOG。
            bool started = _deviceManager.Start();
            if (!started)
            {
                // 启动失败（气压表串口没连上）：把原因写到 LOG，方便现场排查
                WriteLog($"设备启动失败：{_deviceManager.LastStartupError}");
            }

            // 【V1.16.1】顶部"通讯连接状态"只反映 IO 耦合器（阀/载台电控制）是否连接，
            // 不再用"气压表串口是否连上"冒充。Start() 内部已同步触发
            // OnConnectionStatusChanged 事件（数据源 = 耦合器），这里再按实际状态兜底刷新一次。
            _commConnected = _deviceManager.IsIoConnected;
            UpdateConnectionStatus();

            // 【V1.16 新增】启动扫码枪服务（自动识别串口并连接；未启用/未插入时定时重连）
            // 扫码枪是可选设备，内部已做"ScannerEnabled=false 直接跳过"处理，不影响整机启动
            _scanner?.Start();

            // 【V1.16.2】刷新状态栏"扫码枪"连接状态（已连接/未连接/未启用）
            RefreshScannerStatus();

            // 启动定时器更新状态栏时间显示
            timerTime.Start();
        }

        /// <summary>
        /// 动态创建工位显示面板（V1.16 更名：本质是 72 个工位，每个工位对应一台气压表）
        /// 根据配置的设备数量（默认72个）创建对应的工位面板
        /// 面板按行列网格布局排列在中间区域
        ///
        /// 【布局说明】
        /// 使用 TableLayoutPanel 实现网格布局（8列×9行=72个面板 + 1列行全选按钮）
        /// 共 9 列：前 8 列放工位面板（固定宽度），最后 1 列放行全选按钮（固定宽度）
        /// 直接将 TableLayoutPanel 添加到 splitContainerMain.Panel1
        /// 【注意】不能放在 FlowLayoutPanel 中，因为 FlowLayoutPanel
        /// 不尊重子控件的 Dock=Fill 属性，会导致 TableLayoutPanel 尺寸为0
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
            // 清空左侧面板容器和字典（防止重复调用时残留）
            splitContainerMain.Panel1.Controls.Clear();
            _panelViews.Clear();

            // 创建 TableLayoutPanel 作为面板容器，实现网格布局
            var tableLayoutPanel = new TableLayoutPanel();
            tableLayoutPanel.Dock = DockStyle.Fill;  // 填满整个左侧区域
            tableLayoutPanel.AutoScroll = true;      // 内容超出时显示滚动条

            // 设置行列数（根据配置：默认8列9行）
            int rows = _config.PanelRows;
            int cols = _config.PanelColumns;

            // 列数 = 工位列数 + 1（额外一列用于放置行全选按钮）
            tableLayoutPanel.ColumnCount = cols + 1;
            tableLayoutPanel.RowCount = rows;

            // 设置前 cols 列宽（绝对值，确保每个单元格足够宽容纳面板内容）
            // 【修复】之前用百分比列宽，窗口缩小时单元格会压缩到~107px，
            //        远小于面板设计宽度，导致面板重叠、内容被裁剪。
            //        改用绝对列宽（V1.16 工位面板加宽到 245），确保单元格始终足够宽。
            for (int i = 0; i < cols; i++)
            {
                tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, PanelColumnWidth));
            }
            // 最后一列：行全选按钮列，使用绝对宽度
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, RowSelectButtonColumnWidth));

            // 设置行高（绝对值，每个面板高度固定 + 边距）
            // 使用 Absolute 固定行高，确保面板完整显示
            for (int i = 0; i < rows; i++)
            {
                tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, PanelRowHeight));
            }

            // 循环创建每个工位面板
            for (int i = 0; i < _config.TotalBarometers; i++)
            {
                int deviceId = i + 1;  // 设备编号从1开始

                // 创建面板实例（只需设备编号；载台上电输出编号由主窗体用 _config 计算）
                var panel = new WorkstationPanelView(deviceId);

                // 【修复】设置 Dock=Fill 让面板填满单元格，避免与相邻面板重叠
                panel.Dock = DockStyle.Fill;

                // 设置面板边距和内边距，避免面板挤在一起
                panel.Margin = new Padding(2);
                panel.Padding = new Padding(2);

                // 订阅面板的"设置"按钮点击事件（V1.18：打开工位设置窗口）
                panel.OnSetClicked += Panel_OnSetClicked;

                // 【V1.19】订阅面板选中状态变化事件：
                // 任一面板被单独选中/取消（V1.19.5：空白处长按约0.8秒选中 / 单击空白处或点击选中框取消）时，
                // 立即刷新所在行的"全选/取消"按钮文字——只要该行有一台被取消，
                // 按钮就从"取消"恢复为"全选"；
                // 同时刷新所有面板选中框的显示/隐藏（V1.19.5：有任一选中→全部显示，全未选中→全部隐藏）。
                // 【注意】row 是每轮 for 循环新建的局部变量，闭包捕获安全，无循环变量污染。
                int row = i / cols;
                panel.IsSelectedChanged += (s, e2) =>
                {
                    UpdateRowSelectButton(row);
                    UpdateSelectionBoxVisibility();
                };

                // 保存到字典，便于后续按设备编号查找更新
                _panelViews[deviceId] = panel;

                // 计算面板在网格中的行列位置
                int col = i % cols;

                // 添加到 TableLayoutPanel 的指定单元格
                tableLayoutPanel.Controls.Add(panel, col, row);
            }

            // 【V1.19.5】初始刷新一次选中框显示状态（启动时全部未选中 → 全部隐藏）
            UpdateSelectionBoxVisibility();

            // 在每行最后一列添加"行全选"按钮
            // 【V1.18】按钮名由 Set(SEL_N) 改为"全选"：点击后该行所有工位面板被选中
            // （选中状态通过右上角选中指示体现；V1.19.2 起面板背景色/工作状态不再变化）。
            // 按钮样式：浅灰背景（V1.19.4 由深灰 Gray 改为浅灰 LightGray，与面板上电状态灯 boxPower 同色，
            // 深灰显得笨重不好看；浅灰下文字改黑色以保证对比度）。
            for (int row = 0; row < rows; row++)
            {
                var btnSelectRow = new Button();
                btnSelectRow.Name = $"btnSelectRow_{row + 1}";              // 控件名（用于代码查找）
                btnSelectRow.Text = "全选";                                  // 显示文本（V1.18 更名）
                btnSelectRow.BackColor = Color.LightGray;                   // 背景色：浅灰（V1.19.4 与 boxPower 同色）
                btnSelectRow.ForeColor = Color.Black;                       // 文字颜色：黑色（浅灰底保证对比度）
                btnSelectRow.Dock = DockStyle.Fill;                         // 填满单元格（自动随窗体缩放）
                btnSelectRow.Margin = new Padding(2);                       // 外边距，避免按钮贴边
                btnSelectRow.FlatStyle = FlatStyle.Flat;                    // 扁平化样式，更现代
                btnSelectRow.Cursor = Cursors.Hand;                         // 鼠标悬停时显示手型光标

                // 通过闭包捕获行号，避免循环变量捕获问题
                // 【注意】不能直接用 row，否则所有按钮的点击事件都会拿到循环结束后的 row 值
                int capturedRow = row;
                btnSelectRow.Click += (sender, e) => BtnSelectRow_Click(sender, e, capturedRow);

                // 【V1.19】保存该行按钮引用，供 UpdateRowSelectButton 在选中状态变化时刷新文字
                _rowSelectButtons[capturedRow] = btnSelectRow;

                // 添加到最后一列（列索引 = cols）的对应行
                tableLayoutPanel.Controls.Add(btnSelectRow, cols, row);
            }

            // 将 TableLayoutPanel 直接添加到 splitContainerMain 的左侧面板
            // 【关键】不能用 FlowLayoutPanel，因为它不尊重 Dock=Fill
            splitContainerMain.Panel1.Controls.Add(tableLayoutPanel);
        }

        /// <summary>
        /// 行全选按钮点击事件（【V1.18】按钮名由 Set(SEL_N) 改为"全选"）
        /// 选中该行所有工位面板（V1.19.2 起选中仅通过右上角选中指示体现，
        /// 不再改变面板背景色与工作状态文字）。
        ///
        /// 【V1.19】按钮文字改为"实时反映该行当前选中状态"：
        /// - 该行所有面板都选中 → 按钮显示"取消"（此时点击执行整行取消选中）；
        /// - 只要有一台被单独取消选中 → 按钮立即恢复显示"全选"（此时点击执行整行全部选中）。
        /// 注意：按钮文字不是由本次点击简单取反，而是通过 UpdateRowSelectButton
        /// 重新计算该行所有面板的选中状态得来——任一面板被单独选中/取消（点击面板本身、
        /// 点击右上角选中指示）都会触发 IsSelectedChanged 事件刷新按钮文字。
        ///
        /// 【预留说明】
        /// 当前仅切换选中状态（选中指示 ✓ 显示/隐藏；V1.19.2 起面板背景色不再叠加高亮），
        /// 具体的批量操作（如批量设置配方、批量启动等）待业务流程明确后实现，
        /// 可通过遍历 _panelViews 找到所有 IsSelected=true 的面板执行批量操作。
        /// </summary>
        /// <param name="sender">触发事件的按钮</param>
        /// <param name="e">事件参数</param>
        /// <param name="rowIndex">行索引（从0开始）</param>
        private void BtnSelectRow_Click(object sender, EventArgs e, int rowIndex)
        {
            int cols = _config.PanelColumns;
            int rowStartDeviceId = rowIndex * cols + 1;  // 该行第一个设备编号
            int rowEndDeviceId = rowStartDeviceId + cols - 1;  // 该行最后一个设备编号

            // 检查该行是否所有面板都已选中
            bool allSelected = true;
            for (int deviceId = rowStartDeviceId; deviceId <= rowEndDeviceId; deviceId++)
            {
                if (_panelViews.TryGetValue(deviceId, out WorkstationPanelView panel))
                {
                    if (!panel.IsSelected)
                    {
                        allSelected = false;
                        break;
                    }
                }
            }

            // 切换选中状态：全选 ↔ 全不选
            bool newSelectionState = !allSelected;
            for (int deviceId = rowStartDeviceId; deviceId <= rowEndDeviceId; deviceId++)
            {
                if (_panelViews.TryGetValue(deviceId, out WorkstationPanelView panel))
                {
                    panel.IsSelected = newSelectionState;
                }
            }

            // 【V1.19】按钮文字交由 UpdateRowSelectButton 统一刷新：
            // 逐个设置 panel.IsSelected 时会触发 IsSelectedChanged 事件，
            // 该事件已在每次变化时调用 UpdateRowSelectButton 刷新按钮文字，
            // 这里再调用一次兜底，确保文字与最终状态一致。
            UpdateRowSelectButton(rowIndex);

            // 写入日志
            string action = newSelectionState ? "全选" : "取消全选";
            WriteLog($"第 {rowIndex + 1} 行 {action}（设备 {rowStartDeviceId}-{rowEndDeviceId}）");
        }

        /// <summary>
        /// 刷新指定行的"全选/取消"按钮文字（【V1.19 新增】）
        ///
        /// 【规则】按钮文字实时反映该行当前选中状态：
        /// - 该行**所有**工位面板都处于选中状态 → 按钮显示"取消"（点击整行取消选中）；
        /// - 只要有一台未被选中（含被单独取消）→ 按钮显示"全选"（点击整行全部选中）。
        ///
        /// 【触发时机】在以下任一时刻调用：
        /// 1) 单个面板选中状态变化：主窗体订阅了每个面板的 IsSelectedChanged 事件
        ///    （面板点击本身 / 点击右上角选中指示都会触发），事件处理器按所在行调用本方法；
        /// 2) 行全选按钮点击后：BtnSelectRow_Click 末尾调用本方法兜底刷新。
        /// </summary>
        /// <param name="rowIndex">行索引（从0开始）</param>
        private void UpdateRowSelectButton(int rowIndex)
        {
            // 找不到该行按钮（例如行按钮尚未创建）则直接返回
            if (!_rowSelectButtons.TryGetValue(rowIndex, out Button btnSelectRow)) return;

            int cols = _config.PanelColumns;
            int rowStartDeviceId = rowIndex * cols + 1;  // 该行第一个设备编号
            int rowEndDeviceId = rowStartDeviceId + cols - 1;  // 该行最后一个设备编号

            // 检查该行是否所有面板都已选中（TryGetValue 跳过不存在的面板，保证最后一行兼容）
            bool allSelected = true;
            for (int deviceId = rowStartDeviceId; deviceId <= rowEndDeviceId; deviceId++)
            {
                if (_panelViews.TryGetValue(deviceId, out WorkstationPanelView panel))
                {
                    if (!panel.IsSelected)
                    {
                        allSelected = false;
                        break;
                    }
                }
            }

            // 全部选中 → "取消"；否则 → "全选"
            btnSelectRow.Text = allSelected ? "取消" : "全选";
        }

        /// <summary>
        /// 刷新所有面板选中框的显示/隐藏（【V1.19.5】）
        ///
        /// 【规则】选中框"平时全隐藏，有选中才显示"：
        /// - 只要有任一工位被选中 → 所有面板都显示选中框（选中项=绿底白✓，未选中项=空心白框）；
        /// - 全部未选中 → 所有面板隐藏选中框。
        ///
        /// 【触发时机】在任一工位选中状态变化时（面板 IsSelectedChanged 事件）调用，
        /// 让"长按选中某台"或"行全选"后，其余项也能同步显示各自的选中框状态。
        /// </summary>
        private void UpdateSelectionBoxVisibility()
        {
            // 判断是否存在至少一个已选中的工位
            bool anySelected = false;
            foreach (var panel in _panelViews.Values)
            {
                if (panel.IsSelected)
                {
                    anySelected = true;
                    break;
                }
            }

            // 通知每个面板按"是否有选中"刷新选中框显示/隐藏
            foreach (var panel in _panelViews.Values)
            {
                panel.SetSelectionBoxVisible(anySelected);
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
        /// 批量更新所有面板数据显示
        /// 遍历数据数组，根据设备编号找到对应的面板并调用其 UpdateData 方法
        /// 一次调用完成所有面板更新，减少 UI 线程切换次数
        /// </summary>
        /// <param name="allData">本次采集的所有气压表数据</param>
        private void UpdateAllPanels(BarometerData[] allData)
        {
            // 窗体已释放则不更新
            if (this.IsDisposed || allData == null) return;

            foreach (var data in allData)
            {
                if (data != null && _panelViews.TryGetValue(data.DeviceId, out WorkstationPanelView panel))
                {
                    panel.UpdateData(data);
                }
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
        /// 弹出工位设置窗口：查看/设置该工位的状态、SN、配方、延时时间、启动时间、极限温度。
        /// </summary>
        private void Panel_OnSetClicked(object sender, int deviceId)
        {
            using (var form = new StationSettingsForm(_deviceManager, _config, deviceId))
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

            // 【通讯测试】仅技术员及以上权限可见（操作员不可见）
            if (_userManager.HasPermission(UserRole.Technician))
            {
                items.Add(("通讯测试", MenuHelpCommunicationTest_Click));
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
                    // V1.19.7：角色名着色（管理员=红/技术员=天蓝/操作员=绿）
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
        /// - 技术员 → 天蓝色（SkyBlue）
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
                    roleColor = Color.SkyBlue;
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
                form.ShowDialog(this);
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
            var form = new Dialogs.CommunicationTestForm(_config);
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
            using (var form = new BatchRecipeForm())
            {
                // 订阅配方加入事件，记录日志
                form.OnRecipeAdded += (sender2, recipe) =>
                {
                    WriteLog($"[批量设置配方] 配方 \"{recipe.Name}\" 已加入队列");
                };

                // 显示窗口（模态对话框，阻塞主窗口直到关闭）
                DialogResult result = form.ShowDialog(this);

                // 用户关闭窗口后，获取配方队列
                if (result == DialogResult.OK)
                {
                    var recipeQueue = form.GetRecipeQueue();
                    if (recipeQueue.Count > 0)
                    {
                        // 【预留】实际项目中应将配方队列应用到选中的工位面板
                        // 当前简化为提示信息，显示队列中的配方数量
                        WriteLog($"[批量设置配方] 窗口关闭，队列中共有 {recipeQueue.Count} 个配方");
                        MessageBox.Show(
                            $"批量设置配方窗口已关闭！\n\n" +
                            $"配方队列中共有 {recipeQueue.Count} 个配方待处理。\n\n" +
                            $"【预留功能】\n" +
                            $"后续实现：将队列中的配方批量应用到所有选中的工位面板",
                            "批量设置配方",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    else
                    {
                        WriteLog("[批量设置配方] 窗口关闭，配方队列为空");
                    }
                }
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
        /// 遍历所有面板，收集 IsSelected=true 的设备。
        /// 一个都没选时弹提示并返回 null。
        /// </summary>
        /// <returns>选中的设备编号数组；未选择时返回 null</returns>
        private int[] GetSelectedDeviceIds()
        {
            var ids = new List<int>();
            foreach (var kvp in _panelViews)
            {
                if (kvp.Value.IsSelected)
                {
                    ids.Add(kvp.Key);
                }
            }

            if (ids.Count == 0)
            {
                MessageBox.Show("请先在气压表区域选中要操作的设备\n（点击面板或用行全选按钮）",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }

            return ids.ToArray();
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
