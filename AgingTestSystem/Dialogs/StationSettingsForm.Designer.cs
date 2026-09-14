using System;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 工位设置窗口 —— 设计器自动生成部分（V1.18 新增）
    ///
    /// 【布局说明】
    /// ┌────────────────────────────────────────────────┐
    /// │ 工位设置窗口 NO 1                                │  ← 标题栏
    /// ├────────────────────────────────┬───────────────┤
    /// │  状态:                [空闲] │ [破空]        │
    /// │  SN:                    [___] │ [下电]        │
    /// │  配方:                  [___] │ [保存]        │
    /// │  延时时间:              [___] │ [加入对列]     │
    /// │  烧屏时间:              [___] │ [关闭窗口]     │
    /// │  极限温度:              [___] │               │
    /// └────────────────────────────────┴───────────────┘
    ///
    /// 左侧为 6 个设置项（设置项名 + 输入框，均左对齐，整列居中）；
    /// 右侧为一列操作按钮。
    /// </summary>
    partial class StationSettingsForm
    {
        /// <summary>必需的设计器变量</summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>清理所有正在使用的资源</summary>
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

        /// <summary>设计器支持所需的方法</summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.lblState = new Sunny.UI.UILabel();
            this.txtState = new Sunny.UI.UITextBox();
            this.lblSN = new Sunny.UI.UILabel();
            this.txtSN = new Sunny.UI.UITextBox();
            this.lblRecipe = new Sunny.UI.UILabel();
            this.txtRecipe = new Sunny.UI.UITextBox();
            this.lblDelay = new Sunny.UI.UILabel();
            this.nudDelayHours = new System.Windows.Forms.NumericUpDown();
            this.lblDelayColon1 = new Sunny.UI.UILabel();
            this.nudDelayMinutes = new System.Windows.Forms.NumericUpDown();
            this.lblDelayColon2 = new Sunny.UI.UILabel();
            this.nudDelaySeconds = new System.Windows.Forms.NumericUpDown();
            this.lblBurnIn = new Sunny.UI.UILabel();
            this.nudBurnInHours = new System.Windows.Forms.NumericUpDown();
            this.lblBurnInColon1 = new Sunny.UI.UILabel();
            this.nudBurnInMinutes = new System.Windows.Forms.NumericUpDown();
            this.lblBurnInColon2 = new Sunny.UI.UILabel();
            this.nudBurnInSeconds = new System.Windows.Forms.NumericUpDown();
            this.lblTemp = new Sunny.UI.UILabel();
            this.nudTemp = new System.Windows.Forms.NumericUpDown();
            this.lblPressure = new Sunny.UI.UILabel();
            this.nudPressure = new System.Windows.Forms.NumericUpDown();
            this.lblPressureUnit = new Sunny.UI.UILabel();
            this.lblDisplayMode = new Sunny.UI.UILabel();
            this.cmbDisplayMode = new Sunny.UI.UIComboBox();
            this.btnBreakVacuum = new Sunny.UI.UIButton();
            this.btnPowerOff = new Sunny.UI.UIButton();
            this.btnSave = new Sunny.UI.UIButton();
            this.btnAddToQueue = new Sunny.UI.UIButton();
            this.btnClose = new Sunny.UI.UIButton();
            ((System.ComponentModel.ISupportInitialize)(this.nudDelayHours)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudDelayMinutes)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudDelaySeconds)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudBurnInHours)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudBurnInMinutes)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudBurnInSeconds)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudTemp)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudPressure)).BeginInit();
            this.SuspendLayout();
            //
            // lblState - "状态"设置项名称（左对齐，V1.18 只显示"状态"两字）
            //
            this.lblState.AutoSize = true;
            this.lblState.Location = new System.Drawing.Point(30, 63);
            this.lblState.Name = "lblState";
            this.lblState.Size = new System.Drawing.Size(41, 12);
            this.lblState.TabIndex = 0;
            this.lblState.Text = "状态:";
            //
            // txtState - 状态显示输入框（只读，V1.18 显示中文状态：空闲/选中/繁忙/故障）
            //
            this.txtState.Location = new System.Drawing.Point(150, 55);
            this.txtState.Name = "txtState";
            this.txtState.ReadOnly = true;
            this.txtState.Size = new System.Drawing.Size(180, 29);
            this.txtState.TabIndex = 1;
            this.txtState.Text = "空闲";
            //
            // lblSN - "SN"设置项名称（左对齐）
            //
            this.lblSN.AutoSize = true;
            this.lblSN.Location = new System.Drawing.Point(30, 101);
            this.lblSN.Name = "lblSN";
            this.lblSN.Size = new System.Drawing.Size(29, 12);
            this.lblSN.TabIndex = 2;
            this.lblSN.Text = "SN:";
            //
            // txtSN - SN输入框
            //
            this.txtSN.Location = new System.Drawing.Point(150, 93);
            this.txtSN.Name = "txtSN";
            this.txtSN.Size = new System.Drawing.Size(180, 29);
            this.txtSN.TabIndex = 3;
            //
            // lblRecipe - "配方"设置项名称（左对齐）
            //
            this.lblRecipe.AutoSize = true;
            this.lblRecipe.Location = new System.Drawing.Point(30, 139);
            this.lblRecipe.Name = "lblRecipe";
            this.lblRecipe.Size = new System.Drawing.Size(41, 12);
            this.lblRecipe.TabIndex = 4;
            this.lblRecipe.Text = "配方:";
            //
            // txtRecipe - 配方输入框
            //
            this.txtRecipe.Location = new System.Drawing.Point(150, 131);
            this.txtRecipe.Name = "txtRecipe";
            this.txtRecipe.Size = new System.Drawing.Size(180, 29);
            this.txtRecipe.TabIndex = 5;
            //
            // lblDelay - "延时时间"设置项名称（左对齐）
            //
            this.lblDelay.AutoSize = true;
            this.lblDelay.Location = new System.Drawing.Point(30, 177);
            this.lblDelay.Name = "lblDelay";
            this.lblDelay.Size = new System.Drawing.Size(65, 12);
            this.lblDelay.TabIndex = 6;
            this.lblDelay.Text = "延时时间:";
            //
            // nudDelayHours - 延时时间-时（NumericUpDown，V1.28 由 TextBox 改）
            //
            this.nudDelayHours.Location = new System.Drawing.Point(150, 171);
            this.nudDelayHours.Maximum = new decimal(new int[] {
            99,
            0,
            0,
            0});
            this.nudDelayHours.Name = "nudDelayHours";
            this.nudDelayHours.Size = new System.Drawing.Size(48, 21);
            this.nudDelayHours.TabIndex = 7;
            this.nudDelayHours.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // lblDelayColon1 - 延时时间：时与分之间的冒号分隔符
            //
            this.lblDelayColon1.AutoSize = true;
            this.lblDelayColon1.Location = new System.Drawing.Point(200, 175);
            this.lblDelayColon1.Name = "lblDelayColon1";
            this.lblDelayColon1.Size = new System.Drawing.Size(6, 12);
            this.lblDelayColon1.TabIndex = 0;
            this.lblDelayColon1.Text = ":";
            //
            // nudDelayMinutes - 延时时间-分（NumericUpDown）
            //
            this.nudDelayMinutes.Location = new System.Drawing.Point(212, 171);
            this.nudDelayMinutes.Maximum = new decimal(new int[] {
            59,
            0,
            0,
            0});
            this.nudDelayMinutes.Name = "nudDelayMinutes";
            this.nudDelayMinutes.Size = new System.Drawing.Size(48, 21);
            this.nudDelayMinutes.TabIndex = 8;
            this.nudDelayMinutes.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // lblDelayColon2 - 延时时间：分与秒之间的冒号分隔符
            //
            this.lblDelayColon2.AutoSize = true;
            this.lblDelayColon2.Location = new System.Drawing.Point(262, 175);
            this.lblDelayColon2.Name = "lblDelayColon2";
            this.lblDelayColon2.Size = new System.Drawing.Size(6, 12);
            this.lblDelayColon2.TabIndex = 0;
            this.lblDelayColon2.Text = ":";
            //
            // nudDelaySeconds - 延时时间-秒（NumericUpDown）
            //
            this.nudDelaySeconds.Location = new System.Drawing.Point(274, 171);
            this.nudDelaySeconds.Maximum = new decimal(new int[] {
            59,
            0,
            0,
            0});
            this.nudDelaySeconds.Name = "nudDelaySeconds";
            this.nudDelaySeconds.Size = new System.Drawing.Size(48, 21);
            this.nudDelaySeconds.TabIndex = 9;
            this.nudDelaySeconds.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // lblBurnIn - "烧屏时间"设置项名称（左对齐）
            //
            this.lblBurnIn.AutoSize = true;
            this.lblBurnIn.Location = new System.Drawing.Point(30, 215);
            this.lblBurnIn.Name = "lblBurnIn";
            this.lblBurnIn.Size = new System.Drawing.Size(65, 12);
            this.lblBurnIn.TabIndex = 8;
            this.lblBurnIn.Text = "烧屏时间:";
            //
            // nudBurnInHours - 烧屏时间-时（NumericUpDown，V1.28 由 TextBox 改）
            //
            this.nudBurnInHours.Location = new System.Drawing.Point(150, 209);
            this.nudBurnInHours.Maximum = new decimal(new int[] {
            99,
            0,
            0,
            0});
            this.nudBurnInHours.Name = "nudBurnInHours";
            this.nudBurnInHours.Size = new System.Drawing.Size(48, 21);
            this.nudBurnInHours.TabIndex = 10;
            this.nudBurnInHours.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // lblBurnInColon1 - 烧屏时间：时与分之间的冒号分隔符
            //
            this.lblBurnInColon1.AutoSize = true;
            this.lblBurnInColon1.Location = new System.Drawing.Point(200, 213);
            this.lblBurnInColon1.Name = "lblBurnInColon1";
            this.lblBurnInColon1.Size = new System.Drawing.Size(6, 12);
            this.lblBurnInColon1.TabIndex = 0;
            this.lblBurnInColon1.Text = ":";
            //
            // nudBurnInMinutes - 烧屏时间-分（NumericUpDown）
            //
            this.nudBurnInMinutes.Location = new System.Drawing.Point(212, 209);
            this.nudBurnInMinutes.Maximum = new decimal(new int[] {
            59,
            0,
            0,
            0});
            this.nudBurnInMinutes.Name = "nudBurnInMinutes";
            this.nudBurnInMinutes.Size = new System.Drawing.Size(48, 21);
            this.nudBurnInMinutes.TabIndex = 11;
            this.nudBurnInMinutes.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // lblBurnInColon2 - 烧屏时间：分与秒之间的冒号分隔符
            //
            this.lblBurnInColon2.AutoSize = true;
            this.lblBurnInColon2.Location = new System.Drawing.Point(262, 213);
            this.lblBurnInColon2.Name = "lblBurnInColon2";
            this.lblBurnInColon2.Size = new System.Drawing.Size(6, 12);
            this.lblBurnInColon2.TabIndex = 0;
            this.lblBurnInColon2.Text = ":";
            //
            // nudBurnInSeconds - 烧屏时间-秒（NumericUpDown）
            //
            this.nudBurnInSeconds.Location = new System.Drawing.Point(274, 209);
            this.nudBurnInSeconds.Maximum = new decimal(new int[] {
            59,
            0,
            0,
            0});
            this.nudBurnInSeconds.Name = "nudBurnInSeconds";
            this.nudBurnInSeconds.Size = new System.Drawing.Size(48, 21);
            this.nudBurnInSeconds.TabIndex = 12;
            this.nudBurnInSeconds.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // lblTemp - "极限温度"设置项名称（左对齐）
            //
            this.lblTemp.AutoSize = true;
            this.lblTemp.Location = new System.Drawing.Point(30, 253);
            this.lblTemp.Name = "lblTemp";
            this.lblTemp.Size = new System.Drawing.Size(65, 12);
            this.lblTemp.TabIndex = 10;
            this.lblTemp.Text = "极限温度:";
            //
            // nudTemp - 极限温度输入框（【V1.63】TextBox 改 NumericUpDown：
            // 与配方管理窗 nudLimitTemp 对齐：1 位小数/步进 0.5/范围 0~300，
            // 非法输入根本进不来，V1.62 的"非法存 0"问题从输入端消除）
            //
            this.nudTemp.DecimalPlaces = 1;
            this.nudTemp.Increment = 0.5M;
            this.nudTemp.Location = new System.Drawing.Point(150, 250);
            this.nudTemp.Maximum = new decimal(new int[] {
            300,
            0,
            0,
            0});
            this.nudTemp.Name = "nudTemp";
            this.nudTemp.Size = new System.Drawing.Size(180, 21);
            this.nudTemp.TabIndex = 11;
            this.nudTemp.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // lblPressure - "负压阈值"设置项名称（左对齐）
            //
            // 【V1.66】本工位真空工艺要求（kPa）：回填优先级 缓存 > 配方 > 全局；
            // 下发=框里是什么就是什么（存什么定格什么，无魔法值）。项目未上线无老包袱。
            //
            this.lblPressure.AutoSize = true;
            this.lblPressure.Location = new System.Drawing.Point(30, 291);
            this.lblPressure.Name = "lblPressure";
            this.lblPressure.Size = new System.Drawing.Size(65, 12);
            this.lblPressure.TabIndex = 10;
            this.lblPressure.Text = "负压阈值:";
            //
            // nudPressure - 负压阈值输入框（与配方管理窗 nudNegativePressure 对齐：
            // 1 位小数/步进 0.5/范围 ±9999）
            //
            this.nudPressure.DecimalPlaces = 1;
            this.nudPressure.Increment = 0.5M;
            this.nudPressure.Location = new System.Drawing.Point(150, 288);
            this.nudPressure.Maximum = new decimal(new int[] {
            9999,
            0,
            0,
            0});
            this.nudPressure.Minimum = new decimal(new int[] {
            9999,
            0,
            0,
            -2147483648});
            this.nudPressure.Name = "nudPressure";
            this.nudPressure.Size = new System.Drawing.Size(120, 21);
            this.nudPressure.TabIndex = 17;
            this.nudPressure.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // lblPressureUnit - 负压单位
            //
            this.lblPressureUnit.AutoSize = true;
            this.lblPressureUnit.Location = new System.Drawing.Point(275, 291);
            this.lblPressureUnit.Name = "lblPressureUnit";
            this.lblPressureUnit.Size = new System.Drawing.Size(23, 12);
            this.lblPressureUnit.TabIndex = 10;
            this.lblPressureUnit.Text = "kPa";
            //
            // lblDisplayMode - "显示模式"设置项名称（左对齐）
            //
            // 【V1.66】烧屏画面记录（自由文本）：回填优先级同负压；下发走 SetStationRecipe。
            //
            this.lblDisplayMode.AutoSize = true;
            this.lblDisplayMode.Location = new System.Drawing.Point(30, 334);
            this.lblDisplayMode.Name = "lblDisplayMode";
            this.lblDisplayMode.Size = new System.Drawing.Size(65, 12);
            this.lblDisplayMode.TabIndex = 10;
            this.lblDisplayMode.Text = "显示模式:";
            //
            // cmbDisplayMode
            //
            this.cmbDisplayMode.DropDownStyle = Sunny.UI.UIDropDownStyle.DropDownList;
            this.cmbDisplayMode.Location = new System.Drawing.Point(150, 326);
            this.cmbDisplayMode.Name = "cmbDisplayMode";
            this.cmbDisplayMode.Size = new System.Drawing.Size(180, 29);
            this.cmbDisplayMode.TabIndex = 18;
            //
            // btnBreakVacuum - 破空按钮（功能待确认）
            //
            this.btnBreakVacuum.FillColor = System.Drawing.Color.DodgerBlue;
            this.btnBreakVacuum.RectColor = System.Drawing.Color.DodgerBlue;
            this.btnBreakVacuum.ForeColor = System.Drawing.Color.White;
            this.btnBreakVacuum.Style = Sunny.UI.UIStyle.Custom;
            this.btnBreakVacuum.Location = new System.Drawing.Point(370, 60);
            this.btnBreakVacuum.Name = "btnBreakVacuum";
            this.btnBreakVacuum.Size = new System.Drawing.Size(90, 30);
            this.btnBreakVacuum.TabIndex = 12;
            this.btnBreakVacuum.Text = "破空";
            this.btnBreakVacuum.Click += new System.EventHandler(this.btnBreakVacuum_Click);
            //
            // btnPowerOff - 下电按钮（功能待确认）
            //
            this.btnPowerOff.FillColor = System.Drawing.Color.DodgerBlue;
            this.btnPowerOff.RectColor = System.Drawing.Color.DodgerBlue;
            this.btnPowerOff.ForeColor = System.Drawing.Color.White;
            this.btnPowerOff.Style = Sunny.UI.UIStyle.Custom;
            this.btnPowerOff.Location = new System.Drawing.Point(370, 101);
            this.btnPowerOff.Name = "btnPowerOff";
            this.btnPowerOff.Size = new System.Drawing.Size(90, 30);
            this.btnPowerOff.TabIndex = 13;
            this.btnPowerOff.Text = "下电";
            this.btnPowerOff.Click += new System.EventHandler(this.btnPowerOff_Click);
            //
            // btnSave - 保存按钮（功能待确认）
            //
            this.btnSave.FillColor = System.Drawing.Color.LimeGreen;
            this.btnSave.RectColor = System.Drawing.Color.LimeGreen;
            this.btnSave.ForeColor = System.Drawing.Color.White;
            this.btnSave.Style = Sunny.UI.UIStyle.Custom;
            this.btnSave.Location = new System.Drawing.Point(370, 142);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(90, 30);
            this.btnSave.TabIndex = 14;
            this.btnSave.Text = "保存";
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            //
            // btnAddToQueue - 加入对列按钮（功能待确认）
            //
            this.btnAddToQueue.FillColor = System.Drawing.Color.LimeGreen;
            this.btnAddToQueue.RectColor = System.Drawing.Color.LimeGreen;
            this.btnAddToQueue.ForeColor = System.Drawing.Color.White;
            this.btnAddToQueue.Style = Sunny.UI.UIStyle.Custom;
            this.btnAddToQueue.Location = new System.Drawing.Point(370, 183);
            this.btnAddToQueue.Name = "btnAddToQueue";
            this.btnAddToQueue.Size = new System.Drawing.Size(90, 30);
            this.btnAddToQueue.TabIndex = 15;
            this.btnAddToQueue.Text = "加入对列";
            this.btnAddToQueue.Click += new System.EventHandler(this.btnAddToQueue_Click);
            //
            // btnClose - 关闭窗口按钮
            //
            this.btnClose.FillColor = System.Drawing.Color.DimGray;
            this.btnClose.RectColor = System.Drawing.Color.DimGray;
            this.btnClose.ForeColor = System.Drawing.Color.White;
            this.btnClose.Style = Sunny.UI.UIStyle.Custom;
            this.btnClose.Location = new System.Drawing.Point(370, 224);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(90, 30);
            this.btnClose.TabIndex = 16;
            this.btnClose.Text = "关闭窗口";
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            //
            // StationSettingsForm - 窗体属性设置
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(490, 370);
            // 【V1.71】绝对布局：禁缩小（MinimumSize=ClientSize），防缩坏布局；可放大。
            // 【V1.72.6】输入框 21→29 后末两行间距只剩 6px，窗加高 20（350→370），
            // 负压行下移 6、显示行下移 17，行隙回到 15+。
            this.MinimumSize = new System.Drawing.Size(490, 370);
            // 【V1.71】UIForm 自绘蓝标题：删 FormBorderStyle，内容整体上移 35px 已逐项下移。
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.btnAddToQueue);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.btnPowerOff);
            this.Controls.Add(this.btnBreakVacuum);
            this.Controls.Add(this.cmbDisplayMode);
            this.Controls.Add(this.lblDisplayMode);
            this.Controls.Add(this.lblPressureUnit);
            this.Controls.Add(this.nudPressure);
            this.Controls.Add(this.lblPressure);
            this.Controls.Add(this.nudTemp);
            this.Controls.Add(this.lblTemp);
            this.Controls.Add(this.nudBurnInSeconds);
            this.Controls.Add(this.lblBurnInColon2);
            this.Controls.Add(this.nudBurnInMinutes);
            this.Controls.Add(this.lblBurnInColon1);
            this.Controls.Add(this.nudBurnInHours);
            this.Controls.Add(this.lblBurnIn);
            this.Controls.Add(this.nudDelaySeconds);
            this.Controls.Add(this.lblDelayColon2);
            this.Controls.Add(this.nudDelayMinutes);
            this.Controls.Add(this.lblDelayColon1);
            this.Controls.Add(this.nudDelayHours);
            this.Controls.Add(this.lblDelay);
            this.Controls.Add(this.txtRecipe);
            this.Controls.Add(this.lblRecipe);
            this.Controls.Add(this.txtSN);
            this.Controls.Add(this.lblSN);
            this.Controls.Add(this.txtState);
            this.Controls.Add(this.lblState);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "StationSettingsForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "工位设置窗口";
            ((System.ComponentModel.ISupportInitialize)(this.nudDelayHours)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudDelayMinutes)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudDelaySeconds)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudBurnInHours)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudBurnInMinutes)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudBurnInSeconds)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudTemp)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudPressure)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        // 控件字段声明区域
        /// <summary>"状态"设置项名称标签</summary>
        private Sunny.UI.UILabel lblState;
        /// <summary>状态显示输入框（只读）</summary>
        private Sunny.UI.UITextBox txtState;
        /// <summary>"SN"设置项名称标签</summary>
        private Sunny.UI.UILabel lblSN;
        /// <summary>SN输入框</summary>
        private Sunny.UI.UITextBox txtSN;
        /// <summary>"配方"设置项名称标签</summary>
        private Sunny.UI.UILabel lblRecipe;
        /// <summary>配方输入框</summary>
        private Sunny.UI.UITextBox txtRecipe;
        /// <summary>"延时时间"设置项名称标签</summary>
        private Sunny.UI.UILabel lblDelay;
        /// <summary>延时时间-时输入（NumericUpDown，V1.28 由 TextBox 改）</summary>
        private System.Windows.Forms.NumericUpDown nudDelayHours;
        /// <summary>延时时间-时/分冒号分隔符</summary>
        private Sunny.UI.UILabel lblDelayColon1;
        /// <summary>延时时间-分输入（NumericUpDown，V1.28 由 TextBox 改）</summary>
        private System.Windows.Forms.NumericUpDown nudDelayMinutes;
        /// <summary>延时时间-分/秒冒号分隔符</summary>
        private Sunny.UI.UILabel lblDelayColon2;
        /// <summary>延时时间-秒输入（NumericUpDown，V1.28 由 TextBox 改）</summary>
        private System.Windows.Forms.NumericUpDown nudDelaySeconds;
        /// <summary>"烧屏时间"设置项名称标签</summary>
        private Sunny.UI.UILabel lblBurnIn;
        /// <summary>烧屏时间-时输入（NumericUpDown，V1.28 由 TextBox 改）</summary>
        private System.Windows.Forms.NumericUpDown nudBurnInHours;
        /// <summary>烧屏时间-时/分冒号分隔符</summary>
        private Sunny.UI.UILabel lblBurnInColon1;
        /// <summary>烧屏时间-分输入（NumericUpDown，V1.28 由 TextBox 改）</summary>
        private System.Windows.Forms.NumericUpDown nudBurnInMinutes;
        /// <summary>烧屏时间-分/秒冒号分隔符</summary>
        private Sunny.UI.UILabel lblBurnInColon2;
        /// <summary>烧屏时间-秒输入（NumericUpDown，V1.28 由 TextBox 改）</summary>
        private System.Windows.Forms.NumericUpDown nudBurnInSeconds;
        /// <summary>"极限温度"设置项名称标签</summary>
        private Sunny.UI.UILabel lblTemp;
        /// <summary>极限温度输入（NumericUpDown，V1.63 由 TextBox 改，与配方管理窗对齐）</summary>
        private System.Windows.Forms.NumericUpDown nudTemp;
        /// <summary>"负压阈值"设置项名称标签（【V1.66】本工位真空工艺要求，kPa）</summary>
        private Sunny.UI.UILabel lblPressure;
        /// <summary>负压阈值输入（NumericUpDown，V1.66：1位小数/步进0.5/范围±9999）</summary>
        private System.Windows.Forms.NumericUpDown nudPressure;
        /// <summary>负压单位标签（kPa）</summary>
        private Sunny.UI.UILabel lblPressureUnit;
        /// <summary>"显示模式"设置项名称标签（【V1.66】烧屏画面记录）</summary>
        private Sunny.UI.UILabel lblDisplayMode;
        /// <summary>显示模式输入框（【V1.66】自由文本，最长50）</summary>
        private Sunny.UI.UIComboBox cmbDisplayMode;
        /// <summary>破空按钮（功能待确认）</summary>
        private Sunny.UI.UIButton btnBreakVacuum;
        /// <summary>下电按钮（功能待确认）</summary>
        private Sunny.UI.UIButton btnPowerOff;
        /// <summary>保存按钮（功能待确认）</summary>
        private Sunny.UI.UIButton btnSave;
        /// <summary>加入对列按钮（功能待确认）</summary>
        private Sunny.UI.UIButton btnAddToQueue;
        /// <summary>关闭窗口按钮</summary>
        private Sunny.UI.UIButton btnClose;
    }
}
