using System;
using System.Drawing;

namespace AgingTestSystem.Models
{
    /// <summary>
    /// 工位面板布局配置：网格自绘用的坐标/颜色/字号/文字全收敛到此（纯代码缺省，
    /// V1.91 起删掉 PanelLayout.json 文件自定义：项目未上线，改布局直接改代码重编译，
    /// 不留"文件覆盖代码"的第二套口径）。
    /// 单位逻辑像素（96DPI），颜色 "R,G,B" 字符串；72 工位共用一套模板。
    /// 面板内容 204×128（紧凑布局：标题并入第一行，纵向省 42px 给字号腾缩放比；
    /// V1.91 标签列 56→65、值列右移 65→74、按钮 50→46，长标签零余量装得下）。
    ///
    /// 锚定机制（改坐标前必读本节）：元素用字段声明与"面板边缘/其他元素"的相对关系，
    /// 加载时统一解析成最终坐标，改面板尺寸/基准元素时跟随自动联动，不用手算一串坐标。
    /// 原则：只声明关系不挪位置（间距取当前实际空隙，解析结果与原来逐像素一致）。
    /// 锚定字段总表
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
    ///   TopToBottomAlignTo : 上缘贴在"目标元素"下边缘下方 → Y = 目标.Y + 目标有效H + TopToBottomGap（V1.77 电流行）
    ///   TopToBottomGap     : 上述间距 px（Gap=0 紧贴；目标是收起的电流行时其高度按 0）（V1.77）
    ///   VerticalCenterAlignTo : 垂直居中对齐目标 → Y = 目标.Y+(目标H-自身H)/2（V1.58.19）
    ///   CenterOffsetY       : 垂直居中时的额外偏移 px（正下负上，配合对称分布）（V1.58.19）
    /// ElementPoint（标签文字）：
    ///   LeftMargin/TopMargin : 左上角锚定，X/Y 固定距面板左/上缘（V1.58.17）
    ///   RightToLeftAlignTo   : 右缘贴合目标左缘，需配 Width（文字固定宽）（V1.58.16）
    ///   LeftAlignTo          : 左缘对齐目标左缘（V1.58.16）
    ///   VerticalCenterAlignTo : 垂直居中对齐目标矩形（用 LabelTextHeight 算文字高）（V1.58.19）
    ///   VerticalCenterOffset : 垂直居中时的额外偏移 px（本项目统一 -1，文字顶部留 3px）（V1.58.19）
    /// 【三步解析流程（<see cref="ResolveAnchors"/>，顺序不可颠倒）】
    ///   ① ResolveRight        ：直接锚定面板边缘（RightMargin / TopMargin / BottomMargin）
    ///   ② ResolveElementAlign ：元素间锚定（RightAlignTo / VerticalAlignTo / LeftAlignTo /
    ///                            RightToLeftAlignTo / BottomToTopAlignTo / VerticalCenterAlignTo）
    ///   ③ ResolveLabelAnchors ：标签锚定（依赖 ① 和 ② 的矩形结果）
    /// 【依赖顺序铁律】被依赖元素必须先解析（<see cref="ResolveElementAlign"/> 注释为准，
    ///   此处是摘要）：
    ///   ① 面板 → ② 设置按钮(BottomMargin，有效高) → 选中框(右上固定) → 真空关
    ///   (右缘贴选中框左缘) → 下电(左缘对齐 SN＋右缘贴真空关，Y/H 对齐真空块) → SN(X 右缘跟随先解)
    ///   → 压力框(左缘 SN/右缘设置按钮宽 130，Y 吊真空块) → 电流行(TopToBottom:压力框)
    ///   → SN 终解 Y；配方(BottomToTop:SetButton)；延时两行(VerticalCenter:SetButton) → 标签。
    ///   顺序错会取到目标旧值，表现为"改了不生效 / 元素错位"。
    ///   压力/真空关/SN 走"自上而下链"（电流行插入所迫）；SN→配方之间为"交接缝"
    ///   （缺省高度下间距 3px，见下"完整锚定链"）。
    /// 【当前完整锚定链（V1.58.19 横纵双向 + V1.58.20 内容居中 + V1.77 电流行
    /// + V1.88.16 状态块删除 + V1.89 标题并入第一行，解析结果如下）】
    ///   下链（面板底→上，面板增高时整体下移）：纵链头 = View 面板下缘(内容高 128)：
    ///     └─ 设置按钮 RcSetButton(RightMargin=9 + BottomMargin=7 → X=204-9-46=149、Y=128-7-36=85，
    ///        右缘 195 距面板右缘 9px，下缘距面板底 7px；宽 50→46，"设置"12pt 实测 42px 完整显示是底线)
    ///          └─ 配方框 RcRecipeValue(右缘:SetButton→X=74 + 下缘贴设置上缘、Gap=4 → Y=85-16-4=65)
    ///   上链（顶部→下，面板增高时不动；标题/编号与选中框并入第一行）：
    ///     ├─ 选中框 RcSelectBox(右上固定：RightMargin=5 + TopMargin=6 → X=204-5-14=185、Y=6，14×14)
    ///     ├─ 真空关 RcVacuumOpen(右缘贴选中框左缘 Gap=4 → X=185-4-54=127；Y=4 固定顶区，54×18)
    ///     │    ├─ 下电 RcPower(左缘对齐 SN 框 X=74＋右缘贴真空关左缘 Gap=4 → 宽 127-74-4=49；Y/H 对齐真空块)
    ///     │    ├─ 编号 TitlePosition(左上：LeftMargin=6 + TopMargin=6 → X=6、Y=6，与第一行同行)
    ///     │    ├─ 压力框 RcPressureValue(左缘:SN(74)/右缘:SetButton(195)→宽121；
    ///     │    │    Y 吊真空块下方 TopToBottomGap=4 → 4+18+4=26，121×16)
    ///     │    └─ 电流行 RcCurrentValue(左缘 SN(74)/右缘:SetButton(195)→宽121，与压力同界；
    ///     │         Y 吊压力框下方 Gap=2 → 26+16+2=44，121×16)
    ///     │         └─ SN 框 RcSNValue(右缘:SetButton→X=195-121=74；Y 吊电流行下方 Gap=2：
    ///     │              关电流电流行高按 0 → Y=44+0+2=46；开时 Y=44+16+2=62)
    ///     ├─ 延时时间/烧屏时间值框(左缘:SNValue→X=74、宽 72 + 垂直居中于设置按钮，CenterOffsetY=-9/+9)
    ///     │         → Y=86/104（跟下链走；按钮高 36 与值框高 16 差为偶数，偏移 ±9 精确对称；
    ///     │         右缘 146 距按钮左缘 149 留 3px）
    ///     └─ 各标签(横向锚定 + VerticalCenterAlignTo 各自框，offset=-1)：真空压力/电流/SN/配方/延时时间/烧屏时间
    ///         （标签列宽 65：四字 10pt 实测 65px 零余量装得下；X=74-65=9，值列右移 65→74 让位）
    ///   交接缝 SN→配方（两链在此交接）：缺省高度下配方 Y(65) - SN 下缘(46+16=62) = 3px；
    ///   面板增高时间距拉大（上链不动、下链下移）——顶部信息行位置永不动，
    ///   这是故意的（追溯信息不随面板高度漂移）。
    ///   开电流行（ShowCurrent=true，有效高 128+16=144）：上链压力/真空关/电流行不动，
    ///   SN 46→62、配方 65→81、延时 86/104→102/120、设置按钮 85→101，
    ///   间距全都不变（交接缝 81-(62+16)=3 ✓、配方→按钮 101-(81+16)=4 ✓、底边距 144-(101+36)=7 ✓）。
    ///   横链头 = View 面板右缘(内容宽 204)：选中框(RightMargin=5,TopMargin=6→X=185,Y=6)；
    ///   面板左缘：编号(LeftMargin=6,TopMargin=6→X=6)、标签列(X=9)
    ///   标签列左缘 X=9 与设置按钮右缘 195 关于面板中线（204/2=102）对称：
    ///   左留白 9 = 右留白 204-195=9，面板内内容整体水平居中。编号 X=6 比标签列多探出 3px，
    ///   给 11pt 标题留槽位（"NO.72"实测 56px，6+56=62 距下电块 74 留 12px）。改 PanelInnerWidth 时
    ///   左右各留边距、中间元素按锚定自动联动，始终居中。
    /// 【调整指南】
    /// - 改面板宽度：改 PanelInnerWidth / PanelColumnWidth，右缘元素自动跟随，无需手改坐标。
    /// - 改面板高度：改 PanelInnerHeight，设置按钮按 BottomMargin 贴底自动下移，下链
    ///   （配方/延时/标签）按各自 Gap/偏移自动联动；上链（真空关/下电/压力/电流/SN）不动，
    ///   交接缝 SN→配方间距吸收高度差（原来全链联动，电流行插入后顶部锁定）。
    /// - ShowCurrent 是运行时内存开关（不持久化，调用方按需置位再 ResolveAnchors）：
    ///   WorkstationGridView.ShowCurrentRow 置 true → ResolveAnchors 重解 → 面板有效高 +16、
    ///   SN 及以下整体下移 16、间距不变；置 false 回到原来逐像素布局。行高/画布/命中一律走
    ///   GetEffectiveInnerHeight()/GetEffectiveRowHeight()，禁止手写 128/136 常量。
    ///   电流行高改 RcCurrentValue.Height（缺省 16）：有效高、SN 位移、交接缝自动一致，
    ///   因为位移量 = 本行高（TopToBottom 目标有效高机制），三处同源（回归锁"开电流行几何"）。
    /// - 想让面板内容保持"左右对称居中"，只需保证 标签列左缘 X(=9)
    ///   == 面板宽 - 设置按钮右缘（204-195=9）。改 PanelInnerWidth 后按锚定自动联动，
    ///   始终居中；想整体加/减左右留白，同步改标签列 Width 与设置按钮 RightMargin 即可。
    /// - 想整体让下排元素更紧凑/更松：改各 BottomToTopGap；想只挪某个框：改该元素锚定字段或基准元素。
    /// - 新增元素：优先声明锚定关系（贴到某个已有元素），保持链路完整，避免"孤岛坐标"。
    /// 【注意事项 / 常见坑】
    /// - 标签 Width（如 LabelPressurePosition.Width=65）与 LabelTextHeight（默认 14）都依赖字体
    ///   （微软雅黑 10pt），改字体字号必须同步这两个值。
    /// - Y 互斥组：TopMargin / BottomMargin / VerticalAlignTo / BottomToTopAlignTo /
    ///   TopToBottomAlignTo / VerticalCenterAlignTo 同一元素只配其一（后配覆盖 Y）；
    ///   X 互斥组：RightMargin / RightAlignTo / LeftAlignTo / RightToLeftAlignTo 同理。
    /// - VerticalCenterAlignTo 用整数除法 (目标H-自身H)/2，会有 0.5px 截断误差；对称分布两条框时
    ///   需用一正一负且相差 1 的偏移抵消，保证与手工坐标完全一致（当前按钮 36 与框 16 差为偶数，
    ///   偏移取 ±9 即精确对称，无截断）。
    /// </summary>
    public class PanelLayoutConfig
    {
        // ===================== 网格尺寸 =====================

