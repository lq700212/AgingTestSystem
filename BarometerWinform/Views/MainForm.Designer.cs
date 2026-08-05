namespace BarometerWinform.Views
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
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblPermission = new System.Windows.Forms.Label();
            this.lblPlcStatusLabel = new System.Windows.Forms.Label();
            this.lblPlcStatus = new System.Windows.Forms.Label();
            this.tableLayoutPanelMenu = new System.Windows.Forms.TableLayoutPanel();
            this.btnUserPermission = new System.Windows.Forms.Button();
            this.btnCommunication = new System.Windows.Forms.Button();
            this.btnParameter = new System.Windows.Forms.Button();
            this.btnLog = new System.Windows.Forms.Button();
            this.btnTest = new System.Windows.Forms.Button();
            this.btnAbout = new System.Windows.Forms.Button();
            this.splitContainerMain = new System.Windows.Forms.SplitContainer();
            this.tableLayoutPanelRight = new System.Windows.Forms.TableLayoutPanel();
            this.groupBoxStatus = new System.Windows.Forms.GroupBox();
            this.lblRunStatus = new System.Windows.Forms.Label();
            this.groupBoxMonitor = new System.Windows.Forms.GroupBox();
            this.lblLowerTempLabel = new System.Windows.Forms.Label();
            this.txtLowerTemp = new System.Windows.Forms.TextBox();
            this.lblUpperTempLabel = new System.Windows.Forms.Label();
            this.txtUpperTemp = new System.Windows.Forms.TextBox();
            this.lblSetTempLabel = new System.Windows.Forms.Label();
            this.txtSetTemp = new System.Windows.Forms.TextBox();
            this.lblFanStateLabel = new System.Windows.Forms.Label();
            this.lblFanState = new System.Windows.Forms.Label();
            this.groupBoxOperation = new System.Windows.Forms.GroupBox();
            this.btnStartRun = new System.Windows.Forms.Button();
            this.btnStopRun = new System.Windows.Forms.Button();
            this.btnResetAlarm = new System.Windows.Forms.Button();
            this.btnStopAll = new System.Windows.Forms.Button();
            this.btnInputLot = new System.Windows.Forms.Button();
            this.btnBatchRecipe = new System.Windows.Forms.Button();
            this.btnVacuum = new System.Windows.Forms.Button();
            this.btnFanStop = new System.Windows.Forms.Button();
            this.btnTemperatureControl = new System.Windows.Forms.Button();
            this.groupBoxLog = new System.Windows.Forms.GroupBox();
            this.txtLog = new System.Windows.Forms.TextBox();
            this.statusStripMain = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabelDeviceCount = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabelInterval = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabelTesting = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripStatusLabelOnline = new System.Windows.Forms.ToolStripStatusLabel();
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
            this.rootScrollPanel.AutoScrollMinSize = new System.Drawing.Size(1400, 900);
            this.rootScrollPanel.Controls.Add(this.tableLayoutPanelMain);
            this.rootScrollPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rootScrollPanel.Location = new System.Drawing.Point(0, 0);
            this.rootScrollPanel.Name = "rootScrollPanel";
            this.rootScrollPanel.Size = new System.Drawing.Size(1400, 900);
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
            this.tableLayoutPanelMain.MinimumSize = new System.Drawing.Size(1400, 900);
            this.tableLayoutPanelMain.Name = "tableLayoutPanelMain";
            this.tableLayoutPanelMain.RowCount = 4;
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanelMain.Size = new System.Drawing.Size(1400, 900);
            this.tableLayoutPanelMain.TabIndex = 0;
            // 
            // tableLayoutPanelTop
            // 
            this.tableLayoutPanelTop.ColumnCount = 4;
            this.tableLayoutPanelTop.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 40F));
            this.tableLayoutPanelTop.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanelTop.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanelTop.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 15F));
            this.tableLayoutPanelTop.Controls.Add(this.lblTitle, 0, 0);
            this.tableLayoutPanelTop.Controls.Add(this.lblPermission, 1, 0);
            this.tableLayoutPanelTop.Controls.Add(this.lblPlcStatusLabel, 2, 0);
            this.tableLayoutPanelTop.Controls.Add(this.lblPlcStatus, 3, 0);
            this.tableLayoutPanelTop.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelTop.Location = new System.Drawing.Point(3, 3);
            this.tableLayoutPanelTop.Name = "tableLayoutPanelTop";
            this.tableLayoutPanelTop.RowCount = 1;
            this.tableLayoutPanelTop.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelTop.Size = new System.Drawing.Size(1394, 24);
            this.tableLayoutPanelTop.TabIndex = 0;
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Location = new System.Drawing.Point(3, 0);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(113, 17);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "老化测试系统V1.15";
            // 
            // lblPermission
            // 
            this.lblPermission.AutoSize = true;
            this.lblPermission.Location = new System.Drawing.Point(560, 0);
            this.lblPermission.Name = "lblPermission";
            this.lblPermission.Size = new System.Drawing.Size(125, 12);
            this.lblPermission.TabIndex = 1;
            this.lblPermission.Text = "当前操作权限: 操作员";
            // 
            // lblPlcStatusLabel
            // 
            this.lblPlcStatusLabel.AutoSize = true;
            this.lblPlcStatusLabel.Location = new System.Drawing.Point(908, 0);
            this.lblPlcStatusLabel.Name = "lblPlcStatusLabel";
            this.lblPlcStatusLabel.Size = new System.Drawing.Size(77, 12);
            this.lblPlcStatusLabel.TabIndex = 2;
            this.lblPlcStatusLabel.Text = "PLC连接状态:";
            // 
            // lblPlcStatus
            // 
            this.lblPlcStatus.AutoSize = true;
            this.lblPlcStatus.Location = new System.Drawing.Point(1186, 0);
            this.lblPlcStatus.Name = "lblPlcStatus";
            this.lblPlcStatus.Size = new System.Drawing.Size(41, 12);
            this.lblPlcStatus.TabIndex = 3;
            this.lblPlcStatus.Text = "未连接";
            // 
            // tableLayoutPanelMenu
            // 
            this.tableLayoutPanelMenu.ColumnCount = 6;
            this.tableLayoutPanelMenu.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16.66667F));
            this.tableLayoutPanelMenu.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16.66667F));
            this.tableLayoutPanelMenu.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16.66667F));
            this.tableLayoutPanelMenu.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16.66667F));
            this.tableLayoutPanelMenu.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16.66667F));
            this.tableLayoutPanelMenu.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16.66667F));
            this.tableLayoutPanelMenu.Controls.Add(this.btnUserPermission, 0, 0);
            this.tableLayoutPanelMenu.Controls.Add(this.btnCommunication, 1, 0);
            this.tableLayoutPanelMenu.Controls.Add(this.btnParameter, 2, 0);
            this.tableLayoutPanelMenu.Controls.Add(this.btnLog, 3, 0);
            this.tableLayoutPanelMenu.Controls.Add(this.btnTest, 4, 0);
            this.tableLayoutPanelMenu.Controls.Add(this.btnAbout, 5, 0);
            this.tableLayoutPanelMenu.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelMenu.Location = new System.Drawing.Point(3, 33);
            this.tableLayoutPanelMenu.Name = "tableLayoutPanelMenu";
            this.tableLayoutPanelMenu.RowCount = 1;
            this.tableLayoutPanelMenu.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMenu.Size = new System.Drawing.Size(1394, 34);
            this.tableLayoutPanelMenu.TabIndex = 1;
            // 
            // btnUserPermission
            // 
            this.btnUserPermission.BackColor = System.Drawing.Color.LimeGreen;
            this.btnUserPermission.ForeColor = System.Drawing.Color.White;
            this.btnUserPermission.Location = new System.Drawing.Point(3, 3);
            this.btnUserPermission.Name = "btnUserPermission";
            this.btnUserPermission.Size = new System.Drawing.Size(226, 28);
            this.btnUserPermission.TabIndex = 0;
            this.btnUserPermission.Text = "用户权限";
            this.btnUserPermission.UseVisualStyleBackColor = false;
            this.btnUserPermission.Click += new System.EventHandler(this.btnUserPermission_Click);
            // 
            // btnCommunication
            // 
            this.btnCommunication.BackColor = System.Drawing.Color.LimeGreen;
            this.btnCommunication.ForeColor = System.Drawing.Color.White;
            this.btnCommunication.Location = new System.Drawing.Point(235, 3);
            this.btnCommunication.Name = "btnCommunication";
            this.btnCommunication.Size = new System.Drawing.Size(226, 28);
            this.btnCommunication.TabIndex = 1;
            this.btnCommunication.Text = "通信设置";
            this.btnCommunication.UseVisualStyleBackColor = false;
            this.btnCommunication.Click += new System.EventHandler(this.btnCommunication_Click);
            // 
            // btnParameter
            // 
            this.btnParameter.BackColor = System.Drawing.Color.LimeGreen;
            this.btnParameter.ForeColor = System.Drawing.Color.White;
            this.btnParameter.Location = new System.Drawing.Point(467, 3);
            this.btnParameter.Name = "btnParameter";
            this.btnParameter.Size = new System.Drawing.Size(226, 28);
            this.btnParameter.TabIndex = 2;
            this.btnParameter.Text = "参数设置";
            this.btnParameter.UseVisualStyleBackColor = false;
            this.btnParameter.Click += new System.EventHandler(this.btnParameter_Click);
            // 
            // btnLog
            // 
            this.btnLog.BackColor = System.Drawing.Color.LimeGreen;
            this.btnLog.ForeColor = System.Drawing.Color.White;
            this.btnLog.Location = new System.Drawing.Point(699, 3);
            this.btnLog.Name = "btnLog";
            this.btnLog.Size = new System.Drawing.Size(226, 28);
            this.btnLog.TabIndex = 3;
            this.btnLog.Text = "LOG记录";
            this.btnLog.UseVisualStyleBackColor = false;
            this.btnLog.Click += new System.EventHandler(this.btnLog_Click);
            // 
            // btnTest
            // 
            this.btnTest.BackColor = System.Drawing.Color.LimeGreen;
            this.btnTest.ForeColor = System.Drawing.Color.White;
            this.btnTest.Location = new System.Drawing.Point(931, 3);
            this.btnTest.Name = "btnTest";
            this.btnTest.Size = new System.Drawing.Size(226, 28);
            this.btnTest.TabIndex = 4;
            this.btnTest.Text = "TEST";
            this.btnTest.UseVisualStyleBackColor = false;
            this.btnTest.Click += new System.EventHandler(this.btnTest_Click);
            // 
            // btnAbout
            // 
            this.btnAbout.BackColor = System.Drawing.Color.LimeGreen;
            this.btnAbout.ForeColor = System.Drawing.Color.White;
            this.btnAbout.Location = new System.Drawing.Point(1163, 3);
            this.btnAbout.Name = "btnAbout";
            this.btnAbout.Size = new System.Drawing.Size(228, 28);
            this.btnAbout.TabIndex = 5;
            this.btnAbout.Text = "关于";
            this.btnAbout.UseVisualStyleBackColor = false;
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
            this.splitContainerMain.Size = new System.Drawing.Size(1394, 799);
            this.splitContainerMain.SplitterDistance = 1064;
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
            // 行高（V1.10 调整）：状态 90 + 送风机监视 150 + 操作 300 + LOG 剩余
            this.tableLayoutPanelRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 90F));
            this.tableLayoutPanelRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 150F));
            this.tableLayoutPanelRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 300F));
            this.tableLayoutPanelRight.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelRight.Size = new System.Drawing.Size(326, 799);
            this.tableLayoutPanelRight.TabIndex = 0;
            // 
            // groupBoxStatus
            // 
            this.groupBoxStatus.Controls.Add(this.lblRunStatus);
            this.groupBoxStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxStatus.Location = new System.Drawing.Point(3, 3);
            this.groupBoxStatus.Name = "groupBoxStatus";
            this.groupBoxStatus.Size = new System.Drawing.Size(320, 84);
            this.groupBoxStatus.TabIndex = 0;
            this.groupBoxStatus.TabStop = false;
            this.groupBoxStatus.Text = "运行状态";
            //
            // lblRunStatus
            //
            this.lblRunStatus.AutoSize = true;
            this.lblRunStatus.Font = new System.Drawing.Font("微软雅黑", 10F);
            this.lblRunStatus.Location = new System.Drawing.Point(15, 30);
            this.lblRunStatus.Name = "lblRunStatus";
            this.lblRunStatus.Size = new System.Drawing.Size(200, 20);
            this.lblRunStatus.TabIndex = 0;
            this.lblRunStatus.Text = "空闲";
            //
            // groupBoxMonitor
            //
            this.groupBoxMonitor.Controls.Add(this.lblLowerTempLabel);
            this.groupBoxMonitor.Controls.Add(this.txtLowerTemp);
            this.groupBoxMonitor.Controls.Add(this.lblUpperTempLabel);
            this.groupBoxMonitor.Controls.Add(this.txtUpperTemp);
            this.groupBoxMonitor.Controls.Add(this.lblSetTempLabel);
            this.groupBoxMonitor.Controls.Add(this.txtSetTemp);
            this.groupBoxMonitor.Controls.Add(this.lblFanStateLabel);
            this.groupBoxMonitor.Controls.Add(this.lblFanState);
            this.groupBoxMonitor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxMonitor.Location = new System.Drawing.Point(3, 93);
            this.groupBoxMonitor.Name = "groupBoxMonitor";
            this.groupBoxMonitor.Size = new System.Drawing.Size(320, 144);
            this.groupBoxMonitor.TabIndex = 1;
            this.groupBoxMonitor.TabStop = false;
            this.groupBoxMonitor.Text = "送风机监视";
            //
            // lblFanStateLabel
            //
            this.lblFanStateLabel.AutoSize = true;
            this.lblFanStateLabel.Location = new System.Drawing.Point(15, 24);
            this.lblFanStateLabel.Name = "lblFanStateLabel";
            this.lblFanStateLabel.Size = new System.Drawing.Size(65, 12);
            this.lblFanStateLabel.TabIndex = 6;
            this.lblFanStateLabel.Text = "送风机状态";
            //
            // lblFanState
            //
            this.lblFanState.AutoSize = true;
            this.lblFanState.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold);
            this.lblFanState.Location = new System.Drawing.Point(100, 21);
            this.lblFanState.Name = "lblFanState";
            this.lblFanState.Size = new System.Drawing.Size(200, 17);
            this.lblFanState.TabIndex = 7;
            this.lblFanState.Text = "未连接";
            //
            // lblSetTempLabel
            //
            this.lblSetTempLabel.AutoSize = true;
            this.lblSetTempLabel.Location = new System.Drawing.Point(15, 55);
            this.lblSetTempLabel.Name = "lblSetTempLabel";
            this.lblSetTempLabel.Size = new System.Drawing.Size(53, 12);
            this.lblSetTempLabel.TabIndex = 1;
            this.lblSetTempLabel.Text = "当前温度";
            //
            // txtSetTemp
            //
            this.txtSetTemp.Location = new System.Drawing.Point(100, 52);
            this.txtSetTemp.Name = "txtSetTemp";
            this.txtSetTemp.ReadOnly = true;
            this.txtSetTemp.Size = new System.Drawing.Size(200, 21);
            this.txtSetTemp.TabIndex = 0;
            this.txtSetTemp.Text = "---";
            //
            // lblUpperTempLabel
            //
            this.lblUpperTempLabel.AutoSize = true;
            this.lblUpperTempLabel.Location = new System.Drawing.Point(15, 81);
            this.lblUpperTempLabel.Name = "lblUpperTempLabel";
            this.lblUpperTempLabel.Size = new System.Drawing.Size(53, 12);
            this.lblUpperTempLabel.TabIndex = 3;
            this.lblUpperTempLabel.Text = "当前湿度";
            //
            // txtUpperTemp
            //
            this.txtUpperTemp.Location = new System.Drawing.Point(100, 78);
            this.txtUpperTemp.Name = "txtUpperTemp";
            this.txtUpperTemp.ReadOnly = true;
            this.txtUpperTemp.Size = new System.Drawing.Size(200, 21);
            this.txtUpperTemp.TabIndex = 2;
            this.txtUpperTemp.Text = "---";
            //
            // lblLowerTempLabel
            //
            this.lblLowerTempLabel.AutoSize = true;
            this.lblLowerTempLabel.Location = new System.Drawing.Point(15, 107);
            this.lblLowerTempLabel.Name = "lblLowerTempLabel";
            this.lblLowerTempLabel.Size = new System.Drawing.Size(53, 12);
            this.lblLowerTempLabel.TabIndex = 5;
            this.lblLowerTempLabel.Text = "设定温度";
            //
            // txtLowerTemp
            //
            this.txtLowerTemp.Location = new System.Drawing.Point(100, 104);
            this.txtLowerTemp.Name = "txtLowerTemp";
            this.txtLowerTemp.ReadOnly = true;
            this.txtLowerTemp.Size = new System.Drawing.Size(200, 21);
            this.txtLowerTemp.TabIndex = 4;
            this.txtLowerTemp.Text = "---";
            //
            // groupBoxOperation
            //
            this.groupBoxOperation.Controls.Add(this.btnStartRun);
            this.groupBoxOperation.Controls.Add(this.btnStopRun);
            this.groupBoxOperation.Controls.Add(this.btnResetAlarm);
            this.groupBoxOperation.Controls.Add(this.btnStopAll);
            this.groupBoxOperation.Controls.Add(this.btnInputLot);
            this.groupBoxOperation.Controls.Add(this.btnBatchRecipe);
            this.groupBoxOperation.Controls.Add(this.btnVacuum);
            this.groupBoxOperation.Controls.Add(this.btnFanStop);
            this.groupBoxOperation.Controls.Add(this.btnTemperatureControl);
            this.groupBoxOperation.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxOperation.Location = new System.Drawing.Point(3, 243);
            this.groupBoxOperation.Name = "groupBoxOperation";
            this.groupBoxOperation.Size = new System.Drawing.Size(320, 294);
            this.groupBoxOperation.TabIndex = 2;
            this.groupBoxOperation.TabStop = false;
            this.groupBoxOperation.Text = "操作";
            //
            // btnTemperatureControl（送风机定值启动）
            //
            this.btnTemperatureControl.BackColor = System.Drawing.Color.LimeGreen;
            this.btnTemperatureControl.ForeColor = System.Drawing.Color.White;
            this.btnTemperatureControl.Location = new System.Drawing.Point(15, 18);
            this.btnTemperatureControl.Name = "btnTemperatureControl";
            this.btnTemperatureControl.Size = new System.Drawing.Size(300, 28);
            this.btnTemperatureControl.TabIndex = 0;
            this.btnTemperatureControl.Text = "送风机定值启动";
            this.btnTemperatureControl.UseVisualStyleBackColor = false;
            this.btnTemperatureControl.Click += new System.EventHandler(this.btnTemperatureControl_Click);
            //
            // btnFanStop（送风机定值停止）
            //
            this.btnFanStop.BackColor = System.Drawing.Color.LimeGreen;
            this.btnFanStop.ForeColor = System.Drawing.Color.White;
            this.btnFanStop.Location = new System.Drawing.Point(15, 47);
            this.btnFanStop.Name = "btnFanStop";
            this.btnFanStop.Size = new System.Drawing.Size(300, 28);
            this.btnFanStop.TabIndex = 5;
            this.btnFanStop.Text = "送风机定值停止";
            this.btnFanStop.UseVisualStyleBackColor = false;
            this.btnFanStop.Click += new System.EventHandler(this.btnFanStop_Click);
            //
            // btnVacuum（开启真空）
            //
            this.btnVacuum.BackColor = System.Drawing.SystemColors.Control;
            this.btnVacuum.Location = new System.Drawing.Point(15, 76);
            this.btnVacuum.Name = "btnVacuum";
            this.btnVacuum.Size = new System.Drawing.Size(300, 28);
            this.btnVacuum.TabIndex = 1;
            this.btnVacuum.Text = "开启真空（选中台）";
            this.btnVacuum.UseVisualStyleBackColor = false;
            this.btnVacuum.Click += new System.EventHandler(this.btnVacuum_Click);
            //
            // btnStartRun（启动运行）
            //
            this.btnStartRun.BackColor = System.Drawing.Color.DodgerBlue;
            this.btnStartRun.ForeColor = System.Drawing.Color.White;
            this.btnStartRun.Location = new System.Drawing.Point(15, 105);
            this.btnStartRun.Name = "btnStartRun";
            this.btnStartRun.Size = new System.Drawing.Size(300, 28);
            this.btnStartRun.TabIndex = 4;
            this.btnStartRun.Text = "启动运行（选中台）";
            this.btnStartRun.UseVisualStyleBackColor = false;
            this.btnStartRun.Click += new System.EventHandler(this.btnStartRun_Click);
            //
            // btnStopRun（停止运行）
            //
            this.btnStopRun.BackColor = System.Drawing.SystemColors.Control;
            this.btnStopRun.Location = new System.Drawing.Point(15, 134);
            this.btnStopRun.Name = "btnStopRun";
            this.btnStopRun.Size = new System.Drawing.Size(300, 28);
            this.btnStopRun.TabIndex = 6;
            this.btnStopRun.Text = "停止运行（选中台）";
            this.btnStopRun.UseVisualStyleBackColor = false;
            this.btnStopRun.Click += new System.EventHandler(this.btnStopRun_Click);
            //
            // btnResetAlarm（报警复位）
            //
            this.btnResetAlarm.BackColor = System.Drawing.SystemColors.Control;
            this.btnResetAlarm.Location = new System.Drawing.Point(15, 163);
            this.btnResetAlarm.Name = "btnResetAlarm";
            this.btnResetAlarm.Size = new System.Drawing.Size(300, 28);
            this.btnResetAlarm.TabIndex = 7;
            this.btnResetAlarm.Text = "报警复位（选中台）";
            this.btnResetAlarm.UseVisualStyleBackColor = false;
            this.btnResetAlarm.Click += new System.EventHandler(this.btnResetAlarm_Click);
            //
            // btnStopAll（全部停止/急停）
            //
            this.btnStopAll.BackColor = System.Drawing.Color.Crimson;
            this.btnStopAll.ForeColor = System.Drawing.Color.White;
            this.btnStopAll.Location = new System.Drawing.Point(15, 192);
            this.btnStopAll.Name = "btnStopAll";
            this.btnStopAll.Size = new System.Drawing.Size(300, 28);
            this.btnStopAll.TabIndex = 8;
            this.btnStopAll.Text = "全部停止（急停）";
            this.btnStopAll.UseVisualStyleBackColor = false;
            this.btnStopAll.Click += new System.EventHandler(this.btnStopAll_Click);
            //
            // btnBatchRecipe（批量设置配方）
            //
            this.btnBatchRecipe.BackColor = System.Drawing.Color.LimeGreen;
            this.btnBatchRecipe.ForeColor = System.Drawing.Color.White;
            this.btnBatchRecipe.Location = new System.Drawing.Point(15, 221);
            this.btnBatchRecipe.Name = "btnBatchRecipe";
            this.btnBatchRecipe.Size = new System.Drawing.Size(300, 28);
            this.btnBatchRecipe.TabIndex = 2;
            this.btnBatchRecipe.Text = "批量设置配方";
            this.btnBatchRecipe.UseVisualStyleBackColor = false;
            this.btnBatchRecipe.Click += new System.EventHandler(this.btnBatchRecipe_Click);
            //
            // btnInputLot（录入批号）
            //
            this.btnInputLot.BackColor = System.Drawing.Color.LimeGreen;
            this.btnInputLot.ForeColor = System.Drawing.Color.White;
            this.btnInputLot.Location = new System.Drawing.Point(15, 250);
            this.btnInputLot.Name = "btnInputLot";
            this.btnInputLot.Size = new System.Drawing.Size(300, 28);
            this.btnInputLot.TabIndex = 3;
            this.btnInputLot.Text = "录入批号";
            this.btnInputLot.UseVisualStyleBackColor = false;
            this.btnInputLot.Click += new System.EventHandler(this.btnInputLot_Click);
            // 
            // groupBoxLog
            // 
            this.groupBoxLog.Controls.Add(this.txtLog);
            this.groupBoxLog.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxLog.Location = new System.Drawing.Point(3, 543);
            this.groupBoxLog.Name = "groupBoxLog";
            this.groupBoxLog.Size = new System.Drawing.Size(320, 253);
            this.groupBoxLog.TabIndex = 3;
            this.groupBoxLog.TabStop = false;
            this.groupBoxLog.Text = "LOG";
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
            this.txtLog.Size = new System.Drawing.Size(314, 373);
            this.txtLog.TabIndex = 0;
            // 
            // statusStripMain
            // 
            this.statusStripMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabelDeviceCount,
            this.toolStripStatusLabelInterval,
            this.toolStripStatusLabelTesting,
            this.toolStripStatusLabelOnline,
            this.toolStripStatusLabelTime});
            this.statusStripMain.Location = new System.Drawing.Point(0, 878);
            this.statusStripMain.Name = "statusStripMain";
            this.statusStripMain.Size = new System.Drawing.Size(1400, 22);
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
            // toolStripStatusLabelTesting（V1.10：测试中台数）
            //
            this.toolStripStatusLabelTesting.Name = "toolStripStatusLabelTesting";
            this.toolStripStatusLabelTesting.Size = new System.Drawing.Size(80, 17);
            this.toolStripStatusLabelTesting.Text = "测试中: 0";
            //
            // toolStripStatusLabelOnline（V1.10：在线台数）
            //
            this.toolStripStatusLabelOnline.Name = "toolStripStatusLabelOnline";
            this.toolStripStatusLabelOnline.Size = new System.Drawing.Size(90, 17);
            this.toolStripStatusLabelOnline.Text = "在线: 0/72";
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
            this.ClientSize = new System.Drawing.Size(1400, 900);
            this.Controls.Add(this.rootScrollPanel);
            this.MinimumSize = new System.Drawing.Size(800, 600);
            this.Name = "MainForm";
            this.Text = "老化测试系统V1.15";
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
        /// <summary>系统标题标签</summary>
        private System.Windows.Forms.Label lblTitle;
        /// <summary>当前操作权限标签</summary>
        private System.Windows.Forms.Label lblPermission;
        /// <summary>"PLC连接状态:"标签</summary>
        private System.Windows.Forms.Label lblPlcStatusLabel;
        /// <summary>PLC连接状态值标签</summary>
        private System.Windows.Forms.Label lblPlcStatus;
        /// <summary>"送风机运行状态:"标签（V1.10）</summary>
        private System.Windows.Forms.Label lblFanStateLabel;
        /// <summary>送风机运行状态值标签（V1.10，绿色=运行中/灰=停止/红=离线）</summary>
        private System.Windows.Forms.Label lblFanState;
        /// <summary>菜单按钮栏容器</summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelMenu;
        /// <summary>用户权限按钮</summary>
        private System.Windows.Forms.Button btnUserPermission;
        /// <summary>通信设置按钮</summary>
        private System.Windows.Forms.Button btnCommunication;
        /// <summary>参数设置按钮</summary>
        private System.Windows.Forms.Button btnParameter;
        /// <summary>LOG记录按钮</summary>
        private System.Windows.Forms.Button btnLog;
        /// <summary>TEST按钮</summary>
        private System.Windows.Forms.Button btnTest;
        /// <summary>关于按钮</summary>
        private System.Windows.Forms.Button btnAbout;
        /// <summary>中间分割容器（左:气压表 右:操作面板）</summary>
        private System.Windows.Forms.SplitContainer splitContainerMain;
        /// <summary>右侧布局容器</summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelRight;
        /// <summary>运行状态分组</summary>
        private System.Windows.Forms.GroupBox groupBoxStatus;
        /// <summary>运行状态文本标签</summary>
        private System.Windows.Forms.Label lblRunStatus;
        /// <summary>监视分组（温度显示）</summary>
        private System.Windows.Forms.GroupBox groupBoxMonitor;
        /// <summary>"下部温度"标签</summary>
        private System.Windows.Forms.Label lblLowerTempLabel;
        /// <summary>下部温度值显示框</summary>
        private System.Windows.Forms.TextBox txtLowerTemp;
        /// <summary>"上部温度"标签</summary>
        private System.Windows.Forms.Label lblUpperTempLabel;
        /// <summary>上部温度值显示框</summary>
        private System.Windows.Forms.TextBox txtUpperTemp;
        /// <summary>"设置温度"标签</summary>
        private System.Windows.Forms.Label lblSetTempLabel;
        /// <summary>设置温度值显示框</summary>
        private System.Windows.Forms.TextBox txtSetTemp;
        /// <summary>操作分组（5个操作按钮）</summary>
        private System.Windows.Forms.GroupBox groupBoxOperation;
        /// <summary>启动运行按钮</summary>
        private System.Windows.Forms.Button btnStartRun;
        /// <summary>停止运行按钮（V1.10）</summary>
        private System.Windows.Forms.Button btnStopRun;
        /// <summary>报警复位按钮（V1.10）</summary>
        private System.Windows.Forms.Button btnResetAlarm;
        /// <summary>全部停止（急停）按钮（V1.10）</summary>
        private System.Windows.Forms.Button btnStopAll;
        /// <summary>录入批号按钮</summary>
        private System.Windows.Forms.Button btnInputLot;
        /// <summary>批量设置配方按钮</summary>
        private System.Windows.Forms.Button btnBatchRecipe;
        /// <summary>开启真空按钮</summary>
        private System.Windows.Forms.Button btnVacuum;
        /// <summary>送风机定值停止按钮（V1.10）</summary>
        private System.Windows.Forms.Button btnFanStop;
        /// <summary>送风机定值启动按钮（由原"温控操作"按钮改造，V1.10）</summary>
        private System.Windows.Forms.Button btnTemperatureControl;
        /// <summary>LOG日志分组</summary>
        private System.Windows.Forms.GroupBox groupBoxLog;
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
        /// <summary>当前时间状态标签</summary>
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelTime;
        /// <summary>时间更新定时器</summary>
        private System.Windows.Forms.Timer timerTime;
    }
}
