namespace WebsiteMonitor.Config;

public static class ConfigValidator
{
    public static void ValidateOrThrow(AppConfig cfg)
    {
        if (cfg is null) throw new ConfigException("Config is null");

        if (cfg.Sqlite is null) throw new ConfigException("sqlite section is missing");
        if (string.IsNullOrWhiteSpace(cfg.Sqlite.DbPath)) throw new ConfigException("sqlite.dbPath is required");

        if (cfg.Checks is null || cfg.Checks.Count == 0)
            throw new ConfigException("checks must contain at least one check");

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in cfg.Checks)
        {
            if (string.IsNullOrWhiteSpace(c.Id))
                throw new ConfigException("Each check must have an id");

            if (!ids.Add(c.Id))
                throw new ConfigException($"Duplicate check id: {c.Id}");

            if (string.IsNullOrWhiteSpace(c.Name))
                throw new ConfigException($"Check {c.Id} must have a name");

            if (string.IsNullOrWhiteSpace(c.Url))
                throw new ConfigException($"Check {c.Id} must have a url");

            if (!Uri.TryCreate(c.Url, UriKind.Absolute, out var _))
                throw new ConfigException($"Check {c.Id} url is not a valid absolute URI: {c.Url}");

            if (c.TimeoutSeconds <= 0 || c.TimeoutSeconds > 300)
                throw new ConfigException($"Check {c.Id} timeoutSeconds must be 1..300");

            ValidateSeverity(c.Id, c.Severity);
            ValidateMethod(c.Id, c.Method);

            if (c.ExpectedStatus.Min < 100 || c.ExpectedStatus.Max > 599 || c.ExpectedStatus.Min > c.ExpectedStatus.Max)
                throw new ConfigException($"Check {c.Id} expectedStatus must be 100..599 and min<=max");

            if (c.Redirects is not null && c.Redirects.MaxRedirects < 0)
                throw new ConfigException($"Check {c.Id} redirects.maxRedirects must be >= 0");

            if (c.ContentRule is not null)
                ValidateContentRule(c.Id, c.ContentRule);

            if (c.Login is not null)
                ValidateLogin(c.Id, c.Login);

            if (c.Headers is not null)
            {
                foreach (var h in c.Headers)
                {
                    if (string.IsNullOrWhiteSpace(h.Name))
                        throw new ConfigException($"Check {c.Id} headers[] requires name");
                }
            }

            if (c.ContentLength is not null)
            {
                if (c.ContentLength.MinBytes is int min && min < 0)
                    throw new ConfigException($"Check {c.Id} contentLength.minBytes must be >= 0");

                if (c.ContentLength.MaxBytes is int max && max < 0)
                    throw new ConfigException($"Check {c.Id} contentLength.maxBytes must be >= 0");

                if (c.ContentLength.MinBytes is int min2 && c.ContentLength.MaxBytes is int max2 && min2 > max2)
                    throw new ConfigException($"Check {c.Id} contentLength minBytes > maxBytes");
            }

            if (c.MaxBodyBytes is int mb && (mb < 1024 || mb > 50_000_000))
                throw new ConfigException($"Check {c.Id} maxBodyBytes should be between 1024 and 50_000_000");
        }

        if (cfg.Notifications is not null)
        {
            // templates exist by default
            // validate email settings if email enabled
            // validate sms settings if sms enabled
        }
    }

    private static void ValidateSeverity(string checkId, string s)
    {
        if (string.Equals(s, "Info", StringComparison.OrdinalIgnoreCase)) return;
        if (string.Equals(s, "Warning", StringComparison.OrdinalIgnoreCase)) return;
        if (string.Equals(s, "Critical", StringComparison.OrdinalIgnoreCase)) return;

        throw new ConfigException($"Check {checkId} severity must be Info|Warning|Critical (got: {s})");
    }

    private static void ValidateMethod(string checkId, string m)
    {
        if (string.Equals(m, "GET", StringComparison.OrdinalIgnoreCase)) return;
        if (string.Equals(m, "POST", StringComparison.OrdinalIgnoreCase)) return;
        if (string.Equals(m, "HEAD", StringComparison.OrdinalIgnoreCase)) return;

        throw new ConfigException($"Check {checkId} method must be GET|POST|HEAD (got: {m})");
    }

    private static void ValidateContentRule(string checkId, ContentRuleConfig rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Type))
            throw new ConfigException($"Check {checkId} contentRule.type is required");

        if (string.IsNullOrWhiteSpace(rule.Value))
            throw new ConfigException($"Check {checkId} contentRule.value is required");

        if (rule.Type.Equals("contains", StringComparison.OrdinalIgnoreCase)) return;
        if (rule.Type.Equals("regex", StringComparison.OrdinalIgnoreCase)) return;

        throw new ConfigException($"Check {checkId} contentRule.type must be contains|regex");
    }

    private static void ValidateLogin(string checkId, LoginConfig login)
    {
        if (string.IsNullOrWhiteSpace(login.LoginUrl))
            throw new ConfigException($"Check {checkId} login.loginUrl is required");

        if (!Uri.TryCreate(login.LoginUrl, UriKind.Absolute, out _))
            throw new ConfigException($"Check {checkId} login.loginUrl is invalid: {login.LoginUrl}");

        if (string.IsNullOrWhiteSpace(login.UsernameField))
            throw new ConfigException($"Check {checkId} login.usernameField is required");

        if (string.IsNullOrWhiteSpace(login.PasswordField))
            throw new ConfigException($"Check {checkId} login.passwordField is required");

        if (login.SuccessIndicator is not null)
            ValidateContentRule(checkId, login.SuccessIndicator);

        if (login.PostLoginRule is not null)
            ValidateContentRule(checkId, login.PostLoginRule);
    }
}
