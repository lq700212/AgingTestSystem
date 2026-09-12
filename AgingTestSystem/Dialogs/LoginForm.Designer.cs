namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 登录窗体 —— 设计器自动生成部分
    ///
    /// 【说明】
    /// 本文件由 Visual Studio 设计器维护，包含所有控件的创建和布局代码。
    /// 业务逻辑代码请放在 LoginForm.cs 文件中。
    ///
    /// 窗体布局：
    /// ┌──────────────────────────────┐
    /// │       切换为 XXX权限          │
    /// ├──────────────────────────────┤
    /// │  用户名: [________________]  │
    /// │  密  码: [________________]  │
    /// ├──────────────────────────────┤
    /// │       [确认]    [取消]       │
    /// └──────────────────────────────┘
    /// </summary>
    partial class LoginForm
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

        /// <summary>
        /// 设计器支持所需的方法 - 不要使用代码编辑器修改此方法的内容
        /// </summary>
        private void InitializeComponent()
        {
            this.lblTitle = new Sunny.UI.UILabel();
            this.lblUsername = new Sunny.UI.UILabel();
            this.lblPassword = new Sunny.UI.UILabel();
            this.txtUsername = new Sunny.UI.UIComboBox();
            this.txtPassword = new Sunny.UI.UITextBox();
            this.chkRemember = new System.Windows.Forms.CheckBox();
            this.btnOK = new Sunny.UI.UIButton();
            this.btnCancel = new Sunny.UI.UIButton();
            this.SuspendLayout();
            //
            // lblTitle - 标题（显示"切换为 XXX 权限"）
            //
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("微软雅黑", 11F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Location = new System.Drawing.Point(50, 60);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(0, 20);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "登录";
            this.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // lblUsername - "用户名:"标签
            //
            this.lblUsername.AutoSize = true;
            this.lblUsername.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblUsername.Location = new System.Drawing.Point(30, 110);
            this.lblUsername.Name = "lblUsername";
            this.lblUsername.Size = new System.Drawing.Size(54, 17);
            this.lblUsername.TabIndex = 1;
            this.lblUsername.Text = "用户名:";
            //
            // lblPassword - "密  码:"标签
            //
            this.lblPassword.AutoSize = true;
            this.lblPassword.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblPassword.Location = new System.Drawing.Point(30, 150);
            this.lblPassword.Name = "lblPassword";
            this.lblPassword.Size = new System.Drawing.Size(54, 17);
            this.lblPassword.TabIndex = 3;
            this.lblPassword.Text = "密  码:";
            //
            // txtUsername - 用户名下拉框（列出该角色已有账号，可下拉选择或手动输入）
            //
            this.txtUsername.DropDownStyle = Sunny.UI.UIDropDownStyle.DropDown;
            this.txtUsername.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.txtUsername.FormattingEnabled = true;
            this.txtUsername.Location = new System.Drawing.Point(100, 107);
            this.txtUsername.Name = "txtUsername";
            this.txtUsername.Size = new System.Drawing.Size(220, 25);
            this.txtUsername.TabIndex = 2;
            this.txtUsername.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txtUsername_KeyPress);
            //
            // txtPassword - 密码输入框（密码模式：输入显示为 *）
            //
            this.txtPassword.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.txtPassword.Location = new System.Drawing.Point(100, 147);
            this.txtPassword.Name = "txtPassword";
            this.txtPassword.Size = new System.Drawing.Size(220, 23);
            this.txtPassword.TabIndex = 4;
            // PasswordChar='*'：SunnyUI 文本框用字符密码模式（无 UseSystemPasswordChar）
            this.txtPassword.PasswordChar = '*';
            this.txtPassword.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txtPassword_KeyPress);
            //
            // chkRemember - 记住密码复选框
            //
            this.chkRemember.AutoSize = true;
            this.chkRemember.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.chkRemember.Location = new System.Drawing.Point(100, 183);
            this.chkRemember.Name = "chkRemember";
            this.chkRemember.Size = new System.Drawing.Size(75, 21);
            this.chkRemember.TabIndex = 5;
            this.chkRemember.Text = "记住密码";
            this.chkRemember.UseVisualStyleBackColor = true;
            //
            // btnOK - 确认按钮（语义绿：Sunny 自绘按钮走 Style=Custom+FillColor，原生 BackColor 画不出来）
            //
            this.btnOK.FillColor = System.Drawing.Color.LimeGreen;
            this.btnOK.RectColor = System.Drawing.Color.LimeGreen;
            this.btnOK.ForeColor = System.Drawing.Color.White;
            this.btnOK.Style = Sunny.UI.UIStyle.Custom;
            this.btnOK.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.btnOK.Location = new System.Drawing.Point(100, 215);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(100, 32);
            this.btnOK.TabIndex = 6;
            this.btnOK.Text = "确认";
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            //
            // btnCancel - 取消按钮（语义灰，同上走 Custom+FillColor）
            //
            this.btnCancel.FillColor = System.Drawing.Color.DimGray;
            this.btnCancel.RectColor = System.Drawing.Color.DimGray;
            this.btnCancel.ForeColor = System.Drawing.Color.White;
            this.btnCancel.Style = Sunny.UI.UIStyle.Custom;
            this.btnCancel.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.btnCancel.Location = new System.Drawing.Point(220, 215);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(100, 32);
            this.btnCancel.TabIndex = 7;
            this.btnCancel.Text = "取消";
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            //
            // LoginForm - 登录窗体自身属性设置
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            // 禁用最大化按钮（登录窗体不需要最大化）
            this.MaximizeBox = false;
            // 禁用最小化按钮（模态对话框，不应最小化）
            this.MinimizeBox = false;
            // 居中显示在父窗体上
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            // 【V1.71】UIForm 自绘蓝标题（约 35px），内容整体下移 35px，窗体同步加高；
            // FormBorderStyle 删除（UIForm 自己管边框，固定值会盖掉自绘标题）。
            this.ClientSize = new System.Drawing.Size(380, 275);
            // 【V1.71】绝对布局：禁缩小（MinimumSize=ClientSize），防缩坏布局；可放大。
            this.MinimumSize = new System.Drawing.Size(380, 275);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.chkRemember);
            this.Controls.Add(this.txtPassword);
            this.Controls.Add(this.txtUsername);
            this.Controls.Add(this.lblPassword);
            this.Controls.Add(this.lblUsername);
            this.Controls.Add(this.lblTitle);
            this.Name = "LoginForm";
            this.Text = "用户登录";
            this.Load += new System.EventHandler(this.LoginForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        // ===== 控件字段声明（在两个 partial 文件中共享） =====

        /// <summary>标题标签（显示"切换为 XXX 权限"）</summary>
        private Sunny.UI.UILabel lblTitle;
        /// <summary>"用户名:"标签</summary>
        private Sunny.UI.UILabel lblUsername;
        /// <summary>"密  码:"标签</summary>
        private Sunny.UI.UILabel lblPassword;
        /// <summary>用户名下拉框（列出该角色已有账号）</summary>
        private Sunny.UI.UIComboBox txtUsername;
        /// <summary>密码输入框（密码模式）</summary>
        private Sunny.UI.UITextBox txtPassword;
        /// <summary>记住密码复选框</summary>
        private System.Windows.Forms.CheckBox chkRemember;
        /// <summary>确认按钮</summary>
        private Sunny.UI.UIButton btnOK;
        /// <summary>取消按钮</summary>
        private Sunny.UI.UIButton btnCancel;
    }
}
