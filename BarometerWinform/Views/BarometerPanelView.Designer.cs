namespace BarometerWinform.Views
{
    /// <summary>
    /// 气压表显示面板 —— 设计器自动生成部分
    ///
    /// 【说明】
    /// 本文件由 Visual Studio 设计器维护，包含所有控件的创建和布局代码。
    /// 请勿手动修改此文件内容，所有修改请通过设计器界面操作。
    /// 业务逻辑代码请放在 BarometerPanelView.cs 文件中。
    ///
    /// 为什么需要单独的 Designer.cs 文件？
    /// WinForms 设计器依赖"partial class"分部类机制，将界面布局代码（本文件）
    /// 与业务逻辑代码（.cs 文件）分离。设计器只解析 Designer.cs 文件中的
    /// InitializeComponent 方法来渲染设计视图。如果不拆分，设计器会因无法
    /// 正确识别类结构而报错（如"无法设计基类 System.Void"）。
    /// </summary>
    partial class BarometerPanelView
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
            // 创建所有控件实例
            this.lblDeviceId = new System.Windows.Forms.Label();
            this.lblPressure = new System.Windows.Forms.Label();
            this.txtPressure = new System.Windows.Forms.TextBox();
            this.lblSN = new System.Windows.Forms.Label();
            this.txtSN = new System.Windows.Forms.TextBox();
            this.lblRecipe = new System.Windows.Forms.Label();
            this.txtRecipe = new System.Windows.Forms.TextBox();
            this.lblDelayStart = new System.Windows.Forms.Label();
            this.txtDelayStart = new System.Windows.Forms.TextBox();
            this.btnSet = new System.Windows.Forms.Button();
            this.lblDelayArrive = new System.Windows.Forms.Label();
            this.txtDelayArrive = new System.Windows.Forms.TextBox();
            this.boxInput1 = new System.Windows.Forms.Label();
            this.boxOutput1 = new System.Windows.Forms.Label();
            this.boxOutput2 = new System.Windows.Forms.Label();
            this.lblStatus = new System.Windows.Forms.Label();
            this.SuspendLayout();
            //
            // lblDeviceId - 设备编号标签（左上角）
            //
            this.lblDeviceId.AutoSize = true;
            this.lblDeviceId.Font = new System.Drawing.Font("微软雅黑", 9F, System.Drawing.FontStyle.Bold);
            this.lblDeviceId.Location = new System.Drawing.Point(3, 3);
            this.lblDeviceId.Name = "lblDeviceId";
            this.lblDeviceId.Size = new System.Drawing.Size(50, 17);
            this.lblDeviceId.TabIndex = 0;
            this.lblDeviceId.Text = "NO.1";
            //
            // lblPressure - "真空压力"标签
            //
            this.lblPressure.AutoSize = true;
            this.lblPressure.Location = new System.Drawing.Point(3, 80);
            this.lblPressure.Name = "lblPressure";
            this.lblPressure.Size = new System.Drawing.Size(53, 12);
            this.lblPressure.TabIndex = 1;
            this.lblPressure.Text = "真空压力";
            //
            // txtPressure - 真空压力值显示框（只读）
            //
            this.txtPressure.Location = new System.Drawing.Point(60, 77);
            this.txtPressure.Name = "txtPressure";
            this.txtPressure.ReadOnly = true;
            this.txtPressure.Size = new System.Drawing.Size(150, 21);
            this.txtPressure.TabIndex = 2;
            //
            // lblSN - "SN:"标签
            //
            this.lblSN.AutoSize = true;
            this.lblSN.Location = new System.Drawing.Point(3, 107);
            this.lblSN.Name = "lblSN";
            this.lblSN.Size = new System.Drawing.Size(29, 12);
            this.lblSN.TabIndex = 3;
            this.lblSN.Text = "SN:";
            //
            // txtSN - 序列号显示框（只读）
            //
            this.txtSN.Location = new System.Drawing.Point(60, 104);
            this.txtSN.Name = "txtSN";
            this.txtSN.ReadOnly = true;
            this.txtSN.Size = new System.Drawing.Size(150, 21);
            this.txtSN.TabIndex = 4;
            //
            // lblRecipe - "配方"标签
            //
            this.lblRecipe.AutoSize = true;
            this.lblRecipe.Location = new System.Drawing.Point(3, 134);
            this.lblRecipe.Name = "lblRecipe";
            this.lblRecipe.Size = new System.Drawing.Size(29, 12);
            this.lblRecipe.TabIndex = 5;
            this.lblRecipe.Text = "配方";
            //
            // txtRecipe - 配方名称显示框（只读）
            //
            this.txtRecipe.Location = new System.Drawing.Point(60, 131);
            this.txtRecipe.Name = "txtRecipe";
            this.txtRecipe.ReadOnly = true;
            this.txtRecipe.Size = new System.Drawing.Size(150, 21);
            this.txtRecipe.TabIndex = 6;
            //
            // lblDelayStart - "延时开启"标签
            //
            this.lblDelayStart.AutoSize = true;
            this.lblDelayStart.Location = new System.Drawing.Point(3, 161);
            this.lblDelayStart.Name = "lblDelayStart";
            this.lblDelayStart.Size = new System.Drawing.Size(53, 12);
            this.lblDelayStart.TabIndex = 7;
            this.lblDelayStart.Text = "延时开启";
            //
            // txtDelayStart - 延时开启时间输入框
            //
            this.txtDelayStart.Location = new System.Drawing.Point(60, 158);
            this.txtDelayStart.Name = "txtDelayStart";
            this.txtDelayStart.Size = new System.Drawing.Size(80, 21);
            this.txtDelayStart.TabIndex = 8;
            this.txtDelayStart.Text = "00:00:00";
            //
            // btnSet - 延时设置按钮（合并开启和到达两个Set按钮）
            // 位置：与原上方Set按钮顶部对齐(y=156)
            // 高度：从原上方按钮顶部(y=156)到原下方按钮底部(y=183+23=206)，共50像素
            // 点击后由主窗体决定具体设置内容（延时开启/延时到达）
            //
            this.btnSet.BackColor = System.Drawing.Color.LimeGreen;
            this.btnSet.ForeColor = System.Drawing.Color.White;
            this.btnSet.Location = new System.Drawing.Point(146, 156);
            this.btnSet.Name = "btnSet";
            this.btnSet.Size = new System.Drawing.Size(64, 50);
            this.btnSet.TabIndex = 9;
            this.btnSet.Text = "Set";
            this.btnSet.UseVisualStyleBackColor = false;
            this.btnSet.Click += new System.EventHandler(this.btnSet_Click);
            //
            // lblDelayArrive - "延时到达"标签
            //
            this.lblDelayArrive.AutoSize = true;
            this.lblDelayArrive.Location = new System.Drawing.Point(3, 188);
            this.lblDelayArrive.Name = "lblDelayArrive";
            this.lblDelayArrive.Size = new System.Drawing.Size(53, 12);
            this.lblDelayArrive.TabIndex = 10;
            this.lblDelayArrive.Text = "延时到达";
            //
            // txtDelayArrive - 延时到达时间输入框
            //
            this.txtDelayArrive.Location = new System.Drawing.Point(60, 185);
            this.txtDelayArrive.Name = "txtDelayArrive";
            this.txtDelayArrive.Size = new System.Drawing.Size(80, 21);
            this.txtDelayArrive.TabIndex = 11;
            this.txtDelayArrive.Text = "00:00:00";
            //
            // boxInput1 - 真空负压表输入状态框（左, NPN, X地址）
            // 【V1.09 更新】显示2行: 第1行功能名"负压表", 第2行物理地址"X000"
            // 实际文本在运行时由 BarometerPanelView.cs 根据设备编号设置
            //
            this.boxInput1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.boxInput1.Location = new System.Drawing.Point(6, 28);
            this.boxInput1.Name = "boxInput1";
            this.boxInput1.Size = new System.Drawing.Size(64, 40);
            this.boxInput1.AutoSize = false;
            this.boxInput1.TabIndex = 13;
            this.boxInput1.Text = "负压表\r\nX000";
            this.boxInput1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.boxInput1.BackColor = System.Drawing.Color.LightGray;
            //
            // boxOutput1 - 真空电磁阀输出状态框（中, PNP, Y地址）
            // 【V1.09 更新】显示2行: 第1行功能名"真空阀", 第2行物理地址"Y000"
            //
            this.boxOutput1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.boxOutput1.Location = new System.Drawing.Point(72, 28);
            this.boxOutput1.Name = "boxOutput1";
            this.boxOutput1.Size = new System.Drawing.Size(64, 40);
            this.boxOutput1.AutoSize = false;
            this.boxOutput1.TabIndex = 15;
            this.boxOutput1.Text = "真空阀\r\nY000";
            this.boxOutput1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.boxOutput1.BackColor = System.Drawing.Color.LightGray;
            //
            // boxOutput2 - 载台上电输出状态框（右, PNP, Y地址）
            // 【V1.09 更新】显示2行: 第1行功能名"载台电", 第2行物理地址"Y110"
            //
            this.boxOutput2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.boxOutput2.Location = new System.Drawing.Point(138, 28);
            this.boxOutput2.Name = "boxOutput2";
            this.boxOutput2.Size = new System.Drawing.Size(64, 40);
            this.boxOutput2.AutoSize = false;
            this.boxOutput2.TabIndex = 16;
            this.boxOutput2.Text = "载台电\r\nY110";
            this.boxOutput2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.boxOutput2.BackColor = System.Drawing.Color.LightGray;
            //
            // lblStatus - 设备状态标签（右上角）
            //
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(160, 3);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(29, 12);
            this.lblStatus.TabIndex = 19;
            this.lblStatus.Text = "空闲";
            //
            // BarometerPanelView - 面板自身属性设置
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.boxOutput2);
            this.Controls.Add(this.boxOutput1);
            this.Controls.Add(this.boxInput1);
            this.Controls.Add(this.txtDelayArrive);
            this.Controls.Add(this.lblDelayArrive);
            this.Controls.Add(this.btnSet);
            this.Controls.Add(this.txtDelayStart);
            this.Controls.Add(this.lblDelayStart);
            this.Controls.Add(this.txtRecipe);
            this.Controls.Add(this.lblRecipe);
            this.Controls.Add(this.txtSN);
            this.Controls.Add(this.lblSN);
            this.Controls.Add(this.txtPressure);
            this.Controls.Add(this.lblPressure);
            this.Controls.Add(this.lblDeviceId);
            this.Name = "BarometerPanelView";
            this.Size = new System.Drawing.Size(210, 215);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        // 控件字段声明区域
        // 这些字段在两个 partial 文件中共享（本文件赋值，.cs文件使用）

        /// <summary>设备编号标签（左上角）</summary>
        private System.Windows.Forms.Label lblDeviceId;
        /// <summary>"真空压力"标签</summary>
        private System.Windows.Forms.Label lblPressure;
        /// <summary>真空压力值显示框（只读）</summary>
        private System.Windows.Forms.TextBox txtPressure;
        /// <summary>"SN:"标签</summary>
        private System.Windows.Forms.Label lblSN;
        /// <summary>序列号显示框（只读）</summary>
        private System.Windows.Forms.TextBox txtSN;
        /// <summary>"配方"标签</summary>
        private System.Windows.Forms.Label lblRecipe;
        /// <summary>配方名称显示框（只读）</summary>
        private System.Windows.Forms.TextBox txtRecipe;
        /// <summary>"延时开启"标签</summary>
        private System.Windows.Forms.Label lblDelayStart;
        /// <summary>延时开启时间输入框</summary>
        private System.Windows.Forms.TextBox txtDelayStart;
        /// <summary>延时设置按钮（合并开启和到达）</summary>
        private System.Windows.Forms.Button btnSet;
        /// <summary>"延时到达"标签</summary>
        private System.Windows.Forms.Label lblDelayArrive;
        /// <summary>延时到达时间输入框</summary>
        private System.Windows.Forms.TextBox txtDelayArrive;
        /// <summary>真空负压表输入状态框（NPN, X地址）</summary>
        private System.Windows.Forms.Label boxInput1;
        /// <summary>真空电磁阀输出状态框（PNP, Y地址）</summary>
        private System.Windows.Forms.Label boxOutput1;
        /// <summary>载台上电输出状态框（PNP, Y地址）</summary>
        private System.Windows.Forms.Label boxOutput2;
        /// <summary>设备状态标签（右上角）</summary>
        private System.Windows.Forms.Label lblStatus;
    }
}
