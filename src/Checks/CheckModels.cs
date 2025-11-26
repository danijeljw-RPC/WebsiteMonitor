namespace WebsiteMonitor.Checks;

public sealed class CheckResult
{
    public string CheckId { get; set; } = "";
    public string CheckName { get; set; } = "";
    public string Url { get; set; } = "";
    public string Method { get; set; } = "";
    public string Severity { get; set; } = "Critical";

    public bool Succeeded { get; set; }
    public bool WarningOnly { get; set; }

    public int? StatusCode { get; set; }
    public long? LatencyMs { get; set; }
    public string? Error { get; set; }

    public int RedirectCount { get; set; }
    public int? ResponseBytes { get; set; }

    public int? CertDaysRemaining { get; set; }

    public bool SlowTriggered { get; set; }
    public bool CertExpiringTriggered { get; set; }
}
