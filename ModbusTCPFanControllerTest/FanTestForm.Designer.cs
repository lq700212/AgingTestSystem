namespace ModbusTCPFanControllerTest
{
    partial class FanTestForm
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
            this.components = new System.ComponentModel.Container();
            this.lblState = new System.Windows.Forms.Label();
            this.txtState = new System.Windows.Forms.TextBox();
            this.lblTemperature = new System.Windows.Forms.Label();
            this.txtTemperature = new System.Windows.Forms.TextBox();
            this.btnStart = new System.Windows.Forms.Button();
            this.btnStop = new System.Windows.Forms.Button();
            this.rtbLog = new System.Windows.Forms.RichTextBox();
            this.btnConnect = new System.Windows.Forms.Button();
            this.lblIp = new System.Windows.Forms.Label();
            this.txtIp = new System.Windows.Forms.TextBox();
            this.lblPort = new System.Windows.Forms.Label();
            this.txtPort = new System.Windows.Forms.TextBox();
            this.timerRefresh = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            //
            // lblState
            //
            this.lblState.AutoSize = true;
            this.lblState.Location = new System.Drawing.Point(22, 22);
            this.lblState.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblState.Name = "lblState";
            this.lblState.Size = new System.Drawing.Size(59, 12);
            this.lblState.TabIndex = 0;
            this.lblState.Text = "当前状态:";
            //
            // txtState
            //
            this.txtState.Location = new System.Drawing.Point(78, 20);
            this.txtState.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.txtState.Name = "txtState";
            this.txtState.ReadOnly = true;
            this.txtState.Size = new System.Drawing.Size(114, 21);
            this.txtState.TabIndex = 1;
            //
            // lblTemperature
            //
            this.lblTemperature.AutoSize = true;
            this.lblTemperature.Location = new System.Drawing.Point(22, 52);
            this.lblTemperature.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblTemperature.Name = "lblTemperature";
            this.lblTemperature.Size = new System.Drawing.Size(59, 12);
            this.lblTemperature.TabIndex = 2;
            this.lblTemperature.Text = "当前温度:";
            //
            // txtTemperature
            //
            this.txtTemperature.Location = new System.Drawing.Point(78, 50);
            this.txtTemperature.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.txtTemperature.Name = "txtTemperature";
            this.txtTemperature.ReadOnly = true;
            this.txtTemperature.Size = new System.Drawing.Size(114, 21);
            this.txtTemperature.TabIndex = 3;
            //
            // btnStart
            //
            this.btnStart.Location = new System.Drawing.Point(25, 116);
            this.btnStart.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(75, 22);
            this.btnStart.TabIndex = 4;
            this.btnStart.Text = "定值启动";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.BtnStart_Click);
            //
            // btnStop
            //
            this.btnStop.Location = new System.Drawing.Point(116, 116);
            this.btnStop.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(75, 22);
            this.btnStop.TabIndex = 5;
            this.btnStop.Text = "定值停止";
            this.btnStop.UseVisualStyleBackColor = true;
            this.btnStop.Click += new System.EventHandler(this.BtnStop_Click);
            //
            // btnConnect
            //
            this.btnConnect.Location = new System.Drawing.Point(206, 116);
            this.btnConnect.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(75, 22);
            this.btnConnect.TabIndex = 6;
            this.btnConnect.Text = "连接测试";
            this.btnConnect.UseVisualStyleBackColor = true;
            this.btnConnect.Click += new System.EventHandler(this.BtnConnect_Click);
            //
            // rtbLog
            //
            this.rtbLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.rtbLog.Location = new System.Drawing.Point(25, 156);
            this.rtbLog.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.rtbLog.Name = "rtbLog";
            this.rtbLog.Size = new System.Drawing.Size(376, 190);
            this.rtbLog.TabIndex = 7;
            this.rtbLog.Text = "";
            //
            // lblIp
            //
            this.lblIp.AutoSize = true;
            this.lblIp.Location = new System.Drawing.Point(22, 82);
            this.lblIp.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblIp.Name = "lblIp";
            this.lblIp.Size = new System.Drawing.Size(53, 12);
            this.lblIp.TabIndex = 8;
            this.lblIp.Text = "设备 IP:";
            //
            // txtIp
            //
            this.txtIp.Location = new System.Drawing.Point(78, 80);
            this.txtIp.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.txtIp.Name = "txtIp";
            this.txtIp.Size = new System.Drawing.Size(110, 21);
            this.txtIp.TabIndex = 9;
            //
            // lblPort
            //
            this.lblPort.AutoSize = true;
            this.lblPort.Location = new System.Drawing.Point(200, 82);
            this.lblPort.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.lblPort.Name = "lblPort";
            this.lblPort.Size = new System.Drawing.Size(41, 12);
            this.lblPort.TabIndex = 10;
            this.lblPort.Text = "端口:";
            //
            // txtPort
            //
            this.txtPort.Location = new System.Drawing.Point(243, 80);
            this.txtPort.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.txtPort.Name = "txtPort";
            this.txtPort.Size = new System.Drawing.Size(60, 21);
            this.txtPort.TabIndex = 11;
            //
            // timerRefresh
            //
            this.timerRefresh.Enabled = true;
            this.timerRefresh.Interval = 2000;
            this.timerRefresh.Tick += new System.EventHandler(this.TimerRefresh_Tick);
            //
            // FanTestForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(428, 376);
            this.Controls.Add(this.txtPort);
            this.Controls.Add(this.lblPort);
            this.Controls.Add(this.txtIp);
            this.Controls.Add(this.lblIp);
            this.Controls.Add(this.rtbLog);
            this.Controls.Add(this.btnConnect);
            this.Controls.Add(this.btnStop);
            this.Controls.Add(this.btnStart);
            this.Controls.Add(this.txtTemperature);
            this.Controls.Add(this.lblTemperature);
            this.Controls.Add(this.txtState);
            this.Controls.Add(this.lblState);
            this.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.Name = "FanTestForm";
            this.Text = "冷却送风机控制";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblState;
        private System.Windows.Forms.TextBox txtState;
        private System.Windows.Forms.Label lblTemperature;
        private System.Windows.Forms.TextBox txtTemperature;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.RichTextBox rtbLog;
        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.Label lblIp;
        private System.Windows.Forms.TextBox txtIp;
        private System.Windows.Forms.Label lblPort;
        private System.Windows.Forms.TextBox txtPort;
        private System.Windows.Forms.Timer timerRefresh;
    }
}
