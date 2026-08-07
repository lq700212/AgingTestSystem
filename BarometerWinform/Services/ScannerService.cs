using System;
using System.IO.Ports;
using System.Management;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using BarometerWinform.Models;

namespace BarometerWinform.Services
{
    /// <summary>
    /// 扫码枪服务（真实扫码枪接入）
    ///
    /// 【功能说明】
    /// 参考 SerialScannerTest Demo（Honeywell Xenon 1902 扫码枪串口测试）实现，
    /// 把之前"扫码模拟窗体（ScanSimulationForm）"里预留的真实扫码枪逻辑补上：
    /// - 自动识别扫码枪串口（WMI 查询设备名称包含关键词的 COM 口）
    /// - 打开串口并监听数据，把收到的串口数据按行解析成一条条码
    /// - 通过事件 OnBarcodeScanned 通知上层业务（写日志 / ID绑定SN自动填充等）
    /// - 断线/未插入时定时重连，现场不用每次手动重开
    ///
    /// 【通讯参数】（与 SerialScannerTest Demo 实测一致）
    /// - 波特率 115200、数据位 8、停止位 1、校验 None、ASCII 文本
    /// - 扫码枪输出格式：条码内容 + 回车/换行 结尾（一行一条码）
    ///
    /// 【线程安全说明】
    /// SerialPort.DataReceived 在后台线程触发，这里用 SynchronizationContext
    /// 把扫码事件/状态事件封送到创建本服务的线程（通常就是 UI 线程），
    /// 这样上层订阅者可以直接更新控件，不需要再手动 Invoke。
    /// </summary>
    public class ScannerService : IDisposable
    {
        /// <summary>
        /// 扫码完成事件
        /// 参数：扫到的条码内容（已去除首尾空白）
        /// 注意：事件已在 UI 线程触发，订阅者可直接操作控件
        /// </summary>
        public event EventHandler<string> OnBarcodeScanned;

        /// <summary>
        /// 状态变化事件（连接成功 / 断开 / 未找到端口 / 错误）
        /// 参数：中文状态描述文本，用于界面日志显示
        /// 注意：事件已在 UI 线程触发
        /// </summary>
        public event EventHandler<string> OnStatusChanged;

        /// <summary>
        /// 全局配置（来自 App.config）
        /// 包含扫码枪的启用开关、关键词、串口参数等
        /// </summary>
        private readonly DeviceConfig _config;

        /// <summary>
        /// 串口对象（扫码枪虚拟串口）
        /// </summary>
        private SerialPort _serialPort;

        /// <summary>
        /// 接收缓冲区
        /// 串口数据是一帧一帧到达的，一条条码可能被拆成多帧。
        /// 用一个 StringBuilder 把数据先拼起来，等到换行符出现才算一条完整条码。
        /// </summary>
        private readonly StringBuilder _buffer = new StringBuilder();

        /// <summary>
        /// 当前连接的串口名（如 "COM10"），未连接时为空
        /// </summary>
        private string _currentPortName;

        /// <summary>
        /// 是否已连接扫码枪
        /// </summary>
        private bool _isConnected;

        /// <summary>
        /// 重连定时器（UI 线程定时器）
        /// 启动后每 3 秒检查一次：如果已启用扫码枪但当前未连接，则自动尝试重连。
        /// 这样现场开机时扫码枪没插、或中途掉线，都能自动恢复。
        /// </summary>
        private readonly System.Windows.Forms.Timer _reconnectTimer;

        /// <summary>
        /// 创建本服务时的同步上下文（通常是 UI 线程的同步上下文）
        /// 用于把后台线程收到的扫码事件封送到 UI 线程
        /// </summary>
        private readonly SynchronizationContext _syncContext;

        /// <summary>
        /// 是否已释放资源（防止 Dispose 后事件/定时器还在触发）
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 是否已连接扫码枪（只读，供上层显示状态用）
        /// </summary>
        public bool IsConnected => _isConnected;

