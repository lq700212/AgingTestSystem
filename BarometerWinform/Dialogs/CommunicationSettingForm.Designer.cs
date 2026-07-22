namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// PLC通讯设置窗体 —— 设计器自动生成部分
    /// 包含所有控件的创建和布局代码
    /// </summary>
    partial class CommunicationSettingForm
    {
        /// <summary>必需的设计器变量</summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理正在使用的资源
        /// </summary>
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
            this.lblPlcIp = new System.Windows.Forms.Label();
            this.txtPlcIp = new System.Windows.Forms.TextBox();
            this.lblPlcPort = new System.Windows.Forms.Label();
            this.numPlcPort = new System.Windows.Forms.NumericUpDown();
            this.lblProtocol = new System.Windows.Forms.Label();
            this.cboProtocol = new System.Windows.Forms.ComboBox();
            this.lblPortName = new System.Windows.Forms.Label();
            this.txtPortName = new System.Windows.Forms.TextBox();
            this.lblBaudRate = new System.Windows.Forms.Label();
            this.numBaudRate = new System.Windows.Forms.NumericUpDown();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnTestConnect = new System.Windows.Forms.Button();
            this.groupBoxEthernet = new System.Windows.Forms.GroupBox();
            this.groupBoxSerial = new System.Windows.Forms.GroupBox();
            ((System.ComponentModel.ISupportInitialize)(this.numPlcPort)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numBaudRate)).BeginInit();
            this.groupBoxEthernet.SuspendLayout();
            this.groupBoxSerial.SuspendLayout();
            this.SuspendLayout();
            //
            // lblPlcIp - PLC IP地址标签
            //
            this.lblPlcIp.AutoSize = true;
            this.lblPlcIp.Location = new System.Drawing.Point(20, 30);
            this.lblPlcIp.Name = "lblPlcIp";
            this.lblPlcIp.Size = new System.Drawing.Size(65, 12);
            this.lblPlcIp.Text = "PLC IP地址:";
            //
            // txtPlcIp - PLC IP地址输入框
            //
            this.txtPlcIp.Location = new System.Drawing.Point(110, 27);
            this.txtPlcIp.Name = "txtPlcIp";
            this.txtPlcIp.Size = new System.Drawing.Size(180, 21);
            //
            // lblPlcPort - PLC端口标签
            //
            this.lblPlcPort.AutoSize = true;
            this.lblPlcPort.Location = new System.Drawing.Point(20, 60);
            this.lblPlcPort.Name = "lblPlcPort";
            this.lblPlcPort.Size = new System.Drawing.Size(53, 12);
            this.lblPlcPort.Text = "PLC端口:";
            //
            // numPlcPort - PLC端口号输入框
            //
            this.numPlcPort.Location = new System.Drawing.Point(110, 58);
            this.numPlcPort.Maximum = new decimal(new int[] {65535, 0, 0, 0});
            this.numPlcPort.Name = "numPlcPort";
            this.numPlcPort.Size = new System.Drawing.Size(180, 21);
            this.numPlcPort.Value = new decimal(new int[] {502, 0, 0, 0});
            //
            // lblProtocol - 通讯协议标签
            //
            this.lblProtocol.AutoSize = true;
            this.lblProtocol.Location = new System.Drawing.Point(20, 90);
            this.lblProtocol.Name = "lblProtocol";
            this.lblProtocol.Size = new System.Drawing.Size(65, 12);
            this.lblProtocol.Text = "通讯协议:";
            //
            // cboProtocol - 通讯协议下拉框
            //
            this.cboProtocol.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboProtocol.Items.AddRange(new object[] {
                "Modbus TCP",
                "Modbus RTU",
                "自定义协议（预留）"
            });
            this.cboProtocol.Location = new System.Drawing.Point(110, 87);
            this.cboProtocol.Name = "cboProtocol";
            this.cboProtocol.Size = new System.Drawing.Size(180, 20);
            //
            // groupBoxEthernet - 以太网通讯参数分组
            //
            this.groupBoxEthernet.Controls.Add(this.lblPlcIp);
            this.groupBoxEthernet.Controls.Add(this.txtPlcIp);
            this.groupBoxEthernet.Controls.Add(this.lblPlcPort);
            this.groupBoxEthernet.Controls.Add(this.numPlcPort);
            this.groupBoxEthernet.Controls.Add(this.lblProtocol);
            this.groupBoxEthernet.Controls.Add(this.cboProtocol);
            this.groupBoxEthernet.Location = new System.Drawing.Point(15, 15);
            this.groupBoxEthernet.Name = "groupBoxEthernet";
            this.groupBoxEthernet.Size = new System.Drawing.Size(360, 130);
            this.groupBoxEthernet.TabStop = false;
            this.groupBoxEthernet.Text = "以太网通讯参数";
            //
            // lblPortName - 串口号标签
            //
            this.lblPortName.AutoSize = true;
            this.lblPortName.Location = new System.Drawing.Point(20, 30);
            this.lblPortName.Name = "lblPortName";
            this.lblPortName.Size = new System.Drawing.Size(53, 12);
            this.lblPortName.Text = "串口号:";
            //
            // txtPortName - 串口号输入框
            //
            this.txtPortName.Location = new System.Drawing.Point(110, 27);
            this.txtPortName.Name = "txtPortName";
            this.txtPortName.Size = new System.Drawing.Size(180, 21);
            this.txtPortName.Text = "COM1";
            //
            // lblBaudRate - 波特率标签
            //
            this.lblBaudRate.AutoSize = true;
            this.lblBaudRate.Location = new System.Drawing.Point(20, 60);
            this.lblBaudRate.Name = "lblBaudRate";
            this.lblBaudRate.Size = new System.Drawing.Size(53, 12);
            this.lblBaudRate.Text = "波特率:";
            //
            // numBaudRate - 波特率输入框
            //
            this.numBaudRate.Location = new System.Drawing.Point(110, 58);
            this.numBaudRate.Maximum = new decimal(new int[] {115200, 0, 0, 0});
            this.numBaudRate.Name = "numBaudRate";
            this.numBaudRate.Size = new System.Drawing.Size(180, 21);
            this.numBaudRate.Value = new decimal(new int[] {9600, 0, 0, 0});
            //
            // groupBoxSerial - 串口通讯参数分组
            //
            this.groupBoxSerial.Controls.Add(this.lblPortName);
            this.groupBoxSerial.Controls.Add(this.txtPortName);
            this.groupBoxSerial.Controls.Add(this.lblBaudRate);
            this.groupBoxSerial.Controls.Add(this.numBaudRate);
            this.groupBoxSerial.Location = new System.Drawing.Point(15, 155);
            this.groupBoxSerial.Name = "groupBoxSerial";
            this.groupBoxSerial.Size = new System.Drawing.Size(360, 100);
            this.groupBoxSerial.TabStop = false;
            this.groupBoxSerial.Text = "串口通讯参数（用于Modbus RTU）";
            //
            // btnTestConnect - 测试连接按钮（预留功能）
            //
            this.btnTestConnect.Location = new System.Drawing.Point(15, 270);
            this.btnTestConnect.Name = "btnTestConnect";
            this.btnTestConnect.Size = new System.Drawing.Size(110, 30);
            this.btnTestConnect.Text = "测试连接";
            this.btnTestConnect.Click += new System.EventHandler(this.btnTestConnect_Click);
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
            // CommunicationSettingForm - 窗体自身
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(390, 315);
            this.Controls.Add(this.groupBoxEthernet);
            this.Controls.Add(this.groupBoxSerial);
            this.Controls.Add(this.btnTestConnect);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CommunicationSettingForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "PLC通讯设置";
            ((System.ComponentModel.ISupportInitialize)(this.numPlcPort)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numBaudRate)).EndInit();
            this.groupBoxEthernet.ResumeLayout(false);
            this.groupBoxEthernet.PerformLayout();
            this.groupBoxSerial.ResumeLayout(false);
            this.groupBoxSerial.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Label lblPlcIp;
        private System.Windows.Forms.TextBox txtPlcIp;
        private System.Windows.Forms.Label lblPlcPort;
        private System.Windows.Forms.NumericUpDown numPlcPort;
        private System.Windows.Forms.Label lblProtocol;
        private System.Windows.Forms.ComboBox cboProtocol;
        private System.Windows.Forms.Label lblPortName;
        private System.Windows.Forms.TextBox txtPortName;
        private System.Windows.Forms.Label lblBaudRate;
        private System.Windows.Forms.NumericUpDown numBaudRate;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnTestConnect;
        private System.Windows.Forms.GroupBox groupBoxEthernet;
        private System.Windows.Forms.GroupBox groupBoxSerial;
    }
}
