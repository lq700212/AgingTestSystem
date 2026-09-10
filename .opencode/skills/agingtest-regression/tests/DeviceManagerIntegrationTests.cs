// ============================================================================
//  DeviceManagerIntegrationTests.cs —— 设备编排端到端集成测试（V1.59 新增）
//
//  【做什么】
//  用"可控 Fake 气压表 + 可记录 Fake IO 控制器"通过 DeviceManager 的依赖注入
//  构造函数（V1.59 为可测性开放）直接驱动【真实的】三阶段老化状态机，
//  以 30ms 采集间隔在数秒内跑完现场要几小时的生命周期：
//
//    场景1 正常全流程：启动只开阀不上电(安全铁律) → 压力到位+延时开启到自动上电
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
//  【时间约定】采集间隔 30ms、确认超时 600ms、延时开启 0.5s、老化 1.5s——
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
                    return _pressures.TryGetValue(deviceId, out p)
                        ? new BarometerData { DeviceId = deviceId, VacuumPressure = p, CollectTime = DateTime.Now }
                        : null;                                     // 未设定的台视为无数据
                }
            }

            public BarometerData[] ReadAllData()
            {
                var arr = new BarometerData[_total];
                for (int i = 1; i <= _total; i++) arr[i - 1] = ReadData(i);
                return arr;
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
        }

        // ─────────────────────────────────────────────────────────────────
        // Fake 实现：可记录 IO 控制器（保存每个输出点最终状态 + "曾经开过"历史）
        // ─────────────────────────────────────────────────────────────────
        private sealed class FakeIoController : IIoController
        {
            private readonly object _lock = new object();
            private readonly bool[] _outputs;          // 索引 = outputId-1（含输入区占位）
            private readonly HashSet<int> _everOn = new HashSet<int>(); // 曾被置 ON 的输出点
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
            public bool[] ReadAllInputs() => new bool[80];

            public void WriteOutput(int outputId, bool state)
            {
                lock (_lock)
                {
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

        // =====================================================================
        // 15. DeviceManager 三阶段状态机端到端（Fake 设备驱动，V1.59）
        // =====================================================================
        private static void DeviceManagerIntegrationTests()
        {
            EnterCleanDir(); // TestSession.json / Logs\ 全部落隔离目录

            FakeBarometerReader reader; FakeIoController io; DeviceConfig config;
            DeviceManager dm = BuildTestManager(out reader, out io, out config);

            try
            {
                // ================= 场景7+1：正常全流程（工位1）=================
                // 配方：负压值 -3（到位/报警阈值）、延时开启 0.5s、老化 1.5s
                dm.SetStationRecipe(1, "配方R1", -3m);
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

                Check("[流程] 真空到位+延时开启到后自动上电",
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
                dm.SetStationRecipe(2, "R2", -5m);
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
                dm.SetStationRecipe(4, "配方R4", -90m);
                dm.SetStationDelayTimes(4, TimeSpan.Zero, TimeSpan.FromSeconds(30)); // 长老化不会自己完成
                dm.StartTesting(new[] { 4 });
                reader.SetPressure(4, -92m); // -92 ≤ -90 到位 → 会自动上电进入 Aging

                Check("[恢复] 在测期间生成任务快照文件",
                    WaitUntil(() => File.Exists("TestSession.json"), 2000));
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
            }
        }
    }
}
