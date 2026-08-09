using System;
using System.Windows.Forms;
using AgingTestSystem.Services;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 修改密码窗体（业务逻辑部分）
    ///
    /// 【功能说明】
    /// 任意已登录用户（操作员/技术员/管理员）修改自己密码时弹出此窗体。
    /// 与 UserManagementForm 的区别：这里是"改自己密码"，需验证当前密码；
    /// 管理员改其他账号（操作员/技术员）密码仍走用户管理窗体（无需验旧密码，用于忘记密码重置）。
    ///
    /// 【操作流程】
    /// 1. 用户从主界面权限下拉菜单选择"修改密码"，弹出此窗体
    /// 2. 输入当前密码、新密码、确认新密码
    /// 3. 点击"确认"：校验通过后调用 UserManager.ChangeOwnPassword 修改
    /// 4. 修改成功后返回 DialogResult.OK，由主窗体提示用户
    ///
    /// 【校验规则】
    /// - 当前密码必须正确（验证身份，防止他人篡改）
    /// - 新密码不能为空，至少4个字符
    /// - 新密码不能与当前密码相同
    /// - 新密码和确认新密码必须一致
    /// </summary>
    public partial class ChangePasswordForm : Form
    {
        /// <summary>用户管理器（提供修改密码功能）</summary>
        private readonly UserManager _userManager;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="userManager">用户管理器实例</param>
        public ChangePasswordForm(UserManager userManager)
        {
            InitializeComponent();
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        }

        /// <summary>
        /// 窗体加载事件
        /// 显示当前登录用户名，默认聚焦当前密码输入框
        /// </summary>
        private void ChangePasswordForm_Load(object sender, EventArgs e)
        {
            // 显示当前登录用户（未登录时不应打开此窗体，防御性处理）
            if (_userManager.CurrentUser != null)
            {
                lblUserValue.Text = _userManager.CurrentUser.Username;
            }
            else
            {
                lblUserValue.Text = "(未登录)";
                btnOK.Enabled = false;
            }

            txtCurrentPassword.Focus();
        }

        /// <summary>
        /// 确认按钮点击事件
        /// 校验输入并调用 UserManager 修改当前用户密码
        /// </summary>
        private void btnOK_Click(object sender, EventArgs e)
        {
            string oldPassword = txtCurrentPassword.Text;
            string newPassword = txtNewPassword.Text;
            string confirmPassword = txtConfirmPassword.Text;

            // 校验当前密码非空（具体错误提示交给 UserManager）
            if (string.IsNullOrEmpty(oldPassword))
            {
                MessageBox.Show("请输入当前密码", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtCurrentPassword.Focus();
                return;
            }

            // 校验新密码非空
            if (string.IsNullOrEmpty(newPassword))
            {
                MessageBox.Show("请输入新密码", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtNewPassword.Focus();
                return;
            }

            // 校验确认密码
            if (string.IsNullOrEmpty(confirmPassword))
            {
                MessageBox.Show("请输入确认密码", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtConfirmPassword.Focus();
                return;
            }

            // 新密码与确认密码一致性校验
            if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            {
                MessageBox.Show("新密码和确认密码不一致", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtConfirmPassword.Clear();
                txtConfirmPassword.Focus();
                return;
            }

            // 调用 UserManager 修改当前用户密码
            var (success, message) = _userManager.ChangeOwnPassword(oldPassword, newPassword);

            if (success)
            {
                MessageBox.Show(message, "修改成功",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show(message, "修改失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);

                // 当前密码错误时清空当前密码并聚焦，其他情况清空新密码相关输入框
                if (message.Contains("当前密码"))
                {
                    txtCurrentPassword.Clear();
                    txtCurrentPassword.Focus();
                }
                else
                {
                    txtNewPassword.Clear();
                    txtConfirmPassword.Clear();
                    txtNewPassword.Focus();
                }
            }
        }

        /// <summary>
        /// 取消按钮点击事件
        /// 关闭窗体，不做任何操作
        /// </summary>
        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        /// <summary>
        /// 重写 ProcessCmdKey 方法
        /// 实现 Esc 键等同点击"取消"按钮（标准对话框行为）
        /// </summary>
        protected override bool ProcessCmdKey(ref System.Windows.Forms.Message msg, System.Windows.Forms.Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                btnCancel_Click(this, null);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
