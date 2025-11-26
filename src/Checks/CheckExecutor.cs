using System.Net;
using System.Text;
using WebsiteMonitor.Config;
using WebsiteMonitor.Logging;

namespace WebsiteMonitor.Checks;

public sealed class CheckExecutor
{
    private readonly JsonConsoleLogger _log;

    public CheckExecutor(JsonConsoleLogger log)
    {
        _log = log;
    }

    public async Task<CheckResult> ExecuteAsync(CheckConfig check)
    {
        var res = new CheckResult
        {
            CheckId = check.Id,
            CheckName = check.Name,
            Url = check.Url,
            Method = check.Method.ToUpperInvariant(),
            Severity = check.Severity
        };

        var maxRedirects = check.Redirects?.MaxRedirects ?? 5;
        var maxBodyBytes = check.MaxBodyBytes ?? 1_048_576;
        var timeout = TimeSpan.FromSeconds(check.TimeoutSeconds);

        try
        {
            if (check.Login is not null)
            {
                await ExecuteLoginFlowAsync(check, res, timeout, maxRedirects, maxBodyBytes);
            }
            else
            {
                await ExecuteSimpleAsync(check, res, timeout, maxRedirects, maxBodyBytes);
            }

            // TLS probe is separate (optional)
            if (check.Tls is not null && Uri.TryCreate(check.Url, UriKind.Absolute, out var u))
            {
                try
                {
                    res.CertDaysRemaining = await TlsProbe.GetCertDaysRemainingAsync(u, check.TimeoutSeconds, CancellationToken.None);
                    EvaluateTlsTriggers(check, res);
                }
                catch (Exception ex)
                {
                    // TLS probe failure should not wipe the primary HTTP result; record as info in error if nothing else failed.
                    _log.Warn("tls_probe_failed", w=>
                    {
                        w.WriteString("checkId", check.Id);
                        w.WriteString("msg", ex.Message);
                    });
                }
            }

            // latency rules
            EvaluateLatencyTriggers(check, res);

            // final succeeded/failed logic:
            // - primary checks determine success
            // - latencyMode Warn does not fail success, but triggers SlowResponse
            // - TLS warn triggers CertExpiring without failing unless minDaysRemaining breached
            return res;
        }
        catch (Exception ex)
        {
            res.Succeeded = false;
            res.Error = $"{ex.GetType().Name}: {ex.Message}";
            return res;
        }
    }

    private async Task ExecuteSimpleAsync(CheckConfig check, CheckResult res, TimeSpan timeout, int maxRedirects, int maxBodyBytes)
    {
        using var fetcher = new HttpFetcher(timeout, cookies: null, allowDecompression: true);

        using var req = new HttpRequestMessage(ToHttpMethod(check.Method), new Uri(check.Url));
        var fetch = await fetcher.FetchAsync(req, maxRedirects, maxBodyBytes, CancellationToken.None);

        res.StatusCode = fetch.StatusCode;
        res.RedirectCount = fetch.RedirectCount;
        res.ResponseBytes = fetch.BodyBytes.Length;
        res.LatencyMs = fetch.ElapsedMs;

        var errors = new List<string>();

        // status
        if (fetch.StatusCode < check.ExpectedStatus.Min || fetch.StatusCode > check.ExpectedStatus.Max)
            errors.Add($"Status out of range: {fetch.StatusCode} (expected {check.ExpectedStatus.Min}-{check.ExpectedStatus.Max})");

        // expected headers
        if (check.Headers is not null && check.Headers.Count > 0)
        {
            foreach (var eh in check.Headers)
            {
                if (!fetch.Headers.TryGetValue(eh.Name, out var actual))
                    errors.Add($"Missing header: {eh.Name}");
                else if (!actual.Contains(eh.Contains, StringComparison.OrdinalIgnoreCase))
                    errors.Add($"Header mismatch: {eh.Name} does not contain '{eh.Contains}' (actual '{Truncate(actual, 120)}')");
            }
        }

        // redirect budget exhaustion detection
        if (fetch.RedirectCount >= maxRedirects && IsRedirect(fetch.StatusCode))
            errors.Add($"Redirect budget exceeded (max {maxRedirects})");

        // content length
        if (check.ContentLength is not null)
        {
            if (check.ContentLength.MinBytes is int min && fetch.BodyBytes.Length < min)
                errors.Add($"Content too short: {fetch.BodyBytes.Length} bytes (min {min})");
            if (check.ContentLength.MaxBytes is int max && fetch.BodyBytes.Length > max)
                errors.Add($"Content too large: {fetch.BodyBytes.Length} bytes (max {max})");
        }

        // content rule
        if (check.ContentRule is not null)
        {
            var text = DecodeBody(fetch.BodyBytes);
            if (!ContentRules.Evaluate(check.ContentRule, text, out var err))
                errors.Add(err ?? "Content rule failed");
        }

        if (errors.Count > 0)
        {
            res.Succeeded = false;
            res.Error = string.Join(" | ", errors);
        }
        else
        {
            res.Succeeded = true;
        }
    }

