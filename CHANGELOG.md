# CHANGELOG

> 精简版改动历史（最新在前）。只保留有维护价值的功能/修复要点；细微 UI 调整不重复记录。
> 详细上下文可查 git 历史。协议/寄存器类改动同时已同步到 [`docs/通讯接入.md`](docs/通讯接入.md)。

## V1.57.3 — 回退画布缓存：修复 V1.57.2 引发的整软件卡死与面板"连成一片"（2026-08-10）

### 改动范围
- `Views/WorkstationGridView.cs`：**废弃 V1.57.2 的"离屏画布缓存"方案**（`_canvas`、`EnsureCanvas`、`RenderToCanvas`、`RenderPanelToCanvas` 全部删除），恢复旧版"OnPaint 只重绘可见区"；`UpdateAll`/`UpdateSingle`/`InvalidateAfterSelectionChange`/`ClearAllSelection`/`ToggleRow` 回到仅 Invalidate 的旧实现。
- **保留 V1.57.2 中仍然有效的两项优化**：① 16ms 拖拽滚动合并定时器 `_dragScrollTimer`（MouseMove 只记目标、定时器统一应用 AutoScrollPosition）；② 画刷/画笔缓存字段（`_penBorder`、`_brushValueBox`、`_brushRowSelect`、`_brushSetButton`、`_brushSelectChecked`、`_brushSelectUnchecked`）。

### 为什么这么改（V1.57.2 的教训）
- V1.57.2 把网格整体预渲染到离屏 Bitmap 想加速滚动，但实测**离屏大图（2040×2025）上 `TextRenderer.DrawText` 每处约 2.2ms**（屏幕 DC 上近 0ms），全量 72 面板渲染一次高达 **2247ms**。
- 而 `UpdateAll`（1Hz 采集刷新）每次都触发全量渲染 → **整个软件每 1 秒卡死一次**；选中/行全选等操作也走全量渲染 → 点击后"卡住不动"。这就是用户反馈的卡死根因。
- 另因 `RenderToCanvas` 里 `g.Clear(_normalColor)` 把整幅画布刷成白色，面板之间 2px 间隙（原本显示浅灰 `Control` 底色、形成"一个一个"的分隔感）被填白 → 面板看起来"连成一片"。回退后间隙恢复浅灰底色。
- 结论：离屏 TextRenderer 慢是 .NET GDI+ 固有行为，画布缓存方案在此场景不可行；屏幕 DC 直接绘制可见区本就流畅，滚动卡顿应靠"节流+少重绘"解决而非"预渲染"。

### 验证
- 性能（真实屏幕 DC + 真实滚动容器）：`UpdateAll` 全量刷新 2247ms→**23ms/次**；选中翻转 23ms；非翻转选中 4ms；真实滚动帧 22ms（45FPS，恢复至 V1.57.1 基线）。
- 正确性（像素验证）：面板间隙 = 浅灰 `Control`（"连成一片"修复）；选中 NO.1=绿✓、NO.2=空心白框、取消后消失 全部通过。
- 端到端回归（SendInput 真实按键+移动）：右拖滚动量减小、反向拖增大、长按 800ms 选中 全部通过。
- 冒烟测试进程存活。

## V1.57.2 — 拖拽滚动性能优化：画布缓存 + 滚动节流 + 画刷复用（2026-08-10）【已回退，见 V1.57.3】

> ⚠️ 本节方案（离屏画布缓存）因离屏 TextRenderer 性能灾难已由 V1.57.3 整体回退，保留记录供日后避免重蹈覆辙。滚动节流与画刷复用两项有效优化已并入 V1.57.3。

### 改动范围
- `Views/WorkstationGridView.cs`，三个优化叠加，拖动帧耗时由 **24.55ms→5.02ms（约 5 倍）**：
  1. **画布缓存（核心）**：新增 `_canvas` 离屏 Bitmap，整幅网格预先渲染进缓存，`OnPaint` 只做 `DrawImage` 可见区位图拷贝（GDI 硬件加速，实测 1000ms→1.86ms）。数据更新 `UpdateAll`/`UpdateSingle`、选中变化 `InvalidateAfterSelectionChange`/`ClearAllSelection`/`ToggleRow` 改为先把面板画进缓存再 Invalidate 对应区域。滚动时不再逐面板重绘十几处 `TextRenderer`。
  2. **滚动节流**：鼠标回报率（常见 125~1000Hz）远高于屏幕刷新率（60Hz），此前每次 MouseMove 都直接 `AutoScrollPosition` setter（含布局+滚动条更新，约 2.9ms）。改为 MouseMove 只记录 `_dragTargetScroll`，由新增 16ms 的 `_dragScrollTimer` 统一应用（60FPS 合并）；MouseUp 时立即应用最终目标，保证松手停位准确。
  3. **画刷/画笔复用**：边框 `_penBorder`、值框底 `_brushValueBox`、行选按钮 `_brushRowSelect`、设置按钮 `_brushSetButton`、选中框绿/白底 `_brushSelectChecked`/`_brushSelectUnchecked` 缓存为字段，替换原每面板 `new SolidBrush/Pen` 的数百次/帧分配，减轻 GC 压力。

### 为什么这么改
- 卡顿根因经测量确认：滚动容器在 `AutoScrollPosition` 变化时会让子控件重绘**整个可见区**（harness 实测 clip=100% 可见区，而非"新暴露条带"），每帧重画 12 个可见面板的十几处文本 → 单帧 24.55ms，远超 60FPS 的 16.7ms 预算。三层优化分别解决"画得太贵""画得太频繁""分配太多"。

### 验证
- 性能 harness（离屏渲染 + 真实滚动 100 帧）：滚动帧 24.55ms→5.02ms；OnPaint 1000ms→1.86ms；setter 2.88ms→0.62ms；完整拖拽帧 37.9ms→5.4ms。
- 正确性 harness：无选中/选中1台/行全选/取消全部 四种状态在画布缓存下渲染像素全部正确（绿✓、空心白框、消失均验证）。
- 端到端回归（SendInput 真实按键+移动）：右拖滚动量减小、反向拖增大、长按 800ms 选中，全部通过。
- 冒烟测试进程存活。

## V1.57 — 主界面工位列表支持鼠标拖拽滚动（2026-08-10）

### 改动范围
- `Views/MainForm.Designer.cs`：`groupBoxLog` 标题由 "LOG" 改为 "日志"
- `Views/WorkstationGridView.cs`：新增**按住鼠标左键拖动滚动**功能——
  - 按下左键时记录拖拽起点与外层滚动容器（`Panel.AutoScroll`）的当前滚动位置，并捕获鼠标（`Capture=true`），保证指针移出网格后拖动仍不中断；
  - 移动超过 `DragScrollThreshold`(10px) 判定为拖拽，进入拖拽后按位移量持续更新外层容器的 `AutoScrollPosition`，实现列表左右/上下跟随鼠标滑动；
  - 拖拽期间停止长按计时并隐藏悬停提示，抬起时若为拖拽结束则不计为点击（不影响原有面板点击/长按选中交互）；
  - 未超过阈值（单击/双击）行为与之前完全一致。
  - **长按选中兼容（V1.57 强化）**：拖拽启动阈值设为 10px，特意**大于**长按取消阈值(8px)——长按时手指轻微抖动（≤8px）不会进入拖拽，800ms 计时照常触发选中；只有明显拖动（&gt;10px）才进入滚动。鼠标捕获（Capture）只保证拖动不中断，不影响长按计时。实测长按选中仍正常。

### 为什么这么改
- 工位网格自绘画布可达 3060×3038 物理像素（V1.55 高 DPI 缩放后），超出主界面可视区域，滚轮滚动在大量工位下操作较繁琐；用户在操作工位列表时有"按住拖动整体滑动"的直觉需求。
- 拖拽方向换算的关键是 `AutoScrollPosition` 的 WinForms 语义：**getter 返回负值**（内容偏移取反）、**setter 接收正值**（滚动量）。因此内容跟随鼠标移动时，新滚动量 = 起点滚动量 − 鼠标位移，即 `AutoScrollPosition = (−起点X − dx, −起点Y − dy)`。
- 阈值设计上让"长按优先、拖动滞后"：长按取消是 8px，拖拽启动是 10px，两个判定互不抢占，是两者兼容的核心。

### 验证
- harness（`DragReal2`，SendInput 真实按键 + 真实鼠标移动，走完整 Windows 消息链）：初始滚到 (800,600) → 右/下拖 200,150 → `AutoScrollPosition` 由 `{-800,-600}` 变为 `{-600,-450}`（滚动量随鼠标位移减小，内容跟随，方向正确）；反向拖动 getter 变小（滚动量增大）；单击位置不变；**长按 800ms 仍触发选中**。四项全过。
- 冒烟测试进程存活；单击/长按选中等原有交互不受影响。

### V1.57.1 — 修复：选中指示框渲染残留（右上角框不消失 / 其他工位未选中框不显示）
- **Bug 现象**：① 长按选中一台工位后，只有它显示绿✓，其他工位的"空心未选中框"不显示；② 多选后再取消全部选中，右上角框停在"空心未选中"状态不消失（期望：一个都没选时框完全消失）。全选按钮无此问题。
- **根因**：`SetSelected`/`ToggleSelect` 只做**局部重绘**（`Invalidate(GetPanelBounds)`），但选中框画不画取决于**全局** `IsAnySelected`——任一选中时所有面板都要画框、一个没选时全部不画。局部重绘导致：第一次选中时其他面板没重绘（残留"无框"旧画面）；取消到最后一个时其他面板没重绘（残留"空心框"旧画面）。全选按钮走 `ToggleRow` 全量 `Invalidate()`，所以一直正常。
- **修复**：新增 `InvalidateAfterSelectionChange(deviceId, anyBefore)`：修改选中后比较全局状态，**有/无选中翻转时全量 `Invalidate()`**（所有面板框一起显示/隐藏）；仍处于有选中状态（只在已选集合内增删）时只需局部重绘当前面板。
- **验证**：离屏渲染整幅画布逐像素扫描选中框区域——无选中时框区=面板底色(255,255,255) ✓；选中 1 台时该面板=绿底+黑✓、其他面板=白底+黑框空心 ✓；取消全部后全部恢复底色、框消失 ✓。冒烟测试进程存活。

## V1.56 — 高 DPI 适配推广到全部页面：SunnyUI 全局 DPIScale 开关（2026-08-10）

### 改动范围
- `Program.cs`：程序启动时（`Application.Run` 之前）设置 `Sunny.UI.UIStyles.DPIScale = true`，等价于 SunnyUI 官方"主窗体放 UIStyleManager 控件并勾选 DPIScale"的推荐做法

### 为什么这么改
- V1.55 已适配主界面（自绘网格手动 DPI 缩放 + 标准控件走 WinForms AutoScaleMode）。排查其余页面后发现还有 3 个**继承 SunnyUI.UIForm 的窗体**（FanTestForm 送风机测试、CommunicationTestForm 通讯测试、其内部 RemapNoticeForm）在 150% 缩放下"文字溢出/偏大"：
  - 它们 Designer 里是 `AutoScaleMode.None`（SunnyUI 设计如此，`UIBaseForm.OnShown` 还会强制把 Font 模式改回 None，所以 V1.55 曾尝试改 `AutoScaleMode.Font` 完全无效，实测 `CurrentAutoScale={0,0}`）
  - SunnyUI 官方的 DPI 方案是：`AutoScaleMode.None + app.manifest dpiAware + UIStyles.DPIScale=true` 三件套。DPIScale 开启后，`UIForm.OnShown` 会遍历窗体所有 SunnyUI 控件调用 `SetDPIScale()`，把字体大小除以缩放系数（DPI/96=1.5），使控件字体在高分屏下保持设计时的物理大小
