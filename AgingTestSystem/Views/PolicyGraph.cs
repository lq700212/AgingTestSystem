using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using AgingTestSystem.Models;
using Newtonsoft.Json;

namespace AgingTestSystem.Views
{
    /// <summary>
    /// 工艺策略图静态数据（【V1.70 新增为 FlowGraph，V1.73 随窗体改名】工艺策略窗的图真相：
    /// 拓扑画死，文本按配置生成）。
    ///
    /// 【节点】启动开阀 / 抽真空 / 上电老化 / 完成下电 / 报警联动 / 断电恢复 / 下料判定 / MES上报。
    /// 每个节点挂它真正能改的配置 key（右栏编辑器按此生成）——条件写在边上，
    /// 改条件的入口永远在端点节点里，连线本身只读（防对着线发呆）。
    /// MES上报是纯配置节点（无连线：上报是正交的，启动/完成/报警/下料四个事件都可能发，
    /// 画连线反而误导；挂触发器/字段映射/静态字段 3 个跟项目的 key）。
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

        /// <summary>节点可改的单个 key。</summary>
        public class NodeKey
        {
            public string Key;
            public string Label;
            public EditorKind Kind;
            public NodeKey(string key, string label, EditorKind kind)
            {
                Key = key;
                Label = label;
                Kind = kind;
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
        public static readonly List<NodeDef> Nodes = new List<NodeDef>
        {
            new NodeDef
            {
                Id = "start", Title = "启动开阀",
                DefaultRect = new Rectangle(60, 30, 220, 110),
                Keys = new List<NodeKey>
                {
                    new NodeKey("ZeroDurationPolicy", "0时长策略", EditorKind.Enum),
                    new NodeKey("EmptySnPolicy", "空SN策略", EditorKind.Enum),
                }
            },
            new NodeDef
            {
                Id = "vacuum", Title = "抽真空",
                DefaultRect = new Rectangle(60, 185, 220, 130),
                Keys = new List<NodeKey>
                {
                    new NodeKey("SkipVacuum", "跳过抽真空", EditorKind.Bool),
                    new NodeKey("VacuumConfirmTimeoutMs", "建立超时(ms)", EditorKind.Text),
                    new NodeKey("CommunicationLossAlarmCount", "失联次数", EditorKind.Text),
                }
            },
            new NodeDef
            {
                Id = "power", Title = "上电老化",
                DefaultRect = new Rectangle(60, 360, 220, 130),
                Keys = new List<NodeKey>
                {
                    new NodeKey("MaxTestDurationSeconds", "全局时长(秒)", EditorKind.Text),
                    new NodeKey("CompleteExpression", "完成表达式", EditorKind.Text),
                }
            },
            new NodeDef
            {
                Id = "done", Title = "完成下电",
                DefaultRect = new Rectangle(60, 535, 220, 125),
                Keys = new List<NodeKey>
                {
                    new NodeKey("CompletionJudgePolicy", "完成判定", EditorKind.Enum),
                    new NodeKey("CompletionAction", "完成动作", EditorKind.Enum),
                    new NodeKey("VentValveDoPoint", "破空阀点位", EditorKind.Text),
                }
            },
            new NodeDef
            {
                Id = "alarm", Title = "报警联动",
                DefaultRect = new Rectangle(430, 340, 240, 150),
                Keys = new List<NodeKey>
                {
                    new NodeKey("AgingPressureLossPolicy", "老化失压", EditorKind.Enum),
                    new NodeKey("VacuumFailKind", "真空责任", EditorKind.Enum),
                    new NodeKey("UseDiAlarmContact", "DI触点并入", EditorKind.Bool),
                    new NodeKey("FanTempShutdownEnabled", "超温联停", EditorKind.Bool),
                    new NodeKey("FanTempAlarmLimitC", "超温上限(°C)", EditorKind.Text),
                    new NodeKey("FanDisconnectPolicy", "风机断连", EditorKind.Enum),
                    new NodeKey("CustomAlarmRules", "自定义规则", EditorKind.Multiline),
                }
            },
            new NodeDef
            {
                Id = "recover", Title = "断电恢复",
                DefaultRect = new Rectangle(430, 30, 240, 105),
                Keys = new List<NodeKey>
                {
                    new NodeKey("PowerLossPolicy", "恢复策略", EditorKind.Enum),
                }
            },
            new NodeDef
            {
                Id = "unload", Title = "下料判定",
                DefaultRect = new Rectangle(430, 545, 240, 115),
                Info = "待判定模式下，主界面操作区【下料判定】按钮录 PASS/FAIL。\r\n判定口径在【完成下电】节点改。"
            },
            // 【V1.73】MES上报纯配置节点：触发器/字段映射/静态字段 3 个跟项目的 key
            // 全在这里改（连接类开关/地址跟机器，在系统设置 MES 对接分类里改）。
            // 无连线（上报正交于流程），画布右下角，绘制顺序最后。
            new NodeDef
            {
                Id = "mes", Title = "MES上报",
                DefaultRect = new Rectangle(430, 700, 240, 125),
                Keys = new List<NodeKey>
                {
                    new NodeKey("MesTriggers", "上报触发器", EditorKind.Text),
                    new NodeKey("MesFieldMap", "字段映射", EditorKind.Multiline),
                    new NodeKey("MesStaticFields", "静态字段", EditorKind.Multiline),
                }
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
                    return new string[]
                    {
                        $"超时 {config.VacuumConfirmTimeoutMs}ms",
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
                    return new string[] { dur, expr, $"老化 {counts.Aging} 台" };
                case "done":
                    return new string[]
                    {
                        "判定：" + ShortEnum(config.CompletionJudgePolicy.ToString()),
                        "动作：" + ShortEnum(config.CompletionAction.ToString()),
                        $"已完成 {counts.Completed} 台"
                    };
                case "alarm":
                    return new string[]
                    {
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
                    return new string[]
                    {
                        config.CompletionJudgePolicy == CompletionJudgePolicy.PendingReview
                            ? $"待判定 {counts.PendingJudge} 台" : "自动PASS（免判定）",
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
                    return config.SkipVacuum ? "跳过抽真空" : "压力到位 且 延时到";
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
                Services.RuleEngine.ParseRuleList(config.CustomAlarmRules, out defs, out errors);
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
                default: return name;
            }
        }

        /// <summary>
        /// 节点布局存取（【V1.70】纯视图态：只存 X/Y，跟机器走 PolicyLayout.json；
        /// 缩放/平移不存盘；文件损坏/缺失回缺省布局）。
        /// 【V1.73】随窗体改名 FlowLayout.json→PolicyLayout.json（项目未上线，老文件直接弃用）。
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
                    File.WriteAllText(Path(), JsonConvert.SerializeObject(file, Formatting.Indented));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[工艺策略] 布局保存失败: {ex.Message}");
                }
            }
        }
    }
}
