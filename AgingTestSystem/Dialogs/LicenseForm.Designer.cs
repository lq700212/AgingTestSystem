using System.Drawing;
using System.Windows.Forms;
using AgingTestSystem.Controls;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 软件授权窗体 — 设计器部分（【V1.86 新增】纯代码拆分：静态边框进 Designer，VS 可预览编辑）。
    /// 这里只装"静态边框"：窗体属性 + 状态行/机器码行（显示框+眼睛+复制）/导出导入行/明细行/关闭钮。
    /// 以下在 LicenseForm.cs 里用代码做：
    /// ①机器码回填（MachineFingerprint.Compute，构造调完 InitializeComponent 后调，失败置空）；
    /// ②导入按钮可用态（canEdit=管理员才可用，只读模式改文案+挂悬停说明）；
    /// ③眼睛显隐（BtnEye_Click 切 PasswordChar + 翻 Shown 换自绘图案 + 换悬停文案，
    ///    默认掩码防偷窥；_eyeIcon 是 _txtMachine 的子控件，浮在输入框右缘内侧）；
    /// ④复制/导出/导入逻辑（BtnCopy/BtnExport/BtnImport_Click，读的都是 Text 真值，与掩码无关）。
    /// 【布局】绝对定位（UIForm 自绘蓝标题占 35px，内容从 y=50 起排）；
    /// 眼睛图标（EyeIcon 自绘 20x18）内嵌在机器码显示框右缘内侧：它是显示框的
    /// 子控件（_txtMachine.Controls.Add），天然浮在输入框上——不做的"窗体级叠加"
    /// 会被输入框整体盖住（V1.86 血泪，见 AGENTS 叠放控件层级三锁）；
    /// 运行时由 LicenseForm.cs 的 PositionEye 按 ClientSize 精确定位并随 Resize 跟随；
    /// MinimumSize 锁缩小（V1.71 绝对布局窗统一做法）。
    /// 【字体家规】Designer 不写 Font（状态行加粗在 .cs 里按当前字号 Bold，
    /// V1.78 先例）：Designer 写死字号即与继承/换肤字号分叉，VS 重写还转字面串。
    /// </summary>
    partial class LicenseForm
    {
        /// <summary>授权状态行（加粗；初值代码回填，红/绿按状态）</summary>
        private Sunny.UI.UILabel _lblStatus;

        /// <summary>机器码说明行（静态文本）</summary>
        private Sunny.UI.UILabel _lblMachine;

        /// <summary>机器码显示框（只读；默认掩码，读 Text 永远是真值）</summary>
        private Sunny.UI.UITextBox _txtMachine;

        /// <summary>眼睛图标（自绘，_txtMachine 的子控件，浮在右缘内侧；点一下显隐切换）</summary>
        private EyeIcon _eyeIcon;

        /// <summary>复制按钮（拷真值发商务）</summary>
        private Sunny.UI.UIButton _btnCopy;

        /// <summary>导出机器码按钮（明文 txt，含分组与去分组两行）</summary>
        private Sunny.UI.UIButton _btnExport;

        /// <summary>导入授权文件按钮（仅管理员可用）</summary>
        private Sunny.UI.UIButton _btnImport;

        /// <summary>项目/点数/到期明细行（灰字；初值代码回填）</summary>
        private Sunny.UI.UILabel _lblDetail;

        /// <summary>关闭按钮（Sunny 灰；DialogResult=Cancel）</summary>
        private Sunny.UI.UIButton _btnClose;

        private void InitializeComponent()
        {
            this._lblStatus = new Sunny.UI.UILabel();
            this._lblMachine = new Sunny.UI.UILabel();
            this._txtMachine = new Sunny.UI.UITextBox();
            this._eyeIcon = new Controls.EyeIcon();
            this._btnCopy = new Sunny.UI.UIButton();
            this._btnExport = new Sunny.UI.UIButton();
            this._btnImport = new Sunny.UI.UIButton();
            this._lblDetail = new Sunny.UI.UILabel();
            this._btnClose = new Sunny.UI.UIButton();
            this.SuspendLayout();
            this.AutoScaleDimensions = new SizeF(6F, 12F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Name = "LicenseForm";
            this.Text = "软件授权";
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Size = new Size(560, 360);
            this.MinimumSize = new Size(480, 320);
            this.Padding = new Padding(10, 45, 10, 10);
            this._lblStatus.Location = new Point(16, 50);
            this._lblStatus.Size = new Size(510, 30);
            this._lblMachine.Location = new Point(16, 88);
            this._lblMachine.Size = new Size(510, 22);
            this._lblMachine.Text = "本机机器码（发给商务签发授权，一机一证）：";
            this._txtMachine.Location = new Point(16, 112);
            this._txtMachine.Size = new Size(400, 30);
            this._txtMachine.ReadOnly = true;
            this._txtMachine.PasswordChar = '●';
            this._eyeIcon.BackColor = SystemColors.Window;
            this._eyeIcon.Location = new Point(378, 6);
            this._eyeIcon.Size = new Size(20, 18);
            this._eyeIcon.Margin = new Padding(0);
            this._eyeIcon.TabStop = false;
            this._eyeIcon.Click += new System.EventHandler(this.BtnEye_Click);
            this._btnCopy.Location = new Point(424, 112);
            this._btnCopy.Size = new Size(100, 30);
            this._btnCopy.Text = "复制";
            this._btnCopy.Click += new System.EventHandler(this.BtnCopy_Click);
            this._btnExport.Location = new Point(16, 152);
            this._btnExport.Size = new Size(150, 32);
            this._btnExport.Text = "导出机器码";
            this._btnExport.Click += new System.EventHandler(this.BtnExport_Click);
            this._btnImport.Location = new Point(176, 152);
            this._btnImport.Size = new Size(150, 32);
            this._btnImport.Text = "导入授权文件";
            this._btnImport.Click += new System.EventHandler(this.BtnImport_Click);
            this._lblDetail.Location = new Point(16, 194);
            this._lblDetail.Size = new Size(510, 70);
            this._lblDetail.ForeColor = Color.Gray;
            this._btnClose.Location = new Point(374, 274);
            this._btnClose.Size = new Size(150, 32);
            this._btnClose.Text = "关闭";
            this._btnClose.DialogResult = DialogResult.Cancel;
            this._btnClose.FillColor = Color.DimGray;
            this._btnClose.RectColor = Color.DimGray;
            this._btnClose.ForeColor = Color.White;
            this._btnClose.Style = Sunny.UI.UIStyle.Custom;
            this._btnClose.Click += new System.EventHandler(this.BtnClose_Click);
            this.Controls.Add(this._lblStatus);
            this.Controls.Add(this._lblMachine);
            this.Controls.Add(this._txtMachine);
            this.Controls.Add(this._btnCopy);
            this.Controls.Add(this._btnExport);
            this.Controls.Add(this._btnImport);
            this.Controls.Add(this._lblDetail);
            this.Controls.Add(this._btnClose);
            this._txtMachine.Controls.Add(this._eyeIcon);
            this.ResumeLayout(false);
        }
    }
}