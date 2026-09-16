# ============================================================================
#  obfuscated_release.ps1 - One-click 发版流水线（V1.88.10 起，V1.88.11 加调试包）:
#      前置检查 -> Release 构建 -> [混淆] -> 组包 -> 验收(行为+对账+冒烟)
#      缺省打混淆包；加 -DebugPackage 打调试包（不翻标记、不混淆，其余一样）。
#
#  Usage (from anywhere):
#    powershell -ExecutionPolicy Bypass -File <skill>\scripts\obfuscated_release.ps1
#      [ -ReleaseLabel "V1.88.9" ]   缺省自动从 Services/BuildWatermark.cs 读 ReleaseLabel
#      [ -AllowDirty ]               工作区有未提交改动时仍继续（默认直接拒绝，
#                                    防"发的包和入库的代码对不上"，强烈不建议加）
#      [ -DebugPackage ]             打调试包（调试期发工控机联调：水印标"调试版"，
#                                    堆栈可读不用反解；保护靠激活绑机器＋不发 pdb）
#
#  Outputs (all under <repo>\release\<Label>-obf|dbg\, gitignored, never committed):
#      包\        发给客户的全部文件（exe + 依赖 dll + exe.config + 部署说明.txt；
#                 无 pdb/源码/旧数据，拷到工控机即用；-dbg 包的 exe 未混淆）
#      归档\      内部留底（混淆包才有 Mapping.txt；全包 MD5 + pdb + 本次配置 + git 版本；
#                 客户发回堆栈靠"水印版本→这套归档"反解，调试版堆栈直接可读）
#
#  Acceptance (any failure aborts with non-zero, package kept for inspection):
#      A. behavior: 公开 API 行为 12 项（水印版本+版本标记/日志落盘/反射点/JSON/规则/激活）
#      B. dump+diff: Models 元数据 1:1 对账（未混淆 Release 是标准答案；调试包恒过）
#      C. smoke: exe 真启动 22s 存活 + AppLog 首行水印含版本与版本标记 + 无 Crash 文件
#
#  Exit codes: 0 = released+accepted; 1 = build/obfuscate fail;
#              2 = acceptance fail; 3 = setup fail; 4 = dirty worktree.
# ============================================================================

param(
    [string]$ReleaseLabel = "",
    [switch]$AllowDirty,
    [switch]$DebugPackage
)

$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..\")).Path

function Fail([string]$msg, [int]$code) {
    Write-Host $msg -ForegroundColor Red
    exit $code
}

# ---------- 0. 前置检查 ----------
Write-Host "========== [0/6] 前置检查 =========="
Push-Location $RepoRoot
try { $gitStatus = git status --porcelain } catch { Fail "[SETUP-FAIL] git 不可用" 3 }
Pop-Location
if ($gitStatus -and (-not $AllowDirty)) {
    Write-Host "[DIRTY] 工作区有未提交改动，发包会与入库代码对不上：" -ForegroundColor Red
    $gitStatus | Select-Object -First 10 | ForEach-Object { Write-Host ("  " + $_) }
    Write-Host "先提交或 stash，确认要硬发加 -AllowDirty。" -ForegroundColor Red
    exit 4
}

if ($ReleaseLabel -eq "") {
    $wmPath = Join-Path $RepoRoot "AgingTestSystem\Services\BuildWatermark.cs"
    $m = Select-String -LiteralPath $wmPath -Pattern 'ReleaseLabel\s*=\s*"(V[^"]+)"'
    if (-not $m) { Fail "[SETUP-FAIL] 读不到 BuildWatermark.ReleaseLabel" 3 }
    $ReleaseLabel = $m.Matches[0].Groups[1].Value
}
Write-Host "发版版本: $ReleaseLabel"

$obfCmd = Get-Command "obfuscar.console" -ErrorAction SilentlyContinue
if (-not $obfCmd) {
    Write-Host "混淆器未安装，正在安装 Obfuscar.GlobalTool ..."
    & dotnet tool install -g Obfuscar.GlobalTool
    if ($LASTEXITCODE -ne 0) { Fail "[SETUP-FAIL] 混淆器安装失败" 3 }
    $obfCmd = Get-Command "obfuscar.console" -ErrorAction SilentlyContinue
    if (-not $obfCmd) { Fail "[SETUP-FAIL] 混淆器仍不可用（装完请重开终端再跑）" 3 }
}

