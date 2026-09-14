using System;
using System.Drawing;
using System.Windows.Forms;
using AgingTestSystem.Models;
using AgingTestSystem.Services;
using AgingTestSystem.Views;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 主页区域调整可视化编辑器（【V1.58】）
    ///
    /// 【作用】
    /// 让用户不用改代码、不用看坐标数字，直接用鼠标拖动主界面各区域的"边缘"来调整尺寸：
    /// - 顶栏高度（HeaderHeight；【V1.88.23】顶栏菜单并单行，双键合一）
    /// - 右侧状态按钮区宽度（RightPanelWidth）
    /// - 底部状态栏高度（StatusBarHeight）
    /// 工作站列表面板（splitContainerMain.Panel1）自动占满剩余宽度，无需手动配置。
    ///
    /// 【界面布局】（本窗体全部由代码创建，无需 Designer 维护）
    /// ┌─ 主页区域调整 ────────────────────────────────────────┐
    /// │ ┌──────────────────────────────────────────────────┐ │
    /// │ │  预览区（自绘控件，按 1400×900 逻辑坐标系等比缩放）  │ │
    /// │ │ ┌──────────────────────────────────────────────┐ │ │
    /// │ │ │  顶栏 HeaderHeight（项目/权限/通讯＋4 按钮）    │ │ │
    /// │ │ ├───────────────────────────┬──────────────────┤ │ │
    /// │ │ │                           │ 右侧状态按钮区      │ │ │
    /// │ │ │  工作站列表面板（自动占剩余） │ RightPanelWidth  │ │ │
    /// │ │ │                           │                  │ │ │
    /// │ │ ├───────────────────────────┴──────────────────┤ │ │
    /// │ │ │  状态栏  StatusBarHeight                      │ │ │
    /// │ │ └──────────────────────────────────────────────┘ │ │
    /// │ │  提示：把鼠标移到区域边缘，光标变双向箭头后按住拖动 │ │ │
    /// │ └──────────────────────────────────────────────────┘ │
    /// │ 顶栏高 [nudHeader]                                    │
    /// │ 右侧区域宽 [nudRight]   状态栏高 [nudStatus]          │
    /// │        [恢复默认]  [保存]  [取消]                     │
    /// └──────────────────────────────────────────────────────┘
    ///
    /// 【交互说明】
    /// - 预览区内部固定使用 1400×900 逻辑坐标系（与主窗体设计尺寸一致），
    ///   按预览区客户区等比缩放显示，窗口拉大/缩小不影响比例。
    /// - 可拖动的 3 条边缘：顶栏下边 / 右侧区左边 / 状态栏上边。
    /// - 鼠标靠近边缘（≤6 逻辑像素）时高亮该边缘并切换为双向箭头光标，
    ///   按住拖动实时改对应配置值，数值输入框同步刷新；
    ///   也可直接改输入框数值，拖动与输入双向同步。
    /// - 尺寸上下限来自 <see cref="HomeLayoutConfig"/> 的 Range 常量，防止拖出合理范围。
    ///
    /// 【保存】
    /// 点击【保存】把当前值写入 HomeLayout.json（<see cref="HomeLayoutConfig.Save"/>），
    /// 返回 DialogResult.OK；取消则不改动任何配置。
    /// </summary>
    public partial class HomeLayoutEditorForm : Sunny.UI.UIForm
    {
        /// <summary>当前编辑的布局配置（引用外部传入的实例，保存时由外部写盘）</summary>
        private readonly HomeLayoutConfig _layout;

        /// <summary>
        /// 自绘预览控件（【V1.72.16】在构造里代码创建，不进 Designer：
        /// HomeLayoutPreviewControl 是内部自定义控件（还用 new 藏了基类 Layout 事件），
        /// Designer 里 new 它/挂它的 LayoutChanged 事件，每次打开预览都标脏、存盘又零 diff，
        /// 纯幽灵脏，删 Layout=null 行都去不掉，只能整机搬出来，Designer 里只留空 Panel 占位。
        /// 以后新增"内部自绘控件"一律走这个姿势：Designer 只放标准控件占位，本体代码创建。
        /// </summary>
        private readonly HomeLayoutPreviewControl _preview;

        /// <summary>防止输入框与拖动互相触发造成死循环的标志位</summary>
        private bool _syncing;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="layout">当前生效的主页布局配置（由外部 LoadOrDefault 传入，编辑直接改其值）</param>
        public HomeLayoutEditorForm(HomeLayoutConfig layout)
        {
            _layout = layout;

            // 【V1.72.12 Designer 化】静态边框搬进 HomeLayoutEditorForm.Designer.cs
            // （含 SuspendLayout 包裹 + AutoScale 三要素 + Dock 挂接，都在里面）。
            // 这里只回填"要吃构造参数"的三项：预览控件创建 + Layout 引用 + 四个输入框初值。
            InitializeComponent();
            _preview = new HomeLayoutPreviewControl
            {
                Name = "_preview",
                Dock = DockStyle.Fill,
                Layout = _layout,
                BackColor = GetPreviewBackColor(false),
            };
            _preview.LayoutChanged += Preview_LayoutChanged;
            _pnlPreviewHost.Controls.Add(_preview);
            _syncing = true;
            _nudHeader.Value = ClampNud(_nudHeader, _layout.HeaderHeight);
            _nudRight.Value = ClampNud(_nudRight, _layout.RightPanelWidth);
            _nudStatus.Value = ClampNud(_nudStatus, _layout.StatusBarHeight);
            _syncing = false;
        }

        /// <summary>输入框初值钳制（与原来 CreateNud 的 Max(Min,Min(Max,Value)) 同口径）</summary>
        private static decimal ClampNud(NumericUpDown nud, int value)
        {
            if (value < nud.Minimum) return nud.Minimum;
            if (value > nud.Maximum) return nud.Maximum;
            return value;
        }

        /// <summary>
        /// 预览画布主题底色（【V1.60.4】纯函数，方便回归直接断言）。
        /// 深色用户指定纯黑（各区域色块自带浅底+块内文字，不依赖画布底，黑底安全）；
        /// 浅色保持白纸效果。
        /// </summary>
        /// <param name="dark">true=深色，false=浅色</param>
        public static Color GetPreviewBackColor(bool dark)
        {
            return dark ? Color.Black : Color.White;
        }

        /// <summary>
        /// 打开后调一次：整窗按当前主题着色 + 预览画布按主题换底并重绘。
        /// （ThemeManager 跳过自绘预览控件，画布底由这里显式指定；窗体每次 new 的新实例，
        /// 浅色下本来就是白底，无需恢复。）
        /// </summary>
        public void ApplyTheme()
        {
            ThemeManager.ApplyTo(this);
            if (_preview != null)
            {
                _preview.BackColor = GetPreviewBackColor(ThemeManager.IsDark);
                _preview.Invalidate();
            }
        }

        /// <summary>
        /// 数值输入框变化 → 同步到配置并刷新预览。
        /// 【V1.88.17】写入前按 Range 再钳一次：输入框自己的 Maximum 管得住上下箭头，
        /// 管不住手输；钳后脏值到不了 json，
        /// 保存的文件永远合法，下次 LoadOrDefault 不用替它收拾。
        /// </summary>
        private void Nud_ValueChanged(object sender, EventArgs e)
        {
            if (_syncing) return;
            _syncing = true;
            _layout.HeaderHeight = ClampToRange((int)_nudHeader.Value, HomeLayoutConfig.HeaderRange);
            _layout.RightPanelWidth = ClampToRange((int)_nudRight.Value, HomeLayoutConfig.RightPanelRange);
            _layout.StatusBarHeight = ClampToRange((int)_nudStatus.Value, HomeLayoutConfig.StatusBarRange);
            _preview.Invalidate();
            _syncing = false;
        }

        /// <summary>整数钳到 Range（输入框写入配置前的最后一道闸，与 LoadOrDefault 的钳制同口径）</summary>
        private static int ClampToRange(int v, (int Min, int Max) range)
        {
            return v < range.Min ? range.Min : (v > range.Max ? range.Max : v);
        }

        /// <summary>拖动预览边缘 → 同步到输入框</summary>
        private void Preview_LayoutChanged(object sender, EventArgs e)
        {
            if (_syncing) return;
            _syncing = true;
            // 【V1.72.16】赋值前一律钳制：HomeLayout.json 可能是旧版本存的越界值
            // （或预览控件将来又被拖出范围），直接赋给 NumericUpDown.Value 会抛
            // ArgumentOutOfRangeException（现场"340 的值对于 Value 无效"就是这么来的）。
            _nudHeader.Value = ClampNud(_nudHeader, _layout.HeaderHeight);
            _nudRight.Value = ClampNud(_nudRight, _layout.RightPanelWidth);
            _nudStatus.Value = ClampNud(_nudStatus, _layout.StatusBarHeight);
            _syncing = false;
        }

        /// <summary>
        /// 恢复默认：把三个值重置为内置默认并刷新。
        /// 注意：右侧宽度默认值写死在 <see cref="MainForm.DefaultRightPanelWidth"/>（240），
        /// 其余三区域用 <see cref="HomeLayoutConfig"/> 的类默认，与主窗体未配置时的
        /// 生效值保持一致，避免"恢复默认"反而变成另一套尺寸。
        /// </summary>
        private void BtnRestore_Click(object sender, EventArgs e)
        {
            var def = new HomeLayoutConfig();
            _layout.HeaderHeight = def.HeaderHeight;
            _layout.RightPanelWidth = MainForm.DefaultRightPanelWidth;
            _layout.StatusBarHeight = def.StatusBarHeight;
            _syncing = true;
            // 【V1.72.16】同上钳制：缺省值理论上都在范围内，但钳一下零成本，
            // 万一将来改了 Range 常量忘同步 Designer，这里就是最后一道闸。
            _nudHeader.Value = ClampNud(_nudHeader, _layout.HeaderHeight);
            _nudRight.Value = ClampNud(_nudRight, _layout.RightPanelWidth);
            _nudStatus.Value = ClampNud(_nudStatus, _layout.StatusBarHeight);
            _syncing = false;
            _preview.Invalidate();
        }

        /// <summary>取消：不改动任何配置，直接关闭（Designer 原匿名 lambda 落袋）</summary>
        private void BtnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        /// <summary>保存：把当前配置写入 HomeLayout.json 并关闭窗体</summary>
        private void BtnSave_Click(object sender, EventArgs e)
        {
            _layout.Save();
            DialogResult = DialogResult.OK;
        }
    }

    /// <summary>
    /// 主页布局预览自绘控件。
    ///
    /// 【坐标系】
    /// 内部固定使用 1280×900 逻辑坐标系（与主窗体 tableLayoutPanelMain 设计尺寸一致），
    /// 绘制前先把客户区等比缩放到 1280×900 的视口（居中留白），
    /// 所有区域坐标/鼠标命中判断都在逻辑坐标系里做，天然适配任意窗口大小与 DPI。
    ///
    /// 【可拖动边缘】共 3 条，拖动时通过 <see cref="Layout"/> 属性实时改值并触发
    /// <see cref="LayoutChanged"/> 事件：
    /// 1. 顶栏下边（y = HeaderHeight）→ 调 HeaderHeight
    /// （【V1.88.23】顶栏菜单并单行，旧标题栏/菜单两条边合一）
    /// 2. 右侧区域左边（x = 1280 - RightPanelWidth）→ 调 RightPanelWidth
    /// 3. 状态栏上边（y = 900 - StatusBarHeight）→ 调 StatusBarHeight
    /// </summary>
    internal class HomeLayoutPreviewControl : Control
    {
        /// <summary>逻辑坐标系总宽（与主窗体设计宽一致）</summary>
        private const int LOGIC_W = 1280;

        /// <summary>逻辑坐标系总高（与主窗体设计高一致）</summary>
        private const int LOGIC_H = 900;

        /// <summary>鼠标靠近边缘多少逻辑像素内视为"可拖动"</summary>
        private const int HIT_TOLERANCE = 6;

        /// <summary>当前编辑的布局配置</summary>
        /// 【V1.64.3】加 new 显式声明有意隐藏基类 Control.Layout 事件（CS0108）：
        /// 基类那个 Layout 是布局事件，本预览控件从不用它（类内 18 处 Layout. 全指本属性），
        /// 不改名是为少动调用方（_preview = new ... { Layout = _layout } 等），加 new 即零警告。
        public new HomeLayoutConfig Layout { get; set; }

        /// <summary>布局任一尺寸被拖动改变时触发（供输入框同步）</summary>
        public event EventHandler LayoutChanged;

        /// <summary>当前拖动的边缘（None=未拖动）</summary>
        private DragEdge _dragging;

        /// <summary>拖动开始时鼠标的逻辑坐标（用于计算拖动增量）</summary>
        private Point _dragStartLogic;

        /// <summary>拖动开始时对应配置的初始值（增量叠加后钳制到范围）</summary>
        private int _dragStartValue;

        /// <summary>鼠标当前悬停的高亮边缘（None=无）</summary>
        private DragEdge _hoverEdge;

        /// <summary>可拖动的边缘类型（【V1.88.23】顶栏菜单并单行：标题/菜单两条边合一）</summary>
        private enum DragEdge
        {
            None,
            HeaderBottom,   // 顶栏下边 → 调 HeaderHeight
            RightLeft,      // 右侧区域左边 → 调 RightPanelWidth
            StatusTop       // 状态栏上边 → 调 StatusBarHeight
        }

        public HomeLayoutPreviewControl()
        {
            // 自绘控件：关闭标准控件擦背景，避免闪烁（全量自绘由 OnPaint 完成）
            SetStyle(ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint, true);
            BackColor = Color.White;
        }

        // ===================== 绘制 =====================

        /// <summary>把客户区等比缩放为 1280×900 的视口矩形（居中留白）</summary>
        private Rectangle GetViewport()
        {
            int w = ClientSize.Width;
            int h = ClientSize.Height;
            float scale = Math.Min((float)w / LOGIC_W, (float)h / LOGIC_H);
            int vw = (int)(LOGIC_W * scale);
            int vh = (int)(LOGIC_H * scale);
            return new Rectangle((w - vw) / 2, (h - vh) / 2, vw, vh);
        }

        /// <summary>逻辑坐标 → 客户区物理坐标</summary>
        private Point LogicToClient(int lx, int ly)
        {
            Rectangle v = GetViewport();
            return new Point(
                v.Left + (int)((long)lx * v.Width / LOGIC_W),
                v.Top + (int)((long)ly * v.Height / LOGIC_H));
        }

        /// <summary>客户区物理坐标 → 逻辑坐标（越界点不钳制，用于计算拖动增量）</summary>
        private Point ClientToLogic(Point p)
        {
            Rectangle v = GetViewport();
            return new Point(
                (int)((long)(p.X - v.Left) * LOGIC_W / v.Width),
                (int)((long)(p.Y - v.Top) * LOGIC_H / v.Height));
        }

        /// <summary>逻辑宽度 → 客户区像素宽</summary>
        private int ScaleW(int lw)
        {
            Rectangle v = GetViewport();
            return (int)((long)lw * v.Width / LOGIC_W);
        }

        /// <summary>逻辑高度 → 客户区像素高</summary>
        private int ScaleH(int lh)
        {
            Rectangle v = GetViewport();
            return (int)((long)lh * v.Height / LOGIC_H);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.Clear(BackColor);

            if (Layout == null) return;

            Rectangle v = GetViewport();
            int header = Layout.HeaderHeight;
            int right = Layout.RightPanelWidth;
            int status = Layout.StatusBarHeight;

            // 主体区（工作站+右侧）顶部逻辑 y 与高度
            int bodyTop = header;
            int bodyH = LOGIC_H - status - bodyTop;    // 主体区高度

            // ① 顶栏（天蓝；【V1.88.23】标题栏+菜单栏并单行，一块画）
            DrawBlock(g, v.Left, v.Top,
                LOGIC_W, header, "顶栏（项目/权限/通讯+4按钮）  " + header + "px",
                Color.FromArgb(230, 240, 255), Color.FromArgb(70, 110, 180));

            // ② 工作站列表面板（浅绿，占左侧剩余）
            DrawBlock(g, v.Left, v.Top + ScaleH(bodyTop),
                LOGIC_W - ScaleW(right), bodyH, "工作站列表面板（自动占剩余）",
                Color.FromArgb(235, 250, 235), Color.FromArgb(70, 130, 70));

            // ③ 右侧状态按钮区（浅橙）
            DrawBlock(g, v.Left + ScaleW(LOGIC_W - right), v.Top + ScaleH(bodyTop),
                right, bodyH, "右侧状态按钮区  " + right + "px",
                Color.FromArgb(255, 244, 230), Color.FromArgb(200, 130, 40));

            // ④ 状态栏（浅紫）
            DrawBlock(g, v.Left, v.Top + ScaleH(LOGIC_H - status),
                LOGIC_W, status, "状态栏  " + status + "px",
                Color.FromArgb(245, 240, 255), Color.FromArgb(120, 100, 180));

            // 绘制可拖动边缘高亮线（拖动中/悬停时加粗显红）
            DrawEdges(g, v, header, right, status);
        }

        /// <summary>画一个带边框的文字块（逻辑坐标转客户区像素）</summary>
        private void DrawBlock(Graphics g, int x, int y, int lw, int lh,
            string text, Color fill, Color fore)
        {
            Rectangle rc = new Rectangle(x, y, ScaleW(lw), ScaleH(lh));
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(Color.FromArgb(180, 180, 180)))
            {
                g.FillRectangle(brush, rc);
                g.DrawRectangle(pen, rc.X, rc.Y, rc.Width - 1, rc.Height - 1);
            }
            TextRenderer.DrawText(g, text, Font, rc, fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        /// <summary>
        /// 画 3 条可拖动边缘。悬停/拖动中的边缘用红色加粗显示，方便用户看出"这里可以拖"。
        /// </summary>
        private void DrawEdges(Graphics g, Rectangle v, int header, int right, int status)
        {
            int bodyTop = header;
            // 每条边缘的逻辑起点与长度
            var edges = new (DragEdge Edge, int X1, int Y1, int X2, int Y2)[]
            {
                (DragEdge.HeaderBottom, 0, header, LOGIC_W, header),                  // 顶栏下边（横线，全宽）
                (DragEdge.RightLeft, LOGIC_W - right, bodyTop, LOGIC_W - right, LOGIC_H - status), // 右侧左边（竖线）
                (DragEdge.StatusTop, 0, LOGIC_H - status, LOGIC_W, LOGIC_H - status), // 状态栏上边（横线，全宽）
            };

            foreach (var ed in edges)
            {
                bool active = (_dragging == ed.Edge) || (_dragging == DragEdge.None && _hoverEdge == ed.Edge);
                Color lineColor = active ? Color.Red : Color.FromArgb(120, 120, 120);
                int thickness = active ? 2 : 1;
                using (var pen = new Pen(lineColor, thickness))
                {
                    g.DrawLine(pen,
                        LogicToClient(ed.X1, ed.Y1),
                        LogicToClient(ed.X2, ed.Y2));
                }
            }
        }

        // ===================== 鼠标命中与拖动 =====================

        /// <summary>根据逻辑坐标判断鼠标悬停在哪个边缘上（不在任何边缘附近返回 None）</summary>
        private DragEdge HitTest(Point lp)
        {
            if (Layout == null) return DragEdge.None;

            int header = Layout.HeaderHeight;
            int right = Layout.RightPanelWidth;
            int status = Layout.StatusBarHeight;
            int bodyTop = header;

            // 依次判断 3 条边缘（距离 ≤ HIT_TOLERANCE 且落在边缘线段范围内）
            if (Math.Abs(lp.Y - header) <= HIT_TOLERANCE && lp.X >= 0 && lp.X <= LOGIC_W)
                return DragEdge.HeaderBottom;
            if (Math.Abs(lp.X - (LOGIC_W - right)) <= HIT_TOLERANCE && lp.Y >= bodyTop && lp.Y <= LOGIC_H - status)
                return DragEdge.RightLeft;
            if (Math.Abs(lp.Y - (LOGIC_H - status)) <= HIT_TOLERANCE && lp.X >= 0 && lp.X <= LOGIC_W)
                return DragEdge.StatusTop;

            return DragEdge.None;
        }

        /// <summary>边缘类型 → 双向箭头光标</summary>
        private static Cursor EdgeToCursor(DragEdge edge)
        {
            switch (edge)
            {
                case DragEdge.RightLeft: return Cursors.SizeWE;   // 左右拖动
                case DragEdge.StatusTop:
                case DragEdge.HeaderBottom: return Cursors.SizeNS;  // 上下拖动
                default: return Cursors.Default;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point lp = ClientToLogic(e.Location);

            if (_dragging != DragEdge.None)
            {
                // 拖动中：按边缘类型计算新值
                ApplyDragValue(_dragging, lp);
                return;
            }

            // 非拖动：更新悬停高亮与光标
            _hoverEdge = HitTest(lp);
            Cursor = EdgeToCursor(_hoverEdge);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            Point lp = ClientToLogic(e.Location);
            DragEdge edge = HitTest(lp);
            if (edge == DragEdge.None) return;

            // 开始拖动：记录起点与配置初值
            _dragging = edge;
            _dragStartLogic = lp;
            _dragStartValue = GetValue(edge);
            Capture = true;   // 捕获鼠标，拖出控件范围也持续更新
            Cursor = EdgeToCursor(edge);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_dragging == DragEdge.None) return;

            _dragging = DragEdge.None;
            Capture = false;
            Point lp = ClientToLogic(e.Location);
            _hoverEdge = HitTest(lp);
            Cursor = EdgeToCursor(_hoverEdge);
            Invalidate();
        }

        /// <summary>读取某条边缘当前对应的配置值</summary>
        private int GetValue(DragEdge edge)
        {
            switch (edge)
            {
                case DragEdge.HeaderBottom: return Layout.HeaderHeight;
                case DragEdge.RightLeft: return Layout.RightPanelWidth;
                case DragEdge.StatusTop: return Layout.StatusBarHeight;
                default: return 0;
            }
        }

        /// <summary>
        /// 用当前鼠标逻辑坐标计算拖动增量并更新配置值（带范围钳制）。
        /// 增量 = 当前逻辑坐标 - 拖动起点逻辑坐标，换算到对应尺寸后再叠加初值。
        /// </summary>
        private void ApplyDragValue(DragEdge edge, Point lp)
        {
            int newValue = _dragStartValue;
            switch (edge)
            {
                case DragEdge.HeaderBottom:
                    // 下边向下拖 → 高度增大
                    newValue = _dragStartValue + (lp.Y - _dragStartLogic.Y);
                    newValue = Clamp(newValue, HomeLayoutConfig.HeaderRange);
                    break;
                case DragEdge.RightLeft:
                    // 左边向右拖 → 右侧区域变窄（宽度减小）
                    newValue = _dragStartValue - (lp.X - _dragStartLogic.X);
                    newValue = Clamp(newValue, HomeLayoutConfig.RightPanelRange);
                    break;
                case DragEdge.StatusTop:
                    // 上边向上拖 → 状态栏变高
                    newValue = _dragStartValue - (lp.Y - _dragStartLogic.Y);
                    newValue = Clamp(newValue, HomeLayoutConfig.StatusBarRange);
                    break;
            }

            // 值有变化才更新（避免无意义重绘）
            if (newValue != GetValue(edge))
            {
                SetValue(edge, newValue);
                LayoutChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private static int Clamp(int v, (int Min, int Max) range)
        {
            return v < range.Min ? range.Min : (v > range.Max ? range.Max : v);
        }

        private void SetValue(DragEdge edge, int value)
        {
            switch (edge)
            {
                case DragEdge.HeaderBottom: Layout.HeaderHeight = value; break;
                case DragEdge.RightLeft: Layout.RightPanelWidth = value; break;
                case DragEdge.StatusTop: Layout.StatusBarHeight = value; break;
            }
            Invalidate();
        }
    }
}