        /// <summary>单个面板单元格列宽（面板内容 204 + 左右边距各 2 + 边框余量；
        /// V1.58.11 由 245 缩小到 227；紧凑到 209，值框 148→130 省出的宽度）</summary>
        public int PanelColumnWidth { get; set; } = 209;

        /// <summary>单个面板单元格行高（面板内容 128 + 上下边距 8；182→136，
        /// 面板间纵向缝由 12 收到 8，标题并入第一行省 42px，纵向缩放比 0.526→0.703）</summary>
        public int PanelRowHeight { get; set; } = 136;

        /// <summary>最右侧"行全选"按钮列宽（逻辑像素）。竖排单字约 20px（运行字号实测），
        /// 48 在常用缩放下给约 27px 物理宽，字不贴边、手指可点；再窄会夹字。
        /// 改小省出的宽度直接放大横向缩放比（内容总宽 8×209＋本列）</summary>
        public int RowSelectButtonColumnWidth { get; set; } = 48;

        /// <summary>面板内容设计宽（每个面板实际绘制区域宽度；
        /// V1.58.11 由 240 缩小到 222；222→204：SN/配方/压力框 148→130，
        /// 长文本本就走省略号，省 18px 给网格腾宽）</summary>
        public int PanelInnerWidth { get; set; } = 204;

        /// <summary>面板内容设计高（每个面板实际绘制区域高度；
        /// 开电流行时有效高度 = 本值 + 电流行高，见 <see cref="GetEffectiveInnerHeight"/>；
        /// 170→128（V1.89）：标题/选中框并入第一行省 22px、状态块 20→18 省 2px、
        /// 值框 18→16 省 10px、按钮 42→36 省 6px、间隙收紧省 2px，见类头锚定链）</summary>
        public int PanelInnerHeight { get; set; } = 128;

