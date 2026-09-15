using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AgingTestSystem.Models;

namespace AgingTestSystem.Views
{
    /// <summary>
    /// 工位网格（自绘大画布）：8列×9行面板＋行全选列合并为 1 个 UserControl，
    /// 双向精确铺满一屏（zoomX=可用宽/内容宽、zoomY=可用高/内容高独立，无滚动条；
    /// 面板可宽扁拉伸，字取窄边等比缩放、下限 MinFontSize）。判定走纯函数 ComputeFitZoom，
    /// 执行侧 <see cref="WorkstationGridView.UpdateAutoFit"/> 应用（DeviceManager 不碰显示）。
    ///
    /// 关键规则（都是实测栽过的）：
    /// - 72 面板全由 OnPaint 按绝对坐标画，只重绘 ClipRectangle 可见区；禁用 TranslateTransform/
    ///   ScaleTransform（TextRenderer 走 GDI 不认坐标变换，会错位叠字）；禁离屏整幅预渲染
    ///   （离屏大图上 TextRenderer 每处 2.2ms，全量两秒多，屏 DC 上近 0ms）；
    ///   绘制热路径的画刷画笔走缓存字段，别每帧 new；
    /// - DPI：布局是 96DPI 逻辑像素，句柄建好后 _dpiScale=真实DPI/96，
    ///   绘制/命中/画布尺寸全经 ScaledX/ScaledY 放大；取 DPI 用 CreateGraphics().DpiX，
    ///   别用 DeviceDpi（建句柄瞬间谎报 96）；
    /// - 交互全走坐标命中（点选中框/空白翻选，点设置区开窗，鼠标触屏同走 MouseUp）；
    /// - 坐标/颜色/字号走 <see cref="PanelLayoutConfig"/>（纯代码缺省，改布局改代码重编译），
    ///   面板内元素全锚定：字段全表与三步解析顺序见 PanelLayoutConfig 类头，改坐标前必读。
    ///   （锚定只声明"以谁为基准、距离多少"，间距取当前实际空隙；
    ///   老 json 缺新字段时删文件重导，不要手补。）
    ///
    /// 界面布局
    /// 一、整体结构（外层 Panel 容器 + 本控件 = 画布，无滚动条）
    /// ┌───────────────────────────────────────────┬──────────┐
    /// │             画布（本控件 OnPaint）         │ 行全选列 │
    /// │ ┌──────┬──────┬──────┬──────┬──────┬───  │ ├──────────┤
    /// │ │ NO.1 │ NO.2 │ NO.3 │ NO.4 │ NO.5 │ ... │ │ [全]     │ ← 第1行（竖排大字：
    /// │ ├──────┼──────┼──────┼──────┼──────┼───  │ │ [选]     │   "全/选"按正常字隙排成紧凑一竖块、
    /// │ │ NO.9 │ NO.10│ NO.11│ NO.12│ NO.13│ ... │ │ [全]     │   整块居中（等分撑满太散）；
    /// │ │ ...  │ ...  │ ...  │ ...  │ ...  │ ... │ │ [选]     │   字号=正文×RowSelectFontScale；
    /// │ └──────┴──────┴──────┴──────┴──────┴───  │ ├──────────┤
    /// │ 8列（列宽209，每格内容204+左右边距各2）      │ 行内全部   │
    /// │ × 9行（行高148，每格内容140+上下缝8）       │ 选中→[取消]│
    /// │ （竖排：[取]/[消]上下两格）                  │          │
    /// └───────────────────────────────────────────┴──────────┘
    /// （保持 8×9=72；内容总宽 8×209＋行全选列 48＝1720，总高 9×148＝1332）
    /// 行全选按钮高 = 面板内容高-1(=139)，含边框后上下边缘与工作站显示框(140)完全对齐；
    /// 按钮矩形 = (列右缘+2, 行顶+2, 列宽-4, PanelInnerHeight-1)；-1 修正边框底凸出1px
    /// 网格占满全部 72 台设备。
    /// AutoFit=true 即双向精确铺满一屏（FitWidth/FitMode 双模式开关已删，
    /// 只留这一路）：zoomX=可用宽/内容宽、zoomY=可用高/内容高独立，画布精确等于显示区客户区，
    /// 纵向横向滚动条都不出（面板允许宽扁拉伸，字取窄边不变形）。窗口拉大/缩小/最大化跟随缩放；
    /// 字体取窄边等比缩放、下限见 MinFontSize（6pt→4pt，
    /// 1280×1024小屏跟随缩小不挤叠）；列数保持 8×9 不动。
    /// 显示区被挤到极小时 zoom 照算（不再钳 MinZoom，也不转滚动条兜底）：72 站永远一屏看全，
    /// 字保 MinFontSize 可读。关掉 AutoFit 回原尺寸（超出部分直接裁掉、无滚动条）。
    /// 实现见 ComputeFitZoom/UpdateAutoFit/RebuildFonts/UpdateCanvasSize
    /// （zoomX/zoomY 并进 ScaledX/ScaledY）。
    /// 二、单个面板内容（204×140，坐标均为"相对面板左上角"；紧凑布局：
    /// 标题/选中框并入第一行与上下电/真空同行，纵向省 42px 换缩放比；
    /// 下电 54→49→45（左缘对齐值框列 74）、真空 54 不动、值框 130×18→121×16、
    /// 延时框 66→72、设置按钮 50×42→46×36、选中框 16→14→18（对勾保留）；正文 9→10pt、
    /// 标题 12→11pt，物理字号反而 4.7→7.0pt 更清；标签列 56→65 装四字长标签）：
    /// ┌──────────────────────────────────────────────┐
    /// │ NO.1 ┌──────┐ ┌────────┐        ┌──────┐      │ ← 第一行同行四件套
    /// │ (6,6)│上电/下电│ │真空开/关│      │选中框✓ │  │    编号11pt＋下电45×18＋真空54×18＋选中框18×18
    /// │      └──────┘ └────────┘        └──────┘      │
    /// │ 真空压力 ┌──────────────────────────────┐    │
    /// │          │  78 kPa                      │    │
    /// │          └──────────────────────────────┘    │
    /// │ SN:    ┌────────────────────────┐           │
    /// │ 配方:  ┌────────────────────────┐           │
    /// │        └────────────────────────┘           │
    /// │ 延时时间 ┌──────────────┐ ┌───────────────┐ │
    /// │          │ 00:00:00     │ │      设置     │ │ ← 绿底白字
    /// │ 烧屏时间 ┌──────────────┘ └───────────────┘ │
    /// │          │ 00:00:00  │                        │
    /// │          └────────────┘                        │
    /// └──────────────────────────────────────────────┘
    /// 标注说明（括号内为锚定关系）：
    /// - 行1（Y=4,H=18）：编号 NO.1(6,6；LeftMargin=6+TopMargin=6) + 上电/下电块(74,4,45,18；
    ///   左缘对齐 SN 框＋右缘贴真空关左缘 Gap=4，宽两端推导＋Y/H 对齐真空块） + 真空开/关块(123,4,54,18；
    ///   右缘贴选中框左缘 Gap=4＋TopMargin=4） + 选中框(181,4,18,18；RightMargin=5+TopMargin=4，
    ///   边长取缩放后较小边恒正方形，见 SelectBoxSide）
    /// - 行2：压力值框(74,28,121,16；左缘对齐 SN 框＋右缘对齐设置按钮，宽 121；
    ///   Y 吊真空块下方 TopToBottomGap=6，Y=4+18+6=28)
    ///   + 电流值框 RcCurrentValue(74,48,121,16；与压力同界；
    ///   Y 吊压力框下方 Gap=4；ShowCurrentRow 关=整行不画；开=面板 140→156、SN 52→68、
    ///   配方 77→93、延时 98/116→114/132、按钮 97→113，间距全都不变，见下方"V1.77 开态几何")
    /// - 行3：SN 值框(74,52,121,16；吊电流行下方 TopToBottomGap=4：
    ///   关电流电流行高按 0，Y=48+0+4=52；开时 Y=48+16+4=68；SN 序列号最长，值框保持最宽一档)
    /// - 行4：配方值框(74,77,121,16；下缘贴设置按钮上缘、Gap=4)
    /// - 行5：延时时间值框(74,98,72,16；框内值走 9pt 时间字，标签仍 10pt；
    ///   以设置按钮中心为基准、CenterOffsetY=-9) +
    ///   设置按钮(149,97,46,36；下缘距面板底 BottomMargin=7) + 烧屏时间值框(74,116,72,16；CenterOffsetY=9)
    /// - 编号：NO.1(6,6)（LeftMargin=6 + TopMargin=6，与第一行同行）
    /// - 标签列：真空压力(9,28)/SN:(9,52)/配方:(9,77)/延时时间(9,98)/烧屏时间(9,116)
    ///   （X=9 为右缘贴合压力框左缘推导 74-65=9；Y 以各自框中心为基准、VerticalCenterOffset=-1；
    ///   标签列宽 65：四字 10pt 实测 65px 零余量装得下，"00:00:00"实测 71px 装进 72 框）
    /// - 标签列左缘 X=9，设置按钮右缘贴右（RightMargin=9，右缘 195），
    ///   左留白 9 = 右留白 9 → 面板内内容整体水平居中（右缘 195/宽 204 对称不变）；
    ///   编号 X=6 比标签列多探 3px（给 11pt 标题留槽，"NO.72"右缘 62 距下电 74 留 12px）；
    ///   选中框 TopMargin=4（Y=4，与真空块同高同顶同行，框 18 填满行高 18）。
    /// - 值框文字左内边距：ValueTextLeftPadding=6px（V1.52，文字不贴值框左边框，值框坐标不变）
    /// - 状态块配色见下方"状态块配色"；颜色值收敛在 PanelLayoutConfig，改代码生效
    /// - 延时时间/烧屏时间两行中心与设置按钮中心垂直居中对齐
    ///   （现 (106+124)/2=115=(97+18)，偏移 ±9 精确对称）；
    ///   V1.58.19 起改为 VerticalCenterAlignTo 锚定自动保持居中。
    /// - 空闲/真空关/SN框/配方框/设置按钮五者右边缘统一 = 205：
    ///   工作状态块右移 X=153、真空关宽调成与空闲一致(48→52)并右移 X=153、
    ///   SN/配方加宽至 148；真空压力框加宽至 93（右边缘=150，与真空关左边缘 153 保持 3px）。
    ///   校验：下电块(52×23)与工作状态块(52×23)尺寸一致。
    /// - 空闲/真空关左边缘与设置按钮左边缘对齐：工作状态块、真空开/关块
    ///   X=153→145、W=52→60，左右边缘均=145/205（与设置按钮 145/205 完全重合）；
    ///   下电 W 同步 52→60 保持一致；真空压力框缩窄 93→85（右=142，与真空关左 145 保持 3px）。
    /// - V1.58.10 整体右移 16px 居中的观感不佳（偏左的根因是面板
    ///   过宽、右侧留白太多，而非坐标偏左），故 X 全部还原为 V1.58.9 布局，改由缩小面板宽度
    ///   减小右空隙：PanelInnerWidth 240→222（右空隙 35→17px）、PanelColumnWidth 245→227。
    ///   V1.58.6~1.58.9 的各项对齐不受影响。
    /// - 面板缩至 222 后，选中框原 X=212（右缘 235）溢出面板，左移 X=194
    ///   （右缘 217），与面板右边距保持 5px。
    /// - ElementRect 新增可选 RightMargin（右侧锚定边距）：选中框(5)、
    ///   空闲/真空关/SN/配方/设置按钮(17) 改为锚定，加载时 X 自动 = PanelInnerWidth - RightMargin - Width。
    ///   以后改面板宽度（PanelInnerWidth）右缘元素自动跟随，不再手改坐标（V1.58.11/12 的坑）。
    /// - 设置按钮锚定 View 右缘(RightMargin=17)；工作状态块已删，
    ///   真空块接替当上链基准（见 PanelLayoutConfig 类头"完整锚定链"）；下电改对齐真空块。
    ///   解析顺序：先 RightMargin 面板锚定，再 RightAlignTo/VerticalAlignTo 元素间锚定。
    /// - 真空压力框 LeftAlignTo="SNValue"（左缘对齐 SN 框左缘）+
    ///   RightAlignTo="SetButton"（右缘对齐设置按钮，宽 121 与 SN/配方同界）；
    ///   下电 LeftAlignTo="SNValue"（左缘对齐 SN 框，与下方值框列对齐）+
    ///   RightToLeftAlignTo="VacuumOpen"（右缘贴真空关左缘 Gap=4，宽两端推导 49）。
    /// - "压力"标签 Width=56 固定文字宽 + RightToLeftAlignTo="PressureValue"
    ///   （右缘贴合压力框左缘，X=65-56=9）；SN:/配方:/延时/烧屏 四标签 LeftAlignTo="LabelPressure"
    ///   （左缘对齐"压力"标签）。
    /// - 编号 TitlePosition 第一行锚定（LeftMargin=6 + TopMargin=6）；
    ///   选中框右上锚定（RightMargin=5 + TopMargin=5）；延时/烧屏值框补左缘锚定
    ///   LeftAlignTo="SNValue"（跟随值框列）。至此全部元素均已锚定，改面板宽/高基本布局不变。
    /// - 压力/真空关/SN 走自上而下链 TopToBottom；SN→配方为两链交接缝，
    ///   缺省高度下间距 9px，详见 PanelLayoutConfig 类头"完整锚定链"；
    ///   现值：按钮 BottomMargin=7；配方 Gap=4；压力 Gap=6；SN Gap=4，关电流 Y=52；
    ///   延时两行 VerticalCenterAlignTo="SetButton"+CenterOffsetY=-9/+9(以按钮中心为基准对称)；
    ///   各标签 VerticalCenterAlignTo 各自框+offset=-1。改 PanelInnerHeight 时下链自动联动，
    ///   上链（第一行+压力/电流/SN）不动，差值由交接缝吸收。
    /// - ShowCurrentRow=true（UsePowerMeter 开）时单面板内容
    ///   204×156（行高 148→164），压力行(28)及以上不动，新增电流行(48,高16)+标签"电流："，
    ///   SN(68)/配方(93)/延时(114/132)/按钮(113)整体下移 16，间距全都不变；
    ///   false 时与本图逐像素一致。开关走 ShowCurrentRow 属性（MainForm 按 UsePowerMeter 装配一次），
    ///   行高/画布/命中一律走 GetEffectiveRowHeight()/GetEffectiveInnerHeight()，禁止手写 140/148。
    /// - 值框文字左内边距：ValueTextLeftPadding=6px（V1.52，文字不贴值框左边框，值框坐标不变）
    /// - 状态块配色见下方"状态块配色"；颜色值收敛在 PanelLayoutConfig，改代码生效
    /// 【状态块配色（V1.28 约定；工作状态块已删，状态只看面板底色＋上电/真空块；
    /// 绿统一加深为 ForestGreen：白字对比度 2:1→4.6:1，与设置按钮/各窗绿按钮同色）】
    /// - 上电/下电：绿=ForestGreen=上电，浅灰=LightGray=下电
    /// - 面板背景：空闲=白 / 测试中=浅黄 / 故障=浅粉 / 已完成·待取料=淡钢蓝（V1.59）
    /// - 真空块三色：阀开且负压到位=绿底 / 阀开但没吸住=红底（真空开，ColorVacuumAlarm）/
    ///   阀没开=灰底（真空关，浅色配置灰/深色 DimGray）
    /// 【数据流】
    /// 主窗体收到设备批量更新后调用 <see cref="UpdateAll"/> / <see cref="UpdateSingle"/>，
    /// 仅更新内存字段 + Invalidate，1Hz 全量刷新开销极小，完全不影响实时监控。
    /// </summary>
    public partial class WorkstationGridView : System.Windows.Forms.UserControl
    {
        // ===== 布局配置 =====
        /// <summary>面板布局配置（纯代码缺省，无外部文件覆盖）</summary>
        private readonly PanelLayoutConfig _layout;

