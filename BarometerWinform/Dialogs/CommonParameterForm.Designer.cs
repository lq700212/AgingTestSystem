namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 负压阈值设置窗体 —— 设计器自动生成部分
    ///
    /// 【界面布局】（所有控件居中显示）
    /// ┌───────────────────────────────┐
    /// │     负压值设定：[_________]     │ ← Label + 输入框（水平居中成一组）
    /// │         [  保存设置   ]         │ ← 保存按钮（水平居中）
    /// └───────────────────────────────┘
    /// 说明：控件在 InitializeComponent 里按固定位置摆放，
    ///       再在业务代码（CommonParameterForm.cs）的 CenterControls 里
    ///       根据窗体宽度动态居中一次，保证不同分辨率下都居中。
    /// </summary>
    partial class CommonParameterForm
    {
        private System.ComponentModel.IContainer components = null;

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
            this.lblThreshold = new System.Windows.Forms.Label();
            this.txtThreshold = new System.Windows.Forms.TextBox();
            this.btnSave = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lblThreshold
            // 
            this.lblThreshold.AutoSize = true;
            this.lblThreshold.Location = new System.Drawing.Point(40, 30);
            this.lblThreshold.Name = "lblThreshold";
            this.lblThreshold.Size = new System.Drawing.Size(77, 12);
            this.lblThreshold.TabIndex = 4;
            this.lblThreshold.Text = "负压值设定：";
            // 
            // txtThreshold
            // 
            this.txtThreshold.Location = new System.Drawing.Point(127, 27);
            this.txtThreshold.Name = "txtThreshold";
            this.txtThreshold.Size = new System.Drawing.Size(130, 21);
            this.txtThreshold.TabIndex = 3;
            this.txtThreshold.Text = "-95";
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(112, 75);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(96, 32);
            this.btnSave.TabIndex = 2;
            this.btnSave.Text = "保存设置";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // CommonParameterForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(320, 140);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.txtThreshold);
            this.Controls.Add(this.lblThreshold);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CommonParameterForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "公共参数窗口";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblThreshold;
        private System.Windows.Forms.TextBox txtThreshold;
        private System.Windows.Forms.Button btnSave;
    }
}
