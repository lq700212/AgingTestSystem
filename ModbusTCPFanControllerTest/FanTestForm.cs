using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ModbusTCPFanControllerTest
{
    public partial class FanTestForm : Form
    {
        private FanControllerClient _client;   // 通信客户端实例（保存的是"已应用"的地址）

        public FanTestForm()
        {
            InitializeComponent();
            // 【防呆】IP/端口做成可编辑输入框，默认填真实设备地址 192.168.1.220:50000。
            // 现场设备 IP 变了，可以直接在界面上改，再点【连接测试】应用即可，不用改代码重新编译。
            // 【自动识别】连接时除了界面填的 IP，还会自动尝试内置候选 IP（192.168.1.220/.221/.222），
            // 现场控制器在三个地址中的任意一个都能被识别，不用每次去改 IP。
            txtIp.Text = "192.168.1.220";
            txtPort.Text = "50000";
            // 从输入框读取地址创建客户端
            _client = CreateClientFromTextBoxes();
        }

        /// <summary>
        /// 窗体加载时，初始化状态显示并开始定时刷新
        /// </summary>
        private async void Form1_Load(object sender, EventArgs e)
        {
            // 首次加载先停掉自动刷新：如果加载中正在连接（设备连不上时连接可能要等很久），
            // 定时器第一跳（2秒）又进来刷新，两个线程同时操作同一个 _client 会竞态
            //（表现就是 EnsureConnectedAsync 里报 NullReferenceException）。
            // 等这次初始化刷新结束后再启动定时器。
            timerRefresh.Enabled = false;
            await RefreshStateAsync();
            timerRefresh.Enabled = true;
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
            // 防呆：客户端未创建（初始 IP/端口解析失败等极端情况）时不崩溃，直接提示
            if (_client == null)
            {
                AppendLog("客户端未创建，请检查 IP/端口 并点击【连接测试】");
                UpdateUI(txtState, "未创建客户端");
                return;
            }
            try
            {
                // 使用批量读取
                var (state, temp, humidity, tempSet, humSet) = await _client.ReadAllParametersAsync();
                // 状态一律用中文显示（定值启动/定值停止/程式启动/程式停止），见 GetStateText
                UpdateUI(txtState, GetStateText(state));
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
        /// 把读到的状态枚举翻译成中文显示文本
        ///
        /// 寄存器 0x0001 的取值与中文对照（见《冷却送风机 Modbus TCP 通信接口说明文档》第 3 节）：
        ///   0x0000 程式停止 / 0x0001 程式启动 / 0x0002 定值停止 / 0x0003 定值启动
        ///
        /// 【关键】如果直接 state.ToString()，遇到枚举里没定义的值（协议变化/未知码）
        /// 会打印裸数字（如 "1"）——这就是之前 txtState 显示数字的原因。
        /// 这里显式 switch 映射 + 未知值兜底，保证 txtState 永远显示中文。
        /// </summary>
        /// <param name="state">从设备读到的状态枚举</param>
        /// <returns>中文状态文本</returns>
        private static string GetStateText(FanCommand state)
        {
            switch (state)
            {
                case FanCommand.FixedValueStart: return "定值启动";
                case FanCommand.Stop:            return "定值停止";
                case FanCommand.ProgramRunning:  return "程式启动";
                case FanCommand.ProgramStopped:  return "程式停止";
                default:                         return $"未知(0x{(ushort)state:X4})";
            }
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
        /// 连接测试按钮：校验并应用界面上填写的 IP/端口，然后用新地址测试连接
        /// </summary>
        private async void BtnConnect_Click(object sender, EventArgs e)
        {
            btnConnect.Enabled = false;
            // 先停掉自动刷新定时器：本按钮会 Dispose + 重新 new 客户端，
            // 如果此刻定时刷新正在使用旧客户端（正在连接/读写），Dispose 会和它并发，
            // 把 TcpClient 半途关掉，报出 NullReferenceException 等竞态异常。
            // 停表 → 保证整个"释放旧客户端 + 连接测试"期间没有别的线程碰 _client。
            timerRefresh.Enabled = false;
            try
            {
                // 【防呆】先校验界面上填的 IP/端口，格式不对直接提示，不进入连接流程
                var newClient = CreateClientFromTextBoxes();
                if (newClient == null)
                {
                    AppendLog("连接测试已取消（请修正 IP/端口 后重试）");
                    return;
                }

                // 用新地址重建客户端，强制测试连接
                _client?.Dispose();
                _client = newClient;
                await _client.ReadCurrentStateAsync(); // 会触发连接；连不上会抛明确异常（含 3 秒连接超时提示）
                // ConnectedIp 是"自动识别真正连上的设备地址"；Ip 只是界面填的首选地址，
                // 两者可能不同（比如界面填 .220、实际设备在 .221）。日志显示实际连上的，方便现场确认。
                AppendLog($"连接测试成功（实际连接 {_client.ConnectedIp}:{_client.Port}）");
            }
            catch (Exception ex)
            {
                AppendLog($"连接测试失败: {ex.Message}");
            }
            finally
            {
                timerRefresh.Enabled = true;   // 恢复自动刷新
                btnConnect.Enabled = true;
            }
        }

        /// <summary>
        /// 定值启动按钮
        /// </summary>
        private async void BtnStart_Click(object sender, EventArgs e)
        {
            // 【防呆】客户端未创建 / 地址已修改未应用时，给明确提示而不是发到旧地址
            if (!EnsureClientReady()) return;

            btnStart.Enabled = false;
            // 和 BtnConnect_Click 同理：按钮操作和定时刷新会并发使用同一个 _client，
            // 停表避免两个线程同时发 Modbus 请求（帧交叉 / 竞态异常）。
            timerRefresh.Enabled = false;
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
                timerRefresh.Enabled = true;   // 恢复自动刷新
                btnStart.Enabled = true;
            }
        }

        /// <summary>
        /// 定值停止按钮
        /// </summary>
        private async void BtnStop_Click(object sender, EventArgs e)
        {
            // 【防呆】同上：先确认客户端可用、地址已应用
            if (!EnsureClientReady()) return;

            btnStop.Enabled = false;
            // 和 BtnConnect_Click 同理：按钮操作和定时刷新会并发使用同一个 _client，
            // 停表避免两个线程同时发 Modbus 请求（帧交叉 / 竞态异常）。
            timerRefresh.Enabled = false;
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
                timerRefresh.Enabled = true;   // 恢复自动刷新
                btnStop.Enabled = true;
            }
        }

        // ==================== 防呆辅助方法 ====================

        /// <summary>
        /// 从界面的 IP/端口输入框读取并校验参数，创建通信客户端。
        /// 校验失败（格式不对）时返回 null 并记日志。
        ///
        /// 【自动识别 IP】候选 IP 列表 = 界面填写的 IP + 配置文件里配的候选 IP
        ///（App.config 的 FanIpCandidates，配多少个就能识别多少个）。
        /// 连接时按顺序逐个尝试，第一个连上的就是设备真实地址，现场不用每次改 IP。
        /// 构造函数内部会自动去重并过滤非法 IP，所以重复（如界面填的就是列表里的 IP）不会有问题。
        /// </summary>
        private FanControllerClient CreateClientFromTextBoxes()
        {
            if (!TryGetEndpoint(out string ip, out int port, out string error))
            {
                AppendLog($"连接参数错误：{error}");
                return null;
            }

            // 候选 IP = 界面填的 IP（最优先） + 配置文件里配的候选 IP 列表
            var candidates = new List<string> { ip };
            candidates.AddRange(ReadConfigCandidateIps());
            return new FanControllerClient(candidates, port, slaveId: 1, timeoutMs: 3000, logAction: AppendLog);
        }

        /// <summary>
        /// 从 Demo 的 App.config 读取送风机候选 IP 列表（键：FanIpCandidates）。
        ///
        /// 【动态可配置】在配置文件里配了几个控制器 IP，就能自动识别几个（数量不限），
        /// 新增/减少现场控制器只需改配置，不用改代码。
        /// 配置项缺失或全部非法时，退回内置默认候选 IP（<see cref="FanControllerClient.DefaultCandidateIps"/>）。
        /// </summary>
        /// <returns>候选 IP 列表（已过滤非法项；顺序保持配置顺序）</returns>
        private static List<string> ReadConfigCandidateIps()
        {
            var list = new List<string>();
            try
            {
                string raw = System.Configuration.ConfigurationManager.AppSettings["FanIpCandidates"];
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    // 支持逗号 / 分号 / 中英文标点分隔
                    string[] parts = raw.Split(new[] { ',', ';', '，', '；' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string item in parts)
                    {
                        string ip = item.Trim();
                        if (System.Net.IPAddress.TryParse(ip, out _)) list.Add(ip);   // 只收合法 IP
                    }
                }
            }
            catch
            {
                // 读取配置异常时忽略，退回内置默认候选 IP
            }

            // 配置缺失/为空/全非法时，退回内置默认候选 IP（192.168.1.220/.221/.222）
            if (list.Count == 0) list.AddRange(FanControllerClient.DefaultCandidateIps);
            return list;
        }

        /// <summary>
        /// 校验并解析界面输入的 IP / 端口。
        /// 【防呆】IP 必须是合法 IPv4 地址，端口必须是 1~65535 的数字，
        /// 填错立刻给出明确错误原因，而不是等到连接时才抛一堆底层异常。
        /// </summary>
        /// <param name="ip">解析出的 IP</param>
        /// <param name="port">解析出的端口</param>
        /// <param name="error">校验失败时的原因说明</param>
        /// <returns>成功返回 true；失败返回 false</returns>
        private bool TryGetEndpoint(out string ip, out int port, out string error)
        {
            ip = txtIp.Text.Trim();
            port = 0;
            error = null;

            // 校验 IP：必须是点分十进制格式（如 192.168.1.220）
            if (!System.Net.IPAddress.TryParse(ip, out _))
            {
                error = $"IP 地址格式不正确（当前值 '{ip}'），应类似 192.168.1.220";
                return false;
            }

            // 校验端口：1~65535 的整数
            if (!int.TryParse(txtPort.Text.Trim(), out port) || port < 1 || port > 65535)
            {
                error = $"端口格式不正确（当前值 '{txtPort.Text}'），应为 1~65535 的数字";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 检测界面上填写的 IP/端口 是否和当前已应用（_client）的地址不一致。
        /// 【防呆】用户改了输入框但没点【连接测试】，启动/停止仍会走旧地址，
        /// 容易造成"我明明改了 IP 怎么还连不上"的困惑——这里统一拦截并给提示。
        /// </summary>
        private bool IsAddressChanged()
        {
            if (_client == null) return true; // 客户端还没建出来，视为"待应用"
            return !string.Equals(_client.Ip, txtIp.Text.Trim(), StringComparison.OrdinalIgnoreCase)
                || _client.Port != TryParsePortOrDefault(txtPort.Text);
        }

        /// <summary>
        /// 尝试把端口输入框解析成 int；解析失败返回 -1（-1 永远不等于真实端口，
        /// 从而让 IsAddressChanged 判定为"地址已修改"，触发防呆提示）。
        /// </summary>
        private static int TryParsePortOrDefault(string text)
        {
            return int.TryParse(text.Trim(), out int port) ? port : -1;
        }

        /// <summary>
        /// 执行按钮操作（启动/停止）前的通用防呆检查：
        ///   1) 客户端未创建 → 提示先点【连接测试】；
        ///   2) 界面上的 IP/端口 和当前已应用地址不一致 → 提示先点【连接测试】应用。
        /// 返回 true 表示可以继续操作；false 表示已给出提示并中止本次操作。
        /// </summary>
        private bool EnsureClientReady()
        {
            if (_client == null)
            {
                AppendLog("客户端尚未创建，请先检查 IP/端口 并点击【连接测试】");
                return false;
            }
            if (IsAddressChanged())
            {
                AppendLog($"提示：IP/端口已修改（当前应用的是 {_client.Ip}:{_client.Port}），请先点击【连接测试】应用后再操作");
                return false;
            }
            return true;
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