        // ===== 字体 =====
        // 去掉 readonly：自适应缩放（AutoFit）按 _zoom 重建字号，
        // 释放旧字体防 GDI 泄漏（Dispose 已释放两者，见 Designer）。
        /// <summary>面板正文文字字体（显式创建，不继承主窗体缩放字体，保证与小矩形匹配）</summary>
        private Font _panelFont;
        /// <summary>设备编号标题字体（缺省 11pt：并入第一行与上下电/真空同行，槽位 61px；
        /// "NO.72"实测 56px，12pt 要 62px 塞不下；物理字号仍更大更清）</summary>
        private Font _titleFont;
        /// <summary>
        /// 设置按钮字体（独立大字：按钮框 50×36，"设置"两字在正文字号下只占角落；
        /// 字号取配置 SetButtonFontSize（缺省 12），跟 zoom 等比缩放，与 RebuildFonts 同建同释放）。
        /// </summary>
        private Font _setButtonFont;
        /// <summary>
        /// 延时/烧屏时间值字体（比正文小 1pt：时间框 72 是全套最紧的槽，
        /// "00:00:00" 10pt 要 71px 会截断，9pt 只要 56px；字号取配置 TimeValueFontSize，
        /// 跟 zoom 等比缩放，与 RebuildFonts 同建同释放）。
        /// </summary>
        private Font _timeValueFont;
        /// <summary>
        /// 行全选按钮字体（竖排大字：字号 = 正文字号 × 配置倍率，
        /// 与 RebuildFonts 同建同释放；绘制时按字逐格居中，Paint 里不量字）。
        /// </summary>
        private Font _rowSelectFont;
        /// <summary>行全选"全选"文案逐字缓存（构造时拆好，Paint 只按下标取）</summary>
        private readonly string[] _rowSelectCharsAll;
        /// <summary>行全选"取消"文案逐字缓存（构造时拆好，Paint 只按下标取）</summary>
        private readonly string[] _rowSelectCharsCancel;
        /// <summary>竖排单字格高（px，布局态实测缓存，Paint 只读）</summary>
        private int _rowSelectCharH;
        /// <summary>竖排字间隙（px，布局态由字高换算缓存，Paint 只读）</summary>
        private int _rowSelectGap;

        // ===== 配置解析出的颜色（浅色值来自 PanelLayoutConfig 纯代码缺省） =====
        // 以下"跟随主题切换"的颜色去掉 readonly，SetDarkMode 里整体换肤；
        // 语义状态色（上电绿/故障红/繁忙黄/选中橙/完成蓝…）保持 readonly，深浅两边都不动——
        // 绿底白字/灰底黑字在深底上照样清晰，动了反而丢业务含义。
        private Color _normalColor;   // 面板背景-空闲（浅色白 / 深色深灰）
        private Color _testingColor;  // 面板背景-测试中（浅色浅黄 / 深色暗金）
        private Color _faultColor;    // 面板背景-故障（浅色浅粉 / 深色暗红）
        private readonly Color _colorPowerOn;  // 上电块背景（绿，不跟主题）
        private readonly Color _colorPowerOff; // 下电块背景（浅灰，不跟主题）
        private readonly Color _colorVacuumOn; // 真空开块背景（绿，不跟主题）
        private readonly Color _colorVacuumAlarm; // 真空异常块背景（红：阀开但负压未到位，不跟主题）
        private readonly Color _colorVacuumOff;// 真空关块背景（浅灰，不跟主题）
        private Color _completedColor;     // 面板背景-已完成·待取料（浅色淡钢蓝 / 深色深蓝）
        private readonly Color _colorSetButton;    // 设置按钮背景（绿，不跟主题）
        private Color _colorRowSelect;    // 行全选按钮背景（浅色浅灰 / 深色中灰）
        private Color _colorValueBox;     // 值框背景（浅色白 / 深色深灰）
        private Color _colorText;         // 正文文字（浅色黑 / 深色浅灰白）
        private Color _colorBorder;       // 边框（浅色黑 / 深色中灰）

        /// <summary>当前是否为深色模式（默认浅色；主窗体按 ThemeManager.IsDark 调用 SetDarkMode 同步）</summary>
        private bool _darkMode;

        /// <summary>面板列数</summary>
        private int _columns;
        /// <summary>面板行数</summary>
        private int _rows;
        /// <summary>总设备（工位）数</summary>
        private int _totalDevices;

