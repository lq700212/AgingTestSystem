
using System;
using System.Threading;
using BarometerWinform.Interfaces;
using BarometerWinform.Models;

namespace BarometerWinform.Services
{
    /// <summary>
    /// 冷却送风机模拟实现
    /// 用于开发和演示阶段（App.config 里 UseMockCommunication=true 时启用）
    ///
    /// 【与真实实现的区别】
    /// - 真实实现 FanControllerClient：走 Modbus TCP，与厂商控制屏通讯
    /// - 本模拟实现：不连任何硬件，温度随机波动，命令直接生效
    ///
    /// 【设计说明】（给新手看的）
    /// 有了 Mock，即使现场没有接线、没有送风机，也可以先跑通整套 UI 和业务流程：
    /// 点"送风机定值启动" → 状态变成"定值运行中" → 温度开始波动；
    /// 点"定值停止" → 状态变成"已停止"。
    /// </summary>
    public class MockFanController : IFanController
    {
        /// <summary>连接状态标志</summary>
        private bool _isConnected;

        /// <summary>设备配置（由 Connect 方法赋值）</summary>
        private DeviceConfig _config;

        /// <summary>
        /// 模拟"运行中"标志
        /// true 表示定值启动后正在运行，false 表示已停止
        /// </summary>
        private bool _running;

        /// <summary>
        /// 模拟当前温度（单位：°C）
        /// 起始 35°C（老化箱常见温度），运行后在其附近随机波动
        /// </summary>
        private float _currentTemp = 35f;

        /// <summary>模拟当前湿度（单位：%RH）</summary>
        private float _currentHumidity = 60f;

        /// <summary>温度设定值（°C，模拟值，真实设备由厂商控制屏设定）</summary>
        private float _tempSetpoint = 35f;

        /// <summary>湿度设定值（%RH，模拟值）</summary>
        private float _humiditySetpoint = 60f;

        /// <summary>
        /// 随机数生成器
        /// 【线程安全】Random 非线程安全，用 _randomLock 保护（与 MockIoController 一致）
        /// </summary>
        private readonly Random _random = new Random();

        /// <summary>随机数生成器的锁对象</summary>
        private readonly object _randomLock = new object();

        public bool IsConnected => _isConnected;

        /// <summary>
        /// 实际连接成功的送风机 IP（模拟实现：无真实设备，返回配置里的主 IP）
        /// </summary>
        public string ActiveIp => _config?.FanIpAddress;

        public event EventHandler<string> OnError;

        public bool Connect(DeviceConfig config)
        {
            _config = config;
            // 模拟连接耗时，让 UI 有"正在连接"的反馈
            Thread.Sleep(200);
            _isConnected = true;
            return true;
        }

        public void Disconnect()
        {
            _isConnected = false;
        }

        /// <summary>
        /// 按需重连（【V1.16.1 新增】接口成员）
        /// 模拟实现：直接返回"已连接"（Mock 没有真实掉线概念，重新 Connect 即恢复）。
        /// </summary>
        public bool ReconnectNow()
        {
            if (_isConnected) return true;
            Connect(_config);
            return _isConnected;
        }

        /// <summary>
        /// 读取送风机当前状态（模拟）
        /// 温度在运行状态下围绕设定值小幅随机波动，更接近真实设备表现
        /// </summary>
        public FanData ReadStatus()
        {
            if (!_isConnected)
            {
                OnError?.Invoke(this, "设备未连接");
                return null;
            }

            lock (_randomLock)
            {
                // 运行中：温度围绕设定值波动 ±0.3°C；停止时：温度缓慢回归室温方向
                if (_running)
                {
                    _currentTemp += (float)(_random.NextDouble() - 0.5) * 0.6f;
                }
                else
                {
                    // 停止后温度缓慢下降（向 30°C 靠近）
                    _currentTemp += (30f - _currentTemp) * 0.05f;
                }
                // 湿度小幅波动
                _currentHumidity += (float)(_random.NextDouble() - 0.5) * 0.4f;
            }

            return new FanData
            {
                RunState = _running ? FanRunState.FixedValueRunning : FanRunState.FixedValueStopped,
                Temperature = _currentTemp,
                Humidity = _currentHumidity,
                TempSetpoint = _tempSetpoint,
                HumSetpoint = _humiditySetpoint,
                IsOnline = true,
                CollectTime = DateTime.Now
            };
        }

        /// <summary>
        /// 定值启动（模拟）：直接把运行标志置为 true
        /// </summary>
        public bool StartFixedValue()
        {
            _running = true;
            return true;
        }

        /// <summary>
        /// 定值停止（模拟）：直接把运行标志置为 false
        /// </summary>
        public bool Stop()
        {
            _running = false;
            return true;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Disconnect();
        }
    }
}
