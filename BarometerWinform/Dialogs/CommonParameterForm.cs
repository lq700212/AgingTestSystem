using System;
using System.Windows.Forms;
using BarometerWinform.Models;

namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 公共参数设置窗体（业务逻辑部分）
    ///
    /// 【功能说明】
    /// 配置系统全局通用参数，所有气压表共享这些参数：
    /// - 数据采集间隔（影响数据刷新频率）
    /// - 报警阈值（压力异常时触发报警）
    /// - 正常压力范围（用于状态判断）
    /// - 温度上限（保护设备）
    ///
    /// 【预留说明】
    /// 1. 实际参数项需根据现场工艺要求补充
    /// 2. 参数持久化未实现（当前仅内存生效）
    /// 3. 报警触发后的处理逻辑未实现（如发声、停机、记录日志等）
    /// </summary>
    public partial class CommonParameterForm : Form
    {
        /// <summary>
        /// 设备配置对象
        /// </summary>
        private readonly DeviceConfig _config;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="config">设备配置对象</param>
        public CommonParameterForm(DeviceConfig config)
        {
            InitializeComponent();
            _config = config;

            LoadConfigToUI();
        }

        /// <summary>
        /// 加载配置到界面
        /// 修复 H6：NumericUpDown 赋值前做范围校验，避免抛 ArgumentOutOfRangeException
        /// </summary>
        private void LoadConfigToUI()
        {
            // 采集间隔（毫秒）
            // 修复 H6：用 Clamp 限制在 NumericUpDown 的 Minimum/Maximum 范围内
            numCollectInterval.Value = ClampToNumericRange(_config.CollectInterval, numCollectInterval);

            // 其他参数为预留字段，使用默认值显示
            // TODO: 待现场确认实际参数项后补充
        }

        /// <summary>
        /// 将整数值限制在 NumericUpDown 的 Minimum/Maximum 范围内
        /// 修复 H6：避免配置值超出控件范围时抛 ArgumentOutOfRangeException 导致窗体构造失败
        /// </summary>
        /// <param name="value">原始值</param>
        /// <param name="control">目标 NumericUpDown 控件</param>
        /// <returns>限制后的值（在 Minimum 和 Maximum 之间）</returns>
        private decimal ClampToNumericRange(int value, NumericUpDown control)
        {
            decimal min = control.Minimum;
            decimal max = control.Maximum;
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>
        /// 保存按钮点击事件
        /// </summary>
        private void btnOK_Click(object sender, EventArgs e)
        {
            // 采集间隔最小100ms，避免过于频繁导致UI卡顿
            if (numCollectInterval.Value < 100)
            {
                MessageBox.Show("采集间隔不能小于100ms", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _config.CollectInterval = (int)numCollectInterval.Value;

            // 【预留】其他参数保存逻辑
            // TODO: 待现场确认实际参数项后补充

            // 【预留】持久化保存到配置文件
            // TODO: 实现将配置保存到 App.config 或独立配置文件

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
    }
}