$msbuild = @("D:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe") `
    + @(Get-ChildItem "C:\Program Files*\Microsoft Visual Studio", "D:\Program Files*\Microsoft Visual Studio" `
        -Recurse -Filter MSBuild.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName) `
    | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $msbuild) { Fail "[SETUP-FAIL] MSBuild.exe not found" 3 }
$csc = @("D:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\Roslyn\csc.exe") `
    + @(Get-ChildItem "D:\Program Files*\Microsoft Visual Studio" -Recurse -Filter csc.exe -ErrorAction SilentlyContinue `
        | Select-Object -ExpandProperty FullName) `
    | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $csc) { Fail "[SETUP-FAIL] csc.exe not found" 3 }

$workDir = Join-Path $env:TEMP "opencode\obf-release"
$inDir   = Join-Path $workDir "in"
$outDir  = Join-Path $workDir "out"
# 调试包与混淆包只有三处不同：目录后缀 / 水印标记期望 / 要不要跑 Obfuscar，其余组包验收全同路。
$flavorSuffix = "obf"
$expectObfFlag = "1"
$watermarkWord = "混淆版"
if ($DebugPackage) {
    $flavorSuffix = "dbg"
    $expectObfFlag = "0"
    $watermarkWord = "未混淆"
}
$relRoot = Join-Path $RepoRoot ("release\" + $ReleaseLabel + "-" + $flavorSuffix)
$pkgDir  = Join-Path $relRoot "包"
$arcDir  = Join-Path $relRoot "归档"
$wmFile  = Join-Path $RepoRoot "AgingTestSystem\Services\BuildWatermark.cs"
$wmBackup = Join-Path $workDir "BuildWatermark.cs.bak"
$flipped = $false
Write-Host ("发版口味: " + $(if ($DebugPackage) { "调试包（不混淆，水印标调试版）" } else { "混淆包" }))

# ---------- 1. 临时翻混淆标记（混淆包才翻；finally 必还原） ----------
Write-Host ""
Write-Host "========== [1/6] Release 构建 =========="
if (Test-Path $workDir) { Remove-Item -LiteralPath $workDir -Recurse -Force }
New-Item -ItemType Directory -Path $inDir -Force | Out-Null
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
Copy-Item -LiteralPath $wmFile -Destination $wmBackup -Force
if (-not $DebugPackage) {
    # 只翻 IsObfuscatedBuild 那一行（ReleaseLabel 不动：产品版本不变，只是换了个"口味"）。
    $wmText = [IO.File]::ReadAllText($wmFile, [Text.Encoding]::UTF8)
    $nFlip = ([regex]::Matches($wmText, "public const bool IsObfuscatedBuild = false")).Count
    if ($nFlip -ne 1) { Fail "[SETUP-FAIL] IsObfuscatedBuild 行定位失败（期望1处，实际$nFlip），不敢动" 3 }
    $wmText = $wmText.Replace("public const bool IsObfuscatedBuild = false", "public const bool IsObfuscatedBuild = true")
    [IO.File]::WriteAllText($wmFile, $wmText, (New-Object Text.UTF8Encoding $false))
    $flipped = $true
    Write-Host "已临时翻 IsObfuscatedBuild=false->true（发完即还原）"
}

$buildOk = $false
try {
    # 注意用 Rebuild 不用 Build：发版必须是"当前源码→产物"的确定性链路。
    # 增量 Build 碰上"改完又改回"（如混淆标记 true→false 还原后内容与上次一致）会跳过编译，
    # 产物里残留上一次的标记（V1.88.11 实测：调试包里跑出混淆版水印，验收 A3 抓到）。
    & $msbuild (Join-Path $RepoRoot "AgingTestSystem\AgingTestSystem.csproj") `
        /p:Configuration=Release /p:Platform=AnyCPU /t:Rebuild /nologo /v:m
    if ($LASTEXITCODE -ne 0) { Fail "[BUILD FAIL] MSBuild exit=$LASTEXITCODE" 1 }
    Write-Host "[BUILD PASS]" -ForegroundColor Green
    $buildOk = $true

    # ---------- 2. 混淆（调试包跳过：inDir 即发版源） ----------
    Write-Host ""
    $shipExeDir = $outDir
    $mapping = $null
    if ($DebugPackage) {
        Write-Host "========== [2/6] 混淆（调试包跳过） =========="
        Copy-Item -Path (Join-Path $RepoRoot "AgingTestSystem\bin\Release\*") -Destination $inDir -Force
        $shipExeDir = $inDir
        Write-Host "[OBFUSCATE SKIP] 调试包不混淆" -ForegroundColor Green
    }
    else {
    Write-Host "========== [2/6] 混淆 =========="
    Copy-Item -Path (Join-Path $RepoRoot "AgingTestSystem\bin\Release\*") -Destination $inDir -Force
    [xml]$cfg = Get-Content -LiteralPath (Join-Path $RepoRoot "tools\obfuscation\obfuscar.xml") -Encoding UTF8
    # 生成绝对路径版配置（Obfuscar v3 起只认绝对路径；模板里是相对占位，手动跑时自己改）。
    # 注意：这里故意用 SelectSingleNode + SetAttribute 写属性，不用 $_.value = ... 的
    # PowerShell XML 适配器写法——后者在 powershell -File 跑本脚本时抛 XmlNodeSetShouldBeAString，
    # 交互式里却正常（原因未明，显式 DOM 最稳）。
    $nodeIn = $cfg.SelectSingleNode("/Obfuscator/Var[@name='InPath']")
    $nodeOut = $cfg.SelectSingleNode("/Obfuscator/Var[@name='OutPath']")
    if (-not $nodeIn -or -not $nodeOut) { Fail "[SETUP-FAIL] 模板缺 InPath/OutPath 节点" 3 }
    $nodeIn.SetAttribute("value", $inDir)
    $nodeOut.SetAttribute("value", $outDir)
    $runCfg = Join-Path $workDir "obfuscar.run.xml"
    $cfg.Save($runCfg)
    & obfuscar.console $runCfg
    if ($LASTEXITCODE -ne 0) { Fail "[OBFUSCATE FAIL] obfuscar exit=$LASTEXITCODE" 1 }
    $mapping = Join-Path $outDir "Mapping.txt"
    if (-not (Test-Path $mapping)) {
        $mapping = Get-ChildItem -LiteralPath $outDir -Filter "Mapping*" | Select-Object -First 1 -ExpandProperty FullName
        if (-not $mapping) { Fail "[OBFUSCATE FAIL] 找不到 Mapping 文件" 1 }
    }
    Write-Host "[OBFUSCATE PASS] Mapping=$mapping" -ForegroundColor Green
    }

    # ---------- 3. 组包 ----------
    Write-Host ""
    Write-Host "========== [3/6] 组包 =========="
    if (Test-Path $relRoot) { Remove-Item -LiteralPath $relRoot -Recurse -Force }
    New-Item -ItemType Directory -Path $pkgDir -Force | Out-Null
    New-Item -ItemType Directory -Path $arcDir -Force | Out-Null
    # 包里：发版 exe + 未混淆依赖（混淆包只混淆自己的 exe，第三方 dll 本来就不动）。
    # 排除：旧 exe/pdb/文档 xml/运行时数据（Users/ini/Logs/Projects 正常 Release 里本就没有，
    # 这里是防御性排除，万一有人在本机跑过 Release 也不会把脏数据发出去）。
    Copy-Item -Path (Join-Path $inDir "*") -Destination $pkgDir -Force -Exclude @(
        "烧屏测试控制中心.exe", "*.pdb", "*.xml",
        "Users.json", "RememberedLogin.json", "TestSession.json", "MainSetting.ini",
        "MesQueue.json", "Logs", "Projects")
    Copy-Item -LiteralPath (Join-Path $shipExeDir "烧屏测试控制中心.exe") -Destination $pkgDir -Force
    if (-not (Test-Path (Join-Path $pkgDir "烧屏测试控制中心.exe.config"))) {
        Fail "[PACKAGE FAIL] 缺 exe.config（App.config 没编进去，跑不起来）" 1
    }
    $flavorName = $(if ($DebugPackage) { "调试版" } else { "混淆版" })
    $flavorWarn = $(if ($DebugPackage) {
        "（调试版堆栈可读，排错不用反解；保护靠激活绑机器＋不发 pdb，稳定后换混淆版）"
    } else {
        "（堆栈需用归档 Mapping 反解，见内部培训文档十二章）"
    })
    $deployNote = @"
老化测试系统 $ReleaseLabel（$flavorName）部署说明$flavorWarn
================================================
1. 把"包"里全部文件拷到工控机同一个文件夹（不要只拷 exe，dll 和 config 都要）。
2. 双击 烧屏测试控制中心.exe 启动。首次启动自动生成用户文件与激活模板，
   用默认账号登录后第一件事：改掉全部默认密码（admin/technician/operator）。
3. 软件授权：把"设备ID＋设备码"报给我们拿激活码（与 HJVision 同一套工具）。
   MainSetting.ini 一机一码，别拷到别的机器。
4. 出问题时把 Logs 文件夹整个发回（AppLog 首行即版本水印，Crash_*.log 看堆栈），
   外加故障台号与现象截图。
5. 不要删程序目录下的任何 dll/config；升级时整包覆盖（先备份 Logs 与 Projects）。
"@
    [IO.File]::WriteAllText((Join-Path $pkgDir "部署说明.txt"), $deployNote, (New-Object Text.UTF8Encoding $false))
    # 归档：Mapping（混淆包才有） + 全包 MD5 + pdb + 本次配置 + git 版本（客户堆栈反解全靠它）。
    if ($mapping -and (Test-Path $mapping)) {
        Copy-Item -LiteralPath $mapping -Destination (Join-Path $arcDir "Mapping.txt") -Force
    }
    Copy-Item -LiteralPath (Join-Path $RepoRoot "tools\obfuscation\obfuscar.xml") -Destination (Join-Path $arcDir "obfuscar.xml") -Force
    $inPdb = Join-Path $inDir "烧屏测试控制中心.pdb"
    if (Test-Path $inPdb) { Copy-Item -LiteralPath $inPdb -Destination $arcDir -Force }
    Push-Location $RepoRoot
    try {
        $head = git rev-parse HEAD
        $dirty = git status --porcelain
        $diffStat = git diff --stat
    }
    catch { $head = "（git不可用）"; $dirty = ""; $diffStat = "" }
    Pop-Location
    $md5Lines = Get-ChildItem -LiteralPath $pkgDir -File | ForEach-Object {
        $h = (Get-FileHash -LiteralPath $_.FullName -Algorithm MD5).Hash
        ("$h  " + $_.Name)
    }
    [IO.File]::WriteAllText((Join-Path $arcDir "MD5.txt"), ($md5Lines -join "`r`n") + "`r`n", (New-Object Text.UTF8Encoding $false))
    [IO.File]::WriteAllText((Join-Path $arcDir "版本.txt"),
        ("ReleaseLabel=" + $ReleaseLabel + "`r`nIsObfuscatedBuild=" + $(if ($DebugPackage) { "false（调试包）" } else { "true（混淆包）" }) + "`r`n打包时间=" `
            + (Get-Date -Format "yyyy-MM-dd HH:mm:ss") + "`r`ngit=" + $head + "`r`n" `
            + "---- 打包时工作区（-AllowDirty 硬发时看这里，非空=包与HEAD有差） ----`r`n" `
            + (($dirty -join "`r`n") + "`r`n" + $diffStat)),
        (New-Object Text.UTF8Encoding $false))
    Write-Host "[PACKAGE PASS] $pkgDir" -ForegroundColor Green

    # ---------- 4/5/6. 验收（行为 + 对账 + 冒烟） ----------
    Write-Host ""
    Write-Host "========== [4/6] 验收A：行为 =========="
    $accDir = Join-Path $workDir "accept"
    if (Test-Path $accDir) { Remove-Item -LiteralPath $accDir -Recurse -Force }
    New-Item -ItemType Directory -Path $accDir -Force | Out-Null
    Copy-Item -Path (Join-Path $pkgDir "*") -Destination $accDir -Force -Exclude "部署说明.txt"
    & $csc /nologo /t:exe /out:"$accDir\Accept.exe" `
        (Join-Path $RepoRoot ".opencode\skills\agingtest-regression\tests\ObfuscationAcceptance.cs") `
        /r:"$accDir\烧屏测试控制中心.exe" /r:"$accDir\Newtonsoft.Json.dll" /r:"$accDir\SunnyUI.dll" /codepage:65001
    if ($LASTEXITCODE -ne 0) { Fail "[ACCEPT FAIL] 验收跑器编译不过" 2 }
    Push-Location $accDir
    try { & ".\Accept.exe" behavior $accDir $ReleaseLabel $expectObfFlag; $cBeh = $LASTEXITCODE }
    finally { Pop-Location }
    if ($cBeh -ne 0) { Fail "[ACCEPT FAIL] 行为验收未通过（见上）" 2 }
    Write-Host "[ACCEPT-A PASS]" -ForegroundColor Green

    Write-Host ""
    Write-Host "========== [5/6] 验收B：元数据对账 =========="
    Push-Location $accDir
    try { & ".\Accept.exe" dump $accDir | Out-File -LiteralPath (Join-Path $workDir "dump-obf.txt") -Encoding utf8; $cD1 = $LASTEXITCODE }
    finally { Pop-Location }
    Push-Location $inDir
    try { & (Join-Path $accDir "Accept.exe") dump $inDir | Out-File -LiteralPath (Join-Path $workDir "dump-ref.txt") -Encoding utf8; $cD2 = $LASTEXITCODE }
    finally { Pop-Location }
    if ($cD1 -ne 0 -or $cD2 -ne 0) { Fail "[ACCEPT FAIL] 元数据 dump 失败" 2 }
    $diff = Compare-Object (Get-Content (Join-Path $workDir "dump-obf.txt")) (Get-Content (Join-Path $workDir "dump-ref.txt"))
    if ($diff) {
        $diff | Select-Object -First 10 | ForEach-Object { Write-Host ("  " + $_) }
        Fail "[ACCEPT FAIL] Models 元数据与未混淆版不一致（Skip 漏配，先修配置再发）" 2
    }
    Write-Host "[ACCEPT-B PASS] Models 元数据一字不差" -ForegroundColor Green

    Write-Host ""
    Write-Host "========== [6/6] 验收C：真机冒烟 =========="
    $smkDir = Join-Path $workDir "smoke"
    if (Test-Path $smkDir) { Remove-Item -LiteralPath $smkDir -Recurse -Force }
    New-Item -ItemType Directory -Path $smkDir -Force | Out-Null
    Copy-Item -Path (Join-Path $pkgDir "*") -Destination $smkDir -Force -Exclude "部署说明.txt"
    $p = Start-Process -FilePath (Join-Path $smkDir "烧屏测试控制中心.exe") -PassThru
    $waited = 0
    while ($waited -lt 22000 -and -not $p.HasExited) { Start-Sleep -Milliseconds 1000; $waited += 1000 }
    if ($p.HasExited) { Fail ("[ACCEPT FAIL] 发版包启动" + [int]($waited / 1000) + "s内退出，码=" + $p.ExitCode) 2 }
    try { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue } catch { }
    Start-Sleep -Milliseconds 1500
    $appLog = Get-ChildItem -LiteralPath (Join-Path $smkDir "Logs") -Filter "AppLog_*.log" -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $appLog) { Fail "[ACCEPT FAIL] 冒烟后无 AppLog（启动水印都没写，主窗体没起来）" 2 }
    $logText = [IO.File]::ReadAllText($appLog.FullName, [Text.Encoding]::UTF8)
    if (-not ($logText.Contains("[启动]") -and $logText.Contains($ReleaseLabel) -and $logText.Contains($watermarkWord))) {
        Fail ("[ACCEPT FAIL] AppLog 首行水印不对（缺[启动]/版本/" + $watermarkWord + "标记）") 2
    }
    $crash = Get-ChildItem -LiteralPath (Join-Path $smkDir "Logs") -Filter "Crash_*.log" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($crash) { Fail ("[ACCEPT FAIL] 冒烟过程产生崩溃日志：" + $crash.Name) 2 }
    Write-Host "[ACCEPT-C PASS] 存活22s + 水印正确 + 无崩溃" -ForegroundColor Green
}
finally {
    # ---------- 还原混淆标记（混淆包才翻过；调试包从没动过，只校验） ----------
    if ($flipped) {
        Copy-Item -LiteralPath $wmBackup -Destination $wmFile -Force
        $after = [IO.File]::ReadAllText($wmFile, [Text.Encoding]::UTF8)
        $before = [IO.File]::ReadAllText($wmBackup, [Text.Encoding]::UTF8)
        if ($after -ceq $before) { Write-Host "混淆标记已还原（IsObfuscatedBuild=false）" }
        else { Write-Host "[WARN] 标记还原校验失败，请手动检查 BuildWatermark.cs！" -ForegroundColor Red }
    }
}

Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
Write-Host " 发版成功 + 验收全过" -ForegroundColor Green
Write-Host " 包（拷工控机）: $pkgDir"
Write-Host " 归档（内部留底）: $arcDir"
Write-Host "==========================================" -ForegroundColor Green
exit 0
