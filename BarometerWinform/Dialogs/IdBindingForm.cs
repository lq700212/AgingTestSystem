using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// ID绑定窗体（业务逻辑部分）
    ///
    /// 【功能说明】
    /// 本窗口用于将工位编号和产品SN与批号进行绑定，实现生产追溯功能。
    /// 用户先输入/扫描工位编号，再输入/扫描产品SN，系统将两者关联并显示在产品列表中，
    /// 点击保存按钮完成绑定操作。
    ///
    /// 【界面布局】
    /// ┌─────────────────────────────────────────────────────────────┐
    /// │ ID绑定                                                     │ ← 标题栏
    /// ├─────────────────────────────────────────────────────────────┤
    /// │ 左侧输入区域                    │ 右侧产品列表区域          │
    /// │ ┌─────────────────────┐        │ ┌─────────────────────┐   │
    /// │ │ 批号：[___________] │        │ │  产品列表            │   │
    /// │ ├─────────────────────┤        │ │                     │   │
    /// │ │ 工位编号：[________] │        │ │ 工位(10)_SN:xxxxx   │   │
    /// │ ├─────────────────────┤        │ │ 工位(11)_SN:xxxxx   │   │
    /// │ │ SN：[______________] │        │ │ ...                 │   │
    /// │ ├─────────────────────┤        │ │                     │   │
    /// │ │ 红色注释区域        │        │ └─────────────────────┘   │
    /// │ │ 绑定顺序说明        │        │                           │
    /// │ └─────────────────────┘        │          [保存]           │
    /// └─────────────────────────────────────────────────────────────┘
    ///
    /// 【绑定顺序】
    /// 1. 先输入/扫描工位编号（手动输入）
    /// 2. 再输入/扫描产品SN（扫码枪输入）
    /// 3. 输入完成后自动将工位编号和SN移入产品列表
    /// 4. 移入前检查是否有重复的工位，如果有进行覆盖操作
    /// 5. 覆盖后清除输入栏的工位编号和SN
    ///
    /// 【数据流转】
    /// 1. 录入批号窗口确定后弹出此窗口，批号自动填充
    /// 2. 用户输入工位编号和SN，点击回车或等待自动检测
    /// 3. 系统验证输入并添加到产品列表
    /// 4. 用户可继续添加更多产品，或点击保存完成绑定
    /// 5. 触发 OnBindingCompleted 事件，通知主窗体绑定完成
    ///
    /// 【输入校验规则】
    /// - 批号：自动从录入批号窗口传入，不可编辑
    /// - 工位编号：不能为空，允许数字和字母
    /// - SN：不能为空，允许数字和字母
    /// - 产品列表：同一工位编号只能绑定一个SN，重复则覆盖
    /// </summary>
    public partial class IdBindingForm : Form
    {
        /// <summary>
        /// 产品绑定信息类
        /// 用于存储工位编号、产品SN以及配方相关信息
        /// </summary>
        public class ProductBinding
        {
            /// <summary>工位编号</summary>
            public string StationNo { get; set; }

            /// <summary>产品SN</summary>
            public string Sn { get; set; }

            /// <summary>配方名称（默认值，可根据实际情况修改）</summary>
            public string RecipeName { get; set; } = "ABCDEFGH";

            /// <summary>延时时间（默认值，格式：时:分:秒）</summary>
            public string DelayTime { get; set; } = "1:10:20";

            /// <summary>启动时间（默认值，格式：时:分:秒）</summary>
            public string StartTime { get; set; } = "2:10:30";

            /// <summary>显示文本（用于列表显示）</summary>
            public string DisplayText => $"工位({StationNo})_SN:{Sn}";
        }

        /// <summary>
        /// 绑定产品列表
        /// 存储已绑定的工位编号和SN信息
        /// </summary>
        private readonly List<ProductBinding> _productBindings;

        /// <summary>
        /// ID绑定完成事件
        /// 当用户点击"保存"按钮并验证通过后触发
        /// 参数：批号、产品绑定列表
        /// </summary>
        public event EventHandler<Tuple<string, List<ProductBinding>>> OnBindingCompleted;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="lotNumber">从录入批号窗口传入的批号</param>
        public IdBindingForm(string lotNumber)
        {
            InitializeComponent();

            // 初始化产品绑定列表
            _productBindings = new List<ProductBinding>();

            // 设置批号（不可编辑）
            txtLot.Text = lotNumber;
            txtLot.ReadOnly = true;
            txtLot.BackColor = System.Drawing.Color.LightGray;
        }

        /// <summary>
        /// 获取当前批号
        /// </summary>
        /// <returns>批号字符串</returns>
        public string GetLotNumber()
        {
            return txtLot.Text.Trim();
        }

        /// <summary>
        /// 获取已绑定的产品列表
        /// </summary>
        /// <returns>产品绑定列表的副本</returns>
        public List<ProductBinding> GetProductBindings()
        {
            return new List<ProductBinding>(_productBindings);
        }

        /// <summary>
        /// 将工位编号和SN添加到产品列表
        /// </summary>
        private void AddToProductList()
        {
            string stationNo = txtStationNo.Text.Trim();
            string sn = txtSn.Text.Trim();

            // 验证工位编号
            if (string.IsNullOrWhiteSpace(stationNo))
            {
                MessageBox.Show("请输入工位编号", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtStationNo.Focus();
                return;
            }

            // 验证SN
            if (string.IsNullOrWhiteSpace(sn))
            {
                MessageBox.Show("请输入产品SN", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtSn.Focus();
                return;
            }

            // 检查是否有重复的工位编号
            int existingIndex = _productBindings.FindIndex(p => p.StationNo == stationNo);
            if (existingIndex >= 0)
            {
                // 重复工位，进行覆盖操作
                DialogResult result = MessageBox.Show(
                    $"工位编号 \"{stationNo}\" 已绑定SN \"{_productBindings[existingIndex].Sn}\"\n" +
                    $"是否覆盖为新的SN \"{sn}\"？",
                    "覆盖确认",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    // 移除旧的绑定
                    _productBindings.RemoveAt(existingIndex);
                }
                else
                {
                    // 用户取消覆盖，清空输入框
                    ClearInputFields();
                    return;
                }
            }

            // 创建新的产品绑定对象
            var binding = new ProductBinding
            {
                StationNo = stationNo,
                Sn = sn
            };

            // 添加到列表
            _productBindings.Add(binding);

            // 更新列表显示
            UpdateProductListDisplay();

            // 清空输入字段
            ClearInputFields();

            // 自动聚焦到工位编号输入框，方便继续输入
            txtStationNo.Focus();

            // 写入调试日志
            System.Diagnostics.Debug.WriteLine(
                $"[ID绑定] 已添加产品绑定: 工位={stationNo}, SN={sn}");
        }

        /// <summary>
        /// 更新产品列表显示
        /// </summary>
        private void UpdateProductListDisplay()
        {
            listBoxProducts.Items.Clear();
            foreach (var binding in _productBindings)
            {
                listBoxProducts.Items.Add(binding.DisplayText);
            }
        }

        /// <summary>
        /// 清空输入字段
        /// </summary>
        private void ClearInputFields()
        {
            txtStationNo.Text = string.Empty;
            txtSn.Text = string.Empty;
        }

        /// <summary>
        /// 保存按钮点击事件
        /// 验证绑定数据、生成Excel文档、触发完成事件
        /// </summary>
        private void btnSave_Click(object sender, EventArgs e)
        {
            // 验证产品列表是否为空
            if (_productBindings.Count == 0)
            {
                MessageBox.Show("请至少绑定一个产品", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 生成Excel文档
            string excelFilePath = GenerateExcelFile();
            if (string.IsNullOrEmpty(excelFilePath))
            {
                // 用户取消保存或保存失败
                return;
            }

            // 触发绑定完成事件（使用System.Tuple避免与OpenXml.Tuple冲突）
            OnBindingCompleted?.Invoke(this, new System.Tuple<string, List<ProductBinding>>(GetLotNumber(), GetProductBindings()));

            // 显示成功提示
            MessageBox.Show(
                $"ID绑定成功！\n\n" +
                $"批号: {GetLotNumber()}\n" +
                $"绑定产品数量: {_productBindings.Count}\n" +
                $"Excel文档已保存至:\n{excelFilePath}",
                "绑定成功",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            // 设置对话框结果并关闭窗口
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        /// <summary>
        /// 生成Excel文档
        /// 
        /// 【文档命名规则】
        /// 格式：批号_日期_时间.xlsx
        /// 示例：KKNVLVLK_20260724_143025.xlsx
        /// 
        /// 【文档内容格式】
        /// ┌──────┬──────┬──────────────┬──────────┬──────────┬──────────┐
        /// │ 批号 │ 工位号│ SN           │ 配方名称 │ 延时时间 │ 启动时间 │
        /// ├──────┼──────┼──────────────┼──────────┼──────────┼──────────┤
        /// │ KKNV │ 1    │ VFJVIJVVEVVW │ ABCDEFGH │ 1:10:20  │ 2:10:30  │
        /// │ KKNV │ 2    │ DFGTRGEWWW   │ ABCDEFGH │ 1:10:20  │ 2:10:30  │
        /// └──────┴──────┴──────────────┴──────────┴──────────┴──────────┘
        /// 
        /// 【返回值】
        /// 返回生成的Excel文件路径，如果用户取消保存则返回null
        /// </summary>
        /// <returns>Excel文件路径</returns>
        private string GenerateExcelFile()
        {
            try
            {
                // 获取当前时间，用于生成文件名
                DateTime now = DateTime.Now;
                string fileName = $"{GetLotNumber()}_{now:yyyyMMdd}_{now:HHmmss}.xlsx";

                // 创建保存文件对话框
                using (SaveFileDialog saveDialog = new SaveFileDialog())
                {
                    saveDialog.Filter = "Excel文件 (*.xlsx)|*.xlsx";
                    saveDialog.FileName = fileName;
                    saveDialog.Title = "保存ID绑定Excel文档";
                    saveDialog.DefaultExt = "xlsx";
                    saveDialog.AddExtension = true;

                    // 显示保存对话框
                    if (saveDialog.ShowDialog(this) != DialogResult.OK)
                    {
                        // 用户取消保存
                        return null;
                    }

                    string filePath = saveDialog.FileName;

                    // 创建Excel文档
                    using (SpreadsheetDocument document = SpreadsheetDocument.Create(
                        filePath, SpreadsheetDocumentType.Workbook))
                    {
                        // 创建工作簿
                        WorkbookPart workbookPart = document.AddWorkbookPart();
                        workbookPart.Workbook = new Workbook();

                        // 创建工作表
                        WorksheetPart worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                        worksheetPart.Worksheet = new Worksheet(new SheetData());

                        // 创建工作表引用
                        Sheets sheets = workbookPart.Workbook.AppendChild(new Sheets());
                        Sheet sheet = new Sheet();
                        sheet.Id = workbookPart.GetIdOfPart(worksheetPart);
                        sheet.SheetId = 1;
                        sheet.Name = "ID绑定数据";
                        sheets.Append(sheet);

                        // 获取工作表数据
                        SheetData sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();

                        // 创建表头样式（加粗、居中）
                        uint headerStyleIndex = CreateHeaderFormat(document);

                        // 创建表头行
                        string[] headers = { "批号", "工位号", "SN", "配方名称", "延时时间", "启动时间" };
                        Row headerRow = CreateRow(0, headers, headerStyleIndex);
                        sheetData.Append(headerRow);

                        // 创建数据行
                        int rowIndex = 1;
                        foreach (var binding in _productBindings)
                        {
                            string[] rowData = {
                                GetLotNumber(),
                                binding.StationNo,
                                binding.Sn,
                                binding.RecipeName,
                                binding.DelayTime,
                                binding.StartTime
                            };
                            Row dataRow = CreateRow(rowIndex, rowData, null);
                            sheetData.Append(dataRow);
                            rowIndex++;
                        }

                        // 保存工作簿
                        workbookPart.Workbook.Save();
                    }

                    // 写入调试日志
                    System.Diagnostics.Debug.WriteLine(
                        $"[ID绑定] Excel文档已生成: {filePath}");

                    return filePath;
                }
            }
            catch (Exception ex)
            {
                // 捕获异常并显示错误信息
                MessageBox.Show(
                    $"生成Excel文档失败：\n{ex.Message}",
                    "错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                // 写入错误日志
                System.Diagnostics.Debug.WriteLine(
                    $"[ID绑定] 生成Excel文档失败: {ex.Message}\n{ex.StackTrace}");

                return null;
            }
        }

        /// <summary>
        /// 创建表头单元格格式（加粗、居中）
        /// </summary>
        /// <param name="document">SpreadsheetDocument对象</param>
        /// <returns>表头样式索引</returns>
        private uint CreateHeaderFormat(SpreadsheetDocument document)
        {
            WorkbookStylesPart stylesPart = document.WorkbookPart.GetPartsOfType<WorkbookStylesPart>().FirstOrDefault();
            if (stylesPart == null)
            {
                stylesPart = document.WorkbookPart.AddNewPart<WorkbookStylesPart>();
                stylesPart.Stylesheet = new Stylesheet();
            }

            Stylesheet stylesheet = stylesPart.Stylesheet;

            Fonts fonts = stylesheet.Elements<Fonts>().FirstOrDefault();
            if (fonts == null)
            {
                fonts = new Fonts();
                stylesheet.Append(fonts);
            }

            Font headerFont = new Font();
            headerFont.Bold = new Bold();
            headerFont.FontSize = new FontSize { Val = 11 };
            headerFont.Color = new Color { Rgb = "000000" };
            headerFont.FontName = new FontName { Val = "微软雅黑" };
            fonts.Append(headerFont);

            Fills fills = stylesheet.Elements<Fills>().FirstOrDefault();
            if (fills == null)
            {
                fills = new Fills();
                stylesheet.Append(fills);
            }

            Fill headerFill = new Fill();
            PatternFill patternFill = new PatternFill();
            patternFill.PatternType = PatternValues.Solid;
            patternFill.ForegroundColor = new ForegroundColor { Rgb = "D9D9D9" };
            headerFill.PatternFill = patternFill;
            fills.Append(headerFill);

            CellFormats cellFormats = stylesheet.Elements<CellFormats>().FirstOrDefault();
            if (cellFormats == null)
            {
                cellFormats = new CellFormats();
                stylesheet.Append(cellFormats);
            }

            uint fontId = (uint)fonts.Elements<Font>().Count();
            uint fillId = (uint)fills.Elements<Fill>().Count();

            CellFormat headerFormat = new CellFormat();
            headerFormat.FontId = UInt32Value.FromUInt32(fontId);
            headerFormat.FillId = UInt32Value.FromUInt32(fillId);
            headerFormat.ApplyFont = BooleanValue.FromBoolean(true);
            headerFormat.ApplyFill = BooleanValue.FromBoolean(true);

            Alignment headerAlignment = new Alignment();
            headerAlignment.Horizontal = HorizontalAlignmentValues.Center;
            headerAlignment.Vertical = VerticalAlignmentValues.Center;
            headerAlignment.WrapText = BooleanValue.FromBoolean(true);
            headerFormat.Alignment = headerAlignment;

            cellFormats.Append(headerFormat);

            fonts.Count = UInt32Value.FromUInt32((uint)fonts.Elements<Font>().Count());
            fills.Count = UInt32Value.FromUInt32((uint)fills.Elements<Fill>().Count());
            cellFormats.Count = UInt32Value.FromUInt32((uint)cellFormats.Elements<CellFormat>().Count());

            stylesheet.Save();

            return (uint)cellFormats.Elements<CellFormat>().Count();
        }

        /// <summary>
        /// 创建Excel行
        /// </summary>
        /// <param name="rowIndex">行索引（从0开始）</param>
        /// <param name="values">单元格值数组</param>
        /// <param name="cellFormat">单元格格式（可为null）</param>
        /// <returns>创建的行对象</returns>
        private Row CreateRow(int rowIndex, string[] values, uint? styleIndex)
        {
            Row row = new Row();
            row.RowIndex = UInt32Value.FromUInt32((uint)(rowIndex + 1));

            for (int i = 0; i < values.Length; i++)
            {
                Cell cell = new Cell
                {
                    CellReference = $"{GetColumnName(i)}{rowIndex + 1}",
                    CellValue = new CellValue(values[i]),
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

        /// <summary>
        /// 将列索引转换为Excel列名（A, B, C, ..., AA, AB, ...）
        /// </summary>
        /// <param name="index">列索引（从0开始）</param>
        /// <returns>列名</returns>
        private string GetColumnName(int index)
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
        /// 工位编号输入框按键事件
        /// 支持按回车键跳转到SN输入框
        /// </summary>
        private void txtStationNo_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                txtSn.Focus();
                e.SuppressKeyPress = true;
            }
        }

        /// <summary>
        /// SN输入框按键事件
        /// 支持按回车键自动添加到产品列表
        /// </summary>
        private void txtSn_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                AddToProductList();
                e.SuppressKeyPress = true;
            }
        }

        /// <summary>
        /// 窗口关闭事件
        /// 如果用户未保存直接关闭，提示确认
        /// </summary>
        private void IdBindingForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_productBindings.Count > 0 && this.DialogResult != DialogResult.OK)
            {
                DialogResult result = MessageBox.Show(
                    "当前有已绑定的产品数据未保存，确定要关闭窗口吗？",
                    "确认关闭",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.No)
                {
                    e.Cancel = true;
                }
            }
        }
    }
}