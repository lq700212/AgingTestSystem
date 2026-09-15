using System;
using System.IO;
using System.Text;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 测试事件日志记录器（静态类）
    /// 【用途】
    /// 把老化测试过程中的关键事件（启动 / 完成 / 报警 / 复位 / 停止 / 急停）
    /// 追加写入 CSV 文件，按"日期"分文件存放，供质量追溯和历史记录窗体读取。
    /// 【文件格式】
    /// 目录：程序运行目录\Logs\
    /// 文件名：TestLog_yyyyMMdd.csv（每天一个文件）
    /// 表头：时间,批号,SN,配方,设备编号,事件,结果,详情,压力(kPa),温度(°C),电流(A)
    /// （SN/配方/结果结构化进列：启动/完成行详情里原来人肉拼的
    /// "SN:xxx 配方:xxx"字串同步摘除，同一信息只出现在专属列，不在详情里重复。
    /// NaN/无数据记空串，不写"NaN"字样。）
    /// 【口径规则（V1.76）】
    /// - 身份（SN/配方）：DeviceManager.ResolveEventIdentity 统一决策——
    ///   记录现值=事件瞬间绑定值；启动定格=该轮启动值（无快照回退现值）；
    ///   整机事件（设备编号=0，如急停/断电恢复汇总）无单值，记空。
    /// - 结果：只有"完成"（PASS/待判定）、"下料判定"（PASS/FAIL）、
    ///   "报警"（FAIL/装夹异常/设备异常）三类事件有判定语义，其余（启动/上电/
    ///   真空建立/中止/复位/急停/恢复/泄压/规则行）记空——中止/复位明确不计结果，
    ///   启动时填旧结果会误导成"这次已合格"。
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
        /// 日志目录（程序运行目录下的 Logs 文件夹，不存在则自动创建）。
        /// 【大扫荡】目录只建一次（静态记死；被删后写失败会触发重建+失败计数告警）。
        /// </summary>
        private static string _logDirCached;

        private static string LogDirectory
        {
            get
            {
                if (string.IsNullOrEmpty(_logDirCached) || !Directory.Exists(_logDirCached))
                {
                    string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                    Directory.CreateDirectory(dir);
                    _logDirCached = dir;
                }
                return _logDirCached;
            }
        }

        /// <summary>连续写失败计数（磁盘满/权限丢时用；恢复清零，见 Write 尾）。</summary>
        private static int _writeFailStreak;

        /// <summary>
        /// 追加一条事件日志
        /// </summary>
        /// <param name="lotNumber">当前批号（可为空字符串）</param>
        /// <param name="deviceId">设备编号（0 表示整机事件，如急停）</param>
        /// <param name="eventType">事件类型（如 启动 / 停止 / 报警 / 复位 / 急停 / 真空建立）</param>
        /// <param name="detail">事件详情描述</param>
        /// <param name="pressureKPa">关联的压力值（kPa，可选，用于报警/停止时记录）</param>
        /// <param name="temperature">关联的温度值（可选，用于送风机温度告警时记录）</param>
        /// <param name="currentA">关联的载台电流（可选，单位 A；
        /// null/NaN 记空串。老调用保持 6 参，行为不变）。</param>
        /// <param name="sn">产品 SN（结构化列；null=空串）</param>
        /// <param name="recipe">配方名称（结构化列；null=空串）</param>
        /// <param name="result">判定结果（PASS/FAIL/待判定/装夹异常/设备异常；
        /// 无判定语义的事件传空；null=空串）</param>
        public static void Write(string lotNumber, int deviceId, string eventType,
            string detail, decimal? pressureKPa = null, float? temperature = null,
            float? currentA = null, string sn = null, string recipe = null, string result = null)
        {
            try
            {
                lock (_lock)
                {
                    // 【大扫荡】一次 Now 复用：文件名与行时间同一次取值，
                    // 跨午夜不写错文件（以前两次 Now，23:59:59 写进次日文件行时间却是当天）。
                    DateTime now = DateTime.Now;
                    string file = Path.Combine(LogDirectory, $"TestLog_{now:yyyyMMdd}.csv");

                    // 文件不存在时先写表头（便于用 Excel 打开）
                    bool needHeader = !File.Exists(file);

                    // 【大扫荡】数字一律不变文化：逗号小数文化（de-DE）下 ToString 会多出
                    // 一个逗号，CSV 直接断列；NaN 温度记空（与电流分支一致，文件头"NaN 记空"）。
                    var invariant = System.Globalization.CultureInfo.InvariantCulture;
                    var sb = new StringBuilder();
                    sb.Append(now.ToString("yyyy-MM-dd HH:mm:ss")).Append(',');
                    sb.Append(CsvEscape(lotNumber ?? "")).Append(',');
                    sb.Append(CsvEscape(sn ?? "")).Append(',');
                    sb.Append(CsvEscape(recipe ?? "")).Append(',');
                    sb.Append(deviceId).Append(',');
                    sb.Append(CsvEscape(eventType)).Append(',');
                    sb.Append(CsvEscape(result ?? "")).Append(',');
                    sb.Append(CsvEscape(detail)).Append(',');
                    sb.Append(pressureKPa.HasValue
                        ? pressureKPa.Value.ToString(invariant) : "").Append(',');
                    sb.Append((temperature.HasValue && !float.IsNaN(temperature.Value))
                        ? temperature.Value.ToString("0.0", invariant) : "").Append(',');
                    // 电流（V1.74）：有数写两位小数，无数（null/NaN）记空（历史窗与报表都按空处理）
                    sb.Append((currentA.HasValue && !float.IsNaN(currentA.Value))
                        ? currentA.Value.ToString("0.00", invariant) : "");

                    using (var writer = new StreamWriter(file, true, Encoding.UTF8))
                    {
                        if (needHeader)
                        {
                            writer.WriteLine("时间,批号,SN,配方,设备编号,事件,结果,详情,压力(kPa),温度(°C),电流(A)");
                        }
                        writer.WriteLine(sb.ToString());
                    }
                    _writeFailStreak = 0;
                }
            }
            catch (Exception ex)
            {
                // 日志写入失败不能影响主流程（采集/UI），但不能无声断裂：
                // 连续失败记 Debug（首败+每 100 次），磁盘满/权限丢时至少留痕。
                _writeFailStreak++;
                if (_writeFailStreak == 1 || _writeFailStreak % 100 == 0)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[事件日志] 写失败已连续 {_writeFailStreak} 次: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// CSV 字段转义
        /// 字段里如果含逗号 / 双引号 / 换行 / 回车，需要用双引号包裹、双引号翻倍
        /// 补上回车 \r：串口/扫码字符串常带 \r\n，不包裹会导致 CSV 断行错位
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
