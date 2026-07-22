using System;
using System.Windows.Forms;
using BarometerWinform.Models;
using BarometerWinform.Services;

namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 用户管理窗体（业务逻辑部分）
    ///
    /// 【功能说明】
    /// 仅供管理员使用，用于修改操作员和技术员的用户名和密码。
    ///
    /// 【操作流程】
    /// 1. 从角色下拉框选择要修改的角色（操作员/技术员）
    /// 2. 系统自动显示该角色的当前用户名
    /// 3. 在"新用户名"输入框填写新用户名（留空表示不修改用户名）
    /// 4. 在"新密码"和"确认密码"输入框填写新密码（留空表示不修改密码）
    /// 5. 点击"应用修改"按钮提交修改
    ///
    /// 【校验规则】
    /// - 新密码和确认密码必须一致
    /// - 用户名至少2个字符
    /// - 密码至少4个字符
    /// - 用户名不能与其他角色重复
    /// </summary>
    public partial class UserManagementForm : Form
    {
        /// <summary>用户管理器实例</summary>
        private readonly UserManager _userManager;

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
        /// 加载所选角色的当前用户名
        /// </summary>
        private void cboRole_SelectedIndexChanged(object sender, EventArgs e)
        {
            // 获取选中的角色
            UserRole role = GetSelectedRole();
            if (role == UserRole.Administrator)
            {
                // 防御性处理：管理员不允许修改自己的账号
                lblCurrentUsernameValue.Text = "(不可修改)";
                return;
            }

            // 从用户管理器获取该角色的当前用户名
            UserAccount account = _userManager.GetAccount(role);
            if (account != null)
            {
                lblCurrentUsernameValue.Text = account.Username;
            }
            else
            {
                lblCurrentUsernameValue.Text = "(未找到)";
            }

            // 清空输入框（切换角色时重置输入）
            txtNewUsername.Clear();
            txtNewPassword.Clear();
            txtConfirmPassword.Clear();
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
        /// 校验输入并调用 UserManager 修改用户名和密码
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

            // 读取输入值（去除首尾空格）
            string newUsername = txtNewUsername.Text.Trim();
            string newPassword = txtNewPassword.Text;
            string confirmPassword = txtConfirmPassword.Text;

            // 标记是否有修改
            bool hasUsernameChange = !string.IsNullOrEmpty(newUsername);
            bool hasPasswordChange = !string.IsNullOrEmpty(newPassword);

            // 如果两个输入都为空，提示用户
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

            // 修改用户名
            if (hasUsernameChange)
            {
                var (success, message) = _userManager.UpdateUsername(targetRole, newUsername);
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
                var (success, message) = _userManager.UpdatePassword(targetRole, newPassword);
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

                // 刷新当前用户名显示
                UserAccount account = _userManager.GetAccount(targetRole);
                if (account != null)
                {
                    lblCurrentUsernameValue.Text = account.Username;
                }

                // 清空输入框，方便继续修改其他角色
                txtNewUsername.Clear();
                txtNewPassword.Clear();
                txtConfirmPassword.Clear();
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
