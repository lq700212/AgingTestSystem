using System;
using System.Collections.Generic;
using System.Globalization;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 规则表达式引擎（【V1.69 新增】三期：手写递归下降，沙盒求值，无副作用）。
    ///
    /// 【为什么手写而不引入脚本引擎】
    /// 现场配的表达式跑在生产循环里（每秒×72台），IronPython/Lua 等外部引擎是
    /// "代码执行"，配错可能卡死/爆内存；手写解析器只认下面的白名单，语法错在
    /// 保存时就拦，运行时最多返回 error、绝不抛异常拖垮采集。
    ///
    /// 【语言】（冻结，只做加法不做改法）
    /// - 字面量：数字（1 / 2.5 / .5）、true / false（大小写无所谓）；
    /// - 变量（大小写无所谓，见 Vocabulary）：pressure / temp / tempset / hum /
    ///   device / delaysecs / vacsecs / agesecs / duration / threshold / di0 / hour /
    ///   current（V1.74：本工位载台电流A，无表=NaN）；
    /// - 运算符（优先级从低到高）：||  &&  == !=  > < >= <=  + -  * / %  !（非） -（负号）；
    /// - 括号改变优先级。单 & 单 | 非法（防把位运算错当逻辑运算）。
    ///
    /// 【求值语义】全 double；比较返回 1/0；&& || 短路（右分支除零会被跳过，
    /// 如 x != 0 && 10/x > 2 是安全的）；除零/模零/未知变量 → error。
    /// 【NaN 语义】传感器不可用时变量取 NaN：NaN 参与任何比较/相等一律 false
    /// （死传感器不能触发规则，这是 fail-safe，不是 IEEE 标准，特此说明）；
    /// && || 把 NaN 当 false 看。
    /// 最终判定：结果 != 0 且非 NaN 即 true。
    /// </summary>
    public static class RuleExpr
    {
        /// <summary>
        /// 变量 vocabulary（冻结 13 个：加变量要同步改 DeviceManager 组变量、
        /// 类注释、SettingsForm 说明、回归用例，四处）。
        /// pressure=kPa实时压力 / temp=风机当前温度°C（离线=NaN）/
        /// tempset=温度设定值°C（离线=NaN）/ hum=湿度%RH（离线=NaN）/
        /// device=工位号 / delaysecs=延时开启定格秒 / vacsecs=距开阀秒 /
        /// agesecs=距上电秒（未上电=0）/ duration=定格时长秒 / threshold=定格阈值kPa /
        /// di0=DI触点0/1 / hour=当前小时0-23 /
        /// current=本工位载台电流A（【V1.74】无表=NaN，不参与判定只追溯）
        /// </summary>
        public static readonly string[] Vocabulary = new string[]
        {
            "pressure", "temp", "tempset", "hum", "device", "delaysecs",
            "vacsecs", "agesecs", "duration", "threshold", "di0", "hour",
            "current"
        };

        /// <summary>编译后的表达式（不透明句柄：只能由 TryParse 产生，只能由 TryEval 求值）。</summary>
        public sealed class RuleExpression
        {
            internal Node Root;
            internal RuleExpression(Node root) { Root = root; }
        }

        internal abstract class Node
        {
            public abstract double Eval(Dictionary<string, double> vars, ref string error);
        }

        internal sealed class ConstNode : Node
        {
            private readonly double _v;
            public ConstNode(double v) { _v = v; }
            public override double Eval(Dictionary<string, double> vars, ref string error) { return _v; }
        }

        internal sealed class VarNode : Node
        {
            private readonly string _name;
            public VarNode(string name) { _name = name; }
            public override double Eval(Dictionary<string, double> vars, ref string error)
            {
                double v;
                if (!vars.TryGetValue(_name, out v))
                {
                    error = "未知变量: " + _name;
                    return 0;
                }
                return v;
            }
        }

        internal sealed class UnaryNode : Node
        {
            private readonly char _op;
            private readonly Node _e;
            public UnaryNode(char op, Node e) { _op = op; _e = e; }
            public override double Eval(Dictionary<string, double> vars, ref string error)
            {
                double v = _e.Eval(vars, ref error);
                if (error != null) return 0;
                if (_op == '!') return ToBool(v) ? 0.0 : 1.0;
                return -v;   // 负号：NaN 传导
            }
        }

        internal sealed class BinaryNode : Node
        {
            private readonly string _op;
            private readonly Node _l;
            private readonly Node _r;
            public BinaryNode(string op, Node l, Node r) { _op = op; _l = l; _r = r; }
            public override double Eval(Dictionary<string, double> vars, ref string error)
            {
                // && || 短路：左值已能定结果时不碰右值（右分支除零会被跳过）
                if (_op == "&&")
                {
                    double a = _l.Eval(vars, ref error);
                    if (error != null) return 0;
                    if (!ToBool(a)) return 0.0;
                    double b = _r.Eval(vars, ref error);
                    if (error != null) return 0;
                    return ToBool(b) ? 1.0 : 0.0;
                }
                if (_op == "||")
                {
                    double a = _l.Eval(vars, ref error);
                    if (error != null) return 0;
                    if (ToBool(a)) return 1.0;
                    double b = _r.Eval(vars, ref error);
                    if (error != null) return 0;
                    return ToBool(b) ? 1.0 : 0.0;
                }
                double l = _l.Eval(vars, ref error);
                if (error != null) return 0;
                double r = _r.Eval(vars, ref error);
                if (error != null) return 0;
                switch (_op)
                {
                    case "+": return l + r;
                    case "-": return l - r;
                    case "*": return l * r;
                    case "/":
                        if (r == 0.0) { error = "除数为零"; return 0; }
                        return l / r;
                    case "%":
                        if (r == 0.0) { error = "模数为零"; return 0; }
                        return l % r;
                    case ">": return (IsNan(l) || IsNan(r)) ? 0.0 : (l > r ? 1.0 : 0.0);
                    case "<": return (IsNan(l) || IsNan(r)) ? 0.0 : (l < r ? 1.0 : 0.0);
                    case ">=": return (IsNan(l) || IsNan(r)) ? 0.0 : (l >= r ? 1.0 : 0.0);
                    case "<=": return (IsNan(l) || IsNan(r)) ? 0.0 : (l <= r ? 1.0 : 0.0);
                    case "==": return (IsNan(l) || IsNan(r)) ? 0.0 : (l == r ? 1.0 : 0.0);
                    case "!=": return (IsNan(l) || IsNan(r)) ? 0.0 : (l != r ? 1.0 : 0.0);
                    default: error = "未知运算符: " + _op; return 0;
                }
            }
        }

        private static bool IsNan(double v) { return double.IsNaN(v); }

        /// <summary>真值：非零且非 NaN。</summary>
        public static bool ToBool(double v) { return v != 0.0 && !double.IsNaN(v); }

        /// <summary>
        /// 解析表达式（空/空白 → error"表达式为空"，空=禁用由调用方判断，这里只管语法）。
        /// </summary>
        /// <param name="text">表达式文本</param>
        /// <summary>单条表达式长度上限（防手贴超长文本卡死解析；正常规则几十字符）。</summary>
        public const int MaxExprLength = 2000;

        /// <summary>括号嵌套深度上限（防 5000 层括号 StackOverflow 杀进程，不可捕获）。</summary>
        public const int MaxNestingDepth = 32;

        /// <param name="expr">编译结果（失败为 null）</param>
        /// <param name="error">错误描述（带字符位置；成功为 null）</param>
        /// <param name="strictVars">true=未知变量直接报错（保存/校验路径用，死规则当场拦）；
        /// false=未知变量留到求值时报错（运行时容错：手改文件不炸整份）</param>
        public static bool TryParse(string text, out RuleExpression expr, out string error,
            bool strictVars = false)
        {
            expr = null;
            error = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                error = "表达式为空";
                return false;
            }
            if (text.Length > MaxExprLength)
            {
                error = "表达式过长（" + text.Length + "字符，上限" + MaxExprLength + "字符）";
                return false;
            }
            var p = new Parser(text, strictVars);
            Node root = p.ParseOr(ref error);
            if (error != null) return false;
            p.SkipSpaces();
            if (!p.AtEnd())
            {
                error = "第" + (p.Pos + 1) + "字符有多余内容: " + p.RestPreview();
                return false;
            }
            expr = new RuleExpression(root);
            return true;
        }

        /// <summary>
        /// 求值（变量名大小写无所谓；缺变量/除零 → false + error，不抛异常）。
        /// </summary>
        public static bool TryEval(RuleExpression expr, IDictionary<string, double> vars,
            out double value, out string error)
        {
            value = 0;
            error = null;
            if (expr == null || expr.Root == null)
            {
                error = "表达式未编译";
                return false;
            }
            // 大小写不敏感拷贝（变量就 13 个，拷贝开销忽略不计）
            var lookup = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            if (vars != null)
            {
                foreach (var kv in vars) lookup[kv.Key] = kv.Value;
            }
            string err = null;
            value = expr.Root.Eval(lookup, ref err);
            if (err != null)
            {
                error = err;
                return false;
            }
            return true;
        }

        /// <summary>求布尔值（求值失败 → false + error）。</summary>
        public static bool TryEvalBool(RuleExpression expr, IDictionary<string, double> vars, out string error)
        {
            double v;
            if (!TryEval(expr, vars, out v, out error)) return false;
            return ToBool(v);
        }

        /// <summary>递归下降解析器（单遍字符级，无 token 表，代码短、位置准）。</summary>
        private sealed class Parser
        {
            private readonly string _s;
            private readonly bool _strictVars;
            private int _depth;
            public int Pos;
            public Parser(string s, bool strictVars) { _s = s; _strictVars = strictVars; Pos = 0; }
            public bool AtEnd() { return Pos >= _s.Length; }
            public string RestPreview()
            {
                string rest = _s.Substring(Pos).Trim();
                return rest.Length > 12 ? rest.Substring(0, 12) + "…" : rest;
            }
            public void SkipSpaces()
            {
                while (Pos < _s.Length && char.IsWhiteSpace(_s[Pos])) Pos++;
            }
            private bool Match(string op)
            {
                SkipSpaces();
                if (_s.Length - Pos >= op.Length && _s.Substring(Pos, op.Length) == op)
                {
                    Pos += op.Length;
                    return true;
                }
                return false;
            }
            private char Peek()
            {
                SkipSpaces();
                return Pos < _s.Length ? _s[Pos] : '\0';
            }

            public Node ParseOr(ref string error)
            {
                if (++_depth > MaxNestingDepth)
                {
                    error = "括号嵌套过深（上限" + MaxNestingDepth + "层）";
                    return null;
                }
                try
                {
                    Node l = ParseAnd(ref error);
                    if (error != null) return null;
                    while (true)
                    {
                        if (Match("||")) { Node r = ParseAnd(ref error); if (error != null) return null; l = new BinaryNode("||", l, r); }
                        else if (Peek() == '|') { error = "第" + (Pos + 1) + "字符：单 | 非法，请用 ||"; return null; }
                        else break;
                    }
                    return l;
                }
                finally { _depth--; }
            }

            private Node ParseAnd(ref string error)
            {
                Node l = ParseEquality(ref error);
                if (error != null) return null;
                while (true)
                {
                    if (Match("&&")) { Node r = ParseEquality(ref error); if (error != null) return null; l = new BinaryNode("&&", l, r); }
                    else if (Peek() == '&') { error = "第" + (Pos + 1) + "字符：单 & 非法，请用 &&"; return null; }
                    else break;
                }
                return l;
            }

            private Node ParseEquality(ref string error)
            {
                Node l = ParseComparison(ref error);
                if (error != null) return null;
                while (true)
                {
                    if (Match("==")) { Node r = ParseComparison(ref error); if (error != null) return null; l = new BinaryNode("==", l, r); }
                    else if (Match("!=")) { Node r = ParseComparison(ref error); if (error != null) return null; l = new BinaryNode("!=", l, r); }
                    else break;
                }
                return l;
            }

            private Node ParseComparison(ref string error)
            {
                Node l = ParseAdd(ref error);
                if (error != null) return null;
                while (true)
                {
                    if (Match(">=")) { Node r = ParseAdd(ref error); if (error != null) return null; l = new BinaryNode(">=", l, r); }
                    else if (Match("<=")) { Node r = ParseAdd(ref error); if (error != null) return null; l = new BinaryNode("<=", l, r); }
                    else if (Match(">")) { Node r = ParseAdd(ref error); if (error != null) return null; l = new BinaryNode(">", l, r); }
                    else if (Match("<")) { Node r = ParseAdd(ref error); if (error != null) return null; l = new BinaryNode("<", l, r); }
                    else break;
                }
                return l;
            }

            private Node ParseAdd(ref string error)
            {
                Node l = ParseMul(ref error);
                if (error != null) return null;
                while (true)
                {
                    if (Match("+")) { Node r = ParseMul(ref error); if (error != null) return null; l = new BinaryNode("+", l, r); }
                    else if (Match("-")) { Node r = ParseMul(ref error); if (error != null) return null; l = new BinaryNode("-", l, r); }
                    else break;
                }
                return l;
            }

            private Node ParseMul(ref string error)
            {
                Node l = ParseUnary(ref error);
                if (error != null) return null;
                while (true)
                {
                    if (Match("*")) { Node r = ParseUnary(ref error); if (error != null) return null; l = new BinaryNode("*", l, r); }
                    else if (Match("/")) { Node r = ParseUnary(ref error); if (error != null) return null; l = new BinaryNode("/", l, r); }
                    else if (Match("%")) { Node r = ParseUnary(ref error); if (error != null) return null; l = new BinaryNode("%", l, r); }
                    else break;
                }
                return l;
            }

            private Node ParseUnary(ref string error)
            {
                if (Match("!")) { Node e = ParseUnary(ref error); if (error != null) return null; return new UnaryNode('!', e); }
                if (Match("-")) { Node e = ParseUnary(ref error); if (error != null) return null; return new UnaryNode('-', e); }
                // 注意：单目 + 不支持（"+5" 报错，逼着写干净；减号够用了）
                return ParsePrimary(ref error);
            }

            private Node ParsePrimary(ref string error)
            {
                SkipSpaces();
                if (Pos >= _s.Length)
                {
                    error = "表达式意外结束（缺操作数）";
                    return null;
                }
                char c = _s[Pos];
                if (c == '(')
                {
                    Pos++;
                    Node e = ParseOr(ref error);
                    if (error != null) return null;
                    SkipSpaces();
                    if (Pos >= _s.Length || _s[Pos] != ')')
                    {
                        error = "第" + (Pos + 1) + "字符：括号未闭合";
                        return null;
                    }
                    Pos++;
                    return e;
                }
                if (char.IsDigit(c) || c == '.')
                {
                    return ParseNumber(ref error);
                }
                if (char.IsLetter(c) || c == '_')
                {
                    int start = Pos;
                    while (Pos < _s.Length && (char.IsLetterOrDigit(_s[Pos]) || _s[Pos] == '_')) Pos++;
                    string word = _s.Substring(start, Pos - start);
                    if (string.Equals(word, "true", StringComparison.OrdinalIgnoreCase))
                        return new ConstNode(1.0);
                    if (string.Equals(word, "false", StringComparison.OrdinalIgnoreCase))
                        return new ConstNode(0.0);
                    // 【严格模式】未知变量保存时即拦（死规则：过了保存、运行时恒 false，
                    // 用户以为布防成功）；非严格模式留到求值时报错（运行时容错）。
                    if (_strictVars && Array.IndexOf(Vocabulary, word.ToLowerInvariant()) < 0)
                    {
                        error = "未知变量: " + word + "（可用: "
                            + string.Join("/", Vocabulary) + ")";
                        return null;
                    }
                    return new VarNode(word);   // 非严格：未知变量在求值时报错（保留未来加变量的余地）
                }
                error = "第" + (Pos + 1) + "字符无法识别: " + c;
                return null;
            }

            private Node ParseNumber(ref string error)
            {
                int start = Pos;
                bool dot = false;
                while (Pos < _s.Length && (char.IsDigit(_s[Pos]) || _s[Pos] == '.'))
                {
                    if (_s[Pos] == '.')
                    {
                        if (dot) break;
                        dot = true;
                    }
                    Pos++;
                }
                string num = _s.Substring(start, Pos - start);
                double v;
                if (!double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                {
                    error = "第" + (start + 1) + "字符不是合法数字: " + num;
                    return null;
                }
                return new ConstNode(v);
            }
        }
    }
}
