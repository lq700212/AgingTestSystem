using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AgingTestSystem.Models;
using AgingTestSystem.Services;
using Newtonsoft.Json;

namespace AgingTestSystem.Views
{
    /// <summary>
    /// 工艺策略（三期可视化：
    /// 老化业务流固定拓扑 + 点节点改配置）。
    /// 【为什么叫工艺策略不叫流程驾驶舱】现场操作员看到"驾驶舱"不知道是干什么的；
    /// 这里改的正是"参数设置→工艺策略/规则流程/MES上报"同一批 key（存 Policy.json），
    /// 名字与设置分类同词，所见即所得：改工艺来这里。
    /// 【为什么是固定拓扑而不是通用连线编辑器】
    /// 参考过 HJVision 的 mFormFlowEdit（串行站点流水线 ST1→ST2→…，每次部署拓扑都不同，
    /// 所以要拖节点+连线+生成代码）。我们的老化是并行时间状态机：72 台同时在
    /// 抽真空/老化，拓扑被物理锁死（不上真空不能上电，唯一拓扑开关就是 SkipVacuum）。
    /// 通用编辑器会让客户能删掉安全联锁（删个节点=删真空保护），违反"规则只能加严"家规。
    /// 所以这里拓扑画死、节点可拖（纯视图）、点谁改谁的真实配置，存 Policy.json。
    /// 静态边框 Designer 化：右栏空壳/状态条/窗体属性搬进
    /// ProcessPolicyForm.Designer.cs；自绘画布（要吃真实参数）+ 动态编辑器 +
    /// 定时器仍在代码里。无参构造只装边框（构造冒烟/以后 VS 可视编辑用）。
    /// 【界面布局】（左列主链 + 右列分支 + 右下 MES 纯配置节点）
    /// ┌──────────────────────────────────────────────┬───────────────┐
    /// │ 画布（自绘，可缩放/平移/拖节点）               │ 右栏 320px    │
    /// │  [启动开阀]              [断电恢复]            │ 预置策略行    │
    /// │      │开阀                   ┊整台重测/续跑    │ [A/B/C下拉]   │
    /// │  [抽真空]────────超时/失压/失联/规则───────┐   │ [套用预置]    │
    /// │      │压力到位 且 延时时间到                          │ 选中节点标题  │
    /// │  [上电老化]────失压/失联/规则/超温───────┐   │ [编辑器组]    │
    /// │      │时长到 或 表达式                      [报警联动]        │
    /// │  [完成下电]──────────待取料/待判定──────→    │ [保存本节点]  │
    /// │                        └────────→                             │
    /// │                                     [MES上报]（无连线，纯配置）│
    /// ├──────────────────────────────────────────────┴───────────────┤
    /// │ 状态条：项目名 | 选中节点 | 保存提示                           │
    /// └──────────────────────────────────────────────────────────────┘
    /// 右栏顶部（标题46/下拉+按钮68/说明100）：下拉选 A/B/C
    /// （或"自定义"回显），"套用预置"一键整套生效（确认框列差异项 + 前置条件，
    /// 走 PersistChanges 同一条保存路）。只动 12 个行为开关（见 PolicyPresets），
    /// MES/规则/报表/点位/时长阈值不动；只读模式整行禁用。
    /// 全部节点 Bool/Enum 下拉按预置下拉口径统一：
    /// 下拉列表按最长选项实测拉宽（SizeNodeCombo，最长 22 字项原来被截断）+
    /// 悬停看选中项全文（共享 _editorTip，切节点清表防钉住泄漏，随窗体释放）。
    /// 【鼠标操作】（对标 mFormFlowEdit 手感）
    /// - 左键拖节点体 = 移动节点（位置存 PolicyLayout.json，下次打开接着用）；
    /// - 左键点节点/连线 = 选中（右栏切出该节点的编辑器；连线只读，显示条件来源）；
    /// - 左键点空白 = 取消选中；Esc 同样取消；
    /// - 滚轮 = 缩放（25%~400%，以鼠标为中心，不用先点画布抢焦点）；
    /// - 按住中键拖 = 平移画布；Del 键无动作（拓扑固定，不许删除）。
    /// 【坐标】节点 X/Y 存 96DPI 逻辑像素；绘制/命中走 Scale()=DPI×zoom 一处；
    /// 缩放是纯视图态（不进存盘）；字体用 pt（自动随 DPI 放大）×zoom 缓存两档；
    /// 禁用 Graphics.ScaleTransform（TextRenderer/GDI 与变换矩阵行为不一致，家规）。
    /// </summary>
    public partial class ProcessPolicyForm : Sunny.UI.UIForm, IMessageFilter
    {
        private DeviceConfig _config;
        private DeviceManager _deviceManager;

        // 注：_canvas/_pnlRight/_lblNodeTitle/_pnlEditors/_btnSaveNode/_btnResetLayout/
        // _btnClose/_lblStatus 全在 ProcessPolicyForm.Designer.cs 里声明并静态装配。
        // 这里只留运行时态（选中/脏标记/编辑器表/定时器）+ 动态内容逻辑。

        private string _selectedId;
        private readonly Dictionary<string, Control> _editorControls = new Dictionary<string, Control>();
        private bool _dirty;
        private readonly bool _canEdit;
        private Timer _timer;

        // 静态布局（坐标/文本/事件挂接）在 Designer，VS 可预览；
        // 这里只留运行时态：选项填充（数据源 PolicyPresets.All）+ 悬停提示 +
        // 下拉联动自保护旗。预置控件随窗体释放（静态挂接，无动态重建，终结器安全）。
        // _presetTip 无容器托管（本窗 Designer 无 components 容器），随窗体 Dispose
        // 手动释放（见本文件 Dispose 重写；R2 配对检查认方法体内真释放）。
        private ToolTip _presetTip;
        // 下拉联动自保护：程序回显选中项时不触发"用户改选"分支（只套用按钮才真干活）。
        private bool _presetRefreshing;

        // 与 _presetTip 同款的共享悬停提示（一个实例管全部
        // 节点下拉，选中项全文随选随换）。不给每个下拉各建一个：提示按控件建表
        // 强引用，旧编辑器释放时不清表 = 已释放控件被提示钉住不回收（切节点一
        // 次漏几个，小而久的托管泄漏）。所以切节点时 DisposeEditorControls 先
        // RemoveAll() 清表（见该方法注释），窗体释放时这里 Dispose（见 Dispose 重写）。
        private ToolTip _editorTip;

        /// <summary>本次会话保存过的 key（主窗体按需热生效，SettingsForm.SavedKeys 同款语义）</summary>
        public HashSet<string> SavedKeys { get; private set; }

        /// <summary>
        /// 无参构造（Designer/构造冒烟用：只装静态边框，不建画布不读配置，
        /// 所有画布调用处均判空，构造永不抛）。
        /// </summary>
        public ProcessPolicyForm()
        {
            SavedKeys = new HashSet<string>();
            InitializeComponent();
            InitPresetBar();
            _editorTip = new ToolTip();
            UpdateStatus("就绪：点击节点查看/修改配置。滚轮缩放 · 中键平移 · 拖节点移动。");
        }

