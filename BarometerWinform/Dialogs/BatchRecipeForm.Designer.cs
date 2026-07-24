namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 批量设置配方窗口 —— 设计器自动生成部分
    ///
    /// 【界面说明】
    /// 本窗口用于批量设置配方参数，支持将配置好的配方参数加入队列，
    /// 以便后续批量应用到多个选中的气压表面板。
    ///
    /// 【控件布局】
    /// ┌─────────────────────────────────────────────┐
    /// │ 批量设置设置配方窗口                         │  ← 标题栏
    /// ├─────────────────────────────────────────────┤
    /// │ 配方名称：[____________]                    │  ← 配方名称输入框
    /// │ 延时时间1：[__]:[__]:[__]                   │  ← 延时时间1（时:分:秒）
    /// │ 延时时间2：[__]:[__]:[__]                   │  ← 延时时间2（时:分:秒）
    /// │ 启动时间：[__]:[__]:[__]                    │  ← 启动时间（时:分:秒）
    /// │ 极限温度：[____] °C                         │  ← 极限温度输入框
    /// ├─────────────────────────────────────────────┤
    /// │         [加入队列]                          │  ← 加入队列按钮
    /// │         [关闭窗口]                          │  ← 关闭窗口按钮
    /// └─────────────────────────────────────────────┘
    /// </summary>
    partial class BatchRecipeForm
    {
        /// <summary>
        /// 必需的设计器变量
        /// 用于管理设计器创建的组件资源
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源
        /// 在窗体被销毁时调用，释放组件资源
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要使用代码编辑器修改此方法的内容
        /// 此方法负责创建所有控件并设置布局属性
        /// </summary>
        private void InitializeComponent()
        {
            this.tableLayoutPanelMain = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanelInput = new System.Windows.Forms.TableLayoutPanel();
            this.lblRecipeNameLabel = new System.Windows.Forms.Label();
            this.txtRecipeName = new System.Windows.Forms.TextBox();
            this.lblDelayTime1Label = new System.Windows.Forms.Label();
            this.tableLayoutPanelDelay1 = new System.Windows.Forms.TableLayoutPanel();
            this.txtDelay1Hour = new System.Windows.Forms.TextBox();
            this.lblDelay1Colon1 = new System.Windows.Forms.Label();
            this.txtDelay1Minute = new System.Windows.Forms.TextBox();
            this.lblDelay1Colon2 = new System.Windows.Forms.Label();
            this.txtDelay1Second = new System.Windows.Forms.TextBox();
            this.lblDelayTime2Label = new System.Windows.Forms.Label();
            this.tableLayoutPanelDelay2 = new System.Windows.Forms.TableLayoutPanel();
            this.txtDelay2Hour = new System.Windows.Forms.TextBox();
            this.lblDelay2Colon1 = new System.Windows.Forms.Label();
            this.txtDelay2Minute = new System.Windows.Forms.TextBox();
            this.lblDelay2Colon2 = new System.Windows.Forms.Label();
            this.txtDelay2Second = new System.Windows.Forms.TextBox();
            this.lblStartTimeLabel = new System.Windows.Forms.Label();
            this.tableLayoutPanelStart = new System.Windows.Forms.TableLayoutPanel();
            this.txtStartHour = new System.Windows.Forms.TextBox();
            this.lblStartColon1 = new System.Windows.Forms.Label();
            this.txtStartMinute = new System.Windows.Forms.TextBox();
            this.lblStartColon2 = new System.Windows.Forms.Label();
            this.txtStartSecond = new System.Windows.Forms.TextBox();
            this.lblLimitTempLabel = new System.Windows.Forms.Label();
            this.tableLayoutPanelTemp = new System.Windows.Forms.TableLayoutPanel();
            this.txtLimitTemp = new System.Windows.Forms.TextBox();
            this.lblTempUnit = new System.Windows.Forms.Label();
            this.panelButtons = new System.Windows.Forms.Panel();
            this.btnAddToQueue = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.tableLayoutPanelMain.SuspendLayout();
            this.tableLayoutPanelInput.SuspendLayout();
            this.tableLayoutPanelDelay1.SuspendLayout();
            this.tableLayoutPanelDelay2.SuspendLayout();
            this.tableLayoutPanelStart.SuspendLayout();
            this.tableLayoutPanelTemp.SuspendLayout();
            this.panelButtons.SuspendLayout();
            this.SuspendLayout();
            //
            // tableLayoutPanelMain - 主布局容器（2行：输入区域/按钮区域）
            //
            this.tableLayoutPanelMain.ColumnCount = 1;
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.Controls.Add(this.tableLayoutPanelInput, 0, 0);
            this.tableLayoutPanelMain.Controls.Add(this.panelButtons, 0, 1);
            this.tableLayoutPanelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelMain.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanelMain.Name = "tableLayoutPanelMain";
            this.tableLayoutPanelMain.RowCount = 2;
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this.tableLayoutPanelMain.Size = new System.Drawing.Size(480, 320);
            this.tableLayoutPanelMain.TabIndex = 0;
            //
            // tableLayoutPanelInput - 输入区域布局容器（5行：配方名称/延时时间1/延时时间2/启动时间/极限温度）
            //
            this.tableLayoutPanelInput.ColumnCount = 2;
            this.tableLayoutPanelInput.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.tableLayoutPanelInput.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelInput.Controls.Add(this.lblRecipeNameLabel, 0, 0);
            this.tableLayoutPanelInput.Controls.Add(this.txtRecipeName, 1, 0);
            this.tableLayoutPanelInput.Controls.Add(this.lblDelayTime1Label, 0, 1);
            this.tableLayoutPanelInput.Controls.Add(this.tableLayoutPanelDelay1, 1, 1);
            this.tableLayoutPanelInput.Controls.Add(this.lblDelayTime2Label, 0, 2);
            this.tableLayoutPanelInput.Controls.Add(this.tableLayoutPanelDelay2, 1, 2);
            this.tableLayoutPanelInput.Controls.Add(this.lblStartTimeLabel, 0, 3);
            this.tableLayoutPanelInput.Controls.Add(this.tableLayoutPanelStart, 1, 3);
            this.tableLayoutPanelInput.Controls.Add(this.lblLimitTempLabel, 0, 4);
            this.tableLayoutPanelInput.Controls.Add(this.tableLayoutPanelTemp, 1, 4);
            this.tableLayoutPanelInput.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelInput.Location = new System.Drawing.Point(3, 3);
            this.tableLayoutPanelInput.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
            this.tableLayoutPanelInput.Name = "tableLayoutPanelInput";
            this.tableLayoutPanelInput.RowCount = 5;
            this.tableLayoutPanelInput.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanelInput.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanelInput.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanelInput.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanelInput.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanelInput.Size = new System.Drawing.Size(474, 194);
            this.tableLayoutPanelInput.TabIndex = 0;
            //
            // lblRecipeNameLabel - "配方名称"标签
            //
            this.lblRecipeNameLabel.AutoSize = true;
            this.lblRecipeNameLabel.Location = new System.Drawing.Point(3, 10);
            this.lblRecipeNameLabel.Name = "lblRecipeNameLabel";
            this.lblRecipeNameLabel.Size = new System.Drawing.Size(65, 12);
            this.lblRecipeNameLabel.TabIndex = 0;
            this.lblRecipeNameLabel.Text = "配方名称：";
            this.lblRecipeNameLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // txtRecipeName - 配方名称输入框
            //
            this.txtRecipeName.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.txtRecipeName.Location = new System.Drawing.Point(103, 7);
            this.txtRecipeName.Name = "txtRecipeName";
            this.txtRecipeName.Size = new System.Drawing.Size(368, 21);
            this.txtRecipeName.TabIndex = 1;
            //
            // lblDelayTime1Label - "延时时间1"标签
            //
            this.lblDelayTime1Label.AutoSize = true;
            this.lblDelayTime1Label.Location = new System.Drawing.Point(3, 50);
            this.lblDelayTime1Label.Name = "lblDelayTime1Label";
            this.lblDelayTime1Label.Size = new System.Drawing.Size(65, 12);
            this.lblDelayTime1Label.TabIndex = 2;
            this.lblDelayTime1Label.Text = "延时时间1：";
            this.lblDelayTime1Label.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // tableLayoutPanelDelay1 - 延时时间1输入布局（时:分:秒）
            //
            this.tableLayoutPanelDelay1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanelDelay1.ColumnCount = 5;
            this.tableLayoutPanelDelay1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanelDelay1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanelDelay1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanelDelay1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanelDelay1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanelDelay1.Controls.Add(this.txtDelay1Hour, 0, 0);
            this.tableLayoutPanelDelay1.Controls.Add(this.lblDelay1Colon1, 1, 0);
            this.tableLayoutPanelDelay1.Controls.Add(this.txtDelay1Minute, 2, 0);
            this.tableLayoutPanelDelay1.Controls.Add(this.lblDelay1Colon2, 3, 0);
            this.tableLayoutPanelDelay1.Controls.Add(this.txtDelay1Second, 4, 0);
            this.tableLayoutPanelDelay1.Location = new System.Drawing.Point(103, 47);
            this.tableLayoutPanelDelay1.Name = "tableLayoutPanelDelay1";
            this.tableLayoutPanelDelay1.RowCount = 1;
            this.tableLayoutPanelDelay1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelDelay1.Size = new System.Drawing.Size(368, 26);
            this.tableLayoutPanelDelay1.TabIndex = 3;
            //
            // txtDelay1Hour - 延时时间1-小时输入框
            //
            this.txtDelay1Hour.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtDelay1Hour.Location = new System.Drawing.Point(3, 3);
            this.txtDelay1Hour.MaxLength = 2;
            this.txtDelay1Hour.Name = "txtDelay1Hour";
            this.txtDelay1Hour.Size = new System.Drawing.Size(84, 21);
            this.txtDelay1Hour.TabIndex = 0;
            this.txtDelay1Hour.Text = "0";
            //
            // lblDelay1Colon1 - 延时时间1-第一个冒号分隔符
            //
            this.lblDelay1Colon1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.lblDelay1Colon1.AutoSize = true;
            this.lblDelay1Colon1.Location = new System.Drawing.Point(93, 7);
            this.lblDelay1Colon1.Name = "lblDelay1Colon1";
            this.lblDelay1Colon1.Size = new System.Drawing.Size(14, 12);
            this.lblDelay1Colon1.TabIndex = 1;
            this.lblDelay1Colon1.Text = ":";
            //
            // txtDelay1Minute - 延时时间1-分钟输入框
            //
            this.txtDelay1Minute.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtDelay1Minute.Location = new System.Drawing.Point(113, 3);
            this.txtDelay1Minute.MaxLength = 2;
            this.txtDelay1Minute.Name = "txtDelay1Minute";
            this.txtDelay1Minute.Size = new System.Drawing.Size(84, 21);
            this.txtDelay1Minute.TabIndex = 2;
            this.txtDelay1Minute.Text = "0";
            //
            // lblDelay1Colon2 - 延时时间1-第二个冒号分隔符
            //
            this.lblDelay1Colon2.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.lblDelay1Colon2.AutoSize = true;
            this.lblDelay1Colon2.Location = new System.Drawing.Point(203, 7);
            this.lblDelay1Colon2.Name = "lblDelay1Colon2";
            this.lblDelay1Colon2.Size = new System.Drawing.Size(14, 12);
            this.lblDelay1Colon2.TabIndex = 3;
            this.lblDelay1Colon2.Text = ":";
            //
            // txtDelay1Second - 延时时间1-秒输入框
            //
            this.txtDelay1Second.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtDelay1Second.Location = new System.Drawing.Point(223, 3);
            this.txtDelay1Second.MaxLength = 2;
            this.txtDelay1Second.Name = "txtDelay1Second";
            this.txtDelay1Second.Size = new System.Drawing.Size(142, 21);
            this.txtDelay1Second.TabIndex = 4;
            this.txtDelay1Second.Text = "0";
            //
            // lblDelayTime2Label - "延时时间2"标签
            //
            this.lblDelayTime2Label.AutoSize = true;
            this.lblDelayTime2Label.Location = new System.Drawing.Point(3, 90);
            this.lblDelayTime2Label.Name = "lblDelayTime2Label";
            this.lblDelayTime2Label.Size = new System.Drawing.Size(65, 12);
            this.lblDelayTime2Label.TabIndex = 4;
            this.lblDelayTime2Label.Text = "延时时间2：";
            this.lblDelayTime2Label.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // tableLayoutPanelDelay2 - 延时时间2输入布局（时:分:秒）
            //
            this.tableLayoutPanelDelay2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanelDelay2.ColumnCount = 5;
            this.tableLayoutPanelDelay2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanelDelay2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanelDelay2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanelDelay2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanelDelay2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanelDelay2.Controls.Add(this.txtDelay2Hour, 0, 0);
            this.tableLayoutPanelDelay2.Controls.Add(this.lblDelay2Colon1, 1, 0);
            this.tableLayoutPanelDelay2.Controls.Add(this.txtDelay2Minute, 2, 0);
            this.tableLayoutPanelDelay2.Controls.Add(this.lblDelay2Colon2, 3, 0);
            this.tableLayoutPanelDelay2.Controls.Add(this.txtDelay2Second, 4, 0);
            this.tableLayoutPanelDelay2.Location = new System.Drawing.Point(103, 87);
            this.tableLayoutPanelDelay2.Name = "tableLayoutPanelDelay2";
            this.tableLayoutPanelDelay2.RowCount = 1;
            this.tableLayoutPanelDelay2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelDelay2.Size = new System.Drawing.Size(368, 26);
            this.tableLayoutPanelDelay2.TabIndex = 5;
            //
            // txtDelay2Hour - 延时时间2-小时输入框
            //
            this.txtDelay2Hour.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtDelay2Hour.Location = new System.Drawing.Point(3, 3);
            this.txtDelay2Hour.MaxLength = 2;
            this.txtDelay2Hour.Name = "txtDelay2Hour";
            this.txtDelay2Hour.Size = new System.Drawing.Size(84, 21);
            this.txtDelay2Hour.TabIndex = 0;
            this.txtDelay2Hour.Text = "0";
            //
            // lblDelay2Colon1 - 延时时间2-第一个冒号分隔符
            //
            this.lblDelay2Colon1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.lblDelay2Colon1.AutoSize = true;
            this.lblDelay2Colon1.Location = new System.Drawing.Point(93, 7);
            this.lblDelay2Colon1.Name = "lblDelay2Colon1";
            this.lblDelay2Colon1.Size = new System.Drawing.Size(14, 12);
            this.lblDelay2Colon1.TabIndex = 1;
            this.lblDelay2Colon1.Text = ":";
            //
            // txtDelay2Minute - 延时时间2-分钟输入框
            //
            this.txtDelay2Minute.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtDelay2Minute.Location = new System.Drawing.Point(113, 3);
            this.txtDelay2Minute.MaxLength = 2;
            this.txtDelay2Minute.Name = "txtDelay2Minute";
            this.txtDelay2Minute.Size = new System.Drawing.Size(84, 21);
            this.txtDelay2Minute.TabIndex = 2;
            this.txtDelay2Minute.Text = "0";
            //
            // lblDelay2Colon2 - 延时时间2-第二个冒号分隔符
            //
            this.lblDelay2Colon2.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.lblDelay2Colon2.AutoSize = true;
            this.lblDelay2Colon2.Location = new System.Drawing.Point(203, 7);
            this.lblDelay2Colon2.Name = "lblDelay2Colon2";
            this.lblDelay2Colon2.Size = new System.Drawing.Size(14, 12);
            this.lblDelay2Colon2.TabIndex = 3;
            this.lblDelay2Colon2.Text = ":";
            //
            // txtDelay2Second - 延时时间2-秒输入框
            //
            this.txtDelay2Second.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtDelay2Second.Location = new System.Drawing.Point(223, 3);
            this.txtDelay2Second.MaxLength = 2;
            this.txtDelay2Second.Name = "txtDelay2Second";
            this.txtDelay2Second.Size = new System.Drawing.Size(142, 21);
            this.txtDelay2Second.TabIndex = 4;
            this.txtDelay2Second.Text = "0";
            //
            // lblStartTimeLabel - "启动时间"标签
            //
            this.lblStartTimeLabel.AutoSize = true;
            this.lblStartTimeLabel.Location = new System.Drawing.Point(3, 130);
            this.lblStartTimeLabel.Name = "lblStartTimeLabel";
            this.lblStartTimeLabel.Size = new System.Drawing.Size(65, 12);
            this.lblStartTimeLabel.TabIndex = 6;
            this.lblStartTimeLabel.Text = "启动时间：";
            this.lblStartTimeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // tableLayoutPanelStart - 启动时间输入布局（时:分:秒）
            //
            this.tableLayoutPanelStart.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanelStart.ColumnCount = 5;
            this.tableLayoutPanelStart.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanelStart.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanelStart.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanelStart.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanelStart.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanelStart.Controls.Add(this.txtStartHour, 0, 0);
            this.tableLayoutPanelStart.Controls.Add(this.lblStartColon1, 1, 0);
            this.tableLayoutPanelStart.Controls.Add(this.txtStartMinute, 2, 0);
            this.tableLayoutPanelStart.Controls.Add(this.lblStartColon2, 3, 0);
            this.tableLayoutPanelStart.Controls.Add(this.txtStartSecond, 4, 0);
            this.tableLayoutPanelStart.Location = new System.Drawing.Point(103, 127);
            this.tableLayoutPanelStart.Name = "tableLayoutPanelStart";
            this.tableLayoutPanelStart.RowCount = 1;
            this.tableLayoutPanelStart.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelStart.Size = new System.Drawing.Size(368, 26);
            this.tableLayoutPanelStart.TabIndex = 7;
            //
            // txtStartHour - 启动时间-小时输入框
            //
            this.txtStartHour.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtStartHour.Location = new System.Drawing.Point(3, 3);
            this.txtStartHour.MaxLength = 2;
            this.txtStartHour.Name = "txtStartHour";
            this.txtStartHour.Size = new System.Drawing.Size(84, 21);
            this.txtStartHour.TabIndex = 0;
            this.txtStartHour.Text = "0";
            //
            // lblStartColon1 - 启动时间-第一个冒号分隔符
            //
            this.lblStartColon1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.lblStartColon1.AutoSize = true;
            this.lblStartColon1.Location = new System.Drawing.Point(93, 7);
            this.lblStartColon1.Name = "lblStartColon1";
            this.lblStartColon1.Size = new System.Drawing.Size(14, 12);
            this.lblStartColon1.TabIndex = 1;
            this.lblStartColon1.Text = ":";
            //
            // txtStartMinute - 启动时间-分钟输入框
            //
            this.txtStartMinute.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtStartMinute.Location = new System.Drawing.Point(113, 3);
            this.txtStartMinute.MaxLength = 2;
            this.txtStartMinute.Name = "txtStartMinute";
            this.txtStartMinute.Size = new System.Drawing.Size(84, 21);
            this.txtStartMinute.TabIndex = 2;
            this.txtStartMinute.Text = "0";
            //
            // lblStartColon2 - 启动时间-第二个冒号分隔符
            //
            this.lblStartColon2.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.lblStartColon2.AutoSize = true;
            this.lblStartColon2.Location = new System.Drawing.Point(203, 7);
            this.lblStartColon2.Name = "lblStartColon2";
            this.lblStartColon2.Size = new System.Drawing.Size(14, 12);
            this.lblStartColon2.TabIndex = 3;
            this.lblStartColon2.Text = ":";
            //
            // txtStartSecond - 启动时间-秒输入框
            //
            this.txtStartSecond.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtStartSecond.Location = new System.Drawing.Point(223, 3);
            this.txtStartSecond.MaxLength = 2;
            this.txtStartSecond.Name = "txtStartSecond";
            this.txtStartSecond.Size = new System.Drawing.Size(142, 21);
            this.txtStartSecond.TabIndex = 4;
            this.txtStartSecond.Text = "0";
            //
            // lblLimitTempLabel - "极限温度"标签
            //
            this.lblLimitTempLabel.AutoSize = true;
            this.lblLimitTempLabel.Location = new System.Drawing.Point(3, 170);
            this.lblLimitTempLabel.Name = "lblLimitTempLabel";
            this.lblLimitTempLabel.Size = new System.Drawing.Size(65, 12);
            this.lblLimitTempLabel.TabIndex = 8;
            this.lblLimitTempLabel.Text = "极限温度：";
            this.lblLimitTempLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // tableLayoutPanelTemp - 极限温度输入布局（数值 + 单位）
            //
            this.tableLayoutPanelTemp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanelTemp.ColumnCount = 2;
            this.tableLayoutPanelTemp.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 60F));
            this.tableLayoutPanelTemp.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 40F));
            this.tableLayoutPanelTemp.Controls.Add(this.txtLimitTemp, 0, 0);
            this.tableLayoutPanelTemp.Controls.Add(this.lblTempUnit, 1, 0);
            this.tableLayoutPanelTemp.Location = new System.Drawing.Point(103, 167);
            this.tableLayoutPanelTemp.Name = "tableLayoutPanelTemp";
            this.tableLayoutPanelTemp.RowCount = 1;
            this.tableLayoutPanelTemp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelTemp.Size = new System.Drawing.Size(368, 26);
            this.tableLayoutPanelTemp.TabIndex = 9;
            //
            // txtLimitTemp - 极限温度值输入框
            //
            this.txtLimitTemp.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtLimitTemp.Location = new System.Drawing.Point(3, 3);
            this.txtLimitTemp.MaxLength = 3;
            this.txtLimitTemp.Name = "txtLimitTemp";
            this.txtLimitTemp.Size = new System.Drawing.Size(214, 21);
            this.txtLimitTemp.TabIndex = 0;
            this.txtLimitTemp.Text = "50";
            //
            // lblTempUnit - 温度单位标签（°C）
            //
            this.lblTempUnit.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblTempUnit.AutoSize = true;
            this.lblTempUnit.Location = new System.Drawing.Point(223, 7);
            this.lblTempUnit.Name = "lblTempUnit";
            this.lblTempUnit.Size = new System.Drawing.Size(23, 12);
            this.lblTempUnit.TabIndex = 1;
            this.lblTempUnit.Text = "°C";
            //
            // panelButtons - 底部按钮面板（2个按钮：加入队列/关闭窗口）
            //
            this.panelButtons.Controls.Add(this.btnAddToQueue);
            this.panelButtons.Controls.Add(this.btnClose);
            this.panelButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelButtons.Location = new System.Drawing.Point(3, 197);
            this.panelButtons.Margin = new System.Windows.Forms.Padding(3, 0, 3, 3);
            this.panelButtons.Name = "panelButtons";
            this.panelButtons.Size = new System.Drawing.Size(474, 77);
            this.panelButtons.TabIndex = 1;
            //
            // btnAddToQueue - 加入队列按钮
            //
            this.btnAddToQueue.Location = new System.Drawing.Point(150, 10);
            this.btnAddToQueue.Name = "btnAddToQueue";
            this.btnAddToQueue.Size = new System.Drawing.Size(170, 30);
            this.btnAddToQueue.TabIndex = 0;
            this.btnAddToQueue.Text = "加入队列";
            this.btnAddToQueue.Click += new System.EventHandler(this.btnAddToQueue_Click);
            //
            // btnClose - 关闭窗口按钮
            //
            this.btnClose.Location = new System.Drawing.Point(150, 46);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(170, 30);
            this.btnClose.TabIndex = 1;
            this.btnClose.Text = "关闭窗口";
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            //
            // BatchRecipeForm - 窗体自身属性设置
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(480, 320);
            this.Controls.Add(this.tableLayoutPanelMain);
            this.Name = "BatchRecipeForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "批量设置设置配方窗口";
            this.tableLayoutPanelMain.ResumeLayout(false);
            this.tableLayoutPanelInput.ResumeLayout(false);
            this.tableLayoutPanelInput.PerformLayout();
            this.tableLayoutPanelDelay1.ResumeLayout(false);
            this.tableLayoutPanelDelay1.PerformLayout();
            this.tableLayoutPanelDelay2.ResumeLayout(false);
            this.tableLayoutPanelDelay2.PerformLayout();
            this.tableLayoutPanelStart.ResumeLayout(false);
            this.tableLayoutPanelStart.PerformLayout();
            this.tableLayoutPanelTemp.ResumeLayout(false);
            this.tableLayoutPanelTemp.PerformLayout();
            this.panelButtons.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        // 控件字段声明区域
        // 这些字段在两个 partial 文件中共享（本文件赋值，.cs文件使用）

        /// <summary>主布局容器（2行：输入区域/按钮区域）</summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelMain;
        /// <summary>输入区域布局容器（5行：配方名称/延时时间1/延时时间2/启动时间/极限温度）</summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelInput;
        /// <summary>"配方名称"标签</summary>
        private System.Windows.Forms.Label lblRecipeNameLabel;
        /// <summary>配方名称输入框</summary>
        private System.Windows.Forms.TextBox txtRecipeName;
        /// <summary>"延时时间1"标签</summary>
        private System.Windows.Forms.Label lblDelayTime1Label;
        /// <summary>延时时间1输入布局（时:分:秒）</summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelDelay1;
        /// <summary>延时时间1-小时输入框</summary>
        private System.Windows.Forms.TextBox txtDelay1Hour;
        /// <summary>延时时间1-第一个冒号分隔符</summary>
        private System.Windows.Forms.Label lblDelay1Colon1;
        /// <summary>延时时间1-分钟输入框</summary>
        private System.Windows.Forms.TextBox txtDelay1Minute;
        /// <summary>延时时间1-第二个冒号分隔符</summary>
        private System.Windows.Forms.Label lblDelay1Colon2;
        /// <summary>延时时间1-秒输入框</summary>
        private System.Windows.Forms.TextBox txtDelay1Second;
        /// <summary>"延时时间2"标签</summary>
        private System.Windows.Forms.Label lblDelayTime2Label;
        /// <summary>延时时间2输入布局（时:分:秒）</summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelDelay2;
        /// <summary>延时时间2-小时输入框</summary>
        private System.Windows.Forms.TextBox txtDelay2Hour;
        /// <summary>延时时间2-第一个冒号分隔符</summary>
        private System.Windows.Forms.Label lblDelay2Colon1;
        /// <summary>延时时间2-分钟输入框</summary>
        private System.Windows.Forms.TextBox txtDelay2Minute;
        /// <summary>延时时间2-第二个冒号分隔符</summary>
        private System.Windows.Forms.Label lblDelay2Colon2;
        /// <summary>延时时间2-秒输入框</summary>
        private System.Windows.Forms.TextBox txtDelay2Second;
        /// <summary>"启动时间"标签</summary>
        private System.Windows.Forms.Label lblStartTimeLabel;
        /// <summary>启动时间输入布局（时:分:秒）</summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelStart;
        /// <summary>启动时间-小时输入框</summary>
        private System.Windows.Forms.TextBox txtStartHour;
        /// <summary>启动时间-第一个冒号分隔符</summary>
        private System.Windows.Forms.Label lblStartColon1;
        /// <summary>启动时间-分钟输入框</summary>
        private System.Windows.Forms.TextBox txtStartMinute;
        /// <summary>启动时间-第二个冒号分隔符</summary>
        private System.Windows.Forms.Label lblStartColon2;
        /// <summary>启动时间-秒输入框</summary>
        private System.Windows.Forms.TextBox txtStartSecond;
        /// <summary>"极限温度"标签</summary>
        private System.Windows.Forms.Label lblLimitTempLabel;
        /// <summary>极限温度输入布局（数值 + 单位）</summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelTemp;
        /// <summary>极限温度值输入框</summary>
        private System.Windows.Forms.TextBox txtLimitTemp;
        /// <summary>温度单位标签（°C）</summary>
        private System.Windows.Forms.Label lblTempUnit;
        /// <summary>底部按钮面板（2个按钮：加入队列/关闭窗口）</summary>
        private System.Windows.Forms.Panel panelButtons;
        /// <summary>加入队列按钮</summary>
        private System.Windows.Forms.Button btnAddToQueue;
        /// <summary>关闭窗口按钮</summary>
        private System.Windows.Forms.Button btnClose;
    }
}