namespace WebsiteMonitor.Storage;

public static class SqlSchema
{
    public static readonly string CreateTables = """
CREATE TABLE IF NOT EXISTS runs (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  started_utc_unix INTEGER NOT NULL,
  finished_utc_unix INTEGER NULL,
  host TEXT NOT NULL,
  app_version TEXT NOT NULL,
  app_name TEXT NOT NULL,
  environment TEXT NOT NULL,
  overall_status TEXT NULL,
  exit_code INTEGER NULL
);

CREATE TABLE IF NOT EXISTS check_results (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  run_id INTEGER NOT NULL,
  evaluated_utc_unix INTEGER NOT NULL,
  check_id TEXT NOT NULL,
  check_name TEXT NOT NULL,
  url TEXT NOT NULL,
  method TEXT NOT NULL,
  severity TEXT NOT NULL,
  succeeded INTEGER NOT NULL,
  warning_only INTEGER NOT NULL,
  status_code INTEGER NULL,
  latency_ms INTEGER NULL,
  redirect_count INTEGER NOT NULL,
  response_bytes INTEGER NULL,
  cert_days_remaining INTEGER NULL,
  error TEXT NULL,
  FOREIGN KEY (run_id) REFERENCES runs(id)
);

CREATE TABLE IF NOT EXISTS check_state (
  check_id TEXT PRIMARY KEY,
  last_succeeded INTEGER NOT NULL,
  last_changed_utc_unix INTEGER NOT NULL,
  failure_streak INTEGER NOT NULL,
  last_notified_failure_streak INTEGER NOT NULL,
  last_notified_recovery_utc_unix INTEGER NOT NULL,
  last_notified_slow_utc_unix INTEGER NOT NULL,
  last_notified_cert_utc_unix INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS notification_events (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  occurred_utc_unix INTEGER NOT NULL,
  channel TEXT NOT NULL,
  event_type TEXT NOT NULL,
  check_id TEXT NOT NULL,
  check_name TEXT NOT NULL,
  dedupe_key TEXT NOT NULL,
  sent_to TEXT NOT NULL,
  subject TEXT NOT NULL,
  body TEXT NOT NULL,
  success INTEGER NOT NULL,
  error TEXT NULL
);
""";

    public static readonly string CreateIndexes = """
CREATE INDEX IF NOT EXISTS ix_runs_started ON runs(started_utc_unix DESC);
CREATE INDEX IF NOT EXISTS ix_results_check_time ON check_results(check_id, evaluated_utc_unix DESC);
CREATE INDEX IF NOT EXISTS ix_results_run ON check_results(run_id);
CREATE INDEX IF NOT EXISTS ix_notifications_dedupe_time ON notification_events(dedupe_key, occurred_utc_unix DESC);
""";
}
