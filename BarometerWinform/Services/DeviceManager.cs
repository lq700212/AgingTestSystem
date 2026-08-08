using System;
using System.Collections.Generic;
using System.Threading;
using System.Timers;
using BarometerWinform.Interfaces;
using BarometerWinform.Models;

namespace BarometerWinform.Services
{
    /// <summary>
    /// 设备管理器
    /// 负责管理所有气压表、IO 设备、冷却送风机的连接、数据采集和业务状态更新。
    /// 是整个系统的核心服务类。
    ///
    /// 【V1.10 新增业务串联】
    /// 在原有"采集 + 报警联动"基础上，把老化测试业务流程串起来：
    /// 1) 冷却送风机接入（接口化：真实 / Mock），独立定时器轮询，不阻塞 72 台气压表采集
    /// 2) 送风机生命周期全局化：送风机是 72 台共用的环境设备，
    ///    "首台开始测试时启动送风机，最后一台停止时才停止送风机"
    /// 3) 测试状态机：启动运行 = 开真空 + 载台上电 + 标测试中；
    ///    真空建立确认（开阀后 N 毫秒内压力必须进入正常区间，否则按真空失败报警）
    /// 4) 通讯故障报警：某台气压表连续读失败 N 次 → 视为失联报警（关阀+断电+标故障），
    ///    避免"断线后停留在旧压力值上假正常"
    /// 5) 老化计时：到达 MaxTestDurationSeconds 自动停止该台（关阀+断电+记日志）
    /// 6) 人工复位：报警/故障台复位回到空闲，可重新启动
    /// 7) 事件落盘：启动/停止/报警/复位/急停等写入 CSV 日志，供历史记录与追溯
    ///
    /// 【V1.16 门禁解耦 + 自动恢复】
    /// 1) 启动门禁解耦：只要"气压表串口"连通就启动采集；IO 耦合器 / 送风机是可选设备，
    ///    断开不再拖垮整机（原实现要求气压表 + 耦合器全部成功，耦合器连不上会把
    ///    气压表和送风机一起回滚 → 整个界面无数据）。
    /// 2) 气压表串口 CH340 自动识别：配置端口不存在时自动识别 CH340，现场免改配置。
    /// 3) IO 耦合器自动重连：每 5 秒后台尝试，重启耦合器后自动恢复阀/载台电控制。
    /// 4) 批量写阈值暂停采集：SetAllBarometerThresholds 期间停掉采集定时器，
    ///    避免与批量写争抢串口总线导致写超时。
    /// 5) 启动诊断：每一步连接结果经 OnDiagnostic 上报，UI 写 LOG，现场一眼看到
    ///    "哪一步连不上"（含实际使用的串口/耦合器/送风机 IP）。
    ///
    /// 【V1.16.2 心跳机制（静默自愈）】
    /// 现场需求：连接后做心跳，中途断连状态能及时更新、及时提醒"哪个设备断了"；
    /// 同时希望自动重连，但"不要一直重试连接"（怕刷日志/占资源）——两者看似矛盾。
    /// 本版本的统一解法（三个原则）：
    /// 1) 心跳 = 后台轮询（耦合器/气压表随 1s 采集、送风机 2s 轮询、扫码枪串口事件），
    ///    断连在 1~3 秒内被感知，状态标签即时更新；
    /// 2) 日志只记"边沿"：连上 / 断开 各提示一次（带设备名，明确"哪里断了"），
    ///    连续失败的中间过程【静默】，不再刷日志；
    /// 3) 后台【静默持续重连】：不再"重试几次就放弃"，而是按节流一直重试，
    ///    设备插上/恢复后自动连回，全程不打扰操作员；用户操作需要某设备时
    ///    按需重连一次，仍连不上才弹窗"xxx未连接，请先连接"（兜底）。
    /// 所有重连都跑在后台线程，互不阻塞、不影响 72 台采集主链路（性能安全）。
    ///
    /// 【线程安全说明】
    /// - System.Timers.Timer 的 Elapsed 在后台线程触发
    /// - _barometerDataCache / _fanDataCache 用 _cacheLock 保护
    /// - 测试状态数组（_testingStates/_lastAlarmStates/...）用 _stateLock 保护
    ///   （采集线程与 UI 线程的按钮操作会并发访问）
    /// - 送风机轮询独立定时器用 _fanPollLock 防重入
    /// </summary>
    public class DeviceManager : IDisposable
    {
        /// <summary>
        /// 气压表数据读取器（接口）
        /// 真实实现 ModbusRtuBarometerReader 或 Mock 实现 MockBarometerReader
        /// </summary>
        private readonly IBarometerReader _barometerReader;

        /// <summary>
        /// IO控制器（接口）
        /// 真实实现 ModbusTcpIoController 或 Mock 实现 MockIoController
        /// </summary>
        private readonly IIoController _ioController;

        /// <summary>
        /// 冷却送风机控制器（接口）
        /// 【V1.10 新增】
        /// - FanEnabled=true：真实实现 FanControllerClient 或 Mock 实现 MockFanController
        /// - FanEnabled=false：null（不启用送风机，所有送风机方法做 null 判空）
        /// </summary>
        private readonly IFanController _fanController;

        /// <summary>
        /// 设备配置
        /// </summary>
        private readonly DeviceConfig _config;

        /// <summary>
        /// 数据采集定时器（72 台气压表 + IO，主采集链路）
        /// </summary>
        private readonly System.Timers.Timer _collectTimer;

        /// <summary>
        /// 送风机独立轮询定时器
        /// 【设计说明】送风机只有 1 台，把它放进 72 台气压表的采集循环里，
        /// 一旦送风机通讯超时会拖慢整个采集、延迟报警判定。
        /// 所以用独立定时器 + 独立锁，送风机出问题只影响它自己。
        /// </summary>
        private readonly System.Timers.Timer _fanTimer;

        /// <summary>
        /// 送风机轮询防重入锁（参照 _collectLock 的模式）
        /// </summary>
        private readonly object _fanPollLock = new object();

        /// <summary>
        /// 送风机轮询间隔（毫秒）
        /// Demo 文档建议读取间隔 &gt;= 500ms，这里用 2000ms 减少无谓通讯
        /// </summary>
        private const int FanPollIntervalMs = 2000;

        /// <summary>
        /// IO 耦合器自动重连节流间隔（毫秒）
        /// 【V1.16 新增】门禁解耦后，耦合器断开不影响压力采集；但它每 5 秒尝试重连一次，
        /// 现场重启耦合器 / 插拔网线后，几秒内自动恢复"阀 / 载台电"控制，不用重启程序。
        /// </summary>
        private const int IoReconnectIntervalMs = 5000;

        /// <summary>
        /// 上次尝试重连 IO 耦合器的时间（用于节流，防止对已断开的设备频繁发起连接）
        /// </summary>
        private DateTime _lastIoConnectAttempt = DateTime.MinValue;

        /// <summary>
        /// 上一次上报的 IO 耦合器连接状态（边沿检测）
        /// 【V1.16.1】每次采集周期比对当前状态与上次上报值，只在"连上/断开"边沿
        /// 触发一次 OnConnectionStatusChanged，避免每个周期重复刷 UI。
        /// 初始值为 false（构造函数里耦合器还没连接）。
        /// </summary>
        private bool _lastIoConnected;

        /// <summary>
        /// 气压表串口自动重连节流间隔（毫秒，【V1.16.2 新增】）
        /// 串口断开后至少间隔 5 秒重试一次，避免对已拔出的适配器频繁重连。
        /// </summary>
        private const int BarometerReconnectIntervalMs = 5000;

        /// <summary>
        /// 上次尝试重连气压表串口的时间（【V1.16.2 新增】，用于节流）
        /// </summary>
        private DateTime _lastBarometerReconnectAttempt = DateTime.MinValue;

        /// <summary>
        /// 上一次气压表串口连接状态（【V1.16.2 新增】，边沿检测）
        /// 由"已连接 → 未连接"时提示一次"气压表串口已断开"，避免每个采集周期刷日志。
        /// </summary>
        private bool _barometerWasConnected;

        /// <summary>
        /// 上一次送风机连接状态（【V1.16.2 新增】，边沿检测）
        /// 由"已连接 → 未连接"时提示一次"送风机已断开"，避免每个轮询周期刷日志。
        /// </summary>
        private bool _lastFanConnected;

        /// <summary>
        /// 存储所有气压表的最新数据
        /// Key: 设备编号，Value: 气压表数据
        /// 【线程安全】使用 _cacheLock 保护
        /// </summary>
        private readonly Dictionary<int, BarometerData> _barometerDataCache = new Dictionary<int, BarometerData>();

        /// <summary>
        /// 工位静态信息存储（【V1.19.11 新增】）
        /// Key: 工位编号，Value: 该工位的 SN / 配方 / 延时配置。
        ///
        /// 【用途】真实气压表只上报压力，SN / 配方 / 延时无法从设备读取，
        /// 需由上位机维护（ID 绑定扫码/手动录入 SN、工位设置窗口录入配方/延时），
        /// 再在每次采集时叠加到 BarometerData 上，让工位面板同步展示。
        /// 【线程安全】使用 _stationInfoLock 保护
        /// </summary>
        private readonly Dictionary<int, StationInfo> _stationInfo = new Dictionary<int, StationInfo>();

