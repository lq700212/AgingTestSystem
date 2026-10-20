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
    /// 送风机定点校准窗（主界面右栏"监视"区**长按"当前温度/设置温度"**进入）。
    /// <para><b>解决什么</b>：送风机控制屏的小数位（0.1 还是 0.01）是仪表侧参数，
    /// 协议里读不到"当前除数"字段，换型号时上位机温度会差 10 倍 / 100 倍。
    /// 本窗让客户自己校准：把"上位机当前读到的值"和"控制屏上读到的真值"一比，
    /// 自动算出该换多大的定点除数，写回配置并立即刷新主界面显示。</para>
    /// <para><b>为什么用温度/湿度当前读数当探针</b>：这两路寄存器每 2 秒就在读，
    /// 不用往设备写任何东西（写斜率会改设备实际升降温速率，是有副作用的操作）。</para>
    /// <para><b>布局</b>（UIForm 自绘蓝标题占 35px，绝对定位内容从 y=47 起排；
    /// 缩放口径照抄 <c>UnloadJudgeForm</c>：<c>AutoScaleMode=None</c> + <c>ZoomScaleRect</c>，
    /// 微软雅黑 9F 配 <c>AutoScaleDimensions(6,12)</c> 会把客户区缩掉导致内容溢出）：</para>
    /// <code>
    /// ┌────────────────────────────────────────────────┐
    /// │ 送风机定点校准                        (蓝标题)  │  ← UIForm 自绘 35px
    /// ├────────────────────────────────────────────────┤
    /// │ 请把送风机控制屏上读到的温度/湿度填入下面对应   │
    /// │ 的输入框，再点【确定】。                      │
    /// │                                                │
    /// │ 温度   [__________]      当前  22.20 °C       │
    /// │ 湿度   [__________]      当前  55.0 %RH       │
    /// │                                                │
    /// │ 当前定点除数：10                              │
    /// │ 校准结果：────────────────────────────────    │
    /// │                                                │
    /// │                        [ 确定 ]  [ 取消 ]      │
    /// └────────────────────────────────────────────────┘
    /// </code>
    /// </summary>
    public partial class FanCalibrationForm : UIForm
    {
        /// <summary>送风机通讯门面（读当前温湿度用；不建第二路连接）</summary>
        private readonly DeviceManager _deviceManager;

        /// <summary>内存中的设备配置（校准结果写回这里，配置持久化走设置表同一条路）</summary>
        private readonly DeviceConfig _config;

        /// <summary>顶部说明（教客户怎么读控制屏、填几项）</summary>
        private UILabel _lblHint;

        /// <summary>"温度"标题（输入框左侧）</summary>
        private UILabel _lblTempTitle;

        /// <summary>"湿度"标题（输入框左侧）</summary>
        private UILabel _lblHumidityTitle;

        /// <summary>温度输入框（填送风机控制屏上的温度真值）</summary>
        private UITextBox _txtScreenTemp;

        /// <summary>湿度输入框（填送风机控制屏上的湿度真值，可留空）</summary>
        private UITextBox _txtScreenHumidity;

        /// <summary>"当前温度"读数回显（上位机当前读到的值，供用户对照）</summary>
        private UILabel _lblCurrentTemp;

        /// <summary>"当前湿度"读数回显</summary>
        private UILabel _lblCurrentHumidity;

        /// <summary>"当前定点除数"回显</summary>
        private UILabel _lblCurrentDivisor;

        /// <summary>结果/错误提示（多行）</summary>
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
        public FanCalibrationForm() : this(null, new DeviceConfig())
        {
        }

        /// <summary>
        /// 打开校准窗。
        /// </summary>
        /// <param name="deviceManager">送风机通讯门面（读当前温湿度）</param>
        /// <param name="config">内存中的设备配置（校准结果写回这里）</param>
        public FanCalibrationForm(DeviceManager deviceManager, DeviceConfig config)
        {
            _deviceManager = deviceManager;
            _config = config;

            InitializeComponent();
            LoadCurrentReading();
        }

        /// <summary>
        /// 打开窗体并读取当前读数（供用户对照）。
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            LoadCurrentReading();
        }

        /// <summary>
        /// 读当前温湿度与当前除数并回显。
        /// 设备未连接时提示"未连接"，此时无法校准（没有可信的对照值）。
        /// </summary>
        private void LoadCurrentReading()
        {
            if (_closed || IsDisposed) return;

            int divisor = DeviceConfig.ParseFanScaleDivisor(
                (_config != null ? _config.FanScaleDivisor : 0).ToString(CultureInfo.InvariantCulture));
            _lblCurrentDivisor.Text = "当前定点除数：" + divisor;

            FanData data = _deviceManager != null ? _deviceManager.GetFanData() : null;
            if (data == null || !data.IsOnline)
            {
                _lblCurrentTemp.Text = "当前  --（送风机未连接）";
                _lblCurrentHumidity.Text = "当前  --";
                _lblResult.Text = "送风机未连接，读不到当前值，无法校准。\r\n请先确认通讯正常。";
                _lblResult.ForeColor = Color.Red;
                return;
            }

            _lblCurrentTemp.Text = "当前  " + data.Temperature.ToString("0.00") + " °C";
            _lblCurrentHumidity.Text = "当前  " + data.Humidity.ToString("0.0") + " %RH";
            _lblResult.Text = string.Empty;
            _lblResult.ForeColor = Color.FromArgb(48, 48, 48);
        }

        /// <summary>
        /// 【确定】点击 → 采集两路输入 → 推算新除数 → 落盘 + 热回写 → 刷新主界面。
        /// </summary>
        private void btnOk_Click(object sender, EventArgs e)
        {
            if (_closed || IsDisposed) return;

            double? screenTemp = ParseInput(_txtScreenTemp);
            double? screenHumidity = ParseInput(_txtScreenHumidity);

            if (screenTemp == null && screenHumidity == null)
            {
                ShowError("请至少填写一项：送风机控制屏上读到的温度或湿度。");
                return;
            }

            FanData data = _deviceManager != null ? _deviceManager.GetFanData() : null;
            if (data == null || !data.IsOnline)
            {
                ShowError("送风机未连接，读不到当前值，无法校准。");
                return;
            }

            int currentDivisor = DeviceConfig.ParseFanScaleDivisor(
                (_config != null ? _config.FanScaleDivisor : 0).ToString(CultureInfo.InvariantCulture));

            // 温度、湿度各算一次：两路若推出不同除数，说明填错了（或该机温湿度精度本就不同），
            // 本机共用一个除数，不能瞎猜 → 报错让用户复核。
            int divisorFromTemp = 0;
            int divisorFromHumidity = 0;
            string error;

            if (screenTemp != null &&
                !FanCalibration.TryComputeDivisor(data.Temperature, screenTemp.Value,
                    currentDivisor, out divisorFromTemp, out error))
            {
                ShowError("温度校准失败：" + error);
                return;
            }

            if (screenHumidity != null &&
                !FanCalibration.TryComputeDivisor(data.Humidity, screenHumidity.Value,
                    currentDivisor, out divisorFromHumidity, out error))
            {
                ShowError("湿度校准失败：" + error);
                return;
            }

            int newDivisor;
            if (screenTemp != null && screenHumidity != null)
            {
                if (divisorFromTemp != divisorFromHumidity)
                {
                    ShowError("温度推出除数 " + divisorFromTemp + "、湿度推出除数 " + divisorFromHumidity +
                              "，两者不一致。\r\n本机温湿度共用一个定点除数，请复核填入的数值" +
                              "（只填温度或只填湿度也可以）。");
                    return;
                }
                newDivisor = divisorFromTemp;
            }
            else
            {
                newDivisor = screenTemp != null ? divisorFromTemp : divisorFromHumidity;
            }

            if (newDivisor == currentDivisor)
            {
                ShowResult("当前除数 " + currentDivisor + " 已经是对的，无需修改。", false);
                return;
            }

            // 落盘 + 内存热回写（走设置表同一条路，配置语义只有一份）
            var changes = new System.Collections.Generic.Dictionary<string, string>
            {
                { "FanScaleDivisor", newDivisor.ToString(CultureInfo.InvariantCulture) }
            };
            Dialogs.SettingsForm.PersistResult persistResult;
            string persistError;
            if (!SettingsForm.PersistChanges(_config, changes, out persistResult, out persistError))
            {
                ShowError("保存失败：" + persistError);
                return;
            }

            // 立刻重新读一次送风机：主界面温度显示马上按新除数刷新，不用等 2 秒周期
            if (_deviceManager != null) _deviceManager.RefreshFanData();

            ShowResult("校准完成：定点除数 " + currentDivisor + " → " + newDivisor +
                       "，主界面温度显示已按新比例刷新。", true);

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
        /// 解析输入框数值（空 = 不参与校准；非数字/非正数 = 报错并返回 null）。
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
                ShowError("输入的不是数字：" + text);
                return null;
            }
            if (value <= 0)
            {
                ShowError("输入的值必须大于 0：" + text);
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
