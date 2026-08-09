namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 修改密码窗体 —— 设计器自动生成部分
    ///
    /// 【说明】
    /// 任意已登录用户修改自己密码时弹出，需验证当前密码。
    ///
    /// 窗体布局：
    /// ┌──────────────────────────────┐
    /// │        修改密码               │
    /// ├──────────────────────────────┤
    /// │  当前用户:  operator          │
    /// │  当前密码: [________________] │
    /// │  新  密  码: [________________]│
    /// │  确认密码: [________________] │
    /// ├──────────────────────────────┤
    /// │       [确认]    [取消]       │
    /// └──────────────────────────────┘
    /// </summary>
    partial class ChangePasswordForm
    {
        /// <summary>必需的设计器变量</summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false</param>
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
            this.lblUser = new System.Windows.Forms.Label();
            this.lblUserValue = new System.Windows.Forms.Label();
            this.lblCurrentPassword = new System.Windows.Forms.Label();
            this.txtCurrentPassword = new System.Windows.Forms.TextBox();
            this.lblNewPassword = new System.Windows.Forms.Label();
            this.txtNewPassword = new System.Windows.Forms.TextBox();
            this.lblConfirmPassword = new System.Windows.Forms.Label();
            this.txtConfirmPassword = new System.Windows.Forms.TextBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // lblTitle - 标题
            //
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("微软雅黑", 11F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Location = new System.Drawing.Point(150, 20);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(80, 20);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "修改密码";
            //
            // lblUser - "当前用户:"标签
            //
            this.lblUser.AutoSize = true;
            this.lblUser.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblUser.Location = new System.Drawing.Point(30, 62);
            this.lblUser.Name = "lblUser";
            this.lblUser.Size = new System.Drawing.Size(69, 17);
            this.lblUser.TabIndex = 1;
            this.lblUser.Text = "当前用户:";
            //
            // lblUserValue - 当前登录用户名（运行时由 Load 事件设置）
            //
            this.lblUserValue.AutoSize = true;
            this.lblUserValue.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold);
            this.lblUserValue.ForeColor = System.Drawing.Color.DarkBlue;
            this.lblUserValue.Location = new System.Drawing.Point(110, 62);
            this.lblUserValue.Name = "lblUserValue";
            this.lblUserValue.Size = new System.Drawing.Size(0, 17);
            this.lblUserValue.TabIndex = 2;
            this.lblUserValue.Text = "";
            //
            // lblCurrentPassword - "当前密码:"标签
            //
            this.lblCurrentPassword.AutoSize = true;
            this.lblCurrentPassword.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblCurrentPassword.Location = new System.Drawing.Point(30, 97);
            this.lblCurrentPassword.Name = "lblCurrentPassword";
            this.lblCurrentPassword.Size = new System.Drawing.Size(69, 17);
            this.lblCurrentPassword.TabIndex = 3;
            this.lblCurrentPassword.Text = "当前密码:";
            //
            // txtCurrentPassword - 当前密码输入框（密码模式）
            //
            this.txtCurrentPassword.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.txtCurrentPassword.Location = new System.Drawing.Point(110, 94);
            this.txtCurrentPassword.Name = "txtCurrentPassword";
            this.txtCurrentPassword.Size = new System.Drawing.Size(250, 23);
            this.txtCurrentPassword.TabIndex = 4;
            this.txtCurrentPassword.UseSystemPasswordChar = true;
            //
            // lblNewPassword - "新密码:"标签
            //
            this.lblNewPassword.AutoSize = true;
            this.lblNewPassword.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblNewPassword.Location = new System.Drawing.Point(30, 132);
            this.lblNewPassword.Name = "lblNewPassword";
            this.lblNewPassword.Size = new System.Drawing.Size(54, 17);
            this.lblNewPassword.TabIndex = 5;
            this.lblNewPassword.Text = "新密码:";
            //
            // txtNewPassword - 新密码输入框（密码模式）
            //
            this.txtNewPassword.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.txtNewPassword.Location = new System.Drawing.Point(110, 129);
            this.txtNewPassword.Name = "txtNewPassword";
            this.txtNewPassword.Size = new System.Drawing.Size(250, 23);
            this.txtNewPassword.TabIndex = 6;
            this.txtNewPassword.UseSystemPasswordChar = true;
            //
            // lblConfirmPassword - "确认密码:"标签
            //
            this.lblConfirmPassword.AutoSize = true;
            this.lblConfirmPassword.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblConfirmPassword.Location = new System.Drawing.Point(30, 167);
            this.lblConfirmPassword.Name = "lblConfirmPassword";
            this.lblConfirmPassword.Size = new System.Drawing.Size(69, 17);
            this.lblConfirmPassword.TabIndex = 7;
            this.lblConfirmPassword.Text = "确认密码:";
            //
            // txtConfirmPassword - 确认密码输入框（密码模式）
            //
            this.txtConfirmPassword.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.txtConfirmPassword.Location = new System.Drawing.Point(110, 164);
            this.txtConfirmPassword.Name = "txtConfirmPassword";
            this.txtConfirmPassword.Size = new System.Drawing.Size(250, 23);
            this.txtConfirmPassword.TabIndex = 8;
            this.txtConfirmPassword.UseSystemPasswordChar = true;
            //
            // btnOK - 确认按钮
            //
            this.btnOK.BackColor = System.Drawing.Color.LimeGreen;
            this.btnOK.ForeColor = System.Drawing.Color.White;
            this.btnOK.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnOK.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.btnOK.Location = new System.Drawing.Point(110, 210);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(100, 32);
            this.btnOK.TabIndex = 9;
            this.btnOK.Text = "确认";
            this.btnOK.UseVisualStyleBackColor = false;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            //
            // btnCancel - 取消按钮
            //
            this.btnCancel.BackColor = System.Drawing.Color.DimGray;
            this.btnCancel.ForeColor = System.Drawing.Color.White;
            this.btnCancel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCancel.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.btnCancel.Location = new System.Drawing.Point(260, 210);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(100, 32);
            this.btnCancel.TabIndex = 10;
            this.btnCancel.Text = "取消";
            this.btnCancel.UseVisualStyleBackColor = false;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            //
            // ChangePasswordForm - 窗体属性
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.ClientSize = new System.Drawing.Size(390, 260);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.txtConfirmPassword);
            this.Controls.Add(this.lblConfirmPassword);
            this.Controls.Add(this.txtNewPassword);
            this.Controls.Add(this.lblNewPassword);
            this.Controls.Add(this.txtCurrentPassword);
            this.Controls.Add(this.lblCurrentPassword);
            this.Controls.Add(this.lblUserValue);
            this.Controls.Add(this.lblUser);
            this.Controls.Add(this.lblTitle);
            this.Name = "ChangePasswordForm";
            this.Text = "修改密码";
            this.Load += new System.EventHandler(this.ChangePasswordForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        // ===== 控件字段声明 =====
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblUser;
        private System.Windows.Forms.Label lblUserValue;
        private System.Windows.Forms.Label lblCurrentPassword;
        private System.Windows.Forms.TextBox txtCurrentPassword;
        private System.Windows.Forms.Label lblNewPassword;
        private System.Windows.Forms.TextBox txtNewPassword;
        private System.Windows.Forms.Label lblConfirmPassword;
        private System.Windows.Forms.TextBox txtConfirmPassword;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
    }
}