- 不用逐个改窗体：`UIStyles.DPIScale` 是全局静态开关，只在 **UIForm 子类**的 `OnShown` 路径生效；普通 Form（MainForm/SettingsForm/LoginForm/RecipeManagerForm 等）不走该路径，仍然只靠 WinForms 的 `AutoScaleMode.Font` 缩放，二者互不干扰（实测 SettingsForm 开/关 DPIScale 字体与尺寸完全一致）

### 优化点
- 一行代码覆盖全部 UIForm，零侵入各窗体
- 验证（144 DPI harness 实测）：FanTestForm 控件字体 10.5pt→7.0pt、CommunicationTestForm 同步缩小，物理渲染尺寸恢复设计值；SettingsForm/LoginForm/RecipeManagerForm 的 `ClientSize` 缩放与字体不受影响
- UIForm 窗体尺寸本身不放大（AutoScaleMode.None 行为），CommunicationTestForm 高度 1100 不会超出 1600 物理屏高，无超屏风险

## V1.55 — 高 DPI（150% 缩放）屏幕主界面适配：自绘工位网格按 DPI 放大（2026-08-10）

### 改动范围
- `Views/WorkstationGridView.cs`：**给自绘工位网格增加 DPI 缩放支持**，适配高分辨率屏幕（如 2560×1600 @150% 缩放，实际 DPI 144）：
  - 新增字段 `_dpiScale`（缩放因子 = 实际 DPI / 96，150% 缩放下 = 1.5）
  - 新增 `OnHandleCreated` + `UpdateDpiScale`：句柄创建后计算缩放因子，并按缩放后的尺寸重新设置画布 `Size`
  - 新增 `Scaled(int/Point/Rectangle)` 辅助方法：所有绘制/命中坐标统一从"96DPI 逻辑像素"换算成"物理像素"
  - `OnPaint`、`DrawPanel`、`DrawValueBox`（含值框文字左内边距）、命中检测（`TryHitPanel`/`TryHitRowButton`）、悬停提示（`GetTooltipText`）、局部重绘（`GetPanelBounds`）、行全选按钮列全部改为经 `Scaled()` 放大
- `App.config`：`<runtime>` 增加 `AppContextSwitchOverrides value="Switch.System.Windows.Forms.DpiAwareness=PerMonitorV2"`（与已有 `app.manifest` 的 PerMonitorV2 声明配套，让 WinForms 在 DPI 变化时自动缩放标准控件布局）

### 为什么这么改
- 用户新电脑分辨率高（2560×1600 @150%），主界面"很多 UI 显示不正常"。根因：自绘网格是 `AutoScaleMode.None`，画布尺寸与坐标是 96DPI 逻辑像素**不随 DPI 放大**，而 pt 字体会自动放大 1.5 倍 → 文字溢出格子/重叠，且与周围被 AutoScaleMode.Font 放大的标准控件比例失调
- 不能用 `Graphics.ScaleTransform`（TextRenderer 走 GDI 不认坐标系变换，V1.51 已踩坑），只能手动把每个坐标乘缩放因子；字体保持 pt 单位自动放大，两者同步放大后比例与 96DPI 完全一致
- 踩坑：`Control.DeviceDpi` 在 PerMonitorV2 下返回 96（句柄刚创建时 DPI 上下文未生效），实测 `CreateGraphics().DpiX` 才是真实值 144，故以 CreateGraphics 为准

### 优化点
- 96DPI（100% 缩放）老电脑上 `_dpiScale = 1.0`，坐标与历史完全一致，零回归
- 像素级验证：画布 2040×2025 → 3060×3038（×1.5）；离屏渲染后扫描 7 个关键点（标题文字/状态块浅灰/边框黑线/行全选列位置颜色）全部命中
- 冒烟测试进程存活；本版本先只适配主界面，其余页面待用户确认效果后按同思路推广

## V1.54j — 设置窗口数据行最右边缘恢复竖线（分组标题行仍无）（2026-08-09）

### 改动范围
- `Dialogs/SettingsForm.cs`：
  - 新增私有字段 `_gridScrollBar`（保存表格自带垂直滚动条 UIScrollBar 引用）
  - `CreateGrid`：关掉 `ShowLeftLine` 后把滚动条引用存进 `_gridScrollBar`（为 RowPostPaint 定位竖线 X 坐标用）
  - `Grid_RowPostPaint`：非分组行（普通配置行）改为调用新增的 `DrawDataRowRightBorder`，在滚动条左边缘左侧 1px（逻辑 X=1376）补画一条 `_grid.GridColor`（104,173,255）竖线；分组标题行仍走原"色带填充 + 蓝色下边线"逻辑，不画竖线

### 为什么这么改
- V1.54h 关闭滚动条 `ShowLeftLine` 后，数据行最右侧也失去了表格右边界竖线，观感上"表格右边没有封口"
- 用户要求：**数据行最右边缘要有竖线（表格右边界），但分组标题行（区域标题行）不要**——不能直接恢复 ShowLeftLine（它会把分组标题行也画上线，回到 V1.54g 老 bug），所以改为在 RowPostPaint 里只对非分组行补画

### 优化点
- 竖线颜色与数据行 cell border（GridColor=104,173,255）完全一致，视觉统一
- X 坐标动态取 `_gridScrollBar.Bounds.Left - 1`，不写死像素，表格尺寸变化也能自动跟随
- 像素级验证：所有数据行 X=1376 为 `104,173,255`，所有分组标题行仍为标题浅蓝 `237,243,253`（无竖线）

## V1.54i — 设置窗口去掉"搜索配置项："标题，搜索框与分组标题文字严格左对齐（2026-08-09）

### 改动范围
- `Dialogs/SettingsForm.cs` — **删除搜索框左侧的"搜索配置项："文字标题**（多余，输入框自带占位符"输入关键字过滤配置项"已足够示意），并让搜索框左边缘与下面"基础配置"等分组标题文字左边缘**逐像素对齐**：
  - 删除 `SetupSearchBox` 里的 `lblSearch` 控件；`_txtSearch` 左移并保持右边缘 450 不变（X=8, 宽 442），清除按钮 X=454 不动、与文本框间距 4px 不变
  - **对齐推导**（实测 pnlScroll.Padding.Left=18，注释里旧的 12 是错的）：分组标题文字左边缘在 pnlScroll 客户区 = Padding.Left + 8（Grid_CellPainting 给 colKey 文字留 8px 左内边距）；pnlSearch 为 Dock=Top，其左边缘 = Padding.Left；故 `_txtSearch.X = Padding.Left + 8 - Padding.Left = 8`（恒等，与 Padding 无关）
  - 若沿用旧 X=20 会右偏 12px（输入框左边缘在客户区 38 vs 文字 26）

### 为什么这么改
- 用户反馈"搜索配置项"标题多余，直接留输入框即可；且要求输入框左边缘与"基础配置"文本左边缘对齐

### 优化点
- 界面更简洁，少一个标签；搜索框与下方分类标题形成统一左边界线（像素级对齐已验证 diff=0）

## V1.54h — 设置窗口"分组标题行最右侧竖线"根治：关闭滚动条左侧线（2026-08-09）

### 改动范围
- `Dialogs/SettingsForm.cs` — **找到 V1.54f/g 一直没生效的根因并真正修掉"标题行最右侧竖线"**：
  - 之前的认知是"cell border 画在 `_grid.Right` 那一像素，RowPostPaint 覆盖宽度差 1px"，于是反复调覆盖右边界（V1.54f 用 `pnlScroll.ClientSize.Width`、V1.54g 用 `_grid.Width + 1`），但用户仍能看到竖线
  - **真凶**：SunnyUI `UIDataGridView` 内置一个 `UIScrollBar` **子控件**，覆盖在表格右边缘（最后一列右边界 ~ 表格右边界，约 X=918~936 逻辑像素）。它默认 `ShowLeftLine = True`，在 X=918 画一条 `80,160,255` 的蓝色竖线，横跨整张表格**每一行**——分组标题行的浅蓝色带最右侧也被切出一条竖线
  - 为什么之前盖不住：`RowPostPaint` 的 Graphics 被裁剪在行显示矩形内（X < 919），`_grid.Width+1` 的填充根本画不到那一像素；且 `UIScrollBar` 是子控件，永远绘制在表格内容**之上**，RowPostPaint/CellPainting 都压不过它
  - **修复**：`CreateGrid` 里找到该 `UIScrollBar` 子控件并 `ShowLeftLine = false`（该子控件随表格创建即存在，无需等 HandleCreated）。竖线彻底消失，标题色带一路延伸到滚动条处
  - 顺手把 `Grid_RowPostPaint` 里 V1.54g 那段"扩 1 像素"的误导性注释改写为真实机制说明

### 为什么这么改
- 用户反馈（V1.54f 时代起一直存在）："基础配置 这行最右侧还有表格的竖线，下面的很多区域标题的右侧都有这个问题"。之前几版都以为是 cell border 覆盖不彻底，实际是滚动条子控件的左侧线，方向就找错了
- 关闭 `ShowLeftLine` 是最小改动：不动列宽/覆盖逻辑，只去掉那条多余的装饰竖线

### 优化点
- 分组标题行最右侧不再有竖线，色带视觉上横跨整表、真正"不在表格内"
- 数据行右侧也更干净（原滚动条左缘的蓝色竖线一并消失，只剩浅蓝轨道），观感统一
- 通过像素级验证：修复前 X=1377（物理像素）为 `80,160,255` 竖线，修复后变为滚动条轨道 `243,249,255`

## V1.54c — 设置窗口分组标题行去掉表格分割线（2026-08-09）

### 改动范围
- `Dialogs/SettingsForm.cs` — **分组标题行去掉列间垂直线、保留上下水平线**：
  - **首发版（未生效）**：仅在 `CellPainting` 自绘分组标题行 + `e.Handled = true`，但 SunnyUI `UIDataGridView` 内部在 `CellPainting` 返回后还会自己补画 cell border / gridline，导致列间垂直线仍可见
  - **二次修复（去掉全部线）**：再补 `RowPostPaint` 用浅蓝覆盖整行矩形 + 上下 1px，但用户反馈"上下那条横线还是要的，不然整个表格看起来好丑"
  - **本版（最终）**：在 `RowPostPaint` 阶段用浅蓝覆盖**整行矩形但上下边各内缩 1px**——只抹掉列间垂直线、保留 DataGridView 默认的上下水平线
  - **最终调整（横线改蓝色）**：用户反馈默认的黑色/灰色水平线"很丑"，改为在 `RowPostPaint` 里**自己用蓝色 (`214,229,255`，与表头边框同系) 重画**上下两条水平线，不再依赖 DataGridView 默认 cell border

## V1.54g — 标题行右侧竖线 / 上边叠色深度修复（2026-08-09）

### 改动范围
- `Dialogs/SettingsForm.cs` — **V1.54f 修复未生效 + 上边横线叠色修复**：
  - **V1.54f 没生效的根因**：之前用 `pnlScroll.ClientSize.Width` 作覆盖右边界，但 `RowPostPaint` 的 Graphics 坐标系实际是 `_grid` 控件的，`pnlScroll.ClientSize.Width` 值超出 `_grid` 实际可绘范围；且 cell border 画在 X=`_grid.Width` 那一像素（半开区间），之前覆盖矩形 Width=`_grid.Width` 漏掉那一像素
  - **修复**：覆盖右边界改为 `_grid.Width + 1` 像素（Graphics 坐标系下），正好覆盖到 `_grid.Right` 位置那条 cell border
  - **上边叠色修复**：之前同时画上下两条蓝色线，**上边线**与 DataGridView 在标题行顶画的 cell border 叠在同一像素 → 颜色叠加"深很多"。改为**只画下边线**（用户明确要求"标题下面的横线要蓝色"），上边线直接用 DataGridView 默认 cell border（与数据行颜色一致）

