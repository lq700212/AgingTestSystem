using System;
using System.Windows.Forms;
using BarometerWinform.Models;
using BarometerWinform.Services;

namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 登录窗体（业务逻辑部分）
    ///
    /// 【功能说明】
    /// 用户切换权限时弹出此窗体，要求输入用户名和密码。
    /// - 输入正确：返回 DialogResult.OK，主窗体据此切换权限
    /// - 输入错误：弹出提示窗口告知错误原因
    /// - 点击取消：返回 DialogResult.Cancel，不切换权限
    ///
    /// 【交互细节】
    /// 1. 窗体标题动态显示"切换为 XXX 权限"（XXX 为目标角色名）
    /// 2. 按 Enter 键等同点击"确认"按钮（提升操作效率）
    /// 3. 按 Esc 键等同点击"取消"按钮（标准对话框行为）
    /// 4. 密码框使用密码模式，输入字符显示为圆点
    /// 5. 用户名下拉框自动列出该角色已有账号，可直接选择（也可手动输入）
    /// 6. 勾选"记住密码"后，下次登录自动填充该角色的用户名和密码
    /// </summary>
    public partial class LoginForm : Form
    {
        /// <summary>用户管理器（提供登录验证功能）</summary>
        private readonly UserManager _userManager;

        /// <summary>目标角色（用户想切换到的角色）</summary>
        private readonly UserRole _targetRole;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="userManager">用户管理器实例</param>
        /// <param name="targetRole">目标角色（用户想切换到的角色）</param>
        public LoginForm(UserManager userManager, UserRole targetRole)
        {
            InitializeComponent();

            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _targetRole = targetRole;
        }

        /// <summary>
        /// 窗体加载事件
        /// 设置标题文本和窗体标题
        /// </summary>
        private void LoginForm_Load(object sender, EventArgs e)
        {
            // 获取目标角色的中文名
            string roleName = GetRoleDisplayName(_targetRole);

            // 设置标题文本：例如 "切换为 管理员 权限"
            lblTitle.Text = $"切换为 {roleName} 权限";
            // 标题居中显示（AutoSize 模式下需手动计算位置）
            lblTitle.Location = new System.Drawing.Point(
                (this.ClientSize.Width - lblTitle.Width) / 2, lblTitle.Location.Y);

            // 设置窗体标题栏文本
            this.Text = $"{roleName}登录";

            // 加载该角色下已有账号，供用户直接下拉选择（也可手动输入）
            txtUsername.Items.Clear();
            foreach (UserAccount account in _userManager.GetAccounts(_targetRole))
            {
                txtUsername.Items.Add(account.Username);
            }

            // 若该角色记住了登录信息，自动填充用户名和密码并勾选"记住密码"
            var (savedUsername, savedPassword) = _userManager.GetRememberedLogin(_targetRole);
            if (savedUsername != null && savedPassword != null)
            {
                int savedIndex = txtUsername.Items.IndexOf(savedUsername);
                if (savedIndex >= 0)
                {
                    txtUsername.SelectedIndex = savedIndex;
                }
                else
                {
                    txtUsername.Text = savedUsername;
                }
                txtPassword.Text = savedPassword;
                chkRemember.Checked = true;
            }
            else if (txtUsername.Items.Count > 0)
            {
                // 默认选中第一个账号（管理员仅一个账号）
                txtUsername.SelectedIndex = 0;
            }

            // 默认聚焦用户名下拉框，方便用户直接选择或输入
            txtUsername.Focus();
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
        /// 确认按钮点击事件
        /// 调用 UserManager.Login 验证用户名和密码
        /// </summary>
        private void btnOK_Click(object sender, EventArgs e)
        {
            // 调用登录验证
            LoginResult result = _userManager.Login(_targetRole, txtUsername.Text, txtPassword.Text);

            if (result.Success)
            {
                // 记住密码：勾选则保存本次登录信息，未勾选则清除该角色已记住的信息
                if (chkRemember.Checked)
                {
                    _userManager.SaveRememberedLogin(_targetRole, txtUsername.Text, txtPassword.Text);
                }
                else
                {
                    _userManager.ClearRememberedLogin(_targetRole);
                }

                // 登录成功：设置 DialogResult 并关闭窗体
                // 主窗体通过 ShowDialog 返回值判断是否登录成功
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                // 登录失败：弹出错误提示，清空密码框，聚焦用户名框
                MessageBox.Show(
                    result.ErrorMessage,
                    "登录失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                // 清空密码框（用户名保留，方便用户检查拼写）
                txtPassword.Clear();
                // 选中用户名全部内容，方便用户重新输入
                txtUsername.Focus();
                txtUsername.SelectAll();
            }
        }

        /// <summary>
        /// 取消按钮点击事件
        /// 直接关闭窗体，不进行登录验证
        /// </summary>
        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        /// <summary>
        /// 用户名输入框按键事件
        /// 按 Enter 键时自动跳转到密码框（提升操作效率）
        /// </summary>
        private void txtUsername_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Enter 键的字符码是 (char)13
            if (e.KeyChar == (char)Keys.Enter)
            {
                e.Handled = true;  // 阻止系统发出"咚"的提示音
                txtPassword.Focus();
            }
        }

        /// <summary>
        /// 密码输入框按键事件
        /// 按 Enter 键时等同点击"确认"按钮
        /// </summary>
        private void txtPassword_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                e.Handled = true;
                btnOK_Click(sender, e);
            }
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
