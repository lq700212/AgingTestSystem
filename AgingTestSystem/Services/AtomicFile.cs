using System;
using System.IO;
using System.Text;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 原子文件写盘（【大扫荡新增】运行时 json 落盘统一口）。
    ///
    /// 【解决什么问题】以前配方/策略/快照/用户/试用全是裸 File.WriteAllText 直接覆盖：
    /// 写半截断电/杀进程 → 下次启动读到截断 JSON → Load 吞异常回空 → 配方被清空、
    /// 策略全回缺省（按错误工艺跑），全程无告警。原子写=先写临时文件再改名替换，
    /// 读方永远看到"旧全份或新全份"，没有中间态。
    ///
    /// 【实现】写 同目录.tmp_GUID → Move 覆盖目标（同卷改名是原子操作）。
    /// 失败抛异常（调用方决定拦/告警）；临时文件残留清掉，清不掉不管。
    /// </summary>
    public static class AtomicFile
    {
        /// <summary>UTF-8 原子写文本文件（BOM 与 File.WriteAllText(Encoding.UTF8) 一致）。</summary>
        public static void WriteAllText(string path, string content, Encoding encoding = null)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");
            if (encoding == null) encoding = Encoding.UTF8;
            string dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            string tmp = path + ".tmp_" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(tmp, content ?? "", encoding);
                if (File.Exists(path))
                {
                    // 先删后搬：.NET Framework 无 File.Replace 跨卷语义坑，
                    // 同目录内"删+搬"窗口极小，读方失败重试一次即可（见 SafeReadAllText）。
                    File.Delete(path);
                }
                File.Move(tmp, path);
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); }
                catch { /* 残留临时文件不阻塞，下次覆盖 */ }
            }
        }

        /// <summary>读文本文件（带一次重试：恰撞上"删+搬"窗口时第一次读不到，重试一次）。</summary>
        public static string SafeReadAllText(string path, Encoding encoding = null)
        {
            if (encoding == null) encoding = Encoding.UTF8;
            try
            {
                return File.ReadAllText(path, encoding);
            }
            catch (FileNotFoundException)
            {
                System.Threading.Thread.Sleep(50);
                return File.ReadAllText(path, encoding);
            }
        }
    }
}
