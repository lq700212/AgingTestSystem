using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using AgingTestSystem.Models;
using AgingTestSystem.Services;
using Newtonsoft.Json;

namespace AgingTestSystem.Views
{
    /// <summary>
    /// 工艺策略图静态数据（工艺策略窗的图真相：
    /// 拓扑画死，文本按配置生成）。
    /// 【节点】启动开阀 / 抽真空 / 上电老化 / 完成下电 / 报警联动 / 断电恢复 / 下料判定 / MES上报。
    /// 每个节点挂它真正能改的配置 key（右栏编辑器按此生成）——条件写在边上，
    /// 改条件的入口永远在端点节点里，连线本身只读（防对着线发呆）。
    /// MES上报是纯配置节点（无连线：上报是正交的，启动/完成/报警/下料四个事件都可能发，
    /// 画连线反而误导；只挂"启用上报"总开关，触发器/字段映射/静态字段的入口在系统设置·MES 对接）。
    /// 【配置分层（V2.0.5）】右栏按 NodeKey.Tier 分常用/高级（高级默认折叠），
    /// 只影响显示；迁出策略窗的 key 见 MovedKeyTargets（仍跟项目进 Policy.json）。
    /// 【文本】BuildNodeLines / BuildEdgeLabel 纯静态，回归可单测（缺省配置长什么样锁死）。
    /// </summary>
    public static class PolicyGraph
    {
        /// <summary>编辑器种类（右栏按此建控件；数字走 TextBox，保存时 ValidateValue 拦）。</summary>
        public enum EditorKind
        {
            /// <summary>true/false 下拉</summary>
            Bool,
            /// <summary>策略枚举下拉（中文显示/英文存储，选项走 ProjectPolicyStore.EnumOptions）</summary>
            Enum,
            /// <summary>单行文本（数字/字符串/单行表达式）</summary>
            Text,
            /// <summary>多行文本（规则表）</summary>
            Multiline
        }

        /// <summary>
        /// 配置项分层（只影响右栏"默认展开/折叠"，不参与存盘、不影响执行）。
        /// 分层口径：现场按产品/工艺要动的 → 常用；工程细项/追溯/本机接线事实 → 高级。
        /// 新增 key 必须显式声明分层（构造参数没有缺省值，漏写编译不过），
        /// 防止以后加项默认挤进常用区、又把界面撑回一屏 30 项。
        /// </summary>
        public enum KeyTier
        {
            /// <summary>常用：默认展开（约 13 项，覆盖日常工艺决策）。</summary>
            Common,
            /// <summary>高级：默认折叠（工程细项/追溯/本机事实，改前先想清楚）。</summary>
            Advanced
        }

        /// <summary>节点可改的单个 key。</summary>
        public class NodeKey
        {
            public string Key;
            public string Label;
            public EditorKind Kind;
            /// <summary>常用/高级（决定右栏默认展开还是折叠）。</summary>
            public KeyTier Tier;
            /// <summary>组名（可空；报警节点按"压力保护/温度保护/报警行为/…"分组显示）。</summary>
            public string Group;
            public NodeKey(string key, string label, EditorKind kind, KeyTier tier, string group = null)
            {
                Key = key;
                Label = label;
                Kind = kind;
                Tier = tier;
                Group = group;
            }
        }

        /// <summary>节点定义（位置是缺省值，用户拖动后存 PolicyLayout.json 覆盖）。</summary>
        public class NodeDef
        {
            public string Id;
            public string Title;
            public Rectangle DefaultRect;
            public List<NodeKey> Keys = new List<NodeKey>();
            /// <summary>无 key 节点的说明文字（有 key 时不用）</summary>
            public string Info;
        }

        /// <summary>连线定义（拓扑固定；条件文本按配置生成，只读）。</summary>
        public class EdgeDef
        {
            public string Id;
            public string From;
            public string To;
            public bool Dashed;
            public EdgeDef(string id, string from, string to, bool dashed)
            {
                Id = id;
                From = from;
                To = to;
                Dashed = dashed;
            }
        }

