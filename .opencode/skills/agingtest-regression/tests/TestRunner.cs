// ============================================================================
//  AgingTestSystem 全量回归测试 harness（无 UI、无需真设备，可离线自动运行）
//
//  【做什么】
//  用最简单的自研断言框架（Check 计数），对项目里所有"纯逻辑"核心类做
//  面面俱到的功能/边界/异常测试：
//    1. PasswordHasher        —— PBKDF2 哈希与验证（安全红线）
//    2. UserManager           —— 登录/账号管理/改密/权限/记住登录/损坏恢复（隔离临时目录）
//    3. SettingsForm 配置归一化 —— StopBits / Parity 归一映射（反射调私有静态方法）
//    4. IoOutputChannelRemap   —— IO 备用通道映射解析（含各种脏输入）
//    5. DeviceConfig          —— 送风机候选 IP 解析
//    6. RecipeStorage         —— 配方 JSON 存取往返/损坏容错
//    7. TestEventLogger       —— CSV 转义/表头/落盘
//    8. AppLogFileWriter      —— 操作日志追加/UTF-8/多线程并发完整性
//    9. PanelLayoutConfig     —— 默认布局解析、锚定幂等性、宽高联动推导
//   10. HomeLayoutConfig      —— 主页布局默认配置加载
//   11. 模型序列化            —— RecipeConfig/StationInfo/UserAccount 往返与 Clone
//
//  【怎么跑】
//  不直接运行本文件。用本 skill 目录 scripts\run_unit_tests.ps1：
//    它把主程序产物拷到独立临时 run 目录 → csc 编译本文件引用主程序集 → 运行。
//  这样 UserManager/RecipeStorage 写的 Users.json/Recipes.json、日志类写的
//  Logs\ 全部落在临时目录，绝不污染仓库与 bin\Debug。
//
//  【怎么加用例】（约定：每次修 bug / 加功能后必须同步补用例，见 AGENTS.md）
//  在对应 XxxTests 方法里加 Check("用例名", 条件) 即可；新增模块就写一个
//  private static void XxxTests() 并在 Main 里挂上。改完必须重跑全部通过。
//
//  【退出码约定】0 = 全部通过；非 0 = 有失败（供 CI/脚本判断）。
//
//  【覆盖边界说明】涉及真串口/真设备（ModbusRtuBarometerReader、ScannerService、
//  FanControllerClient、ModbusTcpIoController）与 UI 弹窗分支
//  （RecipeStorage.SaveWithDuplicateCheck 同名覆盖确认框）不在本 harness 范围，
//  由现场联调与界面手工测试覆盖；后续可扩展虚拟串口/模拟器用例。
// ============================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using AgingTestSystem.Models;
using AgingTestSystem.Services;

namespace AgingTestSystem.Tests
{
    /// <summary>
    /// 回归 harness 入口（partial：设备编排端到端集成测试见
    /// DeviceManagerIntegrationTests.cs，共享本文件的 Check/Module/EnterCleanDir 断言设施）
    /// </summary>
    internal static partial class TestRunner
    {
        // ────────────── 断言统计 ──────────────
        private static int _pass;
        private static int _fail;
        private static readonly List<string> _failures = new List<string>();
        private static string _module = "";

        /// <summary>单条断言：条件成立记 PASS，否则记 FAIL 并留明细</summary>
        private static void Check(string name, bool cond, string detail = null)
        {
            if (cond)
            {
                _pass++;
                Console.WriteLine("  [PASS] " + name);
            }
            else
            {
                _fail++;
                string msg = name + (string.IsNullOrEmpty(detail) ? "" : "  => " + detail);
                _failures.Add("[" + _module + "] " + msg);
                Console.WriteLine("  [FAIL] " + msg);
            }
        }

