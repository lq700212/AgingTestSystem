
using System;
using System.Collections.Generic;
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

        /// <summary>
        /// 当前实际使用的串口名称（模拟实现：返回配置里填的端口）
        /// </summary>
        public string CurrentPortName => _config?.PortName;

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
            // 【V1.09 更新】依据显耀IO表: 1输入(真空负压表) + 2输出(真空电磁阀 + 载台上电)
            int pressureInt;
            int delayStartMin, delayStartSec;
            int delayArriveMin, delayArriveSec;
            bool vacuumPressureInput;      // 真空负压表输入(NPN, X地址)
            bool vacuumValveOutput;        // 真空电磁阀输出(PNP, Y地址)
            bool carrierPowerOutput;       // 载台上电输出(PNP, Y地址)

            lock (_randomLock)
            {
                // 模拟生成气压数据（V1.10 改为偏向"真空良好"，让 Demo 流程更真实）：
                // - 85% 概率生成"真空良好"：-96000 ~ -100000 Pa（低于报警阈值 -95000，不报警）
                //   这样点"启动运行"后真空能正常建立，演示"测试中→老化计时→自动停止"的完整流程
                // - 15% 概率生成"真空较差"：-1000 ~ -90000 Pa（高于阈值，触发报警联动演示）
                if (_random.Next(100) < 85)
                {
                    pressureInt = -_random.Next(96000, 100001); // 真空良好（低于 -95000 阈值）
                }
                else
                {
                    pressureInt = -_random.Next(1000, 90001);   // 真空较差（高于阈值，会报警）
                }

                delayStartMin = _random.Next(0, 30);
                delayStartSec = _random.Next(0, 60);
                delayArriveMin = _random.Next(0, 60);
                delayArriveSec = _random.Next(0, 60);
                vacuumPressureInput = _random.Next(0, 2) == 1;
                vacuumValveOutput = _random.Next(0, 2) == 1;
                carrierPowerOutput = _random.Next(0, 2) == 1;
            }

            return new BarometerData
            {
                DeviceId = deviceId,
                VacuumPressure = pressureInt,
                SerialNumber = $"SN{deviceId:D4}",
                RecipeName = $"配方{deviceId % 5 + 1}",
                // V1.10：状态统一由 DeviceManager 根据测试状态/报警判定来写，
                // Mock 读取器只负责提供压力数据，避免随机状态误导 Demo
                Status = DeviceStatus.Idle,
                DelayStartTime = new TimeSpan(0, delayStartMin, delayStartSec),
                DelayArriveTime = new TimeSpan(0, delayArriveMin, delayArriveSec),
                CollectTime = DateTime.Now,
                // 1个输入: 真空负压表信号
                InputStatus = new[] { vacuumPressureInput },
                // 2个输出: 真空电磁阀 + 载台上电
                OutputStatus = new[] { vacuumValveOutput, carrierPowerOutput }
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
                // 【V1.16.2 对齐真实实现】返回"全 null 数组"，让 DeviceManager 的
                // 逐台循环能累加失败次数并触发"通讯故障"联动（与真实串口断开行为一致）
                OnError?.Invoke(this, "设备未连接（等待自动重连）");
                return new BarometerData[_config.TotalBarometers];
            }

            var data = new BarometerData[_config.TotalBarometers];
            for (int i = 0; i < _config.TotalBarometers; i++)
            {
                data[i] = ReadData(i + 1);
            }
            return data;
        }

        /// <summary>
        /// 模拟写入单台气压表的设备阈值
        /// Mock 无真实设备，固定返回 true（仅保证接口一致，便于上层联调流程）。
        /// </summary>
        public bool SetThreshold(int deviceId, decimal thresholdValue)
        {
            // 与 ReadData 一致：未连接 / 越界 时按失败处理，便于上层验证边界逻辑
            if (!_isConnected || _config == null || deviceId < 1 || deviceId > _config.TotalBarometers)
            {
                OnError?.Invoke(this, $"设备编号 {deviceId} 超出合法范围 [1, {_config?.TotalBarometers ?? 0}]");
                return false;
            }
            return true; // Mock 模拟成功
        }

        /// <summary>
        /// 模拟批量写入所有气压表的设备阈值
        /// 逐台调用 <see cref="SetThreshold"/>，返回 deviceId → 是否成功。
        /// 【V1.16 对齐】串口未连接时返回空字典，让上层走"未连接"提示分支。
        /// </summary>
        public Dictionary<int, bool> SetAllThresholds(decimal thresholdValue)
        {
            var result = new Dictionary<int, bool>();
            if (_config == null || !_isConnected)
            {
                OnError?.Invoke(this, "未连接，请先调用 Connect 方法");
                return result;
            }

            for (int i = 1; i <= _config.TotalBarometers; i++)
            {
                result[i] = SetThreshold(i, thresholdValue);
            }
            return result;
        }
    }
}
