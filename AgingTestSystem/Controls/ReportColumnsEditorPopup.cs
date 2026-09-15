
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AgingTestSystem.Services;

namespace AgingTestSystem.Controls
{
    /// <summary>
    /// 报表列配置编辑弹出框（供系统设置窗口 ReportColumns 配置项使用）：
    /// 以"一行一列"的表格展示，每行两格：
    ///   显示名（文本，Excel 列头） | 字段（下拉，报表数据源 8 选 1）
    /// 支持添加 / 删除选中 / 上移 / 下移，确定时校验组装回"显示名=字段"格式。
    ///
    /// 【与配置格式的对应】配置里存的即是"显示名=字段"（如 时间=time;批号=lot），
    /// 界面显示与存储一一对应（所见即所得）；字段下拉只给 8 个可用字段
    /// （ReportColumns.AvailableFields），未知字段选不进来。
    /// 【空配置】留空=缺省预设 8 列：打开时表格直接显示预设行，客户在预设上改，
    /// 不用从空白想起；全部删光确定=回到留空=预设（Resolve 兜底，行为一致）。
    ///
    /// 【挂接】SettingsForm.ShowReportColumnsPopup 非模态弹出（定位/越界/主题/
    /// FormClosed 回写+释放与 IP/IO/规则三弹窗同套路，见该方法注释）。
    /// </summary>
    public class ReportColumnsEditorPopup : Form
    {
        private readonly Sunny.UI.UIDataGridView _dgv;
        private readonly Sunny.UI.UIButton _btnAdd;
        private readonly Sunny.UI.UIButton _btnDelete;
        private readonly Sunny.UI.UIButton _btnUp;
        private readonly Sunny.UI.UIButton _btnDown;
        private readonly Sunny.UI.UIButton _btnOk;
        private readonly Sunny.UI.UIButton _btnCancel;
        private bool _closing;

        /// <summary>
        /// 提交后的值（"显示名=字段"分号分隔；全删光=空串=缺省预设），未提交时为 null
        /// </summary>
        public string ResultValue { get; private set; }

        /// <summary>
        /// 构造弹出框
        /// </summary>
        /// <param name="currentValue">当前配置值（如 "时间=time;批号=lot"；空=显示缺省预设行）</param>
        public ReportColumnsEditorPopup(string currentValue)
        {
            // 高 DPI 三要素（纯代码窗）：设计基准 96DPI + 挂起布局，逐个 Add 不固化错误基准。
            AutoScaleMode = AutoScaleMode.Font;
            AutoScaleDimensions = new SizeF(6F, 12F);
            SuspendLayout();
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            BackColor = Color.White;
            // 弹窗整体尺寸：表格 536×160 + 提示行 + 按钮行（左4钮右2钮），
            // 与 IO 映射弹窗同算法：列宽 显示名280+字段240=520，表格宽 536（余 16px 防裁切+滚动条）。
            ClientSize = new Size(560, 280);

            // 列配置表格：蓝主题，两列（显示名文本 / 字段下拉）
            _dgv = new Sunny.UI.UIDataGridView
            {
                Style = Sunny.UI.UIStyle.Blue,
                Location = new Point(12, 12),
                Size = new Size(536, 160),
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                EditMode = DataGridViewEditMode.EditOnEnter,
                RowTemplate = { Height = 26 },
                BackgroundColor = Color.White,
                ScrollBars = ScrollBars.Vertical
            };
            var colDisplay = new DataGridViewTextBoxColumn
            {
                Name = "colDisplay",
                HeaderText = "显示名（Excel 列头）",
                Width = 280,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                MaxInputLength = 20
            };
            colDisplay.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            _dgv.Columns.Add(colDisplay);
            var colField = new DataGridViewComboBoxColumn
            {
                Name = "colField",
                HeaderText = "字段（报表数据源）",
                Width = 240,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            };
            colField.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            foreach (string f in ReportColumns.AvailableFields) colField.Items.Add(f);
            _dgv.Columns.Add(colField);
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
                Text = "一行一列：显示名是 Excel 列头（可改名），字段只能下拉 8 选 1；上下移调整列顺序",
                Location = new Point(12, 178),
                Size = new Size(536, 18),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("微软雅黑", 8.5F),
                ForeColor = Color.FromArgb(130, 138, 150)
            };
            Controls.Add(lblHint);

