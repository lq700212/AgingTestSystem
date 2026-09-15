using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AgingTestSystem.Controls;
using AgingTestSystem.Models;
using AgingTestSystem.Services;
using Sunny.UI;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 端口映射可视化配置窗（备用通道映射的连线版配置页，SunnyUI 界面）。
    /// 【界面】
    /// ┌──────────────────────────────────────────────────────────┐
    /// │ 端口映射配置（UIForm 标题栏）                              │
    /// ├──────────────────────────────────────────────────────────┤
    /// │ pnlTop：[√]启用备用通道映射  目标池[仅预留输出|v]          │
    /// │         源分组[全部|v]  lblCount（源n·目标m·已配k）        │
    /// │         lblStatus（操作指引：先点左侧源，再点右侧目标）     │
    /// ├──────────────────────────────────────────────────────────┤
    /// │ _graph（IoRemapGraphControl，Dock.Fill，两列节点+贝塞尔线）│
    /// ├──────────────────────────────────────────────────────────┤
    /// │ lblMapTitle：已配置映射（k）                               │
    /// │ lstMaps（原生 ListBox，h=96：Y000…→Y220…，与画布选择同步） │
    /// │ [删除选中][清空全部]        [保存并生效][取消]             │
    /// └──────────────────────────────────────────────────────────┘
    /// 【两种用法（同一张页，两种提交，见类头"通用性"说明）】
    ///   A. 实时模式（通讯测试窗右键/菜单进来）：保存后调用方拿 ResultMappings +
    ///      ResultEnabled 走 SettingsForm.PersistChanges 写 App.config，即时生效；
    ///   B. 草稿模式（系统设置表 IoMappingEditorPopup 里"可视化连线…"进来）：
    ///      ShowEnableSwitch=false（开关是设置表另一行的事，这里只改映射），保存后
    ///      调用方把 ResultMappings 写回单元格，等用户按设置表"保存设置"统一落盘。
    /// 本窗体自己永远不碰 App.config：落盘语义只有调用方那一份，不会两处打架。
    /// 【纯代码窗三要素】AutoScaleDimensions(6,12) + SuspendLayout 包裹 + 无参构造
    /// （设计器预览用，空快照占位；保存键加 null 守卫，见 OnSave）。
    /// 【滚轮缩放】本窗实现 IMessageFilter（抄 ProcessPolicyForm 口径）：
    /// OnShown 注册、OnFormClosed 摘除；光标悬停在画布上时滚轮=缩放并吞掉消息
    /// （不用先点画布抢焦点，顶栏改下拉时悬停回来照样缩；清单/下拉上的滚轮不受影响）。
    /// </summary>
    public class IoRemapVisualForm : UIForm, IMessageFilter
    {
        private readonly DeviceConfig _config;

        /// <summary>工作区映射（打开时的拷贝，点保存才交出去；取消=丢弃）</summary>
        private List<IoOutputChannelRemap> _workMappings = new List<IoOutputChannelRemap>();

        /// <summary>工作区开关（同上，草稿模式下隐藏，恒等于打开值）</summary>
        private bool _workEnabled;

        private List<IoRemapEndpoint> _allSources = new List<IoRemapEndpoint>();
        private List<IoRemapEndpoint> _allTargets = new List<IoRemapEndpoint>();

        private bool _binding;   // 批量刷新标记：防事件重入（改下拉→刷图→改列表循环）

        private UIPanel _pnlTop;
        private CheckBox _chkEnabled;
        private UILabel _lblPool;
        private UIComboBox _cmbPool;
        private UILabel _lblGroup;
        private UIComboBox _cmbGroup;
        private UILabel _lblCount;
        private UILabel _lblStatus;
        private IoRemapGraphControl _graph;
        private UILabel _lblMapTitle;
        private ListBox _lstMaps;
        private UIButton _btnDeleteSel;
        private UIButton _btnClearAll;
        private UIButton _btnSave;
        private UIButton _btnCancel;
        private ToolTip _tip;

        /// <summary>保存并关闭后的结果（Confirmed=false 表示用户点了取消/×，调用方直接丢弃）</summary>
        public bool Confirmed { get; private set; }

        /// <summary>结果映射表（Confirmed=true 时有效，调用方拿去落盘/写单元格）</summary>
        public List<IoOutputChannelRemap> ResultMappings { get; private set; }

        /// <summary>结果开关（同上；草稿模式恒等于打开值）</summary>
        public bool ResultEnabled { get; private set; }

        /// <summary>是否显示启用开关（草稿模式设 false，开关归设置表另一行管）</summary>
        private bool _showEnableSwitch = true;

        public bool ShowEnableSwitch
        {
            get { return _showEnableSwitch; }
            set
            {
                _showEnableSwitch = value;
                if (_chkEnabled != null) ApplyShowEnableSwitch();
            }
        }

        /// <summary>保存按钮文字（实时模式"保存并生效"，草稿模式调"确定"）</summary>
        private string _saveButtonText = "保存";

        public string SaveButtonText
        {
            get { return _saveButtonText; }
            set
            {
                _saveButtonText = string.IsNullOrEmpty(value) ? "保存" : value;
                if (_btnSave != null) _btnSave.Text = _saveButtonText;
            }
        }

        /// <summary>隐藏开关时的占位左移只搬一次（构造 + 属性各调一次 Apply，重复搬会越搬越偏）</summary>
        private bool _switchHiddenApplied;

        /// <summary>无参构造（设计器预览：空快照占位，保存键有守卫）</summary>
        public IoRemapVisualForm() : this(null)
        {
        }

        /// <summary>
        /// 构造（按配置建源/目标池 + 载入当前映射快照）。
        /// </summary>
        /// <param name="config">设备配置（建池 + 读当前映射/开关；null 则用缺省配置建池，预览/单测用）</param>
        public IoRemapVisualForm(DeviceConfig config)
        {
            _config = config ?? new DeviceConfig();
            if (config != null)
            {
                _workEnabled = config.IoBackupChannelMappingEnabled;
                if (config.IoBackupChannelMappings != null)
                    _workMappings = new List<IoOutputChannelRemap>(config.IoBackupChannelMappings);
            }

            // 高 DPI 纯代码窗三要素之二：基准 + 挂起布局（末尾 ResumeLayout）
            AutoScaleDimensions = new SizeF(6F, 12F);
            AutoScaleMode = AutoScaleMode.Font;
            SuspendLayout();

            Text = "端口映射配置（可视化连线）";
            ShowTitle = true;
            // 一律屏幕居中：调用方有主窗（通讯测试窗）也有小弹窗（设置表映射格），
            // CenterParent 会跟着小弹窗跑偏甚至出屏；用户要求不管谁打开都在屏幕正中。
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1020, 720);
            MinimumSize = new Size(860, 600);

            BuildTop();
            BuildGraph();
            BuildBottom();

            // WinForms 按 z 序从后往前布局 Dock：
            // 后面的先占边（Top/Bottom），Fill 必须在最前、最后布局，吃剩余区。
            // Controls.Add 插到最前（新加的最靠前），所以 Fill 必须最后 Add；
            // 这里三段分开建（Bottom 在 Graph 之后），显式把 _graph 挪到最前兜底——
            // 以前 BuildGraph/BuildBottom 里各调一次 BringToFront，把画布挤到后面先布局，
            // 它一把梭占满整个客户区，上下固定面板只是浮盖在上面：滚到两端内容滑进
            // 面板底下，列头和首尾行永远看不全（用户现场"上下滑动后两端被窗口挡住"）。
            // 以后往本窗加 Dock 面板：边栏正常 Add，完了再把 _graph BringToFront 一次。
            _graph.BringToFront();

            _tip = new ToolTip();
            _tip.SetToolTip(_chkEnabled, SettingsForm.WrapTooltip("总开关。关闭时下面配的映射全部不生效（读写直通原通道），适合切回正常通道排查。"));
            _tip.SetToolTip(_cmbPool, SettingsForm.WrapTooltip("右侧目标池范围。仅预留输出=只列预留 DO（默认，防误抢正常通道）；全部空闲输出=所有 DO 都可当目标（备用点用完时用，看红色“被占用”徽标别重复）。"));
            _tip.SetToolTip(_cmbGroup, SettingsForm.WrapTooltip("左侧源列表按功能分组筛选。点位太多时先分组再找，比滚动 160 行快。"));
            _tip.SetToolTip(_lstMaps, SettingsForm.WrapTooltip("已配映射清单，与上方连线一一对应。点一行，上方对应连线变红；删改在这里或右键连线都行。"));
            _tip.SetToolTip(_btnSave, SettingsForm.WrapTooltip("确认本页修改并关闭（实时模式=写 App.config 即时生效；设置表里=只写回单元格，还要点“保存设置”）。"));
            _tip.SetToolTip(_btnCancel, SettingsForm.WrapTooltip("放弃本页全部修改（打开后连的线、删的项都不算数）。"));

            ResumeLayout(false);

            ReloadPools();
            ApplyShowEnableSwitch();
            RefreshAll();
        }

        /// <summary>释放自建 ToolTip（无容器手写 Dispose，菜单/画布由各自释放）</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { if (_tip != null) _tip.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        // ===================== 建界面 =====================

        private void BuildTop()
        {
            _pnlTop = new UIPanel
            {
                Dock = DockStyle.Top,
                Height = 104
            };
            Controls.Add(_pnlTop);

            _chkEnabled = new CheckBox
            {
                Text = "启用备用通道映射",
                Location = new Point(14, 12),
                Size = new Size(190, 28),
                Font = new Font("微软雅黑", 10.5F, FontStyle.Bold),
                Checked = _workEnabled
            };
            _chkEnabled.CheckedChanged += (s, e) =>
            {
                if (_binding) return;
                _workEnabled = _chkEnabled.Checked;
                RefreshCounts();
            };
            _pnlTop.Controls.Add(_chkEnabled);

            _lblPool = new UILabel
            {
                Text = "目标池",
                Location = new Point(214, 12),
                Size = new Size(56, 28),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _pnlTop.Controls.Add(_lblPool);

            _cmbPool = new UIComboBox
            {
                DropDownStyle = UIDropDownStyle.DropDownList,
                Location = new Point(270, 12),
                Size = new Size(170, 28)
            };
            _cmbPool.Items.Add("仅预留输出");
            _cmbPool.Items.Add("全部空闲输出");
            _cmbPool.SelectedIndex = 0;
            _cmbPool.SelectedIndexChanged += (s, e) =>
            {
                if (_binding) return;
                ReloadPools();
                RefreshAll();
            };
            _pnlTop.Controls.Add(_cmbPool);

            _lblGroup = new UILabel
            {
                Text = "源分组",
                Location = new Point(452, 12),
                Size = new Size(56, 28),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _pnlTop.Controls.Add(_lblGroup);

            _cmbGroup = new UIComboBox
            {
                DropDownStyle = UIDropDownStyle.DropDownList,
                Location = new Point(508, 12),
                Size = new Size(150, 28)
            };
            _cmbGroup.Items.Add("全部");
            _cmbGroup.Items.Add(IoRemapCatalog.GroupTitleOf(IoFunction.VacuumValve));
            _cmbGroup.Items.Add(IoRemapCatalog.GroupTitleOf(IoFunction.CarrierPower));
            _cmbGroup.Items.Add(IoRemapCatalog.GroupTitleOf(IoFunction.Unknown));
            _cmbGroup.SelectedIndex = 0;
            _cmbGroup.SelectedIndexChanged += (s, e) =>
            {
                if (_binding) return;
                RefreshAll();
            };
            _pnlTop.Controls.Add(_cmbGroup);

            _lblCount = new UILabel
            {
                Location = new Point(670, 12),
                Size = new Size(336, 28),
                TextAlign = ContentAlignment.MiddleRight
            };
            _pnlTop.Controls.Add(_lblCount);

            _lblStatus = new UILabel
            {
                Location = new Point(14, 48),
                Size = new Size(992, 48),
                TextAlign = ContentAlignment.MiddleLeft
            };
            _pnlTop.Controls.Add(_lblStatus);
        }

        private void BuildGraph()
        {
            _graph = new IoRemapGraphControl
            {
                Dock = DockStyle.Fill
            };
            _graph.SelectionChanged += (s, e) => RefreshStatusAndButtons();
            _graph.MappingProposed += OnMappingProposed;
            _graph.MappingDeleteRequested += OnMappingDeleteRequested;
            Controls.Add(_graph);
            // z 序统一在构造末尾摆（见构造里 V1.82 Dock 铁律注释），这里不动，
            // 否则 Fill 先布局占满全区，上下方面板盖住画布两端。
        }

        private void BuildBottom()
        {
            var pnlBottom = new UIPanel
            {
                Dock = DockStyle.Bottom,
                Height = 176
            };
            Controls.Add(pnlBottom);
            // z 序统一在构造末尾摆（见构造里 V1.82 Dock 铁律注释），这里不动。

            _lblMapTitle = new UILabel
            {
                Text = "已配置映射（0）",
                Location = new Point(14, 6),
                Size = new Size(500, 26),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("微软雅黑", 10.5F, FontStyle.Bold)
            };
            pnlBottom.Controls.Add(_lblMapTitle);

            _lstMaps = new ListBox
            {
                Location = new Point(14, 34),
                Size = new Size(790, 88),
                Font = new Font("微软雅黑", 9.5F),
                HorizontalScrollbar = true
            };
            _lstMaps.SelectedIndexChanged += (s, e) => SyncListToGraph();
            pnlBottom.Controls.Add(_lstMaps);

            // 右列按钮（上→下：删除选中 / 清空全部 / 保存 / 取消）
            _btnDeleteSel = MakeButton("删除选中", new Point(818, 34), UIStyle.Orange);
            _btnDeleteSel.Click += (s, e) => DeleteSelectedMapping();
            pnlBottom.Controls.Add(_btnDeleteSel);

            _btnClearAll = MakeButton("清空全部", new Point(818, 72), UIStyle.Gray);
            _btnClearAll.Click += (s, e) => ClearAllMappings();
            pnlBottom.Controls.Add(_btnClearAll);

            _btnSave = MakeButton(SaveButtonText, new Point(818, 110), UIStyle.Blue);
            _btnSave.FillColor = Color.DodgerBlue;
            _btnSave.FillColor2 = Color.DodgerBlue;
            _btnSave.RectColor = Color.DodgerBlue;
            _btnSave.ForeColor = Color.White;
            _btnSave.Click += (s, e) => OnSave();
            pnlBottom.Controls.Add(_btnSave);

            _btnCancel = MakeButton("取消", new Point(918, 110), UIStyle.Gray);
            _btnCancel.Click += (s, e) => { Confirmed = false; Close(); };
            pnlBottom.Controls.Add(_btnCancel);
        }

        /// <summary>统一样式的 Sunny 按钮（保存=蓝字白底系按 V1.72.1 口径，谁改色走 ApplyButtonColors）</summary>
        private static UIButton MakeButton(string text, Point location, UIStyle style)
        {
            return new UIButton
            {
                Text = text,
                Location = location,
                Size = new Size(88, 32),
                Style = UIStyle.Custom,
                Font = new Font("微软雅黑", 10F),
                FillColor = StyleFillOf(style),
                FillColor2 = StyleFillOf(style),
                RectColor = StyleFillOf(style)
            };
        }

        private static Color StyleFillOf(UIStyle style)
        {
            switch (style)
            {
                case UIStyle.Blue: return Color.DodgerBlue;
                case UIStyle.Orange: return Color.FromArgb(220, 155, 40);
                default: return Color.FromArgb(140, 140, 140);
            }
        }

        // ===================== 数据流 =====================

        /// <summary>按当前"目标池"下拉重建目标池（源池与配置绑定，一次建成；映射不动）</summary>
        private void ReloadPools()
        {
            _allSources = IoRemapCatalog.BuildSourceEndpoints(_config);
            IoRemapTargetPool pool = _cmbPool.SelectedIndex == 1
                ? IoRemapTargetPool.AllFreeOutputs
                : IoRemapTargetPool.SpareOnly;
            _allTargets = IoRemapCatalog.BuildTargetEndpoints(_config, pool);
        }

        /// <summary>草稿模式隐藏启用开关（开关归设置表另一行管，这里只改映射，防"改了开关却没落盘"的误会）</summary>
        private void ApplyShowEnableSwitch()
        {
            if (_chkEnabled == null) return;
            _chkEnabled.Visible = _showEnableSwitch;
            if (!_showEnableSwitch && !_switchHiddenApplied)
            {
                _switchHiddenApplied = true;
                _lblPool.Location = new Point(14, 12);
                // 占位左移：开关藏了，目标池顶上（纯坐标搬家，不改逻辑）
                _cmbPool.Location = new Point(70, 12);
                _lblGroup.Location = new Point(252, 12);
                _cmbGroup.Location = new Point(308, 12);
            }
        }

        /// <summary>
        /// 外部预置映射（设置表草稿模式：把表格里"正在改还没存"的值灌进来，
        /// 避免可视化页里看不到用户刚在表格里加的行）。
        /// </summary>
        public void SetInitialMappings(string configValue)
        {
            var list = IoOutputChannelRemap.ParseAll(configValue ?? "", out _);
            _workMappings = list ?? new List<IoOutputChannelRemap>();
            RefreshAll();
        }

        /// <summary>预选源（右键"映射到备用通道…"进来：源已定，用户只需点目标；找不到则提示）。
        /// 注意：分组筛选可能藏住该源，先切"全部"再找；窗体未显示时先记下，OnShown 里再滚
        /// （AutoScrollPosition 置位要句柄，构造时调直接丢）。</summary>
        private int? _pendingSrcReg;
        private int? _pendingSrcCh;

        public void PreselectSource(int reg, int ch)
        {
            _pendingSrcReg = reg;
            _pendingSrcCh = ch;
            if (IsHandleCreated) ApplyPendingPreselect();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // 窗体限工作区：1020×720 在 100% 下刚好，125%/150% 下 AutoScale 放大后
            // 可能超出小屏工控机（1366×768），按钮够不着、画布被压扁——看着也像"布局坏了"。
            // 画布内部自带滚动，窗体缩了不丢内容，只收客户区。
            try
            {
                var work = Screen.FromControl(this).WorkingArea;
                int w = Math.Min(ClientSize.Width, work.Width - 40);
                int h = Math.Min(ClientSize.Height, work.Height - 60);
                w = Math.Max(w, 860);
                h = Math.Max(h, 600);
                if (w != ClientSize.Width || h != ClientSize.Height)
                    ClientSize = new Size(w, h);
            }
            catch { }
            ApplyPendingPreselect();
            // 滚轮悬停即缩放（IMessageFilter 预过滤，见类头；关窗摘除，防野回调）
            try { Application.AddMessageFilter(this); } catch { }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try { Application.RemoveMessageFilter(this); } catch { }
            base.OnFormClosed(e);
        }

        /// <summary>
        /// 滚轮消息预过滤：光标在画布上时直接缩放并吞掉消息（清单/下拉的滚轮不受影响）。
        /// </summary>
        public bool PreFilterMessage(ref Message m)
        {
            const int WM_MOUSEWHEEL = 0x020A;
            if (m.Msg != WM_MOUSEWHEEL) return false;
            if (_graph == null || _graph.IsDisposed) return false;
            try
            {
                if (!_graph.RectangleToScreen(_graph.ClientRectangle).Contains(Cursor.Position))
                    return false;
                int delta = (short)((m.WParam.ToInt32() >> 16) & 0xFFFF);
                _graph.ZoomAtCursor(delta > 0 ? 1 : -1);
                RefreshCounts();   // 缩放百分比显示在计数条，滚一格刷一次
                return true;
            }
            catch { return false; }
        }

        private void ApplyPendingPreselect()
        {
            if (_pendingSrcReg == null) return;
            int reg = _pendingSrcReg.Value, ch = _pendingSrcCh.Value;
            _pendingSrcReg = null;
            _pendingSrcCh = null;
            _binding = true;
            try { _cmbGroup.SelectedIndex = 0; }
            finally { _binding = false; }
            RefreshAll();
            if (!_graph.PreselectSource(reg, ch))
            {
                _lblStatus.Text = string.Format("源 {0} 不在当前点位池（总数配置可能已变），请手动选择。",
                    IoRemapValidator.Describe(reg, ch));
            }
            RefreshStatusAndButtons();
        }

        /// <summary>全刷（池/分组/映射任一变化后调：重灌画布 + 重建清单 + 刷计数）</summary>
        private void RefreshAll()
        {
            _binding = true;
            try
            {
                _chkEnabled.Checked = _workEnabled;
                _graph.SetData(FilterSources(), _allTargets, _workMappings);
                RebuildMapList();
                RefreshCounts();
            }
            finally { _binding = false; }
            RefreshStatusAndButtons();
        }

        /// <summary>按"源分组"下拉过滤源池（映射数据不动，只是暂时不画）</summary>
        private List<IoRemapEndpoint> FilterSources()
        {
            if (_cmbGroup.SelectedIndex <= 0) return _allSources;
            string want = _cmbGroup.SelectedItem != null ? _cmbGroup.SelectedItem.ToString() : "";
            var result = new List<IoRemapEndpoint>();
            foreach (var e in _allSources)
            {
                if (e.GroupTitle == want) result.Add(e);
            }
            return result;
        }

        private void RebuildMapList()
        {
            int keep = _lstMaps.SelectedIndex;
            _lstMaps.Items.Clear();
            foreach (var m in _workMappings)
            {
                if (m == null) continue;
                _lstMaps.Items.Add(DescribeMapping(m));
            }
            if (keep >= 0 && keep < _lstMaps.Items.Count) _lstMaps.SelectedIndex = keep;
        }

        /// <summary>清单行文案（"Y000 真空电磁阀-1 → Y220 预留输出-145"，找不到点位名就退化为寄存器描述）</summary>
        private string DescribeMapping(IoOutputChannelRemap m)
        {
            return string.Format("{0}  →  {1}", EndpointText(_allSources, m.SourceRegister, m.SourceChannel),
                EndpointText(_allTargets, m.TargetRegister, m.TargetChannel));
        }

        private static string EndpointText(List<IoRemapEndpoint> pool, int reg, int ch)
        {
            if (pool != null)
            {
                foreach (var e in pool)
                {
                    if (e.Register == reg && e.Channel == ch) return e.FullText;
                }
            }
            return IoRemapValidator.Describe(reg, ch);
        }

        private void RefreshCounts()
        {
            int hidden = CountHiddenMappings();
            int pct = 100;
            try { if (_graph != null) pct = (int)Math.Round(_graph.ZoomFactor * 100); }
            catch { }
            _lblCount.Text = string.Format("源 {0} · 目标 {1} · 已配 {2} · 缩放 {3}%",
                _allSources.Count, _allTargets.Count, _workMappings.Count, pct);
            _lblMapTitle.Text = string.Format("已配置映射（{0}）{1}", _workMappings.Count,
                hidden > 0 ? string.Format(" — 其中 {0} 条被当前筛选藏起（清单照常可删）", hidden) : "");
        }

        /// <summary>有一端不在当前画布里的映射条数（切池/切分组藏起来的，只藏不丢）</summary>
        private int CountHiddenMappings()
        {
            var srcKeys = new HashSet<string>();
            foreach (var e in FilterSources()) srcKeys.Add(e.Key);
            var tgtKeys = new HashSet<string>();
            foreach (var e in _allTargets) tgtKeys.Add(e.Key);
            int n = 0;
            foreach (var m in _workMappings)
            {
                if (m == null) continue;
                if (!srcKeys.Contains(m.SourceRegister + ":" + m.SourceChannel)
                    || !tgtKeys.Contains(m.TargetRegister + ":" + m.TargetChannel))
                    n++;
            }
            return n;
        }

        /// <summary>状态条 + 按钮使能（选中源→指引点目标；选中映射→可删；都有→优先源）</summary>
        private void RefreshStatusAndButtons()
        {
            var src = _graph.SelectedSource;
            var map = _graph.SelectedMapping;
            if (src != null)
            {
                _lblStatus.Text = string.Format("已选源：{0}。再点右侧一个目标即连线；点空白处取消。",
                    src.FullText);
            }
            else if (map != null)
            {
                _lblStatus.Text = string.Format("已选映射：{0}。点“删除选中”或右键删除；点空白处取消。",
                    DescribeMapping(map));
            }
            else
            {
                _lblStatus.Text = "操作：先左键点左侧一个源通道（变蓝），再点右侧一个备用目标完成连线；"
                    + "点连线可选中，右键可删除。目标独占：一个备用通道只允许接一个源。"
                    + "滚轮以鼠标为中心缩放，按住中键拖动画布，双击中键复位。";
            }
            _btnDeleteSel.Enabled = (map != null);
        }

        /// <summary>清单选择 ↔ 画布选择同步（清单点一行，画布对应连线变红；删除认画布选中，清单行号兜底）</summary>
        private void SyncListToGraph()
        {
            if (_binding) return;
            int idx = _lstMaps.SelectedIndex;
            if (idx < 0 || idx >= _workMappings.Count) return;
            var m = _workMappings[idx];
            if (m == null) return;
            _binding = true;
            try { _graph.SelectMapping(m.SourceRegister, m.SourceChannel, m.TargetRegister, m.TargetChannel); }
            finally { _binding = false; }
            RefreshStatusAndButtons();
        }

        // ===================== 连线/删除/保存 =====================

        private void OnMappingProposed(int srcReg, int srcCh, int dstReg, int dstCh)
        {
            if (!IoRemapValidator.ValidateNewMapping(_workMappings, srcReg, srcCh, dstReg, dstCh, out string error))
            {
                UIMessageBox.Show(error, "无法连线", UIStyle.Orange, UIMessageBoxButtons.OK, true, 0);
                RefreshStatusAndButtons();
                return;
            }
            _workMappings.Add(new IoOutputChannelRemap
            {
                SourceRegister = (ushort)srcReg,
                SourceChannel = srcCh,
                TargetRegister = (ushort)dstReg,
                TargetChannel = dstCh
            });
            // 目标独占：连上即把开关建议打开？不自动开，保存时统一问（防"连一根就改开关"吓到用户）
            RefreshAll();
        }

        private void OnMappingDeleteRequested(IoOutputChannelRemap map)
        {
            if (map == null) return;
            _workMappings = IoRemapValidator.WithoutSource(
                _workMappings, map.SourceRegister, map.SourceChannel);
            RefreshAll();
        }

        private void DeleteSelectedMapping()
        {
            // 删的是"当前选中"：画布连线选中优先（清单点行已同步过去），清单行号兜底
            var sel = _graph.SelectedMapping;
            if (sel == null && _lstMaps.SelectedIndex >= 0 && _lstMaps.SelectedIndex < _workMappings.Count)
                sel = _workMappings[_lstMaps.SelectedIndex];
            if (sel == null) return;
            _workMappings = IoRemapValidator.WithoutSource(
                _workMappings, sel.SourceRegister, sel.SourceChannel);
            RefreshAll();
        }

        private void ClearAllMappings()
        {
            if (_workMappings.Count == 0) return;
            if (MessageBox.Show(this, string.Format("确定清空全部 {0} 条映射吗？（点保存后才真正生效）",
                    _workMappings.Count),
                    "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            _workMappings.Clear();
            RefreshAll();
        }

        private void OnSave()
        {
            // 配了映射但开关没开 = 存了用不上：保存时统一问一次（连线时不问，防打断）
            if (ShowEnableSwitch && _workMappings.Count > 0 && !_workEnabled)
            {
                var r = MessageBox.Show(this,
                    string.Format("已配置 {0} 条映射，但“启用备用通道映射”未勾选（不勾=映射不生效）。\n\n是否同时启用？",
                        _workMappings.Count),
                    "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r == DialogResult.Yes)
                {
                    _workEnabled = true;
                    _binding = true;
                    try { _chkEnabled.Checked = true; }
                    finally { _binding = false; }
                }
            }
            Confirmed = true;
            ResultMappings = new List<IoOutputChannelRemap>(_workMappings);
            ResultEnabled = _workEnabled;
            try { DialogResult = DialogResult.OK; } catch { }
            Close();
        }
    }
}
