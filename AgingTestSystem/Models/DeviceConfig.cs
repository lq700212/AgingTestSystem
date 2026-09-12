using System;
using System.Collections.Generic;

namespace AgingTestSystem.Models
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
        /// 【V1.09 更新】依据IO分配表:
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
        /// 【V1.09 更新】依据IO分配表:
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
        /// 报警压力阈值（单位：kPa）
        /// 
        /// 约定：
        /// - 默认 -5 kPa（界面里也默认填这个）
        /// - 真空压力通常为负数，数值越接近 0 代表真空越差
        /// - 生产环境由"公共参数窗口"保存的负压值实时同步（V1.19.9）
        /// </summary>
        public decimal AlarmPressureThresholdKPa { get; set; } = -5m;

        /// <summary>
        /// 报警比较方向
        ///
        /// true：当 pressureKPa > AlarmPressureThresholdKPa 触发报警（真空变差：负数变“大”）
        /// false：当 pressureKPa < AlarmPressureThresholdKPa 触发报警（少见，保留扩展）
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
        /// 老化测试到时长后自动完成该台（关真空+断载台电+标已完成·待取料PASS+记日志），形成业务闭环。
        /// 【V1.59 取值规则，纠正旧注】
        /// 实际时长 = 工位配方"启动时间(StartTime)" &gt; 0 用配方值，否则回退本全局值；
        /// 配方与全局都为 0 = 不限时长（永不自动完成，只能手动停止）。
        /// 参数在启动瞬间定格，中途改配方/改本值不影响进行中的测试。
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

        /// <summary>
        /// 超温是否全线联停（【V1.66 新增】烧屏安全项，默认 false = 现状只记日志）。
        ///
        /// 【业务意义】
        /// false：超温只记日志、不停机（V1.66 之前的行为，保持不变）；
        /// true：送风机当前温度超过 <see cref="FanTempAlarmLimitC"/> 时，
        /// 主窗体自动停止全部在测工位（关阀+断电，边沿触发一次，回温后自动复位允许再停）。
        /// 全机只有一个温度探头（送风机控制屏），做不到单台联停，只有"全线停"一种动作；
        /// 超温停单台还是全线（问题清单 Q16）等现场拍板，在此之前本开关保持关闭。
        /// </summary>
        public bool FanTempShutdownEnabled { get; set; } = false;

        // =====================================================================
        // 工艺策略（【V1.67 新增】一期 L2 策略层：7 个待确认点全部可配）
        // 说明：
        // - 每个策略的缺省值 = V1.67 之前的行为（枚举 0 值），不配=和以前一模一样；
        // - 策略是跟项目的：存 Projects/<项目>/Policy.json（见 ProjectPolicyStore），
        //   App.config 里同名 key 只做机器级缺省兜底（手改 App.config 也生效，优先级低于项目文件）；
        // - SettingsForm"工艺策略"分类集中编辑，下拉中文显示、存英文名；
        // - 判定类分支一律先写 AgingSequencer 纯函数（家规），DeviceManager 只做执行。
        // =====================================================================

        /// <summary>
        /// 0 时长启动策略（问题清单 Q13 前半）。Warn=只警告（现状），Block=硬拦截。
        /// </summary>
        public ZeroDurationPolicy ZeroDurationPolicy { get; set; } = ZeroDurationPolicy.Warn;

        /// <summary>
        /// 空 SN 启动策略（问题清单 Q13 后半）。Warn=只警告（现状），Block=硬拦截。
        /// </summary>
        public EmptySnPolicy EmptySnPolicy { get; set; } = EmptySnPolicy.Warn;

        /// <summary>
        /// 送风机断连策略（问题清单 Q16 前半）。LogOnly=只提示照跑（现状），
        /// BlockStart=风机未连接时阻断启动。只管启动那一下，不管跑中掉线。
        /// </summary>
        public FanDisconnectPolicy FanDisconnectPolicy { get; set; } = FanDisconnectPolicy.LogOnly;

        /// <summary>
        /// 真空失败责任归属（问题清单 Q19）。ProductFail=记 FAIL（现状），
        /// FixtureAlarm=记"装夹异常"（不计产品不良，可重测）。只影响真空类报警
        /// （压力越限/真空建立失败）；DI 触点与通讯失联口径不变。
        /// </summary>
        public VacuumFailKind VacuumFailKind { get; set; } = VacuumFailKind.ProductFail;

        /// <summary>
        /// 完成判定口径（问题清单 Q22）。AutoPass=到时自动 PASS（现状），
        /// PendingReview=到时标"待判定"，下料时人工录 PASS/FAIL+不良代码+处置。
        /// </summary>
        public CompletionJudgePolicy CompletionJudgePolicy { get; set; } = CompletionJudgePolicy.AutoPass;

        /// <summary>
        /// 断电恢复策略（问题清单 Q21①）。RestartFull=整台重测（现状），
        /// ResumeRemaining=重抽真空后补足中断时刻的剩余时长（断电期间不计入老化）。
        /// </summary>
        public PowerLossPolicy PowerLossPolicy { get; set; } = PowerLossPolicy.RestartFull;

        /// <summary>
        /// 老化中失压策略（问题清单 Q11）。StopOnLoss=停机报警（现状），
        /// KeepRunning=只记事件继续老化。只管 Aging 阶段；抽真空阶段失败永远报警。
        /// </summary>
        public AgingPressureLossPolicy AgingPressureLossPolicy { get; set; } = AgingPressureLossPolicy.StopOnLoss;

        /// <summary>
        /// 到时完成动作（问题清单 Q6/Q15）。PowerOffOnly=只下电关阀（现状）；
        /// 带 Beep=PC 蜂鸣提醒取料；带 Vent=开破空阀泄压（需配 VentValveDoPoint）。
        /// </summary>
        public CompletionAction CompletionAction { get; set; } = CompletionAction.PowerOffOnly;

        /// <summary>
        /// 事件行 SN/配方取值（【V1.76 新增】Q8 追溯口径开关，跟项目走）。
        /// RecordTime=记录现值（现状：事件瞬间绑定的 SN/配方）；
        /// StartSnapshot=启动定格（该轮启动时的 SN/配方，中途重绑不污染已跑任务；
        /// 无快照时回退现值）。CSV/报表/MES 三处统一走 DeviceManager.ResolveEventIdentity。
        /// </summary>
        public EventIdentityMode EventIdentityMode { get; set; } = EventIdentityMode.RecordTime;

        /// <summary>
        /// 破空阀 DO 输出点编号（内部编号，与 TotalInputs/DeviceId 同口径，如 225）。
        /// 0 = 未配置（默认）：CompletionAction 选了泄压也只记日志跳过，不写坏任何通道。
        /// 配了点位才真写 DO；复位/启动/急停时自动关闭（不残留输出）。
        /// 【管路/点位需现场确认后填，现在先留 0】
        /// </summary>
        public int VentValveDoPoint { get; set; } = 0;

        /// <summary>
        /// 本机是否装破空阀（【V1.73 新增】通用型开关：这台工控机接没接破空阀硬件）。
        /// false（默认，本项目无阀）：工位设置窗手动"破空"按钮自动隐藏，
        /// 完成动作选泄压保存即拦（防配出到时"假泄压"）；
        /// true（下个有阀项目）：按钮显示 + 点位生效，泄压真写 DO。
        /// 跟机器（App.config），不是工艺——阀接在哪台机子上是接线事实。
        /// </summary>
        public bool VentValveEnabled { get; set; } = false;

        /// <summary>
        /// 是否启用载台电流回采（【V1.74 新增】Q2 通用骨架总开关）。
        /// false（默认，当前项目现状）：不建电表连接、不读数，BarometerData.LoadCurrentA
        /// 恒为 NaN（面板悬停显示"--"，CSV 记空，规则变量恒 false），零行为变化；
        /// true：按 UseMockCommunication 二选一（Mock 有数 / 真实桩连不上读 NaN），
        /// 电表到货实现 PowerMeterClient 真驱动后即插即用。
        /// 跟机器（App.config，结构型：改后重启生效，与 FanEnabled 同口径）。
        /// </summary>
        public bool UsePowerMeter { get; set; } = false;

        // =====================================================================
        // MES 对接（【V1.68 新增】二期：映射层可配，传输层走 HTTP POST JSON）
        // 说明：
        // - 能配的是"报什么/什么时候报/字段叫什么"（触发器 + 字段映射 + 静态字段）；
        //   协议栈（HTTP/重试/离线缓存）是代码，见 Services/MesReporter.cs。
        //   MES 全可视化是伪命题——每家 MES 的握手/事务都不同，映射层可配已覆盖 90% 差异。
        // - 跟机器还是跟项目：连接（开关/URL/超时/鉴权/重试/Mock）跟机器（这条产线的 MES 地址）；
        //   触发器/字段映射/静态字段跟项目（客户 A 与 B 的 MES 字段名不同），存 Policy.json。
        // - 总开关 MesEnabled 默认 false = 零行为变化；MesMockEnabled=true 时只记日志不发 HTTP，
        //   用于没 MES 环境时的联调验证。
        // =====================================================================

        /// <summary>
        /// 是否启用 MES 上报（默认 false = 完全不碰网络，零行为变化）。
        /// true 时按 MesTriggers 把事件 POST 到 MesEndpoint。
        /// </summary>
        public bool MesEnabled { get; set; } = false;

        /// <summary>
        /// MES 联调 Mock 开关（默认 false）。true 时不发 HTTP，只写一条
        /// "MES上报(Mock)"事件到 CSV（含完整 JSON），用于 MES 还没准备好时的端到端验证。
        /// </summary>
        public bool MesMockEnabled { get; set; } = false;

        /// <summary>
        /// MES 接收地址（完整 URL，如 http://192.168.1.50:8080/api/aging）。
        /// 单一入口：所有事件都 POST 到这里，事件类型在 JSON 的 event 字段区分。
        /// 留空 = 不发（即使 MesEnabled=true 也只记日志，防配了开关忘配地址空转）。
        /// </summary>
        public string MesEndpoint { get; set; } = "";

        /// <summary>
        /// HTTP 超时（毫秒，默认 5000）。上报走后台线程，超时只影响本条重试，不卡采集。
        /// </summary>
        public int MesTimeoutMs { get; set; } = 5000;

        /// <summary>
        /// 鉴权方式（None=无 / Bearer=Authorization: Bearer token / Basic=用户名密码）。
        /// 存英文名，大小写兼容，非法兜底 None。
        /// </summary>
        public string MesAuthType { get; set; } = "None";

        /// <summary>
        /// Bearer token（【V1.68】保存时自动 DPAPI 加密落盘，内存里是明文。
        /// 见 <see cref="Services.MesCrypto"/>）。
        /// </summary>
        public string MesAuthToken { get; set; } = "";

        /// <summary>
        /// Basic 鉴权用户名（MesAuthType=Basic 时用，明文）。
        /// </summary>
        public string MesAuthUser { get; set; } = "";

        /// <summary>
        /// Basic 鉴权密码（【V1.68】同 token 自动加密落盘，内存明文）。
        /// </summary>
        public string MesAuthPassword { get; set; } = "";

        /// <summary>
        /// 单条上报失败后的重试次数（默认 3；0=只发一次）。重试间隔见 MesRetryIntervalMs。
        /// 全部失败后进离线缓存（MesQueue.json），下次上报成功时顺带补发。
        /// </summary>
        public int MesRetryCount { get; set; } = 3;

        /// <summary>
        /// 重试间隔（毫秒，默认 2000）。
        /// </summary>
        public int MesRetryIntervalMs { get; set; } = 2000;

        /// <summary>
        /// 上报触发器（跟项目，逗号分隔，大小写无所谓）：
        /// Start=启动 / Complete=完成 / Alarm=报警 / UnloadJudge=下料判定。
        /// 留空 = 四个全报（缺省全开，开关 MesEnabled 才是总闸）。
        /// </summary>
        public string MesTriggers { get; set; } = "";

        /// <summary>
        /// 字段映射表（跟项目）："MES字段名=本站字段名"，多组用分号分隔。
        /// 本站字段 vocabulary：time/lot/device/event/sn/recipe/result/detail/pressure/
        /// temp/duration/displayMode/project/disposition/defectCode。
        /// 示例：eqId=device;lotNo=lot;opTime=time —— 发出去的 JSON 键就是 eqId/lotNo/opTime。
        /// 留空 = 直通（用本站原名，联调抓包看字段最方便）。
        /// </summary>
        public string MesFieldMap { get; set; } = "";

        /// <summary>
        /// 静态附加字段（跟项目）："键=值"，多组用分号分隔，原样并入每次上报。
        /// 示例：line=L5;workshop=A3 —— 产线/车间/班次这类"每次都一样"的常量放这里，
        /// 不用每个事件重复带。
        /// </summary>
        public string MesStaticFields { get; set; } = "";

        /// <summary>
        /// 自定义 HTTP 头（【V1.68 新增】跟机器）："头名=头值"，多组用分号分隔。
        /// 示例：X-Line=L5;X-ApiVer=2 —— MES 厂要求的租户/版本/产线头放这里。
        /// 与鉴权头同名时鉴权优先（Authorization 永远按 MesAuthType 生成，不会被覆盖，
        /// 防配错头把鉴权顶掉）。
        /// </summary>
        public string MesCustomHeaders { get; set; } = "";

        // =====================================================================
        // 规则流程（【V1.69 新增】三期：规则表达式 + 阶段流，全部跟项目走 Policy.json）
        // 说明：
        // - 规则只能加严不能松绑：自定义报警只会多报警（记 FAIL），动不了内置联锁；
        //   完成表达式 OR 语义只能提前完成（烧屏架少点亮更安全），拖不成无限老化；
        //   详情见 Services/RuleExpr.cs 与 Services/RuleEngine.cs 类注释。
        // - 唯一的显式 bypass 是 SkipVacuum（无真空治具用），开之前确认产品已机械固定，
        //   开了之后压力报警同步豁免（没真空就没压力信号，留着就是误报）。
        // - 三项全空/全关 = 和 V1.68 一模一样，零行为变化。
        // =====================================================================

        /// <summary>
        /// 自定义报警规则表（跟项目，多行文本，一行一条）：
        /// "名称 | 表达式 | 持续秒"（持续秒可省，默认 0=立即）。
        /// 示例：超温偏离 | temp - tempset > 10 | 30
        /// 触发 = 关阀+断电+记 FAIL（非真空类，Q19 策略不改它）；表达式持续成立满
        /// 持续秒才触发（防毛刺）。变量见 RuleExpr.Vocabulary（12 个，冻结）。
        /// </summary>
        public string CustomAlarmRules { get; set; } = "";

        /// <summary>
        /// 完成表达式（跟项目，单行，空=禁用走内置时长）：
        /// 成立即完成（与"时长到"取或，只能提前）。示例：temp > 85（过温提前收工保产品）。
        /// 求值错按"不成立"处理（按内置时长走，不停线）。
        /// </summary>
        public string CompleteExpression { get; set; } = "";

        /// <summary>
        /// 跳过抽真空阶段（跟项目，默认 false）。
        /// true = 启动即上电（不等人真空到位+延时），适用于机械夹具、无真空管路的治具；
        /// 同时豁免压力报警（没真空就没压力信号）。开之前必须确认产品已机械固定，
        /// 配错在真空架上开=真空保护全丢（启动日志会大写警告）。
        /// </summary>
        public bool SkipVacuum { get; set; } = false;

        // =====================================================================
        // 报表导出（【V1.74 新增】Q8 报表可配：列编排跟项目走 Policy.json）
        // 说明：
        // - 配的是"导出的列有哪些/叫什么/什么顺序"（显示名=字段），不是报表格式本身；
        //   格式固定 xlsx（表头加粗居中 + 数据行），由历史窗导出按钮生成；
        // - 留空 = 缺省预设（ReportColumns.DefaultPreset，烧屏追溯惯例列序），
        //   客户改列才填；脏组保存时拦、导出时跳过（与 MES 映射同规矩）。
        // =====================================================================

        /// <summary>
        /// 报表列配置（跟项目，"显示名=字段"，分号分隔，如 "时间=time;批号=lot"）。
        /// 可用字段见 <see cref="Services.ReportColumns.AvailableFields"/>
        /// （历史 CSV 真实有的 8 列）；留空 = 缺省预设。
        /// </summary>
        public string ReportColumns { get; set; } = "";

        // =====================================================================
        // 显示模式字典（【V1.74 新增】Q20 记录层可配：烧屏画面选项名单，跟项目）
        // 说明：
        // - 录入窗（配方管理/批量/工位设置）的显示模式输入框保存时按此校验：
        //   空=清空允许，字典内=存规范写法，字典外=拦并报出全部选项；
        // - 留空 = 缺省预设（DisplayModeOptions.DefaultPreset）；
        // - 只管记录层（存/报什么），不管 PG 控制（没协议）。
        // =====================================================================

        /// <summary>
        /// 显示模式字典（跟项目，逗号分隔，如 "白场,红场,绿场"）。
        /// 可用性见 <see cref="Services.DisplayModeOptions"/>；留空 = 缺省预设 8 项。
        /// </summary>
        public string DisplayModes { get; set; } = "";

        /// <summary>
        /// 是否启用显示模式维度（【V1.75 新增】Q20 收尾：当前项目没提画面，默认藏）。
        /// false（默认）：三窗隐藏显示模式行（标签+下拉，布局同步收缩），配方存空串，
        /// 上报/日志带空——当前项目零打扰；true：三窗显示下拉 + 字典生效。
        /// 跟项目（App.config 只做机器缺省，`Projects/&lt;项目&gt;/Policy.json` 优先）。
        /// </summary>
        public bool DisplayModeEnabled { get; set; } = false;

        /// <summary>
        /// 按事件分地址（【V1.68 新增】跟机器）："触发器=URL"，多组用分号分隔。
        /// 示例：Alarm=http://192.168.1.50:8080/api/alarm —— 报警走专用接口，其余走 MesEndpoint。
        /// 没配的事件回退 MesEndpoint；URL 必须 http(s):// 开头（保存时校验）。
        /// </summary>
        public string MesEndpointMap { get; set; } = "";

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

        /// <summary>
        /// 把另一份配置的全部可写属性原样拷进本实例（【V1.72.10 热更】项目切换用）。
        ///
        /// 【为什么不用"换引用"】MainForm._config 是 readonly，DeviceManager/MesReporter/
        /// MesReporter 持的是同一引用的"别名"——换引用只换了 MainForm 手里的，
        /// 干活的服务还捏着旧对象。用 CopyFrom 就地换血，所有持引用方下个周期
        /// 自动读到新值（读引用是原子的，单属性读写不撕裂；整批拷贝期间调用方
        /// 已暂停主采集，不会有"半新半旧"的一帧）。
        /// 【为什么用反射】配置项 60+ 个且还在涨，手写逐项赋值漏一项就是"切了项目
        /// 策略没换"的灵异 bug；反射按"公开可写实例属性"全量拷，新增属性零维护。
        /// 只读/计算属性（无 setter）自动跳过；拷贝失败（理论上不会）抛异常，
        /// 调用方热更失败走"提示重启"兜底。
        /// </summary>
        /// <param name="source">源配置（一般是 LoadConfig 刚读出来的新项目叠加结果）</param>
        public void CopyFrom(DeviceConfig source)
        {
            if (source == null) throw new System.ArgumentNullException("source");
            var props = typeof(DeviceConfig).GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var p in props)
            {
                if (!p.CanRead || !p.CanWrite) continue;
                if (p.GetIndexParameters().Length > 0) continue;   // 索引器跳过（本类没有，防以后）
                object v = p.GetValue(source, null);
                p.SetValue(this, v, null);
            }
        }
    }
}