            // 添加 / 删除 / 上移 / 下移
            _btnAdd = CreateButton("添加列", new Point(12, 202), new Size(84, 30), Sunny.UI.UIStyle.Blue);
            _btnAdd.Click += (s, e) => AddColumn();
            Controls.Add(_btnAdd);

            _btnDelete = CreateButton("删除选中", new Point(104, 202), new Size(88, 30), Sunny.UI.UIStyle.Orange);
            _btnDelete.Click += (s, e) => DeleteSelected();
            Controls.Add(_btnDelete);

            _btnUp = CreateButton("上移", new Point(200, 202), new Size(64, 30), Sunny.UI.UIStyle.Gray);
            _btnUp.Click += (s, e) => MoveSelected(-1);
            Controls.Add(_btnUp);

            _btnDown = CreateButton("下移", new Point(272, 202), new Size(64, 30), Sunny.UI.UIStyle.Gray);
            _btnDown.Click += (s, e) => MoveSelected(1);
            Controls.Add(_btnDown);

            // 取消 / 确定（靠右：取消 X=560-12-72-8-74=394，确定 X=560-12-74=474）
            _btnCancel = CreateButton("取消", new Point(394, 202), new Size(72, 30), Sunny.UI.UIStyle.Gray);
            _btnCancel.Click += (s, e) => CloseAsCancel();
            Controls.Add(_btnCancel);

            _btnOk = CreateButton("确定", new Point(474, 202), new Size(74, 30), Sunny.UI.UIStyle.Blue);
            _btnOk.Click += (s, e) => Confirm();
            Controls.Add(_btnOk);

            LoadValue(currentValue);
            ResumeLayout(false);
        }

        /// <summary>创建 SunnyUI 风格的按钮（与 IO 映射弹窗同口径）</summary>
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
        /// 把当前配置值解析成一行一列填入表格。
        /// 空配置=显示缺省预设行（客户在预设上改，不用从空白想起）；
        /// 脏组（未知字段）解析层已丢弃，这里只填干净行。
        /// </summary>
        private void LoadValue(string currentValue)
        {
            _dgv.Rows.Clear();
            List<ReportColumns.Column> cols;
            List<string> errors;
            if (string.IsNullOrWhiteSpace(currentValue))
            {
                ReportColumns.Parse(ReportColumns.DefaultPreset, out cols, out errors);
            }
            else
            {
                ReportColumns.Parse(currentValue, out cols, out errors);
            }
            foreach (var c in cols)
            {
                _dgv.Rows.Add(c.Display, c.Field);
            }
        }

        /// <summary>添加一列（显示名空着让用户填，字段默认 time，选中新行即改）</summary>
        private void AddColumn()
        {
            int rowIdx = _dgv.Rows.Add("", "time");
            _dgv.ClearSelection();
            _dgv.Rows[rowIdx].Selected = true;
            _dgv.CurrentCell = _dgv.Rows[rowIdx].Cells["colDisplay"];
            _dgv.BeginEdit(true);
        }

