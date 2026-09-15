using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AgingTestSystem.Models;

namespace AgingTestSystem.Views
{
    /// <summary>
    /// 工作站网格自适应（【V1.88.22 曾新增双模式开关 → 【V1.88.24】已删除）。
    /// 只剩一路：双向精确铺满一屏（zoomX=可用宽/内容宽、zoomY=可用高/内容高独立，
    /// 72 站一屏无滚动条；面板允许宽扁拉伸，字取窄边不变形）。
    /// 判定逻辑只走配套的纯函数（ComputeFitZoom，可单测），
    /// 执行侧 <see cref="WorkstationGridView.UpdateAutoFit"/> 直接双向铺满（DeviceManager 不碰显示）。
    /// 【V1.88.24 删除清单】WorkstationFitMode 枚举/FitWidth/FitMode/单轴 ComputeFitZoom/
    /// MinZoom-MaxZoom 钳制/拖拽滚动（字段/定时器/捕获/滚动同步/事后校正）全部移除
    /// （用户确认：一屏看全够了，不要上下滑动）。
    /// </summary>

    /// <summary>
    /// 工位网格（自绘大画布）——【V1.51 布局外部化 + 文字糊修复】
    ///
    /// 【方案说明】
    /// 原实现为"TableLayoutPanel + 72 个面板控件"，滚动时 WinForms 要逐帧移动 72 个
    /// 子控件窗口（且每个面板内部还有多个子控件），多窗口移动彼此不同步，
    /// 拖动滚动条必然撕裂/卡顿。V1.50 改为 RecyclerView 同源的"单窗口大画布"：
    /// - 整个网格（8列×9行面板 + 行全选按钮列）合并为 **1 个自绘 UserControl**，
    ///   尺寸 = 显示区尺寸（双向铺满一屏），放入外层 Panel 容器中；
    /// - 单窗口自绘，无 72 控件窗口管理开销 → 无撕裂、无滚动条；
    /// - 72 个面板全部由本控件 OnPaint 按坐标绘制，且只重绘可见区域（按 ClipRectangle 算行列范围）；
    /// - 交互（单击选中 / 设置按钮 / 选中框 / 行全选 / 悬停提示）全部用坐标命中实现。
    ///   【V1.88.14】选中框常显后长按选中整套删除：点选中框或面板空白即切换选中
    ///   （鼠标与触摸屏同走 MouseUp 点选，无需区分）；
    ///   【V1.88.24】拖拽滚动整套删除（只留一屏铺满）：按下拖动不再转滚动，
    ///   抬起一律按点击处理（点框/空白翻选、点设置区开窗）。
    ///
    /// 【V1.57.2 性能优化（含回退）】
    /// 先尝试"离屏画布缓存：整幅 RenderToCanvas + OnPaint DrawImage 拷贝"，实测离屏大图
    /// （2040×2025）上 TextRenderer 每处约 2.2ms，全量 72 面板高达 2247ms，且 UpdateAll
    /// 每秒全量渲染、选中翻转也全量 → 整个 UI 卡死。故回退为旧版"OnPaint 只重绘可见区"，
    /// 保留仍然有效的优化：
    /// - 画刷/画笔缓存字段（_penBorder/_brushValueBox 等）：绘制热路径不再每帧 new GDI 对象。
    /// 【V1.88.24】16ms 拖拽滚动合并定时器（_dragScrollTimer）随拖拽滚动整套删除。
    /// 实测屏幕 DC 上 TextRenderer 近 0ms，直接绘制可见区流畅无卡顿。
    ///
    /// 【V1.51 修复：文字"糊成一坨"】
    /// 原实现 OnPaint 用 g.TranslateTransform 平移坐标系后再用 TextRenderer.DrawText 绘制。
    /// TextRenderer 走 GDI 绘制路径，与 Graphics 的坐标变换叠加时**位置/大小会错乱**，
    /// 导致文字溢出到相邻元素上互相叠加，看起来糊成一坨。
    /// 本版本所有元素（背景/状态块/值框/标签/按钮/文字）一律改为**绝对坐标**绘制，
    /// 彻底去掉 TranslateTransform 与 GDI 文字绘制混用的问题。
    ///
    /// 【V1.51 布局外部化】
    /// 面板坐标/颜色/字号/提示文字全部来自 <see cref="PanelLayoutConfig"/>（默认内置，
    /// 可通过程序目录下 PanelLayout.json 覆盖）。现场微调界面只改配置文件、无需重新编译。
    /// 【V1.58.13~1.58.17 锚定机制】
    /// 面板内元素已全部改为"锚定"（字段声明相对关系，加载时统一解析），不再依赖绝对坐标：
    /// - 右缘元素锚定 View 右缘（RightMargin）；多个元素右缘对齐基准元素（RightAlignTo）；
    /// - 左缘/双端锚定（LeftAlignTo / RightToLeftAlignTo）；标签右缘贴目标左缘（Width+RightToLeftAlignTo）；
    /// - 编号/选中框左上、右上锚定（LeftMargin/TopMargin）。
    /// 锚定字段全表、三步解析流程、完整依赖链与调整指南见 <see cref="PanelLayoutConfig"/> 类头注释
    /// （改坐标前务必先读，顺序/互斥/字体依赖等坑都在里面）。
    /// 【V1.58.19 垂直锚定（自下而上链，只声明关系、位置零变化）】
    /// 新增 BottomMargin / BottomToTopAlignTo(+BottomToTopGap) / VerticalCenterAlignTo(+CenterOffsetY)
    /// 等垂直锚定字段。**原则：锚定只声明"以谁为基准、距离多少"，各间距取当前实际空隙，
    /// 解析结果与 V1.58.18 布局完全一致（视觉零变化）**：
    /// - 设置按钮：以面板下缘为基准，BottomMargin（下缘距面板底；V1.58.19 时=10/Y=145，
    ///   【V1.88.17】现=8/Y=120，见 PanelLayoutConfig 类头）；
    /// - 配方框：以设置按钮上缘为基准（V1.58.19 时 Gap=6/Y=118；【V1.88.17】现 Gap=4/Y=98）；
    /// - 【V1.77】SN 框改吊电流行下方、真空关/压力框改吊真空块下方（自上而下链，
    ///   位置零变化，见下"V1.77 开态几何"；【V1.88.17】现 Gap=2/8：SN 关电流=76、压力 Y=54）；
    /// - 延时时间/烧屏时间：以设置按钮中心为基准对称分布（V1.58.19 时偏移 -12/+13、Y=147/172，
    ///   【V1.88.17】现偏移 ±11、Y=121/143，见 PanelLayoutConfig 类头）；
    /// - 各标签：以各自框中心为基准，VerticalCenterOffset=-1（Y=70/96/121/150/175 不变）。
    /// 改 PanelInnerHeight 时下链（按钮/配方/延时）按各自间距自动联动，上链不动（【V1.77】）。
    /// 旧版 PanelLayout.json 无这些新字段
    /// (反序列化为 null) 会导致垂直锚定不生效，需同步 json 或删除让程序重新导出（见 CHANGELOG V1.58.19）。
    /// 【V1.51】值框左边界与左侧标签文字间距加大（值框 X 由 57→62，宽相应缩短），
    /// 解决"数据框紧贴左侧标签"问题；间距可在 PanelLayout.json 中微调。
    ///
    /// 【V1.55 高DPI适配】
    /// 本控件是 AutoScaleMode.None 的自绘控件，若完全脱离 DPI，150% 缩放下会出现：
    /// 画布尺寸（逻辑像素）不放大、而 pt 字体的文字自动变大 → 文字溢出格子、重叠，
    /// 且与周围被 AutoScaleMode.Font 放大的标准控件比例失调（"界面显示不正常"）。
    /// 适配方案：布局配置仍是"96DPI 逻辑像素"，但 <see cref="UpdateDpiScale"/> 在
    /// 句柄创建后计算缩放因子 _dpiScale = DeviceDpi / 96（150% 缩放下 = 1.5），
    /// 所有绘制/命中坐标、画布尺寸一律经 ScaledX/ScaledY 放大（【V1.88.17】横向走 zoomX、
    /// 纵向走 zoomY），字体保持
    /// pt 单位自动放大 → 文字与格子同步放大、比例与 96DPI 完全一致。
    /// 注意：不能用 Graphics.ScaleTransform，因为 TextRenderer 走 GDI 不认坐标系变换
    /// （见上方 V1.51 踩坑），必须手动把每个坐标乘缩放因子。
    ///
    /// 【界面布局】
    /// 一、整体结构（外层 Panel 容器 + 本控件 = 画布，无滚动条）
    /// ┌───────────────────────────────────────────┬──────────┐
    /// │             画布（本控件 OnPaint）         │ 行全选列 │
    /// │ ┌──────┬──────┬──────┬──────┬──────┬───  │ ├──────────┤
    /// │ │ NO.1 │ NO.2 │ NO.3 │ NO.4 │ NO.5 │ ... │ │ [全]     │ ← 第1行（【V1.88.28】竖排大字：
    /// │ ├──────┼──────┼──────┼──────┼──────┼───  │ │ [选]     │   "全/选"按正常字隙排成紧凑一竖块、
    /// │ │ NO.9 │ NO.10│ NO.11│ NO.12│ NO.13│ ... │ │ [全]     │   整块居中（等分撑满太散）；
    /// │ │ ...  │ ...  │ ...  │ ...  │ ...  │ ... │ │ [选]     │   字号=正文×RowSelectFontScale；
    /// │ └──────┴──────┴──────┴──────┴──────┴───  │ ├──────────┤
    /// │ 8列（列宽209，每格内容204+左右边距各2）      │ 行内全部   │
    /// │ × 9行（行高182，每格内容170+上下缝12）      │ 选中→[取消]│
    /// │ （竖排：[取]/[消]上下两格）                  │          │
    /// └───────────────────────────────────────────┴──────────┘
    /// （保持 8×9=72，不动列数；【V1.88.17】面板 222×205 紧凑到 204×170，
    /// 内容 1896×2025→1736×1638，纵向缩放压力大减）
    /// 【V1.58.8】行全选按钮高 = 面板内容高-1(=169)，含边框后上下边缘与工作站显示框(170)完全对齐；
    /// 按钮矩形 = (列右缘+2, 行顶+2, 列宽-4, PanelInnerHeight-1)；-1 修正边框底凸出1px
    /// 网格占满全部 72 台设备。
    /// 【V1.88.24 单一铺满】AutoFit=true 即双向精确铺满一屏（FitWidth/FitMode 双模式开关已删，
    /// 只留这一路）：zoomX=可用宽/内容宽、zoomY=可用高/内容高独立，画布精确等于显示区客户区，
    /// 纵向横向滚动条都不出（面板允许宽扁拉伸，字取窄边不变形）。窗口拉大/缩小/最大化跟随缩放；
    /// 字体取窄边等比缩放、下限见 MinFontSize（【V1.88.21】6pt→4pt，
    /// 1280×1024小屏跟随缩小不挤叠）；列数保持 8×9 不动。
    /// 显示区被挤到极小时 zoom 照算（不再钳 MinZoom，也不转滚动条兜底）：72 站永远一屏看全，
    /// 字保 MinFontSize 可读。关掉 AutoFit 回原尺寸（超出部分直接裁掉、无滚动条）。
    /// 实现见 ComputeFitZoom/UpdateAutoFit/RebuildFonts/UpdateCanvasSize
    /// （zoomX/zoomY 并进 ScaledX/ScaledY）。
    ///
    /// 二、单个面板内容（【V1.88.17】204×170，坐标均为"相对面板左上角"；
    /// 【V1.88.16】工作状态块已删，真空块搬去第一行；本版紧凑：状态块 60×23→56×20、
    /// 值框 148×21→130×18、延时框 80→66、设置按钮 60×50→50×42、选中框 23→18）：
    /// ┌──────────────────────────────────────────────┐
    /// │ NO.1（标题，左上角）            ┌────────────┐│
    /// │ ┌──────────┐  ┌──────────┐     │ 选中指示框  ││ ← 右上角 23×23
    /// │ │ 上电/下电 │  │ 真空开/关 │     │ (绿底白✓)  ││    选中框常显
    /// │ └──────────┘  └──────────┘     └────────────┘│
    /// │                                              │
    /// │ 真空压力 ┌────────────────────────────────┐  │
    /// │          │  78 kPa                        │  │
    /// │          └────────────────────────────────┘  │
    /// │ SN:    ┌────────────────────────┐             │
    /// │ 配方:  ┌────────────────────────┐             │
    /// │        └────────────────────────┘             │
    /// │ 延时时间 ┌────────────┐   ┌─────────────────┐ │
    /// │          │ 00:00:00   │   │      设置       │ │ ← 绿底白字
    /// │ 烧屏时间 ┌────────────┘   └─────────────────┘ │
    /// │          │ 00:00:00  │                        │
    /// │          └───────────┘                        │
    /// └──────────────────────────────────────────────┘
    /// 标注说明（括号内为锚定关系，【V1.88.17】紧凑值）：
    /// - 行1：上电/下电块(65,26,56,20；Y/H 对齐真空块） + 真空开/关块(139,26,56,20；
    ///   右缘对齐设置按钮＋TopMargin=26） + 选中框（右上：边长取 18 缩放后较小边，
    ///   恒正方形跟面板走，1080p 下约 10×10，见 SelectBoxSide）
    /// - 行2：真空压力值框(65,54,130,18；左缘对齐 SN 框＋右缘对齐设置按钮，宽 130；
    ///   Y 吊真空块下方 TopToBottomGap=8，Y=26+20+8=54)
    ///   + 【V1.77】电流值框 RcCurrentValue(65,74,71,18；左缘 SN/右缘贴真空关左缘-3，
    ///   Y 吊压力框下方 Gap=2；ShowCurrentRow 关=整行不画；开=面板 170→188、SN 76→94、
    ///   配方 98→116、延时 121/143→139/161、按钮 120→138，间距全都不变，见下方"V1.77 开态几何")
    /// - 行3：SN 值框(65,76,130,18；【V1.77】改吊电流行下方 TopToBottomGap=2：
    ///   关电流电流行高按 0，Y=74+0+2=76；开时 Y=74+18+2=94)
    /// - 行4：配方值框(65,98,130,18；下缘贴设置按钮上缘、Gap=4)
    /// - 行5：延时时间值框(65,121,66,18；以设置按钮中心为基准、CenterOffsetY=-11) +
    ///   设置按钮(145,120,50,42；下缘距面板底 BottomMargin=8) + 烧屏时间值框(65,143,66,18；CenterOffsetY=11)
    /// - 编号：NO.1(9,4)（LeftMargin=9 + TopMargin=4）
    /// - 标签列：真空压力(9,56)/SN:(9,78)/配方:(9,100)/延时时间(9,123)/烧屏时间(9,145)
    ///   （X=9 为右缘贴合压力框左缘推导 65-56=9；Y 以各自框中心为基准、VerticalCenterOffset=-1）
    /// - 【V1.58.20 内容居中 + 选中框上移】编号/标签列左缘 LeftMargin=9，设置按钮右缘贴右
    ///   （RightMargin=9），左留白 9 = 右留白 9 → 面板内内容整体水平居中
    ///   （V1.58.20 时右缘=213/宽 222；【V1.88.17】现右缘=195/宽 204，对称关系不变）；
    ///   选中框 TopMargin 4→2（Y=2，底缘 25 与真空块上缘 29 间距由 2px 加大到 4px）。
    /// - 值框文字左内边距：ValueTextLeftPadding=6px（V1.52，文字不贴值框左边框，值框坐标不变）
    /// - 状态块配色见下方"状态块配色"；颜色值均可由 PanelLayout.json 覆盖
    /// - 【V1.58.6 对齐】延时时间/烧屏时间两行中心与设置按钮中心垂直居中对齐
    ///   （V1.58.6 时 (157.5+182.5)/2=170=(145+25)；【V1.88.17】现 (130+152)/2=141=(120+21)）；
    ///   V1.58.19 起改为 VerticalCenterAlignTo 锚定自动保持居中。
    /// - 【V1.58.7 右对齐】空闲/真空关/SN框/配方框/设置按钮五者右边缘统一 = 205：
    ///   工作状态块右移 X=153、真空关宽调成与空闲一致(48→52)并右移 X=153、
    ///   SN/配方加宽至 148；真空压力框加宽至 93（右边缘=150，与真空关左边缘 153 保持 3px）。
    ///   校验：下电块(52×23)与工作状态块(52×23)尺寸一致。
    /// - 【V1.58.9 左右同界】空闲/真空关左边缘与设置按钮左边缘对齐：工作状态块、真空开/关块
    ///   X=153→145、W=52→60，左右边缘均=145/205（与设置按钮 145/205 完全重合）；
    ///   下电 W 同步 52→60 保持一致；真空压力框缩窄 93→85（右=142，与真空关左 145 保持 3px）。
    /// - 【V1.58.11 撤销居中+缩面板】V1.58.10 整体右移 16px 居中的观感不佳（偏左的根因是面板
    ///   过宽、右侧留白太多，而非坐标偏左），故 X 全部还原为 V1.58.9 布局，改由缩小面板宽度
    ///   减小右空隙：PanelInnerWidth 240→222（右空隙 35→17px）、PanelColumnWidth 245→227。
    ///   V1.58.6~1.58.9 的各项对齐不受影响。
    /// - 【V1.58.12 选中框回界】面板缩至 222 后，选中框原 X=212（右缘 235）溢出面板，左移 X=194
    ///   （右缘 217），与面板右边距保持 5px。
    /// - 【V1.58.13 右侧锚定】ElementRect 新增可选 RightMargin（右侧锚定边距）：选中框(5)、
    ///   空闲/真空关/SN/配方/设置按钮(17) 改为锚定，加载时 X 自动 = PanelInnerWidth - RightMargin - Width。
    ///   以后改面板宽度（PanelInnerWidth）右缘元素自动跟随，不再手改坐标（V1.58.11/12 的坑）。
    /// - 【V1.58.14 链式锚定】设置按钮锚定 View 右缘(RightMargin=17)；【V1.88.16】工作状态块已删，
    ///   真空块接替当上链基准（见 PanelLayoutConfig 类头"完整锚定链"）；下电改对齐真空块。
    ///   解析顺序：先 RightMargin 面板锚定，再 RightAlignTo/VerticalAlignTo 元素间锚定。
    /// - 【V1.58.15 双端锚定】真空压力框 LeftAlignTo="SNValue"（左缘对齐 SN 框左缘）+
    ///   RightToLeftAlignTo="VacuumOpen"（右缘贴合真空关左缘），宽度自动=145-57=88；
    ///   下电 LeftAlignTo="PressureValue"（左边缘与压力框左边缘对齐）。
    /// - 【V1.58.16 标签锚定】"真空压力"标签 Width=56 固定文字宽 + RightToLeftAlignTo="PressureValue"
    ///   （右缘贴合压力框左缘，X=57-56=1）；SN:/配方:/延时时间/烧屏时间 四标签 LeftAlignTo="LabelPressure"
    ///   （左缘对齐"真空压力"标签）。
    /// - 【V1.58.17 边缘锚定】编号 TitlePosition 左上角锚定（LeftMargin=3 + TopMargin=4）；
    ///   选中框右上角锚定（RightMargin=5 + TopMargin=4）；延时时间/烧屏时间值框补左缘锚定
    ///   LeftAlignTo="SNValue"（跟随值框列）。至此全部元素均已锚定，改面板宽/高基本布局不变。
    /// - 【V1.58.19 垂直锚定链（【V1.77】压力/真空关/SN 改走自上而下链 TopToBottom，位置零变化；
    ///   SN→配方之间改为两链交接缝，缺省高度下间距仍 4px，详见 PanelLayoutConfig 类头"完整锚定链（V1.77）"）】
    ///   保持位置零变化（【V1.88.17】现值：按钮 BottomMargin=8；配方 Gap=4；
    ///   压力 Gap=8；SN Gap=2，关电流 Y=76。V1.58.19 时为 10/6/15/3，见 PanelLayoutConfig 类头）；
    ///   延时两行 VerticalCenterAlignTo="SetButton"+CenterOffsetY=-12/+13(以按钮中心为基准对称)；
    ///   各标签 VerticalCenterAlignTo 各自框+offset=-1。改 PanelInnerHeight 时下链自动联动，
    ///   上链（真空块及以上+压力/电流/SN）不动，差值由交接缝吸收。
    /// - 【V1.77 开态几何】【V1.88.17】ShowCurrentRow=true（UsePowerMeter 开）时单面板内容
    ///   204×188（行高 182→200），压力行(54)及以上逐像素不动，新增电流行(74,高18)+标签"电流："，
    ///   SN(94)/配方(116)/延时(139/161)/按钮(138)整体下移 18，间距全都不变；
    ///   false 时与本图逐像素一致。开关走 ShowCurrentRow 属性（MainForm 按 UsePowerMeter 装配一次），
    ///   行高/画布/命中一律走 GetEffectiveRowHeight()/GetEffectiveInnerHeight()，禁止手写 170/182。
    /// - 值框文字左内边距：ValueTextLeftPadding=6px（V1.52，文字不贴值框左边框，值框坐标不变）
    /// - 状态块配色见下方"状态块配色"；颜色值均可由 PanelLayout.json 覆盖
    ///
    /// 【状态块配色（V1.28 约定；【V1.88.16】工作状态块已删，状态只看面板底色＋上电/真空块；
    /// 【V1.88.29】绿统一加深为 ForestGreen：白字对比度 2:1→4.6:1，与设置按钮/各窗绿按钮同色）】
    /// - 上电/下电：绿=ForestGreen=上电，浅灰=LightGray=下电
    /// - 面板背景：空闲=白 / 测试中=浅黄 / 故障=浅粉 / 已完成·待取料=淡钢蓝（V1.59）
    /// - 真空块三色：阀开且负压到位=绿底 / 阀开但没吸住=红底（真空开，ColorVacuumAlarm）/
    ///   阀没开=灰底（真空关，浅色配置灰/深色 DimGray）
    ///
    /// 【数据流】
    /// 主窗体收到设备批量更新后调用 <see cref="UpdateAll"/> / <see cref="UpdateSingle"/>，
    /// 仅更新内存字段 + Invalidate，1Hz 全量刷新开销极小，完全不影响实时监控。
    /// </summary>
    public partial class WorkstationGridView : System.Windows.Forms.UserControl
    {
        // ===== 布局配置 =====
        /// <summary>面板布局配置（默认内置，可被 PanelLayout.json 覆盖）</summary>
        private readonly PanelLayoutConfig _layout;

