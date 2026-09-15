namespace AgingTestSystem.Views
{
    /// <summary>
    /// 主窗体 —— 设计器自动生成部分
    /// 【说明】
    /// 本文件由 Visual Studio 设计器维护，包含所有控件的创建和布局代码。
    /// 请勿手动修改此文件内容，所有修改请通过设计器界面操作。
    /// 业务逻辑代码请放在 MainForm.cs 文件中。
    /// 为什么需要单独的 Designer.cs 文件？
    /// WinForms 设计器依赖"partial class"分部类机制，将界面布局代码（本文件）
    /// 与业务逻辑代码（.cs 文件）分离。设计器只解析 Designer.cs 文件中的
    /// InitializeComponent 方法来渲染设计视图。如果不拆分，设计器会因无法
    /// 正确识别类结构而报错（如"未能加载基类 System.Windows.Forms.Form"）。
    /// </summary>
    partial class MainForm
    {
        /// <summary>
        /// 必需的设计器变量
        /// 用于管理设计器创建的组件资源（如 Timer）
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源
        /// 在窗体被销毁时调用，释放组件资源
        /// </summary>
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
            this.components = new System.ComponentModel.Container();
            this.rootScrollPanel = new System.Windows.Forms.Panel();
            this.tableLayoutPanelMain = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanelHeader = new System.Windows.Forms.TableLayoutPanel();
            this.pnlProject = new System.Windows.Forms.Panel();
            this.lblProject = new Sunny.UI.UILabel();
            this.lblProjectPrefix = new Sunny.UI.UILabel();
            this.panelPermission = new System.Windows.Forms.Panel();
            this.lblPermissionPrefix = new Sunny.UI.UILabel();
            this.lblPermissionRole = new Sunny.UI.UILabel();
            this.lblCommStatusLabel = new Sunny.UI.UILabel();
            this.lblCommStatus = new Sunny.UI.UILabel();
            this.btnUserPermission = new Sunny.UI.UIButton();
            this.btnParameter = new Sunny.UI.UIButton();
            this.btnLog = new Sunny.UI.UIButton();
            this.btnAbout = new Sunny.UI.UIButton();
            this.btnWinMin = new System.Windows.Forms.Button();
            this.btnWinMax = new System.Windows.Forms.Button();
            this.btnWinClose = new System.Windows.Forms.Button();
            this.splitContainerMain = new System.Windows.Forms.SplitContainer();
            this.tableLayoutPanelRight = new System.Windows.Forms.TableLayoutPanel();
            this.groupBoxStatus = new Sunny.UI.UIGroupBox();
            this.lblRunStatus = new Sunny.UI.UILabel();
            this.groupBoxMonitor = new Sunny.UI.UIGroupBox();
            this.lblUpperTempLabel = new Sunny.UI.UILabel();
            this.lblUpperTemp = new Sunny.UI.UILabel();
            this.lblSetTempLabel = new Sunny.UI.UILabel();
            this.lblSetTemp = new Sunny.UI.UILabel();
            this.lblFanStateLabel = new Sunny.UI.UILabel();
            this.lblFanState = new Sunny.UI.UILabel();
            this.groupBoxOperation = new Sunny.UI.UIGroupBox();
            this.btnStartRun = new Sunny.UI.UIButton();
            this.btnStopRun = new Sunny.UI.UIButton();
            this.btnResetAlarm = new Sunny.UI.UIButton();
            this.btnUnloadJudge = new Sunny.UI.UIButton();
            this.btnStopAll = new Sunny.UI.UIButton();
            this.btnInputLot = new Sunny.UI.UIButton();
            this.btnBatchRecipe = new Sunny.UI.UIButton();
            this.groupBoxLog = new Sunny.UI.UIGroupBox();
            this.txtLog = new System.Windows.Forms.TextBox();
            this.statusStripMain = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabelDeviceCount = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabelInterval = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabelTesting = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabelOnline = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabelScanner = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabelTime = new System.Windows.Forms.ToolStripStatusLabel();
            this.timerTime = new System.Windows.Forms.Timer(this.components);
            this.hashTimer = new System.Windows.Forms.Timer(this.components);
            this.rootScrollPanel.SuspendLayout();
            this.tableLayoutPanelMain.SuspendLayout();
            this.tableLayoutPanelHeader.SuspendLayout();
            this.pnlProject.SuspendLayout();
            this.panelPermission.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).BeginInit();
            this.splitContainerMain.Panel2.SuspendLayout();
            this.splitContainerMain.SuspendLayout();
            this.tableLayoutPanelRight.SuspendLayout();
            this.groupBoxStatus.SuspendLayout();
            this.groupBoxMonitor.SuspendLayout();
            this.groupBoxOperation.SuspendLayout();
            this.groupBoxLog.SuspendLayout();
            this.statusStripMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // rootScrollPanel
            // 
            this.rootScrollPanel.AutoScroll = true;
            this.rootScrollPanel.AutoScrollMinSize = new System.Drawing.Size(1150, 800);
            this.rootScrollPanel.Controls.Add(this.tableLayoutPanelMain);
            this.rootScrollPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rootScrollPanel.Location = new System.Drawing.Point(0, 38);
            this.rootScrollPanel.Name = "rootScrollPanel";
            this.rootScrollPanel.Size = new System.Drawing.Size(1280, 862);
            this.rootScrollPanel.TabIndex = 0;
            // 
            // tableLayoutPanelMain
            // 
            this.tableLayoutPanelMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanelMain.ColumnCount = 1;
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.Controls.Add(this.tableLayoutPanelHeader, 0, 0);
            this.tableLayoutPanelMain.Controls.Add(this.splitContainerMain, 0, 1);
            this.tableLayoutPanelMain.Controls.Add(this.statusStripMain, 0, 2);
            this.tableLayoutPanelMain.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanelMain.MinimumSize = new System.Drawing.Size(1150, 800);
            this.tableLayoutPanelMain.Name = "tableLayoutPanelMain";
            this.tableLayoutPanelMain.RowCount = 3;
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanelMain.Size = new System.Drawing.Size(1280, 862);
            this.tableLayoutPanelMain.TabIndex = 0;
            // 
            // tableLayoutPanelHeader
            // 
            this.tableLayoutPanelHeader.ColumnCount = 12;
            this.tableLayoutPanelHeader.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 180F));
            this.tableLayoutPanelHeader.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 180F));
            this.tableLayoutPanelHeader.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 110F));
            this.tableLayoutPanelHeader.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this.tableLayoutPanelHeader.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelHeader.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.tableLayoutPanelHeader.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.tableLayoutPanelHeader.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.tableLayoutPanelHeader.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.tableLayoutPanelHeader.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanelHeader.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanelHeader.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanelHeader.Controls.Add(this.pnlProject, 0, 0);
            this.tableLayoutPanelHeader.Controls.Add(this.panelPermission, 1, 0);
            this.tableLayoutPanelHeader.Controls.Add(this.lblCommStatusLabel, 2, 0);
            this.tableLayoutPanelHeader.Controls.Add(this.lblCommStatus, 3, 0);
            this.tableLayoutPanelHeader.Controls.Add(this.btnUserPermission, 5, 0);
            this.tableLayoutPanelHeader.Controls.Add(this.btnParameter, 6, 0);
            this.tableLayoutPanelHeader.Controls.Add(this.btnLog, 7, 0);
            this.tableLayoutPanelHeader.Controls.Add(this.btnAbout, 8, 0);
            this.tableLayoutPanelHeader.Controls.Add(this.btnWinMin, 9, 0);
            this.tableLayoutPanelHeader.Controls.Add(this.btnWinMax, 10, 0);
            this.tableLayoutPanelHeader.Controls.Add(this.btnWinClose, 11, 0);
            this.tableLayoutPanelHeader.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelHeader.Location = new System.Drawing.Point(3, 3);
            this.tableLayoutPanelHeader.Name = "tableLayoutPanelHeader";
            this.tableLayoutPanelHeader.RowCount = 1;
            this.tableLayoutPanelHeader.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelHeader.Size = new System.Drawing.Size(1274, 30);
            this.tableLayoutPanelHeader.TabIndex = 0;
            // 
            // pnlProject
            // 
            this.pnlProject.BackColor = System.Drawing.SystemColors.Control;
            this.pnlProject.Controls.Add(this.lblProject);
            this.pnlProject.Controls.Add(this.lblProjectPrefix);
            this.pnlProject.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlProject.Location = new System.Drawing.Point(3, 0);
            this.pnlProject.Margin = new System.Windows.Forms.Padding(3, 0, 12, 0);
            this.pnlProject.Name = "pnlProject";
            this.pnlProject.Size = new System.Drawing.Size(148, 30);
            this.pnlProject.TabIndex = 4;
            // 
            // lblProject
            // 
            this.lblProject.AutoEllipsis = true;
            this.lblProject.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblProject.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblProject.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblProject.Location = new System.Drawing.Point(77, 0);
            this.lblProject.Margin = new System.Windows.Forms.Padding(0);
            this.lblProject.Name = "lblProject";
            this.lblProject.Size = new System.Drawing.Size(71, 16);
            this.lblProject.TabIndex = 1;
            this.lblProject.Text = "烧屏测试";
            this.lblProject.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblProjectPrefix
            // 
            this.lblProjectPrefix.Dock = System.Windows.Forms.DockStyle.Left;
            this.lblProjectPrefix.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblProjectPrefix.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblProjectPrefix.Location = new System.Drawing.Point(0, 0);
            this.lblProjectPrefix.Margin = new System.Windows.Forms.Padding(0);
            this.lblProjectPrefix.Name = "lblProjectPrefix";
            this.lblProjectPrefix.Size = new System.Drawing.Size(77, 30);
            this.lblProjectPrefix.TabIndex = 0;
            this.lblProjectPrefix.Text = "当前项目：";
            this.lblProjectPrefix.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // panelPermission
            // 
            this.panelPermission.BackColor = System.Drawing.SystemColors.Control;
            this.panelPermission.Controls.Add(this.lblPermissionRole);
            this.panelPermission.Controls.Add(this.lblPermissionPrefix);
            this.panelPermission.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelPermission.Location = new System.Drawing.Point(163, 0);
            this.panelPermission.Margin = new System.Windows.Forms.Padding(0, 0, 12, 0);
            this.panelPermission.Name = "panelPermission";
            this.panelPermission.Size = new System.Drawing.Size(174, 30);
            this.panelPermission.TabIndex = 1;
            // 
            // lblPermissionPrefix
            // 
            this.lblPermissionPrefix.AutoSize = false;
            this.lblPermissionPrefix.Dock = System.Windows.Forms.DockStyle.Left;
            this.lblPermissionPrefix.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblPermissionPrefix.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblPermissionPrefix.Location = new System.Drawing.Point(0, 0);
            this.lblPermissionPrefix.Margin = new System.Windows.Forms.Padding(0, 0, 0, 0);
            this.lblPermissionPrefix.Name = "lblPermissionPrefix";
            this.lblPermissionPrefix.Size = new System.Drawing.Size(119, 30);
            this.lblPermissionPrefix.TabIndex = 0;
            this.lblPermissionPrefix.Text = "当前操作权限: ";
            this.lblPermissionPrefix.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblPermissionRole
            // 
            this.lblPermissionRole.AutoEllipsis = true;
            this.lblPermissionRole.AutoSize = false;
            this.lblPermissionRole.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblPermissionRole.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblPermissionRole.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblPermissionRole.Location = new System.Drawing.Point(119, 0);
            this.lblPermissionRole.Margin = new System.Windows.Forms.Padding(0, 0, 0, 0);
            this.lblPermissionRole.Name = "lblPermissionRole";
            this.lblPermissionRole.Size = new System.Drawing.Size(55, 30);
            this.lblPermissionRole.TabIndex = 1;
            this.lblPermissionRole.Text = "操作员";
            this.lblPermissionRole.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblCommStatusLabel
            // 
            this.lblCommStatusLabel.AutoSize = false;
            this.lblCommStatusLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblCommStatusLabel.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblCommStatusLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblCommStatusLabel.Location = new System.Drawing.Point(349, 0);
            this.lblCommStatusLabel.Margin = new System.Windows.Forms.Padding(0, 0, 12, 0);
            this.lblCommStatusLabel.Name = "lblCommStatusLabel";
            this.lblCommStatusLabel.Size = new System.Drawing.Size(111, 30);
            this.lblCommStatusLabel.TabIndex = 2;
            this.lblCommStatusLabel.Text = "通讯模块状态:";
            this.lblCommStatusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblCommStatus
            // 
            this.lblCommStatus.AutoSize = false;
            this.lblCommStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblCommStatus.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblCommStatus.ForeColor = System.Drawing.Color.Red;
            this.lblCommStatus.Location = new System.Drawing.Point(475, 0);
            this.lblCommStatus.Name = "lblCommStatus";
            this.lblCommStatus.Size = new System.Drawing.Size(55, 30);
            this.lblCommStatus.TabIndex = 3;
            this.lblCommStatus.Text = "未连接";
            this.lblCommStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // btnUserPermission
            // 
            this.btnUserPermission.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnUserPermission.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnUserPermission.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnUserPermission.Location = new System.Drawing.Point(797, 3);
            this.btnUserPermission.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnUserPermission.Name = "btnUserPermission";
            this.btnUserPermission.Size = new System.Drawing.Size(114, 24);
            this.btnUserPermission.TabIndex = 0;
            this.btnUserPermission.Text = "用户权限";
            this.btnUserPermission.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnUserPermission.Click += new System.EventHandler(this.btnUserPermission_Click);
            // 
            // btnParameter
            // 
            this.btnParameter.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnParameter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnParameter.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnParameter.Location = new System.Drawing.Point(917, 3);
            this.btnParameter.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnParameter.Name = "btnParameter";
            this.btnParameter.Size = new System.Drawing.Size(114, 24);
            this.btnParameter.TabIndex = 1;
            this.btnParameter.Text = "参数设置";
            this.btnParameter.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnParameter.Click += new System.EventHandler(this.btnParameter_Click);
            // 
            // btnLog
            // 
            this.btnLog.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnLog.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnLog.Location = new System.Drawing.Point(1037, 3);
            this.btnLog.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnLog.Name = "btnLog";
            this.btnLog.Size = new System.Drawing.Size(114, 24);
            this.btnLog.TabIndex = 2;
            this.btnLog.Text = "日志记录";
            this.btnLog.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnLog.Click += new System.EventHandler(this.btnLog_Click);
            // 
            // btnAbout
            // 
            this.btnAbout.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnAbout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnAbout.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnAbout.Location = new System.Drawing.Point(1157, 3);
            this.btnAbout.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnAbout.Name = "btnAbout";
            this.btnAbout.Size = new System.Drawing.Size(114, 24);
            this.btnAbout.TabIndex = 4;
            this.btnAbout.Text = "关于";
            this.btnAbout.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnAbout.Click += new System.EventHandler(this.btnAbout_Click);
            //
            // btnWinMin（最小化：自绘横线，见 MainForm.InitWindowChrome）
            //
            this.btnWinMin.BackColor = System.Drawing.SystemColors.Control;
            this.btnWinMin.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnWinMin.FlatAppearance.BorderSize = 0;
            this.btnWinMin.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnWinMin.Location = new System.Drawing.Point(1157, 0);
            this.btnWinMin.Margin = new System.Windows.Forms.Padding(0);
            this.btnWinMin.Name = "btnWinMin";
            this.btnWinMin.Size = new System.Drawing.Size(40, 30);
            this.btnWinMin.TabIndex = 5;
            this.btnWinMin.TabStop = false;
            this.btnWinMin.UseVisualStyleBackColor = false;
            //
            // btnWinMax（最大化/还原：自绘方框，见 MainForm.InitWindowChrome）
            //
            this.btnWinMax.BackColor = System.Drawing.SystemColors.Control;
            this.btnWinMax.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnWinMax.FlatAppearance.BorderSize = 0;
            this.btnWinMax.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnWinMax.Location = new System.Drawing.Point(1197, 0);
            this.btnWinMax.Margin = new System.Windows.Forms.Padding(0);
            this.btnWinMax.Name = "btnWinMax";
            this.btnWinMax.Size = new System.Drawing.Size(40, 30);
            this.btnWinMax.TabIndex = 6;
            this.btnWinMax.TabStop = false;
            this.btnWinMax.UseVisualStyleBackColor = false;
            //
            // btnWinClose（关闭：自绘叉，悬停红底，见 MainForm.InitWindowChrome）
            //
            this.btnWinClose.BackColor = System.Drawing.SystemColors.Control;
            this.btnWinClose.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnWinClose.FlatAppearance.BorderSize = 0;
            this.btnWinClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnWinClose.Location = new System.Drawing.Point(1237, 0);
            this.btnWinClose.Margin = new System.Windows.Forms.Padding(0);
            this.btnWinClose.Name = "btnWinClose";
            this.btnWinClose.Size = new System.Drawing.Size(40, 30);
            this.btnWinClose.TabIndex = 7;
            this.btnWinClose.TabStop = false;
            this.btnWinClose.UseVisualStyleBackColor = false;
            //
            // splitContainerMain
            // 
            this.splitContainerMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerMain.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainerMain.Location = new System.Drawing.Point(3, 39);
            this.splitContainerMain.Name = "splitContainerMain";
            // 
            // splitContainerMain.Panel2
            // 
            this.splitContainerMain.Panel2.Controls.Add(this.tableLayoutPanelRight);
            this.splitContainerMain.Size = new System.Drawing.Size(1274, 795);
            this.splitContainerMain.SplitterDistance = 972;
            this.splitContainerMain.TabIndex = 2;
            // 
            // tableLayoutPanelRight
            // 
            this.tableLayoutPanelRight.ColumnCount = 1;
            this.tableLayoutPanelRight.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelRight.Controls.Add(this.groupBoxStatus, 0, 0);
            this.tableLayoutPanelRight.Controls.Add(this.groupBoxMonitor, 0, 1);
            this.tableLayoutPanelRight.Controls.Add(this.groupBoxOperation, 0, 2);
            this.tableLayoutPanelRight.Controls.Add(this.groupBoxLog, 0, 3);
            this.tableLayoutPanelRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelRight.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanelRight.Name = "tableLayoutPanelRight";
            this.tableLayoutPanelRight.RowCount = 4;
            this.tableLayoutPanelRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 90F));
            this.tableLayoutPanelRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 120F));
            this.tableLayoutPanelRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 300F));
            this.tableLayoutPanelRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelRight.Size = new System.Drawing.Size(298, 795);
            this.tableLayoutPanelRight.TabIndex = 0;
            // 
            // groupBoxStatus
            // 
            this.groupBoxStatus.Controls.Add(this.lblRunStatus);
            this.groupBoxStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxStatus.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.groupBoxStatus.Location = new System.Drawing.Point(4, 5);
            this.groupBoxStatus.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.groupBoxStatus.MinimumSize = new System.Drawing.Size(1, 1);
            this.groupBoxStatus.Name = "groupBoxStatus";
            this.groupBoxStatus.Padding = new System.Windows.Forms.Padding(0, 32, 0, 0);
            this.groupBoxStatus.Size = new System.Drawing.Size(290, 80);
            this.groupBoxStatus.TabIndex = 0;
            this.groupBoxStatus.TabStop = false;
            this.groupBoxStatus.Text = "运行状态";
            this.groupBoxStatus.TextAlignment = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblRunStatus
            // 
            this.lblRunStatus.AutoSize = true;
            this.lblRunStatus.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblRunStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblRunStatus.Location = new System.Drawing.Point(15, 44);
            this.lblRunStatus.Name = "lblRunStatus";
            this.lblRunStatus.Size = new System.Drawing.Size(39, 16);
            this.lblRunStatus.TabIndex = 0;
            this.lblRunStatus.Text = "空闲";
            // 
            // groupBoxMonitor
            // 
            this.groupBoxMonitor.Controls.Add(this.lblUpperTempLabel);
            this.groupBoxMonitor.Controls.Add(this.lblUpperTemp);
            this.groupBoxMonitor.Controls.Add(this.lblSetTempLabel);
            this.groupBoxMonitor.Controls.Add(this.lblSetTemp);
            this.groupBoxMonitor.Controls.Add(this.lblFanStateLabel);
            this.groupBoxMonitor.Controls.Add(this.lblFanState);
            this.groupBoxMonitor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxMonitor.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.groupBoxMonitor.Location = new System.Drawing.Point(4, 95);
            this.groupBoxMonitor.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.groupBoxMonitor.MinimumSize = new System.Drawing.Size(1, 1);
            this.groupBoxMonitor.Name = "groupBoxMonitor";
            this.groupBoxMonitor.Padding = new System.Windows.Forms.Padding(0, 32, 0, 0);
            this.groupBoxMonitor.Size = new System.Drawing.Size(290, 110);
            this.groupBoxMonitor.TabIndex = 1;
            this.groupBoxMonitor.TabStop = false;
            this.groupBoxMonitor.Text = "监视";
            this.groupBoxMonitor.TextAlignment = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblUpperTempLabel
            // 
            this.lblUpperTempLabel.AutoSize = true;
            this.lblUpperTempLabel.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblUpperTempLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblUpperTempLabel.Location = new System.Drawing.Point(15, 86);
            this.lblUpperTempLabel.Name = "lblUpperTempLabel";
            this.lblUpperTempLabel.Size = new System.Drawing.Size(71, 16);
            this.lblUpperTempLabel.TabIndex = 3;
            this.lblUpperTempLabel.Text = "当前温度";
            // 
            // lblUpperTemp
            // 
            this.lblUpperTemp.AutoSize = true;
            this.lblUpperTemp.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblUpperTemp.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblUpperTemp.Location = new System.Drawing.Point(100, 86);
            this.lblUpperTemp.Name = "lblUpperTemp";
            this.lblUpperTemp.Size = new System.Drawing.Size(31, 16);
            this.lblUpperTemp.TabIndex = 2;
            this.lblUpperTemp.Text = "---";
            // 
            // lblSetTempLabel
            // 
            this.lblSetTempLabel.AutoSize = true;
            this.lblSetTempLabel.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblSetTempLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblSetTempLabel.Location = new System.Drawing.Point(15, 60);
            this.lblSetTempLabel.Name = "lblSetTempLabel";
            this.lblSetTempLabel.Size = new System.Drawing.Size(71, 16);
            this.lblSetTempLabel.TabIndex = 1;
            this.lblSetTempLabel.Text = "设置温度";
            // 
            // lblSetTemp
            // 
            this.lblSetTemp.AutoSize = true;
            this.lblSetTemp.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblSetTemp.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblSetTemp.Location = new System.Drawing.Point(100, 60);
            this.lblSetTemp.Name = "lblSetTemp";
            this.lblSetTemp.Size = new System.Drawing.Size(31, 16);
            this.lblSetTemp.TabIndex = 0;
            this.lblSetTemp.Text = "---";
            // 
            // lblFanStateLabel
            // 
            this.lblFanStateLabel.AutoSize = true;
            this.lblFanStateLabel.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblFanStateLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblFanStateLabel.Location = new System.Drawing.Point(15, 34);
            this.lblFanStateLabel.Name = "lblFanStateLabel";
            this.lblFanStateLabel.Size = new System.Drawing.Size(87, 16);
            this.lblFanStateLabel.TabIndex = 6;
            this.lblFanStateLabel.Text = "送风机状态";
            // 
            // lblFanState
            // 
            this.lblFanState.AutoSize = true;
            this.lblFanState.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblFanState.ForeColor = System.Drawing.Color.Red;
            this.lblFanState.Location = new System.Drawing.Point(112, 34);
            this.lblFanState.Name = "lblFanState";
            this.lblFanState.Size = new System.Drawing.Size(55, 16);
            this.lblFanState.TabIndex = 7;
            this.lblFanState.Text = "未连接";
            // 
            // groupBoxOperation
            // 
            this.groupBoxOperation.Controls.Add(this.btnStartRun);
            this.groupBoxOperation.Controls.Add(this.btnStopRun);
            this.groupBoxOperation.Controls.Add(this.btnResetAlarm);
            this.groupBoxOperation.Controls.Add(this.btnUnloadJudge);
            this.groupBoxOperation.Controls.Add(this.btnStopAll);
            this.groupBoxOperation.Controls.Add(this.btnInputLot);
            this.groupBoxOperation.Controls.Add(this.btnBatchRecipe);
            this.groupBoxOperation.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxOperation.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.groupBoxOperation.Location = new System.Drawing.Point(4, 215);
            this.groupBoxOperation.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.groupBoxOperation.MinimumSize = new System.Drawing.Size(1, 1);
            this.groupBoxOperation.Name = "groupBoxOperation";
            this.groupBoxOperation.Padding = new System.Windows.Forms.Padding(0, 32, 0, 0);
            this.groupBoxOperation.Size = new System.Drawing.Size(290, 290);
            this.groupBoxOperation.TabIndex = 2;
            this.groupBoxOperation.TabStop = false;
            this.groupBoxOperation.Text = "操作";
            this.groupBoxOperation.TextAlignment = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // btnStartRun
            // 
            this.btnStartRun.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnStartRun.FillColor = System.Drawing.Color.DodgerBlue;
            this.btnStartRun.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnStartRun.Location = new System.Drawing.Point(15, 92);
            this.btnStartRun.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnStartRun.Name = "btnStartRun";
            this.btnStartRun.RectColor = System.Drawing.Color.DodgerBlue;
            this.btnStartRun.Size = new System.Drawing.Size(256, 28);
            this.btnStartRun.Style = Sunny.UI.UIStyle.Custom;
            this.btnStartRun.TabIndex = 2;
            this.btnStartRun.Text = "启动运行（选中台）";
            this.btnStartRun.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnStartRun.Click += new System.EventHandler(this.btnStartRun_Click);
            // 
            // btnStopRun
            // 
            this.btnStopRun.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnStopRun.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(140)))), ((int)(((byte)(140)))), ((int)(((byte)(140)))));
            this.btnStopRun.FillColor2 = System.Drawing.Color.FromArgb(((int)(((byte)(140)))), ((int)(((byte)(140)))), ((int)(((byte)(140)))));
            this.btnStopRun.FillHoverColor = System.Drawing.Color.FromArgb(((int)(((byte)(163)))), ((int)(((byte)(163)))), ((int)(((byte)(163)))));
            this.btnStopRun.FillPressColor = System.Drawing.Color.FromArgb(((int)(((byte)(112)))), ((int)(((byte)(112)))), ((int)(((byte)(112)))));
            this.btnStopRun.FillSelectedColor = System.Drawing.Color.FromArgb(((int)(((byte)(112)))), ((int)(((byte)(112)))), ((int)(((byte)(112)))));
            this.btnStopRun.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnStopRun.LightColor = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(248)))), ((int)(((byte)(248)))));
            this.btnStopRun.Location = new System.Drawing.Point(15, 121);
            this.btnStopRun.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnStopRun.Name = "btnStopRun";
            this.btnStopRun.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)(140)))), ((int)(((byte)(140)))), ((int)(((byte)(140)))));
            this.btnStopRun.RectHoverColor = System.Drawing.Color.FromArgb(((int)(((byte)(163)))), ((int)(((byte)(163)))), ((int)(((byte)(163)))));
            this.btnStopRun.RectPressColor = System.Drawing.Color.FromArgb(((int)(((byte)(112)))), ((int)(((byte)(112)))), ((int)(((byte)(112)))));
            this.btnStopRun.RectSelectedColor = System.Drawing.Color.FromArgb(((int)(((byte)(112)))), ((int)(((byte)(112)))), ((int)(((byte)(112)))));
            this.btnStopRun.Size = new System.Drawing.Size(256, 28);
            this.btnStopRun.Style = Sunny.UI.UIStyle.Custom;
            this.btnStopRun.TabIndex = 3;
            this.btnStopRun.Text = "停止运行（选中台）";
            this.btnStopRun.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnStopRun.Click += new System.EventHandler(this.btnStopRun_Click);
            // 
            // btnResetAlarm
            // 
            this.btnResetAlarm.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnResetAlarm.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(140)))), ((int)(((byte)(140)))), ((int)(((byte)(140)))));
            this.btnResetAlarm.FillColor2 = System.Drawing.Color.FromArgb(((int)(((byte)(140)))), ((int)(((byte)(140)))), ((int)(((byte)(140)))));
            this.btnResetAlarm.FillHoverColor = System.Drawing.Color.FromArgb(((int)(((byte)(163)))), ((int)(((byte)(163)))), ((int)(((byte)(163)))));
            this.btnResetAlarm.FillPressColor = System.Drawing.Color.FromArgb(((int)(((byte)(112)))), ((int)(((byte)(112)))), ((int)(((byte)(112)))));
            this.btnResetAlarm.FillSelectedColor = System.Drawing.Color.FromArgb(((int)(((byte)(112)))), ((int)(((byte)(112)))), ((int)(((byte)(112)))));
            this.btnResetAlarm.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnResetAlarm.LightColor = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(248)))), ((int)(((byte)(248)))));
            this.btnResetAlarm.Location = new System.Drawing.Point(15, 150);
            this.btnResetAlarm.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnResetAlarm.Name = "btnResetAlarm";
            this.btnResetAlarm.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)(140)))), ((int)(((byte)(140)))), ((int)(((byte)(140)))));
            this.btnResetAlarm.RectHoverColor = System.Drawing.Color.FromArgb(((int)(((byte)(163)))), ((int)(((byte)(163)))), ((int)(((byte)(163)))));
            this.btnResetAlarm.RectPressColor = System.Drawing.Color.FromArgb(((int)(((byte)(112)))), ((int)(((byte)(112)))), ((int)(((byte)(112)))));
            this.btnResetAlarm.RectSelectedColor = System.Drawing.Color.FromArgb(((int)(((byte)(112)))), ((int)(((byte)(112)))), ((int)(((byte)(112)))));
            this.btnResetAlarm.Size = new System.Drawing.Size(256, 28);
            this.btnResetAlarm.Style = Sunny.UI.UIStyle.Custom;
            this.btnResetAlarm.TabIndex = 4;
            this.btnResetAlarm.Text = "报警复位（选中台）";
            this.btnResetAlarm.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnResetAlarm.Click += new System.EventHandler(this.btnResetAlarm_Click);
            // 
            // btnUnloadJudge
            // 
            this.btnUnloadJudge.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnUnloadJudge.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(140)))), ((int)(((byte)(140)))), ((int)(((byte)(140)))));
            this.btnUnloadJudge.FillColor2 = System.Drawing.Color.FromArgb(((int)(((byte)(140)))), ((int)(((byte)(140)))), ((int)(((byte)(140)))));
            this.btnUnloadJudge.FillHoverColor = System.Drawing.Color.FromArgb(((int)(((byte)(163)))), ((int)(((byte)(163)))), ((int)(((byte)(163)))));
            this.btnUnloadJudge.FillPressColor = System.Drawing.Color.FromArgb(((int)(((byte)(112)))), ((int)(((byte)(112)))), ((int)(((byte)(112)))));
            this.btnUnloadJudge.FillSelectedColor = System.Drawing.Color.FromArgb(((int)(((byte)(112)))), ((int)(((byte)(112)))), ((int)(((byte)(112)))));
            this.btnUnloadJudge.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnUnloadJudge.LightColor = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(248)))), ((int)(((byte)(248)))));
            this.btnUnloadJudge.Location = new System.Drawing.Point(15, 208);
            this.btnUnloadJudge.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnUnloadJudge.Name = "btnUnloadJudge";
            this.btnUnloadJudge.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)(140)))), ((int)(((byte)(140)))), ((int)(((byte)(140)))));
            this.btnUnloadJudge.RectHoverColor = System.Drawing.Color.FromArgb(((int)(((byte)(163)))), ((int)(((byte)(163)))), ((int)(((byte)(163)))));
            this.btnUnloadJudge.RectPressColor = System.Drawing.Color.FromArgb(((int)(((byte)(112)))), ((int)(((byte)(112)))), ((int)(((byte)(112)))));
            this.btnUnloadJudge.RectSelectedColor = System.Drawing.Color.FromArgb(((int)(((byte)(112)))), ((int)(((byte)(112)))), ((int)(((byte)(112)))));
            this.btnUnloadJudge.Size = new System.Drawing.Size(256, 28);
            this.btnUnloadJudge.Style = Sunny.UI.UIStyle.Custom;
            this.btnUnloadJudge.TabIndex = 6;
            this.btnUnloadJudge.Text = "下料判定（选中台）";
            this.btnUnloadJudge.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnUnloadJudge.Click += new System.EventHandler(this.btnUnloadJudge_Click);
            // 
            // btnStopAll
            // 
            this.btnStopAll.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnStopAll.FillColor = System.Drawing.Color.Crimson;
            this.btnStopAll.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnStopAll.Location = new System.Drawing.Point(15, 237);
            this.btnStopAll.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnStopAll.Name = "btnStopAll";
            this.btnStopAll.RectColor = System.Drawing.Color.Crimson;
            this.btnStopAll.Size = new System.Drawing.Size(256, 28);
            this.btnStopAll.Style = Sunny.UI.UIStyle.Custom;
            this.btnStopAll.TabIndex = 7;
            this.btnStopAll.Text = "全部停止（急停）";
            this.btnStopAll.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnStopAll.Click += new System.EventHandler(this.btnStopAll_Click);
            // 
            // btnInputLot
            // 
            this.btnInputLot.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnInputLot.FillColor = System.Drawing.Color.ForestGreen;
            this.btnInputLot.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnInputLot.ForeColor = System.Drawing.Color.White;
            this.btnInputLot.Location = new System.Drawing.Point(15, 63);
            this.btnInputLot.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnInputLot.Name = "btnInputLot";
            this.btnInputLot.RectColor = System.Drawing.Color.ForestGreen;
            this.btnInputLot.Size = new System.Drawing.Size(256, 28);
            this.btnInputLot.Style = Sunny.UI.UIStyle.Custom;
            this.btnInputLot.TabIndex = 1;
            this.btnInputLot.Text = "录入批号";
            this.btnInputLot.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnInputLot.Click += new System.EventHandler(this.btnInputLot_Click);
            // 
            // btnBatchRecipe
            // 
            this.btnBatchRecipe.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnBatchRecipe.FillColor = System.Drawing.Color.ForestGreen;
            this.btnBatchRecipe.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnBatchRecipe.ForeColor = System.Drawing.Color.White;
            this.btnBatchRecipe.Location = new System.Drawing.Point(15, 34);
            this.btnBatchRecipe.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnBatchRecipe.Name = "btnBatchRecipe";
            this.btnBatchRecipe.RectColor = System.Drawing.Color.ForestGreen;
            this.btnBatchRecipe.Size = new System.Drawing.Size(256, 28);
            this.btnBatchRecipe.Style = Sunny.UI.UIStyle.Custom;
            this.btnBatchRecipe.TabIndex = 0;
            this.btnBatchRecipe.Text = "批量设置配方";
            this.btnBatchRecipe.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnBatchRecipe.Click += new System.EventHandler(this.btnBatchRecipe_Click);
            // 
            // groupBoxLog
            // 
            this.groupBoxLog.Controls.Add(this.txtLog);
            this.groupBoxLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxLog.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.groupBoxLog.Location = new System.Drawing.Point(4, 515);
            this.groupBoxLog.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.groupBoxLog.MinimumSize = new System.Drawing.Size(1, 1);
            this.groupBoxLog.Name = "groupBoxLog";
            this.groupBoxLog.Padding = new System.Windows.Forms.Padding(0, 32, 0, 0);
            this.groupBoxLog.Size = new System.Drawing.Size(290, 275);
            this.groupBoxLog.TabIndex = 3;
            this.groupBoxLog.TabStop = false;
            this.groupBoxLog.Text = "日志";
            this.groupBoxLog.TextAlignment = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // txtLog
            // 
            this.txtLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtLog.Font = new System.Drawing.Font("Consolas", 8F);
            this.txtLog.Location = new System.Drawing.Point(0, 32);
            this.txtLog.Multiline = true;
            this.txtLog.Name = "txtLog";
            this.txtLog.ReadOnly = true;
            this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtLog.Size = new System.Drawing.Size(290, 243);
            this.txtLog.TabIndex = 0;
            // 
            // statusStripMain
            // 
            this.statusStripMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabelDeviceCount,
            this.toolStripStatusLabelInterval,
            this.toolStripStatusLabelTesting,
            this.toolStripStatusLabelOnline,
            this.toolStripStatusLabelScanner,
            this.toolStripStatusLabelTime});
            this.statusStripMain.Location = new System.Drawing.Point(0, 840);
            this.statusStripMain.Name = "statusStripMain";
            this.statusStripMain.Size = new System.Drawing.Size(1280, 22);
            this.statusStripMain.TabIndex = 3;
            this.statusStripMain.Text = "statusStrip1";
            // 
            // toolStripStatusLabelDeviceCount
            // 
            this.toolStripStatusLabelDeviceCount.Name = "toolStripStatusLabelDeviceCount";
            this.toolStripStatusLabelDeviceCount.Size = new System.Drawing.Size(77, 17);
            this.toolStripStatusLabelDeviceCount.Text = "设备数量: 72";
            // 
            // toolStripStatusLabelInterval
            // 
            this.toolStripStatusLabelInterval.Name = "toolStripStatusLabelInterval";
            this.toolStripStatusLabelInterval.Size = new System.Drawing.Size(108, 17);
            this.toolStripStatusLabelInterval.Text = "采集间隔: 1000ms";
            // 
            // toolStripStatusLabelTesting
            // 
            this.toolStripStatusLabelTesting.Name = "toolStripStatusLabelTesting";
            this.toolStripStatusLabelTesting.Size = new System.Drawing.Size(58, 17);
            this.toolStripStatusLabelTesting.Text = "测试中: 0";
            // 
            // toolStripStatusLabelOnline
            // 
            this.toolStripStatusLabelOnline.ForeColor = System.Drawing.Color.Red;
            this.toolStripStatusLabelOnline.Name = "toolStripStatusLabelOnline";
            this.toolStripStatusLabelOnline.Size = new System.Drawing.Size(65, 17);
            this.toolStripStatusLabelOnline.Text = "在线: 0/72";
            // 
            // toolStripStatusLabelScanner
            // 
            this.toolStripStatusLabelScanner.Name = "toolStripStatusLabelScanner";
            this.toolStripStatusLabelScanner.Size = new System.Drawing.Size(61, 17);
            this.toolStripStatusLabelScanner.Text = "扫码枪: --";
            // 
            // toolStripStatusLabelTime
            // 
            this.toolStripStatusLabelTime.Name = "toolStripStatusLabelTime";
            this.toolStripStatusLabelTime.Size = new System.Drawing.Size(126, 17);
            this.toolStripStatusLabelTime.Text = "2024-01-01 00:00:00";
            // 
            // timerTime
            // 
            this.timerTime.Interval = 1000;
            this.timerTime.Tick += new System.EventHandler(this.timerTime_Tick);
            // 
            // hashTimer
            // 
            this.hashTimer.Interval = 3600000;
            this.hashTimer.Tick += new System.EventHandler(this.HashTimer_Tick);
            // 
            // MainForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(1280, 900);
            this.Controls.Add(this.rootScrollPanel);
            this.MinimumSize = new System.Drawing.Size(800, 600);
            this.Name = "MainForm";
            this.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            this.ShowTitle = false;
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.ZoomScaleRect = new System.Drawing.Rectangle(15, 15, 1280, 900);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.rootScrollPanel.ResumeLayout(false);
            this.tableLayoutPanelMain.ResumeLayout(false);
            this.tableLayoutPanelMain.PerformLayout();
            this.tableLayoutPanelHeader.ResumeLayout(false);
            this.tableLayoutPanelHeader.PerformLayout();
            this.pnlProject.ResumeLayout(false);
            this.pnlProject.PerformLayout();
            this.panelPermission.ResumeLayout(false);
            this.panelPermission.PerformLayout();
            this.splitContainerMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).EndInit();
            this.splitContainerMain.ResumeLayout(false);
            this.tableLayoutPanelRight.ResumeLayout(false);
            this.groupBoxStatus.ResumeLayout(false);
            this.groupBoxStatus.PerformLayout();
            this.groupBoxMonitor.ResumeLayout(false);
            this.groupBoxMonitor.PerformLayout();
            this.groupBoxOperation.ResumeLayout(false);
            this.groupBoxLog.ResumeLayout(false);
            this.groupBoxLog.PerformLayout();
            this.statusStripMain.ResumeLayout(false);
            this.statusStripMain.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        // 控件字段声明区域
        // 这些字段在两个 partial 文件中共享（本文件赋值，.cs文件使用）

        /// <summary>【新增】根滚动容器，包裹主布局，支持窗体缩小时显示滚动条</summary>
        private System.Windows.Forms.Panel rootScrollPanel;
        /// <summary>主布局容器（3行：顶栏/内容/状态栏；顶栏菜单并单行）</summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelMain;
        /// <summary>顶栏容器（单行：项目/权限/通讯＋4 按钮；列：项目P34/权限P16/通讯100+70/按钮4×120）</summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelHeader;
        /// <summary>当前项目显示容器（顶栏第 1 列：前缀 + 项目名两个标签，背景与顶栏一致）</summary>
        private System.Windows.Forms.Panel pnlProject;
        /// <summary>固定前缀"当前项目："（常规体不加粗，V1.79 用户点名）</summary>
        private Sunny.UI.UILabel lblProjectPrefix;
        /// <summary>项目名标签（容器内 Dock=Fill，构造里回填项目名；V1.78 起加粗）</summary>
        private Sunny.UI.UILabel lblProject;
        /// <summary>当前操作权限显示容器（V1.19.7：拆为前缀+角色名两个标签；V1.88.27 起与 pnlProject 同构 Panel+Dock，上下居中天然成立）</summary>
        private System.Windows.Forms.Panel panelPermission;
        /// <summary>固定前缀"当前操作权限: "（默认黑字）</summary>
        private Sunny.UI.UILabel lblPermissionPrefix;
        /// <summary>角色名标签（V1.19.7：ForeColor 按权限着色——管理员=红/技术员=蓝/操作员=绿）</summary>
        private Sunny.UI.UILabel lblPermissionRole;
        /// <summary>"通讯模块状态:"标签（V1.16 更名：现场无 PLC，改为通讯连接状态；V1.80 再更名：用户点名）</summary>
        private Sunny.UI.UILabel lblCommStatusLabel;
        /// <summary>通讯模块状态值标签（绿=已连接，红=未连接）</summary>
        private Sunny.UI.UILabel lblCommStatus;
        /// <summary>"送风机运行状态:"标签（V1.10）</summary>
        private Sunny.UI.UILabel lblFanStateLabel;
        /// <summary>送风机运行状态值标签（V1.16.1：未连接=红/定值启动·已连接=绿/定值停止=灰）</summary>
        private Sunny.UI.UILabel lblFanState;
        /// <summary>用户权限按钮</summary>
        private Sunny.UI.UIButton btnUserPermission;
        /// <summary>参数设置按钮</summary>
        private Sunny.UI.UIButton btnParameter;
        /// <summary>LOG记录按钮</summary>
        private Sunny.UI.UIButton btnLog;
        /// <summary>关于按钮（下拉：设置 / 版本说明；V1.64 起深浅模式切换也收进该下拉，仅 dev 可见）</summary>
        private Sunny.UI.UIButton btnAbout;
        /// <summary>窗口最小化按钮（顶栏最右，自绘横线；绘制与行为见 MainForm.InitWindowChrome）</summary>
        private System.Windows.Forms.Button btnWinMin;
        /// <summary>窗口最大化/还原按钮（顶栏最右，自绘方框；最大化时画还原叠框）</summary>
        private System.Windows.Forms.Button btnWinMax;
        /// <summary>窗口关闭按钮（顶栏最右，自绘叉；悬停红底，走正常 FormClosing 流程）</summary>
        private System.Windows.Forms.Button btnWinClose;
        /// <summary>中间分割容器（左:气压表 右:操作面板）</summary>
        private System.Windows.Forms.SplitContainer splitContainerMain;
        /// <summary>右侧布局容器</summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelRight;
        /// <summary>运行状态分组</summary>
        private Sunny.UI.UIGroupBox groupBoxStatus;
        /// <summary>运行状态文本标签</summary>
        private Sunny.UI.UILabel lblRunStatus;
        /// <summary>监视分组（温度显示）</summary>
        private Sunny.UI.UIGroupBox groupBoxMonitor;
        /// <summary>"当前温度"标签（V1.16.1 更名：上部温度 → 当前温度）</summary>
        private Sunny.UI.UILabel lblUpperTempLabel;
        /// <summary>当前温度值显示（V1.16.3：TextBox → Label，保证 ForeColor 生效）</summary>
        private Sunny.UI.UILabel lblUpperTemp;
        /// <summary>"设置温度"标签</summary>
        private Sunny.UI.UILabel lblSetTempLabel;
        /// <summary>设置温度值显示（V1.16.3：TextBox → Label）</summary>
        private Sunny.UI.UILabel lblSetTemp;
        /// <summary>操作分组（V1.67：7 个按钮，新增下料判定；顺序：批量设置配方/录入批号/启动运行/停止运行/报警复位/下料判定/全部停止）</summary>
        private Sunny.UI.UIGroupBox groupBoxOperation;
        /// <summary>启动运行按钮</summary>
        private Sunny.UI.UIButton btnStartRun;
        /// <summary>停止运行按钮（V1.10）</summary>
        private Sunny.UI.UIButton btnStopRun;
        /// <summary>报警复位按钮（V1.10）</summary>
        private Sunny.UI.UIButton btnResetAlarm;
        /// <summary>下料判定按钮（V1.67：Q22 待判定配套）</summary>
        private Sunny.UI.UIButton btnUnloadJudge;
        /// <summary>全部停止（急停）按钮（V1.10）</summary>
        private Sunny.UI.UIButton btnStopAll;
        /// <summary>录入批号按钮</summary>
        private Sunny.UI.UIButton btnInputLot;
        /// <summary>批量设置配方按钮</summary>
        private Sunny.UI.UIButton btnBatchRecipe;
        /// <summary>LOG日志分组</summary>
        private Sunny.UI.UIGroupBox groupBoxLog;
        /// <summary>日志输出文本框</summary>
        private System.Windows.Forms.TextBox txtLog;
        /// <summary>底部状态栏</summary>
        private System.Windows.Forms.StatusStrip statusStripMain;
        /// <summary>设备数量状态标签</summary>
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelDeviceCount;
        /// <summary>采集间隔状态标签</summary>
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelInterval;
        /// <summary>测试中台数状态标签（V1.10）</summary>
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelTesting;
        /// <summary>在线台数状态标签（V1.10）</summary>
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelOnline;
        /// <summary>扫码枪连接状态标签（V1.16.2）</summary>
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelScanner;
        /// <summary>当前时间状态标签</summary>
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelTime;
        /// <summary>时间更新定时器</summary>
        private System.Windows.Forms.Timer timerTime;
        /// <summary>授权计时器（V1.87：1 小时一格，与 HJVision 的 HashTimer 一致）</summary>
        private System.Windows.Forms.Timer hashTimer;
    }
}
