# ============================================================================
#  audit_finalizer_risk.ps1 - 终结器跨线程崩溃风险审计（V1.72.13 固化）。
#
#  背景：Sunny 输入控件（UITextBox/UIComboBox，内部包原生 TextBox）与原生
#  TextBox/ComboBox/ListBox，若"显示过句柄 + 没显式 Dispose + 无根引用"，
#  GC 时走终结器线程 Dispose，内部读 Handle 即抛 InvalidOperationException
#  （堆栈终点 ResetAutoComplete←Dispose←Finalize，控件 Name 全空、案发时机
#  看 GC 三特征）。案发现场两次：驾驶舱右栏 Clear（V1.72.12）、设置窗三
#  popup（V1.72.13），案发时用户正在干什么全是巧合。
#
#  用法：powershell -ExecutionPolicy Bypass -File audit_finalizer_risk.ps1
#  改 UI 代码（新增窗体/动态控件/非模态弹窗/Clear/Remove）后必跑；
#  HIGH > 0 即 exit 1（先修再提交），只有 SAFE/INFO 即 exit 0。
#
#  规则（宁误报不漏报，误报进白名单）：
#    R1 Controls.Clear() 前 15 行无 Dispose()                      → HIGH
#    R2 非模态 .Show(（排除 ShowDialog），无释放关联              → HIGH
#    R3 Controls.Remove( 后 10 行无 Dispose()                      → HIGH
#    R4 非 Designer.cs 里 new 输入类控件                          → INFO（人工确认随树/释放）
# ============================================================================

param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")).Path
)

$ErrorActionPreference = "Stop"
$srcDir = Join-Path $RepoRoot "AgingTestSystem"
$high = @()
$info = @()

# 白名单：已知安全的固定模式（加新模式先修代码，确属误报才加这里，并注明原因）。
# 格式 "方法|文件[|释放配对方法|reuse:字段]"：
#   无第三段   → 该方法体 150 行内必须含 .Dispose()（行为检查，注掉即报警）；
#   方法名     → 配对方法体含 .Dispose()（Show/释放分两个方法）；
#   reuse:字段 → 字段复用单例（有根不进终结），文件须含该字段声明。
$SafeClearFiles = @(
    "Views\MainForm.cs",                        # H5：逐个 Dispose 后 Clear
    "Views\FlowCockpitForm.cs"                  # V1.72.12：DisposeEditorControls
)
$SafeShowKeys = @(
    "ShowDropdownPopup|MainForm.cs",             # V1.72.13：FormClosed 里 Dispose
    "ShowIpListPopup|SettingsForm.cs",           # V1.72.13：finally Dispose
    "ShowIoMappingPopup|SettingsForm.cs",        # V1.72.13：finally Dispose
    "ShowRuleListPopup|SettingsForm.cs",         # V1.72.13：finally Dispose
    "MenuHelpCommunicationTest_Click|MainForm.cs", # FormClosed→Dispose
    "MenuHelpFanTest_Click|MainForm.cs",           # FormClosed→Dispose
    "ShowConnecting|MainForm.cs|HideConnecting",   # Close+Dispose 配对在另一方法
    "ShowRemapNotice|CommunicationTestForm.cs|reuse:_remapNoticeForm" # 字段复用单例
)
$SafeRemoveFiles = @(
    "Services\RecipeAutoCompleteProvider.cs"     # Remove 后同块 Dispose
)

function Get-RelPath([string]$full) {
    return $full.Substring($srcDir.Length + 1)
}

# 向上找包住指定行的方法名（private/public/protected 方法声明行）。
function Get-EnclosingMethod([string[]]$codeLines, [int]$lineIdx) {
    for ($j = $lineIdx; $j -ge 0 -and $j -ge ($lineIdx - 200); $j--) {
        if ($codeLines[$j] -match "(private|public|protected|internal)\s+(static\s+)?[\w\.<>,\s\[\]]+\s+(\w+)\s*\(") {
            return $Matches[3]
        }
    }
    return "<top>"
}