        /// <summary>
        /// 是否显示电流行（运行时内存开关，不持久化）。
        /// false（默认）= 关电流布局：电流行高按 0 解析，下游 SN 回到原位；
        /// true = 电流行展开（WorkstationGridView.ShowCurrentRow 由 MainForm 按 UsePowerMeter 传入，
        /// 改后重启生效）。CreateDefault 出来永远是 false，调用方按需置 true 再 ResolveAnchors。
        /// </summary>
        public bool ShowCurrent { get; set; } = false;

        /// <summary>
        /// 面板内容有效高度（逻辑像素，供绘制/命中/画布尺寸用；）。
        /// 关电流行 = PanelInnerHeight；开 = PanelInnerHeight + 电流行高。
        /// </summary>
        public int GetEffectiveInnerHeight()
        {
            if (ShowCurrent && RcCurrentValue != null) return PanelInnerHeight + RcCurrentValue.Height;
            return PanelInnerHeight;
        }

        /// <summary>
        /// 网格行有效高度（逻辑像素，供画布尺寸/可见行计算/命中用；）。
        /// 关 = PanelRowHeight；开 = PanelRowHeight + 电流行高（面板内容增高多少，行就增高多少）。
        /// </summary>
        public int GetEffectiveRowHeight()
        {
            if (ShowCurrent && RcCurrentValue != null) return PanelRowHeight + RcCurrentValue.Height;
            return PanelRowHeight;
        }

        // ===================== 字体 =====================

        /// <summary>面板正文字体名（值必须是系统已安装的字体，如 微软雅黑/宋体）</summary>
        public string FontFamily { get; set; } = "微软雅黑";

        /// <summary>面板正文文字大小（单位：磅 pt；V1.89 由 9→10：
        /// 纵向压紧后 zoomY 0.526→0.703，物理字号 4.7pt→7.0pt；
        /// 四字标签 10pt 实测 65px，故标签列加宽到 65、值列右移 65→74 让位；
        /// 时间串"00:00:00"实测 71px，故延时框加宽到 72）</summary>
        public float FontSize { get; set; } = 10f;

        /// <summary>设备编号标题字体大小（磅；V1.89 由 12→11：
        /// 标题并入第一行与上下电/真空同行，槽位（x=6 到下电左缘 67）61px 宽，
        /// "NO.72" 11pt 实测 56px（12pt 要 62px 塞不下）；物理字号仍 6.3pt→7.7pt 更大更清）</summary>
        public float TitleFontSize { get; set; } = 11f;

        /// <summary>
        /// 设置按钮文字大小（磅；用户点名绿底白字看不清：
        /// 按钮框 46×36 逻辑像素（V1.91 由 50 压到 46），"设置"两字 12pt 实测约 42×22px，
        /// 框内左右各留 2px＋上下各留 7px，完整显示是底线，再窄就顶边框；
        /// 14pt 实测顶满边框故取 12；跟 zoom 等比缩放，下限同 MinFontSize）。
        /// </summary>
        public float SetButtonFontSize { get; set; } = 12f;

        /// <summary>
        /// 延时/烧屏时间值文字大小（磅；时间框 72 宽是整套布局里最紧的槽：
        /// "00:00:00" 10pt 实测 71px，框内文本区 72-6=66px 装不下走省略号（"00:00:.."）；
        /// 9pt 实测 56px（含最宽的"88:88:88"同宽），两边各留 5px；
        /// 数字笔画简单，小 1pt 照样清楚。标签"延时时间"仍是正文 10pt，只动值。
        /// 跟 zoom 等比缩放，下限同 MinFontSize。
        /// </summary>
        public float TimeValueFontSize { get; set; } = 9f;

        /// <summary>设备编号标题是否加粗</summary>
        public bool TitleFontBold { get; set; } = true;

        /// <summary>
        /// 行全选按钮字号倍率（相对面板正文字号；用户点名全选按钮字竖排＋大点：
        /// 按钮高 ≈ 面板高（127 逻辑像素），横排两字显小，竖排后纵向空间绰绰有余，
        /// 字号 = 正文字号 × 本倍率，加粗与正文一致）。
        /// </summary>
        public float RowSelectFontScale { get; set; } = 2f;

        /// <summary>静态标签文字高度（px，默认 14，V1.89 由 12→14）——</summary>
        /// 用于标签垂直居中对齐（<see cref="ElementPoint.VerticalCenterAlignTo"/>）时计算 Y：
        /// Y = 目标中心 - LabelTextHeight / 2。此值依赖字体（10pt 微软雅黑实测约 19px 含边距，
        /// 按 9pt→12 与实测 17 的差值 5 折算取 14，保证标签与 16 高值框视觉居中），
        /// 若改字体字号需同步此值（同标签 Width 依赖字体的道理）。
        public int LabelTextHeight { get; set; } = 14;

        // ===================== 面板内容坐标（相对面板左上角） =====================
        // V1.58.10 的整体右移居中效果不好（偏左观感其实是面板过宽、
        // 右侧留白太多），故 X 全部还原为 V1.58.9 布局，改为缩小面板宽度
        // （PanelInnerWidth 240→222、PanelColumnWidth 245→227）来减小右侧空隙。

        /// <summary>上电/下电状态块（并入第一行：左缘对齐 SN 框 LeftAlignTo="SNValue"（X=74，
        /// 与下方值框列左缘对齐）＋右缘贴真空关左缘 Gap=4（RightToLeftAlignTo="VacuumOpen"），
        /// 宽由两端推导（127-74-4=49）；Y/H 取真空块同行等高。
        /// "上电/下电"10pt 实测 37px，49 宽左右各留 6px；真空块 54 宽不动（"真空开"实测 51px 已到底）。</summary>
        public ElementRect RcPower { get; set; } = new ElementRect { X = 74, Y = 4, Width = 49, Height = 18, LeftAlignTo = "SNValue", RightToLeftAlignTo = "VacuumOpen", RightToLeftGap = 4, VerticalAlignTo = "VacuumOpen" };

        /// <summary>真空开/关状态块（并入第一行当上链基准：右缘贴选中框左缘 Gap=4，
        /// RightToLeftAlignTo="SelectBox"，X=185-4-54=127；Y 取 TopMargin=4 固定顶区，54×18；
        /// 下电块 Y/H 对齐它、压力框 Y 吊它下方（4+18+4=26）。宽 54 不动："真空开"10pt 实测 51px，
        /// 左右各留 1~2px 已是下限。）</summary>
        public ElementRect RcVacuumOpen { get; set; } = new ElementRect { X = 127, Y = 4, Width = 54, Height = 18, RightToLeftAlignTo = "SelectBox", RightToLeftGap = 4, TopMargin = 4 };

