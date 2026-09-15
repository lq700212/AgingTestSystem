
using System;
using System.Collections.Generic;
using System.Threading;
using AgingTestSystem.Interfaces;
using AgingTestSystem.Models;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 气压表数据读取模拟实现
    /// 用于开发和测试阶段，模拟真实气压表数据
    /// 实际使用时需要替换为真实的硬件通信实现
    /// 【修复说明】
    /// 修复 M5：使用 lock 保护 Random，避免多线程访问导致内部状态损坏
    /// 修复 M6：ReadAllData 增加 _config 判空，避免未 Connect 时抛 NullReferenceException
    /// 修复 L9：ReadData 增加 deviceId 边界校验
    /// 【与真实实现的区别（测试写用例前必读，防假绿）】
    /// - Status 恒 Idle：状态机归 DeviceManager 写，Mock 只给压力（真实端按阈值判 Fault/Idle，
    ///   报警语义走 DeviceManagerIntegration 端到端用例，不直接断言 Mock 状态）；
    /// - SN/配方/延时/IO 列是演示填充（面板有字、流程能跑），阈值写入是空操作恒 true；
    ///   真实施压阈值联动走"DeviceManager 回填覆盖"路径，用例断言 DeviceManager 行为。
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
            // 【大扫荡】空配置拒绝（与 MockIoController/MockFanController 同口径）。
            if (config == null) return false;
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
            // 依据IO分配表: 1输入(真空负压表) + 2输出(真空电磁阀 + 载台上电)
            int pressureInt;
            int delayTimeMin, delayTimeSec;
            int burnInMin, burnInSec;
            bool vacuumPressureInput;      // 真空负压表输入(NPN, X地址)
            bool vacuumValveOutput;        // 真空电磁阀输出(PNP, Y地址)
            bool carrierPowerOutput;       // 载台上电输出(PNP, Y地址)

            lock (_randomLock)
            {
                // 模拟生成气压数据（V1.10 改为偏向"真空良好"，让 Demo 流程更真实；
                //  V1.19.9 单位由 Pa 改为 kPa，与真实读取器/报警阈值一致；
                //  V1.61 阈值默认改 -5kPa，两档区间同步下移，保持"良好低于阈值/较差高于阈值"）：
                // - 85% 概率生成"真空良好"：-6 ~ -10 kPa（低于报警阈值 -5，不报警）
                //   这样点"启动运行"后真空能正常建立，演示"测试中→老化计时→自动停止"的完整流程
                // - 15% 概率生成"真空较差"：0 ~ -4 kPa（高于阈值，触发报警联动演示）
                if (_random.Next(100) < 85)
                {
                    pressureInt = -_random.Next(6, 11);       // 真空良好（低于 -5 阈值）
                }
                else
                {
                    pressureInt = -_random.Next(0, 5);        // 真空较差（高于阈值，会报警）
                }

                delayTimeMin = _random.Next(0, 30);
                delayTimeSec = _random.Next(0, 60);
                burnInMin = _random.Next(0, 60);
                burnInSec = _random.Next(0, 60);
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
                DelayTime = new TimeSpan(0, delayTimeMin, delayTimeSec),
                BurnInTime = new TimeSpan(0, burnInMin, burnInSec),
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
                // 返回"全 null 数组"，让 DeviceManager 的
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
        /// 串口未连接时返回空字典，让上层走"未连接"提示分支。
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
