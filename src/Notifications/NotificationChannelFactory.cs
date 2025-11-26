using WebsiteMonitor.Config;
using WebsiteMonitor.Logging;

namespace WebsiteMonitor.Notifications;

public static class NotificationChannelFactory
{
    public static List<INotificationChannel> CreateChannels(IReadOnlyList<string> enabledChannels, NotificationsConfig? cfg, JsonConsoleLogger log)
    {
        var list = new List<INotificationChannel>();
        if (cfg is null) return list;

        foreach (var ch in enabledChannels)
        {
            if (ch.Equals("email", StringComparison.OrdinalIgnoreCase) && cfg.Email is not null)
                list.Add(new EmailNotificationChannel(cfg, log));

            if (ch.Equals("sms", StringComparison.OrdinalIgnoreCase) && cfg.Sms is not null)
                list.Add(new SmsNotificationChannel(cfg, log));
        }

        return list;
    }
}