        /// <summary>压力值框（V1.91 标签恢复"真空压力"四字：标签列 56→65，
        /// 左缘对齐 SN 框 LeftAlignTo="SNValue"（X=74）、右缘对齐设置按钮
        /// RightAlignTo="SetButton"（右缘 195，与 SN/配方同界）→ 宽 130→121；
        /// Y 吊真空块下方 TopToBottomAlignTo="VacuumOpen"，Gap=4 → Y=4+18+4=26；
        /// 高 16 不变。"78 kPa"短文本不受影响，长文本走省略号。）</summary>
        public ElementRect RcPressureValue { get; set; } = new ElementRect { X = 74, Y = 26, Width = 121, Height = 16, LeftAlignTo = "SNValue", RightAlignTo = "SetButton", TopToBottomAlignTo = "VacuumOpen", TopToBottomGap = 4 };

        /// <summary>载台电流值框（UsePowerMeter 开才显示的行，紧贴压力框下方）。
        /// 与压力框同界（左缘 SN→74、右缘设置按钮→195，宽 121）；
        /// Y 吊压力框下方 TopToBottomAlignTo="PressureValue"+TopToBottomGap=2 → Y=26+16+2=44；
        /// 高 16；关电流行高按 0，SN 回到 46；开时面板有效高度 +16（128→144），
        /// 下游 SN/配方/延时/按钮整体下移 16，间距全都不变。
        /// 面板高改变的联动语义见类头"完整锚定链"。</summary>
        public ElementRect RcCurrentValue { get; set; } = DefaultRcCurrentValue();

        /// <summary>
        /// 电流行缺省矩形唯一出处（属性初始值用它，改坐标只改这里）。
        /// 每次返回新实例（调用方会就地改坐标，禁给共享引用）。
        /// </summary>
        public static ElementRect DefaultRcCurrentValue()
        {
            return new ElementRect { X = 74, Y = 44, Width = 121, Height = 16, LeftAlignTo = "SNValue", RightAlignTo = "SetButton", TopToBottomAlignTo = "PressureValue", TopToBottomGap = 2 };
        }

        /// <summary>SN 值框（右缘对齐设置按钮；Y 由 TopToBottomAlignTo="CurrentValue" 自上而下定位。
        /// 宽 130→121（V1.91 值列右移让位给 65 宽标签列）、高 16、Gap=2：
        /// 关电流（电流行高按 0）时 Y=44+0+2=46；开时 Y=44+16+2=62。
        /// 右缘跟随设置按钮，随内容居中）</summary>
        public ElementRect RcSNValue { get; set; } = new ElementRect { X = 74, Y = 46, Width = 121, Height = 16, RightAlignTo = "SetButton", TopToBottomAlignTo = "CurrentValue", TopToBottomGap = 2 };

        /// <summary>配方值框（右缘对齐设置按钮；下缘锚定 BottomToTopAlignTo="SetButton"。
        /// 宽 130→121、高 16、Gap=4 → Y=85-16-4=65。
        /// 右缘跟随设置按钮，随内容居中；面板高改变时设置按钮下移，配方框跟随其上缘联动）</summary>
        public ElementRect RcRecipeValue { get; set; } = new ElementRect { X = 74, Y = 65, Width = 121, Height = 16, RightAlignTo = "SetButton", BottomToTopAlignTo = "SetButton", BottomToTopGap = 4 };

        /// <summary>延时时间值框（左缘锚定 SN 框 LeftAlignTo="SNValue"（X=74）；
        /// 垂直居中于设置按钮 VerticalCenterAlignTo="SetButton"。
        /// 宽 72（框内值走 9pt 时间字："00:00:00"实测 56px，文本区 72-6=66 装得下；10pt 要 71px 会截断）、高 16、
        /// CenterOffsetY=-9：Y=85+(36-16)/2-9=86，即框中心 94 位于按钮中心 103
        /// 上方 9px，与烧屏框精确对称（高差为偶数，无截断误差）。右缘 146 距按钮左缘 149 留 3px）</summary>
        public ElementRect RcDelayTimeValue { get; set; } = new ElementRect { X = 74, Y = 86, Width = 72, Height = 16, LeftAlignTo = "SNValue", VerticalCenterAlignTo = "SetButton", CenterOffsetY = -9 };

        /// <summary>烧屏时间值框（左缘锚定 SN 框；垂直居中于设置按钮。
        /// 宽 72、高 16、CenterOffsetY=+9：
        /// Y=85+10+9=104，即框中心 112 位于按钮中心 103 下方 9px，与延时框对称。
        /// 左缘跟随 SN 框→74）</summary>
        public ElementRect RcBurnInValue { get; set; } = new ElementRect { X = 74, Y = 104, Width = 72, Height = 16, LeftAlignTo = "SNValue", VerticalCenterAlignTo = "SetButton", CenterOffsetY = 9 };

        /// <summary>"设置"按钮区域（右侧锚定 RightMargin=9；下缘锚定 BottomMargin。
        /// 按钮右缘与标签列左缘关于面板中线对称 → 内容整体水平居中。
        /// 宽 50→46（V1.91："设置"12pt 实测 42px，46 宽左右各留 2px，完整显示是底线；
        /// 省出的 4px 连同延时框右移给 65 宽标签列让位）、高 36、BottomMargin=7：
        /// X=204-9-46=149、Y=128-7-36=85（右缘 195、下缘 121）。
        /// 是"垂直链"的链头：面板高改变时按钮自动贴底跟随；改面板宽时按右留白 9 自动联动）</summary>
        public ElementRect RcSetButton { get; set; } = new ElementRect { X = 149, Y = 85, Width = 46, Height = 36, RightMargin = 9, BottomMargin = 7 };

        /// <summary>右上选中指示框（并入第一行与编号/上下电/真空同行：
        /// 右上角锚定 RightMargin=5 + TopMargin=6 → X=204-5-14=185，Y=6，14×14
        /// （16→14：第一行高 18，框 14 上下各留 2px，居中不顶边；点框/点空白都能翻选，
        /// 框小一点不影响操作）。
        /// 注意：选中框属"右上角元素"，不参与内容居中平移，保持右缘距面板右缘 5px。
        /// 绘制恒正方形：边长取本矩形缩放后的较小边（跟面板尺寸走，见
        /// WorkstationGridView.SelectBoxSide），位置仍走 RightMargin/TopMargin；
        /// 命中与绘制同源（GetSelectBoxLocalRect），改锚定不漂移。</summary>
        public ElementRect RcSelectBox { get; set; } = new ElementRect { X = 185, Y = 6, Width = 14, Height = 14, RightMargin = 5, TopMargin = 6 };

