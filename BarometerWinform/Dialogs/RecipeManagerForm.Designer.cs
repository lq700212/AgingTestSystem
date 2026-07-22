namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 配方管理窗体 —— 设计器自动生成部分
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
            this.dgvRecipes = new System.Windows.Forms.DataGridView();
            this.colId = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colPressure = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colDelayStart = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colDelayArrive = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colTempLimit = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.panelButtons = new System.Windows.Forms.Panel();
            this.btnAdd = new System.Windows.Forms.Button();
            this.btnEdit = new System.Windows.Forms.Button();
            this.btnDelete = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.dgvRecipes)).BeginInit();
            this.panelButtons.SuspendLayout();
            this.SuspendLayout();
            //
            // dgvRecipes - 配方列表表格
            //
            this.dgvRecipes.AllowUserToAddRows = false;
            this.dgvRecipes.AllowUserToDeleteRows = false;
            this.dgvRecipes.AllowUserToResizeRows = false;
            this.dgvRecipes.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvRecipes.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvRecipes.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
                this.colId,
                this.colName,
                this.colPressure,
                this.colDelayStart,
                this.colDelayArrive,
                this.colTempLimit});
            this.dgvRecipes.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvRecipes.Location = new System.Drawing.Point(0, 0);
            this.dgvRecipes.MultiSelect = false;
            this.dgvRecipes.Name = "dgvRecipes";
            this.dgvRecipes.ReadOnly = true;
            this.dgvRecipes.RowHeadersVisible = false;
            this.dgvRecipes.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            //
            // colId - 编号列
            //
            this.colId.HeaderText = "编号";
            this.colId.Name = "colId";
            //
            // colName - 名称列
            //
            this.colName.HeaderText = "配方名称";
            this.colName.Name = "colName";
            //
            // colPressure - 负压值列
            //
            this.colPressure.HeaderText = "负压值(Pa)";
            this.colPressure.Name = "colPressure";
            //
            // colDelayStart - 延时开启列
            //
            this.colDelayStart.HeaderText = "延时开启";
            this.colDelayStart.Name = "colDelayStart";
            //
            // colDelayArrive - 延时到达列
            //
            this.colDelayArrive.HeaderText = "延时到达";
            this.colDelayArrive.Name = "colDelayArrive";
            //
            // colTempLimit - 极限温度列
            //
            this.colTempLimit.HeaderText = "极限温度(℃)";
            this.colTempLimit.Name = "colTempLimit";
            //
            // panelButtons - 底部按钮面板
            //
            this.panelButtons.Controls.Add(this.btnAdd);
            this.panelButtons.Controls.Add(this.btnEdit);
            this.panelButtons.Controls.Add(this.btnDelete);
            this.panelButtons.Controls.Add(this.btnClose);
            this.panelButtons.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelButtons.Location = new System.Drawing.Point(0, 320);
            this.panelButtons.Name = "panelButtons";
            this.panelButtons.Size = new System.Drawing.Size(684, 50);
            //
            // btnAdd - 新增按钮
            //
            this.btnAdd.Location = new System.Drawing.Point(20, 10);
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Size = new System.Drawing.Size(100, 30);
            this.btnAdd.Text = "新增";
            this.btnAdd.Click += new System.EventHandler(this.btnAdd_Click);
            //
            // btnEdit - 编辑按钮
            //
            this.btnEdit.Location = new System.Drawing.Point(130, 10);
            this.btnEdit.Name = "btnEdit";
            this.btnEdit.Size = new System.Drawing.Size(100, 30);
            this.btnEdit.Text = "编辑";
            this.btnEdit.Click += new System.EventHandler(this.btnEdit_Click);
            //
            // btnDelete - 删除按钮
            //
            this.btnDelete.Location = new System.Drawing.Point(240, 10);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(100, 30);
            this.btnDelete.Text = "删除";
            this.btnDelete.Click += new System.EventHandler(this.btnDelete_Click);
            //
            // btnClose - 关闭按钮
            //
            this.btnClose.Location = new System.Drawing.Point(564, 10);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(100, 30);
            this.btnClose.Text = "关闭";
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            //
            // RecipeManagerForm - 窗体自身
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(684, 370);
            this.Controls.Add(this.dgvRecipes);
            this.Controls.Add(this.panelButtons);
            this.Name = "RecipeManagerForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "配方管理";
            ((System.ComponentModel.ISupportInitialize)(this.dgvRecipes)).EndInit();
            this.panelButtons.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.DataGridView dgvRecipes;
        private System.Windows.Forms.DataGridViewTextBoxColumn colId;
        private System.Windows.Forms.DataGridViewTextBoxColumn colName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colPressure;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDelayStart;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDelayArrive;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTempLimit;
        private System.Windows.Forms.Panel panelButtons;
        private System.Windows.Forms.Button btnAdd;
        private System.Windows.Forms.Button btnEdit;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.Button btnClose;
    }
}
