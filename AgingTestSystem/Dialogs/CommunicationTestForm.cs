using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using AgingTestSystem.Models;
using AgingTestSystem.Services;
using Sunny.UI;

namespace AgingTestSystem.Dialogs
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
    ///   - 底部按钮栏（pnlBottom）：UIButton 连接测试/全部关闭/读取状态/一键遍历/关闭窗口 + UITextBox 日志
    ///   - 每个页签内的 9×8 = 72 个圆形灯按钮由 ChannelGrid 动态生成（自绘 CircleButton）
    ///
    /// 【一键遍历（V1.22 新增）】
    ///   - 点击底部"一键遍历"按钮（紫），对当前页签的测试做"通断跑马灯"检测：
    ///     每 500ms 只点亮一路通道、其余全部熄灭，72 路循环往复，用于快速检查每路
    ///     DO 输出接线是否通断正常；再次点击（变红"停止遍历"）立即停止并全部关闭。
    ///   - 负压开关测试 / 载台上电测试 两个页签都支持；运行中切换页签会自动停止遍历。
    ///   - 性能：单拍在后台线程执行（写寄存器 + 读回真实状态），完成后切回 UI 线程
    ///     刷新按钮，界面不卡顿；上一拍未完成时自动跳过下一拍，避免任务堆积。
    ///   - 实时反馈：每拍写完后读回 10 个寄存器，用真实通断状态更新圆形灯按钮，
    ///     一眼可看出哪路实际输出异常。
    ///
    ///     【共享连接（V1.23 重构）】
    ///   - 本窗体**不再自建 Modbus TCP 连接**，而是复用主程序 DeviceManager 拥有的
    ///     那一条 IO 耦合器连接（ModbusTcpIoController，与采集线程同源）：
    ///     · 连接/按需重连 → DeviceManager.EnsureIoConnected()（复用采集线程已建好的连接）；
    ///     · 原始寄存器读写（0x2000~0x2009）→ DeviceManager.ReadHoldingRegisters /
    ///       WriteSingleRegister，内部用控制器 _syncRoot 串行化，与采集线程并发安全；
    ///     · 断连检测 → 顶部 LED/状态由 1s 状态定时器按 DeviceManager.IsIoConnected 刷新，
    ///       断开后主程序的心跳/自动重连机制会自动恢复，本窗体只提示不重复弹窗。
    ///   - 效果：主界面与测试窗体共享同一条连接，不再重复建立/占用第二路 TCP。
    ///
    /// 【逻辑】移植自测试工程 ModbusTcpIoControllerTest：
    ///   - 载台上电测试 ← PowerOnTestForm（按钮网格控制每一路 ON/OFF）
    ///   - 负压开关测试 ← MainForm.btnWriteDatas（批量扫描每个通道），并升级为
    ///     与载台上电测试一致的"点击按钮控制该通道亮起"交互
    ///
    /// 【通讯】通讯库 NModbus 由主程序的 ModbusTcpIoController 持有，本窗体只通过
    ///   DeviceManager 的共享连接操作，**不持有任何 TcpClient/IModbusMaster**。
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
    ///   - 写入跟随映射：被映射的源通道位不写源寄存器，其信号汇总写到备用目标寄存器
    ///     （默认 0x2009，RegAddresses[5]）；按钮仍显示旧通道名。
    ///   - 读取跟随映射：读回状态时按映射后的目标寄存器/目标位解析，按钮反映真实物理输出。
    ///   - 0x2009 写采用读-改-写（WriteBackupRegister + ComputeRemapTargetMask）：两个测试
    ///     各自只动自己拥有的映射目标位，不互相覆盖（与主项目逐通道 RMW 一致）。
    ///   - 点击被映射的通道（手动 toggle 或一键遍历点亮）都会弹出**非模态悬浮提示窗**
    ///     （RemapNoticeForm，不阻塞流程、不抢焦点，可保持打开继续操作其他窗口）告知
    ///     "该通道已做备用通道映射、实际输出通道是哪个寄存器第几路"并在日志追加映射记录。
    /// </summary>
    public partial class CommunicationTestForm : UIForm
    {
        /// <summary>设备管理器（连接的唯一所有者；本窗体复用它的共享连接，不持有任何 Modbus 客户端）</summary>
        private readonly DeviceManager _deviceManager;

        /// <summary>设备配置（读取 PlcAddress/PlcPort/IoUnitId/超时/备用映射）</summary>
        private readonly DeviceConfig _config;

        // ===================== 共享连接 =====================

        /// <summary>是否已连接（镜像 DeviceManager.IsIoConnected，由 1s 状态定时器刷新；volatile 供后台遍历线程读取）</summary>
        private volatile bool _connected;

        /// <summary>读-改-写互斥锁：串行化本窗体内部的手动点击 / 一键遍历，保证"读→合并→写"序列原子执行。
        /// （与共享连接的 _syncRoot 无关；后者由 ModbusTcpIoController 负责，保证对连接本身不并发）</summary>
        private readonly object _modbusLock = new object();

        // ===================== 两个测试网格 =====================

        /// <summary>负压开关测试网格（72 路真空电磁阀 Y000~Y107）</summary>
        private readonly ChannelGrid _vacuumGrid;

        /// <summary>载台上电测试网格（72 路载台上电 Y110~Y217）</summary>
        private readonly ChannelGrid _carrierGrid;

        // ===================== 一键遍历（通断跑马灯） =====================

        /// <summary>一键遍历定时器：每 500ms 触发一次，把下一路通道点亮（跑马灯）</summary>
        private readonly System.Windows.Forms.Timer _sweepTimer;

        /// <summary>一键遍历是否正在运行（true=跑马灯进行中，按钮显示"停止遍历"；volatile 供后台遍历线程读取）</summary>
        private volatile bool _sweepActive;

        /// <summary>是否已在后台执行遍历单拍（防重入：上一个 500ms 还没做完就不叠加新的，避免任务堆积）</summary>
        private volatile bool _sweepStepBusy;

        /// <summary>当前遍历到的通道号（0~71；行 = /8，列 = %8，对应 9 排 × 8 列按钮）</summary>
        private int _sweepChannelIndex;

        /// <summary>当前遍历的目标测试网格（按下"一键遍历"时所在页签对应的网格）</summary>
        private ChannelGrid _sweepGrid;

        /// <summary>当前遍历的测试名称（负压开关测试 / 载台上电测试，仅用于日志显示）</summary>
        private string _sweepGridName;

        // ===================== 共享连接状态 / 自动连接 =====================

        /// <summary>连接状态定时器：每 1s 按 DeviceManager.IsIoConnected 刷新 LED/状态，无需发任何 Modbus 报文</summary>
        private readonly System.Windows.Forms.Timer _statusTimer;

        /// <summary>非模态映射提示窗（复用同一实例，多次触发只更新文本，不重复弹窗）</summary>
        private RemapNoticeForm _remapNoticeForm;

        /// <summary>构造函数：复用主程序的共享连接，创建两个测试网格</summary>
        /// <param name="deviceManager">设备管理器（拥有 IO 耦合器共享连接；通过它完成连接与原始寄存器读写）</param>
        public CommunicationTestForm(DeviceManager deviceManager)
        {
            _deviceManager = deviceManager ?? throw new ArgumentNullException(nameof(deviceManager));
            _config = deviceManager.Config ?? throw new ArgumentNullException(nameof(deviceManager.Config));

            InitializeComponent();

            // 初始状态：未连接（更新顶部 LED 与状态文字；稍后状态定时器会同步到实际值）
            SetConnected(false);

            // 初始化一键遍历定时器（跑马灯）：间隔 500ms，仅负责在 UI 线程触发，
            // 实际的寄存器写/读放在后台线程执行（见 SweepStepWorker），保证不卡界面
            _sweepTimer = new System.Windows.Forms.Timer();
            _sweepTimer.Interval = 500;
            _sweepTimer.Tick += SweepTimer_Tick;

            // 初始化连接状态定时器：每 1s 只读 DeviceManager.IsIoConnected（本地布尔，不发报文）
            _statusTimer = new System.Windows.Forms.Timer();
            _statusTimer.Interval = 1000;
            _statusTimer.Tick += StatusTimer_Tick;

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
        /// 窗体首次显示时：启动连接状态定时器并立即后台复用主程序共享连接（与生产环境一致）。
        /// 未连接失败不弹窗，只在日志提示；之后由主程序的心跳/自动重连机制负责恢复，
        /// 本窗体状态定时器每 1s 跟随刷新 LED。
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // 启动连接状态定时器（每 1s 跟随主程序共享连接的实时状态刷新 LED，不发报文）
            _statusTimer.Start();

            // 打开即复用主程序共享连接：后台线程执行，不阻塞界面
            AutoConnect();
        }

        /// <summary>
        /// 后台自动连接（静默）：复用主程序共享连接，失败只写日志；
        /// 之后由主程序心跳/自动重连机制持续恢复，本窗体状态定时器跟随刷新。
        /// </summary>
        private void AutoConnect()
        {
            AppendLog("[连接] 正在连接耦合器（复用主程序共享连接）...");
            Task.Run(() => TryConnectSilent());
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
        /// 静默连接（可在后台线程调用，不弹任何窗）：**复用主程序共享连接**，不新建 TCP。
        /// 调用 DeviceManager.EnsureIoConnected()：已连接直接返回 true；未连接立即用
        /// 主采集线程同一条连接连一次。失败由主程序心跳/自动重连机制负责恢复，
        /// 本窗体只更新状态指示与日志。
        /// </summary>
        /// <returns>true=已连接，false=连接失败/未连接</returns>
        private bool TryConnectSilent()
        {
            bool ok = _deviceManager.EnsureIoConnected();
            RunOnUi(() =>
            {
                _connected = ok;
                SetConnected(ok);
                if (ok)
                {
                    AppendLog($"[连接] 已连接耦合器 {_config.PlcAddress}:{_config.PlcPort}（复用主程序共享连接）");
                }
                else
                {
                    AppendLog($"[错误] 耦合器 {_config.PlcAddress}:{_config.PlcPort} 连接失败（请检查 IP/网线，主程序后台会自动重连）");
                }
            });
            return ok;
        }

        /// <summary>
        /// 手动"连接测试"按钮：后台复用共享连接，完成后弹窗反馈结果（不阻塞 UI）。
        /// </summary>
        private void ConnectAsync()
        {
            AppendLog("[连接] 正在连接耦合器（复用主程序连接）...");
            Task.Run(() =>
            {
                bool ok = TryConnectSilent();
                RunOnUi(() =>
                {
                    if (ok)
                    {
                        UIMessageBox.Show("连接成功！", "提示",
                            UIStyle.Green, UIMessageBoxButtons.OK, true, 0);
                    }
                    else
                    {
                        UIMessageBox.Show($"连接 {_config.PlcAddress}:{_config.PlcPort} 失败，主程序已在后台自动重连。", "错误",
                            UIStyle.Red, UIMessageBoxButtons.OK, true, 0);
                    }
                });
            });
        }

        /// <summary>
        /// 连接状态定时器：每 1s 按 DeviceManager.IsIoConnected 刷新 LED/状态。
        /// 只读本地布尔，**不发任何 Modbus 报文**（共享连接的存活由主程序采集/心跳保障）。
        /// 检测到"已连接 → 未连接"边沿时：停止遍历 + 日志提示（不重复弹窗，主程序会提示）。
        /// </summary>
        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            bool live = _deviceManager.IsIoConnected;
            if (live == _connected) return;
            _connected = live;

            RunOnUi(() =>
            {
                SetConnected(live);
                if (!live)
                {
                    AppendLog("[连接] 与耦合器的连接已断开，主程序正在后台自动重连...");
                    // 遍历跑马灯若在运行，立即停止（写寄存器必然失败）
                    if (_sweepActive) StopSweep();
                }
            });
        }

        /// <summary>
        /// 跨线程安全地在 UI 线程执行委托（后台线程 → UI 线程用 BeginInvoke）。
        /// 窗体已释放/正在释放时直接忽略，避免 ObjectDisposedException。
        /// </summary>
        private void RunOnUi(Action action)
        {
            if (action == null) return;
            if (IsDisposed || Disposing) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(action)); }
                catch { }
            }
            else
            {
                action();
            }
        }

        /// <summary>窗体关闭时停止所有定时器，释放资源（**不**断开共享连接——连接归主程序所有）</summary>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // 停止遍历/状态定时器并释放，避免窗体销毁后定时器回调
            _sweepActive = false;
            if (_sweepTimer != null)
            {
                _sweepTimer.Stop();
                _sweepTimer.Dispose();
            }
            if (_statusTimer != null)
            {
                _statusTimer.Stop();
                _statusTimer.Dispose();
            }
            if (_remapNoticeForm != null)
            {
                _remapNoticeForm.Close();
                _remapNoticeForm.Dispose();
                _remapNoticeForm = null;
            }

            base.OnFormClosed(e);
        }

        // ===================== 寄存器读写（复用共享连接） =====================

        /// <summary>
        /// 读共享连接的保持寄存器（未连接/失败时返回 null）。
        /// 并发安全：内部由 ModbusTcpIoController 的 _syncRoot 串行化，与采集线程共用一条连接。
        /// </summary>
        private ushort[] ReadRegs(ushort address, ushort count)
        {
            if (!_connected) return null;
            return _deviceManager.ReadHoldingRegisters(address, count);
        }

        /// <summary>
        /// 写共享连接的保持寄存器（未连接/失败时返回 false）。
        /// 并发安全：内部由 ModbusTcpIoController 的 _syncRoot 串行化，与采集线程共用一条连接。
        /// </summary>
        private bool WriteReg(ushort address, ushort value)
        {
            if (!_connected) return false;
            return _deviceManager.WriteSingleRegister(address, value);
        }

        /// <summary>
        /// 写入某个测试网格的指定寄存器（读-改-写，避免覆盖 0x2004 上对方测试拥有的字节）。
        /// 加 _modbusLock 与手动点击/一键遍历串行化，保证"读→合并→写"序列原子执行。
        /// </summary>
        /// <param name="grid">目标测试网格</param>
        /// <param name="regIndex">寄存器索引（0~4 对应 RegAddresses；5=备用映射目标 0x2009）</param>
        /// <param name="triggerRow">触发本次写入的排索引（0~8），仅用于日志显示</param>
        public void WriteRegister(ChannelGrid grid, int regIndex, int triggerRow)
        {
            int addr = grid.RegAddresses[regIndex];
            int val = grid.CurrentRegValues[regIndex];

            if (!_connected)
            {
                AppendLog($"[警告] 未连接，无法写入。请先点击“连接测试”。(由第 {triggerRow + 1} 排触发，0x{addr:X4} = 0x{val:X4})");
                return;
            }

            lock (_modbusLock)
            {
                if (!_connected)
                {
                    AppendLog($"[警告] 未连接，无法写入。请先点击“连接测试”。(由第 {triggerRow + 1} 排触发，0x{addr:X4} = 0x{val:X4})");
                    return;
                }

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
                    ushort[] cur = ReadRegs((ushort)addr, 1);
                    ushort current = (cur != null && cur.Length > 0) ? cur[0] : (ushort)0;
                    writeValue = (ushort)((current & ~ownedMask) | (val & ownedMask));
                }

                if (!WriteReg((ushort)addr, writeValue))
                {
                    AppendLog($"[错误] 写入 0x{addr:X4} 失败（连接已断开，主程序后台自动重连中）");
                    return;
                }

                // 备用映射目标寄存器（0x2009）一并下发：读-改-写，只动本网格拥有的映射目标位，
                // 保留另一测试在该寄存器里的映射位（与主项目 ModbusTcpIoController 逐通道 RMW 一致）
                WriteBackupRegister(grid, grid.CurrentRegValues[5]);

                AppendLog($"[写入] 由第 {triggerRow + 1} 排触发  0x{addr:X4} = 0x{writeValue:X4}");
            }
        }

        /// <summary>
        /// 读取 DO 区全部 10 个寄存器（0x2000~0x2009），更新两个测试网格的按钮状态与标签。
        /// 一次性读回，两测试共享的 0x2004 各自按自己的字节解析，互不干扰。
        /// </summary>
        private void ReadAllStatus()
        {
            if (!_connected)
            {
                UIMessageBox.Show("请先点击“连接测试”建立通讯！", "提示",
                    UIStyle.Orange, UIMessageBoxButtons.OK, true, 0);
                return;
            }

            lock (_modbusLock)
            {
                // 读 10 个保持寄存器（0x2000~0x2009；第 10 个 0x2009 为备用映射目标）
                ushort[] regs = ReadRegs(0x2000, 10);
                if (regs == null || regs.Length < 10)
                {
                    AppendLog("[错误] 读取失败：返回寄存器数量不足或连接已断开");
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
        }

        /// <summary>全部关闭：0x2000~0x2009 全写 0，两个测试网格全部按钮置 OFF（现场应急用）</summary>
        private void AllOff()
        {
            // 1) 本地状态全部清零
            _vacuumGrid.ClearAll();
            _carrierGrid.ClearAll();

            // 2) 把 10 个寄存器全部写 0
            if (!_connected)
            {
                AppendLog("[警告] 未连接，仅清除本地状态。请先连接再写入。");
                return;
            }

            lock (_modbusLock)
            {
                for (int i = 0; i < 10; i++)
                {
                    if (!WriteReg((ushort)(0x2000 + i), 0x0000))
                    {
                        AppendLog($"[错误] 写 0x{0x2000 + i:X4} 失败（连接已断开）");
                    }
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
        /// 告知某个通道已做备用通道映射及其实际输出通道（V1.21 新增，V1.22 改为非模态）。
        /// 在用户点击被映射的通道（手动 toggle / 一键遍历点亮）时调用。
        /// 使用非模态悬浮提示窗 RemapNoticeForm 展示：不阻塞代码流程、不强制占用焦点，
        /// 用户可保持窗口打开并继续点击/操作其他窗体；同时日志追加一条映射记录。
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
                "实际输出通道：寄存器 0x{3:X4} 第 {4} 通道（第 {5} 路）。\n\n" +
                "提示窗不阻塞操作，可继续点击其他窗口；无需时点右上角“×”或“知道了”关闭。",
                ioName, srcReg, srcCh + 1, dstReg, dstCh + 1, dstCh + 1);

            // 复用同一个悬浮提示窗（非模态、不抢焦点），多次触发只更新文本，不重复弹窗
            if (_remapNoticeForm == null || _remapNoticeForm.IsDisposed)
            {
                _remapNoticeForm = new RemapNoticeForm();
                _remapNoticeForm.Show(this);
            }
            _remapNoticeForm.SetMessage(msg);

            AppendLog($"[映射] 通道 {ioName}（0x{srcReg:X4} 通道{srcCh + 1}）→ 实际输出 0x{dstReg:X4} 通道{dstCh + 1}");
        }

        /// <summary>
        /// 非模态映射提示窗：WS_EX_NOACTIVATE + WS_EX_TOOLWINDOW，弹窗不激活、不抢焦点、
        /// 不进任务栏/AltTab；置顶显示并停留在屏幕右上角，直到用户手动关闭。
        /// </summary>
        private sealed class RemapNoticeForm : UIForm
        {
            private readonly UILabel _label;

            public RemapNoticeForm()
            {
                Text = "通道映射提示";
                ShowTitle = true;
                ShowInTaskbar = false;
                TopMost = true;
                Width = 500;
                Height = 170;

                var screen = Screen.PrimaryScreen.WorkingArea;
                Location = new Point(screen.Right - Width - 16, screen.Top + 16);

                var footer = new UIPanel { Dock = DockStyle.Bottom, Height = 48 };
                var closeBtn = new UIButton { Text = "知道了", Size = new Size(96, 32) };
                closeBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                closeBtn.Location = new Point(footer.Width - closeBtn.Width - 8, 8);
                closeBtn.Click += (s, e) => Close();
                footer.Controls.Add(closeBtn);

                _label = new UILabel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(12, 8, 12, 4),
                    TextAlign = ContentAlignment.TopLeft,
                    AutoSize = false,
                    Font = new Font("Microsoft YaHei", 11F)
                };

                Controls.Add(_label);
                Controls.Add(footer);
            }

            /// <summary>禁止激活：弹窗不抢占当前窗口焦点（鼠标点击仍有效）。</summary>
            protected override CreateParams CreateParams
            {
                get
                {
                    CreateParams cp = base.CreateParams;
                    cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                    cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                    return cp;
                }
            }

            public void SetMessage(string msg) => _label.Text = msg;
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
            ConnectAsync();
        }

        private void btnAllOff_Click(object sender, EventArgs e)
        {
            AllOff();
        }

        private void btnReadStatus_Click(object sender, EventArgs e)
        {
            ReadAllStatus();
        }

        // ===================== 一键遍历（通断跑马灯） =====================

        /// <summary>
        /// "一键遍历"按钮点击：开始/停止对当前页签测试做"通断跑马灯"检测。
        /// 未连接时先提示；运行中再次点击则停止并全部关闭。
        /// </summary>
        private void btnSweep_Click(object sender, EventArgs e)
        {
            // 遍历需要真实写寄存器，未连接时禁止启动
            if (!_connected)
            {
                UIMessageBox.Show("请先点击“连接测试”建立通讯！", "提示",
                    UIStyle.Orange, UIMessageBoxButtons.OK, true, 0);
                return;
            }

            // 正在运行 → 停止；未运行 → 开始
            if (_sweepActive)
            {
                StopSweep();
            }
            else
            {
                StartSweep();
            }
        }

        /// <summary>
        /// 开始一键遍历：锁定当前页签对应的测试网格，启动 500ms 跑马灯定时器，
        /// 并立即点亮第一路（不用等一个周期）。
        /// </summary>
        private void StartSweep()
        {
            // 按当前页签确定要遍历的测试网格（0=负压开关测试，1=载台上电测试）
            if (tabControl.SelectedIndex == 0)
            {
                _sweepGrid = _vacuumGrid;
                _sweepGridName = "负压开关测试";
            }
            else
            {
                _sweepGrid = _carrierGrid;
                _sweepGridName = "载台上电测试";
            }

            _sweepActive = true;
            _sweepChannelIndex = 0;
            btnSweep.Text = "停止遍历";
            btnSweep.Style = UIStyle.Red;
            _sweepTimer.Start();
            AppendLog($"[遍历] 开始 {_sweepGridName} 一键遍历：72 路通断跑马灯，每路点亮 500ms，循环检测");

            // 立即执行第一拍（后台线程），无需等待第一个 500ms 周期
            StartSweepStep();
        }

        /// <summary>
        /// 停止一键遍历：停定时器、恢复按钮外观，并把当前网格全部通道熄灭写回设备，
        /// 避免输出停留在半亮状态。
        /// </summary>
        private void StopSweep()
        {
            _sweepActive = false;
            _sweepTimer.Stop();
            btnSweep.Text = "一键遍历";
            btnSweep.Style = UIStyle.Purple;

            // 停止后把该网格全部通道关闭（仅已连接时才写设备，未连接只清本地灯）
            if (_sweepGrid != null)
            {
                _sweepGrid.SweepAllOff();
                AppendLog($"[遍历] 已停止，{_sweepGridName} 全部通道已关闭");
            }
            _sweepGrid = null;
        }

        /// <summary>
        /// 启动一拍遍历：丢到后台线程执行，避免 Modbus 写/读阻塞 UI 线程（性能关键点）。
        /// 若上一拍还没做完则跳过本拍（_sweepStepBusy 防重入），宁可放慢也不卡界面。
        /// </summary>
        private void StartSweepStep()
        {
            if (_sweepStepBusy) return;
            _sweepStepBusy = true;
            Task.Run((Action)SweepStepWorker);
        }

        /// <summary>
        /// 遍历单拍（后台线程）：
        ///  1) 纯计算"全灭 + 只亮当前通道"的寄存器值（不碰 UI 控件）；
        ///  2) 加锁整体写回设备（读-改-写）；
        ///  3) 读回真实通道状态，切回 UI 线程实时刷新按钮，让跑马灯与实际通断同步。
        /// 连接中途断开时由共享连接的状态定时器统一处理（停遍历 + 提示）。
        /// </summary>
        private void SweepStepWorker()
        {
            try
            {
                if (!_sweepActive || _sweepGrid == null) return;

                // 连接中断时停止遍历（不再写失败刷屏），交由状态定时器统一处理
                if (!_connected)
                {
                    RunOnUi(() =>
                    {
                        AppendLog("[遍历] 连接已断开，自动停止遍历");
                        StopSweep();
                    });
                    return;
                }

                int idx = _sweepChannelIndex;
                int row = idx / 8;
                int col = idx % 8;
                ChannelGrid grid = _sweepGrid;

                // 1) 纯计算：全灭后只点亮 (row, col) 的寄存器值（含备用映射 0x2009）
                int[] regs = grid.ComputeSweepRegisters(row, col);

                // 2) 加锁写回设备（每个寄存器一次，读-改-写保留共享字节）
                WriteSweepRegisters(grid, regs);

                // 3) 读回 10 个寄存器，用于按钮实时反映真实通断
                ushort[] readBack = null;
                lock (_modbusLock)
                {
                    if (_connected)
                    {
                        readBack = ReadRegs(0x2000, 10);
                    }
                }

                // 推进到下一路（单 worker 串行执行，无并发竞争）
                _sweepChannelIndex = (idx + 1) % 72;

                // 切回 UI 线程：用读回的真实值刷新按钮状态 + 完成一圈提示
                int idxForLog = idx;
                int litRow = row;
                int litCol = col;
                RunOnUi(() =>
                {
                    if (_sweepGrid == null) return;
                    if (readBack != null && readBack.Length >= 10)
                    {
                        int[] vals = new int[10];
                        for (int i = 0; i < 10; i++) vals[i] = readBack[i] & 0xFFFF;
                        _sweepGrid.SetButtonsFromRegisters(vals);
                    }
                    if (idxForLog == 0)
                    {
                        AppendLog($"[遍历] {_sweepGridName} 完成一整圈（72 路通断检测），继续下一圈...");
                    }

                    // 当前点亮的通道若已映射到备用通道：按钮仍显示旧通道名（RowIoNames），
                    // 但弹窗告知用户实际输出的通道是哪一个（现场一眼可辨，避免误以为原通道在工作）
                    int litAbsReg = grid.RegAddresses[grid.RowToRegIndex[litRow]];
                    int litCh = CommunicationTestForm.ChannelOf(grid.RowBitValues[litRow, litCol]);
                    if (IsRemapSource(litAbsReg, litCh))
                    {
                        (int dstReg, int dstBit) = MapChannel(litAbsReg, litCh);
                        ShowRemapNotice(grid.RowIoNames[litRow, litCol], litAbsReg, litCh, dstReg, dstBit);
                    }
                });
            }
            catch (Exception ex)
            {
                // 写/读异常：多数情况是断连，交给状态定时器统一处理（停止遍历 + 提示）
                RunOnUi(() =>
                {
                    AppendLog("[遍历] 寄存器读写异常: " + ex.Message);
                    if (_sweepActive) StopSweep();
                });
            }
            finally
            {
                _sweepStepBusy = false;
            }
        }

        /// <summary>
        /// 后台线程安全地写遍历寄存器（加锁串行化）：按本网格 OwnedMask 读-改-写，每寄存器一次，
        /// 并下发备用映射目标 0x2009。不逐条写日志（避免 500ms 一拍刷屏）。
        /// </summary>
        private void WriteSweepRegisters(ChannelGrid grid, int[] regs)
        {
            lock (_modbusLock)
            {
                if (!_connected) return;

                for (int i = 0; i < 5; i++)
                {
                    // 本网格不拥有的寄存器跳过（如 0x2000 对载台电网格 OwnedMask=0）
                    if (grid.OwnedMask[i] == 0) continue;

                    int addr = grid.RegAddresses[i];
                    int ownedMask = grid.OwnedMask[i];
                    ushort writeValue;
                    if (ownedMask == 0xFFFF)
                    {
                        // 整寄存器归本网格所有，直接写
                        writeValue = (ushort)(regs[i] & 0xFFFF);
                    }
                    else
                    {
                        // 共享寄存器读-改-写：保留对方测试拥有的字节
                        ushort[] cur = ReadRegs((ushort)addr, 1);
                        ushort current = (cur != null && cur.Length > 0) ? cur[0] : (ushort)0;
                        writeValue = (ushort)((current & ~ownedMask) | (regs[i] & ownedMask));
                    }
                    if (!WriteReg((ushort)addr, writeValue))
                    {
                        return;   // 断开：交给状态定时器统一处理
                    }
                }

                // 备用映射目标寄存器（0x2009）一并下发：读-改-写保留另一测试的映射位
                WriteBackupRegister(grid, regs[5]);
            }
        }

        /// <summary>
        /// 读-改-写备用映射目标寄存器（RegAddresses[5]，默认 0x2009）：
        /// 只改写"本网格拥有的映射目标位"（ComputeRemapTargetMask），保留其它测试的位。
        /// 与主项目 ModbusTcpIoController 的逐通道读-改-写行为一致，避免两个测试互相覆盖。
        /// 必须在 _modbusLock 内调用（读写共用一把锁）。
        /// </summary>
        /// <param name="grid">目标测试网格</param>
        /// <param name="remapValue">本网格要写进 0x2009 的映射值（含备用映射目标位）</param>
        private void WriteBackupRegister(ChannelGrid grid, int remapValue)
        {
            // 总开关关闭时不写 0x2009（配置默认关，多数工作台行为不变）
            if (!_config.IoBackupChannelMappingEnabled) return;

            int remapMask = grid.ComputeRemapTargetMask();
            if (remapMask == 0) return;   // 本网格没有任何映射目标，不动 0x2009

            ushort[] cur = ReadRegs((ushort)grid.RegAddresses[5], 1);
            ushort current = (cur != null && cur.Length > 0) ? cur[0] : (ushort)0;
            ushort writeValue = (ushort)((current & ~remapMask) | (remapValue & remapMask));
            WriteReg((ushort)grid.RegAddresses[5], writeValue);
        }

        /// <summary>
        /// 定时器回调：每 500ms 启动一拍遍历（后台线程执行，UI 不卡顿）。
        /// </summary>
        private void SweepTimer_Tick(object sender, EventArgs e)
        {
            if (!_sweepActive) return;
            StartSweepStep();
        }

        /// <summary>
        /// 遍历运行中切换页签时自动停止：一键遍历只作用于按下时所在的测试，
        /// 防止跑马灯误控制另一测试的输出通道。
        /// </summary>
        private void tabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_sweepActive)
            {
                StopSweep();
                AppendLog("[遍历] 检测到切换页签，已停止遍历");
            }
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

            /// <summary>
            /// 一键遍历用：纯计算（不触碰 UI 控件，可在后台线程安全调用）——模拟"全灭后只点亮
            /// (row, col)"，重算 5 个业务寄存器值 + 备用映射目标 0x2009 的值。
            /// 返回 regs[0~4]=业务寄存器、regs[5]=0x2009；由调用方加锁写回设备。
            /// </summary>
            /// <param name="row">要亮起的通道所在排（0~8）</param>
            /// <param name="col">要亮起的通道所在列（0~7）</param>
            public int[] ComputeSweepRegisters(int row, int col)
            {
                int[] regs = new int[6];

                // 业务寄存器 0~4：只累加被点亮通道所属寄存器的那一位
                for (int i = 0; i < 5; i++)
                {
                    int value = 0;
                    for (int r = 0; r < 9; r++)
                    {
                        if (RowToRegIndex[r] != i) continue;
                        int absReg = RegAddresses[i];
                        for (int c = 0; c < 8; c++)
                        {
                            bool on = (r == row && c == col);   // 只有目标通道亮
                            if (!on) continue;
                            int ch = CommunicationTestForm.ChannelOf(RowBitValues[r, c]);
                            if (_owner.IsRemapSource(absReg, ch)) continue;  // 被映射走的源位不写源寄存器
                            value |= RowBitValues[r, c];
                        }
                    }
                    regs[i] = value;
                }

                // 备用映射目标 0x2009：被映射的源通道 ON 位累积到目标通道
                int remap = 0;
                int absRegRow = RegAddresses[RowToRegIndex[row]];
                int chRow = CommunicationTestForm.ChannelOf(RowBitValues[row, col]);
                if (_owner.IsRemapSource(absRegRow, chRow))
                {
                    (_, int dstBit) = _owner.MapChannel(absRegRow, chRow);
                    remap |= (1 << dstBit);
                }
                regs[5] = remap;

                return regs;
            }

            /// <summary>
            /// 本网格在备用映射目标寄存器（RegAddresses[5]，默认 0x2009）中"拥有"的位掩码：
            /// 由所有"目标寄存器 = RegAddresses[5] 且 源通道属于本网格拥有的物理通道"的映射目标通道累加。
            /// 写 0x2009 时用它做读-改-写，只动本网格的映射位、保留另一测试的位（与 0x2004 的处理一致）。
            /// </summary>
            public int ComputeRemapTargetMask()
            {
                int mask = 0;
                if (!_owner._config.IoBackupChannelMappingEnabled || _owner._config.IoBackupChannelMappings == null)
                    return 0;

                foreach (var m in _owner._config.IoBackupChannelMappings)
                {
                    // 只关心目标落在本网格备用寄存器上的映射（如 0x2009）
                    if (m.TargetRegister != RegAddresses[5]) continue;

                    // 该映射的源通道是否属于本网格拥有的物理通道（按 OwnedMask 判断，
                    // 例如负压阀网格拥有 0x2004 低字节、载台电网格拥有 0x2004 高字节）
                    for (int i = 0; i < 5; i++)
                    {
                        if (m.SourceRegister == RegAddresses[i] &&
                            (OwnedMask[i] & (1 << m.SourceChannel)) != 0)
                        {
                            mask |= (1 << m.TargetChannel);
                            break;
                        }
                    }
                }
                return mask;
            }

            /// <summary>
            /// 一键遍历停止时调用：本网格全部通道熄灭并把寄存器整体写 0，
            /// 让设备输出回到全关状态。
            /// </summary>
            public void SweepAllOff()
            {
                for (int r = 0; r < 9; r++)
                {
                    for (int c = 0; c < 8; c++)
                    {
                        Buttons[r, c].IsOn = false;
                    }
                }
                for (int i = 0; i < 5; i++) CurrentRegValues[i] = 0;
                CurrentRegValues[5] = 0;
                RefreshRowLabels();

                WriteAllOwnedRegisters();
            }

            /// <summary>把本网格 6 个寄存器（0~4 业务 + [5] 备用映射目标）逐个写回设备，每个只写一次</summary>
            private void WriteAllOwnedRegisters()
            {
                // 未连接时只更新本地按钮/寄存器状态，不尝试写设备（避免大量"未连接"警告刷屏）
                if (!_owner._connected) return;

                bool[] written = new bool[6];
                for (int r = 0; r < 9; r++)
                {
                    int regIndex = RowToRegIndex[r];
                    if (!written[regIndex])
                    {
                        written[regIndex] = true;
                        _owner.WriteRegister(this, regIndex, r);
                    }
                }
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
