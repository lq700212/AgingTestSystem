// ============================================================================
//  DeviceManagerIntegrationTests.cs —— 设备编排端到端集成测试（V1.59 新增）
//
//  【做什么】
//  用"可控 Fake 气压表 + 可记录 Fake IO 控制器"通过 DeviceManager 的依赖注入
//  构造函数（V1.59 为可测性开放）直接驱动【真实的】三阶段老化状态机，
//  以 30ms 采集间隔在数秒内跑完现场要几小时的生命周期：
//
//    场景1 正常全流程：启动只开阀不上电(安全铁律) → 压力到位+延时时间到自动上电
//                     → 配方时长到自动完成 → Completed·待取料 + PASS + 阀电全关
//    场景2 真空建立失败：宽限窗口内压力始终不到位 → 报警 FAIL，载台全程从未带电
//    场景3 通讯失联：连续读失败 ≥ 阈值 → 标故障但结果=设备异常(不冤枉产品)
//    场景4 手动中止：Aging 中 StopTesting → 回空闲、阀电关、快照清除
//    场景5 断电恢复：在测快照落盘 → 重启(new)后 LoadPendingSession 命中 →
//                   RecoverSession 整台重测(重新只开阀进抽真空态)
//    场景6 扫码重绑：完成态工位重新绑定 SN 自动复位回空闲
//    场景7 配方阈值优先：压力在全局阈值(-5)下越限、配方阈值(-3)下正常 →
//                    证明判定用的是配方值而非全局兜底
//
//  【怎么跑】由 run_unit_tests.ps1 与 TestRunner.cs 一起编译（partial class
//  共享 Check/Module/EnterCleanDir），不单独运行。
//
//  【时间约定】采集间隔 30ms、确认超时 600ms、延时时间 0.5s、老化 1.5s——
//  全套场景约 10s 跑完；WaitUntil 轮询等待条件成立，超时按 FAIL 记。
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AgingTestSystem.Interfaces;
using AgingTestSystem.Models;
using AgingTestSystem.Services;

namespace AgingTestSystem.Tests
{
    internal static partial class TestRunner
    {
        // ─────────────────────────────────────────────────────────────────
        // Fake 实现：可控气压表（压力序列可编程、可注入读取失败）
        // ─────────────────────────────────────────────────────────────────
        private sealed class FakeBarometerReader : IBarometerReader
        {
            private readonly object _lock = new object();
            private readonly int _total;
            private readonly Dictionary<int, decimal> _pressures = new Dictionary<int, decimal>();
            private readonly HashSet<int> _failIds = new HashSet<int>();
            // 【V1.62 扩展】错 id 上报（position→谎报的 DeviceId）与超长数组，
            // 专测采集防火墙；默认关闭，原有场景行为不变。
            private readonly Dictionary<int, int> _spoofIds = new Dictionary<int, int>();
            private readonly List<BarometerData> _extraReads = new List<BarometerData>();
            private bool _connected;

            public FakeBarometerReader(int total) { _total = total; }

            public bool IsConnected => _connected;
            public string CurrentPortName => _connected ? "FAKE-COM" : "";

            public bool Connect(DeviceConfig config) { _connected = true; return true; }
            public void Disconnect() { _connected = false; }

            public BarometerData ReadData(int deviceId)
            {
                lock (_lock)
                {
                    if (_failIds.Contains(deviceId)) return null;   // 模拟该台通讯失联
                    decimal p;
                    if (!_pressures.TryGetValue(deviceId, out p)) return null; // 未设定的台视为无数据
                    int reported = deviceId;
                    int spoofed;
                    if (_spoofIds.TryGetValue(deviceId, out spoofed)) reported = spoofed;
                    return new BarometerData { DeviceId = reported, VacuumPressure = p, CollectTime = DateTime.Now };
                }
            }

            public BarometerData[] ReadAllData()
            {
                var list = new List<BarometerData>();
                for (int i = 1; i <= _total; i++) list.Add(ReadData(i));
                list.AddRange(_extraReads); // 超长数组：模拟实现有 bug 的读取器
                // 【复查补齐】短数组：只回前 N 台，模拟实现返回短数组的读取器
                //（编排侧必须按"尾部通讯失败"处理，不能静默 stale）。
                if (_returnCount.HasValue && list.Count > _returnCount.Value)
                {
                    list.RemoveRange(_returnCount.Value, list.Count - _returnCount.Value);
                }
                return list.ToArray();
            }

            public bool SetThreshold(int deviceId, decimal thresholdValue) => true;

            public Dictionary<int, bool> SetAllThresholds(decimal thresholdValue)
            {
                var dict = new Dictionary<int, bool>();
                for (int i = 1; i <= _total; i++) dict[i] = true;
                return dict;
            }

            public event EventHandler<string> OnError
            {
                add { }
                remove { }
            }

            // ── 测试辅助：设定某台压力 / 注入某台读取失败 ──
            public void SetPressure(int deviceId, decimal pressureKPa)
            {
                lock (_lock) _pressures[deviceId] = pressureKPa;
            }

            public void SetFail(int deviceId, bool fail)
            {
                lock (_lock)
                {
                    if (fail) _failIds.Add(deviceId); else _failIds.Remove(deviceId);
                }
            }

            /// <summary>让某位置上报错误的 DeviceId（模拟实现有 bug 的读取器）</summary>
            public void SetSpoofedDeviceId(int position, int reportedId)
            {
                lock (_lock) { _spoofIds[position] = reportedId; }
            }

            public void ClearSpoofedDeviceIds()
            {
                lock (_lock) { _spoofIds.Clear(); }
            }

            /// <summary>在正常数组后追加额外数据（模拟返回超长数组的读取器）</summary>
            public void AddExtraRead(BarometerData data)
            {
                lock (_lock) { _extraReads.Add(data); }
            }

            private int? _returnCount;

            /// <summary>只返回前 N 台数据（模拟返回短数组的读取器；传 null 恢复全量）</summary>
            public void SetReturnCount(int? n)
            {
                lock (_lock) { _returnCount = n; }
            }

            public void ClearExtraReads()
            {
                lock (_lock) { _extraReads.Clear(); }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Fake 实现：可记录 IO 控制器（保存每个输出点最终状态 + "曾经开过"历史）
        // ─────────────────────────────────────────────────────────────────
        private sealed class FakeIoController : IIoController
        {
            private readonly object _lock = new object();
            private readonly bool[] _outputs;          // 索引 = outputId-1（含输入区占位）
            private readonly HashSet<int> _everOn = new HashSet<int>(); // 曾被置 ON 的输出点
            // 【V1.62 扩展】输入覆写（DI 触点分支用；默认 null 走恒 false 老行为）
            private bool[] _inputOverride;
            private bool _connected;

            public FakeIoController(int totalOutputs) { _outputs = new bool[totalOutputs]; }

            public bool IsConnected => _connected;
            public event EventHandler<string> OnError
            {
                add { }
                remove { }
            }

            public bool Connect(DeviceConfig config) { _connected = true; return true; }
            public void Disconnect() { _connected = false; }

            public bool ReadInput(int inputId) => false;

            public bool[] ReadAllInputs()
            {
                lock (_lock)
                {
                    // 覆写时返回副本（防外部污染）；默认恒 false（老行为）
                    if (_inputOverride != null) return (bool[])_inputOverride.Clone();
                    return new bool[80];
                }
            }

            /// <summary>设定 80 路 DI 输入（触点报警分支用）；传 null 恢复默认恒 false</summary>
            public void SetInputs(bool[] inputs)
            {
                lock (_lock) { _inputOverride = inputs == null ? null : (bool[])inputs.Clone(); }
            }

            public void WriteOutput(int outputId, bool state)
            {
                lock (_lock)
                {
                    _attempted.Add(outputId);   // 先记尝试（含抛异常的），供"下发过"断言
                    if (_throwOnWrite) throw new InvalidOperationException("Fake IO 写失败注入");
                    if (outputId >= 1 && outputId <= _outputs.Length)
                    {
                        _outputs[outputId - 1] = state;
                        if (state) _everOn.Add(outputId);
                    }
                }
            }

            public void WriteOutputs(int[] outputIds, bool[] states)
            {
                for (int i = 0; i < outputIds.Length && i < states.Length; i++)
                {
                    WriteOutput(outputIds[i], states[i]);
                }
            }

            public bool ReadOutput(int outputId)
            {
                lock (_lock) return outputId >= 1 && outputId <= _outputs.Length && _outputs[outputId - 1];
            }

            public bool[] ReadAllOutputs()
            {
                lock (_lock) return (bool[])_outputs.Clone();
            }

            // ── 测试辅助 ──
            public bool EverPowerOn(int outputId)
            {
                lock (_lock) return _everOn.Contains(outputId);
            }

            private bool _throwOnWrite;
            private readonly List<int> _attempted = new List<int>();

            /// <summary>写失败注入开关（模拟耦合器掉线瞬间的下发抛异常；默认关）</summary>
            public void SetThrowOnWrite(bool on)
            {
                lock (_lock) { _throwOnWrite = on; }
            }

            /// <summary>是否尝试写过某输出点（含被注入失败吞掉的尝试）</summary>
            public bool WasAttempted(int outputId)
            {
                lock (_lock) return _attempted.Contains(outputId);
            }
        }

        /// <summary>轮询等待条件成立（集成测试的时钟推进手段）；超时返回 false</summary>
        private static bool WaitUntil(Func<bool> condition, int timeoutMs)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try { if (condition()) return true; } catch { }
                Thread.Sleep(25);
            }
            return condition();
        }

        /// <summary>
        /// 组装一台小规模(4 台)快速(30ms 采集)的 DeviceManager + Fake 设备三件套
        /// </summary>
        private static DeviceManager BuildTestManager(
            out FakeBarometerReader reader, out FakeIoController io, out DeviceConfig config)
        {
            config = new DeviceConfig();
            config.TotalBarometers = 4;
            config.TotalInputs = 80;
            config.TotalOutputs = 160;
            config.CollectInterval = 30;              // 30ms 快速采集，秒级跑完生命周期
            config.VacuumConfirmTimeoutMs = 600;      // 真空建立宽限 600ms
            config.CommunicationLossAlarmCount = 3;   // 连续 3 次读失败判失联
            config.MaxTestDurationSeconds = 0;        // 默认不限时长（用配方时长驱动完成）
            config.AlarmWhenPressureHigherThanThreshold = true;
            config.AlarmPressureThresholdKPa = -5m;   // 全局兜底阈值（与产品默认对齐）
            config.FanEnabled = false;                // 送风机不参测（UpdateFanLifecycle 判空跳过）

            reader = new FakeBarometerReader(config.TotalBarometers);
            io = new FakeIoController(config.TotalInputs + config.TotalOutputs);
            var dm = new DeviceManager(config, reader, io, null);

            // 【关键】先把全部台设为常压 0kPa（模拟真实上电时气压表都有读数）。
            // 否则 ReadAllData 返回 null 会被"连续读失败≥CommunicationLossAlarmCount"
            // 的失联防呆在 ~90ms 内抢先报警关阀，盖过我们要测的场景
            //（V1.10 防呆本身是对的——真实现场断线就该这么保护）。
            for (int i = 1; i <= config.TotalBarometers; i++)
            {
                reader.SetPressure(i, 0m);
            }

            Check("DeviceManager.Start 成功(Fake 设备)", dm.Start());
            return dm;
        }

        /// <summary>内部输出编号：真空阀 = TotalInputs+deviceId；载台上电 = 再加 TotalBarometers</summary>
        private static int ValveOut(DeviceConfig c, int id) { return c.TotalInputs + id; }
        private static int PowerOut(DeviceConfig c, int id) { return c.TotalInputs + c.TotalBarometers + id; }

        /// <summary>反射读 DeviceManager 私有字段（卫生锁/定时器状态类断言用）</summary>
        private static object GetDmField(DeviceManager dm, string name)
        {
            return typeof(DeviceManager).GetField(name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(dm);
        }