        /// <summary>
        /// DPI 缩放因子 = DeviceDpi / 96（）。
        /// 布局配置里的坐标/尺寸都是"96DPI 逻辑像素"，在 150% 缩放的屏幕上
        /// 必须整体放大 DeviceDpi/96 倍，否则格子不变、而 pt 字体的字会自动变大，
        /// 导致文字溢出格子、与周围被 AutoScaleMode.Font 放大的控件比例失调。
        /// 由于 TextRenderer 走 GDI 不能配合 Graphics 坐标变换（见头部 V1.51 踩坑），
        /// 这里采用"手动把所有逻辑像素乘 _dpiScale"的方式，字体保持 pt 单位自动放大，比例一致。
        /// </summary>
        private float _dpiScale = 1f;

        /// <summary>
        /// 自适应缩放因子（zoomX=可用宽/内容宽、zoomY=可用高/内容高，两轴独立；
        /// 画布精确等于显示区，无滚动条）。最终比例 sx=_dpiScale×_zoomX（sy 同理），
        /// 绘制/命中/画布全走 ScaledX/ScaledY；字体取窄边等比缩放（见 RebuildFonts）。
        /// </summary>
        private float _zoomX = 1f;

        /// <summary>纵向自适应缩放因子（与 _zoomX 独立，面板允许宽扁拉伸，字取窄边）</summary>
        private float _zoomY = 1f;

        // MinZoom/MaxZoom 钳制已删：铺满要求 zoom 精确等于可用/内容，
        // 钳住即铺不满（大屏留白边/小屏被裁）；显示区再小也照算，72 站永远一屏，
        // 字保 MinFontSize 可读（不再转滚动条兜底，滚动条已整套移除）。

        /// <summary>
        /// 自适应字号下限 4pt（小屏 zoom≈0.45 时理想字号约 4pt：旧 6pt 下限会把字卡大 1.5 倍，
        /// 标签溢出盖值框；4pt 跟随缩小保证不挤叠，极小窗也保可读）。
        /// </summary>
        public const float MinFontSize = 4f;

        /// <summary>
        /// 是否自适应父容器（默认 true = 双向铺满一屏，无滚动条）。
        /// 关掉回 zoom=1 原尺寸（超出部分直接裁掉、无滚动条）。结构型开关，运行时可随时翻。
        /// </summary>
        private bool _autoFit = true;

        /// <summary>所有工位的显示状态（key = 设备编号，从1开始）</summary>
        private readonly Dictionary<int, GridItem> _items = new Dictionary<int, GridItem>();

        /// <summary>状态块悬停提示</summary>
        private readonly ToolTip _toolTip;
        /// <summary>上次悬停提示文本（避免 MouseMove 频繁重复 Show）</summary>
        private string _lastTooltipText;

        // ============ 缓存画刷/画笔（绘制热路径复用，别每帧 new，防 GC 压力） ============
        // 数据驱动色（状态块/背景）按需 new；每帧每面板都用的常量色缓存字段复用，
        // 其中跟随主题的 4 个去掉 readonly，SetDarkMode 里重建。
        /// <summary>边框画笔，所有矩形描边共用（跟随主题重建）</summary>
        private Pen _penBorder;
        /// <summary>值框背景画刷，5 个值框共用（跟随主题重建）</summary>
        private SolidBrush _brushValueBox;
        /// <summary>行全选按钮背景画刷（跟随主题重建）</summary>
        private SolidBrush _brushRowSelect;
        /// <summary>设置按钮背景画刷（绿）</summary>
        private readonly SolidBrush _brushSetButton;
        /// <summary>选中指示框"已选中"底色画刷（绿）</summary>
        private readonly SolidBrush _brushSelectChecked;
        /// <summary>选中指示框"未选中"底色画刷（跟随主题重建：浅色白 / 深色深灰）</summary>
        private SolidBrush _brushSelectUnchecked;

        // 拖拽滚动已整套删除（只留一屏铺满）：按下拖动不再转滚动，抬起一律按点击处理。

        /// <summary>工位"设置"按钮点击事件（参数为设备编号，主窗体按被点编号直开该工位设置窗口，不看选中集）</summary>
        public event EventHandler<int> OnSetClicked;
        /// <summary>需要写日志的消息（如行全选动作），由主窗体订阅写入 LOG</summary>
        public event EventHandler<string> OnLog;

        // （DragScrollThreshold 随拖拽滚动整套删除，见上。）

        /// <summary>
        /// 无参数构造函数（设计器/运行时通用）
        /// </summary>
        public WorkstationGridView()
        {
            InitializeComponent();
            this.DoubleBuffered = true;

            // 加载布局配置（纯代码缺省，无外部文件）；颜色分两批：语义状态色直接解析（终身不变），
            // 主题色走 ApplyLightColors（SetDarkMode 切深色/切回浅色都调它，保证浅色精确还原配置值）
            _layout = PanelLayoutConfig.CreateDefault();

            _colorPowerOn = Parse(_layout.ColorPowerOn, Color.ForestGreen);
            _colorPowerOff = Parse(_layout.ColorPowerOff, Color.LightGray);
            _colorVacuumOn = Parse(_layout.ColorVacuumOn, Color.ForestGreen);
            _colorVacuumAlarm = Parse(_layout.ColorVacuumAlarm, Color.Red);
            _colorVacuumOff = Parse(_layout.ColorVacuumOff, Color.LightGray);
            _colorSetButton = Parse(_layout.ColorSetButton, Color.ForestGreen);
            ApplyLightColors();

            // 显式创建字体（不依赖 this.Font / 主窗体 AutoScale，保证文字尺寸与固定矩形一致）。
            // 正文字体直接加粗（与 RebuildFonts 同值，首帧不闪常规体，挂载后即重建覆盖）。
            _panelFont = new Font(_layout.FontFamily, _layout.FontSize, FontStyle.Bold);
            _titleFont = new Font(_layout.FontFamily, _layout.TitleFontSize,
                _layout.TitleFontBold ? FontStyle.Bold : FontStyle.Regular);
            _setButtonFont = BuildSetButtonFont(_layout, 1f);
            _timeValueFont = BuildTimeValueFont(_layout, 1f);
            _rowSelectFont = BuildRowSelectFont(_layout, _layout.FontSize);
            // 竖排逐字绘制的字符缓存：Paint 里只按下标取，不量字不拼串不分配；
            // 文案来自配置（json 可覆盖），构造时拆好，全生命周期不变。
            _rowSelectCharsAll = ToCharStrings(_layout.RowSelectAllText);
            _rowSelectCharsCancel = ToCharStrings(_layout.RowSelectCancelText);
            RefreshRowSelectMetrics();

            // 初始化缓存画刷/画笔：语义色两个一次建好，主题色四个走 RebuildThemeBrushes
            // （SetDarkMode 里复用它重建，保证颜色与字段永远一致）。
            _brushSetButton = new SolidBrush(_colorSetButton);
            _brushSelectChecked = new SolidBrush(Color.ForestGreen); // 选中✓绿（随全仓绿统一加深，白✓对比度同步提升）
            RebuildThemeBrushes();

            _toolTip = new ToolTip(components);

            // 拖拽滚动合并定时器随滚动整套删除；MouseDown 空 handler 同步摘除
            // （点击只看 MouseUp：单击/触摸点选，拖动不再转滚动）。

            this.MouseUp += GridView_MouseUp;
            this.MouseMove += GridView_MouseMove;
            this.MouseLeave += GridView_MouseLeave;
        }

        /// <summary>句柄建好后算 DPI 缩放（须在首次绘制前，否则画布仍是 96DPI 尺寸）</summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateDpiScale();
        }

        /// <summary>
        /// 按真实 DPI 更新缩放因子并重算画布（跨屏拖动 DPI 变了也会触发）。
        /// 必须用 CreateGraphics().DpiX（DeviceDpi 在建句柄瞬间谎报 96）。
        /// </summary>
        private void UpdateDpiScale()
        {
            float dpi = 96f;
            try
            {
                using (var g = CreateGraphics())
                {
                    if (g != null && g.DpiX > 0) dpi = g.DpiX;
                }
            }
            catch
            {
                // 图形上下文创建失败时保持 1.0（96DPI），不影响 100% 缩放的旧环境
            }
            _dpiScale = dpi / 96f;
            if (_columns > 0)
            {
                UpdateCanvasSize();
                // DPI 变了 = 内容物理尺寸变了，按新尺寸重算自适应 zoom。
                UpdateAutoFit();
                Invalidate();
            }
        }

        /// <summary>解析配置颜色字符串（"R,G,B"），失败时回退默认色</summary>
        private static Color Parse(string rgb, Color fallback)
        {
            return PanelLayoutConfig.ParseColor(rgb, fallback);
        }

        #region 自适应缩放（V1.88.14 新增：72 站一屏显示全）

        /// <summary>
        /// 双向自适应缩放比（纯函数）：zoomX=可用宽/内容宽、zoomY=可用高/内容高，
        /// 两轴独立，画布精确等于显示区；任一边非法两轴都回 1；只做除法不钳制。
        /// </summary>
        /// <param name="availWidth">可用宽（物理像素）</param>
        /// <param name="availHeight">可用高（物理像素）</param>
        /// <param name="contentWidth">内容宽（物理像素）</param>
        /// <param name="contentHeight">内容高（物理像素）</param>
        /// <param name="zoomX">横向缩放比</param>
        /// <param name="zoomY">纵向缩放比</param>
        public static void ComputeFitZoom(double availWidth, double availHeight,
            double contentWidth, double contentHeight, out double zoomX, out double zoomY)
        {
            if (availWidth <= 0 || availHeight <= 0 || contentWidth <= 0 || contentHeight <= 0)
            {
                zoomX = 1.0;
                zoomY = 1.0;
                return;
            }
            zoomX = availWidth / contentWidth;
            zoomY = availHeight / contentHeight;
        }

        /// <summary>
        /// 是否自适应父容器（默认 true = 双向铺满一屏，无滚动条）。
        /// 关掉回 zoom=1 原尺寸（超出部分直接裁掉、无滚动条）；打开立即按当前父尺寸重算。
        /// </summary>
        public bool AutoFit
        {
            get { return _autoFit; }
            set
            {
                if (_autoFit == value) return;
                _autoFit = value;
                if (!value && (_zoomX != 1f || _zoomY != 1f))
                {
                    _zoomX = 1f;
                    _zoomY = 1f;
                    RebuildFonts();
                    UpdateCanvasSize();
                    Invalidate();
                }
                else if (value)
                {
                    UpdateAutoFit();
                }
            }
        }

