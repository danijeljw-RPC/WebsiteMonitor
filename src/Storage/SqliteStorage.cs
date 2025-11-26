using Microsoft.Data.Sqlite;

namespace WebsiteMonitor.Storage;

public sealed class SqliteStorage : IDisposable
{
    private readonly string _dbPath;
    private SqliteConnection? _conn;

    public SqliteStorage(string dbPath)
    {
        _dbPath = dbPath;
    }

    public void Open()
    {
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        _conn = new SqliteConnection(cs);
        _conn.Open();

        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys=ON;";
        cmd.ExecuteNonQuery();
    }

    public void EnsureSchema()
    {
        EnsureOpen();

        using (var cmd = _conn!.CreateCommand())
        {
            cmd.CommandText = SqlSchema.CreateTables;
            cmd.ExecuteNonQuery();
        }

        using (var cmd = _conn!.CreateCommand())
        {
            cmd.CommandText = SqlSchema.CreateIndexes;
            cmd.ExecuteNonQuery();
        }
    }

    public long InsertRunStarted(RunStartedRow row)
    {
        EnsureOpen();

        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = """
INSERT INTO runs(started_utc_unix, host, app_version, app_name, environment)
VALUES ($started, $host, $ver, $app, $env);
SELECT last_insert_rowid();
""";
        cmd.Parameters.AddWithValue("$started", row.StartedUtcUnix);
        cmd.Parameters.AddWithValue("$host", row.Host);
        cmd.Parameters.AddWithValue("$ver", row.AppVersion);
        cmd.Parameters.AddWithValue("$app", row.AppName);
        cmd.Parameters.AddWithValue("$env", row.Environment);

        var id = (long)cmd.ExecuteScalar()!;
        return id;
    }

    public void UpdateRunFinished(long runId, RunFinishedRow row)
    {
        EnsureOpen();
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = """
UPDATE runs
SET finished_utc_unix=$finished, overall_status=$status, exit_code=$code
WHERE id=$id;
""";
        cmd.Parameters.AddWithValue("$finished", row.FinishedUtcUnix);
        cmd.Parameters.AddWithValue("$status", row.OverallStatus);
        cmd.Parameters.AddWithValue("$code", row.ExitCode);
        cmd.Parameters.AddWithValue("$id", runId);
        cmd.ExecuteNonQuery();
    }

