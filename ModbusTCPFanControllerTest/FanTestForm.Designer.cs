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
            this.timerRefresh = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            // 
            // lblState
            // 
            this.lblState.AutoSize = true;
            this.lblState.Location = new System.Drawing.Point(30, 30);
            this.lblState.Name = "lblState";
            this.lblState.Size = new System.Drawing.Size(68, 17);
            this.lblState.TabIndex = 0;
            this.lblState.Text = "当前状态:";
            // 
            // txtState
            // 
            this.txtState.Location = new System.Drawing.Point(104, 27);
            this.txtState.Name = "txtState";
            this.txtState.ReadOnly = true;
            this.txtState.Size = new System.Drawing.Size(150, 22);
            this.txtState.TabIndex = 1;
            // 
            // lblTemperature
            // 
            this.lblTemperature.AutoSize = true;
            this.lblTemperature.Location = new System.Drawing.Point(30, 70);
            this.lblTemperature.Name = "lblTemperature";
            this.lblTemperature.Size = new System.Drawing.Size(68, 17);
            this.lblTemperature.TabIndex = 2;
            this.lblTemperature.Text = "当前温度:";
            // 
            // txtTemperature
            // 
            this.txtTemperature.Location = new System.Drawing.Point(104, 67);
            this.txtTemperature.Name = "txtTemperature";
            this.txtTemperature.ReadOnly = true;
            this.txtTemperature.Size = new System.Drawing.Size(150, 22);
            this.txtTemperature.TabIndex = 3;
            // 
            // btnStart
            // 
            this.btnStart.Location = new System.Drawing.Point(33, 120);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(100, 30);
            this.btnStart.TabIndex = 4;
            this.btnStart.Text = "定值启动";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.BtnStart_Click);
            // 
            // btnStop
            // 
            this.btnStop.Location = new System.Drawing.Point(154, 120);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(100, 30);
            this.btnStop.TabIndex = 5;
            this.btnStop.Text = "定值停止";
            this.btnStop.UseVisualStyleBackColor = true;
            this.btnStop.Click += new System.EventHandler(this.BtnStop_Click);
            // 
            // btnConnect
            // 
            this.btnConnect.Location = new System.Drawing.Point(275, 120);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(100, 30);
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
            this.rtbLog.Location = new System.Drawing.Point(33, 170);
            this.rtbLog.Name = "rtbLog";
            this.rtbLog.Size = new System.Drawing.Size(500, 200);
            this.rtbLog.TabIndex = 7;
            this.rtbLog.Text = "";
            // 
            // timerRefresh
            // 
            this.timerRefresh.Enabled = true;
            this.timerRefresh.Interval = 2000;
            this.timerRefresh.Tick += new System.EventHandler(this.TimerRefresh_Tick);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(570, 400);
            this.Controls.Add(this.rtbLog);
            this.Controls.Add(this.btnConnect);
            this.Controls.Add(this.btnStop);
            this.Controls.Add(this.btnStart);
            this.Controls.Add(this.txtTemperature);
            this.Controls.Add(this.lblTemperature);
            this.Controls.Add(this.txtState);
            this.Controls.Add(this.lblState);
            this.Name = "Form1";
            this.Text = "冷却送风机控制 - Modbus RTU over TCP (NModbus)";
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
        private System.Windows.Forms.Timer timerRefresh;
    }
}