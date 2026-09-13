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

        private int _stationCount;
        private readonly Func<DateTime> _now;
        // 【大扫荡】UI 线程（启动/复位/停止）与采集线程并发调 ResetStation/Eval*/UpdateRules：
        // TrueSince 数组与 _rules/_completeExpr 引用用小锁保护（采集每轮×72台调，
        // 锁内只有数组读写+引用切换，无 IO，开销可忽略）。
        private readonly object _ruleLock = new object();
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
        ///
        /// 【`||` 与分隔符冲突处理】表达式里的 `||` 会被 Split('|') 切碎
        /// （如"低压报警 | pressure>5 || temp>80 | 5"切成 5 段）。
        /// 段数超 3 时枚举"名称/表达式/持续秒"切分点，首个"表达式合法+持续秒合法"
        /// 的组合即采用；都非法才报格式错（以前一律报格式错，`||` 功能实际死亡）。
        /// </summary>
        /// <param name="strictVars">true=未知变量当场拦（保存/校验/显示路径默认）；
        /// false=未知变量留到运行时（UpdateRules 容错手改文件用）</param>
        public static void ParseRuleList(string raw,
            out List<RuleDef> rules, out List<string> errors, bool strictVars = true)
        {
            rules = new List<RuleDef>();
            errors = new List<string>();
            if (string.IsNullOrWhiteSpace(raw)) return;   // 空=零规则
            string[] lines = raw.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = (lines[i] ?? "").Trim();
                if (line.Length == 0) continue;
                string name, exprText, sustainedText;
                if (!SplitRuleLine(line, strictVars, out name, out exprText, out sustainedText))
                {
                    errors.Add($"第{i + 1}行：应为“名称 | 表达式 | 持续秒(可省)”: {line}");
                    continue;
                }
                if (name.Length == 0)
                {
                    errors.Add($"第{i + 1}行：名称为空");
                    continue;
                }
                RuleExpr.RuleExpression expr;
                string exprErr;
                if (!RuleExpr.TryParse(exprText, out expr, out exprErr, strictVars))
                {
                    errors.Add($"第{i + 1}行“{name}”表达式错误：{exprErr}");
                    continue;
                }
                int sustained = 0;
                if (sustainedText.Length > 0 && (!int.TryParse(sustainedText, out sustained) || sustained < 0))
                {
                    errors.Add($"第{i + 1}行“{name}”持续秒非法（应为≥0整数）: {sustainedText}");
                    continue;
                }
                rules.Add(new RuleDef { Name = name, Expression = exprText, SustainedSecs = sustained });
            }
            if (rules.Count > MaxRules)
            {
                errors.Add($"规则太多（{rules.Count}条，上限{MaxRules}条），请删减");
            }
        }

        /// <summary>
        /// 切分单行规则（名称 | 表达式 | 持续秒），表达式可含 `||`。
        /// 2~3 段走老逻辑；更多段时枚举切分点，首个全合法组合胜出。
        /// </summary>
        private static bool SplitRuleLine(string line, bool strictVars,
            out string name, out string exprText, out string sustainedText)
        {
            name = null; exprText = null; sustainedText = null;
            string[] parts = line.Split('|');
            if (parts.Length < 2 || parts.Length > 30) return false;   // 太碎=乱写，直接错
            if (parts.Length <= 3)
            {
                name = (parts[0] ?? "").Trim();
                exprText = (parts[1] ?? "").Trim();
                sustainedText = parts.Length == 3 ? ((parts[2] ?? "").Trim()) : "";
                return exprText.Length > 0;
            }
            // 多段：枚举名称/表达式/持续秒边界（名称至少占首段，持续秒至少占尾段）。
            for (int i = 1; i <= parts.Length - 2; i++)
            {
                for (int j = i + 1; j <= parts.Length - 1; j++)
                {
                    string n = JoinParts(parts, 0, i).Trim();
                    string e = JoinParts(parts, i, j).Trim();
                    string s = JoinParts(parts, j, parts.Length).Trim();
                    if (n.Length == 0 || e.Length == 0) continue;
                    if (s.Length > 0)
                    {
                        int t;
                        if (!int.TryParse(s, out t) || t < 0) continue;
                    }
                    RuleExpr.RuleExpression tmp;
                    string tmpErr;
                    if (!RuleExpr.TryParse(e, out tmp, out tmpErr, strictVars)) continue;
                    name = n; exprText = e; sustainedText = s;
                    return true;
                }
            }
            return false;
        }

        private static string JoinParts(string[] parts, int from, int to)
        {
            var sb = new System.Text.StringBuilder();
            for (int k = from; k < to; k++)
            {
                if (k > from) sb.Append('|');
                sb.Append(parts[k]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 同步配置（DeviceManager 每采集轮调一次，开销=两次字符串比较）。
        /// 任一变化 → 重编 + 清全部持续计时 + 清完成表达式。
        /// </summary>
        public void UpdateRules(string rulesRaw, string completeRaw)
        {
            lock (_ruleLock)
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
                // 运行时容错：未知变量留到求值时按 false 处理（保存侧已用严格模式拦过，
                // 这里是手改 Policy.json 的兜底；错整份才全空，不停线）。
                ParseRuleList(rulesRaw, out defs, out errors, false);
                // 脏行跳过（保存时已拦，这里是手改文件/热改配置的兜底；错整份才全空）
                int n = Math.Min(defs.Count, MaxRules);
                for (int i = 0; i < n; i++)
                {
                    RuleExpr.RuleExpression expr;
                    string exprErr;
                    if (!RuleExpr.TryParse(defs[i].Expression, out expr, out exprErr, false)) continue;
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
                    if (RuleExpr.TryParse(completeRaw.Trim(), out ce, out ceErr, false))
                    {
                        _completeExpr = ce;
                    }
                    // 解析失败=禁用（保存时已拦；运行时错了按"没配"处理，不停线）
                }
            }
        }

        /// <summary>当前编译好的规则数（界面预览/用例断言用）。</summary>
        public int RuleCount { get { return _rules.Count; } }

        /// <summary>完成表达式是否生效（空=禁用，走内置时长）。</summary>
        public bool HasCompleteExpr { get { return _completeExpr != null; } }

        /// <summary>
        /// 按新工位数重建持续计时数组（【大扫荡】项目热更改工位数时调：
        /// 旧尺寸 TrueSince 继续用=新工位规则静默失效；旧计时作废清零）。
        /// </summary>
        public void Resize(int stationCount)
        {
            lock (_ruleLock)
            {
                _stationCount = Math.Max(1, stationCount);
                _cachedRulesRaw = null;   // 强制下轮重编（新尺寸 TrueSince）
                _cachedCompleteRaw = null;
                _rules = new List<CompiledRule>();
                _completeExpr = null;
                LastError = null;
            }
        }

        /// <summary>复位某台的持续计时（启动/复位时调，旧任务的计时不带到新任务）。</summary>
        public void ResetStation(int deviceId)
        {
            lock (_ruleLock)
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
            lock (_ruleLock)
            {
                // 【LastError 语义】每台每轮先清零：修好后旧错不残留，
                // 同一串错再次出现能重新记日志（以前成功不清零，旧错赖着污染去重）。
                LastError = null;
                int idx = deviceId - 1;
                if (idx < 0 || idx >= _stationCount) return null;
                if (!testing)
                {
                    ResetStationLocked(deviceId);
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
        }

        /// <summary>锁内复位（调用方已持 _ruleLock 时用，防重入开销）。</summary>
        private void ResetStationLocked(int deviceId)
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
        /// 评估完成表达式（OR 语义的一半：成立即"可完成"，调用方再与内置时长取或）。
        /// 未配置/求值错 → false（按内置时长走）。
        /// </summary>
        public bool EvalComplete(IDictionary<string, double> vars)
        {
            lock (_ruleLock)
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
}
