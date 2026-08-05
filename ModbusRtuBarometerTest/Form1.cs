using NModbus;
using NModbus.Device;
using NModbus.Serial;
using Sunny.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ModbusRtuBarometerTest
{

    public partial class Form1 : Form
    {
        private static string comPort = "COM5"; // 串口号，根据实际情况修改
        private static int sum = 72; // 从站数量，1~72

        public Form1()
        {
            InitializeComponent();
            //InitData();
        }

        private void InitData()
        {
            // 1. 配置并打开串口
            //    ⚠️ 重要：将 "COM3" 改为你在设备管理器中看到的实际 COM 口号
            SerialPort port = new SerialPort(comPort);
            port.BaudRate = 19200;   // 压力表默认波特率
            port.DataBits = 8;
            port.Parity = Parity.None;
            port.StopBits = StopBits.One;
            port.Open();

            Console.WriteLine("✅ 串口已打开，开始读取数据...");

            try
            {
                // 2. 使用 ModbusFactory 创建 RTU 主站 (这是 3.0.83 版本的推荐方式)
                var factory = new ModbusFactory();
                IModbusMaster master = factory.CreateRtuMaster(port);

                // 3. 读取保持寄存器
                //    参数: 从机地址=1, 起始地址=0x0002, 读取数量=1
                ushort[] registers = master.ReadHoldingRegisters(1, 0x0010, 1);

                // 4. 输出结果
                llPressure.Text = registers[0].ToString();
                Console.WriteLine($"✅ 读取成功！寄存器 0x0002 的值 = {registers[0]}");
                // 提示：这个原始值可能需要除以 1000 才是以 MPa 为单位的实际压力值
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 读取失败：{ex.Message}");
            }
            finally
            {
                // 5. 关闭串口，释放资源
                port.Close();
                Console.WriteLine("🔌 串口已关闭。按任意键退出...");
            }
        }

        private void btnReadPressure_Click(object sender, EventArgs e)
        {
            float pressure = ReadPressure(comPort, slaveId: 1);
            llPressure.Text = pressure.ToString("F3"); // 显示三位小数
        }

        /// <summary>
        /// 读取气压表的当前显示压力值（浮点数）
        /// </summary>
        /// <param name="comPort">串口号，如 "COM5"</param>
        /// <param name="slaveId">从站地址，默认为 1</param>
        /// <param name="baudRate">波特率，默认 19200</param>
        /// <returns>当前压力值（单位由仪表决定，如 MPa 或 kPa）</returns>
        private float ReadPressure(string comPort, byte slaveId = 1, int baudRate = 19200)
        {
            using (var port = new SerialPort(comPort))
            {
                try
                {
                    // 配置串口参数（必须与气压表设置一致）
                    port.BaudRate = baudRate;
                    port.DataBits = 8;
                    port.Parity = Parity.None;
                    port.StopBits = StopBits.One;
                    port.Open();
                
                    // 创建 Modbus RTU 主站
                    var factory = new ModbusFactory();
                    var master = factory.CreateRtuMaster(port);

                    // 同时读取 0001H 和 0002H 两个寄存器
                    ushort startAddress = 0x0001; // 起始地址
                    ushort numRegisters = 2;      // 读取 2 个寄存器
                    ushort[] data = master.ReadInputRegisters(slaveId, startAddress, numRegisters);

                    // 数据解析
                    int rawValue = data[0];    // 0001H: 原始压力值（整数）
                    int decimalPos = data[1];  // 0002H: 小数点位数（例如 2 表示除以 100）

                    // 计算实际压力值
                    float pressure = rawValue / (float)Math.Pow(10, decimalPos);

                    return pressure;
                }
                catch (Exception ex)
                {
                    return 0;
                }
            }
            // using 保证串口自动关闭
        }

        /// <summary>
        /// 批量修改多个气压表的阈值（寄存器地址 0x0010H）
        /// </summary>
        /// <param name="comPort">串口号，如 "COM5"</param>
        /// <param name="slaveIds">从站地址数组，长度应与 thresholdValues 一致，通常为 1~72</param>
        /// <param name="thresholdValues">要写入的阈值数组，每个值对应一个从站（单位与气压表一致，需预先转换为寄存器整数值）</param>
        /// <param name="baudRate">波特率，默认 19200</param>
        /// <param name="writeDelayMs">每次写入后延时（毫秒），防止从机处理不过来，默认 50ms</param>
        /// <returns>返回一个字典，键为从站地址，值为是否写入成功</returns>
        private Dictionary<byte, bool> batchSetThreshold(
            string comPort,
            byte[] slaveIds,
            ushort[] thresholdValues,
            int baudRate = 19200,
            int writeDelayMs = 50)
        {
            // 参数校验
            if (slaveIds == null || thresholdValues == null)
                throw new ArgumentNullException("slaveIds 和 thresholdValues 不能为 null");
            if (slaveIds.Length != thresholdValues.Length)
                throw new ArgumentException("slaveIds 和 thresholdValues 长度必须一致");
            if (slaveIds.Length == 0)
                throw new ArgumentException("至少需要一个从站");

            // 结果字典，记录每个从站是否写入成功
            var result = new Dictionary<byte, bool>();

            using (var port = new SerialPort(comPort))
            {
                // 配置串口参数（必须与气压表设置一致）
                port.BaudRate = baudRate;
                port.DataBits = 8;
                port.Parity = Parity.None;
                port.StopBits = StopBits.One;
                port.Open();

                // 创建 Modbus RTU 主站
                var factory = new ModbusFactory();
                var master = factory.CreateRtuMaster(port);

                // 循环写入每个从站的阈值
                for (int i = 0; i < slaveIds.Length; i++)
                {
                    byte slaveId = slaveIds[i];
                    ushort value = thresholdValues[i];

                    try
                    {
                        // 使用 06H 功能码写入单个保持寄存器
                        // 参数：从站地址，寄存器地址（0x0010），要写入的值
                        master.WriteSingleRegister(slaveId, 0x0010, value);

                        // 记录成功
                        result[slaveId] = true;
                        Console.WriteLine($"✅ 从站 {slaveId} 阈值写入成功，值 = {value}");
                    }
                    catch (Exception ex)
                    {
                        // 记录失败
                        result[slaveId] = false;
                        Console.WriteLine($"❌ 从站 {slaveId} 写入失败：{ex.Message}");
                    }

                    // 延时，避免从机处理不过来或总线冲突
                    if (writeDelayMs > 0)
                        Thread.Sleep(writeDelayMs);
                }
            } // using 结束，串口自动关闭

            return result;
        }

        // ================= 辅助方法：将浮点数阈值转换为寄存器值 =================

        /// <summary>
        /// 将浮点数阈值转换为寄存器整数值（根据气压表的小数点位数）
        /// </summary>
        /// <param name="thresholdFloat">浮点数阈值（如 1.234）</param>
        /// <param name="decimalPos">小数点位数（如 3）</param>
        /// <returns>寄存器整数值（如 1234）</returns>
        private ushort getFloatToRegisterValue(float thresholdFloat, int decimalPos)
        {
            if (decimalPos < 0 || decimalPos > 4) // 根据实际表支持的小数位数调整
                throw new ArgumentException("小数点位数应在 0~4 之间");

            int multiplier = (int)Math.Pow(10, decimalPos);
            int intValue = (int)Math.Round(thresholdFloat * multiplier); // 四舍五入
            return (ushort)intValue;
        }

        private void btnBatchSetThreashold_Click(object sender, EventArgs e)
        {
            // 1. 获取用户输入并验证
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
            // 1. 准备从站地址数组 1~72
            byte[] slaveIds = new byte[sum];
            for (byte i = 0; i < sum; i++)
                slaveIds[i] = (byte)(i + 1);

            // 2. 准备每个从站的阈值（这里统一为 1.234）
            ushort[] thresholdValues = new ushort[sum];
            ushort registerValue = getFloatToRegisterValue(thresholdValue, decimalPos: 3);
            for (int i = 0; i < sum; i++)
                thresholdValues[i] = registerValue;

            // 3. 批量写入
            var result = batchSetThreshold(comPort, slaveIds, thresholdValues,
                                                   baudRate: 19200, writeDelayMs: 50);

            // 4. 统计结果
            int successCount = 0;
            foreach (var kv in result)
                if (kv.Value) successCount++;

            this.ShowSuccessDialog($"批量设置完成！成功: {successCount} 个，失败: {result.Count - successCount} 个");
        }
    }
}
