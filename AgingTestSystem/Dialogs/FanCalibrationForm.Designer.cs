using System;
using System.Drawing;
using System.Windows.Forms;
using Sunny.UI;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 送风机定点校准窗的界面构造部分（纯代码窗，无 VS 设计器）。
    /// 【缩放口径】AutoScaleMode=None + ZoomScaleRect（Sunny 自己做缩放），
    /// 不设 AutoScaleDimensions —— 微软雅黑 9F 与 (6,12) 基准对不上，
    /// 设了会被固化成 7×17，客户区缩小导致内容溢出（照抄 UnloadJudgeForm 口径）。
    /// 【标题区】UIForm 自绘蓝标题占 35px，绝对定位内容从 y=47 起排（Y&lt;35 会被静默搬到 35 压标题）。
    /// 【单行版】温度与湿度分开校准（厂商手册里两路小数位可能不同），窗里只有一行输入。
    /// 【视觉口径】"干净整洁"的三条做法：
    /// ①提示只留一行短句（教客户填哪个数），不再堆一大段说明；
    /// ②浅灰"对照卡"（UIPanel：FillColor 245 灰 + 220 灰描边，深色模式自动映射成深灰卡）
    ///   把"要填的数"（左）和"上位机当前数"（右）并排放，一眼能比大小；
    /// ③结果行只放状态，平时整行留白（不占视觉），出错时才出现红/绿字。
    /// 卡片内子控件坐标相对卡片客户区（0,0 起）。
    /// </summary>
    partial class FanCalibrationForm
    {
        /// <summary>必需的设计器变量（本窗无组件，留空容器与他窗同口径，设计器不报错）。</summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>释放所有正在使用的资源。</summary>
        /// <param name="disposing">是否释放托管资源</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this._lblHint = new Sunny.UI.UILabel();
            this._lblCurrentScale = new Sunny.UI.UILabel();
            this._pnlCard = new Sunny.UI.UIPanel();
            this._lblItemTitle = new Sunny.UI.UILabel();
            this._txtScreen = new Sunny.UI.UITextBox();
            this._lblUnit = new Sunny.UI.UILabel();
            this._lblCurrentTitle = new Sunny.UI.UILabel();
            this._lblCurrent = new Sunny.UI.UILabel();
            this._lblResult = new Sunny.UI.UILabel();
            this._btnOk = new Sunny.UI.UIButton();
            this._btnCancel = new Sunny.UI.UIButton();
            this._pnlCard.SuspendLayout();
            this.SuspendLayout();
            // 
            // _lblHint
            // 
            this._lblHint.Font = new System.Drawing.Font("微软雅黑", 9F);
            this._lblHint.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(80)))));
            this._lblHint.Location = new System.Drawing.Point(16, 47);
            this._lblHint.Name = "_lblHint";
            this._lblHint.Size = new System.Drawing.Size(240, 18);
            this._lblHint.TabIndex = 0;
            this._lblHint.Text = "填入送风机控制屏上的读数";
            this._lblHint.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _lblCurrentScale
            // 
            this._lblCurrentScale.Font = new System.Drawing.Font("微软雅黑", 9F);
            this._lblCurrentScale.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(80)))));
            this._lblCurrentScale.Location = new System.Drawing.Point(280, 47);
            this._lblCurrentScale.Name = "_lblCurrentScale";
            this._lblCurrentScale.Size = new System.Drawing.Size(144, 18);
            this._lblCurrentScale.TabIndex = 1;
            this._lblCurrentScale.Text = "当前斜率 0.1";
            this._lblCurrentScale.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // _pnlCard
            // 
            this._pnlCard.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(245)))), ((int)(((byte)(245)))));
            this._pnlCard.Controls.Add(this._lblItemTitle);
            this._pnlCard.Controls.Add(this._txtScreen);
            this._pnlCard.Controls.Add(this._lblUnit);
            this._pnlCard.Controls.Add(this._lblCurrentTitle);
            this._pnlCard.Controls.Add(this._lblCurrent);
            this._pnlCard.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(245)))), ((int)(((byte)(245)))), ((int)(((byte)(245)))));
            this._pnlCard.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this._pnlCard.Location = new System.Drawing.Point(16, 72);
            this._pnlCard.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this._pnlCard.MinimumSize = new System.Drawing.Size(1, 1);
            this._pnlCard.Name = "_pnlCard";
            this._pnlCard.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(220)))), ((int)(((byte)(220)))));
            this._pnlCard.Size = new System.Drawing.Size(408, 96);
            this._pnlCard.Style = Sunny.UI.UIStyle.Custom;
            this._pnlCard.TabIndex = 2;
            this._pnlCard.Text = null;
            this._pnlCard.TextAlignment = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // _lblItemTitle
            // 
            this._lblItemTitle.BackColor = System.Drawing.Color.Transparent;
            this._lblItemTitle.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold);
            this._lblItemTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this._lblItemTitle.Location = new System.Drawing.Point(16, 14);
            this._lblItemTitle.Name = "_lblItemTitle";
            this._lblItemTitle.Size = new System.Drawing.Size(44, 26);
            this._lblItemTitle.TabIndex = 0;
            this._lblItemTitle.Text = "温度";
            this._lblItemTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _txtScreen
            // 
            this._txtScreen.Cursor = System.Windows.Forms.Cursors.IBeam;
            this._txtScreen.Font = new System.Drawing.Font("微软雅黑", 11F);
            this._txtScreen.Location = new System.Drawing.Point(64, 12);
            this._txtScreen.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this._txtScreen.MinimumSize = new System.Drawing.Size(1, 16);
            this._txtScreen.Name = "_txtScreen";
            this._txtScreen.Padding = new System.Windows.Forms.Padding(5);
            this._txtScreen.ShowText = false;
            this._txtScreen.Size = new System.Drawing.Size(140, 30);
            this._txtScreen.TabIndex = 1;
            this._txtScreen.TextAlignment = System.Drawing.ContentAlignment.MiddleLeft;
            this._txtScreen.Watermark = "如 22.20";
            // 
            // _lblUnit
            // 
            this._lblUnit.BackColor = System.Drawing.Color.Transparent;
            this._lblUnit.Font = new System.Drawing.Font("微软雅黑", 9F);
            this._lblUnit.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(80)))));
            this._lblUnit.Location = new System.Drawing.Point(212, 14);
            this._lblUnit.Name = "_lblUnit";
            this._lblUnit.Size = new System.Drawing.Size(44, 26);
            this._lblUnit.TabIndex = 2;
            this._lblUnit.Text = "°C";
            this._lblUnit.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _lblCurrentTitle
            // 
            this._lblCurrentTitle.BackColor = System.Drawing.Color.Transparent;
            this._lblCurrentTitle.Font = new System.Drawing.Font("微软雅黑", 9F);
            this._lblCurrentTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(80)))));
            this._lblCurrentTitle.Location = new System.Drawing.Point(16, 58);
            this._lblCurrentTitle.Name = "_lblCurrentTitle";
            this._lblCurrentTitle.Size = new System.Drawing.Size(90, 24);
            this._lblCurrentTitle.TabIndex = 3;
            this._lblCurrentTitle.Text = "上位机当前";
            this._lblCurrentTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _lblCurrent
            // 
            this._lblCurrent.BackColor = System.Drawing.Color.Transparent;
            this._lblCurrent.Font = new System.Drawing.Font("微软雅黑", 14F, System.Drawing.FontStyle.Bold);
            this._lblCurrent.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(80)))), ((int)(((byte)(160)))));
            this._lblCurrent.Location = new System.Drawing.Point(110, 52);
            this._lblCurrent.Name = "_lblCurrent";
            this._lblCurrent.Size = new System.Drawing.Size(200, 36);
            this._lblCurrent.TabIndex = 4;
            this._lblCurrent.Text = "--";
            this._lblCurrent.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _lblResult
            // 
            this._lblResult.Font = new System.Drawing.Font("微软雅黑", 9F);
            this._lblResult.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this._lblResult.Location = new System.Drawing.Point(16, 174);
            this._lblResult.Name = "_lblResult";
            this._lblResult.Size = new System.Drawing.Size(408, 26);
            this._lblResult.TabIndex = 5;
            this._lblResult.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _btnOk
            // 
            this._btnOk.Cursor = System.Windows.Forms.Cursors.Hand;
            this._btnOk.FillColor = System.Drawing.Color.DodgerBlue;
            this._btnOk.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold);
            this._btnOk.Location = new System.Drawing.Point(283, 203);
            this._btnOk.MinimumSize = new System.Drawing.Size(1, 1);
            this._btnOk.Name = "_btnOk";
            this._btnOk.RectColor = System.Drawing.Color.DodgerBlue;
            this._btnOk.Size = new System.Drawing.Size(141, 37);
            this._btnOk.Style = Sunny.UI.UIStyle.Custom;
            this._btnOk.TabIndex = 6;
            this._btnOk.Text = "确定";
            this._btnOk.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this._btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // _btnCancel
            // 
            this._btnCancel.Cursor = System.Windows.Forms.Cursors.Hand;
            this._btnCancel.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(140)))), ((int)(((byte)(140)))), ((int)(((byte)(140)))));
            this._btnCancel.FillColor2 = System.Drawing.Color.FromArgb(((int)(((byte)(140)))), ((int)(((byte)(140)))), ((int)(((byte)(140)))));
            this._btnCancel.FillHoverColor = System.Drawing.Color.FromArgb(((int)(((byte)(163)))), ((int)(((byte)(163)))), ((int)(((byte)(163)))));
            this._btnCancel.FillPressColor = System.Drawing.Color.FromArgb(((int)(((byte)(112)))), ((int)(((byte)(112)))), ((int)(((byte)(112)))));
            this._btnCancel.FillSelectedColor = System.Drawing.Color.FromArgb(((int)(((byte)(112)))), ((int)(((byte)(112)))), ((int)(((byte)(112)))));
            this._btnCancel.Font = new System.Drawing.Font("微软雅黑", 9F);
            this._btnCancel.LightColor = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(248)))), ((int)(((byte)(248)))));
            this._btnCancel.Location = new System.Drawing.Point(16, 203);
            this._btnCancel.MinimumSize = new System.Drawing.Size(1, 1);
            this._btnCancel.Name = "_btnCancel";
            this._btnCancel.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)(140)))), ((int)(((byte)(140)))), ((int)(((byte)(140)))));
            this._btnCancel.RectHoverColor = System.Drawing.Color.FromArgb(((int)(((byte)(163)))), ((int)(((byte)(163)))), ((int)(((byte)(163)))));
            this._btnCancel.RectPressColor = System.Drawing.Color.FromArgb(((int)(((byte)(112)))), ((int)(((byte)(112)))), ((int)(((byte)(112)))));
            this._btnCancel.RectSelectedColor = System.Drawing.Color.FromArgb(((int)(((byte)(112)))), ((int)(((byte)(112)))), ((int)(((byte)(112)))));
            this._btnCancel.Size = new System.Drawing.Size(140, 37);
            this._btnCancel.Style = Sunny.UI.UIStyle.Custom;
            this._btnCancel.TabIndex = 7;
            this._btnCancel.Text = "取消";
            this._btnCancel.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this._btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // FanCalibrationForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(440, 254);
            this.Controls.Add(this._lblHint);
            this.Controls.Add(this._lblCurrentScale);
            this.Controls.Add(this._pnlCard);
            this.Controls.Add(this._lblResult);
            this.Controls.Add(this._btnOk);
            this.Controls.Add(this._btnCancel);
            this.EscClose = true;
            this.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(440, 254);
            this.Name = "FanCalibrationForm";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Style = Sunny.UI.UIStyle.Custom;
            this.Text = "送风机定点校准";
            this.TitleFont = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Bold);
            this.ZoomScaleRect = new System.Drawing.Rectangle(15, 15, 440, 254);
            this._pnlCard.ResumeLayout(false);
            this.ResumeLayout(false);

        }
    }
}
