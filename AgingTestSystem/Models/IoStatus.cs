
using System;

namespace AgingTestSystem.Models
{
    /// <summary>
    /// IO状态数据模型
    /// 用于描述单个IO点的状态信息
    /// </summary>
    public class IoStatus
    {
        /// <summary>
        /// IO点内部编号（全局唯一，连续编号）
        /// 输入范围：1 ~ TotalInputs（默认 1 ~ 72）
        /// 输出范围：TotalInputs+1 ~ TotalInputs+TotalOutputs（默认 73 ~ 216）
        ///
        /// 【V1.09 更新】内部连续编号仅作为程序内部使用的索引，
        /// 实际硬件地址请通过 <see cref="PhysicalAddress"/> 字段获取（如 X000 / Y107）。
        /// </summary>
        public int IoId { get; set; }

        /// <summary>
        /// 物理地址（三菱PLC八进制编址，如 X000 / Y107）
        /// 【V1.09 新增】与 <see cref="IoId"/> 的十进制内部编号不同，
        /// 此字段对应现场IO模块/PLC的实际物理点位地址。
        /// </summary>
        public string PhysicalAddress { get; set; }

        /// <summary>
        /// IO类型：输入或输出
        /// </summary>
        public IoType Type { get; set; }

        /// <summary>
        /// IO点名称/描述（如：真空负压表-1、真空电磁阀-1、载台上电-1）
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 所属气压表编号（1 ~ TotalBarometers）
        /// 一个气压表对应 1 个输入 + 2 个输出（V1.09 更新，依据显耀IO表）
        /// </summary>
        public int DeviceId { get; set; }

        /// <summary>
        /// IO功能类型（真空负压表/真空电磁阀/载台上电）
        /// 【V1.09 新增】用于区分该IO点的业务功能
        /// </summary>
        public IoFunction Function { get; set; }

        /// <summary>
        /// 电气类型（NPN / PNP）
        /// 【V1.09 新增】
        /// - 输入采用 NPN 型（漏型/灌入式）：传感器导通时将信号拉低到 0V
        /// - 输出采用 PNP 型（源型/拉出式）：输出导通时输出 +24V 高电平
        /// </summary>
        public ElectricalType Electrical { get; set; }

        /// <summary>
        /// 在所属设备中的本地编号
        /// 输入：固定为 1（每个气压表仅 1 个输入）
        /// 输出：1 = 真空电磁阀，2 = 载台上电
        /// </summary>
        public int LocalIndex { get; set; }

        /// <summary>
        /// 当前状态：true为高电平/导通，false为低电平/断开
        /// </summary>
        public bool State { get; set; }

        /// <summary>
        /// 状态更新时间
        /// </summary>
        public DateTime UpdateTime { get; set; }

        /// <summary>
        /// 是否有报警
        /// </summary>
        public bool HasAlarm { get; set; }
    }

    /// <summary>
    /// IO类型枚举
    /// </summary>
    public enum IoType
    {
        /// <summary>
        /// 输入信号（现场为 NPN 型，X 地址）
        /// </summary>
        Input,

        /// <summary>
        /// 输出信号（现场为 PNP 型，Y 地址）
        /// </summary>
        Output
    }

    /// <summary>
    /// IO功能类型枚举
    /// 【V1.09 新增】依据"显耀IO表"定义每个IO点的业务功能
    /// </summary>
    public enum IoFunction
    {
        /// <summary>
        /// 未定义/预留 IO 点
        /// </summary>
        Unknown,

        /// <summary>
        /// 真空负压表信号（输入，NPN）
        /// 用于检测真空压力是否到达设定阈值
        /// </summary>
        VacuumPressure,

        /// <summary>
        /// 真空电磁阀控制（输出，PNP）
        /// 用于控制真空回路的通断，驱动中间继电器后控制电磁阀
        /// </summary>
        VacuumValve,

        /// <summary>
        /// 载台上电控制（输出，PNP）
        /// 用于给被测载台（产品治具）供电，驱动中间继电器后控制载台电源
        /// </summary>
        CarrierPower
    }

    /// <summary>
    /// IO电气类型枚举
    /// 【V1.09 新增】区分现场IO模块的输入输出电气特性
    /// </summary>
    public enum ElectricalType
    {
        /// <summary>
        /// NPN 型（漏型/灌入式）
        /// 【输入采用】当传感器导通时，将IO输入信号拉低到 0V（低电平有效）。
        /// IO模块内部提供上拉，NPN 传感器导通时拉低电平，模块识别为"导通"。
        /// 适合 NPN 型接近开关、光电传感器等。
        /// </summary>
        NPN,

        /// <summary>
        /// PNP 型（源型/拉出式）
        /// 【输出采用】当输出导通时，IO输出端输出 +24V 高电平，向外提供电流。
        /// 适合直接驱动中间继电器线圈（继电器另一端接 0V），
        /// 再由继电器触点控制大功率负载（如电磁阀、载台电源）。
        /// </summary>
        PNP
    }
}
