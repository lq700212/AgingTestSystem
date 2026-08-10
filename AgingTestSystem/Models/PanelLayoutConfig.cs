using System;
using System.Drawing;
using System.IO;
using Newtonsoft.Json;

namespace AgingTestSystem.Models
{
    /// <summary>
    /// 工位面板布局配置（【V1.51】布局外部化）
    ///
    /// 【目的】
    /// 工位网格（WorkstationGridView）自绘时使用本配置中的所有坐标/颜色/字号/文字，
    /// 不再写死常量。这样现场微调界面（挪个框、换个色、改字号、改提示文字）只需
    /// 修改程序目录下的 PanelLayout.json 配置文件，**无需改代码、无需重新编译**。
    ///
    /// 【加载规则】
    /// 程序启动时调用 <see cref="LoadOrDefault"/>：
    /// - 若程序目录存在 PanelLayout.json → 读取并解析，失败时回退默认值；
    /// - 否则使用内置默认值（与历史版本布局完全一致）。
    /// 坐标单位均为"逻辑像素"，与 96DPI 下的设计坐标一致。
    /// 颜色统一用 "R,G,B" 字符串表示（如 "255,255,255"）。
    ///
    /// 【配置项说明】
    /// 面板内容按"设计尺寸 222×205"布局（V1.58.11 由 240×205 缩小），
    /// 72 个工位共用同一套面板模板，所以只需编辑一份配置即可同时调整所有面板。
    ///
    /// 【锚定机制总览（V1.58.13~1.58.17，改坐标前务必先读本节）】
    /// ------------------------------------------------------------------
    /// 【是什么 / 为什么】
    /// 早期面板内坐标全是"相对面板左上角"的绝对像素，改一次面板尺寸就要手算一串坐标
    /// （V1.58.11 缩面板后右空隙失控、V1.58.12 选中框溢出，均为血泪教训）。
    /// 从 V1.58.13 起引入"锚定"机制：元素通过字段声明与"面板边缘"或"其他元素"的
    /// 相对关系，加载配置时统一解析成最终坐标。以后改面板宽/高或改基准元素，
    /// 跟随元素自动联动，无需手改坐标。
    ///
    /// 【锚定字段总表】
    /// ElementRect（矩形：状态块/值框/按钮/选中框）：
    ///   RightMargin        : 右缘距面板右缘距离 → X = 面板宽 - RightMargin - Width（V1.58.13）
    ///   TopMargin          : 上缘距面板上缘距离 → Y = TopMargin（V1.58.17）
    ///   RightAlignTo       : 右缘对齐"目标元素"右缘 → X = 目标.X + 目标.Width - 自身Width（V1.58.14）
    ///   VerticalAlignTo    : Y 与 Height 取"目标元素"（上下边缘对齐）（V1.58.14）
    ///   LeftAlignTo        : 左缘对齐"目标元素"左缘 → X = 目标.X（V1.58.15）
    ///   RightToLeftAlignTo : 右缘贴合"目标元素"左缘 → X = 目标.X - 自身Width（V1.58.15）
    ///   ★ LeftAlignTo + RightToLeftAlignTo 同时设置 = 双端锚定，宽度自动推导（V1.58.15）
    /// ElementPoint（标签文字）：
    ///   LeftMargin/TopMargin : 左上角锚定，X/Y 固定距面板左/上缘（V1.58.17）
    ///   RightToLeftAlignTo   : 右缘贴合目标左缘，需配 Width（文字固定宽）（V1.58.16）
    ///   LeftAlignTo          : 左缘对齐目标左缘（V1.58.16）
    ///
    /// 【三步解析流程（<see cref="ResolveAnchors"/>，顺序不可颠倒）】
    ///   ① ResolveRight        ：直接锚定面板边缘（RightMargin / TopMargin）
    ///   ② ResolveElementAlign ：元素间锚定（RightAlignTo / VerticalAlignTo / LeftAlignTo / RightToLeftAlignTo）
    ///   ③ ResolveLabelAnchors ：标签锚定（依赖 ① 和 ② 的矩形结果）
    /// 【依赖顺序铁律】被依赖元素必须先解析：① 面板 → ② 设置按钮→右对齐组→压力框→下电 → ③ 标签。
    ///   顺序错会取到目标旧值，表现为"改了不生效 / 元素错位"。
    ///
    /// 【当前完整锚定链（链头 = View 面板右缘，链尾 = 标签列）】
    ///   View 右缘(面板内容宽 222)
    ///     └─ 设置按钮 RcSetButton(RightMargin=17)
    ///          ├─ 空闲 RcWorkState(RightAlignTo=SetButton)
    ///          │    └─ 下电 RcPower(Y/H=VerticalAlignTo:WorkState, X=LeftAlignTo:PressureValue)
    ///          ├─ 真空关 RcVacuumOpen(RightAlignTo=SetButton)
    ///          │    └─ 压力框 RcPressureValue(LeftAlignTo:SNValue + RightToLeftAlignTo:VacuumOpen 双端)
    ///          │         └─ "真空压力"标签(Width=56, RightToLeftAlignTo:PressureValue)
    ///          │              └─ SN/配方/延时开启/延时到达 标签(LeftAlignTo:LabelPressure)
    ///          ├─ SN 框 RcSNValue(RightAlignTo=SetButton)
    ///          │    └─ 延时开启/到达值框(LeftAlignTo:SNValue)
    ///          └─ 配方框 RcRecipeValue(RightAlignTo=SetButton)
    ///   View 左上角：编号 TitlePosition(LeftMargin=3, TopMargin=4)
    ///   View 右上角：选中框 RcSelectBox(RightMargin=5, TopMargin=4)
    ///
    /// 【调整指南】
    /// - 改面板宽度：改 PanelInnerWidth / PanelColumnWidth，右缘元素自动跟随，
    ///   选中框/设置按钮自动保持边距，无需手改坐标。
    /// - 挪某列/某框：改该元素的锚定字段（或改基准元素），不要手改链上元素的绝对 X。
    /// - 新增元素：优先声明锚定关系（贴到某个已有元素），保持链路完整，避免"孤岛坐标"。
    ///
    /// 【注意事项 / 常见坑】
    /// - 标签 Width（如 LabelPressurePosition.Width=56）依赖字体（微软雅黑 9pt），改字体必须同步该值。
    /// - RightMargin 与 LeftAlignTo/RightAlignTo 不要同时配（X 会被后者覆盖）；
    ///   TopMargin 与 VerticalAlignTo 同理（Y 冲突）。
    /// - PanelLayout.json 与代码默认值必须保持一致（现场改 json 不会重新编译代码，两处都可能生效）。
    /// - Y 方向中部/底部元素（设置按钮/延时行等）暂未做底部锚定（面板高 205 固定）；
    ///   若日后面板高需可调，再给设置按钮/延时到达补 BottomMargin（并保持延时两行垂直居中）。
    /// </summary>
    public class PanelLayoutConfig
    {
        // ===================== 网格尺寸 =====================

