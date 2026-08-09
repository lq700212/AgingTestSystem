
using System;
using System.Threading;
using AgingTestSystem.Interfaces;
using AgingTestSystem.Models;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// IO控制器模拟实现
    /// 用于开发和测试阶段，模拟IO输入输出操作
    /// 实际使用时需要替换为真实的硬件通信实现
    ///
    /// 【V1.09 更新 —— 显耀IO表】
    /// IO点编号规则(依据显耀IO表):
    /// - 输入点(NPN, X地址): 1 ~ TotalInputs(默认 1 ~ 72)
    ///   对应物理地址 X000~X107(三菱八进制), 设备名: 真空负压表-1~72
    /// - 输出点(PNP, Y地址): TotalInputs+1 ~ TotalInputs+TotalOutputs(默认 73 ~ 216)
    ///   真空电磁阀(73~144): Y000~Y107
    ///   载台上电(145~216): Y110~Y217
    /// - 物理地址映射详见 <see cref="IoMapBuilder"/>
    ///
    /// 【修复说明】
    /// 修复 M5：使用 lock 保护 Random，避免多线程访问导致内部状态损坏
    /// 修复 M7：将硬编码的 73/216 替换为基于 _config.TotalInputs/TotalOutputs 的动态计算
    /// </summary>
    public class MockIoController : IIoController
    {
        /// <summary>连接状态标志</summary>
        private bool _isConnected;

        /// <summary>设备配置（由 Connect 方法赋值）</summary>
        private DeviceConfig _config;

        /// <summary>
        /// 存储所有输入点状态（索引0对应输入点1）
        /// </summary>
        private bool[] _inputStates;

        /// <summary>
        /// 存储所有输出点状态（索引0对应输出点 TotalInputs+1）
        /// </summary>
        private bool[] _outputStates;

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

            // 初始化输入状态数组（默认72个输入）
            _inputStates = new bool[config.TotalInputs];
            // 【修复 M5】使用 lock 保护 Random 访问
            lock (_randomLock)
            {
                for (int i = 0; i < config.TotalInputs; i++)
                {
                    _inputStates[i] = _random.Next(0, 2) == 1;
                }
            }

            // 初始化输出状态数组（默认144个输出）
            _outputStates = new bool[config.TotalOutputs];

            Thread.Sleep(300);
            _isConnected = true;
            return true;
        }

        public void Disconnect()
        {
            _isConnected = false;
        }

        /// <summary>
        /// 读取单个输入点状态
        /// </summary>
        /// <param name="inputId">输入点编号（1 ~ TotalInputs）</param>
        public bool ReadInput(int inputId)
        {
            if (!_isConnected)
            {
                OnError?.Invoke(this, "设备未连接");
                return false;
            }

            if (inputId < 1 || inputId > _config.TotalInputs)
            {
                OnError?.Invoke(this, $"无效的输入点编号: {inputId}");
                return false;
            }

            // 模拟输入状态变化（5% 概率翻转）
            lock (_randomLock)
            {
                if (_random.Next(0, 100) < 5)
                {
                    _inputStates[inputId - 1] = !_inputStates[inputId - 1];
                }
            }

            return _inputStates[inputId - 1];
        }

        /// <summary>
        /// 批量读取所有输入点状态
        /// </summary>
        public bool[] ReadAllInputs()
        {
            if (!_isConnected)
            {
                OnError?.Invoke(this, "设备未连接");
                return new bool[0];
            }

            // 更新所有输入状态（模拟实时变化，3% 概率翻转）
            lock (_randomLock)
            {
                for (int i = 0; i < _inputStates.Length; i++)
                {
                    if (_random.Next(0, 100) < 3)
                    {
                        _inputStates[i] = !_inputStates[i];
                    }
                }
            }

            // 返回副本，避免外部修改内部状态
            return (bool[])_inputStates.Clone();
        }

        /// <summary>
        /// 写入单个输出点状态
        ///
        /// 【修复 M7】原硬编码 outputId &lt; 73 || outputId &gt; 216 改为动态计算
        /// 输出点起始编号 = TotalInputs + 1
        /// 输出点结束编号 = TotalInputs + TotalOutputs
        /// </summary>
        /// <param name="outputId">输出点编号（TotalInputs+1 ~ TotalInputs+TotalOutputs）</param>
        /// <param name="state">要写入的状态</param>
        public void WriteOutput(int outputId, bool state)
        {
            if (!_isConnected)
            {
                OnError?.Invoke(this, "设备未连接");
                return;
            }

            // 【修复 M7】动态计算输出点合法范围
            int outputStart = _config.TotalInputs + 1;
            int outputEnd = _config.TotalInputs + _config.TotalOutputs;

            if (outputId < outputStart || outputId > outputEnd)
            {
                OnError?.Invoke(this, $"无效的输出点编号: {outputId}（合法范围 {outputStart}-{outputEnd}）");
                return;
            }

            // 数组索引 = 编号 - 起始编号
            _outputStates[outputId - outputStart] = state;
        }

        /// <summary>
        /// 批量写入多个输出点状态
        /// </summary>
        public void WriteOutputs(int[] outputIds, bool[] states)
        {
            if (!_isConnected)
            {
                OnError?.Invoke(this, "设备未连接");
                return;
            }

            // 防御性检查：参数为空时返回
            if (outputIds == null || states == null)
            {
                OnError?.Invoke(this, "参数不能为空");
                return;
            }

            if (outputIds.Length != states.Length)
            {
                OnError?.Invoke(this, "输出点编号和状态数量不一致");
                return;
            }

            for (int i = 0; i < outputIds.Length; i++)
            {
                WriteOutput(outputIds[i], states[i]);
            }
        }

        /// <summary>
        /// 读取单个输出点状态
        ///
        /// 【修复 M7】同 WriteOutput，动态计算输出点合法范围
        /// </summary>
        public bool ReadOutput(int outputId)
        {
            if (!_isConnected)
            {
                OnError?.Invoke(this, "设备未连接");
                return false;
            }

            // 【修复 M7】动态计算输出点合法范围
            int outputStart = _config.TotalInputs + 1;
            int outputEnd = _config.TotalInputs + _config.TotalOutputs;

            if (outputId < outputStart || outputId > outputEnd)
            {
                OnError?.Invoke(this, $"无效的输出点编号: {outputId}（合法范围 {outputStart}-{outputEnd}）");
                return false;
            }

            return _outputStates[outputId - outputStart];
        }

        /// <summary>
        /// 批量读取所有输出点状态
        /// </summary>
        public bool[] ReadAllOutputs()
        {
            if (!_isConnected)
            {
                OnError?.Invoke(this, "设备未连接");
                return new bool[0];
            }

            // 返回副本，避免外部修改内部状态
            return (bool[])_outputStates.Clone();
        }
    }
}
