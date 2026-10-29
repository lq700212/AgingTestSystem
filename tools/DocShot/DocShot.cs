// DocShot v2 —— 说明文档截图通用 harness（带 Mock 生产数据版）。
//
// 【通用三规则（以后封装"使用说明文档编写 skill"直接照抄）】
// R1 数据走生产路径：Mock 设备 + DeviceManager 公开 API 驱动真实状态机 +
//    TestEventLogger 真实落盘。禁止反射伪造界面文本；
//    唯一例外是"模拟用户打字"（登录密码/批号/配方名），那本就是用户输入。
// R2 截图方式按窗体类型二选一：
//    PrintWindow ＝ 标准对话框（设置表/配方窗/测试窗，渲染完整）；
//    TOPMOST 置顶 + CopyFromScreen ＝ 自绘画布（主界面网格/工艺策略画布，
//    PrintWindow 在滚动后丢 GDI 文字，见 winforms-ui-debug 坑 43）。
// R3 每张拍完做非空校验（内容区去标题栏采样 distinct 颜色数），FAIL 自动重拍一次，
//    再 FAIL 就报出来，绝不让空白图混进文档。
//
// 【环境隔离】当天 TestLog CSV 先备份→拍完还原；截图输出到 docs/images。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using AgingTestSystem.Models;
using AgingTestSystem.Services;
using AgingTestSystem.Interfaces;
using AgingTestSystem.Dialogs;
using AgingTestSystem.Views;

// 定压模拟表：每台恒 -8.0kPa（好真空），无随机坏读数。
// 【为什么不用 MockBarometerReader】Mock 每周期 15% 坏读数，500ms 采集下十几秒内
// 所有在测工位几乎必报警，拍不出"老化中/已完成"；定压版让状态机 deterministic：
// 好阈值（-5）→ 抽真空→上电→完成；苛刻阈值（-9.5）→ 首轮即压力报警（演故障）。
sealed class SteadyReader : IBarometerReader
{
    private bool _connected;
    private int _total = 72;
    public bool IsConnected { get { return _connected; } }
    public string CurrentPortName { get { return "COM9"; } }
    // V1.103 接口新增逐台进度回调：截图工具瞬间读完，用空实现保编译一致。
    public Action<int, BarometerData> SingleReadCallback { get; set; }
    public event EventHandler<string> OnError;
    public bool Connect(DeviceConfig config)
    {
        if (config == null) return false;
        _total = config.TotalBarometers;
        _connected = true;
        return true;
    }
    public void Disconnect() { _connected = false; }
    public BarometerData ReadData(int deviceId)
    {
        if (!_connected || deviceId < 1 || deviceId > _total) return null;
        return new BarometerData
        {
            DeviceId = deviceId,
            VacuumPressure = -8.0m,
            SerialNumber = string.Empty,
            RecipeName = string.Empty,
            Status = DeviceStatus.Idle,
            CollectTime = DateTime.Now,
            InputStatus = new bool[] { true },
            OutputStatus = new bool[] { false, false }
        };
    }
    public BarometerData[] ReadAllData()
    {
        var arr = new BarometerData[_total];
        for (int i = 0; i < _total; i++) arr[i] = ReadData(i + 1);
        return arr;
    }
    public bool SetThreshold(int deviceId, decimal thresholdValue) { return _connected; }
    public System.Collections.Generic.Dictionary<int, bool> SetAllThresholds(decimal thresholdValue)
    {
        var d = new System.Collections.Generic.Dictionary<int, bool>();
        for (int i = 1; i <= _total; i++) d[i] = true;
        return d;
    }
}

