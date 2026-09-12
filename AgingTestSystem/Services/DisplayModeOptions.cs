
using System;
using System.Collections.Generic;
using AgingTestSystem.Models;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 显示模式字典（【V1.74 新增】Q20 记录层可配：烧屏画面选项纯函数）。
    ///
    /// 【为什么是字典而不是自由文本】V1.66 起 DisplayMode 是自由文本框，
    /// 手滑写"白场 "（尾空格）/"白色"（别名）就会造出两条追溯口径，
    /// 后道按画面分组统计时对不上。字典把选项收敛到一份名单。
    ///
    /// 【V1.74】三窗输入框已换成下拉（UIComboBox + DropDownList，只能选字典，
    /// 手打进不来）；保存校验是第二道门（防老配方遗留值 + 代码回填绕过），
    /// 脏输入进不了配方文件。选项由各窗构造按字典代码填（R8a 禁 Designer 写 AddRange）。
    ///
    /// 【PG 控制】本字典只管"记录层"（配方存什么、日志带什么、MES 报什么）；
    /// 上位机不控 PG 切画面（没 PG 协议）。画面分段/视频走二期。
    /// </summary>
    public static class DisplayModeOptions
    {
        /// <summary>
        /// 缺省预设（烧屏常见画面：纯色场 + 灰阶 + 循环 + 棋盘格 + 视频）。
        /// 配置留空 = 用这套；客户在系统设置→工艺策略→显示模式字典里改。
        /// </summary>
        public const string DefaultPreset = "白场,红场,绿场,蓝场,灰阶,RGB循环,棋盘格,视频";

        /// <summary>单项上限（防手滑粘贴长文；中文画面名一般 ≤10 字）</summary>
        public const int MaxItemLength = 20;

        /// <summary>选项上限（防手滑粘贴整段文章）</summary>
        public const int MaxOptionCount = 20;

        /// <summary>分隔符（中英文逗号/分号/顿号都认，与映射类配置同口径）</summary>
        private static readonly char[] Separators = { ',', '，', ';', '；', '、' };

        /// <summary>
        /// 解析字典（逗号分隔，如 "白场,红场,绿场"）。
        /// - 空 = 缺省（返回空表零错误，调用方走 Resolve）；
        /// - 空组跳过；重复（大小写无所谓）后者记一条提醒，前者保留；
        /// - 单项超长/总数超限 → 进 errors（SettingsForm 拦；录入窗不受影响，
        ///   录入窗只读 Resolve 后的干净表）。
        /// </summary>
        public static void Parse(string raw, out List<string> options, out List<string> errors)
        {
            options = new List<string>();
            errors = new List<string>();
            if (string.IsNullOrWhiteSpace(raw)) return;   // 空=缺省
            foreach (string item in raw.Split(Separators))
            {
                string t = (item ?? "").Trim();
                if (t.Length == 0) continue;
                if (t.Length > MaxItemLength)
                {
                    errors.Add("选项超长（≤" + MaxItemLength + "字）: " + t);
                    continue;
                }
                bool dup = false;
                foreach (string x in options)
                {
                    if (string.Equals(x, t, StringComparison.OrdinalIgnoreCase)) { dup = true; break; }
                }
                if (dup)
                {
                    errors.Add("选项重复: " + t);
                    continue;
                }
                options.Add(t);
            }
            if (options.Count == 0 && errors.Count == 0)
            {
                // 配了但全是分隔符/空格 = 配了个寂寞，报出来（与"留空走预设"区分开）
                errors.Add("未解析出任何选项（留空=缺省预设；要自定义请填如 白场,红场）");
            }
            else if (options.Count > MaxOptionCount)
            {
                errors.Add("选项太多（≤" + MaxOptionCount + "个），已截断保留前 " + MaxOptionCount + " 个");
                options = options.GetRange(0, MaxOptionCount);
            }
        }

        /// <summary>
        /// 解出实际生效的字典（配置空/全错 → 缺省预设；部分错 → 对的照用）。
        /// 录入窗 tooltip 与校验都调这个，手改文件写错也不炸。
        /// </summary>
        /// <param name="config">内存配置（已叠加项目 Policy；可为 null，没有就读项目文件）</param>
        public static List<string> Resolve(DeviceConfig config)
        {
            List<string> options;
            List<string> errors;
            if (config != null)
            {
                Parse(config.DisplayModes, out options, out errors);
                if (options.Count > 0) return options;
                // 配置是空（缺省）→ 预设；配置非空但全错 → 也回预设（录入窗不能无选项）
                if (string.IsNullOrWhiteSpace(config.DisplayModes)) return Preset();
            }
            // 无配置对象（配方管理窗）→ 读当前项目文件；再没有 → 预设
            string rawProject = null;
            try { rawProject = ProjectPolicyStore.GetRaw("DisplayModes"); }
            catch { rawProject = null; }
            if (!string.IsNullOrWhiteSpace(rawProject))
            {
                Parse(rawProject, out options, out errors);
                if (options.Count > 0) return options;
            }
            return Preset();
        }

        /// <summary>
        /// 显示模式维度是否启用（【V1.75 新增】三窗显隐总开关，纯函数回归可单测）。
        /// false/空配置=隐藏（三窗不显示该行，配方存空串）；true=显示下拉+字典生效。
        /// </summary>
        public static bool ShouldShowDisplayMode(DeviceConfig config)
        {
            return config != null && config.DisplayModeEnabled;
        }

        /// <summary>缺省预设解析结果（常量解析必成功）。</summary>
        public static List<string> Preset()
        {
            List<string> options;
            List<string> errors;
            Parse(DefaultPreset, out options, out errors);
            return options;
        }

        /// <summary>
        /// 下拉框数据源（含历史遗留值兜底，【V1.74】三窗下拉共用）。
        /// 字典选项原序返回；遗留值（老配方文件里的字典外文本）追加在末尾，
        /// 保证回填可见、保存时由 ValidateInput 拦下整改——看得见才改得掉。
        /// 空/空白遗留值不追加（空=清空，合法状态）。
        /// </summary>
        /// <param name="options">生效字典（Resolve 结果）</param>
        /// <param name="legacyValue">待回填的旧值（可空）</param>
        public static List<string> WithLegacy(List<string> options, string legacyValue)
        {
            var result = new List<string>();
            if (options != null) result.AddRange(options);
            string t = (legacyValue ?? "").Trim();
            if (t.Length == 0) return result;
            foreach (string o in result)
            {
                if (string.Equals(o, t, StringComparison.OrdinalIgnoreCase)) return result;
            }
            result.Add(t);
            return result;
        }

        /// <summary>
        /// 校验录入值（【V1.74】三窗保存入口共用：空=允许（清空），字典内=存规范写法，
        /// 字典外=拦并报出全部选项——客户第一次填错即看到名单，不用来回问）。
        /// </summary>
        /// <param name="input">输入框原文</param>
        /// <param name="options">生效字典（Resolve 结果）</param>
        /// <param name="canonical">规范写法（通过时输出；空输入输出空串）</param>
        /// <param name="error">拦因（不通过时输出）</param>
        /// <returns>true=通过（空或字典内）</returns>
        public static bool ValidateInput(string input, List<string> options,
            out string canonical, out string error)
        {
            canonical = "";
            error = null;
            string t = (input ?? "").Trim();
            if (t.Length == 0) return true;   // 空=清空，允许
            if (options != null)
            {
                foreach (string o in options)
                {
                    if (string.Equals(o, t, StringComparison.OrdinalIgnoreCase))
                    {
                        canonical = o;   // 存字典规范写法（"白场 "→"白场"，别名不进文件）
                        return true;
                    }
                }
            }
            error = "显示模式不在字典中，可选：" + ((options != null && options.Count > 0)
                ? string.Join("/", options.ToArray())
                : string.Join("/", Preset().ToArray()))
                + "（字典在系统设置→工艺策略→显示模式字典里改）";
            return false;
        }
    }
}
