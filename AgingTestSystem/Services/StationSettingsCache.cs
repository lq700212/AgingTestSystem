using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 工位配置缓存条目
    ///
    /// 【用途】工位设置窗口"保存"按钮把当前工位的配置信息缓存下来，
    /// 下次点击该工位"设置"按钮打开窗口时自动回填上一次缓存的内容。
    /// 缓存内容与工位静态信息（StationInfo）一致，额外补充了极限温度
    /// （工位面板 / StationInfo 没有该字段，属于配方配置范畴）。
    /// </summary>
    public class StationCacheEntry
    {
        /// <summary>工位编号（1 ~ TotalBarometers）</summary>
        public int DeviceId { get; set; }

        /// <summary>产品序列号（可空）</summary>
        public string SerialNumber { get; set; }

        /// <summary>配方名称（可空）</summary>
        public string RecipeName { get; set; }

        /// <summary>延时时间（时:分:秒，配方窗口"延时时间"，工位面板"延时开启"）</summary>
        public TimeSpan DelayTime { get; set; }

        /// <summary>启动时间（时:分:秒，配方窗口"启动时间"，工位面板"延时到达"）</summary>
        public TimeSpan StartTime { get; set; }

        /// <summary>极限温度（单位：摄氏度）</summary>
        public decimal LimitTemperature { get; set; }
    }

    /// <summary>
    /// 工位配置缓存服务
    ///
    /// 【存储说明】
    /// - 按工位编号（DeviceId）索引，一个工位一条缓存；
    /// - 持久化到程序运行目录下的 StationSettings.json（与 Recipes.json / Users.json 同级），
    ///   重启程序后仍能自动回填；
    /// - 文件不存在 / 损坏时按空缓存处理（不抛异常，不阻塞界面）。
    ///
    /// 【线程安全】
    /// 所有读写通过 _lock 串行化；返回给调用方的条目都是副本，避免外部修改污染内部存储。
    /// </summary>
    public static class StationSettingsCache
    {
        /// <summary>缓存文件路径（程序运行目录下的 StationSettings.json）</summary>
        private const string CacheFilePath = "StationSettings.json";

        /// <summary>内存缓存（deviceId → 缓存条目），懒加载</summary>
        private static Dictionary<int, StationCacheEntry> _cache;

        /// <summary>缓存读写锁</summary>
        private static readonly object _lock = new object();

        /// <summary>
        /// 静态构造函数：首次访问时尝试从文件加载缓存
        /// </summary>
        static StationSettingsCache()
        {
            LoadFromFile();
        }

        /// <summary>
        /// 获取指定工位的缓存配置
        /// </summary>
        /// <param name="deviceId">工位编号（1 ~ TotalBarometers）</param>
        /// <returns>缓存条目副本；该工位无缓存返回 null</returns>
        public static StationCacheEntry Get(int deviceId)
        {
            lock (_lock)
            {
                EnsureLoaded();
                if (_cache.TryGetValue(deviceId, out StationCacheEntry entry))
                {
                    return Clone(entry);
                }
                return null;
            }
        }

        /// <summary>
        /// 保存指定工位的配置缓存（存在则覆盖）并落盘
        /// </summary>
        /// <param name="entry">要保存的缓存条目</param>
        public static void Save(StationCacheEntry entry)
        {
            if (entry == null) return;

            lock (_lock)
            {
                EnsureLoaded();
                _cache[entry.DeviceId] = Clone(entry);
                WriteToFile();
            }
        }

        /// <summary>
        /// 确保内存缓存已加载（防止 Get/Save 被调用时静态构造函数未执行的极端情况）
        /// </summary>
        private static void EnsureLoaded()
        {
            if (_cache == null)
            {
                LoadFromFile();
            }
        }

        /// <summary>
        /// 从 JSON 文件加载缓存到内存
        /// 文件不存在 / 解析失败时初始化为空字典（不抛异常）
        /// </summary>
        private static void LoadFromFile()
        {
            _cache = new Dictionary<int, StationCacheEntry>();
            try
            {
                if (!File.Exists(CacheFilePath)) return;

                string jsonContent = File.ReadAllText(CacheFilePath);
                List<StationCacheEntry> list =
                    JsonConvert.DeserializeObject<List<StationCacheEntry>>(jsonContent);

                if (list == null) return;

                foreach (var entry in list)
                {
                    if (entry != null && entry.DeviceId > 0)
                    {
                        _cache[entry.DeviceId] = entry;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[工位缓存] 加载成功，共 {_cache.Count} 条");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[工位缓存] 加载缓存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 把内存缓存序列化写入 JSON 文件
        /// </summary>
        private static void WriteToFile()
        {
            try
            {
                string jsonContent =
                    JsonConvert.SerializeObject(new List<StationCacheEntry>(_cache.Values), Formatting.Indented);
                File.WriteAllText(CacheFilePath, jsonContent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[工位缓存] 保存缓存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 深拷贝缓存条目（避免外部修改污染内部存储）
        /// </summary>
        /// <param name="entry">源条目</param>
        /// <returns>副本</returns>
        private static StationCacheEntry Clone(StationCacheEntry entry)
        {
            return new StationCacheEntry
            {
                DeviceId = entry.DeviceId,
                SerialNumber = entry.SerialNumber,
                RecipeName = entry.RecipeName,
                DelayTime = entry.DelayTime,
                StartTime = entry.StartTime,
                LimitTemperature = entry.LimitTemperature
            };
        }
    }
}
