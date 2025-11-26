using WebsiteMonitor.Checks;
using WebsiteMonitor.Config;
using WebsiteMonitor.Logging;
using WebsiteMonitor.Storage;

namespace WebsiteMonitor.Notifications;

public sealed class NotificationPlanner
{
    private readonly SqliteStorage _storage;
    private readonly JsonConsoleLogger _log;

    public NotificationPlanner(SqliteStorage storage, JsonConsoleLogger log)
    {
        _storage = storage;
        _log = log;
    }

    public List<PlannedNotification> Plan(AppConfig cfg, IReadOnlyList<string> enabledChannels, List<CheckResult> results)
    {
        var planned = new List<PlannedNotification>();
        var rules = cfg.Notifications?.Rules ?? new NotificationRulesConfig();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        foreach (var r in results)
        {
            var state = _storage.GetOrCreateCheckState(r.CheckId);

            var wasOk = state.LastSucceeded;
            var isOk = r.Succeeded;

            // failure streak / state transitions
            if (isOk)
            {
                if (!wasOk)
                {
                    // Recovery
                    var dedupeBase = $"Recovered:{r.CheckId}";
                    if (!CooldownHit(enabledChannels, dedupeBase, rules.CooldownSeconds, now))
                    {
                        planned.Add(Build(cfg, r, "Recovered", extra: null));
                        state.LastNotifiedRecoveryUtcUnix = now;
                    }
                }

                state.LastSucceeded = true;
                state.FailureStreak = 0;
                state.LastChangedUtcUnix = now;
                state.LastNotifiedFailureStreak = 0; // reset streak notification marker
            }
            else
            {
                state.FailureStreak = wasOk ? 1 : (state.FailureStreak + 1);
                state.LastSucceeded = false;
                state.LastChangedUtcUnix = now;

                // send CheckFailed once when threshold hit
                if (state.FailureStreak >= rules.ConsecutiveFailures &&
                    state.LastNotifiedFailureStreak < rules.ConsecutiveFailures)
                {
                    var dedupeBase = $"CheckFailed:{r.CheckId}";
                    if (!CooldownHit(enabledChannels, dedupeBase, rules.CooldownSeconds, now))
                    {
                        planned.Add(Build(cfg, r, "CheckFailed", extra: new Dictionary<string, string>
                        {
                            ["FailureStreak"] = state.FailureStreak.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        }));
                        state.LastNotifiedFailureStreak = rules.ConsecutiveFailures;
                    }
                }
            }

            // SlowResponse notification (only if rule triggered and check config says Warn/Fail)
            if (r.SlowTriggered && cfg.Notifications is not null)
            {
                var dedupeBase = $"SlowResponse:{r.CheckId}";
                if (!CooldownHit(enabledChannels, dedupeBase, rules.CooldownSeconds, now))
                {
                    planned.Add(Build(cfg, r, "SlowResponse", extra: new Dictionary<string, string>
                    {
                        ["MaxLatencyMs"] = FindMaxLatency(cfg, r.CheckId) ?? ""
                    }));
                    state.LastNotifiedSlowUtcUnix = now;
                }
            }

            // Cert expiring
            if (r.CertExpiringTriggered && cfg.Notifications is not null)
            {
                var dedupeBase = $"CertExpiring:{r.CheckId}";
                if (!CooldownHit(enabledChannels, dedupeBase, rules.CooldownSeconds, now))
                {
                    planned.Add(Build(cfg, r, "CertExpiring", extra: null));
                    state.LastNotifiedCertUtcUnix = now;
                }
            }

            _storage.UpsertCheckState(state);
        }

        return planned;
    }

    private bool CooldownHit(IReadOnlyList<string> enabledChannels, string dedupeBase, int cooldownSeconds, long now)
    {
        foreach (var ch in enabledChannels)
        {
            var key = $"{ch}:{dedupeBase}";
            if (_storage.WasEventSentWithinCooldown(key, cooldownSeconds, now))
                return true;
        }
        return false;
    }

    private static PlannedNotification Build(AppConfig cfg, CheckResult r, string eventType, Dictionary<string, string>? extra)
    {
        var vars = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["UtcNow"] = DateTimeOffset.UtcNow.ToString("O"),
            ["AppName"] = cfg.App.Name ?? "WebsiteMonitor",
            ["Environment"] = cfg.App.Environment ?? "",
            ["CheckId"] = r.CheckId,
            ["CheckName"] = r.CheckName,
            ["Url"] = r.Url,
            ["StatusCode"] = r.StatusCode?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
            ["LatencyMs"] = r.LatencyMs?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
            ["Error"] = r.Error ?? "",
            ["RedirectCount"] = r.RedirectCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ResponseBytes"] = r.ResponseBytes?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "",
            ["CertDaysRemaining"] = r.CertDaysRemaining?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? ""
        };

        if (extra is not null)
        {
            foreach (var kv in extra)
                vars[kv.Key] = kv.Value;
        }

        return new PlannedNotification
        {
            EventType = eventType,
            CheckId = r.CheckId,
            CheckName = r.CheckName,
            Environment = cfg.App.Environment ?? "",
            Url = r.Url,
            Vars = vars
        };
    }

    private static string? FindMaxLatency(AppConfig cfg, string checkId)
    {
        var c = cfg.Checks.FirstOrDefault(x => x.Id.Equals(checkId, StringComparison.OrdinalIgnoreCase));
        return c?.MaxLatencyMs?.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
