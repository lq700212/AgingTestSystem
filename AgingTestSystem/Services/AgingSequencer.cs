using System;
using AgingTestSystem.Models;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 老化时序决策器（V1.59 新增）——三阶段状态机的"纯判定"函数集
    /// 【为什么单独抽这个类】
    /// DeviceManager 的阶段推进逻辑长在采集循环里，依赖定时器/IO/锁，没法自动化测试。
    /// 把"该不该上电 / 该不该完成 / 真空建立是否超时"这类纯时间-条件判断抽成
    /// 无副作用的静态函数后，回归 harness 可以直接构造边界用例覆盖
    /// （0 延时、延时未到、压力未到位、时长到点、不限时长……），
    /// DeviceManager 只负责"调用决策 + 执行 IO + 写日志"，逻辑与副作用分离。
    /// 【三阶段时序总览】（与 DeviceManager.ProcessTestingProgress 配合阅读）
    ///   启动(只开阀，记开阀时刻 t0；延时=0 的台启动时阀+电同时开，直接进 Aging)
    ///     └─ Vacuuming：等「真空到位」且「now - t0 ≥ 延时时间」（两者取较晚；
    ///          延时=0 不等真空，ShouldPowerOn 直接 true，见下）
    ///          ├─ 到位前超过 VacuumConfirmTimeoutMs 仍未到位 → 报警(产品责任 FAIL)，永不带电
    ///          │  （宽限 0=关闭时不限时等，永不判超时，见 IsVacuumBuildFailed）
    ///          └─ 条件满足 → 载台上电 → Aging，老化计时起点 = 上电时刻
    ///     └─ Aging：now - 计时起点 ≥ 烧屏时间(配方) → 完成(下电+关阀+PASS·待取料)
    ///   延时时间 = "保持载台电源断电、只让真空吸附"的等待段；
    ///   延时=0 = 不要这段等待：启动瞬间阀+电同时开、直接进 Aging 计时，老化全程阀电保持常开，
    ///   只在完成/停止/急停/报警时关闭。真空保护不丢：刚开阀压力还在常压，
    ///   确认宽限（_vacuumConfirmTimes）照常保留，宽限内未到位暂不报警、
    ///   超时未到位报"真空建立超时"断电；到位后宽限关闭，后续失压按老化阶段正常报警断电。
    /// </summary>
    public static class AgingSequencer
    {
        /// <summary>
        /// 压力是否越限（纯函数，回归可直接断言）。
        /// 【判定规则】（单位 kPa，与气压表读数一致）
        /// - alarmWhenHigher=true（默认）：压力 > 阈值 → 越限（真空变差：负数变"大"）；
        /// - false：压力 &lt; 阈值 → 越限（扩展方向）。
        /// - 压力恰等于阈值 → 不越限（边界语义，两边一致）。
        /// 【为什么收拢到这里】原来 DeviceManager.PressureOutOfRange 与
        /// ModbusRtuBarometerReader.IsAlarm 各写了一份相同的 if/else，
        /// 两处一旦改了一处没改另一处就是灾难。现在两处都转调本函数，
        /// 判定口径只有一份（本项目"判定类逻辑写纯函数"的家规，见 AGENTS.md）。
        /// </summary>
        /// <param name="pressureKPa">当前真空压力（kPa）</param>
        /// <param name="thresholdKPa">有效阈值（kPa，配方优先、全局兜底由调用方定）</param>
        /// <param name="alarmWhenHigher">报警方向（对应配置 AlarmWhenPressureHigherThanThreshold）</param>
        /// <returns>true=越限（应报警），false=正常</returns>
        public static bool IsPressureOutOfRange(decimal pressureKPa, decimal thresholdKPa, bool alarmWhenHigher)
        {
            return IsPressureOutOfRange(pressureKPa, thresholdKPa, alarmWhenHigher, true);
        }

        /// <summary>
        /// 压力是否越限（带总开关版，判定口径唯一出处）。
        /// <para>做什么：总开关关闭时恒返回 false（不越限）：阈值越限不报，真空建立超时因"恒到位"同步不报（见调用方 ClassifyAlarm），负压阀保持常开。</para>
        /// <para>为什么收拢到这里：DeviceManager.PressureOutOfRange 与 ModbusRtuBarometerReader.IsAlarm 两处转调本函数，开关只加一处，两边永远同口径。</para>
        /// </summary>
        /// <param name="pressureKPa">当前真空压力（kPa）</param>
        /// <param name="thresholdKPa">有效阈值（kPa，配方优先、全局兜底由调用方定）</param>
        /// <param name="alarmWhenHigher">报警方向（对应配置 AlarmWhenPressureHigherThanThreshold）</param>
        /// <param name="alarmEnabled">压力报警总开关（对应配置 PressureAlarmEnabled；false=全不报）</param>
        /// <returns>true=越限（应报警），false=正常（含总开关关闭）</returns>
        public static bool IsPressureOutOfRange(decimal pressureKPa, decimal thresholdKPa, bool alarmWhenHigher, bool alarmEnabled)
        {
            if (!alarmEnabled)
            {
                return false;
            }
            if (alarmWhenHigher)
            {
                return pressureKPa > thresholdKPa;
            }
            return pressureKPa < thresholdKPa;
        }

        /// <summary>
        /// 抽真空阶段判定：当前是否满足"上电进入老化计时"的条件
        /// </summary>
        /// <param name="pressureInRange">真空压力是否已到位（≤ 该台有效阈值）</param>
        /// <param name="elapsedSinceValveOpen">距开阀时刻经过的时间</param>
        /// <param name="delaySeconds">延时时间（秒）：开阀后至少等这么久才上电；
        /// ≤0 = 不要等待段，直接上电（启动时阀电同开，不等真空到位；
        /// 真空建立靠确认宽限+过程报警兜底，见类头）</param>
        /// <returns>true = 应立即给载台上电并进入 Aging 阶段</returns>
        public static bool ShouldPowerOn(bool pressureInRange, TimeSpan elapsedSinceValveOpen, int delaySeconds)
        {
            // 延时≤0 = 无前置等待：不等真空是否到位，直接上电。
            // （启动侧已阀电同开直接进 Aging，这里短路 true 主要覆盖"上电写失败回滚到
            // Vacuuming 后下轮重试"的路径，保证重试也是立即上电、语义与启动一致。）
            if (delaySeconds <= 0)
            {
                return true;
            }
            // 延时>0：两个前置条件缺一不可：
            // 1) 压力到位——未吸附固定不通电（安全铁律，防止产品没吸住就振动通电）；
            // 2) 延时时间已到——给吸附留稳定时间（行业通识：抽真空有动态过程，
            //    刚到阈值就通电可能仍微漏，延时是工艺裕量）。两者自然取较晚满足者。
            if (!pressureInRange)
            {
                return false;
            }
            return elapsedSinceValveOpen.TotalSeconds >= delaySeconds;
        }

        /// <summary>
        /// 老化计时阶段判定：是否到达自动完成时刻
        /// </summary>
        /// <param name="elapsedSincePowerOn">距上电时刻经过的时间</param>
        /// <param name="durationSeconds">本次老化时长（秒）；0 或负数 = 不限时长（只能手动停止）</param>
        /// <returns>true = 应执行完成动作（下电+关阀+标 PASS·待取料）</returns>
        public static bool ShouldComplete(TimeSpan elapsedSincePowerOn, int durationSeconds)
        {
            if (durationSeconds <= 0)
            {
                return false; // 不限时长：永不自动完成
            }
            return elapsedSincePowerOn.TotalSeconds >= durationSeconds;
        }

        /// <summary>
        /// 抽真空阶段判定：真空建立是否已超时失败
        /// （开阀后 VacuumConfirmTimeoutMs 内压力始终未到位 → 判"真空建立失败"报警）
        /// 【0=关闭】配置为 0 时不限时等待、永不判超时（只靠老化阶段失压报警+人工停止兜底）；
        /// 负数非法，保存校验已拦、手改文件由启动加载钳回缺省，这里 ≤0 一律按关闭处理防崩。
        /// </summary>
        /// <param name="pressureInRange">当前压力是否到位</param>
        /// <param name="elapsedSinceValveOpen">距开阀时刻经过的时间</param>
        /// <param name="confirmTimeoutMs">确认宽限窗口（毫秒，默认 15000；0=关闭超时报警）</param>
        /// <returns>true = 真空建立失败，应报警断电（产品责任 FAIL）</returns>
        public static bool IsVacuumBuildFailed(bool pressureInRange, TimeSpan elapsedSinceValveOpen, int confirmTimeoutMs)
        {
            // 已到位就永远不算失败（后续只是等延时时间，那是正常等待不是故障）
            if (pressureInRange)
            {
                return false;
            }
            // 宽限 0=关闭：不限时等，不判建立超时（Aging 阶段失压报警照常，与 SkipVacuum 全豁免不同）
            if (confirmTimeoutMs <= 0)
            {
                return false;
            }
            return elapsedSinceValveOpen.TotalMilliseconds >= confirmTimeoutMs;
        }

        /// <summary>
        /// 最高级静默是否生效（纯函数：报警全关开关的唯一判定口）。
        /// <para>做什么：MuteAllAlarms = true 时压住一切报警分支（阈值越限/建立超时/DI 触点/
        /// 通讯失联/自定义规则/超温联停）；false 时各分支按各自开关判定。</para>
        /// <para>为什么收拢到这里：DeviceManager 的 ClassifyAlarm/失联分支/自定义规则三处、
        /// 主窗超温联停一处，同调本函数，开关只加一处，四边永远同口径（V1.67 约定）。</para>
        /// </summary>
        /// <param name="muteAllAlarms">报警全关开关（对应配置 MuteAllAlarms；true=全部静默）</param>
        /// <returns>true = 压住报警（不断电/不标故障/不判结果），false = 正常判定</returns>
        public static bool ShouldSuppressAlarm(bool muteAllAlarms)
        {
            return muteAllAlarms;
        }

        /// <summary>
        /// 启动前风险提示文案（纯函数，烧屏场景专用）。
        /// 【为什么是"警告"不是"拦截"】0 时长（不限时长永不自动完成）和空 SN（追溯断链）
        /// 在烧屏工艺里都是高风险，但是否允许仍是现场工艺权——软件只负责把丑话说在前面，
        /// 不替现场做主。调用方（MainForm 启动确认框）把返回的文本直接拼进确认框，
        /// 用户点"是"照跑、点"否"取消，无新弹窗、不改流程。
        /// </summary>
        /// <param name="zeroDurationIds">有效时长为 0 的工位号（配方与全局都为 0 → 不限时长）</param>
        /// <param name="emptySnIds">未绑定 SN 的工位号</param>
        /// <returns>警告文本块；两类都为空返回 ""（调用方直接拼，不用判空）</returns>
        public static string BuildStartWarningText(int[] zeroDurationIds, int[] emptySnIds)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            if (zeroDurationIds != null && zeroDurationIds.Length > 0)
            {
                sb.Append("\r\n⚠ 以下工位老化时长为 0（不限时长、永不到时完成，只能手动停止）：");
                sb.Append(string.Join("、", zeroDurationIds));
            }
            if (emptySnIds != null && emptySnIds.Length > 0)
            {
                sb.Append("\r\n⚠ 以下工位未绑定 SN（完成后无法追溯到单体）：");
                sb.Append(string.Join("、", emptySnIds));
            }
            return sb.ToString();
        }

        /// <summary>
        /// 启动前硬拦截文案（Q13 策略化：Warn 走 BuildStartWarningText，
        /// Block 走本函数——含 0 时长/空 SN 工位时直接阻断，连确认框都不进）。
        /// </summary>
        /// <param name="zeroDurationIds">有效时长为 0 的工位号</param>
        /// <param name="emptySnIds">未绑定 SN 的工位号</param>
        /// <param name="zeroPolicy">0 时长策略</param>
        /// <param name="snPolicy">空 SN 策略</param>
        /// <returns>阻断原因文案；""=不阻断（调用方直接拼，不用判空）</returns>
        public static string BuildStartBlockText(int[] zeroDurationIds, int[] emptySnIds,
            ZeroDurationPolicy zeroPolicy, EmptySnPolicy snPolicy)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            if (zeroPolicy == ZeroDurationPolicy.Block
                && zeroDurationIds != null && zeroDurationIds.Length > 0)
            {
                sb.Append("以下工位老化时长为 0（策略=硬拦截，不限时长不许启动）：");
                sb.Append(string.Join("、", zeroDurationIds));
            }
            if (snPolicy == EmptySnPolicy.Block
                && emptySnIds != null && emptySnIds.Length > 0)
            {
                if (sb.Length > 0) sb.Append("\r\n");
                sb.Append("以下工位未绑定 SN（策略=硬拦截，无追溯不许启动）：");
                sb.Append(string.Join("、", emptySnIds));
            }
            return sb.ToString();
        }

        /// <summary>
        /// 报警结果映射（Q19 策略化：真空失败记 FAIL 还是装夹异常）。
        /// 【映射表】
        /// - 设备异常（通讯失联，productRelated=false）→ "设备异常"（策略管不着，不判产品）；
        /// - 真空类报警（压力越限/真空建立失败）→ 策略决定：ProductFail="FAIL"，FixtureAlarm="装夹异常"；
        /// - 非真空的产品报警（DI 触点）→ 永远 "FAIL"（触点是仪表硬件判的，责任不在真空）。
        /// </summary>
        /// <param name="productRelated">是否产品相关（沿用 HandleAlarm 语义）</param>
        /// <param name="vacuumCause">是否为真空类原因（调用方按"压力越限/真空建立失败"判定）</param>
        /// <param name="vacuumKind">真空失败责任策略</param>
        public static string MapAlarmResult(bool productRelated, bool vacuumCause, VacuumFailKind vacuumKind)
        {
            if (!productRelated) return "设备异常";
            if (vacuumCause && vacuumKind == VacuumFailKind.FixtureAlarm) return "装夹异常";
            return "FAIL";
        }

        /// <summary>
        /// 断电续跑的剩余时长（Q21① 策略化，纯函数）。
        /// 【语义】remaining = duration - (savedAt - powerOn)：中断时刻已跑掉的不补，
        /// 断电期间（savedAt 之后）的不计入老化——补的是"欠的产能"，不是" wall clock"。
        /// - duration ≤ 0（不限时长）→ 返回 0（续跑仍不限时长）；
        /// - 已跑超（remaining ≤ 0）→ 返回 0？不：返回 -1 表示"断电前已到时"，
        ///   调用方直接按完成处理（重抽真空都省了？不——安全起见仍走一次完整重抽+立即完成？
        ///   不：产品已足时，直接标完成更合理。但阀/电状态未知，调用方仍需安全关闭）。
        ///   这里返回 0，调用方 ShouldComplete(≥0 的已跑, 0)... 注意 duration 原值语义：
        ///   返回 0 会被 ShouldComplete 判"不限时长永不完成"——错！
        ///   所以约定：返回 0 仅当输入不限时长；跑超返回 1 秒（下一轮即完成），简单无歧义。
        /// </summary>
        /// <param name="durationSeconds">中断前的定格总时长（秒）</param>
        /// <param name="powerOnTime">上电时刻（老化计时起点；Vacuuming 阶段中断传 MinValue）</param>
        /// <param name="savedAt">快照保存时刻（≈中断时刻）</param>
        /// <returns>续跑时长（秒）；不限时长返回 0；跑超返回 1</returns>
        public static int ComputeResumeDurationSeconds(int durationSeconds, DateTime powerOnTime, DateTime savedAt)
        {
            if (durationSeconds <= 0) return 0;                    // 不限时长：续跑仍不限
            if (powerOnTime == DateTime.MinValue) return durationSeconds; // 还没上电：整段重跑
            int elapsed = (int)(savedAt - powerOnTime).TotalSeconds;
            if (elapsed < 0) elapsed = 0;                          // 时钟回拨兜底：按没跑过算
            int remaining = durationSeconds - elapsed;
            return remaining > 0 ? remaining : 1;                  // 跑超：给 1 秒，下一轮即完成
        }

        /// <summary>
        /// 策略组合校验（防配出自相矛盾的组合；SettingsForm 保存时调用，
        /// 回归同步用例——"配置即代码"，校验器与策略同等重要）。
        /// 【目前锁定的矛盾组合】
        /// 1) 超温联停开了，但上限 ≤ 0（= 温度告警都没启用）→ 联停永远触发不了，
        ///    配了等于没配，必须先填 FanTempAlarmLimitC；
        /// 2) 完成动作选了泄压，但本机没装破空阀（VentValveEnabled=false）→ 到时不会泄压，
        ///    必须先确认硬件并打开开关，或改回不泄压；
        /// 3) 开关开了，但破空阀点位 = 0（未接通道）→ 到时会"跳过泄压只记日志"，
        ///    现场会误以为泄了，必须先填 VentValveDoPoint 或改回不泄压。
        /// </summary>
        /// <returns>矛盾描述；null=组合合法</returns>
        public static string ValidatePolicyCombination(bool fanShutdownEnabled, float fanLimitC,
            CompletionAction action, int ventPoint, bool ventEnabled)
        {
            if (fanShutdownEnabled && fanLimitC <= 0f)
            {
                return "超温联停已开，但温度告警上限为 0（=未启用）："
                    + "请先填 FanTempAlarmLimitC，否则联停永远触发不了。";
            }
            bool wantVent = (action == CompletionAction.PowerOffAndVent
                || action == CompletionAction.PowerOffVentAndBeep);
            if (wantVent && !ventEnabled)
            {
                return "完成动作选了泄压，但本机未装破空阀（VentValveEnabled=false）："
                    + "请先确认硬件并打开破空阀开关，或改回不泄压。";
            }
            if (wantVent && ventPoint <= 0)
            {
                return "完成动作选了泄压，但破空阀点位为 0（=未接硬件）："
                    + "请先填 VentValveDoPoint（现场确认点位后），或改回不泄压。";
            }
            return null;
        }

        /// <summary>
        /// 全局单点输出与工位编号区间碰撞校验（【复查补齐】纯函数：V1.67 约定"判定类分支先写纯函数"）。
        /// 【为什么会撞】破空阀/蜂鸣器这类全局单点走独立 DO 点；工位阀/载台电按
        /// TotalInputs+id / TotalInputs+TotalBarometers+id 编址。若点位配进
        /// 工位编号区间，超温/到时会"写 ON/OFF 到某台的阀/电"→ 误动作还极难排查。
        /// </summary>
        /// <param name="ventPoint">全局单点输出点位（≤0=未配置，不校验）</param>
        /// <param name="totalInputs">DI 总数（输入占用 1~TotalInputs，输出从 TotalInputs+1 起）</param>
        /// <param name="totalBarometers">工位数（每台占 阀编号+电编号 共 2 个输出点）</param>
        /// <param name="instanceName">点位归属名（报错文案用；缺省=破空阀，蜂鸣器传"超温蜂鸣器"/"真空蜂鸣器"）</param>
        /// <returns>矛盾描述；null=无碰撞</returns>
        public static string ValidateVentPointCollision(int ventPoint, int totalInputs, int totalBarometers,
            string instanceName = "破空阀")
        {
            if (ventPoint <= 0) return null;
            if (totalInputs < 0) totalInputs = 0;
            if (totalBarometers <= 0) return null;
            int first = totalInputs + 1;
            int last = totalInputs + 2 * totalBarometers;
            if (ventPoint >= first && ventPoint <= last)
            {
                return $"{instanceName}点位 {ventPoint} 与工位阀/载台电编号冲突"
                    + $"（{first}~{last} 已被 {totalBarometers} 台工位占用），请换预留点位";
            }
            return null;
        }

        /// <summary>
        /// 两路蜂鸣器撞点校验（纯函数：2.0.9 蜂鸣器分开后新增，保存时拦）。
        /// 【为什么撞不得】超温与真空蜂鸣器若配成同一点（非0），一路报警沿写 ON、
        /// 另一路解除沿写 OFF，互踢永远响不齐也停不齐——同一点只能归一路。
        /// 0（未接）不算撞点（不写任何通道）。
        /// </summary>
        /// <param name="fanBuzzerPoint">超温蜂鸣器点位（0=未接）</param>
        /// <param name="vacuumBuzzerPoint">真空蜂鸣器点位（0=未接）</param>
        /// <returns>矛盾描述；null=无撞点</returns>
        public static string ValidateBuzzerPointsCollision(int fanBuzzerPoint, int vacuumBuzzerPoint)
        {
            if (fanBuzzerPoint <= 0 || vacuumBuzzerPoint <= 0) return null;
            if (fanBuzzerPoint != vacuumBuzzerPoint) return null;
            return $"超温蜂鸣器与真空蜂鸣器配成了同一个输出点 {fanBuzzerPoint}，"
                + "两路报警会互踢对方的开关动作；请把其中一个换到其它空余的预留输出点";
        }

        /// <summary>
        /// 送风机超温是否应全线联停（纯函数）。
        /// 【为什么是全线不是单台】全机只有一个温度探头（送风机控制屏 0x0002），
        /// 没有分工位温度，做不到单台联停。超温停单台还是全线（问题清单 Q16）等现场拍板，
        /// 在此之前只提供"全线联停"一种动作，且默认关闭（enabled=false=现状：只记日志）。
        /// 边沿触发（只停一次、回温后自动复位允许再停）由调用方 MainForm 持状态实现，
        /// 本函数只判"此刻应不应该停"，无状态、可单测。
        /// </summary>
        /// <param name="temperatureC">送风机当前温度（°C）</param>
        /// <param name="limitC">告警上限（配置 FanTempAlarmLimitC；≤0=不启用）</param>
        /// <param name="shutdownEnabled">联停开关（配置 FanTempShutdownEnabled；默认 false）</param>
        /// <returns>true=此刻应执行全线联停</returns>
        public static bool IsFanOverTempShutdown(float temperatureC, float limitC, bool shutdownEnabled)
        {
            if (!shutdownEnabled || limitC <= 0f)
            {
                return false;
            }
            return temperatureC > limitC;
        }

        /// <summary>
        /// 送风机超温是否应触发蜂鸣器（纯函数；新现场需求：冷却送风机超温要触发蜂鸣报警）。
        /// 【与联停的口径差异】联停另受 FanTempShutdownEnabled 开关门控（停不停机是安全决策，默认关）；
        /// 蜂鸣只看"超温上限 + 蜂鸣开关 + 报警全关压制"——超温了就该叫，跟停不停机互不牵制。
        /// 2.0.7 起蜂鸣本身也可配置（FanTempAlarmBuzzerEnabled，默认 true=历史现状）：
        /// 客户现场可能嫌超温一直响吵，关掉后超温只记日志；真空异常另用独立的真空蜂鸣器，互不影响。
        /// 触发/停止的边沿锁与蜂鸣方式轮转由调用方 MainForm 持状态实现，本函数只判"此刻该不该叫"。
        /// </summary>
        /// <param name="temperatureC">送风机当前温度（°C；NaN=探头失效不当超温不叫，
        /// 失效另有"传感器无效"边沿日志与界面提示，不静默致盲）</param>
        /// <param name="limitC">告警上限（配置 FanTempAlarmLimitC；≤0=未启用，蜂鸣器也不工作——
        /// 没有阈值就没有"超温"判据，与联停同口径）</param>
        /// <param name="buzzerEnabled">超温蜂鸣开关（配置 FanTempAlarmBuzzerEnabled；
        /// false=超温这一路不参与蜂鸣，只记日志）</param>
        /// <param name="suppressAllAlarms">报警全关（配置 MuteAllAlarms；true=最高级静默，
        /// 蜂鸣器跟着哑，与超温联停同口径压制）</param>
        /// <returns>true=此刻应触发蜂鸣器</returns>
        public static bool ShouldFanBuzzerAlarm(float temperatureC, float limitC,
            bool buzzerEnabled, bool suppressAllAlarms)
        {
            if (!buzzerEnabled || limitC <= 0f || suppressAllAlarms)
            {
                return false;
            }
            // NaN 比较恒 false（fail-safe 约定）：探头失效不当超温，不误开蜂鸣器
            return temperatureC > limitC;
        }

        /// <summary>
        /// 真空异常蜂鸣判定（纯函数：真空异常这一路是否应触发真空蜂鸣器）。
        /// 【2.0.9 蜂鸣器分开】超温与真空异常各用各的蜂鸣器（各自配点位/方式/开关互不合并），
        /// 原共用判定 ShouldAlarmBuzzer 已废；本函数是真空路判定唯一口。
        /// 真空源现状以调用方的 _vacuumAlarmStations 集合为准（真空报警只退单台、不联停整线），
        /// 开关关=这一路不响（报警判定照旧，只记日志），报警全关 MuteAllAlarms 是最高级静默。
        /// 触发/收尾的边沿锁与蜂鸣方式轮转由调用方 MainForm 持状态实现，本函数只判"此刻该不该叫"。
        /// </summary>
        /// <param name="vacuumAlarmPending">是否存在未解除的真空异常台
        /// （调用方先按 VacuumAlarmBuzzerEnabled 门控源集合再传，本函数再兜一道开关）</param>
        /// <param name="buzzerEnabled">真空蜂鸣开关（配置 VacuumAlarmBuzzerEnabled）</param>
        /// <param name="suppressAllAlarms">报警全关（配置 MuteAllAlarms；true=最高级静默）</param>
        /// <returns>true=此刻真空蜂鸣器应处于触发状态</returns>
        public static bool ShouldVacuumBuzzerAlarm(bool vacuumAlarmPending, bool buzzerEnabled,
            bool suppressAllAlarms)
        {
            if (!buzzerEnabled || suppressAllAlarms) return false;
            return vacuumAlarmPending;
        }

        /// <summary>
        /// 超温蜂鸣器是否必须保持现状（暂不收尾，纯函数）。
        /// 【为什么不能一律收尾】超温蜂鸣功能已启用（蜂鸣开关 + 送风机启用 + 上限 &gt; 0），但本轮
        /// 拿不到有效温度（风机离线 / 探头 NaN）时，"温度没超"其实是"不知道"。
        /// 此时若直接停蜂鸣，就是探头一坏、报警静音——比多响几声危险得多。
        /// 所以保持当前状态等读数恢复：在响继续响，没响也不凭空开。
        /// 【2.0.7】超温蜂鸣开关关掉后，超温路整体不参与蜂鸣，"看不见温度"与它无关，不保持（立即收尾）。
        /// 【2.0.9】真空异常已改用独立的真空蜂鸣器，与超温蜂鸣器互不影响，本函数不再需要真空参数。
        /// </summary>
        /// <param name="fanEnabled">送风机功能是否启用（未启用=根本没有温度数据源）</param>
        /// <param name="buzzerEnabled">超温蜂鸣开关（false=超温路不参与蜂鸣，不保持）</param>
        /// <param name="limitC">超温告警上限（≤0 = 未启用超温告警）</param>
        /// <param name="tempKnown">本轮是否读到有效温度（非 NaN）</param>
        /// <param name="suppressAllAlarms">报警全关（最高级静默；true=无条件收尾，不保持）</param>
        public static bool ShouldHoldAlarmBuzzer(bool fanEnabled, bool buzzerEnabled, float limitC,
            bool tempKnown, bool suppressAllAlarms)
        {
            if (suppressAllAlarms) return false;
            return buzzerEnabled && fanEnabled && limitC > 0f && !tempKnown;
        }

        /// <summary>
        /// 送风机是否应保持运行（纯函数：在测或报警中，二者任一成立就保持）。
        /// 【为什么报警也要保持】报警台会退出测试（关阀/断电），若它是最后一台，
        /// 原逻辑"无在测就停送风机"会把炉子的冷却一起关掉——炉子还在发热，
        /// 停风机会把故障扩大成过温事故。报警台未复位前必须保持送风，
        /// 人工复位/急停后才按原逻辑停。
        /// </summary>
        public static bool ShouldKeepFanRunning(bool anyTesting, bool anyAlarm)
        {
            return anyTesting || anyAlarm;
        }
    }
}
