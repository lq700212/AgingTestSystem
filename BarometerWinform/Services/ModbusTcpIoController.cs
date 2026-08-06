using System;
using System.Net.Sockets;
using BarometerWinform.Interfaces;
using BarometerWinform.Models;
using NModbus;

namespace BarometerWinform.Services
{
    /// <summary>
    /// IO 控制器通讯实现（Modbus TCP）
    /// 
    /// 适用场景：
    /// - GX-CL140 或类似的 Modbus TCP IO 耦合器
    /// - 上位机作为 Modbus TCP Client（主站），周期性读取 DI/DO，并按业务写 DO
    /// 
    /// 对新手的关键说明：
    /// 1) Modbus TCP 是“请求-响应”模型：设备不会主动推送变化，上位机必须轮询读取。
    /// 2) 这里把 DI/DO 视为“16 点打包成 1 个寄存器”的常见实现：
    ///    - 第 1~16 点 → 第 1 个寄存器的 bit0~bit15
    ///    - 第 17~32 点 → 第 2 个寄存器的 bit0~bit15
    /// 3) 本项目的寄存器区与位序，已依据现场 ModbusTCPTest 实测结果固化（GX-CL140）：
    ///    - DI（输入）：
    ///      - 起始地址：0x1000
    ///      - 读取方式：ReadInputRegisters（功能码 0x04）
    ///      - 位序：从右往左第 1 位为第 1 路（也就是 bit0=第1路，bit15=第16路）
    ///    - DO（输出）：
    ///      - 起始地址：0x2000
    ///      - 读取方式：ReadHoldingRegisters（功能码 0x03）
    ///      - 写入方式：WriteSingleRegister（功能码 0x06）
    ///      - 位序：同 DI（bit0=第1路）
    ///    - 现场 5 个 DQ50P-S（每个 32 路）对应 10 个寄存器：
    ///      - 模块1：0x2000(1~16) + 0x2001(17~32)
    ///      - 模块2：0x2002(33~48) + 0x2003(49~64)
    ///      - 模块3：0x2004(65~80) + 0x2005(81~96)
    ///      - 模块4：0x2006(97~112) + 0x2007(113~128)
    ///      - 模块5：0x2008(129~144) + 0x2009(145~160)
    /// 4) 为了简单直观，这里对单点输出采用“读-改-写”方式修改某一 bit。
    ///    后续如果发现写入频繁导致闪烁，可以优化成“按寄存器批量写”。
    /// </summary>
    public class ModbusTcpIoController : IIoController, IDisposable
    {
        /// <summary>
        /// TCP/主站对象的互斥锁
        /// 
        /// 为什么需要锁：
        /// - 采集线程会同时读取 DI/DO
        /// - 后续如果 UI 上增加“手动开阀/关阀”按钮，很可能会在 UI 线程触发写 DO
        /// - 这样就会出现多线程同时访问 _master 的风险
        /// 
        /// 因此这里统一用 _syncRoot 保证对 _master 的访问串行化。
        /// </summary>
        private readonly object _syncRoot = new object();

        /// <summary>
        /// 全局配置（来自 App.config / 通讯设置界面）
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

        public bool IsConnected => _isConnected;

        public event EventHandler<string> OnError;

        public bool Connect(DeviceConfig config)
        {
            _config = config;
            try
            {
                // 0) 先断开旧连接（如果之前连接过）
                Disconnect();

                // 1) 建立 TCP 连接
                //    注意：TcpClient.Connect 是同步阻塞的，这里放在后台采集线程里调用，避免卡 UI。
                _client = new TcpClient();
                _client.SendTimeout = config.TcpSendTimeoutMs;
                _client.ReceiveTimeout = config.TcpReceiveTimeoutMs;
                _client.Connect(config.PlcAddress, config.PlcPort);

                // 2) 创建 Modbus 主站（Master）
                var factory = new ModbusFactory();
                _master = factory.CreateMaster(_client);
                _master.Transport.ReadTimeout = config.TcpReceiveTimeoutMs;
                _master.Transport.WriteTimeout = config.TcpSendTimeoutMs;

                // 3) 标记连接成功
                _isConnected = true;
                return true;
            }
            catch (Exception ex)
            {
                // Connect 的设计约定：不向外抛异常，统一用 OnError 通知上层
                OnError?.Invoke(this, ex.Message);
                Disconnect();
                return false;
            }
        }

