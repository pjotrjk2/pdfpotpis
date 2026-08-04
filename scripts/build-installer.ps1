#Requires -Version 5.1
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $root "src\PdfPotpis\PdfPotpis.csproj"
$installerProject = Join-Path $root "src\PdfPotpis.Installer\PdfPotpis.Installer.csproj"
$publishDir = Join-Path $root "src\PdfPotpis\bin\Release\net9.0-windows\win-x64\publish"
$payloadDir = Join-Path $root "src\PdfPotpis.Installer\AppPayload"
$outputDir = Join-Path $root "installer\output"

Write-Host "==> Publishing PDFPotpis (win-x64, self-contained)..."
dotnet publish $appProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish (app) failed" }

Write-Host "==> Staging AppPayload for installer project..."
if (Test-Path $payloadDir) { Remove-Item $payloadDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $payloadDir | Out-Null
Copy-Item -Path (Join-Path $publishDir "*") -Destination $payloadDir -Recurse -Force

Write-Host "==> Publishing installer wizard..."
if (Test-Path $outputDir) { Remove-Item $outputDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
dotnet publish $installerProject -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $outputDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish (installer) failed" }

# Ensure payload sits next to setup exe (csproj CopyToOutput should already do this)
$outPayload = Join-Path $outputDir "AppPayload"
if (-not (Test-Path $outPayload)) {
    New-Item -ItemType Directory -Force -Path $outPayload | Out-Null
    Copy-Item -Path (Join-Path $publishDir "*") -Destination $outPayload -Recurse -Force
}

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($iscc) {
    Write-Host "==> Also building Inno Setup package..."
    & $iscc (Join-Path $root "installer\PdfPotpis.iss")
}

Write-Host ""
Write-Host "Done. Run installer:"
Write-Host "  $outputDir\PDFPotpis-Setup.exe"
