using System.Drawing;
using System.Windows.Forms;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 主页区域调整可视化编辑器 — 设计器部分（【V1.72.12 新增】纯代码拆分：静态边框进 Designer）。
    /// 这里只装"静态边框"：窗体属性 + 预览占位 + 数值面板（2 标签 + 2 输入框）
    /// + 底部三按钮 + 顶部说明条。以下三样仍在 HomeLayoutEditorForm.cs 里用代码做：
    /// ①_preview 自绘预览控件的创建/Layout 赋值/事件挂接（吃构造传进的 layout 真参数，
    /// Designer 给不了；【V1.72.16】_preview 的本体原先也在 Designer 里 new，
    /// 但 HomeLayoutPreviewControl 是内部自定义控件（还 hide 了基类 Layout 事件），
    /// 设计器对它的实例化/事件绑定每次打开都标脏、存盘又零 diff，纯幽灵脏，
    /// 删 Layout=null 行也去不掉，只能整机搬出 Designer，留空 Panel 占位）；
    /// ②两个 nud 初值回填（同上，依赖 layout；范围 Minimum/Maximum 在这里按
    /// HomeLayoutConfig.Range 常量设，初值 Value 在代码里设）；
    /// ③自绘预览控件本体 HomeLayoutPreviewControl（GDI 自绘类，留 .cs 不进 Designer）。
    /// 【V1.72.16 设计器稳定性三条军规（两次被 VS 重写后沉淀，违者预览即脏/运行即炸）】
    /// ①量程必须写字面值（如 180/600），禁止写 HomeLayoutConfig.RightPanelRange.Min 这类
    /// 元组成员表达式——设计器序列化器认不出，打开预览就标脏，存盘时整行删掉，
    /// 输入框变回 0~100，拖预览边缘给 240/340 直接 ArgumentOutOfRangeException，
    /// 整个可视调尺寸功能全坏（本次实锤）。改 Range 常量必须同步改这里两个数；
    /// Value=下限三行是 VS 自动补的（活值被 Minimum 顶上去，
    /// 与默认 0 对不上，不写也脏），留着别删；
    /// ②InitializeComponent 方法体里禁止写任何 // 注释——VS 重写时整段再生，
    /// 注释全删（BatchRecipe 的中文说明就是这么没的），说明一律写文件头/对应 .cs；
    /// ③本窗的 .resx 是 VS 预览自动建的空模板（无真实资源），别手删，
    /// 删了下次预览重建 + csproj 加条目，反而更脏。
    /// 【布局】Dock 布局 + 顶 Pad 38 避开 UIForm 自绘蓝标题（V1.71 姿势）；
    /// 数值面板 2 行 Percent 等分（高 DPI 行高自适应，【V1.88.28】顶栏行已删：顶栏锁死 30）；
    /// 右下两按钮 Location 按 Panel 默认宽 200 算出（2,10)/(98,10)，Anchor=Right
    /// 运行时自动贴右——与原来构造时公式算出的值完全一致，别"优化"改坐标。
    /// </summary>
    partial class HomeLayoutEditorForm
    {
        /// <summary>预览区占位面板（空壳；自绘预览控件在代码里创建后 Dock=Fill 填进来）</summary>
        private Panel _pnlPreviewHost;

        /// <summary>数值输入面板（2 列 × 2 行 Percent 等分）</summary>
        private TableLayoutPanel _pnlValues;

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
            this._pnlPreviewHost = new System.Windows.Forms.Panel();
            this._pnlValues = new System.Windows.Forms.TableLayoutPanel();
            this._lblRight = new Sunny.UI.UILabel();
            this._nudRight = new System.Windows.Forms.NumericUpDown();
            this._lblStatus = new Sunny.UI.UILabel();
            this._nudStatus = new System.Windows.Forms.NumericUpDown();
            this._pnlBottom = new System.Windows.Forms.Panel();
            this._btnRestore = new Sunny.UI.UIButton();
            this._btnCancel = new Sunny.UI.UIButton();
            this._btnSave = new Sunny.UI.UIButton();
            this._lblTip = new Sunny.UI.UILabel();
            this._pnlValues.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._nudRight)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this._nudStatus)).BeginInit();
            this._pnlBottom.SuspendLayout();
            this.SuspendLayout();
            //
            // _pnlPreviewHost（空壳占位；设计时显示白底空框，运行时由构造填入自绘预览）
            //
            this._pnlPreviewHost.BackColor = System.Drawing.Color.White;
            this._pnlPreviewHost.Dock = System.Windows.Forms.DockStyle.Fill;
            this._pnlPreviewHost.Location = new System.Drawing.Point(2, 142);
            this._pnlPreviewHost.Name = "_pnlPreviewHost";
            this._pnlPreviewHost.Size = new System.Drawing.Size(636, 324);
            this._pnlPreviewHost.TabIndex = 0;
            // 
            // _pnlValues
            // 
            this._pnlValues.ColumnCount = 2;
            this._pnlValues.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this._pnlValues.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this._pnlValues.Controls.Add(this._lblRight, 0, 0);
            this._pnlValues.Controls.Add(this._nudRight, 1, 0);
            this._pnlValues.Controls.Add(this._lblStatus, 0, 1);
            this._pnlValues.Controls.Add(this._nudStatus, 1, 1);
            this._pnlValues.Dock = System.Windows.Forms.DockStyle.Top;
            this._pnlValues.Location = new System.Drawing.Point(2, 64);
            this._pnlValues.Name = "_pnlValues";
            this._pnlValues.Padding = new System.Windows.Forms.Padding(12, 6, 12, 6);
            this._pnlValues.RowCount = 2;
            this._pnlValues.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this._pnlValues.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this._pnlValues.Size = new System.Drawing.Size(636, 78);
            this._pnlValues.TabIndex = 1;
            // 
            // _lblRight
            // 
            this._lblRight.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this._lblRight.AutoSize = true;
            this._lblRight.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this._lblRight.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this._lblRight.Location = new System.Drawing.Point(15, 6);
            this._lblRight.Name = "_lblRight";
            this._lblRight.Size = new System.Drawing.Size(127, 34);
            this._lblRight.TabIndex = 0;
            this._lblRight.Text = "右侧区域宽 (px)";
            this._lblRight.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _nudRight
            // 
            this._nudRight.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this._nudRight.Location = new System.Drawing.Point(321, 43);
            this._nudRight.Maximum = new decimal(new int[] {
            600,
            0,
            0,
            0});
            this._nudRight.Minimum = new decimal(new int[] {
            180,
            0,
            0,
            0});
            this._nudRight.Name = "_nudRight";
            this._nudRight.Size = new System.Drawing.Size(120, 26);
            this._nudRight.TabIndex = 1;
            this._nudRight.Value = new decimal(new int[] {
            180,
            0,
            0,
            0});
            this._nudRight.ValueChanged += new System.EventHandler(this.Nud_ValueChanged);
            // 
            // _lblStatus
            // 
            this._lblStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this._lblStatus.AutoSize = true;
            this._lblStatus.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this._lblStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this._lblStatus.Location = new System.Drawing.Point(15, 74);
            this._lblStatus.Name = "_lblStatus";
            this._lblStatus.Size = new System.Drawing.Size(111, 34);
            this._lblStatus.TabIndex = 2;
            this._lblStatus.Text = "状态栏高 (px)";
            this._lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // _nudStatus
            // 
            this._nudStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this._nudStatus.Location = new System.Drawing.Point(321, 77);
            this._nudStatus.Maximum = new decimal(new int[] {
            60,
            0,
            0,
            0});
            this._nudStatus.Minimum = new decimal(new int[] {
            15,
            0,
            0,
            0});
            this._nudStatus.Name = "_nudStatus";
            this._nudStatus.Size = new System.Drawing.Size(120, 26);
            this._nudStatus.TabIndex = 3;
            this._nudStatus.Value = new decimal(new int[] {
            15,
            0,
            0,
            0});
            this._nudStatus.ValueChanged += new System.EventHandler(this.Nud_ValueChanged);
            // 
            // _pnlBottom
            // 
            this._pnlBottom.Controls.Add(this._btnRestore);
            this._pnlBottom.Controls.Add(this._btnCancel);
            this._pnlBottom.Controls.Add(this._btnSave);
            this._pnlBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            this._pnlBottom.Location = new System.Drawing.Point(2, 466);
            this._pnlBottom.Name = "_pnlBottom";
            this._pnlBottom.Padding = new System.Windows.Forms.Padding(12, 6, 12, 6);
            this._pnlBottom.Size = new System.Drawing.Size(636, 52);
            this._pnlBottom.TabIndex = 3;
            // 
            // _btnRestore
            // 
            this._btnRestore.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this._btnRestore.Cursor = System.Windows.Forms.Cursors.Hand;
            this._btnRestore.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this._btnRestore.Location = new System.Drawing.Point(12, 10);
            this._btnRestore.MinimumSize = new System.Drawing.Size(1, 1);
            this._btnRestore.Name = "_btnRestore";
            this._btnRestore.Size = new System.Drawing.Size(96, 32);
            this._btnRestore.TabIndex = 0;
            this._btnRestore.Text = "恢复默认";
            this._btnRestore.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this._btnRestore.Click += new System.EventHandler(this.BtnRestore_Click);
            // 
            // _btnCancel
            // 
            this._btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._btnCancel.Cursor = System.Windows.Forms.Cursors.Hand;
            this._btnCancel.FillColor = System.Drawing.Color.DimGray;
            this._btnCancel.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this._btnCancel.Location = new System.Drawing.Point(438, 10);
            this._btnCancel.MinimumSize = new System.Drawing.Size(1, 1);
            this._btnCancel.Name = "_btnCancel";
            this._btnCancel.RectColor = System.Drawing.Color.DimGray;
            this._btnCancel.Size = new System.Drawing.Size(90, 32);
            this._btnCancel.Style = Sunny.UI.UIStyle.Custom;
            this._btnCancel.TabIndex = 1;
            this._btnCancel.Text = "取消";
            this._btnCancel.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this._btnCancel.Click += new System.EventHandler(this.BtnCancel_Click);
            // 
            // _btnSave
            // 
            this._btnSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this._btnSave.Cursor = System.Windows.Forms.Cursors.Hand;
            this._btnSave.FillColor = System.Drawing.Color.ForestGreen;
            this._btnSave.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this._btnSave.ForeColor = System.Drawing.Color.White;
            this._btnSave.Location = new System.Drawing.Point(534, 10);
            this._btnSave.MinimumSize = new System.Drawing.Size(1, 1);
            this._btnSave.Name = "_btnSave";
            this._btnSave.RectColor = System.Drawing.Color.ForestGreen;
            this._btnSave.Size = new System.Drawing.Size(90, 32);
            this._btnSave.Style = Sunny.UI.UIStyle.Custom;
            this._btnSave.TabIndex = 2;
            this._btnSave.Text = "保存";
            this._btnSave.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this._btnSave.Click += new System.EventHandler(this.BtnSave_Click);
            // 
            // _lblTip
            // 
            this._lblTip.Dock = System.Windows.Forms.DockStyle.Top;
            this._lblTip.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this._lblTip.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(80)))));
            this._lblTip.Location = new System.Drawing.Point(2, 38);
            this._lblTip.Name = "_lblTip";
            this._lblTip.Padding = new System.Windows.Forms.Padding(12, 0, 0, 0);
            this._lblTip.Size = new System.Drawing.Size(636, 26);
            this._lblTip.TabIndex = 2;
            this._lblTip.Text = "将鼠标移到区域边缘，光标变为双向箭头后按住拖动即可调整尺寸（单位：px）";
            this._lblTip.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // HomeLayoutEditorForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(640, 520);
            this.Controls.Add(this._pnlPreviewHost);
            this.Controls.Add(this._pnlValues);
            this.Controls.Add(this._lblTip);
            this.Controls.Add(this._pnlBottom);
            this.MinimumSize = new System.Drawing.Size(560, 460);
            this.Name = "HomeLayoutEditorForm";
            this.Padding = new System.Windows.Forms.Padding(2, 38, 2, 2);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "主页区域调整";
            this.ZoomScaleRect = new System.Drawing.Rectangle(15, 15, 640, 520);
            this._pnlValues.ResumeLayout(false);
            this._pnlValues.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._nudRight)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this._nudStatus)).EndInit();
            this._pnlBottom.ResumeLayout(false);
            this.ResumeLayout(false);

        }
    }
}
