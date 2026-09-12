using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AgingTestSystem.Controls;
using AgingTestSystem.Models;
using AgingTestSystem.Services;
using AgingTestSystem.Views;

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
    ///   报警参数 / 冷却送风机 / 老化测试业务 / 工艺策略（V1.67）/ 扫码枪
    ///
    /// 内容放在单个 UIDataGridView（填满 pnlScroll）里，表格自带垂直滚动条
    /// （DataGridView 虚拟化绘制，只重绘可见行），所有分类一眼看全，不用来回切页签，
    /// 滚动流畅不卡顿。
    ///
    /// 点击【保存设置】后分流写回（【V1.67】）：
    /// - 策略 key（ProjectPolicyStore.PolicyKeys，工艺策略分类）→
    ///   Projects/&lt;当前项目&gt;/Policy.json（跟项目走，切项目即换策略）；
    /// - 其余 → 程序运行目录下的 exe.config（跟机器走）；
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
    /// │ [________________________] [✕]               │ ← pnlSearch（Dock=Top；【V1.54i】已去掉"搜索配置项："文字，输入框左边缘与分组标题文字左边缘严格对齐）
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
    public partial class SettingsForm : Sunny.UI.UIForm
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

        /// <summary>
        /// 表格自带垂直滚动条子控件（SunnyUI UIDataGridView 内部创建，盖在表格右边缘 X=1377 起）。
        /// CreateGrid 里关闭了它的 ShowLeftLine（去掉贯穿所有行的左竖线），
        /// 但 V1.54j 又需要"数据行最右边缘有表格右边界竖线"，于是改由 RowPostPaint
        /// 给非分组行补画；这里保存引用用于读取 Bounds.Left 定位竖线 X。
        /// </summary>
        private Sunny.UI.UIScrollBar _gridScrollBar;

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
        /// <summary>
        /// 窗体已关闭标记（V1.72.15 新增，全仓关窗竞态排查：_pressTimer/复制气泡的 BeginInvoke
        /// 在关闭前后脚仍能投递进已销毁句柄。OnFormClosed 首行置位 + 停表，投递点双查）。
        /// </summary>
        private volatile bool _closed;
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
            "FanTempShutdownEnabled",
            "ScannerEnabled",
            "ScannerDebugLog",
            "MesEnabled",
            "MesMockEnabled",
            "SkipVacuum",
            "VentValveEnabled",
            "UsePowerMeter",
            "DisplayModeEnabled",
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
            "UsePowerMeter",
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
        /// 【V1.58】是否在本次会话中修改了主页布局（在"主页区域"分类点击编辑器并保存后置 true）。
        /// 主页布局不写在 App.config，而是 HomeLayout.json；主窗体在设置窗口关闭后
        /// 读取该标记，为 true 则调用 ApplyHomeLayout() 让新布局立即生效。
        /// </summary>
        public bool HomeLayoutChanged { get; private set; }

        /// <summary>
        /// 【V1.65】主窗体当前生效的右侧宽度（主窗体构造时传入：无 json 时为按窗口比例
        /// 算出的值，有 json 时为文件绝对值）。"主页区域"行的摘要显示与编辑器初始化
        /// 都以它为准，保证设置里看到的和主界面实际一致。为 null 时（理论上只有
        /// 将来别的调用方不传才出现）回退到 MainForm.DefaultRightPanelWidth。
        /// </summary>
        private readonly int? _effectiveRightWidth;

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
            { "IoBackupChannelMappings", "IO 备用通道映射表（点击编辑：寄存器@通道均为十六进制，通道0x00~0x0F，如 0x2000@0x00->0x2009@0x00）" },
            { "TcpSendTimeoutMs", "TCP 发送超时（毫秒，耦合器/送风机通用）" },
            { "TcpReceiveTimeoutMs", "TCP 接收超时（毫秒，耦合器/送风机通用）" },

            // ===== 气压表寄存器 =====
            { "BarometerPressureRegisterAddress", "气压表压力寄存器地址（0x0001）" },
            { "BarometerDefaultDecimalPlaces", "气压表小数位（务必与仪表实际一致，当前 1）" },
            { "BarometerPressureScale", "压力缩放系数（读数 × 该值）" },

            // ===== 报警参数 =====
            { "AlarmPressureThresholdKPa", "报警压力阈值（kPa，如 -5）" },
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
            { "FanTempShutdownEnabled", "超温是否全线联停（false=只记日志；true=超温自动停全部在测工位，默认false）" },

            // ===== 载台电流（V1.74：Q2 通用骨架总开关，跟机器，结构型改后重启生效）=====
            { "UsePowerMeter", "是否启用载台电流回采（false=现状：不接电表，电流恒无数据；true=按Mock开关读数，电表到货即插即用）" },

            // ===== 工艺策略（V1.67：7 个待确认点全部可配，跟项目走存 Policy.json）=====
            { "ZeroDurationPolicy", "0时长启动策略：只警告=提示后可继续（现状）/硬拦截=含0时长工位直接阻断" },
            { "EmptySnPolicy", "空SN启动策略：只警告=提示后可继续（现状）/硬拦截=含空SN工位直接阻断" },
            { "FanDisconnectPolicy", "送风机断连策略：只提示=警告后照跑（现状）/阻断启动=风机未连接不让点火" },
            { "VacuumFailKind", "真空失败责任：产品责任=记FAIL（现状）/治具责任=记装夹异常可重测" },
            { "CompletionJudgePolicy", "完成判定口径：自动PASS=到时无报警即PASS（现状）/待判定=下料人工录PASS/FAIL" },
            { "PowerLossPolicy", "断电恢复策略：整台重测=满时长重跑（现状）/续跑剩余时长=重抽真空+补足剩余" },
            { "AgingPressureLossPolicy", "老化中失压策略：停机报警=关阀断电（现状）/只记不停=记事件继续老化" },
            { "CompletionAction", "到时完成动作：只下电关阀（现状）/蜂鸣提醒/破空泄压（需开破空阀开关+配点位）/都要" },
            { "EventIdentityMode", "事件行SN/配方取值：记录现值=事件瞬间绑定的（现状）/启动定格=该轮启动时的（中途重绑不污染已跑任务，无快照回退现值）" },
            { "VentValveDoPoint", "破空阀DO输出点内部编号（如225），0=未配置（默认，选了泄压也只记日志不写DO）" },
            { "VentValveEnabled", "本机是否装破空阀（false=无阀现状：手动破空按钮隐藏+泄压选项保存即拦；true=有阀项目才开）" },

            // ===== MES 对接（V1.68 二期：映射层可配；连接跟机器，触发器/映射/静态跟项目存 Policy.json）=====
            { "MesEnabled", "是否启用MES上报（false=完全不碰网络，默认false；true=按触发器POST到接收地址）" },
            { "MesMockEnabled", "MES联调Mock（false/true；true=不发HTTP，只写CSV含完整JSON，MES没好也能验格式）" },
            { "MesEndpoint", "MES接收地址（完整URL，单入口；留空=不发，即使开了开关也只记日志）" },
            { "MesTimeoutMs", "HTTP超时（毫秒，后台线程发，不卡采集）" },
            { "MesAuthType", "鉴权方式（None=无 / Bearer=Token / Basic=用户名密码）" },
            { "MesAuthToken", "Bearer token（明文，现场工控机物理隔离；客户要求加密再做DPAPI二期）" },
            { "MesAuthUser", "Basic用户名" },
            { "MesAuthPassword", "Basic密码" },
            { "MesRetryCount", "单条失败重试次数（0=只发一次；全灭进离线缓存，下次成功顺带补发）" },
            { "MesRetryIntervalMs", "重试间隔（毫秒）" },
            { "MesTriggers", "上报触发器（跟项目，逗号分隔：Start=启动/Complete=完成/Alarm=报警/UnloadJudge=下料判定；留空=四个全报）" },
            { "MesFieldMap", "字段映射（跟项目，MES名=本站名，分号分隔；本站字段：time/lot/device/event/sn/recipe/result/detail/pressure/temp/duration/displayMode/project/disposition/defectCode；留空=直通。如 eqId=device;lotNo=lot）" },
            { "MesStaticFields", "静态附加字段（跟项目，键=值，分号分隔，原样并入每次上报。如 line=L5;workshop=A3）" },
            { "MesCustomHeaders", "自定义HTTP头（头名=头值，分号分隔。如 X-Line=L5;X-ApiVer=2；鉴权头同名时鉴权优先）" },
            { "MesEndpointMap", "按事件分地址（触发器=URL，分号分隔。如 Alarm=http://x/api/alarm；没配的事件回退默认地址）" },

            // ===== 规则流程（V1.69 三期：规则表达式 + 阶段流，全部跟项目走 Policy.json）=====
            { "SkipVacuum", "跳过抽真空（false=现状三阶段；true=启动即上电+压力报警同步豁免，机械夹具专用。配错在真空架上开=真空保护全丢，开前确认产品已机械固定！）" },
            { "CompleteExpression", "完成表达式（单行，空=禁用走内置时长；成立即完成，只能提前。如 temp > 85。变量13个：pressure/temp/tempset/hum/device/delaysecs/vacsecs/agesecs/duration/threshold/di0/hour/current）" },
            { "CustomAlarmRules", "自定义报警规则（点击编辑，多行，一行一条：名称 | 表达式 | 持续秒。触发=关阀断电记FAIL。变量同上）" },

            // ===== 报表导出（V1.74：Q8 列可配，跟项目走 Policy.json）=====
            { "ReportColumns", "报表列（跟项目，点击编辑：一行一列，显示名文本+字段下拉，可增删/上下移；留空=缺省预设8列）" },

            // ===== 显示模式字典（V1.74：Q20 记录层可配，跟项目走 Policy.json）=====
            { "DisplayModes", "显示模式字典（跟项目，点击编辑：一行一个选项，可改/增/删；留空=缺省预设8项）" },
            { "DisplayModeEnabled", "是否启用显示模式维度（跟项目；false=三窗隐藏该行，当前项目零打扰；true=显示下拉+字典生效）" },

            // ===== 扫码枪 =====
            { "ScannerEnabled", "是否启用扫码枪（false/true）" },
            { "ScannerPort", "扫码枪固定串口（留空则按关键词自动识别）" },
            { "ScannerDeviceKeyword", "扫码枪设备识别关键词（如 Xenon 1902）" },
            { "ScannerBaudRate", "扫码枪波特率（115200）" },
            { "ScannerDataBits", "扫码枪数据位（8）" },
            { "ScannerStopBits", "扫码枪停止位（1）" },
            { "ScannerParity", "扫码枪校验位（None）" },
            { "ScannerDebugLog", "扫码枪心跳调试日志开关（false/true）" },

            // ===== 主页区域（V1.58，非 App.config 项，点击弹可视化编辑器） =====
            { "HomeLayout", "主界面各区域尺寸（顶部标题栏/菜单栏/右侧区域/状态栏），点击弹出可视化编辑器拖动调整" },
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
                "MaxTestDurationSeconds", "UseDiAlarmContact", "FanTempAlarmLimitC",
                "FanTempShutdownEnabled"
            }),
            // 【V1.74】载台电流独立分类（Q2 骨架总开关，跟机器；显示在业务区后面，找得着）
            ("载台电流", new string[]
            {
                "UsePowerMeter"
            }),
            // 【V1.67】工艺策略独立分类（7 个待确认点 + 完成动作 + 破空点位，共 10 项；
            // FanTempShutdownEnabled 同时是策略，显示在老化测试业务里，这里不再重复列，
            // 但它进 Policy.json——名单以 ProjectPolicyStore.PolicyKeys 为准，不以分类为准）
            ("工艺策略", new string[]
            {
                "ZeroDurationPolicy", "EmptySnPolicy",
                "FanDisconnectPolicy", "VacuumFailKind",
                "CompletionJudgePolicy", "PowerLossPolicy",
                "AgingPressureLossPolicy", "CompletionAction", "VentValveDoPoint", "VentValveEnabled",
                "EventIdentityMode", "DisplayModeEnabled", "DisplayModes"
            }),
            // 【V1.69】规则流程（表达式 + 阶段流，全部跟项目；同表编辑，保存按 PolicyKeys 分流）
            ("规则流程", new string[]
            {
                "SkipVacuum", "CompleteExpression", "CustomAlarmRules"
            }),
            // 【V1.68】MES 对接（连接跟机器；触发器/映射/静态跟项目但同表编辑，
            // 保存时按 PolicyKeys 分流——名单以 ProjectPolicyStore.PolicyKeys 为准）
            ("MES 对接", new string[]
            {
                "MesEnabled", "MesMockEnabled", "MesEndpoint", "MesTimeoutMs",
                "MesAuthType", "MesAuthToken", "MesAuthUser", "MesAuthPassword",
                "MesRetryCount", "MesRetryIntervalMs",
                "MesTriggers", "MesFieldMap", "MesStaticFields",
                "MesCustomHeaders", "MesEndpointMap"
            }),
            // 【V1.74】报表导出独立分类（Q8 列可配，跟项目；与 MES 对接并列，找得着）
            ("报表导出", new string[]
            {
                "ReportColumns"
            }),
            // 【V1.74】显示模式字典进工艺策略分类（Q20 记录层可配，跟项目；改画面选项来这里）
            // 注：工艺策略分类名单 ≠ PolicyKeys 名单（后者以 ProjectPolicyStore 为准），
            // 这里只是"显示位置"，分流/校验认 PolicyKeys（见 ValidateValue 与 PersistChanges）。
            ("扫码枪", new string[]
            {
                "ScannerEnabled", "ScannerPort", "ScannerDeviceKeyword",
                "ScannerBaudRate", "ScannerDataBits", "ScannerStopBits", "ScannerParity",
                "ScannerDebugLog"
            }),
            ("主页区域", new string[]
            {
                "HomeLayout"
            }),
        };

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="config">当前生效的设备配置（主窗体传入，用于取兜底值与类型校验）</param>
        /// <param name="effectiveRightWidth">【V1.65】主窗体当前生效的右侧宽度（见 _effectiveRightWidth）</param>
        public SettingsForm(DeviceConfig config, int? effectiveRightWidth = null)
        {
            InitializeComponent();
            _config = config;
            _effectiveRightWidth = effectiveRightWidth;

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
        /// 窗体关闭时停长按计时器（V1.72.15 新增，全仓关窗竞态排查：_pressTimer 是字段定时器，
        /// 不在 components 容器里，Dispose 不会自动停；关后到点 Tick 即碰已释放表格。
        /// 首行置 _closed 拦投递，停表防 Tick）。
        /// </summary>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _closed = true;
            try
            {
                _pressTimer.Stop();
                _pressTimer.Tick -= PressTimer_Tick;
                _pressTimer.Dispose();
            }
            catch { }
            base.OnFormClosed(e);
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

            // 【V1.54h】关闭表格自带垂直滚动条的"左侧竖线"。
            // 之前用户反馈"分组标题行最右侧有表格的竖线"，真正的元凶不是 cell border：
            // SunnyUI UIDataGridView 内置一个 UIScrollBar 子控件覆盖在表格右边缘
            // （X = 最后一列右边界 ~ 表格右边界），它默认 ShowLeftLine = True，
            // 会在 X=918（逻辑像素）画一条 80,160,255 的蓝色竖线，横跨整张表格每一行，
            // 于是浅蓝色的分组标题色带最右侧也被切出一条竖线，看起来像"还在表格里"。
            // 之前的 V1.54f/g 用 RowPostPaint 填充 _grid.Width+1 想盖住它，
            // 但 RowPostPaint 的 Graphics 被裁剪在行显示矩形内（X < 919），够不到这一像素，
            // 而 UIScrollBar 是子控件、永远画在表格内容之上，所以一直没生效。
            // 正解就是关闭该子控件的 ShowLeftLine：竖线彻底消失，
            // 分组标题色带一路延伸到滚动条处；数据行右侧也只留下滚动条轨道（浅蓝），观感更干净。
            // 该子控件在 UIDataGridView 创建时就已存在（无需等 HandleCreated），这里直接找出来关掉。
            // 【V1.54j】但用户需要"数据行最右边缘有表格右边界竖线"（见 Grid_RowPostPaint），
            // 该竖线不能再靠 UIScrollBar 的 ShowLeftLine（会连分组标题行一起画），
            // 所以这里保存滚动条引用，由 RowPostPaint 用 Bounds.Left 定位并只给数据行补画。
            var uiScroll = grid.Controls.OfType<Sunny.UI.UIScrollBar>().FirstOrDefault();
            if (uiScroll != null)
            {
                uiScroll.ShowLeftLine = false;
                _gridScrollBar = uiScroll;
            }

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
        /// 分组标题行画完后：去掉**列间垂直线** + 表格右边缘残留竖线，**下边线**用蓝色（标题色带延伸）。
        /// 视觉上让标题行像"一条横跨表格的标题带"：
        ///   上边线：DataGridView 默认 cell border（与数据行统一，不另画避免叠色）
        ///   下边线：自画蓝色（与标题文字同色，色带延伸）
        ///   中间：列与列之间、_grid 右边缘都不再有垂直线
        /// 【V1.54h】说明：之前"最右侧那条竖线"其实是表格自带滚动条 UIScrollBar 的
        /// ShowLeftLine（见 CreateGrid 里 V1.54h 注释），由 CreateGrid 直接关闭；
        /// 这里 fill 的右边界仍取 _grid.Width + 1，保证覆盖到 RowPostPaint 能被绘制的
        /// 最大范围（Graphics 被裁剪在行显示矩形内，超出部分本来就画不上，不影响结果）。
        /// 【V1.54j】补充：ShowLeftLine 关掉后，**数据行**最右边缘也失去了表格右边界竖线，
        /// 用户在设置窗口需要"最右边有一条竖线"来明确表格边界，于是这里对非分组行
        /// （即普通配置行）补画一条 1px 竖线，位置在滚动条子控件左边缘左侧 1px
        /// （= colValue 列右边界可视处），颜色用 _grid.GridColor（与默认 cell border 一致）。
        /// 分组标题行故意不画：标题是"横跨表格的色带"，右侧不该有竖线切断它。
        /// </summary>
        private void Grid_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            // 分组标题行走原有"色带填充 + 蓝色下边线"逻辑；普通数据行只需补画右边缘竖线
            if (!_groupRows.Contains(e.RowIndex))
            {
                DrawDataRowRightBorder(e);
                return;
            }

            Color groupBack = Color.FromArgb(237, 243, 253);   // 标题行浅蓝底
            // 【V1.54d】下边线：与标题文字同色（深蓝 48,119,238），色带延伸
            Color groupLine = Color.FromArgb(48, 119, 238);
            // 【V1.54h】右边界覆盖范围：fill 到 _grid.Width + 1（Graphics 坐标系下）。
            // 实测 RowPostPaint 的 Graphics 被裁剪在行显示矩形内（最右像素 X=918 逻辑），
            // 即使填到 _grid.Width+1 也画不到 _grid.Right；但保留它没坏处，
            // 而"标题行最右侧竖线"的真正来源是滚动条 ShowLeftLine，已在 CreateGrid 关闭。
            int coverRight = _grid.Width + 1;
            using (var bgBrush = new SolidBrush(groupBack))
            using (var linePen = new Pen(groupLine, 1f))
            {
                // ① 整行宽矩形：覆盖列间垂直线（画到行显示矩形的最大可绘范围）
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
        /// 给普通配置行（非分组标题行）补画表格最右边缘的 1px 竖线。
        /// 背景：ShowLeftLine 被关闭后（V1.54h），数据行最右侧没有右边界线；
        /// 但分组标题行是"横跨表格的色带"，不需要这条线，所以不能直接恢复 ShowLeftLine
        /// （它会连分组行一起画，回到 V1.54g 的老 bug）。折中：这里用 RowPostPaint
        /// 只对非分组行画竖线，颜色取 _grid.GridColor（104,173,255，与数据行 cell border 一致），
        /// X 取滚动条子控件左边缘 - 1：滚动条 Bounds.X=1377 是最后一列右边界处的
        /// 第一个不可见像素（被滚动条覆盖），它的左侧 1px 正是 colValue 列可见的最右像素。
        /// </summary>
        private void DrawDataRowRightBorder(DataGridViewRowPostPaintEventArgs e)
        {
            // 没有滚动条引用时（理论上不会发生）退化为用 DisplayRectangle 右边界定位
            int lineX = _gridScrollBar != null
                ? _gridScrollBar.Bounds.Left - 1
                : _grid.DisplayRectangle.Right - 1;

            // 只画行显示矩形范围内有效的那段竖线（Graphics 已被裁剪在行矩形内，超出自动无效）
            using (var pen = new Pen(_grid.GridColor, 1f))
            {
                e.Graphics.DrawLine(pen,
                    lineX, e.RowBounds.Top,
                    lineX, e.RowBounds.Bottom - 1);
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

                    // 【V1.67】说明 tooltip：悬停任一单元格都显示完整说明，
                    // 超过 40 字自动换行（WinForms ToolTip 不自动换行，靠 WrapTooltip 插换行符）。
                    string tip = WrapTooltip(desc ?? "");
                    if (!string.IsNullOrEmpty(tip))
                    {
                        foreach (DataGridViewCell c in _grid.Rows[rowIdx].Cells)
                        {
                            c.ToolTipText = tip;
                        }
                    }
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

            // 【V1.54i】去掉"搜索配置项："文字标题（多余，直接看输入框占位符就明白用途），
            // 并让搜索框左边缘与下面"基础配置"等分组标题文字左边缘**严格对齐**：
            //   分组标题文字左边缘在 pnlScroll 客户区 = pnlScroll.Padding.Left + 8
            //     （grid 填满 Padding 内区，Grid_CellPainting 给 colKey 文字留 8px 左内边距）
            //   pnlSearch（Dock=Top）左边缘在 pnlScroll 客户区 = pnlScroll.Padding.Left
            //   所以 _txtSearch.Location.X = Padding.Left + 8 - Padding.Left = **8**（恒等，与 Padding 无关）
            // 实测 pnlScroll.Padding.Left=18 时，旧值 20 会让输入框比文字右偏 12px；改为 8 后逐像素对齐。
            // 文本框右边缘保持与旧布局一致（8+宽442=450），清除按钮 X=454 不动，间距 4px 不变。
            _txtSearch = new Sunny.UI.UITextBox
            {
                Location = new Point(8, 5),
                Size = new Size(442, 26),
                // 【V1.54d】文本框字体 10F，与分组标题字号一致；保持表格内文字的视觉协调
                Font = new Font(this.Font.FontFamily, 10F),
                Watermark = "输入关键字过滤配置项"
            };
            _txtSearch.TextChanged += TxtSearch_TextChanged;
            pnlSearch.Controls.Add(_txtSearch);

            _btnClearSearch = new Button
            {
                Text = "✕",
                // 清除按钮紧贴文本框右侧（文本框右边缘 450 + 间距 4）
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
            else if (key == "CustomAlarmRules")
            {
                // 【V1.69】自定义报警规则：多行文本弹窗编辑（实时校验），结果写回单元格
                string currentValue = grid.Rows[e.RowIndex].Cells["colValue"].Value?.ToString() ?? "";
                ShowRuleListPopup(grid, e.RowIndex, currentValue);
            }
            else if (key == "ReportColumns")
            {
                // 【V1.74】报表列：表格弹窗编辑（一行一列：显示名文本 + 字段下拉，
                // 增删/上下移），结果写回单元格
                string currentValue = grid.Rows[e.RowIndex].Cells["colValue"].Value?.ToString() ?? "";
                ShowReportColumnsPopup(grid, e.RowIndex, currentValue);
            }
            else if (key == "DisplayModes")
            {
                // 【V1.75】显示模式字典：列表弹窗编辑（一行一个选项，可改/增/删），
                // 结果写回单元格（录入窗下拉读的就是这份名单）
                string currentValue = grid.Rows[e.RowIndex].Cells["colValue"].Value?.ToString() ?? "";
                ShowDisplayModesPopup(grid, e.RowIndex, currentValue);
            }
            else if (key == "HomeLayout")
            {
                // 【V1.58】主页区域调整：弹出可视化编辑器，保存后刷新该行显示。
                ShowHomeLayoutEditor(grid, e.RowIndex);
            }
        }

        /// <summary>
        /// 【V1.58.1】获取"当前生效的主页布局"。
        ///
        /// 【V1.65】右侧宽度的取值语义跟随主窗体：无 HomeLayout.json 时主界面是按窗口
        /// 比例算出的值（主窗体构造本窗体时经 effectiveRightWidth 传入），有 json 时
        /// 以文件为准。此方法统一"编辑器初始化 / 设置表摘要显示 / 点击编辑"三处的取值，
        /// 避免未配置时编辑器里显示固定值、主界面实际是另一套的偏差。
        /// </summary>
        private Models.HomeLayoutConfig GetEffectiveHomeLayout()
        {
            var layout = Models.HomeLayoutConfig.LoadOrDefault();
            if (!System.IO.File.Exists(Models.HomeLayoutConfig.GetConfigPath()))
            {
                layout.RightPanelWidth = _effectiveRightWidth ?? MainForm.DefaultRightPanelWidth;
            }
            return layout;
        }

        /// <summary>
        /// 【V1.58】弹出"主页区域调整"可视化编辑器（拖动矩形块边缘调整主界面各区域尺寸）。
        /// 编辑结果直接写入 HomeLayout.json（HomeLayoutConfig.Save），保存后刷新本行摘要，
        /// 并置位 <see cref="HomeLayoutChanged"/> 供主窗体在设置关闭后重新应用布局。
        /// 注意：HomeLayout 不是 App.config 项，保存配置时会被跳过（见 btnSave_Click），
        /// 不会误写入 App.config。
        /// </summary>
        private void ShowHomeLayoutEditor(DataGridView grid, int rowIndex)
        {
            var layout = GetEffectiveHomeLayout();
            using (var editor = new HomeLayoutEditorForm(layout))
            {
                // 【V1.60.4】走窗体自己的 ApplyTheme：整窗着色 + 预览画布深色换纯黑底
                // （布局预览画布底由窗体显式指定，见 HomeLayoutEditorForm.ApplyTheme）
                editor.ApplyTheme();
                if (editor.ShowDialog(this) == DialogResult.OK)
                {
                    // 保存成功：刷新本行显示当前尺寸摘要，并标记"已改主页布局"
                    grid.Rows[rowIndex].Cells["colValue"].Value = BuildHomeLayoutSummary(layout);
                    LayoutSections();
                    HomeLayoutChanged = true;
                }
            }
        }

        /// <summary>把布局配置整理成一行摘要文字（设置表格里显示用）</summary>
        private static string BuildHomeLayoutSummary(Models.HomeLayoutConfig layout)
        {
            return $"右侧区域 {layout.RightPanelWidth}px | 顶部标题栏 {layout.TopBarHeight}px | 菜单栏 {layout.MenuHeight}px | 状态栏 {layout.StatusBarHeight}px";
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
            // 【V1.72.15】关窗竞态：关闭后 BeginInvoke 进已销毁句柄即炸，先查后包 try。
            try
            {
                if (!_closed && !IsDisposed && !Disposing && IsHandleCreated)
                    BeginInvoke(new Action(ShowPendingCopyTip));
            }
            catch { }
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
            if (_closed || IsDisposed || Disposing) return;
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
            // 【V1.72.15】同上，关后丢弃。
            try
            {
                if (!_closed && !IsDisposed && !Disposing && IsHandleCreated)
                    BeginInvoke(new Action(ShowPendingCopyTip));
            }
            catch { }
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
                try
                {
                    if (!string.IsNullOrEmpty(popup.ResultValue))
                    {
                        grid.Rows[rowIndex].Cells["colValue"].Value = popup.ResultValue;
                        // 值可能变化，重新按内容算行高
                        LayoutSections();
                    }
                }
                finally
                {
                    // 【V1.72.13】非模态关闭后必须释放：Close 不释放非模态窗体，
                    // 弹窗里的 Sunny 输入框/表格成孤儿，GC 时走终结器线程 Dispose，
                    // 内部读原生 TextBox.Handle 即跨线程崩溃（与驾驶舱右栏同病根）。
                    popup.Dispose();
                }
            };

            // 【V1.60】IP 列表弹窗打开前按当前主题着色
            AgingTestSystem.Services.ThemeManager.ApplyTo(popup);
            popup.Show(this);
            popup.Activate();
        }

        /// <summary>
        /// 弹出 IO 备用通道映射编辑器，并把编辑结果写回单元格。
        /// 界面与配置同构（寄存器@通道）：寄存器（0x0000~0xFFFF）与通道（0x00~0x0F）
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
                try
                {
                    if (!string.IsNullOrEmpty(popup.ResultValue))
                    {
                        grid.Rows[rowIndex].Cells["colValue"].Value = popup.ResultValue;
                        // 值可能变化，重新按内容算行高
                        LayoutSections();
                    }
                }
                finally
                {
                    // 【V1.72.13】同上：非模态关闭后释放，防孤儿 Sunny 控件终结器跨线程崩溃。
                    popup.Dispose();
                }
            };

            // 【V1.60】IO 映射弹窗打开前按当前主题着色
            AgingTestSystem.Services.ThemeManager.ApplyTo(popup);
            popup.Show(this);
            popup.Activate();
        }

        /// <summary>
        /// 弹出自定义报警规则编辑器，并把编辑结果写回单元格。
        /// 多行文本（一行一条"名称 | 表达式 | 持续秒"），弹窗内实时校验，
        /// 确定时复检（与 ShowIpListPopup 同一套定位/越界保护/主题流程）。
        /// </summary>
        private void ShowRuleListPopup(DataGridView grid, int rowIndex, string currentValue)
        {
            var popup = new Controls.RuleListEditorPopup(currentValue);

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
                try
                {
                    if (popup.ResultValue != null)
                    {
                        grid.Rows[rowIndex].Cells["colValue"].Value = popup.ResultValue;
                        // 值可能变化，重新按内容算行高
                        LayoutSections();
                    }
                }
                finally
                {
                    // 【V1.72.13】同上：非模态关闭后释放，防孤儿控件终结器跨线程崩溃。
                    popup.Dispose();
                }
            };

            // 【V1.60】弹窗打开前按当前主题着色（与 IP/IO 弹窗一致）
            AgingTestSystem.Services.ThemeManager.ApplyTo(popup);
            popup.Show(this);
            popup.Activate();
        }

        /// <summary>
        /// 弹出报表列编辑器，并把编辑结果写回单元格。
        /// 表格（一行一列：显示名文本 + 字段下拉 + 增删/上下移），确定时逐行校验
        /// （与 ShowRuleListPopup 同一套定位/越界保护/主题/释放流程）。
        /// </summary>
        private void ShowReportColumnsPopup(DataGridView grid, int rowIndex, string currentValue)
        {
            var popup = new Controls.ReportColumnsEditorPopup(currentValue);

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
                try
                {
                    if (popup.ResultValue != null)
                    {
                        grid.Rows[rowIndex].Cells["colValue"].Value = popup.ResultValue;
                        // 值可能变化，重新按内容算行高
                        LayoutSections();
                    }
                }
                finally
                {
                    // 【V1.72.13】同上：非模态关闭后释放，防孤儿控件终结器跨线程崩溃。
                    popup.Dispose();
                }
            };

            // 【V1.60】弹窗打开前按当前主题着色（与 IP/IO/规则弹窗一致）
            AgingTestSystem.Services.ThemeManager.ApplyTo(popup);
            popup.Show(this);
            popup.Activate();
        }

        /// <summary>
        /// 弹出显示模式字典编辑器，并把编辑结果写回单元格。
        /// 列表（一行一个选项，可改/增/删），确定时逐行校验
        /// （与 ShowReportColumnsPopup 同一套定位/越界保护/主题/释放流程）。
        /// </summary>
        private void ShowDisplayModesPopup(DataGridView grid, int rowIndex, string currentValue)
        {
            var popup = new Controls.DisplayModesEditorPopup(currentValue);

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
                try
                {
                    if (popup.ResultValue != null)
                    {
                        grid.Rows[rowIndex].Cells["colValue"].Value = popup.ResultValue;
                        // 值可能变化，重新按内容算行高
                        LayoutSections();
                    }
                }
                finally
                {
                    // 【V1.72.13】同上：非模态关闭后释放，防孤儿控件终结器跨线程崩溃。
                    popup.Dispose();
                }
            };

            // 【V1.60】弹窗打开前按当前主题着色（与 IP/IO/规则/报表列弹窗一致）
            AgingTestSystem.Services.ThemeManager.ApplyTo(popup);
            popup.Show(this);
            popup.Activate();
        }

        /// <summary>
        /// 获取配置项的当前值
        /// 【V1.67】取值优先级：项目策略文件 Policy.json（策略 key）→ AppSettings →
        /// 内存 DeviceConfig 属性兜底。策略 key 优先读项目文件，保证界面显示的是
        /// 当前项目真正生效的值（而不是 App.config 里的机器缺省）。
        /// </summary>
        private string GetEffectiveValue(string key)
        {
            // 【V1.58】主页区域调整：HomeLayout 不是 App.config 配置项，而是 HomeLayout.json
            // 里的布局参数。这里显示当前生效的尺寸摘要，方便用户在设置里一眼看到现状。
            if (key == "HomeLayout")
            {
                var layout = GetEffectiveHomeLayout();
                return $"点击编辑：右侧区域 {layout.RightPanelWidth}px | 顶部标题栏 {layout.TopBarHeight}px | 菜单栏 {layout.MenuHeight}px | 状态栏 {layout.StatusBarHeight}px";
            }

            // 【V1.67】策略 key 优先读项目文件（当前项目生效值优先于机器缺省）
            if (ProjectPolicyStore.PolicyKeys.Contains(key))
            {
                string policyRaw = ProjectPolicyStore.GetRaw(key);
                if (policyRaw != null) return policyRaw;
            }

            string raw = System.Configuration.ConfigurationManager.AppSettings[key];
            if (raw != null)
            {
                // 【V1.68】密钥显示解密：文件里是 DPAPI 密文，界面给明文编辑；
                // 非密文（手写明文/损坏）显示空，逼着重填——不明文兼容。
                if (key == "MesAuthToken" || key == "MesAuthPassword")
                {
                    return MesCrypto.Unprotect(raw) ?? "";
                }
                return raw;
            }

            var prop = _config.GetType().GetProperty(key);
            if (prop != null)
            {
                object value = prop.GetValue(_config, null);
                if (value != null) return value.ToString();
            }
            return "";
        }

        /// 根据配置项类型创建"设置值"单元格控件，防止用户乱输导致配置写坏：
        /// - 布尔项（_boolKeys）：下拉框只允许选择 true / false
        /// - 策略项（V1.67，ProjectPolicyStore.EnumOptions）：下拉框中文显示、存英文名
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

            // 【V1.67】工艺策略下拉：中文显示（随便改文案不影响已存配置）、英文名存储；
            // 存值非法（手改文件写错）时 NormalizePolicyValue 兜底回缺省（= 现状行为），
            // 界面永远显示合法选项，已存的脏值在保存时被洗掉（所见即所得）。
            Tuple<string, string>[] policyOptions;
            if (ProjectPolicyStore.EnumOptions.TryGetValue(key, out policyOptions))
            {
                var options = new ComboOption[policyOptions.Length];
                for (int i = 0; i < policyOptions.Length; i++)
                {
                    options[i] = new ComboOption(policyOptions[i].Item1, policyOptions[i].Item2);
                }
                return CreateOptionComboCell(options, NormalizePolicyValue(key, value));
            }

            if (key == "PortName" || key == "ScannerPort")
            {
                return CreatePortComboCell(value);
            }

            // 送风机候选 IP 列表 / IO 备用通道映射 / 自定义报警规则 / 报表列 / 显示字典：
            // 只读单元格 + 点击弹出编辑器
            if (key == "FanIpCandidates" || key == "IoBackupChannelMappings"
                || key == "CustomAlarmRules" || key == "ReportColumns"
                || key == "DisplayModes")
            {
                var cell = new DataGridViewPopupEditCell();
                cell.Value = value;
                return cell;
            }

            // 【V1.58】主页区域调整：只读单元格 + 点击弹出可视化编辑器（见 Grid_CellClick）
            if (key == "HomeLayout")
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
            {                // 界面显示中文，实际存值映射为标准枚举名（None/Odd/Even/Mark/Space），
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

            // 【V1.68】MES 鉴权下拉：中文显示、存英文名（None/Bearer/Basic），
            // 脏值归一回 None（与校验位 NormalizeParity 同思路）
            if (key == "MesAuthType")
            {
                return CreateOptionComboCell(
                    new[]
                    {
                        new ComboOption("无鉴权(None)", "None"),
                        new ComboOption("Bearer Token", "Bearer"),
                        new ComboOption("用户名密码(Basic)", "Basic"),
                    },
                    NormalizeMesAuthType(value));
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

        /// <summary>
        /// 把策略配置值规整为合法存储值（【V1.67 新增】与 NormalizeParity 同思路）：
        /// 大小写不敏感匹配合法名单；非法/空一律回第一个选项（= 现状行为）。
        /// </summary>
        private static string NormalizePolicyValue(string key, string value)
        {
            Tuple<string, string>[] options;
            if (!ProjectPolicyStore.EnumOptions.TryGetValue(key, out options)
                || options == null || options.Length == 0)
            {
                return value ?? "";
            }
            string v = (value ?? "").Trim();
            foreach (var opt in options)
            {
                if (string.Equals(opt.Item2, v, StringComparison.OrdinalIgnoreCase))
                {
                    return opt.Item2;
                }
            }
            return options[0].Item2;
        }

        /// <summary>
        /// Tooltip 换行（【V1.67 新增】）：WinForms 的 ToolTip 不会自动换行，
        /// 超长说明会横向溢出屏幕。这里按字符每 40 字插入换行（中文 1 字 1 位；
        /// 换行点尽量落在标点后，不断英文单词——现场小屏也看得全）。
        /// </summary>
        /// <param name="text">原始说明文本</param>
        /// <returns>插入换行后的文本；空输入返回 ""</returns>
        internal static string WrapTooltip(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            const int width = 40;
            if (text.Length <= width) return text;
            var sb = new System.Text.StringBuilder();
            int lineStart = 0;
            while (lineStart < text.Length)
            {
                int remain = text.Length - lineStart;
                if (remain <= width)
                {
                    sb.Append(text, lineStart, remain);
                    break;
                }
                // 在 40 字窗口内找最后一个可断点（标点/空格），找不到就硬断
                int cut = lineStart + width;
                int breakAt = -1;
                for (int i = cut; i > lineStart; i--)
                {
                    char c = text[i - 1];
                    if (c == ' ' || c == '，' || c == '。' || c == '；' || c == '：'
                        || c == '、' || c == ',' || c == '.' || c == ';' || c == ':'
                        || c == '）' || c == ')')
                    {
                        breakAt = i;
                        break;
                    }
                }
                if (breakAt <= lineStart) breakAt = cut;
                sb.Append(text, lineStart, breakAt - lineStart);
                sb.Append("\r\n");
                lineStart = breakAt;
                // 跳过行首空格（上一行断在空格时），中文无此问题但英文有
                while (lineStart < text.Length && text[lineStart] == ' ') lineStart++;
            }
            return sb.ToString();
        }

        /// <summary>
        /// 把 MES 鉴权配置值规整为合法存储值（【V1.68 新增】None/Bearer/Basic，
        /// 大小写兼容；非法一律回 None——无鉴权是最安全的缺省）。
        /// </summary>
        private static string NormalizeMesAuthType(string value)
        {
            string v = (value ?? "").Trim();
            if (string.Equals(v, "Bearer", StringComparison.OrdinalIgnoreCase)) return "Bearer";
            if (string.Equals(v, "Basic", StringComparison.OrdinalIgnoreCase)) return "Basic";
            return "None";
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
        /// 校验 MES 映射类文本（【V1.68 新增】供 ValidateValue 调用；本体是
        /// MesMapping 纯函数，回归可单测，这里只做分发）。
        /// </summary>
        /// <returns>错误描述列表；空=合法</returns>
        private static List<string> ValidateMesMappingText(string key, string value)
        {
            List<string> errors;
            if (key == "MesTriggers")
            {
                List<string> triggers;
                MesMapping.ParseTriggers(value, out triggers, out errors);
            }
            else if (key == "MesFieldMap")
            {
                Dictionary<string, string> map;
                MesMapping.ParseFieldMap(value, out map, out errors);
            }
            else if (key == "MesStaticFields")
            {
                Dictionary<string, string> fields;
                MesMapping.ParseStaticFields(value, out fields, out errors);
            }
            else if (key == "MesCustomHeaders")
            {
                Dictionary<string, string> headers;
                MesMapping.ParseCustomHeaders(value, out headers, out errors);
            }
            else
            {
                Dictionary<string, string> epMap;
                MesMapping.ParseEndpointMap(value, out epMap, out errors);
            }
            return errors;
        }

        /// <summary>
        /// 按配置项类型校验用户输入的值是否合法
        /// 【V1.70】private → internal：工艺策略窗保存前复用同一套校验（落盘语义只有一份）。
        /// </summary>
        /// <param name="key">配置项名称</param>
        /// <param name="value">用户输入值（已 Trim）</param>
        /// <param name="error">校验失败时的中文提示</param>
        /// <returns>true=合法，false=不合法</returns>
        internal static bool ValidateValue(string key, string value, out string error)
        {
            error = null;

            // 自由文本/列表类配置项（IP、端口映射表等），不强制校验
            switch (key)
            {
                case "FanIpCandidates":
                case "IoBackupChannelMappings":
                    return true;
            }

            // 策略枚举（【V1.67】）：必须命中合法名单（大小写不敏感）。
            // 界面下拉选出来的天然合法；这道校验防的是手改 Policy.json/App.config 写错，
            // 脏值在这里被拦截并报出合法选项，不会带着脏值保存。
            Tuple<string, string>[] enumOptions;
            if (ProjectPolicyStore.EnumOptions.TryGetValue(key, out enumOptions))
            {
                string v = (value ?? "").Trim();
                foreach (var opt in enumOptions)
                {
                    if (string.Equals(opt.Item2, v, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                var names = new List<string>();
                foreach (var opt in enumOptions) names.Add(opt.Item2);
                error = "应为下拉选项之一（" + string.Join(" / ", names.ToArray()) + "）";
                return false;
            }

            // MES 映射类（【V1.68】）：鉴权白名单 + 触发器/映射/静态走 MesMapping 纯函数校验。
            // 脏输入在这里拦截并报出具体哪一组错了，不带病保存（上报线程只跳过不报错，
            // 所以保存时拦是最后一道看得见的门）。
            if (key == "MesAuthType")
            {
                string v = (value ?? "").Trim();
                if (string.Equals(v, "None", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(v, "Bearer", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(v, "Basic", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                error = "应为 None / Bearer / Basic 之一";
                return false;
            }
            if (key == "MesTriggers" || key == "MesFieldMap" || key == "MesStaticFields"
                || key == "MesCustomHeaders" || key == "MesEndpointMap")
            {
                List<string> errors = ValidateMesMappingText(key, value);
                if (errors.Count == 0) return true;
                error = string.Join("；", errors.ToArray());
                return false;
            }

            // 报表列（【V1.74】）：脏组保存时拦并报出哪一组错了；导出时跳过脏列，
            // 与 MES 映射"保存拦、上报跳"双保险同规矩。
            if (key == "ReportColumns")
            {
                List<ReportColumns.Column> cols;
                List<string> colErrs;
                ReportColumns.Parse(value, out cols, out colErrs);
                if (colErrs.Count == 0) return true;
                error = string.Join("；", colErrs.ToArray());
                return false;
            }

            // 显示模式字典（【V1.74】）：空=缺省预设合法；配了必须解析出 ≥1 个选项，
            // 脏组（超长/重复/全空）保存时拦并报出原因。
            if (key == "DisplayModes")
            {
                if (string.IsNullOrWhiteSpace(value)) return true;
                List<string> dmOpts;
                List<string> dmErrs;
                DisplayModeOptions.Parse(value, out dmOpts, out dmErrs);
                if (dmOpts.Count > 0 && dmErrs.Count == 0) return true;
                if (dmOpts.Count == 0 && dmErrs.Count == 0)
                {
                    error = "未解析出任何选项（留空=缺省预设；要自定义请填如 白场,红场）";
                    return false;
                }
                error = string.Join("；", dmErrs.ToArray());
                return false;
            }

            // 规则流程（【V1.69】）：完成表达式单行语法校验（空=禁用合法）；
            // 规则表逐行校验（错行带行号，报全不只报首条——保存拦截要一次看全）。
            if (key == "CompleteExpression")
            {
                if (string.IsNullOrWhiteSpace(value)) return true;
                RuleExpr.RuleExpression expr;
                string exprErr;
                if (RuleExpr.TryParse(value.Trim(), out expr, out exprErr)) return true;
                error = "表达式错误：" + exprErr;
                return false;
            }
            if (key == "CustomAlarmRules")
            {
                List<RuleEngine.RuleDef> defs;
                List<string> ruleErrs;
                RuleEngine.ParseRuleList(value, out defs, out ruleErrs);
                if (ruleErrs.Count == 0) return true;
                error = string.Join("；", ruleErrs.ToArray());
                return false;
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
                case "MesTimeoutMs":
                case "MesRetryCount":
                case "MesRetryIntervalMs":
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
                case "FanTempShutdownEnabled":
                case "ScannerEnabled":
                case "ScannerDebugLog":
                case "MesEnabled":
                case "MesMockEnabled":
                case "SkipVacuum":
                case "VentValveEnabled":
                case "UsePowerMeter":
                case "DisplayModeEnabled":
                    if (!bool.TryParse(value, out _)) { error = "应为 true 或 false"; return false; }
                    return true;

                // 破空阀点位（非负整数，0=未配置）
                case "VentValveDoPoint":
                    int ventPoint;
                    if (!int.TryParse(value, out ventPoint) || ventPoint < 0)
                    {
                        error = "应为 ≥0 的整数（0=未配置破空阀）";
                        return false;
                    }
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
        /// 策略组合校验（【V1.67 新增】供保存按钮调用；校验逻辑本体在
        /// AgingSequencer.ValidatePolicyCombination（纯函数，回归可单测），
        /// 这里只负责拼出"内存现值 + 本次修改叠加后"的生效值）。
        /// 【V1.70】改为静态（入参 config），供工艺策略窗复用同一条保存路。
        /// </summary>
        /// <param name="config">内存中的设备配置（读现值用）</param>
        /// <param name="changes">本次收集到的全部修改（key → 界面值）</param>
        /// <returns>矛盾描述；null=组合合法</returns>
        private static string CheckPolicyCombination(DeviceConfig config, Dictionary<string, string> changes)
        {
            bool shutdown = ResolveEffectiveBool(changes, "FanTempShutdownEnabled", config.FanTempShutdownEnabled);
            float limitC = ResolveEffectiveFloat(changes, "FanTempAlarmLimitC", config.FanTempAlarmLimitC);
            CompletionAction action = ResolveEffectiveEnum(changes, "CompletionAction", config.CompletionAction);
            int ventPoint = ResolveEffectiveInt(changes, "VentValveDoPoint", config.VentValveDoPoint);
            bool ventEnabled = ResolveEffectiveBool(changes, "VentValveEnabled", config.VentValveEnabled);
            return AgingSequencer.ValidatePolicyCombination(shutdown, limitC, action, ventPoint, ventEnabled);
        }

        /// <summary>取某项的生效值：本次改了用本次的，否则用内存现值（下同三个）。</summary>
        private static bool ResolveEffectiveBool(Dictionary<string, string> changes, string key, bool current)
        {
            string v;
            if (changes.TryGetValue(key, out v) && bool.TryParse((v ?? "").Trim(), out bool b)) return b;
            return current;
        }

        /// <summary>取某项的生效值（float 版）。</summary>
        private static float ResolveEffectiveFloat(Dictionary<string, string> changes, string key, float current)
        {
            string v;
            if (changes.TryGetValue(key, out v) && float.TryParse((v ?? "").Trim(), out float f)) return f;
            return current;
        }

        /// <summary>取某项的生效值（int 版）。</summary>
        private static int ResolveEffectiveInt(Dictionary<string, string> changes, string key, int current)
        {
            string v;
            if (changes.TryGetValue(key, out v) && int.TryParse((v ?? "").Trim(), out int i)) return i;
            return current;
        }

        /// <summary>取某项的生效值（枚举版，非法回现值——单项校验已拦过脏值，这里只求不炸）。</summary>
        private static CompletionAction ResolveEffectiveEnum(Dictionary<string, string> changes, string key, CompletionAction current)
        {
            string v;
            object parsed;
            if (changes.TryGetValue(key, out v)
                && (parsed = ProjectPolicyStore.ParseValue(typeof(CompletionAction), v)) != null)
            {
                return (CompletionAction)parsed;
            }
            return current;
        }

        /// <summary>
        /// 持久化结果（【V1.70 新增】PersistChanges 的输出，调用方按需提示/分发）。
        /// </summary>
        public class PersistResult
        {
            /// <summary>本次成功写入的 key（主窗体按需触发重连/热生效）</summary>
            public HashSet<string> SavedKeys = new HashSet<string>();
            /// <summary>其中需重启生效的结构型 key（调用方提示用户）</summary>
            public List<string> StructuralChanged = new List<string>();
            /// <summary>MES 密钥加密失败 fallback 明文（调用方必须明示用户）</summary>
            public bool SecretFallbackPlain;
        }

        /// <summary>
        /// 保存配置统一入口（【V1.70 新增】从 btnSave_Click 抽出，工艺策略窗共用同一条路）：
        /// 组合校验 → MES 就绪校验 → 分流写文件（策略→Policy.json，其余→exe.config，
        /// 密钥加密落盘）→ 内存热回写。调用方只负责"收集 changes + 弹提示"，
        /// 落盘语义只有一份，改了这里两边一起变。
        /// </summary>
        /// <param name="config">内存中的设备配置（读现值 + 热回写目标）</param>
        /// <param name="changes">已过 ValidateValue 单项校验的修改（key → 值）</param>
        /// <param name="result">成功时的明细（失败为 null）</param>
        /// <param name="error">失败原因（成功为 null，调用方直接弹框展示）</param>
        /// <returns>true=保存成功，false=被拦截（error 有内容）</returns>
        public static bool PersistChanges(DeviceConfig config,
            Dictionary<string, string> changes, out PersistResult result, out string error)
        {
            result = null;
            error = null;
            if (config == null || changes == null)
            {
                error = "内部错误：配置为空";
                return false;
            }

            // 1) 策略组合校验（联停开但上限0 / 泄压选但点位0，直接拦截并指明先填哪个）
            string comboError = CheckPolicyCombination(config, changes);
            if (comboError != null)
            {
                error = "策略组合矛盾，保存已拦截：\r\n\r\n" + comboError;
                return false;
            }

            // 2) MES 就绪校验（开了开关没配地址 = 配了等于没配，还后台空转）
            bool mesOn = ResolveEffectiveBool(changes, "MesEnabled", config.MesEnabled);
            string mesUrl;
            if (!changes.TryGetValue("MesEndpoint", out mesUrl) || mesUrl == null)
            {
                mesUrl = config.MesEndpoint;
            }
            if (mesOn && string.IsNullOrWhiteSpace(mesUrl))
            {
                error = "MES 上报已开，但接收地址（MesEndpoint）为空：\n\n" +
                    "请先填 MesEndpoint（MES 接收 URL），或把 MesEnabled 改回 false。";
                return false;
            }

            // 3) 分流：策略 key → 项目 Policy.json；其余 → exe.config（跟机器走）
            var policyChanges = new Dictionary<string, string>();
            var machineChanges = new Dictionary<string, string>();
            foreach (var kv in changes)
            {
                if (ProjectPolicyStore.PolicyKeys.Contains(kv.Key)) policyChanges[kv.Key] = kv.Value;
                else machineChanges[kv.Key] = kv.Value;
            }

            bool secretFallbackPlain = false;
            try
            {
                var cfgFile = System.Configuration.ConfigurationManager.OpenExeConfiguration(
                    System.Configuration.ConfigurationUserLevel.None);

                foreach (var kv in machineChanges)
                {
                    string toFile = kv.Value;
                    // 密钥加密落盘（内存保持明文，上报线程用内存值；加密失败 fallback 明文+上报调用方明示）
                    if ((kv.Key == "MesAuthToken" || kv.Key == "MesAuthPassword")
                        && !string.IsNullOrEmpty(toFile) && !MesCrypto.IsProtected(toFile))
                    {
                        string enc = MesCrypto.Protect(toFile);
                        if (enc != null) toFile = enc;
                        else secretFallbackPlain = true;
                    }
                    var setting = cfgFile.AppSettings.Settings[kv.Key];
                    if (setting == null)
                    {
                        cfgFile.AppSettings.Settings.Add(kv.Key, toFile);
                    }
                    else
                    {
                        setting.Value = toFile;
                    }
                }

                cfgFile.Save(System.Configuration.ConfigurationSaveMode.Modified);
                System.Configuration.ConfigurationManager.RefreshSection("appSettings");
            }
            catch (Exception ex)
            {
                error = "保存配置失败：" + ex.Message;
                return false;
            }

            // 4) 策略写项目文件（失败同样拦截，不走到热回写，避免内存与文件不一致）
            if (policyChanges.Count > 0)
            {
                try
                {
                    ProjectPolicyStore.Save(policyChanges);
                }
                catch (Exception ex)
                {
                    error = "保存项目策略失败：" + ex.Message;
                    return false;
                }
            }

            // 5) 热回写内存（结构型不回写，需重启）
            ApplyChangesToConfig(config, changes);

            result = new PersistResult
            {
                SavedKeys = new HashSet<string>(changes.Keys),
                SecretFallbackPlain = secretFallbackPlain
            };
            foreach (string k in changes.Keys)
            {
                if (StructuralKeys.Contains(k)) result.StructuralChanged.Add(k);
            }
            return true;
        }

        /// <summary>
        /// "保存设置"按钮点击事件
        ///
        /// 【流程】
        /// 1. 遍历全部分类表格，收集每行的 key / 值
        /// 2. 按类型校验每个值，不合法项整批拦截并列出（避免写坏配置文件）
        /// 2.5 【V1.67】策略组合校验（联停开但上限0 / 泄压选但点位0 直接拦截并指明先填哪个）
        /// 3. 分流写回：策略 key → 项目 Policy.json；其余 → exe.config 的 appSettings
        /// 4. 刷新 appSettings 缓存，提示重启生效
        /// 【V1.70】2.5 之后全收拢进 PersistChanges（工艺策略窗共用），这里只剩收集+提示。
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

                // 【V1.58】HomeLayout 是 HomeLayout.json 里的布局配置（非 App.config 项），
                // 保存设置时跳过它——主页布局已由可视化编辑器直接写入 HomeLayout.json，
                // 不该把"摘要文字"误写进 App.config。
                if (key == "HomeLayout") continue;

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

            // 【V1.70】落盘走统一入口（组合/MES校验 + 分流写文件 + 热回写全在里面）
            PersistResult presult;
            string perror;
            if (!PersistChanges(_config, changes, out presult, out perror))
            {
                MessageBox.Show(perror, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 本次保存的配置项 key 交给主窗体，由主窗体按需触发重连（串口/耦合器/送风机/扫码枪）
            SavedKeys = presult.SavedKeys;

            // 弹窗提示：是否包含需重启生效的结构型配置
            string saveMessage;
            if (presult.StructuralChanged.Count > 0)
            {
                saveMessage = "设置已保存。以下配置项需重启程序后生效：\r\n\r\n" +
                    string.Join("、", presult.StructuralChanged.Select(k =>
                        _descriptions.TryGetValue(k, out string d) ? d : k)) +
                    "\r\n\r\n其余配置项已即时生效。";
            }
            else
            {
                saveMessage = "设置已保存并即时生效。";
            }
            // 【V1.68】密钥加密失败时明示（fallback 明文保存了，不能让用户以为已加密）
            if (presult.SecretFallbackPlain)
            {
                saveMessage += "\r\n\r\n注：MES 密钥加密失败，已按明文保存（上报不受影响），请检查后重新保存。";
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
        /// 【V1.70】改为静态（入参 config），供工艺策略窗复用。
        /// </summary>
        private static void ApplyChangesToConfig(DeviceConfig config, Dictionary<string, string> changes)
        {
            foreach (var kv in changes)
            {
                if (StructuralKeys.Contains(kv.Key)) continue;
                var prop = typeof(DeviceConfig).GetProperty(kv.Key);
                if (prop == null || !prop.CanWrite) continue;
                try
                {
                    object converted = ConvertConfigValue(prop.PropertyType, kv.Value);
                    if (converted != null) prop.SetValue(config, converted);
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
            // 【V1.67】策略枚举：走 ProjectPolicyStore.ParseValue（与启动叠加同口径）
            if (propType.IsEnum) { return ProjectPolicyStore.ParseValue(propType, value); }
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