        /// <summary>数当日事件 CSV 里某台的 报警(FAIL) 行数（报警边沿单次性断言用；
        /// V1.76 列序：0=时间,1=批号,2=SN,3=配方,4=设备编号,5=事件,6=结果）</summary>
        private static int CountAlarmLines(int deviceId)
        {
            string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs",
                "TestLog_" + DateTime.Now.ToString("yyyyMMdd") + ".csv");
            if (!File.Exists(file)) return 0;
            int n = 0;
            foreach (string line in File.ReadAllLines(file))
            {
                string[] cols = line.Split(',');
                if (cols.Length >= 6 && cols[4].Trim() == deviceId.ToString()
                    && cols[5].Trim() == "报警(FAIL)") n++;
            }
            return n;
        }

        // =====================================================================
        // 15b. DeviceManager 扩展覆盖（V1.62 新增）
        // 与 15 共用 Fake 三件套：补未覆盖 public API / 反方向报警 / 时长回退 /
        // 延时门控 / 空闲容错 / 边沿单次 / 快照全字段 / 防火墙 / 风机生命周期的断言。
        // 慢场景（时长/延时门）共约 +12s，可接受。
        // =====================================================================
        private static void DeviceManagerExtendedTests()
        {
            EnterCleanDir();

            FakeBarometerReader reader; FakeIoController io; DeviceConfig config;
            DeviceManager dm = BuildTestManager(out reader, out io, out config);

            try
            {
                // ---------- E1 基础状态口 ----------
                Check("[扩展] 启动后在线4台",
                    WaitUntil(() => dm.GetOnlineCount() == 4, 2000));
                Check("[扩展] 启动无失败原因", dm.LastStartupError == "");
                Check("[扩展] 耦合器已连接", dm.IsIoConnected);
                var states0 = dm.GetTestingStates();
                Check("[扩展] 初始无在测", states0.Length == 4 && states0.All(s => !s));
                Check("[扩展] 越界读null", dm.GetBarometerData(0) == null && dm.GetBarometerData(99) == null);
                Check("[扩展] 未配置工位信息null", dm.GetStationInfo(1) == null);
                Check("[扩展] 风机未注入时全套守卫",
                    !dm.IsFanConnected && dm.StartFan() == false && dm.StopFan() == false
                    && dm.ReconnectFan() == false && dm.GetFanData() == null);
                dm.ForceReconnectFan(); // null 风扇静默不抛
                Check("[扩展] 原始寄存器透传null/false",
                    dm.ReadHoldingRegisters(0x2000, 1) == null && dm.WriteSingleRegister(0x2000, 1) == false);

                // ---------- E3 批量 SN ----------
                dm.SetStationSerialNumbers(new Dictionary<int, string>
                    { { 1, "A" }, { 2, "B" }, { 99, "X" }, { 0, "Y" } });
                Check("[扩展] 批量SN写入",
                    dm.GetStationInfo(1).SerialNumber == "A" && dm.GetStationInfo(2).SerialNumber == "B");
                dm.SetStationSerialNumber(1, "  SP  ");
                Check("[扩展] SN去空格", dm.GetStationInfo(1).SerialNumber == "SP");
                dm.SetStationSerialNumber(1, null);
                Check("[扩展] SN置null清空", dm.GetStationInfo(1).SerialNumber == "");
                dm.SetStationSerialNumbers(null);
                dm.SetStationSerialNumbers(new Dictionary<int, string>());

                // ---------- E8 配方名/负压联动 ----------
                dm.SetStationRecipe(1, "R", -3m, "白场");
                Check("[扩展] 配方名+负压同存",
                    dm.GetStationInfo(1).RecipeName == "R"
                    && dm.GetStationInfo(1).RecipeNegativePressure == -3m);
                Check("[扩展] 显示模式同存",
                    dm.GetStationInfo(1).DisplayMode == "白场");
                dm.SetStationRecipeName(1, "R2");
                Check("[扩展] 只改名不改负压",
                    dm.GetStationInfo(1).RecipeName == "R2"
                    && dm.GetStationInfo(1).RecipeNegativePressure == -3m);
                Check("[扩展] 只改名不改显示模式",
                    dm.GetStationInfo(1).DisplayMode == "白场");
                dm.SetStationRecipeName(1, "  ");
                Check("[扩展] 清空名联动清负压",
                    dm.GetStationInfo(1).RecipeName == ""
                    && dm.GetStationInfo(1).RecipeNegativePressure == null);
                Check("[扩展] 清空名联动清显示模式",
                    dm.GetStationInfo(1).DisplayMode == null);
                var cloneGuard = dm.GetStationInfo(1);
                cloneGuard.RecipeName = "HACK";
                Check("[扩展] 工位信息返回副本", dm.GetStationInfo(1).RecipeName == "");

                // ---------- E6/E7 数据副本隔离 ----------
                var all = dm.GetAllBarometerData();
                Check("[扩展] 全量数据长度4", all.Length == 4 && all[0] != null);
                all[0].VacuumPressure = 12345m;
                Check("[扩展] 全量返回副本不污染缓存", dm.GetBarometerData(1).VacuumPressure != 12345m);

                // ---------- E9 输出透传 ----------
                dm.SetOutput(ValveOut(config, 1), true);
                Check("[扩展] 输出写读透传", dm.GetOutput(ValveOut(config, 1)) == true);
                dm.SetOutput(ValveOut(config, 1), false);
                dm.SetOutput(9999, true); // 越界静默不抛
                // Fake 按"全局输出编号空间" sizing（81~240 共 240），与真实实现(160)不同：
                // Fake 只为集成驱动不断言，此处锁 Fake 口径（240），真实现口径由单测精神保证。
                Check("[扩展] 输入恒false/批量列宽",
                    dm.GetInput(1) == false && dm.GetAllInputs().Length == 80
                    && dm.GetAllOutputs().Length == 240);
                Check("[扩展] 单台写阈值透传", dm.SetBarometerThreshold(1, -5m) == true);

                // ---------- E13/E24 非法输入电池 ----------
                dm.StartTesting(null); dm.StartTesting(new int[0]); dm.StartTesting(new[] { 0, 99, -1 });
                dm.StopTesting(null); dm.StopTesting(new int[0]);
                dm.ResetDevices(null);
                dm.StartQuickTracking(null); dm.StartQuickTracking(new int[0]); dm.StartQuickTracking(new[] { 0, 99 });
                dm.RecoverSession(null); dm.DiscardSession(null);
                dm.RecoverSession(new TestSession { Stations = new List<TestSessionStation>() });
                dm.DiscardSession(new TestSession { Stations = new List<TestSessionStation>() });
                dm.SetStationRecipe(99, "x", 1m, null); dm.SetStationSerialNumber(0, "x");
                dm.SetStationDelayTimes(99, TimeSpan.Zero, TimeSpan.Zero);
                Check("[扩展] 非法输入全静默", dm.GetTestingCount() == 0);

                // ---------- E12 连接/间隔 ----------
                Check("[扩展] 按需连接透传", dm.EnsureIoConnected() == true);
                dm.ReconnectIo(); dm.ReconnectBarometerReader(); // Fake 下不抛（串口重连约 500ms）
                dm.UpdateCollectInterval(100);
                Check("[扩展] 采集间隔热生效",
                    ((System.Timers.Timer)GetDmField(dm, "_collectTimer")).Interval == 100);
                dm.UpdateCollectInterval(30);
                bool threwInterval = false;
                try { dm.UpdateCollectInterval(0); } catch (ArgumentException) { threwInterval = true; }
                Check("[扩展] 非法间隔 fail-fast 抛异常", threwInterval);

                // ---------- E5 批量写阈值 ----------
                var thResult = dm.SetAllBarometerThresholds(-5m);
                Check("[扩展] 批量写4台全true",
                    thResult.Count == 4 && thResult.Values.All(v => v));
                Check("[扩展] 批量写后采集定时器恢复",
                    ((System.Timers.Timer)GetDmField(dm, "_collectTimer")).Enabled);
                Thread.Sleep(200);
                Check("[扩展] 批量写后采集正常", dm.GetOnlineCount() == 4);

                // ---------- E15 反方向报警端到端 ----------
                config.AlarmWhenPressureHigherThanThreshold = false;
                reader.SetPressure(3, -6m); // -6<-5：反方向越限
                reader.SetPressure(2, -4m); // -4>-5：反方向正常
                dm.StartTesting(new[] { 2, 3 });
                Check("[扩展][反向] 越限台报警Fault",
                    WaitUntil(() =>
                    {
                        var d = dm.GetBarometerData(3);
                        return d != null && d.Status == DeviceStatus.Fault;
                    }, 3000));
                Check("[扩展][反向] 越限结果FAIL", dm.GetBarometerData(3).LastTestResult == "FAIL");
                Check("[扩展][反向] 正常台自动上电",
                    WaitUntil(() => io.ReadOutput(PowerOut(config, 2)), 2500));
                Thread.Sleep(1000);
                Check("[扩展][反向] 正常台保持老化",
                    dm.GetBarometerData(2).Status == DeviceStatus.Testing && dm.GetTestingCount() == 1);
                dm.ResetDevices(new[] { 2, 3 });
                config.AlarmWhenPressureHigherThanThreshold = true;

                // ---------- E16 全局时长回退完成（无配方，MaxTestDuration=2s） ----------
                config.MaxTestDurationSeconds = 2;
                reader.SetPressure(1, -6m);
                dm.StartTesting(new[] { 1 });
                Check("[扩展][回退] 全局时长到自动完成",
                    WaitUntil(() =>
                    {
                        var d = dm.GetBarometerData(1);
                        return d != null && d.Status == DeviceStatus.Completed;
                    }, 5000));
                Check("[扩展][回退] 结果PASS", dm.GetBarometerData(1).LastTestResult == "PASS");
                config.MaxTestDurationSeconds = 0;
                // 批量重绑完成态自动复位（A3 另一半：不用 ResetDevices，扫码即复位）
                dm.SetStationSerialNumbers(new Dictionary<int, string> { { 1, "NEW-SN-001" } });
                Check("[扩展][批量重绑] 完成态自动回空闲",
                    WaitUntil(() =>
                    {
                        var d = dm.GetBarometerData(1);
                        return d != null && d.Status == DeviceStatus.Idle;
                    }, 2000));
                Check("[扩展][批量重绑] 复位后结果清空",
                    dm.GetBarometerData(1).LastTestResult == "");

                // ---------- E4 中途改全局不影响在测台（定格隔离） ----------
                dm.SetStationRecipe(1, "RX", -3m, null);
                dm.SetStationDelayTimes(1, TimeSpan.Zero, TimeSpan.FromSeconds(1.5));
                reader.SetPressure(1, -4m);
                dm.StartTesting(new[] { 1 });
                Thread.Sleep(150);
                dm.UpdateAlarmPressureThresholdKPa(-100m); // 全局突然收严：-4>-100，若误用全局必报警
                Check("[扩展][定格] 全局确已改动", config.AlarmPressureThresholdKPa == -100m);
                Check("[扩展][定格] 在测台按配方阈值完成",
                    WaitUntil(() =>
                    {
                        var d = dm.GetBarometerData(1);
                        return d != null && d.Status == DeviceStatus.Completed;
                    }, 4000));
                Check("[扩展][定格] 结果PASS非FAIL", dm.GetBarometerData(1).LastTestResult == "PASS");
                config.AlarmPressureThresholdKPa = -5m;
                dm.ResetDevices(new[] { 1 });

                // ---------- E30 定格清理回全局（反射卫生锁） ----------
                config.AlarmPressureThresholdKPa = -7m;
                dm.SetStationRecipe(2, "RZ", -3m, null);
                dm.StartTesting(new[] { 2 });
                Thread.Sleep(150);
                dm.StopTesting(new[] { 2 });
                Check("[扩展][清理] 停止后阈值回全局",
                    ((decimal[])GetDmField(dm, "_sessionThresholdKPa"))[1] == -7m);
                config.AlarmPressureThresholdKPa = -5m;
                dm.SetStationRecipe(2, "RZ", -3m, null); // 恢复后续场景用的配方

                // ---------- E66 显示模式叠加 + 在测id（V1.66） ----------
                reader.SetPressure(1, -6m);
                dm.SetStationRecipe(1, "RM66", -3m, "棋盘格");
                dm.SetStationDelayTimes(1, TimeSpan.Zero, TimeSpan.FromSeconds(60));
                dm.StartTesting(new[] { 1 });
                Check("[扩展][显示模式] 叠加后采集可见",
                    WaitUntil(() =>
                    {
                        var d = dm.GetBarometerData(1);
                        return d != null && d.DisplayMode == "棋盘格";
                    }, 3000));
                Check("[扩展][在测id] 仅1号在测",
                    dm.GetTestingDeviceIds().Length == 1 && dm.GetTestingDeviceIds()[0] == 1);
                dm.StopTesting(new[] { 1 });
                Check("[扩展][在测id] 停止后为空", dm.GetTestingDeviceIds().Length == 0);

                // ---------- E17 不限时长永不完成 ----------
                reader.SetPressure(2, -6m);
                dm.StartTesting(new[] { 2 });
                Check("[扩展][不限时] 到位上电",
                    WaitUntil(() => io.ReadOutput(PowerOut(config, 2)), 2500));
                Thread.Sleep(1500);
                Check("[扩展][不限时] 仍在老化不完成",
                    dm.GetBarometerData(2).Status == DeviceStatus.Testing && dm.GetTestingCount() == 1);
                dm.StopTesting(new[] { 2 });

                // ---------- E18 延时门控 2s ----------
                dm.SetStationDelayTimes(3, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(60));
                reader.SetPressure(3, -6m);
                dm.StartTesting(new[] { 3 });
                Thread.Sleep(1000);
                Check("[扩展][延时] 2秒未到不上电", !io.ReadOutput(PowerOut(config, 3)));
                Check("[扩展][延时] 2秒到后上电",
                    WaitUntil(() => io.ReadOutput(PowerOut(config, 3)), 4000));
                dm.StopTesting(new[] { 3 });

                // ---------- E19 空闲台失败不报警 ----------
                dm.ResetDevices(new[] { 1 });
                reader.SetFail(1, true);
                Thread.Sleep(400);
                var idleFail = dm.GetBarometerData(1);
                Check("[扩展][空闲容错] 空闲台失败不标Fault",
                    idleFail != null && idleFail.Status == DeviceStatus.Idle && idleFail.LastTestResult == "");
                reader.SetFail(1, false);

                // ---------- E20 未达阈值自愈（反射盯失败计数） ----------
                reader.SetFail(1, true);
                bool sawFail = false;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < 2000)
                {
                    if (((int[])GetDmField(dm, "_readFailCounts"))[0] > 0) { sawFail = true; break; }
                    Thread.Sleep(25);
                }
                Check("[扩展][自愈] 失败计数涨过", sawFail);
                reader.SetFail(1, false);
                Thread.Sleep(300);
                Check("[扩展][自愈] 恢复后计数清零且无报警",
                    ((int[])GetDmField(dm, "_readFailCounts"))[0] == 0
                    && dm.GetBarometerData(1).Status == DeviceStatus.Idle);

                // ---------- E21/E22 报警驻留 + 边沿单次（4号台，常压恒越限） ----------
                dm.StartTesting(new[] { 4 });
                Check("[扩展][驻留] 报警Fault",
                    WaitUntil(() =>
                    {
                        var d = dm.GetBarometerData(4);
                        return d != null && d.Status == DeviceStatus.Fault;
                    }, 3000));
                Thread.Sleep(600);
                Check("[扩展][驻留] 600ms后仍Fault+FAIL",
                    dm.GetBarometerData(4).Status == DeviceStatus.Fault
                    && dm.GetBarometerData(4).LastTestResult == "FAIL");
                Thread.Sleep(800);
                Check("[扩展][边沿] 持续越限只记一条报警",
                    CountAlarmLines(4) == 1);
                dm.ResetDevices(new[] { 4 });

                // ---------- E2/E6/E11/E12/E23 快照全字段 + 多台 + 批号 ----------
                dm.CurrentLotNumber = "LOT-E2E";
                dm.SetStationSerialNumber(1, "SN-E");
                dm.SetStationRecipe(1, "RE", -3m, null);
                dm.SetStationRecipe(2, "", null, null); // E30 留下的 RZ/-3 清掉：2 号测全局兜底必须无配方
                dm.SetStationDelayTimes(1, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));
                reader.SetPressure(1, -6m);
                reader.SetPressure(2, -6m);
                dm.StartTesting(new[] { 1, 2 });
                Thread.Sleep(150);
                var vec = dm.GetTestingStates();
                Check("[扩展] 状态向量两台在测",
                    vec.Length == 4 && vec[0] && vec[1] && !vec[2] && !vec[3]
                    && dm.GetTestingCount() == 2);
                var snap = TestSessionStore.Load();
                Check("[扩展] 双台快照", snap != null && snap.Stations.Count == 2);
                if (snap != null && snap.Stations.Count == 2)
                {
                    Check("[扩展] 快照批号", snap.LotNumber == "LOT-E2E");
                    var s1 = snap.Stations.First(s => s.DeviceId == 1);
                    Check("[扩展] 快照SN/配方", s1.SerialNumber == "SN-E" && s1.RecipeName == "RE");
                    Check("[扩展] 快照定格时长30/延时1/阈值-3",
                        s1.DurationSeconds == 30 && s1.DelaySeconds == 1 && s1.AlarmThresholdKPa == -3m);
                    var s2 = snap.Stations.First(s => s.DeviceId == 2);
                    Check("[扩展] 快照2号用全局兜底",
                        s2.DurationSeconds == 0 && s2.DelaySeconds == 0 && s2.AlarmThresholdKPa == -5m);
                }
                dm.StopTesting(new[] { 1, 2 });
                Check("[扩展] 全停后快照清空", TestSessionStore.Load() == null);
                dm.CurrentLotNumber = null;
                Check("[扩展] 批号置null清空", dm.CurrentLotNumber == "");

