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
    /// 这里改的正是与系统设置同源的策略 key（存 Policy.json），
    /// 名字与设置分类同词，所见即所得：改工艺来这里。
    /// （V2.0.5 起只放"现场按工艺要动的决策项"；MES 映射/报表列/破空阀点位迁回
    ///   系统设置，见 PolicyGraph.MovedKeyTargets 与各节点 Info。）
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
    /// │  [启动开阀]              [断电恢复]            │ 方案区        │
    /// │      │开阀                   ┊整台重测/续跑    │ [查看下拉]   │
    /// │  [抽真空]────────超时/失压/失联/规则───────┐   │ [套用到自定义]│
    /// │      │压力到位 且 延时时间到                          │ 说明一行     │
    /// │  [上电老化]────失压/失联/规则/超温───────┐   │ 选中节点标题  │
    /// │      │时长到 或 表达式                      [报警联动]        │
    /// │  [完成下电]──────────待取料/待判定──────→    │ [编辑器组]    │
    /// │                        └────────→                             │
    /// │                                     [MES上报]（无连线，纯配置）│
    /// ├──────────────────────────────────────────────┴───────────────┤
    /// │ 状态条：项目名 | 选中节点 | 保存提示                           │
    /// └──────────────────────────────────────────────────────────────┘
    /// 【方案区模型（2.0.1：下拉=看哪套，套用=复制到自定义，预置可临时改）】
    /// 右栏顶部只留两个控件，语义各一个，现场不用猜：
    /// - **下拉＝"我现在看的是哪套"**：自定义 / 预置A / 预置B / 预置C / 预置D
    ///   （自定义排第一、默认选中；开窗/保存/套用/导入后都回到"自定义"）。
    /// - **按钮＝"把这套模板复制到自定义"**：【套用到自定义】，只在选中预置时可用。
    /// 三种行为：
    /// - 选中**自定义**：画面＝当前生效的那套，节点**可改**，改完点【保存本节点】生效。
    /// - 选中**预置A/B/C/D**：画面＝该模板，节点**可临时改**（用户口径"预置还是需要支持修改"）：
    ///   【保存本节点】只**暂存**（内存，不落盘、不动生效配置），画面与右栏立刻显示暂存值。
    ///   要落地得点【套用到自定义】——模板定义值 ＋ 您的改动一起写进自定义并生效。
    ///   **切走方案或关窗时都会问一句**"是否套用该预置，并把改动同步到自定义？"
    ///   （是＝套用并同步保存；否＝不套用不保存，改动丢弃）。
    /// 【为什么这么设计（2.0.1 两轮改的由来）】
    /// 第一轮把预置做成"纯只读"（现场反馈"预置还是需要支持修改"）→ 改成可临时改 ＋ 暂存。
    /// 第二轮把"暂存按预置分别存"简化成**一份**（用户："简单点，不用按预置分别存"）——
    /// 离开当前预置时（切方案/关窗）必问一句"是否套用并同步"，问完就没了，
    /// 所以不存在"多个预置各留一份"的情况。
    /// 【临时修改存哪】`_presetTempEdits`（key→值，内存）＋ `_presetTempBaseId`（基于哪个预置）；
    /// 预览（画布/右栏）叠加这些暂存值——所见即所得。
    /// 【套用只动 14 个行为开关＋真空超时＋您改的那几项】MES/规则/报表/画面字典/点位/时长阈值不动；
    /// "导出策略"可选 A/B/C/D/自定义任一源装包（全量策略 key，含自由文本），
    /// "导入策略"一律进自定义槽并即时生效（A/B/C/D 覆盖不了）。只读模式套用/导入禁用，导出可用。
    /// 【V2.0.5 配置简化（客户反馈"配置项太多太复杂"）】
    /// ①只放"现场按工艺要动的决策项"：MES 映射/报表列/破空阀点位迁回系统设置
    ///   （PolicyGraph.MovedKeyTargets；节点保留只读状态 + "去哪改"的指引）；
    /// ②每节点分常用/高级，高级默认折叠成一行（控件一次建齐，只切可见性——切来切去不丢输入）；
    /// ③每项右侧标"默认/已改"（基准=项目初始策略：预置A管辖项取A，其余取出厂值；
    ///   不能拿 new DeviceConfig() 当基准——项目初始就是A，那样刚装好会一片【已改】）；
    /// ④高风险项（报警全关/跳过抽真空/超温联停）标签标红，不改交互（保存时已有组合校验）。
    /// 全部节点 Bool/Enum 下拉按预置下拉口径统一：
    /// 下拉列表按最长选项实测拉宽（SizeNodeCombo，最长 22 字项原来被截断）+
    /// 悬停看选中项全文（共享 _editorTip，切节点清表防钉住泄漏，随窗体释放）。
    /// 【鼠标操作】（对标 mFormFlowEdit 手感）
    /// - 左键拖节点体 = 移动节点（位置存 PolicyLayout.json，下次打开接着用）；
    /// - 左键点节点/连线 = 选中（右栏切出该节点的编辑器；连线只读，显示条件来源）；
    /// - 左键点空白 = 取消选中；Esc 同样取消；
    /// - **右键（画布空白处）= 弹出视图菜单**，当前只有一项**"适合图像"**：
    ///   把整张流程图还原成"刚打开窗口时"的样子——缩放回到打开时的默认大小（zoom=1）、
    ///   平移归零，并把整图摆到视野**居中**（节点/连线上右键不弹，别打断选中与拖动）。
    ///   与底部【复位布局】分工：复位布局＝节点位置回缺省（存盘）；适合图像＝**视图**回默认。
    /// - **滚轮按光标位置分工**：在**画布**上 = 以鼠标为中心缩放策略可视化图；
    ///   在**右栏**上 = 上下滚动右栏的编辑器区（滑块跟着走，**不是**缩放画布）。
    ///   【为什么右栏要手动驱动】滚轮消息只发给"有焦点的控件"——点过画布节点后焦点
    ///   就在画布上，鼠标停在右栏滚滚轮，右栏根本收不到消息（观感：右栏滚轮没反应）。
    ///   判定抽成纯函数 `ResolveWheelAction`（回归直接断言，不挪鼠标）；
    ///   滚动是**跟手 + 缓动**：滚轮只改目标位置，16ms 定时器每帧走 30% 滑过去
    ///   （高精度滚轮/触摸板的小增量按 1/120 单位累积成整像素，微移也跟手）。
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

        // —— 右栏行/组结构（V2.0.5 配置分层）——
        // 控件**一次建齐**（常用+高级都建），折叠只切 Visible + 重排 Location，
        // 不销毁重建：点"高级设置"不会丢没保存的输入（重建会丢，这是硬约束）。
        private readonly List<EditorRow> _editorRows = new List<EditorRow>();
        private readonly List<EditorGroup> _editorGroups = new List<EditorGroup>();
        private Sunny.UI.UILabel _advancedToggle;
        private int _headerOffset;
        private bool _advancedExpanded;
        private string _advancedExpandedNodeId;

        /// <summary>右栏一行：标签 + 默认/已改徽标 + 编辑器（控件常驻，折叠只切可见性）。</summary>
        private sealed class EditorRow
        {
            public PolicyGraph.NodeKey Key;
            public Sunny.UI.UILabel Label;
            public Sunny.UI.UILabel Badge;
            public Control Editor;
        }

        /// <summary>右栏一组（报警节点按"压力保护/温度保护/…"分组；其它节点一个无名组）。</summary>
        private sealed class EditorGroup
        {
            public string Name;
            public readonly List<EditorRow> Rows = new List<EditorRow>();
            public Sunny.UI.UILabel Title;
        }

        // 静态布局（坐标/文本/事件挂接）在 Designer，VS 可预览；
        // 这里只留运行时态：选项填充（数据源 PolicyPresets.All）+ 悬停提示 +
        // 下拉联动自保护旗。预置控件随窗体释放（静态挂接，无动态重建，终结器安全）。
        // _presetTip 无容器托管（本窗 Designer 无 components 容器），随窗体 Dispose
        // 手动释放（见本文件 Dispose 重写；R2 配对检查认方法体内真释放）。
        private ToolTip _presetTip;
        // 下拉联动自保护：程序回显选中项时不触发"用户改选"分支（只套用按钮才真干活）。
        private bool _presetRefreshing;
        // 窗体展示过（OnShown 置位）：关闭确认只对"真开过的窗"弹，构造冒烟/回归直调 Close 不扰民。
        private bool _shownOnce;
        // 关闭已确认（关闭按钮走完确认流程后置位，防 OnFormClosing 二次弹框）。
        private bool _closingConfirmed;

        // 与 _presetTip 同款的共享悬停提示（一个实例管全部
        // 节点下拉，选中项全文随选随换）。不给每个下拉各建一个：提示按控件建表
        // 强引用，旧编辑器释放时不清表 = 已释放控件被提示钉住不回收（切节点一
        // 次漏几个，小而久的托管泄漏）。所以切节点时 DisposeEditorControls 先
        // RemoveAll() 清表（见该方法注释），窗体释放时这里 Dispose（见 Dispose 重写）。
        private ToolTip _editorTip;

        // 窗体级预览引用（与画布 FlowCanvas._previewConfig 同源：UpdateCanvasPreview
        // 里配对赋值，null＝看回真实。右栏编辑器一律读 EffectiveConfig（预览??真实），
        // 与画布同一数据源——下拉切到哪，两边就一起看到哪，不再出现"画布 0ms、
        // 右栏 15000ms"的两边打架）。
        private DeviceConfig _previewConfig;
        // 下拉上次选项（脏确认弹回用：下拉切换时有未保存修改且用户选取消，
        // 下拉弹回此选项，画布/说明同步恢复，右栏没动过不用重建）。
        private string _lastPresetId;
        // 预览态标识（UpdatePresetDesc 里算好存下：右栏状态条直接读，不重复读槽文件。
        // _presetReadOnly＝当前查看的是预置模板（只读预览，改不了也存不了）；
        // _previewTitle＝选项中文名）。
        private bool _presetReadOnly;
        private string _previewTitle;

        // —— 右栏滚轮缓动（丝滑滚动）——
        // 滚轮事件只改"目标位置"，16ms 定时器每帧走 30% 滑过去（到位即停）。
        // _wheelRemainderPx：滚轮增量的像素余量（高精度滚轮/触摸板一格只给几个单位，
        //   靠它累积出整像素才动 → 微移平滑，不是一格一格跳）。
        // _scrollTarget：目标滚动距离（px，从顶部算）；_lastAppliedOffset：上次真正
        //   写进去的值（发现被外人改过＝用户自己拖了滑块 → 放弃动画跟随现状）。
        private Timer _scrollAnim;
        private double _wheelRemainderPx;
        private int _scrollTarget;
        private int _lastAppliedOffset;

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
            // 【Dock Fill 必须最后布局（V1.82 家规）】WinForms 按 **z 序从后往前** 布局 Dock，
            // 后面的先占边（Right/Bottom），`Fill` 必须处在 z 序**最前**才轮到最后布局、
            // 只吃"剩下的"客户区。而 `Controls.Add` 是**追加沉底**（后加的层级更靠后，
            // V1.86 血泪订正过"后 Add 压前面"的错觉）——所以光"最后 Add 画布"是不够的：
            // 画布会先布局、一把梭占满整个客户区（harness 实测 1156×940），右栏那 320px
            // 底下压着一层画布（绘制上是浮盖，但**画布的屏幕矩形把右栏整块包进去**）。
            // 后果不是好看不好看，是**命中**：滚轮"光标在画布上才缩放"的判定用的是画布矩形，
            // 鼠标停在右栏也命中 → 右栏滚滚轮，画布在缩放（现场报"右栏滚轮没反应、图还在缩"）。
            // 修法：加完统一 `BringToFront()` 一次（Fill 提到 z 序最前 → 最后布局 →
            // 只占右栏左侧那块 836×888），z 序反了画布会盖住右栏（Designer 头注已写明）。
            _canvas.BringToFront();

            SelectNode(null, false);
            // 自定义槽就地初始化（首次打开按 A 快照建槽，自由文本取当前种子；
            // 已有槽内容即直接返回，不覆盖。失败不拦开窗：回显按无槽走，保存/导入时再建）。
            if (_config != null)
            {
                try { ProjectPolicyStore.EnsureCustomInitialized(_config); }
                catch { /* 开窗不因建槽失败而拦人 */ }
            }
            RefreshPresetRow();
            // 开窗看真实：下拉回显"自定义"（唯一生效、唯一可改的那套），
            // 画面必须显示真实生效值（否则预置的数值项会把真实盖掉，
            // 现场看着像"配置没生效"）。用户主动切下拉时才按预置预览。
            RebuildPreviewFromReal();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _shownOnce = true;
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
                // 缓动定时器同上（无容器托管，必须手停手放，R6 配对锁）
                if (_scrollAnim != null) { _scrollAnim.Stop(); _scrollAnim.Dispose(); _scrollAnim = null; }
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
                    // 缓动定时器（无容器托管，OnFormClosed 已停过，这里再兜一次，
                    // 防止窗体没走 FormClosed 就被 Dispose 的路径留下野定时器）
                    if (_scrollAnim != null) { _scrollAnim.Stop(); _scrollAnim.Dispose(); _scrollAnim = null; }
                }
                catch { }
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// 居中偏移（纯函数：内容设备包围盒 + 视口尺寸 → 视图偏移）。
        /// 内容比视口大时偏移为负（内容左/上边出界，用滚动条找回来），这是"居中"的正常表现。
        /// 【为什么要有这个偏移】AutoScrollPosition 被钳在滚动范围内，而内容比视口小的
        /// 时候根本没有滚动范围（内容 615×810 &lt; 视口 836×888）→ 光靠滚动摆不到中央。
        /// </summary>
        public static Point ComputeCenterOffset(Rectangle contentBox, Size viewport)
        {
            return new Point(
                (viewport.Width - contentBox.Width) / 2 - contentBox.Left,
                (viewport.Height - contentBox.Height) / 2 - contentBox.Top);
        }

        // —— 预置的"临时修改"（2.0.1 用户口径：预置可以改，最终同步到自定义）——
        // key→值：预置视图下【保存本节点】存下来的东西（内存，不落盘、不动生效配置）。
        // 【只有一份，不按预置分】用户口径："简单点，不用按预置分别存"——
        // 离开当前预置时（切方案/关窗）必问一句"是否套用并同步"，问完就没了，
        // 所以不存在"多个预置各留一份"的情况。
        // _presetTempBaseId：这份修改是基于哪个预置（提示文案要用）。
        private readonly Dictionary<string, string> _presetTempEdits
            = new Dictionary<string, string>(StringComparer.Ordinal);
        private string _presetTempBaseId;

        /// <summary>
        /// 滚轮动作判定（纯函数：光标位置 → 该干什么）。**回归直接断言这个函数**，
        /// 不用真的挪鼠标（挪鼠标会打扰现场，也会在无人值守会话里不可靠）。
        /// 【右栏优先】两个区域万一又叠上（布局回退），按"光标在右栏"处理，
        /// 宁可右栏滚也不要画布缩放——用户 complaining 的是"鼠标在右栏，图却缩了"。
        /// </summary>
        public enum WheelAction
        {
            /// <summary>不处理（状态条等其它区域，交给系统默认）。</summary>
            None = 0,
            /// <summary>缩放策略可视化图（光标在画布上）。</summary>
            ZoomCanvas = 1,
            /// <summary>滚动右栏编辑器区（光标在右栏上）。</summary>
            ScrollRightPanel = 2
        }

        /// <summary>
        /// 滚轮分流判定（纯函数）。
        /// </summary>
        /// <param name="cursor">光标的屏幕坐标</param>
        /// <param name="rightPanelScreen">右栏的屏幕矩形</param>
        /// <param name="canvasScreen">画布的屏幕矩形</param>
        public static WheelAction ResolveWheelAction(
            Point cursor, Rectangle rightPanelScreen, Rectangle canvasScreen)
        {
            if (rightPanelScreen.Contains(cursor)) return WheelAction.ScrollRightPanel;
            if (canvasScreen.Contains(cursor)) return WheelAction.ZoomCanvas;
            return WheelAction.None;
        }

        /// <summary>
        /// 滚轮消息预过滤：**按光标位置分工**（2.0.1 用户要求）。
        /// - 光标在**画布**上 → 缩放策略可视化图（沿用原逻辑，吞掉消息）；
        /// - 光标在**右栏**上 → 上下滚动右栏的编辑器区（滑块跟着走），**不是**缩放画布。
        /// 【为什么右栏要手动驱动滚动】滚轮消息只发给"有焦点的控件"——点过画布节点后
        /// 焦点就在画布上，鼠标停在右栏滚滚轮，右栏**根本收不到消息**，
        /// 现场观感就是"右栏滚轮没反应、画布也不动"。所以这里按光标位置主动分流：
        /// 右栏的 `AutoScroll` 由本方法驱动（滚动条与内容同步动）。
        /// 【消费原则】右栏区域一律吞掉滚轮消息（哪怕右栏已到头没得滚）——
        /// 放行的话消息会落到焦点控件（画布）上，出现"鼠标在右栏、画布却缩放了"的怪象。
        /// </summary>
        public bool PreFilterMessage(ref Message m)
        {
            const int WM_MOUSEWHEEL = 0x020A;
            if (m.Msg != WM_MOUSEWHEEL) return false;
            if (_canvas == null || _canvas.IsDisposed) return false;
            try
            {
                int delta = (short)((m.WParam.ToInt32() >> 16) & 0xFFFF);
                if (delta == 0) return false;
                Point cur = Cursor.Position;
                Rectangle rightScreen = Rectangle.Empty;
                try
                {
                    if (_pnlRight != null && !_pnlRight.IsDisposed)
                    {
                        rightScreen = _pnlRight.RectangleToScreen(_pnlRight.ClientRectangle);
                    }
                }
                catch { rightScreen = Rectangle.Empty; }
                Rectangle canvasScreen = _canvas.RectangleToScreen(_canvas.ClientRectangle);
                switch (ResolveWheelAction(cur, rightScreen, canvasScreen))
                {
                    case WheelAction.ScrollRightPanel:
                        ScrollRightPanelByWheel(delta);
                        return true;
                    case WheelAction.ZoomCanvas:
                        _canvas.ZoomAtCursor(delta > 0 ? 1 : -1);
                        return true;
                    default:
                        return false;
                }
            }
            catch { return false; }
        }

        /// <summary>
        /// 右栏滚轮：**跟手 + 缓动**（2.0.1 用户"我要的是丝滑"）。
        /// 【三个真 bug（harness 实测抓到的，不是手感问题）】
        /// ① **范围算法错**（"时好时不好"的真凶）：AutoScroll 滚动时**子控件会跟着上移**
        ///    （实测 firstChildTop 0→-50、子控件底边 658→608），拿"子控件最大底边"当
        ///    内容高度 → 滚完第一格高度就缩到等于客户区高 → 判定"内容没超出、不用滚"
        ///    → **后面每一格都不动**。可滚范围必须用 `DisplayRectangle.Height`（内容总高，
        ///    不随滚动变），子控件底边只作兜底（且要加回当前偏移）。
        /// ② **方向反了**：WM_MOUSEWHEEL 的 delta&gt;0 = 轮子**往前**滚 = 看**更上面**的内容
        ///    （Windows 原生行为，harness 对照 SendMessage 证实），早前实现按"正数往下"
        ///    写反了，滚轮往上推列表却往下跑。
        /// ③ **一格跳太多**：可滚范围常常只有几十 px，一格跳 57px → 要么跳到底、
        ///    要么没得动。改成"滚轮增量按 1/120 单位换算成像素、余量累积到整像素才动"，
        ///    高精度滚轮/触摸板的小增量也能平滑出 1~2px 的微移。
        /// 【丝滑从哪来】滚轮事件只改**目标位置**，由 16ms 定时器**缓动**过去
        /// （每帧走 30%），到位即停 → 观感是"滑过去"而不是"啪地跳一格"；
        /// 用户自己拖了滑块（位置被外人改过）就放弃动画、跟随现状。
        /// </summary>
        private void ScrollRightPanelByWheel(int delta)
        {
            if (_pnlEditors == null || _pnlEditors.IsDisposed) return;
            if (!_pnlEditors.AutoScroll) return;
            int range = ComputeScrollRange(
                _pnlEditors.DisplayRectangle.Height,
                _pnlEditors.ClientSize.Height,
                Math.Abs(_pnlEditors.AutoScrollPosition.Y),
                GetEditorsContentBottom());
            if (range <= 0) return;   // 内容没超出，没有可滚范围
            int step = Math.Max(1, _pnlEditors.Font.Height * 3);   // 一格 = 3 行（系统手感）
            double remainder;
            int cur = Math.Abs(_pnlEditors.AutoScrollPosition.Y);
            int target = ComputeScrollTarget(cur, _wheelRemainderPx, delta, step, range,
                out remainder);
            _wheelRemainderPx = remainder;
            if (target == _scrollTarget && _scrollAnim != null && _scrollAnim.Enabled) return;
            _scrollTarget = target;
            EnsureScrollAnim();
        }

        /// <summary>右栏可滚距离（纯函数：内容高/客户区高/当前偏移/子控件底边 → 可滚 px）。
        /// 【为什么要"子控件底边 + 当前偏移"兜底】AutoScroll 滚动时子控件跟着上移，
        /// 单看底边会越滚越小（bug ①）；加回偏移才是真实内容高度。</summary>
        public static int ComputeScrollRange(
            int contentHeight, int clientHeight, int currentOffset, int childrenBottom)
        {
            int h = contentHeight;
            int byChildren = childrenBottom + currentOffset;
            if (byChildren > h) h = byChildren;
            int range = h - clientHeight;
            return range > 0 ? range : 0;
        }

        /// <summary>滚轮目标位置（纯函数：当前偏移/余量/滚轮增量/步长/可滚范围 → 目标偏移）。
        /// 【方向】delta&gt;0 = 往前滚 = 看更上面的内容 → 目标偏移**变小**。
        /// 【余量累积】增量按 1/120 单位换算成像素，截断取整、余下的小数留到下一次
        /// （高精度滚轮/触摸板一格只给几个单位，靠这个才能平滑微移）。</summary>
        public static int ComputeScrollTarget(
            int currentOffset, double remainderPx, int delta, int step, int range,
            out double newRemainder)
        {
            remainderPx += delta / 120.0 * step;
            int whole = (int)remainderPx;          // 截断（向零取整），小数留到下次
            newRemainder = remainderPx - whole;
            int target = currentOffset - whole;     // 正增量 → 往上看（偏移变小）
            if (target < 0) target = 0;
            if (target > range) target = range;
            return target;
        }

        /// <summary>右栏内容高度（子控件最大底边；随滚动会变小，只作兜底用）。</summary>
        private int GetEditorsContentBottom()
        {
            int bottom = 0;
            try
            {
                foreach (Control c in _pnlEditors.Controls)
                {
                    if (c.Bottom > bottom) bottom = c.Bottom;
                }
            }
            catch { }
            return bottom;
        }

        /// <summary>起滚轮缓动定时器（16ms；只在需要时起，到位即停）。</summary>
        private void EnsureScrollAnim()
        {
            if (_scrollAnim == null)
            {
                _scrollAnim = new Timer { Interval = 16 };
                _scrollAnim.Tick += ScrollAnimTick;
            }
            if (!_scrollAnim.Enabled) _scrollAnim.Start();
        }

        /// <summary>停缓动（到位/关窗/重建右栏时调）。</summary>
        private void StopScrollAnim()
        {
            try { if (_scrollAnim != null) _scrollAnim.Stop(); } catch { }
        }

        /// <summary>放弃缓动、跟随当前位置（用户自己拖了滑块，或右栏被重建）。</summary>
        private void CancelScrollAnim()
        {
            StopScrollAnim();
            _wheelRemainderPx = 0;
            try
            {
                _scrollTarget = _pnlEditors != null && !_pnlEditors.IsDisposed
                    ? Math.Abs(_pnlEditors.AutoScrollPosition.Y) : 0;
                _lastAppliedOffset = _scrollTarget;
            }
            catch { _scrollTarget = 0; _lastAppliedOffset = 0; }
        }

        /// <summary>缓动一帧：当前位置向目标走 30%（到位即停；被外人改过位置就跟随现状）。</summary>
        private void ScrollAnimTick(object sender, EventArgs e)
        {
            try
            {
                if (IsDisposed || Disposing) return;
                if (_pnlEditors == null || _pnlEditors.IsDisposed) { StopScrollAnim(); return; }
                int cur = Math.Abs(_pnlEditors.AutoScrollPosition.Y);
                // 位置不是我们写的 → 用户自己拖了滑块：放弃动画目标，跟随现状
                if (cur != _lastAppliedOffset) _scrollTarget = cur;
                if (cur == _scrollTarget) { StopScrollAnim(); return; }
                int next = cur + (int)((_scrollTarget - cur) * 0.3);
                if ((_scrollTarget > cur && next <= cur) || (_scrollTarget < cur && next >= cur))
                {
                    next = _scrollTarget;
                }
                ApplyRightPanelScroll(next);
            }
            catch { StopScrollAnim(); }
        }

        /// <summary>写入滚动位置（setter 只收正值；写完回读，钳制后的值才是真值）。</summary>
        private void ApplyRightPanelScroll(int offset)
        {
            if (offset < 0) offset = 0;
            _pnlEditors.AutoScrollPosition = new Point(0, offset);
            int actual = Math.Abs(_pnlEditors.AutoScrollPosition.Y);
            _lastAppliedOffset = actual;
            // 请求值被钳掉（内容变短/已到底）：目标不可达，立即收手。
            // 不这么做的话定时器会每帧再请求同一个值、被钳回，16ms 空转不停。
            if (actual != offset)
            {
                _scrollTarget = actual;
                StopScrollAnim();
            }
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
            _editorRows.Clear();
            _editorGroups.Clear();
            _advancedToggle = null;
            _headerOffset = 0;
        }

        /// <summary>
        /// 按选中节点重建右栏编辑器。
        /// 【V2.0.5 配置分层】每个节点分"常用/高级"：常用默认展开，高级默认折叠成
        /// 一行"高级设置（N 项）>>"。控件**一次建齐**，折叠只切 Visible + 重排 Location
        /// （见 LayoutEditorRows）——不销毁重建，所以点折叠不会丢没保存的输入。
        /// 报警节点另按 Group 分组显示（压力保护/温度保护/报警行为/失压与责任/通讯与规则）。
        /// </summary>
        private void RebuildEditors()
        {
            DisposeEditorControls();
            _dirty = false;
            // 换节点＝换一份编辑器清单，滚动位置归零（列表从头看），缓动也一并取消
            // （旧的缓动目标已经无意义，否则它会把新清单拉回旧位置）。
            CancelScrollAnim();
            try { if (_pnlEditors != null && !_pnlEditors.IsDisposed) _pnlEditors.AutoScrollPosition = new Point(0, 0); }
            catch { }

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
            // 【只读两态】①非管理员（原本就只读）②正在看预置模板（模板本身不可改，
            // 保存只是暂存，最终要套用到自定义才生效）。
            bool viewingPreset = IsViewingPreset();
            _lblNodeTitle.Text = viewingPreset ? def.Title + "（预置编辑中）" : def.Title;
            _btnSaveNode.Enabled = _canEdit && def.Keys.Count > 0;

            // 切到另一个节点时高级区默认收起；同一节点重建（保存后）保持上次展开状态，
            // 免得"展开高级→保存→又被折叠"。
            if (!string.Equals(_advancedExpandedNodeId, _selectedId, StringComparison.Ordinal))
            {
                _advancedExpanded = false;
                _advancedExpandedNodeId = _selectedId;
            }

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
            else if (viewingPreset)
            {
                // 预置视图可改，但改的是"临时态"：把这层意思说在编辑器顶上
                // （说明行只有两行、状态条会被冲掉，这里必须点明"存了还没生效"）。
                // 【每行都要装得下】编辑器区宽 270、宋体 12pt 实测约 16.7px/字 ⇒ 每行最多 16 字。
                var ro = new Sunny.UI.UILabel
                {
                    Location = new Point(0, y),
                    Size = new Size(270, 62),
                    Text = "预置可临时改，保存只是暂存。\r\n要生效：点【套用到自定义】。",
                    ForeColor = Color.Gray
                };
                _pnlEditors.Controls.Add(ro);
                y += 66;
            }
            else
            {
                // 图例：现场先看懂"默认/已改"，再决定动不动（客户原话"不知道哪些该动"）。
                var legend = new Sunny.UI.UILabel
                {
                    Location = new Point(0, y),
                    Size = new Size(270, 40),
                    Text = "【默认】=初始值，一般不用动\r\n【已改】=本项目调过",
                    ForeColor = Color.Gray
                };
                _pnlEditors.Controls.Add(legend);
                // "初始值"是哪个值必须能查：预置 A 管辖项取 A，其余取出厂值。
                // 不写清楚的话，现场会拿系统设置里的出厂说明来对，对不上就以为程序乱标。
                try
                {
                    if (_editorTip == null) _editorTip = new ToolTip();
                    _editorTip.SetToolTip(legend,
                        "初始值＝预置A（常用）管辖项取A的值，其余取出厂值。\r\n改过并保存后显示【已改】。");
                }
                catch { /* 提示失败不影响主流程 */ }
                y += 44;
            }
            _headerOffset = y;

            // 一次建齐所有行（含高级），折叠只切可见性。组按 def.Keys 顺序切分。
            EditorGroup currentGroup = null;
            foreach (PolicyGraph.NodeKey key in def.Keys)
            {
                if (key.Group == null)
                {
                    if (currentGroup == null || currentGroup.Name != null)
                    {
                        currentGroup = new EditorGroup { Name = null };
                        _editorGroups.Add(currentGroup);
                    }
                }
                else if (currentGroup == null || !string.Equals(currentGroup.Name, key.Group, StringComparison.Ordinal))
                {
                    currentGroup = new EditorGroup { Name = key.Group };
                    _editorGroups.Add(currentGroup);
                }

                var row = new EditorRow { Key = key };
                var lbl = new Sunny.UI.UILabel
                {
                    Location = new Point(0, 0),
                    Size = new Size(220, 20),
                    Text = key.Label
                };
                // 高风险项标红（不弹二次确认：保存时已有组合校验，避免双弹）。
                if (PolicyGraph.HighRiskKeys.Contains(key.Key)) lbl.ForeColor = Color.Firebrick;
                _pnlEditors.Controls.Add(lbl);
                row.Label = lbl;

                // 标题 tooltip（与系统设置表同源；超 40 字走 WrapTooltip 换行）。
                try
                {
                    if (_editorTip == null) _editorTip = new ToolTip();
                    string desc = Dialogs.SettingsForm.GetDescription(key.Key);
                    if (string.IsNullOrEmpty(desc)) desc = key.Label;
                    _editorTip.SetToolTip(lbl, Dialogs.SettingsForm.WrapTooltip(desc));
                }
                catch { /* 提示写失败不影响主流程（纯展示） */ }

                // 默认/已改徽标：预置视图不显示（预置值 vs 项目初始值没有"本项目改没改"的语义）。
                // 基准取"项目初始值"（A 管辖项取 A，其余出厂值）——不能用 new DeviceConfig()：
                // 项目初始就是 A，A 与代码出厂值本就不同，那样刚装好就一片【已改】。
                if (!viewingPreset)
                {
                    string cur = GetConfigString(key.Key);
                    string baseline = PolicyGraph.GetBaselineString(key.Key);
                    bool changed = !string.Equals(cur, baseline, StringComparison.OrdinalIgnoreCase);
                    var badge = new Sunny.UI.UILabel
                    {
                        Location = new Point(224, 0),
                        Size = new Size(46, 20),
                        Text = changed ? "已改" : "默认",
                        ForeColor = changed ? Color.DodgerBlue : Color.Gray,
                        TextAlign = ContentAlignment.MiddleRight
                    };
                    _pnlEditors.Controls.Add(badge);
                    row.Badge = badge;
                }

                Control editor = CreateEditor(key.Key, key.Kind);
                editor.Location = new Point(0, 0);
                editor.Width = 270;
                // 选项框按预置下拉口径统一：下拉列表按最长选项实测拉宽 +
                // 悬停看选中项全文（见 SizeNodeCombo）。宽 270 定死后才能量（量的是
                // 像素，基准就是这个 270），所以调口必须在 Width 赋值之后。
                var nodeCmb = editor as Sunny.UI.UIComboBox;
                if (nodeCmb != null) SizeNodeCombo(nodeCmb);
                // 只读模式：控件禁用（与系统设置"仅管理员可改"同级，不开后门）
                // 预置视图**不**禁用（预置可以临时改，最终同步到自定义）
                editor.Enabled = _canEdit;
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
                row.Editor = editor;
                currentGroup.Rows.Add(row);
                _editorRows.Add(row);
                _editorControls[key.Key] = editor;

                // 组标题：每个组建一个，Layout 时按"该组本层是否有可见行"决定显隐。
                if (currentGroup.Title == null && !string.IsNullOrEmpty(currentGroup.Name))
                {
                    var gt = new Sunny.UI.UILabel
                    {
                        Location = new Point(0, 0),
                        Size = new Size(270, 20),
                        Text = currentGroup.Name,
                        ForeColor = Color.Gray
                    };
                    _pnlEditors.Controls.Add(gt);
                    currentGroup.Title = gt;
                }
            }

            // 高级折叠开关（只有存在高级项才建）：放在常用项之后、高级项之前。
            int advancedCount = 0;
            foreach (var row in _editorRows)
            {
                if (row.Key.Tier == PolicyGraph.KeyTier.Advanced) advancedCount++;
            }
            if (advancedCount > 0)
            {
                _advancedToggle = new Sunny.UI.UILabel
                {
                    Location = new Point(0, 0),
                    Size = new Size(270, 24),
                    Cursor = Cursors.Hand,
                    ForeColor = Color.DodgerBlue
                };
                _advancedToggle.Click += (s, e) => ToggleAdvanced();
                _pnlEditors.Controls.Add(_advancedToggle);
            }

            int contentBottom = LayoutEditorRows();

            if (def.Keys.Count == 0)
            {
                var info = new Sunny.UI.UILabel
                {
                    Location = new Point(0, contentBottom),
                    Size = new Size(270, 96),
                    Text = def.Info,
                    ForeColor = Color.Gray
                };
                _pnlEditors.Controls.Add(info);
                _btnSaveNode.Enabled = false;
                try { _pnlEditors.AutoScrollMinSize = new Size(0, contentBottom + 100); } catch { }
            }
            else if (!string.IsNullOrEmpty(def.Info))
            {
                // 有 key 也有说明的节点（完成下电/MES）：编辑器下面追加灰字指引，
                // 告诉现场"迁走的那几项去哪改"。
                var foot = new Sunny.UI.UILabel
                {
                    Location = new Point(0, contentBottom),
                    Size = new Size(270, 60),
                    Text = def.Info,
                    ForeColor = Color.Gray
                };
                _pnlEditors.Controls.Add(foot);
                try { _pnlEditors.AutoScrollMinSize = new Size(0, contentBottom + 64); } catch { }
            }
            UpdateStatus(viewingPreset
                ? $"已选中【{def.Title}】（预置编辑中）：改完点“保存本节点”只是暂存，套用到自定义后才生效。"
                : $"已选中【{def.Title}】，改完点“保存本节点”。");
        }

        /// <summary>
        /// 展开/收起高级设置（只切可见性 + 重排，不重建控件——值不丢）。
        /// </summary>
        private void ToggleAdvanced()
        {
            _advancedExpanded = !_advancedExpanded;
            _advancedExpandedNodeId = _selectedId;
            LayoutEditorRows();
            UpdateStatus(_advancedExpanded ? "已展开高级设置。" : "已收起高级设置。");
        }

        /// <summary>
        /// 按"常用 → 折叠开关 → 高级"重排右栏（可见性 + Location，不销毁控件）。
        /// 返回内容底部 y（页脚 Info/AutoScrollMinSize 用）。
        /// </summary>
        private int LayoutEditorRows()
        {
            // 先统一隐藏所有行/组标题，再按层摆位。
            // 【为什么不能边排边藏】折叠时只排"常用"层，纯高级组（本层无行）会被
            // 整组跳过——若指望"没排到"自然隐藏，那些高级行会保持 Visible=true
            // 且停在 (0,0)，叠在常用项上（V2.0.5 harness 实测：折叠后 8 行全在某处）。
            HideAllEditorRows();
            UpdateGroupTitleVisibility();

            int y = _headerOffset;
            y = LayoutTier(PolicyGraph.KeyTier.Common, y);

            if (_advancedToggle != null)
            {
                int advancedCount = 0;
                foreach (var row in _editorRows)
                {
                    if (row.Key.Tier == PolicyGraph.KeyTier.Advanced) advancedCount++;
                }
                _advancedToggle.Location = new Point(0, y);
                _advancedToggle.Text = _advancedExpanded
                    ? "收起高级设置 <<"
                    : "高级设置（" + advancedCount + " 项） >>";
                _advancedToggle.Visible = true;
                y += 28;
            }

            if (_advancedExpanded) y = LayoutTier(PolicyGraph.KeyTier.Advanced, y);

            try { _pnlEditors.AutoScrollMinSize = new Size(0, y); } catch { }
            ClampEditorScrollToContent();
            // 布局还没跑完时 AutoScroll 范围可能仍是旧值：排队在布局完成后再钳一次。
            // （内容变矮后滚动条/滚动位置不自动收敛是 WinForms 老问题，V1.88.15 同源）
            if (IsHandleCreated && !IsDisposed)
            {
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (!IsDisposed && !Disposing) ClampEditorScrollToContent();
                    });
                }
                catch { /* 窗已关：无需校正 */ }
            }
            return y;
        }

        /// <summary>
        /// 组标题显隐按"当前可见层集合"一次判定：某组在常用层有行，或高级已展开且组内有高级行，
        /// 标题就显示。
        /// 【为什么不放在 LayoutTier 里】排高级层时会把"只有常用行"的组标题关掉——
        /// 常用层刚打开的标题又被后一次布局覆盖（V2.0.5 harness 实锤：展开后
        /// 压力保护/温度保护标题消失，上方留一段空白）。
        /// </summary>
        private void UpdateGroupTitleVisibility()
        {
            foreach (var group in _editorGroups)
            {
                if (group.Title == null) continue;
                bool any = false;
                foreach (var row in group.Rows)
                {
                    if (row.Key.Tier == PolicyGraph.KeyTier.Common || _advancedExpanded)
                    {
                        any = true;
                        break;
                    }
                }
                group.Title.Visible = any;
            }
        }

        /// <summary>隐藏全部行与组标题（布局的唯一前置状态；可见性只由 LayoutTier 打开）。</summary>
        private void HideAllEditorRows()
        {
            foreach (var group in _editorGroups)
            {
                if (group.Title != null) group.Title.Visible = false;
                foreach (var row in group.Rows)
                {
                    row.Label.Visible = false;
                    if (row.Badge != null) row.Badge.Visible = false;
                    row.Editor.Visible = false;
                }
            }
        }

        /// <summary>
        /// 把滚动位置与缓动目标钳进当前内容范围。
        /// 【为什么要钳缓动目标】折叠后内容变矮、旧目标超出可滚范围，定时器每帧
        /// 请求同一个不可达位置（setter 被钳回），会 16ms 空转不停。
        /// </summary>
        private void ClampEditorScrollToContent()
        {
            if (_pnlEditors == null || _pnlEditors.IsDisposed) return;
            try
            {
                int maxOffset = Math.Max(0,
                    _pnlEditors.AutoScrollMinSize.Height - _pnlEditors.ClientSize.Height);
                if (_scrollTarget > maxOffset) _scrollTarget = maxOffset;
                int cur = Math.Abs(_pnlEditors.AutoScrollPosition.Y);
                if (cur > maxOffset) _pnlEditors.AutoScrollPosition = new Point(0, maxOffset);
                _lastAppliedOffset = Math.Abs(_pnlEditors.AutoScrollPosition.Y);
            }
            catch { }
        }

        /// <summary>
        /// 排某一层（常用/高级）的可见行并摆位。
        /// 前置条件：HideAllEditorRows + UpdateGroupTitleVisibility 已定好隐藏/显示集合；
        /// 本方法只负责"打开本层行"，不再改标题可见性（否则后一层会覆盖前一层的判定）。
        /// 【行高】单行 22(标签)+26(框)+6(间距)=54；多行 22+96+6=124，与原平铺口径一致。
        /// </summary>
        private int LayoutTier(PolicyGraph.KeyTier tier, int y)
        {
            foreach (var group in _editorGroups)
            {
                // 只有"本层有行"的组才在本层给标题定位：常用组标题在排高级层时
                // 不能再被搬下来（否则标题跑到展开开关后面，V2.0.5 harness 实锤）。
                bool anyThisTier = false;
                foreach (var row in group.Rows)
                {
                    if (row.Key.Tier == tier) { anyThisTier = true; break; }
                }
                if (anyThisTier && group.Title != null && group.Title.Visible)
                {
                    // 组与组之间留 8px（首个组不垫，避免图例下方多出空白）。
                    if (y > _headerOffset) y += 8;
                    group.Title.Location = new Point(0, y);
                    y += 20;
                }
                // 非本层行保持 HideAllEditorRows 的隐藏态（不能靠 continue 隐藏：纯高级组
                // 在折叠时整组都不会进到这里，必须由前置统一隐藏兜住）。
                foreach (var row in group.Rows)
                {
                    if (row.Key.Tier != tier) continue;
                    row.Label.Visible = true;
                    if (row.Badge != null) row.Badge.Visible = true;
                    row.Editor.Visible = true;

                    row.Label.Location = new Point(0, y);
                    if (row.Badge != null) row.Badge.Location = new Point(224, y);
                    row.Editor.Location = new Point(0, y + 22);
                    row.Editor.Width = 270;
                    int editorH = row.Key.Kind == PolicyGraph.EditorKind.Multiline ? 96 : 26;
                    row.Editor.Height = editorH;
                    y += 22 + editorH + 6;
                }
            }
            return y;
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
            // 布尔下拉显示中文（关闭/开启），存英文值（false/true，与设置表/落盘口径一致）：
            // 现场看 true/false 看不懂，显示与存储分离（FlowOpt.Display/Value，读存只认 Value）。
            if (kind == PolicyGraph.EditorKind.Bool)
            {
                var cmb = new Sunny.UI.UIComboBox { DropDownStyle = Sunny.UI.UIDropDownStyle.DropDownList };
                cmb.Items.Add(new FlowOpt("关闭", "false"));
                cmb.Items.Add(new FlowOpt("开启", "true"));
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

        /// <summary>读有效配置字符串（右栏与画布同源：有预览看预览，平时即真实）。</summary>
        private string GetConfigString(string key)
        {
            return ReadConfigString(EffectiveConfig(), key);
        }

        /// <summary>
        /// 读**真实生效配置**的字符串值（不看预览）。
        /// 【为什么单独一口】GetConfigString 读 EffectiveConfig（有预览看预览），
        /// 那口径是给"画面显示"用的：预置视图下预览已经套上了预置值。
        /// 拿它当"当前值"去比 → 差异恒为 0 → 误判"当前已是该预置，无需套用"，
        /// 套用根本不会执行（现场实锤：点【套用到自定义】永远弹"无需套用"，
        /// 切到自定义里看节点什么都没变，用户根本不知道到底成没成）。
        /// 套用＝把预置覆盖到【自定义】上，比较基准必须是当前真正生效的那一套。
        /// </summary>
        private string GetRealConfigString(string key)
        {
            return ReadConfigString(_config, key);
        }

        /// <summary>
        /// 按指定配置对象读 key 的存储口径字符串（真实/预览两个口径共用）。
        /// 反射实现统一在 `PolicyGraph.ReadConfigString`（与默认值徽标同源，
        /// 避免窗体与图数据各抄一份、改口径时分叉）。
        /// </summary>
        private static string ReadConfigString(DeviceConfig cfg, string key)
        {
            return PolicyGraph.ReadConfigString(cfg, key);
        }

        /// <summary>
        /// 右栏/画布统一数据源：有预览看预览（下拉切到哪看到哪），平时即真实 _config。
        /// 【为什么右栏也要看预览】画布一直是这个口径，右栏以前只看真实，
        /// 下拉一切两边就打架（画布 0ms、右栏 15000ms）。统一后两边永远同源；
        /// 预览是只读副本，右栏改了点保存即把该节点的值写入真实（所见即所得）。
        /// </summary>
        private DeviceConfig EffectiveConfig() { return _previewConfig ?? _config; }

        private void BtnSaveNode_Click(object sender, EventArgs e)
        {
            SaveCurrentNode();
        }

        /// <summary>
        /// 把当前内存配置**全量**存进自定义槽（项目级 CustomPolicy.json）。
        /// 【为什么单独成方法】存槽是"保存"动作的固定尾巴，但回归要能单独验它 ——
        /// SaveCurrentNode 结尾会弹模态提示框，用例里不能裸调（等人的用例就是坏用例）。
        /// 【口径】全量存 PolicyKeys 名单内的项（与 ApplyOverlay/导入导出同口径，脏 key 丢弃）。
        /// 【谁会触发】任何一次【保存节点】——包括在预置画面上改的（预置模板本身不可改，
        /// 改的永远是"自定义方案"）。
        /// </summary>
        private void SaveCurrentConfigToCustomSlot()
        {
            try
            {
                ProjectPolicyStore.SaveCustom(ProjectPolicyStore.ToPolicyDict(_config));
            }
            catch (Exception ex)
            {
                // 槽同步失败不拦主保存（配置已经生效了），但要留痕：
                // 否则用户以为"自定义方案存下来了"，下次套用却是老值，找不到原因。
                UpdateStatus("自定义槽同步失败：" + ex.Message);
            }
        }

        /// <summary>复位布局按钮（Designer 命名处理器，原匿名 lambda 落袋）。</summary>
        private void BtnResetLayout_Click(object sender, EventArgs e)
        {
            if (_canvas != null) _canvas.ResetLayout();
            UpdateStatus("布局已复位。");
        }

        /// <summary>关闭按钮：有未保存的修改先问（保存/丢弃/取消），再关。</summary>
        private void BtnClose_Click(object sender, EventArgs e)
        {
            if (!ConfirmCloseAllowed()) return;
            _closingConfirmed = true;
            this.DialogResult = SavedKeys.Count > 0
                ? DialogResult.OK : DialogResult.Cancel;
            this.Close();
        }

        /// <summary>
        /// 右上 X 同样先问未保存的修改（关闭按钮已问过的不重复；取消即 e.Cancel 留在窗内，
        /// DialogResult 同步清 None，防下次关闭沿用旧值）。
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!e.Cancel && !_closingConfirmed && _shownOnce)
            {
                if (!ConfirmCloseAllowed())
                {
                    e.Cancel = true;
                    this.DialogResult = DialogResult.None;
                }
            }
            base.OnFormClosing(e);
        }

        /// <summary>
        /// 保存当前节点：**看的是哪套，决定存到哪**（2.0.1 用户改口径）。
        /// - **自定义视图**：逐项 ValidateValue → 统一 PersistChanges（与系统设置同一条路：
        ///   组合校验 + 分流写文件 + 热回写全在里面）→ 全量落自定义槽 → 即时生效。
        /// - **预置视图**：只把该节点的 key→值**暂存**到 _presetTempEdits（内存），
        ///   **不写生效配置、不落盘**——生效的永远是"自定义"那一份。
        ///   暂存的内容在【套用到自定义】或关窗确认时按用户的选择才同步过去。
        ///   （用户口径："修改完后可以保存节点…即可以临时保存节点"）
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

            // 预置视图：只暂存（不碰生效配置、不落盘）
            if (IsViewingPreset())
            {
                return SavePresetNodeAsTemp(def, changes);
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
            // 存自定义槽：**一律**存（自定义＝生效配置本体，槽永远等于"当前这套"）。
            SaveCurrentConfigToCustomSlot();
            // 真实配置变了：方案行按新真实重算。**保持当前选择**（保存只发生在自定义视图；
            // 若是"带改动切到预置A"，保存动作不能把用户的选择弹回自定义——
            // 那正是"点了预置A却还在看自定义"的怪味来源，见 RefreshPresetRow）。
            // 预览随说明行一起按当前选择重算（自定义＝看真实，预置＝重算该模板预览）。
            RefreshPresetRow(false);
            if (_canvas != null) _canvas.RefreshCounts();
            // 右栏按新真实重建：保存后右栏必须显示刚存下去的值（不是旧控件残留），
            // 且破空阀总闸翻转时点位行的显隐当场跟着变（显隐按有效配置判定，
            // 见 RebuildEditors）——不用切节点才看到。
            RebuildEditors();
            string msg = "已保存并即时生效。";
            if (presult.StructuralChanged.Count > 0) msg += "（含重启生效项）";
            if (presult.SecretFallbackPlain) msg += "（注：密钥加密失败，已明文保存）";
            UpdateStatus(msg);
            MessageBox.Show(this, msg, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }

        /// <summary>
        /// 预置视图下"保存本节点"＝**暂存**（用户口径："可以临时保存节点"）。
        /// 落到临时修改表（内存），同步到自定义由【套用到自定义】或关窗确认触发。
        /// 暂存后刷新预览（画面与右栏立刻显示暂存值）——所见即所得。
        /// </summary>
        private bool SavePresetNodeAsTemp(PolicyGraph.NodeDef def, Dictionary<string, string> changes)
        {
            if (!ApplyPresetTempEdits(changes)) return false;
            string msg = "已暂存到【" + (def.Title) + "】（尚未生效）。\r\n\r\n"
                + "套用到自定义后才生效；切方案或关窗时也会问您是否同步过去。";
            UpdateStatus("已暂存到预置（尚未生效，套用或关窗时再同步）。");
            MessageBox.Show(this, msg, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }

        /// <summary>
        /// 暂存的核心动作（**不弹框**，单独可测：回归要能直接验"暂存不改生效配置"）。
        /// 落到临时修改表（内存）→ 刷新预览（画面与右栏立刻显示暂存值）。
        /// </summary>
        private bool ApplyPresetTempEdits(Dictionary<string, string> changes)
        {
            if (changes == null || changes.Count == 0) return true;
            string presetId = SelectedPresetId(_cboPreset);
            if (PolicyPresets.Find(presetId) == null) return false;
            _presetTempBaseId = presetId;
            foreach (var kv in changes) _presetTempEdits[kv.Key] = kv.Value;
            _dirty = false;
            // 预览重算（带上刚暂存的值）→ 画面与右栏立刻显示
            UpdateCanvasPreview();
            RebuildEditors();
            return true;
        }

        /// <summary>
        /// 有没有"还没落地"的预置修改：暂存过的 ＋ 编辑器里改了没保存的。
        /// 【为什么要统一判】两处要问同一句（切方案、关窗），判定口径必须一致，
        /// 免得"切方案问了一次、关窗又问一次"或者"关窗漏问"。
        /// </summary>
        private bool HasPendingPresetEdits()
        {
            if (_presetTempEdits.Count > 0) return true;
            return IsViewingPreset() && _dirty;
        }

        /// <summary>
        /// 收集"还没落地"的修改内容 = 暂存的 ＋ 当前节点编辑器里改了没保存的。
        /// 非法值不收（校验不过的等用户自己改对，免得把坏值同步进生效配置）。
        /// </summary>
        private Dictionary<string, string> CollectPendingPresetEdits()
        {
            var pending = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in _presetTempEdits) pending[kv.Key] = kv.Value;
            if (_dirty && !string.IsNullOrEmpty(_selectedId))
            {
                PolicyGraph.NodeDef def = PolicyGraph.FindNode(_selectedId);
                if (def != null)
                {
                    foreach (PolicyGraph.NodeKey key in def.Keys)
                    {
                        Control editor;
                        if (!_editorControls.TryGetValue(key.Key, out editor)) continue;
                        string value = ReadEditor(editor);
                        string error;
                        if (!Dialogs.SettingsForm.ValidateValue(key.Key, value, out error)) continue;
                        pending[key.Key] = value;
                    }
                }
            }
            return pending;
        }

        /// <summary>清空临时修改（丢弃或同步完成后都调）。</summary>
        private void ClearPresetTempEdits()
        {
            _presetTempEdits.Clear();
            _presetTempBaseId = null;
        }

        /// <summary>
        /// 有一句"未落地的预置修改"要问：**是否套用当前预置并把改动同步到自定义**。
        /// 【是】套用＋同步＋存盘（改动落到生效配置与自定义槽）；【否】不套用不保存（丢弃）。
        /// 【谁调】切方案下拉时、关窗时（两条路同一句问法、同一套动作）。
        /// </summary>
        /// <returns>true=可以继续（已同步或已丢弃）；false=留在窗内（套用被拦/校验不过）</returns>
        private bool ConfirmPresetEditsSync()
        {
            if (!HasPendingPresetEdits()) return true;
            Dictionary<string, string> pending = CollectPendingPresetEdits();
            PolicyPresets.PolicyPresetDef def = PolicyPresets.Find(_presetTempBaseId);
            string title = def != null ? def.Title : "预置策略";
            DialogResult r = MessageBox.Show(this, BuildPresetSyncPrompt(title, pending.Count),
                "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes)
            {
                ClearPresetTempEdits();
                _dirty = false;
                return true;
            }
            return ApplyPresetEditsToCustom(pending);
        }

        /// <summary>
        /// 同步问句文案（纯函数：预置名 + 待同步项数 → 提示文本；回归直接断言）。
        /// 【言简意赅】一句话说清"改了什么、要不要落盘、两个选择各是什么"。
        /// </summary>
        public static string BuildPresetSyncPrompt(string presetTitle, int pendingCount)
        {
            return "【" + presetTitle + "】的修改还没生效（" + pendingCount + " 项）。\r\n\r\n"
                + "是否套用该预置，并把改动同步到自定义？\r\n\r\n"
                + "【是】套用并同步保存\r\n【否】不套用、不保存";
        }

        /// <summary>
        /// 套用前的"未保存节点是否一起同步"问句文案（纯函数；回归直接断言）。
        /// </summary>
        public static string BuildApplySyncPrompt(string presetTitle)
        {
            return "【" + presetTitle + "】当前节点有未保存的修改。\r\n\r\n"
                + "是否一起同步到自定义？\r\n\r\n"
                + "【是】一起同步\r\n【否】只套用已保存的";
        }

        /// <summary>
        /// 把"未落地的预置修改"套用到自定义并存盘（预置定义值 ＋ 用户改动，一起生效）。
        /// 生效配置与自定义槽同一条落盘路（PersistChanges），落的是套用后的完整生效配置。
        /// </summary>
        private bool ApplyPresetEditsToCustom(Dictionary<string, string> pending)
        {
            string presetId = _presetTempBaseId;
            if (string.IsNullOrEmpty(presetId) || PolicyPresets.Find(presetId) == null)
            {
                ClearPresetTempEdits();
                _dirty = false;
                return true;
            }
            bool ok = ApplyPresetValues(presetId, pending);
            if (ok)
            {
                ClearPresetTempEdits();
                _dirty = false;
            }
            return ok;
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
    /// 方案区运行时初始化（布局在 Designer，这里只做 Designer 干不了的两样：
    /// ①下拉选项填充（数据源 PolicyPresets.All，循环写 InitializeComponent 会被
    /// VS 重写吞掉）；②悬停提示（全文显示，见 UpdatePresetDesc）。
    /// 无参构造也调——_config 为 null 时回显"自定义"、整行禁用，不抛，VS 可预览）。
    /// 【选项顺序：自定义排第一】下拉＝"我现在看的是哪套"，自定义是唯一能改、唯一生效的，
    /// 必须一眼看到、默认就选中（不必靠"强制回显"去纠正用户的选择）。
    /// </summary>
    private void InitPresetBar()
    {
        if (_cboPreset != null && !_cboPreset.IsDisposed && _cboPreset.Items.Count == 0)
        {
            // 自定义第一项（默认选中＝可编辑的那套），后面 A/B/C/D（试用顺序不变）。
            _cboPreset.Items.Add(new FlowOpt(PolicyPresets.CustomTitle, PolicyPresets.CustomId));
            foreach (PolicyPresets.PolicyPresetDef p in PolicyPresets.All)
            {
                _cboPreset.Items.Add(new FlowOpt(p.Title, p.Id));
            }
        }
        if (_presetTip == null)
        {
            _presetTip = new ToolTip();
        }
        RefreshPresetRow();
    }

        /// <summary>
        /// 下拉改选：**切走前先问一句**（用户口径）。
        /// 预置视图下改了东西（暂存过 或 编辑器里没保存）时，切到别的方案要问：
        /// "是否套用该预置，并把改动同步到自定义？"——【是】套用并同步保存，
        /// 【否】不套用不保存（改动丢弃）。
        /// 同步完继续切到用户选的那一项（套用动作本身会把视图收回"自定义"，
        /// 这里按用户的选择再切回去）。
        /// 右栏有未保存修改时按旧规矩问存/丢/取消（与切节点同口径）。
        /// </summary>
        private void PresetComboChanged(object sender, EventArgs e)
        {
            if (_presetRefreshing) return;
            string newId = SelectedPresetId(_cboPreset);
            string oldId = _lastPresetId;
            // ① 切走前：有未落地的预置修改 → 先问（套用+同步 还是 丢弃）
            if (!string.Equals(newId, oldId, StringComparison.OrdinalIgnoreCase)
                && HasPendingPresetEdits())
            {
                if (!ConfirmPresetEditsSync())
                {
                    // 套用被拦（校验不过等）→ 留在原视图，别把用户带偏
                    RevertPresetCombo();
                    return;
                }
                // 套用动作会把视图收回"自定义"（生效的就是自定义那一份），
                // 这里按用户刚才的选择继续切到他要看的那套
                if (!string.Equals(SelectedPresetId(_cboPreset), newId, StringComparison.OrdinalIgnoreCase))
                {
                    _presetRefreshing = true;
                    try { SelectPresetOpt(_cboPreset, newId); }
                    finally { _presetRefreshing = false; }
                }
            }
            // ② 右栏有未保存修改 → 问存/丢/取消（与切节点同规矩）
            if (_dirty && !string.IsNullOrEmpty(_selectedId) && _editorControls.Count > 0)
            {
                DialogResult r = MessageBox.Show(this,
                    "当前节点有未保存的修改，切换预览前保存吗？\n\n【是】保存后切换\n【否】丢弃后切换\n【取消】留在当前预览",
                    "提示", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel)
                {
                    RevertPresetCombo();
                    return;
                }
                if (r == DialogResult.Yes && !SaveCurrentNode())
                {
                    // 保存被拦（校验不合法）：留在旧预览，右栏不动
                    RevertPresetCombo();
                    return;
                }
                _dirty = false;
            }
            _lastPresetId = newId;
            UpdatePresetDesc();
            // 右栏跟随预览：选中节点时按新有效配置重建（无选中/连线只读态不用动）
            if (!string.IsNullOrEmpty(_selectedId)) RebuildEditors();
        }

    /// <summary>下拉弹回上次选项（脏确认取消/保存被拦时：画布/说明同步恢复）。</summary>
    private void RevertPresetCombo()
    {
        _presetRefreshing = true;
        try { SelectPresetOpt(_cboPreset, _lastPresetId); }
        finally { _presetRefreshing = false; }
        UpdatePresetDesc();
    }

    /// <summary>
    /// 回显方案行：**回显"自定义"**（软件只认自定义——A/B/C/D 是只读模板，
    /// 套用也只是把模板复制进自定义，真正生效的永远是自定义那一份）。
    /// 【什么时候调】开窗 / 套用后 / 导入后——这四个时点生效的都是自定义，
    /// 于是视图统一回到"自定义"（可改的那套）。
    /// 【用户自己切下拉时不调】用户切到 A/B/C/D 是"我要看看这套模板"，
    /// 不该被弹回来；那份"看"由 RebuildEditors 的只读态承接。
    /// 只读模式（非管理员）整行禁用（与节点编辑器同级，不开后门）。
    /// </summary>
    /// <param name="resetToCustom">true=回显"自定义"（开窗/套用/导入后）；
    /// false=保持当前选择（保存节点后用——保存只发生在自定义视图，
    /// 保持即可；顺带避免"带改动切到预置"被保存动作弹回自定义）。</param>
    private void RefreshPresetRow(bool resetToCustom = true)
    {
        if (_cboPreset == null || _cboPreset.IsDisposed) return;
        if (resetToCustom)
        {
            _presetRefreshing = true;
            try
            {
                SelectPresetOpt(_cboPreset, PolicyPresets.CustomId);
            }
            finally { _presetRefreshing = false; }
        }
        // 程序回显不走事件，手动同步上次选项（脏确认弹回认它）
        _lastPresetId = SelectedPresetId(_cboPreset);
        UpdatePresetDesc();
        bool editable = _canEdit && _config != null;
        _cboPreset.Enabled = editable;
        UpdateApplyButtonState();
        // 导出是只读操作（看得见就能带走），有配置即可；导入会写文件，只认管理员
        if (_btnExport != null && !_btnExport.IsDisposed) _btnExport.Enabled = _config != null;
        if (_btnImport != null && !_btnImport.IsDisposed) _btnImport.Enabled = editable;
    }

    /// <summary>
    /// 【套用到自定义】按钮的可用性：只在"当前查看的是预置"时可用
    /// （把模板复制进自定义）；停在自定义时无事可做 → 禁用，并给悬停说明
    /// （禁用按钮不给提示，现场只会以为程序坏了）。
    /// </summary>
    private void UpdateApplyButtonState()
    {
        if (_btnApplyPreset == null || _btnApplyPreset.IsDisposed) return;
        bool editable = _canEdit && _config != null;
        bool viewingPreset = IsViewingPreset();
        _btnApplyPreset.Enabled = editable && viewingPreset;
        try
        {
            if (_presetTip != null)
            {
                _presetTip.SetToolTip(_btnApplyPreset, viewingPreset
                    ? "把这套模板的各节点配置 ＋ 您的临时修改，一起复制到自定义并生效（自定义现有内容将被覆盖）"
                    : "当前已是自定义，无需套用");
            }
        }
        catch { /* 提示写失败不影响主流程（纯展示） */ }
    }

    /// <summary>当前查看的是否是预置模板（选中项不是"自定义"且能查到预置定义）。</summary>
    private bool IsViewingPreset()
    {
        return PolicyPresets.Find(SelectedPresetId(_cboPreset)) != null;
    }

    /// <summary>
    /// 右栏是否只读（只一种：非管理员。预置视图**不是**只读——2.0.1 用户改口径：
    /// 预置可以临时改，最终同步到自定义，见 _presetTempEdits）。
    /// </summary>
    private bool IsReadOnlyView()
    {
        return !_canEdit;
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
        // 对不上任何项（或 null 配置）→落"自定义"（下拉第一项＝唯一可改可生效的那套），
        // 绝不空选
        for (int i = 0; i < cmb.Items.Count; i++)
        {
            var o = cmb.Items[i] as FlowOpt;
            if (o != null && string.Equals(o.Value, PolicyPresets.CustomId, StringComparison.OrdinalIgnoreCase))
            {
                cmb.SelectedIndex = i;
                return;
            }
        }
        if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
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
    /// 【说明行只讲"现在是什么状态、接下来怎么走"】
    /// 选预置：只读模板 + 生效的仍是自定义 + 要用得先套用（三句，够现场决策）；
    /// 选自定义：当前生效的策略，可直接改节点。
    /// 【场景描述去哪了】进悬停提示 + 下拉项文字（"预置A·常用（阀电同开保持常开）"），
    /// 说明行只有两行高，塞不下也不该塞——那是"想知道再查"的信息。
    /// </summary>
    private void UpdatePresetDesc()
    {
        // 按钮可用性跟"当前查看的是不是预置"绑定（切换下拉就得跟着变），
        // 所以放在说明行刷新里统一做（不写两处）。
        UpdateApplyButtonState();
        if (_lblPresetDesc == null || _lblPresetDesc.IsDisposed) return;
        string id = SelectedPresetId(_cboPreset);
        PolicyPresets.PolicyPresetDef def = PolicyPresets.Find(id);
        _lblPresetDesc.Text = def != null
            ? PolicyPresets.PresetDesc
            : PolicyPresets.CustomDesc;
        // 悬停提示同步（_cboPreset 闭合显示被截断时，悬停看全文；
        // 自定义文案与说明行同源，只叫"自定义"不带括号，见 PolicyPresets.CustomTitle）
        try
        {
            if (_presetTip != null && _cboPreset != null && !_cboPreset.IsDisposed)
            {
                string tip = def != null
                    ? PolicyPresets.BuildPresetTip(id)
                    : PolicyPresets.BuildCustomTip(PolicyPresets.DetectPreset(_config));
                _presetTip.SetToolTip(_cboPreset, tip);
            }
        }
        catch { /* 提示写失败不影响主流程（纯展示） */ }
        // 下拉即预览：说明换完，左侧画布同步换成该预置的画面（只看不存）
        UpdateCanvasPreview();
    }

        /// <summary>
        /// 按新真实重造预览（清预览＝画面与右栏都看回真实配置）。
        /// 【为什么需要它】下拉是"当前选中的预置"，保存/套用之后真实配置已经等于
        /// 该预置（或用户自己的自定义），这时再按预置套一遍会把预置的数值项
        /// （如 A 的真空超时 0）盖回真实值——画面显示 0、真实是 15000，
        /// 看着就像"保存没生效，配置还是老的"（现场实锤：改完保存、关窗重开
        /// 仍是老配置，只能重启软件才更新）。
        /// 【与"下拉即预览"不冲突】用户主动切下拉时仍按预置预览（所见即套用后），
        /// 只有"开窗/保存/套用后"这几个"按真实重造"的时点用本方法。
        /// </summary>
        private void RebuildPreviewFromReal()
        {
            _previewConfig = null;
            _presetReadOnly = false;
            if (_canvas != null && !_canvas.IsDisposed) _canvas.SetPreviewConfig(null);
        }

        /// <summary>
        /// 下拉即预览：按下拉当前选项给画布换"预览配置"（只换画布看到的值，
        /// 不碰真实 _config、不标脏、不写文件）。
        /// 选 A/B/C/D 任一项＝把当前真实配置克隆一份、套上该预置的全部落盘值
        /// （含 A 的超时 0），画布即时重画，所见即"真套用后"的样子；
        /// 选"自定义"＝看回真实（清预览）。
        /// 【为什么"自定义"看真实】自定义槽是"上次存的自定义方案"，供【套用自定义】
        /// 一键套用；拿旧槽盖住当前配置，策略窗就会显示与真实生效值不一致的过期值
        /// （现场实锤：改完保存、关窗重开仍是老配置）。显示一律以真实为准。
        /// 【为什么是克隆+套用而不是直接读预置值】画布要的是"完整配置"
        /// （14 开关＋超时＋阈值/时长等不动项），预置只有 14 开关＋数值项；
        /// 克隆保证不动项与当前一致，预览与真套用后的画面一字不差。
        /// 【只读承诺】预览是单独 new 出来的副本，画布只读它；右栏编辑器/保存
        /// 仍读真实 _config，切下拉不产生待保存项（_dirty 不动），点"套用预置"才真写。
        /// </summary>
        private void UpdateCanvasPreview()
        {
            if (_canvas == null || _canvas.IsDisposed) return;
            // 窗体级预览引用与画布配对（右栏 EffectiveConfig 与画布同源，见字段注释）；
            // 预览态标识顺手算好（右栏状态条直接读，不重复读槽文件）。
            if (_config == null)
            {
                _previewConfig = null;
                _presetReadOnly = false;
                _previewTitle = "";
                _canvas.SetPreviewConfig(null);
                return;
            }
            string id = SelectedPresetId(_cboPreset);
            _previewConfig = BuildPreviewConfig(_config, id, GetPresetTempEdits(id));
            _canvas.SetPreviewConfig(_previewConfig);
            // 预览态标识顺手算好（右栏状态条直接读，不重复读槽文件）：
            // 选中预置＝预置编辑视图（可临时改，保存只暂存）。
            _presetReadOnly = PolicyPresets.Find(id) != null;
            PolicyPresets.PolicyPresetDef pvDef = PolicyPresets.Find(id);
            _previewTitle = pvDef != null ? pvDef.Title
                : (string.Equals(id, PolicyPresets.CustomId, StringComparison.OrdinalIgnoreCase)
                    ? PolicyPresets.CustomTitle : (id ?? ""));
        }

        /// <summary>取当前预置的临时修改（没有则 null；只有一份，切换预置时会先问清再清空）。</summary>
        private Dictionary<string, string> GetPresetTempEdits(string presetId)
        {
            if (string.IsNullOrEmpty(presetId)) return null;
            if (_presetTempEdits.Count == 0) return null;
            if (!string.Equals(_presetTempBaseId, presetId, StringComparison.OrdinalIgnoreCase)) return null;
            return _presetTempEdits;
        }

        /// <summary>
        /// 按预置 id 造预览配置（纯函数：A/B/C/D＝克隆现配置＋套预置值＋叠加该预置的
        /// **临时修改**；**自定义＝看真实（返回 null）**——自定义就是当前生效的那一套，
        /// 预览没有"另一套"可看。
        /// 【为什么要叠加临时修改】预置视图下用户可以临时改节点（保存只暂存在内存），
        /// 画面必须显示这些暂存值（所见即所得），否则"改了看不到、套用也不带"，
        /// 用户根本不知道自己在改什么。
        /// </summary>
        private static DeviceConfig BuildPreviewConfig(
            DeviceConfig current, string presetId, Dictionary<string, string> tempEdits)
        {
            try
            {
                if (current == null) return null;
                if (string.Equals(presetId, PolicyPresets.CustomId, StringComparison.OrdinalIgnoreCase))
                {
                    // 看真实：自定义槽不参与显示预览。
                    return null;
                }
                if (PolicyPresets.Find(presetId) == null) return null;
                var previewPreset = new DeviceConfig();
                previewPreset.CopyFrom(current);
                if (PolicyPresets.ApplyToConfig(previewPreset, presetId) != null) return null;
                if (tempEdits != null && tempEdits.Count > 0)
                {
                    foreach (var kv in tempEdits)
                    {
                        try
                        {
                            var prop = typeof(DeviceConfig).GetProperty(kv.Key);
                            if (prop == null || !prop.CanWrite) continue;
                            object converted = ProjectPolicyStore.ParseValue(prop.PropertyType, kv.Value);
                            if (converted == null) continue;
                            prop.SetValue(previewPreset, converted, null);
                        }
                        catch { /* 临时值写不进去就跳过（不影响其它项） */ }
                    }
                }
                return previewPreset;
            }
            catch { return null; }
        }

        private void BtnApplyPreset_Click(object sender, EventArgs e)
        {
            ApplyPreset();
        }

        /// <summary>
        /// 算"套用到自定义"会改哪几项（**纯函数**，可单测；不碰窗体/文件/弹框）。
        /// 【比较基准＝真实生效值】套用是把预置覆盖到【自定义】上，
        /// 基准必须是当前真正生效的那一套；拿预览值比会恒为 0
        /// （预置视图下预览已经套上预置值）→ 误判"当前已是该预置，无需套用"，
        /// 套用根本不会执行（现场实锤：点按钮永远提示无需套用，自定义里什么都没变）。
        /// 【比较集合】预置管辖的 14 个开关 ＋ 预置数值项 ＋ 用户在预置视图下的临时修改。
        /// 临时改的非管辖项也要列出来（否则"我改的那两项"在确认框里看不见，
        /// 用户不敢点确定）；同一 key 只列一次（临时值覆盖预置值）。
        /// </summary>
        /// <param name="readRealValue">按 key 读真实生效值（真实口径，不看预览）</param>
        /// <param name="presetValues">预置定义值（14 个行为开关）</param>
        /// <param name="numericValues">预置数值项（如真空超时；没有则传 null）</param>
        /// <param name="tempEdits">用户临时修改（key→值；没有则传 null）</param>
        /// <returns>差异行（"【中文名】 现值 → 新值"）；无差异返回空表</returns>
        public static List<string> BuildApplyDiffLines(
            Func<string, string> readRealValue,
            Dictionary<string, string> presetValues,
            Dictionary<string, string> numericValues,
            Dictionary<string, string> tempEdits)
        {
            var lines = new List<string>();
            if (readRealValue == null) return lines;
            // 目标值 = 预置定义值 ← 数值项 ← 临时修改（后者覆盖前者）
            var want = new Dictionary<string, string>(StringComparer.Ordinal);
            if (presetValues != null)
            {
                foreach (var kv in presetValues) want[kv.Key] = kv.Value;
            }
            if (numericValues != null)
            {
                foreach (var kv in numericValues) want[kv.Key] = kv.Value;
            }
            if (tempEdits != null)
            {
                foreach (var kv in tempEdits) want[kv.Key] = kv.Value;
            }
            foreach (var kv in want)
            {
                string cur = readRealValue(kv.Key) ?? "";
                if (string.Equals(cur.Trim(), (kv.Value ?? "").Trim(),
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string label;
                if (!PolicyPresets.KeyLabels.TryGetValue(kv.Key, out label)) label = kv.Key;
                bool mine = tempEdits != null && tempEdits.ContainsKey(kv.Key);
                lines.Add("【" + label + "】 " + cur + " → " + kv.Value
                    + (mine ? "（您的修改）" : ""));
            }
            return lines;
        }

        /// <summary>
        /// 【套用到自定义】（2.0.1：按钮语义唯一 = 把当前这套预置复制到自定义）。
        /// 流程：①有未保存的节点修改 → 先问"是否一起同步过去"（用户口径）
        /// → ②列差异确认 → ③逐项校验 → PersistChanges 同一条保存路
        /// → 全量落自定义槽 → 视图切回"自定义"（可改的那套）。
        /// 【套用的内容】预置定义值 ＋ 用户在预置视图下的改动（暂存的 + 刚确认一起同步的），
        /// 一起写进生效配置与自定义槽（自定义＝生效配置本体，槽永远等于"当前这套"）。
        /// 【为什么没有"把槽写回配置"】自定义＝生效配置本体，槽在每次保存时就全量落盘，
        /// 两者永远一致，那个动作是永远无差异的死功能。
        /// 【只动 14 个行为开关＋数值项＋用户改的那几项】MES/规则/报表/画面字典/破空点位/
        /// 时长阈值等自由文本与配方/机器参数一律不动——切预置不丢现场已填的东西。
        /// 数值项（A 的真空超时 0）是跟项目走的策略 key，同样进本项目 Policy.json。
        /// 【失败语义】校验拦/D 组合拦（超温上限为 0）都是"按住不动 + 中文告诉人
        /// 去哪填"，绝不悄悄写一半（PersistChanges 内部先验后写）。
        /// </summary>
        private void ApplyPreset()
        {
            if (!_canEdit || _config == null) return;
            string id = SelectedPresetId(_cboPreset);
            PolicyPresets.PolicyPresetDef def = PolicyPresets.Find(id);
            if (def == null)
            {
                // 停在"自定义"：按钮本来就禁用（这里只是兜底，防反射直调）。
                MessageBox.Show(this, "当前已是【自定义】，无需套用。\r\n\r\n"
                    + "要看预置模板请在下拉里选 A/B/C/D，选中后可点【套用到自定义】。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            // ① 编辑器里改了没保存 → 问"是否一起同步过去"（用户口径：
            //    "修改完预置策略的节点后，没有保存节点，直接点了套用到自定义，
            //     则提示已修改的节点是否一起同步过去，让用户选择是或否"）
            var extra = new Dictionary<string, string>(StringComparer.Ordinal);
            if (_dirty)
            {
                DialogResult r = MessageBox.Show(this, BuildApplySyncPrompt(def.Title),
                    "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r == DialogResult.Yes)
                {
                    extra = CollectCurrentNodeEdits();
                    // 当场并入临时修改（后续差异预览与落盘都带着它）
                    foreach (var kv in extra) _presetTempEdits[kv.Key] = kv.Value;
                    if (string.IsNullOrEmpty(_presetTempBaseId)) _presetTempBaseId = id;
                }
                _dirty = false;
            }
            // 套用内容 = 预置定义值 ＋ 临时修改（暂存的 ＋ 刚确认一起同步的）
            Dictionary<string, string> presetValues = PolicyPresets.GetPresetValues(id);
            if (presetValues == null) return;
            Dictionary<string, string> numerics = PolicyPresets.GetNumericValues(id);
            // 差异预览：**基准是真实生效值**（不是预览）。
            // 【为什么】预览在预置视图下已经套上了预置值，拿它当"当前值"比 →
            // 差异恒为 0 → 误判"当前已是该预置，无需套用" → 直接 return，
            // 套用**从来没执行过**（现场实锤：点【套用到自定义】永远弹"无需套用"，
            //  切到自定义里看节点一点没变，用户根本不知道到底成没成）。
            var diffLines = BuildApplyDiffLines(GetRealConfigString,
                presetValues, numerics, _presetTempEdits);
            if (diffLines.Count == 0)
            {
                MessageBox.Show(this,
                    "当前【自定义】的内容已与【" + def.Title + "】完全一致，无需套用。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string confirm = "把【" + def.Title + "】套用到自定义？\r\n\r\n" + def.Scenario + "\r\n"
                + (string.IsNullOrWhiteSpace(def.Requires)
                    ? "" : "\r\n前置条件：" + def.Requires + "\r\n")
                + "\r\n将修改 " + diffLines.Count + " 项：\r\n"
                + string.Join("\r\n", diffLines.ToArray())
                + "\r\n\r\n配方延时/时长/阈值/画面等配方项、MES/规则/报表/画面字典/点位等不动"
                + "（配方延时填0即阀电同开，无配方默认即0，有配方按配方来）。\r\n"
                + "\r\n套用后这套配置成为您的自定义策略并立即生效"
                + "（自定义现有内容将被覆盖），之后就在自定义里改节点。\r\n"
                + def.HowToSwitch;
            if (MessageBox.Show(this, confirm, "套用到自定义",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            {
                return;
            }
            if (!ApplyPresetValues(id, _presetTempEdits)) return;
            ClearPresetTempEdits();
            PolicyPresets.PolicyPresetDef applied = PolicyPresets.Find(id);
            string msg = "已把【" + (applied != null ? applied.Title : id)
                + "】套用到自定义并生效，可直接在这里改节点。";
            UpdateStatus(msg);
            MessageBox.Show(this, msg + "\r\n\r\n" + (applied != null ? applied.HowToSwitch : ""),
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>取当前节点编辑器里的值（只收校验通过的；非法值不参与同步）。</summary>
        private Dictionary<string, string> CollectCurrentNodeEdits()
        {
            var edits = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(_selectedId)) return edits;
            PolicyGraph.NodeDef def = PolicyGraph.FindNode(_selectedId);
            if (def == null) return edits;
            foreach (PolicyGraph.NodeKey key in def.Keys)
            {
                Control editor;
                if (!_editorControls.TryGetValue(key.Key, out editor)) continue;
                string value = ReadEditor(editor);
                string error;
                if (!Dialogs.SettingsForm.ValidateValue(key.Key, value, out error)) continue;
                edits[key.Key] = value;
            }
            return edits;
        }

        /// <summary>
        /// 执行套用：逐项校验 → 同一条保存路（组合校验 + 分流写文件 + 热回写）→
        /// 刷新（右栏重建＋画布＋方案行回显）。
        /// 调用前必须已确认（【套用到自定义】的差异确认框），这里不再二次确认。
        /// </summary>
        /// <returns>true=套用成功，false=被拦（原因已中文明示，留在窗内）</returns>
        private bool ApplyPresetValues(string id)
        {
            return ApplyPresetValues(id, null);
        }

        /// <summary>
        /// 执行套用（带额外改动：预置定义值 ＋ 用户在预置视图下的修改，一起生效）。
        /// </summary>
        private bool ApplyPresetValues(string id, Dictionary<string, string> extraValues)
        {
            Dictionary<string, string> presetValues = PolicyPresets.GetAllValues(id);
            if (presetValues == null) return false;
            Dictionary<string, string> values = new Dictionary<string, string>(presetValues, StringComparer.Ordinal);
            if (extraValues != null)
            {
                foreach (var kv in extraValues) values[kv.Key] = kv.Value;
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
                return false;
            }
            // 同一条保存路：组合校验 + 分流写文件 + 热回写全在里面
            Dialogs.SettingsForm.PersistResult presult;
            string perror;
            if (!Dialogs.SettingsForm.PersistChanges(_config, values, out presult, out perror))
            {
                MessageBox.Show(this, perror, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            foreach (string k in presult.SavedKeys) SavedKeys.Add(k);
            _dirty = false;
            // 套用预置 = 把这套模板变成"我的当前方案"：**全量落自定义槽**。
            // 【为什么】旧口径套用预置不碰槽 → 套完点【套用自定义】拿回的是老值，
            // 跟屏幕上显示的完全两套，用户只会懵（"我明明套了 A 怎么套自定义是另一套"）。
            // 落槽后语义统一：自定义槽永远是"当前这套"，预置是一键把出厂模板变成当前方案。
            // 落的是**套用后的完整生效配置**（预置管辖的全量已换新，自由文本保留现状
            // —— 预置本来就没定义自由文本的值，覆盖不了）。
            SaveCurrentConfigToCustomSlot();
            // 方案行先行：下拉回显"自定义"（套用后生效的就是自定义那一份，
            // 视图必须回到可改的那套）＋预览同步到新真实，
            // 再重建右栏回显新值（右栏看有效配置，顺序反了会按旧预览建）。
            RefreshPresetRow();
            RebuildPreviewFromReal();
            RebuildEditors();
            if (_canvas != null) _canvas.RefreshCounts();
            return true;
        }

        /// <summary>
        /// 导入工艺策略（只进自定义槽：脏先问存 → 选文件 → 解析过滤 → 逐项校验 →
        /// SunnyUI 确认框明示"只进自定义" → PersistChanges 同一条保存路 → 写槽 → 刷新）。
        /// 【为什么只进自定义】A/B/C/D 是代码写死的预置，没有"覆盖预置"这个概念；
        /// 跨工控机搬配置的落点永远是自定义槽，预置在任何机器上都同一套。
        /// </summary>
        private void BtnImport_Click(object sender, EventArgs e)
        {
            ImportPolicy();
        }

        private void ImportPolicy()
        {
            if (!_canEdit || _config == null) return;
            // 脏先问存（与套用同规矩：是=先存本节点，否=丢弃，取消=不导入）
            if (_dirty)
            {
                DialogResult r = MessageBox.Show(this,
                    "当前节点有未保存的修改，导入前保存吗？\n\n【是】保存后导入\n【否】丢弃后导入\n【取消】不导入",
                    "提示", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel) return;
                if (r == DialogResult.Yes && !SaveCurrentNode()) return;
                _dirty = false;
            }
            string path;
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "导入工艺策略（只进自定义槽）";
                dlg.Filter = "策略文件(*.json)|*.json|全部文件(*.*)|*.*";
                dlg.CheckFileExists = true;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                path = dlg.FileName;
            }
            Dictionary<string, string> values;
            string readError;
            if (!ProjectPolicyStore.ReadImportFile(path, out values, out readError))
            {
                MessageBox.Show(this, readError, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            // 逐项校验（文件是手传过来的，脏值在这里拦，不带病写入）
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
                MessageBox.Show(this, "文件里有不合法的策略值，导入已停止：\r\n\r\n" +
                    string.Join("\r\n", invalid.ToArray()),
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            // SunnyUI 确认框：明示落点是自定义槽（防误以为覆盖了预置）
            bool ok;
            try
            {
                ok = Sunny.UI.UIMessageBox.Show(
                    "从文件导入工艺策略（共 " + values.Count + " 项）？\r\n\r\n文件：" + path
                    + "\r\n\r\n配置将导入到【自定义】并即时生效，"
                    + "A/B/C/D 预置不受影响。",
                    "导入到自定义？", Sunny.UI.UIStyle.Blue,
                    Sunny.UI.UIMessageBoxButtons.OKCancel, false, 0);
            }
            catch { ok = false; }   // 弹框失败按"不导入"处理，绝不带病写入
            if (!ok) return;
            // 同一条保存路：组合校验＋分流写文件＋热回写全在里面
            Dialogs.SettingsForm.PersistResult presult;
            string perror;
            if (!Dialogs.SettingsForm.PersistChanges(_config,
                new Dictionary<string, string>(values, StringComparer.Ordinal),
                out presult, out perror))
            {
                MessageBox.Show(this, perror, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            foreach (string k in presult.SavedKeys) SavedKeys.Add(k);
            try { ProjectPolicyStore.SaveCustom(values); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "配置已生效，但自定义槽保存失败"
                    + "（下次打开自定义可能不是这次的内容）：" + ex.Message,
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            _dirty = false;
            // 先预置行（下拉重探测＋预览同步到新真实），再重建右栏
            // （右栏看有效配置，顺序反了会按旧预览建）。
            RefreshPresetRow();
            RebuildPreviewFromReal();
            RebuildEditors();
            if (_canvas != null) _canvas.RefreshCounts();
            string msg = "已导入到【自定义】并即时生效（共 " + values.Count + " 项）。";
            UpdateStatus(msg);
            MessageBox.Show(this, msg, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// 导出工艺策略（先选源 A/B/C/D/自定义 → 存文件；只读模式也可导，看得见就能带走）。
        /// 文件装"全部跟项目策略 key"（含 MES 映射/规则/报表自由文本），
        /// 在另一台工控机上用"导入策略"载入（落点永远是自定义槽）。
        /// </summary>
        private void BtnExport_Click(object sender, EventArgs e)
        {
            ExportPolicy();
        }

        private void ExportPolicy()
        {
            if (_config == null) return;
            string source = ShowExportSourceDialog(SelectedPresetId(_cboPreset));
            if (string.IsNullOrEmpty(source)) return;   // 用户取消
            Dictionary<string, string> custom = null;
            try { custom = ProjectPolicyStore.LoadCustom(); }
            catch { custom = null; }
            Dictionary<string, string> payload =
                PolicyPresets.BuildExportValues(source, _config, custom);
            if (payload == null || payload.Count == 0)
            {
                MessageBox.Show(this, "没有可导出的策略内容。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            bool isCustom = string.Equals(source, PolicyPresets.CustomId,
                StringComparison.OrdinalIgnoreCase);
            PolicyPresets.PolicyPresetDef srcDef = PolicyPresets.Find(source);
            string sourceTitle = isCustom ? PolicyPresets.CustomTitle
                : (srcDef != null ? srcDef.Title : source);
            string path;
            using (var dlg = new SaveFileDialog())
            {
                dlg.Title = "导出工艺策略【" + sourceTitle + "】";
                dlg.Filter = "策略文件(*.json)|*.json|全部文件(*.*)|*.*";
                dlg.FileName = "工艺策略-" + (isCustom ? "自定义" : source) + ".json";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                path = dlg.FileName;
            }
            try { ProjectPolicyStore.WriteExportFile(path, payload); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "导出失败：" + ex.Message,
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            UpdateStatus("已导出【" + sourceTitle + "】。");
            MessageBox.Show(this,
                "已导出【" + sourceTitle + "】（共 " + payload.Count + " 项）到：\r\n" + path
                + "\r\n\r\n在另一台工控机上用“导入策略”载入（只进自定义槽）。",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// 导出源选择框（小模态窗：下拉选 A/B/C/D/自定义，缺省停在当前预置行选项）。
        /// </summary>
        /// <returns>源 Id（A/B/C/D/Custom）；取消/关闭返回 null</returns>
        private string ShowExportSourceDialog(string defaultId)
        {
            string picked = null;
            using (var dlg = new Form())
            {
                dlg.Text = "导出哪个配置？";
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.ShowInTaskbar = false;
                dlg.ClientSize = new Size(360, 130);
                var lbl = new Label
                {
                    Location = new Point(12, 12),
                    Size = new Size(336, 20),
                    Text = "选择要导出的配置（文件含全部策略项，可跨机导入）："
                };
                var cmb = new ComboBox
                {
                    Location = new Point(12, 38),
                    Size = new Size(336, 24),
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                var displayToId = new Dictionary<string, string>();
                foreach (PolicyPresets.PolicyPresetDef p in PolicyPresets.All)
                {
                    cmb.Items.Add(p.Title);
                    displayToId[p.Title] = p.Id;
                }
                cmb.Items.Add(PolicyPresets.CustomTitle);
                displayToId[PolicyPresets.CustomTitle] = PolicyPresets.CustomId;
                // 缺省停在当前预置行选项（对不上就停自定义）
                int defIdx = cmb.Items.Count - 1;
                foreach (var kv in displayToId)
                {
                    if (string.Equals(kv.Value, defaultId, StringComparison.OrdinalIgnoreCase))
                    {
                        defIdx = cmb.Items.IndexOf(kv.Key);
                        break;
                    }
                }
                cmb.SelectedIndex = defIdx >= 0 ? defIdx : cmb.Items.Count - 1;
                var btnOk = new Button
                {
                    Location = new Point(178, 80),
                    Size = new Size(80, 28),
                    Text = "导出",
                    DialogResult = DialogResult.OK
                };
                var btnCancel = new Button
                {
                    Location = new Point(268, 80),
                    Size = new Size(80, 28),
                    Text = "取消",
                    DialogResult = DialogResult.Cancel
                };
                dlg.Controls.Add(lbl);
                dlg.Controls.Add(cmb);
                dlg.Controls.Add(btnOk);
                dlg.Controls.Add(btnCancel);
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;
                try { ThemeManager.ApplyTo(dlg); }
                catch { /* 换肤失败不拦导出 */ }
                if (dlg.ShowDialog(this) != DialogResult.OK) return null;
                object sel = cmb.SelectedItem;
                if (sel != null) displayToId.TryGetValue(sel.ToString(), out picked);
            }
            return picked;
        }

        /// <summary>
        /// 关窗前确认（2.0.1 用户口径）：
        /// ① **有未落地的预置修改**（暂存过 或 编辑器里改了没保存）→ 问一句
        ///    "是否套用该预置，并把改动同步到自定义？"——【是】套用并同步保存，
        ///    【否】不套用不保存（丢弃）。**不管从哪关（X／关闭按钮）都问**。
        /// ② 当前节点有未保存的修改（自定义视图）→ 原来的存/丢/取消。
        /// 【为什么不问"要不要套用"】旧口径问的是"看了模板要不要套"，那是替人做主；
        /// 现在只在**真的改了东西**时问要不要落盘，性质完全不同。
        /// </summary>
        /// <returns>true=继续关闭，false=留在窗内（套用被拦 / 用户取消）</returns>
        private bool ConfirmCloseAllowed()
        {
            try
            {
                if (_config == null || !_shownOnce || IsDisposed || Disposing) return true;
                // ① 预置修改的同步确认（套用+同步 还是 丢弃）
                if (HasPendingPresetEdits())
                {
                    if (!ConfirmPresetEditsSync()) return false;
                }
                // ② 自定义视图里节点有未保存的修改 → 存/丢/取消
                if (_dirty)
                {
                    DialogResult r = MessageBox.Show(this,
                        "当前节点有未保存的修改，关闭前保存吗？\n\n【是】保存后关闭\n【否】丢弃后关闭\n【取消】不关闭",
                        "提示", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    if (r == DialogResult.Cancel) return false;
                    if (r == DialogResult.Yes && !SaveCurrentNode()) return false;
                    _dirty = false;
                }
                return true;
            }
            catch { return true; }  // 关闭路径绝不抛异常拦人
        }

        /// <summary>
        /// 流程画布（自绘 Panel：固定拓扑 + 可拖节点 + 缩放平移 + 点击选中）。
        /// 几何全走 Scale()=_dpiScale×_zoom 一处；命中是绘制的逆运算。
        /// </summary>
        private class FlowCanvas : Panel
        {
            private readonly DeviceConfig _config;
            private readonly DeviceManager _deviceManager;

            // 预览配置（下拉即预览用：非 null 时画布只读它，真实 _config 纹丝不动；
            // null＝看真实（含槽空的自定义）；保存/套用/导入走真实 _config，与这里无关）。
            private DeviceConfig _previewConfig;

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

            // 视图偏移（"适合图像"居中用）：AutoScrollPosition 被钳在滚动范围内，
            // 而内容比视口小的时候根本没有滚动范围（内容 615×810 < 视口 836×888），
            // 想把整图摆到视野中央就必须另开一个不受滚动范围限制的偏移量。
            // 坐标换算一律走 Dx/Dy/ToLogicalX/ToLogicalY（都含它），别在别处加。
            private Point _viewOffset = Point.Empty;

            // 右键菜单（空白处弹出，当前只有一个选项"适合图像"；以后加项在这里加）
            private readonly ContextMenuStrip _viewMenu;
            private readonly ToolStripMenuItem _miFitView;

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

                // 右键菜单（构造期建一次、复用；无容器托管，Dispose 里手放）
                _viewMenu = new ContextMenuStrip();
                _miFitView = new ToolStripMenuItem("适合图像");
                _miFitView.Click += (s, e) => FitToView();
                _viewMenu.Items.Add(_miFitView);

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
                    // 右键菜单无容器托管，必须手放（否则进终结器线程释放控件）
                    try { if (_viewMenu != null) _viewMenu.Dispose(); } catch { }
                }
                base.Dispose(disposing);
            }

            /// <summary>
            /// 【适合图像】＝把整张流程图还原成"刚打开窗口时"的样子：
            /// 缩放回到打开时的默认大小（zoom=1），平移归零，并把整图摆到视野**居中**。
            /// 【为什么居中要另开 _viewOffset】AutoScrollPosition 被钳在滚动范围内，
            /// 而内容比视口小的时候没有滚动范围（内容 615×810 < 视口 836×888），
            /// 光靠滚动根本摆不到中央——所以另开一个不受滚动范围限制的视图偏移。
            /// 【与"复位布局"分工】复位布局＝节点位置回缺省（存盘）；适合图像＝**视图**
            /// 回默认（缩放/平移/居中），不碰节点位置。
            /// </summary>
            public void FitToView()
            {
                _zoom = 1f;                 // 打开窗口时的默认大小
                RebuildFonts();
                UpdateScrollSize();
                this.AutoScrollPosition = new Point(0, 0);   // 平移归零（setter 取正值）
                _viewOffset = ComputeCenterOffset();
                this.Invalidate();
            }

            /// <summary>
            /// 居中偏移（画布侧：节点包围盒 → 设备包围盒 → 纯函数换算）。
            /// 设备包围盒按"自然位置"算（不含视图偏移，否则自相矛盾）。
            /// </summary>
            private Point ComputeCenterOffset()
            {
                int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
                foreach (var n in PolicyGraph.Nodes)
                {
                    Rectangle r = GetRect(n);
                    if (r.Left < minX) minX = r.Left;
                    if (r.Top < minY) minY = r.Top;
                    if (r.Right > maxX) maxX = r.Right;
                    if (r.Bottom > maxY) maxY = r.Bottom;
                }
                if (minX == int.MaxValue || maxX == int.MinValue) return Point.Empty;
                float s = Scale();
                Rectangle box = new Rectangle(
                    (int)(minX * s) + this.AutoScrollPosition.X,
                    (int)(minY * s) + this.AutoScrollPosition.Y,
                    (int)((maxX - minX) * s),
                    (int)((maxY - minY) * s));
                return ProcessPolicyForm.ComputeCenterOffset(box, this.ClientSize);
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

            /// <summary>换预览配置（null＝看回真实配置；只重画，不碰真实数据）。</summary>
            public void SetPreviewConfig(DeviceConfig preview)
            {
                _previewConfig = preview;
                if (!this.IsDisposed) this.Invalidate();
            }

            /// <summary>画布当前看到的配置（有预览看预览，平时即真实）。</summary>
            private DeviceConfig EffectiveConfig() { return _previewConfig ?? _config; }

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
                // 【合成平移】device = logical×Scale − pan，pan = 滚动偏移 − 视图偏移；
                // 两段平移各管各的（滚动偏移受滚动范围限制、视图偏移不受限），
                // 缩放时按"合成平移不变"来锚，差额由视图偏移补，缩放后内容不跳位。
                Point mouse = this.PointToClient(Cursor.Position);
                double lx = (mouse.X - this.AutoScrollPosition.X - _viewOffset.X) / (double)(_dpiScale * old);
                double ly = (mouse.Y - this.AutoScrollPosition.Y - _viewOffset.Y) / (double)(_dpiScale * old);
                _zoom = next;
                RebuildFonts();
                UpdateScrollSize();
                int panX = (int)(lx * _dpiScale * next - mouse.X);   // 想要的合成平移
                int panY = (int)(ly * _dpiScale * next - mouse.Y);
                int maxScrollX = Math.Max(0, this.AutoScrollMinSize.Width - this.ClientSize.Width);
                int maxScrollY = Math.Max(0, this.AutoScrollMinSize.Height - this.ClientSize.Height);
                int scrollX = Math.Max(0, Math.Min(maxScrollX, panX + _viewOffset.X));
                int scrollY = Math.Max(0, Math.Min(maxScrollY, panY + _viewOffset.Y));
                // AutoScrollPosition setter 取正值（getter 吐负值，WinForms 老坑）
                this.AutoScrollPosition = new Point(scrollX, scrollY);
                _viewOffset = new Point(panX - scrollX, panY - scrollY);
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

            // ---- 坐标换算（device = logical×Scale + AutoScrollPosition + _viewOffset） ----
            // 【为什么多一个 _viewOffset】"适合图像"要把整图摆到视野中央，而内容比视口小
            // 时没有滚动范围可挪（AutoScrollPosition 恒为 0）——用这个不受滚动范围限制的
            // 偏移量来摆。命中/缩放锚点都走这里，别在别处单独加（漏一处就错位）。
            private int Dx(int logical) { return (int)(logical * Scale()) + this.AutoScrollPosition.X + _viewOffset.X; }
            private int Dy(int logical) { return (int)(logical * Scale()) + this.AutoScrollPosition.Y + _viewOffset.Y; }
            private int ToLogicalX(int deviceX) { return (int)((deviceX - this.AutoScrollPosition.X - _viewOffset.X) / Scale()); }
            private int ToLogicalY(int deviceY) { return (int)((deviceY - this.AutoScrollPosition.Y - _viewOffset.Y) / Scale()); }

            private void OnMouseDown(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Right)
                {
                    // 右键：**空白处**弹视图菜单（当前只有"适合图像"）；
                    // 节点/连线上不弹（那是选中/拖动的地方，右键不打断）。
                    Point rlogical = new Point(ToLogicalX(e.X), ToLogicalY(e.Y));
                    if (HitNode(rlogical) != null) return;
                    if (HitEdge(e.Location) != null) return;
                    try
                    {
                        if (_viewMenu != null && !_viewMenu.IsDisposed) _viewMenu.Show(this, e.Location);
                    }
                    catch { /* 菜单弹不出来不拦主流程（纯交互） */ }
                    return;
                }
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
                string label = PolicyGraph.BuildEdgeLabel(e.Id, EffectiveConfig());
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

                string[] lines = PolicyGraph.BuildNodeLines(n.Id, EffectiveConfig(), _counts);
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