        // ===== 字体 =====
        // 【V1.88.14】去掉 readonly：自适应缩放（AutoFit）按 _zoom 重建字号，
        // 释放旧字体防 GDI 泄漏（Dispose 已释放两者，见 Designer）。
        /// <summary>面板正文文字字体（显式创建，不继承主窗体缩放字体，保证与小矩形匹配）</summary>
        private Font _panelFont;
        /// <summary>设备编号标题字体（【V1.88.29】缺省 12pt：左上独占行，槽位宽裕）</summary>
        private Font _titleFont;
        /// <summary>
        /// 设置按钮字体（【V1.88.29 新增】独立大字：按钮框 50×42，"设置"两字在正文字号下只占角落；
        /// 字号取配置 SetButtonFontSize（缺省 12），跟 zoom 等比缩放，与 RebuildFonts 同建同释放）。
        /// </summary>
        private Font _setButtonFont;
        /// <summary>
        /// 行全选按钮字体（【V1.88.28 新增】竖排大字：字号 = 正文字号 × 配置倍率，
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

        // ===== 配置解析出的颜色（浅色值来自 PanelLayoutConfig，可被 PanelLayout.json 覆盖） =====
        // 【V1.60 深色模式】以下"跟随主题切换"的颜色去掉 readonly，SetDarkMode 里整体换肤；
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
        private Color _completedColor;     // 【V1.59】面板背景-已完成·待取料（浅色淡钢蓝 / 深色深蓝）
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
        /// DPI 缩放因子 = DeviceDpi / 96（【V1.55 高DPI适配】）。
        /// 布局配置里的坐标/尺寸都是"96DPI 逻辑像素"，在 150% 缩放的屏幕上
        /// 必须整体放大 DeviceDpi/96 倍，否则格子不变、而 pt 字体的字会自动变大，
        /// 导致文字溢出格子、与周围被 AutoScaleMode.Font 放大的控件比例失调。
        /// 由于 TextRenderer 走 GDI 不能配合 Graphics 坐标变换（见头部 V1.51 踩坑），
        /// 这里采用"手动把所有逻辑像素乘 _dpiScale"的方式，字体保持 pt 单位自动放大，比例一致。
        /// </summary>
        private float _dpiScale = 1f;

