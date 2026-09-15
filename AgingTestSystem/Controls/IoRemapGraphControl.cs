using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AgingTestSystem.Models;
using AgingTestSystem.Services;

namespace AgingTestSystem.Controls
{
    /// <summary>
    /// 备用映射可视化连线控件（可复用的 UserControl：通讯测试窗、系统设置表共用）。
    /// 【界面】（纯自绘，一套坐标，走 AutoScroll 滚动）
    /// ┌──────────────────────────────────────────────────────────────┐
    /// │ 源通道（待映射，n）              目标通道（备用，m）           │ ← 列头（随滚动条吸顶？不吸顶，随内容滚）
    /// │ ┌─真空电磁阀────────┐          ┌─预留输出────────┐            │
    /// │ │Y000 真空电磁阀-1  │╲        │ │Y220 预留输出-145│            │
    /// │ │Y001 真空电磁阀-2 →╲═══════►│ │Y221 预留输出-146│            │ ← 贝塞尔连线 = 已配映射
    /// │ │...              │ ╱        │ │...              │            │
    /// │ └─────────────────┘╱          └─────────────────┘            │
    /// │                                                              │
    /// │ 已映射源 = 浅橙底 + 右侧徽标"→0x2009@0x00"；被占目标 = 浅灰底  │
    /// │ + 徽标"已被Y000占用"；选中源 = 浅蓝底 + 蓝框；悬停 = 高亮框   │
    /// └──────────────────────────────────────────────────────────────┘
    /// 【交互】（用户评审结论：点选连线，触屏可点；拖拽以后再加）
    ///   1. 左键点左侧源节点 → 选中（浅蓝），底部状态条提示"再点右侧目标完成连线"；
    ///   2. 选中源后再点右侧目标节点 → 触发 MappingProposed（校验+落盘由宿主窗做，
    ///      本控件不管配置只管画，方便以后嵌到别处）；
    ///   3. 左键点连线 → 选中该映射（变红），右键可"删除此映射"；
    ///   4. 点空白处 / 重复点已选中源 → 取消选择；
    ///   5. 滚轮=以鼠标为中心缩放（0.5~2.5，宿主窗 IMessageFilter 预过滤，
    ///      悬停即缩不用抢焦点；缩放并进布局基准，字与框同比例，命中自动跟）；
    ///   6. 按住中键=拖动画布（AutoScroll 平移，松手复位光标）；
    ///      双击中键=复位视图（100% + 回顶）。
    /// 【通用性设计】
    ///   - 数据全外置：SetData(源池, 目标池, 映射表) 灌入，池子由 IoRemapCatalog 按配置算，
    ///     本控件不认 0x2009、不认 72 路，改总数/换耦合器自动适应；
    ///   - 占用徽标由映射表现算（目标独占拦截是 Validator 的事，画出来是本控件的事）；
    ///   - 映射中"有一端被筛选掉"的线画不出（两端节点必须同时可见），宿主窗用映射列表
    ///     兜底展示，不丢数据（切池/切分组只是"没画"，映射还在）。
    /// 【高 DPI】自绘坐标按 e.Graphics.DpiX / 96 缩放（字体用 pt 天然跟 DPI，不用手算；
    /// 矩形/行高/线宽手乘，见 WorkstationGridView 约定）；命中检测与绘制用同一套布局，
    /// 不会"看着对点着偏"。
    /// </summary>
    public class IoRemapGraphControl : UserControl
    {
        // ===================== 公开事件 =====================

        /// <summary>选中变化（源/映射任一变化即触发，宿主窗用它刷状态条与删除按钮）</summary>
        public event EventHandler SelectionChanged;

        /// <summary>
        /// 连线提议（源已选 + 点了目标）：宿主窗调 IoRemapValidator 校验，
        /// 合法则加入映射表后重新 SetData；本控件不直接改表。
        /// </summary>
        public event Action<int, int, int, int> MappingProposed;

        /// <summary>删除映射请求（右键点连线/已映射源 → 删除此映射）</summary>
        public event Action<IoOutputChannelRemap> MappingDeleteRequested;

        // ===================== 行布局模型 =====================

        /// <summary>画布上一行：分组标题 或 端点节点（左右列各有一套）</summary>
        private sealed class Row
        {
            public bool IsHeader;
            public string HeaderText;
            public IoRemapEndpoint Endpoint;
            public Rectangle Bounds;

            /// <summary>
            /// 装框后的正文（FitRowTexts 按"正文+徽标≤框宽"预截断，末尾加…；
            /// 绘制时直接用，不再量字——量字放布局态，Paint 只管画才跟手）。
            /// </summary>
            public string MainText;

            /// <summary>
            /// 徽标（源="→0x2009@0x00" / 目标="已被Y000占用" / 空闲=null；
            /// 同 MainText 在布局态一次算好，Paint 不扫映射表）。
            /// </summary>
            public string Badge;
        }

        /// <summary>
        /// 自绘画布。
        /// TextRenderer 走 GDI，在离屏缓冲上每处约 2.2ms
        /// （AGENTS 的 V1.57.3 血泪：离屏大图上 2247ms，屏幕 DC 上近 0ms）。
        /// 本画布可见区 ~25 行 × 2 处文字，双缓冲下每帧 100ms+，滚快了"字出不来"还拖影
        /// （看着像错位）。改直画屏幕 DC（近 0ms）+ OnPaint 自填白底（不闪）+ 可见区裁剪，
        /// GDI+ 的贝塞尔直画同样快，无需预渲染（预渲染是另一条红线，同样禁止）。
        /// </summary>
        private sealed class CanvasPanel : Panel
        {
            public CanvasPanel()
            {
                DoubleBuffered = false;
                ResizeRedraw = true;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
                // 双击中键复位视图：Panel 默认没有 StandardDoubleClick，
                // WM_MBUTTONDBLCLK 会走成 MouseDown（还会把 _panning 卡成 true），
                // 必须显式打开，双击事件才出得来。
                // 底色显式锁白（不跟随 Control 默认灰，节点白底+灰字在灰底上发闷；
                // 深色主题由 ThemeManager 运行时覆盖，vClip 填充走 _canvas.BackColor，永远同底）
                BackColor = Color.White;
            }

            /// <summary>背景由 OnPaint 按裁剪区自填白底，不走默认擦除（擦一遍画一遍=闪）</summary>
            protected override void OnPaintBackground(PaintEventArgs e)
            {
            }
        }

        private readonly CanvasPanel _canvas;
        private readonly ToolTip _tip;

        private List<IoRemapEndpoint> _sources = new List<IoRemapEndpoint>();
        private List<IoRemapEndpoint> _targets = new List<IoRemapEndpoint>();
        private List<IoOutputChannelRemap> _mappings = new List<IoOutputChannelRemap>();

