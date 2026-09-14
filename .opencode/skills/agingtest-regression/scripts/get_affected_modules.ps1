# ============================================================================
#  get_affected_modules.ps1 - Impact analysis: changed files -> test modules.
#
#  What it does:
#    1. Collect changed files (working tree vs HEAD by default, or vs -Ref,
#       or an explicit -Files list for testing).
#    2. Map each file to regression modules via $Map below (conservative:
#       a file maps to its directly-covering modules PLUS modules that
#       assert the interaction, e.g. TestEventLogger format -> all
#       DeviceManager* suites that parse CSV).
#    3. Return one pipeline string for callers (build_and_test.ps1):
#         "FULL"          -> run the whole suite (fail-safe fallback)
#         "NONE"          -> nothing verifiable changed (docs only)
#         "ModA,ModB,..." -> run exactly this subset
#
#  Fail-safe rule: any file the map does not recognize (new product file,
#  csproj, Interfaces, skill tests/scripts) forces FULL, never silent skip.
#  >>> If you add a new product .cs file, register it in $Map below, <<<
#  >>> otherwise every future change to it pays a full-suite run.     <<<
#
#  Usage:
#    .\get_affected_modules.ps1                                # working tree
#    .\get_affected_modules.ps1 -Ref main                      # branch diff
#    .\get_affected_modules.ps1 -Files @("AgingTestSystem\Dialogs\CommonParameterForm.cs")
#       （-Files 数组只在 & 调用/交互式里是真数组；powershell -File 调时数组字面量
#       会被 CLI 拆成碎片，此时用单串分号式，见下方 -Files 分支的碎片守卫。）
#    powershell -File get_affected_modules.ps1 -Files 'a.cs;b.cs'   # CLI 手动点测
# ============================================================================

param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..\")).Path,
    [string]$Ref = "",
    [string[]]$Files = @()
)

$ErrorActionPreference = "Stop"

