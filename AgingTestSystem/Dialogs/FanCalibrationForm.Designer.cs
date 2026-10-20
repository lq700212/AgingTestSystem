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
            this._lblTempTitle = new Sunny.UI.UILabel();
            this._lblHumidityTitle = new Sunny.UI.UILabel();
            this._txtScreenTemp = new Sunny.UI.UITextBox();
            this._txtScreenHumidity = new Sunny.UI.UITextBox();
            this._lblCurrentTemp = new Sunny.UI.UILabel();
            this._lblCurrentHumidity = new Sunny.UI.UILabel();
            this._lblCurrentDivisor = new Sunny.UI.UILabel();
            this._lblResult = new Sunny.UI.UILabel();
            this._btnOk = new Sunny.UI.UIButton();
            this._btnCancel = new Sunny.UI.UIButton();
            this.SuspendLayout();
            //
            // FanCalibrationForm（UIForm 蓝标题；绝对布局内容从 y=47 起排）
            //
            this.AutoScaleMode = AutoScaleMode.None;
            this.Text = "送风机定点校准";
            this.Name = "FanCalibrationForm";
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowIcon = false;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("微软雅黑", 9F);
            this.ClientSize = new Size(480, 300);
            this.MinimumSize = new Size(480, 300);
            this.Style = Sunny.UI.UIStyle.Custom;
            this.TitleFont = new Font("微软雅黑", 12F, FontStyle.Bold);
            this.EscClose = true;
            this.ZoomScaleRect = new Rectangle(15, 15, 480, 300);
            //
            // _lblHint
            //
            this._lblHint.Name = "_lblHint";
            this._lblHint.Font = new Font("微软雅黑", 9F);
            this._lblHint.ForeColor = Color.FromArgb(48, 48, 48);
            this._lblHint.Location = new Point(12, 47);
            this._lblHint.Size = new Size(456, 36);
            this._lblHint.Text = "把送风机控制屏上读到的温度/湿度，填入对应输入框后点【确定】。\r\n只要填一项即可；填两项时两者推出的除数必须一致。";
            this._lblHint.TextAlign = ContentAlignment.TopLeft;
            //
            // _lblTempTitle
            //
            this._lblTempTitle.Name = "_lblTempTitle";
            this._lblTempTitle.Font = new Font("微软雅黑", 9F);
            this._lblTempTitle.ForeColor = Color.FromArgb(48, 48, 48);
            this._lblTempTitle.Location = new Point(12, 88);
            this._lblTempTitle.Size = new Size(44, 22);
            this._lblTempTitle.Text = "温度";
            this._lblTempTitle.TextAlign = ContentAlignment.MiddleLeft;
            //
            // _lblHumidityTitle
            //
            this._lblHumidityTitle.Name = "_lblHumidityTitle";
            this._lblHumidityTitle.Font = new Font("微软雅黑", 9F);
            this._lblHumidityTitle.ForeColor = Color.FromArgb(48, 48, 48);
            this._lblHumidityTitle.Location = new Point(12, 120);
            this._lblHumidityTitle.Size = new Size(44, 22);
            this._lblHumidityTitle.Text = "湿度";
            this._lblHumidityTitle.TextAlign = ContentAlignment.MiddleLeft;
            //
            // _txtScreenTemp
            //
            this._txtScreenTemp.Name = "_txtScreenTemp";
            this._txtScreenTemp.Font = new Font("微软雅黑", 10F);
            this._txtScreenTemp.Location = new Point(60, 84);
            this._txtScreenTemp.Size = new Size(120, 26);
            this._txtScreenTemp.ShowText = true;
            this._txtScreenTemp.Watermark = "如 22.20";
            //
            // _txtScreenHumidity
            //
            this._txtScreenHumidity.Name = "_txtScreenHumidity";
            this._txtScreenHumidity.Font = new Font("微软雅黑", 10F);
            this._txtScreenHumidity.Location = new Point(60, 116);
            this._txtScreenHumidity.Size = new Size(120, 26);
            this._txtScreenHumidity.ShowText = true;
            this._txtScreenHumidity.Watermark = "如 55.0";
            //
            // _lblCurrentTemp
            //
            this._lblCurrentTemp.Name = "_lblCurrentTemp";
            this._lblCurrentTemp.Font = new Font("微软雅黑", 9F);
            this._lblCurrentTemp.ForeColor = Color.FromArgb(48, 48, 48);
            this._lblCurrentTemp.Location = new Point(196, 88);
            this._lblCurrentTemp.Size = new Size(180, 22);
            this._lblCurrentTemp.Text = "当前  --";
            this._lblCurrentTemp.TextAlign = ContentAlignment.MiddleLeft;
            //
            // _lblCurrentHumidity
            //
            this._lblCurrentHumidity.Name = "_lblCurrentHumidity";
            this._lblCurrentHumidity.Font = new Font("微软雅黑", 9F);
            this._lblCurrentHumidity.ForeColor = Color.FromArgb(48, 48, 48);
            this._lblCurrentHumidity.Location = new Point(196, 120);
            this._lblCurrentHumidity.Size = new Size(180, 22);
            this._lblCurrentHumidity.Text = "当前  --";
            this._lblCurrentHumidity.TextAlign = ContentAlignment.MiddleLeft;
            //
            // _lblCurrentDivisor
            //
            this._lblCurrentDivisor.Name = "_lblCurrentDivisor";
            this._lblCurrentDivisor.Font = new Font("微软雅黑", 9F, FontStyle.Bold);
            this._lblCurrentDivisor.ForeColor = Color.FromArgb(48, 48, 48);
            this._lblCurrentDivisor.Location = new Point(12, 152);
            this._lblCurrentDivisor.Size = new Size(240, 22);
            this._lblCurrentDivisor.Text = "当前定点除数：10";
            this._lblCurrentDivisor.TextAlign = ContentAlignment.MiddleLeft;
            //
            // _lblResult
            //
            this._lblResult.Name = "_lblResult";
            this._lblResult.Font = new Font("微软雅黑", 9F);
            this._lblResult.ForeColor = Color.FromArgb(48, 48, 48);
            this._lblResult.Location = new Point(12, 180);
            this._lblResult.Size = new Size(456, 50);
            this._lblResult.Text = "";
            this._lblResult.TextAlign = ContentAlignment.TopLeft;
            //
            // _btnOk
            //
            this._btnOk.Name = "_btnOk";
            this._btnOk.Font = new Font("微软雅黑", 9F);
            this._btnOk.Location = new Point(296, 236);
            this._btnOk.Size = new Size(84, 28);
            this._btnOk.Text = "确定";
            this._btnOk.Click += new System.EventHandler(this.btnOk_Click);
            //
            // _btnCancel
            //
            this._btnCancel.Name = "_btnCancel";
            this._btnCancel.Font = new Font("微软雅黑", 9F);
            this._btnCancel.Location = new Point(392, 236);
            this._btnCancel.Size = new Size(84, 28);
            this._btnCancel.Text = "取消";
            this._btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            //
            // FanCalibrationForm
            //
            this.Controls.Add(this._lblHint);
            this.Controls.Add(this._lblTempTitle);
            this.Controls.Add(this._lblHumidityTitle);
            this.Controls.Add(this._txtScreenTemp);
            this.Controls.Add(this._txtScreenHumidity);
            this.Controls.Add(this._lblCurrentTemp);
            this.Controls.Add(this._lblCurrentHumidity);
            this.Controls.Add(this._lblCurrentDivisor);
            this.Controls.Add(this._lblResult);
            this.Controls.Add(this._btnOk);
            this.Controls.Add(this._btnCancel);
            this.ResumeLayout(false);
        }
    }
}
