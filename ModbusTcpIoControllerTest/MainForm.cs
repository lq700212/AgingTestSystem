using EasyModbus;
using System;
using System.Threading;
using System.Windows.Forms;

namespace ModbusTcpIoControllerTest
{
    public partial class MainForm : Form
    {
        // 声明一个 ModbusClient 对象，用于与设备通讯
        private ModbusClient modbusClient;

        public MainForm()
        {
            InitializeComponent();
            InitData();
        }

        // 初始化方法：创建 ModbusClient 实例并设置超时时间
        private void InitData()
        {
            // 创建 ModbusClient 对象，指定目标设备的 IP 地址和端口号（Modbus TCP 默认 502）
            // 注意：IP 必须和耦合器在同一网段，这里用文档中的 192.168.1.20
            modbusClient = new ModbusClient("192.168.1.20", 502);
            // 设置连接超时时间（单位毫秒），防止长时间卡死
            modbusClient.ConnectionTimeout = 5000;
        }

        // ========== 1. 连接测试按钮 ==========
        private void btnConnection_Click(object sender, EventArgs e)
        {
            try
            {
                // 尝试连接设备
                modbusClient.Connect();
                // 检查连接状态
                if (modbusClient.Connected)
                {
                    MessageBox.Show("连接成功！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                // 如果连接失败，显示详细错误信息（包括内部异常）
                string msg = $"连接失败: {ex.Message}";
                if (ex.InnerException != null)
                    msg += $"\n内部异常: {ex.InnerException.Message}";
                MessageBox.Show(msg, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ========== 2. 写入DO（数字量输出）测试按钮 ==========
        // 功能：向地址 0x2000 写入一个值，控制第1~16路输出通道的开关状态，相当于 16 个二进制的 bit 位
        private void btnWriteData_Click(object sender, EventArgs e)
        {
            // 先检查是否已连接，未连接则提示
            if (!modbusClient.Connected)
            {
                MessageBox.Show("请先点击“连接测试”建立通讯！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 设置从站地址（设备地址），文档中耦合器的 Modbus 从站地址为 0x01
            modbusClient.UnitIdentifier = 0x01;

            // 定义要写入的寄存器地址（0x2000 对应数字量输出区域）
            int startAddress = 0x2000;

            // 定义要写入的值（16位，对应16个输出通道）
            // 例如：0x0001 表示只打开第1路；0x0002 只打开第2路；0x0003 同时打开第1和第2路
            // 这里我们测试写入 0x0001，即打开第1路输出
            int valueToWrite = 0x0001;

            try
            {
                // 使用 WriteSingleRegister 方法写入单个寄存器（功能码 0x06）
                // 参数：寄存器地址，要写入的值（int 类型，但内部会转为 ushort）
                modbusClient.WriteSingleRegister(startAddress, valueToWrite);

                // 如果写入成功，设备会返回原样回显，这里显示成功信息
                MessageBox.Show($"写入成功！\n地址 0x{startAddress:X4} 已写入值 0x{valueToWrite:X4}",
                                "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                // 写入失败显示错误
                MessageBox.Show($"写入失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ========== 3. 读取DI（数字量输入）测试按钮 ==========
        // 功能：读取地址 0x1000 的输入寄存器，获取第1~16路DI的状态, 一个寄存器对应
        private void btnReadData_Click(object sender, EventArgs e)
        {
            // 先检查连接状态
            if (!modbusClient.Connected)
            {
                MessageBox.Show("请先点击“连接测试”建立通讯！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 设置从站地址
            modbusClient.UnitIdentifier = 0x01;

            // 要读取的起始地址（0x1000 对应数字量输入区域）
            int startAddress = 0x1000;
            // 要读取的寄存器个数（这里读1个，即16个输入点）
            int numberOfRegisters = 1;

            try
            {
                // 使用 ReadInputRegisters 方法读取输入寄存器（功能码 0x04）
                // 注意：不要用 ReadHoldingRegisters（那是读保持寄存器，功能码0x03）
                // 文档中说明 DI 可以用 0x04，所以我们用这个方法更规范
                int[] result = modbusClient.ReadInputRegisters(startAddress, numberOfRegisters);

                // 如果读取成功，result 是一个 int 数组，每个元素对应一个寄存器的值
                // 我们只读了一个寄存器，所以取 result[0]
                int value = result[0];

                // 将数值转换为二进制字符串（方便查看每一位对应的通道状态）
                // 例如：0x0001 的二进制是 0000 0000 0000 0001，表示第1路为ON
                string binaryStr = Convert.ToString(value, 2).PadLeft(16, '0');

                // 组装显示信息
                string message = $"读取成功！\n" +
                                 $"寄存器地址: 0x{startAddress:X4}\n" +
                                 $"读取到的数值: 0x{value:X4} (十进制: {value})\n" +
                                 $"二进制位: {binaryStr}\n" +
                                 $"（从右往左第1位为第1路，第16位为第16路）\n" +
                                 $"第1路状态: {((value & 0x0001) != 0 ? "ON" : "OFF")}";

                MessageBox.Show(message, "读取结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /**
         * ========== 4. 循环写入DO测试按钮 ==========
         * 功能：循环写入不同的值到地址 0x2000，模拟控制第1~16路输出通道的开关状态
         * 每次循环写入一个值，等待1秒，再写入下一个值，直到所有通道都测试完毕
         * 
         * 现场 GX-CL140-S 耦合器有接 5 个 DQ50P-S 输出模块，测试时可以观察到每个模块的指示灯变化
         * 5 个 DQ50P-S 每个有 16 * 2 = 32 个输出通道，00~0F 和 10~1F，耦合器的 DO 寄存器地址从 0x2000 开始，每个寄存器对应 16 个通道
         * 寄存器的地址为 0x2000~0x2001、0x2002~0x2003、0x2004~0x2005，0x2006~0x2007，0x2008~0x2009 分别对应 5 个模块的 00~0F 和 10~1F 输出通道
         * 
         * 可以同时测试 GX-CL140-S 的输出输入接线是否有误,现场输出连接的是电磁阀,电磁阀控制负压,
         * 负压影响气压表,气压表检测气压低于报警阈值会给信号到输入模块的通道.整条链路通的情况下，改方法可以同时测试输出和输入通道的状态变化
         */
        private void btnWriteDatas_Click(object sender, EventArgs e)
        {
            // 先检查是否已连接，未连接则提示
            if (!modbusClient.Connected)
            {
                MessageBox.Show("请先点击“连接测试”建立通讯！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 设置从站地址（设备地址），文档中耦合器的 Modbus 从站地址为 0x01
            modbusClient.UnitIdentifier = 0x01;

            // 定义要写入的寄存器地址（0x2000 对应数字量输出区域）
            int startAddress = 0x2000;

            // 定义要写入的值（16位，对应16个输出通道）
            // 例如：0x0001 表示只打开第1路；0x0002 只打开第2路；0x0003 同时打开第1和第2路
            // 这里我们测试写入 0x0001，即打开第1路输出
            for(int i = 0; i < 10; i++)
            {
                int valueToWrite = 0x0001;
                for (int j = 1; j <= 16; j++)
                {
                    if(j > 1)
                        valueToWrite *= 2;

                    try
                    {
                        // 使用 WriteSingleRegister 方法写入单个寄存器（功能码 0x06）
                        // 参数：寄存器地址，要写入的值（int 类型，但内部会转为 ushort）
                        modbusClient.WriteSingleRegister(startAddress, valueToWrite);
                    }
                    catch (Exception ex)
                    {
                        // 写入失败显示错误
                        MessageBox.Show($"写入失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                    Thread.Sleep(500); // 每次写入后等待1秒，避免过快操作
                }
                // 循环结束后，将所有通道关闭，写入0x0000
                modbusClient.WriteSingleRegister(startAddress, 0x0000);
                // 为下一轮的循环做准备，将起始地址加1，模拟写入下一个寄存器
                startAddress += 1;
            }
        }

        // ========== 5. 测试载台上电（跳转到 PowerOnTestForm） ==========
        // 功能：打开“载台上电（继电器）测试”窗体，用于现场逐排 / 逐点验证 9×8=72 路载台上电输出。
        // 该窗体内部自己管理 ModbusClient（与 MainForm 解耦），需要先在窗体内点击“连接测试”。
        private void btnPowerOnTest_Click(object sender, EventArgs e)
        {
            // 使用默认参数（192.168.1.20:502）创建子窗体
            // 与本窗体 InitData() 中的 IP/端口保持一致
            using (PowerOnTestForm form = new PowerOnTestForm())
            {
                // 以模态方式打开，关闭后才返回主窗体
                form.ShowDialog(this);
            }
        }
    }
}
