using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using AgingTestSystem.Models;
using Newtonsoft.Json;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// MES 上报器（【V1.68 新增】二期传输层：HTTP POST JSON + 后台队列 + 重试 + 离线缓存）。
    ///
    /// 【设计边界】（评审结论，见 DeviceConfig MES 注释）
    /// - 可配的是"报什么/何时报/字段叫什么/往哪几个地址报/带什么头"
    ///   （MesTriggers/MesFieldMap/MesStaticFields/MesEndpointMap/MesCustomHeaders）；
    /// - 传输是代码：POST JSON、鉴权 None/Bearer/Basic、失败重试 N 次、
    ///   最终失败进离线缓存（MesQueue.json），下次成功时顺带补发。
    ///
    /// 【线程模型】Report 只做"组包+入队"，毫秒级返回，绝不阻塞采集/UI；
    /// 真正的 HTTP 在后台线程串行发送（MES 侧按接收顺序处理，不并发打乱）。
    /// 总开关 MesEnabled=false 时 Report 直接返回，worker 线程都不建，零开销。
    ///
    /// 【失败语义】上报失败不影响生产（只记日志 + 进缓存），MES 对接永远不能成为
    /// 停线的原因——这是二期的底线。
    /// </summary>
    public class MesReporter : IDisposable
    {
        /// <summary>
        /// 传输函数缝（测试用）：(url, json, headers, timeoutMs) → true=成功。
        /// 为 null 时走真实 HttpClient。回归用例注入 Fake 抓包，不断言外网。
        /// 生产代码永远不要赋值它（Mock 联调用 MesMockEnabled，不用这个）。
        /// </summary>
        public static Func<string, string, Dictionary<string, string>, int, bool> Transport;

        /// <summary>离线缓存文件名（程序运行目录；绝对路径，见 QueuePath）。</summary>
        public const string QueueFileName = "MesQueue.json";

        /// <summary>离线缓存上限（条）。MES 长期不通时丢最旧的（保内存），丢时记日志。</summary>
        private const int MaxBacklog = 5000;

        /// <summary>内存待发队列上限（条）。以前无界：MES 宕机 + 报警风暴时无限膨胀
        /// 直到 OOM。超限丢最旧（worker 消费时若发生过丢弃，记一条事件说明）。</summary>
        private const int MaxQueue = 2000;

        /// <summary>目录覆盖测试缝（回归隔离用，生产恒 null；用例赋值后 try/finally 复位）。</summary>
        internal static string BaseDirOverride;

        /// <summary>复用的 HttpClient（【大扫荡】以前每次发送 new 一个：高频上报
        /// TIME_WAIT 堆积；单例+每次设 Timeout。工控机目标地址固定，无 DNS 刷新问题）。</summary>
        private static readonly Lazy<System.Net.Http.HttpClient> _httpClient =
            new Lazy<System.Net.Http.HttpClient>(() => new System.Net.Http.HttpClient(), true);

        private readonly DeviceConfig _config;
        private readonly object _lock = new object();
        private readonly Queue<MesItem> _queue = new Queue<MesItem>();
        /// <summary>队列满丢弃计数（worker 消费时记一条事件说明丢了多少，不每条刷屏）。</summary>
        private int _queueDropped;
        /// <summary>上次缓存落盘时刻（落盘节流：报警风暴时不每条全量写文件）。</summary>
        private DateTime _lastBacklogSave = DateTime.MinValue;
        private Thread _worker;
        private AutoResetEvent _signal;
        private bool _stop;
        private bool _disposed;

        /// <summary>待发项（payload 在入队时已按当时配置序列化 + 快照 URL/鉴权，配置后改不影响已入队项）</summary>
        private class MesItem
        {
            public string Url;
            public string Json;
            public Dictionary<string, string> Headers = new Dictionary<string, string>();
            public DateTime CreatedAt;
        }

        public MesReporter(DeviceConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// 上报事件（fire-and-forget：组包+入队即返回，内部全 try/catch，永不抛）。
        /// </summary>
        /// <param name="trigger">触发器（Start/Complete/Alarm/UnloadJudge，见 MesMapping）</param>
        /// <param name="ourFields">本站字段（键见 MesMapping.FieldVocabulary，值全字符串）</param>
        public void Report(string trigger, Dictionary<string, string> ourFields)
        {
            try
            {
                if (_config == null || !_config.MesEnabled) return;
                if (ourFields == null) return;
                if (!MesMapping.TriggerHit(_config.MesTriggers, trigger)) return;

                string json = BuildPayloadJson(ourFields);
                string lot = GetField(ourFields, "lot");
                int device = 0;
                int.TryParse(GetField(ourFields, "device"), out device);

                // Mock 联调：不发 HTTP，只把完整 JSON 写进 CSV（MES 没好也能端到端验证格式）
                if (_config.MesMockEnabled)
                {
                    TestEventLogger.Write(lot, device, "MES上报(Mock)", json,
                        sn: GetField(ourFields, "sn"), recipe: GetField(ourFields, "recipe"),
                        result: GetField(ourFields, "result"));
                    return;
                }

                string url = MesMapping.ResolveEndpoint(
                    trigger,
                    (_config.MesEndpoint ?? "").Trim(),
                    _config.MesEndpointMap);
                if (url.Length == 0) return;   // 默认与分地址都没配：静默跳过（保存时已拦截，这里是手改文件的兜底）

                var item = new MesItem
                {
                    Url = url,
                    Json = json,
                    Headers = BuildHeaders(),
                    CreatedAt = DateTime.Now
                };
                lock (_lock)
                {
                    // 【大扫荡】已释放后不再建线程：直接进离线缓存落盘（以前事件无声丢失）。
                    if (_disposed || _alreadyDisposed)
                    {
                        var single = LoadBacklog();
                        single.Add(item);
                        while (single.Count > MaxBacklog) single.RemoveAt(0);
                        SaveBacklog(single);
                        return;
                    }
                    // 【大扫荡】队列上限：超限丢最旧（计数由 worker 消费时统一记事件）。
                    if (_queue.Count >= MaxQueue)
                    {
                        _queue.Dequeue();
                        _queueDropped++;
                    }
                    _queue.Enqueue(item);
                    EnsureWorker();
                    if (_signal != null) _signal.Set();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MES上报] 入队失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 组包（public static 可单测）：字段映射改名 → 合并静态字段 → 序列化。
        /// 映射为空 = 直通（本站原名）；映射命中改名；静态字段后合并（同名覆盖映射结果——
        /// 常量优先，文档写明）。
        /// </summary>
        public static string BuildPayloadJson(Dictionary<string, string> ourFields,
            string fieldMapRaw, string staticRaw)
        {
            var payload = new Dictionary<string, string>();
            if (ourFields != null)
            {
                foreach (var kv in ourFields)
                {
                    payload[kv.Key] = kv.Value ?? "";
                }
            }
            // 字段映射改名（脏组已跳过，只用合法部分——上报不能因一笔写错全停）
            Dictionary<string, string> map;
            List<string> errors;
            MesMapping.ParseFieldMap(fieldMapRaw, out map, out errors);
            if (map.Count > 0)
            {
                // 【大扫荡】改名即改名：命中映射的本站键删除，不再"复制一份原名一起发"。
                // 以前配 eqId=device 会同时发出 device 和 eqId，严格 schema 的 MES
                // 拒收或内部字段外泄。未命中的本站键照常直通。
                var renamed = new Dictionary<string, string>();
                foreach (var kv in payload)
                {
                    renamed[kv.Key] = kv.Value;
                }
                foreach (var kv in map)
                {
                    // 【大扫荡】本站键大小写不敏感匹配：映射写 eqId=Device 也能命中
                    // payload 的 device（以前精确匹配，大小写一变改名静默失效）。
                    string hitKey = null;
                    foreach (var pk in payload.Keys)
                    {
                        if (string.Equals(pk, kv.Value, StringComparison.OrdinalIgnoreCase))
                        {
                            hitKey = pk;
                            break;
                        }
                    }
                    if (hitKey != null)
                    {
                        renamed[kv.Key] = payload[hitKey];   // MES名=本站值
                        if (!string.Equals(kv.Key, hitKey, StringComparison.OrdinalIgnoreCase))
                        {
                            renamed.Remove(hitKey);   // 原名删掉，真改名
                        }
                    }
                }
                payload = renamed;
            }
            // 静态字段合并（常量优先覆盖）
            Dictionary<string, string> statics;
            MesMapping.ParseStaticFields(staticRaw, out statics, out errors);
            foreach (var kv in statics)
            {
                payload[kv.Key] = kv.Value;
            }
            return JsonConvert.SerializeObject(payload);
        }

        /// <summary>用当前内存配置组包（实例版）。</summary>
        private string BuildPayloadJson(Dictionary<string, string> ourFields)
        {
            return BuildPayloadJson(ourFields, _config.MesFieldMap, _config.MesStaticFields);
        }

        /// <summary>
        /// 请求头（【V1.68】自定义头 + 鉴权头合并：自定义先铺底，鉴权后覆盖——
        /// Authorization 永远按 MesAuthType 生成，配错自定义头也顶不掉鉴权）。
        /// </summary>
        private Dictionary<string, string> BuildHeaders()
        {
            var h = new Dictionary<string, string>();
            try
            {
                // 自定义头（脏组跳过，保存时已拦）
                Dictionary<string, string> custom;
                List<string> errors;
                MesMapping.ParseCustomHeaders(_config.MesCustomHeaders, out custom, out errors);
                foreach (var kv in custom)
                {
                    h[kv.Key] = kv.Value;
                }
                // 鉴权头（后写，赢）
                string auth = (_config.MesAuthType ?? "None").Trim();
                if (string.Equals(auth, "Bearer", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(_config.MesAuthToken))
                {
                    h["Authorization"] = "Bearer " + _config.MesAuthToken;
                }
                else if (string.Equals(auth, "Basic", StringComparison.OrdinalIgnoreCase))
                {
                    string raw = (_config.MesAuthUser ?? "") + ":" + (_config.MesAuthPassword ?? "");
                    h["Authorization"] = "Basic "
                        + Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
                }
            }
            catch { /* 头组包失败按无头发送，不阻断上报 */ }
            return h;
        }

        private static string GetField(Dictionary<string, string> fields, string key)
        {
            if (fields == null) return "";
            string v;
            return fields.TryGetValue(key, out v) ? (v ?? "") : "";
        }

        /// <summary>启动后台发送线程（懒建：第一个事件才建，_mesEnabled=false 时零线程）。</summary>
        private void EnsureWorker()
        {
            if (_worker != null) return;
            _stop = false;
            _signal = new AutoResetEvent(false);
            _worker = new Thread(WorkerLoop) { IsBackground = true, Name = "MesReporter" };
            _worker.Start();
        }

        /// <summary>
        /// 后台发送循环：取队首 → 按"重试次数+间隔"发 → 成功则顺带冲离线缓存；
        /// 失败则进缓存文件。单线程串行，MES 侧收到的是时间序。
        /// </summary>
        private void WorkerLoop()
        {
            // 启动时先把上次剩的离线缓存读进来（上次崩溃/断网剩的，这次补发）
            List<MesItem> backlog = LoadBacklog();
            try
            {
                while (true)
                {
                    if (_signal != null) _signal.WaitOne(5000);
                    if (_stop) return;

                    // 队列满丢弃过：记一条事件说明（不每条刷屏，CSV 里留痕）。
                    int dropped = _queueDropped;
                    if (dropped > 0)
                    {
                        _queueDropped = 0;
                        try
                        {
                            TestEventLogger.Write("", 0, "MES队列满",
                                $"MES 不通期间内存队列已满，丢弃最旧 {dropped} 条（离线缓存上限 {MaxBacklog} 条）",
                                sn: "", recipe: "", result: "");
                        }
                        catch { }
                    }

                    // 1) 先冲离线缓存（旧的先发，保序）
                    if (backlog.Count > 0)
                    {
                        var remain = new List<MesItem>();
                        foreach (var item in backlog)
                        {
                            if (_stop) return;
                            if (!PostOnce(item)) remain.Add(item);
                        }
                        if (remain.Count != backlog.Count)
                        {
                            backlog = remain;
                            SaveBacklog(backlog);
                        }
                    }

                    // 2) 发本轮新事件（每条按配置重试）
                    while (true)
                    {
                        MesItem item = null;
                        lock (_lock)
                        {
                            if (_queue.Count > 0) item = _queue.Dequeue();
                        }
                        if (item == null) break;
                        if (_stop) return;
                        if (!PostWithRetry(item))
                        {
                            backlog.Add(item);
                            // 缓存上限：丢最旧（MES 久不通时保内存不爆）
                            while (backlog.Count > MaxBacklog) backlog.RemoveAt(0);
                            SaveBacklogThrottled(backlog);
                        }
                        // 成功了就继续下一条；本轮开头发过缓存，网络通了缓存自然就清了
                    }
                    // 循环末补落一次（节流跳过的脏缓存不丢）。
                    SaveBacklogThrottled(backlog, true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MES上报] 后台线程异常退出: {ex.Message}");
            }
        }

        /// <summary>
        /// 缓存落盘（节流版）：2s 内只落一次，报警风暴时不每条全量写文件；
        /// flush=true 或超 2s 才真写。调用方（Dispose/循环末）传 flush 补落。
        /// </summary>
        private void SaveBacklogThrottled(List<MesItem> backlog, bool flush = false)
        {
            if (!flush && (DateTime.Now - _lastBacklogSave).TotalSeconds < 2) return;
            _lastBacklogSave = DateTime.Now;
            SaveBacklog(backlog);
        }

        /// <summary>发一条（按配置重试；全灭返回 false）。重试 sleep 在后台线程，不卡业务。</summary>
        private bool PostWithRetry(MesItem item)
        {
            int retries = _config != null ? Math.Max(0, _config.MesRetryCount) : 0;
            int interval = _config != null ? Math.Max(0, _config.MesRetryIntervalMs) : 0;
            for (int attempt = 0; ; attempt++)
            {
                if (_stop) return false;   // 【大扫荡】退出不硬等：以前 Sleep 里收不到 _stop
                if (PostOnce(item)) return true;
                if (attempt >= retries) return false;
                // 【大扫荡】去掉 10s 静默钳制：配 60s 间隔实际按 10s 重试=重试风暴；
                // 分片 Sleep，退出即醒（Join 不空等）。
                SleepInterruptible(interval);
            }
        }

        /// <summary>可中断 Sleep（500ms 一片，_stop 置位即醒）。</summary>
        private void SleepInterruptible(int millis)
        {
            int waited = 0;
            while (waited < millis && !_stop)
            {
                int slice = Math.Min(500, millis - waited);
                if (slice <= 0) break;
                Thread.Sleep(slice);
                waited += slice;
            }
        }

        /// <summary>
        /// 发一次 HTTP POST（或 Fake transport）。2xx=成功；其余/异常=失败。
        /// HttpClient 进程单例复用（TIME_WAIT 不堆积），Timeout 按条设。
        /// </summary>
        private bool PostOnce(MesItem item)
        {
            try
            {
                if (Transport != null)
                {
                    return Transport(item.Url, item.Json, item.Headers, TimeoutMs());
                }
                var client = _httpClient.Value;
                client.Timeout = TimeSpan.FromMilliseconds(Math.Max(500, TimeoutMs()));
                using (var req = new HttpRequestMessage(HttpMethod.Post, item.Url))
                {
                    req.Content = new StringContent(item.Json ?? "", Encoding.UTF8, "application/json");
                    // 鉴权头挂请求头（Authorization 放内容头是非法的，挂错会 401）
                    foreach (var h in item.Headers)
                    {
                        req.Headers.TryAddWithoutValidation(h.Key, h.Value);
                    }
                    var resp = client.SendAsync(req).GetAwaiter().GetResult();
                    return resp != null && ((int)resp.StatusCode >= 200 && (int)resp.StatusCode < 300);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MES上报] POST失败: {ex.Message}");
                return false;
            }
        }

        private int TimeoutMs()
        {
            return _config != null ? Math.Max(500, _config.MesTimeoutMs) : 5000;
        }

        private static string QueuePath()
        {
            // 【大扫荡】绝对路径：以前裸相对路径跟 CWD 走，快捷方式起始位置不同
            // 缓存写散多处，补发永远找不到旧缓存（与 TestSessionStore 同病）。
            string dir = BaseDirOverride;
            if (string.IsNullOrEmpty(dir))
            {
                try { dir = AppDomain.CurrentDomain.BaseDirectory; }
                catch { dir = "."; }
            }
            return Path.Combine(dir, QueueFileName);
        }

        private static List<MesItem> LoadBacklog()
        {
            try
            {
                string path = QueuePath();
                if (!File.Exists(path)) return new List<MesItem>();
                var list = JsonConvert.DeserializeObject<List<MesItem>>(AtomicFile.SafeReadAllText(path));
                return list ?? new List<MesItem>();
            }
            catch
            {
                return new List<MesItem>();   // 损坏按空处理（上报缓存丢了只影响补发，不影响生产）
            }
        }

        private static void SaveBacklog(List<MesItem> backlog)
        {
            try
            {
                if (backlog == null || backlog.Count == 0)
                {
                    if (File.Exists(QueuePath())) File.Delete(QueuePath());
                    return;
                }
                File.WriteAllText(QueuePath(), JsonConvert.SerializeObject(backlog));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MES上报] 缓存落盘失败: {ex.Message}");
            }
        }

        private bool _alreadyDisposed;
        public void Dispose()
        {
            if (_alreadyDisposed) return;
            _alreadyDisposed = true;
            try
            {
                // 【大扫荡】内存余量转存离线缓存再走：以前 _queue 里没发的直接丢。
                try
                {
                    List<MesItem> left;
                    lock (_lock)
                    {
                        left = new List<MesItem>(_queue);
                        _queue.Clear();
                    }
                    if (left.Count > 0)
                    {
                        var all = LoadBacklog();
                        all.AddRange(left);
                        while (all.Count > MaxBacklog) all.RemoveAt(0);
                        SaveBacklog(all);
                    }
                }
                catch { /* 转存失败不拖退出 */ }
                _stop = true;
                if (_signal != null) _signal.Set();
                if (_worker != null) _worker.Join(2000);   // 最多等2秒，不拖退出
                if (_signal != null) _signal.Dispose();
            }
            catch { }
            _disposed = true;
        }
    }
}
