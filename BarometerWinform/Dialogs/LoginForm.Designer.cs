namespace BarometerWinform.Dialogs
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
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblUsername = new System.Windows.Forms.Label();
            this.lblPassword = new System.Windows.Forms.Label();
            this.txtUsername = new System.Windows.Forms.TextBox();
            this.txtPassword = new System.Windows.Forms.TextBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // lblTitle - 标题（显示"切换为 XXX 权限"）
            //
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("微软雅黑", 11F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Location = new System.Drawing.Point(50, 25);
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
            this.lblUsername.Location = new System.Drawing.Point(30, 75);
            this.lblUsername.Name = "lblUsername";
            this.lblUsername.Size = new System.Drawing.Size(54, 17);
            this.lblUsername.TabIndex = 1;
            this.lblUsername.Text = "用户名:";
            //
            // lblPassword - "密  码:"标签
            //
            this.lblPassword.AutoSize = true;
            this.lblPassword.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblPassword.Location = new System.Drawing.Point(30, 115);
            this.lblPassword.Name = "lblPassword";
            this.lblPassword.Size = new System.Drawing.Size(54, 17);
            this.lblPassword.TabIndex = 3;
            this.lblPassword.Text = "密  码:";
            //
            // txtUsername - 用户名输入框
            //
            this.txtUsername.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.txtUsername.Location = new System.Drawing.Point(100, 72);
            this.txtUsername.Name = "txtUsername";
            this.txtUsername.Size = new System.Drawing.Size(220, 23);
            this.txtUsername.TabIndex = 2;
            this.txtUsername.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txtUsername_KeyPress);
            //
            // txtPassword - 密码输入框（密码模式：输入显示为 *）
            //
            this.txtPassword.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.txtPassword.Location = new System.Drawing.Point(100, 112);
            this.txtPassword.Name = "txtPassword";
            this.txtPassword.Size = new System.Drawing.Size(220, 23);
            this.txtPassword.TabIndex = 4;
            // UseSystemPasswordChar=true：使用系统默认的密码字符（圆点）显示输入内容
            this.txtPassword.UseSystemPasswordChar = true;
            this.txtPassword.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txtPassword_KeyPress);
            //
            // btnOK - 确认按钮
            //
            this.btnOK.BackColor = System.Drawing.Color.LimeGreen;
            this.btnOK.ForeColor = System.Drawing.Color.White;
            this.btnOK.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnOK.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.btnOK.Location = new System.Drawing.Point(100, 165);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(100, 32);
            this.btnOK.TabIndex = 5;
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
            this.btnCancel.Location = new System.Drawing.Point(220, 165);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(100, 32);
            this.btnCancel.TabIndex = 6;
            this.btnCancel.Text = "取消";
            this.btnCancel.UseVisualStyleBackColor = false;
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
            // 固定边框，禁止拖动调整大小
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.ClientSize = new System.Drawing.Size(380, 220);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
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
        private System.Windows.Forms.Label lblTitle;
        /// <summary>"用户名:"标签</summary>
        private System.Windows.Forms.Label lblUsername;
        /// <summary>"密  码:"标签</summary>
        private System.Windows.Forms.Label lblPassword;
        /// <summary>用户名输入框</summary>
        private System.Windows.Forms.TextBox txtUsername;
        /// <summary>密码输入框（密码模式）</summary>
        private System.Windows.Forms.TextBox txtPassword;
        /// <summary>确认按钮</summary>
        private System.Windows.Forms.Button btnOK;
        /// <summary>取消按钮</summary>
        private System.Windows.Forms.Button btnCancel;
    }
}