        /// <summary>设备编号文字位置（V1.89 并入第一行：LeftMargin=6 + TopMargin=6 → X=6、Y=6，
        /// 与下电/真空/选中框同行；原来独占一行（9,4）。左探 3px 给 11pt 标题留槽
        /// （"NO.72"实测 56px，6+56=62 距下电块 67 留 5px）；标签列仍 X=9 对称不变）</summary>
        public ElementPoint TitlePosition { get; set; } = new ElementPoint { X = 6, Y = 6, LeftMargin = 6, TopMargin = 6 };

        /// <summary>静态标签"真空压力"位置（V1.91 恢复四字：Width=56→65，
        /// RightToLeftAlignTo="PressureValue"，X=74-65=9；
        /// 垂直居中于压力框 VerticalCenterAlignTo="PressureValue"+VerticalCenterOffset=-1。
        /// Y=26+(16-14)/2-1=26；10pt 下四字实测 65px，槽位零余量但装得下）</summary>
        public ElementPoint LabelPressurePosition { get; set; } = new ElementPoint { X = 9, Y = 26, Width = 65, RightToLeftAlignTo = "PressureValue", VerticalCenterAlignTo = "PressureValue", VerticalCenterOffset = -1 };

        /// <summary>静态标签"SN:"位置（左缘锚定压力标签→X=9；垂直居中于 SN 框。
        /// 关电流 Y=46+1-1=46；SN 框开电流行时下移，标签自动跟随（开时 62+1-1=62））</summary>
        public ElementPoint LabelSnPosition { get; set; } = new ElementPoint { X = 9, Y = 46, LeftAlignTo = "LabelPressure", VerticalCenterAlignTo = "SNValue", VerticalCenterOffset = -1 };

        /// <summary>静态标签"电流："位置（左缘锚定压力标签→X=9；
        /// 垂直居中于电流行：开时 Y=44+1-1=44；关时电流行高按 0，
        /// Y=44-7-1=36——关电流整行不画，这个坐标用不上，只为解析不空悬）。
        /// RcCurrentValue 为 null 时本标签解析保持默认（X=9），绘制时跳过（见 WorkstationGridView）。</summary>
        public ElementPoint LabelCurrentPosition { get; set; } = new ElementPoint { X = 9, Y = 44, LeftAlignTo = "LabelPressure", VerticalCenterAlignTo = "CurrentValue", VerticalCenterOffset = -1 };

        /// <summary>静态标签"配方:"位置（左缘锚定压力标签→X=9；垂直居中于配方框。
        /// Y=65+1-1=65）</summary>
        public ElementPoint LabelRecipePosition { get; set; } = new ElementPoint { X = 9, Y = 65, LeftAlignTo = "LabelPressure", VerticalCenterAlignTo = "RecipeValue", VerticalCenterOffset = -1 };

        /// <summary>静态标签"延时时间"位置（V1.91 恢复四字；左缘锚定压力标签→X=9；
        /// 垂直居中于延时框。Y=86+1-1=86）</summary>
        public ElementPoint LabelDelayTimePosition { get; set; } = new ElementPoint { X = 9, Y = 86, LeftAlignTo = "LabelPressure", VerticalCenterAlignTo = "DelayTimeValue", VerticalCenterOffset = -1 };

        /// <summary>静态标签"烧屏时间"位置（V1.91 恢复四字；左缘锚定压力标签→X=9；
        /// 垂直居中于烧屏框。Y=104+1-1=104）</summary>
        public ElementPoint LabelBurnInPosition { get; set; } = new ElementPoint { X = 9, Y = 104, LeftAlignTo = "LabelPressure", VerticalCenterAlignTo = "BurnInValue", VerticalCenterOffset = -1 };

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

        /// <summary>上电状态块背景色（绿；随设置按钮统一加深为 ForestGreen：
        /// ON 态白字，对比度 2:1→4.6:1；OFF 态是灰底不受影响）</summary>
        public string ColorPowerOn { get; set; } = "34,139,34";

        /// <summary>下电状态块背景色（浅灰）</summary>
        public string ColorPowerOff { get; set; } = "211,211,211";

        /// <summary>真空开状态块背景色（绿；随设置按钮统一加深为 ForestGreen，同上）</summary>
        public string ColorVacuumOn { get; set; } = "34,139,34";

        /// <summary>
        /// 真空异常状态块背景色（红：阀已开但负压未达到阈值，"开了没吸住"）。
        /// 【为什么单独配】以前阀开恒绿，漏气要等到超时报警才知道；现在开阀即按到位标记显示
        /// 绿/红，红色必须一眼与绿色区分。缺省与故障红同值（255,0,0），现场可按灯光环境微调。
        /// </summary>
        public string ColorVacuumAlarm { get; set; } = "255,0,0";

        /// <summary>真空关状态块背景色（浅灰）</summary>
        public string ColorVacuumOff { get; set; } = "211,211,211";

        /// <summary>面板背景-已完成·待取料（淡钢蓝，V1.59 新增）</summary>
        public string ColorCompletedBackground { get; set; } = "176,196,222";

        /// <summary>"设置"按钮背景色（绿；亮绿 50,205,50→深绿 34,139,34：
        /// 白字压亮绿对比度仅约 2:1，小字 wash 到看不清；深绿约 4.6:1，
        /// 仍是绿色语义，深浅主题通用）</summary>
        public string ColorSetButton { get; set; } = "34,139,34";

        /// <summary>行全选按钮背景色（浅灰）</summary>
        public string ColorRowSelectButton { get; set; } = "211,211,211";

        /// <summary>值框背景色（白）</summary>
        public string ColorValueBox { get; set; } = "255,255,255";

        /// <summary>值框/正文文字颜色（黑）</summary>
        public string ColorText { get; set; } = "0,0,0";

        /// <summary>边框颜色（黑）</summary>
        public string ColorBorder { get; set; } = "0,0,0";

        // ===================== 缺省实例 =====================

        /// <summary>
        /// 创建默认布局（唯一入口）：new 出内置缺省并统一调用 <see cref="ResolveAnchors"/>
        /// 解析面板边缘/元素间锚定，保证 X/Y 与锚定一致。布局是纯代码配置，
        /// 改布局直接改本文件的缺省值并同步回归用例，不读任何外部文件。
        /// </summary>
        public static PanelLayoutConfig CreateDefault()
        {
            var def = new PanelLayoutConfig();
            def.ResolveAnchors();
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
            // V1.89：真空块 TopMargin=4 定第一行 Y；选中框右上固定；设置按钮贴底。
            RcPower = ResolveRight(RcPower);
            RcVacuumOpen = ResolveRight(RcVacuumOpen);
            RcPressureValue = ResolveRight(RcPressureValue);
            RcCurrentValue = ResolveRight(RcCurrentValue);
            RcSNValue = ResolveRight(RcSNValue);
            RcRecipeValue = ResolveRight(RcRecipeValue);
            RcDelayTimeValue = ResolveRight(RcDelayTimeValue);
            RcBurnInValue = ResolveRight(RcBurnInValue);
            RcSetButton = ResolveRight(RcSetButton);
            RcSelectBox = ResolveRight(RcSelectBox);

            // 第二步：元素间对齐（含垂直链，顺序见 ResolveElementAlign 注释）
            ResolveElementAlign();

            // 第三步：标签锚定（右缘贴合目标左缘 / 左缘对齐目标 / 垂直居中），依赖矩形解析结果
            ResolveLabelAnchors();
        }

