namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 历史记录查询窗体 —— 设计器自动生成部分
    /// </summary>
    partial class HistoryRecordForm
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
            this.panelTop = new System.Windows.Forms.Panel();
            this.lblStart = new Sunny.UI.UILabel();
            this.dtpStart = new System.Windows.Forms.DateTimePicker();
            this.lblEnd = new Sunny.UI.UILabel();
            this.dtpEnd = new System.Windows.Forms.DateTimePicker();
            this.btnQuery = new Sunny.UI.UIButton();
            this.btnExport = new Sunny.UI.UIButton();
            this.dgvHistory = new Sunny.UI.UIDataGridView();
            this.colTime = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colDevice = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colEvent = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colDetail = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.btnClose = new Sunny.UI.UIButton();
            this.panelBottom = new System.Windows.Forms.Panel();
            this.panelTop.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvHistory)).BeginInit();
            this.panelBottom.SuspendLayout();
            this.SuspendLayout();
            //
            // panelTop - 顶部查询条件面板
            //
            this.panelTop.Controls.Add(this.lblStart);
            this.panelTop.Controls.Add(this.dtpStart);
            this.panelTop.Controls.Add(this.lblEnd);
            this.panelTop.Controls.Add(this.dtpEnd);
            this.panelTop.Controls.Add(this.btnQuery);
            this.panelTop.Controls.Add(this.btnExport);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Location = new System.Drawing.Point(0, 0);
            this.panelTop.Name = "panelTop";
            this.panelTop.Size = new System.Drawing.Size(784, 50);
            //
            // lblStart - 开始时间标签
            //
            this.lblStart.AutoSize = true;
            this.lblStart.Location = new System.Drawing.Point(15, 18);
            this.lblStart.Name = "lblStart";
            this.lblStart.Text = "开始时间:";
            //
            // dtpStart - 开始时间选择器
            //
            this.dtpStart.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.dtpStart.Location = new System.Drawing.Point(80, 14);
            this.dtpStart.Name = "dtpStart";
            this.dtpStart.Size = new System.Drawing.Size(130, 21);
            //
            // lblEnd - 结束时间标签
            //
            this.lblEnd.AutoSize = true;
            this.lblEnd.Location = new System.Drawing.Point(225, 18);
            this.lblEnd.Name = "lblEnd";
            this.lblEnd.Text = "结束时间:";
            //
            // dtpEnd - 结束时间选择器
            //
            this.dtpEnd.Format = System.Windows.Forms.DateTimePickerFormat.Short;
            this.dtpEnd.Location = new System.Drawing.Point(290, 14);
            this.dtpEnd.Name = "dtpEnd";
            this.dtpEnd.Size = new System.Drawing.Size(130, 21);
            //
            // btnQuery - 查询按钮
            //
            this.btnQuery.Location = new System.Drawing.Point(440, 12);
            this.btnQuery.Name = "btnQuery";
            this.btnQuery.Size = new System.Drawing.Size(80, 28);
            this.btnQuery.Text = "查询";
            this.btnQuery.Click += new System.EventHandler(this.btnQuery_Click);
            //
            // btnExport - 导出按钮（预留功能）
            //
            this.btnExport.Location = new System.Drawing.Point(530, 12);
            this.btnExport.Name = "btnExport";
            this.btnExport.Size = new System.Drawing.Size(80, 28);
            this.btnExport.Text = "导出";
            this.btnExport.Click += new System.EventHandler(this.btnExport_Click);
            //
            // dgvHistory - 历史记录表格
            //
            this.dgvHistory.AllowUserToAddRows = false;
            this.dgvHistory.AllowUserToDeleteRows = false;
            this.dgvHistory.AllowUserToResizeRows = false;
            this.dgvHistory.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvHistory.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvHistory.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
                this.colTime,
                this.colDevice,
                this.colEvent,
                this.colDetail});
            this.dgvHistory.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvHistory.Location = new System.Drawing.Point(0, 50);
            this.dgvHistory.MultiSelect = false;
            this.dgvHistory.Name = "dgvHistory";
            this.dgvHistory.ReadOnly = true;
            this.dgvHistory.RowHeadersVisible = false;
            this.dgvHistory.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            //
            // colTime - 时间列
            //
            this.colTime.HeaderText = "时间";
            this.colTime.Name = "colTime";
            //
            // colDevice - 设备编号列
            //
            this.colDevice.HeaderText = "设备编号";
            this.colDevice.Name = "colDevice";
            //
            // colEvent - 事件列
            //
            this.colEvent.HeaderText = "事件";
            this.colEvent.Name = "colEvent";
            //
            // colDetail - 详情列
            //
            this.colDetail.HeaderText = "详情";
            this.colDetail.Name = "colDetail";
            //
            // panelBottom - 底部按钮面板
            //
            this.panelBottom.Controls.Add(this.btnClose);
            this.panelBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.panelBottom.Location = new System.Drawing.Point(0, 410);
            this.panelBottom.Name = "panelBottom";
            this.panelBottom.Size = new System.Drawing.Size(784, 40);
            //
            // btnClose - 关闭按钮
            //
            this.btnClose.Location = new System.Drawing.Point(684, 5);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(90, 30);
            this.btnClose.Text = "关闭";
            // 【V1.71】关闭按钮语义灰（自绘按钮走 Custom+FillColor）
            this.btnClose.FillColor = System.Drawing.Color.DimGray;
            this.btnClose.RectColor = System.Drawing.Color.DimGray;
            this.btnClose.ForeColor = System.Drawing.Color.White;
            this.btnClose.Style = Sunny.UI.UIStyle.Custom;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            //
            // HistoryRecordForm - 窗体自身
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 450);
            // 【V1.71】Dock 布局自适应放大；禁缩小（MinimumSize=ClientSize），防挤坏。
            this.MinimumSize = new System.Drawing.Size(784, 450);
            // 【V1.71】UIForm 自绘蓝标题：Dock 布局加顶 Pad 避开标题区。
            this.Padding = new System.Windows.Forms.Padding(2, 38, 2, 2);
            this.Controls.Add(this.dgvHistory);
            this.Controls.Add(this.panelTop);
            this.Controls.Add(this.panelBottom);
            this.Name = "HistoryRecordForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "历史记录";
            this.panelTop.ResumeLayout(false);
            this.panelTop.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvHistory)).EndInit();
            this.panelBottom.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Panel panelTop;
        private Sunny.UI.UILabel lblStart;
        private System.Windows.Forms.DateTimePicker dtpStart;
        private Sunny.UI.UILabel lblEnd;
        private System.Windows.Forms.DateTimePicker dtpEnd;
        private Sunny.UI.UIButton btnQuery;
        private Sunny.UI.UIButton btnExport;
        private Sunny.UI.UIDataGridView dgvHistory;
        private System.Windows.Forms.DataGridViewTextBoxColumn colTime;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDevice;
        private System.Windows.Forms.DataGridViewTextBoxColumn colEvent;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDetail;
        private System.Windows.Forms.Panel panelBottom;
        private Sunny.UI.UIButton btnClose;
    }
}