        /// <summary>
        /// 父容器换了：摘旧 Resize、挂新的，按新容器尺寸重算 zoom。
        /// （Configure 时还没 Parent，挂上容器这一跳才是自适应真正生效处。）
        /// </summary>
        protected override void OnParentChanged(EventArgs e)
        {
            // base 之后 Parent 已是新的，旧容器靠字段缓存摘除
            Control oldParent = _fitParent;
            base.OnParentChanged(e);
            if (oldParent != null) oldParent.Resize -= FitParent_Resize;
            _fitParent = Parent;
            if (_fitParent != null) _fitParent.Resize += FitParent_Resize;
            UpdateAutoFit();
        }

        /// <summary>上次挂过 Resize 的父容器（OnParentChanged 换挂时摘除用）</summary>
        private Control _fitParent;

        /// <summary>父容器尺寸变了（主窗最大化/拉宽/Splitter 拖动）→ 重算 zoom</summary>
        private void FitParent_Resize(object sender, EventArgs e)
        {
            UpdateAutoFit();
        }

        /// <summary>
        /// 按父容器可用区重算 zoom 并应用（字体+画布+重绘）。条件不齐直接返回；
        /// 新值差 &lt;0.001 防抖（Splitter 连拖不反复重建字体）。画布精确顶满显示区。
        /// </summary>
        private void UpdateAutoFit()
        {
            if (!_autoFit || _columns <= 0 || Parent == null) return;
            int availW = Parent.ClientSize.Width;
            int availH = Parent.ClientSize.Height;
            if (availW <= 0 || availH <= 0) return;
            double contentW = (double)(_columns * _layout.PanelColumnWidth
                + _layout.RowSelectButtonColumnWidth) * _dpiScale;
            double contentH = (double)(_rows * _layout.GetEffectiveRowHeight()) * _dpiScale;
            double zx, zy;
            ComputeFitZoom(availW, availH, contentW, contentH, out zx, out zy);
            if (Math.Abs(zx - _zoomX) < 0.001 && Math.Abs(zy - _zoomY) < 0.001) return;
            _zoomX = (float)zx;
            _zoomY = (float)zy;
            RebuildFonts();
            UpdateCanvasSize();
            Invalidate();
        }

        /// <summary>
        /// 按当前 zoom 重建字体（旧字体先释放，防 GDI 泄漏）：字号=配置×min(zoomX,zoomY)，
        /// 下限 MinFontSize；正文一律加粗（4~5pt 常规体发虚，加粗黑像素+34%；溢出走省略号不盖框）。
        /// </summary>
        private void RebuildFonts()
        {
            float z = _zoomX < _zoomY ? _zoomX : _zoomY;
            float panelSize = (float)_layout.FontSize * z;
            if (panelSize < MinFontSize) panelSize = MinFontSize;
            float titleSize = (float)_layout.TitleFontSize * z;
            if (titleSize < MinFontSize) titleSize = MinFontSize;
            Font oldPanel = _panelFont;
            Font oldTitle = _titleFont;
            Font oldRowSelect = _rowSelectFont;
            Font oldSetButton = _setButtonFont;
            Font oldTimeValue = _timeValueFont;
            _panelFont = new Font(_layout.FontFamily, panelSize, FontStyle.Bold);
            _titleFont = new Font(_layout.FontFamily, titleSize,
                _layout.TitleFontBold ? FontStyle.Bold : FontStyle.Regular);
            _rowSelectFont = BuildRowSelectFont(_layout, panelSize);
            _setButtonFont = BuildSetButtonFont(_layout, z);
            _timeValueFont = BuildTimeValueFont(_layout, z);
            if (oldPanel != null) oldPanel.Dispose();
            if (oldTitle != null) oldTitle.Dispose();
            if (oldRowSelect != null) oldRowSelect.Dispose();
            if (oldSetButton != null) oldSetButton.Dispose();
            if (oldTimeValue != null) oldTimeValue.Dispose();
            RefreshRowSelectMetrics();
        }

        /// <summary>
        /// 按正文实际字号构建行全选字体（纯静态，可单测：字号 = panelSize × 配置倍率，
        /// 下限保 <see cref="MinFontSize"/>；加粗与正文一致，小字清楚）。
        /// </summary>
        /// <param name="layout">布局配置（取字族与倍率）</param>
        /// <param name="panelSize">正文实际字号（pt，已按 zoom 缩放钳制）</param>
        public static Font BuildRowSelectFont(PanelLayoutConfig layout, float panelSize)
        {
            float scale = layout != null && layout.RowSelectFontScale > 0 ? layout.RowSelectFontScale : 2f;
            float size = panelSize * scale;
            if (size < MinFontSize) size = MinFontSize;
            string family = layout != null && !string.IsNullOrEmpty(layout.FontFamily)
                ? layout.FontFamily : "微软雅黑";
            return new Font(family, size, FontStyle.Bold);
        }

        /// <summary>
        /// 按 zoom 构建设置按钮字体（纯静态，可单测：字号 = 配置 SetButtonFontSize × zoom，
        /// 下限保 <see cref="MinFontSize"/>；白字压深绿，加粗保证小屏清楚）。
        /// </summary>
        /// <param name="layout">布局配置（取字族与 SetButtonFontSize）</param>
        /// <param name="zoom">窄边缩放比（与正文/标题同源，保证三套字同比例）</param>
        public static Font BuildSetButtonFont(PanelLayoutConfig layout, float zoom)
        {
            float baseSize = layout != null && layout.SetButtonFontSize > 0 ? layout.SetButtonFontSize : 12f;
            float size = baseSize * zoom;
            if (size < MinFontSize) size = MinFontSize;
            string family = layout != null && !string.IsNullOrEmpty(layout.FontFamily)
                ? layout.FontFamily : "微软雅黑";
            return new Font(family, size, FontStyle.Bold);
        }

        /// <summary>
        /// 按 zoom 构建延时/烧屏时间值字体（纯静态，可单测：字号 = 配置 TimeValueFontSize × zoom，
        /// 下限保 <see cref="MinFontSize"/>；数字笔画简单，小 1pt 照样清楚，加粗与正文一致）。
        /// </summary>
        /// <param name="layout">布局配置（取字族与 TimeValueFontSize）</param>
        /// <param name="zoom">窄边缩放比（与正文/标题同源，保证四套字同比例）</param>
        public static Font BuildTimeValueFont(PanelLayoutConfig layout, float zoom)
        {
            float baseSize = layout != null && layout.TimeValueFontSize > 0 ? layout.TimeValueFontSize : 9f;
            float size = baseSize * zoom;
            if (size < MinFontSize) size = MinFontSize;
            string family = layout != null && !string.IsNullOrEmpty(layout.FontFamily)
                ? layout.FontFamily : "微软雅黑";
            return new Font(family, size, FontStyle.Bold);
        }

        /// <summary>文案拆逐字数组（null/空即空数组，调用方按空画空按钮）</summary>
        private static string[] ToCharStrings(string text)
        {
            if (string.IsNullOrEmpty(text)) return new string[0];
            string[] chars = new string[text.Length];
            for (int i = 0; i < text.Length; i++) chars[i] = text[i].ToString();
            return chars;
        }

        /// <summary>
        /// 竖排字间隙（纯函数：字高的 1/4 四舍五入、下限 2px——"正常间隙"，
        /// 字挨太紧难读、等分撑满又太散；回归可直接断言）。
        /// </summary>
        public static int RowSelectGapForCharH(int charH)
        {
            if (charH < 1) charH = 1;
            int gap = (int)Math.Round(charH * 0.25);
            return gap < 2 ? 2 : gap;
        }

        /// <summary>
        /// 竖排整块起始 Y（纯函数，回归可直接断言）。
        /// n 个字按"字高＋间隙"排成紧凑一竖块，整块在按钮内垂直居中；
        /// 块超高（极端小窗）时上下对称溢出，仍居中。
        /// </summary>
        /// <param name="rcY">按钮顶</param>
        /// <param name="rcH">按钮高</param>
        /// <param name="charH">单字格高（布局态实测）</param>
        /// <param name="gap">字间隙（见 RowSelectGapForCharH）</param>
        /// <param name="count">字数（"全选"/"取消"恒 2）</param>
        public static int ComputeRowSelectStartY(int rcY, int rcH, int charH, int gap, int count)
        {
            if (count <= 0) return rcY;
            int total = count * charH + (count - 1) * gap;
            return rcY + (rcH - total) / 2;
        }

        /// <summary>
        /// 实测竖排单字格高（布局态调用——构造/RebuildFonts 里各一次，Paint 里只读缓存）。
        /// 四个字（全/选/取/消）同字族同字号等高，量整串单行高即单字格高。
        /// </summary>
        public static int MeasureRowSelectCharH(Font font)
        {
            if (font == null) return 1;
            int h = TextRenderer.MeasureText("全选取消", font).Height;
            return h < 1 ? 1 : h;
        }

        /// <summary>重算行全选竖排度量（字体换了必调：构造一次＋RebuildFonts 里跟 zoom 走）</summary>
        private void RefreshRowSelectMetrics()
        {
            _rowSelectCharH = MeasureRowSelectCharH(_rowSelectFont);
            _rowSelectGap = RowSelectGapForCharH(_rowSelectCharH);
        }

        /// <summary>重算画布总尺寸（改尺寸只改这里；画布恒等于显示区，无滚动可同步，设 Size 即完事）</summary>
        private void UpdateCanvasSize()
        {
            this.Size = new Size(ScaledX(_columns * _layout.PanelColumnWidth + _layout.RowSelectButtonColumnWidth),
                                 ScaledY(_rows * _layout.GetEffectiveRowHeight()));
        }

        #endregion

        #region 深色模式（V1.60 新增）

