using System.Drawing;
using System.Windows.Forms;

namespace AgingTestSystem.Views
{
    /// <summary>
    /// 工艺策略 — 设计器部分（【V1.72 新增】纯代码拆分：静态边框进 Designer；
    /// 【V1.73】由流程驾驶舱改名，类/文件名全量同步）。
    /// 这里只装"静态边框"：窗体属性 + 右栏空壳（标题/编辑器容器/保存/复位/关闭）
    /// + 底部状态条。以下三样仍在 ProcessPolicyForm.cs 里用代码建：
    /// ①自绘画布 FlowCanvas（构造要吃 DeviceConfig/DeviceManager 真参数，
    /// Designer 给不了，只能代码 new；且它是 GDI 自绘，Designer 也摆不了里面的节点）；
    /// ②右栏的动态编辑器（按选中节点现场生成，数据驱动）；
    /// ③秒级刷新定时器（OnShown 里启停）。
    /// 【Z 序铁律】右栏 Dock=Right 先加、状态条 Dock=Bottom 再加、
    /// 画布 Dock=Fill 最后加（代码里加）——顺序错画布会盖住右栏。
    /// 【尺寸】1160×980：画布内容 730×885（8 节点，MES 在最下）默认整窗可见；
    /// MinimumSize 只锁到 950×700（小屏走双滚动条，画布/编辑器都带 AutoScroll）。
    /// 【V1.85】右栏顶部加预置行（标题46/下拉68/说明100，共占约90px）：
    /// 编辑器下移到146、高642，底部按钮顺延（保存794/复位关闭830）。
    /// 静态布局（坐标/文本/事件挂接）全在这里，VS 可预览；
    /// 下拉选项填充在 ProcessPolicyForm.cs 里代码做（数据源 PolicyPresets.All，
    /// Designer 里写循环/自定义项会被 VS 重写吞掉，手写保命线）。
    /// 悬停提示 _presetTip 无容器托管，随窗体 Dispose 手动释放（见 .cs）。
    /// </summary>
    partial class ProcessPolicyForm
    {
        /// <summary>右栏（固定 320px，Dock=Right；动态编辑器运行时填进 _pnlEditors）</summary>
        private Panel _pnlRight;

        /// <summary>右栏标题（选中节点名 / 未选中节点 / 连线（只读））</summary>
        private Sunny.UI.UILabel _lblNodeTitle;

        /// <summary>预置行标题（"预置策略（一键套用A/B/C）"静态文本）</summary>
        private Sunny.UI.UILabel _lblPresetTitle;

        /// <summary>预置下拉（A/B/C/自定义；选项代码填，下拉列表宽340防截断）</summary>
        private Sunny.UI.UIComboBox _cboPreset;

        /// <summary>套用预置按钮（Sunny 默认蓝，主操作）</summary>
        private Sunny.UI.UIButton _btnApplyPreset;

        /// <summary>预置说明（选中项一句话场景，灰字；全文另有悬停提示）</summary>
        private Sunny.UI.UILabel _lblPresetDesc;

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
            this._lblPresetTitle = new Sunny.UI.UILabel();
            this._cboPreset = new Sunny.UI.UIComboBox();
            this._btnApplyPreset = new Sunny.UI.UIButton();
            this._lblPresetDesc = new Sunny.UI.UILabel();
            this._pnlEditors = new Panel();
            this._btnSaveNode = new Sunny.UI.UIButton();
            this._btnResetLayout = new Sunny.UI.UIButton();
            this._btnClose = new Sunny.UI.UIButton();
            this._lblStatus = new Sunny.UI.UILabel();
            this._pnlRight.SuspendLayout();
            this.SuspendLayout();
            //
            // ProcessPolicyForm（UIForm 蓝标题；Dock 布局加顶 Pad 避开 35px 标题区）
            //
            this.AutoScaleDimensions = new SizeF(6F, 12F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Text = "工艺策略（点节点改配置）";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Size = new Size(1160, 980);
            this.MinimumSize = new Size(950, 700);
            this.Padding = new Padding(2, 38, 2, 2);
            //
            // _pnlRight（先加；画布 Fill 在代码里最后加，Z 序不能反）
            //
            this._pnlRight.Controls.Add(this._lblNodeTitle);
            this._pnlRight.Controls.Add(this._lblPresetTitle);
            this._pnlRight.Controls.Add(this._cboPreset);
            this._pnlRight.Controls.Add(this._btnApplyPreset);
            this._pnlRight.Controls.Add(this._lblPresetDesc);
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
            this._lblPresetTitle.Location = new Point(12, 46);
            this._lblPresetTitle.Size = new Size(296, 20);
            this._lblPresetTitle.Text = "预置策略（一键套用A/B/C）";
            this._cboPreset.DropDownStyle = Sunny.UI.UIDropDownStyle.DropDownList;
            this._cboPreset.Location = new Point(12, 68);
            this._cboPreset.Size = new Size(182, 28);
            this._cboPreset.DropDownWidth = 340;
            this._cboPreset.SelectedIndexChanged += new System.EventHandler(this.PresetComboChanged);
            this._btnApplyPreset.Location = new Point(200, 68);
            this._btnApplyPreset.Size = new Size(108, 28);
            this._btnApplyPreset.Text = "套用预置";
            this._btnApplyPreset.Click += new System.EventHandler(this.BtnApplyPreset_Click);
            this._lblPresetDesc.Location = new Point(12, 100);
            this._lblPresetDesc.Size = new Size(296, 42);
            this._lblPresetDesc.ForeColor = Color.Gray;
            //
            // _pnlEditors
            //
            this._pnlEditors.Location = new Point(12, 146);
            this._pnlEditors.Size = new Size(296, 642);
            this._pnlEditors.AutoScroll = true;
            //
            // _btnSaveNode（Sunny 默认蓝，主操作）
            //
            this._btnSaveNode.Location = new Point(12, 794);
            this._btnSaveNode.Size = new Size(296, 32);
            this._btnSaveNode.Text = "保存本节点";
            this._btnSaveNode.Enabled = false;
            this._btnSaveNode.Click += new System.EventHandler(this.BtnSaveNode_Click);
            //
            // _btnResetLayout（Sunny 默认蓝）
            //
            this._btnResetLayout.Location = new Point(12, 830);
            this._btnResetLayout.Size = new Size(144, 30);
            this._btnResetLayout.Text = "复位布局";
            this._btnResetLayout.Click += new System.EventHandler(this.BtnResetLayout_Click);
            //
            // _btnClose（Sunny 灰；语义=取消关闭，走灰）
            //
            this._btnClose.Location = new Point(164, 830);
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