        private readonly List<Row> _srcRows = new List<Row>();
        private readonly List<Row> _tgtRows = new List<Row>();

        /// <summary>
        /// 布局所用 DPI 缩放（错位根因：以前 ComputeLayout 在 SetData 时按
        /// "有无句柄"猜缩放（构造时常为 1.0），而 Paint 里 pt 字体按真实 DPI 放大，
        /// 两套基准打架 → 字比框大、加载即错位。现布局只在 Paint 里按 e.Graphics.DpiX
        /// 现算（EnsureLayout），Bounds 与字体永远同基准，不会再错位）。
        /// </summary>
        private float _layoutScale;

        /// <summary>布局脏标记（数据/宽度变化后置位，Paint 里重算；平时直接复用，不重复算）</summary>
        private bool _layoutDirty = true;

        /// <summary>
        /// 缩放倍率（滚轮缩放 + 中键拖动画布，AutoCAD 手感，抄 FlowCanvas 口径）。
        /// 缩放直接并进布局基准（s = DPI × _zoom）：Metrics/行 Bounds/命中全是 s 的函数，
        /// 缩放=标脏重排，不碰 Graphics 变换矩阵（V1.81.3 红线）；字体按 zoom 重建
        /// （FlowCanvas 同款下限 6pt），量字与绘制同字体，口径一致。
        /// </summary>
        private float _zoom = 1f;
        private const float ZoomMin = 0.5f;
        private const float ZoomMax = 2.5f;

        /// <summary>中键拖动画布状态（down 记起点，move 改 AutoScrollPosition，up 松开）</summary>
        private bool _panning;
        private Point _panStartMouse;
        private Point _panStartScroll;

        /// <summary>上次中键按下（时刻+位置，双击复位判定用）</summary>
        private DateTime _midLastDown = DateTime.MinValue;
        private Point _midLastPos;

        /// <summary>列头文案缓存（随 SetData 更新，Paint 里不拼字符串）</summary>
        private string _headSrc = "源通道";

        /// <summary>列头文案缓存（同上）</summary>
        private string _headTgt = "目标通道";

        /// <summary>当前布局度量（与 _srcRows/_tgtRows 同一次算出，绘制与命中共用）</summary>
        private Metrics _metrics;

        /// <summary>选中的源键（"寄存器:通道"，如 "8192:0"；null = 未选）</summary>
        private string _selSrcKey;

        /// <summary>选中的目标键（仅高亮用，不触发连线；连线必须先选源）</summary>
        private string _selTgtKey;

        /// <summary>选中的映射（存源键+目标键，SetData 换表后仍能对上）</summary>
        private string _selMapSrcKey;
        private string _selMapDstKey;

        private Row _hoverRow;
        private int _hoverLineIndex = -1;
        private string _lastTipText = "";

        // 右键菜单命中（按下右键瞬间记下，避免 Opening 时再算一遍坐标）
        private int _ctxLineIndex = -1;
        private Row _ctxRow;
        private readonly ContextMenuStrip _lineMenu;
        private readonly ToolStripMenuItem _miDeleteMap;

        /// <summary>节点字体（随缩放重建，见 RebuildFonts；Dispose 照旧释放）</summary>
        private Font _fontNode;
        /// <summary>列头字体（同上）</summary>
        private Font _fontHead;
        /// <summary>徽标字体（同上）</summary>
        private Font _fontBadge;

        /// <summary>按当前 zoom 重建三套字体（FlowCanvas 同款：9/10/8pt × zoom，下限 6pt）</summary>
        private void RebuildFonts()
        {
            try { if (_fontNode != null) _fontNode.Dispose(); } catch { }
            try { if (_fontHead != null) _fontHead.Dispose(); } catch { }
            try { if (_fontBadge != null) _fontBadge.Dispose(); } catch { }
            _fontNode = new Font("微软雅黑", Math.Max(6f, 9f * _zoom));
            _fontHead = new Font("微软雅黑", Math.Max(6f, 10f * _zoom), FontStyle.Bold);
            _fontBadge = new Font("微软雅黑", Math.Max(6f, 8f * _zoom));
        }

        /// <summary>构造：建画布 + 提示 + 右键菜单并挂事件</summary>
        public IoRemapGraphControl()
        {
            _canvas = new CanvasPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                AutoScroll = true
            };
            _canvas.Paint += Canvas_Paint;
            _canvas.MouseClick += Canvas_MouseClick;
            _canvas.MouseMove += Canvas_MouseMove;
            _canvas.MouseLeave += Canvas_MouseLeave;
            _canvas.MouseDown += Canvas_MouseDown;
            _canvas.MouseUp += Canvas_MouseUp;
            _canvas.MouseDoubleClick += Canvas_MouseDoubleClick;
            // 宽度变了只标脏，真正重算等下一次 Paint（那时才有准的 DPI），防反复 CreateGraphics
            _canvas.SizeChanged += (s, e) => { _layoutDirty = true; _canvas.Invalidate(); };
            RebuildFonts();
            Controls.Add(_canvas);

            // 悬停提示（无容器手写 Dispose，见下方 Dispose(bool)）
            _tip = new ToolTip();

            // 右键菜单：只在"点中连线/已映射源"时出现（Opening 里按 _ctx* 显隐）
            _lineMenu = new ContextMenuStrip();
            _miDeleteMap = new ToolStripMenuItem("删除此映射");
            _miDeleteMap.Click += (s, e) => RaiseDeleteFromContext();
            _lineMenu.Items.Add(_miDeleteMap);
        }

