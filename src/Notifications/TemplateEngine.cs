namespace WebsiteMonitor.Notifications;

public static class TemplateEngine
{
    // Replaces {{Key}} tokens. No regex.
    public static string Render(string template, IReadOnlyDictionary<string, string> vars)
    {
        if (string.IsNullOrEmpty(template)) return template;
        if (vars.Count == 0) return template;

        var s = template.AsSpan();
        var sb = new System.Text.StringBuilder(template.Length);

        for (var i = 0; i < s.Length; i++)
        {
            var ch = s[i];
            if (ch == '{' && i + 1 < s.Length && s[i + 1] == '{')
            {
                var end = FindTokenEnd(s, i + 2);
                if (end < 0)
                {
                    sb.Append(ch);
                    continue;
                }

                var key = s.Slice(i + 2, end - (i + 2)).ToString().Trim();
                if (vars.TryGetValue(key, out var val)) sb.Append(val);
                else sb.Append(""); // unknown tokens => empty

                i = end + 1; // position at second '}'
                continue;
            }

            sb.Append(ch);
        }

        return sb.ToString();
    }

    private static int FindTokenEnd(ReadOnlySpan<char> s, int start)
    {
        for (var i = start; i + 1 < s.Length; i++)
        {
            if (s[i] == '}' && s[i + 1] == '}')
                return i;
        }
        return -1;
    }
}
