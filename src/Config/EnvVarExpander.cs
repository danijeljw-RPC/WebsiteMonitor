namespace WebsiteMonitor.Config;

public static class EnvVarExpander
{
    // Expands ${VAR} sequences. Deterministic, no regex.
    public static void ExpandInPlace(AppConfig cfg)
    {
        cfg.Sqlite.DbPath = Expand(cfg.Sqlite.DbPath);

        foreach (var c in cfg.Checks)
        {
            c.Url = Expand(c.Url);

            if (c.Login is not null)
            {
                c.Login.LoginUrl = Expand(c.Login.LoginUrl);
                c.Login.Username = ExpandNullable(c.Login.Username);
                c.Login.Password = ExpandNullable(c.Login.Password);

                if (c.Login.AdditionalFields is not null)
                {
                    var keys = c.Login.AdditionalFields.Keys.ToArray();
                    foreach (var k in keys)
                        c.Login.AdditionalFields[k] = Expand(c.Login.AdditionalFields[k]);
                }

                c.Login.PostLoginUrl = ExpandNullable(c.Login.PostLoginUrl);
            }
        }

        if (cfg.Notifications is not null)
        {
            if (cfg.Notifications.Email is not null)
            {
                cfg.Notifications.Email.Host = Expand(cfg.Notifications.Email.Host);
                cfg.Notifications.Email.Username = ExpandNullable(cfg.Notifications.Email.Username);
                cfg.Notifications.Email.Password = ExpandNullable(cfg.Notifications.Email.Password);
                cfg.Notifications.Email.From = Expand(cfg.Notifications.Email.From);

                for (var i = 0; i < cfg.Notifications.Email.To.Count; i++)
                    cfg.Notifications.Email.To[i] = Expand(cfg.Notifications.Email.To[i]);
            }

            if (cfg.Notifications.Sms is not null)
            {
                cfg.Notifications.Sms.Endpoint = Expand(cfg.Notifications.Sms.Endpoint);

                if (cfg.Notifications.Sms.Headers is not null)
                {
                    var keys = cfg.Notifications.Sms.Headers.Keys.ToArray();
                    foreach (var k in keys)
                        cfg.Notifications.Sms.Headers[k] = Expand(cfg.Notifications.Sms.Headers[k]);
                }

                cfg.Notifications.Sms.BodyTemplate = Expand(cfg.Notifications.Sms.BodyTemplate);
            }
        }
    }

    private static string? ExpandNullable(string? s) => s is null ? null : Expand(s);

    public static string Expand(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        // Supports escaping \${VAR} => literal ${VAR}
        var chars = s.AsSpan();
        var sb = new System.Text.StringBuilder(s.Length);

        for (var i = 0; i < chars.Length; i++)
        {
            var ch = chars[i];

            if (ch == '\\' && i + 2 < chars.Length && chars[i + 1] == '$' && chars[i + 2] == '{')
            {
                sb.Append("${");
                i += 2;
                continue;
            }

            if (ch == '$' && i + 1 < chars.Length && chars[i + 1] == '{')
            {
                var end = IndexOf(chars, '}', i + 2);
                if (end < 0)
                {
                    sb.Append(ch);
                    continue;
                }

                var varName = chars.Slice(i + 2, end - (i + 2)).ToString().Trim();
                var val = Environment.GetEnvironmentVariable(varName) ?? "";
                sb.Append(val);
                i = end;
                continue;
            }

            sb.Append(ch);
        }

        return sb.ToString();
    }

    private static int IndexOf(ReadOnlySpan<char> s, char c, int start)
    {
        for (var i = start; i < s.Length; i++)
            if (s[i] == c) return i;
        return -1;
    }
}
