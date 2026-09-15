using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 启动版本水印（V1.88.9 调试部署新增，静态类）。
    ///
    /// 【为什么要有这个类】
    /// 马上要把软件发到客户工控机上联调，客户一出问题就会把 Logs 文件夹拷回来。
    /// 如果日志里没有版本号，会出现三个麻烦：
    /// 1. 分不清这份日志是哪个版本跑出来的（调试期一天可能发两三个包）；
    /// 2. 以后上了混淆版，堆栈里的类名方法名全是 a/b/c，必须靠"版本号→mapping 文件"
    ///    才能反解，没有版本号 mapping 就对不上；
    /// 3. 分不清是调试版还是混淆版跑出来的（两种包的行为-bisect 方向完全不同）。
    /// 所以主窗体启动时第一行日志强制写水印（见 MainForm 构造），崩溃日志里也带同一份
    /// 水印（见 CrashLogWriter），远程排查先看水印再动手。
    ///
    /// 【版本号从哪来】
    /// - ReleaseLabel：跟着 CHANGELOG 走的手工版本号（如 V1.88.9）。AssemblyInfo 里的
    ///   AssemblyVersion 常年是 1.0.0.0（历史原因没人维护），指望不上，所以单立一个常量，
    ///   每次发版同步改这里 + CHANGELOG 对应小节，两处对不上就说明有人漏了。
    /// - 构建时间：取 exe 文件本身的最后写入时间近似（Release 编译产物的写入时间即打包时间，
    ///   精度到秒，够区分"今天发的两个包"）。取不到时显示"未知"，绝不抛异常。
    /// - 混淆标记：IsObfuscatedBuild 常量，调试期恒 false。以后打混淆包时手动改成 true
    ///   再编译（为什么不用配置文件：配置项要走"五处同步"太重，一个布尔常量够了，
    ///   发版检查单里会有一项对它）。
    ///
    /// 【给新手的说明】
    /// - BuildWatermarkLine 是纯函数（输入→字符串，不碰磁盘/注册表/网络），单元测试直接调它；
    /// - GetStartupLine 是运行时入口（自动收集真实版本号/时间/OS），内部全程 try/catch，
    ///   拿不到就写"未知"，保证启动日志第一行永远有东西；
    /// - 改了本文件的输出格式，记得同步改 TestRunner 里 DeployDiagV188_9 模块的断言关键字。
    /// </summary>
    public static class BuildWatermark
    {
        /// <summary>
        /// 对外版本号，跟着 CHANGELOG 走。发版时同步改这里和 CHANGELOG 顶部小节标题。
        /// </summary>
        public const string ReleaseLabel = "V1.91";

        /// <summary>
        /// 是否混淆版。调试期恒 false；以后打混淆包时改成 true 再编译，
        /// 水印里会显示"混淆版"，提醒排障人堆栈要拿 mapping 反解。
        /// </summary>
        public const bool IsObfuscatedBuild = false;

        /// <summary>
        /// 组装水印行（纯函数：同样的输入永远得到同样的字符串，不读磁盘，专供单测和复用）。
        /// </summary>
        /// <param name="releaseLabel">对外版本号，如 V1.88.9</param>
        /// <param name="asmVersion">程序集版本，如 1.0.0.0</param>
        /// <param name="fileVersion">文件版本（exe 属性里看到的版本），拿不到传空串即可</param>
        /// <param name="buildTime">构建时间；拿不到传 DateTime.MinValue，会渲染成"未知"</param>
        /// <param name="isObfuscated">是否混淆版</param>
        /// <param name="is64Bit">是否 64 位进程（现场工控机有 32/64 位两种，记一笔省得来回问）</param>
        /// <param name="osVersion">操作系统版本字串，拿不到传空串即可</param>
        /// <returns>单行水印文本（不带时间戳前缀和换行，由调用方按日志格式包装）</returns>
        public static string BuildWatermarkLine(string releaseLabel, string asmVersion,
            string fileVersion, DateTime buildTime, bool isObfuscated, bool is64Bit, string osVersion)
        {
            // 空值全部兜底成"未知"：调用方（尤其崩溃路径）可能什么都拿不到，
            // 水印缺一块也不能整行消失，否则远程连"哪个版本崩的"都不知道。
            string rel = string.IsNullOrWhiteSpace(releaseLabel) ? "未知" : releaseLabel.Trim();
            string asm = string.IsNullOrWhiteSpace(asmVersion) ? "未知" : asmVersion.Trim();
            string fv = string.IsNullOrWhiteSpace(fileVersion) ? "未知" : fileVersion.Trim();
            string bt = buildTime == DateTime.MinValue
                ? "未知" : buildTime.ToString("yyyy-MM-dd HH:mm:ss");
            string obf = isObfuscated ? "混淆版（堆栈需用mapping反解）" : "调试版（未混淆）";
            string bits = is64Bit ? "64位进程" : "32位进程";
            string os = string.IsNullOrWhiteSpace(osVersion) ? "未知OS" : osVersion.Trim();
            return "[启动] 软件版本 " + rel
                + "｜程序集 " + asm
                + "｜文件版本 " + fv
                + "｜构建时间 " + bt
                + "｜" + obf
                + "｜" + bits
                + "｜" + os;
        }

        /// <summary>
        /// 取 exe 构建时间（用 exe 文件最后写入时间近似）。
        /// 取不到（如宿主特殊、权限不足）返回 DateTime.MinValue，上层渲染成"未知"，绝不抛异常。
        /// </summary>
        public static DateTime GetBuildTime()
        {
            try
            {
                string exePath = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                    return File.GetLastWriteTime(exePath);
            }
            catch
            {
                // 时间拿不到不影响启动，静默回退，调用方显示"未知"即可。
            }
            return DateTime.MinValue;
        }

        /// <summary>
        /// 运行时入口：收集真实环境信息并组装水印行（绝不抛异常，异常时返回带"未知"的降级行）。
        /// 主窗体构造里第一行 WriteLog 调的就是它；崩溃日志正文里嵌的也是它（同一份口径）。
        /// </summary>
        public static string GetStartupLine()
        {
            try
            {
                string asm = Assembly.GetExecutingAssembly().GetName().Version != null
                    ? Assembly.GetExecutingAssembly().GetName().Version.ToString() : "";
                string fv = "";
                try
                {
                    string exePath = Assembly.GetExecutingAssembly().Location;
                    if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                        fv = FileVersionInfo.GetVersionInfo(exePath).FileVersion ?? "";
                }
                catch
                {
                    // 文件版本拿不到就空着，纯函数会兜底成"未知"。
                }
                string os = "";
                try
                {
                    os = Environment.OSVersion.VersionString ?? "";
                }
                catch
                {
                    // OS 版本拿不到就空着，不影响主流程。
                }
                return BuildWatermarkLine(ReleaseLabel, asm, fv, GetBuildTime(),
                    IsObfuscatedBuild, Environment.Is64BitProcess, os);
            }
            catch
            {
                // 最后一道防线：连反射程序集名都失败（极端精简环境），也要给出一行可写日志的文本，
                // 不能让"写第一行日志"这个动作本身把启动搞崩。
                return "[启动] 软件版本 " + ReleaseLabel + "｜环境信息获取失败（未知）";
            }
        }
    }
}
