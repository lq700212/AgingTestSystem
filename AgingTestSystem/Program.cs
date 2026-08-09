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

            // 运行主窗体
            Application.Run(new Views.MainForm());
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
