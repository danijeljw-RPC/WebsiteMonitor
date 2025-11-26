using System.Text.RegularExpressions;
using WebsiteMonitor.Config;

namespace WebsiteMonitor.Checks;

public static class ContentRules
{
    public static bool Evaluate(ContentRuleConfig rule, string content, out string? error)
    {
        error = null;

        if (rule.Type.Equals("contains", StringComparison.OrdinalIgnoreCase))
        {
            if (!content.Contains(rule.Value, StringComparison.Ordinal))
            {
                error = $"Content missing expected marker (contains): {Truncate(rule.Value, 120)}";
                return false;
            }
            return true;
        }

        if (rule.Type.Equals("regex", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                if (!Regex.IsMatch(content, rule.Value, RegexOptions.CultureInvariant))
                {
                    error = $"Content missing expected marker (regex): {Truncate(rule.Value, 120)}";
                    return false;
                }
                return true;
            }
            catch (ArgumentException ex)
            {
                error = $"Invalid regex: {ex.Message}";
                return false;
            }
        }

        error = $"Unknown content rule type: {rule.Type}";
        return false;
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s.Substring(0, max) + "...";
}
