using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AgingTestSystem.Models;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 配方名称自动完成/模糊搜索辅助类
    /// 在 TextBox 下方弹出 ListBox 展示匹配的配方名称，
    /// 支持键盘导航（Up/Down/Enter）和鼠标选择。
    /// </summary>
    internal class RecipeAutoCompleteProvider : IDisposable, IMessageFilter
    {
        /// <summary>WM_LBUTTONDOWN 消息编号（用于点击列表外区域时收起下拉框）</summary>
        private const int WM_LBUTTONDOWN = 0x201;

        private readonly TextBox _textBox;
        private readonly List<RecipeConfig> _recipes;
        private readonly Action<RecipeConfig> _onRecipeSelected;
        private readonly ListBox _listBox;
        private readonly Timer _debounceTimer;
        private bool _disposed;

        /// <summary>
        /// 最近一次通过下拉列表确认选中的配方名称。
        /// 用于抑制选中回填后再次弹出匹配列表（仅当用户产生新输入时才会重新匹配）。
        /// </summary>
        private string _lastConfirmedName;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="textBox">要附加自动完成功能的 TextBox</param>
        /// <param name="recipes">所有配方列表</param>
        /// <param name="onRecipeSelected">选中配方后的回调</param>
        public RecipeAutoCompleteProvider(TextBox textBox, List<RecipeConfig> recipes, Action<RecipeConfig> onRecipeSelected)
        {
            _textBox = textBox ?? throw new ArgumentNullException(nameof(textBox));
            _recipes = recipes ?? throw new ArgumentNullException(nameof(recipes));
            _onRecipeSelected = onRecipeSelected ?? throw new ArgumentNullException(nameof(onRecipeSelected));

            // 创建弹出 ListBox
            _listBox = new ListBox
            {
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false,
                IntegralHeight = false,
                MaximumSize = new Size(0, 120)
            };
            _listBox.MouseClick += ListBox_MouseClick;
            _listBox.Leave += ListBox_Leave;

            // 将 ListBox 添加到父窗体（而非 TextBox），以便正确定位
            Form parentForm = _textBox.FindForm();
            if (parentForm != null)
            {
                parentForm.Controls.Add(_listBox);
                parentForm.Controls.SetChildIndex(_listBox, 0);
                parentForm.Deactivate += ParentForm_Deactivate;
                parentForm.Resize += ParentForm_Resize;
            }

            // 订阅 TextBox 事件
            _textBox.TextChanged += TextBox_TextChanged;
            _textBox.KeyDown += TextBox_KeyDown;
            _textBox.Leave += TextBox_Leave;

            // 全局消息过滤：点击下拉框和输入框之外的区域时收起下拉框
            Application.AddMessageFilter(this);

            // 防抖定时器
            _debounceTimer = new Timer
            {
                Interval = 300
            };
            _debounceTimer.Tick += DebounceTimer_Tick;
        }

        /// <summary>
        /// 显示匹配的配方列表
        /// </summary>
        private void ShowDropdown()
        {
            string input = _textBox.Text;

            List<RecipeConfig> matches;

            if (string.IsNullOrEmpty(input))
            {
                matches = _recipes;
            }
            else
            {
                matches = _recipes
                    .Where(r => r.Name.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }

            _listBox.Items.Clear();
            foreach (RecipeConfig recipe in matches)
            {
                _listBox.Items.Add(recipe);
            }

            _listBox.DisplayMember = "Name";

            if (matches.Count == 0)
            {
                _listBox.Visible = false;
                return;
            }

            // 计算 ListBox 位置
            Point locationBelowTextBox = new Point(_textBox.Left, _textBox.Bottom);
            _listBox.Left = locationBelowTextBox.X;
            _listBox.Top = locationBelowTextBox.Y;
            _listBox.Width = _textBox.Width;

            // 计算高度（每一项约 13px，加上边框）
            int preferredHeight = (matches.Count * 13) + 4;
            if (preferredHeight > 120)
            {
                preferredHeight = 120;
            }
            _listBox.Height = preferredHeight;

            _listBox.SelectedIndex = -1;
            _listBox.Visible = true;
        }

        /// <summary>
        /// 全局消息过滤器：在下拉框显示期间，用户点击下拉框和输入框之外的任何区域
        /// （包括窗体空白处、面板、其他控件等）时收起下拉框。
        /// 不吞掉消息，点击仍正常传递到目标控件。
        /// </summary>
        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WM_LBUTTONDOWN || _disposed || !_listBox.Visible)
            {
                return false;
            }

            Point screenPos = Control.MousePosition;
            Rectangle listScreen = _listBox.RectangleToScreen(_listBox.ClientRectangle);
            Rectangle textScreen = _textBox.RectangleToScreen(_textBox.ClientRectangle);

            if (!listScreen.Contains(screenPos) && !textScreen.Contains(screenPos))
            {
                HideDropdown();
            }

            return false;
        }

        /// <summary>
        /// 隐藏下拉弹窗
        /// </summary>
        private void HideDropdown()
        {
            _listBox.Visible = false;
        }

        /// <summary>
        /// 确认当前选中项
        /// </summary>
        private void ConfirmSelection()
        {
            if (_listBox.SelectedItem is RecipeConfig selected)
            {
                _lastConfirmedName = selected.Name;
                _textBox.Text = selected.Name;
                _textBox.SelectionStart = _textBox.Text.Length;
                _textBox.SelectionLength = 0;
                HideDropdown();
                _onRecipeSelected(selected);
            }
        }

        // ──────────────── 事件处理 ────────────────

        private void TextBox_TextChanged(object sender, EventArgs e)
        {
            // 用户刚通过下拉列表选中配方并回填名称，此变化不视为新输入，
            // 不要重新弹出匹配列表；只有当用户产生新的输入时才继续匹配。
            if (string.Equals(_textBox.Text, _lastConfirmedName, StringComparison.Ordinal))
            {
                return;
            }

            // 重启防抖定时器
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private void DebounceTimer_Tick(object sender, EventArgs e)
        {
            _debounceTimer.Stop();
            ShowDropdown();
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (!_listBox.Visible)
            {
                return;
            }

            if (e.KeyCode == Keys.Down)
            {
                if (_listBox.SelectedIndex < _listBox.Items.Count - 1)
                {
                    _listBox.SelectedIndex++;
                }
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                if (_listBox.SelectedIndex > 0)
                {
                    _listBox.SelectedIndex--;
                }
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                if (_listBox.SelectedItem != null)
                {
                    ConfirmSelection();
                }
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                HideDropdown();
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
        }

        private void TextBox_Leave(object sender, EventArgs e)
        {
            // 延迟隐藏，让鼠标点击 ListBox 有机会触发
            _debounceTimer.Stop();
            Timer delayHide = new Timer
            {
                Interval = 200
            };
            delayHide.Tick += (s, args) =>
            {
                delayHide.Stop();
                delayHide.Dispose();
                if (!_listBox.Focused && !_textBox.Focused)
                {
                    // 如果下拉框已被隐藏（如用户已通过鼠标选中列表项），无需处理
                    if (!_listBox.Visible) return;

                    // 点击非匹配列表区域 → 仅隐藏下拉框，文本框内容保持不变（视为未选择）
                    HideDropdown();
                }
            };
            delayHide.Start();
        }

        /// <summary>
        /// ListBox 失去焦点时（用户点击了窗体空白区域或其他控件），延迟检查后仅隐藏下拉框
        /// </summary>
        private void ListBox_Leave(object sender, EventArgs e)
        {
            if (!_listBox.Visible) return;

            Timer delayHide = new Timer
            {
                Interval = 100
            };
            delayHide.Tick += (s, args) =>
            {
                delayHide.Stop();
                delayHide.Dispose();
                if (!_listBox.Visible) return;
                if (!_textBox.Focused)
                {
                    // 点击非匹配列表区域 → 仅隐藏下拉框，文本框内容保持不变（视为未选择）
                    HideDropdown();
                }
            };
            delayHide.Start();
        }

        private void ListBox_MouseClick(object sender, MouseEventArgs e)
        {
            int index = _listBox.IndexFromPoint(e.Location);
            if (index >= 0 && index < _listBox.Items.Count)
            {
                _listBox.SelectedIndex = index;
                ConfirmSelection();
            }
        }

        private void ParentForm_Deactivate(object sender, EventArgs e)
        {
            HideDropdown();
        }

        private void ParentForm_Resize(object sender, EventArgs e)
        {
            HideDropdown();
        }

        // ──────────────── IDisposable ────────────────

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _debounceTimer.Stop();
                _debounceTimer.Dispose();

                Application.RemoveMessageFilter(this);

                _textBox.TextChanged -= TextBox_TextChanged;
                _textBox.KeyDown -= TextBox_KeyDown;
                _textBox.Leave -= TextBox_Leave;

                _listBox.MouseClick -= ListBox_MouseClick;
                _listBox.Leave -= ListBox_Leave;

                Form parentForm = _textBox.FindForm();
                if (parentForm != null)
                {
                    parentForm.Deactivate -= ParentForm_Deactivate;
                    parentForm.Resize -= ParentForm_Resize;
                    parentForm.Controls.Remove(_listBox);
                }

                _listBox.Dispose();
            }

            _disposed = true;
        }
    }
}