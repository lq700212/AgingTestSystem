
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AgingTestSystem.Services;

namespace AgingTestSystem.Controls
{
    /// <summary>
    /// 显示模式字典编辑弹出框（供系统设置窗口 DisplayModes 配置项使用）：
    /// 以"一行一个选项"的表格展示，支持直接修改、输入新增、选中删除。
    /// 点【确定】校验并提交；点击弹出框外部或【取消】放弃修改。
    ///
    /// 【为什么是列表而不是下拉】字典是"元配置"：下拉选项正是要配的内容本身，
    /// 用固定下拉会循环定义（选项从哪来？）。列表编辑（与 IP 列表弹窗同模式）
    /// 才是正确形态；录入窗的下拉（UIComboBox）读的就是这里配出的字典。
    ///
    /// 【空配置】留空=缺省预设 8 项：打开时表格直接显示预设行，客户在预设上改
    /// （改名/删减/新增），不用从空白想起；全部删光确定=回到留空=预设
    /// （Resolve 兜底，行为一致）。
    ///
    /// 【挂接】SettingsForm.ShowDisplayModesPopup 非模态弹出（定位/越界/主题/
    /// FormClosed 回写+释放与 IP/IO/规则/报表列弹窗同套路，见该方法注释）。
    /// </summary>
    public class DisplayModesEditorPopup : Form
    {
        private readonly Sunny.UI.UIDataGridView _dgv;
        private readonly Sunny.UI.UITextBox _txtNew;
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
        /// <param name="currentValue">当前配置值（逗号分隔，如 "白场,红场"；空=显示缺省预设行）</param>
        public DisplayModesEditorPopup(string currentValue)
        {
            // 高 DPI 三要素（纯代码窗）：设计基准 96DPI + 挂起布局，逐个 Add 不固化错误基准。
            AutoScaleMode = AutoScaleMode.Font;
            AutoScaleDimensions = new SizeF(6F, 12F);
            SuspendLayout();
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            BackColor = Color.White;
            // 与 IP 列表弹窗同尺寸（400×268）：单列表格，一行一个选项
            ClientSize = new Size(400, 268);

            // 选项表格：蓝主题，与设置表格同风格
            _dgv = new Sunny.UI.UIDataGridView
            {
                Style = Sunny.UI.UIStyle.Blue,
                Location = new Point(12, 12),
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
            _dgv.Columns.Add("colMode", "显示模式");
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
                Text = "一行一个画面名，可直接改 / 输入新增 / 选中删除；录入窗下拉读的就是这份名单",
                Location = new Point(12, 176),
                Size = new Size(376, 18),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("微软雅黑", 8.5F),
                ForeColor = Color.FromArgb(130, 138, 150)
            };
            Controls.Add(lblHint);

            // 输入新增：文本框 + 添加按钮
            _txtNew = new Sunny.UI.UITextBox
            {
                Location = new Point(12, 198),
                Size = new Size(270, 30),
                Font = new Font("微软雅黑", 9.5F),
                Watermark = "输入画面名，如 白场",
                MaxLength = DisplayModeOptions.MaxItemLength
            };
            _txtNew.KeyDown += TxtNew_KeyDown;
            Controls.Add(_txtNew);

            _btnAdd = CreateButton("添加", new Point(290, 198), new Size(98, 30), Sunny.UI.UIStyle.Blue);
            _btnAdd.Click += (s, e) => AddMode();
            Controls.Add(_btnAdd);

            // 底部按钮：删除选中（左），取消 / 确定（右）
            _btnDelete = CreateButton("删除选中", new Point(12, 234), new Size(88, 30), Sunny.UI.UIStyle.Orange);
            _btnDelete.Click += (s, e) => DeleteSelected();
            Controls.Add(_btnDelete);

            _btnCancel = CreateButton("取消", new Point(216, 234), new Size(62, 30), Sunny.UI.UIStyle.Gray);
            _btnCancel.Click += (s, e) => CloseAsCancel();
            Controls.Add(_btnCancel);

            _btnOk = CreateButton("确定", new Point(284, 234), new Size(104, 30), Sunny.UI.UIStyle.Blue);
            _btnOk.Click += (s, e) => Confirm();
            Controls.Add(_btnOk);

            LoadValue(currentValue);
            ResumeLayout(false);
        }

