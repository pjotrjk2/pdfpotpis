using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Windows;

namespace PdfPotpis.Services;

/// <summary>
/// Loads signing certificates from the Windows store (including smart-card / Lična karta keys).
/// </summary>
public sealed class CertificateService
{
    private const string OidGivenName = "2.5.4.42";
    private const string OidSurname = "2.5.4.4";
    private const string OidSerialNumber = "2.5.4.5";

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
        (string givenName, string surname) = GetGivenNameAndSurname(certificate);
        string combined = $"{givenName} {surname}".Trim();
        if (!string.IsNullOrWhiteSpace(combined))
        {
            return combined;
        }

        string? simple = certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
        if (!string.IsNullOrWhiteSpace(simple))
        {
            return simple;
        }

        return certificate.Subject;
    }

    /// <summary>
    /// Reads givenName / surname from the subject DN (present on MUP certificates).
    /// Falls back to splitting the common name as "Ime Prezime".
    /// </summary>
    public static (string GivenName, string Surname) GetGivenNameAndSurname(X509Certificate2 certificate)
    {
        string? given = GetSubjectRdn(certificate, OidGivenName);
        string? surname = GetSubjectRdn(certificate, OidSurname);

        if (!string.IsNullOrWhiteSpace(given) || !string.IsNullOrWhiteSpace(surname))
        {
            return (given?.Trim() ?? string.Empty, surname?.Trim() ?? string.Empty);
        }

        string display = certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false)
                         ?? certificate.Subject;
        string[] parts = display.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return (display, string.Empty);
        }

        if (parts.Length == 1)
        {
            return (parts[0], string.Empty);
        }

        return (parts[0], string.Join(' ', parts.Skip(1)));
    }

    /// <summary>
    /// Subject SERIALNUMBER (typically JMBG on MUP certificates).
    /// </summary>
    public static string GetPersonalId(X509Certificate2 certificate)
    {
        return GetSubjectRdn(certificate, OidSerialNumber)?.Trim() ?? string.Empty;
    }

    public static string GetCertificateSerial(X509Certificate2 certificate)
    {
        return certificate.SerialNumber ?? string.Empty;
    }

    private static string? GetSubjectRdn(X509Certificate2 certificate, string oid)
    {
        foreach (X500RelativeDistinguishedName rdn in certificate.SubjectName.EnumerateRelativeDistinguishedNames())
        {
            if (rdn.HasMultipleElements)
            {
                continue;
            }

            Oid type = rdn.GetSingleElementType();
            if (string.Equals(type.Value, oid, StringComparison.Ordinal))
            {
                return rdn.GetSingleElementValue();
            }
        }

        return null;
    }
}
