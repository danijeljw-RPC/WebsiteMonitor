using System.Net.Http.Headers;
using System.Text;
using WebsiteMonitor.Config;
using WebsiteMonitor.Logging;

namespace WebsiteMonitor.Notifications;

public sealed class SmsNotificationChannel : INotificationChannel
{
    private readonly NotificationsConfig _cfg;
    private readonly JsonConsoleLogger _log;
    private static readonly HttpClient Http = new HttpClient();

    public SmsNotificationChannel(NotificationsConfig cfg, JsonConsoleLogger log)
    {
        _cfg = cfg;
        _log = log;
    }

    public string Kind => "sms";

    public bool CanHandle(PlannedNotification evt) => _cfg.Sms is not null;

    public async Task<NotificationSendAttempt> SendAsync(PlannedNotification evt)
    {
        var sms = _cfg.Sms!;
        var template = SelectTemplate(_cfg.Templates, evt.EventType);

        // Render message body for SMS template selection
        var smsText = TemplateEngine.Render(template.SmsTextBody, evt.Vars);

        // Wrap in configured bodyTemplate
        var wrapVars = new Dictionary<string, string>(evt.Vars, StringComparer.Ordinal)
        {
            ["Body"] = smsText
        };

        var body = TemplateEngine.Render(sms.BodyTemplate, wrapVars);

        var subject = ""; // SMS doesn't use subject

        try
        {
            using var req = new HttpRequestMessage(ToMethod(sms.Method), sms.Endpoint);
            req.Content = new StringContent(body, Encoding.UTF8, sms.ContentType);

            if (sms.Headers is not null)
            {
                foreach (var kv in sms.Headers)
                {
                    // Allow Authorization header etc.
                    if (!req.Headers.TryAddWithoutValidation(kv.Key, kv.Value))
                    {
                        req.Content?.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                    }
                }
            }

            using var resp = await Http.SendAsync(req);
            var ok = (int)resp.StatusCode >= 200 && (int)resp.StatusCode <= 299;

            return new NotificationSendAttempt
            {
                Success = ok,
                SentTo = sms.Endpoint,
                Subject = subject,
                Body = body,
                Error = ok ? null : $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}"
            };
        }
        catch (Exception ex)
        {
            _log.Warn("sms_send_failed", w=>
            {
                w.WriteString("evt", evt.EventType);
                w.WriteString("checkId", evt.CheckId);
                w.WriteString("msg", ex.Message);
            });

            return new NotificationSendAttempt
            {
                Success = false,
                SentTo = sms.Endpoint,
                Subject = subject,
                Body = body,
                Error = $"{ex.GetType().Name}: {ex.Message}"
            };
        }
    }

    private static HttpMethod ToMethod(string s)
    {
        if (s.Equals("POST", StringComparison.OrdinalIgnoreCase)) return HttpMethod.Post;
        if (s.Equals("PUT", StringComparison.OrdinalIgnoreCase)) return HttpMethod.Put;
        if (s.Equals("PATCH", StringComparison.OrdinalIgnoreCase)) return HttpMethod.Patch;
        return HttpMethod.Post;
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