        /// <summary>创建 SunnyUI 风格的按钮（与 IP 列表弹窗同口径）</summary>
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

        /// <summary>
        /// 把当前配置值拆分成一行一个选项填入表格。
        /// 空配置=显示缺省预设行（客户在预设上改名/删减/新增，不用从空白想起）。
        /// </summary>
        private void LoadValue(string currentValue)
        {
            _dgv.Rows.Clear();
            List<string> options;
            List<string> errors;
            if (string.IsNullOrWhiteSpace(currentValue))
            {
                options = DisplayModeOptions.Preset();
            }
            else
            {
                DisplayModeOptions.Parse(currentValue, out options, out errors);
            }
            foreach (string o in options)
            {
                _dgv.Rows.Add(o);
            }
        }

        /// <summary>在输入框中输入后回车，等价于点击【添加】</summary>
        private void TxtNew_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                AddMode();
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

        /// <summary>校验输入框中的画面名并追加到表格（超长/重复拦，与 Parse 同规矩）</summary>
        private void AddMode()
        {
            string mode = _txtNew.Text.Trim();
            if (mode.Length == 0)
            {
                _txtNew.Focus();
                return;
            }
            if (mode.Length > DisplayModeOptions.MaxItemLength)
            {
                MessageBox.Show(this, $"画面名太长（≤{DisplayModeOptions.MaxItemLength}字）。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtNew.Focus();
                return;
            }
            foreach (DataGridViewRow r in _dgv.Rows)
            {
                if (r.IsNewRow) continue;
                if (string.Equals((r.Cells[0].Value ?? "").ToString().Trim(), mode,
                    StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(this, "该画面已在名单中。", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _txtNew.Focus();
                    return;
                }
            }
            int rowIdx = _dgv.Rows.Add(mode);
            _dgv.CurrentCell = _dgv.Rows[rowIdx].Cells[0];
            _dgv.Rows[rowIdx].Selected = true;
            _txtNew.Clear();
            _txtNew.Focus();
        }

        /// <summary>删除当前选中的行（先收集索引再删，避免误删，见 IO 映射弹窗同注释）</summary>
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

        /// <summary>
        /// 逐行校验（超长/重复拦，与 Parse 同规矩），全部合法则提交关闭；
        /// 全部删光=空串（=缺省预设，Resolve 兜底，不拦）。
        /// </summary>
        private void Confirm()
        {
            _dgv.EndEdit();

            var modes = new List<string>();
            foreach (DataGridViewRow row in _dgv.Rows)
            {
                if (row.IsNewRow) continue;
                string text = (row.Cells[0].Value ?? "").ToString().Trim();
                if (string.IsNullOrEmpty(text)) continue;

                if (text.Length > DisplayModeOptions.MaxItemLength)
                {
                    MessageBox.Show(this, $"“{text}”太长（≤{DisplayModeOptions.MaxItemLength}字），请修改后再确定。", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _dgv.CurrentCell = row.Cells[0];
                    return;
                }

                bool exists = false;
                foreach (string x in modes)
                {
                    if (string.Equals(x, text, StringComparison.OrdinalIgnoreCase)) { exists = true; break; }
                }
                if (exists)
                {
                    MessageBox.Show(this, $"“{text}”重复了，请删掉一行后再确定。", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _dgv.CurrentCell = row.Cells[0];
                    return;
                }
                modes.Add(text);
            }

            ResultValue = string.Join(",", modes);
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

        /// <summary>无边框窗体，用浅灰描边勾出弹窗边界（与三弹窗同口径）</summary>
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

        /// <summary>点击弹出框外部（窗体失焦）视为取消（与三弹窗同口径）</summary>
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