### 为什么这么改
- 用户反馈 V1.54f "没生效" + "基础配置这些区域标题上面的表格横线好像多画了一次是不是？颜色明显深很多"

### 优化点
- 标题行最右侧的竖线真正消失（覆盖到 _grid.Right 那一像素）
- 上边横线与数据行 cell border 同色（无叠色），下边横线保持深蓝（色带延伸）
- Graphics 坐标系明确为 _grid 控件，不再误用 pnlScroll 坐标系

## V1.54f — 标题行最右侧残留垂直线修复（2026-08-09）

### 改动范围
- `Dialogs/SettingsForm.cs` — **覆盖范围扩展到 pnlScroll.ClientSize.Width**：
  - 之前 `Grid_RowPostPaint` 里 `rowRect.Width = _grid.Width`，只覆盖到 `_grid.Right`
  - DataGridView 在 `_grid` 右边缘外侧还会画一条 cell border（垂直线），导致"基础配置"等每个标题行最右侧仍有一条竖线
  - 改为 `coverRight = pnlScroll.ClientSize.Width`，覆盖矩形和上下水平线都延伸到 pnlScroll 右边缘

### 为什么这么改
- 用户反馈"基础配置 这行最右侧还有表格的竖线，去掉，下面的很多区域标题的右侧都有这个问题"
- RowPostPaint 的 Graphics 是 pnlScroll 级别的（不是 _grid 级别），所以可以画到 pnlScroll 整个宽度

### 优化点
- 标题色带现在横跨整个 pnlScroll 宽度（从 X=0 到 pnlScroll.Right），右侧不再有残留竖线
- 上下蓝色水平线也跟着延长

## V1.54e — 搜索框与分组标题对齐 + 标题横线改为深蓝（2026-08-09）

### 改动范围
- `Dialogs/SettingsForm.cs` — **搜索框与表格分组标题对齐 + 字号一致**：
  - `lblSearch` X 14 → 20（pnlScroll.Padding.Left=12 + colKey 文字左内边距 8 = 20），与下面"基础配置"等分组标题完美左对齐
  - `lblSearch` 字体 11F Bold → **10F Bold**（与分组标题行同款），视觉上"搜索配置项："和"基础配置"是同一族标题
  - `_txtSearch` 文本框 X 124 → 130（左移 6px 保持与 label 起点对齐），字体 11F → 10F
  - `_btnClearSearch` 清除按钮 X 448 → 454（左移 6px 保持与文本框的相对间距）
- `Dialogs/SettingsForm.cs` — **标题行上下水平线颜色修正**：
  - 之前用 `214,229,255`（极浅蓝，视觉接近白），用户反馈"颜色不对"
  - 改为 `48,119,238`（与标题文字同色，深蓝），视觉上与"基础配置"等标题字同色，是"标题带"的延伸

### 为什么这么改
- 用户反馈"搜索配置项 和 下面的 基础配置 左侧对齐，字体大小一样" + "标题下面的横线要蓝色"
- 之前搜索 label 在 X=14（pnlScroll 容器内）、表格第一列 colKey 在 pnlScroll.Padding.Left+8=20（容器内 X 坐标），错位 6px；且字号 11F vs 10F 不一致
- 横线 `214,229,255` 太浅，视觉上几乎看不到，不算"蓝色"

### 优化点
- 搜索框与表格标题完美对齐（X 20 + 字号 10F Bold），整张设置窗口视觉一致
- 标题横线改为与标题文字同色（深蓝 `48,119,238`），与标题色带融为一体

## V1.54d — 设置窗口表格与窗口边缘留出左右空隙（2026-08-09）

### 改动范围
- `Dialogs/SettingsForm.Designer.cs` — **`pnlScroll.Padding = (12,0,12,0)`**：让内部 `_grid`（仍 `Dock=Fill`）相对 pnlScroll 边缘左右各缩 12px，从而表格与窗口左右各留约 12px 空白带
- 之前 `pnlScroll` 无 padding、`_grid` Dock=Fill 撑满，表格左右边缘几乎贴窗口边缘，缺空隙；现在右侧已有 pnlScroll RectColor 边框做分隔，左侧也补上对称空隙

### 为什么这么改
- 用户反馈"表格的左边缘离整个窗口的左边缘太近了，稍微有点空隙吧，就像右边那样"
- 用 Padding 是最小侵入：不动表格列宽、不动控件结构，仅容器留白

### 优化点
- 表格左右对称，视觉上"居中悬浮"在中部

### 为什么这么改
- 用户反馈 V1.54c 首发版"目前还没有搞好"
- SunnyUI UIDataGridView 走自己的 OnPaint，单纯靠 e.Handled=true 拦不住；用 RowPostPaint 主动覆盖是 SunnyUI 自定义控件的标准做法

### 优化点
- 标题行从视觉上完全脱离表格样式（无列间垂直线、无上下水平线）
- 不改变数据行任何样式（RowPostPaint 跳过非分组行）
- 文字重画在覆盖层之后，保证标题文字仍清晰可见

## V1.54b — IO 备用通道映射弹窗箭头加粗 + 弹窗适度加宽（2026-08-09）

### 改动范围
- `Controls/IoMappingEditorPopup.cs` — **箭头列字符加粗 + 弹窗适度加宽**：
  - **加粗（V1.54 续）**：V1.54 首发版表头与数据行箭头都用"微软雅黑 12F, Regular"，但用户反馈"和下面还是有点差别"。根因是"→"是细线条字符、微软雅黑 Regular 视觉偏纤细，**数据行与表头都改 12F, Bold**，像素级一致；自绘路径（`Dgv_CellPainting`）与数据列 `DefaultCellStyle.Font` 同步更新
  - **加宽**：弹窗 `ClientSize` 560×268 → **640×268**；表格宽 536 → **616**；列宽 148+92+56+148+92=536 → 172+104+60+172+104=612。**根因**：原 5 列总宽 536 等于 `dgv.Width`，最右侧"新通道"列十六进制微调框按钮被裁切；现表格宽 616（多 4px 余量），刚好不裁切
  - 提示 `lblHint` 宽度 536 → 616 同步；取消/确定按钮重新靠右（398/466 → 476/546）

### 为什么这么改
- 用户反馈之前加太多（720 太宽），只需要刚好看到右侧边缘即可

### 优化点
- 弹窗从 720 缩回 640，刚好够用不臃肿
- 箭头表头与数据行像素级一致（加粗、12F、48,48,48、居中）

## V1.54 — IO 备用通道映射弹窗表头箭头样式自绘修复（2026-08-09）

### 改动范围
- `Controls/IoMappingEditorPopup.cs` — **表头箭头与数据行箭头样式彻底一致（自绘）**：
  - V1.53 通过 `_dgv.Columns["colArrow"].HeaderCell.Style.Font/ForeColor/Alignment` 子属性赋值想让表头箭头"→"与数据行一致（微软雅黑 12F、未加粗、黑色 48,48,48、居中），但 SunnyUI `UIDataGridView` 下 `HeaderCell.Style` 仍会被 `ColumnHeadersDefaultCellStyle`（9 号**加粗**、左对齐）覆盖——子属性赋值不能整段替换父样式，且加粗属性必须显式 `FontStyle.Regular` 才会去除。效果上表头"→"还是 9 号加粗，与下方数据行的 12 号未加粗箭头观感割裂
  - 改为 **`CellPainting` 自绘该列表头**：用 `e.Paint(CellBounds, Background|Border|SelectionBackground)` 走默认表头背景/边框，再用 `TextRenderer.DrawText` 以"微软雅黑 12F、`FontStyle.Regular`、黑色 48,48,48、居中"画"→"；表头与数据行像素级一致

### 为什么这么改
- 试过把 `ColumnHeadersDefaultCellStyle.Font` 直接改成 12F 也会同时影响"原寄存器/原通道/新寄存器/新通道"四列表头（这些列要求 9 号加粗，标题文字与数据列对齐明确），不可行
- 试过 `colArrow.HeaderCell.Style.Font = new Font(..., 12F, FontStyle.Regular)` 仍被 `ColumnHeadersDefaultCellStyle`（9F,Bold）继承下来（实测表头依然加粗、9 号）
- 自绘只针对箭头这一列的表头单元格，最小侵入、不污染其他列表头

### 优化点
- 表头箭头与数据行箭头视觉完全一致（字号/字色/居中/未加粗）
- 仍然走 SunnyUI 默认表头背景色（浅蓝 237,243,253）与边框，与其他表头保持风格统一
- 注释里把"V1.53 为什么没生效"也写清楚，后续如果要把同样套路迁到其他列可直接参考

## V1.53 — 系统设置窗口滚动卡顿根治 + 复制气泡 ToolTip 修复（2026-08-09）

### 改动范围
- `Dialogs/SettingsForm.cs` + `SettingsForm.Designer.cs` — **设置窗口滚动卡顿根治**：
  - 原实现把 8 个业务分类做成 **8 个独立 UIDataGridView**，放进 `pnlScroll`（SunnyUI UIPanel + AutoScroll）整页滚动。内容总高数千像素，滚动时 WinForms 要逐帧**物理移动 8 个重量级表格窗口并整块重绘**，必然卡顿（与主视图 V1.50 把 72 面板合并为单画布同理）
  - 改为**合并为 1 个 UIDataGridView**：分类标题不再用 UILine 分隔条，改用表格内的"分组标题行"（浅蓝底深蓝粗体，新增 `AddGroupRowStyle`）；表格 `Dock=Fill` 撑满 pnlScroll，滚动由 DataGridView 自身处理（**虚拟化，只重绘可见行**），pnlScroll 关闭 AutoScroll。搜索过滤（整组隐藏）/保存遍历（分组行跳过）/长按复制/IP·IO 映射弹窗全部适配单表格
- `Dialogs/SettingsForm.cs` — **长按复制气泡 ToolTip 不显示修复**：
  - 原用无坐标重载 `Show(text, window, duration)`，会把气泡定位到窗口默认位置（首次可能落到屏幕角落）；且 `CellMouseUp` 里同步 Show 会被 DataGridView 刚释放的鼠标捕获干扰，导致"已复制"气泡看不到
  - 改为**带坐标重载** `Show(text, Point, 1500)`（与主视图悬停提示同一可靠路径，锚点 = 光标右下 12px），`MouseUp` 里用 `BeginInvoke` 延迟到消息链处理完再弹，`OnShown` 预激活也改带坐标版本，并设 `ShowAlways=true`（模态对话框内也可靠显示）
  - **再次修复（宿主窗口改为窗体自身）**：首版气泡挂在 DataGridView 上，其内部窗口结构复杂，Show 的弹窗消息可能被表格窗口干扰导致仍不渲染；改为宿主窗口用 `SettingsForm` 本身（this，与主视图悬停提示一致），锚点取 `this.PointToClient(Cursor.Position)+12px`，Show 前先 `Hide` 清掉 ToolTip 残留显示状态
  - **提示时机（长按即弹，无需等松开）**：气泡由"松开鼠标后弹出"改为"长按到点（700ms，行业常规 500~800ms）复制成功即弹"；松开鼠标时再做一次兜底调用（`_pendingCopyTip` 已消费则空转，不会重复弹），气泡停留时长 1500ms→2500ms，保证按住时也能看清
- `Controls/IoMappingEditorPopup.cs` — **IO 备用通道映射弹窗箭头列样式一致**：表格中间箭头列（→）表头原本走全局表头样式（9 号加粗、左对齐），与数据行箭头（12 号黑色居中）字号/加粗/位置都不一致；单独覆盖该列 `HeaderCell.Style` 为与数据箭头完全一致（微软雅黑 12F、黑 48,48,48、居中），数据行箭头颜色同步统一为黑色（与正文同色）

