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
5. **不添加无必要注释**（除非项目已用 XML 注释风格，可跟随）。

## 代码约定

- 类、方法、属性用 PascalCase；私有字段 `_camelCase`；接口前缀 `I`；常量全大写或 PascalCase 跟随现有风格。
- 界面/控件命名：`btn`/`txt`/`nud`/`cmb`/`grid`/`pnl` 等匈牙利前缀（跟随 Designer 风格）。
- 枚举与配置值的存储约定（**改动串口/配置相关必须先读此处**）：
  - `StopBits` 存字符串 `1` / `15`（=1.5）/ `2`；校验位 `Parity` 存标准枚举名 `None`/`Odd`/`Even`/`Mark`/`Space`。读写两端大小写兼容（ModbusRtu 用 `Enum.TryParse(…, true)`，ScannerService 用 `ToLowerInvariant()` 匹配）。
  - 界面可显示中文/友好文案，但**存到 App.config 的值必须经过归一化映射**（见 `SettingsForm.NormalizeParity` / `NormalizeStopBits`），禁止把非规范字符写进配置。
- 配置项编辑控件统一在 `SettingsForm.CreateValueCell` 按 key 分发（布尔/串口/波特率/数据位/停止位/校验位/数字/文本）。新增串口类配置项时，**气压表与扫码枪两套 key（如 `PortName`+`ScannerPort`）都要覆盖**，共用同一套映射逻辑。

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

## 文档同步

- 功能/修复完成后在 `CHANGELOG.md` 顶部新增或更新当前版本小节：改动范围、为什么这么改、优化点。
- 寄存器/通讯协议类改动同步到 `docs/通讯接入.md`。
- 注释里的中文请保持 UTF-8，写完后自查编码。
