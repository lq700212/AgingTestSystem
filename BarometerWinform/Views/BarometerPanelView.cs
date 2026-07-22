using System;
using System.Drawing;
using System.Windows.Forms;
using BarometerWinform.Models;
using BarometerWinform.Services;

namespace BarometerWinform.Views
{
    /// <summary>
    /// 气压表显示面板（子视图）—— 业务逻辑部分
    /// 用于显示单个气压表的实时数据和状态
    /// 每个面板对应一个气压表设备
    ///
    /// 【说明】
    /// 本文件只包含业务逻辑（数据更新、状态切换、事件处理）。
    /// 界面控件的创建和布局代码在 <see cref="BarometerPanelView.Designer.cs"/> 文件中，
    /// 由 Visual Studio 设计器自动维护，请勿手动修改 Designer.cs 中的控件布局。
    ///
    /// 面板布局说明（V1.09 更新，依据显耀IO表）:
    /// ┌──────────────────────────────┐
    /// │ NO.1                    空闲 │  ← 设备编号 + 状态标签
    /// │ ┌──────┐ ┌──────┐ ┌──────┐ │
    /// │ │负压表│ │真空阀│ │载台电│ │  ← IO状态显示（绿=导通，灰=断开）
    /// │ │ X000 │ │ Y000 │ │ Y110 │ │  ← 第2行显示物理地址(三菱八进制)
    /// │ └──────┘ └──────┘ └──────┘ │
    /// │ 真空压力: [_________] Pa     │  ← 压力值显示
    /// │ SN:      [_________]        │  ← 序列号
    /// │ 配方:    [_________]        │  ← 当前配方
    /// │ 延时开启: [__:__:__] ┌────┐ │  ← 延时设置（合并Set按钮）
    /// │ 延时到达: [__:__:__] │ Set│ │
    /// │                      └────┘ │
    /// └──────────────────────────────┘
    ///
    /// 【V1.09 IO显示说明】
    /// - boxInput1: 真空负压表输入(NPN, X地址), 绿=导通(传感器拉低电平)
    /// - boxOutput1: 真空电磁阀输出(PNP, Y地址), 绿=导通(输出+24V驱动继电器)
    /// - boxOutput2: 载台上电输出(PNP, Y地址), 绿=导通(输出+24V驱动继电器)
    /// </summary>
    /// <remarks>
    /// 【修复 H10】设计器报错"无法设计基类 System.Void"
    ///
    /// 【问题原因】
    /// 当 .cs 文件包含中文字符但没有 UTF-8 BOM 时，VS 设计器使用的 CodeDom 解析器
    /// 可能错误识别文件编码，导致中文注释乱码，进而无法正确解析类声明。
    /// 设计器无法识别基类（UserControl）时，会回退到默认基类 System.Void，
    /// 报"无法设计基类 System.Void"错误。
    ///
    /// 【修复方法】
    /// 1. 将基类声明从 `: UserControl` 改为 `: System.Windows.Forms.UserControl`
    ///    使用完整命名空间路径，避免设计器因 using 语句解析失败而找不到基类
    /// 2. 将所有 .cs 文件保存为 UTF-8 with BOM 编码（见编码转换脚本）
    /// </remarks>
    public partial class BarometerPanelView : System.Windows.Forms.UserControl
    {
        /// <summary>
        /// 所属设备编号（从1开始）
        /// 运行时通过带参数构造函数赋值；设计器预览时为0
        /// </summary>
        public int DeviceId { get; private set; }

        /// <summary>
        /// 当前显示的气压表数据（最近一次 UpdateData 的快照）
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
        /// 当用户点击合并后的 Set 按钮时触发
        /// 参数为设备编号，便于主窗体区分是哪个面板发出的请求
        /// 主窗体收到事件后可弹出参数设置窗体，让用户选择设置"延时开启"或"延时到达"
        /// </summary>
        public event EventHandler<int> OnSetClicked;

        /// <summary>
        /// 无参数构造函数（仅供 Visual Studio 设计器使用）
        /// 设计器只能实例化有无参构造函数的控件，否则会报"无法设计基类"错误
        ///
        /// 【修复 M11】运行时请勿调用此构造函数，会导致 DeviceId=0 的非法实例
        /// 正确用法：使用 BarometerPanelView(int deviceId) 构造函数
        /// </summary>
        public BarometerPanelView()
        {
            // InitializeComponent 定义在 Designer.cs 中
            // 负责创建所有控件并设置布局
            InitializeComponent();
        }

