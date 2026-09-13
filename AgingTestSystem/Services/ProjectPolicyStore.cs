
using System;
using System.Collections.Generic;
using System.IO;
using AgingTestSystem.Models;
using Newtonsoft.Json;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 项目策略存储（【V1.67 新增】一期 L2 策略层的持久化）。
    ///
    /// 【为什么策略不进 App.config】
    /// App.config 是跟机器的（COM 口/IP 是这台工控机的接线）；策略是跟项目的
    /// （客户 A 要拦截、客户 B 要放行）。所以策略住 `Projects/&lt;项目&gt;/Policy.json`，
    /// 一个项目一套策略，切换项目即切换策略——这正是"不等客户确认"的本体。
    ///
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
        /// 【V1.68】MES 触发器/字段映射/静态字段也是"跟项目"的配置，进本名单
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
        };

        /// <summary>当前项目的策略文件路径（Projects/&lt;项目&gt;/Policy.json）。</summary>
        public static string PolicyFilePath
        {
            get { return Path.Combine(ProjectProfile.ActiveProfileDir, ProjectProfile.PolicyFileName); }
        }

        /// <summary>
        /// 加载当前项目的策略字典（key → 原始字符串）。文件不存在/损坏返回空字典
        /// （= 全部回退 App.config/DeviceConfig 缺省，即现状行为，安全）。
        /// 【大扫荡】路径键缓存：录入窗/校验高频调 GetRaw/Resolve，以前每次读文件+
        /// 反序列化；路径变（切项目）/Save 后自动失效，测试换目录也安全。
        /// </summary>
        private static readonly object _cacheLock = new object();
        private static string _cachedPath;
        private static Dictionary<string, string> _cachedDict;

        public static Dictionary<string, string> Load()
        {
            try
            {
                string path = PolicyFilePath;
                lock (_cacheLock)
                {
                    if (_cachedDict != null && string.Equals(_cachedPath, path,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        return new Dictionary<string, string>(_cachedDict);
                    }
                }
                Dictionary<string, string> fresh = LoadFromFile(path);
                lock (_cacheLock)
                {
                    _cachedPath = path;
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
            // 写完即刷新缓存（下次 Load 命中新值，不读脏）。
            lock (_cacheLock)
            {
                _cachedPath = PolicyFilePath;
                _cachedDict = dict;
            }
        }

        /// <summary>
        /// 把项目策略叠加到内存配置（MainForm.LoadConfig 读完 App.config 后调用）。
        /// 解析失败的项保持 App.config/DeviceConfig 缺省（= 现状行为），不抛异常。
        /// </summary>
        public static void ApplyOverlay(DeviceConfig config)
        {
            if (config == null) return;
            var dict = Load();
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
        /// 【V1.68】字符串原样返回（MES 映射类配置是自由文本，校验在 SettingsForm
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
