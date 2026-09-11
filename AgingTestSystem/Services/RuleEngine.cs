using System;
using System.Collections.Generic;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 规则执行器（【V1.69 新增】三期：自定义报警规则 + 完成表达式的运行时）。
    ///
    /// 【职责】
    /// - 编译缓存：配置字符串不变就不重编（每秒×72台调，不能每轮 parse）；
    ///   配置变了自动重编并清持续计时（规则都换了，旧计时作废）。
    /// - 持续计时：表达式持续成立满 sustainedSecs 才触发（防毛刺误报，如压力抖动）。
    /// - 完成表达式：OR 语义——内置时长"到"或表达式成立，任一即完成
    ///   （只能提前完成；对烧屏架少点亮比多点亮安全，见 DeviceManager 注释）。
    ///
    /// 【fail-safe】求值出错（除零/未知变量）→ 按 false 处理 + 记 LastError
    /// （DeviceManager 边沿记一次日志，不刷屏）；报警规则错了=不报（不误报），
    /// 完成表达式错了=不提前完成（按内置时长走，不停线）。
    ///
    /// 【时钟可注入】默认 DateTime.Now；回归用例注入假时钟测持续计时。
    /// </summary>
    public class RuleEngine
    {
        /// <summary>规则条数上限（防配 200 条把采集循环拖死；超了保存时拦，这里是第二道）</summary>
        public const int MaxRules = 20;

        /// <summary>单条规则定义（解析层产物，见 ParseRuleList）。</summary>
        public class RuleDef
        {
            public string Name;
            public string Expression;
            public int SustainedSecs;
        }

        private class CompiledRule
        {
            public string Name;
            public RuleExpr.RuleExpression Expr;
            public int SustainedSecs;
            public DateTime[] TrueSince;   // 每台"持续成立"的起点（MinValue=当前不成立）
        }

        private readonly int _stationCount;
        private readonly Func<DateTime> _now;
        private string _cachedRulesRaw;
        private string _cachedCompleteRaw;
        private List<CompiledRule> _rules = new List<CompiledRule>();
        private RuleExpr.RuleExpression _completeExpr;

        /// <summary>最近一次求值错误（DeviceManager 边沿日志用；无错为 null）。</summary>
        public string LastError { get; private set; }

        public RuleEngine(int stationCount, Func<DateTime> nowProvider = null)
        {
            _stationCount = Math.Max(1, stationCount);
            _now = nowProvider ?? (() => DateTime.Now);
        }

        /// <summary>
        /// 解析规则表（一行一条："名称 | 表达式 | 持续秒"，持续秒可省默认 0=立即）。
        /// 空行跳过；行号从 1 起，错误带行号（设置界面直接展示）。
        /// </summary>
        public static void ParseRuleList(string raw,
            out List<RuleDef> rules, out List<string> errors)
        {
            rules = new List<RuleDef>();
            errors = new List<string>();
            if (string.IsNullOrWhiteSpace(raw)) return;   // 空=零规则
            string[] lines = raw.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = (lines[i] ?? "").Trim();
                if (line.Length == 0) continue;
                string[] parts = line.Split('|');
                if (parts.Length < 2 || parts.Length > 3)
                {
                    errors.Add($"第{i + 1}行：应为“名称 | 表达式 | 持续秒(可省)”: {line}");
                    continue;
                }
                string name = (parts[0] ?? "").Trim();
                string exprText = (parts[1] ?? "").Trim();
                if (name.Length == 0)
                {
                    errors.Add($"第{i + 1}行：名称为空");
                    continue;
                }
                RuleExpr.RuleExpression expr;
                string exprErr;
                if (!RuleExpr.TryParse(exprText, out expr, out exprErr))
                {
                    errors.Add($"第{i + 1}行“{name}”表达式错误：{exprErr}");
                    continue;
                }
                int sustained = 0;
                if (parts.Length == 3)
                {
                    string s = (parts[2] ?? "").Trim();
                    if (s.Length > 0 && (!int.TryParse(s, out sustained) || sustained < 0))
                    {
                        errors.Add($"第{i + 1}行“{name}”持续秒非法（应为≥0整数）: {s}");
                        continue;
                    }
                }
                rules.Add(new RuleDef { Name = name, Expression = exprText, SustainedSecs = sustained });
            }
            if (rules.Count > MaxRules)
            {
                errors.Add($"规则太多（{rules.Count}条，上限{MaxRules}条），请删减");
            }
        }

        /// <summary>
        /// 同步配置（DeviceManager 每采集轮调一次，开销=两次字符串比较）。
        /// 任一变化 → 重编 + 清全部持续计时 + 清完成表达式。
        /// </summary>
        public void UpdateRules(string rulesRaw, string completeRaw)
        {
            if (string.Equals(_cachedRulesRaw, rulesRaw, StringComparison.Ordinal)
                && string.Equals(_cachedCompleteRaw, completeRaw, StringComparison.Ordinal))
            {
                return;
            }
            _cachedRulesRaw = rulesRaw;
            _cachedCompleteRaw = completeRaw;
            LastError = null;

            _rules = new List<CompiledRule>();
            List<RuleDef> defs;
            List<string> errors;
            ParseRuleList(rulesRaw, out defs, out errors);
            // 脏行跳过（保存时已拦，这里是手改文件/热改配置的兜底；错整份才全空）
            int n = Math.Min(defs.Count, MaxRules);
            for (int i = 0; i < n; i++)
            {
                RuleExpr.RuleExpression expr;
                string exprErr;
                if (!RuleExpr.TryParse(defs[i].Expression, out expr, out exprErr)) continue;
                _rules.Add(new CompiledRule
                {
                    Name = defs[i].Name,
                    Expr = expr,
                    SustainedSecs = defs[i].SustainedSecs,
                    TrueSince = new DateTime[_stationCount]
                });
            }

            _completeExpr = null;
            if (!string.IsNullOrWhiteSpace(completeRaw))
            {
                RuleExpr.RuleExpression ce;
                string ceErr;
                if (RuleExpr.TryParse(completeRaw.Trim(), out ce, out ceErr))
                {
                    _completeExpr = ce;
                }
                // 解析失败=禁用（保存时已拦；运行时错了按"没配"处理，不停线）
            }
        }

        /// <summary>当前编译好的规则数（界面预览/用例断言用）。</summary>
        public int RuleCount { get { return _rules.Count; } }

        /// <summary>完成表达式是否生效（空=禁用，走内置时长）。</summary>
        public bool HasCompleteExpr { get { return _completeExpr != null; } }

        /// <summary>复位某台的持续计时（启动/复位时调，旧任务的计时不带到新任务）。</summary>
        public void ResetStation(int deviceId)
        {
            int idx = deviceId - 1;
            if (idx < 0 || idx >= _stationCount) return;
            foreach (var r in _rules)
            {
                if (r.TrueSince != null && idx < r.TrueSince.Length)
                {
                    r.TrueSince[idx] = DateTime.MinValue;
                }
            }
        }

        /// <summary>
        /// 评估自定义报警（每台每轮调；调用方保证只对测试中的台调）。
        /// </summary>
        /// <param name="deviceId">工位号（1起）</param>
        /// <param name="vars">变量表（DeviceManager 组装，键大小写无所谓）</param>
        /// <param name="testing">是否在测；false=复位计时并返回 null</param>
        /// <returns>触发的规则名；null=无触发</returns>
        public string EvalAlarms(int deviceId, IDictionary<string, double> vars, bool testing)
        {
            int idx = deviceId - 1;
            if (idx < 0 || idx >= _stationCount) return null;
            if (!testing)
            {
                ResetStation(deviceId);
                return null;
            }
            DateTime now = _now();
            foreach (var r in _rules)
            {
                string err;
                bool hit = RuleExpr.TryEvalBool(r.Expr, vars, out err);
                if (err != null)
                {
                    // 求值错=按 false + 复位计时（不误报；错误留给调用方边沿日志）
                    LastError = $"规则“{r.Name}”求值失败：{err}";
                    r.TrueSince[idx] = DateTime.MinValue;
                    continue;
                }
                if (!hit)
                {
                    r.TrueSince[idx] = DateTime.MinValue;
                    continue;
                }
                if (r.TrueSince[idx] == DateTime.MinValue)
                {
                    r.TrueSince[idx] = now;
                }
                if ((now - r.TrueSince[idx]).TotalSeconds >= r.SustainedSecs)
                {
                    return r.Name;   // 持续成立满 → 触发（调用方走报警联动）
                }
            }
            return null;
        }

        /// <summary>
        /// 评估完成表达式（OR 语义的一半：成立即"可完成"，调用方再与内置时长取或）。
        /// 未配置/求值错 → false（按内置时长走）。
        /// </summary>
        public bool EvalComplete(IDictionary<string, double> vars)
        {
            if (_completeExpr == null) return false;
            string err;
            bool hit = RuleExpr.TryEvalBool(_completeExpr, vars, out err);
            if (err != null)
            {
                LastError = "完成表达式求值失败：" + err;
                return false;
            }
            return hit;
        }
    }
}
