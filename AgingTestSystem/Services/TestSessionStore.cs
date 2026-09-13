
using System;
using System.IO;
using AgingTestSystem.Models;
using Newtonsoft.Json;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 在测任务快照持久化服务（V1.59 新增）
    ///
    /// 【功能说明】
    /// 把 <see cref="TestSession"/>（当前所有在测工位的任务参数快照）序列化为
    /// 程序运行目录下的 TestSession.json，供异常断电/崩溃后重启时恢复。
    ///
    /// 【生命周期】（由 DeviceManager 调用，UI 层只读判断）
    /// - 启动测试 / 上电进入老化 / 完成 / 停止 / 复位 后 → Save（有在测任务才有内容）；
    /// - 急停 StopAll / 放弃恢复 / 全部任务结束 → Clear（删除文件）；
    /// - 正常退出程序【不删除】：耦合器 DO 保持最后状态，物理上阀和电还开着、
    ///   测试还在继续，下次启动仍应询问恢复。
    ///
    /// 【容错约定】（与 RecipeStorage 一致）
    /// - 文件不存在 → Load 返回 null；
    /// - 文件损坏/格式错 → Load 返回 null（不抛异常），等同于无待恢复任务；
    /// - 写失败静默返回 false（日志系统不能拖垮业务主流程）。
    /// </summary>
    public static class TestSessionStore
    {
        /// <summary>目录覆盖测试缝（回归隔离用，生产恒 null；用例赋值后 try/finally 复位）。</summary>
        internal static string BaseDirOverride;

        /// <summary>
        /// 快照文件路径（程序运行目录下的 TestSession.json，绝对路径——
        /// 【大扫荡】以前是相对路径，跟 CWD 走：快捷方式起始位置不同快照就写散，
        /// 重启找不到=不问恢复，整批任务静默丢失）。
        /// 注意：本文件是程序运行生成的数据文件，已加入 .gitignore，不入库。
        /// </summary>
        private static string SessionFilePath
        {
            get
            {
                string dir = BaseDirOverride;
                if (string.IsNullOrEmpty(dir))
                {
                    try { dir = AppDomain.CurrentDomain.BaseDirectory; }
                    catch { dir = "."; }
                }
                return Path.Combine(dir, "TestSession.json");
            }
        }

        /// <summary>
        /// 保存快照（【大扫荡】原子写：临时文件+改名，无写半截截断 JSON。
        /// 调用方保证 stations 为"当前仍在测的任务"）
        /// </summary>
        /// <param name="session">要保存的快照</param>
        /// <returns>保存成功返回 true</returns>
        public static bool Save(TestSession session)
        {
            try
            {
                if (session == null)
                {
                    return false;
                }
                session.SavedAt = DateTime.Now;
                string json = JsonConvert.SerializeObject(session, Formatting.Indented);
                AtomicFile.WriteAllText(SessionFilePath, json);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[任务快照] 保存失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 加载快照
        /// </summary>
        /// <returns>快照对象；文件不存在 / 损坏 / 内容为空任务时返回 null</returns>
        public static TestSession Load()
        {
            try
            {
                if (!File.Exists(SessionFilePath))
                {
                    return null;
                }

                string json = AtomicFile.SafeReadAllText(SessionFilePath);
                TestSession session = JsonConvert.DeserializeObject<TestSession>(json);

                // 没有任何在测工位的空快照视为无任务（正常退出前最后一次 Save 可能已是空清单）
                if (session == null || session.Stations == null || session.Stations.Count == 0)
                {
                    return null;
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[任务快照] 加载成功，共 {session.Stations.Count} 台在测任务");
                return session;
            }
            catch (Exception ex)
            {
                // 损坏的快照不能阻塞启动：当作无待恢复任务处理
                System.Diagnostics.Debug.WriteLine($"[任务快照] 加载失败(按无任务处理): {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 删除快照文件（急停/放弃恢复/全部任务结束时调用）
        /// 文件不存在时静默成功。
        /// </summary>
        public static void Clear()
        {
            try
            {
                if (File.Exists(SessionFilePath))
                {
                    File.Delete(SessionFilePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[任务快照] 删除失败: {ex.Message}");
            }
        }
    }
}
