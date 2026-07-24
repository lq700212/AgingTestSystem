namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 配方管理窗体 —— 设计器自动生成部分
    /// 
    /// 【界面布局说明】
    /// 根据用户提供的图片样式，采用左右分栏布局：
    /// 
    /// ┌─────────────────────────────────────────────────────────────┐
    /// │ 配方管理窗口                                               │ ← 标题栏
    /// ├─────────────────────────────────────────────────────────────┤
    /// │ TableLayoutPanel (2列)                                      │
    /// │ ┌─────────────────────────┐  ┌─────────────────────────┐   │
    /// │ │ PanelLeft (左侧列表区)  │  │ PanelRight (右侧设置区) │   │
    /// │ │ ┌─────────────────────┐ │  │ ┌─────────────────────┐ │   │
    /// │ │ │ 序号  │ 配方名称     │ │  │ │ 配方设置            │ │   │
    /// │ │ │───────┼─────────────│ │  │ │ 配方名称：ABCDEFGH   │ │   │
    /// │ │ │ 1     │ ABCDEFGH    │ │  │ │ 延时时间：1:20:30    │ │   │
    /// │ │ │ 2     │ BBVJKNVK    │ │  │ │ 启动时间：2:10:5     │ │   │
    /// │ │ │ 3     │ RFTYHYJWF   │ │  │ │ 极限温度：50℃        │ │   │
    /// │ │ │ 4     │ WFRWGYJUK   │ │  │ ├─────────────────────┤ │   │
    /// │ │ │ 5     │ FGYJKIewF   │ │  │ │ [添加] [更新] [删除] │ │   │
    /// │ │ │(带滚动条)           │ │  │ └─────────────────────┘ │   │
    /// │ │ └─────────────────────┘ │  └─────────────────────────┘   │
    /// │ └─────────────────────────┘                                │
    /// ├─────────────────────────────────────────────────────────────┤
    /// │ [保存设置] (横跨整个底部)                                    │
    /// └─────────────────────────────────────────────────────────────┘
    /// 
    /// 【控件说明】
    /// - 左侧：DataGridView 只显示序号和配方名称两列
    /// - 右侧：Label 控件显示选中配方的详细信息（配方名称、延时时间、启动时间、极限温度）
    /// - 底部按钮区域：添加、更新、删除按钮在右侧设置区底部；保存设置按钮横跨底部
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
            this.lblLimitTemp = new System.Windows.Forms.Label();
            this.lblLimitTempValue = new System.Windows.Forms.Label();
            this.lblStartTime = new System.Windows.Forms.Label();
            this.lblStartTimeValue = new System.Windows.Forms.Label();
            this.lblDelayTime = new System.Windows.Forms.Label();
            this.lblDelayTimeValue = new System.Windows.Forms.Label();
            this.lblRecipeName = new System.Windows.Forms.Label();
            this.lblRecipeNameValue = new System.Windows.Forms.Label();
            this.lblRecipeNameLabel = new System.Windows.Forms.Label();
            this.lblRecipeSettings = new System.Windows.Forms.Label();
            this.panelBottom = new System.Windows.Forms.Panel();
            this.btnSaveSettings = new System.Windows.Forms.Button();
            this.tableLayoutPanelMain.SuspendLayout();
            this.panelLeft.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvRecipes)).BeginInit();
            this.panelRight.SuspendLayout();
            this.panelBottom.SuspendLayout();
            this.SuspendLayout();
            //
            // tableLayoutPanelMain - 主布局容器（2列：左侧列表区/右侧设置区）
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
            this.tableLayoutPanelMain.Size = new System.Drawing.Size(700, 400);
            this.tableLayoutPanelMain.TabIndex = 0;
            //
            // panelLeft - 左侧配方列表区域面板
            //
            this.panelLeft.Controls.Add(this.dgvRecipes);
            this.panelLeft.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelLeft.Location = new System.Drawing.Point(3, 3);
            this.panelLeft.Margin = new System.Windows.Forms.Padding(3, 3, 0, 3);
            this.panelLeft.Name = "panelLeft";
            this.panelLeft.Size = new System.Drawing.Size(309, 347);
            this.panelLeft.TabIndex = 0;
            //
            // dgvRecipes - 配方列表表格（只显示序号和配方名称）
            //
            this.dgvRecipes.AllowUserToAddRows = false;
            this.dgvRecipes.AllowUserToDeleteRows = false;
            this.dgvRecipes.AllowUserToResizeRows = false;
            this.dgvRecipes.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
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
            this.dgvRecipes.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvRecipes.Size = new System.Drawing.Size(309, 347);
            this.dgvRecipes.TabIndex = 0;
            this.dgvRecipes.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvRecipes_CellClick);
            //
            // colSeq - 序号列
            //
            this.colSeq.HeaderText = "序号";
            this.colSeq.Name = "colSeq";
            this.colSeq.Width = 60;
            //
            // colName - 配方名称列
            //
            this.colName.HeaderText = "配方名称";
            this.colName.Name = "colName";
            //
            // panelRight - 右侧配方设置区域面板
            //
            this.panelRight.Controls.Add(this.btnDelete);
            this.panelRight.Controls.Add(this.btnUpdate);
            this.panelRight.Controls.Add(this.btnAdd);
            this.panelRight.Controls.Add(this.lblLimitTemp);
            this.panelRight.Controls.Add(this.lblLimitTempValue);
            this.panelRight.Controls.Add(this.lblStartTime);
            this.panelRight.Controls.Add(this.lblStartTimeValue);
            this.panelRight.Controls.Add(this.lblDelayTime);
            this.panelRight.Controls.Add(this.lblDelayTimeValue);
            this.panelRight.Controls.Add(this.lblRecipeName);
            this.panelRight.Controls.Add(this.lblRecipeNameValue);
            this.panelRight.Controls.Add(this.lblRecipeNameLabel);
            this.panelRight.Controls.Add(this.lblRecipeSettings);
            this.panelRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelRight.Location = new System.Drawing.Point(318, 3);
            this.panelRight.Margin = new System.Windows.Forms.Padding(6, 3, 3, 3);
            this.panelRight.Name = "panelRight";
            this.panelRight.Size = new System.Drawing.Size(379, 347);
            this.panelRight.TabIndex = 1;
            //
            // btnDelete - 删除按钮
            //
            this.btnDelete.Location = new System.Drawing.Point(260, 280);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(75, 23);
            this.btnDelete.TabIndex = 12;
            this.btnDelete.Text = "删除";
            this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);
            //
            // btnUpdate - 更新按钮
            //
            this.btnUpdate.Location = new System.Drawing.Point(165, 280);
            this.btnUpdate.Name = "btnUpdate";
            this.btnUpdate.Size = new System.Drawing.Size(75, 23);
            this.btnUpdate.TabIndex = 11;
            this.btnUpdate.Text = "更新";
            this.btnUpdate.Click += new System.EventHandler(this.btnUpdate_Click);
            //
            // btnAdd - 添加按钮
            //
            this.btnAdd.Location = new System.Drawing.Point(70, 280);
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Size = new System.Drawing.Size(75, 23);
            this.btnAdd.TabIndex = 10;
            this.btnAdd.Text = "添加";
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
            //
            // lblLimitTemp - 极限温度标签
            //
            this.lblLimitTemp.AutoSize = true;
            this.lblLimitTemp.Location = new System.Drawing.Point(30, 180);
            this.lblLimitTemp.Name = "lblLimitTemp";
            this.lblLimitTemp.Size = new System.Drawing.Size(65, 12);
            this.lblLimitTemp.TabIndex = 9;
            this.lblLimitTemp.Text = "极限温度：";
            //
            // lblLimitTempValue - 极限温度值显示
            //
            this.lblLimitTempValue.AutoSize = true;
            this.lblLimitTempValue.Location = new System.Drawing.Point(95, 180);
            this.lblLimitTempValue.Name = "lblLimitTempValue";
            this.lblLimitTempValue.Size = new System.Drawing.Size(41, 12);
            this.lblLimitTempValue.TabIndex = 8;
            this.lblLimitTempValue.Text = "50℃";
            //
            // lblStartTime - 启动时间标签
            //
            this.lblStartTime.AutoSize = true;
            this.lblStartTime.Location = new System.Drawing.Point(30, 145);
            this.lblStartTime.Name = "lblStartTime";
            this.lblStartTime.Size = new System.Drawing.Size(65, 12);
            this.lblStartTime.TabIndex = 7;
            this.lblStartTime.Text = "启动时间：";
            //
            // lblStartTimeValue - 启动时间值显示
            //
            this.lblStartTimeValue.AutoSize = true;
            this.lblStartTimeValue.Location = new System.Drawing.Point(95, 145);
            this.lblStartTimeValue.Name = "lblStartTimeValue";
            this.lblStartTimeValue.Size = new System.Drawing.Size(53, 12);
            this.lblStartTimeValue.TabIndex = 6;
            this.lblStartTimeValue.Text = "2:10:5";
            //
            // lblDelayTime - 延时时间标签
            //
            this.lblDelayTime.AutoSize = true;
            this.lblDelayTime.Location = new System.Drawing.Point(30, 110);
            this.lblDelayTime.Name = "lblDelayTime";
            this.lblDelayTime.Size = new System.Drawing.Size(65, 12);
            this.lblDelayTime.TabIndex = 5;
            this.lblDelayTime.Text = "延时时间：";
            //
            // lblDelayTimeValue - 延时时间值显示
            //
            this.lblDelayTimeValue.AutoSize = true;
            this.lblDelayTimeValue.Location = new System.Drawing.Point(95, 110);
            this.lblDelayTimeValue.Name = "lblDelayTimeValue";
            this.lblDelayTimeValue.Size = new System.Drawing.Size(65, 12);
            this.lblDelayTimeValue.TabIndex = 4;
            this.lblDelayTimeValue.Text = "1:20:30";
            //
            // lblRecipeName - 配方名称标签
            //
            this.lblRecipeName.AutoSize = true;
            this.lblRecipeName.Location = new System.Drawing.Point(30, 75);
            this.lblRecipeName.Name = "lblRecipeName";
            this.lblRecipeName.Size = new System.Drawing.Size(65, 12);
            this.lblRecipeName.TabIndex = 3;
            this.lblRecipeName.Text = "配方名称：";
            //
            // lblRecipeNameValue - 配方名称值显示
            //
            this.lblRecipeNameValue.AutoSize = true;
            this.lblRecipeNameValue.Location = new System.Drawing.Point(95, 75);
            this.lblRecipeNameValue.Name = "lblRecipeNameValue";
            this.lblRecipeNameValue.Size = new System.Drawing.Size(77, 12);
            this.lblRecipeNameValue.TabIndex = 2;
            this.lblRecipeNameValue.Text = "ABCDEFGH";
            //
            // lblRecipeNameLabel - 配方名称标签（显示重复的配方名称）
            //
            this.lblRecipeNameLabel.AutoSize = true;
            this.lblRecipeNameLabel.Location = new System.Drawing.Point(95, 50);
            this.lblRecipeNameLabel.Name = "lblRecipeNameLabel";
            this.lblRecipeNameLabel.Size = new System.Drawing.Size(77, 12);
            this.lblRecipeNameLabel.TabIndex = 1;
            this.lblRecipeNameLabel.Text = "ABCDEFGH";
            //
            // lblRecipeSettings - 配方设置标题标签
            //
            this.lblRecipeSettings.AutoSize = true;
            this.lblRecipeSettings.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblRecipeSettings.Location = new System.Drawing.Point(140, 20);
            this.lblRecipeSettings.Name = "lblRecipeSettings";
            this.lblRecipeSettings.Size = new System.Drawing.Size(72, 19);
            this.lblRecipeSettings.TabIndex = 0;
            this.lblRecipeSettings.Text = "配方设置";
            //
            // panelBottom - 底部按钮面板（保存设置按钮）
            //
            this.panelBottom.Controls.Add(this.btnSaveSettings);
            this.panelBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelBottom.Location = new System.Drawing.Point(0, 353);
            this.panelBottom.Name = "panelBottom";
            this.panelBottom.Size = new System.Drawing.Size(700, 47);
            this.panelBottom.TabIndex = 1;
            //
            // btnSaveSettings - 保存设置按钮（横跨整个底部）
            //
            this.btnSaveSettings.Location = new System.Drawing.Point(270, 10);
            this.btnSaveSettings.Name = "btnSaveSettings";
            this.btnSaveSettings.Size = new System.Drawing.Size(160, 27);
            this.btnSaveSettings.TabIndex = 0;
            this.btnSaveSettings.Text = "保存设置";
            this.btnSaveSettings.Click += new System.EventHandler(this.btnSaveSettings_Click);
            //
            // RecipeManagerForm - 窗体自身属性设置
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(700, 400);
            this.Controls.Add(this.tableLayoutPanelMain);
            this.Controls.Add(this.panelBottom);
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
            this.panelBottom.ResumeLayout(false);
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

        /// <summary>底部按钮面板</summary>
        private System.Windows.Forms.Panel panelBottom;

        /// <summary>配方列表表格</summary>
        private System.Windows.Forms.DataGridView dgvRecipes;

        /// <summary>序号列</summary>
        private System.Windows.Forms.DataGridViewTextBoxColumn colSeq;

        /// <summary>配方名称列</summary>
        private System.Windows.Forms.DataGridViewTextBoxColumn colName;

        /// <summary>配方设置标题标签</summary>
        private System.Windows.Forms.Label lblRecipeSettings;

        /// <summary>配方名称标签（上方显示）</summary>
        private System.Windows.Forms.Label lblRecipeNameLabel;

        /// <summary>配方名称标签</summary>
        private System.Windows.Forms.Label lblRecipeName;

        /// <summary>配方名称值显示</summary>
        private System.Windows.Forms.Label lblRecipeNameValue;

        /// <summary>延时时间标签</summary>
        private System.Windows.Forms.Label lblDelayTime;

        /// <summary>延时时间值显示</summary>
        private System.Windows.Forms.Label lblDelayTimeValue;

        /// <summary>启动时间标签</summary>
        private System.Windows.Forms.Label lblStartTime;

        /// <summary>启动时间值显示</summary>
        private System.Windows.Forms.Label lblStartTimeValue;

        /// <summary>极限温度标签</summary>
        private System.Windows.Forms.Label lblLimitTemp;

        /// <summary>极限温度值显示</summary>
        private System.Windows.Forms.Label lblLimitTempValue;

        /// <summary>添加按钮</summary>
        private System.Windows.Forms.Button btnAdd;

        /// <summary>更新按钮</summary>
        private System.Windows.Forms.Button btnUpdate;

        /// <summary>删除按钮</summary>
        private System.Windows.Forms.Button btnDelete;

        /// <summary>保存设置按钮</summary>
        private System.Windows.Forms.Button btnSaveSettings;
    }
}