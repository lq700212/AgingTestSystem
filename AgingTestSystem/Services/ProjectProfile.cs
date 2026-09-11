
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
    /// 出差切项目 = 下拉选个名字 + 重启，30 秒搞定，不动代码。
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
        /// 当前生效的项目名（读 App.config 的 ActiveProject；为空/缺省回 "Default"）。
        /// 大小写保留原样，目录名即项目名。
        /// </summary>
        public static string ActiveProfileName
        {
            get
            {
                string raw = null;
                try { raw = ConfigurationManager.AppSettings["ActiveProject"]; }
                catch { raw = null; }
                if (string.IsNullOrWhiteSpace(raw)) return "Default";
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
        /// 1) 无 ActiveProject → 指向 Default 并写回 exe.config（机器指针初始化）；
        /// 2) 项目目录不存在 → 创建；
        /// 3) 【一次性迁移】项目目录是空的、但程序目录下有老文件（V1.67 前的老用户），
        ///    把老文件搬进 Default（搬家不是复制：原地留会造成"两份数据" Suspicion）。
        ///    只有 ActiveProject == Default 时才搬（非 Default 说明用户已在用档案体，不碰）。
        /// </summary>
        /// <returns>生效的项目名</returns>
        public static string EnsureActiveProfile()
        {
            string name = ActiveProfileName;

            // 1) 指针初始化：没配过就写 Default（只写一次，以后用户在"项目切换"里改）
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

            // 2) 目录就绪
            string dir = ActiveProfileDir;

            // 3) 老文件搬家（仅 Default + 目录为空 + 根目录有老文件）
            if (string.Equals(name, "Default", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    bool dirEmpty = Directory.GetFiles(dir).Length == 0;
                    if (dirEmpty)
                    {
                        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                        foreach (string f in ProjectScopedFiles)
                        {
                            if (string.Equals(f, PolicyFileName, StringComparison.OrdinalIgnoreCase)) continue;
                            string src = Path.Combine(baseDir, f);
                            if (File.Exists(src))
                            {
                                File.Move(src, Path.Combine(dir, f));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[项目档案] 老文件迁移失败: {ex.Message}");
                }
            }

            return name;
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

        /// <summary>列出全部项目名（目录名即项目名，按字母序）。</summary>
        public static List<string> ListProfiles()
        {
            var result = new List<string>();
            try
            {
                string root = ProjectsRoot;
                foreach (string dir in Directory.GetDirectories(root))
                {
                    result.Add(Path.GetFileName(dir));
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
            if (string.IsNullOrWhiteSpace(newName)) return false;
            string name = newName.Trim();
            foreach (char c in name)
            {
                bool ok = char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == ' '
                    || (c >= 0x4E00 && c <= 0x9FFF);
                if (!ok) return false;
            }
            try
            {
                string target = Path.Combine(ProjectsRoot, name);
                if (Directory.Exists(target)) return false;
                Directory.CreateDirectory(target);
                string src = ActiveProfileDir;
                foreach (string f in ProjectScopedFiles)
                {
                    string s = Path.Combine(src, f);
                    if (File.Exists(s))
                    {
                        File.Copy(s, Path.Combine(target, f));
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[项目档案] 创建项目失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 切换当前项目（写机器指针；调用方负责提示重启——运行时文件路径在启动时解析，
        /// 运行中切换会导致"一半读旧目录一半读新目录"，所以必须重启生效）。
        /// </summary>
        /// <param name="name">已存在的项目名</param>
        /// <returns>true=指针已改（待重启），false=项目不存在或写入失败</returns>
        public static bool SwitchTo(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
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
