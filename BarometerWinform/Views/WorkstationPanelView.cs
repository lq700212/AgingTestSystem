using System;
using System.Drawing;
using System.Windows.Forms;
using BarometerWinform.Models;

namespace BarometerWinform.Views
{
    /// <summary>
    /// 工位显示面板（子视图）—— 业务逻辑部分
    /// 每个工位对应一台气压表，本面板显示该工位的实时状态。
    ///
    /// 【V1.16 更名 + 重设计】
    /// 原类名 BarometerPanelView 容易让人误以为只显示气压表；实际上它显示的是 72 个
    /// "工位"（每个工位配一台气压表），因此更名为 WorkstationPanelView（工位面板）。
    ///
    /// 【界面布局】（控件创建见 <see cref="WorkstationPanelView.Designer.cs"/>）
    /// ┌──────────────────────────────┐
    /// │ NO.1                  [□✓]   │  ← 设备编号 + 选中指示（V1.19.5 起"有选中才显示"）
    /// │ [上电/下电] [工作状态]         │  ← 上电状态灯/工作状态（文字+颜色，V1.24）
    /// │ 真空压力 [值] [真空开]         │  ← 压力值只读（V1.19.10 加宽）；真空开/关=文字+颜色
    /// │ SN:    [SN值 Label]           │
    /// │ 配方:  [配方值 Label]         │
    /// │ 延时开启 [__:__:__]   ┌────┐  │
    /// │ 延时到达 [__:__:__]   │设置│  │  ← 点击弹出设置窗口（V1.24：按选中数量分流）
    /// │                       └────┘  │
    /// └──────────────────────────────┘
    ///
    /// 【选中交互（V1.19.6）】
    /// - 在面板空白区域"长按约 0.8 秒"（按住不松手）即选中该工位（首次/新增选中通过长按）；
    /// - 【V1.24】若长按时选中框已可见（已有任一工位被选中）：不再选中当前工位，
    ///   而是**取消全部选中并隐藏所有选中框**（取消全选由主窗体 ClearAllSelectionRequested 事件统一执行）；
    /// - 选中框显示时（已有任一工位被选中）：单击面板空白区域或点击选中框 = **切换**该工位"选中/未选中"；
    /// - 例外：整表只有该工位处于选中状态时，把它切换为未选中 → 全表无选中，所有面板的选中框自动隐藏；
    /// - 选中框平时全部隐藏；只要有任一工位被选中，所有面板同时显示框——
    ///   选中项 = 绿底 + 白色"✓"，未选中项 = 空心白框（显示/隐藏由主窗体统一协调）。
    ///
    /// 【状态灯颜色约定】
    /// - 上电状态灯（boxPower，V1.24 起带文字）：载台上电输出 ON=LimeGreen 绿底白字"上电"，
    ///   OFF=浅灰(LightGray)底黑字"下电"（V1.28 由红改灰，与行全选按钮同色，降低视觉强度）
    /// - 真空开启显示（boxVacuumOpen，V1.19.10 起带文字+颜色）：真空电磁阀输出
    ///   ON=绿底白字"真空开"，OFF=浅灰(LightGray)底黑字"真空关"（V1.28 由红改灰）
    /// - 工作状态（boxWorkState，V1.18 文字用中文）：空闲/选中/繁忙/故障
    ///   空闲=绿底黑字（V1.24 由浅灰改为 LimeGreen，表示就绪）；已上电待测试=选中（橙底白字）；
    ///   测试中=繁忙（黄底白字，V1.24 由绿改为红绿灯"黄灯色" Gold）；
    ///   故障=红底白字（V1.19.4 统一为"信号灯"色系，故障最醒目）
    ///   （V1.19.2：是否选中不再改变工作状态文字）
    /// - 选中指示（btnSelect，右上角，V1.19）：选中显示"✓"（绿底白字），未选中显示空心方框
    ///   （黑框白底）。V1.19.5：平时全部隐藏，有选中才显示（见上"选中交互"）。
    ///   （V1.19.2：选中状态仅靠此指示体现，不再改变面板背景色/工作状态文字）
    ///
    /// 【说明】
    /// 本文件只包含业务逻辑（数据更新、状态切换、事件处理）。
    /// 界面控件的创建和布局代码在 Designer.cs 文件中，由 Visual Studio 设计器维护。
    /// </summary>
    /// <remarks>
    /// 【设计器注意事项（沿用历史修复）】
    /// 当 .cs 文件包含中文字符但没有 UTF-8 BOM 时，VS 设计器使用的 CodeDom 解析器
    /// 可能错误识别文件编码，导致中文注释乱码、无法正确解析类声明。
    /// 本类基类使用完整命名空间 System.Windows.Forms.UserControl 以避免设计器报错
    /// （"无法设计基类 System.Void"）。所有 .cs 文件保存为 UTF-8 with BOM。
    /// </remarks>
    public partial class WorkstationPanelView : System.Windows.Forms.UserControl
    {
        /// <summary>
        /// 所属设备（工位）编号（从1开始）
        /// 运行时通过带参数构造函数赋值；设计器预览时为0
        /// </summary>
        public int DeviceId { get; private set; }

