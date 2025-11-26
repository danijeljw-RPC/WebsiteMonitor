using System.Text.Json.Serialization;

namespace WebsiteMonitor.Config;

public sealed class ConfigException : Exception
{
    public ConfigException(string message) : base(message) { }
}

public sealed class AppConfig
{
    [JsonPropertyName("app")]
    public AppSection App { get; set; } = new();

    [JsonPropertyName("sqlite")]
    public SqliteSection Sqlite { get; set; } = new();

    [JsonPropertyName("checks")]
    public List<CheckConfig> Checks { get; set; } = new();

    [JsonPropertyName("notifications")]
    public NotificationsConfig? Notifications { get; set; }
}

public sealed class AppSection
{
    [JsonPropertyName("name")]
    public string? Name { get; set; } = "WebsiteMonitor";

    [JsonPropertyName("environment")]
    public string? Environment { get; set; } = "local";

    [JsonPropertyName("runIntervalHintSeconds")]
    public int? RunIntervalHintSeconds { get; set; } = 60;

    [JsonPropertyName("timezone")]
    public string? Timezone { get; set; }
}

public sealed class SqliteSection
{
    [JsonPropertyName("dbPath")]
    public string DbPath { get; set; } = "./monitor.db";
}

public sealed class CheckConfig
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    // "Info" | "Warning" | "Critical"
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "Critical";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("method")]
    public string Method { get; set; } = "GET";

    [JsonPropertyName("timeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 15;

    [JsonPropertyName("expectedStatus")]
    public ExpectedStatusConfig ExpectedStatus { get; set; } = new();

    [JsonPropertyName("redirects")]
    public RedirectConfig? Redirects { get; set; }

    [JsonPropertyName("maxLatencyMs")]
    public long? MaxLatencyMs { get; set; }

    // "Ignore" | "Warn" | "Fail" (default Fail if maxLatencyMs is set)
    [JsonPropertyName("latencyMode")]
    public string? LatencyMode { get; set; }

    [JsonPropertyName("contentRule")]
    public ContentRuleConfig? ContentRule { get; set; }

    [JsonPropertyName("login")]
    public LoginConfig? Login { get; set; }

    [JsonPropertyName("tls")]
    public TlsConfig? Tls { get; set; }

    [JsonPropertyName("headers")]
    public List<ExpectedHeaderConfig>? Headers { get; set; }

    [JsonPropertyName("contentLength")]
    public ContentLengthConfig? ContentLength { get; set; }

    // cap response body read (bytes) to keep runs lean
    [JsonPropertyName("maxBodyBytes")]
    public int? MaxBodyBytes { get; set; }
}

public sealed class ExpectedStatusConfig
{
    [JsonPropertyName("min")]
    public int Min { get; set; } = 200;

    [JsonPropertyName("max")]
    public int Max { get; set; } = 299;
}

public sealed class RedirectConfig
{
    [JsonPropertyName("maxRedirects")]
    public int MaxRedirects { get; set; } = 5;
}

public sealed class ContentRuleConfig
{
    // "contains" | "regex"
    [JsonPropertyName("type")]
    public string Type { get; set; } = "contains";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
}

public sealed class LoginConfig
{
    [JsonPropertyName("loginUrl")]
    public string LoginUrl { get; set; } = "";

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("usernameField")]
    public string UsernameField { get; set; } = "username";

    [JsonPropertyName("passwordField")]
    public string PasswordField { get; set; } = "password";

    [JsonPropertyName("additionalFields")]
    public Dictionary<string, string>? AdditionalFields { get; set; }

    [JsonPropertyName("successIndicator")]
    public ContentRuleConfig? SuccessIndicator { get; set; }

    [JsonPropertyName("postLoginUrl")]
    public string? PostLoginUrl { get; set; }

    [JsonPropertyName("postLoginRule")]
    public ContentRuleConfig? PostLoginRule { get; set; }
}

public sealed class TlsConfig
{
    [JsonPropertyName("minDaysRemaining")]
    public int? MinDaysRemaining { get; set; }

    [JsonPropertyName("warnDaysRemaining")]
    public int? WarnDaysRemaining { get; set; }
}

public sealed class ExpectedHeaderConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    // substring match
    [JsonPropertyName("contains")]
    public string Contains { get; set; } = "";
}

public sealed class ContentLengthConfig
{
    [JsonPropertyName("minBytes")]
    public int? MinBytes { get; set; }

    [JsonPropertyName("maxBytes")]
    public int? MaxBytes { get; set; }
}

public sealed class NotificationsConfig
{
    [JsonPropertyName("enabledChannels")]
    public List<string>? EnabledChannels { get; set; } // "email" | "sms"

    [JsonPropertyName("rules")]
    public NotificationRulesConfig? Rules { get; set; }

    [JsonPropertyName("templates")]
    public NotificationTemplatesConfig Templates { get; set; } = new();

    [JsonPropertyName("email")]
    public EmailSettings? Email { get; set; }

    [JsonPropertyName("sms")]
    public SmsSettings? Sms { get; set; }
}

public sealed class NotificationRulesConfig
{
    [JsonPropertyName("consecutiveFailures")]
    public int ConsecutiveFailures { get; set; } = 1;

    [JsonPropertyName("cooldownSeconds")]
    public int CooldownSeconds { get; set; } = 600;
}

public sealed class NotificationTemplatesConfig
{
    [JsonPropertyName("checkFailed")]
    public NotificationTemplate CheckFailed { get; set; } = new();

    [JsonPropertyName("recovered")]
    public NotificationTemplate Recovered { get; set; } = new();

    [JsonPropertyName("slowResponse")]
    public NotificationTemplate SlowResponse { get; set; } = new();

    [JsonPropertyName("certExpiring")]
    public NotificationTemplate CertExpiring { get; set; } = new();
}

public sealed class NotificationTemplate
{
    [JsonPropertyName("emailSubject")]
    public string EmailSubject { get; set; } = "";

    [JsonPropertyName("emailHtmlBody")]
    public string EmailHtmlBody { get; set; } = "";

    [JsonPropertyName("smsTextBody")]
    public string SmsTextBody { get; set; } = "";
}

public sealed class EmailSettings
{
    [JsonPropertyName("host")]
    public string Host { get; set; } = "";

    [JsonPropertyName("port")]
    public int Port { get; set; } = 587;

    [JsonPropertyName("enableSsl")]
    public bool EnableSsl { get; set; } = true;

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("from")]
    public string From { get; set; } = "";

    [JsonPropertyName("to")]
    public List<string> To { get; set; } = new();
}

public sealed class SmsSettings
{
    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = "";

    [JsonPropertyName("method")]
    public string Method { get; set; } = "POST";

    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = "application/json";

    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; set; }

    // Wraps the SMS body. Must include {{Body}} somewhere.
    [JsonPropertyName("bodyTemplate")]
    public string BodyTemplate { get; set; } = "{ \"message\": \"{{Body}}\" }";
}
