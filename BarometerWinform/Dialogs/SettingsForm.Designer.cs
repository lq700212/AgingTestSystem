namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 系统设置窗口 —— 设计器自动生成部分
    ///
    /// 【界面布局】（单页展示，不用选项卡，按业务分类用标题分隔线隔开）
    /// ┌──────────────────────────────────────────────┐
    /// │ 顶部提示条（浅蓝底白条）                      │
    /// ├──────────────────────────────────────────────┤
    /// │ ↓ pnlScroll（整页滚动，内容超高时出现滚动条）  │
    /// │ ── 基础配置 ────────────────────────────     │
    /// │ ┌──────────────────────────────────────────┐ │
    /// │ │  配置表格1（设置名称 / 说明 / 设置值）      │ │
    /// │ └──────────────────────────────────────────┘ │
    /// │ ── 气压表串口通讯 ──────────────────────     │
    /// │ ┌──────────────────────────────────────────┐ │
    /// │ │  配置表格2                                 │ │
    /// │ └──────────────────────────────────────────┘ │
    /// │ ……（其余分类依次向下排列）                    │
    /// ├──────────────────────────────────────────────┤
    /// │                    [保存设置] [关闭]           │
    /// └──────────────────────────────────────────────┘
    /// 说明：分类标题分隔线（SunnyUI UILine）与配置表格（SunnyUI UIDataGridView）
    ///       在 SettingsForm.cs 中按业务分组动态创建，设计器只负责窗体骨架
    ///       （顶部提示条 + 中部滚动面板 + 底部按钮栏，均用 SunnyUI 控件）。
    /// </summary>
    partial class SettingsForm
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
            this.lblHint = new System.Windows.Forms.Label();
            this.pnlHint = new Sunny.UI.UIPanel();
            this.pnlScroll = new Sunny.UI.UIPanel();
            this.pnlBottom = new Sunny.UI.UIPanel();
            this.btnSave = new Sunny.UI.UIButton();
            this.btnClose = new Sunny.UI.UIButton();
            this.pnlHint.SuspendLayout();
            this.pnlBottom.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlHint
            // 
            this.pnlHint.Controls.Add(this.lblHint);
            this.pnlHint.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlHint.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)238))), ((int)(((byte)245))), ((int)(((byte)255))));
            this.pnlHint.Name = "pnlHint";
            this.pnlHint.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)214))), ((int)(((byte)229))), ((int)(((byte)255))));
            this.pnlHint.Size = new System.Drawing.Size(960, 36);
            this.pnlHint.TabIndex = 0;
            // 
            // lblHint
            // 
            this.lblHint.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblHint.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)30))), ((int)(((byte)80))), ((int)(((byte)160))));
            this.lblHint.Location = new System.Drawing.Point(0, 0);
            this.lblHint.Name = "lblHint";
            this.lblHint.Padding = new System.Windows.Forms.Padding(14, 0, 0, 0);
            this.lblHint.Size = new System.Drawing.Size(960, 36);
            this.lblHint.TabIndex = 0;
            this.lblHint.Text = "以下为 App.config 中的全部配置项（按业务分类排列），可直接在“设置值”列修改；点击【保存设置】后立即生效（连接参数自动重连），仅设备数量/布局/模拟开关等结构型配置需重启后生效。";
            this.lblHint.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // pnlScroll
            // 
            this.pnlScroll.AutoScroll = true;
            this.pnlScroll.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlScroll.FillColor = System.Drawing.Color.White;
            this.pnlScroll.Name = "pnlScroll";
            this.pnlScroll.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)230))), ((int)(((byte)234))), ((int)(((byte)240))));
            this.pnlScroll.Size = new System.Drawing.Size(960, 614);
            this.pnlScroll.TabIndex = 1;
            // 
            // pnlBottom
            // 
            this.pnlBottom.Controls.Add(this.btnClose);
            this.pnlBottom.Controls.Add(this.btnSave);
            this.pnlBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.pnlBottom.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)248))), ((int)(((byte)250))), ((int)(((byte)252))));
            this.pnlBottom.Name = "pnlBottom";
            this.pnlBottom.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)248))), ((int)(((byte)250))), ((int)(((byte)252))));
            this.pnlBottom.Size = new System.Drawing.Size(960, 50);
            this.pnlBottom.TabIndex = 2;
            // 
            // btnSave
            // 
            this.btnSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSave.AutoSize = false;
            this.btnSave.Location = new System.Drawing.Point(756, 9);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(96, 32);
            this.btnSave.Style = Sunny.UI.UIStyle.Blue;
            this.btnSave.TabIndex = 0;
            this.btnSave.Text = "保存设置";
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // btnClose
            // 
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.AutoSize = false;
            this.btnClose.Location = new System.Drawing.Point(860, 9);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(90, 32);
            this.btnClose.Style = Sunny.UI.UIStyle.Gray;
            this.btnClose.TabIndex = 1;
            this.btnClose.Text = "关闭";
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // SettingsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(960, 700);
            this.Controls.Add(this.pnlScroll);
            this.Controls.Add(this.pnlBottom);
            this.Controls.Add(this.pnlHint);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MinimumSize = new System.Drawing.Size(800, 500);
            this.Name = "SettingsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "系统设置";
            this.pnlHint.ResumeLayout(false);
            this.pnlBottom.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label lblHint;
        private Sunny.UI.UIPanel pnlHint;
        private Sunny.UI.UIPanel pnlScroll;
        private Sunny.UI.UIPanel pnlBottom;
        private Sunny.UI.UIButton btnSave;
        private Sunny.UI.UIButton btnClose;
    }
}