        /// <summary>单个面板单元格列宽（面板内容 222 + 左右边距各 2 + 边框余量；V1.58.11 由 245 缩小到 227）</summary>
        public int PanelColumnWidth { get; set; } = 227;

        /// <summary>单个面板单元格行高（面板内容 205 + 上下边距各 2）</summary>
        public int PanelRowHeight { get; set; } = 225;

        /// <summary>最右侧"行全选"按钮列宽</summary>
        public int RowSelectButtonColumnWidth { get; set; } = 80;

        /// <summary>面板内容设计宽（每个面板实际绘制区域宽度；V1.58.11 由 240 缩小到 222，右空隙 35→17px）</summary>
        public int PanelInnerWidth { get; set; } = 222;

        /// <summary>面板内容设计高（每个面板实际绘制区域高度）</summary>
        public int PanelInnerHeight { get; set; } = 205;

        // ===================== 字体 =====================

        /// <summary>面板正文字体名（值必须是系统已安装的字体，如 微软雅黑/宋体）</summary>
        public string FontFamily { get; set; } = "微软雅黑";

        /// <summary>面板正文文字大小（单位：磅 pt）</summary>
        public float FontSize { get; set; } = 9f;

        /// <summary>设备编号标题字体大小（磅）</summary>
        public float TitleFontSize { get; set; } = 9f;

        /// <summary>设备编号标题是否加粗</summary>
        public bool TitleFontBold { get; set; } = true;