                // ---------- E12/A12 停止再启动 + 启动前在线0 ----------
                {
                    var cfg0 = new DeviceConfig { TotalBarometers = 4, TotalInputs = 80, TotalOutputs = 160 };
                    var r0 = new FakeBarometerReader(4);
                    var io0 = new FakeIoController(240);
                    var dm0 = new DeviceManager(cfg0, r0, io0, null);
                    try
                    {
                        Check("[扩展][启停] 启动前在线0", dm0.GetOnlineCount() == 0);
                        for (int i = 1; i <= 4; i++) r0.SetPressure(i, 0m);
                        Check("[扩展][启停] 冷启动成功", dm0.Start());
                        Check("[扩展][启停] 启动后在线4",
                            WaitUntil(() => dm0.GetOnlineCount() == 4, 2000));
                    }
                    finally { try { dm0.Dispose(); } catch { } }
                }
                dm.Stop();
                dm.Start();
                Check("[扩展][启停] 停止再启动后采集恢复",
                    WaitUntil(() => dm.GetOnlineCount() == 4, 2000));

                // ---------- E25 在测中急停 ----------
                reader.SetPressure(1, -6m);
                reader.SetPressure(2, -6m);
                dm.StartTesting(new[] { 1, 2 });
                Thread.Sleep(150);
                dm.StopAll();
                Check("[扩展][急停] 阀电全关",
                    !io.ReadOutput(ValveOut(config, 1)) && !io.ReadOutput(PowerOut(config, 1))
                    && !io.ReadOutput(ValveOut(config, 2)) && !io.ReadOutput(PowerOut(config, 2)));
                Check("[扩展][急停] 无在测+快照删", dm.GetTestingCount() == 0 && TestSessionStore.Load() == null);
                Thread.Sleep(300);
                Check("[扩展][急停] 面板回空闲",
                    dm.GetBarometerData(1).Status == DeviceStatus.Idle
                    && dm.GetBarometerData(2).Status == DeviceStatus.Idle);
            }
            finally
            {
                try { dm.StopAll(); } catch { }
                try { dm.Dispose(); } catch { }
                try { TestSessionStore.Clear(); } catch { }
            }

            // ---------- E26 风机生命周期（独立 manager + MockFan） ----------
            {
                FakeBarometerReader reader2; FakeIoController io2; DeviceConfig config2;
                DeviceManager dm2 = BuildTestManager(out reader2, out io2, out config2);
                // BuildTestManager 传 null 风扇：这里换一条带 MockFan 的 manager
                try { dm2.Dispose(); } catch { }
                var mockFan = new MockFanController();
                var dmf = new DeviceManager(config2, reader2, io2, mockFan);
                try
                {
                    for (int i = 1; i <= config2.TotalBarometers; i++) reader2.SetPressure(i, 0m);
                    Check("[扩展][风机] 启动连接风扇", dmf.Start() && mockFan.IsConnected);
                    Check("[扩展][风机] 初始数据null", dmf.GetFanData() == null);
                    reader2.SetPressure(1, -6m);
                    dmf.StartTesting(new[] { 1 });
                    Thread.Sleep(300);
                    Check("[扩展][风机] 首台启动风机",
                        mockFan.ReadStatus() != null
                        && mockFan.ReadStatus().RunState == FanRunState.FixedValueRunning);
                    // 轮询→缓存接线用反射直接驱动 PollFanData（确定性；tick 调度是框架行为，
                    // 已由主采集定时器在全套用例中证明，此处不等 2s tick，省时间消抖动）
                    typeof(DeviceManager).GetMethod("PollFanData",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        .Invoke(dmf, null);
                    var polled = dmf.GetFanData();
                    Check("[扩展][风机] 轮询有数",
                        polled != null && polled.RunState == FanRunState.FixedValueRunning && polled.IsOnline);
                    dmf.StopTesting(new[] { 1 });
                    Thread.Sleep(300);
                    Check("[扩展][风机] 末台停止风机",
                        mockFan.ReadStatus().RunState == FanRunState.FixedValueStopped);
                }
                finally
                {
                    try { dmf.StopAll(); } catch { }
                    try { dmf.Dispose(); } catch { }
                    try { TestSessionStore.Clear(); } catch { }
                }
            }

            // ---------- E28/E29 防火墙（独立 manager，Fake 开挂） ----------
            {
                FakeBarometerReader reader3; FakeIoController io3; DeviceConfig config3;
                DeviceManager dm3 = BuildTestManager(out reader3, out io3, out config3);
                try
                {
                    // 超长数组：第 5 个元素（总数只有 4）
                    reader3.AddExtraRead(new BarometerData
                        { DeviceId = 99, VacuumPressure = -6m, CollectTime = DateTime.Now });
                    reader3.SetPressure(1, -7m);
                    Thread.Sleep(250);
                    Check("[扩展][防火墙] 超长数组不炸且正常台照常更新",
                        dm3.GetBarometerData(1) != null
                        && dm3.GetBarometerData(1).VacuumPressure == -7m);
                    reader3.ClearExtraReads();

                    // 错 id 上报：位置 2 谎报 99
                    reader3.SetPressure(2, -6m);
                    reader3.SetSpoofedDeviceId(2, 99);
                    Thread.Sleep(250);
                    Check("[扩展][防火墙] 错id数据被忽略（缓存保留旧值）",
                        dm3.GetBarometerData(2) != null
                        && dm3.GetBarometerData(2).VacuumPressure == 0m);
                    reader3.ClearSpoofedDeviceIds();
                    Check("[扩展][防火墙] 撤谎后恢复正常",
                        WaitUntil(() =>
                        {
                            var d = dm3.GetBarometerData(2);
                            return d != null && d.VacuumPressure == -6m;
                        }, 2000));

                    // 脏快照恢复：null 台跳过不抛，有效台照常启动
                    dm3.RecoverSession(new TestSession
                    {
                        LotNumber = "DIRTY",
                        Stations = new List<TestSessionStation>
                        {
                            new TestSessionStation
                            {
                                DeviceId = 1, DurationSeconds = 30, DelaySeconds = 0,
                                AlarmThresholdKPa = -5m, SerialNumber = "S", RecipeName = "R"
                            },
                            null
                        }
                    });
                    Thread.Sleep(150);
                    Check("[扩展][防火墙] 脏快照有效台照常开阀",
                        io3.ReadOutput(ValveOut(config3, 1)));
                    dm3.StopTesting(new[] { 1 });
                }
                finally
                {
                    try { dm3.StopAll(); } catch { }
                    try { dm3.Dispose(); } catch { }
                    try { TestSessionStore.Clear(); } catch { }
                }
            }
        }

