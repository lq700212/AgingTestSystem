using System;
using System.Collections.Generic;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace AgingTestSystem.Services.License
{
    /// <summary>
    /// 机器指纹（【V1.83 新增】授权绑机的唯一依据）。
    ///
    /// 【取哪四样】主板序列号 + CPU 编号 + 系统盘序列号 + Windows MachineGuid。
    /// 四样都是"换整机必变、日常维护不变"的：
    /// - 主板/CPU/系统盘：硬件换了才变（加内存、换网卡、重装同版本系统都不变）；
    /// - MachineGuid：重装系统才变（与硬件三样互补：硬件不动只重装系统也能认出来）。
    /// WMI 在本项目本来就在用（扫码枪串口识别走 Win32_PnPEntity，见 ScannerService），
    /// 不新增依赖；取不到的项记 UNKNOWN（虚拟机/精简系统常见），不抛异常。
    ///
    /// 【漂移容忍】授权文件里同时存"总指纹 + 四个分量指纹"（见 LicenseManager 的
    /// 签发格式说明，此处只管算）。校验时总指纹对上直接过；对不上再数分量：
    /// ≥3 个分量命中算"换了一块硬件"（如换系统盘），警告放行；≤2 个算换整机，
    /// 直接拦截。现场换一块盘不找我们要新证，整机拷贝带不走。
    ///
    /// 【显示格式】64 位 hex 按 4-4 分组（XXXX-XXXX…），导出机器码文件给客户
    /// 拷回公司签发；复制按钮走剪贴板，与设置表的"长按复制设置名称"同体验。
    /// </summary>
    public static class MachineFingerprint
    {
        /// <summary>分量名（顺序固定：签名与比对都认这个序，增删即不兼容，必须同步升级 v）。</summary>
        public static readonly string[] ComponentNames =
            { "board", "cpu", "disk", "guid" };

        // 【大扫荡】进程内缓存：机器指纹运行中不变，以前每次启动打 8 遍 WMI
        //（Compute 4 连查 + GetComponentHashes 又 4 连查），WMI 卡时启动更卡。
        private static readonly Lazy<string[]> _rawCache =
            new Lazy<string[]>(ReadAllRaw, true);
        private static readonly Lazy<string[]> _hashCache =
            new Lazy<string[]>(ComputeAllHashes, true);

        private static string[] ReadAllRaw()
        {
            return new string[]
            {
                ReadBoardSerial(),
                ReadCpuId(),
                ReadDiskSerial(),
                ReadMachineGuid(),
            };
        }

        private static string[] ComputeAllHashes()
        {
            string[] raw = _rawCache.Value;
            var out_ = new string[raw.Length];
            for (int i = 0; i < raw.Length; i++) out_[i] = HashOf(ComponentNames[i] + "=" + Norm(raw[i]));
            return out_;
        }

        /// <summary>
        /// 取四个分量的原始值（小白调试用：哪一项 UNKNOWN 一眼看出来）。
        /// 顺序与 <see cref="ComponentNames"/> 对齐；全部 try/catch，永不抛异常。
        /// </summary>
        public static string[] GetRawComponents()
        {
            string[] raw = _rawCache.Value;
            var copy = new string[raw.Length];
            Array.Copy(raw, copy, raw.Length);
            return copy;
        }

        /// <summary>
        /// 算分量指纹（每个分量单独 SHA256 hex，用于授权文件里的漂移比对）。
        /// 与总指纹同算法（见 <see cref="HashOf"/>），分量归一化后各自哈希。
        /// </summary>
        public static string[] GetComponentHashes()
        {
            string[] h = _hashCache.Value;
            var copy = new string[h.Length];
            Array.Copy(h, copy, h.Length);
            return copy;
        }

        /// <summary>
        /// 算总指纹（64 位小写 hex）：SHA256("board=…|cpu=…|disk=…|guid=…")。
        /// 排序/分隔符写死在这里，签发端不用重复实现——签发端收的是这台机器
        /// 导出的现成指纹串，原样写进授权文件即可（见 LicenseForm 的导出）。
        /// </summary>
        public static string Compute()
        {
            string[] raw = _rawCache.Value;
            var sb = new StringBuilder(256);
            for (int i = 0; i < ComponentNames.Length; i++)
            {
                if (i > 0) sb.Append('|');
                sb.Append(ComponentNames[i]).Append('=').Append(Norm(raw[i]));
            }
            return HashOf(sb.ToString());
        }

        /// <summary>指纹展示格式（64 hex → XXXX-XXXX…分组，导给客户的文件与界面都用它）。</summary>
        public static string FormatGrouped(string fingerprint)
        {
            try
            {
                string s = (fingerprint ?? "").Trim().Replace("-", "").Replace(" ", "").ToUpperInvariant();
                if (s.Length != 64) return fingerprint ?? "";
                var sb = new StringBuilder(80);
                for (int i = 0; i < s.Length; i += 4)
                {
                    if (i > 0) sb.Append('-');
                    sb.Append(s.Substring(i, 4));
                }
                return sb.ToString();
            }
            catch { return fingerprint ?? ""; }
        }

        /// <summary>去分组还原（验签比对前先调它，用户手输带横杠的也能认）。</summary>
        public static string Ungroup(string s)
        {
            try { return (s ?? "").Trim().Replace("-", "").Replace(" ", "").ToLowerInvariant(); }
            catch { return ""; }
        }

        // ================= 分量读取（全是"读不到认栽"，绝不抛） =================

        private static string ReadBoardSerial()
        {
            try
            {
                using (var s = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard"))
                foreach (ManagementObject o in s.Get())
                {
                    string v = NormAny(o["SerialNumber"]);
                    if (!IsJunk(v)) return v;
                }
            }
            catch { }
            return "UNKNOWN";
        }

        private static string ReadCpuId()
        {
            try
            {
                using (var s = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
                foreach (ManagementObject o in s.Get())
                {
                    string v = NormAny(o["ProcessorId"]);
                    if (!IsJunk(v)) return v;
                }
            }
            catch { }
            return "UNKNOWN";
        }

        private static string ReadDiskSerial()
        {
            try
            {
                // 系统盘优先：先找 Index=0 的物理盘（工控机一般就一块盘），没有再取第一块。
                string first = null;
                using (var s = new ManagementObjectSearcher(
                    "SELECT Index, SerialNumber, MediaType FROM Win32_DiskDrive"))
                foreach (ManagementObject o in s.Get())
                {
                    string v = NormAny(o["SerialNumber"]);
                    if (IsJunk(v)) continue;
                    if (first == null) first = v;
                    object idx = o["Index"];
                    if (idx != null && idx.ToString() == "0") return v;
                }
                if (first != null) return first;
            }
            catch { }
            return "UNKNOWN";
        }

        private static string ReadMachineGuid()
        {
            try
            {
                using (RegistryKey k = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Cryptography", false))
                {
                    if (k != null)
                    {
                        string v = NormAny(k.GetValue("MachineGuid"));
                        if (!IsJunk(v)) return v;
                    }
                }
            }
            catch { }
            return "UNKNOWN";
        }

        // ================= 小工具 =================

        /// <summary>分量归一化（去空格转大写；全 0/全 F 的假序列号按 UNKNOWN 处理，防" Waters"）。</summary>
        private static string Norm(string v)
        {
            string s = NormAny(v);
            if (IsJunk(s)) return "UNKNOWN";
            return s.ToUpperInvariant();
        }

        private static string NormAny(object v)
        {
            try { return (v != null ? v.ToString() : "").Trim(); }
            catch { return ""; }
        }

        private static bool IsJunk(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return true;
            string t = s.Trim().ToUpperInvariant();
            if (t == "UNKNOWN" || t == "NONE" || t == "TO BE FILLED BY O.E.M."
                || t == "DEFAULT STRING" || t == "SYSTEM SERIAL NUMBER") return true;
            // 全 0/全 F/全 1 的占位序列号（组装机/虚拟机常见）一律按读不到处理，
            // 免得几千台机算出同一个"指纹"，绑机形同虚设。
            bool allSame = true;
            char c0 = t[0];
            if (c0 != '0' && c0 != 'F' && c0 != '1') return false;
            foreach (char c in t)
            {
                if (c != c0 && c != ' ' && c != '-') { allSame = false; break; }
            }
            return allSame;
        }

        internal static string HashOf(string s)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(s ?? ""));
                var sb = new StringBuilder(64);
                foreach (byte b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
