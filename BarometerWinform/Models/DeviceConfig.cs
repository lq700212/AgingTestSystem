
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
        /// 当前现场接线：GX-CL140 后面接了 3 个输入模块（2×DI50N-S + 1×DI40N-S），合计 80 路输入通道。
        ///
        /// 重要：TotalInputs 表示“耦合器提供的 DI 通道总数”，不等同于气压表数量。
        /// - TotalBarometers（气压表数量）当前是 72
        /// - TotalInputs（输入通道数量）当前是 80（其中前 72 路用于真空负压表-1~72，剩余 8 路预留）
        ///
        /// 输入点编号范围：1 ~ TotalInputs（默认 1 ~ 80）
        ///
        /// 【V1.09 更新】依据显耀IO表:
        /// - 72 个输入点均为 NPN 型, 对应三菱PLC X 地址(八进制编址 X000~X107)
        /// - 设备名: 真空负压表-1 ~ 真空负压表-72
        /// - 物理地址映射详见 <see cref="Services.IoMapBuilder"/>
        /// </summary>
        public int TotalInputs { get; set; } = 80;

        /// <summary>
        /// IO输出总数
        /// 当前现场接线：GX-CL140 后面接了 5 个输出模块（5×DQ50P-S），合计 160 路输出通道。
        ///
        /// 重要：TotalOutputs 表示“耦合器提供的 DO 通道总数”，其中业务实际用到的是：
        /// - 真空电磁阀：72 路（对应 真空电磁阀-1~72）
        /// - 载台上电：72 路（对应 载台上电-1~72）
        /// 合计 144 路，其余 16 路预留。
        ///
        /// 输出点编号范围：TotalInputs+1 ~ TotalInputs+TotalOutputs（默认 81 ~ 240）
        ///
        /// 【V1.09 更新】依据显耀IO表:
        /// - 144 个输出点均为 PNP 型, 对应三菱PLC Y 地址(八进制编址)
        /// - 真空电磁阀-1~72: Y000~Y107 (内部编号 = TotalInputs + deviceId)
        /// - 载台上电-1~72:  Y110~Y217 (内部编号 = TotalInputs + TotalBarometers + deviceId)
        /// - 物理地址映射详见 <see cref="Services.IoMapBuilder"/>
        /// </summary>
        public int TotalOutputs { get; set; } = 160;

        /// <summary>
        /// 通信端口名称（如 COM1）
        /// </summary>
        public string PortName { get; set; } = "COM1";

        /// <summary>
        /// 通信波特率
        /// 以 ModbusRtuBarometerTest Demo 实测为准：19200
        /// </summary>
        public int BaudRate { get; set; } = 19200;

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
        /// PLC/IO 耦合器连接地址
        /// 当前现场 GX-CL140 默认 IP：192.168.1.20
        /// </summary>
        public string PlcAddress { get; set; } = "192.168.1.20";

        /// <summary>
        /// PLC通讯端口（默认502为Modbus TCP标准端口）
        /// </summary>
        public int PlcPort { get; set; } = 502;

        /// <summary>
        /// 数据采集间隔（毫秒）
        /// </summary>
        public int CollectInterval { get; set; } = 1000;

        /// <summary>
        /// 主视图每行显示的气压表数量（列数）
        ///
        /// 当前要求：8 列 × 9 行 = 72 个气压表面板
        /// </summary>
        public int PanelColumns { get; set; } = 8;

        /// <summary>
        /// 主视图每列显示的气压表数量（行数）
        ///
        /// 当前要求：8 列 × 9 行 = 72 个气压表面板
        /// </summary>
        public int PanelRows { get; set; } = 9;

        /// <summary>
        /// 是否使用模拟通讯（Mock）
        /// 
        /// 给新手的说明：
        /// - true：不需要接任何线，程序用随机数模拟气压与 IO 状态，方便先把 UI/业务跑通
        /// - false：启用真实通讯（气压表 Modbus RTU + IO Modbus TCP），需要现场接线与正确参数
        /// </summary>
        public bool UseMockCommunication { get; set; } = true;

        /// <summary>
        /// 串口读取超时（毫秒）
        /// 
        /// 超时的意义：
        /// - 防止串口在“设备断线/拔插/地址不对”时一直卡住线程
        /// - 超时后会抛异常，被上层捕获并通过 OnError 通知 UI
        /// </summary>
        public int SerialReadTimeoutMs { get; set; } = 1000;

        /// <summary>
        /// 串口写入超时（毫秒）
        /// </summary>
        public int SerialWriteTimeoutMs { get; set; } = 1000;

        /// <summary>
        /// TCP 发送超时（毫秒）
        /// </summary>
        public int TcpSendTimeoutMs { get; set; } = 3000;

        /// <summary>
        /// TCP 接收超时（毫秒）
        /// </summary>
        public int TcpReceiveTimeoutMs { get; set; } = 3000;

        /// <summary>
        /// 是否对输入点逻辑取反
        ///
        /// 现场可能出现的情况：
        /// - 线路/模块是 NPN（低电平有效）
        /// - 但耦合器映射到寄存器后，有的设备会把“低有效”转换为 “1=ON”，有的不会
        ///
        /// 因为是否需要取反只能通过现场实测确认，所以做成配置项：
        /// - false：寄存器 bit=1 认为输入 ON（默认）
        /// - true：寄存器 bit=0 认为输入 ON（逻辑取反）
        /// </summary>
        public bool InvertInputs { get; set; } = false;

        /// <summary>
        /// 是否对输出点逻辑取反
        /// </summary>
        public bool InvertOutputs { get; set; } = false;

        /// <summary>
        /// IO 模块的从站地址（UnitId/SlaveId）
        /// 
        /// 说明：
        /// - Modbus TCP 连接是 IP:Port，但协议里仍然有 UnitId 字段
        /// - 很多 IO 耦合器默认是 1（0x01）
        /// </summary>
        public byte IoUnitId { get; set; } = 1;

        /// <summary>
        /// IO 输入寄存器起始地址（DI 区域起点）
        /// 
        /// 约定：
        /// - 默认 0x1000（来自你提供的 GX-CL140 测试 Demo）
        /// - 16 个 DI 打包到 1 个寄存器（bit0=第1路，bit15=第16路）是否成立需现场确认
        /// </summary>
        public ushort IoInputRegisterStartAddress { get; set; } = 0x1000;

        /// <summary>
        /// IO 输出寄存器起始地址（DO 区域起点）
        /// 
        /// 约定：
        /// - 默认 0x2000（来自你提供的 GX-CL140 测试 Demo）
        /// - 当前实现采用 Holding Register + Read/Modify/Write 的方式写单点输出
        /// </summary>
        public ushort IoOutputRegisterStartAddress { get; set; } = 0x2000;

        /// <summary>
        /// 气压表压力值寄存器起始地址（Input Register，功能码 0x04）
        ///
        /// 约定（以 ModbusRtuBarometerTest Demo 实测为准）：
        /// - 0x0001 = 压力原始值（按有符号 short 解释，支持负压）
        /// - 0x0002 = 小数位数（合法 0~4；非法时用 BarometerDefaultDecimalPlaces）
        /// - 读取时一次读 2 个寄存器：ReadInputRegisters(slaveId, 0x0001, 2)
        /// </summary>
        public ushort BarometerPressureRegisterAddress { get; set; } = 0x0001;

        /// <summary>
        /// 小数位数默认值（当从设备读到的 0x0002 非法/无效时使用）
        /// 以 ModbusRtuBarometerTest Demo 实测为准：默认 1
        /// </summary>
        public int BarometerDefaultDecimalPlaces { get; set; } = 1;

        /// <summary>
        /// 压力值缩放系数
        /// 
        /// 示例：
        /// - 设备回传 12345，真实压力可能是 12.345kPa，则可配置为 0.001
        /// - 目前默认 1，等待现场确认后再调整
        /// </summary>
        public decimal BarometerPressureScale { get; set; } = 1m;

        /// <summary>
        /// 报警压力阈值（单位：Pa）
        /// 
        /// 约定：
        /// - 默认 -95000 Pa（界面里也默认填这个）
        /// - 真空压力通常为负数，数值越接近 0 代表真空越差
        /// </summary>
        public decimal AlarmPressureThresholdPa { get; set; } = -95000m;

        /// <summary>
        /// 报警比较方向
        /// 
        /// true：当 pressurePa > AlarmPressureThresholdPa 触发报警（真空变差：负数变“大”）
        /// false：当 pressurePa < AlarmPressureThresholdPa 触发报警（少见，保留扩展）
        /// </summary>
        public bool AlarmWhenPressureHigherThanThreshold { get; set; } = true;
    }
}