        /// <summary>实时台数（画布每秒刷新，节点右上角计数徽标用）。</summary>
        public struct FlowCounts
        {
            public int Idle;
            public int Vacuuming;
            public int Aging;
            public int Completed;
            public int Fault;
            public int PendingJudge;
            public int Snapshot;
        }

        /// <summary>全部节点（顺序即绘制顺序，报警等侧栏后画，压边不断）。</summary>
        // 【缺省布局】左列主链（启动→抽真空→老化→完成）+ 右列分支（恢复/报警/下料），
        // 按截图式宽松两列摆：列间距 150px、行间距 45px+，节点加高到 110~150px
        //（3 行文本 + 标题 + 内边距不挤）。power 与 alarm 中心对齐（横向边水平），
        // done 与 unload 中心对齐（横向边水平），alarm 底到 unload 顶留 55px 走
        // 竖向虚线。改坐标后“复位布局”即生效；老版本 PolicyLayout.json 见 LayoutStore。
        //
        // 【配置项归属（V2.0.5 简化）】工艺策略窗只放"现场按工艺要动的决策项"；
        // 工程细项/追溯/本机接线事实迁回系统设置（见 MovedKeyTargets），
        // 对应节点保留为只读状态节点（画布仍显示实时值，右栏给"去哪改"的指引）。
        public static readonly List<NodeDef> Nodes = new List<NodeDef>
        {
            new NodeDef
            {
                Id = "start", Title = "启动开阀",
                DefaultRect = new Rectangle(60, 30, 220, 110),
                Keys = new List<NodeKey>
                {
                    new NodeKey("ZeroDurationPolicy", "0时长策略", EditorKind.Enum, KeyTier.Common),
                    new NodeKey("EmptySnPolicy", "空SN策略", EditorKind.Enum, KeyTier.Common),
                }
            },
            new NodeDef
            {
                Id = "vacuum", Title = "抽真空",
                DefaultRect = new Rectangle(60, 185, 220, 130),
                Keys = new List<NodeKey>
                {
                    new NodeKey("SkipVacuum", "跳过抽真空", EditorKind.Bool, KeyTier.Common),
                    new NodeKey("VacuumConfirmTimeoutMs", "建立超时(ms)", EditorKind.Text, KeyTier.Common),
                    // 失联次数默认 3 次，几乎不动 → 高级（折叠后常用区只留两项）。
                    new NodeKey("CommunicationLossAlarmCount", "失联次数", EditorKind.Text, KeyTier.Advanced),
                }
            },
            new NodeDef
            {
                Id = "power", Title = "上电老化",
                DefaultRect = new Rectangle(60, 360, 220, 130),
                Keys = new List<NodeKey>
                {
                    new NodeKey("MaxTestDurationSeconds", "全局时长(秒)", EditorKind.Text, KeyTier.Common),
                    // 完成表达式/画面维度属于"特定项目才用"的工程项 → 高级折叠。
                    new NodeKey("CompleteExpression", "完成表达式", EditorKind.Text, KeyTier.Advanced),
                    new NodeKey("DisplayModeEnabled", "启用画面维度", EditorKind.Bool, KeyTier.Advanced),
                    new NodeKey("DisplayModes", "画面字典(逗号分隔)", EditorKind.Text, KeyTier.Advanced),
                }
            },
            new NodeDef
            {
                Id = "done", Title = "完成下电",
                DefaultRect = new Rectangle(60, 535, 220, 125),
                Keys = new List<NodeKey>
                {
                    new NodeKey("CompletionJudgePolicy", "完成判定", EditorKind.Enum, KeyTier.Common),
                    new NodeKey("CompletionAction", "完成动作", EditorKind.Enum, KeyTier.Common),
                    // 破空阀两项已迁系统设置：装没装、接哪个 DO 点是本机接线事实
                    //（与蜂鸣器点位同一口径），换台机器就变，不跟工艺走。
                },
                Info = "破空阀开关/点位在系统设置 → 老化测试业务。"
            },
            new NodeDef
            {
                Id = "alarm", Title = "报警联动",
                DefaultRect = new Rectangle(430, 340, 240, 150),
                Keys = new List<NodeKey>
                {
                    // —— 常用（默认展开）：压力保护 / 温度保护 ——
                    // 压力报警总开关是第一项：关了阈值/方向都不看（常开不报警），先翻它再调阈值。
                    new NodeKey("PressureAlarmEnabled", "压力报警开关", EditorKind.Bool, KeyTier.Common, "压力保护"),
                    new NodeKey("AlarmPressureThresholdKPa", "报警阈值(kPa)", EditorKind.Text, KeyTier.Common, "压力保护"),
                    // 超温相关收在温度保护组：上限/联停是安全关键，现场按炉温填。
                    new NodeKey("FanTempAlarmLimitC", "超温上限(°C)", EditorKind.Text, KeyTier.Common, "温度保护"),
                    new NodeKey("FanTempShutdownEnabled", "超温联停", EditorKind.Bool, KeyTier.Common, "温度保护"),

                    // —— 高级（默认折叠）：报警行为 / 失压与责任 / 通讯与规则 ——
                    // 报警全关是最高级静默（DI/失联/规则/超温联停也不报），误开等于关掉全部保护，
                    // 放高级并标红（HighRiskKeys），四个预置默认全关。
                    new NodeKey("MuteAllAlarms", "报警全关", EditorKind.Bool, KeyTier.Advanced, "报警行为"),
                    // 报警方向与阈值是一对，但 90% 现场不动（默认低于阈值报警）→ 高级。
                    new NodeKey("AlarmWhenPressureHigherThanThreshold", "报警方向(高于阈值)", EditorKind.Bool, KeyTier.Advanced, "报警行为"),
                    // 蜂鸣方式跟超温工艺走（持续/短鸣/断续），但一次配好基本不动 → 高级。
                    new NodeKey("FanBuzzerMode", "蜂鸣方式", EditorKind.Enum, KeyTier.Advanced, "报警行为"),
                    // 老化中失压/真空责任/风机断连：都是"出事怎么处理"的策略，调试期才频繁切换。
                    new NodeKey("AgingPressureLossPolicy", "老化失压", EditorKind.Enum, KeyTier.Advanced, "失压与责任"),
                    new NodeKey("VacuumFailKind", "真空责任", EditorKind.Enum, KeyTier.Advanced, "失压与责任"),
                    new NodeKey("FanDisconnectPolicy", "风机断连", EditorKind.Enum, KeyTier.Advanced, "失压与责任"),
                    // DI 触点与自定义规则属于通讯/规则层，现场一般由工程一次配好。
                    new NodeKey("UseDiAlarmContact", "DI触点并入", EditorKind.Bool, KeyTier.Advanced, "通讯与规则"),
                    new NodeKey("CustomAlarmRules", "自定义规则", EditorKind.Multiline, KeyTier.Advanced, "通讯与规则"),
                }
            },
            new NodeDef
            {
                Id = "recover", Title = "断电恢复",
                DefaultRect = new Rectangle(430, 30, 240, 105),
                Keys = new List<NodeKey>
                {
                    new NodeKey("PowerLossPolicy", "恢复策略", EditorKind.Enum, KeyTier.Common),
                }
            },
            // 下料判定：本节点只读（V2.0.5 起）。事件身份口径/报表列属于"产出追溯"，
            // 入口统一在系统设置 → 报表导出；完成判定口径仍在【完成下电】节点。
            new NodeDef
            {
                Id = "unload", Title = "下料判定",
                DefaultRect = new Rectangle(430, 545, 240, 115),
                Keys = new List<NodeKey>(),
                Info = "本节点只读。\r\n事件身份口径/报表列在系统设置 → 报表导出。\r\n完成判定口径在【完成下电】节点改。"
            },
            // MES上报：只留"启用上报"总闸（现场偶尔临时关上报）。
            // 触发器/字段映射/静态字段是对接工程配置，入口统一在系统设置 → MES 对接。
            // 无连线（上报正交于流程），画布右下角，绘制顺序最后。
            new NodeDef
            {
                Id = "mes", Title = "MES上报",
                DefaultRect = new Rectangle(430, 700, 240, 125),
                Keys = new List<NodeKey>
                {
                    new NodeKey("MesEnabled", "启用上报", EditorKind.Bool, KeyTier.Common),
                },
                Info = "上报触发器/字段映射/静态字段在系统设置 → MES 对接。"
            },
        };

