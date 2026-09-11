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

        /// <summary>离线缓存文件名（程序运行目录，相对路径——回归隔离靠 EnterCleanDir 切 cwd）</summary>
        public const string QueueFileName = "MesQueue.json";

        /// <summary>离线缓存上限（条）。MES 长期不通时丢最旧的（保内存），丢时记日志。</summary>
        private const int MaxBacklog = 5000;

        private readonly DeviceConfig _config;
        private readonly object _lock = new object();
        private readonly Queue<MesItem> _queue = new Queue<MesItem>();
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
                    TestEventLogger.Write(lot, device, "MES上报(Mock)", json);
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
                var renamed = new Dictionary<string, string>();
                foreach (var kv in payload)
                {
                    renamed[kv.Key] = kv.Value;   // 先保留原名
                }
                foreach (var kv in map)
                {
                    string v;
                    if (payload.TryGetValue(kv.Value, out v))
                    {
                        renamed[kv.Key] = v;      // MES名=本站值
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
                            SaveBacklog(backlog);
                        }
                        // 成功了就继续下一条；本轮开头发过缓存，网络通了缓存自然就清了
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MES上报] 后台线程异常退出: {ex.Message}");
            }
        }

        /// <summary>发一条（按配置重试；全灭返回 false）。重试 sleep 在后台线程，不卡业务。</summary>
        private bool PostWithRetry(MesItem item)
        {
            int retries = _config != null ? Math.Max(0, _config.MesRetryCount) : 0;
            int interval = _config != null ? Math.Max(0, _config.MesRetryIntervalMs) : 0;
            for (int attempt = 0; ; attempt++)
            {
                if (PostOnce(item)) return true;
                if (attempt >= retries) return false;
                if (interval > 0) Thread.Sleep(Math.Min(interval, 10000));
            }
        }

        /// <summary>
        /// 发一次 HTTP POST（或 Fake transport）。2xx=成功；其余/异常=失败。
        /// 超时只影响本条（HttpClient per-call，量小无所谓复用）。
        /// </summary>
        private bool PostOnce(MesItem item)
        {
            try
            {
                if (Transport != null)
                {
                    return Transport(item.Url, item.Json, item.Headers, TimeoutMs());
                }
                using (var client = new HttpClient())
                {
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
            return QueueFileName;   // 相对路径：产品运行时=cwd=程序目录；回归隔离靠 EnterCleanDir
        }

        private static List<MesItem> LoadBacklog()
        {
            try
            {
                string path = QueuePath();
                if (!File.Exists(path)) return new List<MesItem>();
                var list = JsonConvert.DeserializeObject<List<MesItem>>(File.ReadAllText(path));
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