        /// <param name="config">内存中的设备配置（主窗体同一引用，保存后热回写）</param>
        /// <param name="deviceManager">设备管理器（读实时台数；可为 null，此时计数全 0）</param>
        /// <param name="canEdit">是否可编辑（仅管理员 true；技术员/操作员只读看图。
        /// 与系统设置"仅管理员可改"同级——驾驶舱改的是同一批 key，不能开后门）</param>
        public ProcessPolicyForm(DeviceConfig config, DeviceManager deviceManager, bool canEdit)
            : this()
        {
            _config = config;
            _deviceManager = deviceManager;
            _canEdit = canEdit;

            // 静态边框（右栏空壳/状态条）已由 InitializeComponent 装好（见 Designer）。
            // 这里只建自绘画布：构造要吃真实 config/dm，Designer 给不了，只能代码 new；
            // Dock=Fill 最后加（右栏 Right、状态条 Bottom 先加，Z 序不能反）。

            // 【高 DPI 三要素】剩余一条：画布是自绘 Panel（AutoScaleMode.None），
            // 内部用 CreateGraphics().DpiX 手动缩放（见 FlowCanvas.Scale，家规）。
            _canvas = new FlowCanvas(_config, _deviceManager);
            _canvas.Dock = DockStyle.Fill;
            _canvas.NodeSelected += (id) => SelectNode(id, true);
            _canvas.EdgeSelected += (edgeId) => SelectEdge(edgeId);
            this.Controls.Add(_canvas);

            SelectNode(null, false);
            RefreshPresetRow();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // 滚轮悬停即缩放（不用先点画布抢焦点，右栏改地址时悬停回来照样缩）
            Application.AddMessageFilter(this);
            // 实时台数每秒刷新（画布重画；关窗即停）
            // 【大扫荡】复用安全：窗体若被二次 Show，先清旧 timer（以前匿名 Tick 只增不减）。
            if (_timer != null) { try { _timer.Stop(); _timer.Dispose(); } catch { } _timer = null; }
            _timer = new Timer { Interval = 1000 };
            _timer.Tick += (s, args) =>
            {
                // 关窗竞态：释放后 Tick 丢弃（UI 定时器与关闭同线程串行，
                // 此查防 Dispose 后残留触发；RefreshCounts 内部另有 IsDisposed 自拦，双保险）。
                try
                {
                    if (IsDisposed || Disposing) return;
                    if (_canvas != null && !_canvas.IsDisposed) _canvas.RefreshCounts();
                }
                catch { }
            };
            _timer.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try
            {
                // 【大扫荡】Dispose 后置 null：字段再指已释放对象，复用/探针都干净。
                if (_timer != null) { _timer.Stop(); _timer.Dispose(); _timer = null; }
            }
            catch { }
            Application.RemoveMessageFilter(this);
            base.OnFormClosed(e);
        }

