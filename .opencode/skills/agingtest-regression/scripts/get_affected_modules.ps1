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
    @{ Pat = @("*HistoryRecordForm*");              Mods = @("HistoryCsv", "UiStyleV172_1") },
    @{ Pat = @("*SettingsForm*");                   Mods = @("SettingsForm.Normalize", "SettingsValidate", "PolicyV167", "MesV168", "RuleExprV169", "ScannerParse", "DeviceConfig.ParseFanIpCandidates", "IoOutputChannelRemap", "UiFinalizerV172_14") },
    @{ Pat = @("*BatchRecipeForm*", "*StationSettingsForm*", "*RecipeManagerForm*"); Mods = @("UiPureHelpers", "LegacyRecipeGuard", "RecipeStorage", "UiFinalizerV172_14", "DesignerStabilityV172_16") },
    @{ Pat = @("*IdBindingForm*", "*InputLotForm*"); Mods = @("UiStyleV172_1", "UiPureHelpers", "UiFinalizerV172_14") },
    @{ Pat = @("*ChangePasswordForm*", "*LoginForm*", "*UserManagementForm*"); Mods = @("UserManager") },
    @{ Pat = @("*UnloadJudgeForm*");                Mods = @("PolicyV167", "DeviceManagerPolicy", "UiFinalizerV172_14", "DesignerStabilityV172_16") },
    @{ Pat = @("*ProjectSwitchForm*");              Mods = @("PolicyV167") },
    @{ Pat = @("*HomeLayoutEditorForm*");           Mods = @("HomeLayoutConfig", "DesignerStabilityV172_16") },
    @{ Pat = @("*CommunicationTestForm*", "*FanTestForm*"); Mods = @("ScannerParse", "ModbusConvert", "FanParse", "UiFinalizerV172_14") },
    @{ Pat = @("*ProcessPolicyForm*", "*PolicyGraph*"); Mods = @("ProcessPolicyV170", "PolicyV167", "UiFinalizerV172_14", "DesignerStabilityV172_16") },
    @{ Pat = @("*MainForm*");                       Mods = @("UiPureHelpers", "UiStyleV172_1", "ThemeManager", "SettingsValidate", "UiFinalizerV172_14", "DesignerStabilityV172_16") },
    @{ Pat = @("*WorkstationGridView*");            Mods = @("PanelLayoutConfig", "UiPureHelpers") },
    @{ Pat = @("*RuleListEditorPopup*");            Mods = @("RuleExprV169", "SettingsValidate") },
    @{ Pat = @("*IoMappingEditorPopup*", "*IpListEditorPopup*", "*DataGridViewNumericUpDownCell*"); Mods = @("SettingsValidate", "IoOutputChannelRemap") },
    # --- Services (logic) ---
    @{ Pat = @("*PasswordHasher*");                 Mods = @("PasswordHasher") },
    @{ Pat = @("*UserManager*");                    Mods = @("UserManager") },
    @{ Pat = @("*DeviceManager*");                  Mods = @("DeviceManagerIntegration", "DeviceManagerExtended", "DeviceManagerPolicy", "DeviceManagerMes", "DeviceManagerRules", "AgingBusinessModel", "TestSessionStore") },
    @{ Pat = @("*AgingSequencer*");                 Mods = @("AgingSequencer", "DeviceManagerIntegration", "DeviceManagerExtended", "DeviceManagerPolicy") },
    @{ Pat = @("*ProjectPolicyStore*", "*PolicyEnums*"); Mods = @("PolicyV167", "SettingsValidate", "DeviceManagerPolicy", "ProcessPolicyV170") },
    @{ Pat = @("*ProjectProfile*");                 Mods = @("PolicyV167") },
    @{ Pat = @("*MesMapping*", "*MesCrypto*");      Mods = @("MesV168") },
    @{ Pat = @("*MesReporter*");                    Mods = @("MesV168", "DeviceManagerMes") },
    @{ Pat = @("*RuleExpr*", "*RuleEngine*");       Mods = @("RuleExprV169", "DeviceManagerRules", "SettingsValidate") },
    @{ Pat = @("*ScannerService*");                 Mods = @("ScannerParse", "SettingsValidate") },
    @{ Pat = @("*SerialPortHelper*");               Mods = @("ScannerParse", "ModbusConvert", "FanParse") },
    @{ Pat = @("*ModbusRtuBarometerReader*");       Mods = @("ModbusConvert") },
    @{ Pat = @("*FanControllerClient*");            Mods = @("FanParse") },
    @{ Pat = @("*ModbusTcpIoController*");          Mods = @("IoMapBuilder", "MockDevices") },
    @{ Pat = @("*IoMapBuilder*", "*IoPointDefinition*", "*IoStatus*"); Mods = @("IoMapBuilder", "IoOutputChannelRemap", "MockDevices") },
    @{ Pat = @("*MockBarometerReader*", "*MockFanController*", "*MockIoController*"); Mods = @("MockDevices", "DeviceManagerIntegration", "DeviceManagerExtended") },
    @{ Pat = @("*RecipeStorage*", "*RecipeAutoCompleteProvider*"); Mods = @("RecipeStorage", "UiPureHelpers", "UiFinalizerV172_14") },
    @{ Pat = @("*StationSettingsCache*");           Mods = @("StationCache", "UiPureHelpers") },
    @{ Pat = @("*TestSessionStore*");               Mods = @("TestSessionStore", "DeviceManagerIntegration", "DeviceManagerPolicy") },
    @{ Pat = @("*TestEventLogger*");                Mods = @("TestEventLogger", "HistoryCsv", "DeviceManagerIntegration", "DeviceManagerExtended", "DeviceManagerPolicy", "DeviceManagerMes", "DeviceManagerRules") },
    @{ Pat = @("*AppLogFileWriter*");               Mods = @("AppLogFileWriter") },
    @{ Pat = @("*ThemeManager*");                   Mods = @("ThemeManager", "UiStyleV172_1") },
    @{ Pat = @("*ControlDisposeHelper*");           Mods = @("DesignerStabilityV172_16") },
    # --- Models ---
    @{ Pat = @("*DeviceConfig*");                   Mods = @("ModelDefaults", "SettingsValidate", "PolicyV167", "MesV168", "ProcessPolicyV170", "DeviceConfig.ParseFanIpCandidates", "DeviceManagerIntegration") },
    @{ Pat = @("*IoOutputChannelRemap*");           Mods = @("IoOutputChannelRemap", "SettingsValidate") },
    @{ Pat = @("*BarometerData*", "*FanData*");     Mods = @("AgingBusinessModel", "ModelRoundtrip", "MockDevices") },
    @{ Pat = @("*RecipeConfig*");                   Mods = @("RecipeStorage", "ModelRoundtrip", "LegacyRecipeGuard", "UiPureHelpers", "DeviceManagerExtended") },
    @{ Pat = @("*StationInfo*");                    Mods = @("AgingBusinessModel", "ModelRoundtrip", "StationCache", "DeviceManagerExtended") },
    @{ Pat = @("*PanelLayoutConfig*");              Mods = @("PanelLayoutConfig", "UiPureHelpers") },
    @{ Pat = @("*HomeLayoutConfig*");               Mods = @("HomeLayoutConfig", "DesignerStabilityV172_16") },
    @{ Pat = @("*TestSession*");                    Mods = @("TestSessionStore", "DeviceManagerIntegration", "DeviceManagerPolicy") },
    @{ Pat = @("*UserAccount*", "*UserRole*");      Mods = @("UserManager", "ModelRoundtrip") },
    # --- Entry point: covered by build + smoke stages, no regression modules ---
    @{ Pat = @("*Program*");                        Mods = @() }
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
    $changed = $Files
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
