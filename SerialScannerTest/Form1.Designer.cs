namespace SerialScannerTest
{
    partial class Form1
    {
        /// <summary>
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
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
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.lblTitle = new System.Windows.Forms.Label();
            this.txtReceive = new System.Windows.Forms.TextBox();
            this.lblStatus = new System.Windows.Forms.Label();
            this.btnClear = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Location = new System.Drawing.Point(15, 15);
            this.lblTitle.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(107, 19);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "扫描数据接收区";
            // 
            // txtReceive
            // 
            this.txtReceive.BackColor = System.Drawing.Color.WhiteSmoke;
            this.txtReceive.Font = new System.Drawing.Font("Consolas", 10F);
            this.txtReceive.Location = new System.Drawing.Point(15, 38);
            this.txtReceive.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.txtReceive.Multiline = true;
            this.txtReceive.Name = "txtReceive";
            this.txtReceive.ReadOnly = true;
            this.txtReceive.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtReceive.Size = new System.Drawing.Size(406, 211);
            this.txtReceive.TabIndex = 1;
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblStatus.ForeColor = System.Drawing.Color.DarkBlue;
            this.lblStatus.Location = new System.Drawing.Point(15, 262);
            this.lblStatus.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(65, 17);
            this.lblStatus.TabIndex = 2;
            this.lblStatus.Text = "等待连接...";
            // 
            // btnClear
            // 
            this.btnClear.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnClear.Location = new System.Drawing.Point(339, 259);
            this.btnClear.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.btnClear.Name = "btnClear";
            this.btnClear.Size = new System.Drawing.Size(81, 22);
            this.btnClear.TabIndex = 3;
            this.btnClear.Text = "清空显示";
            this.btnClear.UseVisualStyleBackColor = true;
            this.btnClear.Click += new System.EventHandler(this.BtnClear_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(436, 302);
            this.Controls.Add(this.btnClear);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.txtReceive);
            this.Controls.Add(this.lblTitle);
            this.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Honeywell 扫码枪测试 Demo";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        // 控件声明（供 Form1.cs 使用）
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.TextBox txtReceive;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Button btnClear;
    }
}