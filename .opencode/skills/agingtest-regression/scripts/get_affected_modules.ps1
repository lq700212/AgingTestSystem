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
#         "NONE"          -> nothing verifiable changed (docs only, or
#                            test-source comments only)
#         "ModA,ModB,..." -> run exactly this subset
#         "TESTONLY:ModA,ModB,..." -> test sources only changed: run this
#                            subset and skip smoke (product exe untouched)
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
    @{ Pat = @("*CommunicationTestForm*", "*FanTestForm*", "*IoRemapVisualForm*"); Mods = @("ScannerParse", "ModbusConvert", "FanParse", "IoOutputChannelRemap", "SettingsValidate", "UiFinalizerV172_14", "DesignerStabilityV172_16") },
    @{ Pat = @("*ProcessPolicyForm*", "*PolicyGraph*"); Mods = @("ProcessPolicyV170", "PolicyV167", "PolicyNodeComboV1851", "UiFinalizerV172_14", "DesignerStabilityV172_16") },
    @{ Pat = @("*MainForm*");                       Mods = @("HomeLayoutConfig", "UiPureHelpers", "UiStyleV172_1", "ThemeManager", "SettingsValidate", "UiFinalizerV172_14", "DesignerStabilityV172_16", "MainFormIconV107") },
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
    @{ Pat = @("*RecipeStorage*"); Mods = @("RecipeStorage", "UiPureHelpers", "UiFinalizerV172_14") },
    @{ Pat = @("*StationSettingsCache*");           Mods = @("StationCache", "UiPureHelpers") },
    @{ Pat = @("*TestSessionStore*");               Mods = @("TestSessionStore", "DeviceManagerIntegration", "DeviceManagerPolicy") },
    @{ Pat = @("*TestEventLogger*");                Mods = @("TestEventLogger", "HistoryCsv", "DeviceManagerIntegration", "DeviceManagerExtended", "DeviceManagerPolicy", "DeviceManagerMes", "DeviceManagerRules", "PowerReportV174") },
    @{ Pat = @("*AppLogFileWriter*");               Mods = @("AppLogFileWriter") },
    @{ Pat = @("*BuildWatermark*", "*CrashLogWriter*"); Mods = @("DeployDiagV188_9") },
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
# 注意：TestRunner.cs / DeviceManagerIntegrationTests.cs 不在这里——它们先走
# 上面的 TESTONLY fast path（命中方法级子集），只有触及公共脚手架才回落 FULL。
$FullPatterns = @(
    ".opencode\skills\agingtest-regression\tests\ObfuscationAcceptance.cs",
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

# ── TESTONLY fast path: 纯用例改动只跑命中模块，不跑全量 ──
# 适用文件与 run_unit_tests.ps1 的 csc 编译清单一致（partial TestRunner 的两份源码；
# ObfuscationAcceptance.cs 是验收行为本身，改动仍走 $FullPatterns 兜底全量）。
$TestOnlyFiles = @("TestRunner.cs", "DeviceManagerIntegrationTests.cs")
$MaxTestOnlyModules = 3  # 命中超此数说明改的是共享逻辑，兜底全量

# 调用者闭包：改了非 Tests 结尾的 helper（如 SweepXxx/SelectNodeNoHang），沿调用链
# 往上找引用它的 XxxTests() 方法，落到哪个模块就测哪个模块；引用分散超限/找不到
#（如 Check/BuildTestManager/Main）返回 $null，调用方兜底 FULL。
function Find-CallerModules {
    param([array]$Methods, [hashtable]$Dict, [string]$Name)
    $seen = @{}
    $stack = @($Name)
    $mods = @{}
    while ($stack.Count -gt 0) {
        $cur = $stack[0]
        if ($stack.Count -gt 1) { $stack = $stack[1..($stack.Count - 1)] } else { $stack = @() }
        if ($seen.ContainsKey($cur)) { continue }
        $seen[$cur] = $true
        $pat = '\b' + [regex]::Escape($cur) + '\s*\('
        # 方法表只含顶层方法（嵌套成员建表时已滤掉），此处不再判层级。
        $callers = @($Methods | Where-Object {
            $_.Name -ne $cur -and $_.Body -match $pat
        })
        foreach ($c in $callers) {
            if ($c.IsTests) {
                if (-not $Dict.ContainsKey($c.Name)) { return $null }
                $mods[$Dict[$c.Name]] = $true
            }
            elseif (-not $seen.ContainsKey($c.Name)) { $stack += $c.Name }
        }
    }
    if ($mods.Count -eq 0) { return $null }
    return @($mods.Keys)
}

# 大括号深度扫描：判断方法是否嵌套在 Fake 类里（文本先后不可靠，必须数括号）。
# 逐行剥离注释/字符串/字符字面量（含跨行 @"..."）后再数括号；任何失衡
#（中途负数/收尾非零）即 Valid=$false，调用方兜底 FULL，绝不错判方向。
function Get-BraceDepths {
    param([array]$Content)
    $depths = New-Object int[] $Content.Count
    $depth = 0
    $valid = $true
    $inVerbatim = $false
    for ($i = 0; $i -lt $Content.Count; $i++) {
        $depths[$i] = $depth
        $line = "$($Content[$i])"
        $j = 0
        $n = $line.Length
        $inStr = $false
        $inChr = $false
        while ($j -lt $n) {
            $c = $line[$j]
            if ($inVerbatim) {
                if ($c -eq '"') {
                    if (($j + 1) -lt $n -and $line[$j + 1] -eq '"') { $j += 2; continue }
                    $inVerbatim = $false
                }
                $j++
                continue
            }
            if ($inStr) {
                if ($c -eq '\') { $j += 2; continue }
                if ($c -eq '"') { $inStr = $false }
                $j++
                continue
            }
            if ($inChr) {
                if ($c -eq '\') { $j += 2; continue }
                if ($c -eq "'") { $inChr = $false }
                $j++
                continue
            }
            if ($c -eq '/' -and ($j + 1) -lt $n -and $line[$j + 1] -eq '/') { break }
            if ($c -eq '@' -and ($j + 1) -lt $n -and $line[$j + 1] -eq '"') {
                $inVerbatim = $true; $j += 2; continue
            }
            if ($c -eq '@' -and ($j + 2) -lt $n -and $line[$j + 1] -eq '$' -and $line[$j + 2] -eq '"') {
                $inVerbatim = $true; $j += 3; continue
            }
            if ($c -eq '$' -and ($j + 1) -lt $n -and $line[$j + 1] -eq '"') {
                $inStr = $true; $j += 2; continue
            }
            if ($c -eq '$' -and ($j + 2) -lt $n -and $line[$j + 1] -eq '@' -and $line[$j + 2] -eq '"') {
                $inVerbatim = $true; $j += 3; continue
            }
            if ($c -eq '"') { $inStr = $true; $j++; continue }
            if ($c -eq "'") { $inChr = $true; $j++; continue }
            if ($c -eq '{') { $depth++ }
            elseif ($c -eq '}') { $depth--; if ($depth -lt 0) { $valid = $false } }
            $j++
        }
    }
    if ($depth -ne 0) { $valid = $false }
    return @{ Depths = $depths; Valid = $valid }
}

# 纯用例改动分析：返回 "TESTONLY:ModA,ModB" / "NONE"(纯注释) / "FULL"(任何不确定)。
# 判定链：注释-only→NONE；改动行落在某 XxxTests() 内→字典反查；
# 落在共享脚手架（Main/Check/Fake 类成员/类级字段/using）→FULL；
# 落在普通 helper→调用者闭包；新方法未进字典/命中超限→FULL。
function Get-TestOnlyResult {
    param([string]$RepoRoot, [string[]]$Files)
    $rel = @($Files | ForEach-Object { ($_ -replace "/", "\") })
    foreach ($f in $rel) {
        $leaf = Split-Path $f -Leaf
        if (($TestOnlyFiles -notcontains $leaf) -or ($f -notlike "*tests*")) { return "FULL" }
        if (-not (Test-Path -LiteralPath (Join-Path $RepoRoot $f))) { return "FULL" }
    }
    $changes = @()
    $codeTouched = $false
    $methods = @()
    $dict = @{}
    $lineCounts = @{}
    $bdAll = @{}
    $topAll = @{}
    $allMarkers = @()
    foreach ($f in $rel) {
        $full = Join-Path $RepoRoot $f
        $fwd = $f -replace "\\", "/"
        $dt = @(git -C $RepoRoot diff -U0 HEAD -- $fwd)
        if ($LASTEXITCODE -ne 0) { return "FULL" }
        # 血泪：PS5.1 的 Get-Content 缺省按 ANSI 解码，无 BOM 的 UTF-8 中文源文件会被吞换行
        #（实测 DeviceManagerIntegrationTests.cs 少 154 行），行号与 git hunk 对不上即错判；
        # 此处必须显式 UTF8（本仓铁律：源码一律 UTF-8），且下面有越界护栏双保险。
        $content = @(Get-Content -LiteralPath $full -Encoding UTF8)
        $lineCounts[$f] = $content.Count
        $added = @($dt | Where-Object { $_ -match '^\+' -and $_ -notmatch '^\+\+\+' } |
            ForEach-Object { $_.Substring(1).Trim() })
        $removed = @($dt | Where-Object { $_ -match '^-' -and $_ -notmatch '^---' } |
            ForEach-Object { $_.Substring(1).Trim() })
        $nonComment = @((@($added) + @($removed)) |
            Where-Object { $_ -ne "" -and $_ -notmatch '^(//|/\*|\*|\*/)' })
        if ($nonComment.Count -gt 0) { $codeTouched = $true }
        foreach ($ln in $dt) {
            if ($ln -match '^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@') {
                $ns = [int]$Matches[1]
                $nc = 1
                if ($Matches.Count -ge 3 -and $Matches[2] -ne $null -and "$($Matches[2])" -ne "") {
                    $nc = [int]$Matches[2]
                }
                if ($nc -le 0) { $changes += @{ File = $f; Line = $ns } }
                else { for ($i = 0; $i -lt $nc; $i++) { $changes += @{ File = $f; Line = ($ns + $i) } } }
            }
        }
        # 作用域 markers：只收三类——类型声明（任何深度）、顶层方法声明
        #（深度必须等于类体深，方法体内的局部函数/静态 lambda 一律无视，
        # 否则方法体被切碎，调用者闭包就看不见跨行调用）、换行首的 `[` 与
        # 无括号 static 行（类级字段/属性/委托，方法体内不可能出现这种行）。
        # 方法体内的局部赋值（如 string dir = EnterCleanDir();）故意不收：
        # 它与 Fake 类字段文本同形，收了就会切碎方法体；类级字段由下面的
        # “类体层非声明行”规则兜住，Fake 体由“嵌套类型”规则兜住。
        # 深度先行：括号扫描必须在 marker 循环之前（声明行深度是顶层判定依据）。
        $bd = Get-BraceDepths -Content $content
        if (-not $bd.Valid) { return "FULL" }
        $markers = @()
        $firstTypeLine = -1
        for ($i = 0; $i -lt $content.Count; $i++) {
            $t = ("$($content[$i])").Trim()
            if ($t -eq "" -or $t.StartsWith("//")) { continue }
            if ($t -match '^(private|public|internal|protected)?\s*(static\s+|sealed\s+|abstract\s+|partial\s+)*(class|struct|enum|interface|record)\s+(\w+)') {
                $markers += @{ Line = ($i + 1); Kind = "type"; Name = $Matches[4] }
                if ($firstTypeLine -lt 0) { $firstTypeLine = $i + 1 }
                continue
            }
            if ($t.StartsWith("[")) {
                $markers += @{ Line = ($i + 1); Kind = "shared"; Name = "<attr>" }
                continue
            }
            if ($t -match '\bdelegate\b') {
                $markers += @{ Line = ($i + 1); Kind = "shared"; Name = "<delegate>" }
                continue
            }
            $pi = $t.IndexOf("(")
            if ($pi -lt 0) {
                if ($t -match '^(private|public|internal|protected)?\s*static\s+') {
                    $markers += @{ Line = ($i + 1); Kind = "shared"; Name = "<field>" }
                }
                continue
            }
            if ($t -match '^(private|public|internal|protected)?\s*static\s+[\w\.\<\>\[\],\s\?]+\s+(\w+)\s*\(') {
                $nm = $Matches[2]
                if ($nm -notmatch '^(if|foreach|for|while|switch|using|lock|catch|return|new|where)$') {
                    $markers += @{ Line = ($i + 1); Kind = "method"; Name = $nm; Depth = $bd.Depths[$i] }
                }
            }
        }
        if ($firstTypeLine -lt 0) { return "FULL" }
        $topBodyDepth = $bd.Depths[$firstTypeLine - 1] + 1
        $bdAll[$f] = $bd.Depths
        $topAll[$f] = $topBodyDepth
        # 顶层方法 = 声明行深度恰为类体深；其余 static 方法（Fake 体/局部函数）不进表，
        # 既不归属也不切分方法体。类型 marker 另存一份，供归属时判“嵌套类型体内”。
        foreach ($mk in $markers) {
            if ($mk.Kind -eq "method" -and $mk.Depth -ne $topBodyDepth) { $mk.Kind = "ignored" }
            if ($mk.Kind -eq "type") { $allMarkers += @{ File = $f; Kind = "type"; Line = $mk.Line } }
        }
        # 方法表只收顶层方法：体区间 = 声明行..下一 marker 上一行。
        # 方法体内不可能出现其它 marker（类型不能声明在方法里；`[` 开头/
        # 无括号 static 行不可能是方法体内的语句），体区间天然完整，闭包可见全部调用。
        for ($mi = 0; $mi -lt $markers.Count; $mi++) {
            $mk = $markers[$mi]
            if ($mk.Kind -ne "method") { continue }
            $endLine = $content.Count
            if (($mi + 1) -lt $markers.Count) { $endLine = $markers[$mi + 1].Line - 1 }
            $body = ($content[(($mk.Line - 1))..($endLine - 1)] -join "`n")
            $methods += @{ File = $f; Line = $mk.Line; Name = $mk.Name;
                IsTests = ($mk.Name -like "*Tests"); Body = $body }
        }
        # allModules 字典只在本文件出现时解析（方法名→模块名反查表）。
        $dictStart = -1
        for ($i = 0; $i -lt $content.Count; $i++) {
            if ($content[$i] -match 'allModules\s*=\s*new') { $dictStart = $i; break }
        }
        if ($dictStart -ge 0) {
            for ($i = $dictStart; $i -lt $content.Count; $i++) {
                if ($content[$i] -match '^\s*\};') { break }
                if ($content[$i] -match '\{\s*"([^"]+)"\s*,\s*(\w+)\s*\}') {
                    $dict[$Matches[2]] = $Matches[1]
                }
            }
        }
    }
    if ($changes.Count -eq 0) { return "FULL" }
    # 越界护栏：任一改动行号超出读到的文件行数，说明读文件口径与 git 不一致，直接 FULL。
    foreach ($ch in $changes) {
        $flen = 0
        if ($lineCounts.ContainsKey($ch.File)) { $flen = $lineCounts[$ch.File] }
        if ($flen -le 0 -or $ch.Line -gt $flen -or $ch.Line -lt 1) { return "FULL" }
    }
    if (-not $codeTouched) { return "NONE" }
    $hit = @{}
    foreach ($ch in $changes) {
        # 类体层（深度恰为类体深）的行：只有方法声明行可归属，
        # 其余（字段/属性/多行延续/孤立大括号）一律 FULL。
        if ($bdAll[$ch.File][$ch.Line - 1] -eq $topAll[$ch.File]) {
            $decl = $null
            foreach ($m in $methods) {
                if ($m.File -eq $ch.File -and $m.Line -eq $ch.Line) { $decl = $m; break }
            }
            if ($decl -eq $null) { return "FULL" }
            if ($decl.IsTests) {
                if (-not $dict.ContainsKey($decl.Name)) { return "FULL" }
                $hit[$dict[$decl.Name]] = $true
            }
            else {
                $cm = Find-CallerModules -Methods $methods -Dict $dict -Name $decl.Name
                if ($cm -eq $null) { return "FULL" }
                foreach ($mm in $cm) { $hit[$mm] = $true }
            }
            continue
        }
        # 深层行：最近的方法 marker 与最近的类型 marker 比较——类型在后
        # 说明落在嵌套类型（Fake 类）体内，一律 FULL；否则归属该方法。
        $prevM = $null
        $prevT = 0
        foreach ($m in $methods) {
            if ($m.File -eq $ch.File -and $m.Line -le $ch.Line) {
                if ($prevM -eq $null -or $m.Line -gt $prevM.Line) { $prevM = $m }
            }
        }
        foreach ($mk in $allMarkers) {
            if ($mk.File -eq $ch.File -and $mk.Kind -eq "type" -and $mk.Line -le $ch.Line) {
                if ($mk.Line -gt $prevT) { $prevT = $mk.Line }
            }
        }
        if ($prevM -eq $null) { return "FULL" }
        if ($prevT -gt $prevM.Line) { return "FULL" }
        if ($prevM.IsTests) {
            if (-not $dict.ContainsKey($prevM.Name)) { return "FULL" }
            $hit[$dict[$prevM.Name]] = $true
        }
        else {
            $cm = Find-CallerModules -Methods $methods -Dict $dict -Name $prevM.Name
            if ($cm -eq $null) { return "FULL" }
            foreach ($mm in $cm) { $hit[$mm] = $true }
        }
    }
    if ($hit.Count -eq 0 -or $hit.Count -gt $MaxTestOnlyModules) { return "FULL" }
    return ("TESTONLY:" + (($hit.Keys | Sort-Object) -join ","))
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
# TESTONLY fast path 先行：只碰用例源码时不进下面的产品映射表。
$nonIgnored = @($changed | Where-Object { -not (Test-AnyLike $_ $IgnorePatterns) })
$onlyTests = ($nonIgnored.Count -gt 0)
foreach ($lf in $nonIgnored) {
    $lfLeaf = Split-Path $lf -Leaf
    if (($TestOnlyFiles -notcontains $lfLeaf) -or ($lf -notlike "*tests*")) { $onlyTests = $false; break }
}
if ($onlyTests) {
    $to = Get-TestOnlyResult -RepoRoot $RepoRoot -Files $nonIgnored
    if ($to -eq "NONE") {
        Write-Host "[AFFECTED] 用例注释类改动，无可验证模块。"
        return "NONE"
    }
    if ("$to" -like "TESTONLY:*") {
        Write-Host "[AFFECTED] 纯用例改动 -> 模块子集 $(("$to").Substring(9))（产品未动，冒烟可跳）。"
        return $to
    }
    Write-Host "[AFFECTED] 用例改动触及公共脚手架，兜底全量。"
    return "FULL"
}
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