### 为什么这么改
- 用户反馈设置窗口上下滑动很卡顿、ToolTip 不显示；两者都源于"多重量级控件 + 不可靠的 ToolTip 重载"两个架构层问题
- 单表格虚拟化滚动是 WinForms 大数据量滚动的标准做法，与项目 V1.50 主界面"单画布"优化思路一致

### 优化点
- 滚动只重绘可见行，彻底消除 8 表格整块重绘卡顿；分组标题行固定 30px、数据行按内容换行自动定高，长说明文本仍完整显示
- 搜索过滤、保存校验、IP/IO 映射弹窗、长按复制等行为与 V1.52 完全一致，仅实现层面迁移到单表格
- 复制气泡改为与主视图相同的可靠 ToolTip 路径，模态窗口内也能正常弹出

## V1.52 — 工位网格值框文字内边距 + View 界面注释补齐（2026-08-09）

### 改动范围
- `Models/PanelLayoutConfig.cs` + `bin\Debug\PanelLayout.json` — **值框文字左内边距**：新增 `ValueTextLeftPadding`（默认 6px），值框坐标不变，仅让框内文字与左边框留出间隔（解决"文字贴边/紧贴"观感）；该值可在 PanelLayout.json 中自行微调
- `Views/WorkstationGridView.cs` — `DrawValueBox` 绘制文字时矩形左移内边距（宽度同步缩短，文字不溢出右边框）；类头部注释新增**界面 ASCII 图**（整体网格 + 单面板内容布局 + 坐标标注 + 状态块配色说明），后续改界面可直接把该注释贴给 AI
- `Dialogs/CommunicationTestForm.cs` / `FanTestForm.cs` / `LoginForm.cs` / `ChangePasswordForm.cs` / `UserManagementForm.cs` / `HistoryRecordForm.cs` / `SettingsForm.cs` — 头部注释**补齐界面 ASCII 图**（原来只有文字描述或无布局说明），与 RecipeManagerForm 风格统一

### 为什么这么改
- 用户反馈值框内文字"紧贴左边界"；**首版方案（右移值框 X 57→70）会连带整个框移动，观感是"文本框左边界跟着文本移动"，被用户否决**，改为只加文字内边距、值框本身不动
- 界面图注释让 AI 助手（无法看图）能凭文本理解各窗体布局，便于后续直接按注释改界面

### 优化点
- 文字内边距在配置文件中可调，现场无需重新编译
- 值框坐标与 V1.51 完全一致，无布局错位风险

## V1.51 — 工位网格文字"糊成一坨"修复 + 布局外部化（2026-08-09）

### 改动范围
- `Views/WorkstationGridView.cs` — **重写绘制逻辑**：
  - **修复文字模糊/叠糊**：原实现 `OnPaint` 用 `g.TranslateTransform` 平移坐标系后再调 `TextRenderer.DrawText` 绘制文字，TextRenderer 走 GDI 绘制路径，与 Graphics 坐标变换叠加时位置/尺寸错乱，文字溢出到相邻元素上互相叠加（"糊成一坨"）。现在全部元素（背景/状态块/值框/标签/按钮/文字）一律改为**绝对坐标**绘制（元素坐标 = 面板左上角 + 设计坐标），彻底去掉 Transform 与 GDI 文字混用
  - **字体显式创建**：正文改用独立的 `_panelFont`（取自布局配置，默认微软雅黑 9pt），不再用 `this.Font`（后者继承主窗体 AutoScale 缩放字体，与固定像素矩形不匹配导致文字溢出）
  - **布局外部化**：面板坐标/颜色/字号/按钮与提示文字全部改读 `PanelLayoutConfig`，不再写死常量；`Configure` 首次运行时自动向程序目录导出默认 `PanelLayout.json`，现场改配置文件即可微调界面，无需重新编译
- `Models/PanelLayoutConfig.cs` — **新增布局配置模型**：面板网格尺寸、面板内各元素坐标（RcPower/RcWorkState/RcVacuumOpen/RcPressureValue/RcSNValue/RcRecipeValue/RcDelayStart*/RcSetButton/RcSelectBox/标签位置）、字体（字体名/正文字号/标题字号/标题加粗）、颜色（"R,G,B"字符串，覆盖背景/状态块/按钮/边框等全部配色）、提示文字；提供 `LoadOrDefault`（文件缺失/损坏回退默认值）与 `SaveDefault`（导出配置）；配套 `ElementRect`/`ElementPoint` 可序列化坐标类
- `Views/WorkstationGridView.Designer.cs` — Dispose 补充释放 `_panelFont`
- `AgingTestSystem.csproj` — 注册 `Models/PanelLayoutConfig.cs`

### 为什么这么改
- 用户反馈自绘画布上文字"看不清、糊成一坨"：根因是 TextRenderer（GDI）与 `g.TranslateTransform`（GDI+ 变换）混用导致文字绘制错乱，叠加到相邻元素上
- 同时把用户多次要求的"改坐标/改色/改字号"诉求从改代码升级为改配置文件，降低现场维护门槛

### 优化点
- 默认配置值与历史版本布局完全一致，升级后界面无感变化
- 布局配置只解析一次（构造时），绘制热路径零字符串解析开销

## V1.50 — 主界面工位网格滚动撕裂彻底解决：单窗口自绘大画布（2026-08-09）

### 改动范围
- `Views/WorkstationGridView.cs` + `WorkstationGridView.Designer.cs` — **新增自绘大画布控件**：整个网格（8列×9行面板 + 行全选按钮列）合并为 **1 个 UserControl**，尺寸 = 内容总尺寸（8×245+80 宽、9×225 高），由 `OnPaint` 按坐标绘制全部面板内容与行全选按钮；`UpdateAll`/`UpdateSingle` 只改内存字段 + `Invalidate`，并支持可见区域局部重绘（按 ClipRectangle 计算行列范围）；交互（长按选中/设置按钮/选中框/行全选/悬停提示）全部用坐标命中实现
- `Views/MainForm.cs` — `CreateWorkstationPanels` 重写：删除 TableLayoutPanel + 72 个面板 + 行全选按钮的创建逻辑，改为创建 1 个 `WorkstationGridView` 放入外层 `Panel.AutoScroll` 滚动容器（容器反射开启双缓冲）；`_panelViews`/`_rowSelectButtons` 两个字典字段及 `BtnSelectRow_Click`/`UpdateRowSelectButton`/`UpdateSelectionBoxVisibility`/`Panel_ClearAllSelectionRequested` 等方法整体删除（逻辑已内聚到 GridView 内部）；数据更新改走 `_gridView.UpdateAll`/`UpdateSingle`；`Panel_OnSetClicked` 改用 GridView 的 `SetSelected`/`GetSelectedDeviceIds`；`ShowBatchRecipeForm`/`GetSelectedDeviceIds` 改读 GridView 选中集合；GridView 行全选动作通过 `OnLog` 事件通知主窗体写日志
- `Views/WorkstationPanelView.cs` + `.Designer.cs` + `.resx` — **删除**：功能完全被 WorkstationGridView 替代，项目内已无任何实例化调用（对齐 V1.44/V1.45 移除死代码惯例）；csproj 相应移除三个文件的项目引用
- `AgingTestSystem.csproj` — 注册 `WorkstationGridView.cs` / `WorkstationGridView.Designer.cs`，移除 WorkstationPanelView 三件套
- `README.md` — 目录结构表把 WorkstationPanelView 条目替换为 WorkstationGridView

### 为什么这么改
- V1.49 把每面板内部 13+ 子控件改为自绘，滚动时仍需逐帧移动 72 个面板窗口，拖动滚动条仍有撕裂/卡顿
- V1.50 改为 RecyclerView 同源的"单窗口大画布"：滚动时系统只需移动 **1 个窗口**（内存 BitBlt 移动位图），72 个面板全部由 OnPaint 按坐标绘制且只重绘可见区域 → 无撕裂、无卡顿

### 优化点
- **完全不影响 1Hz 全量实时刷新与监控**：`UpdateAll` 只改内存字段 + Invalidate，1Hz 全量刷新开销极小
- 行全选/选中交互/悬停提示与 V1.49 行为完全一致，只是实现从"多控件事件"迁到"坐标命中"
- 后续若表数量继续增加（数百台），可按相同思路再做"按屏虚拟化"（只保留可见面板），当前 72 台规模不需要

## V1.49 — 主界面工位网格滚动撕裂优化：面板自绘重构（2026-08-09）

### 改动范围
- `Views/WorkstationPanelView.cs` + `WorkstationPanelView.Designer.cs` — **面板自绘重构**：原每面板 13+ 个子控件（Label/TextBox/Button/选中指示）全部删除，改为单个 UserControl 由 `OnPaint` 按坐标自绘全部内容（设备编号/上电灯/工作状态/真空压力/SN/配方/延时/设置按钮/选中框）；"设置按钮、选中框"等交互改用坐标命中（hit-testing），长按选中/单击切换/状态块悬停提示（ToolTip）逻辑全部保留；面板开启双缓冲
- `Views/MainForm.cs` — 主窗体与工位网格 `TableLayoutPanel` 开启双缓冲（TableLayoutPanel 的 `DoubleBuffered` 受保护，通过反射开启）

### 为什么这么改
- 原实现 72 面板 × 13+ 控件 ≈ 936 个控件窗口。WinForms 滚动时 ScrollableControl 要逐帧移动这些子控件窗口（MoveWindow），开销巨大，拖动滚动条必然撕裂/卡顿——双缓冲只能减轻"重绘闪烁"，救不了"移动窗口"本身，因此第一阶段"TextBox 换 Label + 三层双缓冲"现场实测无改善
- 自绘后 72 面板 = 72 个顶层控件，滚动时只需移动 72 个窗口，性能提升一个数量级，撕裂基本消除

### 优化点
- **完全不影响 1Hz 全量实时刷新与监控**：`UpdateData` 只改内存字段 + `Invalidate()`，72 面板全量刷新开销极小；无任何"滚动暂停刷新"逻辑（该方案会滞后数据，已弃用）
- 后续若表数量继续增加（数百台），可按相同思路再做"按屏虚拟化"（只保留可见面板 + 滚动回收复用），当前 72 台规模不需要

## V1.48 — 主界面技术员权限字体颜色加深（2026-08-09）

### 改动范围
- `Views/MainForm.cs` — `UpdatePermissionDisplay` 中技术员角色名颜色由 `Color.SkyBlue`（天蓝，偏浅）改为 `Color.RoyalBlue`（深蓝，更醒目）；相关注释同步更新
- `Views/MainForm.Designer.cs` — 两处"天蓝"注释改为"蓝"

### 为什么这么改
- 天蓝在浅色顶栏上对比度低，不够醒目；深蓝既有辨识度又与管理员红色、操作员绿色区分明显

## V1.47 — 用户管理窗体"用户名"改为可编辑下拉框，默认修改已有账号（2026-08-09）

### 改动范围
- `Dialogs/UserManagementForm.Designer.cs` — 删除独立的"账号"下拉框（cboAccount）与"新用户名"文本框（txtNewUsername），合并为可编辑下拉框 `cboUsername`，标签由"新用户名"改为"用户名"；窗体高度相应收窄，密码输入行上移
- `Dialogs/UserManagementForm.cs` — 账号列表直接填入 `cboUsername`（点击展开列出当前角色全部账号）；`GetSelectedAccount` 改为：下拉选中项优先 → 文本匹配已有账号兜底 → 回退到上次下拉选中的账号（作为改用户名目标）；新增 `_lastSelectedAccount` 记忆字段；"应用修改"默认场景是改已有账号密码，用户名框保持原样即只改密码，输入新名则改名

