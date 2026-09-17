
using System;
using System.Collections.Generic;
using System.IO;
using AgingTestSystem.Models;
using Newtonsoft.Json;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 项目策略存储（一期 L2 策略层的持久化）。
    /// 【为什么策略不进 App.config】
    /// App.config 是跟机器的（COM 口/IP 是这台工控机的接线）；策略是跟项目的
    /// （客户 A 要拦截、客户 B 要放行）。所以策略住 `Projects/&lt;项目&gt;/Policy.json`，
    /// 一个项目一套策略，切换项目即切换策略——这正是"不等客户确认"的本体。
    /// 【加载顺序】App.config（机器缺省，含策略 key 的默认值兜底）
    /// → Policy.json（项目覆盖，写了啥就听啥）→ DeviceConfig 内存。
    /// SettingsForm 保存时同理分流：策略 key 写 Policy.json，其余写 exe.config。
    /// 策略 key 名单只有一份（<see cref="PolicyKeys"/>），两边都认它，防分叉。
    /// </summary>
    public static class ProjectPolicyStore
    {
        /// <summary>
        /// 策略配置项 key 名单（【唯一】新增策略只改这里，两边都认它）。
        /// 必须与 DeviceConfig 里的同名属性一一对应（少一个就存了用不上，多一个就读不到）。
        /// MES 触发器/字段映射/静态字段也是"跟项目"的配置，进本名单
        /// （走 Policy.json + SettingsForm 分流；它们是字符串，没有下拉选项，
        /// 三处同步锁里按"字符串直通"对待，见测试）。
        /// </summary>
        public static readonly HashSet<string> PolicyKeys = new HashSet<string>
        {
            "ZeroDurationPolicy",
            "EmptySnPolicy",
            "FanDisconnectPolicy",
            "VacuumFailKind",
            "CompletionJudgePolicy",
            "PowerLossPolicy",
            "AgingPressureLossPolicy",
            "CompletionAction",
            "EventIdentityMode",
            "VentValveDoPoint",
            "FanTempShutdownEnabled",
            "MesTriggers",
            "MesFieldMap",
            "MesStaticFields",
            "CustomAlarmRules",
            "CompleteExpression",
            "SkipVacuum",
            "ReportColumns",
            "DisplayModes",
            "DisplayModeEnabled",
            // 真空建立确认超时（整数毫秒，0=关闭）：腔体/管路抽气时间因产品而异，
            // 跟项目走（机器缺省 15000 不动）；数字项无策略下拉，同步锁里按
            // VentValveDoPoint 同口径豁免（见回归"三处同步锁"注释）。
            "VacuumConfirmTimeoutMs",
            // 压力报警总开关（布尔，默认 true=现状报警开；false=阈值越限+建立超时全不报，
            // 负压阀保持常开；DI触点/通讯失联/自定义规则不受影响。预置 A 默认关闭，跟项目走）。
            "PressureAlarmEnabled",
            // 报警全关·最高级（布尔，默认 false=现状；true=阈值/超时/DI/失联/规则/超温联停全不报，
            // 优先级高于压力报警总开关。四个预置默认全关，只走手动或自定义开启，跟项目走）。
            "MuteAllAlarms",
        };

        /// <summary>当前项目的策略文件路径（Projects/&lt;项目&gt;/Policy.json，当前生效值）。</summary>
        public static string PolicyFilePath
        {
            get { return Path.Combine(ProjectProfile.ActiveProfileDir, ProjectProfile.PolicyFileName); }
        }

        /// <summary>当前项目的自定义槽文件路径（Projects/&lt;项目&gt;/CustomPolicy.json）。</summary>
        public static string CustomFilePath
        {
            get { return Path.Combine(ProjectProfile.ActiveProfileDir, ProjectProfile.CustomPolicyFileName); }
        }

        /// <summary>
        /// 内存配置→策略字典（全部 PolicyKeys，存储口径字符串：布尔小写/枚举英文名/数字原文）。
        /// 自定义槽快照/导出装包/导入比对全走它，一处口径不分叉。
        /// 非 PolicyKeys 的机器参数（串口/IP/采集间隔等）不在里面——导入导出只装跟项目的策略。
        /// </summary>
        public static Dictionary<string, string> ToPolicyDict(DeviceConfig config)
        {
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            if (config == null) return dict;
            foreach (string key in PolicyKeys)
            {
                try
                {
                    var prop = typeof(DeviceConfig).GetProperty(key);
                    if (prop == null || !prop.CanRead) continue;
                    object v = prop.GetValue(config, null);
                    if (v == null) dict[key] = "";
                    else if (v is bool) dict[key] = ((bool)v) ? "true" : "false";
                    else dict[key] = v.ToString();
                }
                catch { /* 单项读失败跳过，不拦整包 */ }
            }
            return dict;
        }

        /// <summary>
        /// 只留 PolicyKeys 的项（导入/导出/自定义槽三处共用：脏 key 直接丢，
        /// 与 ApplyOverlay“脏 key 忽略”同口径，手改文件写错也不炸）。
        /// </summary>
        public static Dictionary<string, string> FilterToPolicyKeys(Dictionary<string, string> raw)
        {
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            if (raw == null) return dict;
            foreach (var kv in raw)
            {
                if (kv.Key != null && PolicyKeys.Contains(kv.Key)) dict[kv.Key] = kv.Value;
            }
            return dict;
        }

        /// <summary>
        /// 读自定义槽（跟项目走；文件不存在/损坏返回空字典，调用方按“未初始化”处理）。
        /// 返回的只有 PolicyKeys 项（已过滤）。
        /// </summary>
        public static Dictionary<string, string> LoadCustom()
        {
            try
            {
                string path = CustomFilePath;
                if (!File.Exists(path)) return new Dictionary<string, string>(StringComparer.Ordinal);
                string json = AtomicFile.SafeReadAllText(path);
                var raw = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                return FilterToPolicyKeys(raw);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[项目策略] 自定义槽加载失败回空: {ex.Message}");
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }
        }

        /// <summary>
        /// 写自定义槽（原子写；只收 PolicyKeys，脏 key 静默丢）。
        /// 与 Save（写 Policy.json）是两个文件：当前生效值与自定义槽各存各的。
        /// </summary>
        public static void SaveCustom(Dictionary<string, string> values)
        {
            var dict = FilterToPolicyKeys(values);
            AtomicFile.WriteAllText(CustomFilePath, JsonConvert.SerializeObject(dict, Formatting.Indented));
        }

        /// <summary>
        /// 确保自定义槽已初始化（首次打开即调一次）。
        /// 空槽按“A 预置快照＋种子配置的自由文本”建初始槽：
        /// A 管辖的开关＋数值照抄 A，MES 映射/规则/报表等自由文本取种子现状（种子 null 则取缺省），
        /// 之后用户改自定义只动槽文件，A/B/C/D 是代码写死的动不了。
        /// 已有槽只补“缺的”A 管辖项（缺 key 即按 A 补，补完落盘；已有的项一律不动，
        /// 那是用户亲手改的自定义）。为什么补：预置加新管辖项后，旧槽缺该项，
        /// 套用后生效值会回退机器缺省（如压力报警缺省开），用户看着“和A一样”
        /// 实际行为是开的；缺省必须真正等于 A（项目未上线，不兼容缺 key 的旧槽）。
        /// </summary>
        /// <param name="seed">种子配置（一般是内存现值，自由文本从它来；null=缺省）</param>
        /// <returns>自定义槽内容副本（初始化后）</returns>
        public static Dictionary<string, string> EnsureCustomInitialized(DeviceConfig seed)
        {
            Dictionary<string, string> exist = LoadCustom();
            if (exist.Count == 0)
            {
                var baseDict = ToPolicyDict(seed ?? new DeviceConfig());
                Dictionary<string, string> presetA = PolicyPresets.GetAllValues("A");
                if (presetA != null)
                {
                    foreach (var kv in presetA) baseDict[kv.Key] = kv.Value;
                }
                SaveCustom(baseDict);
                return new Dictionary<string, string>(baseDict, StringComparer.Ordinal);
            }
            Dictionary<string, string> aVals = PolicyPresets.GetAllValues("A");
            bool filled = false;
            if (aVals != null)
            {
                foreach (var kv in aVals)
                {
                    if (!exist.ContainsKey(kv.Key)) { exist[kv.Key] = kv.Value; filled = true; }
                }
            }
            if (filled) SaveCustom(exist);
            return exist;
        }

        /// <summary>
        /// 读导入文件（纯解析＋过滤，不校验合法性：校验走 SettingsForm.ValidateValue，
        /// 组合矛盾走 PersistChanges，与保存同一条路，脏文件进不来）。
        /// </summary>
        /// <returns>true=解析出可用项，false=error 有原因</returns>
        public static bool ReadImportFile(string path, out Dictionary<string, string> values, out string error)
        {
            values = new Dictionary<string, string>(StringComparer.Ordinal);
            error = null;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                error = "导入文件不存在，请重新选择。";
                return false;
            }
            Dictionary<string, string> raw;
            try
            {
                raw = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                    AtomicFile.SafeReadAllText(path));
            }
            catch (Exception ex)
            {
                error = "导入文件解析失败（应为本软件导出的策略文件）：" + ex.Message;
                return false;
            }
            if (raw == null || raw.Count == 0)
            {
                error = "导入文件内容为空，没有可用的策略项。";
                return false;
            }
            values = FilterToPolicyKeys(raw);
            if (values.Count == 0)
            {
                error = "文件里没有可识别的策略项（只认跟项目的策略 key）。";
                return false;
            }
            return true;
        }

        /// <summary>写导出文件（只装 PolicyKeys，原子写；失败抛异常，调用方弹框）。</summary>
        public static void WriteExportFile(string path, Dictionary<string, string> values)
        {
            var dict = FilterToPolicyKeys(values);
            AtomicFile.WriteAllText(path, JsonConvert.SerializeObject(dict, Formatting.Indented));
        }

        /// <summary>
        /// 加载当前项目的策略字典（key → 原始字符串）。文件不存在/损坏返回空字典
        /// （= 全部回退 App.config/DeviceConfig 缺省，即现状行为，安全）。
        /// 【大扫荡】路径键缓存：录入窗/校验高频调 GetRaw/Resolve，以前每次读文件+
        /// 反序列化；路径变（切项目）/Save 后自动失效，测试换目录也安全。
        /// 【复查补齐】缓存按"路径+长度+写时间"三元验证：以前只比路径，手改 Policy.json
        /// 后重进设置窗读到的还是旧值（改前每次 Load 都读文件，手改即生效）。
        /// </summary>
        private static readonly object _cacheLock = new object();
        private static string _cachedPath;
        private static long _cachedLen = -1;
        private static long _cachedWriteTicks = -1;
        private static Dictionary<string, string> _cachedDict;

        public static Dictionary<string, string> Load()
        {
            try
            {
                string path = PolicyFilePath;
                lock (_cacheLock)
                {
                    if (_cachedDict != null && string.Equals(_cachedPath, path,
                        StringComparison.OrdinalIgnoreCase)
                        && CacheStillFresh(path))
                    {
                        return new Dictionary<string, string>(_cachedDict);
                    }
                }
                Dictionary<string, string> fresh = LoadFromFile(path);
                lock (_cacheLock)
                {
                    StampCache(path);
                    _cachedDict = fresh;
                    return new Dictionary<string, string>(fresh);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[项目策略] 加载失败回退缺省: {ex.Message}");
                return new Dictionary<string, string>();
            }
        }

        /// <summary>缓存指纹是否仍有效（文件不存在=空字典，长度-1/时间-1 即指纹）。</summary>
        private static bool CacheStillFresh(string path)
        {
            try
            {
                if (!File.Exists(path)) return _cachedLen < 0;
                var fi = new FileInfo(path);
                return fi.Length == _cachedLen
                    && fi.LastWriteTimeUtc.Ticks == _cachedWriteTicks;
            }
            catch { return false; }
        }

        /// <summary>记录当前文件的缓存指纹（调用方已持 _cacheLock）。</summary>
        private static void StampCache(string path)
        {
            _cachedPath = path;
            try
            {
                if (!File.Exists(path)) { _cachedLen = -1; _cachedWriteTicks = -1; return; }
                var fi = new FileInfo(path);
                _cachedLen = fi.Length;
                _cachedWriteTicks = fi.LastWriteTimeUtc.Ticks;
            }
            catch { _cachedLen = -2; _cachedWriteTicks = -2; }
        }

        private static Dictionary<string, string> LoadFromFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return new Dictionary<string, string>();
                string json = AtomicFile.SafeReadAllText(path);
                var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                return dict ?? new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[项目策略] 加载失败回退缺省: {ex.Message}");
                return new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// 取单项策略原始值（SettingsForm 显示用：优先项目文件，没有则回 null 让调用方走 App.config）。
        /// </summary>
        public static string GetRaw(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            var dict = Load();
            string v;
            if (dict.TryGetValue(key, out v)) return v;
            return null;
        }

        /// <summary>
        /// 保存策略修改（SettingsForm 保存按钮调用：只含策略 key 的子集）。
        /// 与现有文件合并（不碰本次没改的项），写失败抛异常（调用方弹框）。
        /// 【大扫荡】原子写，无写半截截断。
        /// </summary>
        public static void Save(Dictionary<string, string> changes)
        {
            var dict = Load();
            foreach (var kv in changes)
            {
                if (PolicyKeys.Contains(kv.Key))
                {
                    dict[kv.Key] = kv.Value;
                }
            }
            AtomicFile.WriteAllText(PolicyFilePath, JsonConvert.SerializeObject(dict, Formatting.Indented));
            // 写完即刷新缓存（含新指纹，下次 Load 命中新值，不读脏）。
            lock (_cacheLock)
            {
                StampCache(PolicyFilePath);
                _cachedDict = dict;
            }
        }

        /// <summary>
        /// 取某策略 key 的项目层缺省（A 管辖的开关＋数值回 A 值；不管的自由文本回 null，
        /// 调用方继续走机器缺省）。ApplyOverlay（生效内存）与 SettingsForm.GetEffectiveValue
        /// （设置表显示）同调本函数：缺省只有一份口径，显示与生效永远一致。
        /// </summary>
        public static string ResolvePolicyDefault(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            Dictionary<string, string> presetA = PolicyPresets.GetAllValues("A");
            if (presetA == null) return null;
            string v;
            if (presetA.TryGetValue(key, out v)) return v;
            return null;
        }

        /// <summary>
        /// 把项目策略叠加到内存配置（MainForm.LoadConfig 读完 App.config 后调用）。
        /// 解析失败的项保持 App.config/DeviceConfig 缺省（= 现状行为），不抛异常。
        /// 缺省分两层：A 管辖的开关＋数值缺 key 时按 A 回填（跟项目的策略缺省 = A，
        /// 如压力报警缺省关；存过的值一律优先，显式配置永远赢；A 缺省口径见
        /// ResolvePolicyDefault，设置表显示同调，改一边必须对另一边）；不管的自由文本
        /// 缺 key 保持机器缺省。为什么：预置加新管辖项后，旧 Policy.json 缺该项，
        /// 生效值回退机器缺省（如报警缺省开），用户看着"自定义和A一样"实际行为是开的；
        /// 缺省必须真正等于 A（项目未上线，不兼容缺 key 的旧文件）。
        /// </summary>
        public static void ApplyOverlay(DeviceConfig config)
        {
            if (config == null) return;
            var dict = Load();
            Dictionary<string, string> presetA = PolicyPresets.GetAllValues("A");
            if (presetA != null)
            {
                foreach (var kv in presetA)
                {
                    if (!dict.ContainsKey(kv.Key)) dict[kv.Key] = kv.Value;
                }
            }
            foreach (var kv in dict)
            {
                if (!PolicyKeys.Contains(kv.Key)) continue;   // 脏 key 直接忽略（手改文件写错也不炸）
                var prop = typeof(DeviceConfig).GetProperty(kv.Key);
                if (prop == null || !prop.CanWrite) continue;
                object converted = ParseValue(prop.PropertyType, kv.Value);
                if (converted == null) continue;              // 非法值：保持缺省（现状行为）
                try { prop.SetValue(config, converted); }
                catch { /* 单项失败不影响其它项 */ }
            }
        }

        /// <summary>
        /// 按属性类型解析策略字符串（枚举/布尔/整数/字符串通用；非法返回 null）。
        /// SettingsForm 的热回写（ConvertConfigValue）也调这里，两边口径一致。
        /// 枚举大小写不敏感（"warn"/"Warn" 都认），手改文件少个大小写不翻车。
        /// 字符串原样返回（MES 映射类配置是自由文本，校验在 SettingsForm
        /// ValidateValue 做，不在这里拦——解析层只管"转得过去"，不管"合不合法"）。
        /// </summary>
        public static object ParseValue(Type propType, string value)
        {
            if (propType == null || value == null) return null;
            try
            {
                if (propType == typeof(string)) return value;
                if (propType.IsEnum)
                {
                    // 不用 Enum.TryParse(Type,...)（老框架重载不全），Parse+IsDefined 同效果
                    try
                    {
                        object e = Enum.Parse(propType, value.Trim(), true);
                        if (Enum.IsDefined(propType, e)) return e;
                    }
                    catch { /* 非法值回 null，调用方保持缺省 */ }
                    return null;
                }
                if (propType == typeof(bool))
                {
                    bool b;
                    return bool.TryParse(value.Trim(), out b) ? b : (object)null;
                }
                if (propType == typeof(int))
                {
                    int i;
                    return int.TryParse(value.Trim(), out i) ? i : (object)null;
                }
            }
            catch { return null; }
            return null;
        }

        /// <summary>
        /// 策略下拉选项（SettingsForm 中文显示 ↔ 存储英文名；显示文本禁改存储值，
        /// 改中文文案不影响已存配置——这就是显示/存储分离的好处）。
        /// 返回：key → (显示文本, 存储值) 数组。VentValveDoPoint 是数字，无下拉。
        /// </summary>
        public static Dictionary<string, Tuple<string, string>[]> EnumOptions
        {
            get
            {
                return new Dictionary<string, Tuple<string, string>[]>
                {
                    { "ZeroDurationPolicy", new Tuple<string, string>[]
                        {
                            Tuple.Create("只警告（现状）：提示后可继续启动", "Warn"),
                            Tuple.Create("硬拦截：含0时长工位直接阻断启动", "Block"),
                        } },
                    { "EmptySnPolicy", new Tuple<string, string>[]
                        {
                            Tuple.Create("只警告（现状）：提示后可继续启动", "Warn"),
                            Tuple.Create("硬拦截：含空SN工位直接阻断启动", "Block"),
                        } },
                    { "FanDisconnectPolicy", new Tuple<string, string>[]
                        {
                            Tuple.Create("只提示（现状）：警告后照跑", "LogOnly"),
                            Tuple.Create("阻断启动：风机未连接不让点火", "BlockStart"),
                        } },
                    { "VacuumFailKind", new Tuple<string, string>[]
                        {
                            Tuple.Create("产品责任（现状）：记FAIL", "ProductFail"),
                            Tuple.Create("治具责任：记装夹异常，可重测", "FixtureAlarm"),
                        } },
                    { "CompletionJudgePolicy", new Tuple<string, string>[]
                        {
                            Tuple.Create("自动PASS（现状）：到时无报警即PASS", "AutoPass"),
                            Tuple.Create("待判定：下料时人工录PASS/FAIL", "PendingReview"),
                        } },
                    { "PowerLossPolicy", new Tuple<string, string>[]
                        {
                            Tuple.Create("整台重测（现状）：满时长重跑", "RestartFull"),
                            Tuple.Create("续跑剩余时长：重抽真空+补足剩余", "ResumeRemaining"),
                        } },
                    { "AgingPressureLossPolicy", new Tuple<string, string>[]
                        {
                            Tuple.Create("停机报警（现状）：关阀断电", "StopOnLoss"),
                            Tuple.Create("只记不停：记事件继续老化", "KeepRunning"),
                        } },
                    { "CompletionAction", new Tuple<string, string>[]
                        {
                            Tuple.Create("只下电关阀（现状）", "PowerOffOnly"),
                            Tuple.Create("下电关阀+蜂鸣提醒", "PowerOffAndBeep"),
                            Tuple.Create("下电+开破空阀泄压（需配点位）", "PowerOffAndVent"),
                            Tuple.Create("蜂鸣+泄压都要", "PowerOffVentAndBeep"),
                        } },
                    { "EventIdentityMode", new Tuple<string, string>[]
                        {
                            Tuple.Create("记录现值（现状）：事件瞬间绑定的SN/配方", "RecordTime"),
                            Tuple.Create("启动定格：该轮启动时的SN/配方，中途重绑不污染", "StartSnapshot"),
                        } },
                };
            }
        }
    }
}
