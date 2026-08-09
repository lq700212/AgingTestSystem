using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AgingTestSystem.Models;

namespace AgingTestSystem.Views
{
    /// <summary>
    /// 工位网格（自绘大画布）——【V1.51 布局外部化 + 文字糊修复】
    ///
    /// 【方案说明】
    /// 原实现为"TableLayoutPanel + 72 个面板控件"，滚动时 WinForms 要逐帧移动 72 个
    /// 子控件窗口（且每个面板内部还有多个子控件），多窗口移动彼此不同步，
    /// 拖动滚动条必然撕裂/卡顿。V1.50 改为 RecyclerView 同源的"单窗口大画布"：
    /// - 整个网格（8列×9行面板 + 行全选按钮列）合并为 **1 个自绘 UserControl**，
    ///   尺寸 = 内容总尺寸，放入外层 Panel.AutoScroll 容器中；
    /// - 滚动时系统只需移动 1 个窗口（内存 BitBlt 移动位图），不再逐帧移动 72 个窗口 → 无撕裂；
    /// - 72 个面板全部由本控件 OnPaint 按坐标绘制，且只重绘可见区域（配合滚动性能）；
    /// - 交互（长按选中 / 设置按钮 / 选中框 / 行全选 / 悬停提示）全部用坐标命中实现。
    ///
    /// 【V1.51 修复：文字"糊成一坨"】
    /// 原实现 OnPaint 用 g.TranslateTransform 平移坐标系后再用 TextRenderer.DrawText 绘制。
    /// TextRenderer 走 GDI 绘制路径，与 Graphics 的坐标变换叠加时**位置/大小会错乱**，
    /// 导致文字溢出到相邻元素上互相叠加，看起来糊成一坨。
    /// 本版本所有元素（背景/状态块/值框/标签/按钮/文字）一律改为**绝对坐标**绘制，
    /// 彻底去掉 TranslateTransform 与 GDI 文字绘制混用的问题。
    ///
    /// 【V1.51 布局外部化】
    /// 面板坐标/颜色/字号/提示文字全部来自 <see cref="PanelLayoutConfig"/>（默认内置，
    /// 可通过程序目录下 PanelLayout.json 覆盖）。现场微调界面只改配置文件、无需重新编译。
    /// 【V1.51】值框左边界与左侧标签文字间距加大（值框 X 由 57→62，宽相应缩短），
    /// 解决"数据框紧贴左侧标签"问题；间距可在 PanelLayout.json 中微调。
    ///
    /// 【V1.55 高DPI适配】
    /// 本控件是 AutoScaleMode.None 的自绘控件，若完全脱离 DPI，150% 缩放下会出现：
    /// 画布尺寸（逻辑像素）不放大、而 pt 字体的文字自动变大 → 文字溢出格子、重叠，
    /// 且与周围被 AutoScaleMode.Font 放大的标准控件比例失调（"界面显示不正常"）。
    /// 适配方案：布局配置仍是"96DPI 逻辑像素"，但 <see cref="UpdateDpiScale"/> 在
    /// 句柄创建后计算缩放因子 _dpiScale = DeviceDpi / 96（150% 缩放下 = 1.5），
    /// 所有绘制/命中坐标、画布尺寸一律经 <see cref="Scaled(int)"/> 放大，字体保持
    /// pt 单位自动放大 → 文字与格子同步放大、比例与 96DPI 完全一致。
    /// 注意：不能用 Graphics.ScaleTransform，因为 TextRenderer 走 GDI 不认坐标系变换
    /// （见上方 V1.51 踩坑），必须手动把每个坐标乘缩放因子。
    ///
    /// 【界面布局】
    /// 一、整体结构（外层 Panel.AutoScroll 滚动容器 + 本控件 = 画布）
    /// ┌───────────────────────────────────────────┬──────────┐
    /// │             画布（本控件 OnPaint）         │ 行全选列 │
    /// │ ┌──────┬──────┬──────┬──────┬──────┬───  │ ├──────────┤
    /// │ │ NO.1 │ NO.2 │ NO.3 │ NO.4 │ NO.5 │ ... │ │ [全选]   │ ← 第1行
    /// │ ├──────┼──────┼──────┼──────┼──────┼───  │ │ [全选]   │ ← 第2行
    /// │ │ NO.9 │ NO.10│ NO.11│ NO.12│ NO.13│ ... │ │ [全选]   │ ← 第3行
    /// │ │ ...  │ ...  │ ...  │ ...  │ ...  │ ... │ │ [全选]   │ ← 第4~9行
    /// │ └──────┴──────┴──────┴──────┴──────┴───  │ ├──────────┤
    /// │ 8列（列宽245，每格内容240+左右边距各2）      │ 行内全部   │
    /// │ × 9行（行高225，每格内容205+上下边距各2）   │ 选中→[取消]│
    /// └───────────────────────────────────────────┴──────────┘
    /// 网格占满全部 72 台设备；下方留白由 AutoScroll 容器滚动。
    ///
    /// 二、单个面板内容（240×205，坐标均为"相对面板左上角"）
    /// ┌──────────────────────────────────────────────┐
    /// │ NO.1（标题，左上角）            ┌────────────┐│
    /// │ ┌──────────┐  ┌──────────┐     │ 选中指示框  ││ ← 右上角 23×23
    /// │ │ 上电/下电 │  │ 空闲/选中 │     │ (绿底白✓)  ││    有选中才显示
    /// │ └──────────┘  │ 繁忙/故障 │     └────────────┘│
    /// │               └──────────┘                   │
    /// │ 真空压力 ┌─────────────┐   ┌───────────┐     │
    /// │          │  78 kPa     │   │ 真空开/关  │     │
    /// │          └─────────────┘   └───────────┘     │
    /// │ SN:    ┌───────────────────────┐             │
    /// │ 配方:  ┌───────────────────────┐             │
    /// │        └───────────────────────┘             │
    /// │ 延时开启 ┌────────────┐   ┌─────────────────┐ │
    /// │          │ 00:00:00   │   │      设置       │ │ ← 绿底白字
    /// │ 延时到达 ┌────────────┘   └─────────────────┘ │
    /// │          │ 00:00:00  │                        │
    /// │          └───────────┘                        │
    /// └──────────────────────────────────────────────┘
    /// 标注说明：
    /// - 行1：上电/下电块(62,29,52,23) + 工作状态块(138,29,52,23) + 选中框(212,4,23,23)
    /// - 行2：真空压力值框(57,67,78,21) + 真空开/关块(138,67,48,21)
    /// - 行3：SN 值框(57,93,140,21)、配方值框(57,118,140,21)
    /// - 行4：延时开启值框(57,143,80,21) + 设置按钮(145,145,60,50)、延时到达值框(57,168,80,21)
    /// - 标签列：真空压力(3,70)/SN:(3,96)/配方:(3,121)/延时开启(3,146)/延时到达(3,171)
    /// - 值框文字左内边距：ValueTextLeftPadding=6px（V1.52，文字不贴值框左边框，值框坐标不变）
    /// - 状态块配色见下方"状态块配色"；颜色值均可由 PanelLayout.json 覆盖
    ///
    /// 【状态块配色（V1.28 约定）】
    /// - 上电/下电：绿=LimeGreen=上电，浅灰=LightGray=下电
    /// - 工作状态：空闲=绿 / 选中(已上电待测试)=橙 / 繁忙(测试中)=黄 / 故障=红
    /// - 真空开/关：真空开=绿底，真空关=浅灰底
    ///
    /// 【数据流】
    /// 主窗体收到设备批量更新后调用 <see cref="UpdateAll"/> / <see cref="UpdateSingle"/>，
    /// 仅更新内存字段 + Invalidate，1Hz 全量刷新开销极小，完全不影响实时监控。
    /// </summary>
    public partial class WorkstationGridView : System.Windows.Forms.UserControl
    {
        // ===== 布局配置 =====
        /// <summary>面板布局配置（默认内置，可被 PanelLayout.json 覆盖）</summary>
        private readonly PanelLayoutConfig _layout;

