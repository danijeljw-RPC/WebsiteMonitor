namespace WebsiteMonitor.Notifications;

public interface INotificationChannel
{
    string Kind { get; } // "email" | "sms"
    bool CanHandle(PlannedNotification evt);
    Task<NotificationSendAttempt> SendAsync(PlannedNotification evt);
}
