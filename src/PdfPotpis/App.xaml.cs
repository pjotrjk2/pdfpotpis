using System.IO;
using System.Windows;

namespace PdfPotpis;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var main = new MainWindow();
        MainWindow = main;
        main.Show();

        if (e.Args.Length > 0)
        {
            string path = e.Args[0].Trim('"');
            if (File.Exists(path) &&
                path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                main.OpenPath(path);
            }
        }
    }
}
