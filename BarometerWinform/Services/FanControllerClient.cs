using System;
using System.Net.Sockets;
using BarometerWinform.Interfaces;
using BarometerWinform.Models;
using NModbus;

namespace BarometerWinform.Services
{
    /// <summary>
    /// 冷却送风机通讯实现（Modbus TCP）
    ///
    /// 【来源】
    /// 移植自 ModbusTCPFanControllerTest Demo（该 Demo 已现场实测通过）。
    /// 与 Demo 的区别：Demo 用的是 async/await 异步方法，这里改成 NModbus 同步方法，
    /// 以与现有 ModbusTcpIoController / ModbusRtuBarometerReader 的同步风格保持一致，
    /// 也避免在采集定时器线程里出现 async/await 的复杂性。
    ///
    /// 【寄存器映射】（实测，见 Demo 文档）
    ///   0x0000 组合状态（未使用，忽略）
    ///   0x0001 控制/状态（写：0x0003=定值启动，0x0002=定值停止；读回同值）
    ///   0x0002 当前温度（值/100 = °C）
    ///   0x0003 当前湿度（值/100 = %RH）
    ///   0x0004 温度设定值（值/100 = °C）
    ///   0x0005 湿度设定值（值/100 = %RH）
    ///
    /// 【物理层】
    /// - 传输：TCP/IP（以太网）
    /// - 端口：默认 50000（非标准 502，来自 Demo 实测）
    /// - 从站地址（UnitId）：默认 1
    ///
    /// 【线程安全】
    /// 与 ModbusTcpIoController 相同，用 _syncRoot 锁串行化对主站的所有访问。
    /// 采集线程（DeviceManager 定时器）和 UI 线程（按钮点击）可能并发调用本类，
    /// 锁保证同一时刻只有一个线程在发 Modbus 请求，避免帧交叉。
    ///
    /// 【断线自愈】
    /// 送风机是"可选设备"，现场可能中途断电/断网。
    /// 本类采用"每次操作前检查连接，未连接则自动重连"的策略；
    /// 并用 10 秒重连节流，避免对已断电的设备每秒发起连接导致卡顿。
    /// </summary>
    public class FanControllerClient : IFanController
    {
        /// <summary>
        /// 主站/连接对象的互斥锁（线程安全，见类注释）
        /// </summary>
        private readonly object _syncRoot = new object();

        /// <summary>
        /// 全局配置（来自 App.config / 通讯设置界面）
        /// Connect 时赋值
        /// </summary>
        private DeviceConfig _config;

        /// <summary>
        /// TCP 客户端（负责网络连接）
        /// </summary>
        private TcpClient _client;

        /// <summary>
        /// Modbus 主站（负责组包/解包、发起请求）
        /// </summary>
        private IModbusMaster _master;

        /// <summary>
        /// 连接状态
        /// </summary>
        private bool _isConnected;

        /// <summary>
        /// 上次连接尝试的时间
        /// 用于"重连节流"：设备掉线时不要每秒都去连一次
        /// </summary>
        private DateTime _lastConnectAttempt = DateTime.MinValue;

        /// <summary>
        /// 重连节流间隔（毫秒）
        /// 两次连接尝试之间至少间隔 10 秒，避免对死设备频繁发起连接
        /// </summary>
        private const int ReconnectIntervalMs = 10000;

        public bool IsConnected => _isConnected;

        public event EventHandler<string> OnError;

        /// <summary>
        /// 连接送风机控制屏
        /// 设计约定：不向外抛异常，统一用 OnError 通知上层
        /// </summary>
        public bool Connect(DeviceConfig config)
        {
            _config = config;
            lock (_syncRoot)
            {
                return ConnectInternal();
            }
        }

        /// <summary>
        /// 连接送风机控制屏（实际执行部分）
        /// 必须在 _syncRoot 锁内调用（Connect / EnsureConnected 都会持有锁进入）
        /// </summary>
        private bool ConnectInternal()
        {
            try
            {
                // 0) 先断开旧连接（如果之前连接过），避免重复 Connect 导致句柄泄漏
                Disconnect();

                // 1) 建立 TCP 连接
                _client = new TcpClient();
                _client.SendTimeout = _config.FanTimeoutMs;
                _client.ReceiveTimeout = _config.FanTimeoutMs;

                // 【重要】TcpClient.Connect 是同步方法，且不受上面 Timeout 属性控制
                //（它走的是系统 TCP 连接超时，默认可能长达 ~20 秒）。
                // 如果送风机掉线/网线没插好，直接 Connect 会让启动画面/按钮卡住很久。
                // 这里改用 BeginConnect + WaitOne 实现"手动超时"：
                //   - FanTimeoutMs 内连接成功 → EndConnect 完成连接
                //   - FanTimeoutMs 内没成功 → 抛超时异常，本次连接放弃
                IAsyncResult connectResult = _client.BeginConnect(_config.FanIpAddress, _config.FanPort, null, null);
                if (!connectResult.AsyncWaitHandle.WaitOne(_config.FanTimeoutMs))
                {
                    _client.Close();
                    throw new TimeoutException($"连接送风机超时（{_config.FanIpAddress}:{_config.FanPort}）");
                }
                _client.EndConnect(connectResult);

                // 2) 创建 Modbus 主站（Master）
                var factory = new ModbusFactory();
                _master = factory.CreateMaster(_client);
                _master.Transport.ReadTimeout = _config.FanTimeoutMs;
                _master.Transport.WriteTimeout = _config.FanTimeoutMs;

                // 3) 标记连接成功
                _isConnected = true;
                return true;
            }
            catch (Exception ex)
            {
                // 连接失败：通知上层（会显示在 UI 上），并清理资源
                OnError?.Invoke(this, $"送风机连接失败: {ex.Message}");
                Disconnect();
                return false;
            }
        }

