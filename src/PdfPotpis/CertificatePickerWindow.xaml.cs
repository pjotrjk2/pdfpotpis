using System.Security.Cryptography.X509Certificates;
using System.Windows;

namespace PdfPotpis;

public partial class CertificatePickerWindow : Window
{
    public X509Certificate2? SelectedCertificate { get; private set; }

    private readonly List<X509Certificate2> _certificates;

    public CertificatePickerWindow(IEnumerable<X509Certificate2> certificates)
    {
        InitializeComponent();
        _certificates = certificates.ToList();
        CertList.ItemsSource = _certificates.Select(c => new CertItem(c)).ToList();
        if (CertList.Items.Count > 0)
        {
            CertList.SelectedIndex = 0;
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (CertList.SelectedItem is not CertItem item)
        {
            MessageBox.Show(this, "Izaberite sertifikat.", "PDFPotpis",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectedCertificate = item.Certificate;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private sealed class CertItem
    {
        public CertItem(X509Certificate2 certificate)
        {
            Certificate = certificate;
            DisplayName = certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                DisplayName = certificate.Subject;
            }

            Issuer = certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: true)
                     ?? certificate.Issuer;
            ValidUntil = certificate.NotAfter.ToString("dd.MM.yyyy");
            Serial = certificate.SerialNumber ?? string.Empty;
        }

        public X509Certificate2 Certificate { get; }

        public string DisplayName { get; }

        public string Issuer { get; }

        public string ValidUntil { get; }

        public string Serial { get; }
    }
}
