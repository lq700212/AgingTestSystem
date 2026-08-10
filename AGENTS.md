# AGENTS.md — AgingTestSystem 项目指南

> 本文件是 AI 助手在操作本项目前的**强制前置阅读**。开工前先读本文件，明确角色、约定与红线。
> 优先级：本文档 > 项目已有代码风格 > 通用最佳实践。

## 项目角色

你是本项目（Windows 窗体 C#/.NET Framework 应用）的**资深维护工程师**，负责按用户需求改代码、修 bug、沉淀约定。改动必须**可编译、可运行、风格统一**，并在关键改动后更新 `CHANGELOG.md`。

## 技术栈

- .NET Framework 4.x WinForms（非 .NET Core/.NET 5+，勿引入其语法/API）
- C#，语言版本取决于编译器（VS2019/2022 默认），以现有代码风格为准
- 关键库：SunnyUI（界面）、NModbus（Modbus 通讯）、Newtonsoft.Json（序列化）、System.Management（WMI/串口识别）
- 构建：`MSBuild.exe AgingTestSystem/AgingTestSystem.csproj`（见下方"构建命令"）
- 仓库：github.com/lq700212/AgingTestSystem，主分支 `main`，提交信息用中文，风格参考 `git log`

## 铁律（违反即返工）

1. **文件编码必须是 UTF-8**（无 BOM 或带 BOM 均可，跟随同目录文件）。
   - **禁止**用 PowerShell `Add-Content` / `Out-File` 默认编码写含中文的文件 —— 会写成 GBK 导致乱码。
   - 写文件用 write 工具；仅追加/改一行用 edit 工具。
   - 新增中文文件后自查：`[IO.File]::ReadAllText(path, UTF8).Contains("预期中文")` 能命中。
2. **不提交运行时数据与机密**：`Users.json`（明文密码）、`Recipes.json`、`StationSettings.json` 等程序运行生成的 json 一律 gitignore，绝不入库。
3. **改动后必须构建验证**，禁止提交编译不过的代码。
4. **不主动 commit/push**，除非用户明确要求；提交前先 `git status` + `git diff` 确认只包含预期改动。
5. **代码注释要详细，让小白能看懂学会**：关键方法/流程/边界条件/配置依赖必须写清"做什么 + 为什么这么写 + 怎么改"，杜绝只写变量名的废话注释（如 `i++ // 自增`）。允许的详细注释样式参考 `WorkstationGridView.cs` / `RecipeManagerForm.cs` 头部与关键方法。

## 代码约定

- 类、方法、属性用 PascalCase；私有字段 `_camelCase`；接口前缀 `I`；常量全大写或 PascalCase 跟随现有风格。
- 界面/控件命名：`btn`/`txt`/`nud`/`cmb`/`grid`/`pnl` 等匈牙利前缀（跟随 Designer 风格）。
- 枚举与配置值的存储约定（**改动串口/配置相关必须先读此处**）：
  - `StopBits` 存字符串 `1` / `15`（=1.5）/ `2`；校验位 `Parity` 存标准枚举名 `None`/`Odd`/`Even`/`Mark`/`Space`。读写两端大小写兼容（ModbusRtu 用 `Enum.TryParse(…, true)`，ScannerService 用 `ToLowerInvariant()` 匹配）。
  - 界面可显示中文/友好文案，但**存到 App.config 的值必须经过归一化映射**（见 `SettingsForm.NormalizeParity` / `NormalizeStopBits`），禁止把非规范字符写进配置。
