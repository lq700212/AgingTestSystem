using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using BarometerWinform.Models;
using NModbus;
using Sunny.UI;

namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 通讯测试窗体（IO 耦合器 DO 输出通道测试）—— SunnyUI 界面版
    ///
    /// 【用途】
    ///   现场调试/开发阶段验证 GX-CL140 耦合器 DO 输出接线。通过主窗体"关于"下拉菜单的
    ///   "通讯测试"进入（仅技术员及以上权限可见，V1.20）。
    ///
    /// 【界面】（整体使用 SunnyUI 控件，风格与主程序/系统设置一致，V1.21 重构）
    ///   - 窗体基类：Sunny.UI.UIForm（蓝色标题栏，ShowTitle）
    ///   - 顶部状态条（pnlHeader）：UILedBulb 连接指示灯 + UILabel 连接状态
    ///   - 中部页签（UITabControl + UIPage）：
    ///     · Tab1"负压开关测试"：72 路真空电磁阀输出（Y000~Y107），寄存器 0x2000~0x2004
    ///     · Tab2"载台上电测试"：72 路载台上电（继电器）输出（Y110~Y217），寄存器 0x2004~0x2008
    ///   - 底部按钮栏（pnlBottom）：UIButton 连接测试/全部关闭/读取状态/关闭窗口 + UITextBox 日志
    ///   - 每个页签内的 9×8 = 72 个圆形灯按钮由 ChannelGrid 动态生成（自绘 CircleButton）
    ///
    /// 【逻辑】移植自测试工程 ModbusTcpIoControllerTest：
    ///   - 载台上电测试 ← PowerOnTestForm（按钮网格控制每一路 ON/OFF）
    ///   - 负压开关测试 ← MainForm.btnWriteDatas（批量扫描每个通道），并升级为
    ///     与载台上电测试一致的"点击按钮控制该通道亮起"交互
    ///
    /// 【通讯库】本窗体用生产工程已有的 NModbus（与 ModbusTcpIoController 同源），
    ///   自建独立连接读写寄存器，不影响采集线程正在使用的连接（Modbus TCP 允许多连接）。
    ///
    /// 【寄存器与通道】
    ///   - DO 起始 0x2000，每寄存器 16 路，bit0=第1路（GX-CL140 + DQ50P-S 已现场确认）
    ///   - 负压阀 72 路：deviceId 1~72 → 0x2000~0x2004（0x2004 只用低字节，Y100~Y107）
    ///   - 载台电 72 路：deviceId 1~72 → 0x2004(高字节 Y110~Y117) ~ 0x2008
    ///   - 0x2004 两测试共享：低字节=负压阀 Y100~Y107，高字节=载台电 Y110~Y117
    ///     因此写入 0x2004 时采用"读-改-写"，只动本 Tab 拥有的字节，不覆盖对方
    ///     （各自用 OwnedMask 标记拥有的位，写前读回现值再合并）。
    ///
    /// 【备用通道映射】复用生产工程配置（DeviceConfig.IoBackupChannelMappingEnabled /
    ///   IoBackupChannelMappings），逻辑与 ModbusTcpIoController 一致；默认关闭。
    ///   V1.21 增强：点击被映射的通道时，先弹出提示（UIMessageBox）告知该通道已映射、
    ///   实际输出通道是哪个寄存器第几路，避免现场误以为原通道还在工作。
    /// </summary>
    public partial class CommunicationTestForm : UIForm
    {
        /// <summary>设备配置（读取 PlcAddress/PlcPort/IoUnitId/超时/备用映射）</summary>
        private readonly DeviceConfig _config;

        // ===================== Modbus 连接 =====================

        /// <summary>TCP 客户端（独立连接，与采集线程的连接互不影响）</summary>
        private TcpClient _client;

        /// <summary>Modbus 主站（负责组包/解包、发起请求）</summary>
        private IModbusMaster _master;

        /// <summary>是否已连接</summary>
        private bool _connected;

        /// <summary>目标设备地址（来自配置）</summary>
        private readonly string _host;

        /// <summary>Modbus TCP 端口（默认 502）</summary>
        private readonly int _port;

        /// <summary>从站地址（耦合器 UnitId，默认 1）</summary>
        private readonly byte _unitId;

        // ===================== 两个测试网格 =====================

        /// <summary>负压开关测试网格（72 路真空电磁阀 Y000~Y107）</summary>
        private readonly ChannelGrid _vacuumGrid;

        /// <summary>载台上电测试网格（72 路载台上电 Y110~Y217）</summary>
        private readonly ChannelGrid _carrierGrid;

        /// <summary>构造函数：读取配置的耦合器地址，创建两个测试网格</summary>
        /// <param name="config">设备配置（PlcAddress / PlcPort / IoUnitId / 备用映射等）</param>
        public CommunicationTestForm(DeviceConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _host = string.IsNullOrWhiteSpace(config.PlcAddress) ? "192.168.1.20" : config.PlcAddress;
            _port = config.PlcPort > 0 ? config.PlcPort : 502;
            _unitId = config.IoUnitId > 0 ? config.IoUnitId : (byte)1;

            InitializeComponent();

            // 初始状态：未连接（更新顶部 LED 与状态文字）
            SetConnected(false);

            // 创建两个测试网格（各自建自己的 9×8 圆形灯按钮）
            _vacuumGrid = new ChannelGrid(this, panelGridVacuum,
                RegAddresses: new[] { 0x2000, 0x2001, 0x2002, 0x2003, 0x2004, 0x2009 },
                RowToRegIndex: new[] { 0, 0, 1, 1, 2, 2, 3, 3, 4 },
                RowByteDesc: new[] { "低字节", "高字节", "低字节", "高字节", "低字节", "高字节", "低字节", "高字节", "低字节" },
                RowBitValues: BuildBytePairRowBits(row0High: false),
                RowIoNames: BuildVacuumIoNames(),
                OwnedMask: new[] { 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0x00FF, 0x0000 });

            _carrierGrid = new ChannelGrid(this, panelGridPowerOn,
                RegAddresses: new[] { 0x2004, 0x2005, 0x2006, 0x2007, 0x2008, 0x2009 },
                RowToRegIndex: new[] { 0, 1, 1, 2, 2, 3, 3, 4, 4 },
                RowByteDesc: new[] { "高字节", "低字节", "高字节", "低字节", "高字节", "低字节", "高字节", "低字节", "高字节" },
                RowBitValues: BuildBytePairRowBits(row0High: true),
                RowIoNames: BuildCarrierIoNames(),
                OwnedMask: new[] { 0xFF00, 0xFFFF, 0xFFFF, 0xFFFF, 0xFFFF, 0x0000 });
        }

        // ===================== 连接状态指示 =====================

        /// <summary>
        /// 更新顶部连接状态指示（SunnyUI LED 指示灯 + 状态文字）
        /// 连接成功 → 绿灯"已连接"；断开/失败 → 红灯"未连接"。
        /// </summary>
        /// <param name="connected">true=已连接，false=未连接</param>
        private void SetConnected(bool connected)
        {
            _connected = connected;

            // LED 颜色与亮灭：已连接=亮绿灯，未连接=亮红灯
            ledStatus.Color = connected ? Color.Lime : Color.FromArgb(230, 80, 80);
            ledStatus.On = true;

            // 状态文字与颜色
            lblStatus.Text = connected ? "已连接" : "未连接";
            lblStatus.ForeColor = connected
                ? Color.FromArgb(0, 150, 80)      // 连接：绿色
                : Color.FromArgb(30, 80, 160);    // 未连接：深蓝
        }

        // ===================== 通道映射表生成 =====================

        /// <summary>
        /// 生成 9 行 × 8 列的位值表（每行 8 位位于同一寄存器的同一字节）。
        /// 低字节 0x0001~0x0080，高字节 0x0100~0x8000。
        /// </summary>
        /// <param name="row0High">第1排(索引0)是否用高字节：负压阀=false(第1排为低字节)，载台电=true(第1排为高字节)。</param>
        private static int[,] BuildBytePairRowBits(bool row0High)
        {
            var bits = new int[9, 8];
            int[] low = { 0x0001, 0x0002, 0x0004, 0x0008, 0x0010, 0x0020, 0x0040, 0x0080 };
            int[] high = { 0x0100, 0x0200, 0x0400, 0x0800, 0x1000, 0x2000, 0x4000, 0x8000 };
            for (int r = 0; r < 9; r++)
            {
                bool highByte = (row0High) ? (r % 2 == 0) : (r % 2 == 1);
                int[] src = highByte ? high : low;
                for (int c = 0; c < 8; c++) bits[r, c] = src[c];
            }
            return bits;
        }

        /// <summary>负压阀 IO 编号（Y000~Y107，8 进制编址：Y000-Y007/Y010-Y017/.../Y100-Y107）</summary>
        private static string[,] BuildVacuumIoNames()
        {
            var names = new string[9, 8];
            for (int r = 0; r < 9; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    // 行 r（0~8）、列 c（0~7）：Y 地址 = r*8 + c（十进制），转 8 进制显示
                    int addr = r * 8 + c;
                    names[r, c] = "Y" + ToOctal3(addr);
                }
            }
            return names;
        }

        /// <summary>载台电 IO 编号（Y110~Y217）</summary>
        private static string[,] BuildCarrierIoNames()
        {
            var names = new string[9, 8];
            for (int r = 0; r < 9; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    // 载台电 Y110~Y217：相对 Y110 的偏移 = r*8 + c
                    int addr = 0x48 + r * 8 + c;   // 0x48 = 八进制 110
                    names[r, c] = "Y" + ToOctal3(addr);
                }
            }
            return names;
        }

        /// <summary>把 0~511 的十进制地址转成 3 位八进制字符串（如 0→000，8→010，0x48→110）</summary>
        private static string ToOctal3(int addr)
        {
            string s = Convert.ToString(addr, 8).PadLeft(3, '0');
            return s;
        }

        // ===================== 连接 / 断开 =====================

        /// <summary>
        /// 与 IO 耦合器建立 Modbus TCP 连接（独立连接）。
        /// 连接超时采用手动 BeginConnect + WaitOne，避免 IP 填错时界面长时间卡住。
        /// 连接成功后更新顶部 LED 状态指示。
        /// </summary>
        private void Connect()
        {
            try
            {
                if (_connected)
                {
                    Disconnect();
                }

                _client = new TcpClient();
                int timeout = _config.TcpReceiveTimeoutMs > 0 ? _config.TcpReceiveTimeoutMs : 3000;
                _client.SendTimeout = timeout;
                _client.ReceiveTimeout = timeout;

                IAsyncResult connectResult = _client.BeginConnect(_host, _port, null, null);
                if (!connectResult.AsyncWaitHandle.WaitOne(timeout))
                {
                    _client.Close();
                    _client.Dispose();
                    _client = null;
                    SetConnected(false);
                    AppendLog($"[错误] 连接 {_host}:{_port} 超时（{timeout}ms），请检查 IP/网线");
                    UIMessageBox.Show($"连接 {_host}:{_port} 超时（{timeout}ms）。", "错误",
                        UIStyle.Red, UIMessageBoxButtons.OK, true, 0);
                    return;
                }
                _client.EndConnect(connectResult);

                var factory = new ModbusFactory();
                _master = factory.CreateMaster(_client);
                _master.Transport.ReadTimeout = timeout;
                _master.Transport.WriteTimeout = timeout;

                SetConnected(true);
                AppendLog($"[连接] 已连接到 {_host}:{_port}（从站 {_unitId}）");
                UIMessageBox.Show("连接成功！", "提示",
                    UIStyle.Green, UIMessageBoxButtons.OK, true, 0);
            }
            catch (Exception ex)
            {
                SetConnected(false);
                string msg = $"连接失败: {ex.Message}";
                if (ex.InnerException != null) msg += $"\n内部异常: {ex.InnerException.Message}";
                AppendLog("[错误] " + msg);
                UIMessageBox.Show(msg, "错误",
                    UIStyle.Red, UIMessageBoxButtons.OK, true, 0);
            }
        }

        /// <summary>断开 Modbus 连接并释放资源，同步更新顶部状态指示</summary>
        private void Disconnect()
        {
            SetConnected(false);
            try
            {
                if (_client != null)
                {
                    _client.Close();
                }
            }
            catch
            {
                // 断开时忽略清理异常
            }
            finally
            {
                _client = null;
                _master = null;
            }
        }

        /// <summary>窗体关闭时断开连接，释放 socket</summary>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Disconnect();
            base.OnFormClosed(e);
        }

        // ===================== 寄存器读写 =====================

        /// <summary>
        /// 写入某个测试网格的指定寄存器（读-改-写，避免覆盖 0x2004 上对方测试拥有的字节）。
        /// </summary>
        /// <param name="grid">目标测试网格</param>
        /// <param name="regIndex">寄存器索引（0~4 对应 RegAddresses；5=备用映射目标 0x2009）</param>
        /// <param name="triggerRow">触发本次写入的排索引（0~8），仅用于日志显示</param>
        public void WriteRegister(ChannelGrid grid, int regIndex, int triggerRow)
        {
            int addr = grid.RegAddresses[regIndex];
            int val = grid.CurrentRegValues[regIndex];

            if (!_connected || _master == null)
            {
                AppendLog($"[警告] 未连接，无法写入。请先点击“连接测试”。(由第 {triggerRow + 1} 排触发，0x{addr:X4} = 0x{val:X4})");
                return;
            }

            try
            {
                // 读-改-写：只改写本网格在寄存器里拥有的位，保留其它位（如 0x2004 上对方的字节）
                int ownedMask = grid.OwnedMask[regIndex];
                ushort writeValue;
                if (ownedMask == 0xFFFF)
                {
                    // 整寄存器归本网格所有，直接写
                    writeValue = (ushort)(val & 0xFFFF);
                }
                else
                {
                    ushort[] cur = _master.ReadHoldingRegisters(_unitId, (ushort)addr, 1);
                    ushort current = (cur != null && cur.Length > 0) ? cur[0] : (ushort)0;
                    writeValue = (ushort)((current & ~ownedMask) | (val & ownedMask));
                }

                _master.WriteSingleRegister(_unitId, (ushort)addr, writeValue);

                // 备用映射目标寄存器一并下发（开关关闭时自动跳过）
                if (_config.IoBackupChannelMappingEnabled)
                {
                    _master.WriteSingleRegister(_unitId, (ushort)grid.RegAddresses[5], (ushort)grid.CurrentRegValues[5]);
                }

                AppendLog($"[写入] 由第 {triggerRow + 1} 排触发  0x{addr:X4} = 0x{writeValue:X4}");
            }
            catch (Exception ex)
            {
                AppendLog($"[错误] 写入 0x{addr:X4} 失败: {ex.Message}");
                UIMessageBox.Show($"写入 0x{addr:X4} 失败:\n{ex.Message}", "错误",
                    UIStyle.Red, UIMessageBoxButtons.OK, true, 0);
            }
        }

        /// <summary>
        /// 读取 DO 区全部 10 个寄存器（0x2000~0x2009），更新两个测试网格的按钮状态与标签。
        /// 一次性读回，两测试共享的 0x2004 各自按自己的字节解析，互不干扰。
        /// </summary>
        private void ReadAllStatus()
        {
            if (!_connected || _master == null)
            {
                UIMessageBox.Show("请先点击“连接测试”建立通讯！", "提示",
                    UIStyle.Orange, UIMessageBoxButtons.OK, true, 0);
                return;
            }

            try
            {
                // 读 10 个保持寄存器（0x2000~0x2009；第 10 个 0x2009 为备用映射目标）
                ushort[] regs = _master.ReadHoldingRegisters(_unitId, 0x2000, 10);
                if (regs == null || regs.Length < 10)
                {
                    AppendLog("[错误] 读取失败：返回寄存器数量不足");
                    return;
                }

                // EasyModbus/NModbus 对 bit15=1 的值可能符号扩展成 32 位负数，统一 & 0xFFFF 还原
                var values = new int[10];
                for (int i = 0; i < 10; i++) values[i] = regs[i] & 0xFFFF;

                _vacuumGrid.SetButtonsFromRegisters(values);
                _carrierGrid.SetButtonsFromRegisters(values);

                AppendLog(string.Format(
                    "[读取] 0x2000=0x{0:X4} 0x2001=0x{1:X4} 0x2002=0x{2:X4} 0x2003=0x{3:X4} 0x2004=0x{4:X4} " +
                    "0x2005=0x{5:X4} 0x2006=0x{6:X4} 0x2007=0x{7:X4} 0x2008=0x{8:X4} 0x2009=0x{9:X4}",
                    values[0], values[1], values[2], values[3], values[4],
                    values[5], values[6], values[7], values[8], values[9]));
            }
            catch (Exception ex)
            {
                AppendLog("[错误] 读取失败: " + ex.Message);
                UIMessageBox.Show("读取失败: " + ex.Message, "错误",
                    UIStyle.Red, UIMessageBoxButtons.OK, true, 0);
            }
        }

        /// <summary>全部关闭：0x2000~0x2009 全写 0，两个测试网格全部按钮置 OFF（现场应急用）</summary>
        private void AllOff()
        {
            // 1) 本地状态全部清零
            _vacuumGrid.ClearAll();
            _carrierGrid.ClearAll();

            // 2) 把 10 个寄存器全部写 0
            if (!_connected || _master == null)
            {
                AppendLog("[警告] 未连接，仅清除本地状态。请先连接再写入。");
                return;
            }

            for (int i = 0; i < 10; i++)
            {
                try
                {
                    _master.WriteSingleRegister(_unitId, (ushort)(0x2000 + i), 0x0000);
                }
                catch (Exception ex)
                {
                    AppendLog($"[错误] 写 0x{0x2000 + i:X4} 失败: {ex.Message}");
                }
            }
            AppendLog("[关闭] 0x2000~0x2009 全部清零");
        }

        // ===================== 备用通道映射（复用配置） =====================

        /// <summary>查询某物理通道是否被映射，返回映射后的目标 (寄存器, 通道)；未启用/未匹配时原样返回</summary>
        public (int reg, int bit) MapChannel(int reg, int bit)
        {
            if (!_config.IoBackupChannelMappingEnabled || _config.IoBackupChannelMappings == null)
                return (reg, bit);

            foreach (var m in _config.IoBackupChannelMappings)
            {
                if (m.SourceRegister == reg && m.SourceChannel == bit)
                    return (m.TargetRegister, m.TargetChannel);
            }
            return (reg, bit);
        }

        /// <summary>该物理通道是否为某个映射的源（写整寄存器时把源位剔除）</summary>
        public bool IsRemapSource(int reg, int bit)
        {
            if (!_config.IoBackupChannelMappingEnabled || _config.IoBackupChannelMappings == null)
                return false;

            foreach (var m in _config.IoBackupChannelMappings)
            {
                if (m.SourceRegister == reg && m.SourceChannel == bit) return true;
            }
            return false;
        }

        /// <summary>
        /// 弹窗说明某个通道已做备用通道映射，并告知实际输出通道（V1.21 新增）。
        /// 在用户点击被映射的通道时调用（仅当映射启用且该通道命中映射表时）。
        /// 使用 SunnyUI UIMessageBox 展示，并在日志中追加一条映射记录。
        /// </summary>
        /// <param name="ioName">UI 上显示的 IO 编号（如 Y000）</param>
        /// <param name="srcReg">源寄存器地址</param>
        /// <param name="srcCh">源通道号（0~15）</param>
        /// <param name="dstReg">实际输出寄存器地址</param>
        /// <param name="dstCh">实际输出通道号（0~15）</param>
        public void ShowRemapNotice(string ioName, int srcReg, int srcCh, int dstReg, int dstCh)
        {
            string msg = string.Format(
                "通道 {0}（寄存器 0x{1:X4} 第 {2} 通道）已做备用通道映射。\n\n" +
                "实际输出通道：寄存器 0x{3:X4} 第 {4} 通道（第 {5} 路）。",
                ioName, srcReg, srcCh + 1, dstReg, dstCh + 1, dstCh + 1);
            UIMessageBox.Show(msg, "通道映射提示",
                UIStyle.Blue, UIMessageBoxButtons.OK, true, 0);
            AppendLog($"[映射] 通道 {ioName}（0x{srcReg:X4} 通道{srcCh + 1}）→ 实际输出 0x{dstReg:X4} 通道{dstCh + 1}");
        }

        /// <summary>由位值反推通道号：0x0001→0，0x0002→1，0x0100→8，0x8000→15</summary>
        public static int ChannelOf(int bitValue)
        {
            int ch = 0;
            int v = bitValue;
            while (v > 1) { v >>= 1; ch++; }
            return ch;
        }

        // ===================== 底部控制按钮 =====================

        private void btnConnect_Click(object sender, EventArgs e)
        {
            Connect();
        }

        private void btnAllOff_Click(object sender, EventArgs e)
        {
            AllOff();
        }

        private void btnReadStatus_Click(object sender, EventArgs e)
        {
            ReadAllStatus();
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        // ===================== 日志输出 =====================

        /// <summary>
        /// 把一条日志追加到底部 txtLog（SunnyUI UITextBox），并加时间戳，最多保留 200 行。
        /// 自动滚动到底部，方便现场随时查看最近操作。
        /// </summary>
        public void AppendLog(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            if (this.txtLog.InvokeRequired)
            {
                this.txtLog.Invoke(new Action<string>(AppendLog), line);
                return;
            }

            this.txtLog.AppendText(line + Environment.NewLine);
            this.txtLog.ScrollToCaret();

            const int maxLines = 200;
            var lines = this.txtLog.Lines;
            if (lines.Length > maxLines)
            {
                int skip = lines.Length - maxLines;
                var kept = new string[maxLines];
                Array.Copy(lines, skip, kept, 0, maxLines);
                this.txtLog.Lines = kept;
            }
        }

        // ========================================================================
        //  通道网格类：管理某一个测试（9 行 × 8 列圆形灯按钮）的映射表、按钮、
        //  寄存器值与行标签，以及点击写入逻辑。两个测试各自持有一个实例。
        // ========================================================================
        public sealed class ChannelGrid
        {
            private readonly CommunicationTestForm _owner;

            /// <summary>承载本网格的容器（SunnyUI UIPanel，白色背景）</summary>
            private readonly UIPanel _panel;

            /// <summary>本网格的寄存器地址（前 5 个为业务寄存器，最后 1 个为备用映射目标 0x2009）</summary>
            public int[] RegAddresses { get; }

            /// <summary>每一排对应的寄存器下标（指向 RegAddresses 0~4）</summary>
            public int[] RowToRegIndex { get; }

            /// <summary>每一排使用的字节说明（仅用于行标签显示）</summary>
            public string[] RowByteDesc { get; }

            /// <summary>每一排 8 个按钮的位值</summary>
            public int[,] RowBitValues { get; }

            /// <summary>每个按钮显示的 IO 编号（Y110 等）</summary>
            public string[,] RowIoNames { get; }

            /// <summary>每个寄存器里本网格拥有的位掩码（用于读-改-写，避免覆盖对方测试的位）</summary>
            public int[] OwnedMask { get; }

            /// <summary>9×8 圆形灯按钮</summary>
            public CircleButton[,] Buttons { get; private set; }

            /// <summary>各寄存器当前值（按位 OR 累积；[5] 为备用映射目标 0x2009）</summary>
            public int[] CurrentRegValues { get; private set; }

            /// <summary>行标签（显示排号/寄存器/字节/当前值）</summary>
            public Label[] RowLabels { get; private set; }

            /// <summary>
            /// 构造一个通道网格：在指定容器内生成 9×8 圆形灯按钮 + 行标签。
            /// </summary>
            /// <param name="owner">所属窗体（用于写入寄存器、映射查询、日志）</param>
            /// <param name="panel">承载网格的容器</param>
            /// <param name="RegAddresses">寄存器地址表（含备用映射目标）</param>
            /// <param name="RowToRegIndex">每排对应的寄存器下标</param>
            /// <param name="RowByteDesc">每排字节说明</param>
            /// <param name="RowBitValues">每排 8 个按钮的位值</param>
            /// <param name="RowIoNames">每排按钮显示的 IO 编号</param>
            /// <param name="OwnedMask">每寄存器本网格拥有的位掩码</param>
            public ChannelGrid(
                CommunicationTestForm owner,
                UIPanel panel,
                int[] RegAddresses,
                int[] RowToRegIndex,
                string[] RowByteDesc,
                int[,] RowBitValues,
                string[,] RowIoNames,
                int[] OwnedMask)
            {
                _owner = owner;
                _panel = panel;
                this.RegAddresses = RegAddresses;
                this.RowToRegIndex = RowToRegIndex;
                this.RowByteDesc = RowByteDesc;
                this.RowBitValues = RowBitValues;
                this.RowIoNames = RowIoNames;
                this.OwnedMask = OwnedMask;

                Buttons = new CircleButton[9, 8];
                CurrentRegValues = new int[6];
                RowLabels = new Label[9];

                BuildButtonGrid();
                RefreshRowLabels();
            }

            /// <summary>动态生成 9 行 × 8 列的圆形灯按钮与行标签</summary>
            private void BuildButtonGrid()
            {
                const int buttonSize = 56;
                const int gapX = 8;
                const int gapY = 14;
                const int rowLabelWidth = 150;
                const int gridLeft = 12;
                const int gridTop = 6;

                // 顶部列号（1~8）
                for (int c = 0; c < 8; c++)
                {
                    Label colHeader = new Label();
                    colHeader.AutoSize = false;
                    colHeader.Size = new Size(buttonSize, 22);
                    colHeader.Location = new Point(gridLeft + rowLabelWidth + c * (buttonSize + gapX), gridTop);
                    colHeader.Text = (c + 1).ToString();
                    colHeader.TextAlign = ContentAlignment.MiddleCenter;
                    colHeader.Font = new Font("宋体", 10F, FontStyle.Bold);
                    colHeader.ForeColor = Color.DarkSlateGray;
                    colHeader.BackColor = _panel.BackColor;
                    _panel.Controls.Add(colHeader);
                }

                for (int r = 0; r < 9; r++)
                {
                    int y = gridTop + 28 + r * (buttonSize + gapY);

                    // 左侧行标签（排号 / 寄存器 / 字节 / 当前值）
                    Label rowLabel = new Label();
                    rowLabel.AutoSize = false;
                    rowLabel.Size = new Size(rowLabelWidth, buttonSize);
                    rowLabel.Location = new Point(gridLeft, y);
                    rowLabel.TextAlign = ContentAlignment.MiddleLeft;
                    rowLabel.Font = new Font("宋体", 9.5F, FontStyle.Regular);
                    rowLabel.ForeColor = Color.Black;
                    rowLabel.BackColor = Color.FromArgb(245, 245, 245);
                    rowLabel.BorderStyle = BorderStyle.FixedSingle;
                    RowLabels[r] = rowLabel;
                    _panel.Controls.Add(rowLabel);

                    // 8 个圆形按钮
                    for (int c = 0; c < 8; c++)
                    {
                        CircleButton btn = new CircleButton();
                        btn.Size = new Size(buttonSize, buttonSize);
                        btn.Location = new Point(gridLeft + rowLabelWidth + c * (buttonSize + gapX), y);
                        btn.Text = RowIoNames[r, c];
                        btn.Row = r;
                        btn.Col = c;
                        btn.BitValue = RowBitValues[r, c];
                        btn.Click += (s, e2) => OnCircleClick((CircleButton)s);
                        Buttons[r, c] = btn;
                        _panel.Controls.Add(btn);
                    }
                }

                // 调整 panel 大小，刚好包住所有控件
                int totalWidth = gridLeft * 2 + rowLabelWidth + 8 * buttonSize + 7 * gapX;
                int totalHeight = gridTop * 2 + 28 + 9 * buttonSize + 8 * gapY;
                _panel.Size = new Size(totalWidth, totalHeight);
            }

            /// <summary>刷新所有行标签（排号/寄存器/字节/当前值）</summary>
            public void RefreshRowLabels()
            {
                for (int r = 0; r < 9; r++)
                {
                    int regIdx = RowToRegIndex[r];
                    RowLabels[r].Text = string.Format(
                        "第 {0} 排  0x{1:X4}  {2}\n当前值: 0x{3:X4}",
                        r + 1,
                        RegAddresses[regIdx],
                        RowByteDesc[r],
                        CurrentRegValues[regIdx]);
                }
            }

            /// <summary>
            /// 圆形按钮点击：toggle ON/OFF，重算所属寄存器值并整体写入。
            /// V1.21 增强：若该通道被映射到备用通道，点击时先弹窗（ShowRemapNotice）
            /// 告知"该通道已做映射、实际输出通道是哪个"，再执行 toggle，方便现场识别。
            /// </summary>
            private void OnCircleClick(CircleButton btn)
            {
                if (btn == null) return;

                int row = btn.Row;
                int absReg = RegAddresses[RowToRegIndex[row]];
                int ch = CommunicationTestForm.ChannelOf(btn.BitValue);

                // 该通道若被映射到备用通道，先弹窗说明实际输出通道
                if (_owner.IsRemapSource(absReg, ch))
                {
                    (int dstReg, int dstBit) = _owner.MapChannel(absReg, ch);
                    _owner.ShowRemapNotice(RowIoNames[row, btn.Col], absReg, ch, dstReg, dstBit);
                }

                btn.IsOn = !btn.IsOn;

                int regIndex = RowToRegIndex[row];
                CurrentRegValues[regIndex] = RecomputeRegValue(regIndex);

                // 被映射走的源通道位已从源寄存器剔除，其信号汇总到 0x2009（CurrentRegValues[5]）
                CurrentRegValues[5] = RecomputeRemapRegValue();

                _owner.WriteRegister(this, regIndex, row);
                RefreshRowLabels();
            }

            /// <summary>重算指定寄存器的合并值（把共享该寄存器的所有排的 ON 位按位 OR）</summary>
            private int RecomputeRegValue(int regIndex)
            {
                int value = 0;
                for (int r = 0; r < 9; r++)
                {
                    if (RowToRegIndex[r] != regIndex) continue;

                    int absReg = RegAddresses[regIndex];
                    for (int c = 0; c < 8; c++)
                    {
                        if (!Buttons[r, c].IsOn) continue;

                        int ch = CommunicationTestForm.ChannelOf(RowBitValues[r, c]);
                        if (_owner.IsRemapSource(absReg, ch)) continue;   // 被映射走的源位不写源寄存器

                        value |= RowBitValues[r, c];
                    }
                }
                return value;
            }

            /// <summary>计算备用映射目标寄存器（0x2009）的值：所有 ON 且被映射的通道位累积到此</summary>
            private int RecomputeRemapRegValue()
            {
                int value = 0;
                for (int r = 0; r < 9; r++)
                {
                    int absReg = RegAddresses[RowToRegIndex[r]];
                    for (int c = 0; c < 8; c++)
                    {
                        if (!Buttons[r, c].IsOn) continue;
                        int ch = CommunicationTestForm.ChannelOf(RowBitValues[r, c]);
                        if (_owner.IsRemapSource(absReg, ch))
                        {
                            (_, int dstBit) = _owner.MapChannel(absReg, ch);
                            value |= (1 << dstBit);
                        }
                    }
                }
                return value;
            }

            /// <summary>按读回的 10 个寄存器值（values[0]=0x2000 ... values[9]=0x2009）刷新按钮 ON/OFF</summary>
            public void SetButtonsFromRegisters(int[] values)
            {
                for (int r = 0; r < 9; r++)
                {
                    int regIndex = RowToRegIndex[r];
                    int absReg = RegAddresses[regIndex];
                    for (int c = 0; c < 8; c++)
                    {
                        int bitVal = RowBitValues[r, c];
                        int ch = CommunicationTestForm.ChannelOf(bitVal);
                        if (_owner.IsRemapSource(absReg, ch))
                        {
                            (int dstReg, int dstBit) = _owner.MapChannel(absReg, ch);
                            int dstIdx = dstReg - 0x2000;
                            Buttons[r, c].IsOn = dstIdx >= 0 && dstIdx < values.Length &&
                                                 (values[dstIdx] & (1 << dstBit)) != 0;
                        }
                        else
                        {
                            int idx = absReg - 0x2000;
                            Buttons[r, c].IsOn = idx >= 0 && idx < values.Length &&
                                                 (values[idx] & bitVal) != 0;
                        }
                    }
                }

                // 同步寄存器本地值（含备用目标 0x2009）
                for (int i = 0; i < RegAddresses.Length && i < CurrentRegValues.Length; i++)
                {
                    int idx = RegAddresses[i] - 0x2000;
                    CurrentRegValues[i] = (idx >= 0 && idx < values.Length) ? values[idx] : 0;
                }
                RefreshRowLabels();
            }

            /// <summary>全部按钮置 OFF，寄存器本地值清零（不写设备，写设备由 AllOff 统一处理）</summary>
            public void ClearAll()
            {
                for (int r = 0; r < 9; r++)
                {
                    for (int c = 0; c < 8; c++)
                    {
                        Buttons[r, c].IsOn = false;
                    }
                }
                for (int i = 0; i < CurrentRegValues.Length; i++) CurrentRegValues[i] = 0;
                RefreshRowLabels();
            }
        }

        // ========================================================================
        //  内部类：CircleButton —— 圆形灯按钮（自绘圆形，ON=亮绿+金边，OFF=深灰）
        //  自绘控件，不依赖 SunnyUI 样式，保持"指示灯"观感与点击交互不变。
        // ========================================================================
        public class CircleButton : Button
        {
            private static readonly Color OffFill = Color.FromArgb(70, 70, 76);
            private static readonly Color OnFill = Color.FromArgb(40, 220, 70);
            private static readonly Color OffBorder = Color.FromArgb(40, 40, 40);
            private static readonly Color OnBorder = Color.Gold;
            private static readonly Color OffText = Color.FromArgb(180, 180, 180);
            private static readonly Color OnText = Color.Black;

            private bool isOn = false;

            /// <summary>当前是否处于 ON 状态；变化时自动重绘</summary>
            public bool IsOn
            {
                get { return isOn; }
                set
                {
                    if (isOn != value)
                    {
                        isOn = value;
                        this.Invalidate();
                    }
                }
            }

            /// <summary>所在排索引（0~8）</summary>
            public int Row { get; set; }

            /// <summary>所在列索引（0~7）</summary>
            public int Col { get; set; }

            /// <summary>该按钮在所属寄存器中的位值</summary>
            public int BitValue { get; set; }

            public CircleButton()
            {
                this.SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.UserPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw,
                    true);
                this.FlatStyle = FlatStyle.Flat;
                this.FlatAppearance.BorderSize = 0;
                this.Cursor = Cursors.Hand;
                this.Font = new Font("宋体", 9F, FontStyle.Bold);
                this.TextAlign = ContentAlignment.MiddleCenter;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                Color back = (this.Parent != null) ? this.Parent.BackColor : Color.White;
                using (SolidBrush bgBrush = new SolidBrush(back))
                {
                    g.FillRectangle(bgBrush, this.ClientRectangle);
                }

                Rectangle circleRect = this.ClientRectangle;
                circleRect.Inflate(-4, -4);

                Color fill = IsOn ? OnFill : OffFill;
                using (SolidBrush fillBrush = new SolidBrush(fill))
                {
                    g.FillEllipse(fillBrush, circleRect);
                }

                using (Pen borderPen = new Pen(IsOn ? OnBorder : OffBorder, IsOn ? 2.5f : 1.5f))
                {
                    g.DrawEllipse(borderPen, circleRect);
                }

                Color textColor = IsOn ? OnText : OffText;
                using (SolidBrush textBrush = new SolidBrush(textColor))
                {
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(this.Text, this.Font, textBrush, this.ClientRectangle, sf);
                }
            }
        }
    }
}