        /// <summary>
        /// 当前显示的工位数据（最近一次 UpdateData 的快照）
        /// 供主窗体读取当前载台上电状态等使用
        /// </summary>
        private BarometerData _currentData;

        /// <summary>
        /// 面板是否被选中（用于批量操作）
        /// 由主窗体的"行全选"按钮、面板空白区域"长按约 0.8 秒"选中（V1.19.5 替换 V1.18 的点击选中）、
        /// 以及选中框显示时单击空白区域 / 点击选中框**切换**选中状态控制（V1.19.6）。
        /// 【V1.19.2】选中状态仅通过右上角选中指示（btnSelect 显示 ✓）体现，不再改变面板背景色与工作状态文字。
        /// 【V1.19.5】选中框平时全部隐藏，只要有任一工位被选中→所有面板同时显示框（选中项=绿底白✓，其它项=空框）。
        /// </summary>
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                // 值未变化时直接返回，避免无谓的界面刷新与事件触发
                if (_isSelected == value) return;

                _isSelected = value;
                UpdateSelectionStyle();

                // 【V1.19】通知主窗体刷新所在行的"全选/取消"按钮文字：
                // 仅当该行所有面板都选中时按钮才显示"取消"，否则始终显示"全选"。
                IsSelectedChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        private bool _isSelected = false;

        /// <summary>
        /// 选中状态变更事件（【V1.19 新增】）
        /// 当 <see cref="IsSelected"/> 实际发生变化时触发，触发来源包括：
        /// - 面板空白区域长按约 0.8 秒选中（LongPressTimer_Tick，V1.19.5 替换点击选中）
        /// - 选中框显示时单击面板空白区域 / 点击选中框**切换**选中状态（Panel_MouseUp / btnSelect_Click，V1.19.6）
        /// - 主窗体行全选按钮（BtnSelectRow_Click）
        ///
        /// 主窗体订阅此事件后，在任意单个面板选中状态变化时调用 UpdateRowSelectButton，
        /// 刷新所在行的"全选/取消"按钮文字，保证：
        /// "仅当该行全部面板都选中时按钮显示【取消】；只要有一台被单独取消，按钮立即恢复【全选】"；
        /// 同时调用 UpdateSelectionBoxVisibility 刷新所有面板选中框的显示/隐藏
        /// （V1.19.5：有任一选中→全部显示，全未选中→全部隐藏；
        ///  V1.19.6 例外：整表唯一选中的工位被切换为未选中时，全表无选中 → 全部隐藏）。
        /// </summary>
        public event EventHandler IsSelectedChanged;

        /// <summary>
        /// 面板在不同设备状态下的背景色配置
        /// 空闲：白色 / 测试中：浅黄 / 故障：浅粉
        /// 【修复 L7】改为 static readonly，所有实例共享同一份颜色对象
        /// 避免每个面板（72 个）都持有副本
        /// </summary>
        private static readonly Color _normalColor = Color.White;
        private static readonly Color _testingColor = Color.LightYellow;
        private static readonly Color _faultColor = Color.LightPink;

        /// <summary>
        /// 长按选中相关配置（V1.19.5）
        /// - 长按时长：在面板空白区域按住约 0.8 秒不松手即选中该工位；
        /// - 移动阈值：按下后鼠标移动超过该距离视为"拖动"而非长按，取消计时，避免误触。
        /// </summary>
        private const int LongPressMilliseconds = 800;
        private const int LongPressMoveThreshold = 8;

