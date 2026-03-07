# publish.ps1 — Build self-contained single-file releases for both architectures
# Usage: .\publish.ps1 [-Version "1.0.0"]
# Output: publish/StadiumPA-win-x64.zip, publish/StadiumPA-win-arm64.zip

param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = 'Stop'

$projectDir = Join-Path $PSScriptRoot 'src\StadiumPA'
$projectFile = Join-Path $projectDir 'StadiumPA.csproj'
$mediaDir = Join-Path $PSScriptRoot 'media'
$publishRoot = Join-Path $PSScriptRoot 'publish'
$runtimes = @('win-x64', 'win-arm64')

# Clean previous output
if (Test-Path $publishRoot) {
    Remove-Item $publishRoot -Recurse -Force
}

foreach ($rid in $runtimes) {
    Write-Host "`n=== Building $rid ===" -ForegroundColor Cyan

    $outDir = Join-Path $publishRoot $rid

    dotnet publish $projectFile `
        -c Release `
        -r $rid `
        -p:Version=$Version `
        -o $outDir

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed for $rid"
        exit 1
    }

    # Copy media files alongside the EXE
    $mediaTarget = Join-Path $outDir 'media'
    New-Item -ItemType Directory -Path $mediaTarget -Force | Out-Null
    Copy-Item (Join-Path $mediaDir '*') -Destination $mediaTarget -Recurse

    # Create zip for GitHub Release
    $zipName = "StadiumPA-$rid.zip"
    $zipPath = Join-Path $publishRoot $zipName

    # Zip the folder contents so extraction gives: StadiumPA/StadiumPA.exe, StadiumPA/media/...
    $stageDir = Join-Path $publishRoot "stage-$rid\StadiumPA"
    New-Item -ItemType Directory -Path $stageDir -Force | Out-Null
    Copy-Item "$outDir\*" -Destination $stageDir -Recurse
    Compress-Archive -Path (Join-Path $publishRoot "stage-$rid\StadiumPA") -DestinationPath $zipPath -Force
    Remove-Item (Join-Path $publishRoot "stage-$rid") -Recurse -Force

    Write-Host "  -> $zipPath" -ForegroundColor Green
}

# Clean raw publish folders (keep only zips)
foreach ($rid in $runtimes) {
    Remove-Item (Join-Path $publishRoot $rid) -Recurse -Force
}

Write-Host "`nDone! Release artifacts:" -ForegroundColor Cyan
Get-ChildItem $publishRoot -Filter '*.zip' | ForEach-Object { Write-Host "  $($_.Name) ($([math]::Round($_.Length / 1MB, 1)) MB)" }