        /// <summary>
        /// 当前连接的串口名（只读，供上层显示状态用）
        /// </summary>
        public string CurrentPortName => _currentPortName;

        /// <summary>
        /// 构造函数
        /// 注意：请在 UI 线程创建本服务，这样扫码事件会自动封送到 UI 线程
        /// </summary>
        /// <param name="config">全局配置（DeviceConfig）</param>
        public ScannerService(DeviceConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            // 记录创建线程的同步上下文（WinForms 下就是 UI 线程的同步上下文）
            // SynchronizationContext.Current 在 UI 线程创建控件/服务时会被设置为
            // WindowsFormsSynchronizationContext，Post 方法会把委托放到 UI 消息队列执行
            _syncContext = SynchronizationContext.Current;

            // 创建重连定时器（System.Windows.Forms.Timer 在 UI 线程触发 Tick）
            _reconnectTimer = new System.Windows.Forms.Timer { Interval = ReconnectIntervalMs };
            _reconnectTimer.Tick += ReconnectTimer_Tick;
        }

        /// <summary>
        /// 重连定时器间隔（毫秒）
        /// 每 3 秒检查一次，扫码枪不在时自动重试
        /// </summary>
        private const int ReconnectIntervalMs = 3000;

        /// <summary>
        /// 启动扫码枪服务
        /// - 如果 App.config 里 ScannerEnabled=false，则不连接（现场没装扫码枪时用）
        /// - 否则启动重连定时器，并立即尝试连接
        /// </summary>
        public void Start()
        {
            ThrowIfDisposed();

            // 扫码枪是可选设备：配置里关掉就完全不连接，不影响整机启动
            if (!_config.ScannerEnabled)
            {
                PostStatus("扫码枪未启用（App.config ScannerEnabled=false），跳过连接");
                return;
            }

            // 启动定时器：持续监听，未连接时自动重试
            _reconnectTimer.Start();

            // 立即尝试第一次连接（成功则直接进入已连接状态）
            TryConnect();
        }

        /// <summary>
        /// 停止扫码枪服务（停止定时器 + 关闭串口）
        /// </summary>
        public void Stop()
        {
            if (_disposed) return;
            _reconnectTimer.Stop();
            Disconnect();
        }

        /// <summary>
        /// 尝试连接扫码枪
        /// 内部方法：由 Start 或重连定时器调用
        /// </summary>
        private void TryConnect()
        {
            // 已经连着就不重复连接
            if (_isConnected) return;

            // 1) 确定要用的串口：
            //    配置里写了固定端口（ScannerPort）就优先用固定端口，
            //    否则通过 WMI 按设备关键词自动识别（如 "Xenon 1902"）
            string port = !string.IsNullOrWhiteSpace(_config.ScannerPort)
                ? _config.ScannerPort.Trim()
                : FindScannerPort();

            // 没找到端口：通知状态，等待重连定时器下次再试
            if (string.IsNullOrEmpty(port))
            {
                PostStatus("未找到扫码枪串口，稍后自动重试（请确认设备已连接并处于虚拟串口模式）");
                return;
            }

            try
            {
                // 2) 创建串口对象并配置参数（与 SerialScannerTest Demo 一致）
                //    若之前连接过，先断开旧连接，避免重复 Open 报"端口被占用"
                Disconnect();

                _serialPort = new SerialPort(port,
                    _config.ScannerBaudRate,
                    ParseParity(_config.ScannerParity),
                    _config.ScannerDataBits,
                    ParseStopBits(_config.ScannerStopBits))
                {
                    // 给个读取超时，防止串口假死时 ReadExisting 一直挂着
                    ReadTimeout = 2000
                };

                // 3) 注册数据接收与错误事件
                _serialPort.DataReceived += SerialPort_DataReceived;
                _serialPort.ErrorReceived += SerialPort_ErrorReceived;

                // 4) 打开串口
                _serialPort.Open();

                _currentPortName = port;
                _isConnected = true;
                PostStatus($"扫码枪已连接: {port}，等待扫码...");
            }
            catch (Exception ex)
            {
                // 打开失败（端口被占用 / 拔掉了 / 驱动异常等）：
                // 不抛出异常（避免启动阶段把主程序崩掉），通知状态后交给定时器重连
                PostStatus($"扫码枪连接失败: {ex.Message}");
                Disconnect();
            }
        }