        /// <summary>
        /// 带设备编号的构造函数（运行时使用）
        /// 通过 : this() 调用无参构造函数完成控件初始化，再设置设备编号
        /// 【V1.09 新增】根据设备编号设置3个IO状态框的文本(功能名 + 物理地址)
        ///
        /// 【注意】此重载使用默认 totalBarometers=72。
        /// 推荐使用 BarometerPanelView(int deviceId, int totalBarometers) 重载,
        /// 以确保配置变更时载台上电物理地址正确。
        /// </summary>
        /// <param name="deviceId">设备编号（从1开始）</param>
        public BarometerPanelView(int deviceId) : this(deviceId, 72)
        {
        }

        /// <summary>
        /// 带设备编号和气压表总数的构造函数（运行时推荐使用）
        /// 通过 : this() 调用无参构造函数完成控件初始化，再设置设备编号和IO标签
        /// 【修复】原 UpdateIoBoxLabels 硬编码 72, 改为通过参数传入, 配置变更时地址正确
        /// </summary>
        /// <param name="deviceId">设备编号（从1开始）</param>
        /// <param name="totalBarometers">气压表总数（用于计算载台上电Y地址偏移）</param>
        public BarometerPanelView(int deviceId, int totalBarometers) : this()
        {
            DeviceId = deviceId;
            // 根据设备编号更新顶部显示
            lblDeviceId.Text = $"NO.{deviceId}";

            // 【V1.09 新增】设置IO状态框文本: 第1行功能名, 第2行物理地址(三菱八进制)
            // 通过 IoMapBuilder 获取该设备的IO点映射(1输入 + 2输出)
            UpdateIoBoxLabels(deviceId, totalBarometers);
        }

        /// <summary>
        /// 更新3个IO状态框的标签文本（功能名 + 物理地址）
        /// 【V1.09 新增】依据显耀IO表，通过 IoMapBuilder 获取该设备对应的IO点定义:
        /// - boxInput1:  真空负压表输入(NPN, X地址)
        /// - boxOutput1:  真空电磁阀输出(PNP, Y地址)
        /// - boxOutput2:  载台上电输出(PNP, Y地址)
        ///
        /// 文本格式为2行: 第1行功能简称, 第2行物理地址(如 "负压表\r\nX000")
        /// </summary>
        /// <param name="deviceId">设备编号(1 ~ TotalBarometers)</param>
        /// <param name="totalBarometers">气压表总数(用于载台上电Y地址偏移计算)</param>
        private void UpdateIoBoxLabels(int deviceId, int totalBarometers)
        {
            // 通过 IoMapBuilder 获取该设备的IO点映射
            // totalBarometers 影响载台上电地址: Y + octal(totalBarometers + deviceId - 1)
            DeviceIoMapping mapping = IoMapBuilder.GetDeviceMapping(deviceId, totalBarometers);

            // 真空负压表输入框: 显示 "负压表" + X地址(如 X000)
            boxInput1.Text = $"负压表\r\n{mapping.VacuumPressureInput.PhysicalAddress}";

            // 真空电磁阀输出框: 显示 "真空阀" + Y地址(如 Y000)
            boxOutput1.Text = $"真空阀\r\n{mapping.VacuumValveOutput.PhysicalAddress}";

            // 载台上电输出框: 显示 "载台电" + Y地址(如 Y110)
            boxOutput2.Text = $"载台电\r\n{mapping.CarrierPowerOutput.PhysicalAddress}";
        }

        /// <summary>
        /// 更新面板显示数据
        /// 由主窗体在收到设备管理器的数据更新事件时调用
        /// </summary>
        /// <param name="data">气压表数据</param>
        public void UpdateData(BarometerData data)
        {
            // 防御性检查：数据为空时直接返回，避免空引用异常
            if (data == null) return;

            _currentData = data;

            // 更新设备编号显示
            lblDeviceId.Text = $"NO.{data.DeviceId}";

            // 更新真空压力值（单位：Pa）
            txtPressure.Text = $"{data.VacuumPressure} Pa";

            // 更新序列号
            txtSN.Text = data.SerialNumber;

            // 更新配方名称
            txtRecipe.Text = data.RecipeName;

            // 更新IO状态显示（输入1个、输出2个）
            UpdateIoStatus(data);

            // 更新延时时间显示（格式：时:分:秒）
            txtDelayStart.Text = data.DelayStartTime.ToString(@"hh\:mm\:ss");
            txtDelayArrive.Text = data.DelayArriveTime.ToString(@"hh\:mm\:ss");

            // 根据设备状态更新面板背景色
            UpdateStatusColor(data.Status);

            // 更新右上角状态标签
            lblStatus.Text = GetStatusText(data.Status);
            lblStatus.ForeColor = GetStatusColor(data.Status);
        }

