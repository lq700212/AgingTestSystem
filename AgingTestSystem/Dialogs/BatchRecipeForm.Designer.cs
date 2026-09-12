using System;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 批量设置配方窗口 —— 设计器自动生成部分
    ///
    /// 【界面说明】
    /// 本窗口用于批量设置配方参数，支持将配置好的配方参数加入队列，
    /// 以便后续批量应用到多个选中的气压表面板。
    ///
    /// 【控件布局（V1.28）】
    /// ┌─────────────────────────────────────────────┐
    /// │ 批量设置设置配方窗口                         │  ← 标题栏
    /// ├─────────────────────────────────────────────┤
    /// │ 配方名称：[____________]                    │  ← 配方名称输入框
    /// │ 延时时间：[__]:[__]:[__]                    │  ← 延时时间（时:分:秒，NumericUpDown）
    /// │ 启动时间：[__]:[__]:[__]                    │  ← 启动时间（时:分:秒，NumericUpDown）
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
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }
                // 释放配方自动检索资源（V1.29 新增）
                var provider = this.GetType().GetField("_recipeAutoComplete",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (provider?.GetValue(this) is IDisposable disposable)
                {
                    disposable.Dispose();
                }
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
            this.lblRecipeNameLabel = new Sunny.UI.UILabel();
            this.txtRecipeName = new Sunny.UI.UITextBox();
            this.lblDelayTime1Label = new Sunny.UI.UILabel();
            this.tableLayoutPanelDelay1 = new System.Windows.Forms.TableLayoutPanel();
            this.nudDelayHours = new System.Windows.Forms.NumericUpDown();
            this.lblDelay1Colon1 = new Sunny.UI.UILabel();
            this.nudDelayMinutes = new System.Windows.Forms.NumericUpDown();
            this.lblDelay1Colon2 = new Sunny.UI.UILabel();
            this.nudDelaySeconds = new System.Windows.Forms.NumericUpDown();
            this.lblStartTimeLabel = new Sunny.UI.UILabel();
            this.tableLayoutPanelStart = new System.Windows.Forms.TableLayoutPanel();
            this.nudStartHours = new System.Windows.Forms.NumericUpDown();
            this.lblStartColon1 = new Sunny.UI.UILabel();
            this.nudStartMinutes = new System.Windows.Forms.NumericUpDown();
            this.lblStartColon2 = new Sunny.UI.UILabel();
            this.nudStartSeconds = new System.Windows.Forms.NumericUpDown();
            this.lblLimitTempLabel = new Sunny.UI.UILabel();
            this.tableLayoutPanelTemp = new System.Windows.Forms.TableLayoutPanel();
            this.txtLimitTemp = new Sunny.UI.UITextBox();
            this.lblTempUnit = new Sunny.UI.UILabel();
            this.lblNegativePressureLabel = new Sunny.UI.UILabel();
            this.tableLayoutPanelPressure = new System.Windows.Forms.TableLayoutPanel();
            this.txtNegativePressure = new Sunny.UI.UITextBox();
            this.lblPressureUnit = new Sunny.UI.UILabel();
            this.lblDisplayModeLabel = new Sunny.UI.UILabel();
            this.cmbDisplayMode = new Sunny.UI.UIComboBox();
            this.panelButtons = new System.Windows.Forms.Panel();
            this.btnAddToQueue = new Sunny.UI.UIButton();
            this.btnClose = new Sunny.UI.UIButton();
            this.tableLayoutPanelMain.SuspendLayout();
            this.tableLayoutPanelInput.SuspendLayout();
            this.tableLayoutPanelDelay1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudDelayHours)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudDelayMinutes)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudDelaySeconds)).BeginInit();
            this.tableLayoutPanelStart.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudStartHours)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudStartMinutes)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudStartSeconds)).BeginInit();
            this.tableLayoutPanelTemp.SuspendLayout();
            this.tableLayoutPanelPressure.SuspendLayout();
            this.panelButtons.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanelMain
            // 
            this.tableLayoutPanelMain.ColumnCount = 1;
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.Controls.Add(this.tableLayoutPanelInput, 0, 0);
            this.tableLayoutPanelMain.Controls.Add(this.panelButtons, 0, 1);
            this.tableLayoutPanelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelMain.Location = new System.Drawing.Point(2, 38);
            this.tableLayoutPanelMain.Name = "tableLayoutPanelMain";
            this.tableLayoutPanelMain.RowCount = 2;
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this.tableLayoutPanelMain.Size = new System.Drawing.Size(476, 320);
            this.tableLayoutPanelMain.TabIndex = 0;
            // 
            // tableLayoutPanelInput
            // 
            this.tableLayoutPanelInput.ColumnCount = 2;
            this.tableLayoutPanelInput.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.tableLayoutPanelInput.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelInput.Controls.Add(this.lblRecipeNameLabel, 0, 0);
            this.tableLayoutPanelInput.Controls.Add(this.txtRecipeName, 1, 0);
            this.tableLayoutPanelInput.Controls.Add(this.lblDelayTime1Label, 0, 1);
            this.tableLayoutPanelInput.Controls.Add(this.tableLayoutPanelDelay1, 1, 1);
            this.tableLayoutPanelInput.Controls.Add(this.lblStartTimeLabel, 0, 2);
            this.tableLayoutPanelInput.Controls.Add(this.tableLayoutPanelStart, 1, 2);
            this.tableLayoutPanelInput.Controls.Add(this.lblLimitTempLabel, 0, 3);
            this.tableLayoutPanelInput.Controls.Add(this.tableLayoutPanelTemp, 1, 3);
            this.tableLayoutPanelInput.Controls.Add(this.lblNegativePressureLabel, 0, 4);
            this.tableLayoutPanelInput.Controls.Add(this.tableLayoutPanelPressure, 1, 4);
            this.tableLayoutPanelInput.Controls.Add(this.lblDisplayModeLabel, 0, 5);
            this.tableLayoutPanelInput.Controls.Add(this.cmbDisplayMode, 1, 5);
            this.tableLayoutPanelInput.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelInput.Location = new System.Drawing.Point(3, 3);
            this.tableLayoutPanelInput.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
            this.tableLayoutPanelInput.Name = "tableLayoutPanelInput";
            this.tableLayoutPanelInput.RowCount = 6;
            this.tableLayoutPanelInput.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanelInput.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanelInput.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanelInput.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanelInput.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanelInput.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelInput.Size = new System.Drawing.Size(470, 237);
            this.tableLayoutPanelInput.TabIndex = 0;
            // 
            // lblRecipeNameLabel
            // 
            this.lblRecipeNameLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblRecipeNameLabel.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblRecipeNameLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblRecipeNameLabel.Location = new System.Drawing.Point(3, 0);
            this.lblRecipeNameLabel.Name = "lblRecipeNameLabel";
            this.lblRecipeNameLabel.Size = new System.Drawing.Size(94, 40);
            this.lblRecipeNameLabel.TabIndex = 0;
            this.lblRecipeNameLabel.Text = "配方名称：";
            this.lblRecipeNameLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // txtRecipeName
            // 
            this.txtRecipeName.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.txtRecipeName.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtRecipeName.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtRecipeName.Location = new System.Drawing.Point(104, 5);
            this.txtRecipeName.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.txtRecipeName.MinimumSize = new System.Drawing.Size(1, 16);
            this.txtRecipeName.Name = "txtRecipeName";
            this.txtRecipeName.Padding = new System.Windows.Forms.Padding(5);
            this.txtRecipeName.ShowText = false;
            this.txtRecipeName.Size = new System.Drawing.Size(362, 29);
            this.txtRecipeName.TabIndex = 1;
            this.txtRecipeName.TextAlignment = System.Drawing.ContentAlignment.MiddleLeft;
            this.txtRecipeName.Watermark = "";
            // 
            // lblDelayTime1Label
            // 
            this.lblDelayTime1Label.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblDelayTime1Label.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblDelayTime1Label.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblDelayTime1Label.Location = new System.Drawing.Point(3, 40);
            this.lblDelayTime1Label.Name = "lblDelayTime1Label";
            this.lblDelayTime1Label.Size = new System.Drawing.Size(94, 40);
            this.lblDelayTime1Label.TabIndex = 2;
            this.lblDelayTime1Label.Text = "延时时间：";
            this.lblDelayTime1Label.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // tableLayoutPanelDelay1
            // 
            this.tableLayoutPanelDelay1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanelDelay1.ColumnCount = 5;
            this.tableLayoutPanelDelay1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanelDelay1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanelDelay1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanelDelay1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanelDelay1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanelDelay1.Controls.Add(this.nudDelayHours, 0, 0);
            this.tableLayoutPanelDelay1.Controls.Add(this.lblDelay1Colon1, 1, 0);
            this.tableLayoutPanelDelay1.Controls.Add(this.nudDelayMinutes, 2, 0);
            this.tableLayoutPanelDelay1.Controls.Add(this.lblDelay1Colon2, 3, 0);
            this.tableLayoutPanelDelay1.Controls.Add(this.nudDelaySeconds, 4, 0);
            this.tableLayoutPanelDelay1.Location = new System.Drawing.Point(103, 45);
            this.tableLayoutPanelDelay1.Name = "tableLayoutPanelDelay1";
            this.tableLayoutPanelDelay1.RowCount = 1;
            this.tableLayoutPanelDelay1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelDelay1.Size = new System.Drawing.Size(364, 30);
            this.tableLayoutPanelDelay1.TabIndex = 3;
            // 
            // nudDelayHours
            // 
            this.nudDelayHours.Dock = System.Windows.Forms.DockStyle.Fill;
            this.nudDelayHours.Location = new System.Drawing.Point(3, 3);
            this.nudDelayHours.Maximum = new decimal(new int[] {
            99,
            0,
            0,
            0});
            this.nudDelayHours.Name = "nudDelayHours";
            this.nudDelayHours.Size = new System.Drawing.Size(102, 26);
            this.nudDelayHours.TabIndex = 0;
            this.nudDelayHours.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // lblDelay1Colon1
            // 
            this.lblDelay1Colon1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.lblDelay1Colon1.AutoSize = true;
            this.lblDelay1Colon1.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblDelay1Colon1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblDelay1Colon1.Location = new System.Drawing.Point(111, 7);
            this.lblDelay1Colon1.Name = "lblDelay1Colon1";
            this.lblDelay1Colon1.Size = new System.Drawing.Size(14, 16);
            this.lblDelay1Colon1.TabIndex = 1;
            this.lblDelay1Colon1.Text = ":";
            // 
            // nudDelayMinutes
            // 
            this.nudDelayMinutes.Dock = System.Windows.Forms.DockStyle.Fill;
            this.nudDelayMinutes.Location = new System.Drawing.Point(131, 3);
            this.nudDelayMinutes.Maximum = new decimal(new int[] {
            59,
            0,
            0,
            0});
            this.nudDelayMinutes.Name = "nudDelayMinutes";
            this.nudDelayMinutes.Size = new System.Drawing.Size(102, 26);
            this.nudDelayMinutes.TabIndex = 2;
            this.nudDelayMinutes.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // lblDelay1Colon2
            // 
            this.lblDelay1Colon2.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.lblDelay1Colon2.AutoSize = true;
            this.lblDelay1Colon2.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblDelay1Colon2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblDelay1Colon2.Location = new System.Drawing.Point(239, 7);
            this.lblDelay1Colon2.Name = "lblDelay1Colon2";
            this.lblDelay1Colon2.Size = new System.Drawing.Size(14, 16);
            this.lblDelay1Colon2.TabIndex = 3;
            this.lblDelay1Colon2.Text = ":";
            // 
            // nudDelaySeconds
            // 
            this.nudDelaySeconds.Dock = System.Windows.Forms.DockStyle.Fill;
            this.nudDelaySeconds.Location = new System.Drawing.Point(259, 3);
            this.nudDelaySeconds.Maximum = new decimal(new int[] {
            59,
            0,
            0,
            0});
            this.nudDelaySeconds.Name = "nudDelaySeconds";
            this.nudDelaySeconds.Size = new System.Drawing.Size(102, 26);
            this.nudDelaySeconds.TabIndex = 4;
            this.nudDelaySeconds.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // lblStartTimeLabel
            // 
            this.lblStartTimeLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblStartTimeLabel.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblStartTimeLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblStartTimeLabel.Location = new System.Drawing.Point(3, 80);
            this.lblStartTimeLabel.Name = "lblStartTimeLabel";
            this.lblStartTimeLabel.Size = new System.Drawing.Size(94, 40);
            this.lblStartTimeLabel.TabIndex = 6;
            this.lblStartTimeLabel.Text = "启动时间：";
            this.lblStartTimeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // tableLayoutPanelStart
            // 
            this.tableLayoutPanelStart.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanelStart.ColumnCount = 5;
            this.tableLayoutPanelStart.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanelStart.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanelStart.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanelStart.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanelStart.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanelStart.Controls.Add(this.nudStartHours, 0, 0);
            this.tableLayoutPanelStart.Controls.Add(this.lblStartColon1, 1, 0);
            this.tableLayoutPanelStart.Controls.Add(this.nudStartMinutes, 2, 0);
            this.tableLayoutPanelStart.Controls.Add(this.lblStartColon2, 3, 0);
            this.tableLayoutPanelStart.Controls.Add(this.nudStartSeconds, 4, 0);
            this.tableLayoutPanelStart.Location = new System.Drawing.Point(103, 85);
            this.tableLayoutPanelStart.Name = "tableLayoutPanelStart";
            this.tableLayoutPanelStart.RowCount = 1;
            this.tableLayoutPanelStart.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelStart.Size = new System.Drawing.Size(364, 30);
            this.tableLayoutPanelStart.TabIndex = 7;
            // 
            // nudStartHours
            // 
            this.nudStartHours.Dock = System.Windows.Forms.DockStyle.Fill;
            this.nudStartHours.Location = new System.Drawing.Point(3, 3);
            this.nudStartHours.Maximum = new decimal(new int[] {
            99,
            0,
            0,
            0});
            this.nudStartHours.Name = "nudStartHours";
            this.nudStartHours.Size = new System.Drawing.Size(102, 26);
            this.nudStartHours.TabIndex = 0;
            this.nudStartHours.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // lblStartColon1
            // 
            this.lblStartColon1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.lblStartColon1.AutoSize = true;
            this.lblStartColon1.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblStartColon1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblStartColon1.Location = new System.Drawing.Point(111, 7);
            this.lblStartColon1.Name = "lblStartColon1";
            this.lblStartColon1.Size = new System.Drawing.Size(14, 16);
            this.lblStartColon1.TabIndex = 1;
            this.lblStartColon1.Text = ":";
            // 
            // nudStartMinutes
            // 
            this.nudStartMinutes.Dock = System.Windows.Forms.DockStyle.Fill;
            this.nudStartMinutes.Location = new System.Drawing.Point(131, 3);
            this.nudStartMinutes.Maximum = new decimal(new int[] {
            59,
            0,
            0,
            0});
            this.nudStartMinutes.Name = "nudStartMinutes";
            this.nudStartMinutes.Size = new System.Drawing.Size(102, 26);
            this.nudStartMinutes.TabIndex = 2;
            this.nudStartMinutes.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // lblStartColon2
            // 
            this.lblStartColon2.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.lblStartColon2.AutoSize = true;
            this.lblStartColon2.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblStartColon2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblStartColon2.Location = new System.Drawing.Point(239, 7);
            this.lblStartColon2.Name = "lblStartColon2";
            this.lblStartColon2.Size = new System.Drawing.Size(14, 16);
            this.lblStartColon2.TabIndex = 3;
            this.lblStartColon2.Text = ":";
            // 
            // nudStartSeconds
            // 
            this.nudStartSeconds.Dock = System.Windows.Forms.DockStyle.Fill;
            this.nudStartSeconds.Location = new System.Drawing.Point(259, 3);
            this.nudStartSeconds.Maximum = new decimal(new int[] {
            59,
            0,
            0,
            0});
            this.nudStartSeconds.Name = "nudStartSeconds";
            this.nudStartSeconds.Size = new System.Drawing.Size(102, 26);
            this.nudStartSeconds.TabIndex = 4;
            this.nudStartSeconds.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // lblLimitTempLabel
            // 
            this.lblLimitTempLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblLimitTempLabel.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblLimitTempLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblLimitTempLabel.Location = new System.Drawing.Point(3, 120);
            this.lblLimitTempLabel.Name = "lblLimitTempLabel";
            this.lblLimitTempLabel.Size = new System.Drawing.Size(94, 40);
            this.lblLimitTempLabel.TabIndex = 8;
            this.lblLimitTempLabel.Text = "极限温度：";
            this.lblLimitTempLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // tableLayoutPanelTemp
            // 
            this.tableLayoutPanelTemp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanelTemp.ColumnCount = 2;
            this.tableLayoutPanelTemp.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 60F));
            this.tableLayoutPanelTemp.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 40F));
            this.tableLayoutPanelTemp.Controls.Add(this.txtLimitTemp, 0, 0);
            this.tableLayoutPanelTemp.Controls.Add(this.lblTempUnit, 1, 0);
            this.tableLayoutPanelTemp.Location = new System.Drawing.Point(103, 123);
            this.tableLayoutPanelTemp.Name = "tableLayoutPanelTemp";
            this.tableLayoutPanelTemp.RowCount = 1;
            this.tableLayoutPanelTemp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelTemp.Size = new System.Drawing.Size(364, 34);
            this.tableLayoutPanelTemp.TabIndex = 9;
            // 
            // txtLimitTemp
            // 
            this.txtLimitTemp.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtLimitTemp.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtLimitTemp.DoubleValue = 50D;
            this.txtLimitTemp.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtLimitTemp.IntValue = 50;
            this.txtLimitTemp.Location = new System.Drawing.Point(4, 5);
            this.txtLimitTemp.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.txtLimitTemp.MaxLength = 3;
            this.txtLimitTemp.MinimumSize = new System.Drawing.Size(1, 16);
            this.txtLimitTemp.Name = "txtLimitTemp";
            this.txtLimitTemp.Padding = new System.Windows.Forms.Padding(5);
            this.txtLimitTemp.ShowText = false;
            this.txtLimitTemp.Size = new System.Drawing.Size(210, 24);
            this.txtLimitTemp.TabIndex = 0;
            this.txtLimitTemp.Text = "50";
            this.txtLimitTemp.TextAlignment = System.Drawing.ContentAlignment.MiddleLeft;
            this.txtLimitTemp.Watermark = "";
            // 
            // lblTempUnit
            // 
            this.lblTempUnit.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblTempUnit.AutoSize = true;
            this.lblTempUnit.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblTempUnit.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblTempUnit.Location = new System.Drawing.Point(221, 9);
            this.lblTempUnit.Name = "lblTempUnit";
            this.lblTempUnit.Size = new System.Drawing.Size(31, 16);
            this.lblTempUnit.TabIndex = 1;
            this.lblTempUnit.Text = "°C";
            // 
            // lblNegativePressureLabel
            // 
            this.lblNegativePressureLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblNegativePressureLabel.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblNegativePressureLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblNegativePressureLabel.Location = new System.Drawing.Point(3, 160);
            this.lblNegativePressureLabel.Name = "lblNegativePressureLabel";
            this.lblNegativePressureLabel.Size = new System.Drawing.Size(94, 40);
            this.lblNegativePressureLabel.TabIndex = 10;
            this.lblNegativePressureLabel.Text = "负压阈值：";
            this.lblNegativePressureLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // tableLayoutPanelPressure
            // 
            this.tableLayoutPanelPressure.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanelPressure.ColumnCount = 2;
            this.tableLayoutPanelPressure.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 60F));
            this.tableLayoutPanelPressure.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 40F));
            this.tableLayoutPanelPressure.Controls.Add(this.txtNegativePressure, 0, 0);
            this.tableLayoutPanelPressure.Controls.Add(this.lblPressureUnit, 1, 0);
            this.tableLayoutPanelPressure.Location = new System.Drawing.Point(103, 163);
            this.tableLayoutPanelPressure.Name = "tableLayoutPanelPressure";
            this.tableLayoutPanelPressure.RowCount = 1;
            this.tableLayoutPanelPressure.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelPressure.Size = new System.Drawing.Size(364, 34);
            this.tableLayoutPanelPressure.TabIndex = 11;
            // 
            // txtNegativePressure
            // 
            this.txtNegativePressure.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.txtNegativePressure.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtNegativePressure.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtNegativePressure.Location = new System.Drawing.Point(4, 5);
            this.txtNegativePressure.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.txtNegativePressure.MaxLength = 7;
            this.txtNegativePressure.MinimumSize = new System.Drawing.Size(1, 16);
            this.txtNegativePressure.Name = "txtNegativePressure";
            this.txtNegativePressure.Padding = new System.Windows.Forms.Padding(5);
            this.txtNegativePressure.ShowText = false;
            this.txtNegativePressure.Size = new System.Drawing.Size(210, 24);
            this.txtNegativePressure.TabIndex = 0;
            this.txtNegativePressure.TextAlignment = System.Drawing.ContentAlignment.MiddleLeft;
            this.txtNegativePressure.Watermark = "";
            // 
            // lblPressureUnit
            // 
            this.lblPressureUnit.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblPressureUnit.AutoSize = true;
            this.lblPressureUnit.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblPressureUnit.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblPressureUnit.Location = new System.Drawing.Point(221, 9);
            this.lblPressureUnit.Name = "lblPressureUnit";
            this.lblPressureUnit.Size = new System.Drawing.Size(31, 16);
            this.lblPressureUnit.TabIndex = 1;
            this.lblPressureUnit.Text = "kPa";
            // 
            // lblDisplayModeLabel
            // 
            this.lblDisplayModeLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblDisplayModeLabel.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblDisplayModeLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(48)))), ((int)(((byte)(48)))), ((int)(((byte)(48)))));
            this.lblDisplayModeLabel.Location = new System.Drawing.Point(3, 200);
            this.lblDisplayModeLabel.Name = "lblDisplayModeLabel";
            this.lblDisplayModeLabel.Size = new System.Drawing.Size(94, 37);
            this.lblDisplayModeLabel.TabIndex = 12;
            this.lblDisplayModeLabel.Text = "显示模式：";
            this.lblDisplayModeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // cmbDisplayMode
            // 
            this.cmbDisplayMode.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.cmbDisplayMode.DropDownStyle = Sunny.UI.UIDropDownStyle.DropDownList;
            this.cmbDisplayMode.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.cmbDisplayMode.Location = new System.Drawing.Point(104, 205);
            this.cmbDisplayMode.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.cmbDisplayMode.MinimumSize = new System.Drawing.Size(1, 16);
            this.cmbDisplayMode.Name = "cmbDisplayMode";
            this.cmbDisplayMode.Size = new System.Drawing.Size(362, 27);
            this.cmbDisplayMode.TabIndex = 13;
            // 
            // panelButtons
            // 
            this.panelButtons.Controls.Add(this.btnAddToQueue);
            this.panelButtons.Controls.Add(this.btnClose);
            this.panelButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelButtons.Location = new System.Drawing.Point(3, 240);
            this.panelButtons.Margin = new System.Windows.Forms.Padding(3, 0, 3, 3);
            this.panelButtons.Name = "panelButtons";
            this.panelButtons.Size = new System.Drawing.Size(470, 77);
            this.panelButtons.TabIndex = 1;
            // 
            // btnAddToQueue
            // 
            this.btnAddToQueue.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnAddToQueue.FillColor = System.Drawing.Color.DodgerBlue;
            this.btnAddToQueue.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnAddToQueue.Location = new System.Drawing.Point(150, 10);
            this.btnAddToQueue.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnAddToQueue.Name = "btnAddToQueue";
            this.btnAddToQueue.RectColor = System.Drawing.Color.DodgerBlue;
            this.btnAddToQueue.Size = new System.Drawing.Size(170, 30);
            this.btnAddToQueue.Style = Sunny.UI.UIStyle.Custom;
            this.btnAddToQueue.TabIndex = 0;
            this.btnAddToQueue.Text = "加入队列";
            this.btnAddToQueue.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnAddToQueue.Click += new System.EventHandler(this.btnAddToQueue_Click);
            // 
            // btnClose
            // 
            this.btnClose.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnClose.FillColor = System.Drawing.Color.DimGray;
            this.btnClose.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnClose.Location = new System.Drawing.Point(150, 46);
            this.btnClose.MinimumSize = new System.Drawing.Size(1, 1);
            this.btnClose.Name = "btnClose";
            this.btnClose.RectColor = System.Drawing.Color.DimGray;
            this.btnClose.Size = new System.Drawing.Size(170, 30);
            this.btnClose.Style = Sunny.UI.UIStyle.Custom;
            this.btnClose.TabIndex = 1;
            this.btnClose.Text = "关闭窗口";
            this.btnClose.TipsFont = new System.Drawing.Font("宋体", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // BatchRecipeForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(480, 360);
            this.Controls.Add(this.tableLayoutPanelMain);
            this.MinimumSize = new System.Drawing.Size(480, 360);
            this.Name = "BatchRecipeForm";
            this.Padding = new System.Windows.Forms.Padding(2, 38, 2, 2);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "批量设置设置配方窗口";
            this.ZoomScaleRect = new System.Drawing.Rectangle(15, 15, 480, 360);
            this.tableLayoutPanelMain.ResumeLayout(false);
            this.tableLayoutPanelInput.ResumeLayout(false);
            this.tableLayoutPanelDelay1.ResumeLayout(false);
            this.tableLayoutPanelDelay1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudDelayHours)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudDelayMinutes)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudDelaySeconds)).EndInit();
            this.tableLayoutPanelStart.ResumeLayout(false);
            this.tableLayoutPanelStart.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.nudStartHours)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudStartMinutes)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudStartSeconds)).EndInit();
            this.tableLayoutPanelTemp.ResumeLayout(false);
            this.tableLayoutPanelTemp.PerformLayout();
            this.tableLayoutPanelPressure.ResumeLayout(false);
            this.tableLayoutPanelPressure.PerformLayout();
            this.panelButtons.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        // 控件字段声明区域
        // 这些字段在两个 partial 文件中共享（本文件赋值，.cs文件使用）

        /// <summary>主布局容器（2行：输入区域/按钮区域）</summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelMain;
        /// <summary>输入区域布局容器（6行：配方名称/延时时间/启动时间/极限温度/负压阈值/显示模式）</summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelInput;
        /// <summary>"配方名称"标签</summary>
        private Sunny.UI.UILabel lblRecipeNameLabel;
        /// <summary>配方名称输入框</summary>
        private Sunny.UI.UITextBox txtRecipeName;
        /// <summary>"延时时间"标签</summary>
        private Sunny.UI.UILabel lblDelayTime1Label;
        /// <summary>延时时间输入布局（时:分:秒）</summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelDelay1;
        /// <summary>延时时间-小时输入（NumericUpDown，V1.28 由 TextBox 改）</summary>
        private System.Windows.Forms.NumericUpDown nudDelayHours;
        /// <summary>延时时间-第一个冒号分隔符</summary>
        private Sunny.UI.UILabel lblDelay1Colon1;
        /// <summary>延时时间-分钟输入（NumericUpDown，V1.28 由 TextBox 改）</summary>
        private System.Windows.Forms.NumericUpDown nudDelayMinutes;
        /// <summary>延时时间-第二个冒号分隔符</summary>
        private Sunny.UI.UILabel lblDelay1Colon2;
        /// <summary>延时时间-秒输入（NumericUpDown，V1.28 由 TextBox 改）</summary>
        private System.Windows.Forms.NumericUpDown nudDelaySeconds;
        /// <summary>"启动时间"标签</summary>
        private Sunny.UI.UILabel lblStartTimeLabel;
        /// <summary>启动时间输入布局（时:分:秒）</summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelStart;
        /// <summary>启动时间-小时输入（NumericUpDown，V1.28 由 TextBox 改，与延时时间/配方管理窗口样式一致）</summary>
        private System.Windows.Forms.NumericUpDown nudStartHours;
        /// <summary>启动时间-第一个冒号分隔符</summary>
        private Sunny.UI.UILabel lblStartColon1;
        /// <summary>启动时间-分钟输入（NumericUpDown，V1.28 由 TextBox 改）</summary>
        private System.Windows.Forms.NumericUpDown nudStartMinutes;
        /// <summary>启动时间-第二个冒号分隔符</summary>
        private Sunny.UI.UILabel lblStartColon2;
        /// <summary>启动时间-秒输入（NumericUpDown，V1.28 由 TextBox 改）</summary>
        private System.Windows.Forms.NumericUpDown nudStartSeconds;
        /// <summary>"极限温度"标签</summary>
        private Sunny.UI.UILabel lblLimitTempLabel;
        /// <summary>极限温度输入布局（数值 + 单位）</summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelTemp;
        /// <summary>极限温度值输入框</summary>
        private Sunny.UI.UITextBox txtLimitTemp;
        /// <summary>温度单位标签（°C）</summary>
        private Sunny.UI.UILabel lblTempUnit;
        /// <summary>"负压阈值"标签（【V1.66】配方真空工艺要求，kPa）</summary>
        private Sunny.UI.UILabel lblNegativePressureLabel;
        /// <summary>负压阈值输入布局（数值 + 单位）</summary>
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanelPressure;
        /// <summary>负压阈值输入框（【V1.66】文本解析，范围±9999，新建默认=全局阈值）</summary>
        private Sunny.UI.UITextBox txtNegativePressure;
        /// <summary>负压单位标签（kPa）</summary>
        private Sunny.UI.UILabel lblPressureUnit;
        /// <summary>"显示模式"标签（【V1.66】烧屏画面记录）</summary>
        private Sunny.UI.UILabel lblDisplayModeLabel;
        /// <summary>显示模式输入框（【V1.66】自由文本，最长50）</summary>
        private Sunny.UI.UIComboBox cmbDisplayMode;
        /// <summary>底部按钮面板（2个按钮：加入队列/关闭窗口）</summary>
        private System.Windows.Forms.Panel panelButtons;
        /// <summary>加入队列按钮</summary>
        private Sunny.UI.UIButton btnAddToQueue;
        /// <summary>关闭窗口按钮</summary>
        private Sunny.UI.UIButton btnClose;
    }
}
