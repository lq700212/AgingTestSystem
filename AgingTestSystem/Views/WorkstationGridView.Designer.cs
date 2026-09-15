namespace AgingTestSystem.Views
{
    /// <summary>
    /// 工位网格（自绘大画布）—— 设计器自动生成部分
    /// 本控件完全自绘（OnPaint，见 WorkstationGridView.cs），不含任何子控件，
    /// 仅保留组件容器（托管 ToolTip）与默认尺寸。
    /// 拖拽滚动合并计时器随滚动整套删除。
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
                if (_rowSelectFont != null) _rowSelectFont.Dispose();
                if (_setButtonFont != null) _setButtonFont.Dispose();
                if (_timeValueFont != null) _timeValueFont.Dispose();
                // 【大扫荡】缓存画刷/画笔销毁时释放：以前只在 RebuildThemeBrushes 覆盖时放，
                // 控件销毁时 6 个全漏（GDI 句柄泄漏，长稳运行+切主题放大）。
                if (_brushSetButton != null) _brushSetButton.Dispose();
                if (_brushSelectChecked != null) _brushSelectChecked.Dispose();
                if (_penBorder != null) _penBorder.Dispose();
                if (_brushValueBox != null) _brushValueBox.Dispose();
                if (_brushRowSelect != null) _brushRowSelect.Dispose();
                if (_brushSelectUnchecked != null) _brushSelectUnchecked.Dispose();
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
            this.Size = new System.Drawing.Size(1720, 1638);
        }

        #endregion
    }
}
