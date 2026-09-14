using System;
using System.Drawing;
using System.Windows.Forms;
using AgingTestSystem.Services;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 软件激活窗（【V1.87】与 HJVision 的 SoftActivation 同布局同流程，
    /// 同一套《获取激活码》工具通用）。
    ///
    /// 【流程】打开显示设备ID/设备码/激活状态 → 用户找厂商拿激活码
    /// （厂商用《获取激活码》工具：设备码→30天码/永久码）→ 输入点激活：
    /// 对上永久码写 RunHash2=Encrypt(设备ID+"ALL")；对上30天码写
    /// RunHash2=Encrypt(设备ID+"0")；对不上静默无操作（与 HJVision 一致）。
    /// 设备绑定（RunHash1）出厂手写进 MainSetting.ini，本窗不写（与 HJVision 一致）。
    ///
    /// 界面布局（ClientSize 409x433；HJVision 原版 409x398，UIForm 自绘蓝标题
    /// 占 35px，整体下移 35px + 窗体加高，V1.71 口径）：
    /// ┌──────────────────────────────────────┐
    /// │ UIForm 标题栏（SunnyUI 蓝色） 软件激活 │
    /// ├──────────────────────────────────────┤
    /// │ 激活状态：…                          │ Row0（_lblStatus）
    /// │ ┌设备ID───────────────┐              │ Row1（_grpDeviceId + _txtDeviceId，只读）
    /// │ ┌设备码───────────────┐              │ Row2（_grpDeviceCode + _txtDeviceCode，只读）
    /// │ ┌激活码───────────────┐              │ Row3（_grpActivation + _txtActivationCode，可输）
    /// │           [激活]                   │ Row4（_btnActivate，弹窗确认蓝）
    /// └──────────────────────────────────────┘
    /// 【harness】无参构造、不依赖主窗体；RefreshStatus 公开，自动化可显式触发；
    /// 只读值不断言弹窗（构造/刷新全程吞异常保界面必开）。
    /// </summary>
    public partial class SoftActivation : Sunny.UI.UIForm
    {
        /// <summary>
        /// 本次打开是否激活成功过（【V1.88.1 新增】付费即恢复用）。
        /// <para>做什么：记住“用户这次有没有输对过一次码”。</para>
        /// <para>为什么这么写：主窗要在弹窗关闭后决定“要不要重查状态解灰”，
        /// 不能只看弹窗关没关——用户打开看看就关、输错码关，都不该触发重查；
        /// 只有真写过一次 RunHash2 才值得重读一次 ini。</para>
        /// <para>怎么改：只在 <see cref="BtnActivate_Click"/> 写文件成功后置 true，
        /// 不提供外部 setter，不随 <see cref="RefreshStatus"/> 复位（一次成功整轮有效）。</para>
        /// </summary>
        public bool ActivatedSuccessfully { get; private set; }

        public SoftActivation()
        {
            InitializeComponent();
        }

        /// <summary>打开时回填三件套（设备ID/设备码/激活状态，与 HJVision 主窗点华骥图标时填的值一致）。</summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RefreshStatus();
        }

        /// <summary>
        /// 回填并显示状态（OnShown 与自动化探针共用；全程吞异常保界面必开）。
        /// </summary>
        public void RefreshStatus()
        {
            try
            {
                string cpuId = SoftwareActivation.GetCpuSerialNumber();
                _txtDeviceId.Text = cpuId;
                _txtDeviceCode.Text = SoftwareActivation.DeviceCode(cpuId);
                string runHash1;
                string runHash2;
                SoftwareActivation.ReadRunHash(out runHash1, out runHash2);
                int slot;
                int daysLeft;
                SoftwareActivation.ActivationStatus status = SoftwareActivation.ComputeStatus(
                    runHash1, runHash2, cpuId, out slot, out daysLeft);
                _lblStatus.Text = SoftwareActivation.StatusText(status, slot, daysLeft);
            }
            catch { }
        }

        /// <summary>
        /// 激活（与 HJVision 主窗 激活_Click 对齐：先永久后30天，对不上静默无操作）。
        /// </summary>
        private void BtnActivate_Click(object sender, EventArgs e)
        {
            try
            {
                SoftwareActivation.ActivationKind kind = SoftwareActivation.VerifyActivationCode(
                    _txtActivationCode.Text, _txtDeviceCode.Text);
                if (kind == SoftwareActivation.ActivationKind.None) return;
                string cpuId = SoftwareActivation.GetCpuSerialNumber();
                if (kind == SoftwareActivation.ActivationKind.Permanent)
                {
                    SoftwareActivation.WriteRunHash2(SoftwareActivation.PermanentMark(cpuId));
                    _lblStatus.Text = "激活状态: 永久使用";
                    ActivatedSuccessfully = true;
                }
                else
                {
                    SoftwareActivation.WriteRunHash2(SoftwareActivation.TrialStartMark(cpuId));
                    _lblStatus.Text = "激活状态: 剩余使用天数 / "
                        + SoftwareActivation.SlotDaysLeft(0).ToString();
                    ActivatedSuccessfully = true;
                }
            }
            catch { }
        }
    }
}
