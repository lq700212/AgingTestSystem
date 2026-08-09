using System;
using System.Collections.Generic;
using System.IO;       // 用于读写"上次连接成功端口"的磁盘缓存文件
using System.IO.Ports;
using AgingTestSystem.Interfaces;
using AgingTestSystem.Models;
using NModbus;
using NModbus.Serial;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 气压表通讯实现（Modbus RTU / RS485）
    /// 
    /// 适用场景：
    /// - 气压表通过 RS485 转 USB 接入工控机
    /// - 上位机作为 Modbus 主站，定时轮询 1~N 个从站地址读取压力值
    /// 
    /// 设计要点（给新手看的）：
    /// 1) SerialPort 不是线程安全的：同一时刻只允许一个线程读/写串口。
    ///    因此这里用 _syncRoot 做互斥锁，保证 Modbus 请求不会并发。
    /// 2) Modbus RTU 每一帧都包含从站地址：这里默认用 deviceId 作为从站地址（1~72）。
    ///    现场如果不是这个规则，需要改成“固定从站地址 + 不同寄存器/偏移”。
    /// 3) 寄存器地址/单位/缩放需要现场确认：代码保留了 TODO，通线后按说明书修正即可。
    /// 4) 端口"工控机记忆"（V1.16）：连接成功后把实际端口写入 exe 目录的 BarometerPort.cache，
    ///    下次启动优先用缓存端口直接连（省去重新搜索）；缓存端口失效（设备被拔/换口）再自动
    ///    重新识别 CH340 —— 与送风机 FanLastIp.cache 的机制一致（见 Connect/BuildCandidatePorts）。
    /// </summary>
    public class ModbusRtuBarometerReader : IBarometerReader, IDisposable
    {
        /// <summary>
        /// 串口/主站对象的互斥锁
        /// 
        /// 为什么需要锁：
        /// - SerialPort 不是线程安全的
        /// - NModbus 的 Master 也不应该在多线程同时发请求
        /// - 如果并发读写，会导致帧交叉，出现 CRC 错误、超时、甚至串口假死
        /// 
        /// 本项目里，采集是在 DeviceManager 的定时器线程里进行，
        /// 正常情况下不会有并发；但保留锁可以防止后续扩展（比如手动读某一路）造成并发问题。
        /// </summary>
        private readonly object _syncRoot = new object();

        /// <summary>
        /// 全局配置（来自 App.config 或通讯设置界面）
        /// Connect 时赋值，Disconnect 时不置空（方便错误排查时看配置），但 ReadAllData 会判空保护
        /// </summary>
        private DeviceConfig _config;

        /// <summary>
        /// 阈值寄存器地址（Holding Register 0x0010，功能码 0x06）
        /// 以 ModbusRtuBarometerTest Demo 实测为准，写入设备内部阈值、驱动硬件报警触点。
        /// 【注意】这是"设备阈值"寄存器，不是压力寄存器（0x0001）。
        /// </summary>
        private const ushort ThresholdRegisterAddress = 0x0010;

        /// <summary>
        /// 串口对象（RS485 转 USB 后会表现为一个 COM 口）
        /// </summary>
        private SerialPort _serialPort;

        /// <summary>
        /// Modbus 主站对象（通过 NModbus 创建）
        /// </summary>
        private IModbusMaster _master;

        /// <summary>
        /// 连接状态标志
        /// </summary>
        private bool _isConnected;

        /// <summary>
        /// 实际使用的串口名称（自动识别或配置指定的结果）
        /// 供日志/诊断显示"气压表到底连在哪个 COM 口"，方便现场排查。
        /// </summary>
        public string CurrentPortName { get; private set; }

        /// <summary>
        /// 磁盘缓存文件名（放在程序 exe 所在目录下）
        /// 内容 = 一行文本：本工控机最近一次连接成功的气压表串口（如 "COM9"）。
        /// 【为什么需要它】气压表 RS485 转 USB（CH340）的 COM 号不固定：
        /// 换 USB 插口 / 换电脑 / 驱动重装都会变。第一次连上后把端口记住，
        /// 下次启动优先用它直接连（省去每次重新 WMI 搜索），
        /// 端口失效（设备被拔 / 换口）再自动重新识别 —— 参考送风机 FanLastIp.cache 的成熟做法。
        /// </summary>
        private const string PortCacheFileName = "BarometerPort.cache";

        /// <summary>
        /// 最近一次连接成功的串口名称（缓存值）
        /// 用于重连优化：下次连接时优先尝试它，端口没变的话一次就连上，不用再走 CH340 搜索。
        /// 程序重启后从磁盘缓存文件恢复（见 <see cref="LoadCachedPort"/>）。
        /// </summary>
        private string _cachedPort;

        /// <summary>
        /// 是否已尝试从磁盘缓存恢复 _cachedPort
        /// 防止每次构建候选端口列表都去读一次磁盘
        /// </summary>
        private bool _cachedPortLoadedFromDisk;

        public bool IsConnected => _isConnected;

        public event EventHandler<string> OnError;

        public bool Connect(DeviceConfig config)
        {
            _config = config;
            try
            {
                // 0) 先断开旧连接（如果之前连接过）
                // 这样可以避免重复 Open 串口导致 "Access denied" 或句柄泄漏
                Disconnect();

                // 0.5) 组装候选端口列表（核心：缓存优先 + CH340 自动识别）
                // 顺序（越靠前越优先）：
                //   ① 上次连接成功的端口（磁盘缓存 BarometerPort.cache，省去每次重新搜索）
                //   ② 配置里填的端口 PortName（尊重手动配置）
                //   ③ CH340 自动识别（气压表 RS485 适配器，现场免改配置，参考 Demo）
                // 逻辑一句话：连上就记住；记住的端口连不上，就重新去找。
                List<string> candidates = BuildCandidatePorts(config);
                if (candidates.Count == 0)
                {
                    OnError?.Invoke(this, "气压表连接参数错误：没有可用的串口，请检查 PortName 配置");
                    return false;
                }

                // 1) 按顺序逐个尝试候选端口：第一个打开成功的即为实际使用的端口
                Exception lastError = null;
                foreach (string portName in candidates)
                {
                    try
                    {
                        // 创建串口对象并配置参数（来自 App.config / 通讯设置界面）：
                        // - BaudRate/DataBits/StopBits/Parity: 必须与现场气压表一致
                        // - ReadTimeout/WriteTimeout: 防止串口调用长期卡死
                        _serialPort = new SerialPort(portName)
                        {
                            BaudRate = config.BaudRate,
                            DataBits = config.DataBits,
                            Parity = ParseParity(config.Parity),
                            StopBits = ParseStopBits(config.StopBits),
                            ReadTimeout = config.SerialReadTimeoutMs,
                            WriteTimeout = config.SerialWriteTimeoutMs
                        };

                        _serialPort.Open();
                        CurrentPortName = portName;

                        // 连接成功 → 把本次实际端口写入磁盘缓存：
                        // 下次启动优先用它，不用再走 CH340 搜索
                        //（参考送风机 FanLastIp.cache 的"工控机记忆"做法）。
                        SaveCachedPort(portName);

                        // 通过 NModbus 创建 RTU 主站（会在串口上组装 RTU 帧并处理 CRC 校验）
                        var factory = new ModbusFactory();
                        _master = factory.CreateRtuMaster(_serialPort);
                        _master.Transport.ReadTimeout = config.SerialReadTimeoutMs;
                        _master.Transport.WriteTimeout = config.SerialWriteTimeoutMs;

                        // 标记连接成功
                        _isConnected = true;
                        return true;
                    }
                    catch (Exception ex)
                    {
                        // 本端口打开失败（端口不存在 / 被占用 / 驱动异常等）：
                        // 记下原因，清理资源后继续尝试下一个候选（即"缓存失败 → 重新找端口"）。
                        lastError = ex;
                        try
                        {
                            if (_serialPort != null)
                            {
                                if (_serialPort.IsOpen) _serialPort.Close();
                                _serialPort.Dispose();
                            }
                        }
                        catch
                        {
                            // Close/Dispose 在"串口被拔插"场景可能抛异常，吞掉继续
                        }
                        _serialPort = null;
                        _master = null;
                    }
                }

                // 2) 所有候选端口都连不上：通知上层（会显示在 UI/LOG 上），并清理资源
                OnError?.Invoke(this,
                    $"气压表串口连接失败（已尝试 {candidates.Count} 个端口: {string.Join(", ", candidates)}）: {lastError?.Message}");
                Disconnect();
                return false;
            }
            catch (Exception ex)
            {
                // Connect 的设计约定：
                // - 不向外抛异常（避免启动阶段直接把主程序崩掉）
                // - 通过 OnError 通知上层 UI/日志
                OnError?.Invoke(this, ex.Message);
                Disconnect();
                return false;
            }
        }

        /// <summary>
        /// 组装本次连接要尝试的候选端口列表（端口识别核心）
        ///
        /// 顺序约定（越靠前越优先）：
        ///   1) 上次连接成功的端口（_cachedPort，程序重启后从磁盘缓存 BarometerPort.cache 恢复）
        ///      ——这就是"工控机记忆"：每台工控机优先用自己上次连上的端口，连不上再回落下面的搜索，
        ///         省去每次启动都重新做 CH340 搜索；
        ///   2) 配置的主端口（PortName）——始终优先尝试（尊重手动配置）；
        ///   3) CH340 自动识别（气压表 RS485 适配器）——缓存/配置都连不上时自动搜索。
        /// 自动过滤：空字符串 / 重复项（避免同一个端口试两次）。
        /// </summary>
        /// <param name="config">设备配置</param>
        /// <returns>候选端口列表（可能为空，表示配置里没端口且没识别到 CH340）</returns>
        private List<string> BuildCandidatePorts(DeviceConfig config)
        {
            var list = new List<string>();

            // 局部函数：把"还没加过"的端口追加进候选列表（去重，避免同一个端口试两次）
            void AddCandidate(string port)
            {
                if (string.IsNullOrWhiteSpace(port)) return;
                port = port.Trim();
                foreach (string x in list)
                {
                    if (string.Equals(x, port, StringComparison.OrdinalIgnoreCase)) return;
                }
                list.Add(port);
            }

            // ① 上次连接成功的端口（磁盘缓存恢复 / 本次会话内存）——优先直接连
            //    首次构建时才从磁盘恢复一次，避免每次都读文件
            if (!_cachedPortLoadedFromDisk)
            {
                _cachedPortLoadedFromDisk = true;
                _cachedPort = _cachedPort ?? LoadCachedPort();
            }
            AddCandidate(_cachedPort);

            // ② 配置端口始终尝试
            AddCandidate(config.PortName);

            // ③ CH340 自动识别（缓存/配置都失效时重新搜索，找到新端口后会覆盖缓存）
            List<string> ch340Ports = SerialPortHelper.GetCh340Ports();
            foreach (string ch in ch340Ports)
            {
                AddCandidate(ch);
            }

            return list;
        }

        /// <summary>
        /// 读取磁盘缓存的上次连接成功的气压表串口
        /// 缓存文件位置：程序 exe 所在目录下的 <see cref="PortCacheFileName"/>（内容 = 一行端口文本）。
        ///
        /// 【失效判定（"缓存端口连接失败 → 重新找"）】
        /// 缓存端口必须"当前系统里仍然存在"才有效：
        /// 设备被拔掉 / 换了 USB 插口 / 换了电脑导致 COM 号变化 → 缓存端口已不在系统串口列表里
        /// → 返回 null，上层就会继续尝试配置端口和 CH340 搜索，找到新端口后覆盖缓存。
        /// 文件不存在 / 内容非法 / 读失败 → 一律返回 null（不阻塞连接，回落配置列表）。
        /// </summary>
        /// <returns>缓存端口名称；无有效缓存返回 null</returns>
        private string LoadCachedPort()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PortCacheFileName);
                if (!File.Exists(path)) return null;

                string content = File.ReadAllText(path).Trim();
                if (string.IsNullOrWhiteSpace(content)) return null;

                // 校验缓存端口是否还在系统串口列表里（防止缓存写坏后一直连错端口）
                string[] systemPorts = SerialPortHelper.GetAllPortNames();
                foreach (string sp in systemPorts)
                {
                    if (string.Equals(sp, content, StringComparison.OrdinalIgnoreCase)) return content;
                }
                return null;
            }
            catch
            {
                return null;   // 读缓存失败不阻塞连接
            }
        }

        /// <summary>
        /// 把"本次连接成功的气压表串口"写入磁盘缓存
        /// 下次启动时 <see cref="LoadCachedPort"/> 会优先读它，直接连上，不用再从 CH340 搜索。
        /// 写失败忽略（无写权限 / 磁盘只读等），下次仍回落配置列表。
        /// </summary>
        /// <param name="port">连接成功的端口名称（如 "COM9"）</param>
        private void SaveCachedPort(string port)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PortCacheFileName);
                File.WriteAllText(path, port);
            }
            catch
            {
                // 写缓存失败忽略（无写权限 / 磁盘只读等），下次仍回落配置列表
            }
        }

        public void Disconnect()
        {
            // 断开连接时，先把状态置为 false
            // 上层看到 IsConnected=false 后，可以避免继续发读写请求
            _isConnected = false;

            try
            {
                if (_serialPort != null)
                {
                    // Close/Dispose 可能会在“串口被拔插”场景抛异常，所以这里用 try/catch 吞掉
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.Close();
                    }
                    _serialPort.Dispose();
                }
            }
            catch
            {
            }
            finally
            {
                _serialPort = null;
                _master = null;
            }
        }

        public BarometerData ReadData(int deviceId)
        {
            if (!_isConnected || _config == null || _master == null)
            {
                OnError?.Invoke(this, "设备未连接");
                return null;
            }

            if (deviceId < 1 || deviceId > _config.TotalBarometers)
            {
                OnError?.Invoke(this, $"设备编号 {deviceId} 超出合法范围 [1, {_config.TotalBarometers}]");
                return null;
            }

            try
            {
                ushort[] registers;
                lock (_syncRoot)
                {
                    // 3) 读取输入寄存器（Input Register，功能码 0x04）
                    //    - slaveAddress：从站地址（默认使用 deviceId）
                    //    - startAddress：寄存器地址（来自配置 BarometerPressureRegisterAddress，默认 0x0001）
                    //    - numberOfPoints：一次读 2 个寄存器（0x0001 压力原始值 + 0x0002 小数位数）。
                    //      注意：0x0002 现场实测不可靠，转换不再用它（固定用配置小数位），
                    //      这里仍读 2 个是为了与 Demo 的读取块保持一致（部分仪表需成对读）。
                    //    注意：以 ModbusRtuBarometerTest Demo 实测为准，压力走 Input Register（0x04），
                    //    不是 Holding Register（0x03）——早期实现读 0x0010 是错的（0x0010 实际是阈值寄存器）。
                    registers = _master.ReadInputRegisters((byte)deviceId, _config.BarometerPressureRegisterAddress, 2);
                    if (registers == null || registers.Length < 2) return null;
                }

                // 4) 寄存器值到压力值的转换（以 Demo 为准）
                //    - 压力原始值按有符号 short 解释（0xFFFE → -2，支持负压）
                //    - 【V1.16.1 修复】小数位固定用配置默认值（BarometerDefaultDecimalPlaces=1），
                //      不再读设备 0x0002 —— 现场实测该寄存器不可靠（72 台中 46 台返回 0，
                //      但仪表实际按 1 位小数显示），按 0 位小数换算会把压力显示错 10 倍
                //      （如仪表显示 -95.0 = 寄存器 -950，程序会显示 -950）。
                //      与阈值写入（SetThreshold）保持同一套固定小数位，和仪表显示完全一致。
                //    - 实际压力 = 有符号原始值 / 10^小数位，再乘以可选缩放系数 BarometerPressureScale
                short rawSigned = (short)registers[0];
                int decimalPos = _config.BarometerDefaultDecimalPlaces;
                decimal pressureKPa = rawSigned / (decimal)Math.Pow(10, decimalPos);
                pressureKPa *= _config.BarometerPressureScale;

                var data = new BarometerData
                {
                    DeviceId = deviceId,
                    VacuumPressure = pressureKPa,
                    CollectTime = DateTime.Now
                };

                // 5) 在“采集层”做一次最基础的报警判断，用于 UI 先显示 Fault（红色）。
                //    真正的联动输出（关阀/断电）由 DeviceManager 统一处理，避免通讯类里写业务逻辑。
                bool alarm = IsAlarm(pressureKPa);
                data.Status = alarm ? DeviceStatus.Fault : DeviceStatus.Idle;
                return data;
            }
            catch (Exception ex)
            {
                // 读失败不抛异常，继续让其它设备有机会读取
                OnError?.Invoke(this, $"设备{deviceId}读取失败: {ex.Message}");

                // 【V1.16.2 串口心跳】若异常是"端口级"故障（RS485 适配器被拔出 /
                // 端口被占用 / 端口已关闭等），说明整条串口已断开：
                // 把 _isConnected 置 false，让上层（DeviceManager）感知并提示
                // "气压表串口已断开"+ 后台自动重连。
                // 【重要】单台设备"无响应"（超时 / Modbus 异常码）属于正常离线，
                // 不是端口断开，这里必须避免误判（见 IsPortLevelFailure 注释）。
                if (IsPortLevelFailure(ex))
                {
                    _isConnected = false;
                }
                return null;
            }
        }

        /// <summary>
        /// 判断异常是否为"串口级"故障（【V1.16.2 新增】）
        ///
        /// 串口心跳的核心：要把"单台设备无响应"（正常，设备离线/换表）和
        /// "整条串口断开"（RS485 适配器被拔出 / USB 口被拔 / 端口被占用）区分开：
        /// - 单台无响应 → NModbus 抛超时类异常，串口本身健康，不能标记断开；
        /// - 串口级故障 → 访问被拒绝 / 对象已释放 / IO 关闭类异常，整条总线不可用。
        ///
        /// 判定依据（覆盖 .NET SerialPort / NModbus 在设备拔出时的常见异常）：
        /// 1) UnauthorizedAccessException：端口访问被拒绝（典型：设备被拔后驱动失效）
        /// 2) ObjectDisposedException：串口对象已被释放/关闭
        /// 3) IOException：消息里带"端口 / 信号量 / 关闭 / 不存在 / port / semaphore"等
        ///    关键字（NModbus 的 SlaveException 消息是"功能码/异常码"，不含这些关键字，
        ///    不会被误判为端口断开）。
        /// </summary>
        private static bool IsPortLevelFailure(Exception ex)
        {
            if (ex is UnauthorizedAccessException || ex is ObjectDisposedException)
            {
                return true;
            }

            if (ex is System.IO.IOException ioEx)
            {
                string msg = ioEx.Message ?? "";
                if (ioEx.InnerException != null) msg += " " + ioEx.InnerException.Message;
                msg = msg.ToLowerInvariant();

                // 只要消息提到"端口关闭/不存在/信号量超时/传输中止"等，即判定为串口级故障
                return msg.Contains("closed") || msg.Contains("close") ||
                       msg.Contains("port") || msg.Contains("semaphore") ||
                       msg.Contains("abort") ||
                       msg.Contains("端口") || msg.Contains("信号量") ||
                       msg.Contains("不存在") || msg.Contains("关闭") ||
                       msg.Contains("终止") || msg.Contains("中止");
            }

            return false;
        }

        public BarometerData[] ReadAllData()
        {
            // ReadAllData 的设计约定：
            // - 永远返回数组（即便失败也返回空数组），避免上层出现空引用异常
            if (_config == null)
            {
                OnError?.Invoke(this, "未连接，请先调用 Connect 方法");
                return new BarometerData[0];
            }

            if (!_isConnected)
            {
                // 【V1.16.2 串口心跳】串口断开时返回"全 null 数组"而不是空数组：
                // 让 DeviceManager 的逐台循环仍然能累加失败次数、触发"通讯故障"联动
                // （关阀 + 断电）的安全兜底——避免整条串口掉线时测试中的设备
                // 无人监管、阀门/载台电保持原状。
                OnError?.Invoke(this, "设备未连接（串口已断开，等待自动重连）");
                return new BarometerData[_config.TotalBarometers];
            }

            var data = new BarometerData[_config.TotalBarometers];
            for (int i = 0; i < _config.TotalBarometers; i++)
            {
                // 逐台读取：如果某台失败返回 null，不影响其它台
                data[i] = ReadData(i + 1);
            }
            return data;
        }

        /// <summary>
        /// 写入单台气压表的设备阈值（Holding Register 0x0010，功能码 0x06）
        ///
        /// 【与 Demo 保持一致】ModbusRtuBarometerTest 的 SetThreshold 逻辑：
        ///   1. 小数位 = 固定用配置 BarometerDefaultDecimalPlaces（默认 1，与 Demo 硬编码 1 一致）
        ///   2. 寄存器值 = round(thresholdValue × 10^小数位)，负数按补码写（设备按有符号 short 解释）
        ///   3. 写 WriteSingleRegister(slaveId=deviceId, 0x0010, 寄存器值)
        ///
        /// 【V1.16.1 修复：为什么小数位固定、不再读设备 0x0002】
        /// 现场实测：0x0002 寄存器不可靠（很多台返回 0，但仪表实际按 1 位小数显示）。
        /// 原来按设备返回值换算，对返回 0 的台把 -95 写成寄存器 -95（应为 -950），
        /// 仪表显示就成了 -9.5（差 10 倍）。Demo 注释也注明"0x0002 可能无效"，
        /// 所以 Demo 写阈值一直硬编码 1 位小数 —— 这里改为与 Demo 一致。
        ///
        /// 【单位提醒】thresholdValue 是"设备单位"（与压力读数同单位同小数位），
        /// 不是软件报警阈值 AlarmPressureThresholdKPa（kPa）。写前务必确认设备单位。
        /// </summary>
        /// <param name="deviceId">气压表编号（1~TotalBarometers）</param>
        /// <param name="thresholdValue">设备单位阈值（如 -95.0）</param>
        /// <returns>是否写入成功；设备不响应 / 超时返回 false（不抛异常）</returns>
        public bool SetThreshold(int deviceId, decimal thresholdValue)
        {
            if (!_isConnected || _config == null || _master == null)
            {
                OnError?.Invoke(this, "设备未连接");
                return false;
            }

            if (deviceId < 1 || deviceId > _config.TotalBarometers)
            {
                OnError?.Invoke(this, $"设备编号 {deviceId} 超出合法范围 [1, {_config.TotalBarometers}]");
                return false;
            }

            try
            {
                lock (_syncRoot)
                {
                    // 【V1.16.1 修复】小数位固定用配置默认值（BarometerDefaultDecimalPlaces=1），
                    // 不再读设备 0x0002 —— 该寄存器现场实测不可靠（很多台返回 0，仪表实际是 1 位小数），
                    // 会算出错误寄存器值（-95 → 仪表显示 -9.5）。与 Demo 硬编码 1 位小数保持一致。
                    int decimalPos = _config.BarometerDefaultDecimalPlaces;

                    // 阈值 → 寄存器值：round(阈值 × 10^小数位)
                    // 有符号 short 范围为 -32768~32767；越界说明单位/位数配错，提醒后返回 false
                    int multiplier = (int)Math.Pow(10, decimalPos);
                    long scaled = (long)Math.Round(thresholdValue * multiplier);
                    if (scaled < short.MinValue || scaled > short.MaxValue)
                    {
                        OnError?.Invoke(this, $"设备{deviceId}阈值 {thresholdValue}×10^{decimalPos}={scaled} 超出寄存器范围，请确认单位/小数位");
                        return false;
                    }

                    _master.WriteSingleRegister((byte)deviceId, ThresholdRegisterAddress, (ushort)scaled);
                    return true;
                }
            }
            catch (Exception ex)
            {
                // 写入失败不抛异常（与 ReadData 约定一致），通过 OnError 通知上层
                OnError?.Invoke(this, $"设备{deviceId}写阈值失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 批量写入所有气压表的设备阈值
        ///
        /// 逐台调用 <see cref="SetThreshold"/>，单台失败不影响其它台；
        /// 返回 deviceId → 是否成功，方便上层汇总"哪些台没写进去"。
        /// 【性能提示】72 台连写 + 坏设备会阻塞较久（每台坏设备约一个读超时），
        /// 调用方应在后台线程执行，不要直接放在 UI 线程里。
        ///
        /// 【未连接时的约定（V1.16）】
        /// 如果串口没连上，直接返回"空字典"而不是 72 台全失败——
        /// 这样上层（公共参数窗口）能给出"未连接任何气压表，请先检查通讯连接"
        /// 的明确提示，而不是弹一串"失败 72 台"让人误以为设备全坏了。
        ///
        /// 【与 Demo 对齐（V1.16）】
        /// 每写一台后延时 50ms（参考 ModbusRtuBarometerTest 的 BatchSetThreshold
        /// writeDelayMs=50），让 RS485 总线安静一下，避免 72 台连写帧间隔过密丢帧。
        /// </summary>
        /// <param name="thresholdValue">设备单位阈值（与压力读数同单位同小数位）</param>
        /// <returns>写入结果字典（deviceId → 是否成功；串口未连接时返回空字典）</returns>
        public Dictionary<int, bool> SetAllThresholds(decimal thresholdValue)
        {
            var result = new Dictionary<int, bool>();
            if (_config == null)
            {
                OnError?.Invoke(this, "未连接，请先调用 Connect 方法");
                return result;
            }

            // 【V1.16.1 按需重连】串口未连接时，用户从公共参数窗口保存 → 先尝试重连一次；
            // 再连不上才返回空字典，由上层弹窗提示"气压表未连接，请先连接"。
            // （本方法由 DeviceManager.SetAllBarometerThresholds 调用，批量写期间已暂停采集定时器，
            //   不会与采集线程并发访问串口。）
            if (!_isConnected || _master == null)
            {
                Connect(_config);
            }

            // 串口未连接：直接返回空字典，让上层走"未连接"提示分支
            if (!_isConnected || _master == null)
            {
                OnError?.Invoke(this, "气压表未连接，无法批量写阈值（请先检查串口/驱动）");
                return result;
            }

            for (int i = 1; i <= _config.TotalBarometers; i++)
            {
                result[i] = SetThreshold(i, thresholdValue);

                // 每写一台后让总线安静一小段（与 Demo 对齐），避免帧间隔过密
                System.Threading.Thread.Sleep(50);
            }
            return result;
        }

        private bool IsAlarm(decimal pressureKPa)
        {
            if (_config.AlarmWhenPressureHigherThanThreshold)
            {
                return pressureKPa > _config.AlarmPressureThresholdKPa;
            }

            return pressureKPa < _config.AlarmPressureThresholdKPa;
        }

        private Parity ParseParity(string parity)
        {
            if (string.IsNullOrWhiteSpace(parity)) return Parity.None;
            if (Enum.TryParse(parity, true, out Parity parsed)) return parsed;
            return Parity.None;
        }

        private StopBits ParseStopBits(int stopBits)
        {
            switch (stopBits)
            {
                case 1:
                    return StopBits.One;
                case 2:
                    return StopBits.Two;
                case 15:   // 与 ScannerService 约定一致：15 表示 1.5 停止位
                    return StopBits.OnePointFive;
                default:
                    return StopBits.One;
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