        /// <summary>单个矩形的面板边缘锚定解析（V1.58.17 含 TopMargin、V1.58.19 含 BottomMargin）：
        /// 有 RightMargin 则 X = 面板宽-边距-宽（右缘贴面板右缘）；
        /// 有 TopMargin 则 Y = 边距（上缘贴顶）；有 BottomMargin 则 Y = 有效面板高-边距-高（下缘贴底，
        /// 用有效高度：开电流行时面板长高 16（V1.89 值），底部链整体下移，关时与原来一致）。</summary>
        private ElementRect ResolveRight(ElementRect r)
        {
            if (r != null)
            {
                if (r.RightMargin.HasValue) r.X = PanelInnerWidth - r.RightMargin.Value - r.Width;
                if (r.TopMargin.HasValue) r.Y = r.TopMargin.Value;
                if (r.BottomMargin.HasValue) r.Y = GetEffectiveInnerHeight() - r.BottomMargin.Value - r.Height;
            }
            return r;
        }

        /// <summary>
        /// 按 <see cref="ElementRect.RightAlignTo"/>（右缘对齐）、<see cref="ElementRect.VerticalAlignTo"/>
        /// （垂直对齐）、<see cref="ElementRect.BottomToTopAlignTo"/>（下缘贴目标上缘）、
        /// <see cref="ElementRect.TopToBottomAlignTo"/>（上缘贴目标下缘，）、
        /// <see cref="ElementRect.VerticalCenterAlignTo"/>（垂直居中）解析元素间锚定。
        /// 基准矩形名（字符串）→ 实际属性的映射见 <see cref="GetRectByName"/>。
        /// 【注意依赖顺序（V1.77 上下双链；V1.88.16 真空块当上链头；
        /// V1.89 标题/选中框并入第一行，真空块改贴选中框）】被依赖的元素必须先解析：
        /// 下链（面板底→上，面板增高时整体下移）：设置按钮（BottomMargin，有效高）→ 配方（贴设置按钮）
        /// 上链（顶部→下，面板增高时不动）：选中框（右上固定）→ 真空关（右缘贴选中框+Y 顶区不动）
        /// → SN（X 右缘跟随先解，Y 按电流缺省先解；电流解完后再终解一次 Y，防手改间隙）
        /// → 下电（左缘对齐 SN＋右缘贴真空关，宽由两端推导；Y/H 对齐真空块，同行等高）
        /// → 压力框（X 左缘 SN/右缘设置按钮，Y 吊真空块）
        /// → 电流行（吊压力框）→ SN 终解 → 延时两行（居中设置按钮）。
        /// 两链在"SN→配方"之间交接：缺省高度下 SN 下缘距配方上缘 3px，
        /// 面板增高时该间距拉大（顶部信息行位置永不动，见类头"完整锚定链"）。
        /// 延时两行垂直居中于设置按钮（跟下链走）。
        /// 顺序错会导致取到目标旧值、元素错位（V1.77 血泪：SN 排在压力框后，
        /// 压力框双端 X 读到 SN 旧 X，改面板宽才现形——回归"宽度+10 压力框"锁）。
        /// </summary>
        private void ResolveElementAlign()
        {
            // ① 下链头：设置按钮（BottomMargin 已在第一步按有效高解析出 Y）
            //    配方：下边缘贴设置按钮上边缘（跟下链走，面板增高时下移）
            RcRecipeValue = AlignSelf(RcRecipeValue);
            // ② 上链头：选中框第一步已右上固定；真空块右缘贴选中框（Y=4 第一步 TopMargin 已定）
            RcVacuumOpen = AlignSelf(RcVacuumOpen);
            // ③ SN：X 右缘跟随设置按钮（必须在压力框/电流行之前——两者左缘都读 SN.X）；
            //    Y 按电流缺省值先解，终解在⑦。
            RcSNValue = AlignSelf(RcSNValue);
            // ④ 下电：左缘对齐 SN（LeftAlignTo）+ 右缘贴真空关（RightToLeft，Gap=4，宽两端推导）
            //    + Y/H 对齐真空块（VerticalAlignTo，同行等高）；
            //    SN.X（③）与真空块（②）上面已定，与编号/选中框同行（Y=4）。
            RcPower = AlignSelf(RcPower);
            // ⑤ 依赖 SN/真空块：压力框左缘对齐 SN + 右缘对齐设置按钮 + Y 吊真空块下方（Y=26，见类头锚定链）
            RcPressureValue = AlignSelf(RcPressureValue);
            // ⑥ 依赖压力框：电流行吊在压力框下方（Y=26+16+2=44，与压力同界宽 130）
            RcCurrentValue = AlignSelf(RcCurrentValue);
            // ⑦ SN 终解 Y（电流已解：手改 TopToBottomGap 也能终值正确；缺省值下与③一致，幂等）
            RcSNValue = AlignSelf(RcSNValue);
            // ⑧ 延时两行：垂直居中于设置按钮（CenterOffsetY ±9 对称分布，跟下链走）
            RcDelayTimeValue = AlignSelf(RcDelayTimeValue);
            RcBurnInValue = AlignSelf(RcBurnInValue);
            // ⑨ 其余元素无链式锚定，保持第一步结果
            AlignSelf(RcSetButton);
            AlignSelf(RcSelectBox);
        }

