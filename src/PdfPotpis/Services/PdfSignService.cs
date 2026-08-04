using System.IO;
using System.Security.Cryptography.X509Certificates;
using iText.Bouncycastle.X509;
using iText.Commons.Bouncycastle.Cert;
using iText.Forms.Fields.Properties;
using iText.Forms.Form.Element;
using iText.Kernel.Pdf;
using iText.Signatures;
using Org.BouncyCastle.X509;
using PdfPotpis.Models;

namespace PdfPotpis.Services;

/// <summary>
/// Creates a PKCS#7 / CMS digital signature with a visible appearance on the chosen page.
/// </summary>
public sealed class PdfSignService
{
    public byte[] Sign(
        byte[] pdfBytes,
        X509Certificate2 certificate,
        SignaturePlacement placement,
        string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        ArgumentNullException.ThrowIfNull(certificate);
        ArgumentNullException.ThrowIfNull(placement);

        if (!certificate.HasPrivateKey)
        {
            throw new InvalidOperationException("Izabrani sertifikat nema privatni ključ.");
        }

        string displayName = CertificateService.GetDisplayName(certificate);
        (string givenName, string surname) = CertificateService.SplitName(certificate);
        string signatureId = certificate.SerialNumber ?? Guid.NewGuid().ToString("N");
        string fieldName = "PdfPotpis_" + Guid.NewGuid().ToString("N")[..8];

        string appearanceText =
            $"Digitalni potpis{Environment.NewLine}" +
            $"Ime: {givenName}{Environment.NewLine}" +
            $"Prezime: {surname}{Environment.NewLine}" +
            $"ID: {signatureId}";

        var signedAppearanceText = new SignedAppearanceText()
            .SetSignedBy(displayName)
            .SetReasonLine(reason ?? "Digitalni potpis dokumenta")
            .SetLocationLine($"ID: {signatureId}");

        var appearance = new SignatureFieldAppearance(fieldName)
            .SetContent(appearanceText, signedAppearanceText);

        var rect = new iText.Kernel.Geom.Rectangle(
            placement.PdfX,
            placement.PdfY,
            placement.WidthPts,
            placement.HeightPts);

        var signerProperties = new SignerProperties()
            .SetFieldName(fieldName)
            .SetPageNumber(placement.PageIndex + 1)
            .SetPageRect(rect)
            .SetReason(reason ?? "Digitalni potpis dokumenta")
            .SetLocation("Republika Srbija")
            .SetSignatureCreator("PDFPotpis")
            .SetSignatureAppearance(appearance);

        IX509Certificate[] chain = BuildChain(certificate);
        IExternalSignature externalSignature = new X509Certificate2Signature(certificate);

        using var input = new MemoryStream(pdfBytes);
        using var reader = new PdfReader(input);
        using var output = new MemoryStream();
        var stampingProperties = new StampingProperties().UseAppendMode();

        var pdfSigner = new PdfSigner(reader, output, stampingProperties);
        pdfSigner.SetSignerProperties(signerProperties);
        pdfSigner.SignDetached(externalSignature, chain, null, null, null, 0, PdfSigner.CryptoStandard.CMS);

        return output.ToArray();
    }

    private static IX509Certificate[] BuildChain(X509Certificate2 certificate)
    {
        var parser = new X509CertificateParser();
        Org.BouncyCastle.X509.X509Certificate? bcCert = parser.ReadCertificate(certificate.RawData);
        if (bcCert is null)
        {
            throw new InvalidOperationException("Neuspešna konverzija sertifikata.");
        }

        var bcAdapterCert = new X509CertificateBC(bcCert);
        return [bcAdapterCert];
    }
}
