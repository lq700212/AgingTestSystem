namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 用户管理窗体 —— 设计器自动生成部分
    /// 【说明】
    /// 仅供管理员使用，管理操作员和技术员的账号（支持多账号），管理员账号不允许在此管理。
    /// 窗体布局：
    /// ┌──────────────────────────────────────────┐
    /// │           用户账号管理                    │
    /// ├──────────────────────────────────────────┤
    /// │ 角色:        [操作员 ▼]                  │
    /// │ 当前角色:    操作员                       │
    /// │ 用户名:      [operator ▼ (可编辑)]       │
    /// │ 新密码:      [____________________]     │
    /// │ 确认密码:    [____________________]     │
    /// ├──────────────────────────────────────────┤
    /// │ [添加账号][删除账号][应用修改][关闭]      │
    /// └──────────────────────────────────────────┘
    /// 【用户名下拉框说明】
    /// cboUsername 是可编辑下拉框（DropDown 模式）：点击展开显示当前角色下已创建的全部账号，
    /// 供管理员选择要对哪个账号进行修改；也可直接输入新用户名（用于改用户名）。
    /// </summary>
    partial class UserManagementForm
    {
        /// <summary>必需的设计器变量</summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        private void InitializeComponent()
        {
            this.lblTitle = new Sunny.UI.UILabel();
            this.lblRole = new Sunny.UI.UILabel();
            this.cboRole = new Sunny.UI.UIComboBox();
            this.lblCurrentUsername = new Sunny.UI.UILabel();
            this.lblCurrentUsernameValue = new Sunny.UI.UILabel();
            this.lblUsername = new Sunny.UI.UILabel();
            this.cboUsername = new Sunny.UI.UIComboBox();
            this.lblNewPassword = new Sunny.UI.UILabel();
            this.txtNewPassword = new Sunny.UI.UITextBox();
            this.lblConfirmPassword = new Sunny.UI.UILabel();
            this.txtConfirmPassword = new Sunny.UI.UITextBox();
            this.btnAddAccount = new Sunny.UI.UIButton();
            this.btnDeleteAccount = new Sunny.UI.UIButton();
            this.btnApply = new Sunny.UI.UIButton();
            this.btnClose = new Sunny.UI.UIButton();
            this.SuspendLayout();
            //
            // lblTitle - 标题
            //
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("微软雅黑", 11F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Location = new System.Drawing.Point(120, 55);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(135, 20);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "用户账号管理";
            //
            // lblRole - "角色:"标签
            //
            this.lblRole.AutoSize = true;
            this.lblRole.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblRole.Location = new System.Drawing.Point(30, 97);
            this.lblRole.Name = "lblRole";
            this.lblRole.Size = new System.Drawing.Size(35, 17);
            this.lblRole.TabIndex = 1;
            this.lblRole.Text = "角色:";
            //
            // cboRole - 角色选择下拉框（操作员/技术员）
            //
            this.cboRole.DropDownStyle = Sunny.UI.UIDropDownStyle.DropDownList;
            this.cboRole.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.cboRole.FormattingEnabled = true;
            this.cboRole.Items.AddRange(new object[] {
                "操作员",
                "技术员"});
            this.cboRole.Location = new System.Drawing.Point(120, 94);
            this.cboRole.Name = "cboRole";
            this.cboRole.Size = new System.Drawing.Size(220, 25);
            this.cboRole.TabIndex = 2;
            this.cboRole.SelectedIndexChanged += new System.EventHandler(this.cboRole_SelectedIndexChanged);
            //
            // lblCurrentUsername - "当前角色:"标签
            //
            this.lblCurrentUsername.AutoSize = true;
            this.lblCurrentUsername.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblCurrentUsername.Location = new System.Drawing.Point(30, 132);
            this.lblCurrentUsername.Name = "lblCurrentUsername";
            this.lblCurrentUsername.Size = new System.Drawing.Size(84, 17);
            this.lblCurrentUsername.TabIndex = 3;
            this.lblCurrentUsername.Text = "当前角色:";
            //
            // lblCurrentUsernameValue - 当前角色名（只读显示，V1.19.7 起显示中文角色名并按角色着色：
            // 技术员=蓝色、操作员=绿色，运行时由 UpdateRoleDisplay 设置）
            //
            this.lblCurrentUsernameValue.AutoSize = true;
            this.lblCurrentUsernameValue.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold);
            this.lblCurrentUsernameValue.ForeColor = System.Drawing.Color.Green;
            this.lblCurrentUsernameValue.Location = new System.Drawing.Point(120, 132);
            this.lblCurrentUsernameValue.Name = "lblCurrentUsernameValue";
            this.lblCurrentUsernameValue.Size = new System.Drawing.Size(39, 17);
            this.lblCurrentUsernameValue.TabIndex = 4;
            this.lblCurrentUsernameValue.Text = "操作员";
            //
            // lblUsername - "用户名:"标签
            //
            this.lblUsername.AutoSize = true;
            this.lblUsername.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblUsername.Location = new System.Drawing.Point(30, 167);
            this.lblUsername.Name = "lblUsername";
            this.lblUsername.Size = new System.Drawing.Size(54, 17);
            this.lblUsername.TabIndex = 5;
            this.lblUsername.Text = "用户名:";
            //
            // cboUsername - 用户名下拉框（可编辑）
            // 点击展开显示当前角色下已创建的全部账号，供选择要修改的目标账号；
            // 也可直接输入新用户名（用于改用户名）。
            //
            this.cboUsername.DropDownStyle = Sunny.UI.UIDropDownStyle.DropDown;
            this.cboUsername.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.cboUsername.FormattingEnabled = true;
            this.cboUsername.Location = new System.Drawing.Point(120, 164);
            this.cboUsername.Name = "cboUsername";
            this.cboUsername.Size = new System.Drawing.Size(220, 25);
            this.cboUsername.TabIndex = 6;
            this.cboUsername.SelectedIndexChanged += new System.EventHandler(this.cboUsername_SelectedIndexChanged);
            //
            // lblNewPassword - "新密码:"标签
            //
            this.lblNewPassword.AutoSize = true;
            this.lblNewPassword.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblNewPassword.Location = new System.Drawing.Point(30, 202);
            this.lblNewPassword.Name = "lblNewPassword";
            this.lblNewPassword.Size = new System.Drawing.Size(54, 17);
            this.lblNewPassword.TabIndex = 7;
            this.lblNewPassword.Text = "新密码:";
            //
            // txtNewPassword - 新密码输入框（密码模式）
            //
            this.txtNewPassword.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.txtNewPassword.Location = new System.Drawing.Point(120, 199);
            this.txtNewPassword.Name = "txtNewPassword";
            this.txtNewPassword.Size = new System.Drawing.Size(220, 23);
            this.txtNewPassword.TabIndex = 8;
            this.txtNewPassword.PasswordChar = '*';
            //
            // lblConfirmPassword - "确认密码:"标签
            //
            this.lblConfirmPassword.AutoSize = true;
            this.lblConfirmPassword.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblConfirmPassword.Location = new System.Drawing.Point(30, 237);
            this.lblConfirmPassword.Name = "lblConfirmPassword";
            this.lblConfirmPassword.Size = new System.Drawing.Size(69, 17);
            this.lblConfirmPassword.TabIndex = 9;
            this.lblConfirmPassword.Text = "确认密码:";
            //
            // txtConfirmPassword - 确认密码输入框（密码模式）
            //
            this.txtConfirmPassword.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.txtConfirmPassword.Location = new System.Drawing.Point(120, 234);
            this.txtConfirmPassword.Name = "txtConfirmPassword";
            this.txtConfirmPassword.Size = new System.Drawing.Size(220, 23);
            this.txtConfirmPassword.TabIndex = 10;
            this.txtConfirmPassword.PasswordChar = '*';
            //
            // btnAddAccount - 添加账号按钮
            //
            this.btnAddAccount.FillColor = System.Drawing.Color.SteelBlue;
            this.btnAddAccount.RectColor = System.Drawing.Color.SteelBlue;
            this.btnAddAccount.ForeColor = System.Drawing.Color.White;
            this.btnAddAccount.Style = Sunny.UI.UIStyle.Custom;
            this.btnAddAccount.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.btnAddAccount.Location = new System.Drawing.Point(20, 282);
            this.btnAddAccount.Name = "btnAddAccount";
            this.btnAddAccount.Size = new System.Drawing.Size(85, 32);
            this.btnAddAccount.TabIndex = 11;
            this.btnAddAccount.Text = "添加账号";
            this.btnAddAccount.Click += new System.EventHandler(this.btnAddAccount_Click);
            //
            // btnDeleteAccount - 删除账号按钮
            //
            this.btnDeleteAccount.FillColor = System.Drawing.Color.OrangeRed;
            this.btnDeleteAccount.RectColor = System.Drawing.Color.OrangeRed;
            this.btnDeleteAccount.ForeColor = System.Drawing.Color.White;
            this.btnDeleteAccount.Style = Sunny.UI.UIStyle.Custom;
            this.btnDeleteAccount.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.btnDeleteAccount.Location = new System.Drawing.Point(110, 282);
            this.btnDeleteAccount.Name = "btnDeleteAccount";
            this.btnDeleteAccount.Size = new System.Drawing.Size(85, 32);
            this.btnDeleteAccount.TabIndex = 12;
            this.btnDeleteAccount.Text = "删除账号";
            this.btnDeleteAccount.Click += new System.EventHandler(this.btnDeleteAccount_Click);
            //
            // btnApply - 应用修改按钮
            //
            this.btnApply.FillColor = System.Drawing.Color.ForestGreen;
            this.btnApply.RectColor = System.Drawing.Color.ForestGreen;
            this.btnApply.ForeColor = System.Drawing.Color.White;
            this.btnApply.Style = Sunny.UI.UIStyle.Custom;
            this.btnApply.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.btnApply.Location = new System.Drawing.Point(200, 282);
            this.btnApply.Name = "btnApply";
            this.btnApply.Size = new System.Drawing.Size(85, 32);
            this.btnApply.TabIndex = 13;
            this.btnApply.Text = "应用修改";
            this.btnApply.Click += new System.EventHandler(this.btnApply_Click);
            //
            // btnClose - 关闭按钮
            //
            this.btnClose.FillColor = System.Drawing.Color.DimGray;
            this.btnClose.RectColor = System.Drawing.Color.DimGray;
            this.btnClose.ForeColor = System.Drawing.Color.White;
            this.btnClose.Style = Sunny.UI.UIStyle.Custom;
            this.btnClose.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.btnClose.Location = new System.Drawing.Point(290, 282);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(90, 32);
            this.btnClose.TabIndex = 14;
            this.btnClose.Text = "关闭";
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            //
            // UserManagementForm - 窗体属性
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.ClientSize = new System.Drawing.Size(400, 335);
            // 绝对布局：禁缩小（MinimumSize=ClientSize），防缩坏布局；可放大。
            this.MinimumSize = new System.Drawing.Size(400, 335);
            // UIForm 自绘蓝标题：删 FormBorderStyle，内容整体下移 35px。
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.btnApply);
            this.Controls.Add(this.btnDeleteAccount);
            this.Controls.Add(this.btnAddAccount);
            this.Controls.Add(this.txtConfirmPassword);
            this.Controls.Add(this.lblConfirmPassword);
            this.Controls.Add(this.txtNewPassword);
            this.Controls.Add(this.lblNewPassword);
            this.Controls.Add(this.cboUsername);
            this.Controls.Add(this.lblUsername);
            this.Controls.Add(this.lblCurrentUsernameValue);
            this.Controls.Add(this.lblCurrentUsername);
            this.Controls.Add(this.cboRole);
            this.Controls.Add(this.lblRole);
            this.Controls.Add(this.lblTitle);
            this.Name = "UserManagementForm";
            this.Text = "用户账号管理";
            this.Load += new System.EventHandler(this.UserManagementForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        // ===== 控件字段声明 =====
        private Sunny.UI.UILabel lblTitle;
        private Sunny.UI.UILabel lblRole;
        private Sunny.UI.UIComboBox cboRole;
        private Sunny.UI.UILabel lblCurrentUsername;
        private Sunny.UI.UILabel lblCurrentUsernameValue;
        private Sunny.UI.UILabel lblUsername;
        private Sunny.UI.UIComboBox cboUsername;
        private Sunny.UI.UILabel lblNewPassword;
        private Sunny.UI.UITextBox txtNewPassword;
        private Sunny.UI.UILabel lblConfirmPassword;
        private Sunny.UI.UITextBox txtConfirmPassword;
        private Sunny.UI.UIButton btnAddAccount;
        private Sunny.UI.UIButton btnDeleteAccount;
        private Sunny.UI.UIButton btnApply;
        private Sunny.UI.UIButton btnClose;
    }
}
