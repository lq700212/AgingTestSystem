using System;
using System.Security.Cryptography;
using System.Text;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// MES 密钥 DPAPI 加解密（【V1.68 新增】token/密码不再明文落盘）。
    ///
    /// 【为什么用 DPAPI】Windows 自带、机器级密钥管理，不用我们自己存密钥文件
    /// （自己存密钥=掩耳盗铃）。Scope 用 LocalMachine：产线工控机 single 账号自启动，
    /// 但管理员可能换 Windows 账号登录维护——CurrentUser 会导致"换个账号登录就解密失败"，
    /// LocalMachine 则本机任何账号都能解（威胁模型：工控机物理隔离，能进本机的人
    /// 本来就能读内存，此处防的是"配置文件被拷贝带走"，LocalMachine 够了）。
    ///
    /// 【存储格式】"DPAPI:" + Base64(加密字节)。无此前缀的一律拒绝（返回 null），
    /// 不做明文兼容——项目未上线（PasswordHasher 同先例：非哈希一律判失败），
    /// 配置里出现明文就是手写错了，必须重填，不能悄悄吞下。
    ///
    /// 【失败语义】解密失败/格式不对返回 null（调用方按空处理 + 记日志），绝不抛异常——
    /// 密钥坏了不能拖垮启动，最多是 MES 401，事件进离线缓存等修好重发。
    /// </summary>
    public static class MesCrypto
    {
        /// <summary>加密值前缀（有此前缀=密文，无=非法）</summary>
        public const string Prefix = "DPAPI:";

        // 本程序专用 entropy（固定盐，防跨程序顺手解密；丢了也能从 git 找回，不影响解密——
        // 真正的密钥在 Windows DPAPI 机器密钥里，不在这里）
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("AgingTestSystem.Mes.v1");

        /// <summary>是否已加密（前缀判断）。</summary>
        public static bool IsProtected(string value)
        {
            return !string.IsNullOrEmpty(value) && value.StartsWith(Prefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// 加密明文（空返回空；已加密的原样返回，防重复加密）。
        /// 失败返回 null（调用方 fallback 明文保存 + 记日志，不阻断保存）。
        /// </summary>
        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return plain;
            if (IsProtected(plain)) return plain;
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(plain);
                byte[] enc = ProtectedData.Protect(data, Entropy, DataProtectionScope.LocalMachine);
                return Prefix + Convert.ToBase64String(enc);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MES密钥] 加密失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 解密（只有 DPAPI: 前缀的密文能解；其它一律返回 null——不明文兼容，
        /// 明文就是配错了，调用方按空处理，逼着重填）。
        /// </summary>
        public static string Unprotect(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (!IsProtected(value)) return null;   // 无前缀=非法，不吞明文
            try
            {
                byte[] enc = Convert.FromBase64String(value.Substring(Prefix.Length));
                byte[] data = ProtectedData.Unprotect(enc, Entropy, DataProtectionScope.LocalMachine);
                return Encoding.UTF8.GetString(data);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MES密钥] 解密失败: {ex.Message}");
                return null;
            }
        }
    }
}
