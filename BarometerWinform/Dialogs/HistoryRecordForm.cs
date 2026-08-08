using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 历史记录查询窗体（业务逻辑部分）
    ///
    /// 【功能说明】
    /// 查询和展示老化测试的历史事件日志（启动/完成/报警/复位/急停/真空建立等）。
    ///
    /// 【数据来源（V1.10 改为读取真实日志文件）】
    /// - 日志文件：程序运行目录\Logs\TestLog_yyyyMMdd.csv（每天一个文件）
    /// - 写入方：<see cref="BarometerWinform.Services.TestEventLogger"/>
    /// - 列格式：时间,批号,设备编号,事件,详情,压力(kPa),温度(°C)
    /// - 历史记录窗体按选择的日期范围读取对应日期的 CSV 文件并展示
    ///
    /// 【导出（预留）】
    /// 当前导出功能为占位，后续可把查询结果导出为 Excel / CSV。
    /// </summary>
    public partial class HistoryRecordForm : Form
    {
        /// <summary>
        /// 日志条目数据结构（与 CSV 列对应）
        /// </summary>
        private class LogEntry
        {
            /// <summary>日志时间</summary>
            public DateTime Time { get; set; }
            /// <summary>批号</summary>
            public string Lot { get; set; }
            /// <summary>设备编号（如 NO.1）</summary>
            public string Device { get; set; }
            /// <summary>事件类型（如 测试开始/报警/复位）</summary>
            public string Event { get; set; }
            /// <summary>事件详情</summary>
            public string Detail { get; set; }
        }

        /// <summary>
        /// 日志数据列表（从 CSV 文件加载）
        /// </summary>
        private readonly List<LogEntry> _logs = new List<LogEntry>();

        /// <summary>
        /// 构造函数
        /// </summary>
        public HistoryRecordForm()
        {
            InitializeComponent();

            // 默认查询当天的记录（让用户打开窗体就能看到数据）
            dtpStart.Value = DateTime.Today;
            dtpEnd.Value = DateTime.Today;

            // 加载并显示当天数据
            QueryLogs();
        }

        /// <summary>
        /// 按日期范围查询日志并显示到 DataGridView
        /// 【V1.10】从 Logs 目录的 CSV 文件读取真实日志
        /// </summary>
        private void QueryLogs()
        {
            dgvHistory.Rows.Clear();
            _logs.Clear();

            // 日期范围：开始日期的 00:00:00 到结束日期的 23:59:59
            DateTime startTime = dtpStart.Value.Date;
            DateTime endTime = dtpEnd.Value.Date.AddDays(1).AddSeconds(-1);

            // 读取日期范围内每天的 CSV 文件（如果存在）
            string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (Directory.Exists(logDir))
            {
                for (DateTime day = startTime.Date; day <= endTime.Date; day = day.AddDays(1))
                {
                    string file = Path.Combine(logDir, $"TestLog_{day:yyyyMMdd}.csv");
                    if (File.Exists(file))
                    {
                        LoadCsvFile(file);
                    }
                }
            }

            // 统计符合时间范围的日志条数
            int matchCount = 0;
            foreach (var log in _logs)
            {
                if (log.Time >= startTime && log.Time <= endTime)
                {
                    // 设备列：NO.x（有批号则附上批号，便于追溯）
                    string deviceText = log.Device;
                    if (!string.IsNullOrWhiteSpace(log.Lot))
                    {
                        deviceText += $" [{log.Lot}]";
                    }

                    dgvHistory.Rows.Add(
                        log.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                        deviceText,
                        log.Event,
                        log.Detail
                    );
                    matchCount++;

                    // 限制最大显示条数，避免界面卡顿
                    if (matchCount >= 500)
                    {
                        break;
                    }
                }
            }

            // 在窗体标题显示查询结果统计
            this.Text = $"历史记录 - 共 {matchCount} 条" + (matchCount >= 500 ? "（已限制 500 条）" : "");
        }

        /// <summary>
        /// 加载单个 CSV 文件的内容到 _logs 列表
        /// 首行（表头）自动跳过
        /// </summary>
        /// <param name="filePath">CSV 文件完整路径</param>
        private void LoadCsvFile(string filePath)
        {
            try
            {
                // 用 StreamReader 逐行读取（文件可能较大）
                using (var reader = new StreamReader(filePath, Encoding.UTF8))
                {
                    bool firstLine = true;
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (firstLine)
                        {
                            // 跳过表头：时间,批号,设备编号,事件,详情,压力(kPa),温度(°C)
                            firstLine = false;
                            continue;
                        }
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        // 解析 CSV 行（支持带双引号的字段）
                        string[] fields = ParseCsvLine(line);
                        if (fields.Length < 5) continue; // 列数不足跳过

                        // 列含义（与 TestEventLogger 写入顺序一致）：
                        // 0=时间, 1=批号, 2=设备编号, 3=事件, 4=详情, 5=压力, 6=温度
                        if (!DateTime.TryParse(fields[0], out DateTime time)) continue;

                        _logs.Add(new LogEntry
                        {
                            Time = time,
                            Lot = fields[1],
                            Device = fields[2].StartsWith("NO.") ? fields[2] : $"NO.{fields[2]}",
                            Event = fields[3],
                            Detail = fields[4]
                        });
                    }
                }
            }
            catch
            {
                // 单个文件读取失败不影响其它文件
            }
        }

        /// <summary>
        /// 解析一行 CSV（支持字段含逗号/双引号时用双引号包裹）
        ///
        /// 【给新手的说明】
        /// 我们写入 CSV 时，如果详情里含逗号，会用双引号包起来，双引号本身翻倍转义。
        /// 解析时从行首逐字符扫描：碰到双引号就进入"引号内"状态，直到下一个双引号。
        /// </summary>
        /// <param name="line">一行 CSV 文本</param>
        /// <returns>解析出的字段数组</returns>
        private string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        // 双引号：可能是一个转义的双引号（""），也可能表示引号结束
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"'); // 转义的双引号
                            i++;
                        }
                        else
                        {
                            inQuotes = false; // 引号结束
                        }
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        fields.Add(current.ToString());
                        current.Clear();
                    }
                    else
                    {
                        current.Append(c);
                    }
                }
            }

            fields.Add(current.ToString());
            return fields.ToArray();
        }

        /// <summary>
        /// 查询按钮点击事件
        /// 按日期范围筛选日志并显示
        /// </summary>
        private void btnQuery_Click(object sender, EventArgs e)
        {
            // 校验日期范围
            if (dtpStart.Value > dtpEnd.Value)
            {
                MessageBox.Show("开始时间不能晚于结束时间", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 执行查询
            QueryLogs();
        }

        /// <summary>
        /// 导出按钮点击事件（预留功能）
        /// 【V1.10 说明】数据已存储在 Logs\TestLog_*.csv，
        /// 如需导出可直接打开/拷贝该目录，或后续实现按查询结果导出 Excel。
        /// </summary>
        private void btnExport_Click(object sender, EventArgs e)
        {
            if (dgvHistory.Rows.Count == 0)
            {
                MessageBox.Show("当前没有可导出的数据，请先查询", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 打开日志目录（让用户直接查看/拷贝 CSV 文件）
            string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (Directory.Exists(logDir))
            {
                try
                {
                    System.Diagnostics.Process.Start("explorer.exe", logDir);
                    return;
                }
                catch
                {
                    // 打开失败则提示路径
                }
            }

            MessageBox.Show(
                $"当前查询结果共 {dgvHistory.Rows.Count} 条日志。\n\n" +
                $"日志目录：{logDir}\n" +
                "已为你打开该目录，可直接查看 / 拷贝 CSV 日志文件。",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// 关闭按钮点击事件
        /// </summary>
        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