        /// <summary>
        /// 释放自建资源（画布是子控件由基类释放；tip/菜单/字体无容器，必须手放，
        /// 否则 Sunny/原生输入框类资源进终结器线程，见 ControlDisposeHelper 头注释）。
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { if (_tip != null) _tip.Dispose(); } catch { }
                try { if (_lineMenu != null) _lineMenu.Dispose(); } catch { }
                try { if (_fontNode != null) _fontNode.Dispose(); } catch { }
                try { if (_fontHead != null) _fontHead.Dispose(); } catch { }
                try { if (_fontBadge != null) _fontBadge.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        // ===================== 公开 API =====================

        /// <summary>
        /// 灌入数据并重画（宿主窗在池切换/分组筛选/映射增删后调用；选中态中"已不存在"的自动清除）。
        /// 三个集合可为 null（视为无），内部做拷贝，调用方改自己的表不影响已显示。
        /// </summary>
        public void SetData(List<IoRemapEndpoint> sources, List<IoRemapEndpoint> targets,
            List<IoOutputChannelRemap> mappings)
        {
            _sources = sources != null ? new List<IoRemapEndpoint>(sources) : new List<IoRemapEndpoint>();
            _targets = targets != null ? new List<IoRemapEndpoint>(targets) : new List<IoRemapEndpoint>();
            _mappings = mappings != null ? new List<IoOutputChannelRemap>(mappings) : new List<IoOutputChannelRemap>();

            // 选中态自愈：源/映射已不在新数据里就清除，不留"选中了个不存在的东西"
            if (_selSrcKey != null && FindEndpoint(_sources, _selSrcKey) == null) _selSrcKey = null;
            if (_selTgtKey != null && FindEndpoint(_targets, _selTgtKey) == null) _selTgtKey = null;
            if (_selMapSrcKey != null && FindMapping(_selMapSrcKey, _selMapDstKey) == null)
            {
                _selMapSrcKey = null;
                _selMapDstKey = null;
            }
            _hoverRow = null;
            _hoverLineIndex = -1;

            // 列头文案随数据走（Paint 只读，逐帧 string.Format 是浪费）
            _headSrc = string.Format("源通道（待映射，{0}）", _sources.Count);
            _headTgt = string.Format("目标通道（备用，{0}）", _targets.Count);

            // 只标脏不计算：无句柄时算出的缩放是猜的（错位根因），等 Paint 用真 DPI 算
            _layoutDirty = true;
            _canvas.Invalidate();
        }

        /// <summary>
        /// 预选源（右键"映射到备用通道…"进来时调）：选中并滚到该节点；找不到返回 false。
        /// </summary>
        public bool PreselectSource(int reg, int ch)
        {
            string key = reg + ":" + ch;
            if (FindEndpoint(_sources, key) == null) return false;
            _selSrcKey = key;
            _selTgtKey = null;
            _selMapSrcKey = null;
            _selMapDstKey = null;
            EnsureLayoutForScroll();
            ScrollToSource(key);
            _canvas.Invalidate();
            RaiseSelectionChanged();
            return true;
        }

        /// <summary>清空全部选中（连线成功后宿主窗可调，视觉回到"一张白纸"）</summary>
        public void ClearSelection()
        {
            _selSrcKey = null;
            _selTgtKey = null;
            _selMapSrcKey = null;
            _selMapDstKey = null;
            _canvas.Invalidate();
            RaiseSelectionChanged();
        }

        /// <summary>当前缩放倍率（宿主窗显示"缩放 n%"用；改缩放走 ZoomAtCursor/ResetView）</summary>
        public float ZoomFactor
        {
            get { return _zoom; }
        }

        /// <summary>
        /// 滚轮缩放（以鼠标为中心；宿主窗经 IMessageFilter 预过滤后调这里，FlowCanvas 同款）。
        /// steps>0 放大（每步 ×1.2），<0 缩小；缩前鼠标下的内容点，缩后仍在鼠标下。
        /// </summary>
        public void ZoomAtCursor(int steps)
        {
            if (steps == 0) return;
            if (_canvas == null || _canvas.IsDisposed) return;
            float next = ZoomFactorOf(_zoom, steps);
            if (Math.Abs(next - _zoom) < 0.001f) return;
            double ratio = (double)next / (double)_zoom;
            _zoom = next;
            RebuildFonts();
            _layoutDirty = true;
            EnsureLayoutForScroll();
            // 鼠标锚定（_canvas 无句柄等极端情况拿不到光标：缩放已生效，只丢锚定，不报错）
            try
            {
                Point mouse = _canvas.PointToClient(Cursor.Position);
                int sxNew = ZoomScrollOf(mouse.X, -_canvas.AutoScrollPosition.X, ratio);
                int syNew = ZoomScrollOf(mouse.Y, -_canvas.AutoScrollPosition.Y, ratio);
                // setter 取正值（getter 是负偏移，符号坑见 ScrollToSource）
                _canvas.AutoScrollPosition = new Point(sxNew, syNew);
            }
            catch { }
            _canvas.Invalidate();
        }

        /// <summary>复位视图（100% + 回顶；双击中键调这里）</summary>
        public void ResetView()
        {
            _zoom = 1f;
            RebuildFonts();
            _layoutDirty = true;
            try
            {
                if (_canvas != null && !_canvas.IsDisposed)
                    _canvas.AutoScrollPosition = new Point(0, 0);
            }
            catch { }
            try { if (_canvas != null && !_canvas.IsDisposed) _canvas.Invalidate(); }
            catch { }
        }

        /// <summary>缩放步进（纯函数：old × 1.2^steps，再钳制到 [ZoomMin, ZoomMax]）</summary>
        private static float ZoomFactorOf(float old, int steps)
        {
            float next = old * (float)Math.Pow(1.2, steps);
            return ClampZoom(next);
        }

        /// <summary>缩放钳制（纯函数，0.5~2.5：列表画布字小了没法看、放太大一行占满屏）</summary>
        private static float ClampZoom(float v)
        {
            if (v < ZoomMin) return ZoomMin;
            if (v > ZoomMax) return ZoomMax;
            return v;
        }

        /// <summary>
        /// 缩后滚动偏移（纯函数，正值口径）：缩前鼠标下的内容点（mouseClient + scrollOffset）
        /// 按 ratio 放缩后仍落在鼠标下；WinForms 上限自动钳，这里只保下限不为负。
        /// </summary>
        private static int ZoomScrollOf(int mouseClient, int scrollOffset, double ratio)
        {
            long v = (long)((mouseClient + scrollOffset) * ratio - mouseClient);
            if (v < 0) return 0;
            if (v > 1000000) return 1000000;
            return (int)v;
        }

        /// <summary>
        /// 按映射选中连线（宿主窗的映射清单点一行 → 画布对应连线变红；
        /// 映射两端被筛选藏起时选中记下但画不出线，返回 false 告诉调用方）。
        /// </summary>
        public bool SelectMapping(int srcReg, int srcCh, int dstReg, int dstCh)
        {
            _selMapSrcKey = srcReg + ":" + srcCh;
            _selMapDstKey = dstReg + ":" + dstCh;
            _selSrcKey = null;
            _selTgtKey = null;
            _canvas.Invalidate();
            RaiseSelectionChanged();
            return FindMapping(_selMapSrcKey, _selMapDstKey) != null
                && FindRow(_srcRows, _selMapSrcKey) != null
                && FindRow(_tgtRows, _selMapDstKey) != null;
        }

        /// <summary>当前选中的源端点（未选返回 null）</summary>
        public IoRemapEndpoint SelectedSource
        {
            get { return _selSrcKey != null ? FindEndpoint(_sources, _selSrcKey) : null; }
        }

        /// <summary>当前选中的映射（未选返回 null）</summary>
        public IoRemapEndpoint SelectedTarget
        {
            get { return _selTgtKey != null ? FindEndpoint(_targets, _selTgtKey) : null; }
        }

        /// <summary>当前选中的映射（点连线选中；未选返回 null）</summary>
        public IoOutputChannelRemap SelectedMapping
        {
            get
            {
                if (_selMapSrcKey == null) return null;
                return FindMapping(_selMapSrcKey, _selMapDstKey);
            }
        }

        // ===================== 布局（绘制与命中同一套） =====================

        /// <summary>布局度量（DPI 缩放后）：列宽/行高/边距，绘制与命中检测共用</summary>
        private struct Metrics
        {
            public int LeftX, LeftW, RightX, RightW, HeadH, NodeH, Gap, GroupH, Margin;
            public int VirtualW;
        }

        /// <summary>
        /// 保证布局与传入 Graphics 同基准（Paint 入口调；缩放没变且不脏直接返回，零开销）。
        /// </summary>
        private void EnsureLayout(Graphics g)
        {
            // 缩放并进布局基准：DPI 归一（<1 按 1）之后再乘 zoom，
            // 缩小到 0.5 时 s=0.5 必须活下来（以前 s<1 一律按 1 会把缩小吃掉）。
            float d = (g != null) ? g.DpiX / 96f : 1f;
            if (d < 1f) d = 1f;
            float s = d * _zoom;
            if (!_layoutDirty && s == _layoutScale) return;
            ComputeLayout(s);
            _layoutScale = s;
            _layoutDirty = false;
            // 有真 DC 才量字截断（屏幕 DC 上量字快；离屏 DC 量字慢但布局态低频，无妨）
            if (g != null)
            {
                try { FitRowTexts(g); }
                catch { }
            }
        }

        /// <summary>
        /// 非绘制路径的布局保证（预选滚动要在 Paint 之前拿到 Bounds）：
        /// 有句柄就现取真 DPI，无句柄用上次基准（Paint 时还会再对一次，不会错位）。
        /// </summary>
        private void EnsureLayoutForScroll()
        {
            if (!_layoutDirty) return;
            try
            {
                if (_canvas != null && !_canvas.IsDisposed && _canvas.IsHandleCreated)
                {
                    using (Graphics g = _canvas.CreateGraphics())
                    {
                        EnsureLayout(g);
                        return;
                    }
                }
            }
            catch { }
            ComputeLayout(_layoutScale > 0 ? _layoutScale : 1f);
            _layoutDirty = false;
        }

        /// <summary>按缩放算一套度量（96DPI 下基准：列宽 250/250，节点高 26，行隙 6）</summary>
        private Metrics BuildMetrics(float s, int clientW)
        {
            var m = new Metrics
            {
                Margin = (int)(10 * s),
                LeftX = (int)(10 * s),
                LeftW = (int)(250 * s),
                RightW = (int)(250 * s),
                HeadH = (int)(30 * s),
                NodeH = (int)(26 * s),
                Gap = (int)(6 * s),
                GroupH = (int)(22 * s)
            };
            // 右列右对齐：画布窄时按最小虚宽撑出滚动条，宽时贴右
            int minW = m.Margin * 2 + m.LeftW + (int)(120 * s) + m.RightW;
            int viewW = Math.Max(clientW, minW);
            m.VirtualW = Math.Max(viewW, minW);
            m.RightX = m.VirtualW - m.Margin - m.RightW;
            return m;
        }

        /// <summary>
        /// 按缩放算出所有行的 Bounds（虚拟坐标）并记度量 + 设 AutoScrollMinSize。
        /// 注意：绘制不挂 TranslateTransform（V1.81.3 去矩阵：滚动偏移手工加到矩形上，
        /// 见 Paint 里的 Off/ToVirtual；旧注释写 TranslateTransform 已过时，特此纠正）。
        /// 鼠标坐标转虚拟坐标见 ToVirtual。
        /// </summary>
        private void ComputeLayout(float s)
        {
            if (_canvas == null || _canvas.IsDisposed) return;
            if (s <= 0f) s = 1f;

            Metrics m = BuildMetrics(s, _canvas.ClientSize.Width);
            _metrics = m;

            BuildRows(_sources, m.LeftX, m.LeftW, m.HeadH, m.NodeH, m.Gap, m.GroupH, _srcRows);
            BuildRows(_targets, m.RightX, m.RightW, m.HeadH, m.NodeH, m.Gap, m.GroupH, _tgtRows);

            int totalH = m.HeadH + m.Margin;
            foreach (var r in _srcRows) totalH = Math.Max(totalH, r.Bounds.Bottom);
            foreach (var r in _tgtRows) totalH = Math.Max(totalH, r.Bounds.Bottom);
            totalH += m.Margin;
            try
            {
                // 宽给完整虚宽（滚动范围 = 虚宽 − 可视宽，WinForms 自己算）：
                // zoom=1 时虚宽==可视宽，无横向条（原行为）；放大后虚宽超可视，
                // 横向条自动出来，右列才滚得出来（给"超出量"是错的，横向永远出不来）。
                var want = new Size(m.VirtualW, totalH);
                if (_canvas.AutoScrollMinSize != want) _canvas.AutoScrollMinSize = want;
            }
            catch { }
        }

        /// <summary>按功能分组建行（同功能连续才合组，源池已按寄存器排序，分组自然连续）</summary>
        private static void BuildRows(List<IoRemapEndpoint> eps, int x, int w,
            int headH, int nodeH, int gap, int groupH, List<Row> rows)
        {            rows.Clear();
            int y = headH;
            string lastGroup = null;
            foreach (var e in eps)
            {
                if (e.GroupTitle != lastGroup)
                {
                    lastGroup = e.GroupTitle;
                    rows.Add(new Row
                    {
                        IsHeader = true,
                        HeaderText = lastGroup,
                        Bounds = new Rectangle(x, y, w, groupH)
                    });
                    y += groupH + gap;
                }
                rows.Add(new Row
                {
                    IsHeader = false,
                    Endpoint = e,
                    // 默认全文；FitRowTexts 在有 DC 时按框宽截断（无 DC 就显示全文，不断言）
                    MainText = e != null ? e.DisplayText : "",
                    Bounds = new Rectangle(x, y, w, nodeH)
                });
                y += nodeH + gap;
            }
        }

        /// <summary>
        /// 按框宽预截断每行正文（布局态做，Paint 里不再量字）：
        /// 可用宽 = 框宽 - 左右各 6 -（徽标宽 + 6，如有徽标）；超了末尾加…。
        /// 徽标用 8pt 量、正文用 9pt 量（与绘制同字体，量出来即画出来，
        /// 不会"量着装得下、画着溢出去"）。
        /// </summary>
        private void FitRowTexts(Graphics g)
        {
            FitRows(g, _srcRows, true);
            FitRows(g, _tgtRows, false);
        }

        private void FitRows(Graphics g, List<Row> rows, bool isSource)
        {
            foreach (var r in rows)
            {
                if (r.IsHeader || r.Endpoint == null) continue;
                string badge = isSource ? SourceBadge(r.Endpoint) : TargetBadge(r.Endpoint);
                r.Badge = badge;
                int avail = r.Bounds.Width - 12;
                if (badge != null)
                {
                    try { avail -= (TextRenderer.MeasureText(g, badge, _fontBadge).Width + 6); }
                    catch { avail -= 90; }
                }
                string full = r.Endpoint.DisplayText ?? "";
                int fullW;
                try { fullW = TextRenderer.MeasureText(g, full, _fontNode).Width; }
                catch { r.MainText = full; continue; }
                r.MainText = (fullW <= avail) ? full : Ellipsize(g, full, _fontNode, avail);
            }
        }

        /// <summary>截断加…（二分找最长前缀，保证结果整体≤maxWidth；框太窄保底只留"…"）</summary>
        private static string Ellipsize(IDeviceContext dc, string text, Font font, int maxWidth)
        {
            const string dots = "…";
            if (string.IsNullOrEmpty(text)) return "";
            if (maxWidth <= 0) return "";
            int dotW;
            try { dotW = TextRenderer.MeasureText(dc, dots, font).Width; }
            catch { return text; }
            if (dotW >= maxWidth) return dots;
            int lo = 0, hi = text.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                int w;
                try { w = TextRenderer.MeasureText(dc, text.Substring(0, mid), font).Width; }
                catch { break; }
                if (w + dotW <= maxWidth) lo = mid;
                else hi = mid - 1;
            }
            if (lo <= 0) return dots;
            if (lo >= text.Length) return text;
            return text.Substring(0, lo) + dots;
        }