        /// <summary>全部连线（固定拓扑）。</summary>
        public static readonly List<EdgeDef> Edges = new List<EdgeDef>
        {
            new EdgeDef("e_start_vacuum", "start", "vacuum", false),
            new EdgeDef("e_vacuum_power", "vacuum", "power", false),
            new EdgeDef("e_power_done", "power", "done", false),
            new EdgeDef("e_vacuum_alarm", "vacuum", "alarm", false),
            new EdgeDef("e_power_alarm", "power", "alarm", false),
            new EdgeDef("e_done_unload", "done", "unload", false),
            new EdgeDef("e_recover_vacuum", "recover", "vacuum", true),
            new EdgeDef("e_alarm_unload", "alarm", "unload", true),
        };

        /// <summary>
        /// 已迁出策略窗的 key → 新入口（"系统设置分类|中文名"）。
        /// 【为什么保留映射】这些 key 仍是 PolicyKeys（仍跟项目走、仍进 Policy.json），
        /// 只是编辑入口从策略窗移到系统设置：
        /// ①设置表保存时的"该项由哪个策略节点管"点名仍能找到去处（LocateKey 非 null），
        /// ②回归可锁"迁移项不在任何节点 Keys 里，但定位信息还在"，防以后又被加回策略窗。
        /// </summary>
        public static readonly Dictionary<string, string> MovedKeyTargets =
            new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "MesTriggers", "系统设置·MES 对接|上报触发器" },
            { "MesFieldMap", "系统设置·MES 对接|字段映射" },
            { "MesStaticFields", "系统设置·MES 对接|静态字段" },
            { "EventIdentityMode", "系统设置·报表导出|事件身份口径" },
            { "ReportColumns", "系统设置·报表导出|报表列" },
            { "VentValveEnabled", "系统设置·老化测试业务|本机装破空阀" },
            { "VentValveDoPoint", "系统设置·老化测试业务|破空阀点位" },
        };

        /// <summary>
        /// 高风险配置项（改错直接影响安全/整线）：右栏标红提示，不弹二次确认
        /// （弹窗留给保存时的组合校验 ValidatePolicyCombination，避免双弹）。
        /// </summary>
        public static readonly HashSet<string> HighRiskKeys =
            new HashSet<string>(StringComparer.Ordinal)
        {
            "MuteAllAlarms",          // 报警全关：DI/失联/规则/超温联停全静默
            "SkipVacuum",             // 跳过抽真空：真空保护全丢，必须机械固定
            "FanTempShutdownEnabled", // 超温联停：上限没填就开等于没配（保存会拦）
        };

        /// <summary>出厂缺省配置（只读，取默认值用；DeviceConfig 无参构造无副作用）。</summary>
        private static readonly DeviceConfig DefaultConfig = new DeviceConfig();

        /// <summary>
        /// 取某 key 的出厂默认值（存储口径字符串：布尔小写/枚举英文名/数字原文）。
        /// 【用途】右栏每项标"默认/已改"——现场一眼看出哪些是出厂值、哪些被本项目调过，
        /// 不用逐项猜"这个数要不要动"。
        /// 【为什么不手写默认值表】32 项各抄一份，DeviceConfig 改缺省值就分叉；
        /// 直接读 new DeviceConfig() 的属性，新增 key 自动生效。
        /// </summary>
        public static string GetDefaultString(string key)
        {
            return ReadConfigString(DefaultConfig, key);
        }

        /// <summary>
        /// 按指定配置对象读 key 的存储口径字符串（布尔小写 true/false、枚举英文名，与落盘一致）。
        /// 【唯一口】右栏"当前值/默认值"两端都走这里：口径只写一份，
        /// 别在窗体里再抄一份反射（改口径漏一处 = 徽标永远"已改"或永远"默认"）。
        /// </summary>
        public static string ReadConfigString(DeviceConfig cfg, string key)
        {
            if (cfg == null || string.IsNullOrEmpty(key)) return "";
            try
            {
                var prop = typeof(DeviceConfig).GetProperty(key);
                if (prop == null) return "";
                object v = prop.GetValue(cfg, null);
                if (v == null) return "";
                if (v is bool) return ((bool)v) ? "true" : "false";
                return v.ToString();
            }
            catch { return ""; }
        }

        /// <summary>按 id 找节点（找不到返回 null）。</summary>
        public static NodeDef FindNode(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var n in Nodes)
            {
                if (string.Equals(n.Id, id, StringComparison.Ordinal)) return n;
            }
            return null;
        }

        /// <summary>
        /// 按配置 key 反查它在哪个节点的哪一项（纯函数：设置表保存弹窗点名用）。
        /// 策略层是最高优先级：设置表里改了节点可配的项，保存后即进策略层，
        /// 弹窗点出"【节点】事项"让用户知道去哪看。
        /// 已迁出策略窗的项（MovedKeyTargets）返回"【系统设置·分类】事项"，
        /// 让弹窗仍能指出实际编辑入口；纯机器项（串口参数等）返回 null，保存不打扰。
        /// </summary>
        /// <returns>"【节点/系统设置分类】事项名"；无人管返回 null</returns>
        public static string LocateKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var n in Nodes)
            {
                if (n.Keys == null) continue;
                foreach (var k in n.Keys)
                {
                    if (string.Equals(k.Key, key, StringComparison.Ordinal))
                        return "【" + n.Title + "】" + k.Label;
                }
            }
            // 已迁出策略窗的项：仍给出去处（设置表保存弹窗可点名"去哪改"），
            // 但不冒充策略节点——返回值直接写系统设置分类。
            string moved;
            if (MovedKeyTargets.TryGetValue(key, out moved) && !string.IsNullOrEmpty(moved))
            {
                int bar = moved.IndexOf('|');
                if (bar > 0)
                {
                    return "【" + moved.Substring(0, bar) + "】" + moved.Substring(bar + 1);
                }
            }
            return null;
        }

        /// <summary>按 id 找连线（找不到返回 null）。</summary>
        public static EdgeDef FindEdge(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var e in Edges)
            {
                if (string.Equals(e.Id, id, StringComparison.Ordinal)) return e;
            }
            return null;
        }

        /// <summary>
        /// 节点副标题行（纯函数：标题下显示的真实配置值 + 实时台数，画布逐秒重画）。
        /// </summary>
        public static string[] BuildNodeLines(string nodeId, DeviceConfig config, FlowCounts counts)
        {
            if (config == null) return new string[0];
            switch (nodeId)
            {
                case "start":
                    return new string[]
                    {
                        "0时长：" + ShortEnum(config.ZeroDurationPolicy.ToString()),
                        "空SN：" + ShortEnum(config.EmptySnPolicy.ToString()),
                        $"空闲 {counts.Idle} 台"
                    };
                case "vacuum":
                    if (config.SkipVacuum)
                    {
                        return new string[]
                        {
                            "已跳过→直接上电",
                            "压力报警已豁免",
                            "⚠确认机械固定！"
                        };
                    }
                    // 超时 0=关闭：不限时等，不判建立超时（只靠老化失压报警+人工停止）；
                    // 显示带"0"与"关闭"两样：设置表存 0、下拉首项"0（关闭）"，三处对得上。
                    string timeoutLine = config.VacuumConfirmTimeoutMs <= 0
                        ? "超时 0ms（关闭）"
                        : $"超时 {config.VacuumConfirmTimeoutMs}ms";
                    return new string[]
                    {
                        timeoutLine,
                        $"失联 {config.CommunicationLossAlarmCount} 次",
                        $"抽真空 {counts.Vacuuming} 台"
                    };
                case "power":
                    string dur = config.MaxTestDurationSeconds <= 0
                        ? "时长：配方定（全局不限）"
                        : $"时长：配方定（全局{config.MaxTestDurationSeconds}s）";
                    string expr = string.IsNullOrWhiteSpace(config.CompleteExpression)
                        ? "完成：时长到"
                        : "完成：时长到 或 表达式";
                    // 画面维度进副标题：关了就是现状（不显示不校验），开了显示字典规模。
                    string dm = config.DisplayModeEnabled
                        ? $"画面：开({DisplayModeCountOf(config)}项)"
                        : "画面：关";
                    return new string[] { dur, expr, dm, $"老化 {counts.Aging} 台" };
                case "done":
                    // 破空阀状态进副标题：无阀是现状（点位行隐藏），有阀显示点位。
                    string vent = config.VentValveEnabled
                        ? (config.VentValveDoPoint > 0
                            ? $"破空阀：有(点{config.VentValveDoPoint})"
                            : "破空阀：有(未配点位)")
                        : "破空阀：无";
                    return new string[]
                    {
                        "判定：" + ShortEnum(config.CompletionJudgePolicy.ToString()),
                        "动作：" + ShortEnum(config.CompletionAction.ToString()),
                        vent,
                        $"已完成 {counts.Completed} 台"
                    };
                case "alarm":
                    // 阈值进副标题：报警联动改完阈值当场看得见，不用再去设置表核对。
                    // 总开关关闭时阈值/方向都不看（常开不报警），副标题直说关闭，不摆阈值误导。
                    // 全关时 DI/失联/规则/超温联停也不报，副标题直说全关（压住总开关）。
                    string thr = config.MuteAllAlarms
                        ? "阈值：全关（DI/失联/规则也不报）"
                        : (!config.PressureAlarmEnabled
                        ? "阈值：关闭（常开不报警）"
                        : "阈值：" + config.AlarmPressureThresholdKPa.ToString("0.##")
                        + "kPa" + (config.AlarmWhenPressureHigherThanThreshold ? "(高于报)" : "(低于报)"));
                    return new string[]
                    {
                        thr,
                        $"规则 {RuleCountOf(config)} 条",
                        "责任：" + ShortEnum(config.VacuumFailKind.ToString()),
                        $"故障 {counts.Fault} 台"
                    };
                case "recover":
                    return new string[]
                    {
                        "策略：" + ShortEnum(config.PowerLossPolicy.ToString()),
                        counts.Snapshot > 0 ? $"快照 {counts.Snapshot} 台待恢复" : "无待恢复快照"
                    };
                case "unload":
                    // 口径与报表规模进副标题：改完事件身份/报表列当场看得见。
                    string ident = "口径：" + ShortEnum(config.EventIdentityMode.ToString());
                    int repCount = ReportColumnCountOf(config);
                    string rep = repCount < 0 ? "报表：缺省" : $"报表：{repCount}列";
                    return new string[]
                    {
                        config.CompletionJudgePolicy == CompletionJudgePolicy.PendingReview
                            ? $"待判定 {counts.PendingJudge} 台" : "自动PASS（免判定）",
                        ident + " | " + rep,
                        "主界面【下料判定】录入"
                    };
                case "mes":
                    return new string[]
                    {
                        "开关：" + (config.MesEnabled ? "开" : "关"),
                        string.IsNullOrWhiteSpace(config.MesTriggers)
                            ? "触发：四个全报" : "触发：" + config.MesTriggers.Trim(),
                        $"映射 {FieldMapCountOf(config)} 组 + 静态 {StaticFieldCountOf(config)} 组"
                    };
                default:
                    return new string[0];
            }
        }

        /// <summary>连线条件标签（纯函数，只读展示）。</summary>
        public static string BuildEdgeLabel(string edgeId, DeviceConfig config)
        {
            if (config == null) return "";
            switch (edgeId)
            {
                case "e_start_vacuum": return "开阀";
                case "e_vacuum_power":
                    return config.SkipVacuum ? "跳过抽真空" : "压力到位 且 延时时间到";
                case "e_power_done":
                    return string.IsNullOrWhiteSpace(config.CompleteExpression)
                        ? "时长到" : "时长到 或 表达式";
                case "e_vacuum_alarm": return "超时 / 失压 / 失联 / 规则";
                case "e_power_alarm": return "失压 / 失联 / 规则 / 超温";
                case "e_done_unload":
                    return config.CompletionJudgePolicy == CompletionJudgePolicy.PendingReview
                        ? "待判定" : "待取料";
                case "e_recover_vacuum":
                    return config.PowerLossPolicy == PowerLossPolicy.ResumeRemaining
                        ? "续跑剩余" : "整台重测";
                case "e_alarm_unload": return "人工复位回空闲";
                default: return "";
            }
        }

        /// <summary>连线只读说明（右栏展示：条件是什么、在哪改）。</summary>
        public static string BuildEdgeInfo(string edgeId, DeviceConfig config)
        {
            EdgeDef e = FindEdge(edgeId);
            if (e == null) return "未知连线。";
            NodeDef from = FindNode(e.From);
            NodeDef to = FindNode(e.To);
            string fn = from != null ? from.Title : e.From;
            string tn = to != null ? to.Title : e.To;
            return $"【{fn}】→【{tn}】\r\n条件：{BuildEdgeLabel(edgeId, config)}\r\n\r\n" +
                "连线只读（拓扑固定）。\r\n改条件请点两端节点。";
        }

        private static int RuleCountOf(DeviceConfig config)
        {
            try
            {
                List<Services.RuleEngine.RuleDef> defs;
                List<string> errors;
                // 非严格=执行口径（运行时容错收的行也计数，与副标题"规则 N 条"一致）。
                Services.RuleEngine.ParseRuleList(config.CustomAlarmRules, out defs, out errors, false);
                return defs.Count;
            }
            catch { return 0; }
        }

        /// <summary>字段映射组数（脏组不计，画布只看"生效了几组"）。</summary>
        private static int FieldMapCountOf(DeviceConfig config)
        {
            try
            {
                Dictionary<string, string> map;
                List<string> errors;
                Services.MesMapping.ParseFieldMap(config.MesFieldMap, out map, out errors);
                return map.Count;
            }
            catch { return 0; }
        }

        /// <summary>
        /// 报表列数（下料节点副标题用：留空=-1 表示缺省预设，不硬编码列数，
        /// 预设变了副标题不用跟着改；配了返回实际解析出的列数，脏组按 0 计）。
        /// </summary>
        private static int ReportColumnCountOf(DeviceConfig config)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(config.ReportColumns)) return -1;
                List<Services.ReportColumns.Column> cols;
                List<string> errors;
                Services.ReportColumns.Parse(config.ReportColumns, out cols, out errors);
                return cols.Count;
            }
            catch { return 0; }
        }

        /// <summary>
        /// 画面字典项数（上电节点副标题用：留空=缺省预设，返回预设规模；
        /// 配了返回实际项数，脏输入按 0 计——保存时 ValidateValue 已拦，画布只看生效规模）。
        /// </summary>
        private static int DisplayModeCountOf(DeviceConfig config)
        {
            try
            {
                List<string> opts = Services.DisplayModeOptions.Resolve(config);
                return opts != null ? opts.Count : 0;
            }
            catch { return 0; }
        }

        /// <summary>静态字段组数（同上）。</summary>
        private static int StaticFieldCountOf(DeviceConfig config)
        {
            try
            {
                Dictionary<string, string> fields;
                List<string> errors;
                Services.MesMapping.ParseStaticFields(config.MesStaticFields, out fields, out errors);
                return fields.Count;
            }
            catch { return 0; }
        }

        private static string ShortEnum(string name)
        {
            // 枚举英文名转短中文（下拉里是全称，节点上只放两字，省地方）
            switch (name)
            {
                case "Warn": return "警告";
                case "Block": return "拦截";
                case "LogOnly": return "提示";
                case "BlockStart": return "阻断";
                case "ProductFail": return "产品";
                case "FixtureAlarm": return "装夹";
                case "AutoPass": return "自动";
                case "PendingReview": return "待判定";
                case "RestartFull": return "重测";
                case "ResumeRemaining": return "续跑";
                case "StopOnLoss": return "停机";
                case "KeepRunning": return "不停";
                case "PowerOffOnly": return "下电";
                case "PowerOffAndBeep": return "蜂鸣";
                case "PowerOffAndVent": return "泄压";
                case "PowerOffVentAndBeep": return "鸣+泄";
                case "RecordTime": return "现值";
                case "StartSnapshot": return "定格";
                default: return name;
            }
        }

        /// <summary>
        /// 节点布局存取（纯视图态：只存 X/Y，跟机器走 PolicyLayout.json；
        /// 缩放/平移不存盘；文件损坏/缺失回缺省布局）。
        /// 随窗体改名 FlowLayout.json→PolicyLayout.json（项目未上线，老文件直接弃用）。
        /// </summary>
        public static class LayoutStore
        {
            private const string FileName = "PolicyLayout.json";
            // V2（2026-09-13）：缺省布局从挤列（宽190/列距100/行距40）换成截图式宽松
            // 两列（宽220~240/列距150/行距45+）。版本号递进即迁移：V1 存的坐标全是
            // 按旧缺省摆的（没拖过的是复位存的旧缺省，拖过的也是相对旧缺省的），直接
            // 沿用会继续挤，所以 V1 文件一律丢弃、回新缺省（拖一次即存 V2，下次接着用）。
            private const int Version = 2;

            private class LayoutFile
            {
                public int version;
                public Dictionary<string, int[]> nodes;
            }

            private static string Path()
            {
                return System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, FileName);
            }

            /// <summary>读布局（id → 位置；读不到的 id 调用方用缺省）。</summary>
            public static Dictionary<string, Point> Load()
            {
                var result = new Dictionary<string, Point>();
                try
                {
                    string path = Path();
                    if (!File.Exists(path)) return result;
                    var file = JsonConvert.DeserializeObject<LayoutFile>(File.ReadAllText(path));
                    if (file == null || file.nodes == null) return result;
                    // 版本不一致按缺省（V1→V2 换宽松布局，老坐标继续用会挤；丢弃后拖一次即存 V2）
                    if (file.version != Version) return result;
                    foreach (var kv in file.nodes)
                    {
                        if (string.IsNullOrEmpty(kv.Key) || kv.Value == null || kv.Value.Length < 2)
                        {
                            continue;
                        }
                        // 坐标钳制（防手改文件把节点扔到姥姥家：±5000 内）
                        int x = Math.Max(-5000, Math.Min(5000, kv.Value[0]));
                        int y = Math.Max(-5000, Math.Min(5000, kv.Value[1]));
                        result[kv.Key] = new Point(x, y);
                    }
                }
                catch { /* 损坏按缺省，回头拖动即重建 */ }
                return result;
            }

            /// <summary>写布局（拖节点落定即调一次，7 个点写小文件，开销忽略不计）。</summary>
            public static void Save(Dictionary<string, Point> positions)
            {
                try
                {
                    var file = new LayoutFile
                    {
                        version = Version,
                        nodes = new Dictionary<string, int[]>()
                    };
                    if (positions != null)
                    {
                        foreach (var kv in positions)
                        {
                            file.nodes[kv.Key] = new int[] { kv.Value.X, kv.Value.Y };
                        }
                    }
                    // 原子写：写半截断电下次 Load 回缺省只丢一次拖动，不断追溯链。
                    AtomicFile.WriteAllText(Path(), JsonConvert.SerializeObject(file, Formatting.Indented));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[工艺策略] 布局保存失败: {ex.Message}");
                }
            }
        }
    }
}
