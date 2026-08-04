using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using iText.Signatures;

namespace PdfPotpis.Services;

/// <summary>
/// Signs digests with a Windows / smart-card private key that cannot be exported.
/// iText passes message bytes that must be hashed and signed (not a precomputed hash).
/// </summary>
public sealed class X509Certificate2Signature : IExternalSignature
{
    private readonly X509Certificate2 _certificate;
    private readonly string _digestAlgorithmName;

    public X509Certificate2Signature(X509Certificate2 certificate, string digestAlgorithmName = "SHA-256")
    {
        _certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
        _digestAlgorithmName = digestAlgorithmName;
    }

    public string GetDigestAlgorithmName() => _digestAlgorithmName;

    public string GetSignatureAlgorithmName()
    {
        using RSA? rsa = _certificate.GetRSAPrivateKey();
        if (rsa is not null)
        {
            return "RSA";
        }

        using ECDsa? ecdsa = _certificate.GetECDsaPrivateKey();
        if (ecdsa is not null)
        {
            return "ECDSA";
        }

        throw new InvalidOperationException("Sertifikat nema podržan privatni ključ (RSA/ECDSA).");
    }

    public ISignatureMechanismParams? GetSignatureMechanismParameters() => null;

    public byte[] Sign(byte[] message)
    {
        HashAlgorithmName hashName = _digestAlgorithmName.ToUpperInvariant() switch
        {
            "SHA-256" or "SHA256" => HashAlgorithmName.SHA256,
            "SHA-384" or "SHA384" => HashAlgorithmName.SHA384,
            "SHA-512" or "SHA512" => HashAlgorithmName.SHA512,
            _ => HashAlgorithmName.SHA256
        };

        using RSA? rsa = _certificate.GetRSAPrivateKey();
        if (rsa is not null)
        {
            return rsa.SignData(message, hashName, RSASignaturePadding.Pkcs1);
        }

        using ECDsa? ecdsa = _certificate.GetECDsaPrivateKey();
        if (ecdsa is not null)
        {
            return ecdsa.SignData(message, hashName);
        }

        throw new InvalidOperationException("Sertifikat nema podržan privatni ključ (RSA/ECDSA).");
    }
}
