using System;
using System.Threading;
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
    /// 修复 L3：实现 Splash 页面效果，程序启动时先显示进度页面，再显示主界面
    ///   【2026-08-07】启动流程暂时不再显示 Splash 页面（mForm_Progress 控件代码保留，
    ///   后续需要时可恢复 ShowSplashScreen() 调用）
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

            // 【修复 L3】Splash 页面效果：先显示进度页面，模拟启动过程
            // 【2026-08-07】暂时不再显示启动进度页，直接进入主界面，加快启动速度。
            // mForm_Progress 控件代码保留未删，后续需要时可取消下面这行注释恢复：
            // ShowSplashScreen();

            // 运行主窗体
            Application.Run(new Views.MainForm());
        }

        /// <summary>
        /// 显示 Splash 启动页面，模拟应用程序启动过程
        /// 类似 Android 的 Splash Screen 效果
        ///
        /// 【2026-08-07】当前启动流程暂不使用本方法（Main 里已注释调用）。
        /// 控件 mForm_Progress 及本方法保留未删，后续需要恢复启动进度页时，
        /// 取消 Main() 里对 ShowSplashScreen() 的注释即可。
        /// </summary>
        private static void ShowSplashScreen()
        {
            using (var splashForm = new Views.mForm_Progress())
            {
                // 显示 Splash 页面（非模态，允许后台操作）
                splashForm.Show();

                // 强制刷新界面，确保显示内容
                splashForm.Refresh();

                // 模拟启动耗时操作（如加载配置、初始化服务等）
                // 实际项目中可以在这里执行真实的初始化逻辑
                Thread.Sleep(2000);

                // 关闭 Splash 页面
                splashForm.Close();
            }
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
