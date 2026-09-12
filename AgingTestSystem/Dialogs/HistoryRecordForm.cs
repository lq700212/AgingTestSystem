using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using AgingTestSystem.Services;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 历史记录查询窗体（业务逻辑部分）
    ///
    /// 【功能说明】
    /// 查询和展示老化测试的历史事件日志（启动/完成/报警/复位/急停/真空建立等）。
    ///
    /// 【数据来源（V1.10 改为读取真实日志文件）】
    /// - 日志文件：程序运行目录\Logs\TestLog_yyyyMMdd.csv（每天一个文件）
    /// - 写入方：<see cref="AgingTestSystem.Services.TestEventLogger"/>
    /// - 列格式：时间,批号,设备编号,事件,详情,压力(kPa),温度(°C),电流(A)
    ///   （V1.74 追加电流列；老文件无此列，解析按"有则取、无则空"，前后兼容）
    /// - 历史记录窗体按选择的日期范围读取对应日期的 CSV 文件并展示
    ///
    /// 【导出（V1.74 落地）】
    /// 导出按钮把"当前查询结果"按 ReportColumns 列配置生成 xlsx：
    /// 留空=缺省预设8列（时间/批号/工位/事件/详情/压力/温度/电流），客户在系统设置→
    /// 报表导出分类里改列（跟项目走，切项目即换模板）。写盘套路与 ID 绑定窗同源
    /// （OpenXml，表头加粗居中 + 数据行普通样式，零新依赖）。
    ///
    /// 【界面布局】
    /// ┌────────────────────────────────────────────────┐
    /// │ 开始时间:[▣]  结束时间:[▣]  [查询] [导出]      │ ← panelTop 顶部查询条
    /// ├────────────────────────────────────────────────┤
    /// │ dgvHistory（DataGridView，Dock Fill）           │
    /// │ ┌──────────┬──────────┬────────┬────────────┐  │
    /// │ │  时间     │ 设备编号  │  事件   │   详情      │  │
    /// │ │ 10:00:00 │ NO.1     │ 测试开始│ ...        │  │
    /// │ │ 10:00:05 │ NO.1     │ 报警   │ ...        │  │
    /// │ │(自动撑满,只读,整行选中)                       │  │
    /// │ └──────────┴──────────┴────────┴────────────┘  │
    /// ├────────────────────────────────────────────────┤
    /// │                                      [关闭]    │ ← panelBottom 底部关闭条
    /// └────────────────────────────────────────────────┘
    /// 说明：按选择的日期范围读取 Logs\TestLog_yyyyMMdd.csv 并展示。
    ///</summary>
    public partial class HistoryRecordForm : Sunny.UI.UIForm
    {
        /// <summary>
        /// 日志条目数据结构（与 CSV 列对应；V1.74 加压力/温度/电流，供报表导出用，
        /// 老文件缺列按空串处理——显示不受影响）。
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
            /// <summary>压力值原文（kPa，老文件无此列则空）</summary>
            public string Pressure { get; set; }
            /// <summary>温度值原文（°C，老文件无此列则空）</summary>
            public string Temperature { get; set; }
            /// <summary>电流值原文（A，V1.74 列；老文件无则空）</summary>
            public string Current { get; set; }
        }

        /// <summary>
        /// 日志数据列表（从 CSV 文件加载）
        /// </summary>
        private readonly List<LogEntry> _logs = new List<LogEntry>();

        /// <summary>
        /// 当前查询结果（【V1.74 新增】导出按钮的数据源：与表格显示同序同内容，
        /// 上限 500 条与显示一致——报表明细一次导 500 条可读性最好，要全量直接拷 CSV）。
        /// </summary>
        private readonly List<LogEntry> _shown = new List<LogEntry>();

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
            _shown.Clear();

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
                    _shown.Add(log);
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
                        // 0=时间, 1=批号, 2=设备编号, 3=事件, 4=详情, 5=压力, 6=温度, 7=电流(V1.74)
                        // 老文件列少：按"有则取、无则空"，缺列不丢行（追溯链不断）。
                        if (!DateTime.TryParse(fields[0], out DateTime time)) continue;

                        _logs.Add(new LogEntry
                        {
                            Time = time,
                            Lot = fields[1],
                            Device = fields[2].StartsWith("NO.") ? fields[2] : $"NO.{fields[2]}",
                            Event = fields[3],
                            Detail = fields[4],
                            Pressure = fields.Length > 5 ? fields[5] : "",
                            Temperature = fields.Length > 6 ? fields[6] : "",
                            Current = fields.Length > 7 ? fields[7] : ""
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
        /// 导出按钮点击事件（【V1.74 落地】按 ReportColumns 列配置导出 xlsx）。
        /// 数据源 = 当前查询结果（_shown，与表格同序同内容，上限 500 条）；
        /// 列配置留空走缺省预设 8 列，手改文件写错走预设兜底（Resolve 保证永远有列）。
        /// </summary>
        private void btnExport_Click(object sender, EventArgs e)
        {
            if (_shown.Count == 0)
            {
                MessageBox.Show("当前没有可导出的数据，请先查询", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 列配置跟项目走：当前项目的 Policy.json（没配=缺省预设；脏值走预设兜底）
            string rawColumns = ProjectPolicyStore.GetRaw("ReportColumns");
            List<ReportColumns.Column> columns = ReportColumns.Resolve(rawColumns);

            DateTime now = DateTime.Now;
            using (SaveFileDialog saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "Excel文件 (*.xlsx)|*.xlsx";
                saveDialog.FileName = $"老化报表_{now:yyyyMMdd}_{now:HHmmss}.xlsx";
                saveDialog.Title = "保存老化报表";
                saveDialog.DefaultExt = "xlsx";
                saveDialog.AddExtension = true;
                if (saveDialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    WriteReportXlsx(saveDialog.FileName, columns, _shown);
                    MessageBox.Show($"报表已生成：{_shown.Count} 条，{columns.Count} 列。\n{saveDialog.FileName}",
                        "导出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"生成报表失败：\n{ex.Message}", "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// 取某条日志在指定报表字段下的单元格文本（字段只可能是 AvailableFields 里的 8 个，
        /// 未知字段（理论上到不了，Resolve 已洗过）返回空串，绝不抛异常）。
        /// </summary>
        private static string GetReportCellText(LogEntry log, string field)
        {
            if (log == null || string.IsNullOrEmpty(field)) return "";
            switch (field)
            {
                case "time": return log.Time.ToString("yyyy-MM-dd HH:mm:ss");
                case "lot": return log.Lot ?? "";
                case "device": return log.Device ?? "";
                case "event": return log.Event ?? "";
                case "detail": return log.Detail ?? "";
                case "pressure": return log.Pressure ?? "";
                case "temp": return log.Temperature ?? "";
                case "current": return log.Current ?? "";
                default: return "";
            }
        }

        /// <summary>
        /// 写报表 xlsx（OpenXml，套路与 ID 绑定窗同源：工作簿→工作表→表头加粗居中→数据行）。
        /// </summary>
        /// <param name="filePath">保存路径</param>
        /// <param name="columns">生效列（显示名做列头）</param>
        /// <param name="rows">数据行（调用方已按查询结果备好）</param>
        private static void WriteReportXlsx(string filePath, List<ReportColumns.Column> columns,
            List<LogEntry> rows)
        {
            using (SpreadsheetDocument document = SpreadsheetDocument.Create(
                filePath, SpreadsheetDocumentType.Workbook))
            {
                WorkbookPart workbookPart = document.AddWorkbookPart();
                workbookPart.Workbook = new Workbook();

                WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                worksheetPart.Worksheet = new Worksheet(new SheetData());

                Sheets sheets = workbookPart.Workbook.AppendChild(new Sheets());
                Sheet sheet = new Sheet();
                sheet.Id = workbookPart.GetIdOfPart(worksheetPart);
                sheet.SheetId = 1;
                sheet.Name = "老化报表";
                sheets.Append(sheet);

                SheetData sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();
                uint headerStyleIndex = CreateReportHeaderFormat(document);

                // 表头行（显示名）
                string[] headers = new string[columns.Count];
                for (int i = 0; i < columns.Count; i++) headers[i] = columns[i].Display;
                sheetData.Append(CreateReportRow(0, headers, headerStyleIndex));

                // 数据行（样式 0=普通）
                int rowIndex = 1;
                foreach (var log in rows)
                {
                    string[] values = new string[columns.Count];
                    for (int i = 0; i < columns.Count; i++)
                    {
                        values[i] = GetReportCellText(log, columns[i].Field);
                    }
                    sheetData.Append(CreateReportRow(rowIndex, values, 0));
                    rowIndex++;
                }

                workbookPart.Workbook.Save();
            }
        }

        /// <summary>
        /// 报表样式表（与 ID 绑定窗同规范：格式 0=普通数据行，格式 1=表头加粗居中换行；
        /// count 必须与实际元素数一致，否则 Excel 报"文件损坏"）。
        /// </summary>
        /// <returns>表头样式索引（固定为 1）</returns>
        private static uint CreateReportHeaderFormat(SpreadsheetDocument document)
        {
            WorkbookStylesPart stylesPart = null;
            foreach (var part in document.WorkbookPart.GetPartsOfType<WorkbookStylesPart>())
            {
                stylesPart = part;
                break;
            }
            if (stylesPart == null)
            {
                stylesPart = document.WorkbookPart.AddNewPart<WorkbookStylesPart>();
                stylesPart.Stylesheet = new Stylesheet();
            }
            Stylesheet stylesheet = stylesPart.Stylesheet;

            Fonts fonts = FirstOf<Fonts>(stylesheet);
            if (fonts == null)
            {
                fonts = new Fonts();
                stylesheet.Append(fonts);
            }
            Font normalFont = new Font();
            normalFont.FontSize = new FontSize { Val = 11 };
            normalFont.FontName = new FontName { Val = "微软雅黑" };
            fonts.Append(normalFont);
            Font headerFont = new Font();
            headerFont.Bold = new Bold();
            headerFont.FontSize = new FontSize { Val = 11 };
            headerFont.Color = new Color { Rgb = "000000" };
            headerFont.FontName = new FontName { Val = "微软雅黑" };
            fonts.Append(headerFont);

            Fills fills = FirstOf<Fills>(stylesheet);
            if (fills == null)
            {
                fills = new Fills();
                stylesheet.Append(fills);
            }
            Fill noneFill = new Fill();
            noneFill.PatternFill = new PatternFill { PatternType = PatternValues.None };
            fills.Append(noneFill);

            Borders borders = FirstOf<Borders>(stylesheet);
            if (borders == null)
            {
                borders = new Borders();
                stylesheet.Append(borders);
            }
            borders.Append(new Border());

            CellStyleFormats cellStyleFormats = FirstOf<CellStyleFormats>(stylesheet);
            if (cellStyleFormats == null)
            {
                cellStyleFormats = new CellStyleFormats();
                stylesheet.Append(cellStyleFormats);
            }
            cellStyleFormats.Append(new CellFormat());

            CellFormats cellFormats = FirstOf<CellFormats>(stylesheet);
            if (cellFormats == null)
            {
                cellFormats = new CellFormats();
                stylesheet.Append(cellFormats);
            }
            CellFormat normalFormat = new CellFormat();
            normalFormat.FontId = UInt32Value.FromUInt32(0);
            normalFormat.FillId = UInt32Value.FromUInt32(0);
            normalFormat.ApplyFont = BooleanValue.FromBoolean(true);
            normalFormat.ApplyFill = BooleanValue.FromBoolean(true);
            cellFormats.Append(normalFormat);
            CellFormat headerFormat = new CellFormat();
            headerFormat.FontId = UInt32Value.FromUInt32(1);
            headerFormat.FillId = UInt32Value.FromUInt32(0);
            headerFormat.ApplyFont = BooleanValue.FromBoolean(true);
            headerFormat.ApplyFill = BooleanValue.FromBoolean(true);
            headerFormat.ApplyAlignment = BooleanValue.FromBoolean(true);
            Alignment headerAlignment = new Alignment();
            headerAlignment.Horizontal = HorizontalAlignmentValues.Center;
            headerAlignment.Vertical = VerticalAlignmentValues.Center;
            headerAlignment.WrapText = BooleanValue.FromBoolean(true);
            headerFormat.Alignment = headerAlignment;
            cellFormats.Append(headerFormat);

            fonts.Count = UInt32Value.FromUInt32((uint)fonts.Elements<Font>().Count());
            fills.Count = UInt32Value.FromUInt32((uint)fills.Elements<Fill>().Count());
            borders.Count = UInt32Value.FromUInt32((uint)borders.Elements<Border>().Count());
            cellStyleFormats.Count = UInt32Value.FromUInt32((uint)cellStyleFormats.Elements<CellFormat>().Count());
            cellFormats.Count = UInt32Value.FromUInt32((uint)cellFormats.Elements<CellFormat>().Count());

            stylesheet.Save();
            return 1;
        }

        /// <summary>取样式表里第一个指定类型的元素（没有返回 null；不用 Linq，保持零新依赖习惯）。</summary>
        private static T FirstOf<T>(Stylesheet stylesheet) where T : OpenXmlElement
        {
            foreach (var el in stylesheet.Elements<T>())
            {
                return el;
            }
            return null;
        }

        /// <summary>创建报表行（单元格全按字符串写，时间/数字不转 Excel 原生类型——追溯报表文本即够用）。</summary>
        private static Row CreateReportRow(int rowIndex, string[] values, uint? styleIndex)
        {
            Row row = new Row();
            row.RowIndex = UInt32Value.FromUInt32((uint)(rowIndex + 1));
            for (int i = 0; i < values.Length; i++)
            {
                Cell cell = new Cell
                {
                    CellReference = $"{GetReportColumnName(i)}{rowIndex + 1}",
                    CellValue = new CellValue(values[i] ?? ""),
                    DataType = CellValues.String
                };
                if (styleIndex.HasValue)
                {
                    cell.StyleIndex = UInt32Value.FromUInt32(styleIndex.Value);
                }
                row.Append(cell);
            }
            return row;
        }

        /// <summary>列索引转 Excel 列名（A~Z, AA…，与 ID 绑定窗同函数）。</summary>
        private static string GetReportColumnName(int index)
        {
            string columnName = string.Empty;
            int currentIndex = index;
            while (currentIndex >= 0)
            {
                char c = (char)('A' + (currentIndex % 26));
                columnName = c + columnName;
                currentIndex = currentIndex / 26 - 1;
            }
            return columnName;
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
