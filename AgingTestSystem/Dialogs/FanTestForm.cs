using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using AgingTestSystem.Models;
using AgingTestSystem.Services;
using Sunny.UI;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 冷却送风机通讯测试窗体 —— 移植自测试工程 ModbusTCPFanControllerTest
    ///
    /// 【用途】
    ///   现场调试/开发阶段验证冷却送风机控制屏的 Modbus TCP 通讯与"定值启动/停止"控制。
    ///   通过主窗体"关于"下拉菜单的"送风机测试"进入（仅技术员及以上权限可见）。
    ///
    /// 【共享连接（V1.23 重构）】
    ///   本窗体**不自己建 TCP 连接、不持有 FanControllerClient**，而是复用主程序
    ///   DeviceManager 拥有的那一条送风机连接（FanControllerClient，由主采集定时器轮询）：
    ///   - 连接/按需重连 → DeviceManager.ReconnectFan()（复用主程序已建好的连接）；
    ///   - 状态读取 → DeviceManager.GetFanData()（读主程序 2s 轮询的缓存，零额外报文）；
    ///   - 定值启动/停止 → DeviceManager.StartFan() / DeviceManager.StopFan()。
    ///   效果：主界面与测试窗体共享同一条送风机连接，不再重复建立第二路 TCP。
    ///   连接参数（IP/端口）只读显示主程序配置，不可在窗体里改（要换设备改 App.config）。
    ///
    /// 【交互】
    ///   - 打开窗体自动后台按需重连；连接成功后每 2s 从主程序缓存读一次温度/湿度/运行状态。
    ///   - 定值启动/定值停止 在后台线程执行（不阻塞界面）。
    ///   - 状态标签由 UI 线程刷新；日志框只读，记录所有操作与错误。
    /// </summary>
    public partial class FanTestForm : UIForm
    {
        /// <summary>设备管理器（送风机连接的唯一所有者；本窗体复用它的共享连接）</summary>
        private readonly DeviceManager _deviceManager;

        /// <summary>设备配置（读取送风机连接参数，只读显示）</summary>
        private readonly DeviceConfig _config;

        /// <summary>是否已连接（镜像 DeviceManager.IsFanConnected，由刷新定时器跟随）</summary>
        private volatile bool _connected;

        /// <summary>自动刷新是否忙（防任务堆积：上一拍未完成时跳过下一拍）</summary>
        private volatile bool _refreshBusy;

        /// <summary>自动刷新定时器（2s，只读主程序缓存，不发报文）</summary>
        private readonly Timer _refreshTimer;

        public FanTestForm(DeviceManager deviceManager)
        {
            _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
            _config = deviceManager.Config ?? throw new ArgumentNullException(nameof(deviceManager.Config));

            InitializeComponent();

            // 连接参数只读显示主程序配置
            txtIp.Text = _config.FanIpAddress;
            txtPort.Text = _config.FanPort.ToString();

            _refreshTimer = new Timer { Interval = 2000 };
            _refreshTimer.Tick += OnRefreshTick;

            SetConnected(false);
        }

        // ===================== 生命周期 =====================

        /// <summary>窗体首次显示时：后台按需重连送风机（复用主程序共享连接）</summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            AutoConnect();
        }

        /// <summary>关闭窗体时：停止定时器（**不**断开连接——连接归主程序所有）</summary>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
            base.OnFormClosed(e);
        }

        // ===================== 自动连接 =====================

        /// <summary>后台自动连接（静默）：失败只写日志，由主程序心跳/自动重连负责恢复</summary>
        private void AutoConnect()
        {
            AppendLog("[连接] 正在连接送风机（复用主程序共享连接）...");
            Task.Run(() => ConnectInternal());
        }

        // ===================== 连接 =====================

        private void btnConnect_Click(object sender, EventArgs e)
        {
            _refreshTimer.Stop();
            AppendLog("[连接] 正在连接送风机（复用主程序共享连接）...");
            Task.Run(() => ConnectInternal());
        }

        /// <summary>后台执行按需重连（不弹窗）：成功更新状态并启动自动刷新，失败只写日志</summary>
        private void ConnectInternal()
        {
            // 生产配置未启用送风机时无法共享连接（主程序根本没建 FanControllerClient）
            if (!_config.FanEnabled)
            {
                RunOnUi(() =>
                {
                    SetConnected(false);
                    AppendLog("[连接] 送风机未启用（App.config FanEnabled=false），主程序未建立连接，无法测试");
                });
                return;
            }

            bool ok = _deviceManager.ReconnectFan();
            RunOnUi(() =>
            {
                SetConnected(ok);
                AppendLog(ok
                    ? $"[连接] 已连接送风机 {_config.FanIpAddress}:{_config.FanPort}（复用主程序共享连接）"
                    : $"[错误] 送风机 {_config.FanIpAddress}:{_config.FanPort} 连接失败（请检查 IP/网线，主程序后台会自动重连）");
                if (ok) _refreshTimer.Start();
            });
        }

        // ===================== 状态读取 =====================

        /// <summary>自动刷新定时器触发：从主程序缓存读状态刷新标签（上一拍未完成则跳过）</summary>
        private void OnRefreshTick(object sender, EventArgs e)
        {
            if (_refreshBusy) return;
            _refreshBusy = true;

            FanData data = _deviceManager.GetFanData();
            bool live = _deviceManager.IsFanConnected;
            var d = data;
            RunOnUi(() =>
            {
                if (live && d != null && d.IsOnline)
                {
                    UpdateStatus(d);
                    SetConnected(true);
                }
                else
                {
                    SetConnected(false);
                }
            });
            _refreshBusy = false;
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            AppendLog("[读取] 正在读取状态...");
            FanData data = _deviceManager.GetFanData();
            bool live = _deviceManager.IsFanConnected;
            var d = data;
            RunOnUi(() =>
            {
                if (live && d != null && d.IsOnline)
                {
                    UpdateStatus(d);
                    SetConnected(true);
                    AppendLog($"[读取] 状态: {GetStateText(d.RunState)}，温度 {d.Temperature:F2}°C，湿度 {d.Humidity:F2}%RH");
                }
                else
                {
                    SetConnected(false);
                    AppendLog("[读取] 读取失败（通讯中断或送风机未连接）");
                }
            });
        }

        // ===================== 控制命令 =====================

        private void btnStartFixed_Click(object sender, EventArgs e)
        {
            RunCommand("定值启动", () => _deviceManager.StartFan());
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            RunCommand("定值停止", () => _deviceManager.StopFan());
        }

        /// <summary>后台执行控制命令并记录结果（走主程序共享连接）</summary>
        private void RunCommand(string name, Func<bool> action)
        {
            if (!_connected)
            {
                MessageBox.Show("未连接送风机，请先连接", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            AppendLog($"[{name}] 正在执行...");
            Task.Run(() =>
            {
                bool ok = false;
                try { ok = action(); } catch (Exception ex) { RunOnUi(() => AppendLog($"[错误] {name} 异常: {ex.Message}")); return; }
                var result = ok;
                RunOnUi(() => AppendLog(result ? $"[{name}] 成功" : $"[{name}] 失败（请检查通讯）"));
            });
        }

        // ===================== UI 辅助 =====================

        /// <summary>更新连接状态指示（LED + 文字）</summary>
        private void SetConnected(bool connected)
        {
            _connected = connected;
            ledStatus.Color = connected ? Color.Lime : Color.FromArgb(230, 80, 80);
            ledStatus.On = true;
            lblStatus.Text = connected ? "已连接" : "未连接";
            lblStatus.ForeColor = connected
                ? Color.FromArgb(0, 150, 80)
                : Color.FromArgb(30, 80, 160);
        }

        /// <summary>用读取到的实时数据刷新各状态标签</summary>
        private void UpdateStatus(FanData d)
        {
            lblRunState.Text = GetStateText(d.RunState);
            lblRunState.ForeColor =
                (d.RunState == FanRunState.FixedValueRunning || d.RunState == FanRunState.ProgramRunning)
                    ? Color.FromArgb(0, 150, 80)
                    : Color.FromArgb(30, 80, 160);
            lblTemp.Text = $"{d.Temperature:F2} °C";
            lblHumidity.Text = $"{d.Humidity:F2} %RH";
            lblTempSet.Text = $"{d.TempSetpoint:F2} °C";
            lblHumSet.Text = $"{d.HumSetpoint:F2} %RH";
        }

        /// <summary>运行状态枚举 → 中文显示文本</summary>
        private static string GetStateText(FanRunState state)
        {
            switch (state)
            {
                case FanRunState.ProgramStopped: return "程式停止";
                case FanRunState.ProgramRunning: return "程式启动";
                case FanRunState.FixedValueStopped: return "定值停止";
                case FanRunState.FixedValueRunning: return "定值启动";
                default: return "--";
            }
        }

        /// <summary>切换到 UI 线程执行（窗体已销毁时安全跳过）</summary>
        private void RunOnUi(Action action)
        {
            if (IsDisposed) return;
            try
            {
                if (InvokeRequired) BeginInvoke(action);
                else action();
            }
            catch { }
        }

        /// <summary>追加一行带时间戳的日志</summary>
        private void AppendLog(string message)
        {
            if (IsDisposed) return;
            try
            {
                txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch { }
        }
    }
}
