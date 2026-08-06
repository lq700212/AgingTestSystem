using NModbus;
using NModbus.Device;
using NModbus.Serial;
using Sunny.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Text;
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

        // 扫描/写入日志的写锁：日志文件是共享资源，多线程同时写会互相踩坏，必须串行化
        private static readonly object ScanLogLock = new object();

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

        // ================= 扫描/写入日志（CSV 落盘） =================

        /// <summary>
        /// 把"批量读取 / 批量写阈值"里每一台的结果追加写入 CSV 日志：
        /// Logs\BarometerScan_yyyyMMdd.csv（在程序 exe 同目录下）。
        ///
        /// 【为什么要落盘】设备掉线分两类，光靠弹窗分不出来：
        ///   1) 永久离线：每次都是满超时（约 3 秒）→ 大概率设备坏 / 断电 / 接线断 / 地址拨错；
        ///   2) 间歇性掉线：这次成功、下次超时 → 大概率总线干扰 / 供电不稳 / 接头接触不良。
        /// 把多次扫描结果汇总进 Excel，按"从站号 + 时间"排序，
        /// 一眼就能看出是"永远失败"还是"偶尔失败"——
        /// 这份客观数据可直接交给电工，帮他判断是"换设备"还是"查接线"。
        ///
        /// 列格式：时间戳, 操作, 从站号, 结果, 耗时ms, 备注
        /// </summary>
        /// <param name="operation">操作名：批量读取 / 批量写阈值</param>
        /// <param name="slaveId">从站地址（1~72）</param>
        /// <param name="result">成功 / 超时</param>
        /// <param name="elapsedMs">本次往返耗时（毫秒）。失败时约等于读超时时间(3000ms)</param>
        /// <param name="note">备注，一般记异常类型名（如 TimeoutException）</param>
        private void AppendScanLog(string operation, byte slaveId, string result, long elapsedMs, string note = "")
        {
            try
            {
                lock (ScanLogLock) // 串行写，避免多线程踩坏同一个文件
                {
                    // 日志放在 exe 同目录的 Logs 子目录，按天分文件，方便按日期翻查
                    string dir = Path.Combine(Application.StartupPath, "Logs");
                    Directory.CreateDirectory(dir);
                    string file = Path.Combine(dir, $"BarometerScan_{DateTime.Now:yyyyMMdd}.csv");

                    bool needHeader = !File.Exists(file); // 新建文件才写表头
                    using (var writer = new StreamWriter(file, true, Encoding.UTF8))
                    {
                        if (needHeader)
                            writer.WriteLine("时间戳,操作,从站号,结果,耗时ms,备注");
                        writer.WriteLine(
                            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff},{operation},{slaveId},{result},{elapsedMs},{note}");
                    }
                }
            }
            catch
            {
                // 日志写失败（磁盘满 / 目录被锁等）绝不能影响主流程，静默忽略
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
        ///
        /// 【重要说明 / 给新手】关于"某个从站写入超时"的真相：
        /// Modbus 主站写完一帧后，必须等到从站回帧才算写入成功。
        /// 如果某台设备 断电 / 掉线 / 从站地址拨错 / 损坏，它永远不会回帧，
        /// 主站只能等 ReadTimeout(3000ms) 超时；NModbus 默认还会重试 3 次，
        /// 所以**一台坏设备会阻塞整个批量约 12 秒**——这是设备问题，不是程序 bug。
        ///
        /// 程序该做的是：把失败设备记录下来，最后一次性告诉操作员"哪几台没写成功"，
        /// 而不是逐台弹 MessageBox（弹窗会卡住整个批量流程，失败几十台就得点几十次确认）。
        /// </summary>
        /// <param name="slaveIds">从站地址数组</param>
        /// <param name="thresholdValues">对应的阈值寄存器值数组</param>
        /// <param name="writeDelayMs">每次写入后延时（毫秒），让总线/设备有时间处理</param>
        /// <returns>写入结果字典（slaveId → 是否成功）</returns>
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

                // 计时：把每台的"往返耗时"记进日志，用于区分永久离线(≈3s满超时)还是间歇(时快时慢)
                var sw = Stopwatch.StartNew();
                try
                {
                    // 写单个保持寄存器（功能码 0x06）。
                    // 注意：NModbus 内部默认对超时自动重试 3 次，这里不要再包一层应用层重试，
                    //       否则坏设备会把批量流程拖得极慢（4次×3s × 再重试 = 数十秒）。
                    _master.WriteSingleRegister(slaveId, 0x0010, value);
                    sw.Stop();
                    result[slaveId] = true;
                    AppendScanLog("批量写阈值", slaveId, "成功", sw.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    // 失败不弹窗、不中断批量：先记录，最后统一汇总给操作员。
                    sw.Stop();
                    result[slaveId] = false;
                    Debug.WriteLine($"❌ 从站 {slaveId} 写入失败：{ex.Message}");
                    AppendScanLog("批量写阈值", slaveId, "超时", sw.ElapsedMilliseconds, ex.GetType().Name);
                }

                // 每写一台后让总线安静一小段，避免 72 台连写时帧间隔过密（干扰/丢帧）。
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

            // 4. 批量写入（不再逐台弹窗：失败设备最后统一汇总，见 ShowBatchWriteSummary）
            try
            {
                var result = BatchSetThreshold(slaveIds, thresholdValues, writeDelayMs: 50);
                ShowBatchWriteSummary(result);
            }
            catch (Exception ex)
            {
                this.ShowWarningDialog($"批量设置失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 汇总显示批量写入结果
        /// 一次性告诉操作员"成功几台、失败几台、失败的是哪几台"。
        /// 这是相对旧实现（逐台 MessageBox）的关键改进：失败几十台也不用点几十次确认。
        /// </summary>
        /// <param name="result">slaveId → 是否成功</param>
        private void ShowBatchWriteSummary(Dictionary<byte, bool> result)
        {
            int successCount = 0;
            var failedList = new List<byte>();
            foreach (var kv in result)
            {
                if (kv.Value) successCount++;
                else failedList.Add(kv.Key);
            }

            if (failedList.Count == 0)
            {
                this.ShowSuccessDialog($"批量设置完成！成功 {successCount} 台，全部成功。");
                return;
            }

            string failedText = string.Join("、", failedList);
            this.ShowWarningDialog($"批量设置完成！成功 {successCount} 台，失败 {failedList.Count} 台。\r\n" +
                                   $"失败从站：{failedText}\r\n\r\n" +
                                   "提示：失败通常表示该台气压表断电 / 掉线 / 从站地址拨错 / 损坏，\r\n" +
                                   "请检查硬件后重试（可用『批量读取压力』按钮扫一遍确认）。");
        }

        /// <summary>
        /// 批量读取所有从站压力，找出"离线 / 无响应"的设备
        ///
        /// 【用途】排查"某台写入超时"之前，先用这个扫一遍，定位到底是哪几台没响应
        /// （写入超时的设备 = 读也不响应，基本可以确认是设备/接线/供电问题）。
        /// </summary>
        /// <returns>离线的从站地址列表（在线设备不返回）</returns>
        private List<byte> ScanOfflineDevices()
        {
            var offline = new List<byte>();
            if (!_isPortOpen || _master == null)
            {
                this.ShowWarningDialog("串口未打开，请检查连接！");
                return offline;
            }

            // 扫描时关闭 NModbus 内部重试：只要"能否读到"，一次超时(3s)就够判定，
            // 否则一台坏设备会重试 4 次拖 12 秒，整个扫描会非常慢。
            int oldRetries = _master.Transport.Retries;
            _master.Transport.Retries = 0;
            try
            {
                for (byte slaveId = 1; slaveId <= sum; slaveId++)
                {
                    // 计时：每台耗时写进 CSV，离线台的耗时 ≈ 满超时(3s)，可用于判断"永久"还是"间歇"
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        _master.ReadInputRegisters(slaveId, 0x0001, 2);
                        sw.Stop();
                        AppendScanLog("批量读取", slaveId, "成功", sw.ElapsedMilliseconds);
                    }
                    catch (Exception ex)
                    {
                        sw.Stop();
                        offline.Add(slaveId);
                        AppendScanLog("批量读取", slaveId, "超时", sw.ElapsedMilliseconds, ex.GetType().Name);
                    }
                    Thread.Sleep(20); // 稍微间隔，避免连读时总线过密
                }
            }
            finally
            {
                _master.Transport.Retries = oldRetries; // 恢复原设置，不影响其它功能
            }
            return offline;
        }

        /// <summary>
        /// 点击"批量读取压力"按钮：扫描全部从站，报告离线的设备
        /// </summary>
        private void btnBatchRead_Click(object sender, EventArgs e)
        {
            try
            {
                var offline = ScanOfflineDevices();
                if (offline.Count == 0)
                {
                    this.ShowSuccessDialog($"已扫描 {sum} 台，全部在线。");
                }
                else
                {
                    this.ShowWarningDialog($"已扫描 {sum} 台，离线 {offline.Count} 台：{string.Join("、", offline)}\r\n\r\n" +
                                           "请检查对应设备的供电 / RS485 接线 / 从站地址拨码。");
                }
            }
            catch (Exception ex)
            {
                this.ShowWarningDialog($"批量读取失败：{ex.Message}");
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