        /// <summary>
        /// 自适应缩放因子（【V1.88.14 新增按宽顶满 → 【V1.88.17】双向铺满 →
        /// 【V1.88.22】双模式 → 【V1.88.24】删回单路铺满：只有双向独立这一路）。
        /// zoomX=可用宽/内容宽、zoomY=可用高/内容高，画布精确等于显示区客户区，
        /// 纵向横向滚动条都不出。zoom 并进布局基准（最终比例 sx = _dpiScale × _zoomX、
        /// sy = _dpiScale × _zoomY，绘制/命中/画布尺寸全走 ScaledX/ScaledY，
        /// 不碰 Graphics 变换矩阵——与 V1.55 DPI 同路、V1.82 画布缩放同口径）；
        /// 字体按 min(zoomX, zoomY) 等比缩放重建（pt 单位，下限见 MinFontSize，
        /// 见 RebuildFonts，字不变形，窄边决定字号）。
        /// 【V1.88.17】面板同步紧凑到 204×170（见 PanelLayoutConfig），内容高 2025→1638，
        /// 1080p 下 zoomY 由 0.44 升到约 0.55，6pt 字在 18 高值框内放得下。
        /// 【V1.88.21】1280×1024下zoomY≈0.45，旧6pt下限把字卡大1.5倍致标签挤叠，
        /// 下限降到4pt跟随缩小（见 MinFontSize）。
        /// </summary>
        private float _zoomX = 1f;