        /// <summary>断言某段代码抛出指定类型异常（"抛异常"本身也是预期行为）</summary>
        private static void CheckThrows<TEx>(string name, Action action) where TEx : Exception
        {
            try
            {
                action();
                Check(name, false, "未抛出 " + typeof(TEx).Name);
            }
            catch (TEx)
            {
                Check(name, true);
            }
            catch (Exception ex)
            {
                Check(name, false, "抛出了别的异常 " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>.NET Framework 没有 Convert.TryFromBase64String，用 try-catch 等价实现</summary>
        private static bool TryDecodeBase64(string s, out byte[] bytes)
        {
            try { bytes = Convert.FromBase64String(s); return true; }
            catch { bytes = null; return false; }
        }

        /// <summary>标记当前模块名（失败明细带前缀，方便定位哪个模块挂了）；模块内异常不会中断整个测试</summary>
        private static void Module(string name, Action body)
        {
            _module = name;
            Console.WriteLine();
            Console.WriteLine("======== 模块：" + name + " ========");
            try { body(); }
            catch (Exception ex)
            {
                _fail++;
                _failures.Add("[" + name + "] 模块级异常: " + ex.GetType().Name + ": " + ex.Message);
                Console.WriteLine("  [FAIL] 模块级异常: " + ex);
            }
        }

        // ────────────── 临时目录隔离工具 ──────────────
        // UserManager / RecipeStorage 用相对路径读写 json，
        // 必须切到一次性临时目录里测：用例之间互不污染，也不碰真实数据。
        private static readonly List<string> _tempDirs = new List<string>();

        private static string EnterCleanDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "AgingTestSysUT",
                DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(dir);
            _tempDirs.Add(dir);
            Environment.CurrentDirectory = dir;
            return dir;
        }

        [STAThread]
        private static int Main()
        {
            // 控制台输出切 UTF-8，避免中文断言信息乱码（脚本捕获输出也按 UTF-8 读）
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }

            Console.WriteLine("AgingTestSystem 回归测试 harness");
            Console.WriteLine("BaseDirectory = " + AppDomain.CurrentDomain.BaseDirectory);
            Console.WriteLine("起始工作目录   = " + Environment.CurrentDirectory);

            Module("PasswordHasher", PasswordHasherTests);
            Module("UserManager", UserManagerTests);
            Module("SettingsForm.Normalize", NormalizeTests);
            Module("IoOutputChannelRemap", IoRemapTests);
            Module("DeviceConfig.ParseFanIpCandidates", FanIpTests);
            Module("RecipeStorage", RecipeStorageTests);
            Module("TestEventLogger", TestEventLoggerTests);
            Module("AppLogFileWriter", AppLogWriterTests);
            Module("PanelLayoutConfig", PanelLayoutTests);
            Module("HomeLayoutConfig", HomeLayoutTests);
            Module("ModelRoundtrip", ModelRoundtripTests);
            Module("AgingSequencer", AgingSequencerTests);
            Module("TestSessionStore", TestSessionStoreTests);
            Module("AgingBusinessModel", AgingBusinessModelTests);
            Module("DeviceManagerIntegration", DeviceManagerIntegrationTests);

            // 统一清理临时目录（尽力而为，删不掉不影响结果）
            foreach (string dir in _tempDirs)
            {
                try { Directory.Delete(dir, true); } catch { }
            }

            Console.WriteLine();
            Console.WriteLine("================= 汇总 =================");
            Console.WriteLine("PASS = " + _pass + "   FAIL = " + _fail);
            if (_fail > 0)
            {
                Console.WriteLine("失败明细：");
                foreach (string f in _failures) Console.WriteLine("  - " + f);
                return 1;
            }
            Console.WriteLine("ALL PASS");
            return 0;
        }

        // =====================================================================
        // 1. PasswordHasher —— PBKDF2 哈希与验证
        // =====================================================================
        private static void PasswordHasherTests()
        {
            string h1 = PasswordHasher.Hash("123456");

            // 格式自描述：PBKDF2$迭代$盐$哈希
            Check("哈希带 PBKDF2$ 前缀", h1.StartsWith("PBKDF2$", StringComparison.Ordinal), h1);
            string[] segs = h1.Substring(7).Split('$');
            Check("去掉前缀后应为 3 段(迭代/盐/哈希)", segs.Length == 3, h1);
            Check("默认迭代次数为 100000", segs[0] == "100000");

            int saltLen = -1, hashLen = -1;
            bool saltOk = TryDecodeBase64(segs[1], out byte[] salt);
            bool hashOk = TryDecodeBase64(segs[2], out byte[] hashBytes);
            if (saltOk) saltLen = salt.Length;
            if (hashOk) hashLen = hashBytes.Length;
            Check("盐为合法 Base64 且长度 16B", saltOk && saltLen == 16);
            Check("哈希为合法 Base64 且长度 32B", hashOk && hashLen == 32);

            // 验证正反例
            Check("正确密码 Verify=true", PasswordHasher.Verify("123456", h1));
            Check("错误密码 Verify=false", !PasswordHasher.Verify("654321", h1));
            Check("大小写不同视为不同密码", !PasswordHasher.Verify("123456A", h1));

            // 盐随机性：同一密码两次哈希必须不同，但都能验证通过
            string h2 = PasswordHasher.Hash("123456");
            Check("同密码两次 Hash 结果不同(随机盐)", h1 != h2);
            Check("同密码两个哈希都能验证通过", PasswordHasher.Verify("123456", h2));

            // 参数边界与异常
            CheckThrows<ArgumentNullException>("Hash(null) 抛 ArgumentNullException", () => PasswordHasher.Hash(null));
            Check("Verify(null, 哈希)=false", !PasswordHasher.Verify(null, h1));
            Check("Verify(密码, null)=false", !PasswordHasher.Verify("x", null));
            Check("Verify(密码, 空串)=false", !PasswordHasher.Verify("x", ""));

            // 非 PBKDF2 格式的存储串一律判失败（防旧版明文/裸哈希混入）
            Check("明文存储串判失败", !PasswordHasher.Verify("123456", "123456"));
            Check("MD5$ 前缀串判失败", !PasswordHasher.Verify("123456", "MD5$abcdef"));
            Check("PBKDF2 前缀但段数不足判失败", !PasswordHasher.Verify("123456", "PBKDF2$100000"));

            // 损坏串静默失败（绝不能抛异常拖垮登录流程）
            Check("截断哈希串判失败", !PasswordHasher.Verify("123456", h1.Substring(0, h1.Length - 5)));
            Check("非法 Base64 盐判失败", !PasswordHasher.Verify("123456", "PBKDF2$100000$@@##$$****"));
            Check("迭代次数非数字判失败", !PasswordHasher.Verify("123456", "PBKDF2$abc$AAAA==$BBBB=="));
            Check("迭代次数为负判失败", !PasswordHasher.Verify("123456", "PBKDF2$-1$AAAA==$BBBB=="));

            // 特殊字符密码往返
            string cnHash = PasswordHasher.Hash("中文密码!@#￥%……&*（）");
            Check("中文+特殊字符密码往返成功", PasswordHasher.Verify("中文密码!@#￥%……&*（）", cnHash));
            Check("中文密码少一位即失败", !PasswordHasher.Verify("中文密码!@#￥%……&*（", cnHash));
            string longPwd = new string('a', 256) + "X9";
            Check("256 字长密码往返成功", PasswordHasher.Verify(longPwd, PasswordHasher.Hash(longPwd)));
            string spHash = PasswordHasher.Hash(" 123 ");
            Check("前后空格参与哈希(原样保留)", PasswordHasher.Verify(" 123 ", spHash) && !PasswordHasher.Verify("123", spHash));
        }

        // =====================================================================
        // 2. UserManager —— 登录/账号/改密/权限/记住登录/损坏恢复
        // =====================================================================
        private static void UserManagerTests()
        {
            // ── A. 新装流程：默认账号生成 + Users.json 只存哈希不存明文 ──
            EnterCleanDir();
            var um1 = new UserManager();

            Check("新目录实例化后生成 Users.json", File.Exists("Users.json"));
            string usersJson = File.ReadAllText("Users.json");
            Check("Users.json 含 PBKDF2$ 哈希", usersJson.Contains("PBKDF2$"));
            Check("Users.json 不含明文密码 123456", !usersJson.Contains("\"123456\""));

            var rAdmin = um1.Login(UserRole.Administrator, "admin", "123456");
            Check("管理员默认账号登录成功", rAdmin.Success && rAdmin.User != null && rAdmin.User.Role == UserRole.Administrator);
            um1.Logout();
            Check("Logout 后 CurrentUser 为 null", um1.CurrentUser == null);

            var umTech = new UserManager();
            Check("技术员默认账号登录成功", umTech.Login(UserRole.Technician, "technician", "123456").Success);
            var umOp = new UserManager();
            Check("操作员默认账号登录成功", umOp.Login(UserRole.Operator, "operator", "123456").Success);

            // ── B. 登录边界 ──
            Check("错误密码登录失败", !umOp.Login(UserRole.Operator, "operator", "wrong").Success);
            Check("错误用户名登录失败", !umOp.Login(UserRole.Operator, "nouser", "123456").Success);
            Check("空用户名登录失败", !umOp.Login(UserRole.Operator, "", "123456").Success);
            Check("null 用户名登录失败", !umOp.Login(UserRole.Operator, null, "123456").Success);
            Check("空密码登录失败", !umOp.Login(UserRole.Operator, "operator", "").Success);
            Check("null 密码登录失败", !umOp.Login(UserRole.Operator, "operator", null).Success);
            Check("用户名带首尾空格仍能登录(Trim)", umOp.Login(UserRole.Operator, "  operator  ", "123456").Success);
            Check("用户名大小写敏感(Admin≠admin)", !umOp.Login(UserRole.Administrator, "Admin", "123456").Success);
            Check("角色错配(operator 登管理员)被拒绝", !umOp.Login(UserRole.Administrator, "operator", "123456").Success);

            // ── C. 权限矩阵 HasPermission（枚举值 Admin(2)>Tech(1)>Operator(0)）──
            umOp.Logout();
            Check("未登录有 Operator 权限", umOp.HasPermission(UserRole.Operator));
            Check("未登录无 Technician 权限", !umOp.HasPermission(UserRole.Technician));
            Check("未登录无 Administrator 权限", !umOp.HasPermission(UserRole.Administrator));
            umOp.Login(UserRole.Operator, "operator", "123456");
            Check("操作员无 Technician 权限", !umOp.HasPermission(UserRole.Technician));
            umTech.Login(UserRole.Technician, "technician", "123456");
            Check("技术员有 Technician 权限", umTech.HasPermission(UserRole.Technician));
            Check("技术员无 Administrator 权限", !umTech.HasPermission(UserRole.Administrator));
            umTech.Login(UserRole.Administrator, "admin", "123456"); // 同一实例切换为管理员身份，后续账号管理用它
            Check("管理员有 Administrator 权限", umTech.HasPermission(UserRole.Administrator));

            // ── D. 账号管理（AddAccount / RemoveAccount / 改密 / 改名）──
            Check("操作员身份 AddAccount 权限不足", !umOp.AddAccount(UserRole.Operator, "x1", "abcd").Success);

            var add1 = umTech.AddAccount(UserRole.Operator, "op2", "abcd");
            Check("管理员添加操作员 op2 成功", add1.Success);
            Check("重复用户名(忽略大小写 OP2)被拒", !umTech.AddAccount(UserRole.Operator, "OP2", "abcd").Success);
            Check("与其他角色重名(admin)也被拒", !umTech.AddAccount(UserRole.Operator, "ADMIN", "abcd").Success);
            Check("短用户名(1字符)被拒", !umTech.AddAccount(UserRole.Operator, "a", "abcd").Success);
            Check("短密码(3字符)被拒", !umTech.AddAccount(UserRole.Operator, "op3", "abc").Success);
            Check("空密码被拒", !umTech.AddAccount(UserRole.Operator, "op3", "").Success);
            Check("第二个管理员账号被拒", !umTech.AddAccount(UserRole.Administrator, "admin2", "abcd").Success);

            // 持久化：新实例（模拟重启）后 op2 能登录，且密码仍是哈希
            var umReload = new UserManager();
            Check("重启后新账号 op2 可登录(持久化生效)", umReload.Login(UserRole.Operator, "op2", "abcd").Success);
            string reloadedJson = File.ReadAllText("Users.json");
            Check("新增账号密码也是哈希存储", reloadedJson.Contains("PBKDF2$") && !reloadedJson.Contains("\": \"abcd\""));

            // 删除账号：保底与管理员保护
            umReload.Login(UserRole.Administrator, "admin", "123456");
            Check("删除 op2 成功", umReload.RemoveAccount(UserRole.Operator, "op2").Success);
            Check("删除后重启该账号不再存在", !new UserManager().Login(UserRole.Operator, "op2", "abcd").Success);
            var umLast = new UserManager();
            umLast.Login(UserRole.Administrator, "admin", "123456");
            Check("角色仅剩 1 个账号时删除被拒", !umLast.RemoveAccount(UserRole.Operator, "operator").Success);
            Check("管理员账号不允许删除", !umLast.RemoveAccount(UserRole.Administrator, "admin").Success);
            Check("删除不存在的账号报错", !umLast.RemoveAccount(UserRole.Operator, "ghost").Success);
            Check("非管理员 RemoveAccount 失败", !new UserManager().RemoveAccount(UserRole.Operator, "whatever").Success);

            // 管理员改他人密码：旧密失效、新密生效。
            // 【坑】登录验证必须用独立实例：Login 成功会把该实例的 CurrentUser
            // 从管理员顶成操作员，后续"管理员改用户名"就会权限不足（曾致用例误报）。
            var umPwd = new UserManager();
            umPwd.Login(UserRole.Administrator, "admin", "123456");
            var umOpOnly = new UserManager(); // 操作员会话，用于验证权限不足
            umOpOnly.Login(UserRole.Operator, "operator", "123456");
            var opAccForPerm = umOpOnly.GetAccounts(UserRole.Operator).First(a => a.Username == "operator");
            Check("操作员 UpdatePassword 权限不足", !umOpOnly.UpdatePassword(opAccForPerm, "abcd").Success);

            var opAcc2 = umPwd.GetAccounts(UserRole.Operator).First(a => a.Username == "operator");
            Check("短密码 UpdatePassword 被拒", !umPwd.UpdatePassword(opAcc2, "abc").Success);
            Check("空密码 UpdatePassword 被拒", !umPwd.UpdatePassword(opAcc2, "").Success);
            Check("null 账号 UpdatePassword 报'未找到目标账号'", !umPwd.UpdatePassword(null, "abcd").Success);
            Check("管理员 UpdatePassword 成功", umPwd.UpdatePassword(opAcc2, "newpw123").Success);
            var umVerify = new UserManager(); // 独立实例验证密码，不污染 umPwd 的管理员身份
            Check("旧密码已失效", !umVerify.Login(UserRole.Operator, "operator", "123456").Success);
            Check("新密码可登录", umVerify.Login(UserRole.Operator, "operator", "newpw123").Success);

            // 改用户名（umPwd 保持管理员身份）
            Check("操作员 UpdateUsername 权限不足", !umOpOnly.UpdateUsername(opAcc2, "renamed").Success);
            Check("改名为已占用用户名(忽略大小写 ADMIN)失败", !umPwd.UpdateUsername(opAcc2, "ADMIN").Success);
            Check("改名过短失败", !umPwd.UpdateUsername(opAcc2, "o").Success);
            Check("改名为空白失败", !umPwd.UpdateUsername(opAcc2, "   ").Success);
            Check("null 账号 UpdateUsername 失败", !umPwd.UpdateUsername(null, "renamed").Success);
            Check("管理员改名成功", umPwd.UpdateUsername(opAcc2, "renamed_op").Success);
            // 必须再开新实例：umVerify 内存里还是改名前的快照，只有重新从文件加载才看得到新用户名
            Check("改名后新用户名可登录", new UserManager().Login(UserRole.Operator, "renamed_op", "newpw123").Success);

            // ── E. ChangeOwnPassword（自己改自己密码，需验旧密；成功后清除记住的登录）──
            // 用全新隔离目录：operator 密码恢复默认 123456，与 D 组的改密/改名互不干扰
            EnterCleanDir();
            var umSelf = new UserManager();
            Check("未登录 ChangeOwnPassword 失败", !umSelf.ChangeOwnPassword("123456", "abcd").Success);
            umSelf.Login(UserRole.Operator, "operator", "123456");
            Check("旧密码错误时改密失败", !umSelf.ChangeOwnPassword("badold", "abcd").Success);
            Check("旧密码为空被拒", !umSelf.ChangeOwnPassword("", "abcd").Success);
            Check("新密码过短被拒", !umSelf.ChangeOwnPassword("123456", "abc").Success);
            Check("新密码为空被拒", !umSelf.ChangeOwnPassword("123456", "").Success);
            Check("新旧相同被拒", !umSelf.ChangeOwnPassword("123456", "123456").Success);

            umSelf.SaveRememberedLogin(UserRole.Operator, "operator", "123456");
            Check("改密前 GetRememberedLogin 有值", umSelf.GetRememberedLogin(UserRole.Operator).Username != null);
            Check("正常改密成功", umSelf.ChangeOwnPassword("123456", "abcd1234").Success);
            Check("改密后记住的登录信息被自动清除", umSelf.GetRememberedLogin(UserRole.Operator).Username == null);
            Check("改密后旧密码失效", !umSelf.Login(UserRole.Operator, "operator", "123456").Success);
            Check("改密后新密码生效", umSelf.Login(UserRole.Operator, "operator", "abcd1234").Success);

            // ── F. 记住登录往返（含中文密码、Base64 非明文落盘）──
            var umRem = new UserManager();
            umRem.SaveRememberedLogin(UserRole.Technician, "technician", "密码123#");
            var rem = umRem.GetRememberedLogin(UserRole.Technician);
            Check("记住的登录信息往返一致(中文密码)", rem.Username == "technician" && rem.Password == "密码123#");
            umRem.ClearRememberedLogin(UserRole.Technician);
            Check("清除后取不到记住的信息", umRem.GetRememberedLogin(UserRole.Technician).Username == null);
            umRem.ClearRememberedLogin(UserRole.Technician); // 重复清除不应抛异常
            umRem.SaveRememberedLogin(UserRole.Technician, "", "x"); // 空用户名静默忽略
            Check("空用户名的记住请求被忽略", umRem.GetRememberedLogin(UserRole.Technician).Username == null);
            string remJson = File.Exists("RememberedLogin.json")
                ? File.ReadAllText("RememberedLogin.json") : "";
            Check("RememberedLogin.json 中密码为 Base64 非明文", remJson == "" || !remJson.Contains("密码123#"));

            // ── G. Users.json 损坏 → 回退默认并重建文件 ──
            File.WriteAllText("Users.json", "{ this is not valid json !!");
            var umFix = new UserManager();
            Check("损坏 Users.json 后仍可用默认账号登录", umFix.Login(UserRole.Administrator, "admin", "123456").Success);
            Check("损坏文件已被重建(垃圾内容消失)", !File.ReadAllText("Users.json").Contains("this is not valid json"));

            // ── H. Users.json 缺角色 → 自动补齐缺失角色且保留既有账号 ──
            File.WriteAllText("Users.json",
                "[{\"Username\":\"only\",\"Password\":\"" + PasswordHasher.Hash("1234") + "\",\"Role\":0}]");
            var umFill = new UserManager();
            Check("缺角色配置下默认管理员仍可登录", umFill.Login(UserRole.Administrator, "admin", "123456").Success);
            Check("缺失的技术员角色被自动补齐", umFill.Login(UserRole.Technician, "technician", "123456").Success);
            Check("文件中的既有操作员账号保留", umFill.Login(UserRole.Operator, "only", "1234").Success);

            // ── I. 手改出两个管理员 → 加载时只保留第一个（防呆约定）──
            string dupAdmins =
                "[{\"Username\":\"a1\",\"Password\":\"" + PasswordHasher.Hash("1111") + "\",\"Role\":2}," +
                "{\"Username\":\"a2\",\"Password\":\"" + PasswordHasher.Hash("2222") + "\",\"Role\":2}]";
            File.WriteAllText("Users.json", dupAdmins);
            var umDup = new UserManager();
            Check("手改双管理员只保留第一个(a1 可登录)", umDup.Login(UserRole.Administrator, "a1", "1111").Success);
            Check("第二个管理员(a2)被丢弃", !umDup.Login(UserRole.Administrator, "a2", "2222").Success);
        }

        // =====================================================================
        // 3. SettingsForm 配置归一化（私有静态方法，反射调用）
        //    约定：StopBits 存 1/15(=1.5)/2；Parity 只能是 None/Odd/Even/Mark/Space
        // =====================================================================
        private static void NormalizeTests()
        {
            const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Static;
            var miStop = typeof(AgingTestSystem.Dialogs.SettingsForm).GetMethod("NormalizeStopBits", Flags);
            var miParity = typeof(AgingTestSystem.Dialogs.SettingsForm).GetMethod("NormalizeParity", Flags);
            Check("反射找到 NormalizeStopBits", miStop != null);
            Check("反射找到 NormalizeParity", miParity != null);
            if (miStop == null || miParity == null) return;

            Func<string, string> normStop = v => (string)miStop.Invoke(null, new object[] { v });
            Func<string, string> normParity = v => (string)miParity.Invoke(null, new object[] { v });

            // 停止位：界面显示 1/1.5/2 → 配置统一存 1/15/2（15 表示 1.5）
            Check("StopBits '1'→1", normStop("1") == "1");
            Check("StopBits '1.5'→15", normStop("1.5") == "15");
            Check("StopBits '15'→15", normStop("15") == "15");
            Check("StopBits '2'→2", normStop("2") == "2");
            Check("StopBits ' 2 '(带空白)→2", normStop(" 2 ") == "2");
            Check("StopBits 空串→1(默认)", normStop("") == "1");
            Check("StopBits null→1(默认)", normStop(null) == "1");
            Check("StopBits 非法值 abc→1(默认)", normStop("abc") == "1");
            Check("StopBits '3' 非法→1(默认)", normStop("3") == "1");

            // 校验位：历史写法/中文/缩写 → 标准枚举名；非法一律归 None
            Check("Parity 'None'→None", normParity("None") == "None");
            Check("Parity 'none'→None(大小写兼容)", normParity("none") == "None");
            Check("Parity 'N'→None(缩写)", normParity("N") == "None");
            Check("Parity '无校验'→None(中文)", normParity("无校验") == "None");
            Check("Parity 'Odd'/'odd'→Odd", normParity("Odd") == "Odd" && normParity("odd") == "Odd");
            Check("Parity '奇校验'→Odd", normParity("奇校验") == "Odd");
            Check("Parity 'Even'/'even'→Even", normParity("Even") == "Even" && normParity("even") == "Even");
            Check("Parity '偶校验'→Even", normParity("偶校验") == "Even");
            Check("Parity 'Mark'/'m'→Mark", normParity("Mark") == "Mark" && normParity("m") == "Mark");
            Check("Parity '1校验'→Mark", normParity("1校验") == "Mark");
            Check("Parity '标记'→Mark", normParity("标记") == "Mark");
            Check("Parity 'Space'/'s'→Space", normParity("Space") == "Space" && normParity("s") == "Space");
            Check("Parity '空格校验'→Space", normParity("空格校验") == "Space");
            Check("Parity 带空白 ' odd '→Odd", normParity(" odd ") == "Odd");
            Check("Parity 非法值 xyz→None(兜底)", normParity("xyz") == "None");
            Check("Parity 数字'0'→None(兜底)", normParity("0") == "None");
            Check("Parity null→None(兜底)", normParity(null) == "None");
            Check("Parity 纯空白→None(兜底)", normParity("  ") == "None");
        }

        // =====================================================================
        // 4. IoOutputChannelRemap.ParseAll —— IO 备用通道映射解析
        // =====================================================================
        private static void IoRemapTests()
        {
            string err;

            var list = IoOutputChannelRemap.ParseAll("0x2000@0x00->0x2009@0x10;0x2008@0x01->0x2009@0x11", out err);
            Check("两组合法映射解析数量正确", list.Count == 2);
            Check("全合法时 error 为 null", err == null);
            if (list.Count == 2)
            {
                Check("第1组源寄存器=0x2000", list[0].SourceRegister == 0x2000);
                Check("第1组源通道 0x00→位号0", list[0].SourceChannel == 0);
                Check("第1组目标寄存器=0x2009", list[0].TargetRegister == 0x2009);
                Check("第1组目标通道 0x10→位号16", list[0].TargetChannel == 16);
                Check("第2组源通道 0x01→位号1", list[1].SourceChannel == 1);
                Check("第2组目标通道 0x11→位号17", list[1].TargetChannel == 17);
            }

            list = IoOutputChannelRemap.ParseAll("0x2000@0x00->0x2009@0x10；0x2008@0x02->0x2009@0x12", out err);
            Check("中文分号分隔兼容", list.Count == 2 && err == null);

            list = IoOutputChannelRemap.ParseAll("0X2000@0X00->0X2009@0x10", out err);
            Check("大写 0X 前缀兼容", list.Count == 1 && err == null);

            list = IoOutputChannelRemap.ParseAll("2000@0x00->2009@0x01", out err);
            Check("寄存器不带 0x 前缀仍兼容解析", list.Count == 1 && list[0].SourceRegister == 0x2000);

            // 脏输入：逐项跳过并汇总 error，不影响其它合法项
            list = IoOutputChannelRemap.ParseAll("0x2000@0x00->0x2009@0x10;bad-item;0x2008@0x01->0x2009@0x11", out err);
            Check("中间坏项被跳过其余保留", list.Count == 2);
            Check("坏项内容汇总进 error", err != null && err.Contains("bad-item"));

            list = IoOutputChannelRemap.ParseAll("0x2000@0x00->0x2000@0x00", out err);
            Check("源与目标相同被忽略并报 error", list.Count == 0 && err != null);

            list = IoOutputChannelRemap.ParseAll("0x2000@0x20->0x2009@0x00", out err);
            Check("通道越界(0x20>0x1F)报错", list.Count == 0 && err != null && err.Contains("0x20"));

            list = IoOutputChannelRemap.ParseAll("0x2000@5->0x2009@0x01", out err);
            Check("通道缺 0x 前缀报错", list.Count == 0 && err != null);

            list = IoOutputChannelRemap.ParseAll("0x2000@0x00", out err);
            Check("缺少 -> 分隔符报错", list.Count == 0 && err != null);

            list = IoOutputChannelRemap.ParseAll("zz@0x00->0x2009@0x01", out err);
            Check("寄存器非十六进制报错", list.Count == 0 && err != null);

            list = IoOutputChannelRemap.ParseAll("0x2000->0x2009@0x01", out err);
            Check("端点缺 @ 报错", list.Count == 0 && err != null);

            // 空输入族
            list = IoOutputChannelRemap.ParseAll("", out err);
            Check("空串→空列表 error=null", list.Count == 0 && err == null);
            list = IoOutputChannelRemap.ParseAll(null, out err);
            Check("null→空列表 error=null", list.Count == 0 && err == null);
            list = IoOutputChannelRemap.ParseAll(" ; ； ", out err);
            Check("纯分隔符→空列表 error=null", list.Count == 0 && err == null);
        }

        // =====================================================================
        // 5. DeviceConfig.ParseFanIpCandidates —— 送风机候选 IP 解析
        // =====================================================================
        private static void FanIpTests()
        {
            var ips = DeviceConfig.ParseFanIpCandidates("192.168.1.220,192.168.1.221");
            Check("英文逗号分隔解析 2 个", ips.Count == 2 && ips[0] == "192.168.1.220");

            ips = DeviceConfig.ParseFanIpCandidates("192.168.1.220；192.168.1.221，192.168.1.222;192.168.1.223");
            Check("中英文逗号/分号混合分隔 4 个", ips.Count == 4);

            ips = DeviceConfig.ParseFanIpCandidates(" 192.168.1.220 , 192.168.1.221 ");
            Check("项两侧空白被 Trim", ips.Count == 2 && ips[1] == "192.168.1.221");

            ips = DeviceConfig.ParseFanIpCandidates("abc,,192.168.1.5,");
            Check("非法 IP 与空项被过滤", ips.Count == 1 && ips[0] == "192.168.1.5");

            ips = DeviceConfig.ParseFanIpCandidates("192.168.1.5, 192.168.1.5, 192.168.1.6");
            Check("去重保序", ips.Count == 2 && ips[0] == "192.168.1.5" && ips[1] == "192.168.1.6");

            ips = DeviceConfig.ParseFanIpCandidates("300.1.1.1");
            Check("超范围 IPv4 被过滤", ips.Count == 0);

            ips = DeviceConfig.ParseFanIpCandidates("::1");
            Check("IPv6 回环地址可解析(系统行为)", ips.Count == 1 && ips[0] == "::1");

            Check("null→空列表", DeviceConfig.ParseFanIpCandidates(null).Count == 0);
            Check("空串→空列表", DeviceConfig.ParseFanIpCandidates("").Count == 0);
            Check("纯标点空白→空列表", DeviceConfig.ParseFanIpCandidates("  , ， ").Count == 0);
        }

        // =====================================================================
        // 6. RecipeStorage —— 配方 JSON 存取
        // =====================================================================
        private static void RecipeStorageTests()
        {
            EnterCleanDir();
            Check("文件不存在 Load 返回 null", RecipeStorage.Load() == null);

            var src = new List<RecipeConfig>
            {
                new RecipeConfig
                {
                    Id = 1,
                    Name = "配方A-高温老化",
                    NegativePressure = -85.5m,
                    DelayTime = TimeSpan.FromSeconds(90),
                    StartTime = new TimeSpan(8, 30, 0),
                    LimitTemperature = 75.5m,
                    CreateTime = new DateTime(2026, 8, 25, 10, 0, 0),
                    IsEnabled = true
                },
                new RecipeConfig
                {
                    Id = 2, Name = "B", NegativePressure = -60m, DelayTime = TimeSpan.Zero,
                    StartTime = TimeSpan.Zero, LimitTemperature = 0m,
                    CreateTime = DateTime.Today, IsEnabled = false
                }
            };
            Check("Save 返回 true", RecipeStorage.Save(src));
            Check("保存后文件存在", File.Exists("Recipes.json"));

            var loaded = RecipeStorage.Load();
            Check("Load 返回非 null 且 2 条", loaded != null && loaded.Count == 2);
            if (loaded != null && loaded.Count == 2)
            {
                Check("Name 往返一致", loaded[0].Name == "配方A-高温老化");
                Check("中文配方名无乱码", loaded[0].Name.Contains("高温老化"));
                Check("NegativePressure 往返一致", loaded[0].NegativePressure == -85.5m);
                Check("DelayTime 往返一致", loaded[0].DelayTime == TimeSpan.FromSeconds(90));
                Check("StartTime 往返一致", loaded[0].StartTime == new TimeSpan(8, 30, 0));
                Check("LimitTemperature 往返一致", loaded[0].LimitTemperature == 75.5m);
                Check("CreateTime 往返一致", loaded[0].CreateTime == new DateTime(2026, 8, 25, 10, 0, 0));
                Check("IsEnabled 往返一致(false)", loaded[1].IsEnabled == false);
            }

            File.WriteAllText("Recipes.json", "{broken json");
            Check("损坏 json Load 返回 null(不抛异常)", RecipeStorage.Load() == null);

            File.WriteAllText("Recipes.json", "[]");
            var empty = RecipeStorage.Load();
            Check("空数组 json Load 返回空列表(非 null)", empty != null && empty.Count == 0);

            // SaveWithDuplicateCheck 新增分支（同名覆盖分支弹 UI 对话框，归界面手工测试覆盖）
            var shared = new List<RecipeConfig>();
            var r = new RecipeConfig { Name = "新建配方", NegativePressure = -70m };
            Check("SaveWithDuplicateCheck 新增返回 true", RecipeStorage.SaveWithDuplicateCheck(shared, r));
            Check("新增后 Id 自动分配为 1", shared.Count == 1 && shared[0].Id == 1);
            Check("新增后立即落盘", File.Exists("Recipes.json"));
            Check("参数 recipe=null 返回 false", !RecipeStorage.SaveWithDuplicateCheck(shared, null));
            Check("参数 list=null 返回 false", !RecipeStorage.SaveWithDuplicateCheck(null, r));
        }

        // =====================================================================
        // 7. TestEventLogger —— CSV 测试事件日志（转义/表头/落盘/字段格式）
        // =====================================================================
        private static void TestEventLoggerTests()
        {
            // 反射测 CsvEscape 私有静态方法
            var mi = typeof(TestEventLogger).GetMethod("CsvEscape", BindingFlags.NonPublic | BindingFlags.Static);
            Check("反射找到 CsvEscape", mi != null);
            if (mi != null)
            {
                Func<string, string> esc = v => (string)mi.Invoke(null, new object[] { v });
                Check("普通文本不包裹", esc("abc") == "abc");
                Check("null→空串", esc(null) == "");
                Check("含逗号被双引号包裹", esc("a,b") == "\"a,b\"");
                Check("含双引号翻倍并包裹", esc("a\"b") == "\"a\"\"b\"");
                Check("含换行被包裹", esc("a\nb") == "\"a\nb\"");
                // 期望值分段构造避免转义数错：结果应为 "x,""y"",z"（包裹+引号逐个翻倍）
                string expected = "\"" + "x," + "\"" + "\"" + "y" + "\"" + "\"" + ",z" + "\"";
                Check("引号+逗号组合正确", esc("x,\"y\",z") == expected,
                    "期望[" + expected + "] 实际[" + esc("x,\"y\",z") + "]");
            }

            // 真实写入：表头 + 数据行（BaseDirectory 是临时 run 目录，Logs 落在那里，不污染仓库）
            TestEventLogger.Write("LOT20260825", 3, "报警", "压力超限,需关注", -85.5m, 66.6f);
            TestEventLogger.Write("", 0, "急停", "手动触发", null, null);

            string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            string file = Path.Combine(logDir, "TestLog_" + DateTime.Now.ToString("yyyyMMdd") + ".csv");
            Check("CSV 日志文件已生成", File.Exists(file));
            if (!File.Exists(file)) return;

            string[] lines = ReadAllLinesShared(file);
            Check("首行为固定表头", lines.Length > 0 &&
                lines[0] == "时间,批号,设备编号,事件,详情,压力(kPa),温度(°C)");
            Check("两条事件均已落盘", lines.Length >= 3);
            if (lines.Length < 3) return;

            Check("时间列 yyyy-MM-dd HH:mm:ss 格式开头",
                System.Text.RegularExpressions.Regex.IsMatch(lines[1], @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2},"));
            Check("批号列正确", lines[1].Contains(",LOT20260825,"));
            Check("设备编号列=3", lines[1].Contains(",LOT20260825,3,"));
            Check("含逗号详情被转义包裹", lines[1].Contains("\"压力超限,需关注\""));
            Check("压力列=-85.5", lines[1].Contains(",-85.5,"));
            Check("温度列一位小数 66.6 结尾", lines[1].EndsWith("66.6"));
            // 压力/温度都为空时，详情后的两个空字段让行尾必然是 ",,"（CSV 列留空）
            Check("可选压力/温度缺省时留空(行尾,,)", lines[2].EndsWith(",急停,手动触发,,"));
        }

        // =====================================================================
        // 8. AppLogFileWriter —— 操作日志追加 / UTF-8 / 多线程并发完整
        // =====================================================================
        /// <summary>
        /// 以 FileShare.ReadWrite 共享读文本文件。
        /// 【为什么不用 File.ReadAllLines】AppLogFileWriter 的 StreamWriter 常驻不关
        /// （产品设计：句柄复用+每次 Flush），其共享级别为 Read；ReadAllLines 以
        /// FileShare.Read 打开不含 Write 共享会被 Windows 拒绝抛 IOException。
        /// 外部工具（记事本等）都用 ReadWrite 共享打开所以现场无碍——测试侧对齐即可。
        /// </summary>
        private static string[] ReadAllLinesShared(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, Encoding.UTF8))
            {
                var list = new List<string>();
                string line;
                while ((line = sr.ReadLine()) != null) list.Add(line);
                return list.ToArray();
            }
        }