        // ===================== 绘制 =====================

        private void Canvas_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            // 布局与绘制同基准 + 脏了就地重算（错位根因见 _layoutScale 注释）
            EnsureLayout(g);
            Metrics m = _metrics;

            // 手工滚动偏移，不调 TranslateTransform：TextRenderer 走 GDI，
            // 对 Graphics 变换的响应在不同驱动/DC 下不一致（V1.51 在 Scale 上栽过，
            // 位移也不值得赌），全部画设备坐标；命中检测继续用虚拟坐标，
            // 两边经 Off（画）/ ToVirtual（鼠标/裁剪）换算，口径一致。
            int ox = _canvas.AutoScrollPosition.X;
            int oy = _canvas.AutoScrollPosition.Y;

            // 客户裁剪（设备坐标，直接填）与虚拟裁剪（行列取舍）各取所需
            Rectangle clip = e.ClipRectangle;
            Rectangle vClip = ToVirtual(clip);

            // 自填底（OnPaintBackground 已禁默认擦除，这里按客户裁剪填一次，不闪也不留残影）
            using (var bg = new SolidBrush(_canvas.BackColor))
            {
                g.FillRectangle(bg, clip);
            }

            // 列头
            DrawHead(g, Off(new Rectangle(m.LeftX, m.Margin, m.LeftW, m.HeadH - m.Margin), ox, oy), _headSrc);
            DrawHead(g, Off(new Rectangle(m.RightX, m.Margin, m.RightW, m.HeadH - m.Margin), ox, oy), _headTgt);

