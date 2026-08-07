using System;
using System.Collections.Generic;

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
        /// 是否启用 IO 输出"备用通道映射"
        ///
        /// 【背景】
        /// 现场某个 DQ 输出通道烧毁 / 电压不足后，把该通道的信号改写到备用通道。
        /// 因为本程序会复用到多个工作台，多数工作台没有烧通道，所以做成**开关**：
        /// - false：不启用（默认），所有工作台行为完全不变
        /// - true：启用，按 <see cref="IoBackupChannelMappings"/> 把物理读写位置重定向到备用通道
        ///
        /// 业务侧（输出点编号、UI 显示、报警联动）在启用后完全不变，
        /// 只是"写 DO / 读 DO"时自动改写到备用通道。
        /// </summary>
        public bool IoBackupChannelMappingEnabled { get; set; } = false;

        /// <summary>
        /// IO 输出备用通道映射表（IoBackupChannelMappingEnabled = true 时生效）
        /// 配置格式与解析见 <see cref="IoOutputChannelRemap.ParseAll"/>。
        /// </summary>
        public List<IoOutputChannelRemap> IoBackupChannelMappings { get; set; } = new List<IoOutputChannelRemap>();

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

        // =====================================================================
        // 冷却送风机配置
        // 说明：冷却送风机（厂商自带控制屏）的自动控温已由厂商集成，
        //      上位机只需要"定值启动 / 定值停止"，并周期读取状态用于显示。
        // 寄存器映射 / 端口以 ModbusTCPFanControllerTest Demo 实测为准。
        // =====================================================================

        /// <summary>
        /// 是否启用冷却送风机接入
        ///
        /// - true：程序启动时尝试连接送风机控制屏并周期轮询状态；
        ///         送风机是"可选设备"，连接失败不会影响整机启动
        /// - false：完全跳过送风机（不创建连接、不轮询、不显示）
        /// </summary>
        public bool FanEnabled { get; set; } = true;

        /// <summary>
        /// 冷却送风机控制屏 IP 地址
        /// 以现场实际设备为准：192.168.1.220（Demo 默认值同步此地址，改 IP 可在界面直接填）
        /// </summary>
        public string FanIpAddress { get; set; } = "192.168.1.220";

        /// <summary>
        /// 冷却送风机通讯端口
        /// 实测默认 50000（厂商控制屏，非标准 Modbus TCP 502 端口）
        /// </summary>
        public int FanPort { get; set; } = 50000;

        /// <summary>
        /// 冷却送风机从站地址（UnitId / SlaveId）
        /// 实测默认 1
        /// </summary>
        public byte FanUnitId { get; set; } = 1;

        /// <summary>
        /// 冷却送风机通讯超时（毫秒）
        /// 同时用于连接超时、读写超时，防止设备掉线时界面卡死
        /// </summary>
        public int FanTimeoutMs { get; set; } = 3000;

        /// <summary>
        /// 送风机 IP 自动识别开关
        ///
        /// 【背景】现场冷却送风机控制器的 IP 可能是 192.168.1.220 / .221 / .222 中的任意一个
        ///（换工作台、换控制器都会变），如果 IP 写死，换现场就得改配置。
        /// 所以做成自动识别：
        /// - true（默认）：连接时按顺序尝试 <see cref="FanIpAddress"/> + <see cref="FanIpCandidates"/>，
        ///   第一个能连上的 IP 就是设备真实地址，现场不需要改配置。
        /// - false：只尝试 <see cref="FanIpAddress"/>（与旧版本行为一致）。
        /// </summary>
        public bool FanAutoDetectEnabled { get; set; } = true;

        /// <summary>
        /// 送风机候选 IP 列表（FanAutoDetectEnabled = true 时生效）
        /// 连接时按顺序逐个尝试，第一个连接成功的 IP 即为设备真实地址。
        /// 配置里用逗号 / 分号分隔（中英文标点均可），非法 IP 自动忽略，见
        /// <see cref="ParseFanIpCandidates"/>。
        /// </summary>
        public List<string> FanIpCandidates { get; set; } = new List<string>();

        /// <summary>
        /// 解析配置文件里的候选 IP 列表字符串
        /// 支持中英文逗号/分号分隔，自动过滤空项与非法 IP，并按原顺序去重。
        /// </summary>
        /// <param name="raw">原始配置字符串，如 "192.168.1.220,192.168.1.221,192.168.1.222"</param>
        /// <returns>解析后的 IP 列表（保持原顺序，无重复）</returns>
        public static List<string> ParseFanIpCandidates(string raw)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(raw)) return result;

            // 兼容中英文逗号/分号分隔（用户可能手输中文标点）
            char[] separators = { ',', ';', '，', '；' };
            foreach (string item in raw.Split(separators))
            {
                string ip = item?.Trim();
                if (string.IsNullOrEmpty(ip)) continue;                     // 跳过空项
                if (!System.Net.IPAddress.TryParse(ip, out _)) continue;    // 跳过非法 IP

                // 去重（按原顺序保留第一次出现的地址）
                bool exists = false;
                foreach (string x in result)
                {
                    if (string.Equals(x, ip, StringComparison.OrdinalIgnoreCase)) { exists = true; break; }
                }
                if (!exists) result.Add(ip);
            }
            return result;
        }

        // =====================================================================
        // 老化测试业务参数
        // 这些参数决定"启动运行 / 报警联动 / 自动停止"等业务规则，
        // 是设计评审后新增的（详见 README 业务逻辑章节）。
        // =====================================================================

        /// <summary>
        /// 真空建立确认超时（毫秒）
        ///
        /// 【业务意义】
        /// 启动运行时先打开真空电磁阀，但真空建立需要时间（从常压抽到目标负压）。
        /// 如果开阀后 <VacuumConfirmTimeoutMs> 毫秒内压力仍未进入正常区间
        /// （说明真空没建立：阀故障/管路泄漏/产品没放好），按"真空建立失败"报警，
        /// 关闭该台电磁阀并切断载台上电，避免产品在未吸附固定的情况下通电老化。
        /// </summary>
        public int VacuumConfirmTimeoutMs { get; set; } = 15000;

        /// <summary>
        /// 通讯故障报警阈值（连续读取失败次数）
        ///
        /// 【业务意义】
        /// 气压表通讯中断时，压力会停留在旧值上，如果不处理会"假正常"继续老化。
        /// 当某台连续读取失败达到本阈值，视为通讯故障 → 触发报警（关阀+断电+标故障）。
        /// </summary>
        public int CommunicationLossAlarmCount { get; set; } = 3;

        /// <summary>
        /// 老化测试最大时长（秒），0 = 不限时长（手动停止）
        ///
        /// 【业务意义】
        /// 老化测试到时长后自动停止该台（关真空+断载台电+记日志），形成业务闭环。
        /// 注：后续可扩展为按配方（每台）指定时长，当前用全局默认值。
        /// </summary>
        public int MaxTestDurationSeconds { get; set; } = 0;

        /// <summary>
        /// 是否把"气压表报警触点（DI）"并入报警判定
        ///
        /// 【业务意义】
        /// 现场气压表除 RTU 压力值外，还有一路硬件报警触点接在 DI 上。
        /// 默认 false（仅显示，不参与联锁），因为触点"常开/常闭"与 NPN 线制
        /// 需要现场确认后才能确定触发电平；确认后置 true 可作为软件报警的冗余输入。
        /// </summary>
        public bool UseDiAlarmContact { get; set; } = false;

        /// <summary>
        /// 送风机温度告警上限（°C），0 = 不启用温度告警
        ///
        /// 【业务意义】
        /// 送风机回报的当前温度超过本上限时，界面把温度显示为红色并写日志，
        /// 提醒操作员老化箱可能过温（厂商自动控温异常时的人工兜底）。
        /// </summary>
        public float FanTempAlarmLimitC { get; set; } = 0f;

        // =====================================================================
        // 扫码枪配置（V1.16 新增，参考 SerialScannerTest Demo 实现）
        // 说明：扫码枪（Honeywell Xenon 1902 等）通过虚拟串口接入，
        //       扫到的条码内容 + 回车/换行 结尾（一行一条码）。
        // 相关实现见 Services/ScannerService.cs。
        // =====================================================================

        /// <summary>
        /// 是否启用扫码枪
        ///
        /// - true：程序启动时自动识别并连接扫码枪串口，扫码结果写入日志 /
        ///         ID绑定窗体的 SN 输入框自动填充
        /// - false（默认）：完全不连接扫码枪（现场没装扫码枪时用，避免无谓的 WMI 查询）
        /// </summary>
        public bool ScannerEnabled { get; set; } = false;

        /// <summary>
        /// 扫码枪固定串口（如 "COM10"）
        ///
        /// - 留空（默认）：通过 WMI 按 <see cref="ScannerDeviceKeyword"/> 自动识别端口
        /// - 填了具体端口（如 "COM10"）：直接用固定端口连接（WMI 识别不到时用这个兜底）
        /// </summary>
        public string ScannerPort { get; set; } = "";

        /// <summary>
        /// 扫码枪设备识别关键词（用于 WMI 自动识别串口）
        /// 对应设备管理器里显示的设备名称中包含的关键字，
        /// 当前现场扫码枪为 Honeywell Xenon 1902（默认 "Xenon 1902"）。
        /// </summary>
        public string ScannerDeviceKeyword { get; set; } = "Xenon 1902";

        /// <summary>
        /// 扫码枪串口波特率
        /// 以 SerialScannerTest Demo 实测为准：115200
        /// </summary>
        public int ScannerBaudRate { get; set; } = 115200;

        /// <summary>
        /// 扫码枪串口数据位（默认 8）
        /// </summary>
        public int ScannerDataBits { get; set; } = 8;

        /// <summary>
        /// 扫码枪串口停止位（默认 1）
        /// </summary>
        public int ScannerStopBits { get; set; } = 1;

        /// <summary>
        /// 扫码枪串口校验位（默认 None）
        /// </summary>
        public string ScannerParity { get; set; } = "None";

        /// <summary>
        /// 扫码枪心跳调试日志开关（默认 false）
        /// true 时每个心跳周期把端口搜索的实际结果（GetPortNames / WMI 匹配 / 判定）
        /// 通过状态事件打到 LOG，用于现场排查"断连识别不到"问题。
        /// </summary>
        public bool ScannerDebugLog { get; set; } = false;
    }
}
