// ============================================================================
//  ObfuscationAcceptance —— 混淆包验收 harness（V1.88.10 起，混淆发版流程固化）。
//
//  【为什么要有这个文件】
//  .NET 混淆（重命名）最怕把"字符串反射找名字"的地方改坏：本项目的策略页/设置表/
//  主题切换/配方联想全是 GetProperty/GetMethod/GetField 传字面量，改名即静默失效，
//  而且冒烟（进程活着）根本测不出来——窗体照样打开，只是下拉/保存 quietly 不工作。
//  这个 harness 专门验收"混淆后功能没被改坏"，跑在"混淆包拷贝＋本文件编译出的小跑器"上，
//  不碰源码目录，不污染仓库与 bin。
//
//  【怎么跑】
//  不直接跑。用 skill 的 scripts\obfuscated_release.ps1 一键发版（里面自动编、自动跑）；
//  手动挡：见本 skill SKILL.md"发版（混淆包）流水线"节。
//  跑器分两个子命令（必须分两个进程跑，原因见 Main 注释）：
//    behavior <混淆包目录> <期望版本号> <是否混淆版1/0>   执行态行为验收
//    dump <程序集目录>                                    元数据清单打印（发版脚本 diff 两边）
//
//  【验什么（两部分）】
//  A. 执行态（跑起来调公开 API）：水印含版本＋混淆标记 / AppLog 落盘中文无乱码 /
//     Crash 落盘 / DeviceConfig 缺省 / PolicyKeys×GetProperty 全命中 /
//     RecipeConfig JSON 往返 / 规则解析 / 激活比对不抛 / SetDarkMode 与两窗
//     SetDarkMode 反射仍在（Skip 表生效的直接证据）。
//  B. 元数据对账（dump＋diff）：未混淆版是"标准答案"，Models 命名空间下同名类型
//     的公开属性/字段名集合必须一字不差（Json 存盘＋字符串反射全靠这些名字）。
//     设计师私字段（窗体/控件）允许改名（代码引用随改，不靠名字），不在对账范围。
//
//  【退出码】0 = 全过；1 = 有失败（逐条打印，拿给发版人看）。
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using AgingTestSystem.Models;
using AgingTestSystem.Services;

namespace AgingTestSystem.Tests
{
    internal static class ObfuscationAcceptance
    {
        private static int _pass;
        private static int _fail;

        private static void Check(string name, bool cond, string detail = null)
        {
            if (cond)
            {
                _pass++;
                Console.WriteLine("  [PASS] " + name);
            }
            else
            {
                _fail++;
                Console.WriteLine("  [FAIL] " + name + (string.IsNullOrEmpty(detail) ? "" : "  => " + detail));
            }
        }

        private static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            // 两种模式分开跑（必须分开！）：behavior 模式执行态加载混淆程序集调公开 API；
            // dump 模式把指定目录程序集的 Models 元数据打印成文本（供脚本 diff）。
            // 为什么对账要"各跑一次再 diff 文本"，而不直接在进程里载两边对账：
            // 同名同版本（都是 AgingTestSystem 1.0.0.0）的两个程序集在同一 AppDomain 里
            // 只能存在一份，执行态和反射态加载都按 identity 归一，第二份直接报
            // "已从其他位置加载"（V1.88.10 实测）。分进程各 dump 一份文本再比，最稳。
            // 用法：behavior <混淆包目录> <期望版本号> <是否混淆版1/0>
            //      dump <程序集目录>   （打印 Models 类型/公开属性/公开字段清单）
            if (args.Length >= 1 && args[0].Trim().Equals("dump", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2)
                {
                    Console.WriteLine("用法: ObfuscationAcceptance.exe dump <程序集目录>");
                    return 2;
                }
                return RunDump(args[1]);
            }
            if (args.Length < 4 || !args[0].Trim().Equals("behavior", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("用法: ObfuscationAcceptance.exe behavior <混淆包目录> <期望版本号> <是否混淆版1/0>");
                Console.WriteLine("用法: ObfuscationAcceptance.exe dump <程序集目录>");
                return 2;
            }
            return RunBehavior(args[1], args[2], args[3].Trim() == "1");
        }