        // ===== 字体 =====
        /// <summary>面板正文文字字体（显式创建，不继承主窗体缩放字体，保证与小矩形匹配）</summary>
        private readonly Font _panelFont;
        /// <summary>设备编号标题字体（微软雅黑 9 Bold）</summary>
        private readonly Font _titleFont;

        // ===== 配置解析出的颜色（初始化时解析一次，避免绘制时反复解析字符串） =====
        private readonly Color _normalColor;   // 面板背景-空闲（白）
        private readonly Color _testingColor;  // 面板背景-测试中（浅黄）
        private readonly Color _faultColor;    // 面板背景-故障（浅粉）
        private readonly Color _colorPowerOn;  // 上电块背景（绿）
        private readonly Color _colorPowerOff; // 下电块背景（浅灰）
        private readonly Color _colorVacuumOn; // 真空开块背景（绿）
        private readonly Color _colorVacuumOff;// 真空关块背景（浅灰）
        private readonly Color _colorWorkFault;    // 工作状态-故障（红）
        private readonly Color _colorWorkBusy;     // 工作状态-繁忙（黄）
        private readonly Color _colorWorkSelected; // 工作状态-选中/已上电待测试（橙）
        private readonly Color _colorWorkIdle;     // 工作状态-空闲（绿）
        private readonly Color _colorSetButton;    // 设置按钮背景（绿）
        private readonly Color _colorRowSelect;    // 行全选按钮背景（浅灰）
        private readonly Color _colorValueBox;     // 值框背景（白）
        private readonly Color _colorText;         // 正文文字（黑）
        private readonly Color _colorBorder;       // 边框（黑）

        /// <summary>面板列数</summary>
        private int _columns;
        /// <summary>面板行数</summary>
        private int _rows;
        /// <summary>总设备（工位）数</summary>
        private int _totalDevices;

        /// <summary>
        /// DPI 缩放因子 = DeviceDpi / 96（【V1.55 高DPI适配】）。
        /// 布局配置里的坐标/尺寸都是"96DPI 逻辑像素"，在 150% 缩放的屏幕上
        /// 必须整体放大 DeviceDpi/96 倍，否则格子不变、而 pt 字体的字会自动变大，
        /// 导致文字溢出格子、与周围被 AutoScaleMode.Font 放大的控件比例失调。
        /// 由于 TextRenderer 走 GDI 不能配合 Graphics 坐标变换（见头部 V1.51 踩坑），
        /// 这里采用"手动把所有逻辑像素乘 _dpiScale"的方式，字体保持 pt 单位自动放大，比例一致。
        /// </summary>
        private float _dpiScale = 1f;

