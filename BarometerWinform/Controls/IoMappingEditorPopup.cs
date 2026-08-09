using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace BarometerWinform.Controls
{
    /// <summary>
    /// IO 备用通道映射编辑弹出框（供系统设置窗口 IoBackupChannelMappings 配置项使用）：
    /// 以"一行一条映射"的表格展示，每行三列：原 IO 通道 → 新 IO 通道，
    /// 左右两侧为十六进制通道号（0x00~0xFF，可用微调框调整），中间箭头标识对应关系。
    /// 支持直接修改、输入新增、选中删除。
    ///
    /// 【通道号 ↔ 原配置格式】UI 显示的是"通道号"（十六进制，0x00~0xFF），
    /// 配置文件里存的却是"寄存器@位"（如 0x2000@0-&gt;0x2009@10）。
    /// 映射关系：寄存器 = 起始寄存器地址 + (通道号&gt;&gt;4)，位 = 通道号 &amp; 0x0F；
    /// 反推：通道号 = (寄存器 - 起始地址)&lt;&lt;4 | 位。
    /// 点【确定】时逐行换算回"寄存器@位-&gt;寄存器@位"格式，保证与代码其它部分解析一致。
    /// </summary>
    public class IoMappingEditorPopup : Form
    {
        private readonly Sunny.UI.UIDataGridView _dgv;
        private readonly Sunny.UI.UIButton _btnAdd;
        private readonly Sunny.UI.UIButton _btnDelete;
        private readonly Sunny.UI.UIButton _btnOk;
        private readonly Sunny.UI.UIButton _btnCancel;
        private readonly int _baseRegister;
        private bool _closing;

        /// <summary>
        /// 提交后的值（原配置格式，分号分隔），未提交时为 null
        /// </summary>
        public string ResultValue { get; private set; }

        /// <summary>
        /// 构造弹出框
        /// </summary>
        /// <param name="currentValue">当前配置值（如 "0x2000@0->0x2009@10;0x2008@0->0x2009@11"）</param>
        /// <param name="baseRegister">IO 输出寄存器起始地址（IoOutputRegisterStartAddress），用于通道号 ↔ 寄存器换算</param>
        public IoMappingEditorPopup(string currentValue, int baseRegister)
        {
            _baseRegister = baseRegister;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            BackColor = Color.White;
            ClientSize = new Size(440, 268);

            // 映射表格：蓝主题，三列（原通道 / 箭头 / 新通道）
            _dgv = new Sunny.UI.UIDataGridView
            {
                Style = Sunny.UI.UIStyle.Blue,
                Location = new Point(12, 12),
                Size = new Size(416, 158),
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
            _dgv.Columns.Add("colSrc", "原 IO 通道");
            _dgv.Columns.Add("colArrow", "→");
            _dgv.Columns.Add("colDst", "新 IO 通道");
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

            // 原/新通道：十六进制数字微调单元格（0x00~0xFF）
            _dgv.Columns["colSrc"].CellTemplate = new DataGridViewHexNumericUpDownCell { Maximum = 0xFF };
            _dgv.Columns["colDst"].CellTemplate = new DataGridViewHexNumericUpDownCell { Maximum = 0xFF };
            _dgv.Columns["colSrc"].Width = 170;
            _dgv.Columns["colSrc"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            _dgv.Columns["colSrc"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _dgv.Columns["colDst"].Width = 170;
            _dgv.Columns["colDst"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            _dgv.Columns["colDst"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            // 中间箭头列：不可编辑，始终显示 "→"
            _dgv.Columns["colArrow"].Width = 64;
            _dgv.Columns["colArrow"].ReadOnly = true;
            _dgv.Columns["colArrow"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _dgv.Columns["colArrow"].DefaultCellStyle.Font = new Font("微软雅黑", 12F);
            _dgv.Columns["colArrow"].DefaultCellStyle.ForeColor = Color.FromArgb(48, 119, 238);

            // 支持按 Delete 键直接删除选中行
            _dgv.KeyDown += Dgv_KeyDown;
            Controls.Add(_dgv);

            // 操作提示
            var lblHint = new Label
            {
                Text = "通道号为十六进制（0x00~0xFF）：高四位=寄存器偏移，低四位=位号；保存时自动换算回 寄存器@位",
                Location = new Point(12, 176),
                Size = new Size(416, 18),
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

            // 取消 / 确定
            _btnCancel = CreateButton("取消", new Point(282, 198), new Size(62, 30), Sunny.UI.UIStyle.Gray);
            _btnCancel.Click += (s, e) => CloseAsCancel();
            Controls.Add(_btnCancel);

            _btnOk = CreateButton("确定", new Point(344, 198), new Size(84, 30), Sunny.UI.UIStyle.Blue);
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

        /// <summary>把当前配置值解析成一行一条映射填入表格（通道号 = (寄存器-起始地址)<<4 | 位）</summary>
        private void LoadValue(string currentValue)
        {
            _dgv.Rows.Clear();
            if (string.IsNullOrWhiteSpace(currentValue)) return;

            var mappings = BarometerWinform.Models.IoOutputChannelRemap.ParseAll(currentValue, out _);
            foreach (var m in mappings)
            {
                int srcChannel = (m.SourceRegister - _baseRegister) << 4 | m.SourceChannel;
                int dstChannel = (m.TargetRegister - _baseRegister) << 4 | m.TargetChannel;
                _dgv.Rows.Add((decimal)srcChannel, "→", (decimal)dstChannel);
            }
        }

        /// <summary>添加一行空白映射（左右通道留给用户输入）</summary>
        private void AddMapping()
        {
            int rowIdx = _dgv.Rows.Add(null, "→", null);
            _dgv.Rows[rowIdx].Cells["colSrc"].Selected = true;
            _dgv.CurrentCell = _dgv.Rows[rowIdx].Cells["colSrc"];
        }

        /// <summary>表格内按 Delete 键删除选中的行</summary>
        private void Dgv_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                DeleteSelected();
            }
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

                string srcText = row.Cells["colSrc"].Value?.ToString()?.Trim();
                string dstText = row.Cells["colDst"].Value?.ToString()?.Trim();
                if (string.IsNullOrEmpty(srcText) || string.IsNullOrEmpty(dstText)) continue;

                if (!TryParseChannel(srcText, out int srcChannel) || !TryParseChannel(dstText, out int dstChannel))
                {
                    MessageBox.Show(this, "通道号应为 0x00~0xFF 的十六进制数字。", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                int srcReg = _baseRegister + (srcChannel >> 4);
                int srcBit = srcChannel & 0x0F;
                int dstReg = _baseRegister + (dstChannel >> 4);
                int dstBit = dstChannel & 0x0F;

                // 源与目标相同没有意义（没换位置）
                if (srcReg == dstReg && srcBit == dstBit)
                {
                    MessageBox.Show(this, $"第 {row.Index + 1} 行：源通道 0x{srcChannel:X2} 与目标通道相同，请修改。", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                parts.Add($"0x{srcReg:X4}@{srcBit}->0x{dstReg:X4}@{dstBit}");
            }

            ResultValue = string.Join(";", parts);
            _closing = true;
            Close();
        }

        /// <summary>解析通道号（兼容带 0x 前缀与不带，十六进制）</summary>
        private static bool TryParseChannel(string text, out int channel)
        {
            string t = text.Trim();
            if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
                t.StartsWith("0X", StringComparison.OrdinalIgnoreCase))
            {
                t = t.Substring(2);
            }
            if (int.TryParse(t, System.Globalization.NumberStyles.HexNumber, null, out channel))
            {
                return channel >= 0 && channel <= 0xFF;
            }
            channel = 0;
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
