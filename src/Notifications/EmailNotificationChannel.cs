using System.Net;
using System.Net.Mail;
using WebsiteMonitor.Config;
using WebsiteMonitor.Logging;

namespace WebsiteMonitor.Notifications;

public sealed class EmailNotificationChannel : INotificationChannel
{
    private readonly NotificationsConfig _cfg;
    private readonly JsonConsoleLogger _log;

    public EmailNotificationChannel(NotificationsConfig cfg, JsonConsoleLogger log)
    {
        _cfg = cfg;
        _log = log;
    }

    public string Kind => "email";

    public bool CanHandle(PlannedNotification evt) => _cfg.Email is not null;

    public Task<NotificationSendAttempt> SendAsync(PlannedNotification evt)
    {
        var email = _cfg.Email!;
        var template = SelectTemplate(_cfg.Templates, evt.EventType);

        var subject = TemplateEngine.Render(template.EmailSubject, evt.Vars);
        var bodyHtml = TemplateEngine.Render(template.EmailHtmlBody, evt.Vars);

        // AOT-friendly: use SmtpClient (simple, BCL). No secrets logged.
        try
        {
            using var msg = new MailMessage
            {
                From = new MailAddress(email.From),
                Subject = subject,
                Body = bodyHtml,
                IsBodyHtml = true
            };

            foreach (var to in email.To)
                msg.To.Add(to);

            using var client = new SmtpClient(email.Host, email.Port)
            {
                EnableSsl = email.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false
            };

            if (!string.IsNullOrWhiteSpace(email.Username))
                client.Credentials = new NetworkCredential(email.Username, email.Password ?? "");

            client.Send(msg);

            return Task.FromResult(new NotificationSendAttempt
            {
                Success = true,
                SentTo = string.Join(",", email.To),
                Subject = subject,
                Body = bodyHtml
            });
        }
        catch (Exception ex)
        {
            _log.Warn("email_send_failed", w =>
            {
                w.WriteString("evt", evt.EventType);
                w.WriteString("checkId", evt.CheckId);
                w.WriteString("msg", ex.Message);
            });

            return Task.FromResult(new NotificationSendAttempt
            {
                Success = false,
                SentTo = string.Join(",", email.To),
                Subject = subject,
                Body = bodyHtml,
                Error = $"{ex.GetType().Name}: {ex.Message}"
            });
        }
    }

    private static NotificationTemplate SelectTemplate(NotificationTemplatesConfig t, string eventType)
        => eventType switch
        {
            "CheckFailed" => t.CheckFailed,
            "Recovered" => t.Recovered,
            "SlowResponse" => t.SlowResponse,
            "CertExpiring" => t.CertExpiring,
            _ => t.CheckFailed
        };
}
