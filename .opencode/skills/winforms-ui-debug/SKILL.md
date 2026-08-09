---
name: winforms-ui-debug
description: 调试本项目（AgingTestSystem，WinForms/.NET Framework 4.x + SunnyUI）的界面渲染问题：标题行竖线、颜色叠加、边框/裁剪、DPI 缩放残影、控件错位等"像素级"视觉 bug。核心是"指哪打哪"——用户说哪个窗体/页面，就编译一个独立 harness 直接启动该窗体（绕过登录/主流程），再配合 PrintWindow 截图 + 像素扫描 + 反射探查来定位根因、验证修复。用户提到具体页面/窗体名（如设置窗口、SettingsForm、工位网格、WorkstationGridView）或"界面竖线/横线/颜色不对/叠色/滚动了/错位"等词时触发。
---

# WinForms 界面像素级调试（指哪打哪）

本技能沉淀"编译并直接启动指定窗体 + 像素探针"的调试套路。这是本仓库 UI bug 定位最快的方式：**不改主程序流程、不进登录页，直接 new 出目标窗体来观察**。适用于所有 WinForms/SunnyUI 渲染问题（分割线、颜色、裁剪、DPI、滚动条、对齐）。

> 开工前先读 `AGENTS.md`（文件编码 UTF-8、改后必构建、文档同步等红线全部适用）。

## 一、总流程

```
1. 构建项目（拿到最新的 exe + DLL）
2. 写独立 harness（.cs）→ 用 csc 编译成一个小 exe
3. 运行 harness：直接 new 目标窗体并 Show
4. harness 内用反射读私有字段、打印几何、像素扫描 / PrintWindow 截图
5. 定位根因 → 改代码 → 重新构建 → 重跑 harness 对比 before/after 像素
6. 冒烟测试主程序 + 更新 CHANGELOG.md（+ 必要时 README/docs）
```

## 二、构建与 harness 编译命令

### 1. 构建主项目

```powershell
& "D:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe" AgingTestSystem/AgingTestSystem.csproj /p:Configuration=Debug /p:Platform=AnyCPU /t:Build /nologo /v:m
```

构建产物在 `AgingTestSystem/bin/Debug/`（exe + 全部依赖 DLL）。**每次改完代码都必须重新构建再跑 harness**，否则 harness 引用的是旧 exe。

### 2. 编译 harness（关键命令，可复用）

```powershell
$bin = "E:\Project\AgingTestSystem\AgingTestSystem\bin\Debug"
& "D:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\Roslyn\csc.exe" `
  /nologo /t:exe "/out:$bin\UiProbe.exe" "C:\Users\ADMINI~1\AppData\Local\Temp\opencode\UiProbe.cs" `
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.dll `
  "/r:$bin\AgingTestSystem.exe" "/r:$bin\SunnyUI.dll" "/r:$bin\SunnyUI.Common.dll" `
  "/r:$bin\NModbus.dll" "/r:$bin\NModbus.Serial.dll" "/r:$bin\Newtonsoft.Json.dll" `
  "/r:$bin\DocumentFormat.OpenXml.dll" "/r:$bin\DocumentFormat.OpenXml.Framework.dll"
```

要点：
- 编译报错只看 `error CS` 行（`2>&1 | Select-String -Pattern "error CS"`）。
- `$?` 为真才运行 exe：`if ($?) { Push-Location $bin; & ".\UiProbe.exe"; Pop-Location }`。
- 若 `Roslyn\csc.exe` 找不到，用 `Get-ChildItem 'D:\Program Files\Microsoft Visual Studio' -Recurse -Filter csc.exe` 定位。
- **harness 源码放临时目录**（`C:\Users\ADMINI~1\AppData\Local\Temp\opencode\`），产物放 bin/Debug（gitignore，不污染仓库）。调试完记得删掉 `bin/Debug` 里生成的 `UiProbe*.exe`。

## 三、harness 通用骨架（直接改目标窗体）

```csharp
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

