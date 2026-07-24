using System;
using System.Collections.Generic;
using System.IO;
using BarometerWinform.Models;
using Newtonsoft.Json;

namespace BarometerWinform.Services
{
    /// <summary>
    /// 用户管理服务
    ///
    /// 【功能说明】
    /// 1. 维护系统中所有用户账号（操作员/技术员/管理员）
    /// 2. 提供登录验证功能（校验用户名和密码）
    /// 3. 提供密码修改功能（仅管理员可调用）
    /// 4. 提供用户名修改功能（仅管理员可调用）
    /// 5. 用户数据持久化到 JSON 文件（程序重启后数据不丢失）
    ///
    /// 【默认账号】
    /// - 管理员: admin / 123456
    /// - 技术员: technician / 123456
    /// - 操作员: operator / 123456
    ///
    /// 【数据存储说明】
    /// 用户数据持久化到 JSON 文件：Users.json（程序运行目录下）
    /// - 程序启动时自动加载用户数据
    /// - 用户数据变更时自动保存到文件
    /// - 文件不存在时使用默认账号并自动创建文件
    /// - 文件损坏或格式错误时使用默认账号并重建文件
    ///
    /// 【JSON 持久化方案选择】
    /// 选择 JSON 而非 XML 的原因：
    /// 1. JSON 文件体积更小，键值对结构简洁，无冗余标签
    /// 2. JSON 更易读，直观的键值格式，便于人工查看和修改
    /// 3. Newtonsoft.Json API 简单，一行代码即可完成序列化/反序列化
    /// 4. JSON 是当前主流数据交换格式，学习成本低
    /// 5. XML 需要处理命名空间、声明等，相对复杂繁琐
    /// </summary>
    public class UserManager
    {
        /// <summary>
        /// 用户数据文件路径（程序运行目录下的 Users.json）
        /// </summary>
        private const string UserDataFilePath = "Users.json";

        /// <summary>
        /// 用户列表（按角色索引方便查找）
        /// Key: 用户角色，Value: 该角色的用户账号
        /// </summary>
        private readonly Dictionary<UserRole, UserAccount> _users;

        /// <summary>
        /// 当前已登录的用户（未登录时为 null）
        /// </summary>
        public UserAccount CurrentUser { get; private set; }

        /// <summary>
        /// 构造函数 - 初始化用户账号
        /// 优先从 JSON 文件加载，文件不存在则使用默认账号
        /// </summary>
        public UserManager()
        {
            _users = new Dictionary<UserRole, UserAccount>();

            // 尝试从文件加载用户数据
            bool loadSuccess = LoadUsersFromFile();

            if (!loadSuccess)
            {
                // 加载失败，使用默认账号
                InitializeDefaultUsers();

                // 保存默认账号到文件
                SaveUsersToFile();
            }

            // 默认状态：未登录（CurrentPermission 显示为"操作员"对应未登录态）
            CurrentUser = null;
        }

        /// <summary>
        /// 初始化默认用户账号
        /// 当 JSON 文件不存在或加载失败时调用
        /// </summary>
        private void InitializeDefaultUsers()
        {
            _users.Clear();
            _users.Add(UserRole.Operator, new UserAccount("operator", "123456", UserRole.Operator));
            _users.Add(UserRole.Technician, new UserAccount("technician", "123456", UserRole.Technician));
            _users.Add(UserRole.Administrator, new UserAccount("admin", "123456", UserRole.Administrator));
        }

