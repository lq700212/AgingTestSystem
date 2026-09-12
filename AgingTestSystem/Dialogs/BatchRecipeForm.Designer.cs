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
            this.txtDisplayMode = new Sunny.UI.UITextBox();
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
            this.tableLayoutPanelMain.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanelMain.Name = "tableLayoutPanelMain";
            this.tableLayoutPanelMain.RowCount = 2;
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 80F));
            this.tableLayoutPanelMain.Size = new System.Drawing.Size(480, 400);
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
            this.tableLayoutPanelInput.Controls.Add(this.txtDisplayMode, 1, 5);
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
            this.tableLayoutPanelInput.Size = new System.Drawing.Size(474, 317);
            this.tableLayoutPanelInput.TabIndex = 0;
            // 
            // lblRecipeNameLabel
            // 
            this.lblRecipeNameLabel.Dock = System.Windows.Forms.DockStyle.Fill;
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
            this.txtRecipeName.Location = new System.Drawing.Point(103, 9);
            this.txtRecipeName.Name = "txtRecipeName";
            this.txtRecipeName.Size = new System.Drawing.Size(368, 21);
            this.txtRecipeName.TabIndex = 1;
            // 
            // lblDelayTime1Label
            // 
            this.lblDelayTime1Label.Dock = System.Windows.Forms.DockStyle.Fill;
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
            this.tableLayoutPanelDelay1.Location = new System.Drawing.Point(103, 47);
            this.tableLayoutPanelDelay1.Name = "tableLayoutPanelDelay1";
            this.tableLayoutPanelDelay1.RowCount = 1;
            this.tableLayoutPanelDelay1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelDelay1.Size = new System.Drawing.Size(368, 26);
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
            this.nudDelayHours.Size = new System.Drawing.Size(103, 21);
            this.nudDelayHours.TabIndex = 0;
            this.nudDelayHours.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // lblDelay1Colon1
            // 
            this.lblDelay1Colon1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.lblDelay1Colon1.AutoSize = true;
            this.lblDelay1Colon1.Location = new System.Drawing.Point(113, 7);
            this.lblDelay1Colon1.Name = "lblDelay1Colon1";
            this.lblDelay1Colon1.Size = new System.Drawing.Size(11, 12);
            this.lblDelay1Colon1.TabIndex = 1;
            this.lblDelay1Colon1.Text = ":";
            // 
            // nudDelayMinutes
            // 
            this.nudDelayMinutes.Dock = System.Windows.Forms.DockStyle.Fill;
            this.nudDelayMinutes.Location = new System.Drawing.Point(132, 3);
            this.nudDelayMinutes.Maximum = new decimal(new int[] {
            59,
            0,
            0,
            0});
            this.nudDelayMinutes.Name = "nudDelayMinutes";
            this.nudDelayMinutes.Size = new System.Drawing.Size(103, 21);
            this.nudDelayMinutes.TabIndex = 2;
            this.nudDelayMinutes.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // lblDelay1Colon2
            // 
            this.lblDelay1Colon2.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.lblDelay1Colon2.AutoSize = true;
            this.lblDelay1Colon2.Location = new System.Drawing.Point(242, 7);
            this.lblDelay1Colon2.Name = "lblDelay1Colon2";
            this.lblDelay1Colon2.Size = new System.Drawing.Size(11, 12);
            this.lblDelay1Colon2.TabIndex = 3;
            this.lblDelay1Colon2.Text = ":";
            // 
            // nudDelaySeconds
            // 
            this.nudDelaySeconds.Dock = System.Windows.Forms.DockStyle.Fill;
            this.nudDelaySeconds.Location = new System.Drawing.Point(261, 3);
            this.nudDelaySeconds.Maximum = new decimal(new int[] {
            59,
            0,
            0,
            0});
            this.nudDelaySeconds.Name = "nudDelaySeconds";
            this.nudDelaySeconds.Size = new System.Drawing.Size(104, 21);
            this.nudDelaySeconds.TabIndex = 4;
            this.nudDelaySeconds.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // lblStartTimeLabel
            // 
            this.lblStartTimeLabel.Dock = System.Windows.Forms.DockStyle.Fill;
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
            this.tableLayoutPanelStart.Location = new System.Drawing.Point(103, 87);
            this.tableLayoutPanelStart.Name = "tableLayoutPanelStart";
            this.tableLayoutPanelStart.RowCount = 1;
            this.tableLayoutPanelStart.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelStart.Size = new System.Drawing.Size(368, 26);
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
            this.nudStartHours.Size = new System.Drawing.Size(103, 21);
            this.nudStartHours.TabIndex = 0;
            this.nudStartHours.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // lblStartColon1
            // 
            this.lblStartColon1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.lblStartColon1.AutoSize = true;
            this.lblStartColon1.Location = new System.Drawing.Point(113, 7);
            this.lblStartColon1.Name = "lblStartColon1";
            this.lblStartColon1.Size = new System.Drawing.Size(11, 12);
            this.lblStartColon1.TabIndex = 1;
            this.lblStartColon1.Text = ":";
            // 
            // nudStartMinutes
            // 
            this.nudStartMinutes.Dock = System.Windows.Forms.DockStyle.Fill;
            this.nudStartMinutes.Location = new System.Drawing.Point(132, 3);
            this.nudStartMinutes.Maximum = new decimal(new int[] {
            59,
            0,
            0,
            0});
            this.nudStartMinutes.Name = "nudStartMinutes";
            this.nudStartMinutes.Size = new System.Drawing.Size(103, 21);
            this.nudStartMinutes.TabIndex = 2;
            this.nudStartMinutes.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // lblStartColon2
            // 
            this.lblStartColon2.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.lblStartColon2.AutoSize = true;
            this.lblStartColon2.Location = new System.Drawing.Point(242, 7);
            this.lblStartColon2.Name = "lblStartColon2";
            this.lblStartColon2.Size = new System.Drawing.Size(11, 12);
            this.lblStartColon2.TabIndex = 3;
            this.lblStartColon2.Text = ":";
            // 
            // nudStartSeconds
            // 
            this.nudStartSeconds.Dock = System.Windows.Forms.DockStyle.Fill;
            this.nudStartSeconds.Location = new System.Drawing.Point(261, 3);
            this.nudStartSeconds.Maximum = new decimal(new int[] {
            59,
            0,
            0,
            0});
            this.nudStartSeconds.Name = "nudStartSeconds";
            this.nudStartSeconds.Size = new System.Drawing.Size(104, 21);
            this.nudStartSeconds.TabIndex = 4;
            this.nudStartSeconds.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // lblLimitTempLabel
            // 
            this.lblLimitTempLabel.Dock = System.Windows.Forms.DockStyle.Fill;
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
            this.tableLayoutPanelTemp.Location = new System.Drawing.Point(103, 127);
            this.tableLayoutPanelTemp.Name = "tableLayoutPanelTemp";
            this.tableLayoutPanelTemp.RowCount = 1;
            this.tableLayoutPanelTemp.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelTemp.Size = new System.Drawing.Size(368, 26);
            this.tableLayoutPanelTemp.TabIndex = 9;
            // 
            // txtLimitTemp
            // 
            this.txtLimitTemp.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtLimitTemp.Location = new System.Drawing.Point(3, 3);
            this.txtLimitTemp.MaxLength = 3;
            this.txtLimitTemp.Name = "txtLimitTemp";
            this.txtLimitTemp.Size = new System.Drawing.Size(214, 21);
            this.txtLimitTemp.TabIndex = 0;
            this.txtLimitTemp.Text = "50";
            // 
            // lblTempUnit
            // 
            this.lblTempUnit.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblTempUnit.AutoSize = true;
            this.lblTempUnit.Location = new System.Drawing.Point(223, 7);
            this.lblTempUnit.Name = "lblTempUnit";
            this.lblTempUnit.Size = new System.Drawing.Size(23, 12);
            this.lblTempUnit.TabIndex = 1;
            this.lblTempUnit.Text = "°C";
            // 
            // lblNegativePressureLabel
            // 
            // 【V1.66】配方负压阈值（kPa）：以前本窗没有这个框，新建配方 NegativePressure 恒 0，
            // 下发后阈值≈0（负压域里≈永远到位），等于悄悄关掉真空保护。现在必填实数，
            // 新建默认=全局 AlarmPressureThresholdKPa，存什么定格什么，无魔法值。
            // 
            this.lblNegativePressureLabel.Dock = System.Windows.Forms.DockStyle.Fill;
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
            this.tableLayoutPanelPressure.Location = new System.Drawing.Point(103, 167);
            this.tableLayoutPanelPressure.Name = "tableLayoutPanelPressure";
            this.tableLayoutPanelPressure.RowCount = 1;
            this.tableLayoutPanelPressure.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelPressure.Size = new System.Drawing.Size(368, 26);
            this.tableLayoutPanelPressure.TabIndex = 11;
            // 
            // txtNegativePressure
            // 
            this.txtNegativePressure.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtNegativePressure.Location = new System.Drawing.Point(3, 3);
            this.txtNegativePressure.MaxLength = 7;
            this.txtNegativePressure.Name = "txtNegativePressure";
            this.txtNegativePressure.Size = new System.Drawing.Size(214, 21);
            this.txtNegativePressure.TabIndex = 0;
            // 
            // lblPressureUnit
            // 
            this.lblPressureUnit.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.lblPressureUnit.AutoSize = true;
            this.lblPressureUnit.Location = new System.Drawing.Point(223, 7);
            this.lblPressureUnit.Name = "lblPressureUnit";
            this.lblPressureUnit.Size = new System.Drawing.Size(23, 12);
            this.lblPressureUnit.TabIndex = 1;
            this.lblPressureUnit.Text = "kPa";
            // 
            // lblDisplayModeLabel
            // 
            // 【V1.66】烧屏画面记录（自由文本如"白场/RGB循环/棋盘格"）：只存配方追溯，
            // 不参与任何判定；下发工位走 SetStationRecipe（见 DeviceManager）。
            // 
            this.lblDisplayModeLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblDisplayModeLabel.Location = new System.Drawing.Point(3, 200);
            this.lblDisplayModeLabel.Name = "lblDisplayModeLabel";
            this.lblDisplayModeLabel.Size = new System.Drawing.Size(94, 40);
            this.lblDisplayModeLabel.TabIndex = 12;
            this.lblDisplayModeLabel.Text = "显示模式：";
            this.lblDisplayModeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // txtDisplayMode
            // 
            this.txtDisplayMode.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.txtDisplayMode.Location = new System.Drawing.Point(103, 207);
            this.txtDisplayMode.MaxLength = 50;
            this.txtDisplayMode.Name = "txtDisplayMode";
            this.txtDisplayMode.Size = new System.Drawing.Size(368, 21);
            this.txtDisplayMode.TabIndex = 13;
            // 
            // panelButtons
            // 
            this.panelButtons.Controls.Add(this.btnAddToQueue);
            this.panelButtons.Controls.Add(this.btnClose);
            this.panelButtons.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelButtons.Location = new System.Drawing.Point(3, 240);
            this.panelButtons.Margin = new System.Windows.Forms.Padding(3, 0, 3, 3);
            this.panelButtons.Name = "panelButtons";
            this.panelButtons.Size = new System.Drawing.Size(474, 77);
            this.panelButtons.TabIndex = 1;
            // 
            // btnAddToQueue
            // 
            // btnAddToQueue - 加入队列（主按钮蓝，与各弹窗确认按钮统一）
            //
            this.btnAddToQueue.FillColor = System.Drawing.Color.DodgerBlue;
            this.btnAddToQueue.RectColor = System.Drawing.Color.DodgerBlue;
            this.btnAddToQueue.ForeColor = System.Drawing.Color.White;
            this.btnAddToQueue.Style = Sunny.UI.UIStyle.Custom;
            this.btnAddToQueue.Location = new System.Drawing.Point(150, 10);
            this.btnAddToQueue.Name = "btnAddToQueue";
            this.btnAddToQueue.Size = new System.Drawing.Size(170, 30);
            this.btnAddToQueue.TabIndex = 0;
            this.btnAddToQueue.Text = "加入队列";
            this.btnAddToQueue.Click += new System.EventHandler(this.btnAddToQueue_Click);
            // 
            // btnClose
            // 
            // btnClose - 关闭窗口（语义灰）
            //
            this.btnClose.FillColor = System.Drawing.Color.DimGray;
            this.btnClose.RectColor = System.Drawing.Color.DimGray;
            this.btnClose.ForeColor = System.Drawing.Color.White;
            this.btnClose.Style = Sunny.UI.UIStyle.Custom;
            this.btnClose.Location = new System.Drawing.Point(150, 46);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(170, 30);
            this.btnClose.TabIndex = 1;
            this.btnClose.Text = "关闭窗口";
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // BatchRecipeForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(480, 320);
            // 【V1.71】Dock 布局自适应放大；禁缩小（MinimumSize=ClientSize），防挤坏。
            this.MinimumSize = new System.Drawing.Size(480, 320);
            // 【V1.71】UIForm 自绘蓝标题：Dock=Fill 布局加顶 Pad 避开标题区。
            this.Padding = new System.Windows.Forms.Padding(2, 38, 2, 2);
            this.Controls.Add(this.tableLayoutPanelMain);
            this.Name = "BatchRecipeForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "批量设置设置配方窗口";
            this.tableLayoutPanelMain.ResumeLayout(false);
            this.tableLayoutPanelInput.ResumeLayout(false);
            this.tableLayoutPanelInput.PerformLayout();
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
        private Sunny.UI.UITextBox txtDisplayMode;
        /// <summary>底部按钮面板（2个按钮：加入队列/关闭窗口）</summary>
        private System.Windows.Forms.Panel panelButtons;
        /// <summary>加入队列按钮</summary>
        private Sunny.UI.UIButton btnAddToQueue;
        /// <summary>关闭窗口按钮</summary>
        private Sunny.UI.UIButton btnClose;
    }
}
