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
    /// 表头：时间,批号,设备编号,事件,详情,压力(kPa),温度(°C),电流(A)
    /// （【V1.74】电流列追加在末尾：老文件无此列，历史窗解析按索引取前 5 列，
    /// 新列缺失按空处理，前后兼容，追溯链不断。NaN/无数据记空串，不写"NaN"字样。）
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
        /// <param name="currentA">关联的载台电流（【V1.74 新增】可选，单位 A；
        /// null/NaN 记空串。老调用保持 6 参，行为不变）。</param>
        public static void Write(string lotNumber, int deviceId, string eventType,
            string detail, decimal? pressureKPa = null, float? temperature = null,
            float? currentA = null)
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
                    sb.Append(temperature.HasValue ? temperature.Value.ToString("0.0") : "").Append(',');
                    // 电流（V1.74）：有数写两位小数，无数（null/NaN）记空（历史窗与报表都按空处理）
                    sb.Append((currentA.HasValue && !float.IsNaN(currentA.Value))
                        ? currentA.Value.ToString("0.00") : "");

                    using (var writer = new StreamWriter(file, true, Encoding.UTF8))
                    {
                        if (needHeader)
                        {
                            writer.WriteLine("时间,批号,设备编号,事件,详情,压力(kPa),温度(°C),电流(A)");
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
        /// 字段里如果含逗号 / 双引号 / 换行 / 回车，需要用双引号包裹、双引号翻倍
        /// 【V1.62】补上回车 \r：串口/扫码字符串常带 \r\n，不包裹会导致 CSV 断行错位
        /// （历史记录窗按行解析，错位后整行被丢弃，追溯链断裂）。
        /// </summary>
        private static string CsvEscape(string value)
        {
            if (value == null) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }
    }
}
