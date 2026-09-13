# AI 开发引导（首次接手本项目必读，10 分钟）

> 你是本项目的**资深维护工程师**：改代码、修 bug、沉淀约定，改动必须可编译、可运行、风格统一。
> **规范唯一源是根目录 `AGENTS.md`——开工前必须通读全文**，本篇不替代它，只回答：
> 这是什么项目 / 代码怎么组织 / 活怎么干 / 怎么验证。本篇与 AGENTS 冲突以 AGENTS 为准。

## 一、项目一句话

WinForms 桌面程序（.NET Framework 4.7.2 / C#，不是 .NET Core）：
监控 72 台气压表真空压力，控制 72 路真空电磁阀 + 72 路载台上电，
接 1 台冷却送风机 + 1 把扫码枪，走"抽真空→上电计时→到时下电"自动流程。

| 硬件 | 接入 | 关键类 |
| :--- | :--- | :--- |
| 气压表 ×72 | Modbus RTU 19200 8N1，从站=设备号，压力 0x04@0x0001 | `ModbusRtuBarometerReader` |
| IO 耦合器 GX-CL140 | Modbus TCP 192.168.1.20:502，DI 0x1000 / DO 0x2000 | `ModbusTcpIoController` + `IoMapBuilder` |
| 送风机 | Modbus TCP 端口 **50000**（非 502） | `FanControllerClient` |
| 扫码枪 Xenon 1902 | 虚拟串口 115200，WMI 自动识别 | `ScannerService`（必须 UI 线程 Start） |

`UseMockCommunication` 一键切 Mock，真机联调只改配置。

## 二、必读顺序（分层，别全读）

| 层 | 文件 | 什么时候读 |
| :--- | :--- | :--- |
| 每次必读 | `AGENTS.md` 全文 + 本篇 | 任何任务开工前 |
| 业务 | `README.md` 4/5/6 节（业务流/配置速查/菜单权限） | 动业务、动配置、动菜单 |
| 协议 | `docs/通讯接入.md`（唯一协议文档） | 动寄存器/串口/IO 映射/排障 |
| 现场视角 | `docs/现场工程师培训手册.md` | 改了操作入口/流程/按钮文字（改完同步它） |
| 地图 | `docs/内部开发人员说明.md` | 找文件、查同步清单 |
| 最近动态 | `CHANGELOG.md` 顶部 3 节 | 接手时了解"最近在折腾什么" |

## 三、业务速览（压缩版，细则见 README 4 节）

```
装夹 → 扫码绑SN → 下发配方 → 启动（只开阀不带电，参数定格）
→ 真空到位+延时到 → 自动上电计时 → 到时下电关阀 → 蓝屏待取料 → 复位回空闲
```

- 定格语义：启动瞬间定格，中途改配方只对新启动生效；配方优先、全局兜底。
- 时长 0 = 不限时长永不自动完成；SN 空可启动（警告），追溯无 SN。
- 报警统一关阀断电标红（边沿一次）：压力越限/真空超时=产品 FAIL；
  通讯失联=设备异常（不冤枉产品）；停止不清红蓝屏，必须报警复位；
  急停=全 OFF+清快照；断电恢复默认整台重测。
- 缺省全是"现状行为"：策略/规则/MES/电流/报表列全空=零行为，加东西只加严不松绑。

## 四、代码地图（三条主干）

```
视图 Views/MainForm + WorkstationGridView（自绘，坐标锚定，PanelLayout.json 可调）
  ↓ 弹出 Dialogs（SettingsForm 设置表 / ProcessPolicyForm 驾驶舱 / RecipeManagerForm 等三窗 / LicenseForm）
  ↓ 调用 Services/DeviceManager（编排：只做调用决策+IO+日志）
        ├─ 判定 → Services/AgingSequencer（纯函数，改判定的唯一入口，同步用例）
        ├─ 配置 → Models/DeviceConfig（App.config）+ ProjectPolicyStore（Policy.json，PolicyKeys 唯一名单）
        └─ 项目 → ProjectProfile（Projects/<项目>/；配方/策略/布局跟项目，用户/快照/日志跟机器）
```

## 五、高频任务 cookbook（步骤+落点，细则见 AGENTS 对应章节）

1. **加 App.config 项**：`DeviceConfig` 属性 → `App.config` key → `MainForm` 读取 →
   `SettingsForm`（`_boolKeys`+`ValidateValue` 两名单+`_descriptions`+`_categories`）。
   回归"布尔键一致"变绿才算完。
2. **加工艺策略 key**：`PolicyEnums`（0=现状，只能追加）→ `DeviceConfig` →
   `PolicyKeys` → `EnumOptions` → `App.config` → `MainForm`（`ParsePolicyEnum` 兜底）
   → `SettingsForm` 四分支（不进 `_boolKeys`）。"三处同步锁"变绿才算完。
