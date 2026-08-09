namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 用户管理窗体 —— 设计器自动生成部分
    ///
    /// 【说明】
    /// 仅供管理员使用，管理操作员和技术员的账号（支持多账号），管理员账号不允许在此管理。
    ///
    /// 窗体布局：
    /// ┌──────────────────────────────────────────┐
    /// │           用户账号管理                    │
    /// ├──────────────────────────────────────────┤
    /// │ 角色:        [操作员 ▼]                  │
    /// │ 当前角色:    操作员                       │
    /// │ 账号:        [operator ▼]                │
    /// │ 新用户名:    [____________________]     │
    /// │ 新密码:      [____________________]     │
    /// │ 确认密码:    [____________________]     │
    /// ├──────────────────────────────────────────┤
    /// │ [添加账号][删除账号][应用修改][关闭]      │
    /// └──────────────────────────────────────────┘
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
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblRole = new System.Windows.Forms.Label();
            this.cboRole = new System.Windows.Forms.ComboBox();
            this.lblCurrentUsername = new System.Windows.Forms.Label();
            this.lblCurrentUsernameValue = new System.Windows.Forms.Label();
            this.lblAccount = new System.Windows.Forms.Label();
            this.cboAccount = new System.Windows.Forms.ComboBox();
            this.lblNewUsername = new System.Windows.Forms.Label();
            this.txtNewUsername = new System.Windows.Forms.TextBox();
            this.lblNewPassword = new System.Windows.Forms.Label();
            this.txtNewPassword = new System.Windows.Forms.TextBox();
            this.lblConfirmPassword = new System.Windows.Forms.Label();
            this.txtConfirmPassword = new System.Windows.Forms.TextBox();
            this.btnAddAccount = new System.Windows.Forms.Button();
            this.btnDeleteAccount = new System.Windows.Forms.Button();
            this.btnApply = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // lblTitle - 标题
            //
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("微软雅黑", 11F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Location = new System.Drawing.Point(120, 20);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(135, 20);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "用户账号管理";
            //
            // lblRole - "角色:"标签
            //
            this.lblRole.AutoSize = true;
            this.lblRole.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblRole.Location = new System.Drawing.Point(30, 62);
            this.lblRole.Name = "lblRole";
            this.lblRole.Size = new System.Drawing.Size(35, 17);
            this.lblRole.TabIndex = 1;
            this.lblRole.Text = "角色:";
            //
            // cboRole - 角色选择下拉框（操作员/技术员）
            //
            this.cboRole.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboRole.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.cboRole.FormattingEnabled = true;
            this.cboRole.Items.AddRange(new object[] {
                "操作员",
                "技术员"});
            this.cboRole.Location = new System.Drawing.Point(120, 59);
            this.cboRole.Name = "cboRole";
            this.cboRole.Size = new System.Drawing.Size(220, 25);
            this.cboRole.TabIndex = 2;
            this.cboRole.SelectedIndexChanged += new System.EventHandler(this.cboRole_SelectedIndexChanged);
            //
            // lblCurrentUsername - "当前角色:"标签
            //
            this.lblCurrentUsername.AutoSize = true;
            this.lblCurrentUsername.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblCurrentUsername.Location = new System.Drawing.Point(30, 97);
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
            this.lblCurrentUsernameValue.Location = new System.Drawing.Point(120, 97);
            this.lblCurrentUsernameValue.Name = "lblCurrentUsernameValue";
            this.lblCurrentUsernameValue.Size = new System.Drawing.Size(39, 17);
            this.lblCurrentUsernameValue.TabIndex = 4;
            this.lblCurrentUsernameValue.Text = "操作员";
            //
            // lblAccount - "账号:"标签
            //
            this.lblAccount.AutoSize = true;
            this.lblAccount.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblAccount.Location = new System.Drawing.Point(30, 132);
            this.lblAccount.Name = "lblAccount";
            this.lblAccount.Size = new System.Drawing.Size(39, 17);
            this.lblAccount.TabIndex = 5;
            this.lblAccount.Text = "账号:";
            //
            // cboAccount - 账号选择下拉框（列出该角色已有账号）
            //
            this.cboAccount.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboAccount.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.cboAccount.FormattingEnabled = true;
            this.cboAccount.Location = new System.Drawing.Point(120, 129);
            this.cboAccount.Name = "cboAccount";
            this.cboAccount.Size = new System.Drawing.Size(220, 25);
            this.cboAccount.TabIndex = 6;
            this.cboAccount.SelectedIndexChanged += new System.EventHandler(this.cboAccount_SelectedIndexChanged);
            //
            // lblNewUsername - "新用户名:"标签
            //
            this.lblNewUsername.AutoSize = true;
            this.lblNewUsername.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblNewUsername.Location = new System.Drawing.Point(30, 167);
            this.lblNewUsername.Name = "lblNewUsername";
            this.lblNewUsername.Size = new System.Drawing.Size(69, 17);
            this.lblNewUsername.TabIndex = 7;
            this.lblNewUsername.Text = "新用户名:";
            //
            // txtNewUsername - 新用户名输入框
            //
            this.txtNewUsername.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.txtNewUsername.Location = new System.Drawing.Point(120, 164);
            this.txtNewUsername.Name = "txtNewUsername";
            this.txtNewUsername.Size = new System.Drawing.Size(220, 23);
            this.txtNewUsername.TabIndex = 8;
            //
            // lblNewPassword - "新密码:"标签
            //
            this.lblNewPassword.AutoSize = true;
            this.lblNewPassword.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblNewPassword.Location = new System.Drawing.Point(30, 202);
            this.lblNewPassword.Name = "lblNewPassword";
            this.lblNewPassword.Size = new System.Drawing.Size(54, 17);
            this.lblNewPassword.TabIndex = 9;
            this.lblNewPassword.Text = "新密码:";
            //
            // txtNewPassword - 新密码输入框（密码模式）
            //
            this.txtNewPassword.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.txtNewPassword.Location = new System.Drawing.Point(120, 199);
            this.txtNewPassword.Name = "txtNewPassword";
            this.txtNewPassword.Size = new System.Drawing.Size(220, 23);
            this.txtNewPassword.TabIndex = 10;
            this.txtNewPassword.UseSystemPasswordChar = true;
            //
            // lblConfirmPassword - "确认密码:"标签
            //
            this.lblConfirmPassword.AutoSize = true;
            this.lblConfirmPassword.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblConfirmPassword.Location = new System.Drawing.Point(30, 237);
            this.lblConfirmPassword.Name = "lblConfirmPassword";
            this.lblConfirmPassword.Size = new System.Drawing.Size(69, 17);
            this.lblConfirmPassword.TabIndex = 11;
            this.lblConfirmPassword.Text = "确认密码:";
            //
            // txtConfirmPassword - 确认密码输入框（密码模式）
            //
            this.txtConfirmPassword.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.txtConfirmPassword.Location = new System.Drawing.Point(120, 234);
            this.txtConfirmPassword.Name = "txtConfirmPassword";
            this.txtConfirmPassword.Size = new System.Drawing.Size(220, 23);
            this.txtConfirmPassword.TabIndex = 12;
            this.txtConfirmPassword.UseSystemPasswordChar = true;
            //
            // btnAddAccount - 添加账号按钮
            //
            this.btnAddAccount.BackColor = System.Drawing.Color.SteelBlue;
            this.btnAddAccount.ForeColor = System.Drawing.Color.White;
            this.btnAddAccount.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnAddAccount.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.btnAddAccount.Location = new System.Drawing.Point(20, 282);
            this.btnAddAccount.Name = "btnAddAccount";
            this.btnAddAccount.Size = new System.Drawing.Size(85, 32);
            this.btnAddAccount.TabIndex = 13;
            this.btnAddAccount.Text = "添加账号";
            this.btnAddAccount.UseVisualStyleBackColor = false;
            this.btnAddAccount.Click += new System.EventHandler(this.btnAddAccount_Click);
            //
            // btnDeleteAccount - 删除账号按钮
            //
            this.btnDeleteAccount.BackColor = System.Drawing.Color.OrangeRed;
            this.btnDeleteAccount.ForeColor = System.Drawing.Color.White;
            this.btnDeleteAccount.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnDeleteAccount.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.btnDeleteAccount.Location = new System.Drawing.Point(110, 282);
            this.btnDeleteAccount.Name = "btnDeleteAccount";
            this.btnDeleteAccount.Size = new System.Drawing.Size(85, 32);
            this.btnDeleteAccount.TabIndex = 14;
            this.btnDeleteAccount.Text = "删除账号";
            this.btnDeleteAccount.UseVisualStyleBackColor = false;
            this.btnDeleteAccount.Click += new System.EventHandler(this.btnDeleteAccount_Click);
            //
            // btnApply - 应用修改按钮
            //
            this.btnApply.BackColor = System.Drawing.Color.LimeGreen;
            this.btnApply.ForeColor = System.Drawing.Color.White;
            this.btnApply.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnApply.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.btnApply.Location = new System.Drawing.Point(200, 282);
            this.btnApply.Name = "btnApply";
            this.btnApply.Size = new System.Drawing.Size(85, 32);
            this.btnApply.TabIndex = 15;
            this.btnApply.Text = "应用修改";
            this.btnApply.UseVisualStyleBackColor = false;
            this.btnApply.Click += new System.EventHandler(this.btnApply_Click);
            //
            // btnClose - 关闭按钮
            //
            this.btnClose.BackColor = System.Drawing.Color.DimGray;
            this.btnClose.ForeColor = System.Drawing.Color.White;
            this.btnClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnClose.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.btnClose.Location = new System.Drawing.Point(290, 282);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(90, 32);
            this.btnClose.TabIndex = 16;
            this.btnClose.Text = "关闭";
            this.btnClose.UseVisualStyleBackColor = false;
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
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.ClientSize = new System.Drawing.Size(400, 335);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.btnApply);
            this.Controls.Add(this.btnDeleteAccount);
            this.Controls.Add(this.btnAddAccount);
            this.Controls.Add(this.txtConfirmPassword);
            this.Controls.Add(this.lblConfirmPassword);
            this.Controls.Add(this.txtNewPassword);
            this.Controls.Add(this.lblNewPassword);
            this.Controls.Add(this.txtNewUsername);
            this.Controls.Add(this.lblNewUsername);
            this.Controls.Add(this.cboAccount);
            this.Controls.Add(this.lblAccount);
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
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblRole;
        private System.Windows.Forms.ComboBox cboRole;
        private System.Windows.Forms.Label lblCurrentUsername;
        private System.Windows.Forms.Label lblCurrentUsernameValue;
        private System.Windows.Forms.Label lblAccount;
        private System.Windows.Forms.ComboBox cboAccount;
        private System.Windows.Forms.Label lblNewUsername;
        private System.Windows.Forms.TextBox txtNewUsername;
        private System.Windows.Forms.Label lblNewPassword;
        private System.Windows.Forms.TextBox txtNewPassword;
        private System.Windows.Forms.Label lblConfirmPassword;
        private System.Windows.Forms.TextBox txtConfirmPassword;
        private System.Windows.Forms.Button btnAddAccount;
        private System.Windows.Forms.Button btnDeleteAccount;
        private System.Windows.Forms.Button btnApply;
        private System.Windows.Forms.Button btnClose;
    }
}
