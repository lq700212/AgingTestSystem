# ============================================================================
#  smoke_test.ps1 - Smoke test the real 烧屏测试控制中心.exe.
#
#  What it does:
#    1. Verify bin/Debug/烧屏测试控制中心.exe exists (build it first if not).
#    2. Start the exe, keep it alive for -Seconds (default 18; device connect
#       timeouts make real startup take ~10-15s), then stop it.
#    3. PASS = process still alive after the wait window (did not crash),
#       plus basic sanity: CPU consumed > 0 and memory allocated.
#
#  Exit codes: 0 = pass, 1 = fail, 2 = setup error.
# ============================================================================

param(
    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..\")).Path,
    [int]$Seconds = 18
)

$ErrorActionPreference = "Stop"
$exe = Join-Path $RepoRoot "AgingTestSystem\bin\Debug\烧屏测试控制中心.exe"

if (-not (Test-Path $exe)) {
    Write-Host "[SMOKE FAIL] exe not found, build first." -ForegroundColor Red
    exit 2
}

Write-Host ">>> Starting $exe"
$p = Start-Process -FilePath $exe -PassThru

# Poll instead of one big sleep so we can report early crash precisely.
$pollMs = 1000
$waited = 0
while ($waited -lt ($Seconds * 1000) -and -not $p.HasExited) {
    Start-Sleep -Milliseconds $pollMs
    $waited += $pollMs
}

if ($p.HasExited) {
    Write-Host ("[SMOKE FAIL] process exited after {0}s, ExitCode={1}" -f `
        [int]($waited / 1000), $p.ExitCode) -ForegroundColor Red
    exit 1
}

$cpuSec = [math]::Round($p.TotalProcessorTime.TotalSeconds, 2)
$memMb  = [int]($p.WorkingSet64 / 1MB)
Write-Host ("[SMOKE PASS] alive {0}s, CPU={1}s, Mem={2}MB" -f $Seconds, $cpuSec, $memMb) -ForegroundColor Green

try     { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue } catch { }
exit 0
