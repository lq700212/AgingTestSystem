using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AgingTestSystem.Controls
{
    /// <summary>
    /// IO 备用通道映射编辑弹出框（供系统设置窗口 IoBackupChannelMappings 配置项使用）：
    /// 以"一行一条映射"的表格展示，每行四列输入 + 中间箭头：
    ///   原寄存器 0x2000 | 原通道 0x00 → 新寄存器 0x2009 | 新通道 0x10
    /// 寄存器地址（0x0000~0xFFFF）与通道号（0x00~0x1F）均为十六进制，可微调。
    /// 支持直接修改、添加新行、选中删除。
    ///
    /// 【与配置格式的对应】配置里存的即是"寄存器@通道"（如 0x2000@0x00->0x2009@0x10），
    /// 与界面显示完全一致（所见即所得），无需换算；十六进制通道由 IoOutputChannelRemap
    /// 在解析时统一转成十进制位号（0~31）供位运算使用。
    /// </summary>
    public class IoMappingEditorPopup : Form
    {
        private readonly Sunny.UI.UIDataGridView _dgv;
        private readonly Sunny.UI.UIButton _btnAdd;
        private readonly Sunny.UI.UIButton _btnDelete;
        private readonly Sunny.UI.UIButton _btnOk;
        private readonly Sunny.UI.UIButton _btnCancel;
        private bool _closing;

        /// <summary>
        /// 提交后的值（原配置格式，分号分隔），未提交时为 null
        /// </summary>
        public string ResultValue { get; private set; }

        /// <summary>
        /// 构造弹出框
        /// </summary>
        /// <param name="currentValue">当前配置值（如 "0x2000@0x00->0x2009@0x10;0x2008@0x00->0x2009@0x11"）</param>
        public IoMappingEditorPopup(string currentValue)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            BackColor = Color.White;
            // 弹窗整体尺寸：【V1.54b】从 560×268 调到 640×268——
            // 原 560 时表格 5 列总宽 148+92+56+148+92=536 等于 dgv.Width，最右侧列微调按钮
            // 被裁切。现列宽 172+104+60+172+104=612，表格宽 616（多 4px 余量保证不裁切），
            // 弹窗宽 640（=616+左右各 12 边距）。
            ClientSize = new Size(640, 268);

            // 映射表格：蓝主题，五列（原寄存器/原通道/箭头/新寄存器/新通道）
            _dgv = new Sunny.UI.UIDataGridView
            {
                Style = Sunny.UI.UIStyle.Blue,
                Location = new Point(12, 12),
                Size = new Size(616, 158),
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                MultiSelect = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                EditMode = DataGridViewEditMode.EditOnEnter,
                RowTemplate = { Height = 26 },
                BackgroundColor = Color.White,
                ScrollBars = ScrollBars.Vertical
            };
            _dgv.Columns.Add("colSrcReg", "原寄存器");
            _dgv.Columns.Add("colSrcCh", "原通道");
            _dgv.Columns.Add("colArrow", "→");
            _dgv.Columns.Add("colDstReg", "新寄存器");
            _dgv.Columns.Add("colDstCh", "新通道");
            _dgv.DefaultCellStyle.BackColor = Color.White;
            _dgv.DefaultCellStyle.ForeColor = Color.FromArgb(48, 48, 48);
            _dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(48, 119, 238);
            _dgv.DefaultCellStyle.SelectionForeColor = Color.White;
            _dgv.DefaultCellStyle.Font = new Font("微软雅黑", 9.5F);
            _dgv.EnableHeadersVisualStyles = false;
            _dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(237, 243, 253);
            _dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(48, 48, 48);
            _dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(237, 243, 253);
            _dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.FromArgb(48, 48, 48);
            _dgv.ColumnHeadersDefaultCellStyle.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
            _dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            // 原/新寄存器：十六进制微调框（0x0000~0xFFFF，4 位 + 0x 前缀）
            _dgv.Columns["colSrcReg"].CellTemplate = new DataGridViewHexNumericUpDownCell
            {
                Maximum = 0xFFFF,
                HexDigits = 4,
                ShowPrefix = true
            };
            _dgv.Columns["colDstReg"].CellTemplate = new DataGridViewHexNumericUpDownCell
            {
                Maximum = 0xFFFF,
                HexDigits = 4,
                ShowPrefix = true
            };
            // 原/新通道：十六进制微调框（0x00~0x1F，兼容 32 点/模块），与寄存器一样带 0x 前缀
            _dgv.Columns["colSrcCh"].CellTemplate = new DataGridViewHexNumericUpDownCell { Maximum = 0x1F, ShowPrefix = true };
            _dgv.Columns["colDstCh"].CellTemplate = new DataGridViewHexNumericUpDownCell { Maximum = 0x1F, ShowPrefix = true };

            // 列宽分配：【V1.54b】整体放大——寄存器列 148→172、通道列 92→104、箭头列 56→60，
            // 总宽 172+104+60+172+104=612，表格宽 616（多 4px 余量保证不裁切）。
            // 放大原因：十六进制微调框右侧有上下调按钮，最右侧一列"新通道"内容（0x00~0x1F）
            // 在原列宽下被部分遮挡、调节按钮也按不到。
            _dgv.Columns["colSrcReg"].Width = 172;
            _dgv.Columns["colSrcReg"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            _dgv.Columns["colSrcReg"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _dgv.Columns["colSrcCh"].Width = 104;
            _dgv.Columns["colSrcCh"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            _dgv.Columns["colSrcCh"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _dgv.Columns["colDstReg"].Width = 172;
            _dgv.Columns["colDstReg"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            _dgv.Columns["colDstReg"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _dgv.Columns["colDstCh"].Width = 104;
            _dgv.Columns["colDstCh"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            _dgv.Columns["colDstCh"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            // 中间箭头列：不可编辑，始终显示 "→"（12 号加粗、黑色、居中）
            _dgv.Columns["colArrow"].Width = 60;
            _dgv.Columns["colArrow"].ReadOnly = true;
            _dgv.Columns["colArrow"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            // 加粗：与表头"→"（自绘 12F,Bold）像素级一致；微软雅黑"→"是细线条字符，
            // Regular 视觉上偏纤细，统一 Bold 后表头与数据行箭头粗细完全相同。
            _dgv.Columns["colArrow"].DefaultCellStyle.Font = new Font("微软雅黑", 12F, FontStyle.Bold);
            _dgv.Columns["colArrow"].DefaultCellStyle.ForeColor = Color.FromArgb(48, 48, 48);

            // 表头箭头与数据行箭头样式保持一致（同字号/同色/同居中、**加粗**）：
            // V1.54 首发版表头用 12F Regular、数据行 12F Regular，视觉上仍有差异（"→"是细线条字符，
            // 微软雅黑 12F Regular 本身偏纤细，与下方数据箭头放一起看会显"轻"；与表头其他列
            // 9F Bold 标题放一起又显"弱"）。用户反馈"和下面还是有点差别"，**统一加粗**最稳——
            // 数据行与表头都用 12F Bold，标题与正文箭头像素级一致；
            // 自绘实现（_dgv.CellPainting → Dgv_CellPainting），彻底绕开 HeaderCell.Style 继承。
            _dgv.CellPainting += Dgv_CellPainting;

            // 支持按 Delete 键直接删除选中行
            _dgv.KeyDown += Dgv_KeyDown;
            Controls.Add(_dgv);

            // 操作提示
            var lblHint = new Label
            {
                Text = "寄存器（0x0000~0xFFFF）与通道（0x00~0x1F）均为十六进制；保存后配置与界面显示一致，无需换算",
                Location = new Point(12, 176),
                Size = new Size(616, 18),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("微软雅黑", 8.5F),
                ForeColor = Color.FromArgb(130, 138, 150)
            };
            Controls.Add(lblHint);

            // 添加 / 删除
            _btnAdd = CreateButton("添加映射", new Point(12, 198), new Size(98, 30), Sunny.UI.UIStyle.Blue);
            _btnAdd.Click += (s, e) => AddMapping();
            Controls.Add(_btnAdd);

            _btnDelete = CreateButton("删除选中", new Point(118, 198), new Size(88, 30), Sunny.UI.UIStyle.Orange);
            _btnDelete.Click += (s, e) => DeleteSelected();
            Controls.Add(_btnDelete);

            // 取消 / 确定（【V1.54b】弹窗宽 640，按钮靠右：取消 X=640-12-152=476，确定 X=640-12-82=546）
            _btnCancel = CreateButton("取消", new Point(476, 198), new Size(62, 30), Sunny.UI.UIStyle.Gray);
            _btnCancel.Click += (s, e) => CloseAsCancel();
            Controls.Add(_btnCancel);

            _btnOk = CreateButton("确定", new Point(546, 198), new Size(82, 30), Sunny.UI.UIStyle.Blue);
            _btnOk.Click += (s, e) => Confirm();
            Controls.Add(_btnOk);

            LoadValue(currentValue);
        }

        /// <summary>创建 SunnyUI 风格的按钮（按样式区分主色）</summary>
        private static Sunny.UI.UIButton CreateButton(string text, Point location, Size size, Sunny.UI.UIStyle style)
        {
            var btn = new Sunny.UI.UIButton
            {
                Text = text,
                Location = location,
                Size = size,
                Style = style,
                Font = new Font("微软雅黑", 9F)
            };
            return btn;
        }

        /// <summary>把当前配置值解析成一行一条映射填入表格（寄存器保持十六进制显示，位号显示为十六进制）</summary>
        private void LoadValue(string currentValue)
        {
            _dgv.Rows.Clear();
            if (string.IsNullOrWhiteSpace(currentValue)) return;

            var mappings = AgingTestSystem.Models.IoOutputChannelRemap.ParseAll(currentValue, out _);
            foreach (var m in mappings)
            {
                _dgv.Rows.Add(
                    (decimal)m.SourceRegister, (decimal)m.SourceChannel,
                    "→",
                    (decimal)m.TargetRegister, (decimal)m.TargetChannel);
            }
        }

        /// <summary>添加一行空白映射（四格留给用户输入）</summary>
        private void AddMapping()
        {
            int rowIdx = _dgv.Rows.Add(null, null, "→", null, null);
            _dgv.Rows[rowIdx].Cells["colSrcReg"].Selected = true;
            _dgv.CurrentCell = _dgv.Rows[rowIdx].Cells["colSrcReg"];
        }

        /// <summary>表格内按 Delete 键删除选中的行</summary>
        private void Dgv_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                DeleteSelected();
            }
        }

        /// <summary>
        /// 自绘箭头列表头：与数据行箭头"→"完全一致的样式（微软雅黑 12F、**加粗**、黑色 48,48,48、居中）。
        /// 这里必须手绘，因为 HeaderCell.Style 在 SunnyUI UIDataGridView 下会被
        /// ColumnHeadersDefaultCellStyle（9 号加粗、左对齐）覆盖，无法靠子属性赋值让它与数据行一致；
        /// 自绘则完全可控——把 HeaderBackground/Background 走默认，再用 TextRenderer 以
        /// 与数据行 DataGridView 走 GDI+ 一致的居中格式画出"→"，最终表头与数据行像素级一致。
        /// </summary>
        private void Dgv_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex != -1) return;                    // 只处理表头
            if (e.ColumnIndex < 0) return;
            if (_dgv.Columns[e.ColumnIndex].Name != "colArrow") return;

            // 走一次默认绘制（含 HeaderBackground/边框/选择态等），随后我们在其上覆写文本
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background
                | DataGridViewPaintParts.Border
                | DataGridViewPaintParts.SelectionBackground);

            // 文本绘制参数与数据列保持完全一致：
            //   字体：微软雅黑 12F、FontStyle.Bold（与数据箭头 DefaultCellStyle.Font 一致）
            //   颜色：48,48,48（与数据箭头 ForeColor 一致）
            //   居中：DataGridViewContentAlignment.MiddleCenter
            // 走 GDI（TextRenderer），与 WinForms DataGridView 默认文本绘制路径一致，
            // 不会出现 GDI+ 与 GDI 混用导致的文字模糊/位置偏移。
            var font = new Font("微软雅黑", 12F, FontStyle.Bold);
            try
            {
                TextRenderer.DrawText(
                    e.Graphics,
                    "→",
                    font,
                    e.CellBounds,
                    Color.FromArgb(48, 48, 48),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }
            finally
            {
                font.Dispose();
            }

            // 通知 DataGridView 这一格已由我们完整绘制，避免系统再次覆绘内容
            e.Handled = true;
        }

        /// <summary>删除当前选中的行（先收集索引再删，避免删除当前行时误删全部）</summary>
        private void DeleteSelected()
        {
            _dgv.EndEdit();

            var indices = new List<int>();
            for (int i = 0; i < _dgv.Rows.Count; i++)
            {
                if (_dgv.Rows[i].Selected) indices.Add(i);
            }
            if (indices.Count == 0) return;

            indices.Sort();
            for (int i = indices.Count - 1; i >= 0; i--)
            {
                _dgv.Rows.RemoveAt(indices[i]);
            }

            _dgv.ClearSelection();
        }

        /// <summary>逐行校验并换算回"寄存器@位->寄存器@位"格式，合法则提交关闭</summary>
        private void Confirm()
        {
            _dgv.EndEdit();

            var parts = new List<string>();
            foreach (DataGridViewRow row in _dgv.Rows)
            {
                if (row.IsNewRow) continue;

                if (!TryGetDecimal(row, "colSrcReg", out decimal srcRegDec) ||
                    !TryGetDecimal(row, "colSrcCh", out decimal srcChDec) ||
                    !TryGetDecimal(row, "colDstReg", out decimal dstRegDec) ||
                    !TryGetDecimal(row, "colDstCh", out decimal dstChDec))
                {
                    continue;
                }

                int srcReg = (int)srcRegDec;
                int srcCh = (int)srcChDec;
                int dstReg = (int)dstRegDec;
                int dstCh = (int)dstChDec;

                // 源与目标相同没有意义（没换位置）
                if (srcReg == dstReg && srcCh == dstCh)
                {
                    MessageBox.Show(this, $"第 {row.Index + 1} 行：源（0x{srcReg:X4}@{srcCh:X2}）与目标相同，请修改。", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // 寄存器与通道均以十六进制输出，与界面显示完全一致（所见即所得）；
                // 十六进制通道由 IoOutputChannelRemap 解析时转成十进制位号（0~31）
                parts.Add($"0x{srcReg:X4}@0x{srcCh:X2}->0x{dstReg:X4}@0x{dstCh:X2}");
            }

            ResultValue = string.Join(";", parts);
            _closing = true;
            Close();
        }

        /// <summary>取某行某列的值；空值返回 false（跳过未填写的行）</summary>
        private static bool TryGetDecimal(DataGridViewRow row, string column, out decimal value)
        {
            object raw = row.Cells[column].Value;
            if (raw is decimal d)
            {
                value = d;
                return true;
            }
            value = 0;
            return false;
        }

        /// <summary>取消：放弃修改</summary>
        private void CloseAsCancel()
        {
            ResultValue = null;
            _closing = true;
            Close();
        }

        /// <summary>无边框窗体，用浅灰描边勾出弹窗边界，与表格底框视觉统一</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Color.FromArgb(214, 220, 228)))
            {
                Rectangle rect = ClientRectangle;
                rect.Width -= 1;
                rect.Height -= 1;
                e.Graphics.DrawRectangle(pen, rect);
            }
        }

        /// <summary>点击弹出框外部（窗体失焦）视为取消</summary>
        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (!_closing)
            {
                ResultValue = null;
                _closing = true;
                Close();
            }
        }
    }
}
