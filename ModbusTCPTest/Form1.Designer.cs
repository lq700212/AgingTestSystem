namespace ModbusTCPTest
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
            this.btnConnection = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.btnWriteData = new System.Windows.Forms.Button();
            this.btnReadData = new System.Windows.Forms.Button();
            this.btnWriteDatas = new System.Windows.Forms.Button();
            this.btnReadDatas = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // btnConnection
            // 
            this.btnConnection.AutoSize = true;
            this.btnConnection.Font = new System.Drawing.Font("宋体", 21.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnConnection.Location = new System.Drawing.Point(283, 119);
            this.btnConnection.Name = "btnConnection";
            this.btnConnection.Size = new System.Drawing.Size(139, 39);
            this.btnConnection.TabIndex = 0;
            this.btnConnection.Text = "网络连接";
            this.btnConnection.UseVisualStyleBackColor = true;
            this.btnConnection.Click += new System.EventHandler(this.btnConnection_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("宋体", 42F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.label1.Location = new System.Drawing.Point(178, 31);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(388, 56);
            this.label1.TabIndex = 1;
            this.label1.Text = "ModbusTCP测试";
            // 
            // btnWriteData
            // 
            this.btnWriteData.AutoSize = true;
            this.btnWriteData.Font = new System.Drawing.Font("宋体", 21.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnWriteData.Location = new System.Drawing.Point(156, 212);
            this.btnWriteData.Name = "btnWriteData";
            this.btnWriteData.Size = new System.Drawing.Size(139, 39);
            this.btnWriteData.TabIndex = 2;
            this.btnWriteData.Text = "写入";
            this.btnWriteData.UseVisualStyleBackColor = true;
            this.btnWriteData.Click += new System.EventHandler(this.btnWriteData_Click);
            // 
            // btnReadData
            // 
            this.btnReadData.AutoSize = true;
            this.btnReadData.Font = new System.Drawing.Font("宋体", 21.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnReadData.Location = new System.Drawing.Point(156, 295);
            this.btnReadData.Name = "btnReadData";
            this.btnReadData.Size = new System.Drawing.Size(139, 39);
            this.btnReadData.TabIndex = 3;
            this.btnReadData.Text = "读取";
            this.btnReadData.UseVisualStyleBackColor = true;
            this.btnReadData.Click += new System.EventHandler(this.btnReadData_Click);
            // 
            // btnWriteDatas
            // 
            this.btnWriteDatas.Font = new System.Drawing.Font("宋体", 21.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnWriteDatas.Location = new System.Drawing.Point(378, 212);
            this.btnWriteDatas.Name = "btnWriteDatas";
            this.btnWriteDatas.Size = new System.Drawing.Size(227, 39);
            this.btnWriteDatas.TabIndex = 4;
            this.btnWriteDatas.Text = "批量写入验证";
            this.btnWriteDatas.UseVisualStyleBackColor = true;
            this.btnWriteDatas.Click += new System.EventHandler(this.btnWriteDatas_Click);
            // 
            // btnReadDatas
            // 
            this.btnReadDatas.Font = new System.Drawing.Font("宋体", 21.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnReadDatas.Location = new System.Drawing.Point(378, 295);
            this.btnReadDatas.Name = "btnReadDatas";
            this.btnReadDatas.Size = new System.Drawing.Size(227, 39);
            this.btnReadDatas.TabIndex = 5;
            this.btnReadDatas.Text = "批量读取验证";
            this.btnReadDatas.UseVisualStyleBackColor = true;
            this.btnReadDatas.Click += new System.EventHandler(this.btnReadDatas_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.btnReadDatas);
            this.Controls.Add(this.btnWriteDatas);
            this.Controls.Add(this.btnReadData);
            this.Controls.Add(this.btnWriteData);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnConnection);
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Form1";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnConnection;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnWriteData;
        private System.Windows.Forms.Button btnReadData;
        private System.Windows.Forms.Button btnWriteDatas;
        private System.Windows.Forms.Button btnReadDatas;
    }
}

