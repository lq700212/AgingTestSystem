using System;
using System.Drawing;
using System.Windows.Forms;
using BarometerWinform.Models;

namespace BarometerWinform.Views
{
    /// <summary>
    /// 工位显示面板（子视图）—— 业务逻辑部分
    /// 每个工位对应一台气压表，本面板显示该工位的实时状态，并提供"载台上电"手动控制。
    ///
    /// 【V1.16 更名 + 重设计】
    /// 原类名 BarometerPanelView 容易让人误以为只显示气压表；实际上它显示的是 72 个
    /// "工位"（每个工位配一台气压表），因此更名为 WorkstationPanelView（工位面板）。
    ///
    /// 【界面布局】（控件创建见 <see cref="WorkstationPanelView.Designer.cs"/>）
    /// ┌──────────────────────────────┐
    /// │ NO.1                                  │  ← 设备编号
    /// │ [上电状态灯] [上电/下电按钮]           │  ← 上电灯=纯色状态；按钮控制载台上电
    /// │ 真空压力 [值] [真空开启灯] [工作状态]   │  ← 压力值只读；真空灯=纯色；工作状态文字
    /// │ SN:      [_________]                 │
    /// │ 配方:    [_________]                 │
    /// │ 延时开启 [__:__:__]      ┌────┐      │
    /// │ 延时到达 [__:__:__]      │ Set│      │
    /// │                          └────┘      │
    /// └──────────────────────────────┘
    ///
    /// 【状态灯颜色约定】
    /// - 上电状态灯（boxPower）：载台上电输出 ON=绿色，OFF=灰色（纯色无文字）
    /// - 真空开启灯（boxVacuumOpen）：真空电磁阀输出 ON=绿色，OFF=灰色（纯色无文字）
    /// - 工作状态（boxWorkState）：
    ///   空闲且未上电=IDLE；空闲但已上电=SELECT；测试中=BUSY；故障=FAULT
    /// - 上电/下电按钮（btnPower）：文字显示"要执行的动作"——未上电显示"上电"，
    ///   已上电显示"下电"；测试中/故障时禁用（防止测试中途误断电）。
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
        /// 由主窗体的"行全选"按钮控制
        /// 选中时面板背景色变为浅蓝，便于用户识别哪些面板将被批量操作
        /// </summary>
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;
                UpdateSelectionStyle();
            }
        }
        private bool _isSelected = false;

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
        /// 选中状态下的背景色（浅蓝高亮）
        /// 同样为 static readonly，所有实例共享
        /// </summary>
        private static readonly Color _selectedColor = Color.LightSkyBlue;

        /// <summary>
        /// Set按钮点击事件
        /// 当用户点击面板右下角的 Set 按钮时触发，参数为设备编号。
        /// 主窗体收到事件后弹出单台手动控制窗体（DeviceManualForm），
        /// 可查看该台 DI 报警触点、手动开/关真空阀与载台上电。
        /// </summary>
        public event EventHandler<int> OnSetClicked;

        /// <summary>
        /// 上电/下电按钮点击事件（【V1.16 新增】）
        /// 当用户点击面板的"上电/下电"按钮时触发，参数为设备编号。
        /// 主窗体收到事件后切换该工位的载台上电输出（当前上电则下电，反之上电）。
        /// </summary>
        public event EventHandler<int> OnPowerToggled;

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
        /// 更新面板显示数据
        /// 由主窗体在收到设备管理器的数据更新事件时调用（UI 线程）。
        ///
        /// 【V1.16 更新】新面板显示：
        /// - 压力值 / SN / 配方 / 延时（原样保留）
        /// - 上电状态灯（载台上电输出）
        /// - 上电/下电按钮文字 + 可用状态
        /// - 真空开启灯（真空电磁阀输出）
        /// - 工作状态（IDLE/SELECT/BUSY/FAULT）
        /// - 面板背景色（空闲/测试中/故障 + 选中高亮）
        /// </summary>
        /// <param name="data">工位数据（气压表数据）</param>
        public void UpdateData(BarometerData data)
        {
            // 防御性检查：数据为空时直接返回，避免空引用异常
            if (data == null) return;

            _currentData = data;

            // 更新设备编号显示
            lblDeviceId.Text = $"NO.{data.DeviceId}";

            // 更新真空压力值（单位：Pa，与全系统一致）
            txtPressure.Text = $"{data.VacuumPressure} Pa";

            // 更新序列号
            txtSN.Text = data.SerialNumber;

            // 更新配方名称
            txtRecipe.Text = data.RecipeName;

            // 更新延时时间显示（格式：时:分:秒）
            txtDelayStart.Text = data.DelayStartTime.ToString(@"hh\:mm\:ss");
            txtDelayArrive.Text = data.DelayArriveTime.ToString(@"hh\:mm\:ss");

            // ===== 解析 IO 输出状态（依据显耀IO表） =====
            // OutputStatus[0] = 真空电磁阀输出（控制真空开/关）
            // OutputStatus[1] = 载台上电输出（控制工位载台上电）
            bool vacuumOpen = data.OutputStatus != null && data.OutputStatus.Length >= 1 && data.OutputStatus[0];
            bool carrierPower = data.OutputStatus != null && data.OutputStatus.Length >= 2 && data.OutputStatus[1];

            // ===== 真空开启灯（纯色：绿=开启，灰=关闭） =====
            UpdateStatusLight(boxVacuumOpen, vacuumOpen);

            // ===== 上电状态灯（纯色：绿=已上电，灰=未上电） =====
            UpdateStatusLight(boxPower, carrierPower);

            // ===== 上电/下电按钮文字（显示"要执行的动作"） =====
            // 未上电 → 按钮显示"上电"（点击后上电）；已上电 → 显示"下电"（点击后下电）
            btnPower.Text = carrierPower ? "下电" : "上电";

            // ===== 工作状态显示（IDLE/SELECT/BUSY/FAULT） =====
            UpdateWorkState(data.Status, carrierPower);

            // ===== 面板背景色（空闲/测试中/故障 + 选中高亮） =====
            UpdateStatusColor(data.Status);

            // ===== 上电按钮可用性：测试中/故障时禁用 =====
            // 防止测试中途误断电（停止由"停止运行/急停"按钮负责）
            btnPower.Enabled = (data.Status == DeviceStatus.Idle);
        }

        /// <summary>
        /// 更新状态灯颜色（纯色灯通用方法）
        /// </summary>
        /// <param name="ctrl">状态灯控件（boxPower / boxVacuumOpen）</param>
        /// <param name="isActive">true=绿色（开），false=灰色（关）</param>
        private void UpdateStatusLight(Control ctrl, bool isActive)
        {
            ctrl.BackColor = isActive ? Color.LimeGreen : Color.LightGray;
        }

        /// <summary>
        /// 更新工作状态显示（IDLE / SELECT / BUSY / FAULT）
        ///
        /// 【状态规则】（按现场草图语义）
        /// - 故障：FAULT（浅粉底 + 红字）
        /// - 测试中：BUSY（绿底 + 白字）
        /// - 空闲但已上电：SELECT（橙底 + 白字）——按了上电按钮、准备测试
        /// - 空闲且未上电：IDLE（灰底 + 黑字）
        /// </summary>
        /// <param name="status">设备状态</param>
        /// <param name="carrierPower">载台上电是否开启</param>
        private void UpdateWorkState(DeviceStatus status, bool carrierPower)
        {
            switch (status)
            {
                case DeviceStatus.Fault:
                    boxWorkState.Text = "FAULT";
                    boxWorkState.BackColor = _faultColor;
                    boxWorkState.ForeColor = Color.Red;
                    break;

                case DeviceStatus.Testing:
                    boxWorkState.Text = "BUSY";
                    boxWorkState.BackColor = Color.LimeGreen;
                    boxWorkState.ForeColor = Color.White;
                    break;

                default: // Idle / 其它
                    if (carrierPower)
                    {
                        // 已上电但还没启动测试 → 待测试（SELECT）
                        boxWorkState.Text = "SELECT";
                        boxWorkState.BackColor = Color.Orange;
                        boxWorkState.ForeColor = Color.White;
                    }
                    else
                    {
                        // 完全空闲 → IDLE
                        boxWorkState.Text = "IDLE";
                        boxWorkState.BackColor = Color.LightGray;
                        boxWorkState.ForeColor = Color.Black;
                    }
                    break;
            }
        }

        /// <summary>
        /// 根据设备状态更新面板背景色
        /// 通过颜色直观区分设备运行状态
        /// 修复 H8：故障状态优先级最高，不会被选中色遮蔽
        /// </summary>
        /// <param name="status">设备状态</param>
        private void UpdateStatusColor(DeviceStatus status)
        {
            // 故障状态优先级最高，始终显示红色（不论是否选中）
            if (status == DeviceStatus.Fault)
            {
                this.BackColor = _faultColor;
                return;
            }

            // 非故障状态下，根据选中状态决定背景色
            if (_isSelected)
            {
                // 选中状态：背景色叠加浅蓝高亮
                this.BackColor = _selectedColor;
            }
            else
            {
                // 未选中：恢复为当前设备状态对应的背景色
                this.BackColor = GetStatusBackColor(status);
            }
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
        /// 选中时背景色变为浅蓝（但故障状态优先级更高，始终显示红色）
        /// </summary>
        private void UpdateSelectionStyle()
        {
            if (_currentData != null)
            {
                // 复用 UpdateStatusColor，确保故障状态优先级最高
                UpdateStatusColor(_currentData.Status);
            }
            else
            {
                // 数据未初始化时，根据选中状态显示
                this.BackColor = _isSelected ? _selectedColor : _normalColor;
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
        /// Set 按钮点击事件
        /// 触发 OnSetClicked 事件，通知主窗体打开单台手动控制窗口（DeviceManualForm）。
        /// </summary>
        private void btnSet_Click(object sender, EventArgs e)
        {
            OnSetClicked?.Invoke(this, DeviceId);
        }

        /// <summary>
        /// 上电/下电按钮点击事件（【V1.16 新增】）
        /// 触发 OnPowerToggled 事件，由主窗体切换该工位的载台上电输出。
        /// </summary>
        private void btnPower_Click(object sender, EventArgs e)
        {
            OnPowerToggled?.Invoke(this, DeviceId);
        }
    }
}
