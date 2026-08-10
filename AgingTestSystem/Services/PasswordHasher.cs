using System;
using System.Security.Cryptography;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 密码哈希工具（PBKDF2-HMAC-SHA256）
    ///
    /// 【作用】
    /// 把用户密码转成不可逆的哈希字符串后再写入 Users.json，
    /// 彻底消除"明文密码落盘"的安全隐患（V1.58.22 起生效）。
    ///
    /// 【为什么用 PBKDF2 而非 MD5 / SHA1 / SHA256 裸哈希】
    /// 裸哈希（MD5/SHA1/SHA256）虽然不可逆，但对常见弱密码（如 123456）可用
    /// 彩虹表/暴力碰撞秒破；而 PBKDF2 通过"随机盐 + 上十万次迭代"让每次计算都
    /// 变慢（约几十毫秒），暴力破解的成本被拉高到不可接受。同一明文在不同盐下
    /// 哈希结果不同，也避免了"两个账号密码相同→哈希相同"的泄密推断。
    /// .NET Framework 自带 Rfc2898DeriveBytes 实现 PBKDF2，无需第三方库。
    ///
    /// 【存储格式】
    /// 哈希字符串格式为：PBKDF2$迭代次数$盐(Base64)$哈希(Base64)
    /// 例如：PBKDF2$100000$xxxxx==$yyyyy==
    /// - 盐和迭代次数随哈希一起保存，验证时直接读取，无需额外配置。
    /// - 迭代次数作为明文部分，未来想提升安全强度（加迭代）时旧密码仍可验证。
    ///
    /// 【兼容性约定】
    /// 项目尚未上线，无需兼容旧版明文格式：存储串必须以 "PBKDF2$" 前缀开头，
    /// 否则 Verify 一律判失败（Users.json 一律由本哈希生成，不存在明文）。
    /// </summary>
    public static class PasswordHasher
    {
        /// <summary>
        /// 哈希字符串前缀标记（用于识别字符串是否为哈希格式，区分旧版明文）
        /// </summary>
        private const string Prefix = "PBKDF2$";

        /// <summary>
        /// 盐长度（16 字节 = 128 位随机数，足以保证每个账号盐唯一）
        /// </summary>
        private const int SaltSize = 16;

        /// <summary>
        /// 哈希输出长度（32 字节 = 256 位，匹配 SHA256）
        /// </summary>
        private const int HashSize = 32;

        /// <summary>
        /// PBKDF2 迭代次数。
        /// 数值越大暴力破解越慢、越安全，但登录/改密时等待也越长（本值约 30~60ms，可接受）。
        /// 现场若觉得登录卡顿，可适度下调（如 50000）；追求更强安全可上调（如 200000）。
        /// 注意：改此值只影响"新生成的哈希"，旧哈希按各自记录的迭代次数验证，不受影响。
        /// </summary>
        private const int Iterations = 100000;

        /// <summary>
        /// 生成密码哈希
        /// </summary>
        /// <param name="password">明文密码</param>
        /// <returns>PBKDF2$迭代次数$盐$哈希 格式的字符串，存入 Users.json</returns>
        public static string Hash(string password)
        {
            if (password == null)
            {
                throw new ArgumentNullException(nameof(password));
            }

            // 每次生成都使用新的随机盐，保证同一密码多次生成的哈希互不相同
            byte[] salt = new byte[SaltSize];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt);
            }

            // 用盐 + 迭代次数导出定长哈希
            byte[] hash = ComputeHash(password, salt, Iterations);

            // 把参数和结果拼成一个自描述的字符串（验证时无需额外存储迭代次数/盐）
            return Prefix + Iterations + "$"
                 + Convert.ToBase64String(salt) + "$"
                 + Convert.ToBase64String(hash);
        }

        /// <summary>
        /// 验证密码是否匹配存储的哈希
        /// </summary>
        /// <param name="password">用户输入的明文密码</param>
        /// <param name="stored">Users.json 中存储的 PBKDF2 哈希字符串</param>
        /// <returns>匹配返回 true，不匹配或格式非法返回 false</returns>
        public static bool Verify(string password, string stored)
        {
            if (password == null || string.IsNullOrEmpty(stored))
            {
                return false;
            }

            // 存储串必须是哈希格式（未上线、无需兼容旧版明文，非哈希一律判失败）
            if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                // 拆分 PBKDF2$迭代次数$盐$哈希 各段
                string[] parts = stored.Substring(Prefix.Length).Split('$');
                if (parts.Length != 3)
                {
                    return false;
                }

                int iterations = int.Parse(parts[0]);
                byte[] salt = Convert.FromBase64String(parts[1]);
                byte[] expected = Convert.FromBase64String(parts[2]);

                // 用同样的盐和迭代次数重新计算，再恒定时间比较
                byte[] actual = ComputeHash(password, salt, iterations);
                return FixedTimeEquals(actual, expected);
            }
            catch (Exception ex)
            {
                // 存储串损坏/位数非法时静默失败，避免登录崩溃（也无法登录成功）
                System.Diagnostics.Debug.WriteLine("[密码哈希] 验证异常: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// PBKDF2 派生核心计算（密码 + 盐 + 迭代次数 → 定长哈希字节）
        /// </summary>
        private static byte[] ComputeHash(string password, byte[] salt, int iterations)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(HashSize);
            }
        }

        /// <summary>
        /// 恒定时间比较两个字节数组（长度不同直接返回 false）
        ///
        /// 【为什么不用简单 == 逐位比较】
        /// 普通比较在第一位不同时就提前返回，攻击者可利用"返回耗时差异"推断
        /// 哈希前缀，辅助暴力破解。恒定时间比较始终走完全部字节，耗时与内容无关。
        /// </summary>
        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }
    }
}
