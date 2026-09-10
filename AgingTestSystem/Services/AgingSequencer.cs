
using System;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 老化时序决策器（V1.59 新增）——三阶段状态机的"纯判定"函数集
    ///
    /// 【为什么单独抽这个类】
    /// DeviceManager 的阶段推进逻辑长在采集循环里，依赖定时器/IO/锁，没法自动化测试。
    /// 把"该不该上电 / 该不该完成 / 真空建立是否超时"这类纯时间-条件判断抽成
    /// 无副作用的静态函数后，回归 harness 可以直接构造边界用例覆盖
    /// （0 延时、延时未到、压力未到位、时长到点、不限时长……），
    /// DeviceManager 只负责"调用决策 + 执行 IO + 写日志"，逻辑与副作用分离。
    ///
    /// 【三阶段时序总览】（与 DeviceManager.ProcessTestingProgress 配合阅读）
    ///   启动(只开阀，记开阀时刻 t0)
    ///     └─ Vacuuming：等「真空到位」且「now - t0 ≥ 延时开启」（两者取较晚）
    ///          ├─ 到位前超过 VacuumConfirmTimeoutMs 仍未到位 → 报警(产品责任 FAIL)，永不带电
    ///          └─ 条件满足 → 载台上电 → Aging，老化计时起点 = 上电时刻
    ///     └─ Aging：now - 计时起点 ≥ 启动时间(配方) → 完成(下电+关阀+PASS·待取料)
    /// </summary>
    public static class AgingSequencer
    {
        /// <summary>
        /// 压力是否越限（【V1.62 新增】纯函数，回归可直接断言）。
        ///
        /// 【判定规则】（单位 kPa，与气压表读数一致）
        /// - alarmWhenHigher=true（默认）：压力 > 阈值 → 越限（真空变差：负数变"大"）；
        /// - false：压力 &lt; 阈值 → 越限（扩展方向）。
        /// - 压力恰等于阈值 → 不越限（边界语义，两边一致）。
        ///
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
        /// <param name="delaySeconds">延时开启（秒）：开阀后至少等这么久才上电；0 = 不额外等待</param>
        /// <returns>true = 应立即给载台上电并进入 Aging 阶段</returns>
        public static bool ShouldPowerOn(bool pressureInRange, TimeSpan elapsedSinceValveOpen, int delaySeconds)
        {
            // 两个前置条件缺一不可：
            // 1) 压力到位——未吸附固定不通电（安全铁律，防止产品没吸住就振动通电）；
            // 2) 延时开启已到——给吸附留稳定时间（行业通识：抽真空有动态过程，
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
        /// </summary>
        /// <param name="pressureInRange">当前压力是否到位</param>
        /// <param name="elapsedSinceValveOpen">距开阀时刻经过的时间</param>
        /// <param name="confirmTimeoutMs">确认宽限窗口（毫秒，全局配置，默认 15000）</param>
        /// <returns>true = 真空建立失败，应报警断电（产品责任 FAIL）</returns>
        public static bool IsVacuumBuildFailed(bool pressureInRange, TimeSpan elapsedSinceValveOpen, int confirmTimeoutMs)
        {
            // 已到位就永远不算失败（后续只是等延时开启，那是正常等待不是故障）
            if (pressureInRange)
            {
                return false;
            }
            return elapsedSinceValveOpen.TotalMilliseconds >= confirmTimeoutMs;
        }
    }
}
