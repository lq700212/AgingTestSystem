
using System;
using BarometerWinform.Models;

namespace BarometerWinform.Interfaces
{
    /// <summary>
    /// IO控制器接口
    /// 定义了对IO输入/输出进行控制的标准方法
    /// 
    /// 【预留说明】
    /// 当前接口为抽象定义，IO通信协议尚未确定。
    /// 现场有72个IO输入和144个IO输出需要接入。
    /// 后续需要根据实际使用的IO模块通信协议（如PLC协议、IO采集卡协议等）
    /// 实现具体的控制类。
    /// 
    /// 接入方式：
    /// 1. 创建新类实现此接口，如 PlcIoController
    /// 2. 在 DeviceManager 中替换 MockIoController 为实际实现
    /// 3. 根据协议实现 Connect、ReadInput、WriteOutput、Disconnect 方法
    /// 
    /// IO点编号规则（暂定）：
    /// - 输入点：1-72（对应72个输入通道）
    /// - 输出点：73-216（对应144个输出通道，72+144=216）
    /// 
    /// 每个气压表对应：
    /// - 2个输入点：用于检测传感器信号
    /// - 4个输出点：用于控制执行器
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
        /// <param name="inputId">输入点编号（1-72）</param>
        /// <returns>输入点状态，true为高电平/导通</returns>
        bool ReadInput(int inputId);

        /// <summary>
        /// 批量读取所有输入点状态
        /// </summary>
        /// <returns>输入点状态数组，索引0对应输入点1</returns>
        bool[] ReadAllInputs();

        /// <summary>
        /// 写入单个输出点状态
        /// </summary>
        /// <param name="outputId">输出点编号（73-216）</param>
        /// <param name="state">输出状态，true为高电平/导通</param>
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
        /// <param name="outputId">输出点编号（73-216）</param>
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