# ── 1. Product file -> modules map (match on leaf file name, ordered) ──
# Each entry: wildcard(s) => module list. Keep it conservative: when in doubt,
# add the interacting module rather than leaving it out (a subset that is just
# a little bigger is still far cheaper than FULL).
$Map = @(
    # --- Dialogs / Views / Controls (UI) ---
    @{ Pat = @("*CommonParameterForm*");            Mods = @("UiStyleV172_1", "UiFinalizerV172_14") },
    @{ Pat = @("*HistoryRecordForm*");              Mods = @("HistoryCsv", "UiStyleV172_1", "PowerReportV174") },
    @{ Pat = @("*SettingsForm*");                   Mods = @("SettingsForm.Normalize", "SettingsValidate", "PolicyV167", "MesV168", "RuleExprV169", "ScannerParse", "DeviceConfig.ParseFanIpCandidates", "IoOutputChannelRemap", "UiFinalizerV172_14", "PowerReportV174") },
    @{ Pat = @("*BatchRecipeForm*", "*StationSettingsForm*", "*RecipeManagerForm*"); Mods = @("UiPureHelpers", "LegacyRecipeGuard", "RecipeStorage", "UiFinalizerV172_14", "DesignerStabilityV172_16", "PowerReportV174") },
    @{ Pat = @("*IdBindingForm*", "*InputLotForm*"); Mods = @("UiStyleV172_1", "UiPureHelpers", "UiFinalizerV172_14") },
    @{ Pat = @("*ChangePasswordForm*", "*LoginForm*", "*UserManagementForm*"); Mods = @("UserManager") },
    @{ Pat = @("*UnloadJudgeForm*");                Mods = @("PolicyV167", "DeviceManagerPolicy", "UiFinalizerV172_14", "DesignerStabilityV172_16") },
    @{ Pat = @("*ProjectSwitchForm*");              Mods = @("PolicyV167") },
    @{ Pat = @("*HomeLayoutEditorForm*");           Mods = @("HomeLayoutConfig", "DesignerStabilityV172_16") },
    @{ Pat = @("*CommunicationTestForm*", "*FanTestForm*", "*IoRemapVisualForm*"); Mods = @("ScannerParse", "ModbusConvert", "FanParse", "IoOutputChannelRemap", "SettingsValidate", "UiFinalizerV172_14", "DesignerStabilityV172_16") },
    @{ Pat = @("*ProcessPolicyForm*", "*PolicyGraph*"); Mods = @("ProcessPolicyV170", "PolicyV167", "PolicyNodeComboV1851", "UiFinalizerV172_14", "DesignerStabilityV172_16") },
    @{ Pat = @("*MainForm*");                       Mods = @("UiPureHelpers", "UiStyleV172_1", "ThemeManager", "SettingsValidate", "UiFinalizerV172_14", "DesignerStabilityV172_16") },
    @{ Pat = @("*WorkstationGridView*");            Mods = @("PanelLayoutConfig", "UiPureHelpers") },
    @{ Pat = @("*RuleListEditorPopup*");            Mods = @("RuleExprV169", "SettingsValidate", "PowerReportV174") },
    @{ Pat = @("*IoMappingEditorPopup*", "*IpListEditorPopup*", "*ReportColumnsEditorPopup*", "*DisplayModesEditorPopup*", "*DataGridViewNumericUpDownCell*", "*IoRemapGraphControl*"); Mods = @("SettingsValidate", "IoOutputChannelRemap", "PowerReportV174", "UiFinalizerV172_14") },
    # --- Services (logic) ---
    @{ Pat = @("*PasswordHasher*");                 Mods = @("PasswordHasher") },
    @{ Pat = @("*UserManager*");                    Mods = @("UserManager") },
    @{ Pat = @("*DeviceManager*");                  Mods = @("DeviceManagerIntegration", "DeviceManagerExtended", "DeviceManagerPolicy", "DeviceManagerMes", "DeviceManagerRules", "DeviceManagerSweep", "AgingBusinessModel", "TestSessionStore", "PowerReportV174") },
    @{ Pat = @("*AgingSequencer*");                 Mods = @("AgingSequencer", "DeviceManagerIntegration", "DeviceManagerExtended", "DeviceManagerPolicy") },
    @{ Pat = @("*ProjectPolicyStore*", "*PolicyEnums*"); Mods = @("PolicyV167", "SettingsValidate", "DeviceManagerPolicy", "ProcessPolicyV170", "PowerReportV174") },
    @{ Pat = @("*PolicyPresets*");                 Mods = @("PolicyPresetV185", "PolicyV167", "ProcessPolicyV170") },
    @{ Pat = @("*ProjectProfile*");                 Mods = @("PolicyV167") },
    @{ Pat = @("*MesMapping*", "*MesCrypto*");      Mods = @("MesV168") },
    @{ Pat = @("*MesReporter*");                    Mods = @("MesV168", "DeviceManagerMes") },
    @{ Pat = @("*RuleExpr*", "*RuleEngine*");       Mods = @("RuleExprV169", "DeviceManagerRules", "SettingsValidate", "PowerReportV174") },
    @{ Pat = @("*ScannerService*");                 Mods = @("ScannerParse", "SettingsValidate") },
    @{ Pat = @("*SerialPortHelper*");               Mods = @("ScannerParse", "ModbusConvert", "FanParse") },
    @{ Pat = @("*ModbusRtuBarometerReader*");       Mods = @("ModbusConvert") },
    @{ Pat = @("*FanControllerClient*");            Mods = @("FanParse") },
    @{ Pat = @("*ModbusTcpIoController*");          Mods = @("IoMapBuilder", "MockDevices", "IoOutputChannelRemap") },
    @{ Pat = @("*IoRemapValidator*", "*IoRemapCatalog*"); Mods = @("IoOutputChannelRemap", "SettingsValidate") },
    @{ Pat = @("*IoMapBuilder*", "*IoPointDefinition*", "*IoStatus*"); Mods = @("IoMapBuilder", "IoOutputChannelRemap", "MockDevices") },
    @{ Pat = @("*MockBarometerReader*", "*MockFanController*", "*MockIoController*", "*MockPowerMeter*"); Mods = @("MockDevices", "DeviceManagerIntegration", "DeviceManagerExtended", "PowerReportV174") },
    @{ Pat = @("*PowerMeterClient*", "*ReportColumns*", "*DisplayModeOptions*"); Mods = @("PowerReportV174", "SettingsValidate") },
    @{ Pat = @("*RecipeStorage*", "*RecipeAutoCompleteProvider*"); Mods = @("RecipeStorage", "UiPureHelpers", "UiFinalizerV172_14") },
    @{ Pat = @("*StationSettingsCache*");           Mods = @("StationCache", "UiPureHelpers") },
    @{ Pat = @("*TestSessionStore*");               Mods = @("TestSessionStore", "DeviceManagerIntegration", "DeviceManagerPolicy") },
    @{ Pat = @("*TestEventLogger*");                Mods = @("TestEventLogger", "HistoryCsv", "DeviceManagerIntegration", "DeviceManagerExtended", "DeviceManagerPolicy", "DeviceManagerMes", "DeviceManagerRules", "PowerReportV174") },
    @{ Pat = @("*AppLogFileWriter*");               Mods = @("AppLogFileWriter") },
    @{ Pat = @("*ThemeManager*");                   Mods = @("ThemeManager", "UiStyleV172_1") },
    @{ Pat = @("*ControlDisposeHelper*");           Mods = @("DesignerStabilityV172_16") },
    @{ Pat = @("*EyeIcon*", "*SoftwareActivation*", "*SoftActivation*"); Mods = @("SoftActivation") },
    @{ Pat = @("*AtomicFile*");                    Mods = @("TestSessionStore", "RecipeStorage", "StationCache", "MesV168") },
    # --- Models ---
    @{ Pat = @("*DeviceConfig*");                   Mods = @("ModelDefaults", "SettingsValidate", "PolicyV167", "MesV168", "ProcessPolicyV170", "DeviceConfig.ParseFanIpCandidates", "DeviceManagerIntegration", "PowerReportV174") },
    @{ Pat = @("*IoOutputChannelRemap*");           Mods = @("IoOutputChannelRemap", "SettingsValidate") },
    @{ Pat = @("*BarometerData*", "*FanData*");     Mods = @("AgingBusinessModel", "ModelRoundtrip", "MockDevices", "PowerReportV174") },
    @{ Pat = @("*RecipeConfig*");                   Mods = @("RecipeStorage", "ModelRoundtrip", "LegacyRecipeGuard", "UiPureHelpers", "DeviceManagerExtended") },
    @{ Pat = @("*StationInfo*");                    Mods = @("AgingBusinessModel", "ModelRoundtrip", "StationCache", "DeviceManagerExtended") },
    @{ Pat = @("*PanelLayoutConfig*");              Mods = @("PanelLayoutConfig", "UiPureHelpers") },
    @{ Pat = @("*HomeLayoutConfig*");               Mods = @("HomeLayoutConfig", "DesignerStabilityV172_16") },
    @{ Pat = @("*TestSession*");                    Mods = @("TestSessionStore", "DeviceManagerIntegration", "DeviceManagerPolicy") },
    @{ Pat = @("*UserAccount*", "*UserRole*");      Mods = @("UserManager", "ModelRoundtrip") }
    # 【V1.87.1】本行禁尾逗号：PS5.1 里 @() 最后一个哈希表后跟逗号即整本解析失败
    #（MissingExpression，v_corner 实测：有尾逗号红、无尾逗号绿），-Affected 全残。
    # --- Entry point: fail-safe FULL (no map entry on purpose) ---
    # 【V1.87】Program 已无授权逻辑（启动闸随 RSA 方案删除），不再映射任何模块：
    # 改 Program.cs 会走"映射表无登记→兜底全量"（安全；入口改动极少，可接受）。
)

