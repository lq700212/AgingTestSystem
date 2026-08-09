using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Windows.Forms;

namespace BarometerWinform.Controls
{    /// <summary>
    /// 候选 IP 列表编辑弹出框（供系统设置窗口 FanIpCandidates 配置项使用）：
    /// 在"设置值"单元格下方弹出，以"一行一个 IP"的可编辑表格展示，
    /// 支持直接修改已有 IP、通过输入框添加新 IP、选中删除。
    /// 点【确定】校验并提交；点击弹出框外部或【取消】放弃修改。
    /// </summary>
    public class IpListEditorPopup : Form
    {
        private readonly DataGridView _dgv;
        private readonly TextBox _txtNewIp;
        private readonly Button _btnAdd;
        private readonly Button _btnDelete;
        private readonly Button _btnOk;
        private readonly Button _btnCancel;
        private bool _closing;

        /// <summary>
        /// 提交后的值（逗号分隔），未提交时为 null
        /// </summary>
        public string ResultValue { get; private set; }

        /// <summary>
        /// 构造弹出框
        /// </summary>
        /// <param name="currentValue">当前配置值（逗号分隔，如 "192.168.1.220,192.168.1.221"）</param>
        public IpListEditorPopup(string currentValue)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            BackColor = Color.White;
            ClientSize = new Size(360, 248);

            var lblHint = new Label
            {
                Text = "候选 IP 列表（一行一个，支持修改 / 新增 / 删除）：",
                Location = new Point(10, 8),
                Size = new Size(340, 20),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("微软雅黑", 9F),
                ForeColor = Color.FromArgb(48, 48, 48)
            };
            Controls.Add(lblHint);

            _dgv = new DataGridView
            {
                Location = new Point(10, 30),
                Size = new Size(340, 132),
                BorderStyle = BorderStyle.FixedSingle,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                EditMode = DataGridViewEditMode.EditOnEnter,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowTemplate = { Height = 24 },
                BackgroundColor = Color.White,
                ScrollBars = ScrollBars.Vertical
            };
            _dgv.Columns.Add("colIp", "IP 地址");
            _dgv.DefaultCellStyle.BackColor = Color.White;
            _dgv.DefaultCellStyle.ForeColor = Color.FromArgb(48, 48, 48);
            _dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(48, 119, 238);
            _dgv.DefaultCellStyle.SelectionForeColor = Color.White;
            _dgv.EnableHeadersVisualStyles = false;
            _dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(237, 243, 253);
            _dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(48, 48, 48);
            Controls.Add(_dgv);

            _txtNewIp = new TextBox
            {
                Location = new Point(10, 170),
                Size = new Size(226, 26),
                Font = new Font("微软雅黑", 10F)
            };
            _txtNewIp.KeyDown += TxtNewIp_KeyDown;
            Controls.Add(_txtNewIp);

            _btnAdd = CreateButton("添加", new Point(244, 168), new Size(106, 30));
            _btnAdd.Click += (s, e) => AddIp();
            Controls.Add(_btnAdd);

            _btnDelete = CreateButton("删除选中", new Point(10, 206), new Size(106, 30));
            _btnDelete.Click += (s, e) => DeleteSelected();
            Controls.Add(_btnDelete);

            _btnCancel = CreateButton("取消", new Point(292, 206), new Size(58, 30));
            _btnCancel.Click += (s, e) => CloseAsCancel();
            Controls.Add(_btnCancel);

            _btnOk = CreateButton("确定", new Point(228, 206), new Size(60, 30), true);
            _btnOk.Click += (s, e) => Confirm();
            Controls.Add(_btnOk);

            LoadValue(currentValue);
        }

        private static Button CreateButton(string text, Point location, Size size, bool primary = false)
        {
            return new Button
            {
                Text = text,
                Location = location,
                Size = size,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("微软雅黑", 9F),
                BackColor = primary ? Color.FromArgb(48, 119, 238) : Color.FromArgb(237, 243, 253),
                ForeColor = primary ? Color.White : Color.FromArgb(48, 48, 48),
                FlatAppearance = { BorderColor = Color.FromArgb(200, 214, 240) }
            };
        }

        /// <summary>把当前配置值拆分成一行一个 IP 填入表格</summary>
        private void LoadValue(string currentValue)
        {
            _dgv.Rows.Clear();
            if (string.IsNullOrWhiteSpace(currentValue)) return;

            foreach (string item in currentValue.Split(new[] { ',', ';', '，', '；' }))
            {
                string ip = item?.Trim();
                if (!string.IsNullOrEmpty(ip) && IsValidIp(ip))
                {
                    _dgv.Rows.Add(ip);
                }
            }
        }

