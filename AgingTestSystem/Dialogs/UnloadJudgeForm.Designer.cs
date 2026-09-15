using System.Drawing;
using System.Windows.Forms;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 下料判定窗体 — 设计器部分（纯代码拆分：静态边框进 Designer）。
    /// 这里只装"静态边框"：窗体属性 + 范围标签/单选/输入/下拉/两按钮/结果标签。
    /// 以下两样仍在 UnloadJudgeForm.cs 里用代码做：
    /// ①_lblScope 初值（BuildScopeText 要吃构造传进的 deviceIds/deviceManager 真参数，
    /// Designer 给不了，构造调完 InitializeComponent 后再回填；设计器无参构造给空快照占位）；
    /// ②判定执行逻辑（BtnExecute_Click 调 DeviceManager 落盘）。
    /// 【布局】绝对定位（UIForm 自绘蓝标题占 35px，内容从 y=47 起排）；
    /// MinimumSize=ClientSize 锁缩小（V1.71 绝对布局窗统一做法）。
    /// 上次只补了无参构造 + 标签具名，漏了两个设计器认不出的东西，预览照样坏：
    /// ①_cmbDisposition.Items.AddRange(Dispositions) 引了另一个 partial 里的静态字段，
    /// 设计器的 CodeDom 反序列化在实例上找不到静态成员，直接加载失败。
    /// 处置选项改由构造在 InitializeComponent 之后用代码填（见 .cs），Designer 里不留；
    /// ②AutoScaleMode.Font + AutoScaleDimensions：在带 ZoomScaleRect 的 Sunny 窗上，
    /// ZoomScaleRect 的 setter 会把 AutoScaleMode 掰回 None（Sunny 自己做缩放），
    /// 设计器加载完发现活值（None）与代码（Font）对不上，打开即标脏甚至加载失败。
    /// 全仓带 ZoomScaleRect 且预览正常的窗（批量/通讯/风扇/主页布局）一律 None 无 Dimensions，
    /// 本窗是最后一个 Font+Zoom 混搭，改齐。运行时零影响（终值本来就是 None）。
    /// </summary>
    partial class UnloadJudgeForm
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

        /// <summary>送判范围说明（完成态可判/其余跳过；初值代码回填）</summary>
        private Sunny.UI.UILabel _lblScope;

        /// <summary>PASS 单选（默认选中）</summary>
        private RadioButton _rbPass;

        /// <summary>FAIL 单选</summary>
        private RadioButton _rbFail;

        /// <summary>"不良代码："静态标签（具名字段，设计器序列化不丢）</summary>
        private Sunny.UI.UILabel _lblCode;

        /// <summary>不良代码输入（FAIL 必填）</summary>
        private Sunny.UI.UITextBox _txtDefectCode;

        /// <summary>"处置："静态标签（具名字段，设计器序列化不丢）</summary>
        private Sunny.UI.UILabel _lblDisp;

        /// <summary>处置下拉（FAIL 必选；选项=Dispositions 静态数组）</summary>
        private Sunny.UI.UIComboBox _cmbDisposition;

        /// <summary>执行判定按钮（Sunny 默认蓝，主操作）</summary>
        private Sunny.UI.UIButton _btnExecute;

        /// <summary>关闭按钮（Sunny 灰；DialogResult=Cancel）</summary>
        private Sunny.UI.UIButton _btnClose;

        /// <summary>判定结果回显（蓝字）</summary>
        private Sunny.UI.UILabel _lblResult;

        private void InitializeComponent()
        {
            this._lblScope = new Sunny.UI.UILabel();
            this._rbPass = new RadioButton();
            this._rbFail = new RadioButton();
            this._lblCode = new Sunny.UI.UILabel();
            this._txtDefectCode = new Sunny.UI.UITextBox();
            this._lblDisp = new Sunny.UI.UILabel();
            this._cmbDisposition = new Sunny.UI.UIComboBox();
            this._btnExecute = new Sunny.UI.UIButton();
            this._btnClose = new Sunny.UI.UIButton();
            this._lblResult = new Sunny.UI.UILabel();
            this.SuspendLayout();
            //
            // UnloadJudgeForm（UIForm 蓝标题；绝对布局内容从 y=47 起排）
            //
            this.AutoScaleMode = AutoScaleMode.None;
            this.Text = "下料判定";
            this.Name = "UnloadJudgeForm";
            this.StartPosition = FormStartPosition.CenterParent;
            this.ShowIcon = false;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("微软雅黑", 9F);
            this.ClientSize = new Size(440, 335);
            this.MinimumSize = new Size(440, 335);
            this.Style = Sunny.UI.UIStyle.Custom;
            this.TitleFont = new Font("微软雅黑", 12F, FontStyle.Bold);
            this.EscClose = true;
            this.ZoomScaleRect = new Rectangle(15, 15, 440, 335);
            //
            // _lblScope（初值代码回填，见构造；设计器里给占位文本，预览不空白）
            //
            this._lblScope.Name = "_lblScope";
            this._lblScope.Font = new Font("微软雅黑", 9F);
            this._lblScope.ForeColor = Color.FromArgb(48, 48, 48);
            this._lblScope.Location = new Point(12, 47);
            this._lblScope.Size = new Size(416, 36);
            this._lblScope.Text = "送判 N 台：完成态 M 台可判，K 台非完成态将跳过。";
            this._lblScope.TextAlign = ContentAlignment.TopLeft;
            //
            // _rbPass / _rbFail
            //
            this._rbPass.Name = "_rbPass";
            this._rbPass.Font = new Font("微软雅黑", 10F, FontStyle.Bold);
            this._rbPass.Location = new Point(12, 91);
            this._rbPass.Size = new Size(100, 24);
            this._rbPass.Text = "PASS";
            this._rbPass.Checked = true;
            this._rbFail.Name = "_rbFail";
            this._rbFail.Font = new Font("微软雅黑", 10F, FontStyle.Bold);
            this._rbFail.Location = new Point(120, 91);
            this._rbFail.Size = new Size(100, 24);
            this._rbFail.Text = "FAIL";
            //
            // 不良代码行
            //
            this._lblCode.Name = "_lblCode";
            this._lblCode.Font = new Font("微软雅黑", 9F);
            this._lblCode.Location = new Point(12, 127);
            this._lblCode.Size = new Size(80, 20);
            this._lblCode.Text = "不良代码：";
            this._lblCode.TextAlign = ContentAlignment.MiddleLeft;
            this._txtDefectCode.Name = "_txtDefectCode";
            this._txtDefectCode.Font = new Font("微软雅黑", 10F);
            this._txtDefectCode.Location = new Point(96, 123);
            this._txtDefectCode.Size = new Size(332, 24);
            this._txtDefectCode.ShowText = false;
            this._txtDefectCode.Watermark = "FAIL 时必填，如 E01";
            //
            // 处置行
            //
            this._lblDisp.Name = "_lblDisp";
            this._lblDisp.Font = new Font("微软雅黑", 9F);
            this._lblDisp.Location = new Point(12, 159);
            this._lblDisp.Size = new Size(80, 20);
            this._lblDisp.Text = "处置：";
            this._lblDisp.TextAlign = ContentAlignment.MiddleLeft;
            this._cmbDisposition.Name = "_cmbDisposition";
            this._cmbDisposition.Font = new Font("微软雅黑", 10F);
            this._cmbDisposition.Location = new Point(96, 155);
            this._cmbDisposition.Size = new Size(332, 24);
            this._cmbDisposition.DropDownStyle = Sunny.UI.UIDropDownStyle.DropDownList;
            // 处置选项不在这里填：Items.AddRange(Dispositions) 引静态字段，设计器加载失败，
            // 改由构造在 InitializeComponent 后用代码填（运行时 4 项，预览空下拉，不影响）。
            // _btnExecute（Sunny 默认蓝，主操作）
            //
            this._btnExecute.Name = "_btnExecute";
            this._btnExecute.Font = new Font("微软雅黑", 10F, FontStyle.Bold);
            this._btnExecute.Location = new Point(12, 195);
            this._btnExecute.Size = new Size(200, 30);
            this._btnExecute.Text = "执行判定";
            this._btnExecute.Click += new System.EventHandler(this.BtnExecute_Click);
            //
            // _btnClose（Sunny 灰；语义=取消关闭，走灰）
            //
            this._btnClose.Name = "_btnClose";
            this._btnClose.Font = new Font("微软雅黑", 10F, FontStyle.Bold);
            this._btnClose.Location = new Point(228, 195);
            this._btnClose.Size = new Size(200, 30);
            this._btnClose.Text = "关闭";
            this._btnClose.DialogResult = DialogResult.Cancel;
            this._btnClose.FillColor = Color.DimGray;
            this._btnClose.RectColor = Color.DimGray;
            this._btnClose.ForeColor = Color.White;
            this._btnClose.Style = Sunny.UI.UIStyle.Custom;
            //
            // _lblResult（蓝字）
            //
            this._lblResult.Name = "_lblResult";
            this._lblResult.Font = new Font("微软雅黑", 9F);
            this._lblResult.Location = new Point(12, 235);
            this._lblResult.Size = new Size(416, 40);
            this._lblResult.ForeColor = Color.Blue;
            this._lblResult.TextAlign = ContentAlignment.TopLeft;
            //
            // 挂接
            //
            this.Controls.Add(this._lblScope);
            this.Controls.Add(this._rbPass);
            this.Controls.Add(this._rbFail);
            this.Controls.Add(this._lblCode);
            this.Controls.Add(this._txtDefectCode);
            this.Controls.Add(this._lblDisp);
            this.Controls.Add(this._cmbDisposition);
            this.Controls.Add(this._btnExecute);
            this.Controls.Add(this._btnClose);
            this.Controls.Add(this._lblResult);
            this.CancelButton = this._btnClose;
            this.ResumeLayout(false);
        }
    }
}
