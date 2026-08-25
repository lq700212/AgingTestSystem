# ============================================================================
#  build_and_test.ps1 - One-click full verification pipeline:
#      build  ->  smoke test (real exe)  ->  unit/regression harness.
#
#  Usage (from anywhere):
#    powershell -ExecutionPolicy Bypass -File <skill>\scripts\build_and_test.ps1
#
#  Exit codes: 0 = all green; non-zero = first failing stage:
#      1 = build failed, 2 = smoke failed, 3 = unit tests failed, 4 = setup.
# ============================================================================

$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..\")).Path

Write-Host "========== [1/3] BUILD =========="
$msbCandidates = @(
    "D:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
) + @(Get-ChildItem "C:\Program Files*\Microsoft Visual Studio", "D:\Program Files*\Microsoft Visual Studio" `
        -Recurse -Filter MSBuild.exe -ErrorAction SilentlyContinue |
      Select-Object -ExpandProperty FullName)
$msbuild = $msbCandidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $msbuild) {
    Write-Host "[BUILD FAIL] MSBuild.exe not found" -ForegroundColor Red
    exit 4
}

& $msbuild (Join-Path $RepoRoot "AgingTestSystem\AgingTestSystem.csproj") `
    /p:Configuration=Debug /p:Platform=AnyCPU /t:Build /nologo /v:m
if ($LASTEXITCODE -ne 0) {
    Write-Host "[BUILD FAIL] MSBuild exit=$LASTEXITCODE" -ForegroundColor Red
    exit 1
}
Write-Host "[BUILD PASS]" -ForegroundColor Green

Write-Host ""
Write-Host "========== [2/3] SMOKE TEST =========="
& (Join-Path $PSScriptRoot "smoke_test.ps1") -RepoRoot $RepoRoot
if ($LASTEXITCODE -ne 0) { Write-Host "[SMOKE FAIL]" -ForegroundColor Red; exit 2 }

Write-Host ""
Write-Host "========== [3/3] UNIT / REGRESSION TESTS =========="
& (Join-Path $PSScriptRoot "run_unit_tests.ps1") -RepoRoot $RepoRoot
if ($LASTEXITCODE -ne 0) { Write-Host "[UNIT-TESTS FAIL]" -ForegroundColor Red; exit 3 }

Write-Host ""
Write-Host "=========================================="
Write-Host " ALL GREEN: build + smoke + regression OK" -ForegroundColor Green
exit 0
