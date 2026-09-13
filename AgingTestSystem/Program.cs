using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace AgingTestSystem
{
    /// <summary>
    /// 应用程序入口类
    ///
    /// 【修复说明】
    /// 修复 L1：注册全局异常处理，避免未捕获异常导致程序静默崩溃
    ///   - Application.ThreadException：UI 线程异常
    ///   - AppDomain.UnhandledException：非 UI 线程异常
    /// 修复 L2：通过 app.manifest 启用 DPI 感知，避免高 DPI 屏幕下界面模糊
    ///   （manifest 文件已添加到项目，由 .csproj 的 ApplicationManifest 引用）
    /// </summary>
    static class Program
    {
        /// <summary>
        /// 本次启动的授权判定（【V1.83】启动闸算一次，主窗体直接拿去显示标题后缀，
        /// 不重复读 WMI 指纹；试用/宽限的提醒弹框也只弹一次，不在主窗体里二次打扰）。
        /// </summary>
        internal static Services.License.LicenseResult CurrentLicense;

        /// <summary>
        /// 应用程序的主入口点
        /// </summary>
        [STAThread]
        static void Main()
        {
            // 【修复 L1】注册 UI 线程未捕获异常处理程序
            // 当 WinForms 控件事件中抛出未捕获异常时触发
            Application.ThreadException += Application_ThreadException;

            // 【修复 L1】注册非 UI 线程未捕获异常处理程序
            // 当后台线程（如 Timer 回调）中抛出未捕获异常时触发
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // 启用视觉样式
            Application.EnableVisualStyles();

            // 设置文本渲染模式为兼容模式
            Application.SetCompatibleTextRenderingDefault(false);

            // 【修复 L2】DPI 感知通过 app.manifest 声明，此处无需额外代码
            // manifest 中 <dpiAware>true</dpiAware> 使程序在高 DPI 屏幕下自动缩放

            // 【修复 L3】SunnyUI 全局 DPI 缩放开关（V1.56 推广高 DPI 适配到全部页面）
            // 作用范围：只对 SunnyUI 的 UIForm 子类生效（FanTestForm、CommunicationTestForm 等）。
            // 原理：UIBaseForm.OnShown 会调用 SetDPIScale()，当 UIStyles.DPIScale=true 时，
            //   遍历窗体内所有 IStyleInterface 控件，把字体大小除以缩放系数（DPI/96=1.5），
            //   使控件字体在高分屏下保持设计时的物理大小，避免文字溢出/错位。
            // 注意：普通 Form（MainForm、SettingsForm、LoginForm）不走 UIBaseForm.OnShown，
            //   不受此开关影响，仍走 WinForms 自带的 AutoScaleMode 缩放（已验证正常）。
            // 若将来要临时关闭，注释掉下面一行即可，不影响其余 DPI 适配。
            Sunny.UI.UIStyles.DPIScale = true;

            // 【V1.83】软件授权启动闸（一机一证，防拷机复用）。
            // 放这里而不是主窗体构造里：主窗体构造会连硬件（无设备时超时 10~15s），
            // 无授权时不该让用户空等——先验授权，拦住了直接弹授权窗/退出。
            // 参数只读 5 个键（项目名/工位数/MES开关/规则三件套），不复刻 LoadConfig
            // 全量逻辑；进主窗体后，主窗体用内存 _config 的真实值再算一次显示用。
            if (!CheckLicenseGate())
            {
                return;   // 用户放弃导入 / 仍无有效授权：直接退出，不进主界面
            }

            // 运行主窗体
            Application.Run(new Views.MainForm());
        }

        /// <summary>
        /// 授权启动闸（【V1.83】返回值 true=放行进主界面，false=退出程序）。
        ///
        /// 【流程】读 5 个键 → EnsureStartupLicense 判定 →
        /// Blocked 弹授权窗（导对了重查一次，还过不去才退出）；
        /// 试用/宽限/功能超范围只弹一次提醒（主窗体标题栏再挂后缀，不二次弹框）。
        /// </summary>
        private static bool CheckLicenseGate()
        {
            try
            {
                string project;
                int stations;
                bool mes;
                bool rules;
                ReadGateParams(out project, out stations, out mes, out rules);

                Services.License.LicenseResult res =
                    Services.License.LicenseManager.EnsureStartupLicense(project, stations, mes, rules);
                CurrentLicense = res;

                if (res == null || res.Status == Services.License.LicenseStatus.Blocked)
                {
                    string msg = res != null ? res.Message : "授权检查失败。";
                    using (var form = new Dialogs.LicenseForm(res, true))
                    {
                        Services.ThemeManager.ApplyTo(form);
                        MessageBox.Show(msg, "软件授权",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        if (form.ShowDialog() != DialogResult.OK || !form.Imported)
                        {
                            return false;   // 没导入直接退出
                        }
                    }
                    // 导入后重查一次（读刚落盘的 License.lic）：过了进，死了退。
                    ReadGateParams(out project, out stations, out mes, out rules);
                    res = Services.License.LicenseManager.EnsureStartupLicense(project, stations, mes, rules);
                    CurrentLicense = res;
                    if (res == null || !res.Allowed)
                    {
                        MessageBox.Show(res != null ? res.Message : "授权检查失败。",
                            "软件授权", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                    return true;
                }

                // 放行分支：试用将尽/过期宽限/功能超范围，各弹一次提醒（主窗体只挂标题后缀）。
                // 【V1.83】同一台机每天最多弹一次：开发/冒烟反复启动不刷屏，客户也只是每天
                // 见一次（标题栏天天挂着剩余信息，不怕漏）。弹没弹记在注册表，删了最多再弹一次。
                if (res.Status == Services.License.LicenseStatus.TrialExpiring
                    || res.Status == Services.License.LicenseStatus.GraceExpired
                    || !string.IsNullOrEmpty(res.FeatureWarning))
                {
                    if (MarkNotifiedToday())
                    {
                        string body = res.Message;
                        if (!string.IsNullOrEmpty(res.FeatureWarning))
                            body = "授权提醒：\n\n" + res.FeatureWarning;
                        MessageBox.Show(body, "软件授权",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("授权检查异常，安全起见不进主界面：\n\n" + ex.Message,
                    "软件授权", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// 启动闸读参（【V1.83】只读 5 个键，不复刻 MainForm.LoadConfig 全量）。
        /// 项目名走 ProjectProfile（静态，与主窗体同源）；工位数/MES开关跟机器
        /// 读 AppSettings；规则三件套跟项目，Policy.json 有就听项目的（与
        /// SettingsForm.GetEffectiveValue 同优先级：项目文件优先于机器缺省）。
        /// 任何一项读失败都给"最宽松但安全"的值（试用分支照样能算，不拦启动闸本身）。
        /// </summary>
        private static void ReadGateParams(
            out string project, out int stations, out bool mes, out bool rules)
        {
            project = "";
            stations = 72;
            mes = false;
            rules = false;
            try { project = Services.ProjectProfile.ActiveProfileName ?? ""; }
            catch { project = ""; }
            try
            {
                int n;
                if (int.TryParse(
                    System.Configuration.ConfigurationManager.AppSettings["TotalBarometers"], out n) && n > 0)
                {
                    stations = n;
                }
            }
            catch { }
            try
            {
                mes = string.Equals(
                    System.Configuration.ConfigurationManager.AppSettings["MesEnabled"],
                    "true", StringComparison.OrdinalIgnoreCase);
            }
            catch { }
            try
            {
                string skip = EffectiveGateValue("SkipVacuum");
                string expr = EffectiveGateValue("CompleteExpression");
                string list = EffectiveGateValue("CustomAlarmRules");
                rules = string.Equals((skip ?? "").Trim(), "true", StringComparison.OrdinalIgnoreCase)
                    || !string.IsNullOrWhiteSpace(expr)
                    || !string.IsNullOrWhiteSpace(list);
            }
            catch { }
        }

        /// <summary>启动闸单键有效值：项目文件优先，缺席回 AppSettings（与设置表同口径）。</summary>
        private static string EffectiveGateValue(string key)
        {
            try
            {
                string v = Services.ProjectPolicyStore.GetRaw(key);
                if (v != null) return v;
                return System.Configuration.ConfigurationManager.AppSettings[key] ?? "";
            }
            catch { return ""; }
        }

        /// <summary>
        /// 非阻断授权提醒"一天一次"去重（【V1.83】记 HKCU\Software\AgingTestSystem
        /// 的 LicenseLastNotify=当天日期；今天已提醒过返回 false，调用的不弹）。
        /// 删键只换再弹一次（试用的日期防线在 LicenseManager 那边，这里只是防刷屏）。
        /// </summary>
        private static bool MarkNotifiedToday()
        {
            try
            {
                string today = DateTime.Now.ToString("yyyy-MM-dd");
                const string path = @"Software\AgingTestSystem";
                using (Microsoft.Win32.RegistryKey k =
                    Microsoft.Win32.Registry.CurrentUser.CreateSubKey(path))
                {
                    if (k != null)
                    {
                        object old = k.GetValue("LicenseLastNotify");
                        if (old != null && (old.ToString() ?? "") == today) return false;
                        k.SetValue("LicenseLastNotify", today);
                        return true;
                    }
                }
            }
            catch { }
            return true;   // 注册表写不上就默许弹（宁多不弹少，提醒是生意）
        }

        /// <summary>
        /// UI 线程未捕获异常处理程序
        /// 当 WinForms 控件事件处理中抛出未捕获异常时触发
        /// 弹出错误对话框并记录日志，避免程序静默崩溃
        /// </summary>
        private static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            try
            {
                MessageBox.Show(
                    $"程序发生异常（UI 线程）：\n\n{e.Exception.GetType().Name}\n{e.Exception.Message}\n\n{e.Exception.StackTrace}",
                    "错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch
            {
                // 弹框本身失败时，最后兜底写入调试日志
                System.Diagnostics.Debug.WriteLine($"UI 线程异常处理失败: {e.Exception}");
            }
        }

        /// <summary>
        /// 非 UI 线程未捕获异常处理程序
        /// 当后台线程（如 System.Timers.Timer 回调）中抛出未捕获异常时触发
        /// </summary>
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var ex = e.ExceptionObject as Exception;
                MessageBox.Show(
                    $"程序发生异常（非 UI 线程）：\n\n{(ex?.GetType().Name ?? "未知")}\n{(ex?.Message ?? e.ExceptionObject?.ToString())}\n\n{ex?.StackTrace}",
                    "错误",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch
            {
                // 兜底写入调试日志
                System.Diagnostics.Debug.WriteLine($"非 UI 线程异常处理失败: {e.ExceptionObject}");
            }
        }
    }
}