3. **配方加字段**：`RecipeConfig` → 录入三窗（输入+保存+回填+检索+ASCII 图）→
   `SetStationRecipe`（null=保持，0=定格 0，勿混）→ `StationInfo`(+Clone) →
   `ApplyStationInfo` → `BarometerData`(+Clone)。
4. **改判定/时序**：先写 `AgingSequencer` 纯函数 + `TestRunner.cs` 用例，
   `DeviceManager` 只调不用写逻辑；快照加字段列状态变迁表。
5. **改 UI**：文件头 ASCII 布局图同步；Sunny 换肤（新窗 `ApplyTo`，按钮色不动）；
   Designer 三军规（Init 内无注释/以 VS 重写版为基线/resx 不删）；
   动态重建走 `DisposeAllAndClear`；叠放用"子控件"方案；tooltip 全覆盖。
6. **改 MES vocabulary**：只改 `MesMapping` 纯函数（三方共用），用例 try/finally 复位 Transport。
7. **修 bug**：先在 `TestRunner.cs` 对应模块加复现用例（红→修→绿），再修产品代码。

## 六、验证闭环（改完代码必须走）

```powershell
# 构建（MSBuild 找不到先定位，见 AGENTS）
& "D:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe" AgingTestSystem/AgingTestSystem.csproj /p:Configuration=Debug /p:Platform=AnyCPU /t:Build /nologo /v:m
# 日常小改：分级回归（新 .cs 先在 get_affected_modules.ps1 的 $Map 登记）
powershell -ExecutionPolicy Bypass -File .opencode\skills\agingtest-regression\scripts\build_and_test.ps1 -Affected
# 大重构/发布前跑全量（默认）；改 UI 加跑终结器审计（HIGH 拦提交）
```

新增 .cs 漏登记 csproj 报 CS0246；提交前 `git status`+`git diff` 确认范围；
用户没要求不 commit/push；提交信息中文。

## 七、二期池与已知缺失（原 `docs/问题确认清单.md` 已删，V1.86.3，有效信息迁此）

原文件使命完成：Q2/Q8/Q18/Q20 落地可配、V1.85 预置 A/B/C、签认表是一次性文件
（git 历史可回溯，签完字的纸质件自行归档）。仅 P2 三项未落地，迁此备查：

- **P2-❽ 分组错峰上电**：72 台同时上电冲击电流大，需"分组+间隔"配置与上电调度，
  现状无此逻辑，排期约 1~2 天（含回归）。
- **P2-❾ 三色灯柱/蜂鸣器**：红故障/绿完成/黄待判定+蜂鸣，装车间柱子。
  蜂鸣已可配（`CompletionAction` 带 Beep，无需硬件）；灯柱需硬件+灯逻辑+DO 点位。
  **坑**：`0x2009` 现在既是备用通道映射目标又是灯预留——真做时另选预留寄存器
  或与备用映射互斥校验，否则点灯即烧映射。
- **烟感**：预留 DI，超限全线急停+报警，硬件另计；可用自定义规则 `di0`
  曲线救国（持续秒拉长防误报），正式配置等硬件到了再做。

已知缺失：`AGENTS.md` 文档同步节引用的 `docs/现场业务预研Plan.md` 全仓无此文件
（历史遗留，待用户定夺补建或删引用）。

## 八、禁区（碰一条返工一条）

- 私钥 `tools/LicenseKeyGen/license_private.xml` 永不入库；`Users.json`/`Recipes.json`/
  `StationSettings.json`/`License.lic` 等运行时数据 gitignore。
- 拷贝文件夹到新机授权即拦是生意防线，别"修"掉；换公钥=老证全废。
- 落盘一律 `AtomicFile` + `BaseDirectory` 绝对路径；对外只吐副本；测试缝 finally 复位。
- PowerShell 默认编码写中文文件必乱码，用专用工具写文件。

## 九、文档分工（改了什么同步哪篇，交付前逐份过）

| 改了 | 同步 |
| :--- | :--- |
| 功能/修复 | `CHANGELOG.md` 顶部新小节（再小也记） |
| 目录/业务流/构建 | `README.md` 对应章节 |
| 寄存器/协议/IO 映射 | `docs/通讯接入.md`（写明版本） |
| 操作入口/流程/按钮文字 | `docs/现场工程师培训手册.md` |
| 地图/清单/红线索引 | `docs/内部开发人员说明.md`（新约定沉淀进 `AGENTS.md`） |
| 被改功能名 | 全仓 rg 确认无残留引用（CHANGELOG 历史条目不回改） |
