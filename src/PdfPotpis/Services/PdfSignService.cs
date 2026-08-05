using System.IO;
using System.Security.Cryptography.X509Certificates;
using iText.Bouncycastle.X509;
using iText.Commons.Bouncycastle.Cert;
using iText.Forms.Form.Element;
using iText.IO.Font;
using iText.Kernel.Font;
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
    public const float DefaultStampWidthPts = 260f;
    public const float DefaultStampHeightPts = 48f;

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

        string fieldName = "PdfPotpis_" + Guid.NewGuid().ToString("N")[..8];
        string appearanceText = BuildAppearanceText(certificate, DateTime.Now);

        var appearance = new SignatureFieldAppearance(fieldName)
            .SetContent(appearanceText)
            .SetFont(CreateAppearanceFont())
            .SetFontSize(8);

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

    public static string BuildAppearanceText(X509Certificate2 certificate, DateTime signedAt)
    {
        (string givenName, string surname) = CertificateService.GetGivenNameAndSurname(certificate);
        string personalId = CertificateService.GetPersonalId(certificate);
        string certSerial = CertificateService.GetCertificateSerial(certificate);

        string nameLine = $"{givenName} {surname}".Trim();
        if (!string.IsNullOrWhiteSpace(personalId))
        {
            nameLine = $"{nameLine} {personalId}".Trim();
        }

        nameLine = $"{nameLine} Sign".Trim();

        string timeLine = signedAt.ToString("HH:mm:ss dd.MM.yyyy.");
        string idLine = string.IsNullOrWhiteSpace(certSerial) ? "ID:" : $"ID: {certSerial}";

        return $"{nameLine}{Environment.NewLine}{timeLine}{Environment.NewLine}{idLine}";
    }

    private static PdfFont CreateAppearanceFont()
    {
        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "ARIAL.TTF"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "calibri.ttf"),
        ];

        foreach (string path in candidates)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            return PdfFontFactory.CreateFont(
                path,
                PdfEncodings.IDENTITY_H,
                PdfFontFactory.EmbeddingStrategy.PREFER_EMBEDDED);
        }

        return PdfFontFactory.CreateFont();
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