        /// <summary>
        /// 从 JSON 文件加载用户数据
        /// </summary>
        /// <returns>加载成功返回 true，失败返回 false</returns>
        private bool LoadUsersFromFile()
        {
            try
            {
                // 检查文件是否存在
                if (!File.Exists(UserDataFilePath))
                {
                    System.Diagnostics.Debug.WriteLine("[用户管理] 用户数据文件不存在，将使用默认账号");
                    return false;
                }

                // 读取文件内容
                string jsonContent = File.ReadAllText(UserDataFilePath);

                // 反序列化 JSON
                List<UserAccount> userList = JsonConvert.DeserializeObject<List<UserAccount>>(jsonContent);

                if (userList == null || userList.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[用户管理] 用户数据文件为空，将使用默认账号");
                    return false;
                }

                // 将用户列表转换为字典（按角色索引）
                _users.Clear();
                foreach (var user in userList)
                {
                    _users[user.Role] = user;
                }

                // 确保三个角色都有账号（防止文件中缺少某些角色）
                EnsureAllRolesExist();

                System.Diagnostics.Debug.WriteLine("[用户管理] 用户数据加载成功，共 {0} 个用户", _users.Count);
                return true;
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine("[用户管理] JSON 解析失败，将使用默认账号: {0}", ex.Message);
                return false;
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine("[用户管理] 读取用户数据文件失败，将使用默认账号: {0}", ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[用户管理] 加载用户数据异常，将使用默认账号: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 确保三个角色都有账号
        /// 如果文件中缺少某个角色的账号，使用默认值补充
        /// </summary>
        private void EnsureAllRolesExist()
        {
            if (!_users.ContainsKey(UserRole.Operator))
            {
                _users.Add(UserRole.Operator, new UserAccount("operator", "123456", UserRole.Operator));
            }
            if (!_users.ContainsKey(UserRole.Technician))
            {
                _users.Add(UserRole.Technician, new UserAccount("technician", "123456", UserRole.Technician));
            }
            if (!_users.ContainsKey(UserRole.Administrator))
            {
                _users.Add(UserRole.Administrator, new UserAccount("admin", "123456", UserRole.Administrator));
            }
        }

        /// <summary>
        /// 将用户数据保存到 JSON 文件
        /// </summary>
        /// <returns>保存成功返回 true，失败返回 false</returns>
        private bool SaveUsersToFile()
        {
            try
            {
                // 将字典转换为列表
                List<UserAccount> userList = new List<UserAccount>(_users.Values);

                // 序列化 JSON（格式化输出，方便阅读）
                string jsonContent = JsonConvert.SerializeObject(userList, Formatting.Indented);

                // 写入文件
                File.WriteAllText(UserDataFilePath, jsonContent);

                System.Diagnostics.Debug.WriteLine("[用户管理] 用户数据保存成功");
                return true;
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine("[用户管理] JSON 序列化失败: {0}", ex.Message);
                return false;
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine("[用户管理] 写入用户数据文件失败: {0}", ex.Message);
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[用户管理] 保存用户数据异常: {0}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 登录验证
        ///
        /// 根据目标角色查找对应账号，校验用户名和密码是否匹配。
        ///
        /// 【设计说明】
        /// 此方法不仅校验用户名和密码，还会校验"该账号是否属于目标角色"。
        /// 例如：用户想切换为"管理员"权限，必须输入管理员角色的账号密码，
        /// 不能用技术员账号登录获取管理员权限。
        /// </summary>
        /// <param name="targetRole">目标角色（用户想切换到的角色）</param>
        /// <param name="username">输入的用户名</param>
        /// <param name="password">输入的密码</param>
        /// <returns>登录结果（成功/失败及原因）</returns>
        public LoginResult Login(UserRole targetRole, string username, string password)
        {
            // 参数空值检查
            if (string.IsNullOrWhiteSpace(username))
            {
                return LoginResult.Fail("用户名不能为空");
            }
            if (string.IsNullOrWhiteSpace(password))
            {
                return LoginResult.Fail("密码不能为空");
            }

            // 去除用户名首尾空格（避免用户输入时多打空格导致登录失败）
            string trimmedUsername = username.Trim();

            // 查找目标角色的账号
            if (!_users.TryGetValue(targetRole, out UserAccount user))
            {
                // 理论上不会发生（三个角色都有默认账号），防御性处理
                return LoginResult.Fail("未找到对应角色的账号");
            }

            // 校验用户名
            if (!string.Equals(user.Username, trimmedUsername, StringComparison.Ordinal))
            {
                return LoginResult.Fail("用户名错误");
            }

            // 校验密码
            if (!string.Equals(user.Password, password, StringComparison.Ordinal))
            {
                return LoginResult.Fail("密码错误");
            }

            // 登录成功，记录当前用户
            CurrentUser = user;
            return LoginResult.Ok(user);
        }

        /// <summary>
        /// 退出登录（恢复为未登录状态）
        /// </summary>
        public void Logout()
        {
            CurrentUser = null;
        }

        /// <summary>
        /// 修改指定角色的用户名（仅管理员可调用）
        /// </summary>
        /// <param name="targetRole">要修改的目标角色</param>
        /// <param name="newUsername">新用户名</param>
        /// <returns>修改成功返回 true，失败返回 false（含错误信息）</returns>
        public (bool Success, string Message) UpdateUsername(UserRole targetRole, string newUsername)
        {
            // 权限校验：必须管理员才能修改
            if (CurrentUser == null || CurrentUser.Role != UserRole.Administrator)
            {
                return (false, "权限不足：只有管理员可以修改用户名");
            }

            // 新用户名校验
            if (string.IsNullOrWhiteSpace(newUsername))
            {
                return (false, "新用户名不能为空");
            }

            string trimmed = newUsername.Trim();
            if (trimmed.Length < 2)
            {
                return (false, "用户名至少需要2个字符");
            }

            // 不能与现有其他角色用户名重复
            foreach (var kv in _users)
            {
                if (kv.Key != targetRole &&
                    string.Equals(kv.Value.Username, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    return (false, $"用户名 '{trimmed}' 已被其他角色占用");
                }
            }

            // 找到目标账号并修改
            if (!_users.TryGetValue(targetRole, out UserAccount user))
            {
                return (false, "未找到目标角色账号");
            }

            user.Username = trimmed;

            // 修改成功后保存到文件
            SaveUsersToFile();

            return (true, "用户名修改成功");
        }

        /// <summary>
        /// 修改指定角色的密码（仅管理员可调用）
        /// </summary>
        /// <param name="targetRole">要修改的目标角色</param>
        /// <param name="newPassword">新密码</param>
        /// <returns>修改结果（成功/失败及信息）</returns>
        public (bool Success, string Message) UpdatePassword(UserRole targetRole, string newPassword)
        {
            // 权限校验：必须管理员才能修改
            if (CurrentUser == null || CurrentUser.Role != UserRole.Administrator)
            {
                return (false, "权限不足：只有管理员可以修改密码");
            }

            // 新密码校验
            if (string.IsNullOrWhiteSpace(newPassword))
            {
                return (false, "新密码不能为空");
            }

            if (newPassword.Length < 4)
            {
                return (false, "密码至少需要4个字符");
            }

            // 找到目标账号并修改
            if (!_users.TryGetValue(targetRole, out UserAccount user))
            {
                return (false, "未找到目标角色账号");
            }

            user.Password = newPassword;

            // 修改成功后保存到文件
            SaveUsersToFile();

            return (true, "密码修改成功");
        }

        /// <summary>
        /// 获取指定角色的账号信息（用于用户管理窗体显示）
        /// </summary>
        /// <param name="role">目标角色</param>
        /// <returns>账号信息（未找到返回 null）</returns>
        public UserAccount GetAccount(UserRole role)
        {
            _users.TryGetValue(role, out UserAccount user);
            return user;
        }

        /// <summary>
        /// 检查当前用户是否拥有指定角色或更高权限
        ///
        /// 【使用场景】
        /// 按钮权限控制：例如"通讯设置"按钮要求技术员或管理员才能操作，
        /// 可调用 HasPermission(UserRole.Technician) 判断。
        /// 由于枚举值 Administrator(2) > Technician(1) > Operator(0)，
        /// 当前用户角色值 >= 要求角色值即视为有权限。
        /// </summary>
        /// <param name="requiredRole">要求的最低角色</param>
        /// <returns>有权限返回 true，无权限返回 false</returns>
        public bool HasPermission(UserRole requiredRole)
        {
            if (CurrentUser == null)
            {
                // 未登录视为操作员权限（最低）
                return UserRole.Operator >= requiredRole;
            }
            return CurrentUser.Role >= requiredRole;
        }
    }
}
