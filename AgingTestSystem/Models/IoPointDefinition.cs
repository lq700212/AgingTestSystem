
namespace AgingTestSystem.Models
{
    /// <summary>
    /// IO点定义模型
    /// 【V1.09 新增】描述单个IO点的静态配置信息（地址、功能、电气类型等）
    ///
    /// 该模型基于现场"IO分配表"整理而成，用于建立"内部连续编号"与"物理地址"之间的映射。
    /// 程序内部使用 <see cref="IoId"/>（十进制连续编号）进行索引，
    /// 与硬件通信时使用 <see cref="PhysicalAddress"/>（三菱PLC八进制地址，如 X000 / Y107）。
    /// </summary>
    public class IoPointDefinition
    {
        /// <summary>
        /// IO点内部编号（全局唯一，十进制连续编号）
        /// 输入：1 ~ TotalInputs（如 1 ~ 72）
        /// 输出：TotalInputs+1 ~ TotalInputs+TotalOutputs（如 73 ~ 216）
        /// </summary>
        public int IoId { get; set; }

        /// <summary>
        /// 物理地址（三菱PLC八进制编址）
        /// 输入示例：X000、X007、X010、X107
        /// 输出示例：Y000、Y107、Y110、Y217
        ///
        /// 【八进制编址说明】
        /// 三菱PLC的 X/Y 点采用八进制编号，每位数字只能是 0~7。
        /// 例如 X007 的下一个是 X010（不是 X008），X077 的下一个是 X100。
        /// </summary>
        public string PhysicalAddress { get; set; }

        /// <summary>
        /// IO点设备名称（来自IO分配表）
        /// 如：真空负压表-1、真空电磁阀-1、载台上电-1
        /// </summary>
        public string DeviceName { get; set; }

        /// <summary>
        /// 所属气压表编号（1 ~ TotalBarometers）
        /// </summary>
        public int DeviceId { get; set; }

        /// <summary>
        /// IO类型（输入 / 输出）
        /// </summary>
        public IoType Type { get; set; }

        /// <summary>
        /// IO功能类型（真空负压表 / 真空电磁阀 / 载台上电）
        /// </summary>
        public IoFunction Function { get; set; }

        /// <summary>
        /// 电气类型（NPN / PNP）
        /// 输入点为 NPN，输出点为 PNP（依据客户确认）
        /// </summary>
        public ElectricalType Electrical { get; set; }

        /// <summary>
        /// 在所属设备中的本地编号
        /// 输入：固定为 1（每气压表仅 1 个输入）
        /// 输出：1 = 真空电磁阀，2 = 载台上电
        /// </summary>
        public int LocalIndex { get; set; }
    }

    /// <summary>
    /// 单个气压表对应的IO点映射集合
    /// 【V1.09 新增】每个气压表对应 1 个输入 + 2 个输出
    /// </summary>
    public class DeviceIoMapping
    {
        /// <summary>
        /// 真空负压表输入点（NPN，X 地址）
        /// </summary>
        public IoPointDefinition VacuumPressureInput { get; set; }

        /// <summary>
        /// 真空电磁阀输出点（PNP，Y 地址）
        /// </summary>
        public IoPointDefinition VacuumValveOutput { get; set; }

        /// <summary>
        /// 载台上电输出点（PNP，Y 地址）
        /// </summary>
        public IoPointDefinition CarrierPowerOutput { get; set; }
    }
}
