
namespace BarometerWinform.Models
{
    /// <summary>
    /// 设备配置模型
    /// 用于存储系统中所有设备的配置参数
    /// 通过配置文件加载，支持动态调整设备数量
    /// </summary>
    public class DeviceConfig
    {
        /// <summary>
        /// 气压表总数
        /// 当前需求：72个，可通过配置调整
        /// </summary>
        public int TotalBarometers { get; set; } = 72;

        /// <summary>
        /// IO输入总数
        /// 当前需求：72 个（每个气压表对应 1 个输入点，共 72 个设备）
        /// 输入点编号范围：1 ~ TotalInputs（默认 1 ~ 72）
        ///
        /// 【V1.09 更新】依据显耀IO表:
        /// - 72 个输入点均为 NPN 型, 对应三菱PLC X 地址(八进制编址 X000~X107)
        /// - 设备名: 真空负压表-1 ~ 真空负压表-72
        /// - 物理地址映射详见 <see cref="Services.IoMapBuilder"/>
        /// </summary>
        public int TotalInputs { get; set; } = 72;

        /// <summary>
        /// IO输出总数
        /// 当前需求：144 个（每个气压表对应 2 个输出点，共 72 个设备 × 2 = 144）
        /// 输出点编号范围：TotalInputs+1 ~ TotalInputs+TotalOutputs（默认 73 ~ 216）
        ///
        /// 【V1.09 更新】依据显耀IO表:
        /// - 144 个输出点均为 PNP 型, 对应三菱PLC Y 地址(八进制编址)
        /// - 真空电磁阀-1~72: Y000~Y107 (内部编号 73~144)
        /// - 载台上电-1~72:  Y110~Y217 (内部编号 145~216)
        /// - 物理地址映射详见 <see cref="Services.IoMapBuilder"/>
        /// </summary>
        public int TotalOutputs { get; set; } = 144;

        /// <summary>
        /// 通信端口名称（如 COM1）
        /// </summary>
        public string PortName { get; set; } = "COM1";

        /// <summary>
        /// 通信波特率
        /// </summary>
        public int BaudRate { get; set; } = 9600;

        /// <summary>
        /// 数据位
        /// </summary>
        public int DataBits { get; set; } = 8;

        /// <summary>
        /// 停止位
        /// </summary>
        public int StopBits { get; set; } = 1;

        /// <summary>
        /// 校验位
        /// </summary>
        public string Parity { get; set; } = "None";

        /// <summary>
        /// PLC连接地址（预留字段，待确认协议）
        /// </summary>
        public string PlcAddress { get; set; } = "192.168.1.100";

        /// <summary>
        /// PLC通讯端口（默认502为Modbus TCP标准端口）
        /// </summary>
        public int PlcPort { get; set; } = 502;

        /// <summary>
        /// 数据采集间隔（毫秒）
        /// </summary>
        public int CollectInterval { get; set; } = 1000;

        /// <summary>
        /// 主视图每行显示的气压表数量
        /// </summary>
        public int PanelColumns { get; set; } = 9;

        /// <summary>
        /// 主视图每列显示的气压表数量
        /// </summary>
        public int PanelRows { get; set; } = 8;
    }
}
