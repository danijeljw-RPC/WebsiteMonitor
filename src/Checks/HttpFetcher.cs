using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;

namespace WebsiteMonitor.Checks;

public sealed class HttpFetcher : IDisposable
{
    private readonly HttpClient _client;

    public HttpFetcher(TimeSpan timeout, CookieContainer? cookies, bool allowDecompression)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = cookies is not null,
            CookieContainer = cookies ?? new CookieContainer(),
            AutomaticDecompression = allowDecompression ? DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli : DecompressionMethods.None,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        };

        _client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = timeout
        };

        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("WebsiteMonitor", "1.0"));
    }

    public async Task<HttpFetchResult> FetchAsync(HttpRequestMessage initialRequest, int maxRedirects, int maxBodyBytes, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var current = initialRequest;

        var redirectCount = 0;
        while (true)
        {
            using var resp = await _client.SendAsync(current, HttpCompletionOption.ResponseHeadersRead, ct);
            var status = (int)resp.StatusCode;

            if (IsRedirect(resp.StatusCode) && resp.Headers.Location is not null && redirectCount < maxRedirects)
            {
                redirectCount++;

                var next = ResolveRedirect(current.RequestUri!, resp.Headers.Location);
                current.Dispose();

                var nextMethod = status is 301 or 302 or 303 ? HttpMethod.Get : current.Method;
                current = new HttpRequestMessage(nextMethod, next);
                continue;
            }

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var h in resp.Headers)
                headers[h.Key] = string.Join(",", h.Value);
            foreach (var h in resp.Content.Headers)
                headers[h.Key] = string.Join(",", h.Value);

            var body = await ReadUpToAsync(await resp.Content.ReadAsStreamAsync(ct), maxBodyBytes, ct);

            sw.Stop();

            return new HttpFetchResult
            {
                StatusCode = status,
                Headers = headers,
                BodyBytes = body,
                RedirectCount = redirectCount,
                FinalUri = resp.RequestMessage?.RequestUri ?? current.RequestUri!,
                ElapsedMs = sw.ElapsedMilliseconds
            };
        }
    }

    private static bool IsRedirect(HttpStatusCode code)
        => code is HttpStatusCode.Moved or HttpStatusCode.Redirect or HttpStatusCode.RedirectMethod or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private static Uri ResolveRedirect(Uri baseUri, Uri location)
        => location.IsAbsoluteUri ? location : new Uri(baseUri, location);

    private static async Task<byte[]> ReadUpToAsync(Stream s, int maxBytes, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        using var ms = new MemoryStream();

        while (true)
        {
            var remaining = maxBytes - (int)ms.Length;
            if (remaining <= 0) break;

            var toRead = Math.Min(buffer.Length, remaining);
            var read = await s.ReadAsync(buffer.AsMemory(0, toRead), ct);
            if (read <= 0) break;

            ms.Write(buffer, 0, read);
        }

        return ms.ToArray();
    }

    public void Dispose() => _client.Dispose();
}

public sealed class HttpFetchResult
{
    public int StatusCode { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public byte[] BodyBytes { get; set; } = Array.Empty<byte>();
    public int RedirectCount { get; set; }
    public Uri FinalUri { get; set; } = new Uri("about:blank");
    public long ElapsedMs { get; set; }
}
