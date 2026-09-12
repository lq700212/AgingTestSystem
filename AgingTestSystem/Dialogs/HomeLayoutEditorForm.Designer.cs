using System.Drawing;
using System.Windows.Forms;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 主页区域调整可视化编辑器 — 设计器部分（【V1.72.12 新增】纯代码拆分：静态边框进 Designer）。
    /// 这里只装"静态边框"：窗体属性 + 预览占位 + 数值面板（4 标签 + 4 输入框）
    /// + 底部三按钮 + 顶部说明条。以下三样仍在 HomeLayoutEditorForm.cs 里用代码做：
    /// ①_preview.Layout 赋值（吃构造传进的 layout 真参数，Designer 给不了）；
    /// ②四个 nud 初值回填（同上，依赖 layout；范围 Minimum/Maximum 在这里按
    /// HomeLayoutConfig.Range 常量设，初值 Value 在代码里设）；
    /// ③自绘预览控件本体 HomeLayoutPreviewControl（GDI 自绘类，留 .cs 不进 Designer）。
    /// 【布局】Dock 布局 + 顶 Pad 38 避开 UIForm 自绘蓝标题（V1.71 姿势）；
    /// 数值面板 4 行 Percent 等分（高 DPI 行高自适应）；
    /// 右下两按钮 Location 按 Panel 默认宽 200 算出（2,10)/(98,10)，Anchor=Right
    /// 运行时自动贴右——与原来构造时公式算出的值完全一致，别"优化"改坐标。
    /// </summary>
    partial class HomeLayoutEditorForm
    {
        /// <summary>预览自绘控件（占位；Layout 在代码里赋值）</summary>
        private HomeLayoutPreviewControl _preview;

        /// <summary>数值输入面板（2 列 × 4 行 Percent 等分）</summary>
        private TableLayoutPanel _pnlValues;

        /// <summary>顶部标题栏高标签/输入框</summary>
        private Sunny.UI.UILabel _lblTop;
        private NumericUpDown _nudTop;

        /// <summary>菜单栏高标签/输入框</summary>
        private Sunny.UI.UILabel _lblMenu;
        private NumericUpDown _nudMenu;

        /// <summary>右侧区域宽标签/输入框</summary>
        private Sunny.UI.UILabel _lblRight;
        private NumericUpDown _nudRight;

        /// <summary>状态栏高标签/输入框</summary>
        private Sunny.UI.UILabel _lblStatus;
        private NumericUpDown _nudStatus;

        /// <summary>底部按钮条</summary>
        private Panel _pnlBottom;

        /// <summary>恢复默认按钮（左下）</summary>
        private Sunny.UI.UIButton _btnRestore;

        /// <summary>取消按钮（右下，Sunny 灰）</summary>
        private Sunny.UI.UIButton _btnCancel;

        /// <summary>保存按钮（右下，绿）</summary>
        private Sunny.UI.UIButton _btnSave;

        /// <summary>顶部说明条</summary>
        private Sunny.UI.UILabel _lblTip;

        private void InitializeComponent()
        {
            this._preview = new HomeLayoutPreviewControl();
            this._pnlValues = new TableLayoutPanel();
            this._lblTop = new Sunny.UI.UILabel();
            this._nudTop = new NumericUpDown();
            this._lblMenu = new Sunny.UI.UILabel();
            this._nudMenu = new NumericUpDown();
            this._lblRight = new Sunny.UI.UILabel();
            this._nudRight = new NumericUpDown();
            this._lblStatus = new Sunny.UI.UILabel();
            this._nudStatus = new NumericUpDown();
            this._pnlBottom = new Panel();
            this._btnRestore = new Sunny.UI.UIButton();
            this._btnCancel = new Sunny.UI.UIButton();
            this._btnSave = new Sunny.UI.UIButton();
            this._lblTip = new Sunny.UI.UILabel();
            this._pnlValues.SuspendLayout();
            this._pnlBottom.SuspendLayout();
            this.SuspendLayout();
            //
            // HomeLayoutEditorForm（UIForm 蓝标题；Dock 布局加顶 Pad 避开标题区）
            //
            this.AutoScaleDimensions = new SizeF(6F, 12F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Text = "主页区域调整";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Padding = new Padding(2, 38, 2, 2);
            this.MinimumSize = new Size(560, 460);
            this.ClientSize = new Size(640, 520);
            //
            // _preview（Dock=Fill 最后加之前的占位；Layout 代码赋值）
            //
            this._preview.Dock = DockStyle.Fill;
            this._preview.BackColor = Color.White;
            this._preview.LayoutChanged += new System.EventHandler(this.Preview_LayoutChanged);
            //
            // _pnlValues（Dock=Top，高 148；4 行 Percent 等分，行高随 DPI 缩放）
            //
            this._pnlValues.Dock = DockStyle.Top;
            this._pnlValues.Height = 148;
            this._pnlValues.ColumnCount = 2;
            this._pnlValues.RowCount = 4;
            this._pnlValues.Padding = new Padding(12, 6, 12, 6);
            this._pnlValues.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            this._pnlValues.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            this._pnlValues.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            this._pnlValues.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            this._pnlValues.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            this._pnlValues.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            //
            // 四组标签 + 输入框（范围与 HomeLayoutConfig.Range 常量同步；Value 初值代码回填）
            //
            this._lblTop.Text = "顶部标题栏高 (px)";
            this._lblTop.AutoSize = true;
            this._lblTop.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom;
            this._lblTop.TextAlign = ContentAlignment.MiddleLeft;
            this._nudTop.Minimum = Models.HomeLayoutConfig.TopBarRange.Min;
            this._nudTop.Maximum = Models.HomeLayoutConfig.TopBarRange.Max;
            this._nudTop.Width = 120;
            this._nudTop.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom;
            this._nudTop.ValueChanged += new System.EventHandler(this.Nud_ValueChanged);
            this._lblMenu.Text = "菜单栏高 (px)";
            this._lblMenu.AutoSize = true;
            this._lblMenu.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom;
            this._lblMenu.TextAlign = ContentAlignment.MiddleLeft;
            this._nudMenu.Minimum = Models.HomeLayoutConfig.MenuRange.Min;
            this._nudMenu.Maximum = Models.HomeLayoutConfig.MenuRange.Max;
            this._nudMenu.Width = 120;
            this._nudMenu.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom;
            this._nudMenu.ValueChanged += new System.EventHandler(this.Nud_ValueChanged);
            this._lblRight.Text = "右侧区域宽 (px)";
            this._lblRight.AutoSize = true;
            this._lblRight.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom;
            this._lblRight.TextAlign = ContentAlignment.MiddleLeft;
            this._nudRight.Minimum = Models.HomeLayoutConfig.RightPanelRange.Min;
            this._nudRight.Maximum = Models.HomeLayoutConfig.RightPanelRange.Max;
            this._nudRight.Width = 120;
            this._nudRight.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom;
            this._nudRight.ValueChanged += new System.EventHandler(this.Nud_ValueChanged);
            this._lblStatus.Text = "状态栏高 (px)";
            this._lblStatus.AutoSize = true;
            this._lblStatus.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom;
            this._lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            this._nudStatus.Minimum = Models.HomeLayoutConfig.StatusBarRange.Min;
            this._nudStatus.Maximum = Models.HomeLayoutConfig.StatusBarRange.Max;
            this._nudStatus.Width = 120;
            this._nudStatus.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Bottom;
            this._nudStatus.ValueChanged += new System.EventHandler(this.Nud_ValueChanged);
            this._pnlValues.Controls.Add(this._lblTop, 0, 0);
            this._pnlValues.Controls.Add(this._nudTop, 1, 0);
            this._pnlValues.Controls.Add(this._lblMenu, 0, 1);
            this._pnlValues.Controls.Add(this._nudMenu, 1, 1);
            this._pnlValues.Controls.Add(this._lblRight, 0, 2);
            this._pnlValues.Controls.Add(this._nudRight, 1, 2);
            this._pnlValues.Controls.Add(this._lblStatus, 0, 3);
            this._pnlValues.Controls.Add(this._nudStatus, 1, 3);
            //
            // _pnlBottom（Dock=Bottom，高 52）
            //
            this._pnlBottom.Dock = DockStyle.Bottom;
            this._pnlBottom.Height = 52;
            this._pnlBottom.Padding = new Padding(12, 6, 12, 6);
            //
            // _btnRestore（左下）
            //
            this._btnRestore.Text = "恢复默认";
            this._btnRestore.Width = 96;
            this._btnRestore.Height = 32;
            this._btnRestore.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            this._btnRestore.Location = new Point(12, 10);
            this._btnRestore.Click += new System.EventHandler(this.BtnRestore_Click);
            //
            // _btnCancel（右下灰；Location 按 Panel 默认宽 200 算出，Anchor 运行时贴右）
            //
            this._btnCancel.Text = "取消";
            this._btnCancel.Width = 90;
            this._btnCancel.Height = 32;
            this._btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this._btnCancel.FillColor = Color.DimGray;
            this._btnCancel.RectColor = Color.DimGray;
            this._btnCancel.ForeColor = Color.White;
            this._btnCancel.Style = Sunny.UI.UIStyle.Custom;
            this._btnCancel.Location = new Point(2, 10);
            this._btnCancel.Click += new System.EventHandler(this.BtnCancel_Click);
            //
            // _btnSave（右下绿）
            //
            this._btnSave.Text = "保存";
            this._btnSave.Width = 90;
            this._btnSave.Height = 32;
            this._btnSave.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this._btnSave.FillColor = Color.LimeGreen;
            this._btnSave.RectColor = Color.LimeGreen;
            this._btnSave.ForeColor = Color.White;
            this._btnSave.Style = Sunny.UI.UIStyle.Custom;
            this._btnSave.Location = new Point(98, 10);
            this._btnSave.Click += new System.EventHandler(this.BtnSave_Click);
            this._pnlBottom.Controls.Add(this._btnRestore);
            this._pnlBottom.Controls.Add(this._btnCancel);
            this._pnlBottom.Controls.Add(this._btnSave);
            //
            // _lblTip（Dock=Top，高 26）
            //
            this._lblTip.Dock = DockStyle.Top;
            this._lblTip.Height = 26;
            this._lblTip.Text = "将鼠标移到区域边缘，光标变为双向箭头后按住拖动即可调整尺寸（单位：px）";
            this._lblTip.ForeColor = Color.FromArgb(80, 80, 80);
            this._lblTip.TextAlign = ContentAlignment.MiddleLeft;
            this._lblTip.Padding = new Padding(12, 0, 0, 0);
            //
            // 挂接（Dock 顺序：Fill 的 _preview 最先加，Top/Bottom 后加按 Z 序反排；
            // 与原来 Controls.Add(_preview/_pnlValues/_lblTip/_pnlBottom) 顺序一致）
            //
            this.Controls.Add(this._preview);
            this.Controls.Add(this._pnlValues);
            this.Controls.Add(this._lblTip);
            this.Controls.Add(this._pnlBottom);
            this._pnlValues.ResumeLayout(false);
            this._pnlBottom.ResumeLayout(false);
            this.ResumeLayout(false);
        }
    }
}