static class UiProbe
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool SetProcessDPIAware();

    [STAThread]
    static void Main()
    {
        SetProcessDPIAware();          // 关键！不设的话渲染结果 ≠ 真实程序（DPI 会不同）
        Application.EnableVisualStyles();
        var config = new AgingTestSystem.Models.DeviceConfig();
        var form = new AgingTestSystem.Dialogs.SettingsForm(config);  // ① 换成目标窗体
        form.StartPosition = FormStartPosition.CenterScreen;
        form.Show();
        Application.DoEvents();
        System.Threading.Thread.Sleep(400);   // 等首帧画完
        Application.DoEvents();

        // ② 反射拿私有字段（UIDataGridView 等）
        var f = typeof(AgingTestSystem.Dialogs.SettingsForm)
            .GetField("_grid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var grid = f.GetValue(form) as DataGridView;

        // ③ 打印几何（帮理解布局/坐标系）
        Console.WriteLine("Form=" + form.ClientSize + " Grid=" + grid.ClientSize + " DisplayRect=" + grid.DisplayRectangle);
        foreach (DataGridViewColumn col in grid.Columns)
            Console.WriteLine("Col " + col.Name + " W=" + col.Width + " Auto=" + col.AutoSizeMode);

        form.Close();
    }
}
```

### 如何"指哪打哪"启动任意窗体
- 打开目标窗体的 `.cs`，看构造函数签名（例：`SettingsForm(DeviceConfig)`、`RecipeManagerForm()`）。
- 构造所需依赖：大部分窗体直接 `new`；若依赖 `DeviceConfig` / `UserManager` / 主窗体引用，用反射或 `new` 现造的实例传进去。
- 不是主窗体的对话框类，直接 `new + Show()` 就能独立运行。

## 四、三大调试工具（本技能核心）

### 1. PrintWindow 整窗截图（最可靠，与屏幕位置无关）

`Screen Capture / CopyFromScreen` 在"窗体被居中到非标准分辨率 / 半屏外"时会得到错位、裁剪、甚至不可复现的像素数据（本会话踩过坑）。**整窗截图一律用 PrintWindow**，它渲染完整窗口（含标题栏/边框），输出位图坐标 = 窗口坐标，稳定可复现：

```csharp
[DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
[DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint f);
struct RECT { public int Left, Top, Right, Bottom; }

static Bitmap CaptureWindow(Form form)
{
    RECT r; GetWindowRect(form.Handle, out r);
    var bmp = new Bitmap(r.Right - r.Left, r.Bottom - r.Top);
    using (var g = Graphics.FromImage(bmp))
    {
        IntPtr hdc = g.GetHdc();
        PrintWindow(form.Handle, hdc, 0);
        g.ReleaseHdc(hdc);
    }
    return bmp;
}
```

### 2. grid 客户区坐标 → PNG 位图像素坐标映射

DPI 感知进程里 `GetCellDisplayRectangle`/`GetRowDisplayRectangle` 返回的可能是**物理像素**，且窗口有边框偏移，不能直接当 PNG 坐标用。正确换算：

```csharp
static Point GridToPng(Form form, DataGridView grid, Bitmap bmp, int gx, int gy)
{
    RECT win; GetWindowRect(form.Handle, out win);
    Point clientOrigin = form.PointToScreen(Point.Empty);   // 客户区原点(屏幕)
    Point gridOrigin  = grid.PointToScreen(Point.Empty);    // grid 客户区原点(屏幕)
    Rectangle scrGrid = grid.RectangleToScreen(grid.ClientRectangle);
    double scale = (double)scrGrid.Width / grid.ClientSize.Width;   // DPI 缩放（150% 时 = 1.5）
    int px = (int)Math.Round((gridOrigin.X - clientOrigin.X) + gx * scale) + (clientOrigin.X - win.Left);
    int py = (int)Math.Round((gridOrigin.Y - clientOrigin.Y) + gy * scale) + (clientOrigin.Y - win.Top);
    return new Point(px, py);
}
```

### 3. 像素扫描找"线"（定位竖线/横线/叠色的颜色与坐标）

沿一条水平线逐像素 `bmp.GetPixel(x,y)`，按颜色特征找线：
- 先打印整段色值（`x:R,G,B`），**用颜色值反推来源**：不同颜色 = 不同绘制者（例：cell border=GridColor `104,173,255`，滚动条左线=`80,160,255`，标题蓝=`48,119,238`）。
- 再扫多行 y（行 top/mid/bot），确认是"整行贯穿"还是"局部"，区分竖线/横线/文字像素。
- 用已知控件属性值做色值字典（`Color.FromArgb(...)` 与屏幕采样比对），快速锁定是谁画的。

### 4. 改像素前先读几何（一次命中的关键，本次 V1.54j 的做法）

遇到"某处要不要有竖线/横线、线画在哪"这类问题，**先反射打印布局几何 + 扫描色值，用几何推导出绘制坐标，再改代码**，往往一稿就过。比"改了→看→再改"的试错快得多。**而且一定要打印真实尺寸，别信代码注释里的硬编码值**（踩坑 8）。

拿"数据行最右边缘要有竖线、分组标题行不要"为例，定位候选 X 的思路链：

```csharp
// 1) 容器与边界真实几何（反射私有字段）
Console.WriteLine("pnlScroll.Padding=" + pnlScroll.Padding);          // 实测 Left=18，注释写 12 是错的！
Console.WriteLine("DisplayRect=" + grid.DisplayRectangle);           // 内容可视区
Console.WriteLine("CellBorderStyle=" + grid.CellBorderStyle + " GridColor=" + grid.GridColor);
// 2) 找盖在表格右边缘的滚动条子控件，这是"视线终点"
var sc = grid.Controls.OfType<Sunny.UI.UIScrollBar>().FirstOrDefault();
Console.WriteLine("UIScrollBar Bounds=" + sc.Bounds + " ShowLeftLine=" + sc.ShowLeftLine); // X=1377 W=27
// 3) 最后一列逻辑右边缘（即"想画线的位置"）
var cr = grid.GetCellDisplayRectangle(grid.Columns.Count - 1, 0, false);
Console.WriteLine("lastCol cell0=" + cr);                            // Right=1378
// 4) 推导：scrollBar 从 1377 起盖住 1377~1404，所以 cell.Right(1378) 不可见，
//    数据行"可见的最右像素"= scrollBar.Bounds.Left - 1 = 1376 ← 竖线就画这
// 5) 扫描该行带前后各 10px 确认底色（这一步是"验证候选坐标"而不是"找 bug"）
```

**通用推导口诀**：被子控件遮住的像素别去画——`可见最右 x = 子控件.Bounds.Left - 1`，`可见最下 y = 子控件.Bounds.Top - 1`。拿到这个 x 再决定画线的颜色（跟 `GridColor` 或对应 cell border 一致，避免另造新色）。

## 五、本仓库踩过的坑（下次直接避）

1. **Child 控件永远画在父内容之上**：SunnyUI `UIDataGridView` 内置一个 `UIScrollBar` **子控件**盖在表格右边缘（最后一列右边界 ~ 表格右边界）。它默认 `ShowLeftLine = True`，在 X=918 画一条 `80,160,255` 竖线贯穿每一行——分组标题行最右侧的"竖线"真凶就是它。**任何 RowPostPaint / CellPainting / 覆写 OnPaint 都压不过子控件**；正解是找到子控件改属性（`grid.Controls.OfType<Sunny.UI.UIScrollBar>().First().ShowLeftLine = false;`）。
2. **RowPostPaint 的 Graphics 被裁剪在行显示矩形内**：`e.Graphics.ClipBounds` 打印确认。想覆盖 cell border 画到 `_grid.Width+1` 是没用的——超出裁剪的部分画不上。别在"覆盖宽度差 1px"上反复纠结，先确认那个"线"到底是谁画的。
3. **逻辑像素 vs 物理像素（DPI）**：150% 缩放下 1 逻辑像素对应 1.5 物理像素，border 落在半像素上会被渲染到某个物理像素，颜色可能与你预期不同。**harness 必须 `SetProcessDPIAware()`**，否则渲染路径都不一样，探针结果不可信。
4. **Screen Capture 不可靠，用 PrintWindow**：`CopyFromScreen` + 居中窗口在多显示器/窗体超屏时像素错位、时有时无，浪费大量时间。整窗截图一律 PrintWindow。
5. **Color 值是最强的线索**：同一列边框是 `104,173,255`、滚动条左线是 `80,160,255`、滚动条轨道 `243,249,255`、表格背景 `237,243,253`、数据行背景 `243,249,255`。像素颜色不匹配 ≠ 你改的那个对象，先反查绘制来源再动手。
6. **验证 DPI 与"行是否真的被扫到"**：探针 y 取错位置会得出"没线"的假结论。用 `grid.GetRowDisplayRectangle(i,false)` + `RectangleToScreen` 映射后再取 y，别凭肉眼估。
7. **cell 逻辑右边缘 ≠ 可见最右像素**：SunnyUI UIDataGridView 的 `UIScrollBar` 子控件（Bounds.X=1377, Width=27）会盖住最后一列（colValue）右边缘 1~27px。所以 `GetCellDisplayRectangle(...).Right`（=1378）那个像素你是画不上/看不见的；想给数据行画"表格右边界竖线"，X 要取 `scrollBar.Bounds.Left - 1`（=1376）。**别把线画进子控件地盘**。
8. **代码注释里的硬编码坐标可能是错的，以实测为准**：例如 `Grid_RowPostPaint` 注释写"pnlScroll.Padding.Left=12"，实测却是 **18**；注释写"滚动条线在 X=918"，那只是当时窗口宽度的值。调试/改布局时，先反射打印 `Padding`/`Bounds`/`DisplayRectangle` 等真实值，再决定画在哪。自己写注释时也标注"实测"值。
9. **"某行有、某行没有"这类需求，用行号集合分流，不要另开事件**：需在 RowPostPaint 里给普通数据行画线、给分组行不画时，直接复用已有的 `_groupRows`（行号集合）做分支：`if (!_groupRows.Contains(e.RowIndex)) { 数据行逻辑; return; } else { 分组行逻辑; }`。既不动行样式、也不用区分事件，一次画绘里处理两类，可读性好。
10. **善用"批量纵向扫描多行"一次性确认规则**：改完"按行分支画线"后，harness 里循环前 N 行（`for r in 0..13`）对固定 X 取色，对照行号集合打印 `行号 [GROUP|data] 色值`，能一眼确认"分组行=浅蓝、数据行=线色"全部命中，比单看两行更稳。

## 五·五、高 DPI 适配专项（V1.55 沉淀）

用户报"高分辨率屏幕界面显示不正常"时，先分清两类控件，再决定适配方式：

### 判断根因：先确认是"不缩放"还是"缩放比例不一致"
- 打印 `form.DeviceDpi`、各关键控件 `CreateGraphics().DpiX`、`AutoScaleMode`。
- **标准控件窗体（AutoScaleMode.Font）**：WinForms 会自动按字体比例放大，通常无需动代码，只需保证 `app.manifest` 有 `PerMonitorV2` + `App.config` 有 `Switch.System.Windows.Forms.DpiAwareness=PerMonitorV2` 运行时开关（**两个缺一不可**）。
- **自绘控件（AutoScaleMode.None，如 WorkstationGridView）**：画布尺寸和坐标是"96DPI 逻辑像素"，**不随 DPI 放大**，但 pt 字体会自动放大 → 文字溢出格子、与周围标准控件比例失调。这是"主界面显示不正常"的典型根因。

### 自绘控件 DPI 适配三步
1. **计算缩放因子**：句柄创建后（`OnHandleCreated`）算 `_dpiScale = 实际DPI / 96`。**踩坑：`Control.DeviceDpi` 在 PerMonitorV2 下句柄刚创建时返回 96（不准确），必须用 `CreateGraphics().DpiX`（实测 144 才是真值）**。
2. **手动乘坐标，别用 ScaleTransform**：自绘里 TextRenderer 走 GDI 不认 `Graphics.ScaleTransform/TranslateTransform`（见坑 1、V1.51 踩坑），必须写 `Scaled(int/Point/Rectangle)` 辅助方法，把所有绘制坐标、画布 Size、命中检测（鼠标坐标是物理像素）、tooltip、局部重绘矩形统一乘 `_dpiScale`。字体保持 pt 单位自动放大，两者同步放大比例才一致。
3. **命中检测别漏**：鼠标 `e.Location` 是物理像素，与缩放后的布局矩形比较；可见列/行范围计算（`clip.Left / 列宽`）也要用缩放后的列宽，否则只重绘左上角一小块。

### 验证超画布自绘控件：用离屏 OnPaint，别用 PrintWindow
- 自绘画布 3060×3038 远超屏幕，**PrintWindow 只截可见区**，超屏部分全是背景残留色（Magenta），像素扫描全假失败。
- 正解：反射调用 `Control.OnPaint` 渲染到完整尺寸 Bitmap（`new PaintEventArgs(g, new Rectangle(0,0,宽,高))`），无视屏幕限制，再逐像素验证。
- 验证点示例（150% 缩放 = 逻辑坐标 ×1.5）：面板左上角 = `Scaled(2)=3`；上电块 = `Offset(Scaled(RcPower),3,3)`；行全选列 x = `Scaled(8*245+2)=2943`。



## 六、验证与收尾（必做）

- 构建通过（MSBuild 输出 exe、无 error）。
- harness 复跑，对比修复前/后同坐标像素颜色，确认目标线/色消失且不引入新问题。
- 冒烟测试主程序：`Start-Process` 启动 exe，等几秒确认进程存活再 `Stop-Process`。
- 更新 `CHANGELOG.md`（写清改动范围/为什么/优化点），必要时 `docs/通讯接入.md`、`README.md`。
- 删除 `bin/Debug` 下生成的 harness exe；源文件在临时目录不提交。
- UTF-8 自检：`[IO.File]::ReadAllText(path, [Text.Encoding]::UTF8).Contains("预期中文")`。

## 七、推广到其他项目

套路本身与项目无关：**任意 WinForms 项目**都可以"构建 → csc 引 bin 里的 exe+DLL 编译独立 harness → 直接 new 目标窗体 → 反射探私有字段 + PrintWindow 截图 + 像素扫描"。换项目只需替换：MSBuild/csc 路径、bin 目录、DLL 引用清单、目标窗体类名与构造函数。
