using System;
using System.IO;
using System.Text;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 崩溃日志落盘器（V1.88.9 调试部署新增，静态类）。
    ///
    /// 【解决什么问题】
    /// 以前 Program.cs 里的两个全局异常处理只弹 MessageBox + 写一句 Debug 输出：
    /// 客户现场一崩，操作员点掉弹窗就什么都没留下，远程排查只能靠"口述报错截图"。
    /// 现在崩溃瞬间多做两件事（都在 try/catch 里，写失败也绝不添乱）：
    /// 1. 把完整异常（含类型/消息/全堆栈 + 版本水印）写成独立文件
    ///    Logs\Crash_yyyyMMdd_HHmmssfff_xxxxxxxx.log（一崩一文件，文件名带时间+随机尾，
    ///    并发两崩也不会互盖，客户单发这一个文件就能排障）；
    /// 2. 由调用方（Program.cs）再往 AppLog 里追加一行摘要，指明崩溃文件叫什么，
    ///    这样只拷 AppLog 也能顺藤摸瓜找到 crash 文件。
    ///
    /// 【为什么不用 AtomicFile】
    /// AtomicFile 是给"会被反复覆盖的运行时 json"准备的（防写半截断电）。
    /// 崩溃文件一崩一新名、一次性写入、写完才有人来拷，不存在"读半截"和"并发覆写"，
    /// 直接 WriteAllText + lock 防同进程并发两崩串行就够了，保持和 AppLogFileWriter
    /// 一致的轻量风格。
    ///
    /// 【给新手的说明】
    /// - BuildCrashFileName / BuildCrashContent 是纯函数（不碰磁盘），单元测试只测它们，
    ///   另加一条真实 Write 落盘（harness 会把产物拷到隔离临时目录，不污染仓库）；
    /// - Write 第一个参数是"哪个线程崩的"（UI线程/非UI线程），由 Program.cs 传入；
    /// - exceptionObject 故意用 object 而不用 Exception：非 UI 线程的 UnhandledException
    ///   可能装着非 Exception 对象甚至 null，三种情况都要吃得下（见 Write 内分支注释）；
    /// - 本类所有公开方法绝不向外抛异常：崩溃处理路径上再抛一次，进程就直接没了。
    /// </summary>
    public static class CrashLogWriter
    {
        /// <summary>多线程并发两崩时串行化文件写入，防止内容交错</summary>
        private static readonly object _lock = new object();

        /// <summary>
        /// 组装崩溃文件名（纯函数）。
        /// 格式 Crash_20260915_103015123_ab12cd34.log：日期时间精确到毫秒 + 8 位随机尾。
        /// 【为什么要随机尾】同一毫秒内连崩两次（比如 UI 定时器和采集线程同时崩），
        /// 光靠时间戳会同名互盖，后崩的把先崩的吃了——随机尾保证一崩一文件。
        /// 单测时传固定 suffix 可得到确定性文件名；生产调用 Write 时内部自动生成随机尾。
        /// </summary>
        public static string BuildCrashFileName(DateTime now, string suffix)
        {
            string tail = string.IsNullOrWhiteSpace(suffix)
                ? Guid.NewGuid().ToString("N").Substring(0, 8)
                : suffix.Trim();
            return "Crash_" + now.ToString("yyyyMMdd_HHmmssfff") + "_" + tail + ".log";
        }

        /// <summary>
        /// 组装崩溃文件正文（纯函数）。
        /// 排版顺序是给排障人看的：先水印（先定版本再谈堆栈）→ 线程 → 类型/消息 → 全堆栈。
        /// 注意用 UTF-8 写，中文消息（比如"设备启动失败：COM3 打开超时"）不会乱码。
        /// </summary>
        public static string BuildCrashContent(DateTime now, string watermarkLine,
            string threadKind, string exTypeName, string exMessage, string exDetail)
        {
            var sb = new StringBuilder();
            sb.AppendLine("========== 老化测试系统崩溃日志 ==========");
            sb.AppendLine("崩溃时间：" + now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            sb.AppendLine(string.IsNullOrWhiteSpace(watermarkLine)
                ? "[启动] 软件版本 未知（水印缺失）" : watermarkLine);
            sb.AppendLine("崩溃线程：" + (string.IsNullOrWhiteSpace(threadKind) ? "未知" : threadKind.Trim()));
            sb.AppendLine("异常类型：" + (string.IsNullOrWhiteSpace(exTypeName) ? "未知" : exTypeName.Trim()));
            sb.AppendLine("异常消息：" + (exMessage ?? "（无消息）"));
            sb.AppendLine("---------- 完整堆栈（混淆版需用对应版本mapping反解） ----------");
            sb.AppendLine(string.IsNullOrEmpty(exDetail) ? "（无堆栈详情）" : exDetail);
            sb.AppendLine("========== 文件结束（请把本文件发给厂家排查） ==========");
            return sb.ToString();
        }

        /// <summary>
        /// 写一条崩溃日志（运行时入口）。成功返回完整文件路径，失败返回 null，绝不抛异常。
        /// </summary>
        /// <param name="threadKind">崩溃线程说明，如 "UI线程" / "非UI线程"</param>
        /// <param name="exceptionObject">异常对象：Exception / 非 Exception 对象 / null 都能吃</param>
        public static string Write(string threadKind, object exceptionObject)
        {
            try
            {
                // 分三种情况拆异常信息（顺序不能乱：先判 Exception，再判 null，最后当未知对象 ToString）：
                // 1. 正常 Exception：类型用 FullName（含命名空间，混淆反解时比短名好定位），
                //    详情用 ex.ToString()（自带类型+消息+堆栈+内部异常链，比单拼 StackTrace 全）；
                // 2. null：非 UI 线程极端情况下 ExceptionObject 可能为 null，给占位不断链；
                // 3. 非 Exception 对象（如有人 throw "字符串"）：ToString 留痕，总比"未知"强。
                Exception ex = exceptionObject as Exception;
                string typeName;
                string message;
                string detail;
                if (ex != null)
                {
                    typeName = ex.GetType().FullName;
                    message = ex.Message;
                    try
                    {
                        detail = ex.ToString();
                    }
                    catch
                    {
                        detail = "（异常详情获取失败）";
                    }
                }
                else if (exceptionObject == null)
                {
                    typeName = "（空异常对象）";
                    message = "非 UI 线程抛出了 null（极端情况，无消息）";
                    detail = "（无堆栈详情）";
                }
                else
                {
                    string objText;
                    try
                    {
                        objText = exceptionObject.ToString();
                    }
                    catch
                    {
                        objText = "（对象 ToString 失败）";
                    }
                    typeName = "非托管异常对象：" + exceptionObject.GetType().FullName;
                    message = objText;
                    detail = objText;
                }

                DateTime now = DateTime.Now;
                string watermark;
                try
                {
                    watermark = BuildWatermark.GetStartupLine();
                }
                catch
                {
                    // 水印失败不能挡住崩溃落盘，降级成一行占位。
                    watermark = "[启动] 软件版本 " + BuildWatermark.ReleaseLabel + "（水印获取失败）";
                }
                string content = BuildCrashContent(now, watermark, threadKind, typeName, message, detail);

                lock (_lock)
                {
                    string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                    Directory.CreateDirectory(dir);
                    string file = Path.Combine(dir, BuildCrashFileName(now, null));
                    File.WriteAllText(file, content, Encoding.UTF8);
                    return file;
                }
            }
            catch
            {
                // 崩溃落盘本身失败（磁盘满/无权限/路径被占）：静默返回 null，
                // 调用方（Program.cs）会在弹框里如实写"崩溃日志写入失败"，不二次弹框，
                // 更不能让异常处理程序自己抛异常（那会直接终结进程）。
                return null;
            }
        }
    }
}
