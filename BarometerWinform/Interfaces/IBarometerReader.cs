
using System;
using BarometerWinform.Models;

namespace BarometerWinform.Interfaces
{
    /// <summary>
    /// 气压表数据读取接口
    /// 定义了从气压表设备读取数据的标准方法
    /// 
    /// 【预留说明】
    /// 当前接口为抽象定义，实际协议尚未确定。
    /// 后续需要根据现场实际使用的气压表通信协议（如Modbus、RS232自定义协议等）
    /// 实现具体的读取类。
    /// 
    /// 接入方式：
    /// 1. 创建新类实现此接口，如 ModbusBarometerReader
    /// 2. 在 DeviceManager 中替换 MockBarometerReader 为实际实现
    /// 3. 根据协议实现 Connect、ReadData、Disconnect 方法
    /// </summary>
    public interface IBarometerReader
    {
        /// <summary>
        /// 连接状态
        /// true表示已连接，false表示未连接
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 连接气压表设备
        /// </summary>
        /// <param name="config">设备配置参数</param>
        /// <returns>是否连接成功</returns>
        bool Connect(DeviceConfig config);

        /// <summary>
        /// 断开连接
        /// </summary>
        void Disconnect();

        /// <summary>
        /// 读取单个气压表数据
        /// </summary>
        /// <param name="deviceId">气压表编号（1-72）</param>
        /// <returns>气压表数据，如果读取失败返回null</returns>
        BarometerData ReadData(int deviceId);

        /// <summary>
        /// 批量读取所有气压表数据
        /// </summary>
        /// <returns>包含所有气压表数据的数组</returns>
        BarometerData[] ReadAllData();

        /// <summary>
        /// 读取失败时的事件通知
        /// </summary>
        event EventHandler<string> OnError;
    }
}
