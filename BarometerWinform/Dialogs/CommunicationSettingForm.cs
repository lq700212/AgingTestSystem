using System;
using System.Windows.Forms;
using BarometerWinform.Models;

namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// PLC通讯设置窗体（业务逻辑部分）
    ///
    /// 【功能说明】
    /// 用于配置与PLC的通讯参数，包括：
    /// - IP地址与端口号（以太网通讯）
    /// - 串口参数（串口通讯，如Modbus RTU）
    /// - 通讯协议选择
    ///
    /// 【预留说明】
    /// 1. 实际协议参数需根据现场PLC型号确认后补充
    /// 2. "测试连接"按钮功能预留，待真实PLC接入后实现
    /// 3. 当前保存的配置仅在内存中生效，未持久化到文件
    ///    持久化功能预留（可考虑保存到App.config或独立配置文件）
    /// </summary>
    public partial class CommunicationSettingForm : Form
    {
        /// <summary>
        /// 设备配置对象（用于读取/保存通讯参数）
        /// </summary>
        private readonly DeviceConfig _config;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="config">设备配置对象，传入后修改将反映到该对象</param>
        public CommunicationSettingForm(DeviceConfig config)
        {
            InitializeComponent();
            _config = config;

            // 加载当前配置到界面控件
            LoadConfigToUI();
        }

        /// <summary>
        /// 将配置对象的值加载到界面控件
        /// 修复 H6：NumericUpDown 赋值前做范围校验
        /// </summary>
        private void LoadConfigToUI()
        {
            txtPlcIp.Text = _config.PlcAddress;
            // 修复 H6：用 Clamp 限制在控件范围内
            numPlcPort.Value = ClampToNumericRange(_config.PlcPort, numPlcPort);
            txtPortName.Text = _config.PortName;
            numBaudRate.Value = ClampToNumericRange(_config.BaudRate, numBaudRate);

            // 默认选择第一个协议项
            if (cboProtocol.Items.Count > 0)
            {
                cboProtocol.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// 将整数值限制在 NumericUpDown 的 Minimum/Maximum 范围内
        /// 修复 H6：避免配置值超出控件范围时抛 ArgumentOutOfRangeException
        /// </summary>
        /// <param name="value">原始值</param>
        /// <param name="control">目标 NumericUpDown 控件</param>
        /// <returns>限制后的值</returns>
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
        /// 将界面值写回配置对象
        /// </summary>
        private void btnOK_Click(object sender, EventArgs e)
        {
            // 简单的输入校验：IP不能为空
            if (string.IsNullOrWhiteSpace(txtPlcIp.Text))
            {
                MessageBox.Show("请输入PLC的IP地址", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 将界面值写回配置对象
            _config.PlcAddress = txtPlcIp.Text.Trim();
            _config.PlcPort = (int)numPlcPort.Value;
            _config.PortName = txtPortName.Text.Trim();
            _config.BaudRate = (int)numBaudRate.Value;

            // 【预留】持久化保存到配置文件
            // TODO: 实现将配置保存到 App.config 或独立JSON/XML文件
            // 当前仅在内存中生效，程序重启后失效

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        /// <summary>
        /// 取消按钮点击事件
        /// 不保存任何修改直接关闭
        /// </summary>
        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        /// <summary>
        /// 测试连接按钮点击事件（预留功能）
        ///
        /// 【预留说明】
        /// 待真实PLC接入后，应实现：
        /// 1. 根据当前配置尝试连接PLC
        /// 2. 显示连接结果（成功/失败及原因）
        /// 3. 连接成功后自动断开
        /// </summary>
        private void btnTestConnect_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
                "测试连接功能预留。\n\n" +
                "待真实PLC接入后实现：\n" +
                "1. 根据当前配置尝试连接PLC\n" +
                "2. 显示连接结果\n" +
                "3. 自动断开连接",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
