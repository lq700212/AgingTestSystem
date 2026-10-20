
using System;

namespace AgingTestSystem.Models
{
    /// <summary>
    /// 冷却送风机实时数据模型
    ///
    /// 【来源】
    /// 数据来自"冷却送风机控制屏"（Modbus TCP，厂商自带）。
    /// 寄存器映射以 ModbusTCPFanControllerTest Demo 实测为准：
    /// - 0x0000 组合状态（未使用，忽略）
    /// - 0x0001 控制/状态（读：0x0002=定值停止，0x0003=定值启动，0x0001=程式启动，0x0000=程式停止）
    /// - 0x0002 当前温度（实际值 = 寄存器值 ÷ 温度定点除数，单位 °C）
    /// - 0x0003 当前湿度（实际值 = 寄存器值 ÷ 湿度定点除数，单位 %RH）
    /// - 0x0004 温度设定值（实际值 = 寄存器值 ÷ 温度定点除数，单位 °C）
    /// - 0x0005 湿度设定值（实际值 = 寄存器值 ÷ 湿度定点除数，单位 %RH）
    /// 【定点比例】温度、湿度各一份斜率（DeviceConfig.FanTempScale / DeviceConfig.FanHumidityScale，
    /// 缺省 0.1 = 0.1 精度定点：屏显 22.2°C ↔ 寄存器 222；0.01 精度的屏用 0.01）。
    /// 厂商手册里两路小数位可能不同，所以分成两份。
    /// </summary>
    public class FanData
    {
        /// <summary>
        /// 运行状态（从 0x0001 读到的值）
        /// </summary>
        public FanRunState RunState { get; set; }

        /// <summary>
        /// 当前温度（单位：°C）
        /// </summary>
        public float Temperature { get; set; }

        /// <summary>
        /// 当前湿度（单位：%RH）
        /// </summary>
        public float Humidity { get; set; }

        /// <summary>
        /// 温度设定值（单位：°C）
        /// 【说明】设定值由厂商控制屏设定，上位机只读不写。
        /// </summary>
        public float TempSetpoint { get; set; }

        /// <summary>
        /// 湿度设定值（单位：%RH）
        /// </summary>
        public float HumSetpoint { get; set; }

        /// <summary>
        /// 本次是否成功从设备读到数据
        /// - true：通讯正常，Temperature/Humidity 等字段有效
        /// - false：通讯失败，本帧数据为默认值
        /// </summary>
        public bool IsOnline { get; set; }

        /// <summary>
        /// 采集时间戳
        /// </summary>
        public DateTime CollectTime { get; set; }

        /// <summary>
        /// 深拷贝
        /// 【用途】DeviceManager 返回缓存数据时返回副本，避免外部修改污染缓存
        /// （与 BarometerData.Clone 的设计约定一致）
        /// </summary>
        /// <returns>当前对象的深拷贝</returns>
        public FanData Clone()
        {
            return new FanData
            {
                RunState = this.RunState,
                Temperature = this.Temperature,
                Humidity = this.Humidity,
                TempSetpoint = this.TempSetpoint,
                HumSetpoint = this.HumSetpoint,
                IsOnline = this.IsOnline,
                CollectTime = this.CollectTime
            };
        }
    }

    /// <summary>
    /// 冷却送风机运行状态枚举
    ///
    /// 【说明】寄存器 0x0001 读取到的值直接对应命令码（实测）：
    /// - 0x0000 程式停止
    /// - 0x0001 程式启动
    /// - 0x0002 定值停止
    /// - 0x0003 定值启动
    ///
    /// 本上位机只需要"定值启动/定值停止"，程式模式是设备自带能力，保留枚举便于识别显示。
    /// Unknown 是本程序自定义的哨兵值（表示读失败/未初始化），不会出现在设备寄存器里。
    /// </summary>
    public enum FanRunState
    {
        /// <summary>未知（读失败或未初始化，本程序自定义）</summary>
        Unknown = -1,

        /// <summary>程式停止</summary>
        ProgramStopped = 0x0000,

        /// <summary>程式运行中</summary>
        ProgramRunning = 0x0001,

        /// <summary>定值停止</summary>
        FixedValueStopped = 0x0002,

        /// <summary>定值启动</summary>
        FixedValueRunning = 0x0003
    }

    /// <summary>
    /// 冷却送风机温度/湿度斜率（升降温速率）
    ///
    /// 【用途】客户现场标定用。
    /// 控制器的定点小数位是"仪表侧参数"（可在控制器屏上改），Modbus 寄存器表里没有
    /// "当前小数位/当前除数"这个字段，上位机读不出来。但斜率寄存器可写（客户文档第 6 章：
    /// 0x0020 温度斜率 RW、0x0021 湿度斜率 RW），客户按"写一个已知原始值 → 看屏上显示成多少"
    /// 的流程，就能自己反推出这台屏的定点除数（10 / 100 / 1）。
    ///
    /// 【字段说明】
    /// - TemperatureSlope / HumiditySlope：按当前除数换算出来的实际斜率（显示用）
    /// - RawTemperature / RawHumidity：寄存器原始值（标定时要填进"屏显对照"用）
    /// - Divisor：本次换算用的除数（标定结果就落在这里）
    /// </summary>
    public class FanSlopeData
    {
        /// <summary>温度斜率（换算后的实际值，单位：℃/分）</summary>
        public float TemperatureSlope { get; set; }

        /// <summary>湿度斜率（换算后的实际值，单位：%RH/分）</summary>
        public float HumiditySlope { get; set; }

        /// <summary>温度斜率寄存器原始值（0x0020 读回/写入的那个数）</summary>
        public ushort RawTemperature { get; set; }

        /// <summary>湿度斜率寄存器原始值（0x0021 读回/写入的那个数）</summary>
        public ushort RawHumidity { get; set; }

        /// <summary>本次换算使用的定点除数（标定出来的就是它）</summary>
        public float Divisor { get; set; }

        /// <summary>采集/读写时间戳</summary>
        public DateTime CollectTime { get; set; }
    }
}
