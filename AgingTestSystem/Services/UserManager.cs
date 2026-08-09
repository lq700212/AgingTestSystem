using System;
using System.Collections.Generic;
using System.IO;
using AgingTestSystem.Models;
using Newtonsoft.Json;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 用户管理服务
    ///
    /// 【功能说明】
    /// 1. 维护系统中所有用户账号（操作员/技术员/管理员）
    /// 2. 提供登录验证功能（校验用户名和密码）
    /// 3. 提供密码修改功能：任意角色可修改自己的密码（ChangeOwnPassword，验证旧密码）；管理员可修改操作员/技术员密码（UpdatePassword）
    /// 4. 提供用户名修改功能（仅管理员可调用）
    /// 5. 用户数据持久化到 JSON 文件（程序重启后数据不丢失）
    ///
    /// 【默认账号】
    /// - 管理员: admin / 123456（仅一个账号）
    /// - 技术员: technician / 123456（支持多账号）
    /// - 操作员: operator / 123456（支持多账号）
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
        /// 记住的登录信息文件路径（程序运行目录下的 RememberedLogin.json）
        /// 属于运行时用户数据（含密码），已被 gitignore，不入库
        /// </summary>
        private const string RememberedLoginFilePath = "RememberedLogin.json";

        /// <summary>
        /// 用户列表（按角色索引方便查找）
        /// Key: 用户角色，Value: 该角色下的账号列表
        /// 操作员/技术员支持多账号；管理员仅保留一个账号
        /// </summary>
        private readonly Dictionary<UserRole, List<UserAccount>> _users;

        /// <summary>
        /// 记住的登录信息（按角色索引，用于登录时自动填充用户名/密码）
        /// </summary>
        private readonly Dictionary<UserRole, RememberedLogin> _rememberedLogins;

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
            _users = new Dictionary<UserRole, List<UserAccount>>();

            // 尝试从文件加载用户数据
            bool loadSuccess = LoadUsersFromFile();

            if (!loadSuccess)
            {
                // 加载失败，使用默认账号
                InitializeDefaultUsers();

                // 保存默认账号到文件
                SaveUsersToFile();
            }

            // 加载记住的登录信息（用于登录时自动填充）
            _rememberedLogins = new Dictionary<UserRole, RememberedLogin>();
            LoadRememberedLogins();

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
            _users.Add(UserRole.Operator, new List<UserAccount>());
            _users.Add(UserRole.Technician, new List<UserAccount>());
            _users.Add(UserRole.Administrator, new List<UserAccount>());

            _users[UserRole.Operator].Add(new UserAccount("operator", "123456", UserRole.Operator));
            _users[UserRole.Technician].Add(new UserAccount("technician", "123456", UserRole.Technician));
            _users[UserRole.Administrator].Add(new UserAccount("admin", "123456", UserRole.Administrator));
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

                // 将用户列表按角色分组（管理员仅保留第一个账号，防止手改出多个）
                _users.Clear();
                foreach (UserRole role in Enum.GetValues(typeof(UserRole)))
                {
                    _users.Add(role, new List<UserAccount>());
                }
                foreach (var user in userList)
                {
                    if (!_users.ContainsKey(user.Role))
                    {
                        continue;
                    }
                    if (user.Role == UserRole.Administrator)
                    {
                        if (_users[user.Role].Count == 0)
                        {
                            _users[user.Role].Add(user);
                        }
                    }
                    else
                    {
                        _users[user.Role].Add(user);
                    }
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
                _users.Add(UserRole.Operator, new List<UserAccount>());
            }
            if (!_users.ContainsKey(UserRole.Technician))
            {
                _users.Add(UserRole.Technician, new List<UserAccount>());
            }
            if (!_users.ContainsKey(UserRole.Administrator))
            {
                _users.Add(UserRole.Administrator, new List<UserAccount>());
            }

            // 每个角色至少保留一个默认账号
            if (_users[UserRole.Operator].Count == 0)
            {
                _users[UserRole.Operator].Add(new UserAccount("operator", "123456", UserRole.Operator));
            }
            if (_users[UserRole.Technician].Count == 0)
            {
                _users[UserRole.Technician].Add(new UserAccount("technician", "123456", UserRole.Technician));
            }
            if (_users[UserRole.Administrator].Count == 0)
            {
                _users[UserRole.Administrator].Add(new UserAccount("admin", "123456", UserRole.Administrator));
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
                // 展平所有角色的账号列表
                List<UserAccount> userList = new List<UserAccount>();
                foreach (var accountList in _users.Values)
                {
                    userList.AddRange(accountList);
                }

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

            // 查找目标角色下所有账号
            if (!_users.TryGetValue(targetRole, out List<UserAccount> accounts) || accounts.Count == 0)
            {
                // 理论上不会发生（每个角色都有默认账号），防御性处理
                return LoginResult.Fail("未找到对应角色的账号");
            }

            // 在目标角色的账号列表中匹配用户名和密码
            foreach (UserAccount account in accounts)
            {
                // 用户名不匹配则继续查找下一个账号
                if (!string.Equals(account.Username, trimmedUsername, StringComparison.Ordinal))
                {
                    continue;
                }

                // 校验密码
                if (!string.Equals(account.Password, password, StringComparison.Ordinal))
                {
                    return LoginResult.Fail("密码错误");
                }

                // 登录成功，记录当前用户
                CurrentUser = account;
                return LoginResult.Ok(account);
            }

            return LoginResult.Fail("用户名错误");
        }

        /// <summary>
        /// 退出登录（恢复为未登录状态）
        /// </summary>
        public void Logout()
        {
            CurrentUser = null;
        }

        /// <summary>
        /// 修改指定账号的用户名（仅管理员可调用）
        /// </summary>
        /// <param name="account">要修改的账号</param>
        /// <param name="newUsername">新用户名</param>
        /// <returns>修改成功返回 true，失败返回 false（含错误信息）</returns>
        public (bool Success, string Message) UpdateUsername(UserAccount account, string newUsername)
        {
            // 权限校验：必须管理员才能修改
            if (CurrentUser == null || CurrentUser.Role != UserRole.Administrator)
            {
                return (false, "权限不足：只有管理员可以修改用户名");
            }

            if (account == null)
            {
                return (false, "未找到目标账号");
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

            // 用户名在全部角色账号内保持唯一（排除账号自身）
            foreach (var accountList in _users.Values)
            {
                foreach (var other in accountList)
                {
                    if (ReferenceEquals(other, account))
                    {
                        continue;
                    }
                    if (string.Equals(other.Username, trimmed, StringComparison.OrdinalIgnoreCase))
                    {
                        return (false, $"用户名 '{trimmed}' 已被其他账号占用");
                    }
                }
            }

            account.Username = trimmed;

            // 修改成功后保存到文件
            SaveUsersToFile();

            return (true, "用户名修改成功");
        }

        /// <summary>
        /// 修改当前登录用户自己的密码（操作员/技术员/管理员均可调用）
        ///
        /// 【场景】
        /// 操作员/技术员/管理员修改自己的密码，必须验证旧密码，
        /// 防止他人在无人值守时篡改账号密码。
        /// 管理员修改其他账号（操作员/技术员）密码请使用 UpdatePassword。
        /// </summary>
        /// <param name="oldPassword">当前密码</param>
        /// <param name="newPassword">新密码</param>
        /// <returns>修改结果（成功/失败及信息）</returns>
        public (bool Success, string Message) ChangeOwnPassword(string oldPassword, string newPassword)
        {
            // 必须已登录才能修改自己的密码
            if (CurrentUser == null)
            {
                return (false, "当前未登录，无法修改密码");
            }

            // 当前密码不能为空
            if (string.IsNullOrWhiteSpace(oldPassword))
            {
                return (false, "请输入当前密码");
            }

            // 验证当前密码是否正确
            if (!string.Equals(CurrentUser.Password, oldPassword, StringComparison.Ordinal))
            {
                return (false, "当前密码错误");
            }

            // 新密码不能为空
            if (string.IsNullOrWhiteSpace(newPassword))
            {
                return (false, "新密码不能为空");
            }

            if (newPassword.Length < 4)
            {
                return (false, "新密码至少需要4个字符");
            }

            // 新密码不能与当前密码相同
            if (string.Equals(CurrentUser.Password, newPassword, StringComparison.Ordinal))
            {
                return (false, "新密码不能与当前密码相同");
            }

            // 修改当前登录用户的密码
            CurrentUser.Password = newPassword;

            // 修改成功后保存到文件
            SaveUsersToFile();

            // 密码已变更，清除该角色记住的登录信息（避免下次自动填充旧密码导致登录失败）
            ClearRememberedLogin(CurrentUser.Role);

            return (true, "密码修改成功");
        }

        /// <summary>
        /// 修改指定账号的密码（仅管理员可调用）
        /// </summary>
        /// <param name="account">要修改的账号</param>
        /// <param name="newPassword">新密码</param>
        /// <returns>修改结果（成功/失败及信息）</returns>
        public (bool Success, string Message) UpdatePassword(UserAccount account, string newPassword)
        {
            // 权限校验：必须管理员才能修改
            if (CurrentUser == null || CurrentUser.Role != UserRole.Administrator)
            {
                return (false, "权限不足：只有管理员可以修改密码");
            }

            if (account == null)
            {
                return (false, "未找到目标账号");
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

            account.Password = newPassword;

            // 修改成功后保存到文件
            SaveUsersToFile();

            return (true, "密码修改成功");
        }

        /// <summary>
        /// 获取指定角色下的全部账号（用于登录下拉框/用户管理窗体显示）
        /// </summary>
        /// <param name="role">目标角色</param>
        /// <returns>账号列表（角色不存在时返回空列表）</returns>
        public IReadOnlyList<UserAccount> GetAccounts(UserRole role)
        {
            if (_users.TryGetValue(role, out List<UserAccount> accounts))
            {
                return accounts;
            }
            return new List<UserAccount>();
        }

        /// <summary>
        /// 添加新账号（仅管理员可调用）
        /// 操作员/技术员支持多账号；管理员账号最多一个。
        /// </summary>
        /// <param name="role">目标角色</param>
        /// <param name="username">用户名</param>
        /// <param name="password">密码</param>
        /// <returns>添加结果（成功/失败及信息）</returns>
        public (bool Success, string Message) AddAccount(UserRole role, string username, string password)
        {
            // 权限校验：必须管理员才能添加账号
            if (CurrentUser == null || CurrentUser.Role != UserRole.Administrator)
            {
                return (false, "权限不足：只有管理员可以添加账号");
            }

            // 用户名校验
            if (string.IsNullOrWhiteSpace(username))
            {
                return (false, "用户名不能为空");
            }

            string trimmedUsername = username.Trim();
            if (trimmedUsername.Length < 2)
            {
                return (false, "用户名至少需要2个字符");
            }

            // 密码校验
            if (string.IsNullOrWhiteSpace(password))
            {
                return (false, "密码不能为空");
            }

            if (password.Length < 4)
            {
                return (false, "密码至少需要4个字符");
            }

            // 管理员账号最多一个
            if (role == UserRole.Administrator)
            {
                if (_users.TryGetValue(role, out List<UserAccount> admins) && admins.Count >= 1)
                {
                    return (false, "管理员账号只能有一个");
                }
            }

            // 用户名在全部角色账号内保持唯一
            foreach (var accountList in _users.Values)
            {
                foreach (var other in accountList)
                {
                    if (string.Equals(other.Username, trimmedUsername, StringComparison.OrdinalIgnoreCase))
                    {
                        return (false, $"用户名 '{trimmedUsername}' 已被占用");
                    }
                }
            }

            if (!_users.TryGetValue(role, out List<UserAccount> accounts))
            {
                accounts = new List<UserAccount>();
                _users.Add(role, accounts);
            }

            accounts.Add(new UserAccount(trimmedUsername, password, role));

            // 添加成功后保存到文件
            SaveUsersToFile();

            return (true, $"账号 '{trimmedUsername}' 添加成功");
        }

        /// <summary>
        /// 删除指定角色下的账号（仅管理员可调用）
        /// 每角色至少保留一个账号；管理员账号不允许删除。
        /// </summary>
        /// <param name="role">目标角色</param>
        /// <param name="username">要删除的用户名</param>
        /// <returns>删除结果（成功/失败及信息）</returns>
        public (bool Success, string Message) RemoveAccount(UserRole role, string username)
        {
            // 权限校验：必须管理员才能删除账号
            if (CurrentUser == null || CurrentUser.Role != UserRole.Administrator)
            {
                return (false, "权限不足：只有管理员可以删除账号");
            }

            // 管理员账号不允许删除（只能有一个，删除后无法恢复默认管理）
            if (role == UserRole.Administrator)
            {
                return (false, "管理员账号不允许删除");
            }

            if (!_users.TryGetValue(role, out List<UserAccount> accounts))
            {
                return (false, "未找到目标角色账号");
            }

            UserAccount target = null;
            foreach (var account in accounts)
            {
                if (string.Equals(account.Username, username, StringComparison.OrdinalIgnoreCase))
                {
                    target = account;
                    break;
                }
            }

            if (target == null)
            {
                return (false, "未找到目标账号");
            }

            // 每角色至少保留一个账号
            if (accounts.Count <= 1)
            {
                return (false, "该角色至少需要保留一个账号");
            }

            accounts.Remove(target);

            // 若删除的是当前登录账号，恢复为未登录状态
            if (ReferenceEquals(CurrentUser, target))
            {
                CurrentUser = null;
            }

            // 删除成功后保存到文件
            SaveUsersToFile();

            return (true, $"账号 '{username}' 删除成功");
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

        /// <summary>
        /// 获取指定角色记住的登录信息（登录窗体加载时自动填充）
        /// </summary>
        /// <param name="role">目标角色</param>
        /// <returns>记住的用户名和密码（未记住时返回 (null, null)）</returns>
        public (string Username, string Password) GetRememberedLogin(UserRole role)
        {
            if (_rememberedLogins.TryGetValue(role, out RememberedLogin remembered))
            {
                return (remembered.Username, DecodePassword(remembered.Password));
            }
            return (null, null);
        }

        /// <summary>
        /// 记住指定角色的登录信息（勾选"记住密码"登录成功后调用）
        /// </summary>
        /// <param name="role">目标角色</param>
        /// <param name="username">用户名</param>
        /// <param name="password">密码</param>
        public void SaveRememberedLogin(UserRole role, string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || password == null)
            {
                return;
            }

            _rememberedLogins[role] = new RememberedLogin
            {
                Role = role,
                Username = username.Trim(),
                Password = EncodePassword(password)
            };
            SaveRememberedLoginsToFile();
        }

        /// <summary>
        /// 清除指定角色记住的登录信息（未勾选"记住密码"登录成功后调用）
        /// </summary>
        /// <param name="role">目标角色</param>
        public void ClearRememberedLogin(UserRole role)
        {
            if (_rememberedLogins.Remove(role))
            {
                SaveRememberedLoginsToFile();
            }
        }

        /// <summary>
        /// 从文件加载记住的登录信息（文件不存在或损坏时静默忽略）
        /// </summary>
        private void LoadRememberedLogins()
        {
            try
            {
                if (!File.Exists(RememberedLoginFilePath))
                {
                    return;
                }

                string jsonContent = File.ReadAllText(RememberedLoginFilePath);
                List<RememberedLogin> list = JsonConvert.DeserializeObject<List<RememberedLogin>>(jsonContent);
                if (list == null)
                {
                    return;
                }

                foreach (var item in list)
                {
                    _rememberedLogins[item.Role] = item;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[用户管理] 加载记住的登录信息失败: {0}", ex.Message);
            }
        }

        /// <summary>
        /// 保存记住的登录信息到文件
        /// </summary>
        private void SaveRememberedLoginsToFile()
        {
            try
            {
                List<RememberedLogin> list = new List<RememberedLogin>(_rememberedLogins.Values);
                string jsonContent = JsonConvert.SerializeObject(list, Formatting.Indented);
                File.WriteAllText(RememberedLoginFilePath, jsonContent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[用户管理] 保存记住的登录信息失败: {0}", ex.Message);
            }
        }

        /// <summary>
        /// 密码编码（Base64 混淆，仅演示用，非安全加密）
        /// 与 Users.json 一样仅存本地、不提交 git，若需安全存储应改用 DPAPI/哈希
        /// </summary>
        private string EncodePassword(string password)
            => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password));

        /// <summary>
        /// 密码解码（对应 EncodePassword）
        /// </summary>
        private string DecodePassword(string encoded)
        {
            try
            {
                return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[用户管理] 解码记住的密码失败: {0}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 记住的登录信息模型（用于 JSON 序列化）
        /// </summary>
        private class RememberedLogin
        {
            /// <summary>目标角色</summary>
            public UserRole Role { get; set; }

            /// <summary>用户名</summary>
            public string Username { get; set; }

            /// <summary>密码（Base64 混淆存储）</summary>
            public string Password { get; set; }
        }
    }
}
