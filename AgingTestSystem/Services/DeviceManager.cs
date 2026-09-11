using System;
using System.Collections.Generic;
using System.Threading;
using System.Timers;
using AgingTestSystem.Interfaces;
using AgingTestSystem.Models;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 设备管理器
    /// 负责管理所有气压表、IO 设备、冷却送风机的连接、数据采集和业务状态更新。
    /// 是整个系统的核心服务类。
    ///
    /// 【V1.59 业务串联完善：三阶段状态机 + 结果判定 + 断电恢复】
    /// 1) 时序安全改造：启动只开真空阀，载台上电由采集循环在「真空到位 + 延时开启到」
    ///    时补发——落实"未吸附固定不通电"（旧版开阀+上电同时下发，与安全意图矛盾）；
    /// 2) 配方参数接入编排：老化时长 = 工位配方"启动时间(StartTime)"（回退全局
    ///    MaxTestDurationSeconds）；上电前置等待 = 配方"延时开启(DelayTime)"；
    ///    到位/报警阈值 = 配方负压值（回退全局 AlarmPressureThresholdKPa）。
    ///    参数在启动瞬间定格，中途改配置不影响进行中的测试；
    /// 3) 完成语义：到时自动下电关阀 → Completed·待取料(PASS)；报警按责任分类
    ///    （压力类=产品 FAIL / 通讯失联=设备异常）；手动停止=中止回空闲；
    /// 4) 断电恢复：在测任务快照持久化（TestSession.json），重启后询问
    ///    恢复（整台重测，参数用快照定格值）/ 放弃（安全关闭阀与电源）。
    ///
    /// 【V1.10 新增业务串联】（历史记录：以下3)/5)两条已被V1.59覆盖，看现行逻辑请看上面V1.59小节）
    /// 在原有"采集 + 报警联动"基础上，把老化测试业务流程串起来：
    /// 1) 冷却送风机接入（接口化：真实 / Mock），独立定时器轮询，不阻塞 72 台气压表采集
    /// 2) 送风机生命周期全局化：送风机是 72 台共用的环境设备，
    ///    "首台开始测试时启动送风机，最后一台停止时才停止送风机"
    /// 3) 测试状态机（V1.59前旧行为：启动同时开阀+上电；现行V1.59启动只开阀，上电由真空到位+延时到补发）：
    ///    启动运行 = 开真空 + 载台上电 + 标测试中；
    ///    真空建立确认（开阀后 N 毫秒内压力必须进入正常区间，否则按真空失败报警）
    /// 4) 通讯故障报警：某台气压表连续读失败 N 次 → 视为失联报警（关阀+断电+标故障），
    ///    避免"断线后停留在旧压力值上假正常"
    /// 5) 老化计时（V1.59前旧行为：只看全局MaxTestDurationSeconds做自动停止；
    ///    现行V1.59配方StartTime优先、全局兜底，到时自动完成标Completed·待取料PASS，不限时长=0时永不自动完成）：
    ///    到达 MaxTestDurationSeconds 自动停止该台（关阀+断电+记日志）
    /// 6) 人工复位：报警/故障台复位回到空闲，可重新启动（V1.59扩展：已完成·待取料同样走复位确认取件）
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
        /// 【V1.67】每台"失压保持运行"是否已记过事件（AgingPressureLossPolicy=KeepRunning 时用）。
        /// 只在"正常 → 失压"的边沿记一条日志，不每轮刷屏；回到正常/报警/复位/启动时清零。
        /// </summary>
        private readonly bool[] _lossNoted;

        /// <summary>
        /// MES 上报器（【V1.68 新增】二期；构造时建、Dispose 时释放；开关关闭时零开销）。
        /// </summary>
        private readonly MesReporter _mes;

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

        // =====================================================================
        // 【V1.59 新增】三阶段老化状态机 + 任务定格参数 + 断电恢复持久化
        // 时序：启动(只开阀) → Vacuuming(等真空到位+延时开启) → 上电 → Aging(配方时长)
        //       → 到时自动完成(PASS·待取料)。报警按责任分产品 FAIL / 设备异常。
        // =====================================================================

        /// <summary>
        /// 每台当前所处的老化子阶段（None/Vacuuming/Aging）
        /// 【为什么需要子阶段】原来"测试中"一个 bool 无法表达"阀开了但还没上电"，
        /// 而安全规则恰恰要求这个区分：Vacuuming 阶段载台必须断电，
        /// 真空建立失败就永远不该带电。
        /// </summary>
        private readonly AgingPhase[] _testPhases;

        /// <summary>
        /// 每台本次测试的开阀时刻（延时开启与真空确认超时的计时起点 t0）
        /// </summary>
        private readonly DateTime[] _valveOpenTimes;

        /// <summary>
        /// 每台本次任务的"定格参数"——启动测试瞬间从工位配方/全局配置抄写一份：
        /// 老化时长（秒，0=不限）、延时开启（秒）、报警阈值（kPa）。
        /// 【为什么要定格】测试跑几小时的中途操作员可能改配置/换配方，
        /// 进行中的任务必须用启动那一刻的参数跑完，否则同一批产品工艺不一致，
        /// 质量数据没法追溯。
        /// 【V1.62 约定】无任务时三者恒为"清零态"（时长/延时=0、阈值=全局值）：
        /// 所有清理点（停止/复位/急停/完成/报警）清时长延时的同时必须把阈值同步回全局，
        /// 否则残留的旧阈值会让后人误以为"退出测试还有定格生效"。
        /// </summary>
        private readonly int[] _sessionDurationSecs;
        private readonly int[] _sessionDelaySecs;
        private readonly decimal[] _sessionThresholdKPa;

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

        // =====================================================================
        // IO 触发后快速跟踪刷新（【V1.30 新增】）
        // 触发 IO（开/关真空阀、上/断电、启动/停止测试）后，对目标工位启动
        // 独立高频补读定时器（250ms/次），压力变化 ≤0.5 秒可见，
        // 不拖慢 72 台全量轮询链路；跟踪窗口到期自动退出恢复正常轮询。
        // =====================================================================

        /// <summary>快速跟踪补读间隔（毫秒）</summary>
        private const int QuickTrackIntervalMs = 250;

        /// <summary>单次快速跟踪最长窗口（毫秒，覆盖真空建立 15s 内从常压抽到目标负压）</summary>
        private const int QuickTrackWindowMs = 12000;

        /// <summary>正在快速跟踪的工位集合（受 <see cref="_quickTrackLock"/> 保护）</summary>
        private readonly HashSet<int> _quickTrackSet = new HashSet<int>();

        /// <summary>保护快速跟踪集合的锁</summary>
        private readonly object _quickTrackLock = new object();

        /// <summary>快速跟踪高频补读定时器（AutoReset=true，仅在集合非空时运行）</summary>
        private readonly System.Timers.Timer _quickTrackTimer;

        /// <summary>本批快速跟踪开始时间（用于窗口到期自动退出）</summary>
        private DateTime _quickTrackStart;

        /// <summary>
        /// 批量数据更新事件（一次采集周期触发一次，参数为本次采集的所有数据）
        /// 【注意】在后台线程触发，UI 层需用 BeginInvoke 切到 UI 线程
        /// </summary>
        public event EventHandler<BarometerData[]> OnBatchDataUpdated;

        /// <summary>
        /// 单台快速跟踪增量更新事件（【V1.30 新增】）
        /// IO 触发后高频补读指定工位，每读到一次触发一次（参数为该工位最新数据）。
        /// 【注意】在后台线程触发，UI 层需用 BeginInvoke 切到 UI 线程更新对应面板。
        /// </summary>
        public event EventHandler<BarometerData> OnQuickTrackDataUpdated;

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
        /// 设备配置（只读引用）。
        /// 【用途】各窗体（通讯测试 / 送风机测试 / 系统设置）读取连接参数与备用映射配置时，
        /// 统一通过本属性拿配置，与主窗体共用同一实例，保证"一台设备一条连接"口径一致。
        /// </summary>
        public DeviceConfig Config => _config;

        /// <summary>
        /// IO 耦合器当前是否已连接（【V1.16.1 新增】）
        /// 顶部"通讯连接状态"标签的数据源：只反映耦合器（阀 / 载台电控制）是否连通。
        /// 后台读/写失败时会自动置 false，TryReconnectIo 自动重连成功后置 true。
        /// </summary>
        public bool IsIoConnected => _ioController.IsConnected;

        /// <summary>
        /// 初始化设备管理器（生产入口：按 UseMockCommunication 自动选择 Mock/真实实现）
        /// </summary>
        public DeviceManager(DeviceConfig config) : this(config, null, null, null)
        {
        }

        /// <summary>
        /// 初始化设备管理器（【V1.59】依赖注入构造——回归集成测试专用）
        ///
        /// 【为什么开放注入】三阶段老化状态机的行为验证需要"可控的压力序列 +
        /// 可记录的输出调用"（Fake 设备），而本类原来在构造函数里硬编码 new
        /// Mock/Real 实现，外部无法替身。开放此重载后：
        /// - 生产路径走无参设备版本（行为完全不变）；
        /// - 测试 harness 传入 Fake 实现即可端到端驱动状态机
        ///   （启动→抽真空→上电→计时→完成/报警/恢复）。
        /// 任一参数传 null 时按原逻辑自动创建（config 决定 Mock 或真实实现）。
        /// </summary>
        public DeviceManager(DeviceConfig config,
            IBarometerReader barometerReaderOverride,
            IIoController ioControllerOverride,
            IFanController fanControllerOverride)
        {
            _config = config;

            // 初始化硬件接口实现（真实 / Mock 二选一，由 App.config 的 UseMockCommunication 决定；
            // 测试可通过 override 参数注入 Fake 实现）
            if (barometerReaderOverride != null)
            {
                _barometerReader = barometerReaderOverride;
            }
            else if (_config.UseMockCommunication)
            {
                _barometerReader = new MockBarometerReader();
            }
            else
            {
                _barometerReader = new ModbusRtuBarometerReader();
            }

            if (ioControllerOverride != null)
            {
                _ioController = ioControllerOverride;
            }
            else if (_config.UseMockCommunication)
            {
                _ioController = new MockIoController();
            }
            else
            {
                _ioController = new ModbusTcpIoController();
            }

            if (fanControllerOverride != null)
            {
                _fanController = fanControllerOverride;
            }
            else if (_config.UseMockCommunication)
            {
                // 送风机 Mock：只有启用送风机时才创建
                _fanController = _config.FanEnabled ? new MockFanController() : null;
            }
            else
            {
                // 送风机真实实现：只有启用送风机时才创建
                _fanController = _config.FanEnabled ? new FanControllerClient() : null;
            }

            // 初始化状态数组（按气压表总数）
            _lastAlarmStates = new bool[_config.TotalBarometers];
            _lossNoted = new bool[_config.TotalBarometers];
            _testingStates = new bool[_config.TotalBarometers];
            _testStartTimes = new DateTime[_config.TotalBarometers];
            _testDurations = new int[_config.TotalBarometers];
            _vacuumConfirmTimes = new DateTime[_config.TotalBarometers];
            _readFailCounts = new int[_config.TotalBarometers];
            _lastGoodTimes = new DateTime[_config.TotalBarometers];

            // 【V1.59】三阶段状态机数组（None=全部未在测试；定格参数先清零）
            _testPhases = new AgingPhase[_config.TotalBarometers];
            _valveOpenTimes = new DateTime[_config.TotalBarometers];
            _sessionDurationSecs = new int[_config.TotalBarometers];
            _sessionDelaySecs = new int[_config.TotalBarometers];
            _sessionThresholdKPa = new decimal[_config.TotalBarometers];

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

            // IO 触发后快速跟踪定时器（【V1.30 新增】）
            // 初始不启动，仅在 StartQuickTracking 加入工位后运行；集合空时自动停止。
            _quickTrackTimer = new System.Timers.Timer(QuickTrackIntervalMs);
            _quickTrackTimer.Elapsed += QuickTrackTimer_Elapsed;
            _quickTrackTimer.AutoReset = true;

            // 送风机独立轮询定时器（只有启用时才创建）
            if (_config.FanEnabled)
            {
                _fanTimer = new System.Timers.Timer(FanPollIntervalMs);
                _fanTimer.Elapsed += FanTimer_Elapsed;
                _fanTimer.AutoReset = true;
            }

            // 【V1.68】MES 上报器（持 _config 引用读最新配置；MesEnabled=false 时
            // Report 直接返回、worker 懒建，零开销；Dispose 时释放后台线程）
            _mes = new MesReporter(_config);
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
        /// 按最新配置重连气压表串口（系统设置保存后热生效）
        /// Connect 内部会先断开旧串口，再按新参数（端口 / 波特率 / 超时等）重新连接。
        /// 【注意】应在后台线程调用（打开串口 + 建 Modbus 主站耗时）。
        /// </summary>
        public void ReconnectBarometerReader()
        {
            try
            {
                bool ok = _barometerReader.Connect(_config);
                if (ok)
                {
                    _barometerWasConnected = true;
                    Diagnostic($"气压表串口已按新配置重连：{(_barometerReader.CurrentPortName ?? _config.PortName)}");
                }
                else
                {
                    Diagnostic("气压表串口重连失败（新配置端口不可用），请检查串口参数");
                }
            }
            catch (Exception ex)
            {
                Diagnostic($"气压表串口重连异常：{ex.Message}");
            }
        }

        /// <summary>
        /// 按最新配置强制重连 IO 耦合器（系统设置保存后热生效）
        /// 与 <see cref="EnsureIoConnected"/> 不同：即使当前已连接也先断开，
        /// 用新 IP / 端口 / 超时重新连接。
        /// 【注意】应在后台线程调用（TCP 连接超时最长约 3 秒）。
        /// </summary>
        public void ReconnectIo()
        {
            try
            {
                _ioController.Disconnect();
                bool ok = _ioController.Connect(_config);
                _lastIoConnected = ok;
                OnConnectionStatusChanged?.Invoke(this, ok);
                Diagnostic(ok
                    ? $"IO耦合器已按新配置重连：{_config.PlcAddress}:{_config.PlcPort}"
                    : $"IO耦合器重连失败：{_config.PlcAddress}:{_config.PlcPort}，后台将自动重试");
            }
            catch (Exception ex)
            {
                Diagnostic($"IO耦合器重连异常：{ex.Message}");
            }
        }

        /// <summary>
        /// 按最新配置强制重连送风机（系统设置保存后热生效）
        /// 与 <see cref="ReconnectFan"/> 不同：即使已连接也先断开，
        /// 用新 IP / 端口 / 候选列表重新连接。
        /// 【注意】应在后台线程调用。
        /// </summary>
        public void ForceReconnectFan()
        {
            if (_fanController == null) return;
            try
            {
                _fanController.Disconnect();
                bool ok = _fanController.ReconnectNow();
                Diagnostic(ok
                    ? $"冷却送风机已按新配置重连：{(_fanController.ActiveIp ?? _config.FanIpAddress)}:{_config.FanPort}"
                    : "冷却送风机重连失败（新配置 IP 不可用），请检查 IP/网线");
            }
            catch (Exception ex)
            {
                Diagnostic($"冷却送风机重连异常：{ex.Message}");
            }
        }

        /// <summary>
        /// 更新主采集定时器间隔（系统设置保存 CollectInterval 后热生效）
        /// </summary>
        public void UpdateCollectInterval(int intervalMs)
        {
            _collectTimer.Interval = intervalMs;
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
            bool rebindOnCompleted = false;
            lock (_stationInfoLock)
            {
                if (!_stationInfo.TryGetValue(deviceId, out StationInfo info))
                {
                    info = new StationInfo { DeviceId = deviceId };
                    _stationInfo[deviceId] = info;
                }
                info.SerialNumber = sn;
            }

            // 【V1.59】重新扫码绑定 = 新产品上料：若该台还处于"已完成·待取料"，
            // 自动复位回空闲，操作员不用再手动点复位（流水式作业少一步操作）。
            // 报警(Fault)态不自动清——漏气原因没确认前上新产品有风险，仍需人工复位。
            lock (_cacheLock)
            {
                if (_barometerDataCache.TryGetValue(deviceId, out BarometerData cached)
                    && cached != null && cached.Status == DeviceStatus.Completed)
                {
                    rebindOnCompleted = true;
                }
            }
            if (rebindOnCompleted)
            {
                ResetDevices(new[] { deviceId });
                TestEventLogger.Write(_currentLotNumber, deviceId, "复位", "重新扫码绑定，完成态自动复位");
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
            SetStationRecipe(deviceId, recipeName, null, null);
        }

        /// <summary>
        /// 设置指定工位的配方名称 + 负压值 + 显示模式（【V1.59 新增，V1.66 加显示模式】）
        ///
        /// 【为什么要把负压值一起存】配方的 NegativePressure 是该产品的真空工艺要求，
        /// 启动测试时要用它做"到位判定/报警阈值"。原来只存配方名字符串，
        /// 编排层拿不到数值，导致配方参数与实际判定脱节。
        /// negativePressure 传 null = 不修改已有值（兼容只改名不改工艺的调用）。
        /// 【V1.66】显示模式同理：配方 DisplayMode 是"这次烧什么画面"的追溯依据，
        /// 与配方名同步下发、同步清空；displayMode 传 null = 不修改已有值。
        /// </summary>
        /// <param name="deviceId">工位编号（1 ~ TotalBarometers）</param>
        /// <param name="recipeName">配方名称（空白视为清空）</param>
        /// <param name="negativePressure">配方负压值（kPa）；null = 保持不变</param>
        /// <param name="displayMode">配方显示模式；null = 保持不变</param>
        public void SetStationRecipe(int deviceId, string recipeName, decimal? negativePressure, string displayMode)
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
                if (negativePressure.HasValue)
                {
                    info.RecipeNegativePressure = negativePressure.Value;
                }
                if (displayMode != null)
                {
                    info.DisplayMode = displayMode.Trim();
                }
                // 清空配方名时把负压值与显示模式一并清掉，避免残留旧工艺
                if (string.IsNullOrEmpty(name))
                {
                    info.RecipeNegativePressure = null;
                    info.DisplayMode = null;
                }
            }
        }

        /// <summary>
        /// 设置指定工位的延时时间（【V1.19.11 新增】）
        /// 由工位设置窗口保存按钮调用。写入后采集叠加显示在工位面板延时标签上。
        /// </summary>
        /// <param name="deviceId">工位编号（1 ~ TotalBarometers）</param>
        /// <param name="delayTime">延时时间（配方窗口"延时时间"，工位面板"延时开启"；可空 = 不修改）</param>
        /// <param name="startTime">启动时间（配方窗口"启动时间"，工位面板"延时到达"；可空 = 不修改）</param>
        public void SetStationDelayTimes(int deviceId, TimeSpan? delayTime, TimeSpan? startTime)
        {
            if (deviceId < 1 || deviceId > _config.TotalBarometers) return;
            lock (_stationInfoLock)
            {
                if (!_stationInfo.TryGetValue(deviceId, out StationInfo info))
                {
                    info = new StationInfo { DeviceId = deviceId };
                    _stationInfo[deviceId] = info;
                }
                if (delayTime.HasValue) info.DelayTime = delayTime;
                if (startTime.HasValue) info.StartTime = startTime;
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

            // 先记录哪些台处于"已完成·待取料"（重新扫码 = 新产品上料，自动复位）
            var completedIds = new List<int>();
            lock (_cacheLock)
            {
                foreach (int deviceId in serialNumbers.Keys)
                {
                    if (deviceId < 1 || deviceId > _config.TotalBarometers) continue;
                    if (_barometerDataCache.TryGetValue(deviceId, out BarometerData cached)
                        && cached != null && cached.Status == DeviceStatus.Completed)
                    {
                        completedIds.Add(deviceId);
                    }
                }
            }

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

            // 【V1.59】对"重绑的已完成台"逐台复位（与单台绑定行为一致）
            if (completedIds.Count > 0)
            {
                ResetDevices(completedIds.ToArray());
                TestEventLogger.Write(_currentLotNumber, 0, "复位",
                    $"批量扫码重绑，{completedIds.Count} 个完成态工位自动复位");
            }
        }

        /// <summary>
        /// 把工位静态信息（SN / 配方 / 延时）叠加到采集数据上（【V1.19.11 新增】）
        ///
        /// 【为什么需要叠加】
        /// 真实气压表只上报压力，BarometerData 的 SerialNumber / RecipeName /
        /// DelayTime / StartTime 在采集层是空的。
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
            // 【V1.66】显示模式与配方名同步叠加：配了才写，空=没配不过来
            if (!string.IsNullOrEmpty(info.DisplayMode))
            {
                data.DisplayMode = info.DisplayMode;
            }
            if (info.DelayTime.HasValue)
            {
                data.DelayTime = info.DelayTime.Value;
            }
            if (info.StartTime.HasValue)
            {
                data.StartTime = info.StartTime.Value;
            }
        }

        /// <summary>
        /// 写入单台气压表的设备阈值（透传 IBarometerReader.SetThreshold）
        ///
        /// 【单位提醒】thresholdValue 是"设备单位"（与压力读数同单位同小数位），
        /// 不是软件报警阈值 AlarmPressureThresholdKPa（kPa）。写前务必确认设备单位。
        /// </summary>
        /// <param name="deviceId">气压表编号（1~TotalBarometers）</param>
        /// <param name="thresholdValue">设备单位阈值（如 -5.0）</param>
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
        /// 【单位】thresholdKPa 单位是 kPa（与气压表读数同单位，如 -5）。
        /// </summary>
        /// <param name="thresholdKPa">报警压力阈值（kPa，如 -5）</param>
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

            // 【V1.30】IO 触发后快速跟踪：写输出成功即对目标工位高频补读，
            // 压力变化 ≤0.5 秒内反映到面板（非阀/载台电输出点自动忽略）。
            if (TryGetDeviceIdFromOutput(outputId, out int deviceId))
            {
                StartQuickTracking(new[] { deviceId });
            }
        }

        // =====================================================================
        // IO 触发后快速跟踪（【V1.30 新增】）
        // 触发 IO 后，目标工位进入快速跟踪集合，由独立高频定时器（250ms）
        // 只补读这几台压力 + IO 状态，并广播 OnQuickTrackDataUpdated 刷新对应面板。
        // 窗口到期（QuickTrackWindowMs）自动清空集合、停止定时器，恢复正常轮询。
        // =====================================================================

        /// <summary>
        /// 启动 IO 触发后的快速跟踪
        /// 把指定工位加入快速跟踪集合并启动高频补读定时器；集合已有该工位则幂等。
        /// 有新工位加入时重置窗口计时，保证最近一次触发也能看满窗口。
        /// </summary>
        /// <param name="deviceIds">要快速跟踪的工位编号数组</param>
        public void StartQuickTracking(int[] deviceIds)
        {
            if (deviceIds == null || deviceIds.Length == 0) return;
            if (_disposed) return;

            lock (_quickTrackLock)
            {
                foreach (int deviceId in deviceIds)
                {
                    if (deviceId < 1 || deviceId > _config.TotalBarometers) continue;
                    _quickTrackSet.Add(deviceId);
                }

                if (_quickTrackSet.Count == 0) return;

                // 刷新窗口起点：有新工位加入就重新计时，保证最近一次触发也能看满窗口
                _quickTrackStart = DateTime.Now;
                if (!_quickTrackTimer.Enabled)
                {
                    _quickTrackTimer.Start();
                }
            }

            System.Diagnostics.Debug.WriteLine($"[快速跟踪] 开始跟踪 {_quickTrackSet.Count} 台");
        }

        /// <summary>
        /// 快速跟踪定时器触发
        /// 对集合内每台：读单台压力 + 补齐 IO 输出状态 → 广播增量事件刷新对应面板。
        /// 窗口到期后清空集合并停止定时器，恢复正常全量轮询。
        /// 【防重入】用 Monitor.TryEnter 防止上一次补读未完成时重复进入（参照 _collectLock 模式）。
        /// </summary>
        private void QuickTrackTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (_disposed) return;

            if (!Monitor.TryEnter(_quickTrackLock))
            {
                return; // 上一次补读未完成，跳过本次
            }

            try
            {
                if (_quickTrackSet.Count == 0)
                {
                    _quickTrackTimer.Stop();
                    return;
                }

                // 窗口到期：结束本次快速跟踪，恢复正常轮询
                if ((DateTime.Now - _quickTrackStart).TotalMilliseconds > QuickTrackWindowMs)
                {
                    _quickTrackSet.Clear();
                    _quickTrackTimer.Stop();
                    System.Diagnostics.Debug.WriteLine("[快速跟踪] 窗口到期，恢复正常轮询");
                    return;
                }

                // 读全量 DO 一次，用于补齐真实模式下读取器不返回的输出状态
                bool[] allOutputs = _ioController.ReadAllOutputs();

                // 全程持有 _quickTrackLock，集合不会被 StartQuickTracking 并发修改，直接遍历安全
                foreach (int deviceId in _quickTrackSet)
                {
                    BarometerData data = _barometerReader.ReadData(deviceId);
                    if (data == null) continue; // 单台读失败跳过，不中断其它台

                    // 补齐 IO 输出状态（真空阀 + 载台上电）；Mock 读取器自带则跳过
                    if (data.OutputStatus == null || data.OutputStatus.Length < 2)
                    {
                        ApplyQuickTrackOutputStatus(data, deviceId, allOutputs);
                    }

                    // 测试中的台状态修正为 Testing（与 CollectData 判定一致）
                    lock (_stateLock)
                    {
                        if (_testingStates[deviceId - 1])
                        {
                            data.Status = DeviceStatus.Testing;
                        }
                    }

                    OnQuickTrackDataUpdated?.Invoke(this, data);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"快速跟踪补读失败: {ex.Message}");
            }
            finally
            {
                Monitor.Exit(_quickTrackLock);
            }
        }

        /// <summary>
        /// 为快速跟踪读到的数据补齐 IO 输出状态（真空阀 + 载台上电 2 位）
        /// 映射逻辑与 CollectData 一致（内部编号：阀 = TotalInputs + deviceId，
        /// 载台电 = TotalInputs + TotalBarometers + deviceId）。
        /// </summary>
        private void ApplyQuickTrackOutputStatus(BarometerData data, int deviceId, bool[] allOutputs)
        {
            if (allOutputs == null || allOutputs.Length < _config.TotalOutputs) return;
            if (data.OutputStatus == null || data.OutputStatus.Length < 2)
            {
                data.OutputStatus = new bool[2];
            }

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

        /// <summary>
        /// 从输出点内部编号反推工位编号
        /// 阀：TotalInputs + deviceId；载台电：TotalInputs + TotalBarometers + deviceId。
        /// 非阀/载台电输出（如预留通道）返回 false。
        /// </summary>
        private bool TryGetDeviceIdFromOutput(int outputId, out int deviceId)
        {
            deviceId = 0;
            if (outputId <= _config.TotalInputs) return false;

            int valveOffset = outputId - _config.TotalInputs;
            if (valveOffset >= 1 && valveOffset <= _config.TotalBarometers)
            {
                deviceId = valveOffset;
                return true;
            }

            int carrierOffset = outputId - _config.TotalInputs - _config.TotalBarometers;
            if (carrierOffset >= 1 && carrierOffset <= _config.TotalBarometers)
            {
                deviceId = carrierOffset;
                return true;
            }

            return false;
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

        /// <summary>
        /// 读取连续多个保持寄存器 —— 复用主程序**同一条** IO 耦合器连接（线程安全）
        ///
        /// 【用途】通讯测试窗体（CommunicationTestForm）的原始寄存器读写（0x2000~0x2009）走这里，
        /// 与采集线程共用 <see cref="ModbusTcpIoController"/> 及它内部的锁，不再自建第二条 TCP 连接。
        /// Mock 模式（<see cref="MockIoController"/>）不支持原始寄存器访问，返回 null。
        /// </summary>
        /// <param name="startAddress">起始寄存器地址</param>
        /// <param name="count">寄存器数量</param>
        /// <returns>寄存器值数组；未连接/Mock 模式/异常返回 null</returns>
        public ushort[] ReadHoldingRegisters(ushort startAddress, ushort count)
        {
            var real = _ioController as ModbusTcpIoController;
            return real?.ReadHoldingRegisters(startAddress, count);
        }

        /// <summary>
        /// 写单个保持寄存器 —— 复用主程序**同一条** IO 耦合器连接（线程安全）
        ///
        /// 【用途】同 <see cref="ReadHoldingRegisters"/>，供通讯测试窗体写入 DO 原始寄存器。
        /// Mock 模式（<see cref="MockIoController"/>）不支持原始寄存器访问，返回 false。
        /// </summary>
        /// <param name="address">寄存器地址</param>
        /// <param name="value">要写入的值</param>
        /// <returns>true=写成功；false=未连接/Mock 模式/异常</returns>
        public bool WriteSingleRegister(ushort address, ushort value)
        {
            var real = _ioController as ModbusTcpIoController;
            return real != null && real.WriteSingleRegister(address, value);
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
        /// 启动老化测试（批量）【V1.59 时序安全改造】
        ///
        /// 【与旧版的区别】旧版"开阀 + 载台上电"同时下发——真空还没建立产品就带电了，
        /// 与配置注释里"避免产品在未吸附固定的情况下通电老化"的安全意图矛盾。
        /// 新版启动时【只开真空阀】，载台上电由采集循环在
        /// 「真空压力到位 且 延时开启时间到」时补发（见 ProcessTestingProgress）；
        /// 真空建立失败（宽限窗口内压力始终不到位）则报警切断，全程不带电。
        ///
        /// 【参数定格】启动瞬间从工位配方抄写本次任务的时长/延时/报警阈值，
        /// 中途改配方不影响已在跑的测试（同批产品工艺一致性）。
        /// </summary>
        public void StartTesting(int[] deviceIds)
        {
            if (deviceIds == null || deviceIds.Length == 0) return;

            foreach (int deviceId in deviceIds)
            {
                StartSingleTestCore(deviceId, null);
            }

            // 批量写输出后对目标工位快速跟踪，实时反映真空建立过程
            StartQuickTracking(deviceIds);

            // 送风机生命周期联动（首台启动时启动送风机）
            UpdateFanLifecycle();
        }

        /// <summary>
        /// 单台启动测试的核心动作（StartTesting 循环体 / 断电恢复共用）
        ///
        /// 【动作】开真空阀（载台保持断电）→ 进入 Vacuuming 阶段 → 定格任务参数 → 快照落盘。
        /// 【overrideDurationSecs】断电恢复时传快照里定格的时长（此时工位配方可能已被改动，
        /// 以快照为准保证"重测参数 = 中断前参数"）；正常启动传 null（现场定格）。
        /// </summary>
        /// <param name="deviceId">工位编号</param>
        /// <param name="overrideParams">恢复用的定格参数；null = 正常启动按当前配置定格</param>
        private void StartSingleTestCore(int deviceId, TestSessionStation overrideParams)
        {
            if (deviceId < 1 || deviceId > _config.TotalBarometers) return;

            // ---- 1) 读取工位配方并定格本次任务参数 ----
            string sn = "", recipeName = "", displayMode = "";
            TimeSpan delayTime = TimeSpan.Zero, startTime = TimeSpan.Zero;
            decimal? recipePressure = null;
            lock (_stationInfoLock)
            {
                if (_stationInfo.TryGetValue(deviceId, out StationInfo info) && info != null)
                {
                    sn = info.SerialNumber ?? "";
                    recipeName = info.RecipeName ?? "";
                    displayMode = info.DisplayMode ?? "";
                    if (info.DelayTime.HasValue) delayTime = info.DelayTime.Value;
                    if (info.StartTime.HasValue) startTime = info.StartTime.Value;
                    recipePressure = info.RecipeNegativePressure;
                }
            }

            // 老化时长：配方的启动时间(StartTime) > 0 用配方值，否则回退全局 MaxTestDurationSeconds
            int durationSecs = (int)startTime.TotalSeconds;
            if (durationSecs <= 0)
            {
                durationSecs = _config.MaxTestDurationSeconds;
            }
            // 报警/到位阈值：配方负压值优先（null=没下发，如手输配方名未命中），
            // 否则回退全局 AlarmPressureThresholdKPa。
            // 【V1.66】不做"0=回全局"魔法：项目未上线、无老配方包袱，三个录入窗现在必填实数
            // （新建默认=全局值），存什么用什么，所见即所得。
            decimal thresholdKPa = recipePressure ?? _config.AlarmPressureThresholdKPa;

            // 断电恢复：以快照参数为准（中断前的工艺不能被中途改动污染）
            if (overrideParams != null)
            {
                durationSecs = overrideParams.DurationSeconds;
                thresholdKPa = overrideParams.AlarmThresholdKPa;
                sn = overrideParams.SerialNumber ?? sn;
                recipeName = overrideParams.RecipeName ?? recipeName;
                delayTime = TimeSpan.FromSeconds(overrideParams.DelaySeconds);
            }

            // ---- 2) 开阀（只开阀！上电由采集循环在真空到位+延时到后补发） ----
            _ioController.WriteOutput(_config.TotalInputs + deviceId, true);
            // 【V1.67】新任务开始：破空阀关闭（上一批泄压状态不带到下一批）+ 失压标记清零
            TurnOffVentValve();

            // ---- 3) 状态进入 Vacuuming，定格参数 ----
            lock (_stateLock)
            {
                DateTime now = DateTime.Now;
                _testingStates[deviceId - 1] = true;                 // 进入测试中
                _testPhases[deviceId - 1] = AgingPhase.Vacuuming;    // 子阶段：抽真空（载台断电）
                _valveOpenTimes[deviceId - 1] = now;                 // 延时/确认超时的计时起点
                _vacuumConfirmTimes[deviceId - 1] = now;             // 真空确认宽限开始
                _testStartTimes[deviceId - 1] = DateTime.MinValue;   // 上电后才计时
                _testDurations[deviceId - 1] = durationSecs;
                _sessionDurationSecs[deviceId - 1] = durationSecs;
                _sessionDelaySecs[deviceId - 1] = (int)Math.Round(delayTime.TotalSeconds);
                _sessionThresholdKPa[deviceId - 1] = thresholdKPa;
                _lastAlarmStates[deviceId - 1] = false;              // 清报警边沿，允许重新报警
                _lossNoted[deviceId - 1] = false;                    // 【V1.67】清失压保持标记
                _readFailCounts[deviceId - 1] = 0;                   // 清通讯失败计数
            }

            TestEventLogger.Write(_currentLotNumber, deviceId, "启动",
                $"启动老化测试（开真空，待真空建立+延时开启到后自动上电）SN:{sn} 配方:{recipeName}" +
                (string.IsNullOrEmpty(displayMode) ? "" : $" 显示模式:{displayMode}"));

            // 【V1.68】MES 上报：启动事件（触发器 Start；字段按 MesFieldMap 改名；
            // 开关关闭/触发器未命中时 Report 内部直接返回，零开销）
            try
            {
                var startFields = MesCommonFields(deviceId);
                startFields["event"] = "Start";
                startFields["sn"] = sn;
                startFields["recipe"] = recipeName;
                startFields["displayMode"] = displayMode;
                startFields["duration"] = durationSecs.ToString();
                startFields["detail"] = "启动老化测试（开真空）";
                _mes.Report("Start", startFields);
            }
            catch { /* 上报永不阻断启动 */ }

            // ---- 4) 任务快照落盘（断电/崩溃后可恢复重测）----
            SaveSessionSnapshot();
        }

        /// <summary>
        /// 停止老化测试（批量，手动停止 = 中止）
        /// 【动作】对选中的每台：关真空阀 + 断载台电 + 退出"测试中"状态 + 记录日志。
        /// 【结果语义 V1.59】手动中途停止记"中止"（不判 PASS 也不判 FAIL），
        /// 工位回空闲；与"到时自动完成(·待取料)"区分开。
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
                    _testPhases[deviceId - 1] = AgingPhase.None;
                    _vacuumConfirmTimes[deviceId - 1] = DateTime.MinValue;
                    _testStartTimes[deviceId - 1] = DateTime.MinValue;
                    _valveOpenTimes[deviceId - 1] = DateTime.MinValue;
                    _testDurations[deviceId - 1] = 0;
                    _sessionDurationSecs[deviceId - 1] = 0;
                    _sessionDelaySecs[deviceId - 1] = 0;
                    _sessionThresholdKPa[deviceId - 1] = _config.AlarmPressureThresholdKPa;
                }

                TestEventLogger.Write(_currentLotNumber, deviceId, "中止", "手动停止（未到时中止，不计判定结果）");
            }

            _ioController.WriteOutputs(outputIds.ToArray(), states.ToArray());

            // 【V1.30】停止测试后快速跟踪压力回落
            StartQuickTracking(deviceIds);

            // 送风机生命周期联动（如果是最后一台，这里会停止送风机）
            UpdateFanLifecycle();

            // 在测任务清单变化 → 快照落盘（全部停完时 SaveSessionSnapshot 会自动清文件）
            SaveSessionSnapshot();
        }

        /// <summary>
        /// 人工复位（报警/故障台复位回到空闲）
        /// 【动作】清除该台的报警边沿 / 测试状态 / 通讯失败计数，
        /// 并确保输出处于安全关闭状态（阀、载台电都 OFF），可重新启动测试。
        /// 【设计说明】报警后不自动恢复（真空失效原因未确认前自动重启有风险），
        /// 必须由操作员人工确认复位（设计评审结论）。
        /// </summary>
        /// <summary>
        /// 人工复位（报警/故障/已完成·待取料台复位回到空闲）
        /// 【动作】清除该台的报警边沿 / 测试状态 / 通讯失败计数，
        /// 并确保输出处于安全关闭状态（阀、载台电都 OFF），可重新启动测试。
        /// 【V1.59】Completed（已完成·待取料）也由本方法复位——语义即"确认取件完毕"。
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
                    _testPhases[deviceId - 1] = AgingPhase.None;
                    _vacuumConfirmTimes[deviceId - 1] = DateTime.MinValue;
                    _testStartTimes[deviceId - 1] = DateTime.MinValue;
                    _valveOpenTimes[deviceId - 1] = DateTime.MinValue;
                    _testDurations[deviceId - 1] = 0;
                    _sessionDurationSecs[deviceId - 1] = 0;
                    _sessionDelaySecs[deviceId - 1] = 0;
                    _sessionThresholdKPa[deviceId - 1] = _config.AlarmPressureThresholdKPa;
                    _readFailCounts[deviceId - 1] = 0;
                    _lastAlarmStates[deviceId - 1] = false;
                    _lossNoted[deviceId - 1] = false;   // 【V1.67】清失压保持标记
                }

                // 复位后保证输出处于安全关闭状态
                _ioController.WriteOutput(_config.TotalInputs + deviceId, false);
                _ioController.WriteOutput(_config.TotalInputs + _config.TotalBarometers + deviceId, false);
                // 【V1.67】破空阀同关（完成泄压后阀还开着，复位=确认取件，不残留输出）
                TurnOffVentValve();

                TestEventLogger.Write(_currentLotNumber, deviceId, "复位", "人工复位（报警解除/取件确认）");
            }

            // 把缓存里这些台的状态改为空闲，并清掉上次测试结果标记
            lock (_cacheLock)
            {
                foreach (int deviceId in deviceIds)
                {
                    if (_barometerDataCache.TryGetValue(deviceId, out BarometerData cached) && cached != null)
                    {
                        cached.Status = DeviceStatus.Idle;
                        cached.LastTestResult = "";
                    }
                }
            }

            // 在测任务清单变化 → 快照落盘
            SaveSessionSnapshot();
        }

        /// <summary>
        /// MES 通用字段（【V1.68 新增】time/lot/device/project；各事件再加自己的字段）。
        /// </summary>
        private Dictionary<string, string> MesCommonFields(int deviceId)
        {
            return new Dictionary<string, string>
            {
                { "time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                { "lot", _currentLotNumber ?? "" },
                { "device", deviceId.ToString() },
                { "project", ProjectProfile.ActiveProfileName }
            };
        }

        /// <summary>
        /// 读工位 SN/配方（【V1.68 新增】MES 上报用；无则空串，不抛异常）。
        /// </summary>
        private void GetStationSnRecipe(int deviceId, out string sn, out string recipe)
        {
            sn = "";
            recipe = "";
            try
            {
                lock (_stationInfoLock)
                {
                    StationInfo info;
                    if (_stationInfo.TryGetValue(deviceId, out info) && info != null)
                    {
                        sn = info.SerialNumber ?? "";
                        recipe = info.RecipeName ?? "";
                    }
                }
            }
            catch { /* 上报辅助失败不影响主链路 */ }
        }

        /// <summary>
        /// 关闭破空阀（【V1.67 新增】完成泄压是"开着保持"，复位/启动/急停时统一关闭，
        /// 不残留输出。点位未配置（=0）直接跳过，绝不写未知通道）。
        /// </summary>
        private void TurnOffVentValve()
        {
            if (_config.VentValveDoPoint <= 0) return;
            try
            {
                _ioController.WriteOutput(_config.VentValveDoPoint, false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[破空阀] 关闭失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 下料判定（【V1.67 新增】Q22 PendingReview 配套：到时标"待判定"的台，
        /// 下料时人工录 PASS/FAIL + 不良代码 + 处置）。
        ///
        /// 【流程】只收"已完成·待取料(Completed)"的台 → 逐台写"下料判定"事件
        /// （判定结果/不良代码/处置/SN 全进 CSV，追溯靠历史查询）→ 调 ResetDevices
        /// 回空闲（= 确认取件，面板清掉待判定）。
        /// 非 Completed 的台（空闲/测试中/故障）不碰，进 skipped 由调用方提示。
        /// 判定结果以 CSV/历史查询为准——面板回空闲后不再保留，这是既有追溯链，
        /// 不是新坑（LastTestResult 本来就只活到复位）。
        /// </summary>
        /// <param name="deviceIds">选中的工位号</param>
        /// <param name="pass"> true=PASS，false=FAIL</param>
        /// <param name="defectCode">不良代码（PASS 时可空）</param>
        /// <param name="disposition">处置（重测/报废/降级/让步放行，FAIL 时必填）</param>
        /// <param name="judgedIds">实际完成判定的工位号</param>
        /// <param name="skippedIds">跳过的工位号（非完成态/非法编号）</param>
        public void RecordUnloadJudge(int[] deviceIds, bool pass, string defectCode,
            string disposition, out int[] judgedIds, out int[] skippedIds)
        {
            var judged = new List<int>();
            var skipped = new List<int>();
            if (deviceIds != null)
            {
                foreach (int deviceId in deviceIds)
                {
                    if (deviceId < 1 || deviceId > _config.TotalBarometers)
                    {
                        skipped.Add(deviceId);
                        continue;
                    }
                    string sn = "";
                    bool completed = false;
                    lock (_cacheLock)
                    {
                        if (_barometerDataCache.TryGetValue(deviceId, out BarometerData cached)
                            && cached != null)
                        {
                            sn = cached.SerialNumber ?? "";
                            completed = (cached.Status == DeviceStatus.Completed);
                        }
                    }
                    if (!completed)
                    {
                        skipped.Add(deviceId);
                        continue;
                    }
                    string result = pass ? "PASS" : "FAIL";
                    TestEventLogger.Write(_currentLotNumber, deviceId, "下料判定",
                        $"判定={result} 不良代码:{defectCode} 处置:{disposition} SN:{sn}");
                    // 【V1.68】MES 上报：下料判定事件（触发器 UnloadJudge）
                    try
                    {
                        string ujRecipe, dummy;
                        GetStationSnRecipe(deviceId, out dummy, out ujRecipe);
                        var ujFields = MesCommonFields(deviceId);
                        ujFields["event"] = "UnloadJudge";
                        ujFields["sn"] = sn;
                        ujFields["recipe"] = ujRecipe;
                        ujFields["result"] = result;
                        ujFields["defectCode"] = defectCode ?? "";
                        ujFields["disposition"] = disposition ?? "";
                        _mes.Report("UnloadJudge", ujFields);
                    }
                    catch { /* 上报永不阻断判定 */ }
                    judged.Add(deviceId);
                }
            }
            if (judged.Count > 0)
            {
                // 判定完即确认取件：回空闲可投下一批（ResetDevices 会再记一条复位事件，正常）
                ResetDevices(judged.ToArray());
            }
            judgedIds = judged.ToArray();
            skippedIds = skipped.ToArray();
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
                    _testPhases[i] = AgingPhase.None;
                    _vacuumConfirmTimes[i] = DateTime.MinValue;
                    _testStartTimes[i] = DateTime.MinValue;
                    _valveOpenTimes[i] = DateTime.MinValue;
                    _testDurations[i] = 0;
                    _sessionDurationSecs[i] = 0;
                    _sessionDelaySecs[i] = 0;
                    _sessionThresholdKPa[i] = _config.AlarmPressureThresholdKPa;
                    _lastAlarmStates[i] = false;
                    _lossNoted[i] = false;   // 【V1.67】清失压保持标记
                }
            }

            // 停止送风机（急停时也停）
            if (_fanController != null)
            {
                _fanRunning = false;
                _fanController.Stop();
            }

            // 急停 = 放弃全部在测任务：删除快照，下次启动不再询问恢复
            TestSessionStore.Clear();

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
        /// 获取当前正在测试的工位号列表（【V1.66 新增】超温全线联停用）。
        ///
        /// 【为什么单开一个方法】调用方（MainForm 超温联停）要的是"id 列表"而不是
        /// bool 向量，用 GetTestingStates 再自己转一遍也行，但"在测 id"是个稳定业务概念，
        /// 值得一个名字。返回副本，调用方随便改不影响内部状态。
        /// </summary>
        /// <returns>在测工位号数组；无在测返回空数组（不返回 null，调用方免判空）</returns>
        public int[] GetTestingDeviceIds()
        {
            lock (_stateLock)
            {
                System.Collections.Generic.List<int> ids =
                    new System.Collections.Generic.List<int>();
                for (int i = 0; i < _testingStates.Length; i++)
                {
                    if (_testingStates[i]) ids.Add(i + 1);
                }
                return ids.ToArray();
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
        // 【V1.59 新增】断电恢复：快照落盘 / 恢复重测 / 放弃恢复
        // =====================================================================

        /// <summary>
        /// 把"当前所有在测任务"快照到 TestSession.json（内部各状态变化点调用）
        ///
        /// 【实现说明】在测清单为空时等价于 Clear（删除文件），调用方无需区分。
        /// 写文件失败静默（存储类异常不能拖垮采集/编排主链路）。
        /// </summary>
        private void SaveSessionSnapshot()
        {
            try
            {
                var stations = new List<TestSessionStation>();
                lock (_stateLock)
                {
                    for (int i = 0; i < _config.TotalBarometers; i++)
                    {
                        if (!_testingStates[i]) continue;

                        int deviceId = i + 1;
                        string sn = "", recipeName = "";
                        lock (_stationInfoLock)
                        {
                            if (_stationInfo.TryGetValue(deviceId, out StationInfo info) && info != null)
                            {
                                sn = info.SerialNumber ?? "";
                                recipeName = info.RecipeName ?? "";
                            }
                        }

                        stations.Add(new TestSessionStation
                        {
                            DeviceId = deviceId,
                            SerialNumber = sn,
                            RecipeName = recipeName,
                            DurationSeconds = _sessionDurationSecs[i],
                            DelaySeconds = _sessionDelaySecs[i],
                            AlarmThresholdKPa = _sessionThresholdKPa[i],
                            // 【V1.67】续跑需要：中断时的子阶段 + 上电时刻（锁内读，与定格参数同一快照）
                            Phase = (int)_testPhases[i],
                            PowerOnTime = _testStartTimes[i]
                        });
                    }
                }

                if (stations.Count == 0)
                {
                    // 没有在测任务了：清掉快照，避免下次启动误询问恢复
                    TestSessionStore.Clear();
                    return;
                }

                TestSessionStore.Save(new TestSession
                {
                    LotNumber = _currentLotNumber ?? "",
                    Stations = stations
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[任务快照] 生成快照失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 读取待恢复的在测任务快照（MainForm 启动时调用，用于弹窗询问）
        /// </summary>
        /// <returns>待恢复的快照；无待恢复任务返回 null</returns>
        public TestSession LoadPendingSession()
        {
            return TestSessionStore.Load();
        }

        /// <summary>
        /// 断电恢复：把快照里的在测工位重新投入测试（用户选定的策略）
        ///
        /// 【V1.59 整台重测】断电期间载台已断电、产品状态未知，老化数据不连续
        /// （行业通识 + 设计评审结论）。参数用快照里定格的值，批号一并写回保证追溯连贯。
        /// 【V1.67 续跑】PowerLossPolicy=ResumeRemaining 时：Aging 阶段中断的台
        /// 按"中断时刻的剩余时长"补足（断电期间不计入老化）；但真空必须重抽
        /// （断电后管路已泄压，不重抽就上电=安全事故，所以一律重新开阀走 Vacuuming）。
        /// Vacuuming 阶段中断 / 老快照无阶段字段 → 整段重跑（安全回退）。
        /// </summary>
        /// <param name="session">LoadPendingSession 读到的快照</param>
        public void RecoverSession(TestSession session)
        {
            if (session?.Stations == null || session.Stations.Count == 0) return;

            // 批号写回：恢复的重测记录仍挂在原批次下
            if (!string.IsNullOrEmpty(session.LotNumber))
            {
                _currentLotNumber = session.LotNumber;
            }

            bool resume = (_config.PowerLossPolicy == PowerLossPolicy.ResumeRemaining);
            foreach (var station in session.Stations)
            {
                if (station == null) continue;
                if (resume && station.Phase == (int)AgingPhase.Aging)
                {
                    // 续跑：重抽真空 + 补足剩余时长（ComputeResumeDurationSeconds 纯函数，
                    // 边界：不限时长→仍不限；断电前已到时→给 1 秒下一轮即完成）
                    int resumeSecs = AgingSequencer.ComputeResumeDurationSeconds(
                        station.DurationSeconds, station.PowerOnTime, session.SavedAt);
                    var adjusted = new TestSessionStation
                    {
                        DeviceId = station.DeviceId,
                        SerialNumber = station.SerialNumber,
                        RecipeName = station.RecipeName,
                        DurationSeconds = resumeSecs,
                        DelaySeconds = station.DelaySeconds,
                        AlarmThresholdKPa = station.AlarmThresholdKPa,
                        Phase = station.Phase,
                        PowerOnTime = station.PowerOnTime
                    };
                    StartSingleTestCore(station.DeviceId, adjusted);
                    TestEventLogger.Write(_currentLotNumber, station.DeviceId, "断电恢复",
                        $"续跑：已重抽真空，老化按剩余 {resumeSecs} 秒补足（断电期间不计入老化）");
                }
                else
                {
                    // 用快照参数重新走完整启动流程（开阀→抽真空→延时→上电→满时长老化）
                    StartSingleTestCore(station.DeviceId, station);
                }
            }
            // 【V1.62】脏快照里可能混着 null 台（上轮循环已跳过 null）：
            // ConvertAll 直接取 s.DeviceId 会空引用，这里只收有效台的编号
            // （StartQuickTracking 本身也会忽略非法编号，双保险）。
            var validIds = new List<int>();
            foreach (var station in session.Stations)
            {
                if (station != null) validIds.Add(station.DeviceId);
            }
            StartQuickTracking(validIds.ToArray());
            UpdateFanLifecycle();

            // 恢复汇总日志（文案跟随策略：重测 / 续跑）
            if (_config.PowerLossPolicy == PowerLossPolicy.ResumeRemaining)
            {
                TestEventLogger.Write(_currentLotNumber, 0, "断电恢复",
                    $"检测到上次未完成任务，{session.Stations.Count} 台已投入续跑（重抽真空+补足剩余时长）");
            }
            else
            {
                TestEventLogger.Write(_currentLotNumber, 0, "断电恢复",
                    $"检测到上次未完成任务，{session.Stations.Count} 台已按原参数整台重测");
            }
        }

        /// <summary>
        /// 放弃断电恢复（操作员选择"否"时调用）：
        /// 对快照涉及的所有工位执行安全关闭（关阀+断电——耦合器 DO 保持态，
        /// 上次退出时阀/电可能还开着，不能放着不管），然后删除快照。
        /// </summary>
        /// <param name="session">LoadPendingSession 读到的快照</param>
        public void DiscardSession(TestSession session)
        {
            if (session?.Stations == null || session.Stations.Count == 0)
            {
                TestSessionStore.Clear();
                return;
            }

            var outputIds = new List<int>();
            var states = new List<bool>();
            foreach (var station in session.Stations)
            {
                if (station == null || station.DeviceId < 1 || station.DeviceId > _config.TotalBarometers)
                {
                    continue;
                }
                outputIds.Add(_config.TotalInputs + station.DeviceId);                          // 阀 OFF
                states.Add(false);
                outputIds.Add(_config.TotalInputs + _config.TotalBarometers + station.DeviceId); // 电 OFF
                states.Add(false);
            }
            if (outputIds.Count > 0)
            {
            _ioController.WriteOutputs(outputIds.ToArray(), states.ToArray());
            // 【V1.67】破空阀同关（急停不残留任何输出）
            TurnOffVentValve();
            }

            TestSessionStore.Clear();
            TestEventLogger.Write(_currentLotNumber, 0, "放弃恢复",
                $"放弃 {session.Stations.Count} 台未完成任务并已安全关闭其阀与电源");
        }

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

                // 【V1.62 防火墙】只处理前 TotalBarometers 个位置：
                // 内置读取器返回数组长度恒等于总数；若某个读取器实现返回更长的数组，
                // 超出的位置会先把 _readFailCounts[i] 等状态数组索引越界（越界检查在后）。
                int laneCount = Math.Min(allData.Length, _config.TotalBarometers);
                for (int i = 0; i < laneCount; i++)
                {
                    int deviceId = i + 1;
                    BarometerData data = allData[i];

                    // 【V1.62 防火墙】上报 id 必须与轮询位置一致（内置实现恒一致）。
                    // 不一致说明读取器实现有 bug：IsAlarm 用上报 id 查状态数组、
                    // 缓存却按位置 id 读写，会状态分裂甚至越界。本轮跳过该台
                    // （缓存保留上轮值），不计通讯失败（不是通讯问题）。
                    if (data != null && data.DeviceId != deviceId)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[采集] 台{deviceId}上报 DeviceId={data.DeviceId} 与轮询位置不一致，已忽略本轮数据");
                        continue;
                    }

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
                                // 【V1.59】设备责任：不判产品 FAIL，结果记"设备异常"
                                HandleAlarm(deviceId, "通讯故障（连续读取失败）", productRelated: false, vacuumCause: false);
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
                    // 【V1.59】提前读取上一轮缓存中需要延续的标记（在状态分支之前）：
                    // - Completed（已完成·待取料）/ Fault（报警）：保留到人工复位/重新扫码；
                    // - LastTestResult（PASS/FAIL/设备异常）：保留到复位。
                    // 每个采集周期的 data 都是读取器新建的对象，不主动延续的话
                    // 这些标记会在下一轮被批量更新缓存时冲掉，追溯链就断了。
                    bool prevCompleted = false;
                    bool prevFaulted = false;
                    string prevResult = null;
                    lock (_cacheLock)
                    {
                        if (_barometerDataCache.TryGetValue(deviceId, out BarometerData prev) && prev != null)
                        {
                            prevCompleted = (prev.Status == DeviceStatus.Completed);
                            prevFaulted = (prev.Status == DeviceStatus.Fault);
                            prevResult = prev.LastTestResult;
                        }
                    }

                    bool isAlarm = false;
                    if (isTesting)
                    {
                        // 【V1.67】带原因的分类（Q19 责任策略与 Q11 失压策略的前置输入）
                        AlarmClassification classified = ClassifyAlarm(data);
                        isAlarm = classified.IsAlarm;

                        // 【V1.67】老化中失压 KeepRunning：Aging 阶段的真空类报警只记事件、不停机
                        // （抽真空阶段的真空建立失败不在此列：phase==Vacuuming 照常报警，
                        // 否则阀开了永不上电、无限空等，操作员还被蒙在鼓里）。
                        // 当轮按"正常测试中"走：状态 Testing、计时继续推进。
                        bool inAgingPhase = false;
                        if (isAlarm && classified.VacuumCause
                            && _config.AgingPressureLossPolicy == AgingPressureLossPolicy.KeepRunning)
                        {
                            lock (_stateLock)
                            {
                                inAgingPhase = (_testPhases[deviceId - 1] == AgingPhase.Aging);
                            }
                        }
                        if (inAgingPhase)
                        {
                            isAlarm = false;
                            lock (_stateLock)
                            {
                                if (!_lossNoted[deviceId - 1])
                                {
                                    _lossNoted[deviceId - 1] = true;
                                    TestEventLogger.Write(_currentLotNumber, deviceId, "失压保持运行",
                                        $"老化中压力越限（{data.VacuumPressure} kPa），策略=只记不停，继续老化",
                                        pressureKPa: data.VacuumPressure);
                                }
                            }
                        }
                        else
                        {
                            lock (_stateLock)
                            {
                                _lossNoted[deviceId - 1] = false;
                            }

                            lock (_stateLock)
                            {
                                if (isAlarm && !_lastAlarmStates[deviceId - 1])
                                {
                                    // 进入报警边沿：执行一次联动输出
                                    // 【V1.59】压力类报警（越限/真空建立失败）= 产品责任
                                    // 【V1.67】Q19：真空类按 VacuumFailKind 记 FAIL/装夹异常
                                    // （HandleAlarm 内部会退出测试态并把结果写入缓存；
                                    //   这里同步给本轮广播数据，面板当轮就能显示）
                                    _lastAlarmStates[deviceId - 1] = true;
                                    prevResult = HandleAlarm(deviceId, GetAlarmReason(data),
                                        productRelated: classified.ProductRelated,
                                        vacuumCause: classified.VacuumCause);
                                }
                                _lastAlarmStates[deviceId - 1] = isAlarm;
                            }
                        }
                    }

                    if (isAlarm)
                    {
                        data.Status = DeviceStatus.Fault;
                        if (!string.IsNullOrEmpty(prevResult))
                        {
                            data.LastTestResult = prevResult;
                        }
                    }
                    else if (isTesting)
                    {
                        data.Status = DeviceStatus.Testing;
                    }
                    else
                    {
                        // 非测试的台：延续完成态/故障态标记；其余情况覆盖读取器对常压
                        // （接近 0kPa）的"压力越限"误判，恢复为空闲。
                        // 【V1.59】Fault 也延续（prev.Status==Fault 只有报警/失联写过）：
                        // 否则报警台退出测试后下一轮就被冲成 Idle，面板上故障
                        // "闪一帧"即逝，操作员根本看不到（V1.10 以来的既有瑕疵）。
                        // Fault/Completed/结果标记都持续到人工复位或重新扫码才清除。
                        data.Status = prevCompleted ? DeviceStatus.Completed
                                    : prevFaulted ? DeviceStatus.Fault
                                    : DeviceStatus.Idle;
                        if (!string.IsNullOrEmpty(prevResult))
                        {
                            data.LastTestResult = prevResult;
                        }
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
                        // 【V1.62 防火墙】只收合法编号的数据进缓存（与逐台循环的 id 一致性
                        // 检查呼应）：上报 id 非法的脏数据不许污染缓存，避免按位置读缓存
                        // 的面板/快照拿到错台数据。
                        if (data != null && data.DeviceId >= 1 && data.DeviceId <= _config.TotalBarometers)
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
        /// 处理测试中的进度（【V1.59】三阶段状态机：抽真空 → 上电老化计时 → 到时完成）
        /// 仅在"测试中且未报警"时由采集循环调用。
        ///
        /// 【阶段推进逻辑】（判定函数在 <see cref="AgingSequencer"/>，本方法只负责执行）
        /// - Vacuuming：等「压力到位」且「距开阀 ≥ 延时开启」（两者自然取较晚）；
        ///   压力首次到位时记"真空建立"事件；到位前超时由 IsAlarm 判真空建立失败报警；
        ///   条件满足 → 载台上电 → Aging，老化计时起点 = 此刻。
        /// - Aging：到配方定格时长 → 自动完成（下电+关阀+PASS·待取料）。
        /// </summary>
        private void ProcessTestingProgress(int deviceId, BarometerData data)
        {
            int idx = deviceId - 1;

            // 该台本次任务的定格阈值（启动测试时已从配方/全局配置抄写）
            decimal threshold;
            lock (_stateLock)
            {
                threshold = _sessionThresholdKPa[idx];
            }
            bool inRange = !PressureOutOfRange(data.VacuumPressure, threshold);

            bool powerOnNow = false;   // 锁内决策、锁外执行 IO
            bool completeNow = false;

            lock (_stateLock)
            {
                switch (_testPhases[idx])
                {
                    case AgingPhase.Vacuuming:
                    {
                        // ---- 真空到位确认（只记一次"真空建立"事件）----
                        if (_vacuumConfirmTimes[idx] != DateTime.MinValue && inRange)
                        {
                            _vacuumConfirmTimes[idx] = DateTime.MinValue;
                            TestEventLogger.Write(_currentLotNumber, deviceId, "真空建立",
                                $"真空已到位: {data.VacuumPressure} kPa（阈值 {threshold} kPa），等待上电");
                        }

                        // ---- 到位 且 延时开启已到 → 上电进入老化计时 ----
                        if (_vacuumConfirmTimes[idx] == DateTime.MinValue &&
                            AgingSequencer.ShouldPowerOn(inRange, DateTime.Now - _valveOpenTimes[idx],
                                _sessionDelaySecs[idx]))
                        {
                            _testPhases[idx] = AgingPhase.Aging;
                            _testStartTimes[idx] = DateTime.Now;   // 老化计时起点 = 上电时刻
                            powerOnNow = true;
                        }
                        break;
                    }

                    case AgingPhase.Aging:
                    {
                        // ---- 老化计时到点 → 自动完成 ----
                        if (_testStartTimes[idx] != DateTime.MinValue &&
                            AgingSequencer.ShouldComplete(DateTime.Now - _testStartTimes[idx],
                                _sessionDurationSecs[idx]))
                        {
                            _testingStates[idx] = false;
                            completeNow = true;
                        }
                        break;
                    }
                }
            }

            // ---- 锁外执行副作用（IO / 日志 / 快照），不持锁做网络通讯 ----
            if (powerOnNow)
            {
                // 载台上电（内部编号 = TotalInputs + TotalBarometers + deviceId）
                _ioController.WriteOutput(_config.TotalInputs + _config.TotalBarometers + deviceId, true);
                string durationText = (_sessionDurationSecs[idx] <= 0) ? "不限" : (_sessionDurationSecs[idx] + "秒");
                TestEventLogger.Write(_currentLotNumber, deviceId, "上电",
                    $"真空确认+延时开启完成，载台上电开始老化（时长 {durationText}）");
                // 【V1.67】上电=计时起点落定：快照一次（含 Phase=Aging + 上电时刻，
                // 断电续跑就靠这份快照算剩余时长；边沿一次，非每轮写盘）
                SaveSessionSnapshot();
            }

            if (completeNow)
            {
                CompleteDeviceInternal(deviceId, "老化时长到");

                // 【V1.59 关键】同步修正本轮广播数据：状态分支在本轮早于完成动作执行，
                // data.Status 还是 Testing；若不改回来，本轮末尾"批量更新缓存"会用
                // Testing 覆盖 CompleteDeviceInternal 刚写入缓存的 Completed/判定结果，
                // 导致"已完成·待取料"只存活不到一个采集周期就被冲掉（面板闪一下即逝）。
                data.Status = DeviceStatus.Completed;
                data.LastTestResult = GetCompletionJudgeResult();
            }
        }

        /// <summary>
        /// 完成单台老化（内部方法，供老化到时自动完成调用）【V1.59 新增语义】
        /// 【动作】断载台电 + 关真空阀 → 状态置"已完成·待取料(Completed)" +
        /// 结果标 PASS（全程无报警才会走到这里）→ 记日志 → 快照落盘。
        /// 【为什么不回空闲】操作员需要一眼区分"没投料"和"测完该取件"，
        /// 取件后通过人工复位或重新扫码绑定回到空闲。
        /// 【注意】本方法会访问 _ioController（通讯）和写日志，
        /// 必须在 _stateLock 锁外调用（避免持锁做网络 IO）。
        /// </summary>
        /// <param name="deviceId">设备编号</param>
        /// <param name="reason">完成原因描述</param>
        private void CompleteDeviceInternal(int deviceId, string reason)
        {
            // 【V1.68】MES 上报先拍照：时长定格值在下面被清零，SN/配方与快照无关但统一快照口径
            int doneDuration = 0;
            string doneSn, doneRecipe;
            lock (_stateLock) { doneDuration = _sessionDurationSecs[deviceId - 1]; }
            GetStationSnRecipe(deviceId, out doneSn, out doneRecipe);

            // 断电 + 关阀
            _ioController.WriteOutput(_config.TotalInputs + deviceId, false);
            _ioController.WriteOutput(_config.TotalInputs + _config.TotalBarometers + deviceId, false);

            // 清理该台状态机
            lock (_stateLock)
            {
                _testingStates[deviceId - 1] = false;
                _testPhases[deviceId - 1] = AgingPhase.None;
                _vacuumConfirmTimes[deviceId - 1] = DateTime.MinValue;
                _testStartTimes[deviceId - 1] = DateTime.MinValue;
                _valveOpenTimes[deviceId - 1] = DateTime.MinValue;
                _testDurations[deviceId - 1] = 0;
                _sessionDurationSecs[deviceId - 1] = 0;
                _sessionDelaySecs[deviceId - 1] = 0;
                _sessionThresholdKPa[deviceId - 1] = _config.AlarmPressureThresholdKPa;
            }

            // 记录完成事件（【V1.67】Q22 策略化：AutoPass=标PASS（现状）；
            // PendingReview=标"待判定"，下料时人工录 PASS/FAIL+不良代码+处置）
            string judgeResult = GetCompletionJudgeResult();
            TestEventLogger.Write(_currentLotNumber, deviceId, "完成",
                $"{reason}·待取料({judgeResult})");

            // 缓存置为"已完成·待取料"+ 判定结果（不覆盖 Fault——报警台的结果另行标记）
            lock (_cacheLock)
            {
                if (_barometerDataCache.TryGetValue(deviceId, out BarometerData cached) && cached != null
                    && cached.Status != DeviceStatus.Fault)
                {
                    cached.Status = DeviceStatus.Completed;
                    cached.LastTestResult = judgeResult;
                }
            }

            // 【V1.68】MES 上报：完成事件（触发器 Complete）
            try
            {
                var doneFields = MesCommonFields(deviceId);
                doneFields["event"] = "Complete";
                doneFields["sn"] = doneSn;
                doneFields["recipe"] = doneRecipe;
                doneFields["result"] = judgeResult;
                doneFields["duration"] = doneDuration.ToString();
                doneFields["detail"] = reason;
                _mes.Report("Complete", doneFields);
            }
            catch { /* 上报永不阻断完成 */ }

            // 【V1.67】完成动作策略（Q6/Q15）：蜂鸣提醒 / 破空泄压
            // - Beep：PC 蜂鸣一声（无硬件要求；无音频设备时静默跳过，不抛异常）；
            // - Vent：开破空阀泄压（关阀≠泄压，残压还在徒手可能拿不下）。
            //   点位未配置（VentValveDoPoint=0）只记日志跳过——绝不写坏未知通道。
            try
            {
                CompletionAction action = _config.CompletionAction;
                if (action == CompletionAction.PowerOffAndBeep
                    || action == CompletionAction.PowerOffVentAndBeep)
                {
                    try { Console.Beep(880, 400); }
                    catch { /* 无音频设备时静默跳过 */ }
                }
                if (action == CompletionAction.PowerOffAndVent
                    || action == CompletionAction.PowerOffVentAndBeep)
                {
                    if (_config.VentValveDoPoint > 0)
                    {
                        _ioController.WriteOutput(_config.VentValveDoPoint, true);
                        TestEventLogger.Write(_currentLotNumber, deviceId, "破空泄压",
                            $"已开启破空阀（DO内部编号 {_config.VentValveDoPoint}），取料后复位/启动自动关闭");
                    }
                    else
                    {
                        TestEventLogger.Write(_currentLotNumber, deviceId, "破空泄压",
                            "完成动作要求泄压但 VentValveDoPoint=0（未配点位），已跳过泄压只下电");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[完成动作] 蜂鸣/泄压执行失败: {ex.Message}");
            }

            // 在测任务清单变化 → 快照落盘
            SaveSessionSnapshot();
        }

        /// <summary>
        /// 本次完成的判定结果（【V1.67 新增】Q22 策略）：
        /// AutoPass → "PASS"（现状）；PendingReview → "待判定"（下料人工录）。
        /// </summary>
        private string GetCompletionJudgeResult()
        {
            return _config.CompletionJudgePolicy == CompletionJudgePolicy.PendingReview
                ? "待判定" : "PASS";
        }

        /// <summary>
        /// 报警联动（关阀 + 断电 + 记日志 + 结果标记）
        /// 【设计说明】
        /// - 只在进入报警的边沿触发一次（调用方保证）
        /// - 报警后不自动恢复，需要人工复位（ResetDevices）后再重新测试
        /// 【V1.59 责任分类】productRelated 决定结果口径：
        /// - true  = 产品相关（压力越限/真空建立失败/DI 触点）→ LastTestResult="FAIL"；
        /// - false = 设备异常（气压表通讯失联）→ LastTestResult="设备异常"，不判产品不合格，
        ///   避免设备问题拉低产品直通率、冤枉良品。
        /// </summary>
        /// <param name="deviceId">设备编号</param>
        /// <param name="reason">报警原因描述</param>
        /// <param name="productRelated">是否产品相关的报警（决定 FAIL 口径）</param>
        /// <param name="vacuumCause">是否为真空类原因（压力越限/真空建立失败）：
        /// 真空类才受 VacuumFailKind 策略影响（Q19）；DI 触点传 false，永远 FAIL</param>
        /// <returns>写入缓存的结果标记（FAIL / 装夹异常 / 设备异常，调用方同步给本轮广播数据）</returns>
        private string HandleAlarm(int deviceId, string reason, bool productRelated, bool vacuumCause)
        {
            int valveOutputId = _config.TotalInputs + deviceId;
            int carrierOutputId = _config.TotalInputs + _config.TotalBarometers + deviceId;

            // 报警联动动作：
            // - 关真空阀（防止继续抽真空/泄漏等异常扩大）
            // - 断载台上电（保护被测件/治具）
            _ioController.WriteOutput(valveOutputId, false);
            _ioController.WriteOutput(carrierOutputId, false);

            // 清理该台状态机（退出测试）
            lock (_stateLock)
            {
                _testingStates[deviceId - 1] = false;
                _testPhases[deviceId - 1] = AgingPhase.None;
                _vacuumConfirmTimes[deviceId - 1] = DateTime.MinValue;
                _testStartTimes[deviceId - 1] = DateTime.MinValue;
                _valveOpenTimes[deviceId - 1] = DateTime.MinValue;
                _sessionDurationSecs[deviceId - 1] = 0;
                _sessionDelaySecs[deviceId - 1] = 0;
                _sessionThresholdKPa[deviceId - 1] = _config.AlarmPressureThresholdKPa;
            }

            // 结果标记（【V1.67】Q19 策略化：真空类报警按 VacuumFailKind 记 FAIL/装夹异常；
            // 缺省 ProductFail 时与原来逐字一致；DI 与失联口径不受策略影响）
            string result = AgingSequencer.MapAlarmResult(
                productRelated, vacuumCause, _config.VacuumFailKind);

            // 记录报警事件（供追溯；压力值用于漏气分析）
            TestEventLogger.Write(_currentLotNumber, deviceId, $"报警({result})", reason,
                pressureKPa: GetDeviceLastPressure(deviceId));

            // 缓存里标结果（Status 由调用方置 Fault）
            lock (_cacheLock)
            {
                if (_barometerDataCache.TryGetValue(deviceId, out BarometerData cached) && cached != null)
                {
                    cached.LastTestResult = result;
                }
            }

            // 【V1.68】MES 上报：报警事件（触发器 Alarm；含压力值供 MES 侧漏气分析）
            try
            {
                string almSn, almRecipe;
                GetStationSnRecipe(deviceId, out almSn, out almRecipe);
                var almFields = MesCommonFields(deviceId);
                almFields["event"] = "Alarm";
                almFields["sn"] = almSn;
                almFields["recipe"] = almRecipe;
                almFields["result"] = result;
                almFields["detail"] = reason;
                decimal? p = GetDeviceLastPressure(deviceId);
                almFields["pressure"] = p.HasValue ? p.Value.ToString() : "";
                _mes.Report("Alarm", almFields);
            }
            catch { /* 上报永不阻断报警 */ }

            // 在测任务清单变化 → 快照落盘
            SaveSessionSnapshot();

            return result;
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
        /// 【V1.59】阈值由调用方传入：测试中的台用"定格的本次任务阈值"
        /// （配方负压值优先、全局兜底），使不同产品可按各自工艺要求判定。
        /// </summary>
        private bool PressureOutOfRange(decimal pressureKPa, decimal thresholdKPa)
        {
            // 【V1.62】判定口径收拢进 AgingSequencer.IsPressureOutOfRange，
            // 本方法只剩"取配置方向后转调"（行为与原来逐字一致）。
            return AgingSequencer.IsPressureOutOfRange(
                pressureKPa, thresholdKPa, _config.AlarmWhenPressureHigherThanThreshold);
        }

        /// <summary>
        /// 报警分类结果（【V1.67 新增】Q19/Q11 策略化的前置：先分清"真空类"还是"设备类"，
        /// 策略才知道该不该插手）。
        /// - 真空类（压力越限 / 真空建立失败）→ Q19 责任策略、Q11 失压策略可干预；
        /// - 非真空类（通讯失联 / DI 触点）→ 策略不碰：失联永远"设备异常"，
        ///   DI 触点是仪表硬件判的永远 FAIL（沿用 V1.59 口径）。
        /// </summary>
        private struct AlarmClassification
        {
            public bool IsAlarm;
            public bool ProductRelated;
            public bool VacuumCause;
        }

        /// <summary>
        /// 综合报警分类（【V1.67 新增】IsAlarm 的"带原因版"；IsAlarm 转调本方法，
        /// 判定口径只有一份，行为与原来逐字一致）。
        /// </summary>
        private AlarmClassification ClassifyAlarm(BarometerData data)
        {
            var none = new AlarmClassification { IsAlarm = false };
            if (data == null) return new AlarmClassification { IsAlarm = true }; // 空数据视为异常

            int idx = data.DeviceId - 1;

            // 读取测试状态与该台的定格阈值（与原 IsAlarm 同口径）
            bool isTesting = false;
            bool inConfirmWindow = false;
            DateTime valveOpenTime = DateTime.MinValue;
            decimal threshold;
            lock (_stateLock)
            {
                isTesting = _testingStates[idx];
                threshold = _sessionThresholdKPa[idx];
                // 未在测试的台没有定格任务，用全局阈值兜底
                if (!isTesting && _testPhases[idx] == AgingPhase.None)
                {
                    threshold = _config.AlarmPressureThresholdKPa;
                }
                if (isTesting)
                {
                    inConfirmWindow = (_vacuumConfirmTimes[idx] != DateTime.MinValue);
                    valveOpenTime = _valveOpenTimes[idx];
                }
            }

            // 压力越限判断（按该台的有效阈值）
            bool pressureAlarm = PressureOutOfRange(data.VacuumPressure, threshold);

            // ===== 真空确认宽限窗口 =====
            if (isTesting && inConfirmWindow)
            {
                // 【注意传参语义】pressureAlarm=true 表示"越限"="尚未到位"，
                // 而 IsVacuumBuildFailed 的首参要的是"是否已到位"，必须取反传入。
                if (AgingSequencer.IsVacuumBuildFailed(!pressureAlarm,
                    DateTime.Now - valveOpenTime, _config.VacuumConfirmTimeoutMs))
                {
                    return new AlarmClassification { IsAlarm = true, ProductRelated = true, VacuumCause = true };
                }
                return none;    // 窗口内：暂不报警（压力到位后由 ProcessTestingProgress 推进）
            }

            // 1) 压力越限 → 真空类（本函数只被采集循环对"测试中"的台调用，
            //    未测试的台走不到这里——见 CollectData 的 isTesting 门控）。
            if (pressureAlarm)
            {
                return new AlarmClassification { IsAlarm = true, ProductRelated = true, VacuumCause = true };
            }

            // 2) DI 报警触点 → 产品相关但【非真空类】（Q19 策略不改它，永远 FAIL）
            if (_config.UseDiAlarmContact &&
                data.InputStatus != null && data.InputStatus.Length >= 1 &&
                data.InputStatus[0])
            {
                return new AlarmClassification { IsAlarm = true, ProductRelated = true, VacuumCause = false };
            }

            return none;
        }

        /// <summary>
        /// 综合报警判定（【V1.10 增强】【V1.59 责任分类】）
        ///
        /// 报警来源与责任归属（决定 LastTestResult 的 FAIL 口径）：
        /// 1) 压力越限（失压 / 超抽）——【产品相关】漏气/没放好 → FAIL
        /// 2) 真空建立超时：抽真空阶段宽限窗口内压力始终未到位——【产品相关】→ FAIL
        /// 3) （可选，配置 UseDiAlarmContact）气压表硬件报警触点——【产品相关】→ FAIL
        /// （通讯失联报警在 CollectData 的 data==null 分支处理，属【设备异常】不判产品 FAIL）
        ///
        /// 【真空确认宽限说明】
        /// 刚开阀时压力还接近常压，处于"越限"状态是正常的（真空需要时间建立）。
        /// 所以在确认窗口内不按压力报警；超时还没建立才报警。
        ///
        /// 【V1.67】本方法只剩"转调 ClassifyAlarm 取是否报警"（行为逐字一致）；
        /// 需要原因的调用方（CollectData 边沿/失压策略）直接调 ClassifyAlarm。
        /// </summary>
        private bool IsAlarm(BarometerData data)
        {
            return ClassifyAlarm(data).IsAlarm;
        }

        /// <summary>
        /// 生成报警原因描述（用于日志 / 显示）
        /// 【V1.59】阈值取该台的定格值（配方优先），与实际判定一致便于现场对数
        /// </summary>
        private string GetAlarmReason(BarometerData data)
        {
            if (data == null) return "通讯故障";
            decimal threshold = _config.AlarmPressureThresholdKPa;
            int idx = data.DeviceId - 1;
            lock (_stateLock)
            {
                // 测试中的台用定格任务阈值；否则用全局兜底
                if (idx >= 0 && idx < _sessionThresholdKPa.Length && _testingStates[idx])
                {
                    threshold = _sessionThresholdKPa[idx];
                }
            }
            return $"真空压力越限: {data.VacuumPressure} kPa（阈值 {threshold} kPa）";
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
                    _quickTrackTimer?.Stop();
                    _quickTrackTimer?.Dispose();

                    // 【V1.68】停 MES 后台发送线程（最多等2秒，不拖退出）
                    if (_mes != null) { try { _mes.Dispose(); } catch { } }
                }
            }
        }
    }
}
