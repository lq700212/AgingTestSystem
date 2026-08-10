using System;
using System.Collections.Generic;

namespace AgingTestSystem.Models
{
    /// <summary>
    /// 用户账号模型
    ///
    /// 【说明】
    /// 存储一个用户的完整信息：用户名、密码、角色。
    ///
    /// 【密码安全（V1.58.22 起）】
    /// Password 字段保存的是 PBKDF2 哈希字符串（格式见 Services/PasswordHasher.cs），
    /// 而非明文密码，因此 Users.json 中不会出现明文。所有写入入口（UserManager 的
    /// 添加账号/修改密码/默认账号）都先经 PasswordHasher.Hash 再赋值；
    /// 所有比对入口（登录/验证旧密码）都经 PasswordHasher.Verify 完成。
    /// </summary>
    public class UserAccount
    {
        /// <summary>用户名（登录时输入）</summary>
        public string Username { get; set; }

        /// <summary>密码（PBKDF2 哈希字符串，非明文；明文不落盘）</summary>
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