        /// <summary>
        /// behavior 模式：执行态行为验收（当前进程加载的就是混淆后程序集，调公开 API）。
        /// 注意：本方法一旦被 JIT，混淆程序集即执行态加载；oracle 模式绝不进本方法。
        /// </summary>
        private static int RunBehavior(string obfDir, string expectLabel, bool expectObf)
        {

            Console.WriteLine("混淆包验收（执行态）");
            Console.WriteLine("包目录 =" + obfDir);

            // ＝＝＝＝ A. 执行态（当前进程加载的就是混淆后程序集） ＝＝＝＝
            Console.WriteLine();
            Console.WriteLine("---- A. 执行态行为 ----");
            string line = "";
            try { line = BuildWatermark.GetStartupLine(); }
            catch (Exception ex) { line = ""; Console.WriteLine("  [FAIL] 水印入口抛异常: " + ex.GetType().Name); _fail++; }
            Check("A1 水印非空含启动标记", line.Contains("[启动]"), line);
            Check("A2 水印含期望版本号 " + expectLabel, line.Contains(expectLabel), line);
            Check(expectObf ? "A3 水印标混淆版" : "A3 水印标调试版",
                expectObf ? line.Contains("混淆版") : line.Contains("未混淆"), line);

            // AppLog 真实落盘（混淆后文件写路径出过 0 字节文件的坑，见 V1.88.10 复盘：必须真写断言）。
            string mark = "混淆验收_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            string logFile = Path.Combine(logDir, "AppLog_" + DateTime.Now.ToString("yyyyMMdd") + ".log");
            bool logOk = false;
            try
            {
                AppLogFileWriter.Write("[验收] 中文落盘 " + mark + "\r\n");
                string all = "";
                using (var fs = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs, Encoding.UTF8))
                    all = sr.ReadToEnd();
                logOk = all.Contains(mark);
            }
            catch (Exception ex) { logOk = false; Console.WriteLine("  异常: " + ex.GetType().Name + ": " + ex.Message); }
            Check("A4 AppLog 真实落盘中文无乱码", logOk);

            // Crash 真实落盘。
            string crashPath = null;
            bool crashOk = false;
            try
            {
                crashPath = CrashLogWriter.Write("验收线程", new InvalidOperationException("验收崩溃_" + mark));
                crashOk = crashPath != null && File.Exists(crashPath)
                    && File.ReadAllText(crashPath, Encoding.UTF8).Contains("验收崩溃_" + mark);
            }
            catch { crashOk = false; }
            Check("A5 Crash 真实落盘内容全", crashOk);
            try { if (crashPath != null && File.Exists(crashPath)) File.Delete(crashPath); } catch { }

            // DeviceConfig 缺省（公开属性名被改会直接编译不过；这里验行为值）。
            bool cfgOk = false;
            try { cfgOk = new DeviceConfig().TotalBarometers == 72; }
            catch (Exception ex) { Console.WriteLine("  异常: " + ex.GetType().Name); }
            Check("A6 DeviceConfig 缺省72台", cfgOk);

            // PolicyKeys × 字符串反射：混淆最怕的就是这一条，全命中才算 Skip 表有效。
            bool policyOk = false;
            string policyMiss = "";
            try
            {
                var keys = ProjectPolicyStore.PolicyKeys;
                int hit = 0;
                foreach (string k in keys)
                {
                    if (typeof(DeviceConfig).GetProperty(k) != null) hit++;
                    else if (policyMiss.Length < 200) policyMiss += k + ";";
                }
                policyOk = keys.Count > 0 && hit == keys.Count;
                policyMiss = "命中" + hit + "/" + keys.Count + " 缺失:" + policyMiss;
            }
            catch (Exception ex) { policyMiss = ex.GetType().Name + ": " + ex.Message; }
            Check("A7 策略key字符串反射全命中", policyOk, policyMiss);

            // RecipeConfig JSON 往返（Newtonsoft 按属性名序列化，改名即老文件读坏）。
            bool jsonOk = false;
            try
            {
                var rc = new RecipeConfig { Id = 7, Name = "验收配方" + mark };
                string s = Newtonsoft.Json.JsonConvert.SerializeObject(rc);
                var back = Newtonsoft.Json.JsonConvert.DeserializeObject<RecipeConfig>(s);
                jsonOk = s.Contains("验收配方" + mark) && back != null && back.Name == rc.Name && back.Id == 7;
            }
            catch (Exception ex) { jsonOk = false; Console.WriteLine("  异常: " + ex.GetType().Name + ": " + ex.Message); }
            Check("A8 配方JSON往返属性名完好", jsonOk);

