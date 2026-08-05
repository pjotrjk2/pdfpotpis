#Requires -Version 5.1
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $root "src\PdfPotpis\PdfPotpis.csproj"
$installerProject = Join-Path $root "src\PdfPotpis.Installer\PdfPotpis.Installer.csproj"
$publishDir = Join-Path $root "src\PdfPotpis\bin\Release\net9.0-windows\win-x64\publish"
$payloadDir = Join-Path $root "src\PdfPotpis.Installer\AppPayload"
$outputDir = Join-Path $root "installer\output"

$singleFileArgs = @(
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:EnableCompressionInSingleFile=true",
    "-p:DebugType=None",
    "-p:DebugSymbols=false"
)

Write-Host "==> Publishing PDFPotpis single-file (win-x64, self-contained)..."
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $appProject @singleFileArgs -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish (app) failed" }

$appExe = Join-Path $publishDir "PdfPotpis.exe"
if (-not (Test-Path $appExe)) { throw "Published app not found: $appExe" }

Write-Host "==> Staging AppPayload (single PdfPotpis.exe)..."
if (Test-Path $payloadDir) { Remove-Item $payloadDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $payloadDir | Out-Null
# Keep folder tracked
Set-Content -Path (Join-Path $payloadDir ".gitkeep") -Value "" -Encoding ascii
Copy-Item -Path $appExe -Destination (Join-Path $payloadDir "PdfPotpis.exe") -Force

Write-Host "==> Publishing single-file installer wizard..."
if (Test-Path $outputDir) { Remove-Item $outputDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
dotnet publish $installerProject @singleFileArgs `
    -p:IncludeAllContentForSelfExtract=true `
    -o $outputDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish (installer) failed" }

$setupExe = Join-Path $outputDir "PDFPotpis-Setup.exe"
if (-not (Test-Path $setupExe)) { throw "Installer not found: $setupExe" }

Write-Host "==> Staging portable build..."
$portableExe = Join-Path $outputDir "PDFPotpis-Portable.exe"
Copy-Item -Path $appExe -Destination $portableExe -Force

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($iscc) {
    Write-Host "==> Also building Inno Setup package..."
    & $iscc (Join-Path $root "installer\PdfPotpis.iss")
}

Write-Host ""
Write-Host "Done."
Write-Host "  Installer: $setupExe"
Write-Host "  Portable:  $portableExe"
