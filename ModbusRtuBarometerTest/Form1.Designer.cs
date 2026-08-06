namespace ModbusRtuBarometerTest
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
            this.llPressure = new System.Windows.Forms.Label();
            this.llSN = new System.Windows.Forms.Label();
            this.btnReadPressure = new System.Windows.Forms.Button();
            this.btnGetSN = new System.Windows.Forms.Button();
            this.btnBatchSetThreshold = new System.Windows.Forms.Button();
            this.tbThreshold = new Sunny.UI.UITextBox();
            this.btnSetThreshold = new System.Windows.Forms.Button();
            this.btnBatchRead = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // llPressure
            // 
            this.llPressure.AutoSize = true;
            this.llPressure.Font = new System.Drawing.Font("宋体", 42F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.llPressure.Location = new System.Drawing.Point(416, 26);
            this.llPressure.Name = "llPressure";
            this.llPressure.Size = new System.Drawing.Size(136, 56);
            this.llPressure.TabIndex = 1;
            this.llPressure.Text = "NULL";
            // 
            // llSN
            // 
            this.llSN.AutoSize = true;
            this.llSN.Font = new System.Drawing.Font("宋体", 42F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.llSN.Location = new System.Drawing.Point(416, 143);
            this.llSN.Name = "llSN";
            this.llSN.Size = new System.Drawing.Size(136, 56);
            this.llSN.TabIndex = 3;
            this.llSN.Text = "NULL";
            // 
            // btnReadPressure
            // 
            this.btnReadPressure.Font = new System.Drawing.Font("宋体", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnReadPressure.Location = new System.Drawing.Point(118, 26);
            this.btnReadPressure.Name = "btnReadPressure";
            this.btnReadPressure.Size = new System.Drawing.Size(210, 50);
            this.btnReadPressure.TabIndex = 4;
            this.btnReadPressure.Text = "真空压力读取";
            this.btnReadPressure.UseVisualStyleBackColor = true;
            this.btnReadPressure.Click += new System.EventHandler(this.btnReadPressure_Click);
            // 
            // btnGetSN
            // 
            this.btnGetSN.Font = new System.Drawing.Font("宋体", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnGetSN.Location = new System.Drawing.Point(118, 143);
            this.btnGetSN.Name = "btnGetSN";
            this.btnGetSN.Size = new System.Drawing.Size(210, 56);
            this.btnGetSN.TabIndex = 5;
            this.btnGetSN.Text = "扫码测试：获取SN";
            this.btnGetSN.UseVisualStyleBackColor = true;
            // 
            // btnBatchSetThreshold
            // 
            this.btnBatchSetThreshold.Font = new System.Drawing.Font("宋体", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnBatchSetThreshold.Location = new System.Drawing.Point(118, 308);
            this.btnBatchSetThreshold.Name = "btnBatchSetThreshold";
            this.btnBatchSetThreshold.Size = new System.Drawing.Size(210, 57);
            this.btnBatchSetThreshold.TabIndex = 6;
            this.btnBatchSetThreshold.Text = "批量设置气压阈值";
            this.btnBatchSetThreshold.UseVisualStyleBackColor = true;
            this.btnBatchSetThreshold.Click += new System.EventHandler(this.btnBatchSetThreashold_Click);
            // 
            // tbThreshold
            // 
            this.tbThreshold.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.tbThreshold.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tbThreshold.Location = new System.Drawing.Point(426, 274);
            this.tbThreshold.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.tbThreshold.MinimumSize = new System.Drawing.Size(1, 16);
            this.tbThreshold.Name = "tbThreshold";
            this.tbThreshold.Padding = new System.Windows.Forms.Padding(5);
            this.tbThreshold.ShowText = false;
            this.tbThreshold.Size = new System.Drawing.Size(204, 57);
            this.tbThreshold.TabIndex = 7;
            this.tbThreshold.Text = "0";
            this.tbThreshold.TextAlignment = System.Drawing.ContentAlignment.MiddleRight;
            this.tbThreshold.Watermark = "请输入阈值，如 1.234";
            // 
            // btnSetThreshold
            //
            this.btnSetThreshold.Font = new System.Drawing.Font("宋体", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnSetThreshold.Location = new System.Drawing.Point(118, 243);
            this.btnSetThreshold.Name = "btnSetThreshold";
            this.btnSetThreshold.Size = new System.Drawing.Size(210, 59);
            this.btnSetThreshold.TabIndex = 8;
            this.btnSetThreshold.Text = "设置1号表气压阈值";
            this.btnSetThreshold.UseVisualStyleBackColor = true;
            this.btnSetThreshold.Click += new System.EventHandler(this.btnSetThreshold_Click);
            //
            // btnBatchRead
            //
            this.btnBatchRead.Font = new System.Drawing.Font("宋体", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnBatchRead.Location = new System.Drawing.Point(118, 371);
            this.btnBatchRead.Name = "btnBatchRead";
            this.btnBatchRead.Size = new System.Drawing.Size(210, 50);
            this.btnBatchRead.TabIndex = 9;
            this.btnBatchRead.Text = "批量读取压力";
            this.btnBatchRead.UseVisualStyleBackColor = true;
            this.btnBatchRead.Click += new System.EventHandler(this.btnBatchRead_Click);
            //
            // Form1
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.btnBatchRead);
            this.Controls.Add(this.btnSetThreshold);
            this.Controls.Add(this.tbThreshold);
            this.Controls.Add(this.btnBatchSetThreshold);
            this.Controls.Add(this.btnGetSN);
            this.Controls.Add(this.btnReadPressure);
            this.Controls.Add(this.llSN);
            this.Controls.Add(this.llPressure);
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "气压表设置";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Label llPressure;
        private System.Windows.Forms.Label llSN;
        private System.Windows.Forms.Button btnReadPressure;
        private System.Windows.Forms.Button btnGetSN;
        private System.Windows.Forms.Button btnBatchSetThreshold;
        private Sunny.UI.UITextBox tbThreshold;
        private System.Windows.Forms.Button btnSetThreshold;
        private System.Windows.Forms.Button btnBatchRead;
    }
}