        // =====================================================================
        // 15. DeviceManager 三阶段状态机端到端（Fake 设备驱动，V1.59）
        // =====================================================================
        private static void DeviceManagerIntegrationTests()
        {
            // 【大扫荡】快照绝对路径化后走 BaseDirOverride 隔离（finally 复位）。
            string integDir = EnterCleanDir(); // TestSession.json / Logs\ 全部落隔离目录
            var integBase = typeof(TestSessionStore).GetField("BaseDirOverride",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (integBase != null) integBase.SetValue(null, integDir);
            string integSnap = System.IO.Path.Combine(integDir, "TestSession.json");

            FakeBarometerReader reader; FakeIoController io; DeviceConfig config;
            DeviceManager dm = BuildTestManager(out reader, out io, out config);

            try
            {
                // ================= 场景7+1：正常全流程（工位1）=================
                // 配方：负压值 -3（到位/报警阈值）、延时时间 0.5s、老化 1.5s
                dm.SetStationRecipe(1, "配方R1", -3m, null);
                dm.SetStationDelayTimes(1, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1.5));

                dm.StartTesting(new[] { 1 });
                Thread.Sleep(120); // 过几个采集周期

                Check("[流程] 启动后真空阀已开",
                    io.ReadOutput(ValveOut(config, 1)));
                Check("[流程] 启动后载台未上电(未吸附固定不通电)",
                    !io.ReadOutput(PowerOut(config, 1)));

                // 压力 -4kPa：对配方阈值 -3 是"到位"，对全局 -5 是"越限"——
                // 若判定误用全局值，此台会立刻报警断电而不是进入后续阶段（一石二鸟的断言）
                reader.SetPressure(1, -4m);

                Check("[流程] 真空到位+延时时间到后自动上电",
                    WaitUntil(() => io.ReadOutput(PowerOut(config, 1)), 2500));
                Check("[流程] 上电时真空阀保持开",
                    io.ReadOutput(ValveOut(config, 1)));

                Check("[流程] 配方时长到自动完成(Completed·待取料)",
                    WaitUntil(() =>
                    {
                        var d = dm.GetBarometerData(1);
                        return d != null && d.Status == DeviceStatus.Completed;
                    }, 4000));
                var done = dm.GetBarometerData(1);
                Check("[流程] 完成后结果标 PASS", done != null && done.LastTestResult == "PASS");
                Check("[流程] 完成后载台已断电", !io.ReadOutput(PowerOut(config, 1)));
                Check("[流程] 完成后真空阀已关闭", !io.ReadOutput(ValveOut(config, 1)));
                Check("[流程] 完成后无在测任务(GetTestingCount=0)", dm.GetTestingCount() == 0);
                Check("[流程] 全程未触发越限报警(证明配方阈值生效而非全局-5)",
                    WaitUntil(() => true, 50) && done != null && done.LastTestResult != "FAIL");

                // ================= 场景6：扫码重绑清完成态（工位1）=================
                dm.SetStationSerialNumber(1, "NEW-SN-001");
                Check("[重绑] 完成态工位重新绑定SN后自动回空闲",
                    WaitUntil(() =>
                    {
                        var d = dm.GetBarometerData(1);
                        return d != null && d.Status == DeviceStatus.Idle;
                    }, 2000));
                Check("[重绑] 复位后测试结果清空",
                    (dm.GetBarometerData(1) ?? new BarometerData()).LastTestResult == "");
                Check("[重绑] 复位后输出保持安全关闭",
                    !io.ReadOutput(PowerOut(config, 1)) && !io.ReadOutput(ValveOut(config, 1)));

                // ================= 场景2：真空建立失败（工位2）=================
                dm.StartTesting(new[] { 2 });
                // 不给压力（常压 0 > -5 恒越限）：600ms 宽限窗口耗尽应判真空建立失败
                Check("[真空失败] 宽限窗口超时后报警断电标Fault",
                    WaitUntil(() =>
                    {
                        var d = dm.GetBarometerData(2);
                        return d != null && d.Status == DeviceStatus.Fault;
                    }, 3000));
                var failDev = dm.GetBarometerData(2);
                Check("[真空失败] 结果标 FAIL(产品责任)",
                    failDev != null && failDev.LastTestResult == "FAIL");
                Check("[真空失败] 该台载台全程从未带电",
                    !io.EverPowerOn(PowerOut(config, 2)));
                Check("[真空失败] 报警联动已关真空阀",
                    !io.ReadOutput(ValveOut(config, 2)));

                // ================= 场景3：通讯失联=设备异常（工位3）=================
                dm.StartTesting(new[] { 3 });
                reader.SetFail(3, true); // 连续读失败 ≥3 次(30ms×3 很快触发)
                Check("[失联] 连续读失败触发故障",
                    WaitUntil(() =>
                    {
                        var d = dm.GetBarometerData(3);
                        return d != null && d.Status == DeviceStatus.Fault;
                    }, 3000));
                Check("[失联] 结果记\"设备异常\"而非 FAIL(不冤枉产品)",
                    dm.GetBarometerData(3).LastTestResult == "设备异常");
                Check("[失联] 已安全关阀断电",
                    !io.ReadOutput(ValveOut(config, 3)) && !io.ReadOutput(PowerOut(config, 3)));
                reader.SetFail(3, false);

                // ================= 场景4：手动中止（工位2 复位后再启停）=================
                dm.ResetDevices(new[] { 2 }); // 清掉场景2的故障态
                dm.SetStationRecipe(2, "R2", -5m, null);
                dm.SetStationDelayTimes(2, TimeSpan.Zero, TimeSpan.FromSeconds(60)); // 长老化
                dm.StartTesting(new[] { 2 });
                reader.SetPressure(2, -6m); // -6 ≤ -5 到位 → 应自动上电
                Check("[中止] 到位上电进入老化",
                    WaitUntil(() => io.ReadOutput(PowerOut(config, 2)), 2500));
                dm.StopTesting(new[] { 2 });
                Check("[中止] 手动停止后回空闲且阀电全关",
                    WaitUntil(() =>
                    {
                        var d = dm.GetBarometerData(2);
                        return d != null && d.Status == DeviceStatus.Idle;
                    }, 2000)
                    && !io.ReadOutput(PowerOut(config, 2)) && !io.ReadOutput(ValveOut(config, 2)));
                Check("[中止] 手动停止不计判定结果",
                    (dm.GetBarometerData(2) ?? new BarometerData()).LastTestResult == "");
                Check("[中止] 全部停止后快照清空", TestSessionStore.Load() == null);

                // ================= 场景5：断电恢复（工位4，放最后——本场景会 Dispose dm）=================
                dm.SetStationRecipe(4, "配方R4", -90m, null);
                dm.SetStationDelayTimes(4, TimeSpan.Zero, TimeSpan.FromSeconds(30)); // 长老化不会自己完成
                dm.StartTesting(new[] { 4 });
                reader.SetPressure(4, -92m); // -92 ≤ -90 到位 → 会自动上电进入 Aging

                Check("[恢复] 在测期间生成任务快照文件",
                    WaitUntil(() => File.Exists(integSnap), 2000));
                Check("[恢复] 上电进入老化后仍在测(GetTestingCount=1)",
                    WaitUntil(() => io.ReadOutput(PowerOut(config, 4)), 2500) && dm.GetTestingCount() == 1);

                // 模拟断电：直接 Dispose（不走 StopTesting），快照应保留
                dm.Dispose();

                var pending = TestSessionStore.Load();
                Check("[恢复] 异常退出后能读到待恢复快照", pending != null && pending.Stations.Count >= 1);
                Check("[恢复] 快照记录了批号与定格参数",
                    pending != null && pending.Stations.Any(s => s.DeviceId == 4 && s.DurationSeconds == 30));

                // 重启（新 DeviceManager + 新 Fake）→ 恢复整台重测：重新只开阀进抽真空态
                FakeBarometerReader reader2; FakeIoController io2; DeviceConfig config2;
                DeviceManager dm2 = BuildTestManager(out reader2, out io2, out config2);
                try
                {
                    dm2.RecoverSession(pending);
                    Thread.Sleep(150);
                    Check("[恢复] 整台重测从抽真空开始(阀开/电不开)",
                        io2.ReadOutput(ValveOut(config2, 4)) && !io2.ReadOutput(PowerOut(config2, 4)));
                    Check("[恢复] 恢复后回到在测状态", dm2.GetTestingCount() == 1);
                }
                finally
                {
                    // 放弃路径验证：DiscardSession 应安全关闭并清快照
                    var p2 = dm2.LoadPendingSession();
                    dm2.DiscardSession(p2);
                    Check("[恢复] 放弃恢复后阀与电源均已关闭",
                        !io2.ReadOutput(ValveOut(config2, 4)) && !io2.ReadOutput(PowerOut(config2, 4)));
                    Check("[恢复] 放弃后快照已删除", TestSessionStore.Load() == null);
                    dm2.Dispose();
                }
            }
            finally
            {
                // 兜底清理：急停语义（关全部输出+删快照）再释放，避免影响后续模块。
                // 注意 dm 可能已在场景5 中 Dispose，StopAll/Dispose 需容忍重复释放。
                try { dm.StopAll(); } catch { }
                try { dm.Dispose(); } catch { }
                try { TestSessionStore.Clear(); } catch { }
                try { if (integBase != null) integBase.SetValue(null, null); } catch { }
            }
        }

        // =====================================================================
        // 15c. 工艺策略端到端（V1.67 新增：一期 L2 策略层执行侧）
        // 与 15/15b 共用 Fake 三件套；每个策略独立 dm，互不污染。
        // 策略缺省=现状的部分由既有场景覆盖（15 的 PASS/FAIL/重测），这里只测
        // 非缺省分支：治具责任 / 待判定+下料 / 失压保持 / 续跑 / 泄压。
        // =====================================================================
        private static void DeviceManagerPolicyTests()
        {
            EnterCleanDir();

            // 当日 CSV 行数统计（报警边沿单次/下料判定/失压标记不断言用）
            Func<string, int> countLines = marker =>
            {
                try
                {
                    string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs",
                        "TestLog_" + DateTime.Now.ToString("yyyyMMdd") + ".csv");
                    if (!File.Exists(file)) return 0;
                    int n = 0;
                    foreach (string line in File.ReadAllLines(file))
                    {
                        if (line.Contains(marker)) n++;
                    }
                    return n;
                }
                catch { return 0; }
            };

