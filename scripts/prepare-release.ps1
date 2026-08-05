#Requires -Version 5.1
<#
.SYNOPSIS
  Bump/set the product version, build single-file installer + portable, stage downloads + SHAs,
  and optionally create a GitHub Release.

.EXAMPLE
  .\scripts\prepare-release.ps1 -Bump patch
  .\scripts\prepare-release.ps1 -Version 1.2.0 -CreateRelease
  .\scripts\prepare-release.ps1
#>
param(
    [string]$Version,
    [ValidateSet("major", "minor", "patch")]
    [string]$Bump,
    [switch]$CreateRelease,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$propsPath = Join-Path $root "Directory.Build.props"
$issPath = Join-Path $root "installer\PdfPotpis.iss"
$appManifest = Join-Path $root "src\PdfPotpis\app.manifest"
$installerManifest = Join-Path $root "src\PdfPotpis.Installer\app.manifest"
$indexPath = Join-Path $root "website\index.html"
$downloadsDir = Join-Path $root "website\downloads"
$setupName = "PDFPotpis-Setup.exe"
$portableName = "PDFPotpis-Portable.exe"
$setupZipName = "PDFPotpis-Setup.zip"
$portableZipName = "PDFPotpis-Portable.zip"
$installerOutput = Join-Path $root "installer\output\$setupName"
$portableOutput = Join-Path $root "installer\output\$portableName"
$buildScript = Join-Path $PSScriptRoot "build-installer.ps1"
$utf8 = New-Object System.Text.UTF8Encoding $false

function Write-TextFile([string]$Path, [string]$Content) {
    [System.IO.File]::WriteAllText($Path, $Content, $utf8)
}

function Read-TextFile([string]$Path) {
    return [System.IO.File]::ReadAllText($Path, $utf8)
}

function Get-ProductVersion {
    [xml]$xml = Read-TextFile $propsPath
    $node = $xml.Project.PropertyGroup.Version
    if (-not $node) { throw "Version not found in Directory.Build.props" }
    return [string]$node
}

function Set-ProductVersion([string]$NewVersion) {
    if ($NewVersion -notmatch '^\d+\.\d+\.\d+$') {
        throw "Version must be MAJOR.MINOR.PATCH (got '$NewVersion')"
    }

    $fourPart = "$NewVersion.0"

    $props = Read-TextFile $propsPath
    $props = [regex]::Replace($props, '(<Version>)[^<]+(</Version>)', "`${1}$NewVersion`${2}")
    Write-TextFile $propsPath $props

    $iss = Read-TextFile $issPath
    $iss = [regex]::Replace($iss, '(#define MyAppVersion ")[^"]+(")', "`${1}$NewVersion`${2}")
    Write-TextFile $issPath $iss

    foreach ($manifest in @($appManifest, $installerManifest)) {
        $text = Read-TextFile $manifest
        $text = [regex]::Replace($text, '(assemblyIdentity\s+version=")[^"]+(")', "`${1}$fourPart`${2}")
        Write-TextFile $manifest $text
    }

    $html = Read-TextFile $indexPath
    $html = [regex]::Replace($html, '<!--VERSION-->.*?<!--/VERSION-->', "<!--VERSION-->$NewVersion<!--/VERSION-->")
    $html = [regex]::Replace($html, '("softwareVersion"\s*:\s*")[^"]*(")', "`${1}$NewVersion`${2}")
    Write-TextFile $indexPath $html
}

function Bump-Version([string]$Current, [string]$Part) {
    $parts = $Current.Split('.') | ForEach-Object { [int]$_ }
    if ($parts.Count -ne 3) { throw "Cannot bump version '$Current'" }
    switch ($Part) {
        "major" { $parts[0]++; $parts[1] = 0; $parts[2] = 0 }
        "minor" { $parts[1]++; $parts[2] = 0 }
        "patch" { $parts[2]++ }
    }
    return "{0}.{1}.{2}" -f $parts[0], $parts[1], $parts[2]
}

function Set-SiteSha([string]$Marker, [string]$Sha) {
    $html = Read-TextFile $indexPath
    $html = [regex]::Replace($html, "<!--$Marker-->.*?<!--/$Marker-->", "<!--$Marker-->$Sha<!--/$Marker-->")
    Write-TextFile $indexPath $html
}

function Stage-DownloadZip([string]$SourceExePath, [string]$ZipFileName, [string]$ShaMarker) {
    $exeName = [System.IO.Path]::GetFileName($SourceExePath)
    $zipPath = Join-Path $downloadsDir $ZipFileName
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

    # Compress into a temp folder so the zip root contains only the exe (not full paths)
    $stage = Join-Path ([System.IO.Path]::GetTempPath()) ("pdfpotpis-zip-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Force -Path $stage | Out-Null
    try {
        Copy-Item -Path $SourceExePath -Destination (Join-Path $stage $exeName) -Force
        Compress-Archive -Path (Join-Path $stage $exeName) -DestinationPath $zipPath -CompressionLevel Optimal -Force
    }
    finally {
        Remove-Item -Recurse -Force $stage -ErrorAction SilentlyContinue
    }

    $sha = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $shaFile = Join-Path $downloadsDir "$ZipFileName.sha256"
    Write-TextFile $shaFile "$sha  $ZipFileName`n"
    Set-SiteSha $ShaMarker $sha

    $rawKb = [math]::Round((Get-Item $SourceExePath).Length / 1KB)
    $zipKb = [math]::Round((Get-Item $zipPath).Length / 1KB)
    Write-Host ("  {0}: {1} KB -> {2} KB" -f $ZipFileName, $rawKb, $zipKb)

    return [pscustomobject]@{ Path = $zipPath; ShaFile = $shaFile; Sha = $sha }
}

# --- resolve version ---
$current = Get-ProductVersion
if ($Bump -and $Version) {
    throw "Specify either -Bump or -Version, not both."
}
if ($Bump) {
    $Version = Bump-Version $current $Bump
}
elseif (-not $Version) {
    $Version = $current
}

Write-Host "==> Product version: $Version$(if ($Version -ne $current) { " (was $current)" })"

Set-ProductVersion $Version

# --- build ---
if (-not $SkipBuild) {
    Write-Host "==> Building single-file app + installer..."
    & $buildScript
    if ($LASTEXITCODE -ne 0) { throw "build-installer.ps1 failed" }
}

if (-not (Test-Path $installerOutput)) { throw "Installer not found: $installerOutput" }
if (-not (Test-Path $portableOutput)) { throw "Portable build not found: $portableOutput" }

# --- stage downloads ---
Write-Host "==> Staging zip downloads + SHA checksums..."
New-Item -ItemType Directory -Force -Path $downloadsDir | Out-Null

# Remove stale loose exes from older releases
Remove-Item -Force (Join-Path $downloadsDir $setupName) -ErrorAction SilentlyContinue
Remove-Item -Force (Join-Path $downloadsDir $portableName) -ErrorAction SilentlyContinue
Remove-Item -Force (Join-Path $downloadsDir "$setupName.sha256") -ErrorAction SilentlyContinue
Remove-Item -Force (Join-Path $downloadsDir "$portableName.sha256") -ErrorAction SilentlyContinue

$setup = Stage-DownloadZip $installerOutput $setupZipName "SHA256-SETUP"
$portable = Stage-DownloadZip $portableOutput $portableZipName "SHA256-PORTABLE"

Write-Host ""
Write-Host "Staged:"
Write-Host "  $($setup.Path)"
Write-Host "  SHA-256 (setup zip): $($setup.Sha)"
Write-Host "  $($portable.Path)"
Write-Host "  SHA-256 (portable zip): $($portable.Sha)"

# --- optional GitHub release ---
$assets = @($setup.Path, $setup.ShaFile, $portable.Path, $portable.ShaFile)
if ($CreateRelease) {
    $tag = "v$Version"
    $gh = Get-Command gh -ErrorAction SilentlyContinue
    if (-not $gh) {
        Write-Host ""
        Write-Warning "GitHub CLI (gh) not found. Install it, then run:"
        Write-Host ('  gh release create {0} {1} --title "PDFPotpis {0}" --generate-notes' -f $tag, ($assets -join ' '))
    }
    else {
        Write-Host ""
        Write-Host "==> Creating GitHub release $tag..."
        & gh release view $tag 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Release $tag already exists - uploading/replacing assets..."
            & gh release upload $tag @assets --clobber
            if ($LASTEXITCODE -ne 0) { throw "gh release upload failed" }
        }
        else {
            & gh release create $tag @assets --title "PDFPotpis $tag" --generate-notes
            if ($LASTEXITCODE -ne 0) { throw "gh release create failed" }
        }
        $url = & gh release view $tag --json url -q .url
        Write-Host "Release ready: $url"
    }
}
else {
    Write-Host ""
    Write-Host "Next (when ready to publish):"
    Write-Host "  1. Commit version bumps + site updates"
    Write-Host "  2. Deploy website/ (includes downloads/ artifacts)"
    Write-Host "  3. Re-run with -CreateRelease  (needs GitHub CLI: gh)"
}

Write-Host ""
Write-Host "Done."
