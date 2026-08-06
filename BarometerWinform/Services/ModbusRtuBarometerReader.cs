using System;
using System.Collections.Generic;
using System.IO.Ports;
using BarometerWinform.Interfaces;
using BarometerWinform.Models;
using NModbus;
using NModbus.Serial;

namespace BarometerWinform.Services
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

                // 1) 创建串口对象并配置参数
                //    这些参数来自 App.config / 通讯设置界面：
                //    - PortName: 例如 COM5
                //    - BaudRate/DataBits/StopBits/Parity: 必须与现场气压表一致
                //    - ReadTimeout/WriteTimeout: 防止串口调用长期卡死
                _serialPort = new SerialPort(config.PortName)
                {
                    BaudRate = config.BaudRate,
                    DataBits = config.DataBits,
                    Parity = ParseParity(config.Parity),
                    StopBits = ParseStopBits(config.StopBits),
                    ReadTimeout = config.SerialReadTimeoutMs,
                    WriteTimeout = config.SerialWriteTimeoutMs
                };

                _serialPort.Open();

                // 2) 通过 NModbus 创建 RTU 主站
                //    说明：NModbus 会在串口上组装 RTU 帧并处理 CRC 校验。
                var factory = new ModbusFactory();
                _master = factory.CreateRtuMaster(_serialPort);
                _master.Transport.ReadTimeout = config.SerialReadTimeoutMs;
                _master.Transport.WriteTimeout = config.SerialWriteTimeoutMs;

                // 3) 标记连接成功
                _isConnected = true;
                return true;
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
                    //    - numberOfPoints：一次读 2 个寄存器（0x0001 压力原始值 + 0x0002 小数位数）
                    //    注意：以 ModbusRtuBarometerTest Demo 实测为准，压力走 Input Register（0x04），
                    //    不是 Holding Register（0x03）——早期实现读 0x0010 是错的（0x0010 实际是阈值寄存器）。
                    registers = _master.ReadInputRegisters((byte)deviceId, _config.BarometerPressureRegisterAddress, 2);
                    if (registers == null || registers.Length < 2) return null;
                }

                // 4) 寄存器值到压力值的转换（以 Demo 为准）
                //    - 压力原始值按有符号 short 解释（0xFFFE → -2，支持负压）
                //    - 小数位数寄存器合法范围 0~4，非法则用默认值（BarometerDefaultDecimalPlaces，默认 1）
                //    - 实际压力 = 有符号原始值 / 10^小数位，再乘以可选缩放系数 BarometerPressureScale
                short rawSigned = (short)registers[0];
                int decimalPos = (registers[1] <= 4) ? registers[1] : _config.BarometerDefaultDecimalPlaces;
                decimal pressurePa = rawSigned / (decimal)Math.Pow(10, decimalPos);
                pressurePa *= _config.BarometerPressureScale;

                var data = new BarometerData
                {
                    DeviceId = deviceId,
                    VacuumPressure = pressurePa,
                    CollectTime = DateTime.Now
                };

                // 5) 在“采集层”做一次最基础的报警判断，用于 UI 先显示 Fault（红色）。
                //    真正的联动输出（关阀/断电）由 DeviceManager 统一处理，避免通讯类里写业务逻辑。
                bool alarm = IsAlarm(pressurePa);
                data.Status = alarm ? DeviceStatus.Fault : DeviceStatus.Idle;
                return data;
            }
            catch (Exception ex)
            {
                // 读失败不抛异常，继续让其它设备有机会读取
                OnError?.Invoke(this, $"设备{deviceId}读取失败: {ex.Message}");
                return null;
            }
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
                OnError?.Invoke(this, "设备未连接");
                return new BarometerData[0];
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
        ///   1. 小数位 = 从设备 0x0002 读到（非法则用 BarometerDefaultDecimalPlaces，默认 1）
        ///   2. 寄存器值 = round(thresholdValue × 10^小数位)，负数按补码写（设备按有符号 short 解释）
        ///   3. 写 WriteSingleRegister(slaveId=deviceId, 0x0010, 寄存器值)
        ///
        /// 【单位提醒】thresholdValue 是"设备单位"（与压力读数同单位同小数位），
        /// 不是软件报警阈值 AlarmPressureThresholdPa。写前务必确认设备单位。
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
                    // 先读 0x0002 拿小数位（与 ReadData 同一套取值规则：合法 0~4，否则默认）
                    ushort[] info = _master.ReadInputRegisters((byte)deviceId, 0x0001, 2);
                    int decimalPos = (info != null && info.Length >= 2 && info[1] <= 4)
                        ? info[1]
                        : _config.BarometerDefaultDecimalPlaces;

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
        /// </summary>
        /// <param name="thresholdValue">设备单位阈值（与压力读数同单位同小数位）</param>
        /// <returns>写入结果字典（deviceId → 是否成功）</returns>
        public Dictionary<int, bool> SetAllThresholds(decimal thresholdValue)
        {
            var result = new Dictionary<int, bool>();
            if (_config == null)
            {
                OnError?.Invoke(this, "未连接，请先调用 Connect 方法");
                return result;
            }

            for (int i = 1; i <= _config.TotalBarometers; i++)
            {
                result[i] = SetThreshold(i, thresholdValue);
            }
            return result;
        }

        private bool IsAlarm(decimal pressurePa)
        {
            if (_config.AlarmWhenPressureHigherThanThreshold)
            {
                return pressurePa > _config.AlarmPressureThresholdPa;
            }

            return pressurePa < _config.AlarmPressureThresholdPa;
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
