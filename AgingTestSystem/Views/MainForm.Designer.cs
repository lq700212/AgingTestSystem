namespace AgingTestSystem.Views
{
    /// <summary>
    /// 主窗体 —— 设计器自动生成部分
    ///
    /// 【说明】
    /// 本文件由 Visual Studio 设计器维护，包含所有控件的创建和布局代码。
    /// 请勿手动修改此文件内容，所有修改请通过设计器界面操作。
    /// 业务逻辑代码请放在 MainForm.cs 文件中。
    ///
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
            this.tableLayoutPanelTop = new System.Windows.Forms.TableLayoutPanel();
            this.lblProject = new Sunny.UI.UILabel();
            this.panelPermission = new System.Windows.Forms.FlowLayoutPanel();
            this.lblPermissionPrefix = new Sunny.UI.UILabel();
            this.lblPermissionRole = new Sunny.UI.UILabel();
            this.lblCommStatusLabel = new Sunny.UI.UILabel();
            this.lblCommStatus = new Sunny.UI.UILabel();
            this.tableLayoutPanelMenu = new System.Windows.Forms.TableLayoutPanel();
            this.btnUserPermission = new Sunny.UI.UIButton();
            this.btnParameter = new Sunny.UI.UIButton();
            this.btnLog = new Sunny.UI.UIButton();
            this.btnAbout = new Sunny.UI.UIButton();
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
            this.rootScrollPanel.SuspendLayout();
            this.tableLayoutPanelMain.SuspendLayout();
            this.tableLayoutPanelTop.SuspendLayout();
            this.tableLayoutPanelMenu.SuspendLayout();
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
            // 【V1.65】窗体级最小尺寸 1400×900 → 1150×800：原来 1400 宽在 1366 宽工控机上
            // 一最大化就出现窗体级横向滚动条（就差 34px）。1150 保证 1366/1280 屏一屏显示；
            // 右侧按 23.4% 比例自适应（见 MainForm.ComputeRightPanelWidth），不再靠最小宽撑布局。
            this.rootScrollPanel.AutoScrollMinSize = new System.Drawing.Size(1150, 800);
            this.rootScrollPanel.Controls.Add(this.tableLayoutPanelMain);
            this.rootScrollPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rootScrollPanel.Location = new System.Drawing.Point(0, 0);
            this.rootScrollPanel.Name = "rootScrollPanel";
            this.rootScrollPanel.Size = new System.Drawing.Size(1280, 900);
            this.rootScrollPanel.TabIndex = 0;
            // 
            // tableLayoutPanelMain
            // 
            this.tableLayoutPanelMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanelMain.ColumnCount = 1;
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.Controls.Add(this.tableLayoutPanelTop, 0, 0);
            this.tableLayoutPanelMain.Controls.Add(this.tableLayoutPanelMenu, 0, 1);
            this.tableLayoutPanelMain.Controls.Add(this.splitContainerMain, 0, 2);
            this.tableLayoutPanelMain.Controls.Add(this.statusStripMain, 0, 3);
            this.tableLayoutPanelMain.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanelMain.MinimumSize = new System.Drawing.Size(1150, 800);
            this.tableLayoutPanelMain.Name = "tableLayoutPanelMain";
            this.tableLayoutPanelMain.RowCount = 4;
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanelMain.Size = new System.Drawing.Size(1280, 900);
            this.tableLayoutPanelMain.TabIndex = 0;
            // 
            // tableLayoutPanelTop
            // 【V1.72.7】顶栏显示当前项目（切错项目=跑错工艺，首屏可见防呆）；
            // 【V1.72.8】删 lblTitle（软件名窗口标题栏已有，顶栏重复多余），当前项目
            // 直接占第 1 列（列宽回到 40/25/20/15，权限/通讯状态两组保留不动）。
            //
            this.tableLayoutPanelTop.ColumnCount = 4;
            this.tableLayoutPanelTop.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 40F));
            this.tableLayoutPanelTop.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanelTop.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanelTop.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 15F));
            this.tableLayoutPanelTop.Controls.Add(this.lblProject, 0, 0);
            this.tableLayoutPanelTop.Controls.Add(this.panelPermission, 1, 0);
            this.tableLayoutPanelTop.Controls.Add(this.lblCommStatusLabel, 2, 0);
            this.tableLayoutPanelTop.Controls.Add(this.lblCommStatus, 3, 0);
            this.tableLayoutPanelTop.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelTop.Location = new System.Drawing.Point(3, 3);
            this.tableLayoutPanelTop.Name = "tableLayoutPanelTop";
            this.tableLayoutPanelTop.RowCount = 1;
            this.tableLayoutPanelTop.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelTop.Size = new System.Drawing.Size(1274, 24);
            this.tableLayoutPanelTop.TabIndex = 0;
            //
            // lblProject - 当前项目显示（【V1.72.7 新增】切错项目=跑错工艺，顶栏首屏可见防呆；
            // 项目名由 MainForm 构造里 UpdateProjectDisplay 回填，切换项目必须重启故只需设一次；
            // Dock=Fill 占满第 1 列（【V1.72.8】标题删后项目移到该列，40% 宽），
            // 超长项目名 AutoEllipsis 省略号不断行）。
            //
            this.lblProject.AutoEllipsis = true;
            this.lblProject.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblProject.Location = new System.Drawing.Point(3, 0);
            this.lblProject.Name = "lblProject";
            this.lblProject.Size = new System.Drawing.Size(503, 24);
            this.lblProject.TabIndex = 4;
            this.lblProject.Text = "当前项目：烧屏测试";
            this.lblProject.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // panelPermission - 当前操作权限显示容器（V1.19.7：拆为"前缀 + 角色名"两个标签）
            // FlowLayoutPanel 水平排列：前缀标签固定黑色，角色名标签由
            // MainForm.UpdatePermissionDisplay 按权限设置 ForeColor（管理员=红/技术员=蓝/操作员=绿）。
            // 背景色与顶栏一致，观感与普通标签相同。
            // 
            this.panelPermission.BackColor = System.Drawing.SystemColors.Control;
            this.panelPermission.Controls.Add(this.lblPermissionPrefix);
            this.panelPermission.Controls.Add(this.lblPermissionRole);
            this.panelPermission.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelPermission.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            this.panelPermission.Location = new System.Drawing.Point(560, 0);
            this.panelPermission.Margin = new System.Windows.Forms.Padding(0);
            this.panelPermission.Name = "panelPermission";
            this.panelPermission.Padding = new System.Windows.Forms.Padding(0);
            this.panelPermission.Size = new System.Drawing.Size(348, 24);
            this.panelPermission.TabIndex = 1;
            this.panelPermission.WrapContents = false;
            // 
            // lblPermissionPrefix - 固定前缀"当前操作权限: "（始终默认黑字）
            // 
            this.lblPermissionPrefix.AutoSize = true;
            this.lblPermissionPrefix.Location = new System.Drawing.Point(3, 3);
            this.lblPermissionPrefix.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
            this.lblPermissionPrefix.Name = "lblPermissionPrefix";
            this.lblPermissionPrefix.Size = new System.Drawing.Size(110, 17);
            this.lblPermissionPrefix.TabIndex = 0;
            this.lblPermissionPrefix.Text = "当前操作权限: ";
            // 
            // lblPermissionRole - 角色名（V1.19.7：运行时按权限着色）
            // 
            this.lblPermissionRole.AutoSize = true;
            this.lblPermissionRole.Location = new System.Drawing.Point(110, 3);
            this.lblPermissionRole.Margin = new System.Windows.Forms.Padding(0, 6, 0, 0);
            this.lblPermissionRole.Name = "lblPermissionRole";
            this.lblPermissionRole.Size = new System.Drawing.Size(40, 17);
            this.lblPermissionRole.TabIndex = 1;
            this.lblPermissionRole.Text = "操作员";
            // 
            // lblCommStatusLabel
            // 
            this.lblCommStatusLabel.AutoSize = true;
            this.lblCommStatusLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblCommStatusLabel.Location = new System.Drawing.Point(908, 0);
            this.lblCommStatusLabel.Name = "lblCommStatusLabel";
            this.lblCommStatusLabel.Size = new System.Drawing.Size(83, 12);
            this.lblCommStatusLabel.TabIndex = 2;
            this.lblCommStatusLabel.Text = "通讯连接状态:";
            this.lblCommStatusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblCommStatus
            // 
            this.lblCommStatus.AutoSize = true;
            this.lblCommStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblCommStatus.ForeColor = System.Drawing.Color.Red;
            this.lblCommStatus.Location = new System.Drawing.Point(1186, 0);
            this.lblCommStatus.Name = "lblCommStatus";
            this.lblCommStatus.Size = new System.Drawing.Size(41, 12);
            this.lblCommStatus.TabIndex = 3;
            this.lblCommStatus.Text = "未连接";
            this.lblCommStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // tableLayoutPanelMenu
            // 
            this.tableLayoutPanelMenu.ColumnCount = 4;
            this.tableLayoutPanelMenu.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanelMenu.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanelMenu.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanelMenu.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanelMenu.Controls.Add(this.btnUserPermission, 0, 0);
            this.tableLayoutPanelMenu.Controls.Add(this.btnParameter, 1, 0);
            this.tableLayoutPanelMenu.Controls.Add(this.btnLog, 2, 0);
            this.tableLayoutPanelMenu.Controls.Add(this.btnAbout, 3, 0);
            this.tableLayoutPanelMenu.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelMenu.Location = new System.Drawing.Point(3, 33);
            this.tableLayoutPanelMenu.Name = "tableLayoutPanelMenu";
            this.tableLayoutPanelMenu.RowCount = 1;
            this.tableLayoutPanelMenu.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMenu.Size = new System.Drawing.Size(1274, 34);
            this.tableLayoutPanelMenu.TabIndex = 1;
            // 
            // btnUserPermission
            // 
            this.btnUserPermission.Location = new System.Drawing.Point(3, 3);
            this.btnUserPermission.Name = "btnUserPermission";
            this.btnUserPermission.Size = new System.Drawing.Size(226, 28);
            this.btnUserPermission.TabIndex = 0;
            this.btnUserPermission.Text = "用户权限";
            this.btnUserPermission.Click += new System.EventHandler(this.btnUserPermission_Click);
            // 
            // btnParameter
            // 
            this.btnParameter.Location = new System.Drawing.Point(282, 3);
            this.btnParameter.Name = "btnParameter";
            this.btnParameter.Size = new System.Drawing.Size(226, 28);
            this.btnParameter.TabIndex = 1;
            this.btnParameter.Text = "参数设置";
            this.btnParameter.Click += new System.EventHandler(this.btnParameter_Click);
            // 
            // btnLog
            // 
            this.btnLog.Location = new System.Drawing.Point(561, 3);
            this.btnLog.Name = "btnLog";
            this.btnLog.Size = new System.Drawing.Size(226, 28);
            this.btnLog.TabIndex = 2;
            this.btnLog.Text = "日志记录";
            this.btnLog.Click += new System.EventHandler(this.btnLog_Click);
            // 
            // btnAbout - "关于"按钮（V1.19.12 更名：btnHelp → btnAbout，文字 帮助 → 关于）
            // 点击弹出下拉菜单：设置（仅管理员） / 版本说明
            //
            this.btnAbout.Location = new System.Drawing.Point(840, 3);
            this.btnAbout.Name = "btnAbout";
            this.btnAbout.Size = new System.Drawing.Size(228, 28);
            this.btnAbout.TabIndex = 4;
            this.btnAbout.Text = "关于";
            this.btnAbout.Click += new System.EventHandler(this.btnAbout_Click);
            // 
            // splitContainerMain
            // 
            this.splitContainerMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerMain.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainerMain.Location = new System.Drawing.Point(3, 73);
            this.splitContainerMain.Name = "splitContainerMain";
            // 
            // splitContainerMain.Panel2
            // 
            this.splitContainerMain.Panel2.Controls.Add(this.tableLayoutPanelRight);
            this.splitContainerMain.Size = new System.Drawing.Size(1274, 799);
            // 【V1.65】设计值按比例换算：1274 × 0.234 ≈ 298 右侧 → 1274-298-4(分隔条)=972。
            // 运行时会被 AdjustRightPanelWidth 按窗口实际宽度重算覆盖，这里只保证设计视图不错位。
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
            this.tableLayoutPanelRight.Size = new System.Drawing.Size(298, 799);
            this.tableLayoutPanelRight.TabIndex = 0;
            // 
            // groupBoxStatus
            // 
            this.groupBoxStatus.Controls.Add(this.lblRunStatus);
            this.groupBoxStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxStatus.Location = new System.Drawing.Point(3, 3);
            this.groupBoxStatus.Name = "groupBoxStatus";
            this.groupBoxStatus.Size = new System.Drawing.Size(292, 84);
            this.groupBoxStatus.TabIndex = 0;
            this.groupBoxStatus.TabStop = false;
            this.groupBoxStatus.Text = "运行状态";
            // 
            // lblRunStatus
            // 
            this.lblRunStatus.AutoSize = true;
            this.lblRunStatus.Location = new System.Drawing.Point(15, 44);
            this.lblRunStatus.Name = "lblRunStatus";
            this.lblRunStatus.Size = new System.Drawing.Size(37, 20);
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
            this.groupBoxMonitor.Location = new System.Drawing.Point(3, 93);
            this.groupBoxMonitor.Name = "groupBoxMonitor";
            this.groupBoxMonitor.Size = new System.Drawing.Size(292, 114);
            this.groupBoxMonitor.TabIndex = 1;
            this.groupBoxMonitor.TabStop = false;
            this.groupBoxMonitor.Text = "监视";
            // 
            // lblUpperTempLabel
            //
            this.lblUpperTempLabel.AutoSize = true;
            this.lblUpperTempLabel.Location = new System.Drawing.Point(15, 86);
            this.lblUpperTempLabel.Name = "lblUpperTempLabel";
            this.lblUpperTempLabel.Size = new System.Drawing.Size(53, 12);
            this.lblUpperTempLabel.TabIndex = 3;
            this.lblUpperTempLabel.Text = "当前温度";
            // 
            // lblUpperTemp
            //
            this.lblUpperTemp.AutoSize = true;
            this.lblUpperTemp.Location = new System.Drawing.Point(100, 86);
            this.lblUpperTemp.Name = "lblUpperTemp";
            this.lblUpperTemp.TabIndex = 2;
            this.lblUpperTemp.Text = "---";
            //
            // lblSetTempLabel
            //
            this.lblSetTempLabel.AutoSize = true;
            this.lblSetTempLabel.Location = new System.Drawing.Point(15, 60);
            this.lblSetTempLabel.Name = "lblSetTempLabel";
            this.lblSetTempLabel.Size = new System.Drawing.Size(53, 12);
            this.lblSetTempLabel.TabIndex = 1;
            this.lblSetTempLabel.Text = "设置温度";
            //
            // lblSetTemp
            //
            this.lblSetTemp.AutoSize = true;
            this.lblSetTemp.Location = new System.Drawing.Point(100, 60);
            this.lblSetTemp.Name = "lblSetTemp";
            this.lblSetTemp.TabIndex = 0;
            this.lblSetTemp.Text = "---";
            // 
            // lblFanStateLabel
            // 
            this.lblFanStateLabel.AutoSize = true;
            this.lblFanStateLabel.Location = new System.Drawing.Point(15, 34);
            this.lblFanStateLabel.Name = "lblFanStateLabel";
            this.lblFanStateLabel.Size = new System.Drawing.Size(65, 12);
            this.lblFanStateLabel.TabIndex = 6;
            this.lblFanStateLabel.Text = "送风机状态";
            // 
            // lblFanState
            // 
            this.lblFanState.AutoSize = true;
            this.lblFanState.ForeColor = System.Drawing.Color.Red;
            this.lblFanState.Location = new System.Drawing.Point(112, 34);
            this.lblFanState.Name = "lblFanState";
            this.lblFanState.Size = new System.Drawing.Size(44, 17);
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
            this.groupBoxOperation.Location = new System.Drawing.Point(3, 243);
            this.groupBoxOperation.Name = "groupBoxOperation";
            this.groupBoxOperation.Size = new System.Drawing.Size(292, 294);
            this.groupBoxOperation.TabIndex = 2;
            this.groupBoxOperation.TabStop = false;
            this.groupBoxOperation.Text = "操作";
            //
            // btnBatchRecipe（V1.59.1 重排：按业务流程顺序 选配方→录批号→启动→停止→复位→急停，自上而下）
            // 【V1.71】语义绿走 Custom+FillColor（自绘按钮 BackColor 画不出来）
            //
            this.btnBatchRecipe.FillColor = System.Drawing.Color.LimeGreen;
            this.btnBatchRecipe.RectColor = System.Drawing.Color.LimeGreen;
            this.btnBatchRecipe.ForeColor = System.Drawing.Color.White;
            this.btnBatchRecipe.Style = Sunny.UI.UIStyle.Custom;
            this.btnBatchRecipe.Location = new System.Drawing.Point(15, 34);
            this.btnBatchRecipe.Name = "btnBatchRecipe";
            this.btnBatchRecipe.Size = new System.Drawing.Size(256, 28);
            this.btnBatchRecipe.TabIndex = 0;
            this.btnBatchRecipe.Text = "批量设置配方";
            this.btnBatchRecipe.Click += new System.EventHandler(this.btnBatchRecipe_Click);
            //
            // btnInputLot
            //
            this.btnInputLot.FillColor = System.Drawing.Color.LimeGreen;
            this.btnInputLot.RectColor = System.Drawing.Color.LimeGreen;
            this.btnInputLot.ForeColor = System.Drawing.Color.White;
            this.btnInputLot.Style = Sunny.UI.UIStyle.Custom;
            this.btnInputLot.Location = new System.Drawing.Point(15, 63);
            this.btnInputLot.Name = "btnInputLot";
            this.btnInputLot.Size = new System.Drawing.Size(256, 28);
            this.btnInputLot.TabIndex = 1;
            this.btnInputLot.Text = "录入批号";
            this.btnInputLot.Click += new System.EventHandler(this.btnInputLot_Click);
            //
            // btnStartRun
            //
            this.btnStartRun.FillColor = System.Drawing.Color.DodgerBlue;
            this.btnStartRun.RectColor = System.Drawing.Color.DodgerBlue;
            this.btnStartRun.ForeColor = System.Drawing.Color.White;
            this.btnStartRun.Style = Sunny.UI.UIStyle.Custom;
            this.btnStartRun.Location = new System.Drawing.Point(15, 92);
            this.btnStartRun.Name = "btnStartRun";
            this.btnStartRun.Size = new System.Drawing.Size(256, 28);
            this.btnStartRun.TabIndex = 2;
            this.btnStartRun.Text = "启动运行（选中台）";
            this.btnStartRun.Click += new System.EventHandler(this.btnStartRun_Click);
            //
            // btnStopRun
            //
            // 【V1.71】无语义默认灰走 Sunny Gray 档（深色换肤经 ApplyOperationButtonsTheme+ApplyButtonColors）
            //
            this.btnStopRun.Style = Sunny.UI.UIStyle.Gray;
            this.btnStopRun.Location = new System.Drawing.Point(15, 121);
            this.btnStopRun.Name = "btnStopRun";
            this.btnStopRun.Size = new System.Drawing.Size(256, 28);
            this.btnStopRun.TabIndex = 3;
            this.btnStopRun.Text = "停止运行（选中台）";
            this.btnStopRun.Click += new System.EventHandler(this.btnStopRun_Click);
            //
            // btnResetAlarm
            //
            this.btnResetAlarm.Style = Sunny.UI.UIStyle.Gray;
            this.btnResetAlarm.Location = new System.Drawing.Point(15, 150);
            this.btnResetAlarm.Name = "btnResetAlarm";
            this.btnResetAlarm.Size = new System.Drawing.Size(256, 28);
            this.btnResetAlarm.TabIndex = 4;
            this.btnResetAlarm.Text = "报警复位（选中台）";
            this.btnResetAlarm.Click += new System.EventHandler(this.btnResetAlarm_Click);
            //
            // btnUnloadJudge（V1.67：Q22 待判定配套，下料时人工录 PASS/FAIL；平时隐藏？不：
            // 常驻但 AutoPass 下点它只提示，无操作员误触风险；位置在复位下、急停上，29px 步进）
            //
            this.btnUnloadJudge.Style = Sunny.UI.UIStyle.Gray;
            this.btnUnloadJudge.Location = new System.Drawing.Point(15, 208);
            this.btnUnloadJudge.Name = "btnUnloadJudge";
            this.btnUnloadJudge.Size = new System.Drawing.Size(256, 28);
            this.btnUnloadJudge.TabIndex = 6;
            this.btnUnloadJudge.Text = "下料判定（选中台）";
            this.btnUnloadJudge.Click += new System.EventHandler(this.btnUnloadJudge_Click);
            //
            // btnStopAll
            //
            this.btnStopAll.FillColor = System.Drawing.Color.Crimson;
            this.btnStopAll.RectColor = System.Drawing.Color.Crimson;
            this.btnStopAll.ForeColor = System.Drawing.Color.White;
            this.btnStopAll.Style = Sunny.UI.UIStyle.Custom;
            this.btnStopAll.Location = new System.Drawing.Point(15, 237);
            this.btnStopAll.Name = "btnStopAll";
            this.btnStopAll.Size = new System.Drawing.Size(256, 28);
            this.btnStopAll.TabIndex = 7;
            this.btnStopAll.Text = "全部停止（急停）";
            this.btnStopAll.Click += new System.EventHandler(this.btnStopAll_Click);
            //
            // groupBoxLog
            //
            this.groupBoxLog.Controls.Add(this.txtLog);
            this.groupBoxLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxLog.Location = new System.Drawing.Point(3, 543);
            this.groupBoxLog.Name = "groupBoxLog";
            this.groupBoxLog.Size = new System.Drawing.Size(292, 253);
            this.groupBoxLog.TabIndex = 3;
            this.groupBoxLog.TabStop = false;
            this.groupBoxLog.Text = "日志";
            // 
            // txtLog
            // 
            this.txtLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtLog.Font = new System.Drawing.Font("Consolas", 8F);
            this.txtLog.Location = new System.Drawing.Point(3, 17);
            this.txtLog.Multiline = true;
            this.txtLog.Name = "txtLog";
            this.txtLog.ReadOnly = true;
            this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtLog.Size = new System.Drawing.Size(280, 233);
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
            this.statusStripMain.Location = new System.Drawing.Point(0, 878);
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
            this.toolStripStatusLabelScanner.Size = new System.Drawing.Size(88, 17);
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
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1280, 900);
            // 【V1.71】UIForm 自绘蓝标题：Dock=Fill 主布局加顶 Pad 避开标题区。
            this.Padding = new System.Windows.Forms.Padding(0, 38, 0, 0);
            // 【V1.71】UIForm 自绘蓝标题：Dock=Fill 主布局加顶 Pad 避开标题区。
            this.Padding = new System.Windows.Forms.Padding(0, 38, 0, 0);
            this.Controls.Add(this.rootScrollPanel);
            this.MinimumSize = new System.Drawing.Size(800, 600);
            this.Name = "MainForm";
            this.Text = "老化测试系统V1.16";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.rootScrollPanel.ResumeLayout(false);
            this.tableLayoutPanelMain.ResumeLayout(false);
            this.tableLayoutPanelMain.PerformLayout();
            this.tableLayoutPanelTop.ResumeLayout(false);
            this.tableLayoutPanelTop.PerformLayout();
            this.tableLayoutPanelMenu.ResumeLayout(false);
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
        /// <summary>主布局容器（4行：顶栏/菜单/内容/状态栏）</summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelMain;
        /// <summary>顶部信息栏容器</summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelTop;
        /// <summary>当前项目显示标签（【V1.72.7 新增】顶栏第 1 列，构造里回填项目名）</summary>
        private Sunny.UI.UILabel lblProject;
        /// <summary>当前操作权限显示容器（V1.19.7：拆为前缀+角色名两个标签）</summary>
        private System.Windows.Forms.FlowLayoutPanel panelPermission;
        /// <summary>固定前缀"当前操作权限: "（默认黑字）</summary>
        private Sunny.UI.UILabel lblPermissionPrefix;
        /// <summary>角色名标签（V1.19.7：ForeColor 按权限着色——管理员=红/技术员=蓝/操作员=绿）</summary>
        private Sunny.UI.UILabel lblPermissionRole;
        /// <summary>"通讯连接状态:"标签（V1.16 更名：现场无 PLC，改为通讯连接状态）</summary>
        private Sunny.UI.UILabel lblCommStatusLabel;
        /// <summary>通讯连接状态值标签（绿=已连接，红=未连接）</summary>
        private Sunny.UI.UILabel lblCommStatus;
        /// <summary>"送风机运行状态:"标签（V1.10）</summary>
        private Sunny.UI.UILabel lblFanStateLabel;
        /// <summary>送风机运行状态值标签（V1.16.1：未连接=红/定值启动·已连接=绿/定值停止=灰）</summary>
        private Sunny.UI.UILabel lblFanState;
        /// <summary>菜单按钮栏容器</summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelMenu;
        /// <summary>用户权限按钮</summary>
        private Sunny.UI.UIButton btnUserPermission;
        /// <summary>参数设置按钮</summary>
        private Sunny.UI.UIButton btnParameter;
        /// <summary>LOG记录按钮</summary>
        private Sunny.UI.UIButton btnLog;
        /// <summary>关于按钮（下拉：设置 / 版本说明；V1.64 起深浅模式切换也收进该下拉，仅 dev 可见）</summary>
        private Sunny.UI.UIButton btnAbout;
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
    }
}
