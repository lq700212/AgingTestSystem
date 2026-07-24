using System;
using System.Collections.Generic;

namespace BarometerWinform.Models
{
    /// <summary>
    /// 用户账号模型
    ///
    /// 【说明】
    /// 存储一个用户的完整信息：用户名、密码（明文，仅演示用）、角色。
    ///
    /// 【安全提示】
    /// 实际项目中密码不应以明文存储，应使用 SHA256/BCrypt 等哈希算法加密保存。
    /// 当前为简化演示版本，使用明文密码。
    /// </summary>
    public class UserAccount
    {
        /// <summary>用户名（登录时输入）</summary>
        public string Username { get; set; }

        /// <summary>密码（明文，仅演示用，实际项目应哈希存储）</summary>
        public string Password { get; set; }

        /// <summary>用户角色（决定可操作的功能范围）</summary>
        public UserRole Role { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码</param>
        /// <param name="role">角色</param>
        public UserAccount(string username, string password, UserRole role)
        {
            Username = username;
            Password = password;
            Role = role;
        }

        /// <summary>
        /// 无参构造函数（用于 JSON 序列化/反序列化）
        /// Newtonsoft.Json 在反序列化时需要无参构造函数
        /// </summary>
        public UserAccount()
        {
        }
    }

    /// <summary>
    /// 用户登录结果
    /// </summary>
    public class LoginResult
    {
        /// <summary>是否登录成功</summary>
        public bool Success { get; set; }

        /// <summary>登录成功时返回的用户账号，失败时为 null</summary>
        public UserAccount User { get; set; }

        /// <summary>失败原因（用于提示用户）</summary>
        public string ErrorMessage { get; set; }

        /// <summary>创建一个成功的结果</summary>
        public static LoginResult Ok(UserAccount user)
            => new LoginResult { Success = true, User = user, ErrorMessage = null };

        /// <summary>创建一个失败的结果</summary>
        public static LoginResult Fail(string message)
            => new LoginResult { Success = false, User = null, ErrorMessage = message };
    }
}