        /// <summary>长按计时器（V1.19.5）：按下即启动，到时触发选中；松手/移动超阈值/离开控件即停止</summary>
        private readonly System.Windows.Forms.Timer _longPressTimer;

        /// <summary>本次按下是否已触发长按选中（V1.19.5）：已触发则松手时不再执行"单击取消"</summary>
        private bool _longPressFired;

        /// <summary>按下时的鼠标屏幕坐标（V1.19.5），用于判断长按期间鼠标是否移动</summary>
        private System.Drawing.Point _pressStartPoint;

        /// <summary>
        /// 选中框是否显示（V1.19.5）：由主窗体统一协调——
        /// 只要有任一工位被选中 → 所有面板都显示选中框；全部未选中 → 全部隐藏。
        /// </summary>
        private bool _selectionBoxVisible;

        /// <summary>
        /// 工位设置按钮点击事件
        /// 当用户点击面板右下角的"设置"按钮时触发，参数为设备编号。
        /// 【V1.24】主窗体收到事件后按当前选中工位数分流：
        /// 只选中 1 个 → 弹出该工位的工位设置窗口（StationSettingsForm）；
        /// 选中 2 个及以上 → 弹出批量设置配方窗口（BatchRecipeForm）；
        /// 若按钮所在工位未选中，主窗体先将其加入选中集合再分流。
        /// </summary>
        public event EventHandler<int> OnSetClicked;

        /// <summary>
        /// 取消全部选中请求事件（【V1.24 新增】）
        /// 在面板空白区域"长按约 0.8 秒"时，若选中框已可见（已有任一工位被选中），
        /// 触发本事件请求主窗体**取消全部选中并隐藏所有选中框**（而非选中当前工位）。
        /// 主窗体订阅后在所有面板上执行 IsSelected=false，并刷新选中框隐藏。
        /// </summary>
        public event EventHandler ClearAllSelectionRequested;

        /// <summary>
        /// 无参数构造函数（仅供 Visual Studio 设计器使用）
        /// 设计器只能实例化有无参构造函数的控件，否则会报"无法设计基类"错误。
        /// 【注意】运行时请使用 WorkstationPanelView(int deviceId) 构造函数。
        /// </summary>
        public WorkstationPanelView()
        {
            // InitializeComponent 定义在 Designer.cs 中
            // 负责创建所有控件并设置布局
            InitializeComponent();

            // 【V1.19.6】长按选中：给面板及其子控件（除按钮外）挂上鼠标按下/抬起事件，
            // 在空白区域"长按约 0.8 秒"选中该工位；选中框显示时"单击"空白区域切换选中状态。
            // 替换 V1.18 的"点击切换选中"。选中框平时全部隐藏，有选中才显示
            // （显示/隐藏由主窗体通过 SetSelectionBoxVisible 统一协调）。
            _longPressTimer = new System.Windows.Forms.Timer();
            _longPressTimer.Interval = LongPressMilliseconds;
            _longPressTimer.Tick += LongPressTimer_Tick;
            WirePanelLongPressSelect();
        }

        /// <summary>
        /// 带设备编号的构造函数（运行时使用）
        /// 通过 : this() 调用无参构造函数完成控件初始化，再设置设备编号。
        /// </summary>
        /// <param name="deviceId">设备（工位）编号（从1开始）</param>
        public WorkstationPanelView(int deviceId) : this()
        {
            DeviceId = deviceId;
            // 顶部显示工位编号，如 NO.1
            lblDeviceId.Text = $"NO.{deviceId}";
        }

        /// <summary>
        /// 给面板及其所有非按钮子控件挂接"长按选中"鼠标事件（V1.19.5 替换 V1.18 的点击切换选中）
        ///
        /// 【背景】现场希望"在工位面板空白区域长按"选中该工位。WinForms 中子控件的鼠标事件
        /// 不会自动冒泡到父控件，因此需要把面板自身及其所有子控件（按钮除外，按钮已有专属功能）
        /// 的 MouseDown / MouseUp / MouseMove / MouseLeave 都挂到同一组处理函数上。
        ///
        /// 【交互规则】
        /// - 长按（按下后约 0.8 秒不松手）→ 选中该工位；
        /// - 单击（松手时间未达长按时长）→ 选中框显示时切换该工位"选中/未选中"（V1.19.6）；
        /// - 长按期间鼠标移动超过阈值 → 视为拖动，取消长按；
        /// - 鼠标离开控件（尚未触发长按）→ 取消待触发长按。
        /// </summary>
        private void WirePanelLongPressSelect()
        {
            AttachSelectionMouse(this);
        }