        /// <summary>
        /// 工位静态信息锁（保护 _stationInfo）
        /// 与 _cacheLock 分开：采集线程（读叠加）与 UI 线程（写绑定/设置）并发访问
        /// </summary>
        private readonly object _stationInfoLock = new object();

        /// <summary>
        /// 送风机最新数据
        /// 【线程安全】使用 _cacheLock 保护
        /// </summary>
        private FanData _fanDataCache;

        /// <summary>
        /// 数据缓存锁对象（保护 _barometerDataCache / _fanDataCache）
        /// </summary>
        private readonly object _cacheLock = new object();

        /// <summary>
        /// 采集锁对象（防止主采集定时器重入）
        /// </summary>
        private readonly object _collectLock = new object();

        /// <summary>
        /// 测试状态锁对象
        /// 【V1.10 新增】保护所有"测试状态数组"的并发访问。
        /// 为什么需要独立的锁：
        /// - 采集线程（定时器）会在 CollectData 里读写这些状态
        /// - UI 线程（启动运行/停止运行/复位按钮）也会写这些状态
        /// - 不加锁会导致 bool[] 元素读写错乱
        /// </summary>
        private readonly object _stateLock = new object();

        /// <summary>
        /// 记录上一次采集周期的"报警状态"（边沿检测）
        /// 只在该路"从未报警 → 进入报警"的边沿触发一次联动输出，避免每 1s 重复下发
        /// </summary>
        private readonly bool[] _lastAlarmStates;

        /// <summary>
        /// 每台是否正在老化测试
        /// 【V1.10 新增】由"启动运行/停止运行/报警/复位"控制
        /// </summary>
        private readonly bool[] _testingStates;

        /// <summary>
        /// 每台本次测试的开始时间（真空确认完成时刻）
        /// 用于老化计时（到时自动停止）。DateTime.MinValue = 尚未开始计时
        /// </summary>
        private readonly DateTime[] _testStartTimes;

        /// <summary>
        /// 每台本次测试的时长（秒），0 = 不限时长
        /// </summary>
        private readonly int[] _testDurations;

        /// <summary>
        /// 每台"真空确认"的开始时间（开阀时刻）
        /// DateTime.MinValue = 已确认完成（真空已建立）或未在测试
        /// 非 MinValue = 仍在"真空确认宽限窗口"内（等待压力进入正常区间）
        /// </summary>
        private readonly DateTime[] _vacuumConfirmTimes;

        /// <summary>
        /// 每台连续读取失败次数
        /// 用于通讯故障报警（连续失败 CommunicationLossAlarmCount 次 → 报警）
        /// </summary>
        private readonly int[] _readFailCounts;

        /// <summary>
        /// 每台最近一次成功读数的时间
        /// 用于统计"在线台数"（旧数据视为离线）
        /// </summary>
        private readonly DateTime[] _lastGoodTimes;

        /// <summary>
        /// 系统认为送风机当前应处于的运行状态
        /// 【用途】让"首台启动/末台停止"的联动只在下发一次命令，
        /// 避免每个采集周期都重复写 0x0003/0x0002 造成无谓通讯。
        /// </summary>
        private bool _fanRunning;

        /// <summary>
        /// 当前批号（由 UI 录入批号后设置，用于日志追溯）
        /// </summary>
        private string _currentLotNumber = "";

        /// <summary>
        /// 标记是否已释放资源（volatile 保证跨线程可见）
        /// </summary>
        private volatile bool _disposed = false;

        /// <summary>
        /// 批量数据更新事件（一次采集周期触发一次，参数为本次采集的所有数据）
        /// 【注意】在后台线程触发，UI 层需用 BeginInvoke 切到 UI 线程
        /// </summary>
        public event EventHandler<BarometerData[]> OnBatchDataUpdated;

        /// <summary>
        /// 连接状态变更事件（【V1.16.1】语义 = IO 耦合器是否连接）
        /// 顶部"通讯连接状态"只判断耦合器（阀 / 载台电控制）是否连通：
        /// - true = 耦合器已连上；false = 耦合器未连上（气压表 / 送风机状态不并入本事件）。
        /// 送风机是可选设备，其连接状态见 <see cref="OnFanDataUpdated"/>。
        /// </summary>
        public event EventHandler<bool> OnConnectionStatusChanged;

        /// <summary>
        /// 送风机数据更新事件
        /// 【V1.10 新增】送风机独立定时器轮询后触发，参数为最新 FanData（失败时为 null）
        /// 【注意】在后台线程触发，UI 层需用 BeginInvoke 切到 UI 线程
        /// </summary>
        public event EventHandler<FanData> OnFanDataUpdated;

        /// <summary>
        /// 启动/连接诊断事件（【V1.16 新增】）
        /// 启动时逐步上报：实际使用的气压表串口、IO 耦合器连接结果、送风机连接结果、
        /// 耦合器自动重连成功等，UI 层把内容写进 LOG，让现场一眼看到"到底哪一步连不上"。
        /// 【注意】在后台线程触发，UI 层需用 BeginInvoke 切到 UI 线程写日志。
        /// </summary>
        public event EventHandler<string> OnDiagnostic;

        /// <summary>
        /// 上次启动失败的诊断信息（【V1.16 新增】）
        /// 供 MainForm 启动失败时把原因写到 LOG/提示，避免"只显示未连接却不知道原因"。
        /// </summary>
        public string LastStartupError { get; private set; } = "";

        /// <summary>
        /// 当前批号（用于日志追溯）
        /// </summary>
        public string CurrentLotNumber
        {
            get { return _currentLotNumber; }
            set { _currentLotNumber = value ?? ""; }
        }

        /// <summary>
        /// 是否启用了送风机
        /// </summary>
        public bool IsFanEnabled => _config.FanEnabled;

        /// <summary>
        /// IO 耦合器当前是否已连接（【V1.16.1 新增】）
        /// 顶部"通讯连接状态"标签的数据源：只反映耦合器（阀 / 载台电控制）是否连通。
        /// 后台读/写失败时会自动置 false，TryReconnectIo 自动重连成功后置 true。
        /// </summary>
        public bool IsIoConnected => _ioController.IsConnected;

        /// <summary>
        /// 初始化设备管理器
        /// </summary>
        public DeviceManager(DeviceConfig config)
        {
            _config = config;

            // 初始化硬件接口实现（真实 / Mock 二选一，由 App.config 的 UseMockCommunication 决定）
            if (_config.UseMockCommunication)
            {
                _barometerReader = new MockBarometerReader();
                _ioController = new MockIoController();
                // 送风机 Mock：只有启用送风机时才创建
                _fanController = _config.FanEnabled ? new MockFanController() : null;
            }
            else
            {
                _barometerReader = new ModbusRtuBarometerReader();
                _ioController = new ModbusTcpIoController();
                // 送风机真实实现：只有启用送风机时才创建
                _fanController = _config.FanEnabled ? new FanControllerClient() : null;
            }

            // 初始化状态数组（按气压表总数）
            _lastAlarmStates = new bool[_config.TotalBarometers];
            _testingStates = new bool[_config.TotalBarometers];
            _testStartTimes = new DateTime[_config.TotalBarometers];
            _testDurations = new int[_config.TotalBarometers];
            _vacuumConfirmTimes = new DateTime[_config.TotalBarometers];
            _readFailCounts = new int[_config.TotalBarometers];
            _lastGoodTimes = new DateTime[_config.TotalBarometers];

            // 订阅错误事件（使用命名方法，便于 Dispose 时取消订阅）
            _barometerReader.OnError += BarometerReader_OnError;
            _ioController.OnError += IoController_OnError;
            if (_fanController != null)
            {
                _fanController.OnError += FanController_OnError;
            }

            // 主采集定时器（72 台气压表 + IO）
            _collectTimer = new System.Timers.Timer(_config.CollectInterval);
            _collectTimer.Elapsed += CollectTimer_Elapsed;
            _collectTimer.AutoReset = true;

            // 送风机独立轮询定时器（只有启用时才创建）
            if (_config.FanEnabled)
            {
                _fanTimer = new System.Timers.Timer(FanPollIntervalMs);
                _fanTimer.Elapsed += FanTimer_Elapsed;
                _fanTimer.AutoReset = true;
            }
        }

        /// <summary>
        /// 气压表读取器错误回调
        /// </summary>
        private void BarometerReader_OnError(object sender, string message)
        {
            System.Diagnostics.Debug.WriteLine($"气压表读取错误: {message}");
        }

        /// <summary>
        /// IO控制器错误回调
        /// </summary>
        private void IoController_OnError(object sender, string message)
        {
            System.Diagnostics.Debug.WriteLine($"IO控制错误: {message}");
        }

        /// <summary>
        /// 送风机错误回调（【V1.10 新增】）
        /// </summary>
        private void FanController_OnError(object sender, string message)
        {
            System.Diagnostics.Debug.WriteLine($"送风机错误: {message}");
        }

