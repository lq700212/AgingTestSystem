namespace ModbusTCPTest
{
    /// <summary>
    /// PowerOnTestForm 的设计器分部类。
    /// 只放静态控件（标题、提示、控制按钮、日志框）。
    /// 9×8 = 72 个圆形灯按钮由 PowerOnTestForm.BuildButtonGrid() 动态生成。
    /// </summary>
    partial class PowerOnTestForm
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
            this.labelTitle = new System.Windows.Forms.Label();
            this.labelHint = new System.Windows.Forms.Label();
            this.panelGrid = new System.Windows.Forms.Panel();
            this.btnConnect = new System.Windows.Forms.Button();
            this.btnAllOff = new System.Windows.Forms.Button();
            this.btnReadStatus = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.txtLog = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            //
            // labelTitle
            //
            this.labelTitle.AutoSize = true;
            this.labelTitle.Font = new System.Drawing.Font("宋体", 21.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelTitle.Location = new System.Drawing.Point(180, 18);
            this.labelTitle.Name = "labelTitle";
            this.labelTitle.Size = new System.Drawing.Size(340, 29);
            this.labelTitle.TabIndex = 0;
            this.labelTitle.Text = "载台上电（继电器）测试";
            //
            // labelHint
            //
            this.labelHint.AutoSize = false;
            this.labelHint.Font = new System.Drawing.Font("宋体", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.labelHint.ForeColor = System.Drawing.Color.DarkSlateGray;
            this.labelHint.Location = new System.Drawing.Point(20, 60);
            this.labelHint.Name = "labelHint";
            this.labelHint.Size = new System.Drawing.Size(660, 28);
            this.labelHint.TabIndex = 1;
            this.labelHint.Text = "操作说明：点击按钮切换上电 ON/OFF。同一排多个按钮 ON 时，寄存器值会自动按位叠加。\r\n例如：第1排第1个=Y110(0x0100)，再加第1排第2个=Y111(0x0200) → 写入 0x0300。";
            //
            // panelGrid
            // 注意：这里设置的 Size 是初始值，运行时会被 BuildButtonGrid() 重新计算覆盖
            //
            this.panelGrid.BackColor = System.Drawing.Color.White;
            this.panelGrid.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelGrid.Location = new System.Drawing.Point(20, 95);
            this.panelGrid.Name = "panelGrid";
            this.panelGrid.Size = new System.Drawing.Size(660, 660);
            this.panelGrid.TabIndex = 2;
            //
            // btnConnect
            //
            this.btnConnect.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnConnect.Location = new System.Drawing.Point(20, 770);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(120, 42);
            this.btnConnect.TabIndex = 3;
            this.btnConnect.Text = "连接测试";
            this.btnConnect.UseVisualStyleBackColor = true;
            this.btnConnect.Click += new System.EventHandler(this.btnConnect_Click);
            //
            // btnAllOff
            //
            this.btnAllOff.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnAllOff.Location = new System.Drawing.Point(150, 770);
            this.btnAllOff.Name = "btnAllOff";
            this.btnAllOff.Size = new System.Drawing.Size(120, 42);
            this.btnAllOff.TabIndex = 4;
            this.btnAllOff.Text = "全部关闭";
            this.btnAllOff.UseVisualStyleBackColor = true;
            this.btnAllOff.Click += new System.EventHandler(this.btnAllOff_Click);
            //
            // btnReadStatus
            //
            this.btnReadStatus.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnReadStatus.Location = new System.Drawing.Point(280, 770);
            this.btnReadStatus.Name = "btnReadStatus";
            this.btnReadStatus.Size = new System.Drawing.Size(120, 42);
            this.btnReadStatus.TabIndex = 5;
            this.btnReadStatus.Text = "读取状态";
            this.btnReadStatus.UseVisualStyleBackColor = true;
            this.btnReadStatus.Click += new System.EventHandler(this.btnReadStatus_Click);
            //
            // btnClose
            //
            this.btnClose.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnClose.Location = new System.Drawing.Point(560, 770);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(120, 42);
            this.btnClose.TabIndex = 6;
            this.btnClose.Text = "关闭";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            //
            // txtLog
            //
            this.txtLog.Font = new System.Drawing.Font("Consolas", 10F);
            this.txtLog.Location = new System.Drawing.Point(20, 825);
            this.txtLog.Multiline = true;
            this.txtLog.Name = "txtLog";
            this.txtLog.ReadOnly = true;
            this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtLog.Size = new System.Drawing.Size(660, 80);
            this.txtLog.TabIndex = 7;
            //
            // PowerOnTestForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(242)))), ((int)(((byte)(245)))));
            this.ClientSize = new System.Drawing.Size(700, 920);
            this.Controls.Add(this.labelTitle);
            this.Controls.Add(this.labelHint);
            this.Controls.Add(this.panelGrid);
            this.Controls.Add(this.btnConnect);
            this.Controls.Add(this.btnAllOff);
            this.Controls.Add(this.btnReadStatus);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.txtLog);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "PowerOnTestForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "载台上电（继电器）测试";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label labelTitle;
        private System.Windows.Forms.Label labelHint;
        private System.Windows.Forms.Panel panelGrid;
        private System.Windows.Forms.Button btnConnect;
        private System.Windows.Forms.Button btnAllOff;
        private System.Windows.Forms.Button btnReadStatus;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.TextBox txtLog;
    }
}
