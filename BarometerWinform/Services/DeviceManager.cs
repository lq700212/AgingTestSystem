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
    /// 负责管理所有气压表和IO设备的连接、数据采集和状态更新
    /// 是整个系统的核心服务类
    ///
    /// 【线程安全说明】
    /// 本类使用 System.Timers.Timer 进行定时数据采集，Elapsed 事件在后台线程池触发。
    /// 因此 _barometerDataCache 使用 lock 保护，防止后台写入与UI读取同时发生。
    /// _disposed 字段使用 volatile 修饰，确保跨线程可见性。
    /// </summary>
    public class DeviceManager : IDisposable
    {
        /// <summary>
        /// 气压表数据读取器（接口）
        /// 当前使用 MockBarometerReader，实际使用时替换为真实实现
        /// </summary>
        private readonly IBarometerReader _barometerReader;

        /// <summary>
        /// IO控制器（接口）
        /// 当前使用 MockIoController，实际使用时替换为真实实现
        /// </summary>
        private readonly IIoController _ioController;

        /// <summary>
        /// 设备配置
        /// </summary>
        private readonly DeviceConfig _config;

        /// <summary>
        /// 数据采集定时器
        /// 使用 System.Timers.Timer，Elapsed 事件在后台线程触发
        /// </summary>
        private readonly System.Timers.Timer _collectTimer;

        /// <summary>
        /// 存储所有气压表的最新数据
        /// Key: 设备编号，Value: 气压表数据
        /// 【线程安全】使用 _cacheLock 保护对此字典的所有读写操作
        /// </summary>
        private readonly Dictionary<int, BarometerData> _barometerDataCache = new Dictionary<int, BarometerData>();

        /// <summary>
        /// 数据缓存锁对象
        /// 保护 _barometerDataCache 的线程安全访问
        /// </summary>
        private readonly object _cacheLock = new object();

        /// <summary>
        /// 采集锁对象
        /// 防止定时器 Elapsed 事件重入（上一次采集未完成时，下一次又触发）
        /// </summary>
        private readonly object _collectLock = new object();

        /// <summary>
        /// 记录上一次采集周期的“报警状态”
        /// 
        /// 目的（给新手看的）：
        /// - 如果某一路气压持续处于报警区间，我们不希望每 1 秒都重复下发“关阀/断电”
        /// - 所以只在“从未报警 → 进入报警”的边沿触发一次输出动作
        /// </summary>
        private readonly bool[] _lastAlarmStates;

        /// <summary>
        /// 标记是否已释放资源
        /// 【线程安全】使用 volatile 修饰，确保跨线程可见性
        /// 防止 Dispose 期间后台线程仍访问已释放资源
        /// </summary>
        private volatile bool _disposed = false;

        /// <summary>
        /// 批量数据更新事件
        /// 当一次采集周期完成时触发一次，参数为本次采集的所有数据数组
        /// UI层订阅此事件后批量刷新界面，避免 72 次单条事件触发的性能问题
        /// 【注意】此事件在后台线程触发，UI层需要使用 BeginInvoke 异步切换到UI线程
        /// </summary>
        public event EventHandler<BarometerData[]> OnBatchDataUpdated;

        /// <summary>
        /// 连接状态变更事件
        /// 当设备连接状态发生变化时触发
        /// 【注意】Dispose 期间不会触发此事件（避免回调到已释放的UI）
        /// </summary>
        public event EventHandler<bool> OnConnectionStatusChanged;

        /// <summary>
        /// 初始化设备管理器
        /// </summary>
        /// <param name="config">设备配置</param>
        public DeviceManager(DeviceConfig config)
        {
            _config = config;

            // 初始化硬件接口实现
            // 
            // 对新手说明：
            // - 项目开发早期没有接线/设备时，用 Mock 可以保证 UI/业务流程能跑起来
            // - 现场接线完成后，把 App.config 里的 UseMockCommunication=false 即可切换到真实通讯
            if (_config.UseMockCommunication)
            {
                _barometerReader = new MockBarometerReader();
                _ioController = new MockIoController();
            }
            else
            {
                _barometerReader = new ModbusRtuBarometerReader();
                _ioController = new ModbusTcpIoController();
            }

            _lastAlarmStates = new bool[_config.TotalBarometers];

            // 订阅错误事件（使用命名方法，便于 Dispose 时取消订阅）
            _barometerReader.OnError += BarometerReader_OnError;
            _ioController.OnError += IoController_OnError;

            // 初始化数据采集定时器
            _collectTimer = new System.Timers.Timer(_config.CollectInterval);
            _collectTimer.Elapsed += CollectTimer_Elapsed;
            _collectTimer.AutoReset = true;
            // 设置 SynchronizingObject 为 null，确保 Elapsed 在线程池触发
            // （默认即为 null，这里显式说明）
            _collectTimer.SynchronizingObject = null;
        }

        /// <summary>
        /// 气压表读取器错误回调（命名方法，便于取消订阅）
        /// </summary>
        private void BarometerReader_OnError(object sender, string message)
        {
            System.Diagnostics.Debug.WriteLine($"气压表读取错误: {message}");
        }

        /// <summary>
        /// IO控制器错误回调（命名方法，便于取消订阅）
        /// </summary>
        private void IoController_OnError(object sender, string message)
        {
            System.Diagnostics.Debug.WriteLine($"IO控制错误: {message}");
        }

        /// <summary>
        /// 启动设备管理器
        /// 连接设备并开始数据采集
        ///
        /// 【修复 H2】部分连接失败时回滚已建立的连接，避免资源泄漏
        /// 【修复 M1】先采集一次数据再启动定时器，避免定时器与首次采集并发
        /// </summary>
        /// <returns>是否启动成功</returns>
        public bool Start()
        {
            try
            {
                // 连接气压表读取器
                bool barometerConnected = _barometerReader.Connect(_config);

                // 连接IO控制器
                bool ioConnected = _ioController.Connect(_config);

                // 全部连接成功才继续
                if (barometerConnected && ioConnected)
                {
                    // 先采集一次数据（同步调用，确保首次数据立即可用）
                    // 在启动定时器之前调用，避免定时器立即触发导致与首次采集并发
                    CollectData();

                    // 启动数据采集定时器
                    _collectTimer.Start();

                    // 触发连接状态变更事件
                    OnConnectionStatusChanged?.Invoke(this, true);

                    return true;
                }

                // 【修复 H2】部分连接失败时回滚已建立的连接，避免资源泄漏
                // 下次 Start() 时才能重新建立连接
                if (barometerConnected)
                {
                    _barometerReader.Disconnect();
                }
                if (ioConnected)
                {
                    _ioController.Disconnect();
                }

                // 触发连接状态变更事件（连接失败）
                OnConnectionStatusChanged?.Invoke(this, false);

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设备管理器启动失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 停止设备管理器
        /// 停止数据采集并断开设备连接
        ///
        /// 【修复 H3】Dispose 期间调用此方法时，_disposed 已为 true，跳过事件触发
        /// 避免回调到已释放的 UI 线程
        /// </summary>
        public void Stop()
        {
            // 先停止定时器，防止新的采集任务进入
            _collectTimer.Stop();

            // 断开硬件连接
            _barometerReader.Disconnect();
            _ioController.Disconnect();

            // 仅在非 Dispose 期间触发事件
            // Dispose 调用 Stop 时，UI 可能正在释放，触发事件会回调到已释放的控件
            if (!_disposed)
            {
                OnConnectionStatusChanged?.Invoke(this, false);
            }
        }

        /// <summary>
        /// 获取指定设备的最新数据
        /// 【线程安全】使用 lock 保护缓存读取
        /// 【修复 M3】返回数据的副本（深拷贝），避免外部修改污染缓存
        /// </summary>
        /// <param name="deviceId">设备编号</param>
        /// <returns>气压表数据副本，如果不存在返回null</returns>
        public BarometerData GetBarometerData(int deviceId)
        {
            lock (_cacheLock)
            {
                _barometerDataCache.TryGetValue(deviceId, out BarometerData data);
                // 返回副本，避免外部修改污染缓存
                return data?.Clone();
            }
        }

        /// <summary>
        /// 获取所有设备的最新数据
        /// 【线程安全】使用 lock 保护缓存读取
        /// 【修复 M3】返回每个数据的副本，避免外部修改污染缓存
        /// </summary>
        /// <returns>包含所有气压表数据副本的数组</returns>
        public BarometerData[] GetAllBarometerData()
        {
            lock (_cacheLock)
            {
                var data = new BarometerData[_config.TotalBarometers];
                for (int i = 0; i < _config.TotalBarometers; i++)
                {
                    _barometerDataCache.TryGetValue(i + 1, out BarometerData original);
                    // 返回副本，避免外部修改污染缓存
                    data[i] = original?.Clone();
                }
                return data;
            }
        }

        /// <summary>
        /// 设置输出点状态
        /// </summary>
        /// <param name="outputId">输出点编号</param>
        /// <param name="state">输出状态</param>
        public void SetOutput(int outputId, bool state)
        {
            _ioController.WriteOutput(outputId, state);
        }

        /// <summary>
        /// 获取输入点状态
        /// </summary>
        /// <param name="inputId">输入点编号</param>
        /// <returns>输入状态</returns>
        public bool GetInput(int inputId)
        {
            return _ioController.ReadInput(inputId);
        }

        /// <summary>
        /// 获取所有输入点状态
        /// </summary>
        /// <returns>输入状态数组</returns>
        public bool[] GetAllInputs()
        {
            return _ioController.ReadAllInputs();
        }

        /// <summary>
        /// 获取所有输出点状态
        /// </summary>
        /// <returns>输出状态数组</returns>
        public bool[] GetAllOutputs()
        {
            return _ioController.ReadAllOutputs();
        }

        /// <summary>
        /// 数据采集定时器触发事件
        /// 定时采集所有气压表数据
        ///
        /// 【防重入说明】
        /// System.Timers.Timer 的 Elapsed 事件在后台线程触发，
        /// 如果上一次采集耗时超过采集间隔，可能会重入。
        /// 使用 Monitor.TryEnter 防止重入（获取不到锁直接跳过本次）。
        /// </summary>
        private void CollectTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            // 已释放则不再采集
            if (_disposed) return;

            // 尝试获取锁，如果获取失败说明上一次采集还在进行，直接跳过
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
        /// 执行数据采集
        /// 从气压表读取器批量读取所有设备数据，更新缓存，并触发批量数据更新事件
        ///
        /// 【修复 M2】改为批量事件触发，一次采集周期只触发一次 OnBatchDataUpdated
        /// 避免逐条触发 72 次事件导致的 UI 卡顿
        /// </summary>
        private void CollectData()
        {
            try
            {
                // 读取所有气压表数据
                var allData = _barometerReader.ReadAllData();

                // 防御性检查：数据为空时直接返回，避免空引用异常
                if (allData == null || allData.Length == 0) return;

                // 从 IO 控制器读取整机 DI/DO 状态
                //
                // 给新手的说明：
                // - IO 模块通常支持一次性读出一整段寄存器（比如从 0x1000 读 5 个寄存器 = 72 路输入）
                // - 如果我们逐点调用 ReadInput(1) / ReadInput(2) ... ReadInput(72)，
                //   相当于发 72 次网络请求，更慢，也更容易超时
                // - 所以这里设计为“先批量读到 bool[]”，再按 deviceId 分配给每个面板的数据对象
                bool[] allInputs = _ioController.ReadAllInputs();
                bool[] allOutputs = _ioController.ReadAllOutputs();

                for (int i = 0; i < allData.Length; i++)
                {
                    BarometerData data = allData[i];
                    if (data == null) continue;

                    int deviceId = data.DeviceId;
                    if (deviceId < 1 || deviceId > _config.TotalBarometers) continue;

                    // 1) 回填输入状态（每个气压表 1 个输入：真空负压表信号）
                    // allInputs 的索引规则：索引 0 对应 inputId=1（X000）
                    if (allInputs != null && allInputs.Length >= _config.TotalInputs && deviceId <= _config.TotalInputs)
                    {
                        if (data.InputStatus == null || data.InputStatus.Length < 1) data.InputStatus = new bool[1];
                        data.InputStatus[0] = allInputs[deviceId - 1];
                    }

                    // 2) 回填输出状态（每个气压表 2 个输出：真空电磁阀 + 载台上电）
                    // allOutputs 的索引规则：索引 0 对应 outputId=TotalInputs+1（默认 73，对应 Y000）
                    if (allOutputs != null && allOutputs.Length >= _config.TotalOutputs)
                    {
                        if (data.OutputStatus == null || data.OutputStatus.Length < 2) data.OutputStatus = new bool[2];

                        int outputStart = _config.TotalInputs + 1;

                        // 输出点内部编号规则（与 IoMapBuilder 一致）：
                        // - 真空电磁阀：TotalInputs + deviceId
                        //   例：deviceId=1 => outputId=73（Y000）
                        // - 载台上电：TotalInputs + TotalBarometers + deviceId
                        //   例：deviceId=1 => outputId=145（Y110）
                        int valveOutputId = _config.TotalInputs + deviceId;
                        int carrierOutputId = _config.TotalInputs + _config.TotalBarometers + deviceId;

                        // 从“内部编号 outputId”换算成 allOutputs[] 的数组下标：
                        // - allOutputs[0] 对应 outputId=outputStart
                        // - 所以 index = outputId - outputStart
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

                    // 3) 计算报警状态（只依赖压力值）
                    // 注意：这里是“业务判定”，不是硬件通讯协议的一部分
                    bool isAlarm = IsAlarm(data.VacuumPressure);
                    if (isAlarm && !_lastAlarmStates[deviceId - 1])
                    {
                        // 进入报警边沿：执行一次联动输出
                        HandleAlarm(deviceId);
                    }

                    // 记录本次报警状态，供下次采集做边沿判断
                    _lastAlarmStates[deviceId - 1] = isAlarm;
                    if (isAlarm)
                    {
                        // UI 表现：如果报警则把面板状态置为 Fault（红色）
                        data.Status = DeviceStatus.Fault;
                    }
                }

                // 批量更新缓存
                lock (_cacheLock)
                {
                    // 为什么需要 lock：
                    // - _collectTimer 的 Elapsed 在后台线程执行
                    // - UI 线程会通过 GetBarometerData/GetAllBarometerData 读取缓存
                    // - 不加锁会导致字典并发读写抛异常
                    foreach (var data in allData)
                    {
                        if (data != null)
                        {
                            _barometerDataCache[data.DeviceId] = data;
                        }
                    }
                }

                // 触发批量数据更新事件（一次采集只触发一次）
                // UI层订阅此事件后批量刷新界面，避免 72 次单条事件触发的性能问题
                // 【注意】此事件在后台线程触发，UI层需要使用 BeginInvoke 异步切换到UI线程
                OnBatchDataUpdated?.Invoke(this, allData);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"数据采集失败: {ex.Message}");
            }
        }

        private bool IsAlarm(decimal pressurePa)
        {
            // 报警判定规则说明：
            // - 真空压力通常是负数，绝对值越大真空越好
            // - “阈值”是工艺定义：例如 -95000 Pa
            // - AlarmWhenPressureHigherThanThreshold=true 的含义：
            //   当压力值变“更高”（更接近 0）时，代表真空变差，触发报警
            if (_config.AlarmWhenPressureHigherThanThreshold)
            {
                return pressurePa > _config.AlarmPressureThresholdPa;
            }

            return pressurePa < _config.AlarmPressureThresholdPa;
        }

        private void HandleAlarm(int deviceId)
        {
            int valveOutputId = _config.TotalInputs + deviceId;
            int carrierOutputId = _config.TotalInputs + _config.TotalBarometers + deviceId;

            // 报警联动动作（当前策略）：
            // - 关真空阀（防止继续抽真空/泄漏等异常扩大）
            // - 断载台上电（保护被测件/治具）
            //
            // 现场确认项：
            // - 是否需要“报警解除后自动恢复”？
            // - 关闭/断电是否需要保持，是否需要人工复位？
            _ioController.WriteOutput(valveOutputId, false);
            _ioController.WriteOutput(carrierOutputId, false);

            // TODO: 待现场确认报警动作（关闭阀/断电是否需要保持，报警解除后是否自动恢复）
        }

        /// <summary>
        /// 释放资源
        /// 实现 IDisposable 接口，供主窗体关闭时调用
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源的实际实现
        /// 【修复 H3】先标记 _disposed=true 再 Stop，避免 Stop 中触发事件回调到已释放的 UI
        /// 【修复 M4】取消所有事件订阅，避免内存泄漏
        /// </summary>
        /// <param name="disposing">true 表示手动释放，false 表示终结器调用</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                // 先标记为已释放，Stop 中检测到此标志后不再触发事件
                _disposed = true;

                if (disposing)
                {
                    // 释放托管资源
                    Stop();

                    // 取消错误事件订阅（避免内存泄漏）
                    _barometerReader.OnError -= BarometerReader_OnError;
                    _ioController.OnError -= IoController_OnError;

                    // 释放定时器
                    _collectTimer?.Dispose();
                }
            }
        }
    }
}