        /// <summary>
        /// 按本矩形的锚定字段对齐到目标矩形。优先级：
        /// ① RightAlignTo（右缘=目标右缘）② LeftAlignTo+RightToLeftAlignTo（双端锚定定 X 与宽）
        /// ③ 单独 LeftAlignTo（左缘=目标左缘）④ 单独 RightToLeftAlignTo（右缘=目标左缘）
        /// ⑤ VerticalAlignTo（Y/Height=目标）⑥ BottomToTopAlignTo（Y=目标上缘-自身高，贴目标上方）
        /// ⑦ TopToBottomAlignTo（Y=目标下缘+间距，吊目标下方；目标是收起的电流行时
        /// 其高度按 0，见 TopToTopAlignTo 注释）⑧ VerticalCenterAlignTo（Y=目标中心-自身高/2，
        /// 垂直居中；可配 CenterOffsetY 偏移）
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
            if (!string.IsNullOrEmpty(self.TopToBottomAlignTo))
            {
                var t = GetRectByName(self.TopToBottomAlignTo);
                if (t != null)
                {
                    // 目标有效高：平时=目标.Height；目标是收起的电流行（关电流）时按 0，
                    // 下游 SN 回到原位（Height 属性本身不动，反复解析不漂移，见幂等用例）。
                    int th = t.Height;
                    if (!ShowCurrent && t == RcCurrentValue) th = 0;
                    self.Y = t.Y + th + (self.TopToBottomGap ?? 0);  // 上缘=目标下缘+间距（吊在目标下方）
                }
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
                case "VacuumOpen": return RcVacuumOpen;
                case "PressureValue": return RcPressureValue;
                case "CurrentValue": return RcCurrentValue;
                case "SNValue": return RcSNValue;
                case "RecipeValue": return RcRecipeValue;
                case "DelayTimeValue": return RcDelayTimeValue;
                case "BurnInValue": return RcBurnInValue;
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
            ResolveLabel(LabelCurrentPosition);    // 垂直居中于电流行（关电流不画，坐标无意义）
            ResolveLabel(LabelSnPosition);         // 左缘对齐真空压力标签
            ResolveLabel(LabelRecipePosition);
            ResolveLabel(LabelDelayTimePosition);
            ResolveLabel(LabelBurnInPosition);
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
                case "LabelCurrent": return LabelCurrentPosition;
                case "LabelSn": return LabelSnPosition;
                case "LabelRecipe": return LabelRecipePosition;
                case "LabelDelayTime": return LabelDelayTimePosition;
                case "LabelBurnIn": return LabelBurnInPosition;
                default: return null;
            }
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
    /// 矩形坐标（ plain 数据类，替代不可直接持久化的 System.Drawing.Rectangle；
    /// 另带可选的锚定字段，见 <see cref="PanelLayoutConfig.ResolveAnchors"/>）
    /// </summary>
    public class ElementRect
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        /// <summary>
        /// 右侧锚定边距（px，可空）——
        /// 若设置（非 null），加载配置时 X 会被重算为 面板内容宽 - RightMargin - Width，
        /// 使该元素始终与面板右缘保持固定距离；以后调整 PanelInnerWidth 时自动跟随，
        /// 无需手改 X（解决面板缩窄后选中框溢出、右空隙失控等反复手调问题）。
        /// 未设置（null）时使用绝对 X，兼容旧配置。
        /// </summary>
        public int? RightMargin { get; set; }

        /// <summary>
        /// 顶部锚定边距（px，可空）——
        /// 若设置，Y = TopMargin，即元素上边缘固定距面板上缘该距离；
        /// 配合 <see cref="RightMargin"/> 构成"右上角锚定"（如选中框）。
        /// 与 <see cref="VerticalAlignTo"/> 互斥，同一元素只配其一。
        /// </summary>
        public int? TopMargin { get; set; }

        /// <summary>
        /// 右缘对齐目标（可空）——
        /// 若设置（如 "SetButton"），加载时 X 被重算为 目标矩形右缘 - 自身 Width，
        /// 即与本元素与目标元素"右侧对齐"。用于"多个元素右缘对齐且共同跟随一个基准元素"。
        /// 目标名见 <see cref="PanelLayoutConfig.ResolveElementAlign"/>；未设置则忽略。
        /// </summary>
        public string RightAlignTo { get; set; }

        /// <summary>
        /// 垂直对齐目标（可空）——
        /// 若设置（如 "VacuumOpen"），加载时 Y 与 Height 取目标矩形的值，
        /// 即本元素与目标元素"上边缘、下边缘对齐"。未设置则忽略。
        /// </summary>
        public string VerticalAlignTo { get; set; }

        /// <summary>
        /// 左缘对齐目标（可空）——
        /// 若设置（如 "SNValue"），加载时 X 取目标矩形的 X，即本元素左边缘与目标左边缘对齐。
        /// 与 <see cref="RightToLeftAlignTo"/> 同时设置时构成"双端锚定"：宽度由两端自动推导
        /// （宽 = 右锚定目标左缘 - 左锚定目标左缘），无需手设 Width。未设置则忽略。
        /// </summary>
        public string LeftAlignTo { get; set; }

        /// <summary>
        /// 右缘对齐到目标左缘（可空）——
        /// 若设置（如 "VacuumOpen"），加载时本元素右边缘对齐到目标矩形的左边缘
        /// （右缘贴合目标左缘）。与 <see cref="LeftAlignTo"/> 同时设置时构成"双端锚定"推导宽度。
        /// 未设置则忽略。
        /// </summary>
        public string RightToLeftAlignTo { get; set; }

        /// <summary>
        /// 右缘与目标左缘的间隙（px，可空）——
        /// 仅配合 <see cref="RightToLeftAlignTo"/>（含与 <see cref="LeftAlignTo"/> 组合的双端锚定）使用：
        /// - 双端锚定时：Width = 右锚定目标左缘 - 左锚定目标左缘 - Gap（右缘不再紧贴目标左缘，留 Gap 空隙）；
        /// - 单独 RightToLeftAlignTo 时：X = 目标左缘 - 自身 Width - Gap。
        /// 【背景】V1.58.9 真空压力框与真空关之间本有 3px 间隙（宽 85、右缘 142 vs 真空关左 145）；
        /// V1.58.15 双端锚定把宽算成 145-57=88 导致紧贴，本字段用于恢复该间隙。
        /// 未设置（null）时 Gap=0（紧贴），兼容旧配置。
        /// </summary>
        public int? RightToLeftGap { get; set; }

        /// <summary>
        /// 底部锚定边距（px，可空）——
        /// 若设置（非 null），加载时 Y 会被重算为 面板内容高 - BottomMargin - Height，
        /// 即该元素**下边缘距面板下缘**该距离（"自下而上"锚定）。以后调整
        /// PanelInnerHeight 时自动跟随，无需手改 Y。
        /// 【注意：保持布局不变】BottomMargin 取"当前下边缘到面板下缘的实际距离"即可
        /// 让位置完全不变（如设置按钮当前 Y=120、H=42、面板高 170 → BottomMargin=8）。
        /// 与 <see cref="TopMargin"/>、<see cref="VerticalAlignTo"/>、<see cref="BottomToTopAlignTo"/>、
        /// <see cref="TopToBottomAlignTo"/>、<see cref="VerticalCenterAlignTo"/> 互斥，同一元素只配其一（Y 会被后配的覆盖）。
        /// 未设置（null）时使用绝对 Y，兼容旧配置。
        /// </summary>
        public int? BottomMargin { get; set; }

