using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ModbusTCPFanControllerTest
{
    public partial class FanTestForm : Form
    {
        private FanControllerClient _client;   // 通信客户端实例
        private readonly string _ip = "192.168.1.221";
        private readonly int _port = 50000;

        public FanTestForm()
        {
            InitializeComponent();
            // 初始化客户端，传入日志回调
            _client = new FanControllerClient(
                ip: _ip,
                port: _port,
                slaveId: 1,
                timeoutMs: 3000,
                logAction: AppendLog
            );
        }

        /// <summary>
        /// 窗体加载时，初始化状态显示并开始定时刷新
        /// </summary>
        private async void Form1_Load(object sender, EventArgs e)
        {
            await RefreshStateAsync();
        }

        /// <summary>
        /// 追加日志到 RichTextBox（线程安全）
        /// </summary>
        private void AppendLog(string msg)
        {
            if (rtbLog.InvokeRequired)
            {
                rtbLog.Invoke(new Action<string>(AppendLog), msg);
                return;
            }
            rtbLog.AppendText(msg + Environment.NewLine);
            rtbLog.ScrollToCaret();
        }

        /// <summary>
        /// 刷新当前状态和温度（线程安全更新UI）
        /// </summary>
        private async Task RefreshStateAsync()
        {
            try
            {
                // 使用批量读取
                var (state, temp, humidity, tempSet, humSet) = await _client.ReadAllParametersAsync();
                string stateText = state == FanCommand.FixedValueStart ? "定值运行中" :
                                   state == FanCommand.Stop ? "已停止" :
                                   state.ToString();
                UpdateUI(txtState, stateText);
                UpdateUI(txtTemperature, $"{temp:F2} °C");
                // 显示两位小数
                // 如果界面有湿度文本框，可以更新：
                // UpdateUI(txtHumidity, $"{humidity:F1} %");
                // UpdateUI(txtTempSet, $"{tempSet:F2} °C");
                // UpdateUI(txtHumSet, $"{humSet:F1} %");
            }
            catch (Exception ex)
            {
                AppendLog($"刷新状态失败: {ex.Message}");
                UpdateUI(txtState, "读取失败");
            }
        }

        /// <summary>
        /// 安全更新UI控件文本
        /// </summary>
        private void UpdateUI(Control control, string text)
        {
            if (control.InvokeRequired)
            {
                control.Invoke(new Action<Control, string>(UpdateUI), control, text);
                return;
            }
            control.Text = text;
        }

        /// <summary>
        /// 定时器触发：定时刷新状态
        /// </summary>
        private async void TimerRefresh_Tick(object sender, EventArgs e)
        {
            timerRefresh.Enabled = false;
            await RefreshStateAsync();
            timerRefresh.Enabled = true;
        }

        /// <summary>
        /// 连接测试按钮
        /// </summary>
        private async void BtnConnect_Click(object sender, EventArgs e)
        {
            btnConnect.Enabled = false;
            try
            {
                // 强制重新创建客户端以测试连接
                _client.Dispose();
                _client = new FanControllerClient(_ip, _port, 1, 3000, AppendLog);
                await _client.ReadCurrentStateAsync(); // 会触发连接
                AppendLog("连接测试成功");
            }
            catch (Exception ex)
            {
                AppendLog($"连接测试失败: {ex.Message}");
            }
            finally
            {
                btnConnect.Enabled = true;
            }
        }

        /// <summary>
        /// 定值启动按钮
        /// </summary>
        private async void BtnStart_Click(object sender, EventArgs e)
        {
            btnStart.Enabled = false;
            try
            {
                await _client.StartFixedValueAsync();
                AppendLog("定值启动命令已发送");
            }
            catch (Exception ex)
            {
                AppendLog($"启动失败: {ex.Message}");
            }
            finally
            {
                btnStart.Enabled = true;
            }
        }

        /// <summary>
        /// 定值停止按钮
        /// </summary>
        private async void BtnStop_Click(object sender, EventArgs e)
        {
            btnStop.Enabled = false;
            try
            {
                await _client.StopAsync();
                AppendLog("停止命令已发送");
            }
            catch (Exception ex)
            {
                AppendLog($"停止失败: {ex.Message}");
            }
            finally
            {
                btnStop.Enabled = true;
            }
        }

        /// <summary>
        /// 窗体关闭时释放资源
        /// </summary>
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _client?.Dispose();
            base.OnFormClosing(e);
        }
    }
}