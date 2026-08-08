namespace BarometerWinform.Dialogs
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
    /// │ pnlBottom：[连接测试][全部关闭][读取状态][关闭窗口]│
    /// │            txtLog（日志框，只读多行）             │
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
            this.ledStatus = new Sunny.UI.UILedBulb();
            this.lblStatus = new Sunny.UI.UILabel();
            this.lblHeaderHint = new Sunny.UI.UILabel();
            this.tabControl = new Sunny.UI.UITabControl();
            this.pageVacuum = new Sunny.UI.UIPage();
            this.panelGridVacuum = new Sunny.UI.UIPanel();
            this.pagePowerOn = new Sunny.UI.UIPage();
            this.panelGridPowerOn = new Sunny.UI.UIPanel();
            this.pnlBottom = new Sunny.UI.UIPanel();
            this.txtLog = new Sunny.UI.UITextBox();
            this.btnConnect = new Sunny.UI.UIButton();
            this.btnAllOff = new Sunny.UI.UIButton();
            this.btnReadStatus = new Sunny.UI.UIButton();
            this.btnClose = new Sunny.UI.UIButton();
            this.pnlHeader.SuspendLayout();
            this.pageVacuum.SuspendLayout();
            this.pagePowerOn.SuspendLayout();
            this.pnlBottom.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlHeader
            // 
            this.pnlHeader.Controls.Add(this.lblHeaderHint);
            this.pnlHeader.Controls.Add(this.lblStatus);
            this.pnlHeader.Controls.Add(this.ledStatus);
            this.pnlHeader.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlHeader.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)238))), ((int)(((byte)245))), ((int)(((byte)255))));
            this.pnlHeader.Name = "pnlHeader";
            this.pnlHeader.Padding = new System.Windows.Forms.Padding(14, 0, 14, 0);
            this.pnlHeader.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)214))), ((int)(((byte)229))), ((int)(((byte)255))));
            this.pnlHeader.Size = new System.Drawing.Size(780, 40);
            this.pnlHeader.TabIndex = 0;
            // 
            // ledStatus
            // 
            this.ledStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)));
            this.ledStatus.Color = System.Drawing.Color.FromArgb(((int)(((byte)230))), ((int)(((byte)80))), ((int)(((byte)80))));
            this.ledStatus.Location = new System.Drawing.Point(14, 8);
            this.ledStatus.Name = "ledStatus";
            this.ledStatus.On = false;
            this.ledStatus.Size = new System.Drawing.Size(24, 24);
            this.ledStatus.TabIndex = 0;
            // 
            // lblStatus
            // 
            this.lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)));
            this.lblStatus.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Bold);
            this.lblStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)30))), ((int)(((byte)80))), ((int)(((byte)160))));
            this.lblStatus.Location = new System.Drawing.Point(44, 4);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(120, 32);
            this.lblStatus.TabIndex = 1;
            this.lblStatus.Text = "未连接";
            this.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblHeaderHint
            // 
            this.lblHeaderHint.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblHeaderHint.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Regular);
            this.lblHeaderHint.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(0x1E)))), ((int)(((byte)(0x50)))), ((int)(((byte)(0xA0)))));
            this.lblHeaderHint.Location = new System.Drawing.Point(240, 4);
            this.lblHeaderHint.Name = "lblHeaderHint";
            this.lblHeaderHint.Size = new System.Drawing.Size(526, 32);
            this.lblHeaderHint.TabIndex = 2;
            this.lblHeaderHint.Text = "现场调试用：点击圆形灯控制对应通道输出（ON=亮绿）；已做备用通道映射的通道点击时会提示实际输出通道。";
            this.lblHeaderHint.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // tabControl
            // 
            this.tabControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Bold);
            this.tabControl.Name = "tabControl";
            this.tabControl.ShowActiveCloseButton = false;
            this.tabControl.ShowCloseButton = false;
            this.tabControl.Size = new System.Drawing.Size(780, 664);
            this.tabControl.Style = Sunny.UI.UIStyle.Blue;
            this.tabControl.TabIndex = 1;
            this.tabControl.TabSelectedHighColor = System.Drawing.Color.Red;
            this.tabControl.TabSelectedHighColorSize = 3;
            // 
            // pageVacuum
            // 
            // 页面高度已由窗体高度保证，能完整显示 9 排按钮（不出现滚动条）
            this.pageVacuum.AutoScroll = false;
            this.pageVacuum.BackColor = System.Drawing.Color.White;
            this.pageVacuum.Controls.Add(this.panelGridVacuum);
            this.pageVacuum.Name = "pageVacuum";
            this.pageVacuum.Padding = new System.Windows.Forms.Padding(3);
            this.pageVacuum.ShowTitle = false;
            this.pageVacuum.Style = Sunny.UI.UIStyle.Blue;
            this.pageVacuum.Text = "负压开关测试";
            // 
            // panelGridVacuum
            // 
            this.panelGridVacuum.BackColor = System.Drawing.Color.White;
            this.panelGridVacuum.FillColor = System.Drawing.Color.White;
            this.panelGridVacuum.Location = new System.Drawing.Point(3, 3);
            this.panelGridVacuum.Name = "panelGridVacuum";
            this.panelGridVacuum.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)(0xE6)))), ((int)(((byte)(0xEA)))), ((int)(((byte)(0xF0)))));
            this.panelGridVacuum.Size = new System.Drawing.Size(680, 660);
            this.panelGridVacuum.TabIndex = 0;
            // 
            // pagePowerOn
            // 
            // 页面高度已由窗体高度保证，能完整显示 9 排按钮（不出现滚动条）
            this.pagePowerOn.AutoScroll = false;
            this.pagePowerOn.BackColor = System.Drawing.Color.White;
            this.pagePowerOn.Controls.Add(this.panelGridPowerOn);
            this.pagePowerOn.Name = "pagePowerOn";
            this.pagePowerOn.Padding = new System.Windows.Forms.Padding(3);
            this.pagePowerOn.ShowTitle = false;
            this.pagePowerOn.Style = Sunny.UI.UIStyle.Blue;
            this.pagePowerOn.Text = "载台上电测试";
            // 
            // panelGridPowerOn
            // 
            this.panelGridPowerOn.BackColor = System.Drawing.Color.White;
            this.panelGridPowerOn.FillColor = System.Drawing.Color.White;
            this.panelGridPowerOn.Location = new System.Drawing.Point(3, 3);
            this.panelGridPowerOn.Name = "panelGridPowerOn";
            this.panelGridPowerOn.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)(0xE6)))), ((int)(((byte)(0xEA)))), ((int)(((byte)(0xF0)))));
            this.panelGridPowerOn.Size = new System.Drawing.Size(680, 660);
            this.panelGridPowerOn.TabIndex = 0;
            // 
            // tabControl 页签
            // 
            // 把两个 UIPage 页加入 UITabControl（SunnyUI 专用 AddPage，普通 TabPages.Add 不支持 UIPage）
            this.tabControl.AddPage(this.pageVacuum);
            this.tabControl.AddPage(this.pagePowerOn);
            // 
            // pnlBottom
            // 
            this.pnlBottom.Controls.Add(this.txtLog);
            this.pnlBottom.Controls.Add(this.btnClose);
            this.pnlBottom.Controls.Add(this.btnReadStatus);
            this.pnlBottom.Controls.Add(this.btnAllOff);
            this.pnlBottom.Controls.Add(this.btnConnect);
            this.pnlBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlBottom.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)248))), ((int)(((byte)250))), ((int)(((byte)252))));
            this.pnlBottom.Name = "pnlBottom";
            this.pnlBottom.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)248))), ((int)(((byte)250))), ((int)(((byte)252))));
            this.pnlBottom.Size = new System.Drawing.Size(780, 196);
            this.pnlBottom.TabIndex = 2;
            // 
            // txtLog
            // 
            this.txtLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtLog.FillReadOnlyColor = System.Drawing.Color.White;
            this.txtLog.Font = new System.Drawing.Font("微软雅黑", 9.5F, System.Drawing.FontStyle.Regular);
            this.txtLog.Location = new System.Drawing.Point(14, 58);
            this.txtLog.Multiline = true;
            this.txtLog.Name = "txtLog";
            this.txtLog.ReadOnly = true;
            this.txtLog.ScrollBarStyleInherited = false;
            this.txtLog.ShowScrollBar = true;
            this.txtLog.Size = new System.Drawing.Size(752, 130);
            this.txtLog.TabIndex = 4;
            this.txtLog.Watermark = "操作日志将显示在这里";
            // 
            // btnConnect
            // 
            this.btnConnect.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Bold);
            this.btnConnect.Location = new System.Drawing.Point(14, 12);
            this.btnConnect.Name = "btnConnect";
            this.btnConnect.Size = new System.Drawing.Size(140, 38);
            this.btnConnect.Style = Sunny.UI.UIStyle.Green;
            this.btnConnect.TabIndex = 0;
            this.btnConnect.Text = "连接测试";
            this.btnConnect.Click += new System.EventHandler(this.btnConnect_Click);
            // 
            // btnAllOff
            // 
            this.btnAllOff.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Bold);
            this.btnAllOff.Location = new System.Drawing.Point(166, 12);
            this.btnAllOff.Name = "btnAllOff";
            this.btnAllOff.Size = new System.Drawing.Size(140, 38);
            this.btnAllOff.Style = Sunny.UI.UIStyle.Orange;
            this.btnAllOff.TabIndex = 1;
            this.btnAllOff.Text = "全部关闭";
            this.btnAllOff.Click += new System.EventHandler(this.btnAllOff_Click);
            // 
            // btnReadStatus
            // 
            this.btnReadStatus.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Bold);
            this.btnReadStatus.Location = new System.Drawing.Point(318, 12);
            this.btnReadStatus.Name = "btnReadStatus";
            this.btnReadStatus.Size = new System.Drawing.Size(140, 38);
            this.btnReadStatus.Style = Sunny.UI.UIStyle.Blue;
            this.btnReadStatus.TabIndex = 2;
            this.btnReadStatus.Text = "读取状态";
            this.btnReadStatus.Click += new System.EventHandler(this.btnReadStatus_Click);
            // 
            // btnClose
            // 
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.Font = new System.Drawing.Font("微软雅黑", 10.5F, System.Drawing.FontStyle.Bold);
            this.btnClose.Location = new System.Drawing.Point(626, 12);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(140, 38);
            this.btnClose.Style = Sunny.UI.UIStyle.Gray;
            this.btnClose.TabIndex = 3;
            this.btnClose.Text = "关闭窗口";
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // CommunicationTestForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            // 高度加大到 1000：完整容纳标题栏(35) + 状态条(40) + 9 排按钮页(约689) + 底部按钮/日志(196)，
            // 9 排×8 列的通道按钮一屏显示完整，页面不出现滚动条
            this.ClientSize = new System.Drawing.Size(780, 1000);
            this.Controls.Add(this.tabControl);
            this.Controls.Add(this.pnlHeader);
            this.Controls.Add(this.pnlBottom);
            this.EscClose = true;
            this.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Regular);
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            // 限制最小尺寸，防止用户缩小窗口导致 9 排按钮被截断
            this.MinimumSize = new System.Drawing.Size(780, 1000);
            this.Name = "CommunicationTestForm";
            this.ShowIcon = false;
            this.ShowTitle = true;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Style = Sunny.UI.UIStyle.Blue;
            this.Text = "通讯测试";
            this.TitleFont = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Bold);
            this.pnlHeader.ResumeLayout(false);
            this.pageVacuum.ResumeLayout(false);
            this.pagePowerOn.ResumeLayout(false);
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
        private Sunny.UI.UIButton btnClose;
        private Sunny.UI.UITextBox txtLog;
    }
}
