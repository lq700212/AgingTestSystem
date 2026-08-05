using NModbus;
using NModbus.Device;   // 包含 ModbusFactory, IModbusMaster
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ModbusTCPFanControllerTest
{
    /// <summary>
    /// 冷却送风机 Modbus TCP 通信客户端
    /// 
    /// 本类封装了与冷却送风机控制器的 Modbus TCP 通信，寄存器映射基于实测数据：
    /// 
    /// 寄存器地址（十六进制）  功能        读写    数据类型        说明
    /// ------------------------------------------------------------------------
    /// 0x0000              组合状态    只读    ushort          未使用（忽略）
    /// 0x0001              控制/状态   读写    ushort          读取：0x0002=定值停止，0x0003=定值启动，
    ///                                                        0x0001=程式启动，0x0000=程式停止
    ///                                                        写入：0x0003=定值启动，0x0002=停止
    /// 0x0002              当前温度    只读    ushort          实际值 = 寄存器值 / 100（单位：°C）
    /// 0x0003              当前湿度    只读    ushort          实际值 = 寄存器值 / 100（单位：%RH）
    /// 0x0004              温度设定值  只读    ushort          实际值 = 寄存器值 / 100（单位：°C）
    /// 0x0005              湿度设定值  只读    ushort          实际值 = 寄存器值 / 100（单位：%RH）
    /// 
    /// 通信方式：
    ///   - 物理层：TCP/IP（以太网）
    ///   - 协议：标准 Modbus TCP（带 MBAP 头部，无 CRC 校验）
    ///   - 端口：默认为 50000（实际使用时可修改为标准 502 或其他）
    ///   - 使用 ModbusFactory.CreateMaster() 方法创建主站，
    ///     该方法自动处理 Modbus TCP 报文头部（事务ID、协议标识、字节计数），
    ///     开发者无需手动构造。
    /// </summary>
    public class FanControllerClient : IDisposable
    {
        private readonly string _ip;                 // 设备 IP 地址
        private readonly int _port;                  // 端口号（默认 50000）
        private readonly byte _slaveId;              // 从站地址（默认 1）
        private readonly int _timeoutMs;             // 超时毫秒数
        private readonly Action<string> _logAction;  // 日志回调

        private TcpClient _tcpClient;
        private IModbusMaster _master;              // ★ 使用接口类型 IModbusMaster
        private readonly SemaphoreSlim _connectLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="ip">控制器 IP 地址</param>
        /// <param name="port">端口号，默认 50000（若设备使用标准 502 端口可传入 502）</param>
        /// <param name="slaveId">从站地址，默认 1</param>
        /// <param name="timeoutMs">通讯超时（毫秒），默认 3000</param>
        /// <param name="logAction">日志记录委托，可选</param>
        public FanControllerClient(string ip, int port = 50000, byte slaveId = 1,
            int timeoutMs = 3000, Action<string> logAction = null)
        {
            _ip = ip ?? throw new ArgumentNullException(nameof(ip));
            _port = port;
            _slaveId = slaveId;
            _timeoutMs = timeoutMs;
            _logAction = logAction;
        }

        /// <summary>
        /// 记录日志（内部使用）
        /// </summary>
        private void Log(string msg) => _logAction?.Invoke($"{DateTime.Now:HH:mm:ss.fff} {msg}");

        /// <summary>
        /// 确保 TCP 连接已建立，并创建 Modbus TCP Master。
        /// 该方法线程安全，使用 SemaphoreSlim 防止并发重连。
        /// </summary>
        private async Task EnsureConnectedAsync()
        {
            // 快速检查，避免不必要的锁等待
            if (_tcpClient != null && _tcpClient.Connected)
                return;

            await _connectLock.WaitAsync();
            try
            {
                // 双重检查，防止等待锁期间其他线程已完成连接
                if (_tcpClient != null && _tcpClient.Connected)
                    return;

                // 关闭旧连接
                _tcpClient?.Close();
                _tcpClient?.Dispose();

                // 创建新 TCP 连接
                _tcpClient = new TcpClient();
                _tcpClient.ReceiveTimeout = _timeoutMs;
                _tcpClient.SendTimeout = _timeoutMs;

                try
                {
                    await _tcpClient.ConnectAsync(_ip, _port);
                    Log($"已连接到 {_ip}:{_port}");
                }
                catch (Exception ex)
                {
                    Log($"连接失败: {ex.Message}");
                    _tcpClient?.Dispose();
                    _tcpClient = null;
                    throw;  // 重新抛出，让上层处理
                }

                // ★★★ 关键修改：使用 ModbusFactory.CreateMaster 方法 ★★★
                // 该方法接受 TcpClient 对象，返回 IModbusMaster 接口，
                // 是 NModbus 3.0.x 版本中创建 TCP 主站的标准方式。
                var factory = new ModbusFactory();
                _master = factory.CreateMaster(_tcpClient);

                // 设置超时（通过 Transport 属性）
                if (_master.Transport != null)
                {
                    _master.Transport.ReadTimeout = _timeoutMs;
                    _master.Transport.WriteTimeout = _timeoutMs;
                }
                Log($"Modbus TCP Master 创建成功，从站地址: {_slaveId}");
            }
            finally
            {
                _connectLock.Release();
            }
        }

        /// <summary>
        /// 写入单个保持寄存器（功能码 0x06）
        /// 用于发送控制命令（启动/停止）
        /// </summary>
        /// <param name="registerAddress">寄存器地址（如 0x0001）</param>
        /// <param name="value">要写入的值</param>
        private async Task WriteSingleRegisterAsync(ushort registerAddress, ushort value)
        {
            await EnsureConnectedAsync();
            if (_master == null)
                throw new InvalidOperationException("Modbus Master 未初始化");
            await _master.WriteSingleRegisterAsync(_slaveId, registerAddress, value);
            Log($"写入寄存器 0x{registerAddress:X4} = 0x{value:X4}");
        }

        /// <summary>
        /// 读取连续多个保持寄存器（功能码 0x03）
        /// </summary>
        /// <param name="startAddress">起始寄存器地址</param>
        /// <param name="quantity">寄存器数量</param>
        /// <returns>ushort 数组，每个元素对应一个寄存器值</returns>
        private async Task<ushort[]> ReadHoldingRegistersAsync(ushort startAddress, ushort quantity)
        {
            await EnsureConnectedAsync();
            if (_master == null)
                throw new InvalidOperationException("Modbus Master 未初始化");
            ushort[] values = await _master.ReadHoldingRegistersAsync(_slaveId, startAddress, quantity);
            Log($"读取寄存器 0x{startAddress:X4} 数量 {quantity} 成功");
            return values;
        }

        // ========== 控制命令（对外公开） ==========

        /// <summary>
        /// 发送定值启动命令（写入 0x0001 = 0x0003）
        /// </summary>
        public async Task StartFixedValueAsync()
        {
            await WriteSingleRegisterAsync(0x0001, (ushort)FanCommand.FixedValueStart);
            Log("定值启动指令已发送");
        }

        /// <summary>
        /// 发送停止命令（写入 0x0001 = 0x0002）
        /// </summary>
        public async Task StopAsync()
        {
            await WriteSingleRegisterAsync(0x0001, (ushort)FanCommand.Stop);
            Log("停止指令已发送");
        }

        // ========== 各参数独立读取（供需要单独获取的场景使用） ==========

        /// <summary>
        /// 读取运行状态（寄存器 0x0001）
        /// </summary>
        /// <returns>FanCommand 枚举值</returns>
        public async Task<FanCommand> ReadCurrentStateAsync()
        {
            ushort[] values = await ReadHoldingRegistersAsync(0x0001, 1);
            return (FanCommand)values[0];
        }

        /// <summary>
        /// 读取当前温度（寄存器 0x0002，单位 0.01°C）
        /// </summary>
        /// <returns>温度值（摄氏度）</returns>
        public async Task<float> ReadTemperatureAsync()
        {
            ushort[] values = await ReadHoldingRegistersAsync(0x0002, 1);
            return values[0] / 100.0f;
        }

        /// <summary>
        /// 读取当前湿度（寄存器 0x0003，单位 0.01%RH）
        /// </summary>
        /// <returns>湿度值（百分比）</returns>
        public async Task<float> ReadHumidityAsync()
        {
            ushort[] values = await ReadHoldingRegistersAsync(0x0003, 1);
            return values[0] / 100.0f;
        }

        /// <summary>
        /// 读取温度设定值（寄存器 0x0004，单位 0.01°C）
        /// </summary>
        /// <returns>设定温度（摄氏度）</returns>
        public async Task<float> ReadTemperatureSetpointAsync()
        {
            ushort[] values = await ReadHoldingRegistersAsync(0x0004, 1);
            return values[0] / 100.0f;
        }

        /// <summary>
        /// 读取湿度设定值（寄存器 0x0005，单位 0.01%RH）
        /// </summary>
        /// <returns>设定湿度（百分比）</returns>
        public async Task<float> ReadHumiditySetpointAsync()
        {
            ushort[] values = await ReadHoldingRegistersAsync(0x0005, 1);
            return values[0] / 100.0f;
        }

        // ========== 批量读取（推荐，一次读取6个寄存器，减少通讯次数） ==========

        /// <summary>
        /// 一次性读取所有参数（寄存器 0x0000 ~ 0x0005 共6个）
        /// 返回元组包含：状态、温度、湿度、温度设定、湿度设定。
        /// 注意：0x0000 寄存器未使用，但为了保持与实测指令一致，仍读取6个。
        /// </summary>
        /// <returns>(状态, 温度, 湿度, 温度设定, 湿度设定)</returns>
        public async Task<(FanCommand State, float Temperature, float Humidity, float TempSetpoint, float HumSetpoint)>
            ReadAllParametersAsync()
        {
            // 读取 6 个寄存器，对应地址 0x0000 ~ 0x0005
            ushort[] values = await ReadHoldingRegistersAsync(0x0000, 6);

            // 根据实测数据，各参数在数组中的索引：
            // values[0] -> 0x0000（忽略）
            // values[1] -> 0x0001（状态）
            // values[2] -> 0x0002（温度）
            // values[3] -> 0x0003（湿度）
            // values[4] -> 0x0004（温度设定）
            // values[5] -> 0x0005（湿度设定）
            return (
                State: (FanCommand)values[1],
                Temperature: values[2] / 100.0f,
                Humidity: values[3] / 100.0f,
                TempSetpoint: values[4] / 100.0f,
                HumSetpoint: values[5] / 100.0f
            );
        }

        /// <summary>
        /// 释放所有资源（关闭连接、释放锁）
        /// </summary>
        public void Dispose()
        {
            _master?.Dispose();
            _tcpClient?.Close();
            _tcpClient?.Dispose();
            _connectLock?.Dispose();
            Log("资源已释放");
        }
    }
}