        /// <summary>
        /// 释放悬停提示（_presetTip 无容器托管（本窗 Designer 无 components
        /// 容器），必须手写释放；R2 配对检查认方法体内真 Dispose。静态挂接随窗体走，
        /// _timer 照旧在 OnFormClosed 里停（同线程串行，双保险判空）。
        /// _editorTip 同款无容器托管，同处释放（两行一一对应，缺一行
        /// 漏一个；回归锁"关窗后两字段皆 null"）。
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    if (_presetTip != null) { _presetTip.Dispose(); _presetTip = null; }
                    if (_editorTip != null) { _editorTip.Dispose(); _editorTip = null; }
                }
                catch { }
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// 滚轮消息预过滤：光标在画布上时直接缩放并吞掉消息（右栏文本框的滚轮不受影响）。
        /// </summary>
        public bool PreFilterMessage(ref Message m)
        {
            const int WM_MOUSEWHEEL = 0x020A;
            if (m.Msg != WM_MOUSEWHEEL) return false;
            if (_canvas == null || _canvas.IsDisposed) return false;
            try
            {
                if (!_canvas.RectangleToScreen(_canvas.ClientRectangle).Contains(Cursor.Position))
                {
                    return false;
                }
                int delta = (short)((m.WParam.ToInt32() >> 16) & 0xFFFF);
                _canvas.ZoomAtCursor(delta > 0 ? 1 : -1);
                return true;
            }
            catch { return false; }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                SelectNode(null, true);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>选中节点：切右栏编辑器（脏了先问存不存，不丢输入）。</summary>
        private void SelectNode(string id, bool fromCanvas)
        {
            if (!string.Equals(_selectedId, id, StringComparison.Ordinal) && _dirty)
            {
                DialogResult r = MessageBox.Show(this,
                    "当前节点有未保存的修改，切换前保存吗？\n\n【是】保存后切换\n【否】丢弃后切换\n【取消】留在当前节点",
                    "提示", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel)
                {
                    // 留在原节点：画布选中弹回（直接重设，不触发二次确认）
                    if (_canvas != null) _canvas.SetSelected(_selectedId, null);
                    return;
                }
                if (r == DialogResult.Yes && !SaveCurrentNode())
                {
                    // 保存失败（校验拦）：留在原节点
                    if (_canvas != null) _canvas.SetSelected(_selectedId, null);
                    return;
                }
                _dirty = false;
            }
            _selectedId = id;
            if (_canvas != null) _canvas.SetSelected(_selectedId, null);
            RebuildEditors();
        }

        /// <summary>选中连线：右栏只读展示条件来源（条件本身在两端节点里改）。</summary>
        private void SelectEdge(string edgeId)
        {
            if (_dirty)
            {
                DialogResult r = MessageBox.Show(this,
                    "当前节点有未保存的修改，切换前保存吗？",
                    "提示", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel) return;
                if (r == DialogResult.Yes && !SaveCurrentNode()) return;
                _dirty = false;
            }
            _selectedId = null;
            RebuildEdgeInfo(edgeId);
        }

        /// <summary>
        /// 摘掉右栏旧编辑器（先 Dispose 再 Clear；改走快照）。
        /// 【修什么崩溃】Controls.Clear() 只摘父子关系、不释放资源：
        /// 旧编辑器（含 Sunny UITextBox/UIComboBox，内部包着原生 TextBox）变成孤儿，
        /// GC 时走终结器线程 Dispose，Sunny 的 Dispose 路径里读了内部 TextBox.Handle，
        /// 创建线程（UI）≠终结器线程 → InvalidOperationException 跨线程崩溃。
        /// 现场症状 = 点/拖节点后随机时刻弹"非 UI 线程"错（终结时机不定，
        /// 炸的时候用户正在干别的事，极具迷惑性；Name="" 是因为动态控件都没设 Name）。
        /// 主窗 CreateWorkstationPanels 早有同款处理（H5 先 Dispose 再 Clear），这里补齐。
        /// foreach 直接枚举 Controls 集合逐个 Dispose
        /// 是错的：Control.Dispose() 会把自己从父集合摘除，释放第 0 个后其余前移，
        /// 枚举器下标 +1 正好跳过一个——被跳过的孩子没释放、又被随后的 Clear() 摘掉，
        /// 照样是孤儿，GC 时照样在终结器线程炸（拖业务框时炸的其实是早先某次切节点
        /// 漏掉的旧编辑器）。正确姿势是 ControlDisposeHelper：先 CopyTo 快照成数组
        /// 再逐个释放（枚举快照不怕原集合被改），最后 Clear。以后动态重建一律调它。
        /// ToolTip 内部按控件建表（强引用）：旧编辑器 Dispose 了，
        /// 表不清 = 已释放的下拉还被 _editorTip 钉着，GC 回收不了。切节点是高频动作
        /// （现场一个个点过去），不清表就是"切一次漏几个控件"的慢性泄漏。
        /// 所以快照释放前先 RemoveAll()（清表不碰控件），再走 helper 释放旧编辑器。
        /// </summary>
        private void DisposeEditorControls()
        {
            try { if (_editorTip != null) _editorTip.RemoveAll(); }
            catch { }
            ControlDisposeHelper.DisposeAllAndClear(_pnlEditors.Controls);
            _editorControls.Clear();
        }

        /// <summary>按选中节点重建右栏编辑器。</summary>
        private void RebuildEditors()
        {
            DisposeEditorControls();
            _dirty = false;

            if (string.IsNullOrEmpty(_selectedId))
            {
                _lblNodeTitle.Text = "未选中节点";
                _btnSaveNode.Enabled = false;
                var hint = new Sunny.UI.UILabel
                {
                    Location = new Point(0, 0),
                    Size = new Size(270, 120),
                    Text = "点击左侧节点，\r\n在这里改它的配置。",
                    ForeColor = Color.Gray
                };
                _pnlEditors.Controls.Add(hint);
                UpdateStatus("未选中节点。");
                return;
            }

            PolicyGraph.NodeDef def = PolicyGraph.FindNode(_selectedId);
            if (def == null)
            {
                _lblNodeTitle.Text = "未知节点";
                _btnSaveNode.Enabled = false;
                return;
            }
            _lblNodeTitle.Text = def.Title;
            _btnSaveNode.Enabled = _canEdit;

            int y = 0;
            if (!_canEdit)
            {
                var ro = new Sunny.UI.UILabel
                {
                    Location = new Point(0, y),
                    Size = new Size(270, 30),
                    Text = "只读模式（管理员可改）",
                    ForeColor = Color.Gray
                };
                _pnlEditors.Controls.Add(ro);
                y += 34;
            }

            foreach (PolicyGraph.NodeKey key in def.Keys)
            {
                // 无阀本机藏破空点位行：VentValveDoPoint 是"有阀才填"的通道号，
                // 无阀时露出来只会诱导人填，填了也写不出去（保存校验+执行双拦）。
                // 开关本身（VentValveEnabled）照常显示——开阀门的总闸不能藏。
                if (string.Equals(key.Key, "VentValveDoPoint", StringComparison.Ordinal)
                    && (_config == null || !_config.VentValveEnabled))
                {
                    continue;
                }
                var lbl = new Sunny.UI.UILabel
                {
                    Location = new Point(0, y),
                    Size = new Size(270, 20),
                    Text = key.Label
                };
                _pnlEditors.Controls.Add(lbl);
                // 【标题 tooltip】每个配置项标题悬停看中文说明（与系统设置表同源，
                // 超 40 字走 SettingsForm.WrapTooltip 换行，全仓唯一换行口，不手写截断）。
                // 说明取不到时回退显示标题本身，保证"每项都有提示"（正常全能取到，
                // 回退只防以后加 key 忘加说明）。挂在 _editorTip 上（与节点下拉共享实例，
                // 切节点时 DisposeEditorControls.RemoveAll() 统一清表，不钉住已释放控件；
                // 窗体 Dispose 统一释放，不新增 ToolTip 字段，终结器安全）。
                try
                {
                    if (_editorTip == null) _editorTip = new ToolTip();
                    string desc = Dialogs.SettingsForm.GetDescription(key.Key);
                    if (string.IsNullOrEmpty(desc)) desc = key.Label;
                    _editorTip.SetToolTip(lbl, Dialogs.SettingsForm.WrapTooltip(desc));
                }
                catch { /* 提示写失败不影响主流程（纯展示） */ }
                y += 22;

                Control editor = CreateEditor(key.Key, key.Kind);
                editor.Location = new Point(0, y);
                editor.Width = 270;
                // 选项框按预置下拉口径统一：下拉列表按最长选项实测拉宽 +
                // 悬停看选中项全文（见 SizeNodeCombo）。宽 270 定死后才能量（量的是
                // 像素，基准就是这个 270），所以调口必须在 Width 赋值之后。
                var nodeCmb = editor as Sunny.UI.UIComboBox;
                if (nodeCmb != null) SizeNodeCombo(nodeCmb);
                // 只读模式：控件禁用（与系统设置"仅管理员可改"同级，不开后门）
                editor.Enabled = _canEdit;
                if (key.Kind == PolicyGraph.EditorKind.Multiline)
                {
                    editor.Height = 96;
                    y += 102;
                }
                else
                {
                    editor.Height = 26;
                    y += 32;
                }
                _pnlEditors.Controls.Add(editor);
                // 【输入框 tooltip】文本/多行框本身也挂同一份说明（悬停框体即见，
                // 不用先瞄准 20px 高的标题行）。下拉框不动：它的悬停是"当前选中项全文"
                // （SizeNodeCombo 已设，说明在标题行看，两边各管各的不打架）。
                if (nodeCmb == null)
                {
                    try
                    {
                        if (_editorTip == null) _editorTip = new ToolTip();
                        string edesc = Dialogs.SettingsForm.GetDescription(key.Key);
                        if (string.IsNullOrEmpty(edesc)) edesc = key.Label;
                        _editorTip.SetToolTip(editor, Dialogs.SettingsForm.WrapTooltip(edesc));
                    }
                    catch { /* 提示写失败不影响主流程（纯展示） */ }
                }
                _editorControls[key.Key] = editor;
            }

            if (def.Keys.Count == 0)
            {
                var info = new Sunny.UI.UILabel
                {
                    Location = new Point(0, y),
                    Size = new Size(270, 80),
                    Text = def.Info,
                    ForeColor = Color.Gray
                };
                _pnlEditors.Controls.Add(info);
                _btnSaveNode.Enabled = false;
            }
            else if (!string.IsNullOrEmpty(def.Info))
            {
                // 有 key 也有说明的节点（下料判定）：编辑器下面追加灰字指引。
                // 以前 Info 只在无 key 时显示；下料节点收进事件口径/报表列后有 key 了，
                // "判定口径在完成下电改"的指引不能丢，改成页脚保留（不占编辑器名额）。
                var foot = new Sunny.UI.UILabel
                {
                    Location = new Point(0, y),
                    Size = new Size(270, 60),
                    Text = def.Info,
                    ForeColor = Color.Gray
                };
                _pnlEditors.Controls.Add(foot);
            }
            UpdateStatus($"已选中【{def.Title}】，改完点“保存本节点”。");
        }

        /// <summary>连线只读信息（条件在哪改指明，不让用户对着线发呆）。</summary>
        private void RebuildEdgeInfo(string edgeId)
        {
            DisposeEditorControls();
            _lblNodeTitle.Text = "连线（只读）";
            _btnSaveNode.Enabled = false;
            var info = new Sunny.UI.UILabel
            {
                Location = new Point(0, 0),
                Size = new Size(270, 160),
                Text = PolicyGraph.BuildEdgeInfo(edgeId, _config),
                ForeColor = Color.Gray
            };
            _pnlEditors.Controls.Add(info);
            UpdateStatus("连线只读：条件在两端节点里改。");
        }

        /// <summary>按 key 类型建编辑器（初值读内存 _config，与系统设置热回写同一源）。</summary>
        private Control CreateEditor(string key, PolicyGraph.EditorKind kind)
        {
            string current = GetConfigString(key);
            if (kind == PolicyGraph.EditorKind.Bool)
            {
                var cmb = new Sunny.UI.UIComboBox { DropDownStyle = Sunny.UI.UIDropDownStyle.DropDownList };
                cmb.Items.Add(new FlowOpt("false", "false"));
                cmb.Items.Add(new FlowOpt("true", "true"));
                SelectOpt(cmb, current.Equals("true", StringComparison.OrdinalIgnoreCase) ? "true" : "false");
                cmb.SelectedIndexChanged += (s, e) => OnNodeComboChanged(s);
                return cmb;
            }
            if (kind == PolicyGraph.EditorKind.Enum)
            {
                var cmb = new Sunny.UI.UIComboBox { DropDownStyle = Sunny.UI.UIDropDownStyle.DropDownList };
                Tuple<string, string>[] opts;
                if (ProjectPolicyStore.EnumOptions.TryGetValue(key, out opts))
                {
                    foreach (var o in opts) cmb.Items.Add(new FlowOpt(o.Item1, o.Item2));
                }
                SelectOpt(cmb, NormalizeEnumValue(key, current));
                cmb.SelectedIndexChanged += (s, e) => OnNodeComboChanged(s);
                return cmb;
            }
            if (kind == PolicyGraph.EditorKind.Multiline)
            {
                var txt = new Sunny.UI.UITextBox { Multiline = true, ShowScrollBar = true };
                txt.Text = current;
                txt.TextChanged += (s, e) => MarkDirty();
                return txt;
            }
            var box = new Sunny.UI.UITextBox();
            box.Text = current;
            box.TextChanged += (s, e) => MarkDirty();
            return box;
        }

        private void MarkDirty()
        {
            _dirty = true;
            UpdateStatus("有未保存的修改。");
        }

        private static void SelectOpt(Sunny.UI.UIComboBox cmb, string value)
        {
            for (int i = 0; i < cmb.Items.Count; i++)
            {
                var o = cmb.Items[i] as FlowOpt;
                if (o != null && string.Equals(o.Value, value, StringComparison.OrdinalIgnoreCase))
                {
                    cmb.SelectedIndex = i;
                    return;
                }
            }
            if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
        }

        /// <summary>节点下拉改选：标脏 + 悬停全文同步换（提示永远是当前选中项）。</summary>
        private void OnNodeComboChanged(object sender)
        {
            MarkDirty();
            RefreshNodeComboTip(sender as Sunny.UI.UIComboBox);
        }

        /// <summary>
        /// 节点选项框按预置下拉（_cboPreset）口径统一（全节点生效）：
        /// 下拉列表按最长选项实测拉宽（不再默认与框同宽 270，最长的
        /// "启动定格：该轮启动时的SN/配方，中途重绑不污染"原来在下拉里被拦腰截断），
        /// 悬停看选中项全文（闭合框 270 放不下长中文时，悬停补全文，与预置行同手感）。
        /// 【为什么实测不定死 340】预置选项文案固定四条，写死 340 省事；节点是 9 组
        /// 枚举 + 布尔两类，文案长短差 4 倍（"false" 5 个字符 vs 最长 22 个汉字+英文）。
        /// 写死 340 会让布尔下拉虚胖、极端长项仍可能差几个像素。实测量一次全都有：
        /// 短的保持框宽（不虚胖），长的按需拉宽（不截断），480 封顶（超出看悬停，
        /// 不把下拉铺满屏）。EnumOptions 加新长文案时这里自动跟上，不用手改。
        /// 【高 DPI 说明】量的是当前 DC 的设备像素，下拉宽与字同源，缩放一致；
        /// 万一哪台工控机字体渲染偏胖差几个像素，闭合框还有悬停全文兜底，
        /// 现场永远有路看到全文。
        /// </summary>
        private void SizeNodeCombo(Sunny.UI.UIComboBox cmb)
        {
            if (cmb == null || cmb.IsDisposed) return;
            try
            {
                int need = cmb.Width;
                using (Graphics g = cmb.CreateGraphics())
                {
                    foreach (var item in cmb.Items)
                    {
                        string t = item != null ? item.ToString() : null;
                        if (string.IsNullOrEmpty(t)) continue;
                        int w = TextRenderer.MeasureText(g, t, cmb.Font,
                            new Size(int.MaxValue, int.MaxValue),
                            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
                        if (w > need) need = w;
                    }
                }
                need += SystemInformation.VerticalScrollBarWidth + 12;
                if (need < cmb.Width) need = cmb.Width;
                if (need > 480) need = 480;
                cmb.DropDownWidth = need;
            }
            catch { try { cmb.DropDownWidth = cmb.Width; } catch { } }
            RefreshNodeComboTip(cmb);
        }

        /// <summary>悬停全文同步为当前选中项（与预置行"闭合框看全文"同手感）。</summary>
        private void RefreshNodeComboTip(Sunny.UI.UIComboBox cmb)
        {
            try
            {
                if (_editorTip == null || cmb == null || cmb.IsDisposed) return;
                object sel = (cmb.SelectedIndex >= 0 && cmb.SelectedIndex < cmb.Items.Count)
                    ? cmb.Items[cmb.SelectedIndex] : null;
                _editorTip.SetToolTip(cmb, sel != null ? sel.ToString() : "");
            }
            catch { }
        }

        private static string NormalizeEnumValue(string key, string current)
        {
            Tuple<string, string>[] opts;
            if (ProjectPolicyStore.EnumOptions.TryGetValue(key, out opts))
            {
                foreach (var o in opts)
                {
                    if (string.Equals(o.Item2, current != null ? current.Trim() : "",
                        StringComparison.OrdinalIgnoreCase)) return o.Item2;
                }
                if (opts.Length > 0) return opts[0].Item2;
            }
            return current ?? "";
        }

        /// <summary>读内存配置字符串（枚举存英文名，布尔小写，与保存口径一致）。</summary>
        private string GetConfigString(string key)
        {
            try
            {
                var prop = typeof(DeviceConfig).GetProperty(key);
                if (prop == null) return "";
                object v = prop.GetValue(_config, null);
                if (v == null) return "";
                if (v is bool) return ((bool)v) ? "true" : "false";
                return v.ToString();
            }
            catch { return ""; }
        }

        private void BtnSaveNode_Click(object sender, EventArgs e)
        {
            SaveCurrentNode();
        }

        /// <summary>复位布局按钮（Designer 命名处理器，原匿名 lambda 落袋）。</summary>
        private void BtnResetLayout_Click(object sender, EventArgs e)
        {
            if (_canvas != null) _canvas.ResetLayout();
            UpdateStatus("布局已复位。");
        }

        /// <summary>关闭按钮（Designer 命名处理器，原匿名 lambda 落袋）。</summary>
        private void BtnClose_Click(object sender, EventArgs e)
        {
            this.DialogResult = SavedKeys.Count > 0
                ? DialogResult.OK : DialogResult.Cancel;
            this.Close();
        }

        /// <summary>
        /// 保存当前节点：逐项 ValidateValue → 统一 PersistChanges（与系统设置同一条路：
        /// 组合校验 + 分流写文件 + 热回写全在里面）。
        /// </summary>
        /// <returns>true=保存成功（或无节点），false=被拦截（留在本节点）</returns>
        private bool SaveCurrentNode()
        {
            if (string.IsNullOrEmpty(_selectedId)) return true;
            PolicyGraph.NodeDef def = PolicyGraph.FindNode(_selectedId);
            if (def == null || def.Keys.Count == 0) return true;

            var changes = new Dictionary<string, string>();
            var invalid = new List<string>();
            foreach (PolicyGraph.NodeKey key in def.Keys)
            {
                Control editor;
                if (!_editorControls.TryGetValue(key.Key, out editor)) continue;
                string value = ReadEditor(editor);
                string error;
                if (!Dialogs.SettingsForm.ValidateValue(key.Key, value, out error))
                {
                    invalid.Add($"【{key.Label}】 {value}  →  {error}");
                    continue;
                }
                changes[key.Key] = value;
            }
            if (invalid.Count > 0)
            {
                MessageBox.Show(this, "以下配置值不合法，请修改后再保存：\r\n\r\n" +
                    string.Join("\r\n", invalid.ToArray()),
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            Dialogs.SettingsForm.PersistResult presult;
            string perror;
            if (!Dialogs.SettingsForm.PersistChanges(_config, changes, out presult, out perror))
            {
                MessageBox.Show(this, perror, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            foreach (string k in presult.SavedKeys) SavedKeys.Add(k);
            _dirty = false;
            // 破空阀总闸翻转后刷新右栏：VentValveDoPoint 行的显隐是按
            // _config.VentValveEnabled 即时判定的（见 RebuildEditors），刚保存完
            // 内存已热回写，这里重建一次右栏，点位行当场出现/消失，不用切节点才看到。
            if (presult.SavedKeys.Contains("VentValveEnabled")) RebuildEditors();
            if (_canvas != null) _canvas.RefreshCounts();
            string msg = "已保存并即时生效。";
            if (presult.StructuralChanged.Count > 0) msg += "（含重启生效项）";
            if (presult.SecretFallbackPlain) msg += "（注：密钥加密失败，已明文保存）";
            UpdateStatus(msg);
            MessageBox.Show(this, msg, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }

        private static string ReadEditor(Control editor)
        {
            var cmb = editor as Sunny.UI.UIComboBox;
            if (cmb != null)
            {
                var o = cmb.SelectedItem as FlowOpt;
                return o != null ? o.Value : (cmb.Text ?? "");
            }
            var txt = editor as Sunny.UI.UITextBox;
            if (txt != null) return (txt.Text ?? "").Trim();
            // 兜底：未知编辑器类型读 Text（改干净：不留原生分支）
            var any = editor as Control;
            if (any != null) return (any.Text ?? "").Trim();
            return "";
        }

        private void UpdateStatus(string text)
        {
            if (_lblStatus != null && !_lblStatus.IsDisposed)
            {
                _lblStatus.Text = $"项目：{ProjectProfile.ActiveProfileName} | {text}";
            }
        }

        /// <summary>
        /// 预置行运行时初始化（布局在 Designer，这里只做 Designer 干不了的两样：
        /// ①下拉选项填充（数据源 PolicyPresets.All，循环写 InitializeComponent 会被
        /// VS 重写吞掉）；②悬停提示（全文显示，见 UpdatePresetDesc）。
        /// 无参构造也调——_config 为 null 时回显"自定义"、整行禁用，不抛，VS 可预览）。
        /// </summary>
        private void InitPresetBar()
        {
            if (_cboPreset != null && !_cboPreset.IsDisposed && _cboPreset.Items.Count == 0)
            {
                foreach (PolicyPresets.PolicyPresetDef p in PolicyPresets.All)
                {
                    _cboPreset.Items.Add(new FlowOpt(p.Title, p.Id));
                }
                _cboPreset.Items.Add(new FlowOpt("自定义（当前配置）", PolicyPresets.CustomId));
            }
            if (_presetTip == null)
            {
                _presetTip = new ToolTip();
            }
            RefreshPresetRow();
        }

        /// <summary>下拉改选只换说明文字（真干活只认"套用预置"按钮，防手滑一切换就改配置）。</summary>
        private void PresetComboChanged(object sender, EventArgs e)
        {
            if (_presetRefreshing) return;
            UpdatePresetDesc();
        }

        /// <summary>
        /// 按内存配置回显预置行：凑上 A/B/C 就选中它，否则落"自定义"；
        /// 只读模式（非管理员）整行禁用（与节点编辑器同级，不开后门）。
        /// </summary>
        private void RefreshPresetRow()
        {
            if (_cboPreset == null || _cboPreset.IsDisposed) return;
            _presetRefreshing = true;
            try
            {
                SelectPresetOpt(_cboPreset, PolicyPresets.DetectPreset(_config));
            }
            finally { _presetRefreshing = false; }
            UpdatePresetDesc();
            bool editable = _canEdit && _config != null;
            _cboPreset.Enabled = editable;
            _btnApplyPreset.Enabled = editable;
        }

        private static void SelectPresetOpt(Sunny.UI.UIComboBox cmb, string id)
        {
            for (int i = 0; i < cmb.Items.Count; i++)
            {
                var o = cmb.Items[i] as FlowOpt;
                if (o != null && string.Equals(o.Value, id, StringComparison.OrdinalIgnoreCase))
                {
                    cmb.SelectedIndex = i;
                    return;
                }
            }
            // 对不上任何预置（或 null 配置）→落"自定义"末项，绝不空选
            cmb.SelectedIndex = cmb.Items.Count - 1;
        }

        private static string SelectedPresetId(Sunny.UI.UIComboBox cmb)
        {
            if (cmb == null) return PolicyPresets.CustomId;
            var o = cmb.SelectedItem as FlowOpt;
            return o != null ? o.Value : PolicyPresets.CustomId;
        }

        /// <summary>
        /// 下拉当前项的一句话说明 + 悬停全文（下拉框窄看不全：
        /// 下拉列表本身已拉宽到 340，闭合框的悬停提示在这里补全文——标题 +
        /// 场景 + 前置条件，与确认框同源，不另写一份文案）。
        /// </summary>
        private void UpdatePresetDesc()
        {
            if (_lblPresetDesc == null || _lblPresetDesc.IsDisposed) return;
            string id = SelectedPresetId(_cboPreset);
            PolicyPresets.PolicyPresetDef def = PolicyPresets.Find(id);
            _lblPresetDesc.Text = def != null
                ? def.Scenario
                : "当前配置与A/B/C都不完全一致（手动微调过），可重选一套覆盖。";
            // 悬停提示同步（_cboPreset 闭合显示被截断时，悬停看全文）
            try
            {
                if (_presetTip != null && _cboPreset != null && !_cboPreset.IsDisposed)
                {
                    string tip = def != null
                        ? def.Title + "\r\n\r\n" + def.Scenario + "\r\n\r\n" + def.HowToSwitch
                            + (string.IsNullOrWhiteSpace(def.Requires)
                                ? "" : "\r\n\r\n前置条件：" + def.Requires)
                        : "自定义（当前配置）\r\n\r\n"
                            + "当前 12 个行为开关与A/B/C都不完全一致（手动微调过），"
                            + "下拉重选一套并点“套用预置”可整体覆盖。";
                    _presetTip.SetToolTip(_cboPreset, tip);
                }
            }
            catch { /* 提示写失败不影响主流程（纯展示） */ }
        }

        private void BtnApplyPreset_Click(object sender, EventArgs e)
        {
            ApplyPreset();
        }

        /// <summary>
        /// 套用预置（傻瓜入口：脏先问存 → 列差异确认 → 逐项校验 →
        /// PersistChanges 同一条保存路 → 刷新右栏编辑器 + 画布 + 预置回显）。
        /// 【只动 12 个行为开关＋数值项】MES/规则/报表/画面字典/破空点位/时长阈值等
        /// 自由文本与配方/机器参数一律不动——切预置不丢现场已填的东西。
        /// 数值项（D 的真空超时 0）是跟项目走的策略 key，同样进本项目 Policy.json。
        /// 【失败语义】校验拦/C 组合拦（超温上限为 0）都是"按住不动 + 中文告诉人
        /// 去哪填"，绝不悄悄写一半（PersistChanges 内部先验后写）。
        /// </summary>
        private void ApplyPreset()
        {
            if (!_canEdit || _config == null) return;
            string id = SelectedPresetId(_cboPreset);
            PolicyPresets.PolicyPresetDef def = PolicyPresets.Find(id);
            if (def == null)
            {
                MessageBox.Show(this, "请先在下拉里选预置A、B 或 C，再点套用。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            // 脏先问存（与切节点同规矩：是=先存本节点，否=丢弃，取消=不套用）
            if (_dirty)
            {
                DialogResult r = MessageBox.Show(this,
                    "当前节点有未保存的修改，套用预置前保存吗？\n\n【是】保存后套用\n【否】丢弃后套用\n【取消】不套用",
                    "提示", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel) return;
                if (r == DialogResult.Yes && !SaveCurrentNode()) return;
                _dirty = false;
            }
            Dictionary<string, string> values = PolicyPresets.GetAllValues(id);
            if (values == null) return;
            // 差异预览：只列"真的会变"的项（中文名 + 现值→预置值；
            // 数值项（真空超时 0）同样列出，D 预置一次看全）
            var diffLines = new List<string>();
            foreach (string key in PolicyPresets.GovernedKeys)
            {
                string want;
                if (!values.TryGetValue(key, out want)) continue;
                string cur = GetConfigString(key);
                if (string.Equals(cur != null ? cur.Trim() : "",
                    want != null ? want.Trim() : "", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string label;
                if (!PolicyPresets.KeyLabels.TryGetValue(key, out label)) label = key;
                diffLines.Add("【" + label + "】 " + cur + " → " + want);
            }
            Dictionary<string, string> numerics = PolicyPresets.GetNumericValues(id);
            if (numerics != null)
            {
                foreach (var kv in numerics)
                {
                    string cur = GetConfigString(kv.Key);
                    if (string.Equals(cur != null ? cur.Trim() : "",
                        kv.Value != null ? kv.Value.Trim() : "", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    string label;
                    if (!PolicyPresets.KeyLabels.TryGetValue(kv.Key, out label)) label = kv.Key;
                    diffLines.Add("【" + label + "】 " + cur + " → " + kv.Value);
                }
            }
            if (diffLines.Count == 0)
            {
                MessageBox.Show(this, "当前已是【" + def.Title + "】，无需套用。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string confirm = "套用【" + def.Title + "】？\r\n\r\n" + def.Scenario + "\r\n"
                + (string.IsNullOrWhiteSpace(def.Requires)
                    ? "" : "\r\n前置条件：" + def.Requires + "\r\n")
                + "\r\n将修改 " + diffLines.Count + " 项：\r\n"
                + string.Join("\r\n", diffLines.ToArray())
                + "\r\n\r\n配方延时/时长/阈值/画面等配方项、MES/规则/报表/画面字典/点位等不动"
                + "（配方延时填0即阀电同开，无配方默认即0，有配方按配方来）。\r\n"
                + def.HowToSwitch;
            if (MessageBox.Show(this, confirm, "套用预置",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            {
                return;
            }
            // 逐项校验（预置值理论上全合法；真被拦就是预置本身写错了，报出来修预置）
            var invalid = new List<string>();
            foreach (var kv in values)
            {
                string error;
                if (!Dialogs.SettingsForm.ValidateValue(kv.Key, kv.Value, out error))
                {
                    invalid.Add("【" + kv.Key + "】 " + kv.Value + "  →  " + error);
                }
            }
            if (invalid.Count > 0)
            {
                MessageBox.Show(this, "预置本身写错了（不是您配错了），请联系开发：\r\n\r\n" +
                    string.Join("\r\n", invalid.ToArray()),
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            // 同一条保存路：组合校验 + 分流写文件 + 热回写全在里面
            Dialogs.SettingsForm.PersistResult presult;
            string perror;
            if (!Dialogs.SettingsForm.PersistChanges(_config, values, out presult, out perror))
            {
                MessageBox.Show(this, perror, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            foreach (string k in presult.SavedKeys) SavedKeys.Add(k);
            _dirty = false;
            // 内存已热回写：当前节点编辑器重建回显新值（不重建会显示旧值，误导人）
            RebuildEditors();
            if (_canvas != null) _canvas.RefreshCounts();
            RefreshPresetRow();
            string msg = "已套用【" + def.Title + "】并即时生效。";
            UpdateStatus(msg);
            MessageBox.Show(this, msg + "\r\n\r\n" + def.HowToSwitch,
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// 流程画布（自绘 Panel：固定拓扑 + 可拖节点 + 缩放平移 + 点击选中）。
        /// 几何全走 Scale()=_dpiScale×_zoom 一处；命中是绘制的逆运算。
        /// </summary>
        private class FlowCanvas : Panel
        {
            private readonly DeviceConfig _config;
            private readonly DeviceManager _deviceManager;

            private readonly Dictionary<string, Point> _positions = new Dictionary<string, Point>();
            private PolicyGraph.FlowCounts _counts;
            private string _selNode;
            private string _selEdge;

            private float _dpiScale = 1f;
            private float _zoom = 1f;
            private const float ZoomMin = 0.25f;
            private const float ZoomMax = 4f;

            private Font _fontTitle;
            private Font _fontSub;
            private Font _fontEdge;

            private string _dragNode;
            private Point _dragLastLogical;
            private bool _dragMoved;
            private bool _panning;
            private Point _panStartMouse;
            private Point _panStartScroll;

            public event Action<string> NodeSelected;
            public event Action<string> EdgeSelected;

            public FlowCanvas(DeviceConfig config, DeviceManager deviceManager)
            {
                _config = config;
                _deviceManager = deviceManager;
                this.DoubleBuffered = true;
                this.AutoScroll = true;
                this.BackColor = Color.White;
                this.Cursor = Cursors.Default;

                // 节点位置：缺省布局 + 用户存盘覆盖（存过才认，没存过走缺省）
                foreach (var n in PolicyGraph.Nodes)
                {
                    _positions[n.Id] = n.DefaultRect.Location;
                }
                foreach (var kv in PolicyGraph.LayoutStore.Load())
                {
                    if (_positions.ContainsKey(kv.Key)) _positions[kv.Key] = kv.Value;
                }

                this.MouseDown += OnMouseDown;
                this.MouseMove += OnMouseMove;
                this.MouseUp += OnMouseUp;
                RebuildFonts();
                RefreshCounts();
            }

            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                try { _dpiScale = this.CreateGraphics().DpiX / 96f; }
                catch { _dpiScale = 1f; }
                if (_dpiScale <= 0) _dpiScale = 1f;
                UpdateScrollSize();
            }

            private float Scale() { return _dpiScale * _zoom; }

            private void RebuildFonts()
            {
                if (_fontTitle != null) _fontTitle.Dispose();
                if (_fontSub != null) _fontSub.Dispose();
                if (_fontEdge != null) _fontEdge.Dispose();
                float t = Math.Max(6f, 10f * _zoom);
                float s = Math.Max(6f, 9f * _zoom);
                _fontTitle = new Font("微软雅黑", t, FontStyle.Bold);
                _fontSub = new Font("微软雅黑", s);
                _fontEdge = new Font("微软雅黑", Math.Max(6f, 8.5f * _zoom));
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    if (_fontTitle != null) _fontTitle.Dispose();
                    if (_fontSub != null) _fontSub.Dispose();
                    if (_fontEdge != null) _fontEdge.Dispose();
                }
                base.Dispose(disposing);
            }

            /// <summary>刷新实时台数并重画（窗体定时器每秒调一次）。</summary>
            public void RefreshCounts()
            {
                try
                {
                    var c = new PolicyGraph.FlowCounts();
                    if (_deviceManager != null)
                    {
                        int vac, age, done, fault, idle;
                        _deviceManager.GetPhaseCounts(out vac, out age, out done, out fault, out idle);
                        c.Vacuuming = vac;
                        c.Aging = age;
                        c.Completed = done;
                        c.Fault = fault;
                        c.Idle = idle;
                        try
                        {
                            var all = _deviceManager.GetAllBarometerData();
                            if (all != null)
                            {
                                foreach (var d in all)
                                {
                                    if (d != null && d.Status == Models.DeviceStatus.Completed
                                        && d.LastTestResult == "待判定") c.PendingJudge++;
                                }
                            }
                        }
                        catch { /* 计数失败按 0，不影响画布 */ }
                        try
                        {
                            var snap = Services.TestSessionStore.Load();
                            if (snap != null && snap.Stations != null) c.Snapshot = snap.Stations.Count;
                        }
                        catch { /* 无快照按 0 */ }
                    }
                    _counts = c;
                }
                catch { /* 计数永不拖垮绘制 */ }
                if (!this.IsDisposed) this.Invalidate();
            }

            /// <summary>复位布局（缺省位置 + 存盘 + 重画）。</summary>
            public void ResetLayout()
            {
                foreach (var n in PolicyGraph.Nodes)
                {
                    _positions[n.Id] = n.DefaultRect.Location;
                }
                PolicyGraph.LayoutStore.Save(new Dictionary<string, Point>(_positions));
                UpdateScrollSize();
                this.Invalidate();
            }

            public void SetSelected(string nodeId, string edgeId)
            {
                _selNode = nodeId;
                _selEdge = edgeId;
                this.Invalidate();
            }

            /// <summary>滚轮缩放（以鼠标为中心；IMessageFilter 预过滤后调这里）。</summary>
            public void ZoomAtCursor(int steps)
            {
                if (steps == 0) return;
                float old = _zoom;
                float next = old * (float)Math.Pow(1.2, steps);
                if (next < ZoomMin) next = ZoomMin;
                if (next > ZoomMax) next = ZoomMax;
                if (Math.Abs(next - old) < 0.001f) return;

                // 鼠标锚定：缩前鼠标下的逻辑点，缩后仍在鼠标下（AutoCAD 手感）
                Point mouse = this.PointToClient(Cursor.Position);
                double lx = (mouse.X - this.AutoScrollPosition.X) / (double)(_dpiScale * old);
                double ly = (mouse.Y - this.AutoScrollPosition.Y) / (double)(_dpiScale * old);
                _zoom = next;
                RebuildFonts();
                UpdateScrollSize();
                int sx = (int)(lx * _dpiScale * next - mouse.X);
                int sy = (int)(ly * _dpiScale * next - mouse.Y);
                // AutoScrollPosition setter 取正值（getter 吐负值，WinForms 老坑）
                this.AutoScrollPosition = new Point(Math.Max(0, sx), Math.Max(0, sy));
                this.Invalidate();
            }

            private void UpdateScrollSize()
            {
                int maxR = 0, maxB = 0;
                foreach (var n in PolicyGraph.Nodes)
                {
                    Point p = GetPos(n.Id);
                    maxR = Math.Max(maxR, p.X + n.DefaultRect.Width);
                    maxB = Math.Max(maxB, p.Y + n.DefaultRect.Height);
                }
                float s = Scale();
                this.AutoScrollMinSize = new Size(
                    (int)((maxR + 60) * s), (int)((maxB + 60) * s));
            }

            private Point GetPos(string id)
            {
                Point p;
                if (_positions.TryGetValue(id, out p)) return p;
                var def = PolicyGraph.FindNode(id);
                return def != null ? def.DefaultRect.Location : Point.Empty;
            }

            private Rectangle GetRect(PolicyGraph.NodeDef n)
            {
                Point p = GetPos(n.Id);
                return new Rectangle(p, n.DefaultRect.Size);
            }

            // ---- 坐标换算（device = logical×Scale + AutoScrollPosition） ----
            private int Dx(int logical) { return (int)(logical * Scale()) + this.AutoScrollPosition.X; }
            private int Dy(int logical) { return (int)(logical * Scale()) + this.AutoScrollPosition.Y; }
            private int ToLogicalX(int deviceX) { return (int)((deviceX - this.AutoScrollPosition.X) / Scale()); }
            private int ToLogicalY(int deviceY) { return (int)((deviceY - this.AutoScrollPosition.Y) / Scale()); }

            private void OnMouseDown(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Middle)
                {
                    // 中键平移：记起点（滚动值取正，见 setter 注释的老坑）
                    _panning = true;
                    _panStartMouse = e.Location;
                    _panStartScroll = new Point(-this.AutoScrollPosition.X, -this.AutoScrollPosition.Y);
                    this.Cursor = Cursors.SizeAll;
                    return;
                }
                if (e.Button != MouseButtons.Left) return;

                Point logical = new Point(ToLogicalX(e.X), ToLogicalY(e.Y));
                PolicyGraph.NodeDef hit = HitNode(logical);
                if (hit != null)
                {
                    // 点节点：选中 + 开始拖（up 时没动也算一次点击选中）
                    _dragNode = hit.Id;
                    _dragLastLogical = logical;
                    _dragMoved = false;
                    this.Cursor = Cursors.SizeAll;
                    if (NodeSelected != null) NodeSelected(hit.Id);
                    return;
                }
                PolicyGraph.EdgeDef edge = HitEdge(e.Location);
                if (edge != null)
                {
                    if (EdgeSelected != null) EdgeSelected(edge.Id);
                    return;
                }
                // 点空白：取消选中
                if (NodeSelected != null) NodeSelected(null);
            }

            private void OnMouseMove(object sender, MouseEventArgs e)
            {
                if (_panning)
                {
                    int nx = _panStartScroll.X - (e.X - _panStartMouse.X);
                    int ny = _panStartScroll.Y - (e.Y - _panStartMouse.Y);
                    this.AutoScrollPosition = new Point(Math.Max(0, nx), Math.Max(0, ny));
                    return;
                }
                if (_dragNode != null && (e.Button & MouseButtons.Left) == MouseButtons.Left)
                {
                    Point logical = new Point(ToLogicalX(e.X), ToLogicalY(e.Y));
                    int dx = logical.X - _dragLastLogical.X;
                    int dy = logical.Y - _dragLastLogical.Y;
                    if (dx != 0 || dy != 0)
                    {
                        Point p = GetPos(_dragNode);
                        _positions[_dragNode] = new Point(p.X + dx, p.Y + dy);
                        _dragLastLogical = logical;
                        _dragMoved = true;
                        UpdateScrollSize();
                        this.Invalidate();
                    }
                    return;
                }
            }

            private void OnMouseUp(object sender, MouseEventArgs e)
            {
                if (_panning && e.Button == MouseButtons.Middle)
                {
                    _panning = false;
                    this.Cursor = Cursors.Default;
                    return;
                }
                if (e.Button == MouseButtons.Left && _dragNode != null)
                {
                    string moved = _dragMoved ? _dragNode : null;
                    _dragNode = null;
                    _dragMoved = false;
                    this.Cursor = Cursors.Default;
                    if (moved != null)
                    {
                        // 拖完落定即存盘（下次打开接着用）
                        PolicyGraph.LayoutStore.Save(new Dictionary<string, Point>(_positions));
                        this.Invalidate();
                    }
                }
            }

            private PolicyGraph.NodeDef HitNode(Point logical)
            {
                // 倒序命中（后画的报警等侧栏优先，压边不断）
                for (int i = PolicyGraph.Nodes.Count - 1; i >= 0; i--)
                {
                    var n = PolicyGraph.Nodes[i];
                    if (GetRect(n).Contains(logical)) return n;
                }
                return null;
            }

            private PolicyGraph.EdgeDef HitEdge(Point device)
            {
                foreach (var e in PolicyGraph.Edges)
                {
                    Point a, b;
                    EdgeEndpoints(e, out a, out b);
                    if (DistToSegment(device, a, b) <= 7) return e;
                }
                return null;
            }

            private static double DistToSegment(Point p, Point a, Point b)
            {
                double dx = b.X - a.X, dy = b.Y - a.Y;
                double len2 = dx * dx + dy * dy;
                if (len2 < 1) return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
                double t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2;
                if (t < 0) t = 0;
                if (t > 1) t = 1;
                double cx = a.X + t * dx, cy = a.Y + t * dy;
                return Math.Sqrt((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy));
            }

            /// <summary>连线端点（设备坐标）：按两框相对位置选边（横向走左右，纵向走上下）。</summary>
            private void EdgeEndpoints(PolicyGraph.EdgeDef e, out Point a, out Point b)
            {
                var na = PolicyGraph.FindNode(e.From);
                var nb = PolicyGraph.FindNode(e.To);
                Rectangle ra = na != null ? GetRect(na) : Rectangle.Empty;
                Rectangle rb = nb != null ? GetRect(nb) : Rectangle.Empty;
                Point ca = new Point(ra.Left + ra.Width / 2, ra.Top + ra.Height / 2);
                Point cb = new Point(rb.Left + rb.Width / 2, rb.Top + rb.Height / 2);
                int dx = cb.X - ca.X, dy = cb.Y - ca.Y;
                Point pa, pb;
                if (Math.Abs(dx) >= Math.Abs(dy))
                {
                    if (dx >= 0) { pa = new Point(ra.Right, ca.Y); pb = new Point(rb.Left, cb.Y); }
                    else { pa = new Point(ra.Left, ca.Y); pb = new Point(rb.Right, cb.Y); }
                }
                else
                {
                    if (dy >= 0) { pa = new Point(ca.X, ra.Bottom); pb = new Point(cb.X, rb.Top); }
                    else { pa = new Point(ca.X, ra.Top); pb = new Point(cb.X, rb.Bottom); }
                }
                a = new Point(Dx(pa.X), Dy(pa.Y));
                b = new Point(Dx(pb.X), Dy(pb.Y));
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                Graphics g = e.Graphics;
                bool dark = false;
                try { dark = Services.ThemeManager.IsDark; }
                catch { dark = false; }
                this.BackColor = dark ? Color.FromArgb(24, 24, 24) : Color.White;
                g.Clear(this.BackColor);

                // 先画边（X 形交叉处边在下，节点在上）
                foreach (var edge in PolicyGraph.Edges)
                {
                    bool selected = string.Equals(_selEdge, edge.Id, StringComparison.Ordinal);
                    DrawEdge(g, edge, selected, dark);
                }
                // 后画节点
                foreach (var n in PolicyGraph.Nodes)
                {
                    bool selected = string.Equals(_selNode, n.Id, StringComparison.Ordinal);
                    DrawNode(g, n, selected, dark);
                }
            }

            private void DrawEdge(Graphics g, PolicyGraph.EdgeDef e, bool selected, bool dark)
            {
                Point a, b;
                EdgeEndpoints(e, out a, out b);
                Color c = selected ? Color.FromArgb(48, 119, 238)
                    : (dark ? Color.FromArgb(140, 140, 140) : Color.FromArgb(120, 120, 120));
                using (var pen = new Pen(c, selected ? 2.5f : 1.5f))
                {
                    if (e.Dashed) pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    g.DrawLine(pen, a, b);
                    // 箭头（末端两笔）
                    double ang = Math.Atan2(b.Y - a.Y, b.X - a.X);
                    int len = (int)(10 * Scale()) + 6;
                    Point p1 = new Point(
                        b.X - (int)(len * Math.Cos(ang - 0.42)),
                        b.Y - (int)(len * Math.Sin(ang - 0.42)));
                    Point p2 = new Point(
                        b.X - (int)(len * Math.Cos(ang + 0.42)),
                        b.Y - (int)(len * Math.Sin(ang + 0.42)));
                    g.DrawLine(pen, b, p1);
                    g.DrawLine(pen, b, p2);
                }
                // 条件标签（中点，白底/黑底衬一下，字不糊在线上）
                string label = PolicyGraph.BuildEdgeLabel(e.Id, _config);
                if (!string.IsNullOrEmpty(label))
                {
                    Point mid = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                    Size sz = TextRenderer.MeasureText(label, _fontEdge);
                    Rectangle bg = new Rectangle(mid.X - sz.Width / 2 - 3, mid.Y - sz.Height / 2 - 1,
                        sz.Width + 6, sz.Height + 2);
                    using (var brush = new SolidBrush(dark ? Color.FromArgb(24, 24, 24) : Color.White))
                    {
                        g.FillRectangle(brush, bg);
                    }
                    TextRenderer.DrawText(g, label, _fontEdge, bg,
                        dark ? Color.FromArgb(220, 220, 220) : Color.FromArgb(80, 80, 80),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }

            private void DrawNode(Graphics g, PolicyGraph.NodeDef n, bool selected, bool dark)
            {
                Rectangle lr = GetRect(n);
                Rectangle r = new Rectangle(Dx(lr.X), Dy(lr.Y),
                    (int)(lr.Width * Scale()), (int)(lr.Height * Scale()));
                Color head = NodeColor(n.Id, dark);
                Color body = dark ? Color.FromArgb(45, 45, 45) : Color.White;
                Color fore = dark ? Color.FromArgb(235, 235, 235) : Color.FromArgb(48, 48, 48);
                Color sub = dark ? Color.FromArgb(180, 180, 180) : Color.FromArgb(110, 110, 110);

                using (var bodyBrush = new SolidBrush(body))
                {
                    g.FillRectangle(bodyBrush, r);
                }
                int headH = Math.Max(18, (int)(26 * Scale()));
                using (var headBrush = new SolidBrush(head))
                {
                    g.FillRectangle(headBrush, new Rectangle(r.Left, r.Top, r.Width, headH));
                }
                TextRenderer.DrawText(g, n.Title, _fontTitle,
                    new Rectangle(r.Left + 6, r.Top, r.Width - 12, headH),
                    Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);

                string[] lines = PolicyGraph.BuildNodeLines(n.Id, _config, _counts);
                int y = r.Top + headH + 3;
                foreach (string line in lines)
                {
                    if (string.IsNullOrEmpty(line)) continue;
                    Size sz = TextRenderer.MeasureText(line, _fontSub);
                    if (y + sz.Height > r.Bottom - 2) break;
                    TextRenderer.DrawText(g, line, _fontSub,
                        new Rectangle(r.Left + 6, y, r.Width - 12, sz.Height + 2), sub);
                    y += sz.Height + 2;
                }

                using (var pen = new Pen(selected ? Color.FromArgb(48, 119, 238) : head,
                    selected ? 3f : 1.5f))
                {
                    Rectangle border = r;
                    border.Width -= 1;
                    border.Height -= 1;
                    g.DrawRectangle(pen, border);
                }
            }

            private static Color NodeColor(string id, bool dark)
            {
                // 语义色（红=报警，蓝=主链，灰=恢复/下料）；深色下压暗一档，字还是白字可读
                switch (id)
                {
                    case "start": return dark ? Color.FromArgb(46, 110, 60) : Color.FromArgb(62, 146, 82);
                    case "vacuum": return dark ? Color.FromArgb(36, 90, 160) : Color.FromArgb(48, 119, 238);
                    case "power": return dark ? Color.FromArgb(36, 90, 160) : Color.FromArgb(48, 119, 238);
                    case "done": return dark ? Color.FromArgb(70, 90, 140) : Color.FromArgb(90, 120, 190);
                    case "alarm": return dark ? Color.FromArgb(150, 50, 50) : Color.FromArgb(200, 60, 60);
                    case "recover": return dark ? Color.FromArgb(100, 100, 100) : Color.FromArgb(130, 130, 130);
                    case "unload": return dark ? Color.FromArgb(150, 110, 40) : Color.FromArgb(200, 150, 60);
                    default: return dark ? Color.FromArgb(100, 100, 100) : Color.FromArgb(130, 130, 130);
                }
            }
        }

        /// <summary>下拉选项（显示中文，存英文值；ToString 显示用）。</summary>
        private class FlowOpt
        {
            public readonly string Display;
            public readonly string Value;
            public FlowOpt(string display, string value) { Display = display; Value = value; }
            public override string ToString() { return Display; }
        }
    }
}