# Files that force FULL no matter what (harness self-change must prove no
# collateral; project/build graph change may affect everything).
$FullPatterns = @(
    ".opencode\skills\agingtest-regression\tests\*",
    ".opencode\skills\agingtest-regression\scripts\*",
    "*Interfaces\*",
    "*.csproj",
    "*.sln"
)

# Files that need no verification at all (docs, notes, git metadata).
$IgnorePatterns = @("*.md", "docs\*", "*.txt", ".gitignore", "*.gitattributes")

# App.config defaults feed ModelDefaults/SettingsValidate/DeviceConfig locks.
$ConfigPatterns = @("*\App.config", "App.config")

function Test-AnyLike([string]$Path, [string[]]$Patterns) {
    foreach ($p in $Patterns) { if ($Path -like $p) { return $true } }
    return $false
}

# ── 2. Collect changed files ──
[string[]]$changed = @()
if ($Files.Count -gt 0) {
    # 【V1.87.1】-Files 两种传法：& 调用传真数组（元素原样用）；
    # powershell -File 调用传单串（分号/逗号分隔，这里拆开；正常路径不含这两符）。
    # 注意 -File 下 -Files @('a','b') 只认首个、其余静默丢弃（CLI 绑定器行为，
    # showparam 探针实测 Count=1，脚本侧无从察觉）——多文件点测用 & 调用或单串分号式。
    $flat = @()
    $sq = "'"
    $dq = '"'
    foreach ($x in $Files) {
        $t = ("$x").Trim()
        if ($t.StartsWith("@(") -and $t.EndsWith(")")) { $t = $t.Substring(2, $t.Length - 3) }
        foreach ($part in ($t -split '[;,]')) {
            $p = $part.Trim().Trim($sq).Trim($dq)
            if ($p -ne "") { $flat += $p }
        }
    }
    $changed = $flat
}
else {
    Push-Location $RepoRoot
    try {
        if ($Ref -ne "") { $diff = git diff --name-only "$Ref" }
        else             { $diff = git diff --name-only HEAD }
        $untracked = git ls-files --others --exclude-standard
        $changed = @($diff) + @($untracked) | Where-Object { $_ -ne $null -and $_ -ne "" }
    }
    finally { Pop-Location }
}
$changed = @($changed | ForEach-Object { "$_".Replace("/", "\") } | Sort-Object -Unique)

if ($changed.Count -eq 0) {
    Write-Host "[AFFECTED] 工作区干净，无改动，无需验证。"
    return "NONE"
}

# ── 3. Classify ──
$mods = New-Object System.Collections.Generic.HashSet[string]
$fullReasons = @()
$ignored = @()
foreach ($f in $changed) {
    $leaf = Split-Path $f -Leaf
    if (Test-AnyLike $f $IgnorePatterns) { $ignored += $f; continue }
    if (Test-AnyLike $f $FullPatterns) { $fullReasons += ($f + "（骨架/用例/接口改动，兜底全量）"); continue }
    if (Test-AnyLike $f $ConfigPatterns) {
        foreach ($m in @("ModelDefaults", "SettingsValidate", "DeviceConfig.ParseFanIpCandidates")) { [void]$mods.Add($m) }
        continue
    }
    $hit = $false
    foreach ($entry in $Map) {
        if (Test-AnyLike $leaf $entry.Pat) {
            foreach ($m in $entry.Mods) { [void]$mods.Add($m) }
            $hit = $true
            break
        }
    }
    if (-not $hit) {
        if ($leaf -like "*.cs" -or $leaf -like "*.resx" -or $leaf -like "*.config") {
            $fullReasons += ($f + "（映射表无登记，兜底全量；请在 get_affected_modules.ps1 的 Map 登记）")
        }
        else { $ignored += $f }
    }
}

if ($fullReasons.Count -gt 0) {
    Write-Host "[AFFECTED] 命中全量兜底："
    foreach ($r in $fullReasons) { Write-Host "  FULL <= $r" }
    return "FULL"
}
if ($mods.Count -eq 0) {
    Write-Host "[AFFECTED] 改动均为文档/注释类（$($ignored -join '、')），无可验证模块。"
    return "NONE"
}
$result = ($mods | Sort-Object) -join ","
Write-Host "[AFFECTED] 改动 $($changed.Count) 个文件 -> 模块子集 $($mods.Count) 个：$result"
return $result
