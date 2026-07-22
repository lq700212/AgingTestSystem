
using System;
using System.Threading;
using BarometerWinform.Interfaces;
using BarometerWinform.Models;

namespace BarometerWinform.Services
{
    /// <summary>
    /// 气压表数据读取模拟实现
    /// 用于开发和测试阶段，模拟真实气压表数据
    /// 实际使用时需要替换为真实的硬件通信实现
    ///
    /// 【修复说明】
    /// 修复 M5：使用 lock 保护 Random，避免多线程访问导致内部状态损坏
    /// 修复 M6：ReadAllData 增加 _config 判空，避免未 Connect 时抛 NullReferenceException
    /// 修复 L9：ReadData 增加 deviceId 边界校验
    /// </summary>
    public class MockBarometerReader : IBarometerReader
    {
        /// <summary>连接状态标志</summary>
        private bool _isConnected;

        /// <summary>设备配置（由 Connect 方法赋值）</summary>
        private DeviceConfig _config;

        /// <summary>
        /// 随机数生成器
        /// 【修复 M5】Random 非线程安全，使用 lock 保护
        /// 避免并发访问导致返回 0 或抛异常
        /// </summary>
        private readonly Random _random = new Random();

        /// <summary>
        /// 随机数生成器的锁对象
        /// 保护 _random 的所有访问
        /// </summary>
        private readonly object _randomLock = new object();

        public bool IsConnected => _isConnected;

        public event EventHandler<string> OnError;

        public bool Connect(DeviceConfig config)
        {
            _config = config;
            Thread.Sleep(500);
            _isConnected = true;
            return true;
        }

        public void Disconnect()
        {
            _isConnected = false;
        }

        /// <summary>
        /// 读取单个气压表数据
        /// 修复 L9：增加 deviceId 边界校验
        /// </summary>
        public BarometerData ReadData(int deviceId)
        {
            if (!_isConnected)
            {
                OnError?.Invoke(this, "设备未连接");
                return null;
            }

            // 修复 L9：校验 deviceId 是否在合法范围
            if (_config == null || deviceId < 1 || deviceId > _config.TotalBarometers)
            {
                OnError?.Invoke(this, $"设备编号 {deviceId} 超出合法范围 [1, {_config?.TotalBarometers ?? 0}]");
                return null;
            }

            // 使用 lock 保护 Random 访问（修复 M5）
            int pressureInt;
            int statusInt;
            int delayStartMin, delayStartSec;
            int delayArriveMin, delayArriveSec;
            bool in1, in2, out1, out2, out3, out4;

            lock (_randomLock)
            {
                // 模拟生成气压数据，范围在 -1000 到 -100000 Pa 之间（真空度）
                pressureInt = -_random.Next(1000, 100000);
                statusInt = _random.Next(0, 3);
                delayStartMin = _random.Next(0, 30);
                delayStartSec = _random.Next(0, 60);
                delayArriveMin = _random.Next(0, 60);
                delayArriveSec = _random.Next(0, 60);
                in1 = _random.Next(0, 2) == 1;
                in2 = _random.Next(0, 2) == 1;
                out1 = _random.Next(0, 2) == 1;
                out2 = _random.Next(0, 2) == 1;
                out3 = _random.Next(0, 2) == 1;
                out4 = _random.Next(0, 2) == 1;
            }

            return new BarometerData
            {
                DeviceId = deviceId,
                VacuumPressure = pressureInt,
                SerialNumber = $"SN{deviceId:D4}",
                RecipeName = $"配方{deviceId % 5 + 1}",
                Status = (DeviceStatus)statusInt,
                DelayStartTime = new TimeSpan(0, delayStartMin, delayStartSec),
                DelayArriveTime = new TimeSpan(0, delayArriveMin, delayArriveSec),
                CollectTime = DateTime.Now,
                InputStatus = new[] { in1, in2 },
                OutputStatus = new[] { out1, out2, out3, out4 }
            };
        }

        /// <summary>
        /// 批量读取所有气压表数据
        /// 修复 M6：增加 _config 判空，未 Connect 时触发 OnError 事件
        /// </summary>
        public BarometerData[] ReadAllData()
        {
            // 修复 M6：未 Connect 时返回空数组，避免 NullReferenceException
            if (_config == null)
            {
                OnError?.Invoke(this, "未连接，请先调用 Connect 方法");
                return new BarometerData[0];
            }

            if (!_isConnected)
            {
                OnError?.Invoke(this, "设备未连接");
                return new BarometerData[0];
            }

            var data = new BarometerData[_config.TotalBarometers];
            for (int i = 0; i < _config.TotalBarometers; i++)
            {
                data[i] = ReadData(i + 1);
            }
            return data;
        }
    }
}