        /// <summary>纵向自适应缩放因子（与 _zoomX 独立，面板允许宽扁拉伸，字取窄边）</summary>
        private float _zoomY = 1f;

        // 【V1.88.24】MinZoom/MaxZoom 钳制已删：铺满要求 zoom 精确等于可用/内容，
        // 钳住即铺不满（大屏留白边/小屏被裁）；显示区再小也照算，72 站永远一屏，
        // 字保 MinFontSize 可读（不再转滚动条兜底，滚动条已整套移除）。

        /// <summary>
        /// 自适应字号下限（【V1.88.21 1280×1024小屏适配】6pt→4pt）。
        ///
        /// 【为什么从6降到4】1280×1024下可用区约972×736，zoomX≈0.56、zoomY≈0.45，
        /// 窄边0.45×9pt=4.04pt才是"格子与字等比"的理想字号；旧下限6pt把字卡在
        /// 理想的1.5倍：4字标签"真空压力"实测37px vs 标签槽31px（56×0.56）溢出6px，
        /// 直接盖到右边值框上（用户报的"标签挤叠"）；值框高18×0.45≈8px也装不下
        /// 6pt的11px字高，上下也被夹。降到4pt后字体跟随缩小：理想4.04pt≥下限，
        /// 标签约25px vs 31px（余6px）、"00:00:00"约27px vs 延时框37px、
        /// 字高约7px vs 框高8px，横竖都放得下。1080p等大屏zoom≈0.55理想4.9pt，
        /// 同样跟随缩小（原来也被6pt卡住），字略小但保证不溢出（用户已确认：
        /// 小屏宁可字小也要显示完整）。极小窗（zoom触0.15）时字保4pt可读，
        /// 再小走滚动条兜底，不再往下压成像素点。
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

        // ============ 缓存画刷/画笔（V1.57.2：绘制热路径避免高频 new SolidBrush/Pen 导致 GC 压力） ============
        // 面板数据驱动的颜色（状态块/背景）仍按需 new，但边框、值框底、行选按钮底、设置按钮底、
        // 选中框底色等"每帧每面板都用的常量色"全部缓存为字段复用，一次分配、整生命周期复用。
        // 【V1.60】其中跟随主题的 4 个（边框/值框/行选/未选中框）去掉 readonly，SetDarkMode 里重建。
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

