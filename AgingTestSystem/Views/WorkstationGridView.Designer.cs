namespace AgingTestSystem.Views
{
    /// <summary>
    /// 工位网格（自绘大画布）—— 设计器自动生成部分
    /// 本控件完全自绘（OnPaint，见 WorkstationGridView.cs），不含任何子控件，
    /// 仅保留组件容器（托管 ToolTip/长按计时器）与默认尺寸。
    /// </summary>
    partial class WorkstationGridView
    {
        /// <summary>
        /// 必需的设计器变量
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清理所有正在使用的资源
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
                if (_titleFont != null) _titleFont.Dispose();
                if (_panelFont != null) _panelFont.Dispose();
            }
            base.Dispose(disposing);
        }

        #region 组件设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要使用代码编辑器修改此方法的内容
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Name = "WorkstationGridView";
            this.Size = new System.Drawing.Size(2040, 2025);
        }

        #endregion
    }
}
