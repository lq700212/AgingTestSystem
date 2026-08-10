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
    /// 【锚定机制总览（V1.58.13~1.58.19，改坐标前务必先读本节）】
    /// ------------------------------------------------------------------
    /// 【是什么 / 为什么】
    /// 早期面板内坐标全是"相对面板左上角"的绝对像素，改一次面板尺寸就要手算一串坐标
    /// （V1.58.11 缩面板后右空隙失控、V1.58.12 选中框溢出，均为血泪教训）。
    /// 从 V1.58.13 起引入"锚定"机制：元素通过字段声明与"面板边缘"或"其他元素"的
    /// 相对关系，加载配置时统一解析成最终坐标。以后改面板宽/高或改基准元素，
    /// 跟随元素自动联动，无需手改坐标。
    /// ★【最重要原则：锚定只改"声明关系"，不改"当前位置"】加锚定时，各间距/边距字段取
    ///   当前布局的实际空隙，解析结果与未加锚定前**完全一致**（视觉零变化）。锚定带来的
    ///   是"以后改面板尺寸/基准元素时自动联动"，而不是立刻把元素挪到贴边/居中的新位置。
    ///
    /// 【锚定字段总表】
    /// ElementRect（矩形：状态块/值框/按钮/选中框）：
    ///   RightMargin        : 右缘距面板右缘距离 → X = 面板宽 - RightMargin - Width（V1.58.13）
    ///   TopMargin          : 上缘距面板上缘距离 → Y = TopMargin（V1.58.17）
    ///   BottomMargin       : 下缘距面板下缘距离 → Y = 面板高 - BottomMargin - Height（V1.58.19）
    ///   RightAlignTo       : 右缘对齐"目标元素"右缘 → X = 目标.X + 目标.Width - 自身Width（V1.58.14）
    ///   VerticalAlignTo    : Y 与 Height 取"目标元素"（上下边缘对齐）（V1.58.14）
    ///   LeftAlignTo        : 左缘对齐"目标元素"左缘 → X = 目标.X（V1.58.15）
    ///   RightToLeftAlignTo : 右缘贴合"目标元素"左缘 → X = 目标.X - 自身Width（V1.58.15）
    ///   ★ LeftAlignTo + RightToLeftAlignTo 同时设置 = 双端锚定，宽度自动推导（V1.58.15）
    ///   RightToLeftGap    : 右缘与目标左缘的间隙 px（双端/单独贴合时右缘留 Gap，默认 0 紧贴）（V1.58.19）
    ///   BottomToTopAlignTo : 下缘贴在"目标元素"上边缘上方 → Y = 目标.Y - 自身H - BottomToTopGap（V1.58.19 垂直链）
    ///   BottomToTopGap     : 上述间距 px（Gap=0 紧贴；取当前实际空隙可保持布局不变）（V1.58.19）
    ///   VerticalCenterAlignTo : 垂直居中对齐目标 → Y = 目标.Y+(目标H-自身H)/2（V1.58.19）
    ///   CenterOffsetY       : 垂直居中时的额外偏移 px（正下负上，配合对称分布）（V1.58.19）
    /// ElementPoint（标签文字）：
    ///   LeftMargin/TopMargin : 左上角锚定，X/Y 固定距面板左/上缘（V1.58.17）
    ///   RightToLeftAlignTo   : 右缘贴合目标左缘，需配 Width（文字固定宽）（V1.58.16）
    ///   LeftAlignTo          : 左缘对齐目标左缘（V1.58.16）
    ///   VerticalCenterAlignTo : 垂直居中对齐目标矩形（用 LabelTextHeight 算文字高）（V1.58.19）
    ///   VerticalCenterOffset : 垂直居中时的额外偏移 px（本项目统一 -1，文字顶部留 3px）（V1.58.19）
    ///
    /// 【三步解析流程（<see cref="ResolveAnchors"/>，顺序不可颠倒）】
    ///   ① ResolveRight        ：直接锚定面板边缘（RightMargin / TopMargin / BottomMargin）
    ///   ② ResolveElementAlign ：元素间锚定（RightAlignTo / VerticalAlignTo / LeftAlignTo /
    ///                            RightToLeftAlignTo / BottomToTopAlignTo / VerticalCenterAlignTo）
    ///   ③ ResolveLabelAnchors ：标签锚定（依赖 ① 和 ② 的矩形结果）
    /// 【依赖顺序铁律】被依赖元素必须先解析：
    ///   ① 面板 → ② 设置按钮(BottomMargin) → 配方(BottomToTop:SetButton) → SN(BottomToTop:RecipeValue)
    ///   → 真空关/压力框(BottomToTop:SNValue) → 空闲/下电 → 延时两行(VerticalCenter:SetButton) → ③ 标签。
    ///   顺序错会取到目标旧值，表现为"改了不生效 / 元素错位"。
    ///
    /// 【当前完整锚定链（V1.58.19 横纵双向 + V1.58.20 内容居中，解析结果如下）】
    ///   纵链头 = View 面板下缘(内容高 205)：
    ///     └─ 设置按钮 RcSetButton(RightMargin=9 + BottomMargin=10 → X=222-9-60=153、Y=205-10-50=145，
    ///        右缘 213 距面板右缘 9px，下缘距面板底 10px)
    ///          ├─ 配方框 RcRecipeValue(右缘:SetButton→X=65 + 下缘贴设置上缘、Gap=6 → Y=145-21-6=118)
    ///          │    └─ SN 框 RcSNValue(右缘:SetButton→X=213-148=65 + 下缘贴配方上缘、Gap=4 → Y=118-21-4=93)
    ///          │         ├─ 真空关 RcVacuumOpen(右缘:SetButton→X=153 + 下缘贴 SN 上缘、Gap=5 → Y=93-21-5=67)
    ///          │         │    └─ 压力框 RcPressureValue(双端:SN(65)/VacuumOpen(153)+RightToLeftGap=3 → 宽85 右缘150，
    ///          │         │        与真空关左缘 153 留 3px 间隙；下缘贴 SN 上缘、Gap=5 → Y=67)
    ///          │         └─ 空闲 RcWorkState(右缘:SetButton→X=153；Y=29 固定顶区)
    ///          │              └─ 下电 RcPower(Y/H=VerticalAlignTo:WorkState, X=LeftAlignTo:PressureValue→X=65)
    ///          ├─ 延时开启/到达 值框(左缘:SNValue→X=65 + 垂直居中于设置按钮，CenterOffsetY=-12/+13)
    ///          │         → 两行中心 157.5/182.5 关于按钮中心 170 对称，Y=147/172
    ///          └─ 各标签(横向锚定 + VerticalCenterAlignTo 各自框，offset=-1)：真空压力/SN/配方/延时开启/延时到达
    ///   横链头 = View 面板右缘(内容宽 222)：选中框(RightMargin=5,TopMargin=2→X=194,Y=2)；
    ///   面板左缘：编号(LeftMargin=9,TopMargin=4→X=9)、标签列(X=9)
    ///   【V1.58.20 内容居中】左边界元素（编号/标签列 LeftMargin=9，X=9）与右边界元素
    ///   （设置按钮 RightMargin=9，右缘=213）关于面板中线（222/2=111）对称：
    ///   左留白 9 = 右留白 222-213=9，面板内内容整体水平居中。改 PanelInnerWidth 时
    ///   左右各留 9px 边距、中间元素按锚定自动联动，始终居中。
    ///
    /// 【调整指南】
    /// - 改面板宽度：改 PanelInnerWidth / PanelColumnWidth，右缘元素自动跟随，无需手改坐标。
    /// - 改面板高度：改 PanelInnerHeight，设置按钮按 BottomMargin 贴底自动下移，整个纵链
    ///   （配方/SN/真空关/压力/延时/标签）按各自 Gap/偏移自动联动。
    /// - 【V1.58.20 居中调节】想让面板内容保持"左右对称居中"，只需保证 编号/标签列 LeftMargin
    ///   == 设置按钮 RightMargin（当前都是 9）。改 PanelInnerWidth 后左右各留该边距、中间元素
    ///   按锚定自动联动，始终居中；想整体加/减左右留白，同步改这两个值即可。
    /// - 想整体让下排元素更紧凑/更松：改各 BottomToTopGap；想只挪某个框：改该元素锚定字段或基准元素。
    /// - 新增元素：优先声明锚定关系（贴到某个已有元素），保持链路完整，避免"孤岛坐标"。
    ///
    /// 【注意事项 / 常见坑】
    /// - 标签 Width（如 LabelPressurePosition.Width=56）与 LabelTextHeight（默认 12）都依赖字体
    ///   （微软雅黑 9pt），改字体字号必须同步这两个值。
    /// - Y 互斥组：TopMargin / BottomMargin / VerticalAlignTo / BottomToTopAlignTo / VerticalCenterAlignTo
    ///   同一元素只配其一（后配覆盖 Y）；X 互斥组：RightMargin / RightAlignTo / LeftAlignTo / RightToLeftAlignTo 同理。
    /// - VerticalCenterAlignTo 用整数除法 (目标H-自身H)/2，会有 0.5px 截断误差；对称分布两条框时
    ///   需用一正一负且相差 1 的偏移（如 -12/+13）抵消，保证与手工坐标完全一致。
    /// - PanelLayout.json 与代码默认值必须保持一致；**升级到 V1.58.19 时，现场若已有旧版 json，
    ///   其中没有 BottomMargin 等新字段（反序列化为 null）→ 垂直锚定不生效**，需手动补充新字段或删除 json
    ///   让程序重新导出默认配置。
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

        /// <summary>静态标签文字高度（px，默认 12）——【V1.58.19 垂直锚定】</summary>
        /// 用于标签垂直居中对齐（<see cref="ElementPoint.VerticalCenterAlignTo"/>）时计算 Y：
        /// Y = 目标中心 - LabelTextHeight / 2。此值依赖字体（9pt 微软雅黑约为 12px），
        /// 若改字体字号需同步此值（同标签 Width 依赖字体的道理）。
        public int LabelTextHeight { get; set; } = 12;

        // ===================== 面板内容坐标（相对面板左上角） =====================
        // 【V1.58.11 撤销居中】V1.58.10 的整体右移居中效果不好（偏左观感其实是面板过宽、
        // 右侧留白太多），故 X 全部还原为 V1.58.9 布局，改为缩小面板宽度
        // （PanelInnerWidth 240→222、PanelColumnWidth 245→227）来减小右侧空隙。

        /// <summary>上电/下电状态块（V1.58.14 垂直对齐锚定空闲；V1.58.15 增加 LeftAlignTo="PressureValue"
        /// 左边缘与真空压力显示框左边缘对齐，X 自动=65（V1.58.20 随压力框内容居中））</summary>
        public ElementRect RcPower { get; set; } = new ElementRect { X = 65, Y = 29, Width = 60, Height = 23, LeftAlignTo = "PressureValue", VerticalAlignTo = "WorkState" };

        /// <summary>工作状态块（V1.58.14 右缘对齐锚定设置按钮：RightAlignTo="SetButton"，
        /// X=设置按钮右缘(213)-自身宽=153；上下边缘需对齐下电时也由此基准决定。
        /// 【V1.58.20】跟随设置按钮右缘右移 8px（153），保持右缘对齐、内容居中）</summary>
        public ElementRect RcWorkState { get; set; } = new ElementRect { X = 153, Y = 29, Width = 60, Height = 23, RightAlignTo = "SetButton" };

        /// <summary>真空开/关状态块（V1.58.14 右缘对齐锚定设置按钮；V1.58.19 下缘锚定
        /// BottomToTopAlignTo="SNValue"+BottomToTopGap=5，保持原 Y=67 不变：93-21-5=67。
        /// 【V1.58.20】右缘跟随设置按钮→153（与工作状态块右缘对齐、内容居中））</summary>
        public ElementRect RcVacuumOpen { get; set; } = new ElementRect { X = 153, Y = 67, Width = 60, Height = 21, RightAlignTo = "SetButton", BottomToTopAlignTo = "SNValue", BottomToTopGap = 5 };

        /// <summary>真空压力值框（V1.58.15 双端锚定：LeftAlignTo="SNValue"（左缘对齐 SN 框左缘）、
        /// RightToLeftAlignTo="VacuumOpen"（右缘贴合真空关左缘）；V1.58.19 补 RightToLeftGap=3，
        /// 右缘距真空关左缘 3px，宽自动=153-65-3=85（恢复 V1.58.9 的 3px 间距，不再紧贴）；
        /// 下缘锚定 BottomToTopAlignTo="SNValue"+BottomToTopGap=5，保持原 Y=67 不变。
        /// 【V1.58.20】左缘跟随 SN 框→65、右缘贴真空关左缘(153)，整体随内容居中）</summary>
        public ElementRect RcPressureValue { get; set; } = new ElementRect { X = 65, Y = 67, Width = 85, Height = 21, LeftAlignTo = "SNValue", RightToLeftAlignTo = "VacuumOpen", RightToLeftGap = 3, BottomToTopAlignTo = "SNValue", BottomToTopGap = 5 };

        /// <summary>SN 值框（V1.58.7 加宽 148；V1.58.14 右缘对齐锚定设置按钮；V1.58.19 下缘锚定
        /// BottomToTopAlignTo="RecipeValue"+BottomToTopGap=4，保持原 Y=93 不变：118-21-4=93。
        /// 【V1.58.20】右缘跟随设置按钮(213)→X=213-148=65，随内容居中）</summary>
        public ElementRect RcSNValue { get; set; } = new ElementRect { X = 65, Y = 93, Width = 148, Height = 21, RightAlignTo = "SetButton", BottomToTopAlignTo = "RecipeValue", BottomToTopGap = 4 };

        /// <summary>配方值框（V1.58.7 加宽 148；V1.58.14 右缘对齐锚定设置按钮；V1.58.19 下缘锚定
        /// BottomToTopAlignTo="SetButton"+BottomToTopGap=6，保持原 Y=118 不变：145-21-6=118。
        /// 【V1.58.20】右缘跟随设置按钮(213)→X=213-148=65，随内容居中。
        /// 面板高改变时设置按钮下移，配方框以间距 6 跟随其上缘联动）</summary>
        public ElementRect RcRecipeValue { get; set; } = new ElementRect { X = 65, Y = 118, Width = 148, Height = 21, RightAlignTo = "SetButton", BottomToTopAlignTo = "SetButton", BottomToTopGap = 6 };

        /// <summary>延时开启值框（V1.58.17 左缘锚定 SN 框：LeftAlignTo="SNValue"；V1.58.19 垂直居中于
        /// 设置按钮 VerticalCenterAlignTo="SetButton"+CenterOffsetY=-12，保持原 Y=147 不变：
        /// 145+14-12=147，即框中心 157.5 位于按钮中心 170 上方 12.5px，与到达框对称分布。
        /// 【V1.58.20】左缘跟随 SN 框→65，随内容居中）</summary>
        public ElementRect RcDelayStartValue { get; set; } = new ElementRect { X = 65, Y = 147, Width = 80, Height = 21, LeftAlignTo = "SNValue", VerticalCenterAlignTo = "SetButton", CenterOffsetY = -12 };

        /// <summary>延时到达值框（V1.58.17 左缘锚定 SN 框；V1.58.19 垂直居中于设置按钮
        /// VerticalCenterAlignTo="SetButton"+CenterOffsetY=13，保持原 Y=172 不变：145+14+13=172，
        /// 即框中心 182.5 位于按钮中心 170 下方 12.5px，与开启框对称分布。
        /// 说明：因整数除法截断 0.5px，两行偏移取 -12/+13 才能与 V1.58.18 坐标完全一致。
        /// 【V1.58.20】左缘跟随 SN 框→65，随内容居中）</summary>
        public ElementRect RcDelayArriveValue { get; set; } = new ElementRect { X = 65, Y = 172, Width = 80, Height = 21, LeftAlignTo = "SNValue", VerticalCenterAlignTo = "SetButton", CenterOffsetY = 13 };

        /// <summary>"设置"按钮区域（V1.58.13 右侧锚定 RightMargin=9，X 自动=153；V1.58.19 下缘锚定
        /// BottomMargin=10，保持原 Y=145 不变：205-10-50=145，即下缘 195 距面板下缘 10px。
        /// 【V1.58.20 内容居中】RightMargin 由 17→9：按钮右缘=213，与左侧标签左缘(9)关于面板中线
        /// (222/2=111) 对称 → 面板内容整体水平居中（左留白 9 = 右留白 9）。
        /// 是"垂直链"的链头：面板高改变时按钮自动贴底跟随；改面板宽时按钮按右留白 9 自动联动）</summary>
        public ElementRect RcSetButton { get; set; } = new ElementRect { X = 153, Y = 145, Width = 60, Height = 50, RightMargin = 9, BottomMargin = 10 };

        /// <summary>右上角选中指示框（V1.58.17 右上角锚定：RightMargin=5 右缘贴 View 右缘 + TopMargin=2 上缘贴顶。
        /// 【V1.58.20】TopMargin 由 4→2：选中框上移 2px，底缘 25 与下方工作状态块（"空闲"，上缘 29）
        /// 的垂直间隔由 2px 加大到 4px，避免"选中框贴着空闲块上边缘"的拥挤感。
        /// 注意：选中框属"右上角元素"，不参与内容居中平移，保持右缘距面板右缘 5px）</summary>
        public ElementRect RcSelectBox { get; set; } = new ElementRect { X = 194, Y = 2, Width = 23, Height = 23, RightMargin = 5, TopMargin = 2 };

        /// <summary>设备编号文字位置（V1.58.17 左上角锚定：LeftMargin=9 左缘距面板左缘 9px + TopMargin=4 上缘贴顶。
        /// 【V1.58.20 内容居中】LeftMargin 由 3→9：编号左缘与标签列统一为 X=9，作为面板内容最左元素，
        /// 与设置按钮右缘(213)关于面板中线对称，构成"左留白 9 = 右留白 9"的居中布局）</summary>
        public ElementPoint TitlePosition { get; set; } = new ElementPoint { X = 9, Y = 4, LeftMargin = 9, TopMargin = 4 };

        /// <summary>静态标签"真空压力"位置（V1.58.16 右缘锚定压力框左缘：Width=56 固定文字宽，
        /// RightToLeftAlignTo="PressureValue"，X 自动=65-56=9（V1.58.20 压力框左缘居中后随之居中）；
        /// V1.58.19 垂直居中于压力框
        /// VerticalCenterAlignTo="PressureValue"+VerticalCenterOffset=-1，保持原 Y=70 不变：67+4-1=70）</summary>
        public ElementPoint LabelPressurePosition { get; set; } = new ElementPoint { X = 9, Y = 70, Width = 56, RightToLeftAlignTo = "PressureValue", VerticalCenterAlignTo = "PressureValue", VerticalCenterOffset = -1 };

        /// <summary>静态标签"SN:"位置（V1.58.16 左缘锚定真空压力标签→X=9；V1.58.19 垂直居中于 SN 框，保持原 Y=96 不变：93+4-1=96）</summary>
        public ElementPoint LabelSnPosition { get; set; } = new ElementPoint { X = 9, Y = 96, LeftAlignTo = "LabelPressure", VerticalCenterAlignTo = "SNValue", VerticalCenterOffset = -1 };

        /// <summary>静态标签"配方:"位置（V1.58.16 左缘锚定真空压力标签→X=9；V1.58.19 垂直居中于配方框，保持原 Y=121 不变：118+4-1=121）</summary>
        public ElementPoint LabelRecipePosition { get; set; } = new ElementPoint { X = 9, Y = 121, LeftAlignTo = "LabelPressure", VerticalCenterAlignTo = "RecipeValue", VerticalCenterOffset = -1 };

        /// <summary>静态标签"延时开启"位置（V1.58.16 左缘锚定真空压力标签→X=9；V1.58.19 垂直居中于延时开启框，保持原 Y=150 不变：147+4-1=150）</summary>
        public ElementPoint LabelDelayStartPosition { get; set; } = new ElementPoint { X = 9, Y = 150, LeftAlignTo = "LabelPressure", VerticalCenterAlignTo = "DelayStartValue", VerticalCenterOffset = -1 };

        /// <summary>静态标签"延时到达"位置（V1.58.16 左缘锚定真空压力标签→X=9；V1.58.19 垂直居中于延时到达框，保持原 Y=175 不变：172+4-1=175）</summary>
        public ElementPoint LabelDelayArrivePosition { get; set; } = new ElementPoint { X = 9, Y = 175, LeftAlignTo = "LabelPressure", VerticalCenterAlignTo = "DelayArriveValue", VerticalCenterOffset = -1 };

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
        /// 加载后统一调用 <see cref="ResolveAnchors"/> 解析面板边缘/元素间锚定（V1.58.13~1.58.19）。
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
        /// 第一步：直接锚定面板边缘（<see cref="ElementRect.RightMargin"/> / TopMargin / BottomMargin）
        /// → X = PanelInnerWidth - RightMargin - Width、Y = TopMargin 或 PanelInnerHeight - BottomMargin - Height；
        /// 第二步：元素间对齐（RightAlignTo / VerticalAlignTo / BottomToTopAlignTo / VerticalCenterAlignTo 等，
        /// 见 <see cref="ResolveElementAlign"/>，含自下而上垂直链）。
        /// 三步顺序不可颠倒：元素间对齐依赖基准元素（如设置按钮）先被面板锚定出最终 X/Y。
        /// 这样改面板宽度/高度或基准元素后，所有跟随元素自动联动，无需手改坐标。
        /// </summary>
        public void ResolveAnchors()
        {
            // 第一步：面板边缘锚定（RightMargin/TopMargin/BottomMargin）
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

            // 第二步：元素间对齐（含垂直链，顺序见 ResolveElementAlign 注释）
            ResolveElementAlign();

            // 第三步：标签锚定（右缘贴合目标左缘 / 左缘对齐目标 / 垂直居中），依赖矩形解析结果
            ResolveLabelAnchors();
        }

        /// <summary>单个矩形的面板边缘锚定解析（V1.58.17 含 TopMargin、V1.58.19 含 BottomMargin）：
        /// 有 RightMargin 则 X = 面板宽-边距-宽（右缘贴面板右缘）；
        /// 有 TopMargin 则 Y = 边距（上缘贴顶）；有 BottomMargin 则 Y = 面板高-边距-高（下缘贴底）。</summary>
        private ElementRect ResolveRight(ElementRect r)
        {
            if (r != null)
            {
                if (r.RightMargin.HasValue) r.X = PanelInnerWidth - r.RightMargin.Value - r.Width;
                if (r.TopMargin.HasValue) r.Y = r.TopMargin.Value;
                if (r.BottomMargin.HasValue) r.Y = PanelInnerHeight - r.BottomMargin.Value - r.Height;
            }
            return r;
        }

        /// <summary>
        /// 按 <see cref="ElementRect.RightAlignTo"/>（右缘对齐）、<see cref="ElementRect.VerticalAlignTo"/>
        /// （垂直对齐）、<see cref="ElementRect.BottomToTopAlignTo"/>（下缘贴目标上缘）、
        /// <see cref="ElementRect.VerticalCenterAlignTo"/>（垂直居中）解析元素间锚定。
        /// 基准矩形名（字符串）→ 实际属性的映射见 <see cref="GetRectByName"/>。
        /// 【注意依赖顺序（V1.58.19 扩展为垂直链）】被依赖的元素必须先解析：
        /// 设置按钮（BottomMargin 下缘贴面板底）→ 配方（下缘贴设置按钮上缘）→
        /// SN（下缘贴配方上缘）→ 真空关/压力框（下缘贴 SN 上缘）→
        /// 空闲/下电（右缘/左缘跟随）→ 延时两行（垂直居中于设置按钮）。
        /// 顺序错会导致取到目标旧值、元素错位。
        /// </summary>
        private void ResolveElementAlign()
        {
            // ① 依赖设置按钮（BottomMargin 已在第一步解析出 Y=面板底-高）
            //    配方：下边缘贴设置按钮上边缘（垂直链第二环）
            RcRecipeValue = AlignSelf(RcRecipeValue);
            // ② 依赖配方：SN 下边缘贴配方上边缘
            RcSNValue = AlignSelf(RcSNValue);
            // ③ 依赖 SN：真空关/压力框下边缘贴 SN 上边缘
            RcVacuumOpen = AlignSelf(RcVacuumOpen);
            RcPressureValue = AlignSelf(RcPressureValue);
            // ④ 依赖压力框/真空关（左缘对齐/右缘跟随设置按钮）
            RcWorkState = AlignSelf(RcWorkState);
            RcPower = AlignSelf(RcPower);
            // ⑤ 延时两行：垂直居中于设置按钮（CenterOffsetY 对称分布）
            RcDelayStartValue = AlignSelf(RcDelayStartValue);
            RcDelayArriveValue = AlignSelf(RcDelayArriveValue);
            // ⑥ 其余元素无链式锚定，保持第一步结果
            AlignSelf(RcSetButton);
            AlignSelf(RcSelectBox);
        }

        /// <summary>
        /// 按本矩形的锚定字段对齐到目标矩形。优先级：
        /// ① RightAlignTo（右缘=目标右缘）② LeftAlignTo+RightToLeftAlignTo（双端锚定定 X 与宽）
        /// ③ 单独 LeftAlignTo（左缘=目标左缘）④ 单独 RightToLeftAlignTo（右缘=目标左缘）
        /// ⑤ VerticalAlignTo（Y/Height=目标）⑥ BottomToTopAlignTo（Y=目标上缘-自身高，贴目标上方）
        /// ⑦ VerticalCenterAlignTo（Y=目标中心-自身高/2，垂直居中；可配 CenterOffsetY 偏移）
        /// 同一元素配置冲突时由用户保证（本项目配置互斥）。
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
                int gap = self.RightToLeftGap ?? 0;                        // 右缘与目标左缘的间隙（默认 0 紧贴）
                if (l != null && r != null)
                {
                    self.X = l.X;                                          // 左缘=左锚定目标左缘
                    self.Width = r.X - l.X - gap;                          // 双端锚定：宽由两端推导，右缘留 gap 间隙
                }
                else if (l != null) self.X = l.X;
                else if (r != null) self.X = r.X - self.Width - gap;       // 右缘贴合目标左缘，留 gap 间隙
            }
            if (!string.IsNullOrEmpty(self.VerticalAlignTo))
            {
                var t = GetRectByName(self.VerticalAlignTo);
                if (t != null) { self.Y = t.Y; self.Height = t.Height; }  // 上下边缘对齐：Y 与 Height 取目标
            }
            if (!string.IsNullOrEmpty(self.BottomToTopAlignTo))
            {
                var t = GetRectByName(self.BottomToTopAlignTo);
                if (t != null) self.Y = t.Y - self.Height - (self.BottomToTopGap ?? 0);  // 下缘=目标上缘-间距（叠加在目标上方）
            }
            if (!string.IsNullOrEmpty(self.VerticalCenterAlignTo))
            {
                var t = GetRectByName(self.VerticalCenterAlignTo);
                if (t != null) self.Y = t.Y + (t.Height - self.Height) / 2 + (self.CenterOffsetY ?? 0);  // 垂直居中
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

        /// <summary>单个标签的锚定解析（V1.58.17 边缘锚定、V1.58.19 垂直居中）：
        /// 先 LeftMargin/TopMargin（面板边缘锚定），然后 RightToLeftAlignTo（右缘贴合目标左缘，需 Width）
        /// > LeftAlignTo（左缘对齐目标）可覆盖 X；VerticalCenterAlignTo 用文字高垂直居中对齐目标框</summary>
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
            if (!string.IsNullOrEmpty(self.VerticalCenterAlignTo))
            {
                var rect = GetRectByName(self.VerticalCenterAlignTo);
                if (rect != null) self.Y = rect.Y + (rect.Height - LabelTextHeight) / 2 + (self.VerticalCenterOffset ?? 0);
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

        /// <summary>
        /// 右缘与目标左缘的间隙（px，可空）——【V1.58.19 补充，恢复原设计间距】
        /// 仅配合 <see cref="RightToLeftAlignTo"/>（含与 <see cref="LeftAlignTo"/> 组合的双端锚定）使用：
        /// - 双端锚定时：Width = 右锚定目标左缘 - 左锚定目标左缘 - Gap（右缘不再紧贴目标左缘，留 Gap 空隙）；
        /// - 单独 RightToLeftAlignTo 时：X = 目标左缘 - 自身 Width - Gap。
        /// 【背景】V1.58.9 真空压力框与真空关之间本有 3px 间隙（宽 85、右缘 142 vs 真空关左 145）；
        /// V1.58.15 双端锚定把宽算成 145-57=88 导致紧贴，本字段用于恢复该间隙。
        /// 未设置（null）时 Gap=0（紧贴），兼容旧配置。
        /// </summary>
        public int? RightToLeftGap { get; set; }

        /// <summary>
        /// 底部锚定边距（px，可空）——【V1.58.19 垂直锚定】
        /// 若设置（非 null），加载时 Y 会被重算为 面板内容高 - BottomMargin - Height，
        /// 即该元素**下边缘距面板下缘**该距离（"自下而上"锚定）。以后调整
        /// PanelInnerHeight 时自动跟随，无需手改 Y。
        /// 【注意：保持布局不变】BottomMargin 取"当前下边缘到面板下缘的实际距离"即可
        /// 让位置完全不变（如设置按钮当前 Y=145、H=50、面板高 205 → BottomMargin=10）。
        /// 与 <see cref="TopMargin"/>、<see cref="VerticalAlignTo"/>、<see cref="BottomToTopAlignTo"/>、
        /// <see cref="VerticalCenterAlignTo"/> 互斥，同一元素只配其一（Y 会被后配的覆盖）。
        /// 未设置（null）时使用绝对 Y，兼容旧配置。
        /// </summary>
        public int? BottomMargin { get; set; }

        /// <summary>
        /// 下边缘贴目标上边缘（可空）——【V1.58.19 垂直锚定】
        /// 若设置（如 "SetButton"），加载时 Y = 目标.Y - 自身.Height - (<see cref="BottomToTopGap"/> ?? 0)，
        /// 即本元素**下边缘紧贴目标元素上边缘上方**（"叠加在目标上方"，可留间距）。
        /// 用于构建"自下而上的垂直链"：如 配方下边缘贴设置按钮上边缘 → SN 下边缘贴配方上边缘。
        /// 【注意：保持布局不变】配 <see cref="BottomToTopGap"/> 为"当前下边缘到目标上边缘的实际距离"
        /// 即可让位置完全不变（如配方框当前 Y=118、H=21、设置按钮 Y=145 → Gap=145-118-21=6）。
        /// 与 <see cref="TopMargin"/>、<see cref="BottomMargin"/>、<see cref="VerticalAlignTo"/>、
        /// <see cref="VerticalCenterAlignTo"/> 互斥。目标名见 <see cref="PanelLayoutConfig.GetRectByName"/>。
        /// </summary>
        public string BottomToTopAlignTo { get; set; }

        /// <summary>
        /// 下缘贴目标上缘时的间距（px，可空）——【V1.58.19 垂直锚定】
        /// 仅配合 <see cref="BottomToTopAlignTo"/> 使用：Y = 目标.Y - 自身.Height - Gap，
        /// 即本元素下边缘与目标上边缘之间留出 Gap 像素空隙（Gap=0 时紧贴）。
        /// 用于在"保持当前布局不变"的前提下声明垂直链（间距取当前实际空隙）。
        /// </summary>
        public int? BottomToTopGap { get; set; }

        /// <summary>
        /// 垂直居中对齐目标（可空）——【V1.58.19 垂直锚定】
        /// 若设置（如 "SetButton"），加载时 Y = 目标.Y + (目标.Height - 自身.Height) / 2，
        /// 即本元素**垂直中心线与目标元素垂直中心线重合**（上下居中于目标）。
        /// 常配合 <see cref="CenterOffsetY"/> 微调偏移，用于"多个元素以某基准上下居中分布"。
        /// 与 <see cref="VerticalAlignTo"/>（上下边缘完全对齐）不同，本字段只居中对齐。
        /// 与 <see cref="TopMargin"/>、<see cref="BottomMargin"/>、<see cref="BottomToTopAlignTo"/> 互斥。
        /// </summary>
        public string VerticalCenterAlignTo { get; set; }

        /// <summary>
        /// 垂直居中偏移（px，可空）——【V1.58.19 垂直锚定】
        /// 仅配合 <see cref="VerticalCenterAlignTo"/> 使用：居中计算后再加本偏移（正数向下、负数向上）。
        /// 用于让一组元素以同一基准"对称分布"（如两条延时行：一条偏移 -12、另一条偏移 +12）。
        /// </summary>
        public int? CenterOffsetY { get; set; }

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

        /// <summary>
        /// 垂直居中对齐目标（可空）——【V1.58.19 垂直锚定】
        /// 若设置（如 "DelayStartValue"），加载时 Y = 目标.Y + (目标.Height - LabelTextHeight)/2，
        /// 即标签文字垂直中心线与目标矩形垂直中心线重合（文字上下居中于目标框）。
        /// 目标必须是矩形名（见 <see cref="PanelLayoutConfig.GetRectByName"/>）；文字高度用
        /// <see cref="PanelLayoutConfig.LabelTextHeight"/>（默认 12，依赖字体 9pt 微软雅黑）。
        /// 常配合 <see cref="VerticalCenterOffset"/> 微调。与 <see cref="TopMargin"/> 互斥。
        /// </summary>
        public string VerticalCenterAlignTo { get; set; }

        /// <summary>
        /// 垂直居中偏移（px，可空）——【V1.58.19 垂直锚定】
        /// 仅配合 <see cref="VerticalCenterAlignTo"/> 使用：居中后再加本偏移（正数向下、负数向上）。
        /// 用于把标签微调到与目标框保持原设计间距（本项目统一 -1，使文字略偏上、顶部留 3px）。
        /// </summary>
        public int? VerticalCenterOffset { get; set; }

        /// <summary>转成 System.Drawing.Point 供绘制使用</summary>
        public Point ToPoint()
        {
            return new Point(X, Y);
        }
    }
}
