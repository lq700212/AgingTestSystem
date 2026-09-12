# ============================================================================
#  audit_finalizer_risk.ps1 - 终结器跨线程崩溃 + 关窗竞态风险审计
#  （V1.72.13 固化终结器，V1.72.15 追加关窗竞态 R5/R6/R7）。
#
#  背景：Sunny 输入控件（UITextBox/UIComboBox，内部包原生 TextBox）与原生
#  TextBox/ComboBox/ListBox，若"显示过句柄 + 没显式 Dispose + 无根引用"，
#  GC 时走终结器线程 Dispose，内部读 Handle 即抛 InvalidOperationException
#  （堆栈终点 ResetAutoComplete←Dispose←Finalize，控件 Name 全空、案发时机
#  看 GC 三特征）。案发现场两次：驾驶舱右栏 Clear（V1.72.12）、设置窗三
#  popup（V1.72.13），案发时用户正在干什么全是巧合。
#  V1.72.14 定罪第三类"关窗竞态"：释放路径全对，仍炸——后台拍在关窗前后脚
#  Invoke/BeginInvoke 进已销毁句柄，或排队回调关后执行直碰已释放控件；
#  "关 A 开 B 必炸"是关 A 尾巴被开 B 的 GC/排队赶出来。对策三件套：
#  _closed 首行置位 + 句柄三查 + 日志 BeginInvoke（禁同步 Invoke），
#  排队回调入口自拦，关后硬件写停手。
#
#  用法：powershell -ExecutionPolicy Bypass -File audit_finalizer_risk.ps1
#  改 UI 代码（新增窗体/动态控件/非模态弹窗/Clear/Remove/后台线程/定时器/
#  事件订阅）后必跑；HIGH > 0 即 exit 1（先修再提交），只有 INFO 即 exit 0。
#
#  规则（宁误报不漏报，误报进白名单）：
#    R1 Controls.Clear() 前 15 行无 ControlDisposeHelper            → HIGH
#       （V1.72.16 收紧：以前只认 Dispose()，但 foreach 直接枚举逐个 Dispose
#       会因 Dispose 摘除父集合导致跳过 → 孤儿 → 终结器跨线程炸；
#       一律走快照 helper。白名单已删，靠行为检查，人人平等）
#    R2 非模态 .Show(（排除 ShowDialog），无释放关联              → HIGH
#    R3 Controls.Remove( 后 10 行无 Dispose()                      → HIGH
#    R4 非 Designer.cs 里 new 输入类控件                          → INFO（人工确认随树/释放）
#    R5 UI 文件（Dialogs/Views/Controls）同步 Control.Invoke(    → HIGH
#       （后台线程必须 BeginInvoke + 关窗守卫；?.Invoke 事件触发与反射
#       MethodInfo.Invoke 不在此列；V1.72.15：通讯窗 txtLog.Invoke 即此病）
#    R6 非 Designer.cs 里 Timer 字段无同文件 Dispose()            → HIGH
#       （字段定时器不在 components 容器就必须手停手放；V1.72.15：
#       设置窗 _pressTimer 曾漏网；(components) 构造的跳过）
#    R7 UI 文件里自定义 On* 事件 += 无同文件 -=                  → HIGH
#       （长生命周期的事件源抓着窗体不放 = 泄漏 + 关后回调；
#       V1.72.15：ID 绑定窗扫码/MainForm 五事件即此锁）
#    R8 Designer.cs 可序列化锁（V1.72.16：预览即脏/预览失败/量程丢失三连） → HIGH
#       a) AddRange(静态字段)——设计器 CodeDom 在实例上找不到静态成员，
#          加载直接失败（判定窗 Dispositions 实锤）；只许 new 数组/字面；
#       b) = xxx.Range.Min/Max 元组成员表达式——序列化器认不出，存盘整行删，
#          输入框变回 0~100，拖动赋值 340 即 ArgumentOutOfRange（主页布局实锤）；
#          量程一律写字面值，改常量同步改 Designer（注释写文件头，方法体内注释
#          VS 重写全删，批量窗中文说明就是这么没的）；
#       c) ZoomScaleRect + AutoScaleMode.Font 混搭——Zoom 的 setter 把模式掰回
#          None，活值与代码对不上，打开即脏甚至加载失败；Sunny 窗一律 None
#          且无 AutoScaleDimensions（运行时零影响，终值本来就是 None）。
#       注释行（// 开头）不扫，说明文字里提这些词不算犯规。
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
# 【V1.72.16】$SafeClearFiles 已删：R1 现在认 ControlDisposeHelper 行为，
# 主窗 H5 与驾驶舱都已改走 helper，靠行为检查通过，不再靠名单放行。
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

        # ── R1：Controls.Clear()（V1.72.16 收紧：只认快照 helper） ──
        # foreach 直接枚举逐个 Dispose 会跳过（Dispose 摘除父集合，枚举器下标错位），
        # 漏掉的孩子变孤儿，GC 时终结器线程炸。15 行内必须出现 ControlDisposeHelper。
        if ($ln -match "\.Controls\.Clear\(\)") {
            $from = [Math]::Max(0, $i - 15)
            $window = ($lines[$from..$i] -join "`n")
            if ($window -notmatch "ControlDisposeHelper") {
                $high += "R1 HIGH $($rel):$($i + 1) Controls.Clear() 前15行无 ControlDisposeHelper（改走快照释放，foreach直释会跳过）"
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

        # ── R5：UI 文件同步 Control.Invoke(（V1.72.15 关窗竞态） ──
        # 后台线程回 UI 必须 BeginInvoke + 关窗守卫；同步 Invoke 在关闭前后脚
        # 直接炸"线程间操作无效"。?.Invoke（事件触发）与反射 Invoke 不在此列。
        # 范围：Dialogs/Views/Controls（Services 的 MethodInfo.Invoke 如
        # ThemeManager 反射着色是合法用途，不扫）。
        $isUiDir = ($rel -like "Dialogs\*" -or $rel -like "Views\*" -or $rel -like "Controls\*")
        if ($isUiDir -and $ln -match "(?<!\?)\.Invoke\(" -and $ln -notmatch "MethodInfo|GetMethod|\bm\.Invoke") {
            $high += "R5 HIGH $($rel):$($i + 1) 同步 .Invoke(（改 BeginInvoke + _closed/句柄守卫）"
        }

        # ── R7：长生命周期服务事件 += 无同文件 -=（V1.72.15 关窗竞态） ──
        # 设备管理器/扫码枪服务活得比任何窗体都久：窗体订阅其 On* 事件后若不退订，
        # 服务抓着窗体引用 = 泄漏 + 关后回调炸框；退订还拦不住已排队的 Post，
        # handler 入口另需 _closed 自拦（R7 只锁退订）。
        # 短命源（bindingForm/form/popup 等局部弹窗的事件）不在此列：源先死，无泄漏。
        # _gridView 自绘网格同理（重建即 Dispose 旧实例，订阅随旧实例进 GC）。
        if (-not $isDesigner -and $isUiDir -and $ln -match "(_deviceManager|_scanner)\.(On[A-Z]\w*)\s*\+=") {
            $src = $Matches[1]
            $evt = $Matches[2]
            $body = ($lines -join "`n")
            if ($body -notmatch [regex]::Escape("$src.$evt") + "\s*-=") {
                $high += "R7 HIGH $($rel):$($i + 1) 长生命周期 $src.$evt 只有 += 无 -=（OnFormClosed/FormClosing 里退订）"
            }
        }

        # ── R8：Designer.cs 可序列化锁（V1.72.16） ──
        # 注释行不扫（说明文字里提 AddRange/Range.Min 不算犯规）。
        if ($isDesigner -and $ln -notmatch "^\s*//") {
            # a) AddRange(静态字段/变量)：只许 new 数组，裸标识符即静态引用，设计器加载失败。
            if ($ln -match "\.AddRange\(\s*(?!new\b)([A-Za-z_]\w*)") {
                $high += "R8a HIGH $($rel):$($i + 1) Designer 里 AddRange($($Matches[1])) 非 new 数组（静态引用设计器认不出，改构造代码填）"
            }
            # b) = xxx.Range.Min/Max：元组成员表达式序列化器认不出，存盘整行删。
            if ($ln -match "=\s*[\w\.]+\.(Min|Max)\s*;") {
                $high += "R8b HIGH $($rel):$($i + 1) Designer 里量程写成员表达式（改字面值，与常量文件同步）"
            }
        }
    }

    # ── R8c：ZoomScaleRect + AutoScaleMode.Font 混搭（文件级，每文件报一次） ──
    if ($rel -like "*.Designer.cs") {
        $full = ($lines -join "`n")
        if ($full -match "ZoomScaleRect" -and $full -match "AutoScaleMode\s*=\s*[\w\.]*Font") {
            $high += "R8c HIGH $($rel) ZoomScaleRect 与 AutoScaleMode.Font 混搭（改 None 并删 AutoScaleDimensions）"
        }
    }

    # ── R6：非 Designer Timer 字段无同文件 Dispose()（V1.72.15 关窗竞态） ──
    # 字段定时器不在 components 容器就必须手停手放，否则关后 Tick 碰释放后控件。
    # (components) 构造的随容器释放，跳过。文件级检查（放循环外，每文件报一次）。
    if (-not $isDesigner) {
        $full = ($lines -join "`n")
        foreach ($tm in [regex]::Matches($full, "(?m)^\s*(?:private|readonly|private\s+readonly)[\w\.\s,<>]*\bTimer\s+(_\w+)")) {
            $fld = $tm.Groups[1].Value
            $decl = $tm.Value
            if ($full -match [regex]::Escape($fld) + "\s*=\s*new[^\;]*\(\s*components\s*\)") { continue }
            if ($full -match [regex]::Escape($fld) + "\s*=\s*new[^\;]*\(\s*this\.components\s*\)") { continue }
            # ?.Dispose（DeviceManager 的 _collectTimer?.Dispose() 之类）同样算数。
            if ($full -notmatch [regex]::Escape($fld) + "\s*\?*\.Dispose\(\)") {
                $high += "R6 HIGH $($rel) Timer 字段 $fld 无 Dispose（OnFormClosed/Dispose 里 Stop+Dispose）"
            }
        }
    }
}

Write-Host "===== 终结器风险审计 ====="
foreach ($h in $high) { Write-Host "  $h" }
foreach ($m in $info) { Write-Host "  $m" }
Write-Host "HIGH=$($high.Count) INFO=$($info.Count)"
if ($high.Count -gt 0) { exit 1 }
exit 0
