using System;
using System.Drawing;
using System.Windows.Forms;
using BarometerWinform.Models;
using BarometerWinform.Services;

namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 单台手动控制窗体（V1.10 新增）
    ///
    /// 【用途】
    /// 点击气压表面板上的"设置"按钮打开本窗体（V1.18 起主窗体已改用工位设置窗口，本窗体保留备用）。
    /// 现场调试时非常有用：接线条点对应、排查单台故障、
    /// 单独点动某台的真空阀 / 载台上电、实时查看该台 DI 报警触点状态。
    ///
    /// 【功能】
    /// - 显示该台的 IO 点位（物理地址 X / Y）
    /// - 实时显示当前压力（来自采集缓存）
    /// - 实时显示 DI 报警触点状态（每秒刷新，直接读 IO 模块）
    /// - 手动开/关真空电磁阀、开/关载台上电（按钮自动根据当前状态禁用）
    ///
    /// 【说明】
    /// 本窗体的手动操作是"单动作"入口（只改一个输出点），
    /// 与"启动运行/停止运行"的批量流程互不干扰。
    /// </summary>
    public partial class DeviceManualForm : Form
    {
        /// <summary>设备管理器（用于读压力/IO、写输出）</summary>
        private readonly DeviceManager _deviceManager;

        /// <summary>设备配置（用于计算输出点内部编号）</summary>
        private readonly DeviceConfig _config;

        /// <summary>当前操作的设备编号</summary>
        private readonly int _deviceId;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="deviceManager">设备管理器</param>
        /// <param name="config">设备配置</param>
        /// <param name="deviceId">设备编号（1 ~ TotalBarometers）</param>
        public DeviceManualForm(DeviceManager deviceManager, DeviceConfig config, int deviceId)
        {
            InitializeComponent();
            _deviceManager = deviceManager;
            _config = config;
            _deviceId = deviceId;

            this.Text = $"设备 NO.{deviceId} 手动控制";

            // 显示该台的 IO 点位信息（通过 IoMapBuilder 获取物理地址）
            DeviceIoMapping mapping = IoMapBuilder.GetDeviceMapping(deviceId, config.TotalBarometers, config.TotalInputs);
            lblIoInfo.Text =
                $"输入  : {mapping.VacuumPressureInput.PhysicalAddress}（真空负压表报警触点）\r\n" +
                $"输出1 : {mapping.VacuumValveOutput.PhysicalAddress}（真空电磁阀）\r\n" +
                $"输出2 : {mapping.CarrierPowerOutput.PhysicalAddress}（载台上电）";

            // 启动刷新定时器，实时显示状态
            timerRefresh.Start();
            RefreshDisplay();
        }

        /// <summary>
        /// 刷新显示（每秒由 timerRefresh 触发）
        /// - 压力：从采集缓存读取（采集线程每秒更新）
        /// - DI / 阀 / 载台电：直接实时读取 IO 模块，保证手动操作后立即可见
        /// </summary>
        private void RefreshDisplay()
        {
            // 压力从缓存读
            BarometerData data = _deviceManager.GetBarometerData(_deviceId);
            if (data != null)
            {
                txtPressure.Text = $"{data.VacuumPressure} Pa";
            }
            else
            {
                txtPressure.Text = "读取失败 / 离线";
            }

            // 以下 IO 状态实时读取
            bool diState = _deviceManager.GetInput(_deviceId);                                       // 真空负压表报警触点
            bool valveState = _deviceManager.GetOutput(_config.TotalInputs + _deviceId);              // 真空电磁阀
            bool carrierState = _deviceManager.GetOutput(_config.TotalInputs + _config.TotalBarometers + _deviceId); // 载台上电

            // DI 报警触点显示（ON=已触发，红色）
            lblDiState.Text = diState ? "ON（已触发）" : "OFF";
            lblDiState.ForeColor = diState ? Color.Red : Color.Green;

            // 按钮可用性跟随当前状态（已开的只显示"关"可用）
            btnValveOn.Enabled = !valveState;
            btnValveOff.Enabled = valveState;
            btnCarrierOn.Enabled = !carrierState;
            btnCarrierOff.Enabled = carrierState;
        }

        /// <summary>刷新定时器</summary>
        private void timerRefresh_Tick(object sender, EventArgs e)
        {
            RefreshDisplay();
        }

        /// <summary>开真空阀</summary>
        private void btnValveOn_Click(object sender, EventArgs e)
        {
            _deviceManager.SetOutput(_config.TotalInputs + _deviceId, true);
            RefreshDisplay();
        }

        /// <summary>关真空阀</summary>
        private void btnValveOff_Click(object sender, EventArgs e)
        {
            _deviceManager.SetOutput(_config.TotalInputs + _deviceId, false);
            RefreshDisplay();
        }

        /// <summary>载台上电</summary>
        private void btnCarrierOn_Click(object sender, EventArgs e)
        {
            _deviceManager.SetOutput(_config.TotalInputs + _config.TotalBarometers + _deviceId, true);
            RefreshDisplay();
        }

        /// <summary>载台断电</summary>
        private void btnCarrierOff_Click(object sender, EventArgs e)
        {
            _deviceManager.SetOutput(_config.TotalInputs + _config.TotalBarometers + _deviceId, false);
            RefreshDisplay();
        }

        /// <summary>关闭</summary>
        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
