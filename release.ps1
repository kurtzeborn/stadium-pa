# release.ps1 — Tag and push to trigger a GitHub Actions release
# Reads version from .csproj, creates a git tag, and pushes it.
# Usage: .\release.ps1

$ErrorActionPreference = 'Stop'

$projectFile = Join-Path $PSScriptRoot 'src\StadiumPA\StadiumPA.csproj'

# Read version from .csproj
[xml]$csproj = Get-Content $projectFile
$version = $csproj.Project.PropertyGroup[0].Version

if (-not $version) {
    Write-Error "Could not read <Version> from $projectFile"
    exit 1
}

$tag = "v$version"

# Check for uncommitted changes
$status = git status --porcelain
if ($status) {
    Write-Error "Working tree has uncommitted changes. Commit or stash first."
    exit 1
}

# Check if tag already exists
$existingTag = git tag -l $tag
if ($existingTag) {
    Write-Error "Tag '$tag' already exists. Bump the version in the .csproj first."
    exit 1
}

Write-Host "Version: $version" -ForegroundColor Cyan
Write-Host "Tag:     $tag" -ForegroundColor Cyan
Write-Host ""

$confirm = Read-Host "Create tag '$tag' and push to trigger release? (y/n)"
if ($confirm -ne 'y') {
    Write-Host "Cancelled."
    exit 0
}

git tag $tag
git push origin main --tags

Write-Host ""
Write-Host "Done! GitHub Actions will build and publish the release." -ForegroundColor Green
Write-Host "Watch progress at: $(git remote get-url origin)/actions" -ForegroundColor Cyan
