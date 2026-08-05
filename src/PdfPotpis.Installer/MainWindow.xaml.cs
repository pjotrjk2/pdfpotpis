using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace PdfPotpis.Installer;

public partial class MainWindow : Window
{
    private enum WizardStep
    {
        Folder,
        Progress,
        Done
    }

    private WizardStep _step = WizardStep.Folder;
    private bool _installing;

    public MainWindow()
    {
        InitializeComponent();
        FolderBox.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PDFPotpis");
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        // FolderBrowserDialog is WinForms; use simple SaveFileDialog-style workaround via OpenFolder if available.
        var dialog = new OpenFolderDialog
        {
            Title = "Izaberite folder instalacije",
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(FolderBox.Text) && Directory.Exists(FolderBox.Text))
        {
            dialog.InitialDirectory = FolderBox.Text;
        }

        if (dialog.ShowDialog(this) == true)
        {
            FolderBox.Text = dialog.FolderName;
        }
    }

    private async void Primary_Click(object sender, RoutedEventArgs e)
    {
        if (_step == WizardStep.Done)
        {
            Close();
            return;
        }

        if (_step == WizardStep.Folder)
        {
            string target = FolderBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(target))
            {
                MessageBox.Show(this, "Unesite folder instalacije.", "PDFPotpis",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _step = WizardStep.Progress;
            StepFolder.Visibility = Visibility.Collapsed;
            StepProgress.Visibility = Visibility.Visible;
            BtnPrimary.IsEnabled = false;
            BtnCancel.IsEnabled = false;
            await RunInstallAsync(target);
            return;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (_installing)
        {
            return;
        }

        Close();
    }

    private async Task RunInstallAsync(string targetDir)
    {
        _installing = true;
        ProgressTitle.Text = "Instalacija u toku…";
        ProgressDetail.Text = "Kopiranje fajlova…";
        ProgressBar.Value = 0;

        try
        {
            string payload = ResolvePayloadDirectory();
            if (!Directory.Exists(payload))
            {
                throw new DirectoryNotFoundException(
                    "Nedostaje AppPayload. Pokrenite scripts\\build-installer.ps1 da pripremite instalaciju.");
            }

            Directory.CreateDirectory(targetDir);
            string[] files = Directory.GetFiles(payload, "*", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                throw new InvalidOperationException("AppPayload je prazan.");
            }

            for (int i = 0; i < files.Length; i++)
            {
                string source = files[i];
                string relative = Path.GetRelativePath(payload, source);
                string dest = Path.Combine(targetDir, relative);
                string? destDir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                await Task.Run(() => File.Copy(source, dest, overwrite: true));
                ProgressBar.Value = (i + 1) * 100.0 / files.Length;
                ProgressDetail.Text = relative;
            }

            string exePath = Path.Combine(targetDir, "PdfPotpis.exe");
            bool ok = File.Exists(exePath);
            if (ok)
            {
                ProgressDetail.Text = "Registracija PDF asocijacije…";
                FileAssociation.Register(exePath);
                ProgressDetail.Text = "Registracija u Apps & features…";
                UninstallRegistration.Register(targetDir, exePath);
            }

            CreateStartMenuShortcut(exePath);

            _step = WizardStep.Done;
            ProgressTitle.Text = "Instalacija završena";
            ProgressBar.Value = 100;
            ValidationText.Visibility = Visibility.Visible;
            if (ok)
            {
                ValidationText.Text =
                    $"Uspeh: PDFPotpis je instaliran u:{Environment.NewLine}{targetDir}" +
                    $"{Environment.NewLine}{Environment.NewLine}" +
                    "Dostupan je u „Otvori pomoću” za PDF i u Podešavanja → Aplikacije.";
                ValidationText.Foreground = System.Windows.Media.Brushes.DarkGreen;
                ProgressDetail.Text = "Validacija: PdfPotpis.exe je pronađen.";
            }
            else
            {
                ValidationText.Text = "Upozorenje: PdfPotpis.exe nije pronađen nakon kopiranja.";
                ValidationText.Foreground = System.Windows.Media.Brushes.DarkRed;
            }

            BtnPrimary.Content = "Završi";
            BtnPrimary.IsEnabled = true;
            BtnCancel.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ProgressTitle.Text = "Instalacija nije uspela";
            ProgressDetail.Text = ex.Message;
            ValidationText.Visibility = Visibility.Visible;
            ValidationText.Text = "Validacija: instalacija nije završena uspešno.";
            ValidationText.Foreground = System.Windows.Media.Brushes.DarkRed;
            BtnPrimary.Content = "Zatvori";
            BtnPrimary.IsEnabled = true;
            BtnCancel.Visibility = Visibility.Collapsed;
            _step = WizardStep.Done;
        }
        finally
        {
            _installing = false;
        }
    }

    private static string ResolvePayloadDirectory()
    {
        string baseDir = AppContext.BaseDirectory;
        string beside = Path.Combine(baseDir, "AppPayload");
        if (Directory.Exists(beside))
        {
            return beside;
        }

        // Dev fallback: published app output
        string dev = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "PdfPotpis", "bin", "Release", "net9.0-windows", "win-x64", "publish"));
        return dev;
    }

    private static void CreateStartMenuShortcut(string exePath)
    {
        try
        {
            string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            string linkDir = Path.Combine(programs, "PDFPotpis");
            Directory.CreateDirectory(linkDir);
            string linkPath = Path.Combine(linkDir, "PDFPotpis.lnk");

            // Minimal shortcut via PowerShell (no extra COM package).
            string ps =
                "$ws = New-Object -ComObject WScript.Shell; " +
                "$s = $ws.CreateShortcut('" + linkPath.Replace("'", "''") + "'); " +
                "$s.TargetPath = '" + exePath.Replace("'", "''") + "'; " +
                "$s.WorkingDirectory = '" + Path.GetDirectoryName(exePath)!.Replace("'", "''") + "'; " +
                "$s.Save()";

            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -Command \"" + ps.Replace("\"", "\\\"") + "\"",
                UseShellExecute = false,
                CreateNoWindow = true
            })?.WaitForExit(5000);
        }
        catch
        {
            // Shortcut is optional.
        }
    }
}