        /// <summary>
        /// 下边缘贴目标上边缘（可空）——
        /// 若设置（如 "SetButton"），加载时 Y = 目标.Y - 自身.Height - (<see cref="BottomToTopGap"/> ?? 0)，
        /// 即本元素**下边缘紧贴目标元素上边缘上方**（"叠加在目标上方"，可留间距）。
        /// 用于构建"自下而上的垂直链"：如 配方下边缘贴设置按钮上边缘 → SN 下边缘贴配方上边缘。
        /// 【注意：保持布局不变】配 <see cref="BottomToTopGap"/> 为"当前下边缘到目标上边缘的实际距离"
        /// 即可让位置完全不变（如配方框当前 Y=98、H=18、设置按钮 Y=120 → Gap=120-98-18=4）。
        /// 与 <see cref="TopMargin"/>、<see cref="BottomMargin"/>、<see cref="VerticalAlignTo"/>、
        /// <see cref="TopToBottomAlignTo"/>、<see cref="VerticalCenterAlignTo"/> 互斥。目标名见 <see cref="PanelLayoutConfig.GetRectByName"/>。
        /// </summary>
        public string BottomToTopAlignTo { get; set; }

        /// <summary>
        /// 下缘贴目标上缘时的间距（px，可空）——
        /// 仅配合 <see cref="BottomToTopAlignTo"/> 使用：Y = 目标.Y - 自身.Height - Gap，
        /// 即本元素下边缘与目标上边缘之间留出 Gap 像素空隙（Gap=0 时紧贴）。
        /// 用于在"保持当前布局不变"的前提下声明垂直链（间距取当前实际空隙）。
        /// </summary>
        public int? BottomToTopGap { get; set; }

        /// <summary>
        /// 上边缘贴目标下边缘（可空）——
        /// 若设置（如 "PressureValue"），加载时 Y = 目标.Y + 目标有效高 + (<see cref="TopToBottomGap"/> ?? 0)，
        /// 即本元素**上边缘紧贴目标元素下边缘下方**（"吊在目标下方"，可留间距）。
        /// 与 <see cref="BottomToTopAlignTo"/> 方向相反：BottomToTop 是"自下而上链"（目标在下方），
        /// TopToBottom 是"自上而下链"（目标在上方）。目标有效高平时 = 目标.Height；
        /// 目标是电流行且 <see cref="PanelLayoutConfig.ShowCurrent"/> 关闭时按 0
        /// （电流行收起，下游 SN 回到原位，关电流布局与原来逐像素一致）。
        /// 与 <see cref="TopMargin"/>、<see cref="BottomMargin"/>、<see cref="VerticalAlignTo"/>、
        /// <see cref="BottomToTopAlignTo"/>、<see cref="VerticalCenterAlignTo"/> 互斥。
        /// </summary>
        public string TopToBottomAlignTo { get; set; }

        /// <summary>
        /// 上缘贴目标下缘时的间距（px，可空）——
        /// 仅配合 <see cref="TopToBottomAlignTo"/> 使用：Y = 目标下缘 + Gap。
        /// 未设置（null）时 Gap=0（紧贴），兼容旧配置。
        /// </summary>
        public int? TopToBottomGap { get; set; }

        /// <summary>
        /// 垂直居中对齐目标（可空）——
        /// 若设置（如 "SetButton"），加载时 Y = 目标.Y + (目标.Height - 自身.Height) / 2，
        /// 即本元素**垂直中心线与目标元素垂直中心线重合**（上下居中于目标）。
        /// 常配合 <see cref="CenterOffsetY"/> 微调偏移，用于"多个元素以某基准上下居中分布"。
        /// 与 <see cref="VerticalAlignTo"/>（上下边缘完全对齐）不同，本字段只居中对齐。
        /// 与 <see cref="TopMargin"/>、<see cref="BottomMargin"/>、<see cref="BottomToTopAlignTo"/>、
        /// <see cref="TopToBottomAlignTo"/> 互斥。
        /// </summary>
        public string VerticalCenterAlignTo { get; set; }

        /// <summary>
        /// 垂直居中偏移（px，可空）——
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
    /// 点坐标（plain 数据类，替代 System.Drawing.Point；另带可选的锚定字段）
    /// </summary>
    public class ElementPoint
    {
        public int X { get; set; }
        public int Y { get; set; }

        /// <summary>
        /// 标签文字固定宽度（px，可空）——
        /// 仅配合 <see cref="RightToLeftAlignTo"/> 使用：右缘贴合目标左缘时
        /// X = 目标左缘 - Width。宽度依赖字体，若改字体需同步此值。
        /// </summary>
        public int? Width { get; set; }

        /// <summary>
        /// 右缘贴合目标左缘（可空）——
        /// 若设置（如 "PressureValue"），X = 目标矩形/标签左缘 - Width，即标签文字右边缘
        /// 对齐到目标左边缘。目标名可为矩形名或标签名，见 <see cref="PanelLayoutConfig.GetAnchorX"/>。
        /// </summary>
        public string RightToLeftAlignTo { get; set; }

        /// <summary>
        /// 左缘对齐目标（可空）——
        /// 若设置（如 "LabelPressure"），X = 目标矩形/标签左缘，即本标签与目标左边缘对齐。
        /// </summary>
        public string LeftAlignTo { get; set; }

        /// <summary>
        /// 左缘锚定边距（px，可空）——
        /// 若设置，X = LeftMargin，即标签左边缘固定距面板左缘该距离（左上角锚定，如编号）。
        /// 与 <see cref="LeftAlignTo"/> 互斥，同一标签只配其一。
        /// </summary>
        public int? LeftMargin { get; set; }

        /// <summary>
        /// 顶部锚定边距（px，可空）——
        /// 若设置，Y = TopMargin，即标签上边缘固定距面板上缘该距离。
        /// </summary>
        public int? TopMargin { get; set; }

        /// <summary>
        /// 垂直居中对齐目标（可空）——
        /// 若设置（如 "DelayTimeValue"），加载时 Y = 目标.Y + (目标.Height - LabelTextHeight)/2，
        /// 即标签文字垂直中心线与目标矩形垂直中心线重合（文字上下居中于目标框）。
        /// 目标必须是矩形名（见 <see cref="PanelLayoutConfig.GetRectByName"/>）；文字高度用
        /// <see cref="PanelLayoutConfig.LabelTextHeight"/>（默认 12，依赖字体 9pt 微软雅黑）。
        /// 常配合 <see cref="VerticalCenterOffset"/> 微调。与 <see cref="TopMargin"/> 互斥。
        /// </summary>
        public string VerticalCenterAlignTo { get; set; }

        /// <summary>
        /// 垂直居中偏移（px，可空）——
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
