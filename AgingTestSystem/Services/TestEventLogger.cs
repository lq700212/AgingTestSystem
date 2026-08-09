using System;
using System.IO;
using System.Text;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 测试事件日志记录器（静态类）
    ///
    /// 【用途】
    /// 把老化测试过程中的关键事件（启动 / 完成 / 报警 / 复位 / 停止 / 急停）
    /// 追加写入 CSV 文件，按"日期"分文件存放，供质量追溯和历史记录窗体读取。
    ///
    /// 【文件格式】
    /// 目录：程序运行目录\Logs\
    /// 文件名：TestLog_yyyyMMdd.csv（每天一个文件）
    /// 表头：时间,批号,设备编号,事件,详情,压力(kPa),温度(°C)
    ///
    /// 【给新手的说明】
    /// - 静态类不需要实例化，直接 TestEventLogger.Write(...) 调用即可
    /// - 用 lock 保证多线程（采集线程 / UI 线程）同时写文件不会冲突
    /// - 写日志失败不影响主流程（catch 掉），日志系统不能拖垮业务
    /// </summary>
    public static class TestEventLogger
    {
        /// <summary>
        /// 写文件用的互斥锁（多线程追加写 CSV 必须串行化）
        /// </summary>
        private static readonly object _lock = new object();

        /// <summary>
        /// 日志目录（程序运行目录下的 Logs 文件夹，不存在则自动创建）
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
        /// 追加一条事件日志
        /// </summary>
        /// <param name="lotNumber">当前批号（可为空字符串）</param>
        /// <param name="deviceId">设备编号（0 表示整机事件，如急停）</param>
        /// <param name="eventType">事件类型（如 启动 / 停止 / 报警 / 复位 / 急停 / 真空建立）</param>
        /// <param name="detail">事件详情描述</param>
        /// <param name="pressureKPa">关联的压力值（kPa，可选，用于报警/停止时记录）</param>
        /// <param name="temperature">关联的温度值（可选，用于送风机温度告警时记录）</param>
        public static void Write(string lotNumber, int deviceId, string eventType,
            string detail, decimal? pressureKPa = null, float? temperature = null)
        {
            try
            {
                lock (_lock)
                {
                    string file = Path.Combine(LogDirectory, $"TestLog_{DateTime.Now:yyyyMMdd}.csv");

                    // 文件不存在时先写表头（便于用 Excel 打开）
                    bool needHeader = !File.Exists(file);

                    var sb = new StringBuilder();
                    sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append(',');
                    sb.Append(CsvEscape(lotNumber ?? "")).Append(',');
                    sb.Append(deviceId).Append(',');
                    sb.Append(CsvEscape(eventType)).Append(',');
                    sb.Append(CsvEscape(detail)).Append(',');
                    sb.Append(pressureKPa.HasValue ? pressureKPa.Value.ToString() : "").Append(',');
                    sb.Append(temperature.HasValue ? temperature.Value.ToString("0.0") : "");

                    using (var writer = new StreamWriter(file, true, Encoding.UTF8))
                    {
                        if (needHeader)
                        {
                            writer.WriteLine("时间,批号,设备编号,事件,详情,压力(kPa),温度(°C)");
                        }
                        writer.WriteLine(sb.ToString());
                    }
                }
            }
            catch
            {
                // 日志写入失败不能影响主流程（采集/UI），静默吞掉
            }
        }

        /// <summary>
        /// CSV 字段转义
        /// 字段里如果含逗号 / 双引号 / 换行，需要用双引号包裹、双引号翻倍
        /// </summary>
        private static string CsvEscape(string value)
        {
            if (value == null) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }
    }
}
