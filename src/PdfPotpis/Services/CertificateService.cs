using System.Security.Cryptography.X509Certificates;
using System.Windows;

namespace PdfPotpis.Services;

/// <summary>
/// Loads signing certificates from the Windows store (including smart-card / Lična karta keys).
/// </summary>
public sealed class CertificateService
{
    public X509Certificate2? PickSigningCertificate(Window owner)
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

        X509Certificate2Collection candidates = store.Certificates
            .Find(X509FindType.FindByTimeValid, DateTime.Now, validOnly: false)
            .Find(X509FindType.FindByKeyUsage, X509KeyUsageFlags.DigitalSignature, validOnly: false);

        if (candidates.Count == 0)
        {
            candidates = store.Certificates.Find(X509FindType.FindByTimeValid, DateTime.Now, validOnly: false);
        }

        if (candidates.Count == 0)
        {
            MessageBox.Show(owner,
                "Nije pronađen nijedan sertifikat u Windows skladištu (CurrentUser\\My).",
                "PDFPotpis",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return null;
        }

        var picker = new CertificatePickerWindow(candidates.Cast<X509Certificate2>())
        {
            Owner = owner
        };

        return picker.ShowDialog() == true ? picker.SelectedCertificate : null;
    }

    public static string GetDisplayName(X509Certificate2 certificate)
    {
        string? simple = certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
        if (!string.IsNullOrWhiteSpace(simple))
        {
            return simple;
        }

        return certificate.Subject;
    }

    public static (string GivenName, string Surname) SplitName(X509Certificate2 certificate)
    {
        string display = GetDisplayName(certificate);
        string[] parts = display.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return (display, string.Empty);
        }

        if (parts.Length == 1)
        {
            return (parts[0], string.Empty);
        }

        return (parts[^1], string.Join(' ', parts.Take(parts.Length - 1)));
    }
}
