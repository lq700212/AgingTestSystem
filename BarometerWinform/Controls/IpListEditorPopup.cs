using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Windows.Forms;

namespace BarometerWinform.Controls
{
    /// <summary>
    /// 候选 IP 列表编辑弹出框（供系统设置窗口 FanIpCandidates 配置项使用）：
    /// 在"设置值"单元格下方弹出，以"一行一个 IP"的可编辑表格展示，
    /// 支持直接修改已有 IP、通过输入框添加新 IP、选中删除。
    /// 点【确定】校验并提交；点击弹出框外部或【取消】放弃修改。
    /// 界面风格与系统设置窗口一致（SunnyUI 蓝主题 + 白底）。
    /// </summary>
    public class IpListEditorPopup : Form
    {
        private readonly Sunny.UI.UIDataGridView _dgv;
        private readonly Sunny.UI.UITextBox _txtNewIp;
        private readonly Sunny.UI.UIButton _btnAdd;
        private readonly Sunny.UI.UIButton _btnDelete;
        private readonly Sunny.UI.UIButton _btnOk;
        private readonly Sunny.UI.UIButton _btnCancel;
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
            ClientSize = new Size(400, 306);

            // 顶部蓝色标题栏（与 SunnyUI 蓝主题一致）
            var header = new Sunny.UI.UIPanel
            {
                Dock = DockStyle.Top,
                Height = 40,
                FillColor = Color.FromArgb(48, 119, 238),
                RectColor = Color.FromArgb(48, 119, 238),
                Style = Sunny.UI.UIStyle.Custom
            };
            var lblTitle = new Label
            {
                Text = "候选 IP 列表编辑",
                Location = new Point(12, 0),
                Size = new Size(240, 40),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("微软雅黑", 11F, FontStyle.Bold),
                ForeColor = Color.White
            };
            var lblHeaderHint = new Label
            {
                Text = "可修改 / 新增 / 删除",
                Location = new Point(252, 0),
                Size = new Size(140, 40),
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("微软雅黑", 9F),
                ForeColor = Color.FromArgb(220, 228, 245)
            };
            header.Controls.Add(lblTitle);
            header.Controls.Add(lblHeaderHint);
            Controls.Add(header);

            // 候选 IP 表格：蓝主题，与设置表格同风格
            _dgv = new Sunny.UI.UIDataGridView
            {
                Style = Sunny.UI.UIStyle.Blue,
                Location = new Point(12, 50),
                Size = new Size(376, 158),
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                MultiSelect = true,
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
            _dgv.DefaultCellStyle.Font = new Font("微软雅黑", 9.5F);
            _dgv.EnableHeadersVisualStyles = false;
            _dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(237, 243, 253);
            _dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(48, 48, 48);
            _dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(237, 243, 253);
            _dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.FromArgb(48, 48, 48);
            _dgv.ColumnHeadersDefaultCellStyle.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
            _dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            // 支持按 Delete 键直接删除选中行
            _dgv.KeyDown += Dgv_KeyDown;
            Controls.Add(_dgv);

            // 操作提示
            var lblHint = new Label
            {
                Text = "一行一个 IP，可直接修改 / 输入新增 / 选中删除",
                Location = new Point(12, 214),
                Size = new Size(376, 18),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("微软雅黑", 8.5F),
                ForeColor = Color.FromArgb(130, 138, 150)
            };
            Controls.Add(lblHint);

            // 输入新增：文本框 + 添加按钮
            _txtNewIp = new Sunny.UI.UITextBox
            {
                Location = new Point(12, 236),
                Size = new Size(270, 30),
                Font = new Font("微软雅黑", 9.5F),
                Watermark = "输入 IP 地址，如 192.168.1.220"
            };
            _txtNewIp.KeyDown += TxtNewIp_KeyDown;
            Controls.Add(_txtNewIp);

            _btnAdd = CreateButton("添加", new Point(290, 236), new Size(98, 30), Sunny.UI.UIStyle.Blue);
            _btnAdd.Click += (s, e) => AddIp();
            Controls.Add(_btnAdd);

            // 底部按钮：删除选中（左），取消 / 确定（右）
            _btnDelete = CreateButton("删除选中", new Point(12, 272), new Size(88, 30), Sunny.UI.UIStyle.Orange);
            _btnDelete.Click += (s, e) => DeleteSelected();
            Controls.Add(_btnDelete);

            _btnCancel = CreateButton("取消", new Point(216, 272), new Size(62, 30), Sunny.UI.UIStyle.Gray);
            _btnCancel.Click += (s, e) => CloseAsCancel();
            Controls.Add(_btnCancel);

            _btnOk = CreateButton("确定", new Point(284, 272), new Size(104, 30), Sunny.UI.UIStyle.Blue);
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

        /// <summary>表格内按 Delete 键删除选中的行</summary>
        private void Dgv_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                DeleteSelected();
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
            _dgv.Rows[rowIdx].Selected = true;
            _txtNewIp.Clear();
            _txtNewIp.Focus();
        }

        /// <summary>删除当前选中的行。
        /// 先收集要删除的行索引再统一删除，避免删除当前行时
        /// DataGridView 自动重选"锚点→当前行"区间导致误删全部行。</summary>
        private void DeleteSelected()
        {
            _dgv.EndEdit();

            // 先记下选中的行索引，再进行删除
            var indices = new List<int>();
            for (int i = 0; i < _dgv.Rows.Count; i++)
            {
                if (_dgv.Rows[i].Selected) indices.Add(i);
            }
            if (indices.Count == 0) return;

            // 倒序删除，索引不受影响
            indices.Sort();
            for (int i = indices.Count - 1; i >= 0; i--)
            {
                _dgv.Rows.RemoveAt(indices[i]);
            }

            // 删除后清除自动选区，避免残留整列选中
            _dgv.ClearSelection();
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
