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
    /// │ 用户名:      [operator ▼ (可编辑)]       │
    /// │ 新密码:      [____________________]     │
    /// │ 确认密码:    [____________________]     │
    /// ├──────────────────────────────────────────┤
    /// │ [添加账号][删除账号][应用修改][关闭]      │
    /// └──────────────────────────────────────────┘
    ///
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
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblRole = new System.Windows.Forms.Label();
            this.cboRole = new System.Windows.Forms.ComboBox();
            this.lblCurrentUsername = new System.Windows.Forms.Label();
            this.lblCurrentUsernameValue = new System.Windows.Forms.Label();
            this.lblUsername = new System.Windows.Forms.Label();
            this.cboUsername = new System.Windows.Forms.ComboBox();
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
            // lblUsername - "用户名:"标签
            //
            this.lblUsername.AutoSize = true;
            this.lblUsername.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblUsername.Location = new System.Drawing.Point(30, 132);
            this.lblUsername.Name = "lblUsername";
            this.lblUsername.Size = new System.Drawing.Size(54, 17);
            this.lblUsername.TabIndex = 5;
            this.lblUsername.Text = "用户名:";
            //
            // cboUsername - 用户名下拉框（可编辑）
            // 点击展开显示当前角色下已创建的全部账号，供选择要修改的目标账号；
            // 也可直接输入新用户名（用于改用户名）。
            //
            this.cboUsername.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDown;
            this.cboUsername.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.cboUsername.FormattingEnabled = true;
            this.cboUsername.Location = new System.Drawing.Point(120, 129);
            this.cboUsername.Name = "cboUsername";
            this.cboUsername.Size = new System.Drawing.Size(220, 25);
            this.cboUsername.TabIndex = 6;
            this.cboUsername.SelectedIndexChanged += new System.EventHandler(this.cboUsername_SelectedIndexChanged);
            //
            // lblNewPassword - "新密码:"标签
            //
            this.lblNewPassword.AutoSize = true;
            this.lblNewPassword.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblNewPassword.Location = new System.Drawing.Point(30, 167);
            this.lblNewPassword.Name = "lblNewPassword";
            this.lblNewPassword.Size = new System.Drawing.Size(54, 17);
            this.lblNewPassword.TabIndex = 7;
            this.lblNewPassword.Text = "新密码:";
            //
            // txtNewPassword - 新密码输入框（密码模式）
            //
            this.txtNewPassword.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.txtNewPassword.Location = new System.Drawing.Point(120, 164);
            this.txtNewPassword.Name = "txtNewPassword";
            this.txtNewPassword.Size = new System.Drawing.Size(220, 23);
            this.txtNewPassword.TabIndex = 8;
            this.txtNewPassword.UseSystemPasswordChar = true;
            //
            // lblConfirmPassword - "确认密码:"标签
            //
            this.lblConfirmPassword.AutoSize = true;
            this.lblConfirmPassword.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblConfirmPassword.Location = new System.Drawing.Point(30, 202);
            this.lblConfirmPassword.Name = "lblConfirmPassword";
            this.lblConfirmPassword.Size = new System.Drawing.Size(69, 17);
            this.lblConfirmPassword.TabIndex = 9;
            this.lblConfirmPassword.Text = "确认密码:";
            //
            // txtConfirmPassword - 确认密码输入框（密码模式）
            //
            this.txtConfirmPassword.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.txtConfirmPassword.Location = new System.Drawing.Point(120, 199);
            this.txtConfirmPassword.Name = "txtConfirmPassword";
            this.txtConfirmPassword.Size = new System.Drawing.Size(220, 23);
            this.txtConfirmPassword.TabIndex = 10;
            this.txtConfirmPassword.UseSystemPasswordChar = true;
            //
            // btnAddAccount - 添加账号按钮
            //
            this.btnAddAccount.BackColor = System.Drawing.Color.SteelBlue;
            this.btnAddAccount.ForeColor = System.Drawing.Color.White;
            this.btnAddAccount.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnAddAccount.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.btnAddAccount.Location = new System.Drawing.Point(20, 247);
            this.btnAddAccount.Name = "btnAddAccount";
            this.btnAddAccount.Size = new System.Drawing.Size(85, 32);
            this.btnAddAccount.TabIndex = 11;
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
            this.btnDeleteAccount.Location = new System.Drawing.Point(110, 247);
            this.btnDeleteAccount.Name = "btnDeleteAccount";
            this.btnDeleteAccount.Size = new System.Drawing.Size(85, 32);
            this.btnDeleteAccount.TabIndex = 12;
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
            this.btnApply.Location = new System.Drawing.Point(200, 247);
            this.btnApply.Name = "btnApply";
            this.btnApply.Size = new System.Drawing.Size(85, 32);
            this.btnApply.TabIndex = 13;
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
            this.btnClose.Location = new System.Drawing.Point(290, 247);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(90, 32);
            this.btnClose.TabIndex = 14;
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
            this.ClientSize = new System.Drawing.Size(400, 300);
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
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblRole;
        private System.Windows.Forms.ComboBox cboRole;
        private System.Windows.Forms.Label lblCurrentUsername;
        private System.Windows.Forms.Label lblCurrentUsernameValue;
        private System.Windows.Forms.Label lblUsername;
        private System.Windows.Forms.ComboBox cboUsername;
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
