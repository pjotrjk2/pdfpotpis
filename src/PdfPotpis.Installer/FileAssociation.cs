using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PdfPotpis.Installer;

/// <summary>
/// Registers PDFPotpis under the current user so it appears in "Open with" for .pdf
/// without forcing itself as the default handler.
/// </summary>
internal static class FileAssociation
{
    private const string ProgId = "PDFPotpis.Document";
    private const string AppKeyName = "PdfPotpis.exe";

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private const uint SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    public static void Register(string exePath)
    {
        string command = $"\"{exePath}\" \"%1\"";
        string icon = $"\"{exePath}\",0";

        using (RegistryKey progId = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}")!)
        {
            progId.SetValue(null, "PDF dokument (PDFPotpis)");
            using (RegistryKey iconKey = progId.CreateSubKey("DefaultIcon")!)
            {
                iconKey.SetValue(null, icon);
            }

            using (RegistryKey open = progId.CreateSubKey(@"shell\open\command")!)
            {
                open.SetValue(null, command);
            }
        }

        using (RegistryKey openWith = Registry.CurrentUser.CreateSubKey(@"Software\Classes\.pdf\OpenWithProgids")!)
        {
            openWith.SetValue(ProgId, "", RegistryValueKind.String);
        }

        using (RegistryKey app = Registry.CurrentUser.CreateSubKey($@"Software\Classes\Applications\{AppKeyName}")!)
        {
            app.SetValue("FriendlyAppName", "PDFPotpis");
            using (RegistryKey open = app.CreateSubKey(@"shell\open\command")!)
            {
                open.SetValue(null, command);
            }

            using (RegistryKey types = app.CreateSubKey("SupportedTypes")!)
            {
                types.SetValue(".pdf", "", RegistryValueKind.String);
            }
        }

        using (RegistryKey caps = Registry.CurrentUser.CreateSubKey(@"Software\PDFPotpis\Capabilities")!)
        {
            caps.SetValue("ApplicationName", "PDFPotpis");
            caps.SetValue("ApplicationDescription", "Lokalno potpisivanje PDF dokumenata");
            using (RegistryKey fileAssoc = caps.CreateSubKey("FileAssociations")!)
            {
                fileAssoc.SetValue(".pdf", ProgId);
            }
        }

        using (RegistryKey registered = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications")!)
        {
            registered.SetValue("PDFPotpis", @"Software\PDFPotpis\Capabilities");
        }

        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
    }

    public static void Unregister()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{ProgId}", throwOnMissingSubKey: false);
            using (RegistryKey? openWith = Registry.CurrentUser.OpenSubKey(@"Software\Classes\.pdf\OpenWithProgids", writable: true))
            {
                openWith?.DeleteValue(ProgId, throwOnMissingValue: false);
            }

            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\Applications\{AppKeyName}", throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\PDFPotpis", throwOnMissingSubKey: false);
            using (RegistryKey? registered = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", writable: true))
            {
                registered?.DeleteValue("PDFPotpis", throwOnMissingValue: false);
            }

            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}
