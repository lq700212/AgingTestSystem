using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 应用主题模式（浅色 / 深色）
    /// 【做什么】
    /// - Light（浅色）：就是软件现在设计好的样子，所有颜色保持设计原样；
    /// - Dark（深色）：容器底变深灰、文字变浅色，方便晚上/暗光车间长时间盯屏。
    /// 【怎么用（给新手的三句话）】
    /// 1. 程序启动时调一次 <see cref="LoadFromConfig"/> 读出上次保存的主题；
    /// 2. 每次打开一个窗体前调一次 <see cref="ApplyTo(Control)"/>，窗体就按当前主题着色；
    /// 3. 点"深色模式"按钮调 <see cref="Toggle"/>，会自动保存 + 把所有已打开的窗体重着色。
    /// 【为什么不用"记住原色"方案】
    /// 有些控件的颜色是运行时动态改的（比如通讯状态红/绿、权限角色名红/蓝/绿），
    /// 如果"浅色=恢复快照"，一切回浅色时会把这些运行时状态色也洗掉。
    /// 所以本类用"双向映射表"：只映射设计稿里的浅色，运行时状态色（红/绿/蓝/橙…）
    /// 两边都不在表里，切来切去永远不动。这就是"语义色保留"原则。
    /// 【配色约定】
    /// - 按钮一律不动：全项目的按钮都是语义色（绿=确认/启动、蓝=动作、红=急停/删除、
    ///   灰=取消/关闭），深浅色下都清晰可辨，动了反而丢业务含义；
    ///   SunnyUI 的 UIButton 不是原生 Button 的子类（自绘控件），
    ///   `is Button` 认不出它——这里按类型名单独走同一条"不动"分支；
    ///   语义色经 ApplyButtonColors 写入（原生走 BackColor，Sunny 走 Style=Custom+FillColor）。
    /// - 自绘控件不动：WorkstationGridView（工位大画布）、CircleButton（圆形灯）
    ///   自己管颜色，由各自的 SetDarkMode 或父容器透色跟随，不在本类递归里硬改；
    /// - MessageBox 是系统弹窗，跟随 Windows 系统主题，本类管不着，不管。
    /// </summary>
    public enum AppThemeMode
    {
        /// <summary>浅色（默认，跟现行设计稿一致）</summary>
        Light,

        /// <summary>深色（深灰底 + 浅色字）</summary>
        Dark
    }

    /// <summary>
    /// 全局主题管理器（静态单例）。
    /// 【线程说明】所有方法都应在 UI 线程调用（着色本质是改控件属性，跨线程会抛异常；
    /// 唯一例外是纯查表函数 Parse/MapXxx，可在测试里随便调）。
    /// </summary>
    public static class ThemeManager
    {
        /// <summary>App.config 里存主题的 key（值为 Light / Dark，大小写随便）</summary>
        public const string ConfigKey = "AppTheme";

        /// <summary>当前主题（默认浅色；启动时由 LoadFromConfig 覆盖）</summary>
        private static AppThemeMode _current = AppThemeMode.Light;

        /// <summary>当前主题</summary>
        public static AppThemeMode Current
        {
            get { return _current; }
        }

        /// <summary>当前是否为深色</summary>
        public static bool IsDark
        {
            get { return _current == AppThemeMode.Dark; }
        }

        /// <summary>
        /// 主题切换事件（Toggle 触发）。
        /// 目前主窗体直接调 ApplyToAllOpenForms，不订阅本事件；留给以后"常驻非模态窗自刷新"用。
        /// </summary>
        public static event EventHandler ThemeChanged;

        // ================= 深色调色板（公开，供测试断言 + 自绘控件参考） ================
        // 命名规则：Dark + 用途。取值参考 VS 深色主题，保证"底深字浅、对比够、_semantic 色不动_"。

        /// <summary>深色-窗体/容器底（#2D2D30）</summary>
        public static readonly Color DarkSurfaceBack = Color.FromArgb(45, 45, 48);

        /// <summary>深色-输入框底（#1E1E1E，比容器更深一档，框与底能分开）</summary>
        public static readonly Color DarkInputBack = Color.FromArgb(30, 30, 30);

        /// <summary>深色-正文字（#DCDCDC）</summary>
        public static readonly Color DarkText = Color.FromArgb(220, 220, 220);

        /// <summary>深色-表格体底（#252526）</summary>
        public static readonly Color DarkCellBack = Color.FromArgb(37, 37, 38);

        /// <summary>深色-表头/分组行底（#3E3E42）</summary>
        public static readonly Color DarkHeaderBack = Color.FromArgb(62, 62, 66);

        /// <summary>深色-分组标题蓝字（浅天蓝，深底上可读；浅色原值 48,119,238）</summary>
        public static readonly Color DarkGroupBlue = Color.FromArgb(120, 180, 255);

        /// <summary>深色-提示蓝字（浅色原值 30,80,160）</summary>
        public static readonly Color DarkHintBlue = Color.FromArgb(120, 180, 255);

        /// <summary>深色- slate 灰蓝字（浅色原值 DarkSlateGray）</summary>
        public static readonly Color DarkSlateText = Color.FromArgb(176, 196, 222);

        /// <summary>深色-深蓝字提亮（浅色原值 DarkBlue）</summary>
        public static readonly Color DarkBrightBlue = Color.FromArgb(140, 180, 255);

        /// <summary>深色-中灰字（浅色原值 80,80,80）</summary>
        public static readonly Color DarkGrayText = Color.FromArgb(170, 170, 170);

        /// <summary>深色-表格线（浅色原值 ControlDark 系）</summary>
        public static readonly Color DarkGridLine = Color.FromArgb(90, 90, 90);

        /// <summary>深色-SunnyUI描边（浅色原中性灰描边映射至此）</summary>
        public static readonly Color DarkRectBorder = Color.FromArgb(110, 110, 110);

        // ================= 读写配置 =================

        /// <summary>
        /// 解析配置字符串为主题（给新手：App.config 里手写错也不怕，兜底浅色）。
        /// "Dark"（忽略大小写与首尾空格）= 深色，其余一切（含 null/空/乱写）= 浅色。
        /// </summary>
        public static AppThemeMode Parse(string raw)
        {
            if (!string.IsNullOrWhiteSpace(raw)
                && raw.Trim().Equals("Dark", StringComparison.OrdinalIgnoreCase))
            {
                return AppThemeMode.Dark;
            }
            return AppThemeMode.Light;
        }

        /// <summary>
        /// 启动时从 App.config 读主题（读不到/读坏默认浅色，不抛异常，保证程序一定能起）。
        /// 注意：读的是"运行目录 exe.config"，IDE 里改 App.config 后要重新生成才会带过去。
        /// </summary>
        public static void LoadFromConfig()
        {
            try
            {
                string raw = System.Configuration.ConfigurationManager.AppSettings[ConfigKey];
                _current = Parse(raw);
            }
            catch
            {
                _current = AppThemeMode.Light;
            }
        }

        /// <summary>
        /// 把主题写回运行目录 exe.config（只动 AppTheme 这一项，其它配置原样保留）。
        /// </summary>
        /// <param name="mode">要保存的主题</param>
        /// <returns>true=保存成功；false=保存失败（调用方记日志提示即可，不阻断切换）</returns>
        public static bool SaveToConfig(AppThemeMode mode)
        {
            try
            {
                var config = System.Configuration.ConfigurationManager.OpenExeConfiguration(
                    System.Configuration.ConfigurationUserLevel.None);
                var setting = config.AppSettings.Settings[ConfigKey];
                string value = mode == AppThemeMode.Dark ? "Dark" : "Light";
                if (setting == null)
                {
                    config.AppSettings.Settings.Add(ConfigKey, value);
                }
                else
                {
                    setting.Value = value;
                }
                config.Save(System.Configuration.ConfigurationSaveMode.Modified);
                System.Configuration.ConfigurationManager.RefreshSection("appSettings");
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 设置当前主题（内存值）。save=true 时同时写配置文件。
        /// 测试用 save=false，避免污染测试运行目录的 config。
        /// </summary>
        public static void SetMode(AppThemeMode mode, bool save)
        {
            _current = mode;
            if (save)
            {
                SaveToConfig(mode);
            }
        }

        /// <summary>
        /// 切换主题并保存 + 通知（主窗体"深色模式"按钮调它）。
        /// 保存失败也不回滚——内存主题照切，只是下次启动恢复旧值，调用方记条日志即可。
        /// </summary>
        /// <returns>切换后的主题</returns>
        public static AppThemeMode Toggle()
        {
            AppThemeMode next = IsDark ? AppThemeMode.Light : AppThemeMode.Dark;
            _current = next;
            SaveToConfig(next);
            try
            {
                if (ThemeChanged != null)
                {
                    ThemeChanged(null, EventArgs.Empty);
                }
            }
            catch
            {
                // 订阅者异常不能把切换流程炸掉
            }
            return next;
        }

        // ================= 双向映射表 =================
        // 每一对"浅色值 ↔ 深色值"必须一一对应（ round-trip 精确还原），
        // 做法是浅→深、深→浅各一张字典。查不到的一律"保持不动"（语义色保留）。

        /// <summary>单张映射表（一一对应的一对字典）</summary>
        private sealed class ColorMap
        {
            public readonly Dictionary<Color, Color> ToDark = new Dictionary<Color, Color>();
            public readonly Dictionary<Color, Color> ToLight = new Dictionary<Color, Color>();

            /// <summary>
            /// 归一化颜色（给新手：System.Drawing.Color 看似"红绿蓝值相同就是同一颜色"，
            /// 其实字典比较时还认"名字"——Black 和 ControlText 都是纯黑(0,0,0)，
            /// 但名字不同，字典会当成两个键！控件属性吐出来的经常是 ControlText/Window 这类
            /// 系统名，而表里登记的是 Black/White，不归一化就永远查不到。
            /// 归一化 = 去掉名字只留 ARGB 值；Empty（=没设颜色）保持 Empty 不动，
            /// 因为 FromArgb(0,0,0,0) 会变成 Transparent，那是另一个意思）。
            /// </summary>
            private static Color Norm(Color c)
            {
                if (c.IsEmpty) return Color.Empty;
                return Color.FromArgb(c.A, c.R, c.G, c.B);
            }

            /// <summary>
            /// 登记一对互映颜色（浅色 ↔ 深色）。
            /// 深→浅登记"先到先得"：比如 Empty/Black/(48,48,48) 三个浅色都映到同一个深色字，
            /// 还原时统一回 Empty（Empty 本来就是"跟着父容器走"，渲染出来和黑字一样），
            /// 保证 round-trip 结果唯一、测试可断言。
            /// </summary>
            public void Add(Color light, Color dark)
            {
                Color l = Norm(light);
                Color d = Norm(dark);
                ToDark[l] = d;
                if (!ToLight.ContainsKey(d)) ToLight[d] = l;
            }

            /// <summary>查表映射；查不到返回原值（保持不动）</summary>
            public Color Map(Color c, bool toDark)
            {
                Color key = Norm(c);
                Color hit;
                if (toDark)
                {
                    if (ToDark.TryGetValue(key, out hit)) return hit;
                }
                else
                {
                    if (ToLight.TryGetValue(key, out hit)) return hit;
                }
                return c;
            }
        }

        // —— 容器底（窗体/面板/分组/Tab页/状态栏…）：每档浅灰各给一个深色档，保证精确还原 ——
        private static readonly ColorMap _containerBackMap = BuildContainerBackMap();

        private static ColorMap BuildContainerBackMap()
        {
            var m = new ColorMap();
            m.Add(SystemColors.Control, DarkSurfaceBack);                       // 主窗体默认底
            m.Add(Color.White, DarkCellBack);                                   // 各对话框白色底
            m.Add(Color.FromArgb(245, 245, 245), Color.FromArgb(55, 55, 58));    // 通讯测试行标签底
            m.Add(Color.FromArgb(243, 249, 255), Color.FromArgb(48, 58, 84));    // 通讯测试 Tab 页底
            m.Add(Color.FromArgb(245, 248, 255), Color.FromArgb(52, 64, 92));    // 设置窗搜索条底
            m.Add(Color.FromArgb(237, 243, 253), DarkHeaderBack);                // 设置窗表头系浅蓝底
            return m;
        }

        // —— 文字色（标签/分组标题/输入框字/工具条字…）：白字两边都不动（本来就是亮的） ——
        private static readonly ColorMap _foreMap = BuildForeMap();

        private static ColorMap BuildForeMap()
        {
            var m = new ColorMap();
            // 注意：Color.Empty（=没显式设颜色，跟着父控件走）也登记：
            // 深色下给显式浅字，浅色下还原 Empty 继续跟随——这样运行时动态设的红/绿等
            // 状态色因为不在表里，切主题时原样保留（这正是"语义色保留"想要的）。
            m.Add(Color.Empty, DarkText);
            m.Add(Color.Black, DarkText);                    // 含 ControlText/WindowText（ARGB 都是 0,0,0，同一键）
            m.Add(Color.FromArgb(48, 48, 48), DarkText);     // 正文深灰
            m.Add(Color.DarkSlateGray, DarkSlateText);       // 通讯测试列头
            m.Add(Color.FromArgb(30, 80, 160), DarkHintBlue); // 各窗蓝色提示字
            m.Add(Color.DarkBlue, DarkBrightBlue);           // 改密窗用户名
            m.Add(Color.FromArgb(80, 80, 80), DarkGrayText);  // 布局编辑器说明条
            return m;
        }

        // —— 输入框底（文本框/下拉/数字框/列表）：浅色档各给深色档 ——
        private static readonly ColorMap _inputBackMap = BuildInputBackMap();

        private static ColorMap BuildInputBackMap()
        {
            var m = new ColorMap();
            m.Add(Color.White, DarkInputBack);
            m.Add(SystemColors.Window, DarkInputBack);       // ARGB 与 White 相同，字典自动合并为同一键
            m.Add(SystemColors.Control, DarkSurfaceBack);
            m.Add(Color.LightGray, Color.FromArgb(70, 70, 70)); // ID绑定"批号"只读灰底→深灰（仍比输入底浅一档，保留"只读"暗示）
            return m;
        }

        // —— 表格体颜色（SettingsForm 等 DataGridView 的单元格，Selection 系有意跳过：蓝底白字两边通吃） ——
        private static readonly ColorMap _cellBackMap = BuildCellBackMap();

        private static ColorMap BuildCellBackMap()
        {
            var m = new ColorMap();
            m.Add(Color.Empty, DarkCellBack);                // 默认样式（没显式设）按白底处理
            m.Add(Color.White, DarkCellBack);
            m.Add(Color.FromArgb(237, 243, 253), DarkHeaderBack); // 分组标题行/表头浅蓝底
            return m;
        }

        private static readonly ColorMap _cellForeMap = BuildCellForeMap();

        private static ColorMap BuildCellForeMap()
        {
            var m = new ColorMap();
            m.Add(Color.Empty, DarkText);
            m.Add(Color.Black, DarkText);
            m.Add(Color.FromArgb(48, 48, 48), DarkText);
            m.Add(Color.FromArgb(48, 119, 238), DarkGroupBlue); // 分组标题蓝字→浅天蓝
            return m;
        }

        // —— 对外公开的查表函数（测试直接断言它们，保证映射表改坏立刻被回归抓住） ——

        /// <summary>容器底映射（窗体/面板/分组/Tab页/状态栏）</summary>
        public static Color MapContainerBack(Color c, bool toDark) { return _containerBackMap.Map(c, toDark); }

        /// <summary>文字色映射（标签/分组标题/输入框字/工具条字）</summary>
        public static Color MapForeColor(Color c, bool toDark) { return _foreMap.Map(c, toDark); }

        /// <summary>输入框底映射</summary>
        public static Color MapInputBack(Color c, bool toDark) { return _inputBackMap.Map(c, toDark); }

        /// <summary>单元格底映射</summary>
        public static Color MapGridCellBack(Color c, bool toDark) { return _cellBackMap.Map(c, toDark); }

        /// <summary>单元格字映射</summary>
        public static Color MapGridCellFore(Color c, bool toDark) { return _cellForeMap.Map(c, toDark); }

        // ================= 递归着色 =================

        /// <summary>
        /// SunnyUI 扩展颜色属性名（FillColor/描边/标题栏）。反射读写，属性不存在就跳过，
        /// 所以 SunnyUI 将来改名/删属性也不会把程序炸掉。
        /// </summary>
        private static readonly string[] _sunnyFillProps = new string[] { "FillColor" };

        /// <summary>
        /// 按"当前主题"给整个控件树着色（窗体打开前调一次；切换主题时对所有已打开窗体各调一次）。
        /// 空引用/已释放控件直接跳过，单个控件异常不中断整树（记 Debug，不断整个界面）。
        /// </summary>
        /// <param name="root">窗体或容器（传窗体最常见）</param>
        public static void ApplyTo(Control root)
        {
            if (root == null || root.IsDisposed || root.Disposing) return;
            bool toDark = IsDark;
            try { root.SuspendLayout(); } catch { /* 极个别控件不支持挂起布局，忽略 */ }
            try
            {
                ApplyRecursive(root, toDark);
            }
            finally
            {
                try { root.ResumeLayout(false); } catch { }
                try { root.Invalidate(true); } catch { }
            }
        }

        /// <summary>把当前主题应用到所有已打开的窗体（切换按钮调它；逐个 try，保证一个坏窗体不连累其它）</summary>
        public static void ApplyToAllOpenForms()
        {
            List<Form> open = new List<Form>();
            try
            {
                // FormCollection 是只读集合（没有 CopyTo），先逐个抄到 List 再刷，
                // 避免着色过程中有窗体开关导致枚举失效。
                foreach (Form f in Application.OpenForms)
                {
                    if (f != null) open.Add(f);
                }
            }
            catch
            {
                return;
            }
            foreach (Form f in open)
            {
                try { ApplyTo(f); } catch { /* 单个窗体失败只记 Debug，继续刷其它窗体 */ }
            }
        }

        /// <summary>中性浅色判定：RGB 三通道都 ≥235（白/Control/245系浅灰…），SunnyUI 填充色用它识别"默认浅底"</summary>
        private static bool IsLightNeutral(Color c)
        {
            return c.A == 255 && c.R >= 235 && c.G >= 235 && c.B >= 235;
        }

        /// <summary>
        /// 递归入口：先给自己着色，再钻进子控件 + 工具条目。
        /// 【跳过名单】（自绘/自己管颜色，硬改反而坏事，见类头"配色约定"）：
        /// WorkstationGridView（调自己的 SetDarkMode）、CircleButton（圆形灯自绘）、
        /// UIScrollBar（SunnyUI 滚动条自绘）。
        /// </summary>
        private static void ApplyRecursive(Control c, bool toDark)
        {
            if (c == null || c.IsDisposed || c.Disposing) return;

            string typeName = c.GetType().FullName ?? "";
            if (typeName == "AgingTestSystem.Views.WorkstationGridView")
            {
                // 自绘大画布：颜色全在内部字段 + 缓存画刷里，调它自己的开关（反射调用，
                // 避免 Services 层反向依赖 Views 层；方法不存在就跳过）。
                try
                {
                    var m = c.GetType().GetMethod("SetDarkMode");
                    if (m != null) m.Invoke(c, new object[] { toDark });
                }
                catch { }
                return; // 它没有需要递归的子控件，直接返回
            }
            if (typeName.EndsWith("CircleButton")
                || typeName.EndsWith("UIScrollBar"))
            {
                return;
            }

            try
            {
                // SunnyUI 自绘控件的类型判定（它们大多不是原生控件的子类，
                // `is Button/TextBox` 认不出，必须按类型名走分支，否则会被容器表误染）：
                // - Sunny.UI.UIButton：自绘按钮，走"按钮不动"分支（语义色保护）；
                // - Sunny.UI.UITextBox / Sunny.UI.UIComboBox：自绘输入框，走输入分支；
                // - Sunny.UI.UILabel：继承原生 Label，自动命中下面的 Label 分支，不用管；
                // - Sunny.UI.UIGroupBox：走容器分支（与原生 GroupBox 同待遇）。
                bool isSunnyButton = string.Equals(typeName, "Sunny.UI.UIButton", StringComparison.Ordinal);
                bool isSunnyInput = string.Equals(typeName, "Sunny.UI.UITextBox", StringComparison.Ordinal)
                    || string.Equals(typeName, "Sunny.UI.UIComboBox", StringComparison.Ordinal);

                // 标准按钮（确认绿/动作蓝/急停红/取消灰…）是语义色，两边都清晰，原样保留。
                // CheckBox/RadioButton 例外：它们只是勾选项，字要跟着主题走。
                if ((c is Button && !(c is CheckBox) && !(c is RadioButton)) || isSunnyButton)
                {
                    // 只钻子控件（一般没有），颜色不动
                }
                else if (c is DataGridView)
                {
                    ApplyGrid((DataGridView)c, toDark);
                }
                else if (c is TextBoxBase || c is ComboBox || c is ListBox
                    || c is NumericUpDown || c is DateTimePicker || isSunnyInput)
                {
                    // 输入类：底、字都按表走（只读灰底 LightGray 也在表里→深灰，保留"只读"暗示）
                    c.BackColor = _inputBackMap.Map(c.BackColor, toDark);
                    c.ForeColor = _foreMap.Map(c.ForeColor, toDark);
                    ApplySunnyFill(c, toDark);
                }
                else if (c is Label || c is CheckBox || c is RadioButton)
                {
                    // 标签/勾选：字按表走；底只有"显式浅底"才变透明跟随父容器
                    // （Label 默认本来就是 Transparent；rowLabel 那种 245 灰底→透明后自动透出深底）。
                    // 浅色还原时透明标签保持透明即可（浅色父容器下透明标签本来就是这个观感，
                    // 白底标签变透明后肉眼也看不出区别），所以这里只在 toDark 时动手。
                    c.ForeColor = _foreMap.Map(c.ForeColor, toDark);
                    if (toDark && c.BackColor != Color.Transparent && IsLightNeutral(c.BackColor))
                    {
                        c.BackColor = Color.Transparent;
                    }
                    ApplySunnyFill(c, toDark);
                }
                else if (c is ToolStrip)
                {
                    // 状态栏/工具条：条底按容器表，条上每个文字项按字表（红/绿状态字不在表里→保留）
                    c.BackColor = _containerBackMap.Map(c.BackColor, toDark);
                    c.ForeColor = _foreMap.Map(c.ForeColor, toDark);
                    ApplyToolStripItems((ToolStrip)c, toDark);
                }
                else if (c is Form)
                {
                    c.BackColor = _containerBackMap.Map(c.BackColor, toDark);
                    c.ForeColor = _foreMap.Map(c.ForeColor, toDark);
                    ApplySunnyFormChrome(c, toDark);
                }
                else
                {
                    // 其余容器（面板/分组/Tab页/SplitContainer/TableLayout…）：底按容器表、字按字表
                    Color newBack = _containerBackMap.Map(c.BackColor, toDark);
                    if (newBack != c.BackColor) c.BackColor = newBack;
                    Color newFore = _foreMap.Map(c.ForeColor, toDark);
                    if (newFore != c.ForeColor) c.ForeColor = newFore;
                    ApplySunnyFill(c, toDark);
                }
            }
            catch
            {
                // 单个控件着色失败（比如句柄期特殊状态）不中断整树
            }

            // 钻进子控件（快照式拷贝数组：着色过程不增删控件，但防第三方控件捣乱）
            Control[] children;
            try
            {
                children = new Control[c.Controls.Count];
                c.Controls.CopyTo(children, 0);
            }
            catch
            {
                return;
            }
            foreach (Control child in children)
            {
                ApplyRecursive(child, toDark);
            }
        }

        /// <summary>
        /// 工具条上的文字项（状态栏"设备数量/在线/扫码枪…"）：非宿主项按字表映射，
        /// 宿主项（里面嵌了控件的）钻进去递归。红/绿状态字不在表里→保留。
        /// </summary>
        private static void ApplyToolStripItems(ToolStrip strip, bool toDark)
        {
            ToolStripItem[] items;
            try
            {
                items = new ToolStripItem[strip.Items.Count];
                strip.Items.CopyTo(items, 0);
            }
            catch
            {
                return;
            }
            foreach (ToolStripItem item in items)
            {
                if (item == null) continue;
                try
                {
                    ToolStripControlHost host = item as ToolStripControlHost;
                    if (host != null && host.Control != null)
                    {
                        ApplyRecursive(host.Control, toDark);
                    }
                    else
                    {
                        item.ForeColor = _foreMap.Map(item.ForeColor, toDark);
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// 表格着色（标准 DataGridView 与 SunnyUI UIDataGridView 同走这里）：
        /// - 表格底/表格线：原值快照保存（运行时没人改它们），深色给深底/深线，浅色精确还原；
        /// - 表头：深色下关闭 EnableHeadersVisualStyles（否则 Windows 系统主题强制画浅色头），
        ///   浅色时恢复开关原值（借 grid.Tag 记，表格的 Tag 平时没人用）；
        /// - 单元格：整体默认样式 + 表头样式 + 每列样式 + 逐行逐格自定义样式全部过表，
        ///   Selection 系有意跳过（蓝底白字选中态深浅两边通吃；SettingsForm 分组行选中不变色是它自己画的，不管）；
        /// - 查不到的语义色（如红/绿字）原样保留。
        /// </summary>
        private static void ApplyGrid(DataGridView grid, bool toDark)
        {
            // 表格底/表格线：原值快照保存（运行时没人改它们，快照还原精确无副作用）。
            // 深色映射：Empty（默认）/ ControlDark（默认）/ AppWorkspace（DataGridView 默认底）
            // / ControlLight 系浅灰 → 深底；显式设计底（SettingsForm 的白）按容器表走；
            // 其它语义色保持不动。
            try
            {
                Color cur = grid.BackgroundColor;
                if (toDark)
                {
                    RememberExtra(grid, "BackgroundColor", cur);
                    Color next;
                    if (cur == Color.Empty
                        || cur.ToArgb() == SystemColors.ControlDark.ToArgb()
                        || cur.ToArgb() == SystemColors.AppWorkspace.ToArgb()
                        || cur.ToArgb() == SystemColors.ControlLight.ToArgb())
                    {
                        next = DarkSurfaceBack;
                    }
                    else
                    {
                        next = _containerBackMap.Map(cur, true);
                        if (next.ToArgb() == cur.ToArgb() && IsLightNeutral(cur)) next = DarkSurfaceBack;
                    }
                    grid.BackgroundColor = next;
                }
                else
                {
                    Color orig;
                    if (TryRecallExtra(grid, "BackgroundColor", out orig)) grid.BackgroundColor = orig;
                }
            }
            catch { }
            try
            {
                Color cur = grid.GridColor;
                if (toDark)
                {
                    RememberExtra(grid, "GridColor", cur);
                    Color next;
                    if (cur == Color.Empty || cur.ToArgb() == SystemColors.ControlDark.ToArgb())
                    {
                        next = DarkGridLine;
                    }
                    else
                    {
                        next = cur;
                        if (IsLightNeutral(cur)) next = DarkGridLine;
                    }
                    grid.GridColor = next;
                }
                else
                {
                    Color orig;
                    if (TryRecallExtra(grid, "GridColor", out orig)) grid.GridColor = orig;
                }
            }
            catch { }

            try
            {
                if (toDark)
                {
                    if (grid.EnableHeadersVisualStyles)
                    {
                        grid.Tag = "HeadersVisualStylesOn"; // 借 Tag 记一下，浅色时恢复（Tag 平时没人用表格的）
                        grid.EnableHeadersVisualStyles = false;
                    }
                }
                else
                {
                    if (grid.Tag as string == "HeadersVisualStylesOn")
                    {
                        grid.EnableHeadersVisualStyles = true;
                        grid.Tag = null;
                    }
                }
            }
            catch { }

            MapDataGridViewStyle(grid.DefaultCellStyle, toDark);
            MapDataGridViewStyle(grid.ColumnHeadersDefaultCellStyle, toDark);
            MapDataGridViewStyle(grid.RowHeadersDefaultCellStyle, toDark);
            MapDataGridViewStyle(grid.RowsDefaultCellStyle, toDark);
            MapDataGridViewStyle(grid.AlternatingRowsDefaultCellStyle, toDark);

            try
            {
                foreach (DataGridViewColumn col in grid.Columns)
                {
                    if (col != null) MapDataGridViewStyle(col.DefaultCellStyle, toDark);
                }
            }
            catch { }

            try
            {
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (row == null || row.IsNewRow) continue;
                    MapDataGridViewStyle(row.DefaultCellStyle, toDark);
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        if (cell != null) MapDataGridViewStyle(cell.Style, toDark);
                    }
                }
            }
            catch { }
        }

        /// <summary>单个单元格样式过表（只动底/字，Selection 系跳过，见 ApplyGrid 注释）</summary>
        private static void MapDataGridViewStyle(DataGridViewCellStyle style, bool toDark)
        {
            if (style == null) return;
            try
            {
                style.BackColor = _cellBackMap.Map(style.BackColor, toDark);
                style.ForeColor = _cellForeMap.Map(style.ForeColor, toDark);
            }
            catch { }
        }

        /// <summary>
        /// 给按钮写语义色（SunnyUI 换肤配套）：
        /// 原生 Button 走 BackColor/ForeColor；SunnyUI UIButton 是自绘的，
        /// BackColor 画不出来，必须 Style=Custom + FillColor/RectColor/ForeColor。
        /// </summary>
        public static void ApplyButtonColors(Control btn, Color back, Color fore)
        {
            if (btn == null || btn.IsDisposed || btn.Disposing) return;
            try
            {
                string typeName = btn.GetType().FullName ?? "";
                if (string.Equals(typeName, "Sunny.UI.UIButton", StringComparison.Ordinal))
                {
                    TrySetEnumProperty(btn, "Style", "Sunny.UI.UIStyle, SunnyUI", "Custom");
                    TrySetColorProperty(btn, "FillColor", back);
                    TrySetColorProperty(btn, "RectColor", back);
                    TrySetColorProperty(btn, "ForeColor", fore);
                    return;
                }
                btn.BackColor = back;
                btn.ForeColor = fore;
            }
            catch { }
        }

        /// <summary>反射写枚举属性（程序集限定名解析，失败吞掉：版本差异是正常的）。</summary>
        private static void TrySetEnumProperty(object obj, string propName,
            string enumTypeName, string valueName)
        {
            try
            {
                var p = obj.GetType().GetProperty(propName);
                if (p == null || !p.CanWrite) return;
                Type enumType = Type.GetType(enumTypeName);
                if (enumType == null || !enumType.IsEnum) return;
                if (p.PropertyType != enumType) return;
                object value = Enum.Parse(enumType, valueName);
                p.SetValue(obj, value, null);
            }
            catch { }
        }

        /// <summary>
        /// 读按钮的"实际显示色"（下拉菜单项继承宿主颜色用）：
        /// 原生 Button 读 BackColor/ForeColor；SunnyUI UIButton 是自绘的，
        /// BackColor 只是底衬，真实显示色在 FillColor/ForeColor。
        /// </summary>
        public static void GetEffectiveButtonColors(Control btn, out Color back, out Color fore)
        {
            back = Color.Empty;
            fore = Color.Empty;
            if (btn == null || btn.IsDisposed || btn.Disposing) return;
            try
            {
                string typeName = btn.GetType().FullName ?? "";
                if (string.Equals(typeName, "Sunny.UI.UIButton", StringComparison.Ordinal))
                {
                    Color fill;
                    if (TryGetColorProperty(btn, "FillColor", out fill)) back = fill;
                    Color fg;
                    if (TryGetColorProperty(btn, "ForeColor", out fg)) fore = fg;
                    return;
                }
                back = btn.BackColor;
                fore = btn.ForeColor;
            }
            catch { }
        }

        /// <summary>
        /// SunnyUI 控件的填充色（FillColor）：只动"中性浅底"（白/浅灰系→深输入底），
        /// 饱和语义色（绿/红/蓝按钮…）原样保留。用快照记原值，浅色精确还原。
        /// </summary>
        private static void ApplySunnyFill(Control c, bool toDark)
        {
            if (!IsSunnyControl(c)) return;
            foreach (string prop in _sunnyFillProps)
            {
                try
                {
                    Color cur;
                    if (!TryGetColorProperty(c, prop, out cur)) continue;
                    if (toDark)
                    {
                        RememberExtra(c, prop, cur);
                        if (IsLightNeutral(cur) && cur != Color.Transparent)
                        {
                            TrySetColorProperty(c, prop, DarkInputBack);
                        }
                    }
                    else
                    {
                        Color orig;
                        if (TryRecallExtra(c, prop, out orig))
                        {
                            TrySetColorProperty(c, prop, orig);
                        }
                    }
                }
                catch { }
            }
            // SunnyUI 描边：浅中性灰→深灰；快照还原
            try
            {
                Color cur;
                if (TryGetColorProperty(c, "RectColor", out cur))
                {
                    if (toDark)
                    {
                        RememberExtra(c, "RectColor", cur);
                        if (IsLightNeutral(cur)) TrySetColorProperty(c, "RectColor", DarkRectBorder);
                    }
                    else
                    {
                        Color orig;
                        if (TryRecallExtra(c, "RectColor", out orig)) TrySetColorProperty(c, "RectColor", orig);
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// SunnyUI 窗体（UIForm）的标题栏：深色下标题底跟窗体走深、标题字走浅字；
        /// 原值快照保存，浅色精确还原（反射读写，属性不存在就当没这回事）。
        /// </summary>
        private static void ApplySunnyFormChrome(Control form, bool toDark)
        {
            if (!IsSunnyControl(form)) return;
            MapExtraWithDarkConst(form, "TitleColor", DarkSurfaceBack, toDark);
            MapExtraWithDarkConst(form, "TitleForeColor", DarkText, toDark);
        }

        /// <summary>扩展颜色属性：深色→固定深色值；浅色→快照原值（无快照就不动）</summary>
        private static void MapExtraWithDarkConst(Control c, string prop, Color darkConst, bool toDark)
        {
            try
            {
                Color cur;
                if (!TryGetColorProperty(c, prop, out cur)) return;
                if (toDark)
                {
                    RememberExtra(c, prop, cur);
                    if (cur != darkConst) TrySetColorProperty(c, prop, darkConst);
                }
                else
                {
                    Color orig;
                    if (TryRecallExtra(c, prop, out orig)) TrySetColorProperty(c, prop, orig);
                }
            }
            catch { }
        }

        /// <summary>是否 SunnyUI 控件（命名空间 Sunny.UI 开头）</summary>
        private static bool IsSunnyControl(Control c)
        {
            try
            {
                string ns = c.GetType().Namespace ?? "";
                return ns == "Sunny.UI" || ns.StartsWith("Sunny.UI.", StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        // —— SunnyUI 扩展属性原值快照（ConditionalWeakTable：控件释放时快照自动跟着释放，不泄漏） ——
        private sealed class ExtraSnapshot
        {
            public readonly Dictionary<string, Color> Values = new Dictionary<string, Color>();
        }

        private static readonly ConditionalWeakTable<Control, ExtraSnapshot> _extraTable =
            new ConditionalWeakTable<Control, ExtraSnapshot>();

        /// <summary>记住扩展属性原值（只记第一次，保证"原值"永远是设计稿颜色）</summary>
        private static void RememberExtra(Control c, string prop, Color cur)
        {
            try
            {
                ExtraSnapshot snap = _extraTable.GetOrCreateValue(c);
                lock (snap)
                {
                    if (!snap.Values.ContainsKey(prop)) snap.Values[prop] = cur;
                }
            }
            catch { }
        }

        /// <summary>取回扩展属性原值（没记过返回 false，调用方保持不动）</summary>
        private static bool TryRecallExtra(Control c, string prop, out Color orig)
        {
            orig = Color.Empty;
            try
            {
                ExtraSnapshot snap;
                if (!_extraTable.TryGetValue(c, out snap)) return false;
                lock (snap)
                {
                    return snap.Values.TryGetValue(prop, out orig);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>反射读颜色属性（不存在/不可读写/非Color类型一律返回 false，不抛异常）</summary>
        private static bool TryGetColorProperty(object obj, string name, out Color value)
        {
            value = Color.Empty;
            try
            {
                var p = obj.GetType().GetProperty(name);
                if (p == null || !p.CanRead || !p.CanWrite) return false;
                if (p.PropertyType != typeof(Color)) return false;
                value = (Color)p.GetValue(obj, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>反射写颜色属性（失败吞掉：不同 SunnyUI 小版本属性有差异是正常的）</summary>
        private static void TrySetColorProperty(object obj, string name, Color value)
        {
            try
            {
                var p = obj.GetType().GetProperty(name);
                if (p == null || !p.CanRead || !p.CanWrite) return;
                if (p.PropertyType != typeof(Color)) return;
                p.SetValue(obj, value, null);
            }
            catch { }
        }
    }
}
