# ============================================================================
#  run_unit_tests.ps1 - Compile and run the AgingTestSystem regression harness.
#
#  What it does:
#    1. Copy build outputs from AgingTestSystem/bin/Debug into a clean,
#       isolated run directory under %TEMP%\opencode\agingtest-run
#       (so Users.json / Recipes.json / Logs written during tests never
#        pollute the repo or the real bin folder).
#    2. Compile tests/TestRunner.cs against those assemblies with csc.exe.
#       NOTE: /codepage:65001 is mandatory - the source file contains Chinese
#       comments and has no BOM; without it csc reads GBK and mojibakes.
#    3. Run TestRunner.exe, forward its output and exit code.
#
#  Exit codes: 0 = all pass, 1 = test failures, 2 = setup/build errors.
# ============================================================================

param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..\")).Path
)

$ErrorActionPreference = "Stop"

$bin    = Join-Path $RepoRoot "AgingTestSystem\bin\Debug"
$srcCs  = @(
    (Join-Path $PSScriptRoot "..\tests\TestRunner.cs"),
    (Join-Path $PSScriptRoot "..\tests\DeviceManagerIntegrationTests.cs")
)
$runDir = Join-Path $env:TEMP "opencode\agingtest-run"

if (-not (Test-Path (Join-Path $bin "AgingTestSystem.exe"))) {
    Write-Host "[SETUP-FAIL] Build first: MSBuild.exe AgingTestSystem/AgingTestSystem.csproj" -ForegroundColor Red
    exit 2
}
$srcRes = $srcCs | ForEach-Object { Resolve-Path $_ }

# --- 1. prepare isolated run dir -------------------------------------------
if (Test-Path $runDir) { Remove-Item -LiteralPath $runDir -Recurse -Force }
New-Item -ItemType Directory -Path $runDir -Force | Out-Null
Copy-Item -Path (Join-Path $bin "*") -Destination $runDir -Force

# remove runtime json so harness starts from a clean state every time
foreach ($f in @("Users.json", "Recipes.json", "PanelLayout.json", "HomeLayout.json",
                 "RememberedLogin.json", "StationSettings.json")) {
    $p = Join-Path $runDir $f
    if (Test-Path $p) { Remove-Item -LiteralPath $p -Force }
}

# --- 2. locate csc.exe ------------------------------------------------------
$cscCandidates = @(
    "D:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\Roslyn\csc.exe"
) + @(Get-ChildItem "D:\Program Files*\Microsoft Visual Studio" -Recurse -Filter csc.exe -ErrorAction SilentlyContinue |
      Select-Object -ExpandProperty FullName)
$csc = $cscCandidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $csc) {
    Write-Host "[SETUP-FAIL] csc.exe not found" -ForegroundColor Red
    exit 2
}

# --- 3. compile harness -----------------------------------------------------
$outExe = Join-Path $runDir "TestRunner.exe"
$cscArgs = @(
    '/nologo', '/t:exe', "/out:$outExe"
) + @($srcRes) + @(
    '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll',
    "/r:$runDir\Newtonsoft.Json.dll",
    "/r:$runDir\AgingTestSystem.exe",
    # V1.62: UiPureHelpers/HistoryCsv/StationCache 等用例直接 new SunnyUI 派生窗体
    # (HistoryRecordForm/RecipeManagerForm/SettingsForm)，编译期必须能解析 UIForm 基类
    "/r:$runDir\SunnyUI.dll",
    '/codepage:65001'
)
& $csc @cscArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "[COMPILE-FAIL] csc exit=$LASTEXITCODE" -ForegroundColor Red
    exit 2
}

# --- 4. run harness ---------------------------------------------------------
Write-Host ""
Write-Host ">>> Running regression suite in: $runDir"
# Force UTF-8 console so Chinese assertion names are not mojibake
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
Push-Location $runDir
try     { & ".\TestRunner.exe"; $code = $LASTEXITCODE }
finally { Pop-Location }

if ($code -eq 0) { Write-Host "`n[UNIT-TESTS PASS]" -ForegroundColor Green }
else            { Write-Host "`n[UNIT-TESTS FAIL] exit=$code" -ForegroundColor Red }
exit $code