            // ---------- P1 真空失败记治具（Q19=FixtureAlarm） ----------
            FakeBarometerReader r1; FakeIoController io1; DeviceConfig c1;
            DeviceManager dm1 = BuildTestManager(out r1, out io1, out c1);
            try
            {
                c1.VacuumFailKind = VacuumFailKind.FixtureAlarm;
                dm1.StartTesting(new[] { 1 });
                // 常压 0 恒越限：600ms 宽限耗尽判真空建立失败
                Check("[策略P1] 治具责任下真空失败仍报警断电标Fault",
                    WaitUntil(() =>
                    {
                        var d = dm1.GetBarometerData(1);
                        return d != null && d.Status == DeviceStatus.Fault;
                    }, 3000));
                Check("[策略P1] 结果记装夹异常而非FAIL",
                    (dm1.GetBarometerData(1) ?? new BarometerData()).LastTestResult == "装夹异常");
                Check("[策略P1] CSV记报警(装夹异常)",
                    WaitUntil(() => countLines("报警(装夹异常)") >= 1, 2000));
            }
            finally { try { dm1.StopAll(); } catch { } try { dm1.Dispose(); } catch { } }

            // ---------- P2 待判定+下料录入（Q22=PendingReview） ----------
            FakeBarometerReader r2; FakeIoController io2; DeviceConfig c2;
            DeviceManager dm2 = BuildTestManager(out r2, out io2, out c2);
            try
            {
                c2.CompletionJudgePolicy = CompletionJudgePolicy.PendingReview;
                dm2.SetStationRecipe(1, "配方R1", -3m, null);
                dm2.SetStationDelayTimes(1, TimeSpan.Zero, TimeSpan.FromSeconds(1.5));
                dm2.StartTesting(new[] { 1 });
                r2.SetPressure(1, -4m); // 配方阈值-3下到位
                Check("[策略P2] 待判定模式仍正常上电",
                    WaitUntil(() => io2.ReadOutput(PowerOut(c2, 1)), 2500));
                Check("[策略P2] 到时完成但标待判定而非PASS",
                    WaitUntil(() =>
                    {
                        var d = dm2.GetBarometerData(1);
                        return d != null && d.Status == DeviceStatus.Completed
                            && d.LastTestResult == "待判定";
                    }, 4000));
                int[] judged; int[] skipped;
                dm2.RecordUnloadJudge(new[] { 1 }, false, "黑点", "报废", out judged, out skipped);
                Check("[策略P2] 下料判定收完成态台",
                    judged.Length == 1 && judged[0] == 1 && skipped.Length == 0);
                Check("[策略P2] 判定后回空闲(确认取件)",
                    WaitUntil(() =>
                    {
                        var d = dm2.GetBarometerData(1);
                        return d != null && d.Status == DeviceStatus.Idle;
                    }, 2000));
                Check("[策略P2] CSV记下料判定明细",
                    WaitUntil(() => countLines("下料判定") >= 1, 2000));
                dm2.RecordUnloadJudge(new[] { 2 }, true, "", "—", out judged, out skipped);
                Check("[策略P2] 非完成态台跳过不碰",
                    judged.Length == 0 && skipped.Length == 1);
                dm2.RecordUnloadJudge(null, true, "", "—", out judged, out skipped);
                Check("[策略P2] null输入静默", judged.Length == 0 && skipped.Length == 0);
            }
            finally { try { dm2.StopAll(); } catch { } try { dm2.Dispose(); } catch { } }

            // ---------- P3 老化中失压只记不停（Q11=KeepRunning） ----------
            FakeBarometerReader r3; FakeIoController io3; DeviceConfig c3;
            DeviceManager dm3 = BuildTestManager(out r3, out io3, out c3);
            try
            {
                c3.AgingPressureLossPolicy = AgingPressureLossPolicy.KeepRunning;
                dm3.SetStationRecipe(1, "配方R1", -5m, null);
                dm3.SetStationDelayTimes(1, TimeSpan.Zero, TimeSpan.FromSeconds(60)); // 长老化不自己完成
                dm3.StartTesting(new[] { 1 });
                r3.SetPressure(1, -6m);
                Check("[策略P3] 到位上电进入老化",
                    WaitUntil(() => io3.ReadOutput(PowerOut(c3, 1)), 2500));
                r3.SetPressure(1, 0m); // 老化中掉真空
                Thread.Sleep(600);     // 数个采集周期：缺省会报警，这里应保持
                var d3 = dm3.GetBarometerData(1);
                Check("[策略P3] 失压不停机(仍Testing非Fault)",
                    d3 != null && d3.Status == DeviceStatus.Testing);
                Check("[策略P3] 失压不写判定结果",
                    string.IsNullOrEmpty((dm3.GetBarometerData(1) ?? new BarometerData()).LastTestResult));
                Check("[策略P3] 边沿记一条失压保持运行",
                    WaitUntil(() => countLines("失压保持运行") >= 1, 2000));
                Thread.Sleep(600);
                Check("[策略P3] 不刷屏(仍只有一条)",
                    countLines("失压保持运行") == 1);
            }
            finally { try { dm3.StopAll(); } catch { } try { dm3.Dispose(); } catch { } }

            // ---------- P4 断电续跑（Q21①=ResumeRemaining） ----------
            FakeBarometerReader r4; FakeIoController io4; DeviceConfig c4;
            DeviceManager dm4 = BuildTestManager(out r4, out io4, out c4);
            try
            {
                // 先跑进 Aging，验证快照带阶段+上电时刻
                dm4.SetStationRecipe(1, "配方R1", -5m, null);
                dm4.SetStationDelayTimes(1, TimeSpan.Zero, TimeSpan.FromSeconds(100));
                dm4.StartTesting(new[] { 1 });
                r4.SetPressure(1, -6m);
                Check("[策略P4] 跑进老化",
                    WaitUntil(() => io4.ReadOutput(PowerOut(c4, 1)), 2500));
                var snap = TestSessionStore.Load();
                var s1 = snap != null && snap.Stations != null
                    ? snap.Stations.Find(s => s != null && s.DeviceId == 1) : null;
                Check("[策略P4] 快照带Aging阶段",
                    s1 != null && s1.Phase == (int)AgingPhase.Aging);
                Check("[策略P4] 快照带有上电时刻",
                    s1 != null && s1.PowerOnTime != default(DateTime));
                dm4.StopAll();
                TestSessionStore.Clear();

                // 手工构造"中断40秒"的快照，在新 dm 上续跑：100-40=60
                FakeBarometerReader r4b; FakeIoController io4b; DeviceConfig c4b;
                DeviceManager dm4b = BuildTestManager(out r4b, out io4b, out c4b);
                try
                {
                    c4b.PowerLossPolicy = PowerLossPolicy.ResumeRemaining;
                    DateTime now = DateTime.Now;
                    var session = new TestSession
                    {
                        LotNumber = "LOT-RESUME",
                        SavedAt = now,
                        Stations = new List<TestSessionStation>
                        {
                            new TestSessionStation
                            {
                                DeviceId = 1, SerialNumber = "S1", RecipeName = "R",
                                DurationSeconds = 100, DelaySeconds = 0,
                                AlarmThresholdKPa = -5m,
                                Phase = (int)AgingPhase.Aging,
                                PowerOnTime = now.AddSeconds(-40)
                            }
                        }
                    };
                    dm4b.RecoverSession(session);
                    Thread.Sleep(150);
                    int resumed = -1;
                    try
                    {
                        resumed = ((int[])GetDmField(dm4b, "_sessionDurationSecs"))[0];
                    }
                    catch { }
                    Check("[策略P4] 续跑时长=剩余(100-40=60)",
                        resumed == 60);
                    Check("[策略P4] 续跑从重抽真空开始(阀开/电不开)",
                        io4b.ReadOutput(ValveOut(c4b, 1)) && !io4b.ReadOutput(PowerOut(c4b, 1)));
                    dm4b.DiscardSession(dm4b.LoadPendingSession());
                }
                finally { try { dm4b.StopAll(); } catch { } try { dm4b.Dispose(); } catch { } }
            }
            finally { try { dm4.StopAll(); } catch { } try { dm4.Dispose(); } catch { } }

            // ---------- P5 完成泄压+复位关阀（Q6/Q15=PowerOffVentAndBeep） ----------
            FakeBarometerReader r5; FakeIoController io5; DeviceConfig c5;
            DeviceManager dm5 = BuildTestManager(out r5, out io5, out c5);
            try
            {
                c5.CompletionAction = CompletionAction.PowerOffVentAndBeep;
                c5.VentValveDoPoint = 200; // Fake 输出空间 240，200 合法
                c5.VentValveEnabled = true; // V1.73：无阀不写 DO，e2e 必须先声明有阀
                dm5.SetStationRecipe(1, "配方R1", -3m, null);
                dm5.SetStationDelayTimes(1, TimeSpan.Zero, TimeSpan.FromSeconds(1.5));
                dm5.StartTesting(new[] { 1 });
                r5.SetPressure(1, -4m);
                Check("[策略P5] 到时完成",
                    WaitUntil(() =>
                    {
                        var d = dm5.GetBarometerData(1);
                        return d != null && d.Status == DeviceStatus.Completed;
                    }, 4000));
                Check("[策略P5] 破空阀已开启",
                    WaitUntil(() => io5.ReadOutput(200), 1000));
                Check("[策略P5] CSV记破空泄压",
                    WaitUntil(() => countLines("破空泄压") >= 1, 2000));
                dm5.ResetDevices(new[] { 1 });
                Check("[策略P5] 复位关闭破空阀(不残留)",
                    WaitUntil(() => !io5.ReadOutput(200), 1000));
            }
            finally { try { dm5.StopAll(); } catch { } try { dm5.Dispose(); } catch { } }