        /// <summary>
        /// 断开连接
        /// 【线程安全】用 _syncRoot 锁保护对 _client/_master 的修改。
        /// 注意：C# 的 lock 在同一线程是可重入的，所以 ConnectInternal 在锁内再调
        /// Disconnect 也不会死锁。
        /// </summary>
        public void Disconnect()
        {
            lock (_syncRoot)
            {
                // 先把状态置为 false，上层看到 IsConnected=false 后不再发请求
                _isConnected = false;
                try
                {
                    if (_client != null)
                    {
                        _client.Close();
                        _client.Dispose();
                    }
                }
                catch
                {
                    // Close/Dispose 在"网线被拔"等场景可能抛异常，这里吞掉
                }
                finally
                {
                    _client = null;
                    _master = null;
                }
            }
        }

        /// <summary>
        /// 确保连接已建立；未连接则尝试（节流后）自动重连
        /// 必须在 _syncRoot 锁内调用
        /// </summary>
        /// <returns>true 表示当前可用（已连接），false 表示不可用</returns>
        private bool EnsureConnected()
        {
            // 已连接且主站存在 → 直接可用
            if (_isConnected && _master != null && _client != null)
            {
                return true;
            }

            // 未连接：先做"重连节流"判断
            // 如果刚尝试过连接失败，10 秒内不再重试，避免对死设备频繁连接
            if ((DateTime.Now - _lastConnectAttempt).TotalMilliseconds < ReconnectIntervalMs)
            {
                return false;
            }

            // 记录本次尝试时间，然后尝试连接
            _lastConnectAttempt = DateTime.Now;
            return Connect(_config);
        }

        /// <summary>
        /// 读取送风机当前状态（状态 + 温度 + 湿度 + 设定值）
        /// 一次批量读取 6 个寄存器（0x0000 ~ 0x0005），减少通讯次数
        /// </summary>
        /// <returns>送风机数据；读取失败返回 null（上层显示"离线"）</returns>
        public FanData ReadStatus()
        {
            // 配置为空或未启用送风机时，直接返回 null
            if (_config == null || !_config.FanEnabled)
            {
                OnError?.Invoke(this, "送风机未启用");
                return null;
            }

            try
            {
                ushort[] values;
                lock (_syncRoot)
                {
                    // 未连接则尝试（节流后）自动重连
                    if (!EnsureConnected()) return null;

                    // 读保持寄存器（功能码 0x03），从 0x0000 一次读 6 个
                    values = _master.ReadHoldingRegisters(_config.FanUnitId, 0x0000, 6);
                }

                // 防御性检查：寄存器数量不足说明设备返回异常
                if (values == null || values.Length < 6) return null;

                // 按实测映射解析（索引对应关系见类注释）：
                // values[0] -> 0x0000（组合状态，未使用，忽略）
                // values[1] -> 0x0001（控制/状态）
                // values[2] -> 0x0002（当前温度，/100 = °C）
                // values[3] -> 0x0003（当前湿度，/100 = %RH）
                // values[4] -> 0x0004（温度设定值，/100 = °C）
                // values[5] -> 0x0005（湿度设定值，/100 = %RH）
                return new FanData
                {
                    RunState = (FanRunState)values[1],
                    Temperature = values[2] / 100.0f,
                    Humidity = values[3] / 100.0f,
                    TempSetpoint = values[4] / 100.0f,
                    HumSetpoint = values[5] / 100.0f,
                    IsOnline = true,
                    CollectTime = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                // 读取失败：断开连接（下次操作会按节流规则自动重连）
                _isConnected = false;
                OnError?.Invoke(this, $"送风机读取失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 定值启动（写入 0x0001 = 0x0003）
        /// 让送风机按控制屏设定的温度运行（厂商自动控温）
        /// </summary>
        public bool StartFixedValue()
        {
            return WriteCommand(0x0003);
        }

        /// <summary>
        /// 定值停止（写入 0x0001 = 0x0002）
        /// </summary>
        public bool Stop()
        {
            return WriteCommand(0x0002);
        }

        /// <summary>
        /// 向控制寄存器 0x0001 写入控制命令（公共内部方法）
        /// </summary>
        /// <param name="command">命令值（0x0003=定值启动，0x0002=定值停止）</param>
        /// <returns>是否发送成功</returns>
        private bool WriteCommand(ushort command)
        {
            // 配置为空或未启用送风机时，直接返回 false
            if (_config == null || !_config.FanEnabled)
            {
                OnError?.Invoke(this, "送风机未启用");
                return false;
            }

            try
            {
                lock (_syncRoot)
                {
                    // 未连接则尝试（节流后）自动重连
                    if (!EnsureConnected()) return false;

                    // 写单个保持寄存器（功能码 0x06）
                    _master.WriteSingleRegister(_config.FanUnitId, 0x0001, command);
                }
                return true;
            }
            catch (Exception ex)
            {
                // 发送失败：断开连接（下次操作自动重连）
                _isConnected = false;
                OnError?.Invoke(this, $"送风机命令发送失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 释放资源（关闭连接）
        /// </summary>
        public void Dispose()
        {
            Disconnect();
        }
    }
}