        /// <summary>所有工位的显示状态（key = 设备编号，从1开始）</summary>
        private readonly Dictionary<int, GridItem> _items = new Dictionary<int, GridItem>();

        /// <summary>状态块悬停提示</summary>
        private readonly ToolTip _toolTip;
        /// <summary>长按选中计时器</summary>
        private readonly System.Windows.Forms.Timer _longPressTimer;
        /// <summary>本次按下是否已触发长按选中</summary>
        private bool _longPressFired;
        /// <summary>按下时的鼠标屏幕坐标（判断长按期间是否移动）</summary>
        private Point _pressStartPoint;
        /// <summary>按下时命中的设备编号（仅面板空白区域会启动长按）</summary>
        private int _pressDeviceId;
        /// <summary>上次悬停提示文本（避免 MouseMove 频繁重复 Show）</summary>
        private string _lastTooltipText;

        /// <summary>工位"设置"按钮点击事件（参数为设备编号，主窗体按选中数量分流窗口）</summary>
        public event EventHandler<int> OnSetClicked;
        /// <summary>需要写日志的消息（如行全选动作），由主窗体订阅写入 LOG</summary>
        public event EventHandler<string> OnLog;

        private const int LongPressMilliseconds = 800;
        private const int LongPressMoveThreshold = 8;

        /// <summary>
        /// 无参数构造函数（设计器/运行时通用）
        /// </summary>
        public WorkstationGridView()
        {
            InitializeComponent();
            this.DoubleBuffered = true;

            // 加载布局配置并解析出所有颜色，只做一次，绘制时直接取用
            _layout = PanelLayoutConfig.LoadOrDefault();

            _normalColor = Parse(_layout.ColorNormalBackground, Color.White);
            _testingColor = Parse(_layout.ColorTestingBackground, Color.LightYellow);
            _faultColor = Parse(_layout.ColorFaultBackground, Color.LightPink);
            _colorPowerOn = Parse(_layout.ColorPowerOn, Color.LimeGreen);
            _colorPowerOff = Parse(_layout.ColorPowerOff, Color.LightGray);
            _colorVacuumOn = Parse(_layout.ColorVacuumOn, Color.LimeGreen);
            _colorVacuumOff = Parse(_layout.ColorVacuumOff, Color.LightGray);
            _colorWorkFault = Parse(_layout.ColorWorkFault, Color.Red);
            _colorWorkBusy = Parse(_layout.ColorWorkBusy, Color.Gold);
            _colorWorkSelected = Parse(_layout.ColorWorkSelected, Color.Orange);
            _colorWorkIdle = Parse(_layout.ColorWorkIdle, Color.LimeGreen);
            _colorSetButton = Parse(_layout.ColorSetButton, Color.LimeGreen);
            _colorRowSelect = Parse(_layout.ColorRowSelectButton, Color.LightGray);
            _colorValueBox = Parse(_layout.ColorValueBox, Color.White);
            _colorText = Parse(_layout.ColorText, Color.Black);
            _colorBorder = Parse(_layout.ColorBorder, Color.Black);

            // 显式创建字体（不依赖 this.Font / 主窗体 AutoScale，保证文字尺寸与固定矩形一致）
            _panelFont = new Font(_layout.FontFamily, _layout.FontSize, FontStyle.Regular);
            _titleFont = new Font(_layout.FontFamily, _layout.TitleFontSize,
                _layout.TitleFontBold ? FontStyle.Bold : FontStyle.Regular);

            _toolTip = new ToolTip(components);
            _longPressTimer = new System.Windows.Forms.Timer(components);
            _longPressTimer.Interval = LongPressMilliseconds;
            _longPressTimer.Tick += LongPressTimer_Tick;

            this.MouseDown += GridView_MouseDown;
            this.MouseUp += GridView_MouseUp;
            this.MouseMove += GridView_MouseMove;
            this.MouseLeave += GridView_MouseLeave;
        }

        /// <summary>
        /// 控件句柄创建后计算 DPI 缩放因子（【V1.55 高DPI适配】）。
        /// DeviceDpi 只有在句柄创建后才能取到真实值；必须在 Configure 之后、
        /// 首次绘制之前调用，否则画布尺寸仍是 96DPI 逻辑大小。
        /// </summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateDpiScale();
        }

        /// <summary>
        /// 根据当前设备 DPI 更新缩放因子，并重新计算画布尺寸。
        /// DPI 缩放因子 = DeviceDpi / 96（96 是布局配置的逻辑像素基准）。
        /// 该方法在句柄创建时调用一次；后续若发生 DPI 变更（跨屏拖动）也会触发。
        ///
        /// 【踩坑】不能用 Control.DeviceDpi 属性——在 PerMonitorV2 环境下它有时返回 96
        /// （句柄刚创建时 DPI 上下文尚未生效），而 CreateGraphics().DpiX 才是真实值。
        /// 实测同屏：DeviceDpi=96、CreateGraphics().DpiX=144，所以这里以 CreateGraphics 为准。
        /// </summary>
        private void UpdateDpiScale()
        {
            float dpi = 96f;
            try
            {
                using (var g = CreateGraphics())
                {
                    if (g != null && g.DpiX > 0) dpi = g.DpiX;
                }
            }
            catch
            {
                // 图形上下文创建失败时保持 1.0（96DPI），不影响 100% 缩放的旧环境
            }
            _dpiScale = dpi / 96f;
            if (_columns > 0)
            {
                this.Size = new Size(Scaled(_columns * _layout.PanelColumnWidth + _layout.RowSelectButtonColumnWidth),
                                     Scaled(_rows * _layout.PanelRowHeight));
                Invalidate();
            }
        }