        /// <summary>
        /// 启动设备管理器
        /// 连接设备并开始数据采集
        ///
        /// 【V1.10 说明】送风机是可选设备：连接失败不影响整机启动，
        /// 只记日志（送风机独立定时器会周期尝试自动重连）。
        ///
        /// 【V1.16 门禁解耦】只要求"气压表串口"连通；IO 耦合器/送风机是可选设备，
        /// 断开不影响压力采集。返回值 = 气压表是否连通。
        /// </summary>
        /// <returns>是否启动成功（成功 = 气压表串口已连接）</returns>
        public bool Start()
        {
            try
            {
                // 收集启动失败原因，最后汇总进 LastStartupError（供 MainForm 提示）
                var startupErrors = new List<string>();

                // ===== 连接各设备（气压表=主设备；耦合器/送风机=可选设备） =====

                // 连接气压表读取器（主设备：串口连不上则无法采集压力数据）
                bool barometerConnected = _barometerReader.Connect(_config);
                if (barometerConnected)
                {
                    Diagnostic($"气压表串口已连接：{(_barometerReader.CurrentPortName ?? _config.PortName)}");
                }
                else
                {
                    string msg =
                        $"气压表串口连接失败（配置端口 {_config.PortName} 不存在且未识别到 CH340，请检查 RS485 适配器/驱动）";
                    startupErrors.Add(msg);
                    Diagnostic(msg);
                }

                // 连接IO耦合器（可选：断开不影响压力采集，仅阀/载台电控制不可用，会自动重连）
                bool ioConnected = _ioController.Connect(_config);
                if (ioConnected)
                {
                    Diagnostic($"IO耦合器已连接：{_config.PlcAddress}:{_config.PlcPort}");
                }
                else
                {
                    string msg =
                        $"IO耦合器 {_config.PlcAddress}:{_config.PlcPort} 连接失败（不影响气压表采集，控制阀/载台电暂不可用，将自动重连）";
                    startupErrors.Add(msg);
                    Diagnostic(msg);
                }

                // 尝试连接送风机（独立 try/catch 隔离，失败只记诊断）
                TryConnectFan();

                // 汇总启动失败原因（若都成功则保持空字符串）
                LastStartupError = string.Join("；", startupErrors);

                // ===== 门禁解耦（V1.16 核心修复） =====
                // 原来要求"气压表 + 耦合器全部成功"才启动采集；耦合器连不上时会把
                // 气压表和送风机一起回滚、定时器也不启动 → 整个界面无数据。
                // 现在：只要气压表连通就开始采集（压力数据优先）；耦合器/送风机断开
                // 不影响主链路，各自在后台自动重连。
                if (barometerConnected)
                {
                    // 先采集一次数据（同步调用，确保首次数据立即可用）
                    CollectData();

                    // 启动数据采集定时器
                    _collectTimer.Start();
                }

                // 送风机独立轮询定时器：无论主设备是否连通都启动
                //（送风机是独立 TCP 设备，可以单独工作）
                if (_fanTimer != null) _fanTimer.Start();

                // 触发连接状态变更事件（【V1.16.1】语义：IO 耦合器是否连接）
                // 顶部"通讯连接状态"只判断耦合器（阀 / 载台电控制）是否连通，
                // 不再用气压表串口状态冒充耦合器状态。
                _lastIoConnected = ioConnected;
                OnConnectionStatusChanged?.Invoke(this, ioConnected);

                // 【V1.16.2 心跳】初始化边沿检测的"上一次"状态：
                // 之后某个设备中途断开时，能正确识别"已连接 → 未连接"边沿并提示一次。
                _barometerWasConnected = barometerConnected;
                _lastFanConnected = _fanController != null && _fanController.IsConnected;

                return barometerConnected;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设备管理器启动失败: {ex.Message}");
                LastStartupError = $"设备管理器启动异常：{ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// 尝试连接送风机（可选设备）
        /// 独立 try/catch：连接失败只记诊断，不影响整机启动。
        /// 送风机连接失败后，其独立定时器的 EnsureConnected 会按节流自动重连。
        /// </summary>
        private void TryConnectFan()
        {
            try
            {
                if (_fanController != null && !_fanController.IsConnected)
                {
                    bool ok = _fanController.Connect(_config);
                    Diagnostic(ok
                        ? $"冷却送风机已连接：{(_fanController.ActiveIp ?? _config.FanIpAddress)}:{_config.FanPort}"
                        : "冷却送风机连接失败（已尝试候选 IP），将按节流自动重连，请检查送风机 IP/网线");
                }
            }
            catch (Exception ex)
            {
                Diagnostic($"冷却送风机连接异常：{ex.Message}");
            }
        }

        /// <summary>
        /// 上报诊断信息（【V1.16 新增】）
        /// 触发 OnDiagnostic 事件 + 写 Debug 输出。
        /// 【注意】可能在后台线程调用，UI 层订阅后需用 BeginInvoke 切回 UI 线程。
        /// </summary>
        /// <param name="message">诊断文本</param>
        private void Diagnostic(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[诊断] {message}");
            OnDiagnostic?.Invoke(this, message);
        }

        /// <summary>
        /// IO 耦合器心跳 + 自动重连（【V1.16 新增】【V1.16.2 心跳机制】）
        ///
        /// 门禁解耦后，耦合器断开只影响"阀 / 载台电"控制，不影响压力采集。
        /// 本方法在每个采集周期调用：
        /// - 状态边沿检测：连上/断开各上报一次 OnConnectionStatusChanged（顶部标签实时更新），
        ///   并在"已连接 → 断开"边沿记一次日志，明确提醒"哪个设备断了"；
        /// - 未连接时按节流（5 秒）在后台线程静默重连，连上后提示一次；失败过程不刷日志。
        /// 现场重启耦合器 / 插拔网线后，几秒内自动恢复控制，不用重启程序。
        /// </summary>
        private void TryReconnectIo()
        {
            // ===== 状态边沿检测 =====
            // 每个采集周期比对一次"当前耦合器连接状态"与"上次上报的状态"：
            // 连上 → 上报 true，断开（读/写失败自动置 false）→ 上报 false，
            // 顶部"通讯连接状态"标签据此实时显示耦合器是否连接。
            bool ioConnectedNow = _ioController.IsConnected;
            if (ioConnectedNow != _lastIoConnected)
            {
                _lastIoConnected = ioConnectedNow;
                OnConnectionStatusChanged?.Invoke(this, ioConnectedNow);

                // 【V1.16.2 心跳】只在"已连接 → 断开"边沿提示一次"哪里断了"；
                // 重连成功由下面 Connect 成功分支提示（避免每个采集周期刷日志）。
                if (!ioConnectedNow)
                {
                    Diagnostic("IO耦合器已断开（通讯异常），正在后台自动重连...");
                }
            }

            // 已连接则无需处理
            if (ioConnectedNow) return;

            // 节流：两次重连尝试至少间隔 5 秒，避免对已断开的设备频繁发起连接
            if ((DateTime.Now - _lastIoConnectAttempt).TotalMilliseconds < IoReconnectIntervalMs) return;
            _lastIoConnectAttempt = DateTime.Now;

            // 在后台线程执行连接（耦合器 TCP 超时最长约 3 秒），不阻塞采集循环
            System.Threading.Tasks.Task.Run(() =>
            {
                bool ok = _ioController.Connect(_config);
                if (ok)
                {
                    Diagnostic($"IO耦合器已自动重连：{_config.PlcAddress}:{_config.PlcPort}");
                    // 重连成功 → 上报"已连接"（边沿检测会在下一采集周期覆盖，这里立即上报更及时）
                    _lastIoConnected = true;
                    OnConnectionStatusChanged?.Invoke(this, true);
                }
                // 连接失败：静默（V1.16.2 心跳机制下后台继续按节流重试，不再报"自动重连已停止"）
            });
        }

        /// <summary>
        /// IO 耦合器按需连接（【V1.16.1 新增】）
        /// 用户操作需要耦合器时（上电/开阀/启动停止测试等）调用：
        /// 已连接直接返回 true；未连接则立即重连一次（即使自动重连已放弃），
        /// 连上则复位自动重连状态，连不上返回 false（由调用方弹窗提示"耦合器未连接，请先连接"）。
        /// 【注意】在调用线程同步执行，耦合器 TCP 超时最长约 3 秒。
        /// </summary>
        /// <returns>当前是否可用（已连接）</returns>
        public bool EnsureIoConnected()
        {
            if (_ioController.IsConnected) return true;

            bool ok = _ioController.Connect(_config);
            if (ok)
            {
                // 按需连上：更新状态并上报（心跳机制下后台会持续静默重连，无需复位计数）
                _lastIoConnected = true;
                OnConnectionStatusChanged?.Invoke(this, true);
                Diagnostic($"IO耦合器已连接：{_config.PlcAddress}:{_config.PlcPort}");
            }
            return ok;
        }

        /// <summary>
        /// 气压表串口心跳 + 后台自动重连（【V1.16.2 新增】）
        /// 每个采集周期调用一次：
        /// - 状态边沿：气压表由"已连接 → 未连接"时，记一次"气压表串口已断开"日志，
        ///   明确提醒操作员哪个设备断了（ModbusRtuBarometerReader 检测到端口级故障
        ///   会自动把 IsConnected 置 false，本方法据此感知）；
        /// - 未连接时按节流（5 秒）在后台线程重连，连上后提示一次；失败过程静默不刷日志。
        /// 【性能】重连在 Task.Run 后台执行，不阻塞 72 台采集主链路；串口未连接时
        /// ReadAllData 几乎零开销，不影响其它设备（耦合器/送风机各自独立重连）。
        /// </summary>
        private void TryReconnectBarometer()
        {
            bool baroConnected = _barometerReader.IsConnected;

            // 边沿：已连接 → 未连接，提示一次"哪里断了"
            if (_barometerWasConnected && !baroConnected)
            {
                _barometerWasConnected = false;
                Diagnostic("气压表串口已断开（RS485 适配器被拔出/掉线），正在后台自动重连...");
            }
            if (baroConnected)
            {
                _barometerWasConnected = true;
                return;
            }

            // 节流：至少间隔 5 秒，避免对已断开的串口频繁重连
            if ((DateTime.Now - _lastBarometerReconnectAttempt).TotalMilliseconds < BarometerReconnectIntervalMs) return;
            _lastBarometerReconnectAttempt = DateTime.Now;

            // 后台线程重连（打开串口 + 建 Modbus 主站，约几十毫秒），不阻塞采集循环
            System.Threading.Tasks.Task.Run(() =>
            {
                bool ok = _barometerReader.Connect(_config);
                if (ok)
                {
                    _barometerWasConnected = true;
                    Diagnostic($"气压表串口已重连：{(_barometerReader.CurrentPortName ?? _config.PortName)}");
                }
                // 重连失败：静默（后台继续按节流重试）
            });
        }

        /// <summary>
        /// 停止设备管理器
        /// 停止数据采集并断开设备连接
        /// </summary>
        public void Stop()
        {
            // 先停止定时器，防止新的采集任务进入
            _collectTimer.Stop();
            if (_fanTimer != null) _fanTimer.Stop();

            // 断开硬件连接
            _barometerReader.Disconnect();
            _ioController.Disconnect();
            if (_fanController != null) _fanController.Disconnect();

            // 仅在非 Dispose 期间触发事件
            if (!_disposed)
            {
                OnConnectionStatusChanged?.Invoke(this, false);
            }
        }

        /// <summary>
        /// 获取指定设备的最新数据（返回副本，避免外部修改污染缓存）
        /// </summary>
        public BarometerData GetBarometerData(int deviceId)
        {
            lock (_cacheLock)
            {
                _barometerDataCache.TryGetValue(deviceId, out BarometerData data);
                return data?.Clone();
            }
        }

        /// <summary>
        /// 获取所有设备的最新数据（返回副本）
        /// </summary>
        public BarometerData[] GetAllBarometerData()
        {
            lock (_cacheLock)
            {
                var data = new BarometerData[_config.TotalBarometers];
                for (int i = 0; i < _config.TotalBarometers; i++)
                {
                    _barometerDataCache.TryGetValue(i + 1, out BarometerData original);
                    data[i] = original?.Clone();
                }
                return data;
            }
        }

        /// <summary>
        /// 获取指定工位的静态信息（SN / 配方 / 延时，【V1.19.11 新增】）
        /// 用于工位设置窗口回显。返回副本，避免外部修改污染内部存储。
        /// </summary>
        /// <param name="deviceId">工位编号（1 ~ TotalBarometers）</param>
        /// <returns>工位静态信息；未配置过时返回 null</returns>
        public StationInfo GetStationInfo(int deviceId)
        {
            lock (_stationInfoLock)
            {
                _stationInfo.TryGetValue(deviceId, out StationInfo info);
                return info?.Clone();
            }
        }

        /// <summary>
        /// 设置指定工位的 SN（【V1.19.11 新增】）
        ///
        /// 【调用方】ID 绑定（IdBindingForm 保存时，扫码枪扫码或手动输入的 SN）、
        /// 工位设置窗口（StationSettingsForm 保存按钮）。
        /// 写入后，采集线程在下次采集时把 SN 叠加到该工位的数据上，
        /// 工位面板的 SN 标签即可同步显示。
        /// </summary>
        /// <param name="deviceId">工位编号（1 ~ TotalBarometers）</param>
        /// <param name="serialNumber">产品序列号（空字符串/空白视为清空）</param>
        public void SetStationSerialNumber(int deviceId, string serialNumber)
        {
            if (deviceId < 1 || deviceId > _config.TotalBarometers) return;
            string sn = serialNumber?.Trim() ?? "";
            lock (_stationInfoLock)
            {
                if (!_stationInfo.TryGetValue(deviceId, out StationInfo info))
                {
                    info = new StationInfo { DeviceId = deviceId };
                    _stationInfo[deviceId] = info;
                }
                info.SerialNumber = sn;
            }
        }

        /// <summary>
        /// 设置指定工位的配方名称（【V1.19.11 新增】）
        /// 由工位设置窗口保存按钮调用。写入后采集叠加显示在工位面板配方标签上。
        /// </summary>
        /// <param name="deviceId">工位编号（1 ~ TotalBarometers）</param>
        /// <param name="recipeName">配方名称（空白视为清空）</param>
        public void SetStationRecipeName(int deviceId, string recipeName)
        {
            if (deviceId < 1 || deviceId > _config.TotalBarometers) return;
            string name = recipeName?.Trim() ?? "";
            lock (_stationInfoLock)
            {
                if (!_stationInfo.TryGetValue(deviceId, out StationInfo info))
                {
                    info = new StationInfo { DeviceId = deviceId };
                    _stationInfo[deviceId] = info;
                }
                info.RecipeName = name;
            }
        }

        /// <summary>
        /// 设置指定工位的延时时间（【V1.19.11 新增】）
        /// 由工位设置窗口保存按钮调用。写入后采集叠加显示在工位面板延时标签上。
        /// </summary>
        /// <param name="deviceId">工位编号（1 ~ TotalBarometers）</param>
        /// <param name="delayStartTime">延时开启时间（可空 = 不修改）</param>
        /// <param name="delayArriveTime">延时到达时间（可空 = 不修改）</param>
        public void SetStationDelayTimes(int deviceId, TimeSpan? delayStartTime, TimeSpan? delayArriveTime)
        {
            if (deviceId < 1 || deviceId > _config.TotalBarometers) return;
            lock (_stationInfoLock)
            {
                if (!_stationInfo.TryGetValue(deviceId, out StationInfo info))
                {
                    info = new StationInfo { DeviceId = deviceId };
                    _stationInfo[deviceId] = info;
                }
                if (delayStartTime.HasValue) info.DelayStartTime = delayStartTime;
                if (delayArriveTime.HasValue) info.DelayArriveTime = delayArriveTime;
            }
        }

        /// <summary>
        /// 批量设置指定工位的 SN（【V1.19.11 新增，供 ID 绑定保存时调用）
        /// 一次写入多个工位的 SN，避免逐台循环加锁。
        /// </summary>
        /// <param name="serialNumbers">工位编号 → SN 的映射（无效编号自动忽略）</param>
        public void SetStationSerialNumbers(IReadOnlyDictionary<int, string> serialNumbers)
        {
            if (serialNumbers == null || serialNumbers.Count == 0) return;
            lock (_stationInfoLock)
            {
                foreach (var kv in serialNumbers)
                {
                    int deviceId = kv.Key;
                    if (deviceId < 1 || deviceId > _config.TotalBarometers) continue;
                    if (!_stationInfo.TryGetValue(deviceId, out StationInfo info))
                    {
                        info = new StationInfo { DeviceId = deviceId };
                        _stationInfo[deviceId] = info;
                    }
                    info.SerialNumber = kv.Value?.Trim() ?? "";
                }
            }
        }

        /// <summary>
        /// 把工位静态信息（SN / 配方 / 延时）叠加到采集数据上（【V1.19.11 新增】）
        ///
        /// 【为什么需要叠加】
        /// 真实气压表只上报压力，BarometerData 的 SerialNumber / RecipeName /
        /// DelayStartTime / DelayArriveTime 在采集层是空的。
        /// 工位面板（WorkstationPanelView）显示的 SN / 配方 / 延时正是读取这些字段，
        /// 所以必须在数据流出前把 _stationInfo 里维护的配置覆盖上去，
        /// 保证"有显示 SN/配方/延时 的地方都与绑定/设置关联一致"。
        ///
        /// 【与 Mock 的关系】
        /// Mock 读取器生成的 SN / 配方 / 延时是模拟值；本方法只在工位静态信息
        /// 已配置（非空）时覆盖，未配置的工位保留原值（Mock 模拟值 / 空）。
        /// </summary>
        /// <param name="data">采集到的工位数据（会被就地修改）</param>
        private void ApplyStationInfo(BarometerData data)
        {
            if (data == null) return;

            StationInfo info;
            lock (_stationInfoLock)
            {
                if (!_stationInfo.TryGetValue(data.DeviceId, out info)) return;
                info = info.Clone();
            }

            // 仅覆盖"已配置"的字段：配置过 SN 才写 SN，配方/延时同理
            if (!string.IsNullOrEmpty(info.SerialNumber))
            {
                data.SerialNumber = info.SerialNumber;
            }
            if (!string.IsNullOrEmpty(info.RecipeName))
            {
                data.RecipeName = info.RecipeName;
            }
            if (info.DelayStartTime.HasValue)
            {
                data.DelayStartTime = info.DelayStartTime.Value;
            }
            if (info.DelayArriveTime.HasValue)
            {
                data.DelayArriveTime = info.DelayArriveTime.Value;
            }
        }

        /// <summary>
        /// 写入单台气压表的设备阈值（透传 IBarometerReader.SetThreshold）
        ///
        /// 【单位提醒】thresholdValue 是"设备单位"（与压力读数同单位同小数位），
        /// 不是软件报警阈值 AlarmPressureThresholdKPa（kPa）。写前务必确认设备单位。
        /// </summary>
        /// <param name="deviceId">气压表编号（1~TotalBarometers）</param>
        /// <param name="thresholdValue">设备单位阈值（如 -95.0）</param>
        /// <returns>是否写入成功（设备不响应返回 false）</returns>
        public bool SetBarometerThreshold(int deviceId, decimal thresholdValue)
        {
            return _barometerReader.SetThreshold(deviceId, thresholdValue);
        }

        /// <summary>
        /// 更新软件报警压力阈值（【V1.19.9 新增】）
        ///
        /// 公共参数窗口保存负压值（单位 kPa）时同步调用，把界面输入的负压值写入
        /// <see cref="DeviceConfig.AlarmPressureThresholdKPa"/>，让 DeviceManager 的压力
        /// 报警判定（IsAlarm / PressureOutOfRange）与气压表设备阈值保持一致。
        ///
        /// 【单位】thresholdKPa 单位是 kPa（与气压表读数同单位，如 -95）。
        /// </summary>
        /// <param name="thresholdKPa">报警压力阈值（kPa，如 -95）</param>
        public void UpdateAlarmPressureThresholdKPa(decimal thresholdKPa)
        {
            _config.AlarmPressureThresholdKPa = thresholdKPa;
        }

        /// <summary>
        /// 批量写入所有气压表的设备阈值（透传 IBarometerReader.SetAllThresholds）
        ///
        /// 返回 deviceId → 是否成功，方便上层汇总"哪些台没写进去"。
        /// 【性能提示】72 台连写 + 坏设备会阻塞较久，调用方应在后台线程执行，
        /// 不要直接放在 UI 线程里（否则界面会卡住数十秒）。
        ///
        /// 【V1.16 修复】批量写期间【暂停主采集定时器】：
        /// 原来批量写和 1s 采集定时器会争抢同一条 RS485 串口总线，导致写帧大量超时
        /// （现场表现为"保存失败 N 台"）。这里在批量写期间停掉采集，写完再恢复，
        /// 与 Demo 的 BatchSetThreshold（独占总线批量写）行为对齐。
        /// </summary>
        /// <param name="thresholdValue">设备单位阈值（与压力读数同单位同小数位）</param>
        /// <returns>写入结果字典（deviceId → 是否成功）</returns>
        public Dictionary<int, bool> SetAllBarometerThresholds(decimal thresholdValue)
        {
            // 记住采集定时器当前是否在跑，批量写结束后按原状态恢复
            bool timerWasRunning = _collectTimer.Enabled;
            _collectTimer.Stop();

            try
            {
                return _barometerReader.SetAllThresholds(thresholdValue);
            }
            finally
            {
                // 无论成功失败都要恢复采集（try/finally 保证）
                if (timerWasRunning) _collectTimer.Start();
            }
        }

        /// <summary>
        /// 设置输出点状态
        /// </summary>
        public void SetOutput(int outputId, bool state)
        {
            _ioController.WriteOutput(outputId, state);
        }

        /// <summary>
        /// 读取单个输出点状态（用于手动控制对话框实时回读）
        /// 【V1.10 新增】透传 IIoController.ReadOutput
        /// </summary>
        public bool GetOutput(int outputId)
        {
            return _ioController.ReadOutput(outputId);
        }

        /// <summary>
        /// 获取输入点状态
        /// </summary>
        public bool GetInput(int inputId)
        {
            return _ioController.ReadInput(inputId);
        }

        /// <summary>
        /// 获取所有输入点状态
        /// </summary>
        public bool[] GetAllInputs()
        {
            return _ioController.ReadAllInputs();
        }

        /// <summary>
        /// 获取所有输出点状态
        /// </summary>
        public bool[] GetAllOutputs()
        {
            return _ioController.ReadAllOutputs();
        }

        // =====================================================================
        // 送风机相关（【V1.10 新增】）
        // =====================================================================

        /// <summary>
        /// 获取送风机最新数据（返回副本）
        /// </summary>
        public FanData GetFanData()
        {
            lock (_cacheLock)
            {
                return _fanDataCache?.Clone();
            }
        }

        /// <summary>
        /// 手动定值启动送风机
        /// 幂等：多次调用只下发一次命令（用 _fanRunning 做状态记忆）
        /// 【V1.16.1】未连接时先按需重连一次，连不上返回 false（上层弹窗提示"送风机未连接"）。
        /// </summary>
        public bool StartFan()
        {
            if (_fanController == null) return false;
            if (!_fanController.IsConnected && !_fanController.ReconnectNow())
            {
                return false;   // 按需重连失败：送风机不可用
            }
            _fanRunning = true;
            return _fanController.StartFixedValue();
        }

        /// <summary>
        /// 手动定值停止送风机
        /// 【注意】如果有任何一台正在测试，下一次采集循环会自动重新启动送风机
        /// （送风机是环境设备，测试期间必须保持运行）。
        /// 【V1.16.1】未连接时先按需重连一次，连不上返回 false（上层弹窗提示）。
        /// </summary>
        public bool StopFan()
        {
            if (_fanController == null) return false;
            if (!_fanController.IsConnected && !_fanController.ReconnectNow())
            {
                return false;
            }
            _fanRunning = false;
            return _fanController.Stop();
        }

        /// <summary>
        /// 送风机当前是否已连接（【V1.16.1 新增】）
        /// 供 UI 判断是否需要在启动测试前提示"送风机未连接"。
        /// </summary>
        public bool IsFanConnected => _fanController != null && _fanController.IsConnected;

        /// <summary>
        /// 送风机按需重连（【V1.16.1 新增】）
        /// 用户操作需要送风机时调用：已连接直接返回 true，未连接立即重连一次。
        /// </summary>
        /// <returns>重连后是否已连接</returns>
        public bool ReconnectFan()
        {
            if (_fanController == null) return false;
            if (_fanController.IsConnected) return true;
            return _fanController.ReconnectNow();
        }

        /// <summary>
        /// 送风机独立轮询定时器触发
        /// </summary>
        private void FanTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // 已释放则不再轮询
            if (_disposed) return;
            if (_fanController == null) return;

            // 防重入：上一次轮询还没结束则跳过本次
            if (!Monitor.TryEnter(_fanPollLock))
            {
                return;
            }

            try
            {
                PollFanData();
            }
            finally
            {
                Monitor.Exit(_fanPollLock);
            }
        }

        /// <summary>
        /// 轮询送风机状态并广播事件
        /// </summary>
        private void PollFanData()
        {
            FanData data = _fanController.ReadStatus();

            // 【V1.16.2 送风机心跳】连接状态边沿检测：只在"连上 / 断开"各提示一次，
            // 明确提醒操作员"哪个设备断了"；连续失败过程不刷日志。
            //（读失败时 FanControllerClient 已把 IsConnected 置 false，data 为 null）
            bool fanConnected = _fanController.IsConnected;
            if (fanConnected != _lastFanConnected)
            {
                _lastFanConnected = fanConnected;
                if (fanConnected)
                {
                    Diagnostic($"冷却送风机已重连：{(_fanController.ActiveIp ?? _config.FanIpAddress)}:{_config.FanPort}");
                }
                else
                {
                    Diagnostic("冷却送风机已断开（通讯异常），正在后台自动重连...");
                }
            }

            // 缓存最新数据（读失败时 data 为 null，UI 据此显示"离线"）
            lock (_cacheLock)
            {
                _fanDataCache = data;
            }

            // 广播事件（后台线程触发，UI 层自行 BeginInvoke）
            OnFanDataUpdated?.Invoke(this, data);
        }

        /// <summary>
        /// 送风机生命周期联动（首台启动 / 末台停止）
        ///
        /// 【为什么必须这样设计】
        /// 送风机是 72 台共用的"环境设备"。如果按"选中组"启停送风机，
        /// 会出现"第一组启动（风机开）→ 第二组启动（风机已开）→ 第一组停止（风机被停）"
        /// 的交叉场景，导致仍在老化的设备失去温控。
        ///
        /// 正确策略（设计评审结论）：
        /// - 有任何一台在测试 → 送风机必须运行（保持温控）
        /// - 没有任何一台在测试 → 送风机可以停止
        /// 用 _fanRunning 做状态记忆，只在 0→1 / 1→0 边界各下发一次命令。
        /// </summary>
        private void UpdateFanLifecycle()
        {
            if (_fanController == null) return;

            // 判断是否有任何一台正在测试
            bool anyTesting;
            lock (_stateLock)
            {
                anyTesting = Array.IndexOf(_testingStates, true) >= 0;
            }

            if (anyTesting && !_fanRunning)
            {
                // 首台进入测试：启动送风机
                _fanRunning = true;
                if (!_fanController.StartFixedValue())
                {
                    // 【V1.16.2 心跳】送风机未连上：明确告知操作员（安全提示：
                    // 测试期间没有环境温控，后台会静默重连，恢复后送风机自动重新启动）
                    Diagnostic("送风机未连接，无法启动环境温控，正在后台自动重连（测试期间温度不受控！）");
                }
            }
            else if (!anyTesting && _fanRunning)
            {
                // 最后一台退出测试：停止送风机
                _fanRunning = false;
                _fanController.Stop();
            }
        }

        // =====================================================================
        // 老化测试业务流程（【V1.10 新增】）
        // =====================================================================

        /// <summary>
        /// 启动老化测试（批量）
        /// 【动作】对选中的每台：开真空阀 + 载台上电 + 进入"测试中"状态 + 记录日志。
        /// 真空建立确认：开阀后进入 <see cref="DeviceConfig.VacuumConfirmTimeoutMs"/> 宽限窗口，
        /// 期间压力进入正常区间才算确认成功；超时未建立则报警（见 CollectData 判定）。
        ///
        /// 【送风机】首台启动时自动定值启动（见 UpdateFanLifecycle）。
        ///
        /// 【线程安全】UI 线程调用；内部用 _stateLock 写测试状态。
        /// </summary>
        /// <param name="deviceIds">要启动的设备编号数组</param>
        public void StartTesting(int[] deviceIds)
        {
            if (deviceIds == null || deviceIds.Length == 0) return;

            // 收集要写的输出点（每台 2 个：真空阀 ON + 载台上电 ON），一次批量下发
            var outputIds = new List<int>();
            var states = new List<bool>();

            foreach (int deviceId in deviceIds)
            {
                if (deviceId < 1 || deviceId > _config.TotalBarometers) continue;

                // 真空电磁阀 ON（内部编号 = TotalInputs + deviceId）
                outputIds.Add(_config.TotalInputs + deviceId);
                states.Add(true);

                // 载台上电 ON（内部编号 = TotalInputs + TotalBarometers + deviceId）
                outputIds.Add(_config.TotalInputs + _config.TotalBarometers + deviceId);
                states.Add(true);

                // 更新测试状态（在锁内）
                lock (_stateLock)
                {
                    _testingStates[deviceId - 1] = true;                 // 进入测试中
                    _vacuumConfirmTimes[deviceId - 1] = DateTime.Now;    // 开始真空确认宽限
                    _testStartTimes[deviceId - 1] = DateTime.MinValue;   // 真空确认后才开始计时
                    _testDurations[deviceId - 1] = _config.MaxTestDurationSeconds; // 本次时长
                    _lastAlarmStates[deviceId - 1] = false;              // 清报警边沿，允许重新报警
                    _readFailCounts[deviceId - 1] = 0;                   // 清通讯失败计数
                }

                TestEventLogger.Write(_currentLotNumber, deviceId, "启动",
                    "启动老化测试（开真空 + 载台上电）");
            }

            // 批量写输出
            _ioController.WriteOutputs(outputIds.ToArray(), states.ToArray());

            // 送风机生命周期联动（首台启动时启动送风机）
            UpdateFanLifecycle();
        }

        /// <summary>
        /// 停止老化测试（批量）
        /// 【动作】对选中的每台：关真空阀 + 断载台电 + 退出"测试中"状态 + 记录日志。
        /// 【送风机】不在这里直接停送风机！最后一台停止时由 UpdateFanLifecycle 统一停止
        /// （避免停掉还在老化的其它台的环境温控）。
        /// </summary>
        public void StopTesting(int[] deviceIds)
        {
            if (deviceIds == null || deviceIds.Length == 0) return;

            var outputIds = new List<int>();
            var states = new List<bool>();

            foreach (int deviceId in deviceIds)
            {
                if (deviceId < 1 || deviceId > _config.TotalBarometers) continue;

                outputIds.Add(_config.TotalInputs + deviceId);                            // 真空阀 OFF
                states.Add(false);
                outputIds.Add(_config.TotalInputs + _config.TotalBarometers + deviceId);   // 载台上电 OFF
                states.Add(false);

                lock (_stateLock)
                {
                    _testingStates[deviceId - 1] = false;
                    _vacuumConfirmTimes[deviceId - 1] = DateTime.MinValue;
                    _testStartTimes[deviceId - 1] = DateTime.MinValue;
                    _testDurations[deviceId - 1] = 0;
                }

                TestEventLogger.Write(_currentLotNumber, deviceId, "停止", "手动停止");
            }

            _ioController.WriteOutputs(outputIds.ToArray(), states.ToArray());

            // 送风机生命周期联动（如果是最后一台，这里会停止送风机）
            UpdateFanLifecycle();
        }

        /// <summary>
        /// 人工复位（报警/故障台复位回到空闲）
        /// 【动作】清除该台的报警边沿 / 测试状态 / 通讯失败计数，
        /// 并确保输出处于安全关闭状态（阀、载台电都 OFF），可重新启动测试。
        /// 【设计说明】报警后不自动恢复（真空失效原因未确认前自动重启有风险），
        /// 必须由操作员人工确认复位（设计评审结论）。
        /// </summary>
        public void ResetDevices(int[] deviceIds)
        {
            if (deviceIds == null) return;

            foreach (int deviceId in deviceIds)
            {
                if (deviceId < 1 || deviceId > _config.TotalBarometers) continue;

                lock (_stateLock)
                {
                    _testingStates[deviceId - 1] = false;
                    _vacuumConfirmTimes[deviceId - 1] = DateTime.MinValue;
                    _testStartTimes[deviceId - 1] = DateTime.MinValue;
                    _testDurations[deviceId - 1] = 0;
                    _readFailCounts[deviceId - 1] = 0;
                    _lastAlarmStates[deviceId - 1] = false;
                }

                // 复位后保证输出处于安全关闭状态
                _ioController.WriteOutput(_config.TotalInputs + deviceId, false);
                _ioController.WriteOutput(_config.TotalInputs + _config.TotalBarometers + deviceId, false);

                TestEventLogger.Write(_currentLotNumber, deviceId, "复位", "人工复位（报警解除）");
            }

            // 把缓存里这些台的状态改为空闲
            lock (_cacheLock)
            {
                foreach (int deviceId in deviceIds)
                {
                    if (_barometerDataCache.TryGetValue(deviceId, out BarometerData cached) && cached != null)
                    {
                        cached.Status = DeviceStatus.Idle;
                    }
                }
            }
        }

        /// <summary>
        /// 全部停止（急停）
        /// 【动作】关闭所有真空阀 + 断开所有载台上电 + 停止送风机 + 全部状态复位。
        /// 【安全意义】老化现场的一键兜底：发现异常立即切断全部输出。
        /// </summary>
        public void StopAll()
        {
            // 收集所有设备的输出点（全部阀 + 全部载台电 → OFF）
            var outputIds = new List<int>();
            var states = new List<bool>();
            for (int deviceId = 1; deviceId <= _config.TotalBarometers; deviceId++)
            {
                outputIds.Add(_config.TotalInputs + deviceId);                          // 真空阀 OFF
                states.Add(false);
                outputIds.Add(_config.TotalInputs + _config.TotalBarometers + deviceId); // 载台上电 OFF
                states.Add(false);
            }
            _ioController.WriteOutputs(outputIds.ToArray(), states.ToArray());

            // 全部状态复位
            lock (_stateLock)
            {
                for (int i = 0; i < _config.TotalBarometers; i++)
                {
                    _testingStates[i] = false;
                    _vacuumConfirmTimes[i] = DateTime.MinValue;
                    _testStartTimes[i] = DateTime.MinValue;
                    _testDurations[i] = 0;
                    _lastAlarmStates[i] = false;
                }
            }

            // 停止送风机（急停时也停）
            if (_fanController != null)
            {
                _fanRunning = false;
                _fanController.Stop();
            }

            TestEventLogger.Write(_currentLotNumber, 0, "急停", "全部停止（关闭所有阀与载台电）");
        }

        /// <summary>
        /// 获取每台是否正在测试（返回副本）
        /// </summary>
        public bool[] GetTestingStates()
        {
            lock (_stateLock)
            {
                return (bool[])_testingStates.Clone();
            }
        }

        /// <summary>
        /// 获取当前正在测试的台数
        /// </summary>
        public int GetTestingCount()
        {
            lock (_stateLock)
            {
                int count = 0;
                for (int i = 0; i < _testingStates.Length; i++)
                {
                    if (_testingStates[i]) count++;
                }
                return count;
            }
        }

        /// <summary>
        /// 获取当前"通讯在线"的台数
        /// 【判断规则】最近 <see cref="OnlineFreshnessSeconds"/> 秒内成功读到过数据的视为在线。
        /// </summary>
        public int GetOnlineCount()
        {
            lock (_stateLock)
            {
                int count = 0;
                DateTime now = DateTime.Now;
                for (int i = 0; i < _lastGoodTimes.Length; i++)
                {
                    if ((now - _lastGoodTimes[i]).TotalSeconds < OnlineFreshnessSeconds)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        /// <summary>
        /// 判断"在线"的时间窗（秒）
        /// 超过该时间没有成功读到数据，视为该台离线
        /// </summary>
        private const int OnlineFreshnessSeconds = 10;

        // =====================================================================
        // 数据采集（主循环）
        // =====================================================================

        /// <summary>
        /// 数据采集定时器触发事件
        /// 【防重入】用 Monitor.TryEnter 防止上一次采集未完成时重复进入
        /// </summary>
        private void CollectTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (_disposed) return;

            if (!Monitor.TryEnter(_collectLock))
            {
                return;
            }

            try
            {
                CollectData();
            }
            finally
            {
                Monitor.Exit(_collectLock);
            }
        }

        /// <summary>
        /// 执行数据采集（主循环）
        ///
        /// 【V1.10 改动】
        /// 1) 通讯失败不再简单 continue：连续失败达到阈值 → 触发"通讯故障报警"
        /// 2) 报警判定增强：压力越限 / 真空建立超时 / （可选）DI 报警触点
        /// 3) 测试中且未报警的台 → 状态置"测试中"
        /// 4) 老化计时：到时自动停止该台
        /// 5) 广播数据时，读失败的台用缓存数据并标 Fault（让面板显示报警而非旧值）
        /// </summary>
        private void CollectData()
        {
            try
            {
                // ===== IO 耦合器心跳 + 自动重连（V1.16 / V1.16.2）=====
                // 门禁解耦后，耦合器断开时每 5 秒在后台静默重连；已连接则本调用几乎无开销。
                TryReconnectIo();

                // ===== 气压表串口心跳 + 自动重连（V1.16.2）=====
                // 串口断开时 1 秒内感知并提示 + 后台静默重连；已连接则本调用几乎无开销。
                TryReconnectBarometer();

                // 读取所有气压表数据
                //（串口断开时返回"全 null 数组"，让下面的逐台循环继续累加失败次数、
                //   触发"通讯故障"关阀断电的安全兜底——见 ModbusRtuBarometerReader.ReadAllData）
                var allData = _barometerReader.ReadAllData();

                // 防御性检查（_config 未初始化时返回空数组 → 结束本轮）
                if (allData == null || allData.Length == 0) return;

                // 批量读取 IO 状态
                bool[] allInputs = _ioController.ReadAllInputs();
                bool[] allOutputs = _ioController.ReadAllOutputs();

                DateTime now = DateTime.Now;

                for (int i = 0; i < allData.Length; i++)
                {
                    int deviceId = i + 1;
                    BarometerData data = allData[i];

                    // ===== 1) 通讯状态判断（新增：传感器失联报警） =====
                    if (data == null)
                    {
                        // 该台读取失败：累加失败次数
                        bool triggerAlarm = false;
                        bool wasTesting = false;
                        lock (_stateLock)
                        {
                            _readFailCounts[i]++;
                            wasTesting = _testingStates[i];
                            // 连续失败达到阈值，且还没报过警 → 触发通讯故障报警
                            if (_readFailCounts[i] >= _config.CommunicationLossAlarmCount && !_lastAlarmStates[i])
                            {
                                _lastAlarmStates[i] = true;
                                _testingStates[i] = false;
                                triggerAlarm = true;
                            }
                        }

                        if (triggerAlarm)
                        {
                            if (wasTesting)
                            {
                                // 测试中的台失联：关阀 + 断电 + 标故障
                                //（失压未知，安全起见断电；面板显示报警色）
                                HandleAlarm(deviceId, "通讯故障（连续读取失败）");
                                lock (_cacheLock)
                                {
                                    if (_barometerDataCache.TryGetValue(deviceId, out BarometerData cached) && cached != null)
                                    {
                                        cached.Status = DeviceStatus.Fault;
                                    }
                                }
                            }
                            // 非测试中的台：不处理（输出本来就是安全关闭状态），
                            // 状态栏"在线 X/72"会如实反映离线台数
                        }
                        continue;
                    }

                    // 读到数据：清零失败计数，记录最后成功时间
                    lock (_stateLock)
                    {
                        _readFailCounts[i] = 0;
                        _lastGoodTimes[i] = now;
                    }

                    if (deviceId < 1 || deviceId > _config.TotalBarometers) continue;

                    // ===== 2) 回填输入状态（每个气压表 1 个输入：真空负压表报警触点） =====
                    if (allInputs != null && allInputs.Length >= _config.TotalInputs && deviceId <= _config.TotalInputs)
                    {
                        if (data.InputStatus == null || data.InputStatus.Length < 1) data.InputStatus = new bool[1];
                        data.InputStatus[0] = allInputs[deviceId - 1];
                    }

                    // ===== 3) 回填输出状态（每个气压表 2 个输出：真空电磁阀 + 载台上电） =====
                    if (allOutputs != null && allOutputs.Length >= _config.TotalOutputs)
                    {
                        if (data.OutputStatus == null || data.OutputStatus.Length < 2) data.OutputStatus = new bool[2];

                        int outputStart = _config.TotalInputs + 1;
                        int valveOutputId = _config.TotalInputs + deviceId;
                        int carrierOutputId = _config.TotalInputs + _config.TotalBarometers + deviceId;

                        int valveIndex = valveOutputId - outputStart;
                        int carrierIndex = carrierOutputId - outputStart;

                        if (valveIndex >= 0 && valveIndex < allOutputs.Length)
                        {
                            data.OutputStatus[0] = allOutputs[valveIndex];
                        }
                        if (carrierIndex >= 0 && carrierIndex < allOutputs.Length)
                        {
                            data.OutputStatus[1] = allOutputs[carrierIndex];
                        }
                    }

                    // ===== 3.5) 叠加工位静态信息（【V1.19.11 新增】） =====
                    // 真实气压表只上报压力，SN / 配方 / 延时需由上位机维护
                    // （ID 绑定扫码/手动录入 SN、工位设置窗口录入配方/延时）。
                    // 这里把 _stationInfo 里存的静态信息覆盖到采集数据上，
                    // 使工位面板的 SN / 配方 / 延时显示与绑定/设置保持关联一致。
                    ApplyStationInfo(data);

                    // ===== 4) 报警判定（增强版） =====
                    bool isTesting;
                    lock (_stateLock)
                    {
                        isTesting = _testingStates[deviceId - 1];
                    }

                    // 【业务修正】只有"正在测试"的台才做压力报警判定。
                    // 原因：未测试的台（阀关着）压力是常压（接近 0kPa），
                    // 按"压力 > 阈值"判定必然越限，但这是正常状态，不是报警。
                    // 报警联动（关阀+断电）只对测试中的台有意义。
                    bool isAlarm = false;
                    if (isTesting)
                    {
                        isAlarm = IsAlarm(data);

                        lock (_stateLock)
                        {
                            if (isAlarm && !_lastAlarmStates[deviceId - 1])
                            {
                                // 进入报警边沿：执行一次联动输出
                                _lastAlarmStates[deviceId - 1] = true;
                                _testingStates[deviceId - 1] = false;
                                HandleAlarm(deviceId, GetAlarmReason(data));
                            }
                            _lastAlarmStates[deviceId - 1] = isAlarm;
                        }
                    }

                    if (isAlarm)
                    {
                        data.Status = DeviceStatus.Fault;
                    }
                    else if (isTesting)
                    {
                        data.Status = DeviceStatus.Testing;
                    }
                    else
                    {
                        // 非测试的台：覆盖读取器对常压（接近 0kPa）的"压力越限"误判，
                        // 恢复为空闲（读取器仅做基础报警提示，真正判定以本处业务逻辑为准）
                        data.Status = DeviceStatus.Idle;
                    }

                    // ===== 5) 真空建立确认 + 老化计时 =====
                    if (isTesting && !isAlarm)
                    {
                        ProcessTestingProgress(deviceId, data);
                    }
                }

                // ===== 6) 送风机生命周期联动（首台启动 / 末台停止） =====
                UpdateFanLifecycle();

                // ===== 7) 批量更新缓存 =====
                lock (_cacheLock)
                {
                    foreach (var data in allData)
                    {
                        if (data != null)
                        {
                            _barometerDataCache[data.DeviceId] = data;
                        }
                    }
                }

                // ===== 8) 组装广播数据 =====
                // 读失败的台，用缓存数据并保持 Fault 状态，让面板显示报警而不是停在上次旧值
                var broadcastData = new BarometerData[allData.Length];
                for (int i = 0; i < allData.Length; i++)
                {
                    if (allData[i] != null)
                    {
                        broadcastData[i] = allData[i];
                    }
                    else
                    {
                        lock (_cacheLock)
                        {
                            if (_barometerDataCache.TryGetValue(i + 1, out BarometerData cached))
                            {
                                broadcastData[i] = cached?.Clone();
                            }
                        }
                    }
                }

                // 触发批量数据更新事件（一次采集只触发一次）
                OnBatchDataUpdated?.Invoke(this, broadcastData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"数据采集失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理测试中的进度（真空确认 + 老化计时）
        /// 仅在"测试中且未报警"时调用
        /// </summary>
        private void ProcessTestingProgress(int deviceId, BarometerData data)
        {
            bool inRange = !PressureOutOfRange(data.VacuumPressure);

            lock (_stateLock)
            {
                // ---- 真空建立确认 ----
                // _vacuumConfirmTimes != MinValue 表示仍在"确认宽限窗口"内
                if (_vacuumConfirmTimes[deviceId - 1] != DateTime.MinValue)
                {
                    if (inRange)
                    {
                        // 压力进入正常区间 → 真空建立成功，确认完成
                        _vacuumConfirmTimes[deviceId - 1] = DateTime.MinValue;
                        // 从此刻开始老化计时
                        _testStartTimes[deviceId - 1] = DateTime.Now;

                        TestEventLogger.Write(_currentLotNumber, deviceId, "真空建立",
                            $"真空已建立: {data.VacuumPressure} kPa");
                    }
                    return; // 还在确认窗口内，不检查计时
                }

                // ---- 老化计时 ----
                int duration = _testDurations[deviceId - 1];
                if (duration <= 0) return;   // 0 = 不限时长
                DateTime start = _testStartTimes[deviceId - 1];
                if (start == DateTime.MinValue) return;

                if ((DateTime.Now - start).TotalSeconds >= duration)
                {
                    // 老化时长到 → 自动停止该台
                    // 注意：不能在锁内调用 StopDevice（StopDevice 会再取锁），先标记再解锁
                    _testingStates[deviceId - 1] = false;
                    StopDeviceInternal(deviceId, "老化时长到");
                }
            }
        }

        /// <summary>
        /// 停止单台（内部方法，供老化到时自动停止调用）
        /// 【注意】本方法会访问 _ioController（通讯）和写日志，
        /// 必须在 _stateLock 锁外调用（避免持锁做网络 IO）。
        /// </summary>
        /// <param name="deviceId">设备编号</param>
        /// <param name="reason">停止原因</param>
        private void StopDeviceInternal(int deviceId, string reason)
        {
            // 关阀 + 断电
            _ioController.WriteOutput(_config.TotalInputs + deviceId, false);
            _ioController.WriteOutput(_config.TotalInputs + _config.TotalBarometers + deviceId, false);

            // 复位该台状态（在锁内）
            lock (_stateLock)
            {
                _testingStates[deviceId - 1] = false;
                _vacuumConfirmTimes[deviceId - 1] = DateTime.MinValue;
                _testStartTimes[deviceId - 1] = DateTime.MinValue;
                _testDurations[deviceId - 1] = 0;
            }

            // 记录日志
            TestEventLogger.Write(_currentLotNumber, deviceId, "完成", reason);

            // 缓存里该台置为空闲（不覆盖 Fault）
            lock (_cacheLock)
            {
                if (_barometerDataCache.TryGetValue(deviceId, out BarometerData cached) && cached != null && cached.Status != DeviceStatus.Fault)
                {
                    cached.Status = DeviceStatus.Idle;
                }
            }
        }

        /// <summary>
        /// 报警联动（关阀 + 断电 + 记日志）
        /// 【设计说明】
        /// - 只在进入报警的边沿触发一次（调用方保证）
        /// - 报警后不自动恢复，需要人工复位（ResetDevices）后再重新测试
        /// </summary>
        /// <param name="deviceId">设备编号</param>
        /// <param name="reason">报警原因描述</param>
        private void HandleAlarm(int deviceId, string reason)
        {
            int valveOutputId = _config.TotalInputs + deviceId;
            int carrierOutputId = _config.TotalInputs + _config.TotalBarometers + deviceId;

            // 报警联动动作：
            // - 关真空阀（防止继续抽真空/泄漏等异常扩大）
            // - 断载台上电（保护被测件/治具）
            _ioController.WriteOutput(valveOutputId, false);
            _ioController.WriteOutput(carrierOutputId, false);

            // 记录报警事件（供追溯）
            TestEventLogger.Write(_currentLotNumber, deviceId, "报警", reason,
                pressureKPa: GetDeviceLastPressure(deviceId));
        }

        /// <summary>
        /// 获取某台最近一次的压力值（用于报警日志），无缓存返回 null
        /// </summary>
        private decimal? GetDeviceLastPressure(int deviceId)
        {
            lock (_cacheLock)
            {
                if (_barometerDataCache.TryGetValue(deviceId, out BarometerData cached) && cached != null)
                {
                    return cached.VacuumPressure;
                }
            }
            return null;
        }

        /// <summary>
        /// 压力是否越限（失压 / 超抽）
        ///
        /// 【判定规则】（单位 kPa，与气压表读数一致，V1.19.9 由 Pa 改为 kPa）
        /// - AlarmWhenPressureHigherThanThreshold=true（默认）：压力 > 阈值 → 报警
        ///   真空压力为负，数值越大（越接近 0）真空越差，触发失压报警
        /// - false：压力 &lt; 阈值 → 报警（扩展用）
        /// </summary>
        private bool PressureOutOfRange(decimal pressureKPa)
        {
            if (_config.AlarmWhenPressureHigherThanThreshold)
            {
                return pressureKPa > _config.AlarmPressureThresholdKPa;
            }
            return pressureKPa < _config.AlarmPressureThresholdKPa;
        }

        /// <summary>
        /// 综合报警判定（【V1.10 增强】）
        ///
        /// 报警来源：
        /// 1) 压力越限（失压 / 超抽）
        /// 2) 真空建立超时：测试中且在确认宽限窗口内超时，压力仍未进入正常区间
        /// 3) （可选，配置 UseDiAlarmContact）气压表硬件报警触点（DI）触发
        ///
        /// 【真空确认宽限说明】
        /// 刚开阀时压力还接近常压，处于"越限"状态是正常的（真空需要时间建立）。
        /// 所以在确认窗口内不按压力报警；超时还没建立才报警。
        /// </summary>
        private bool IsAlarm(BarometerData data)
        {
            if (data == null) return true; // 空数据视为异常（正常路径不会到这里）

            int idx = data.DeviceId - 1;

            // 读取测试状态
            bool isTesting = false;
            bool inConfirmWindow = false;
            bool confirmExpired = false;
            lock (_stateLock)
            {
                isTesting = _testingStates[idx];
                if (isTesting)
                {
                    DateTime confirmStart = _vacuumConfirmTimes[idx];
                    inConfirmWindow = (confirmStart != DateTime.MinValue);
                    if (inConfirmWindow &&
                        (DateTime.Now - confirmStart).TotalMilliseconds >= _config.VacuumConfirmTimeoutMs)
                    {
                        confirmExpired = true; // 确认窗口超时
                    }
                }
            }

            // 压力越限判断
            bool pressureAlarm = PressureOutOfRange(data.VacuumPressure);

            // ===== 真空确认宽限窗口 =====
            // 刚开阀时压力还接近常压（越限是正常的，真空需要时间建立），
            // 所以在窗口内【不按压力报警】，给真空建立留时间。
            // 只有在"窗口超时 + 压力仍未进入正常区间"时才判定真空建立失败报警。
            if (isTesting && inConfirmWindow)
            {
                if (confirmExpired && pressureAlarm)
                {
                    return true; // 确认窗口超时且真空始终未建立 → 报警
                }
                return false;    // 窗口内：暂不报警（压力进入正常区间后由 ProcessTestingProgress 确认）
            }

            // 1) 压力越限（已确认真空的测试中，或非测试状态的设备）
            if (pressureAlarm) return true;

            // 2) DI 报警触点（可选，需现场确认触点电平后开启）
            if (_config.UseDiAlarmContact &&
                data.InputStatus != null && data.InputStatus.Length >= 1 &&
                data.InputStatus[0])
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 生成报警原因描述（用于日志 / 显示）
        /// </summary>
        private string GetAlarmReason(BarometerData data)
        {
            if (data == null) return "通讯故障";
            return $"真空压力越限: {data.VacuumPressure} kPa（阈值 {_config.AlarmPressureThresholdKPa} kPa）";
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源的实际实现
        /// 【V1.10】补上送风机的退订 / 断开 / 定时器释放（均 null 判空）
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                if (disposing)
                {
                    Stop();

                    // 取消事件订阅（避免内存泄漏）
                    _barometerReader.OnError -= BarometerReader_OnError;
                    _ioController.OnError -= IoController_OnError;
                    if (_fanController != null)
                    {
                        _fanController.OnError -= FanController_OnError;
                        _fanController.Disconnect();
                    }

                    // 释放定时器
                    _collectTimer?.Dispose();
                    _fanTimer?.Stop();
                    _fanTimer?.Dispose();
                }
            }
        }
    }
}
