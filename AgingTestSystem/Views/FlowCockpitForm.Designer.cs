using System.Drawing;
using System.Windows.Forms;

namespace AgingTestSystem.Views
{
    /// <summary>
    /// 流程驾驶舱 — 设计器部分（【V1.72 新增】纯代码拆分：静态边框进 Designer）。
    /// 这里只装"静态边框"：窗体属性 + 右栏空壳（标题/编辑器容器/保存/复位/关闭）
    /// + 底部状态条。以下三样仍在 FlowCockpitForm.cs 里用代码建：
    /// ①自绘画布 FlowCanvas（构造要吃 DeviceConfig/DeviceManager 真参数，
    /// Designer 给不了，只能代码 new；且它是 GDI 自绘，Designer 也摆不了里面的节点）；
    /// ②右栏的动态编辑器（按选中节点现场生成，数据驱动）；
    /// ③秒级刷新定时器（OnShown 里启停）。
    /// 【Z 序铁律】右栏 Dock=Right 先加、状态条 Dock=Bottom 再加、
    /// 画布 Dock=Fill 最后加（代码里加）——顺序错画布会盖住右栏。
    /// </summary>
    partial class FlowCockpitForm
    {
        /// <summary>右栏（固定 320px，Dock=Right；动态编辑器运行时填进 _pnlEditors）</summary>
        private Panel _pnlRight;

        /// <summary>右栏标题（选中节点名 / 未选中节点 / 连线（只读））</summary>
        private Sunny.UI.UILabel _lblNodeTitle;

        /// <summary>编辑器容器（空壳；内容按选中节点动态重建）</summary>
        private Panel _pnlEditors;

        /// <summary>保存本节点按钮（无选中/只读时禁用）</summary>
        private Sunny.UI.UIButton _btnSaveNode;

        /// <summary>复位布局按钮（节点回缺省位置）</summary>
        private Sunny.UI.UIButton _btnResetLayout;

        /// <summary>关闭按钮（Sunny 灰；保存过返回 OK，否则 Cancel）</summary>
        private Sunny.UI.UIButton _btnClose;

        /// <summary>底部状态条（项目名 | 提示）</summary>
        private Sunny.UI.UILabel _lblStatus;

        /// <summary>自绘画布（代码里按真实参数 new，Dock=Fill 最后加）</summary>
        private FlowCanvas _canvas;

        private void InitializeComponent()
        {
            this._pnlRight = new Panel();
            this._lblNodeTitle = new Sunny.UI.UILabel();
            this._pnlEditors = new Panel();
            this._btnSaveNode = new Sunny.UI.UIButton();
            this._btnResetLayout = new Sunny.UI.UIButton();
            this._btnClose = new Sunny.UI.UIButton();
            this._lblStatus = new Sunny.UI.UILabel();
            this._pnlRight.SuspendLayout();
            this.SuspendLayout();
            //
            // FlowCockpitForm（UIForm 蓝标题；Dock 布局加顶 Pad 避开 35px 标题区）
            //
            this.AutoScaleDimensions = new SizeF(6F, 12F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Text = "流程驾驶舱（点节点改配置）";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Size = new Size(1080, 700);
            this.MinimumSize = new Size(860, 560);
            this.Padding = new Padding(2, 38, 2, 2);
            //
            // _pnlRight（先加；画布 Fill 在代码里最后加，Z 序不能反）
            //
            this._pnlRight.Controls.Add(this._lblNodeTitle);
            this._pnlRight.Controls.Add(this._pnlEditors);
            this._pnlRight.Controls.Add(this._btnSaveNode);
            this._pnlRight.Controls.Add(this._btnResetLayout);
            this._pnlRight.Controls.Add(this._btnClose);
            this._pnlRight.Dock = DockStyle.Right;
            this._pnlRight.Width = 320;
            //
            // _lblNodeTitle
            //
            this._lblNodeTitle.Location = new Point(12, 12);
            this._lblNodeTitle.Size = new Size(296, 28);
            this._lblNodeTitle.Font = new Font(this.Font.FontFamily, 11F, FontStyle.Bold);
            //
            // _pnlEditors
            //
            this._pnlEditors.Location = new Point(12, 48);
            this._pnlEditors.Size = new Size(296, 480);
            this._pnlEditors.AutoScroll = true;
            //
            // _btnSaveNode（Sunny 默认蓝，主操作）
            //
            this._btnSaveNode.Location = new Point(12, 540);
            this._btnSaveNode.Size = new Size(296, 32);
            this._btnSaveNode.Text = "保存本节点";
            this._btnSaveNode.Enabled = false;
            this._btnSaveNode.Click += new System.EventHandler(this.BtnSaveNode_Click);
            //
            // _btnResetLayout（Sunny 默认蓝）
            //
            this._btnResetLayout.Location = new Point(12, 578);
            this._btnResetLayout.Size = new Size(144, 30);
            this._btnResetLayout.Text = "复位布局";
            this._btnResetLayout.Click += new System.EventHandler(this.BtnResetLayout_Click);
            //
            // _btnClose（Sunny 灰；语义=取消关闭，走灰）
            //
            this._btnClose.Location = new Point(164, 578);
            this._btnClose.Size = new Size(144, 30);
            this._btnClose.Text = "关闭";
            this._btnClose.FillColor = Color.DimGray;
            this._btnClose.RectColor = Color.DimGray;
            this._btnClose.ForeColor = Color.White;
            this._btnClose.Style = Sunny.UI.UIStyle.Custom;
            this._btnClose.Click += new System.EventHandler(this.BtnClose_Click);
            //
            // _lblStatus（Dock=Bottom，Designer 里加；画布在代码里最后加）
            //
            this._lblStatus.Dock = DockStyle.Bottom;
            this._lblStatus.Height = 26;
            this._lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            //
            // 挂接（顺序=右栏→状态条；画布 Fill 代码里最后加）
            //
            this.Controls.Add(this._pnlRight);
            this.Controls.Add(this._lblStatus);
            this._pnlRight.ResumeLayout(false);
            this.ResumeLayout(false);
        }
    }
}