        // ===================== 面板内容坐标（相对面板左上角） =====================
        // 【V1.58.11 撤销居中】V1.58.10 的整体右移居中效果不好（偏左观感其实是面板过宽、
        // 右侧留白太多），故 X 全部还原为 V1.58.9 布局，改为缩小面板宽度
        // （PanelInnerWidth 240→222、PanelColumnWidth 245→227）来减小右侧空隙。

        /// <summary>上电/下电状态块（V1.58.14 垂直对齐锚定空闲；V1.58.15 增加 LeftAlignTo="PressureValue"
        /// 左边缘与真空压力显示框左边缘对齐，X 自动=57）</summary>
        public ElementRect RcPower { get; set; } = new ElementRect { X = 57, Y = 29, Width = 60, Height = 23, LeftAlignTo = "PressureValue", VerticalAlignTo = "WorkState" };

        /// <summary>工作状态块（V1.58.14 右缘对齐锚定设置按钮：RightAlignTo="SetButton"，
        /// X=设置按钮右缘-自身宽；上下边缘需对齐下电时也由此基准决定）</summary>
        public ElementRect RcWorkState { get; set; } = new ElementRect { X = 145, Y = 29, Width = 60, Height = 23, RightAlignTo = "SetButton" };

        /// <summary>真空开/关状态块（V1.58.14 右缘对齐锚定设置按钮）</summary>
        public ElementRect RcVacuumOpen { get; set; } = new ElementRect { X = 145, Y = 67, Width = 60, Height = 21, RightAlignTo = "SetButton" };

        /// <summary>真空压力值框（V1.58.15 双端锚定：LeftAlignTo="SNValue"（左缘对齐 SN 框左缘）、
        /// RightToLeftAlignTo="VacuumOpen"（右缘贴合真空关左缘），宽自动=145-57=88）</summary>
        public ElementRect RcPressureValue { get; set; } = new ElementRect { X = 57, Y = 67, Width = 85, Height = 21, LeftAlignTo = "SNValue", RightToLeftAlignTo = "VacuumOpen" };

        /// <summary>SN 值框（V1.58.7 加宽 148；V1.58.14 右缘对齐锚定设置按钮）</summary>
        public ElementRect RcSNValue { get; set; } = new ElementRect { X = 57, Y = 93, Width = 148, Height = 21, RightAlignTo = "SetButton" };

        /// <summary>配方值框（V1.58.7 加宽 148；V1.58.14 右缘对齐锚定设置按钮）</summary>
        public ElementRect RcRecipeValue { get; set; } = new ElementRect { X = 57, Y = 118, Width = 148, Height = 21, RightAlignTo = "SetButton" };

        /// <summary>延时开启值框（V1.58.17 左缘锚定 SN 框：LeftAlignTo="SNValue"，X 自动=57 跟随值框列）</summary>
        public ElementRect RcDelayStartValue { get; set; } = new ElementRect { X = 57, Y = 147, Width = 80, Height = 21, LeftAlignTo = "SNValue" };

        /// <summary>延时到达值框（V1.58.17 左缘锚定 SN 框）</summary>
        public ElementRect RcDelayArriveValue { get; set; } = new ElementRect { X = 57, Y = 172, Width = 80, Height = 21, LeftAlignTo = "SNValue" };

        /// <summary>"设置"按钮区域（V1.58.13 右侧锚定 RightMargin=17，X 自动=145，左右边缘 145/205）</summary>
        public ElementRect RcSetButton { get; set; } = new ElementRect { X = 145, Y = 145, Width = 60, Height = 50, RightMargin = 17 };

        /// <summary>右上角选中指示框（V1.58.17 右上角锚定：RightMargin=5 右缘贴 View 右缘 + TopMargin=4 上缘贴顶）</summary>
        public ElementRect RcSelectBox { get; set; } = new ElementRect { X = 194, Y = 4, Width = 23, Height = 23, RightMargin = 5, TopMargin = 4 };

        /// <summary>设备编号文字位置（V1.58.17 左上角锚定：LeftMargin=3 左缘贴 View 左缘 + TopMargin=4 上缘贴顶）</summary>
        public ElementPoint TitlePosition { get; set; } = new ElementPoint { X = 3, Y = 4, LeftMargin = 3, TopMargin = 4 };

