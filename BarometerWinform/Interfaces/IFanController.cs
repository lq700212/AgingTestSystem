
using System;
using BarometerWinform.Models;

namespace BarometerWinform.Interfaces
{
    /// <summary>
    /// 冷却送风机控制接口
    ///
    /// 【设计说明】（给新手看的）
    /// 与 IBarometerReader / IIoController 保持同一套设计风格：
    /// - 上层（DeviceManager）只依赖接口，不关心底层是"真实设备"还是"Mock 模拟"
    /// - 现场接入真实送风机时，只要实现本接口即可，不动上层业务代码
    ///
    /// 【业务说明】
    /// 冷却送风机（厂商自带控制屏）的自动控温已由厂商集成好，
    /// 上位机只需要做两件事：
    /// 1. 定值启动：让送风机按控制屏上的设定温度运行（厂商自动控温）
    /// 2. 定值停止：停止送风机
    /// 另外可以周期读取状态（运行中/已停止、当前温度、当前湿度等）用于显示。
    ///
    /// 【线程说明】
    /// 通讯可能被"采集线程（DeviceManager 定时器）"和"UI 线程（按钮点击）"同时调用，
    /// 实现类必须保证线程安全（一般用 lock 串行化对主站的访问）。
    /// </summary>
    public interface IFanController : IDisposable
    {
        /// <summary>
        /// 连接状态
        /// true 表示已连接，false 表示未连接
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 连接送风机控制屏
        /// </summary>
        /// <param name="config">设备配置（含 FanIpAddress / FanPort / FanUnitId / FanTimeoutMs）</param>
        /// <returns>是否连接成功</returns>
        bool Connect(DeviceConfig config);

        /// <summary>
        /// 断开连接
        /// </summary>
        void Disconnect();

        /// <summary>
        /// 读取送风机当前状态（状态 + 温度 + 湿度 + 设定值）
        /// </summary>
        /// <returns>
        /// 送风机数据；读取失败返回 null（上层据此显示"离线"）
        /// </returns>
        FanData ReadStatus();

        /// <summary>
        /// 定值启动
        /// 让送风机按控制屏设定的温度运行（写入 0x0001 = 0x0003）
        /// </summary>
        /// <returns>是否发送成功</returns>
        bool StartFixedValue();

        /// <summary>
        /// 定值停止
        /// 停止送风机（写入 0x0001 = 0x0002）
        /// </summary>
        /// <returns>是否发送成功</returns>
        bool Stop();

        /// <summary>
        /// 通讯错误事件（连接失败、读写超时等）
        /// </summary>
        event EventHandler<string> OnError;
    }
}
