
using System;
using System.Threading;
using AgingTestSystem.Interfaces;
using AgingTestSystem.Models;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 载台电流表模拟实现（Q2 通用骨架的 Mock 端）。
    /// 用于开发和演示阶段（UseMockCommunication=true 且 UsePowerMeter=true 时启用）。
    /// 【与真实实现的区别】
    /// - 真实实现（以后写）：走电表协议，读真实电流；
    /// - 本模拟实现：不连任何硬件，每路电流在 0.05~0.60A 之间缓慢随机漂移
    ///   （模拟载台待机/老化负载），上电工位偏大、未上电工位偏小——演示面板悬停
    ///   与 CSV 落盘时肉眼可辨即可，不追求电气真实性。
    /// 【设计说明】（给新手看的）
    /// 写法与 MockFanController 同模式：Connect 睡 200ms 模拟连接耗时；
    /// Random 非线程安全，用 _randomLock 保护；状态数组按 deviceCount 现配现建。
    /// </summary>
    public class MockPowerMeter : IPowerMeter
    {
        /// <summary>连接状态标志</summary>
        private bool _isConnected;

        /// <summary>设备配置（由 Connect 方法赋值，现状仅存引用，电表参数以后加）</summary>
        private DeviceConfig _config;

        /// <summary>
        /// 每路电流当前模拟值（下标 0 = 1 号工位；读一次漂移一次，模拟负载波动）。
        /// null = 还没按总数建表（Connect 后第一次读时建）。
        /// </summary>
        private float[] _currents;

        /// <summary>
        /// 随机数生成器
        /// 【线程安全】Random 非线程安全，用 _randomLock 保护（与 MockFanController 一致）
        /// </summary>
        private readonly Random _random = new Random();

        /// <summary>随机数生成器的锁对象</summary>
        private readonly object _randomLock = new object();

        public bool IsConnected => _isConnected;

        public event EventHandler<string> OnError;

        public bool Connect(DeviceConfig config)
        {
            // 【大扫荡】空配置拒绝（与 MockIoController/MockFanController 同口径；
            // 以前 null 也"连上"，ReconnectNow 从未 Connect 时也能假连上）。
            if (config == null) return false;
            _config = config;
            // 重上电=漂移表清零（以前断线重连延续旧曲线，与真表行为不符）。
            _currents = null;
            // 模拟连接耗时，让 UI 有"正在连接"的反馈（与 MockFanController 同值）
            Thread.Sleep(200);
            _isConnected = true;
            return true;
        }

        public void Disconnect()
        {
            _isConnected = false;
            _currents = null;   // 断连清表：下次连接从待机初值重新漂移
        }

        public bool ReconnectNow()
        {
            if (_isConnected) return true;
            return Connect(_config);
        }

        public float[] ReadAllCurrents(int deviceCount)
        {
            if (!_isConnected || deviceCount <= 0) return null;
            lock (_randomLock)
            {
                // 总数变了（换项目/改配置重启）就重建表，老值丢弃（模拟值无追溯意义）
                if (_currents == null || _currents.Length != deviceCount)
                {
                    _currents = new float[deviceCount];
                    for (int i = 0; i < deviceCount; i++)
                    {
                        // 初值 0.10~0.30A（载台待机量级，演示用，不代表真实功耗）
                        _currents[i] = 0.10f + (float)_random.NextDouble() * 0.20f;
                    }
                }
                var result = new float[deviceCount];
                for (int i = 0; i < deviceCount; i++)
                {
                    // 每次读漂移 ±0.02A，钳在 0.05~0.60A（防随机游走到负数/过大）
                    float drift = ((float)_random.NextDouble() - 0.5f) * 0.04f;
                    float v = _currents[i] + drift;
                    if (v < 0.05f) v = 0.05f;
                    if (v > 0.60f) v = 0.60f;
                    _currents[i] = v;
                    result[i] = v;
                }
                return result;
            }
        }

        public void Dispose()
        {
            // Mock 无非托管资源：断开即释放（与 MockFanController 同口径）
            _isConnected = false;
        }
    }
}
