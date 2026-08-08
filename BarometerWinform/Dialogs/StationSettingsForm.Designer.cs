namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 工位设置窗口 —— 设计器自动生成部分（V1.18 新增）
    ///
    /// 【布局说明】
    /// ┌────────────────────────────────────────────────┐
    /// │ 工位设置窗口 NO 1                                │  ← 标题栏
    /// ├────────────────────────────────┬───────────────┤
    /// │  状态:                [空闲] │ [破空]        │
    /// │  SN:                    [___] │ [下电]        │
    /// │  配方:                  [___] │ [保存]        │
    /// │  延时时间:              [___] │ [加入对列]     │
    /// │  启动时间:              [___] │ [关闭窗口]     │
    /// │  极限温度:              [___] │               │
    /// └────────────────────────────────┴───────────────┘
    ///
    /// 左侧为 6 个设置项（设置项名 + 输入框，均左对齐，整列居中）；
    /// 右侧为一列操作按钮。
    /// </summary>
    partial class StationSettingsForm
    {
        /// <summary>必需的设计器变量</summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>清理所有正在使用的资源</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>设计器支持所需的方法</summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.lblState = new System.Windows.Forms.Label();
            this.txtState = new System.Windows.Forms.TextBox();
            this.lblSN = new System.Windows.Forms.Label();
            this.txtSN = new System.Windows.Forms.TextBox();
            this.lblRecipe = new System.Windows.Forms.Label();
            this.txtRecipe = new System.Windows.Forms.TextBox();
            this.lblDelay = new System.Windows.Forms.Label();
            this.nudDelayHours = new System.Windows.Forms.NumericUpDown();
            this.lblDelayColon1 = new System.Windows.Forms.Label();
            this.nudDelayMinutes = new System.Windows.Forms.NumericUpDown();
            this.lblDelayColon2 = new System.Windows.Forms.Label();
            this.nudDelaySeconds = new System.Windows.Forms.NumericUpDown();
            this.lblStart = new System.Windows.Forms.Label();
            this.nudStartHours = new System.Windows.Forms.NumericUpDown();
            this.lblStartColon1 = new System.Windows.Forms.Label();
            this.nudStartMinutes = new System.Windows.Forms.NumericUpDown();
            this.lblStartColon2 = new System.Windows.Forms.Label();
            this.nudStartSeconds = new System.Windows.Forms.NumericUpDown();
            this.lblTemp = new System.Windows.Forms.Label();
            this.txtTemp = new System.Windows.Forms.TextBox();
            this.btnBreakVacuum = new System.Windows.Forms.Button();
            this.btnPowerOff = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnAddToQueue = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.nudDelayHours)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudDelayMinutes)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudDelaySeconds)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudStartHours)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudStartMinutes)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudStartSeconds)).BeginInit();
            this.SuspendLayout();
            //
            // lblState - "状态"设置项名称（左对齐，V1.18 只显示"状态"两字）
            //
            this.lblState.AutoSize = true;
            this.lblState.Location = new System.Drawing.Point(30, 28);
            this.lblState.Name = "lblState";
            this.lblState.Size = new System.Drawing.Size(41, 12);
            this.lblState.TabIndex = 0;
            this.lblState.Text = "状态:";
            //
            // txtState - 状态显示输入框（只读，V1.18 显示中文状态：空闲/选中/繁忙/故障）
            //
            this.txtState.Location = new System.Drawing.Point(150, 25);
            this.txtState.Name = "txtState";
            this.txtState.ReadOnly = true;
            this.txtState.Size = new System.Drawing.Size(180, 21);
            this.txtState.TabIndex = 1;
            this.txtState.Text = "空闲";
            //
            // lblSN - "SN"设置项名称（左对齐）
            //
            this.lblSN.AutoSize = true;
            this.lblSN.Location = new System.Drawing.Point(30, 66);
            this.lblSN.Name = "lblSN";
            this.lblSN.Size = new System.Drawing.Size(29, 12);
            this.lblSN.TabIndex = 2;
            this.lblSN.Text = "SN:";
            //
            // txtSN - SN输入框
            //
            this.txtSN.Location = new System.Drawing.Point(150, 63);
            this.txtSN.Name = "txtSN";
            this.txtSN.Size = new System.Drawing.Size(180, 21);
            this.txtSN.TabIndex = 3;
            //
            // lblRecipe - "配方"设置项名称（左对齐）
            //
            this.lblRecipe.AutoSize = true;
            this.lblRecipe.Location = new System.Drawing.Point(30, 104);
            this.lblRecipe.Name = "lblRecipe";
            this.lblRecipe.Size = new System.Drawing.Size(41, 12);
            this.lblRecipe.TabIndex = 4;
            this.lblRecipe.Text = "配方:";
            //
            // txtRecipe - 配方输入框
            //
            this.txtRecipe.Location = new System.Drawing.Point(150, 101);
            this.txtRecipe.Name = "txtRecipe";
            this.txtRecipe.Size = new System.Drawing.Size(180, 21);
            this.txtRecipe.TabIndex = 5;
            //
            // lblDelay - "延时时间"设置项名称（左对齐）
            //
            this.lblDelay.AutoSize = true;
            this.lblDelay.Location = new System.Drawing.Point(30, 142);
            this.lblDelay.Name = "lblDelay";
            this.lblDelay.Size = new System.Drawing.Size(65, 12);
            this.lblDelay.TabIndex = 6;
            this.lblDelay.Text = "延时时间:";
            //
            // nudDelayHours - 延时时间-时（NumericUpDown，V1.28 由 TextBox 改）
            //
            this.nudDelayHours.Location = new System.Drawing.Point(150, 136);
            this.nudDelayHours.Maximum = new decimal(new int[] {
            99,
            0,
            0,
            0});
            this.nudDelayHours.Name = "nudDelayHours";
            this.nudDelayHours.Size = new System.Drawing.Size(48, 21);
            this.nudDelayHours.TabIndex = 7;
            this.nudDelayHours.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // lblDelayColon1 - 延时时间：时与分之间的冒号分隔符
            //
            this.lblDelayColon1.AutoSize = true;
            this.lblDelayColon1.Location = new System.Drawing.Point(200, 140);
            this.lblDelayColon1.Name = "lblDelayColon1";
            this.lblDelayColon1.Size = new System.Drawing.Size(6, 12);
            this.lblDelayColon1.TabIndex = 0;
            this.lblDelayColon1.Text = ":";
            //
            // nudDelayMinutes - 延时时间-分（NumericUpDown）
            //
            this.nudDelayMinutes.Location = new System.Drawing.Point(212, 136);
            this.nudDelayMinutes.Maximum = new decimal(new int[] {
            59,
            0,
            0,
            0});
            this.nudDelayMinutes.Name = "nudDelayMinutes";
            this.nudDelayMinutes.Size = new System.Drawing.Size(48, 21);
            this.nudDelayMinutes.TabIndex = 8;
            this.nudDelayMinutes.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // lblDelayColon2 - 延时时间：分与秒之间的冒号分隔符
            //
            this.lblDelayColon2.AutoSize = true;
            this.lblDelayColon2.Location = new System.Drawing.Point(262, 140);
            this.lblDelayColon2.Name = "lblDelayColon2";
            this.lblDelayColon2.Size = new System.Drawing.Size(6, 12);
            this.lblDelayColon2.TabIndex = 0;
            this.lblDelayColon2.Text = ":";
            //
            // nudDelaySeconds - 延时时间-秒（NumericUpDown）
            //
            this.nudDelaySeconds.Location = new System.Drawing.Point(274, 136);
            this.nudDelaySeconds.Maximum = new decimal(new int[] {
            59,
            0,
            0,
            0});
            this.nudDelaySeconds.Name = "nudDelaySeconds";
            this.nudDelaySeconds.Size = new System.Drawing.Size(48, 21);
            this.nudDelaySeconds.TabIndex = 9;
            this.nudDelaySeconds.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // lblStart - "启动时间"设置项名称（左对齐）
            //
            this.lblStart.AutoSize = true;
            this.lblStart.Location = new System.Drawing.Point(30, 180);
            this.lblStart.Name = "lblStart";
            this.lblStart.Size = new System.Drawing.Size(65, 12);
            this.lblStart.TabIndex = 8;
            this.lblStart.Text = "启动时间:";
            //
            // nudStartHours - 启动时间-时（NumericUpDown，V1.28 由 TextBox 改）
            //
            this.nudStartHours.Location = new System.Drawing.Point(150, 174);
            this.nudStartHours.Maximum = new decimal(new int[] {
            99,
            0,
            0,
            0});
            this.nudStartHours.Name = "nudStartHours";
            this.nudStartHours.Size = new System.Drawing.Size(48, 21);
            this.nudStartHours.TabIndex = 10;
            this.nudStartHours.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // lblStartColon1 - 启动时间：时与分之间的冒号分隔符
            //
            this.lblStartColon1.AutoSize = true;
            this.lblStartColon1.Location = new System.Drawing.Point(200, 178);
            this.lblStartColon1.Name = "lblStartColon1";
            this.lblStartColon1.Size = new System.Drawing.Size(6, 12);
            this.lblStartColon1.TabIndex = 0;
            this.lblStartColon1.Text = ":";
            //
            // nudStartMinutes - 启动时间-分（NumericUpDown）
            //
            this.nudStartMinutes.Location = new System.Drawing.Point(212, 174);
            this.nudStartMinutes.Maximum = new decimal(new int[] {
            59,
            0,
            0,
            0});
            this.nudStartMinutes.Name = "nudStartMinutes";
            this.nudStartMinutes.Size = new System.Drawing.Size(48, 21);
            this.nudStartMinutes.TabIndex = 11;
            this.nudStartMinutes.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // lblStartColon2 - 启动时间：分与秒之间的冒号分隔符
            //
            this.lblStartColon2.AutoSize = true;
            this.lblStartColon2.Location = new System.Drawing.Point(262, 178);
            this.lblStartColon2.Name = "lblStartColon2";
            this.lblStartColon2.Size = new System.Drawing.Size(6, 12);
            this.lblStartColon2.TabIndex = 0;
            this.lblStartColon2.Text = ":";
            //
            // nudStartSeconds - 启动时间-秒（NumericUpDown）
            //
            this.nudStartSeconds.Location = new System.Drawing.Point(274, 174);
            this.nudStartSeconds.Maximum = new decimal(new int[] {
            59,
            0,
            0,
            0});
            this.nudStartSeconds.Name = "nudStartSeconds";
            this.nudStartSeconds.Size = new System.Drawing.Size(48, 21);
            this.nudStartSeconds.TabIndex = 12;
            this.nudStartSeconds.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // lblTemp - "极限温度"设置项名称（左对齐）
            //
            this.lblTemp.AutoSize = true;
            this.lblTemp.Location = new System.Drawing.Point(30, 218);
            this.lblTemp.Name = "lblTemp";
            this.lblTemp.Size = new System.Drawing.Size(65, 12);
            this.lblTemp.TabIndex = 10;
            this.lblTemp.Text = "极限温度:";
            //
            // txtTemp - 极限温度输入框
            //
            this.txtTemp.Location = new System.Drawing.Point(150, 215);
            this.txtTemp.Name = "txtTemp";
            this.txtTemp.Size = new System.Drawing.Size(180, 21);
            this.txtTemp.TabIndex = 11;
            //
            // btnBreakVacuum - 破空按钮（功能待确认）
            //
            this.btnBreakVacuum.BackColor = System.Drawing.Color.DodgerBlue;
            this.btnBreakVacuum.ForeColor = System.Drawing.Color.White;
            this.btnBreakVacuum.Location = new System.Drawing.Point(370, 25);
            this.btnBreakVacuum.Name = "btnBreakVacuum";
            this.btnBreakVacuum.Size = new System.Drawing.Size(90, 30);
            this.btnBreakVacuum.TabIndex = 12;
            this.btnBreakVacuum.Text = "破空";
            this.btnBreakVacuum.UseVisualStyleBackColor = false;
            this.btnBreakVacuum.Click += new System.EventHandler(this.btnBreakVacuum_Click);
            //
            // btnPowerOff - 下电按钮（功能待确认）
            //
            this.btnPowerOff.BackColor = System.Drawing.Color.DodgerBlue;
            this.btnPowerOff.ForeColor = System.Drawing.Color.White;
            this.btnPowerOff.Location = new System.Drawing.Point(370, 66);
            this.btnPowerOff.Name = "btnPowerOff";
            this.btnPowerOff.Size = new System.Drawing.Size(90, 30);
            this.btnPowerOff.TabIndex = 13;
            this.btnPowerOff.Text = "下电";
            this.btnPowerOff.UseVisualStyleBackColor = false;
            this.btnPowerOff.Click += new System.EventHandler(this.btnPowerOff_Click);
            //
            // btnSave - 保存按钮（功能待确认）
            //
            this.btnSave.BackColor = System.Drawing.Color.LimeGreen;
            this.btnSave.ForeColor = System.Drawing.Color.White;
            this.btnSave.Location = new System.Drawing.Point(370, 107);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(90, 30);
            this.btnSave.TabIndex = 14;
            this.btnSave.Text = "保存";
            this.btnSave.UseVisualStyleBackColor = false;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            //
            // btnAddToQueue - 加入对列按钮（功能待确认）
            //
            this.btnAddToQueue.BackColor = System.Drawing.Color.LimeGreen;
            this.btnAddToQueue.ForeColor = System.Drawing.Color.White;
            this.btnAddToQueue.Location = new System.Drawing.Point(370, 148);
            this.btnAddToQueue.Name = "btnAddToQueue";
            this.btnAddToQueue.Size = new System.Drawing.Size(90, 30);
            this.btnAddToQueue.TabIndex = 15;
            this.btnAddToQueue.Text = "加入对列";
            this.btnAddToQueue.UseVisualStyleBackColor = false;
            this.btnAddToQueue.Click += new System.EventHandler(this.btnAddToQueue_Click);
            //
            // btnClose - 关闭窗口按钮
            //
            this.btnClose.BackColor = System.Drawing.Color.DimGray;
            this.btnClose.ForeColor = System.Drawing.Color.White;
            this.btnClose.Location = new System.Drawing.Point(370, 189);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(90, 30);
            this.btnClose.TabIndex = 16;
            this.btnClose.Text = "关闭窗口";
            this.btnClose.UseVisualStyleBackColor = false;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            //
            // StationSettingsForm - 窗体属性设置
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(490, 270);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.btnAddToQueue);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.btnPowerOff);
            this.Controls.Add(this.btnBreakVacuum);
            this.Controls.Add(this.txtTemp);
            this.Controls.Add(this.lblTemp);
            this.Controls.Add(this.nudStartSeconds);
            this.Controls.Add(this.lblStartColon2);
            this.Controls.Add(this.nudStartMinutes);
            this.Controls.Add(this.lblStartColon1);
            this.Controls.Add(this.nudStartHours);
            this.Controls.Add(this.lblStart);
            this.Controls.Add(this.nudDelaySeconds);
            this.Controls.Add(this.lblDelayColon2);
            this.Controls.Add(this.nudDelayMinutes);
            this.Controls.Add(this.lblDelayColon1);
            this.Controls.Add(this.nudDelayHours);
            this.Controls.Add(this.lblDelay);
            this.Controls.Add(this.txtRecipe);
            this.Controls.Add(this.lblRecipe);
            this.Controls.Add(this.txtSN);
            this.Controls.Add(this.lblSN);
            this.Controls.Add(this.txtState);
            this.Controls.Add(this.lblState);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "StationSettingsForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "工位设置窗口";
            ((System.ComponentModel.ISupportInitialize)(this.nudDelayHours)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudDelayMinutes)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudDelaySeconds)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudStartHours)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudStartMinutes)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudStartSeconds)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        // 控件字段声明区域
        /// <summary>"状态"设置项名称标签</summary>
        private System.Windows.Forms.Label lblState;
        /// <summary>状态显示输入框（只读）</summary>
        private System.Windows.Forms.TextBox txtState;
        /// <summary>"SN"设置项名称标签</summary>
        private System.Windows.Forms.Label lblSN;
        /// <summary>SN输入框</summary>
        private System.Windows.Forms.TextBox txtSN;
        /// <summary>"配方"设置项名称标签</summary>
        private System.Windows.Forms.Label lblRecipe;
        /// <summary>配方输入框</summary>
        private System.Windows.Forms.TextBox txtRecipe;
        /// <summary>"延时时间"设置项名称标签</summary>
        private System.Windows.Forms.Label lblDelay;
        /// <summary>延时时间-时输入（NumericUpDown，V1.28 由 TextBox 改）</summary>
        private System.Windows.Forms.NumericUpDown nudDelayHours;
        /// <summary>延时时间-时/分冒号分隔符</summary>
        private System.Windows.Forms.Label lblDelayColon1;
        /// <summary>延时时间-分输入（NumericUpDown，V1.28 由 TextBox 改）</summary>
        private System.Windows.Forms.NumericUpDown nudDelayMinutes;
        /// <summary>延时时间-分/秒冒号分隔符</summary>
        private System.Windows.Forms.Label lblDelayColon2;
        /// <summary>延时时间-秒输入（NumericUpDown，V1.28 由 TextBox 改）</summary>
        private System.Windows.Forms.NumericUpDown nudDelaySeconds;
        /// <summary>"启动时间"设置项名称标签</summary>
        private System.Windows.Forms.Label lblStart;
        /// <summary>启动时间-时输入（NumericUpDown，V1.28 由 TextBox 改）</summary>
        private System.Windows.Forms.NumericUpDown nudStartHours;
        /// <summary>启动时间-时/分冒号分隔符</summary>
        private System.Windows.Forms.Label lblStartColon1;
        /// <summary>启动时间-分输入（NumericUpDown，V1.28 由 TextBox 改）</summary>
        private System.Windows.Forms.NumericUpDown nudStartMinutes;
        /// <summary>启动时间-分/秒冒号分隔符</summary>
        private System.Windows.Forms.Label lblStartColon2;
        /// <summary>启动时间-秒输入（NumericUpDown，V1.28 由 TextBox 改）</summary>
        private System.Windows.Forms.NumericUpDown nudStartSeconds;
        /// <summary>"极限温度"设置项名称标签</summary>
        private System.Windows.Forms.Label lblTemp;
        /// <summary>极限温度输入框</summary>
        private System.Windows.Forms.TextBox txtTemp;
        /// <summary>破空按钮（功能待确认）</summary>
        private System.Windows.Forms.Button btnBreakVacuum;
        /// <summary>下电按钮（功能待确认）</summary>
        private System.Windows.Forms.Button btnPowerOff;
        /// <summary>保存按钮（功能待确认）</summary>
        private System.Windows.Forms.Button btnSave;
        /// <summary>加入对列按钮（功能待确认）</summary>
        private System.Windows.Forms.Button btnAddToQueue;
        /// <summary>关闭窗口按钮</summary>
        private System.Windows.Forms.Button btnClose;
    }
}