            // 空态
            if (_srcRows.Count == 0)
                DrawEmpty(g, Off(new Rectangle(m.LeftX, 40, m.LeftW, 30), ox, oy), "无源通道（请检查总数配置）");
            if (_tgtRows.Count == 0)
                DrawEmpty(g, Off(new Rectangle(m.RightX, 40, m.RightW, 30), ox, oy), "无备用目标（预留点已用完可切“全部空闲”）");

            // 连线画在节点下层（先画线后画节点，横穿时不断字；选中红线照样从节点边缘露出）
            // 两端节点必须同时可见才画；被筛选掉的不画，宿主窗列表兜底。
            // 抗锯齿只开在线段上（GDI 文字不受影响，矩形边框保持锐利）。
            var savedMode = g.SmoothingMode;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            try
            {
                for (int i = 0; i < _mappings.Count; i++)
                {
                    var map = _mappings[i];
                    if (map == null) continue;
                    Row s = FindRow(_srcRows, map.SourceRegister + ":" + map.SourceChannel);
                    Row t = FindRow(_tgtRows, map.TargetRegister + ":" + map.TargetChannel);
                    if (s == null || t == null) continue;
                    // 包围盒判交（禁只看两端点）：中段穿过可见区、两端都在屏外的长连线也必须画，
                    // 否则滚屏后新露出的条带只有白底没有线 → 线段断连（V1.81.4 血泪）。
                    if (!LinkCrossesClip(s.Bounds, t.Bounds, vClip)) continue;
                    bool selected = (map.SourceRegister + ":" + map.SourceChannel == _selMapSrcKey)
                        && (map.TargetRegister + ":" + map.TargetChannel == _selMapDstKey);
                    bool hovered = (i == _hoverLineIndex);
                    // 箭头属于目标端：目标滚出去了就不画箭头，只画穿过可见区的线段
                    bool drawArrow = t.Bounds.IntersectsWith(vClip);
                    DrawLink(g, Off(s.Bounds, ox, oy), Off(t.Bounds, ox, oy), selected, hovered, drawArrow);
                }

                // 预连线（源已选 + 悬停在目标上：灰色虚线"预告"点下去会连到哪）
                if (_selSrcKey != null && _hoverRow != null && !_hoverRow.IsHeader
                    && IsTargetRow(_hoverRow))
                {
                    Row s = FindRow(_srcRows, _selSrcKey);
                    if (s != null)
                    {
                        float pw = 1.5f * _zoom;
                        if (pw < 1f) pw = 1f;
                        using (var pen = new Pen(Color.Gray, pw))
                        {
                            pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                            DrawBezier(g, pen, Off(s.Bounds, ox, oy), Off(_hoverRow.Bounds, ox, oy));
                        }
                    }
                }
            }
            finally
            {
                g.SmoothingMode = savedMode;
            }

