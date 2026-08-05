using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace PdfPotpis.Installer;

/// <summary>
/// Registers the app under the current user's "Apps &amp; features" list
/// and writes Uninstall.ps1 into the install folder.
/// </summary>
internal static class UninstallRegistration
{
    public const string UninstallKeyName = "PDFPotpis";
    private const string UninstallScriptName = "Uninstall.ps1";

    public static void Register(string installDir, string exePath)
    {
        string scriptPath = Path.Combine(installDir, UninstallScriptName);
        File.WriteAllText(scriptPath, BuildUninstallScript(), Encoding.UTF8);

        string version = "1.0.0";
        try
        {
            version = FileVersionInfo.GetVersionInfo(exePath).ProductVersion?
                .Split('+')[0]
                .Trim()
                ?? version;
        }
        catch
        {
            // keep fallback
        }

        long sizeKb = 0;
        try
        {
            var info = new FileInfo(exePath);
            sizeKb = Math.Max(1, info.Length / 1024);
        }
        catch
        {
            // optional
        }

        string uninstallCmd =
            $"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"";

        using RegistryKey key = Registry.CurrentUser.CreateSubKey(
            $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{UninstallKeyName}")!;

        key.SetValue("DisplayName", "PDFPotpis");
        key.SetValue("DisplayVersion", version);
        key.SetValue("Publisher", "PDFPotpis");
        key.SetValue("InstallLocation", installDir);
        key.SetValue("DisplayIcon", $"\"{exePath}\",0");
        key.SetValue("UninstallString", uninstallCmd);
        key.SetValue("QuietUninstallString", uninstallCmd);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        if (sizeKb > 0)
        {
            key.SetValue("EstimatedSize", (int)Math.Min(int.MaxValue, sizeKb), RegistryValueKind.DWord);
        }
        key.SetValue("URLInfoAbout", "https://pdfpotpis.petar.website/");
        key.SetValue("HelpLink", "https://github.com/pjotrjk2/pdfpotpis/issues");
    }

    public static void UnregisterArp()
    {
        Registry.CurrentUser.DeleteSubKeyTree(
            $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{UninstallKeyName}",
            throwOnMissingSubKey: false);
    }

    private static string BuildUninstallScript() => """
# PDFPotpis uninstall (per-user)
$ErrorActionPreference = 'Stop'
$installDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Remove Start Menu shortcut
$linkDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\PDFPotpis'
if (Test-Path $linkDir) { Remove-Item -Recurse -Force $linkDir }

# File association / Open with
Remove-Item -Recurse -Force 'HKCU:\Software\Classes\PDFPotpis.Document' -ErrorAction SilentlyContinue
try {
  Remove-ItemProperty -Path 'HKCU:\Software\Classes\.pdf\OpenWithProgids' -Name 'PDFPotpis.Document' -ErrorAction SilentlyContinue
} catch {}
Remove-Item -Recurse -Force 'HKCU:\Software\Classes\Applications\PdfPotpis.exe' -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force 'HKCU:\Software\PDFPotpis' -ErrorAction SilentlyContinue
try {
  if ($null -ne (Get-ItemProperty -Path 'HKCU:\Software\RegisteredApplications' -Name 'PDFPotpis' -ErrorAction SilentlyContinue)) {
    Remove-ItemProperty -Path 'HKCU:\Software\RegisteredApplications' -Name 'PDFPotpis' -ErrorAction SilentlyContinue
  }
} catch {}

# Apps & features entry
Remove-Item -Recurse -Force 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\PDFPotpis' -ErrorAction SilentlyContinue

# Refresh shell associations
try {
  Add-Type -Namespace Shell32 -Name Native -MemberDefinition '[DllImport("shell32.dll")] public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);'
  [Shell32.Native]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)
} catch {}

# Delete install files (script lives inside the folder — delete from a delayed cmd)
$cmd = @"
@echo off
timeout /t 1 /nobreak >nul
rmdir /s /q "$installDir"
"@
$tmp = Join-Path $env:TEMP ("pdfpotpis-uninstall-" + [guid]::NewGuid().ToString('N') + ".cmd")
Set-Content -Path $tmp -Value $cmd -Encoding ASCII
Start-Process -FilePath $tmp -WindowStyle Hidden
""";
}