namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 公共参数设置窗体 —— 设计器自动生成部分
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
            this.groupBoxCollect = new System.Windows.Forms.GroupBox();
            this.lblCollectInterval = new System.Windows.Forms.Label();
            this.numCollectInterval = new System.Windows.Forms.NumericUpDown();
            this.lblIntervalUnit = new System.Windows.Forms.Label();
            this.groupBoxAlarm = new System.Windows.Forms.GroupBox();
            this.lblAlarmThreshold = new System.Windows.Forms.Label();
            this.txtAlarmThreshold = new System.Windows.Forms.TextBox();
            this.lblAlarmUnit = new System.Windows.Forms.Label();
            this.lblMinPressure = new System.Windows.Forms.Label();
            this.txtMinPressure = new System.Windows.Forms.TextBox();
            this.lblMaxPressure = new System.Windows.Forms.Label();
            this.txtMaxPressure = new System.Windows.Forms.TextBox();
            this.lblTempLimit = new System.Windows.Forms.Label();
            this.txtTempLimit = new System.Windows.Forms.TextBox();
            this.lblTempUnit = new System.Windows.Forms.Label();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.numCollectInterval)).BeginInit();
            this.groupBoxCollect.SuspendLayout();
            this.groupBoxAlarm.SuspendLayout();
            this.SuspendLayout();
            //
            // lblCollectInterval - 采集间隔标签
            //
            this.lblCollectInterval.AutoSize = true;
            this.lblCollectInterval.Location = new System.Drawing.Point(20, 30);
            this.lblCollectInterval.Name = "lblCollectInterval";
            this.lblCollectInterval.Text = "采集间隔:";
            //
            // numCollectInterval - 采集间隔输入框
            //
            this.numCollectInterval.Location = new System.Drawing.Point(110, 28);
            this.numCollectInterval.Maximum = new decimal(new int[] {60000, 0, 0, 0});
            this.numCollectInterval.Minimum = new decimal(new int[] {100, 0, 0, 0});
            this.numCollectInterval.Name = "numCollectInterval";
            this.numCollectInterval.Size = new System.Drawing.Size(120, 21);
            this.numCollectInterval.Value = new decimal(new int[] {1000, 0, 0, 0});
            //
            // lblIntervalUnit - 采集间隔单位
            //
            this.lblIntervalUnit.AutoSize = true;
            this.lblIntervalUnit.Location = new System.Drawing.Point(240, 30);
            this.lblIntervalUnit.Name = "lblIntervalUnit";
            this.lblIntervalUnit.Text = "毫秒 (ms)";
            //
            // groupBoxCollect - 采集参数分组
            //
            this.groupBoxCollect.Controls.Add(this.lblCollectInterval);
            this.groupBoxCollect.Controls.Add(this.numCollectInterval);
            this.groupBoxCollect.Controls.Add(this.lblIntervalUnit);
            this.groupBoxCollect.Location = new System.Drawing.Point(15, 15);
            this.groupBoxCollect.Name = "groupBoxCollect";
            this.groupBoxCollect.Size = new System.Drawing.Size(360, 70);
            this.groupBoxCollect.TabStop = false;
            this.groupBoxCollect.Text = "采集参数";
            //
            // lblAlarmThreshold - 报警阈值标签
            //
            this.lblAlarmThreshold.AutoSize = true;
            this.lblAlarmThreshold.Location = new System.Drawing.Point(20, 30);
            this.lblAlarmThreshold.Name = "lblAlarmThreshold";
            this.lblAlarmThreshold.Text = "报警压力阈值:";
            //
            // txtAlarmThreshold - 报警阈值输入框
            //
            this.txtAlarmThreshold.Location = new System.Drawing.Point(110, 27);
            this.txtAlarmThreshold.Name = "txtAlarmThreshold";
            this.txtAlarmThreshold.Size = new System.Drawing.Size(120, 21);
            this.txtAlarmThreshold.Text = "-95000";
            //
            // lblAlarmUnit - 报警阈值单位
            //
            this.lblAlarmUnit.AutoSize = true;
            this.lblAlarmUnit.Location = new System.Drawing.Point(240, 30);
            this.lblAlarmUnit.Name = "lblAlarmUnit";
            this.lblAlarmUnit.Text = "Pa";
            //
            // lblMinPressure - 最小压力标签
            //
            this.lblMinPressure.AutoSize = true;
            this.lblMinPressure.Location = new System.Drawing.Point(20, 60);
            this.lblMinPressure.Name = "lblMinPressure";
            this.lblMinPressure.Text = "正常压力下限:";
            //
            // txtMinPressure - 最小压力输入框
            //
            this.txtMinPressure.Location = new System.Drawing.Point(110, 57);
            this.txtMinPressure.Name = "txtMinPressure";
            this.txtMinPressure.Size = new System.Drawing.Size(120, 21);
            this.txtMinPressure.Text = "-90000";
            //
            // lblMaxPressure - 最大压力标签
            //
            this.lblMaxPressure.AutoSize = true;
            this.lblMaxPressure.Location = new System.Drawing.Point(20, 90);
            this.lblMaxPressure.Name = "lblMaxPressure";
            this.lblMaxPressure.Text = "正常压力上限:";
            //
            // txtMaxPressure - 最大压力输入框
            //
            this.txtMaxPressure.Location = new System.Drawing.Point(110, 87);
            this.txtMaxPressure.Name = "txtMaxPressure";
            this.txtMaxPressure.Size = new System.Drawing.Size(120, 21);
            this.txtMaxPressure.Text = "-10000";
            //
            // lblTempLimit - 温度上限标签
            //
            this.lblTempLimit.AutoSize = true;
            this.lblTempLimit.Location = new System.Drawing.Point(20, 120);
            this.lblTempLimit.Name = "lblTempLimit";
            this.lblTempLimit.Text = "温度上限:";
            //
            // txtTempLimit - 温度上限输入框
            //
            this.txtTempLimit.Location = new System.Drawing.Point(110, 117);
            this.txtTempLimit.Name = "txtTempLimit";
            this.txtTempLimit.Size = new System.Drawing.Size(120, 21);
            this.txtTempLimit.Text = "85";
            //
            // lblTempUnit - 温度单位
            //
            this.lblTempUnit.AutoSize = true;
            this.lblTempUnit.Location = new System.Drawing.Point(240, 120);
            this.lblTempUnit.Name = "lblTempUnit";
            this.lblTempUnit.Text = "℃";
            //
            // groupBoxAlarm - 报警参数分组
            //
            this.groupBoxAlarm.Controls.Add(this.lblAlarmThreshold);
            this.groupBoxAlarm.Controls.Add(this.txtAlarmThreshold);
            this.groupBoxAlarm.Controls.Add(this.lblAlarmUnit);
            this.groupBoxAlarm.Controls.Add(this.lblMinPressure);
            this.groupBoxAlarm.Controls.Add(this.txtMinPressure);
            this.groupBoxAlarm.Controls.Add(this.lblMaxPressure);
            this.groupBoxAlarm.Controls.Add(this.txtMaxPressure);
            this.groupBoxAlarm.Controls.Add(this.lblTempLimit);
            this.groupBoxAlarm.Controls.Add(this.txtTempLimit);
            this.groupBoxAlarm.Controls.Add(this.lblTempUnit);
            this.groupBoxAlarm.Location = new System.Drawing.Point(15, 95);
            this.groupBoxAlarm.Name = "groupBoxAlarm";
            this.groupBoxAlarm.Size = new System.Drawing.Size(360, 160);
            this.groupBoxAlarm.TabStop = false;
            this.groupBoxAlarm.Text = "报警参数（预留，需现场确认）";
            //
            // btnOK - 保存按钮
            //
            this.btnOK.Location = new System.Drawing.Point(165, 270);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(100, 30);
            this.btnOK.Text = "保存";
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            //
            // btnCancel - 取消按钮
            //
            this.btnCancel.Location = new System.Drawing.Point(275, 270);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(100, 30);
            this.btnCancel.Text = "取消";
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            //
            // CommonParameterForm - 窗体自身
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(390, 315);
            this.Controls.Add(this.groupBoxCollect);
            this.Controls.Add(this.groupBoxAlarm);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CommonParameterForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "公共参数设置";
            ((System.ComponentModel.ISupportInitialize)(this.numCollectInterval)).EndInit();
            this.groupBoxCollect.ResumeLayout(false);
            this.groupBoxCollect.PerformLayout();
            this.groupBoxAlarm.ResumeLayout(false);
            this.groupBoxAlarm.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.GroupBox groupBoxCollect;
        private System.Windows.Forms.Label lblCollectInterval;
        private System.Windows.Forms.NumericUpDown numCollectInterval;
        private System.Windows.Forms.Label lblIntervalUnit;
        private System.Windows.Forms.GroupBox groupBoxAlarm;
        private System.Windows.Forms.Label lblAlarmThreshold;
        private System.Windows.Forms.TextBox txtAlarmThreshold;
        private System.Windows.Forms.Label lblAlarmUnit;
        private System.Windows.Forms.Label lblMinPressure;
        private System.Windows.Forms.TextBox txtMinPressure;
        private System.Windows.Forms.Label lblMaxPressure;
        private System.Windows.Forms.TextBox txtMaxPressure;
        private System.Windows.Forms.Label lblTempLimit;
        private System.Windows.Forms.TextBox txtTempLimit;
        private System.Windows.Forms.Label lblTempUnit;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
    }
}
