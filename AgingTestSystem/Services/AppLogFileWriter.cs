using System;
using System.IO;
using System.Text;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 主窗体操作日志持久化（静态类）
    ///
    /// 【用途】
    /// 把主窗体 UI 日志（MainForm.WriteLog 输出的所有消息：设备启动失败/连接状态变化/扫码/
    /// 温度告警/行全选/设置变更等操作动作）追加写入本地纯文本日志文件，按"日期"分文件存放。
    /// 程序重启后日志不丢失，可离线追溯，避免"日志只存在内存里、断电即丢"。
    ///
    /// 【与 TestEventLogger 的区别】
    /// - TestEventLogger：结构化测试事件日志（时间,批号,设备编号,事件,详情，CSV），
    ///   供"历史记录查询窗体"（HistoryRecordForm）按日期筛选展示；
    /// - AppLogFileWriter：自由文本操作日志（带时间戳的一行一行消息），
    ///   与主窗体右侧 LOG 文本框显示内容完全一致，供人工翻阅/排查。
    /// 两者共用同一个 Logs 目录，只是文件名前缀不同，互不干扰。
    ///
    /// 【文件格式】
    /// 目录：程序运行目录\Logs\（不存在则自动创建）
    /// 文件名：AppLog_yyyyMMdd.log（每天一个文件，跨天自动切换）
    /// 内容：与 UI 文本框逐行一致，如 "[2024-01-01 12:00:00] 设备启动失败：xxx"
    ///
    /// 【给新手的说明】
    /// - 静态类不需要实例化，直接 AppLogFileWriter.Write(logLine) 调用即可；
    /// - 用 lock 保证多线程（采集线程 / UI 线程）同时写文件不会互相覆盖；
    /// - StreamWriter 缓存复用：不用每条日志都打开/关闭文件，但每次写入后立即 Flush
    ///   强制落盘——万一程序崩溃/断电，已写的日志也不会丢；
    /// - 写日志失败一律静默（catch 掉），日志系统绝不能拖垮业务主流程。
    /// </summary>
    public static class AppLogFileWriter
    {
        /// <summary>
        /// 写文件用的互斥锁（多线程追加写必须串行化，防止内容交错/覆盖）
        /// </summary>
        private static readonly object _lock = new object();

        /// <summary>
        /// 当前打开的日志文件完整路径（用于跨日期切换判断：换天则关闭旧文件、开新文件）
        /// </summary>
        private static string _currentFile;

        /// <summary>
        /// 当前日志文件的写入器（缓存复用，避免每条日志都 new StreamWriter）
        /// </summary>
        private static StreamWriter _writer;

        /// <summary>
        /// 日志目录（程序运行目录下的 Logs 文件夹，不存在则自动创建）
        /// 与 TestEventLogger 共用同一目录，保证所有日志集中在 Logs 下方便现场拷贝
        /// </summary>
        private static string LogDirectory
        {
            get
            {
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        /// <summary>
        /// 追加一条完整日志行到当日日志文件
        /// </summary>
        /// <param name="logLine">完整日志行（含时间戳，如 "[2024-01-01 12:00:00] 消息\r\n"），
        /// 与 UI 文本框显示内容保持一致；内部直接原样写入，不再额外换行/加时间戳</param>
        public static void Write(string logLine)
        {
            if (string.IsNullOrEmpty(logLine)) return;
            try
            {
                lock (_lock)
                {
                    // 按日期分文件：文件名里带当天日期，跨天自动切换新文件
                    string file = Path.Combine(LogDirectory, $"AppLog_{DateTime.Now:yyyyMMdd}.log");

                    // 首次写入 / 跨天切换：关闭旧写入器，打开（或追加创建）新文件
                    // 用 UTF-8 编码写入，与项目"文件编码必须 UTF-8"约定一致，避免中文乱码
                    if (_writer == null || _currentFile != file)
                    {
                        _writer?.Dispose();
                        _writer = new StreamWriter(file, true, Encoding.UTF8);
                        _currentFile = file;
                    }

                    // 原样写入日志行，然后立即 Flush 落盘：
                    // 若程序在两次写之间崩溃，已写入的日志仍在磁盘上，不会丢失
                    _writer.Write(logLine);
                    _writer.Flush();
                }
            }
            catch
            {
                // 日志写入失败不能影响主流程（采集/UI），静默吞掉
                // 常见原因：磁盘已满 / 目录无写权限 / 文件被占用
            }
        }
    }
}
