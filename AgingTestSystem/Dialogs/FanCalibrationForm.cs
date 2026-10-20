using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Sunny.UI;
using AgingTestSystem.Models;
using AgingTestSystem.Services;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 送风机定点校准窗（一次校准一项：温度 或 湿度）。
    /// <para><b>入口</b>：温度 → 主界面右栏"监视"区**长按"当前温度/设置温度"**（含标题文字）；
    /// 湿度 → 主界面不显示湿度，入口在系统设置 → 冷却送风机 → "校准湿度"按钮。</para>
    /// <para><b>解决什么</b>：送风机控制屏的小数位（0.1 还是 0.01）是仪表侧参数，
    /// 协议里读不到"当前除数"字段，换型号时上位机会差 10 倍 / 100 倍。
    /// 本窗让客户自己校准：把"上位机当前读到的值"和"控制屏上读到的真值"一比，
    /// 自动算出该换多大的定点比例，写回配置并立即刷新主界面显示。</para>
    /// <para><b>为什么温度湿度分开校</b>：厂商手册里两路小数位可能不同（常见 温度×0.01、湿度×0.1），
    /// 配置里也就分成 <see cref="DeviceConfig.FanTempScale"/> / <see cref="DeviceConfig.FanHumidityScale"/> 两份；
    /// 校准窗一次只管一项，改一项不影响另一项。</para>
    /// <para><b>为什么用当前读数当探针</b>：这两路寄存器每 2 秒就在读，
    /// 不用往设备写任何东西（写斜率会改设备实际升降温速率，是有副作用的操作）。</para>
    /// <para><b>布局</b>（UIForm 自绘蓝标题占 35px，绝对定位内容从 y=47 起排；
    /// 缩放口径照抄 <c>UnloadJudgeForm</c>：<c>AutoScaleMode=None</c> + <c>ZoomScaleRect</c>，
    /// 微软雅黑 9F 配 <c>AutoScaleDimensions(6,12)</c> 会把客户区缩掉导致内容溢出）：</para>
    /// <code>
    /// ┌────────────────────────────────────────────────┐
    /// │ 送风机温度定点校准                   (蓝标题)  │  ← UIForm 自绘 35px
    /// ├────────────────────────────────────────────────┤
    /// │ 填入送风机控制屏上的温度        当前斜率 0.1    │  ← 提示一行 + 右上角斜率
    /// │  ┌──────────────────────────────────────────┐  │
    /// │  │ 温度   [ 22.20        ] °C                │  │  ← 浅灰对照卡：左填右比
    /// │  │                                          │  │
    /// │  │ 上位机当前          222.00 °C             │  │
    /// │  └──────────────────────────────────────────┘  │
    /// │  (结果/错误一行，平时留白)                      │
    /// │                             [ 确定 ]  [ 取消 ]  │
    /// └────────────────────────────────────────────────┘
    /// </code>
    /// </summary>
    public partial class FanCalibrationForm : UIForm
    {
        /// <summary>送风机通讯门面（读当前温湿度用；不建第二路连接）</summary>
        private readonly DeviceManager _deviceManager;

        /// <summary>内存中的设备配置（校准结果写回这里，配置持久化走设置表同一条路）</summary>
        private readonly DeviceConfig _config;

        /// <summary>本次校准的目标项（温度 / 湿度）</summary>
        private readonly FanCalibrationTarget _target;

        /// <summary>顶部提示（一行短句：教客户填哪个数，不堆说明）</summary>
        private UILabel _lblHint;

        /// <summary>当前斜率回显（温度斜率 / 湿度斜率，右上角）</summary>
        private UILabel _lblCurrentScale;

        /// <summary>浅灰"对照卡"（把要填的数与上位机当前数并排放，一眼能比）</summary>
        private UIPanel _pnlCard;

        /// <summary>项目标题（输入框左侧，如"温度"/"湿度"）</summary>
        private UILabel _lblItemTitle;

        /// <summary>输入框（填送风机控制屏上的真值）</summary>
        private UITextBox _txtScreen;

        /// <summary>单位（°C / %RH，跟在输入框右边）</summary>
        private UILabel _lblUnit;

        /// <summary>当前读数的小标题（"上位机当前"）</summary>
        private UILabel _lblCurrentTitle;

        /// <summary>上位机当前读数回显（供用户对照）</summary>
        private UILabel _lblCurrent;

        /// <summary>结果/错误提示（平时整行留白，出错时才出现红/绿字）</summary>
        private UILabel _lblResult;

        /// <summary>确定按钮（执行校准）</summary>
        private UIButton _btnOk;

        /// <summary>取消按钮</summary>
        private UIButton _btnCancel;

        /// <summary>窗体关闭标志（后台回调用，避免关窗后继续写控件）</summary>
        private bool _closed;

        /// <summary>
        /// 设计器预览用无参构造（空快照占位；设备管理器为 null → 显示"未连接"）。
        /// 缺了它 VS 预览报"没有无参数构造函数"，窗体在设计器里打不开。
        /// </summary>
        public FanCalibrationForm() : this(null, new DeviceConfig(), FanCalibrationTarget.Temperature)
        {
        }

        /// <summary>
        /// 打开校准窗。
        /// </summary>
        /// <param name="deviceManager">送风机通讯门面（读当前温湿度）</param>
        /// <param name="config">内存中的设备配置（校准结果写回这里）</param>
        /// <param name="target">本次校准的目标项（温度 / 湿度）</param>
        public FanCalibrationForm(DeviceManager deviceManager, DeviceConfig config, FanCalibrationTarget target)
        {
            _deviceManager = deviceManager;
            _config = config;
            _target = target;

            InitializeComponent();

            // 按目标项改窗体文案（一次只校一项，两项各有各的斜率）
            string itemName = FanCalibration.GetDisplayName(target);
            string unit = FanCalibration.GetUnit(target);
            this.Text = "送风机" + itemName + "定点校准";
            _lblItemTitle.Text = itemName;
            _lblUnit.Text = unit;
            _lblCurrentTitle.Text = "上位机当前";
            _lblCurrentScale.Text = "当前斜率 " + FormatScale(FanCalibration.GetScale(config, target));
            _txtScreen.Watermark = target == FanCalibrationTarget.Humidity ? "如 55.0" : "如 22.20";
            // 提示只留一行短句：客户只要知道"填控制屏上的哪个数"就够了
            _lblHint.Text = "填入送风机控制屏上的" + itemName;

            // 回车=确定、Esc=取消（UIButton 实现 IButtonControl，AcceptButton/CancelButton 照用）
            this.AcceptButton = _btnOk;
            this.CancelButton = _btnCancel;

            LoadCurrentReading();
        }

        /// <summary>
        /// 打开窗体并读取当前读数（供用户对照）。
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            LoadCurrentReading();
            // 焦点直接落在输入框：打开就能敲数（客户不用先点一下）
            try { _txtScreen.Focus(); } catch { }
        }

        /// <summary>
        /// 读当前读数与当前斜率并回显。
        /// 设备未连接时只把读数显示成"--"，**不在窗里再写一遍"未连接"** ——
        /// 未连接压根不会开这个窗（见 <see cref="TryShow"/>），能走到这里的只可能是
        /// "开窗之后掉线"，那种情况留给【确定】按钮点击时再报。
        /// </summary>
        private void LoadCurrentReading()
        {
            if (_closed || IsDisposed) return;

            string itemName = FanCalibration.GetDisplayName(_target);
            string unit = FanCalibration.GetUnit(_target);
            double currentScale = FanCalibration.GetScale(_config, _target);
            _lblCurrentScale.Text = "当前斜率 " + FormatScale(currentScale);

            FanData data = _deviceManager != null ? _deviceManager.GetFanData() : null;
            if (data == null || !data.IsOnline)
            {
                _lblCurrent.Text = "--";
                _lblResult.Text = string.Empty;
                return;
            }
            double reading = FanCalibration.GetReading(data, _target);
            _lblCurrent.Text = reading.ToString("0.00") + " " + unit;
            _lblResult.Text = string.Empty;
            _lblResult.ForeColor = Color.FromArgb(48, 48, 48);
        }

        /// <summary>
        /// 打开校准窗的统一入口（**先检查送风机是否连接**）。
        /// <para><b>为什么在这里挡</b>：送风机没连上就没有"上位机当前读数"这个参照物，
        /// 校准无从谈起 —— 与其弹个窗让客户对着"--"发呆，不如直接弹一句提示就完事。
        /// 窗内因此不再重复显示"送风机未连接"（红字只留给"开窗之后掉线"这一种情况，
        /// 那时点【确定】会报出来）。</para>
        /// <para><b>为什么统一走这里</b>：入口有两处（主界面长按温度 / 系统设置校准湿度），
        /// 判定与弹窗必须同源，不能各写一份。</para>
        /// </summary>
        /// <param name="deviceManager">送风机通讯门面（读当前温湿度）</param>
        /// <param name="config">内存中的设备配置（校准结果写回这里）</param>
        /// <param name="target">本次校准的目标项（温度 / 湿度）</param>
        /// <param name="owner">父窗体（模态归属用，可为 null）</param>
        /// <returns>true=已开窗；false=送风机未连接，只弹了提示没开窗</returns>
        public static bool TryShow(DeviceManager deviceManager, DeviceConfig config,
            FanCalibrationTarget target, IWin32Window owner)
        {
            if (!FanCalibration.IsDeviceOnline(deviceManager))
            {
                UIMessageBox.Show("送风机未连接，无法校准", "提示",
                    UIStyle.Orange, UIMessageBoxButtons.OK, true, 0);
                return false;
            }

            using (var form = new FanCalibrationForm(deviceManager, config, target))
            {
                ThemeManager.ApplyTo(form);
                form.ShowDialog(owner);
            }
            return true;
        }

        /// <summary>
        /// 【确定】点击 → 取输入值 → 推算新斜率 → 落盘 + 热回写 → 刷新主界面。
        /// 【一次只改一项】温度与湿度各有各的斜率，本次只写目标项那一个 key。
        /// </summary>
        private void btnOk_Click(object sender, EventArgs e)
        {
            if (_closed || IsDisposed) return;

            string itemName = FanCalibration.GetDisplayName(_target);
            double? screenValue = ParseInput(_txtScreen);
            if (screenValue == null)
            {
                ShowError("请填写控制屏读数");
                return;
            }

            FanData data = _deviceManager != null ? _deviceManager.GetFanData() : null;
            if (data == null || !data.IsOnline)
            {
                ShowError("送风机未连接，无法校准");
                return;
            }

            double currentScale = FanCalibration.GetScale(_config, _target);
            double reading = FanCalibration.GetReading(data, _target);

            double newScale;
            string error;
            if (!FanCalibration.TryComputeScale(reading, screenValue.Value, currentScale, out newScale, out error))
            {
                ShowError(itemName + "校准失败：" + error);
                return;
            }

            if (Math.Abs(newScale - currentScale) < 1e-9)
            {
                ShowResult("当前斜率 " + FormatScale(currentScale) + " 已正确，无需修改", false);
                return;
            }

            // 落盘 + 内存热回写（走设置表同一条路，配置语义只有一份）
            var changes = new System.Collections.Generic.Dictionary<string, string>
            {
                { FanCalibration.GetScaleKey(_target), FormatScale(newScale) }
            };
            Dialogs.SettingsForm.PersistResult persistResult;
            string persistError;
            if (!SettingsForm.PersistChanges(_config, changes, out persistResult, out persistError))
            {
                ShowError("保存失败：" + persistError);
                return;
            }

            // 立刻重新读一次送风机：主界面温度显示马上按新斜率刷新，不用等 2 秒周期
            if (_deviceManager != null) _deviceManager.RefreshFanData();

            ShowResult(itemName + "斜率 " + FormatScale(currentScale) + " → " + FormatScale(newScale) + "，已生效", true);

            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>
        /// 【取消】点击 → 直接关闭，不改任何配置。
        /// </summary>
        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        /// <summary>
        /// 斜率格式化成界面/配置文本（0.1 / 0.01 / 0.001 / 0.0001）
        /// </summary>
        /// <param name="scale">斜率值</param>
        /// <returns>文本</returns>
        private static string FormatScale(double scale)
        {
            return scale.ToString("0.####", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 解析输入框数值（空/非数字/非正数 = 报错并返回 null）。
        /// </summary>
        /// <param name="box">输入框</param>
        /// <returns>解析到的数值；空或非法时返回 null</returns>
        private double? ParseInput(UITextBox box)
        {
            if (box == null) return null;
            string text = (box.Text ?? string.Empty).Trim();
            if (text.Length == 0) return null;

            double value;
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) &&
                !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                ShowError("读数不是数字");
                return null;
            }
            if (value <= 0)
            {
                ShowError("读数必须大于 0");
                return null;
            }
            return value;
        }

        /// <summary>显示错误提示（红字）</summary>
        private void ShowError(string message)
        {
            if (_closed || IsDisposed) return;
            _lblResult.Text = message;
            _lblResult.ForeColor = Color.Red;
        }

        /// <summary>显示结果提示（绿字）</summary>
        private void ShowResult(string message, bool success)
        {
            if (_closed || IsDisposed) return;
            _lblResult.Text = message;
            _lblResult.ForeColor = success ? Color.ForestGreen : Color.FromArgb(48, 48, 48);
        }

        /// <summary>
        /// 窗体关闭：置关闭标志（后台回调入口自拦，避免写已释放控件）。
        /// </summary>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _closed = true;
            base.OnFormClosed(e);
        }
    }
}
