using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AgingTestSystem.Controls;
using AgingTestSystem.Models;
using AgingTestSystem.Services;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 系统设置窗口 —— 业务逻辑部分
    ///
    /// 【功能说明】
    /// 把 App.config 里分散的配置项按【业务分类】单页纵向展示（不使用选项卡），
    /// 所有分类合并到一个 UIDataGridView，分类标题用"分组标题行"（浅蓝底深蓝粗体）分隔：
    /// - 设置名称（配置项 key，只读）
    /// - 说明（每个配置项的中文含义，只读）
    /// - 设置值（可直接编辑输入）
    ///
    /// 分类（见 _categories）：
    ///   基础配置 / 气压表串口通讯 / IO耦合器（Modbus TCP）/ 气压表寄存器 /
    ///   报警参数 / 冷却送风机 / 老化测试业务 / 扫码枪
    ///
    /// 内容放在单个 UIDataGridView（填满 pnlScroll）里，表格自带垂直滚动条
    /// （DataGridView 虚拟化绘制，只重绘可见行），所有分类一眼看全，不用来回切页签，
    /// 滚动流畅不卡顿。
    ///
    /// 点击【保存设置】后，把所有改动写回程序运行目录下的 exe.config
    /// （即程序实际读取的配置文件，与 App.config 同源），
    /// 写完后刷新 appSettings 缓存，并把非结构型配置就地回写内存中的 DeviceConfig 实例：
    /// 各服务每次读写实时访问该实例，因此业务逻辑类配置（寄存器地址/IO 映射/取反/阈值等）
    /// 保存后立即生效；连接参数类由主窗体触发重连后生效；只有结构型配置
    /// （设备数量/布局/Mock/送风机启用）需重启程序才生效（保存时已提示）。
    ///
    /// 【实现要点】
    /// - 分类与 key 顺序在 _categories 中集中维护；所有分类合并为 **1 个 UIDataGridView**，
    ///   分类标题用"分组标题行"（浅蓝底深蓝粗体，见 AddGroupRowStyle）呈现。
    ///   【V1.53】不再使用 8 个独立表格 + 滚动容器——多表格在 AutoScroll 容器中物理移动、
    ///   逐帧整块重绘是滚动卡顿的根源（与主视图 V1.50 把 72 面板合并为单画布同理）；
    ///   单表格由 DataGridView 自身滚动（虚拟化，只重绘可见行），滚动流畅。
    ///   新增配置项只需在 _descriptions 和 _categories 里各加一行，无需改界面布局
    /// - 表格三列均启用内容换行，行高按内容（TextRenderer 测量换行高度）在 LayoutSections 中
    ///   逐行计算，保证说明 / 设置值等长文本全部显示不被截断；分组标题行固定高度 30
    /// - 搜索过滤：无匹配的分类整组隐藏（分组标题行 + 数据行一起隐藏，_sectionVisible 状态数组），
    ///   避免结果区留大片空白
    /// - 界面使用 SunnyUI 控件（UIDataGridView 表格 / UIButton 按钮）呈现，风格与主程序一致
    /// - 保存前按配置项类型做合法性校验（整数/小数/布尔/十六进制地址），
    ///   不合法项会整批拦截并列出，避免写坏配置文件
    /// - 使用 System.Configuration.ConfigurationManager.OpenExeConfiguration
    ///   读写 exe.config，配置值来源为运行时的 ConfigurationManager.AppSettings，
    ///   与程序启动加载的取值完全一致
    ///
    /// 【界面布局】（单页纵向展示；分类标题 = 表格内分组标题行，表格自身滚动）
    /// ┌──────────────────────────────────────────────┐
    /// │ 顶部提示条（浅蓝底白字：修改后保存立即/重启生效提示）│
    /// ├──────────────────────────────────────────────┤
    /// │ ↓ pnlScroll（容器不滚动，仅承载下方内容）      │
    /// │ [搜索配置项：____________] [✕]               │ ← pnlSearch（Dock=Top）
    /// │ ┌──────────────────────────────────────────┐ │ ← UIDataGridView（Dock=Fill，自带垂直滚动条）
    /// │ │ ▓ 基础配置（分组标题行，浅蓝底深蓝粗体）    │ │
    /// │ │ 设置名称(key) │ 说明       │ 设置值       │ │
    /// │ │ ...           │ ...        │ [编辑控件]   │ │
    /// │ │ ▓ 气压表串口通讯（分组标题行）              │ │
    /// │ │ ...（每个分类 = 分组标题行 + 数据行）       │ │
    /// │ │ ……（其余分类依次向下，搜索无匹配整组隐藏） │ │
    /// │ └──────────────────────────────────────────┘ │
    /// ├──────────────────────────────────────────────┤
    /// │                    [保存设置] [关闭]           │
    /// └──────────────────────────────────────────────┘
    /// 表格三列（设置名称/说明/设置值），值列控件由 CreateValueCell 按 key 分发
    /// （布尔=下拉框 / 串口波特率等=下拉框 / 数字=数字框 / 文本=文本框）。
    ///</summary>
    public partial class SettingsForm : Form
    {
        /// <summary>
        /// 当前程序正在使用的设备配置（用于取当前生效值做兜底、以及按属性类型校验）
        /// </summary>
        private readonly DeviceConfig _config;

        /// <summary>
        /// 唯一一张配置表格（合并了所有分类）。【V1.53】不再为每个分类单独建表：
        /// 8 个独立表格放在 AutoScroll 容器中滚动会逐帧整块重绘导致卡顿；
        /// 合并为单表格后由 DataGridView 自身滚动（虚拟化，只重绘可见行），滚动流畅。
        /// </summary>
        private Sunny.UI.UIDataGridView _grid;

        /// <summary>分组标题行的行号集合（分类标题不再用 UILine，改为表格内的"分组行"）</summary>
        private readonly HashSet<int> _groupRows = new HashSet<int>();

        /// <summary>分组标题行的行号列表（按行号升序，搜索过滤时用相邻行号定位每组的行范围）</summary>
        private readonly List<int> _groupRowList = new List<int>();

        /// <summary>分组标题行字体（深蓝粗体，与 UILine 标题字号一致）</summary>
        private Font _groupFont;

        /// <summary>搜索框控件</summary>
        private Sunny.UI.UITextBox _txtSearch;
        /// <summary>清除搜索按钮</summary>
        private Button _btnClearSearch;

        /// <summary>设置名称长按复制：记录按下时的表格 / 单元格 / 时间</summary>
        private DataGridView _pressGrid;
        private int _pressRow = -1;
        private int _pressCol = -1;
        /// <summary>长按计时器：按够 700ms 即触发复制（不用等松开，体验更跟手）</summary>
        private readonly Timer _pressTimer = new Timer { Interval = 700 };
        /// <summary>长按复制的提示气泡（ShowAlways=true 保证模态对话框内也可靠显示）</summary>
        private readonly ToolTip _copyTip = new ToolTip { ShowAlways = true };
        /// <summary>长按复制已触发、等待松开鼠标后弹出的提示内容（显示太早会被鼠标捕获盖住）</summary>
        private string _pendingCopyTip;
        /// <summary>长按复制时所在的表格/单元格，松开鼠标后用于定位气泡</summary>
        private DataGridView _pressTooltipGrid;
        private int _pressTooltipRow = -1;
        private int _pressTooltipCol = -1;

        /// <summary>
        /// 每个分类是否显示（搜索过滤时置为是否有匹配行）。
        /// 不用控件 Visible 判断：窗体尚未显示时控件 Visible 恒为 false，会导致初始布局错乱。
        /// </summary>
        private bool[] _sectionVisible;

        /// <summary>
        /// 需要限定为 true/false 下拉选择的配置项（防输入错误，只能选其一）
        /// </summary>
        private static readonly HashSet<string> _boolKeys = new HashSet<string>
        {
            "UseMockCommunication",
            "InvertInputs",
            "InvertOutputs",
            "IoBackupChannelMappingEnabled",
            "AlarmWhenPressureHigherThanThreshold",
            "FanEnabled",
            "FanAutoDetectEnabled",
            "UseDiAlarmContact",
            "ScannerEnabled",
            "ScannerDebugLog",
        };

        /// <summary>
        /// 结构型配置：改动影响设备数量 / 界面布局 / 实现类选择，
        /// 需重启程序才生效（保存时照常写入配置文件，但【不回写内存】，避免运行期结构不一致）。
        /// 判断依据：运行期结构（状态数组、面板布局、Reader/Controller 实现）在启动时一次性建立。
        /// </summary>
        public static readonly HashSet<string> StructuralKeys = new HashSet<string>
        {
            "TotalBarometers", "TotalInputs", "TotalOutputs",
            "PanelColumns", "PanelRows",
            "UseMockCommunication",
            "FanEnabled",
        };

        /// <summary>气压表串口连接参数：改动后需重连串口才生效</summary>
        public static readonly HashSet<string> BarometerConnectionKeys = new HashSet<string>
        {
            "PortName", "BaudRate", "DataBits", "StopBits", "Parity",
            "SerialReadTimeoutMs", "SerialWriteTimeoutMs",
        };

        /// <summary>IO 耦合器连接参数：改动后需重连 TCP 才生效</summary>
        public static readonly HashSet<string> IoConnectionKeys = new HashSet<string>
        {
            "PlcAddress", "PlcPort",
            "TcpSendTimeoutMs", "TcpReceiveTimeoutMs",
        };

        /// <summary>送风机连接参数：改动后需重连 TCP 才生效（FanEnabled 属结构型）</summary>
        public static readonly HashSet<string> FanConnectionKeys = new HashSet<string>
        {
            "FanIpAddress", "FanPort", "FanTimeoutMs",
            "FanAutoDetectEnabled", "FanIpCandidates",
        };

        /// <summary>扫码枪连接参数：改动后需重连串口才生效</summary>
        public static readonly HashSet<string> ScannerConnectionKeys = new HashSet<string>
        {
            "ScannerEnabled", "ScannerPort", "ScannerDeviceKeyword",
            "ScannerBaudRate", "ScannerDataBits", "ScannerStopBits", "ScannerParity",
        };

        /// <summary>
        /// 本次保存成功写回配置文件的配置项 key 集合（供主窗体分发重连 / 判断需重启项）
        /// </summary>
        public HashSet<string> SavedKeys { get; private set; }

        /// <summary>
        /// 数字类配置项的范围约束（防输入越界/乱输），保存前仍会按 ValidateValue 二次校验。
        /// </summary>
        private static readonly Dictionary<string, (decimal Min, decimal Max, int Decimals, decimal Increment)> _numericKeys =
            new Dictionary<string, (decimal, decimal, int, decimal)>
            {
                { "TotalBarometers", (1, 999, 0, 1) },
                { "TotalInputs", (1, 999, 0, 1) },
                { "TotalOutputs", (1, 999, 0, 1) },
                { "CollectInterval", (10, 60000, 0, 10) },
                { "PanelColumns", (1, 100, 0, 1) },
                { "PanelRows", (1, 100, 0, 1) },

                { "SerialReadTimeoutMs", (10, 60000, 0, 10) },
                { "SerialWriteTimeoutMs", (10, 60000, 0, 10) },

                { "PlcPort", (1, 65535, 0, 1) },
                { "IoUnitId", (1, 255, 0, 1) },
                { "TcpSendTimeoutMs", (10, 60000, 0, 10) },
                { "TcpReceiveTimeoutMs", (10, 60000, 0, 10) },

                { "BarometerDefaultDecimalPlaces", (0, 4, 0, 1) },
                { "BarometerPressureScale", (-100, 100, 3, 0.1m) },

                { "AlarmPressureThresholdKPa", (-200, 200, 2, 0.5m) },

                { "FanPort", (1, 65535, 0, 1) },
                { "FanUnitId", (1, 255, 0, 1) },
                { "FanTimeoutMs", (10, 60000, 0, 10) },

                { "VacuumConfirmTimeoutMs", (100, 600000, 0, 100) },
                { "CommunicationLossAlarmCount", (1, 10000, 0, 1) },
                { "MaxTestDurationSeconds", (0, 86400, 0, 10) },
                { "FanTempAlarmLimitC", (0, 200, 1, 0.5m) },
            };

        /// <summary>
        /// 配置项说明字典（key → 中文说明），显示在表格"说明"列
        /// 覆盖 App.config 全部配置项，key 必须与 _categories 中用到的 key 一致
        /// </summary>
        private readonly Dictionary<string, string> _descriptions = new Dictionary<string, string>
        {
            // ===== 基础配置 =====
            { "TotalBarometers", "气压表总数（当前 72）" },
            { "TotalInputs", "IO 输入总数（当前 80）" },
            { "TotalOutputs", "IO 输出总数（当前 160）" },
            { "CollectInterval", "数据采集间隔（毫秒）" },
            { "PanelColumns", "主视图每行显示的气压表数量（列数）" },
            { "PanelRows", "主视图每列显示的气压表数量（行数）" },

            // ===== 气压表串口通讯 =====
            { "PortName", "气压表通信端口（如 COM9，留空则启动时自动识别 CH340）" },
            { "BaudRate", "气压表波特率（19200）" },
            { "DataBits", "数据位（8）" },
            { "StopBits", "停止位（1）" },
            { "Parity", "校验位（None）" },
            { "SerialReadTimeoutMs", "串口读取超时（毫秒）" },
            { "SerialWriteTimeoutMs", "串口写入超时（毫秒）" },
            { "UseMockCommunication", "是否使用模拟通讯（true=不接硬件用假数据）" },

            // ===== IO耦合器（Modbus TCP）与 TCP 超时 =====
            { "PlcAddress", "PLC / IO 耦合器 IP（如 192.168.1.20）" },
            { "PlcPort", "PLC 通讯端口（502）" },
            { "IoUnitId", "IO 耦合器从站地址（UnitId，默认 1）" },
            { "IoInputRegisterStartAddress", "IO 输入寄存器起始地址（十六进制，如 0x1000）" },
            { "IoOutputRegisterStartAddress", "IO 输出寄存器起始地址（十六进制，如 0x2000）" },
            { "InvertInputs", "输入点逻辑是否取反（false/true）" },
            { "InvertOutputs", "输出点逻辑是否取反（false/true）" },
            { "IoBackupChannelMappingEnabled", "是否启用 IO 输出备用通道映射（false/true）" },
            { "IoBackupChannelMappings", "IO 备用通道映射表（点击编辑：寄存器@通道均为十六进制，如 0x2000@0x00->0x2009@0x10）" },
            { "TcpSendTimeoutMs", "TCP 发送超时（毫秒，耦合器/送风机通用）" },
            { "TcpReceiveTimeoutMs", "TCP 接收超时（毫秒，耦合器/送风机通用）" },

            // ===== 气压表寄存器 =====
            { "BarometerPressureRegisterAddress", "气压表压力寄存器地址（0x0001）" },
            { "BarometerDefaultDecimalPlaces", "气压表小数位（务必与仪表实际一致，当前 1）" },
            { "BarometerPressureScale", "压力缩放系数（读数 × 该值）" },

            // ===== 报警参数 =====
            { "AlarmPressureThresholdKPa", "报警压力阈值（kPa，如 -95）" },
            { "AlarmWhenPressureHigherThanThreshold", "报警方向（true=压力高于阈值时报警）" },

            // ===== 冷却送风机 =====
            { "FanEnabled", "是否启用冷却送风机（false/true）" },
            { "FanIpAddress", "送风机控制屏 IP（如 192.168.1.220）" },
            { "FanAutoDetectEnabled", "送风机 IP 自动识别开关（false/true）" },
            { "FanIpCandidates", "送风机候选 IP 列表（逗号分隔，如 192.168.1.220,192.168.1.221）" },
            { "FanPort", "送风机通讯端口（50000）" },
            { "FanUnitId", "送风机从站地址（默认 1）" },
            { "FanTimeoutMs", "送风机通讯超时（毫秒）" },

            // ===== 老化测试业务 =====
            { "VacuumConfirmTimeoutMs", "真空建立确认超时（毫秒，默认 15000）" },
            { "CommunicationLossAlarmCount", "通讯故障报警阈值（连续读取失败 N 次）" },
            { "MaxTestDurationSeconds", "老化测试最大时长（秒，0=不限时手动停止）" },
            { "UseDiAlarmContact", "气压表报警触点(DI)是否并入报警判定（false/true）" },
            { "FanTempAlarmLimitC", "送风机温度告警上限（°C，0=不启用）" },

            // ===== 扫码枪 =====
            { "ScannerEnabled", "是否启用扫码枪（false/true）" },
            { "ScannerPort", "扫码枪固定串口（留空则按关键词自动识别）" },
            { "ScannerDeviceKeyword", "扫码枪设备识别关键词（如 Xenon 1902）" },
            { "ScannerBaudRate", "扫码枪波特率（115200）" },
            { "ScannerDataBits", "扫码枪数据位（8）" },
            { "ScannerStopBits", "扫码枪停止位（1）" },
            { "ScannerParity", "扫码枪校验位（None）" },
            { "ScannerDebugLog", "扫码枪心跳调试日志开关（false/true）" },
        };

        /// <summary>
        /// 分类定义：页签标题 → 该分类下的配置项 key 列表（按显示顺序）
        ///
        /// 分组逻辑（与 App.config 注释分组一致）：
        /// - 基础配置：设备数量 / 采集间隔 / 面板布局
        /// - 气压表串口通讯：串口参数 / 超时 / Mock 开关
        /// - IO耦合器：IP / 端口 / 从站地址 / 寄存器地址 / 逻辑取反 / 备用通道映射 / TCP 超时
        /// - 气压表寄存器：压力寄存器 / 小数位 / 缩放系数
        /// - 报警参数：压力报警阈值与方向
        /// - 冷却送风机：启用 / IP 自动识别 / 端口 / 超时
        /// - 老化测试业务：真空确认 / 失联报警 / 最大时长 / DI 触点 / 温度告警
        /// - 扫码枪：启用 / 端口识别 / 串口参数 / 调试日志
        ///
        /// 【新增配置项】只需：①在 _descriptions 加说明；②在本数组对应分类的 Keys 里加 key
        /// </summary>
        private readonly (string Title, string[] Keys)[] _categories = new (string Title, string[] Keys)[]
        {
            ("基础配置", new string[]
            {
                "TotalBarometers", "TotalInputs", "TotalOutputs",
                "CollectInterval", "PanelColumns", "PanelRows"
            }),
            ("气压表串口通讯", new string[]
            {
                "PortName", "BaudRate", "DataBits", "StopBits", "Parity",
                "SerialReadTimeoutMs", "SerialWriteTimeoutMs", "UseMockCommunication"
            }),
            ("IO耦合器（Modbus TCP）", new string[]
            {
                "PlcAddress", "PlcPort", "IoUnitId",
                "IoInputRegisterStartAddress", "IoOutputRegisterStartAddress",
                "InvertInputs", "InvertOutputs",
                "IoBackupChannelMappingEnabled", "IoBackupChannelMappings",
                "TcpSendTimeoutMs", "TcpReceiveTimeoutMs"
            }),
            ("气压表寄存器", new string[]
            {
                "BarometerPressureRegisterAddress",
                "BarometerDefaultDecimalPlaces", "BarometerPressureScale"
            }),
            ("报警参数", new string[]
            {
                "AlarmPressureThresholdKPa", "AlarmWhenPressureHigherThanThreshold"
            }),
            ("冷却送风机", new string[]
            {
                "FanEnabled", "FanIpAddress", "FanAutoDetectEnabled", "FanIpCandidates",
                "FanPort", "FanUnitId", "FanTimeoutMs"
            }),
            ("老化测试业务", new string[]
            {
                "VacuumConfirmTimeoutMs", "CommunicationLossAlarmCount",
                "MaxTestDurationSeconds", "UseDiAlarmContact", "FanTempAlarmLimitC"
            }),
            ("扫码枪", new string[]
            {
                "ScannerEnabled", "ScannerPort", "ScannerDeviceKeyword",
                "ScannerBaudRate", "ScannerDataBits", "ScannerStopBits", "ScannerParity",
                "ScannerDebugLog"
            }),
        };

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="config">当前生效的设备配置（主窗体传入，用于取兜底值与类型校验）</param>
        public SettingsForm(DeviceConfig config)
        {
            InitializeComponent();
            _config = config;

            // 按分类创建分隔标题和表格，再填充数据并排布版面
            SetupSections();
            LoadSettings();
            LayoutSections();

            // 创建搜索框（位于布局顶部）
            SetupSearchBox();
        }

        /// <summary>
        /// 窗体显示后预激活复制提示气泡：
        /// ToolTip 首次 Show 时原生窗口尚未创建，直接 Show 会不显示；
        /// 这里先空转一次（带坐标 Show 一个空格并立即 Hide）把内部窗口建好，后续 Show 才可靠。
        /// 必须在窗体句柄创建之后做，构造函数里调用会因窗口未激活而无效。
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _copyTip.Show(" ", this, new Point(1, 1), 1);
            _copyTip.Hide(this);
        }

        /// <summary>
        /// 创建唯一一张配置表格（UIDataGridView）填满 pnlScroll。
        /// 【V1.53】分类不再各自建表：8 个独立表格放滚动容器里滚动会逐帧整块重绘导致卡顿，
        /// 合并为单表格后由 DataGridView 自身滚动（虚拟化，只重绘可见行）；
        /// 分类标题用”分组标题行”（见 LoadSettings 的 AddGroupRowStyle）。
        /// </summary>
        private void SetupSections()
        {
            _groupRows.Clear();
            _groupRowList.Clear();

            // 每节分类的显示状态（搜索过滤用）：初始全部显示
            _sectionVisible = new bool[_categories.Length];
            for (int i = 0; i < _sectionVisible.Length; i++)
            {
                _sectionVisible[i] = true;
            }

            // 分组标题行字体（与旧 UILine 标题同款：10 号加粗）
            _groupFont = new Font(this.Font.FontFamily, 10F, FontStyle.Bold);

            // 唯一一张配置表格，Dock=Fill 撑满滚动容器；滚动条由表格自带
            _grid = CreateGrid();
            _grid.Dock = DockStyle.Fill;
            pnlScroll.Controls.Add(_grid);
        }

        /// <summary>
        /// 创建一个配置表格（SunnyUI UIDataGridView），三列：设置名称 / 说明 / 设置值
        /// 名称列和说明列只读，设置值列可编辑（用户输入新值）
        /// </summary>
        private Sunny.UI.UIDataGridView CreateGrid()
        {
            var grid = new Sunny.UI.UIDataGridView
            {
                Style = Sunny.UI.UIStyle.Blue,
                AllowUserToAddRows = false,          // 不允许用户新增行
                AllowUserToDeleteRows = false,       // 不允许用户删除行
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                RowTemplate = { Height = 24 },
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                // 【V1.53】单表格自带垂直滚动条（虚拟化绘制，只重绘可见行），
                // 不再靠外层 pnlScroll 滚动整页（8 个表格整块移动重绘会卡顿）。
                ScrollBars = ScrollBars.Vertical
            };

            // 三列表头
            grid.Columns.Add("colKey", "设置名称");
            grid.Columns.Add("colDesc", "说明");
            grid.Columns.Add("colValue", "设置值");

            // 名称 / 说明列只读
            grid.Columns["colKey"].ReadOnly = true;
            grid.Columns["colDesc"].ReadOnly = true;

            // 列宽分配：名称 260 + 说明 430 + 设置值占满剩余宽度
            grid.Columns["colKey"].Width = 260;
            grid.Columns["colKey"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            grid.Columns["colDesc"].Width = 430;
            grid.Columns["colDesc"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            grid.Columns["colValue"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            // 三列内容显示不下时自动换行（行高按内容在 LayoutSections 里自动计算，保证内容全部显示）
            grid.Columns["colKey"].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.Columns["colDesc"].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.Columns["colValue"].DefaultCellStyle.WrapMode = DataGridViewTriState.True;

            // 单元格统一样式：白底深字，避免下拉框/数字框出现系统灰色底
            grid.DefaultCellStyle.BackColor = Color.White;
            grid.DefaultCellStyle.ForeColor = Color.FromArgb(48, 48, 48);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(48, 119, 238);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(237, 243, 253);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(48, 48, 48);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(237, 243, 253);
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.FromArgb(48, 48, 48);
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            // 可手输下拉（波特率）允许输入列表外的自定义值：捕获校验异常，把新值补进列表后提交
            grid.DataError += Grid_DataError;

            // IP 列表单元格（FanIpCandidates）点击弹出编辑器
            grid.CellClick += Grid_CellClick;
            // 设置名称列支持鼠标左键长按复制
            grid.CellMouseDown += Grid_CellMouseDown;
            grid.CellMouseUp += Grid_CellMouseUp;

            // 分组标题行自绘：去掉表格分割线，让标题看起来不在表格内（见 Grid_CellPainting 注释）
            grid.CellPainting += Grid_CellPainting;
            // 行级画完后再补一刀：把分组标题行上下两条水平线以及三列之间残留的垂直线
            // 用浅蓝（与标题行背景同色）覆盖，消除"标题是表格里的一行"的视觉感。
            // CellPainting + e.Handled=true 在 SunnyUI UIDataGridView 下不一定能完全阻止
            // 控件画 cell border（SunnyUI 可能在 CellPainting 返回后再补画 gridline），
            // 这里在 RowPostPaint 阶段主动用背景色覆盖这些线，最稳。
            grid.RowPostPaint += Grid_RowPostPaint;

            return grid;
        }

        /// <summary>
        /// 处理 DataGridView 数据错误：波特率下拉允许手输自定义值，
        /// 输入不在列表里的波特率时自动补进 Items 并接受该值，而不是弹出错误
        /// </summary>
        private void Grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid != null && e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                DataGridViewCell cell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
                if (cell is DataGridViewComboBoxCell combo && grid.EditingControl is ComboBox editing)
                {
                    string typed = editing.Text;
                    if (!string.IsNullOrWhiteSpace(typed) && !combo.Items.Contains(typed))
                    {
                        combo.Items.Add(typed);
                        cell.Value = typed;
                        e.ThrowException = false;
                        return;
                    }
                }
            }
            e.ThrowException = false;
        }

        /// <summary>
        /// 分组标题行自绘：去掉表格分割线（水平/垂直边框），只保留浅蓝背景 + 深蓝粗体标题文字，
        /// 从视觉上让标题行看起来**不在表格内部**，更像一条独立的分类色带。
        ///
        /// 原理：DataGridView 默认渲染每个单元格时都会画出边框（CellBorderStyle=Single），
        /// 分组标题行如果走默认绘制，三列之间会有垂直分割线、上下行之间有水平分割线，
        /// 看起来就是"表格里的一行"。
        /// 这里拦截分组标题行的 CellPainting，只画 Background（浅蓝铺满整格），
        /// 再手动画标题文字，不画 Border 也不画 ContentBackground/Foreground，
        /// 最终标题行看起来就是"放在表格里的一个色带"，而不是"表格的一行"。
        /// </summary>
        private void Grid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            // 只处理分组标题行，非分组行/表头/-1 行一律跳过
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (!_groupRows.Contains(e.RowIndex)) return;

            // 只画背景（浅蓝），不画任何边框/分割线
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background);

            // 第一列（colKey）画标题文字，其他列留空不画
            var cellValue = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? "";
            if (!string.IsNullOrEmpty(cellValue))
            {
                var textBounds = e.CellBounds;
                textBounds.X += 8;   // 左内边距 8px，与数据行设置名称列文字对齐
                textBounds.Width -= 8;
                TextRenderer.DrawText(e.Graphics, cellValue, _groupFont, textBounds,
                    Color.FromArgb(48, 119, 238),
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }

            // 标记已由我们完整绘制，阻止系统再画边框/焦点/选择态等
            e.Handled = true;
        }

        /// <summary>
        /// 分组标题行画完后：去掉**列间垂直线** + _grid 右侧残留竖线，**下边线**用蓝色（标题色带延伸）。
        /// 视觉上让标题行像"一条横跨表格的标题带"：
        ///   上边线：DataGridView 默认 cell border（与数据行统一，不另画避免叠色）
        ///   下边线：自画蓝色（与标题文字同色，色带延伸）
        ///   中间：列与列之间、_grid 右边缘都不再有垂直线
        /// </summary>
        private void Grid_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            if (!_groupRows.Contains(e.RowIndex)) return;

            Color groupBack = Color.FromArgb(237, 243, 253);   // 标题行浅蓝底
            // 【V1.54d】下边线：与标题文字同色（深蓝 48,119,238），色带延伸
            Color groupLine = Color.FromArgb(48, 119, 238);
            // 【V1.54g】覆盖右边缘扩 1 像素：DataGridView 的 cell border 画在 _grid.Right 那一像素
            // （半开区间 [0, _grid.Width) 内的 cell 在 X=_grid.Width-1，border 在 X=_grid.Width），
            // 之前覆盖矩形 Width=_grid.Width 漏掉 _grid.Right 的 border，导致每个标题行最右侧仍有一条竖线。
            // 改为 _grid.Width + 1 像素正好覆盖住。
            int coverRight = _grid.Width + 1;
            using (var bgBrush = new SolidBrush(groupBack))
            using (var linePen = new Pen(groupLine, 1f))
            {
                // ① 整行宽矩形（X=0 到 _grid.Right，包含 cell border 那一像素），覆盖列间垂直线 + 右侧 cell border
                Rectangle rowRect = e.RowBounds;
                rowRect.X = 0;
                rowRect.Width = coverRight;
                e.Graphics.FillRectangle(bgBrush, rowRect);

                // ② 只画下边一条蓝色水平线（用户明确要求"标题下面的横线要蓝色"）
                // 上边不画，避免与 DataGridView 自己的 cell border 叠色成"颜色深很多"
                e.Graphics.DrawLine(linePen, 0, e.RowBounds.Bottom - 1, coverRight - 1, e.RowBounds.Bottom - 1);

                // ③ 重画标题文字（RowPostPaint 在 CellPainting 之后执行，文字可能被覆盖）
                DataGridViewRow row = _grid.Rows[e.RowIndex];
                var cell = row.Cells["colKey"];
                string text = cell.Value?.ToString() ?? "";
                if (!string.IsNullOrEmpty(text))
                {
                    var textBounds = _grid.GetCellDisplayRectangle(cell.ColumnIndex, e.RowIndex, false);
                    textBounds.X += 8;
                    textBounds.Width -= 8;
                    TextRenderer.DrawText(e.Graphics, text, _groupFont, textBounds,
                        Color.FromArgb(48, 119, 238),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter
                        | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                }
            }
        }

        /// <summary>
        /// 把全部配置项按分类填入各分类表格
        /// 值取运行时的 ConfigurationManager.AppSettings（与程序启动读取一致），
        /// 缺失时用程序当前生效值（DeviceConfig 属性）兜底
        /// </summary>
        private void LoadSettings()
        {
            _groupRows.Clear();
            _groupRowList.Clear();
            _grid.Rows.Clear();

            for (int i = 0; i < _categories.Length; i++)
            {
                // 每个分类先加一行"分组标题行"（浅蓝底深蓝粗体，占一行宽作为分类分隔）
                int groupRow = _grid.Rows.Add();
                AddGroupRowStyle(groupRow, _categories[i].Title);
                _groupRows.Add(groupRow);
                _groupRowList.Add(groupRow);

                foreach (string key in _categories[i].Keys)
                {
                    // 说明列：取不到说明（配置被移除）时显示为空，不阻塞加载
                    _descriptions.TryGetValue(key, out string desc);

                    int rowIdx = _grid.Rows.Add();
                    _grid.Rows[rowIdx].Cells["colKey"].Value = key;
                    _grid.Rows[rowIdx].Cells["colDesc"].Value = desc ?? "";
                    _grid.Rows[rowIdx].Cells["colValue"] = CreateValueCell(key, GetEffectiveValue(key));
                }
            }
        }

        /// <summary>
        /// 把指定行设置为"分组标题行"：浅蓝底 + 深蓝粗体标题，整行只读、选中不变色。
        /// 三列背景统一为浅蓝，视觉上是一条贯穿表格宽度的标题带（替代旧 UILine 分类分隔条）。
        /// </summary>
        private void AddGroupRowStyle(int rowIndex, string title)
        {
            DataGridViewRow row = _grid.Rows[rowIndex];
            row.ReadOnly = true;

            Color back = Color.FromArgb(237, 243, 253);   // 浅蓝底（与表头同色系）
            Color fore = Color.FromArgb(48, 119, 238);    // 深蓝字（与旧 UILine 标题同色）
            foreach (DataGridViewCell cell in row.Cells)
            {
                cell.Style.BackColor = back;
                cell.Style.ForeColor = fore;
                // 选中/点击时保持同色，避免分组行出现高亮变色
                cell.Style.SelectionBackColor = back;
                cell.Style.SelectionForeColor = fore;
                cell.Style.Font = _groupFont;
            }

            row.Cells["colKey"].Value = title;
            row.Cells["colDesc"].Value = "";
            row.Cells["colValue"].Value = "";
        }

        /// <summary>
        /// 设置表格每行行高：分组标题行固定 30，数据行按内容换行后的高度计算。
        /// 表格本身 Dock=Fill 撑满 pnlScroll 并由 DataGridView 自带滚动，
        /// 不再需要手动摆放多个控件 / 设置 AutoScrollMinSize。
        /// </summary>
        private void LayoutSections()
        {
            if (_grid == null) return;

            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (_groupRows.Contains(row.Index))
                {
                    row.Height = 30;   // 分组标题行固定高度
                }
                else
                {
                    row.Height = ComputeRowHeight(_grid, row);
                }
            }
        }

        /// <summary>
        /// 按单元格内容换行后的实际行高计算（三列取最大值，保证内容全部显示不被截断）
        /// 换行宽度按列宽减去左右内边距计算，行高 = 文本高度 + 上下内边距，最小不低于默认行高 24。
        /// </summary>
        /// <param name="grid">所属表格（取列宽与字体）</param>
        /// <param name="row">要计算的行</param>
        /// <returns>该行应设置的像素高度</returns>
        private static int ComputeRowHeight(DataGridView grid, DataGridViewRow row)
        {
            const int xPadding = 8;   // 左右内边距（从换行宽度中扣除）
            const int yPadding = 3;   // 上下内边距（加到文本高度上）
            int maxTextHeight = 1;

            foreach (DataGridViewCell cell in row.Cells)
            {
                string text = cell.Value?.ToString() ?? "";
                if (text.Length == 0) continue;

                int colWidth = grid.Columns[cell.ColumnIndex].Width - xPadding;
                if (colWidth < 10) colWidth = 10;

                Size textSize = TextRenderer.MeasureText(text, grid.Font,
                    new Size(colWidth, 0),
                    TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding);
                if (textSize.Height > maxTextHeight) maxTextHeight = textSize.Height;
            }

            return Math.Max(maxTextHeight + yPadding, 24);
        }

        /// <summary>
        /// 在滚动面板顶部创建搜索框，用于快速过滤配置项
        /// </summary>
        private void SetupSearchBox()
        {
            var pnlSearch = new Panel
            {
                // Dock=Top 占据 pnlScroll 顶部 36px，下方的表格（Dock=Fill）占剩余区域；
                // Location/Width 交给 Dock 管理，无需手动设置
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = Color.FromArgb(245, 248, 255)
            };

            var lblSearch = new Label
            {
                Text = "搜索配置项：",
                // 【V1.54d】左对齐：与下面"基础配置"等分组标题对齐（pnlScroll.Padding.Left=12 + 8=20）
                Location = new Point(20, 5),
                Size = new Size(110, 26),
                TextAlign = ContentAlignment.MiddleLeft,
                // 【V1.54d】字体大小一致：与分组标题行同款（10F Bold），视觉上"搜索配置项："和"基础配置"是同一族标题
                Font = new Font(this.Font.FontFamily, 10F, FontStyle.Bold),
                ForeColor = Color.FromArgb(48, 119, 238)
            };
            pnlSearch.Controls.Add(lblSearch);

            _txtSearch = new Sunny.UI.UITextBox
            {
                // 文本框也左移 6px（与 label 对齐到同一起点）
                Location = new Point(130, 5),
                Size = new Size(320, 26),
                // 【V1.54d】文本框字体 11F → 10F，与分组标题字号一致；保持表格内文字的视觉协调
                Font = new Font(this.Font.FontFamily, 10F),
                Watermark = "输入关键字过滤配置项"
            };
            _txtSearch.TextChanged += TxtSearch_TextChanged;
            pnlSearch.Controls.Add(_txtSearch);

            _btnClearSearch = new Button
            {
                Text = "✕",
                // 清除按钮 X 跟着文本框左移 6px（保持与文本框的相对间距 4px）
                Location = new Point(454, 5),
                Size = new Size(26, 26),
                FlatStyle = FlatStyle.Flat,
                TabStop = false
            };
            _btnClearSearch.Click += (s, e) => { _txtSearch.Clear(); _txtSearch.Focus(); };
            pnlSearch.Controls.Add(_btnClearSearch);

            pnlScroll.Controls.Add(pnlSearch);

            // Dock 布局按 Controls 集合顺序：把搜索框（Dock=Top）排到表格（Dock=Fill）前面，
            // 保证搜索框占顶部、表格占剩余区域而不互相覆盖（SetupSections 先添加了表格）。
            pnlScroll.Controls.SetChildIndex(pnlSearch, 0);
            if (_grid != null) pnlScroll.Controls.SetChildIndex(_grid, 1);
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            ApplySearchFilter();
        }

        private void ApplySearchFilter()
        {
            string keyword = _txtSearch.Text.Trim();
            bool hasKeyword = !string.IsNullOrEmpty(keyword);

            if (!hasKeyword)
            {
                // 无搜索关键字 → 全部行显示
                foreach (DataGridViewRow row in _grid.Rows)
                {
                    row.Visible = true;
                }
                for (int i = 0; i < _sectionVisible.Length; i++) _sectionVisible[i] = true;
                return;
            }

            // ① 先按关键字过滤数据行（分组标题行跳过，稍后统一处理）
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow || _groupRows.Contains(row.Index)) continue;

                string key = row.Cells["colKey"].Value?.ToString() ?? "";
                string desc = row.Cells["colDesc"].Value?.ToString() ?? "";
                bool match = key.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0
                          || desc.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
                row.Visible = match;
            }

            // ② 分组标题行：其下方（到下一个分组行之前）有可见数据行才显示，实现整组隐藏
            for (int i = 0; i < _groupRowList.Count; i++)
            {
                int groupRow = _groupRowList[i];
                int next = (i + 1 < _groupRowList.Count) ? _groupRowList[i + 1] : _grid.Rows.Count;

                bool anyVisible = false;
                for (int r = groupRow + 1; r < next; r++)
                {
                    if (_grid.Rows[r].Visible) { anyVisible = true; break; }
                }

                _grid.Rows[groupRow].Visible = anyVisible;
                _sectionVisible[i] = anyVisible;
            }
        }

        /// <summary>
        /// 点击"设置值"列时，若该行是列表/映射类配置项，弹出对应的编辑器：
        /// FanIpCandidates → IP 列表编辑器；IoBackupChannelMappings → IO 映射编辑器。
        /// </summary>
        private void Grid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid == null || e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (e.ColumnIndex != grid.Columns["colValue"].Index) return;

            string key = grid.Rows[e.RowIndex].Cells["colKey"].Value?.ToString();
            if (key == "FanIpCandidates")
            {
                string currentValue = grid.Rows[e.RowIndex].Cells["colValue"].Value?.ToString() ?? "";
                ShowIpListPopup(grid, e.RowIndex, currentValue);
            }
            else if (key == "IoBackupChannelMappings")
            {
                string currentValue = grid.Rows[e.RowIndex].Cells["colValue"].Value?.ToString() ?? "";
                ShowIoMappingPopup(grid, e.RowIndex, currentValue);
            }
        }

        /// <summary>
        /// 在"设置名称"列按下鼠标左键时记录按下位置，并启动长按计时器
        /// （计时到 700ms 即复制，无需等松开）。
        /// </summary>
        private void Grid_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid == null || e.Button != MouseButtons.Left || e.RowIndex < 0) return;
            if (e.ColumnIndex != grid.Columns["colKey"].Index) return;

            _pressGrid = grid;
            _pressRow = e.RowIndex;
            _pressCol = e.ColumnIndex;
            _pressTooltipGrid = grid;
            _pressTooltipRow = e.RowIndex;
            _pressTooltipCol = e.ColumnIndex;
            _pressTimer.Stop();
            _pressTimer.Tick -= PressTimer_Tick;
            _pressTimer.Tick += PressTimer_Tick;
            _pressTimer.Start();
        }

        /// <summary>松开鼠标左键：取消长按计时（不足 700ms 则未触发复制）。
        /// 气泡已在长按到点（PressTimer_Tick）时即时弹出，这里再 BeginInvoke 一次做兜底——
        /// 若按住时气泡已显示过（_pendingCopyTip 已被消费为 null），本次调用直接返回，不会重复弹。</summary>
        private void Grid_CellMouseUp(object sender, DataGridViewCellMouseEventArgs e)
        {
            _pressTimer.Stop();
            _pressTimer.Tick -= PressTimer_Tick;
            _pressGrid = null;
            _pressRow = -1;
            _pressCol = -1;

            // 兜底：万一按住状态下气泡因表格鼠标捕获未成功渲染，松开后再弹一次；
            // 已显示过则 _pendingCopyTip 已为 null，此调用是空转（见 ShowPendingCopyTip）。
            BeginInvoke(new Action(ShowPendingCopyTip));
        }

        /// <summary>
        /// 长按计时到点：把设置名称复制到剪贴板，并立即弹出"已复制"气泡。
        /// 无需等松开鼠标——复制与提示都在按住过程中完成，松开仅结束长按。
        /// 用 BeginInvoke 延迟到本事件处理完再弹：避免长按时表格仍捕获鼠标
        /// 干扰 ToolTip 渲染；宿主窗体用 this（见 ShowPendingCopyTip）。
        /// </summary>
        private void PressTimer_Tick(object sender, EventArgs e)
        {
            _pressTimer.Stop();
            DataGridView grid = _pressGrid;
            int rowIndex = _pressRow;
            int colIndex = _pressCol;
            _pressGrid = null;
            _pressRow = -1;
            _pressCol = -1;

            if (grid == null || grid.IsDisposed) return;
            if (rowIndex < 0 || rowIndex >= grid.Rows.Count) return;

            string text = grid.Rows[rowIndex].Cells["colKey"].Value?.ToString();
            if (string.IsNullOrEmpty(text)) return;

            try
            {
                System.Windows.Forms.Clipboard.SetText(text);
            }
            catch (Exception)
            {
                return;
            }

            _pendingCopyTip = text;

            // 长按到点即弹提示（无需等松开鼠标）
            BeginInvoke(new Action(ShowPendingCopyTip));
        }

        /// <summary>
        /// 弹出"已复制"气泡（长按到点即弹；松开鼠标时兜底再调一次，已显示过则空转）。
        /// 用带坐标重载 Show(text, window, Point, duration)，与主视图悬停提示同一可靠路径：
        /// - **宿主窗口用 SettingsForm 本身（this）**：主视图悬停提示（已验证可靠）就是
        ///   用宿主控件 this 作 window。DataGridView 内部窗口结构复杂，把气泡挂到 grid 上
        ///   可能被其窗口消息干扰导致不渲染；窗体是普通窗口，作为宿主最稳。
        /// - 锚点取光标当前位置（松开鼠标时仍在长按的单元格附近），显示在光标右下 12px，
        ///   不用换算单元格坐标、不受表格滚动影响；
        /// - 该重载（而非无坐标的 Show(text, window, duration)）在首次调用前需原生窗口
        ///   已建好（OnShown 已用带坐标版本预激活）；无坐标版会把气泡定位到窗口默认位置，
        ///   首次可能落到屏幕角落，看起来就像"没显示"。
        /// 目标单元格取 MouseDown 时记录的 _pressTooltipRow/_pressTooltipCol，
        /// 不受 Timer 重置 _pressRow/_pressCol 影响。
        /// </summary>
        private void ShowPendingCopyTip()
        {
            string text = _pendingCopyTip;
            _pendingCopyTip = null;
            if (text == null) return;

            DataGridView grid = _pressTooltipGrid;
            _pressTooltipGrid = null;
            int rowIndex = _pressTooltipRow;
            int colIndex = _pressTooltipCol;
            _pressTooltipRow = -1;
            _pressTooltipCol = -1;
            if (grid == null || grid.IsDisposed) return;
            if (rowIndex < 0 || rowIndex >= grid.Rows.Count) return;
            if (colIndex < 0) return;

            // 先 Hide 清掉 ToolTip 残留的显示状态，避免残留状态导致本次 Show 失效
            _copyTip.Hide(this);

            // 宿主窗口用窗体自身，锚点取光标位置（光标仍在刚复制的单元格附近），
            // 显示在光标右下 12px（Show 的 Point 是相对宿主窗体客户区的坐标）
            Point tipPoint = this.PointToClient(Cursor.Position);
            tipPoint.Offset(12, 12);
            // 时长 2500ms：长按触发时用户往往仍按住鼠标，气泡要多停留一会儿才看得清
            _copyTip.Show("已复制：" + text, this, tipPoint, 2500);
        }

        /// <summary>
        /// 弹出候选 IP 列表编辑器，并把编辑结果写回单元格。
        /// </summary>
        private void ShowIpListPopup(DataGridView grid, int rowIndex, string currentValue)
        {
            var popup = new Controls.IpListEditorPopup(currentValue);

            // 定位到该单元格正下方
            Rectangle cellRect = grid.GetCellDisplayRectangle(grid.Columns["colValue"].Index, rowIndex, true);
            Rectangle screenRect = grid.RectangleToScreen(cellRect);
            popup.Location = new Point(screenRect.Left, screenRect.Bottom + 2);

            // 越界保护：弹窗底部超出屏幕时改为显示在单元格上方
            var workArea = Screen.FromControl(grid).WorkingArea;
            if (popup.Bottom > workArea.Bottom)
            {
                popup.Location = new Point(screenRect.Left, screenRect.Top - popup.Height - 2);
            }

            popup.FormClosed += (s, args) =>
            {
                if (!string.IsNullOrEmpty(popup.ResultValue))
                {
                    grid.Rows[rowIndex].Cells["colValue"].Value = popup.ResultValue;
                    // 值可能变化，重新按内容算行高
                    LayoutSections();
                }
            };

            popup.Show(this);
            popup.Activate();
        }

        /// <summary>
        /// 弹出 IO 备用通道映射编辑器，并把编辑结果写回单元格。
        /// 界面与配置同构（寄存器@通道）：寄存器（0x0000~0xFFFF）与通道（0x00~0x1F）
        /// 均为十六进制显示，所见即所得；十六进制通道由 IoOutputChannelRemap 解析时转成十进制位号。
        /// </summary>
        private void ShowIoMappingPopup(DataGridView grid, int rowIndex, string currentValue)
        {
            var popup = new Controls.IoMappingEditorPopup(currentValue);

            // 定位到该单元格正下方
            Rectangle cellRect = grid.GetCellDisplayRectangle(grid.Columns["colValue"].Index, rowIndex, true);
            Rectangle screenRect = grid.RectangleToScreen(cellRect);
            popup.Location = new Point(screenRect.Left, screenRect.Bottom + 2);

            // 越界保护：弹窗底部超出屏幕时改为显示在单元格上方
            var workArea = Screen.FromControl(grid).WorkingArea;
            if (popup.Bottom > workArea.Bottom)
            {
                popup.Location = new Point(screenRect.Left, screenRect.Top - popup.Height - 2);
            }

            popup.FormClosed += (s, args) =>
            {
                if (!string.IsNullOrEmpty(popup.ResultValue))
                {
                    grid.Rows[rowIndex].Cells["colValue"].Value = popup.ResultValue;
                    // 值可能变化，重新按内容算行高
                    LayoutSections();
                }
            };

            popup.Show(this);
            popup.Activate();
        }

        /// <summary>
        /// 获取配置项的当前值
        /// 优先读 ConfigurationManager.AppSettings（与程序启动读取一致）；
        /// 若配置里没有该键，则用内存中 DeviceConfig 的属性值兜底
        /// </summary>
        private string GetEffectiveValue(string key)
        {
            string raw = System.Configuration.ConfigurationManager.AppSettings[key];
            if (raw != null) return raw;

            var prop = _config.GetType().GetProperty(key);
            if (prop != null)
            {
                object value = prop.GetValue(_config, null);
                if (value != null) return value.ToString();
            }
            return "";
        }

        /// <summary>
        /// 根据配置项类型创建"设置值"单元格控件，防止用户乱输导致配置写坏：
        /// - 布尔项（_boolKeys）：下拉框只允许选择 true / false
        /// - PortName：下拉框列出系统当前检测到的所有串口，供用户直接选择
        /// - 串口通讯参数：波特率用可手输下拉（常用档位 + 自定义），数据位/停止位/校验位用固定选项下拉
        /// - 数字项（_numericKeys）：用 NumericUpDown 单元格，按范围限制上下限与小数位
        /// - 其余文本项：普通文本框
        /// </summary>
        private static DataGridViewCell CreateValueCell(string key, string value)
        {
            if (_boolKeys.Contains(key))
            {
                return CreateStrictComboCell(
                    new[] { "false", "true" },
                    value != null && value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) ? "true" : "false");
            }

            if (key == "PortName" || key == "ScannerPort")
            {
                return CreatePortComboCell(value);
            }

            // 送风机候选 IP 列表 / IO 备用通道映射：只读单元格 + 点击弹出编辑器
            if (key == "FanIpCandidates" || key == "IoBackupChannelMappings")
            {
                var cell = new DataGridViewPopupEditCell();
                cell.Value = value;
                return cell;
            }

            // 串口通讯参数：波特率（含扫码枪）用可手输下拉，支持自定义波特率
            if (key == "BaudRate" || key == "ScannerBaudRate")
            {
                return CreateBaudComboCell(value);
            }

            if (key == "DataBits" || key == "ScannerDataBits")
            {
                return CreateStrictComboCell(new[] { "5", "6", "7", "8" }, value);
            }

            if (key == "StopBits" || key == "ScannerStopBits")
            {
                // 显示 1 / 1.5 / 2，配置存 1 / 15 / 2（15 表示 1.5，与 ScannerService 约定一致）
                return CreateOptionComboCell(
                    new[]
                    {
                        new ComboOption("1", "1"),
                        new ComboOption("1.5", "15"),
                        new ComboOption("2", "2"),
                    },
                    NormalizeStopBits(value));
            }

            if (key == "Parity" || key == "ScannerParity")
            {
                // 界面显示中文，实际存值映射为标准枚举名（None/Odd/Even/Mark/Space），
                // 保证配置文件里只有这 5 种合法值，杜绝非法字符导致下游解析失败
                return CreateOptionComboCell(
                    new[]
                    {
                        new ComboOption("无校验(NONE)", "None"),
                        new ComboOption("奇校验(ODD)", "Odd"),
                        new ComboOption("偶校验(EVEN)", "Even"),
                        new ComboOption("1校验(MARK)", "Mark"),
                        new ComboOption("空格校验(SPACE)", "Space"),
                    },
                    NormalizeParity(value));
            }

            if (_numericKeys.TryGetValue(key, out var range))
            {
                var cell = new DataGridViewNumericUpDownCell
                {
                    Minimum = range.Min,
                    Maximum = range.Max,
                    DecimalPlaces = range.Decimals,
                    Increment = range.Increment
                };

                decimal parsed;
                if (decimal.TryParse(value, out parsed))
                {
                    parsed = Math.Max(range.Min, Math.Min(range.Max, parsed));
                }
                else
                {
                    parsed = Math.Max(range.Min, Math.Min(range.Max, 0));
                }
                cell.Value = parsed;
                return cell;
            }

            var textCell = new DataGridViewTextBoxCell();
            textCell.Value = value;
            return textCell;
        }

        /// <summary>把停止位配置值规整为下拉项实际保存的值（1.5 或 15 都统一为 15）</summary>
        private static string NormalizeStopBits(string value)
        {
            value = (value ?? "").Trim();
            if (value == "1.5" || value == "15") return "15";
            if (value == "2") return "2";
            return "1";
        }

        /// <summary>
        /// 把校验位配置值映射为标准枚举名（None/Odd/Even/Mark/Space）。
        /// 兼容历史写法（小写 none/odd、中文"无校验"、缩写等），
        /// 任何非法/未知值都归一到 None，保证存值只有这 5 种合法枚举名。
        /// </summary>
        private static string NormalizeParity(string value)
        {
            string v = (value ?? "").Trim();
            switch (v.ToLowerInvariant())
            {
                case "none":
                case "n":
                case "无校验":
                    return "None";
                case "odd":
                case "o":
                case "奇校验":
                    return "Odd";
                case "even":
                case "e":
                case "偶校验":
                    return "Even";
                case "mark":
                case "m":
                case "1校验":
                case "标记":
                    return "Mark";
                case "space":
                case "s":
                case "空格校验":
                    return "Space";
                default:
                    return "None";
            }
        }

        /// <summary>
        /// 固定选项下拉单元格（DropDownList，只能选不能手输）。
        /// 选项为纯字符串，当前值不在列表时补一项避免空显示。
        /// </summary>
        private static DataGridViewStrictComboBoxCell CreateStrictComboCell(IEnumerable<string> items, string currentValue)
        {
            var cell = new DataGridViewStrictComboBoxCell();
            StyleComboCell(cell);
            foreach (string item in items)
            {
                if (!cell.Items.Contains(item)) cell.Items.Add(item);
            }
            if (!string.IsNullOrEmpty(currentValue) && !cell.Items.Contains(currentValue))
            {
                cell.Items.Add(currentValue);
            }
            if (!string.IsNullOrEmpty(currentValue))
            {
                cell.Value = currentValue;
            }
            return cell;
        }

        /// <summary>
        /// 固定选项下拉单元格（DropDownList），选项为"显示文本/实际保存值"，
        /// 用于停止位（显示 1.5 存 15）、校验位（显示中文存枚举名）。
        /// </summary>
        private static DataGridViewStrictComboBoxCell CreateOptionComboCell(ComboOption[] options, string currentValue)
        {
            var cell = new DataGridViewStrictComboBoxCell();
            StyleComboCell(cell);
            foreach (ComboOption option in options)
            {
                cell.Items.Add(option);
            }
            cell.DisplayMember = "Display";
            cell.ValueMember = "Value";

            if (!string.IsNullOrEmpty(currentValue))
            {
                bool found = false;
                foreach (ComboOption option in cell.Items)
                {
                    if (option.Value == currentValue) { found = true; break; }
                }
                if (!found)
                {
                    cell.Items.Add(new ComboOption(currentValue, currentValue));
                }
                cell.Value = currentValue;
            }
            return cell;
        }

        /// <summary>串口下拉单元格（DropDownList）：列出系统检测到的所有串口</summary>
        private static DataGridViewStrictComboBoxCell CreatePortComboCell(string currentValue)
        {
            var cell = new DataGridViewStrictComboBoxCell();
            StyleComboCell(cell);
            cell.DropDownWidth = 220;

            string[] ports = SerialPortHelper.GetAllPortNames();
            foreach (string port in ports)
            {
                if (!cell.Items.Contains(port)) cell.Items.Add(port);
            }

            // 当前值不在检测列表里也保留，避免已配置但当前未插的端口被误清
            if (!string.IsNullOrEmpty(currentValue) && !cell.Items.Contains(currentValue))
            {
                cell.Items.Add(currentValue);
            }

            // 留空 = 启动时自动识别，因此空值保持空（下拉框显示空白待选），不强制选第一个
            if (!string.IsNullOrEmpty(currentValue))
            {
                cell.Value = currentValue;
            }
            return cell;
        }

        /// <summary>波特率下拉单元格（可手输）：列出常用档位，也支持输入自定义波特率</summary>
        private static DataGridViewEditableComboBoxCell CreateBaudComboCell(string currentValue)
        {
            var cell = new DataGridViewEditableComboBoxCell();
            StyleComboCell(cell);

            string[] rates =
            {
                "110", "300", "600", "1200", "2400", "4800",      // 低速档
                "9600", "19200", "38400", "57600",                 // 中速档
                "115200", "230400", "460800", "921600"             // 高速档
            };
            foreach (string rate in rates)
            {
                if (!cell.Items.Contains(rate)) cell.Items.Add(rate);
            }

            // 当前值不在列表里也补一项，便于回显自定义波特率
            if (!string.IsNullOrEmpty(currentValue) && !cell.Items.Contains(currentValue))
            {
                cell.Items.Add(currentValue);
            }
            cell.Value = currentValue;
            return cell;
        }

        /// <summary>下拉单元格统一样式：扁平无灰底、白底深字，与页面风格一致</summary>
        private static void StyleComboCell(DataGridViewComboBoxCell cell)
        {
            cell.FlatStyle = FlatStyle.Flat;
            cell.DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton;
            cell.Style.BackColor = Color.White;
            cell.Style.ForeColor = Color.FromArgb(48, 48, 48);
            cell.Style.SelectionBackColor = Color.FromArgb(48, 119, 238);
            cell.Style.SelectionForeColor = Color.White;
        }

        /// <summary>
        /// 按配置项类型校验用户输入的值是否合法
        /// </summary>
        /// <param name="key">配置项名称</param>
        /// <param name="value">用户输入值（已 Trim）</param>
        /// <param name="error">校验失败时的中文提示</param>
        /// <returns>true=合法，false=不合法</returns>
        private static bool ValidateValue(string key, string value, out string error)
        {
            error = null;

            // 自由文本/列表类配置项（IP、端口映射表等），不强制校验
            switch (key)
            {
                case "FanIpCandidates":
                case "IoBackupChannelMappings":
                    return true;
            }

            switch (key)
            {
                // 整数
                case "TotalBarometers":
                case "TotalInputs":
                case "TotalOutputs":
                case "CollectInterval":
                case "PanelColumns":
                case "PanelRows":
                case "BaudRate":
                case "DataBits":
                case "StopBits":
                case "SerialReadTimeoutMs":
                case "SerialWriteTimeoutMs":
                case "TcpSendTimeoutMs":
                case "TcpReceiveTimeoutMs":
                case "BarometerDefaultDecimalPlaces":
                case "PlcPort":
                case "FanPort":
                case "FanTimeoutMs":
                case "VacuumConfirmTimeoutMs":
                case "CommunicationLossAlarmCount":
                case "MaxTestDurationSeconds":
                case "ScannerBaudRate":
                case "ScannerDataBits":
                case "ScannerStopBits":
                    if (!int.TryParse(value, out _)) { error = "应为整数"; return false; }
                    return true;

                // 字节（0~255）
                case "IoUnitId":
                case "FanUnitId":
                    if (!byte.TryParse(value, out _)) { error = "应为 0~255 的整数"; return false; }
                    return true;

                // 寄存器地址（支持十进制或十六进制 0x 写法）
                case "IoInputRegisterStartAddress":
                case "IoOutputRegisterStartAddress":
                case "BarometerPressureRegisterAddress":
                    if (!TryParseUShort(value)) { error = "应为数字或十六进制（如 0x1000）"; return false; }
                    return true;

                // 小数（decimal / float）
                case "BarometerPressureScale":
                case "AlarmPressureThresholdKPa":
                    if (!decimal.TryParse(value, out _)) { error = "应为数字"; return false; }
                    return true;
                case "FanTempAlarmLimitC":
                    if (!float.TryParse(value, out _)) { error = "应为数字"; return false; }
                    return true;

                // 布尔
                case "UseMockCommunication":
                case "InvertInputs":
                case "InvertOutputs":
                case "IoBackupChannelMappingEnabled":
                case "AlarmWhenPressureHigherThanThreshold":
                case "FanEnabled":
                case "FanAutoDetectEnabled":
                case "UseDiAlarmContact":
                case "ScannerEnabled":
                case "ScannerDebugLog":
                    if (!bool.TryParse(value, out _)) { error = "应为 true 或 false"; return false; }
                    return true;

                // 其余为字符串类（端口名、IP、关键词、校验位等），不做强制校验
                default:
                    return true;
            }
        }

        /// <summary>
        /// 解析 ushort（支持 "4096" 或 "0x1000" 两种写法）
        /// </summary>
        private static bool TryParseUShort(string value)
        {
            return TryParseUShort(value, out _);
        }

        /// <summary>
        /// 解析 ushort（支持 "4096" 或 "0x1000" 两种写法），并输出解析结果
        /// </summary>
        private static bool TryParseUShort(string value, out ushort result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return ushort.TryParse(value.Substring(2), System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out result);
            }
            return ushort.TryParse(value, out result);
        }

        /// <summary>
        /// "保存设置"按钮点击事件
        ///
        /// 【流程】
        /// 1. 遍历全部分类表格，收集每行的 key / 值
        /// 2. 按类型校验每个值，不合法项整批拦截并列出（避免写坏配置文件）
        /// 3. 写回 exe.config 的 appSettings（OpenExeConfiguration + Save）
        /// 4. 刷新 appSettings 缓存，提示重启生效
        /// </summary>
        private void btnSave_Click(object sender, EventArgs e)
        {
            var changes = new Dictionary<string, string>();
            var invalid = new List<string>();

            // 遍历唯一表格的每一行（跳过分组标题行），收集各配置项的修改
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow) continue;
                if (_groupRows.Contains(row.Index)) continue;   // 分组标题行不参与收集

                string key = row.Cells["colKey"].Value?.ToString();
                if (string.IsNullOrWhiteSpace(key)) continue;

                string value = (row.Cells["colValue"].Value?.ToString() ?? "").Trim();
                if (!ValidateValue(key, value, out string error))
                {
                    invalid.Add($"【{key}】 {value}  →  {error}");
                    continue;
                }
                changes[key] = value;
            }

            if (invalid.Count > 0)
            {
                MessageBox.Show("以下配置值不合法，请修改后再保存：\r\n\r\n" +
                    string.Join("\r\n", invalid),
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 写回配置文件（程序运行目录下的 exe.config，与 App.config 同源）
            try
            {
                var config = System.Configuration.ConfigurationManager.OpenExeConfiguration(
                    System.Configuration.ConfigurationUserLevel.None);

                foreach (var kv in changes)
                {
                    var setting = config.AppSettings.Settings[kv.Key];
                    if (setting == null)
                    {
                        config.AppSettings.Settings.Add(kv.Key, kv.Value);
                    }
                    else
                    {
                        setting.Value = kv.Value;
                    }
                }

                config.Save(System.Configuration.ConfigurationSaveMode.Modified);
                System.Configuration.ConfigurationManager.RefreshSection("appSettings");
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存配置失败：" + ex.Message, "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 【热生效】保存成功后就地回写内存中的 DeviceConfig 实例（主窗体传入的同一引用）：
            // 各服务每次读写实时访问 _config.xxx，回写后业务逻辑类配置立即生效；
            // 结构型配置不回写（避免运行期结构不一致），需重启后生效。
            ApplyChangesToConfig(changes);

            // 本次保存的配置项 key 交给主窗体，由主窗体按需触发重连（串口/耦合器/送风机/扫码枪）
            SavedKeys = new HashSet<string>(changes.Keys);

            // 弹窗提示：是否包含需重启生效的结构型配置
            var structuralChanged = changes.Keys.Where(k => StructuralKeys.Contains(k)).ToList();
            string saveMessage;
            if (structuralChanged.Count > 0)
            {
                saveMessage = "设置已保存。以下配置项需重启程序后生效：\r\n\r\n" +
                    string.Join("、", structuralChanged.Select(k =>
                        _descriptions.TryGetValue(k, out string d) ? d : k)) +
                    "\r\n\r\n其余配置项已即时生效。";
            }
            else
            {
                saveMessage = "设置已保存并即时生效。";
            }

            MessageBox.Show(saveMessage, "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        /// <summary>
        /// 把本次保存的配置项就地回写内存中的 DeviceConfig 实例（热生效核心）：
        /// 各服务持有的都是主窗体传入的同一实例，且每次读写实时访问 _config.xxx，
        /// 回写后业务逻辑类配置（寄存器地址 / IO 映射 / 取反 / 小数位 / 阈值等）立即生效，
        /// 无需重启；连接参数类由主窗体另触发重连。
        /// 结构型配置（<see cref="StructuralKeys"/>）不回写，需重启后生效。
        /// </summary>
        private void ApplyChangesToConfig(Dictionary<string, string> changes)
        {
            foreach (var kv in changes)
            {
                if (StructuralKeys.Contains(kv.Key)) continue;
                var prop = typeof(DeviceConfig).GetProperty(kv.Key);
                if (prop == null || !prop.CanWrite) continue;
                try
                {
                    object converted = ConvertConfigValue(prop.PropertyType, kv.Value);
                    if (converted != null) prop.SetValue(_config, converted);
                }
                catch
                {
                    // 单项转换失败不影响其它项（保存前已过类型校验，正常不会发生）
                }
            }
        }

        /// <summary>
        /// 把配置字符串按目标属性类型转换（与主窗体启动加载逻辑一致：
        /// 支持 0x 十六进制寄存器地址、IO 映射表 / 候选 IP 列表等复合类型）
        /// </summary>
        private static object ConvertConfigValue(Type propType, string value)
        {
            if (value == null) return null;
            if (propType == typeof(bool))   { return bool.TryParse(value, out bool b) ? b : (object)null; }
            if (propType == typeof(int))    { return int.TryParse(value, out int i) ? i : (object)null; }
            if (propType == typeof(ushort)) { return TryParseUShort(value, out ushort u) ? u : (object)null; }
            if (propType == typeof(byte))   { return byte.TryParse(value, out byte b) ? b : (object)null; }
            if (propType == typeof(decimal)){ return decimal.TryParse(value, out decimal d) ? d : (object)null; }
            if (propType == typeof(float))  { return float.TryParse(value, out float f) ? f : (object)null; }
            if (propType == typeof(string)) { return value; }
            if (propType == typeof(List<IoOutputChannelRemap>)) { return IoOutputChannelRemap.ParseAll(value, out _); }
            if (propType == typeof(List<string>)) { return DeviceConfig.ParseFanIpCandidates(value); }
            return null;
        }

        /// <summary>
        /// "关闭"按钮点击事件：直接关闭窗口（不保存）
        /// </summary>
        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
