using System.Drawing;
using System.Windows.Forms;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 项目切换窗体 — 设计器部分（【V1.72.12 新增】纯代码拆分：静态边框进 Designer）。
    /// 这里只装"静态边框"：窗体属性 + 当前项目标签/项目列表/新建行/三操作按钮。
    /// 【V1.73】删掉底部灰字备注 _lblNote（丑）：说明转到 _btnSwitch/_lblCurrent 的
    /// 悬停 tooltip（超 40 字走 SettingsForm.WrapTooltip，全仓统一口径）；
    /// 窗体随之缩高 415→350。
    /// 以下在 ProjectSwitchForm.cs 里用代码做：
    /// ①RefreshList 初填（读 ProjectProfile 列表，构造调完 InitializeComponent 后调）；
    /// ②新建/切换/删除逻辑（BtnCreate/BtnSwitch/BtnDelete_Click 调项目档案服务）；
    /// ③在测台数适配器（CountAdapter 吃构造传进的 Func，真参数 Designer 给不了）；
    /// ④切换按钮可用态轮询（1s 定时器，在测>0 即禁用，tooltip 同步换文案）。
    /// 【布局】绝对定位（UIForm 自绘蓝标题占 35px，内容从 y=47 起排）；
    /// 操作行一排三按钮（各 120 宽、间距 8）；
    /// MinimumSize=ClientSize 锁缩小（V1.71 绝对布局窗统一做法）。
    /// </summary>
    partial class ProjectSwitchForm
    {
        /// <summary>当前项目标题（加粗；初值代码回填）</summary>
        private Sunny.UI.UILabel _lblCurrent;

        /// <summary>项目列表（★=当前；双击=切换）</summary>
        private ListBox _lstProjects;

        /// <summary>新建项目名输入</summary>
        private Sunny.UI.UITextBox _txtNewName;

        /// <summary>创建按钮（以当前为模板复制）</summary>
        private Sunny.UI.UIButton _btnCreate;

        /// <summary>切换并生效按钮（热加载，无需重启）</summary>
        private Sunny.UI.UIButton _btnSwitch;

        /// <summary>删除项目按钮（删非当前，二次确认）</summary>
        private Sunny.UI.UIButton _btnDelete;

        /// <summary>关闭按钮（Sunny 灰；DialogResult=Cancel）</summary>
        private Sunny.UI.UIButton _btnClose;

        /// <summary>"新建："静态标签（无逻辑引用，Designer 具名防后人误改坐标）</summary>
        private Sunny.UI.UILabel _lblNew;

        private void InitializeComponent()
        {
            this._lblCurrent = new Sunny.UI.UILabel();
            this._lstProjects = new ListBox();
            this._lblNew = new Sunny.UI.UILabel();
            this._txtNewName = new Sunny.UI.UITextBox();
            this._btnCreate = new Sunny.UI.UIButton();
            this._btnSwitch = new Sunny.UI.UIButton();
            this._btnDelete = new Sunny.UI.UIButton();
            this._btnClose = new Sunny.UI.UIButton();
            this.SuspendLayout();
            //
            // ProjectSwitchForm（UIForm 蓝标题；绝对布局内容从 y=47 起排）
            //
            this.AutoScaleDimensions = new SizeF(6F, 12F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Text = "项目切换（即时生效）";
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(400, 350);
            this.MinimumSize = new Size(400, 350);
            //
            // _lblCurrent（加粗；初值代码回填）
            //
            this._lblCurrent.Location = new Point(12, 47);
            this._lblCurrent.Size = new Size(376, 24);
            this._lblCurrent.Font = new Font(this.Font, FontStyle.Bold);
            //
            // _lstProjects（双击=切换）
            //
            this._lstProjects.Location = new Point(12, 77);
            this._lstProjects.Size = new Size(376, 180);
            this._lstProjects.DoubleClick += new System.EventHandler(this.BtnSwitch_Click);
            //
            // 新建行
            //
            this._lblNew.Location = new Point(12, 271);
            this._lblNew.Size = new Size(48, 20);
            this._lblNew.Text = "新建：";
            this._txtNewName.Location = new Point(64, 267);
            this._txtNewName.Size = new Size(220, 24);
            this._btnCreate.Location = new Point(292, 266);
            this._btnCreate.Size = new Size(96, 26);
            this._btnCreate.Text = "创建";
            this._btnCreate.Click += new System.EventHandler(this.BtnCreate_Click);
            //
            // 操作行一排三按钮（各 120 宽、间距 8）
            //
            this._btnSwitch.Location = new Point(12, 301);
            this._btnSwitch.Size = new Size(120, 30);
            this._btnSwitch.Text = "切换并生效";
            this._btnSwitch.Click += new System.EventHandler(this.BtnSwitch_Click);
            this._btnDelete.Location = new Point(140, 301);
            this._btnDelete.Size = new Size(120, 30);
            this._btnDelete.Text = "删除项目";
            this._btnDelete.Click += new System.EventHandler(this.BtnDelete_Click);
            this._btnClose.Location = new Point(268, 301);
            this._btnClose.Size = new Size(120, 30);
            this._btnClose.Text = "关闭";
            this._btnClose.DialogResult = DialogResult.Cancel;
            this._btnClose.FillColor = Color.DimGray;
            this._btnClose.RectColor = Color.DimGray;
            this._btnClose.ForeColor = Color.White;
            this._btnClose.Style = Sunny.UI.UIStyle.Custom;
            //
            // 挂接
            //
            this.Controls.Add(this._lblCurrent);
            this.Controls.Add(this._lstProjects);
            this.Controls.Add(this._lblNew);
            this.Controls.Add(this._txtNewName);
            this.Controls.Add(this._btnCreate);
            this.Controls.Add(this._btnSwitch);
            this.Controls.Add(this._btnDelete);
            this.Controls.Add(this._btnClose);
            this.CancelButton = this._btnClose;
            this.ResumeLayout(false);
        }
    }
}
