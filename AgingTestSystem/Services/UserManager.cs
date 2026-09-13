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
    /// - 管理员: admin / 123456（仅一个业务管理员）
    /// - 技术员: technician / 123456（支持多账号）
    /// - 操作员: operator / 123456（支持多账号）
    /// - 最高权限: dev / dev123（V1.64，见下方【dev 最高权限账号】）
    ///
    /// 【dev 最高权限账号（V1.64）】
    /// - dev 是真正的最高权限，归属 Administrator 角色：走"用户权限→管理员"登录框
    ///   输入 dev / dev123 即可登录，界面上没有任何入口提示（隐藏入口）。
    /// - dev 专属能力：删除/改名/重置业务管理员（admin）账号；普通管理员只能管
    ///   操作员/技术员，碰管理员账号一律被拒。
    /// - dev 自身受保护：不允许改名、不允许删除、不允许被注册/改名占用
    ///   （AddAccount/UpdateUsername 遇到 dev 名直接回"该账号名不可用"）。
    /// - 自愈：新装自动种子 dev；老 Users.json（没有 dev）加载时自动补上，
    ///   且绝不重置任何已存账号的密码；手改文件时保留"dev + 第一个业务管理员"。/// 
    /// 【数据存储说明】
    /// 用户数据持久化到 JSON 文件：Users.json（程序运行目录下）
    /// - 程序启动时自动加载用户数据
    /// - 用户数据变更时自动保存到文件
    /// - 文件不存在时使用默认账号并自动创建文件
    /// - 文件损坏或格式错误时使用默认账号并重建文件
    ///
    /// 【密码安全（V1.58.22 起）】
    /// - 密码一律以 PBKDF2 哈希存储（见 PasswordHasher），Users.json 中不再出现明文密码。
    /// - 登录/改密/新增账号全流程走哈希（Hash 写入、Verify 比对）；项目尚未上线，
    ///   无需兼容旧版明文（存储串非哈希格式一律判定失败）。
    /// - "记住密码"功能（RememberedLogin.json）因需回填登录框，密码以 Base64 可逆形式保存，
    ///   该文件属运行时本地数据已被 gitignore，与 Users.json 的不可逆哈希是两条独立路径。
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
        /// 操作员/技术员支持多账号；管理员仅保留一个业务账号（另有 dev 最高权限账号，见 DevUsername）
        /// </summary>
        private readonly Dictionary<UserRole, List<UserAccount>> _users;

        /// <summary>
        /// 记住的登录信息（按角色索引，用于登录时自动填充用户名/密码）
        /// </summary>
        private readonly Dictionary<UserRole, RememberedLogin> _rememberedLogins;

        /// <summary>
        /// 当前已登录的用户（未登录时为 null）
        /// </summary>
        public UserAccount CurrentUser
        {
            // 【大扫荡】对外只给副本：调用方改返回对象的 Password 也污染不了内部。
            get { return _currentUser != null ? _currentUser.Clone() : null; }
            private set { _currentUser = value; }
        }
        private UserAccount _currentUser;

        /// <summary>
        /// dev 最高权限账号名（V1.64）。
        /// 归属 Administrator 角色，走管理员登录框进入（隐藏入口，无界面提示）。
        /// 该名字被系统保留：任何人注册/改名成它都会收到"该账号名不可用"。
        /// </summary>
        public const string DevUsername = "dev";

        /// <summary>dev 账号的初始默认密码（仅用于新装种子与老文件自愈补种）</summary>
        private const string DevDefaultPassword = "dev123";

        /// <summary>
        /// 是否为 dev 保留名（大小写不敏感：dev / DEV / Dev 一律视为占用，
        /// 防止用大小写变体混淆视听）。
        /// </summary>
        /// <param name="username">待检查的用户名（可带首尾空格）</param>
        public static bool IsDevUsername(string username)
        {
            return string.Equals(username == null ? null : username.Trim(),
                DevUsername, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 当前登录的是否为 dev 最高权限（V1.64）。
        /// 判据只看用户名（dev 归属 Administrator 角色，能登录进来本身已验过密码）。
        /// </summary>
        public bool IsDevLoggedIn
        {
            get
            {
                return _currentUser != null &&
                    string.Equals(_currentUser.Username, DevUsername, StringComparison.OrdinalIgnoreCase);
            }
        }

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

            _users[UserRole.Operator].Add(new UserAccount("operator", PasswordHasher.Hash("123456"), UserRole.Operator));
            _users[UserRole.Technician].Add(new UserAccount("technician", PasswordHasher.Hash("123456"), UserRole.Technician));
            _users[UserRole.Administrator].Add(new UserAccount("admin", PasswordHasher.Hash("123456"), UserRole.Administrator));
            // 【V1.64】种子 dev 最高权限账号（归属管理员角色，走管理员登录框进入）
            _users[UserRole.Administrator].Add(new UserAccount(DevUsername, PasswordHasher.Hash(DevDefaultPassword), UserRole.Administrator));
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
                string jsonContent = AtomicFile.SafeReadAllText(UserDataFilePath);

                // 反序列化 JSON
                List<UserAccount> userList = JsonConvert.DeserializeObject<List<UserAccount>>(jsonContent);

                if (userList == null || userList.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[用户管理] 用户数据文件为空，将使用默认账号");
                    return false;
                }

                // 将用户列表按角色分组。
                // 管理员组保留规则（V1.64）：保留 dev + 第一个业务管理员。
                // 为什么不断舍离：dev 是最高权限入口，丢了就进不来；业务管理员只留一个
                // （历史约定，防手改出多个）。只读文件不写回，下次存盘自然收敛。
                _users.Clear();
                foreach (UserRole role in Enum.GetValues(typeof(UserRole)))
                {
                    _users.Add(role, new List<UserAccount>());
                }
                bool keptDevAdmin = false;
                bool keptBizAdmin = false;
                foreach (var user in userList)
                {
                    if (!_users.ContainsKey(user.Role))
                    {
                        continue;
                    }
                    if (user.Role == UserRole.Administrator)
                    {
                        if (IsDevUsername(user.Username))
                        {
                            if (!keptDevAdmin)
                            {
                                _users[user.Role].Add(user);
                                keptDevAdmin = true;
                            }
                        }
                        else if (!keptBizAdmin)
                        {
                            _users[user.Role].Add(user);
                            keptBizAdmin = true;
                        }
                        else
                        {
                            // 【大扫荡】多余业务管理员照历史约定丢弃，但点名记一笔
                            //（以前无声消失，下次存盘永久擦除，排障时对不上数）。
                            System.Diagnostics.Debug.WriteLine(
                                "[用户管理] 手改文件含多余业务管理员，已忽略: {0}", user.Username);
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
                _users[UserRole.Operator].Add(new UserAccount("operator", PasswordHasher.Hash("123456"), UserRole.Operator));
            }
            if (_users[UserRole.Technician].Count == 0)
            {
                _users[UserRole.Technician].Add(new UserAccount("technician", PasswordHasher.Hash("123456"), UserRole.Technician));
            }
            if (_users[UserRole.Administrator].Count == 0)
            {
                _users[UserRole.Administrator].Add(new UserAccount("admin", PasswordHasher.Hash("123456"), UserRole.Administrator));
            }

            // 【V1.64】老文件自愈：管理员组里没有 dev 就补一个（默认密码 dev123）。
            // 只补缺席、不碰已存账号：老 admin 密码是什么还是什么，dev 自改过的密码也不会被重置。
            // （dev 在内存里不存在才会补；文件里有 dev 时上面分组已保留，走不到这里。）
            bool hasDev = false;
            foreach (var admin in _users[UserRole.Administrator])
            {
                if (IsDevUsername(admin.Username))
                {
                    hasDev = true;
                    break;
                }
            }
            if (!hasDev)
            {
                _users[UserRole.Administrator].Add(new UserAccount(DevUsername, PasswordHasher.Hash(DevDefaultPassword), UserRole.Administrator));
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

                // 写入文件（【大扫荡】原子写：不断电写半截）
                AtomicFile.WriteAllText(UserDataFilePath, jsonContent);

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
            // 【大扫荡】大小写不敏感（与注册/改名查重口径一致；以前精确匹配，
            // 存"dev"输"DEV"报用户名错误、但该名又注册不了，口径分裂）。
            foreach (UserAccount account in accounts)
            {
                // 用户名不匹配则继续查找下一个账号
                if (!string.Equals(account.Username, trimmedUsername, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // 校验密码（存储的是 PBKDF2 哈希，Verify 内部会哈希后再比对）
                if (!PasswordHasher.Verify(password, account.Password))
                {
                    return LoginResult.Fail("密码错误");
                }

                // 登录成功，记录当前用户
                // 【大扫荡】存副本：调用方拿到 LoginResult.User 后改什么都污染不了内部。
                CurrentUser = account.Clone();
                return LoginResult.Ok(account.Clone());
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
        /// 按角色+用户名找内部账号对象（【大扫荡】写操作统一入口：调用方传的可能是
        /// GetAccounts/LoginResult 的副本，按值定位内部对象再改，改副本等于没改）。
        /// 大小写不敏感（与登录/查重口径一致）。
        /// </summary>
        private UserAccount FindInternal(UserRole role, string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return null;
            if (!_users.TryGetValue(role, out List<UserAccount> accounts)) return null;
            foreach (var a in accounts)
            {
                if (a != null && string.Equals(a.Username, username.Trim(),
                    StringComparison.OrdinalIgnoreCase)) return a;
            }
            return null;
        }

        /// <summary>
        /// 修改指定账号的用户名（仅管理员可调用；V1.64 起操作管理员组账号须 dev 在场）。
        /// </summary>
        /// <param name="account">要修改的账号</param>
        /// <param name="newUsername">新用户名</param>
        /// <returns>修改成功返回 true，失败返回 false（含错误信息）</returns>
        public (bool Success, string Message) UpdateUsername(UserAccount account, string newUsername)
        {
            // 权限校验：必须管理员才能修改
            if (_currentUser == null || _currentUser.Role != UserRole.Administrator)
            {
                return (false, "权限不足：只有管理员可以修改用户名");
            }

            if (account == null)
            {
                return (false, "未找到目标账号");
            }

            // 【大扫荡】按值定位内部对象（调用方传的可能是副本，改副本等于没改）。
            UserAccount target = FindInternal(account.Role, account.Username);
            if (target == null)
            {
                return (false, "未找到目标账号");
            }

            // 【V1.64】dev 账号不允许改名：改了名隐藏入口就对不上了，且自愈会再种一个 dev 出来造成混乱
            if (IsDevUsername(target.Username))
            {
                return (false, "dev 账号不允许改名");
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

            // 【V1.64】新名字不允许占用 dev（大小写变体也不行，防混淆）
            if (IsDevUsername(trimmed))
            {
                return (false, "该账号名不可用");
            }

            // 【V1.64】业务管理员的改名只有 dev 能动：普通管理员管操作员/技术员，
            // 管理员组的人事权收归 dev（防管理员之间互相改名捣乱）
            if (target.Role == UserRole.Administrator && !IsDevLoggedIn)
            {
                return (false, "只有 dev 最高权限可以修改管理员账号");
            }

            // 用户名在全部角色账号内保持唯一（排除账号自身）
            foreach (var accountList in _users.Values)
            {
                foreach (var other in accountList)
                {
                    if (ReferenceEquals(other, target))
                    {
                        continue;
                    }
                    if (string.Equals(other.Username, trimmed, StringComparison.OrdinalIgnoreCase))
                    {
                        return (false, $"用户名 '{trimmed}' 已被其他账号占用");
                    }
                }
            }

            string oldName = target.Username;
            target.Username = trimmed;
            // 改的是当前登录账号：同步当前会话名（否则会话还叫旧名）
            if (_currentUser != null && string.Equals(_currentUser.Username, oldName,
                StringComparison.OrdinalIgnoreCase))
            {
                _currentUser.Username = trimmed;
            }

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
        /// 管理员修改其他账号（操作员/技术员）密码请使用 UpdatePassword；
        /// 管理员组账号的密码重置请找 dev（UpdatePassword，V1.64）。
        /// </summary>
        /// <param name="oldPassword">当前密码</param>
        /// <param name="newPassword">新密码</param>
        /// <returns>修改结果（成功/失败及信息）</returns>
        public (bool Success, string Message) ChangeOwnPassword(string oldPassword, string newPassword)
        {
            // 必须已登录才能修改自己的密码
            if (_currentUser == null)
            {
                return (false, "当前未登录，无法修改密码");
            }

            // 【大扫荡】定位内部对象改（CurrentUser 是副本，改它等于没改）。
            UserAccount self = FindInternal(_currentUser.Role, _currentUser.Username);
            if (self == null)
            {
                return (false, "当前账号不存在");
            }

            // 当前密码不能为空
            if (string.IsNullOrWhiteSpace(oldPassword))
            {
                return (false, "请输入当前密码");
            }

            // 验证当前密码是否正确（哈希比对）
            if (!PasswordHasher.Verify(oldPassword, self.Password))
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

            // 新密码不能与当前密码相同（哈希比对，判断是否仍是同一明文）
            if (PasswordHasher.Verify(newPassword, self.Password))
            {
                return (false, "新密码不能与当前密码相同");
            }

            // 修改当前登录用户的密码（存哈希，明文不落盘）
            self.Password = PasswordHasher.Hash(newPassword);
            _currentUser.Password = self.Password;

            // 修改成功后保存到文件
            SaveUsersToFile();

            // 密码已变更，清除该角色记住的登录信息（避免下次自动填充旧密码导致登录失败）
            ClearRememberedLogin(_currentUser.Role);

            return (true, "密码修改成功");
        }

        /// <summary>
        /// 修改指定账号的密码（仅管理员可调用；V1.64 起操作管理员组账号须 dev 在场。
        /// dev 自己的密码建议走 ChangeOwnPassword 自改；dev 调本方法改自己也放行）。
        /// </summary>
        /// <param name="account">要修改的账号</param>
        /// <param name="newPassword">新密码</param>
        /// <returns>修改结果（成功/失败及信息）</returns>
        public (bool Success, string Message) UpdatePassword(UserAccount account, string newPassword)
        {
            // 权限校验：必须管理员才能修改
            if (_currentUser == null || _currentUser.Role != UserRole.Administrator)
            {
                return (false, "权限不足：只有管理员可以修改密码");
            }

            if (account == null)
            {
                return (false, "未找到目标账号");
            }

            // 【大扫荡】按值定位内部对象（调用方传的可能是副本）。
            UserAccount target = FindInternal(account.Role, account.Username);
            if (target == null)
            {
                return (false, "未找到目标账号");
            }

            // 【V1.64】管理员组密码重置权收归 dev（含 dev 自身：普通管理员连 dev 的边都碰不到）
            if (target.Role == UserRole.Administrator && !IsDevLoggedIn)
            {
                return (false, "只有 dev 最高权限可以修改管理员账号");
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

            // 存哈希，明文不落盘
            target.Password = PasswordHasher.Hash(newPassword);
            // 改的是当前登录账号：同步会话副本
            if (_currentUser != null && string.Equals(_currentUser.Username, target.Username,
                StringComparison.OrdinalIgnoreCase))
            {
                _currentUser.Password = target.Password;
            }

            // 修改成功后保存到文件
            SaveUsersToFile();

            return (true, "密码修改成功");
        }

        /// <summary>
        /// 获取指定角色下的全部账号（用于登录下拉框/用户管理窗体显示）。
        /// 【大扫荡】返回副本：调用方强转/改密码都污染不了内部。
        /// </summary>
        /// <param name="role">目标角色</param>
        /// <returns>账号列表副本（角色不存在时返回空列表）</returns>
        public IReadOnlyList<UserAccount> GetAccounts(UserRole role)
        {
            if (_users.TryGetValue(role, out List<UserAccount> accounts))
            {
                var copy = new List<UserAccount>(accounts.Count);
                foreach (var a in accounts)
                {
                    if (a != null) copy.Add(a.Clone());
                }
                return copy;
            }
            return new List<UserAccount>();
        }

        /// <summary>
        /// 添加新账号（仅管理员可调用）
        /// 操作员/技术员支持多账号；业务管理员账号最多一个（dev 不占这个名额）。
        /// dev 名被系统保留，任何人注册都回"该账号名不可用"。
        /// </summary>
        /// <param name="role">目标角色</param>
        /// <param name="username">用户名</param>
        /// <param name="password">密码</param>
        /// <returns>添加结果（成功/失败及信息）</returns>
        public (bool Success, string Message) AddAccount(UserRole role, string username, string password)
        {
            // 权限校验：必须管理员才能添加账号
            if (_currentUser == null || _currentUser.Role != UserRole.Administrator)
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

            // 【V1.64】dev 名系统保留：注册/添加时直接提示不可用（大小写变体同样拦截）
            if (IsDevUsername(trimmedUsername))
            {
                return (false, "该账号名不可用");
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

            // 业务管理员账号最多一个（dev 是系统账号，不占这个名额、不参与计数）
            if (role == UserRole.Administrator)
            {
                int bizAdminCount = 0;
                if (_users.TryGetValue(role, out List<UserAccount> admins))
                {
                    foreach (var a in admins)
                    {
                        if (!IsDevUsername(a.Username))
                        {
                            bizAdminCount++;
                        }
                    }
                }
                if (bizAdminCount >= 1)
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

            // 存哈希，明文不落盘
            accounts.Add(new UserAccount(trimmedUsername, PasswordHasher.Hash(password), role));

            // 添加成功后保存到文件
            SaveUsersToFile();

            return (true, $"账号 '{trimmedUsername}' 添加成功");
        }

        /// <summary>
        /// 删除指定角色下的账号（仅管理员可调用；V1.64 起删除管理员组账号须 dev 在场）。
        /// 每角色至少保留一个账号；dev 账号任何人都不允许删除。
        /// </summary>
        /// <param name="role">目标角色</param>
        /// <param name="username">要删除的用户名</param>
        /// <returns>删除结果（成功/失败及信息）</returns>
        public (bool Success, string Message) RemoveAccount(UserRole role, string username)
        {
            // 权限校验：必须管理员才能删除账号
            if (_currentUser == null || _currentUser.Role != UserRole.Administrator)
            {
                return (false, "权限不足：只有管理员可以删除账号");
            }

            // 【V1.64】dev 账号是最高权限入口，任何人（含 dev 自己）都不允许删除：
            // 删了就再也进不来，只能去服务器上改 Users.json 救火
            if (IsDevUsername(username))
            {
                return (false, "dev 账号不允许删除");
            }

            // 【V1.64】业务管理员的删除权收归 dev：普通管理员删不动管理员组，
            // dev 可删业务管理员（如 admin 离职交接、密码丢失且自改通道失效时兜底）
            if (role == UserRole.Administrator && !IsDevLoggedIn)
            {
                return (false, "只有 dev 最高权限可以删除管理员账号");
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
            // 【大扫荡】按用户名比对（CurrentUser 是副本，ReferenceEquals 永假）。
            if (_currentUser != null && string.Equals(_currentUser.Username, target.Username,
                StringComparison.OrdinalIgnoreCase))
            {
                _currentUser = null;
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
            if (_currentUser == null)
            {
                // 未登录视为操作员权限（最低）
                return UserRole.Operator >= requiredRole;
            }
            return _currentUser.Role >= requiredRole;
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

                string jsonContent = AtomicFile.SafeReadAllText(RememberedLoginFilePath);
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
                AtomicFile.WriteAllText(RememberedLoginFilePath, jsonContent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[用户管理] 保存记住的登录信息失败: {0}", ex.Message);
            }
        }

        /// <summary>
        /// 密码编码（【大扫荡】DPAPI 本机加密存"记住密码"，替代 Base64 明文可逆。
        /// 拷走 RememberedLogin.json 也解不开——与 MES 密钥同口径。
        /// 老 Base64 文件读回 null（当没记住过，重输一次即可；项目未上线不做兼容）。
        /// </summary>
        private string EncodePassword(string password)
        {
            string enc = MesCrypto.Protect(password);
            return enc ?? "";
        }

        /// <summary>
        /// 密码解码（对应 EncodePassword；老格式/损坏返回 null）。
        /// </summary>
        private string DecodePassword(string encoded)
        {
            try
            {
                if (string.IsNullOrEmpty(encoded)) return null;
                // 老 Base64 格式无 DPAPI 前缀 → Unprotect 拒绝（返回 null），当没记住过。
                return MesCrypto.Unprotect(encoded);
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
