
using System;
using System.Collections.Generic;
using BarometerWinform.Models;

namespace BarometerWinform.Interfaces
{
    /// <summary>
    /// 气压表数据读取接口
    /// 定义了从气压表设备读取数据 / 写入阈值的标准方法
    ///
    /// 【协议口径（已定论）】
    /// 协议以 ModbusRtuBarometerTest Demo 实测为准：
    /// - 读压力：Input Register 0x0001（功能码 0x04），同时读 0x0002 取小数位
    /// - 写阈值：Holding Register 0x0010（功能码 0x06），值 = round(阈值 × 10^小数位)
    /// 真实实现见 <see cref="Services.ModbusRtuBarometerReader"/>，模拟实现见
    /// <see cref="Services.MockBarometerReader"/>（UseMockCommunication=true 时使用）。
    ///
    /// 【线程说明】
    /// 实现类必须保证线程安全：SerialPort / Modbus Master 不支持并发读写。
    /// 真实实现用 _syncRoot 互斥锁串行化所有请求（含 SetThreshold）。
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
        /// 写入单台气压表的设备阈值（Holding Register 0x0010）
        ///
        /// 【单位说明（重要）】
        /// 参数 thresholdValue 是"设备单位"，即与压力读数同单位、同小数位的值
        /// （寄存器值 = round(thresholdValue × 10^小数位)，与 Demo 一致）。
        /// 它 ≠ 软件报警阈值 AlarmPressureThresholdPa（Pa），两者是不同概念：
        /// - 软件阈值：上位机内存里比较压力用的，不写设备；
        /// - 设备阈值：写进气压表内部，驱动硬件报警触点（→ GX-CL140 的 DI）。
        /// 是否设、设成什么值属工艺决策，单位需按设备说明书确认后再调用本方法。
        /// </summary>
        /// <param name="deviceId">气压表编号（1-72）</param>
        /// <param name="thresholdValue">设备单位阈值（如 -95.0）</param>
        /// <returns>是否写入成功（设备不响应返回 false）</returns>
        bool SetThreshold(int deviceId, decimal thresholdValue);

        /// <summary>
        /// 批量写入所有气压表的设备阈值
        ///
        /// 逐台调用 <see cref="SetThreshold"/>，单台失败不影响其它台。
        /// 【注意】72 台连写 + 坏设备会阻塞较长时间（每台坏设备约一个读超时），
        /// 调用方应在后台线程执行，不要在 UI 线程直接调用。
        /// </summary>
        /// <param name="thresholdValue">设备单位阈值（与压力读数同单位同小数位）</param>
        /// <returns>写入结果字典（deviceId → 是否成功）</returns>
        Dictionary<int, bool> SetAllThresholds(decimal thresholdValue);

        /// <summary>
        /// 读取失败时的事件通知
        /// </summary>
        event EventHandler<string> OnError;
    }
}