### 为什么这么改
- 原布局"账号下拉框 + 新用户名文本框"职责重叠且易混淆：此窗体默认用途就是修改已有账号（新增账号有独立"添加账号"按钮），"新用户名"叫法会让管理员误以为在此新增账号
- 改为可编辑下拉框后：下拉即"选要改哪个账号"，保持原样点应用=只改密码，直接输入新名=改名，一个控件承载两种操作，交互更直观

### 优化点
- 切换角色时下拉框尽量保持上次选中的用户名；应用修改成功后保持选中该账号，便于连续操作
- 删除/添加账号后列表自动刷新，新添加账号自动填入下拉框

## V1.46 — 操作员/技术员/管理员均可修改自己的密码（2026-08-09）

### 改动范围
- `Services/UserManager.cs` — 新增 `ChangeOwnPassword(oldPassword, newPassword)`：任意已登录角色修改自己密码，须验证当前密码（防止他人在无人值守时篡改）；新密码至少4字符、不得与当前密码相同；修改成功后自动清除该角色记住的登录信息（避免下次自动填充旧密码导致登录失败）。原 `UpdatePassword`（管理员改他人密码，无需验旧密码，用于忘记密码重置）保持不变
- `Dialogs/ChangePasswordForm.cs` + `ChangePasswordForm.Designer.cs` — 新增修改密码对话框：显示当前登录用户，输入当前密码/新密码/确认密码，逐项校验后调用 `ChangeOwnPassword`；Esc 取消
- `Views/MainForm.cs` — 用户权限下拉菜单在任意角色已登录时追加"修改密码"菜单项（未登录不显示），成功后写日志
- `AgingTestSystem.csproj` — 注册新增的两个窗体文件

### 为什么这么改
- 原设计仅管理员能改密码，操作员/技术员密码遗忘或需要定期更换时只能等管理员重置，流程繁琐
- 符合工业软件通行做法：所有用户可改自己密码（验旧密码）；管理员可重置其他账号密码（不验旧密码），同时管理员也能改自己的密码

### 优化点
- 改自己密码必须验证当前密码，避免机器无人值守时被他人随意改密
- 改密后自动清除记住密码，杜绝旧密码残留导致自动填充登录失败

## V1.45 — 移除未被使用的单台手动控制窗体 DeviceManualForm（2026-08-09）

### 改动范围
- 删除 `Dialogs/DeviceManualForm.cs`、`DeviceManualForm.Designer.cs` 两个文件（项目内从未实例化调用，属死代码）
- `AgingTestSystem.csproj` — 移除上述文件的项目引用
- `Services/IoMapBuilder.cs` — 注释里对 DeviceManualForm 的说明已过时，顺手去掉

## V1.44 — 移除已停用的启动 Splash 页面及其图片资源（2026-08-09）

### 改动范围
- `Program.cs` — 删除 `ShowSplashScreen()` 方法及其相关注释（含类注释里修复 L3 说明、Main 里的调用注释），并移除随之不再使用的 `using System.Threading`
- 删除 `Views/mForm_Progress.cs`、`mForm_Progress.Designer.cs`、`mForm_Progress.resx` 三个文件
- `AgingTestSystem.csproj` — 移除上述文件与 `Resources` 下两张启动图（`华际光电(1)(1).png`、`华际光电(1)(1)(1).png`）的项目引用
- `Properties/Resources.resx` / `Resources.Designer.cs` — 移除两张图片的嵌入资源与对应访问属性

### 为什么这么改
- 启动流程早已改为直接进入主界面（Splash 调用被注释），此功能不再使用，属历史遗留死代码，彻底删除避免维护困惑

## V1.43 — 公共参数窗口负压值输入框改为数值框（支持正负数）（2026-08-09）

### 改动范围
- `Dialogs/CommonParameterForm.Designer.cs` — `txtThreshold`（TextBox）改为 `nudThreshold`（NumericUpDown）：范围 -9999~9999（支持正负数）、保留 1 位小数、步进 1、默认 -95
- `Dialogs/CommonParameterForm.cs` — 保存逻辑去掉"非空/非数字"文本校验（NumericUpDown 天然保证输入为有效数字），直接读取 `Value`；居中定位代码同步改引 `nudThreshold`

### 为什么这么改
- 原 TextBox 需手输数字且无法防止非法字符；改为数值框后可点上下箭头调整、输入更直观，且天然限制范围，杜绝误输非法值

### 优化点
- 支持正负数（负压现场为负值，但需求要求正负数都允许）
- 保留 1 位小数（如 -95.5），与 `SetAllBarometerThresholds` 接收 decimal 的能力一致

## V1.42 — 登录窗体新增"记住密码"功能（2026-08-09）

### 改动范围
- `Dialogs/LoginForm.Designer.cs` — 密码框与按钮之间新增"记住密码"复选框，按钮/窗体高度相应下移
- `Dialogs/LoginForm.cs` — 窗体加载时读取该角色记住的登录信息并自动填充用户名、密码、勾选复选框；登录成功后按勾选状态保存或清除记住信息
- `Services/UserManager.cs` — 新增记住密码的读写：`GetRememberedLogin` / `SaveRememberedLogin` / `ClearRememberedLogin`，按角色分别存储，持久化到程序运行目录的 `RememberedLogin.json`；密码以 Base64 混淆存储（仅演示，非安全加密）
- `.gitignore` — 新增忽略 `RememberedLogin.json`（含密码的运行时用户数据，不入库）

### 为什么这么改
- 现场操作员/技术员反复切换权限登录，每次手动输入账号密码繁琐且易输错；记住密码后登录只需确认或输入密码即可，操作更快

### 优化点
- 记住信息按角色分别保存：切换为操作员/技术员/管理员时各自自动填充，互不干扰
- 未勾选"记住密码"登录成功时会自动清除该角色已记住的信息，避免残留
- 用户名自动匹配到下拉框已有账号则高亮选中，账号已删除时回退为手动输入值

## V1.41 — 用户账号支持多账号：登录下拉选择 + 管理界面增删改（2026-08-09）

### 改动范围
- `Services/UserManager.cs` — 数据模型由"每角色单账号"扩展为"每角色账号列表"（操作员/技术员支持多账号，管理员仍仅一个账号）；登录改为在目标角色的账号列表中匹配用户名+密码；`UpdateUsername`/`UpdatePassword` 改为针对指定账号对象修改；新增 `AddAccount`（添加账号，管理员唯一性校验）、`RemoveAccount`（删除账号，每角色至少保留一个、管理员账号禁止删除）、`GetAccounts`（获取角色下全部账号，供下拉框使用）；加载 `Users.json` 时按角色分组，管理员仅保留第一个账号（防止手改出多个）
- `Dialogs/LoginForm.cs` + `LoginForm.Designer.cs` — 用户名输入框由 TextBox 改为下拉框（ComboBox），加载时自动列出该角色已有账号并默认选中，仍可手动输入；登录校验兼容下拉选择值
- `Dialogs/UserManagementForm.cs` + `UserManagementForm.Designer.cs` — 新增"账号"下拉框列出该角色全部已有账号；新增"添加账号"/"删除账号"按钮（添加时弹出用户名/密码输入窗口）；"应用修改"改为针对当前选中账号执行；切换角色/账号时自动刷新列表并重置输入框

### 为什么这么改
1. **原实现每角色只有一个账号**：用户管理窗体只能改一个账号，多人共用同一角色时只能共用一套用户名密码，不便于区分操作记录。
2. **需求明确要求登录时"读取已有账号、下拉选择"**：把用户名输入改为下拉选择已有账号，减少手输错误；同时管理员账号有且仅有一个。

### 优化点
- 用户名在全部角色账号内保持唯一；删除账号有确认提示且每角色至少保留一个；若删除的是当前登录账号会自动登出
- 登录下拉框默认选中第一个账号（管理员登录即选中 admin），操作路径更短
- `Users.json` 格式不变（仍是 `List<UserAccount>`），旧数据无缝兼容，管理员重复账号在加载时自动去重

## V1.40 — 系统设置保存后热生效，无需重启（结构型配置除外）（2026-08-09）

### 改动范围
- `Dialogs/SettingsForm.cs` — 保存成功后把非结构型配置**就地回写内存中的 DeviceConfig 实例**（主窗体传入的同一引用），并把本次保存的配置项 key 集合暴露给主窗体（`SavedKeys`）；弹窗改为分层提示：只有结构型配置改动时才提示"需重启程序后生效"，其余提示"已即时生效"。新增结构型 / 各连接参数分类集合（`StructuralKeys` / `BarometerConnectionKeys` / `IoConnectionKeys` / `FanConnectionKeys` / `ScannerConnectionKeys`），新增 `ApplyChangesToConfig` / `ConvertConfigValue`（反射按属性类型转换，支持 0x 十六进制地址、IO 映射表、候选 IP 列表）
- `Views/MainForm.cs` — 设置窗口关闭后调用 `ApplySettingsHotReload`：采集间隔更新主定时器；气压表串口 / 耦合器 / 送风机 / 扫码枪连接参数改动时在后台线程触发重连
- `Services/DeviceManager.cs` — 新增 `ReconnectBarometerReader`（Connect 内部先断开旧串口再按新参数连接）、`ReconnectIo` / `ForceReconnectFan`（即使已连接也先断开，用新 IP/端口/候选列表重连）、`UpdateCollectInterval`
- `Dialogs/SettingsForm.Designer.cs` — 顶部提示文案更新为"保存后立即生效（连接参数自动重连），仅结构型配置需重启"

### 为什么这么改
1. **原实现所有配置改动都要重启才生效**：服务在启动时一次性把 AppSettings 读进 DeviceConfig 后就不再更新，设置窗口只写 exe.config 不回写内存，导致现场改个寄存器地址 / IO 映射都要重启软件，体验差。
2. **各服务每次读写都实时访问 `_config.xxx`**（同一 DeviceConfig 实例）：只要保存后就地回写该实例，业务逻辑类配置（寄存器地址 / IO 映射 / 取反 / 小数位 / 缩放 / 报警阈值 / 老化参数等）立即生效；连接参数（串口 / IP / 端口 / 超时）在回写后触发对应设备重连即生效。
3. **结构型配置不能热改**：设备数量 / 面板布局 / Mock 开关 / 送风机启用影响运行期一次性建立的结构（状态数组、UI 面板、Reader/Controller 实现），热改会造成越界或状态不一致，因此不回写内存、照常写入配置文件并弹窗提示重启生效。

### 优化点
- 弹窗按改动内容区分文案，避免误导（不再一律说"重启程序后生效"）
- 重连均在后台线程执行（`Task.Run`），不阻塞 UI；复用各控制器已有的同步锁，避免与采集线程并发冲突
- 保存弹窗仍只弹一次：结构型提醒已并入设置窗口的保存提示，主窗体不再重复弹

## V1.39 — IO 备用通道映射界面/配置全链路十六进制统一（2026-08-09）

### 改动范围
- `Models/IoOutputChannelRemap.cs` — 通道解析统一为十六进制（带 0x 前缀，如 `@0x0A`），去掉旧的十进制位号分支；内部解析成 0~31 十进制位号供位运算
- `Controls/IoMappingEditorPopup.cs` — 通道微调框加 `0x` 前缀（显示 `0x00~0x1F`，与寄存器风格统一）；保存时通道按十六进制输出（`0x2000@0x0A->...`），配置与界面所见即所得；列宽与提示文案同步调整
- `Controls/DataGridViewNumericUpDownCell.cs` — `DataGridViewHexNumericUpDownCell.ShowPrefix` 注释更新（寄存器/通道均显示前缀）
- `Dialogs/SettingsForm.cs` — `IoBackupChannelMappings` 说明文案更新为"寄存器@通道均十六进制"
- `Views/MainForm.cs` — 备用通道映射解析处注释更新为十六进制格式
- `App.config` — 默认 `IoBackupChannelMappings` 及注释改为十六进制通道（如 `0x2000@0x00->0x2009@0x01`）