        /// <summary>
        /// 断开串口连接（关闭并释放串口对象，清除连接状态）
        /// </summary>
        private void Disconnect()
        {
            if (_serialPort != null)
            {
                try
                {
                    if (_serialPort.IsOpen) _serialPort.Close();
                }
                catch { /* 关闭失败不阻塞（可能已被系统移除） */ }
                _serialPort.DataReceived -= SerialPort_DataReceived;
                _serialPort.ErrorReceived -= SerialPort_ErrorReceived;
                _serialPort.Dispose();
                _serialPort = null;
            }
            _isConnected = false;
            _currentPortName = null;
            // 清空接收缓冲区，避免下一次连接时还残留上一段的数据
            _buffer.Clear();
        }

        /// <summary>
        /// 通过 WMI 查询设备描述，自动定位包含关键词的 COM 端口
        ///
        /// 【原理】（与 SerialScannerTest Demo 一致）
        /// 用 System.Management 查询 Win32_PnPEntity，筛选名称同时包含 "COM" 和
        /// 设备关键词（默认 "Xenon 1902"）的设备，再从名称里提取端口号。
        /// </summary>
        /// <returns>端口名称（如 "COM10"），未找到返回 null</returns>
        private string FindScannerPort()
        {
            try
            {
                // 获取当前系统所有串口名称（如 COM1、COM10 ...）
                string[] portNames = SerialPort.GetPortNames();
                if (portNames == null || portNames.Length == 0)
                    return null;

                // WMI 查询 PnP 设备，过滤名称中含 "COM" 和关键词的设备
                using (var searcher = new ManagementObjectSearcher(
                    $"SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%COM%' AND Name LIKE '%{_config.ScannerDeviceKeyword}%'"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string name = obj["Name"]?.ToString();
                        if (string.IsNullOrEmpty(name)) continue;

                        // 设备名称里一般包含端口号（如 "Honeywell Xenon 1902 (COM10)"），
                        // 和系统串口列表比对，匹配到就返回
                        foreach (string portName in portNames)
                        {
                            if (name.Contains(portName))
                                return portName;
                        }
                    }
                }
                return null;
            }
            catch (Exception)
            {
                // WMI 查询失败（可能权限不足）：返回 null，交给上层重试逻辑
                return null;
            }
        }

        /// <summary>
        /// 串口数据接收事件（在后台线程触发）
        /// 把收到的数据追加到缓冲区，按换行符切分成一条条完整的条码
        /// </summary>
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (_serialPort == null || !_serialPort.IsOpen) return;

                // 一次性读取当前缓冲区所有可用数据（ASCII 文本）
                string chunk = _serialPort.ReadExisting();
                if (string.IsNullOrEmpty(chunk)) return;

                _buffer.Append(chunk);

