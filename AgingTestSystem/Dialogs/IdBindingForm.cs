using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AgingTestSystem.Services;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AgingTestSystem.Dialogs
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
    /// 1. 扫码枪扫"工位号"（恰好 2 位数字，如 01~72，自动识别为工位号）
    /// 2. 扫码枪扫"产品SN"（一般不止 2 位，自动识别为SN）
    /// 3. 工位号和SN 都扫齐后，自动将两者移入产品列表（不要求固定先后顺序）
    /// 4. 移入前检查是否有重复的工位，如果有进行覆盖确认操作
    /// 5. 移入后清除输入栏的工位编号和SN
    ///
    /// 【防错机制（V1.16 更新）】
    /// 扫码枪扫出的条码通过格式自动区分是"工位号"还是"产品SN"：
    /// 恰好 2 位数字 → 工位号；其他内容 → 产品SN。
    /// 因此即使工人没按"先工位号、后SN"的顺序扫，系统也能正确配对录入。
    ///
    /// 【数据流转】
    /// 1. 录入批号窗口确定后弹出此窗口，批号自动填充
    /// 2. 扫码枪扫码 → 自动识别工位号/SN → 填入对应输入框
    /// 3. 两条都齐后系统自动验证输入并添加到产品列表
    /// 4. 用户可继续添加更多产品，或点击保存完成绑定
    /// 5. 触发 OnBindingCompleted 事件，通知主窗体绑定完成
    ///
    /// 【输入校验规则】
    /// - 批号：自动从录入批号窗口传入，不可编辑
    /// - 工位编号：扫码枪自动识别（恰好 2 位数字）或手动输入，不能为空
    /// - SN：扫码枪自动识别（非 2 位数字）或手动输入，不能为空
    /// - 产品列表：同一工位编号只能绑定一个SN，重复则覆盖确认
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
        /// 【V1.16 新增】扫码枪服务引用
        /// 由录入批号窗体（InputLotForm）传入。
        /// 【V1.16 更新】扫码枪扫到条码时自动识别是"工位号"还是"产品SN"并填入对应输入框：
        /// - 恰好 2 位数字（如 01~72）→ 判断为工位号 → 填入"工位编号"输入框
        /// - 其他内容（产品SN一般不止 2 位）→ 判断为产品SN → 填入"SN"输入框
        /// 工位号和 SN 都填齐后自动加入产品列表（等效按回车）。
        /// 通过格式自动区分，即使乱序扫码也能正确配对。
        /// 可能为 null（未启用扫码枪），使用前需要判空。
        /// </summary>
        private readonly ScannerService _scanner;

        /// <summary>
        /// 【V1.19.11 新增】设备管理器引用
        /// 由录入批号窗体（InputLotForm）传入（可能为 null）。
        /// 【用途】绑定保存时，把每个"工位 → SN"的对应关系写入设备管理器
        /// 的工位静态信息，使工位面板的 SN 显示与绑定结果关联一致。
        /// 即使不启用扫码枪、纯手动输入工位号+SN，保存后同样生效。
        /// </summary>
        private readonly DeviceManager _deviceManager;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="lotNumber">从录入批号窗口传入的批号</param>
        /// <param name="scanner">扫码枪服务（由录入批号窗体传入，可能为 null）</param>
        /// <param name="deviceManager">设备管理器（V1.19.11 新增，可能为 null；用于绑定后把 SN 关联到工位）</param>
        public IdBindingForm(string lotNumber, ScannerService scanner = null, DeviceManager deviceManager = null)
        {
            InitializeComponent();

            // 初始化产品绑定列表
            _productBindings = new List<ProductBinding>();

            // 设置批号（不可编辑）
            txtLot.Text = lotNumber;
            txtLot.ReadOnly = true;
            txtLot.BackColor = System.Drawing.Color.LightGray;

            // 【V1.16】启用扫码枪时订阅扫码完成事件，实现 SN 自动填充
            // 注意：扫码事件已在 UI 线程触发（ScannerService 内部已封送），可直接更新控件
            _scanner = scanner;
            if (_scanner != null)
            {
                _scanner.OnBarcodeScanned += Scanner_OnBarcodeScanned;
            }

            // 【V1.19.11】保存设备管理器引用（绑定完成后把 SN 关联到对应工位）
            _deviceManager = deviceManager;
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

        // ============ 扫码枪工位号/SN 自动识别填充（V1.16 新增，V1.16 更新支持工位号） ============

        /// <summary>
        /// 扫码完成事件处理（已由 ScannerService 封送到 UI 线程，可直接操作控件）
        /// 把扫到的条码按格式自动识别为"工位号"或"产品SN"并填入对应输入框
        /// </summary>
        /// <param name="sender">扫码枪服务</param>
        /// <param name="barcode">扫到的条码内容</param>
        private void Scanner_OnBarcodeScanned(object sender, string barcode)
        {
            HandleScannedBarcode(barcode);
        }

        /// <summary>
        /// 处理扫码结果（自动区分工位号 / 产品SN，两条都齐后自动加入产品列表）
        ///
        /// 【条码识别规则】（V1.16 防错机制）
        /// 扫码枪扫到的条码可能是"工位号"或"产品SN"，通过格式自动区分：
        /// - 恰好 2 位数字（现场实测 01~72）→ 判断为工位号 → 填入"工位编号"输入框
        /// - 其他内容（产品SN一般不止 2 位）→ 判断为产品SN → 填入"SN"输入框
        ///
        /// 【防错效果】
        /// 规范顺序是"先扫工位号、再扫产品SN"，但工人可能没按顺序扫。
        /// 由于工位号/SN 能按"恰好 2 位数字"自动区分，无论先扫哪一条：
        /// 两条都齐后，自动把工位号和产品SN 一起移入产品列表（等效按回车），
        /// 中间不需要人工判断当前扫的是哪一类，实现"乱序也能正常录入"。
        ///
        /// 【处理流程】
        /// 1. 扫码 → 判断是工位号还是产品SN → 填入对应输入框
        /// 2. 工位号和 SN 都齐了 → 自动调用 AddToProductList()
        /// 3. AddToProductList 内部：检查重复工位号（重复则确认覆盖）→
        ///    加入产品列表 → 清空工位号/SN 输入框 → 聚焦工位编号
        /// </summary>
        /// <param name="barcode">扫到的条码内容</param>
        private void HandleScannedBarcode(string barcode)
        {
            // 空条码直接忽略（理论上不会发生，防御性判断）
            if (string.IsNullOrWhiteSpace(barcode)) return;

            // 统一去掉首尾空白
            string value = barcode.Trim();

            // 写入调试日志，方便现场排查
            System.Diagnostics.Debug.WriteLine($"[ID绑定] 扫码枪读码: {value}");

            // 自动区分条码类型并填入对应输入框：
            // 恰好 2 位数字 → 工位号；其他内容 → 产品SN
            if (IsStationNumber(value))
            {
                txtStationNo.Text = value;
            }
            else
            {
                txtSn.Text = value;
            }

            // 工位号和 SN 都齐了 → 自动加入产品列表（等效用户按回车）
            // 不区分先后顺序：先扫工位号或先扫SN 都能正确配对
            if (!string.IsNullOrWhiteSpace(txtStationNo.Text) &&
                !string.IsNullOrWhiteSpace(txtSn.Text))
            {
                AddToProductList();
            }
        }

        /// <summary>
        /// 判断条码是否为工位号
        ///
        /// 【规则】恰好 2 位且都是数字（如 "01"~"72"）→ 工位号
        /// 产品SN一般不止 2 位，因此可用"恰好 2 位数字"作为判别依据。
        /// </summary>
        /// <param name="value">扫码内容（已去除首尾空白）</param>
        /// <returns>是工位号返回 true，否则返回 false</returns>
        private static bool IsStationNumber(string value)
        {
            // 长度必须恰好 2 位，且两位都是数字
            return value != null &&
                   value.Length == 2 &&
                   char.IsDigit(value[0]) &&
                   char.IsDigit(value[1]);
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
        /// 【V1.19.11】保存时把"工位 → SN"写入设备管理器工位静态信息，
        /// 使工位面板的 SN 显示与绑定关联一致（扫码枪扫码或手动输入均可）。
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

            // 【V1.19.11】把绑定的 SN 关联到对应工位
            // 纯手动输入（未启用扫码枪）同样生效：工位编号 + SN 已录入列表即可。
            // 未传设备管理器（null）时跳过，不影响 Excel 导出。
            ApplyBindingsToStations();

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
        /// 把已绑定的"工位 → SN"写入设备管理器工位静态信息（【V1.19.11 新增】）
        ///
        /// 【说明】
        /// - 遍历产品绑定列表，把每个工位编号对应的 SN 通过 DeviceManager 保存，
        ///   采集线程下次叠加后，工位面板的 SN 标签即显示绑定后的 SN。
        /// - 工位编号按 int 解析（"01" → 1）；解析失败或不在 1~72 范围自动忽略。
        /// - 未传入设备管理器（_deviceManager == null）时不操作。
        /// </summary>
        private void ApplyBindingsToStations()
        {
            if (_deviceManager == null) return;

            var serialNumbers = new Dictionary<int, string>();
            foreach (var binding in _productBindings)
            {
                if (int.TryParse(binding.StationNo?.Trim(), out int stationNo))
                {
                    serialNumbers[stationNo] = binding.Sn;
                }
            }
            _deviceManager.SetStationSerialNumbers(serialNumbers);

            System.Diagnostics.Debug.WriteLine(
                $"[ID绑定] 已把 {serialNumbers.Count} 个工位的 SN 关联到设备管理器");
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
                        // 数据行使用样式索引 0（普通样式：不加粗、无灰底），
                        // 只有表头（列名）使用样式索引 1（加粗 + 灰底）。
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
                            Row dataRow = CreateRow(rowIndex, rowData, 0);
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
        /// 创建样式表（表头加粗居中换行，数据行普通样式）
        ///
        /// 【修复说明】原实现只建了 1 个"加粗"格式并返回 Count()=1，导致：
        /// - 表头行引用样式索引 1（实际只有索引 0，索引 1 不存在）
        /// - 数据行不指定样式 → 默认命中索引 0，恰好是那个"加粗"格式
        /// 结果是"内容加粗、列名没加粗"，与预期相反。
        /// 现按 OOXML 规范建完整样式表，两个格式：
        /// - 格式 0：普通样式（数据行用，不加粗、无填充）
        /// - 格式 1：表头样式（列名用，加粗 + 居中换行，不加灰底）
        /// </summary>
        /// <param name="document">SpreadsheetDocument对象</param>
        /// <returns>表头样式索引（固定为 1）</returns>
        private uint CreateHeaderFormat(SpreadsheetDocument document)
        {
            // 获取或创建工作簿样式部件（存放 字体/填充/边框/单元格格式 的容器）
            WorkbookStylesPart stylesPart = document.WorkbookPart.GetPartsOfType<WorkbookStylesPart>().FirstOrDefault();
            if (stylesPart == null)
            {
                stylesPart = document.WorkbookPart.AddNewPart<WorkbookStylesPart>();
                stylesPart.Stylesheet = new Stylesheet();
            }

            Stylesheet stylesheet = stylesPart.Stylesheet;

            // ==================== 字体（Fonts） ====================
            // 字体索引0：普通字体（数据行用，不加粗）
            Fonts fonts = stylesheet.Elements<Fonts>().FirstOrDefault();
            if (fonts == null)
            {
                fonts = new Fonts();
                stylesheet.Append(fonts);
            }

            Font normalFont = new Font();
            normalFont.FontSize = new FontSize { Val = 11 };
            normalFont.FontName = new FontName { Val = "微软雅黑" };
            fonts.Append(normalFont);

            // 字体索引1：加粗字体（表头列名用）
            Font headerFont = new Font();
            headerFont.Bold = new Bold();
            headerFont.FontSize = new FontSize { Val = 11 };
            headerFont.Color = new Color { Rgb = "000000" };
            headerFont.FontName = new FontName { Val = "微软雅黑" };
            fonts.Append(headerFont);

            // ==================== 填充（Fills） ====================
            // 填充索引0：无填充（OOXML 规范要求索引0为"无图案"，数据行和表头都用它，
            // 表头不加灰底，保持白色背景，只靠加粗和居中换行来区分列名）
            Fills fills = stylesheet.Elements<Fills>().FirstOrDefault();
            if (fills == null)
            {
                fills = new Fills();
                stylesheet.Append(fills);
            }

            Fill noneFill = new Fill();
            PatternFill nonePattern = new PatternFill { PatternType = PatternValues.None };
            noneFill.PatternFill = nonePattern;
            fills.Append(noneFill);

            // ==================== 边框（Borders，OOXML 要求至少1个） ====================
            // 边框索引0：无边框（整表都不画边框）
            Borders borders = stylesheet.Elements<Borders>().FirstOrDefault();
            if (borders == null)
            {
                borders = new Borders();
                stylesheet.Append(borders);
            }
            borders.Append(new Border());

            // ==================== 单元格样式引用（cellStyleXfs，OOXML 要求至少1个） ====================
            // 索引0：默认单元格样式
            CellStyleFormats cellStyleFormats = stylesheet.Elements<CellStyleFormats>().FirstOrDefault();
            if (cellStyleFormats == null)
            {
                cellStyleFormats = new CellStyleFormats();
                stylesheet.Append(cellStyleFormats);
            }
            cellStyleFormats.Append(new CellFormat());

            // ==================== 单元格格式（CellFormats / cellXfs，实际生效） ====================
            CellFormats cellFormats = stylesheet.Elements<CellFormats>().FirstOrDefault();
            if (cellFormats == null)
            {
                cellFormats = new CellFormats();
                stylesheet.Append(cellFormats);
            }

            // 格式索引0：普通格式（数据行用：不加粗、无灰底）
            CellFormat normalFormat = new CellFormat();
            normalFormat.FontId = UInt32Value.FromUInt32(0);   // 指向普通字体
            normalFormat.FillId = UInt32Value.FromUInt32(0);   // 指向无填充
            normalFormat.ApplyFont = BooleanValue.FromBoolean(true);
            normalFormat.ApplyFill = BooleanValue.FromBoolean(true);
            cellFormats.Append(normalFormat);

            // 格式索引1：表头格式（列名用：加粗 + 居中换行，不加灰底）
            CellFormat headerFormat = new CellFormat();
            headerFormat.FontId = UInt32Value.FromUInt32(1);   // 指向加粗字体
            headerFormat.FillId = UInt32Value.FromUInt32(0);   // 指向无填充（不加灰底）
            headerFormat.ApplyFont = BooleanValue.FromBoolean(true);
            headerFormat.ApplyFill = BooleanValue.FromBoolean(true);
            headerFormat.ApplyAlignment = BooleanValue.FromBoolean(true);

            Alignment headerAlignment = new Alignment();
            headerAlignment.Horizontal = HorizontalAlignmentValues.Center;
            headerAlignment.Vertical = VerticalAlignmentValues.Center;
            headerAlignment.WrapText = BooleanValue.FromBoolean(true);
            headerFormat.Alignment = headerAlignment;

            cellFormats.Append(headerFormat);

            // ==================== 统计数量（OOXML 要求 count 与实际元素数一致） ====================
            fonts.Count = UInt32Value.FromUInt32((uint)fonts.Elements<Font>().Count());
            fills.Count = UInt32Value.FromUInt32((uint)fills.Elements<Fill>().Count());
            borders.Count = UInt32Value.FromUInt32((uint)borders.Elements<Border>().Count());
            cellStyleFormats.Count = UInt32Value.FromUInt32((uint)cellStyleFormats.Elements<CellFormat>().Count());
            cellFormats.Count = UInt32Value.FromUInt32((uint)cellFormats.Elements<CellFormat>().Count());

            stylesheet.Save();

            // 表头样式索引固定为 1（格式0=普通，格式1=表头）
            return 1;
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

        /// <summary>
        /// 窗体已关闭事件（重写）
        /// 【V1.16】窗体确定关闭时退订扫码事件，避免窗体销毁后扫码事件
        /// 还回调到已释放的控件（使用 FormClosed 而非 FormClosing：
        /// FormClosing 可能被用户"取消"而退订过早，导致窗体还在但收不到扫码）。
        /// </summary>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);

            // 退订扫码事件，防止事件回调到已销毁的窗体
            if (_scanner != null)
            {
                _scanner.OnBarcodeScanned -= Scanner_OnBarcodeScanned;
            }
        }
    }
}