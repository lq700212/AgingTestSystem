using System.Drawing;
using System.Windows.Forms;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 软件激活窗体 — 设计器部分（静态边框进 Designer，VS 可预览编辑）。
    /// 布局照抄 HJVision 的 SoftActivation.Designer：激活状态行 + 设备ID组 +
    /// 设备码组 + 激活码组 + 激活按钮；坐标整体下移 35px（UIForm 自绘蓝标题），
    /// 窗体加高 35px（V1.71 口径）。回填与激活逻辑在 SoftActivation.cs。
    /// 【字体家规】Designer 不写 Font（V1.78 先例）：写死字号即与继承字号分叉。
    /// </summary>
    partial class SoftActivation
    {
        private System.Windows.Forms.Label _lblStatus;
        private Sunny.UI.UIGroupBox _grpDeviceId;
        private Sunny.UI.UITextBox _txtDeviceId;
        private Sunny.UI.UIGroupBox _grpDeviceCode;
        private Sunny.UI.UITextBox _txtDeviceCode;
        private Sunny.UI.UIGroupBox _grpActivation;
        private Sunny.UI.UITextBox _txtActivationCode;
        private Sunny.UI.UIButton _btnActivate;

        private void InitializeComponent()
        {
            this._lblStatus = new System.Windows.Forms.Label();
            this._grpDeviceId = new Sunny.UI.UIGroupBox();
            this._txtDeviceId = new Sunny.UI.UITextBox();
            this._grpDeviceCode = new Sunny.UI.UIGroupBox();
            this._txtDeviceCode = new Sunny.UI.UITextBox();
            this._grpActivation = new Sunny.UI.UIGroupBox();
            this._txtActivationCode = new Sunny.UI.UITextBox();
            this._btnActivate = new Sunny.UI.UIButton();
            this._grpDeviceId.SuspendLayout();
            this._grpDeviceCode.SuspendLayout();
            this._grpActivation.SuspendLayout();
            this.SuspendLayout();
            this.AutoScaleDimensions = new SizeF(6F, 12F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Name = "SoftActivation";
            this.Text = "软件授权";
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(409, 433);
            this.MinimumSize = new Size(409, 433);
            this.Padding = new Padding(10, 45, 10, 10);
            this._lblStatus.AutoSize = true;
            this._lblStatus.Location = new Point(9, 69);
            this._lblStatus.Name = "_lblStatus";
            this._lblStatus.Size = new Size(137, 25);
            this._lblStatus.Text = "激活状态：";
            this._grpDeviceId.Location = new Point(18, 120);
            this._grpDeviceId.Name = "_grpDeviceId";
            this._grpDeviceId.Size = new Size(374, 83);
            this._grpDeviceId.Text = "设备ID";
            this._txtDeviceId.Location = new Point(10, 27);
            this._txtDeviceId.Name = "_txtDeviceId";
            this._txtDeviceId.Size = new Size(358, 25);
            this._txtDeviceId.ReadOnly = true;
            this._grpDeviceCode.Location = new Point(18, 209);
            this._grpDeviceCode.Name = "_grpDeviceCode";
            this._grpDeviceCode.Size = new Size(374, 83);
            this._grpDeviceCode.Text = "设备码";
            this._txtDeviceCode.Location = new Point(6, 36);
            this._txtDeviceCode.Name = "_txtDeviceCode";
            this._txtDeviceCode.Size = new Size(358, 25);
            this._txtDeviceCode.ReadOnly = true;
            this._grpActivation.Location = new Point(18, 298);
            this._grpActivation.Name = "_grpActivation";
            this._grpActivation.Size = new Size(374, 83);
            this._grpActivation.Text = "激活码";
            this._txtActivationCode.Location = new Point(6, 36);
            this._txtActivationCode.Name = "_txtActivationCode";
            this._txtActivationCode.Size = new Size(358, 25);
            this._btnActivate.Location = new Point(143, 387);
            this._btnActivate.Name = "_btnActivate";
            this._btnActivate.Size = new Size(100, 35);
            this._btnActivate.Text = "激活";
            this._btnActivate.FillColor = Color.DodgerBlue;
            this._btnActivate.RectColor = Color.DodgerBlue;
            this._btnActivate.ForeColor = Color.White;
            this._btnActivate.Style = Sunny.UI.UIStyle.Custom;
            this._btnActivate.Click += new System.EventHandler(this.BtnActivate_Click);
            this._grpDeviceId.Controls.Add(this._txtDeviceId);
            this._grpDeviceCode.Controls.Add(this._txtDeviceCode);
            this._grpActivation.Controls.Add(this._txtActivationCode);
            this.Controls.Add(this._lblStatus);
            this.Controls.Add(this._grpDeviceId);
            this.Controls.Add(this._grpDeviceCode);
            this.Controls.Add(this._grpActivation);
            this.Controls.Add(this._btnActivate);
            this._grpDeviceId.ResumeLayout(false);
            this._grpDeviceCode.ResumeLayout(false);
            this._grpActivation.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