        // ============ 拖拽滚动（V1.57~V1.88.23，已删） ============
        // 【V1.88.24】整套删除：_dragStartPoint/_dragStartScroll/_isDragging/_captured/
        // _dragScrollTimer/_dragTargetScroll/DragScrollThreshold + GridView_MouseDown 捕获/
        // MouseMove 换算/DragScrollTimer_Tick/UpdateCanvasSize 滚动同步/ClampScrollToContent。
        // 只留一屏铺满后无滚动可拖：按下拖动不再转滚动，抬起一律按点击处理。

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

            // 加载布局配置；颜色分两批：语义状态色直接解析（终身不变），
            // 主题色走 ApplyLightColors（SetDarkMode 切深色/切回浅色都调它，保证浅色精确还原配置值）
            _layout = PanelLayoutConfig.LoadOrDefault();

            _colorPowerOn = Parse(_layout.ColorPowerOn, Color.ForestGreen);
            _colorPowerOff = Parse(_layout.ColorPowerOff, Color.LightGray);
            _colorVacuumOn = Parse(_layout.ColorVacuumOn, Color.ForestGreen);
            _colorVacuumAlarm = Parse(_layout.ColorVacuumAlarm, Color.Red);
            _colorVacuumOff = Parse(_layout.ColorVacuumOff, Color.LightGray);
            _colorSetButton = Parse(_layout.ColorSetButton, Color.ForestGreen);
            ApplyLightColors();

            // 显式创建字体（不依赖 this.Font / 主窗体 AutoScale，保证文字尺寸与固定矩形一致）。
            // 【V1.88.25】正文字体直接加粗（与 RebuildFonts 同值，首帧不闪常规体，挂载后即重建覆盖）。
            _panelFont = new Font(_layout.FontFamily, _layout.FontSize, FontStyle.Bold);
            _titleFont = new Font(_layout.FontFamily, _layout.TitleFontSize,
                _layout.TitleFontBold ? FontStyle.Bold : FontStyle.Regular);
            _setButtonFont = BuildSetButtonFont(_layout, 1f);
            _rowSelectFont = BuildRowSelectFont(_layout, _layout.FontSize);
            // 【V1.88.28】竖排逐字绘制的字符缓存：Paint 里只按下标取，不量字不拼串不分配；
            // 文案来自配置（json 可覆盖），构造时拆好，全生命周期不变。
            _rowSelectCharsAll = ToCharStrings(_layout.RowSelectAllText);
            _rowSelectCharsCancel = ToCharStrings(_layout.RowSelectCancelText);
            RefreshRowSelectMetrics();

            // 【V1.57.2】初始化缓存画刷/画笔：语义色两个一次建好，主题色四个走 RebuildThemeBrushes
            // （SetDarkMode 里复用它重建，保证颜色与字段永远一致）。
            _brushSetButton = new SolidBrush(_colorSetButton);
            _brushSelectChecked = new SolidBrush(Color.ForestGreen); // 选中✓绿（【V1.88.29】随全仓绿统一加深，白✓对比度同步提升）
            RebuildThemeBrushes();

            _toolTip = new ToolTip(components);

            // 【V1.88.24】拖拽滚动合并定时器随滚动整套删除；MouseDown 空 handler 同步摘除
            // （点击只看 MouseUp：单击/触摸点选，拖动不再转滚动）。

            this.MouseUp += GridView_MouseUp;
            this.MouseMove += GridView_MouseMove;
            this.MouseLeave += GridView_MouseLeave;
        }

