using System;
using System.Collections.Generic;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// MES 映射表解析（【V1.68 新增】二期纯函数：触发器 / 字段映射 / 静态字段）。
    ///
    /// 【为什么是纯函数】映射字符串是用户手填的，脏输入（中文标点/多余空格/未知字段）
    /// 必须在"解析层"就洗掉或报错，不能流到上报线程。SettingsForm 校验、MesReporter
    /// 组包、回归用例三方共用同一份 vocabulary，改 vocabulary 只改这里。
    ///
    /// 【分隔符兼容】中英文逗号/分号都能做分隔（用户可能手输中文标点，见
    /// DeviceConfig.ParseFanIpCandidates 同思路）。
    /// </summary>
    public static class MesMapping
    {
        /// <summary>触发器 vocabulary（事件英文名，大小写无所谓）</summary>
        public static readonly string[] TriggerVocabulary = new string[]
        {
            "Start", "Complete", "Alarm", "UnloadJudge"
        };

        /// <summary>
        /// 本站字段 vocabulary（上报 payload 里本站侧的键名，大小写无所谓）：
        /// time=事件时间 / lot=批号 / device=工位号 / event=事件类型 / sn=产品SN /
        /// recipe=配方名 / result=判定结果 / detail=详情 / pressure=压力kPa /
        /// temp=温度°C / duration=时长秒 / displayMode=显示模式 / project=项目名 /
        /// disposition=处置 / defectCode=不良代码
        /// </summary>
        public static readonly string[] FieldVocabulary = new string[]
        {
            "time", "lot", "device", "event", "sn", "recipe", "result", "detail",
            "pressure", "temp", "duration", "displayMode", "project",
            "disposition", "defectCode"
        };

        /// <summary>组分隔符（中英文分号/逗号/顿号/空格都认）</summary>
        private static readonly char[] GroupSeparators = { ';', '；', ',', '，', '、' };

        /// <summary>
        /// 解析触发器（逗号分隔，如 "Complete,Alarm"）。
        /// 空 = 全开（缺省四个全报）；未知触发器进 errors（调用方决定拦还是跳过——
        /// SettingsForm 拦，MesReporter 跳过，双保险）。
        /// </summary>
        /// <param name="raw">原始配置字符串</param>
        /// <param name="triggers">解析出的触发器（原大小写，去重保序）</param>
        /// <param name="errors">未知项描述（无则空）</param>
        public static void ParseTriggers(string raw,
            out List<string> triggers, out List<string> errors)
        {
            triggers = new List<string>();
            errors = new List<string>();
            if (string.IsNullOrWhiteSpace(raw)) return;   // 空=全开，调用方按空处理
            foreach (string item in raw.Split(GroupSeparators))
            {
                string t = (item ?? "").Trim();
                if (t.Length == 0) continue;
                bool known = false;
                foreach (string v in TriggerVocabulary)
                {
                    if (string.Equals(v, t, StringComparison.OrdinalIgnoreCase)) { known = true; break; }
                }
                if (!known)
                {
                    errors.Add("未知触发器: " + t + "（合法: " + string.Join("/", TriggerVocabulary) + "）");
                    continue;
                }
                bool dup = false;
                foreach (string x in triggers)
                {
                    if (string.Equals(x, t, StringComparison.OrdinalIgnoreCase)) { dup = true; break; }
                }
                if (!dup) triggers.Add(t);
            }
        }

        /// <summary>触发器是否命中（空配置=全开；大小写无所谓）。</summary>
        public static bool TriggerHit(string configured, string trigger)
        {
            List<string> list;
            List<string> errors;
            ParseTriggers(configured, out list, out errors);
            if (list.Count == 0 && errors.Count == 0) return true;  // 空=全开
            foreach (string t in list)
            {
                if (string.Equals(t, trigger, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>
        /// 解析字段映射（"MES名=本站名"，如 "eqId=device;lotNo=lot"）。
        /// - 本站名未知 → 进 errors（SettingsForm 拦；MesReporter 跳过该组）；
        /// - MES 名为空/含空格/= → 进 errors；
        /// - 同一 MES 名出现两次 → 后者覆盖前者（最后一次为准，记一条 errors 提醒）。
        /// </summary>
        /// <param name="raw">原始配置字符串</param>
        /// <param name="map">MES名 → 本站名（保序可用 Keys 顺序，Dictionary 本身不保证）</param>
        /// <param name="errors">非法组描述</param>
        public static void ParseFieldMap(string raw,
            out Dictionary<string, string> map, out List<string> errors)
        {
            map = new Dictionary<string, string>();
            errors = new List<string>();
            if (string.IsNullOrWhiteSpace(raw)) return;   // 空=直通
            foreach (string item in raw.Split(GroupSeparators))
            {
                string g = (item ?? "").Trim();
                if (g.Length == 0) continue;
                int eq = g.IndexOf('=');
                if (eq <= 0 || eq >= g.Length - 1)
                {
                    errors.Add("非法映射组（应为 MES名=本站名）: " + g);
                    continue;
                }
                string mes = g.Substring(0, eq).Trim();
                string ours = g.Substring(eq + 1).Trim();
                if (mes.Length == 0 || mes.Contains(" ") || mes.Contains("="))
                {
                    errors.Add("MES 字段名非法（含空格/= 或为空）: " + g);
                    continue;
                }
                bool known = false;
                foreach (string v in FieldVocabulary)
                {
                    if (string.Equals(v, ours, StringComparison.OrdinalIgnoreCase)) { known = true; break; }
                }
                if (!known)
                {
                    errors.Add("未知本站字段: " + ours + "（合法: " + string.Join("/", FieldVocabulary) + "）");
                    continue;
                }
                if (map.ContainsKey(mes))
                {
                    errors.Add("MES 字段重复，后者覆盖前者: " + mes);
                }
                map[mes] = ours;
            }
        }

        /// <summary>
        /// 解析静态字段（"键=值"，如 "line=L5;workshop=A3"）。
        /// 值是字面量（不用 vocabulary 校验）；键为空/含空格/= 进 errors。
        /// </summary>
        public static void ParseStaticFields(string raw,
            out Dictionary<string, string> fields, out List<string> errors)
        {
            fields = new Dictionary<string, string>();
            errors = new List<string>();
            if (string.IsNullOrWhiteSpace(raw)) return;
            foreach (string item in raw.Split(GroupSeparators))
            {
                string g = (item ?? "").Trim();
                if (g.Length == 0) continue;
                int eq = g.IndexOf('=');
                if (eq <= 0)
                {
                    errors.Add("非法静态组（应为 键=值）: " + g);
                    continue;
                }
                string k = g.Substring(0, eq).Trim();
                string v = (eq >= g.Length - 1) ? "" : g.Substring(eq + 1).Trim();
                if (k.Length == 0 || k.Contains(" ") || k.Contains("="))
                {
                    errors.Add("静态字段名非法（含空格/= 或为空）: " + g);
                    continue;
                }
                fields[k] = v;   // 重复键后者覆盖（静态常量，覆盖即改值，不报错）
            }
        }

        /// <summary>
        /// 解析自定义 HTTP 头（【V1.68 新增】"头名=头值"，如 "X-Line=L5;X-ApiVer=2"）。
        /// 头名规则比静态字段严一档：不许中文/空格（HTTP 头名必须是 token）。
        /// 头值是字面量（Base64 的 = 填充没关系，按第一个 = 切分）。
        /// </summary>
        public static void ParseCustomHeaders(string raw,
            out Dictionary<string, string> headers, out List<string> errors)
        {
            headers = new Dictionary<string, string>();
            errors = new List<string>();
            if (string.IsNullOrWhiteSpace(raw)) return;
            foreach (string item in raw.Split(GroupSeparators))
            {
                string g = (item ?? "").Trim();
                if (g.Length == 0) continue;
                int eq = g.IndexOf('=');
                if (eq <= 0)
                {
                    errors.Add("非法请求头组（应为 头名=头值）: " + g);
                    continue;
                }
                string k = g.Substring(0, eq).Trim();
                string v = (eq >= g.Length - 1) ? "" : g.Substring(eq + 1).Trim();
                if (!IsHeaderName(k))
                {
                    errors.Add("请求头名非法（只允许字母/数字/连横线/下划线）: " + g);
                    continue;
                }
                headers[k] = v;   // 重复头后者覆盖
            }
        }

        /// <summary>
        /// HTTP 头名合法性（token 子集，纯 ASCII：字母数字 + - _ .，首字符必须是字母）。
        /// 注意不能用 char.IsLetter——中文在 .NET 里也算 Letter，但 HTTP 头名只认 ASCII。
        /// </summary>
        private static bool IsHeaderName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (!IsAsciiLetter(name[0])) return false;
            foreach (char c in name)
            {
                if (IsAsciiLetter(c) || (c >= '0' && c <= '9') || c == '-' || c == '_' || c == '.') continue;
                return false;
            }
            return true;
        }

        private static bool IsAsciiLetter(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
        }

        /// <summary>
        /// 解析按事件分地址（【V1.68 新增】"触发器=URL"，如 "Alarm=http://x/api/alarm"）。
        /// 触发器必须命中 vocabulary（大小写无所谓，存规范大小写）；
        /// URL 必须 http:// 或 https:// 开头（防手滑把字段映射串进来）。
        /// 同一触发器配两次 → 后者覆盖前者（记一条提醒）。
        /// </summary>
        public static void ParseEndpointMap(string raw,
            out Dictionary<string, string> map, out List<string> errors)
        {
            map = new Dictionary<string, string>();
            errors = new List<string>();
            if (string.IsNullOrWhiteSpace(raw)) return;   // 空=全走默认地址
            foreach (string item in raw.Split(GroupSeparators))
            {
                string g = (item ?? "").Trim();
                if (g.Length == 0) continue;
                int eq = g.IndexOf('=');
                if (eq <= 0 || eq >= g.Length - 1)
                {
                    errors.Add("非法分地址组（应为 触发器=URL）: " + g);
                    continue;
                }
                string t = g.Substring(0, eq).Trim();
                string url = g.Substring(eq + 1).Trim();
                string canonical = null;
                foreach (string v in TriggerVocabulary)
                {
                    if (string.Equals(v, t, StringComparison.OrdinalIgnoreCase)) { canonical = v; break; }
                }
                if (canonical == null)
                {
                    errors.Add("未知触发器: " + t + "（合法: " + string.Join("/", TriggerVocabulary) + "）");
                    continue;
                }
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("URL 必须 http(s):// 开头: " + g);
                    continue;
                }
                if (map.ContainsKey(canonical))
                {
                    errors.Add("触发器重复，后者覆盖前者: " + canonical);
                }
                map[canonical] = url;
            }
        }

        /// <summary>
        /// 按事件解析出实际地址（【V1.68 新增】）：命中分地址用分地址，否则回默认地址。
        /// 脏组跳过（保存时已拦，这里是手改文件的兜底）。
        /// </summary>
        public static string ResolveEndpoint(string trigger, string defaultUrl, string endpointMapRaw)
        {
            Dictionary<string, string> map;
            List<string> errors;
            ParseEndpointMap(endpointMapRaw, out map, out errors);
            foreach (var kv in map)
            {
                if (string.Equals(kv.Key, trigger, StringComparison.OrdinalIgnoreCase))
                {
                    return kv.Value;
                }
            }
            return defaultUrl;
        }
    }
}
