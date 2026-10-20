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
    /// 【单行版】温度与湿度分开校准（厂商手册里两路小数位可能不同），
    /// 所以窗里只有一行：标题 + 输入框 + 当前读数 + 当前斜率，行/列位置对两行版保持不变，
    /// 客户看两版都顺手。
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
            this._lblItemTitle = new Sunny.UI.UILabel();
            this._txtScreen = new Sunny.UI.UITextBox();
            this._lblCurrent = new Sunny.UI.UILabel();
            this._lblCurrentScale = new Sunny.UI.UILabel();
            this._lblResult = new Sunny.UI.UILabel();
            this._btnOk = new Sunny.UI.UIButton();
            this._btnCancel = new Sunny.UI.UIButton();
            this.SuspendLayout();
            // 
            // _lblHint
            // 
            this._lblHint.Font = new System.Drawing.Font("微软雅黑", 9F);
            this._lblHint.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this._lblHint.Location = new System.Drawing.Point(12, 47);
            this._lblHint.Name = "_lblHint";
            this._lblHint.Size = new System.Drawing.Size(456, 36);
            this._lblHint.TabIndex = 0;
            this._lblHint.Text = "把送风机控制屏上读到的数值，填入下面输入框后点【确定】。";
            // 
            // _lblItemTitle
            // 
            this._lblItemTitle.Font = new System.Drawing.Font("微软雅黑", 9F);
            this._lblItemTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this._lblItemTitle.Location = new System.Drawing.Point(12, 88);
            this._lblItemTitle.Name = "_lblItemTitle";
            this._lblItemTitle.Size = new System.Drawing.Size(44, 22);
            this._lblItemTitle.TabIndex = 1;
            this._lblItemTitle.Text = "温度";
            this._lblItemTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _txtScreen
            // 
            this._txtScreen.Cursor = System.Windows.Forms.Cursors.IBeam;
            this._txtScreen.Font = new System.Drawing.Font("微软雅黑", 10F);
            this._txtScreen.Location = new System.Drawing.Point(60, 84);
            this._txtScreen.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this._txtScreen.MinimumSize = new System.Drawing.Size(1, 16);
            this._txtScreen.Name = "_txtScreen";
            this._txtScreen.Padding = new System.Windows.Forms.Padding(5);
            this._txtScreen.Size = new System.Drawing.Size(120, 26);
            this._txtScreen.TabIndex = 2;
            this._txtScreen.TextAlignment = System.Drawing.ContentAlignment.MiddleLeft;
            this._txtScreen.Watermark = "如 22.20";
            // 
            // _lblCurrent
            // 
            this._lblCurrent.Font = new System.Drawing.Font("微软雅黑", 9F);
            this._lblCurrent.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this._lblCurrent.Location = new System.Drawing.Point(196, 88);
            this._lblCurrent.Name = "_lblCurrent";
            this._lblCurrent.Size = new System.Drawing.Size(180, 22);
            this._lblCurrent.TabIndex = 3;
            this._lblCurrent.Text = "当前  --";
            this._lblCurrent.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _lblCurrentScale
            // 
            this._lblCurrentScale.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold);
            this._lblCurrentScale.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this._lblCurrentScale.Location = new System.Drawing.Point(12, 118);
            this._lblCurrentScale.Name = "_lblCurrentScale";
            this._lblCurrentScale.Size = new System.Drawing.Size(240, 22);
            this._lblCurrentScale.TabIndex = 4;
            this._lblCurrentScale.Text = "当前温度斜率：0.1";
            this._lblCurrentScale.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _lblResult
            // 
            this._lblResult.Font = new System.Drawing.Font("微软雅黑", 9F);
            this._lblResult.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this._lblResult.Location = new System.Drawing.Point(12, 146);
            this._lblResult.Name = "_lblResult";
            this._lblResult.Size = new System.Drawing.Size(456, 60);
            this._lblResult.TabIndex = 5;
            // 
            // _btnOk
            // 
            this._btnOk.Cursor = System.Windows.Forms.Cursors.Hand;
            this._btnOk.Font = new System.Drawing.Font("微软雅黑", 9F);
            this._btnOk.Location = new System.Drawing.Point(46, 236);
            this._btnOk.MinimumSize = new System.Drawing.Size(1, 1);
            this._btnOk.Name = "_btnOk";
            this._btnOk.Size = new System.Drawing.Size(134, 28);
            this._btnOk.TabIndex = 6;
            this._btnOk.Text = "确定";
            this._btnOk.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this._btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // _btnCancel
            // 
            this._btnCancel.Cursor = System.Windows.Forms.Cursors.Hand;
            this._btnCancel.Font = new System.Drawing.Font("微软雅黑", 9F);
            this._btnCancel.Location = new System.Drawing.Point(292, 236);
            this._btnCancel.MinimumSize = new System.Drawing.Size(1, 1);
            this._btnCancel.Name = "_btnCancel";
            this._btnCancel.Size = new System.Drawing.Size(136, 28);
            this._btnCancel.TabIndex = 7;
            this._btnCancel.Text = "取消";
            this._btnCancel.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this._btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // FanCalibrationForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(480, 300);
            this.Controls.Add(this._lblHint);
            this.Controls.Add(this._lblItemTitle);
            this.Controls.Add(this._txtScreen);
            this.Controls.Add(this._lblCurrent);
            this.Controls.Add(this._lblCurrentScale);
            this.Controls.Add(this._lblResult);
            this.Controls.Add(this._btnOk);
            this.Controls.Add(this._btnCancel);
            this.EscClose = true;
            this.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(480, 300);
            this.Name = "FanCalibrationForm";
            this.ShowIcon = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Style = Sunny.UI.UIStyle.Custom;
            this.Text = "送风机定点校准";
            this.TitleFont = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Bold);
            this.ZoomScaleRect = new System.Drawing.Rectangle(15, 15, 480, 300);
            this.ResumeLayout(false);

        }
    }
}