                // 从缓冲区中不断取出"以换行符结尾"的完整行，直到没有换行符为止
                while (true)
                {
                    // 查找换行符位置（\r 或 \n，兼容 CR / LF / CRLF 三种结尾）
                    int nlIndex = _buffer.ToString().IndexOfAny(LineBreakChars);
                    if (nlIndex < 0) break;   // 还没有完整的一行，剩下的留到缓冲区等下一帧

                    // 取出这一行的内容（一条条码），并去掉首尾空白
                    string line = _buffer.ToString(0, nlIndex).Trim();

                    // 跳过这一行结尾的换行符（可能同时有 \r\n 两个字符）
                    int removeCount = nlIndex + 1;
                    while (removeCount < _buffer.Length &&
                           (_buffer[removeCount] == '\r' || _buffer[removeCount] == '\n'))
                    {
                        removeCount++;
                    }
                    _buffer.Remove(0, removeCount);

                    // 非空行 → 触发扫码完成事件
                    if (!string.IsNullOrEmpty(line))
                    {
                        RaiseBarcodeScanned(line);
                    }
                }
            }
            catch (Exception ex)
            {
                // 读取异常（设备被拔掉等）：
                // 通知状态并断开连接（_isConnected=false），
                // 否则重连定时器会以为还连着而不重试，导致扫码枪永远恢复不了
                PostStatus($"读取扫码枪数据异常: {ex.Message}，准备重连...");
                Disconnect();
            }
        }

        /// <summary>
        /// 换行符字符集合（\r 和 \n）
        /// </summary>
        private static readonly char[] LineBreakChars = { '\r', '\n' };

        /// <summary>
        /// 串口错误接收事件（后台线程触发）
        /// 收到错误（如帧错误、设备移除）时：
        /// 通知状态并断开连接，交给重连定时器恢复
        /// （设备移除后如果不断开，_isConnected 保持 true，定时器就不会重连）
        /// </summary>
        private void SerialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            PostStatus($"扫码枪串口错误: {e.EventType}，准备重连...");
            Disconnect();
        }

        /// <summary>
        /// 重连定时器 Tick（UI 线程触发）
        /// 未连接时自动尝试重连
        /// </summary>
        private void ReconnectTimer_Tick(object sender, EventArgs e)
        {
            TryConnect();
        }

        /// <summary>
        /// 触发扫码完成事件（封送到 UI 线程）
        /// </summary>
        /// <param name="barcode">条码内容</param>
        private void RaiseBarcodeScanned(string barcode)
        {
            if (_disposed) return;

            if (_syncContext != null)
            {
                // 把事件封送到创建本服务的线程（UI 线程）
                _syncContext.Post(_ => OnBarcodeScanned?.Invoke(this, barcode), null);
            }
            else
            {
                // 没有同步上下文（非 UI 线程创建），直接触发
                OnBarcodeScanned?.Invoke(this, barcode);
            }
        }

        /// <summary>
        /// 触发状态事件（封送到 UI 线程）
        /// </summary>
        /// <param name="message">状态描述文本</param>
        private void PostStatus(string message)
        {
            if (_disposed) return;

            if (_syncContext != null)
            {
                _syncContext.Post(_ => OnStatusChanged?.Invoke(this, message), null);
            }
            else
            {
                OnStatusChanged?.Invoke(this, message);
            }
        }

        /// <summary>
        /// 把配置里的校验位字符串解析为 Parity 枚举
        /// 支持："None"、"Even"、"Odd"、"Mark"、"Space"（忽略大小写）
        /// </summary>
        private static Parity ParseParity(string parity)
        {
            if (string.IsNullOrWhiteSpace(parity)) return Parity.None;
            switch (parity.Trim().ToLowerInvariant())
            {
                case "even": return Parity.Even;
                case "odd": return Parity.Odd;
                case "mark": return Parity.Mark;
                case "space": return Parity.Space;
                default: return Parity.None;
            }
        }

        /// <summary>
        /// 把配置里的停止位数值解析为 StopBits 枚举
        /// 支持：1（默认）、2；若现场需要 1.5 可扩展（配置里写成 15 映射到 OnePointFive）
        /// </summary>
        private static StopBits ParseStopBits(int stopBits)
        {
            switch (stopBits)
            {
                case 2: return StopBits.Two;
                case 15: return StopBits.OnePointFive;   // 需要 1.5 停止位时配置写 15
                default: return StopBits.One;
            }
        }

        /// <summary>
        /// 释放资源（停止定时器 + 关闭串口）
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Stop();

            // 退订定时器事件，避免悬空引用
            _reconnectTimer.Tick -= ReconnectTimer_Tick;
            _reconnectTimer.Dispose();

            // 清除事件订阅者引用，防止外部持有本对象造成泄漏
            OnBarcodeScanned = null;
            OnStatusChanged = null;
        }

        /// <summary>
        /// 检查是否已释放，已释放时抛出 ObjectDisposedException
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ScannerService));
        }
    }
}