            // 规则引擎解析（纯函数行为，混淆后逻辑必须一致）。
            bool ruleOk = false;
            try
            {
                RuleExpr.RuleExpression expr;
                string err;
                ruleOk = RuleExpr.TryParse("pressure<-5&&device==1", out expr, out err) && expr != null;
            }
            catch { ruleOk = false; }
            Check("A9 规则解析行为正常", ruleOk);

            // 激活比对不抛（公式区行为，错码静默是预期）。
            bool actOk = false;
            try
            {
                var kind = SoftwareActivation.VerifyActivationCode("错码", "错码");
                actOk = !string.IsNullOrEmpty(kind.ToString());
            }
            catch { actOk = false; }
            Check("A10 激活比对不抛", actOk);

            // SetDarkMode 反射仍在（ThemeManager 字符串找它；public 本来就保留，这里是双保险）。
            bool darkOk = false;
            try
            {
                darkOk = typeof(AgingTestSystem.Views.WorkstationGridView).GetMethod("SetDarkMode") != null;
            }
            catch { darkOk = false; }
            Check("A11 SetDarkMode 反射仍在", darkOk);

            // A12（V1.88.13 改）：两窗配方名已转下拉、_recipeAutoComplete 整文件删除，
            // 原 SkipField 随之移除——这里改锁"字段确已删除"（若日后误引回，验收当场红）。
            bool fldGone = false;
            try
            {
                var f1 = typeof(AgingTestSystem.Dialogs.BatchRecipeForm).GetField("_recipeAutoComplete",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var f2 = typeof(AgingTestSystem.Dialogs.StationSettingsForm).GetField("_recipeAutoComplete",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                fldGone = f1 == null && f2 == null;
            }
            catch { fldGone = false; }
            Check("A12 两窗联想字段已删除", fldGone);

            Console.WriteLine();
            Console.WriteLine("PASS = " + _pass + "   FAIL = " + _fail);
            Console.WriteLine(_fail == 0 ? "ACCEPT" : "REJECT");
            return _fail == 0 ? 0 : 1;
        }

        /// <summary>
        /// dump 模式：把指定目录 烧屏测试控制中心.exe 里 Models 命名空间的
        /// 类型/公开属性/公开字段打印成排序后的确定性文本（供发版脚本 diff 两边）。
        /// 本进程只加载这一份程序集（执行态），依赖（SunnyUI 等）就地解析。
        /// </summary>
        private static int RunDump(string asmDir)
        {
            try
            {
                string asmPath = Path.Combine(asmDir, "烧屏测试控制中心.exe");
                if (!File.Exists(asmPath))
                {
                    Console.WriteLine("DUMP-FAIL 程序集不存在: " + asmPath);
                    return 2;
                }
                AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
                {
                    string p = Path.Combine(asmDir, new AssemblyName(e.Name).Name + ".dll");
                    if (File.Exists(p))
                    {
                        try { return Assembly.LoadFrom(p); } catch { }
                    }
                    return null;
                };
                Assembly asm = Assembly.LoadFrom(asmPath);
                var lines = new List<string>();
                foreach (Type t in asm.GetTypes())
                {
                    if (t.Namespace == null || !t.Namespace.Equals("AgingTestSystem.Models")) continue;
                    if (t.IsNested) continue;
                    lines.Add("T:" + t.FullName);
                    foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                        lines.Add("P:" + t.FullName + "." + p.Name);
                    foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                        lines.Add("F:" + t.FullName + "." + f.Name);
                }
                if (lines.Count == 0)
                {
                    Console.WriteLine("DUMP-FAIL Models 下无类型（探针失效）");
                    return 1;
                }
                lines.Sort(StringComparer.Ordinal);
                foreach (string l in lines) Console.WriteLine(l);
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("DUMP-FAIL " + ex.GetType().Name + ": " + ex.Message);
                return 1;
            }
        }
    }
}
