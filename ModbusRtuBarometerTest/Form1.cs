using NModbus;
using NModbus.Device;
using NModbus.Serial;
using Sunny.UI;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using System.Windows.Forms;

namespace ModbusRtuBarometerTest
{
    public partial class Form1 : Form
    {
        // 串口相关（类级，复用）
        private SerialPort _port;
        private IModbusMaster _master;
        private bool _isPortOpen = false;

        // 从站数量（用于批量设置）
        private static int sum = 72; // 1~72

        // 默认小数位数（当从设备读取的小数位数无效时使用）
        private const int DefaultDecimalPlaces = 1;

        public Form1()
        {
            InitializeComponent();
            // 窗体加载时打开串口
            this.Load += Form1_Load;
            // 窗体关闭时释放资源
            this.FormClosing += Form1_FormClosing;
        }

        /// <summary>
        /// 窗体加载：打开串口并创建 Modbus 主站
        /// </summary>
        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                string comPort = SerialPortHelper.GetCh340PortName();
                if (string.IsNullOrEmpty(comPort))
                {
                    this.ShowWarningDialog("未找到 CH340 串口，请检查设备连接！");
                    return;
                }

                _port = new SerialPort(comPort);
                _port.BaudRate = 19200;
                _port.DataBits = 8;
                _port.Parity = Parity.None;
                _port.StopBits = StopBits.One;
                _port.ReadTimeout = 3000;   // 读超时 3 秒
                _port.WriteTimeout = 3000;  // 写超时 3 秒
                _port.Open();

                var factory = new ModbusFactory();
                _master = factory.CreateRtuMaster(_port);
                _isPortOpen = true;

