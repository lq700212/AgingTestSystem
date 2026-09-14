using System;
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

            // 【V1.87】授权与 HJVision 同源：无启动闸（HJVision 亦无）。
            // 新设备/过期由主窗 HashTimer 每小时提醒一次，不阻断启动与生产。

            // 运行主窗体
            Application.Run(new Views.MainForm());
        }

        /// <summary>
        /// UI 线程未捕获异常处理程序
        /// 当 WinForms 控件事件处理中抛出未捕获异常时触发
        /// 弹出错误对话框并记录日志，避免程序静默崩溃
        ///
        /// 【V1.88.9 调试部署】以前只弹窗，客户点掉就无据可查。现在多做两件事：
        /// 1. 先把完整异常落盘到 Logs\Crash_*.log（CrashLogWriter.Write，内部全程兜底不抛）；
        /// 2. 再往 AppLog 追加一行摘要（崩溃时间+文件路径），只拷 AppLog 也能顺藤摸瓜。
        /// 弹框文本末尾带上 crash 文件路径，请客户把该文件发回排障。
        /// </summary>
        private static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            // 先落盘（顺序不能反：弹框是阻塞的，客户可能直接×掉进程，盘必须先落）。
            // 两个 try 各自独立：落盘失败不能挡住弹框，弹框失败不能挡住落盘。
            string crashPath = null;
            try
            {
                crashPath = AgingTestSystem.Services.CrashLogWriter.Write("UI线程", e.Exception);
            }
            catch
            {
                crashPath = null;
            }
            try
            {
                string where = crashPath ?? "（崩溃日志写入失败：磁盘满/无权限，请截图本框）";
                AgingTestSystem.Services.AppLogFileWriter.Write(
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [崩溃] UI线程未捕获异常：{e.Exception?.Message}（详情见 {where}）\r\n");
            }
            catch
            {
                // AppLog 追加失败静默：主角（crash 文件+弹框）不受影响。
            }
            try
            {
                MessageBox.Show(
                    $"程序发生异常（UI 线程）：\n\n{e.Exception.GetType().Name}\n{e.Exception.Message}\n\n{e.Exception.StackTrace}"
                    + (crashPath == null ? "" : $"\n\n崩溃日志已保存到：\n{crashPath}\n请把该文件发给厂家排查。"),
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
        ///
        /// 【V1.88.9 调试部署】与 UI 线程处理同口径：先落盘 Crash_*.log，再补 AppLog 摘要行，
        /// 弹框带文件路径。注意 ex 可能为 null（ExceptionObject 装着非 Exception 对象甚至 null），
        /// 解析交给 CrashLogWriter.Write（它三种情况都吃得下），这里只做空安全拼接。
        /// </summary>
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            string crashPath = null;
            try
            {
                crashPath = AgingTestSystem.Services.CrashLogWriter.Write("非UI线程", e.ExceptionObject);
            }
            catch
            {
                crashPath = null;
            }
            try
            {
                string where = crashPath ?? "（崩溃日志写入失败：磁盘满/无权限，请截图本框）";
                string msg = ex != null ? ex.Message : (e.ExceptionObject?.ToString() ?? "（无异常信息）");
                AgingTestSystem.Services.AppLogFileWriter.Write(
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [崩溃] 非UI线程未捕获异常：{msg}（详情见 {where}）\r\n");
            }
            catch
            {
                // AppLog 追加失败静默：主角（crash 文件+弹框）不受影响。
            }
            try
            {
                MessageBox.Show(
                    $"程序发生异常（非 UI 线程）：\n\n{(ex?.GetType().Name ?? "未知")}\n{(ex?.Message ?? e.ExceptionObject?.ToString())}\n\n{ex?.StackTrace}"
                    + (crashPath == null ? "" : $"\n\n崩溃日志已保存到：\n{crashPath}\n请把该文件发给厂家排查。"),
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
