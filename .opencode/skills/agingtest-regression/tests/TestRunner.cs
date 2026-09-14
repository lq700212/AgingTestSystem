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
//   12b. PolicyV167            —— 工艺策略纯函数 + 名单同步锁 + tooltip + 项目档案
//   12c. MesV168                —— MES 映射解析 + 组包 + 上报器（Fake 传输，零外网）
//   12d. RuleExprV169            —— 规则表达式解析求值 + 规则表 + 执行器（假时钟）
//   12e. ProcessPolicyV170          —— 流程图静态文本 + 布局存取（纯函数，零 UI）
//   12f. PowerReportV174          —— 电流骨架(Mock/桩/接线/规则变量) + 报表列 + 显示字典
//   12g. SoftActivation           —— 软件激活（HJVision 同源：Encrypt 方程 + 计数格 + 激活比对 + ini 往返 + 激活窗构造）
//   12h. PolicyPresetV185          —— 预置策略 A/B/C（套用/探测纯函数 + 名单/口径/安全锁 + UI 预置行回显）
//   12i. PolicyNodeComboV1851      —— 节点选项框按预置下拉口径统一（下拉实测拉宽 + 悬停全文 + 切节点清表）
//   12j. DeployDiagV188_9           —— 调试部署诊断：启动版本水印纯函数 + 崩溃日志文件名/正文/落盘（含null/非Exception兜底）
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
//  FanControllerClient、ModbusTcpIoController）与 UI 弹窗分支不在本 harness 范围，
//  由现场联调与界面手工测试覆盖；后续可扩展虚拟串口/模拟器用例。
//  （配方同名覆盖确认框已搬到 UI 层，Services 层 SaveRecipe 无 UI 全覆盖。）
// ============================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
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
        private static int Main(string[] args)
        {
            // 控制台输出切 UTF-8，避免中文断言信息乱码（脚本捕获输出也按 UTF-8 读）
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }

            Console.WriteLine("AgingTestSystem 回归测试 harness");
            Console.WriteLine("BaseDirectory = " + AppDomain.CurrentDomain.BaseDirectory);
            Console.WriteLine("起始工作目录   = " + Environment.CurrentDirectory);

            // ── V1.72.4 分级回归：39 个模块全表（新增模块只加这里一行，
            // 下面的选中逻辑与 SKILL.md 覆盖表自动跟随，无需再改别处）──
            var allModules = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase)
            {
                { "PasswordHasher", PasswordHasherTests },
                { "UserManager", UserManagerTests },
                { "SettingsForm.Normalize", NormalizeTests },
                { "IoOutputChannelRemap", IoRemapTests },
                { "DeviceConfig.ParseFanIpCandidates", FanIpTests },
                { "RecipeStorage", RecipeStorageTests },
                { "TestEventLogger", TestEventLoggerTests },
                { "AppLogFileWriter", AppLogWriterTests },
                { "PanelLayoutConfig", PanelLayoutTests },
                { "HomeLayoutConfig", HomeLayoutTests },
                { "ModelRoundtrip", ModelRoundtripTests },
                { "AgingSequencer", AgingSequencerTests },
                { "PolicyV167", PolicyTests },
                { "MesV168", MesTests },
                { "RuleExprV169", RuleExprTests },
                { "TestSessionStore", TestSessionStoreTests },
                { "AgingBusinessModel", AgingBusinessModelTests },
                { "ThemeManager", ThemeManagerTests },
                { "IoMapBuilder", IoMapBuilderTests },
                { "MockDevices", MockDeviceTests },
                { "StationCache", StationCacheTests },
                { "ModelDefaults", ModelDefaultsTests },
                { "SettingsValidate", SettingsValidateTests },
                { "ScannerParse", ScannerParseTests },
                { "ModbusConvert", ModbusConvertTests },
                { "FanParse", FanParseTests },
                { "StationTime", StationTimeTests },
                { "HistoryCsv", HistoryCsvTests },
                { "UiPureHelpers", UiPureHelperTests },
                { "DeviceManagerIntegration", DeviceManagerIntegrationTests },
                { "DeviceManagerExtended", DeviceManagerExtendedTests },
                { "DeviceManagerPolicy", DeviceManagerPolicyTests },
                { "DeviceManagerMes", DeviceManagerMesTests },
                { "DeviceManagerRules", DeviceManagerRulesTests },
                { "DeviceManagerIdentity", DeviceManagerIdentityTests },
                { "DeviceManagerSweep", DeviceManagerSweepTests },
                { "ProcessPolicyV170", ProcessPolicyTests },
                { "UiStyleV172_1", UiStyleV172_1Tests },
                { "UiFinalizerV172_14", UiFinalizerV172_14Tests },
                { "LegacyRecipeGuard", LegacyRecipeGuardTests },
                { "DesignerStabilityV172_16", DesignerStabilityV172_16Tests },
                { "PowerReportV174", PowerReportV174Tests },
                { "SoftActivation", SoftActivationTests },
                { "PolicyPresetV185", PolicyPresetTests },
                { "PolicyNodeComboV1851", PolicyNodeComboTests },
                { "DeployDiagV188_9", DeployDiagTests },
            };

            // 参数约定：无参=全量；"模块A,模块B"=子集（大小写不敏感）；
            // "list"=只打印模块清单（给脚本做补全/校验用）。
            // 未知模块名直接 exit 2 并打印清单（fail-fast，防拼写错导致"零模块全绿"误报）。
            List<string> selected;
            if (args.Length == 1 && args[0].Trim().Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("可用模块（共 " + allModules.Count + " 个）：");
                foreach (string k in allModules.Keys) Console.WriteLine("  " + k);
                return 0;
            }
            else if (args.Length == 0 || (args.Length == 1 && string.IsNullOrWhiteSpace(args[0])))
            {
                selected = new List<string>(allModules.Keys);
            }
            else
            {
                selected = new List<string>();
                var unknown = new List<string>();
                foreach (string raw in string.Join(" ", args).Split(','))
                {
                    string m = raw.Trim();
                    if (m.Length == 0) continue;
                    if (allModules.ContainsKey(m))
                    {
                        // 按全表顺序去重加入（输出顺序稳定，与参数顺序无关）
                        if (!selected.Exists(s => s.Equals(m, StringComparison.OrdinalIgnoreCase)))
                            selected.Add(m);
                    }
                    else unknown.Add(m);
                }
                if (unknown.Count > 0)
                {
                    Console.WriteLine("[SETUP-FAIL] 未知模块: " + string.Join("、", unknown.ToArray()));
                    Console.WriteLine("可用模块（共 " + allModules.Count + " 个）：");
                    foreach (string k in allModules.Keys) Console.WriteLine("  " + k);
                    return 2;
                }
                if (selected.Count == 0)
                {
                    Console.WriteLine("[SETUP-FAIL] 未选中任何模块（参数为空），拒绝空跑。");
                    return 2;
                }
            }

            Console.WriteLine("选中模块 " + selected.Count + "/" + allModules.Count
                + (selected.Count == allModules.Count ? "（全量）" : "（子集）") + "："
                + string.Join("、", selected.ToArray()));
            foreach (string name in allModules.Keys)
            {
                if (selected.Exists(s => s.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    Module(name, allModules[name]);
                else
                    Console.WriteLine("(跳过模块：" + name + ")");
            }

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
            // 【大扫荡】迭代次数上下界（防 DoS）：手改 Users.json 写 PBKDF2$2147483647$…
            // 一次登录卡死 UI 数分钟；超界=非法串，判失败且不跑哈希。
            Check("迭代次数过低判失败", !PasswordHasher.Verify("123456", "PBKDF2$1$AAAA==$BBBB=="));
            Check("迭代次数巨量判失败(DoS防护)", !PasswordHasher.Verify("123456", "PBKDF2$2147483647$AAAA==$BBBB=="));
            Check("迭代次数100000正常仍可用", PasswordHasher.Verify("123456", h1));

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
            // 【大扫荡】登录大小写不敏感（与注册/改名查重口径一致；以前精确匹配，
            // 存"dev"输"DEV"报用户名错误、但该名又注册不了，口径分裂）。
            Check("用户名大小写不敏感(Admin=admin)", umOp.Login(UserRole.Administrator, "Admin", "123456").Success);
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

            // ── J. dev 最高权限账号（V1.64）：种子/隐藏登录/管管理员/注册保护/老文件自愈 ──
            EnterCleanDir();
            var umDevFresh = new UserManager();
            Check("新装自带 dev 账号(管理员组)", umDevFresh.GetAccounts(UserRole.Administrator).Any(a => a.Username == "dev"));
            Check("Users.json 不含 dev 明文密码", !File.ReadAllText("Users.json").Contains("dev123"));
            var rDev = umDevFresh.Login(UserRole.Administrator, "dev", "dev123");
            Check("dev 走管理员登录入口成功(隐藏入口)", rDev.Success && umDevFresh.IsDevLoggedIn);
            Check("dev 密码错误被拒", !new UserManager().Login(UserRole.Administrator, "dev", "wrong").Success);
            Check("dev 不能走技术员入口(角色错配)", !new UserManager().Login(UserRole.Technician, "dev", "dev123").Success);
            var umAdmChk = new UserManager();
            umAdmChk.Login(UserRole.Administrator, "admin", "123456");
            Check("普通管理员登录成功", umAdmChk.CurrentUser != null && umAdmChk.CurrentUser.Username == "admin");
            Check("普通管理员 IsDevLoggedIn 为 false", !umAdmChk.IsDevLoggedIn);

            // 注册保护：dev 名任何人都要不到
            var addDev = umAdmChk.AddAccount(UserRole.Operator, "dev", "abcd1234");
            Check("注册 dev 名被拒", !addDev.Success);
            Check("注册 dev 名提示不可用", addDev.Message.Contains("不可用"));
            Check("注册 DEV 大小写变体被拒", !umAdmChk.AddAccount(UserRole.Operator, "DEV", "abcd1234").Success);
            Check("注册 Dev 变体被拒", !umAdmChk.AddAccount(UserRole.Technician, " Dev ", "abcd1234").Success);
            Check("第二个业务管理员仍被拒", !umAdmChk.AddAccount(UserRole.Administrator, "admin2", "abcd1234").Success);

            // 普通管理员碰管理员组一律被拒；dev 全放行（除动 dev 自身）
            Check("管理员删管理员被拒", !umAdmChk.RemoveAccount(UserRole.Administrator, "admin").Success);
            var bizAdmin = umAdmChk.GetAccounts(UserRole.Administrator).First(a => a.Username == "admin");
            Check("管理员重置管理员密码被拒", !umAdmChk.UpdatePassword(bizAdmin, "newpw99").Success);
            Check("管理员改管理员名被拒", !umAdmChk.UpdateUsername(bizAdmin, "admin_x").Success);

            var umDev = new UserManager();
            umDev.Login(UserRole.Administrator, "dev", "dev123");
            Check("dev 登录后 IsDevLoggedIn 为 true", umDev.IsDevLoggedIn);
            var devSelf = umDev.GetAccounts(UserRole.Administrator).First(a => a.Username == "dev");
            var bizAdm2 = umDev.GetAccounts(UserRole.Administrator).First(a => a.Username == "admin");
            Check("dev 重置管理员密码成功", umDev.UpdatePassword(bizAdm2, "newadminpw").Success);
            Check("dev 改管理员用户名成功", umDev.UpdateUsername(bizAdm2, "admin_renamed").Success);
            Check("改名为 dev 被拒(不可用)", !umDev.UpdateUsername(
                umDev.GetAccounts(UserRole.Operator).First(a => a.Username == "operator"), "dev").Success);
            Check("dev 自身不允许改名", !umDev.UpdateUsername(devSelf, "dev2").Success);
            Check("dev 自身不允许删除", !umDev.RemoveAccount(UserRole.Administrator, "dev").Success);
            var umAdmDel = new UserManager();
            Check("改名改密后业务管理员仍可登录",
                umAdmDel.Login(UserRole.Administrator, "admin_renamed", "newadminpw").Success);
            Check("管理员删 dev 被拒", !umAdmDel.RemoveAccount(UserRole.Administrator, "dev").Success);
            Check("dev 删除业务管理员成功", umDev.RemoveAccount(UserRole.Administrator, "admin_renamed").Success);
            Check("被删管理员不再能登录",
                !new UserManager().Login(UserRole.Administrator, "admin_renamed", "newadminpw").Success);
            Check("dev 可建新业务管理员(空出名额)", umDev.AddAccount(UserRole.Administrator, "admin2", "abcd1234").Success);
            Check("新业务管理员可登录", new UserManager().Login(UserRole.Administrator, "admin2", "abcd1234").Success);
            Check("dev 改自己密码(验旧密)成功", umDev.ChangeOwnPassword("dev123", "dev456").Success);
            Check("dev 新密码可登录", new UserManager().Login(UserRole.Administrator, "dev", "dev456").Success);

            // 老文件自愈：没有 dev 的旧 Users.json → 自动补 dev，且老密码一个不动
            EnterCleanDir();
            File.WriteAllText("Users.json",
                "[{\"Username\":\"operator\",\"Password\":\"" + PasswordHasher.Hash("123456") + "\",\"Role\":0}," +
                "{\"Username\":\"technician\",\"Password\":\"" + PasswordHasher.Hash("123456") + "\",\"Role\":1}," +
                "{\"Username\":\"admin\",\"Password\":\"" + PasswordHasher.Hash("oldadminpw") + "\",\"Role\":2}]");
            var umHeal = new UserManager();
            Check("老文件无 dev 时自动补上", umHeal.Login(UserRole.Administrator, "dev", "dev123").Success);
            Check("老管理员密码未被重置", umHeal.Login(UserRole.Administrator, "admin", "oldadminpw").Success);

            // 手改文件同时有 dev+双业务管理员 → 保留 dev+第一个
            File.WriteAllText("Users.json",
                "[{\"Username\":\"dev\",\"Password\":\"" + PasswordHasher.Hash("dev123") + "\",\"Role\":2}," +
                "{\"Username\":\"b1\",\"Password\":\"" + PasswordHasher.Hash("1111") + "\",\"Role\":2}," +
                "{\"Username\":\"b2\",\"Password\":\"" + PasswordHasher.Hash("2222") + "\",\"Role\":2}]");
            var umKeep = new UserManager();
            Check("dev+双业务管理员保留 dev", umKeep.Login(UserRole.Administrator, "dev", "dev123").Success);
            Check("dev+双业务管理员保留第一个(b1)", umKeep.Login(UserRole.Administrator, "b1", "1111").Success);
            Check("dev+双业务管理员丢弃第二个(b2)", !umKeep.Login(UserRole.Administrator, "b2", "2222").Success);
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

            // ── V1.81 可视化连线：Validator 新建拦截 + 序列化往返 ──
            var existing = IoOutputChannelRemap.ParseAll(
                "0x2000@0x00->0x2009@0x00;0x2001@0x05->0x2009@0x07", out err);
            string verr;
            Check("V181合法新连线通过", IoRemapValidator.ValidateNewMapping(
                existing, 0x2002, 3, 0x2009, 1, out verr) && verr == null);
            Check("V181空表+null表都放行",
                IoRemapValidator.ValidateNewMapping(null, 0x2000, 0, 0x2009, 0, out verr)
                && IoRemapValidator.ValidateNewMapping(new List<IoOutputChannelRemap>(), 0x2000, 0, 0x2009, 0, out verr));
            Check("V181重复源被拦截",
                !IoRemapValidator.ValidateNewMapping(existing, 0x2000, 0, 0x2009, 2, out verr) && verr != null);
            Check("V181重复目标被拦截（独占）",
                !IoRemapValidator.ValidateNewMapping(existing, 0x2003, 1, 0x2009, 0, out verr) && verr != null && verr.Contains("占用"));
            Check("V181自环被拦截",
                !IoRemapValidator.ValidateNewMapping(existing, 0x2000, 1, 0x2000, 1, out verr) && verr != null);
            Check("V181源通道16越界被拦截",
                !IoRemapValidator.ValidateNewMapping(existing, 0x2000, 16, 0x2009, 3, out verr) && verr != null);
            Check("V181目标通道-1越界被拦截",
                !IoRemapValidator.ValidateNewMapping(existing, 0x2000, 1, 0x2009, -1, out verr) && verr != null);
            Check("V181老配置多源同目标照常加载（只拦新建不拦加载）",
                IoOutputChannelRemap.ParseAll("0x2000@0x00->0x2009@0x00;0x2001@0x01->0x2009@0x00", out err).Count == 2);

            var pruned = IoRemapValidator.WithoutSource(existing, 0x2000, 0);
            Check("V181按源删除剩1条且不动原表",
                pruned.Count == 1 && existing.Count == 2 && pruned[0].SourceRegister == 0x2001);
            Check("V181删不存在的源返回等价拷贝",
                IoRemapValidator.WithoutSource(existing, 0x2008, 0).Count == 2);
            Check("V181null表删除返回空表",
                IoRemapValidator.WithoutSource(null, 0x2000, 0).Count == 0);
            Check("V181按源查找命中/落空",
                IoRemapValidator.FindBySource(existing, 0x2001, 5) != null
                && IoRemapValidator.FindBySource(existing, 0x2008, 0) == null);
            Check("V181目标占用判断",
                IoRemapValidator.IsTargetUsed(existing, 0x2009, 7)
                && !IoRemapValidator.IsTargetUsed(existing, 0x2009, 3));

            string ser = IoRemapValidator.Serialize(existing);
            Check("V181序列化格式与解析器同口径",
                ser == "0x2000@0x00->0x2009@0x00;0x2001@0x05->0x2009@0x07");
            var roundtrip = IoOutputChannelRemap.ParseAll(ser, out err);
            Check("V181序列化→解析往返一致",
                err == null && roundtrip.Count == 2
                && roundtrip[1].SourceRegister == 0x2001 && roundtrip[1].TargetChannel == 7);
            Check("V181空表序列化为空串", IoRemapValidator.Serialize(null) == "" && IoRemapValidator.Serialize(new List<IoOutputChannelRemap>()) == "");
            Check("V181单端描述格式", IoRemapValidator.Describe(0x2000, 10) == "0x2000@0x0A");

            // ── V1.81 可视化连线：Catalog 点位池（缺省 72/80/160 配置） ──
            var defCfg = new DeviceConfig();
            var srcs = IoRemapCatalog.BuildSourceEndpoints(defCfg);
            var spareTgts = IoRemapCatalog.BuildTargetEndpoints(defCfg, IoRemapTargetPool.SpareOnly);
            var freeTgts = IoRemapCatalog.BuildTargetEndpoints(defCfg, IoRemapTargetPool.AllFreeOutputs);
            Check("V181源池=全部输出160路", srcs.Count == 160);
            Check("V181备用目标池=16路", spareTgts.Count == 16);
            Check("V181全空闲目标池=160路", freeTgts.Count == 160);
            Check("V181源池首点Y000@0x2000 bit0",
                srcs.Count > 0 && srcs[0].IoName == "Y000" && srcs[0].Register == 0x2000 && srcs[0].Channel == 0);
            Check("V181载台上电-1落0x2004 bit8",
                srcs.Exists(e => e.DeviceName == "载台上电-1" && e.Register == 0x2004 && e.Channel == 8));
            Check("V181备用首点Y220@0x2009 bit0",
                spareTgts.Count > 0 && spareTgts[0].IoName == "Y220" && spareTgts[0].Register == 0x2009 && spareTgts[0].Channel == 0);
            Check("V181分组标题三类齐全",
                IoRemapCatalog.GroupTitleOf(IoFunction.VacuumValve) == "真空电磁阀"
                && IoRemapCatalog.GroupTitleOf(IoFunction.CarrierPower) == "载台上电"
                && IoRemapCatalog.GroupTitleOf(IoFunction.Unknown) == "预留输出");
            Check("V181非法配置返回空池不抛",
                IoRemapCatalog.BuildSourceEndpoints(new DeviceConfig { TotalBarometers = 72, TotalInputs = 1, TotalOutputs = 1 }).Count == 0);
            Check("V181null配置返回空池不抛", IoRemapCatalog.BuildSourceEndpoints(null).Count == 0);

            // ── V1.81.1 表格弹窗失焦守卫（反射直调 OnDeactivate，无需 Show 真窗） ──
            // 背景：OpenVisual 用 ShowDialog(this) 开模态连线页，模态激活瞬间表格弹窗失焦，
            // 无守卫时 OnDeactivate 自杀会连带 owned 模态窗一起销毁（现场"进不去"）。
            var mDeact = typeof(IoMappingEditorPopup).GetMethod("OnDeactivate",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var fVisual = typeof(IoMappingEditorPopup).GetField("_visualOpen",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var popClose = new IoMappingEditorPopup("", new DeviceConfig());
            mDeact.Invoke(popClose, new object[] { EventArgs.Empty });
            Check("V181失焦自关旧行为保留（点外部取消）", popClose.IsDisposed);
            try { popClose.Dispose(); } catch { }
            var popKeep = new IoMappingEditorPopup("", new DeviceConfig());
            fVisual.SetValue(popKeep, true);
            mDeact.Invoke(popKeep, new object[] { EventArgs.Empty });
            Check("V181连线页打开中失焦不自杀", !popKeep.IsDisposed);
            try { popKeep.Dispose(); } catch { }

            // ── V1.81.2 连线窗结构锁（纯构造+反射，不 Show 真窗） ──
            var vform = new IoRemapVisualForm(new DeviceConfig());
            Check("V181连线窗一律屏幕居中", vform.StartPosition == FormStartPosition.CenterScreen);
            var vgraph = typeof(IoRemapVisualForm).GetField("_graph",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(vform);
            var vcanvas = vgraph.GetType().GetField("_canvas",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(vgraph);
            bool vdbl = (bool)typeof(Control).GetProperty("DoubleBuffered",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(vcanvas, null);
            Check("V181画布双缓冲关闭（直画屏幕DC防GDI离屏慢）", vdbl == false);
            try { vform.Dispose(); } catch { }
            var mEllip = typeof(IoRemapGraphControl).GetMethod("Ellipsize",
                BindingFlags.NonPublic | BindingFlags.Static);
            using (var bmp1 = new Bitmap(1, 1))
            using (var gg = Graphics.FromImage(bmp1))
            using (var f9 = new Font("微软雅黑", 9f))
            {
                string fit = (string)mEllip.Invoke(null, new object[] { gg, "Y000 真空电磁阀-1", f9, 10000 });
                Check("V181装得下原样返回", fit == "Y000 真空电磁阀-1");
                string cut = (string)mEllip.Invoke(null, new object[] { gg, "Y000 真空电磁阀-1", f9, 60 });
                Check("V181装不下截断加…且不超宽",
                    cut.EndsWith("…") && TextRenderer.MeasureText(cut, f9).Width <= 60);
                string dot = (string)mEllip.Invoke(null, new object[] { gg, "Y000 真空电磁阀-1", f9, 0 });
                Check("V181零宽保底空串", dot == "");
            }

            // ── V1.81.3 徽标/截断布局态预计算（SetData+ComputeLayout 纯数学，
            // FitRows 用内存 DC 量字，全程无句柄无弹窗，runner 里安全） ──
            var gtest = new IoRemapGraphControl();
            var gcfg = new DeviceConfig();
            var gsrcs = IoRemapCatalog.BuildSourceEndpoints(gcfg);
            var gtgts = IoRemapCatalog.BuildTargetEndpoints(gcfg, IoRemapTargetPool.SpareOnly);
            var gmaps = IoOutputChannelRemap.ParseAll("0x2000@0x00->0x2009@0x00", out _);
            gtest.SetData(gsrcs, gtgts, gmaps);
            var gtGraph = typeof(IoRemapGraphControl);
            var mLayout = gtGraph.GetMethod("ComputeLayout", BindingFlags.NonPublic | BindingFlags.Instance);
            var mFit = gtGraph.GetMethod("FitRows", BindingFlags.NonPublic | BindingFlags.Instance);
            mLayout.Invoke(gtest, new object[] { 1f });
            var gsrcRows = (System.Collections.IList)gtGraph.GetField("_srcRows",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(gtest);
            var gtgtRows = (System.Collections.IList)gtGraph.GetField("_tgtRows",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(gtest);
            using (var bmpF = new Bitmap(1, 1))
            using (var gf = Graphics.FromImage(bmpF))
            using (var ff = new Font("微软雅黑", 9f))
            using (var fb = new Font("微软雅黑", 8f))
            {
                mFit.Invoke(gtest, new object[] { gf, gsrcRows, true });
                mFit.Invoke(gtest, new object[] { gf, gtgtRows, false });
                string badgeHit = null;
                string badgeMiss = "哨兵";
                int fitBad = 0;
                int nodeCount = 0;
                foreach (System.Collections.IList pool in new object[] { gsrcRows, gtgtRows })
                {
                    foreach (var r in pool)
                    {
                        var rt = r.GetType();
                        if ((bool)rt.GetField("IsHeader").GetValue(r)) continue;
                        nodeCount++;
                        var ep = rt.GetField("Endpoint").GetValue(r);
                        var et = ep.GetType();
                        int greg = (ushort)et.GetField("Register").GetValue(ep);
                        int gch = (int)et.GetField("Channel").GetValue(ep);
                        string main = (string)rt.GetField("MainText").GetValue(r);
                        string badge = (string)rt.GetField("Badge").GetValue(r);
                        var bounds = (Rectangle)rt.GetField("Bounds").GetValue(r);
                        if (greg == 0x2000 && gch == 0) badgeHit = badge + "";
                        if (greg == 0x2000 && gch == 1) badgeMiss = badge + "";
                        int w = TextRenderer.MeasureText(main ?? "", ff).Width
                            + (badge != null ? TextRenderer.MeasureText(badge, fb).Width + 6 : 0);
                        if (string.IsNullOrEmpty(main) || w > bounds.Width - 12) fitBad++;
                    }
                }
                Check("V181徽标随映射预计算（有/无）", badgeHit == "→0x2009@0x00" && badgeMiss == "");
                Check("V181缺省176行截断后全装框", nodeCount == 176 && fitBad == 0);

                // 60 字超长名：必截断且装框（杜绝以后加长设备名撑爆节点）
                var longEp = new IoRemapEndpoint
                {
                    Register = 0x2000, Channel = 0, IoName = "Y000",
                    DeviceName = new string('X', 60), Function = IoFunction.Unknown, IoId = 1
                };
                gtest.SetData(new List<IoRemapEndpoint> { longEp }, gtgts, null);
                mLayout.Invoke(gtest, new object[] { 1f });
                var loneRows = (System.Collections.IList)gtGraph.GetField("_srcRows",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(gtest);
                mFit.Invoke(gtest, new object[] { gf, loneRows, true });
                string loneMain = "";
                foreach (var r in loneRows)
                {
                    var rt = r.GetType();
                    if ((bool)rt.GetField("IsHeader").GetValue(r)) continue;
                    loneMain = (string)rt.GetField("MainText").GetValue(r);
                }
                Check("V181超长名截断加…且装框",
                    loneMain.EndsWith("…") && TextRenderer.MeasureText(loneMain, ff).Width <= 250 - 12);
            }
            try { gtest.Dispose(); } catch { }

            // ── V1.81.4 连线包围盒裁剪（纯静态判交，反射直调；中段穿屏不断是断连根因） ──
            var mCross = typeof(IoRemapGraphControl).GetMethod("LinkCrossesClip",
                BindingFlags.NonPublic | BindingFlags.Static);
            Check("V181连线裁剪两端可见画", (bool)mCross.Invoke(null, new object[] {
                new Rectangle(10, 100, 250, 26), new Rectangle(500, 100, 250, 26),
                new Rectangle(0, 0, 800, 600) }));
            Check("V181长连线中段穿屏也画", (bool)mCross.Invoke(null, new object[] {
                new Rectangle(10, 0, 250, 26), new Rectangle(500, 2000, 250, 26),
                new Rectangle(0, 900, 800, 600) }));
            Check("V181连线全在屏外不画", !(bool)mCross.Invoke(null, new object[] {
                new Rectangle(10, 0, 250, 26), new Rectangle(500, 100, 250, 26),
                new Rectangle(0, 900, 800, 600) }));
            Check("V181连线水平无交不画", !(bool)mCross.Invoke(null, new object[] {
                new Rectangle(10, 100, 100, 26), new Rectangle(200, 100, 100, 26),
                new Rectangle(500, 0, 800, 600) }));

            // ── V1.82 缩放纯函数（反射私静，无句柄可测；步进×1.2，钳制0.5~2.5） ──
            var mZoomF = typeof(IoRemapGraphControl).GetMethod("ZoomFactorOf",
                BindingFlags.NonPublic | BindingFlags.Static);
            var mClamp = typeof(IoRemapGraphControl).GetMethod("ClampZoom",
                BindingFlags.NonPublic | BindingFlags.Static);
            var mScroll = typeof(IoRemapGraphControl).GetMethod("ZoomScrollOf",
                BindingFlags.NonPublic | BindingFlags.Static);
            Check("V182放大一步×1.2",
                Math.Abs((float)mZoomF.Invoke(null, new object[] { 1f, 1 }) - 1.2f) < 0.001);
            Check("V182缩小一步÷1.2",
                Math.Abs((float)mZoomF.Invoke(null, new object[] { 1f, -1 }) - 1f / 1.2f) < 0.001);
            Check("V182上钳2.5",
                Math.Abs((float)mZoomF.Invoke(null, new object[] { 2.5f, 1 }) - 2.5f) < 0.001);
            Check("V182下钳0.5",
                Math.Abs((float)mZoomF.Invoke(null, new object[] { 0.5f, -1 }) - 0.5f) < 0.001);
            Check("V182钳制上", Math.Abs((float)mClamp.Invoke(null, new object[] { 10f }) - 2.5f) < 0.001);
            Check("V182钳制下", Math.Abs((float)mClamp.Invoke(null, new object[] { 0.1f }) - 0.5f) < 0.001);
            Check("V182钳制内", Math.Abs((float)mClamp.Invoke(null, new object[] { 1.5f }) - 1.5f) < 0.001);
            Check("V182锚定跟随",
                (int)mScroll.Invoke(null, new object[] { 100, 200, 1.2 }) == 260);
            Check("V182锚定零点",
                (int)mScroll.Invoke(null, new object[] { 0, 0, 0.5 }) == 0);
            Check("V182锚定不负",
                (int)mScroll.Invoke(null, new object[] { 10, 0, 0.1 }) == 0);
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
            StationSettingsCache.Save(new StationCacheEntry { DeviceId = 0, SerialNumber = "野" });
            StationSettingsCache.Save(new StationCacheEntry { DeviceId = -3, SerialNumber = "野" });
            Check("非法编号拒绝入库", StationSettingsCache.Get(0) == null && StationSettingsCache.Get(-3) == null);

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
            File.WriteAllText(ProjectProfile.ResolveDataPath("StationSettings.json", true), "{broken json");
            ResetStationCache();
            Check("损坏文件读null不抛", StationSettingsCache.Get(1) == null);
            File.WriteAllText(ProjectProfile.ResolveDataPath("StationSettings.json", true), "[]");
            ResetStationCache();
            Check("空数组读null", StationSettingsCache.Get(1) == null);
            File.WriteAllText(ProjectProfile.ResolveDataPath("StationSettings.json", true), "[{\"DeviceId\":0},{\"DeviceId\":-3},null]");
            ResetStationCache();
            Check("非法编号条目被过滤", StationSettingsCache.Get(0) == null);
            File.Delete(ProjectProfile.ResolveDataPath("StationSettings.json", true));
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
                    DisplayMode = "白场24h",
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
            Check("保存后文件存在", File.Exists(ProjectProfile.ResolveDataPath("Recipes.json", true)));

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
                Check("DisplayMode 往返一致", loaded[0].DisplayMode == "白场24h");
                Check("DisplayMode 缺值往返为 null", loaded[1].DisplayMode == null);
                Check("CreateTime 往返一致", loaded[0].CreateTime == new DateTime(2026, 8, 25, 10, 0, 0));
                Check("IsEnabled 往返一致(false)", loaded[1].IsEnabled == false);
            }

            File.WriteAllText(ProjectProfile.ResolveDataPath("Recipes.json", true), "{broken json");
            Check("损坏 json Load 返回 null(不抛异常)", RecipeStorage.Load() == null);

            File.WriteAllText(ProjectProfile.ResolveDataPath("Recipes.json", true), "[]");
            var empty = RecipeStorage.Load();
            Check("空数组 json Load 返回空列表(非 null)", empty != null && empty.Count == 0);

            File.WriteAllText(ProjectProfile.ResolveDataPath("Recipes.json", true), "null");
            var nulllit = RecipeStorage.Load();
            Check("json字面量null Load 返回空列表(非 null)", nulllit != null && nulllit.Count == 0);

            // SaveRecipe 新增/覆盖分支（覆盖确认框在 UI 层，单测只测无 UI 的落盘语义）
            var shared = new List<RecipeConfig>();
            var r = new RecipeConfig { Name = "新建配方", NegativePressure = -70m };
            Check("SaveRecipe 新增返回 true", RecipeStorage.SaveRecipe(shared, r, true));
            Check("新增后 Id 自动分配为 1", shared.Count == 1 && shared[0].Id == 1);
            Check("新增后立即落盘", File.Exists(ProjectProfile.ResolveDataPath("Recipes.json", true)));
            var r2 = new RecipeConfig { Name = "第二个配方" };
            Check("第二条新增 Id=2", RecipeStorage.SaveRecipe(shared, r2, true) && shared[1].Id == 2);
            // V1.62：删中间配方后新增不许撞号（Max+1，不是 Count+1）
            shared.RemoveAt(0);
            var r3 = new RecipeConfig { Name = "第三个配方" };
            Check("删Id=1后新增Id=3不撞号", RecipeStorage.SaveRecipe(shared, r3, true) && shared[1].Id == 3);
            Check("参数 recipe=null 返回 false", !RecipeStorage.SaveRecipe(shared, null, true));
            Check("参数 list=null 返回 false", !RecipeStorage.SaveRecipe(null, r, true));
            Check("空名配方拒绝", !RecipeStorage.SaveRecipe(shared, new RecipeConfig { Name = "  " }, true));
            // 同名语义：FindDuplicateIndex 定位 + overwrite 开关
            Check("同名定位忽略大小写",
                RecipeStorage.FindDuplicateIndex(shared, "第二个配方") == 0
                && RecipeStorage.FindDuplicateIndex(shared, "不存在") < 0);
            int keepId = shared[0].Id;
            var dup = new RecipeConfig { Name = "第二个配方", NegativePressure = -11m };
            Check("同名不覆盖被拒且列表不动",
                !RecipeStorage.SaveRecipe(shared, dup, false) && shared.Count == 2
                && shared[0].NegativePressure != -11m);
            Check("同名覆盖保留原Id",
                RecipeStorage.SaveRecipe(shared, dup, true) && shared.Count == 2
                && shared[0].Id == keepId && shared[0].NegativePressure == -11m);
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
                lines[0] == "时间,批号,SN,配方,设备编号,事件,结果,详情,压力(kPa),温度(°C),电流(A)");
            Check("两条事件均已落盘", lines.Length >= 3);
            if (lines.Length < 3) return;

            Check("时间列 yyyy-MM-dd HH:mm:ss 格式开头",
                System.Text.RegularExpressions.Regex.IsMatch(lines[1], @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2},"));
            Check("批号/SN/配方/编号列序正确", lines[1].Contains(",LOT20260825,,,3,"));
            Check("事件结果列正确", lines[1].Contains(",3,报警,,"));
            Check("含逗号详情被转义包裹", lines[1].Contains("\"压力超限,需关注\""));
            Check("压力列=-85.5", lines[1].Contains(",-85.5,"));
            Check("温度列一位小数 66.6", lines[1].Contains(",66.6,"));
            // V1.74：电流列追加末尾，老调用无电流记空（行尾多一个逗号）
            Check("无电流时电流列留空(行尾逗号)", lines[1].EndsWith("66.6,"));
            // 压力/温度/电流都为空时，详情后三个空字段让行尾必然是 ",,,"（CSV 列留空）
            Check("可选压力/温度/电流缺省时留空(行尾,,,)", lines[2].EndsWith(",急停,,手动触发,,,"));

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
            Check("全null字段仍11列不抛", last.Split(',').Length >= 11 && last.Contains(",-1,"));
            Check("温度一位小数格式化33.6", last.Contains(",33.6,"));
            Check("压力整数12原样写", last.Contains(",12,"));
            TestEventLogger.Write("LOTX", 1, "启动", "ok", null, null);

            // V1.74：电流列（有数两位小数；NaN 按空处理，CSV 里不许出现"NaN"字样）
            TestEventLogger.Write("LOTC", 7, "上电", "载台上电", null, null, 0.424f);
            string[] linesC = ReadAllLinesShared(file);
            string lastC = linesC[linesC.Length - 1];
            Check("电流有数写两位小数", lastC.EndsWith(",0.42"));
            Check("电流列有数不断列", lastC.Split(',').Length >= 11);
            TestEventLogger.Write("LOTN", 8, "上电", "无表", null, null, float.NaN);
            string lastN = ReadAllLinesShared(file)[ReadAllLinesShared(file).Length - 1];
            Check("电流NaN记空不写NaN字样", lastN.EndsWith(",") && !lastN.Contains("NaN"));

            // V1.76：SN/配方/结果结构化列（11列：时间,批号,SN,配方,设备编号,事件,结果,详情,压力,温度,电流）
            TestEventLogger.Write("LOT2", 5, "完成", "到时", null, null, null, "SN001", "R-A", "PASS");
            string lastS = ReadAllLinesShared(file)[ReadAllLinesShared(file).Length - 1];
            Check("SN配方结果列序正确", lastS.Contains(",LOT2,SN001,R-A,5,完成,PASS,"));
            TestEventLogger.Write("LOT3", 0, "急停", "整机", null, null, null, null, null, null);
            string lastZ = ReadAllLinesShared(file)[ReadAllLinesShared(file).Length - 1];
            Check("整机行身份结果记空", lastZ.Contains(",LOT3,,,0,急停,,"));

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

            // ── 高度联动：面板高 +10 → 下链下移、上链锁定、差值由交接缝吸收（V1.77）──
            var tall = PanelLayoutConfig.LoadOrDefault();
            tall.PanelInnerHeight = 215;
            tall.ResolveAnchors();
            Check("高度+10: 设置按钮 Y 145→155", tall.RcSetButton.ToRectangle().Y == 155);
            Check("高度+10: SN 框 Y=93 不动(上链锁定，V1.77 改走自上而下链)",
                tall.RcSNValue.ToRectangle().Y == 93);
            Check("高度+10: 配方框 Y 118→128", tall.RcRecipeValue.ToRectangle().Y == 128);
            Check("高度+10: 交接缝吸收差值(SN→配方间距4→14)",
                tall.RcRecipeValue.ToRectangle().Y - (tall.RcSNValue.ToRectangle().Y + 21) == 14);
            Check("高度+10: 真空开/压力框 Y=67 不动(吊空闲下方)",
                tall.RcVacuumOpen.ToRectangle().Y == 67 && tall.RcPressureValue.ToRectangle().Y == 67);
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

            // ── 标签垂直居中：随目标框移动（V1.77：压力框定高位，标签不动）──
            Check("压力标签定高位不动(Y=70)",
                c.LabelPressurePosition.ToPoint().Y == 70 && tall.LabelPressurePosition.ToPoint().Y == 70,
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

            // ── V1.77 电流行：关=原来逐像素一致，开=下游下移21间距不变 ──
            // 纯代码默认（不读文件，零文件依赖）：new 出来就是 ShowCurrent=false 的原布局
            var cur = new PanelLayoutConfig();
            Check("电流行缺省关闭", cur.ShowCurrent == false);
            Check("关电流有效高=205/行高=225",
                cur.GetEffectiveInnerHeight() == 205 && cur.GetEffectiveRowHeight() == 225);
            cur.ShowCurrent = true;
            cur.ResolveAnchors();
            Check("开电流有效高226/行高246",
                cur.GetEffectiveInnerHeight() == 226 && cur.GetEffectiveRowHeight() == 246);
            var rcCur = cur.RcCurrentValue.ToRectangle();
            Check("开电流电流行 (65,90,85,21)",
                rcCur.X == 65 && rcCur.Y == 90 && rcCur.Width == 85 && rcCur.Height == 21,
                "实际 " + rcCur.ToString());
            Check("开电流SN下移114", cur.RcSNValue.ToRectangle().Y == 114);
            Check("开电流配方139且交接缝仍4",
                cur.RcRecipeValue.ToRectangle().Y == 139
                && cur.RcRecipeValue.ToRectangle().Y - (cur.RcSNValue.ToRectangle().Y + 21) == 4);
            Check("开电流按钮166/延时168/193",
                cur.RcSetButton.ToRectangle().Y == 166
                && cur.RcDelayStartValue.ToRectangle().Y == 168
                && cur.RcDelayArriveValue.ToRectangle().Y == 193);
            Check("开电流压力/真空关不动67",
                cur.RcPressureValue.ToRectangle().Y == 67 && cur.RcVacuumOpen.ToRectangle().Y == 67);
            Check("开电流标签93", cur.LabelCurrentPosition.ToPoint().Y == 93);
            Check("开电流SN标签跟随117", cur.LabelSnPosition.ToPoint().Y == 117);
            // 关回去：与默认快照零差异（开关往返不漂移）
            cur.ShowCurrent = false;
            cur.ResolveAnchors();
            Check("开关往返布局零漂移", DictionaryEquals(snap1, SnapshotLayout(cur)));
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
            string cfgPath = ProjectProfile.ResolveDataPath("HomeLayout.json", true);
            if (File.Exists(cfgPath)) File.Delete(cfgPath);

            var c = HomeLayoutConfig.LoadOrDefault();
            Check("HomeLayout 默认配置非 null", c != null);
            if (c == null) return;
            Check("TopBarHeight 默认 40", c.TopBarHeight == 40);
            Check("MenuHeight 默认 50", c.MenuHeight == 50);
            Check("RightPanelWidth 默认 240(V1.65比例时代的编辑器基准)", c.RightPanelWidth == 240);
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
                Check("损坏文件回退默认40/50/240/30",
                    fallback.TopBarHeight == 40 && fallback.MenuHeight == 50
                    && fallback.RightPanelWidth == 240 && fallback.StatusBarHeight == 30);
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
            Check("默认未装破空阀（按钮隐藏+泄压拦）",
                cfg.VentValveEnabled == false && cfg.VentValveDoPoint == 0);
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
            var bnc = bn.Clone(); // null 数组不抛且回非空（下游 Length/[0] 不判空）
            Check("Clone空数组不抛且非空", bnc.InputStatus != null && bnc.OutputStatus != null
                && bnc.InputStatus.Length == 0 && bnc.OutputStatus.Length == 0);

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

            // ── V1.66：启动风险提示文案（烧屏：0时长=无限点亮只能手动停/空SN=断链，只警告不拦截） ──
            Check("两类都空返回空串",
                AgingSequencer.BuildStartWarningText(new int[0], new int[0]) == "");
            Check("null输入返回空串",
                AgingSequencer.BuildStartWarningText(null, null) == "");
            string warnZero = AgingSequencer.BuildStartWarningText(new[] { 3, 5 }, new int[0]);
            Check("0时长警告含工位号",
                warnZero.Contains("3") && warnZero.Contains("5"));
            string warnSn = AgingSequencer.BuildStartWarningText(new int[0], new[] { 7 });
            Check("空SN警告含工位号",
                warnSn.Contains("7") && warnSn.Contains("SN"));
            string warnBoth = AgingSequencer.BuildStartWarningText(new[] { 1 }, new[] { 2 });
            Check("两类并存拼两段",
                warnBoth.Contains("1") && warnBoth.Contains("2"));

            // ── V1.66：超温全线联停判定（默认关=现状只记日志；全机单探头只能全线停） ──
            Check("开关关闭永不停",
                !AgingSequencer.IsFanOverTempShutdown(999f, 60f, false));
            Check("上限0=不启用",
                !AgingSequencer.IsFanOverTempShutdown(999f, 0f, true));
            Check("恰等上限不停(边界>)",
                !AgingSequencer.IsFanOverTempShutdown(60f, 60f, true));
            Check("超限+开关开+上限>0才停",
                AgingSequencer.IsFanOverTempShutdown(60.1f, 60f, true));
        }

        // =====================================================================
        // 12b. 工艺策略纯函数（V1.67 一期 L2 策略层：7 个待确认点全部可配）
        //     铁律锁：缺省=现状行为；非法值兜底现状；策略 key 三处同步
        //     （PolicyKeys/DeviceConfig 属性/SettingsForm 名单）防"存了用不上"。
        // =====================================================================
        private static void PolicyTests()
        {
            // ── BuildStartBlockText：Warn 不拦，Block 才拦 ──
            Check("Warn模式永不阻断",
                AgingSequencer.BuildStartBlockText(new[] { 1 }, new[] { 2 },
                    ZeroDurationPolicy.Warn, EmptySnPolicy.Warn) == "");
            Check("Block模式0时长阻断含工位号",
                AgingSequencer.BuildStartBlockText(new[] { 3 }, new int[0],
                    ZeroDurationPolicy.Block, EmptySnPolicy.Warn).Contains("3"));
            Check("Block模式空SN阻断含工位号",
                AgingSequencer.BuildStartBlockText(new int[0], new[] { 7 },
                    ZeroDurationPolicy.Warn, EmptySnPolicy.Block).Contains("7"));
            Check("Block模式两类并存拼两段",
                AgingSequencer.BuildStartBlockText(new[] { 1 }, new[] { 2 },
                    ZeroDurationPolicy.Block, EmptySnPolicy.Block).Contains("1")
                && AgingSequencer.BuildStartBlockText(new[] { 1 }, new[] { 2 },
                    ZeroDurationPolicy.Block, EmptySnPolicy.Block).Contains("2"));
            Check("Block模式两类都空不阻断",
                AgingSequencer.BuildStartBlockText(new int[0], new int[0],
                    ZeroDurationPolicy.Block, EmptySnPolicy.Block) == "");
            Check("Block模式null不阻断",
                AgingSequencer.BuildStartBlockText(null, null,
                    ZeroDurationPolicy.Block, EmptySnPolicy.Block) == "");

            // ── MapAlarmResult：设备异常/Q19 责任/DI 口径 ──
            Check("失联永远设备异常(策略管不着)",
                AgingSequencer.MapAlarmResult(false, true, VacuumFailKind.FixtureAlarm) == "设备异常");
            Check("真空类+产品责任=FAIL(现状)",
                AgingSequencer.MapAlarmResult(true, true, VacuumFailKind.ProductFail) == "FAIL");
            Check("真空类+治具责任=装夹异常",
                AgingSequencer.MapAlarmResult(true, true, VacuumFailKind.FixtureAlarm) == "装夹异常");
            Check("DI触点永远FAIL(非真空类，Q19不改它)",
                AgingSequencer.MapAlarmResult(true, false, VacuumFailKind.FixtureAlarm) == "FAIL");
            Check("DI触点现状同样FAIL",
                AgingSequencer.MapAlarmResult(true, false, VacuumFailKind.ProductFail) == "FAIL");

            // ── ComputeResumeDurationSeconds：补欠的产能，断电期间不计 ──
            DateTime on = new DateTime(2026, 9, 11, 10, 0, 0);
            Check("不限时长续跑仍不限(返回0)",
                AgingSequencer.ComputeResumeDurationSeconds(0, on, on.AddHours(1)) == 0);
            Check("还没上电整段重跑",
                AgingSequencer.ComputeResumeDurationSeconds(3600, DateTime.MinValue, on) == 3600);
            Check("正常剩余=总-已跑",
                AgingSequencer.ComputeResumeDurationSeconds(3600, on, on.AddMinutes(10)) == 3000);
            Check("断电前已到时给1秒(下一轮即完成)",
                AgingSequencer.ComputeResumeDurationSeconds(3600, on, on.AddHours(2)) == 1);
            Check("时钟回拨按没跑过算",
                AgingSequencer.ComputeResumeDurationSeconds(3600, on, on.AddMinutes(-5)) == 3600);

            // ── ValidatePolicyCombination：矛盾组合锁 ──
            Check("全缺省组合合法",
                AgingSequencer.ValidatePolicyCombination(false, 0f, CompletionAction.PowerOffOnly, 0, false) == null);
            Check("联停开但上限0被拦",
                AgingSequencer.ValidatePolicyCombination(true, 0f, CompletionAction.PowerOffOnly, 0, false) != null);
            Check("联停开+上限>0放行",
                AgingSequencer.ValidatePolicyCombination(true, 60f, CompletionAction.PowerOffOnly, 0, false) == null);
            Check("泄压选但点位0被拦",
                AgingSequencer.ValidatePolicyCombination(false, 0f, CompletionAction.PowerOffAndVent, 0, true) != null);
            Check("蜂鸣+泄压都要但点位0同样被拦",
                AgingSequencer.ValidatePolicyCombination(false, 0f, CompletionAction.PowerOffVentAndBeep, 0, true) != null);
            Check("泄压+有点位放行",
                AgingSequencer.ValidatePolicyCombination(false, 0f, CompletionAction.PowerOffAndVent, 225, true) == null);
            Check("纯蜂鸣无点位放行(无硬件要求)",
                AgingSequencer.ValidatePolicyCombination(false, 0f, CompletionAction.PowerOffAndBeep, 0, false) == null);
            // 【V1.73】破空阀开关：无阀时泄压组合直接拦（点位对了也没用，先开开关）
            Check("无阀+泄压被拦（点位对了也没用）",
                AgingSequencer.ValidatePolicyCombination(false, 0f, CompletionAction.PowerOffAndVent, 225, false) != null);
            Check("无阀+蜂鸣泄压被拦",
                AgingSequencer.ValidatePolicyCombination(false, 0f, CompletionAction.PowerOffVentAndBeep, 225, false) != null);
            Check("无阀+纯蜂鸣放行（蜂鸣无硬件要求）",
                AgingSequencer.ValidatePolicyCombination(false, 0f, CompletionAction.PowerOffAndBeep, 0, false) == null);
            // 【复查补齐】破空阀点位碰撞纯函数（TotalInputs=16/72台：输出区 17~160）
            Check("点位0=未配置不拦",
                AgingSequencer.ValidateVentPointCollision(0, 16, 72) == null);
            Check("点位落在工位阀区被拦(17)",
                AgingSequencer.ValidateVentPointCollision(17, 16, 72) != null);
            Check("点位落在载台电区被拦(160)",
                AgingSequencer.ValidateVentPointCollision(160, 16, 72) != null);
            Check("预留点位放行(200)",
                AgingSequencer.ValidateVentPointCollision(200, 16, 72) == null);
            Check("输入区点位放行(8)",
                AgingSequencer.ValidateVentPointCollision(8, 16, 72) == null);

            // ── ProjectPolicyStore.ParseValue：大小写兼容，非法回null ──
            Check("枚举正常解析",
                Equals(ProjectPolicyStore.ParseValue(typeof(ZeroDurationPolicy), "Block"), ZeroDurationPolicy.Block));
            Check("枚举小写兼容",
                Equals(ProjectPolicyStore.ParseValue(typeof(ZeroDurationPolicy), "block"), ZeroDurationPolicy.Block));
            Check("枚举非法回null(调用方保持缺省)",
                ProjectPolicyStore.ParseValue(typeof(ZeroDurationPolicy), "xxx") == null);
            Check("枚举空回null",
                ProjectPolicyStore.ParseValue(typeof(ZeroDurationPolicy), "  ") == null);
            Check("布尔解析",
                Equals(ProjectPolicyStore.ParseValue(typeof(bool), "true"), true));
            Check("整数非法回null",
                ProjectPolicyStore.ParseValue(typeof(int), "abc") == null);

            // ── 名单同步锁：PolicyKeys/DeviceConfig属性/下拉选项三处一一对应 ──
            bool keysOk = true;
            foreach (string k in ProjectPolicyStore.PolicyKeys)
            {
                if (typeof(DeviceConfig).GetProperty(k) == null) keysOk = false;
            }
            Check("PolicyKeys 每个都有 DeviceConfig 同名属性(防存了用不上)", keysOk);
            Check("PolicyKeys 含超温联停开关(老 key 收编)",
                ProjectPolicyStore.PolicyKeys.Contains("FanTempShutdownEnabled"));
            bool optsOk = true;
            foreach (string k in ProjectPolicyStore.PolicyKeys)
            {
                // VentValveDoPoint 是数字项、FanTempShutdownEnabled/SkipVacuum/DisplayModeEnabled
                // 是布尔项、MesTriggers/MesFieldMap/MesStaticFields/CustomAlarmRules/
                // CompleteExpression/ReportColumns/DisplayModes 是自由文本
                // （V1.68/V1.69/V1.74/V1.75），都无策略下拉
                if (k == "VentValveDoPoint" || k == "FanTempShutdownEnabled" || k == "SkipVacuum"
                    || k == "DisplayModeEnabled"
                    || k == "MesTriggers" || k == "MesFieldMap" || k == "MesStaticFields"
                    || k == "CustomAlarmRules" || k == "CompleteExpression"
                    || k == "ReportColumns" || k == "DisplayModes") continue;
                Tuple<string, string>[] opts;
                if (!ProjectPolicyStore.EnumOptions.TryGetValue(k, out opts) || opts.Length < 2) { optsOk = false; break; }
                var prop = typeof(DeviceConfig).GetProperty(k);
                foreach (var o in opts)
                {
                    if (ProjectPolicyStore.ParseValue(prop.PropertyType, o.Item2) == null) { optsOk = false; break; }
                }
            }
            Check("每个枚举策略都有≥2个下拉选项且选项全可解析", optsOk);

            // ── DeviceConfig 缺省锁：不配=和以前一模一样 ──
            var dc = new DeviceConfig();
            Check("缺省0时长=Warn/空SN=Warn/断连=LogOnly",
                dc.ZeroDurationPolicy == ZeroDurationPolicy.Warn
                && dc.EmptySnPolicy == EmptySnPolicy.Warn
                && dc.FanDisconnectPolicy == FanDisconnectPolicy.LogOnly);
            Check("缺省真空责任=ProductFail/完成=AutoPass/断电=RestartFull",
                dc.VacuumFailKind == VacuumFailKind.ProductFail
                && dc.CompletionJudgePolicy == CompletionJudgePolicy.AutoPass
                && dc.PowerLossPolicy == PowerLossPolicy.RestartFull);
            Check("缺省失压=StopOnLoss/完成动作=PowerOffOnly/破空点位=0",
                dc.AgingPressureLossPolicy == AgingPressureLossPolicy.StopOnLoss
                && dc.CompletionAction == CompletionAction.PowerOffOnly
                && dc.VentValveDoPoint == 0);
            Check("缺省身份口径=记录现值",
                dc.EventIdentityMode == EventIdentityMode.RecordTime);

            // ── V1.76：事件身份口径（开关解析 + ResolveEventIdentity 纯函数）──
            Check("身份枚举解析StartSnapshot",
                Equals(ProjectPolicyStore.ParseValue(typeof(EventIdentityMode), "StartSnapshot"), EventIdentityMode.StartSnapshot));
            Check("身份枚举小写兼容",
                Equals(ProjectPolicyStore.ParseValue(typeof(EventIdentityMode), "startsnapshot"), EventIdentityMode.StartSnapshot));
            Check("身份枚举非法回null(调用方保持缺省)",
                ProjectPolicyStore.ParseValue(typeof(EventIdentityMode), "xxx") == null);
            string oSn, oRecipe;
            DeviceManager.ResolveEventIdentity(EventIdentityMode.RecordTime, "A", "RA", "B", "RB", out oSn, out oRecipe);
            Check("现值模式取现值", oSn == "B" && oRecipe == "RB");
            DeviceManager.ResolveEventIdentity(EventIdentityMode.StartSnapshot, "A", "RA", "B", "RB", out oSn, out oRecipe);
            Check("定格模式取启动值", oSn == "A" && oRecipe == "RA");
            DeviceManager.ResolveEventIdentity(EventIdentityMode.StartSnapshot, "", "", "B", "RB", out oSn, out oRecipe);
            Check("定格无快照回退现值", oSn == "B" && oRecipe == "RB");
            DeviceManager.ResolveEventIdentity(EventIdentityMode.StartSnapshot, "", "RA", "B", "RB", out oSn, out oRecipe);
            Check("半快照仍用快照", oSn == "" && oRecipe == "RA");
            DeviceManager.ResolveEventIdentity(EventIdentityMode.RecordTime, null, null, null, null, out oSn, out oRecipe);
            Check("全null转空串不抛", oSn == "" && oRecipe == "");

            // ── 快照新字段缺省锁：老快照(无阶段字段)按整段重跑，安全回退 ──
            var oldSnap = new TestSessionStation { DeviceId = 1, DurationSeconds = 100 };
            Check("老快照Phase缺省0(None≠Aging)",
                oldSnap.Phase == 0 && oldSnap.PowerOnTime == default(DateTime));

            // ── SettingsForm.ValidateValue 策略分支（反射，防手改文件脏值入库） ──
            const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Static;
            var miValidate = typeof(SettingsForm).GetMethod("ValidateValue", Flags);
            Check("反射找到 ValidateValue", miValidate != null);
            if (miValidate != null)
            {
                object[] args = new object[] { "ZeroDurationPolicy", "Block", null };
                Check("策略合法值过",
                    (bool)miValidate.Invoke(null, args) == true);
                args = new object[] { "ZeroDurationPolicy", "xxx", null };
                Check("策略脏值被拦",
                    (bool)miValidate.Invoke(null, args) == false);
                args = new object[] { "VentValveDoPoint", "0", null };
                Check("破空点位0过(未配置合法)",
                    (bool)miValidate.Invoke(null, args) == true);
                args = new object[] { "VentValveDoPoint", "-1", null };
                Check("破空点位负数被拦",
                    (bool)miValidate.Invoke(null, args) == false);
            }

            // ── SettingsForm.NormalizePolicyValue（反射：脏值兜底回缺省） ──
            var miNorm = typeof(SettingsForm).GetMethod("NormalizePolicyValue", Flags);
            Check("反射找到 NormalizePolicyValue", miNorm != null);
            if (miNorm != null)
            {
                Check("小写归一到存储名",
                    (string)miNorm.Invoke(null, new object[] { "ZeroDurationPolicy", "block" }) == "Block");
                Check("脏值兜底回首选项(现状)",
                    (string)miNorm.Invoke(null, new object[] { "ZeroDurationPolicy", "xxx" }) == "Warn");
            }

            // ── SettingsForm.WrapTooltip（反射：40字换行） ──
            var miWrap = typeof(SettingsForm).GetMethod("WrapTooltip", Flags);
            Check("反射找到 WrapTooltip", miWrap != null);
            if (miWrap != null)
            {
                Func<string, string> wrap = s => (string)miWrap.Invoke(null, new object[] { s });
                Check("短文本原样返回",
                    wrap("报警压力阈值（kPa，如 -5）") == "报警压力阈值（kPa，如 -5）");
                Check("空返回空串", wrap("") == "" && wrap(null) == "");
                string long60 = new string('测', 60);
                string wrapped = wrap(long60);
                bool linesOk = true;
                foreach (string line in wrapped.Split(new[] { "\r\n" }, StringSplitOptions.None))
                {
                    if (line.Length > 40) linesOk = false;
                }
                Check("长文本换行且每行≤40字",
                    wrapped.Contains("\r\n") && linesOk
                    && wrapped.Replace("\r\n", "").Length == 60);
            }

            // ── ProjectProfile：非法名/重复/不存在切换全拒绝（运行目录隔离，见 EnterCleanDir） ──
            EnterCleanDir();
            Check("空名建项目被拒", !ProjectProfile.CreateProfile("  "));
            Check("路径穿越字符被拒", !ProjectProfile.CreateProfile("../逃逸"));
            Check("斜杠被拒", !ProjectProfile.CreateProfile("a/b"));
            // 【大扫荡】Delete/Switch/列表过滤同样防穿越（以前只有 Create 拦）：
            // DeleteProfile("..\\重要目录") 存在即递归删除，Switch 写成指针全盘带出 Projects。
            Check("删除穿越名被拒", !ProjectProfile.DeleteProfile(".."));
            Check("删除斜杠名被拒", !ProjectProfile.DeleteProfile("../重要目录"));
            Check("切换穿越名被拒", !ProjectProfile.SwitchTo(".."));
            Check("切换带路径名被拒", !ProjectProfile.SwitchTo("a/b"));
            Check("名单校验合法名过", ProjectProfile.IsValidProfileName("项目A_2"));
            Check("名单校验穿越名拒", !ProjectProfile.IsValidProfileName("..")
                && !ProjectProfile.IsValidProfileName("a\\b") && !ProjectProfile.IsValidProfileName(". .."));
            // 空/野目录不冒充项目（手丢 backup 文件夹切入后配方策略全空易误跑）。
            string junkDir = System.IO.Path.Combine(ProjectProfile.ProjectsRoot, "backup_野目录");
            try { Directory.CreateDirectory(junkDir); } catch { }
            Check("空野目录不算项目", !ProjectProfile.ListProfiles().Contains("backup_野目录"));
            try { Directory.Delete(junkDir, true); } catch { }
            string tmpName = "UT_TMP_单元测试";
            Check("合法名可建(中文下划线)", ProjectProfile.CreateProfile(tmpName));
            Check("重复建被拒", !ProjectProfile.CreateProfile(tmpName));
            Check("列表含新建项目", ProjectProfile.ListProfiles().Contains(tmpName));
            Check("切换不存在项目被拒", !ProjectProfile.SwitchTo("UT_不存在_999"));
            string resolved = ProjectProfile.ResolveDataPath("Recipes.json", true);
            Check("项目文件路径落在Projects目录下",
                resolved.Contains("Projects") && resolved.EndsWith("Recipes.json"));
            string global = ProjectProfile.ResolveDataPath("Users.json", false);
            Check("全局文件路径不进Projects",
                !global.Contains("Projects") && global.EndsWith("Users.json"));
            try { Directory.Delete(System.IO.Path.Combine(ProjectProfile.ProjectsRoot, tmpName), true); }
            catch { }
            Check("临时项目已清理", !ProjectProfile.ListProfiles().Contains(tmpName));
            // 【V1.72.9】缺省项目名=烧屏测试（harness 无 ActiveProject 配置，稳定回缺省）
            Check("缺省项目名=烧屏测试", ProjectProfile.ActiveProfileName == "烧屏测试");

            // ── V1.72.10 热更：切换指针往返 + 工位缓存跨项目隔离 ──
            // 场景 = 用户在"项目切换"里选 A→切→选 B→切回：指针、路径、回填缓存必须跟人走。
            // 约束：harness 与其它用例共享 run 目录，tmp 名唯一、finally 必恢复指针并删目录。
            string hotOrig = ProjectProfile.ActiveProfileName;
            string hotOrigDir = Path.Combine(ProjectProfile.ProjectsRoot, hotOrig);
            bool hotHadOrigDir = Directory.Exists(hotOrigDir);
            string hotA = "UT_热更_A";
            string hotB = "UT_热更_B";
            try
            {
                if (!hotHadOrigDir) Directory.CreateDirectory(hotOrigDir);
                Check("热更项目A可建", ProjectProfile.CreateProfile(hotA));
                Check("热更项目B可建", ProjectProfile.CreateProfile(hotB));
                Check("切到A成功", ProjectProfile.SwitchTo(hotA));
                Check("指针已指A", ProjectProfile.ActiveProfileName == hotA);
                Check("项目文件路径落在A目录下",
                    ProjectProfile.ResolveDataPath("StationSettings.json", true).Contains(hotA));
                StationSettingsCache.Reload();
                StationSettingsCache.Save(new StationCacheEntry { DeviceId = 1, SerialNumber = "SN-热更A" });
                Check("A项目缓存写入可读",
                    StationSettingsCache.Get(1) != null && StationSettingsCache.Get(1).SerialNumber == "SN-热更A");
                Check("切到B成功", ProjectProfile.SwitchTo(hotB));
                StationSettingsCache.Reload();
                Check("切项目Reload后旧项目缓存不可见", StationSettingsCache.Get(1) == null);
                StationSettingsCache.Save(new StationCacheEntry { DeviceId = 1, SerialNumber = "SN-热更B" });
                Check("切回A成功", ProjectProfile.SwitchTo(hotA));
                StationSettingsCache.Reload();
                Check("切回A后A数据回来无串扰",
                    StationSettingsCache.Get(1) != null && StationSettingsCache.Get(1).SerialNumber == "SN-热更A");
            }
            finally
            {
                try { ProjectProfile.SwitchTo(hotOrig); } catch { }
                try { Directory.Delete(Path.Combine(ProjectProfile.ProjectsRoot, hotA), true); } catch { }
                try { Directory.Delete(Path.Combine(ProjectProfile.ProjectsRoot, hotB), true); } catch { }
                if (!hotHadOrigDir) { try { Directory.Delete(hotOrigDir, true); } catch { } }
                StationSettingsCache.Reload(); // 缓存指回原项目（后模块还会 ResetStationCache 再隔离）
            }
            Check("指针已恢复原项目", ProjectProfile.ActiveProfileName == hotOrig);
            Check("临时项目已清理", !ProjectProfile.ListProfiles().Contains(hotA)
                && !ProjectProfile.ListProfiles().Contains(hotB));

            // ── V1.72.10 改干净：遗留 Default 自愈（列表里永远没有 Default） ──
            // run 目录是各模块共享的：burnDir 若已存在（别的用例留的货）绝不能删，
            // 只验证"有正主时清 Default + 合并补拷 + 重名不覆盖"；反之验证"整体改名"。
            string burnDir = Path.Combine(ProjectProfile.ProjectsRoot, "烧屏测试");
            string legacyDir = Path.Combine(ProjectProfile.ProjectsRoot, "Default");
            bool hadBurn = Directory.Exists(burnDir);
            bool hadLegacy = Directory.Exists(legacyDir);
            string stashDir = null;
            if (hadLegacy)
            {
                stashDir = legacyDir + "_UT暂存_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                try { Directory.Move(legacyDir, stashDir); } catch { stashDir = null; }
            }
            try
            {
                if (!hadBurn)
                {
                    // 正主不在：有货 Default 整体改名（数据不丢，列表干净）
                    Directory.CreateDirectory(legacyDir);
                    File.WriteAllText(Path.Combine(legacyDir, "Recipes.json"), "[]");
                    ProjectProfile.CleanupLegacyDefault();
                    Check("正主不在时有货Default整体改名",
                        !Directory.Exists(legacyDir) && File.Exists(Path.Combine(burnDir, "Recipes.json")));
                    try { Directory.Delete(burnDir, true); } catch { } // 还原现场
                }
                else
                {
                    // 正主在：Default 必删；独有文件补拷；重名文件不覆盖正主
                    string burnStation = Path.Combine(burnDir, "StationSettings.json");
                    bool hadBurnStation = File.Exists(burnStation);
                    string burnStationBackup = hadBurnStation ? File.ReadAllText(burnStation) : null;
                    Directory.CreateDirectory(legacyDir);
                    string stationMarker = "[{\"DeviceId\": 7, \"SerialNumber\": \"SN-UT补拷\"}]";
                    File.WriteAllText(Path.Combine(legacyDir, "StationSettings.json"), stationMarker);
                    ProjectProfile.CleanupLegacyDefault();
                    Check("正主在时Default被删", !Directory.Exists(legacyDir));
                    Check("项目列表无Default", !ProjectProfile.ListProfiles().Contains("Default"));
                    if (hadBurnStation)
                        Check("重名回填文件不覆盖正主", File.ReadAllText(burnStation) == burnStationBackup);
                    else
                    {
                        Check("独有回填文件补拷给正主", File.ReadAllText(burnStation) == stationMarker);
                        try { File.Delete(burnStation); } catch { } // 只删我们补拷的
                    }
                }
            }
            finally
            {
                // 有一删一：测试造的 Default 不能留；之前暂存的有货 Default 原样还回去
                try { if (Directory.Exists(legacyDir) && stashDir != null) Directory.Delete(legacyDir, true); } catch { }
                if (stashDir != null)
                {
                    try { if (!Directory.Exists(legacyDir)) Directory.Move(stashDir, legacyDir); } catch { }
                }
            }

            // ── V1.72.10 热更：DeviceConfig.CopyFrom 就地换血（引用不变值全换） ──
            var copySrc = new DeviceConfig
            {
                CollectInterval = 2000,
                ZeroDurationPolicy = ZeroDurationPolicy.Block,
                MesEndpoint = "http://ut",
                ScannerEnabled = true,
                AlarmPressureThresholdKPa = -9.5m
            };
            var copyDst = new DeviceConfig();
            DeviceConfig alias = copyDst; // 持别名的服务（DeviceManager/MES）视角：引用不能变
            copyDst.CopyFrom(copySrc);
            Check("CopyFrom引用不变", Object.ReferenceEquals(alias, copyDst));
            Check("CopyFrom全量拷(数字/枚举/字符串/布尔/小数)",
                copyDst.CollectInterval == 2000
                && copyDst.ZeroDurationPolicy == ZeroDurationPolicy.Block
                && copyDst.MesEndpoint == "http://ut"
                && copyDst.ScannerEnabled == true
                && copyDst.AlarmPressureThresholdKPa == -9.5m);
            copySrc.MesEndpoint = "http://changed";
            Check("CopyFrom后两边脱钩(值拷贝)", copyDst.MesEndpoint == "http://ut");
            bool copyThrew = false;
            try { copyDst.CopyFrom(null); } catch (ArgumentNullException) { copyThrew = true; }
            Check("CopyFrom(null)抛参数异常", copyThrew);

            // ── V1.72.10 热更：DeviceManager.ClearProjectScopedState + 采集暂停 ──
            // 只构造不 Start（无线程无时序；Fake 设备零硬件），用完 Dispose。
            DeviceManager hotDm = null;
            try
            {
                var hotCfg = new DeviceConfig
                {
                    TotalBarometers = 4, TotalInputs = 80, TotalOutputs = 160, FanEnabled = false
                };
                hotDm = new DeviceManager(hotCfg,
                    new FakeBarometerReader(hotCfg.TotalBarometers),
                    new FakeIoController(hotCfg.TotalInputs + hotCfg.TotalOutputs), null);
                hotDm.SetStationRecipeName(1, "旧项目配方");
                Check("清理前指派存在", hotDm.GetStationInfo(1) != null
                    && hotDm.GetStationInfo(1).RecipeName == "旧项目配方");
                hotDm.ClearProjectScopedState();
                Check("清理后旧指派归零", hotDm.GetStationInfo(1) == null);
                bool wasRunning = hotDm.PauseCollection();
                Check("未启动时Pause返回false", wasRunning == false);
                hotDm.ResumeCollection(false);
                Check("Resume(false)不擅自启动", hotDm.PauseCollection() == false);
            }
            finally
            {
                if (hotDm != null) { try { hotDm.Dispose(); } catch { } }
            }

            // ── V1.72.11：DeleteProfile（建错/验证完的清理口） ──
            string delCur = ProjectProfile.ActiveProfileName; // 前面 finally 已恢复
            string delName = "UT_待删";
            try
            {
                Check("删不存在项目被拒", !ProjectProfile.DeleteProfile("UT_不存在_999"));
                Check("空名删除被拒", !ProjectProfile.DeleteProfile("  "));
                Check("可建待删项目", ProjectProfile.CreateProfile(delName));
                Check("切到待删项目", ProjectProfile.SwitchTo(delName));
                Check("当前项目拒绝删除", !ProjectProfile.DeleteProfile(delName));
                Check("切回原项目", ProjectProfile.SwitchTo(delCur));
                Check("非当前项目可删", ProjectProfile.DeleteProfile(delName));
                Check("列表无待删项目", !ProjectProfile.ListProfiles().Contains(delName));
            }
            finally
            {
                try { ProjectProfile.SwitchTo(delCur); } catch { }
                try { Directory.Delete(Path.Combine(ProjectProfile.ProjectsRoot, delName), true); } catch { }
                StationSettingsCache.Reload();
            }
            Check("删除后指针仍在原项目", ProjectProfile.ActiveProfileName == delCur);

            // ── V1.72.12：ApplyLoadedRecipes（删光配方后热切换不串数据） ──
            // 复现：B 项目删光配方（文件=[]）→切走→切回，内存必须为空而非上个项目残留。
            var memRecipes = new List<RecipeConfig>
            {
                new RecipeConfig { Id = 1, Name = "上个项目残留" }
            };
            MainForm.ApplyLoadedRecipes(memRecipes, new List<RecipeConfig>());
            Check("空列表进内存=清空（删光后切回无残留）", memRecipes.Count == 0);
            memRecipes.Add(new RecipeConfig { Id = 2, Name = "旧数据" });
            MainForm.ApplyLoadedRecipes(memRecipes, null);
            Check("null（无文件/损坏）同样清空不串项目", memRecipes.Count == 0);
            var fresh = new List<RecipeConfig>
            {
                new RecipeConfig { Id = 7, Name = "新项目配方" }
            };
            List<RecipeConfig> aliasRecipes = memRecipes;
            MainForm.ApplyLoadedRecipes(memRecipes, fresh);
            Check("有数据正常替换", memRecipes.Count == 1 && memRecipes[0].Name == "新项目配方");
            Check("就地换引用不变（自动完成源不受影响）", Object.ReferenceEquals(aliasRecipes, memRecipes));
            MainForm.ApplyLoadedRecipes(null, fresh); // null 目标不抛
            Check("目标null静默返回", true);

            // ── ProjectPolicyStore.Save/Load 往返（写当前项目 Policy.json，隔离目录） ──
            string policyPath = ProjectPolicyStore.PolicyFilePath;
            bool hadPolicy = File.Exists(policyPath);
            string backup = hadPolicy ? File.ReadAllText(policyPath) : null;
            try
            {
                ProjectPolicyStore.Save(new Dictionary<string, string>
                    { { "ZeroDurationPolicy", "Block" } });
                Check("策略存取往返",
                    ProjectPolicyStore.GetRaw("ZeroDurationPolicy") == "Block");
                Check("没存的项回null(走机器缺省)",
                    ProjectPolicyStore.GetRaw("EmptySnPolicy") == null);
                // 【复查补齐】手改文件即生效：缓存按"路径+长度+写时间"验证，不读脏
                var handDict = new Dictionary<string, string> { { "ZeroDurationPolicy", "Allow" } };
                File.WriteAllText(policyPath,
                    Newtonsoft.Json.JsonConvert.SerializeObject(handDict));
                Check("手改Policy.json后Load见新值(缓存不脏)",
                    ProjectPolicyStore.GetRaw("ZeroDurationPolicy") == "Allow");
            }
            finally
            {
                try
                {
                    if (hadPolicy) File.WriteAllText(policyPath, backup);
                    else if (File.Exists(policyPath)) File.Delete(policyPath);
                }
                catch { }
            }
        }

        // =====================================================================
        // 12c. MES 映射与上报（V1.68 二期：映射层可配，传输走 HTTP）
        //     传输用 Fake（MesReporter.Transport 静态缝），全程零外网。
        // =====================================================================
        private static void MesTests()
        {
            // ── ParseTriggers：空=全开；中英文分隔；未知进错；去重 ──
            List<string> tg; List<string> te;
            MesMapping.ParseTriggers("", out tg, out te);
            Check("触发器空=零项零错(调用方按全开)", tg.Count == 0 && te.Count == 0);
            MesMapping.ParseTriggers("Complete,Alarm", out tg, out te);
            Check("英文逗号解析2项", tg.Count == 2 && te.Count == 0);
            MesMapping.ParseTriggers("Complete，Alarm、Start", out tg, out te);
            Check("中文逗号顿号兼容3项", tg.Count == 3 && te.Count == 0);
            MesMapping.ParseTriggers("Complete,BadTrigger", out tg, out te);
            Check("未知触发器进错但合法保留", tg.Count == 1 && te.Count == 1);
            MesMapping.ParseTriggers("complete,Complete", out tg, out te);
            Check("大小写去重只留1", tg.Count == 1 && te.Count == 0);
            Check("空配置全命中", MesMapping.TriggerHit("", "Alarm"));
            Check("小写命中", MesMapping.TriggerHit("complete", "Complete"));
            Check("未配置不命中", !MesMapping.TriggerHit("Complete", "Alarm"));

            // ── ParseFieldMap：合法/未知本站/坏组/坏MES名/重复 ──
            Dictionary<string, string> mp; List<string> me;
            MesMapping.ParseFieldMap("eqId=device;lotNo=lot", out mp, out me);
            Check("两组映射解析", mp.Count == 2 && mp["eqId"] == "device" && me.Count == 0);
            MesMapping.ParseFieldMap("", out mp, out me);
            Check("空=直通零项零错", mp.Count == 0 && me.Count == 0);
            MesMapping.ParseFieldMap("eqId=bogus", out mp, out me);
            Check("未知本站字段进错", mp.Count == 0 && me.Count == 1);
            MesMapping.ParseFieldMap("noequals", out mp, out me);
            Check("无等号坏组进错", me.Count == 1);
            MesMapping.ParseFieldMap("has space=device", out mp, out me);
            Check("MES名含空格进错", me.Count == 1);
            MesMapping.ParseFieldMap("a=device;a=lot", out mp, out me);
            Check("MES名重复后者覆盖+提醒", mp.Count == 1 && mp["a"] == "lot" && me.Count == 1);
            MesMapping.ParseFieldMap("EQ=device", out mp, out me);
            Check("本站名大小写兼容", mp.Count == 1 && me.Count == 0);

            // ── ParseStaticFields：合法/坏组/空值 ──
            Dictionary<string, string> st;
            MesMapping.ParseStaticFields("line=L5;workshop=A3", out st, out me);
            Check("两组静态解析", st.Count == 2 && st["line"] == "L5" && me.Count == 0);
            MesMapping.ParseStaticFields("novalue", out st, out me);
            Check("无等号进错", me.Count == 1);
            MesMapping.ParseStaticFields("k=", out st, out me);
            Check("空值合法(空串)", st.Count == 1 && st["k"] == "" && me.Count == 0);

            // ── BuildPayloadJson：直通/改名/静态合并覆盖 ──
            var ours = new Dictionary<string, string>
                { { "device", "7" }, { "lot", "LOT1" }, { "result", "PASS" } };
            string j0 = MesReporter.BuildPayloadJson(ours, "", "");
            Check("映射空=直通用原名",
                j0.Contains("\"device\":\"7\"") && j0.Contains("\"lot\":\"LOT1\""));
            string j1 = MesReporter.BuildPayloadJson(ours, "eqId=device;lotNo=lot", "");
            Check("映射改名(MES名出现)",
                j1.Contains("\"eqId\":\"7\"") && j1.Contains("\"lotNo\":\"LOT1\""));
            string j2 = MesReporter.BuildPayloadJson(ours, "", "line=L5");
            Check("静态字段并入", j2.Contains("\"line\":\"L5\""));
            string j3 = MesReporter.BuildPayloadJson(ours, "", "device=STATIC");
            Check("静态覆盖同名(常量优先)", j3.Contains("\"device\":\"STATIC\""));

            // ── ParseValue 字符串分支（V1.68：MES 文本直通，校验在 ValidateValue） ──
            Check("字符串原样返回",
                Equals(ProjectPolicyStore.ParseValue(typeof(string), "a=b;c"), "a=b;c"));
            Check("MES三key进PolicyKeys(跟项目走)",
                ProjectPolicyStore.PolicyKeys.Contains("MesTriggers")
                && ProjectPolicyStore.PolicyKeys.Contains("MesFieldMap")
                && ProjectPolicyStore.PolicyKeys.Contains("MesStaticFields"));

            // ── MesCrypto DPAPI（V1.68：token/密码加密落盘） ──
            Check("空串原样返回", MesCrypto.Protect("") == "" && MesCrypto.Unprotect("") == "");
            string enc = MesCrypto.Protect("s3cr3t-token");
            Check("加密带前缀且非明文",
                enc != null && enc.StartsWith("DPAPI:") && !enc.Contains("s3cr3t"));
            Check("加解密往返一致",
                enc != null && MesCrypto.Unprotect(enc) == "s3cr3t-token");
            Check("已加密不再重复加密",
                enc != null && MesCrypto.Protect(enc) == enc);
            Check("明文不兼容回null（未上线，明文=配错，逼重填）",
                MesCrypto.Unprotect("plain-token") == null);
            Check("IsProtected只认前缀",
                MesCrypto.IsProtected(enc) && !MesCrypto.IsProtected("plain"));
            Check("篡改密文解密失败回null",
                MesCrypto.Unprotect("DPAPI:!!!不是Base64!!!") == null);

            // ── ParseCustomHeaders：合法/坏组/坏名/重复 ──
            Dictionary<string, string> ch;
            MesMapping.ParseCustomHeaders("X-Line=L5;X-ApiVer=2", out ch, out me);
            Check("两组自定义头解析", ch.Count == 2 && ch["X-Line"] == "L5" && me.Count == 0);
            MesMapping.ParseCustomHeaders("noequals", out ch, out me);
            Check("无等号进错", me.Count == 1);
            MesMapping.ParseCustomHeaders("has space=v", out ch, out me);
            Check("头名含空格进错", me.Count == 1);
            MesMapping.ParseCustomHeaders("中文头=v", out ch, out me);
            Check("中文头名进错", me.Count == 1);
            MesMapping.ParseCustomHeaders("1abc=v", out ch, out me);
            Check("数字开头进错", me.Count == 1);
            MesMapping.ParseCustomHeaders("X-K=a;X-K=b", out ch, out me);
            Check("重复头后者覆盖", ch.Count == 1 && ch["X-K"] == "b");
            MesMapping.ParseCustomHeaders("X-Token=abc==", out ch, out me);
            Check("头值Base64填充保留", ch.Count == 1 && ch["X-Token"] == "abc==" && me.Count == 0);

            // ── ParseEndpointMap + ResolveEndpoint：合法/未知触发器/非http/重复/回退 ──
            Dictionary<string, string> ep;
            MesMapping.ParseEndpointMap("Alarm=http://x/api/alarm", out ep, out me);
            Check("分地址解析", ep.Count == 1 && ep["Alarm"] == "http://x/api/alarm" && me.Count == 0);
            MesMapping.ParseEndpointMap("Bogus=http://x/", out ep, out me);
            Check("未知触发器进错", me.Count == 1);
            MesMapping.ParseEndpointMap("Alarm=ftp://x/", out ep, out me);
            Check("非http拒收", me.Count == 1);
            MesMapping.ParseEndpointMap("alarm=http://a/;Alarm=http://b/", out ep, out me);
            Check("大小写归一+重复覆盖提醒",
                ep.Count == 1 && ep["Alarm"] == "http://b/" && me.Count == 1);
            Check("命中走分地址",
                MesMapping.ResolveEndpoint("Alarm", "http://def/", "Alarm=http://x/a") == "http://x/a");
            Check("未命中回默认",
                MesMapping.ResolveEndpoint("Complete", "http://def/", "Alarm=http://x/a") == "http://def/");
            Check("映射空全走默认",
                MesMapping.ResolveEndpoint("Alarm", "http://def/", "") == "http://def/");

            // ── DeviceConfig MES 缺省锁：不配=零行为 ──
            var dc = new DeviceConfig();
            Check("缺省不上报不Mock",
                dc.MesEnabled == false && dc.MesMockEnabled == false);
            Check("缺省地址空/超时5000/鉴权None",
                dc.MesEndpoint == "" && dc.MesTimeoutMs == 5000 && dc.MesAuthType == "None");
            Check("缺省重试3次/间隔2000",
                dc.MesRetryCount == 3 && dc.MesRetryIntervalMs == 2000);
            Check("缺省触发映射静态全空",
                dc.MesTriggers == "" && dc.MesFieldMap == "" && dc.MesStaticFields == "");
            Check("缺省自定义头与分地址全空",
                dc.MesCustomHeaders == "" && dc.MesEndpointMap == "");

            // ── SettingsForm.ValidateValue MES 分支（反射） ──
            const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Static;
            var miValidate = typeof(SettingsForm).GetMethod("ValidateValue", Flags);
            if (miValidate != null)
            {
                Func<string, string, bool> valid = (k, v) =>
                {
                    object[] a = new object[] { k, v, null };
                    return (bool)miValidate.Invoke(null, a);
                };
                Check("鉴权Bearer过", valid("MesAuthType", "Bearer"));
                Check("鉴权小写过", valid("MesAuthType", "basic"));
                Check("鉴权脏值被拦", !valid("MesAuthType", "Token"));
                Check("触发器合法过", valid("MesTriggers", "Complete,Alarm"));
                Check("触发器空过(全开)", valid("MesTriggers", ""));
                Check("触发器脏值被拦", !valid("MesTriggers", "Complete,Bad"));
                Check("映射合法过", valid("MesFieldMap", "eqId=device"));
                Check("映射脏值被拦", !valid("MesFieldMap", "eqId=bogus"));
                Check("静态合法过", valid("MesStaticFields", "line=L5"));
                Check("静态坏组被拦", !valid("MesStaticFields", "=v"));
                Check("MES布尔过", valid("MesEnabled", "true"));
                Check("MES布尔脏值被拦", !valid("MesEnabled", "YES"));
                Check("MES超时整数过", valid("MesTimeoutMs", "5000"));
                Check("MES超时脏值被拦", !valid("MesTimeoutMs", "abc"));
                Check("自定义头合法过", valid("MesCustomHeaders", "X-Line=L5"));
                Check("自定义头脏值被拦", !valid("MesCustomHeaders", "中文头=v"));
                Check("分地址合法过", valid("MesEndpointMap", "Alarm=http://x/a"));
                Check("分地址脏值被拦", !valid("MesEndpointMap", "Alarm=ftp://x/"));
            }
            else
            {
                Check("反射找到 ValidateValue", false);
            }

            // ── NormalizeMesAuthType（反射：脏值兜底 None） ──
            var miAuth = typeof(SettingsForm).GetMethod("NormalizeMesAuthType", Flags);
            Check("反射找到 NormalizeMesAuthType", miAuth != null);
            if (miAuth != null)
            {
                Check("小写归一Bearer",
                    (string)miAuth.Invoke(null, new object[] { "bearer" }) == "Bearer");
                Check("脏值兜底None",
                    (string)miAuth.Invoke(null, new object[] { "xxx" }) == "None");
            }

            // ── MesReporter + Fake 传输：上报/开关/触发器/Mock/离线缓存 ──
            // 【大扫荡】缓存绝对路径化后走 BaseDirOverride 隔离（finally 复位）。
            string mesDir = EnterCleanDir();
            var fMesBase = typeof(MesReporter).GetField("BaseDirOverride",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (fMesBase != null) fMesBase.SetValue(null, mesDir);
            string mesQueuePath = Path.Combine(mesDir, "MesQueue.json");
            var captured = new List<Tuple<string, string, Dictionary<string, string>>>();
            var capLock = new object();
            bool transportOk = true;
            MesReporter.Transport = (url, json, headers, timeout) =>
            {
                lock (capLock) { captured.Add(Tuple.Create(url, json, headers)); }
                return transportOk;
            };
            try
            {
                var cfg = new DeviceConfig
                {
                    MesEnabled = true,
                    MesEndpoint = "http://fake-mes:8080/api",
                    MesRetryCount = 0,
                    MesFieldMap = "eqId=device",
                    MesStaticFields = "line=L5"
                };
                var fields = new Dictionary<string, string>
                {
                    { "time", "2026-09-11 10:00:00" }, { "lot", "LOT1" },
                    { "device", "7" }, { "event", "Complete" }, { "result", "PASS" }
                };
                using (var rep = new MesReporter(cfg))
                {
                    rep.Report("Complete", fields);
                    bool got = WaitFor(() =>
                    {
                        lock (capLock) { return captured.Count >= 1; }
                    }, 5000);
                    Check("启用后上报发出", got);
                    string json = "";
                    lock (capLock) { if (captured.Count > 0) json = captured[0].Item2; }
                    Check("映射改名生效(eqId)",
                        json.Contains("\"eqId\":\"7\""));
                    // 【大扫荡】真改名：原名 device 不再一起发出（严格 schema 拒收多余字段）。
                    Check("改名后原名已删", !json.Contains("\"device\":"));
                    // 本站键大小写不敏感：映射写 Device 也命中 device。
                    string jCase = MesReporter.BuildPayloadJson(fields, "eqId=Device", "");
                    Check("映射本站键大小写兼容",
                        jCase.Contains("\"eqId\":\"7\"") && !jCase.Contains("\"device\":"));
                    Check("静态字段并入(line)",
                        json.Contains("\"line\":\"L5\""));
                    Check("发往配置地址",
                        captured.Count > 0 && captured[0].Item1 == "http://fake-mes:8080/api");

                    // 开关关闭：零发送
                    lock (capLock) { captured.Clear(); }
                    cfg.MesEnabled = false;
                    rep.Report("Complete", fields);
                    Thread.Sleep(400);
                    lock (capLock) { Check("开关关闭零发送", captured.Count == 0); }
                    cfg.MesEnabled = true;

                    // 触发器未命中：零发送
                    lock (capLock) { captured.Clear(); }
                    cfg.MesTriggers = "Alarm";
                    rep.Report("Complete", fields);
                    Thread.Sleep(400);
                    lock (capLock) { Check("触发器未命中零发送", captured.Count == 0); }
                    cfg.MesTriggers = "";
                }

                // Mock：不走传输，只写 CSV
                var mockCfg = new DeviceConfig
                {
                    MesEnabled = true,
                    MesMockEnabled = true,
                    MesEndpoint = "http://fake-mes:8080/api",
                    MesRetryCount = 0
                };
                using (var mock = new MesReporter(mockCfg))
                {
                    lock (capLock) { captured.Clear(); }
                    mock.Report("Complete", fields);
                    Thread.Sleep(400);
                    lock (capLock) { Check("Mock不发HTTP", captured.Count == 0); }
                    Check("Mock写CSV事件", WaitFor(() => CountCsvLines("MES上报(Mock)") >= 1, 3000));
                }

                // 自定义头 + 鉴权优先 + 按事件分地址
                var advCfg = new DeviceConfig
                {
                    MesEnabled = true,
                    MesEndpoint = "http://fake-mes:8080/api",
                    MesEndpointMap = "Alarm=http://fake-mes:8080/alarm",
                    MesAuthType = "Bearer",
                    MesAuthToken = "tok123",
                    MesCustomHeaders = "X-Line=L5;Authorization=HACK",
                    MesRetryCount = 0
                };
                using (var adv = new MesReporter(advCfg))
                {
                    lock (capLock) { captured.Clear(); }
                    var fieldsAlarm = new Dictionary<string, string>(fields);
                    fieldsAlarm["event"] = "Alarm";
                    adv.Report("Complete", fields);
                    adv.Report("Alarm", fieldsAlarm);
                    bool advGot = WaitFor(() =>
                    {
                        lock (capLock) { return captured.Count >= 2; }
                    }, 5000);
                    Check("分地址两条都发出", advGot);
                    string urlComplete = "";
                    string urlAlarm = "";
                    Dictionary<string, string> headers = null;
                    lock (capLock)
                    {
                        foreach (var c in captured)
                        {
                            if (c.Item2.Contains("\"event\":\"Complete\"")) urlComplete = c.Item1;
                            if (c.Item2.Contains("\"event\":\"Alarm\""))
                            {
                                urlAlarm = c.Item1;
                                headers = c.Item3;
                            }
                        }
                    }
                    Check("未命中走默认地址", urlComplete == "http://fake-mes:8080/api");
                    Check("命中走分地址", urlAlarm == "http://fake-mes:8080/alarm");
                    Check("自定义头透传",
                        headers != null && headers.ContainsKey("X-Line") && headers["X-Line"] == "L5");
                    Check("鉴权优先顶掉伪造Authorization",
                        headers != null && headers.ContainsKey("Authorization")
                        && headers["Authorization"] == "Bearer tok123");
                }

                // 离线缓存：全灭→落盘；恢复→补发清盘
                var failCfg = new DeviceConfig
                {
                    MesEnabled = true,
                    MesEndpoint = "http://fake-mes:8080/api",
                    MesRetryCount = 0
                };
                transportOk = false;
                using (var repFail = new MesReporter(failCfg))
                {
                    repFail.Report("Complete", fields);
                    Check("全灭后离线缓存落盘",
                        WaitFor(() => File.Exists(mesQueuePath), 5000));
                }
                transportOk = true;
                using (var repBack = new MesReporter(failCfg))
                {
                    repBack.Report("Complete", fields);
                    Check("恢复后补发并清盘",
                        WaitFor(() => !File.Exists(mesQueuePath), 8000));
                }
            }
            finally
            {
                MesReporter.Transport = null;
                try { if (fMesBase != null) fMesBase.SetValue(null, null); } catch { }
                try { if (File.Exists(mesQueuePath)) File.Delete(mesQueuePath); } catch { }
            }
        }

        // =====================================================================
        // 12d. 规则表达式引擎 + 执行器（V1.69 三期：沙盒解析求值 + 持续计时）
        // =====================================================================
        private static void RuleExprTests()
        {
            // —— 求值小工具：文本 + 变量 → bool（失败记 FAIL 并返回 false） ——
            Func<string, Dictionary<string, double>, bool> eval = (text, vars) =>
            {
                RuleExpr.RuleExpression ex;
                string perr;
                if (!RuleExpr.TryParse(text, out ex, out perr)) return false;
                string eerr;
                return RuleExpr.TryEvalBool(ex, vars, out eerr);
            };
            Func<string, Dictionary<string, double>, string> evalErr = (text, vars) =>
            {
                RuleExpr.RuleExpression ex;
                string perr;
                if (!RuleExpr.TryParse(text, out ex, out perr)) return "parse:" + perr;
                double v;
                string eerr;
                if (!RuleExpr.TryEval(ex, vars, out v, out eerr)) return "eval:" + eerr;
                return null;
            };
            var empty = new Dictionary<string, double>();

            // ── 四则与优先级 ──
            Check("1+2*3=7",
                eval("1+2*3==7", empty));
            Check("括号改变优先级",
                eval("(1+2)*3==9", empty));
            Check("单目负号",
                eval("-5+2==-3", empty));
            Check("除法小数",
                eval("10/4==2.5", empty));
            Check("取模",
                eval("10%3==1", empty));
            Check("空格容忍",
                eval("  1 + 2 == 3 ", empty));
            Check("true/false字面量",
                eval("TRUE && True", empty) && !eval("true && false", empty));

            // ── 比较与逻辑 ──
            Check("大于", eval("3>2", empty));
            Check("大于等于边界", eval("2>=2", empty));
            Check("小于否", !eval("3<2", empty));
            Check("不等", eval("2!=3", empty));
            Check("与或非", eval("1&&1", empty) && !eval("1&&0", empty)
                && eval("0||1", empty) && eval("!0", empty) && !eval("!5", empty));
            Check("&&优先级高于||",
                !eval("0||1&&0", empty) && eval("1||0&&0", empty));
            Check("混合表达式",
                eval("1+2>2 && 3<4", empty));

            // ── 变量（大小写无所谓） ──
            var vp = new Dictionary<string, double> { { "Pressure", -4.0 } };
            Check("变量大小写兼容", eval("pressure>-5", vp) && eval("PRESSURE<-3", vp));

            // ── 短路（右分支除零被跳过：不断言值，只断言"无错"；值用下一行单测） ──
            Check("&&短路跳过除零(无错)",
                evalErr("0==1 && 10/0>1", empty) == null);
            Check("&&短路值为假",
                !eval("0==1 && 10/0>1", empty));
            Check("||短路跳过除零(无错且为真)",
                evalErr("1==1 || 10/0>1", empty) == null && eval("1==1 || 10/0>1", empty));
            Check("非短路分支除零报错", evalErr("1==1 && 10/0>1", empty) != null);

            // ── 除零/模零/未知变量 ──
            Check("除零求值错", evalErr("1/0>1", empty) != null);
            Check("模零求值错", evalErr("1%0==0", empty) != null);
            Check("未知变量求值错", evalErr("bogus>1", empty) != null);

            // ── 语法错（带位置） ──
            Check("空表达式错", evalErr("", empty) != null);
            Check("缺操作数错", evalErr("1+", empty) != null);
            Check("括号未闭合错", evalErr("(1+2", empty) != null);
            Check("多余内容错", evalErr("1 2", empty) != null);
            Check("单&非法", evalErr("1 & 0", empty) != null);
            Check("单|非法", evalErr("1 | 0", empty) != null);
            Check("单目+不支持", evalErr("+5==5", empty) != null);
            Check("非法字符错", evalErr("1 $ 2", empty) != null);

            // ── NaN语义（死传感器不触发） ──
            var vn = new Dictionary<string, double> { { "temp", double.NaN } };
            Check("NaN比较恒false", !eval("temp>1", vn) && !eval("temp<100", vn));
            Check("NaN相等也false（非IEEE，fail-safe）",
                !eval("temp==temp", vn) && !eval("temp!=0", vn));
            Check("NaN取反为true", eval("!(temp>1)", vn));

            // ── ParseRuleList：行格式 ──
            List<RuleEngine.RuleDef> defs;
            List<string> rerrs;
            RuleEngine.ParseRuleList("超温偏离 | temp - tempset > 10 | 30\r\n即时 | pressure>1", out defs, out rerrs);
            Check("两行解析（含\\r\\n）",
                rerrs.Count == 0 && defs.Count == 2
                && defs[0].Name == "超温偏离" && defs[0].SustainedSecs == 30
                && defs[1].Name == "即时" && defs[1].SustainedSecs == 0);
            RuleEngine.ParseRuleList("", out defs, out rerrs);
            Check("空=零规则", defs.Count == 0 && rerrs.Count == 0);
            RuleEngine.ParseRuleList("没有分隔符", out defs, out rerrs);
            Check("坏行带行号", rerrs.Count == 1 && rerrs[0].Contains("第1行"));
            RuleEngine.ParseRuleList("名 | temp>", out defs, out rerrs);
            Check("坏表达式带名", rerrs.Count == 1 && rerrs[0].Contains("名"));
            RuleEngine.ParseRuleList("名 | temp>1 | abc", out defs, out rerrs);
            Check("坏持续秒被拦", rerrs.Count == 1);
            RuleEngine.ParseRuleList("名 | temp>1 | -1", out defs, out rerrs);
            Check("负持续秒被拦", rerrs.Count == 1);
            RuleEngine.ParseRuleList(" | temp>1", out defs, out rerrs);
            Check("空名称被拦", rerrs.Count == 1);
            // 【大扫荡】表达式含 || 的分隔冲突：段数超中间的应被自动识别为表达式一部分。
            // 以前 Split('|')>3 直接报格式错，`||` 功能实际死亡。
            RuleEngine.ParseRuleList("低压 | pressure>5 || temp>80 | 5", out defs, out rerrs);
            Check("含||规则解析成功",
                rerrs.Count == 0 && defs.Count == 1
                && defs[0].Name == "低压" && defs[0].SustainedSecs == 5
                && defs[0].Expression.Contains("||"));
            RuleEngine.ParseRuleList("a | 1>0 || 2>1 || 3>2 || 4>3 | 1", out defs, out rerrs);
            Check("多个||仍可解析", rerrs.Count == 0 && defs.Count == 1 && defs[0].Expression.Contains("4>3"));
            RuleEngine.ParseRuleList("名 | bogus > 1", out defs, out rerrs);
            Check("严格模式未知变量被拦", rerrs.Count == 1 && rerrs[0].Contains("未知变量"));
            RuleEngine.ParseRuleList("名 | bogus > 1", out defs, out rerrs, false);
            Check("非严格模式未知变量放行(运行时容错)", defs.Count == 1 && rerrs.Count == 0);
            // 深嵌套防护：5000 层括号不能 StackOverflow（解析器有深度上限）。
            string deep = new string('(', 300) + "1" + new string(')', 300);
            RuleEngine.ParseRuleList("深嵌套 | " + deep, out defs, out rerrs);
            Check("超深嵌套被拦(防栈溢出)", rerrs.Count == 1 && rerrs[0].Contains("嵌套"));
            string deepOk = new string('(', 10) + "1" + new string(')', 10);
            RuleEngine.ParseRuleList("浅嵌套 | " + deepOk, out defs, out rerrs);
            Check("浅嵌套正常过", rerrs.Count == 0 && defs.Count == 1);
            // 【复查补齐】连写一元符同样吃深度预算（以前只数括号，"!!!!…"走漏）
            string deepBang = new string('!', 100) + "1>0";
            RuleEngine.ParseRuleList("深取反 | " + deepBang, out defs, out rerrs);
            Check("超深连写!被拦(防栈溢出)", rerrs.Count == 1 && rerrs[0].Contains("嵌套"));
            RuleEngine.ParseRuleList("浅取反 | !!(1>0)", out defs, out rerrs);
            Check("浅取反正常过", rerrs.Count == 0 && defs.Count == 1);
            var many = new System.Text.StringBuilder();
            for (int i = 0; i < 25; i++) many.AppendLine("r" + i + " | 1>0");
            RuleEngine.ParseRuleList(many.ToString(), out defs, out rerrs);
            Check("超20条上限被拦", rerrs.Count == 1 && rerrs[0].Contains("上限"));

            // ── RuleEngine：持续计时（假时钟） ──
            DateTime fakeNow = new DateTime(2026, 9, 11, 10, 0, 0);
            var eng = new RuleEngine(4, () => fakeNow);
            eng.UpdateRules("r | pressure > 1 | 10", "");
            Check("编译1条", eng.RuleCount == 1 && !eng.HasCompleteExpr);
            var v1 = new Dictionary<string, double> { { "pressure", 2.0 } };
            Check("刚成立不触发", eng.EvalAlarms(1, v1, true) == null);
            fakeNow = fakeNow.AddSeconds(9);
            Check("9秒未满不触发", eng.EvalAlarms(1, v1, true) == null);
            fakeNow = fakeNow.AddSeconds(2);
            Check("满10秒触发", eng.EvalAlarms(1, v1, true) == "r");
            // 中断复位
            var v0 = new Dictionary<string, double> { { "pressure", 0.0 } };
            Check("变false复位计时", eng.EvalAlarms(1, v0, true) == null);
            Check("重成立从头计时", eng.EvalAlarms(1, v1, true) == null);
            // 同配置更新不清计时
            fakeNow = fakeNow.AddSeconds(10);
            eng.UpdateRules("r | pressure > 1 | 10", "");
            Check("同配置不清计时（累计满即触发）", eng.EvalAlarms(1, v1, true) == "r");
            // 换配置清计时
            eng.UpdateRules("r2 | pressure > 1 | 100", "");
            Check("换配置清计时", eng.EvalAlarms(1, v1, true) == null);
            // 非在测复位
            Check("非在测返回null", eng.EvalAlarms(1, v1, false) == null);
            Check("越界工位返回null", eng.EvalAlarms(99, v1, true) == null);
            // 立即规则
            eng.UpdateRules("now | 1==1 | 0", "");
            Check("持续0立即触发", eng.EvalAlarms(2, v0, true) == "now");
            // 求值错按不触发+记错
            eng.UpdateRules("bad | bogus > 1 | 0", "");
            Check("未知变量不触发", eng.EvalAlarms(2, v0, true) == null);
            Check("求值错记LastError", eng.LastError != null && eng.LastError.Contains("bad"));
            eng.ResetStation(2);
            Check("ResetStation不抛", true);

            // ── RuleEngine：完成表达式 ──
            var eng2 = new RuleEngine(4, () => fakeNow);
            eng2.UpdateRules("", "");
            Check("空完成表达式恒false",
                !eng2.EvalComplete(new Dictionary<string, double> { { "agesecs", 99999.0 } }));
            eng2.UpdateRules("", "agesecs>=100");
            Check("完成表达式生效标记", eng2.HasCompleteExpr);
            Check("未到不完成",
                !eng2.EvalComplete(new Dictionary<string, double> { { "agesecs", 90.0 } }));
            Check("到点完成",
                eng2.EvalComplete(new Dictionary<string, double> { { "agesecs", 100.0 } }));
            eng2.UpdateRules("", "bogus>1");
            Check("完成表达式求值错按false",
                !eng2.EvalComplete(new Dictionary<string, double>()));
            Check("完成求值错记LastError", eng2.LastError != null);

            // ── DeviceConfig 缺省锁：三项全空/关=零行为 ──
            var dc = new DeviceConfig();
            Check("缺省规则空/表达式空/不跳真空",
                dc.CustomAlarmRules == "" && dc.CompleteExpression == "" && dc.SkipVacuum == false);

            // ── SettingsForm.ValidateValue 规则分支（反射） ──
            const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Static;
            var miValidate = typeof(SettingsForm).GetMethod("ValidateValue", Flags);
            if (miValidate != null)
            {
                Func<string, string, bool> valid = (k, v) =>
                {
                    object[] a = new object[] { k, v, null };
                    return (bool)miValidate.Invoke(null, a);
                };
                Check("完成表达式合法过", valid("CompleteExpression", "temp > 85"));
                Check("完成表达式空过(禁用)", valid("CompleteExpression", ""));
                Check("完成表达式脏值被拦", !valid("CompleteExpression", "temp >"));
                Check("规则表合法过", valid("CustomAlarmRules", "超温 | temp>80 | 30\r\n即时 | 1>0"));
                Check("规则表空过(零规则)", valid("CustomAlarmRules", ""));
                Check("规则表坏行被拦", !valid("CustomAlarmRules", "没有分隔符"));
                Check("规则表坏表达式被拦", !valid("CustomAlarmRules", "名 | temp>"));
            }
            else
            {
                Check("反射找到 ValidateValue", false);
            }
        }

        // =====================================================================
        // 12e. 工艺策略静态图（V1.70：拓扑画死，文本按配置生成，纯函数零 UI）
        // =====================================================================
        /// <summary>某节点是否挂了某 key（V1.83 补齐锁用，找不到节点返回 false）。</summary>
        private static bool NodeHasKey(string nodeId, string key)
        {
            var n = Views.PolicyGraph.FindNode(nodeId);
            if (n == null || n.Keys == null) return false;
            foreach (var k in n.Keys)
            {
                if (k.Key == key) return true;
            }
            return false;
        }

        private static void ProcessPolicyTests()
        {
            // ── 拓扑完整性锁：8 节点 8 边，id 全可查 ──
            Check("节点8个", Views.PolicyGraph.Nodes.Count == 8);
            Check("连线8条", Views.PolicyGraph.Edges.Count == 8);
            string[] ids = { "start", "vacuum", "power", "done", "alarm", "recover", "unload", "mes" };
            bool allFound = true;
            foreach (string id in ids)
            {
                if (Views.PolicyGraph.FindNode(id) == null) allFound = false;
            }
            Check("8节点id全可查", allFound);
            Check("未知节点返回null", Views.PolicyGraph.FindNode("bogus") == null);
            Check("未知连线返回null", Views.PolicyGraph.FindEdge("bogus") == null);
            // 边的端点必须都是已知节点（拓扑不断线）
            bool edgesOk = true;
            foreach (var e in Views.PolicyGraph.Edges)
            {
                if (Views.PolicyGraph.FindNode(e.From) == null
                    || Views.PolicyGraph.FindNode(e.To) == null) edgesOk = false;
            }
            Check("边端点全是已知节点", edgesOk);
            // 每个节点至少挂1个 key（点谁都有东西可改）；unload 有 key 也有说明
            //（【V1.83】下料节点收进事件口径/报表列，指引文案改页脚保留）。
            bool keysOk = true;
            foreach (var n in Views.PolicyGraph.Nodes)
            {
                if (n.Keys.Count == 0) keysOk = false;
            }
            var unload = Views.PolicyGraph.FindNode("unload");
            Check("8节点全挂key（V1.83起unload也有口径/报表）",
                keysOk && unload != null && unload.Keys.Count > 0
                && !string.IsNullOrEmpty(unload.Info));
            // 节点 key 必须全是 DeviceConfig 真属性（防挂错名存不上）
            bool propsOk = true;
            foreach (var n in Views.PolicyGraph.Nodes)
            {
                foreach (var k in n.Keys)
                {
                    if (typeof(DeviceConfig).GetProperty(k.Key) == null) propsOk = false;
                }
            }
            Check("节点key全是DeviceConfig真属性", propsOk);

            // ── 缺省文本锁（缺省配置画出来长什么样） ──
            var dc = new DeviceConfig();
            var zero = new Views.PolicyGraph.FlowCounts();
            Func<string, string> linesOf = id =>
                string.Join("|", Views.PolicyGraph.BuildNodeLines(id, dc, zero));
            Check("启动节点缺省（警告/警告/空闲0台）",
                linesOf("start").Contains("警告") && linesOf("start").Contains("空闲 0 台"));
            Check("抽真空缺省（超时15000/失联3次）",
                linesOf("vacuum").Contains("15000") && linesOf("vacuum").Contains("3"));
            Check("上电缺省（全局不限/时长到）",
                linesOf("power").Contains("全局不限") && linesOf("power").Contains("时长到"));
            Check("完成缺省（自动/下电）",
                linesOf("done").Contains("自动") && linesOf("done").Contains("下电"));
            Check("报警缺省（0条规则）",
                linesOf("alarm").Contains("0 条"));
            Check("恢复缺省（重测/无快照）",
                linesOf("recover").Contains("重测") && linesOf("recover").Contains("无待恢复"));
            Check("下料缺省（自动PASS）",
                linesOf("unload").Contains("自动PASS"));
            Check("MES缺省（关/全报/0组）",
                linesOf("mes").Contains("关") && linesOf("mes").Contains("全报")
                && linesOf("mes").Contains("0 组"));
            Check("未知节点空行", Views.PolicyGraph.BuildNodeLines("bogus", dc, zero).Length == 0);
            Check("配置null空行", Views.PolicyGraph.BuildNodeLines("start", null, zero).Length == 0);

            // ── V1.83 补齐锁：驾驶舱漏的 4 策略 + 阈值 + 双总闸全挂上节点 ──
            Func<string, bool> hasKey = key =>
            {
                foreach (var n in Views.PolicyGraph.Nodes)
                    foreach (var k in n.Keys)
                        if (k.Key == key) return true;
                return false;
            };
            Check("阈值挂报警节点", NodeHasKey("alarm", "AlarmPressureThresholdKPa") && hasKey("AlarmWhenPressureHigherThanThreshold"));
            Check("破空总闸挂完成节点", NodeHasKey("done", "VentValveEnabled"));
            Check("MES总闸挂上报节点", NodeHasKey("mes", "MesEnabled"));
            Check("画面维度挂上电节点", NodeHasKey("power", "DisplayModeEnabled") && hasKey("DisplayModes"));
            Check("口径报表挂下料节点", NodeHasKey("unload", "EventIdentityMode") && hasKey("ReportColumns"));
            // 缺省副标题把新 key 显示出来（改了当场看得见，不用去设置表核对）
            Check("报警缺省含阈值-5kPa", linesOf("alarm").Contains("-5"));
            Check("完成缺省破空阀无", linesOf("done").Contains("无"));
            Check("上电缺省画面关", linesOf("power").Contains("画面：关"));
            Check("下料缺省口径现值报表缺省",
                linesOf("unload").Contains("现值") && linesOf("unload").Contains("缺省"));

            // ── 策略切换文本跟着变 ──
            dc.SkipVacuum = true;
            Check("跳过抽真空节点变文案",
                linesOf("vacuum").Contains("跳过") && linesOf("vacuum").Contains("豁免"));
            Check("跳过抽真空边变文案",
                Views.PolicyGraph.BuildEdgeLabel("e_vacuum_power", dc).Contains("跳过"));
            dc.SkipVacuum = false;
            dc.CompletionJudgePolicy = CompletionJudgePolicy.PendingReview;
            Check("待判定边变文案",
                Views.PolicyGraph.BuildEdgeLabel("e_done_unload", dc).Contains("待判定"));
            Check("待判定节点变文案",
                linesOf("unload").Contains("待判定"));
            dc.CompletionJudgePolicy = CompletionJudgePolicy.AutoPass;
            dc.CompleteExpression = "temp > 85";
            Check("完成表达式边变文案",
                Views.PolicyGraph.BuildEdgeLabel("e_power_done", dc).Contains("表达式"));
            dc.CompleteExpression = "";
            dc.PowerLossPolicy = PowerLossPolicy.ResumeRemaining;
            Check("续跑边变文案",
                Views.PolicyGraph.BuildEdgeLabel("e_recover_vacuum", dc).Contains("续跑"));
            // 【V1.73】MES节点文本跟着配置变
            dc.MesEnabled = true;
            dc.MesTriggers = "Complete,Alarm";
            dc.MesFieldMap = "eqId=device;lotNo=lot";
            dc.MesStaticFields = "line=L5";
            Check("MES节点变文案（开/触发/组数）",
                linesOf("mes").Contains("开") && linesOf("mes").Contains("Complete,Alarm")
                && linesOf("mes").Contains("2 组") && linesOf("mes").Contains("1 组"));
            dc.MesEnabled = false;
            dc.MesTriggers = "";
            dc.MesFieldMap = "";
            dc.MesStaticFields = "";
            Check("未知边空串", Views.PolicyGraph.BuildEdgeLabel("bogus", dc) == "");
            Check("配置null边空串", Views.PolicyGraph.BuildEdgeLabel("e_start_vacuum", null) == "");
            Check("固定边开阀", Views.PolicyGraph.BuildEdgeLabel("e_start_vacuum", dc) == "开阀");
            Check("复位边文案",
                Views.PolicyGraph.BuildEdgeLabel("e_alarm_unload", dc).Contains("复位"));
            Check("连线说明含两端标题",
                Views.PolicyGraph.BuildEdgeInfo("e_vacuum_power", dc).Contains("抽真空")
                && Views.PolicyGraph.BuildEdgeInfo("e_vacuum_power", dc).Contains("上电老化"));
            Check("未知连线说明", Views.PolicyGraph.BuildEdgeInfo("bogus", dc).Contains("未知"));

            // ── 台数进文本 ──
            var counts = new Views.PolicyGraph.FlowCounts
            {
                Idle = 70, Vacuuming = 1, Aging = 1,
                Completed = 0, Fault = 0, PendingJudge = 0, Snapshot = 0
            };
            Func<string, string> linesOf2 = id =>
                string.Join("|", Views.PolicyGraph.BuildNodeLines(id, dc, counts));
            Check("台数进节点文本",
                linesOf2("start").Contains("空闲 70 台")
                && linesOf2("vacuum").Contains("抽真空 1 台")
                && linesOf2("power").Contains("老化 1 台"));

            // ── 布局存取往返（运行目录隔离；用完删干净不污染） ──
            string layoutPath = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "PolicyLayout.json");
            bool hadLayout = File.Exists(layoutPath);
            string backup = hadLayout ? File.ReadAllText(layoutPath) : null;
            try
            {
                var pos = new Dictionary<string, System.Drawing.Point>();
                pos["start"] = new System.Drawing.Point(11, 22);
                pos["bogus-node"] = new System.Drawing.Point(1, 2);
                Views.PolicyGraph.LayoutStore.Save(pos);
                Check("布局文件落盘", File.Exists(layoutPath));
                var loaded = Views.PolicyGraph.LayoutStore.Load();
                Check("布局往返一致",
                    loaded.ContainsKey("start")
                    && loaded["start"].X == 11 && loaded["start"].Y == 22);
                Check("未知id也保留（画布按缺省画，不丢）",
                    loaded.ContainsKey("bogus-node"));
                // 钳制：±5000 外的坐标被拉回
                var wild = new Dictionary<string, System.Drawing.Point>();
                wild["start"] = new System.Drawing.Point(99999, -99999);
                Views.PolicyGraph.LayoutStore.Save(wild);
                var loaded2 = Views.PolicyGraph.LayoutStore.Load();
                Check("野坐标钳制±5000",
                    loaded2["start"].X == 5000 && loaded2["start"].Y == -5000);
                File.WriteAllText(layoutPath, "{broken json");
                Check("损坏布局按空处理",
                    Views.PolicyGraph.LayoutStore.Load().Count == 0);
                // V2 迁移：旧版本文件直接丢弃、回新缺省（V1 按旧挤列摆的，沿用会继续挤）
                File.WriteAllText(layoutPath, "{\"version\":1,\"nodes\":{\"start\":[11,22]}}");
                Check("旧版本布局按空处理（回新缺省）",
                    Views.PolicyGraph.LayoutStore.Load().Count == 0);
            }
            finally
            {
                try
                {
                    if (hadLayout) File.WriteAllText(layoutPath, backup);
                    else if (File.Exists(layoutPath)) File.Delete(layoutPath);
                }
                catch { }
            }

            // ── 缺省布局宽松锁（截图式两列：列距150/行距45+，节点不重叠） ──
            {
                Func<string, System.Drawing.Rectangle> rectOf = id =>
                {
                    var n = Views.PolicyGraph.FindNode(id);
                    return n != null ? n.DefaultRect : System.Drawing.Rectangle.Empty;
                };
                var rStart = rectOf("start");
                var rVacuum = rectOf("vacuum");
                var rPower = rectOf("power");
                var rDone = rectOf("done");
                var rAlarm = rectOf("alarm");
                var rRecover = rectOf("recover");
                var rUnload = rectOf("unload");
                var rMes = rectOf("mes");
                // 左列同 X、右列同 X（两列式，防漂）
                Check("缺省左列同X",
                    rStart.X == rVacuum.X && rVacuum.X == rPower.X && rPower.X == rDone.X);
                Check("缺省右列同X",
                    rAlarm.X == rRecover.X && rRecover.X == rUnload.X && rUnload.X == rMes.X);
                // 列间距≥130（之前 100 太挤，截图式 150）
                Check("缺省列间距≥130",
                    rRecover.Left - rStart.Right >= 130);
                // 左列行间距≥40（标题+3行文本不顶框）
                Check("缺省左列行距≥40",
                    rVacuum.Top - rStart.Bottom >= 40
                    && rPower.Top - rVacuum.Bottom >= 40
                    && rDone.Top - rPower.Bottom >= 40);
                // 报警底到下料顶留竖线位置≥40
                Check("缺省报警到下料竖距≥40",
                    rUnload.Top - rAlarm.Bottom >= 40);
                // MES 纯配置节点在右列最下，与下料留出行距
                Check("缺省MES在右列最下且行距≥40",
                    rMes.Top - rUnload.Bottom >= 40);
                // 两两不重叠（挤了当场红）
                var all = new System.Drawing.Rectangle[]
                    { rStart, rVacuum, rPower, rDone, rAlarm, rRecover, rUnload, rMes };
                bool overlap = false;
                for (int i = 0; i < all.Length && !overlap; i++)
                    for (int j = i + 1; j < all.Length && !overlap; j++)
                        if (all[i].IntersectsWith(all[j])) overlap = true;
                Check("缺省8节点两两不重叠", !overlap);
                // 横向边近水平：power↔alarm、done↔unload 中心Y差≤30（差多了线就斜得难看）
                int pcY = rPower.Top + rPower.Height / 2;
                int acY = rAlarm.Top + rAlarm.Height / 2;
                int dcY = rDone.Top + rDone.Height / 2;
                int ucY = rUnload.Top + rUnload.Height / 2;
                Check("power与alarm中心对齐（横边水平）", Math.Abs(pcY - acY) <= 30);
                Check("done与unload中心对齐（横边水平）", Math.Abs(dcY - ucY) <= 30);
                // 节点最小尺寸（宽≥210/高≥100，之前 190×92 装3行太挤）
                bool sizeOk = true;
                foreach (var r in all)
                    if (r.Width < 210 || r.Height < 100) sizeOk = false;
                Check("缺省节点最小210×100", sizeOk);
            }

            // ── 工艺策略窗窗体构造不断言弹窗（V1.71：构造即跑全部编辑器创建链，NRE 当场现形） ──
            {
                var cfg = new DeviceConfig();
                DeviceManager dmPolicy = null;
                Views.ProcessPolicyForm policyForm = null;
                try
                {
                    dmPolicy = new DeviceManager(cfg);
                    policyForm = new Views.ProcessPolicyForm(cfg, dmPolicy, true);
                    Check("工艺策略窗构造成功", policyForm != null);
                    Check("初态未选中无保存",
                        policyForm.SavedKeys != null && policyForm.SavedKeys.Count == 0);
                }
                finally
                {
                    try { if (policyForm != null) policyForm.Dispose(); } catch { }
                    try { if (dmPolicy != null) dmPolicy.Dispose(); } catch { }
                }
            }

            // ── 工艺策略窗无参构造（V1.72 Designer 拆分：只装边框不建画布，构造永不抛） ──
            {
                Views.ProcessPolicyForm bare = null;
                bool bareOk = true;
                try { bare = new Views.ProcessPolicyForm(); }
                catch { bareOk = false; }
                Check("工艺策略窗无参构造不抛", bareOk && bare != null);
                if (bare != null)
                {
                    var t = bare.GetType();
                    string[] chrome = { "_pnlRight", "_lblNodeTitle", "_pnlEditors",
                        "_btnSaveNode", "_btnResetLayout", "_btnClose", "_lblStatus" };
                    bool allBuilt = true;
                    foreach (var n in chrome)
                    {
                        var f = t.GetField(n, BindingFlags.NonPublic | BindingFlags.Instance);
                        if (f == null || f.GetValue(bare) == null) { allBuilt = false; break; }
                    }
                    Check("无参构造边框7件全建好", allBuilt);
                    try { bare.Dispose(); } catch { }
                }
            }

            // ── 右栏重建先Dispose再Clear（V1.72.12：终结器跨线程崩溃锁） ──
            // 复现：点节点→RebuildEditors→Controls.Clear()只摘不放，旧 UITextBox
            // 进终结器线程Dispose，Sunny内部读原生TextBox.Handle即炸（堆栈终点
            // TextBox.ResetAutoComplete←Dispose←Finalize）。修后旧控件当场释放。
            {
                Views.ProcessPolicyForm bare2 = null;
                try
                {
                    bare2 = new Views.ProcessPolicyForm();
                    var t2 = bare2.GetType();
                    var miRebuild = t2.GetMethod("RebuildEditors",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    Check("反射找到 RebuildEditors", miRebuild != null);
                    if (miRebuild != null)
                    {
                        var pnlF = t2.GetField("_pnlEditors",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        var pnl = (System.Windows.Forms.Control)pnlF.GetValue(bare2);
                        miRebuild.Invoke(bare2, null);
                        var firstHint = pnl.Controls.Count > 0 ? pnl.Controls[0] : null;
                        Check("首次重建有提示控件", firstHint != null);
                        miRebuild.Invoke(bare2, null);
                        Check("重建后旧控件已释放（不进终结器）",
                            firstHint != null && firstHint.IsDisposed);
                        Check("重建后新控件是另一实例",
                            pnl.Controls.Count > 0 && !Object.ReferenceEquals(pnl.Controls[0], firstHint));
                    }
                }
                finally
                {
                    try { if (bare2 != null) bare2.Dispose(); } catch { }
                }
            }

            // ── 检索框光标定位（V1.71 provider 泛化 Control：原生与 Sunny 同名属性） ──
            // provider 是 internal 类，走程序集按名取类型（与 ValidateValue 反射同套路）
            {
                var provType = typeof(DeviceConfig).Assembly.GetType(
                    "AgingTestSystem.Services.RecipeAutoCompleteProvider");
                Check("反射找到 provider 类型", provType != null);
                var miCaret = provType != null ? provType.GetMethod("SetCaretToEnd",
                    BindingFlags.NonPublic | BindingFlags.Static) : null;
                Check("反射找到 SetCaretToEnd", miCaret != null);
                if (miCaret != null)
                {
                    var tb = new TextBox { Text = "abc" };
                    miCaret.Invoke(null, new object[] { tb });
                    Check("原生框光标到末尾",
                        tb.SelectionStart == 3 && tb.SelectionLength == 0);
                    var stxt = new Sunny.UI.UITextBox { Text = "abc" };
                    bool sunnyOk = true;
                    try { miCaret.Invoke(null, new object[] { stxt }); }
                    catch { sunnyOk = false; }
                    Check("Sunny框光标调用不抛", sunnyOk);
                    Check("Sunny框光标到末尾",
                        stxt.SelectionStart == 3 && stxt.SelectionLength == 0);
                    bool panelOk = true;
                    try { miCaret.Invoke(null, new object[] { new Panel() }); }
                    catch { panelOk = false; }
                    Check("无光标属性控件静默跳过", panelOk);
                    tb.Dispose();
                    stxt.Dispose();
                }
            }
        }

        // =====================================================================
        // 12g. SoftActivation —— 软件激活（V1.87：与 HJVision 同源，同一套《获取激活码》工具）。
        //     Encrypt 与 HJVision MainForm.Encrypt / 工具 Form1.Encrypt 逐字节一致
        //     （RFC1321 标准向量 pin 住算法本身，关系式锁住"工具算的码本软件认"）；
        //     判定全是纯函数（字符串进出，不碰 WMI/真实 ini）；
        //     ini 读写走显式 path 进隔离临时目录（EnterCleanDir），不碰真实 MainSetting.ini；
        //     激活窗只构造不 Show（未 Show 不点按钮，直接调私有 handler，V1.86 先例）。
        // =====================================================================
        private static void SoftActivationTests()
        {
            const string cpu = "BFEBFBFF000906A9";   // 固定假 CPU：公式不断言真机 WMI，只锁方程

            // ── Encrypt：RFC1321 标准向量（MD5("")/MD5("a") 取前 15 字节 hex） ──
            Check("Encrypt空串",
                Services.SoftwareActivation.Encrypt("") == "d41d8cd98f00b204e9800998ecf842");
            Check("Encrypt(a)",
                Services.SoftwareActivation.Encrypt("a") == "0cc175b9c0f1b6a831c399e2697726");
            Check("Encrypt恒30字符",
                Services.SoftwareActivation.Encrypt(cpu).Length == 30);

            // ── 公式与工具同源（关系式即兼容契约：工具按同式算，本软件按同式认） ──
            Check("设备ID码=Encrypt(ID+A)",
                Services.SoftwareActivation.DeviceIdCode(cpu)
                == Services.SoftwareActivation.Encrypt(cpu + "A"));
            Check("设备码=Encrypt(ID+1)",
                Services.SoftwareActivation.DeviceCode(cpu)
                == Services.SoftwareActivation.Encrypt(cpu + "1"));
            Check("永久标记=Encrypt(ID+ALL)",
                Services.SoftwareActivation.PermanentMark(cpu)
                == Services.SoftwareActivation.Encrypt(cpu + "ALL"));
            Check("30天起点=Encrypt(ID+0)",
                Services.SoftwareActivation.TrialStartMark(cpu)
                == Services.SoftwareActivation.Encrypt(cpu + "0"));
            string dev = Services.SoftwareActivation.DeviceCode(cpu);
            Check("30天码=Encrypt(设备码+30)",
                Services.SoftwareActivation.Code30(dev)
                == Services.SoftwareActivation.Encrypt(dev + "30"));
            Check("永久码=Encrypt(设备码+ALL)",
                Services.SoftwareActivation.CodePermanent(dev)
                == Services.SoftwareActivation.Encrypt(dev + "ALL"));

            // ── 激活比对：先永久后 30 天，对不上静默（与 HJVision 激活_Click 对齐） ──
            Check("永久码认出",
                Services.SoftwareActivation.VerifyActivationCode(
                    Services.SoftwareActivation.CodePermanent(dev), dev)
                == Services.SoftwareActivation.ActivationKind.Permanent);
            Check("30天码认出",
                Services.SoftwareActivation.VerifyActivationCode(
                    Services.SoftwareActivation.Code30(dev), dev)
                == Services.SoftwareActivation.ActivationKind.ThirtyDays);
            Check("错码无操作",
                Services.SoftwareActivation.VerifyActivationCode("000000000000000000000000000000", dev)
                == Services.SoftwareActivation.ActivationKind.None);
            Check("空输入无操作",
                Services.SoftwareActivation.VerifyActivationCode("", dev)
                == Services.SoftwareActivation.ActivationKind.None);
            Check("空设备码无操作",
                Services.SoftwareActivation.VerifyActivationCode(
                    Services.SoftwareActivation.Code30(dev), "")
                == Services.SoftwareActivation.ActivationKind.None);

            // ── 设备绑定：RunHash1 == Encrypt(设备ID + "A") ──
            Check("绑定对上",
                Services.SoftwareActivation.IsDeviceBound(
                    Services.SoftwareActivation.DeviceIdCode(cpu), cpu));
            Check("绑定错位",
                !Services.SoftwareActivation.IsDeviceBound(
                    Services.SoftwareActivation.DeviceIdCode("OTHERCPU"), cpu));
            Check("空RunHash1=新设备",
                !Services.SoftwareActivation.IsDeviceBound("", cpu));

            // ── 计数格：0..839 找格，768 以下有效 ──
            Check("第0格找到",
                Services.SoftwareActivation.FindSlot(
                    Services.SoftwareActivation.Encrypt(cpu + "0"), cpu) == 0);
            Check("第767格找到",
                Services.SoftwareActivation.FindSlot(
                    Services.SoftwareActivation.Encrypt(cpu + "767"), cpu) == 767);
            Check("第839格找到",
                Services.SoftwareActivation.FindSlot(
                    Services.SoftwareActivation.Encrypt(cpu + "839"), cpu) == 839);
            Check("840之外找不到",
                Services.SoftwareActivation.FindSlot(
                    Services.SoftwareActivation.Encrypt(cpu + "840"), cpu) == -1);
            Check("乱串找不到",
                Services.SoftwareActivation.FindSlot("zzzz", cpu) == -1);
            Check("0与767有效/768与-1无效",
                Services.SoftwareActivation.IsSlotValid(0)
                && Services.SoftwareActivation.IsSlotValid(767)
                && !Services.SoftwareActivation.IsSlotValid(768)
                && !Services.SoftwareActivation.IsSlotValid(-1));
            Check("0格剩30天", Services.SoftwareActivation.SlotDaysLeft(0) == 30);
            Check("24格剩29天", Services.SoftwareActivation.SlotDaysLeft(24) == 29);

            // ── 综合判定：先设备、再永久、再计数 ──
            int slot;
            int days;
            Check("未绑定=新设备",
                Services.SoftwareActivation.ComputeStatus("nope",
                    Services.SoftwareActivation.Encrypt(cpu + "0"), cpu, out slot, out days)
                == Services.SoftwareActivation.ActivationStatus.NewDevice);
            Check("永久",
                Services.SoftwareActivation.ComputeStatus(
                    Services.SoftwareActivation.DeviceIdCode(cpu),
                    Services.SoftwareActivation.PermanentMark(cpu), cpu, out slot, out days)
                == Services.SoftwareActivation.ActivationStatus.Permanent);
            Check("计数有效=试用",
                Services.SoftwareActivation.ComputeStatus(
                    Services.SoftwareActivation.DeviceIdCode(cpu),
                    Services.SoftwareActivation.Encrypt(cpu + "5"), cpu, out slot, out days)
                == Services.SoftwareActivation.ActivationStatus.InTrial
                && slot == 5 && days == 30);
            Check("768格=过期",
                Services.SoftwareActivation.ComputeStatus(
                    Services.SoftwareActivation.DeviceIdCode(cpu),
                    Services.SoftwareActivation.Encrypt(cpu + "768"), cpu, out slot, out days)
                == Services.SoftwareActivation.ActivationStatus.Expired);
            Check("找不到=过期",
                Services.SoftwareActivation.ComputeStatus(
                    Services.SoftwareActivation.DeviceIdCode(cpu),
                    "nope", cpu, out slot, out days)
                == Services.SoftwareActivation.ActivationStatus.Expired);
            Check("永久文案",
                Services.SoftwareActivation.StatusText(
                    Services.SoftwareActivation.ActivationStatus.Permanent, -1, 0)
                == "激活状态: 永久使用");
            Check("试用文案带天数",
                Services.SoftwareActivation.StatusText(
                    Services.SoftwareActivation.ActivationStatus.InTrial, 5, 30).Contains("30"));
            Check("过期文案",
                Services.SoftwareActivation.StatusText(
                    Services.SoftwareActivation.ActivationStatus.Expired, -1, 0).Contains("已过期"));
            Check("新设备文案",
                Services.SoftwareActivation.StatusText(
                    Services.SoftwareActivation.ActivationStatus.NewDevice, -1, 0).Contains("未绑定"));

            // ── 付费即恢复判定（V1.88.1：只有永久/试用中才解灰，新设备/过期不恢复） ──
            Check("永久应恢复",
                Services.SoftwareActivation.ShouldRestoreUserPermission(
                    Services.SoftwareActivation.ActivationStatus.Permanent));
            Check("试用中应恢复",
                Services.SoftwareActivation.ShouldRestoreUserPermission(
                    Services.SoftwareActivation.ActivationStatus.InTrial));
            Check("新设备不应恢复",
                !Services.SoftwareActivation.ShouldRestoreUserPermission(
                    Services.SoftwareActivation.ActivationStatus.NewDevice));
            Check("过期不应恢复",
                !Services.SoftwareActivation.ShouldRestoreUserPermission(
                    Services.SoftwareActivation.ActivationStatus.Expired));

            // ── ini 往返（隔离目录，不碰真实 MainSetting.ini） ──
            string dir = EnterCleanDir();
            string ini = Path.Combine(dir, "MainSetting.ini");
            Services.SoftwareActivation.WriteValueTo(
                ini, Services.SoftwareActivation.KeyDevice,
                Services.SoftwareActivation.DeviceIdCode(cpu));
            Services.SoftwareActivation.WriteValueTo(
                ini, Services.SoftwareActivation.KeyRuntime,
                Services.SoftwareActivation.Encrypt(cpu + "3"));
            string h1;
            string h2;
            Services.SoftwareActivation.ReadRunHashFrom(ini, out h1, out h2);
            Check("ini双键往返",
                h1 == Services.SoftwareActivation.DeviceIdCode(cpu)
                && h2 == Services.SoftwareActivation.Encrypt(cpu + "3"));
            string m1;
            string m2;
            Services.SoftwareActivation.ReadRunHashFrom(
                Path.Combine(dir, "NoSuch.ini"), out m1, out m2);
            Check("缺文件读空", m1 == "" && m2 == "");
            // 推进一格 = HJVision Tick 语义（找到 i 就写 i+1）
            int s = Services.SoftwareActivation.FindSlot(h2, cpu);
            Services.SoftwareActivation.WriteValueTo(ini,
                Services.SoftwareActivation.KeyRuntime,
                Services.SoftwareActivation.Encrypt(cpu + (s + 1).ToString()));
            Services.SoftwareActivation.ReadRunHashFrom(ini, out h1, out h2);
            Check("推进一格", Services.SoftwareActivation.FindSlot(h2, cpu) == 4);

            // ── 空模板：缺文件自动建（只建不覆盖，空值=新设备语义） ──
            string tpl = Path.Combine(dir, "Tpl", "MainSetting.ini");
            Services.SoftwareActivation.EnsureIniTemplateTo(tpl);
            Check("缺文件建出模板", File.Exists(tpl));
            string t1;
            string t2;
            Services.SoftwareActivation.ReadRunHashFrom(tpl, out t1, out t2);
            Check("模板两键读空", t1 == "" && t2 == "");
            Check("空模板=新设备语义",
                !Services.SoftwareActivation.IsDeviceBound(t1, cpu));
            Services.SoftwareActivation.WriteValueTo(tpl,
                Services.SoftwareActivation.KeyDevice,
                Services.SoftwareActivation.DeviceIdCode(cpu));
            Services.SoftwareActivation.EnsureIniTemplateTo(tpl);   // 已有文件再调
            Services.SoftwareActivation.ReadRunHashFrom(tpl, out t1, out t2);
            Check("已有文件不覆盖",
                t1 == Services.SoftwareActivation.DeviceIdCode(cpu));

            // ── 激活窗构造（只构造不 Show，不断言弹窗） ──
            var fl = typeof(SoftActivation);
            var flInst = BindingFlags.NonPublic | BindingFlags.Instance;
            SoftActivation frm = null;
            try
            {
                frm = new SoftActivation();
                var fId = fl.GetField("_txtDeviceId", flInst);
                var fCode = fl.GetField("_txtDeviceCode", flInst);
                var fAct = fl.GetField("_txtActivationCode", flInst);
                var fStatus = fl.GetField("_lblStatus", flInst);
                var idBox = fId != null ? fId.GetValue(frm) as Sunny.UI.UITextBox : null;
                var codeBox = fCode != null ? fCode.GetValue(frm) as Sunny.UI.UITextBox : null;
                var actBox = fAct != null ? fAct.GetValue(frm) as Sunny.UI.UITextBox : null;
                var statusLbl = fStatus != null ? fStatus.GetValue(frm) as Control : null;
                Check("无参构造三框齐全",
                    frm != null && idBox != null && codeBox != null && actBox != null
                    && statusLbl != null);
                // V1.88.1：付费即恢复标记——新窗默认 false（打开看看就关不触发主窗重查）
                Check("新窗未激活成功", frm != null && !frm.ActivatedSuccessfully);
                if (idBox != null && codeBox != null && statusLbl != null)
                {
                    // 不 Show 直接刷（读真机 WMI + 真实 ini；关系式与机器无关恒成立）
                    frm.RefreshStatus();
                    Check("设备码=Encrypt(设备ID+1)",
                        codeBox.Text == Services.SoftwareActivation.DeviceCode(idBox.Text),
                        "len=" + (codeBox.Text ?? "").Length);
                    Check("状态行非空且激活口径",
                        !string.IsNullOrWhiteSpace(statusLbl.Text)
                        && statusLbl.Text.StartsWith("激活状态"),
                        statusLbl.Text);
                    // 错码点激活：静默无操作不抛（与 HJVision 一致；直接调 handler）
                    string before = statusLbl.Text;
                    var actHandler = fl.GetMethod("BtnActivate_Click", flInst);
                    Check("激活handler存在", actHandler != null);
                    if (actHandler != null)
                    {
                        actBox.Text = "错的激活码";
                        actHandler.Invoke(frm, new object[] { actBox, EventArgs.Empty });
                        Check("错码静默（状态不动）", statusLbl.Text == before, statusLbl.Text);
                        // V1.88.1：错码不写文件也不置位，主窗不会误解灰（成功路径写真实 ini，不进回归）
                        Check("错码不置成功位", !frm.ActivatedSuccessfully);
                    }
                }
            }
            finally { try { if (frm != null) frm.Dispose(); } catch { } }
        }

        /// <summary>轮询等待（MesTests 用：后台线程投递需等待；超时返回 false）</summary>
        private static bool WaitFor(Func<bool> condition, int timeoutMs)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try { if (condition()) return true; } catch { }
                Thread.Sleep(50);
            }
            try { return condition(); } catch { return false; }
        }

        /// <summary>数当日事件 CSV 里含标记的行数（MesTests 的 Mock/边沿断言用）</summary>
        private static int CountCsvLines(string marker)
        {
            try
            {
                string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs",
                    "TestLog_" + DateTime.Now.ToString("yyyyMMdd") + ".csv");
                if (!File.Exists(file)) return 0;
                int n = 0;
                foreach (string line in File.ReadAllLines(file))
                {
                    if (line.Contains(marker)) n++;
                }
                return n;
            }
            catch { return 0; }
        }

        // =====================================================================
        // 13. TestSessionStore —— 在测任务快照持久化（断电恢复，V1.59 新增）
        //     生命周期：有在测任务才存；急停/放弃恢复/全部结束删除；损坏按无任务处理。
        // =====================================================================
        private static void TestSessionStoreTests()
        {
            // 【大扫荡】快照路径改绝对（BaseDirectory）后，隔离改走 BaseDirOverride 测试缝
            //（相对路径字面量同步改拼装路径；finally 复位，零残留）。
            string dir = EnterCleanDir();
            var fBase = typeof(TestSessionStore).GetField("BaseDirOverride",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (fBase != null) fBase.SetValue(null, dir);
            string snapPath = Path.Combine(dir, "TestSession.json");
            try
            {
            // 无文件 → 无任务
            Check("无快照文件时 Load=null", TestSessionStore.Load() == null);
            // 【大扫荡】原子写基础：写后内容完整 + 无 .tmp 残留半截。
            string atomPath = Path.Combine(dir, "atom_test.txt");
            Services.AtomicFile.WriteAllText(atomPath, "中文内容\r\nline2");
            try
            {
                Check("原子写文件内容完整", File.ReadAllText(atomPath, Encoding.UTF8).Contains("中文内容"));
                bool tmpLeft = false;
                foreach (string f in Directory.GetFiles(dir, "*.tmp_*")) { tmpLeft = true; break; }
                Check("原子写无临时残留", !tmpLeft);
                // 【复查补齐】二次覆盖走 File.Replace 原子替换：旧文件被完整替换、无残留
                Services.AtomicFile.WriteAllText(atomPath, "第二版内容");
                Check("覆盖写内容完整",
                    File.ReadAllText(atomPath, Encoding.UTF8).Contains("第二版内容"));
                tmpLeft = false;
                foreach (string f in Directory.GetFiles(dir, "*.tmp_*")) { tmpLeft = true; break; }
                Check("覆盖写无临时残留", !tmpLeft);
            }
            finally { try { File.Delete(atomPath); } catch { } }

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
            Check("快照文件确实生成", File.Exists(snapPath));

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
            File.WriteAllText(snapPath, "{ 这不是合法 json !!!");
            Check("损坏快照静默返回 null", TestSessionStore.Load() == null);

            // 空在测清单的快照视为无任务
            TestSessionStore.Save(new TestSession());
            Check("空清单快照 Load=null", TestSessionStore.Load() == null);

            // V1.62：Stations:null 与字面量 null 的 Load 语义
            File.WriteAllText(snapPath, "{\"LotNumber\":\"L\",\"Stations\":null}");
            Check("Stations为null Load=null", TestSessionStore.Load() == null);
            File.WriteAllText(snapPath, "null");
            Check("json字面量null Load=null", TestSessionStore.Load() == null);
            Check("Save(null)返回false", TestSessionStore.Save(null) == false);

            TestSessionStore.Clear();
            }
            finally
            {
                try { if (fBase != null) fBase.SetValue(null, null); } catch { }
            }
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
                DisplayMode = "白场",
                DelayTime = TimeSpan.FromSeconds(30),
                StartTime = TimeSpan.FromHours(4)
            };
            var ic = info.Clone();
            ic.RecipeNegativePressure = 0m;
            Check("StationInfo.Clone 复制负压值且深拷贝互不影响",
                info.RecipeNegativePressure == -60m && ic.DelayTime == TimeSpan.FromSeconds(30));
            Check("StationInfo.Clone 复制显示模式",
                ic.DisplayMode == "白场");
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

                // _boolKeys 17 项逐项过校验（防"只加一边"的配置漂移；V1.68 +MesEnabled/MesMockEnabled；V1.69 +SkipVacuum；V1.73 +VentValveEnabled；V1.74 +UsePowerMeter；V1.75 +DisplayModeEnabled）
                var boolKeys = (HashSet<string>)typeof(SettingsForm).GetField("_boolKeys",
                    BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                Check("布尔键17项", boolKeys != null && boolKeys.Count == 17);
                if (boolKeys != null)
                {
                    bool allBoolOk = boolKeys.All(k => ok(k, "true") && ok(k, "false") && !ok(k, "YES"));
                    Check("布尔键true/false过YES不过", allBoolOk);
                    Check("含关键布尔键",
                        boolKeys.Contains("AlarmWhenPressureHigherThanThreshold")
                        && boolKeys.Contains("ScannerDebugLog")
                        && boolKeys.Contains("UseMockCommunication")
                        && boolKeys.Contains("MesEnabled")
                        && boolKeys.Contains("MesMockEnabled")
                        && boolKeys.Contains("SkipVacuum")
                        && boolKeys.Contains("VentValveEnabled")
                        && boolKeys.Contains("UsePowerMeter")
                        && boolKeys.Contains("DisplayModeEnabled"));
                }
            }

            // —— PersistChanges 统一保存路（V1.70 重构锁：拦截语义与按钮时代逐字一致） ——
            // 写 exe.config 与 Policy.json：harness 运行目录隔离（run 临时目录），
            // 用完备份还原，不污染后续模块。
            EnterCleanDir();
            {
                string policyPath = ProjectPolicyStore.PolicyFilePath;
                bool hadPolicy = File.Exists(policyPath);
                string policyBackup = hadPolicy ? File.ReadAllText(policyPath) : null;
                string exeConfig = AppDomain.CurrentDomain.BaseDirectory + "TestRunner.exe.config";
                // 注：exe.config 可能不存在（首次运行），备份判空处理
                bool hadExeCfg = File.Exists(exeConfig);
                string exeBackup = hadExeCfg ? File.ReadAllText(exeConfig) : null;
                try
                {
                    // 1) 矛盾组合被拦（联停开+上限0），且不写文件不改内存
                    var c1 = new DeviceConfig();
                    SettingsForm.PersistResult r1;
                    string e1;
                    bool ok1 = SettingsForm.PersistChanges(c1,
                        new Dictionary<string, string> { { "FanTempShutdownEnabled", "true" } },
                        out r1, out e1);
                    Check("矛盾组合拦截", !ok1 && r1 == null && e1 != null && e1.Contains("上限"));
                    Check("拦截后内存不动", c1.FanTempShutdownEnabled == false);

                    // 2) MES 开关无地址被拦
                    var c2 = new DeviceConfig();
                    SettingsForm.PersistResult r2;
                    string e2;
                    bool ok2 = SettingsForm.PersistChanges(c2,
                        new Dictionary<string, string> { { "MesEnabled", "true" } },
                        out r2, out e2);
                    Check("MES无地址拦截", !ok2 && e2 != null && e2.Contains("MesEndpoint"));

                    // 3) 策略键写 Policy.json + 内存热回写
                    var c3 = new DeviceConfig();
                    SettingsForm.PersistResult r3;
                    string e3;
                    bool ok3 = SettingsForm.PersistChanges(c3,
                        new Dictionary<string, string> { { "ZeroDurationPolicy", "Block" } },
                        out r3, out e3);
                    Check("策略保存成功", ok3 && e3 == null && r3 != null
                        && r3.SavedKeys.Contains("ZeroDurationPolicy"));
                    Check("策略内存热回写", c3.ZeroDurationPolicy == ZeroDurationPolicy.Block);
                    Check("策略落盘Policy.json",
                        File.Exists(policyPath)
                        && File.ReadAllText(policyPath).Contains("Block"));

                    // 4) 机器键写 exe.config + 内存热回写
                    var c4 = new DeviceConfig();
                    SettingsForm.PersistResult r4;
                    string e4;
                    bool ok4 = SettingsForm.PersistChanges(c4,
                        new Dictionary<string, string> { { "CollectInterval", "2000" } },
                        out r4, out e4);
                    Check("机器键保存成功", ok4 && r4 != null);
                    Check("机器键内存热回写", c4.CollectInterval == 2000);
                    Check("机器键落盘exe.config",
                        File.Exists(exeConfig) && File.ReadAllText(exeConfig).Contains("CollectInterval"));

                    // 5) 空改动直接成功（无文件写入也无报错）
                    var c5 = new DeviceConfig();
                    SettingsForm.PersistResult r5;
                    string e5;
                    Check("空改动成功",
                        SettingsForm.PersistChanges(c5,
                            new Dictionary<string, string>(), out r5, out e5)
                        && r5 != null && r5.SavedKeys.Count == 0);

                    // 6) null 参数 fail-fast（不抛）
                    SettingsForm.PersistResult r6 = null;
                    string e6 = null;
                    bool ok6 = false;
                    try { ok6 = SettingsForm.PersistChanges(null, null, out r6, out e6); }
                    catch { ok6 = true; /* 不应抛 */ }
                    Check("null参数拦截不抛", !ok6 && e6 != null);
                }
                finally
                {
                    try
                    {
                        if (hadPolicy) File.WriteAllText(policyPath, policyBackup);
                        else if (File.Exists(policyPath)) File.Delete(policyPath);
                        if (hadExeCfg) File.WriteAllText(exeConfig, exeBackup);
                        else if (File.Exists(exeConfig)) File.Delete(exeConfig);
                        System.Configuration.ConfigurationManager.RefreshSection("appSettings");
                    }
                    catch { }
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
            Check("报表列→弹窗格", cellOf("ReportColumns", "时间=time") is DataGridViewPopupEditCell);
            Check("显示字典→弹窗格", cellOf("DisplayModes", "白场") is DataGridViewPopupEditCell);
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
                    // 【_descriptions 已转 static（说明与策略窗标题 tooltip 同源，无实例可取）】
                    // 反射按 Static 取；实例字段已无，Instance 会取到 null。
                    var desc = (Dictionary<string, string>)typeof(SettingsForm).GetField("_descriptions",
                        BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
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

            // ── 非模态编辑弹窗关闭即释放（V1.72.13：终结器跨线程崩溃锁） ──
            // 复现：设置窗点映射/IP/规则格弹非模态窗，FormClosed 只回写不 Dispose，
            // 里面的 Sunny 输入框/表格成孤儿，GC 时终结器线程 Dispose 即炸
            // （与工艺策略窗右栏同病根；案发时机看 GC，报错时正在干什么都是巧合）。
            // 走生产挂接路径验证：反射调 ShowXxxPopup → OpenForms 按类型找窗 →
            // Close → IsDisposed 必须 true（修的是 handler，裸 Show/Close 测不到）。
            SettingsForm sfPop = null;
            try
            {
                sfPop = new SettingsForm(new DeviceConfig());
                sfPop.Show();
                Application.DoEvents();
                var gridF = typeof(SettingsForm).GetField("_grid",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var grid = (DataGridView)gridF.GetValue(sfPop);
                Check("反射拿到设置表格", grid != null && grid.Rows.Count > 0);
                if (grid != null && grid.Rows.Count > 0)
                {
                    var cases = new[]
                    {
                        new { Method = "ShowIpListPopup", Type = typeof(Controls.IpListEditorPopup),
                            Value = (object)"192.168.1.220", Name = "IP弹窗" },
                        new { Method = "ShowIoMappingPopup", Type = typeof(Controls.IoMappingEditorPopup),
                            Value = (object)"0x2000@0x00->0x2009@0x01", Name = "IO映射弹窗" },
                        new { Method = "ShowRuleListPopup", Type = typeof(Controls.RuleListEditorPopup),
                            Value = (object)"", Name = "规则弹窗" },
                        new { Method = "ShowReportColumnsPopup", Type = typeof(Controls.ReportColumnsEditorPopup),
                            Value = (object)"时间=time;批号=lot", Name = "报表列弹窗" },
                        new { Method = "ShowDisplayModesPopup", Type = typeof(Controls.DisplayModesEditorPopup),
                            Value = (object)"白场,红场", Name = "显示字典弹窗" },
                    };
                    foreach (var c in cases)
                    {
                        var mi = typeof(SettingsForm).GetMethod(c.Method,
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        Check("反射找到 " + c.Method, mi != null);
                        if (mi == null) continue;
                        mi.Invoke(sfPop, new object[] { grid, 0, c.Value });
                        Application.DoEvents();
                        Form found = null;
                        foreach (Form f in Application.OpenForms)
                        {
                            if (f != null && f.GetType() == c.Type) { found = f; break; }
                        }
                        Check(c.Name + "已弹出", found != null);
                        if (found != null)
                        {
                            found.Close();
                            Application.DoEvents();
                            Check(c.Name + "Close后已释放（不进终结器）", found.IsDisposed);
                        }
                    }
                }
            }
            finally
            {
                // 兜底：关残留弹窗 + 设置窗，不留孤儿污染后模块
                foreach (Form f in new System.Collections.ArrayList(Application.OpenForms))
                {
                    try
                    {
                        if (f != null && !(f is SettingsForm) && !f.IsDisposed
                            && (f is Controls.IpListEditorPopup
                                || f is Controls.IoMappingEditorPopup
                                || f is Controls.RuleListEditorPopup)) f.Dispose();
                    }
                    catch { }
                }
                try { if (sfPop != null) { sfPop.Close(); sfPop.Dispose(); } } catch { }
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

                // 互逆：写入器转义 → 解析器还原（11 列对齐，详情含逗号引号）
                EnterCleanDir();
                TestEventLogger.Write("INV", 5, "报警", "详情,有\"引号\"", -5.5m, 36.6f, null, "SN9", "配方Z", "FAIL");
                string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs",
                    "TestLog_" + DateTime.Now.ToString("yyyyMMdd") + ".csv");
                string[] lines = ReadAllLinesShared(file);
                string[] f = parse(lines[lines.Length - 1]);
                Check("互逆11列", f.Length == 11);
                if (f.Length == 11)
                {
                    Check("互逆批号/SN/配方", f[1] == "INV" && f[2] == "SN9" && f[3] == "配方Z");
                    Check("互逆编号/事件/结果", f[4] == "5" && f[5] == "报警" && f[6] == "FAIL");
                    Check("互逆详情还原", f[7] == "详情,有\"引号\"");
                    Check("互逆压力温度", f[8] == "-5.5" && f[9] == "36.6");
                    Check("互逆电流空", f[10] == "");
                }
                // V1.76：报表格字段映射（sn/recipe/result 落专属列，防索引漂移）
                var logType = typeof(HistoryRecordForm).GetNestedType("LogEntry",
                    BindingFlags.NonPublic);
                var cellMi = typeof(HistoryRecordForm).GetMethod("GetReportCellText",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Check("反射找到 LogEntry 与 GetReportCellText", logType != null && cellMi != null);
                if (logType != null && cellMi != null)
                {
                    object log = Activator.CreateInstance(logType);
                    logType.GetProperty("Sn").SetValue(log, "SN9", null);
                    logType.GetProperty("Recipe").SetValue(log, "配方Z", null);
                    logType.GetProperty("Result").SetValue(log, "FAIL", null);
                    Func<string, string> cell = fd => (string)cellMi.Invoke(null, new object[] { log, fd });
                    Check("报表格SN/配方/结果映射",
                        cell("sn") == "SN9" && cell("recipe") == "配方Z" && cell("result") == "FAIL");
                }
                // V1.75：报表列按钮权限门（默认无权限；管理员才可见，操作员看不见改不了）
                var flagF = typeof(HistoryRecordForm).GetField("_canConfigureColumns",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var btnF = typeof(HistoryRecordForm).GetField("btnColumns",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Check("反射找到权限旗与列按钮", flagF != null && btnF != null);
                if (flagF != null && btnF != null)
                {
                    Check("默认无列配置权限",
                        Equals(flagF.GetValue(form), false));
                    var btnCol = btnF.GetValue(form) as Control;
                    Check("报表列按钮已建好", btnCol != null && btnCol.Text == "报表列设置");
                }
            }
            finally { form.Dispose(); }

            // V1.75：管理员构造带权限旗（按钮显隐逻辑走 Visible，窗体未 Show 时读恒 false，
            // 故只锁权限旗本身；显隐已在构造赋值，界面手工验证）。
            HistoryRecordForm adminForm = null;
            try { adminForm = new HistoryRecordForm(true); }
            catch (Exception ex) { Check("管理员历史窗可构造", false, ex.GetType().Name + ":" + ex.Message); }
            if (adminForm != null)
            {
                try
                {
                    var flagA = typeof(HistoryRecordForm).GetField("_canConfigureColumns",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    Check("管理员有列配置权限",
                        flagA != null && Equals(flagA.GetValue(adminForm), true));
                }
                finally { adminForm.Dispose(); }
            }
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
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(lotForm) as Sunny.UI.UITextBox;
                Check("反射拿到批号框(V1.71 Sunny)", txtLot != null);
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
            var rmForm = new RecipeManagerForm(recipes, -5m);
            try
            {
                // V1.66：负压/显示模式框回填（存什么显什么：配方0→框0，null→空串，无魔法值）
                var nudP = typeof(RecipeManagerForm).GetField("nudNegativePressure",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(rmForm) as NumericUpDown;
                var txtM = typeof(RecipeManagerForm).GetField("cmbDisplayMode",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(rmForm) as Sunny.UI.UIComboBox;
                Check("负压框回填配方值", nudP != null && nudP.Value == 0m);
                Check("显示模式框null回填空串(V1.71 Sunny)", txtM != null && txtM.Text == "");
                Check("维度关时配方窗下拉空(V1.75: 隐藏=恒空，不读字典)",
                    txtM != null && txtM.Items.Count == 0 && txtM.Text == "");
                Check("显示模式是下拉单选(V1.74)",
                    txtM != null && txtM.DropDownStyle == Sunny.UI.UIDropDownStyle.DropDownList);
                Check("维度关时配方窗收缩(V1.75: 355→317)",
                    rmForm.ClientSize.Height == 317);
                // 配方管理窗6项tooltip全覆盖（标签+输入框双挂，超40字走WrapTooltip换行）：
                // 以前只有显示模式有说明，其余5项悬停空白；现在6项与批量窗/工位窗同口径。
                var tipRM = typeof(RecipeManagerForm).GetField("_tip",
                    BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(rmForm) as ToolTip;
                Check("配方窗tooltip容器已建", tipRM != null);
                if (tipRM != null)
                {
                    Func<string, Control> rmCtl = n => typeof(RecipeManagerForm).GetField(n,
                        BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(rmForm) as Control;
                    Func<string, string> rmTip = n => { var c = rmCtl(n); return c == null ? "" : tipRM.GetToolTip(c); };
                    Check("配方名称tooltip双挂（含覆盖更新）",
                        rmTip("lblRecipeName").Contains("覆盖更新") && rmTip("txtRecipeName") == rmTip("lblRecipeName")
                        && rmTip("lblRecipeName").Length > 0);
                    Check("延时时间tooltip双挂（含延时开启）",
                        rmTip("lblDelayTime").Contains("延时开启") && rmTip("nudDelayHours") == rmTip("lblDelayTime"));
                    Check("启动时间tooltip双挂（含延时到达）",
                        rmTip("lblStartTime").Contains("延时到达") && rmTip("nudStartHours") == rmTip("lblStartTime"));
                    Check("极限温度tooltip双挂（0~300℃追溯）",
                        rmTip("lblLimitTemp").Contains("0~300") && rmTip("lblLimitTemp").Contains("追溯")
                        && rmTip("nudLimitTemp") == rmTip("lblLimitTemp"));
                    Check("负压阈值tooltip双挂（含定格）",
                        rmTip("lblNegativePressure").Contains("定格")
                        && rmTip("nudNegativePressure") == rmTip("lblNegativePressure"));
                    Check("显示模式tooltip双挂（含追溯）",
                        rmTip("lblDisplayMode").Contains("追溯") && rmTip("cmbDisplayMode") == rmTip("lblDisplayMode"));
                    Check("超长tooltip已换行（负压条含换行且每行≤40字）",
                        rmTip("lblNegativePressure").Contains("\r\n")
                        && rmTip("lblNegativePressure").Split(new[] { "\r\n" }, StringSplitOptions.None).All(
                            line => line.Length <= 40));
                    // 大白话版（V1.86.5）：时间轴举例+负压符号+0语义必须讲清，否则小白照样迷茫。
                    // 注：tooltip 含 WrapTooltip 插的换行，查内容先去换行（换行位置随字数浮动，
                    // 直接 Contains 会被从中切断的例子误杀，本条红过）。
                    Func<string, string> rmTipFlat = n => rmTip(n).Replace("\r\n", "");
                    Check("延时说明讲清先开阀后上电",
                        rmTipFlat("lblDelayTime").Contains("只开真空阀") && rmTipFlat("lblDelayTime").Contains("00:00:30"));
                    Check("启动说明讲清0回退全局",
                        rmTipFlat("lblStartTime").Contains("全局") && rmTipFlat("lblStartTime").Contains("08:00:00"));
                    Check("负压说明举例符号方向",
                        rmTipFlat("lblNegativePressure").Contains("-7") && rmTipFlat("lblNegativePressure").Contains("-3"));
                    Check("长说明悬停多停留15秒（默认5秒看不完）",
                        tipRM.AutoPopDelay == 15000);
                }
                var rmFormOn = new RecipeManagerForm(new List<RecipeConfig>(),
                    -5m, new DeviceConfig { DisplayModeEnabled = true });
                try
                {
                    Check("维度开时配方窗高度不变(V1.75)",
                        rmFormOn.ClientSize.Height == 355);
                    var cmbOn = typeof(RecipeManagerForm).GetField("cmbDisplayMode",
                        BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(rmFormOn) as Sunny.UI.UIComboBox;
                    Check("维度开时下拉项=缺省预设8项(V1.75)",
                        cmbOn != null && cmbOn.Items.Count == 8 && cmbOn.Items.Contains("白场"));
                }
                finally { rmFormOn.Dispose(); }
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

            // —— 工位窗温度读取（V1.63 文本框改数字框：恒合法+范围断言） ——
            StationSettingsForm stForm = null;
            try { stForm = new StationSettingsForm(null, new DeviceConfig(), new List<RecipeConfig>(), 1); }
            catch { }
            Check("工位窗可构造", stForm != null);
            if (stForm != null)
            {
                try
                {
                    var nudTemp = typeof(StationSettingsForm).GetField("nudTemp",
                        BindingFlags.NonPublic | BindingFlags.Instance).GetValue(stForm) as NumericUpDown;
                    var parseTemp = typeof(StationSettingsForm).GetMethod("ParseTemperature",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    Check("反射拿到温度框与解析", nudTemp != null && parseTemp != null);
                    if (nudTemp != null && parseTemp != null)
                    {
                        Check("数字框口径1位小数/0~300",
                            nudTemp.DecimalPlaces == 1 && nudTemp.Minimum == 0m && nudTemp.Maximum == 300m);
                        nudTemp.Value = 37.5m;
                        Check("温度37.5直读",
                            (decimal)parseTemp.Invoke(stForm, null) == 37.5m);
                        nudTemp.Value = 0m;
                        Check("温度0直读", (decimal)parseTemp.Invoke(stForm, null) == 0m);
                        // 非法输入进不来（数字框天然保证）：回填钳制走同一套 Min/Max，
                        // 用配方回填路径验证超界钳制
                        var onSel = typeof(StationSettingsForm).GetMethod("OnRecipeSelected",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        onSel.Invoke(stForm, new object[]
                        {
                            new RecipeConfig
                            {
                                Name = "T", DelayTime = TimeSpan.Zero, StartTime = TimeSpan.Zero,
                                LimitTemperature = 9999m
                            }
                        });
                        Check("配方回填超界钳300", nudTemp.Value == 300m);
                    }
                }
                finally { stForm.Dispose(); }
            }

            // —— 破空按钮显隐 + 全项tooltip（V1.73：无阀隐藏；tooltip超40字换行） ——
            {
                StationSettingsForm stNoVent = null;
                try { stNoVent = new StationSettingsForm(null, new DeviceConfig(), new List<RecipeConfig>(), 1); }
                catch { }
                Check("无阀工位窗可构造", stNoVent != null);
                if (stNoVent != null)
                {
                    try
                    {
                        var btnBreak = typeof(StationSettingsForm).GetField("btnBreakVacuum",
                            BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(stNoVent) as Control;
                        Check("无阀破空按钮隐藏", btnBreak != null && btnBreak.Visible == false);
                        var tip = typeof(StationSettingsForm).GetField("_tip",
                            BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(stNoVent) as ToolTip;
                        var lblDm = typeof(StationSettingsForm).GetField("lblDisplayMode",
                            BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(stNoVent) as Control;
                        Check("工位窗tooltip已挂（含显示模式追溯说明）",
                            tip != null && lblDm != null && tip.GetToolTip(lblDm).Contains("追溯"));
                        var cmbDm = typeof(StationSettingsForm).GetField("cmbDisplayMode",
                            BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(stNoVent) as Sunny.UI.UIComboBox;
                        Check("工位窗显示模式是下拉单选(V1.74)",
                            cmbDm != null && cmbDm.DropDownStyle == Sunny.UI.UIDropDownStyle.DropDownList);
                        Check("维度关时工位窗下拉空(V1.75)",
                            cmbDm != null && cmbDm.Items.Count == 0);
                        Check("维度关时工位窗收缩(V1.75: 370→330)",
                            stNoVent.ClientSize.Height == 330);
                    }
                    finally { stNoVent.Dispose(); }
                }
                StationSettingsForm stVent = null;
                try { stVent = new StationSettingsForm(null, new DeviceConfig { VentValveEnabled = true }, new List<RecipeConfig>(), 1); }
                catch { }
                Check("有阀工位窗可构造", stVent != null);
                if (stVent != null)
                    try
                    {
                        // Visible 在窗体未 Show 时读恒 false，测纯函数 ShouldShowBreakVacuum
                        var showFn = typeof(StationSettingsForm).GetMethod("ShouldShowBreakVacuum",
                            BindingFlags.NonPublic | BindingFlags.Static);
                        Check("破空显隐纯函数（无阀/空/false，有阀true）",
                            showFn != null
                            && Equals(showFn.Invoke(null, new object[] { new DeviceConfig() }), false)
                            && Equals(showFn.Invoke(null, new object[] { null }), false)
                            && Equals(showFn.Invoke(null, new object[] { new DeviceConfig { VentValveEnabled = true } }), true));
                        Check("维度关时有阀窗同样收缩(V1.75)",
                            stVent.ClientSize.Height == 330);
                    }
                    finally { stVent.Dispose(); }
                StationSettingsForm stModeOn = null;
                try { stModeOn = new StationSettingsForm(null,
                    new DeviceConfig { DisplayModeEnabled = true }, new List<RecipeConfig>(), 1); }
                catch { }
                Check("维度开时工位窗可构造", stModeOn != null);
                if (stModeOn != null)
                {
                    try
                    {
                        Check("维度开时工位窗高度不变(V1.75)",
                            stModeOn.ClientSize.Height == 370);
                        var cmbDmOn = typeof(StationSettingsForm).GetField("cmbDisplayMode",
                            BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(stModeOn) as Sunny.UI.UIComboBox;
                        Check("维度开时工位窗下拉项非空(V1.75)",
                            cmbDmOn != null && cmbDmOn.Items.Count >= 8 && cmbDmOn.Items.Contains("白场"));
                    }
                    finally { stModeOn.Dispose(); }
                }
                BatchRecipeForm bTipForm = null;
                try { bTipForm = new BatchRecipeForm(null, new List<RecipeConfig>(), new List<int>()); }
                catch { }
                Check("批量窗可构造(tooltip)", bTipForm != null);
                if (bTipForm != null)
                {
                    try
                    {
                        var tipB = typeof(BatchRecipeForm).GetField("_tip",
                            BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(bTipForm) as ToolTip;
                        var lblB = typeof(BatchRecipeForm).GetField("lblDisplayModeLabel",
                            BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(bTipForm) as Control;
                        Check("批量窗tooltip已挂（含显示模式追溯说明）",
                            tipB != null && lblB != null && tipB.GetToolTip(lblB).Contains("追溯"));
                        var cmbB = typeof(BatchRecipeForm).GetField("cmbDisplayMode",
                            BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(bTipForm) as Sunny.UI.UIComboBox;
                        Check("批量窗显示模式是下拉单选(V1.74)",
                            cmbB != null && cmbB.DropDownStyle == Sunny.UI.UIDropDownStyle.DropDownList);
                        Check("维度关时批量窗下拉空(V1.75)",
                            cmbB != null && cmbB.Items.Count == 0);
                        Check("维度关时批量窗收缩(V1.75: 360→323)",
                            bTipForm.ClientSize.Height == 323);
                    }
                    finally { bTipForm.Dispose(); }
                }
                DeviceManager dmModeOn = null;
                BatchRecipeForm bModeOn = null;
                try
                {
                    dmModeOn = new DeviceManager(
                        new DeviceConfig { DisplayModeEnabled = true });
                    bModeOn = new BatchRecipeForm(dmModeOn, new List<RecipeConfig>(), new List<int>());
                }
                catch { }
                Check("维度开时批量窗可构造", bModeOn != null);
                if (bModeOn != null)
                {
                    try
                    {
                        Check("维度开时批量窗高度不变(V1.75)",
                            bModeOn.ClientSize.Height == 360);
                        var cmbBOn = typeof(BatchRecipeForm).GetField("cmbDisplayMode",
                            BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(bModeOn) as Sunny.UI.UIComboBox;
                        Check("维度开时批量窗下拉项非空(V1.75)",
                            cmbBOn != null && cmbBOn.Items.Count >= 8 && cmbBOn.Items.Contains("视频"));
                    }
                    finally { bModeOn.Dispose(); }
                }
                if (dmModeOn != null) { try { dmModeOn.Dispose(); } catch { } }
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
            finally
            {
                grid.Dispose();
                // 【大扫荡】缓存画笔/画刷随控件销毁释放（Designer.Dispose 补的），
                // 防 GDI 句柄泄漏（以前只在换主题时覆盖释放）。
                // 【复查补齐】以前用 GetProperty("Disposed")——那是事件不是属性，
                // 反射拿 null 再 ??true，恒绿假绿。改走 Control.IsDisposed 公开属性。
                try
                {
                    var tg2 = typeof(WorkstationGridView);
                    string[] brs = { "_brushSetButton", "_brushSelectChecked", "_penBorder", "_brushValueBox", "_brushRowSelect", "_brushSelectUnchecked" };
                    var isDispProp = tg2.GetProperty("IsDisposed",
                        BindingFlags.Instance | BindingFlags.Public);
                    bool isDisp = isDispProp != null
                        && (bool)isDispProp.GetValue(grid, null);
                    Check("网格Dispose后已释放", isDispProp != null && isDisp);
                    // 6 个缓存对象必须"找得到字段 + 读得到内部句柄 + 句柄已归零"，
                    // 三项缺一即假绿：字段名写错/运行时改名会当场变红。
                    // 注意 nativeBrush 在基类 Brush 上（private 不继承），必须沿继承链找。
                    int probed = 0;
                    bool allZero = brs.All(n =>
                    {
                        var f = tg2.GetField(n, BindingFlags.NonPublic | BindingFlags.Instance);
                        if (f == null) return false;
                        var v = f.GetValue(grid);
                        if (v == null) return false;
                        // .NET Framework 的 Pen/Brush 无 IsReleased，反射取内部 GDI 句柄：
                        // 释放后 nativePen/nativeBrush 应为 IntPtr.Zero。
                        System.Reflection.FieldInfo hf = null;
                        for (Type t = v.GetType(); t != null && hf == null; t = t.BaseType)
                        {
                            hf = t.GetField("nativePen", BindingFlags.NonPublic | BindingFlags.Instance)
                                ?? t.GetField("nativeBrush", BindingFlags.NonPublic | BindingFlags.Instance);
                        }
                        if (hf == null) return false;
                        var ptr = hf.GetValue(v) as IntPtr?;
                        bool zero = ptr.HasValue && ptr.Value == IntPtr.Zero;
                        if (zero) probed++;
                        return zero;
                    });
                    Check("网格GDI缓存已随释放(6个句柄归零)", allZero && probed == 6);
                }
                catch { }
            }

            // —— 通讯测试位值→通道号（公开静态） ——
            Check("0x0001→0", CommunicationTestForm.ChannelOf(0x0001) == 0);
            Check("0x0002→1", CommunicationTestForm.ChannelOf(0x0002) == 1);
            Check("0x0100→8", CommunicationTestForm.ChannelOf(0x0100) == 8);
            Check("0x8000→15", CommunicationTestForm.ChannelOf(0x8000) == 15);
            Check("0→0", CommunicationTestForm.ChannelOf(0) == 0);

            // —— 预留点位网格纯函数（V1.80：SpareGrid.ComputePreserveMask/ComputeWriteValue） ——
            var spareMaps = new List<IoOutputChannelRemap>
            {
                new IoOutputChannelRemap { SourceRegister = 0x2000, SourceChannel = 0, TargetRegister = 0x2009, TargetChannel = 3 },
                new IoOutputChannelRemap { SourceRegister = 0x2001, SourceChannel = 5, TargetRegister = 0x2009, TargetChannel = 7 },
                new IoOutputChannelRemap { SourceRegister = 0x2000, SourceChannel = 1, TargetRegister = 0x2008, TargetChannel = 0 },
            };
            Check("保位掩码只收目标落0x2009的位",
                CommunicationTestForm.SpareGrid.ComputePreserveMask(spareMaps, 0x2009) == ((1 << 3) | (1 << 7)));
            Check("他寄存器掩码只收自己的位",
                CommunicationTestForm.SpareGrid.ComputePreserveMask(spareMaps, 0x2008) == (1 << 0));
            Check("null映射表掩码为0",
                CommunicationTestForm.SpareGrid.ComputePreserveMask(null, 0x2009) == 0);
            Check("非法目标通道跳过",
                CommunicationTestForm.SpareGrid.ComputePreserveMask(
                    new List<IoOutputChannelRemap> { new IoOutputChannelRemap { TargetRegister = 0x2009, TargetChannel = 16 } }, 0x2009) == 0);
            Check("RMW保位合并",
                CommunicationTestForm.SpareGrid.ComputeWriteValue(0xFF00, 0x00FF, 0xF000) == 0xF0FF);
            Check("保位0即直写",
                CommunicationTestForm.SpareGrid.ComputeWriteValue(0x1234, 0x00FF, 0) == 0x00FF);
            Check("全保位即现值不变",
                CommunicationTestForm.SpareGrid.ComputeWriteValue(0xABCD, 0x00FF, 0xFFFF) == 0xABCD);
            // 预留点位表与映射器同源（默认 80DI/160DO：DI 73~80→X110~X117，DO 145~160→Y220~Y237）
            var spareMap = IoMapBuilder.Build(new DeviceConfig { TotalBarometers = 72, TotalInputs = 80, TotalOutputs = 160 });
            var spareIn = spareMap.Where(p => p.Function == IoFunction.Unknown && p.Type == IoType.Input).ToList();
            var spareOut = spareMap.Where(p => p.Function == IoFunction.Unknown && p.Type == IoType.Output).ToList();
            Check("预留DI 8路X110起",
                spareIn.Count == 8 && spareIn[0].PhysicalAddress == "X110" && spareIn[7].PhysicalAddress == "X117");
            Check("预留DO 16路Y220起",
                spareOut.Count == 16 && spareOut[0].PhysicalAddress == "Y220" && spareOut[15].PhysicalAddress == "Y237");
            Check("无预留配置两区为空",
                IoMapBuilder.Build(new DeviceConfig { TotalBarometers = 72, TotalInputs = 72, TotalOutputs = 144 })
                    .Count(p => p.Function == IoFunction.Unknown) == 0);

            // —— 右侧宽度比例自适应（V1.65：MainForm.ComputeRightPanelWidth 纯函数） ——
            Check("比例常量0.234/护栏180~340",
                MainForm.RightPanelRatio == 0.234
                && MainForm.RightPanelMinWidth == 180 && MainForm.RightPanelMaxWidth == 340);
            Check("设计宽1394→326(与现状一致)",
                MainForm.ComputeRightPanelWidth(1394, false, 0) == 326);
            Check("1366屏分隔容器1360→318",
                MainForm.ComputeRightPanelWidth(1360, false, 0) == 318);
            Check("新设计宽1274→298",
                MainForm.ComputeRightPanelWidth(1274, false, 0) == 298);
            Check("1080p大屏钳到上限340",
                MainForm.ComputeRightPanelWidth(1914, false, 0) == 340);
            Check("小屏700钳到下限180",
                MainForm.ComputeRightPanelWidth(700, false, 0) == 180);
            Check("宽0/负数按设计宽兜底→328",
                MainForm.ComputeRightPanelWidth(0, false, 0) == 328
                && MainForm.ComputeRightPanelWidth(-5, false, 0) == 328);
            Check("有json自定义值原样优先",
                MainForm.ComputeRightPanelWidth(1394, true, 260) == 260
                && MainForm.ComputeRightPanelWidth(1394, true, 500) == 500);

            // —— 参数设置入口无权限提示（新增：按钮常亮可点，无权限点后弹"权限不够"） ——
            // 以前操作员下按钮 Enabled=false，点了零反馈像卡死；现常亮+点击时拦截。
            // 纯函数 GetParameterDeniedMessage 不弹真框：有权限回空串放行，
            // 无权限含"权限不够"并指明去【用户权限】提权（现场知道下一步干什么）。
            Check("参数入口有权限放行", MainForm.GetParameterDeniedMessage(true) == "");
            string denied = MainForm.GetParameterDeniedMessage(false);
            Check("参数入口无权限提示权限不够", denied.Contains("权限不够"));
            Check("参数入口无权限指明去用户权限", denied.Contains("用户权限"));

            // —— 风机状态中文（反射私有静态；与主窗文案差异已知，锁本窗契约） ——
            var gst = typeof(FanTestForm).GetMethod("GetStateText",
                BindingFlags.NonPublic | BindingFlags.Static);
            Func<FanRunState, string> stx = v => (string)gst.Invoke(null, new object[] { v });
            Check("四态中文(V1.63与主窗对齐)", stx(FanRunState.ProgramStopped) == "程式停止"
                && stx(FanRunState.ProgramRunning) == "程式运行中"
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

            // —— 串口参数钳制（V1.63：主窗加载时调，防手改配置配出"连不上"误导） ——
            Check("数据位5~8放行",
                SerialPortHelper.ClampDataBits(5) == 5 && SerialPortHelper.ClampDataBits(8) == 8);
            Check("数据位9/4/0/负数回退8",
                SerialPortHelper.ClampDataBits(9) == 8 && SerialPortHelper.ClampDataBits(4) == 8
                && SerialPortHelper.ClampDataBits(0) == 8 && SerialPortHelper.ClampDataBits(-1) == 8);
            Check("波特率正数放行",
                SerialPortHelper.ClampBaudRate(19200, 19200) == 19200
                && SerialPortHelper.ClampBaudRate(999999, 19200) == 999999);
            Check("波特率0/负数回退",
                SerialPortHelper.ClampBaudRate(0, 19200) == 19200
                && SerialPortHelper.ClampBaudRate(-1, 115200) == 115200);
            Check("超时正数放行", SerialPortHelper.ClampTimeoutMs(1000, 1000) == 1000);
            Check("超时0/负数回退",
                SerialPortHelper.ClampTimeoutMs(0, 1000) == 1000
                && SerialPortHelper.ClampTimeoutMs(-5, 3000) == 3000);
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

            // —— V1.60.4：布局预览画布底（纯函数，不 new 窗体就能断） ——
            // （注：V1.71 公共参数保存按钮改语义绿，DimGray 特例与 GetSaveButtonThemeColors 已删除；
            // Sunny 按钮语义色保护由上面的通用分支覆盖，不再单列。）
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

            // —— V1.71：SunnyUI 自绘控件换肤分支（UIForm 换肤配套，STA harness 直接 new） ——
            var sBtn = new Sunny.UI.UIButton();
            var sBtnSem = new Sunny.UI.UIButton();
            ThemeManager.ApplyButtonColors(sBtnSem, Color.Crimson, Color.White);
            var sTxt = new Sunny.UI.UITextBox();
            var sCmb = new Sunny.UI.UIComboBox();
            var sLbl = new Sunny.UI.UILabel();
            var sPnl = new Sunny.UI.UIPanel();
            sPnl.Controls.Add(sBtn);
            sPnl.Controls.Add(sBtnSem);
            sPnl.Controls.Add(sTxt);
            sPnl.Controls.Add(sCmb);
            sPnl.Controls.Add(sLbl);
            Color sTxtBack0 = sTxt.BackColor;
            Color sBtnFill0 = sBtn.FillColor;

            ThemeManager.SetMode(AppThemeMode.Dark, false);
            ThemeManager.ApplyTo(sPnl);
            Check("Sunny按钮填充色从未被改动(语义色保护)",
                sBtn.FillColor.ToArgb() == sBtnFill0.ToArgb());
            Check("ApplyButtonColors写语义色(Style=Custom+FillColor)",
                sBtnSem.FillColor.ToArgb() == Color.Crimson.ToArgb()
                && sBtnSem.Style.ToString() == "Custom");
            Check("深色Sunny输入框底变深",
                sTxt.BackColor.ToArgb() == ThemeManager.DarkInputBack.ToArgb()
                || sTxt.FillColor.ToArgb() == ThemeManager.DarkInputBack.ToArgb());
            Check("深色Sunny下拉底变深",
                sCmb.BackColor.ToArgb() == ThemeManager.DarkInputBack.ToArgb()
                || sCmb.FillColor.ToArgb() == ThemeManager.DarkInputBack.ToArgb());
            Check("深色Sunny标签字变浅",
                sLbl.ForeColor.ToArgb() == ThemeManager.DarkText.ToArgb());

            ThemeManager.SetMode(AppThemeMode.Light, false);
            ThemeManager.ApplyTo(sPnl);
            Check("浅色Sunny输入框底还原", sTxt.BackColor.ToArgb() == sTxtBack0.ToArgb());
            Check("浅色语义按钮Fill保留",
                sBtnSem.FillColor.ToArgb() == Color.Crimson.ToArgb());

            ThemeManager.SetMode(AppThemeMode.Light, false); // 收尾复位
            sPnl.Dispose();
        }

        // =====================================================================
        // UiStyleV172_1 —— 主按钮蓝 + 历史日期布局 + 公共参数所见即所得（V1.72.1）
        // 背景：登录窗确认按钮先换蓝（V1.72），剩余 5 个主按钮跟进统一 DodgerBlue；
        // 历史窗日期框重叠/裁剪加宽修复；公共参数设计器 Y 与运行时对齐（只居中 X）。
        // 全部构造真窗体断言（与 HistoryCsv/UiPureHelpers 同套路，不弹模态框）。
        // =====================================================================
        private static void UiStyleV172_1Tests()
        {
            // —— 主按钮蓝判定：DodgerBlue + Rect同色 + 白字 + Custom ——
            Func<Sunny.UI.UIButton, bool> isMainBlue = btn =>
                btn != null
                && btn.FillColor.ToArgb() == Color.DodgerBlue.ToArgb()
                && btn.RectColor.ToArgb() == Color.DodgerBlue.ToArgb()
                && btn.ForeColor.ToArgb() == Color.White.ToArgb()
                && btn.Style.ToString() == "Custom";

            // —— 改密窗（需 UserManager：隔离目录防污染真实 Users.json） ——
            EnterCleanDir();
            var um = new UserManager();
            var pwdForm = new ChangePasswordForm(um);
            try
            {
                var btn = typeof(ChangePasswordForm).GetField("btnOK",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pwdForm) as Sunny.UI.UIButton;
                Check("改密窗确认钮主按钮蓝", isMainBlue(btn));
            }
            finally { pwdForm.Dispose(); }

            // —— 批号窗（无参可构造） ——
            var lotForm = new InputLotForm();
            try
            {
                var btn = typeof(InputLotForm).GetField("btnOK",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(lotForm) as Sunny.UI.UIButton;
                Check("批号窗确定钮主按钮蓝", isMainBlue(btn));
            }
            finally { lotForm.Dispose(); }

            // —— 批量配方窗（dm=null 纯保存模式可构造） ——
            var batchForm = new BatchRecipeForm(null, new List<RecipeConfig>(), new List<int>());
            try
            {
                var btn = typeof(BatchRecipeForm).GetField("btnAddToQueue",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(batchForm) as Sunny.UI.UIButton;
                Check("批量配方加入队列主按钮蓝", isMainBlue(btn));
            }
            finally { batchForm.Dispose(); }

            // —— 公共参数窗（dm=null 可构造；只调 CenterControls 不点保存） ——
            var pmForm = new CommonParameterForm(null);
            try
            {
                var btn = typeof(CommonParameterForm).GetField("btnSave",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pmForm) as Sunny.UI.UIButton;
                Check("公共参数保存钮主按钮蓝", isMainBlue(btn));

                // 设计器 Y 即运行 Y（lbl 65 / nud 62 / btn 110，含 35px 标题区）
                var lbl = typeof(CommonParameterForm).GetField("lblThreshold",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pmForm) as Control;
                var nud = typeof(CommonParameterForm).GetField("nudThreshold",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pmForm) as Control;
                Check("公共参数设计Y含标题区(lbl65/nud62/btn110)",
                    lbl != null && nud != null && btn != null
                    && lbl.Top == 65 && nud.Top == 62 && btn.Top == 110);
                // CenterControls 只居中 X、不动 Y（所见即所得锁：调两次 Y 不变）
                var center = typeof(CommonParameterForm).GetMethod("CenterControls",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Check("反射找到 CenterControls", center != null);
                if (center != null)
                {
                    int y0 = lbl.Top, y1 = nud.Top, y2 = btn.Top;
                    center.Invoke(pmForm, null);
                    Check("CenterControls不动Y(所见即所得)",
                        lbl.Top == y0 && nud.Top == y1 && btn.Top == y2);
                    Check("CenterControls后控件仍在窗内",
                        lbl.Left >= 0 && nud.Left > lbl.Left && btn.Left >= 0
                        && nud.Right <= pmForm.ClientSize.Width && btn.Right <= pmForm.ClientSize.Width);
                    // V1.72.3 无重叠锁：输入框左缘 − 标签右缘（AutoSize 实占）≥ 8px。
                    // 直接锁视觉间距，不与产品 CenterControls 同口径（防循环自证）：
                    // 探针实测 MeasureText=143、AutoSize 实宽=146（delta=3），
                    // 旧口径（纯文本宽+gap 8）视觉间距仅 8−3=5px → 本条红；
                    // 新口径（Max 实宽+gap 10）视觉间距 10px → 本条绿。
                    // 另注意 Designer 残留 Size 107 是旧字体过期值，算居中勿用它。
                    Check("公共参数标签输入框无重叠",
                        nud.Left - lbl.Right >= 8);
                }
            }
            finally { pmForm.Dispose(); }

            // —— ID 绑定窗（lotNumber 即可构造，scanner/dm 均缺省 null） ——
            var idForm = new IdBindingForm("LOT-1");
            try
            {
                var btn = typeof(IdBindingForm).GetField("btnSave",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(idForm) as Sunny.UI.UIButton;
                Check("ID绑定保存钮主按钮蓝", isMainBlue(btn));
            }
            finally { idForm.Dispose(); }

            // —— 历史窗日期布局（加宽 150 + 间隙防重叠） ——
            var hisForm = new HistoryRecordForm();
            try
            {
                var t = typeof(HistoryRecordForm);
                var lblS = t.GetField("lblStart", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hisForm) as Control;
                var dtpS = t.GetField("dtpStart", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hisForm) as Control;
                var lblE = t.GetField("lblEnd", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hisForm) as Control;
                var dtpE = t.GetField("dtpEnd", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hisForm) as Control;
                var btnQ = t.GetField("btnQuery", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hisForm) as Control;
                var btnX = t.GetField("btnExport", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hisForm) as Control;
                Check("历史窗6件全建好",
                    lblS != null && dtpS != null && lblE != null && dtpE != null && btnQ != null && btnX != null);
                if (dtpS != null && dtpE != null)
                {
                    Check("日期框加宽150防裁剪", dtpS.Width >= 150 && dtpE.Width >= 150);
                }
                if (lblS != null && dtpS != null && lblE != null && dtpE != null)
                {
                    // 标签 AutoSize 的 Width 在未显示时未布局，用实测文本宽判定
                    //（与 CommonParameterForm.CenterControls 同口径）
                    Func<Control, int> textW = c =>
                        System.Windows.Forms.TextRenderer.MeasureText(c.Text, c.Font).Width;
                    // 标签右缘与日期框左缘留 ≥3px（原来 80-15=65 紧贴，字体稍大即叠）
                    Check("开始组无重叠",
                        dtpS.Left >= lblS.Left + textW(lblS) + 3);
                    Check("结束组无重叠",
                        dtpE.Left >= lblE.Left + textW(lblE) + 3);
                    Check("两组先后不碰",
                        lblE.Left >= dtpS.Left + dtpS.Width + 3);
                }
                var btnC = t.GetField("btnColumns", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hisForm) as Control;
                var pnlT = t.GetField("panelTop", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hisForm) as Control;
                Check("报表列按钮已建好", btnC != null && btnC.Text == "报表列设置");
                if (btnC != null && btnX != null && pnlT != null)
                {
                    // V1.75：窗加宽 784→880，报表列设置在导出右侧，不叠不出界，五字容得下
                    Check("报表列设置与导出无重叠",
                        btnC.Left >= btnX.Left + btnX.Width + 3);
                    Check("报表列设置不出顶栏右界",
                        btnC.Left + btnC.Width <= pnlT.Width);
                    Check("报表列设置宽容得下五字",
                        btnC.Width >= 110);
                }
            }
            finally { hisForm.Dispose(); }

            // —— 语义保护：深色 ApplyTo 不改主按钮蓝（ThemeManager 只动中性色） ——
            var guard = new Sunny.UI.UIButton();
            ThemeManager.ApplyButtonColors(guard, Color.DodgerBlue, Color.White);
            var pnl = new Panel();
            pnl.Controls.Add(guard);
            ThemeManager.SetMode(AppThemeMode.Dark, false);
            ThemeManager.ApplyTo(pnl);
            Check("深色下主按钮蓝保留", guard.FillColor.ToArgb() == Color.DodgerBlue.ToArgb());
            ThemeManager.SetMode(AppThemeMode.Light, false);
            ThemeManager.ApplyTo(pnl);
            Check("浅色下主按钮蓝保留", guard.FillColor.ToArgb() == Color.DodgerBlue.ToArgb());
            ThemeManager.SetMode(AppThemeMode.Light, false);
            pnl.Dispose();
        }

        // =====================================================================
        // UiFinalizerV172_14 —— 终结器跨线程崩溃 + 判定窗预览 + 关于 SunnyUI（V1.72.14）
        // 背景：关通讯窗开风扇窗时弹"非 UI 线程"错，堆栈终点 ResetAutoComplete←Dispose←Finalize
        // （Sunny UITextBox/原生 TextBox 皆然：有句柄 + 未显式 Dispose + 失根 → 终结器线程碰 Handle 即炸）。
        // 修法是"关窗即拦后台回调"（_closed + 句柄双查 + BeginInvoke + 全程 try）与"非模态自释"，
        // 而非改 Sunny 源码。本模块锁三件事，防后人删 guard 又复发：
        // ①Comm/Fan 有 _closed 且关后日志/切线程不炸；②判定窗无参可预览、标签具名；
        // ③关于弹窗是 SunnyUIForm + 确认蓝。全程只构造不 Show、不点会弹框的按钮（零 MessageBox 阻塞）。
        // =====================================================================
        private static void UiFinalizerV172_14Tests()
        {
            const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Public
                | BindingFlags.Instance | BindingFlags.Static;

            // —— ①Comm/Fan 关窗标记与释放安全（反射找字段 + 构造释放不炸） ——
            Check("通讯窗有_closed关窗标记",
                typeof(CommunicationTestForm).GetField("_closed", Flags) != null);
            Check("风扇窗有_closed关窗标记",
                typeof(FanTestForm).GetField("_closed", Flags) != null);

            var mockCfg = new DeviceConfig { UseMockCommunication = true };
            var mockDm = new DeviceManager(mockCfg);
            try
            {
                var comm = new CommunicationTestForm(mockDm);
                try
                {
                    var closedF = typeof(CommunicationTestForm).GetField("_closed", Flags);
                    Check("通讯窗初建_closed=false", closedF != null && !(bool)closedF.GetValue(comm));
                    bool noThrow = true;
                    try
                    {
                        // 未 Show 无句柄：AppendLog 应直接丢弃而非 Invoke 炸框
                        comm.AppendLog("构造后日志（无句柄应丢弃）");
                    }
                    catch { noThrow = false; }
                    Check("通讯窗无句柄AppendLog不炸", noThrow);
                    // 映射提示窗类型自带 FormClosed 自释（用户先×提示窗也不进终结器）
                    var noticeT = typeof(CommunicationTestForm).GetNestedType("RemapNoticeForm", Flags);
                    Check("反射找到RemapNoticeForm", noticeT != null);
                }
                finally { try { comm.Dispose(); } catch { } }

                var fan = new FanTestForm(mockDm);
                try
                {
                    var closedF = typeof(FanTestForm).GetField("_closed", Flags);
                    Check("风扇窗初建_closed=false", closedF != null && !(bool)closedF.GetValue(fan));
                    // 反射置 _closed=true 模拟"已关窗排队回调"：RunOnUi/AppendLog/SetConnected 全丢弃不炸
                    closedF.SetValue(fan, true);
                    bool quiet = true;
                    try
                    {
                        var runOnUi = typeof(FanTestForm).GetMethod("RunOnUi", BindingFlags.NonPublic | BindingFlags.Instance);
                        runOnUi.Invoke(fan, new object[] { new Action(() => { throw new Exception("不应执行"); }) });
                        var append = typeof(FanTestForm).GetMethod("AppendLog", BindingFlags.NonPublic | BindingFlags.Instance);
                        append.Invoke(fan, new object[] { "关后日志应丢弃" });
                        var setConn = typeof(FanTestForm).GetMethod("SetConnected", BindingFlags.NonPublic | BindingFlags.Instance);
                        setConn.Invoke(fan, new object[] { true });
                    }
                    catch { quiet = false; }
                    Check("风扇窗关后三件套静默丢弃", quiet);
                    closedF.SetValue(fan, false);
                }
                finally { try { fan.Dispose(); } catch { } }
            }
            finally { try { mockDm.Dispose(); } catch { } }

            // —— ②判定窗设计器可预览：无参构造 + 标签具名 + 下拉选项 ——
            Check("判定窗有无参构造（设计器必需）",
                typeof(UnloadJudgeForm).GetConstructor(Type.EmptyTypes) != null);
            var judge = new UnloadJudgeForm();
            try
            {
                var t = typeof(UnloadJudgeForm);
                var lblCode = t.GetField("_lblCode", Flags)?.GetValue(judge) as Control;
                var lblDisp = t.GetField("_lblDisp", Flags)?.GetValue(judge) as Control;
                Check("判定窗静态标签具名（_lblCode/_lblDisp）", lblCode != null && lblDisp != null);
                var cmbObj = t.GetField("_cmbDisposition", Flags)?.GetValue(judge);
                // Sunny UIComboBox 不是原生 ComboBox 子类，按 Items 反射断言选项数
                // （V1.71 血泪：is Button/TextBox 认不出 Sunny 自绘，断言一律走反射/类型名）
                int itemCount = -1;
                try
                {
                    var itemsProp = cmbObj?.GetType().GetProperty("Items");
                    var items = itemsProp?.GetValue(cmbObj, null) as System.Collections.IList;
                    itemCount = items?.Count ?? -1;
                }
                catch { }
                // 兜底：直接数静态数组（构造 AddRange 的源）
                Check("判定窗处置下拉选项数=Dispositions",
                    itemCount == UnloadJudgeForm.Dispositions.Length);
                var scope = t.GetField("_lblScope", Flags)?.GetValue(judge) as Control;
                Check("判定窗无参空快照范围文案", scope != null && scope.Text.Contains("送判 0 台"));
                var btnEx = t.GetField("_btnExecute", Flags)?.GetValue(judge) as Control;
                var btnCl = t.GetField("_btnClose", Flags)?.GetValue(judge) as Control;
                Check("判定窗两按钮全建好", btnEx != null && btnCl != null);
            }
            finally { try { judge.Dispose(); } catch { } }

            // —— ③关于弹窗 SunnyUI 风格（反射调 internal static，不 new 主窗） ——
            var buildMi = typeof(MainForm).GetMethod("BuildVersionInfoDialog", Flags);            Check("反射找到BuildVersionInfoDialog", buildMi != null);
            if (buildMi != null)
            {
                Form dlg = null;
                try { dlg = buildMi.Invoke(null, null) as Form; }
                catch { }
                Check("关于弹窗构造成功", dlg != null);
                if (dlg != null)
                {
                    try
                    {
                        Check("关于弹窗是SunnyUIForm（蓝标题）",
                            dlg.GetType().FullName == "Sunny.UI.UIForm"
                            || dlg.GetType().BaseType?.FullName == "Sunny.UI.UIForm"
                            || dlg is Sunny.UI.UIForm);
                        var txt = dlg.Controls.Find("txtVersionInfo", true).FirstOrDefault()
                            as Sunny.UI.UITextBox;
                        Check("关于弹窗只读多行UITextBox",
                            txt != null && txt.Multiline && txt.ReadOnly);
                        if (txt != null)
                        {
                            Check("关于内容从标题区下起排（Y>=35）", txt.Top >= 35);
                            Check("关于文本含版本号与版权",
                                (txt.Text ?? "").Contains("V1.58.4")
                                && (txt.Text ?? "").Contains("版权所有"));
                        }
                        var btnOk = dlg.Controls.Find("btnOk", true).FirstOrDefault()
                            as Sunny.UI.UIButton;
                        bool blue = btnOk != null
                            && btnOk.FillColor.ToArgb() == Color.DodgerBlue.ToArgb()
                            && btnOk.RectColor.ToArgb() == Color.DodgerBlue.ToArgb()
                            && btnOk.ForeColor.ToArgb() == Color.White.ToArgb()
                            && btnOk.Style.ToString() == "Custom";
                        Check("关于确定钮确认蓝（DodgerBlue白字Custom）", blue);
                        Check("关于弹窗Accept落确定钮", dlg.AcceptButton == btnOk);
                    }
                    finally { try { dlg.Dispose(); } catch { } }
                }
            }

            // —— ④全仓关窗竞态锁（V1.72.15）：有后台线程/定时器/长事件订阅的窗全有 _closed ——
            // 只构造不 Show、不点会弹框的按钮（零 MessageBox 阻塞）；关后调用静默丢弃。
            Check("公共参数窗有_closed关窗标记",
                typeof(CommonParameterForm).GetField("_closed", Flags) != null);
            Check("ID绑定窗有_closed关窗标记",
                typeof(IdBindingForm).GetField("_closed", Flags) != null);
            Check("设置窗有_closed关窗标记",
                typeof(SettingsForm).GetField("_closed", Flags) != null);
            Check("主窗有_mainClosing退出标记",
                typeof(MainForm).GetField("_mainClosing", Flags) != null);

            // 公共参数窗：反射调 OnFormClosed 置位（构造不弹框，投递点靠审计 R5 锁）。
            var pmForm2 = new CommonParameterForm(null);
            try
            {
                var onClosed = typeof(CommonParameterForm).GetMethod("OnFormClosed",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Check("反射找到公共参数OnFormClosed", onClosed != null);
                if (onClosed != null)
                {
                    onClosed.Invoke(pmForm2, new object[] { new FormClosedEventArgs(CloseReason.None) });
                    var closedF = typeof(CommonParameterForm).GetField("_closed", Flags);
                    Check("公共参数关后_closed=true", closedF != null && (bool)closedF.GetValue(pmForm2));
                    // 关后完成回调静默丢弃（会碰 btnSave/MessageBox，未守卫即炸）。
                    bool quiet = true;
                    try
                    {
                        var done = typeof(CommonParameterForm).GetMethod("OnBatchWriteCompleted",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        done.Invoke(pmForm2, new object[] { -5m, new Dictionary<int, bool>() });
                    }
                    catch { quiet = false; }
                    Check("公共参数关后完成回调静默丢弃", quiet);
                }
            }
            finally { try { pmForm2.Dispose(); } catch { } }

            // ID 绑定窗：释放后扫码回调静默丢弃（Post 排队拦不住退订，靠入口 _closed）。
            var idForm2 = new IdBindingForm("LOT-1");
            try
            {
                try { idForm2.Dispose(); } catch { }
                bool quiet = true;
                try
                {
                    var scan = typeof(IdBindingForm).GetMethod("Scanner_OnBarcodeScanned",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    scan.Invoke(idForm2, new object[] { null, "01" });
                }
                catch { quiet = false; }
                Check("ID绑定释放后扫码回调静默丢弃", quiet);
            }
            finally { try { idForm2.Dispose(); } catch { } }

            // 通讯窗：关后写寄存器停手（在途遍历拍收尾调用即丢弃，不动设备）。
            {
                var cfg2 = new DeviceConfig { UseMockCommunication = true };
                var dm2 = new DeviceManager(cfg2);
                try
                {
                    var comm2 = new CommunicationTestForm(dm2);
                    try
                    {
                        var closedF = typeof(CommunicationTestForm).GetField("_closed", Flags);
                        closedF.SetValue(comm2, true);
                        bool quiet = true;
                        try
                        {
                            var gridF = typeof(CommunicationTestForm).GetField("_vacuumGrid",
                                BindingFlags.NonPublic | BindingFlags.Instance);
                            object grid = gridF.GetValue(comm2);
                            comm2.WriteRegister(grid as CommunicationTestForm.ChannelGrid, 0, 0);
                        }
                        catch { quiet = false; }
                        Check("通讯窗关后写寄存器停手", quiet);
                        closedF.SetValue(comm2, false);
                    }
                    finally { try { comm2.Dispose(); } catch { } }

                    // 风扇窗：关后控制命令停手（未连接分支本会弹 MessageBox，关后应先退）。
                    var fan2 = new FanTestForm(dm2);
                    try
                    {
                        var closedF = typeof(FanTestForm).GetField("_closed", Flags);
                        closedF.SetValue(fan2, true);
                        bool quiet = true;
                        try
                        {
                            var run = typeof(FanTestForm).GetMethod("RunCommand",
                                BindingFlags.NonPublic | BindingFlags.Instance);
                            run.Invoke(fan2, new object[] { "定值启动", new Func<bool>(() => true) });
                        }
                        catch { quiet = false; }
                        Check("风扇窗关后控制命令停手（不弹框）", quiet);
                        closedF.SetValue(fan2, false);
                    }
                    finally { try { fan2.Dispose(); } catch { } }
                }
                finally { try { dm2.Dispose(); } catch { } }
            }

            // 自动补全提供者：释放排空延迟定时器（internal 类一律反射，不直引）。
            try
            {
                var provType = typeof(DeviceManager).Assembly
                    .GetType("AgingTestSystem.Services.RecipeAutoCompleteProvider");
                Check("反射找到RecipeAutoCompleteProvider", provType != null);
                if (provType != null)
                {
                    var box = new Sunny.UI.UITextBox();
                    object prov = null;
                    try
                    {
                        prov = Activator.CreateInstance(provType,
                            new object[] { box, new List<RecipeConfig>(), new Action<RecipeConfig>(r => { }) });
                    }
                    catch (Exception ex)
                    {
                        Check("补全提供者构造（" + ex.GetType().Name + "）", false);
                    }
                    if (prov != null)
                    {
                        try
                        {
                            var pendF = provType.GetField("_pendingHideTimers", Flags);
                            var pend = pendF?.GetValue(prov) as System.Collections.IList;
                            Check("补全提供者有延迟定时器登记表", pend != null);
                            bool quiet = true;
                            try { provType.GetMethod("Dispose").Invoke(prov, null); }
                            catch { quiet = false; }
                            Check("补全提供者释放不炸", quiet && pend.Count == 0);
                            // 释放后消息过滤静默（Post 关窗期点击不碰释放后列表）。
                            bool filterQuiet = true;
                            try
                            {
                                var msg = new Message();
                                object[] args = new object[] { msg };
                                provType.GetMethod("PreFilterMessage").Invoke(prov, args);
                            }
                            catch { filterQuiet = false; }
                            Check("补全提供者释放后过滤静默", filterQuiet);
                        }
                        finally { try { box.Dispose(); } catch { } }
                    }
                    else
                    {
                        try { box.Dispose(); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Check("补全提供者构造释放链路（" + ex.GetType().Name + "）", false);
            }

            // ── 项目切换窗tooltip+在测禁用（V1.73：_lblNote删了转tooltip，超40字换行） ──
            {
                ProjectSwitchForm ps0 = null;
                try { ps0 = new ProjectSwitchForm(() => 0); }
                catch { }
                Check("切换窗可构造(0在测)", ps0 != null);
                if (ps0 != null)
                {
                    try
                    {
                        var btnSw = typeof(ProjectSwitchForm).GetField("_btnSwitch",
                            BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(ps0) as Control;
                        var tipS = typeof(ProjectSwitchForm).GetField("_tip",
                            BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(ps0) as ToolTip;
                        Check("0在测切换可用+提示即时生效",
                            btnSw != null && btnSw.Enabled
                            && tipS != null && tipS.GetToolTip(btnSw).Contains("即时生效"));
                    }
                    finally { try { ps0.Dispose(); } catch { } }
                }
                ProjectSwitchForm ps2 = null;
                try { ps2 = new ProjectSwitchForm(() => 2); }
                catch { }
                Check("切换窗可构造(2在测)", ps2 != null);
                if (ps2 != null)
                {
                    try
                    {
                        var btnSw2 = typeof(ProjectSwitchForm).GetField("_btnSwitch",
                            BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(ps2) as Control;
                        var tipS2 = typeof(ProjectSwitchForm).GetField("_tip",
                            BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(ps2) as ToolTip;
                        Check("2在测切换禁用+提示原因",
                            btnSw2 != null && !btnSw2.Enabled
                            && tipS2 != null && tipS2.GetToolTip(btnSw2).Contains("在测"));
                    }
                    finally { try { ps2.Dispose(); } catch { } }
                }
            }
        }

        // =====================================================================
        // LegacyRecipeGuard —— V1.59 老配方 0 值语义锁（V1.72.2）
        // 背景：V1.59 起 RecipeConfig.NegativePressure 字段就存在，但三录入窗无此输入框，
        // 新建配方该字段恒为 decimal 缺省 0（死数据）；批量窗/工位窗下发时原样传入 0，
        // SetStationRecipe 里 0 = 有值（只认 null = 保持/回退全局），于是阈值定格 0，
        // 在负压域里≈永远到位，真空保护形同虚设。V1.66 只是让它可见（框里显示 0）+
        // 新建默认填全局，不敢设"0=全局"魔法回退（存什么下什么，所见即所得）。
        // 本模块把"0≠全局、null=回退、老文件读出 0、新建默认全局"四条语义锁死：
        // 以后谁动下发语义必须先过这里；现场老 Recipes.json 的 0 值须人工复核工艺值。
        // 全程只构造对象不断言弹窗不启采集（DeviceManager 不 Start，无线程无时序）。
        // =====================================================================
        private static void LegacyRecipeGuardTests()
        {
            // —— 老文件（V1.59 时代：无 NegativePressure/DisplayMode 字段）能读 ——
            EnterCleanDir();
            string legacyJson = "[{\"Id\":1,\"Name\":\"老配方A\",\"DelayTime\":\"00:01:30\","
                + "\"StartTime\":\"08:30:00\",\"LimitTemperature\":75.5,"
                + "\"CreateTime\":\"2026-08-25T10:00:00\",\"IsEnabled\":true}]";
            File.WriteAllText(ProjectProfile.ResolveDataPath("Recipes.json", true), legacyJson);
            var legacy = RecipeStorage.Load();
            Check("老配方文件加载成功", legacy != null && legacy.Count == 1);
            if (legacy == null || legacy.Count != 1) return;
            Check("缺字段NegativePressure反序列化=0(死数据)",
                legacy[0].NegativePressure == 0m);
            Check("缺字段DisplayMode反序列化=null", legacy[0].DisplayMode == null);

            // —— 下发语义：0 = 有值（定格 0），null = 保持（新工位=回退全局） ——
            var dm = new DeviceManager(new DeviceConfig());
            try
            {
                dm.SetStationRecipe(1, "老配方A", 0m, null);
                var info0 = dm.GetStationInfo(1);
                Check("下发0则工位负压=0(0≠全局)",
                    info0 != null && info0.RecipeNegativePressure == 0m);

                dm.SetStationRecipeName(2, "只改名");
                var infoNull = dm.GetStationInfo(2);
                Check("只改名不碰负压(null保持,新工位=null走全局兜底)",
                    infoNull != null && infoNull.RecipeName == "只改名"
                    && infoNull.RecipeNegativePressure == null);

                dm.SetStationRecipe(1, "老配方A", null, null);
                var infoKeep = dm.GetStationInfo(1);
                Check("传null保持已有值(0不被洗掉)",
                    infoKeep != null && infoKeep.RecipeNegativePressure == 0m);

                dm.SetStationRecipe(1, "", null, null);
                var infoClear = dm.GetStationInfo(1);
                Check("清空配方名同步清负压(回全局,防残留旧工艺)",
                    infoClear != null && infoClear.RecipeNegativePressure == null);
            }
            finally { try { dm.Dispose(); } catch { } }

            // —— 录入窗：新建默认=全局（V1.66 修复锁），老配方回填显示 0（可见不悄悄） ——
            var batchForm = new BatchRecipeForm(null, new List<RecipeConfig>(), new List<int>());
            try
            {
                var txtNeg = typeof(BatchRecipeForm).GetField("txtNegativePressure",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(batchForm) as Sunny.UI.UITextBox;
                Check("批量窗新建负压框默认=全局阈值",
                    txtNeg != null && txtNeg.Text == new DeviceConfig().AlarmPressureThresholdKPa.ToString("0.#"));

                var onSel = typeof(BatchRecipeForm).GetMethod("OnRecipeSelected",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Check("反射找到 OnRecipeSelected", onSel != null);
                if (onSel != null)
                {
                    onSel.Invoke(batchForm, new object[] { legacy[0] });
                    Check("老配方回填框显示0(操作员看得见,须人工复核)",
                        txtNeg != null && txtNeg.Text == "0");
                }
            }
            finally { batchForm.Dispose(); }
        }

        // =====================================================================
        // DesignerStabilityV172_16 —— 快照释放 + 设计器可序列化 + 量程字面值（V1.72.16）
        // 背景（用户一次报四个，全是"释放路径看着对、但差一层"）：
        // ①工艺策略窗拖业务框照样终结器跨线程炸：V1.72.12 的 foreach 直接枚举
        //   Controls 逐个 Dispose 是错的——Dispose 会把自己从父集合摘除，枚举器下标
        //   错位跳过一个，被跳过的 Clear 后变孤儿，GC 时终结器线程炸（炸在拖的时候，
        //   漏在早先切节点的时候，极具迷惑性）。修法 = ControlDisposeHelper 快照后释放。
        // ②批量窗/主页布局窗双击预览即脏：手写 Designer 与 VS 序列化口径不一致
        //   （AutoScale.Font+Zoom混搭、AI 估的坐标与真实布局差几像素），打开即标脏；
        //   以 VS 重写版为 canonical 基线，不再手改回去。
        // ③主页布局窗拖预览边缘 ArgumentOutOfRange（"340 对 Value 无效"）：
        //   VS 重写时删掉了元组表达式写的 Minimum/Maximum（序列化器认不出），
        //   输入框变回 0~100。修法 = 量程写字面值 + 赋值前 ClampNud。
        // ④判定窗预览还是坏：Designer 里 Items.AddRange(Dispositions) 引静态字段
        //   （CodeDom 在实例上找不到静态成员，加载失败）+ AutoScale.Font+Zoom 混搭；
        //   选项改构造填，模式改 None。全程只构造不 Show（零 MessageBox 阻塞）。
        // =====================================================================
        private static void DesignerStabilityV172_16Tests()
        {
            const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Public
                | BindingFlags.Instance | BindingFlags.Static;

            // —— ①快照释放：helper 全释放无孤儿；旧 foreach 写法复现跳过 ——
            var panel = new Panel();
            var sunnyMulti = new Sunny.UI.UITextBox { Multiline = true, ShowScrollBar = true };
            var sunnyCmb = new Sunny.UI.UIComboBox();
            var lbl = new Sunny.UI.UILabel { Text = "名" };
            panel.Controls.AddRange(new Control[] { sunnyMulti, sunnyCmb, lbl });
            ControlDisposeHelper.DisposeAllAndClear(panel.Controls);
            Check("快照释放后集合空",
                panel.Controls.Count == 0);
            Check("快照释放三个孩子全 IsDisposed",
                sunnyMulti.IsDisposed && sunnyCmb.IsDisposed && lbl.IsDisposed);
            bool nullOk = true;
            try { ControlDisposeHelper.DisposeAllAndClear(null); }
            catch { nullOk = false; }
            Check("快照释放 null 不炸", nullOk);
            try { panel.Dispose(); } catch { }

            // 旧写法复现（红证据）：同样 3 个孩子，foreach 直释必然抛"集合已修改"
            // 或漏释放——这就是 V1.72.12 没修住的原因。框架行为若变，此条会红，强制重审。
            var oldPanel = new Panel();
            var oa = new TextBox(); var ob = new TextBox(); var oc = new TextBox();
            oldPanel.Controls.AddRange(new Control[] { oa, ob, oc });
            bool oldThrew = false;
            try
            {
                foreach (Control c in oldPanel.Controls) { c.Dispose(); }
                oldPanel.Controls.Clear();
            }
            catch { oldThrew = true; }
            int oldLeft = 0;
            foreach (Control c in new Control[] { oa, ob, oc }) { if (!c.IsDisposed) oldLeft++; }
            Check("旧foreach直释复现抛异常或漏释放（根因证据）",
                oldThrew || oldLeft > 0);
            ControlDisposeHelper.DisposeAllAndClear(oldPanel.Controls);
            try { oldPanel.Dispose(); } catch { }

            // 真实调用点：工艺策略窗 DisposeEditorControls（反射）释放右栏编辑器无孤儿。
            var policyForm = new ProcessPolicyForm();
            try
            {
                var pnlF = typeof(ProcessPolicyForm).GetField("_pnlEditors", Flags);
                var editorsPanel = pnlF?.GetValue(policyForm) as Panel;
                Check("反射找到工艺策略窗_pnlEditors", editorsPanel != null);
                if (editorsPanel != null)
                {
                    var e1 = new Sunny.UI.UITextBox { Multiline = true, ShowScrollBar = true };
                    var e2 = new Sunny.UI.UIComboBox();
                    var e3 = new Sunny.UI.UILabel { Text = "规则" };
                    editorsPanel.Controls.AddRange(new Control[] { e1, e2, e3 });
                    bool quiet = true;
                    try
                    {
                        typeof(ProcessPolicyForm).GetMethod("DisposeEditorControls", Flags)
                            ?.Invoke(policyForm, null);
                    }
                    catch { quiet = false; }
                    Check("工艺策略窗DisposeEditorControls不炸且清光",
                        quiet && editorsPanel.Controls.Count == 0
                        && e1.IsDisposed && e2.IsDisposed && e3.IsDisposed);
                }
            }
            finally { try { policyForm.Dispose(); } catch { } }

            // —— ②③主页布局窗：量程字面值 + 越界赋值不炸 ——
            var layout = new HomeLayoutConfig();
            var homeForm = new HomeLayoutEditorForm(layout);
            try
            {
                var t = typeof(HomeLayoutEditorForm);
                var nudTop = t.GetField("_nudTop", Flags)?.GetValue(homeForm) as NumericUpDown;
                var nudMenu = t.GetField("_nudMenu", Flags)?.GetValue(homeForm) as NumericUpDown;
                var nudRight = t.GetField("_nudRight", Flags)?.GetValue(homeForm) as NumericUpDown;
                var nudStatus = t.GetField("_nudStatus", Flags)?.GetValue(homeForm) as NumericUpDown;
                Check("布局窗四个输入框全建好",
                    nudTop != null && nudMenu != null && nudRight != null && nudStatus != null);
                if (nudTop != null && nudMenu != null && nudRight != null && nudStatus != null)
                {
                    Check("顶部量程=Range字面值(15~80)",
                        nudTop.Minimum == HomeLayoutConfig.TopBarRange.Min
                        && nudTop.Maximum == HomeLayoutConfig.TopBarRange.Max);
                    Check("菜单量程=Range字面值(25~100)",
                        nudMenu.Minimum == HomeLayoutConfig.MenuRange.Min
                        && nudMenu.Maximum == HomeLayoutConfig.MenuRange.Max);
                    Check("右侧量程=Range字面值(180~600)",
                        nudRight.Minimum == HomeLayoutConfig.RightPanelRange.Min
                        && nudRight.Maximum == HomeLayoutConfig.RightPanelRange.Max);
                    Check("状态栏量程=Range字面值(15~60)",
                        nudStatus.Minimum == HomeLayoutConfig.StatusBarRange.Min
                        && nudStatus.Maximum == HomeLayoutConfig.StatusBarRange.Max);

                    // 现场原案：右侧 340（旧 0~100 会炸，现在范围内）拖动同步不抛。
                    layout.RightPanelWidth = 340;
                    bool dragOk = true;
                    try
                    {
                        t.GetMethod("Preview_LayoutChanged", Flags)
                            ?.Invoke(homeForm, new object[] { homeForm, EventArgs.Empty });
                    }
                    catch { dragOk = false; }
                    Check("拖动同步340不抛且框值=340",
                        dragOk && nudRight.Value == 340m);

                    // 旧文件越界值（如右侧 100，小于下限 180）同步时钳住不抛。
                    layout.RightPanelWidth = 100;
                    bool clampOk = true;
                    try
                    {
                        t.GetMethod("Preview_LayoutChanged", Flags)
                            ?.Invoke(homeForm, new object[] { homeForm, EventArgs.Empty });
                    }
                    catch { clampOk = false; }
                    Check("越界旧值100同步钳到下限180不抛",
                        clampOk && nudRight.Value == nudRight.Minimum);
                }
                Check("布局窗AutoScale=None（Sunny canonical，预览不脏）",
                    homeForm.AutoScaleMode == AutoScaleMode.None);
            }
            finally { try { homeForm.Dispose(); } catch { } }

            // 构造期越界初值也不炸（ClampNud 第一道闸）。
            var legacyLayout = new HomeLayoutConfig { RightPanelWidth = 50 };
            bool ctorOk = true;
            HomeLayoutEditorForm legacyForm = null;
            try { legacyForm = new HomeLayoutEditorForm(legacyLayout); }
            catch { ctorOk = false; }
            Check("构造期越界初值50不炸", ctorOk && legacyForm != null);
            if (legacyForm != null) { try { legacyForm.Dispose(); } catch { } }

            // —— ④判定窗 AutoScale.None（与批量窗同锁，防 Font+Zoom 混搭回潮） ——
            var judge2 = new UnloadJudgeForm();
            try
            {
                Check("判定窗AutoScale=None（Sunny canonical）",
                    judge2.AutoScaleMode == AutoScaleMode.None);
            }
            finally { try { judge2.Dispose(); } catch { } }
            var batchForm2 = new BatchRecipeForm(null, new List<RecipeConfig>(), new List<int>());
            try
            {
                Check("批量窗AutoScale=None（VS canonical，预览正常）",
                    batchForm2.AutoScaleMode == AutoScaleMode.None);
            }
            finally { try { batchForm2.Dispose(); } catch { } }
        }

        // =====================================================================
        // PowerReportV174 —— V1.74 电流骨架 + 报表列可配 + 显示模式字典
        // （Q2/Q8/Q20：纯函数与缺省锁全进回归；真电表/PG/报表样张等现场项不在此列）
        // =====================================================================
        private static void PowerReportV174Tests()
        {
            // —— 缺省锁：不配=和以前一模一样 ——
            var dc = new DeviceConfig();
            Check("缺省不用电表", dc.UsePowerMeter == false);
            Check("缺省显示维度关闭", dc.DisplayModeEnabled == false);
            Check("维度开关真值显示", DisplayModeOptions.ShouldShowDisplayMode(
                new DeviceConfig { DisplayModeEnabled = true }));
            Check("维度开关假值隐藏",
                !DisplayModeOptions.ShouldShowDisplayMode(new DeviceConfig())
                && !DisplayModeOptions.ShouldShowDisplayMode(null));
            Check("缺省报表列空=预设", dc.ReportColumns == "");
            Check("缺省显示字典空=预设", dc.DisplayModes == "");
            Check("电流缺省NaN", float.IsNaN(new BarometerData().LoadCurrentA));
            var bd = new BarometerData { DeviceId = 1, LoadCurrentA = 0.42f };
            var bc = bd.Clone();
            bc.LoadCurrentA = 0f;
            Check("Clone带电流且独立", bc.LoadCurrentA == 0f && bd.LoadCurrentA == 0.42f);
            Check("策略名单含报表与字典(跟项目)",
                ProjectPolicyStore.PolicyKeys.Contains("ReportColumns")
                && ProjectPolicyStore.PolicyKeys.Contains("DisplayModes"));

            // —— V1.77 电流行直显开关（构造即默认关；置位只重解布局+重算画布，不抛） ——
            AgingTestSystem.Views.WorkstationGridView gv = null;
            string gvErr = null;
            try { gv = new AgingTestSystem.Views.WorkstationGridView(); }
            catch (Exception ex) { gvErr = ex.GetType().Name + ":" + ex.Message; }
            Check("网格可构造", gv != null, gvErr);
            if (gv != null)
            {
                try
                {
                    Check("电流行缺省关闭", gv.ShowCurrentRow == false);
                    gv.ShowCurrentRow = true;
                    Check("电流行开关置位", gv.ShowCurrentRow == true);
                    gv.ShowCurrentRow = false;
                    Check("电流行开关可关回", gv.ShowCurrentRow == false);
                }
                finally { try { gv.Dispose(); } catch { } }
            }

            // —— Mock 电表：有数、范围、未连接返回null ——
            var mock = new MockPowerMeter();
            Check("Mock初态未连接", mock.IsConnected == false);
            Check("Mock未连接读null", mock.ReadAllCurrents(72) == null);
            Check("Mock连接成功", mock.Connect(new DeviceConfig()) && mock.IsConnected);
            float[] currents = mock.ReadAllCurrents(72);
            bool mockOk = currents != null && currents.Length == 72;
            if (mockOk)
            {
                foreach (float c in currents)
                {
                    if (float.IsNaN(c) || c < 0.05f || c > 0.60f) { mockOk = false; break; }
                }
            }
            Check("Mock72路有数且0.05~0.60A", mockOk);
            Check("Mock重连幂等", mock.ReconnectNow() && mock.IsConnected);
            mock.Disconnect();
            Check("Mock断开后未连接", mock.IsConnected == false);
            try { mock.Dispose(); } catch { }
            Check("Mock释放不抛", true);

            // —— 真实桩：连不上、读全NaN但不断追溯 ——
            var stub = new PowerMeterClient();
            bool stubErr = false;
            stub.OnError += (s, m) => { stubErr = true; };
            Check("桩连接失败", stub.Connect(new DeviceConfig()) == false && stub.IsConnected == false);
            Check("桩失败发一次错误事件", stubErr);
            float[] stubCur = stub.ReadAllCurrents(72);
            bool stubNaN = stubCur != null && stubCur.Length == 72;
            if (stubNaN)
            {
                foreach (float c in stubCur)
                {
                    if (!float.IsNaN(c)) { stubNaN = false; break; }
                }
            }
            Check("桩读数全NaN不断追溯", stubNaN);
            Check("桩重连仍失败", stub.ReconnectNow() == false);
            try { stub.Dispose(); stub.Disconnect(); } catch { }
            Check("桩释放断开不抛", true);

            // —— 编排接线（不 Start：只验"开关管住创建"，采集时序由集成模块覆盖） ——
            var dmOff = new DeviceManager(new DeviceConfig());
            try
            {
                Check("开关关时对外不可见", dmOff.IsPowerMeterConnected == false);
                var fOff = typeof(DeviceManager).GetField("_powerMeter",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Check("反射找到_powerMeter", fOff != null);
                if (fOff != null) Check("开关关不建表", fOff.GetValue(dmOff) == null);
            }
            finally { try { dmOff.Dispose(); } catch { } }
            var cfgOn = new DeviceConfig { UsePowerMeter = true };
            var dmOn = new DeviceManager(cfgOn);
            try
            {
                var fOn = typeof(DeviceManager).GetField("_powerMeter",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                object pm = fOn != null ? fOn.GetValue(dmOn) : null;
                Check("开关开建Mock表", pm is MockPowerMeter);
                Check("未Start前仍显示未连接", dmOn.IsPowerMeterConnected == false);
            }
            finally { try { dmOn.Dispose(); } catch { } }

            // —— 规则变量 current：解析+求值+NaN恒false ——
            bool hasCurrent = false;
            foreach (string v in RuleExpr.Vocabulary)
            {
                if (string.Equals(v, "current", StringComparison.OrdinalIgnoreCase)) { hasCurrent = true; break; }
            }
            Check("词汇表含current", hasCurrent && RuleExpr.Vocabulary.Length == 13);
            RuleExpr.RuleExpression exprCur;
            string exprCurErr;
            Check("current表达式解析过",
                RuleExpr.TryParse("current > 0.5", out exprCur, out exprCurErr), exprCurErr);
            if (exprCur != null)
            {
                var vars = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { { "current", 0.8 } };
                double val;
                string evalErr;
                Check("current=0.8求值过",
                    RuleExpr.TryEval(exprCur, vars, out val, out evalErr) && val != 0, evalErr);
                vars["current"] = double.NaN;
                string berr;
                Check("current=NaN恒false",
                    RuleExpr.TryEvalBool(exprCur, vars, out berr) == false);
            }

            // —— 报表列：预设11列/解析/兜底/校验 ——
            List<ReportColumns.Column> presetCols;
            List<string> presetErrs;
            ReportColumns.Parse(ReportColumns.DefaultPreset, out presetCols, out presetErrs);
            Check("预设解析11列零错", presetCols.Count == 11 && presetErrs.Count == 0);
            Check("预设首列时间/身份事件列序/末列电流",
                presetCols[0].Display == "时间" && presetCols[0].Field == "time"
                && presetCols[2].Display == "SN" && presetCols[2].Field == "sn"
                && presetCols[3].Display == "配方" && presetCols[3].Field == "recipe"
                && presetCols[6].Display == "结果" && presetCols[6].Field == "result"
                && presetCols[10].Display == "电流(A)" && presetCols[10].Field == "current");
            Check("空配置走预设", ReportColumns.Resolve("").Count == 11);
            Check("全错配置走预设", ReportColumns.Resolve("xxx=yyy").Count == 11);
            List<ReportColumns.Column> customCols;
            List<string> customErrs;
            ReportColumns.Parse("时间=time;批号=lot", out customCols, out customErrs);
            Check("自定义2列保序", customCols.Count == 2 && customErrs.Count == 0
                && customCols[1].Display == "批号" && customCols[1].Field == "lot");
            List<ReportColumns.Column> badCols;
            List<string> badErrs;
            ReportColumns.Parse("时间=nosuchfield", out badCols, out badErrs);
            Check("未知字段报错且丢弃", badCols.Count == 0 && badErrs.Count > 0);
            // 【复查补齐】列数上限（到64即停，不先吃满内存）
            var manyCols = new System.Text.StringBuilder();
            for (int ci = 0; ci < 200; ci++) { if (ci > 0) manyCols.Append(';'); manyCols.Append("列" + ci + "=time"); }
            List<ReportColumns.Column> capCols;
            List<string> capErrs;
            ReportColumns.Parse(manyCols.ToString(), out capCols, out capErrs);
            Check("200列截断到64且报错",
                capCols.Count == ReportColumns.MaxColumns && capErrs.Count > 0);
            const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Static;
            var miValidate = typeof(SettingsForm).GetMethod("ValidateValue", Flags);
            if (miValidate != null)
            {
                Check("报表列空合法",
                    (bool)miValidate.Invoke(null, new object[] { "ReportColumns", "", null }) == true);
                Check("报表列合法过",
                    (bool)miValidate.Invoke(null, new object[] { "ReportColumns", "时间=time", null }) == true);
                Check("报表列脏拦截",
                    (bool)miValidate.Invoke(null, new object[] { "ReportColumns", "时间=nosuch", null }) == false);
            }
            else Check("反射找到 ValidateValue", false);

            // —— 报表列弹窗直测（构造即填行；Confirm 私有，反射调，happy path 无弹窗） ——
            var popEmpty = new Controls.ReportColumnsEditorPopup("");
            try
            {
                var dgvF = typeof(Controls.ReportColumnsEditorPopup).GetField("_dgv",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var dgv = dgvF != null ? dgvF.GetValue(popEmpty) as DataGridView : null;
                Check("空配置弹窗显示预设11行", dgv != null && dgv.Rows.Count == 11);
                var miConfirm = typeof(Controls.ReportColumnsEditorPopup).GetMethod("Confirm",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Check("反射找到 Confirm", miConfirm != null);
                if (miConfirm != null && dgv != null)
                {
                    miConfirm.Invoke(popEmpty, null);
                    Check("预设行确定回写预设串",
                        Equals(popEmpty.ResultValue, ReportColumns.DefaultPreset));
                }
            }
            finally { try { popEmpty.Dispose(); } catch { } }
            var popCustom = new Controls.ReportColumnsEditorPopup("批号=lot;时间=time");
            try
            {
                var dgvF2 = typeof(Controls.ReportColumnsEditorPopup).GetField("_dgv",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var dgv2 = dgvF2 != null ? dgvF2.GetValue(popCustom) as DataGridView : null;
                Check("自定义2行保序（批号首行）",
                    dgv2 != null && dgv2.Rows.Count == 2
                    && Equals(dgv2.Rows[0].Cells["colDisplay"].Value, "批号")
                    && Equals(dgv2.Rows[0].Cells["colField"].Value, "lot"));
            }
            finally { try { popCustom.Dispose(); } catch { } }
            // —— 显示字典弹窗直测（空=预设行；Confirm 回写；happy path 无弹窗） ——
            var popModesEmpty = new Controls.DisplayModesEditorPopup("");
            try
            {
                var dgvFM = typeof(Controls.DisplayModesEditorPopup).GetField("_dgv",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var dgvM = dgvFM != null ? dgvFM.GetValue(popModesEmpty) as DataGridView : null;
                Check("空字典弹窗显示预设8行", dgvM != null && dgvM.Rows.Count == 8);
                var miConfirmM = typeof(Controls.DisplayModesEditorPopup).GetMethod("Confirm",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Check("反射找到字典Confirm", miConfirmM != null);
                if (miConfirmM != null && dgvM != null)
                {
                    miConfirmM.Invoke(popModesEmpty, null);
                    Check("预设行确定回写预设串",
                        Equals(popModesEmpty.ResultValue, DisplayModeOptions.DefaultPreset));
                }
            }
            finally { try { popModesEmpty.Dispose(); } catch { } }
            var popModesCustom = new Controls.DisplayModesEditorPopup("红场,绿场");
            try
            {
                var dgvFM2 = typeof(Controls.DisplayModesEditorPopup).GetField("_dgv",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var dgvM2 = dgvFM2 != null ? dgvFM2.GetValue(popModesCustom) as DataGridView : null;
                Check("自定义2行保序（红场首行）",
                    dgvM2 != null && dgvM2.Rows.Count == 2
                    && Equals(dgvM2.Rows[0].Cells[0].Value, "红场")
                    && Equals(dgvM2.Rows[1].Cells[0].Value, "绿场"));
            }
            finally { try { popModesCustom.Dispose(); } catch { } }

            // —— 显示模式字典：预设/校验/规范写法/兜底 ——
            Check("缺省预设8项含白场与视频",
                DisplayModeOptions.Preset().Count == 8
                && DisplayModeOptions.Preset().Contains("白场")
                && DisplayModeOptions.Preset().Contains("视频"));
            List<string> dmOpts;
            List<string> dmErrs;
            DisplayModeOptions.Parse("", out dmOpts, out dmErrs);
            Check("空字典解析零项零错", dmOpts.Count == 0 && dmErrs.Count == 0);
            DisplayModeOptions.Parse("白场,红场,白场", out dmOpts, out dmErrs);
            Check("重复提醒且保留首个", dmOpts.Count == 2 && dmErrs.Count > 0);
            // 【复查补齐】超限即停（到上限break，不先吃满内存；以前全量进表再截断）
            var manyModes = new System.Text.StringBuilder();
            for (int mi = 0; mi < 200; mi++) { if (mi > 0) manyModes.Append(','); manyModes.Append("模式" + mi); }
            DisplayModeOptions.Parse(manyModes.ToString(), out dmOpts, out dmErrs);
            Check("200项截断到上限且报错",
                dmOpts.Count == DisplayModeOptions.MaxOptionCount && dmErrs.Count > 0);
            string canon, derr;
            var dictOpts = DisplayModeOptions.Preset();
            Check("空输入=清空通过",
                DisplayModeOptions.ValidateInput("  ", dictOpts, out canon, out derr) && canon == "");
            Check("字典内过且存规范写法",
                DisplayModeOptions.ValidateInput("白场 ", dictOpts, out canon, out derr) && canon == "白场");
            Check("字典外拦并报选项",
                !DisplayModeOptions.ValidateInput("紫场", dictOpts, out canon, out derr)
                && derr != null && derr.Contains("白场"));
            Check("无配置对象走预设兜底", DisplayModeOptions.Resolve(null).Count == 8);
            var cfgModes = new DeviceConfig { DisplayModes = "红场,绿场" };
            var resolved = DisplayModeOptions.Resolve(cfgModes);
            Check("配置字典优先", resolved.Count == 2 && resolved[0] == "红场");
            var legacyList = DisplayModeOptions.WithLegacy(dictOpts, "紫场旧值");
            Check("遗留值追加末尾", legacyList.Count == 9 && legacyList[8] == "紫场旧值");
            Check("字典内遗留不重复",
                DisplayModeOptions.WithLegacy(dictOpts, "白场").Count == 8);
            Check("空遗留不追加",
                DisplayModeOptions.WithLegacy(dictOpts, "  ").Count == 8);
            if (miValidate != null)
            {
                Check("字典空合法",
                    (bool)miValidate.Invoke(null, new object[] { "DisplayModes", "", null }) == true);
                Check("字典合法过",
                    (bool)miValidate.Invoke(null, new object[] { "DisplayModes", "白场,红场", null }) == true);
                Check("字典全空拦截",
                    (bool)miValidate.Invoke(null, new object[] { "DisplayModes", " , ", null }) == false);
            }
        }

        // 12h. PolicyPresetV185 —— 预置策略 A/B/C（一键套用，V1.85 新增）
        private static void PolicyPresetTests()
        {
            // —— 名单与规模锁 ——
            Check("预置3个且试用顺序A/B/C",
                PolicyPresets.All.Count == 3
                && PolicyPresets.All[0].Id == "A"
                && PolicyPresets.All[1].Id == "B"
                && PolicyPresets.All[2].Id == "C");
            Check("管辖12个行为开关", PolicyPresets.GovernedKeys.Count == 12);
            bool governedInKeys = true;
            foreach (string k in PolicyPresets.GovernedKeys)
            {
                if (!ProjectPolicyStore.PolicyKeys.Contains(k)) { governedInKeys = false; break; }
            }
            Check("管辖全是PolicyKeys成员（跟项目走Policy.json）", governedInKeys);
            bool labelsOk = true;
            foreach (string k in PolicyPresets.GovernedKeys)
            {
                string lbl;
                if (!PolicyPresets.KeyLabels.TryGetValue(k, out lbl)
                    || string.IsNullOrWhiteSpace(lbl)) { labelsOk = false; break; }
            }
            Check("管辖开关中文名齐（确认框列差异项用）", labelsOk);
            bool fullSet = true;
            foreach (var p in PolicyPresets.All)
            {
                if (p.Values == null || p.Values.Count != PolicyPresets.GovernedKeys.Count) { fullSet = false; break; }
                foreach (string k in PolicyPresets.GovernedKeys)
                {
                    if (!p.Values.ContainsKey(k)) { fullSet = false; break; }
                }
                if (!fullSet) break;
            }
            Check("每预置都是完整12项集合（切预置不残留）", fullSet);
            bool metaOk = true;
            foreach (var p in PolicyPresets.All)
            {
                if (string.IsNullOrWhiteSpace(p.Title) || string.IsNullOrWhiteSpace(p.Scenario)
                    || string.IsNullOrWhiteSpace(p.HowToSwitch)) { metaOk = false; break; }
            }
            Check("预置标题/场景/换挡指引非空（UI直读，不另写文案）", metaOk);

            // —— 预置值与保存口径一致（存得进去） ——
            bool parseOk = true;
            string parseBad = "";
            foreach (var p in PolicyPresets.All)
            {
                foreach (var kv in p.Values)
                {
                    var prop = typeof(DeviceConfig).GetProperty(kv.Key);
                    if (prop == null
                        || ProjectPolicyStore.ParseValue(prop.PropertyType, kv.Value) == null)
                    {
                        parseOk = false;
                        parseBad = p.Id + ":" + kv.Key + "=" + kv.Value;
                        break;
                    }
                }
                if (!parseOk) break;
            }
            Check("预置值全部可解析（与PersistChanges同口径）", parseOk, parseBad);
            bool enumOptOk = true;
            foreach (var p in PolicyPresets.All)
            {
                foreach (var kv in p.Values)
                {
                    var prop = typeof(DeviceConfig).GetProperty(kv.Key);
                    if (prop == null || !prop.PropertyType.IsEnum) continue;
                    Tuple<string, string>[] opts;
                    if (!ProjectPolicyStore.EnumOptions.TryGetValue(kv.Key, out opts)) { enumOptOk = false; break; }
                    bool hit = false;
                    foreach (var o in opts)
                    {
                        if (string.Equals(o.Item2, kv.Value, StringComparison.OrdinalIgnoreCase)) { hit = true; break; }
                    }
                    if (!hit) { enumOptOk = false; break; }
                }
                if (!enumOptOk) break;
            }
            Check("枚举预置值全在下拉选项里（节点编辑器显示得出来）", enumOptOk);

            // —— 套用/探测往返 ——
            bool roundOk = true;
            foreach (var p in PolicyPresets.All)
            {
                var cfg = new DeviceConfig();
                if (PolicyPresets.ApplyToConfig(cfg, p.Id) != null
                    || !string.Equals(PolicyPresets.DetectPreset(cfg), p.Id, StringComparison.Ordinal))
                {
                    roundOk = false;
                    break;
                }
            }
            Check("A/B/C套用后探测回原预置", roundOk);
            Check("缺省配置=自定义（预置是刻意偏离现状，不是现状本身）",
                PolicyPresets.DetectPreset(new DeviceConfig()) == PolicyPresets.CustomId);
            var cfgA = new DeviceConfig();
            PolicyPresets.ApplyToConfig(cfgA, "A");
            cfgA.CompletionAction = CompletionAction.PowerOffOnly;
            Check("改一项即自定义（探测是全对上才算）",
                PolicyPresets.DetectPreset(cfgA) == PolicyPresets.CustomId);
            Check("null配置探测=自定义不抛",
                PolicyPresets.DetectPreset(null) == PolicyPresets.CustomId);
            Check("未知预置找不到", PolicyPresets.Find("Z") == null);
            Check("未知预置取值null", PolicyPresets.GetPresetValues("Z") == null);
            Check("未知预置套用报错不抛",
                PolicyPresets.ApplyToConfig(new DeviceConfig(), "Z") != null);
            Check("null配置套用报错不抛",
                PolicyPresets.ApplyToConfig(null, "A") != null);
            var valsA1 = PolicyPresets.GetPresetValues("A");
            var valsA2 = PolicyPresets.GetPresetValues("A");
            valsA1["SkipVacuum"] = "true";
            Check("取值返回副本（改返回不污染预置本身）",
                PolicyPresets.GetPresetValues("A")["SkipVacuum"] == "false"
                && valsA2["SkipVacuum"] == "false");

            // —— 本机无阀/单探头炉的安全锁 ——
            bool noVent = true;
            bool noSkip = true;
            foreach (var p in PolicyPresets.All)
            {
                string act = p.Values["CompletionAction"];
                if (string.Equals(act, "PowerOffAndVent", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(act, "PowerOffVentAndBeep", StringComparison.OrdinalIgnoreCase))
                {
                    noVent = false;
                }
                if (!string.Equals(p.Values["SkipVacuum"], "false", StringComparison.OrdinalIgnoreCase))
                {
                    noSkip = false;
                }
            }
            Check("三预置全不选泄压（本机无破空阀，选了保存即拦）", noVent);
            Check("三预置全不跳抽真空（真空治具永不跳过）", noSkip);
            Check("A/B超温联停关（零门槛直接套）",
                PolicyPresets.Find("A").Values["FanTempShutdownEnabled"] == "false"
                && PolicyPresets.Find("B").Values["FanTempShutdownEnabled"] == "false");
            Check("C超温联停开（单探头炉最后一道闸）",
                PolicyPresets.Find("C").Values["FanTempShutdownEnabled"] == "true");
            Check("C在本机上限0时组合校验拦（逼人先填上限，fail-safe）",
                AgingSequencer.ValidatePolicyCombination(
                    true, 0f, CompletionAction.PowerOffAndBeep, 0, false) != null);
            Check("C在上限60时组合校验过",
                AgingSequencer.ValidatePolicyCombination(
                    true, 60f, CompletionAction.PowerOffAndBeep, 0, false) == null);
            Check("A零门槛组合校验过（上电/风机/阀全默认）",
                AgingSequencer.ValidatePolicyCombination(
                    false, 0f, CompletionAction.PowerOffAndBeep, 0, false) == null);

            // —— 序列化口径 ——
            Check("布尔存小写",
                PolicyPresets.ToStorageString(true) == "true"
                && PolicyPresets.ToStorageString(false) == "false");
            Check("枚举存英文名",
                PolicyPresets.ToStorageString(ZeroDurationPolicy.Block) == "Block");
            Check("null存空串", PolicyPresets.ToStorageString(null) == "");

            // —— UI预置行（构造不断言弹窗，反射探针；注意 Sunny 下拉/按钮不是原生
            // ComboBox/Button 子类，一律按 Control + 反射读 Items/SelectedIndex，
            // 直接 as 原生类型会静默 null 造成假红，V1.85 实锤） ——
            ProcessPolicyForm bare = null;
            try
            {
                bare = new ProcessPolicyForm();
                var t = typeof(ProcessPolicyForm);
                object fCbo = t.GetField("_cboPreset",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(bare);
                object fBtn = t.GetField("_btnApplyPreset",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(bare);
                var desc = t.GetField("_lblPresetDesc",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(bare) as Control;
                var title = t.GetField("_lblPresetTitle",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(bare) as Control;
                Check("预置行4件全建好",
                    fCbo != null && fBtn != null && desc != null && title != null);
                var items = fCbo != null
                    ? fCbo.GetType().GetProperty("Items").GetValue(fCbo, null)
                        as System.Collections.IList
                    : null;
                Check("预置下拉4项（A/B/C/自定义）",
                    items != null && items.Count == 4);
                int selIdx = fCbo != null
                    ? (int)fCbo.GetType().GetProperty("SelectedIndex").GetValue(fCbo, null)
                    : -1;
                Check("无参构造回显自定义且整行禁用（只读安全）",
                    selIdx == 3
                    && !((Control)fCbo).Enabled && !((Control)fBtn).Enabled
                    && !string.IsNullOrWhiteSpace(desc.Text));
            }
            finally { try { if (bare != null) bare.Dispose(); } catch { } }
            ProcessPolicyForm full = null;
            try
            {
                var cfgFull = new DeviceConfig();
                PolicyPresets.ApplyToConfig(cfgFull, "B");
                full = new ProcessPolicyForm(cfgFull, null, true);
                var t = typeof(ProcessPolicyForm);
                object fCbo = t.GetField("_cboPreset",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(full);
                object fBtn = t.GetField("_btnApplyPreset",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(full);
                var desc = t.GetField("_lblPresetDesc",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(full) as Control;
                int selIdx = (int)fCbo.GetType().GetProperty("SelectedIndex").GetValue(fCbo, null);
                Check("B配置打开回显选中B", selIdx == 1);
                Check("管理员模式预置行可用",
                    ((Control)fCbo).Enabled && ((Control)fBtn).Enabled);
                Check("说明行显示B场景",
                    desc != null && desc.Text.Contains("调试"));
                Check("下拉列表拉宽防截断（看全选项）",
                    (int)fCbo.GetType().GetProperty("DropDownWidth").GetValue(fCbo, null) >= 300);
                object fTip = t.GetField("_presetTip",
                    BindingFlags.NonPublic | BindingFlags.Instance).GetValue(full);
                string tipText = "";
                if (fTip != null)
                {
                    tipText = (string)fTip.GetType().GetMethod("GetToolTip",
                        new Type[] { typeof(Control) }).Invoke(fTip, new object[] { (Control)fCbo });
                }
                Check("悬停提示存在且含B全标题（闭合框看全文）",
                    !string.IsNullOrWhiteSpace(tipText) && tipText.Contains("宽松试产"));
                // 释放配对（R2：无容器托管的提示必须手写释放，字段归 null 即证据；
                // 未 Show 的窗 Close 语义各版本不一，抛了就走 Dispose，两种路都进重写）
                try { ((Form)full).Close(); }
                catch { try { full.Dispose(); } catch { } }
                bool tipGone = true;
                try
                {
                    var tipNow = t.GetField("_presetTip",
                        BindingFlags.NonPublic | BindingFlags.Instance).GetValue(full);
                    tipGone = (tipNow == null);
                }
                catch { tipGone = false; }
                Check("关窗后悬停提示已释放（不进终结器）", tipGone);
            }
            finally { try { if (full != null) full.Dispose(); } catch { } }
        }

        // 12i. PolicyNodeComboV1851 —— 节点选项框按预置下拉口径统一（V1.85.1 新增）
        //
        // 【测什么】全部节点 Bool/Enum 下拉：下拉列表按最长选项实测拉宽（不再与框
        // 同宽 270，最长 22 字项旧口径下被拦腰截断）+ 悬停恒=选中项全文（闭合框
        // 270 放不下长中文时的第二路）+ 切节点清提示表（不清=已释放控件被钉住泄漏）。
        // 独立再量一遍逐项宽度（与产品同 TextRenderer 口径，但每个数字重新算，
        // 不是复述产品字段），另有 harness 真实展开截图为第二证据（见 CHANGELOG）。
        static void PolicyNodeComboTests()
        {
            var t = typeof(ProcessPolicyForm);
            var mSelect = t.GetMethod("SelectNode",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var fEditors = t.GetField("_editorControls",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var fTip = t.GetField("_editorTip",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Check("节点编辑反射口径有效（防探针本身失效假绿）",
                mSelect != null && fEditors != null && fTip != null);

            ProcessPolicyForm form = null;
            try
            {
                form = new ProcessPolicyForm(new DeviceConfig(), null, true);
                object tipObj = fTip.GetValue(form);
                Check("节点悬停提示单例已建", tipObj != null);

                // 期望数：全部节点 Bool/Enum key 个数（Text/Multiline 不是下拉，不进数；
                // VentValveDoPoint 藏行是 Text 也不影响，数对不上=有下拉漏网或多算）。
                int expect = 0;
                foreach (var node in PolicyGraph.Nodes)
                {
                    var keys = node.GetType().GetField("Keys").GetValue(node)
                        as System.Collections.IList;
                    foreach (var k in keys)
                    {
                        string kind = k.GetType().GetField("Kind").GetValue(k).ToString();
                        if (kind == "Bool" || kind == "Enum") expect++;
                    }
                }

                int comboTotal = 0, narrowTotal = 0, clipTotal = 0, tipTotal = 0;
                int worstNeed = 0;
                string worstWhere = "";
                Control firstCombo = null;
                foreach (var node in PolicyGraph.Nodes)
                {
                    string id = (string)node.GetType().GetField("Id").GetValue(node);
                    mSelect.Invoke(form, new object[] { id, false });
                    var editors = fEditors.GetValue(form) as System.Collections.IDictionary;
                    foreach (System.Collections.DictionaryEntry en in editors)
                    {
                        var cmb = en.Value as Control;
                        if (cmb == null || cmb.GetType().Name != "UIComboBox") continue;
                        comboTotal++;
                        if (firstCombo == null) firstCombo = cmb;
                        int ddw = (int)cmb.GetType().GetProperty("DropDownWidth")
                            .GetValue(cmb, null);
                        if (ddw < cmb.Width) narrowTotal++;
                        var items = cmb.GetType().GetProperty("Items")
                            .GetValue(cmb, null) as System.Collections.IList;
                        int need = cmb.Width;
                        using (Graphics g = cmb.CreateGraphics())
                        {
                            foreach (var it in items)
                            {
                                string s = it != null ? it.ToString() : "";
                                if (s.Length == 0) continue;
                                int w = TextRenderer.MeasureText(g, s, cmb.Font,
                                    new Size(int.MaxValue, int.MaxValue),
                                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding).Width;
                                if (w > need) need = w;
                            }
                        }
                        need += SystemInformation.VerticalScrollBarWidth + 12;
                        if (need > worstNeed) { worstNeed = need; worstWhere = id + "/" + en.Key; }
                        if (need > ddw + 1) clipTotal++;
                        int sel = (int)cmb.GetType().GetProperty("SelectedIndex")
                            .GetValue(cmb, null);
                        string selText = (sel >= 0 && sel < items.Count && items[sel] != null)
                            ? items[sel].ToString() : "";
                        string tipText = null;
                        try
                        {
                            tipText = (string)tipObj.GetType().GetMethod("GetToolTip",
                                new Type[] { typeof(Control) })
                                .Invoke(tipObj, new object[] { cmb });
                        }
                        catch { tipText = null; }
                        if (tipText != selText) tipTotal++;
                    }
                }
                Check("选项框全扫到（数=全部Bool/Enum key）",
                    expect > 0 && comboTotal == expect,
                    "扫到" + comboTotal + "/期望" + expect);
                Check("下拉都不比框窄", narrowTotal == 0);
                Check("下拉列表无截断（逐项独立实测）", clipTotal == 0,
                    "最长 " + worstWhere + " 需" + worstNeed);
                Check("悬停恒=选中全文", tipTotal == 0);
                // 反向验证（坑38：新探针必须能 FAIL）：旧口径下拉=框宽 270，
                // 最长项需要 >270，旧代码跑同一判据必红，证明本模块不是恒绿。
                Check("旧口径必截断（反向验证）", worstNeed > 270,
                    worstWhere + " 需" + worstNeed + ">270");
                // 切节点清表：首节点旧下拉已释放，提示表里必须查无此人
                //（不清表=旧控件被提示钉住，切一次漏几个）。
                bool cleared = false;
                try
                {
                    string stale = (string)tipObj.GetType().GetMethod("GetToolTip",
                        new Type[] { typeof(Control) })
                        .Invoke(tipObj, new object[] { firstCombo });
                    cleared = string.IsNullOrEmpty(stale);
                }
                catch { cleared = false; }
                Check("切节点后旧下拉提示已清（不清表即泄漏）", cleared);
                // 改选同步：旧节点重选后换一项，悬停跟着变（提示不是一次性快照）
                mSelect.Invoke(form, new object[] { "unload", false });
                var ed2 = fEditors.GetValue(form) as System.Collections.IDictionary;
                var cmb2 = ed2["EventIdentityMode"] as Control;
                bool syncOk = false;
                try
                {
                    var propSel = cmb2.GetType().GetProperty("SelectedIndex");
                    var items2 = cmb2.GetType().GetProperty("Items")
                        .GetValue(cmb2, null) as System.Collections.IList;
                    int other = ((int)propSel.GetValue(cmb2, null) == 0) ? 1 : 0;
                    propSel.SetValue(cmb2, other, null);
                    string tip2 = (string)tipObj.GetType().GetMethod("GetToolTip",
                        new Type[] { typeof(Control) })
                        .Invoke(tipObj, new object[] { cmb2 });
                    syncOk = tip2 == items2[other].ToString();
                }
                catch { syncOk = false; }
                Check("改选后悬停同步换（非一次性快照）", syncOk);
                // —— 每项标题 tooltip（新增：标题悬停看说明，超40字换行） ——
                // 静态层：全部节点 key 在 SettingsForm 都有说明（与设置表同源，不另写一份）；
                // 换行层：WrapTooltip 后每行≤40字（全仓唯一换行口）。
                // 说明/换行都是 internal static，走反射（与本文件既有 WrapTooltip 用例同口径）。
                var miDesc = typeof(SettingsForm).GetMethod("GetDescription",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var miWrap2 = typeof(SettingsForm).GetMethod("WrapTooltip",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Check("反射找到 GetDescription/WrapTooltip", miDesc != null && miWrap2 != null);
                bool allDescOk = miDesc != null && miWrap2 != null;
                bool allWrapOk = miDesc != null && miWrap2 != null;
                if (miDesc != null && miWrap2 != null)
                {
                    foreach (var node in PolicyGraph.Nodes)
                    {
                        var keys = node.GetType().GetField("Keys").GetValue(node)
                            as System.Collections.IList;
                        foreach (var k in keys)
                        {
                            string key = (string)k.GetType().GetField("Key").GetValue(k);
                            string desc = (string)miDesc.Invoke(null, new object[] { key });
                            if (string.IsNullOrEmpty(desc)) { allDescOk = false; continue; }
                            string wrapped = (string)miWrap2.Invoke(null, new object[] { desc });
                            if (string.IsNullOrEmpty(wrapped)) { allWrapOk = false; continue; }
                            foreach (string line in wrapped.Split(new[] { "\r\n" }, StringSplitOptions.None))
                            {
                                if (line.Length > 40) { allWrapOk = false; break; }
                            }
                        }
                    }
                }
                Check("节点key全有标题说明（与设置表同源）", allDescOk);
                Check("标题说明换行后每行≤40字", allWrapOk);
                bool emptyOk = false;
                try
                {
                    emptyOk = (string)miDesc.Invoke(null, new object[] { "BogusKey_Xyz" }) == ""
                        && (string)miDesc.Invoke(null, new object[] { null }) == ""
                        && (string)miWrap2.Invoke(null, new object[] { "" }) == "";
                }
                catch { emptyOk = false; }
                Check("说明缺key回空不抛", emptyOk);
                // UI 层：切到报警节点（key 最多），每个 20px 高的标题行都有悬停且每行≤40字；
                // 文本框也同挂（下拉框是选中项全文，不管它）。
                bool uiTipsOk = false;
                try
                {
                    // 【防挂起】上面的"改选同步换"故意改了下拉选项（窗体已置脏）：
                    // 直接切节点会弹"有未保存修改，切换前保存吗"模态框，无人值守下永远卡住
                    // （现场复现：卡在"说明缺key回空不抛"之后）。这里只读悬停文本，
                    // 不测脏提示，先把 _dirty 复位再切节点。
                    var fDirty = t.GetField("_dirty",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (fDirty != null) fDirty.SetValue(form, false);
                    mSelect.Invoke(form, new object[] { "alarm", false });
                    var pnlF = t.GetField("_pnlEditors",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    var pnl = pnlF != null ? pnlF.GetValue(form) as Control : null;
                    var getTip = tipObj.GetType().GetMethod("GetToolTip",
                        new Type[] { typeof(Control) });
                    int titleCount = 0, titleOk = 0;
                    if (pnl != null)
                    {
                        foreach (Control c in pnl.Controls)
                        {
                            if (c == null || c.GetType().Name != "UILabel") continue;
                            if (c.Height != 20) continue;
                            titleCount++;
                            string tt = (string)getTip.Invoke(tipObj, new object[] { c });
                            if (string.IsNullOrEmpty(tt)) continue;
                            bool wrapOk = true;
                            foreach (string line in tt.Split(new[] { "\r\n" }, StringSplitOptions.None))
                            {
                                if (line.Length > 40) { wrapOk = false; break; }
                            }
                            if (wrapOk) titleOk++;
                        }
                    }
                    uiTipsOk = titleCount > 0 && titleOk == titleCount;
                }
                catch { uiTipsOk = false; }
                Check("报警节点每项标题都有换行tooltip", uiTipsOk);
                // 释放配对（R2：与 _presetTip 同款无容器托管，关窗两字段皆 null）
                try { ((Form)form).Close(); }
                catch { try { form.Dispose(); } catch { } }
                bool bothGone = false;
                try
                {
                    bothGone = t.GetField("_presetTip",
                            BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form) == null
                        && t.GetField("_editorTip",
                            BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form) == null;
                }
                catch { bothGone = false; }
                Check("关窗后两悬停提示皆释放", bothGone);
            }
            finally { try { if (form != null) form.Dispose(); } catch { } }
        }

        // =====================================================================
        // 12j. DeployDiagV188_9 —— 调试部署诊断（V1.88.9 新增）
        //   BuildWatermark：启动水印纯函数（版本/时间/混淆标记/空兜底）+ 运行时入口不抛；
        //   CrashLogWriter：文件名格式/正文排版/落盘（含 null 与非 Exception 对象兜底）。
        //   真实 Write 落在 BaseDirectory\Logs（harness 把产物拷到隔离临时目录跑，
        //   不污染仓库与 bin\Debug，见本文件头"怎么跑"）。
        // =====================================================================
        static void DeployDiagTests()
        {
            // —— 水印纯函数：固定输入看输出关键字（与分隔符解耦，只锁"含什么"） ——
            DateTime bt = new DateTime(2026, 9, 15, 10, 30, 0);
            string line = BuildWatermark.BuildWatermarkLine(
                "V1.88.9", "1.0.0.0", "1.0.0.0", bt, false, true, "Microsoft Windows NT 10.0");
            Check("水印含对外版本号", line.Contains("V1.88.9"));
            Check("水印含程序集版本", line.Contains("1.0.0.0"));
            Check("水印含构建时间", line.Contains("2026-09-15 10:30:00"));
            Check("调试版标未混淆", line.Contains("未混淆"));
            Check("水印含进程位数", line.Contains("64位"));

            string lineObf = BuildWatermark.BuildWatermarkLine(
                "V1.89.0", "1.0.0.0", "", bt, true, false, "");
            Check("混淆版提示mapping反解", lineObf.Contains("mapping"));
            Check("构建时间拿不到显示未知", lineObf.Contains("未知"));

            // 空输入全兜底：null/空串/MinValue 进来不能抛，整行也不能消失（崩溃路径可能啥都拿不到）。
            string lineEmpty = "";
            bool emptyOk = false;
            try
            {
                lineEmpty = BuildWatermark.BuildWatermarkLine(null, null, null,
                    DateTime.MinValue, false, false, null);
                emptyOk = lineEmpty.Contains("未知") && lineEmpty.Contains("[启动]");
            }
            catch { emptyOk = false; }
            Check("空输入兜底未知不抛", emptyOk);

            // —— 运行时入口：真实环境收集，不抛且带上 ReleaseLabel 常量 ——
            string startup = "";
            bool startupOk = false;
            try
            {
                startup = BuildWatermark.GetStartupLine();
                startupOk = startup.Contains("[启动]")
                    && startup.Contains(BuildWatermark.ReleaseLabel);
            }
            catch { startupOk = false; }
            Check("GetStartupLine不抛且含版本", startupOk);
            // 调试期锁：发混淆包时才允许改 true（改时同步改本断言 + CHANGELOG），
            // 平时谁手滑翻了这里立刻红，防"调试包被标成混淆版"误导排障。
            Check("调试期混淆标记恒false", BuildWatermark.IsObfuscatedBuild == false);
            // 构建时间入口同样不抛（取不到回 MinValue，由纯函数渲染成"未知"）。
            bool btOk = false;
            try
            {
                DateTime t = BuildWatermark.GetBuildTime();
                btOk = t == DateTime.MinValue || t > new DateTime(2020, 1, 1);
            }
            catch { btOk = false; }
            Check("GetBuildTime不抛", btOk);

            // —— 崩溃文件名：格式 + 固定尾确定性 ——
            string fn = CrashLogWriter.BuildCrashFileName(new DateTime(2026, 9, 15, 10, 30, 15, 123), "ab12cd34");
            Check("文件名确定性（固定尾）",
                fn == "Crash_20260915_103015123_ab12cd34.log", fn);
            string fnAuto = CrashLogWriter.BuildCrashFileName(DateTime.Now, null);
            Check("自动尾文件名格式合法",
                fnAuto.StartsWith("Crash_20") && fnAuto.EndsWith(".log") && fnAuto.Length > 30, fnAuto);

            // —— 崩溃正文：排版顺序水印→线程→类型/消息→堆栈，全关键字锁 ——
            string content = CrashLogWriter.BuildCrashContent(
                new DateTime(2026, 9, 15, 10, 30, 15), "[启动] 软件版本 V1.88.9",
                "UI线程", "System.NullReferenceException", "未将对象引用设置到对象实例",
                "   在 AgingTestSystem.Views.MainForm.Foo() 位置 1");
            Check("正文含水印行", content.Contains("V1.88.9"));
            Check("正文含崩溃线程", content.Contains("UI线程"));
            Check("正文含异常类型+消息",
                content.Contains("NullReferenceException") && content.Contains("未将对象引用"));
            Check("正文含堆栈详情", content.Contains("MainForm.Foo()"));

            // 堆栈为空→占位符，不能出现空行断链。
            string noStack = CrashLogWriter.BuildCrashContent(
                DateTime.Now, "", "", "", null, "");
            Check("空堆栈给占位不抛",
                noStack.Contains("无堆栈详情") && noStack.Contains("未知"));

            // null 异常对象（非 UI 线程极端情况）：Write 必须吃下并落盘。
            string nullMark = "NULL兜底_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string p0 = null;
            bool nullOk = false;
            try
            {
                p0 = CrashLogWriter.Write("非UI线程" + nullMark, null);
                nullOk = p0 != null && File.Exists(p0)
                    && File.ReadAllText(p0, Encoding.UTF8).Contains("空异常对象");
            }
            catch { nullOk = false; }
            Check("null异常对象落盘不抛", nullOk);
            try { if (p0 != null && File.Exists(p0)) File.Delete(p0); } catch { }

            // 非 Exception 对象（如 throw "字符串"）：ToString 留痕。
            string p1 = null;
            bool objOk = false;
            try
            {
                p1 = CrashLogWriter.Write("非UI线程", "字符串异常_" + nullMark);
                objOk = p1 != null && File.Exists(p1)
                    && File.ReadAllText(p1, Encoding.UTF8).Contains("字符串异常_" + nullMark);
            }
            catch { objOk = false; }
            Check("非Exception对象留痕不抛", objOk);
            try { if (p1 != null && File.Exists(p1)) File.Delete(p1); } catch { }

            // —— 真实落盘：正常 Exception 全链路（路径在 Logs 下 + 内容含标记 + 中文无乱码） ——
            string mark = "部署诊断zne_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            Exception boom;
            try { throw new InvalidOperationException("模拟崩溃_" + mark + "中文"); }
            catch (Exception e2) { boom = e2; }
            string p2 = null;
            bool diskOk = false;
            try
            {
                p2 = CrashLogWriter.Write("UI线程", boom);
                string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                string text = p2 != null && File.Exists(p2)
                    ? File.ReadAllText(p2, Encoding.UTF8) : "";
                diskOk = p2 != null && File.Exists(p2)
                    && p2.StartsWith(logDir)
                    && text.Contains("模拟崩溃_" + mark)
                    && text.Contains("InvalidOperationException")
                    && text.Contains(BuildWatermark.ReleaseLabel);
            }
            catch { diskOk = false; }
            Check("真实崩溃落盘内容全", diskOk);

            // 连写两次互不覆盖（一崩一文件，文件名不同且都存在）。
            string p3 = null;
            bool twiceOk = false;
            try
            {
                p3 = CrashLogWriter.Write("UI线程", boom);
                twiceOk = p3 != null && p2 != null && p3 != p2
                    && File.Exists(p2) && File.Exists(p3);
            }
            catch { twiceOk = false; }
            Check("连写两次文件名不同且都存在", twiceOk);
            try { if (p2 != null && File.Exists(p2)) File.Delete(p2); } catch { }
            try { if (p3 != null && File.Exists(p3)) File.Delete(p3); } catch { }
        }

    }
}

