namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 冷却送风机通讯测试窗体 —— 设计器自动生成部分（SunnyUI 界面版）
    ///
    /// 【界面布局】（与通讯测试窗体 CommunicationTestForm 一致，SunnyUI 控件、蓝色主题）
    /// ┌──────────────────────────────────────────────────┐
    /// │ [●]未连接        冷却送风机通讯测试（UIForm 标题） │
    /// ├──────────────────────────────────────────────────┤
    /// │ pnlHeader（顶部状态条：LED 指示灯 + 连接状态）      │
    /// ├──────────────────────────────────────────────────┤
    /// │ pnlConn（连接参数：送风机 IP / 端口 + [连接测试]）  │
    /// ├──────────────────────────────────────────────────┤
    /// │ pnlStatus（实时状态 + [定值启动][定值停止][读取状态]）│
    /// ├──────────────────────────────────────────────────┤
    /// │ txtLog（日志框，只读多行，Dock Bottom）            │
    /// └──────────────────────────────────────────────────┘
    ///
    /// 说明：
    /// - 窗体基类为 Sunny.UI.UIForm（蓝色标题栏，ShowTitle），与通讯测试窗体一致。
    /// - **共享连接（V1.23 重构）**：本窗体不自己建 TCP 连接，复用主程序 DeviceManager
    ///   拥有的那一条送风机连接（FanControllerClient）。连接参数（IP/端口）只读显示
    ///   主程序配置；连接/读状态/启停都走 DeviceManager 的共享接口，不再自建第二路连接。
    /// - 连接成功后每 2s 从主程序缓存读一次状态刷新各状态标签（零额外报文，不阻塞界面）。
    /// - 定值启动/定值停止 通过 DeviceManager.StartFan()/StopFan() 在后台线程执行。
    /// </summary>
    partial class FanTestForm
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
            this.pnlConn = new Sunny.UI.UIPanel();
            this.btnConnect = new Sunny.UI.UIButton();
            this.txtPort = new Sunny.UI.UITextBox();
            this.lblPortLabel = new Sunny.UI.UILabel();
            this.txtIp = new Sunny.UI.UITextBox();
            this.lblIpLabel = new Sunny.UI.UILabel();
            this.pnlStatus = new Sunny.UI.UIPanel();
            this.btnRefresh = new Sunny.UI.UIButton();
            this.btnStop = new Sunny.UI.UIButton();
            this.btnStartFixed = new Sunny.UI.UIButton();
            this.lblHumSet = new Sunny.UI.UILabel();
            this.lblHumSetLabel = new Sunny.UI.UILabel();
            this.lblTempSet = new Sunny.UI.UILabel();
            this.lblTempSetLabel = new Sunny.UI.UILabel();
            this.lblHumidity = new Sunny.UI.UILabel();
            this.lblHumLabel = new Sunny.UI.UILabel();
            this.lblTemp = new Sunny.UI.UILabel();
            this.lblTempLabel = new Sunny.UI.UILabel();
            this.lblRunState = new Sunny.UI.UILabel();
            this.lblRunLabel = new Sunny.UI.UILabel();
            this.txtLog = new Sunny.UI.UITextBox();
            this.pnlHeader.SuspendLayout();
            this.pnlConn.SuspendLayout();
            this.pnlStatus.SuspendLayout();
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
            this.pnlHeader.Size = new System.Drawing.Size(700, 40);
            this.pnlHeader.TabIndex = 0;
            this.pnlHeader.Text = null;
            this.pnlHeader.TextAlignment = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // lblHeaderHint
            // 
            this.lblHeaderHint.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblHeaderHint.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.lblHeaderHint.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(80)))), ((int)(((byte)(160)))));
            this.lblHeaderHint.Location = new System.Drawing.Point(280, 4);
            this.lblHeaderHint.Name = "lblHeaderHint";
            this.lblHeaderHint.Size = new System.Drawing.Size(406, 32);
            this.lblHeaderHint.TabIndex = 2;
            this.lblHeaderHint.Text = "现场调试用：复用主程序共享连接，连接后每 2s 自动读取温度/湿度/运行状态；可手动定值启动/停止。";
            this.lblHeaderHint.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // lblStatus
            // 
            this.lblStatus.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Bold);
            this.lblStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(80)))), ((int)(((byte)(160)))));
            this.lblStatus.Location = new System.Drawing.Point(44, 4);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(140, 32);
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
            // pnlConn
            // 
            this.pnlConn.Controls.Add(this.btnConnect);
            this.pnlConn.Controls.Add(this.txtPort);
            this.pnlConn.Controls.Add(this.lblPortLabel);
            this.pnlConn.Controls.Add(this.txtIp);
            this.pnlConn.Controls.Add(this.lblIpLabel);
            this.pnlConn.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlConn.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(250)))), ((int)(((byte)(252)))));
            this.pnlConn.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.pnlConn.Location = new System.Drawing.Point(0, 75);
            this.pnlConn.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.pnlConn.MinimumSize = new System.Drawing.Size(1, 1);
            this.pnlConn.Name = "pnlConn";
            this.pnlConn.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(234)))), ((int)(((byte)(240)))));
            this.pnlConn.Size = new System.Drawing.Size(700, 64);
            this.pnlConn.TabIndex = 1;
            this.pnlConn.Text = null;
            this.pnlConn.TextAlignment = System.Drawing.ContentAlignment.MiddleCenter;
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
            this.btnConnect.Location = new System.Drawing.Point(456, 14);
            this.btnConnect.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)(110)))), ((int)(((byte)(190)))), ((int)(((byte)(40)))));
            this.btnConnect.RectHoverColor = System.Drawing.Color.FromArgb(((int)(((byte)(139)))), ((int)(((byte)(203)))), ((int)(((byte)(83)))));
            this.btnConnect.RectPressColor = System.Drawing.Color.FromArgb(((int)(((byte)(88)))), ((int)(((byte)(152)))), ((int)(((byte)(32)))));
            this.btnConnect.RectSelectedColor = System.Drawing.Color.FromArgb(((int)(((byte)(88)))), ((int)(((byte)(152)))), ((int)(((byte)(32)))));
            this.btnConnect.Size = new System.Drawing.Size(140, 36);
            this.btnConnect.Style = Sunny.UI.UIStyle.Custom;
            this.btnConnect.TabIndex = 4;
            this.btnConnect.Text = "连接测试";
            this.btnConnect.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnConnect.Click += new System.EventHandler(this.btnConnect_Click);
            // 
            // txtPort
            // 
            this.txtPort.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtPort.DoubleValue = 50000D;
            this.txtPort.Font = new System.Drawing.Font("微软雅黑", 10.5F);
            this.txtPort.IntValue = 50000;
            this.txtPort.Location = new System.Drawing.Point(356, 16);
            this.txtPort.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.txtPort.MinimumSize = new System.Drawing.Size(1, 16);
            this.txtPort.Name = "txtPort";
            this.txtPort.Padding = new System.Windows.Forms.Padding(5);
            this.txtPort.ReadOnly = true;
            this.txtPort.ShowText = false;
            this.txtPort.Size = new System.Drawing.Size(80, 32);
            this.txtPort.TabIndex = 3;
            this.txtPort.Text = "50000";
            this.txtPort.TextAlignment = System.Drawing.ContentAlignment.MiddleLeft;
            this.txtPort.Watermark = "";
            // 
            // lblPortLabel
            // 
            this.lblPortLabel.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblPortLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblPortLabel.Location = new System.Drawing.Point(300, 18);
            this.lblPortLabel.Name = "lblPortLabel";
            this.lblPortLabel.Size = new System.Drawing.Size(56, 28);
            this.lblPortLabel.TabIndex = 2;
            this.lblPortLabel.Text = "端口:";
            this.lblPortLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // txtIp
            // 
            this.txtIp.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtIp.Font = new System.Drawing.Font("微软雅黑", 10.5F);
            this.txtIp.Location = new System.Drawing.Point(106, 16);
            this.txtIp.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.txtIp.MinimumSize = new System.Drawing.Size(1, 16);
            this.txtIp.Name = "txtIp";
            this.txtIp.Padding = new System.Windows.Forms.Padding(5);
            this.txtIp.ReadOnly = true;
            this.txtIp.ShowText = false;
            this.txtIp.Size = new System.Drawing.Size(180, 32);
            this.txtIp.TabIndex = 1;
            this.txtIp.TextAlignment = System.Drawing.ContentAlignment.MiddleLeft;
            this.txtIp.Watermark = "";
            // 
            // lblIpLabel
            // 
            this.lblIpLabel.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblIpLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblIpLabel.Location = new System.Drawing.Point(16, 18);
            this.lblIpLabel.Name = "lblIpLabel";
            this.lblIpLabel.Size = new System.Drawing.Size(90, 28);
            this.lblIpLabel.TabIndex = 0;
            this.lblIpLabel.Text = "送风机 IP:";
            this.lblIpLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // pnlStatus
            // 
            this.pnlStatus.Controls.Add(this.btnRefresh);
            this.pnlStatus.Controls.Add(this.btnStop);
            this.pnlStatus.Controls.Add(this.btnStartFixed);
            this.pnlStatus.Controls.Add(this.lblHumSet);
            this.pnlStatus.Controls.Add(this.lblHumSetLabel);
            this.pnlStatus.Controls.Add(this.lblTempSet);
            this.pnlStatus.Controls.Add(this.lblTempSetLabel);
            this.pnlStatus.Controls.Add(this.lblHumidity);
            this.pnlStatus.Controls.Add(this.lblHumLabel);
            this.pnlStatus.Controls.Add(this.lblTemp);
            this.pnlStatus.Controls.Add(this.lblTempLabel);
            this.pnlStatus.Controls.Add(this.lblRunState);
            this.pnlStatus.Controls.Add(this.lblRunLabel);
            this.pnlStatus.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlStatus.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(252)))), ((int)(((byte)(253)))), ((int)(((byte)(255)))));
            this.pnlStatus.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.pnlStatus.Location = new System.Drawing.Point(0, 139);
            this.pnlStatus.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.pnlStatus.MinimumSize = new System.Drawing.Size(1, 1);
            this.pnlStatus.Name = "pnlStatus";
            this.pnlStatus.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(234)))), ((int)(((byte)(240)))));
            this.pnlStatus.Size = new System.Drawing.Size(700, 199);
            this.pnlStatus.TabIndex = 2;
            this.pnlStatus.Text = null;
            this.pnlStatus.TextAlignment = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // btnRefresh
            // 
            this.btnRefresh.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnRefresh.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Bold);
            this.btnRefresh.Location = new System.Drawing.Point(302, 132);
            this.btnRefresh.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(120, 36);
            this.btnRefresh.Style = Sunny.UI.UIStyle.Custom;
            this.btnRefresh.TabIndex = 12;
            this.btnRefresh.Text = "读取状态";
            this.btnRefresh.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);
            // 
            // btnStop
            // 
            this.btnStop.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnStop.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(155)))), ((int)(((byte)(40)))));
            this.btnStop.FillColor2 = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(155)))), ((int)(((byte)(40)))));
            this.btnStop.FillHoverColor = System.Drawing.Color.FromArgb(((int)(((byte)(227)))), ((int)(((byte)(175)))), ((int)(((byte)(83)))));
            this.btnStop.FillPressColor = System.Drawing.Color.FromArgb(((int)(((byte)(176)))), ((int)(((byte)(124)))), ((int)(((byte)(32)))));
            this.btnStop.FillSelectedColor = System.Drawing.Color.FromArgb(((int)(((byte)(176)))), ((int)(((byte)(124)))), ((int)(((byte)(32)))));
            this.btnStop.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Bold);
            this.btnStop.LightColor = System.Drawing.Color.FromArgb(((int)(((byte)(253)))), ((int)(((byte)(249)))), ((int)(((byte)(241)))));
            this.btnStop.Location = new System.Drawing.Point(158, 132);
            this.btnStop.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnStop.Name = "btnStop";
            this.btnStop.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(155)))), ((int)(((byte)(40)))));
            this.btnStop.RectHoverColor = System.Drawing.Color.FromArgb(((int)(((byte)(227)))), ((int)(((byte)(175)))), ((int)(((byte)(83)))));
            this.btnStop.RectPressColor = System.Drawing.Color.FromArgb(((int)(((byte)(176)))), ((int)(((byte)(124)))), ((int)(((byte)(32)))));
            this.btnStop.RectSelectedColor = System.Drawing.Color.FromArgb(((int)(((byte)(176)))), ((int)(((byte)(124)))), ((int)(((byte)(32)))));
            this.btnStop.Size = new System.Drawing.Size(130, 36);
            this.btnStop.Style = Sunny.UI.UIStyle.Custom;
            this.btnStop.TabIndex = 11;
            this.btnStop.Text = "定值停止";
            this.btnStop.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnStop.Click += new System.EventHandler(this.btnStop_Click);
            // 
            // btnStartFixed
            // 
            this.btnStartFixed.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnStartFixed.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(110)))), ((int)(((byte)(190)))), ((int)(((byte)(40)))));
            this.btnStartFixed.FillColor2 = System.Drawing.Color.FromArgb(((int)(((byte)(110)))), ((int)(((byte)(190)))), ((int)(((byte)(40)))));
            this.btnStartFixed.FillHoverColor = System.Drawing.Color.FromArgb(((int)(((byte)(139)))), ((int)(((byte)(203)))), ((int)(((byte)(83)))));
            this.btnStartFixed.FillPressColor = System.Drawing.Color.FromArgb(((int)(((byte)(88)))), ((int)(((byte)(152)))), ((int)(((byte)(32)))));
            this.btnStartFixed.FillSelectedColor = System.Drawing.Color.FromArgb(((int)(((byte)(88)))), ((int)(((byte)(152)))), ((int)(((byte)(32)))));
            this.btnStartFixed.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Bold);
            this.btnStartFixed.LightColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(251)))), ((int)(((byte)(241)))));
            this.btnStartFixed.Location = new System.Drawing.Point(14, 132);
            this.btnStartFixed.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnStartFixed.Name = "btnStartFixed";
            this.btnStartFixed.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)(110)))), ((int)(((byte)(190)))), ((int)(((byte)(40)))));
            this.btnStartFixed.RectHoverColor = System.Drawing.Color.FromArgb(((int)(((byte)(139)))), ((int)(((byte)(203)))), ((int)(((byte)(83)))));
            this.btnStartFixed.RectPressColor = System.Drawing.Color.FromArgb(((int)(((byte)(88)))), ((int)(((byte)(152)))), ((int)(((byte)(32)))));
            this.btnStartFixed.RectSelectedColor = System.Drawing.Color.FromArgb(((int)(((byte)(88)))), ((int)(((byte)(152)))), ((int)(((byte)(32)))));
            this.btnStartFixed.Size = new System.Drawing.Size(130, 36);
            this.btnStartFixed.Style = Sunny.UI.UIStyle.Custom;
            this.btnStartFixed.TabIndex = 10;
            this.btnStartFixed.Text = "定值启动";
            this.btnStartFixed.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnStartFixed.Click += new System.EventHandler(this.btnStartFixed_Click);
            // 
            // lblHumSet
            // 
            this.lblHumSet.Font = new System.Drawing.Font("微软雅黑", 10.5F);
            this.lblHumSet.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(80)))), ((int)(((byte)(160)))));
            this.lblHumSet.Location = new System.Drawing.Point(390, 92);
            this.lblHumSet.Name = "lblHumSet";
            this.lblHumSet.Size = new System.Drawing.Size(160, 28);
            this.lblHumSet.TabIndex = 9;
            this.lblHumSet.Text = "--";
            this.lblHumSet.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblHumSetLabel
            // 
            this.lblHumSetLabel.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblHumSetLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblHumSetLabel.Location = new System.Drawing.Point(300, 92);
            this.lblHumSetLabel.Name = "lblHumSetLabel";
            this.lblHumSetLabel.Size = new System.Drawing.Size(90, 28);
            this.lblHumSetLabel.TabIndex = 8;
            this.lblHumSetLabel.Text = "湿度设定:";
            this.lblHumSetLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblTempSet
            // 
            this.lblTempSet.Font = new System.Drawing.Font("微软雅黑", 10.5F);
            this.lblTempSet.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(80)))), ((int)(((byte)(160)))));
            this.lblTempSet.Location = new System.Drawing.Point(106, 92);
            this.lblTempSet.Name = "lblTempSet";
            this.lblTempSet.Size = new System.Drawing.Size(160, 28);
            this.lblTempSet.TabIndex = 7;
            this.lblTempSet.Text = "--";
            this.lblTempSet.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblTempSetLabel
            // 
            this.lblTempSetLabel.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblTempSetLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblTempSetLabel.Location = new System.Drawing.Point(16, 92);
            this.lblTempSetLabel.Name = "lblTempSetLabel";
            this.lblTempSetLabel.Size = new System.Drawing.Size(90, 28);
            this.lblTempSetLabel.TabIndex = 6;
            this.lblTempSetLabel.Text = "温度设定:";
            this.lblTempSetLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblHumidity
            // 
            this.lblHumidity.Font = new System.Drawing.Font("微软雅黑", 10.5F);
            this.lblHumidity.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(80)))), ((int)(((byte)(160)))));
            this.lblHumidity.Location = new System.Drawing.Point(390, 56);
            this.lblHumidity.Name = "lblHumidity";
            this.lblHumidity.Size = new System.Drawing.Size(160, 28);
            this.lblHumidity.TabIndex = 5;
            this.lblHumidity.Text = "--";
            this.lblHumidity.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblHumLabel
            // 
            this.lblHumLabel.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblHumLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblHumLabel.Location = new System.Drawing.Point(300, 56);
            this.lblHumLabel.Name = "lblHumLabel";
            this.lblHumLabel.Size = new System.Drawing.Size(90, 28);
            this.lblHumLabel.TabIndex = 4;
            this.lblHumLabel.Text = "当前湿度:";
            this.lblHumLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblTemp
            // 
            this.lblTemp.Font = new System.Drawing.Font("微软雅黑", 10.5F);
            this.lblTemp.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(80)))), ((int)(((byte)(160)))));
            this.lblTemp.Location = new System.Drawing.Point(106, 56);
            this.lblTemp.Name = "lblTemp";
            this.lblTemp.Size = new System.Drawing.Size(160, 28);
            this.lblTemp.TabIndex = 3;
            this.lblTemp.Text = "--";
            this.lblTemp.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblTempLabel
            // 
            this.lblTempLabel.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblTempLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblTempLabel.Location = new System.Drawing.Point(16, 56);
            this.lblTempLabel.Name = "lblTempLabel";
            this.lblTempLabel.Size = new System.Drawing.Size(90, 28);
            this.lblTempLabel.TabIndex = 2;
            this.lblTempLabel.Text = "当前温度:";
            this.lblTempLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblRunState
            // 
            this.lblRunState.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Bold);
            this.lblRunState.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(80)))), ((int)(((byte)(160)))));
            this.lblRunState.Location = new System.Drawing.Point(106, 14);
            this.lblRunState.Name = "lblRunState";
            this.lblRunState.Size = new System.Drawing.Size(220, 36);
            this.lblRunState.TabIndex = 1;
            this.lblRunState.Text = "--";
            this.lblRunState.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblRunLabel
            // 
            this.lblRunLabel.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblRunLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblRunLabel.Location = new System.Drawing.Point(16, 16);
            this.lblRunLabel.Name = "lblRunLabel";
            this.lblRunLabel.Size = new System.Drawing.Size(90, 32);
            this.lblRunLabel.TabIndex = 0;
            this.lblRunLabel.Text = "运行状态:";
            this.lblRunLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // txtLog
            // 
            this.txtLog.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtLog.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.txtLog.FillReadOnlyColor = System.Drawing.Color.White;
            this.txtLog.Font = new System.Drawing.Font("微软雅黑", 9.5F);
            this.txtLog.Location = new System.Drawing.Point(0, 338);
            this.txtLog.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.txtLog.MinimumSize = new System.Drawing.Size(1, 16);
            this.txtLog.Multiline = true;
            this.txtLog.Name = "txtLog";
            this.txtLog.Padding = new System.Windows.Forms.Padding(5);
            this.txtLog.ReadOnly = true;
            this.txtLog.ShowScrollBar = true;
            this.txtLog.ShowText = false;
            this.txtLog.Size = new System.Drawing.Size(700, 162);
            this.txtLog.TabIndex = 3;
            this.txtLog.TextAlignment = System.Drawing.ContentAlignment.MiddleLeft;
            this.txtLog.Watermark = "操作日志将显示在这里";
            // 
            // FanTestForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(700, 500);
            this.Controls.Add(this.txtLog);
            this.Controls.Add(this.pnlStatus);
            this.Controls.Add(this.pnlConn);
            this.Controls.Add(this.pnlHeader);
            this.EscClose = true;
            this.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FanTestForm";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Style = Sunny.UI.UIStyle.Custom;
            this.Text = "冷却送风机通讯测试";
            this.TitleFont = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Bold);
            this.ZoomScaleRect = new System.Drawing.Rectangle(15, 15, 700, 434);
            this.pnlHeader.ResumeLayout(false);
            this.pnlConn.ResumeLayout(false);
            this.pnlStatus.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private Sunny.UI.UIPanel pnlHeader;
        private Sunny.UI.UILedBulb ledStatus;
        private Sunny.UI.UILabel lblStatus;
        private Sunny.UI.UILabel lblHeaderHint;
        private Sunny.UI.UIPanel pnlConn;
        private Sunny.UI.UILabel lblIpLabel;
        private Sunny.UI.UITextBox txtIp;
        private Sunny.UI.UILabel lblPortLabel;
        private Sunny.UI.UITextBox txtPort;
        private Sunny.UI.UIButton btnConnect;
        private Sunny.UI.UIPanel pnlStatus;
        private Sunny.UI.UILabel lblRunLabel;
        private Sunny.UI.UILabel lblRunState;
        private Sunny.UI.UILabel lblTempLabel;
        private Sunny.UI.UILabel lblTemp;
        private Sunny.UI.UILabel lblHumLabel;
        private Sunny.UI.UILabel lblHumidity;
        private Sunny.UI.UILabel lblTempSetLabel;
        private Sunny.UI.UILabel lblTempSet;
        private Sunny.UI.UILabel lblHumSetLabel;
        private Sunny.UI.UILabel lblHumSet;
        private Sunny.UI.UIButton btnStartFixed;
        private Sunny.UI.UIButton btnStop;
        private Sunny.UI.UIButton btnRefresh;
        private Sunny.UI.UITextBox txtLog;
    }
}
