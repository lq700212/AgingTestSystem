using System.Drawing;
using System.Windows.Forms;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 下料判定窗体 — 设计器部分（【V1.72.12 新增】纯代码拆分：静态边框进 Designer）。
    /// 这里只装"静态边框"：窗体属性 + 范围标签/单选/输入/下拉/两按钮/结果标签。
    /// 以下两样仍在 UnloadJudgeForm.cs 里用代码做：
    /// ①_lblScope 初值（BuildScopeText 要吃构造传进的 deviceIds/deviceManager 真参数，
    /// Designer 给不了，构造调完 InitializeComponent 后再回填）；
    /// ②判定执行逻辑（BtnExecute_Click 调 DeviceManager 落盘）。
    /// 【布局】绝对定位（UIForm 自绘蓝标题占 35px，内容从 y=47 起排）；
    /// MinimumSize=ClientSize 锁缩小（V1.71 绝对布局窗统一做法）。
    /// </summary>
    partial class UnloadJudgeForm
    {
        /// <summary>送判范围说明（完成态可判/其余跳过；初值代码回填）</summary>
        private Sunny.UI.UILabel _lblScope;

        /// <summary>PASS 单选（默认选中）</summary>
        private RadioButton _rbPass;

        /// <summary>FAIL 单选</summary>
        private RadioButton _rbFail;

        /// <summary>不良代码输入（FAIL 必填）</summary>
        private Sunny.UI.UITextBox _txtDefectCode;

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
            this._txtDefectCode = new Sunny.UI.UITextBox();
            this._cmbDisposition = new Sunny.UI.UIComboBox();
            this._btnExecute = new Sunny.UI.UIButton();
            this._btnClose = new Sunny.UI.UIButton();
            this._lblResult = new Sunny.UI.UILabel();
            this.SuspendLayout();
            //
            // UnloadJudgeForm（UIForm 蓝标题；绝对布局内容从 y=47 起排）
            //
            this.AutoScaleDimensions = new SizeF(6F, 12F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Text = "下料判定";
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(440, 335);
            this.MinimumSize = new Size(440, 335);
            //
            // _lblScope（初值代码回填，见构造）
            //
            this._lblScope.Location = new Point(12, 47);
            this._lblScope.Size = new Size(416, 36);
            //
            // _rbPass / _rbFail
            //
            this._rbPass.Location = new Point(12, 91);
            this._rbPass.Size = new Size(100, 24);
            this._rbPass.Text = "PASS";
            this._rbPass.Checked = true;
            this._rbFail.Location = new Point(120, 91);
            this._rbFail.Size = new Size(100, 24);
            this._rbFail.Text = "FAIL";
            //
            // 不良代码行（标签口头创建：静态文本，无逻辑引用）
            //
            var lblCode = new Sunny.UI.UILabel();
            lblCode.Location = new Point(12, 127);
            lblCode.Size = new Size(80, 20);
            lblCode.Text = "不良代码：";
            this._txtDefectCode.Location = new Point(96, 123);
            this._txtDefectCode.Size = new Size(332, 24);
            //
            // 处置行
            //
            var lblDisp = new Sunny.UI.UILabel();
            lblDisp.Location = new Point(12, 159);
            lblDisp.Size = new Size(80, 20);
            lblDisp.Text = "处置：";
            this._cmbDisposition.Location = new Point(96, 155);
            this._cmbDisposition.Size = new Size(332, 24);
            this._cmbDisposition.DropDownStyle = Sunny.UI.UIDropDownStyle.DropDownList;
            this._cmbDisposition.Items.AddRange(Dispositions);
            //
            // _btnExecute（Sunny 默认蓝，主操作）
            //
            this._btnExecute.Location = new Point(12, 195);
            this._btnExecute.Size = new Size(200, 30);
            this._btnExecute.Text = "执行判定";
            this._btnExecute.Click += new System.EventHandler(this.BtnExecute_Click);
            //
            // _btnClose（Sunny 灰；语义=取消关闭，走灰）
            //
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
            this._lblResult.Location = new Point(12, 235);
            this._lblResult.Size = new Size(416, 40);
            this._lblResult.ForeColor = Color.Blue;
            //
            // 挂接
            //
            this.Controls.Add(this._lblScope);
            this.Controls.Add(this._rbPass);
            this.Controls.Add(this._rbFail);
            this.Controls.Add(lblCode);
            this.Controls.Add(this._txtDefectCode);
            this.Controls.Add(lblDisp);
            this.Controls.Add(this._cmbDisposition);
            this.Controls.Add(this._btnExecute);
            this.Controls.Add(this._btnClose);
            this.Controls.Add(this._lblResult);
            this.CancelButton = this._btnClose;
            this.ResumeLayout(false);
        }
    }
}
