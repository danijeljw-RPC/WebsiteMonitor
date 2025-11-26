using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

namespace WebsiteMonitor.Checks;

public static class TlsProbe
{
    public static async Task<int?> GetCertDaysRemainingAsync(Uri uri, int timeoutSeconds, CancellationToken ct)
    {
        if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            return null;

        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 443;

        using var tcp = new TcpClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        await tcp.ConnectAsync(host, port, cts.Token);

        await using var ns = tcp.GetStream();
        using var ssl = new SslStream(ns, leaveInnerStreamOpen: false, userCertificateValidationCallback: (_, _, _, _) => true);

        var options = new SslClientAuthenticationOptions
        {
            TargetHost = host,
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck
        };

        await ssl.AuthenticateAsClientAsync(options, cts.Token);

        var cert = ssl.RemoteCertificate;
        if (cert is null) return null;

        using var x509 = new X509Certificate2(cert);
        var days = (int)Math.Floor((x509.NotAfter.ToUniversalTime() - DateTime.UtcNow).TotalDays);
        return days;
    }
}
