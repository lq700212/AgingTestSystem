using System;
using System.Windows.Forms;
using BarometerWinform.Services;

namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 录入批号窗体（业务逻辑部分）
    ///
    /// 【功能说明】
    /// 用于手动录入产品批号，点击主界面的"录入批号"按钮后弹出此窗口。
    /// 批号录入后可用于标识当前生产批次，便于后续追溯和数据分析。
    ///
    /// 【界面布局】
    /// 参考用户提供的图片设计：
    /// ┌─────────────────────────────────────┐
    /// │ 录入批号窗口                        │ ← 标题栏
    /// ├─────────────────────────────────────┤
    /// │  批号：[________________________]   │ ← 批号标签 + 输入框
    /// │                                     │
    /// │    [确定]           [取消]          │ ← 确定和取消按钮
    /// └─────────────────────────────────────┘
    ///
    /// 【工作流程】
    /// 1. 用户点击主界面"录入批号"按钮，弹出此窗口
    /// 2. 用户在文本框中输入批号
    /// 3. 点击"确定"按钮或按回车键确认录入
    /// 4. 触发批号录入完成事件，主窗体可订阅此事件获取批号
    /// 5. 点击"取消"按钮关闭窗口，不做任何操作
    ///
    /// 【输入校验规则】
    /// - 批号不能为空
    /// - 批号长度限制：最大100个字符（可根据实际需求调整）
    /// - 允许包含：数字、字母、下划线、中划线等常见批号字符
    ///
    /// 【预留说明】
    /// 1. 当前批号仅通过事件传递给主窗体，未持久化存储
    /// 2. 后续可扩展：将批号写入数据库、关联生产记录等
    /// 3. 可增加批号格式校验（如日期+流水号格式）
    /// </summary>
    public partial class InputLotForm : Form
    {
        /// <summary>
        /// 批号录入完成事件
        /// 当用户点击"确定"按钮并校验通过后触发
        /// 主窗体可订阅此事件获取录入的批号
        /// </summary>
        public event EventHandler<string> OnLotInputCompleted;

        /// <summary>
        /// 【V1.16 新增】扫码枪服务引用
        /// 由主窗体传入，继续传递给 ID绑定窗体（IdBindingForm），
        /// 使 ID 绑定界面打开时扫码结果能自动填充 SN 输入框。
        /// 可能为 null（未启用扫码枪），使用前需要判空。
        /// </summary>
        private readonly ScannerService _scanner;

        /// <summary>
        /// 构造函数
        /// 初始化窗体界面
        /// </summary>
        /// <param name="scanner">扫码枪服务（由主窗体传入，可能为 null）</param>
        public InputLotForm(ScannerService scanner = null)
        {
            InitializeComponent();
            _scanner = scanner;
        }

        /// <summary>
        /// 获取用户输入的批号
        /// </summary>
        /// <returns>批号字符串（已去除首尾空格）</returns>
        public string GetLotNumber()
        {
            return txtLot.Text.Trim();
        }

        /// <summary>
        /// 确定按钮点击事件
        /// 校验输入并触发批号录入完成事件
        /// </summary>
        private void btnOK_Click(object sender, EventArgs e)
        {
            DoConfirm();
        }

        /// <summary>
        /// 取消按钮点击事件
        /// 关闭窗口，不做任何操作
        /// </summary>
        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        /// <summary>
        /// 文本框按键事件
        /// 支持按回车键直接确认录入（提高操作效率）
        /// </summary>
        private void txtLot_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                DoConfirm();
                e.SuppressKeyPress = true;
            }
        }

        /// <summary>
        /// 执行确认逻辑（核心方法）
        /// 抽取为独立方法，避免重复代码
        /// 
        /// 【业务流程】
        /// 1. 验证批号输入
        /// 2. 验证通过后弹出 ID绑定界面（IdBindingForm）
        /// 3. ID绑定完成后触发批号录入完成事件
        /// 4. 关闭录入批号窗口
        /// </summary>
        private void DoConfirm()
        {
            string lotNumber = txtLot.Text.Trim();

            // 验证批号不能为空
            if (string.IsNullOrWhiteSpace(lotNumber))
            {
                MessageBox.Show("请输入批号", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtLot.Focus();
                return;
            }

            // 验证批号长度
            if (lotNumber.Length > 100)
            {
                MessageBox.Show("批号长度不能超过100个字符", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtLot.Focus();
                return;
            }

            // 批号验证通过，弹出ID绑定界面
            // 【V1.16】把扫码枪服务传进去：ID绑定窗体打开时，扫码结果自动填充 SN 输入框
            using (var bindingForm = new IdBindingForm(lotNumber, _scanner))
            {
                // 订阅ID绑定完成事件
                bindingForm.OnBindingCompleted += (sender2, data) =>
                {
                    string boundLot = data.Item1;
                    var bindings = data.Item2;

                    // 写入调试日志
                    System.Diagnostics.Debug.WriteLine(
                        $"[录入批号] ID绑定完成: 批号={boundLot}, 绑定产品数量={bindings.Count}");
                };

                // 显示ID绑定对话框
                DialogResult bindingResult = bindingForm.ShowDialog(this);

                // 如果ID绑定成功完成
                if (bindingResult == DialogResult.OK)
                {
                    // 触发批号录入完成事件，通知主窗体
                    OnLotInputCompleted?.Invoke(this, lotNumber);

                    // 设置对话框结果并关闭窗口
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    // 用户取消了ID绑定，不关闭录入批号窗口
                    System.Diagnostics.Debug.WriteLine(
                        $"[录入批号] 用户取消了ID绑定");
                }
            }
        }
    }
}