- 配置项编辑控件统一在 `SettingsForm.CreateValueCell` 按 key 分发（布尔/串口/波特率/数据位/停止位/校验位/数字/文本）。新增串口类配置项时，**气压表与扫码枪两套 key（如 `PortName`+`ScannerPort`）都要覆盖**，共用同一套映射逻辑。
- **界面文件头注释必须带 ASCII 布局图**：所有 View/Dialog（`Views/*.cs`、`Dialogs/*.cs`）的类 XML 注释里都要有一段用 `┌─┐│└┘` 画出的界面布局图（参考 `RecipeManagerForm.cs` / `WorkstationGridView.cs` 头部注释），框内标注控件名与关键交互点。AI 无法看图，改界面全靠这段文本图，故**每次新增/修改界面文件都补画或同步更新该图**，且要和实际控件布局一致（坐标、控件名、按钮文字都对上）。
- **自绘控件（WorkstationGridView 等）的坐标类常量一律外部化**：不写死像素常量，放到布局配置模型（如 `Models/PanelLayoutConfig.cs`，可被 `PanelLayout.json` 覆盖），并把坐标标注进头部注释的 ASCII 图里，便于现场改配置微调间距/颜色/字号。
- **高 DPI 适配约定（V1.55 起）**：
  - 标准控件窗体用 `AutoScaleMode.Font`，WinForms 自动缩放，前提是 `app.manifest` 声明 `PerMonitorV2` **且** `App.config` 配 `Switch.System.Windows.Forms.DpiAwareness=PerMonitorV2`（两个缺一不可）。
  - **纯代码窗体（无 Designer）的高 DPI 三要素**（V1.58.4 实测血泪）：
    1. 必须显式设 `AutoScaleDimensions = new SizeF(6F, 12F)`，只设 `AutoScaleMode.Font` 会以 96DPI 为基准不缩放；
    2. **必须用 `SuspendLayout()` 包裹全部控件创建、在末尾 `ResumeLayout(false)`**——若未挂起布局时逐次 `Controls.Add`，WinForms 会在每次 Add 触发 PerformAutoScale 时把 AutoScaleDimensions 固化成当前 DPI 值（144 DPI 下变 9×18），导致"设计基准==运行基准"、缩放因子恒为 1、窗体永不放大。Designer 窗体天生带 SuspendLayout 所以正常，纯代码必须手动补齐；
    3. 验证时注意：纯代码 harness 需配 `app.manifest(PerMonitorV2)` + `.exe.config(AppContextSwitchOverrides)` 才能真正走 PerMonitorV2 缩放路径，只调 `SetProcessDPIAware()` 是 system-aware、AutoScaleDimensions 会被覆盖、测不出缩放。
  - 自绘控件（AutoScaleMode.None）坐标是 96DPI 逻辑像素，必须**内部手动乘 `_dpiScale = CreateGraphics().DpiX / 96`** 做 DPI 缩放（字体保持 pt 自动放大）；**禁用 `Graphics.ScaleTransform`**（TextRenderer 走 GDI 不认坐标变换，V1.51 踩坑）。
  - 获取实际 DPI 用 `CreateGraphics().DpiX`，**不要用 `Control.DeviceDpi`**（PerMonitorV2 下句柄刚创建时返回 96，实测不可靠）。
  - 新增自绘控件/改自绘坐标时，记得同步缩放命中检测（鼠标坐标是物理像素）、tooltip、局部重绘矩形，漏一处点击/重绘就错位。
- **自绘性能大坑（V1.57.3 血泪教训）**：**禁止用"离屏 Bitmap 整幅预渲染 + OnPaint DrawImage 拷贝"来优化自绘控件**。实测离屏大图（2040×2025）上 `TextRenderer.DrawText` 每处约 **2.2ms**（屏幕 DC 上近 0ms），全量渲染 72 面板一次高达 2247ms，而 `UpdateAll` 每秒全量刷新 → 整个软件每 1 秒卡死。且 `g.Clear(白色)` 会把面板间隙刷白导致"面板连成一片"。**正确做法**：OnPaint 只重绘可见区面板（`e.ClipRectangle` 算行列范围），数据/选中变化仅 `Invalidate`；滚动卡顿用"16ms 定时器节流 AutoScrollPosition + 画刷/画笔缓存字段"解决，不要预渲染。判断优化效果务必用**真实屏幕 DC**（`CreateGraphics`）测，离屏 Graphics 的 TextRenderer 慢是 GDI+ 固有行为、不代表真实帧速。

## 关键文件导航

