using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using AgingTestSystem.Models;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 扫码枪服务（真实扫码枪接入）
    ///
    /// 【功能说明】
    /// 参考 SerialScannerTest Demo（Honeywell Xenon 1902 扫码枪串口测试）实现真实扫码枪逻辑：
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

        // ===================== 系统设备消息（USB 插拔检测，V1.16.4） =====================
        /// <summary>WM_DEVICECHANGE：系统设备状态变化消息（USB 插拔等），Windows 会广播给所有顶层窗口</summary>
        private const int WmDeviceChange = 0x0219;
        /// <summary>DBT_DEVNODES_CHANGED：设备树发生变化（任何 PnP 设备插拔/启停都会触发，最泛也最可靠）</summary>
        private const int DbtDevnodesChanged = 0x0007;
        /// <summary>DBT_DEVICEARRIVAL：设备已插入</summary>
        private const int DbtDeviceArrival = 0x8000;
        /// <summary>DBT_DEVICEREMOVEPENDING：设备移除挂起——拔掉"正被占用"的串口时，很多驱动只发这个、不发 REMOVECOMPLETE</summary>
        private const int DbtDeviceRemovePending = 0x8003;
        /// <summary>DBT_DEVICEREMOVECOMPLETE：设备已移除完成（物理拔出后的权威通知）</summary>
        private const int DbtDeviceRemoveComplete = 0x8004;

        /// <summary>
        /// 周期探测频率：每 N 次心跳（3 秒/次）做一次"关闭-重搜-重开"（约 N×3 秒）。
        /// 【V1.16.6 断连检测的兜底保证】WMI/注册表在应用还握着打开句柄时会被驱动残留
        /// 骗过（鬼设备）；只有关掉句柄再搜才绝对可靠。正在收数据时自动延后探测。
        /// </summary>
        private const int CloseRescanEveryTicks = 4;   // 约 12 秒

        /// <summary>距上次"关闭-重搜-重开"探测过了几次心跳</summary>
        private int _ticksSinceCloseRescan;

        /// <summary>
        /// 隐藏消息窗口（接收 WM_DEVICECHANGE 系统广播）
        /// 【V1.16.4 新增】扫码枪被拔出时 Windows 会广播"设备移除完成"消息，
        /// 收到后立即断开并重新识别，不依赖 WMI/注册表是否残留（见 HandleDeviceChangeMessage）。
        /// 在 UI 线程创建（ScannerService 在 UI 线程构造），消息处理也就在 UI 线程，线程安全。
        /// </summary>
        private DeviceChangeWindow _deviceChangeWindow;

        /// <summary>
        /// 上一次连接状态（用于"连上/断开"边沿检测，【V1.16.2 新增】）
        /// 心跳机制下，只在上一次是"已连接"、本次变成"未连接"（或反向）时提示一次，
        /// 避免失败过程每 3 秒刷一条日志。
        /// </summary>
        private bool _wasConnected;

        /// <summary>
        /// 本次"未连接"是否已提示过（【V1.16.2 新增】）
        /// true = 本次掉线已经提示过一次，后续后台静默重试不再刷日志；
        /// 连接成功后复位为 false，下次掉线允许再提示一次。
        /// </summary>
        private bool _disconnectReported;

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

            // 【V1.16.4】创建设备消息窗口：USB 插拔时 Windows 会广播 WM_DEVICECHANGE，
            // 收到"设备移除完成"立即断开重连、"设备插入"立即试连，不用等心跳周期。
            if (_deviceChangeWindow == null)
            {
                try
                {
                    _deviceChangeWindow = new DeviceChangeWindow(HandleDeviceChangeMessage);
                }
                catch (Exception ex)
                {
                    // 窗口创建失败不致命：心跳轮询（每 3 秒）仍是兜底方案
                    DebugLog($"设备消息窗口创建失败: {ex.Message}");
                    _deviceChangeWindow = null;
                }
            }

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
        /// 处理系统设备变化通知（【V1.16.4 新增】【V1.16.6 扩展覆盖更多消息类型】）
        /// 由 <see cref="DeviceChangeWindow"/> 在 UI 线程调用，参数是 WM_DEVICECHANGE 的 wParam。
        ///
        /// 【为什么这能解决"拔掉识别不到"】
        /// 心跳轮询的 WMI / 注册表判定，在应用还握着串口打开句柄时会被驱动残留误导
        /// （PnP 节点、SERIALCOMM 条目不随物理拔出立即消失）。
        /// 而 WM_DEVICECHANGE 的 DBT_DEVICEREMOVEPENDING / REMOVECOMPLETE 是 Windows 在设备
        /// 移除（挂起/完成）时向所有顶层窗口广播的通知，与驱动残留无关。
        ///
        /// 【V1.16.6 为什么原来没检测到（只处理 REMOVECOMPLETE 不够）】
        /// 拔掉"正被应用占用"的 USB 串口时，很多驱动【只发 DBT_DEVICEREMOVEPENDING，
        /// 不发 DBT_DEVICEREMOVECOMPLETE】（移除一直处于挂起，直到句柄关闭才完成）。
        /// 所以这里把 PENDING 也当断连处理；DEVNODES_CHANGED（任何设备树变化都触发）
        /// 作为最泛的兜底，收到就刷新一次连接状态。
        ///
        /// 【处理方式】
        /// - 移除挂起/完成：先 Disconnect 关闭句柄（不碰 COM，可立即执行；关闭后残留
        ///   的注册表/PnP 节点才真正清除），再 TryConnect 重新按关键词搜索：
        ///   扫码枪真没了 → 搜不到 → 保持未连接；是别的设备被拔 → 扫码枪还在 → 自动重连成功。
        ///   状态/日志由 TryConnect 的边沿逻辑负责，不会刷屏。
        /// - 设备插入：扫码枪没连上时立即试连一次，不用等心跳周期。
        /// - 设备树变化：延迟做一次"检查+重连"，由搜索决定扫码枪在不在。
        ///
        /// 【V1.16.5 关键修复：不能在消息里直接发 WMI 查询】
        /// WM_DEVICECHANGE 处理期间系统正处于"输入同步呼叫"中，此时发起任何传出
        /// COM 调用（WMI 的 ManagementObjectSearcher）会抛
        /// RPC_E_CANTCALLOUT_ININPUTSYNCCALL（托管调试助手 DisconnectedContext）。
        /// 实机运行这个异常会被 FindMatchingPorts 的 catch 吃掉并返回 null，
        /// 表现为"插入扫码枪后连不上"。所以这里只做不碰 COM 的 Disconnect，
        /// 重连（内部要发 WMI 查询）一律 Post 到消息队列末尾，等本消息处理完、
        /// 脱离该上下文后再执行（仍留在 UI 线程）。
        /// </summary>
        /// <param name="wParam">WM_DEVICECHANGE 的 wParam（事件类型）</param>
        private void HandleDeviceChangeMessage(int wParam)
        {
            if (_disposed) return;

            if (wParam == DbtDeviceArrival)
            {
                if (!_isConnected)
                {
                    DebugLog($"设备插入通知(0x{wParam:X4})：立即尝试连接...");
                    PostDeferred(TryConnect);     // 重连要发 WMI 查询，推迟到消息处理完成后执行
                }
            }
            else if (wParam == DbtDeviceRemovePending || wParam == DbtDeviceRemoveComplete)
            {
                // 移除挂起/完成都当断连：只关句柄（不碰 COM，可在此直接执行），
                // 重连（内部要发 WMI 查询）推迟出本消息，见类注释 V1.16.5。
                DebugLog($"设备移除通知(0x{wParam:X4})：立即断开并重新识别...");
                if (_isConnected) Disconnect();
                PostDeferred(TryConnect);
            }
            else if (wParam == DbtDevnodesChanged)
            {
                // 设备树变化：不确定是不是扫码枪，延迟做一次"检查+重连"，
                // 由搜索决定：扫码枪还在 → 维持/恢复连接；没了 → 断连。
                DebugLog($"设备树变化通知(0x{wParam:X4})：刷新连接状态...");
                PostDeferred(() =>
                {
                    if (_isConnected) CheckConnectionAlive();
                    else TryConnect();
                });
            }
        }

        /// <summary>
        /// 把动作推迟到当前消息（WM_DEVICECHANGE）处理完之后执行
        /// 【V1.16.5 新增】见 <see cref="HandleDeviceChangeMessage"/>：
        /// 设备消息处理期间的 COM 上下文不允许传出呼叫，必须等消息返回、
        /// 回到正常消息泵后再发 WMI 查询。有 UI 同步上下文就用它 Post
        /// （下一轮消息泵执行，仍在本线程）；没有则直接执行。
        /// </summary>
        /// <param name="action">要推迟执行的动作</param>
        private void PostDeferred(Action action)
        {
            if (_disposed) return;
            if (_syncContext != null)
                _syncContext.Post(_ => action(), null);
            else
                action();
        }

        /// <summary>
        /// 尝试连接扫码枪
        /// 内部方法：由 Start、重连定时器或 <see cref="TryReconnectNow"/> 调用
        ///
        /// 【V1.16.2 静默后台重连（心跳机制）】
        /// 不再"重试几次就放弃"，而是让重连定时器一直开着、每 3 秒在后台静默重试。
        /// 日志只记"边沿"：连上 / 断开 各提示一次；连续失败的中间过程不刷日志
        /// （解决"一直重试很吵"的痛点，又保留"设备插上后自动恢复"的能力）。
        /// 需要扫码时上层调用 <see cref="TryReconnectNow"/> 立即重连一次。
        /// </summary>
        private void TryConnect()
        {
            // 已经连着就不重复连接
            if (_isConnected) return;

            string failReason = null;
            bool ok = false;
            try
            {
                // 1) 确定要用的串口：
                //    配置里写了固定端口（ScannerPort）就优先用固定端口，
                //    否则通过 WMI 按设备关键词自动识别（如 "Xenon 1902"）
                string port = !string.IsNullOrWhiteSpace(_config.ScannerPort)
                    ? _config.ScannerPort.Trim()
                    : FindScannerPort();

                // 没找到端口：记下原因（提示只发一次，见下方边沿逻辑）
                if (string.IsNullOrEmpty(port))
                {
                    failReason = "未找到扫码枪串口（请确认设备已连接并处于虚拟串口模式）";
                }
                else
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
                    ok = true;
                }
            }
            catch (Exception ex)
            {
                // 打开失败（端口被占用 / 拔掉了 / 驱动异常等）：
                // 不抛出异常（避免启动阶段把主程序崩掉），记下原因交给下方边沿逻辑
                failReason = $"连接失败: {ex.Message}";
                Disconnect();
            }

            // ===== 边沿日志（V1.16.2 心跳机制：只在状态变化时提示，失败过程静默） =====
            if (ok)
            {
                // 连接成功：只有"之前未连接"才提示一次"已连接"
                if (!_wasConnected)
                {
                    _wasConnected = true;
                    PostStatus($"扫码枪已连接: {_currentPortName}，等待扫码...");
                }
                // 复位"已提示未连接"标记：下次掉线允许再提示一次
                _disconnectReported = false;

                // 【V1.16.3】心跳调试日志：确认新代码在跑，并记录实际解析到的端口
                DebugLog($"连接成功: 端口={_currentPortName}，识别关键词='{_config.ScannerDeviceKeyword}'");
            }
            else
            {
                // 连接失败：置为未连接状态
                _wasConnected = false;
                // 只在"本次掉线还没提示过"时提示一次，之后静默重试（不刷日志）
                if (!_disconnectReported)
                {
                    _disconnectReported = true;
                    PostStatus(failReason ?? "扫码枪未连接，正在后台自动重试...");
                }
            }
        }

        /// <summary>
        /// 断连边沿处理（【V1.16.2 新增】）
        /// 由串口错误 / 数据接收异常调用：只在"原本已连接"变成"断开"时提示一次
        /// "哪个设备断了、什么原因"，并标记"已提示"，让后续静默重试不刷日志。
        /// </summary>
        /// <param name="reason">断连原因描述</param>
        private void OnDisconnectDetected(string reason)
        {
            if (_disposed) return;

            // 原本就没连接上：不算"掉线"（启动失败等），不提示，避免重复打扰
            if (!_wasConnected) return;

            _wasConnected = false;
            _disconnectReported = true;   // 断连原因已提示，后续失败静默
            PostStatus($"扫码枪已断开，正在后台自动重试...（{reason}）");
        }

        /// <summary>
        /// 按需重连（【V1.16.1 新增】【V1.16.2 简化】）
        /// 用户需要扫码时（如打开"录入批号 / ID绑定"窗口）调用本方法立即重连一次。
        /// 心跳机制下后台本来就会静默重试，这里只是确保重连定时器在跑并立刻试一次。
        /// </summary>
        /// <returns>本次重连后是否已连接</returns>
        public bool TryReconnectNow()
        {
            if (_disposed) return false;

            // 确保重连定时器在跑（正常情况下一直在跑，这里是兜底）
            if (!_reconnectTimer.Enabled) _reconnectTimer.Start();

            TryConnect();
            return _isConnected;
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
        /// 从设备名称里提取 COM 口的正则（【V1.16.4 修正】）
        /// 例："Honeywell Xenon 1902 (COM10)" → "COM10"。
        /// 用 (?!\d) 防止 "COM10" 里的 "COM1" 被当成独立端口误匹配
        /// （老的字符串 Contains 写法在设备名含 COM10 时会同时误收 COM1）。
        /// </summary>
        private static readonly Regex ComPortInNameRegex =
            new Regex(@"(COM\d{1,4})(?!\d)", RegexOptions.IgnoreCase);

        /// <summary>
        /// 通过 WMI 查询设备描述，自动定位包含关键词的 COM 端口
        ///
        /// 【原理】（与 SerialScannerTest Demo 一致）
        /// 用 System.Management 查询 Win32_PnPEntity，筛选名称同时包含 "COM" 和
        /// 设备关键词（默认 "Xenon 1902"）的设备，再从名称里提取端口号。
        /// 连接建立用这里的结果；心跳断连判定用 <see cref="FindMatchingPorts"/>
        /// 重新跑一遍同一套搜索，确认设备还插着。
        /// </summary>
        /// <returns>端口名称（如 "COM10"），未找到或查询失败返回 null</returns>
        private string FindScannerPort()
        {
            List<string> matches = FindMatchingPorts();
            return (matches == null || matches.Count == 0) ? null : matches[0];
        }

        /// <summary>
        /// 按设备关键词搜索匹配的串口名称列表（WMI 动态搜索）
        /// 连接建立与心跳断连判定共用这一套"动态搜索串口名"的逻辑。
        ///
        /// 【返回值约定】（心跳判定依赖这个区分）
        /// - 返回 null：WMI 查询失败（权限不足/服务临时异常）——无法判定设备状态
        /// - 返回空列表：查询成功，但没有任何串口匹配设备关键词——设备已不在（被拔掉）
        /// - 返回非空列表：查询成功，找到匹配的串口（第一个即连接建立要用的端口）
        /// </summary>
        private List<string> FindMatchingPorts()
        {
            var matches = new List<string>();
            try
            {
                // 获取当前系统所有串口名称（如 COM1、COM10 ...）
                string[] portNames = SerialPort.GetPortNames();
                if (portNames == null || portNames.Length == 0)
                    return matches;

                // 系统串口列表转成集合，用于比对（忽略大小写）
                var portSet = new HashSet<string>(portNames, StringComparer.OrdinalIgnoreCase);

                // WMI 查询 PnP 设备，过滤名称中含 "COM" 和关键词的设备
                using (var searcher = new ManagementObjectSearcher(
                    $"SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%COM%' AND Name LIKE '%{_config.ScannerDeviceKeyword}%'"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string name = obj["Name"]?.ToString();
                        if (string.IsNullOrEmpty(name)) continue;

                        // 从设备名称里提取 COM 口（如 "Honeywell Xenon 1902 (COM10)" → "COM10"），
                        // 再和系统串口列表比对，匹配到就收集（去重）。
                        // 用正则提取而非字符串 Contains：避免 "COM10" 被误匹配出 "COM1"。
                        Match m = ComPortInNameRegex.Match(name);
                        while (m.Success)
                        {
                            string port = m.Value;
                            if (portSet.Contains(port) && !matches.Contains(port))
                                matches.Add(port);
                            m = m.NextMatch();
                        }
                    }
                }
                return matches;
            }
            catch (Exception)
            {
                // WMI 查询失败（可能权限不足）：返回 null，由调用方决定怎么处理
                // （连接时=找不到端口；心跳时=不误判断连）
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
                // 按"断连边沿"提示一次并断开连接（_isConnected=false），
                // 否则重连定时器会以为还连着而不重试，导致扫码枪永远恢复不了
                OnDisconnectDetected($"读取数据异常: {ex.Message}");
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
        /// 按"断连边沿"提示一次，再断开连接交给重连定时器静默恢复
        /// （设备移除后如果不断开，_isConnected 保持 true，定时器就不会重连）
        /// </summary>
        private void SerialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            OnDisconnectDetected($"串口错误: {e.EventType}");
            Disconnect();
        }

        /// <summary>
        /// 重连定时器 Tick（UI 线程触发）
        /// 【V1.16.3 心跳】先检查当前连接是否还"活着"
        /// （动态识别模式：按设备关键词重新搜索串口；固定端口模式：查系统串口列表），
        /// 如果掉线了再自动尝试重连。
        /// </summary>
        private void ReconnectTimer_Tick(object sender, EventArgs e)
        {
            // 1) 心跳检查：已连接时验证串口还在不在（USB 被拔出时状态能及时变"未连接"）
            CheckConnectionAlive();

            // 2) 未连接时自动尝试重连
            TryConnect();
        }

        /// <summary>
        /// 心跳检查：当前连接是否还存活（【V1.16.3 重写】）
        ///
        /// 【为什么原来的断连检测失效（V1.16.2 的双重判定不可靠）】
        /// 单纯拔掉 USB 虚拟串口时，SerialPort 的 ErrorReceived / DataReceived
        /// 事件在 Windows 上【不一定触发】。更关键的是下面两招在扫码枪上都不灵：
        /// ① GetPortNames() 查的是注册表 SERIALCOMM——应用还【握着打开句柄】时，
        ///    拔掉 USB 后该 COM 条目常常仍留在系统列表里，所以"端口名还在"≠"设备还在"。
        /// ② ReadExisting() 探测——期望对失效句柄抛 IOException，但多数 USB 转串口
        ///    驱动（CH340 / PL2303 / CP210x）在设备被拔后只是静默返回空串，不抛异常。
        /// 结果：_isConnected 一直保持 true，状态栏永远"已连接"，
        /// 重连定时器里 TryConnect() 第一行 if(_isConnected) return 短路，
        /// 扫码枪拔掉后再插回去也永远恢复不了。
        ///
        /// 【V1.16.3 核心思路：和连接建立一样"动态搜索串口名"再确认一遍】
        /// 连接建立本来就是按设备关键词（默认 "Xenon 1902"）用 WMI 搜出来的；
        /// 心跳也重新跑一遍同样的搜索：设备被拔掉后，PnP 设备节点会从系统里消失，
        /// WMI 就再也搜不到该串口 → 判定断连。WMI 反映的是【物理设备是否真在】，
        /// 不受"注册表残留条目 / 打开句柄"影响，比上面两招可靠。
        /// - 动态识别模式（ScannerPort 为空）：以 WMI 重新搜索为主判定；
        ///   搜索失败（权限/临时故障）时不误判断连，交给 I/O 探测兜底。
        /// - 固定串口模式（ScannerPort 配了固定值）：没有关键词可搜，
        ///   回落"端口名是否还在系统列表 + I/O 探测"。
        /// 检测到断连后按"边沿"提示一次，交给重连定时器静默恢复。
        /// </summary>
        private void CheckConnectionAlive()
        {
            if (_disposed) return;

            // 未连接时不需要心跳（TryConnect 自己会处理重连）
            if (!_isConnected) return;

            // 情况 1：串口对象被置空 / 已被关闭 → 视为断连
            if (_serialPort == null || !_serialPort.IsOpen)
            {
                OnDisconnectDetected("串口已关闭");
                Disconnect();
                return;
            }

            // 情况 2：动态识别模式——重新跑一遍连接建立时的 WMI 设备关键词搜索，
            // 确认扫码枪物理设备还在。设备被拔掉 → PnP 节点消失 → 搜不到 → 断连。
            // 同时用"当前端口是否还在系统串口列表"作第二路独立信号（防止其中一路
            // 因驱动残留/查询失败而漏判）；两路任一路确定端口不在 → 判定断连。
            if (string.IsNullOrWhiteSpace(_config.ScannerPort))
            {
                // ① WMI 设备关键词搜索：反映物理设备是否真在（null=查询失败 / 空=不在 / 非空=在）
                List<string> wmiMatches = FindMatchingPorts();
                bool wmiSaysPresent = wmiMatches != null &&
                    wmiMatches.Exists(p => string.Equals(p, _currentPortName, StringComparison.OrdinalIgnoreCase));

                // ② 系统串口列表（GetPortNames，读注册表 SERIALCOMM）：当前端口是否还在
                string[] portNames = null;
                bool inPortList = false;
                try
                {
                    portNames = SerialPort.GetPortNames();
                    if (portNames != null)
                    {
                        foreach (string p in portNames)
                        {
                            if (string.Equals(p, _currentPortName, StringComparison.OrdinalIgnoreCase))
                            {
                                inPortList = true;
                                break;
                            }
                        }
                    }
                }
                catch { /* GetPortNames 失败：跳过这一路判定 */ }

                // 诊断日志（ScannerDebugLog=true 时打到 LOG，供现场排查"断连识别不到"）
                DebugLog($"心跳: 当前={_currentPortName}, WMI匹配=[{JoinPorts(wmiMatches)}], " +
                         $"系统列表=[{JoinPorts(portNames)}], WMI在={wmiSaysPresent}, 列表在={inPortList}");

                // 两路独立判定：任一路"查询成功但端口已不在" → 判定断连
                // （查询失败的那一路自动跳过，避免 WMI/注册表偶发故障误报）
                if ((wmiMatches != null && !wmiSaysPresent) ||
                    (portNames != null && !inPortList))
                {
                    OnDisconnectDetected("扫码枪已被拔掉（动态搜索不到该串口）");
                    Disconnect();
                    return;
                }
            }
            else
            {
                // 情况 3：固定串口模式——没有设备关键词可搜，
                // 回落"当前端口名是否还在系统串口列表"判断
                try
                {
                    bool portExists = false;
                    foreach (string p in SerialPort.GetPortNames())
                    {
                        if (string.Equals(p, _currentPortName, StringComparison.OrdinalIgnoreCase))
                        {
                            portExists = true;
                            break;
                        }
                    }
                    if (!portExists)
                    {
                        OnDisconnectDetected("USB 串口已被移除");
                        Disconnect();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    OnDisconnectDetected($"端口探测失败: {ex.Message}");
                    Disconnect();
                    return;
                }
            }

            // ===== 【V1.16.6】周期"关闭-重搜-重开"探测（断连检测的兜底保证） =====
            // 上面的轻量检查在应用还握着打开句柄时会被驱动残留骗过（鬼设备）：
            // WMI / 系统串口列表都还显示端口在，实际上扫码枪已经被拔。
            // 唯一绝对可靠的验证是关掉句柄再搜——句柄一关，残留立刻释放，
            // WMI 反映的就是真实物理状态。每 CloseRescanEveryTicks 次心跳做一次。
            // 正在收数据（BytesToRead>0）时自动延后，避免把条码读丢。
            TryPeriodicCloseRescan();
            // 探测若判定断连已 Disconnect 并重连处理，直接结束本轮心跳
            if (!_isConnected) return;

            // I/O 探测兜底：上面判不出断连（固定端口 / WMI 查询失败）时，主动读一次。
            // 端口有数据在等时跳过探测（避免和 DataReceived 抢数据、把条码读丢）；
            // 安静时 ReadExisting() 立即返回空串（不会阻塞等待数据），
            // 句柄已失效（设备被拔/驱动异常）会抛异常 → 进 catch 判定断连。
            try
            {
                if (_serialPort.BytesToRead <= 0)
                {
                    _serialPort.ReadExisting();
                }
            }
            catch (Exception ex)
            {
                // I/O 探测抛异常 = 串口句柄已失效（设备被拔/驱动异常）→ 判定断连
                OnDisconnectDetected($"端口探测失败: {ex.Message}");
                Disconnect();
            }
        }

        /// <summary>
        /// 周期"关闭-重搜-重开"探测（【V1.16.6 新增】断连检测的兜底保证）
        ///
        /// 【为什么需要它】
        /// WMI 重新搜索（心跳①）和系统串口列表（心跳②）都建立在"拔掉后残留会清除"
        /// 的假设上。但应用【还握着打开句柄】时，部分 USB 转串口驱动会让 PnP 节点和
        /// SERIALCOMM 条目一直残留（鬼设备），两路信号都误判"设备还在"，
        /// 心跳就永远判不出断连。而【关掉句柄】是唯一能把残留真正释放掉的操作——
        /// 句柄一关，再搜 WMI 就是绝对真实的物理状态。
        ///
        /// 【做法】每 CloseRescanEveryTicks 次心跳（约 12 秒）：
        /// - Disconnect() 关闭句柄（释放残留）
        /// - TryConnect() 重新按关键词搜索：
        ///   设备真没了 → 搜不到 → 保持未连接（状态变红）；
        ///   设备还在（或刚才拔的是别的设备）→ 重开新句柄 → 用户无感。
        /// 正在收数据（BytesToRead>0）时跳过并清零计数，避免把条码读丢。
        /// </summary>
        private void TryPeriodicCloseRescan()
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;

            // 正在收数据：延后探测（避免和 DataReceived 抢数据、把条码读丢）
            if (_serialPort.BytesToRead > 0)
            {
                _ticksSinceCloseRescan = 0;
                return;
            }

            _ticksSinceCloseRescan++;
            if (_ticksSinceCloseRescan < CloseRescanEveryTicks) return;
            _ticksSinceCloseRescan = 0;

            DebugLog("心跳：周期探测-关闭串口重新识别（确认设备真实存在）");
            Disconnect();   // 关闭句柄 → 释放鬼设备残留
            TryConnect();   // 重搜：设备在 → 重开新句柄；设备没了 → 保持未连接
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
        /// 心跳调试日志（【V1.16.3 新增】）
        /// ScannerDebugLog=true 时把端口搜索结果通过状态事件打到 LOG，
        /// 用于现场排查"扫码枪断连识别不到"：能直接看到心跳每个周期
        /// WMI 搜到什么、系统串口列表是什么、判定结果如何。
        /// </summary>
        /// <param name="message">调试内容</param>
        private void DebugLog(string message)
        {
            if (_disposed || !_config.ScannerDebugLog) return;
            PostStatus($"[心跳调试] {message}");
        }

        /// <summary>
        /// 把端口列表拼成可读字符串（心跳调试日志用）
        /// null / 空列表都显示 "-"，便于一眼看出"啥都没有"
        /// </summary>
        private static string JoinPorts(IEnumerable<string> ports)
        {
            if (ports == null) return "-";
            bool any = false;
            var sb = new StringBuilder();
            foreach (string p in ports)
            {
                if (any) sb.Append(',');
                sb.Append(p);
                any = true;
            }
            return any ? sb.ToString() : "-";
        }

        /// <summary>
        /// 隐藏消息窗口（【V1.16.4 新增】）
        /// 一个不显示的顶层窗口，用来接收 Windows 广播的 WM_DEVICECHANGE 设备消息。
        /// - 在 UI 线程创建（ScannerService 在 UI 线程构造 → Start 也在 UI 线程），
        ///   所以 WndProc 也在 UI 线程执行，可直接调用 Disconnect/TryConnect，无跨线程问题。
        /// - 用 WS_POPUP 顶层窗口而非 message-only 窗口：消息窗口收不到设备广播，
        ///   顶层窗口（无父窗口）才会收到 BroadcastSystemMessage 的设备插拔消息。
        /// - 从不需要 Show：没有 WS_VISIBLE，用户永远看不到它。
        /// </summary>
        private class DeviceChangeWindow : NativeWindow
        {
            private readonly Action<int> _onDeviceChange;

            public DeviceChangeWindow(Action<int> onDeviceChange)
            {
                _onDeviceChange = onDeviceChange;
                var cp = new CreateParams();
                cp.Style = unchecked((int)0x80000000);   // WS_POPUP：顶层窗口（无父），确保能收到系统设备广播
                CreateHandle(cp);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WmDeviceChange)
                {
                    try
                    {
                        _onDeviceChange((int)m.WParam.ToInt64());
                    }
                    catch { /* 消息处理异常不影响窗口本身 */ }
                }
                base.WndProc(ref m);
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

            // 【V1.16.4】销毁隐藏消息窗口（和创建同线程，UI 线程）
            if (_deviceChangeWindow != null)
            {
                try { _deviceChangeWindow.DestroyHandle(); }
                catch { /* 已销毁/异常忽略 */ }
                _deviceChangeWindow = null;
            }

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
