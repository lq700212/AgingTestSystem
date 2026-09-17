
using System;
using System.Collections.Generic;
using AgingTestSystem.Models;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 预置工艺策略（傻瓜化：现场不用逐项理解 20 个策略 key，
    /// 在工艺策略窗顶部下拉选 A/B/C/D，点"套用"即整套生效，切预置=换整套行为）。
    /// 【为什么要做预置】
    /// 本机是显示屏烧屏老化线（合肥显耀 MicroLED 微显示：屏小、怕过温；
    /// 炉内单温控探头 + 冷却送风机，72 工位真空吸附固定、载台上电点亮）。
    /// 烧屏工艺大差不差就四种情况：①常用（客户常开：阀电同开保持常开，先试这个）
    /// ②量产标准跑 ③新治具/新配方调试（误报多，先跑起来）④出货放行（最严）。
    /// 现场工艺没时间逐项核对，
    /// 那就把几种情况提前配好：常用直接套 A；标准误报多切 C；出货放行切 D。
    /// 【设计三原则（改预置内容前必读）】
    /// 1) 只动"行为开关"，不动"自由文本/配方/机器参数"：14 个开关 key 全在
    ///    <see cref="ProjectPolicyStore.PolicyKeys"/> 里（跟项目，走 Policy.json）。
    ///    MES 映射/自定义规则/完成表达式/报表列/画面字典/破空点位是各现场手填的，
    ///    切预置不能把人填好的东西洗掉；时长/阈值/温度上限是配方或跟机器的参数，
    ///    预置无权碰（超温上限这种涉及安全的更不能代填，必须人按炉温填）。
    ///    例外（V1.105）：跟项目走的策略数值（PolicyKeys 成员，如真空超时）可进
    ///    NumericValues——套用时一并写入本项目 Policy.json（与开关同一条保存路），
    ///    不参与 DetectPreset（探测只认 14 开关：数值是各项目的微调，不算身份）。
    ///    配方延时（DelayTime）是配方项，任何预置都不写：无配方默认即 0，有配方按配方来。
    /// 【设计三原则（改预置内容前必读）】
    /// 1) 只动"行为开关"，不动"自由文本/配方/机器参数"：14 个开关 key 全在
    ///    <see cref="ProjectPolicyStore.PolicyKeys"/> 里（跟项目，走 Policy.json）。
    ///    MES 映射/自定义规则/完成表达式/报表列/画面字典/破空点位是各现场手填的，
    ///    切预置不能把人填好的东西洗掉；时长/阈值/温度上限是配方或跟机器的参数，
    ///    预置无权碰（超温上限这种涉及安全的更不能代填，必须人按炉温填）。
    /// 2) 存的是"英文存储值"（枚举英文名/布尔小写），与 PersistChanges 落盘口径
    ///    一字不差——ApplyToConfig 复用 <see cref="ProjectPolicyStore.ParseValue"/>
    ///    做类型转换，和保存走的是同一套解析，预置值永远"存得进去"。
    /// 3) 每个预置都是"完整集合"（14 项全列），切 A→B 不会残留 A 的某一项；
    ///    DetectPreset 只比这 14 项：全对上=该预置，差一项=自定义。
    /// 【四个预置速览】
    /// A 常用（默认首选）：宽松试产整套开关＋本项目真空超时写 0（不限时等），
    ///    配方延时填 0 即阀电同开（无配方默认即 0，有配方按配方来），建成后失压
    ///    只记不停——不关闭、不断电、不报警。
    /// B 标准烧屏：量产标准，门槛稍高但每道拦都有中文指引，
    ///    被拦按提示填上即可（填时长/扫SN/连风机）。
    /// C 宽松试产（跑起来优先）：新治具/样机调试，误报变提示、人工复判兜底。
    /// D 严格出货（放行标准）：B 的严格版 + 启动定格追溯 + 超温联停 + 画面记录。
    ///    开 D 前置：本机【超温上限】必须先填＞0（报警节点里填，按炉子工艺温度定），
    ///    否则保存即拦——这是故意的（fail-safe：没上限的联停等于没配，不能悄悄开）。
    /// A 与 C 除压力报警开关外全同（A 以 C 为基准，V1.106 起；2.0.0 起 A 关/C 开），差的是报警开关＋超时 0；
    /// 探测时数值也算身份：C 开关＋超时 0＋报警关即 A（见 DetectPreset）。
    /// 【自定义槽】下拉末项“自定义”是独立槽（跟项目走，见 ProjectPolicyStore.CustomFilePath）：
    /// 初始内容＝A 的快照，之后改自定义只写槽文件，A/B/C/D 是代码写死的谁也覆盖不了。
    /// 【导入导出】文件装“全部跟项目策略 key”（含 MES 映射/规则/报表等自由文本，
    /// 见 BuildExportValues）：导出可选 A/B/C/D/自定义任一源，导入一律进自定义槽并即时生效。
    /// </summary>
    public static class PolicyPresets
    {
        /// <summary>自定义槽 Id（下拉末项，显示名见 CustomTitle，只叫“自定义”不带括号）。</summary>
        public const string CustomId = "Custom";

        /// <summary>自定义下拉显示名（单源：下拉选项/回显/导入导出源选择全读它，不各写一份）。</summary>
        public const string CustomTitle = "自定义";

        /// <summary>自定义说明行·已改动（槽内容与 A 不一致时回显它，不带括号描述）。</summary>
        public const string CustomScenario = "当前配置与A/B/C/D都不完全一致，可重选一套覆盖。";

        /// <summary>自定义说明行·未改动（槽内容与 A 一致时回显它：槽还是初始快照）。</summary>
        public const string CustomScenarioDefault = "当前自定义为默认配置，与预置A一致。";

        /// <summary>自定义悬停全文·已改动（与说明行同源，同样不带括号描述）。</summary>
        public const string CustomTip = "自定义\r\n\r\n"
            + "当前 14 个行为开关与A/B/C/D都不完全一致，"
            + "下拉重选一套并点“套用预置”可整体覆盖。";

        /// <summary>自定义悬停全文·未改动（与默认说明行同源）。</summary>
        public const string CustomTipDefault = "自定义\r\n\r\n"
            + "尚未改动过，内容与预置A一致；"
            + "改动后自动存入自定义槽，预置A不受影响。";

        /// <summary>
        /// 自定义槽是否还是初始快照（纯函数：只比 A 管辖的开关＋数值，
        /// MES/规则等自由文本不算身份——初始槽的自由文本取种子现状，本来就和 A 无关）。
        /// 槽空/null 即非初始（无槽可比，说明行按"已改动"口径回显差异文案）。
        /// </summary>
        public static bool IsCustomPristine(Dictionary<string, string> customSnapshot)
        {
            try
            {
                Dictionary<string, string> presetA = GetAllValues("A");
                Dictionary<string, string> snap =
                    ProjectPolicyStore.FilterToPolicyKeys(customSnapshot);
                if (presetA == null || snap.Count == 0) return false;
                foreach (var kv in presetA)
                {
                    string got;
                    if (!snap.TryGetValue(kv.Key, out got)) return false;
                    if (!string.Equals(got != null ? got.Trim() : "",
                        kv.Value != null ? kv.Value.Trim() : "",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// 预置管辖的 14 个行为开关（【唯一名单】增减必须同步改三处：
        /// 每个预置的 Values、KeyLabels、回归"管辖全是PolicyKeys成员"用例）。
        /// </summary>
        public static readonly List<string> GovernedKeys = new List<string>
        {
            "ZeroDurationPolicy",
            "EmptySnPolicy",
            "FanDisconnectPolicy",
            "VacuumFailKind",
            "CompletionJudgePolicy",
            "PowerLossPolicy",
            "AgingPressureLossPolicy",
            "PressureAlarmEnabled",
            "MuteAllAlarms",
            "CompletionAction",
            "EventIdentityMode",
            "FanTempShutdownEnabled",
            "SkipVacuum",
            "DisplayModeEnabled",
        };

        /// <summary>开关中文名（套用确认框列"改了哪几项"用，不进存盘）。</summary>
        public static readonly Dictionary<string, string> KeyLabels =
            new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "ZeroDurationPolicy", "0时长策略" },
            { "EmptySnPolicy", "空SN策略" },
            { "FanDisconnectPolicy", "风机断连" },
            { "VacuumFailKind", "真空责任" },
            { "CompletionJudgePolicy", "完成判定" },
            { "PowerLossPolicy", "断电恢复" },
            { "AgingPressureLossPolicy", "老化失压" },
            { "PressureAlarmEnabled", "压力报警" },
            { "MuteAllAlarms", "报警全关" },
            { "CompletionAction", "完成动作" },
            { "EventIdentityMode", "事件身份口径" },
            { "FanTempShutdownEnabled", "超温联停" },
            { "SkipVacuum", "跳过抽真空" },
            { "DisplayModeEnabled", "画面维度" },
            // 数值项中文名（NumericValues 差异预览用，不进存盘；只收 PolicyKeys 成员）
            { "VacuumConfirmTimeoutMs", "真空超时" },
        };

        /// <summary>单个预置定义（纯数据：UI 下拉/确认框/说明提示全读它，不另写一份文案）。</summary>
        public class PolicyPresetDef
        {
            /// <summary>预置 Id（"A"/"B"/"C"/"D"，下拉存它；项目未上线，改名不动存盘，直改）。</summary>
            public string Id;
            /// <summary>下拉显示名（如"预置A·常用（阀电同开保持常开）"）。</summary>
            public string Title;
            /// <summary>一句话适用场景（下拉下方灰字 + 悬停提示）。</summary>
            public string Scenario;
            /// <summary>什么时候切走（A→B→C 的换挡指引，写给现场看的）。</summary>
            public string HowToSwitch;
            /// <summary>前置条件（空=零门槛直接套；C 有一条：本机超温上限先填＞0）。</summary>
            public string Requires;
            /// <summary>14 项存储值（key→英文存储值/小写布尔，与落盘口径一致）。</summary>
            public Dictionary<string, string> Values;
            /// <summary>
            /// 数值项存储值（key→存储字符串，如真空超时毫秒数；V1.105 新增）。
            /// 只要 PolicyKeys 成员才收（跟项目走，套用时与 Values 同一条保存路进 Policy.json）。
            /// 探测口径（见 DetectPreset）：无数值项的预置只认 14 开关；
            /// 带数值项的预置（当前只有 A 常用）数值也算身份（C 开关＋超时 0 即 A）。
            /// B/C/D 为空集合（行为与旧版一致）。
            /// </summary>
            public Dictionary<string, string> NumericValues;
        }

        /// <summary>全部预置（顺序即试用顺序 A→B→C→D，下拉按此排；A 常用放首位，现场先试这个）。</summary>
        public static readonly List<PolicyPresetDef> All = new List<PolicyPresetDef>
        {
                // A 常用（客户常开工艺，阀电同开保持常开，一键套用免逐项改）：
                // 14 开关照抄 C 宽松试产再关掉压力报警（误报变提示、人工复判兜底，常开不报警；报警全关仍关）；
            // 数值项把本项目真空超时写 0（不限时等）。
            // 配方延时不归预置管：填 0 即阀电同开（无配方默认即 0，有配方按配方来）。
            new PolicyPresetDef
            {
                Id = "A",
                Title = "预置A·常用（阀电同开保持常开）",
                Scenario = "常用工艺（客户常开）：真空吸附与上电同时开、保持常开不关闭。"
                    + "行为开关与宽松试产一致（只记不停、人工复判，不误报警）；"
                    + "本项目真空超时随套用写0（不限时等）。",
                HowToSwitch = "配方延时填0即阀电同开（无配方默认即0，有配方按配方来）；"
                    + "超时0随套用写入本项目；建成后失压只记不停。"
                    + "回到标准→切B；出货放行→切D。",
                Requires = "",
                Values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    // 0时长/空SN只警告：先放行，人工复判兜底（常开不报警的核心）。
                    { "ZeroDurationPolicy", "Warn" },
                    { "EmptySnPolicy", "Warn" },
                    // 风机只提示：提示一次照跑（人必须在场看炉温）。
                    { "FanDisconnectPolicy", "LogOnly" },
                    // 真空责任=装夹异常：不冤枉产品，可重测。
                    { "VacuumFailKind", "FixtureAlarm" },
                    // 完成待判定：到时标"待判定"，下料人工录 PASS/FAIL + 不良代码。
                    { "CompletionJudgePolicy", "PendingReview" },
                    // 断电续跑剩余：省时间（重抽真空后补足剩余时长）。
                    { "PowerLossPolicy", "ResumeRemaining" },
                    // 老化失压只记不停：保持常开的核心，
                    // 管路波动/失压记一条事件继续烧，不关阀不断电。
                    { "AgingPressureLossPolicy", "KeepRunning" },
                    // 压力报警=关：阈值越限与建立超时全不报，负压阀保持常开（常用常开的另一半）。
                    { "PressureAlarmEnabled", "false" },
                    // 报警全关=关：A 只静默真空类，DI/失联/规则照常（最高级静默只走手动/自定义，预置不带）。
                    { "MuteAllAlarms", "false" },
                    // 只下电关阀：最安静。
                    { "CompletionAction", "PowerOffOnly" },
                    { "EventIdentityMode", "RecordTime" },
                    // 超温联停=关：零门槛（上限还没按炉温填，开了保存即拦）。
                    { "FanTempShutdownEnabled", "false" },
                    // 跳过抽真空=关：有真空治具永不跳过（A 只是不限时等，不是不看真空）。
                    { "SkipVacuum", "false" },
                    // 画面维度=关（现状零打扰）。
                    { "DisplayModeEnabled", "false" },
                },
                NumericValues = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    // 真空超时 0=关闭：不限时等，不判建立超时（只靠老化失压记录+人工停止兜底）
                    { "VacuumConfirmTimeoutMs", "0" },
                },
            },
            new PolicyPresetDef
            {
                Id = "B",
                Title = "预置B·标准烧屏（量产标准）",
                Scenario = "量产标准跑：72 台真空吸附 + 载台上电，炉子加热、风机冷却，"
                    + "到时自动下电蜂鸣提醒取料。",
                HowToSwitch = "启动被拦就按提示填（时长/SN/风机）；误报多（装夹/管路波动）→切C；"
                    + "出货放行→切D。",
                Requires = "",
                Values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    // 0时长硬拦截：烧屏无限点亮最危险（72 台彻夜空烧），配方/全局必须填时长；
                    // 真要"不限时长手动停"的工艺→切C（Warn）。
                    { "ZeroDurationPolicy", "Block" },
                    // 空SN硬拦截：MicroLED 量产无 SN 无法追溯单体；样机没码→切C。
                    { "EmptySnPolicy", "Block" },
                    // 风机断连阻断启动：炉子加热 + 单探头，无冷却点火=过温风险；
                    // 只管"启动那一下"，跑中掉线不停机（另有超温联停管）。
                    { "FanDisconnectPolicy", "BlockStart" },
                    // 真空责任=产品：首试按产品记，不良不流出；装夹问题多→切C记装夹异常。
                    { "VacuumFailKind", "ProductFail" },
                    // 完成自动PASS：到时无报警即 PASS·待取料；要人工逐台复判→切C。
                    { "CompletionJudgePolicy", "AutoPass" },
                    // 断电整台重测：评审结论（断电不连续，已跑作废）；试产省时间→切C续跑。
                    { "PowerLossPolicy", "RestartFull" },
                    // 老化失压停机：失压还烧=带病烧屏；管路波动误报多→切C只记不停。
                    { "AgingPressureLossPolicy", "StopOnLoss" },
                    // 压力报警=开：阈值越限与建立超时正常报警断电（标准保护）。
                    { "PressureAlarmEnabled", "true" },
                    // 报警全关=关：最高级静默只走手动/自定义，预置不带（量产默认必须报警）。
                    { "MuteAllAlarms", "false" },
                    // 下电+蜂鸣：提醒取料；本机无破空阀，泄压类动作预置里没有（选了保存即拦）。
                    { "CompletionAction", "PowerOffAndBeep" },
                    // 事件口径=现值（现状）；严格追溯→切D定格。
                    { "EventIdentityMode", "RecordTime" },
                    // 超温联停=关：零门槛（上限还没按炉温填，开了保存即拦）；填好上限后可手动开，或切D。
                    { "FanTempShutdownEnabled", "false" },
                    // 跳过抽真空=关：四个预置全关——真空治具永不跳过（无真空治具的机械夹具才手动开）。
                    { "SkipVacuum", "false" },
                    // 画面维度=关（现状零打扰，PG 画面本来也由治具/PG 固定输出）；要记追溯→切D。
                    { "DisplayModeEnabled", "false" },
                },
                NumericValues = new Dictionary<string, string>(StringComparer.Ordinal),
            },
            new PolicyPresetDef
            {
                Id = "C",
                Title = "预置C·宽松试产（跑起来优先）",
                Scenario = "新治具/新配方/样机调试：误报变提示、人工复判兜底，先跑起来摸条件。",
                HowToSwitch = "条件摸熟、误报收敛→切回B；出货放行→切D。"
                    + "注意：C 是试产口径（续跑/装夹/待判定），量产不要长期跑C。",
                Requires = "",
                Values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    // 0时长/空SN只警告：样机没码、时长还在摸，先警告放行（启动框点"是"照跑）。
                    { "ZeroDurationPolicy", "Warn" },
                    { "EmptySnPolicy", "Warn" },
                    // 风机只提示：单机调试风机可能没开，提示一次照跑（人必须在场看炉温）。
                    { "FanDisconnectPolicy", "LogOnly" },
                    // 真空责任=装夹异常：新治具装夹问题多，不冤枉产品，可重测。
                    { "VacuumFailKind", "FixtureAlarm" },
                    // 完成待判定：到时标"待判定"，下料人工录 PASS/FAIL + 不良代码（摸不良模式）。
                    { "CompletionJudgePolicy", "PendingReview" },
                    // 断电续跑剩余：试产省时间（重抽真空后补足剩余时长）；量产以评审结论（重测）为准。
                    { "PowerLossPolicy", "ResumeRemaining" },
                    // 老化失压只记不停：管路波动/开门看料容忍，边沿记一条事件继续烧，不刷屏。
                    { "AgingPressureLossPolicy", "KeepRunning" },
                    // 压力报警=开：只靠失压策略容忍波动，抽真空阶段超时仍报警（与 A 的全关是两档）。
                    { "PressureAlarmEnabled", "true" },
                    // 报警全关=关：最高级静默只走手动/自定义，预置不带。
                    { "MuteAllAlarms", "false" },
                    // 只下电关阀：最安静（调试现场够吵了）。
                    { "CompletionAction", "PowerOffOnly" },
                    { "EventIdentityMode", "RecordTime" },
                    { "FanTempShutdownEnabled", "false" },
                    { "SkipVacuum", "false" },
                    { "DisplayModeEnabled", "false" },
                },
                NumericValues = new Dictionary<string, string>(StringComparer.Ordinal),
            },
            // D 严格出货（放行标准，一键套用免逐项改）：
            // B 标准的严格版 + 启动定格追溯 + 超温联停 + 画面记录。
            new PolicyPresetDef
            {
                Id = "D",
                Title = "预置D·严格出货（放行标准）",
                Scenario = "量产放行：B 的严格版 + 启动定格追溯 + 超温联停 + 画面记录，"
                    + "全程无报警才算 PASS。",
                HowToSwitch = "D 是终点口径，不用再切；若 D 太严（频繁拦）→退回B找原因，"
                    + "不要长期混用（追溯口径会两边倒）。",
                // 前置写死在这里：UI 确认框与说明都读它，不另写一份。
                Requires = "本机【超温上限】必须先填＞0（报警节点【超温上限(°C)】，按炉子工艺温度定），"
                    + "否则保存即拦。时长/配方/SN/风机要求与B相同。",
                Values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "ZeroDurationPolicy", "Block" },
                    { "EmptySnPolicy", "Block" },
                    { "FanDisconnectPolicy", "BlockStart" },
                    { "VacuumFailKind", "ProductFail" },
                    { "CompletionJudgePolicy", "AutoPass" },
                    { "PowerLossPolicy", "RestartFull" },
                    { "AgingPressureLossPolicy", "StopOnLoss" },
                    // 压力报警=开：放行标准全程无报警才算 PASS，阈值保护全开。
                    { "PressureAlarmEnabled", "true" },
                    // 报警全关=关：放行标准不允许任何静默。
                    { "MuteAllAlarms", "false" },
                    { "CompletionAction", "PowerOffAndBeep" },
                    // 事件口径=启动定格：中途重绑 SN 不污染已跑任务的事件归属（CSV/报表/MES统一）。
                    { "EventIdentityMode", "StartSnapshot" },
                    // 超温联停=开：单探头炉无人值守烧屏的最后一道闸；上限取本机配置，
                    // 为0时保存即拦（ValidatePolicyCombination），逼人先填上限。
                    { "FanTempShutdownEnabled", "true" },
                    { "SkipVacuum", "false" },
                    // 画面维度=开：烧屏画面（白场/RGB/棋盘格…）记追溯；空=允许，老配方不炸。
                    { "DisplayModeEnabled", "true" },
                },
                NumericValues = new Dictionary<string, string>(StringComparer.Ordinal),
            },
        };

        /// <summary>按 Id 找预置（找不到/空返回 null；"Custom"不是预置，同样 null）。</summary>
        public static PolicyPresetDef Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var p in All)
            {
                if (string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)) return p;
            }
            return null;
        }

        /// <summary>取某预置的存储值副本（调用方可直接喂 PersistChanges；未知 Id 返回 null）。</summary>
        public static Dictionary<string, string> GetPresetValues(string id)
        {
            PolicyPresetDef def = Find(id);
            if (def == null || def.Values == null) return null;
            return new Dictionary<string, string>(def.Values, StringComparer.Ordinal);
        }

        /// <summary>
        /// 取某预置的数值项副本（V1.105 新增；未知 Id 返回 null，已知但无数值返回空字典——
        /// 与 GetPresetValues 对称，调用方无需判 null 即可合并）。
        /// </summary>
        public static Dictionary<string, string> GetNumericValues(string id)
        {
            PolicyPresetDef def = Find(id);
            if (def == null) return null;
            if (def.NumericValues == null) return new Dictionary<string, string>(StringComparer.Ordinal);
            return new Dictionary<string, string>(def.NumericValues, StringComparer.Ordinal);
        }

        /// <summary>
        /// 组装导出文件内容（纯函数：文件装“全部跟项目策略 key”，
        /// 含 MES 映射/规则/报表等自由文本，不止 14 开关）。
        /// A/B/C/D＝该预置的开关＋数值叠加到当前配置的自由文本上（预置不管自由文本，
        /// 导出时把本机已填的一并带走，目标工控机才不用重填）；
        /// 自定义＝自定义槽的整包快照（槽空时退回当前配置，至少是全量）。
        /// 未知 Id 返回 null。
        /// </summary>
        public static Dictionary<string, string> BuildExportValues(
            string sourceId, DeviceConfig current, Dictionary<string, string> customSnapshot)
        {
            if (string.IsNullOrEmpty(sourceId)) return null;
            if (string.Equals(sourceId, CustomId, StringComparison.OrdinalIgnoreCase))
            {
                Dictionary<string, string> snap =
                    ProjectPolicyStore.FilterToPolicyKeys(customSnapshot);
                if (snap.Count > 0) return snap;
                return ProjectPolicyStore.ToPolicyDict(current);
            }
            Dictionary<string, string> preset = GetAllValues(sourceId);
            if (preset == null) return null;
            Dictionary<string, string> baseDict = ProjectPolicyStore.ToPolicyDict(current);
            foreach (var kv in preset) baseDict[kv.Key] = kv.Value;
            return baseDict;
        }

        /// <summary>
        /// 取某预置的全部落盘值（14 开关＋数值项合并，V1.105 新增；
        /// 工艺策略窗"套用预置"走这一份：差异预览/校验/保存看到的是同一集合）。
        /// 未知 Id 返回 null。
        /// </summary>
        public static Dictionary<string, string> GetAllValues(string id)
        {
            Dictionary<string, string> values = GetPresetValues(id);
            if (values == null) return null;
            Dictionary<string, string> numerics = GetNumericValues(id);
            if (numerics != null)
            {
                foreach (var kv in numerics) values[kv.Key] = kv.Value;
            }
            return values;
        }

        /// <summary>
        /// 把预置写进内存配置（UI 套用前先填内存？不——套用走 PersistChanges 直写文件+
        /// 热回写，这里只给"预览差异/单测"用：纯函数，不碰文件不碰静态）。
        /// </summary>
        /// <returns>null=12 项全写进去了；非 null=错误描述（未知预置/null 配置/某项转不过去）</returns>
        public static string ApplyToConfig(DeviceConfig config, string id)
        {
            if (config == null) return "配置对象为空，无法套用预置。";
            // 开关＋数值一起写（数值走同一解析口；探测口径见 DetectPreset：
            // 无数值项的预置只认 14 开关，带数值项的（A 常用）数值也算身份）
            Dictionary<string, string> values = GetAllValues(id);
            if (values == null) return "未知预置：" + (id ?? "");
            var bad = new List<string>();
            foreach (var kv in values)
            {
                try
                {
                    var prop = typeof(DeviceConfig).GetProperty(kv.Key);
                    if (prop == null || !prop.CanWrite) { bad.Add(kv.Key + "(无此属性)"); continue; }
                    // 与 ProjectPolicyStore.ApplyOverlay 同一解析口：预置值转不过去
                    // 说明预置写错了（不是用户配错了），必须修预置本身。
                    object converted = ProjectPolicyStore.ParseValue(prop.PropertyType, kv.Value);
                    if (converted == null) { bad.Add(kv.Key + "(值非法)"); continue; }
                    prop.SetValue(config, converted, null);
                }
                catch (Exception ex) { bad.Add(kv.Key + "(" + ex.GetType().Name + ")"); }
            }
            if (bad.Count > 0) return "预置写入失败：" + string.Join("、", bad.ToArray());
            return null;
        }

        /// <summary>
        /// 反查当前配置≈哪个预置（纯函数：13 项逐项序列化比对，全对上才算；
        /// 带数值项的预置（当前只有 A 常用）数值也算身份：C 开关＋超时 0 即 A，
        /// 超时非 0 即 C——A 以 C 为基准，超时 0 是两者唯一的差别，不认数值就分不出来）。
        /// </summary>
        /// <returns>预置 Id（"A"/"B"/"C"/"D"）；凑不上任何一个返回 <see cref="CustomId"/>。</returns>
        public static string DetectPreset(DeviceConfig config)
        {
            if (config == null) return CustomId;
            foreach (var p in All)
            {
                if (p.Values == null) continue;
                bool allMatch = true;
                foreach (string key in GovernedKeys)
                {
                    string want;
                    if (!p.Values.TryGetValue(key, out want)) { allMatch = false; break; }
                    if (!string.Equals(ToStorageString(GetProp(config, key)),
                        want != null ? want.Trim() : "", StringComparison.OrdinalIgnoreCase))
                    {
                        allMatch = false;
                        break;
                    }
                }
                // 数值身份：只对"带数值项"的预置加赛一轮（无数值项的走上面 14 开关即定）。
                if (allMatch && p.NumericValues != null)
                {
                    foreach (var kv in p.NumericValues)
                    {
                        if (!string.Equals(ToStorageString(GetProp(config, kv.Key)),
                            kv.Value != null ? kv.Value.Trim() : "", StringComparison.OrdinalIgnoreCase))
                        {
                            allMatch = false;
                            break;
                        }
                    }
                }
                if (allMatch) return p.Id;
            }
            return CustomId;
        }

        /// <summary>读内存属性并序列化成"存储口径"字符串（枚举英文名/布尔小写/数字原文）。</summary>
        private static object GetProp(DeviceConfig config, string key)
        {
            try
            {
                var prop = typeof(DeviceConfig).GetProperty(key);
                if (prop == null) return null;
                return prop.GetValue(config, null);
            }
            catch { return null; }
        }

        /// <summary>
        /// 值→存储字符串（与 ProcessPolicyForm.GetConfigString 同口径：
        /// 布尔小写 true/false，枚举 ToString 英文名，其余 ToString）。
        /// </summary>
        public static string ToStorageString(object v)
        {
            if (v == null) return "";
            if (v is bool) return ((bool)v) ? "true" : "false";
            return v.ToString();
        }
    }
}