# 某方法体（声明起 150 行）内是否含 .Dispose() 调用。
function Test-MethodHasDispose([string[]]$codeLines, [string]$methodName) {
    for ($j = 0; $j -lt $codeLines.Count; $j++) {
        if ($codeLines[$j] -match "(private|public|protected|internal)\s+(static\s+)?[\w\.<>,\s\[\]]+\s+$methodName\s*\(") {
            $to = [Math]::Min($codeLines.Count - 1, $j + 150)
            $body = ($codeLines[$j..$to] -join "`n")
            return ($body -match "\.Dispose\(\)")
        }
    }
    return $false
}

$files = Get-ChildItem $srcDir -Recurse -Filter "*.cs" |
    Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" } |
    Select-Object -ExpandProperty FullName

foreach ($file in $files) {
    $rel = Get-RelPath $file
    $lines = [IO.File]::ReadAllLines($file, [Text.Encoding]::UTF8)
    $isDesigner = $rel -like "*.Designer.cs"

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $ln = $lines[$i]

        # ── R1：Controls.Clear() ──
        if ($ln -match "\.Controls\.Clear\(\)") {
            if ($SafeClearFiles -contains $rel) { continue }
            $from = [Math]::Max(0, $i - 15)
            $window = ($lines[$from..$i] -join "`n")
            if ($window -notmatch "Dispose\(\)") {
                $high += "R1 HIGH $($rel):$($i + 1) Controls.Clear() 前15行无 Dispose()"
            }
        }

        # ── R3：Controls.Remove( ──
        if ($ln -match "\.Controls\.Remove\(") {
            if ($SafeRemoveFiles -contains $rel) { continue }
            $to = [Math]::Min($lines.Count - 1, $i + 10)
            $window = ($lines[$i..$to] -join "`n")
            if ($window -notmatch "Dispose\(\)") {
                $high += "R3 HIGH $($rel):$($i + 1) Controls.Remove( 后10行无 Dispose()"
            }
        }

        # ── R2：非模态 .Show(（排除 ShowDialog） ──
        # 行为检查（V1.72.13 反向验证教训：只认登记不认行为的白名单，注掉释放也放行）。
        # 找到 Show 所在方法 → 白名单必须登记该方法 → 按配对类型验释放行为还在；
        # 未登记的新增 Show 点一律 HIGH。新增弹窗 = 登记 + 行为双过。
        if ($ln -match "\.Show\((this)?\)" -and $ln -notmatch "ShowDialog") {
            $method = Get-EnclosingMethod $lines $i
            $entry = $null
            foreach ($k in $SafeShowKeys) {
                $parts = $k -split "\|"
                if ($parts[0] -eq $method -and $rel -like ("*" + $parts[1])) { $entry = $parts; break }
            }
            $ok = $false
            if ($entry -ne $null) {
                if ($entry.Count -eq 2) {
                    $ok = Test-MethodHasDispose $lines $method
                } elseif ($entry[2] -like "reuse:*") {
                    $fld = $entry[2].Substring(6)
                    $ok = (([IO.File]::ReadAllText($file, [Text.Encoding]::UTF8)) -match [regex]::Escape($fld))
                } else {
                    $ok = Test-MethodHasDispose $lines $entry[2]
                }
            }
            if (-not $ok) {
                if ($entry -eq $null) {
                    $high += "R2 HIGH $($rel):$($i + 1) 非模态 Show 在方法 $method（白名单无登记）"
                } else {
                    $high += "R2 HIGH $($rel):$($i + 1) 白名单方法 $method 的释放行为丢失（Dispose 被删改）"
                }
            }
        }

        # ── R4：非 Designer 里 new 输入/表格类控件（INFO，人工确认） ──
        # DataGridView 也进名单：自带编辑控件与滚动条，随树释放才安全。
        if (-not $isDesigner -and $ln -match "new (Sunny\.UI\.UITextBox|Sunny\.UI\.UIComboBox|Sunny\.UI\.UIDataGridView|TextBox|ComboBox|ListBox|RichTextBox|DataGridView)\b") {
            $info += "R4 INFO $($rel):$($i + 1) 动态创建 $($Matches[1])（确认随窗体树释放或显式 Dispose）"
        }
    }
}

Write-Host "===== 终结器风险审计 ====="
foreach ($h in $high) { Write-Host "  $h" }
foreach ($m in $info) { Write-Host "  $m" }
Write-Host "HIGH=$($high.Count) INFO=$($info.Count)"
if ($high.Count -gt 0) { exit 1 }
exit 0
