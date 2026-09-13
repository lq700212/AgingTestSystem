using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    /// ┌──────────────────────────────────────────────────┐
    /// │ [●]未连接         通讯测试（UIForm 标题栏）        │ ← 蓝色标题栏，ShowTitle
    /// ├──────────────────────────────────────────────────┤
    /// │ pnlHeader：● UILedBulb 连接指示灯 + 连接状态文字    │ ← 顶部状态条
    /// ├──────────────────────────────────────────────────┤
    /// │ tabControl（UITabControl，三个 UIPage 页）         │
    /// │ ┌────────────────────────────────────────────┐   │
    /// │ │ ①负压开关测试 页：panelGridVacuum           │   │
    /// │ │   72 路真空电磁阀 Y000~Y107，寄存器0x2000~  │   │
    /// │ │   0x2004，9×8 圆形灯按钮                   │   │
    /// │ ├────────────────────────────────────────────┤   │
    /// │ │ ②载台上电测试 页：panelGridPowerOn         │   │
    /// │ │   72 路载台上电 Y110~Y217，寄存器0x2004~   │   │
    /// │ │   0x2008，9×8 圆形灯按钮                   │   │
    /// │ ├────────────────────────────────────────────┤   │
    /// │ │ ③预留点位 页（V1.80 新增）：SpareGrid       │   │
    /// │ │   上半 panelSpareDi：预留 DI 只读状态灯     │   │
    /// │ │   （默认 X110~X117 共 8 路，FC0x04 读）     │   │
    /// │ │   下半 panelSpareDo：预留 DO 可点圆形灯     │   │
    /// │ │   （默认 Y220~Y237 共 16 路，读-改-写）     │   │
    /// │ └────────────────────────────────────────────┘   │
    /// ├──────────────────────────────────────────────────┤
    /// │ pnlBottom：[连接测试][全部关闭][读取状态]         │
    /// │            [一键遍历][关闭窗口]                   │
    /// │ txtLog（日志框，只读多行）                        │
    /// └──────────────────────────────────────────────────┘
    /// 圆形灯按钮：每个通道一个自绘 CircleButton，点击控制该路 ON/OFF。
    ///
    /// 【预留点位页设计（V1.80 新增）】
    ///   - 点位表不手写：构造时按 IoMapBuilder.Build(_config) 取 Function=Unknown 的点，
    ///     随 TotalInputs/TotalOutputs 改配置自适应（默认预留 DI=73~80/X110~X117 8 路，
    ///     预留 DO=145~160/Y220~Y237 16 路）；无预留时两区显示"无预留"空态。
    ///   - DI 只读：经 DeviceManager.GetAllInputs()（FC0x04，与采集同源）刷新状态灯；
    ///     状态定时器坚持不发报文，DI 只在"读取状态"/进页自动刷新。
    ///   - DO 可写：一律读-改-写。0x2009 同时是备用映射目标（见问题确认清单#106），
    ///     映射启用时自动保位（ComputePreserveMask）：两边写同一寄存器互不覆盖。
    ///   - 一键遍历：预留页只遍历 DO（DI 驱动不了）；三个网格统一走 ISweepableGrid 接口。
    ///
    /// 【一键遍历（V1.22 新增）】
    ///   - 点击底部"一键遍历"按钮（紫），对当前页签的测试做"通断跑马灯"检测：
    ///     每 500ms 只点亮一路通道、其余全部熄灭，72 路循环往复，用于快速检查每路
    ///     DO 输出接线是否通断正常；再次点击（变红"停止遍历"）立即停止并全部关闭。
    ///   - 负压开关测试 / 载台上电测试 / 预留点位测试（只遍历 DO）三个页签都支持；运行中切换页签会自动停止遍历。
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
    ///
    /// 【右键映射（V1.81 新增）】
    ///   - 负压/载台/预留 DO 的圆形灯支持右键菜单：映射到备用通道…（打开可视化连线页并
    ///     预选该源，点目标即连线）/ 取消本通道映射（直接删 + 落盘）/ 查看映射去向 /
    ///     打开端口映射配置…（不预选，全图总览）。
    ///   - 可视化连线页（IoRemapVisualForm）：左列源通道、右列备用目标，点选连线；
    ///     目标独占（一个备用只接一个源，IoRemapValidator 拦截）；保存走
    ///     SettingsForm.PersistChanges（与设置表同一条落盘路），写 App.config 即时生效。
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

        // ===================== 三个测试网格 =====================

        /// <summary>负压开关测试网格（72 路真空电磁阀 Y000~Y107）</summary>
        private readonly ChannelGrid _vacuumGrid;

        /// <summary>载台上电测试网格（72 路载台上电 Y110~Y217）</summary>
        private readonly ChannelGrid _carrierGrid;

        /// <summary>预留点位网格（V1.80 新增：上半预留 DI 只读灯 + 下半预留 DO 可点灯）</summary>
        private readonly SpareGrid _spareGrid;

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
        private ISweepableGrid _sweepGrid;

        /// <summary>当前遍历的测试名称（负压开关测试 / 载台上电测试 / 预留点位测试，仅用于日志显示）</summary>
        private string _sweepGridName;

        // ===================== 共享连接状态 / 自动连接 =====================

        /// <summary>连接状态定时器：每 1s 按 DeviceManager.IsIoConnected 刷新 LED/状态，无需发任何 Modbus 报文</summary>
        private readonly System.Windows.Forms.Timer _statusTimer;

        /// <summary>非模态映射提示窗（复用同一实例，多次触发只更新文本，不重复弹窗）</summary>
        private RemapNoticeForm _remapNoticeForm;

        /// <summary>通道右键菜单（三页可点灯共用一份实例，Opening 时按点的按钮动态改文案/使能）</summary>
        private ContextMenuStrip _remapMenu;

        /// <summary>右键项：映射到备用通道…（带当前通道名）</summary>
        private ToolStripMenuItem _miRemapTo;

        /// <summary>右键项：取消本通道映射（未映射时置灰）</summary>
        private ToolStripMenuItem _miCancelRemap;

        /// <summary>右键项：查看映射去向（未映射时置灰）</summary>
        private ToolStripMenuItem _miViewRemap;

        /// <summary>右键项：打开端口映射配置…（总览，不预选）</summary>
        private ToolStripMenuItem _miOpenVisual;

        /// <summary>窗体已关闭标记（V1.72.14 新增）：OnFormClosed 首行置 true，后台遍历/连接线程的
        /// RunOnUi/AppendLog 见到即直接丢弃，不再 BeginInvoke/Invoke，避免"句柄已销毁后跨线程碰 txtLog"
        /// 与"已释放窗体上建新句柄"两类 InvalidOperationException；volatile 保证后台线程立即可见。</summary>
        private volatile bool _closed;

        /// <summary>构造函数：复用主程序的共享连接，创建三个测试网格</summary>
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

            // 创建三个测试网格（各自建自己的圆形灯按钮；预留网格点位来自 IoMapBuilder）
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

            // 【V1.80】预留点位网格：点位表来自 IoMapBuilder（Function=Unknown），
            // 在自己的两个面板里建灯（DI 只读 + DO 可点），标题行显示实际路数/地址段
            _spareGrid = new SpareGrid(this, panelSpareDi, panelSpareDo, lblSpareDiTitle, lblSpareDoTitle);

            // 【V1.81】右键端口映射：三页可点灯共用一份菜单（预留 DI 只读灯不挂）
            BuildRemapMenu();
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

            // 【V1.81】右键入口一次性提示（现场第一次打开即知道，不用翻文档）
            AppendLog("[提示] 通道灯支持右键：映射到备用通道 / 取消映射 / 查看去向（可视化连线，保存即时生效）");

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
            // 【V1.72.14】关窗竞态下已排队的 BeginInvoke 仍会进到这里，直碰 led/lbl 即炸，故先拦。
            if (_closed || IsDisposed || Disposing) { _connected = connected; return; }
            try
            {
                if (ledStatus == null || ledStatus.IsDisposed) return;
                if (lblStatus == null || lblStatus.IsDisposed) return;
            }
            catch { return; }
            _connected = connected;

            try
            {
                // LED 颜色与亮灭：已连接=亮绿灯，未连接=亮红灯
                ledStatus.Color = connected ? Color.Lime : Color.FromArgb(230, 80, 80);
                ledStatus.On = true;

                // 状态文字与颜色
                lblStatus.Text = connected ? "已连接" : "未连接";
                lblStatus.ForeColor = connected
                    ? Color.FromArgb(0, 150, 80)      // 连接：绿色
                    : Color.FromArgb(30, 80, 160);    // 未连接：深蓝
            }
            catch { }
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
            // 【V1.72.15】关后不碰硬件：关闭瞬间后台连接任务若刚起步，直接返回，
            // 不调共享连接、不排队 UI 回调（RunOnUi 另有自拦，双保险）。
            if (_closed || IsDisposed || Disposing) return false;
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
        /// 【V1.72.14 加固】关闭后（_closed/IsDisposed/Disposing/句柄未建/已销毁）直接丢弃，
        /// 不再 BeginInvoke：旧版只查 IsDisposed，关闭瞬间后台拍仍能 BeginInvoke 进已销毁句柄，
        /// 抛"线程间操作无效"（非 UI 线程弹窗）；BeginInvoke 本身包 try，防竞态。
        /// </summary>
        private void RunOnUi(Action action)
        {
            if (action == null) return;
            if (_closed || IsDisposed || Disposing) return;
            try
            {
                if (!IsHandleCreated) return;
            }
            catch { return; }
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(action)); }
                catch { }
            }
            else
            {
                try { action(); }
                catch { }
            }
        }

        /// <summary>窗体关闭时停止所有定时器，释放资源（**不**断开共享连接——连接归主程序所有）
        /// 【V1.72.14】首行置 _closed 拦后台回调；定时器/提示窗逐个 try 包，单个失败不影响其余；
        /// 提示窗 Close+Dispose 包 try（它被用户点×后 Close 再调无妨，Dispose 保 Sunny 输入框在 UI 线程释放）。</summary>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _closed = true;
            // 停止遍历/状态定时器并释放，避免窗体销毁后定时器回调
            _sweepActive = false;
            try
            {
                if (_sweepTimer != null)
                {
                    _sweepTimer.Stop();
                    _sweepTimer.Dispose();
                }
            }
            catch { }
            try
            {
                if (_statusTimer != null)
                {
                    _statusTimer.Stop();
                    _statusTimer.Dispose();
                }
            }
            catch { }
            try
            {
                if (_remapNoticeForm != null)
                {
                    if (!_remapNoticeForm.IsDisposed) _remapNoticeForm.Close();
                }
            }
            catch { }
            try
            {
                if (_remapNoticeForm != null)
                {
                    if (!_remapNoticeForm.IsDisposed) _remapNoticeForm.Dispose();
                    _remapNoticeForm = null;
                }
            }
            catch { _remapNoticeForm = null; }
            try
            {
                if (_remapMenu != null)
                {
                    _remapMenu.Dispose();
                    _remapMenu = null;
                }
            }
            catch { _remapMenu = null; }

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
            // 【V1.72.15】关后停硬件写（在途遍历拍的收尾调用进到这里即丢弃）。
            if (_closed || IsDisposed || Disposing) return;
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
        /// 读取 DO 区全部 10 个寄存器（0x2000~0x2009），更新三个测试网格的按钮状态与标签。
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
                // 【V1.80】预留页同步：DO 灯按快照刷新 + DI 灯实时读（FC0x04）
                _spareGrid.SetButtonsFromRegisters(values);
                _spareGrid.RefreshDiInputs();

                AppendLog(string.Format(
                    "[读取] 0x2000=0x{0:X4} 0x2001=0x{1:X4} 0x2002=0x{2:X4} 0x2003=0x{3:X4} 0x2004=0x{4:X4} " +
                    "0x2005=0x{5:X4} 0x2006=0x{6:X4} 0x2007=0x{7:X4} 0x2008=0x{8:X4} 0x2009=0x{9:X4}",
                    values[0], values[1], values[2], values[3], values[4],
                    values[5], values[6], values[7], values[8], values[9]));
            }
        }

        /// <summary>全部关闭：0x2000~0x2009 全写 0，三个测试网格全部按钮置 OFF（现场应急用）</summary>
        private void AllOff()
        {
            // 1) 本地状态全部清零（预留 DI 灯是只读镜像不清零；预留 DO 灯本地灭，
            // 设备侧由下面的 0x2000~0x2009 清零循环覆盖，默认配置预留 DO 就在 0x2009 内）
            _vacuumGrid.ClearAll();
            _carrierGrid.ClearAll();
            _spareGrid.ClearDoButtons();

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
            if (_closed || IsDisposed || Disposing) return;
            string msg = string.Format(
                "通道 {0}（寄存器 0x{1:X4} 第 {2} 通道）已做备用通道映射。\n\n" +
                "实际输出通道：寄存器 0x{3:X4} 第 {4} 通道（第 {5} 路）。\n\n" +
                "提示窗不阻塞操作，可继续点击其他窗口；无需时点右上角“×”或“知道了”关闭。",
                ioName, srcReg, srcCh + 1, dstReg, dstCh + 1, dstCh + 1);

            // 复用同一个悬浮提示窗（非模态、不抢焦点），多次触发只更新文本，不重复弹窗
            // 【V1.72.14】关闭竞态下不再建新窗（建了也随主窗一起释放，不如直接丢弃防孤儿）。
            try
            {
                if (_remapNoticeForm == null || _remapNoticeForm.IsDisposed)
                {
                    _remapNoticeForm = new RemapNoticeForm();
                    // 【V1.60】提示窗打开前按当前主题着色
                    AgingTestSystem.Services.ThemeManager.ApplyTo(_remapNoticeForm);
                    _remapNoticeForm.Show(this);
                }
                if (!_remapNoticeForm.IsDisposed) _remapNoticeForm.SetMessage(msg);
            }
            catch { }

            AppendLog($"[映射] 通道 {ioName}（0x{srcReg:X4} 通道{srcCh + 1}）→ 实际输出 0x{dstReg:X4} 通道{dstCh + 1}");
        }

        // ===================== 右键端口映射（V1.81 新增） =====================
        //
        // 【三页一套菜单】负压 72 灯 + 载台 72 灯 + 预留 DO 灯共用一份 ContextMenuStrip，
        // 点哪路、那路有没有映射，全在 Opening 现算（SourceControl 反查按钮→通道）。
        // 以后加第四页可点灯：建灯处挂 _remapMenu + GetChannelInfoFromButton 加一行即可。
        // ========================================================================

        /// <summary>
        /// 建右键菜单并挂到三页全部可点灯上（预留 DI 只读灯不挂：Enabled=false 的灯本来也点不出菜单）。
        /// </summary>
        private void BuildRemapMenu()
        {
            _remapMenu = new ContextMenuStrip();
            _miRemapTo = new ToolStripMenuItem("映射到备用通道…");
            _miRemapTo.Click += (s, e) => OpenRemapVisualForButton();
            _miCancelRemap = new ToolStripMenuItem("取消本通道映射");
            _miCancelRemap.Click += (s, e) => CancelRemapForButton();
            _miViewRemap = new ToolStripMenuItem("查看映射去向");
            _miViewRemap.Click += (s, e) => ViewRemapForButton();
            _miOpenVisual = new ToolStripMenuItem("打开端口映射配置…");
            _miOpenVisual.Click += (s, e) => OpenRemapVisual(null, null);
            _remapMenu.Items.Add(_miRemapTo);
            _remapMenu.Items.Add(_miCancelRemap);
            _remapMenu.Items.Add(_miViewRemap);
            _remapMenu.Items.Add(new ToolStripSeparator());
            _remapMenu.Items.Add(_miOpenVisual);
            _remapMenu.Opening += RemapMenu_Opening;

            for (int r = 0; r < 9; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    _vacuumGrid.Buttons[r, c].ContextMenuStrip = _remapMenu;
                    _carrierGrid.Buttons[r, c].ContextMenuStrip = _remapMenu;
                }
            }
            _spareGrid.AttachDoContextMenu(_remapMenu);
        }

        /// <summary>
        /// 菜单弹出前：按点的按钮反查通道，动态改文案/使能。
        /// 未映射的路"取消/查看"置灰；已映射的路把"源→目标"直接写进菜单，一眼可辨。
        /// </summary>
        private void RemapMenu_Opening(object sender, CancelEventArgs e)
        {
            // 关窗竞态：菜单排队弹出时窗体已关，直接取消，不碰已释放的按钮
            if (_closed || IsDisposed || Disposing) { e.Cancel = true; return; }
            CircleButton btn = (_remapMenu != null) ? _remapMenu.SourceControl as CircleButton : null;
            int reg, ch;
            string ioName, gridName;
            if (btn == null || !GetChannelInfoFromButton(btn, out reg, out ch, out ioName, out gridName))
            {
                e.Cancel = true;
                return;
            }
            if (!IsRemapSource(reg, ch))
            {
                _miRemapTo.Text = string.Format("映射到备用通道…（{0}）", ioName);
                _miCancelRemap.Text = "取消本通道映射";
                _miCancelRemap.Enabled = false;
                _miViewRemap.Text = "查看映射去向";
                _miViewRemap.Enabled = false;
                return;
            }
            int dstReg, dstBit;
            {
                var mapped = MapChannel(reg, ch);
                dstReg = mapped.reg;
                dstBit = mapped.bit;
            }
            _miRemapTo.Text = string.Format("映射到备用通道…（{0}，已映射）", ioName);
            _miCancelRemap.Text = string.Format("取消本通道映射（{0}→0x{1:X4}@0x{2:X2}）", ioName, dstReg, dstBit);
            _miCancelRemap.Enabled = true;
            _miViewRemap.Text = string.Format("查看映射去向（实际输出 0x{0:X4} 第 {1} 路）", dstReg, dstBit + 1);
            _miViewRemap.Enabled = true;
        }

        /// <summary>
        /// 由右键按钮反查通道信息（负压→载台→预留 DO 依次用引用比对定位；
        /// 引用比对而非行列号，因为三页行列号重叠，行列对不上号）。
        /// </summary>
        /// <returns>true=找到，false=未知按钮（菜单直接取消）</returns>
        private bool GetChannelInfoFromButton(CircleButton btn,
            out int reg, out int ch, out string ioName, out string gridName)
        {
            if (_vacuumGrid.TryGetChannelInfo(btn, out reg, out ch, out ioName))
            {
                gridName = "负压开关测试";
                return true;
            }
            if (_carrierGrid.TryGetChannelInfo(btn, out reg, out ch, out ioName))
            {
                gridName = "载台上电测试";
                return true;
            }
            if (_spareGrid.TryGetDoChannelInfo(btn, out reg, out ch, out ioName))
            {
                gridName = "预留点位测试";
                return true;
            }
            gridName = null;
            return false;
        }

        /// <summary>右键"映射到备用通道…"：打开可视化连线页并预选该源，用户只需再点一个目标</summary>
        private void OpenRemapVisualForButton()
        {
            CircleButton btn = (_remapMenu != null) ? _remapMenu.SourceControl as CircleButton : null;
            int reg, ch;
            string ioName, gridName;
            if (btn == null || !GetChannelInfoFromButton(btn, out reg, out ch, out ioName, out gridName)) return;
            OpenRemapVisual(reg, ch);
        }

        /// <summary>
        /// 打开端口映射可视化配置页（模态）。保存后走 SettingsForm.PersistChanges 写
        /// App.config（与设置表同一条落盘路，IoBackup 两 key 非结构型，内存即时热回写），
        /// 成功后刷新三页灯态 + 记日志；失败弹原因，不关窗（用户可改完重试）。
        /// </summary>
        /// <param name="preselectReg">预选源寄存器（null=不预选，全图总览）</param>
        /// <param name="preselectCh">预选源通道（0~15）</param>
        private void OpenRemapVisual(int? preselectReg, int? preselectCh)
        {
            if (_closed || IsDisposed || Disposing) return;
            using (var form = new IoRemapVisualForm(_config))
            {
                // 【V1.60】新窗打开前按当前主题着色
                Services.ThemeManager.ApplyTo(form);
                form.SaveButtonText = "保存并生效";
                if (preselectReg != null) form.PreselectSource(preselectReg.Value, preselectCh.Value);
                if (form.ShowDialog(this) != DialogResult.OK || !form.Confirmed) return;

                var changes = new Dictionary<string, string>();
                changes["IoBackupChannelMappingEnabled"] = form.ResultEnabled ? "true" : "false";
                changes["IoBackupChannelMappings"] = Services.IoRemapValidator.Serialize(form.ResultMappings);
                SettingsForm.PersistResult presult;
                string perror;
                if (!SettingsForm.PersistChanges(_config, changes, out presult, out perror))
                {
                    UIMessageBox.Show(perror, "保存失败", UIStyle.Red, UIMessageBoxButtons.OK, true, 0);
                    return;
                }
                AppendLog(string.Format("[映射] 端口映射已保存并生效：{0} 条，开关={1}",
                    form.ResultMappings.Count, form.ResultEnabled ? "启用" : "关闭"));
                RefreshAfterMappingChange();
            }
        }

        /// <summary>右键"取消本通道映射"：只删这一源，开关不动（可能还有别的映射在用），落盘即生效</summary>
        private void CancelRemapForButton()
        {
            CircleButton btn = (_remapMenu != null) ? _remapMenu.SourceControl as CircleButton : null;
            int reg, ch;
            string ioName, gridName;
            if (btn == null || !GetChannelInfoFromButton(btn, out reg, out ch, out ioName, out gridName)) return;
            if (!IsRemapSource(reg, ch)) return;

            var next = Services.IoRemapValidator.WithoutSource(_config.IoBackupChannelMappings, reg, ch);
            var changes = new Dictionary<string, string>();
            changes["IoBackupChannelMappings"] = Services.IoRemapValidator.Serialize(next);
            SettingsForm.PersistResult presult;
            string perror;
            if (!SettingsForm.PersistChanges(_config, changes, out presult, out perror))
            {
                UIMessageBox.Show(perror, "保存失败", UIStyle.Red, UIMessageBoxButtons.OK, true, 0);
                return;
            }
            AppendLog(string.Format("[映射] 已取消 {0}（0x{1:X4}@0x{2:X2}）的映射，剩余 {3} 条",
                ioName, reg, ch, next.Count));
            RefreshAfterMappingChange();
        }

        /// <summary>右键"查看映射去向"：复用点击已映射通道的悬浮提示（不阻塞，可继续操作）</summary>
        private void ViewRemapForButton()
        {
            CircleButton btn = (_remapMenu != null) ? _remapMenu.SourceControl as CircleButton : null;
            int reg, ch;
            string ioName, gridName;
            if (btn == null || !GetChannelInfoFromButton(btn, out reg, out ch, out ioName, out gridName)) return;
            if (!IsRemapSource(reg, ch)) return;
            var mapped = MapChannel(reg, ch);
            ShowRemapNotice(ioName, reg, ch, mapped.reg, mapped.bit);
        }

        /// <summary>
        /// 映射变更后刷新灯态：已连接就重读 10 个寄存器（按新映射解析，灯=真值）；
        /// 未连接只刷行标签（本地值没变，映射下次读写/读取时起效）。
        /// </summary>
        private void RefreshAfterMappingChange()
        {
            if (_closed || IsDisposed || Disposing) return;
            if (_connected)
            {
                ReadAllStatus();
            }
            else
            {
                _vacuumGrid.RefreshRowLabels();
                _carrierGrid.RefreshRowLabels();
            }
        }

        /// <summary>
        /// 非模态映射提示窗：WS_EX_NOACTIVATE + WS_EX_TOOLWINDOW，弹窗不激活、不抢焦点、
        /// 不进任务栏/AltTab；置顶显示并停留在屏幕右上角，直到用户手动关闭。
        /// 【V1.72.14】自带 FormClosed→Dispose：非模态 Close 不释放，不自释就是孤儿 Sunny 控件，
        /// GC 终结器线程 Dispose 读 Handle 即跨线程崩（R2 白名单 reuse 只能保"主窗在时不进终结"，
        /// 用户先×掉提示窗、主窗后关的窗口期仍会进终结；自释后此窗永不进终结）。
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

                // 非模态自释（UI 线程 FormClosed 里 Dispose，Sunny 控件永不在终结器线程释放）
                this.FormClosed += (s, e) => { try { this.Dispose(); } catch { } };
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
        /// 页签 0=负压开关测试，1=载台上电测试，2=预留点位测试（只遍历 DO）。
        /// </summary>
        private void StartSweep()
        {
            // 【V1.72.15】排队回调（状态定时断连/页签切换）关后进到这里即丢弃，不碰按钮/定时器。
            if (_closed || IsDisposed || Disposing) return;
            // 按当前页签确定要遍历的测试网格
            if (tabControl.SelectedIndex == 0)
            {
                _sweepGrid = _vacuumGrid;
                _sweepGridName = "负压开关测试";
            }
            else if (tabControl.SelectedIndex == 1)
            {
                _sweepGrid = _carrierGrid;
                _sweepGridName = "载台上电测试";
            }
            else
            {
                _sweepGrid = _spareGrid;
                _sweepGridName = "预留点位测试";
            }

            // 预留页无 DO 可遍历时（如配置把通道用满）直接提示，不启动定时器
            // （否则 SweepChannelCount=0 会在 worker 里除零）
            if (_sweepGrid.SweepChannelCount == 0)
            {
                UIMessageBox.Show("当前配置无可遍历通道（预留点位为空）。", "提示",
                    UIStyle.Orange, UIMessageBoxButtons.OK, true, 0);
                _sweepGrid = null;
                return;
            }

            _sweepActive = true;
            _sweepChannelIndex = 0;
            btnSweep.Text = "停止遍历";
            btnSweep.Style = UIStyle.Red;
            _sweepTimer.Start();
            AppendLog($"[遍历] 开始 {_sweepGridName} 一键遍历：{_sweepGrid.SweepChannelCount} 路通断跑马灯，每路点亮 500ms，循环检测");

            // 立即执行第一拍（后台线程），无需等待第一个 500ms 周期
            StartSweepStep();
        }

        /// <summary>
        /// 停止一键遍历：停定时器、恢复按钮外观，并把当前网格全部通道熄灭写回设备，
        /// 避免输出停留在半亮状态。
        /// </summary>
        private void StopSweep()
        {
            // 【V1.72.15】同上：关后（定时器已释放）StopSweep 再调 Stop/Text 即炸，只收标记。
            _sweepActive = false;
            if (_closed || IsDisposed || Disposing) { _sweepGrid = null; return; }
            try { _sweepTimer.Stop(); } catch { }
            try
            {
                btnSweep.Text = "一键遍历";
                btnSweep.Style = UIStyle.Purple;
            }
            catch { }

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
        /// 【V1.72.15】关后停硬件写：关闭瞬间在途的拍若继续写寄存器，等于"关了窗还在动设备"，
        /// 现每步查 _closed，关后整拍丢弃（设备保持关窗前状态，不残留半拍）。
        /// </summary>
        private void SweepStepWorker()
        {
            try
            {
                if (_closed || IsDisposed || Disposing) return;
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
                ISweepableGrid grid = _sweepGrid;

                // 1) 纯计算：全灭后只点亮第 idx 路的寄存器值（含备用映射 0x2009）
                int[] regs = grid.ComputeSweepRegisters(idx);

                // 2) 加锁写回设备（读-改-写保留共享字节/映射位）
                // 【V1.72.15】写前再查关闭：计算与写之间关窗即停手。
                if (_closed || IsDisposed || Disposing) return;
                grid.ApplySweepRegisters(regs);

                // 3) 读回 10 个寄存器，用于按钮实时反映真实通断
                ushort[] readBack = null;
                lock (_modbusLock)
                {
                    if (_closed) return;
                    if (_connected)
                    {
                        readBack = ReadRegs(0x2000, 10);
                    }
                }

                // 推进到下一路（单 worker 串行执行，无并发竞争）
                _sweepChannelIndex = (idx + 1) % grid.SweepChannelCount;

                // 切回 UI 线程：用读回的真实值刷新按钮状态 + 完成一圈提示
                int idxForLog = idx;
                int totalForLog = grid.SweepChannelCount;
                RunOnUi(() =>
                {
                    // 【V1.72.14】排队期间关窗即丢弃，不碰已释放的圆形灯按钮/提示窗。
                    if (_closed || IsDisposed || Disposing) return;
                    if (_sweepGrid == null) return;
                    try
                    {
                        if (readBack != null && readBack.Length >= 10)
                        {
                            int[] vals = new int[10];
                            for (int i = 0; i < 10; i++) vals[i] = readBack[i] & 0xFFFF;
                            _sweepGrid.SetButtonsFromRegisters(vals);
                        }
                    }
                    catch { return; }
                    if (idxForLog == 0)
                    {
                        AppendLog($"[遍历] {_sweepGridName} 完成一整圈（{totalForLog} 路通断检测），继续下一圈...");
                    }

                    // 当前点亮的通道若已映射到备用通道：按钮仍显示旧通道名，
                    // 但弹窗告知用户实际输出的通道是哪一个（现场一眼可辨，避免误以为原通道在工作）
                    if (grid.GetSweepChannelLocation(idxForLog, out int litAbsReg, out int litCh)
                        && IsRemapSource(litAbsReg, litCh))
                    {
                        (int dstReg, int dstBit) = MapChannel(litAbsReg, litCh);
                        ShowRemapNotice(grid.GetSweepChannelName(idxForLog), litAbsReg, litCh, dstReg, dstBit);
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
        /// 【V1.80】切进预留页时自动刷新一次（DI 灯平时不轮询，进页看最新）。
        /// </summary>
        private void tabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_sweepActive)
            {
                StopSweep();
                AppendLog("[遍历] 检测到切换页签，已停止遍历");
            }
            if (!_closed && tabControl.SelectedIndex == 2 && _connected && !_sweepActive)
            {
                RefreshSparePage();
            }
        }

        /// <summary>
        /// 刷新预留页（V1.80）：DO 灯按输出区快照同步 + DI 灯实时读；切进预留页与
        /// "读取状态"共用。未连接时静默返回（灯保持旧态，不误报全灭）。
        /// </summary>
        private void RefreshSparePage()
        {
            if (_closed || IsDisposed || Disposing || !_connected) return;
            lock (_modbusLock)
            {
                if (!_connected) return;
                int regCount = (_config.TotalOutputs + 15) / 16;
                ushort[] regs = ReadRegs((ushort)_config.IoOutputRegisterStartAddress, (ushort)regCount);
                if (regs == null || regs.Length < regCount)
                {
                    AppendLog("[读取] 预留输出快照读取失败（连接已断开）");
                    return;
                }
                var values = new int[regs.Length];
                for (int i = 0; i < regs.Length; i++) values[i] = regs[i] & 0xFFFF;
                _spareGrid.SetButtonsFromRegisters(values);
                _spareGrid.RefreshDiInputs();
                AppendLog($"[读取] 预留点位已刷新（DO 快照 {regCount} 个寄存器 + DI 实时）");
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
        /// 【V1.72.14】旧版用同步 Invoke：关闭瞬间后台拍 Invoke 进已销毁句柄即炸"线程间操作无效"
        /// （非 UI 线程弹窗）；现改异步 BeginInvoke + 关闭/句柄三查 + 全程 try，后台收尾日志丢了就丢了，
        /// 不炸框；UI 线程直写分支同样包 try（关窗竞态下 txtLog 已释放）。
        /// </summary>
        public void AppendLog(string message)
        {
            if (_closed || IsDisposed || Disposing) return;
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            try
            {
                if (txtLog == null || txtLog.IsDisposed) return;
                if (!IsHandleCreated || !txtLog.IsHandleCreated) return;
            }
            catch { return; }
            if (this.txtLog.InvokeRequired)
            {
                try { this.txtLog.BeginInvoke(new Action<string>(AppendLogInner), line); }
                catch { }
                return;
            }

            AppendLogInner(line);
        }

        /// <summary>日志真正落盘（必在 UI 线程调；AppendLog 已分流，后台不直调这里）。</summary>
        private void AppendLogInner(string line)
        {
            if (_closed || IsDisposed || Disposing) return;
            try
            {
                if (txtLog == null || txtLog.IsDisposed) return;
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
            catch { }
        }

        // ========================================================================
        //  一键遍历接口（V1.80 新增）：三个测试网格（负压 / 载台 / 预留 DO）统一抽象，
        //  SweepStepWorker 只认接口，不认具体网格类。
        //  - ComputeSweepRegisters / ApplySweepRegisters 传的是"本网格自解释负载"：
        //    ChannelGrid 用 6 元数组（[0~4]=业务寄存器，[5]=备用映射目标）；
        //    SpareGrid 用 2 元数组（[0]=寄存器地址，[1]=该寄存器期望值）；
        //    生产消费同一实现，不混用。
        // ========================================================================
        public interface ISweepableGrid
        {
            /// <summary>一键遍历总路数（负压/载台=72，预留 DO=配置点数，默认 16）</summary>
            int SweepChannelCount { get; }

            /// <summary>第 index 路的显示通道名（如 Y000 / Y220，仅日志/提示用）</summary>
            string GetSweepChannelName(int index);

            /// <summary>纯计算（可在后台线程调用）：模拟"全灭后只点亮第 index 路"的寄存器负载</summary>
            int[] ComputeSweepRegisters(int index);

            /// <summary>加锁写回设备（读-改-写保留共享位；未连接/关窗后静默丢弃，不逐条记日志）</summary>
            void ApplySweepRegisters(int[] regs);

            /// <summary>按读回的 DO 区寄存器快照刷新按钮 ON/OFF（与读快照同基址：0x2000 起或配置输出起始）</summary>
            void SetButtonsFromRegisters(int[] values);

            /// <summary>遍历停止时全灭并写回设备</summary>
            void SweepAllOff();

            /// <summary>第 index 路的物理位置（寄存器地址 + 通道号 0~15），供映射提示用</summary>
            bool GetSweepChannelLocation(int index, out int absReg, out int channel);
        }

        // ========================================================================
        //  通道网格类：管理某一个测试（9 行 × 8 列圆形灯按钮）的映射表、按钮、
        //  寄存器值与行标签，以及点击写入逻辑。两个测试各自持有一个实例。
        // ========================================================================
        public sealed class ChannelGrid : ISweepableGrid
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
            /// 由按钮反查通道信息（右键菜单用）：引用比对确认按钮归属本网格，
            /// 再算出寄存器/通道/IO 名。行列号三页重叠，只能用引用认，不认号。
            /// </summary>
            /// <returns>true=是本网格的按钮，false=不是（调用方继续问下一个网格）</returns>
            public bool TryGetChannelInfo(CircleButton btn, out int reg, out int ch, out string ioName)
            {
                reg = 0;
                ch = 0;
                ioName = null;
                if (btn == null || Buttons == null) return false;
                for (int r = 0; r < 9; r++)
                {
                    for (int c = 0; c < 8; c++)
                    {
                        if (!ReferenceEquals(Buttons[r, c], btn)) continue;
                        reg = RegAddresses[RowToRegIndex[r]];
                        ch = CommunicationTestForm.ChannelOf(btn.BitValue);
                        ioName = RowIoNames[r, c];
                        return true;
                    }
                }
                return false;
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

            /// <summary>一键遍历总路数（9×8=72）</summary>
            public int SweepChannelCount => 72;

            /// <summary>第 index 路的通道名（index = row*8+col）</summary>
            public string GetSweepChannelName(int index) => RowIoNames[index / 8, index % 8];

            /// <summary>一键遍历用：线性序号版（转调行列版，行为一致）</summary>
            public int[] ComputeSweepRegisters(int index) => ComputeSweepRegisters(index / 8, index % 8);

            /// <summary>
            /// 后台线程安全地写遍历寄存器（加锁串行化）：按本网格 OwnedMask 读-改-写，每寄存器一次，
            /// 并下发备用映射目标 0x2009。不逐条写日志（避免 500ms 一拍刷屏）。
            /// 【V1.80】原窗体 WriteSweepRegisters 下沉到网格：三个网格各管各的写语义，worker 只认接口。
            /// </summary>
            public void ApplySweepRegisters(int[] regs)
            {
                // 【V1.72.15】关后停硬件写（与 SweepStepWorker 双保险）。
                if (_owner._closed || _owner.IsDisposed || _owner.Disposing) return;
                lock (_owner._modbusLock)
                {
                    if (!_owner._connected) return;

                    for (int i = 0; i < 5; i++)
                    {
                        // 本网格不拥有的寄存器跳过（如 0x2000 对载台电网格 OwnedMask=0）
                        if (OwnedMask[i] == 0) continue;

                        int addr = RegAddresses[i];
                        int ownedMask = OwnedMask[i];
                        ushort writeValue;
                        if (ownedMask == 0xFFFF)
                        {
                            // 整寄存器归本网格所有，直接写
                            writeValue = (ushort)(regs[i] & 0xFFFF);
                        }
                        else
                        {
                            // 共享寄存器读-改-写：保留对方测试拥有的字节
                            ushort[] cur = _owner.ReadRegs((ushort)addr, 1);
                            ushort current = (cur != null && cur.Length > 0) ? cur[0] : (ushort)0;
                            writeValue = (ushort)((current & ~ownedMask) | (regs[i] & ownedMask));
                        }
                        if (!_owner.WriteReg((ushort)addr, writeValue))
                        {
                            return;   // 断开：交给状态定时器统一处理
                        }
                    }

                    // 备用映射目标寄存器（0x2009）一并下发：读-改-写保留另一测试的映射位
                    _owner.WriteBackupRegister(this, regs[5]);
                }
            }

            /// <summary>第 index 路的物理位置（寄存器地址 + 通道号 0~15），供映射提示用</summary>
            public bool GetSweepChannelLocation(int index, out int absReg, out int channel)
            {
                int row = index / 8, col = index % 8;
                absReg = RegAddresses[RowToRegIndex[row]];
                channel = CommunicationTestForm.ChannelOf(RowBitValues[row, col]);
                return true;
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
        //  预留点位网格（V1.80 新增）：上半预留 DI 只读状态灯 + 下半预留 DO 手动点动。
        //
        //  【点位来源】构造时按 IoMapBuilder.Build(config) 取 Function=Unknown 的点，
        //  地址/路数全部来自映射表（默认 DI=73~80/X110~X117 共 8 路，
        //  DO=145~160/Y220~Y237 共 16 路），改 TotalInputs/TotalOutputs 配置自动适应；
        //  无预留时对应区显示"无预留"空态，窗体照常打开。
        //
        //  【DI 只读灯】状态经 DeviceManager.GetAllInputs()（FC0x04，与采集同源）刷新，
        //  灯复用 CircleButton（Enabled=false 禁点击，纯看）；刷新入口只有两个：
        //  "读取状态"按钮 / 切进预留页自动刷新——状态定时器坚持不发报文，不管 DI。
        //
        //  【DO 可点灯】点击 toggle + 读-改-写。所在寄存器（默认 0x2009）同时是
        //  备用映射目标（问题确认清单#106 备案的双重身份），映射启用时按下式保位，
        //  两边写同一寄存器互不覆盖；映射关闭时 preserve=0 即整寄存器直写。
        // ========================================================================
        public sealed class SpareGrid : ISweepableGrid
        {
            private readonly CommunicationTestForm _owner;
            private readonly DeviceConfig _config;

            /// <summary>预留输入点（IoId 升序，来自 IoMapBuilder，Function=Unknown）</summary>
            private readonly List<IoPointDefinition> _spareInputs = new List<IoPointDefinition>();

            /// <summary>预留输出点（IoId 升序）</summary>
            private readonly List<IoPointDefinition> _spareOutputs = new List<IoPointDefinition>();

            /// <summary>DI 状态灯（与 _spareInputs 一一对应，只读）</summary>
            private CircleButton[] _diLamps = new CircleButton[0];

            /// <summary>DO 点动灯（与 _spareOutputs 一一对应，可点）</summary>
            private CircleButton[] _doButtons = new CircleButton[0];

            /// <summary>
            /// 构造预留网格：在两个面板里建灯并写好标题行。
            /// </summary>
            /// <param name="owner">所属窗体（连接/读写/映射查询/日志都走它）</param>
            /// <param name="diPanel">预留 DI 灯容器</param>
            /// <param name="doPanel">预留 DO 灯容器</param>
            /// <param name="diTitle">DI 区标题行（显示实际路数/地址段）</param>
            /// <param name="doTitle">DO 区标题行</param>
            public SpareGrid(CommunicationTestForm owner, UIPanel diPanel, UIPanel doPanel,
                UILabel diTitle, UILabel doTitle)
            {
                _owner = owner;
                _config = owner._config;

                // 点位表：只取预留（Function=Unknown），输入/输出分开（IoMapBuilder 已按 IoId 升序）
                try
                {
                    var map = IoMapBuilder.Build(_config);
                    foreach (var p in map)
                    {
                        if (p.Function != IoFunction.Unknown) continue;
                        if (p.Type == IoType.Input) _spareInputs.Add(p);
                        else _spareOutputs.Add(p);
                    }
                }
                catch
                {
                    // 配置非法时 Build 抛异常：两区留空显示"无预留"，窗体照常打开，
                    // 用户去系统设置把 TotalInputs/TotalOutputs 改合法再进
                }

                BuildLamps(diPanel, isInput: true);
                BuildLamps(doPanel, isInput: false);
                RefreshTitles(diTitle, doTitle);

                // 映射启用时所在寄存器双重身份：提示一次，后续读写自动保位（见 PreserveMaskFor）
                if (_config.IoBackupChannelMappingEnabled && _spareOutputs.Count > 0)
                {
                    _owner.AppendLog("[预留] 备用映射已启用：预留 DO 与映射目标共用寄存器，写入自动保位互不覆盖");
                }
            }

            /// <summary>由 Io 点定义反推寄存器地址与位（与 ModbusTcpIoController 口径一致：
            /// 输入 IoId=1→起始+0/bit0，输出 TotalInputs+1→输出起始+0/bit0）</summary>
            private void RegBitOf(IoPointDefinition p, out int reg, out int bit)
            {
                if (p.Type == IoType.Input)
                {
                    int bitIndex = p.IoId - 1;
                    reg = _config.IoInputRegisterStartAddress + bitIndex / 16;
                    bit = bitIndex % 16;
                }
                else
                {
                    int outIndex = p.IoId - 1 - _config.TotalInputs;
                    reg = _config.IoOutputRegisterStartAddress + outIndex / 16;
                    bit = outIndex % 16;
                }
            }

            /// <summary>动态建灯：按寄存器分组（地址升序），组内按位升序，每 8 位一排；
            /// 行标签"0x地址 高/低字节 + 首~尾通道名"，与另两页同观感。无点位时显示空态。</summary>
            private void BuildLamps(UIPanel panel, bool isInput)
            {
                const int buttonSize = 56;
                const int gapX = 8;
                const int gapY = 14;
                const int rowLabelWidth = 210;
                const int gridLeft = 12;
                const int gridTop = 6;

                var points = isInput ? _spareInputs : _spareOutputs;
                if (points.Count == 0)
                {
                    var empty = new UILabel
                    {
                        AutoSize = false,
                        Size = new Size(panel.Width - 24, 40),
                        Location = new Point(gridLeft, gridTop + 10),
                        Text = isInput
                            ? $"当前配置无预留输入（TotalInputs={_config.TotalInputs}，通道已用满）"
                            : $"当前配置无预留输出（TotalOutputs={_config.TotalOutputs}，通道已用满）",
                        TextAlign = ContentAlignment.MiddleLeft,
                        Font = new Font("微软雅黑", 10.5F),
                        ForeColor = Color.Gray
                    };
                    panel.Controls.Add(empty);
                    return;
                }

                // 下标按 (寄存器, 位) 排序后分排：同寄存器连续 8 位一排
                var order = new List<int>();
                for (int i = 0; i < points.Count; i++) order.Add(i);
                order.Sort((a, b) =>
                {
                    RegBitOf(points[a], out int ra, out int ba);
                    RegBitOf(points[b], out int rb, out int bb);
                    int c = ra.CompareTo(rb);
                    return c != 0 ? c : ba.CompareTo(bb);
                });
                var rows = new List<List<int>>();
                var rowRegs = new List<int>();
                var rowLowByte = new List<bool>();
                int lastReg = -1, lastChunk = -1;
                foreach (int i in order)
                {
                    RegBitOf(points[i], out int reg, out int bit);
                    int chunk = bit / 8;
                    if (reg != lastReg || chunk != lastChunk || rows[rows.Count - 1].Count >= 8)
                    {
                        rows.Add(new List<int>());
                        rowRegs.Add(reg);
                        rowLowByte.Add(chunk == 0);
                        lastReg = reg;
                        lastChunk = chunk;
                    }
                    rows[rows.Count - 1].Add(i);
                }

                // 顶部列号（1~8，与另两页一致）
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
                    colHeader.BackColor = panel.BackColor;
                    panel.Controls.Add(colHeader);
                }

                var buttons = new CircleButton[points.Count];
                for (int r = 0; r < rows.Count; r++)
                {
                    int y = gridTop + 28 + r * (buttonSize + gapY);

                    Label rowLabel = new Label();
                    rowLabel.AutoSize = false;
                    rowLabel.Size = new Size(rowLabelWidth, buttonSize);
                    rowLabel.Location = new Point(gridLeft, y);
                    rowLabel.TextAlign = ContentAlignment.MiddleLeft;
                    rowLabel.Font = new Font("宋体", 9.5F, FontStyle.Regular);
                    rowLabel.ForeColor = Color.Black;
                    rowLabel.BackColor = Color.FromArgb(245, 245, 245);
                    rowLabel.BorderStyle = BorderStyle.FixedSingle;
                    int first = rows[r][0], last = rows[r][rows[r].Count - 1];
                    rowLabel.Text = string.Format("0x{0:X4} {1}\n{2}~{3}",
                        rowRegs[r], rowLowByte[r] ? "低字节" : "高字节",
                        points[first].PhysicalAddress, points[last].PhysicalAddress);
                    panel.Controls.Add(rowLabel);

                    for (int c = 0; c < rows[r].Count; c++)
                    {
                        int pi = rows[r][c];
                        CircleButton btn = new CircleButton();
                        btn.Size = new Size(buttonSize, buttonSize);
                        btn.Location = new Point(gridLeft + rowLabelWidth + c * (buttonSize + gapX), y);
                        btn.Text = points[pi].PhysicalAddress;
                        btn.Row = pi;
                        btn.Col = c;
                        RegBitOf(points[pi], out int br, out int bb);
                        btn.BitValue = 1 << bb;
                        if (isInput)
                        {
                            // 只读灯：禁点击（不触发 Click），鼠标保持默认箭头
                            btn.Enabled = false;
                            btn.Cursor = Cursors.Default;
                        }
                        else
                        {
                            btn.Click += (s, e2) => OnDoClick((CircleButton)s);
                        }
                        buttons[pi] = btn;
                        panel.Controls.Add(btn);
                    }
                }

                if (isInput) _diLamps = buttons;
                else _doButtons = buttons;
            }

            /// <summary>标题行写实际路数/地址段（配置自适应，构造时一次性写好）</summary>
            private void RefreshTitles(UILabel diTitle, UILabel doTitle)
            {
                diTitle.Text = _spareInputs.Count == 0
                    ? "预留输入监视（只读）：当前配置无预留输入"
                    : string.Format("预留输入监视（只读，共{0}路：{1}~{2}，随读取状态/进页刷新）",
                        _spareInputs.Count,
                        _spareInputs[0].PhysicalAddress,
                        _spareInputs[_spareInputs.Count - 1].PhysicalAddress);
                if (_spareOutputs.Count == 0)
                {
                    doTitle.Text = "预留输出测试（可点）：当前配置无预留输出";
                    return;
                }
                var regs = DistinctDoRegs();
                string regText = regs.Count == 1
                    ? string.Format("@0x{0:X4}", regs[0])
                    : string.Format("@{0}个寄存器(0x{1:X4}起)", regs.Count, regs[0]);
                doTitle.Text = string.Format("预留输出测试（可点，共{0}路：{1}~{2} {3}）",
                    _spareOutputs.Count,
                    _spareOutputs[0].PhysicalAddress,
                    _spareOutputs[_spareOutputs.Count - 1].PhysicalAddress,
                    regText);
            }

            /// <summary>预留 DO 所在的不同寄存器地址（升序）</summary>
            private List<int> DistinctDoRegs()
            {
                var regs = new List<int>();
                foreach (var p in _spareOutputs)
                {
                    RegBitOf(p, out int reg, out int bit);
                    if (!regs.Contains(reg)) regs.Add(reg);
                }
                regs.Sort();
                return regs;
            }

            /// <summary>
            /// 某寄存器的映射保位掩码：备用映射启用时，目标落在本寄存器的映射目标位必须保留
            /// （预留 DO 与映射目标共用寄存器，两边写互不覆盖）；关闭时返回 0 即整寄存器直写。
            /// </summary>
            private int PreserveMaskFor(int reg)
            {
                if (!_config.IoBackupChannelMappingEnabled) return 0;
                return ComputePreserveMask(_config.IoBackupChannelMappings, reg);
            }

            /// <summary>
            /// 纯函数：由映射表算某目标寄存器的保位掩码（目标落该寄存器的映射目标位 OR）。
            /// null/非法通道一律跳过，返回 0 即"无位需保"——调用方直写。
            /// </summary>
            public static int ComputePreserveMask(List<IoOutputChannelRemap> mappings, int targetReg)
            {
                if (mappings == null) return 0;
                int mask = 0;
                foreach (var m in mappings)
                {
                    if (m == null || m.TargetRegister != targetReg) continue;
                    if (m.TargetChannel < 0 || m.TargetChannel > 15) continue;
                    mask |= (1 << m.TargetChannel);
                }
                return mask;
            }

            /// <summary>
            /// 纯函数：读-改-写合并值 =（现值 & 保位）|（期望 & ~保位）。
            /// 保位=0 时即期望直写；断读（现值取 0）时保位退化为 0，比写错映射位安全。
            /// </summary>
            public static int ComputeWriteValue(int current, int desired, int preserveMask)
            {
                return (current & preserveMask) | (desired & ~preserveMask);
            }

            /// <summary>DO 灯点击：toggle + 读-改-写所在寄存器（只写这一路所在的寄存器）</summary>
            private void OnDoClick(CircleButton btn)
            {
                if (btn == null) return;
                int idx = btn.Row;
                if (idx < 0 || idx >= _spareOutputs.Count) return;
                var p = _spareOutputs[idx];
                RegBitOf(p, out int reg, out int bit);

                // 与另两网格一致：被映射走的源通道先提示实际输出通道
                if (_owner.IsRemapSource(reg, bit))
                {
                    (int dstReg, int dstBit) = _owner.MapChannel(reg, bit);
                    _owner.ShowRemapNotice(p.PhysicalAddress, reg, bit, dstReg, dstBit);
                }

                btn.IsOn = !btn.IsOn;
                WriteDoRegisters(reg);
            }

            /// <summary>写单个预留 DO 寄存器：期望=本寄存器内 ON 灯的位 OR（被映射走的源位剔除），
            /// 映射保位保留，未连接/失败只记日志不抛。</summary>
            private void WriteDoRegisters(int reg)
            {
                // 【V1.72.15】关后停硬件写
                if (_owner._closed || _owner.IsDisposed || _owner.Disposing) return;
                if (!_owner._connected)
                {
                    _owner.AppendLog($"[警告] 未连接，无法写入预留输出。请先点击“连接测试”。(0x{reg:X4})");
                    return;
                }
                lock (_owner._modbusLock)
                {
                    if (!_owner._connected)
                    {
                        _owner.AppendLog($"[警告] 未连接，无法写入预留输出。请先点击“连接测试”。(0x{reg:X4})");
                        return;
                    }
                    int desired = 0;
                    for (int i = 0; i < _spareOutputs.Count; i++)
                    {
                        RegBitOf(_spareOutputs[i], out int r, out int b);
                        if (r != reg || !_doButtons[i].IsOn) continue;
                        if (_owner.IsRemapSource(r, b)) continue;   // 被映射走的源位不写源寄存器
                        desired |= (1 << b);
                    }
                    int preserve = PreserveMaskFor(reg);
                    ushort[] cur = _owner.ReadRegs((ushort)reg, 1);
                    ushort current = (cur != null && cur.Length > 0) ? cur[0] : (ushort)0;
                    int w = ComputeWriteValue(current, desired, preserve);
                    if (!_owner.WriteReg((ushort)reg, (ushort)w))
                    {
                        _owner.AppendLog($"[错误] 写入 0x{reg:X4} 失败（连接已断开，主程序后台自动重连中）");
                        return;
                    }
                    _owner.AppendLog($"[写入] 预留输出  0x{reg:X4} = 0x{w:X4}");
                }
            }

            /// <summary>
            /// 刷新 DI 灯（必在 UI 线程调：碰 IsOn→Invalidate）。
            /// 未连接/读失败时灯保持旧态并记日志，不误报全灭。
            /// </summary>
            public void RefreshDiInputs()
            {
                if (_owner._closed || _owner.IsDisposed || _owner.Disposing) return;
                if (_spareInputs.Count == 0) return;
                if (!_owner._connected) return;
                bool[] all;
                try { all = _owner._deviceManager.GetAllInputs(); }
                catch { all = null; }
                if (all == null || all.Length < _config.TotalInputs)
                {
                    _owner.AppendLog("[警告] 预留输入读取失败（返回点数不足），状态灯保持上次值");
                    return;
                }
                for (int i = 0; i < _spareInputs.Count; i++)
                {
                    int ioId = _spareInputs[i].IoId;
                    _diLamps[i].IsOn = ioId >= 1 && ioId <= all.Length && all[ioId - 1];
                }
            }

            /// <summary>全部 DO 灯本地熄灭（不写设备：写设备由 AllOff 的清零循环统一处理）</summary>
            public void ClearDoButtons()
            {
                foreach (var b in _doButtons) b.IsOn = false;
            }

            /// <summary>给全部 DO 灯挂右键菜单（DI 只读灯不挂；与另两网格同一份菜单）</summary>
            public void AttachDoContextMenu(ContextMenuStrip menu)
            {
                if (_doButtons == null || menu == null) return;
                foreach (var b in _doButtons)
                {
                    if (b != null) b.ContextMenuStrip = menu;
                }
            }

            /// <summary>
            /// 由按钮反查 DO 通道信息（右键菜单用）：引用比对确认归属，
            /// 再由点位表算出寄存器/通道/IO 名。
            /// </summary>
            /// <returns>true=是本网格的 DO 灯，false=不是</returns>
            public bool TryGetDoChannelInfo(CircleButton btn, out int reg, out int ch, out string ioName)
            {
                reg = 0;
                ch = 0;
                ioName = null;
                if (btn == null || _doButtons == null) return false;
                for (int i = 0; i < _doButtons.Length; i++)
                {
                    if (!ReferenceEquals(_doButtons[i], btn)) continue;
                    if (i < 0 || i >= _spareOutputs.Count) return false;
                    var p = _spareOutputs[i];
                    RegBitOf(p, out reg, out ch);
                    ioName = p.PhysicalAddress;
                    return true;
                }
                return false;
            }

            // ===================== ISweepableGrid（一键遍历只走 DO，DI 驱动不了） =====================

            /// <summary>一键遍历总路数 = 预留 DO 路数（默认 16）</summary>
            public int SweepChannelCount => _spareOutputs.Count;

            /// <summary>第 index 路的通道名（如 Y220）</summary>
            public string GetSweepChannelName(int index) => _spareOutputs[index].PhysicalAddress;

            /// <summary>
            /// 纯计算：模拟"全灭后只点亮第 index 路"——返回 2 元负载 [寄存器地址, 该寄存器期望值]；
            /// 被映射走的源位期望为 0（信号走映射目标，不从源寄存器出）。
            /// </summary>
            public int[] ComputeSweepRegisters(int index)
            {
                RegBitOf(_spareOutputs[index], out int reg, out int bit);
                int value = _owner.IsRemapSource(reg, bit) ? 0 : (1 << bit);
                return new int[] { reg, value };
            }

            /// <summary>加锁写回设备：单寄存器读-改-写（保映射位）；未连接/关窗后静默丢弃</summary>
            public void ApplySweepRegisters(int[] regs)
            {
                // 【V1.72.15】关后停硬件写（与 SweepStepWorker 双保险）。
                if (_owner._closed || _owner.IsDisposed || _owner.Disposing) return;
                lock (_owner._modbusLock)
                {
                    if (!_owner._connected) return;
                    int reg = regs[0];
                    int desired = regs[1] & 0xFFFF;
                    int preserve = PreserveMaskFor(reg);
                    ushort[] cur = _owner.ReadRegs((ushort)reg, 1);
                    ushort current = (cur != null && cur.Length > 0) ? cur[0] : (ushort)0;
                    _owner.WriteReg((ushort)reg, (ushort)ComputeWriteValue(current, desired, preserve));
                }
            }

            /// <summary>按读回的 DO 区快照刷新 DO 灯（values 基址=配置输出起始；越界灯保持旧态）</summary>
            public void SetButtonsFromRegisters(int[] values)
            {
                if (values == null) return;
                int outputBase = _config.IoOutputRegisterStartAddress;
                for (int i = 0; i < _spareOutputs.Count; i++)
                {
                    RegBitOf(_spareOutputs[i], out int reg, out int bit);
                    if (_owner.IsRemapSource(reg, bit))
                    {
                        // 被映射走的源通道：灯跟映射目标位（与 ChannelGrid 一致）
                        (int dstReg, int dstBit) = _owner.MapChannel(reg, bit);
                        int dstIdx = dstReg - outputBase;
                        if (dstIdx >= 0 && dstIdx < values.Length)
                            _doButtons[i].IsOn = (values[dstIdx] & (1 << dstBit)) != 0;
                        continue;
                    }
                    int idx = reg - outputBase;
                    if (idx < 0 || idx >= values.Length) continue;
                    _doButtons[i].IsOn = (values[idx] & (1 << bit)) != 0;
                }
            }

            /// <summary>遍历停止时全灭并写回设备（未连接只灭本地灯）</summary>
            public void SweepAllOff()
            {
                foreach (var b in _doButtons) b.IsOn = false;
                if (!_owner._connected) return;
                foreach (int reg in DistinctDoRegs())
                    WriteDoRegisters(reg);
            }

            /// <summary>第 index 路的物理位置（寄存器地址 + 通道号 0~15）</summary>
            public bool GetSweepChannelLocation(int index, out int absReg, out int channel)
            {
                RegBitOf(_spareOutputs[index], out absReg, out channel);
                return true;
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
