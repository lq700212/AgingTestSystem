namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// ID绑定窗体 —— 设计器自动生成部分
    ///
    /// 【界面布局说明】
    /// 窗口样式参考用户提供的图片，包含左右两个区域：
    /// 左侧区域：输入区域，包含批号、工位编号、SN输入框
    /// 右侧区域：产品列表区域，显示已绑定的产品信息，底部有保存按钮
    ///
    /// 【控件布局】
    /// ┌─────────────────────────────────────────────────────────────┐
    /// │ ID绑定                                                     │ ← 标题栏
    /// ├─────────────────────────────────────────────────────────────┤
    /// │ TableLayoutPanel (2列)                                      │
    /// │ ┌─────────────────────────┐  ┌─────────────────────────┐   │
    /// │ │ PanelLeft (左侧输入区)  │  │ PanelRight (右侧列表区) │   │
    /// │ │ ┌─────────────────────┐ │  │ ┌─────────────────────┐ │   │
    /// │ │ │ lblLot              │ │  │ │ lblProductListTitle  │ │   │
    /// │ │ │ txtLot (只读)       │ │  │ ├─────────────────────┤ │   │
    /// │ │ ├─────────────────────┤ │  │ │ listBoxProducts      │ │   │
    /// │ │ │ lblStationNo        │ │  │ │ (带滚动条)           │ │   │
    /// │ │ │ txtStationNo        │ │  │ ├─────────────────────┤ │   │
    /// │ │ ├─────────────────────┤ │  │ │ btnSave              │ │   │
    /// │ │ │ lblSn               │ │  │ └─────────────────────┘ │   │
    /// │ │ │ txtSn               │ │  └─────────────────────────┘   │
    /// │ │ └─────────────────────┘ │                                │
    /// │ └─────────────────────────┘                                │
    /// └─────────────────────────────────────────────────────────────┘
    /// </summary>
    partial class IdBindingForm
    {
        /// <summary>必需的设计器变量</summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>清理所有正在使用的资源</summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false</param>
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
        /// 此方法负责创建所有控件并设置布局属性
        /// </summary>
        private void InitializeComponent()
        {
            this.tableLayoutPanelMain = new System.Windows.Forms.TableLayoutPanel();
            this.panelLeft = new System.Windows.Forms.Panel();
            this.txtSn = new System.Windows.Forms.TextBox();
            this.lblSn = new System.Windows.Forms.Label();
            this.txtStationNo = new System.Windows.Forms.TextBox();
            this.lblStationNo = new System.Windows.Forms.Label();
            this.txtLot = new System.Windows.Forms.TextBox();
            this.lblLot = new System.Windows.Forms.Label();
            this.panelRight = new System.Windows.Forms.Panel();
            this.btnSave = new System.Windows.Forms.Button();
            this.listBoxProducts = new System.Windows.Forms.ListBox();
            this.lblProductListTitle = new System.Windows.Forms.Label();
            this.tableLayoutPanelMain.SuspendLayout();
            this.panelLeft.SuspendLayout();
            this.panelRight.SuspendLayout();
            this.SuspendLayout();
            //
            // tableLayoutPanelMain - 主布局容器（2列：左侧输入区/右侧列表区）
            //
            this.tableLayoutPanelMain.ColumnCount = 2;
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 40F));
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 60F));
            this.tableLayoutPanelMain.Controls.Add(this.panelLeft, 0, 0);
            this.tableLayoutPanelMain.Controls.Add(this.panelRight, 1, 0);
            this.tableLayoutPanelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelMain.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanelMain.Name = "tableLayoutPanelMain";
            this.tableLayoutPanelMain.RowCount = 1;
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.Size = new System.Drawing.Size(750, 450);
            this.tableLayoutPanelMain.TabIndex = 0;
            //
            // panelLeft - 左侧输入区域面板
            //
            this.panelLeft.Controls.Add(this.txtSn);
            this.panelLeft.Controls.Add(this.lblSn);
            this.panelLeft.Controls.Add(this.txtStationNo);
            this.panelLeft.Controls.Add(this.lblStationNo);
            this.panelLeft.Controls.Add(this.txtLot);
            this.panelLeft.Controls.Add(this.lblLot);
            this.panelLeft.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelLeft.Location = new System.Drawing.Point(3, 3);
            this.panelLeft.Margin = new System.Windows.Forms.Padding(3, 3, 0, 3);
            this.panelLeft.Name = "panelLeft";
            this.panelLeft.Size = new System.Drawing.Size(294, 444);
            this.panelLeft.TabIndex = 0;
            //
            // txtSn - SN输入框
            //
            this.txtSn.Location = new System.Drawing.Point(85, 130);
            this.txtSn.Name = "txtSn";
            this.txtSn.Size = new System.Drawing.Size(190, 21);
            this.txtSn.TabIndex = 5;
            this.txtSn.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtSn_KeyDown);
            //
            // lblSn - SN标签
            //
            this.lblSn.AutoSize = true;
            this.lblSn.Location = new System.Drawing.Point(15, 133);
            this.lblSn.Name = "lblSn";
            this.lblSn.Size = new System.Drawing.Size(65, 12);
            this.lblSn.TabIndex = 4;
            this.lblSn.Text = "SN：";
            //
            // txtStationNo - 工位编号输入框
            //
            this.txtStationNo.Location = new System.Drawing.Point(85, 85);
            this.txtStationNo.Name = "txtStationNo";
            this.txtStationNo.Size = new System.Drawing.Size(190, 21);
            this.txtStationNo.TabIndex = 3;
            this.txtStationNo.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtStationNo_KeyDown);
            //
            // lblStationNo - 工位编号标签
            //
            this.lblStationNo.AutoSize = true;
            this.lblStationNo.Location = new System.Drawing.Point(15, 88);
            this.lblStationNo.Name = "lblStationNo";
            this.lblStationNo.Size = new System.Drawing.Size(65, 12);
            this.lblStationNo.TabIndex = 2;
            this.lblStationNo.Text = "工位编号：";
            //
            // txtLot - 批号输入框（只读）
            //
            this.txtLot.Location = new System.Drawing.Point(85, 40);
            this.txtLot.Name = "txtLot";
            this.txtLot.ReadOnly = true;
            this.txtLot.Size = new System.Drawing.Size(190, 21);
            this.txtLot.TabIndex = 1;
            //
            // lblLot - 批号标签
            //
            this.lblLot.AutoSize = true;
            this.lblLot.Location = new System.Drawing.Point(15, 43);
            this.lblLot.Name = "lblLot";
            this.lblLot.Size = new System.Drawing.Size(41, 12);
            this.lblLot.TabIndex = 0;
            this.lblLot.Text = "批号：";
            //
            // panelRight - 右侧产品列表区域面板
            //
            this.panelRight.Controls.Add(this.btnSave);
            this.panelRight.Controls.Add(this.listBoxProducts);
            this.panelRight.Controls.Add(this.lblProductListTitle);
            this.panelRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelRight.Location = new System.Drawing.Point(303, 3);
            this.panelRight.Margin = new System.Windows.Forms.Padding(6, 3, 3, 3);
            this.panelRight.Name = "panelRight";
            this.panelRight.Size = new System.Drawing.Size(444, 444);
            this.panelRight.TabIndex = 1;
            //
            // btnSave - 保存按钮
            //
            this.btnSave.Location = new System.Drawing.Point(330, 395);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(100, 40);
            this.btnSave.TabIndex = 2;
            this.btnSave.Text = "保存";
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            //
            // listBoxProducts - 产品列表框（带滚动条）
            //
            this.listBoxProducts.FormattingEnabled = true;
            this.listBoxProducts.ItemHeight = 12;
            this.listBoxProducts.Location = new System.Drawing.Point(15, 40);
            this.listBoxProducts.Name = "listBoxProducts";
            this.listBoxProducts.Size = new System.Drawing.Size(415, 340);
            this.listBoxProducts.TabIndex = 1;
            //
            // lblProductListTitle - 产品列表标题标签
            //
            this.lblProductListTitle.AutoSize = true;
            this.lblProductListTitle.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblProductListTitle.Location = new System.Drawing.Point(15, 15);
            this.lblProductListTitle.Name = "lblProductListTitle";
            this.lblProductListTitle.Size = new System.Drawing.Size(72, 19);
            this.lblProductListTitle.TabIndex = 0;
            this.lblProductListTitle.Text = "产品列表";
            //
            // IdBindingForm - 窗体自身属性设置
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(750, 450);
            this.Controls.Add(this.tableLayoutPanelMain);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "IdBindingForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "ID绑定";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.IdBindingForm_FormClosing);
            this.tableLayoutPanelMain.ResumeLayout(false);
            this.panelLeft.ResumeLayout(false);
            this.panelLeft.PerformLayout();
            this.panelRight.ResumeLayout(false);
            this.panelRight.PerformLayout();
            this.ResumeLayout(false);
        }

        #endregion

        // 控件字段声明区域
        // 这些字段在两个 partial 文件中共享（本文件赋值，.cs文件使用）

        /// <summary>主布局容器（2列：左侧输入区/右侧列表区）</summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelMain;

        /// <summary>左侧输入区域面板</summary>
        private System.Windows.Forms.Panel panelLeft;

        /// <summary>右侧产品列表区域面板</summary>
        private System.Windows.Forms.Panel panelRight;

        /// <summary>批号标签</summary>
        private System.Windows.Forms.Label lblLot;

        /// <summary>批号输入框（只读）</summary>
        private System.Windows.Forms.TextBox txtLot;

        /// <summary>工位编号标签</summary>
        private System.Windows.Forms.Label lblStationNo;

        /// <summary>工位编号输入框</summary>
        private System.Windows.Forms.TextBox txtStationNo;

        /// <summary>SN标签</summary>
        private System.Windows.Forms.Label lblSn;

        /// <summary>SN输入框</summary>
        private System.Windows.Forms.TextBox txtSn;

        /// <summary>产品列表标题标签</summary>
        private System.Windows.Forms.Label lblProductListTitle;

        /// <summary>产品列表框（带滚动条）</summary>
        private System.Windows.Forms.ListBox listBoxProducts;

        /// <summary>保存按钮</summary>
        private System.Windows.Forms.Button btnSave;
    }
}