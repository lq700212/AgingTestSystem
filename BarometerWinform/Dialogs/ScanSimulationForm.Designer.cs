namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 扫码模拟窗体 —— 设计器自动生成部分
    /// </summary>
    partial class ScanSimulationForm
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
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblBarcode = new System.Windows.Forms.Label();
            this.txtBarcode = new System.Windows.Forms.TextBox();
            this.lblTip = new System.Windows.Forms.Label();
            this.btnScan = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // lblTitle - 标题标签
            //
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Location = new System.Drawing.Point(15, 15);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Text = "扫码模拟（开发调试用）";
            //
            // lblBarcode - 条码内容标签
            //
            this.lblBarcode.AutoSize = true;
            this.lblBarcode.Location = new System.Drawing.Point(15, 55);
            this.lblBarcode.Name = "lblBarcode";
            this.lblBarcode.Text = "条码内容:";
            //
            // txtBarcode - 条码内容输入框
            //
            this.txtBarcode.Location = new System.Drawing.Point(90, 52);
            this.txtBarcode.Name = "txtBarcode";
            this.txtBarcode.Size = new System.Drawing.Size(280, 21);
            this.txtBarcode.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtBarcode_KeyDown);
            //
            // lblTip - 提示标签
            //
            this.lblTip.AutoSize = true;
            this.lblTip.ForeColor = System.Drawing.Color.Gray;
            this.lblTip.Location = new System.Drawing.Point(15, 90);
            this.lblTip.Name = "lblTip";
            this.lblTip.Size = new System.Drawing.Size(350, 36);
            this.lblTip.Text = "提示：输入条码内容后按回车键或点击\"模拟扫码\"按钮。\n" +
                               "实际项目中应替换为真实扫码枪的扫码事件。";
            //
            // btnScan - 模拟扫码按钮
            //
            this.btnScan.Location = new System.Drawing.Point(165, 135);
            this.btnScan.Name = "btnScan";
            this.btnScan.Size = new System.Drawing.Size(100, 30);
            this.btnScan.Text = "模拟扫码";
            this.btnScan.Click += new System.EventHandler(this.btnScan_Click);
            //
            // btnCancel - 取消按钮
            //
            this.btnCancel.Location = new System.Drawing.Point(275, 135);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(95, 30);
            this.btnCancel.Text = "取消";
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            //
            // ScanSimulationForm - 窗体自身
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(385, 180);
            this.Controls.Add(this.lblTitle);
            this.Controls.Add(this.lblBarcode);
            this.Controls.Add(this.txtBarcode);
            this.Controls.Add(this.lblTip);
            this.Controls.Add(this.btnScan);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ScanSimulationForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "扫码模拟";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblBarcode;
        private System.Windows.Forms.TextBox txtBarcode;
        private System.Windows.Forms.Label lblTip;
        private System.Windows.Forms.Button btnScan;
        private System.Windows.Forms.Button btnCancel;
    }
}