                MessageBox.Show($"✅ 串口 {comPort} 已打开，Modbus 主站已就绪。");
            }
            catch (Exception ex)
            {
                this.ShowWarningDialog($"串口打开失败：{ex.Message}");
                _isPortOpen = false;
            }
        }

        /// <summary>
        /// 窗体关闭：释放串口资源
        /// </summary>
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                _master?.Dispose();
                if (_port != null && _port.IsOpen)
                    _port.Close();
                _port?.Dispose();
                MessageBox.Show("🔌 串口资源已释放。");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"释放资源时发生异常：{ex.Message}");
            }
        }

        // ================= 读取压力 =================

        /// <summary>
        /// 读取当前压力值（带重试机制）
        /// 修正：将无符号寄存器值正确转换为有符号值，并智能确定小数位数
        /// </summary>
        /// <param name="slaveId">从站地址，默认 1</param>
        /// <param name="decimalPos">输出参数：实际使用的小数位数（用于 UI 格式化）</param>
        /// <returns>压力值（浮点数），失败返回 0</returns>
        private float ReadPressure(byte slaveId, out int decimalPos)
        {
            decimalPos = DefaultDecimalPlaces; // 先赋默认值，防止未赋值

            if (!_isPortOpen || _master == null)
            {
                this.ShowWarningDialog("串口未打开，请检查连接！");
                return 0;
            }

            // 最多重试 2 次
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    // 读取输入寄存器 0x0001（压力原始值）和 0x0002（小数点位数，可能无效）
                    ushort[] data = _master.ReadInputRegisters(slaveId, 0x0001, 2);

                    // ---- 关键修正：将 ushort 转为有符号 short ----
                    short rawValueSigned = (short)data[0];   // 例如 0xFFFE → -2
                    int rawValue = rawValueSigned;           // 转为 int 便于计算

                    // ---- 确定小数位数 ----
                    // 检查第二个寄存器是否在合法范围（0~4），若是则使用，否则使用默认值
                    if (data[1] >= 0 && data[1] <= 4)
                        decimalPos = data[1];
                    else
                        decimalPos = DefaultDecimalPlaces;   // 根据示例设为 1

                    // 计算实际压力值：原始值 / 10^decimalPos
                    float pressure = rawValue / (float)Math.Pow(10, decimalPos);
                    return pressure;
                }
                catch (Exception ex) when (attempt == 0)
                {
                    // 第一次失败：记录日志，短暂延时后重试
                    MessageBox.Show($"⚠️ 读取压力失败 (尝试 {attempt + 1})：{ex.Message}");
                    Thread.Sleep(150);
                }
                catch (Exception ex) // 第二次失败则抛出
                {
                    MessageBox.Show($"❌ 读取压力最终失败：{ex.Message}");
                    throw; // 可改为返回 0 或抛出，这里抛出让上层捕获
                }
            }
            return 0; // 理论上不会执行到这里
        }

        // ================= 批量设置阈值 =================

        /// <summary>
        /// 批量写入多个从站的阈值寄存器 (0x0010)
        /// </summary>
        /// <param name="slaveIds">从站地址数组</param>
        /// <param name="thresholdValues">对应的阈值寄存器值数组</param>
        /// <param name="writeDelayMs">每次写入后延时（毫秒）</param>
        /// <returns>写入结果字典</returns>
        private Dictionary<byte, bool> BatchSetThreshold(byte[] slaveIds, ushort[] thresholdValues, int writeDelayMs = 50)
        {
            if (!_isPortOpen || _master == null)
                throw new InvalidOperationException("串口未打开，无法写入！");

            if (slaveIds == null || thresholdValues == null)
                throw new ArgumentNullException("slaveIds 和 thresholdValues 不能为 null");
            if (slaveIds.Length != thresholdValues.Length)
                throw new ArgumentException("两个数组长度必须一致");
            if (slaveIds.Length == 0)
                throw new ArgumentException("至少需要一个从站");

            var result = new Dictionary<byte, bool>();

            for (int i = 0; i < slaveIds.Length; i++)
            {
                byte slaveId = slaveIds[i];
                ushort value = thresholdValues[i];

                try
                {
                    _master.WriteSingleRegister(slaveId, 0x0010, value);
                    result[slaveId] = true;
                    //MessageBox.Show($"✅ 从站 {slaveId} 阈值写入成功，值 = {value}");
                }
                catch (Exception ex)
                {
                    result[slaveId] = false;
                    MessageBox.Show($"❌ 从站 {slaveId} 写入失败：{ex.Message}");
                }

                if (writeDelayMs > 0)
                    Thread.Sleep(writeDelayMs);
            }

            return result;
        }

        /// <summary>
        /// 将浮点数阈值转换为寄存器整数值（根据指定的小数位数）
        /// </summary>
        /// <param name="thresholdFloat">浮点数阈值</param>
        /// <param name="decimalPos">小数位数（应与压力值的小数位数一致）</param>
        /// <returns>ushort 寄存器值</returns>
        private ushort GetFloatToRegisterValue(float thresholdFloat, int decimalPos)
        {
            if (decimalPos < 0 || decimalPos > 4)
                throw new ArgumentException("小数点位数应在 0~4 之间");
            int multiplier = (int)Math.Pow(10, decimalPos);
            int intValue = (int)Math.Round(thresholdFloat * multiplier);
            return (ushort)intValue;
        }

        // ================= UI 事件响应 =================

        /// <summary>
        /// 点击“读取压力”按钮
        /// </summary>
        private void btnReadPressure_Click(object sender, EventArgs e)
        {
            try
            {
                // 读取压力，同时获取实际使用的小数位数
                float pressure = ReadPressure(slaveId: 1, out int decimalPos);
                // 动态格式化，显示与精度匹配的小数位数
                llPressure.Text = pressure.ToString($"F{decimalPos}");
            }
            catch (Exception ex)
            {
                this.ShowWarningDialog($"读取压力失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 点击“批量设置阈值”按钮
        /// 注意：阈值的小数位数应与压力值的小数位数保持一致，这里固定为 1（可根据实际调整）
        /// </summary>
        private void btnBatchSetThreashold_Click(object sender, EventArgs e)
        {
            // 1. 获取用户输入
            if (string.IsNullOrWhiteSpace(tbThreshold.Text))
            {
                this.ShowWarningDialog("请输入阈值！");
                return;
            }
            if (!float.TryParse(tbThreshold.Text, out float thresholdValue))
            {
                this.ShowWarningDialog("请输入有效的数字！");
                return;
            }

            // 2. 准备从站地址 1~72
            byte[] slaveIds = new byte[sum];
            for (int i = 0; i < sum; i++)
                slaveIds[i] = (byte)(i + 1);

            // 3. 将阈值转换为寄存器值（小数位数固定为 1，与默认读取一致）
            //    如果您的设备小数位数不同，请修改此处或从设备读取
            const int decimalPosForThreshold = 1;
            ushort registerValue = GetFloatToRegisterValue(thresholdValue, decimalPosForThreshold);
            ushort[] thresholdValues = new ushort[sum];
            for (int i = 0; i < sum; i++)
                thresholdValues[i] = registerValue;

            // 4. 批量写入
            try
            {
                var result = BatchSetThreshold(slaveIds, thresholdValues, writeDelayMs: 50);
                int successCount = 0;
                foreach (var kv in result)
                    if (kv.Value) successCount++;

                this.ShowSuccessDialog($"批量设置完成！成功: {successCount} 个，失败: {result.Count - successCount} 个");
            }
            catch (Exception ex)
            {
                this.ShowWarningDialog($"批量设置失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 点击“单个设置阈值”按钮（仅对从站 1 设置）
        /// </summary>
        private void btnSetThreshold_Click(object sender, EventArgs e)
        {
            // 1. 获取用户输入
            if (string.IsNullOrWhiteSpace(tbThreshold.Text))
            {
                this.ShowWarningDialog("请输入阈值！");
                return;
            }
            if (!float.TryParse(tbThreshold.Text, out float thresholdValue))
            {
                this.ShowWarningDialog("请输入有效的数字！");
                return;
            }

            // 2. 检查串口是否已打开
            if (!_isPortOpen || _master == null)
            {
                this.ShowWarningDialog("串口未打开，请检查连接！");
                return;
            }

            // 3. 将阈值转换为寄存器值（小数位数固定为 1，与默认读取一致）
            const int decimalPosForThreshold = 1;
            ushort registerValue = GetFloatToRegisterValue(thresholdValue, decimalPosForThreshold);

            // 4. 对从站 1 执行写入
            try
            {
                _master.WriteSingleRegister(1, 0x0010, value: registerValue);
                this.ShowSuccessDialog($"✅ 从站 1 阈值写入成功！设定值 = {thresholdValue}，寄存器值 = {registerValue}");
            }
            catch (Exception ex)
            {
                this.ShowWarningDialog($"❌ 从站 1 阈值写入失败：{ex.Message}");
            }
        }
    }
}