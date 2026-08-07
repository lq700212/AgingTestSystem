namespace BarometerWinform.Views
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
    /// │ NO.1                                  │  ← 设备编号
    /// │ [上电状态灯] [上电/下电按钮]           │  ← 上电灯=纯色(绿=上电/灰=下电)；按钮控制载台上电
    /// │ 真空压力 [值] [真空开启灯] [工作状态]   │  ← 压力值只读；真空灯=纯色；工作状态文字
    /// │ SN:      [_________]                 │
    /// │ 配方:    [_________]                 │
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
            this.btnPower = new System.Windows.Forms.Button();
            this.lblVacuum = new System.Windows.Forms.Label();
            this.txtPressure = new System.Windows.Forms.TextBox();
            this.boxVacuumOpen = new System.Windows.Forms.Label();
            this.boxWorkState = new System.Windows.Forms.Label();
            this.lblSN = new System.Windows.Forms.Label();
            this.txtSN = new System.Windows.Forms.TextBox();
            this.lblRecipe = new System.Windows.Forms.Label();
            this.txtRecipe = new System.Windows.Forms.TextBox();
            this.lblDelayStart = new System.Windows.Forms.Label();
            this.txtDelayStart = new System.Windows.Forms.TextBox();
            this.lblDelayArrive = new System.Windows.Forms.Label();
            this.txtDelayArrive = new System.Windows.Forms.TextBox();
            this.btnSet = new System.Windows.Forms.Button();
            this.toolTipPanel = new System.Windows.Forms.ToolTip(this.components);
            this.SuspendLayout();
            //
            // lblDeviceId - 设备编号标签（左上角）
            //
            this.lblDeviceId.AutoSize = true;
            this.lblDeviceId.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold);
            this.lblDeviceId.Location = new System.Drawing.Point(3, 4);
            this.lblDeviceId.Name = "lblDeviceId";
            this.lblDeviceId.Size = new System.Drawing.Size(50, 17);
            this.lblDeviceId.TabIndex = 0;
            this.lblDeviceId.Text = "NO.1";
            //
            // boxPower - 上电状态灯（纯色，无文字）
            // 载台上电输出：ON=绿色，OFF=灰色。颜色在业务代码里更新。
            //
            this.boxPower.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.boxPower.Location = new System.Drawing.Point(6, 26);
            this.boxPower.Name = "boxPower";
            this.boxPower.Size = new System.Drawing.Size(55, 36);
            this.boxPower.AutoSize = false;
            this.boxPower.TabIndex = 1;
            this.boxPower.Text = "";
            this.boxPower.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.boxPower.BackColor = System.Drawing.Color.LightGray;
            //
            // btnPower - 上电/下电按钮（控制载台上电）
            // 文字显示"要执行的动作"：未上电显示"上电"（点击上电），已上电显示"下电"（点击下电）。
            // 测试中/故障时禁用（业务代码里设置）。
            //
            this.btnPower.Location = new System.Drawing.Point(65, 26);
            this.btnPower.Name = "btnPower";
            this.btnPower.Size = new System.Drawing.Size(85, 36);
            this.btnPower.TabIndex = 2;
            this.btnPower.Text = "上电";
            this.btnPower.UseVisualStyleBackColor = true;
            this.btnPower.Click += new System.EventHandler(this.btnPower_Click);
            //
            // lblVacuum - "真空压力"标签
            //
            this.lblVacuum.AutoSize = true;
            this.lblVacuum.Location = new System.Drawing.Point(3, 70);
            this.lblVacuum.Name = "lblVacuum";
            this.lblVacuum.Size = new System.Drawing.Size(53, 12);
            this.lblVacuum.TabIndex = 3;
            this.lblVacuum.Text = "真空压力";
            //
            // txtPressure - 真空压力值显示框（只读）
            //
            this.txtPressure.Location = new System.Drawing.Point(57, 67);
            this.txtPressure.Name = "txtPressure";
            this.txtPressure.ReadOnly = true;
            this.txtPressure.Size = new System.Drawing.Size(58, 21);
            this.txtPressure.TabIndex = 4;
            this.txtPressure.Text = "---";
            //
            // boxVacuumOpen - 真空开启状态灯（纯色，无文字）
            // 真空电磁阀输出：ON=绿色，OFF=灰色。颜色在业务代码里更新。
            //
            this.boxVacuumOpen.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.boxVacuumOpen.Location = new System.Drawing.Point(118, 67);
            this.boxVacuumOpen.Name = "boxVacuumOpen";
            this.boxVacuumOpen.Size = new System.Drawing.Size(55, 21);
            this.boxVacuumOpen.AutoSize = false;
            this.boxVacuumOpen.TabIndex = 5;
            this.boxVacuumOpen.Text = "";
            this.boxVacuumOpen.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.boxVacuumOpen.BackColor = System.Drawing.Color.LightGray;
            //
            // boxWorkState - 工作状态显示（IDLE / SELECT / BUSY / FAULT）
            // 空闲且未上电=IDLE；空闲但已上电=SELECT；测试中=BUSY；故障=FAULT。
            //
            this.boxWorkState.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.boxWorkState.Location = new System.Drawing.Point(176, 67);
            this.boxWorkState.Name = "boxWorkState";
            this.boxWorkState.Size = new System.Drawing.Size(60, 21);
            this.boxWorkState.AutoSize = false;
            this.boxWorkState.TabIndex = 6;
            this.boxWorkState.Text = "IDLE";
            this.boxWorkState.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.boxWorkState.BackColor = System.Drawing.Color.LightGray;
            //
            // lblSN - "SN:"标签
            //
            this.lblSN.AutoSize = true;
            this.lblSN.Location = new System.Drawing.Point(3, 96);
            this.lblSN.Name = "lblSN";
            this.lblSN.Size = new System.Drawing.Size(29, 12);
            this.lblSN.TabIndex = 7;
            this.lblSN.Text = "SN:";
            //
            // txtSN - 序列号显示框（只读）
            //
            this.txtSN.Location = new System.Drawing.Point(57, 93);
            this.txtSN.Name = "txtSN";
            this.txtSN.ReadOnly = true;
            this.txtSN.Size = new System.Drawing.Size(140, 21);
            this.txtSN.TabIndex = 8;
            //
            // lblRecipe - "配方"标签
            //
            this.lblRecipe.AutoSize = true;
            this.lblRecipe.Location = new System.Drawing.Point(3, 121);
            this.lblRecipe.Name = "lblRecipe";
            this.lblRecipe.Size = new System.Drawing.Size(29, 12);
            this.lblRecipe.TabIndex = 9;
            this.lblRecipe.Text = "配方:";
            //
            // txtRecipe - 配方名称显示框（只读）
            //
            this.txtRecipe.Location = new System.Drawing.Point(57, 118);
            this.txtRecipe.Name = "txtRecipe";
            this.txtRecipe.ReadOnly = true;
            this.txtRecipe.Size = new System.Drawing.Size(140, 21);
            this.txtRecipe.TabIndex = 10;
            //
            // lblDelayStart - "延时开启"标签
            //
            this.lblDelayStart.AutoSize = true;
            this.lblDelayStart.Location = new System.Drawing.Point(3, 146);
            this.lblDelayStart.Name = "lblDelayStart";
            this.lblDelayStart.Size = new System.Drawing.Size(53, 12);
            this.lblDelayStart.TabIndex = 11;
            this.lblDelayStart.Text = "延时开启";
            //
            // txtDelayStart - 延时开启时间显示框（只读，由数据填充）
            //
            this.txtDelayStart.Location = new System.Drawing.Point(57, 143);
            this.txtDelayStart.Name = "txtDelayStart";
            this.txtDelayStart.ReadOnly = true;
            this.txtDelayStart.Size = new System.Drawing.Size(80, 21);
            this.txtDelayStart.TabIndex = 12;
            this.txtDelayStart.Text = "00:00:00";
            //
            // lblDelayArrive - "延时到达"标签
            //
            this.lblDelayArrive.AutoSize = true;
            this.lblDelayArrive.Location = new System.Drawing.Point(3, 171);
            this.lblDelayArrive.Name = "lblDelayArrive";
            this.lblDelayArrive.Size = new System.Drawing.Size(53, 12);
            this.lblDelayArrive.TabIndex = 13;
            this.lblDelayArrive.Text = "延时到达";
            //
            // txtDelayArrive - 延时到达时间显示框（只读，由数据填充）
            //
            this.txtDelayArrive.Location = new System.Drawing.Point(57, 168);
            this.txtDelayArrive.Name = "txtDelayArrive";
            this.txtDelayArrive.ReadOnly = true;
            this.txtDelayArrive.Size = new System.Drawing.Size(80, 21);
            this.txtDelayArrive.TabIndex = 14;
            this.txtDelayArrive.Text = "00:00:00";
            //
            // btnSet - 延时设置按钮（合并开启和到达两个 Set 按钮）
            // 点击后由主窗体弹出单台手动控制窗口（DeviceManualForm）。
            //
            this.btnSet.BackColor = System.Drawing.Color.LimeGreen;
            this.btnSet.ForeColor = System.Drawing.Color.White;
            this.btnSet.Location = new System.Drawing.Point(145, 145);
            this.btnSet.Name = "btnSet";
            this.btnSet.Size = new System.Drawing.Size(60, 50);
            this.btnSet.TabIndex = 15;
            this.btnSet.Text = "Set";
            this.btnSet.UseVisualStyleBackColor = false;
            this.btnSet.Click += new System.EventHandler(this.btnSet_Click);
            //
            // WorkstationPanelView - 面板自身属性设置
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.Controls.Add(this.btnSet);
            this.Controls.Add(this.txtDelayArrive);
            this.Controls.Add(this.lblDelayArrive);
            this.Controls.Add(this.txtDelayStart);
            this.Controls.Add(this.lblDelayStart);
            this.Controls.Add(this.txtRecipe);
            this.Controls.Add(this.lblRecipe);
            this.Controls.Add(this.txtSN);
            this.Controls.Add(this.lblSN);
            this.Controls.Add(this.boxWorkState);
            this.Controls.Add(this.boxVacuumOpen);
            this.Controls.Add(this.txtPressure);
            this.Controls.Add(this.lblVacuum);
            this.Controls.Add(this.btnPower);
            this.Controls.Add(this.boxPower);
            this.Controls.Add(this.lblDeviceId);
            this.Name = "WorkstationPanelView";
            this.Size = new System.Drawing.Size(240, 225);
            this.ResumeLayout(false);
            this.PerformLayout();

            // 为"纯色状态灯"加上鼠标悬停提示，方便操作员理解灯的含义
            this.toolTipPanel.SetToolTip(this.boxPower, "上电状态：绿=已上电，灰=未上电");
            this.toolTipPanel.SetToolTip(this.boxVacuumOpen, "真空开启状态：绿=开启，灰=关闭");
        }

        #endregion

        // 控件字段声明区域
        // 这些字段在两个 partial 文件中共享（本文件赋值，.cs文件使用）

        /// <summary>设备编号标签（左上角）</summary>
        private System.Windows.Forms.Label lblDeviceId;
        /// <summary>上电状态灯（纯色：绿=已上电，灰=未上电）</summary>
        private System.Windows.Forms.Label boxPower;
        /// <summary>上电/下电按钮（控制载台上电）</summary>
        private System.Windows.Forms.Button btnPower;
        /// <summary>"真空压力"标签</summary>
        private System.Windows.Forms.Label lblVacuum;
        /// <summary>真空压力值显示框（只读）</summary>
        private System.Windows.Forms.TextBox txtPressure;
        /// <summary>真空开启状态灯（纯色：绿=开启，灰=关闭）</summary>
        private System.Windows.Forms.Label boxVacuumOpen;
        /// <summary>工作状态显示（IDLE/SELECT/BUSY/FAULT）</summary>
        private System.Windows.Forms.Label boxWorkState;
        /// <summary>"SN:"标签</summary>
        private System.Windows.Forms.Label lblSN;
        /// <summary>序列号显示框（只读）</summary>
        private System.Windows.Forms.TextBox txtSN;
        /// <summary>"配方:"标签</summary>
        private System.Windows.Forms.Label lblRecipe;
        /// <summary>配方名称显示框（只读）</summary>
        private System.Windows.Forms.TextBox txtRecipe;
        /// <summary>"延时开启"标签</summary>
        private System.Windows.Forms.Label lblDelayStart;
        /// <summary>延时开启时间显示框（只读）</summary>
        private System.Windows.Forms.TextBox txtDelayStart;
        /// <summary>"延时到达"标签</summary>
        private System.Windows.Forms.Label lblDelayArrive;
        /// <summary>延时到达时间显示框（只读）</summary>
        private System.Windows.Forms.TextBox txtDelayArrive;
        /// <summary>延时设置按钮（弹出单台手动控制窗口）</summary>
        private System.Windows.Forms.Button btnSet;
        /// <summary>纯色状态灯的悬停提示</summary>
        private System.Windows.Forms.ToolTip toolTipPanel;
    }
}
