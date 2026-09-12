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
    ///
    /// 【界面】（SunnyUI 控件、蓝色主题，与通讯测试窗体一致，V1.21 重构）
    /// ┌──────────────────────────────────────────────────┐
    /// │ [●]未连接       冷却送风机通讯测试（UIForm 标题）  │ ← 蓝色标题栏，ShowTitle
    /// ├──────────────────────────────────────────────────┤
    /// │ pnlHeader：● UILedBulb 连接指示灯 + 连接状态文字   │ ← 顶部状态条
    /// ├──────────────────────────────────────────────────┤
    /// │ pnlConn（连接参数，只读显示主程序配置）：          │
    /// │   送风机 IP: [xxx.xxx.xxx.xxx]  端口: [502]       │
    /// │   [连接测试]                                      │
    /// ├──────────────────────────────────────────────────┤
    /// │ pnlStatus（实时状态）：                           │
    /// │   温度: [xx.x]℃   湿度: [xx.x]%   运行状态: [运行/停止] │
    /// │   [定值启动]  [定值停止]  [读取状态]              │
    /// ├──────────────────────────────────────────────────┤
    /// │ txtLog（日志框，只读多行，Dock Bottom）            │
    /// └──────────────────────────────────────────────────┘
    /// </summary>
    public partial class FanTestForm : UIForm
    {
        /// <summary>设备管理器（送风机连接的唯一所有者；本窗体复用它的共享连接）</summary>
        private readonly DeviceManager _deviceManager;

        /// <summary>设备配置（读取送风机连接参数，只读显示）</summary>
        private readonly DeviceConfig _config;

        /// <summary>是否已连接（镜像 DeviceManager.IsFanConnected，由刷新定时器跟随）</summary>
        private volatile bool _connected;

        /// <summary>窗体已关闭标记（V1.72.14 新增，与通讯窗同因：拦后台命令/重连线程在关闭后碰句柄）</summary>
        private volatile bool _closed;

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

        /// <summary>关闭窗体时：停止定时器（**不**断开连接——连接归主程序所有）
        /// 【V1.72.14】首行置 _closed 拦后台 RunOnUi/AppendLog；定时器 Stop+Dispose 包 try。</summary>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _closed = true;
            try
            {
                if (_refreshTimer != null)
                {
                    _refreshTimer.Stop();
                    _refreshTimer.Dispose();
                }
            }
            catch { }
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

        /// <summary>后台执行按需重连（不弹窗）：成功更新状态并启动自动刷新，失败只写日志
        /// 【V1.72.15】关后不碰硬件（与通讯窗 TryConnectSilent 同因）。</summary>
        private void ConnectInternal()
        {
            if (_closed || IsDisposed || Disposing) return;
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
                // 【V1.72.15】执行期再查：排队期间关窗则定时器已释放，Start 即炸，丢弃。
                if (ok && !_closed && !IsDisposed && !Disposing && _refreshTimer != null)
                {
                    try { _refreshTimer.Start(); } catch { }
                }
            });
        }

        // ===================== 状态读取 =====================

        /// <summary>自动刷新定时器触发：从主程序缓存读状态刷新标签（上一拍未完成则跳过）
        /// 【V1.72.14】包 try/finally：旧版异常即卡死 _refreshBusy（以后永不刷新）；关窗直接丢弃。</summary>
        private void OnRefreshTick(object sender, EventArgs e)
        {
            if (_closed || IsDisposed || Disposing) return;
            if (_refreshBusy) return;
            _refreshBusy = true;
            try
            {
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
            }
            catch { }
            finally
            {
                _refreshBusy = false;
            }
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

        /// <summary>后台执行控制命令并记录结果（走主程序共享连接）
        /// 【V1.72.15】关后不发硬件命令：关闭瞬间在途的启停若继续下发，等于"关了窗还在动设备"。</summary>
        private void RunCommand(string name, Func<bool> action)
        {
            if (_closed || IsDisposed || Disposing) return;
            if (!_connected)
            {
                MessageBox.Show("未连接送风机，请先连接", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            AppendLog($"[{name}] 正在执行...");
            Task.Run(() =>
            {
                // 发命令前再查一次：排队期间关窗即停手。
                if (_closed || IsDisposed || Disposing) return;
                bool ok = false;
                try { ok = action(); } catch (Exception ex) { RunOnUi(() => AppendLog($"[错误] {name} 异常: {ex.Message}")); return; }
                var result = ok;
                RunOnUi(() => AppendLog(result ? $"[{name}] 成功" : $"[{name}] 失败（请检查通讯）"));
            });
        }

        // ===================== UI 辅助 =====================

        /// <summary>更新连接状态指示（LED + 文字）
        /// 【V1.72.14】关窗竞态下已排队回调进到这里即拦（与通讯窗 SetConnected 同因）。</summary>
        private void SetConnected(bool connected)
        {
            if (_closed || IsDisposed || Disposing) { _connected = connected; return; }
            _connected = connected;
            try
            {
                if (ledStatus == null || ledStatus.IsDisposed) return;
                if (lblStatus == null || lblStatus.IsDisposed) return;
                ledStatus.Color = connected ? Color.Lime : Color.FromArgb(230, 80, 80);
                ledStatus.On = true;
                lblStatus.Text = connected ? "已连接" : "未连接";
                lblStatus.ForeColor = connected
                    ? Color.FromArgb(0, 150, 80)
                    : Color.FromArgb(30, 80, 160);
            }
            catch { }
        }

        /// <summary>用读取到的实时数据刷新各状态标签
        /// 【V1.72.14】同上，排队回调关窗后丢弃。</summary>
        private void UpdateStatus(FanData d)
        {
            if (_closed || IsDisposed || Disposing) return;
            if (d == null) return;
            try
            {
                if (lblRunState == null || lblRunState.IsDisposed) return;
                if (lblTemp == null || lblTemp.IsDisposed) return;
                if (lblHumidity == null || lblHumidity.IsDisposed) return;
                if (lblTempSet == null || lblTempSet.IsDisposed) return;
                if (lblHumSet == null || lblHumSet.IsDisposed) return;
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
            catch { }
        }

        /// <summary>
        /// 运行状态枚举 → 中文显示文本
        /// 【V1.63】与主窗体口径对齐：ProgramRunning 显示"程式运行中"
        /// （原来本窗叫"程式启动"，两窗对不上）；未知态保持 "--" 不动
        /// （主窗把未知显示成"已连接"偏 misleading，测试窗诚实一点）。
        /// </summary>
        private static string GetStateText(FanRunState state)
        {
            switch (state)
            {
                case FanRunState.ProgramStopped: return "程式停止";
                case FanRunState.ProgramRunning: return "程式运行中";
                case FanRunState.FixedValueStopped: return "定值停止";
                case FanRunState.FixedValueRunning: return "定值启动";
                default: return "--";
            }
        }

        /// <summary>切换到 UI 线程执行（窗体已销毁时安全跳过）
        /// 【V1.72.14】加 _closed + 句柄双查：旧版只查 IsDisposed，关闭瞬间后台命令仍能 BeginInvoke
        /// 进已销毁句柄（与通讯窗同病根）；action 本体包 try，防关窗竞态下 lbl/txt 已释放。</summary>
        private void RunOnUi(Action action)
        {
            if (action == null) return;
            if (_closed || IsDisposed || Disposing) return;
            try
            {
                if (!IsHandleCreated) return;
            }
            catch { return; }
            try
            {
                if (InvokeRequired) BeginInvoke(action);
                else action();
            }
            catch { }
        }

        /// <summary>追加一行带时间戳的日志
        /// 【V1.72.14】旧版直调 txtLog.AppendText（调用方全在 UI 线程时没事，但关窗竞态下句柄已毁即炸）；
        /// 现与通讯窗同口径：关闭/释放/句柄三查 + 全程 try，丢日志不炸框。</summary>
        private void AppendLog(string message)
        {
            if (_closed || IsDisposed || Disposing) return;
            try
            {
                if (txtLog == null || txtLog.IsDisposed) return;
                if (!IsHandleCreated || !txtLog.IsHandleCreated) return;
                if (txtLog.InvokeRequired)
                {
                    try { txtLog.BeginInvoke(new Action<string>(AppendLogInner), message); }
                    catch { }
                    return;
                }
                AppendLogInner(message);
            }
            catch { }
        }

        /// <summary>日志真正落盘（必在 UI 线程调）。</summary>
        private void AppendLogInner(string message)
        {
            if (_closed || IsDisposed || Disposing) return;
            try
            {
                if (txtLog == null || txtLog.IsDisposed) return;
                txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch { }
        }
    }
}