| 文件 | 作用 |
| --- | --- |
| `AgingTestSystem/Views/MainForm.cs` | 主窗体、启动装配、配置加载 |
| `AgingTestSystem/Models/DeviceConfig.cs` | 设备配置模型 |
| `AgingTestSystem/Services/ModbusRtuBarometerReader.cs` | 气压表 Modbus RTU 读取 |
| `AgingTestSystem/Services/ScannerService.cs` | 扫码枪识别/读取 |
| `AgingTestSystem/Services/UserManager.cs` | 用户/权限（Users.json） |
| `AgingTestSystem/Services/RecipeAutoCompleteProvider.cs` | 配方名称自动检索 |
| `AgingTestSystem/Dialogs/SettingsForm.cs` | 系统设置（配置项编辑、校验、保存） |
| `AgingTestSystem/Controls/DataGridViewNumericUpDownCell.cs` | 数字/下拉单元格控件 |
| `CHANGELOG.md` | 版本改动记录（最新在前，V1.xx 小节） |

## 构建与验证命令

```powershell
# 构建（若提示找不到 MSBuild，先定位：Get-ChildItem 'C:\Program Files*\Microsoft Visual Studio' -Recurse -Filter MSBuild.exe | Select -First 1）
& "D:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe" AgingTestSystem/AgingTestSystem.csproj /p:Configuration=Debug /p:Platform=AnyCPU /t:Build /nologo /v:m
```

- 构建成功标准：输出 `AgingTestSystem -> ...\bin\Debug\AgingTestSystem.exe` 且无 error。
- 有 GUI 改动时可冒烟测试：`Start-Process` 启动 exe，等几秒确认进程存活再 `Stop-Process`。
- 无单元测试框架；以构建通过 + 冒烟测试作为验证手段。
- **界面像素级 bug（竖线/横线/颜色/叠色/裁剪/滚动条）**：调用技能 `winforms-ui-debug`——编译独立 harness 直接 new 目标窗体（指哪打哪，绕过登录/主流程），用反射探私有字段 + PrintWindow 截图 + 像素扫描定位根因并验证修复。含可复用的 csc 编译命令、坐标映射、色值字典与踩坑清单。
- **调试完自动沉淀技能**：每次用 `winforms-ui-debug` 排查成功（尤其是"一次性改对"的高光案例）后，**主动把可复用的新套路/新踩坑/新型探针代码回写到该 SKILL.md**（新增/补充小节、追加踩坑条目），不用等用户提醒。价值标准：换个人靠这份 skill 能更快解决同类问题。

## 文档同步（每次任务完成必做，逐条核对）

- **`CHANGELOG.md`**：功能/修复完成后必须在顶部新增或更新当前版本小节，写明"改动范围、为什么这么改、优化点"三部分（参考既有 V1.xx 小节格式）。改动再小也要记，防止现场追溯不到。
- **`README.md`**：若改动了目录结构、新增/删除文件、核心业务流、构建方式，同步更新对应章节（如"目录结构表"、`WorkstationGridView` 等条目），保持与实际代码一致。
- **`docs/通讯接入.md`**：寄存器/寄存器地址/Modbus 协议/串口参数/IO 映射等通讯类改动，必须同步到该文档，并写明对应版本号。
- **`AGENTS.md` 自身**：若本次工作中发现了新的约定、红线、套路（如"界面注释要画 ASCII 图"、"坐标要外部化到配置"），立刻沉淀进本文件，让下次任务自动遵守。
- **代码注释**：改动处的代码注释要详细到小白能看懂（做什么 + 为什么 + 怎么改），样式参考 `WorkstationGridView.cs` / `RecipeManagerForm.cs`；新文件/新方法尤其要写清头部说明。
- 注释里的中文请保持 UTF-8，写完后自查编码：`[IO.File]::ReadAllText(path, UTF8).Contains("预期中文")` 能命中。
- **提交前自检**：`git status` + `git diff` 确认改动范围与文档同步都完成后再交付；用户不要求 commit 时只留工作区改动即可。
