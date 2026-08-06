using System;
using System.Collections.Generic;
using System.Drawing;
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
// - 通讯设置和参数设置按钮需要技术员或管理员权限才能操作

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
    /// │ [用户权限] [通信设置] [参数设置] [LOG记录] [TEST] [关于] │
    /// ├──────────────────────────────┬──────────────────────────┤
    /// │                              │ 运行状态                 │
    /// │                              │ ┌────────────────────┐   │
    /// │   气压表显示区域             │ │ 空闲/测试中(D4204)  │   │
    /// │   (动态加载72个面板)         │ └────────────────────┘   │
    /// │                              │                         │
    /// │   BarometerPanelView × 72    │ 监视                    │
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
    /// 与 BarometerPanelView.cs 同样的问题：.cs 文件包含中文字符但没有 UTF-8 BOM，
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
        /// 存储所有气压表显示面板
        /// Key: 设备编号，Value: 显示面板控件
        /// 用于快速查找指定设备的面板进行数据更新
        /// </summary>
        private readonly Dictionary<int, BarometerPanelView> _panelViews = new Dictionary<int, BarometerPanelView>();

        /// <summary>
        /// 当前操作权限（中文显示名：操作员/技术员/管理员）
        /// 初始为"操作员"（未登录状态），登录成功后更新为对应角色名
        /// </summary>
        private string _currentPermission = "操作员";

        /// <summary>
        /// PLC连接状态
        /// true=已连接，false=未连接
        /// </summary>
        private bool _plcConnected = false;

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
        /// <summary>气压表面板的行高（像素），包含面板高度和上下边距</summary>
        private const int PanelRowHeight = 220;
        /// <summary>
        /// 气压表面板的列宽（像素）= 面板设计宽度210 + 左右边距4 + 边框余量6
        /// 【说明】使用绝对列宽而非百分比，确保每个单元格足够宽容纳面板内容，
        ///        避免窗口缩小时面板被压缩导致内容显示不全。
        ///        窗口宽度不够时由 TableLayoutPanel.AutoScroll 显示水平滚动条。
        /// </summary>
        private const int PanelColumnWidth = 220;
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

            // 5. 更新权限显示
            lblPermission.Text = $"当前操作权限: {_currentPermission}";

            // 6. 更新状态栏信息
            UpdateStatusBar();

            // 【新增】7. 初始化按钮权限状态
            // 默认未登录（操作员权限），通讯设置和参数设置按钮不可用
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

            if (decimal.TryParse(System.Configuration.ConfigurationManager.AppSettings["AlarmPressureThresholdPa"], out decimal alarmThresholdPa))
            {
                config.AlarmPressureThresholdPa = alarmThresholdPa;
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
            // 动态创建气压表显示面板（根据配置的设备数量）
            CreateBarometerPanels();

            // 启动设备管理器（开始数据采集）
            bool started = _deviceManager.Start();
            if (started)
            {
                _plcConnected = true;
                UpdateConnectionStatus();
            }

            // 启动定时器更新状态栏时间显示
            timerTime.Start();
        }

        /// <summary>
        /// 动态创建气压表显示面板
        /// 根据配置的设备数量（默认72个）创建对应的显示面板
        /// 面板按行列网格布局排列在中间区域
        ///
        /// 【布局说明】
        /// 使用 TableLayoutPanel 实现网格布局（8列×9行=72个面板 + 1列行全选按钮）
        /// 共 9 列：前 8 列放气压表面板（固定宽度），最后 1 列放行全选按钮（固定宽度）
        /// 直接将 TableLayoutPanel 添加到 splitContainerMain.Panel1
        /// 【注意】不能放在 FlowLayoutPanel 中，因为 FlowLayoutPanel
        /// 不尊重子控件的 Dock=Fill 属性，会导致 TableLayoutPanel 尺寸为0
        /// </summary>
        private void CreateBarometerPanels()
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

            // 列数 = 气压表列数 + 1（额外一列用于放置行全选按钮）
            tableLayoutPanel.ColumnCount = cols + 1;
            tableLayoutPanel.RowCount = rows;

            // 设置前 cols 列宽（绝对值，确保每个单元格足够宽容纳面板内容）
            // 【修复】之前用百分比列宽，窗口缩小时单元格会压缩到~107px，
            //        远小于面板设计宽度210px，导致面板重叠、内容被裁剪。
            //        改用绝对列宽220px，确保单元格始终足够宽。
            for (int i = 0; i < cols; i++)
            {
                tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, PanelColumnWidth));
            }
            // 最后一列：行全选按钮列，使用绝对宽度
            tableLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, RowSelectButtonColumnWidth));

            // 设置行高（绝对值，每个面板高度固定215+边距）
            // 使用 Absolute 固定行高，确保面板完整显示
            for (int i = 0; i < rows; i++)
            {
                tableLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, PanelRowHeight));
            }

            // 循环创建每个气压表面板
            for (int i = 0; i < _config.TotalBarometers; i++)
            {
                int deviceId = i + 1;  // 设备编号从1开始

                // 创建面板实例（带设备编号、气压表总数、IO输入通道总数）
                // TotalInputs 参与“输出点内部编号”的计算（outputId 从 TotalInputs+1 开始）
                var panel = new BarometerPanelView(deviceId, _config.TotalBarometers, _config.TotalInputs);

                // 【修复】设置 Dock=Fill 让面板填满单元格，避免与相邻面板重叠
                panel.Dock = DockStyle.Fill;

                // 设置面板边距和内边距，避免面板挤在一起
                panel.Margin = new Padding(2);
                panel.Padding = new Padding(2);

                // 订阅面板的 Set 按钮点击事件
                panel.OnSetClicked += Panel_OnSetClicked;

                // 保存到字典，便于后续按设备编号查找更新
                _panelViews[deviceId] = panel;

                // 计算面板在网格中的行列位置
                int row = i / cols;
                int col = i % cols;

                // 添加到 TableLayoutPanel 的指定单元格
                tableLayoutPanel.Controls.Add(panel, col, row);
            }

            // 在每行最后一列添加"行全选"按钮
            // 按钮名格式：Set(SEL_1)、Set(SEL_2) ... Set(SEL_N)，N = PanelRows
            for (int row = 0; row < rows; row++)
            {
                var btnSelectRow = new Button();
                btnSelectRow.Name = $"btnSelectRow_{row + 1}";              // 控件名（用于代码查找）
                btnSelectRow.Text = $"Set(SEL_{row + 1})";                  // 显示文本，如 Set(SEL_1)
                btnSelectRow.BackColor = Color.DodgerBlue;                  // 背景色：道奇蓝（醒目）
                btnSelectRow.ForeColor = Color.White;                       // 文字颜色：白色
                btnSelectRow.Dock = DockStyle.Fill;                         // 填满单元格（自动随窗体缩放）
                btnSelectRow.Margin = new Padding(2);                       // 外边距，避免按钮贴边
                btnSelectRow.FlatStyle = FlatStyle.Flat;                    // 扁平化样式，更现代
                btnSelectRow.Cursor = Cursors.Hand;                         // 鼠标悬停时显示手型光标

                // 通过闭包捕获行号，避免循环变量捕获问题
                // 【注意】不能直接用 row，否则所有按钮的点击事件都会拿到循环结束后的 row 值
                int capturedRow = row;
                btnSelectRow.Click += (sender, e) => BtnSelectRow_Click(sender, e, capturedRow);

                // 添加到最后一列（列索引 = cols）的对应行
                tableLayoutPanel.Controls.Add(btnSelectRow, cols, row);
            }

            // 将 TableLayoutPanel 直接添加到 splitContainerMain 的左侧面板
            // 【关键】不能用 FlowLayoutPanel，因为它不尊重 Dock=Fill
            splitContainerMain.Panel1.Controls.Add(tableLayoutPanel);
        }

        /// <summary>
        /// 行全选按钮点击事件
        /// 切换该行所有气压表面板的选中状态
        /// 如果当前行有未选中的面板，则全部选中；如果已全部选中，则全部取消选中
        ///
        /// 【预留说明】
        /// 当前仅切换选中状态（视觉高亮），具体的批量操作（如批量设置配方、批量启动等）
        /// 待业务流程明确后实现，可通过遍历 _panelViews 找到所有 IsSelected=true 的面板执行批量操作
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
                if (_panelViews.TryGetValue(deviceId, out BarometerPanelView panel))
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
                if (_panelViews.TryGetValue(deviceId, out BarometerPanelView panel))
                {
                    panel.IsSelected = newSelectionState;
                }
            }

            // 写入日志
            string action = newSelectionState ? "全选" : "取消全选";
            WriteLog($"第 {rowIndex + 1} 行 {action}（设备 {rowStartDeviceId}-{rowEndDeviceId}）");
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
                if (data != null && _panelViews.TryGetValue(data.DeviceId, out BarometerPanelView panel))
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
        /// 更新送风机监视区显示（【V1.10 新增】）
        /// 显示：运行状态 / 当前温度 / 当前湿度 / 设定温度
        /// data 为 null 表示通讯失败/离线
        /// </summary>
        private void UpdateFanDisplay(FanData data)
        {
            if (this.IsDisposed) return;

            // 未启用送风机
            if (!_deviceManager.IsFanEnabled)
            {
                lblFanState.Text = "未启用";
                lblFanState.ForeColor = Color.Gray;
                txtSetTemp.Text = "---";
                txtUpperTemp.Text = "---";
                txtLowerTemp.Text = "---";
                return;
            }

            // 通讯失败 / 离线
            if (data == null)
            {
                lblFanState.Text = "离线";
                lblFanState.ForeColor = Color.Red;
                txtSetTemp.Text = "---";
                txtUpperTemp.Text = "---";
                txtLowerTemp.Text = "---";
                return;
            }

            // 运行状态文本 + 颜色
            string stateText;
            Color stateColor;
            switch (data.RunState)
            {
                case FanRunState.FixedValueRunning:
                    stateText = "定值运行中";
                    stateColor = Color.Green;
                    break;
                case FanRunState.ProgramRunning:
                    stateText = "程式运行中";
                    stateColor = Color.Green;
                    break;
                case FanRunState.FixedValueStopped:
                case FanRunState.ProgramStopped:
                    stateText = "已停止";
                    stateColor = Color.Gray;
                    break;
                default:
                    stateText = "未知";
                    stateColor = Color.Orange;
                    break;
            }
            lblFanState.Text = stateText;
            lblFanState.ForeColor = stateColor;

            // 温度 / 湿度 / 设定温度
            txtSetTemp.Text = $"{data.Temperature:F2} °C";
            txtUpperTemp.Text = $"{data.Humidity:F2} %";
            txtLowerTemp.Text = $"{data.TempSetpoint:F2} °C";

            // 【V1.10】送风机温度告警：超过配置上限 → 温度标红并记日志
            if (_config.FanTempAlarmLimitC > 0 && data.Temperature > _config.FanTempAlarmLimitC)
            {
                txtSetTemp.ForeColor = Color.Red;
                if (data.Temperature > _fanTempAlarmLoggedThreshold)
                {
                    // 只在第一次超过新阈值时记日志，避免每秒重复记录
                    _fanTempAlarmLoggedThreshold = data.Temperature;
                    WriteLog($"[送风机] 温度 {data.Temperature:F1}°C 超过告警上限 {_config.FanTempAlarmLimitC:F1}°C");
                }
            }
            else
            {
                txtSetTemp.ForeColor = Color.Black;
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
            _plcConnected = isConnected;
            UpdateConnectionStatus();
        }

        /// <summary>
        /// 更新顶部 PLC 连接状态显示
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

                lblPlcStatus.Text = _plcConnected ? "已连接" : "未连接";
                lblPlcStatus.ForeColor = _plcConnected ? Color.Green : Color.Red;
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
        /// 面板 Set 按钮点击事件处理（【V1.10】由占位弹窗改为单台手动控制）
        /// 弹出单台手动控制窗口：实时查看该台 DI 报警触点状态，并可手动开/关阀、载台上电。
        /// 【用途】现场接线条点对应、排查单台故障时非常有用。
        /// </summary>
        private void Panel_OnSetClicked(object sender, int deviceId)
        {
            using (var form = new DeviceManualForm(_deviceManager, _config, deviceId))
            {
                form.ShowDialog(this);
            }
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
        /// 通信设置按钮点击 → 显示通讯设置下拉菜单
        /// 菜单项：PLC通讯设置
        /// </summary>
        private void btnCommunication_Click(object sender, EventArgs e)
        {
            ShowDropdownPopup(btnCommunication, new (string, EventHandler)[]
            {
                ("PLC通讯设置", MenuCommPlc_Click)
            });
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
        /// LOG记录按钮点击 → 显示LOG记录下拉菜单
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
        /// TEST按钮点击 → 显示TEST下拉菜单
        /// 菜单项：扫码模拟
        /// </summary>
        private void btnTest_Click(object sender, EventArgs e)
        {
            ShowDropdownPopup(btnTest, new (string, EventHandler)[]
            {
                ("扫码模拟", MenuTestScan_Click)
            });
        }

        /// <summary>
        /// 关于按钮点击 → 显示关于下拉菜单
        /// 菜单项：版本说明
        /// </summary>
        private void btnAbout_Click(object sender, EventArgs e)
        {
            ShowDropdownPopup(btnAbout, new (string, EventHandler)[]
            {
                ("版本说明", MenuAboutVersion_Click)
            });
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
                    lblPermission.Text = $"当前操作权限: {roleName}";

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
        /// 【新增】根据当前权限更新按钮可用状态
        ///
        /// 【权限规则】
        /// - 通讯设置（btnCommunication）：技术员或管理员可操作
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

            // 通讯设置按钮
            btnCommunication.Enabled = canAccessSettings;
            // 参数设置按钮（包含配方管理）
            btnParameter.Enabled = canAccessSettings;

            // 【预留】其他需要权限控制的按钮可在此处添加
            // TODO: 根据业务需求补充其他按钮的权限控制
        }

        #endregion

        #region 通讯设置菜单项

        /// <summary>
        /// PLC通讯设置 → 弹出PLC通讯设置窗体
        /// </summary>
        private void MenuCommPlc_Click(object sender, EventArgs e)
        {
            using (var form = new CommunicationSettingForm(_config))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    WriteLog("PLC通讯设置已更新（仅在内存中生效）");
                    // 【预留】保存后应重启设备管理器使配置生效
                    // TODO: 实现 _deviceManager 重启逻辑或重新加载配置
                }
            }
        }

        #endregion

        #region 参数设置菜单项

        /// <summary>
        /// 公共参数 → 弹出公共参数设置窗体
        /// </summary>
        private void MenuParamCommon_Click(object sender, EventArgs e)
        {
            using (var form = new CommonParameterForm(_config))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    WriteLog($"公共参数已更新，采集间隔: {_config.CollectInterval}ms");
                    // 【预留】保存后应更新设备管理器的采集间隔
                    // TODO: 实现 _deviceManager 更新采集间隔的逻辑
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

        #region TEST菜单项

        /// <summary>
        /// 扫码模拟 → 弹出扫码模拟窗体
        /// </summary>
        private void MenuTestScan_Click(object sender, EventArgs e)
        {
            using (var form = new ScanSimulationForm())
            {
                // 订阅扫码完成事件，将扫码结果写入日志
                form.OnScanCompleted += (sender2, barcode) =>
                {
                    WriteLog($"[扫码模拟] 扫码内容: {barcode}");
                    // 【预留】实际项目中应根据业务处理扫码结果
                    // TODO: 根据扫码内容匹配设备/配方，触发对应业务流程
                };

                form.ShowDialog(this);
            }
        }

        #endregion

        #region 关于菜单项

        /// <summary>
        /// 版本说明 → 弹出版本信息对话框
        /// </summary>
        private void MenuAboutVersion_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
                "老化测试系统 V1.15\n\n" +
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
        private void btnTemperatureControl_Click(object sender, EventArgs e)
        {
            bool ok = _deviceManager.StartFan();
            WriteLog(ok ? "送风机定值启动命令已发送" : "送风机定值启动失败（请检查通讯）");
        }

        /// <summary>
        /// 送风机定值停止按钮点击（【V1.10 新增】）
        /// 【注意】如果有任何一台正在测试，采集循环会自动重新启动送风机
        /// （送风机是环境设备，测试期间必须保持运行）。
        /// </summary>
        private void btnFanStop_Click(object sender, EventArgs e)
        {
            bool ok = _deviceManager.StopFan();
            WriteLog(ok ? "送风机定值停止命令已发送" : "送风机定值停止失败（请检查通讯）");
        }

        /// <summary>
        /// 开启真空按钮点击（【V1.10】接真实业务）
        /// 对选中的面板打开真空电磁阀（只做单动作，供预检/手动使用；
        /// "启动运行"是开真空 + 载台上电的组合快捷入口）
        /// </summary>
        private void btnVacuum_Click(object sender, EventArgs e)
        {
            int[] ids = GetSelectedDeviceIds();
            if (ids == null) return;

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
                        // 【预留】实际项目中应将配方队列应用到选中的气压表面板
                        // 当前简化为提示信息，显示队列中的配方数量
                        WriteLog($"[批量设置配方] 窗口关闭，队列中共有 {recipeQueue.Count} 个配方");
                        MessageBox.Show(
                            $"批量设置配方窗口已关闭！\n\n" +
                            $"配方队列中共有 {recipeQueue.Count} 个配方待处理。\n\n" +
                            $"【预留功能】\n" +
                            $"后续实现：将队列中的配方批量应用到所有选中的气压表面板",
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
            using (var form = new InputLotForm())
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
        /// </summary>
        private void btnStartRun_Click(object sender, EventArgs e)
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

            _deviceManager.StartTesting(ids);
            WriteLog($"启动老化测试（{ids.Length} 台）");
        }

        /// <summary>
        /// 停止运行按钮点击（【V1.10 新增】）
        /// 对选中的面板执行：关真空阀 + 断载台上电 + 退出测试中
        /// （最后一台停止时送风机自动停止）
        /// </summary>
        private void btnStopRun_Click(object sender, EventArgs e)
        {
            int[] ids = GetSelectedDeviceIds();
            if (ids == null) return;

            DialogResult r = MessageBox.Show(
                $"确认停止 {ids.Length} 台的运行？\n\n将执行：\n1. 关闭真空电磁阀\n2. 断开载台上电",
                "停止运行",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;

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
        private void btnStopAll_Click(object sender, EventArgs e)
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
            }

            // 释放设备管理器（内部会调用 Stop 停止定时器和断开连接）
            _deviceManager?.Dispose();

            // 注意：下拉菜单改为动态创建的弹出窗体（Form），点击菜单项或失去焦点后自动关闭
            // 不需要在这里手动释放，Form.Close 会触发 Dispose
        }
    }
}
