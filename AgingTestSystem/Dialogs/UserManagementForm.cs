using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AgingTestSystem.Models;
using AgingTestSystem.Services;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 用户管理窗体（业务逻辑部分）
    ///
    /// 【功能说明】
    /// 仅供管理员使用，管理操作员和技术员的账号（每个角色支持多个账号）。
    ///
    /// 【操作流程】
    /// 1. 从角色下拉框选择要管理的角色（操作员/技术员）
    /// 2. 从用户名下拉框选择要操作的账号（下拉列表自动列出该角色下所有已有账号）
    /// 3. "应用修改"：修改选中账号的密码；如需改用户名，直接在用户名框输入新名字后点应用
    /// 4. "添加账号"：为当前角色新增一个账号（弹出输入窗口）
    /// 5. "删除账号"：删除选中的账号（每角色至少保留一个）
    ///
    /// 【用户名下拉框语义（可编辑）】
    /// - 点击展开：列出当前角色下已创建的全部账号，供选择要修改的目标账号
    /// - 保持原样点"应用修改"：只修改该账号密码
    /// - 在框内输入新名字点"应用修改"：同时把该账号改名（新名字须全局唯一）
    /// - 输入的文本若能匹配某个已有账号，则视为选中该账号
    ///
    /// 【校验规则】
    /// - 新密码和确认密码必须一致
    /// - 用户名至少2个字符，密码至少4个字符
    /// - 用户名在全部角色内唯一
    /// - 管理员账号不在此管理（仅一个）
    /// </summary>
    public partial class UserManagementForm : Form
    {
        /// <summary>用户管理器实例</summary>
        private readonly UserManager _userManager;

        /// <summary>
        /// 上次通过下拉框选中的账号
        /// 用户选择账号后编辑用户名文本（SelectedIndex 会变为 -1）时，
        /// 仍以该账号作为"改用户名"的目标。
        /// </summary>
        private UserAccount _lastSelectedAccount;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="userManager">用户管理器实例</param>
        public UserManagementForm(UserManager userManager)
        {
            InitializeComponent();
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        }

        /// <summary>
        /// 窗体加载事件
        /// 默认选中第一个角色（操作员），并显示其当前用户名
        /// </summary>
        private void UserManagementForm_Load(object sender, EventArgs e)
        {
            // 默认选中"操作员"
            if (cboRole.Items.Count > 0)
            {
                cboRole.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// 角色下拉框选择变更事件
        /// 刷新该角色下的账号列表，并重置输入框
        /// </summary>
        private void cboRole_SelectedIndexChanged(object sender, EventArgs e)
        {
            // 获取选中的角色
            UserRole role = GetSelectedRole();

            // 显示角色中文名并按角色着色（V1.19.7：技术员=蓝色，操作员=绿色）
            UpdateRoleDisplay(role);

            // 刷新该角色下所有账号
            RefreshAccountList(role);

            // 清空输入框（切换角色时重置输入）
            ResetInputFields();
        }

        /// <summary>
        /// 用户名下拉框选择变更事件
        /// 记录选中的目标账号，并重置密码输入框，
        /// 避免误把上一账号的修改内容应用到新账号
        /// </summary>
        private void cboUsername_SelectedIndexChanged(object sender, EventArgs e)
        {
            UserRole role = GetSelectedRole();
            IReadOnlyList<UserAccount> accounts = _userManager.GetAccounts(role);
            int index = cboUsername.SelectedIndex;
            if (index >= 0 && index < accounts.Count)
            {
                _lastSelectedAccount = accounts[index];
            }
            ResetInputFields();
        }

        /// <summary>
        /// 刷新用户名下拉框：列出指定角色下的全部已有账号，默认选中第一个
        /// </summary>
        /// <param name="role">目标角色</param>
        private void RefreshAccountList(UserRole role)
        {
            // 记住当前选中用户名（切角色/刷新后尽量保持选中）
            string previous = cboUsername.Text.Trim();

            cboUsername.Items.Clear();
            foreach (UserAccount account in _userManager.GetAccounts(role))
            {
                cboUsername.Items.Add(account.Username);
            }

            if (cboUsername.Items.Count > 0)
            {
                // 优先恢复之前的选中项，否则默认选中第一个账号
                int index = previous.Length > 0 ? cboUsername.Items.IndexOf(previous) : -1;
                cboUsername.SelectedIndex = index >= 0 ? index : 0;
            }
        }

        /// <summary>
        /// 获取当前要操作的账号对象
        ///
        /// 【匹配规则】
        /// 1. 下拉框已选中某项：目标为该选中账号（用户可能同时修改了文本，视为"改用户名"）
        /// 2. 下拉框未选中（用户直接输入文本定位）：先按文本匹配已有账号（忽略大小写）；
        ///    匹配不上则回退到上次下拉选中的账号（_lastSelectedAccount，作为改名目标）
        /// 3. 均无法确定时返回 null
        /// </summary>
        /// <returns>目标账号（未确定返回 null）</returns>
        private UserAccount GetSelectedAccount()
        {
            UserRole role = GetSelectedRole();
            IReadOnlyList<UserAccount> accounts = _userManager.GetAccounts(role);
            string username = cboUsername.Text.Trim();

            // 下拉框已选中某项：直接返回该账号（文本可能已被用户改为新用户名）
            int index = cboUsername.SelectedIndex;
            if (index >= 0 && index < accounts.Count)
            {
                return accounts[index];
            }

            // 下拉框未选中：先按文本匹配已有账号（用户直接输入已有账号名定位）
            if (username.Length > 0)
            {
                foreach (UserAccount account in accounts)
                {
                    if (string.Equals(account.Username, username, StringComparison.OrdinalIgnoreCase))
                    {
                        return account;
                    }
                }
            }

            // 文本不匹配任何账号：回退到上次下拉选中的账号（视为改用户名的目标）
            if (_lastSelectedAccount != null)
            {
                return _lastSelectedAccount;
            }

            return null;
        }

        /// <summary>
        /// 清空密码/确认密码输入框（用户名下拉框保留，作为选择/改名载体）
        /// </summary>
        private void ResetInputFields()
        {
            txtNewPassword.Clear();
            txtConfirmPassword.Clear();
        }

        /// <summary>
        /// 更新"当前角色"显示（【V1.19.7】）
        /// 显示角色中文名并按角色着色，替代原先显示用户名：
        /// - 技术员 → 蓝色（Blue）
        /// - 操作员 → 绿色（Green）
        /// - 管理员 → 红色（Red，防御性分支）
        /// - 其他 → 默认文字色
        /// </summary>
        /// <param name="role">当前选中的角色</param>
        private void UpdateRoleDisplay(UserRole role)
        {
            switch (role)
            {
                case UserRole.Technician:
                    lblCurrentUsernameValue.Text = "技术员";
                    lblCurrentUsernameValue.ForeColor = Color.Blue;
                    break;
                case UserRole.Operator:
                    lblCurrentUsernameValue.Text = "操作员";
                    lblCurrentUsernameValue.ForeColor = Color.Green;
                    break;
                case UserRole.Administrator:
                    lblCurrentUsernameValue.Text = "管理员";
                    lblCurrentUsernameValue.ForeColor = Color.Red;
                    break;
                default:
                    lblCurrentUsernameValue.Text = "(未知)";
                    lblCurrentUsernameValue.ForeColor = SystemColors.ControlText;
                    break;
            }
        }

        /// <summary>
        /// 获取下拉框中选中的角色
        /// </summary>
        /// <returns>选中的角色枚举</returns>
        private UserRole GetSelectedRole()
        {
            // 根据下拉框索引返回对应角色
            // 0 = 操作员, 1 = 技术员
            switch (cboRole.SelectedIndex)
            {
                case 0:
                    return UserRole.Operator;
                case 1:
                    return UserRole.Technician;
                default:
                    return UserRole.Operator;
            }
        }

        /// <summary>
        /// 应用修改按钮点击事件
        /// 校验输入并调用 UserManager 修改选中账号的用户名和密码
        /// </summary>
        private void btnApply_Click(object sender, EventArgs e)
        {
            UserRole targetRole = GetSelectedRole();

            // 防御性校验：不允许修改管理员账号
            if (targetRole == UserRole.Administrator)
            {
                MessageBox.Show("不允许修改管理员账号", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 获取目标账号（用户名框文本匹配已有账号，或作为改名目标取下拉选中账号）
            UserAccount account = GetSelectedAccount();
            if (account == null)
            {
                MessageBox.Show("请先选择要修改的账号", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 读取输入值（用户名去除首尾空格，密码原样保留）
            string newUsername = cboUsername.Text.Trim();
            string newPassword = txtNewPassword.Text;
            string confirmPassword = txtConfirmPassword.Text;

            // 标记是否有修改：用户名与当前账号不同视为改用户名；密码非空视为改密码
            bool hasUsernameChange = !string.Equals(account.Username, newUsername, StringComparison.Ordinal);
            bool hasPasswordChange = !string.IsNullOrEmpty(newPassword);

            // 如果两个输入都没有修改，提示用户
            if (!hasUsernameChange && !hasPasswordChange)
            {
                MessageBox.Show("请至少填写一项要修改的内容", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 校验密码：如果填了新密码，必须填确认密码且一致
            if (hasPasswordChange)
            {
                if (string.IsNullOrEmpty(confirmPassword))
                {
                    MessageBox.Show("请输入确认密码", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtConfirmPassword.Focus();
                    return;
                }

                if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
                {
                    MessageBox.Show("新密码和确认密码不一致", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtConfirmPassword.Clear();
                    txtConfirmPassword.Focus();
                    return;
                }
            }

            // 执行修改操作
            bool anySuccess = false;
            string lastMessage = "";

            // 修改用户名（输入的用户名与目标账号不同才需要改）
            if (hasUsernameChange)
            {
                var (success, message) = _userManager.UpdateUsername(account, newUsername);
                lastMessage = message;
                if (success)
                {
                    anySuccess = true;
                }
                else
                {
                    // 用户名修改失败，直接提示并返回（不继续修改密码）
                    MessageBox.Show(message, "用户名修改失败",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            // 修改密码
            if (hasPasswordChange)
            {
                var (success, message) = _userManager.UpdatePassword(account, newPassword);
                lastMessage = message;
                if (success)
                {
                    anySuccess = true;
                }
                else
                {
                    MessageBox.Show(message, "密码修改失败",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            // 修改成功后的处理
            if (anySuccess)
            {
                MessageBox.Show(lastMessage, "修改成功",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

                // 用户名可能已变更，刷新账号列表（尽量保持选中该账号）
                RefreshAccountList(targetRole);
                cboUsername.Text = account.Username;

                // 清空密码输入框，方便继续修改其他账号
                ResetInputFields();
            }
        }

        /// <summary>
        /// 添加账号按钮点击事件
        /// 弹出输入窗口，为当前角色新增一个账号
        /// </summary>
        private void btnAddAccount_Click(object sender, EventArgs e)
        {
            UserRole targetRole = GetSelectedRole();

            // 防御性校验：不允许添加管理员账号（管理员仅一个）
            if (targetRole == UserRole.Administrator)
            {
                MessageBox.Show("不允许添加管理员账号（管理员账号只能有一个）", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 弹出输入窗口（用户取消返回 null）
            var (username, password) = ShowAddAccountDialog(targetRole);
            if (username == null)
            {
                return;
            }

            var (success, message) = _userManager.AddAccount(targetRole, username, password);

            MessageBox.Show(message, success ? "添加成功" : "添加失败",
                MessageBoxButtons.OK,
                success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

            if (success)
            {
                // 刷新账号列表并选中新添加的账号
                RefreshAccountList(targetRole);
                cboUsername.Text = username;
                ResetInputFields();
            }
        }

        /// <summary>
        /// 删除账号按钮点击事件
        /// 确认后删除当前选中的账号（每角色至少保留一个）
        /// </summary>
        private void btnDeleteAccount_Click(object sender, EventArgs e)
        {
            UserRole targetRole = GetSelectedRole();

            // 防御性校验：不允许删除管理员账号
            if (targetRole == UserRole.Administrator)
            {
                MessageBox.Show("不允许删除管理员账号", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            UserAccount account = GetSelectedAccount();
            if (account == null)
            {
                MessageBox.Show("请先选择要删除的账号", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult confirm = MessageBox.Show(
                $"确定删除账号 '{account.Username}' 吗？",
                "删除确认",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            var (success, message) = _userManager.RemoveAccount(targetRole, account.Username);

            MessageBox.Show(message, success ? "删除成功" : "删除失败",
                MessageBoxButtons.OK,
                success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

            if (success)
            {
                RefreshAccountList(targetRole);
                ResetInputFields();
            }
        }

        /// <summary>
        /// 弹出添加账号的输入窗口（用户名/密码）
        /// </summary>
        /// <param name="role">目标角色（用于对话框标题）</param>
        /// <returns>输入的用户名和密码；用户取消时返回 (null, null)</returns>
        private (string Username, string Password) ShowAddAccountDialog(UserRole role)
        {
            using (var dlg = new Form())
            {
                dlg.Text = $"添加{GetRoleDisplayName(role)}账号";
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.ClientSize = new Size(340, 150);
                dlg.BackColor = Color.White;

                var lblUsername = new Label
                {
                    Text = "用户名:",
                    AutoSize = true,
                    Location = new Point(30, 25),
                    Font = new Font("微软雅黑", 9F)
                };
                var txtUsername = new TextBox
                {
                    Location = new Point(110, 22),
                    Width = 190,
                    Font = new Font("微软雅黑", 9F)
                };
                var lblPassword = new Label
                {
                    Text = "密码:",
                    AutoSize = true,
                    Location = new Point(30, 65),
                    Font = new Font("微软雅黑", 9F)
                };
                var txtPassword = new TextBox
                {
                    Location = new Point(110, 62),
                    Width = 190,
                    Font = new Font("微软雅黑", 9F),
                    UseSystemPasswordChar = true
                };

                var btnOK = new Button
                {
                    Text = "确定",
                    Location = new Point(110, 105),
                    Size = new Size(90, 30),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.LimeGreen,
                    ForeColor = Color.White
                };
                btnOK.DialogResult = DialogResult.OK;

                var btnCancel = new Button
                {
                    Text = "取消",
                    Location = new Point(210, 105),
                    Size = new Size(90, 30),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.DimGray,
                    ForeColor = Color.White
                };
                btnCancel.DialogResult = DialogResult.Cancel;

                dlg.Controls.Add(lblUsername);
                dlg.Controls.Add(txtUsername);
                dlg.Controls.Add(lblPassword);
                dlg.Controls.Add(txtPassword);
                dlg.Controls.Add(btnOK);
                dlg.Controls.Add(btnCancel);
                dlg.AcceptButton = btnOK;
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    return (null, null);
                }
                return (txtUsername.Text.Trim(), txtPassword.Text);
            }
        }

        /// <summary>
        /// 获取角色的中文显示名
        /// </summary>
        /// <param name="role">角色枚举</param>
        /// <returns>中文名（操作员/技术员/管理员）</returns>
        private string GetRoleDisplayName(UserRole role)
        {
            switch (role)
            {
                case UserRole.Operator:
                    return "操作员";
                case UserRole.Technician:
                    return "技术员";
                case UserRole.Administrator:
                    return "管理员";
                default:
                    return role.ToString();
            }
        }

        /// <summary>
        /// 关闭按钮点击事件
        /// </summary>
        private void btnClose_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
