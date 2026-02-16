param(
    [Parameter(Mandatory = $true)]
    [string]$SampleGarcDir,

    [Parameter(Mandatory = $true)]
    [string]$OutputDir,

    [string]$ProjectRoot = "D:/Projects/PokemonGreen/src/PokemonGreen.OhanaCli",

    [int]$Limit = 20
)

$ErrorActionPreference = "Stop"

$appProject = Join-Path $ProjectRoot "src/OhanaCli.App/OhanaCli.App.csproj"
$sampleGarcs = Get-ChildItem -Path $SampleGarcDir -Filter *.garc -File | Sort-Object Name

if ($sampleGarcs.Count -eq 0) {
    throw "No .garc files found in '$SampleGarcDir'."
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

Write-Host "Running parity checks on $($sampleGarcs.Count) sample GARCs"

foreach ($garc in $sampleGarcs) {
    $garcOut = Join-Path $OutputDir $garc.BaseName
    New-Item -ItemType Directory -Path $garcOut -Force | Out-Null

    Write-Host "[PARITY] Diagnose $($garc.Name)"
    dotnet run --project $appProject -- diagnose $garc.FullName --start 0 --end 10 | Tee-Object -FilePath (Join-Path $garcOut "diagnose.txt")

    Write-Host "[PARITY] Convert $($garc.Name) with animation diagnostics"
    dotnet run --project $appProject -- convert $garc.FullName -o $garcOut -f dae -a 0 -n $Limit --diag-anim 2>&1 |
        Tee-Object -FilePath (Join-Path $garcOut "convert_diag.txt")
}

Write-Host "Parity checks complete. Logs saved under '$OutputDir'."
