using System;
using System.IO.Ports;
using System.Management;
using System.Windows.Forms;

namespace SerialScannerTest
{
    public partial class Form1 : Form
    {
        // 串口对象
        private SerialPort _serialPort;
        // 用于匹配设备的描述关键字（可在设备管理器中看到）
        private readonly string _targetDeviceKeyword = "Xenon 1902";

        public Form1()
        {
            InitializeComponent();
        }

        // ------------------- 核心逻辑 -------------------

        /// <summary>
        /// 窗体加载时自动查找并连接扫码枪
        /// </summary>
        private void Form1_Load(object sender, EventArgs e)
        {
            AutoConnect();
        }

        /// <summary>
        /// 自动查找并打开串口
        /// </summary>
        private void AutoConnect()
        {
            string port = FindScannerPort();
            if (string.IsNullOrEmpty(port))
            {
                UpdateStatus("未找到扫码枪串口，请检查设备是否已连接并处于虚拟串口模式。");
                return;
            }

            try
            {
                // 配置串口参数（与调试助手一致）
                _serialPort = new SerialPort(port, 115200, Parity.None, 8, StopBits.One);
                _serialPort.DataReceived += SerialPort_DataReceived;   // 注册数据接收事件
                _serialPort.Open();
                UpdateStatus($"已连接到 {port}，等待扫码...");
            }
            catch (Exception ex)
            {
                UpdateStatus($"打开串口失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 通过 WMI 查询设备描述，自动定位包含关键词的 COM 端口
        /// </summary>
        /// <returns>端口名称（如 "COM10"），若未找到则返回 null</returns>
        private string FindScannerPort()
        {
            try
            {
                // 获取当前所有串口名称
                string[] portNames = SerialPort.GetPortNames();
                if (portNames == null || portNames.Length == 0)
                    return null;

                // 使用 WMI 查询所有 PnP 设备，筛选名称中包含 "COM" 和 "Xenon 1902" 的设备
                using (var searcher = new ManagementObjectSearcher(
                    $"SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%COM%' AND Name LIKE '%{_targetDeviceKeyword}%'"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string name = obj["Name"]?.ToString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            // 从设备名称中提取 COM 端口号（例如 "COM10"）
                            foreach (string portName in portNames)
                            {
                                if (name.Contains(portName))
                                    return portName;
                            }
                        }
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                // WMI 查询失败（可能权限不足），可降级为手动输入或尝试所有端口
                // 此处简单处理，返回 null
                return null;
            }
        }

        /// <summary>
        /// 串口数据接收事件（在后台线程触发）
        /// </summary>
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                // 读取当前缓冲区中的所有数据（ASCII 格式）
                string data = _serialPort.ReadExisting();
                if (!string.IsNullOrEmpty(data))
                {
                    // 将数据添加到 UI 文本框（需要跨线程调用）
                    AppendText(data);
                }
            }
            catch (Exception ex)
            {
                AppendText($"读取数据错误: {ex.Message}\r\n");
            }
        }

        /// <summary>
        /// 线程安全地向文本框追加文本，并自动滚动到底部
        /// </summary>
        /// <param name="text">要追加的文本</param>
        private void AppendText(string text)
        {
            if (txtReceive.InvokeRequired)
            {
                txtReceive.Invoke(new Action<string>(AppendText), text);
            }
            else
            {
                txtReceive.AppendText(text);
                // 滚动到最新内容
                txtReceive.SelectionStart = txtReceive.Text.Length;
                txtReceive.ScrollToCaret();
            }
        }

        /// <summary>
        /// 线程安全地更新状态栏
        /// </summary>
        private void UpdateStatus(string message)
        {
            if (lblStatus.InvokeRequired)
            {
                lblStatus.Invoke(new Action<string>(UpdateStatus), message);
            }
            else
            {
                lblStatus.Text = message;
            }
        }

        // ------------------- 控件事件 -------------------

        private void BtnClear_Click(object sender, EventArgs e)
        {
            txtReceive.Clear();
        }

        // ------------------- 窗体关闭时释放资源 -------------------

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
                _serialPort.Dispose();
            }
        }
    }
}