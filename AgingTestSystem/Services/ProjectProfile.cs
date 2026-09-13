
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 项目档案管理（【V1.67 新增】一期"多项目切换"）。
    ///
    /// 【解决什么问题】
    /// 以前所有运行时文件（配方/工位设置/主页布局）都堆在程序目录，换一个客户
    /// 就得手动备份一堆 json，出差现场极易弄混。现在按"项目"隔离：
    ///   程序目录/Projects/&lt;项目名&gt;/  =  Recipes.json + StationSettings.json
    ///                              + HomeLayout.json + Policy.json（策略）
    /// 出差切项目 = 下拉选个名字即时生效（【V1.72.10 热更】无需重启），30 秒搞定，不动代码。
    ///
    /// 【跟项目的 vs 跟机器的：为什么这样分】
    /// - 跟项目（进 Profile 目录）：配方、工位设置、主页布局、工艺策略——换客户就换这套。
    /// - 跟机器（留程序目录全局）：Users.json（账号全公司通用）、TestSession.json
    ///   （本机中断快照）、PanelLayout.json（本机屏幕布局）、Logs/（本机日志）、
    ///   App.config 连接参数（COM 口/IP 是这台工控机的接线，不是工艺）。
    /// 当前是哪个项目，只是一个机器级指针（App.config 的 ActiveProject），
    /// 所以重装/换工控机要重新选一次项目，这是有意为之（防止指错项目跑错工艺）。
    /// </summary>
    public static class ProjectProfile
    {
        /// <summary>档案根目录名（程序运行目录下）。运行时目录被 gitignore（bin/），不入库。</summary>
        public const string ProjectsDirName = "Projects";

        /// <summary>策略文件名（每个项目目录下一份，见 ProjectPolicyStore）。</summary>
        public const string PolicyFileName = "Policy.json";

        /// <summary>跟项目走的文件（切换项目即切换这些文件）。</summary>
        public static readonly string[] ProjectScopedFiles = new string[]
        {
            "Recipes.json",
            "StationSettings.json",
            "HomeLayout.json",
            PolicyFileName
        };

        /// <summary>
        /// 档案根目录绝对路径（程序运行目录/Projects，不存在则创建）。
        /// </summary>
        public static string ProjectsRoot
        {
            get
            {
                string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ProjectsDirName);
                if (!Directory.Exists(root))
                {
                    Directory.CreateDirectory(root);
                }
                return root;
            }
        }

        /// <summary>
        /// <summary>缺省项目名（首跑/指针缺失时用；【V1.72.9】Default 改名而来，列表里不再有 Default）。</summary>
        public const string DefaultProfileName = "烧屏测试";

        /// <summary>
        /// 历史遗留目录名（V1.72.9 之前的缺省项目名）。项目未上线、无老用户，
        /// 启动自愈见 <see cref="CleanupLegacyDefault"/>：只做"改名/删除"，不做数据迁移。
        /// </summary>
        private const string LegacyDefaultProfileName = "Default";

        /// <summary>
        /// 当前生效的项目名（读 App.config 的 ActiveProject；为空/缺省/非法回 "烧屏测试"）。
        /// 大小写保留原样，目录名即项目名。
        /// 【大扫荡】非法值（手改 ..\..）回缺省，不让 ActiveProfileDir 建到外面去。
        /// </summary>
        public static string ActiveProfileName
        {
            get
            {
                string raw = null;
                try { raw = ConfigurationManager.AppSettings["ActiveProject"]; }
                catch { raw = null; }
                if (!IsValidProfileName(raw)) return DefaultProfileName;
                return raw.Trim();
            }
        }

        /// <summary>
        /// 当前项目目录（不存在则创建）。调用方直接拼文件名即可。
        /// </summary>
        public static string ActiveProfileDir
        {
            get
            {
                string dir = Path.Combine(ProjectsRoot, ActiveProfileName);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                return dir;
            }
        }

        /// <summary>
        /// 启动时确保档案就绪（MainForm 读任何运行时文件之前调用）：
        /// 1) 无 ActiveProject → 指向 烧屏测试 并写回 exe.config（机器指针初始化）；
        /// 2) 清掉历史遗留的 Default 目录（见 CleanupLegacyDefault，项目未上线、改干净）；
        /// 3) 项目目录不存在 → 创建。
        /// 【V1.68 改干净】删掉了"老文件搬家"：项目未上线，没有 V1.67 前的老用户，
        /// 程序目录下的散文件一律视为垃圾不再认——要是启动后配方空了，去 Projects/烧屏测试
        /// 里建，不要从根目录捡（两份数据源是 Suspicion 之源）。
        /// </summary>
        /// <returns>生效的项目名</returns>
        public static string EnsureActiveProfile()
        {
            // 0) 先清遗留 Default（必须在 ActiveProfileDir 建目录之前：若 Default 里有货，
            //    直接整体改名成 烧屏测试，后面第 3 步就不用再建空目录，数据一次到位）
            CleanupLegacyDefault();

            string name = ActiveProfileName;

            // 1) 指针初始化：没配过就写 烧屏测试（只写一次，以后用户在"项目切换"里改）
            try
            {
                if (string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["ActiveProject"]))
                {
                    var cfg = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                    var setting = cfg.AppSettings.Settings["ActiveProject"];
                    if (setting == null) cfg.AppSettings.Settings.Add("ActiveProject", name);
                    else setting.Value = name;
                    cfg.Save(ConfigurationSaveMode.Modified);
                    ConfigurationManager.RefreshSection("appSettings");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[项目档案] 初始化 ActiveProject 失败: {ex.Message}");
            }

            // 2) 目录就绪（幂等：已存在即跳过；首次建目录的问题在启动时暴露，
            //    而不是拖到第一次保存时才炸）
            Directory.CreateDirectory(ActiveProfileDir);

            return name;
        }

        /// <summary>
        /// 清掉历史遗留的 Default 目录（【V1.72.10 改干净】V1.72.9 把缺省项目改名
        /// "烧屏测试"，但老版本跑过的机器上还留着空的 Projects/Default，项目列表里
        /// 阴魂不散。本方法在每次启动时幂等执行，保证列表里永远没有 Default）。
        ///
        /// 【规则】项目未上线、无老用户包袱，只认"目录里有没有货"：
        /// - Default 不存在 → 直接返回（大多数机器走这里，零开销）；
        /// - Default 存在、而 烧屏测试 不存在 → 整体改名（Move，里面若有配方一个不少）；
        /// - 两个都存在 → 把 Default 里"烧屏测试没有"的文件拷过去补齐，然后删掉 Default
        ///   （重名文件以 烧屏测试 为准，不覆盖——新名字是正主）；
        /// - 空的 Default → 直接删。
        /// 全程 try/catch：清不掉（如文件被占用）只记 Debug 日志、不阻塞启动，
        /// 下次启动再试一次。
        /// </summary>
        public static void CleanupLegacyDefault()
        {
            try
            {
                string root = ProjectsRoot;
                string legacy = Path.Combine(root, LegacyDefaultProfileName);
                if (!Directory.Exists(legacy)) return;
                string target = Path.Combine(root, DefaultProfileName);
                if (!Directory.Exists(target))
                {
                    // 正主还没建：整体改名，一步到位（目录非空也照搬，数据不丢）
                    Directory.Move(legacy, target);
                    return;
                }
                // 两个都在：Default 里独有的文件补拷给正主（重名不覆盖），然后删 Default
                foreach (string f in ProjectScopedFiles)
                {
                    try
                    {
                        string src = Path.Combine(legacy, f);
                        string dst = Path.Combine(target, f);
                        if (File.Exists(src) && !File.Exists(dst)) File.Copy(src, dst);
                    }
                    catch { /* 单个文件失败跳过，继续清别的 */ }
                }
                try { Directory.Delete(legacy, true); }
                catch { /* 被占用下次启动再清 */ }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[项目档案] 清理遗留 Default 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 取运行时文件路径（调用方唯一入口，别再自己拼 BaseDirectory）：
        /// projectScoped=true（配方/工位设置/主页布局）→ 当前项目目录；
        /// 否则 → 程序目录（全局：用户/快照/日志等跟机器的文件）。
        /// </summary>
        /// <param name="fileName">文件名（如 "Recipes.json"）</param>
        /// <param name="projectScoped">是否跟项目走</param>
        public static string ResolveDataPath(string fileName, bool projectScoped)
        {
            if (projectScoped)
            {
                return Path.Combine(ActiveProfileDir, fileName);
            }
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
        }

        /// <summary>
        /// 项目名合法性（【大扫荡】唯一口径：Delete/SwitchTo/ActiveProfileDir 与
        /// CreateProfile 共用。只允许字母/数字/中文/下划线/连横线/空格——`..`、
        /// `/`、`\`、`:` 全拒，防路径穿越出 Projects 目录删任意目录）。
        /// </summary>
        public static bool IsValidProfileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            string n = name.Trim();
            if (n.Length == 0 || n.Length > 64) return false;
            if (n == "." || n == "..") return false;
            foreach (char c in n)
            {
                bool ok = char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == ' ';
                if (!ok) return false;
            }
            return true;
        }

        /// <summary>
        /// 项目目录哨兵文件名（【大扫荡】CreateProfile 成功最后写入，证明"这是走正常流程建的项目"。
        /// ListProfiles 认"哨兵或任一跟项目文件"——手丢的 backup 空目录冒充不了可切换项目）。
        /// </summary>
        public const string ProfileMarkerFileName = ".project";

        /// <summary>列出全部项目名（目录名即项目名，按字母序）。</summary>
        public static List<string> ListProfiles()
        {
            var result = new List<string>();
            try
            {
                string root = ProjectsRoot;
                foreach (string dir in Directory.GetDirectories(root))
                {
                    string name = Path.GetFileName(dir);
                    // 【大扫荡】只认"真项目"：名合法 +（有哨兵 或 至少含一个跟项目文件）。
                    // 手丢的 backup 空目录/野目录不再冒充可切换项目（切入后配方策略全空易误跑）；
                    // 老目录（无哨兵但有数据）照认，不误伤。
                    if (!IsValidProfileName(name)) continue;
                    if (!File.Exists(Path.Combine(dir, ProfileMarkerFileName)))
                    {
                        bool hasData = false;
                        foreach (string f in ProjectScopedFiles)
                        {
                            if (File.Exists(Path.Combine(dir, f))) { hasData = true; break; }
                        }
                        if (!hasData) continue;
                    }
                    result.Add(name);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[项目档案] 列出项目失败: {ex.Message}");
            }
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        /// <summary>
        /// 新建项目（以当前生效文件为模板复制，回来改配置即可，不用从零填）：
        /// 复制当前项目目录下的全部跟项目文件；目标已存在返回 false。
        /// 项目名只允许字母/数字/中文/下划线/连横线/空格，其余字符拒绝（防路径穿越）。
        /// </summary>
        /// <param name="newName">新项目名</param>
        /// <returns>true=创建成功，false=名字非法或已存在</returns>
        public static bool CreateProfile(string newName)
        {
            if (!IsValidProfileName(newName)) return false;
            string name = newName.Trim();
            string tmpDir = null;
            try
            {
                string target = Path.Combine(ProjectsRoot, name);
                if (Directory.Exists(target)) return false;
                // 【大扫荡】原子化：临时目录组装（拷模板+写哨兵），一次 Move 到位；
                // 中途失败删临时目录，不留半截空目录进列表。
                tmpDir = Path.Combine(ProjectsRoot, name + ".tmp_" + Guid.NewGuid().ToString("N"));
                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
                Directory.CreateDirectory(tmpDir);
                string src = ActiveProfileDir;
                foreach (string f in ProjectScopedFiles)
                {
                    string s = Path.Combine(src, f);
                    if (File.Exists(s))
                    {
                        File.Copy(s, Path.Combine(tmpDir, f));
                    }
                }
                File.WriteAllText(Path.Combine(tmpDir, ProfileMarkerFileName),
                    "AgingTestSystem project: " + name);
                Directory.Move(tmpDir, target);
                tmpDir = null;   // 已到位，不再清理
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[项目档案] 创建项目失败: {ex.Message}");
                return false;
            }
            finally
            {
                if (tmpDir != null)
                {
                    try { if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true); }
                    catch { /* 清不掉下次启动再说，不阻塞 */ }
                }
            }
        }

        /// <summary>
        /// 删除项目（【V1.72.11】建错/验证用项目的清理口，配切换窗"删除项目"按钮）。
        ///
        /// 【规则】空名、当前项目、不存在 → 一律 false：
        /// - 当前项目正在用（内存数据就是它），删了文件和内存对不上，
        ///   必须先切到别的项目再删；
        /// - 删的是"非当前"项目目录，不碰当前内存/采集，在测也可删别的项目，
        ///   调用方只拦"删当前"不拦在测（别误收紧）。
        /// 成功 = 整个项目目录递归删除（配方/工位设置/布局/策略一起走）。
        /// </summary>
        /// <param name="name">要删除的项目名</param>
        /// <returns>true=已删除，false=拒绝或失败</returns>
        public static bool DeleteProfile(string name)
        {
            // 【大扫荡】先验名：DeleteProfile("..\\..\\重要目录")存在即递归删除，
            // 必须拦在 Path.Combine 之前（CreateProfile 早拦了，这里补齐）。
            if (!IsValidProfileName(name)) return false;
            string target = name.Trim();
            try
            {
                // 当前项目禁删（先切走再删，防内存与文件对不上）
                if (string.Equals(target, ActiveProfileName, StringComparison.OrdinalIgnoreCase))
                    return false;
                string dir = Path.Combine(ProjectsRoot, target);
                if (!Directory.Exists(dir)) return false;
                Directory.Delete(dir, true);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[项目档案] 删除项目失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 切换当前项目（只写机器指针 ActiveProject + 刷 appSettings 缓存）。
        /// 【V1.72.10 热更】写完指针即返回，内存数据的换装由调用方
        /// （MainForm.ReloadActiveProject）接力完成：重载配方/工位缓存/策略叠加/
        /// 主页布局 + 清工位指派 + 刷面板，全程无需重启。
        /// 约束：调用前必须确认无工位在测（ProjectSwitchForm 已拦，MainForm 双保险复查）。
        /// </summary>
        /// <param name="name">已存在的项目名</param>
        /// <returns>true=指针已改（待热加载），false=项目不存在或写入失败</returns>
        public static bool SwitchTo(string name)
        {
            // 【大扫荡】先验名：指针一旦写成 ..\..，后续 ResolveDataPath 全带出 Projects。
            if (!IsValidProfileName(name)) return false;
            try
            {
                string target = Path.Combine(ProjectsRoot, name.Trim());
                if (!Directory.Exists(target)) return false;
                var cfg = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                var setting = cfg.AppSettings.Settings["ActiveProject"];
                if (setting == null) cfg.AppSettings.Settings.Add("ActiveProject", name.Trim());
                else setting.Value = name.Trim();
                cfg.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[项目档案] 切换项目失败: {ex.Message}");
                return false;
            }
        }
    }
}
