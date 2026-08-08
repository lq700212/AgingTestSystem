namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 配方管理窗体 —— 设计器自动生成部分
    ///
    /// 【界面布局说明】
    /// 左右分栏布局：
    /// - 左侧：DataGridView 只显示序号和配方名称两列
    /// - 右侧：可编辑输入区
    ///   - 配方名称：TextBox
    ///   - 延时时间 / 启动时间：三个 NumericUpDown 以冒号分隔显示（时:分:秒，V1.28）
    ///   - 极限温度：NumericUpDown + ℃ 单位
    /// - 底部按钮区域：添加、更新、删除按钮在右侧设置区底部（V1.27 起无底部"保存设置"按钮，
    ///   添加/更新/删除操作即自动落盘）
    /// </summary>
    partial class RecipeManagerForm
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
            this.tableLayoutPanelMain = new System.Windows.Forms.TableLayoutPanel();
            this.panelLeft = new System.Windows.Forms.Panel();
            this.dgvRecipes = new System.Windows.Forms.DataGridView();
            this.colSeq = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panelRight = new System.Windows.Forms.Panel();
            this.btnDelete = new System.Windows.Forms.Button();
            this.btnUpdate = new System.Windows.Forms.Button();
            this.btnAdd = new System.Windows.Forms.Button();
            this.lblLimitTempUnit = new System.Windows.Forms.Label();
            this.nudLimitTemp = new System.Windows.Forms.NumericUpDown();
            this.lblLimitTemp = new System.Windows.Forms.Label();
            this.nudStartSeconds = new System.Windows.Forms.NumericUpDown();
            this.lblStartMinutesUnit = new System.Windows.Forms.Label();
            this.nudStartMinutes = new System.Windows.Forms.NumericUpDown();
            this.lblStartHoursUnit = new System.Windows.Forms.Label();
            this.nudStartHours = new System.Windows.Forms.NumericUpDown();
            this.lblStartTime = new System.Windows.Forms.Label();
            this.nudDelaySeconds = new System.Windows.Forms.NumericUpDown();
            this.lblDelayMinutesUnit = new System.Windows.Forms.Label();
            this.nudDelayMinutes = new System.Windows.Forms.NumericUpDown();
            this.lblDelayHoursUnit = new System.Windows.Forms.Label();
            this.nudDelayHours = new System.Windows.Forms.NumericUpDown();
            this.lblDelayTime = new System.Windows.Forms.Label();
            this.txtRecipeName = new System.Windows.Forms.TextBox();
            this.lblRecipeName = new System.Windows.Forms.Label();
            this.lblRecipeSettings = new System.Windows.Forms.Label();
            this.tableLayoutPanelMain.SuspendLayout();
            this.panelLeft.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvRecipes)).BeginInit();
            this.panelRight.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudLimitTemp)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudStartSeconds)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudStartMinutes)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudStartHours)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudDelaySeconds)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudDelayMinutes)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudDelayHours)).BeginInit();
            this.SuspendLayout();
            //
            // tableLayoutPanelMain
            //
            this.tableLayoutPanelMain.ColumnCount = 2;
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 45F));
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 55F));
            this.tableLayoutPanelMain.Controls.Add(this.panelLeft, 0, 0);
            this.tableLayoutPanelMain.Controls.Add(this.panelRight, 1, 0);
            this.tableLayoutPanelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelMain.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanelMain.Name = "tableLayoutPanelMain";
            this.tableLayoutPanelMain.RowCount = 1;
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.Size = new System.Drawing.Size(700, 353);
            this.tableLayoutPanelMain.TabIndex = 0;
            //
            // panelLeft
            //
            this.panelLeft.Controls.Add(this.dgvRecipes);
            this.panelLeft.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelLeft.Location = new System.Drawing.Point(3, 3);
            this.panelLeft.Margin = new System.Windows.Forms.Padding(3, 3, 0, 3);
            this.panelLeft.Name = "panelLeft";
            this.panelLeft.Size = new System.Drawing.Size(312, 347);
            this.panelLeft.TabIndex = 0;
            //
            // dgvRecipes
            //
            this.dgvRecipes.AllowUserToAddRows = false;
            this.dgvRecipes.AllowUserToDeleteRows = false;
            this.dgvRecipes.AllowUserToResizeRows = false;
            this.dgvRecipes.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvRecipes.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.None;
            this.dgvRecipes.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvRecipes.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colSeq,
            this.colName});
            this.dgvRecipes.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvRecipes.Location = new System.Drawing.Point(0, 0);
            this.dgvRecipes.MultiSelect = false;
            this.dgvRecipes.Name = "dgvRecipes";
            this.dgvRecipes.ReadOnly = true;
            this.dgvRecipes.RowHeadersVisible = false;
            this.dgvRecipes.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.dgvRecipes.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvRecipes.Size = new System.Drawing.Size(312, 347);
            this.dgvRecipes.TabIndex = 0;
            this.dgvRecipes.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvRecipes_CellClick);
            //
            // colSeq
            //
            this.colSeq.HeaderText = "序号";
            this.colSeq.Name = "colSeq";
            this.colSeq.ReadOnly = true;
            //
            // colName
            //
            this.colName.HeaderText = "配方名称";
            this.colName.Name = "colName";
            this.colName.ReadOnly = true;
            //
            // panelRight
            //
            this.panelRight.Controls.Add(this.btnDelete);
            this.panelRight.Controls.Add(this.btnUpdate);
            this.panelRight.Controls.Add(this.btnAdd);
            this.panelRight.Controls.Add(this.lblLimitTempUnit);
            this.panelRight.Controls.Add(this.nudLimitTemp);
            this.panelRight.Controls.Add(this.lblLimitTemp);
            this.panelRight.Controls.Add(this.nudStartSeconds);
            this.panelRight.Controls.Add(this.lblStartMinutesUnit);
            this.panelRight.Controls.Add(this.nudStartMinutes);
            this.panelRight.Controls.Add(this.lblStartHoursUnit);
            this.panelRight.Controls.Add(this.nudStartHours);
            this.panelRight.Controls.Add(this.lblStartTime);
            this.panelRight.Controls.Add(this.nudDelaySeconds);
            this.panelRight.Controls.Add(this.lblDelayMinutesUnit);
            this.panelRight.Controls.Add(this.nudDelayMinutes);
            this.panelRight.Controls.Add(this.lblDelayHoursUnit);
            this.panelRight.Controls.Add(this.nudDelayHours);
            this.panelRight.Controls.Add(this.lblDelayTime);
            this.panelRight.Controls.Add(this.txtRecipeName);
            this.panelRight.Controls.Add(this.lblRecipeName);
            this.panelRight.Controls.Add(this.lblRecipeSettings);
            this.panelRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelRight.Location = new System.Drawing.Point(321, 3);
            this.panelRight.Margin = new System.Windows.Forms.Padding(6, 3, 3, 3);
            this.panelRight.Name = "panelRight";
            this.panelRight.Size = new System.Drawing.Size(376, 347);
            this.panelRight.TabIndex = 1;
            //
            // btnDelete
            //
            this.btnDelete.Location = new System.Drawing.Point(260, 280);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(75, 23);
            this.btnDelete.TabIndex = 15;
            this.btnDelete.Text = "删除";
            this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);
            //
            // btnUpdate
            //
            this.btnUpdate.Location = new System.Drawing.Point(165, 280);
            this.btnUpdate.Name = "btnUpdate";
            this.btnUpdate.Size = new System.Drawing.Size(75, 23);
            this.btnUpdate.TabIndex = 14;
            this.btnUpdate.Text = "更新";
            this.btnUpdate.Click += new System.EventHandler(this.btnUpdate_Click);
            //
            // btnAdd
            //
            this.btnAdd.Location = new System.Drawing.Point(70, 280);
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Size = new System.Drawing.Size(75, 23);
            this.btnAdd.TabIndex = 13;
            this.btnAdd.Text = "添加";
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
            //
            // lblLimitTemp
            //
            this.lblLimitTemp.AutoSize = true;
            this.lblLimitTemp.Location = new System.Drawing.Point(30, 180);
            this.lblLimitTemp.Name = "lblLimitTemp";
            this.lblLimitTemp.Size = new System.Drawing.Size(65, 12);
            this.lblLimitTemp.TabIndex = 11;
            this.lblLimitTemp.Text = "极限温度：";
            //
            // nudLimitTemp
            //
            this.nudLimitTemp.DecimalPlaces = 1;
            this.nudLimitTemp.Increment = 0.5M;
            this.nudLimitTemp.Location = new System.Drawing.Point(95, 176);
            this.nudLimitTemp.Maximum = new decimal(new int[] {
            300,
            0,
            0,
            0});
            this.nudLimitTemp.Name = "nudLimitTemp";
            this.nudLimitTemp.Size = new System.Drawing.Size(70, 21);
            this.nudLimitTemp.TabIndex = 12;
            this.nudLimitTemp.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // lblLimitTempUnit
            //
            this.lblLimitTempUnit.AutoSize = true;
            this.lblLimitTempUnit.Location = new System.Drawing.Point(167, 182);
            this.lblLimitTempUnit.Name = "lblLimitTempUnit";
            this.lblLimitTempUnit.Size = new System.Drawing.Size(12, 12);
            this.lblLimitTempUnit.TabIndex = 0;
            this.lblLimitTempUnit.Text = "℃";
            //
            // lblStartTime
            //
            this.lblStartTime.AutoSize = true;
            this.lblStartTime.Location = new System.Drawing.Point(30, 145);
            this.lblStartTime.Name = "lblStartTime";
            this.lblStartTime.Size = new System.Drawing.Size(65, 12);
            this.lblStartTime.TabIndex = 7;
            this.lblStartTime.Text = "启动时间：";
            //
            // nudStartHours
            //
            this.nudStartHours.Location = new System.Drawing.Point(95, 141);
            this.nudStartHours.Maximum = new decimal(new int[] {
            99,
            0,
            0,
            0});
            this.nudStartHours.Name = "nudStartHours";
            this.nudStartHours.Size = new System.Drawing.Size(48, 21);
            this.nudStartHours.TabIndex = 8;
            this.nudStartHours.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // lblStartHoursUnit - 启动时间：时与分之间的冒号分隔符（V1.28 由"时"单位改为":"）
            //
            this.lblStartHoursUnit.AutoSize = true;
            this.lblStartHoursUnit.Location = new System.Drawing.Point(145, 145);
            this.lblStartHoursUnit.Name = "lblStartHoursUnit";
            this.lblStartHoursUnit.Size = new System.Drawing.Size(6, 12);
            this.lblStartHoursUnit.TabIndex = 0;
            this.lblStartHoursUnit.Text = ":";
            //
            // nudStartMinutes
            //
            this.nudStartMinutes.Location = new System.Drawing.Point(159, 141);
            this.nudStartMinutes.Maximum = new decimal(new int[] {
            59,
            0,
            0,
            0});
            this.nudStartMinutes.Name = "nudStartMinutes";
            this.nudStartMinutes.Size = new System.Drawing.Size(48, 21);
            this.nudStartMinutes.TabIndex = 9;
            this.nudStartMinutes.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // lblStartMinutesUnit - 启动时间：分与秒之间的冒号分隔符（V1.28 由"分"单位改为":"）
            //
            this.lblStartMinutesUnit.AutoSize = true;
            this.lblStartMinutesUnit.Location = new System.Drawing.Point(209, 145);
            this.lblStartMinutesUnit.Name = "lblStartMinutesUnit";
            this.lblStartMinutesUnit.Size = new System.Drawing.Size(6, 12);
            this.lblStartMinutesUnit.TabIndex = 0;
            this.lblStartMinutesUnit.Text = ":";
            //
            // nudStartSeconds
            //
            this.nudStartSeconds.Location = new System.Drawing.Point(223, 141);
            this.nudStartSeconds.Maximum = new decimal(new int[] {
            59,
            0,
            0,
            0});
            this.nudStartSeconds.Name = "nudStartSeconds";
            this.nudStartSeconds.Size = new System.Drawing.Size(48, 21);
            this.nudStartSeconds.TabIndex = 10;
            this.nudStartSeconds.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // lblDelayTime
            //
            this.lblDelayTime.AutoSize = true;
            this.lblDelayTime.Location = new System.Drawing.Point(30, 110);
            this.lblDelayTime.Name = "lblDelayTime";
            this.lblDelayTime.Size = new System.Drawing.Size(65, 12);
            this.lblDelayTime.TabIndex = 3;
            this.lblDelayTime.Text = "延时时间：";
            //
            // nudDelayHours
            //
            this.nudDelayHours.Location = new System.Drawing.Point(95, 106);
            this.nudDelayHours.Maximum = new decimal(new int[] {
            99,
            0,
            0,
            0});
            this.nudDelayHours.Name = "nudDelayHours";
            this.nudDelayHours.Size = new System.Drawing.Size(48, 21);
            this.nudDelayHours.TabIndex = 4;
            this.nudDelayHours.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // lblDelayHoursUnit - 延时时间：时与分之间的冒号分隔符（V1.28 由"时"单位改为":"）
            //
            this.lblDelayHoursUnit.AutoSize = true;
            this.lblDelayHoursUnit.Location = new System.Drawing.Point(145, 110);
            this.lblDelayHoursUnit.Name = "lblDelayHoursUnit";
            this.lblDelayHoursUnit.Size = new System.Drawing.Size(6, 12);
            this.lblDelayHoursUnit.TabIndex = 0;
            this.lblDelayHoursUnit.Text = ":";
            //
            // nudDelayMinutes
            //
            this.nudDelayMinutes.Location = new System.Drawing.Point(159, 106);
            this.nudDelayMinutes.Maximum = new decimal(new int[] {
            59,
            0,
            0,
            0});
            this.nudDelayMinutes.Name = "nudDelayMinutes";
            this.nudDelayMinutes.Size = new System.Drawing.Size(48, 21);
            this.nudDelayMinutes.TabIndex = 5;
            this.nudDelayMinutes.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // lblDelayMinutesUnit - 延时时间：分与秒之间的冒号分隔符（V1.28 由"分"单位改为":"）
            //
            this.lblDelayMinutesUnit.AutoSize = true;
            this.lblDelayMinutesUnit.Location = new System.Drawing.Point(209, 110);
            this.lblDelayMinutesUnit.Name = "lblDelayMinutesUnit";
            this.lblDelayMinutesUnit.Size = new System.Drawing.Size(6, 12);
            this.lblDelayMinutesUnit.TabIndex = 0;
            this.lblDelayMinutesUnit.Text = ":";
            //
            // nudDelaySeconds
            //
            this.nudDelaySeconds.Location = new System.Drawing.Point(223, 106);
            this.nudDelaySeconds.Maximum = new decimal(new int[] {
            59,
            0,
            0,
            0});
            this.nudDelaySeconds.Name = "nudDelaySeconds";
            this.nudDelaySeconds.Size = new System.Drawing.Size(48, 21);
            this.nudDelaySeconds.TabIndex = 6;
            this.nudDelaySeconds.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // lblRecipeName
            //
            this.lblRecipeName.AutoSize = true;
            this.lblRecipeName.Location = new System.Drawing.Point(30, 75);
            this.lblRecipeName.Name = "lblRecipeName";
            this.lblRecipeName.Size = new System.Drawing.Size(65, 12);
            this.lblRecipeName.TabIndex = 1;
            this.lblRecipeName.Text = "配方名称：";
            //
            // txtRecipeName
            //
            this.txtRecipeName.Location = new System.Drawing.Point(95, 72);
            this.txtRecipeName.Name = "txtRecipeName";
            this.txtRecipeName.Size = new System.Drawing.Size(180, 21);
            this.txtRecipeName.TabIndex = 2;
            //
            // lblRecipeSettings
            //
            this.lblRecipeSettings.AutoSize = true;
            this.lblRecipeSettings.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblRecipeSettings.Location = new System.Drawing.Point(140, 20);
            this.lblRecipeSettings.Name = "lblRecipeSettings";
            this.lblRecipeSettings.Size = new System.Drawing.Size(65, 19);
            this.lblRecipeSettings.TabIndex = 0;
            this.lblRecipeSettings.Text = "配方设置";
            //
            // RecipeManagerForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(700, 355);
            this.Controls.Add(this.tableLayoutPanelMain);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "RecipeManagerForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "配方管理窗口";
            this.tableLayoutPanelMain.ResumeLayout(false);
            this.panelLeft.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvRecipes)).EndInit();
            this.panelRight.ResumeLayout(false);
            this.panelRight.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudLimitTemp)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudStartSeconds)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudStartMinutes)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudStartHours)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudDelaySeconds)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudDelayMinutes)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudDelayHours)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        // 控件字段声明区域
        // 这些字段在两个 partial 文件中共享（本文件赋值，.cs文件使用）

        /// <summary>主布局容器（2列：左侧列表区/右侧设置区）</summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelMain;

        /// <summary>左侧配方列表区域面板</summary>
        private System.Windows.Forms.Panel panelLeft;

        /// <summary>右侧配方设置区域面板</summary>
        private System.Windows.Forms.Panel panelRight;

        /// <summary>配方列表表格</summary>
        private System.Windows.Forms.DataGridView dgvRecipes;

        /// <summary>序号列</summary>
        private System.Windows.Forms.DataGridViewTextBoxColumn colSeq;

        /// <summary>配方名称列</summary>
        private System.Windows.Forms.DataGridViewTextBoxColumn colName;

        /// <summary>配方设置标题标签</summary>
        private System.Windows.Forms.Label lblRecipeSettings;

        /// <summary>配方名称标签</summary>
        private System.Windows.Forms.Label lblRecipeName;

        /// <summary>配方名称输入框</summary>
        private System.Windows.Forms.TextBox txtRecipeName;

        /// <summary>延时时间标签</summary>
        private System.Windows.Forms.Label lblDelayTime;

        /// <summary>延时时间：时输入</summary>
        private System.Windows.Forms.NumericUpDown nudDelayHours;

        /// <summary>延时时间：时单位</summary>
        private System.Windows.Forms.Label lblDelayHoursUnit;

        /// <summary>延时时间：分输入</summary>
        private System.Windows.Forms.NumericUpDown nudDelayMinutes;

        /// <summary>延时时间：分单位</summary>
        private System.Windows.Forms.Label lblDelayMinutesUnit;

        /// <summary>延时时间：秒输入</summary>
        private System.Windows.Forms.NumericUpDown nudDelaySeconds;

        /// <summary>启动时间标签</summary>
        private System.Windows.Forms.Label lblStartTime;

        /// <summary>启动时间：时输入</summary>
        private System.Windows.Forms.NumericUpDown nudStartHours;

        /// <summary>启动时间：时单位</summary>
        private System.Windows.Forms.Label lblStartHoursUnit;

        /// <summary>启动时间：分输入</summary>
        private System.Windows.Forms.NumericUpDown nudStartMinutes;

        /// <summary>启动时间：分单位</summary>
        private System.Windows.Forms.Label lblStartMinutesUnit;

        /// <summary>启动时间：秒输入</summary>
        private System.Windows.Forms.NumericUpDown nudStartSeconds;

        /// <summary>极限温度标签</summary>
        private System.Windows.Forms.Label lblLimitTemp;

        /// <summary>极限温度输入</summary>
        private System.Windows.Forms.NumericUpDown nudLimitTemp;

        /// <summary>极限温度单位</summary>
        private System.Windows.Forms.Label lblLimitTempUnit;

        /// <summary>添加按钮</summary>
        private System.Windows.Forms.Button btnAdd;

        /// <summary>更新按钮</summary>
        private System.Windows.Forms.Button btnUpdate;

        /// <summary>删除按钮</summary>
        private System.Windows.Forms.Button btnDelete;
    }
}
