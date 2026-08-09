namespace AgingTestSystem.Views
{
    /// <summary>
    /// 工位显示面板 —— 设计器自动生成部分
    ///
    /// 【说明】
    /// 本文件由 Visual Studio 设计器维护，包含所有控件的创建和布局代码。
    /// 业务逻辑代码放在 WorkstationPanelView.cs 文件中。
    ///
    /// 【V1.16 重设计（依据现场草图）】
    /// 面板显示的是"工位"（每个工位对应一台气压表）：
    /// ┌──────────────────────────────┐
    /// │ NO.1                  [□✓]   │  ← 设备编号 + 选中指示（右上角，V1.19）
    /// │ [上电/下电] [工作状态]         │  ← 上电状态灯/工作状态（文字+颜色，V1.24）
    /// │ 真空压力 [值] [真空开]         │  ← 压力值只读（V1.19.10 加宽）；真空开/关=文字+颜色
    /// │ SN:    [SN值 Label]                 │  ← V1.19.3：内容改为 Label 显示
    /// │ 配方:  [配方值 Label]               │
    /// │ 延时开启 [__:__:__]      ┌────┐      │
    /// │ 延时到达 [__:__:__]      │ Set│      │
    /// │                          └────┘      │
    /// └──────────────────────────────┘
    /// 为什么需要单独的 Designer.cs 文件？
    /// WinForms 设计器依赖"partial class"分部类机制，将界面布局代码（本文件）
    /// 与业务逻辑代码（.cs 文件）分离。设计器只解析 Designer.cs 文件中的
    /// InitializeComponent 方法来渲染设计视图。
    /// </summary>
    partial class WorkstationPanelView
    {
        /// <summary>
        /// 必需的设计器变量
        /// 用于管理设计器创建的组件资源
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源
        /// 在控件被销毁时调用，释放组件资源
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

        #region 组件设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要使用代码编辑器修改此方法的内容
        /// 此方法负责创建所有控件并设置布局属性
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.lblDeviceId = new System.Windows.Forms.Label();
            this.boxPower = new System.Windows.Forms.Label();
            this.btnSelect = new System.Windows.Forms.Button();
            this.lblVacuum = new System.Windows.Forms.Label();
            this.txtPressure = new System.Windows.Forms.TextBox();
            this.boxVacuumOpen = new System.Windows.Forms.Label();
            this.boxWorkState = new System.Windows.Forms.Label();
            this.lblSN = new System.Windows.Forms.Label();
            this.lblSNValue = new System.Windows.Forms.Label();
            this.lblRecipe = new System.Windows.Forms.Label();
            this.lblRecipeValue = new System.Windows.Forms.Label();
            this.lblDelayStart = new System.Windows.Forms.Label();
            this.txtDelayStart = new System.Windows.Forms.TextBox();
            this.lblDelayArrive = new System.Windows.Forms.Label();
            this.txtDelayArrive = new System.Windows.Forms.TextBox();
            this.btnSet = new System.Windows.Forms.Button();
            this.toolTipPanel = new System.Windows.Forms.ToolTip(this.components);
            this.SuspendLayout();
            // 
            // lblDeviceId
            // 
            this.lblDeviceId.AutoSize = true;
            this.lblDeviceId.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold);
            this.lblDeviceId.Location = new System.Drawing.Point(3, 4);
            this.lblDeviceId.Name = "lblDeviceId";
            this.lblDeviceId.Size = new System.Drawing.Size(38, 17);
            this.lblDeviceId.TabIndex = 0;
            this.lblDeviceId.Text = "NO.1";
            // 
            // boxPower
            // 
            this.boxPower.BackColor = System.Drawing.Color.LightGray;
            this.boxPower.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.boxPower.ForeColor = System.Drawing.Color.Black;
            this.boxPower.Location = new System.Drawing.Point(57, 29);
            this.boxPower.Name = "boxPower";
            this.boxPower.Size = new System.Drawing.Size(52, 23);
            this.boxPower.TabIndex = 1;
            this.boxPower.Text = "下电";
            this.boxPower.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.toolTipPanel.SetToolTip(this.boxPower, "上电状态（V1.28）：绿(LimeGreen)=上电，浅灰(LightGray)=下电");
            // 
            // btnSelect
            // 
            this.btnSelect.FlatAppearance.BorderColor = System.Drawing.Color.Black;
            this.btnSelect.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSelect.Location = new System.Drawing.Point(212, 4);
            this.btnSelect.Name = "btnSelect";
            this.btnSelect.Size = new System.Drawing.Size(23, 23);
            this.btnSelect.TabIndex = 2;
            this.toolTipPanel.SetToolTip(this.btnSelect, "选中指示（V1.19.6）：空白处长按约0.8秒=选中(绿✓)；有选中后单击空白处或本框=切换选中/取消；整表唯一选中项被取消时全部隐藏");
            this.btnSelect.UseVisualStyleBackColor = false;
            this.btnSelect.Visible = false;
            this.btnSelect.Click += new System.EventHandler(this.btnSelect_Click);
            // 
            // lblVacuum
            // 
            this.lblVacuum.AutoSize = true;
            this.lblVacuum.Location = new System.Drawing.Point(3, 70);
            this.lblVacuum.Name = "lblVacuum";
            this.lblVacuum.Size = new System.Drawing.Size(53, 12);
            this.lblVacuum.TabIndex = 3;
            this.lblVacuum.Text = "真空压力";
            // 
            // txtPressure
            // 
            this.txtPressure.Location = new System.Drawing.Point(57, 67);
            this.txtPressure.Name = "txtPressure";
            this.txtPressure.ReadOnly = true;
            this.txtPressure.Size = new System.Drawing.Size(78, 21);
            this.txtPressure.TabIndex = 4;
            this.txtPressure.Text = "---";
            // 
            // boxVacuumOpen
            // 
            this.boxVacuumOpen.BackColor = System.Drawing.Color.LightGray;
            this.boxVacuumOpen.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.boxVacuumOpen.ForeColor = System.Drawing.Color.Black;
            this.boxVacuumOpen.Location = new System.Drawing.Point(138, 67);
            this.boxVacuumOpen.Name = "boxVacuumOpen";
            this.boxVacuumOpen.Size = new System.Drawing.Size(48, 21);
            this.boxVacuumOpen.TabIndex = 5;
            this.boxVacuumOpen.Text = "真空关";
            this.boxVacuumOpen.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.toolTipPanel.SetToolTip(this.boxVacuumOpen, "真空开启状态（V1.28）：真空开=绿底，真空关=浅灰(LightGray)底");
            // 
            // boxWorkState
            // 
            this.boxWorkState.BackColor = System.Drawing.Color.LimeGreen;
            this.boxWorkState.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.boxWorkState.ForeColor = System.Drawing.Color.White;
            this.boxWorkState.Location = new System.Drawing.Point(138, 29);
            this.boxWorkState.Name = "boxWorkState";
            this.boxWorkState.Size = new System.Drawing.Size(52, 23);
            this.boxWorkState.TabIndex = 6;
            this.boxWorkState.Text = "空闲";
            this.boxWorkState.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.toolTipPanel.SetToolTip(this.boxWorkState, "工作状态（V1.24 配色）：空闲=绿 / 选中(已上电待测试)=橙 / 繁忙(测试中)=黄 / 故障=红");
            // 
            // lblSN
            // 
            this.lblSN.AutoSize = true;
            this.lblSN.Location = new System.Drawing.Point(3, 96);
            this.lblSN.Name = "lblSN";
            this.lblSN.Size = new System.Drawing.Size(23, 12);
            this.lblSN.TabIndex = 7;
            this.lblSN.Text = "SN:";
            // 
            // lblSNValue
            // 
            this.lblSNValue.BackColor = System.Drawing.Color.White;
            this.lblSNValue.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblSNValue.Location = new System.Drawing.Point(57, 93);
            this.lblSNValue.Name = "lblSNValue";
            this.lblSNValue.Size = new System.Drawing.Size(140, 21);
            this.lblSNValue.TabIndex = 8;
            this.lblSNValue.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblRecipe
            // 
            this.lblRecipe.AutoSize = true;
            this.lblRecipe.Location = new System.Drawing.Point(3, 121);
            this.lblRecipe.Name = "lblRecipe";
            this.lblRecipe.Size = new System.Drawing.Size(35, 12);
            this.lblRecipe.TabIndex = 9;
            this.lblRecipe.Text = "配方:";
            // 
            // lblRecipeValue
            // 
            this.lblRecipeValue.BackColor = System.Drawing.Color.White;
            this.lblRecipeValue.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblRecipeValue.Location = new System.Drawing.Point(57, 118);
            this.lblRecipeValue.Name = "lblRecipeValue";
            this.lblRecipeValue.Size = new System.Drawing.Size(140, 21);
            this.lblRecipeValue.TabIndex = 10;
            this.lblRecipeValue.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblDelayStart
            // 
            this.lblDelayStart.AutoSize = true;
            this.lblDelayStart.Location = new System.Drawing.Point(3, 146);
            this.lblDelayStart.Name = "lblDelayStart";
            this.lblDelayStart.Size = new System.Drawing.Size(53, 12);
            this.lblDelayStart.TabIndex = 11;
            this.lblDelayStart.Text = "延时开启";
            // 
            // txtDelayStart
            // 
            this.txtDelayStart.Location = new System.Drawing.Point(57, 143);
            this.txtDelayStart.Name = "txtDelayStart";
            this.txtDelayStart.ReadOnly = true;
            this.txtDelayStart.Size = new System.Drawing.Size(80, 21);
            this.txtDelayStart.TabIndex = 12;
            this.txtDelayStart.Text = "00:00:00";
            // 
            // lblDelayArrive
            // 
            this.lblDelayArrive.AutoSize = true;
            this.lblDelayArrive.Location = new System.Drawing.Point(3, 171);
            this.lblDelayArrive.Name = "lblDelayArrive";
            this.lblDelayArrive.Size = new System.Drawing.Size(53, 12);
            this.lblDelayArrive.TabIndex = 13;
            this.lblDelayArrive.Text = "延时到达";
            // 
            // txtDelayArrive
            // 
            this.txtDelayArrive.Location = new System.Drawing.Point(57, 168);
            this.txtDelayArrive.Name = "txtDelayArrive";
            this.txtDelayArrive.ReadOnly = true;
            this.txtDelayArrive.Size = new System.Drawing.Size(80, 21);
            this.txtDelayArrive.TabIndex = 14;
            this.txtDelayArrive.Text = "00:00:00";
            // 
            // btnSet
            // 
            this.btnSet.BackColor = System.Drawing.Color.LimeGreen;
            this.btnSet.ForeColor = System.Drawing.Color.White;
            this.btnSet.Location = new System.Drawing.Point(145, 145);
            this.btnSet.Name = "btnSet";
            this.btnSet.Size = new System.Drawing.Size(60, 50);
            this.btnSet.TabIndex = 15;
            this.btnSet.Text = "设置";
            this.btnSet.UseVisualStyleBackColor = false;
            this.btnSet.Click += new System.EventHandler(this.btnSet_Click);
            // 
            // WorkstationPanelView
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.Controls.Add(this.btnSet);
            this.Controls.Add(this.txtDelayArrive);
            this.Controls.Add(this.lblDelayArrive);
            this.Controls.Add(this.txtDelayStart);
            this.Controls.Add(this.lblDelayStart);
            this.Controls.Add(this.lblRecipeValue);
            this.Controls.Add(this.lblRecipe);
            this.Controls.Add(this.lblSNValue);
            this.Controls.Add(this.lblSN);
            this.Controls.Add(this.boxWorkState);
            this.Controls.Add(this.boxVacuumOpen);
            this.Controls.Add(this.txtPressure);
            this.Controls.Add(this.lblVacuum);
            this.Controls.Add(this.btnSelect);
            this.Controls.Add(this.boxPower);
            this.Controls.Add(this.lblDeviceId);
            this.Name = "WorkstationPanelView";
            this.Size = new System.Drawing.Size(240, 205);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        // 控件字段声明区域
        // 这些字段在两个 partial 文件中共享（本文件赋值，.cs文件使用）

        /// <summary>设备编号标签（左上角）</summary>
        private System.Windows.Forms.Label lblDeviceId;
        /// <summary>上电状态灯（文字+颜色：绿=LimeGreen"上电"白字，浅灰="下电"黑字，V1.24/V1.28）</summary>
        private System.Windows.Forms.Label boxPower;
        /// <summary>选中指示/切换按钮（右上角，原 btnPower 改为选中指示，V1.19）</summary>
        private System.Windows.Forms.Button btnSelect;
        /// <summary>"真空压力"标签</summary>
        private System.Windows.Forms.Label lblVacuum;
        /// <summary>真空压力值显示框（只读）</summary>
        private System.Windows.Forms.TextBox txtPressure;
        /// <summary>真空开启状态显示（V1.19.10 起带文字+颜色：真空开=绿底白字，真空关=浅灰底黑字，V1.28 由红改灰）</summary>
        private System.Windows.Forms.Label boxVacuumOpen;
        /// <summary>工作状态显示（空闲/选中/繁忙/故障，V1.19.4 起带"信号灯"配色）</summary>
        private System.Windows.Forms.Label boxWorkState;
        /// <summary>"SN:"标签</summary>
        private System.Windows.Forms.Label lblSN;
        /// <summary>序列号显示标签（V1.19.3：由只读 TextBox 改为 Label）</summary>
        private System.Windows.Forms.Label lblSNValue;
        /// <summary>"配方:"标签</summary>
        private System.Windows.Forms.Label lblRecipe;
        /// <summary>配方名称显示标签（V1.19.3：由只读 TextBox 改为 Label）</summary>
        private System.Windows.Forms.Label lblRecipeValue;
        /// <summary>"延时开启"标签</summary>
        private System.Windows.Forms.Label lblDelayStart;
        /// <summary>延时开启时间显示框（只读）</summary>
        private System.Windows.Forms.TextBox txtDelayStart;
        /// <summary>"延时到达"标签</summary>
        private System.Windows.Forms.Label lblDelayArrive;
        /// <summary>延时到达时间显示框（只读）</summary>
        private System.Windows.Forms.TextBox txtDelayArrive;
        /// <summary>工位设置按钮（点击弹出工位设置窗口，V1.18 更名：Set → 设置）</summary>
        private System.Windows.Forms.Button btnSet;
        /// <summary>纯色状态灯的悬停提示</summary>
        private System.Windows.Forms.ToolTip toolTipPanel;
    }
}
