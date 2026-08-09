namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 公共参数窗口（设置所有气压表负压阈值）—— 设计器自动生成部分
    ///
    /// 【界面布局】（所有控件居中显示）
    /// ┌───────────────────────────────┐
    /// │  负压值设定(kPa)：[  -95.0  ]  │ ← Label + 数值框（支持正负数，水平居中成一组）
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
            this.nudThreshold = new System.Windows.Forms.NumericUpDown();
            this.btnSave = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lblThreshold
            // 
            this.lblThreshold.AutoSize = true;
            this.lblThreshold.Location = new System.Drawing.Point(54, 30);
            this.lblThreshold.Name = "lblThreshold";
            this.lblThreshold.Size = new System.Drawing.Size(107, 12);
            this.lblThreshold.TabIndex = 4;
            this.lblThreshold.Text = "负压值设定(kPa)：";
            // 
            // nudThreshold - 负压值数值框（支持正负数，范围 -9999~9999）
            // 
            this.nudThreshold.DecimalPlaces = 1;
            this.nudThreshold.Increment = 1m;
            this.nudThreshold.Location = new System.Drawing.Point(167, 27);
            this.nudThreshold.Maximum = 9999m;
            this.nudThreshold.Minimum = -9999m;
            this.nudThreshold.Name = "nudThreshold";
            this.nudThreshold.Size = new System.Drawing.Size(91, 21);
            this.nudThreshold.TabIndex = 3;
            this.nudThreshold.Value = -95m;
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
            this.Controls.Add(this.nudThreshold);
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
        private System.Windows.Forms.NumericUpDown nudThreshold;
        private System.Windows.Forms.Button btnSave;
    }
}