        /// <summary>载入浅色主题色（json 配的那套；切回浅色再调一次，保证精确还原）</summary>
        private void ApplyLightColors()
        {
            _normalColor = Parse(_layout.ColorNormalBackground, Color.White);
            _testingColor = Parse(_layout.ColorTestingBackground, Color.LightYellow);
            _faultColor = Parse(_layout.ColorFaultBackground, Color.LightPink);
            _completedColor = Parse(_layout.ColorCompletedBackground, Color.LightSteelBlue);
            _colorRowSelect = Parse(_layout.ColorRowSelectButton, Color.LightGray);
            _colorValueBox = Parse(_layout.ColorValueBox, Color.White);
            _colorText = Parse(_layout.ColorText, Color.Black);
            _colorBorder = Parse(_layout.ColorBorder, Color.Black);
        }

        /// <summary>
        /// 载入深色主题色（固定深灰系）：面板/值框/文字/边框走深，语义状态块（绿/红/黄/蓝）不动；
        /// 下电/真空关块走 DimGray 底白字（浅灰在深底上太跳，见 GetOffBlockThemeColors）。
        /// </summary>
        private void ApplyDarkColors()
        {
            _normalColor = Color.FromArgb(45, 45, 48);
            _testingColor = Color.FromArgb(96, 80, 18);
            _faultColor = Color.FromArgb(96, 30, 35);
            _completedColor = Color.FromArgb(38, 68, 110);
            _colorRowSelect = Color.FromArgb(62, 62, 66);
            _colorValueBox = Color.FromArgb(37, 37, 38);
            _colorText = Color.FromArgb(220, 220, 220);
            _colorBorder = Color.FromArgb(120, 120, 120);
        }

        /// <summary>
        /// 下电/真空关块的主题配色（纯函数，方便回归直接断言）。
        /// 浅色：配置原灰底（默认 LightGray）+ 黑字；深色：浅灰在深底上太跳，
        /// 参考主窗体停止/复位按钮走 DimGray 底 + 白字（跟各窗"取消"按钮同款）。
        /// 开/上电块（绿底白字）两边都不动，不走这里。
        /// </summary>
        /// <param name="dark">true=深色配色，false=浅色配色</param>
        /// <param name="lightBack">浅色底（配置值，来自 PanelLayoutConfig）</param>
        /// <param name="back">块底色</param>
        /// <param name="fore">块文字色</param>
        public static void GetOffBlockThemeColors(bool dark, Color lightBack, out Color back, out Color fore)
        {
            back = dark ? Color.DimGray : lightBack;
            fore = dark ? Color.White : Color.Black;
        }

        /// <summary>下电/真空关块底色（浅色跟配置，深色 DimGray）</summary>
        private static Color GetOffBlockBack(bool dark, Color lightBack)
        {
            Color back;
            Color fore;
            GetOffBlockThemeColors(dark, lightBack, out back, out fore);
            return back;
        }

        /// <summary>下电/真空关块文字色（浅色黑字，深色白字）</summary>
        private static Color GetOffBlockFore(bool dark)
        {
            Color back;
            Color fore;
            GetOffBlockThemeColors(dark, Color.LightGray, out back, out fore);
            return fore;
        }

        /// <summary>
        /// 深色开关（给新手：主窗体主题按钮 → ThemeManager → 反射调到这里）。
        /// 相同值重复调直接返回；切换后重建缓存画刷、刷新全部面板底色与下电/真空关块色、重绘。
        /// 注意：本控件 BackColor（面板间缝隙底）也同步走深/浅，缝隙才不会"白一道黑一道"。
        /// </summary>
        /// <param name="dark">true=深色，false=浅色</param>
        public void SetDarkMode(bool dark)
        {
            if (_darkMode == dark) return;
            _darkMode = dark;
            if (dark) ApplyDarkColors();
            else ApplyLightColors();
            RebuildThemeBrushes();
            RefreshItemBackgrounds();
            RefreshOffBlockColors();
            this.BackColor = dark ? Color.FromArgb(30, 30, 30) : SystemColors.Control;
            Invalidate();
        }

        /// <summary>
        /// 重建跟随主题的 4 个缓存画刷/画笔（先释放旧的，避免 GDI 句柄越切越多）。
        /// 语义色那两个（_brushSetButton/_brushSelectChecked）终身不变，不管。
        /// </summary>
        private void RebuildThemeBrushes()
        {
            if (_penBorder != null) { _penBorder.Dispose(); _penBorder = null; }
            if (_brushValueBox != null) { _brushValueBox.Dispose(); _brushValueBox = null; }
            if (_brushRowSelect != null) { _brushRowSelect.Dispose(); _brushRowSelect = null; }
            if (_brushSelectUnchecked != null) { _brushSelectUnchecked.Dispose(); _brushSelectUnchecked = null; }
            _penBorder = new Pen(_colorBorder);
            _brushValueBox = new SolidBrush(_colorValueBox);
            _brushRowSelect = new SolidBrush(_colorRowSelect);
            _brushSelectUnchecked = new SolidBrush(_darkMode ? Color.FromArgb(37, 37, 38) : Color.White);
        }

        /// <summary>
        /// 按当前主题重算全部面板的背景色（切主题时调；平时每轮采集由 UpdateSingleItem 逐台刷新）。
        /// GridItem 只记了颜色没记状态，所以这里额外记了 Status（见 GridItem.Status 字段注释）。
        /// </summary>
        private void RefreshItemBackgrounds()
        {
            foreach (var kv in _items)
            {
                GridItem item = kv.Value;
                if (item == null) continue;
                item.BackColor = GetStatusBackColor(item.Status);
            }
        }

        /// <summary>
        /// 按当前主题重算全部面板的下电/真空关块色（切主题时调，免得等下一轮 1s 采集才变；
        /// 平时每轮采集由 ApplyData 逐台刷新）。GridItem 记了 CarrierPower/VacuumOpen，
        /// 开块（上电绿/真空到位绿/真空未到位红）本来两边主题都不动，这里只重算灰色的关块。
        /// </summary>
        private void RefreshOffBlockColors()
        {
            foreach (var kv in _items)
            {
                GridItem item = kv.Value;
                if (item == null) continue;
                if (!item.CarrierPower)
                {
                    item.PowerColor = GetOffBlockBack(_darkMode, _colorPowerOff);
                    item.PowerForeColor = GetOffBlockFore(_darkMode);
                }
                if (!item.VacuumOpen)
                {
                    item.VacuumColor = GetOffBlockBack(_darkMode, _colorVacuumOff);
                    item.VacuumForeColor = GetOffBlockFore(_darkMode);
                }
            }
        }
        /// <summary>状态→面板底色（ApplyData 与 RefreshItemBackgrounds 共用，保证两处永远一致）</summary>
        private Color GetStatusBackColor(DeviceStatus status)
        {
            if (status == DeviceStatus.Fault) return _faultColor;
            if (status == DeviceStatus.Testing) return _testingColor;
            if (status == DeviceStatus.Completed) return _completedColor;
            return _normalColor;
        }

        #endregion

        /// <summary>
        /// 按配置创建工位网格并设置画布总尺寸（恒等于显示区，一屏铺满无滚动条）
        /// </summary>
        public void Configure(int columns, int rows, int totalDevices)
        {
            _columns = columns;
            _rows = rows;
            _totalDevices = totalDevices;
            _items.Clear();
            for (int i = 0; i < totalDevices; i++)
            {
                _items[i + 1] = new GridItem { DeviceId = i + 1 };
            }
            // 新面板默认底按当前主题走（否则深色下首屏 1 秒内面板是白的，等首轮采集才变深）
            RefreshItemBackgrounds();
            // 下电/真空关块色同样按当前主题初始化（深色首屏直接 DimGray，不闪一下浅灰）
            RefreshOffBlockColors();
            // 画布总尺寸 = 逻辑像素尺寸 × DPI缩放因子。
            // 若不放大，150% 缩放下格子保持 96DPI 大小、文字却自动变大 → 溢出重叠。
            // 尺寸计算收进 UpdateCanvasSize；此时多半还没挂进父容器
            // （MainForm 先 Configure 后 Add），自适应在 OnParentChanged 里补算一次。
            UpdateCanvasSize();
            UpdateAutoFit();
            Invalidate();
        }

        /// <summary>
        /// 是否显示电流行（运行时开关，默认 false = 原来布局逐像素不变）。
        /// 主窗体按 DeviceConfig.UsePowerMeter 传入一次（结构型开关，改后重启生效，
        /// 与 UsePowerMeter 同口径，不跟项目热更）。
        /// 置 true → 布局 ShowCurrent 置位 + 锚定重解（面板有效高 +16，下游下移 16）
        /// + 画布重算 + 重绘；置 false 回到原来布局。重解幂等，反复置位不漂移。
        /// </summary>
        public bool ShowCurrentRow
        {
            get { return _layout.ShowCurrent; }
            set
            {
                if (_layout.ShowCurrent == value) return;
                _layout.ShowCurrent = value;
                _layout.ResolveAnchors();
                if (_columns > 0)
                {
                    UpdateCanvasSize();
                    // 内容高变了（面板 140→156），自适应 zoom 跟着重算。
                    UpdateAutoFit();
                }
                Invalidate();
            }
        }

        #region DPI 缩放辅助

        /// <summary>逻辑像素横向 → 物理像素（× _dpiScale × _zoomX，四舍五入）</summary>
        private int ScaledX(int v)
        {
            return (int)Math.Round(v * _dpiScale * _zoomX);
        }

        /// <summary>逻辑像素纵向 → 物理像素（× _dpiScale × _zoomY，四舍五入）</summary>
        private int ScaledY(int v)
        {
            return (int)Math.Round(v * _dpiScale * _zoomY);
        }

        /// <summary>逻辑像素 Point → 物理像素 Point（X 走横向比、Y 走纵向比）</summary>
        private Point Scaled(Point p)
        {
            return new Point(ScaledX(p.X), ScaledY(p.Y));
        }

        /// <summary>逻辑像素 Rectangle → 物理像素 Rectangle（X/宽走横向比，Y/高走纵向比）</summary>
        private Rectangle Scaled(Rectangle r)
        {
            return new Rectangle(ScaledX(r.X), ScaledY(r.Y), ScaledX(r.Width), ScaledY(r.Height));
        }

