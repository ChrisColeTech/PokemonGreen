# Batch Export All Pokemon Models (Parallel)
# Usage: .\export_all.ps1 [-ArcDir <path>] [-OutputDir <path>] [-Jobs <N>]
# Runs multiple model exports in parallel for maximum throughput.

param(
    [string]$ArcDir = "D:\Projects\PokemonGreen\src\PokemonGreen.Tests\violet-dump\arc",
    [string]$OutputDir = "D:\Projects\PokemonGreen\src\PokemonGreen.Tests\violet-dump\output\all",
    [string]$ExePath = "D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli\publish\SwitchToolboxCli.App.exe",
    [int]$Jobs = 8
)

$ErrorActionPreference = "Continue"

# Build the published EXE if it doesn't exist
if (-not (Test-Path $ExePath)) {
    Write-Host "Building published EXE..." -ForegroundColor Yellow
    dotnet publish "D:\Projects\PokemonGreen\src\PokemonGreen.SwitchToolboxCli\src\SwitchToolboxCli.App" -c Release --nologo -o (Split-Path $ExePath)
}

# Step 1: Get model list
Write-Host "Listing models from archive..." -ForegroundColor Cyan
$rawList = & $ExePath --arc $ArcDir --list 2>$null
$models = $rawList | Where-Object { $_ -match "^\s+\S+\.trmdl$" } | ForEach-Object { $_.Trim() }
$total = $models.Count
Write-Host "  Found $total models. Running $Jobs parallel jobs." -ForegroundColor Green

# Step 2: Export each model in parallel
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$failedFile = Join-Path $OutputDir "_failed_models.txt"
$startTime = Get-Date

# Thread-safe counters
$successCount = [System.Threading.Interlocked]
$counter = [ref]0
$successRef = [ref]0
$failedRef = [ref]0
$skippedRef = [ref]0

$models | ForEach-Object -ThrottleLimit $Jobs -Parallel {
    $model = $_
    $modelName = [System.IO.Path]::GetFileNameWithoutExtension($model)
    $modelOutDir = Join-Path $using:OutputDir $modelName
    $idx = [System.Threading.Interlocked]::Increment($using:counter)
    $pct = [math]::Round(($idx / $using:total) * 100, 1)

    # Skip if already exported
    if (Test-Path (Join-Path $modelOutDir "model.dae")) {
        [System.Threading.Interlocked]::Increment($using:skippedRef) | Out-Null
        return
    }

    # Run export with unique temp files for stdout/stderr
    $tmpOut = Join-Path $using:OutputDir "_tmp_${modelName}_out.log"
    $tmpErr = Join-Path $using:OutputDir "_tmp_${modelName}_err.log"

    try {
        $proc = Start-Process -FilePath $using:ExePath `
            -ArgumentList "--arc",$using:ArcDir,"--model",$model,"-o",$modelOutDir `
            -NoNewWindow -PassThru `
            -RedirectStandardOutput $tmpOut `
            -RedirectStandardError $tmpErr

        $finished = $proc.WaitForExit(300000)
        if (-not $finished) { $proc.Kill() }

        if ($finished -and $proc.ExitCode -eq 0) {
            [System.Threading.Interlocked]::Increment($using:successRef) | Out-Null
            Write-Host "[$idx/$using:total] ($pct%) $modelName OK" -ForegroundColor Green
        } else {
            [System.Threading.Interlocked]::Increment($using:failedRef) | Out-Null
            Write-Host "[$idx/$using:total] ($pct%) $modelName FAIL" -ForegroundColor Red
            $mutex = [System.Threading.Mutex]::new($false, "ExportFailedMutex")
            $mutex.WaitOne() | Out-Null
            $model | Out-File $using:failedFile -Append -Encoding utf8
            $mutex.ReleaseMutex()
        }
    } catch {
        [System.Threading.Interlocked]::Increment($using:failedRef) | Out-Null
        Write-Host "[$idx/$using:total] ($pct%) $modelName CRASH" -ForegroundColor Red
    } finally {
        Remove-Item $tmpOut, $tmpErr -ErrorAction SilentlyContinue
    }
}

# Step 3: Summary
$elapsed = (Get-Date) - $startTime
$summary = @"

=== Batch Export Complete ===
  Total:    $total
  Success:  $($successRef.Value)
  Failed:   $($failedRef.Value)
  Skipped:  $($skippedRef.Value) (already exported)
  Parallel: $Jobs jobs
  Elapsed:  $([math]::Round($elapsed.TotalMinutes, 1)) minutes
  Output:   $OutputDir
"@

Write-Host $summary -ForegroundColor Cyan
$summary | Out-File (Join-Path $OutputDir "_batch_results.log") -Encoding utf8