        /// <summary>静态标签"真空压力"位置（V1.58.16 右缘锚定压力框左缘：Width=56 固定文字宽，
        /// RightToLeftAlignTo="PressureValue"，X 自动=57-56=1）</summary>
        public ElementPoint LabelPressurePosition { get; set; } = new ElementPoint { X = 3, Y = 70, Width = 56, RightToLeftAlignTo = "PressureValue" };

        /// <summary>静态标签"SN:"位置（V1.58.16 左缘锚定真空压力标签：LeftAlignTo="LabelPressure"，X 自动=1）</summary>
        public ElementPoint LabelSnPosition { get; set; } = new ElementPoint { X = 3, Y = 96, LeftAlignTo = "LabelPressure" };

        /// <summary>静态标签"配方:"位置（V1.58.16 左缘锚定真空压力标签）</summary>
        public ElementPoint LabelRecipePosition { get; set; } = new ElementPoint { X = 3, Y = 121, LeftAlignTo = "LabelPressure" };

        /// <summary>静态标签"延时开启"位置（V1.58.16 左缘锚定真空压力标签）</summary>
        public ElementPoint LabelDelayStartPosition { get; set; } = new ElementPoint { X = 3, Y = 150, LeftAlignTo = "LabelPressure" };

        /// <summary>静态标签"延时到达"位置（V1.58.16 左缘锚定真空压力标签）</summary>
        public ElementPoint LabelDelayArrivePosition { get; set; } = new ElementPoint { X = 3, Y = 175, LeftAlignTo = "LabelPressure" };

        // ===================== 文字内容 =====================

        /// <summary>"设置"按钮文字</summary>
        public string SetButtonText { get; set; } = "设置";

        /// <summary>行全选按钮文字（整行全选中时显示"取消"，否则显示本文字）</summary>
        public string RowSelectAllText { get; set; } = "全选";

        /// <summary>行全选按钮文字（整行全选中时显示）</summary>
        public string RowSelectCancelText { get; set; } = "取消";

        /// <summary>选中指示符号（选中时显示，如 ✓）</summary>
        public string SelectedMarkText { get; set; } = "✓";

        /// <summary>值框内文字左内边距（px）：让文本与值框左边框留出间隔，避免"贴边"（V1.52 新增）</summary>
        public int ValueTextLeftPadding { get; set; } = 6;

        // ===================== 颜色（"R,G,B" 格式） =====================

        /// <summary>面板背景色-空闲（白）</summary>
        public string ColorNormalBackground { get; set; } = "255,255,255";

        /// <summary>面板背景色-测试中（浅黄）</summary>
        public string ColorTestingBackground { get; set; } = "255,255,224";

        /// <summary>面板背景色-故障（浅粉）</summary>
        public string ColorFaultBackground { get; set; } = "255,192,203";

        /// <summary>上电状态块背景色（绿）</summary>
        public string ColorPowerOn { get; set; } = "50,205,50";

        /// <summary>下电状态块背景色（浅灰）</summary>
        public string ColorPowerOff { get; set; } = "211,211,211";

        /// <summary>真空开状态块背景色（绿）</summary>
        public string ColorVacuumOn { get; set; } = "50,205,50";

        /// <summary>真空关状态块背景色（浅灰）</summary>
        public string ColorVacuumOff { get; set; } = "211,211,211";

        /// <summary>工作状态-故障（红）</summary>
        public string ColorWorkFault { get; set; } = "255,0,0";

        /// <summary>工作状态-繁忙/测试中（金黄）</summary>
        public string ColorWorkBusy { get; set; } = "255,215,0";

        /// <summary>工作状态-选中/已上电待测试（橙）</summary>
        public string ColorWorkSelected { get; set; } = "255,165,0";

        /// <summary>工作状态-空闲（绿）</summary>
        public string ColorWorkIdle { get; set; } = "50,205,50";

        /// <summary>"设置"按钮背景色（绿）</summary>
        public string ColorSetButton { get; set; } = "50,205,50";

        /// <summary>行全选按钮背景色（浅灰）</summary>
        public string ColorRowSelectButton { get; set; } = "211,211,211";

        /// <summary>值框背景色（白）</summary>
        public string ColorValueBox { get; set; } = "255,255,255";

        /// <summary>值框/正文文字颜色（黑）</summary>
        public string ColorText { get; set; } = "0,0,0";

        /// <summary>边框颜色（黑）</summary>
        public string ColorBorder { get; set; } = "0,0,0";

        // ===================== 加载与默认 =====================