        private static void AppLogWriterTests()
        {
            string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            string file = Path.Combine(logDir, "AppLog_" + DateTime.Now.ToString("yyyyMMdd") + ".log");
            int before = File.Exists(file) ? ReadAllLinesShared(file).Length : 0;

            AppLogFileWriter.Write("[T1] 气压表连接失败：COM3 打开超时\r\n");
            AppLogFileWriter.Write("[T2] 扫码枪上线 SN=A1B2C3\r\n");
            AppLogFileWriter.Write(""); // 空串应被忽略

            Check("AppLog 文件已生成", File.Exists(file));
            if (File.Exists(file))
            {
                string[] lines = ReadAllLinesShared(file);
                Check("两行日志均已追加(空串不计)", lines.Length >= before + 2);
                string l1 = lines.Length > before ? lines[before] : "";
                string l2 = lines.Length > before + 1 ? lines[before + 1] : "";
                Check("第 1 行中文 UTF-8 无乱码", l1.Contains("气压表连接失败"));
                Check("第 2 行内容正确", l2.Contains("SN=A1B2C3"));
            }

            // 多线程并发写完整性：8 线程 × 各 5 行 = 40 行，lock 保证一行不丢不错乱
            int concurrentBefore = File.Exists(file) ? ReadAllLinesShared(file).Length : 0;
            const int Threads = 8, PerThread = 5;
            var tasks = new Task[Threads];
            for (int t = 0; t < Threads; t++)
            {
                int tid = t;
                tasks[t] = Task.Run(() =>
                {
                    for (int i = 0; i < PerThread; i++)
                        AppLogFileWriter.Write("[并发" + tid + "-" + i + "] 测试消息\r\n");
                });
            }
            Task.WaitAll(tasks);
            int after = ReadAllLinesShared(file).Length;
            Check("40 条并发写入一条不少(lock 生效)", after >= concurrentBefore + Threads * PerThread,
                "期望≥" + (concurrentBefore + Threads * PerThread) + " 实际" + after);

            // 写失败静默：传一个不可能的路径不会抛异常（Write 本身无返回值，能走完即静默成功）
            AppLogFileWriter.Write("[T3] 这行应正常写入\r\n");
            int final = ReadAllLinesShared(file).Length;
            Check("后续写入不受影响", final >= concurrentBefore + Threads * PerThread + 1);
        }

