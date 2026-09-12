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
            this.lblTitle = new Sunny.UI.UILabel();
            this.lblUser = new Sunny.UI.UILabel();
            this.lblUserValue = new Sunny.UI.UILabel();
            this.lblCurrentPassword = new Sunny.UI.UILabel();
            this.txtCurrentPassword = new Sunny.UI.UITextBox();
            this.lblNewPassword = new Sunny.UI.UILabel();
            this.txtNewPassword = new Sunny.UI.UITextBox();
            this.lblConfirmPassword = new Sunny.UI.UILabel();
            this.txtConfirmPassword = new Sunny.UI.UITextBox();
            this.btnOK = new Sunny.UI.UIButton();
            this.btnCancel = new Sunny.UI.UIButton();
            this.SuspendLayout();
            //
            // lblTitle - 标题
            //
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("微软雅黑", 11F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Location = new System.Drawing.Point(150, 55);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(80, 20);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "修改密码";
            //
            // lblUser - "当前用户:"标签
            //
            this.lblUser.AutoSize = true;
            this.lblUser.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblUser.Location = new System.Drawing.Point(30, 97);
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
            this.lblUserValue.Location = new System.Drawing.Point(110, 97);
            this.lblUserValue.Name = "lblUserValue";
            this.lblUserValue.Size = new System.Drawing.Size(0, 17);
            this.lblUserValue.TabIndex = 2;
            this.lblUserValue.Text = "";
            //
            // lblCurrentPassword - "当前密码:"标签
            //
            this.lblCurrentPassword.AutoSize = true;
            this.lblCurrentPassword.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblCurrentPassword.Location = new System.Drawing.Point(30, 132);
            this.lblCurrentPassword.Name = "lblCurrentPassword";
            this.lblCurrentPassword.Size = new System.Drawing.Size(69, 17);
            this.lblCurrentPassword.TabIndex = 3;
            this.lblCurrentPassword.Text = "当前密码:";
            //
            // txtCurrentPassword - 当前密码输入框（密码模式）
            //
            this.txtCurrentPassword.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.txtCurrentPassword.Location = new System.Drawing.Point(110, 129);
            this.txtCurrentPassword.Name = "txtCurrentPassword";
            this.txtCurrentPassword.Size = new System.Drawing.Size(250, 23);
            this.txtCurrentPassword.TabIndex = 4;
            this.txtCurrentPassword.PasswordChar = '*';
            //
            // lblNewPassword - "新密码:"标签
            //
            this.lblNewPassword.AutoSize = true;
            this.lblNewPassword.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblNewPassword.Location = new System.Drawing.Point(30, 167);
            this.lblNewPassword.Name = "lblNewPassword";
            this.lblNewPassword.Size = new System.Drawing.Size(54, 17);
            this.lblNewPassword.TabIndex = 5;
            this.lblNewPassword.Text = "新密码:";
            //
            // txtNewPassword - 新密码输入框（密码模式）
            //
            this.txtNewPassword.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.txtNewPassword.Location = new System.Drawing.Point(110, 164);
            this.txtNewPassword.Name = "txtNewPassword";
            this.txtNewPassword.Size = new System.Drawing.Size(250, 23);
            this.txtNewPassword.TabIndex = 6;
            this.txtNewPassword.PasswordChar = '*';
            //
            // lblConfirmPassword - "确认密码:"标签
            //
            this.lblConfirmPassword.AutoSize = true;
            this.lblConfirmPassword.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblConfirmPassword.Location = new System.Drawing.Point(30, 202);
            this.lblConfirmPassword.Name = "lblConfirmPassword";
            this.lblConfirmPassword.Size = new System.Drawing.Size(69, 17);
            this.lblConfirmPassword.TabIndex = 7;
            this.lblConfirmPassword.Text = "确认密码:";
            //
            // txtConfirmPassword - 确认密码输入框（密码模式）
            //
            this.txtConfirmPassword.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.txtConfirmPassword.Location = new System.Drawing.Point(110, 199);
            this.txtConfirmPassword.Name = "txtConfirmPassword";
            this.txtConfirmPassword.Size = new System.Drawing.Size(250, 23);
            this.txtConfirmPassword.TabIndex = 8;
            this.txtConfirmPassword.PasswordChar = '*';
            //
            // btnOK - 确认按钮（主按钮蓝，与登录窗确认按钮统一走 Custom+FillColor）
            //
            this.btnOK.FillColor = System.Drawing.Color.DodgerBlue;
            this.btnOK.RectColor = System.Drawing.Color.DodgerBlue;
            this.btnOK.ForeColor = System.Drawing.Color.White;
            this.btnOK.Style = Sunny.UI.UIStyle.Custom;
            this.btnOK.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.btnOK.Location = new System.Drawing.Point(110, 245);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(100, 32);
            this.btnOK.TabIndex = 9;
            this.btnOK.Text = "确认";
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            //
            // btnCancel - 取消按钮
            //
            this.btnCancel.FillColor = System.Drawing.Color.DimGray;
            this.btnCancel.RectColor = System.Drawing.Color.DimGray;
            this.btnCancel.ForeColor = System.Drawing.Color.White;
            this.btnCancel.Style = Sunny.UI.UIStyle.Custom;
            this.btnCancel.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.btnCancel.Location = new System.Drawing.Point(260, 245);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(100, 32);
            this.btnCancel.TabIndex = 10;
            this.btnCancel.Text = "取消";
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
            // 【V1.71】UIForm 自绘蓝标题：删 FormBorderStyle，内容整体下移 35px；语义按钮走 Custom+FillColor。
            this.ClientSize = new System.Drawing.Size(390, 295);
            // 【V1.71】绝对布局：禁缩小（MinimumSize=ClientSize），防缩坏布局；可放大。
            this.MinimumSize = new System.Drawing.Size(390, 295);
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
        private Sunny.UI.UILabel lblTitle;
        private Sunny.UI.UILabel lblUser;
        private Sunny.UI.UILabel lblUserValue;
        private Sunny.UI.UILabel lblCurrentPassword;
        private Sunny.UI.UITextBox txtCurrentPassword;
        private Sunny.UI.UILabel lblNewPassword;
        private Sunny.UI.UITextBox txtNewPassword;
        private Sunny.UI.UILabel lblConfirmPassword;
        private Sunny.UI.UITextBox txtConfirmPassword;
        private Sunny.UI.UIButton btnOK;
        private Sunny.UI.UIButton btnCancel;
    }
}