        /// <summary>在输入框中输入 IP 后回车，等价于点击【添加】</summary>
        private void TxtNewIp_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                AddIp();
            }
        }

        /// <summary>校验输入框中的 IP 并追加到表格</summary>
        private void AddIp()
        {
            string ip = _txtNewIp.Text.Trim();
            if (!IsValidIp(ip))
            {
                MessageBox.Show(this, "请输入合法的 IPv4 地址，如 192.168.1.220。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtNewIp.Focus();
                return;
            }

            bool exists = _dgv.Rows.Cast<DataGridViewRow>()
                .Any(r => string.Equals(r.Cells[0].Value?.ToString(), ip, StringComparison.OrdinalIgnoreCase));
            if (exists)
            {
                MessageBox.Show(this, "该 IP 已在列表中。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtNewIp.Focus();
                return;
            }

            int rowIdx = _dgv.Rows.Add(ip);
            _dgv.CurrentCell = _dgv.Rows[rowIdx].Cells[0];
            _txtNewIp.Clear();
            _txtNewIp.Focus();
        }

        /// <summary>删除当前选中的行</summary>
        private void DeleteSelected()
        {
            if (_dgv.CurrentCell == null) return;

            _dgv.EndEdit();
            for (int i = _dgv.Rows.Count - 1; i >= 0; i--)
            {
                if (_dgv.Rows[i].Selected)
                {
                    _dgv.Rows.RemoveAt(i);
                }
            }
        }

        /// <summary>逐行校验 IP 格式，全部合法则提交关闭；否则提示留在弹窗</summary>
        private void Confirm()
        {
            _dgv.EndEdit();

            var ips = new List<string>();
            foreach (DataGridViewRow row in _dgv.Rows)
            {
                if (row.IsNewRow) continue;
                string text = row.Cells[0].Value?.ToString()?.Trim();
                if (string.IsNullOrEmpty(text)) continue;

                if (!IsValidIp(text))
                {
                    MessageBox.Show(this, $"“{text}”不是合法的 IPv4 地址，请修改后再确定。", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _dgv.CurrentCell = row.Cells[0];
                    return;
                }

                bool exists = ips.Any(x => string.Equals(x, text, StringComparison.OrdinalIgnoreCase));
                if (!exists) ips.Add(text);
            }

            ResultValue = string.Join(",", ips);
            _closing = true;
            Close();
        }

        /// <summary>取消：放弃修改</summary>
        private void CloseAsCancel()
        {
            ResultValue = null;
            _closing = true;
            Close();
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

        private static bool IsValidIp(string text)
        {
            return !string.IsNullOrWhiteSpace(text)
                && IPAddress.TryParse(text.Trim(), out IPAddress addr)
                && addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
        }
    }

    /// <summary>
    /// 候选 IP 列表单元格：显示逗号分隔的 IP 列表，右侧画一个向下箭头提示可点击。
    /// 点击单元格（在 SettingsForm 的 CellClick 中处理）弹出 <see cref="IpListEditorPopup"/> 编辑，
    /// 单元格本身不可直接编辑（ReadOnly=true），编辑统一走弹窗，避免输入非法 IP。
    /// </summary>
    public class DataGridViewIpListCell : DataGridViewTextBoxCell
    {
        /// <summary>只读单元格：不可直接键入，统一通过弹窗编辑</summary>
        public override bool ReadOnly => true;

        /// <summary>编辑类型返回 null（不进入编辑态），点击由外部弹窗处理</summary>
        public override Type EditType => null;

        /// <summary>在文本右侧绘制一个下拉箭头，提示该项是"点击编辑的下拉列表"</summary>
        protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds,
            int rowIndex, DataGridViewElementStates cellState, object value,
            object formattedValue, string errorText, DataGridViewCellStyle cellStyle,
            DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
        {
            base.Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, value,
                formattedValue, errorText, cellStyle, advancedBorderStyle, paintParts);

            // 只在普通绘制（非选中文字覆盖时）补画箭头
            if ((paintParts & DataGridViewPaintParts.ContentForeground) == 0) return;

            int arrowSize = 5;
            int x = cellBounds.Right - 12;
            int y = cellBounds.Top + (cellBounds.Height - arrowSize) / 2;

            var pen = new Pen(Selected ? Color.White : Color.FromArgb(120, 120, 120), 1.5f);
            try
            {
                Point[] pts =
                {
                    new Point(x, y),
                    new Point(x + arrowSize, y),
                    new Point(x + arrowSize / 2, y + arrowSize - 1)
                };
                graphics.DrawLines(pen, pts);
            }
            finally
            {
                pen.Dispose();
            }
        }
    }
}
