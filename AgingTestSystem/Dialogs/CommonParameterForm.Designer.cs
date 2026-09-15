namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 公共参数窗口（设置所有气压表负压阈值）—— 设计器自动生成部分
    /// 【界面布局】（所有控件居中显示）
    /// ┌───────────────────────────────┐
    /// │  负压值设定(kPa)：[  -5.0  ]  │ ← Label + 数值框（支持正负数，水平居中成一组）
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
            this.lblThreshold = new Sunny.UI.UILabel();
            this.nudThreshold = new System.Windows.Forms.NumericUpDown();
            this.btnSave = new Sunny.UI.UIButton();
            this.SuspendLayout();
            // 
            // lblThreshold
            // lblThreshold（Y=65：UIForm 自绘蓝标题占 35px 客户区，设计值即运行值，所见即所得；
            // CenterControls 只调水平居中、不动 Y，见 .cs 注释）
            // X=36：运行时刻整组居中真值（探针实测 AutoSize 实宽 146 + 间距 10 + 数值框 91 = 247，
            // (320-247)/2=36；Designer 残留 Size 107 是旧字体过期值，勿用它算），
            // 设计器按运行值摆，所见即所得（V1.72.3：标签实宽口径修正后同步）
            //
            this.lblThreshold.AutoSize = true;
            this.lblThreshold.Location = new System.Drawing.Point(36, 65);
            this.lblThreshold.Name = "lblThreshold";
            this.lblThreshold.Size = new System.Drawing.Size(107, 12);
            this.lblThreshold.TabIndex = 4;
            this.lblThreshold.Text = "负压值设定(kPa)：";
            // 
            // nudThreshold - 负压值数值框（支持正负数，范围 -9999~9999）
            // X=192：运行时刻居中真值（36 + 标签实宽 146 + 间距 10；探针实测，见 lbl 注释），
            // 设计器按运行值摆（V1.72.3 同步）
            //
            this.nudThreshold.DecimalPlaces = 1;
            this.nudThreshold.Increment = 1m;
            this.nudThreshold.Location = new System.Drawing.Point(192, 62);
            this.nudThreshold.Maximum = 9999m;
            this.nudThreshold.Minimum = -9999m;
            this.nudThreshold.Name = "nudThreshold";
            this.nudThreshold.Size = new System.Drawing.Size(91, 21);
            this.nudThreshold.TabIndex = 3;
            this.nudThreshold.Value = -5m;
            // 
            // btnSave - 保存按钮（主按钮蓝；深浅两色下蓝底白字都清晰）
            //
            this.btnSave.FillColor = System.Drawing.Color.DodgerBlue;
            this.btnSave.RectColor = System.Drawing.Color.DodgerBlue;
            this.btnSave.ForeColor = System.Drawing.Color.White;
            this.btnSave.Style = Sunny.UI.UIStyle.Custom;
            this.btnSave.Location = new System.Drawing.Point(112, 110);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(96, 32);
            this.btnSave.TabIndex = 2;
            this.btnSave.Text = "保存设置";
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // CommonParameterForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(320, 175);
            // 绝对布局：禁缩小（MinimumSize=ClientSize），防缩坏布局；可放大。
            this.MinimumSize = new System.Drawing.Size(320, 175);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.nudThreshold);
            this.Controls.Add(this.lblThreshold);
            // UIForm 自绘蓝标题：删 FormBorderStyle；Y 已含 35px 标题区（CenterControls 只居中 X）。
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CommonParameterForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "公共参数窗口";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private Sunny.UI.UILabel lblThreshold;
        private System.Windows.Forms.NumericUpDown nudThreshold;
        private Sunny.UI.UIButton btnSave;
    }
}