            try { TestSessionStore.Clear(); } catch { }
        }

        // =====================================================================
        // 15d. MES 上报端到端（V1.68 新增：二期映射层可配）
        // Fake 传输抓包（MesReporter.Transport 静态缝），全程零外网：
        // 启动/完成/报警/下料四个触发器各抓一条，断言映射改名+静态合并+关键字段。
        // =====================================================================
        private static void DeviceManagerMesTests()
        {
            EnterCleanDir();

            var captured = new List<Tuple<string, string>>();
            MesReporter.Transport = (url, json, headers, timeout) =>
            {
                lock (captured) { captured.Add(Tuple.Create(url, json)); }
                return true;
            };
            try
            {
                FakeBarometerReader reader; FakeIoController io; DeviceConfig config;
                DeviceManager dm = BuildTestManager(out reader, out io, out config);
                try
                {
                    config.MesEnabled = true;
                    config.MesEndpoint = "http://fake-mes:8080/api";
                    config.MesRetryCount = 0;
                    config.MesTriggers = "Start,Complete,Alarm,UnloadJudge";
                    config.MesFieldMap = "eqId=device";
                    config.MesStaticFields = "line=L5";

                    // —— 启动 + 完成（工位1，短老化 1.5s）——
                    dm.SetStationRecipe(1, "配方R1", -3m, null);
                    dm.SetStationDelayTimes(1, TimeSpan.Zero, TimeSpan.FromSeconds(1.5));
                    dm.SetStationSerialNumber(1, "SN-MES-001");
                    dm.StartTesting(new[] { 1 });
                    reader.SetPressure(1, -4m);
                    Check("[MES端到端] 启动事件上报",
                        WaitUntil(() =>
                        {
                            lock (captured)
                            {
                                return captured.Any(c => c.Item2.Contains("\"event\":\"Start\""));
                            }
                        }, 3000));
                    Check("[MES端到端] 到时完成",
                        WaitUntil(() =>
                        {
                            var d = dm.GetBarometerData(1);
                            return d != null && d.Status == DeviceStatus.Completed;
                        }, 4000));
                    string completeJson = "";
                    lock (captured)
                    {
                        var hit = captured.FirstOrDefault(c => c.Item2.Contains("\"event\":\"Complete\""));
                        if (hit != null) completeJson = hit.Item2;
                    }
                    Check("[MES端到端] 完成含结果PASS",
                        completeJson.Contains("\"result\":\"PASS\""));
                    Check("[MES端到端] 完成映射改名(eqId)+静态(line)+SN",
                        completeJson.Contains("\"eqId\":\"1\"")
                        && completeJson.Contains("\"line\":\"L5\"")
                        && completeJson.Contains("SN-MES-001"));

                    // —— 下料判定（工位1，刚完成的台）——
                    int[] judged; int[] skipped;
                    dm.RecordUnloadJudge(new[] { 1 }, false, "黑点", "报废",
                        out judged, out skipped);
                    Check("[MES端到端] 下料判定事件上报",
                        WaitUntil(() =>
                        {
                            lock (captured)
                            {
                                return captured.Any(c => c.Item2.Contains("\"event\":\"UnloadJudge\"")
                                    && c.Item2.Contains("黑点"));
                            }
                        }, 3000));

                    // —— 报警（工位2，真空建立失败）——
                    dm.StartTesting(new[] { 2 });
                    Check("[MES端到端] 报警事件上报",
                        WaitUntil(() =>
                        {
                            lock (captured)
                            {
                                return captured.Any(c => c.Item2.Contains("\"event\":\"Alarm\"")
                                    && c.Item2.Contains("\"result\":\"FAIL\""));
                            }
                        }, 4000));
                    lock (captured)
                    {
                        Check("[MES端到端] 四个触发器各至少一条",
                            captured.Any(c => c.Item2.Contains("\"event\":\"Start\""))
                            && captured.Any(c => c.Item2.Contains("\"event\":\"Complete\""))
                            && captured.Any(c => c.Item2.Contains("\"event\":\"Alarm\""))
                            && captured.Any(c => c.Item2.Contains("\"event\":\"UnloadJudge\"")));
                        Check("[MES端到端] 发往配置地址",
                            captured.All(c => c.Item1 == "http://fake-mes:8080/api"));
                    }
                }
                finally { try { dm.StopAll(); } catch { } try { dm.Dispose(); } catch { } }
            }
            finally
            {
                MesReporter.Transport = null;
                try { TestSessionStore.Clear(); } catch { }
            }
        }

        // =====================================================================
        // 15e. 规则流程端到端（V1.69 新增：三期规则表达式 + 阶段流）
        // 场景：R1 自定义报警触发FAIL / R2 完成表达式提前完成 /
        //       R3 跳过抽真空直接上电（压力豁免不误报）。
        // =====================================================================
        private static void DeviceManagerRulesTests()
        {
            EnterCleanDir();

            // ---------- R1 自定义报警（压力规则，常压0恒成立，立即触发） ----------
            FakeBarometerReader r1; FakeIoController io1; DeviceConfig c1;
            DeviceManager dm1 = BuildTestManager(out r1, out io1, out c1);
            try
            {
                c1.VacuumConfirmTimeoutMs = 60000;   // 内置真空失败先靠边站，只看自定义规则
                c1.CustomAlarmRules = "失压测试 | pressure > -1 | 0";
                dm1.StartTesting(new[] { 1 });
                // 常压 0 > -1：首轮即触发（内置宽限窗口还没耗尽，证明是规则先开的火）
                Check("[规则R1] 自定义报警标Fault",
                    WaitUntil(() =>
                    {
                        var d = dm1.GetBarometerData(1);
                        return d != null && d.Status == DeviceStatus.Fault;
                    }, 3000));
                Check("[规则R1] 结果记FAIL(非真空类，Q19不改它)",
                    (dm1.GetBarometerData(1) ?? new BarometerData()).LastTestResult == "FAIL");
                Check("[规则R1] CSV记规则名",
                    WaitUntil(() => HasCsvLine("自定义规则[失压测试]触发"), 2000));
            }
            finally { try { dm1.StopAll(); } catch { } try { dm1.Dispose(); } catch { } }

            // ---------- R2 完成表达式提前完成（agesecs>=1，定格60s） ----------
            FakeBarometerReader r2; FakeIoController io2; DeviceConfig c2;
            DeviceManager dm2 = BuildTestManager(out r2, out io2, out c2);
            try
            {
                c2.CompleteExpression = "agesecs >= 1";
                dm2.SetStationRecipe(1, "配方R1", -3m, null);
                dm2.SetStationDelayTimes(1, TimeSpan.Zero, TimeSpan.FromSeconds(60));
                dm2.StartTesting(new[] { 1 });
                r2.SetPressure(1, -4m);
                Check("[规则R2] 上电进入老化",
                    WaitUntil(() => io2.ReadOutput(PowerOut(c2, 1)), 2500));
                Check("[规则R2] 上电约1秒后提前完成（远不到60s）",
                    WaitUntil(() =>
                    {
                        var d = dm2.GetBarometerData(1);
                        return d != null && d.Status == DeviceStatus.Completed;
                    }, 8000));
                Check("[规则R2] CSV记自定义完成原因",
                    WaitUntil(() => HasCsvLine("自定义完成条件触发"), 2000));
            }
            finally { try { dm2.StopAll(); } catch { } try { dm2.Dispose(); } catch { } }

            // ---------- R3 跳过抽真空（常压0不报警，直接上电） ----------
            FakeBarometerReader r3; FakeIoController io3; DeviceConfig c3;
            DeviceManager dm3 = BuildTestManager(out r3, out io3, out c3);
            try
            {
                c3.SkipVacuum = true;
                dm3.SetStationDelayTimes(1, TimeSpan.Zero, TimeSpan.FromSeconds(60));
                dm3.StartTesting(new[] { 1 });
                // 常压 0：内置压力报警被豁免，不会Fault；载台应立即上电（不等真空）
                Check("[规则R3] 跳过抽真空直接上电",
                    WaitUntil(() => io3.ReadOutput(PowerOut(c3, 1)), 1500));
                Thread.Sleep(400);
                var d3 = dm3.GetBarometerData(1);
                Check("[规则R3] 常压下不误报（仍Testing）",
                    d3 != null && d3.Status == DeviceStatus.Testing);
                var snap = TestSessionStore.Load();
                var s1 = snap != null && snap.Stations != null
                    ? snap.Stations.Find(s => s != null && s.DeviceId == 1) : null;
                Check("[规则R3] 快照阶段=Aging（计时起点即启动时刻）",
                    s1 != null && s1.Phase == (int)AgingPhase.Aging
                    && s1.PowerOnTime != default(DateTime));
                Check("[规则R3] CSV记跳过抽真空",
                    WaitUntil(() => HasCsvLine("跳过抽真空"), 2000));
            }
            finally { try { dm3.StopAll(); } catch { } try { dm3.Dispose(); } catch { } }

            // ---------- R4 各阶段台数（V1.70 驾驶舱计数口径） ----------
            FakeBarometerReader r4; FakeIoController io4; DeviceConfig c4;
            DeviceManager dm4 = BuildTestManager(out r4, out io4, out c4);
            try
            {
                dm4.SetStationDelayTimes(1, TimeSpan.Zero, TimeSpan.FromSeconds(60));
                dm4.SetStationDelayTimes(2, TimeSpan.Zero, TimeSpan.FromSeconds(60));
                dm4.StartTesting(new[] { 1, 2 });
                r4.SetPressure(1, -6m); // 1号到位上电进老化；2号常压0留抽真空
                Check("[规则R4] 1号上电",
                    WaitUntil(() => io4.ReadOutput(PowerOut(c4, 1)), 2500));
                Thread.Sleep(200); // 等一轮采集把缓存状态刷齐
                int vac, age, done, fault, idle;
                dm4.GetPhaseCounts(out vac, out age, out done, out fault, out idle);
                Check("[规则R4] 抽真空1/老化1",
                    vac == 1 && age == 1);
                Check("[规则R4] 完成0/故障0/空闲2",
                    done == 0 && fault == 0 && idle == 2);
            }
            finally { try { dm4.StopAll(); } catch { } try { dm4.Dispose(); } catch { } }

            try { TestSessionStore.Clear(); } catch { }
        }

        // =====================================================================
        // 15c. DeviceManager 身份口径端到端（V1.76 新增：SN/配方/结果结构化列 E2E）
        // 与手工验证清单一一对应：启动行结果空/完成行结果=判定值/定格模式中途重绑
        // 报警仍归属启动 SN。不 mock CSV 解析器，读真实落盘文件（与 HistoryCsv 互逆
        // 形成"写→读"闭环：这里锁"写对了"，HistoryCsv 锁"读对了"）。
        // =====================================================================
        private static void DeviceManagerIdentityTests()
        {
            EnterCleanDir();
            FakeBarometerReader reader; FakeIoController io; DeviceConfig config;
            DeviceManager dm = BuildTestManager(out reader, out io, out config);
            try
            {
                // ---------- I1 现值模式（缺省）启动→完成：11 列 E2E ----------
                dm.CurrentLotNumber = "LOT-ID";
                dm.SetStationSerialNumber(1, "SN-1");
                dm.SetStationRecipe(1, "R1", -3m, null);
                dm.SetStationDelayTimes(1, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1.5));
                dm.StartTesting(new[] { 1 });
                Thread.Sleep(120); // 过几个采集周期（BuildTestManager 初始常压 0，先别触发宽限）
                reader.SetPressure(1, -4m); // 配方阈值 -3 下到位
                Check("[身份] 1号到时完成",
                    WaitUntil(() =>
                    {
                        var d = dm.GetBarometerData(1);
                        return d != null && d.Status == DeviceStatus.Completed;
                    }, 5000));
                string[] startRow = FindCsvRow(1, "启动");
                Check("[身份] 启动行SN/配方有值",
                    startRow != null && startRow[2] == "SN-1" && startRow[3] == "R1");
                Check("[身份] 启动行结果记空（未跑完无判定）",
                    startRow != null && startRow[6] == "");
                Check("[身份] 启动详情无SN字串（专属列已承载，不重复）",
                    startRow != null && !startRow[7].Contains("SN:"));
                string[] doneRow = FindCsvRow(1, "完成");
                Check("[身份] 完成行SN/配方有值",
                    doneRow != null && doneRow[2] == "SN-1" && doneRow[3] == "R1");
                Check("[身份] 完成行结果=判定值PASS",
                    doneRow != null && doneRow[6] == "PASS");

                // ---------- I2 定格模式：中途重绑→报警归属启动 SN ----------
                config.EventIdentityMode = EventIdentityMode.StartSnapshot; // 热切（helper 读实时值）
                dm.SetStationSerialNumber(2, "SN-A");
                dm.SetStationRecipe(2, "RA", -3m, null);
                dm.StartTesting(new[] { 2 });
                Check("[身份] 2号进入在测",
                    WaitUntil(() => dm.GetTestingCount() >= 1, 2000));
                dm.SetStationSerialNumber(2, "SN-B"); // 在测中途重绑（测试中台不断测）
                Check("[身份] 在测中途重绑不断测",
                    dm.GetTestingCount() >= 1 && io.ReadOutput(ValveOut(config, 2)));
                // 常压 0 恒越限：600ms 宽限耗尽判真空建立失败
                Check("[身份] 重绑后报警",
                    WaitUntil(() =>
                    {
                        var d = dm.GetBarometerData(2);
                        return d != null && d.Status == DeviceStatus.Fault;
                    }, 3000));
                string[] almRow = FindCsvRow(2, "报警(FAIL)");
                Check("[身份] 定格模式报警行归属启动SN",
                    almRow != null && almRow[2] == "SN-A");
                Check("[身份] 报警行结果=FAIL",
                    almRow != null && almRow[6] == "FAIL");

                // ---------- I3 现值模式：同样重绑→报警归属现值 ----------
                config.EventIdentityMode = EventIdentityMode.RecordTime;
                dm.SetStationSerialNumber(3, "SN-C");
                dm.SetStationRecipe(3, "RC", -3m, null);
                dm.StartTesting(new[] { 3 });
                dm.SetStationSerialNumber(3, "SN-D");
                Check("[身份] 3号报警",
                    WaitUntil(() =>
                    {
                        var d = dm.GetBarometerData(3);
                        return d != null && d.Status == DeviceStatus.Fault;
                    }, 3000));
                string[] almRow3 = FindCsvRow(3, "报警(FAIL)");
                Check("[身份] 现值模式报警行归属重绑后SN",
                    almRow3 != null && almRow3[2] == "SN-D");
            }
            finally { try { dm.StopAll(); } catch { } try { dm.Dispose(); } catch { } }
        }

        /// <summary>当日事件 CSV 含标记的行数（前后差值断言，防旧运行残留行假绿）</summary>
        private static int CountCsvMarker(string marker)
        {
            try
            {
                string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs",
                    "TestLog_" + DateTime.Now.ToString("yyyyMMdd") + ".csv");
                if (!File.Exists(file)) return 0;
                int n = 0;
                foreach (string line in File.ReadAllLines(file))
                {
                    if (line.Contains(marker)) n++;
                }
                return n;
            }
            catch { return 0; }
        }

        /// <summary>取当日 CSV 里某台某事件的最后一行（V1.76 列序拆列；无则 null）</summary>
        private static string[] FindCsvRow(int deviceId, string evt)
        {
            try
            {
                string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs",
                    "TestLog_" + DateTime.Now.ToString("yyyyMMdd") + ".csv");
                if (!File.Exists(file)) return null;
                string[] found = null;
                foreach (string line in File.ReadAllLines(file))
                {
                    string[] cols = line.Split(',');
                    if (cols.Length >= 11 && cols[4].Trim() == deviceId.ToString()
                        && cols[5].Trim() == evt) found = cols;
                }
                return found;
            }
            catch { return null; }
        }

        /// <summary>当日事件 CSV 是否含标记行（规则场景的CSV断言用）</summary>
        private static bool HasCsvLine(string marker)
        {
            try
            {
                string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs",
                    "TestLog_" + DateTime.Now.ToString("yyyyMMdd") + ".csv");
                if (!File.Exists(file)) return false;
                foreach (string line in File.ReadAllLines(file))
                {
                    if (line.Contains(marker)) return true;
                }
                return false;
            }
            catch { return false; }
        }

        // =====================================================================
        // 15d. 大扫荡行为锁（V1.84 复查补齐：编排核心修复当时只修了产品代码，
        // DeviceManager 行为没有回归用例锁，15/15b 全是老行为）。
        // 用 Fake 写失败注入 + 短数组 + 反射，直接锁：
        // 上电失败回滚 / 停止先写后清 / 短数组尾部按读失败 / 广播按总数 /
        // SkipVacuum 启动定格 / 报警原因文案 / 下料认领原子化 / 热更重建数组 /
        // 急停关破空阀 / 订阅异常隔离 / 负延时钳零。
        // =====================================================================
        private static void DeviceManagerSweepTests()
        {
            string dir = EnterCleanDir();
            var fBase = typeof(TestSessionStore).GetField("BaseDirOverride",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (fBase != null) fBase.SetValue(null, dir);
            try
            {
                SweepPowerFailRollback();
                SweepStopWriteBeforeClear();
                SweepShortArrayAndBroadcast();
                SweepSkipVacuumFreeze();
                SweepAlarmReasons();
                SweepJudgeClaim();
                SweepRebuildArrays();
                SweepStopAllVent();
                SweepStopAllFanFail();
                SweepSubscriberIsolation();
                SweepNegativeDelay();
                SweepVacuumInRangeFlag();
            }
            finally
            {
                try { if (fBase != null) fBase.SetValue(null, null); } catch { }
            }
        }

        /// <summary>反射调 DeviceManager 私有方法（ClassifyAlarm 原因文案用）</summary>
        private static object InvokeDm(DeviceManager dm, string name, params object[] args)
        {
            var mi = typeof(DeviceManager).GetMethod(name,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return mi.Invoke(dm, args);
        }

        /// <summary>停采集定时器（反射探测/改状态时防 30ms 采集循环竞态，用完必须 Start）</summary>
        private static void PauseCollect(DeviceManager dm)
        {
            var t = GetDmField(dm, "_collectTimer") as System.Timers.Timer;
            if (t != null) t.Stop();
        }

        private static void ResumeCollect(DeviceManager dm)
        {
            var t = GetDmField(dm, "_collectTimer") as System.Timers.Timer;
            if (t != null) t.Start();
        }

        // ---------- S1 上电写失败回滚：不断假 PASS，恢复后自动补上电 ----------
        private static void SweepPowerFailRollback()
        {
            FakeBarometerReader reader; FakeIoController io; DeviceConfig config;
            DeviceManager dm = BuildTestManager(out reader, out io, out config);
            try
            {
                dm.StartTesting(new[] { 1 });
                Check("[大扫荡][上电回滚] 开阀",
                    WaitUntil(() => io.ReadOutput(ValveOut(config, 1)), 2000));
                int pwFailBefore = CountCsvMarker("上电失败");
                io.SetThrowOnWrite(true);          // 耦合器恰在上电瞬间掉线
                reader.SetPressure(1, -95m);       // 压力到位 → 下一轮即尝试上电
                int pwr = PowerOut(config, 1);
                Check("[大扫荡][上电回滚] 上电被尝试下发",
                    WaitUntil(() => io.WasAttempted(pwr), 2000));
                Thread.Sleep(200);                 // 再跑几轮：确认回滚态稳定
                Check("[大扫荡][上电回滚] 写失败台仍在测（等重试，不死不假PASS）",
                    dm.GetTestingStates()[0]);
                Check("[大扫荡][上电回滚] 载台实际没带电",
                    !io.ReadOutput(pwr) && !io.EverPowerOn(pwr));
                Check("[大扫荡][上电回滚] 记上电失败事件",
                    WaitUntil(() => CountCsvMarker("上电失败") > pwFailBefore, 2000));
                io.SetThrowOnWrite(false);         // 耦合器恢复
                Check("[大扫荡][上电回滚] 恢复后自动补上电",
                    WaitUntil(() => io.ReadOutput(pwr), 3000));
            }
            finally { try { io.SetThrowOnWrite(false); } catch { } try { dm.StopAll(); } catch { } try { dm.Dispose(); } catch { } }
        }

        // ---------- S2 停止先写后清：写失败抛，状态不清、台还显示在测 ----------
        private static void SweepStopWriteBeforeClear()
        {
            FakeBarometerReader reader; FakeIoController io; DeviceConfig config;
            DeviceManager dm = BuildTestManager(out reader, out io, out config);
            try
            {
                dm.StartTesting(new[] { 2 });
                Check("[大扫荡][停止] 开阀",
                    WaitUntil(() => io.ReadOutput(ValveOut(config, 2)), 2000));
                io.SetThrowOnWrite(true);
                bool threw = false;
                try { dm.StopTesting(new[] { 2 }); }
                catch { threw = true; }
                Check("[大扫荡][停止] 写失败抛给调用方", threw);
                Check("[大扫荡][停止] 状态不清、台还显示在测",
                    dm.GetTestingStates()[1]);
                io.SetThrowOnWrite(false);
                dm.StopTesting(new[] { 2 });
                Check("[大扫荡][停止] 恢复后停干净",
                    !dm.GetTestingStates()[1] && !io.ReadOutput(ValveOut(config, 2)));
            }
            finally { try { io.SetThrowOnWrite(false); } catch { } try { dm.StopAll(); } catch { } try { dm.Dispose(); } catch { } }
        }

        // ---------- S3 短数组尾部按读失败 + S4 广播按总数 ----------
        private static void SweepShortArrayAndBroadcast()
        {
            FakeBarometerReader reader; FakeIoController io; DeviceConfig config;
            DeviceManager dm = BuildTestManager(out reader, out io, out config);
            BarometerData[] got = null;
            EventHandler<BarometerData[]> sub = (s, d) => { got = d; };
            try
            {
                // 在测台才标 Fault（空闲台不断言：失联分支只处理在测台，
                // 空闲台看"在线计数"），故先启动 3/4 再截断。
                int commBefore = CountCsvMarker("通讯故障");
                reader.SetPressure(3, -95m);
                reader.SetPressure(4, -95m);
                reader.SetReturnCount(2);   // 只回前 2 台：模拟短数组读取器
                dm.StartTesting(new[] { 3, 4 });
                Check("[大扫荡][短数组] 尾部按失联标Fault",
                    WaitUntil(() =>
                    {
                        var d3 = dm.GetBarometerData(3);
                        var d4 = dm.GetBarometerData(4);
                        return d3 != null && d4 != null
                            && d3.Status == DeviceStatus.Fault
                            && d4.Status == DeviceStatus.Fault;
                    }, 4000));
                Check("[大扫荡][短数组] 失联关阀断电",
                    !io.ReadOutput(ValveOut(config, 3)) && !io.ReadOutput(PowerOut(config, 3)));
                Check("[大扫荡][短数组] 记通讯故障事件",
                    CountCsvMarker("通讯故障") > commBefore);
                Check("[大扫荡][短数组] 头部正常台不受牵连",
                    dm.GetBarometerData(1).Status != DeviceStatus.Fault
                    && dm.GetBarometerData(2).Status != DeviceStatus.Fault);
                dm.OnBatchDataUpdated += sub;
                Thread.Sleep(300);
                Check("[大扫荡][广播] 短数组下广播仍按总数（下游按索引取不錯位）",
                    got != null && got.Length == config.TotalBarometers);
            }
            finally
            {
                try { dm.OnBatchDataUpdated -= sub; } catch { }
                try { reader.SetReturnCount(null); } catch { }
                try { dm.ResetDevices(new[] { 3, 4 }); } catch { }
                try { dm.StopAll(); } catch { } try { dm.Dispose(); } catch { }
            }
        }

        // ---------- S5 SkipVacuum 启动定格：运行中翻开关不影响在跑任务 ----------
        private static void SweepSkipVacuumFreeze()
        {
            FakeBarometerReader reader; FakeIoController io; DeviceConfig config;
            DeviceManager dm = BuildTestManager(out reader, out io, out config);
            try
            {
                config.SkipVacuum = false;
                dm.StartTesting(new[] { 1 });
                Check("[大扫荡][定格] 启动进入在测",
                    WaitUntil(() => dm.GetTestingStates()[0], 2000));
                var frozen = GetDmField(dm, "_sessionSkipVacuum") as bool[];
                Check("[大扫荡][定格] 启动定格false", frozen != null && frozen[0] == false);
                config.SkipVacuum = true;   // 运行中翻开关
                Thread.Sleep(200);
                frozen = GetDmField(dm, "_sessionSkipVacuum") as bool[];
                Check("[大扫荡][定格] 运行中翻开关不改定格", frozen != null && frozen[0] == false);
            }
            finally { try { config.SkipVacuum = false; } catch { } try { dm.StopAll(); } catch { } try { dm.Dispose(); } catch { } }
        }

        // ---------- S6 报警原因文案：超时/越限/DI 各说各的 ----------
        private static void SweepAlarmReasons()
        {
            FakeBarometerReader reader; FakeIoController io; DeviceConfig config;
            DeviceManager dm = BuildTestManager(out reader, out io, out config);
            try
            {
                dm.StartTesting(new[] { 4 });
                Check("[大扫荡][原因] 4号进入在测",
                    WaitUntil(() => dm.GetTestingStates()[3], 2000));
                PauseCollect(dm);
                try
                {
                    var confirms = GetDmField(dm, "_vacuumConfirmTimes") as DateTime[];
                    var opens = GetDmField(dm, "_valveOpenTimes") as DateTime[];
                    // DI 触点开关默认关：先开（helper 读实时值，热切生效）
                    config.UseDiAlarmContact = true;
                    // 越限：窗口已过（确认态），压力 0 > -5
                    confirms[3] = DateTime.MinValue;
                    object r1 = InvokeDm(dm, "ClassifyAlarm",
                        new BarometerData { DeviceId = 4, VacuumPressure = 0m });
                    string reason1 = (string)r1.GetType().GetField("Reason").GetValue(r1);
                    Check("[大扫荡][原因] 越限说越限",
                        (bool)r1.GetType().GetField("IsAlarm").GetValue(r1)
                        && reason1 != null && reason1.Contains("越限"));
                    // 超时：窗口内 + 开阀 5 秒前 + 压力不到位
                    confirms[3] = DateTime.Now;
                    opens[3] = DateTime.Now.AddSeconds(-5);
                    object r2 = InvokeDm(dm, "ClassifyAlarm",
                        new BarometerData { DeviceId = 4, VacuumPressure = 0m });
                    string reason2 = (string)r2.GetType().GetField("Reason").GetValue(r2);
                    Check("[大扫荡][原因] 超时说超时不说越限",
                        reason2 != null && reason2.Contains("超时") && !reason2.Contains("越限"));
                    // DI：窗口已过 + 压力到位 + 触点闭合
                    confirms[3] = DateTime.MinValue;
                    object r3 = InvokeDm(dm, "ClassifyAlarm",
                        new BarometerData
                        {
                            DeviceId = 4,
                            VacuumPressure = -95m,
                            InputStatus = new bool[] { true }
                        });
                    string reason3 = (string)r3.GetType().GetField("Reason").GetValue(r3);
                    Check("[大扫荡][原因] DI说触点",
                        reason3 != null && reason3.Contains("DI"));
                }
                finally { ResumeCollect(dm); }
                // 行为 wiring：真跑出超时，边沿日志用的就是 Reason（以前写"压力越限"）。
                // 重开确认窗（开阀已久，下一轮即超时），压力恒不到位。
                PauseCollect(dm);
                try
                {
                    var confirms2 = GetDmField(dm, "_vacuumConfirmTimes") as DateTime[];
                    confirms2[3] = DateTime.Now;
                }
                finally { ResumeCollect(dm); }
                reader.SetPressure(4, 0m);   // 恒不到位 → 600ms 宽限耗尽
                Check("[大扫荡][原因] 超时Fault",
                    WaitUntil(() =>
                    {
                        var d = dm.GetBarometerData(4);
                        return d != null && d.Status == DeviceStatus.Fault;
                    }, 4000));
                string[] row = FindCsvRow(4, "报警(FAIL)");
                Check("[大扫荡][原因] 报警行记超时原因",
                    row != null && row.Length > 7 && row[7].Contains("真空建立超时"));
            }
            finally
            {
                try { config.UseDiAlarmContact = false; } catch { }
                try { dm.StopAll(); } catch { } try { dm.Dispose(); } catch { }
            }
        }

        // ---------- S7 负延时钳零 ----------
        private static void SweepNegativeDelay()
        {
            FakeBarometerReader reader; FakeIoController io; DeviceConfig config;
            DeviceManager dm = BuildTestManager(out reader, out io, out config);
            try
            {
                dm.SetStationDelayTimes(1, TimeSpan.FromSeconds(-5), null);
                var info = dm.GetStationInfo(1);
                Check("[大扫荡][延时] 负延时钳零",
                    info != null && info.DelayTime == TimeSpan.Zero);
            }
            finally { try { dm.StopAll(); } catch { } try { dm.Dispose(); } catch { } }
        }

        // ---------- S13 真空到位标记随采集刷新（面板真空块三色的数据源） ----------
        // 口径与报警一致：测试中用启动定格阈值，未测试用全局 -5kPa；
        // 常压 0 → false（面板红），-6 → true（面板绿）。
        // 【不断言阀开关】600ms 真空宽限后未到位会报警关阀，阀态与标记翻转竞态，
        // 只锁"标记跟压力走"，阀开/关的三色组合由面板 ApplyData 反射用例覆盖。
        private static void SweepVacuumInRangeFlag()
        {
            FakeBarometerReader reader; FakeIoController io; DeviceConfig config;
            DeviceManager dm = BuildTestManager(out reader, out io, out config);
            try
            {
                dm.StartTesting(new[] { 1 });
                Check("[真空灯] 开阀",
                    WaitUntil(() => io.ReadOutput(ValveOut(config, 1)), 2000));
                Check("[真空灯] 常压未到位标记false",
                    WaitUntil(() => { var d = dm.GetBarometerData(1); return d != null && !d.VacuumInRange; }, 3000));
                reader.SetPressure(1, -6m);
                Check("[真空灯] 到位后标记true",
                    WaitUntil(() => { var d = dm.GetBarometerData(1); return d != null && d.VacuumInRange; }, 3000));
                // Clone 必须带标记：面板/广播拿到的都是副本，丢了标记灯永远不绿。
                var snap = dm.GetBarometerData(1);
                Check("[真空灯] 副本保留标记",
                    snap != null && snap.Clone().VacuumInRange == snap.VacuumInRange);
            }
            finally { try { dm.StopAll(); } catch { } try { dm.Dispose(); } catch { } }
        }

        // ---------- S10 下料认领原子化：同一台连判两次，第二判进 skipped ----------
        private static void SweepJudgeClaim()
        {
            FakeBarometerReader reader; FakeIoController io; DeviceConfig config;
            DeviceManager dm = BuildTestManager(out reader, out io, out config);
            try
            {
                PauseCollect(dm);
                try
                {
                    // 缓存里造一个 Completed 台（持 _cacheLock 防采集竞态改字典结构）
                    var cacheLock = GetDmField(dm, "_cacheLock");
                    var cache = GetDmField(dm, "_barometerDataCache")
                        as Dictionary<int, BarometerData>;
                    lock (cacheLock)
                    {
                        BarometerData e;
                        if (!cache.TryGetValue(1, out e) || e == null)
                        {
                            e = new BarometerData { DeviceId = 1 };
                            cache[1] = e;
                        }
                        e.Status = DeviceStatus.Completed;
                    }
                }
                finally { ResumeCollect(dm); }
                int[] judged, skipped;
                dm.RecordUnloadJudge(new[] { 1 }, true, "", "", out judged, out skipped);
                Check("[大扫荡][认领] 首判认领",
                    judged.Length == 1 && judged[0] == 1 && skipped.Length == 0);
                int[] judged2, skipped2;
                dm.RecordUnloadJudge(new[] { 1 }, false, "X", "报废", out judged2, out skipped2);
                Check("[大扫荡][认领] 重判进skipped（无双判定行）",
                    judged2.Length == 0 && skipped2.Length == 1 && skipped2[0] == 1);
            }
            finally { try { dm.StopAll(); } catch { } try { dm.Dispose(); } catch { } }
        }

        // ---------- S9 热更重建数组 + 工位静态信息同步清 ----------
        private static void SweepRebuildArrays()
        {
            FakeBarometerReader reader; FakeIoController io; DeviceConfig config;
            DeviceManager dm = BuildTestManager(out reader, out io, out config);
            try
            {
                dm.SetStationSerialNumber(3, "旧项目SN");
                config.TotalBarometers = 6;
                dm.RebuildStationArrays();
                Check("[大扫荡][重建] 调大后数组跟上",
                    dm.GetTestingStates().Length == 6);
                Thread.Sleep(200);   // 采集照跑（读器只回4台，5/6走短数组分支），不抛
                Check("[大扫荡][重建] 重建后采集存活",
                    dm.GetTestingCount() == 0 && dm.GetOnlineCount() == 4);
                config.TotalBarometers = 4;
                dm.RebuildStationArrays();
                Check("[大扫荡][重建] 调小后数组跟上",
                    dm.GetTestingStates().Length == 4);
                Check("[大扫荡][重建] 旧项目SN绑定已清",
                    dm.GetStationInfo(3) == null);
            }
            finally
            {
                try { config.TotalBarometers = 4; dm.RebuildStationArrays(); } catch { }
                try { dm.StopAll(); } catch { } try { dm.Dispose(); } catch { }
            }
        }

        // ---------- S8 急停关破空阀 ----------
        private static void SweepStopAllVent()
        {
            FakeBarometerReader reader; FakeIoController io; DeviceConfig config;
            DeviceManager dm = BuildTestManager(out reader, out io, out config);
            try
            {
                config.VentValveDoPoint = 200;   // Fake 输出空间 240 内合法
                io.WriteOutput(200, true);       // 模拟完成泄压后阀开着
                dm.StopAll();
                Check("[大扫荡][急停] 破空阀同关", !io.ReadOutput(200));
            }
            finally
            {
                try { config.VentValveDoPoint = 0; } catch { }
                try { dm.StopAll(); } catch { } try { dm.Dispose(); } catch { }
            }
        }

        /// <summary>Stop 抛异常的风机（急停风机失败路径用）</summary>
        private sealed class ThrowingFan : AgingTestSystem.Interfaces.IFanController
        {
            public bool IsConnected => true;
            public string ActiveIp => "FAKE-FAN";
            public event EventHandler<string> OnError { add { } remove { } }
            public bool Connect(DeviceConfig config) { return true; }
            public void Disconnect() { }
            public bool ReconnectNow() { return true; }
            public FanData ReadStatus() { return null; }
            public bool StartFixedValue() { return true; }
            public bool Stop() { throw new InvalidOperationException("Fake 风机停失败注入"); }
            public void Dispose() { }
        }

        // ---------- S12 急停风机失败：记账照做（快照清+事件记），异常仍抛 ----------
        private static void SweepStopAllFanFail()
        {
            var config = new DeviceConfig();
            config.TotalBarometers = 4;
            config.TotalInputs = 80;
            config.TotalOutputs = 160;
            config.CollectInterval = 30;
            config.VacuumConfirmTimeoutMs = 600;
            config.CommunicationLossAlarmCount = 3;
            config.AlarmPressureThresholdKPa = -5m;
            config.FanEnabled = false;
            var reader = new FakeBarometerReader(config.TotalBarometers);
            var io = new FakeIoController(config.TotalInputs + config.TotalOutputs);
            for (int i = 1; i <= config.TotalBarometers; i++) reader.SetPressure(i, 0m);
            DeviceManager dm = null;
            try
            {
                dm = new DeviceManager(config, reader, io, new ThrowingFan());
                Check("[大扫荡][急停] 风机注入版启动成功", dm.Start());
                int stopFailBefore = CountCsvMarker("送风机停止失败");
                bool threw = false;
                try { dm.StopAll(); }
                catch (InvalidOperationException) { threw = true; }
                Check("[大扫荡][急停] 风机失败仍抛给UI", threw);
                Check("[大扫荡][急停] 快照照清（下次不问恢复）",
                    TestSessionStore.Load() == null);
                Check("[大扫荡][急停] 急停事件照记且点名风机",
                    CountCsvMarker("送风机停止失败") > stopFailBefore);
            }
            finally { try { if (dm != null) dm.Dispose(); } catch { } }
        }

        // ---------- S11 订阅异常隔离：坏订阅者不影响好订阅者与采集 ----------
        private static void SweepSubscriberIsolation()
        {
            FakeBarometerReader reader; FakeIoController io; DeviceConfig config;
            DeviceManager dm = BuildTestManager(out reader, out io, out config);
            EventHandler<BarometerData[]> bad = (s, d) =>
            {
                throw new InvalidOperationException("订阅者bug");
            };
            BarometerData[] got = null;
            EventHandler<BarometerData[]> good = (s, d) => { got = d; };
            try
            {
                dm.OnBatchDataUpdated += bad;
                dm.OnBatchDataUpdated += good;
                Thread.Sleep(300);
                Check("[大扫荡][订阅] 坏订阅不影响好订阅",
                    got != null && got.Length == config.TotalBarometers);
                Check("[大扫荡][订阅] 采集照跑", dm.GetOnlineCount() == 4);
            }
            finally
            {
                try { dm.OnBatchDataUpdated -= bad; } catch { }
                try { dm.OnBatchDataUpdated -= good; } catch { }
                try { dm.StopAll(); } catch { } try { dm.Dispose(); } catch { }
            }
        }
    }
}