            foreach (var r in _srcRows)
            {
                if (r.Bounds.IntersectsWith(vClip)) DrawRow(g, r, Off(r.Bounds, ox, oy), true);
            }
            foreach (var r in _tgtRows)
            {
                if (r.Bounds.IntersectsWith(vClip)) DrawRow(g, r, Off(r.Bounds, ox, oy), false);
            }
        }

        /// <summary>虚拟矩形 → 设备矩形（加滚动偏移；Paint 里所有绘制都走这里，不碰变换矩阵）</summary>
        private static Rectangle Off(Rectangle virtualRect, int ox, int oy)
        {
            virtualRect.Offset(ox, oy);
            return virtualRect;
        }

        private void DrawHead(Graphics g, Rectangle dev, string text)
        {
            using (var bg = new SolidBrush(Color.FromArgb(237, 243, 253)))
                g.FillRectangle(bg, dev);
            TextRenderer.DrawText(g, text, _fontHead, dev,
                Color.FromArgb(30, 80, 160),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        private void DrawEmpty(Graphics g, Rectangle dev, string text)
        {
            TextRenderer.DrawText(g, text, _fontNode, dev, Color.Gray,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        }

        /// <summary>画一行（分组条 / 端点节点，徽标规则见类头注释；bounds 已是设备坐标）</summary>
        private void DrawRow(Graphics g, Row r, Rectangle dev, bool isSource)
        {
            if (r.IsHeader)
            {
                using (var bg = new SolidBrush(Color.FromArgb(245, 245, 245)))
                    g.FillRectangle(bg, dev);
                TextRenderer.DrawText(g, r.HeaderText, _fontHead, dev, Color.DimGray,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                return;
            }

            var e = r.Endpoint;
            string key = e.Key;
            bool selected = isSource ? (key == _selSrcKey) : (key == _selTgtKey);
            bool hovered = (r == _hoverRow);
            // 徽标布局态已算好（r.Badge），Paint 不扫映射表、不拼串
            string badge = r.Badge;

            Color fill = Color.White;
            Color border = Color.FromArgb(180, 180, 180);
            if (isSource && badge != null) { fill = Color.FromArgb(255, 247, 230); border = Color.FromArgb(220, 155, 40); }
            if (!isSource && badge != null) { fill = Color.FromArgb(242, 242, 242); border = Color.FromArgb(200, 90, 90); }
            if (selected) { fill = Color.FromArgb(232, 241, 255); border = Color.FromArgb(48, 112, 238); }

            using (var bg = new SolidBrush(fill))
                g.FillRectangle(bg, dev);
            float bw = (selected || hovered) ? 2f : 1f;
            Color bc = hovered ? Color.FromArgb(48, 112, 238) : border;
            using (var pen = new Pen(bc, bw))
                g.DrawRectangle(pen, dev);

            // 正文左对齐，徽标右对齐（徽标 null 就是空闲端点，不画；
            // 正文是布局态截好的 MainText，绘制宽度必进框，不会压徽标/出框）
            var textRc = new Rectangle(dev.X + 6, dev.Y, dev.Width - 12, dev.Height);
            Color tc = (isSource && badge != null) ? Color.FromArgb(120, 80, 10) : Color.FromArgb(48, 48, 48);
            TextRenderer.DrawText(g, r.MainText ?? e.DisplayText, _fontNode, textRc, tc,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            if (badge != null)
            {
                TextRenderer.DrawText(g, badge, _fontBadge, textRc, Color.FromArgb(150, 90, 90),
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }
        }

        /// <summary>源徽标：已映射返回"→0x2009@0x00"；未映射返回 null</summary>
        private string SourceBadge(IoRemapEndpoint e)
        {
            var map = IoRemapValidator.FindBySource(_mappings, e.Register, e.Channel);
            if (map == null) return null;
            return "→" + IoRemapValidator.Describe(map.TargetRegister, map.TargetChannel);
        }

        /// <summary>目标徽标：被占用返回"已被Y000占用"；空闲返回 null</summary>
        private string TargetBadge(IoRemapEndpoint e)
        {
            foreach (var map in _mappings)
            {
                if (map == null) continue;
                if (map.TargetRegister == e.Register && map.TargetChannel == e.Channel)
                {
                    var src = FindEndpoint(_sources, map.SourceRegister + ":" + map.SourceChannel);
                    string who = src != null ? src.IoName : IoRemapValidator.Describe(map.SourceRegister, map.SourceChannel);
                    return "已被" + who + "占用";
                }
            }
            return null;
        }

        /// <summary>画一条映射连线（贝塞尔 + 目标端箭头；选中红加粗，悬停橙）</summary>
        /// <summary>
        /// 画一条映射连线（贝塞尔 + 目标端箭头；选中红加粗，悬停橙）。
        /// src/dst 已是设备坐标；drawArrow=false 时只画线（目标滚出屏，箭头无处可落）。
        /// </summary>
        private void DrawLink(Graphics g, Rectangle src, Rectangle dst, bool selected, bool hovered, bool drawArrow)
        {
            Color c = selected ? Color.Red : (hovered ? Color.Orange : Color.DodgerBlue);
            // 线宽随缩放（放再大线不毛边，缩再小线不糊成一团；下限 1px）
            float w = (selected ? 3f : 2f) * _zoom;
            if (w < 1f) w = 1f;
            using (var pen = new Pen(c, w))
            {
                DrawBezier(g, pen, src, dst);
            }
            if (!drawArrow) return;
            // 箭头（小三角，指向目标节点左边缘中点；尺寸随缩放，下限 3×2）
            int ax = Math.Max(3, (int)(8 * _zoom));
            int ay = Math.Max(2, (int)(5 * _zoom));
            Point tip = new Point(dst.Left, dst.Top + dst.Height / 2);
            Point[] tri = { tip, new Point(tip.X - ax, tip.Y - ay), new Point(tip.X - ax, tip.Y + ay) };
            using (var br = new SolidBrush(selected ? Color.Red : Color.DodgerBlue))
                g.FillPolygon(br, tri);
        }

        /// <summary>
        /// 连线包围盒判交（裁剪用，代替"两端点判交"）。
        /// 贝塞尔曲线必落在控制点包围盒内（p0/c1/c2/p3 的外包矩形），
        /// 用它判交：保守（偶尔多画几段）但绝无漏网——中段穿屏、两端屏外的长线不会被裁断。
        /// 反之只看两端点：滚屏后新露出的条带只有白底没有线，就是"线段断连"。
        /// </summary>
        /// <param name="src">源节点 bounds（虚拟坐标，与命中检测同口径）</param>
        /// <param name="dst">目标节点 bounds（虚拟坐标）</param>
        /// <param name="vClip">可见区（虚拟坐标）</param>
        /// <returns>true=曲线可能穿过可见区，必须画</returns>
        private static bool LinkCrossesClip(Rectangle src, Rectangle dst, Rectangle vClip)
        {
            // 控制点：p0=(src右,源中线) c1=(mx,源中线) c2=(mx,目标中线) p3=(dst左,目标中线)，
            // 与 DrawBezier 同口径（改一处必须改另一处）；包围盒取四点外包即可。
            int x0 = Math.Min(src.Right, dst.Left);
            int x1 = Math.Max(src.Right, dst.Left);
            int y0 = Math.Min(src.Top, dst.Top);
            int y1 = Math.Max(src.Bottom, dst.Bottom);
            return x1 >= vClip.Left && x0 <= vClip.Right && y1 >= vClip.Top && y0 <= vClip.Bottom;
        }

        private static void DrawBezier(Graphics g, Pen pen, Rectangle src, Rectangle dst)
        {
            Point p0 = new Point(src.Right, src.Top + src.Height / 2);
            Point p3 = new Point(dst.Left, dst.Top + dst.Height / 2);
            int mx = (p0.X + p3.X) / 2;
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path.AddBezier(p0, new Point(mx, p0.Y), new Point(mx, p3.Y), p3);
                g.DrawPath(pen, path);
            }
        }

        // ===================== 鼠标交互 =====================

        /// <summary>客户区坐标 → 虚拟坐标（AutoScrollPosition 是负偏移，反向加回）</summary>
        private Point ToVirtual(Point client)
        {
            return new Point(client.X - _canvas.AutoScrollPosition.X, client.Y - _canvas.AutoScrollPosition.Y);
        }

        /// <summary>客户区矩形 → 虚拟矩形（可见区裁剪用，与上面同口径）</summary>
        private Rectangle ToVirtual(Rectangle client)
        {
            Point p = ToVirtual(new Point(client.X, client.Y));
            return new Rectangle(p.X, p.Y, client.Width, client.Height);
        }

        private void Canvas_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle) return;   // 中键只管拖画布/双击复位，不参与选择
            if (e.Button == MouseButtons.Right) return;   // 右键走 MouseDown 菜单，不参与选择
            Point v = ToVirtual(e.Location);

            Row hit = HitRow(v);
            if (hit != null && !hit.IsHeader)
            {
                if (IsSourceRow(hit))
                {
                    // 点源：重复点=取消；换源=改选；同时清掉连线选中（一次只表达一件事）
                    string key = hit.Endpoint.Key;
                    _selSrcKey = (key == _selSrcKey) ? null : key;
                    _selTgtKey = null;
                    _selMapSrcKey = null;
                    _selMapDstKey = null;
                    _canvas.Invalidate();
                    RaiseSelectionChanged();
                    return;
                }
                // 点目标：有源已选=提议连线；无源=只高亮 + 提示先选源
                if (_selSrcKey != null)
                {
                    var src = FindEndpoint(_sources, _selSrcKey);
                    if (src != null)
                    {
                        var handler = MappingProposed;
                        string keepSel = _selSrcKey;
                        _selSrcKey = null;   // 提议发出即收回选中（成败都一样，视觉不残留）
                        _canvas.Invalidate();
                        RaiseSelectionChanged();
                        if (handler != null)
                            handler(src.Register, src.Channel, hit.Endpoint.Register, hit.Endpoint.Channel);
                        else
                            _selSrcKey = keepSel;   // 无人处理则恢复选中（设计器预览时）
                        return;
                    }
                }
                _selTgtKey = hit.Endpoint.Key;
                _selMapSrcKey = null;
                _selMapDstKey = null;
                _canvas.Invalidate();
                RaiseSelectionChanged();
                return;
            }

            int line = HitLine(v);
            if (line >= 0)
            {
                var map = _mappings[line];
                _selMapSrcKey = map.SourceRegister + ":" + map.SourceChannel;
                _selMapDstKey = map.TargetRegister + ":" + map.TargetChannel;
                _selSrcKey = null;
                _selTgtKey = null;
                _canvas.Invalidate();
                RaiseSelectionChanged();
                return;
            }

            // 点空白：全清
            if (_selSrcKey != null || _selTgtKey != null || _selMapSrcKey != null)
            {
                ClearSelection();
            }
        }

        private void Canvas_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
            {
                // 中键双击=复位视图（自己按系统双击口径判定，不依赖 DoubleClick 消息：
                // Panel 的双击风格对中键不可靠，实测 WM_MBUTTONDBLCLK 会走成 MouseDown
                // 还把 _panning 卡住——V1.82 探针实锤；手动判定最稳）。
                DateTime now = DateTime.UtcNow;
                if (_midLastDown != DateTime.MinValue
                    && (now - _midLastDown).TotalMilliseconds <= SystemInformation.DoubleClickTime
                    && Math.Abs(e.X - _midLastPos.X) <= SystemInformation.DoubleClickSize.Width
                    && Math.Abs(e.Y - _midLastPos.Y) <= SystemInformation.DoubleClickSize.Height)
                {
                    _midLastDown = DateTime.MinValue;
                    _panning = false;
                    try { _canvas.Capture = false; } catch { }
                    try { _canvas.Cursor = Cursors.Default; } catch { }
                    ResetView();
                    return;
                }
                _midLastDown = now;
                _midLastPos = e.Location;
                // 中键拖动画布（FlowCanvas 同款手感）：记正偏移起点，move 里跟量走
                _panning = true;
                _panStartMouse = e.Location;
                _panStartScroll = new Point(-_canvas.AutoScrollPosition.X, -_canvas.AutoScrollPosition.Y);
                try { _canvas.Capture = true; } catch { }
                try { _canvas.Cursor = Cursors.SizeAll; } catch { }
                return;
            }
            if (e.Button != MouseButtons.Right) return;
            Point v = ToVirtual(e.Location);
            _ctxLineIndex = HitLine(v);
            _ctxRow = HitRow(v);
            bool canDelete = false;
            if (_ctxLineIndex >= 0) canDelete = true;
            else if (_ctxRow != null && !_ctxRow.IsHeader && IsSourceRow(_ctxRow)
                && IoRemapValidator.FindBySource(_mappings,
                    _ctxRow.Endpoint.Register, _ctxRow.Endpoint.Channel) != null)
                canDelete = true;
            _miDeleteMap.Visible = canDelete;
            if (canDelete)
            {
                try { _lineMenu.Show(_canvas, e.Location); } catch { }
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_panning)
            {
                // 拖画布中：滚动跟鼠标反向等量（setter 取正值）；拖时不算悬停，松手再恢复
                try
                {
                    _canvas.AutoScrollPosition = new Point(
                        Math.Max(0, _panStartScroll.X + (_panStartMouse.X - e.Location.X)),
                        Math.Max(0, _panStartScroll.Y + (_panStartMouse.Y - e.Location.Y)));
                }
                catch { }
                return;
            }
            Point v = ToVirtual(e.Location);
            Row hit = HitRow(v);
            int line = (hit == null) ? HitLine(v) : -1;   // 节点优先，空白处才算连线
            if (hit != _hoverRow || line != _hoverLineIndex)
            {
                bool borderOnly = (hit == _hoverRow) && (line != _hoverLineIndex);
                _hoverRow = hit;
                _hoverLineIndex = line;
                _canvas.Invalidate();   // 全重画最稳（节点<300，连线<64，无性能压力）
                UpdateTip();
            }
            try
            {
                _canvas.Cursor = (hit != null && !hit.IsHeader) || line >= 0
                    ? Cursors.Hand : Cursors.Default;
            }
            catch { }
        }

        private void Canvas_MouseLeave(object sender, EventArgs e)
        {
            _hoverRow = null;
            _hoverLineIndex = -1;
            UpdateTip();
            _canvas.Invalidate();
        }

        private void Canvas_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Middle || !_panning) return;
            _panning = false;
            try { _canvas.Capture = false; } catch { }
            try { _canvas.Cursor = Cursors.Default; } catch { }
        }

        private void Canvas_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            // 双击中键=复位视图（AutoCAD 双击中键是范围缩放；列表画布取 100%+回顶）
            if (e.Button != MouseButtons.Middle) return;
            ResetView();
        }

        /// <summary>悬停提示内容（节点全称 / 连线两端 / 空白清掉）</summary>
        private void UpdateTip()
        {
            string text = "";
            if (_hoverRow != null && !_hoverRow.IsHeader && _hoverRow.Endpoint != null)
            {
                var ep = _hoverRow.Endpoint;
                text = IsSourceRow(_hoverRow)
                    ? ep.FullText + "（左键选中，再点右侧目标连线）"
                    : ep.FullText + "（先在左侧选源，再点我完成连线）";
            }
            else if (_hoverLineIndex >= 0 && _hoverLineIndex < _mappings.Count)
            {
                var map = _mappings[_hoverLineIndex];
                if (map != null)
                    text = string.Format("{0} → {1}（左键选中，右键可删除）",
                        IoRemapValidator.Describe(map.SourceRegister, map.SourceChannel),
                        IoRemapValidator.Describe(map.TargetRegister, map.TargetChannel));
            }
            if (text != _lastTipText)
            {
                _lastTipText = text;
                try { _tip.SetToolTip(_canvas, text); } catch { }
            }
        }

        private void RaiseDeleteFromContext()
        {
            IoOutputChannelRemap map = null;
            if (_ctxLineIndex >= 0 && _ctxLineIndex < _mappings.Count)
                map = _mappings[_ctxLineIndex];
            else if (_ctxRow != null && !_ctxRow.IsHeader && _ctxRow.Endpoint != null)
                map = IoRemapValidator.FindBySource(_mappings,
                    _ctxRow.Endpoint.Register, _ctxRow.Endpoint.Channel);
            _ctxLineIndex = -1;
            _ctxRow = null;
            if (map == null) return;
            var handler = MappingDeleteRequested;
            if (handler != null) handler(map);
        }

        private void RaiseSelectionChanged()
        {
            try
            {
                if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
            }
            catch { }
            UpdateTip();
        }

        // ===================== 命中与查找 =====================

        private bool IsSourceRow(Row r) { return _srcRows.Contains(r); }
        private bool IsTargetRow(Row r) { return _tgtRows.Contains(r); }

        private Row HitRow(Point v)
        {
            foreach (var r in _srcRows)
            {
                if (!r.IsHeader && r.Bounds.Contains(v)) return r;
            }
            foreach (var r in _tgtRows)
            {
                if (!r.IsHeader && r.Bounds.Contains(v)) return r;
            }
            return null;
        }

        /// <summary>连线命中（采样贝塞尔 24 段折线，点到折线距离 &lt;5px 即中）</summary>
        private int HitLine(Point v)
        {
            if (_mappings.Count == 0) return -1;
            // 快拒：连线只活在两列之间的竖带里（节点上的点击早被 HitRow 吃掉，
            // 左右列内部的空白点进来直接返回，不做 24 段采样；鼠标划过左列空白区最受益）
            int x0 = _metrics.LeftX + _metrics.LeftW - 12;
            int x1 = _metrics.RightX + 12;
            if (v.X < x0 || v.X > x1) return -1;
            for (int i = 0; i < _mappings.Count; i++)
            {
                var map = _mappings[i];
                if (map == null) continue;
                Row s = FindRow(_srcRows, map.SourceRegister + ":" + map.SourceChannel);
                Row t = FindRow(_tgtRows, map.TargetRegister + ":" + map.TargetChannel);
                if (s == null || t == null) continue;
                if (DistanceToBezier(v, s.Bounds, t.Bounds) < Math.Max(3f, 5f / _zoom)) return i;
            }
            return -1;
        }

        private static float DistanceToBezier(Point v, Rectangle src, Rectangle dst)
        {
            PointF p0 = new PointF(src.Right, src.Top + src.Height / 2f);
            PointF p3 = new PointF(dst.Left, dst.Top + dst.Height / 2f);
            float mx = (p0.X + p3.X) / 2f;
            PointF c1 = new PointF(mx, p0.Y);
            PointF c2 = new PointF(mx, p3.Y);
            float best = float.MaxValue;
            PointF prev = p0;
            for (int i = 1; i <= 24; i++)
            {
                float t = i / 24f;
                PointF cur = BezierPoint(p0, c1, c2, p3, t);
                best = Math.Min(best, DistancePointToSegment(v, prev, cur));
                prev = cur;
            }
            return best;
        }

        private static PointF BezierPoint(PointF p0, PointF c1, PointF c2, PointF p3, float t)
        {
            float u = 1 - t;
            float x = u * u * u * p0.X + 3 * u * u * t * c1.X + 3 * u * t * t * c2.X + t * t * t * p3.X;
            float y = u * u * u * p0.Y + 3 * u * u * t * c1.Y + 3 * u * t * t * c2.Y + t * t * t * p3.Y;
            return new PointF(x, y);
        }

        private static float DistancePointToSegment(Point v, PointF a, PointF b)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float len2 = dx * dx + dy * dy;
            if (len2 < 0.0001f) return (float)Math.Sqrt((v.X - a.X) * (v.X - a.X) + (v.Y - a.Y) * (v.Y - a.Y));
            float t = ((v.X - a.X) * dx + (v.Y - a.Y) * dy) / len2;
            t = Math.Max(0, Math.Min(1, t));
            float px = a.X + t * dx - v.X, py = a.Y + t * dy - v.Y;
            return (float)Math.Sqrt(px * px + py * py);
        }

        private static Row FindRow(List<Row> rows, string key)
        {
            foreach (var r in rows)
            {
                if (!r.IsHeader && r.Endpoint != null && r.Endpoint.Key == key) return r;
            }
            return null;
        }

        private void ScrollToSource(string key)
        {
            Row r = FindRow(_srcRows, key);
            if (r == null) return;
            try
            {
                // setter 取正值（getter 是负偏移，符号坑见 ProcessPolicyForm 画布注释）
                int y = Math.Max(0, r.Bounds.Y - 60);
                _canvas.AutoScrollPosition = new Point(0, y);
            }
            catch { }
        }

        private static IoRemapEndpoint FindEndpoint(List<IoRemapEndpoint> list, string key)
        {
            if (list == null || key == null) return null;
            foreach (var e in list)
            {
                if (e != null && e.Key == key) return e;
            }
            return null;
        }

        private IoOutputChannelRemap FindMapping(string srcKey, string dstKey)
        {
            if (srcKey == null || dstKey == null) return null;
            foreach (var m in _mappings)
            {
                if (m == null) continue;
                if (m.SourceRegister + ":" + m.SourceChannel == srcKey
                    && m.TargetRegister + ":" + m.TargetChannel == dstKey)
                    return m;
            }
            return null;
        }
    }
}