        /// <summary>
        /// 递归为控件挂接"长按选中"鼠标事件（V1.19.5）
        /// </summary>
        /// <param name="control">当前控件（面板或子控件）</param>
        private void AttachSelectionMouse(Control control)
        {
            // 按钮（设置 / 选中指示）已有专属点击功能，跳过，避免误触发选中/取消
            if (control is Button) return;

            // 挂接长按选中相关鼠标事件
            control.MouseDown += Panel_MouseDown;
            control.MouseUp += Panel_MouseUp;
            control.MouseMove += Panel_MouseMove;
            control.MouseLeave += Panel_MouseLeave;

            // 递归处理所有子控件
            foreach (Control child in control.Controls)
            {
                AttachSelectionMouse(child);
            }
        }

        /// <summary>
        /// 鼠标按下（左键）：启动长按计时器（V1.19.5）
        /// 记录按下位置（屏幕坐标），开始计时；计时到点触发选中。
        /// </summary>
        private void Panel_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            _pressStartPoint = Control.MousePosition;   // 屏幕坐标，统一比较不受控件位置影响
            _longPressFired = false;
            _longPressTimer.Start();
        }

        /// <summary>
        /// 鼠标抬起（左键）（V1.19.6）
        /// - 长按已触发选中：松手不再做任何事（避免"刚选中又被切换"）；
        /// - 普通单击：仅当选中框显示（已有任一工位被选中）时执行"选中/未选中"切换；
        ///   选中框隐藏（全表未选中）时单击不动作，避免绕过"长按选中"直接点选。
        /// </summary>
        private void Panel_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            _longPressTimer.Stop();
            if (_longPressFired) return;

