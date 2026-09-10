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
using System.Windows.Forms;
using AgingTestSystem.Controls;
using AgingTestSystem.Dialogs;
using AgingTestSystem.Models;
using AgingTestSystem.Services;
using AgingTestSystem.Views;

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
            Module("ThemeManager", ThemeManagerTests);
            Module("IoMapBuilder", IoMapBuilderTests);
            Module("MockDevices", MockDeviceTests);
            Module("StationCache", StationCacheTests);
            Module("ModelDefaults", ModelDefaultsTests);
            Module("SettingsValidate", SettingsValidateTests);
            Module("ScannerParse", ScannerParseTests);
            Module("ModbusConvert", ModbusConvertTests);
            Module("FanParse", FanParseTests);
            Module("StationTime", StationTimeTests);
            Module("HistoryCsv", HistoryCsvTests);
            Module("UiPureHelpers", UiPureHelperTests);
            Module("DeviceManagerIntegration", DeviceManagerIntegrationTests);
            Module("DeviceManagerExtended", DeviceManagerExtendedTests);

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
        // 【V1.62】通道合法范围收紧为 0x00~0x0F（一个寄存器 16 个 bit；
        // 0x10+ 在执行侧静默失效，现解析直接拒绝，见 IoOutputChannelRemap 注释）。
        // =====================================================================
        private static void IoRemapTests()
        {
            string err;

            var list = IoOutputChannelRemap.ParseAll("0x2000@0x00->0x2009@0x00;0x2008@0x01->0x2009@0x01", out err);
            Check("两组合法映射解析数量正确", list.Count == 2);
            Check("全合法时 error 为 null", err == null);
            if (list.Count == 2)
            {
                Check("第1组源寄存器=0x2000", list[0].SourceRegister == 0x2000);
                Check("第1组源通道 0x00→位号0", list[0].SourceChannel == 0);
                Check("第1组目标寄存器=0x2009", list[0].TargetRegister == 0x2009);
                Check("第1组目标通道 0x00→位号0", list[0].TargetChannel == 0);
                Check("第2组源通道 0x01→位号1", list[1].SourceChannel == 1);
                Check("第2组目标通道 0x01→位号1", list[1].TargetChannel == 1);
            }

            list = IoOutputChannelRemap.ParseAll("0x2000@0x0F->0x2009@0x0F", out err);
            Check("边界 0x0F 合法", list.Count == 1 && err == null);

            list = IoOutputChannelRemap.ParseAll("0x2000@0x00->0x2009@0x00；0x2008@0x02->0x2009@0x02", out err);
            Check("中文分号分隔兼容", list.Count == 2 && err == null);

            list = IoOutputChannelRemap.ParseAll("0x2000@0x00→0x2009@0x00", out err);
            Check("中文箭头分隔兼容", list.Count == 1 && err == null);

            list = IoOutputChannelRemap.ParseAll("0X2000@0X00->0X2009@0x00", out err);
            Check("大写 0X 前缀兼容", list.Count == 1 && err == null);

            list = IoOutputChannelRemap.ParseAll("2000@0x00->2009@0x01", out err);
            Check("寄存器不带 0x 前缀仍兼容解析", list.Count == 1 && list[0].SourceRegister == 0x2000);

            list = IoOutputChannelRemap.ParseAll(" 0x2000 @ 0x00 -> 0x2009 @ 0x01 ", out err);
            Check("端点两侧空格容错", list.Count == 1 && err == null);

            // 脏输入：逐项跳过并汇总 error，不影响其它合法项
            list = IoOutputChannelRemap.ParseAll("0x2000@0x00->0x2009@0x00;bad-item;0x2008@0x01->0x2009@0x01", out err);
            Check("中间坏项被跳过其余保留", list.Count == 2);
            Check("坏项内容汇总进 error", err != null && err.Contains("bad-item"));

            list = IoOutputChannelRemap.ParseAll("bad1;bad2", out err);
            Check("多错 error 用中文分号拼接", list.Count == 0 && err != null && err.Contains("；"));

            list = IoOutputChannelRemap.ParseAll("0x2000@0x00->0x2000@0x00", out err);
            Check("源与目标相同被忽略并报 error", list.Count == 0 && err != null);

            // V1.62 收紧：0x10+ 一律拒绝（源/目标任一端越界整项丢弃并明示）
            list = IoOutputChannelRemap.ParseAll("0x2000@0x10->0x2009@0x00", out err);
            Check("源通道 0x10 越界报错", list.Count == 0 && err != null && err.Contains("0x10"));
            list = IoOutputChannelRemap.ParseAll("0x2000@0x00->0x2009@0x10", out err);
            Check("目标通道 0x10 越界报错", list.Count == 0 && err != null && err.Contains("0x10"));
            list = IoOutputChannelRemap.ParseAll("0x2000@0x1F->0x2009@0x00", out err);
            Check("源通道 0x1F 越界报错", list.Count == 0 && err != null && err.Contains("0x1F"));

            list = IoOutputChannelRemap.ParseAll("0x2000@0x20->0x2009@0x00", out err);
            Check("通道越界(0x20>0x0F)报错", list.Count == 0 && err != null && err.Contains("0x20"));

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
        // 4b. IoMapBuilder —— 八进制 IO 映射（V1.62 新增，之前零覆盖）
        // =====================================================================
        private static void IoMapBuilderTests()
        {
            var cfg = new DeviceConfig { TotalBarometers = 72, TotalInputs = 80, TotalOutputs = 160 };
            var map = IoMapBuilder.Build(cfg);
            Check("默认配置总数=80输入+160输出", map.Count == 240);

            // 八进制编址：n=1→X000，n=8→X007，n=9→X010，n=72→X107
            Check("输入1→X000", map[0].PhysicalAddress == "X000" && map[0].DeviceName == "真空负压表-1");
            Check("输入8→X007", map[7].PhysicalAddress == "X007");
            Check("输入9→X010(八进制进位)", map[8].PhysicalAddress == "X010");
            Check("输入72→X107", map[71].PhysicalAddress == "X107");
            // 预留输入 73~80 → X110~X117，Function=Unknown
            Check("预留输入73→X110", map[72].PhysicalAddress == "X110" && map[72].Function == IoFunction.Unknown);
            Check("预留输入80→X117", map[79].PhysicalAddress == "X117");

            // 真空电磁阀：内部编号=80+n，Y+octal(n-1)
            Check("电磁阀1编号81→Y000", map[80].IoId == 81 && map[80].PhysicalAddress == "Y000"
                && map[80].Function == IoFunction.VacuumValve && map[80].Electrical == ElectricalType.PNP);
            Check("电磁阀72→Y107", map[151].PhysicalAddress == "Y107");
            // 载台上电：内部编号=80+72+n，Y+octal(72+n-1)：n=1→Y110，n=72→Y217
            Check("上电1编号153→Y110", map[152].IoId == 153 && map[152].PhysicalAddress == "Y110"
                && map[152].Function == IoFunction.CarrierPower);
            Check("上电72→Y217", map[223].PhysicalAddress == "Y217");
            // 预留输出 145~160 → Y220~Y237
            Check("预留输出145→Y220", map[224].PhysicalAddress == "Y220" && map[224].Function == IoFunction.Unknown);
            Check("预留输出160→Y237", map[239].IoId == 240 && map[239].PhysicalAddress == "Y237");

            // 非法配置四抛
            CheckThrows<ArgumentNullException>("配置null抛", () => IoMapBuilder.Build((DeviceConfig)null));
            CheckThrows<ArgumentOutOfRangeException>("总数0抛", () => IoMapBuilder.Build(
                new DeviceConfig { TotalBarometers = 0, TotalInputs = 80, TotalOutputs = 160 }));
            CheckThrows<ArgumentOutOfRangeException>("输入少于气压表数抛", () => IoMapBuilder.Build(
                new DeviceConfig { TotalBarometers = 72, TotalInputs = 70, TotalOutputs = 160 }));
            CheckThrows<ArgumentOutOfRangeException>("输出少于2倍抛", () => IoMapBuilder.Build(
                new DeviceConfig { TotalBarometers = 72, TotalInputs = 80, TotalOutputs = 140 }));

            // 兼容重载：无预留时总数=3倍数
            Check("Build(72)总数216无预留", IoMapBuilder.Build(72).Count == 216);

            // GetDeviceMapping 编号公式
            var m = IoMapBuilder.GetDeviceMapping(1, 72, 80);
            Check("映射输入IoId=设备号", m.VacuumPressureInput.IoId == 1 && m.VacuumPressureInput.PhysicalAddress == "X000");
            Check("映射阀IoId=80+号", m.VacuumValveOutput.IoId == 81 && m.VacuumValveOutput.PhysicalAddress == "Y000");
            Check("映射上电IoId=80+72+号", m.CarrierPowerOutput.IoId == 153 && m.CarrierPowerOutput.PhysicalAddress == "Y110");
            var m72 = IoMapBuilder.GetDeviceMapping(72, 72, 80);
            Check("映射72上电→Y217", m72.CarrierPowerOutput.PhysicalAddress == "Y217");
            CheckThrows<ArgumentOutOfRangeException>("编号0抛", () => IoMapBuilder.GetDeviceMapping(0, 72, 80));
            CheckThrows<ArgumentOutOfRangeException>("编号超总数抛", () => IoMapBuilder.GetDeviceMapping(73, 72, 80));
            CheckThrows<ArgumentOutOfRangeException>("总数0抛", () => IoMapBuilder.GetDeviceMapping(1, 0, 80));
        }

        // =====================================================================
        // 4c. Mock 三件套自身行为（V1.62 新增：集成地基，Mock 错则集成误报）
        // =====================================================================
        private static void MockDeviceTests()
        {
            var cfg = new DeviceConfig { TotalBarometers = 4, TotalInputs = 80, TotalOutputs = 160 };

            // ---- MockIoController ----
            var io = new MockIoController();
            Check("未连接读输入=false", io.ReadInput(1) == false);
            Check("未连接批量输入=空数组", io.ReadAllInputs().Length == 0);
            Check("未连接读输出=false", io.ReadOutput(81) == false);
            Check("未连接批量输出=空数组", io.ReadAllOutputs().Length == 0);
            io.WriteOutput(81, true); // 未连接静默不抛
            Check("Connect(null)拒绝", io.Connect(null) == false && io.IsConnected == false);
            Check("Connect成功", io.Connect(cfg) == true && io.IsConnected == true);
            io.WriteOutput(81, true);
            Check("输出81写读往返", io.ReadOutput(81) == true);
            io.WriteOutput(81, false);
            Check("输出81关读回false", io.ReadOutput(81) == false);
            Check("输出越界读false", io.ReadOutput(80) == false && io.ReadOutput(241) == false);
            io.WriteOutput(80, true); // 越界静默不抛
            io.WriteOutputs(null, null); // 空参静默不抛
            io.WriteOutputs(new[] { 81 }, new[] { true, false }); // 长不等静默不抛
            Check("批量写读一致", io.ReadOutput(81) == false); // 上一句因长度不等未执行
            io.WriteOutputs(new[] { 81, 82 }, new[] { true, true });
            var all = io.ReadAllOutputs();
            Check("批量输出长度160", all.Length == 160 && all[0] == true && all[1] == true);
            all[0] = false;
            Check("批量读返回副本不污染内部", io.ReadOutput(81) == true);
            Check("输入越界读false", io.ReadInput(0) == false && io.ReadInput(81) == false);

            // ---- MockBarometerReader ----
            var baro = new MockBarometerReader();
            Check("未连接读单台=null", baro.ReadData(1) == null);
            Check("未连接批量=空数组", baro.ReadAllData().Length == 0);
            Check("未连接写阈值=false", baro.SetThreshold(1, -5m) == false);
            Check("未连接批量写=空字典", baro.SetAllThresholds(-5m).Count == 0);
            baro.Connect(cfg);
            Check("越界0读null", baro.ReadData(0) == null);
            Check("越界5读null", baro.ReadData(5) == null);
            var d = baro.ReadData(1);
            Check("正常读SN/状态/IO列", d != null && d.DeviceId == 1 && d.SerialNumber == "SN0001"
                && d.Status == DeviceStatus.Idle && d.InputStatus.Length == 1 && d.OutputStatus.Length == 2);
            var batch = baro.ReadAllData();
            Check("批量长度=总数", batch.Length == 4);
            Check("写单台阈值=true", baro.SetThreshold(1, -5m) == true);
            Check("写越界阈值=false", baro.SetThreshold(5, -5m) == false);
            var allTh = baro.SetAllThresholds(-5m);
            Check("批量写4台全true", allTh.Count == 4 && allTh.Values.All(v => v));
            // V1.62 两档区间：千次采样断言范围（良好-6~-10/较差0~-4），只锁范围不锁比例防抖动
            bool rangeOk = true, seenGood = false, seenBad = false;
            for (int i = 0; i < 1000; i++)
            {
                decimal p = baro.ReadData(1).VacuumPressure;
                if (p < -10 || p > 0) { rangeOk = false; break; }
                if (p <= -6) seenGood = true; else seenBad = true;
            }
            Check("千次采样压力恒∈[-10,0]", rangeOk);
            Check("千次采样两档都出现过", seenGood && seenBad);
            baro.Disconnect();
            var gone = baro.ReadAllData();
            Check("断开后批量长度不变但全null", gone.Length == 4 && gone.All(x => x == null));

            // ---- MockFanController ----
            var fan = new MockFanController();
            Check("未连接读状态=null", fan.ReadStatus() == null);
            Check("未连接ActiveIp=null", fan.ActiveIp == null);
            Check("未连接启动=false且不改状态", fan.StartFixedValue() == false);
            Check("未连接停止=false", fan.Stop() == false);
            Check("从未Connect时重连=false", fan.ReconnectNow() == false && fan.IsConnected == false);
            fan.Connect(cfg);
            Check("ActiveIp取配置主IP", fan.ActiveIp == "192.168.1.220");
            Check("启动后运行态", fan.StartFixedValue() == true && fan.ReadStatus().RunState == FanRunState.FixedValueRunning);
            Check("停止后停止态", fan.Stop() == true && fan.ReadStatus().RunState == FanRunState.FixedValueStopped);
            var f1 = fan.ReadStatus();
            var f2 = fan.ReadStatus();
            Check("在线标志+温度漂移有界", f1.IsOnline && f2.IsOnline && Math.Abs((double)(f2.Temperature - f1.Temperature)) < 2.0);
            fan.Disconnect();
            Check("断开后重连=true", fan.ReconnectNow() == true && fan.IsConnected == true);
            fan.Dispose();
            Check("释放后断开", fan.IsConnected == false);
        }

        // =====================================================================
        // 4d. StationSettingsCache（V1.62 新增：反射重置静态缓存+隔离目录）
        // =====================================================================
        private static void StationCacheTests()
        {
            EnterCleanDir();
            ResetStationCache(); // 强制从当前隔离目录重载（进程内静态缓存跨模块常驻）

            Check("无缓存读null", StationSettingsCache.Get(1) == null);
            StationSettingsCache.Save(null); // 静默不抛

            var e = new StationCacheEntry
            {
                DeviceId = 1, SerialNumber = "SN-A001", RecipeName = "配方X",
                DelayTime = TimeSpan.FromSeconds(30), StartTime = new TimeSpan(2, 0, 0),
                LimitTemperature = 75.5m
            };
            StationSettingsCache.Save(e);
            var back = StationSettingsCache.Get(1);
            Check("往返全字段", back != null && back.SerialNumber == "SN-A001" && back.RecipeName == "配方X"
                && back.DelayTime == TimeSpan.FromSeconds(30) && back.StartTime == new TimeSpan(2, 0, 0)
                && back.LimitTemperature == 75.5m);
            back.SerialNumber = "HACKED";
            Check("返回副本不污染内部", StationSettingsCache.Get(1).SerialNumber == "SN-A001");
            e.SerialNumber = "HACKED2";
            Check("改传入对象不影响已存", StationSettingsCache.Get(1).SerialNumber == "SN-A001");

            // 覆盖语义 + 落盘重载
            StationSettingsCache.Save(new StationCacheEntry { DeviceId = 1, SerialNumber = "SN-B002" });
            Check("同号覆盖", StationSettingsCache.Get(1).SerialNumber == "SN-B002");
            ResetStationCache(); // 模拟重启：从 StationSettings.json 重载
            Check("重启后落盘可读", StationSettingsCache.Get(1) != null && StationSettingsCache.Get(1).SerialNumber == "SN-B002");

            // 脏文件三态
            File.WriteAllText("StationSettings.json", "{broken json");
            ResetStationCache();
            Check("损坏文件读null不抛", StationSettingsCache.Get(1) == null);
            File.WriteAllText("StationSettings.json", "[]");
            ResetStationCache();
            Check("空数组读null", StationSettingsCache.Get(1) == null);
            File.WriteAllText("StationSettings.json", "[{\"DeviceId\":0},{\"DeviceId\":-3},null]");
            ResetStationCache();
            Check("非法编号条目被过滤", StationSettingsCache.Get(0) == null);
            File.Delete("StationSettings.json");
            ResetStationCache();
        }

        /// <summary>反射把 StationSettingsCache 静态 _cache 置 null，强制下次 Get/Save 从当前目录重载文件</summary>
        private static void ResetStationCache()
        {
            var f = typeof(StationSettingsCache).GetField("_cache", BindingFlags.NonPublic | BindingFlags.Static);
            f.SetValue(null, null);
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

            // V1.61：负压阈值默认 -5kPa（公共参数窗 nudThreshold 默认值与之对齐）
            Check("报警压力阈值默认 -5kPa", new DeviceConfig().AlarmPressureThresholdKPa == -5m);
            Check("报警方向默认高于阈值报警", new DeviceConfig().AlarmWhenPressureHigherThanThreshold == true);
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

            File.WriteAllText("Recipes.json", "null");
            var nulllit = RecipeStorage.Load();
            Check("json字面量null Load 返回空列表(非 null)", nulllit != null && nulllit.Count == 0);

            // SaveWithDuplicateCheck 新增分支（同名覆盖分支弹 UI 对话框，归界面手工测试覆盖）
            var shared = new List<RecipeConfig>();
            var r = new RecipeConfig { Name = "新建配方", NegativePressure = -70m };
            Check("SaveWithDuplicateCheck 新增返回 true", RecipeStorage.SaveWithDuplicateCheck(shared, r));
            Check("新增后 Id 自动分配为 1", shared.Count == 1 && shared[0].Id == 1);
            Check("新增后立即落盘", File.Exists("Recipes.json"));
            var r2 = new RecipeConfig { Name = "第二个配方" };
            Check("第二条新增 Id=2", RecipeStorage.SaveWithDuplicateCheck(shared, r2) && shared[1].Id == 2);
            // V1.62：删中间配方后新增不许撞号（Max+1，不是 Count+1）
            shared.RemoveAt(0);
            var r3 = new RecipeConfig { Name = "第三个配方" };
            Check("删Id=1后新增Id=3不撞号", RecipeStorage.SaveWithDuplicateCheck(shared, r3) && shared[1].Id == 3);
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

            // V1.62：回车转义 + 字段格式边角
            if (mi != null)
            {
                Func<string, string> esc2 = v => (string)mi.Invoke(null, new object[] { v });
                Check("含回车被双引号包裹", esc2("a\rb") == "\"a\rb\"");
                Check("含回车换行被包裹", esc2("a\r\nb") == "\"a\r\nb\"");
            }
            TestEventLogger.Write(null, -1, null, null, 12m, 33.56f);
            string[] lines2 = ReadAllLinesShared(file);
            string last = lines2[lines2.Length - 1];
            Check("全null字段仍7列不抛", last.Split(',').Length >= 7 && last.Contains(",-1,"));
            Check("温度一位小数格式化33.6", last.EndsWith("33.6"));
            Check("压力整数12原样写", last.Contains(",12,"));
            TestEventLogger.Write("LOTX", 1, "启动", "ok", null, null);

            // 并发写零丢失（对标 AppLog 并发用例）
            int beforeCount = ReadAllLinesShared(file).Length;
            var tasks = new List<Task>();
            for (int t = 0; t < 20; t++)
            {
                int id = t;
                tasks.Add(Task.Run(() =>
                {
                    for (int k = 0; k < 5; k++) TestEventLogger.Write("CC", id, "并发", "x", null, null);
                }));
            }
            Task.WaitAll(tasks.ToArray());
            Check("20线程x5行并发零丢失", ReadAllLinesShared(file).Length == beforeCount + 100);

            // 删目录后自动重建
            Directory.Delete(logDir, true);
            TestEventLogger.Write("RE", 1, "重建", "dir", null, null);
            Check("删Logs目录后自动重建", File.Exists(file));
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

            // 调整范围约束（编辑器钳制依据，与 HomeLayoutEditorForm 常量同步）
            Check("标题栏范围15~80", HomeLayoutConfig.TopBarRange.Min == 15 && HomeLayoutConfig.TopBarRange.Max == 80);
            Check("菜单栏范围25~100", HomeLayoutConfig.MenuRange.Min == 25 && HomeLayoutConfig.MenuRange.Max == 100);
            Check("右侧区范围180~600", HomeLayoutConfig.RightPanelRange.Min == 180 && HomeLayoutConfig.RightPanelRange.Max == 600);
            Check("状态栏范围15~60", HomeLayoutConfig.StatusBarRange.Min == 15 && HomeLayoutConfig.StatusBarRange.Max == 60);

            // 损坏回退：写垃圾后仍返回默认值（BaseDirectory 是共享 run 目录，用完必须还原）
            try
            {
                File.WriteAllText(cfgPath, "{broken json");
                var fallback = HomeLayoutConfig.LoadOrDefault();
                Check("损坏文件回退默认40/50/260/30",
                    fallback.TopBarHeight == 40 && fallback.MenuHeight == 50
                    && fallback.RightPanelWidth == 260 && fallback.StatusBarHeight == 30);
            }
            finally
            {
                if (File.Exists(cfgPath)) File.Delete(cfgPath);
            }
        }

        // =====================================================================
        // 10b. ModelDefaults —— 模型枚举值与构造默认值锁定（V1.62 新增）
        // =====================================================================
        private static void ModelDefaultsTests()
        {
            // DeviceConfig 构造默认值（App.config 缺项时的回退口径，全锁死防手滑）
            var cfg = new DeviceConfig();
            Check("默认72/80/160", cfg.TotalBarometers == 72 && cfg.TotalInputs == 80 && cfg.TotalOutputs == 160);
            Check("默认COM1/19200/8/1/None", cfg.PortName == "COM1" && cfg.BaudRate == 19200
                && cfg.DataBits == 8 && cfg.StopBits == 1 && cfg.Parity == "None");
            Check("默认采集1000/面板8x9", cfg.CollectInterval == 1000 && cfg.PanelColumns == 8 && cfg.PanelRows == 9);
            Check("默认Mock=true", cfg.UseMockCommunication == true);
            Check("默认超时1000/1000/3000/3000",
                cfg.SerialReadTimeoutMs == 1000 && cfg.SerialWriteTimeoutMs == 1000
                && cfg.TcpSendTimeoutMs == 3000 && cfg.TcpReceiveTimeoutMs == 3000);
            Check("默认不取反/UnitId=1", cfg.InvertInputs == false && cfg.InvertOutputs == false && cfg.IoUnitId == 1);
            Check("默认寄存器0x1000/0x2000/0x0001",
                cfg.IoInputRegisterStartAddress == 0x1000 && cfg.IoOutputRegisterStartAddress == 0x2000
                && cfg.BarometerPressureRegisterAddress == 0x0001);
            Check("默认小数1/缩放1", cfg.BarometerDefaultDecimalPlaces == 1 && cfg.BarometerPressureScale == 1m);
            Check("默认备用映射关闭+空表", cfg.IoBackupChannelMappingEnabled == false && cfg.IoBackupChannelMappings != null
                && cfg.IoBackupChannelMappings.Count == 0);
            Check("默认风机220/50000/1/3000", cfg.FanIpAddress == "192.168.1.220" && cfg.FanPort == 50000
                && cfg.FanUnitId == 1 && cfg.FanTimeoutMs == 3000);
            Check("默认风机自识别开+空候选", cfg.FanAutoDetectEnabled == true && cfg.FanIpCandidates != null
                && cfg.FanIpCandidates.Count == 0);
            Check("默认15000/3/0", cfg.VacuumConfirmTimeoutMs == 15000 && cfg.CommunicationLossAlarmCount == 3
                && cfg.MaxTestDurationSeconds == 0);
            Check("默认DI不并入/温度告警关", cfg.UseDiAlarmContact == false && cfg.FanTempAlarmLimitC == 0f);
            Check("默认扫码枪关闭全套", cfg.ScannerEnabled == false && cfg.ScannerPort == ""
                && cfg.ScannerDeviceKeyword == "Xenon 1902" && cfg.ScannerBaudRate == 115200
                && cfg.ScannerDataBits == 8 && cfg.ScannerStopBits == 1 && cfg.ScannerParity == "None"
                && cfg.ScannerDebugLog == false);

            // 风机状态枚举 = 寄存器值（改一个数就读错设备，必须锁）
            Check("风机枚举映射-1/0/1/2/3",
                (int)FanRunState.Unknown == -1 && (int)FanRunState.ProgramStopped == 0
                && (int)FanRunState.ProgramRunning == 1 && (int)FanRunState.FixedValueStopped == 2
                && (int)FanRunState.FixedValueRunning == 3);

            // FanData.Clone 全字段（含旧用例漏的设定值/时间戳）
            var f = new FanData
            {
                RunState = FanRunState.FixedValueRunning, Temperature = 36.6f, Humidity = 44.4f,
                TempSetpoint = 37f, HumSetpoint = 45f, IsOnline = true,
                CollectTime = new DateTime(2026, 9, 10, 12, 0, 0)
            };
            var fc = f.Clone();
            fc.TempSetpoint = 0f;
            Check("FanData.Clone全字段",
                fc.RunState == FanRunState.FixedValueRunning && fc.Temperature == 36.6f
                && fc.Humidity == 44.4f && fc.HumSetpoint == 45f && fc.IsOnline
                && fc.CollectTime == f.CollectTime && f.TempSetpoint == 37f);

            // BarometerData.Clone 数组深拷贝 + 默认列宽
            var b = new BarometerData { DeviceId = 1 };
            Check("默认输入1列输出2列", b.InputStatus.Length == 1 && b.OutputStatus.Length == 2
                && b.LastTestResult == "");
            b.InputStatus[0] = true; b.OutputStatus[1] = true;
            var bc = b.Clone();
            bc.InputStatus[0] = false; bc.OutputStatus[1] = false;
            Check("Clone数组深拷贝", b.InputStatus[0] && b.OutputStatus[1]);
            var bn = new BarometerData { InputStatus = null, OutputStatus = null };
            var bnc = bn.Clone(); // null 数组不抛
            Check("Clone空数组不抛且仍null", bnc.InputStatus == null && bnc.OutputStatus == null);

            // UserAccount.LoginResult 工厂
            var u = new UserAccount("op", "h", UserRole.Operator);
            var ok = LoginResult.Ok(u);
            Check("Ok三字段", ok.Success && object.ReferenceEquals(ok.User, u) && ok.ErrorMessage == null);
            var fail = LoginResult.Fail("密码错");
            Check("Fail用户null+带原因", !fail.Success && fail.User == null && fail.ErrorMessage == "密码错");

            // 权限枚举值（权限比较地基）
            Check("角色值0/1/2", (int)UserRole.Operator == 0 && (int)UserRole.Technician == 1
                && (int)UserRole.Administrator == 2);

            // TestSession 构造默认值
            var ts = new TestSession();
            Check("快照默认空批号+非空清单", ts.LotNumber == "" && ts.Stations != null && ts.Stations.Count == 0);
            var tst = new TestSessionStation();
            Check("快照台默认空串+零时长", tst.SerialNumber == "" && tst.RecipeName == ""
                && tst.DurationSeconds == 0 && tst.DelaySeconds == 0);

            // RecipeConfig 默认启用
            Check("新配方默认启用", new RecipeConfig().IsEnabled == true);
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
            Check("计时为负永不判失败",
                !AgingSequencer.IsVacuumBuildFailed(false, TimeSpan.FromSeconds(-1), 15000));
            Check("零宽限+未到位立即失败",
                AgingSequencer.IsVacuumBuildFailed(false, TimeSpan.Zero, 0));

            // ── IsPressureOutOfRange（V1.62 新增：两处私有判定的唯一口径）──
            Check("默认方向-4>-5越限", AgingSequencer.IsPressureOutOfRange(-4m, -5m, true));
            Check("默认方向-6<-5正常", !AgingSequencer.IsPressureOutOfRange(-6m, -5m, true));
            Check("默认方向恰等阈值不越限", !AgingSequencer.IsPressureOutOfRange(-5m, -5m, true));
            Check("反方向-6<-5越限", AgingSequencer.IsPressureOutOfRange(-6m, -5m, false));
            Check("反方向-4>-5正常", !AgingSequencer.IsPressureOutOfRange(-4m, -5m, false));
            Check("反方向恰等阈值不越限", !AgingSequencer.IsPressureOutOfRange(-5m, -5m, false));
            Check("常压0恒越限(默认方向)", AgingSequencer.IsPressureOutOfRange(0m, -5m, true));

            // ── 负时间输入语义锁 ──
            Check("延时为负视为已到(立即上电)",
                AgingSequencer.ShouldPowerOn(true, TimeSpan.Zero, -5));
            Check("计时为负不上电",
                !AgingSequencer.ShouldPowerOn(true, TimeSpan.FromSeconds(-1), 0));
            Check("计时为负不完成",
                !AgingSequencer.ShouldComplete(TimeSpan.FromSeconds(-1), 3600));
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
                        RecipeName = "配方X,-5kPa",
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
                        a.DeviceId == 3 && a.SerialNumber == "SN-A001" && a.RecipeName == "配方X,-5kPa");
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

            // V1.62：Stations:null 与字面量 null 的 Load 语义
            File.WriteAllText("TestSession.json", "{\"LotNumber\":\"L\",\"Stations\":null}");
            Check("Stations为null Load=null", TestSessionStore.Load() == null);
            File.WriteAllText("TestSession.json", "null");
            Check("json字面量null Load=null", TestSessionStore.Load() == null);
            Check("Save(null)返回false", TestSessionStore.Save(null) == false);

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

        // =====================================================================
        // 14b. SettingsValidate —— 保存校验/范围/键表一致性（V1.62 新增）
        // =====================================================================
        private static void SettingsValidateTests()
        {
            // —— ValidateValue 全类型矩阵（反射私有静态，无需窗体实例） ——
            var vv = typeof(SettingsForm).GetMethod("ValidateValue",
                BindingFlags.NonPublic | BindingFlags.Static);
            Check("反射找到 ValidateValue", vv != null);
            if (vv != null)
            {
                Func<string, string, bool> ok = (k, v) =>
                {
                    object[] args = new object[] { k, v, null };
                    return (bool)vv.Invoke(null, args);
                };
                Func<string, string, string> errOf = (k, v) =>
                {
                    object[] args = new object[] { k, v, null };
                    vv.Invoke(null, args);
                    return args[2] as string;
                };
                Check("整数合法", ok("TotalBarometers", "72") && errOf("TotalBarometers", "72") == null);
                Check("整数非法文案", !ok("TotalBarometers", "abc") && errOf("TotalBarometers", "abc") == "应为整数");
                Check("波特率非数字拦截", !ok("BaudRate", "abc"));
                Check("IoUnitId边界255合法", ok("IoUnitId", "255"));
                Check("IoUnitId=256越界(byte)", !ok("IoUnitId", "256"));
                Check("IoUnitId负数拦截", !ok("IoUnitId", "-1"));
                Check("寄存器0x写法合法", ok("BarometerPressureRegisterAddress", "0x1000"));
                Check("寄存器十进制合法", ok("BarometerPressureRegisterAddress", "4096"));
                Check("寄存器乱写拦截", !ok("BarometerPressureRegisterAddress", "xyz"));
                Check("小数阈值-5.5合法", ok("AlarmPressureThresholdKPa", "-5.5"));
                Check("小数乱写拦截", !ok("BarometerPressureScale", "abc"));
                Check("温度小数合法", ok("FanTempAlarmLimitC", "36.6"));
                Check("布尔true合法", ok("UseMockCommunication", "true"));
                Check("布尔大小写兼容", ok("UseMockCommunication", "True"));
                Check("布尔YES拦截", !ok("UseMockCommunication", "YES"));
                Check("文本端口不校验", ok("PortName", "COM9") && ok("PortName", ""));
                Check("IP候选/映射表不强制校验",
                    ok("FanIpCandidates", "xxx") && ok("IoBackupChannelMappings", "xxx"));

                // _boolKeys 10 项逐项过校验（防"只加一边"的配置漂移）
                var boolKeys = (HashSet<string>)typeof(SettingsForm).GetField("_boolKeys",
                    BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                Check("布尔键10项", boolKeys != null && boolKeys.Count == 10);
                if (boolKeys != null)
                {
                    bool allBoolOk = boolKeys.All(k => ok(k, "true") && ok(k, "false") && !ok(k, "YES"));
                    Check("布尔键true/false过YES不过", allBoolOk);
                    Check("含关键布尔键",
                        boolKeys.Contains("AlarmWhenPressureHigherThanThreshold")
                        && boolKeys.Contains("ScannerDebugLog")
                        && boolKeys.Contains("UseMockCommunication"));
                }
            }

            // —— TryParseUShort 双重载（反射选双参版） ——
            var tpu = typeof(SettingsForm).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .First(m => m.Name == "TryParseUShort" && m.GetParameters().Length == 2);
            Func<string, Tuple<bool, ushort>> parse = v =>
            {
                object[] args = new object[] { v, (ushort)0 };
                bool r = (bool)tpu.Invoke(null, args);
                return Tuple.Create(r, (ushort)args[1]);
            };
            var p1 = parse("4096");
            Check("十进制4096", p1.Item1 && p1.Item2 == 4096);
            var p2 = parse("0x1000");
            Check("十六进制0x1000", p2.Item1 && p2.Item2 == 4096);
            var p3 = parse("0X1000");
            Check("大写0X兼容", p3.Item1 && p3.Item2 == 4096);
            Check("上限65535合法", parse("65535").Item1);
            Check("65536越界", !parse("65536").Item1);
            Check("空串非法", !parse("").Item1);
            Check("null非法", !parse(null).Item1);
            Check("0x后缀缺失非法", !parse("0x").Item1);
            Check("负数非法", !parse("-1").Item1);

            // —— _numericKeys 范围抽查 + 默认值落在范围内 ——
            var nkf = typeof(SettingsForm).GetField("_numericKeys",
                BindingFlags.NonPublic | BindingFlags.Static);
            var ranges = (Dictionary<string, ValueTuple<decimal, decimal, int, decimal>>)nkf.GetValue(null);
            var th = ranges["AlarmPressureThresholdKPa"];
            Check("阈值范围±200/2位小数", th.Item1 == -200m && th.Item2 == 200m && th.Item3 == 2);
            Check("新默认-5落在范围内", -5m >= th.Item1 && -5m <= th.Item2);
            var tb = ranges["TotalBarometers"];
            Check("总数范围1~999整数", tb.Item1 == 1m && tb.Item2 == 999m && tb.Item3 == 0);
            Check("端口范围上限65535", ranges["PlcPort"].Item2 == 65535m);
            Check("采集间隔下限10", ranges["CollectInterval"].Item1 == 10m);

            // —— 公开连接键集合契约 ——
            Check("结构键含数量/Mock/风机",
                SettingsForm.StructuralKeys.Contains("TotalBarometers")
                && SettingsForm.StructuralKeys.Contains("UseMockCommunication")
                && SettingsForm.StructuralKeys.Contains("FanEnabled"));
            Check("气压表连接键含串口五件套",
                SettingsForm.BarometerConnectionKeys.Contains("PortName")
                && SettingsForm.BarometerConnectionKeys.Contains("Parity"));
            Check("IO连接键含地址端口",
                SettingsForm.IoConnectionKeys.Contains("PlcAddress")
                && SettingsForm.IoConnectionKeys.Contains("PlcPort"));
            Check("FanEnabled是结构键不是连接键",
                !SettingsForm.FanConnectionKeys.Contains("FanEnabled")
                && SettingsForm.FanConnectionKeys.Contains("FanIpCandidates"));
            Check("扫码枪连接键含端口识别",
                SettingsForm.ScannerConnectionKeys.Contains("ScannerPort")
                && SettingsForm.ScannerConnectionKeys.Contains("ScannerDeviceKeyword"));
            Check("调试日志开关不触发重连",
                !SettingsForm.ScannerConnectionKeys.Contains("ScannerDebugLog"));

            // —— CreateValueCell 分发（反射私有静态，返回类型即契约） ——
            var cvc = typeof(SettingsForm).GetMethod("CreateValueCell",
                BindingFlags.NonPublic | BindingFlags.Static);
            Func<string, string, DataGridViewCell> cellOf = (k, v) =>
                (DataGridViewCell)cvc.Invoke(null, new object[] { k, v });
            var boolCell = cellOf("UseMockCommunication", "TRUE");
            Check("布尔→下拉且TRUE归一true",
                boolCell is DataGridViewComboBoxCell && Equals(boolCell.Value, "true"));
            Check("布尔非法值归一false",
                Equals(cellOf("UseMockCommunication", "yes").Value, "false"));
            var portCell = cellOf("PortName", "COM99");
            Check("串口→下拉且保留已配值",
                portCell is DataGridViewComboBoxCell && Equals(portCell.Value, "COM99"));
            Check("空串口保持空(自动识别语义)",
                cellOf("ScannerPort", "").Value == null);
            Check("扫描串口同走下拉", cellOf("ScannerPort", "COM10") is DataGridViewComboBoxCell);
            var popCell = cellOf("IoBackupChannelMappings", "0x2000@0x00->0x2009@0x00");
            Check("映射表→弹窗只读格",
                popCell is DataGridViewPopupEditCell && Equals(popCell.Value, "0x2000@0x00->0x2009@0x00"));
            Check("IP候选→弹窗格", cellOf("FanIpCandidates", "a") is DataGridViewPopupEditCell);
            Check("主页布局→弹窗格", cellOf("HomeLayout", "") is DataGridViewPopupEditCell);
            var baudCell = cellOf("BaudRate", "230400");
            Check("波特率含档位+自定义回显",
                baudCell is DataGridViewComboBoxCell
                && ((DataGridViewComboBoxCell)baudCell).Items.Contains("19200")
                && ((DataGridViewComboBoxCell)baudCell).Items.Contains("230400"));
            var bitsCell = (DataGridViewComboBoxCell)cellOf("DataBits", "8");
            Check("数据位5~8", bitsCell.Items.Count == 4 && bitsCell.Items.Contains("8"));
            var stopCell = cellOf("StopBits", "15");
            Check("停止位15存值(显示走ValueMember)",
                stopCell is DataGridViewStrictComboBoxCell && Equals(stopCell.Value, "15"));
            Check("停止位非法归一1", Equals(cellOf("StopBits", "3").Value, "1"));
            var parCell = cellOf("Parity", "Odd");
            Check("校验位存枚举名", Equals(parCell.Value, "Odd"));
            var numCell = (DataGridViewNumericUpDownCell)cellOf("AlarmPressureThresholdKPa", "-5.5");
            Check("数字格范围小数位一致",
                numCell.Minimum == -200m && numCell.Maximum == 200m && numCell.DecimalPlaces == 2
                && Equals(numCell.Value, -5.5m));
            Check("数字超界钳Max", Equals(cellOf("TotalBarometers", "99999").Value, 999m));
            Check("数字非法文本钳Min", Equals(cellOf("TotalBarometers", "abc").Value, 1m));
            var txtCell = cellOf("PlcAddress", "192.168.1.20");
            Check("其余走文本格", txtCell is DataGridViewTextBoxCell && Equals(txtCell.Value, "192.168.1.20"));

            // —— 分类/说明键对齐（需窗体实例读实例字典；构造失败则只记一条） ——
            SettingsForm sf = null;
            string buildErr = null;
            try { sf = new SettingsForm(new DeviceConfig()); }
            catch (Exception ex) { buildErr = ex.GetType().Name + ":" + ex.Message; }
            Check("设置窗可构造", sf != null, buildErr);
            if (sf != null)
            {
                try
                {
                    var desc = (Dictionary<string, string>)typeof(SettingsForm).GetField("_descriptions",
                        BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sf);
                    var cats = (Array)typeof(SettingsForm).GetField("_categories",
                        BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sf);
                    var catKeys = new List<string>();
                    foreach (object c in cats)
                    {
                        var keys = (string[])c.GetType().GetField("Item2").GetValue(c);
                        catKeys.AddRange(keys);
                    }
                    Check("分类键全有说明", catKeys.All(k => desc.ContainsKey(k)),
                        "缺:" + string.Join(",", catKeys.Where(k => !desc.ContainsKey(k)).ToArray()));
                    Check("关键键在分类里",
                        catKeys.Contains("AlarmPressureThresholdKPa")
                        && catKeys.Contains("IoBackupChannelMappings")
                        && catKeys.Contains("HomeLayout"));
                    Check("分类键50+项", catKeys.Count >= 50);
                }
                finally { sf.Dispose(); }
            }
        }

        // =====================================================================
        // 14c. ScannerParse —— 扫码枪纯解析（V1.62 新增，反射私有静态）
        // =====================================================================
        private static void ScannerParseTests()
        {
            var t = typeof(ScannerService);
            var join = t.GetMethod("JoinPorts", BindingFlags.NonPublic | BindingFlags.Static);
            Func<IEnumerable<string>, string> jp = v => (string)join.Invoke(null, new object[] { v });
            Check("null端口→-", jp(null) == "-");
            Check("空列表→-", jp(new string[0]) == "-");
            Check("多项逗号拼接", jp(new[] { "COM3", "COM5" }) == "COM3,COM5");

            var pp = t.GetMethod("ParseParity", BindingFlags.NonPublic | BindingFlags.Static);
            Func<string, string> par = v => pp.Invoke(null, new object[] { v }).ToString();
            Check("None", par("None") == "None");
            Check("小写even", par("even") == "Even");
            Check("大写ODD", par("ODD") == "Odd");
            Check("空→None", par("") == "None" && par(null) == "None");
            Check("非法→None", par("xyz") == "None");

            var ps = t.GetMethod("ParseStopBits", BindingFlags.NonPublic | BindingFlags.Static);
            Func<int, string> stb = v => ps.Invoke(null, new object[] { v }).ToString();
            Check("2→Two", stb(2) == "Two");
            Check("15→1.5", stb(15) == "OnePointFive");
            Check("1→One", stb(1) == "One");
            Check("非法→One", stb(9) == "One" && stb(0) == "One");
            // 跨文件契约：与 SettingsForm.NormalizeStopBits 两端一致（15↔1.5）
            var norm = typeof(SettingsForm).GetMethod("NormalizeStopBits",
                BindingFlags.NonPublic | BindingFlags.Static);
            Check("两端15口径一致",
                ((string)norm.Invoke(null, new object[] { "1.5" }) == "15") && stb(15) == "OnePointFive");
        }

        // =====================================================================
        // 14d. ModbusConvert —— 气压换算/端口故障判定/未连接约定（V1.62 新增）
        // =====================================================================
        private static void ModbusConvertTests()
        {
            // —— 纯换算（公开静态，直接调） ——
            Check("0xFFFE→-2再除10=-0.2",
                ModbusRtuBarometerReader.ConvertRawToPressureKPa(unchecked((short)0xFFFE), 1, 1m) == -0.2m);
            Check("-50/10=-5.0",
                ModbusRtuBarometerReader.ConvertRawToPressureKPa(-50, 1, 1m) == -5.0m);
            Check("小数位0不除",
                ModbusRtuBarometerReader.ConvertRawToPressureKPa(123, 0, 1m) == 123m);
            Check("缩放系数生效",
                ModbusRtuBarometerReader.ConvertRawToPressureKPa(100, 1, 0.5m) == 5.0m);
            Check("short下界换算",
                ModbusRtuBarometerReader.ConvertRawToPressureKPa(short.MinValue, 1, 1m) == -3276.8m);
            Check("阈值-5.0/1位→-50",
                ModbusRtuBarometerReader.ConvertThresholdToRegister(-5.0m, 1) == -50L);
            Check("阈值12.345/3位→12345",
                ModbusRtuBarometerReader.ConvertThresholdToRegister(12.345m, 3) == 12345L);
            Check("阈值0→0",
                ModbusRtuBarometerReader.ConvertThresholdToRegister(0m, 1) == 0L);
            Check("阈值-95.06/1位→-951",
                ModbusRtuBarometerReader.ConvertThresholdToRegister(-95.06m, 1) == -951L);

            // —— 端口级故障判定（反射私有静态） ——
            var ipf = typeof(ModbusRtuBarometerReader).GetMethod("IsPortLevelFailure",
                BindingFlags.NonPublic | BindingFlags.Static);
            Func<Exception, bool> isPortFail = ex => (bool)ipf.Invoke(null, new object[] { ex });
            Check("无权限访问算端口故障", isPortFail(new UnauthorizedAccessException()));
            Check("对象释放算端口故障", isPortFail(new ObjectDisposedException("sp")));
            Check("英文port closed算", isPortFail(new System.IO.IOException("port closed")));
            Check("中文信号量超时算", isPortFail(new System.IO.IOException("信号量超时")));
            Check("普通IO异常不算", !isPortFail(new System.IO.IOException("普通读写超时")));
            Check("读写超时异常不算", !isPortFail(new TimeoutException("x")));
            Check("从站异常响应不算", !isPortFail(new Exception("Slave device failed")));

            // —— 未连接约定（全新实例，不碰硬件） ——
            var reader = new ModbusRtuBarometerReader();
            Check("初始未连接", reader.IsConnected == false);
            Check("未连接读单台=null", reader.ReadData(1) == null);
            Check("未连接批量=空数组", reader.ReadAllData().Length == 0);
            Check("未连接写阈值=false", reader.SetThreshold(1, -5m) == false);
            Check("未连接批量写=空字典", reader.SetAllThresholds(-5m).Count == 0);
            reader.Disconnect(); // 未连接时断开不抛
            Check("断开幂等", reader.IsConnected == false);

            // —— 串口参数解析（反射私有实例，需实例但不动硬件） ——
            var r2 = new ModbusRtuBarometerReader();
            var mpp = typeof(ModbusRtuBarometerReader).GetMethod("ParseParity",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Func<string, string> rpar = v => mpp.Invoke(r2, new object[] { v }).ToString();
            Check("Rtu:None", rpar("None") == "None");
            Check("Rtu:小写odd", rpar("odd") == "Odd");
            Check("Rtu:空→None", rpar("") == "None" && rpar(null) == "None");
            Check("Rtu:非法→None", rpar("xyz") == "None");
            var msb = typeof(ModbusRtuBarometerReader).GetMethod("ParseStopBits",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Func<int, string> rstb = v => msb.Invoke(r2, new object[] { v }).ToString();
            Check("Rtu:1→One", rstb(1) == "One");
            Check("Rtu:2→Two", rstb(2) == "Two");
            Check("Rtu:15→1.5", rstb(15) == "OnePointFive");
            Check("Rtu:非法→One", rstb(3) == "One");
        }

        // =====================================================================
        // 14e. FanParse —— 风机寄存器解析/未连接约定（V1.62 新增）
        // =====================================================================
        private static void FanParseTests()
        {
            // —— 纯解析（公开静态，直接调；数值全选二进制精确值防抖动） ——
            Check("null→null", FanControllerClient.ParseFanRegisters(null) == null);
            Check("不足6个→null", FanControllerClient.ParseFanRegisters(new ushort[] { 1, 2, 3 }) == null);
            var fd = FanControllerClient.ParseFanRegisters(new ushort[] { 0, 3, 2500, 6000, 3700, 5000 });
            Check("6寄存器解析全字段",
                fd != null && fd.RunState == FanRunState.FixedValueRunning
                && fd.Temperature == 25f && fd.Humidity == 60f
                && fd.TempSetpoint == 37f && fd.HumSetpoint == 50f && fd.IsOnline);
            var fd0 = FanControllerClient.ParseFanRegisters(new ushort[] { 0, 0, 0, 0, 0, 0 });
            Check("全0→程式停止+零值", fd0.RunState == FanRunState.ProgramStopped && fd0.Temperature == 0f);
            var fdbad = FanControllerClient.ParseFanRegisters(new ushort[] { 0, 9, 0, 0, 0, 0 });
            Check("非法枚举值透传(显示层兜底)", (int)fdbad.RunState == 9);

            // —— 未连接约定（全新实例，不碰网络） ——
            var fan = new FanControllerClient();
            Check("初始未连接", fan.IsConnected == false);
            Check("配置空读状态=null", fan.ReadStatus() == null);
            Check("配置空启动=false", fan.StartFixedValue() == false);
            Check("配置空停止=false", fan.Stop() == false);
            bool threw = false;
            bool rc = false;
            try { rc = fan.ReconnectNow(); }
            catch { threw = true; }
            Check("配置空重连false不抛", !threw && rc == false);
            bool cnull = false;
            threw = false;
            try { cnull = fan.Connect(null); }
            catch { threw = true; }
            Check("Connect(null)返回false不抛", !threw && cnull == false);
            Check("ActiveIp初始null", fan.ActiveIp == null);
            fan.Dispose(); // 未连接释放不抛
        }

        // =====================================================================
        // 14f. StationTime —— 工位时间 helpers（V1.62 新增，含 24h 截断修复）
        // =====================================================================
        private static void StationTimeTests()
        {
            var t = typeof(StationSettingsForm);
            var get = t.GetMethod("GetTimeSpan", BindingFlags.NonPublic | BindingFlags.Static);
            var set = t.GetMethod("SetTimeInputs", BindingFlags.NonPublic | BindingFlags.Static);
            var txt = t.GetMethod("GetTimeText", BindingFlags.NonPublic | BindingFlags.Static);
            Check("反射找到三方法", get != null && set != null && txt != null);
            if (get == null || set == null || txt == null) return;

            NumericUpDown h = new NumericUpDown { Minimum = 0, Maximum = 99 };
            NumericUpDown m = new NumericUpDown { Minimum = 0, Maximum = 59 };
            NumericUpDown s = new NumericUpDown { Minimum = 0, Maximum = 59 };
            h.Value = 1; m.Value = 10; s.Value = 20;
            var ts = (TimeSpan)get.Invoke(null, new object[] { h, m, s });
            Check("组合01:10:20", ts == new TimeSpan(1, 10, 20));

            // V1.62 修复：25 小时不再截断成 1
            set.Invoke(null, new object[] { h, m, s, new TimeSpan(25, 10, 20) });
            Check("25小时回填不截断", h.Value == 25m && m.Value == 10m && s.Value == 20m);
            var back = (TimeSpan)get.Invoke(null, new object[] { h, m, s });
            Check("25小时往返一致", back == new TimeSpan(25, 10, 20));
            set.Invoke(null, new object[] { h, m, s, new TimeSpan(150, 0, 0) });
            Check("超99钳99", h.Value == 99m);
            Check("文本01:10:20", (string)txt.Invoke(null, new object[] { new TimeSpan(1, 10, 20) }) == "01:10:20");
            Check("文本25小时不截断", (string)txt.Invoke(null, new object[] { new TimeSpan(25, 0, 0) }) == "25:00:00");
            Check("文本零值", (string)txt.Invoke(null, new object[] { TimeSpan.Zero }) == "00:00:00");

            var clamp = t.GetMethod("Clamp", BindingFlags.NonPublic | BindingFlags.Static);
            Check("钳制上界", Equals(clamp.Invoke(null, new object[] { h, 500 }), 99m));
            Check("钳制下界", Equals(clamp.Invoke(null, new object[] { h, -5 }), 0m));
        }

        // =====================================================================
        // 14g. HistoryCsv —— 历史记录 CSV 解析 + 与写入器互逆（V1.62 新增）
        // =====================================================================
        private static void HistoryCsvTests()
        {
            HistoryRecordForm form = null;
            string buildErr = null;
            try { form = new HistoryRecordForm(); }
            catch (Exception ex) { buildErr = ex.GetType().Name + ":" + ex.Message; }
            Check("历史窗可构造", form != null, buildErr);
            if (form == null) return;
            try
            {
                var mi = typeof(HistoryRecordForm).GetMethod("ParseCsvLine",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Check("反射找到 ParseCsvLine", mi != null);
                if (mi == null) return;
                Func<string, string[]> parse = v => (string[])mi.Invoke(form, new object[] { v });
                Check("普通3段", parse("a,b,c").Length == 3);
                Check("引号逗号1+1", parse("\"a,b\",c")[0] == "a,b" && parse("\"a,b\",c")[1] == "c");
                // CSV 里 "" 转义只在引号字段内有效（写入器永远整字段包裹，与解析器配对）
                Check("双引号翻倍还原", parse("\"a\"\"b\"")[0] == "a\"b");
                Check("尾逗号出空尾段", parse("a,").Length == 2 && parse("a,")[1] == "");
                Check("空行出1空段", parse("").Length == 1 && parse("")[0] == "");

                // 互逆：写入器转义 → 解析器还原（7 列对齐，详情含逗号引号）
                EnterCleanDir();
                TestEventLogger.Write("INV", 5, "报警", "详情,有\"引号\"", -5.5m, 36.6f);
                string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs",
                    "TestLog_" + DateTime.Now.ToString("yyyyMMdd") + ".csv");
                string[] lines = ReadAllLinesShared(file);
                string[] f = parse(lines[lines.Length - 1]);
                Check("互逆7列", f.Length == 7);
                if (f.Length == 7)
                {
                    Check("互逆批号/编号/事件", f[1] == "INV" && f[2] == "5" && f[3] == "报警");
                    Check("互逆详情还原", f[4] == "详情,有\"引号\"");
                    Check("互逆压力温度", f[5] == "-5.5" && f[6] == "36.6");
                }
            }
            finally { form.Dispose(); }
        }

        // =====================================================================
        // 14h. UiPureHelpers —— 对话框/控件/串口识别纯函数（V1.62 新增）
        // =====================================================================
        private static void UiPureHelperTests()
        {
            // —— 批号录入（公开方法，scanner=null 可构造） ——
            var lotForm = new InputLotForm();
            try
            {
                var txtLot = typeof(InputLotForm).GetField("txtLot",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(lotForm) as TextBox;
                Check("反射拿到批号框", txtLot != null);
                if (txtLot != null)
                {
                    txtLot.Text = "  LOT-9 ";
                    Check("批号去首尾空格", lotForm.GetLotNumber() == "LOT-9");
                    txtLot.Text = "";
                    Check("空批号→空串", lotForm.GetLotNumber() == "");
                }
            }
            finally { lotForm.Dispose(); }

            // —— 配方管理查找（忽略大小写，需实例+列表） ——
            var recipes = new List<RecipeConfig>
            {
                new RecipeConfig { Id = 1, Name = "高温配方" },
                new RecipeConfig { Id = 2, Name = "r2-abc" }
            };
            var rmForm = new RecipeManagerForm(recipes);
            try
            {
                var find = typeof(RecipeManagerForm).GetMethod("FindRecipeIndex",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Check("反射找到 FindRecipeIndex", find != null);
                if (find != null)
                {
                    Func<string, int> idx = v => (int)find.Invoke(rmForm, new object[] { v });
                    Check("精确命中0", idx("高温配方") == 0);
                    Check("大小写命中1", idx("R2-ABC") == 1);
                    Check("未命中-1", idx("没有这个") == -1);
                }
                // SetTimeInputs 走 TotalHours（与工位窗 V1.62 修复对齐，防回退）
                var rmSet = typeof(RecipeManagerForm).GetMethod("SetTimeInputs",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (rmSet != null)
                {
                    NumericUpDown h = new NumericUpDown { Minimum = 0, Maximum = 99 };
                    NumericUpDown m = new NumericUpDown { Minimum = 0, Maximum = 59 };
                    NumericUpDown s = new NumericUpDown { Minimum = 0, Maximum = 59 };
                    rmSet.Invoke(rmForm, new object[] { h, m, s, new TimeSpan(25, 10, 20) });
                    Check("配方窗25小时不截断", h.Value == 25m && m.Value == 10m && s.Value == 20m);
                }
            }
            finally { rmForm.Dispose(); }

            // —— 工位窗温度解析（实例方法读文本框，构造传 null 设备管理器可测） ——
            StationSettingsForm stForm = null;
            try { stForm = new StationSettingsForm(null, new DeviceConfig(), new List<RecipeConfig>(), 1); }
            catch { }
            Check("工位窗可构造", stForm != null);
            if (stForm != null)
            {
                try
                {
                    var txtTemp = typeof(StationSettingsForm).GetField("txtTemp",
                        BindingFlags.NonPublic | BindingFlags.Instance).GetValue(stForm) as TextBox;
                    var parseTemp = typeof(StationSettingsForm).GetMethod("ParseTemperature",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    Check("反射拿到温度框与解析", txtTemp != null && parseTemp != null);
                    if (txtTemp != null && parseTemp != null)
                    {
                        Func<string, decimal> pt = v =>
                        {
                            txtTemp.Text = v;
                            return (decimal)parseTemp.Invoke(stForm, null);
                        };
                        Check("温度37.5", pt("37.5") == 37.5m);
                        Check("非法温度→0", pt("abc") == 0m);
                        Check("空温度→0", pt("") == 0m);
                    }
                }
                finally { stForm.Dispose(); }
            }

            // —— IP 合法性（反射私有静态） ——
            var isIp = typeof(IpListEditorPopup).GetMethod("IsValidIp",
                BindingFlags.NonPublic | BindingFlags.Static);
            Func<string, bool> validIp = v => (bool)isIp.Invoke(null, new object[] { v });
            Check("IPv4合法", validIp("192.168.1.20"));
            Check("IPv4带空格合法", validIp(" 192.168.1.20 "));
            Check("IPv6拒绝(与弹窗口径一致)", !validIp("::1"));
            Check("空/null拒绝", !validIp("") && !validIp(null));
            Check("越界拒绝", !validIp("256.1.1.1") && !validIp("abc"));

            // —— 数字格解析钳制（公开重写，直接 new 格） ——
            var numCell = new DataGridViewNumericUpDownCell { Minimum = -200m, Maximum = 200m };
            var style = new DataGridViewCellStyle();
            Check("decimal原样", Equals(numCell.ParseFormattedValue(12.5m, style, null, null), 12.5m));
            Check("字符串解析", Equals(numCell.ParseFormattedValue("12", style, null, null), 12m));
            Check("超界钳Max", Equals(numCell.ParseFormattedValue("300", style, null, null), 200m));
            Check("欠界钳Min", Equals(numCell.ParseFormattedValue("-300", style, null, null), -200m));

            // —— 网格命中/边界/状态色（直接 new 控件 + Configure，反射私有方法） ——
            var grid = new WorkstationGridView();
            try
            {
                grid.Configure(8, 9, 72);
                var tg = typeof(WorkstationGridView);
                var hitPanel = tg.GetMethod("TryHitPanel", BindingFlags.NonPublic | BindingFlags.Instance);
                var boundsOf = tg.GetMethod("GetPanelBounds", BindingFlags.NonPublic | BindingFlags.Instance);
                var backOf = tg.GetMethod("GetStatusBackColor", BindingFlags.NonPublic | BindingFlags.Instance);
                Check("反射找到命中三方法", hitPanel != null && boundsOf != null && backOf != null);
                if (hitPanel != null && boundsOf != null && backOf != null)
                {
                    Rectangle b1 = (Rectangle)boundsOf.Invoke(grid, new object[] { 1 });
                    Rectangle b2 = (Rectangle)boundsOf.Invoke(grid, new object[] { 2 });
                    Check("1号面板原点", b1.X == 0 && b1.Y == 0);
                    Check("2号面板紧贴1号", b2.X == b1.Width && b2.Y == 0);
                    Func<Point, Tuple<bool, int>> hit = p =>
                    {
                        object[] args = new object[] { p, 0, new Point() };
                        bool r = (bool)hitPanel.Invoke(grid, args);
                        return Tuple.Create(r, (int)args[1]);
                    };
                    Check("超大坐标不命中", !hit(new Point(100000, 100000)).Item1);
                    Check("负坐标不命中", !hit(new Point(-5, -5)).Item1);
                    var center = hit(new Point(b1.Width / 2, b1.Height / 2));
                    Check("1号中心命中1", center.Item1 && center.Item2 == 1);
                    Func<DeviceStatus, Color> bc = st => (Color)backOf.Invoke(grid, new object[] { st });
                    var cFault = bc(DeviceStatus.Fault); var cTest = bc(DeviceStatus.Testing);
                    var cDone = bc(DeviceStatus.Completed); var cIdle = bc(DeviceStatus.Idle);
                    Check("四状态四色互异",
                        cFault.ToArgb() != cTest.ToArgb() && cFault.ToArgb() != cDone.ToArgb()
                        && cFault.ToArgb() != cIdle.ToArgb() && cTest.ToArgb() != cDone.ToArgb()
                        && cTest.ToArgb() != cIdle.ToArgb() && cDone.ToArgb() != cIdle.ToArgb());
                    grid.SetDarkMode(true);
                    var cFaultDark = bc(DeviceStatus.Fault);
                    Check("深色故障色与浅色不同", cFaultDark.ToArgb() != cFault.ToArgb());
                    grid.SetDarkMode(false);
                    Check("切回浅色还原", bc(DeviceStatus.Fault).ToArgb() == cFault.ToArgb());
                }
            }
            finally { grid.Dispose(); }

            // —— 通讯测试位值→通道号（公开静态） ——
            Check("0x0001→0", CommunicationTestForm.ChannelOf(0x0001) == 0);
            Check("0x0002→1", CommunicationTestForm.ChannelOf(0x0002) == 1);
            Check("0x0100→8", CommunicationTestForm.ChannelOf(0x0100) == 8);
            Check("0x8000→15", CommunicationTestForm.ChannelOf(0x8000) == 15);
            Check("0→0", CommunicationTestForm.ChannelOf(0) == 0);

            // —— 风机状态中文（反射私有静态；与主窗文案差异已知，锁本窗契约） ——
            var gst = typeof(FanTestForm).GetMethod("GetStateText",
                BindingFlags.NonPublic | BindingFlags.Static);
            Func<FanRunState, string> stx = v => (string)gst.Invoke(null, new object[] { v });
            Check("四态中文", stx(FanRunState.ProgramStopped) == "程式停止"
                && stx(FanRunState.ProgramRunning) == "程式启动"
                && stx(FanRunState.FixedValueStopped) == "定值停止"
                && stx(FanRunState.FixedValueRunning) == "定值启动");
            Check("未知态→--", stx(FanRunState.Unknown) == "--"
                && stx((FanRunState)99) == "--");

            // —— CH340 识别谓词（公开静态） ——
            Check("标准CH340命中",
                SerialPortHelper.IsCh340Device("USB-SERIAL CH340 (COM3)",
                "USB\\VID_1A86&PID_7523\\6&1 confusion"));
            Check("小写描述命中(忽略大小写)",
                SerialPortHelper.IsCh340Device("usb-serial ch340 (COM3)", "USB\\VID_1A86&PID_7523\\X"));
            Check("FTDI拒绝",
                !SerialPortHelper.IsCh340Device("USB Serial Port (COM4)", "USB\\VID_0403&PID_6001\\X"));
            Check("错VID拒绝",
                !SerialPortHelper.IsCh340Device("USB-SERIAL CH340 (COM3)", "USB\\VID_0403&PID_7523\\X"));
            Check("null/空拒绝",
                !SerialPortHelper.IsCh340Device(null, "USB\\VID_1A86&PID_7523\\X")
                && !SerialPortHelper.IsCh340Device("USB-SERIAL CH340 (COM3)", null)
                && !SerialPortHelper.IsCh340Device("", ""));
            Check("系统串口列表非null", SerialPortHelper.GetAllPortNames() != null);
        }

        // =====================================================================
        // 15. 深色/浅色主题 —— 解析/映射表往返/内存切换/整树着色冒烟（V1.60）
        //
        // 【测什么】
        // ThemeManager 是纯静态主题服务：Parse 读配置、MapXxx 查双向映射表、
        // SetMode 切内存主题、ApplyTo 递归着色。文件读写（SaveToConfig）不测——
        // 它动的是测试进程自己的 exe.config，断言文件内容又脆又没价值；
        // 保存逻辑由冒烟测试（真机启动→按钮切换→重启看主题保持）人工覆盖。
        // =====================================================================
        private static void ThemeManagerTests()
        {
            // —— Parse：Dark 大小写/空格兼容，其余一切兜底浅色（手写错配置也不炸） ——
            Check("Parse Dark", ThemeManager.Parse("Dark") == AppThemeMode.Dark);
            Check("Parse 小写dark", ThemeManager.Parse("dark") == AppThemeMode.Dark);
            Check("Parse 带空格大写DARK", ThemeManager.Parse("  DARK  ") == AppThemeMode.Dark);
            Check("Parse Light", ThemeManager.Parse("Light") == AppThemeMode.Light);
            Check("Parse 空串兜底浅色", ThemeManager.Parse("") == AppThemeMode.Light);
            Check("Parse null兜底浅色", ThemeManager.Parse(null) == AppThemeMode.Light);
            Check("Parse 乱写兜底浅色", ThemeManager.Parse("深色") == AppThemeMode.Light);

            // —— 映射表：代表色映射 + 往返精确还原 + 语义色原样保留 ——
            Check("容器底 Control→深",
                ThemeManager.MapContainerBack(SystemColors.Control, true).ToArgb()
                == ThemeManager.DarkSurfaceBack.ToArgb());
            Check("容器底 深→Control精确还原",
                ThemeManager.MapContainerBack(ThemeManager.DarkSurfaceBack, false).ToArgb()
                == SystemColors.Control.ToArgb());
            Check("容器底 White往返精确",
                ThemeManager.MapContainerBack(
                    ThemeManager.MapContainerBack(Color.White, true), false).ToArgb()
                == Color.White.ToArgb());
            Check("文字黑→浅字",
                ThemeManager.MapForeColor(Color.Black, true).ToArgb()
                == ThemeManager.DarkText.ToArgb());
            Check("文字红深色不动(语义色保留)",
                ThemeManager.MapForeColor(Color.Red, true).ToArgb() == Color.Red.ToArgb());
            Check("文字绿浅色不动(语义色保留)",
                ThemeManager.MapForeColor(Color.Green, false).ToArgb() == Color.Green.ToArgb());
            Check("单元格分组蓝往返精确",
                ThemeManager.MapGridCellFore(
                    ThemeManager.MapGridCellFore(Color.FromArgb(48, 119, 238), true), false).ToArgb()
                == Color.FromArgb(48, 119, 238).ToArgb());
            Check("输入底 LightGray→深灰(保留只读暗示)",
                ThemeManager.MapInputBack(Color.LightGray, true).ToArgb()
                == Color.FromArgb(70, 70, 70).ToArgb());

            // —— 内存切换（save=false，不写配置文件） ——
            ThemeManager.SetMode(AppThemeMode.Light, false);
            Check("初始浅色", !ThemeManager.IsDark && ThemeManager.Current == AppThemeMode.Light);
            ThemeManager.SetMode(AppThemeMode.Dark, false);
            Check("切深色", ThemeManager.IsDark && ThemeManager.Current == AppThemeMode.Dark);
            ThemeManager.SetMode(AppThemeMode.Light, false);
            Check("切回浅色", !ThemeManager.IsDark);

            // —— 整树着色冒烟（STA harness，可直接 new 控件；只断言颜色，不弹窗） ——
            var pnl = new Panel();
            var lbl = new Label();   // 默认：Empty 字 + Transparent 底
            var txt = new TextBox(); // 默认：白底黑字
            var btn = new Button();  // 默认灰按钮（着色跳过，底不动）
            var btnOk = new Button { BackColor = Color.LimeGreen, ForeColor = Color.White }; // 语义按钮
            var grid = new DataGridView();
            pnl.Controls.Add(lbl);
            pnl.Controls.Add(txt);
            pnl.Controls.Add(btn);
            pnl.Controls.Add(btnOk);
            pnl.Controls.Add(grid);
            Color pnlBack0 = pnl.BackColor;
            Color gridBack0 = grid.BackgroundColor;

            ThemeManager.SetMode(AppThemeMode.Dark, false);
            ThemeManager.ApplyTo(pnl);
            Check("深色面板底变深", pnl.BackColor.ToArgb() == ThemeManager.DarkSurfaceBack.ToArgb(),
                "实际=" + pnl.BackColor);
            Check("深色标签字变浅", lbl.ForeColor.ToArgb() == ThemeManager.DarkText.ToArgb(),
                "实际=" + lbl.ForeColor);
            Check("深色输入框底变深", txt.BackColor.ToArgb() == ThemeManager.DarkInputBack.ToArgb(),
                "实际=" + txt.BackColor);
            // 注意：Button/Label 的 getter 在本地 Empty 时会返回父容器颜色（WinForms 环境属性继承），
            // 所以"没碰过"的正确断言是"跟父容器一致"，而不是 Empty/Control 这种具体值。
            Check("默认按钮底色从未被改动(Transparent继承=跟随面板走)",
                btn.BackColor.ToArgb() == pnl.BackColor.ToArgb(),
                "按钮=" + btn.BackColor + " 面板=" + pnl.BackColor);
            Check("语义绿按钮两色不动",
                btnOk.BackColor.ToArgb() == Color.LimeGreen.ToArgb()
                && btnOk.ForeColor.ToArgb() == Color.White.ToArgb());
            Check("深色表格底变深", grid.BackgroundColor.ToArgb() == ThemeManager.DarkSurfaceBack.ToArgb(),
                "实际=" + grid.BackgroundColor);
            Check("深色表头不跟系统主题(EnableHeadersVisualStyles=false)",
                grid.EnableHeadersVisualStyles == false);

            ThemeManager.SetMode(AppThemeMode.Light, false);
            ThemeManager.ApplyTo(pnl);
            Check("浅色面板底精确还原", pnl.BackColor.ToArgb() == pnlBack0.ToArgb(),
                "实际=" + pnl.BackColor);
            Check("浅色标签字跟随父容器(无残留深色值)", lbl.ForeColor.ToArgb() == pnl.ForeColor.ToArgb(),
                "标签=" + lbl.ForeColor + " 面板=" + pnl.ForeColor);
            Check("浅色输入框底还原白", txt.BackColor.ToArgb() == Color.White.ToArgb(),
                "实际=" + txt.BackColor);
            Check("浅色表格底精确还原", grid.BackgroundColor.ToArgb() == gridBack0.ToArgb(),
                "实际=" + grid.BackgroundColor);
            Check("浅色表头恢复系统主题", grid.EnableHeadersVisualStyles == true);

            ThemeManager.SetMode(AppThemeMode.Light, false); // 收尾复位，免得影响其它模块
            grid.Dispose();
            pnl.Dispose();

            // —— V1.60.1：停止/复位两按钮主题配色（纯函数，不 new 主窗体就能断） ——
            Color back;
            Color fore;
            MainForm.GetOperationButtonThemeColors(true, out back, out fore);
            Check("深色停止/复位按钮深灰底白字",
                back.ToArgb() == Color.DimGray.ToArgb() && fore.ToArgb() == Color.White.ToArgb(),
                "实际=" + back + "/" + fore);
            MainForm.GetOperationButtonThemeColors(false, out back, out fore);
            Check("浅色停止/复位按钮恢复系统默认灰底黑字",
                back.ToArgb() == SystemColors.Control.ToArgb()
                && fore.ToArgb() == SystemColors.ControlText.ToArgb(),
                "实际=" + back + "/" + fore);

            // —— V1.60.3：工位下电/真空关块主题配色（纯函数，不 new 网格就能断） ——
            Color offBack;
            Color offFore;
            WorkstationGridView.GetOffBlockThemeColors(true, Color.LightGray, out offBack, out offFore);
            Check("深色下电/真空关块深灰底白字(参考停止按钮)",
                offBack.ToArgb() == Color.DimGray.ToArgb() && offFore.ToArgb() == Color.White.ToArgb(),
                "实际=" + offBack + "/" + offFore);
            WorkstationGridView.GetOffBlockThemeColors(false, Color.LightGray, out offBack, out offFore);
            Check("浅色下电/真空关块跟配置灰底黑字",
                offBack.ToArgb() == Color.LightGray.ToArgb() && offFore.ToArgb() == Color.Black.ToArgb(),
                "实际=" + offBack + "/" + offFore);

            // —— V1.60.4：公共参数保存按钮 + 布局预览画布底（纯函数，不 new 窗体就能断） ——
            Color saveBack;
            Color saveFore;
            CommonParameterForm.GetSaveButtonThemeColors(true, out saveBack, out saveFore);
            Check("深色公共参数保存按钮灰底白字",
                saveBack.ToArgb() == Color.DimGray.ToArgb() && saveFore.ToArgb() == Color.White.ToArgb(),
                "实际=" + saveBack + "/" + saveFore);
            CommonParameterForm.GetSaveButtonThemeColors(false, out saveBack, out saveFore);
            Check("浅色公共参数保存按钮保持原生(Empty=不动)",
                saveBack == Color.Empty && saveFore == Color.Empty,
                "实际=" + saveBack + "/" + saveFore);
            Check("深色布局预览画布纯黑",
                HomeLayoutEditorForm.GetPreviewBackColor(true).ToArgb() == Color.Black.ToArgb());
            Check("浅色布局预览画布白纸",
                HomeLayoutEditorForm.GetPreviewBackColor(false).ToArgb() == Color.White.ToArgb());

            // —— V1.62：映射表剩余分支全锁（公开查表函数直接断言） ——
            Check("单元格Empty/白→深格底",
                ThemeManager.MapGridCellBack(Color.Empty, true).ToArgb() == ThemeManager.DarkCellBack.ToArgb()
                && ThemeManager.MapGridCellBack(Color.White, true).ToArgb() == ThemeManager.DarkCellBack.ToArgb());
            Check("单元格浅蓝→深表头",
                ThemeManager.MapGridCellBack(Color.FromArgb(237, 243, 253), true).ToArgb()
                == ThemeManager.DarkHeaderBack.ToArgb());
            Check("单元格红保留",
                ThemeManager.MapGridCellBack(Color.Red, true).ToArgb() == Color.Red.ToArgb());
            Check("单元格字黑→浅字",
                ThemeManager.MapGridCellFore(Color.Black, true).ToArgb() == ThemeManager.DarkText.ToArgb());
            Check("单元格字48灰→浅字",
                ThemeManager.MapGridCellFore(Color.FromArgb(48, 48, 48), true).ToArgb()
                == ThemeManager.DarkText.ToArgb());
            Check("容器245灰→深档",
                ThemeManager.MapContainerBack(Color.FromArgb(245, 245, 245), true).ToArgb()
                == Color.FromArgb(55, 55, 58).ToArgb());
            Check("容器243蓝→深档",
                ThemeManager.MapContainerBack(Color.FromArgb(243, 249, 255), true).ToArgb()
                == Color.FromArgb(48, 58, 84).ToArgb());
            Check("文字 slate 映射+往返",
                ThemeManager.MapForeColor(Color.DarkSlateGray, true).ToArgb()
                == ThemeManager.DarkSlateText.ToArgb()
                && ThemeManager.MapForeColor(
                    ThemeManager.MapForeColor(Color.DarkSlateGray, true), false).ToArgb()
                == Color.DarkSlateGray.ToArgb());
            Check("文字30蓝→提示蓝",
                ThemeManager.MapForeColor(Color.FromArgb(30, 80, 160), true).ToArgb()
                == ThemeManager.DarkHintBlue.ToArgb());
            Check("文字80灰→灰字",
                ThemeManager.MapForeColor(Color.FromArgb(80, 80, 80), true).ToArgb()
                == ThemeManager.DarkGrayText.ToArgb());
            Check("输入Window→深输入底",
                ThemeManager.MapInputBack(SystemColors.Window, true).ToArgb()
                == ThemeManager.DarkInputBack.ToArgb());
            Check("输入Control→深界面底",
                ThemeManager.MapInputBack(SystemColors.Control, true).ToArgb()
                == ThemeManager.DarkSurfaceBack.ToArgb());
        }

    }
}