    private async Task ExecuteLoginFlowAsync(CheckConfig check, CheckResult res, TimeSpan timeout, int maxRedirects, int maxBodyBytes)
    {
        var login = check.Login!;
        var cookies = new CookieContainer();
        using var fetcher = new HttpFetcher(timeout, cookies: cookies, allowDecompression: true);

        // 1) GET login page (optional, but establishes cookies for some apps)
        if (!string.IsNullOrWhiteSpace(check.Url))
        {
            using var preReq = new HttpRequestMessage(HttpMethod.Get, new Uri(check.Url));
            var pre = await fetcher.FetchAsync(preReq, maxRedirects, maxBodyBytes, CancellationToken.None);
            res.RedirectCount += pre.RedirectCount;
        }

        // 2) POST login form (application/x-www-form-urlencoded)
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [login.UsernameField] = login.Username ?? "",
            [login.PasswordField] = login.Password ?? ""
        };
        if (login.AdditionalFields is not null)
        {
            foreach (var kv in login.AdditionalFields)
                fields[kv.Key] = kv.Value;
        }

        using var postReq = new HttpRequestMessage(HttpMethod.Post, new Uri(login.LoginUrl));
        postReq.Content = new FormUrlEncodedContent(fields);

        var post = await fetcher.FetchAsync(postReq, maxRedirects, maxBodyBytes, CancellationToken.None);

        res.StatusCode = post.StatusCode;
        res.RedirectCount += post.RedirectCount;
        res.ResponseBytes = post.BodyBytes.Length;
        res.LatencyMs = post.ElapsedMs;

        var errors = new List<string>();

        // status on login post should be "successful enough" for the configured expected range
        if (post.StatusCode < check.ExpectedStatus.Min || post.StatusCode > check.ExpectedStatus.Max)
            errors.Add($"Login POST status out of range: {post.StatusCode} (expected {check.ExpectedStatus.Min}-{check.ExpectedStatus.Max})");

        // successIndicator on login response content
        if (login.SuccessIndicator is not null)
        {
            var text = DecodeBody(post.BodyBytes);
            if (!ContentRules.Evaluate(login.SuccessIndicator, text, out var err))
                errors.Add(err ?? "Login success indicator failed");
        }

        // 3) GET post-login url, validate marker
        if (!string.IsNullOrWhiteSpace(login.PostLoginUrl))
        {
            using var afterReq = new HttpRequestMessage(HttpMethod.Get, new Uri(login.PostLoginUrl!));
            var after = await fetcher.FetchAsync(afterReq, maxRedirects, maxBodyBytes, CancellationToken.None);

            res.StatusCode = after.StatusCode;                 // overwrite to reflect actual target page
            res.LatencyMs = (res.LatencyMs ?? 0) + after.ElapsedMs;
            res.RedirectCount += after.RedirectCount;
            res.ResponseBytes = after.BodyBytes.Length;

            if (after.StatusCode < 200 || after.StatusCode > 399)
                errors.Add($"Post-login GET unexpected status: {after.StatusCode}");

            if (login.PostLoginRule is not null)
            {
                var text = DecodeBody(after.BodyBytes);
                if (!ContentRules.Evaluate(login.PostLoginRule, text, out var err))
                    errors.Add(err ?? "Post-login rule failed");
            }
        }

        if (errors.Count > 0)
        {
            res.Succeeded = false;
            res.Error = string.Join(" | ", errors);
        }
        else
        {
            res.Succeeded = true;
        }
    }

    private static void EvaluateLatencyTriggers(CheckConfig check, CheckResult res)
    {
        if (check.MaxLatencyMs is not long max || res.LatencyMs is not long got)
            return;

        if (got <= max) return;

        var mode = (check.LatencyMode ?? "Fail").Trim();

        res.SlowTriggered = true;

        if (mode.Equals("Ignore", StringComparison.OrdinalIgnoreCase))
        {
            res.SlowTriggered = false;
            return;
        }

        if (mode.Equals("Warn", StringComparison.OrdinalIgnoreCase))
        {
            // doesn't fail the check, but should be notified
            res.WarningOnly = true;
            return;
        }

        // Fail (default)
        res.Succeeded = false;
        res.Error = Append(res.Error, $"Slow response: {got}ms > {max}ms");
    }

    private static void EvaluateTlsTriggers(CheckConfig check, CheckResult res)
    {
        if (check.Tls is null) return;
        if (res.CertDaysRemaining is not int days) return;

        var warn = check.Tls.WarnDaysRemaining;
        var min = check.Tls.MinDaysRemaining;

        if (warn is int w && days <= w)
        {
            res.CertExpiringTriggered = true;
        }

        if (min is int m && days <= m)
        {
            res.Succeeded = false;
            res.Error = Append(res.Error, $"TLS cert too close to expiry: {days} days remaining (min {m})");
        }
    }

    private static bool IsRedirect(int statusCode)
        => statusCode is 301 or 302 or 303 or 307 or 308;

    private static HttpMethod ToHttpMethod(string s)
    {
        if (s.Equals("GET", StringComparison.OrdinalIgnoreCase)) return HttpMethod.Get;
        if (s.Equals("POST", StringComparison.OrdinalIgnoreCase)) return HttpMethod.Post;
        if (s.Equals("HEAD", StringComparison.OrdinalIgnoreCase)) return HttpMethod.Head;
        return HttpMethod.Get;
    }

    private static string DecodeBody(byte[] bytes)
        => Encoding.UTF8.GetString(bytes); // keep simple; for HTML marker checks, UTF-8 is adequate in most cases

    private static string Append(string? a, string b)
        => string.IsNullOrWhiteSpace(a) ? b : (a + " | " + b);

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s.Substring(0, max) + "...";
}