            // 单击 → 切换选中状态（V1.19.6：由 V1.19.5 的"单击取消"改为"切换"）
            // 例外：若这是整表唯一选中的工位，切换为未选中后全表无选中，
            // 主窗体 UpdateSelectionBoxVisibility 会自动隐藏所有选中框。
            if (_selectionBoxVisible)
            {
                IsSelected = !IsSelected;
            }
        }

        /// <summary>
        /// 鼠标移动：按住期间移动超过阈值 → 视为拖动，取消长按计时（V1.19.5）
        /// </summary>
        private void Panel_MouseMove(object sender, MouseEventArgs e)
        {
            if (_longPressFired || !_longPressTimer.Enabled) return;

            Point current = Control.MousePosition;
            if (Math.Abs(current.X - _pressStartPoint.X) > LongPressMoveThreshold ||
                Math.Abs(current.Y - _pressStartPoint.Y) > LongPressMoveThreshold)
            {
                _longPressTimer.Stop();
            }
        }

        /// <summary>
        /// 鼠标离开控件：尚未触发长按时取消计时（V1.19.5）
        /// 已触发长按（选中完成）后离开则忽略，避免误取消。
        /// </summary>
        private void Panel_MouseLeave(object sender, EventArgs e)
        {
            if (!_longPressFired)
            {
                _longPressTimer.Stop();
            }
        }

        /// <summary>
        /// 长按计时到点（V1.24 更新）
        /// - 选中框未显示（全表未选中）→ 选中该工位（首次/新增选中）；
        /// - 选中框已显示（已有任一工位被选中）→ 不选中当前工位，
        ///   触发 ClearAllSelectionRequested 请求主窗体取消全部选中并隐藏所有选中框。
        /// 标记本次按下已触发长按，松手时不再执行"单击切换"。
        /// </summary>
        private void LongPressTimer_Tick(object sender, EventArgs e)
        {
            _longPressTimer.Stop();
            _longPressFired = true;

            // 【V1.24】已有选中（选中框可见）时，长按空白处 = 取消全部选中并隐藏所有选中框
            if (_selectionBoxVisible)
            {
                ClearAllSelectionRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            IsSelected = true;
        }

        /// <summary>
        /// 更新面板显示数据
        /// 由主窗体在收到设备管理器的数据更新事件时调用（UI 线程）。
        ///
        /// 【V1.16 更新】新面板显示：
        /// - 压力值 / SN / 配方 / 延时（原样保留）
        /// - 上电状态灯（载台上电输出）
        /// - 选中指示（右上角，V1.19 原上电/下电按钮改为选中指示；V1.19.5 有选中才显示）
        /// - 真空开启显示（文字+颜色，V1.19.10：真空开=绿底 / 真空关=浅灰底；V1.28 由红改灰）
        /// - 工作状态（空闲/选中/繁忙/故障，V1.18 由英文改中文）
        /// - 面板背景色（空闲/测试中/故障；V1.19.2 起不再叠加选中高亮）
        /// </summary>
        /// <param name="data">工位数据（气压表数据）</param>
        public void UpdateData(BarometerData data)
        {
            // 防御性检查：数据为空时直接返回，避免空引用异常
            if (data == null) return;

            _currentData = data;

            // 更新设备编号显示
            lblDeviceId.Text = $"NO.{data.DeviceId}";

            // 更新真空压力值（单位：kPa，与气压表读数一致，V1.19.9 由 Pa 改为 kPa）
            txtPressure.Text = $"{data.VacuumPressure} kPa";

            // 更新序列号（V1.19.3：lblSNValue 为 Label 显示）
            lblSNValue.Text = data.SerialNumber;

            // 更新配方名称（V1.19.3：lblRecipeValue 为 Label 显示）
            lblRecipeValue.Text = data.RecipeName;

            // 更新延时时间显示（格式：时:分:秒）
            txtDelayStart.Text = data.DelayTime.ToString(@"hh\:mm\:ss");
            txtDelayArrive.Text = data.StartTime.ToString(@"hh\:mm\:ss");

            // ===== 解析 IO 输出状态（依据显耀IO表） =====
            // OutputStatus[0] = 真空电磁阀输出（控制真空开/关）
            // OutputStatus[1] = 载台上电输出（控制工位载台上电）
            bool vacuumOpen = data.OutputStatus != null && data.OutputStatus.Length >= 1 && data.OutputStatus[0];
            bool carrierPower = GetCarrierPower(data);

            // ===== 真空开启显示（V1.19.10：文字+颜色，真空开=绿底 / 真空关=浅灰底，V1.28 由红改灰） =====
            UpdateVacuumOpenDisplay(vacuumOpen);

            // ===== 上电状态灯（V1.24 起带文字：绿=上电 / 浅灰=下电，V1.28 由红改灰） =====
            UpdateStatusLight(boxPower, carrierPower);

            // ===== 工作状态显示（空闲/选中/繁忙/故障） =====
            UpdateWorkState(data.Status, carrierPower);

            // ===== 面板背景色（空闲/测试中/故障；V1.19.2 起不再叠加选中高亮） =====
            UpdateStatusColor(data.Status);

            // ===== 选中指示（右上角 ✓ / 空心方框） =====
            UpdateSelectIndicator();
        }

        /// <summary>
        /// 更新上电状态灯（boxPower，V1.24 起带文字：绿底"上电" / 灰底"下电"）
        /// V1.28：下电由红改浅灰(LightGray)黑字，与行全选按钮同色，避免红色过度醒目。
        /// </summary>
        /// <param name="ctrl">上电状态灯控件（boxPower）</param>
        /// <param name="isActive">true=上电（绿底 LimeGreen + 白字"上电"），false=下电（浅灰底 + 黑字"下电"）</param>
        private void UpdateStatusLight(Control ctrl, bool isActive)
        {
            ctrl.BackColor = isActive ? Color.LimeGreen : Color.LightGray;
            ctrl.ForeColor = isActive ? Color.White : Color.Black;
            ctrl.Text = isActive ? "上电" : "下电";
        }

        /// <summary>
        /// 更新真空开启显示（V1.19.10 起带文字 + 颜色）
        ///
        /// 【需求背景】
        /// 原真空开启灯为"纯色无文字"（绿=开启，灰=关闭），操作员需对照颜色判断，
        /// 观感不够直观。V1.19.10 改为"文字 + 颜色"显示：
        /// - 真空开启（真空电磁阀输出 ON）：绿底白字"真空开"
        /// - 真空关闭（真空电磁阀输出 OFF）：浅灰(LightGray)底黑字"真空关"
        ///
        /// 【颜色说明（V1.28）】
        /// 真空关由红改浅灰（与行全选按钮同色）——真空未开是待机常态，红色过于醒目刺眼；
        /// "故障"仍保留红色（见工作状态 boxWorkState），只有异常才用红色警示。
        /// </summary>
        /// <param name="vacuumOpen">true=真空开启，false=真空关闭</param>
        private void UpdateVacuumOpenDisplay(bool vacuumOpen)
        {
            if (vacuumOpen)
            {
                boxVacuumOpen.Text = "真空开";
                boxVacuumOpen.BackColor = Color.LimeGreen;
                boxVacuumOpen.ForeColor = Color.White;
            }
            else
            {
                boxVacuumOpen.Text = "真空关";
                boxVacuumOpen.BackColor = Color.LightGray;
                boxVacuumOpen.ForeColor = Color.Black;
            }
        }

        /// <summary>
        /// 解析载台上电输出状态（V1.18 抽取为公共方法，供 UpdateData / UpdateSelectionStyle 复用）
        /// </summary>
        /// <param name="data">工位数据</param>
        /// <returns>true=载台已上电（OutputStatus[1]），false=未上电/无数据</returns>
        private bool GetCarrierPower(BarometerData data)
        {
            return data != null && data.OutputStatus != null &&
                   data.OutputStatus.Length >= 2 && data.OutputStatus[1];
        }

        /// <summary>
        /// 工作状态显示规则（空闲 / 选中 / 繁忙 / 故障）
        ///
        /// 【状态规则】（V1.18 状态文字改为中文：空闲 / 选中 / 繁忙 / 故障）
        /// - 故障：故障（红底 + 白字，V1.19.4 由浅粉底+红字改为醒目红底，面板本身仍是浅粉打底）
        /// - 测试中：繁忙（黄底 + 白字，V1.24 由绿改为红绿灯"黄灯色"）
        /// - 空闲但已上电：选中（橙底 + 白字）——按了上电按钮、准备测试
        /// - 空闲且未上电：空闲（绿底 + 黑字，V1.24 由浅灰改为 LimeGreen，表示就绪）
        ///
        /// 【配色搭配】（V1.19.4）统一为"信号灯"色系，一眼区分状态：
        /// 红=故障（最醒目）/ 橙=已上电待测试（暖色提醒）/ 黄=测试中（V1.24 黄灯色）/ 绿=空闲就绪（V1.24）。
        ///
        /// 【V1.19.2】"是否被选中（IsSelected）"不再影响工作状态文字，
        /// 选中状态只通过右上角选中指示（btnSelect）体现。
        /// </summary>
        /// <param name="status">设备状态</param>
        /// <param name="carrierPower">载台上电是否开启</param>
        private void UpdateWorkState(DeviceStatus status, bool carrierPower)
        {
            switch (status)
            {
                case DeviceStatus.Fault:
                    boxWorkState.Text = "故障";
                    boxWorkState.BackColor = Color.Red;
                    boxWorkState.ForeColor = Color.White;
                    break;

                case DeviceStatus.Testing:
                    boxWorkState.Text = "繁忙";
                    // V1.24：红绿灯黄灯色（Gold = #FFD700）
                    boxWorkState.BackColor = Color.Gold;
                    boxWorkState.ForeColor = Color.White;
                    break;

                default: // Idle / 其它
                    if (carrierPower)
                    {
                        // 已上电但还没启动测试 → 选中（橙底 + 白字）
                        boxWorkState.Text = "选中";
                        boxWorkState.BackColor = Color.Orange;
                        boxWorkState.ForeColor = Color.White;
                    }
                    else
                    {
                        // 完全空闲 → 空闲（绿底 + 黑字，V1.24 由浅灰改为 LimeGreen）
                        boxWorkState.Text = "空闲";
                        boxWorkState.BackColor = Color.LimeGreen;
                        boxWorkState.ForeColor = Color.White;
                    }
                    break;
            }
        }

        /// <summary>
        /// 根据设备状态更新面板背景色
        /// 通过颜色直观区分设备运行状态
        /// 【V1.19.2】不再叠加选中高亮：面板背景色只反映设备状态，与是否选中无关，
        /// 选中状态仅通过右上角选中指示（btnSelect）体现。
        /// </summary>
        /// <param name="status">设备状态</param>
        private void UpdateStatusColor(DeviceStatus status)
        {
            // 故障状态优先级最高，始终显示红色
            if (status == DeviceStatus.Fault)
            {
                this.BackColor = _faultColor;
                return;
            }

            // 非故障状态：直接使用设备状态对应的背景色（不随选中状态变化）
            this.BackColor = GetStatusBackColor(status);
        }

        /// <summary>
        /// 根据设备状态获取对应的背景色
        /// 修复 L8：抽取公共方法，消除重复 switch
        /// </summary>
        /// <param name="status">设备状态</param>
        /// <returns>状态对应的背景色</returns>
        private Color GetStatusBackColor(DeviceStatus status)
        {
            switch (status)
            {
                case DeviceStatus.Idle:
                    return _normalColor;
                case DeviceStatus.Testing:
                    return _testingColor;
                case DeviceStatus.Fault:
                    return _faultColor;
                default:
                    // 未知状态默认显示空闲色
                    return _normalColor;
            }
        }

        /// <summary>
        /// 更新选中状态的视觉样式
        /// 【V1.19.2】选中状态仅通过右上角选中指示（btnSelect）体现，
        /// 面板背景色与工作状态文字不再随选中状态变化（它们只反映设备状态），
        /// 因此本方法只刷新选中指示即可。
        /// 【V1.19.5】选中框显示与否由主窗体协调（SetSelectionBoxVisible）：
        /// 只要有任一工位被选中→所有面板都显示框；全部未选中→全部隐藏。
        /// </summary>
        private void UpdateSelectionStyle()
        {
            // 显示/隐藏选中框（有选中才显示），再按自身选中状态填充样式
            btnSelect.Visible = _selectionBoxVisible;
            UpdateSelectIndicator();
        }

        /// <summary>
        /// 设置选中框是否显示（【V1.19.5】）
        /// 由主窗体统一协调：只要有任一工位被选中 → 所有面板都显示选中框；
        /// 全部未选中 → 全部隐藏（"平时全隐藏，有选中才显示"）。
        /// 显示时按自身选中状态填充样式（选中=绿底白✓ / 未选中=空心白框）。
        /// </summary>
        /// <param name="visible">true=显示选中框，false=隐藏</param>
        public void SetSelectionBoxVisible(bool visible)
        {
            if (_selectionBoxVisible == visible) return;
            _selectionBoxVisible = visible;
            UpdateSelectionStyle();
        }

        /// <summary>
        /// 更新右上角选中指示按钮的视觉样式（V1.19 新增，btnSelect）
        /// - 选中：绿底（ForestGreen）+ 白色"✓"（V1.19.5 由浅蓝底绿勾改为绿底白勾）
        /// - 未选中：空心方框（黑框白底，无文字）
        /// 【V1.19.2】选中状态仅靠此指示体现，面板背景色不再叠加高亮。
        /// </summary>
        private void UpdateSelectIndicator()
        {
            if (_isSelected)
            {
                btnSelect.Text = "✓";
                btnSelect.ForeColor = Color.White;
                btnSelect.BackColor = Color.LimeGreen;
            }
            else
            {
                btnSelect.Text = "";
                btnSelect.ForeColor = Color.Black;
                btnSelect.BackColor = Color.White;
            }
        }

        /// <summary>
        /// 获取当前显示的数据
        /// </summary>
        /// <returns>最近一次更新的工位数据</returns>
        public BarometerData GetCurrentData()
        {
            return _currentData;
        }

        /// <summary>
        /// "设置"按钮点击事件（V1.18 更名：Set → 设置）
        /// 触发 OnSetClicked 事件，通知主窗体打开工位设置窗口（StationSettingsForm）。
        /// </summary>
        private void btnSet_Click(object sender, EventArgs e)
        {
            OnSetClicked?.Invoke(this, DeviceId);
        }

        /// <summary>
        /// 选中指示按钮点击事件（V1.19：原 btnPower 上电/下电按钮改为选中指示）
        /// 【V1.19.6】选中框显示时点击 = **切换**该工位"选中/未选中"（V1.19.5 曾为"单击取消"）。
        /// 例外：若这是整表唯一选中的工位，切换为未选中后全表无选中，
        /// 主窗体 UpdateSelectionBoxVisibility 会自动隐藏所有选中框。
        /// </summary>
        private void btnSelect_Click(object sender, EventArgs e)
        {
            IsSelected = !IsSelected;
        }
    }
}
