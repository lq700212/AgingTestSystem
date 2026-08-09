
using System;
using AgingTestSystem.Models;

namespace AgingTestSystem.Interfaces
{
    /// <summary>
    /// IO控制器接口
    /// 定义了对IO输入/输出进行控制的标准方法
    ///
    /// 【V1.09 更新 —— 显耀IO表接入】
    /// 现场IO配置已明确，依据"显耀IO表":
    /// - 输入: 72 个 NPN 型输入点, 三菱PLC X 地址(八进制编址)
    /// - 输出: 144 个 PNP 型输出点, 三菱PLC Y 地址(八进制编址)
    ///
    /// 【内部编号 vs 物理地址】
    /// 本接口的方法参数(inputId/outputId)使用"内部十进制连续编号":
    ///   输入: 1 ~ TotalInputs (1 ~ 72)
    ///   输出: TotalInputs+1 ~ TotalInputs+TotalOutputs (73 ~ 216)
    /// 与硬件通信时，需通过 <see cref="Services.IoMapBuilder"/> 转换为物理地址。
    ///
    /// 【IO点编号规则】(依据显耀IO表)
    /// 输入点(1~72, NPN, X地址):
    ///   内部编号 n → 物理地址 X + octal(n-1)
    ///   1→X000, 8→X007, 9→X010, 72→X107
    ///   设备名: 真空负压表-1 ~ 真空负压表-72
    ///
    /// 输出点(73~216, PNP, Y地址):
    ///   真空电磁阀(内部 73~144): n→Y + octal(n-1), 1→Y000, 72→Y107
    ///   载台上电(内部 145~216): n→Y + octal(72+n-1), 1→Y110, 72→Y217
    ///
    /// 每个气压表对应: 1输入(真空负压表) + 2输出(真空电磁阀 + 载台上电)
    ///
    /// 【电气特性说明】
    /// - 输入 NPN(漏型): 传感器导通时拉低信号到 0V, IO模块内部上拉后识别为"导通"。
    ///   适合 NPN 型接近开关、光电传感器。
    /// - 输出 PNP(源型): 输出导通时输出 +24V 高电平, 向外提供电流。
    ///   适合直接驱动中间继电器线圈(继电器另一端接 0V),
    ///   再由继电器触点控制大功率负载(电磁阀、载台电源)。
    ///
    /// 【预留说明】
    /// 当前接口为抽象定义，IO通信协议尚未确定。
    /// 当前使用 <see cref="Services.MockIoController"/> 作为模拟实现。
    /// 实际使用时需要根据现场PLC/IO模块的通信协议(如三菱MC协议、Modbus TCP等)
    /// 实现具体的控制类(如 PlcIoController)。
    ///
    /// 接入方式:
    /// 1. 创建新类实现此接口, 如 PlcIoController
    /// 2. 在 DeviceManager 中替换 MockIoController 为实际实现
    /// 3. 根据协议实现 Connect、ReadInput、WriteOutput、Disconnect 方法
    /// 4. 使用 IoMapBuilder.GetDeviceMapping(deviceId, totalBarometers) 获取物理地址进行通信
    /// </summary>
    public interface IIoController
    {
        /// <summary>
        /// 连接状态
        /// true表示已连接，false表示未连接
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 连接IO设备
        /// </summary>
        /// <param name="config">设备配置参数</param>
        /// <returns>是否连接成功</returns>
        bool Connect(DeviceConfig config);

        /// <summary>
        /// 断开连接
        /// </summary>
        void Disconnect();

        /// <summary>
        /// 读取单个输入点状态
        /// </summary>
        /// <param name="inputId">输入点内部编号（1 ~ TotalInputs，默认 1 ~ 72）
        /// 对应物理地址 X000 ~ X107(八进制), 详见 IoMapBuilder</param>
        /// <returns>输入点状态，true为导通(NPN传感器拉低电平)，false为断开</returns>
        bool ReadInput(int inputId);

        /// <summary>
        /// 批量读取所有输入点状态
        /// </summary>
        /// <returns>输入点状态数组，索引0对应输入点1(X000)</returns>
        bool[] ReadAllInputs();

        /// <summary>
        /// 写入单个输出点状态
        /// </summary>
        /// <param name="outputId">输出点内部编号（TotalInputs+1 ~ TotalInputs+TotalOutputs，默认 73 ~ 216）
        /// 73~144 对应 Y000~Y107(真空电磁阀), 145~216 对应 Y110~Y217(载台上电)</param>
        /// <param name="state">输出状态，true为导通(PNP输出+24V驱动继电器)，false为断开</param>
        void WriteOutput(int outputId, bool state);

        /// <summary>
        /// 批量写入输出点状态
        /// </summary>
        /// <param name="outputIds">输出点编号数组</param>
        /// <param name="states">输出状态数组，与outputIds一一对应</param>
        void WriteOutputs(int[] outputIds, bool[] states);

        /// <summary>
        /// 读取单个输出点状态（用于回读确认）
        /// </summary>
        /// <param name="outputId">输出点内部编号（73 ~ 216）</param>
        /// <returns>输出点状态</returns>
        bool ReadOutput(int outputId);

        /// <summary>
        /// 批量读取所有输出点状态
        /// </summary>
        /// <returns>输出点状态数组</returns>
        bool[] ReadAllOutputs();

        /// <summary>
        /// 读取失败时的事件通知
        /// </summary>
        event EventHandler<string> OnError;
    }
}