### 为什么这么改
1. **界面与配置显示不一致易引发误会**：原实现界面用十六进制微调框（`00~1F`），但保存时把通道转回十进制位号（`@10`），用户输入 `0A` 打开配置文件却看到 `@10`，会怀疑自己输错或软件有 bug。
2. **统一为"界面=配置=十六进制，换算只在代码内部"**：寄存器、通道在配置与界面均以十六进制出现（所见即所得），`IoOutputChannelRemap` 解析时内部再转成 0~31 十进制位号做位运算，对外无感。

### 优化点
- 弹窗提示明确"保存后配置与界面显示一致，无需换算"，消除用户困惑
- 通道显示加 `0x` 前缀后与寄存器风格完全统一，不易把十六进制误读成十进制
- 通道解析只认 `0x` 前缀十六进制（0x00~0x1F），其它写法直接报错，杜绝歧义

## V1.38 — IO 备用通道映射可视化编辑 + 修复设置名称长按复制（2026-08-09）

### 改动范围
- `Controls/IoMappingEditorPopup.cs`（新增）— IO 备用通道映射编辑器：一行一条映射，五列"原寄存器 → 原通道 → 新寄存器 → 新通道"，寄存器（0x0000~0xFFFF）与通道（00~1F）均为十六进制微调框，支持修改/添加/删除
- `Controls/DataGridViewNumericUpDownCell.cs` — 新增十六进制数字单元格 `DataGridViewHexNumericUpDownCell`（编辑时弹出 Hexadecimal NumericUpDown 微调框），支持 `HexDigits`（显示位数）与 `ShowPrefix`（0x 前缀）配置，寄存器与通道共用
- `Models/IoOutputChannelRemap.cs` — 通道号合法范围由 0~15 放宽到 0~31
- `Controls/IpListEditorPopup.cs` — 单元格类由 `DataGridViewIpListCell` 更名/泛化为 `DataGridViewPopupEditCell`（点击弹编辑器），供 IP 列表与 IO 映射共用
- `SettingsForm.cs` — IoBackupChannelMappings 改用弹窗编辑器；修复"设置名称长按复制"气泡不显示
- `AgingTestSystem.csproj` — 注册新控件文件

### 为什么这么改
1. **IO 映射手输易错**：原 IoBackupChannelMappings 是自由文本，需手输"寄存器@位->寄存器@位"（如 `0x2000@0->0x2009@10`），格式/寄存器偏移极易写错。改为弹窗可视化编辑：一行一条映射、左右各显示"寄存器 + 通道"两个十六进制微调框，保存时自动换回原配置格式，既直观又杜绝非法项。
2. **界面与配置同构、无需换算**：界面四列（原寄存器/原通道/新寄存器/新通道）与配置格式"寄存器@位"一一对应，只是把配置里的十进制位号（0~31）显示成两位十六进制（00~1F），保存时再转回十进制位号，与 `IoOutputChannelRemap.ParseAll` 解析格式完全一致，不再依赖起始寄存器地址换算。通道号放宽到 0~31（00~1F），兼容 32 点/模块。
3. **长按复制气泡不显示（根因）**：
   - 长按计时到点（鼠标仍按住、被表格捕获）时调用 `ToolTip.Show`，气泡被鼠标捕获盖住不渲染。改为 Timer 到点只负责复制、先暂存提示内容，等松开鼠标（MouseUp）后再弹出气泡。
   - `ToolTip.Show(..., Point, ...)` 的 Point 是**控件客户端坐标**（内部实现 `windowRect.left + point.X`），原实现误用 `RectangleToScreen` 传了屏幕坐标，气泡被移到屏幕外看不到；且点坐标版首次调用时原生窗口尚未建好会**直接不显示**。改为 `OnShown` 里先空转一次预激活气泡，并改用 `Show(text, window, duration)`（光标定位版，走与悬停提示相同的 SemiAbsolute 路径）——松开鼠标时光标就在单元格上，气泡自然落在单元格附近。
   - 松开鼠标时目标单元格行列被 Timer 重置成了 -1，气泡定位失败直接返回。改为在 MouseDown 时把行列记到独立的 `_pressTooltipRow/_pressTooltipCol`，与复制计时用的 `_pressRow/_pressCol` 分开。

### 优化点
- IO 映射弹窗：原/新寄存器用 4 位十六进制（带 0x 前缀）、原/新通道用 2 位十六进制微调框防误输；中间箭头列只读固定显示 "→"；支持 Delete 键删行；添加按钮直接新增一行空白映射
- `DataGridViewNumericUpDownCell` / `DataGridViewHexNumericUpDownCell` 重写 `Clone()` 带出自定义属性（`Maximum`/`HexDigits`/`ShowPrefix` 等），否则 CellTemplate 克隆成实际单元格时会回落默认值——修复寄存器只显示 2 位、无 0x 前缀的问题
- 删除行逻辑沿用"先收集索引再删"，避免 DataGridView 删除当前行时误删全部
- 设置名称长按复制改为 Timer 触发，按住 700ms 即复制，松开鼠标后气泡提示（气泡定位到单元格正下方）

## V1.37 — 候选 IP 弹窗去掉顶部蓝色标题栏（2026-08-09）

### 改动范围
- `Controls/IpListEditorPopup.cs` — 移除弹窗顶部的蓝色标题栏，改为无标题栏 + 浅灰描边，内容区整体上移紧凑排列

### 为什么这么改
- 蓝色标题栏与弹窗整体白底轻量风格不协调，视觉偏重；去掉后更贴合设置窗口的简洁观感

### 优化点
- 弹窗高度由 306 压缩到 268，无边框窗体用浅灰边框勾边，仍保持边界清晰

## V1.36 — 候选 IP 弹窗修复删除全清 bug + 界面改版（2026-08-09）

### 改动范围
- `Controls/IpListEditorPopup.cs` — 修复"删除最后一个 IP 导致全部被删"的 bug；界面改为与系统设置一致的 SunnyUI 蓝主题风格
- 弹窗新增蓝色标题栏、蓝/橙/灰样式按钮、水印输入框、操作提示文字，支持表格内按 Delete 键删除

### 为什么这么改
1. **删除最后一项会清空全部**：原删除逻辑在反向遍历中边删边判断 `Rows[i].Selected`。DataGridView 删除"当前行"时会自动把选区重设为"锚点行 → 新的当前行"之间的区间，若删的是最后一行，新区间覆盖所有剩余行，导致后续遍历把所有行都判为选中并全部删掉。改为先收集选中行索引、再倒序删除，并从根上不受删除过程中的选区变化影响。
2. **弹窗太朴素**：原弹窗是默认 WinForms 控件拼装，与程序整体 SunnyUI 蓝主题风格不搭。改用 UIDataGridView / UIButton / UITextBox，加蓝色标题栏与提示文字，视觉与系统设置窗口统一。

### 优化点
- 删除逻辑不再依赖删除过程中的选区状态，行为稳定可靠
- 表格内按 Delete 键即可删除选中行，操作更顺手

## V1.35 — 系统设置：FanIpCandidates 下拉编辑 + 设置名称长按复制（2026-08-09）

### 改动范围
- `Controls/IpListEditorPopup.cs`（新增）— 候选 IP 列表编辑弹窗：一行一个 IP 的可编辑表格，支持直接修改、输入新增、选中删除；确定前逐行校验 IPv4 格式
- `Controls/IpListEditorPopup.cs` — 新增 `DataGridViewIpListCell` 单元格（只读 + 右侧下拉箭头提示）
- `SettingsForm.cs` — FanIpCandidates 改用 IP 列表单元格，点击弹出编辑；设置名称列支持鼠标左键长按（≥700ms）复制到剪贴板并气泡提示
- `AgingTestSystem.csproj` — 注册新控件文件

### 为什么这么改
1. **FanIpCandidates 手输易错**：原为自由文本，需手输逗号分隔的 IP 列表，格式/非法 IP 易出错。改为点击弹出编辑器：一行一个 IP、可逐个修改/新增/删除，提交前逐行校验 IPv4，既直观又杜绝非法 IP 入库。
2. **设置名称无法复制**：设置名称列是只读的，用户想复制某个配置项的 key（用于向技术人员反馈/查配置）只能手抄。增加左键长按 ≥700ms 复制到剪贴板，附带"已复制"气泡提示。

### 优化点
- 弹窗支持回车快速新增、非法/重复 IP 即时拦截提示、点击弹窗外区域自动取消
- IP 单元格右侧绘制下拉箭头，视觉上提示"这是可点击编辑的下拉列表"
- 弹窗自动定位到单元格正下方，超出屏幕底部时自动改到上方显示

## V1.34 — 系统设置搜索框与表格滑块修复（2026-08-09）

### 改动范围
- `SettingsForm.cs` — 配置表格去掉无意义的垂直滑块；搜索框改用 SunnyUI UITextBox、放大字号与尺寸

### 为什么这么改
1. **表格右侧出现无意义滑块**：每个配置表格的高度是按"可见行行高之和"自动算好、恰好完整显示全部内容的（外层滚动面板负责整页滚动），本不需要表格自带的垂直滚动条。`ScrollBars.Vertical` 在搜索过滤后仍显示滑块，且清除搜索后部分表格滑块残留（DataGridView 滚动条状态刷新不稳定）。改为 `ScrollBars.None` 后从根上消除。
2. **搜索框字体过小**：原输入框字号 9F（旁边标题 11F 加粗），22px 高，视觉不协调。改为 SunnyUI UITextBox（与 FanTestForm/CommunicationTestForm 输入风格一致），字号 11F、加宽加高，并带"输入关键字过滤配置项"水印提示。

### 优化点
- 搜索框类型由 WinForms TextBox 换成 Sunny.UI.UITextBox，风格与主程序统一
- 表格固定不出现垂直滑块，搜索/清除搜索后布局始终干净一致

## V1.33 — 系统设置窗口防错输入（2026-08-09）

### 改动范围
- `SettingsForm.cs` — 设置值列按配置项类型自动切换编辑控件；串口通讯参数改为下拉选择；修复数值框显示问题
- `Controls/DataGridViewNumericUpDownCell.cs` — 数字单元格控件（NumericUpDown 编辑），修复非编辑态不显示数字的 bug
- `Controls/DataGridViewStrictComboBoxCell.cs` / `DataGridViewEditableComboBoxCell.cs` — 新增只读/可手输下拉单元格
- `ModbusRtuBarometerReader.cs` — 停止位 15（=1.5）解析支持

### 为什么这么改
1. **布尔项乱输**：原 true/false 类配置项是可自由输入的文本框，用户手输 "ture"、"TRUE " 等非法值，保存校验虽会拦截但体验割裂。改为下拉框只允许选 true / false，从源头杜绝。
2. **串口名乱输**：PortName 需手动输入，记错/打错端口名导致气压表连不上。改为下拉框列出系统当前检测到的所有串口，直接选择；保留"留空=自动识别"语义（空值不强制选第一个）。
3. **数字项乱输**：波特率、超时、寄存器等数字项自由输入，易输入越界/小数位数错误。改用 NumericUpDown 微调控件，按配置项限制上下限、步进、小数位。
4. **串口通讯参数手输易错**：波特率/数据位/停止位/校验位直接手输，容易填错枚举名（如 Parity 写 "Odd" 与 ScannerService 的 switch 大小写约定不一致导致解析为 None）。统一改为下拉选择，显示中文/标准值、存储内部约定的枚举值。