        public void Disconnect()
        {
            // 断开连接时，先把状态置为 false
            _isConnected = false;
            try
            {
                if (_client != null)
                {
                    // Close 会关闭网络连接并释放 Socket 资源
                    _client.Close();
                }
            }
            catch
            {
            }
            finally
            {
                _client = null;
                _master = null;
            }
        }

        public bool ReadInput(int inputId)
        {
            if (!_isConnected || _config == null || _master == null)
            {
                OnError?.Invoke(this, "设备未连接");
                return false;
            }

            if (inputId < 1 || inputId > _config.TotalInputs)
            {
                OnError?.Invoke(this, $"无效的输入点编号: {inputId}");
                return false;
            }

            try
            {
                // inputId 是“内部连续编号”（1~TotalInputs）
                // 先换算为 0 基下标 bitIndex，然后再计算寄存器地址与位序
                int bitIndex = inputId - 1;
                ushort regAddress = (ushort)(_config.IoInputRegisterStartAddress + (bitIndex / 16));
                int bit = bitIndex % 16;

                ushort value;
                lock (_syncRoot)
                {
                    // 读取 1 个输入寄存器（Input Register，功能码 0x04）
                    ushort[] regs = _master.ReadInputRegisters(_config.IoUnitId, regAddress, 1);
                    if (regs == null || regs.Length < 1) return false;
                    value = regs[0];
                }

                // 用位运算取某一位：
                // - (1 << bit) 生成掩码，例如 bit=0 => 0x0001，bit=15 => 0x8000
                // - value & mask != 0 表示该 bit 为 1
                //
                // 【已现场确认（来自 ModbusTCPTest 实测）】
                // - bit0 对应“第 1 路输入”，bit15 对应“第 16 路输入”
                // - 因此 inputId=1 对应 reg=0x1000, bit=0
                //
                // InvertInputs 用于兼容少数现场“低有效/高有效”逻辑与寄存器 bit 值不一致的情况：
                // - false：bit=1 认为输入 ON（默认）
                // - true：逻辑取反（把 bit=0 当成 ON）
                bool rawState = (value & (1 << bit)) != 0;
                return _config.InvertInputs ? !rawState : rawState;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex.Message);
                return false;
            }
        }