static class DocShot
{
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint f);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    struct RECT { public int Left, Top, Right, Bottom; }

    // ---- 真屏截图防抢屏：遮挡检测 ＋ 定向最小化 ----
    // （2026-10-29 双图翻车沉淀：01-main/18-ioremap 两张真屏截图拍进了 OpenCode
    // 窗口内容——不能按"目标窗必然在前台"截图。TopMost 只保证压住非 topmost 窗；
    // 遮挡者自己也是置顶窗时（或焦点被抢的同层窗），焦点窗永远在上，目标窗照样被盖。
    // Correct做法是拍前实测"谁与截图矩形相交"：只与目标窗矩形相交的外部进程可见窗
    // 最小化（IsIconic 的不动，用户本来就收着的不管），登记起来拍完统一恢复。
    // 不相交的窗口一概不碰——别的进程窗口只需为挡路的负责，不必全屏清场。
    // PrintWindow 截图走窗口自身 DC，不经过屏幕，不需要这个保护。）
    static readonly System.Collections.Generic.HashSet<IntPtr> minimizedOthers =
        new System.Collections.Generic.HashSet<IntPtr>();
    const int SW_MINIMIZE = 6, SW_RESTORE = 9;

    static void MinimizeBlockers(Form f)
    {
        try
        {
            RECT tr; GetWindowRect(f.Handle, out tr);
            uint me = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            EnumWindows((h, l) =>
            {
                try
                {
                    uint pid; GetWindowThreadProcessId(h, out pid);
                    if (pid == me || !IsWindowVisible(h) || IsIconic(h)) return true;
                    var cls = new System.Text.StringBuilder(64);
                    GetClassName(h, cls, 64);
                    string c = cls.ToString();
                    // 系统表面不动：任务栏/桌面/Alt+Tab 切换条/提示层
                    if (c == "Shell_TrayWnd" || c == "Progman" || c == "WorkerW"
                        || c == "MSCTFIME UI" || c == "Tooltips_class32"
                        || c.StartsWith("Windows.UI")) return true;
                    RECT wr; GetWindowRect(h, out wr);
                    // 只处理"真的盖住截图区"的窗口：矩形相交才最小化
                    bool intersect = wr.Left < tr.Right && tr.Left < wr.Right
                        && wr.Top < tr.Bottom && tr.Top < wr.Bottom;
                    if (!intersect) return true;
                    ShowWindow(h, SW_MINIMIZE);
                    minimizedOthers.Add(h);
                    var cap = new System.Text.StringBuilder(128);
                    GetWindowText(h, cap, 128);
                    Console.WriteLine("BLOCKER minimized: '" + cap + "' class=" + c);
                }
                catch { }
                return true;
            }, IntPtr.Zero);
            Application.DoEvents();
            Thread.Sleep(150); // 最小化动画收尾，避免半路截到中间态
        }
        catch { }
    }

    static void RestoreBlockers()
    {
        foreach (var h in minimizedOthers)
        {
            try { ShowWindow(h, SW_RESTORE); }
            catch { }
        }
        minimizedOthers.Clear();
    }

    static string outDir = @"E:\Project\AgingTestSystem\docs\images";
    static int failCount = 0;

    // ---- 防卡住三层防御（截图 harness 通用，以后 skill 照抄） ----
    // 第 1 层（主要）：harness 关窗一律直接 Dispose，不调 Close()。
    //   Close 会走 FormClosing 确认框（ID 绑定"有未保存先确认"、设置表"改选同步换"等），
    //   无人值守下直接卡死；Dispose 跳过 FormClosing，直达资源释放，截图流程不需要优雅关闭。
    // 第 2 层（兜底）：看门狗线程，每 300ms 扫描本进程的"意外弹窗"并按安全策略自动点掉。
    //   判定意外弹窗：可见顶层窗 + （对话框类 #32770，或有 Owner 的 WinForms 窗），
    //   且不在"harness 自己打开的窗体"名单里。
    //   点按钮策略（非破坏优先）：有"否/取消"点它（保持现状）→ 否则"确定/是"点它 →
    //   都没有则 WM_CLOSE。每次动作都打印日志，事后人类复查根因。
    //   前提假设：截图流程里不存在"必须人工点的模态框"（我们从不点启动/保存类按钮），
    //   所以冒出来的任何模态都是事故，一律消灭。
    // 第 3 层（保险）：所有 Shot/custom 块 try/catch，单张失败记数继续拍，不整批陪葬。
    static readonly System.Collections.Generic.HashSet<IntPtr> knownWindows =
        new System.Collections.Generic.HashSet<IntPtr>();
    static volatile bool watchdogRun = false;
    static Thread watchdogThread;

    delegate bool EnumWinProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWinProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern int GetClassName(IntPtr h, System.Text.StringBuilder b, int n);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] static extern int GetWindowText(IntPtr h, System.Text.StringBuilder b, int n);
    [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr h, EnumWinProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    const uint GW_OWNER = 4;
    const uint BM_CLICK = 0xF5;
    const uint WM_CLOSE = 0x10;
    const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10;
    static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

    // ---- 后台静默三件（用户提的"截图别一直占前台、别打扰干别的工作"） ----
    // ①焦点借用即还：Form.Show() 天然抢焦点弹到前台。借一次就马上把焦点还给
    //   Show 之前用户正在用的窗口（harness 进程 Spawn 后自己调用是允许的），
    //   打字浏览统统不被打断。②屏外窗：PrintWindow 走窗口自身 DC 与屏幕无关，
    //   Show 后把窗口挪到 (-3000,-3000)，视野里再无弹窗。③NOACTIVATE 置顶：
    //   真屏路径必须内容在屏幕最上，但置顶用 SetWindowPos(NOACTION)，不抢焦点。
    static void ShowSilently(Form f, bool useScreen)
    {
        IntPtr prev = GetForegroundWindow(); // 用户此刻在用的窗（可能为 0）
        f.Show();
        Application.DoEvents();
        MakeSilentAfterShow(f, prev, useScreen);
    }

    // 已 Show 过的窗补做静默：焦点借即还＋（真屏 NOACTIVATE 置顶｜PrintWindow 挪屏外）
    static void MakeSilentAfterShow(Form f, IntPtr prev, bool useScreen)
    {
        if (prev != IntPtr.Zero && prev != f.Handle)
        {
            try { SetForegroundWindow(prev); }
            catch { } // 焦点借即还：失败就退化为旧行为（静默降级，不炸）
        }
        if (useScreen)
        {
            // 真屏窗：置顶保住画面最前，但不抢输入焦点
            SetWindowPos(f.Handle, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE);
        }
        else
        {
            // PrintWindow 窗：挪到屏幕外，人眼看不见它（位置与渲染无关）
            f.Location = new Point(-3000, -3000);
        }
    }

    static void StartWatchdog()
    {
        watchdogRun = true;
        watchdogThread = new Thread(() =>
        {
            uint me = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            while (watchdogRun)
            {
                try { ScanPopups(me); }
                catch { }
                Thread.Sleep(300);
            }
        });
        watchdogThread.IsBackground = true;
        watchdogThread.Start();
    }
    static void StopWatchdog() { watchdogRun = false; }

    static void ScanPopups(uint me)
    {
        EnumWindows((h, l) =>
        {
            try
            {
                uint pid; GetWindowThreadProcessId(h, out pid);
                if (pid != me || !IsWindowVisible(h)) return true;
                lock (knownWindows) { if (knownWindows.Contains(h)) return true; }
                var cls = new System.Text.StringBuilder(256);
                GetClassName(h, cls, 256);
                string clsName = cls.ToString();
                IntPtr owner = GetWindow(h, GW_OWNER);
                bool isPopup = clsName == "#32770" || owner != IntPtr.Zero;
                if (!isPopup) return true;
                var cap = new System.Text.StringBuilder(256);
                GetWindowText(h, cap, 256);
                // 收集按钮
                var buttons = new System.Collections.Generic.List<Tuple<IntPtr, string>>();
                EnumChildWindows(h, (ch, ll) =>
                {
                    var cc = new System.Text.StringBuilder(256);
                    GetClassName(ch, cc, 256);
                    if (cc.ToString() == "Button")
                    {
                        var tx = new System.Text.StringBuilder(256);
                        GetWindowText(ch, tx, 256);
                        buttons.Add(Tuple.Create(ch, tx.ToString()));
                    }
                    return true;
                }, IntPtr.Zero);
                IntPtr target = IntPtr.Zero;
                string why = "WM_CLOSE";
                foreach (var b in buttons)
                {
                    string t = b.Item2;
                    if (t.Contains("否") || t.Contains("取消") || t.Contains("No") || t.Contains("Cancel"))
                    { target = b.Item1; why = "click[" + t + "]"; break; }
                }
                if (target == IntPtr.Zero)
                    foreach (var b in buttons)
                    {
                        string t = b.Item2;
                        if (t.Contains("确定") || t.Contains("OK") || t == "是" || t.Contains("关闭") || t.Contains("Close"))
                        { target = b.Item1; why = "click[" + t + "]"; break; }
                    }
                if (target != IntPtr.Zero) SendMessage(target, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                else PostMessage(h, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                Console.WriteLine("WATCHDOG dismiss popup class=" + clsName + " caption='" + cap + "' via " + why);
            }
            catch { }
            return true;
        }, IntPtr.Zero);
    }

    static void Track(Form f)
    {
        try
        {
            if (f.IsHandleCreated) { lock (knownWindows) { knownWindows.Add(f.Handle); } }
        }
        catch { }
    }
    static void Untrack(Form f)
    {
        // 关窗不调 Close（跳过 FormClosing 确认框，第 1 层防御），直接 Dispose。
        try { f.Dispose(); }
        catch { }
        Application.DoEvents();
        Thread.Sleep(250);
    }

    // ---- R2：两种截图方式 ----
    static Bitmap CapturePrint(Form f)
    {
        RECT r; GetWindowRect(f.Handle, out r);
        var bmp = new Bitmap(Math.Max(1, r.Right - r.Left), Math.Max(1, r.Bottom - r.Top));
        using (var g = Graphics.FromImage(bmp))
        {
            IntPtr hdc = g.GetHdc();
            try { PrintWindow(f.Handle, hdc, 0); }
            finally { g.ReleaseHdc(hdc); }
        }
        return bmp;
    }

    static Bitmap CaptureScreen(Form f)
    {
        // 遮挡者最小化/NOACTIVATE 置顶已由 Shot.ShowSilently＋MinimizeBlockers 处理；
        // 这里只做纯像素拷贝（客户区）＋标题栏拼接。
        var r = f.RectangleToScreen(f.ClientRectangle);
        var bmp = new Bitmap(Math.Max(1, r.Width), Math.Max(1, r.Height));
        using (var g = Graphics.FromImage(bmp))
            g.CopyFromScreen(r.Location, Point.Empty, r.Size);
        // 把蓝标题也拼上（文档截图要看到窗口名）
        RECT w; GetWindowRect(f.Handle, out w);
        int titleH = Math.Max(0, r.Top - w.Top);
        var full = new Bitmap(bmp.Width, bmp.Height + titleH);
        using (var g = Graphics.FromImage(full))
        {
            g.CopyFromScreen(new Point(w.Left, w.Top), Point.Empty, new Size(bmp.Width, titleH));
            g.DrawImage(bmp, 0, titleH);
        }
        bmp.Dispose();
        return full;
    }

    // ---- R3：非空校验（两次血泪校准，MetricProbe 实测表） ----
    // v1 只用 distinct>=15：ID 绑定这类稀疏表单误杀。
    // v2 用 distinct>=8 && dark>=20：登录窗（28/16）、项目切换（6/12）、公共参数（7/12）
    //   全是真图却被误杀——小窗深色采样天然不足 20。
    // 实测校准（含真空白对照）：login 28/16、inputlot 13/12、switch 6/12、common 7/12、
    //   空白原生窗 4/70（70 是系统边框，SunnyUI 无边框窗无此项）。
    // 结论：SunnyUI 无边框截图按 distinct>=6 && dark>=8 判定；
    //   真空白 SunnyUI 截图约 2~4 色 / 0~5 深色，安全分开。
    // 注意校验只是绊线不是法官：FAIL 的图照样存盘，事后人眼复核。
    static bool HasContent(Bitmap bmp)
    {
        var colors = new HashSet<int>();
        int dark = 0;
        for (int y = 60; y < bmp.Height; y += 7)
            for (int x = 0; x < bmp.Width; x += 7)
            {
                Color c = bmp.GetPixel(x, y);
                colors.Add((c.R >> 4) << 8 | (c.G >> 4) << 4 | (c.B >> 4));
                if (c.R + c.G + c.B < 240) dark++;
            }
        return colors.Count >= 6 && dark >= 8;
    }

    static void Shot(Form f, string name, bool useScreen, int sleepMs)
    {
        try
        {
            f.StartPosition = FormStartPosition.CenterScreen;
            ShowSilently(f, useScreen); // Show ＋ 焦点借即还 ＋ 屏外窗/NOACTIVATE 置顶
            Track(f); // 看门狗名单：自己打开的不算意外弹窗
            if (useScreen) MinimizeBlockers(f); // 只收走真正挡住截图区的窗，拍完恢复
            Thread.Sleep(sleepMs);
            Application.DoEvents();
            Track(f); // 睡后重登记一次：SunnyUI 主题/字体可能 RecreateHandle，老句柄失登记会被看门狗当弹窗关掉（07 号坑）
            string path = Path.Combine(outDir, name);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                using (var bmp = useScreen ? CaptureScreen(f) : CapturePrint(f))
                {
                    if (HasContent(bmp) || attempt == 1)
                    {
                        bmp.Save(path, ImageFormat.Png);
                        Console.WriteLine((HasContent(bmp) ? "OK " : "BLANK ") + name +
                            " " + bmp.Width + "x" + bmp.Height + (attempt == 1 ? " (retry)" : ""));
                        if (!HasContent(bmp)) failCount++;
                        break;
                    }
                    Console.WriteLine("RETRY " + name + " (blank, waiting 800ms)");
                    Thread.Sleep(800);
                    Application.DoEvents();
                }
            }
            if (useScreen) RestoreBlockers(); // 拍完立刻还回挡路的窗，别让它们收着等收尾
        }
        catch (Exception ex) { failCount++; Console.WriteLine("FAIL " + name + " : " + ex.GetType().Name + " " + ex.Message); }
        finally { Untrack(f); } // 直接 Dispose，不 Close（防 FormClosing 确认框卡住）
    }

    // ---- R1：模拟用户打字（反射填输入框，唯一允许的"伪造"——本就是用户输入） ----
    static void Fill(Form f, string field, string text)
    {
        var fi = f.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        if (fi == null) { Console.WriteLine("  no field " + field + " on " + f.GetType().Name); return; }
        var c = fi.GetValue(f) as Control;
        if (c == null) { Console.WriteLine("  field " + field + " not a Control"); return; }
        if (c is NumericUpDown) { return; }
        c.Text = text;
    }
    static void FillNum(Form f, string field, decimal v)
    {
        var fi = f.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        var nud = fi != null ? fi.GetValue(f) as NumericUpDown : null;
        if (nud == null) { Console.WriteLine("  no nud " + field); return; }
        try { nud.Value = Math.Max(nud.Minimum, Math.Min(nud.Maximum, v)); }
        catch (Exception ex) { Console.WriteLine("  nud " + field + " set fail: " + ex.Message); }
    }
    // V1.88.13补：配方名由输入框改下拉（UIComboBox DropDownList），harness不能再Fill文本。
    // 选中第0项即触发回填（延时/烧屏/温度/负压自动填好），截图显示下拉框+选中值。
    static void SelectCombo(Form f, string field, int index)
    {
        var fi = f.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        if (fi == null) { Console.WriteLine("  no field " + field + " on " + f.GetType().Name); return; }
        var c = fi.GetValue(f) as Control;
        if (c == null) { Console.WriteLine("  field " + field + " not a Control"); return; }
        try
        {
            var prop = c.GetType().GetProperty("SelectedIndex");
            if (prop == null) { Console.WriteLine("  combo " + field + " no SelectedIndex"); return; }
            prop.SetValue(c, index, null);
            Application.DoEvents();
        }
        catch (Exception ex) { Console.WriteLine("  combo " + field + " set fail: " + ex.Message); }
    }

    static void ScanBarcode(Form f, string code)
    {
        var m = f.GetType().GetMethod("HandleScannedBarcode", BindingFlags.NonPublic | BindingFlags.Instance);
        if (m == null) { Console.WriteLine("  no HandleScannedBarcode"); return; }
        m.Invoke(f, new object[] { code });
    }

    [STAThread]
    static void Main()
    {
        SetProcessDPIAware();
        Application.EnableVisualStyles();
        Directory.CreateDirectory(outDir);
        StartWatchdog(); // 第 2 层防御：意外模态弹窗自动点掉

        // ---- 环境隔离：备份当天 CSV ----
        string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        string todayCsv = Path.Combine(logDir, "TestLog_" + DateTime.Now.ToString("yyyyMMdd") + ".csv");
        string backupCsv = todayCsv + ".docshot.bak";
        bool hadCsv = File.Exists(todayCsv);
        try
        {
            if (hadCsv) File.Copy(todayCsv, backupCsv, true);

            // ---- R1：Mock 驱动真实状态机 ----
            var cfg = new DeviceConfig();
            cfg.UseMockCommunication = true;
            cfg.CollectInterval = 1000;
            var recipes = new List<RecipeConfig> {
                new RecipeConfig { Id=1, Name="烧屏8小时", DelayTime=TimeSpan.FromSeconds(30), BurnInTime=TimeSpan.FromHours(8), LimitTemperature=60, NegativePressure=-5.0m, CreateTime=DateTime.Now },
                new RecipeConfig { Id=2, Name="老化24小时", DelayTime=TimeSpan.FromSeconds(60), BurnInTime=TimeSpan.FromHours(24), LimitTemperature=65, NegativePressure=-6.0m, CreateTime=DateTime.Now },
                new RecipeConfig { Id=3, Name="试产验证2小时", DelayTime=TimeSpan.FromSeconds(10), BurnInTime=TimeSpan.FromHours(2), LimitTemperature=55, NegativePressure=-4.0m, CreateTime=DateTime.Now }
            };
            var dm = new DeviceManager(cfg, new SteadyReader(), null, null); // 定压注入，确定性状态
            string lot = "LOT2026091401";
            for (int i = 1; i <= 8; i++)
                dm.SetStationSerialNumber(i, "SN250914" + i.ToString("D4"));
            dm.SetStationRecipe(1, "烧屏8小时", -5.0m, null);
            dm.SetStationRecipe(2, "烧屏8小时", -5.0m, null);
            dm.SetStationRecipe(3, "试产验证2小时", -5.0m, null);
            dm.SetStationRecipe(4, "试产验证2小时", -5.0m, null);
            dm.SetStationRecipe(7, "烧屏8小时", -9.5m, null); // 阈值苛刻→大概率出压力报警（演故障）
            dm.SetStationDelayTimes(1, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(300));
            dm.SetStationDelayTimes(2, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(300));
            dm.SetStationDelayTimes(3, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(12));
            dm.SetStationDelayTimes(4, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(12));
            dm.SetStationDelayTimes(5, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(300));
            dm.SetStationDelayTimes(6, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(300));
            dm.SetStationDelayTimes(7, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(300));
            dm.Start();
            dm.StartTesting(new int[] { 1, 2, 3, 4, 7 });
            // 等 3/4 号完成（12s 时长 + 抽真空），最多等 60s
            int vac = 0, ag = 0, comp = 0, flt = 0, idle = 0;
            DateTime deadline = DateTime.Now.AddSeconds(60);
            do
            {
                Thread.Sleep(1000);
                dm.GetPhaseCounts(out vac, out ag, out comp, out flt, out idle);
                Console.WriteLine("phase vac=" + vac + " aging=" + ag + " comp=" + comp + " fault=" + flt);
            } while (comp < 2 && DateTime.Now < deadline);
            dm.StartTesting(new int[] { 5, 6 }); //  staggered：拍策略图时 5/6 号正在抽真空
            Thread.Sleep(3000);
            Application.DoEvents();
            dm.GetPhaseCounts(out vac, out ag, out comp, out flt, out idle);
            Console.WriteLine("FINAL vac=" + vac + " aging=" + ag + " comp=" + comp + " fault=" + flt);

            // 已完成的工位号（下料判定用）
            var doneIds = new List<int>();
            for (int i = 1; i <= 8; i++)
            {
                try
                {
                    var d = dm.GetBarometerData(i);
                    if (d != null && d.Status == DeviceStatus.Completed) doneIds.Add(i);
                }
                catch { }
            }
            Console.WriteLine("doneIds=" + string.Join(",", doneIds.ToArray()));

            // ---- R1：历史记录种子（真实落盘，拍完还原） ----
            TestEventLogger.Write(lot, 1, "启动", "工位1 启动测试，配方=烧屏8小时", -8.2m, null, null, "SN2509140001", "烧屏8小时", null);
            TestEventLogger.Write(lot, 2, "启动", "工位2 启动测试，配方=烧屏8小时", -7.6m, null, null, "SN2509140002", "烧屏8小时", null);
            TestEventLogger.Write(lot, 7, "报警", "压力越限：-3.2kPa 高于阈值-9.5kPa", -3.2m, null, null, "SN2509140007", "烧屏8小时", "FAIL");
            TestEventLogger.Write(lot, 3, "完成", "老化计时到，自动下电关阀", -8.8m, null, null, "SN2509140003", "试产验证2小时", "PASS");
            TestEventLogger.Write(lot, 3, "下料判定", "人工判定 PASS", null, null, null, "SN2509140003", "试产验证2小时", "PASS");
            TestEventLogger.Write(lot, 7, "复位", "报警复位回空闲", null, null, null, "SN2509140007", "烧屏8小时", null);
            TestEventLogger.Write(lot, 0, "MES上报(Mock)", "启动事件已上报（Mock 只写 CSV）", null, null, null, null, null, null);
            TestEventLogger.Write(lot, 5, "启动", "工位5 启动测试，配方=烧屏8小时", -9.1m, null, null, "SN2509140005", "烧屏8小时", null);

            var um = new UserManager();

            // ---- 开拍（策略图/下料判定先拍，状态最新鲜） ----
            Shot(new ProcessPolicyForm(cfg, dm, true), "10-policy.png", true, 1200);
            Shot(new UnloadJudgeForm(doneIds.ToArray(), dm), "11-unload.png", false, 600);
            // 正式 ID 绑定：模拟扫码枪逐条扫入（生产路径）
            var idf = new IdBindingForm(lot, null, dm);
            idf.StartPosition = FormStartPosition.CenterScreen;
            var idPrev = GetForegroundWindow();
            idf.Show();
            Application.DoEvents();
            MakeSilentAfterShow(idf, idPrev, false);
            Thread.Sleep(500);
            ScanBarcode(idf, "01"); ScanBarcode(idf, "SN2509140001");
            ScanBarcode(idf, "02"); ScanBarcode(idf, "SN2509140002");
            ScanBarcode(idf, "03"); ScanBarcode(idf, "SN2509140003");
            Application.DoEvents();
            Thread.Sleep(400);
            using (var bmp = CapturePrint(idf))
            {
                bmp.Save(Path.Combine(outDir, "04-idbinding.png"), ImageFormat.Png);
                Console.WriteLine((HasContent(bmp) ? "OK " : "BLANK ") + "04-idbinding.png");
                if (!HasContent(bmp)) failCount++;
            }
            Untrack(idf);

            Shot(new StationSettingsForm(dm, cfg, recipes, 1), "07-stationsettings.png", false, 600);
            Shot(new CommunicationTestForm(dm), "16-comtest.png", false, 800);
            // 送风机窗"连接后每 2s 自动读数"：多等两个周期再拍，否则温湿度全是"--"
            Shot(new FanTestForm(dm), "17-fantest.png", false, 5000);
            // 2.0.1 客户自助校准窗（温度模式，客户版校准节配图）：Show 时即拉一次
            // 当前读数（dm.GetFanData()，Mock 下 FanEnabled=true 自动有 Mock 风机），
            // 等 2s 保证"上位机当前"回显有值；窗内写的是缺省斜率 0.1，与文档口径一致。
            Shot(new FanCalibrationForm(dm, cfg, FanCalibrationTarget.Temperature),
                "19-calibration.png", false, 2000);

            var login = new LoginForm(um, UserRole.Operator);
            login.StartPosition = FormStartPosition.CenterScreen;
            var lgPrev = GetForegroundWindow();
            login.Show(); Application.DoEvents();
            MakeSilentAfterShow(login, lgPrev, false); Thread.Sleep(400);
            Fill(login, "txtPassword", "123456");
            Application.DoEvents(); Thread.Sleep(300);
            using (var bmp = CapturePrint(login))
            {
                bmp.Save(Path.Combine(outDir, "02-login.png"), ImageFormat.Png);
                Console.WriteLine((HasContent(bmp) ? "OK " : "BLANK ") + "02-login.png");
                if (!HasContent(bmp)) failCount++;
            }
            Untrack(login);

            var lot2 = new InputLotForm(null, null);
            lot2.StartPosition = FormStartPosition.CenterScreen;
            var lotPrev = GetForegroundWindow();
            lot2.Show(); Application.DoEvents();
            MakeSilentAfterShow(lot2, lotPrev, false); Thread.Sleep(400);
            Fill(lot2, "txtLot", lot);
            Application.DoEvents(); Thread.Sleep(300);
            using (var bmp = CapturePrint(lot2))
            {
                bmp.Save(Path.Combine(outDir, "03-inputlot.png"), ImageFormat.Png);
                Console.WriteLine((HasContent(bmp) ? "OK " : "BLANK ") + "03-inputlot.png");
                if (!HasContent(bmp)) failCount++;
            }
            Untrack(lot2);

            Shot(new RecipeManagerForm(recipes, -5.0m, cfg), "05-recipe.png", false, 600);

            var batch = new BatchRecipeForm(dm, recipes, new List<int> { 1, 2, 5, 6 });
            batch.StartPosition = FormStartPosition.CenterScreen;
            batch.Show(); Application.DoEvents(); Thread.Sleep(400);
            SelectCombo(batch, "cmbRecipeName", 0); // V1.88.13起下拉单选，选中触发回填
            FillNum(batch, "nudDelayHours", 0); FillNum(batch, "nudDelayMinutes", 0); FillNum(batch, "nudDelaySeconds", 30);
            FillNum(batch, "nudBurnInHours", 8); FillNum(batch, "nudBurnInMinutes", 0); FillNum(batch, "nudBurnInSeconds", 0);
            Fill(batch, "txtLimitTemp", "60");
            Fill(batch, "txtNegativePressure", "-5");
            Application.DoEvents(); Thread.Sleep(300);
            using (var bmp = CapturePrint(batch))
            {
                bmp.Save(Path.Combine(outDir, "06-batchrecipe.png"), ImageFormat.Png);
                Console.WriteLine((HasContent(bmp) ? "OK " : "BLANK ") + "06-batchrecipe.png");
                if (!HasContent(bmp)) failCount++;
            }
            Untrack(batch);

            Shot(new HistoryRecordForm(false), "08-history.png", false, 800);
            var cfgSite = new DeviceConfig();
            cfgSite.UseMockCommunication = false;
            Shot(new SettingsForm(cfgSite), "09-settings.png", false, 800);
            Shot(new ProjectSwitchForm(() => dm.GetTestingCount()), "12-projectswitch.png", false, 600);
            Shot(new UserManagementForm(um), "14-usermgmt.png", false, 600);
            var common = new CommonParameterForm(dm);
            common.StartPosition = FormStartPosition.CenterScreen;
            var cmPrev = GetForegroundWindow();
            common.Show(); Application.DoEvents();
            MakeSilentAfterShow(common, cmPrev, false); Thread.Sleep(400);
            FillNum(common, "nudThreshold", -5);
            Application.DoEvents(); Thread.Sleep(300);
            using (var bmp = CapturePrint(common))
            {
                bmp.Save(Path.Combine(outDir, "15-commonparam.png"), ImageFormat.Png);
                Console.WriteLine((HasContent(bmp) ? "OK " : "BLANK ") + "15-commonparam.png");
                if (!HasContent(bmp)) failCount++;
            }
            Untrack(common);

            // V1.88.13补拍：此前harness缺这三张（01主界面/13授权/18连线页），靠手工存量图。
            // 13/18用无参构造直拍。
            Shot(new SoftActivation(), "13-activation.png", false, 600);
            Shot(new IoRemapVisualForm(), "18-ioremap.png", true, 800);
            // 01主界面（V1.88.19重拍：完整主窗体＋Mock生产数据）。
            // 血泪：V1.88.18 版只截了 WorkstationGridView 画布，缺右侧操作区/
            // 顶部菜单/底部状态栏，文档"四个区"对不上图。必须截完整 MainForm。
            // 做法：new MainForm() 全走生产构造 → 反射置 _config.UseMockCommunication
            // =true（后台 Start 秒连免等待）→ 删残留 TestSession.json（防启动恢复弹窗）→
            // Show → 在主窗自带 dm 上用公开 API 跑演示任务（与现场操作同一条路）：
            // 1/2/5/6 号阈值 0（恒到位、绝不误报）长烧屏演"老化中"，3/4 号阈值 0
            // 短烧屏 12 秒演"已完成"，7 号阈值 -11（Mock 值 -6~-10 恒高于阈值＝恒不到位，
            // 15 秒建立超时报故障）演"故障"。阈值 0/-11 下原生 Mock 的 15% 坏读数
            // 翻不出花样，全程确定性（前提：压力报警开着＋真空超时非 0，见上 fixture）。→ 勾选两台演示
            // 正方形选中框 → 真屏截图。网格＋状态栏＋运行状态全是 dm 真实状态，
            // 天然一致，无需任何反射灌数。
            var mainWin = new MainForm();
            try
            {
                var cfgField = typeof(MainForm).GetField("_config",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var mainCfg = cfgField != null
                    ? cfgField.GetValue(mainWin) as DeviceConfig : null;
                if (mainCfg != null)
                {
                    mainCfg.UseMockCommunication = true;
                    // 演示要三态齐全（3/4 完成、7 号故障、其余老化中），但当前项目的
                    // 策略层（Policy.json，2.0.x 加载 App.config → Policy 叠加、策略赢）
                    // 若是 A 档：压力报警总开关关 ＋ 真空超时写 0——7 号阈值的越限报警
                    // 被总开关压死、"建立超时"判定又因超时=0 永不触发（fault 恒 0 的根因，
                    // 实测确认）。这两项是演示开关，不是工艺身份，演示前必须掰回：
                    // 阈值 -11 下 Mock 值（-6~-10）恒高于阈值＝恒不到位 ⇒ 15 秒建立
                    // 超时报故障，确定性强；其余 71 台阈值 0 恒到位不受影响。
                    if (!mainCfg.PressureAlarmEnabled)
                    {
                        mainCfg.PressureAlarmEnabled = true;
                        Console.WriteLine("  fixture: 项目策略关着压力报警，演示需开回 true");
                    }
                    if (mainCfg.VacuumConfirmTimeoutMs <= 0)
                    {
                        mainCfg.VacuumConfirmTimeoutMs = 15000;
                        Console.WriteLine("  fixture: 项目策略把真空超时写成 0，演示需开回 15000");
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine("  main mock flag fail: " + ex.Message); }
            try
            {
                string staleSnap = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "TestSession.json");
                if (File.Exists(staleSnap))
                {
                    File.Delete(staleSnap);
                    Console.WriteLine("  deleted stale TestSession.json");
                }
            }
            catch (Exception ex) { Console.WriteLine("  snap delete fail: " + ex.Message); }
            mainWin.StartPosition = FormStartPosition.CenterScreen;
            var mwPrev = GetForegroundWindow();
            mainWin.Show();
            Application.DoEvents();
            MakeSilentAfterShow(mainWin, mwPrev, true); // NOACTIVATE 置顶，不抢用户焦点
            Track(mainWin); // 看门狗名单：自己打开的不算意外弹窗
            Thread.Sleep(2500); // 等 Load 建画布＋后台 mock Start 完成
            Application.DoEvents();
            Track(mainWin); // 睡后重登记一次（句柄可能重建，见 Shot 注释 07 号坑）
            try
            {
                var dmField = typeof(MainForm).GetField("_deviceManager",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var mainDm = dmField != null
                    ? dmField.GetValue(mainWin) as DeviceManager : null;
                if (mainDm == null) throw new Exception("反射拿不到主窗 _deviceManager");
                // 72 台全开满载（V1.88.19 血泪：只开 7 台时其余 65 台空闲格显示
                // Mock 自造的 SN0009~/配方A1~A5＋每秒乱跳的假延时，文档没法看；
                // 全开后每格 SN/配方/时间全是本次真实下发的演示值，无 Mock 噪声）：
                // 阈值 0＝恒到位绝不误报，7 号阈值 -11 恒不到位 ⇒ 15 秒建立超时演故障。
                int[] allIds = new int[72];
                for (int i = 1; i <= 72; i++)
                {
                    allIds[i - 1] = i;
                    mainDm.SetStationSerialNumber(i, "SN250914" + i.ToString("D4"));
                    mainDm.SetStationRecipe(i, "演示烧屏", i == 7 ? -11m : 0m, null);
                    if (i == 3 || i == 4)
                        mainDm.SetStationDelayTimes(i,
                            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(12));
                    else
                        mainDm.SetStationDelayTimes(i,
                            TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(300));
                }
                mainDm.StartTesting(allIds); // 69 台老化中＋3/4 待完成＋7 待故障
                DateTime mainDeadline = DateTime.Now.AddSeconds(90);
                int mVac = 0, mAg = 0, mComp = 0, mFlt = 0, mIdle = 0;
                do
                {
                    Thread.Sleep(1000);
                    Application.DoEvents();
                    mainDm.GetPhaseCounts(out mVac, out mAg, out mComp, out mFlt, out mIdle);
                    Console.WriteLine("main vac=" + mVac + " aging=" + mAg
                        + " comp=" + mComp + " fault=" + mFlt);
                } while ((mComp < 2 || mFlt < 1) && DateTime.Now < mainDeadline);
                var gridField = typeof(MainForm).GetField("_gridView",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var mainGrid = gridField != null
                    ? gridField.GetValue(mainWin)
                        as AgingTestSystem.Views.WorkstationGridView : null;
                if (mainGrid != null)
                {
                    mainGrid.SetSelected(1, true);
                    mainGrid.SetSelected(3, true);
                }
                Application.DoEvents();
                Thread.Sleep(800);
                Application.DoEvents();
                MinimizeBlockers(mainWin); // 拍前收走挡路的窗（OpenCode 等），拍完恢复
                using (var bmp = CaptureScreen(mainWin))
                {
                    bmp.Save(Path.Combine(outDir, "01-main.png"), ImageFormat.Png);
                    Console.WriteLine((HasContent(bmp) ? "OK " : "BLANK ")
                        + "01-main.png " + bmp.Width + "x" + bmp.Height);
                    if (!HasContent(bmp)) failCount++;
                }
                RestoreBlockers(); // 拍完立刻还回，保住用户桌面原状
            }
            catch (Exception ex)
            {
                failCount++;
                Console.WriteLine("FAIL 01-main.png : "
                    + ex.GetType().Name + " " + ex.Message);
            }
            finally { Untrack(mainWin); } // 直接 Dispose，不 Close

            try { dm.Stop(); } catch { } try { dm.Dispose(); } catch { }
            StopWatchdog();
            Console.WriteLine(failCount == 0 ? "ALL PASS" : ("FAILURES=" + failCount));
            Environment.ExitCode = failCount == 0 ? 0 : 1;
            return; // 注意：必须 return 走 finally 做 CSV 还原；Environment.Exit 会跳过 finally
        }
        finally
        {
            // ---- 环境还原 ----
            RestoreBlockers(); // 兜底：防异常退出把挡路的窗（OpenCode 等）留在最小化
            try
            {
                if (File.Exists(todayCsv)) File.Delete(todayCsv);
                if (hadCsv) File.Copy(backupCsv, todayCsv, true);
                if (File.Exists(backupCsv)) File.Delete(backupCsv);
                Console.WriteLine("csv restored, hadCsv=" + hadCsv);
            }
            catch (Exception ex) { Console.WriteLine("restore fail: " + ex.Message); }
        }
    }
}
