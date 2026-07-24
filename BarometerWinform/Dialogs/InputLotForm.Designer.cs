namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 录入批号窗体 —— 设计器自动生成部分
    /// 
    /// 【界面布局说明】
    /// 窗口样式参考用户提供的图片，包含：
    /// 1. 标题栏：录入批号窗口
    /// 2. 批号标签 + 文本输入框
    /// 3. 红色背景注释标签：手动输入批号
    /// 4. 确定按钮和取消按钮（水平排列）
    /// </summary>
    partial class InputLotForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        private void InitializeComponent()
        {
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblLot = new System.Windows.Forms.Label();
            this.txtLot = new System.Windows.Forms.TextBox();
            this.lblComment = new System.Windows.Forms.Label();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // lblTitle - 窗口标题标签
            //
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("微软雅黑", 10F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Location = new System.Drawing.Point(15, 15);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Text = "录入批号窗口";
            //
            // lblLot - 批号标签
            //
            this.lblLot.AutoSize = true;
            this.lblLot.Location = new System.Drawing.Point(40, 60);
            this.lblLot.Name = "lblLot";
            this.lblLot.Size = new System.Drawing.Size(41, 12);
            this.lblLot.Text = "批号：";
            //
            // txtLot - 批号输入文本框
            //
            this.txtLot.Location = new System.Drawing.Point(90, 57);
            this.txtLot.Name = "txtLot";
            this.txtLot.Size = new System.Drawing.Size(280, 21);
            this.txtLot.KeyDown += new System.Windows.Forms.KeyEventHandler(this.txtLot_KeyDown);
            //
            // lblComment - 注释提示标签（红色背景）
            //
            this.lblComment.AutoSize = true;
            this.lblComment.BackColor = System.Drawing.Color.Red;
            this.lblComment.ForeColor = System.Drawing.Color.White;
            this.lblComment.Location = new System.Drawing.Point(120, 95);
            this.lblComment.Name = "lblComment";
            this.lblComment.Size = new System.Drawing.Size(140, 12);
            this.lblComment.Text = "注释: 手动输入批号";
            //
            // btnOK - 确定按钮
            //
            this.btnOK.Location = new System.Drawing.Point(80, 145);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(100, 30);
            this.btnOK.Text = "确定";
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            //
            // btnCancel - 取消按钮
            //
            this.btnCancel.Location = new System.Drawing.Point(200, 145);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(100, 30);
            this.btnCancel.Text = "取消";
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            //
            // InputLotForm - 窗体自身属性设置
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(385, 195);
            this.Controls.Add(this.lblTitle);
            this.Controls.Add(this.lblLot);
            this.Controls.Add(this.txtLot);
            this.Controls.Add(this.lblComment);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "InputLotForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "录入批号窗口";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblLot;
        private System.Windows.Forms.TextBox txtLot;
        private System.Windows.Forms.Label lblComment;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
    }
}