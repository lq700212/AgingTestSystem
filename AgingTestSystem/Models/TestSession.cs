
using System;
using System.Collections.Generic;

namespace AgingTestSystem.Models
{
    /// <summary>
    /// 在测任务快照（V1.59 新增，断电恢复用）
    ///
    /// 【用途】老化测试讲究连续性，但现场可能遇到异常断电/程序崩溃。
    /// DeviceManager 在"有工位处于测试中"期间把所有在测任务的参数快照
    /// 持久化到 TestSession.json（经 <see cref="Services.TestSessionStore"/>）；
    /// 程序重新启动后检测到该文件即弹窗询问操作员：
    /// - 恢复 = 对每台【整台重测】（重新开阀→抽真空→延时→满时长老化）。
    ///   为什么不续跑剩余时长：断电期间载台已断电、产品状态未知，
    ///   老化数据已不连续，续跑没有质量意义（行业通识 + 设计评审结论）；
    /// - 放弃 = 关闭这些工位的阀与电源（安全兜底），删除快照文件。
    ///
    /// 【存了哪些参数、为什么】
    /// 启动测试时会把"定格后的本次任务参数"存进来（老化时长/报警阈值/SN/配方名/批号）：
    /// - 重启后 StationSettingsCache 的恢复时序不受控件加载顺序影响，快照自包含最稳；
    /// - SN / 配方名 / 批号用于恢复时的日志追溯，保证重测记录能对上原来的批次。
    /// </summary>
    public class TestSession
    {
        /// <summary>
        /// 快照保存时间（用于弹窗展示"上次是什么时候中断的"）
        /// </summary>
        public DateTime SavedAt { get; set; }

        /// <summary>
        /// 当时的批号（恢复时写回 DeviceManager.CurrentLotNumber，保证日志追溯连贯）
        /// </summary>
        public string LotNumber { get; set; } = "";

        /// <summary>
        /// 在测工位清单（每台一条）
        /// </summary>
        public List<TestSessionStation> Stations { get; set; } = new List<TestSessionStation>();
    }

    /// <summary>
    /// 单个在测工位的任务参数快照
    /// </summary>
    public class TestSessionStation
    {
        /// <summary>工位编号（1 ~ TotalBarometers）</summary>
        public int DeviceId { get; set; }

        /// <summary>绑定的产品 SN（可为空，仅用于追溯展示）</summary>
        public string SerialNumber { get; set; } = "";

        /// <summary>配方名称（可为空，仅用于追溯展示）</summary>
        public string RecipeName { get; set; } = "";

        /// <summary>
        /// 本次老化时长（秒）。0 = 不限时长（手动停止）。
        /// 启动时定格：工位配方的启动时间(StartTime) &gt; 0 用配方值，否则回退全局配置。
        /// </summary>
        public int DurationSeconds { get; set; }

        /// <summary>
        /// 延时开启（秒）：开阀后至少等这么久才上电。启动时从工位配方的
        /// 延时时间(DelayTime) 定格；未配置为 0（真空到位立即上电）。
        /// </summary>
        public int DelaySeconds { get; set; }

        /// <summary>
        /// 本次的真空到位判定/报警阈值（kPa，负值如 -5）。
        /// 配方负压值优先，未配置回退全局 AlarmPressureThresholdKPa。
        /// </summary>
        public decimal AlarmThresholdKPa { get; set; }

        /// <summary>
        /// 中断时的子阶段（【V1.67 新增】断电续跑用：(int)AgingPhase）。
        /// Vacuuming=还没上电→恢复时整段重跑；Aging=已上电→可按剩余时长续跑。
        /// 老快照没有本字段（默认 0=None）→ 按整段重跑，安全回退。
        /// </summary>
        public int Phase { get; set; }

        /// <summary>
        /// 上电时刻（【V1.67 新增】老化计时起点；还没上电=MinValue）。
        /// 续跑剩余时长 = DurationSeconds - (SavedAt - PowerOnTime)，断电期间不计入老化。
        /// </summary>
        public DateTime PowerOnTime { get; set; }
    }
}
