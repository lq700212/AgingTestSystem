
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
        /// 【修复 L4】原注释存在矛盾（既说"每个气压表2个输入"又说"按区域管理为72组"），
        /// 实际配置值就是 72，与 App.config 中 TotalInputs=72 一致。
        /// 注释已更正为与配置值一致的说明。
        /// </summary>
        public int TotalInputs { get; set; } = 72;

        /// <summary>
        /// IO输出总数
        /// 当前需求：144 个（每个气压表对应 2 个输出点，共 72 个设备 × 2 = 144）
        /// 输出点编号范围：TotalInputs+1 ~ TotalInputs+TotalOutputs（默认 73 ~ 216）
        ///
        /// 【修复 L4】原注释存在矛盾（既说"每个气压表4个输出"又说"按区域管理为144组"），
        /// 实际配置值就是 144，与 App.config 中 TotalOutputs=144 一致。
        /// 注释已更正为与配置值一致的说明。
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