### 优化点
- 布尔下拉：当前值是 true 时下拉只有 false（反之亦然），即一键切换
- 串口下拉：实时读取系统已装串口；当前值不在列表时也保留，避免已配置但未插的端口被误清（气压表 PortName 与扫码枪 ScannerPort 共用同一套下拉）
- 波特率下拉：列出低速/中速/高速常用档位（110~921600），且**支持手输自定义波特率**（可编辑下拉，输新值自动补入）
- 数据位下拉：5 / 6 / 7 / 8
- 停止位下拉：1 / 1.5 / 2（配置存 1/15/2，15 表示 1.5，与 ScannerService 约定一致，ModbusRtu 同步支持）
- 校验位下拉：无校验(NONE) / 奇校验(ODD) / 偶校验(EVEN) / 1校验(MARK) / 空格校验(SPACE)。界面显示中文，存值经映射归一为标准枚举名 None/Odd/Even/Mark/Space，兼容历史小写/中文/缩写写法，任何非法字符一律归为 None，杜绝配置里出现非法校验位导致解析失败
- 数字微调：每项独立配置范围（如 SerialReadTimeoutMs 10~60000、AlarmPressureThresholdKPa ±200 两位小数），保存前仍做二次校验
- 修复 DataGridViewNumericUpDownCell 非编辑态空白不显示数字的问题（FormattedValueType 改为 string + 重写 GetFormattedValue）
- 单元格统一样式：白底深字 + 主题蓝选中色，下拉框/数字框不再出现系统灰色底

## V1.31 — 配置搜索 + 配方操作优化 + 配方自动检索（2026-08-09）

### 改动范围
- `SettingsForm.cs` — 新增搜索框，支持按关键字快速过滤配置项
- `RecipeManagerForm.cs` — btnUpdate 找不到同名配方时直接添加，btnAdd 提示文案优化
- `BatchRecipeForm.cs` / `StationSettingsForm.cs` — 配方名称输入框新增自动检索，输入时弹出模糊匹配列表，选中后自动填写配方名称、延时时间、启动时间、极限温度
- `RecipeAutoCompleteProvider.cs` — 新增配方自动检索辅助类，支持防抖、键盘导航、鼠标选择
- `BatchRecipeForm.Designer.cs` / `StationSettingsForm.Designer.cs` — Dispose 方法增加自动检索资源释放

### 为什么这么改
1. **配置搜索**：SettingsForm 配置项多达 40+ 个并按 8 个分类分布，每次查找特定配置项需要逐行滚动扫描，效率低。增加搜索框后输入关键字即可快速过滤显示匹配项，无需记忆配置项所在分类。
2. **配方操作优化**：btnUpdate 原来找不到同名配方时仅弹窗提示"列表中不存在"，用户需要再点"添加"才能完成操作，流程割裂。改为找不到时自动添加，减少操作步骤。btnAdd 提示语增加"覆盖"二字，语义更清晰。
3. **配方自动检索**：用户在批量设置/工位设置窗口输入配方名称时，需要手动记忆已存在的配方名称并逐个字符输入，无法快速复用已有配方配置。增加自动检索后，输入时弹出模糊匹配列表，选中即可自动填充所有参数，大幅提升操作效率。

### 优化点
- SettingsForm 搜索支持对"设置名称"和"说明"两列同时匹配，匹配结果实时过滤并重新布局，无匹配的分类自动隐藏
- 配方自动检索使用 300ms 防抖定时器，避免频繁刷新列表导致界面卡顿
- 支持键盘上下键导航、Enter 确认、Escape 关闭，交互与主流 IDE 一致
- 点击下拉框外部区域（含窗体空白处、其他控件）时收起匹配列表，文本框内容保持原样（视为未选择），不再清空
- 用户从匹配列表选中配方回填后不再重复弹出匹配列表，只有产生新的输入时才继续匹配
- 下拉框失焦收起改为消息级过滤（`IMessageFilter` 监听鼠标左键），点击不可获得焦点的区域也能正常收起

## V1.32 — 系统设置窗口排版与换行优化（2026-08-09）

### 改动范围
- `SettingsForm.cs` — 设置列表内容自动换行、行高按内容计算、搜索框样式优化、搜索过滤排版修复

### 为什么这么改
1. **内容显示不全**：设置列表行高固定 24px，"说明""设置值"列的长文本（如 IO 备用通道映射表）换行后被截断显示不全。改为三列均启用换行，行高按内容（TextRenderer 测量换行高度）逐行计算，保证内容全部显示。
2. **搜索过滤排版错乱**：搜索"映射"等关键字时，被过滤隐藏的分类仍占用页面排布空间、网格高度仍按全部行数计算，导致结果与搜索框之间出现大片空白；且窗体尚未显示时控件 `Visible` 恒为 false，若用控件可见性判断分类是否参与排布，会直接导致初始布局全部错乱。
3. **搜索框样式**：原"搜索配置项"标签 9pt 偏小不醒目，加大并加粗、主题蓝着色。

### 优化点
- 设置列表三列（设置名称 / 说明 / 设置值）均启用自动换行，行高按每行内容测量计算（`ComputeRowHeight`），长文本全部显示不被截断，最小行高保持 24
- 搜索过滤排版修复：隐藏分类用独立 `_sectionVisible` 状态数组标记（不依赖控件 `Visible`），不参与排布；网格高度仅统计可见行
- 搜索框标签"搜索配置项："加大加粗（11pt Bold）、主题蓝着色，搜索框与清除按钮位置同步右移

## V1.30 — IO 触发后气压表压力值快速刷新（快速跟踪，2026-08-08）
- **需求**：触发 IO（开/关真空阀、上/断电、启动/停止测试）后，气压表压力值更新有约 1~3 秒延时——72 台气压表逐台串行 Modbus RTU 轮询，一轮全量采集耗时决定了刷新周期，IO 写后必须等下一轮轮询才读到压力变化。
- **实现**：写输出成功后对目标工位启动**独立高频补读定时器（250ms/次）**，只读这几台压力 + IO 状态，立即广播刷新对应面板，**压力变化 ≤0.5 秒可见**；跟踪窗口 12 秒（覆盖真空建立 15s 内从常压抽到目标负压）后自动退出，恢复正常全量轮询。
- **埋点**：`DeviceManager.SetOutput`（统一覆盖主窗体"开启真空"、设备手动窗体开/关阀与上/断电、工位设置"载台下电"）+ `StartTesting`（跟踪真空建立）+ `StopTesting`（跟踪压力回落）；报警联动 / 老化到时 / 人工复位**不触发**（面板已即时标"故障/空闲"状态色，压力回落非紧急）。
- **线程安全**：快速跟踪集合受锁保护、定时器防重入（`Monitor.TryEnter`，对齐 `_collectLock` 模式）；单台串口读复用 `ReadData` 内部 `_syncRoot` 与全量轮询互斥，单台仅 ~10~20ms 开销，不影响 72 台正常采集。

## V1.29 — 移除 JSON 兼容 + 工位面板待机配色调整（2026-08-08）
- **移除 `[JsonProperty]` 兼容**：`RecipeConfig` / `StationCacheEntry` 的时间字段不再用 `[JsonProperty]` 保留旧 JSON 键名（`DelayStartTime`/`DelayArriveTime`），JSON 键名直接用新属性名（`DelayTime`/`StartTime`）；旧 Recipes.json / StationSettings.json 数据已不兼容，需删除重建（用户已清空旧配方，不保留兼容代码）。
- **工位面板配色微调**：`boxPower` 下电（原红底白字）与 `boxVacuumOpen` 真空关（原红底白字）改为浅灰(LightGray)底黑字，与每行最右侧全选按钮同色，降低待机状态的视觉刺激；红色仅保留给工作状态"故障"（`boxWorkState`）。

## V1.28 — 时间输入样式统一：冒号分隔 + NumericUpDown（2026-08-08）
- **配方管理窗口**：延时时间 / 启动时间由"时/分/秒"三个带单位标签的数字框，改为 `时:分:秒` **冒号分隔**显示（单位标签改为 ":"，移除"秒"单位标签）。
- **批量设置配方窗口**：删除"延时时间2"，仅保留"延时时间"与"启动时间"；两者均由三个 TextBox 改为三个 `NumericUpDown`（时0-99/分0-59/秒0-59，冒号分隔），控件命名同步改为 `nudDelayHours/Minutes/Seconds`、`nudStartHours/Minutes/Seconds`。
- **工位设置窗口**：`txtDelay` / `txtStart` 改为与配方管理窗口一致的三框 `NumericUpDown` 冒号分隔样式（`nudDelayHours/Minutes/Seconds`、`nudStartHours/Minutes/Seconds`），相关逻辑同步调整——读取用 `GetTimeSpan` 组合三框，回填用 `SetTimeInputs`（越界钳制到控件范围），成功提示时间文本用 `GetTimeText`。
- 说明：三个窗口的时间输入样式 / 命名自此统一，字段映射对齐——**延时时间 → `DelayTime`（工位面板"延时开启"），启动时间 → `StartTime`（工位面板"延时到达"）**；字段名同步统一（原 `DelayStartTime`/`DelayArriveTime` 改名为 `DelayTime`/`StartTime`）；批量设置窗口原"启动时间"输入被丢弃（校验后不保存）的缺陷一并修复，现在两个时间都写入配方。（注：V1.28 提交时曾用 `[JsonProperty]` 兼容旧 JSON 键名，V1.29 已移除，见下条。）

## V1.27 — 配方管理移除"保存设置"按钮：操作即自动落盘（2026-08-08）
- **配方管理窗口**：移除底部"保存设置"按钮（按钮、底部面板及窗口高度一并调整）。
- **操作即持久化**：添加 / 更新 / 删除 每次操作成功后自动把整个配方列表写入 Recipes.json
  （新增 `PersistRecipes()` 统一落盘，失败弹窗提示），关闭窗口不再需要手动保存，改动重启程序不丢失。
- 说明：原"保存设置"是唯一落盘入口（添加/更新/删除仅改内存），故先让三个操作按钮自动落盘，再移除该按钮。

## V1.26 — 配方应用打通：批量/工位设置保存配方 + 工位下电（2026-08-08）
- **批量设置配方"加入队列"**（重写）：先把当前配置的配方保存到本地配方列表（Recipes.json，有同名弹窗询问是否覆盖更新）；
  再判断是否选中了工位面板——一个都没选中 → 提示"请先选择工位"（配方已保存，可关闭窗口选好工位后重新打开本窗口点击加入队列，
  或在「参数设置 → 配方管理」中选用该配方）；有选中 → 把配方名称 / 延时开启 / 延时到达应用到所有选中的工位面板。
- **工位设置窗口"保存"**：应用配置到本工位面板（写 DeviceManager 工位静态信息 → 采集叠加 → 面板更新）+
  缓存配置（新增 `StationSettingsCache`，`StationSettings.json`，下次点击该工位"设置"按钮自动回填上一次缓存）+
  保存配方到本地配方列表（同名询问覆盖更新）。
- **工位设置窗口"加入对列"**：与"保存"一致——把配置加载到对应工位的 WorkstationPanelView + 保存配方到本地配方列表。
- **工位设置窗口"下电"**（实现）：关闭当前工位载台上电输出
  （内部编号 = `TotalInputs + TotalBarometers + deviceId`，已下电则仅提示）；"破空"业务待确认，保留 TODO。
- **`RecipeStorage.SaveWithDuplicateCheck`**：批量/工位窗体共用的"界面配方 → 共享列表 + 落盘"方法，
  同名配方弹窗询问覆盖更新，新增配方自动分配编号。
