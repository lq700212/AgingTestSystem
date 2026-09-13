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
    /// 在输入框下方弹出 ListBox 展示匹配的配方名称，
    /// 支持键盘导航（Up/Down/Enter）和鼠标选择。
    /// 【V1.71】输入框泛化为 Control：原生 TextBox 与 SunnyUI UITextBox 通吃
    /// （用到的全是 Control 级成员；光标定位两行走反射，两边同名属性）。
    /// </summary>
    internal class RecipeAutoCompleteProvider : IDisposable, IMessageFilter
    {
        /// <summary>WM_LBUTTONDOWN 消息编号（用于点击列表外区域时收起下拉框）</summary>
        private const int WM_LBUTTONDOWN = 0x201;

        private readonly Control _textBox;
        private readonly List<RecipeConfig> _recipes;
        private readonly Action<RecipeConfig> _onRecipeSelected;
        private readonly ListBox _listBox;
        private readonly Timer _debounceTimer;
        private bool _disposed;
        /// <summary>
        /// 实际挂接的父窗体（【大扫荡】构造期 FindForm() 可能为 null，挂接动作推迟到
        /// ShowDropdown；记下来 Dispose 时精准摘事件，不依赖 Dispose 时再 FindForm）。
        /// </summary>
        private Form _parentForm;

        /// <summary>
        /// 待触发的延迟隐藏定时器（V1.72.15 新增，全仓关窗竞态排查：TextBox_Leave/ListBox_Leave
        /// 建的 200ms/100ms 一次性 Timer 若在窗体关闭后才到点，Tick 直碰已释放 _listBox/_textBox
        /// 即炸。集中登记，Dispose 时统一 Stop+Dispose；Tick 入口先查 _disposed 再碰控件）。
        /// </summary>
        private readonly List<Timer> _pendingHideTimers = new List<Timer>();

        /// <summary>
        /// 最近一次通过下拉列表确认选中的配方名称。
        /// 用于抑制选中回填后再次弹出匹配列表（仅当用户产生新输入时才会重新匹配）。
        /// </summary>
        private string _lastConfirmedName;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="textBox">要附加自动完成功能的输入框（原生 TextBox / SunnyUI UITextBox 均可）</param>
        /// <param name="recipes">所有配方列表</param>
        /// <param name="onRecipeSelected">选中配方后的回调</param>
        public RecipeAutoCompleteProvider(Control textBox, List<RecipeConfig> recipes, Action<RecipeConfig> onRecipeSelected)
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

            // 将 ListBox 添加到父窗体（而非 TextBox），以便正确定位。
            // 【大扫荡】构造期 FindForm() 可能为 null（窗体还没建完就挂接）：
            // 这里挂不上不报错，ShowDropdown 时重找再挂（见 ShowDropdown）。
            AttachToParentForm();

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
        /// 把下拉框挂到文本框所在父窗体（构造调一次 + 每次 Show 前补调）。
        /// 构造期窗体未建完 FindForm() 为 null 时不报错，等 Show 时再挂。
        /// </summary>
        private void AttachToParentForm()
        {
            try
            {
                if (_parentForm != null && !_parentForm.IsDisposed) return;   // 已挂好
                if (_textBox == null || _textBox.IsDisposed) return;
                Form parentForm = _textBox.FindForm();
                if (parentForm == null || parentForm.IsDisposed) return;
                if (_listBox.Parent != parentForm)
                {
                    parentForm.Controls.Add(_listBox);
                    parentForm.Controls.SetChildIndex(_listBox, 0);
                }
                if (_parentForm != parentForm)
                {
                    parentForm.Deactivate += ParentForm_Deactivate;
                    parentForm.Resize += ParentForm_Resize;
                    _parentForm = parentForm;
                }
            }
            catch { /* 挂不上等下次 Show，绝不炸构造 */ }
        }

        /// <summary>
        /// 显示匹配的配方列表（V1.72.15：释放后静默丢弃）。
        /// </summary>
        private void ShowDropdown()
        {
            try
            {
                if (_disposed) return;
                if (_textBox == null || _textBox.IsDisposed) return;
                if (_listBox == null || _listBox.IsDisposed) return;
                // 构造期没挂上父窗的，这里补挂；还挂不上（无父窗）直接返回。
                AttachToParentForm();
                if (_listBox.Parent == null) return;
            string input = _textBox.Text;

            List<RecipeConfig> matches;

            if (string.IsNullOrEmpty(input))
            {
                // 空输入=显示全部（脏项照样过滤，免得空行占位）。
                matches = _recipes.Where(r => r != null && r.Name != null).ToList();
            }
            else
            {
                // 【大扫荡】空名/脏项跳过：以前 r.Name 为 null 直接 NRE，被外层 catch 吞，
                // 下拉永不弹且无日志（配了脏配方连正常联想一起死）。
                matches = _recipes
                    .Where(r => r != null && r.Name != null
                        && r.Name.IndexOf(input, StringComparison.OrdinalIgnoreCase) >= 0)
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

            // 计算 ListBox 位置（【大扫荡】坐标系修正：_listBox 挂在父窗体上，
            // 以前直接用 _textBox.Left/Bottom（相对其父容器坐标）定位，
            // 文本框在 Panel/GroupBox 里即错位。现在经屏幕坐标换算到父窗客户区）。
            Point screenBelow = (_textBox.Parent != null ? _textBox.Parent : _textBox)
                .PointToScreen(new Point(_textBox.Left, _textBox.Bottom));
            Form host = _listBox.Parent as Form;
            Point locationBelowTextBox = host != null
                ? host.PointToClient(screenBelow)
                : new Point(_textBox.Left, _textBox.Bottom);
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
            catch { }
        }

        /// <summary>
        /// 全局消息过滤器：在下拉框显示期间，用户点击下拉框和输入框之外的任何区域
        /// （包括窗体空白处、面板、其他控件等）时收起下拉框。
        /// 不吞掉消息，点击仍正常传递到目标控件。
        /// </summary>
        public bool PreFilterMessage(ref Message m)
        {
            try
            {
                if (m.Msg != WM_LBUTTONDOWN || _disposed) return false;
                if (_listBox == null || _listBox.IsDisposed || !_listBox.Visible) return false;
                if (_textBox == null || _textBox.IsDisposed) return false;

            Point screenPos = Control.MousePosition;
            Rectangle listScreen = _listBox.RectangleToScreen(_listBox.ClientRectangle);
            Rectangle textScreen = _textBox.RectangleToScreen(_textBox.ClientRectangle);

            if (!listScreen.Contains(screenPos) && !textScreen.Contains(screenPos))
            {
                HideDropdown();
            }

            return false;
            }
            catch { return false; }
        }

        /// <summary>
        /// 隐藏下拉弹窗（V1.72.15：关后静默丢弃，防延迟 Tick 在释放后碰句柄）。
        /// </summary>
        private void HideDropdown()
        {
            try
            {
                if (_disposed) return;
                if (_listBox == null || _listBox.IsDisposed) return;
                _listBox.Visible = false;
            }
            catch { }
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
                SetCaretToEnd(_textBox);
                HideDropdown();
                _onRecipeSelected(selected);
            }
        }

        /// <summary>
        /// 光标移到末尾（原生 TextBox 与 SunnyUI UITextBox 同名属性，反射一次调用，
        /// 不给 Services 层引入 Sunny 依赖；失败静默——光标位置不影响功能正确性）。
        /// </summary>
        private static void SetCaretToEnd(Control c)
        {
            try
            {
                var p1 = c.GetType().GetProperty("SelectionStart");
                var p2 = c.GetType().GetProperty("SelectionLength");
                if (p1 == null || p2 == null || !p1.CanWrite || !p2.CanWrite) return;
                p1.SetValue(c, c.Text.Length, null);
                p2.SetValue(c, 0, null);
            }
            catch { }
        }

        // ──────────────── 事件处理 ────────────────

        private void TextBox_TextChanged(object sender, EventArgs e)
        {
            if (_disposed) return;
            try
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
            catch { }
        }

        private void DebounceTimer_Tick(object sender, EventArgs e)
        {
            if (_disposed) return;
            try
            {
                _debounceTimer.Stop();
                ShowDropdown();
            }
            catch { }
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (_disposed) return;
            try
            {
                if (_listBox == null || _listBox.IsDisposed || !_listBox.Visible)
                {
                    return;
                }
            }
            catch { return; }

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
            if (_disposed) return;
            // 延迟隐藏，让鼠标点击 ListBox 有机会触发
            _debounceTimer.Stop();
            Timer delayHide = new Timer
            {
                Interval = 200
            };
            _pendingHideTimers.Add(delayHide);
            delayHide.Tick += (s, args) =>
            {
                try
                {
                    delayHide.Stop();
                    _pendingHideTimers.Remove(delayHide);
                    delayHide.Dispose();
                    // 【V1.72.15】关窗竞态：宿主窗体已释放时直接丢弃，不碰已释放控件。
                    if (_disposed) return;
                    if (_listBox == null || _listBox.IsDisposed) return;
                    if (_textBox == null || _textBox.IsDisposed) return;
                    if (!_listBox.Focused && !_textBox.Focused)
                    {
                        // 如果下拉框已被隐藏（如用户已通过鼠标选中列表项），无需处理
                        if (!_listBox.Visible) return;

                        // 点击非匹配列表区域 → 仅隐藏下拉框，文本框内容保持不变（视为未选择）
                        HideDropdown();
                    }
                }
                catch { }
            };
            try
            {
                if (_disposed) { delayHide.Dispose(); return; }
                delayHide.Start();
            }
            catch { }
        }

        /// <summary>
        /// ListBox 失去焦点时（用户点击了窗体空白区域或其他控件），延迟检查后仅隐藏下拉框
        /// </summary>
        private void ListBox_Leave(object sender, EventArgs e)
        {
            if (_disposed) return;
            if (!_listBox.Visible) return;

            Timer delayHide = new Timer
            {
                Interval = 100
            };
            _pendingHideTimers.Add(delayHide);
            delayHide.Tick += (s, args) =>
            {
                try
                {
                    delayHide.Stop();
                    _pendingHideTimers.Remove(delayHide);
                    delayHide.Dispose();
                    // 【V1.72.15】关窗竞态：同 TextBox_Leave，见该处注释。
                    if (_disposed) return;
                    if (!_listBox.Visible) return;
                    if (_textBox == null || _textBox.IsDisposed) return;
                    if (!_textBox.Focused)
                    {
                        // 点击非匹配列表区域 → 仅隐藏下拉框，文本框内容保持不变（视为未选择）
                        HideDropdown();
                    }
                }
                catch { }
            };
            try
            {
                if (_disposed) { delayHide.Dispose(); return; }
                delayHide.Start();
            }
            catch { }
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
                // 【V1.72.15】先排空延迟隐藏定时器：它们到点即碰 _listBox/_textBox，
                // 宿主关闭后到点就是关窗竞态，Stop+Dispose 后 Tick 永不再 fire。
                foreach (Timer t in _pendingHideTimers.ToArray())
                {
                    try { t.Stop(); t.Dispose(); }
                    catch { }
                }
                _pendingHideTimers.Clear();

                try { _debounceTimer.Stop(); } catch { }
                try { _debounceTimer.Dispose(); } catch { }

                Application.RemoveMessageFilter(this);

                _textBox.TextChanged -= TextBox_TextChanged;
                _textBox.KeyDown -= TextBox_KeyDown;
                _textBox.Leave -= TextBox_Leave;

                _listBox.MouseClick -= ListBox_MouseClick;
                _listBox.Leave -= ListBox_Leave;

                // 【大扫荡】用挂接时记的父窗摘事件（以前 Dispose 时重 FindForm，
                // 窗体半拆时可能找到另一个，事件摘错地方）。
                Form parentForm = (_parentForm != null && !_parentForm.IsDisposed)
                    ? _parentForm : null;
                if (parentForm == null)
                {
                    try { parentForm = _textBox.FindForm(); } catch { parentForm = null; }
                }
                if (parentForm != null)
                {
                    parentForm.Deactivate -= ParentForm_Deactivate;
                    parentForm.Resize -= ParentForm_Resize;
                    parentForm.Controls.Remove(_listBox);
                }
                _parentForm = null;

                _listBox.Dispose();
            }

            _disposed = true;
        }
    }
}