        /// <summary>
        /// 控件句柄创建后计算 DPI 缩放因子（【V1.55 高DPI适配】）。
        /// DeviceDpi 只有在句柄创建后才能取到真实值；必须在 Configure 之后、
        /// 首次绘制之前调用，否则画布尺寸仍是 96DPI 逻辑大小。
        /// </summary>
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateDpiScale();
        }

        /// <summary>
        /// 根据当前设备 DPI 更新缩放因子，并重新计算画布尺寸。
        /// DPI 缩放因子 = DeviceDpi / 96（96 是布局配置的逻辑像素基准）。
        /// 该方法在句柄创建时调用一次；后续若发生 DPI 变更（跨屏拖动）也会触发。
        ///
        /// 【踩坑】不能用 Control.DeviceDpi 属性——在 PerMonitorV2 环境下它有时返回 96
        /// （句柄刚创建时 DPI 上下文尚未生效），而 CreateGraphics().DpiX 才是真实值。
        /// 实测同屏：DeviceDpi=96、CreateGraphics().DpiX=144，所以这里以 CreateGraphics 为准。
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
                // 【V1.88.14】DPI 变了 = 内容物理尺寸变了，按新尺寸重算自适应 zoom。
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
        /// 按可用区与内容区算双向自适应缩放比（【V1.88.17 新增】纯函数，回归可直接断言；
        /// 【V1.88.24】单/双模式二合一后唯一自适应入口，原名 ComputeFitZoomBoth 改名至此，
        /// 单轴版 ComputeFitZoom（V1.88.22）与 FitMode 开关同步删除）。
        ///
        /// 【语义】精确铺满一屏：zoomX = 可用宽/内容宽，zoomY = 可用高/内容高，
        /// 两轴独立（面板允许宽扁拉伸，字号取窄边，见字段注释）；画布精确等于显示区，
        /// 纵向横向滚动条都不出（不同工控机屏即换即铺满）。
        /// 任一边非法（≤0）时两轴都回 1（原尺寸，不摆烂半边）。
        /// 本函数只做除法、不断言范围：铺满要求精确值，不做钳制
        /// （【V1.88.24】MinZoom/MaxZoom 已删，滚动条兜底同步移除）。
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

        // 【V1.88.24】FitMode 属性/_fitMode 字段/单轴 ComputeFitZoom(availW, contentW)
        // 随双模式开关整套删除（只留双向铺满一路，见 ComputeFitZoom 四参版）。

        /// <summary>
        /// 父容器换了（MainForm 装配时挂上 scrollContainer）：
        /// 摘旧容器的 Resize、挂新容器的，并按新容器尺寸重算 zoom。
        /// Configure 先于 Add 进容器调用（此时 Parent==null 算不出），
        /// 挂上容器这一跳才是自适应真正生效的时机。
        /// </summary>
        protected override void OnParentChanged(EventArgs e)
        {
            Control oldParent = null;
            // 取旧容器：base.OnParentChanged 之后 Parent 已是新的，
            // 这里用字段缓存上次挂过的容器做摘除（首挂时为 null，直接挂新的）。
            oldParent = _fitParent;
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
        /// 按父容器当前可用区重算 _zoomX/_zoomY 并应用（字体+画布+重绘）。
        /// 不满足任一条件直接返回（保持当前 zoom）：AutoFit 关/未 Configure/无父容器/
        /// 父容器尚未布局（宽或高为 0）/新值与当前差都 &lt;0.001（防抖，
        /// Splitter 拖动连续 Resize 不反复重建字体）。
        /// 【V1.88.24】只剩双向铺满一路（FitWidth 分支/钳制/滚动兜底全删）：
        /// zoomX=可用宽/内容宽、zoomY=可用高/内容高独立，画布精确等于显示区客户区，
        /// 纵向横向滚动条都不出；显示区再小也照算（72 站永远一屏，字保 MinFontSize）。
        /// 无预扣 1px：外层容器 AutoScroll=false，取整相等也不会挤出滚动条，直接精确顶满。
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
        /// 按当前 zoom 重建两套字体（旧字体先释放，防 GDI 句柄泄漏）。
        /// 字号 = 配置字号 × min(zoomX, zoomY)，下限见 <see cref="MinFontSize"/>（4pt）：
        /// 双向拉伸下面板允许宽扁，但字不变形、取窄边；【V1.88.21】小屏下字体跟随缩小
        /// 保证不溢出（旧6pt下限在1280×1024下把字卡大1.5倍致标签挤叠，见 MinFontSize 注释），
        /// 格子里的字走 EndEllipsis/居中截断不断行。
        /// 【V1.88.25】正文字体一律加粗：一屏铺满后字号只剩 4~5pt（1280×1024屏实测 4.81pt，
        /// 笔画 1px，常规体发虚；harness A/B：常规→加粗，黑像素 +34%、同屏对比明显更清楚，
        /// 且加粗只加笔画不断行（溢出仍走省略号，不盖框）。标题字体跟配置 TitleFontBold
        /// （缺省 true，本来就粗），不动。
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
            _panelFont = new Font(_layout.FontFamily, panelSize, FontStyle.Bold);
            _titleFont = new Font(_layout.FontFamily, titleSize,
                _layout.TitleFontBold ? FontStyle.Bold : FontStyle.Regular);
            _rowSelectFont = BuildRowSelectFont(_layout, panelSize);
            _setButtonFont = BuildSetButtonFont(_layout, z);
            if (oldPanel != null) oldPanel.Dispose();
            if (oldTitle != null) oldTitle.Dispose();
            if (oldRowSelect != null) oldRowSelect.Dispose();
            if (oldSetButton != null) oldSetButton.Dispose();
            RefreshRowSelectMetrics();
        }

        /// <summary>
        /// 按正文实际字号构建行全选字体（【V1.88.28】纯静态，可单测：字号 = panelSize × 配置倍率，
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
        /// 按 zoom 构建设置按钮字体（【V1.88.29】纯静态，可单测：字号 = 配置 SetButtonFontSize × zoom，
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

        /// <summary>文案拆逐字数组（null/空即空数组，调用方按空画空按钮）</summary>
        private static string[] ToCharStrings(string text)
        {
            if (string.IsNullOrEmpty(text)) return new string[0];
            string[] chars = new string[text.Length];
            for (int i = 0; i < text.Length; i++) chars[i] = text[i].ToString();
            return chars;
        }

        /// <summary>
        /// 竖排字间隙（【V1.88.28】纯函数：字高的 1/4 四舍五入、下限 2px——"正常间隙"，
        /// 字挨太紧难读、等分撑满又太散；回归可直接断言）。
        /// </summary>
        public static int RowSelectGapForCharH(int charH)
        {
            if (charH < 1) charH = 1;
            int gap = (int)Math.Round(charH * 0.25);
            return gap < 2 ? 2 : gap;
        }

        /// <summary>
        /// 竖排整块起始 Y（【V1.88.28】纯函数，回归可直接断言）。
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

        /// <summary>
        /// 按当前列/行/布局重算画布总尺寸（Configure/UpdateDpiScale/ShowCurrentRow/
        /// UpdateAutoFit 四处共用，改尺寸只改这里一处）。
        /// 【V1.88.24】画布恒等于显示区（zoom 即按可用区算出，取整 ±1px），
        /// 外层容器 AutoScroll=false，纵向横向滚动条都不出；
        /// V1.88.15 的滚动同步（MinSize/比例恢复/BeginInvoke 校正/ClampScrollToContent）
        /// 随滚动整套删除（无滚动可同步，设 Size 即完事）。
        /// </summary>
        private void UpdateCanvasSize()
        {
            this.Size = new Size(ScaledX(_columns * _layout.PanelColumnWidth + _layout.RowSelectButtonColumnWidth),
                                 ScaledY(_rows * _layout.GetEffectiveRowHeight()));
        }

        #endregion

        #region 深色模式（V1.60 新增）

        /// <summary>
        /// 载入浅色主题色（就是 PanelLayout.json 里配的那套；json 没配就用内置默认）。
        /// 构造函数调一次；SetDarkMode(false) 切回浅色时再调一次，保证浅色永远精确还原配置值。
        /// </summary>
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
        /// 载入深色主题色（固定深灰系，与 ThemeManager.DarkXxx 同系）：
        /// 面板底走深（空闲深灰/测试暗金/故障暗红/完成深蓝），值框/文字/边框/行选按钮同步走深，
        /// 语义状态块（上电绿/故障红/繁忙黄/选中橙/完成蓝、设置按钮绿）原样不动——
        /// 白字压在绿/红/蓝块上，深浅两边都清晰。
        /// 【V1.60.3】下电/真空关不走浅灰了：浅灰底(211)在深面板上太跳，参考主窗体停止/
        /// 复位按钮改 DimGray 底 + 白字（GetOffBlockThemeColors）；浅色仍用配置灰底黑字。
        /// 注意：状态块那圈 1px 边框是全面板共用的 _colorBorder，深色下统一变灰，这是正常的。
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
        /// 下电/真空关块的主题配色（【V1.60.3】纯函数，方便回归直接断言）。
        /// 浅色：配置原灰底（默认 LightGray）+ 黑字；深色：浅灰在深底上太跳，
        /// 参考主窗体停止/复位按钮走 DimGray 底 + 白字（跟各窗"取消"按钮同款）。
        /// 开/上电块（绿底白字）两边都不动，不走这里。
        /// </summary>
        /// <param name="dark">true=深色配色，false=浅色配色</param>
        /// <param name="lightBack">浅色底（配置值，PanelLayout.json 可覆盖）</param>
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
            // 【V1.51】首次运行时若程序目录没有 PanelLayout.json，自动导出一份默认配置，
            // 方便现场直接修改配置文件微调界面（坐标/颜色/字号/文字），无需重新编译。
            if (!System.IO.File.Exists(PanelLayoutConfig.GetConfigPath()))
            {
                _layout.SaveDefault();
            }

            _columns = columns;
            _rows = rows;
            _totalDevices = totalDevices;
            _items.Clear();
            for (int i = 0; i < totalDevices; i++)
            {
                _items[i + 1] = new GridItem { DeviceId = i + 1 };
            }
            // 【V1.60】新面板默认底按当前主题走（否则深色下首屏 1 秒内面板是白的，等首轮采集才变深）
            RefreshItemBackgrounds();
            // 【V1.60.3】下电/真空关块色同样按当前主题初始化（深色首屏直接 DimGray，不闪一下浅灰）
            RefreshOffBlockColors();
            // 【V1.55 高DPI适配】画布总尺寸 = 逻辑像素尺寸 × DPI缩放因子。
            // 若不放大，150% 缩放下格子保持 96DPI 大小、文字却自动变大 → 溢出重叠。
            // 【V1.88.14】尺寸计算收进 UpdateCanvasSize；此时多半还没挂进父容器
            // （MainForm 先 Configure 后 Add），自适应在 OnParentChanged 里补算一次。
            UpdateCanvasSize();
            UpdateAutoFit();
            Invalidate();
        }

        /// <summary>
        /// 是否显示电流行（【V1.77 新增】运行时开关，默认 false = 原来布局逐像素不变）。
        /// 主窗体按 DeviceConfig.UsePowerMeter 传入一次（结构型开关，改后重启生效，
        /// 与 UsePowerMeter 同口径，不跟项目热更）。
        /// 置 true → 布局 ShowCurrent 置位 + 锚定重解（面板有效高 +21，下游下移 21）
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
                    // 【V1.77】内容高变了（【V1.88.17】面板 170→188），自适应 zoom 跟着重算。
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
        /// 选中框边长（物理像素，【V1.88.17 新增】恒正方形 + 跟面板尺寸走）。
        ///
        /// 【为什么不能直接画布局矩形】双向自适应下 zoomX≠zoomY（如 1080p 下 0.844/0.553），
        /// 18×18 的布局框会被压成 15×10 的扁条（用户目检：长方形不好看）。
        /// 改为取"缩放后宽高较小边"为边长：面板大框大、面板小框小（自适应），且永远是正方形；
        /// 下限 4px（再小点不中画了，直接保底，此时早已触 zoom 下限走滚动兜底）。
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
        /// 【V1.57.2 回退】此前尝试"离屏画布缓存 + OnPaint 拷贝"，实测离屏大图上
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
        /// 【V1.57.2 回退】见 UpdateAll 注释：恢复为旧版"仅 Invalidate 面板区域"。
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
            // 【V1.74】电流文本：有数显示（如 0.42 A），无数据（NaN）记空串；
            // 【V1.77】改直绘：电流画在面板电流行（见 DrawPanel），无数据画 "--"；
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

            // 【V1.88.16】工作状态块已删：状态只看面板底色（空闲白/测试浅黄/故障浅粉/
            // 完成淡钢蓝）＋上电/真空块，不再有文字块。item.Status 照记（切主题重算底色用）。

            // 面板背景色（空闲白/测试浅黄/故障浅粉/完成淡钢蓝【V1.59】；深色下走深色档【V1.60】）
            // 【V1.60】状态同步记到 item.Status：切主题时 RefreshItemBackgrounds 靠它重算底色；
            // 底色取值走 GetStatusBackColor，两处共用，改配色只改一处。
            item.Status = data.Status;
            item.BackColor = GetStatusBackColor(data.Status);
        }

        #endregion

        #region 自绘渲染

        /// <summary>
        /// 自绘整个工位网格（OnPaint 入口）——【V1.57.2 回退】
        /// 【为什么回退】V1.57.2 曾改为"离屏画布缓存：整幅 RenderToCanvas + OnPaint DrawImage 拷贝"，
        /// 实测离屏大图（2040×2025）上 TextRenderer 绘制极慢（每处约 2.2ms，全量 72 面板 2247ms），
        /// 而 UpdateAll 每秒触发全量渲染、选中翻转也触发，UI 线程被拖死 →"整个软件都卡"。
        /// 旧版直接绘制到屏幕 DC（TextRenderer 实测近 0ms），只重绘可见区域，反而流畅。
        /// 【本版策略】回到旧版"OnPaint 只重绘可见列/行范围的面板"，仅保留 V1.57.2 中仍然有效的
        /// 画刷/画笔缓存字段（_penBorder/_brushValueBox 等，减少每帧 new GDI 对象）。
        /// 【V1.88.24】16ms 拖拽滚动合并定时器随滚动整套删除（一屏铺满后无滚动可合并）。
        /// 全部使用绝对坐标绘制：每个面板元素的最终坐标 = 面板左上角 + 设计坐标，
        /// 不再使用 TranslateTransform（避免 TextRenderer 的 GDI 绘制与坐标变换错乱导致文字模糊）。
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            if (_columns == 0) return;

            // 【V1.55 高DPI适配】e.ClipRectangle 是物理像素坐标，而布局配置是 96DPI 逻辑像素，
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
                    // 【V1.88.17】横向走 zoomX、纵向走 zoomY）
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
                    // 【V1.58.8】按钮高度取"面板内容高-1"：使全选按钮含黑色边框的上下边缘
                    // 与每行工作站显示框完全对齐（【V1.88.17】面板 204×170：高=169，
                    // 边框底=2+169=171，与面板内容底(2+170=172)差 1px 即 -1 修正）。
                    // 为什么 -1：DrawRectangle 边框线画在矩形下边界(y=2+高度)，不减 1 边框底
                    // 会比面板内容底多 1px，肉眼可见底部凸出。
                    // Y 与面板内容同为 row*行高+2，顶部天然对齐；宽度为列宽-左右边距(=60)。
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
            // 面板背景（状态色），尺寸按 DPI 放大（【V1.88.17】宽走 zoomX、高走 zoomY）
            using (var bg = new SolidBrush(item.BackColor))
            {
                g.FillRectangle(bg, panelLeft, panelTop, ScaledX(_layout.PanelInnerWidth), ScaledY(_layout.GetEffectiveInnerHeight()));
            }

            // 设备编号（左上角）
            TextRenderer.DrawText(g, $"NO.{item.DeviceId}", _titleFont,
                new Point(panelLeft + ScaledX(_layout.TitlePosition.X), panelTop + ScaledY(_layout.TitlePosition.Y)), _colorText);

            // 状态块（【V1.88.16】工作状态块已删：第一行只剩上电/下电＋真空开/关；
            // 状态看面板底色＋这两块，不再有文字状态块）
            DrawStatusBlock(g, Offset(Scaled(_layout.RcPower.ToRectangle()), panelLeft, panelTop),
                item.PowerColor, item.PowerForeColor, item.PowerText);
            DrawStatusBlock(g, Offset(Scaled(_layout.RcVacuumOpen.ToRectangle()), panelLeft, panelTop),
                item.VacuumColor, item.VacuumForeColor, item.VacuumText);

            // 值框
            DrawValueBox(g, Offset(Scaled(_layout.RcPressureValue.ToRectangle()), panelLeft, panelTop), item.PressureText);
            // 【V1.77】电流值行（ShowCurrentRow 开才画）：标签 + 值框走锚定矩形
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
            DrawValueBox(g, Offset(Scaled(_layout.RcDelayTimeValue.ToRectangle()), panelLeft, panelTop), item.DelayTimeText);
            DrawValueBox(g, Offset(Scaled(_layout.RcBurnInValue.ToRectangle()), panelLeft, panelTop), item.BurnInTimeText);

            // 静态标签（【V1.88.17】X 走 zoomX、Y 走 zoomY）
            DrawLabel(g, new Point(panelLeft + ScaledX(_layout.LabelPressurePosition.X), panelTop + ScaledY(_layout.LabelPressurePosition.Y)), "真空压力");
            // 【V1.77】"电流："标签（与值框同条件：开才画；关时坐标无意义，不画即可）。
            if (ShowCurrentRow && _layout.LabelCurrentPosition != null)
            {
                DrawLabel(g, new Point(panelLeft + ScaledX(_layout.LabelCurrentPosition.X), panelTop + ScaledY(_layout.LabelCurrentPosition.Y)), "电流：");
            }
            DrawLabel(g, new Point(panelLeft + ScaledX(_layout.LabelSnPosition.X), panelTop + ScaledY(_layout.LabelSnPosition.Y)), "SN:");
            DrawLabel(g, new Point(panelLeft + ScaledX(_layout.LabelRecipePosition.X), panelTop + ScaledY(_layout.LabelRecipePosition.Y)), "配方:");
            DrawLabel(g, new Point(panelLeft + ScaledX(_layout.LabelDelayTimePosition.X), panelTop + ScaledY(_layout.LabelDelayTimePosition.Y)), "延时时间");
            DrawLabel(g, new Point(panelLeft + ScaledX(_layout.LabelBurnInPosition.X), panelTop + ScaledY(_layout.LabelBurnInPosition.Y)), "烧屏时间");

            // 设置按钮（绿底白字；【V1.88.29】独立大字 _setButtonFont＋深绿底，
            // 白字对比度 2:1→4.6:1，小屏看得清）
            Rectangle rcSet = Offset(Scaled(_layout.RcSetButton.ToRectangle()), panelLeft, panelTop);
            g.FillRectangle(_brushSetButton, rcSet);
            g.DrawRectangle(_penBorder, rcSet);
            TextRenderer.DrawText(g, _layout.SetButtonText, _setButtonFont, rcSet, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            // 选中指示（常显：选中=绿底白✓，未选中=空心白框；无选中时框也在，操作员一眼知道点哪里选中）
            // 【V1.88.17】框恒正方形：边长取布局矩形缩放后的较小边（双向拉伸下宽≠高，
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
        /// 【V1.88.28】文字改竖排大字：n 个字按"字高＋正常间隙"排成紧凑一竖块、
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
        /// 【V1.52】文字不再贴值框左边框：绘制矩形左移 ValueTextLeftPadding 像素，
        /// 值框本身坐标不变（避免"移动整个框"造成的错位观感）。
        /// </summary>
        private void DrawValueBox(Graphics g, Rectangle rc, string text)
        {
            g.FillRectangle(_brushValueBox, rc);
            g.DrawRectangle(_penBorder, rc);
            // 文本绘制矩形 = 值框矩形左移内边距（宽度同步缩短，防止文字溢出到右边框）
            // 【V1.55】内边距按 DPI 放大，保证 150% 缩放下文字仍与值框左边框保持合理间距
            // 【V1.88.17】内边距是横向量，走 zoomX
            int pad = ScaledX(_layout.ValueTextLeftPadding);
            Rectangle textRc = new Rectangle(
                rc.X + pad,
                rc.Y,
                Math.Max(1, rc.Width - pad),
                rc.Height);
            TextRenderer.DrawText(g, text, _panelFont, textRc, _colorText,
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

        // 【V1.88.24】GridView_MouseDown 已删：拖拽滚动移除后按下无事可做，点击只看 MouseUp。

        /// <summary>
        /// 鼠标抬起（左键）：
        /// - 行全选按钮 → 整行选中/取消；
        /// - 面板内"设置"区域 → 触发 OnSetClicked；
        /// - 面板内"选中框"或"空白区域" → 直接切换该工位选中（单击/触摸点选）。
        /// 【V1.88.14】选中框常显后不再设门槛：无选中时点框/点空白同样选中，
        /// 长按（选中首个/取消全选）整套删除；取消选中逐台点框/整行"取消"。
        /// 【V1.88.24】拖拽滚动已删：按下拖动不再转滚动，抬起一律按点击处理
        /// （内容恒一屏、无处可滑，"滑动浏览不误选"的前提已不存在）。
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
                // 【V1.55】local 是物理像素坐标，布局矩形需缩放后比较
                // 【V1.88.17】选中框命中与绘制同源（GetSelectBoxLocalRect），框变正方形后点选不漂移
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

        /// <summary>
        /// 鼠标移动：状态块悬停提示（【V1.88.24】拖拽滚动换算已删，只剩提示）。
        /// </summary>
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

        // 【V1.88.24】DragScrollTimer_Tick 随拖拽滚动整套删除。

        /// <summary>
        /// 切换指定工位的选中状态并重绘（选中框常显，只需刷新当前面板）。
        /// 【选中框常显】框的显示不取决于任何全局状态：所有面板右上角永远画框
        /// （选中=绿底白✓，未选中=空心框）；【V1.88.14】单击/触摸点框或点空白
        /// 直接翻转，无门槛（旧"无选中时单击不翻选、长按才选中"门控已删，
        /// IsAnySelected/ClearAllSelection/长按计时器同步删除）。
        /// 单台翻转只影响自己，局部重绘即可；整行切换一次动多台，才全量 Invalidate()。
        /// 【为什么常显+点选】以前"无选中时全场无框"，操作员找不到点哪里选中；
        /// 常显后框永远可见，所见即所得，现场触摸屏点一下即打勾。
        /// 取消选中：逐台点框/空白翻回，或整行"取消"按钮。
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
        /// 【V1.55】p 是物理像素坐标，需与缩放后的列宽/行高比对
        /// 【V1.88.15】面板间隙不命中：行列整除会把面板之间的缝隙算进上一格，
        /// 点缝隙翻选上一个面板是 bug；local 落在面板内容矩形外一律返回 false
        /// （悬停提示同步消失，见 GetTooltipText）。
        /// </summary>
        private bool TryHitPanel(Point p, out int deviceId, out Point local)
        {
            deviceId = 0;
            local = Point.Empty;
            if (_columns == 0) return false;

            // 【V1.88.17】列宽走 zoomX、行高走 zoomY（与绘制同口径，否则点选错位）
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
            // 【V1.55】local 是物理像素坐标，布局矩形需缩放后比较
            if (Scaled(_layout.RcPower.ToRectangle()).Contains(local)) return "上电状态：绿=上电，浅灰=下电";
            if (Scaled(_layout.RcVacuumOpen.ToRectangle()).Contains(local)) return "真空状态：阀开且负压到位=绿底 / 阀开但没吸住=红底 / 阀没开=灰底";
            // 【V1.77】压力框悬停回退到原来（无提示）：电流已改直绘（电流行），
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
            /// 工位当前业务状态（【V1.60 新增】：切主题重算底色用；平时由 UpdateSingleItem 随采集刷新）。
            /// 为什么要记它：GridItem 原来只记颜色不记状态，切主题时想重算底色就找不到依据，
            /// 只好把状态也记下来（就是 DeviceStatus 空闲/测试/故障/完成那几个值）。
            /// </summary>
            public DeviceStatus Status;
            /// <summary>
            /// 载台是否上电 / 真空阀是否打开（【V1.60.3 新增】：切主题重算下电/真空关块色用；
            /// 平时由 ApplyData 随采集刷新。只记开关不记颜色，颜色永远由当前主题现算；
            /// 真空开块的绿/红（到位/没吸住）两边主题都不动，同样不用记，只重算灰色的关块）。
            /// </summary>
            public bool CarrierPower;
            public bool VacuumOpen;
            public string PressureText = "---";
            /// <summary>
            /// 载台电流文本（【V1.74 新增】Q2 骨架：ApplyData 随采集刷新，有数如"0.42 A"，
            /// 无数据记空串；【V1.77】用于电流行直绘（ShowCurrentRow 开时画，无数据画 "--"）。
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