        /// <summary>
        /// 选中框边长（物理像素，恒正方形 + 跟面板尺寸走）。
        /// 【为什么不能直接画布局矩形】双向自适应下 zoomX≠zoomY（如旧布局 1080p 下 0.844/0.553），
        /// 布局框会被压成扁条（用户目检：长方形不好看）。
        /// 改为取"缩放后宽高较小边"为边长：面板大框大、面板小框小（自适应），且永远是正方形；
        /// 下限 4px（再小点不中画了，直接保底）。
        /// V1.89 框 18→16 但缩放比变大，物理反而更大更易点。
        /// </summary>
        private int SelectBoxSide()
        {
            Rectangle r = Scaled(_layout.RcSelectBox.ToRectangle());
            int side = r.Width < r.Height ? r.Width : r.Height;
            return side < 4 ? 4 : side;
        }

        /// <summary>
        /// 选中框画布绝对矩形（绘制用；位置：右缘距面板右缘 RightMargin、上缘距顶 TopMargin，
        /// 与布局锚定同口径；边长见 <see cref="SelectBoxSide"/>）。
        /// </summary>
        private Rectangle GetSelectBoxRect(int panelLeft, int panelTop)
        {
            int side = SelectBoxSide();
            int mR = ScaledX(_layout.RcSelectBox.RightMargin ?? 5);
            int mT = ScaledY(_layout.RcSelectBox.TopMargin ?? 2);
            return new Rectangle(
                panelLeft + ScaledX(_layout.PanelInnerWidth) - side - mR,
                panelTop + mT, side, side);
        }

        /// <summary>
        /// 选中框面板内局部矩形（命中用；与 <see cref="GetSelectBoxRect"/> 同源，
        /// 原点在面板内容左上角，绘制与点选永不错位）。
        /// </summary>
        private Rectangle GetSelectBoxLocalRect()
        {
            int side = SelectBoxSide();
            int mR = ScaledX(_layout.RcSelectBox.RightMargin ?? 5);
            int mT = ScaledY(_layout.RcSelectBox.TopMargin ?? 2);
            return new Rectangle(
                ScaledX(_layout.PanelInnerWidth) - side - mR,
                mT, side, side);
        }

        #endregion

        /// <summary>
        /// 批量更新所有面板数据（1Hz 采集周期全量刷新入口）。
        /// 此前尝试"离屏画布缓存 + OnPaint 拷贝"，实测离屏大图上
        /// TextRenderer 每处 ~2.2ms、全量 72 面板高达 2247ms，1Hz 刷新即卡死 UI，
        /// 故回退为"只 Invalidate，OnPaint 只重绘可见区面板"（旧版行为，见 OnPaint）。
        /// 实测屏幕 DC 上 TextRenderer 近 0ms，直接绘制可见区即可流畅。
        /// </summary>
        public void UpdateAll(BarometerData[] allData)
        {
            if (allData == null || allData.Length == 0) return;
            bool changed = false;
            foreach (var data in allData)
            {
                if (data != null && _items.TryGetValue(data.DeviceId, out GridItem item))
                {
                    ApplyData(item, data);
                    changed = true;
                }
            }
            if (changed) Invalidate();
        }

        /// <summary>
        /// 更新单个面板数据（快速跟踪专用，只重绘该面板区域）
        /// 见 UpdateAll 注释：恢复为旧版"仅 Invalidate 面板区域"。
        /// </summary>
        public void UpdateSingle(BarometerData data)
        {
            if (data == null || !_items.TryGetValue(data.DeviceId, out GridItem item)) return;
            ApplyData(item, data);
            Invalidate(GetPanelBounds(data.DeviceId));
        }

        /// <summary>
        /// 设置指定工位的选中状态（用于"设置"按钮点击时确保该工位被选中）
        /// </summary>
        public void SetSelected(int deviceId, bool selected)
        {
            if (_items.TryGetValue(deviceId, out GridItem item) && item.IsSelected != selected)
            {
                item.IsSelected = selected;
                Invalidate(GetPanelBounds(deviceId));
            }
        }        /// <summary>获取当前选中的设备编号数组</summary>
        public int[] GetSelectedDeviceIds()
        {
            var list = new List<int>();
            foreach (var kvp in _items)
            {
                if (kvp.Value.IsSelected) list.Add(kvp.Key);
            }
            return list.ToArray();
        }

        #region 数据应用到显示状态

        /// <summary>
        /// 把设备数据应用到工位显示状态缓存（内存字段，OnPaint 读取）
        /// </summary>
        private void ApplyData(GridItem item, BarometerData data)
        {
            item.PressureText = $"{data.VacuumPressure} kPa";
            // 电流文本：有数显示（如 0.42 A），无数据（NaN）记空串；
            // 改直绘：电流画在面板电流行（见 DrawPanel），无数据画 "--"；
            // 悬停不再带电流（压力框回到原来无提示，见 GetTooltipText）。
            item.CurrentText = float.IsNaN(data.LoadCurrentA) ? "" : $"{data.LoadCurrentA:0.00} A";
            item.SnText = data.SerialNumber ?? "";
            item.RecipeText = data.RecipeName ?? "";
            item.DelayTimeText = data.DelayTime.ToString(@"hh\:mm\:ss");
            item.BurnInTimeText = data.BurnInTime.ToString(@"hh\:mm\:ss");

            // IO 输出状态：OutputStatus[0]=真空电磁阀，OutputStatus[1]=载台上电
            bool vacuumOpen = data.OutputStatus != null && data.OutputStatus.Length >= 1 && data.OutputStatus[0];
            bool carrierPower = data.OutputStatus != null && data.OutputStatus.Length >= 2 && data.OutputStatus[1];

            // 真空块三色（下电逻辑不动，仍只看载台输出；真空灯新增"开了没吸住"的红色）：
            // - 阀没开 → "真空关"灰底（浅色配置灰 / 深色 DimGray，走 GetOffBlockThemeColors）；
            // - 阀开了且负压已达到阈值 → "真空开"绿底（正常）；
            // - 阀开了但负压没达到阈值 → "真空开"红底（管子掉了/漏气/没吸住，开阀即见红，
            //   不用等到超时报警才知道；到位标记 VacuumInRange 由 DeviceManager 按该工位有效阈值填，
            //   面板只显示不判定，与报警同口径）。
            // 【为什么文字保持"真空开"】阀确实开着，红的是"没吸住"这个状态；
            // 改文字会动悬停/截图/用例多处，颜色已足够让现场一眼定位。
            item.VacuumOpen = vacuumOpen;
            item.VacuumText = vacuumOpen ? "真空开" : "真空关";
            if (!vacuumOpen)
            {
                item.VacuumColor = GetOffBlockBack(_darkMode, _colorVacuumOff);
                item.VacuumForeColor = GetOffBlockFore(_darkMode);
            }
            else if (data.VacuumInRange)
            {
                item.VacuumColor = _colorVacuumOn;
                item.VacuumForeColor = Color.White;
            }
            else
            {
                item.VacuumColor = _colorVacuumAlarm;
                item.VacuumForeColor = Color.White;
            }

            // 上电/下电（V1.28：下电由红改浅灰；V1.60.3：深色下走 DimGray 底白字，见 GetOffBlockThemeColors）
            item.CarrierPower = carrierPower;
            item.PowerText = carrierPower ? "上电" : "下电";
            item.PowerColor = carrierPower ? _colorPowerOn : GetOffBlockBack(_darkMode, _colorPowerOff);
            item.PowerForeColor = carrierPower ? Color.White : GetOffBlockFore(_darkMode);

            // 工作状态块已删：状态只看面板底色（空闲白/测试浅黄/故障浅粉/
            // 完成淡钢蓝）＋上电/真空块，不再有文字块。item.Status 照记（切主题重算底色用）。

            // 面板背景色（空闲白/测试浅黄/故障浅粉/完成淡钢蓝；深色下走深色档）
            // 状态同步记到 item.Status：切主题时 RefreshItemBackgrounds 靠它重算底色；
            // 底色取值走 GetStatusBackColor，两处共用，改配色只改一处。
            item.Status = data.Status;
            item.BackColor = GetStatusBackColor(data.Status);
        }

        #endregion

        #region 自绘渲染

        /// <summary>
        /// 自绘整个工位网格（OnPaint 入口）：直画屏幕 DC、只重绘可见列/行（离屏整幅预渲染
        /// 实测慢两个数量级，会卡死 UI）；画刷画笔走缓存；全部绝对坐标（面板左上＋设计坐标）。
        /// ClipRectangle 是物理像素，行列范围先按缩放后宽高算（否则高 DPI 只重绘左上一小块）。
        /// 不再使用 TranslateTransform（避免 TextRenderer 的 GDI 绘制与坐标变换错乱导致文字模糊）。
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            if (_columns == 0) return;

            // e.ClipRectangle 是物理像素坐标，而布局配置是 96DPI 逻辑像素，
            // 所以可见列/行范围计算必须先乘缩放因子，否则 150% 缩放下只重绘左上角一小块。
            int colW = ScaledX(_layout.PanelColumnWidth);
            int rowH = ScaledY(_layout.GetEffectiveRowHeight());

            Rectangle clip = e.ClipRectangle;
            int startCol = Math.Max(0, clip.Left / colW);
            int endCol = Math.Min(_columns - 1, (clip.Right + colW - 1) / colW);
            int startRow = Math.Max(0, clip.Top / rowH);
            int endRow = Math.Min(_rows - 1, (clip.Bottom + rowH - 1) / rowH);

            for (int row = startRow; row <= endRow; row++)
            {
                for (int col = startCol; col <= endCol; col++)
                {
                    int deviceId = row * _columns + col + 1;
                    if (!_items.TryGetValue(deviceId, out GridItem item)) continue;

                    // 面板左上角绝对坐标（面板内容设计尺寸 + 上下左右各 2px 外边距，均按 DPI 放大；
                    // 横向走 zoomX、纵向走 zoomY）
                    int panelLeft = ScaledX(col * _layout.PanelColumnWidth + 2);
                    int panelTop = ScaledY(row * _layout.GetEffectiveRowHeight() + 2);
                    DrawPanel(g, item, panelLeft, panelTop);
                }
            }