        /// <summary>解析配置颜色字符串（"R,G,B"），失败时回退默认色</summary>
        private static Color Parse(string rgb, Color fallback)
        {
            return PanelLayoutConfig.ParseColor(rgb, fallback);
        }

        /// <summary>
        /// 按配置创建工位网格并设置画布总尺寸（外层 Panel.AutoScroll 据此出现滚动条）
        /// </summary>
        public void Configure(int columns, int rows, int totalDevices)
        {
            // 【V1.51】首次运行时若程序目录没有 PanelLayout.json，自动导出一份默认配置，
            // 方便现场直接修改配置文件微调界面（坐标/颜色/字号/文字），无需重新编译。
            if (!System.IO.File.Exists(PanelLayoutConfig.GetConfigPath()))
            {
                _layout.SaveDefault();
            }

            _columns = columns;
            _rows = rows;
            _totalDevices = totalDevices;
            _items.Clear();
            for (int i = 0; i < totalDevices; i++)
            {
                _items[i + 1] = new GridItem { DeviceId = i + 1 };
            }
            // 【V1.55 高DPI适配】画布总尺寸 = 逻辑像素尺寸 × DPI缩放因子。
            // 若不放大，150% 缩放下格子保持 96DPI 大小、文字却自动变大 → 溢出重叠。
            this.Size = new Size(Scaled(_columns * _layout.PanelColumnWidth + _layout.RowSelectButtonColumnWidth),
                                 Scaled(_rows * _layout.PanelRowHeight));
            Invalidate();
        }

        #region DPI 缩放辅助

        /// <summary>逻辑像素 → 物理像素（× _dpiScale，四舍五入）</summary>
        private int Scaled(int v)
        {
            return (int)Math.Round(v * _dpiScale);
        }

        /// <summary>逻辑像素 Point → 物理像素 Point</summary>
        private Point Scaled(Point p)
        {
            return new Point(Scaled(p.X), Scaled(p.Y));
        }

        /// <summary>逻辑像素 Rectangle → 物理像素 Rectangle（坐标与尺寸同步放大）</summary>
        private Rectangle Scaled(Rectangle r)
        {
            return new Rectangle(Scaled(r.X), Scaled(r.Y), Scaled(r.Width), Scaled(r.Height));
        }

        #endregion

        /// <summary>
        /// 批量更新所有面板数据（1Hz 采集周期全量刷新入口）
        /// </summary>
        public void UpdateAll(BarometerData[] allData)
        {
            if (allData == null || allData.Length == 0) return;
            bool changed = false;
            foreach (var data in allData)
            {
                if (data != null && _items.TryGetValue(data.DeviceId, out GridItem item))
                {
                    ApplyData(item, data);
                    changed = true;
                }
            }
            if (changed) Invalidate();
        }

        /// <summary>
        /// 更新单个面板数据（快速跟踪专用，只重绘该面板区域）
        /// </summary>
        public void UpdateSingle(BarometerData data)
        {
            if (data == null || !_items.TryGetValue(data.DeviceId, out GridItem item)) return;
            ApplyData(item, data);
            Invalidate(GetPanelBounds(data.DeviceId));
        }

