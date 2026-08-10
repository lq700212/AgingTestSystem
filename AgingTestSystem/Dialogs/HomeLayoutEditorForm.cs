using System;
using System.Drawing;
using System.Windows.Forms;
using AgingTestSystem.Models;
using AgingTestSystem.Views;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 主页区域调整可视化编辑器（【V1.58】）
    ///
    /// 【作用】
    /// 让用户不用改代码、不用看坐标数字，直接用鼠标拖动主界面各区域的"边缘"来调整尺寸：
    /// - 顶部标题栏高度（TopBarHeight）
    /// - 菜单栏高度（MenuHeight）
    /// - 右侧状态按钮区宽度（RightPanelWidth）
    /// - 底部状态栏高度（StatusBarHeight）
    /// 工作站列表面板（splitContainerMain.Panel1）自动占满剩余宽度，无需手动配置。
    ///
    /// 【界面布局】（本窗体全部由代码创建，无需 Designer 维护）
    /// ┌─ 主页区域调整 ────────────────────────────────────────┐
    /// │ ┌──────────────────────────────────────────────────┐ │
    /// │ │  预览区（自绘控件，按 1400×900 逻辑坐标系等比缩放）  │ │
    /// │ │ ┌──────────────────────────────────────────────┐ │ │
    /// │ │ │  顶部标题栏  TopBarHeight                      │ │ │
    /// │ │ ├──────────────────────────────────────────────┤ │ │
    /// │ │ │  菜单栏  MenuHeight                            │ │ │
    /// │ │ ├───────────────────────────┬──────────────────┤ │ │
    /// │ │ │                           │ 右侧状态按钮区      │ │ │
    /// │ │ │  工作站列表面板（自动占剩余） │ RightPanelWidth  │ │ │
    /// │ │ │                           │                  │ │ │
    /// │ │ ├───────────────────────────┴──────────────────┤ │ │
    /// │ │ │  状态栏  StatusBarHeight                      │ │ │
    /// │ │ └──────────────────────────────────────────────┘ │ │
    /// │ │  提示：把鼠标移到区域边缘，光标变双向箭头后按住拖动 │ │ │
    /// │ └──────────────────────────────────────────────────┘ │
    /// │ 顶部标题栏高 [nudTop]   菜单栏高 [nudMenu]            │
    /// │ 右侧区域宽 [nudRight]   状态栏高 [nudStatus]          │
    /// │        [恢复默认]  [保存]  [取消]                     │
    /// └──────────────────────────────────────────────────────┘
    ///
    /// 【交互说明】
    /// - 预览区内部固定使用 1400×900 逻辑坐标系（与主窗体设计尺寸一致），
    ///   按预览区客户区等比缩放显示，窗口拉大/缩小不影响比例。
    /// - 可拖动的 4 条边缘：顶部标题栏下边 / 菜单栏下边 / 右侧区左边 / 状态栏上边。
    /// - 鼠标靠近边缘（≤6 逻辑像素）时高亮该边缘并切换为双向箭头光标，
    ///   按住拖动实时改对应配置值，数值输入框同步刷新；
    ///   也可直接改输入框数值，拖动与输入双向同步。
    /// - 尺寸上下限来自 <see cref="HomeLayoutConfig"/> 的 Range 常量，防止拖出合理范围。
    ///
    /// 【保存】
    /// 点击【保存】把当前值写入 HomeLayout.json（<see cref="HomeLayoutConfig.Save"/>），
    /// 返回 DialogResult.OK；取消则不改动任何配置。
    /// </summary>
    public class HomeLayoutEditorForm : Form
    {
        /// <summary>当前编辑的布局配置（引用外部传入的实例，保存时由外部写盘）</summary>
        private readonly HomeLayoutConfig _layout;

        /// <summary>预览自绘控件</summary>
        private readonly HomeLayoutPreviewControl _preview;

        /// <summary>防止输入框与拖动互相触发造成死循环的标志位</summary>
        private bool _syncing;

        /// <summary>顶部标题栏高输入框</summary>
        private NumericUpDown _nudTop;

        /// <summary>菜单栏高输入框</summary>
        private NumericUpDown _nudMenu;

        /// <summary>右侧区域宽输入框</summary>
        private NumericUpDown _nudRight;

        /// <summary>状态栏高输入框</summary>
        private NumericUpDown _nudStatus;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="layout">当前生效的主页布局配置（由外部 LoadOrDefault 传入，编辑直接改其值）</param>
        public HomeLayoutEditorForm(HomeLayoutConfig layout)
        {
            _layout = layout;

            // 【V1.58.4 高 DPI】必须先用 SuspendLayout 挂起布局，再设置 AutoScaleDimensions
            // 与 AutoScaleMode，最后在构造函数末尾 ResumeLayout(false) 恢复。
            // 原因：若在构造过程中（未挂起）添加控件，WinForms 会在每次 Add 时触发
            // PerformLayout → PerformAutoScale，而此时 AutoScaleDimensions 尚未生效，
            // 会导致"以 96DPI 为基准不缩放"，高分屏（150%）下窗体/控件全部偏小。
            // Designer 生成的窗体（如 SettingsForm）同样在 SuspendLayout 后才设置这些，
            // 纯代码窗体必须手动补齐，否则 AutoScale 完全不生效（实测 V1.58.4 血泪）。
            SuspendLayout();

            // 窗体骨架
            Text = "主页区域调整";
            StartPosition = FormStartPosition.CenterParent;
            // 【V1.58.4 高 DPI 适配】与其他标准窗体一致：
            // - AutoScaleDimensions(6F,12F) + AutoScaleMode.Font，WinForms 会按
            //   实际 DPI 自动放大窗体及所有子控件（前提：app.manifest 声明 PerMonitorV2
            //   且 App.config 配置 DpiAwareness=PerMonitorV2，两处主程序均已具备）。
            // - 此前只设了 AutoScaleMode.Font 而未设 AutoScaleDimensions，WinForms
            //   默认以 96DPI 基准不缩放，高分屏（如 150%）下控件/文字偏小或布局错位。
            AutoScaleDimensions = new SizeF(6F, 12F);
            AutoScaleMode = AutoScaleMode.Font;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(560, 460);
            ClientSize = new Size(640, 520);

            // 预览自绘控件（占满窗体中部）
            _preview = new HomeLayoutPreviewControl { Layout = _layout, Dock = DockStyle.Fill };
            _preview.LayoutChanged += Preview_LayoutChanged;

            // 数值输入面板：4 行等分（Percent 25%，行高随窗体 AutoScale 等比缩放，
            // 兼容高低 DPI；Absolute 行高不会随缩放，高分屏下会溢出/留白）。
            // 【V1.58.4 高 DPI】行高改为 Percent 等分，行内控件用 Top|Bottom 锚定，
            // 保证 DPI 缩放后文字仍垂直居中、不溢出。
            var pnlValues = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 148,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(12, 6, 12, 6)
            };
            pnlValues.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            pnlValues.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            // 4 行各占 25%，自适应窗体缩放后的高度（96DPI 下约 34px/行，容纳控件）
            pnlValues.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            pnlValues.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            pnlValues.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            pnlValues.RowStyles.Add(new RowStyle(SizeType.Percent, 25));

            // 四个尺寸输入框（范围与 HomeLayoutConfig.Range 常量同步）
            _nudTop = CreateNud(pnlValues, "顶部标题栏高 (px)", _layout.TopBarHeight,
                HomeLayoutConfig.TopBarRange.Min, HomeLayoutConfig.TopBarRange.Max, 0);
            _nudMenu = CreateNud(pnlValues, "菜单栏高 (px)", _layout.MenuHeight,
                HomeLayoutConfig.MenuRange.Min, HomeLayoutConfig.MenuRange.Max, 1);
            _nudRight = CreateNud(pnlValues, "右侧区域宽 (px)", _layout.RightPanelWidth,
                HomeLayoutConfig.RightPanelRange.Min, HomeLayoutConfig.RightPanelRange.Max, 2);
            _nudStatus = CreateNud(pnlValues, "状态栏高 (px)", _layout.StatusBarHeight,
                HomeLayoutConfig.StatusBarRange.Min, HomeLayoutConfig.StatusBarRange.Max, 3);

            // 底部按钮
            var pnlBottom = new Panel { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(12, 6, 12, 6) };
            var btnRestore = new Button
            {
                Text = "恢复默认",
                Width = 96, Height = 32,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnRestore.Click += BtnRestore_Click;

            var btnCancel = new Button
            {
                Text = "取消",
                Width = 90, Height = 32,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;

            var btnSave = new Button
            {
                Text = "保存",
                Width = 90, Height = 32,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnSave.Click += BtnSave_Click;

            // 手工摆放（不用 FlowLayout，避免依赖其尺寸算法）：恢复默认在左下，保存/取消在右下
            btnRestore.Location = new Point(12, 10);
            btnCancel.Location = new Point(pnlBottom.Width - 12 - 90 - 90 - 6, 10);
            btnSave.Location = new Point(pnlBottom.Width - 12 - 90, 10);

            pnlBottom.Controls.Add(btnRestore);
            pnlBottom.Controls.Add(btnCancel);
            pnlBottom.Controls.Add(btnSave);

            // 顶部说明条
            var lblTip = new Label
            {
                Dock = DockStyle.Top,
                Height = 26,
                Text = "将鼠标移到区域边缘，光标变为双向箭头后按住拖动即可调整尺寸（单位：px）",
                ForeColor = Color.FromArgb(80, 80, 80),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            };

            // 组合
            Controls.Add(_preview);
            Controls.Add(pnlValues);
            Controls.Add(lblTip);
            Controls.Add(pnlBottom);

            // 【V1.58.4 高 DPI】所有控件添加完毕后再恢复布局，此时才真正执行
            // PerformAutoScale（以 AutoScaleDimensions=6×12 为基准按实际 DPI 放大）。
            ResumeLayout(false);
        }

        /// <summary>
        /// 创建一组"文字标签 + 数值输入框"并放入数值面板的指定行
        /// </summary>
        private NumericUpDown CreateNud(TableLayoutPanel pnl, string caption, int value,
            decimal min, decimal max, int row)
        {
            var lbl = new Label
            {
                Text = caption,
                AutoSize = true,
                // 垂直锚定 Top|Bottom：行高随窗体缩放变化时文字保持垂直居中
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom,
                TextAlign = ContentAlignment.MiddleLeft
            };
            var nud = new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                Value = Math.Max(min, Math.Min(max, value)),
                Width = 120,
                // 垂直锚定 Top|Bottom：DPI 缩放后输入框随行高拉高、始终占满行
                Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom
            };
            nud.ValueChanged += Nud_ValueChanged;
            pnl.Controls.Add(lbl, 0, row);
            pnl.Controls.Add(nud, 1, row);
            return nud;
        }

        /// <summary>数值输入框变化 → 同步到配置并刷新预览</summary>
        private void Nud_ValueChanged(object sender, EventArgs e)
        {
            if (_syncing) return;
            _syncing = true;
            _layout.TopBarHeight = (int)_nudTop.Value;
            _layout.MenuHeight = (int)_nudMenu.Value;
            _layout.RightPanelWidth = (int)_nudRight.Value;
            _layout.StatusBarHeight = (int)_nudStatus.Value;
            _preview.Invalidate();
            _syncing = false;
        }

        /// <summary>拖动预览边缘 → 同步到输入框</summary>
        private void Preview_LayoutChanged(object sender, EventArgs e)
        {
            if (_syncing) return;
            _syncing = true;
            _nudTop.Value = _layout.TopBarHeight;
            _nudMenu.Value = _layout.MenuHeight;
            _nudRight.Value = _layout.RightPanelWidth;
            _nudStatus.Value = _layout.StatusBarHeight;
            _syncing = false;
        }

        /// <summary>
        /// 恢复默认：把四个值重置为内置默认并刷新。
        /// 注意：右侧宽度默认值写死在 <see cref="MainForm.DefaultRightPanelWidth"/>（300），
        /// 其余三区域用 <see cref="HomeLayoutConfig"/> 的类默认，与主窗体未配置时的
        /// 生效值保持一致，避免"恢复默认"反而变成另一套尺寸。
        /// </summary>
        private void BtnRestore_Click(object sender, EventArgs e)
        {
            var def = new HomeLayoutConfig();
            _layout.TopBarHeight = def.TopBarHeight;
            _layout.MenuHeight = def.MenuHeight;
            _layout.RightPanelWidth = MainForm.DefaultRightPanelWidth;
            _layout.StatusBarHeight = def.StatusBarHeight;
            _syncing = true;
            _nudTop.Value = _layout.TopBarHeight;
            _nudMenu.Value = _layout.MenuHeight;
            _nudRight.Value = _layout.RightPanelWidth;
            _nudStatus.Value = _layout.StatusBarHeight;
            _syncing = false;
            _preview.Invalidate();
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
    /// 内部固定使用 1400×900 逻辑坐标系（与主窗体 tableLayoutPanelMain 设计尺寸一致），
    /// 绘制前先把客户区等比缩放到 1400×900 的视口（居中留白），
    /// 所有区域坐标/鼠标命中判断都在逻辑坐标系里做，天然适配任意窗口大小与 DPI。
    ///
    /// 【可拖动边缘】共 4 条，拖动时通过 <see cref="Layout"/> 属性实时改值并触发
    /// <see cref="LayoutChanged"/> 事件：
    /// 1. 顶部标题栏下边（y = TopBarHeight）→ 调 TopBarHeight
    /// 2. 菜单栏下边（y = TopBarHeight + MenuHeight）→ 调 MenuHeight
    /// 3. 右侧区域左边（x = 1400 - RightPanelWidth）→ 调 RightPanelWidth
    /// 4. 状态栏上边（y = 900 - StatusBarHeight）→ 调 StatusBarHeight
    /// </summary>
    internal class HomeLayoutPreviewControl : Control
    {
        /// <summary>逻辑坐标系总宽（与主窗体设计宽一致）</summary>
        private const int LOGIC_W = 1400;

        /// <summary>逻辑坐标系总高（与主窗体设计高一致）</summary>
        private const int LOGIC_H = 900;

        /// <summary>鼠标靠近边缘多少逻辑像素内视为"可拖动"</summary>
        private const int HIT_TOLERANCE = 6;

        /// <summary>当前编辑的布局配置</summary>
        public HomeLayoutConfig Layout { get; set; }

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

        /// <summary>可拖动的边缘类型</summary>
        private enum DragEdge
        {
            None,
            TopBarBottom,   // 顶部标题栏下边 → 调 TopBarHeight
            MenuBottom,     // 菜单栏下边 → 调 MenuHeight
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

        /// <summary>把客户区等比缩放为 1400×900 的视口矩形（居中留白）</summary>
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
            int topBar = Layout.TopBarHeight;
            int menu = Layout.MenuHeight;
            int right = Layout.RightPanelWidth;
            int status = Layout.StatusBarHeight;

            // 三个主体区域的高度（逻辑）：标题 + 菜单 + 剩余主体
            int bodyTop = topBar + menu;               // 主体区（工作站+右侧）顶部逻辑 y
            int bodyH = LOGIC_H - status - bodyTop;    // 主体区高度

            // ① 顶部标题栏（天蓝）
            DrawBlock(g, v.Left, v.Top,
                LOGIC_W, topBar, "顶部标题栏  " + topBar + "px",
                Color.FromArgb(230, 240, 255), Color.FromArgb(70, 110, 180));

            // ② 菜单栏（浅灰）
            DrawBlock(g, v.Left, v.Top + ScaleH(topBar),
                LOGIC_W, menu, "菜单栏  " + menu + "px",
                Color.FromArgb(240, 240, 240), Color.FromArgb(90, 90, 90));

            // ③ 工作站列表面板（浅绿，占左侧剩余）
            DrawBlock(g, v.Left, v.Top + ScaleH(bodyTop),
                LOGIC_W - ScaleW(right), bodyH, "工作站列表面板（自动占剩余）",
                Color.FromArgb(235, 250, 235), Color.FromArgb(70, 130, 70));

            // ④ 右侧状态按钮区（浅橙）
            DrawBlock(g, v.Left + ScaleW(LOGIC_W - right), v.Top + ScaleH(bodyTop),
                right, bodyH, "右侧状态按钮区  " + right + "px",
                Color.FromArgb(255, 244, 230), Color.FromArgb(200, 130, 40));

            // ⑤ 状态栏（浅紫）
            DrawBlock(g, v.Left, v.Top + ScaleH(LOGIC_H - status),
                LOGIC_W, status, "状态栏  " + status + "px",
                Color.FromArgb(245, 240, 255), Color.FromArgb(120, 100, 180));

            // 绘制可拖动边缘高亮线（拖动中/悬停时加粗显红）
            DrawEdges(g, v, topBar, menu, right, status);
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
        /// 画 4 条可拖动边缘。悬停/拖动中的边缘用红色加粗显示，方便用户看出"这里可以拖"。
        /// </summary>
        private void DrawEdges(Graphics g, Rectangle v, int topBar, int menu, int right, int status)
        {
            int bodyTop = topBar + menu;
            // 每条边缘的逻辑起点与长度
            var edges = new (DragEdge Edge, int X1, int Y1, int X2, int Y2)[]
            {
                (DragEdge.TopBarBottom, 0, topBar, LOGIC_W, topBar),                      // 标题栏下边（横线，全宽）
                (DragEdge.MenuBottom, 0, bodyTop, LOGIC_W, bodyTop),                      // 菜单栏下边（横线，全宽）
                (DragEdge.RightLeft, LOGIC_W - right, bodyTop, LOGIC_W - right, LOGIC_H - status), // 右侧左边（竖线）
                (DragEdge.StatusTop, 0, LOGIC_H - status, LOGIC_W, LOGIC_H - status),     // 状态栏上边（横线，全宽）
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

            int topBar = Layout.TopBarHeight;
            int menu = Layout.MenuHeight;
            int right = Layout.RightPanelWidth;
            int status = Layout.StatusBarHeight;
            int bodyTop = topBar + menu;

            // 依次判断 4 条边缘（距离 ≤ HIT_TOLERANCE 且落在边缘线段范围内）
            if (Math.Abs(lp.Y - topBar) <= HIT_TOLERANCE && lp.X >= 0 && lp.X <= LOGIC_W)
                return DragEdge.TopBarBottom;
            if (Math.Abs(lp.Y - bodyTop) <= HIT_TOLERANCE && lp.X >= 0 && lp.X <= LOGIC_W)
                return DragEdge.MenuBottom;
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
                case DragEdge.TopBarBottom:
                case DragEdge.MenuBottom: return Cursors.SizeNS;  // 上下拖动
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
                case DragEdge.TopBarBottom: return Layout.TopBarHeight;
                case DragEdge.MenuBottom: return Layout.MenuHeight;
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
                case DragEdge.TopBarBottom:
                    // 下边向下拖 → 高度增大
                    newValue = _dragStartValue + (lp.Y - _dragStartLogic.Y);
                    newValue = Clamp(newValue, HomeLayoutConfig.TopBarRange);
                    break;
                case DragEdge.MenuBottom:
                    newValue = _dragStartValue + (lp.Y - _dragStartLogic.Y);
                    newValue = Clamp(newValue, HomeLayoutConfig.MenuRange);
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
                case DragEdge.TopBarBottom: Layout.TopBarHeight = value; break;
                case DragEdge.MenuBottom: Layout.MenuHeight = value; break;
                case DragEdge.RightLeft: Layout.RightPanelWidth = value; break;
                case DragEdge.StatusTop: Layout.StatusBarHeight = value; break;
            }
            Invalidate();
        }
    }
}
