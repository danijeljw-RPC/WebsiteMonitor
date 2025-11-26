namespace WebsiteMonitor.Notifications;

public static class NotificationChannelSelector
{
    public static IReadOnlyList<string> ResolveEnabledChannels(
        List<string>? configEnabledChannels,
        bool cliEmail,
        bool cliSms)
    {
        // If CLI flags are used, treat them as the explicit enabled set.
        if (cliEmail || cliSms)
        {
            var list = new List<string>();
            if (cliEmail) list.Add("email");
            if (cliSms) list.Add("sms");
            return list;
        }

        return (configEnabledChannels is { Count: > 0 })
            ? configEnabledChannels
            : Array.Empty<string>();
    }
}

public sealed class PlannedNotification
{
    public string EventType { get; set; } = "";     // CheckFailed | Recovered | SlowResponse | CertExpiring
    public string CheckId { get; set; } = "";
    public string CheckName { get; set; } = "";
    public string Environment { get; set; } = "";
    public string Url { get; set; } = "";

    // Variables for templating ({{Var}})
    public Dictionary<string, string> Vars { get; set; } = new(StringComparer.Ordinal);

    public string DedupeKeyForChannel(string channelKind) => $"{channelKind}:{EventType}:{CheckId}";
}

public sealed class NotificationSendAttempt
{
    public bool Success { get; set; }
    public string SentTo { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string? Error { get; set; }
}