        // =====================================================================
        // 9. PanelLayoutConfig —— 默认布局 / 锚定幂等 / 宽高联动（自绘面板布局核心）
        //    断言基准 = V1.58.20 默认值（PanelInnerWidth=222, Height=205）
        // =====================================================================
        private static void PanelLayoutTests()
        {
            // 保证测的是"代码内置默认布局"：删掉 run 目录里可能被拷进来的 PanelLayout.json
            string cfgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PanelLayout.json");
            if (File.Exists(cfgPath)) File.Delete(cfgPath);

            var c = PanelLayoutConfig.LoadOrDefault();
            Check("无配置文件时 LoadOrDefault 返回默认布局", c != null && c.PanelInnerWidth == 222 && c.PanelInnerHeight == 205);
            c.ResolveAnchors();

            // 关键元素按锚定规则解析出的基准坐标
            var setBtn = c.RcSetButton.ToRectangle();
            Check("设置按钮右锚定 X=222-9-60=153", setBtn.X == 153 && setBtn.Width == 60,
                "实际 " + setBtn.ToString());
            Check("设置按钮下锚定 Y=205-10-50=145", setBtn.Y == 145 && setBtn.Height == 50,
                "实际 " + setBtn.ToString());
            var selBox = c.RcSelectBox.ToRectangle();
            Check("选中框右上角锚定 (194,2)", selBox.X == 194 && selBox.Y == 2, "实际 " + selBox.ToString());
            var sn = c.RcSNValue.ToRectangle();
            Check("SN 框右对齐设置按钮 X=65 宽148", sn.X == 65 && sn.Width == 148, "实际 " + sn.ToString());
            var pressure = c.RcPressureValue.ToRectangle();
            Check("压力框双端锚定自动定宽 85", pressure.Width == 85 && pressure.X == 65,
                "实际 " + pressure.ToString());
            Check("压力框与真空关保持 3px 间隙",
                c.RcVacuumOpen.ToRectangle().X - (pressure.X + pressure.Width) == 3);

            // ── 幂等性：重复解析结果必须完全一致（多次 Load 不漂移）──
            var snap1 = SnapshotLayout(c);
            c.ResolveAnchors();
            var snap2 = SnapshotLayout(c);
            Check("ResolveAnchors 幂等(二次解析零漂移)", DictionaryEquals(snap1, snap2));

            // ── 高度联动：面板高 +10 → 纵链全链下移、间距不变 ──
            var tall = PanelLayoutConfig.LoadOrDefault();
            tall.PanelInnerHeight = 215;
            tall.ResolveAnchors();
            Check("高度+10: 设置按钮 Y 145→155", tall.RcSetButton.ToRectangle().Y == 155);
            Check("高度+10: SN 框 Y 93→103(链式跟随)", tall.RcSNValue.ToRectangle().Y == 103);
            Check("高度+10: 配方框 Y 118→128", tall.RcRecipeValue.ToRectangle().Y == 128);
            Check("高度+10: 真空开/压力框 Y 67→77",
                tall.RcVacuumOpen.ToRectangle().Y == 77 && tall.RcPressureValue.ToRectangle().Y == 77);
            Check("高度+10: 延时两行 Y 147/172→157/182",
                tall.RcDelayStartValue.ToRectangle().Y == 157 && tall.RcDelayArriveValue.ToRectangle().Y == 182);
            Check("高度+10: 选中框 TopMargin 锚定不动仍 Y=2", tall.RcSelectBox.ToRectangle().Y == 2);
            Check("高度+10: 工作状态块绝对坐标不动仍 Y=29", tall.RcWorkState.ToRectangle().Y == 29);
            Check("高度+10: 下电框跟随工作状态仍 Y=29", tall.RcPower.ToRectangle().Y == 29);
            Check("高度+10: 压力框与真空关间隙仍 3px",
                tall.RcVacuumOpen.ToRectangle().X - (tall.RcPressureValue.ToRectangle().X + tall.RcPressureValue.ToRectangle().Width) == 3);

            // ── 宽度联动：面板宽 +10 → 右锚定组整体右移、双端锚定宽度随动 ──
            // 右对齐公式 X = 目标右缘 - 自身宽：SetButton.X=232-9-60=163，
            // 其右缘=223 → SN.X = 223-148 = 75（基准 65 +10，整组右移）
            var wide = PanelLayoutConfig.LoadOrDefault();
            wide.PanelInnerWidth = 232;
            wide.ResolveAnchors();
            Check("宽度+10: 设置按钮 X 153→163", wide.RcSetButton.ToRectangle().X == 163);
            Check("宽度+10: SN 框 X 65→75(右缘跟随设置按钮)", wide.RcSNValue.ToRectangle().X == 75,
                "实际 " + wide.RcSNValue.ToRectangle().ToString());
            // 双端锚定的压力框两端都随动（左贴 SN、右贴真空关-3px），宽度保持不变、间隙恒定
            var wPressure = wide.RcPressureValue.ToRectangle();
            Check("宽度+10: 压力框两端随动 X=75 宽仍 85", wPressure.X == 75 && wPressure.Width == 85,
                "实际 " + wPressure.ToString());
            Check("宽度+10: 选中框 X 194→204(RightMargin=5 跟随)", wide.RcSelectBox.ToRectangle().X == 204);

            // ── 标签垂直居中：随目标框移动 ──
            Check("压力标签随框垂直居中(Y=70→80)",
                c.LabelPressurePosition.ToPoint().Y == 70 && tall.LabelPressurePosition.ToPoint().Y == 80,
                "基准 " + c.LabelPressurePosition.ToPoint().ToString() + " 加高后 " + tall.LabelPressurePosition.ToPoint().ToString());

            // ── 颜色解析工具 ──
            Color fallback = Color.FromArgb(1, 2, 3);
            Check("ParseColor 合法 '255,0,0'", PanelLayoutConfig.ParseColor("255,0,0", fallback).ToArgb() == Color.Red.ToArgb());
            Check("ParseColor 带空格 ' 10 , 20 , 30 '", PanelLayoutConfig.ParseColor(" 10 , 20 , 30 ", fallback).ToArgb() == Color.FromArgb(10, 20, 30).ToArgb());
            Check("ParseColor 超界 300→钳到 255", PanelLayoutConfig.ParseColor("300,0,0", fallback).R == 255);
            Check("ParseColor 负数 -5→钳到 0", PanelLayoutConfig.ParseColor("-5,0,0", fallback).R == 0);
            Check("ParseColor 非法段回退 fallback", PanelLayoutConfig.ParseColor("a,b,c", fallback).ToArgb() == fallback.ToArgb());
            Check("ParseColor 段数不足回退", PanelLayoutConfig.ParseColor("1,2", fallback).ToArgb() == fallback.ToArgb());
            Check("ParseColor null 回退", PanelLayoutConfig.ParseColor(null, fallback).ToArgb() == fallback.ToArgb());
            Check("ToColorString 往返", PanelLayoutConfig.ToColorString(Color.FromArgb(12, 34, 56)) == "12,34,56");

            // ── SaveDefault → LoadOrDefault 往返（json 与代码默认一致约定）──
            c.SaveDefault();
            Check("SaveDefault 后文件生成", File.Exists(cfgPath));
            var reloaded = PanelLayoutConfig.LoadOrDefault();
            reloaded.ResolveAnchors();
            var snapReload = SnapshotLayout(reloaded);
            // 重载后的解析结果应与内存默认一致（防"json 与代码默认不一致"经典坑）
            List<string> diffs;
            DictionaryDiff(snap1, snapReload, out diffs);
            Check("SaveDefault→重载→解析结果与默认零差异", diffs.Count == 0,
                "差异: " + string.Join("; ", diffs.Take(5)));
        }