        /// <summary>当前是否有任意工位被选中</summary>
        public bool IsAnySelected
        {
            get
            {
                foreach (var item in _items.Values)
                {
                    if (item.IsSelected) return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 设置指定工位的选中状态（用于"设置"按钮点击时确保该工位被选中）
        /// </summary>
        public void SetSelected(int deviceId, bool selected)
        {
            if (_items.TryGetValue(deviceId, out GridItem item) && item.IsSelected != selected)
            {
                item.IsSelected = selected;
                Invalidate(GetPanelBounds(deviceId));
            }
        }

        /// <summary>获取当前选中的设备编号数组</summary>
        public int[] GetSelectedDeviceIds()
        {
            var list = new List<int>();
            foreach (var kvp in _items)
            {
                if (kvp.Value.IsSelected) list.Add(kvp.Key);
            }
            return list.ToArray();
        }

        #region 数据应用到显示状态

        /// <summary>
        /// 把设备数据应用到工位显示状态缓存（内存字段，OnPaint 读取）
        /// </summary>
        private void ApplyData(GridItem item, BarometerData data)
        {
            item.PressureText = $"{data.VacuumPressure} kPa";
            item.SnText = data.SerialNumber ?? "";
            item.RecipeText = data.RecipeName ?? "";
            item.DelayStartText = data.DelayTime.ToString(@"hh\:mm\:ss");
            item.DelayArriveText = data.StartTime.ToString(@"hh\:mm\:ss");

            // IO 输出状态：OutputStatus[0]=真空电磁阀，OutputStatus[1]=载台上电
            bool vacuumOpen = data.OutputStatus != null && data.OutputStatus.Length >= 1 && data.OutputStatus[0];
            bool carrierPower = data.OutputStatus != null && data.OutputStatus.Length >= 2 && data.OutputStatus[1];

            // 真空开/关（V1.28：真空关由红改浅灰）
            item.VacuumText = vacuumOpen ? "真空开" : "真空关";
            item.VacuumColor = vacuumOpen ? _colorVacuumOn : _colorVacuumOff;
            item.VacuumForeColor = vacuumOpen ? Color.White : Color.Black;

            // 上电/下电（V1.28：下电由红改浅灰）
            item.PowerText = carrierPower ? "上电" : "下电";
            item.PowerColor = carrierPower ? _colorPowerOn : _colorPowerOff;
            item.PowerForeColor = carrierPower ? Color.White : Color.Black;

            // 工作状态（故障=红 / 繁忙=黄 / 已上电待测试=橙"选中" / 空闲=绿）
            switch (data.Status)
            {
                case DeviceStatus.Fault:
                    item.WorkText = "故障"; item.WorkColor = _colorWorkFault; item.WorkForeColor = Color.White; break;
                case DeviceStatus.Testing:
                    item.WorkText = "繁忙"; item.WorkColor = _colorWorkBusy; item.WorkForeColor = Color.White; break;
                default:
                    if (carrierPower)
                    {
                        item.WorkText = "选中"; item.WorkColor = _colorWorkSelected; item.WorkForeColor = Color.White;
                    }
                    else
                    {
                        item.WorkText = "空闲"; item.WorkColor = _colorWorkIdle; item.WorkForeColor = Color.White;
                    }
                    break;
            }

            // 面板背景色（空闲白/测试浅黄/故障浅粉）
            item.BackColor = data.Status == DeviceStatus.Fault ? _faultColor
                           : data.Status == DeviceStatus.Testing ? _testingColor
                           : _normalColor;
        }

        #endregion

        #region 自绘渲染

        /// <summary>
        /// 自绘整个工位网格（只重绘可见区域，配合滚动容器性能）。
        /// 全部使用绝对坐标绘制：每个面板元素的最终坐标 = 面板左上角 + 设计坐标，
        /// 不再使用 TranslateTransform（避免 TextRenderer 的 GDI 绘制与坐标变换错乱导致文字模糊）。
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            if (_columns == 0) return;

            // 【V1.55 高DPI适配】e.ClipRectangle 是物理像素坐标，而布局配置是 96DPI 逻辑像素，
            // 所以可见列/行范围计算必须先乘缩放因子，否则 150% 缩放下只重绘左上角一小块。
            int colW = Scaled(_layout.PanelColumnWidth);
            int rowH = Scaled(_layout.PanelRowHeight);

            Rectangle clip = e.ClipRectangle;
            int startCol = Math.Max(0, clip.Left / colW);
            int endCol = Math.Min(_columns - 1, (clip.Right + colW - 1) / colW);
            int startRow = Math.Max(0, clip.Top / rowH);
            int endRow = Math.Min(_rows - 1, (clip.Bottom + rowH - 1) / rowH);

            bool anySelected = IsAnySelected;

            for (int row = startRow; row <= endRow; row++)
            {
                for (int col = startCol; col <= endCol; col++)
                {
                    int deviceId = row * _columns + col + 1;
                    if (!_items.TryGetValue(deviceId, out GridItem item)) continue;

                    // 面板左上角绝对坐标（面板内容设计尺寸 + 上下左右各 2px 外边距，均按 DPI 放大）
                    int panelLeft = Scaled(col * _layout.PanelColumnWidth + 2);
                    int panelTop = Scaled(row * _layout.PanelRowHeight + 2);
                    DrawPanel(g, item, anySelected, panelLeft, panelTop);
                }
            }

            // 行全选按钮列
            if (clip.Right > Scaled(_columns * _layout.PanelColumnWidth))
            {
                for (int row = startRow; row <= endRow; row++)
                {
                    Rectangle btnRect = new Rectangle(
                        Scaled(_columns * _layout.PanelColumnWidth + 2),
                        Scaled(row * _layout.PanelRowHeight + 2),
                        Scaled(_layout.RowSelectButtonColumnWidth - 4),
                        Scaled(_layout.PanelRowHeight - 4));
                    DrawRowSelectButton(g, btnRect, row);
                }
            }
        }

        /// <summary>
        /// 绘制单个工位面板（以绝对坐标绘制，panelLeft/panelTop 为面板左上角）。
        /// 面板内部所有元素坐标 = 设计坐标偏移 + 面板左上角。
        /// </summary>
        private void DrawPanel(Graphics g, GridItem item, bool anySelected, int panelLeft, int panelTop)
        {
            // 面板背景（状态色），尺寸按 DPI 放大
            using (var bg = new SolidBrush(item.BackColor))
            {
                g.FillRectangle(bg, panelLeft, panelTop, Scaled(_layout.PanelInnerWidth), Scaled(_layout.PanelInnerHeight));
            }

            // 设备编号（左上角）
            TextRenderer.DrawText(g, $"NO.{item.DeviceId}", _titleFont,
                new Point(panelLeft + Scaled(_layout.TitlePosition.X), panelTop + Scaled(_layout.TitlePosition.Y)), _colorText);

            // 状态块
            DrawStatusBlock(g, Offset(Scaled(_layout.RcPower.ToRectangle()), panelLeft, panelTop),
                item.PowerColor, item.PowerForeColor, item.PowerText);
            DrawStatusBlock(g, Offset(Scaled(_layout.RcWorkState.ToRectangle()), panelLeft, panelTop),
                item.WorkColor, item.WorkForeColor, item.WorkText);
            DrawStatusBlock(g, Offset(Scaled(_layout.RcVacuumOpen.ToRectangle()), panelLeft, panelTop),
                item.VacuumColor, item.VacuumForeColor, item.VacuumText);

            // 值框
            DrawValueBox(g, Offset(Scaled(_layout.RcPressureValue.ToRectangle()), panelLeft, panelTop), item.PressureText);
            DrawValueBox(g, Offset(Scaled(_layout.RcSNValue.ToRectangle()), panelLeft, panelTop), item.SnText);
            DrawValueBox(g, Offset(Scaled(_layout.RcRecipeValue.ToRectangle()), panelLeft, panelTop), item.RecipeText);
            DrawValueBox(g, Offset(Scaled(_layout.RcDelayStartValue.ToRectangle()), panelLeft, panelTop), item.DelayStartText);
            DrawValueBox(g, Offset(Scaled(_layout.RcDelayArriveValue.ToRectangle()), panelLeft, panelTop), item.DelayArriveText);

            // 静态标签
            DrawLabel(g, new Point(panelLeft + Scaled(_layout.LabelPressurePosition.X), panelTop + Scaled(_layout.LabelPressurePosition.Y)), "真空压力");
            DrawLabel(g, new Point(panelLeft + Scaled(_layout.LabelSnPosition.X), panelTop + Scaled(_layout.LabelSnPosition.Y)), "SN:");
            DrawLabel(g, new Point(panelLeft + Scaled(_layout.LabelRecipePosition.X), panelTop + Scaled(_layout.LabelRecipePosition.Y)), "配方:");
            DrawLabel(g, new Point(panelLeft + Scaled(_layout.LabelDelayStartPosition.X), panelTop + Scaled(_layout.LabelDelayStartPosition.Y)), "延时开启");
            DrawLabel(g, new Point(panelLeft + Scaled(_layout.LabelDelayArrivePosition.X), panelTop + Scaled(_layout.LabelDelayArrivePosition.Y)), "延时到达");

            // 设置按钮（绿底白字）
            Rectangle rcSet = Offset(Scaled(_layout.RcSetButton.ToRectangle()), panelLeft, panelTop);
            using (var brush = new SolidBrush(_colorSetButton))
            {
                g.FillRectangle(brush, rcSet);
            }
            using (var pen = new Pen(_colorBorder))
            {
                g.DrawRectangle(pen, rcSet);
            }
            TextRenderer.DrawText(g, _layout.SetButtonText, _panelFont, rcSet, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            // 选中指示（有任一选中才显示：选中=绿底白✓，未选中=空心白框）
            if (anySelected)
            {
                Rectangle rcSelect = Offset(Scaled(_layout.RcSelectBox.ToRectangle()), panelLeft, panelTop);
                if (item.IsSelected)
                {
                    using (var brush = new SolidBrush(_colorWorkIdle))
                    {
                        g.FillRectangle(brush, rcSelect);
                    }
                    using (var pen = new Pen(_colorBorder))
                    {
                        g.DrawRectangle(pen, rcSelect);
                    }
                    TextRenderer.DrawText(g, _layout.SelectedMarkText, _panelFont, rcSelect, Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                else
                {
                    g.FillRectangle(Brushes.White, rcSelect);
                    using (var pen = new Pen(_colorBorder))
                    {
                        g.DrawRectangle(pen, rcSelect);
                    }
                }
            }
        }

        /// <summary>
        /// 绘制行全选按钮（浅灰底，该行全部选中时显示"取消"否则"全选"）
        /// </summary>
        private void DrawRowSelectButton(Graphics g, Rectangle rc, int row)
        {
            using (var brush = new SolidBrush(_colorRowSelect))
            {
                g.FillRectangle(brush, rc);
            }
            using (var pen = new Pen(_colorBorder))
            {
                g.DrawRectangle(pen, rc);
            }
            TextRenderer.DrawText(g, IsRowAllSelected(row) ? _layout.RowSelectCancelText : _layout.RowSelectAllText,
                _panelFont, rc, _colorText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        /// <summary>绘制状态色块（填充 + 边框 + 文字水平垂直居中）</summary>
        private void DrawStatusBlock(Graphics g, Rectangle rc, Color back, Color fore, string text)
        {
            using (var brush = new SolidBrush(back))
            {
                g.FillRectangle(brush, rc);
            }
            using (var pen = new Pen(_colorBorder))
            {
                g.DrawRectangle(pen, rc);
            }
            TextRenderer.DrawText(g, text, _panelFont, rc, fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        /// <summary>
        /// 绘制值框（白底 + 黑边框 + 左对齐垂直居中文字）。
        /// 【V1.52】文字不再贴值框左边框：绘制矩形左移 ValueTextLeftPadding 像素，
        /// 值框本身坐标不变（避免"移动整个框"造成的错位观感）。
        /// </summary>
        private void DrawValueBox(Graphics g, Rectangle rc, string text)
        {
            using (var brush = new SolidBrush(_colorValueBox))
            {
                g.FillRectangle(brush, rc);
            }
            using (var pen = new Pen(_colorBorder))
            {
                g.DrawRectangle(pen, rc);
            }
            // 文本绘制矩形 = 值框矩形左移内边距（宽度同步缩短，防止文字溢出到右边框）
            // 【V1.55】内边距按 DPI 放大，保证 150% 缩放下文字仍与值框左边框保持合理间距
            int pad = Scaled(_layout.ValueTextLeftPadding);
            Rectangle textRc = new Rectangle(
                rc.X + pad,
                rc.Y,
                Math.Max(1, rc.Width - pad),
                rc.Height);
            TextRenderer.DrawText(g, text, _panelFont, textRc, _colorText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }

        /// <summary>绘制静态标签文字</summary>
        private void DrawLabel(Graphics g, Point location, string text)
        {
            TextRenderer.DrawText(g, text, _panelFont, location, _colorText);
        }

        /// <summary>把面板设计坐标偏移到画布绝对坐标</summary>
        private static Rectangle Offset(Rectangle rc, int panelLeft, int panelTop)
        {
            return new Rectangle(rc.X + panelLeft, rc.Y + panelTop, rc.Width, rc.Height);
        }

        #endregion

        #region 坐标命中与交互

        /// <summary>
        /// 鼠标按下（左键）：记录起点；命中面板空白区域（非设置/选中框）时启动长按计时
        /// </summary>
        private void GridView_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            _pressStartPoint = Control.MousePosition;
            _longPressFired = false;

            if (TryHitPanel(e.Location, out int deviceId, out Point local))
            {
                // 【V1.55】local 是物理像素坐标，布局矩形需缩放后比较
                Rectangle rcSet = Scaled(_layout.RcSetButton.ToRectangle());
                Rectangle rcSelect = Scaled(_layout.RcSelectBox.ToRectangle());
                if (!rcSet.Contains(local) && !rcSelect.Contains(local))
                {
                    _pressDeviceId = deviceId;
                    _longPressTimer.Start();
                }
            }
        }

        /// <summary>
        /// 鼠标抬起（左键）：
        /// - 行全选按钮 → 整行选中/取消；
        /// - 面板内"设置"区域 → 触发 OnSetClicked；
        /// - 面板内"选中指示"区域（有选中时）→ 切换选中；
        /// - 面板空白（有选中时）→ 单击切换选中。
        /// </summary>
        private void GridView_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            _longPressTimer.Stop();
            if (_longPressFired) return;

            if (TryHitRowButton(e.Location, out int row))
            {
                ToggleRow(row);
                return;
            }

            if (TryHitPanel(e.Location, out int deviceId, out Point local))
            {
                // 【V1.55】local 是物理像素坐标，布局矩形需缩放后比较
                Rectangle rcSet = Scaled(_layout.RcSetButton.ToRectangle());
                Rectangle rcSelect = Scaled(_layout.RcSelectBox.ToRectangle());
                if (rcSet.Contains(local))
                {
                    OnSetClicked?.Invoke(this, deviceId);
                    return;
                }
                if (rcSelect.Contains(local))
                {
                    if (IsAnySelected) ToggleSelect(deviceId);
                    return;
                }
                // 单击空白区域：有选中时切换选中状态
                if (IsAnySelected) ToggleSelect(deviceId);
            }
        }

        /// <summary>
        /// 鼠标移动：长按期间移动超过阈值视为拖动取消计时；刷新状态块悬停提示
        /// </summary>
        private void GridView_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_longPressFired && _longPressTimer.Enabled)
            {
                Point current = Control.MousePosition;
                if (Math.Abs(current.X - _pressStartPoint.X) > LongPressMoveThreshold ||
                    Math.Abs(current.Y - _pressStartPoint.Y) > LongPressMoveThreshold)
                {
                    _longPressTimer.Stop();
                }
            }

            string tip = GetTooltipText(e.Location);
            if (tip != _lastTooltipText)
            {
                _lastTooltipText = tip;
                if (string.IsNullOrEmpty(tip))
                {
                    _toolTip.Hide(this);
                }
                else
                {
                    _toolTip.Show(tip, this, new Point(e.X + 12, e.Y + 12), 1500);
                }
            }
        }

        /// <summary>
        /// 鼠标离开控件：取消未触发的长按；隐藏悬停提示
        /// </summary>
        private void GridView_MouseLeave(object sender, EventArgs e)
        {
            if (!_longPressFired) _longPressTimer.Stop();
            _lastTooltipText = "";
            _toolTip.Hide(this);
        }

        /// <summary>
        /// 长按计时到点：已有选中 → 取消全部选中；否则选中当前工位
        /// </summary>
        private void LongPressTimer_Tick(object sender, EventArgs e)
        {
            _longPressTimer.Stop();
            _longPressFired = true;

            if (IsAnySelected)
            {
                ClearAllSelection();
            }
            else
            {
                SetSelected(_pressDeviceId, true);
            }
        }

        /// <summary>切换指定工位的选中状态并重绘</summary>
        private void ToggleSelect(int deviceId)
        {
            if (_items.TryGetValue(deviceId, out GridItem item))
            {
                item.IsSelected = !item.IsSelected;
                Invalidate(GetPanelBounds(deviceId));
            }
        }

        /// <summary>取消全部选中并重绘</summary>
        private void ClearAllSelection()
        {
            bool any = false;
            foreach (var item in _items.Values)
            {
                if (item.IsSelected)
                {
                    item.IsSelected = false;
                    any = true;
                }
            }
            if (any) Invalidate();
        }

        /// <summary>切换整行选中状态（全选 ↔ 取消全选）</summary>
        private void ToggleRow(int row)
        {
            int startDeviceId = row * _columns + 1;
            int endDeviceId = Math.Min(startDeviceId + _columns - 1, _totalDevices);
            bool allSelected = IsRowAllSelected(row);

            bool newState = !allSelected;
            for (int d = startDeviceId; d <= endDeviceId; d++)
            {
                if (_items.TryGetValue(d, out GridItem item))
                {
                    item.IsSelected = newState;
                }
            }
            Invalidate();

            OnLog?.Invoke(this, $"第 {row + 1} 行 {(newState ? "全选" : "取消全选")}（设备 {startDeviceId}-{endDeviceId}）");
        }

        /// <summary>该行是否全部工位都被选中</summary>
        private bool IsRowAllSelected(int row)
        {
            int startDeviceId = row * _columns + 1;
            int endDeviceId = Math.Min(startDeviceId + _columns - 1, _totalDevices);
            for (int d = startDeviceId; d <= endDeviceId; d++)
            {
                if (_items.TryGetValue(d, out GridItem item) && !item.IsSelected)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 坐标命中面板：返回设备编号与面板内局部坐标
        /// 【V1.55】p 是物理像素坐标，需与缩放后的列宽/行高比对
        /// </summary>
        private bool TryHitPanel(Point p, out int deviceId, out Point local)
        {
            deviceId = 0;
            local = Point.Empty;
            if (_columns == 0) return false;

            int colW = Scaled(_layout.PanelColumnWidth);
            int rowH = Scaled(_layout.PanelRowHeight);
            if (p.X < 0 || p.Y < 0 || p.X >= Scaled(_columns * _layout.PanelColumnWidth) || p.Y >= Scaled(_rows * _layout.PanelRowHeight)) return false;

            int col = p.X / colW;
            int row = p.Y / rowH;
            if (col >= _columns || row >= _rows) return false;

            deviceId = row * _columns + col + 1;
            if (deviceId > _totalDevices) return false;

            // 面板内局部坐标 = 鼠标物理坐标 - 面板左上角物理坐标（含 2px 外边距，已缩放）
            local = new Point(p.X - Scaled(col * _layout.PanelColumnWidth + 2), p.Y - Scaled(row * _layout.PanelRowHeight + 2));
            return true;
        }

        /// <summary>坐标是否命中行全选按钮列，返回行号（物理像素比对）</summary>
        private bool TryHitRowButton(Point p, out int row)
        {
            row = -1;
            if (p.X < Scaled(_columns * _layout.PanelColumnWidth) || p.Y < 0 || p.Y >= Scaled(_rows * _layout.PanelRowHeight)) return false;
            row = p.Y / Scaled(_layout.PanelRowHeight);
            return row >= 0 && row < _rows;
        }

        /// <summary>根据坐标返回命中的状态块悬停提示文本（未命中返回 null）</summary>
        private string GetTooltipText(Point p)
        {
            if (!TryHitPanel(p, out int deviceId, out Point local)) return null;
            // 【V1.55】local 是物理像素坐标，布局矩形需缩放后比较
            if (Scaled(_layout.RcPower.ToRectangle()).Contains(local)) return "上电状态：绿=上电，浅灰=下电";
            if (Scaled(_layout.RcWorkState.ToRectangle()).Contains(local)) return "工作状态：空闲=绿 / 选中(已上电待测试)=橙 / 繁忙(测试中)=黄 / 故障=红";
            if (Scaled(_layout.RcVacuumOpen.ToRectangle()).Contains(local)) return "真空开启状态：真空开=绿底，真空关=浅灰底";
            return null;
        }

        /// <summary>获取指定工位面板在画布中的边界（物理像素，用于局部重绘）</summary>
        private Rectangle GetPanelBounds(int deviceId)
        {
            int index = deviceId - 1;
            int col = index % _columns;
            int row = index / _columns;
            return new Rectangle(Scaled(col * _layout.PanelColumnWidth), Scaled(row * _layout.PanelRowHeight),
                                 Scaled(_layout.PanelColumnWidth), Scaled(_layout.PanelRowHeight));
        }

        #endregion

        /// <summary>
        /// 单个工位的显示状态缓存（内存字段，OnPaint 读取）
        /// </summary>
        private class GridItem
        {
            public int DeviceId;
            public string PressureText = "---";
            public string SnText = "";
            public string RecipeText = "";
            public string DelayStartText = "00:00:00";
            public string DelayArriveText = "00:00:00";
            public Color PowerColor = Color.LightGray;
            public Color PowerForeColor = Color.Black;
            public string PowerText = "下电";
            public Color VacuumColor = Color.LightGray;
            public Color VacuumForeColor = Color.Black;
            public string VacuumText = "真空关";
            public Color WorkColor = Color.LimeGreen;
            public Color WorkForeColor = Color.White;
            public string WorkText = "空闲";
            public Color BackColor = Color.White;
            public bool IsSelected;
        }
    }
}
