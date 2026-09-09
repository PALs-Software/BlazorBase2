using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace BlazorBase.User.SecurityHarness.Hosting;

/// <summary>
/// Generates a self-signed, loopback-only certificate entirely in-process, so an HTTPS profile
/// never depends on a machine-installed development certificate and runs unmodified on a clean CI
/// agent.
/// </summary>
internal static class HarnessSelfSignedCertificate
{
    /// <summary>
    /// The certificate returned is re-imported from a PFX rather than the ephemeral-keyset
    /// certificate <see cref="CertificateRequest.CreateSelfSigned"/> returns directly, because that
    /// ephemeral-keyset certificate is not reliably usable as a Kestrel server certificate on every
    /// platform.
    /// </summary>
    public static X509Certificate2 Create()
    {
        using var key = RSA.Create(2048);

        var request = new CertificateRequest(
            "CN=BlazorBase Security Harness",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
        subjectAlternativeNames.AddIpAddress(IPAddress.Loopback);
        subjectAlternativeNames.AddDnsName("localhost");
        request.CertificateExtensions.Add(subjectAlternativeNames.Build());

        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            [new Oid("1.3.6.1.5.5.7.3.1")], false));

        var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        var notAfter = notBefore.AddDays(1);

        using var ephemeralCertificate = request.CreateSelfSigned(notBefore, notAfter);

        return X509CertificateLoader.LoadPkcs12(ephemeralCertificate.Export(X509ContentType.Pfx), password: null, X509KeyStorageFlags.Exportable);
    }
}