- 批量设置配方窗口改构造 `(DeviceManager, 共享配方列表, 选中工位)`，移除旧队列/事件（OnRecipeAdded/GetRecipeQueue）逻辑；
  工位设置窗口改构造 `(DeviceManager, DeviceConfig, 共享配方列表, deviceId)`，主窗体传 `_recipes` 使新配方即时进入配方管理列表。

## V1.25 — 配方管理编辑化 + 持久化 + 批量设置窗口对齐（2026-08-08）
- **配方管理窗口设置区改可编辑**：配方名称改输入框；延时时间 / 启动时间按 **时、分、秒** 三个数字框分拆输入（`NumericUpDown`）；极限温度数字框（℃）。左侧列表选中某配方时，设置区自动同步显示该配方的设置内容。
- **添加防重名**：点击"添加"时若名称已存在，弹窗"已存在XXX配方，是否更新配方"（确定=走与"更新"相同逻辑）；"更新"按当前名称定位列表中配方并覆盖；"删除"按当前名称定位，弹出二次确认（确定/取消）。
- **配方持久化**：新增 `RecipeStorage`（程序目录 `Recipes.json`）；"保存设置"把整个配方列表及每项设置落盘，主窗体启动时 `LoadRecipes()` 自动加载恢复（V1.12 配方管理窗口自此闭环）。
- **配方列表滚动**：左侧列表固定行高并启用垂直滚动条（`ScrollBars.Vertical`），条目过多时区域右侧出现滑块可下滑查看。
- **批量设置配方窗口对齐**：修复左侧标签与右侧输入框垂直错位——标签由"靠单元格顶部"改为 `Dock=Fill + MiddleLeft` 垂直居中，与右侧输入框中心对齐。

## V1.24 — 面板"设置"智能分流 + 长按取消全选 + 离线/配色优化（2026-08-08）
- 面板"设置"按钮点击逻辑优化（`Panel_OnSetClicked`）：点击时若按钮所在工位未被选中，先将其加入选中集合（选中框同步显示）；
  再按选中数量分流——只选中 1 个工位 → 弹出该工位的 `StationSettingsForm`；选中 2 个及以上 → 弹出 `BatchRecipeForm`。
- 批量设置配方窗口打开逻辑抽为公共方法 `ShowBatchRecipeForm()`，供"批量设置配方"菜单与面板多选场景复用（队列处理/日志一致）。
- 长按空白处**取消全部选中**：全表未选中时长按仍为"选中该工位"；已有选中（选中框可见）时长按空白处 → 取消全部选中并隐藏所有选中框
  （`ClearAllSelectionRequested` 事件 → 主窗体 `Panel_ClearAllSelectionRequested` 统一置 `IsSelected=false` + `UpdateSelectionBoxVisibility`）。
- **离线/未加载状态标红**：状态栏"在线"统计全部离线（`在线: 0/N`）时 `toolStripStatusLabelOnline` 文字变红（默认即红，其余情况恢复默认色）；
  默认显示"未连接"的 `lblFanState`/`lblCommStatus` 设计时默认字体改红色，数据未加载时直观告警。
- **工位面板配色**：`boxVacuumOpen` 默认"真空关"改为红底白字（与加载后一致）；`boxWorkState` 测试中=繁忙改红绿灯**黄灯色**（Gold，原绿），空闲改 **LimeGreen**（原浅灰）；`boxPower` 上电状态灯未上电由灰色改 **红色**（上电仍为 LimeGreen）。

## V1.23 — 通讯测试窗体共享主程序连接（2026-08-08）
- 通讯测试/送风机测试窗体**不再自建 TCP 连接**，全部复用 `DeviceManager` 的共享连接，消除"连接数翻倍、IP 改了测试窗体能连主程序连不上"的割裂。
- `ModbusTcpIoController` 新增线程安全 `ReadHoldingRegisters`/`WriteSingleRegister`（与采集线程共用同一条连接 + `_syncRoot` 串行化）。
- `DeviceManager` 新增门面：`Config`、`ReadHoldingRegisters`、`WriteSingleRegister`。
- `CommunicationTestForm`：移除自建 TcpClient/IModbusMaster/心跳/重连定时器，连接状态按 `IsIoConnected` 1s 刷新（不发报文）；构造函数改 `(DeviceManager)`。
- `FanTestForm`：移除自建 FanControllerClient，走 `ReconnectFan`/`GetFanData`（读缓存零报文）/`StartFan`/`StopFan`；IP/端口只读显示主程序配置。
- **修复：扫码枪启动改回 UI 线程**。`MainForm` 启动优化曾把 `_scanner.Start()` 放进 `Task.Run`，而 `ScannerService` 内部依赖 UI 消息泵（`System.Windows.Forms.Timer` 重连/心跳 + `NativeWindow` 监听 `WM_DEVICECHANGE` 热插拔），后台线程下定时重连与热插拔监听全部失效，扫码枪拔插后检测不到。改为 `RunOnUi` 封送回 UI 线程执行。

## V1.22 — 通讯测试窗体补强（2026-08-08）
- 新增**一键遍历**：每 500ms 点亮一路、其余全灭，72 路通断跑马灯检测 DO 接线；单拍后台线程执行（写寄存器+读回真实状态），按钮状态实时反映真实通断。
- 打开即**后台自动连接**（3s 静默重试）+ **心跳**（1s 读 0x2000 探测，断连弹窗提醒）；所有 Modbus 读写共用锁串行化。
- 备用目标寄存器 0x2009 写入改**读-改-写**（`WriteBackupRegister`+`ComputeRemapTargetMask`，各测试只动自己映射位，对齐主项目 RMW）。
- 映射提示窗改**非模态悬浮窗**（WS_EX_NOACTIVATE 不抢焦点，单实例更新文本）。

## V1.21 — 通讯测试窗体 SunnyUI 重构 + 映射点击提示（2026-08-08）
- 原生控件改 SunnyUI（UIForm 标题栏 + UITabControl + UILedBulb 状态灯等）；点击被映射通道弹提示告知实际输出通道。

## V1.20 — 新增通讯测试窗体（2026-08-08）
- IO 耦合器 DO 输出手动测试：负压阀（Y000~Y107 @0x2000~2004）与载台上电（Y110~Y217 @0x2004~2008）两页 9×8 灯按钮，读-改-写不覆盖共享 0x2004；复用 App.config 备用通道映射。

## V1.19.x — 主界面工位交互与显示系列（2026-08-08）
- **V1.19.11** 工位 SN/配方/延时关联打通：新增 `StationInfo` + DeviceManager 每工位静态信息，采集时叠加到工位数据；工位设置窗口"保存"实现；ID 绑定把"工位→SN"写入；菜单"LOG记录"→"日志记录"。
- **V1.19.9** 报警阈值与界面单位统一为 kPa；**V1.19.10** 真空开启显示文字化（开=绿底白字/关=红底白字）+ 压力框加宽。
- **V1.19.5~6** 选中交互改"长按约 0.8s 选中 + 有选中才显示绿✓框"，选中框显示时单击切换；**V1.19.1~4** 行全选按钮文字实时反映行选中、工作状态配色统一信号灯色系、面板布局微调（SN/配方改 Label）。
- **V1.19.7~8** 权限显示角色名着色（管理员红/技术员天蓝/操作员绿）、用户管理窗体优化。
- **V1.19.12** 面板高度减小；主菜单"帮助"→"关于"，"关于"→"版本说明"。

## V1.18.x — 工位设置窗口 + 行全选（2026-08-08）
- 新增 `StationSettingsForm`（面板"设置"按钮打开，SN/配方/延时/启动时间/极限温度 + 破空/下电/保存/加入对列）。
- 行"全选/取消"按钮替代 Set(SEL_N)；状态文字改中文（空闲/选中/繁忙/故障）。

## V1.17 — 系统设置窗口（2026-08-07）
- "关于→设置"（仅管理员）弹出 `SettingsForm`：单页按分类（基础/串口/IO耦合器/气压表寄存器/报警/送风机/老化业务/扫码枪）表格编辑 App.config 全部配置项，保存前按类型校验，写回 exe.config 重启生效。

## V1.16.x — 扫码枪 + 连接自愈（2026-08-07）
- 接入真实扫码枪 `ScannerService`（WMI 识别串口 + 串口读码 + 断线重连）；ID 绑定窗体扫码自动识别"工位号(2位数字)/SN"。
- CH340 串口自动识别（`SerialPortHelper`，VID_1A86/PID_7523）+ 端口缓存 `BarometerPort.cache`（与送风机 FanLastIp.cache 同款工控机记忆）。
- 连接心跳机制（静默自愈）：断连 1~3s 内提示一次 → 后台静默持续重连（只在连上/断开边沿各提示一次），操作时按需重连+弹窗兜底；耦合器/送风机断开不再拖垮气压表采集。
- **V1.16.1** 四项现场修复：①负压阈值 -95 写成 -9.5（0x0002 小数位寄存器 47/72 台不可靠返回 0，压力读取与阈值写入统一固定 `BarometerDefaultDecimalPlaces`=1）；②顶部通讯状态只判断 IO 耦合器；③送风机监视区"上部温度"→"当前温度"；④送风机状态文字+颜色。
- **V1.16.3~6** 扫码枪断连判定演进：监听 USB 插拔消息 → 心跳重跑 WMI 动态搜索（PnP 节点消失即判定断连，注册表/ReadExisting 均不可靠）→ 多消息类型 + 关句柄重搜兜底。
- 工位面板更名重设计（BarometerPanelView → WorkstationPanelView）；移除 TEST 菜单与 ScanSimulationForm；公共参数窗体改为批量写气压表阈值（后台线程 + 失败汇总）。

## V1.15 — 老化测试业务闭环 + 送风机接入（2026-08-06）
- 新增送风机接入（Modbus TCP 50000）：定值启停 + 温湿度监视 + 全局生命周期（首台启动/末台停止）+ **IP 自动识别**（候选列表 + FanLastIp.cache）+ 连接防呆/竞态修复。
- DeviceManager 测试状态机：启动/停止/报警复位/全部停止（急停）、真空建立确认、通讯失联报警、老化计时自动停止。
- 设备阈值写入能力（气压表 0x0010）；IO 输出**备用通道映射**（DQ 通道烧毁时启用）；事件 CSV 落盘（`TestEventLogger`）+ 历史记录读真实日志；新增单台手动控制对话框。

## V1.14 — 真实通讯链路接入（2026-08-03）
- 气压表 Modbus RTU + IO Modbus TCP 真实实现，`UseMockCommunication` 切换；报警边沿→关阀/断载台电联动；新增 CHANGELOG 与通讯接入文档。

## V1.13 — 用户数据持久化（2026-07-24）
- 用户账号持久化 Users.json（启动加载/修改即存/损坏重建）。

## V1.12 — 配方管理窗口（2026-07-24）
- 左右分栏：左侧列表（序号+名称），右侧详情（配方名称/延时/启动时间/极限温度）+ 添加/更新/删除。

## V1.09 — IO 分配表接入（2026-07-22）
- 新增 IoMapBuilder（内部编号↔三菱八进制 X/Y 映射）与 IoPointDefinition；气压表 IO 调整为 1 输入+2 输出（真空负压表/电磁阀/载台上电）。

## V1.08 — 用户权限系统 + 自适应布局（2026-07-21）
- 登录/用户管理/权限按钮控制；主窗体自适应分辨率（rootScrollPanel + Anchor，缩小时出滚动条）。

## ≤V1.07 — 早期（2026-07-21）
- 下拉菜单改无边框弹出窗体（V1.05）；partial class 拆分 + UTF-8 with BOM 修复设计器（V1.01/07）；代码审查 33 项修复（V1.06）；初始 Mock 架构（V1.00~V1.04）。
