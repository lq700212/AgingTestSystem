namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 单台手动控制窗体 —— 设计器自动生成部分（V1.10 新增）
    ///
    /// 【布局说明】
    /// ┌────────────────────────────────────────────┐
    /// │ 设备 NO.x 手动控制                          │
    /// ├────────────────────────────────────────────┤
    /// │ 输入: X000（真空负压表报警触点）             │
    /// │ 输出1: Y000（真空电磁阀）                    │
    /// │ 输出2: Y110（载台上电）                      │
    /// ├────────────────────────────────────────────┤
    /// │ 当前压力: [___________ kPa]                 │
    /// │ DI报警触点: [ OFF ]                         │
    /// │ 真空电磁阀: [开] [关]                       │
    /// │ 载台上电:   [开] [关]                       │
    /// │                       [关闭]               │
    /// └────────────────────────────────────────────┘
    ///
    /// 用途：现场接线条点对应、排查单台故障时手动点动单个阀/载台上电。
    /// </summary>
    partial class DeviceManualForm
    {
        /// <summary>必需的设计器变量</summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>清理所有正在使用的资源</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>设计器支持所需的方法</summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.lblIoInfo = new System.Windows.Forms.Label();
            this.lblPressureLabel = new System.Windows.Forms.Label();
            this.txtPressure = new System.Windows.Forms.TextBox();
            this.lblDiLabel = new System.Windows.Forms.Label();
            this.lblDiState = new System.Windows.Forms.Label();
            this.lblValveLabel = new System.Windows.Forms.Label();
            this.btnValveOn = new System.Windows.Forms.Button();
            this.btnValveOff = new System.Windows.Forms.Button();
            this.lblCarrierLabel = new System.Windows.Forms.Label();
            this.btnCarrierOn = new System.Windows.Forms.Button();
            this.btnCarrierOff = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.timerRefresh = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            //
            // lblIoInfo
            //
            this.lblIoInfo.Location = new System.Drawing.Point(15, 15);
            this.lblIoInfo.Name = "lblIoInfo";
            this.lblIoInfo.Size = new System.Drawing.Size(330, 45);
            this.lblIoInfo.TabIndex = 0;
            this.lblIoInfo.Text = "IO 点位信息";
            //
            // lblPressureLabel
            //
            this.lblPressureLabel.AutoSize = true;
            this.lblPressureLabel.Location = new System.Drawing.Point(15, 70);
            this.lblPressureLabel.Name = "lblPressureLabel";
            this.lblPressureLabel.Size = new System.Drawing.Size(65, 12);
            this.lblPressureLabel.TabIndex = 1;
            this.lblPressureLabel.Text = "当前压力：";
            //
            // txtPressure
            //
            this.txtPressure.Location = new System.Drawing.Point(100, 67);
            this.txtPressure.Name = "txtPressure";
            this.txtPressure.ReadOnly = true;
            this.txtPressure.Size = new System.Drawing.Size(245, 21);
            this.txtPressure.TabIndex = 2;
            //
            // lblDiLabel
            //
            this.lblDiLabel.AutoSize = true;
            this.lblDiLabel.Location = new System.Drawing.Point(15, 100);
            this.lblDiLabel.Name = "lblDiLabel";
            this.lblDiLabel.Size = new System.Drawing.Size(77, 12);
            this.lblDiLabel.TabIndex = 3;
            this.lblDiLabel.Text = "DI报警触点：";
            //
            // lblDiState
            //
            this.lblDiState.AutoSize = true;
            this.lblDiState.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold);
            this.lblDiState.Location = new System.Drawing.Point(100, 97);
            this.lblDiState.Name = "lblDiState";
            this.lblDiState.Size = new System.Drawing.Size(30, 17);
            this.lblDiState.TabIndex = 4;
            this.lblDiState.Text = "OFF";
            //
            // lblValveLabel
            //
            this.lblValveLabel.AutoSize = true;
            this.lblValveLabel.Location = new System.Drawing.Point(15, 130);
            this.lblValveLabel.Name = "lblValveLabel";
            this.lblValveLabel.Size = new System.Drawing.Size(77, 12);
            this.lblValveLabel.TabIndex = 5;
            this.lblValveLabel.Text = "真空电磁阀：";
            //
            // btnValveOn
            //
            this.btnValveOn.BackColor = System.Drawing.Color.LimeGreen;
            this.btnValveOn.ForeColor = System.Drawing.Color.White;
            this.btnValveOn.Location = new System.Drawing.Point(100, 125);
            this.btnValveOn.Name = "btnValveOn";
            this.btnValveOn.Size = new System.Drawing.Size(80, 28);
            this.btnValveOn.TabIndex = 6;
            this.btnValveOn.Text = "开";
            this.btnValveOn.UseVisualStyleBackColor = false;
            this.btnValveOn.Click += new System.EventHandler(this.btnValveOn_Click);
            //
            // btnValveOff
            //
            this.btnValveOff.BackColor = System.Drawing.SystemColors.Control;
            this.btnValveOff.Location = new System.Drawing.Point(190, 125);
            this.btnValveOff.Name = "btnValveOff";
            this.btnValveOff.Size = new System.Drawing.Size(80, 28);
            this.btnValveOff.TabIndex = 7;
            this.btnValveOff.Text = "关";
            this.btnValveOff.UseVisualStyleBackColor = false;
            this.btnValveOff.Click += new System.EventHandler(this.btnValveOff_Click);
            //
            // lblCarrierLabel
            //
            this.lblCarrierLabel.AutoSize = true;
            this.lblCarrierLabel.Location = new System.Drawing.Point(15, 170);
            this.lblCarrierLabel.Name = "lblCarrierLabel";
            this.lblCarrierLabel.Size = new System.Drawing.Size(77, 12);
            this.lblCarrierLabel.TabIndex = 8;
            this.lblCarrierLabel.Text = "载台上电：";
            //
            // btnCarrierOn
            //
            this.btnCarrierOn.BackColor = System.Drawing.Color.LimeGreen;
            this.btnCarrierOn.ForeColor = System.Drawing.Color.White;
            this.btnCarrierOn.Location = new System.Drawing.Point(100, 165);
            this.btnCarrierOn.Name = "btnCarrierOn";
            this.btnCarrierOn.Size = new System.Drawing.Size(80, 28);
            this.btnCarrierOn.TabIndex = 9;
            this.btnCarrierOn.Text = "开";
            this.btnCarrierOn.UseVisualStyleBackColor = false;
            this.btnCarrierOn.Click += new System.EventHandler(this.btnCarrierOn_Click);
            //
            // btnCarrierOff
            //
            this.btnCarrierOff.BackColor = System.Drawing.SystemColors.Control;
            this.btnCarrierOff.Location = new System.Drawing.Point(190, 165);
            this.btnCarrierOff.Name = "btnCarrierOff";
            this.btnCarrierOff.Size = new System.Drawing.Size(80, 28);
            this.btnCarrierOff.TabIndex = 10;
            this.btnCarrierOff.Text = "关";
            this.btnCarrierOff.UseVisualStyleBackColor = false;
            this.btnCarrierOff.Click += new System.EventHandler(this.btnCarrierOff_Click);
            //
            // btnClose
            //
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.Location = new System.Drawing.Point(260, 215);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(85, 28);
            this.btnClose.TabIndex = 11;
            this.btnClose.Text = "关闭";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            //
            // timerRefresh
            //
            this.timerRefresh.Interval = 1000;
            this.timerRefresh.Tick += new System.EventHandler(this.timerRefresh_Tick);
            //
            // DeviceManualForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(360, 255);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.btnCarrierOff);
            this.Controls.Add(this.btnCarrierOn);
            this.Controls.Add(this.lblCarrierLabel);
            this.Controls.Add(this.btnValveOff);
            this.Controls.Add(this.btnValveOn);
            this.Controls.Add(this.lblValveLabel);
            this.Controls.Add(this.lblDiState);
            this.Controls.Add(this.lblDiLabel);
            this.Controls.Add(this.txtPressure);
            this.Controls.Add(this.lblPressureLabel);
            this.Controls.Add(this.lblIoInfo);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DeviceManualForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "设备手动控制";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        // 控件字段声明区域
        /// <summary>IO 点位信息标签</summary>
        private System.Windows.Forms.Label lblIoInfo;
        /// <summary>"当前压力："标签</summary>
        private System.Windows.Forms.Label lblPressureLabel;
        /// <summary>当前压力值显示框（只读）</summary>
        private System.Windows.Forms.TextBox txtPressure;
        /// <summary>"DI报警触点："标签</summary>
        private System.Windows.Forms.Label lblDiLabel;
        /// <summary>DI 报警触点状态标签（绿=OFF，红=ON）</summary>
        private System.Windows.Forms.Label lblDiState;
        /// <summary>"真空电磁阀："标签</summary>
        private System.Windows.Forms.Label lblValveLabel;
        /// <summary>开真空阀按钮</summary>
        private System.Windows.Forms.Button btnValveOn;
        /// <summary>关真空阀按钮</summary>
        private System.Windows.Forms.Button btnValveOff;
        /// <summary>"载台上电："标签</summary>
        private System.Windows.Forms.Label lblCarrierLabel;
        /// <summary>载台上电按钮</summary>
        private System.Windows.Forms.Button btnCarrierOn;
        /// <summary>载台断电按钮</summary>
        private System.Windows.Forms.Button btnCarrierOff;
        /// <summary>关闭按钮</summary>
        private System.Windows.Forms.Button btnClose;
        /// <summary>界面刷新定时器</summary>
        private System.Windows.Forms.Timer timerRefresh;
    }
}
