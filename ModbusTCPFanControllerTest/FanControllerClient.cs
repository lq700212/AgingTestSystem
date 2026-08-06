using NModbus;
using NModbus.Device;   // 包含 ModbusFactory, IModbusMaster
using System;
using System.Collections.Generic;
using System.IO;        // 用于读写"上次连接成功 IP"的磁盘缓存文件
using System.Linq;      // 用于 List.Contains(item, comparer) 的去重
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
    ///   - 连接超时（防呆）：.NET Framework 的 TcpClient.ConnectAsync 不受 ReceiveTimeout 约束，
    ///     IP/端口填错时系统 TCP 连接默认要等约 20 秒才超时。本类用 Task.WhenAny 自设超时，
    ///     timeoutMs 毫秒连不上立即抛 TimeoutException，并给出"请检查 IP/端口"的明确提示，
    ///     避免界面长时间假死、也缩小了并发竞态被放大的窗口。
    ///   - 自动识别 IP（V1.2 新增）：现场送风机控制器可能位于 192.168.1.220 / .221 / .222 中的
    ///     任意一个。传入候选 IP 列表后，连接时按顺序逐个尝试，第一个连接成功的即为设备真实地址
    ///     （见 <see cref="DefaultCandidateIps"/> 与 ConnectedIp 属性），现场换设备/换 IP 不用改代码。
    ///   - 工控机记忆（V1.2 新增）：连接成功后把"本工控机连上的控制器 IP"写入程序目录下的
    ///     FanLastIp.cache；下次启动优先用缓存地址直接连，连不上再回落候选列表，省去逐个试探的耗时。
    /// </summary>
    public class FanControllerClient : IDisposable
    {
        private readonly string _ip;                 // 首选/界面填写的 IP 地址（用于防呆比较，不一定是实际连上的）
        private readonly string[] _candidateIps;     // 候选 IP 列表（自动识别时按顺序逐个尝试）
        private readonly int _port;                  // 端口号（默认 50000）
        private readonly byte _slaveId;              // 从站地址（默认 1）
        private readonly int _timeoutMs;             // 超时毫秒数
        private readonly Action<string> _logAction;  // 日志回调

        private TcpClient _tcpClient;
        private IModbusMaster _master;              // ★ 使用接口类型 IModbusMaster
        private readonly SemaphoreSlim _connectLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// 实际连接成功的 IP 地址（自动识别结果）
        /// 连接成功前为 null；连接成功后为候选列表里第一个连上的地址。
        /// </summary>
        private string _connectedIp;

        /// <summary>
        /// 优先尝试的 IP（磁盘缓存的上次成功地址，即"本工控机记忆"）
        /// 连接时排在最前面：上次连上过哪个控制器，下次直接先试它，省去逐个试探的耗时。
        /// </summary>
        private string _preferredIp;

        /// <summary>
        /// 是否已尝试从磁盘缓存恢复 _preferredIp（防止每次连接都读一次磁盘）
        /// </summary>
        private bool _preferredIpLoaded;

        /// <summary>
        /// 磁盘缓存文件名（放在程序 exe 所在目录下）
        /// 内容 = 一行文本：本工控机最近一次连接成功的送风机 IP。
        /// </summary>
        private const string IpCacheFileName = "FanLastIp.cache";

        /// <summary>
        /// 内置的送风机候选 IP 列表（现场实测：控制器可能在这三个 IP 中的任意一个）
        /// 供"自动识别"使用：连接时按顺序逐个尝试，第一个连上的即为设备真实地址，
        /// 现场换设备 / 换工作台 / 换 IP 都不用改代码、不用改配置。
        /// </summary>
        public static readonly string[] DefaultCandidateIps =
        {
            "192.168.1.220",   // 现场主设备（当前实测连接成功）
            "192.168.1.221",   // 备用：现场另一台控制器可能用这个
            "192.168.1.222"    // 备用：现场另一台控制器可能用这个
        };

        /// <summary>
        /// 构造函数（单个 IP，向后兼容）
        /// 等价于只给一个候选 IP 的 <see cref="FanControllerClient(IEnumerable{string}, int, byte, int, Action{string})"/>。
        /// </summary>
        /// <param name="ip">控制器 IP 地址</param>
        /// <param name="port">端口号，默认 50000（若设备使用标准 502 端口可传入 502）</param>
        /// <param name="slaveId">从站地址，默认 1</param>
        /// <param name="timeoutMs">通讯超时（毫秒），默认 3000</param>
        /// <param name="logAction">日志记录委托，可选</param>
        public FanControllerClient(string ip, int port = 50000, byte slaveId = 1,
            int timeoutMs = 3000, Action<string> logAction = null)
            : this(new[] { ip }, port, slaveId, timeoutMs, logAction)
        {
        }

        /// <summary>
        /// 构造函数（自动识别：候选 IP 列表）
        /// 连接时按顺序尝试候选 IP，第一个连接成功的即为设备真实地址（见 EnsureConnectedAsync）。
        /// </summary>
        /// <param name="candidateIps">候选 IP 列表（自动去重、过滤非法 IP；第一个作为 <see cref="Ip"/> 的"首选地址"）</param>
        /// <param name="port">端口号，默认 50000（若设备使用标准 502 端口可传入 502）</param>
        /// <param name="slaveId">从站地址，默认 1</param>
        /// <param name="timeoutMs">通讯超时（毫秒），默认 3000</param>
        /// <param name="logAction">日志记录委托，可选</param>
        public FanControllerClient(IEnumerable<string> candidateIps, int port = 50000, byte slaveId = 1,
            int timeoutMs = 3000, Action<string> logAction = null)
        {
            if (candidateIps == null) throw new ArgumentNullException(nameof(candidateIps));

            // 过滤空项 / 非法 IP，并按原顺序去重，得到"有效候选 IP 列表"
            var list = new List<string>();
            foreach (string ip in candidateIps)
            {
                if (string.IsNullOrWhiteSpace(ip)) continue;
                string trimmed = ip.Trim();
                if (!System.Net.IPAddress.TryParse(trimmed, out _)) continue;   // 跳过非法 IP
                if (!list.Contains(trimmed, StringComparer.OrdinalIgnoreCase)) list.Add(trimmed);
            }
            if (list.Count == 0)
                throw new ArgumentException("候选 IP 列表为空或全部非法，至少需要一个有效 IP", nameof(candidateIps));

            _candidateIps = list.ToArray();
            // Ip 属性返回"首选地址"（通常是界面填写的 IP），供 UI 判断"界面地址是否已应用"。
            // 注意：这不是实际连上的地址；实际连上的地址在 ConnectedIp。
            _ip = _candidateIps[0];
            _port = port;
            _slaveId = slaveId;
            _timeoutMs = timeoutMs;
            _logAction = logAction;
        }

        /// <summary>
        /// 当前客户端使用的首选 IP 地址（只读，构造后不可变）。
        /// 注意：这是"首选/界面填写的"地址，不一定是实际连上的地址。
        /// 供 UI 判断"界面上填的地址是否已应用"。
        /// </summary>
        public string Ip => _ip;

        /// <summary>
        /// 实际连接成功的 IP 地址（自动识别结果）
        /// 与 <see cref="Ip"/> 的区别：Ip 是"首选地址"（界面填写的），
        /// ConnectedIp 是"真正连上的设备地址"（可能在候选列表的任意一个）。
        /// 连接成功前为 null。
        /// </summary>
        public string ConnectedIp => _connectedIp;

        /// <summary>
        /// 当前客户端使用的端口号（只读，构造后不可变）。
        /// 供 UI 判断"界面上填的地址是否已应用"。
        /// </summary>
        public int Port => _port;

        /// <summary>
        /// 组装本次连接要尝试的 IP 顺序（自动识别核心 + 工控机记忆）
        ///
        /// 顺序约定（越靠前越优先）：
        ///   1) 磁盘缓存的上次成功 IP（_preferredIp）——"本工控机记忆"：
        ///      上次连上过哪个控制器，下次直接先试它，不用再从配置列表开头逐个试探；
        ///   2) 构造时传入的候选 IP 列表（界面填的 IP 排最前 + 配置 FanIpCandidates）。
        /// 自动去重：缓存地址与候选列表里相同的不重复尝试。
        /// </summary>
        private IEnumerable<string> BuildCandidateOrder()
        {
            // 首次使用时从磁盘缓存恢复"上次连接成功的 IP"
            if (!_preferredIpLoaded)
            {
                _preferredIpLoaded = true;
                _preferredIp = LoadCachedIp();
            }

            var order = new List<string>();
            if (!string.IsNullOrWhiteSpace(_preferredIp)) order.Add(_preferredIp);   // 缓存优先
            foreach (string ip in _candidateIps)
            {
                if (!order.Contains(ip, StringComparer.OrdinalIgnoreCase)) order.Add(ip);
            }
            return order;
        }

        /// <summary>
        /// 读取磁盘缓存的上次连接成功的送风机 IP
        /// 缓存文件位置：程序 exe 所在目录下的 <see cref="IpCacheFileName"/>（内容 = 一行 IP 文本）。
        /// 文件不存在 / 内容非法 → 返回 null（表示无缓存，回落候选列表逐个尝试）。
        /// 读失败不阻塞连接：任何异常都当作"无缓存"处理。
        /// </summary>
        private string LoadCachedIp()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, IpCacheFileName);
                if (!File.Exists(path)) return null;
                string content = File.ReadAllText(path).Trim();
                // 只认合法 IPv4：防止缓存被写坏后一直连错地址
                return System.Net.IPAddress.TryParse(content, out _) ? content : null;
            }
            catch
            {
                return null;   // 读缓存失败不阻塞连接
            }
        }

        /// <summary>
        /// 把"本次连接成功的送风机 IP"写入磁盘缓存
        /// 下次启动 <see cref="BuildCandidateOrder"/> 会优先用它，直接连上，不用再从头试探。
        /// 写失败忽略（无写权限 / 磁盘只读等），下次仍回落候选列表。
        /// </summary>
        /// <param name="ip">连接成功的 IP 地址</param>
        private void SaveCachedIp(string ip)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, IpCacheFileName);
                File.WriteAllText(path, ip);
            }
            catch
            {
                // 写缓存失败忽略（无写权限 / 磁盘只读等），下次仍回落候选列表
            }
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

                // 关闭旧连接（含旧的 Master），全部置空，保证从干净状态开始重连
                _master?.Dispose();
                _master = null;
                _tcpClient?.Close();
                _tcpClient?.Dispose();
                _tcpClient = null;

                try
                {
                    // 【自动识别 IP + 工控机记忆】优先试"上次连接成功的 IP"（磁盘缓存），
                    // 连不上再按顺序试候选列表（界面填的 IP + 配置 FanIpCandidates）。
                    // 第一个连接成功的就是设备真实地址，不需要每次改 IP/配置。
                    bool connected = false;
                    foreach (string ip in BuildCandidateOrder())
                    {
                        // 每个候选 IP 都用一个全新的 TcpClient（上一个失败时旧的已关闭，不能复用）
                        _tcpClient = new TcpClient();
                        _tcpClient.ReceiveTimeout = _timeoutMs;
                        _tcpClient.SendTimeout = _timeoutMs;

                        try
                        {
                            // 【防呆】连接超时控制：
                            // .NET Framework 的 TcpClient.ConnectAsync 不受 ReceiveTimeout/SendTimeout 约束，
                            // 一旦 IP/端口填错（比如连到不存在的地址），系统 TCP 连接默认要等约 20 秒才超时，
                            // 期间界面像"假死"，也会放大"连接测试 vs 定时刷新"的并发竞态窗口。
                            // 这里用 Task.WhenAny 包一层自设超时：timeoutMs 毫秒连不上，立即放弃本候选 IP。
                            var connectTask = _tcpClient.ConnectAsync(ip, _port);
                            var timeoutTask = Task.Delay(_timeoutMs);
                            if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
                            {
                                // 本候选 IP 超时：销毁这次连接，尝试下一个候选
                                _tcpClient.Close();
                                _tcpClient.Dispose();
                                _tcpClient = null;
                                Log($"连接超时（{_timeoutMs}ms）：{ip}:{_port}，尝试下一个候选 IP");
                                continue;
                            }
                            await connectTask; // 连接已成功；若失败这里会抛出真正的异常（如 SocketException）
                            _connectedIp = ip;   // 记录实际连上的地址（供界面显示）
                            connected = true;
                            SaveCachedIp(ip);    // 写盘缓存：本工控机"上次连上的控制器 IP"，下次启动直接用它
                            Log($"已连接到 {ip}:{_port}");
                            break;   // 找到设备，不再尝试后面的候选 IP
                        }
                        catch (Exception ex)
                        {
                            // 本候选 IP 连接失败：记日志，继续尝试下一个候选
                            Log($"连接失败: {ex.Message}（{ip}:{_port}），尝试下一个候选 IP");
                            _tcpClient?.Dispose();
                            _tcpClient = null;
                        }
                    }

                    // 所有候选 IP 都连不上：抛明确异常，提示检查 IP/端口
                    if (!connected)
                    {
                        throw new TimeoutException(
                            $"所有候选 IP 都无法连接（共 {_candidateIps.Length} 个: {string.Join(", ", _candidateIps)}），请检查设备 IP / 端口是否正确");
                    }
                }
                catch (Exception ex)
                {
                    Log($"连接失败: {ex.Message}");
                    _master?.Dispose();
                    _master = null;
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
        ///
        /// 【为什么要加锁】
        /// Demo 里"连接测试"按钮会 Dispose + 重新 new 客户端，而 2 秒自动刷新定时器
        /// （TimerRefresh_Tick）也可能正在 RefreshStateAsync。
        /// 如果 Dispose 不拿锁，就会和正在进行的 ConnectAsync/读写并发，把 TcpClient
        /// 半途关掉，报出不可预期的异常（如 NullReferenceException / SocketException）。
        /// 这里拿同一把 _connectLock，确保"先等正在进行的操作结束，再销毁连接"。
        /// </summary>
        public void Dispose()
        {
            // 最多等 2 秒：正常情况下定时刷新很快就结束；极端情况（设备连不上、
            // 连接正挂起）也不能让 UI 卡死——超时就强制销毁，后续调用会看到
            // _tcpClient 为 null 并自动重建。
            bool acquired = _connectLock.Wait(2000);

            try
            {
                _master?.Dispose();
                _master = null;
                _tcpClient?.Close();
                _tcpClient?.Dispose();
                _tcpClient = null;
                Log("资源已释放");
            }
            finally
            {
                // 只有真正拿到锁才 Release；超时没拿到就不释放，
                // 否则会把信号量计数加爆（SemaphoreFullException）。
                if (acquired) _connectLock.Release();
            }
        }
    }
}