        /// <summary>
        /// 更新IO状态显示
        /// 根据输入输出状态设置相应控件的背景色
        /// 状态为 true 时显示绿色（导通），false 时显示灰色（断开）
        ///
        /// 【V1.09 更新】依据显耀IO表，每个气压表对应:
        /// - 1个输入(真空负压表, NPN): boxInput1
        /// - 2个输出(真空电磁阀 + 载台上电, PNP): boxOutput1、boxOutput2
        /// </summary>
        /// <param name="data">气压表数据</param>
        private void UpdateIoStatus(BarometerData data)
        {
            // 更新输入状态（1个输入: 真空负压表）
            if (data.InputStatus != null && data.InputStatus.Length >= 1)
            {
                UpdateIoBoxColor(boxInput1, data.InputStatus[0]);
            }

            // 更新输出状态（2个输出: 真空电磁阀 + 载台上电）
            if (data.OutputStatus != null && data.OutputStatus.Length >= 2)
            {
                UpdateIoBoxColor(boxOutput1, data.OutputStatus[0]);
                UpdateIoBoxColor(boxOutput2, data.OutputStatus[1]);
            }
        }

        /// <summary>
        /// 更新单个IO状态框的颜色
        /// </summary>
        /// <param name="control">状态框控件（Label类型）</param>
        /// <param name="isActive">是否为激活状态（true=导通，false=断开）</param>
        private void UpdateIoBoxColor(Control control, bool isActive)
        {
            control.BackColor = isActive ? Color.LimeGreen : Color.LightGray;
            control.ForeColor = isActive ? Color.White : Color.Black;
        }

        /// <summary>
        /// 根据设备状态更新面板背景色
        /// 通过颜色直观区分设备运行状态
        /// 修复 H8：故障状态优先级最高，不会被选中色遮蔽
        /// 修复 L8：抽取 GetStatusBackColor 消除重复 switch
        /// 修复 L10：添加 default 分支，扩展枚举时也能正确处理
        /// </summary>
        /// <param name="status">设备状态</param>
        private void UpdateStatusColor(DeviceStatus status)
        {
            // 故障状态优先级最高，始终显示红色（不论是否选中）
            // 修复 H8：避免选中色遮蔽故障告警
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
        /// 修复 L8：抽取公共方法，消除 UpdateStatusColor 和 UpdateStatusColorOnly 的重复 switch
        /// 修复 L10：添加 default 分支，未来扩展 DeviceStatus 枚举时默认显示空闲色
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
                    // 修复 L10：未知状态默认显示空闲色
                    return _normalColor;
            }
        }

        /// <summary>
        /// 更新选中状态的视觉样式
        /// 选中时背景色变为浅蓝（但故障状态优先级更高，始终显示红色）
        /// 注意：本控件 BorderStyle=FixedSingle 不支持自定义颜色，
        /// 因此用 BackColor 高亮（选中时叠加浅蓝色）来体现选中状态
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
        /// 获取状态显示文本
        /// </summary>
        /// <param name="status">设备状态</param>
        /// <returns>中文状态文本</returns>
        private string GetStatusText(DeviceStatus status)
        {
            switch (status)
            {
                case DeviceStatus.Idle: return "空闲";
                case DeviceStatus.Testing: return "测试中";
                case DeviceStatus.Fault: return "故障";
                default: return "未知";
            }
        }

        /// <summary>
        /// 获取状态显示颜色
        /// </summary>
        /// <param name="status">设备状态</param>
        /// <returns>状态对应的颜色</returns>
        private Color GetStatusColor(DeviceStatus status)
        {
            switch (status)
            {
                case DeviceStatus.Idle: return Color.Green;
                case DeviceStatus.Testing: return Color.Orange;
                case DeviceStatus.Fault: return Color.Red;
                default: return Color.Gray;
            }
        }

        /// <summary>
        /// 获取当前显示的数据
        /// </summary>
        /// <returns>最近一次更新的气压表数据</returns>
        public BarometerData GetCurrentData()
        {
            return _currentData;
        }

        /// <summary>
        /// Set 按钮点击事件
        /// 触发 OnSetClicked 事件，通知主窗体打开参数设置窗口
        /// 主窗体收到事件后，可弹出一个统一的参数设置窗体，让用户选择设置"延时开启"或"延时到达"
        /// </summary>
        private void btnSet_Click(object sender, EventArgs e)
        {
            OnSetClicked?.Invoke(this, DeviceId);
        }
    }
}
