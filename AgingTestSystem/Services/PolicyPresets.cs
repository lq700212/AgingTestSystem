
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
    /// 烧屏工艺大差不差就四种情况：①量产标准跑（先试这个）②新治具/新配方
    /// 调试（误报多，先跑起来）③出货放行（最严）④客户常开（阀电同开保持常开）。
    /// 现场工艺没时间逐项核对，
    /// 那就把几种情况提前配好，A 不行切 B，B 不行切 C，客户常开直接套 D。
    /// 【设计三原则（改预置内容前必读）】
    /// 1) 只动"行为开关"，不动"自由文本/配方/机器参数"：12 个开关 key 全在
    ///    <see cref="ProjectPolicyStore.PolicyKeys"/> 里（跟项目，走 Policy.json）。
    ///    MES 映射/自定义规则/完成表达式/报表列/画面字典/破空点位是各现场手填的，
    ///    切预置不能把人填好的东西洗掉；时长/阈值/温度上限是配方或跟机器的参数，
    ///    预置无权碰（超温上限这种涉及安全的更不能代填，必须人按炉温填）。
    ///    例外（V1.105）：跟项目走的策略数值（PolicyKeys 成员，如真空超时）可进
    ///    NumericValues——套用时一并写入本项目 Policy.json（与开关同一条保存路），
    ///    不参与 DetectPreset（探测只认 12 开关：数值是各项目的微调，不算身份）。
    ///    配方延时（DelayTime）是配方项，任何预置都不写：无配方默认即 0，有配方按配方来。
    /// 【设计三原则（改预置内容前必读）】
    /// 1) 只动"行为开关"，不动"自由文本/配方/机器参数"：12 个开关 key 全在
    ///    <see cref="ProjectPolicyStore.PolicyKeys"/> 里（跟项目，走 Policy.json）。
    ///    MES 映射/自定义规则/完成表达式/报表列/画面字典/破空点位是各现场手填的，
    ///    切预置不能把人填好的东西洗掉；时长/阈值/温度上限是配方或跟机器的参数，
    ///    预置无权碰（超温上限这种涉及安全的更不能代填，必须人按炉温填）。
    /// 2) 存的是"英文存储值"（枚举英文名/布尔小写），与 PersistChanges 落盘口径
    ///    一字不差——ApplyToConfig 复用 <see cref="ProjectPolicyStore.ParseValue"/>
    ///    做类型转换，和保存走的是同一套解析，预置值永远"存得进去"。
    /// 3) 每个预置都是"完整集合"（12 项全列），切 A→B 不会残留 A 的某一项；
    ///    DetectPreset 只比这 12 项：全对上=该预置，差一项=自定义。
    /// 【三个预置速览】
    /// A 标准烧屏（推荐首试）：量产标准，门槛稍高但每道拦都有中文指引，
    ///    被拦按提示填上即可（填时长/扫SN/连风机）。
    /// B 宽松试产（跑起来优先）：新治具/样机调试，误报变提示、人工复判兜底。
    /// C 严格出货（放行标准）：A 的严格版 + 启动定格追溯 + 超温联停 + 画面记录。
    ///    开 C 前置：本机【超温上限】必须先填＞0（报警节点里填，按炉子工艺温度定），
    ///    否则保存即拦——这是故意的（fail-safe：没上限的联停等于没配，不能悄悄开）。
    /// D 客户常开（阀电同开保持常开）：A 标准的 12 开关只动"老化失压→只记不停"，
    ///    另把本项目真空超时写 0（不限时等，数值项见 NumericValues）；配方延时填 0
    ///    即阀电同开（无配方默认即 0，有配方按配方来——配方项预置不动）。
    /// </summary>
    public static class PolicyPresets
    {
        /// <summary>自定义：当前 12 项凑不出任何预置（人手微调过），下拉显示此行。</summary>
        public const string CustomId = "Custom";

        /// <summary>
        /// 预置管辖的 12 个行为开关（【唯一名单】增减必须同步改三处：
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
            /// <summary>预置 Id（"A"/"B"/"C"，下拉存它；改名即不兼容，不改）。</summary>
            public string Id;
            /// <summary>下拉显示名（如"预置A·标准烧屏（推荐首试）"）。</summary>
            public string Title;
            /// <summary>一句话适用场景（下拉下方灰字 + 悬停提示）。</summary>
            public string Scenario;
            /// <summary>什么时候切走（A→B→C 的换挡指引，写给现场看的）。</summary>
            public string HowToSwitch;
            /// <summary>前置条件（空=零门槛直接套；C 有一条：本机超温上限先填＞0）。</summary>
            public string Requires;
            /// <summary>12 项存储值（key→英文存储值/小写布尔，与落盘口径一致）。</summary>
            public Dictionary<string, string> Values;
            /// <summary>
            /// 数值项存储值（key→存储字符串，如真空超时毫秒数；V1.105 新增）。
            /// 只要 PolicyKeys 成员才收（跟项目走，套用时与 Values 同一条保存路进 Policy.json）；
            /// 不参与 DetectPreset（探测只认 12 开关）；A/B/C 为空集合（行为与旧版一致）。
            /// </summary>
            public Dictionary<string, string> NumericValues;
        }

        /// <summary>全部预置（顺序即试用顺序 A→B→C→D，下拉按此排）。</summary>
        public static readonly List<PolicyPresetDef> All = new List<PolicyPresetDef>
        {
            new PolicyPresetDef
            {
                Id = "A",
                Title = "预置A·标准烧屏（推荐首试）",
                Scenario = "量产标准跑：72 台真空吸附 + 载台上电，炉子加热、风机冷却，"
                    + "到时自动下电蜂鸣提醒取料。",
                HowToSwitch = "启动被拦就按提示填（时长/SN/风机）；误报多（装夹/管路波动）→切B；"
                    + "出货放行→切C。",
                Requires = "",
                Values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    // 0时长硬拦截：烧屏无限点亮最危险（72 台彻夜空烧），配方/全局必须填时长；
                    // 真要"不限时长手动停"的工艺→切B（Warn）。
                    { "ZeroDurationPolicy", "Block" },
                    // 空SN硬拦截：MicroLED 量产无 SN 无法追溯单体；样机没码→切B。
                    { "EmptySnPolicy", "Block" },
                    // 风机断连阻断启动：炉子加热 + 单探头，无冷却点火=过温风险；
                    // 只管"启动那一下"，跑中掉线不停机（另有超温联停管）。
                    { "FanDisconnectPolicy", "BlockStart" },
                    // 真空责任=产品：首试按产品记，不良不流出；装夹问题多→切B记装夹异常。
                    { "VacuumFailKind", "ProductFail" },
                    // 完成自动PASS：到时无报警即 PASS·待取料；要人工逐台复判→切B。
                    { "CompletionJudgePolicy", "AutoPass" },
                    // 断电整台重测：评审结论（断电不连续，已跑作废）；试产省时间→切B续跑。
                    { "PowerLossPolicy", "RestartFull" },
                    // 老化失压停机：失压还烧=带病烧屏；管路波动误报多→切B只记不停。
                    { "AgingPressureLossPolicy", "StopOnLoss" },
                    // 下电+蜂鸣：提醒取料；本机无破空阀，泄压类动作预置里没有（选了保存即拦）。
                    { "CompletionAction", "PowerOffAndBeep" },
                    // 事件口径=现值（现状）；严格追溯→切C定格。
                    { "EventIdentityMode", "RecordTime" },
                    // 超温联停=关：零门槛（上限还没按炉温填，开了保存即拦）；填好上限后可手动开，或切C。
                    { "FanTempShutdownEnabled", "false" },
                    // 跳过抽真空=关：四个预置全关——真空治具永不跳过（无真空治具的机械夹具才手动开）。
                    { "SkipVacuum", "false" },
                    // 画面维度=关（现状零打扰，PG 画面本来也由治具/PG 固定输出）；要记追溯→切C。
                    { "DisplayModeEnabled", "false" },
                },
                NumericValues = new Dictionary<string, string>(StringComparer.Ordinal),
            },
            new PolicyPresetDef
            {
                Id = "B",
                Title = "预置B·宽松试产（跑起来优先）",
                Scenario = "新治具/新配方/样机调试：误报变提示、人工复判兜底，先跑起来摸条件。",
                HowToSwitch = "条件摸熟、误报收敛→切回A；出货放行→切C。"
                    + "注意：B 是试产口径（续跑/装夹/待判定），量产不要长期跑B。",
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
                    // 只下电关阀：最安静（调试现场够吵了）。
                    { "CompletionAction", "PowerOffOnly" },
                    { "EventIdentityMode", "RecordTime" },
                    { "FanTempShutdownEnabled", "false" },
                    { "SkipVacuum", "false" },
                    { "DisplayModeEnabled", "false" },
                },
                NumericValues = new Dictionary<string, string>(StringComparer.Ordinal),
            },
            new PolicyPresetDef
            {
                Id = "C",
                Title = "预置C·严格出货（放行标准）",
                Scenario = "量产放行：A 的严格版 + 启动定格追溯 + 超温联停 + 画面记录，"
                    + "全程无报警才算 PASS。",
                HowToSwitch = "C 是终点口径，不用再切；若 C 太严（频繁拦）→退回A找原因，"
                    + "不要长期混用（追溯口径会两边倒）。",
                // 前置写死在这里：UI 确认框与说明都读它，不另写一份。
                Requires = "本机【超温上限】必须先填＞0（报警节点【超温上限(°C)】，按炉子工艺温度定），"
                    + "否则保存即拦。时长/配方/SN/风机要求与A相同。",
                Values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "ZeroDurationPolicy", "Block" },
                    { "EmptySnPolicy", "Block" },
                    { "FanDisconnectPolicy", "BlockStart" },
                    { "VacuumFailKind", "ProductFail" },
                    { "CompletionJudgePolicy", "AutoPass" },
                    { "PowerLossPolicy", "RestartFull" },
                    { "AgingPressureLossPolicy", "StopOnLoss" },
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
            // D 客户常开（阀电同开保持常开，一键套用免逐项改）：
            // 12 开关照抄 A，只动老化失压→只记不停；数值项把本项目真空超时写 0。
            // 配方延时不归预置管：无配方默认即 0（阀电同开），有配方按配方来。
            new PolicyPresetDef
            {
                Id = "D",
                Title = "预置D·客户常开（阀电同开保持常开）",
                Scenario = "客户常开工艺：真空吸附与上电同时开、保持常开不关闭。"
                    + "行为开关与A标准一致，只把老化失压改成只记不停；"
                    + "本项目真空超时随套用写0（不限时等）。",
                HowToSwitch = "配方延时填0即阀电同开（无配方默认即0，有配方按配方来）；"
                    + "超时0随套用写入本项目；建成后失压只记不停。"
                    + "误报多→切B；出货放行→切C；回到标准→切A。",
                Requires = "",
                Values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "ZeroDurationPolicy", "Block" },
                    { "EmptySnPolicy", "Block" },
                    { "FanDisconnectPolicy", "BlockStart" },
                    { "VacuumFailKind", "ProductFail" },
                    { "CompletionJudgePolicy", "AutoPass" },
                    { "PowerLossPolicy", "RestartFull" },
                    // 与 A 唯一的差别：老化失压只记不停——保持常开的核心，
                    // 管路波动/失压记一条事件继续烧，不关阀不断电。
                    { "AgingPressureLossPolicy", "KeepRunning" },
                    { "CompletionAction", "PowerOffAndBeep" },
                    { "EventIdentityMode", "RecordTime" },
                    { "FanTempShutdownEnabled", "false" },
                    // 跳过抽真空=关：有真空治具永不跳过（D 只是不限时等，不是不看真空）。
                    { "SkipVacuum", "false" },
                    { "DisplayModeEnabled", "false" },
                },
                NumericValues = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    // 真空超时 0=关闭：不限时等，不判建立超时（只靠老化失压报警+人工停止兜底）
                    { "VacuumConfirmTimeoutMs", "0" },
                },
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
        /// 取某预置的全部落盘值（12 开关＋数值项合并，V1.105 新增；
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
            // 开关＋数值一起写（数值走同一解析口；探测仍只认 12 开关，见 DetectPreset）
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
        /// 反查当前配置≈哪个预置（纯函数：12 项逐项序列化比对，全对上才算；
        /// 数值项不参与——数值是各项目的微调（如超时 0/15000），不算身份）。
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
