
using System;
using AgingTestSystem.Models;

namespace AgingTestSystem.Interfaces
{
    /// <summary>
    /// 载台电流表接口（【V1.74 新增】Q2 通用骨架：电流回采预留）。
    ///
    /// 【设计说明】（给新手看的）
    /// 与 IFanController 保持同一套设计风格：
    /// - 上层（DeviceManager）只依赖接口，不关心底层是"真实电表"还是"Mock 模拟"；
    /// - 电表选型确定后，只要实现本接口即可，不动上层业务代码。
    ///
    /// 【业务说明】
    /// 当前项目不接电表（UsePowerMeter=false，零行为变化）；后续项目有回采要求时，
    /// 打开开关 + 实现真实驱动（把本接口的 ReadAllCurrents 接到电表协议上），
    /// 面板悬停/CSV/规则变量/MES 侧（以后）直接复用，无需再改业务。
    ///
    /// 【数据模型】每工位一路（与 72 工位架构对齐，deviceId=1~N）：
    /// - 单位：安培（A），float；
    /// - float.NaN = 该路无数据（电表离线/未接），上层按"无数据"处理，绝不参与判定。
    ///
    /// 【线程说明】
    /// 由 DeviceManager 采集线程每秒调用一次，实现类必须保证线程安全
    /// （一般用 lock 串行化对设备的访问，与 MockFanController 一致）。
    /// </summary>
    public interface IPowerMeter : IDisposable
    {
        /// <summary>
        /// 连接状态
        /// true 表示已连接，false 表示未连接
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 连接电表设备
        /// </summary>
        /// <param name="config">设备配置（以后电表串口/IP 参数放这里，现状 Mock/桩实现忽略）</param>
        /// <returns>是否连接成功</returns>
        bool Connect(DeviceConfig config);

        /// <summary>
        /// 断开连接
        /// </summary>
        void Disconnect();

        /// <summary>
        /// 按需重连：已连接直接返回 true，未连接立即重连一次
        /// （与 IFanController.ReconnectNow 同语义，调用方无需区分设备类型）。
        /// </summary>
        /// <returns>重连后是否已连接</returns>
        bool ReconnectNow();

        /// <summary>
        /// 一次读回全部工位的载台电流（下标 0 = 1 号工位）。
        /// </summary>
        /// <param name="deviceCount">工位总数（= TotalBarometers）</param>
        /// <returns>
        /// 长度 ≥ deviceCount 的数组；某路无数据填 float.NaN（上层跳过，不判故障）。
        /// 读取失败返回 null（上层保留上轮缓存，不断追溯链）。
        /// </returns>
        float[] ReadAllCurrents(int deviceCount);

        /// <summary>
        /// 通讯错误事件（连接失败、读写超时等）
        /// </summary>
        event EventHandler<string> OnError;
    }
}
