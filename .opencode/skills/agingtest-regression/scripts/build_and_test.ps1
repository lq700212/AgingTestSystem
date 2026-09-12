# ============================================================================
#  build_and_test.ps1 - One-click full verification pipeline:
#      build  ->  smoke test (real exe)  ->  unit/regression harness.
#
#  Usage (from anywhere):
#    powershell -ExecutionPolicy Bypass -File <skill>\scripts\build_and_test.ps1
#      [ -Modules "ModA,ModB" ]   只跑指定模块（分级回归：小改走子集）
#      [ -Affected ]              按 git 改动自动算模块子集（小改最常用）；
#                                 映射不到的文件兜底全量，纯文档改动跳过回归
#
#  分级策略（V1.72.4）：日常小改用 -Affected（只测影响面）；
#  大重构/发布前/骨架改动（csproj/Interfaces/用例自身）走全量（默认）。
#
#  Exit codes: 0 = all green; non-zero = first failing stage:
#      1 = build failed, 2 = smoke failed, 3 = unit tests failed, 4 = setup.
# ============================================================================

param(
    [string]$Modules = "",
    [switch]$Affected
)
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
# V1.72.4 分级回归：-Affected 按 git 改动自动算子集；显式 -Modules 优先。
if ($Affected -and ($Modules -eq "")) {
    # 注意：局部变量名绝不能叫 $affected（与开关 $Affected 同名，PS 变量大小写不敏感，
    # 赋值即炸 SwitchParameter 转换错）——血泪，见 SKILL.md 踩坑清单。
    $affectedResult = & (Join-Path $PSScriptRoot "get_affected_modules.ps1") -RepoRoot $RepoRoot
    if ($affectedResult -eq "FULL") {
        Write-Host "[AFFECTED] 兜底全量回归。"
    }
    elseif ($affectedResult -eq "NONE") {
        Write-Host "[AFFECTED] 无可验证模块，跳过回归（构建+冒烟已过）。"
        Write-Host ""
        Write-Host "=========================================="
        Write-Host " ALL GREEN: build + smoke OK (regression skipped: nothing verifiable)" -ForegroundColor Green
        exit 0
    }
    else {
        $Modules = $affectedResult
        Write-Host "[AFFECTED] 按影响面跑子集。"
    }
}
& (Join-Path $PSScriptRoot "run_unit_tests.ps1") -RepoRoot $RepoRoot -Modules $Modules
if ($LASTEXITCODE -ne 0) { Write-Host "[UNIT-TESTS FAIL]" -ForegroundColor Red; exit 3 }

Write-Host ""
Write-Host "=========================================="
Write-Host " ALL GREEN: build + smoke + regression OK" -ForegroundColor Green
exit 0