        /// <summary>
        /// 从程序目录加载 PanelLayout.json；文件不存在或解析失败时返回内置默认配置。
        /// 默认配置与历史版本的坐标/颜色/字号完全一致，保证升级后界面不变。
        /// 加载后统一调用 <see cref="ResolveRightAnchors"/> 解析右侧锚定（V1.58.13）。
        /// </summary>
        public static PanelLayoutConfig LoadOrDefault()
        {
            string path = GetConfigPath();
            try
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                    var cfg = JsonConvert.DeserializeObject<PanelLayoutConfig>(json);
                    if (cfg != null)
                    {
                        cfg.ResolveAnchors();   // 解析面板锚定 + 元素间锚定（V1.58.13/1.58.14）
                        return cfg;
                    }
                }
            }
            catch (Exception)
            {
                // 配置损坏时静默回退默认值，避免程序无法启动
            }
            var def = new PanelLayoutConfig();
            def.ResolveAnchors();              // 内置默认同样解析，保证 X 与锚定一致
            return def;
        }

        /// <summary>
        /// 解析所有锚定关系（V1.58.14 扩展，原 ResolveRightAnchors）：
        /// 第一步：直接锚定面板右缘（<see cref="ElementRect.RightMargin"/>）→ X = PanelInnerWidth - RightMargin - Width；
        /// 第二步：元素间对齐（<see cref="ElementRect.RightAlignTo"/> 右缘对齐基准元素、<see cref="ElementRect.VerticalAlignTo"/> 垂直对齐基准元素）。
        /// 两步顺序不可颠倒：元素间对齐依赖基准元素（如设置按钮）先被面板锚定出最终 X/Y。
        /// 这样改面板宽度或基准元素后，所有跟随元素自动联动，无需手改坐标。
        /// </summary>
        public void ResolveAnchors()
        {
            // 第一步：面板右缘锚定（RightMargin）
            RcPower = ResolveRight(RcPower);
            RcWorkState = ResolveRight(RcWorkState);
            RcVacuumOpen = ResolveRight(RcVacuumOpen);
            RcPressureValue = ResolveRight(RcPressureValue);
            RcSNValue = ResolveRight(RcSNValue);
            RcRecipeValue = ResolveRight(RcRecipeValue);
            RcDelayStartValue = ResolveRight(RcDelayStartValue);
            RcDelayArriveValue = ResolveRight(RcDelayArriveValue);
            RcSetButton = ResolveRight(RcSetButton);
            RcSelectBox = ResolveRight(RcSelectBox);

            // 第二步：元素间对齐（RightAlignTo / VerticalAlignTo）
            ResolveElementAlign();

            // 第三步：标签锚定（右缘贴合目标左缘 / 左缘对齐目标），依赖矩形解析结果
            ResolveLabelAnchors();
        }

        /// <summary>单个矩形的面板边缘锚定解析（V1.58.17 含 TopMargin）：有 RightMargin 则 X = 面板宽-边距-宽，
        /// 有 TopMargin 则 Y = 边距（上缘贴顶）</summary>
        private ElementRect ResolveRight(ElementRect r)
        {
            if (r != null)
            {
                if (r.RightMargin.HasValue) r.X = PanelInnerWidth - r.RightMargin.Value - r.Width;
                if (r.TopMargin.HasValue) r.Y = r.TopMargin.Value;
            }
            return r;
        }

        /// <summary>
        /// 按 <see cref="ElementRect.RightAlignTo"/>（右缘对齐）与 <see cref="ElementRect.VerticalAlignTo"/>
        /// （垂直对齐）解析元素间锚定。基准矩形名（字符串）→ 实际属性的映射见 <see cref="GetRectByName"/>。
        /// 【注意依赖顺序】被依赖的元素必须先解析：设置按钮（RightMargin）→ 右对齐组（空闲/真空关/SN/配方）→
        /// 压力框（依赖 SN/真空关）→ 下电（依赖压力框）。顺序错会导致取到目标旧值。
        /// </summary>
        private void ResolveElementAlign()
        {
            // 依赖设置按钮（右缘跟随）
            RcWorkState = AlignSelf(RcWorkState);
            RcVacuumOpen = AlignSelf(RcVacuumOpen);
            RcSNValue = AlignSelf(RcSNValue);
            RcRecipeValue = AlignSelf(RcRecipeValue);
            // 依赖 SN/真空关（双端锚定定宽）
            RcPressureValue = AlignSelf(RcPressureValue);
            // 依赖压力框（左缘对齐）
            RcPower = AlignSelf(RcPower);
            // 其余元素无链式锚定，保持第一步结果
            AlignSelf(RcDelayStartValue);
            AlignSelf(RcDelayArriveValue);
            AlignSelf(RcSetButton);
            AlignSelf(RcSelectBox);
        }

        /// <summary>
        /// 按本矩形的锚定字段对齐到目标矩形。优先级：
        /// ① RightAlignTo（右缘=目标右缘）② LeftAlignTo+RightToLeftAlignTo（双端锚定定 X 与宽）
        /// ③ 单独 LeftAlignTo（左缘=目标左缘）④ 单独 RightToLeftAlignTo（右缘=目标左缘）
        /// ⑤ VerticalAlignTo（Y/Height=目标）。同一元素配置冲突时由用户保证（本项目配置互斥）。
        /// </summary>
        private ElementRect AlignSelf(ElementRect self)
        {
            if (self == null) return self;
            if (!string.IsNullOrEmpty(self.RightAlignTo))
            {
                var t = GetRectByName(self.RightAlignTo);
                if (t != null) self.X = t.X + t.Width - self.Width;   // 右缘对齐：自身右缘=目标右缘
            }
            if (!string.IsNullOrEmpty(self.LeftAlignTo) || !string.IsNullOrEmpty(self.RightToLeftAlignTo))
            {
                var l = string.IsNullOrEmpty(self.LeftAlignTo) ? null : GetRectByName(self.LeftAlignTo);
                var r = string.IsNullOrEmpty(self.RightToLeftAlignTo) ? null : GetRectByName(self.RightToLeftAlignTo);
                if (l != null && r != null)
                {
                    self.X = l.X;                                    // 左缘=左锚定目标左缘
                    self.Width = r.X - l.X;                          // 双端锚定：宽由两端推导
                }
                else if (l != null) self.X = l.X;
                else if (r != null) self.X = r.X - self.Width;       // 右缘贴合目标左缘
            }
            if (!string.IsNullOrEmpty(self.VerticalAlignTo))
            {
                var t = GetRectByName(self.VerticalAlignTo);
                if (t != null) { self.Y = t.Y; self.Height = t.Height; }  // 上下边缘对齐：Y 与 Height 取目标
            }
            return self;
        }

        /// <summary>矩形名称 → 属性实例（供锚定字符串引用）</summary>
        private ElementRect GetRectByName(string name)
        {
            switch (name)
            {
                case "Power": return RcPower;
                case "WorkState": return RcWorkState;
                case "VacuumOpen": return RcVacuumOpen;
                case "PressureValue": return RcPressureValue;
                case "SNValue": return RcSNValue;
                case "RecipeValue": return RcRecipeValue;
                case "DelayStartValue": return RcDelayStartValue;
                case "DelayArriveValue": return RcDelayArriveValue;
                case "SetButton": return RcSetButton;
                case "SelectBox": return RcSelectBox;
                default: return null;
            }
        }

        /// <summary>
        /// 解析标签（ElementPoint）锚定（V1.58.16/1.58.17）：真空压力标签右缘贴合压力框左缘（用 Width 推 X），
        /// 其余标签左缘对齐真空压力标签；编号 TitlePosition 用 LeftMargin/TopMargin 锚定左上角。
        /// 解析顺序：LabelPressure 在前（依赖矩形已定 X），其余在后（依赖它）。
        /// </summary>
        private void ResolveLabelAnchors()
        {
            ResolveLabel(TitlePosition);           // 左上角锚定（LeftMargin/TopMargin）
            ResolveLabel(LabelPressurePosition);   // 右缘贴合压力框左缘
            ResolveLabel(LabelSnPosition);         // 左缘对齐真空压力标签
            ResolveLabel(LabelRecipePosition);
            ResolveLabel(LabelDelayStartPosition);
            ResolveLabel(LabelDelayArrivePosition);
        }

        /// <summary>单个标签的锚定解析（V1.58.17）：LeftMargin/TopMargin 先设（边缘锚定），
        /// 然后 RightToLeftAlignTo（右缘贴合目标左缘，需 Width）> LeftAlignTo（左缘对齐目标）可覆盖 X</summary>
        private ElementPoint ResolveLabel(ElementPoint self)
        {
            if (self == null) return self;
            if (self.LeftMargin.HasValue) self.X = self.LeftMargin.Value;   // 锚定面板左缘
            if (self.TopMargin.HasValue) self.Y = self.TopMargin.Value;     // 锚定面板上缘
            if (!string.IsNullOrEmpty(self.RightToLeftAlignTo))
            {
                var tx = GetAnchorX(self.RightToLeftAlignTo);
                if (tx.HasValue && self.Width.HasValue) self.X = tx.Value - self.Width.Value;
            }
            else if (!string.IsNullOrEmpty(self.LeftAlignTo))
            {
                var tx = GetAnchorX(self.LeftAlignTo);
                if (tx.HasValue) self.X = tx.Value;
            }
            return self;
        }

        /// <summary>按名称取矩形或标签的左缘 X（锚定引用统一入口：先查矩形后查标签）</summary>
        private int? GetAnchorX(string name)
        {
            var rect = GetRectByName(name);
            if (rect != null) return rect.X;
            var label = GetLabelByName(name);
            return label?.X;
        }

        /// <summary>标签名称 → 属性实例（供锚定字符串引用）</summary>
        private ElementPoint GetLabelByName(string name)
        {
            switch (name)
            {
                case "Title": return TitlePosition;
                case "LabelPressure": return LabelPressurePosition;
                case "LabelSn": return LabelSnPosition;
                case "LabelRecipe": return LabelRecipePosition;
                case "LabelDelayStart": return LabelDelayStartPosition;
                case "LabelDelayArrive": return LabelDelayArrivePosition;
                default: return null;
            }
        }

        /// <summary>
        /// 把当前配置写入程序目录的 PanelLayout.json（供导出默认配置/备份使用）。
        /// 便于用户先导出"默认配置"，再按需修改。
        /// </summary>
        public void SaveDefault()
        {
            try
            {
                string path = GetConfigPath();
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(path, json, new System.Text.UTF8Encoding(false));
            }
            catch (Exception)
            {
                // 写入失败不阻断主流程
            }
        }

        /// <summary>配置文件路径（程序运行目录下的 PanelLayout.json）</summary>
        public static string GetConfigPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PanelLayout.json");
        }

        /// <summary>把 "R,G,B" 字符串转成 Color；解析失败返回 fallback</summary>
        public static Color ParseColor(string rgb, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(rgb)) return fallback;
            string[] parts = rgb.Split(',');
            if (parts.Length >= 3
                && int.TryParse(parts[0].Trim(), out int r)
                && int.TryParse(parts[1].Trim(), out int g)
                && int.TryParse(parts[2].Trim(), out int b))
            {
                return Color.FromArgb(Clamp(r), Clamp(g), Clamp(b));
            }
            return fallback;
        }

        /// <summary>把 Color 转成 "R,G,B" 字符串（用于导出配置）</summary>
        public static string ToColorString(Color c)
        {
            return $"{c.R},{c.G},{c.B}";
        }

        private static int Clamp(int v)
        {
            return v < 0 ? 0 : (v > 255 ? 255 : v);
        }
    }

    /// <summary>
    /// 矩形坐标（用于 JSON 序列化，替代不可直接序列化的 System.Drawing.Rectangle）
    /// </summary>
    public class ElementRect
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        /// <summary>
        /// 右侧锚定边距（px，可空）——【V1.58.13 自适应】
        /// 若设置（非 null），加载配置时 X 会被重算为 面板内容宽 - RightMargin - Width，
        /// 使该元素始终与面板右缘保持固定距离；以后调整 PanelInnerWidth 时自动跟随，
        /// 无需手改 X（解决面板缩窄后选中框溢出、右空隙失控等反复手调问题）。
        /// 未设置（null）时使用绝对 X，兼容旧配置。
        /// </summary>
        public int? RightMargin { get; set; }

        /// <summary>
        /// 顶部锚定边距（px，可空）——【V1.58.17 边缘锚定】
        /// 若设置，Y = TopMargin，即元素上边缘固定距面板上缘该距离；
        /// 配合 <see cref="RightMargin"/> 构成"右上角锚定"（如选中框）。
        /// 与 <see cref="VerticalAlignTo"/> 互斥，同一元素只配其一。
        /// </summary>
        public int? TopMargin { get; set; }

        /// <summary>
        /// 右缘对齐目标（可空）——【V1.58.14 链式锚定】
        /// 若设置（如 "SetButton"），加载时 X 被重算为 目标矩形右缘 - 自身 Width，
        /// 即与本元素与目标元素"右侧对齐"。用于"多个元素右缘对齐且共同跟随一个基准元素"。
        /// 目标名见 <see cref="PanelLayoutConfig.ResolveElementAlign"/>；未设置则忽略。
        /// </summary>
        public string RightAlignTo { get; set; }

        /// <summary>
        /// 垂直对齐目标（可空）——【V1.58.14 链式锚定】
        /// 若设置（如 "WorkState"），加载时 Y 与 Height 取目标矩形的值，
        /// 即本元素与目标元素"上边缘、下边缘对齐"。未设置则忽略。
        /// </summary>
        public string VerticalAlignTo { get; set; }

        /// <summary>
        /// 左缘对齐目标（可空）——【V1.58.15 链式锚定】
        /// 若设置（如 "SNValue"），加载时 X 取目标矩形的 X，即本元素左边缘与目标左边缘对齐。
        /// 与 <see cref="RightToLeftAlignTo"/> 同时设置时构成"双端锚定"：宽度由两端自动推导
        /// （宽 = 右锚定目标左缘 - 左锚定目标左缘），无需手设 Width。未设置则忽略。
        /// </summary>
        public string LeftAlignTo { get; set; }

        /// <summary>
        /// 右缘对齐到目标左缘（可空）——【V1.58.15 链式锚定】
        /// 若设置（如 "VacuumOpen"），加载时本元素右边缘对齐到目标矩形的左边缘
        /// （右缘贴合目标左缘）。与 <see cref="LeftAlignTo"/> 同时设置时构成"双端锚定"推导宽度。
        /// 未设置则忽略。
        /// </summary>
        public string RightToLeftAlignTo { get; set; }

        /// <summary>转成 System.Drawing.Rectangle 供绘制使用</summary>
        public Rectangle ToRectangle()
        {
            return new Rectangle(X, Y, Width, Height);
        }
    }

    /// <summary>
    /// 点坐标（用于 JSON 序列化，替代 System.Drawing.Point）
    /// </summary>
    public class ElementPoint
    {
        public int X { get; set; }
        public int Y { get; set; }

        /// <summary>
        /// 标签文字固定宽度（px，可空）——【V1.58.16 标签锚定】
        /// 仅配合 <see cref="RightToLeftAlignTo"/> 使用：右缘贴合目标左缘时
        /// X = 目标左缘 - Width。宽度依赖字体，若改字体需同步此值。
        /// </summary>
        public int? Width { get; set; }

        /// <summary>
        /// 右缘贴合目标左缘（可空）——【V1.58.16 标签锚定】
        /// 若设置（如 "PressureValue"），X = 目标矩形/标签左缘 - Width，即标签文字右边缘
        /// 对齐到目标左边缘。目标名可为矩形名或标签名，见 <see cref="PanelLayoutConfig.GetAnchorX"/>。
        /// </summary>
        public string RightToLeftAlignTo { get; set; }

        /// <summary>
        /// 左缘对齐目标（可空）——【V1.58.16 标签锚定】
        /// 若设置（如 "LabelPressure"），X = 目标矩形/标签左缘，即本标签与目标左边缘对齐。
        /// </summary>
        public string LeftAlignTo { get; set; }

        /// <summary>
        /// 左缘锚定边距（px，可空）——【V1.58.17 边缘锚定】
        /// 若设置，X = LeftMargin，即标签左边缘固定距面板左缘该距离（左上角锚定，如编号）。
        /// 与 <see cref="LeftAlignTo"/> 互斥，同一标签只配其一。
        /// </summary>
        public int? LeftMargin { get; set; }

        /// <summary>
        /// 顶部锚定边距（px，可空）——【V1.58.17 边缘锚定】
        /// 若设置，Y = TopMargin，即标签上边缘固定距面板上缘该距离。
        /// </summary>
        public int? TopMargin { get; set; }

        /// <summary>转成 System.Drawing.Point 供绘制使用</summary>
        public Point ToPoint()
        {
            return new Point(X, Y);
        }
    }
}
