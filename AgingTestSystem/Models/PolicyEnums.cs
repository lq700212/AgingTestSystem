
using System;

namespace AgingTestSystem.Models
{
    /// <summary>
    /// 工艺策略枚举集（【V1.67 新增】一期"万物可配"的 L2 策略层）。
    ///
    /// 【为什么用枚举而不用 bool】
    /// 7 个待确认点本质都是"二选一行为"，bool 只能表达开/关、看不出语义；
    /// 枚举存字符串名（如 "Warn"/"Block"），配置文件可读、SettingsForm 下拉中文显示、
    /// 非法值兜底回默认值（= 现状行为，老项目零变化）。
    ///
    /// 【铁律：第一个枚举值必须是"现状行为"】
    /// 每个枚举的 0 值 = V1.67 之前的软件行为。解析失败/缺省一律回 0，
    /// 保证"不配 = 和以前一模一样"，这是出差改配置不翻车的底线。
    /// 新增选项只能往后追加，禁止改已有值的顺序（配置文件里存的是名字，改名即不兼容）。
    /// </summary>

    /// <summary>
    /// 0 时长启动策略（问题清单 Q13 前半）。
    /// 配方与全局都为 0 = 不限时长、永不到时完成（烧屏=无限点亮）。
    /// </summary>
    public enum ZeroDurationPolicy
    {
        /// <summary>只警告（现状）：启动框拼风险提示，点"是"照跑。</summary>
        Warn = 0,
        /// <summary>硬拦截：含 0 时长工位时直接阻断启动，不进确认框。</summary>
        Block = 1
    }

    /// <summary>
    /// 空 SN 启动策略（问题清单 Q13 后半）。
    /// 空 SN = 完成后无法追溯到单体。
    /// </summary>
    public enum EmptySnPolicy
    {
        /// <summary>只警告（现状）：启动框拼风险提示，点"是"照跑。</summary>
        Warn = 0,
        /// <summary>硬拦截：含空 SN 工位时直接阻断启动，不进确认框。</summary>
        Block = 1
    }

    /// <summary>
    /// 送风机断连策略（问题清单 Q16 前半：断连阻断否）。
    /// 注意只管"启动那一下"：跑起来之后风机掉了不停机（停机会误伤整批；
    /// 跑中停机要配超温联停开关 FanTempShutdownEnabled，那是另一回事）。
    /// </summary>
    public enum FanDisconnectPolicy
    {
        /// <summary>只提示（现状）：弹一次"没有温控"警告，测试照跑。</summary>
        LogOnly = 0,
        /// <summary>阻断启动：风机没连上就不让点火，先修风机。</summary>
        BlockStart = 1
    }

    /// <summary>
    /// 真空失败责任归属（问题清单 Q19）。
    /// 决定压力越限/真空建立失败记什么结果：产品 FAIL 还是装夹异常。
    /// </summary>
    public enum VacuumFailKind
    {
        /// <summary>产品责任（现状）：LastTestResult="FAIL"，计入产品不良。</summary>
        ProductFail = 0,
        /// <summary>治具责任：LastTestResult="装夹异常"，不计产品 FAIL，可重测。</summary>
        FixtureAlarm = 1
    }

    /// <summary>
    /// 完成判定口径（问题清单 Q22：老化≠合格）。
    /// </summary>
    public enum CompletionJudgePolicy
    {
        /// <summary>自动 PASS（现状）：到时无报警即 PASS·待取料。</summary>
        AutoPass = 0,
        /// <summary>待判定：到时标"待判定"，下料时人工录 PASS/FAIL+不良代码+处置。</summary>
        PendingReview = 1
    }

    /// <summary>
    /// 断电恢复策略（问题清单 Q21①）。
    /// </summary>
    public enum PowerLossPolicy
    {
        /// <summary>整台重测（现状）：满时长重跑，已跑作废（评审结论：断电不连续）。</summary>
        RestartFull = 0,
        /// <summary>续跑剩余时长：重抽真空（安全必备，真空已泄不能省），
        /// 老化计时按"中断时刻的剩余时长"补足（断电期间不计入老化）。</summary>
        ResumeRemaining = 1
    }

    /// <summary>
    /// 老化中失压策略（问题清单 Q11：老化中掉真空停不停）。
    /// 只管 Aging 阶段的压力越限；抽真空阶段的真空建立失败永远报警
    /// （否则阀开了永不上电、无限空等，操作员还不知道）。
    /// </summary>
    public enum AgingPressureLossPolicy
    {
        /// <summary>停机报警（现状）：关阀+断电+标 FAIL/装夹异常。</summary>
        StopOnLoss = 0,
        /// <summary>只记不停：写一条"失压保持运行"事件继续老化（边沿记一次，不刷屏）。</summary>
        KeepRunning = 1
    }

    /// <summary>
    /// 到时完成动作（问题清单 Q6/Q15：自动下电够吗）。
    /// </summary>
    public enum CompletionAction
    {
        /// <summary>只下电关阀（现状：关阀≠泄压，无声）。</summary>
        PowerOffOnly = 0,
        /// <summary>下电关阀 + PC 蜂鸣一声（提醒取料，无需硬件）。</summary>
        PowerOffAndBeep = 1,
        /// <summary>下电 + 开破空阀泄压（需硬件：VentValveDoPoint 配点位；未配则跳过并记日志）。</summary>
        PowerOffAndVent = 2,
        /// <summary>蜂鸣 + 泄压都要。</summary>
        PowerOffVentAndBeep = 3
    }
}
