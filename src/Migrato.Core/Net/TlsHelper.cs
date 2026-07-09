using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Migrato.Core.Net;

public static class TlsHelper
{
    /// <summary>
    /// Vytvoří jednorázový self-signed certifikát pro jednu relaci přenosu.
    /// Důvěra se nezakládá na certifikátu samotném, ale na PIN párování,
    /// které je na otisk certifikátu navázané (viz Pairing).
    /// </summary>
    public static X509Certificate2 CreateEphemeralCertificate(string machineName)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN=Migrato {machineName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

        // Re-import přes PKCS#12 — přímo vytvořený certifikát nemá na všech
        // platformách použitelný privátní klíč pro SslStream.
        return X509CertificateLoader.LoadPkcs12(
            cert.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
    }

    /// <summary>SHA-256 otisk certifikátu jako hex řetězec.</summary>
    public static string Fingerprint(X509Certificate certificate)
        => Convert.ToHexString(SHA256.HashData(certificate.GetRawCertData()));
}
