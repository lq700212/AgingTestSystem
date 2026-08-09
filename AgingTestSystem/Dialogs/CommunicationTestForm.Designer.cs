namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 通讯测试窗体 —— 设计器自动生成部分（SunnyUI 界面版）
    ///
    /// 【界面布局】（整体改为 SunnyUI 控件，风格与主程序/系统设置一致）
    /// ┌──────────────────────────────────────────────────┐
    /// │ [●]未连接        通讯测试（UIForm 标题栏）         │
    /// ├──────────────────────────────────────────────────┤
    /// │ pnlHeader（顶部状态条：LED 指示灯 + 连接状态）      │
    /// ├──────────────────────────────────────────────────┤
    /// │ tabControl（UITabControl，两个 UIPage 页）        │
    /// │ ┌────────────────────────────────────────────┐   │
    /// │ │ 负压开关测试：panelGridVacuum（9×8 圆形灯）  │   │
    /// │ │ 载台上电测试：panelGridPowerOn（9×8 圆形灯） │   │
    /// │ └────────────────────────────────────────────┘   │
    /// ├──────────────────────────────────────────────────┤
    /// │ pnlBottom：[连接测试][全部关闭][读取状态][一键遍历][关闭窗口]│
    /// │            txtLog（日志框，只读多行）                        │
    /// └──────────────────────────────────────────────────┘
    ///
    /// 说明：
    /// - 窗体基类由 System.Windows.Forms.Form 改为 Sunny.UI.UIForm，
    ///   启用 ShowTitle 显示 SunnyUI 风格的标题栏（蓝色主题）。
    /// - 顶部 pnlHeader 放置 UILedBulb 连接指示灯 + UILabel 连接状态，
    ///   连接成功/断开时由 CommunicationTestForm.cs 的 SetConnected 更新。
    /// - 两个 Tab 页内的 9×8 = 72 个圆形灯按钮由 CommunicationTestForm 的
    ///   ChannelGrid.BuildButtonGrid() 动态生成（每个 ChannelGrid 持有一个 UIPanel）。
    /// - 底部按钮全部为 Sunny.UI.UIButton，日志框为 Sunny.UI.UITextBox（只读多行）。
    /// </summary>
    partial class CommunicationTestForm
    {
        /// <summary>必需的设计器变量。</summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>释放所有正在使用的资源。</summary>
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
            this.pnlHeader = new Sunny.UI.UIPanel();
            this.lblHeaderHint = new Sunny.UI.UILabel();
            this.lblStatus = new Sunny.UI.UILabel();
            this.ledStatus = new Sunny.UI.UILedBulb();
            this.tabControl = new Sunny.UI.UITabControl();
            this.pageVacuum = new Sunny.UI.UIPage();
            this.panelGridVacuum = new Sunny.UI.UIPanel();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.pagePowerOn = new Sunny.UI.UIPage();
            this.panelGridPowerOn = new Sunny.UI.UIPanel();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.pnlBottom = new Sunny.UI.UIPanel();
            this.txtLog = new Sunny.UI.UITextBox();
            this.btnClose = new Sunny.UI.UIButton();
            this.btnSweep = new Sunny.UI.UIButton();
            this.btnReadStatus = new Sunny.UI.UIButton();
            this.btnAllOff = new Sunny.UI.UIButton();
            this.btnConnect = new Sunny.UI.UIButton();
            this.pnlHeader.SuspendLayout();
            this.tabControl.SuspendLayout();
            this.pageVacuum.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.pagePowerOn.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.pnlBottom.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlHeader
            // 
            this.pnlHeader.Controls.Add(this.lblHeaderHint);
            this.pnlHeader.Controls.Add(this.lblStatus);
            this.pnlHeader.Controls.Add(this.ledStatus);
            this.pnlHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlHeader.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(238)))), ((int)(((byte)(245)))), ((int)(((byte)(255)))));
            this.pnlHeader.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.pnlHeader.Location = new System.Drawing.Point(0, 35);
            this.pnlHeader.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.pnlHeader.MinimumSize = new System.Drawing.Size(1, 1);
            this.pnlHeader.Name = "pnlHeader";
            this.pnlHeader.Padding = new System.Windows.Forms.Padding(14, 0, 14, 0);
            this.pnlHeader.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)(214)))), ((int)(((byte)(229)))), ((int)(((byte)(255)))));
            this.pnlHeader.Size = new System.Drawing.Size(780, 40);
            this.pnlHeader.TabIndex = 0;
            this.pnlHeader.Text = null;
            this.pnlHeader.TextAlignment = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblHeaderHint
            // 
            this.lblHeaderHint.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblHeaderHint.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblHeaderHint.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(80)))), ((int)(((byte)(160)))));
            this.lblHeaderHint.Location = new System.Drawing.Point(240, 4);
            this.lblHeaderHint.Name = "lblHeaderHint";
            this.lblHeaderHint.Size = new System.Drawing.Size(526, 32);
            this.lblHeaderHint.TabIndex = 2;
            this.lblHeaderHint.Text = "现场调试用：点击圆形灯控制对应通道输出（ON=亮绿）；已做备用通道映射的通道点击时会提示实际输出通道。";
            this.lblHeaderHint.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // lblStatus
            // 
            this.lblStatus.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Bold);
            this.lblStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(80)))), ((int)(((byte)(160)))));
            this.lblStatus.Location = new System.Drawing.Point(44, 4);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(120, 32);
            this.lblStatus.TabIndex = 1;
            this.lblStatus.Text = "未连接";
            this.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // ledStatus
            // 
            this.ledStatus.Color = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(80)))), ((int)(((byte)(80)))));
            this.ledStatus.Location = new System.Drawing.Point(14, 8);
            this.ledStatus.Name = "ledStatus";
            this.ledStatus.On = false;
            this.ledStatus.Size = new System.Drawing.Size(24, 24);
            this.ledStatus.TabIndex = 0;
            // 
            // tabControl
            // 
            this.tabControl.Controls.Add(this.tabPage1);
            this.tabControl.Controls.Add(this.tabPage2);
            this.tabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl.DrawMode = System.Windows.Forms.TabDrawMode.OwnerDrawFixed;
            this.tabControl.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Bold);
            this.tabControl.ItemSize = new System.Drawing.Size(150, 40);
            this.tabControl.Location = new System.Drawing.Point(0, 75);
            this.tabControl.MainPage = "";
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(780, 713);
            this.tabControl.SizeMode = System.Windows.Forms.TabSizeMode.Fixed;
            this.tabControl.Style = Sunny.UI.UIStyle.Custom;
            this.tabControl.TabIndex = 1;
            this.tabControl.TabSelectedHighColor = System.Drawing.Color.Red;
            this.tabControl.TabSelectedHighColorSize = 3;
            this.tabControl.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.tabControl.SelectedIndexChanged += new System.EventHandler(this.tabControl_SelectedIndexChanged);
            // 
            // pageVacuum
            // 
            this.pageVacuum.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(243)))), ((int)(((byte)(249)))), ((int)(((byte)(255)))));
            this.pageVacuum.ClientSize = new System.Drawing.Size(780, 624);
            this.pageVacuum.ControlBoxCloseFillHoverColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(80)))), ((int)(((byte)(80)))));
            this.pageVacuum.Controls.Add(this.panelGridVacuum);
            this.pageVacuum.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pageVacuum.Font = new System.Drawing.Font("宋体", 12F);
            this.pageVacuum.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.pageVacuum.Frame = null;
            this.pageVacuum.ImageInterval = 6;
            this.pageVacuum.Location = new System.Drawing.Point(0, 0);
            this.pageVacuum.Margin = new System.Windows.Forms.Padding(5);
            this.pageVacuum.MaximizeBox = false;
            this.pageVacuum.MinimizeBox = false;
            this.pageVacuum.Name = "pageVacuum";
            this.pageVacuum.Padding = new System.Windows.Forms.Padding(3, 0, 3, 3);
            this.pageVacuum.PageGuid = new System.Guid("1ff203a2-97a5-45ee-8428-ceef5ada87e4");
            this.pageVacuum.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(160)))), ((int)(((byte)(255)))));
            this.pageVacuum.ShowIcon = false;
            this.pageVacuum.ShowInTaskbar = false;
            this.pageVacuum.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.pageVacuum.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.pageVacuum.Style = Sunny.UI.UIStyle.Custom;
            this.pageVacuum.TabPage = this.tabPage1;
            this.pageVacuum.Text = "负压开关测试";
            this.pageVacuum.TitleFont = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.pageVacuum.Visible = false;
            // 
            // panelGridVacuum
            // 
            this.panelGridVacuum.BackColor = System.Drawing.Color.White;
            this.panelGridVacuum.FillColor = System.Drawing.Color.White;
            this.panelGridVacuum.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.panelGridVacuum.Location = new System.Drawing.Point(3, 3);
            this.panelGridVacuum.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.panelGridVacuum.MinimumSize = new System.Drawing.Size(1, 1);
            this.panelGridVacuum.Name = "panelGridVacuum";
            this.panelGridVacuum.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(234)))), ((int)(((byte)(240)))));
            this.panelGridVacuum.Size = new System.Drawing.Size(680, 660);
            this.panelGridVacuum.TabIndex = 0;
            this.panelGridVacuum.Text = null;
            this.panelGridVacuum.TextAlignment = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.pageVacuum);
            this.tabPage1.Location = new System.Drawing.Point(0, 40);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Size = new System.Drawing.Size(780, 624);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "负压开关测试";
            this.tabPage1.Visible = false;
            // 
            // pagePowerOn
            // 
            this.pagePowerOn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(243)))), ((int)(((byte)(249)))), ((int)(((byte)(255)))));
            this.pagePowerOn.ClientSize = new System.Drawing.Size(780, 673);
            this.pagePowerOn.ControlBoxCloseFillHoverColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(80)))), ((int)(((byte)(80)))));
            this.pagePowerOn.Controls.Add(this.panelGridPowerOn);
            this.pagePowerOn.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pagePowerOn.Font = new System.Drawing.Font("宋体", 12F);
            this.pagePowerOn.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.pagePowerOn.Frame = null;
            this.pagePowerOn.ImageInterval = 6;
            this.pagePowerOn.Location = new System.Drawing.Point(0, 0);
            this.pagePowerOn.Margin = new System.Windows.Forms.Padding(5);
            this.pagePowerOn.MaximizeBox = false;
            this.pagePowerOn.MinimizeBox = false;
            this.pagePowerOn.Name = "pagePowerOn";
            this.pagePowerOn.Padding = new System.Windows.Forms.Padding(3, 0, 3, 3);
            this.pagePowerOn.PageGuid = new System.Guid("8a10d5ca-616b-4e7e-9bfe-eeb31f4118fa");
            this.pagePowerOn.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(160)))), ((int)(((byte)(255)))));
            this.pagePowerOn.ShowIcon = false;
            this.pagePowerOn.ShowInTaskbar = false;
            this.pagePowerOn.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.pagePowerOn.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.pagePowerOn.Style = Sunny.UI.UIStyle.Custom;
            this.pagePowerOn.TabPage = this.tabPage2;
            this.pagePowerOn.Text = "载台上电测试";
            this.pagePowerOn.TitleFont = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.pagePowerOn.Visible = false;
            // 
            // panelGridPowerOn
            // 
            this.panelGridPowerOn.BackColor = System.Drawing.Color.White;
            this.panelGridPowerOn.FillColor = System.Drawing.Color.White;
            this.panelGridPowerOn.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.panelGridPowerOn.Location = new System.Drawing.Point(3, 3);
            this.panelGridPowerOn.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.panelGridPowerOn.MinimumSize = new System.Drawing.Size(1, 1);
            this.panelGridPowerOn.Name = "panelGridPowerOn";
            this.panelGridPowerOn.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(234)))), ((int)(((byte)(240)))));
            this.panelGridPowerOn.Size = new System.Drawing.Size(680, 660);
            this.panelGridPowerOn.TabIndex = 0;
            this.panelGridPowerOn.Text = null;
            this.panelGridPowerOn.TextAlignment = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.pagePowerOn);
            this.tabPage2.Location = new System.Drawing.Point(0, 40);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Size = new System.Drawing.Size(780, 673);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "载台上电测试";
            this.tabPage2.Visible = false;
            // 
            // pnlBottom
            // 
            this.pnlBottom.Controls.Add(this.txtLog);
            this.pnlBottom.Controls.Add(this.btnClose);
            this.pnlBottom.Controls.Add(this.btnSweep);
            this.pnlBottom.Controls.Add(this.btnReadStatus);
            this.pnlBottom.Controls.Add(this.btnAllOff);
            this.pnlBottom.Controls.Add(this.btnConnect);
            this.pnlBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlBottom.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(250)))), ((int)(((byte)(252)))));
            this.pnlBottom.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.pnlBottom.Location = new System.Drawing.Point(0, 788);
            this.pnlBottom.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.pnlBottom.MinimumSize = new System.Drawing.Size(1, 1);
            this.pnlBottom.Name = "pnlBottom";
            this.pnlBottom.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(250)))), ((int)(((byte)(252)))));
            this.pnlBottom.Size = new System.Drawing.Size(780, 312);
            this.pnlBottom.TabIndex = 2;
            this.pnlBottom.Text = null;
            this.pnlBottom.TextAlignment = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // txtLog
            // 
            this.txtLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtLog.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtLog.FillReadOnlyColor = System.Drawing.Color.White;
            this.txtLog.Font = new System.Drawing.Font("微软雅黑", 9.5F);
            this.txtLog.Location = new System.Drawing.Point(14, 73);
            this.txtLog.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.txtLog.MinimumSize = new System.Drawing.Size(1, 16);
            this.txtLog.Multiline = true;
            this.txtLog.Name = "txtLog";
            this.txtLog.Padding = new System.Windows.Forms.Padding(5);
            this.txtLog.ReadOnly = true;
            this.txtLog.ShowScrollBar = true;
            this.txtLog.ShowText = false;
            this.txtLog.Size = new System.Drawing.Size(752, 231);
            this.txtLog.TabIndex = 4;
            this.txtLog.TextAlignment = System.Drawing.ContentAlignment.MiddleLeft;
            this.txtLog.Watermark = "操作日志将显示在这里";
            // 
            // btnClose
            // 
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnClose.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(140)))), ((int)(((byte)(140)))), ((int)(((byte)(140)))));
            this.btnClose.FillColor2 = System.Drawing.Color.FromArgb(((int)(((byte)(140)))), ((int)(((byte)(140)))), ((int)(((byte)(140)))));
            this.btnClose.FillHoverColor = System.Drawing.Color.FromArgb(((int)(((byte)(163)))), ((int)(((byte)(163)))), ((int)(((byte)(163)))));
            this.btnClose.FillPressColor = System.Drawing.Color.FromArgb(((int)(((byte)(112)))), ((int)(((byte)(112)))), ((int)(((byte)(112)))));
            this.btnClose.FillSelectedColor = System.Drawing.Color.FromArgb(((int)(((byte)(112)))), ((int)(((byte)(112)))), ((int)(((byte)(112)))));
            this.btnClose.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Bold);
            this.btnClose.LightColor = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(248)))), ((int)(((byte)(248)))));
            this.btnClose.Location = new System.Drawing.Point(626, 12);
            this.btnClose.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnClose.Name = "btnClose";
            this.btnClose.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)(140)))), ((int)(((byte)(140)))), ((int)(((byte)(140)))));
            this.btnClose.RectHoverColor = System.Drawing.Color.FromArgb(((int)(((byte)(163)))), ((int)(((byte)(163)))), ((int)(((byte)(163)))));
            this.btnClose.RectPressColor = System.Drawing.Color.FromArgb(((int)(((byte)(112)))), ((int)(((byte)(112)))), ((int)(((byte)(112)))));
            this.btnClose.RectSelectedColor = System.Drawing.Color.FromArgb(((int)(((byte)(112)))), ((int)(((byte)(112)))), ((int)(((byte)(112)))));
            this.btnClose.Size = new System.Drawing.Size(140, 38);
            this.btnClose.Style = Sunny.UI.UIStyle.Custom;
            this.btnClose.TabIndex = 3;
            this.btnClose.Text = "关闭窗口";
            this.btnClose.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // btnSweep
            // 
            this.btnSweep.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnSweep.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(102)))), ((int)(((byte)(58)))), ((int)(((byte)(183)))));
            this.btnSweep.FillColor2 = System.Drawing.Color.FromArgb(((int)(((byte)(102)))), ((int)(((byte)(58)))), ((int)(((byte)(183)))));
            this.btnSweep.FillHoverColor = System.Drawing.Color.FromArgb(((int)(((byte)(133)))), ((int)(((byte)(97)))), ((int)(((byte)(198)))));
            this.btnSweep.FillPressColor = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(46)))), ((int)(((byte)(147)))));
            this.btnSweep.FillSelectedColor = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(46)))), ((int)(((byte)(147)))));
            this.btnSweep.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Bold);
            this.btnSweep.LightColor = System.Drawing.Color.FromArgb(((int)(((byte)(244)))), ((int)(((byte)(242)))), ((int)(((byte)(251)))));
            this.btnSweep.Location = new System.Drawing.Point(470, 12);
            this.btnSweep.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnSweep.Name = "btnSweep";
            this.btnSweep.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)(102)))), ((int)(((byte)(58)))), ((int)(((byte)(183)))));
            this.btnSweep.RectHoverColor = System.Drawing.Color.FromArgb(((int)(((byte)(133)))), ((int)(((byte)(97)))), ((int)(((byte)(198)))));
            this.btnSweep.RectPressColor = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(46)))), ((int)(((byte)(147)))));
            this.btnSweep.RectSelectedColor = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(46)))), ((int)(((byte)(147)))));
            this.btnSweep.Size = new System.Drawing.Size(140, 38);
            this.btnSweep.Style = Sunny.UI.UIStyle.Custom;
            this.btnSweep.TabIndex = 5;
            this.btnSweep.Text = "一键遍历";
            this.btnSweep.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnSweep.Click += new System.EventHandler(this.btnSweep_Click);
            // 
            // btnReadStatus
            // 
            this.btnReadStatus.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnReadStatus.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Bold);
            this.btnReadStatus.Location = new System.Drawing.Point(318, 12);
            this.btnReadStatus.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnReadStatus.Name = "btnReadStatus";
            this.btnReadStatus.Size = new System.Drawing.Size(140, 38);
            this.btnReadStatus.Style = Sunny.UI.UIStyle.Custom;
            this.btnReadStatus.TabIndex = 2;
            this.btnReadStatus.Text = "读取状态";
            this.btnReadStatus.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnReadStatus.Click += new System.EventHandler(this.btnReadStatus_Click);
            // 
            // btnAllOff
            // 
            this.btnAllOff.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnAllOff.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(155)))), ((int)(((byte)(40)))));
            this.btnAllOff.FillColor2 = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(155)))), ((int)(((byte)(40)))));
            this.btnAllOff.FillHoverColor = System.Drawing.Color.FromArgb(((int)(((byte)(227)))), ((int)(((byte)(175)))), ((int)(((byte)(83)))));
            this.btnAllOff.FillPressColor = System.Drawing.Color.FromArgb(((int)(((byte)(176)))), ((int)(((byte)(124)))), ((int)(((byte)(32)))));
            this.btnAllOff.FillSelectedColor = System.Drawing.Color.FromArgb(((int)(((byte)(176)))), ((int)(((byte)(124)))), ((int)(((byte)(32)))));
            this.btnAllOff.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Bold);
            this.btnAllOff.LightColor = System.Drawing.Color.FromArgb(((int)(((byte)(253)))), ((int)(((byte)(249)))), ((int)(((byte)(241)))));
            this.btnAllOff.Location = new System.Drawing.Point(166, 12);
            this.btnAllOff.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnAllOff.Name = "btnAllOff";
            this.btnAllOff.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(155)))), ((int)(((byte)(40)))));
            this.btnAllOff.RectHoverColor = System.Drawing.Color.FromArgb(((int)(((byte)(227)))), ((int)(((byte)(175)))), ((int)(((byte)(83)))));
            this.btnAllOff.RectPressColor = System.Drawing.Color.FromArgb(((int)(((byte)(176)))), ((int)(((byte)(124)))), ((int)(((byte)(32)))));
            this.btnAllOff.RectSelectedColor = System.Drawing.Color.FromArgb(((int)(((byte)(176)))), ((int)(((byte)(124)))), ((int)(((byte)(32)))));
            this.btnAllOff.Size = new System.Drawing.Size(140, 38);
            this.btnAllOff.Style = Sunny.UI.UIStyle.Custom;
            this.btnAllOff.TabIndex = 1;
            this.btnAllOff.Text = "全部关闭";
            this.btnAllOff.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnAllOff.Click += new System.EventHandler(this.btnAllOff_Click);
            // 
            // btnConnect
            // 
            this.btnConnect.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnConnect.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(110)))), ((int)(((byte)(190)))), ((int)(((byte)(40)))));
            this.btnConnect.FillColor2 = System.Drawing.Color.FromArgb(((int)(((byte)(110)))), ((int)(((byte)(190)))), ((int)(((byte)(40)))));
            this.btnConnect.FillHoverColor = System.Drawing.Color.FromArgb(((int)(((byte)(139)))), ((int)(((byte)(203)))), ((int)(((byte)(83)))));
            this.btnConnect.FillPressColor = System.Drawing.Color.FromArgb(((int)(((byte)(88)))), ((int)(((byte)(152)))), ((int)(((byte)(32)))));
            this.btnConnect.FillSelectedColor = System.Drawing.Color.FromArgb(((int)(((byte)(88)))), ((int)(((byte)(152)))), ((int)(((byte)(32)))));
            this.btnConnect.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Bold);
            this.btnConnect.LightColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(251)))), ((int)(((byte)(241)))));
            this.btnConnect.Location = new System.Drawing.Point(14, 12);
            this.btnConnect.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)(110)))), ((int)(((byte)(190)))), ((int)(((byte)(40)))));
            this.btnConnect.RectHoverColor = System.Drawing.Color.FromArgb(((int)(((byte)(139)))), ((int)(((byte)(203)))), ((int)(((byte)(83)))));
            this.btnConnect.RectPressColor = System.Drawing.Color.FromArgb(((int)(((byte)(88)))), ((int)(((byte)(152)))), ((int)(((byte)(32)))));
            this.btnConnect.RectSelectedColor = System.Drawing.Color.FromArgb(((int)(((byte)(88)))), ((int)(((byte)(152)))), ((int)(((byte)(32)))));
            this.btnConnect.Size = new System.Drawing.Size(140, 38);
            this.btnConnect.Style = Sunny.UI.UIStyle.Custom;
            this.btnConnect.TabIndex = 0;
            this.btnConnect.Text = "连接测试";
            this.btnConnect.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnConnect.Click += new System.EventHandler(this.btnConnect_Click);
            // 
            // CommunicationTestForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(780, 1100);
            this.Controls.Add(this.tabControl);
            this.Controls.Add(this.pnlHeader);
            this.Controls.Add(this.pnlBottom);
            this.EscClose = true;
            this.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.MinimumSize = new System.Drawing.Size(780, 1000);
            this.Name = "CommunicationTestForm";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Style = Sunny.UI.UIStyle.Custom;
            this.Text = "通讯测试";
            this.TitleFont = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Bold);
            this.ZoomScaleRect = new System.Drawing.Rectangle(15, 15, 780, 1000);
            this.pnlHeader.ResumeLayout(false);
            this.tabControl.ResumeLayout(false);
            this.pageVacuum.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.pagePowerOn.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            this.pnlBottom.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private Sunny.UI.UIPanel pnlHeader;
        private Sunny.UI.UILedBulb ledStatus;
        private Sunny.UI.UILabel lblStatus;
        private Sunny.UI.UILabel lblHeaderHint;
        private Sunny.UI.UITabControl tabControl;
        private Sunny.UI.UIPage pageVacuum;
        private Sunny.UI.UIPanel panelGridVacuum;
        private Sunny.UI.UIPage pagePowerOn;
        private Sunny.UI.UIPanel panelGridPowerOn;
        private Sunny.UI.UIPanel pnlBottom;
        private Sunny.UI.UIButton btnConnect;
        private Sunny.UI.UIButton btnAllOff;
        private Sunny.UI.UIButton btnReadStatus;
        private Sunny.UI.UIButton btnSweep;
        private Sunny.UI.UIButton btnClose;
        private Sunny.UI.UITextBox txtLog;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
    }
}