        public bool[] ReadAllInputs()
        {
            if (!_isConnected || _config == null || _master == null)
            {
                OnError?.Invoke(this, "设备未连接");
                return new bool[0];
            }

            try
            {
                // 例如：72 路输入 → 需要 5 个寄存器（(72+15)/16 = 5）
                int regCount = (_config.TotalInputs + 15) / 16;
                ushort[] regs;
                lock (_syncRoot)
                {
                    // 一次性批量读取多个寄存器，减少网络往返次数
                    regs = _master.ReadInputRegisters(_config.IoUnitId, _config.IoInputRegisterStartAddress, (ushort)regCount);
                }

                if (regs == null) return new bool[0];

                var result = new bool[_config.TotalInputs];
                for (int i = 0; i < _config.TotalInputs; i++)
                {
                    // i 是 0 基通道下标：
                    // - regIndex：落在哪个寄存器（每 16 路一个寄存器）
                    // - bit：寄存器内的 bit 位
                    int regIndex = i / 16;
                    int bit = i % 16;
                    if (regIndex >= regs.Length) break;
                    bool rawState = (regs[regIndex] & (1 << bit)) != 0;
                    result[i] = _config.InvertInputs ? !rawState : rawState;
                }

                return result;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex.Message);
                return new bool[0];
            }
        }

        /// <summary>
        /// 【备用通道映射】把物理通道 (regAddress, bit) 重定向到备用通道。
        ///
        /// 现场某个 DQ 通道烧毁 / 电压不足后，把该通道信号改写到备用通道。
        /// 业务侧输出点编号（outputId）完全不变，只是这里把"物理寄存器 + bit"换了位置。
        /// 总开关 IoBackupChannelMappingEnabled 关闭时，原样返回（多数工作台行为不变）。
        /// </summary>
        /// <param name="regAddress">输入源寄存器地址；输出映射后的寄存器地址（可能被改写）</param>
        /// <param name="bit">输入源通道号（0~15）；输出映射后的通道号（可能被改写）</param>
        private void MapOutputChannel(ref ushort regAddress, ref int bit)
        {
            if (!_config.IoBackupChannelMappingEnabled || _config.IoBackupChannelMappings == null)
                return;

            foreach (var remap in _config.IoBackupChannelMappings)
            {
                if (remap.SourceRegister == regAddress && remap.SourceChannel == bit)
                {
                    regAddress = remap.TargetRegister;
                    bit = remap.TargetChannel;
                    return; // 一个源通道只会被映射一次
                }
            }
        }

        public void WriteOutput(int outputId, bool state)
        {
            if (!_isConnected || _config == null || _master == null)
            {
                OnError?.Invoke(this, "设备未连接");
                return;
            }

            // outputId 是“内部连续编号”（默认 73~216）
            // outputStart = TotalInputs + 1，便于换算成 0 基下标
            int outputStart = _config.TotalInputs + 1;
            int outputEnd = _config.TotalInputs + _config.TotalOutputs;

            if (outputId < outputStart || outputId > outputEnd)
            {
                OnError?.Invoke(this, $"无效的输出点编号: {outputId}（合法范围 {outputStart}-{outputEnd}）");
                return;
            }

            try
            {
                bool outputState = _config.InvertOutputs ? !state : state;
                int bitIndex = outputId - outputStart;
                ushort regAddress = (ushort)(_config.IoOutputRegisterStartAddress + (bitIndex / 16));
                int bit = bitIndex % 16;

                // 【备用通道映射】烧毁通道 → 备用通道（开关关闭时原样不动）
                MapOutputChannel(ref regAddress, ref bit);

                lock (_syncRoot)
                {
                    // 读-改-写：先把所在寄存器的 16bit 状态读出来，再只修改其中 1 个 bit
                    // 这样就不会误伤同一个寄存器里其它通道的状态。
                    ushort[] currentRegs = _master.ReadHoldingRegisters(_config.IoUnitId, regAddress, 1);
                    ushort current = (currentRegs != null && currentRegs.Length > 0) ? currentRegs[0] : (ushort)0;

                    ushort mask = (ushort)(1 << bit);
                    ushort newValue = outputState ? (ushort)(current | mask) : (ushort)(current & ~mask);

                    // 写单寄存器（功能码 0x06）
                    // - 写入的是 16bit 的 newValue，其中只有一个 bit 被改变
                    // - 其它 bit 保持原状
                    //
                    // 【已现场确认（来自 ModbusTCPTest 实测）】
                    // - GX-CL140 + DQ50P-S 输出模块：DO 区域可用 Holding Register 写入（0x06）控制指示灯/通道
                    // - 起始地址默认 0x2000，每个寄存器 16 路，bit0=第1路
                    _master.WriteSingleRegister(_config.IoUnitId, regAddress, newValue);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex.Message);
            }
        }

        public void WriteOutputs(int[] outputIds, bool[] states)
        {
            if (!_isConnected)
            {
                OnError?.Invoke(this, "设备未连接");
                return;
            }

            if (outputIds == null || states == null)
            {
                OnError?.Invoke(this, "参数不能为空");
                return;
            }

            if (outputIds.Length != states.Length)
            {
                OnError?.Invoke(this, "输出点编号和状态数量不一致");
                return;
            }

            for (int i = 0; i < outputIds.Length; i++)
            {
                WriteOutput(outputIds[i], states[i]);
            }
        }

        public bool ReadOutput(int outputId)
        {
            if (!_isConnected || _config == null || _master == null)
            {
                OnError?.Invoke(this, "设备未连接");
                return false;
            }

            int outputStart = _config.TotalInputs + 1;
            int outputEnd = _config.TotalInputs + _config.TotalOutputs;

            if (outputId < outputStart || outputId > outputEnd)
            {
                OnError?.Invoke(this, $"无效的输出点编号: {outputId}（合法范围 {outputStart}-{outputEnd}）");
                return false;
            }

            try
            {
                int bitIndex = outputId - outputStart;
                ushort regAddress = (ushort)(_config.IoOutputRegisterStartAddress + (bitIndex / 16));
                int bit = bitIndex % 16;

                // 【备用通道映射】烧毁通道 → 备用通道（开关关闭时原样不动），
                // 读取也要跟随映射后的物理位置，否则读回来的是烧毁通道的旧值/空值。
                MapOutputChannel(ref regAddress, ref bit);

                ushort value;
                lock (_syncRoot)
                {
                    // 读取保持寄存器（Holding Register，功能码 0x03）
                    ushort[] regs = _master.ReadHoldingRegisters(_config.IoUnitId, regAddress, 1);
                    if (regs == null || regs.Length < 1) return false;
                    value = regs[0];
                }

                bool rawState = (value & (1 << bit)) != 0;
                return _config.InvertOutputs ? !rawState : rawState;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex.Message);
                return false;
            }
        }

        public bool[] ReadAllOutputs()
        {
            if (!_isConnected || _config == null || _master == null)
            {
                OnError?.Invoke(this, "设备未连接");
                return new bool[0];
            }

            try
            {
                // 例如：144 路输出 → 需要 9 个寄存器（(144+15)/16 = 9）
                int regCount = (_config.TotalOutputs + 15) / 16;

                // 【备用通道映射】映射目标可能落在业务输出范围之外（如 0x2009 只用于备用），
                // 把批量读取范围扩到能覆盖所有映射目标，保证一次读到全部物理通道的真实状态。
                if (_config.IoBackupChannelMappingEnabled && _config.IoBackupChannelMappings != null)
                {
                    foreach (var remap in _config.IoBackupChannelMappings)
                    {
                        int need = (remap.TargetRegister - _config.IoOutputRegisterStartAddress) + 1;
                        if (need > regCount) regCount = need;
                    }
                }

                ushort[] regs;
                lock (_syncRoot)
                {
                    // 一次性批量读取多个寄存器
                    regs = _master.ReadHoldingRegisters(_config.IoUnitId, _config.IoOutputRegisterStartAddress, (ushort)regCount);
                }

                if (regs == null) return new bool[0];

                var result = new bool[_config.TotalOutputs];
                for (int i = 0; i < _config.TotalOutputs; i++)
                {
                    int regIndex = i / 16;
                    int bit = i % 16;
                    ushort regAddress = (ushort)(_config.IoOutputRegisterStartAddress + regIndex);

                    // 【备用通道映射】按通道重定向后，从已读到的块里取目标寄存器的对应位
                    MapOutputChannel(ref regAddress, ref bit);
                    int mappedRegIndex = regAddress - _config.IoOutputRegisterStartAddress;
                    if (mappedRegIndex < 0 || mappedRegIndex >= regs.Length) break;

                    bool rawState = (regs[mappedRegIndex] & (1 << bit)) != 0;
                    result[i] = _config.InvertOutputs ? !rawState : rawState;
                }

                return result;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex.Message);
                return new bool[0];
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
