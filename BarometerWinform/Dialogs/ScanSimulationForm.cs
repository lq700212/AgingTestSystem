using System;
using System.Windows.Forms;

namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 扫码模拟窗体（业务逻辑部分）
    ///
    /// 【功能说明】
    /// 模拟扫码枪扫描条码/二维码，用于：
    /// - 开发阶段测试扫码业务逻辑
    /// - 现场调试时模拟扫码输入
    ///
    /// 工作流程：
    /// 1. 在文本框中输入条码内容
    /// 2. 点击"模拟扫码"按钮
    /// 3. 触发扫码完成事件，主窗体可订阅此事件处理扫码结果
    ///
    /// 【V1.16 说明】
    /// 真实扫码枪接入已由 Services/ScannerService.cs 实现（WMI 自动识别串口 + 串口读码），
    /// 本"扫码模拟"窗体保留用于：没有扫码枪时的开发调试、模拟扫码输入。
    /// 真实扫码枪扫码结果写 LOG 日志，并在 ID绑定窗体打开时自动填充 SN 输入框。
    /// </summary>
    public partial class ScanSimulationForm : Form
    {
        /// <summary>
        /// 扫码完成事件
        /// 当用户点击"模拟扫码"按钮时触发
        /// 主窗体可订阅此事件获取扫码内容
        /// </summary>
        public event EventHandler<string> OnScanCompleted;

        /// <summary>
        /// 构造函数
        /// </summary>
        public ScanSimulationForm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 模拟扫码按钮点击事件
        /// 触发扫码完成事件并关闭窗体
        /// </summary>
        private void btnScan_Click(object sender, EventArgs e)
        {
            DoScan();
        }

        /// <summary>
        /// 执行扫码逻辑（修复 M14：抽取核心方法，避免事件参数类型错配）
        /// 原 txtBarcode_KeyDown 直接调用 btnScan_Click(sender, e) 把 KeyEventArgs 误当 EventArgs 传递
        /// 虽然当前能工作（btnScan_Click 内部未使用 e），但是潜在隐患
        /// </summary>
        private void DoScan()
        {
            // 校验输入不能为空
            if (string.IsNullOrWhiteSpace(txtBarcode.Text))
            {
                MessageBox.Show("请输入要模拟的条码内容", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 获取扫码内容
            string barcode = txtBarcode.Text.Trim();

            // 触发扫码完成事件，通知订阅者
            OnScanCompleted?.Invoke(this, barcode);

            // 关闭窗体
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        /// <summary>
        /// 文本框按键事件
        /// 支持按回车键直接模拟扫码（与真实扫码枪行为一致）
        /// </summary>
        private void txtBarcode_KeyDown(object sender, KeyEventArgs e)
        {
            // 按下回车键时触发扫码
            if (e.KeyCode == Keys.Enter)
            {
                // 修复 M14：调用核心方法 DoScan，而非 btnScan_Click
                // 避免 KeyEventArgs 被当作 EventArgs 传递的类型错配问题
                DoScan();
                // 标记事件已处理，避免回车键的声音
                e.SuppressKeyPress = true;
            }
        }
    }
}