            // 行全选按钮列
            if (clip.Right > ScaledX(_columns * _layout.PanelColumnWidth))
            {
                for (int row = startRow; row <= endRow; row++)
                {
                    // 按钮高取"面板内容高-1"（=139）：边框画在矩形下边界，不减 1 底边比面板内容底凸 1px；
                    // Y 与面板同为 row*行高+2 顶部对齐；宽=列宽-左右边距(=44)。
                    Rectangle btnRect = new Rectangle(
                        ScaledX(_columns * _layout.PanelColumnWidth + 2),
                        ScaledY(row * _layout.GetEffectiveRowHeight() + 2),
                        ScaledX(_layout.RowSelectButtonColumnWidth - 4),
                        ScaledY(_layout.GetEffectiveInnerHeight() - 1));
                    DrawRowSelectButton(g, btnRect, row);
                }
            }
        }

        /// <summary>
        /// 绘制单个工位面板（以绝对坐标绘制，panelLeft/panelTop 为面板左上角）。
        /// 面板内部所有元素坐标 = 设计坐标偏移 + 面板左上角。
        /// </summary>
        private void DrawPanel(Graphics g, GridItem item, int panelLeft, int panelTop)
        {
            // 面板背景（状态色），尺寸按 DPI 放大（宽走 zoomX、高走 zoomY）
            using (var bg = new SolidBrush(item.BackColor))
            {
                g.FillRectangle(bg, panelLeft, panelTop, ScaledX(_layout.PanelInnerWidth), ScaledY(_layout.GetEffectiveInnerHeight()));
            }

            // 设备编号（左上角）
            TextRenderer.DrawText(g, $"NO.{item.DeviceId}", _titleFont,
                new Point(panelLeft + ScaledX(_layout.TitlePosition.X), panelTop + ScaledY(_layout.TitlePosition.Y)), _colorText);

            // 状态块（工作状态块已删：第一行只剩上电/下电＋真空开/关；
            // 状态看面板底色＋这两块，不再有文字状态块）
            DrawStatusBlock(g, Offset(Scaled(_layout.RcPower.ToRectangle()), panelLeft, panelTop),
                item.PowerColor, item.PowerForeColor, item.PowerText);
            DrawStatusBlock(g, Offset(Scaled(_layout.RcVacuumOpen.ToRectangle()), panelLeft, panelTop),
                item.VacuumColor, item.VacuumForeColor, item.VacuumText);

            // 值框
            DrawValueBox(g, Offset(Scaled(_layout.RcPressureValue.ToRectangle()), panelLeft, panelTop), item.PressureText);
            // 电流值行（ShowCurrentRow 开才画）：标签 + 值框走锚定矩形
            // （RcCurrentValue/LabelCurrentPosition，与压力/SN 同套路：Scaled 走 DPI、
            // _brushValueBox/_penBorder 复用；无新增命中区，纯展示，命中/DPI 零改动）。
            // 无数据（电表未接/离线）画 "--"（与压力框缺省 "---" 同风格，不留空框）。
            // 关 = 整行不画，布局与原来逐像素一致。
            if (ShowCurrentRow && _layout.RcCurrentValue != null && _layout.LabelCurrentPosition != null)
            {
                DrawValueBox(g, Offset(Scaled(_layout.RcCurrentValue.ToRectangle()), panelLeft, panelTop),
                    string.IsNullOrEmpty(item.CurrentText) ? "--" : item.CurrentText);
            }
            DrawValueBox(g, Offset(Scaled(_layout.RcSNValue.ToRectangle()), panelLeft, panelTop), item.SnText);
            DrawValueBox(g, Offset(Scaled(_layout.RcRecipeValue.ToRectangle()), panelLeft, panelTop), item.RecipeText);
            // 时间框走小 1pt 的时间字（72 框最紧，"00:00:00" 10pt 会截断、9pt 装得下；标签仍是正文 10pt）
            DrawValueBox(g, Offset(Scaled(_layout.RcDelayTimeValue.ToRectangle()), panelLeft, panelTop), item.DelayTimeText, _timeValueFont);
            DrawValueBox(g, Offset(Scaled(_layout.RcBurnInValue.ToRectangle()), panelLeft, panelTop), item.BurnInTimeText, _timeValueFont);

            // 静态标签（X 走 zoomX、Y 走 zoomY；标签列 65px 宽，四字 10pt 实测 65px 刚好装下）
            DrawLabel(g, new Point(panelLeft + ScaledX(_layout.LabelPressurePosition.X), panelTop + ScaledY(_layout.LabelPressurePosition.Y)), "真空压力");
            // "电流："标签（与值框同条件：开才画；关时坐标无意义，不画即可）。
            if (ShowCurrentRow && _layout.LabelCurrentPosition != null)
            {
                DrawLabel(g, new Point(panelLeft + ScaledX(_layout.LabelCurrentPosition.X), panelTop + ScaledY(_layout.LabelCurrentPosition.Y)), "电流：");
            }
            DrawLabel(g, new Point(panelLeft + ScaledX(_layout.LabelSnPosition.X), panelTop + ScaledY(_layout.LabelSnPosition.Y)), "SN:");
            DrawLabel(g, new Point(panelLeft + ScaledX(_layout.LabelRecipePosition.X), panelTop + ScaledY(_layout.LabelRecipePosition.Y)), "配方:");
            DrawLabel(g, new Point(panelLeft + ScaledX(_layout.LabelDelayTimePosition.X), panelTop + ScaledY(_layout.LabelDelayTimePosition.Y)), "延时时间");
            DrawLabel(g, new Point(panelLeft + ScaledX(_layout.LabelBurnInPosition.X), panelTop + ScaledY(_layout.LabelBurnInPosition.Y)), "烧屏时间");

            // 设置按钮（绿底白字；独立大字 _setButtonFont＋深绿底，
            // 白字对比度 2:1→4.6:1，小屏看得清）
            Rectangle rcSet = Offset(Scaled(_layout.RcSetButton.ToRectangle()), panelLeft, panelTop);
            g.FillRectangle(_brushSetButton, rcSet);
            g.DrawRectangle(_penBorder, rcSet);
            TextRenderer.DrawText(g, _layout.SetButtonText, _setButtonFont, rcSet, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            // 选中指示（常显：选中=绿底白✓，未选中=空心白框；无选中时框也在，操作员一眼知道点哪里选中）
            // 框恒正方形：边长取布局矩形缩放后的较小边（双向拉伸下宽≠高，
            // 直接按原矩形画会被压成扁条）；右上位置仍走 RightMargin/TopMargin 锚定。
            {
                Rectangle rcSelect = GetSelectBoxRect(panelLeft, panelTop);
                if (item.IsSelected)
                {
                    g.FillRectangle(_brushSelectChecked, rcSelect);
                    g.DrawRectangle(_penBorder, rcSelect);
                    TextRenderer.DrawText(g, _layout.SelectedMarkText, _panelFont, rcSelect, Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                else
                {
                    g.FillRectangle(_brushSelectUnchecked, rcSelect);
                    g.DrawRectangle(_penBorder, rcSelect);
                }
            }
        }

        /// <summary>
        /// 绘制行全选按钮（浅灰底，该行全部选中时显示"取消"否则"全选"）。
        /// 文字改竖排大字：n 个字按"字高＋正常间隙"排成紧凑一竖块、
        /// 整块在按钮内垂直居中（见 ComputeRowSelectStartY；等分撑满两字离太远、丑）；
        /// 字体走 _rowSelectFont（字号 = 正文 × 配置倍率，RebuildFonts 里跟 zoom 重建）；
        /// 字高/间隙是布局态实测缓存（RefreshRowSelectMetrics），字符取构造缓存，
        /// Paint 里不量字不拼串。
        /// </summary>
        private void DrawRowSelectButton(Graphics g, Rectangle rc, int row)
        {
            g.FillRectangle(_brushRowSelect, rc);
            g.DrawRectangle(_penBorder, rc);
            string[] chars = IsRowAllSelected(row) ? _rowSelectCharsCancel : _rowSelectCharsAll;
            if (chars == null || chars.Length == 0) return;
            int step = _rowSelectCharH + _rowSelectGap;
            int y = ComputeRowSelectStartY(rc.Y, rc.Height, _rowSelectCharH, _rowSelectGap, chars.Length);
            for (int i = 0; i < chars.Length; i++)
            {
                var slot = new Rectangle(rc.X, y + i * step, rc.Width, _rowSelectCharH);
                TextRenderer.DrawText(g, chars[i], _rowSelectFont, slot, _colorText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        /// <summary>绘制状态色块（填充 + 边框 + 文字水平垂直居中）</summary>
        private void DrawStatusBlock(Graphics g, Rectangle rc, Color back, Color fore, string text)
        {
            using (var brush = new SolidBrush(back))
            {
                g.FillRectangle(brush, rc);
            }
            using (var pen = new Pen(_colorBorder))
            {
                g.DrawRectangle(pen, rc);
            }
            TextRenderer.DrawText(g, text, _panelFont, rc, fore,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        /// <summary>
        /// 绘制值框（白底 + 黑边框 + 左对齐垂直居中文字）。
        /// 文字不再贴值框左边框：绘制矩形左移 ValueTextLeftPadding 像素，
        /// 值框本身坐标不变（避免"移动整个框"造成的错位观感）。
        /// </summary>
        private void DrawValueBox(Graphics g, Rectangle rc, string text)
        {
            DrawValueBox(g, rc, text, _panelFont);
        }

        /// <summary>
        /// 绘制值框（字体由调用方指定：延时/烧屏时间框走小 1pt 的时间字，其余走正文字）。
        /// </summary>
        private void DrawValueBox(Graphics g, Rectangle rc, string text, Font font)
        {
            g.FillRectangle(_brushValueBox, rc);
            g.DrawRectangle(_penBorder, rc);
            // 文本绘制矩形 = 值框矩形左移内边距（宽度同步缩短，防止文字溢出到右边框）
            // 内边距按 DPI 放大，保证 150% 缩放下文字仍与值框左边框保持合理间距
            // 内边距是横向量，走 zoomX
            int pad = ScaledX(_layout.ValueTextLeftPadding);
            Rectangle textRc = new Rectangle(
                rc.X + pad,
                rc.Y,
                Math.Max(1, rc.Width - pad),
                rc.Height);
            TextRenderer.DrawText(g, text, font ?? _panelFont, textRc, _colorText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }

        /// <summary>绘制静态标签文字</summary>
        private void DrawLabel(Graphics g, Point location, string text)
        {
            TextRenderer.DrawText(g, text, _panelFont, location, _colorText);
        }

        /// <summary>把面板设计坐标偏移到画布绝对坐标</summary>
        private static Rectangle Offset(Rectangle rc, int panelLeft, int panelTop)
        {
            return new Rectangle(rc.X + panelLeft, rc.Y + panelTop, rc.Width, rc.Height);
        }

        #endregion

        #region 坐标命中与交互

        // GridView_MouseDown 已删：拖拽滚动移除后按下无事可做，点击只看 MouseUp。

        /// <summary>
        /// 鼠标抬起（左键）：行全选按钮→整行翻选；设置区→OnSetClicked；
        /// 选中框/空白→单台翻选（无门槛；取消逐台点或整行"取消"）。
        /// </summary>
        private void GridView_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            if (TryHitRowButton(e.Location, out int row))
            {
                ToggleRow(row);
                return;
            }

            if (TryHitPanel(e.Location, out int deviceId, out Point local))
            {
                // local 是物理像素，布局矩形先缩放再比；选中框命中与绘制同源，不漂移
                Rectangle rcSet = Scaled(_layout.RcSetButton.ToRectangle());
                Rectangle rcSelect = GetSelectBoxLocalRect();
                if (rcSet.Contains(local))
                {
                    OnSetClicked?.Invoke(this, deviceId);
                    return;
                }
                if (rcSelect.Contains(local))
                {
                    ToggleSelect(deviceId);
                    return;
                }
                // 单击空白区域：直接切换选中状态
                ToggleSelect(deviceId);
            }
        }

        /// <summary>鼠标移动：状态块悬停提示（文本变了才重 Show，防闪）</summary>
        private void GridView_MouseMove(object sender, MouseEventArgs e)
        {
            string tip = GetTooltipText(e.Location);
            if (tip != _lastTooltipText)
            {
                _lastTooltipText = tip;
                if (string.IsNullOrEmpty(tip))
                {
                    _toolTip.Hide(this);
                }
                else
                {
                    _toolTip.Show(tip, this, new Point(e.X + 12, e.Y + 12), 1500);
                }
            }
        }

        /// <summary>
        /// 鼠标离开控件：隐藏悬停提示
        /// </summary>
        private void GridView_MouseLeave(object sender, EventArgs e)
        {
            _lastTooltipText = "";
            _toolTip.Hide(this);
        }

        /// <summary>
        /// 单台翻选（选中框常显：所有面板右上永远有框，所见即所得；单台只局部重绘）。
        /// </summary>
        private void ToggleSelect(int deviceId)
        {
            if (_items.TryGetValue(deviceId, out GridItem item))
            {
                item.IsSelected = !item.IsSelected;
                Invalidate(GetPanelBounds(deviceId));
            }
        }

        /// <summary>切换整行选中状态（全选 ↔ 取消全选）</summary>
        private void ToggleRow(int row)
        {
            int startDeviceId = row * _columns + 1;
            int endDeviceId = Math.Min(startDeviceId + _columns - 1, _totalDevices);
            bool allSelected = IsRowAllSelected(row);

            bool newState = !allSelected;
            for (int d = startDeviceId; d <= endDeviceId; d++)
            {
                if (_items.TryGetValue(d, out GridItem item))
                {
                    item.IsSelected = newState;
                }
            }
            Invalidate();

            OnLog?.Invoke(this, $"第 {row + 1} 行 {(newState ? "全选" : "取消全选")}（设备 {startDeviceId}-{endDeviceId}）");
        }

        /// <summary>该行是否全部工位都被选中</summary>
        private bool IsRowAllSelected(int row)
        {
            int startDeviceId = row * _columns + 1;
            int endDeviceId = Math.Min(startDeviceId + _columns - 1, _totalDevices);
            for (int d = startDeviceId; d <= endDeviceId; d++)
            {
                if (_items.TryGetValue(d, out GridItem item) && !item.IsSelected)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 坐标命中面板：返回设备编号与面板内局部坐标
        /// p 是物理像素坐标，需与缩放后的列宽/行高比对
        /// 面板间隙不命中：行列整除会把面板之间的缝隙算进上一格，
        /// 点缝隙翻选上一个面板是 bug；local 落在面板内容矩形外一律返回 false
        /// （悬停提示同步消失，见 GetTooltipText）。
        /// </summary>
        private bool TryHitPanel(Point p, out int deviceId, out Point local)
        {
            deviceId = 0;
            local = Point.Empty;
            if (_columns == 0) return false;

            // 列宽走 zoomX、行高走 zoomY（与绘制同口径，否则点选错位）
            int colW = ScaledX(_layout.PanelColumnWidth);
            int rowH = ScaledY(_layout.GetEffectiveRowHeight());
            if (p.X < 0 || p.Y < 0 || p.X >= ScaledX(_columns * _layout.PanelColumnWidth) || p.Y >= ScaledY(_rows * _layout.GetEffectiveRowHeight())) return false;

            int col = p.X / colW;
            int row = p.Y / rowH;
            if (col >= _columns || row >= _rows) return false;

            deviceId = row * _columns + col + 1;
            if (deviceId > _totalDevices) return false;

            // 面板内局部坐标 = 鼠标物理坐标 - 面板左上角物理坐标（含 2px 外边距，已缩放）
            local = new Point(p.X - ScaledX(col * _layout.PanelColumnWidth + 2), p.Y - ScaledY(row * _layout.GetEffectiveRowHeight() + 2));
            // 内容 bounds：local 原点在面板内容左上角，落在内容外 = 点在面板间隙上
            // （左右缝 local.X 越界、上下缝 local.Y 越界），不命中任何面板。
            if (local.X < 0 || local.Y < 0
                || local.X >= ScaledX(_layout.PanelInnerWidth)
                || local.Y >= ScaledY(_layout.GetEffectiveInnerHeight()))
            {
                deviceId = 0;
                local = Point.Empty;
                return false;
            }
            return true;
        }

        /// <summary>坐标是否命中行全选按钮列，返回行号（物理像素比对）</summary>
        private bool TryHitRowButton(Point p, out int row)
        {
            row = -1;
            // 【大扫荡】补右界：以前按钮列右侧空白也命中整行翻选；
            // 右界=绘制右界（左界+列宽；绘制宽=列宽-4，命中比绘制宽 2px，
            // 点到按钮右边缝也算——按钮列已是控件最右缘，无他物不误触；
            // 与 TryHitPanel 同为严格右界口径，不含容差）。
            int left = ScaledX(_columns * _layout.PanelColumnWidth);
            int right = left + ScaledX(_layout.RowSelectButtonColumnWidth);
            if (p.X < left || p.X >= right
                || p.Y < 0 || p.Y >= ScaledY(_rows * _layout.GetEffectiveRowHeight())) return false;
            row = p.Y / ScaledY(_layout.GetEffectiveRowHeight());
            return row >= 0 && row < _rows;
        }

        /// <summary>根据坐标返回命中的状态块悬停提示文本（未命中返回 null）</summary>
        private string GetTooltipText(Point p)
        {
            if (!TryHitPanel(p, out int deviceId, out Point local)) return null;
            // local 是物理像素坐标，布局矩形需缩放后比较
            if (Scaled(_layout.RcPower.ToRectangle()).Contains(local)) return "上电状态：绿=上电，浅灰=下电";
            if (Scaled(_layout.RcVacuumOpen.ToRectangle()).Contains(local)) return "真空状态：阀开且负压到位=绿底 / 阀开但没吸住=红底 / 阀没开=灰底";
            // 压力框悬停回退到原来（无提示）：电流已改直绘（电流行），
            // 悬停不再承担"看得到电流"的需求，压力框回到 V1.73 及以前的无提示行为。
            return null;
        }

        /// <summary>获取指定工位面板在画布中的边界（物理像素，用于局部重绘）</summary>
        private Rectangle GetPanelBounds(int deviceId)
        {
            int index = deviceId - 1;
            int col = index % _columns;
            int row = index / _columns;
            return new Rectangle(ScaledX(col * _layout.PanelColumnWidth), ScaledY(row * _layout.GetEffectiveRowHeight()),
                                 ScaledX(_layout.PanelColumnWidth), ScaledY(_layout.GetEffectiveRowHeight()));
        }

        #endregion

        /// <summary>
        /// 单个工位的显示状态缓存（内存字段，OnPaint 读取）
        /// </summary>
        private class GridItem
        {
            public int DeviceId;
            /// <summary>
            /// 工位当前业务状态（：切主题重算底色用；平时由 UpdateSingleItem 随采集刷新）。
            /// 为什么要记它：GridItem 原来只记颜色不记状态，切主题时想重算底色就找不到依据，
            /// 只好把状态也记下来（就是 DeviceStatus 空闲/测试/故障/完成那几个值）。
            /// </summary>
            public DeviceStatus Status;
            /// <summary>
            /// 载台是否上电 / 真空阀是否打开（：切主题重算下电/真空关块色用；
            /// 平时由 ApplyData 随采集刷新。只记开关不记颜色，颜色永远由当前主题现算；
            /// 真空开块的绿/红（到位/没吸住）两边主题都不动，同样不用记，只重算灰色的关块）。
            /// </summary>
            public bool CarrierPower;
            public bool VacuumOpen;
            public string PressureText = "---";
            /// <summary>
            /// 载台电流文本（Q2 骨架：ApplyData 随采集刷新，有数如"0.42 A"，
            /// 无数据记空串；用于电流行直绘（ShowCurrentRow 开时画，无数据画 "--"）。
            /// </summary>
            public string CurrentText = "";
            public string SnText = "";
            public string RecipeText = "";
            public string DelayTimeText = "00:00:00";
            public string BurnInTimeText = "00:00:00";
            public Color PowerColor = Color.LightGray;
            public Color PowerForeColor = Color.Black;
            public string PowerText = "下电";
            public Color VacuumColor = Color.LightGray;
            public Color VacuumForeColor = Color.Black;
            public string VacuumText = "真空关";
            public Color BackColor = Color.White;
            public bool IsSelected;
        }
    }
}