        /// <summary>表格内按 Delete 键删除选中的行</summary>
        private void Dgv_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                DeleteSelected();
            }
        }

        /// <summary>删除当前选中的行（单选模式，只删一行）</summary>
        private void DeleteSelected()
        {
            _dgv.EndEdit();
            if (_dgv.SelectedRows.Count == 0) return;
            _dgv.Rows.RemoveAt(_dgv.SelectedRows[0].Index);
            _dgv.ClearSelection();
        }

        /// <summary>上移/下移选中行（到顶/到底不动；只换两格的值，选中跟随）</summary>
        /// <param name="delta">-1=上移，+1=下移</param>
        private void MoveSelected(int delta)
        {
            _dgv.EndEdit();
            if (_dgv.SelectedRows.Count == 0) return;
            int idx = _dgv.SelectedRows[0].Index;
            int target = idx + delta;
            if (target < 0 || target >= _dgv.Rows.Count) return;
            foreach (string col in new string[] { "colDisplay", "colField" })
            {
                object tmp = _dgv.Rows[idx].Cells[col].Value;
                _dgv.Rows[idx].Cells[col].Value = _dgv.Rows[target].Cells[col].Value;
                _dgv.Rows[target].Cells[col].Value = tmp;
            }
            _dgv.ClearSelection();
            _dgv.Rows[target].Selected = true;
            _dgv.CurrentCell = _dgv.Rows[target].Cells["colDisplay"];
        }

        /// <summary>
        /// 逐行校验并组装回"显示名=字段"格式，合法则提交关闭。
        /// - 显示名空 → 报第几行，拦；
        /// - 显示名重复 → 报哪两个，拦（与 Parse 同规矩）；
        /// - 字段归一到 8 个可用之一（大小写无所谓），未命中拦；
        /// - 全部删光 → 空串（=缺省预设，Resolve 兜底，行为一致，不拦）。
        /// </summary>
        private void Confirm()
        {
            _dgv.EndEdit();

            var parts = new List<string>();
            var seen = new List<string>();
            foreach (DataGridViewRow row in _dgv.Rows)
            {
                if (row.IsNewRow) continue;
                string display = (row.Cells["colDisplay"].Value ?? "").ToString().Trim();
                object fieldRaw = row.Cells["colField"].Value;
                string field = (fieldRaw ?? "").ToString().Trim();
                if (display.Length == 0)
                {
                    MessageBox.Show(this, $"第 {row.Index + 1} 行显示名是空的：填上 Excel 列头，或删掉该行。",
                        "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _dgv.CurrentCell = row.Cells["colDisplay"];
                    return;
                }
                if (display.Contains(" ") || display.Contains("=") || display.Contains(";"))
                {
                    MessageBox.Show(this, $"第 {row.Index + 1} 行显示名含空格/= /分号，Excel 列头不支持，请改名。",
                        "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _dgv.CurrentCell = row.Cells["colDisplay"];
                    return;
                }
                bool dup = false;
                foreach (string s in seen)
                {
                    if (string.Equals(s, display, StringComparison.Ordinal)) { dup = true; break; }
                }
                if (dup)
                {
                    MessageBox.Show(this, $"显示名“{display}”重复了，两列同名导出会串列，请改名。",
                        "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _dgv.CurrentCell = row.Cells["colDisplay"];
                    return;
                }
                seen.Add(display);
                // 字段列默认可手输（DataGridViewComboBoxColumn 无 DropDownList 锁），
                // 这里按 AvailableFields 归一：命中存规范小写，未命中拦（与 Parse 同规矩）。
                string canonical = null;
                foreach (string v in ReportColumns.AvailableFields)
                {
                    if (string.Equals(v, field, StringComparison.OrdinalIgnoreCase)) { canonical = v; break; }
                }
                if (canonical == null)
                {
                    MessageBox.Show(this, $"第 {row.Index + 1} 行字段“{field}”不在数据源里，" +
                        "请从下拉 8 选 1（time/lot/device/event/detail/pressure/temp/current）。",
                        "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _dgv.CurrentCell = row.Cells["colField"];
                    return;
                }
                parts.Add(display + "=" + canonical);
            }

            ResultValue = string.Join(";", parts);
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

        /// <summary>无边框窗体，用浅灰描边勾出弹窗边界（与 IO 映射弹窗同口径）</summary>
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