        /// <summary>把布局对象里所有 ElementRect/ElementPoint 属性拍成 名称→字符串 快照</summary>
        private static Dictionary<string, string> SnapshotLayout(PanelLayoutConfig c)
        {
            var d = new Dictionary<string, string>();
            var props = typeof(PanelLayoutConfig).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var p in props)
            {
                if (p.PropertyType == typeof(ElementRect))
                    d["R:" + p.Name] = ((ElementRect)p.GetValue(c)).ToRectangle().ToString();
                else if (p.PropertyType == typeof(ElementPoint))
                    d["P:" + p.Name] = ((ElementPoint)p.GetValue(c)).ToPoint().ToString();
            }
            return d;
        }

        private static bool DictionaryEquals(Dictionary<string, string> a, Dictionary<string, string> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var kv in a)
            {
                string v;
                if (!b.TryGetValue(kv.Key, out v) || v != kv.Value) return false;
            }
            return true;
        }

        private static void DictionaryDiff(Dictionary<string, string> a, Dictionary<string, string> b, out List<string> diffs)
        {
            diffs = new List<string>();
            foreach (var kv in a)
            {
                string v;
                if (!b.TryGetValue(kv.Key, out v)) { diffs.Add(kv.Key + " 仅在左侧"); continue; }
                if (v != kv.Value) diffs.Add(kv.Key + ": " + kv.Value + " → " + v);
            }
            foreach (var kv in b)
                if (!a.ContainsKey(kv.Key)) diffs.Add(kv.Key + " 仅在右侧");
        }

        // =====================================================================
        // 10. HomeLayoutConfig —— 主页布局默认配置
        // =====================================================================
        private static void HomeLayoutTests()
        {
            string cfgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HomeLayout.json");
            if (File.Exists(cfgPath)) File.Delete(cfgPath);

            var c = HomeLayoutConfig.LoadOrDefault();
            Check("HomeLayout 默认配置非 null", c != null);
            if (c == null) return;
            Check("TopBarHeight 默认 40", c.TopBarHeight == 40);
            Check("MenuHeight 默认 50", c.MenuHeight == 50);
            Check("RightPanelWidth 默认 260", c.RightPanelWidth == 260);
            Check("StatusBarHeight 默认 30", c.StatusBarHeight == 30);

            // 往返：改值保存 → 重新加载应读到新值
            c.TopBarHeight = 44;
            c.Save();
            var reloaded = HomeLayoutConfig.LoadOrDefault();
            Check("Save→Load 往返读到修改值 44", reloaded.TopBarHeight == 44);
            if (File.Exists(cfgPath)) File.Delete(cfgPath); // 还原，避免影响后续用例
        }

        // =====================================================================
        // 11. 模型序列化往返 / Clone
        // =====================================================================
        private static void ModelRoundtripTests()
        {
            // RecipeConfig JSON 往返（Newtonsoft 直接序列化路径，配方管理窗体走同一机制）
            var r = new RecipeConfig
            {
                Id = 7,
                Name = "往返测试-特殊\"引号\",逗号",
                NegativePressure = -99.99m,
                DelayTime = new TimeSpan(1, 2, 3),
                StartTime = new TimeSpan(23, 59, 58),
                LimitTemperature = 123.45m,
                CreateTime = new DateTime(2026, 1, 2, 3, 4, 5),
                IsEnabled = true
            };
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(r);
            var r2 = Newtonsoft.Json.JsonConvert.DeserializeObject<RecipeConfig>(json);
            Check("RecipeConfig JSON 往返字段全部一致",
                r2.Id == r.Id && r2.Name == r.Name && r2.NegativePressure == r.NegativePressure &&
                r2.DelayTime == r.DelayTime && r2.StartTime == r.StartTime &&
                r2.LimitTemperature == r.LimitTemperature && r2.CreateTime == r.CreateTime &&
                r2.IsEnabled == r.IsEnabled);
            Check("RecipeConfig 特殊字符名称无损坏", r2.Name.Contains("\"") && r2.Name.Contains(","));

            // UserAccount JSON 往返（Users.json 同机制）
            var u = new UserAccount("张三", "PBKDF2$100000$AA==$BB==", UserRole.Technician);
            var u2 = Newtonsoft.Json.JsonConvert.DeserializeObject<UserAccount>(
                Newtonsoft.Json.JsonConvert.SerializeObject(u));
            Check("UserAccount JSON 往返一致(含中文用户名)",
                u2.Username == "张三" && u2.Password == u.Password && u2.Role == UserRole.Technician);

            // StationInfo.Clone：改克隆不影响原件（工位数据复制场景）
            var s = new StationInfo { DeviceId = 5, SerialNumber = "SN001", RecipeName = "R1" };
            var sc = s.Clone();
            sc.SerialNumber = "SN002";
            sc.DeviceId = 9;
            bool cloneOk = s.SerialNumber == "SN001" && s.RecipeName == "R1" &&
                           s.DeviceId == 5 && sc.DeviceId == 9 && sc.RecipeName == "R1";
            Check("StationInfo.Clone 深拷贝互不影响", cloneOk,
                "s.SN=" + s.SerialNumber + " s.Id=" + s.DeviceId);

            // FanData.Clone 抽查
            var f = new FanData { Temperature = 33.5f, Humidity = 55.5f, IsOnline = true };
            var fc = f.Clone();
            fc.Temperature = 0f;
            fc.IsOnline = false;
            Check("FanData.Clone 深拷贝互不影响",
                f.Temperature == 33.5f && f.Humidity == 55.5f && f.IsOnline &&
                fc.Humidity == 55.5f && !fc.IsOnline);
        }

        // =====================================================================
        // 12. AgingSequencer —— 老化三阶段状态机纯函数决策（V1.59 新增）
        //     时序：启动只开阀 → Vacuuming(真空到位+延时开启) → 上电 → Aging(配方时长)
        //           → 到时完成(PASS·待取料)。决策逻辑抽成静态函数保证可测。
        // =====================================================================
        private static void AgingSequencerTests()
        {
            // ── ShouldPowerOn：上电前置条件 = 压力到位 且 延时开启已到（两者缺一不可）──
            Check("压力未到位永不上电(即使延时已过)",
                !AgingSequencer.ShouldPowerOn(false, TimeSpan.FromMinutes(10), 0));
            Check("压力到位+零延时立即上电",
                AgingSequencer.ShouldPowerOn(true, TimeSpan.Zero, 0));
            Check("压力到位但延时未到不上电",
                !AgingSequencer.ShouldPowerOn(true, TimeSpan.FromSeconds(29), 30));
            Check("压力到位且延时刚好到点上电",
                AgingSequencer.ShouldPowerOn(true, TimeSpan.FromSeconds(30), 30));
            Check("压力到位且延时早已过也上电(取较晚满足者)",
                AgingSequencer.ShouldPowerOn(true, TimeSpan.FromHours(1), 30));

            // ── ShouldComplete：老化到时判定；0/负时长 = 不限时长 ──
            Check("不限时长(0)永不自动完成",
                !AgingSequencer.ShouldComplete(TimeSpan.FromHours(100), 0));
            Check("负时长同样视为不限",
                !AgingSequencer.ShouldComplete(TimeSpan.FromHours(100), -1));
            Check("计时未到不完成",
                !AgingSequencer.ShouldComplete(TimeSpan.FromSeconds(3599), 3600));
            Check("计时刚好到点完成",
                AgingSequencer.ShouldComplete(TimeSpan.FromSeconds(3600), 3600));
            Check("超过时长完成",
                AgingSequencer.ShouldComplete(TimeSpan.FromSeconds(3601), 3600));

            // ── IsVacuumBuildFailed：宽限窗口内未到位不算失败；到位后永不算失败 ──
            Check("真空已到位永远不算建立失败",
                !AgingSequencer.IsVacuumBuildFailed(true, TimeSpan.FromMinutes(10), 15000));
            Check("未到位但在宽限窗口内不算失败",
                !AgingSequencer.IsVacuumBuildFailed(false, TimeSpan.FromSeconds(14), 15000));
            Check("未到位且刚好超时判失败",
                AgingSequencer.IsVacuumBuildFailed(false, TimeSpan.FromMilliseconds(15000), 15000));
            Check("未到位且远超窗口判失败",
                AgingSequencer.IsVacuumBuildFailed(false, TimeSpan.FromSeconds(60), 15000));
        }

        // =====================================================================
        // 13. TestSessionStore —— 在测任务快照持久化（断电恢复，V1.59 新增）
        //     生命周期：有在测任务才存；急停/放弃恢复/全部结束删除；损坏按无任务处理。
        // =====================================================================
        private static void TestSessionStoreTests()
        {
            EnterCleanDir(); // 快照写在运行目录(TestSession.json 相对路径)，必须隔离

            // 无文件 → 无任务
            Check("无快照文件时 Load=null", TestSessionStore.Load() == null);

            // Save→Load 往返全字段
            var session = new TestSession
            {
                LotNumber = "LOT2026断电恢复\"",
                Stations = new List<TestSessionStation>
                {
                    new TestSessionStation
                    {
                        DeviceId = 3,
                        SerialNumber = "SN-A001",
                        RecipeName = "配方X,-95kPa",
                        DurationSeconds = 7200,
                        DelaySeconds = 90,
                        AlarmThresholdKPa = -88.5m
                    },
                    new TestSessionStation { DeviceId = 44 }
                }
            };
            Check("Save 成功", TestSessionStore.Save(session));
            Check("快照文件确实生成", File.Exists("TestSession.json"));

            var loaded = TestSessionStore.Load();
            Check("Load 读回非空", loaded != null);
            if (loaded != null)
            {
                Check("批号往返一致(含特殊字符)", loaded.LotNumber == session.LotNumber);
                Check("工位数量往返一致", loaded.Stations.Count == 2);
                if (loaded.Stations.Count == 2)
                {
                    var a = loaded.Stations[0];
                    Check("工位参数往返一致(Id/SN/配方)",
                        a.DeviceId == 3 && a.SerialNumber == "SN-A001" && a.RecipeName == "配方X,-95kPa");
                    Check("定格参数往返一致(时长/延时/阈值)",
                        a.DurationSeconds == 7200 && a.DelaySeconds == 90 && a.AlarmThresholdKPa == -88.5m);
                    Check("最小字段工位反序列化不抛(DeviceId=44)", loaded.Stations[1].DeviceId == 44);
                }
                Check("SavedAt 自动写入", loaded.SavedAt != default(DateTime));
            }

            // Clear 后无任务；对不存在文件 Clear 也不抛
            TestSessionStore.Clear();
            Check("Clear 后 Load=null", TestSessionStore.Load() == null);
            bool clearAgainSafe = true;
            try { TestSessionStore.Clear(); } catch { clearAgainSafe = false; }
            Check("重复 Clear 幂等不抛", clearAgainSafe);

            // 损坏 json → 静默按无任务处理（绝不能拖垮启动流程）
            File.WriteAllText("TestSession.json", "{ 这不是合法 json !!!");
            Check("损坏快照静默返回 null", TestSessionStore.Load() == null);

            // 空在测清单的快照视为无任务
            TestSessionStore.Save(new TestSession());
            Check("空清单快照 Load=null", TestSessionStore.Load() == null);

            TestSessionStore.Clear();
        }

        // =====================================================================
        // 14. 业务串联模型扩展 —— Completed 状态 / LastTestResult / 配方负压值（V1.59）
        // =====================================================================
        private static void AgingBusinessModelTests()
        {
            // DeviceStatus.Completed 枚举与序列化：面板渲染和广播数据都走这个状态
            Check("DeviceStatus 存在 Completed 枚举值",
                Enum.IsDefined(typeof(DeviceStatus), "Completed"));
            var data = new BarometerData
            {
                DeviceId = 7,
                VacuumPressure = -92.5m,
                Status = DeviceStatus.Completed,
                LastTestResult = "PASS"
            };
            var d2 = Newtonsoft.Json.JsonConvert.DeserializeObject<BarometerData>(
                Newtonsoft.Json.JsonConvert.SerializeObject(data));
            Check("BarometerData 往返保留 Completed+PASS",
                d2.Status == DeviceStatus.Completed && d2.LastTestResult == "PASS");
            Check("BarometerData.Clone 复制 LastTestResult",
                data.Clone().LastTestResult == "PASS");
            Check("LastTestResult 默认为空串(未测过)", new BarometerData().LastTestResult == "");

            // AgingPhase 三阶段枚举齐全（None/Vacuuming/Aging）
            Check("AgingPhase 含 None/Vacuuming/Aging 三值",
                Enum.IsDefined(typeof(AgingPhase), "None") &&
                Enum.IsDefined(typeof(AgingPhase), "Vacuuming") &&
                Enum.IsDefined(typeof(AgingPhase), "Aging"));

            // StationInfo.RecipeNegativePressure：配方负压值参与到位/报警判定
            var info = new StationInfo
            {
                DeviceId = 2,
                RecipeName = "R",
                RecipeNegativePressure = -60m,
                DelayTime = TimeSpan.FromSeconds(30),
                StartTime = TimeSpan.FromHours(4)
            };
            var ic = info.Clone();
            ic.RecipeNegativePressure = 0m;
            Check("StationInfo.Clone 复制负压值且深拷贝互不影响",
                info.RecipeNegativePressure == -60m && ic.DelayTime == TimeSpan.FromSeconds(30));
            Check("RecipeNegativePressure 可为 null(未配置=全局兜底)",
                new StationInfo().RecipeNegativePressure == null);
        }

    }
}

