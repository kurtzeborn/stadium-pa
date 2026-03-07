# release.ps1 — Tag and push to trigger a GitHub Actions release
# Reads version from .csproj, creates a git tag, and pushes it.
# If the version is already released, offers to bump and release the next version.
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

# Check if tag already exists — offer to bump if so
$existingTag = git tag -l $tag
if ($existingTag) {
    Write-Host "Tag '$tag' already exists." -ForegroundColor Yellow

    # Parse and bump patch version
    $parts = $version -split '\.'
    $major = [int]$parts[0]
    $minor = [int]$parts[1]
    $patch = if ($parts.Length -ge 3) { [int]$parts[2] } else { 0 }
    $patch++
    $newVersion = "$major.$minor.$patch"
    $newTag = "v$newVersion"

    Write-Host ""
    Write-Host "Bump to $newVersion and release as '$newTag'?" -ForegroundColor Cyan
    $bump = Read-Host "(y/n)"
    if ($bump -ne 'y') {
        Write-Host "Cancelled."
        exit 0
    }

    # Update .csproj with new version
    $content = Get-Content $projectFile -Raw
    $content = $content -replace "<Version>$([regex]::Escape($version))</Version>", "<Version>$newVersion</Version>"
    Set-Content $projectFile -Value $content -NoNewline

    # Commit the version bump
    git add $projectFile
    git commit -m "Bump version to $newVersion"

    $version = $newVersion
    $tag = $newTag

    Write-Host "Version bumped to $newVersion and committed." -ForegroundColor Green
    Write-Host ""
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