    public void InsertCheckResult(long runId, WebsiteMonitor.Checks.CheckResult r)
    {
        EnsureOpen();
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = """
INSERT INTO check_results(
  run_id, evaluated_utc_unix, check_id, check_name, url, method, severity,
  succeeded, warning_only, status_code, latency_ms, redirect_count, response_bytes, cert_days_remaining, error
)
VALUES(
  $run, $ts, $cid, $cname, $url, $method, $sev,
  $ok, $warn, $status, $lat, $redir, $bytes, $cert, $err
);
""";
        cmd.Parameters.AddWithValue("$run", runId);
        cmd.Parameters.AddWithValue("$ts", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$cid", r.CheckId);
        cmd.Parameters.AddWithValue("$cname", r.CheckName);
        cmd.Parameters.AddWithValue("$url", r.Url);
        cmd.Parameters.AddWithValue("$method", r.Method);
        cmd.Parameters.AddWithValue("$sev", r.Severity);
        cmd.Parameters.AddWithValue("$ok", r.Succeeded ? 1 : 0);
        cmd.Parameters.AddWithValue("$warn", r.WarningOnly ? 1 : 0);
        cmd.Parameters.AddWithValue("$status", (object?)r.StatusCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$lat", (object?)r.LatencyMs ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$redir", r.RedirectCount);
        cmd.Parameters.AddWithValue("$bytes", (object?)r.ResponseBytes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$cert", (object?)r.CertDaysRemaining ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$err", (object?)r.Error ?? DBNull.Value);

        cmd.ExecuteNonQuery();
    }

    public CheckStateRow GetOrCreateCheckState(string checkId)
    {
        EnsureOpen();

        using (var cmd = _conn!.CreateCommand())
        {
            cmd.CommandText = """
SELECT check_id, last_succeeded, last_changed_utc_unix, failure_streak, last_notified_failure_streak,
       last_notified_recovery_utc_unix, last_notified_slow_utc_unix, last_notified_cert_utc_unix
FROM check_state
WHERE check_id = $id;
""";
            cmd.Parameters.AddWithValue("$id", checkId);

            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                return new CheckStateRow
                {
                    CheckId = r.GetString(0),
                    LastSucceeded = r.GetInt32(1) != 0,
                    LastChangedUtcUnix = r.GetInt64(2),
                    FailureStreak = r.GetInt32(3),
                    LastNotifiedFailureStreak = r.GetInt32(4),
                    LastNotifiedRecoveryUtcUnix = r.GetInt64(5),
                    LastNotifiedSlowUtcUnix = r.GetInt64(6),
                    LastNotifiedCertUtcUnix = r.GetInt64(7)
                };
            }
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var init = new CheckStateRow
        {
            CheckId = checkId,
            LastSucceeded = true,
            LastChangedUtcUnix = now,
            FailureStreak = 0,
            LastNotifiedFailureStreak = 0,
            LastNotifiedRecoveryUtcUnix = 0,
            LastNotifiedSlowUtcUnix = 0,
            LastNotifiedCertUtcUnix = 0
        };

        UpsertCheckState(init);
        return init;
    }

    public void UpsertCheckState(CheckStateRow row)
    {
        EnsureOpen();
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = """
INSERT INTO check_state(
  check_id, last_succeeded, last_changed_utc_unix, failure_streak,
  last_notified_failure_streak, last_notified_recovery_utc_unix, last_notified_slow_utc_unix, last_notified_cert_utc_unix
)
VALUES(
  $id, $ok, $changed, $streak,
  $notFail, $notRec, $notSlow, $notCert
)
ON CONFLICT(check_id) DO UPDATE SET
  last_succeeded=excluded.last_succeeded,
  last_changed_utc_unix=excluded.last_changed_utc_unix,
  failure_streak=excluded.failure_streak,
  last_notified_failure_streak=excluded.last_notified_failure_streak,
  last_notified_recovery_utc_unix=excluded.last_notified_recovery_utc_unix,
  last_notified_slow_utc_unix=excluded.last_notified_slow_utc_unix,
  last_notified_cert_utc_unix=excluded.last_notified_cert_utc_unix;
""";
        cmd.Parameters.AddWithValue("$id", row.CheckId);
        cmd.Parameters.AddWithValue("$ok", row.LastSucceeded ? 1 : 0);
        cmd.Parameters.AddWithValue("$changed", row.LastChangedUtcUnix);
        cmd.Parameters.AddWithValue("$streak", row.FailureStreak);
        cmd.Parameters.AddWithValue("$notFail", row.LastNotifiedFailureStreak);
        cmd.Parameters.AddWithValue("$notRec", row.LastNotifiedRecoveryUtcUnix);
        cmd.Parameters.AddWithValue("$notSlow", row.LastNotifiedSlowUtcUnix);
        cmd.Parameters.AddWithValue("$notCert", row.LastNotifiedCertUtcUnix);

        cmd.ExecuteNonQuery();
    }

    public bool WasEventSentWithinCooldown(string dedupeKey, int cooldownSeconds, long nowUtcUnix)
    {
        EnsureOpen();
        if (cooldownSeconds <= 0) return false;

        var minTs = nowUtcUnix - cooldownSeconds;

        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = """
SELECT occurred_utc_unix
FROM notification_events
WHERE dedupe_key = $k
ORDER BY occurred_utc_unix DESC
LIMIT 1;
""";
        cmd.Parameters.AddWithValue("$k", dedupeKey);

        var val = cmd.ExecuteScalar();
        if (val is null || val == DBNull.Value) return false;

        var last = Convert.ToInt64(val);
        return last >= minTs;
    }

    public void InsertNotificationEvent(NotificationEventRow row)
    {
        EnsureOpen();
        using var cmd = _conn!.CreateCommand();
        cmd.CommandText = """
INSERT INTO notification_events(
  occurred_utc_unix, channel, event_type, check_id, check_name, dedupe_key, sent_to, subject, body, success, error
)
VALUES(
  $ts, $ch, $et, $cid, $cn, $dk, $to, $sub, $body, $ok, $err
);
""";
        cmd.Parameters.AddWithValue("$ts", row.OccurredUtcUnix);
        cmd.Parameters.AddWithValue("$ch", row.Channel);
        cmd.Parameters.AddWithValue("$et", row.EventType);
        cmd.Parameters.AddWithValue("$cid", row.CheckId);
        cmd.Parameters.AddWithValue("$cn", row.CheckName);
        cmd.Parameters.AddWithValue("$dk", row.DedupeKey);
        cmd.Parameters.AddWithValue("$to", row.SentTo);
        cmd.Parameters.AddWithValue("$sub", row.Subject);
        cmd.Parameters.AddWithValue("$body", row.Body);
        cmd.Parameters.AddWithValue("$ok", row.Success ? 1 : 0);
        cmd.Parameters.AddWithValue("$err", (object?)row.Error ?? DBNull.Value);

        cmd.ExecuteNonQuery();
    }

    private void EnsureOpen()
    {
        if (_conn is null) throw new InvalidOperationException("SQLite connection is not open");
    }

    public void Dispose()
    {
        _conn?.Dispose();
        _conn = null;
    }
}

public sealed class RunStartedRow
{
    public long StartedUtcUnix { get; set; }
    public string Host { get; set; } = "";
    public string AppVersion { get; set; } = "";
    public string AppName { get; set; } = "";
    public string Environment { get; set; } = "";
}

public sealed class RunFinishedRow
{
    public long FinishedUtcUnix { get; set; }
    public string OverallStatus { get; set; } = "";
    public int ExitCode { get; set; }
}

public sealed class CheckStateRow
{
    public string CheckId { get; set; } = "";
    public bool LastSucceeded { get; set; }
    public long LastChangedUtcUnix { get; set; }
    public int FailureStreak { get; set; }
    public int LastNotifiedFailureStreak { get; set; }
    public long LastNotifiedRecoveryUtcUnix { get; set; }
    public long LastNotifiedSlowUtcUnix { get; set; }
    public long LastNotifiedCertUtcUnix { get; set; }
}

public sealed class NotificationEventRow
{
    public long OccurredUtcUnix { get; set; }
    public string Channel { get; set; } = "";
    public string EventType { get; set; } = "";
    public string CheckId { get; set; } = "";
    public string CheckName { get; set; } = "";
    public string DedupeKey { get; set; } = "";
    public string SentTo { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public bool Success { get; set; }
    public string? Error { get; set; }
}
