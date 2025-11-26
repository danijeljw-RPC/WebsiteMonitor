# WebsiteMonitor (.NET 9) — cron-friendly website checks + SQLite + notifications (AOT-ready)

A single-run console app intended to be executed by `crontab` every minute:

- loads config (JSON or YAML)
- executes checks once
- writes results to SQLite
- sends notifications on state changes / sustained windows / cooldown
- exits with a cron-friendly exit code

## Features

### Checks (per configured item)

- HTTP status range validation (default 200–299)
- latency measurement (ms) with optional threshold + mode (`Warn` vs `Fail`)
- content validation: `contains` or `regex`
- redirect budget (detect unexpected loops)
- expected header/value present
- content length min/max
- optional login flow (form POST + cookies) then validate target page
- optional TLS certificate expiry probe (days remaining)

### Storage (SQLite via `Microsoft.Data.Sqlite`)

- run records (`runs`)
- per-check results (`check_results`)
- per-check state (`check_state`) for state-change + sustained-window logic
- notification send events (`notification_events`) to dedupe/observe deliveries

### Notifications (pluggable)

- Email (SMTP) with HTML templates
- SMS (generic HTTP API) with templated body/headers

### Triggers

- OK → FAIL: `CheckFailed` (optionally after N consecutive failures)
- FAIL → OK: `Recovered`
- Slow response: `SlowResponse`
- TLS expiring: `CertExpiring`

### Exit codes

- `0` = all checks OK (or only warnings/info failed)
- `2` = any *Critical* check fails
- `1` = internal error (bad config / DB failure / unexpected app error)

---

## Quick start

### Generate config templates

JSON:

```bash
./WebsiteMonitor --generate-json-config
# writes ./config.json
```

YAML:

```bash
./WebsiteMonitor --generate-yaml-config
# writes ./config.yaml
```

### Run once

```bash
./WebsiteMonitor --config=./config.yaml -email -sms
```

If you omit `-email/-sms`, enabled channels come from `notifications.enabledChannels`.

### Cron (every minute)

Example (Linux):

```cron
* * * * * /opt/website-monitor/WebsiteMonitor --config=/opt/website-monitor/config.yaml -email >> /var/log/website-monitor.log 2>&1
```

---

## CLI usage

`--help` output (example):

```
WebsiteMonitor - cron-friendly website monitor (.NET 9 / AOT-friendly)

Usage:
  WebsiteMonitor [options]

Options:
  -h, --help                    Show help and exit (0)
  -v, --version                 Show version and exit (0)
  --config=<path>               Path to config.json/config.yaml/config.yml
  --generate-json-config        Write a default JSON config template and exit (0)
  --generate-yaml-config        Write a default YAML config template and exit (0)
  -email                        Enable Email notifications (SMTP) this run
  -sms                          Enable SMS notifications (HTTP API) this run

Exit codes:
  0 = OK (or warnings only)
  2 = Critical check failed
  1 = internal error
```

---

## Build + NativeAOT publish

Requires .NET 9 SDK.

Linux x64 AOT:

```bash
dotnet publish -c Release -r linux-x64 \
  /p:PublishAot=true /p:StripSymbols=true \
  -o ./publish/linux-x64
```

Windows x64 AOT:

```powershell
dotnet publish -c Release -r win-x64 `
  /p:PublishAot=true /p:StripSymbols=true `
  -o .\publish\win-x64
```

---

## Config schema

The same object model is used for JSON and YAML.

Top level:

- `app`: name/environment/run interval hint/timezone (timezone is informational; stored timestamps are UTC)
- `sqlite`: SQLite database path
- `checks`: list of checks
- `notifications`: channels/settings/templates/rules

### Config fields (minimum set)

#### `app`

- `name` (string)
- `environment` (string)
- `runIntervalHintSeconds` (int)
- `timezone` (string, optional)

#### `sqlite`

- `dbPath` (string)

#### `checks[]`

Per-check:

- `id` (string, unique)
- `name` (string)
- `enabled` (bool)
- `severity` (`Info|Warning|Critical`)
- `url` (string)
- `method` (`GET|POST|HEAD`)
- `timeoutSeconds` (int)
- `expectedStatus`: `{ min: int, max: int }` (default 200–299)
- `redirects` (optional): `{ maxRedirects: int }`
- `maxLatencyMs` (optional long)
- `latencyMode` (optional): `Ignore|Warn|Fail` (default `Fail` if `maxLatencyMs` is set)
- `contentRule` (optional): `{ type: "contains"|"regex", value: "..." }`
- `headers` (optional): list of `{ name: "Header-Name", contains: "substring" }`
- `contentLength` (optional): `{ minBytes: int?, maxBytes: int? }`
- `maxBodyBytes` (optional): cap body read to avoid huge pages (default 1 MiB)
- `login` (optional):
  - `loginUrl`
  - `username` / `password` (supports `${ENV_VAR}` expansion)
  - `usernameField` / `passwordField`
  - `additionalFields` (map)
  - `successIndicator` (optional rule) - checks login response HTML
  - `postLoginUrl` (optional) - checks after login
  - `postLoginRule` (optional rule) - checks target page HTML
- `tls` (optional):
  - `warnDaysRemaining` (int?)
  - `minDaysRemaining` (int?) (breaching this fails the check)

#### `notifications`

- `enabledChannels`: list of `email|sms` (default used if CLI doesn’t specify `-email/-sms`)
- `rules`:
  - `consecutiveFailures` (int, default 1)
  - `cooldownSeconds` (int, default 600)
- `templates` for:
  - `checkFailed`
  - `recovered`
  - `slowResponse`
  - `certExpiring`
  Each template contains:
  - `emailSubject`
  - `emailHtmlBody`
  - `smsTextBody`
- `email` (SMTP):
  - `host`, `port`, `enableSsl`
  - `username`, `password` (supports `${ENV_VAR}`)
  - `from`
  - `to` (list)
- `sms` (HTTP API):
  - `endpoint`
  - `method` (POST/PUT/PATCH)
  - `contentType`
  - `headers` (map, supports `${ENV_VAR}`)
  - `bodyTemplate` (must contain `{{Body}}`)

### Template variables

All templates can use:

- `{{UtcNow}}`
- `{{AppName}}`
- `{{Environment}}`
- `{{CheckId}}`
- `{{CheckName}}`
- `{{Url}}`
- `{{StatusCode}}`
- `{{LatencyMs}}`
- `{{Error}}`
- `{{RedirectCount}}`
- `{{ResponseBytes}}`
- `{{CertDaysRemaining}}`
- `{{MaxLatencyMs}}` (provided for slow notifications)
- `{{FailureStreak}}` (provided for failure notifications)

---

## JSON config example

```json
{
  "app": {
    "name": "WebsiteMonitor",
    "environment": "prod",
    "runIntervalHintSeconds": 60,
    "timezone": "Australia/Sydney"
  },
  "sqlite": {
    "dbPath": "./monitor.db"
  },
  "checks": [
    {
      "id": "home",
      "name": "Homepage",
      "enabled": true,
      "severity": "Critical",
      "url": "https://example.com/",
      "method": "GET",
      "timeoutSeconds": 15,
      "expectedStatus": { "min": 200, "max": 299 },
      "redirects": { "maxRedirects": 5 },
      "maxLatencyMs": 1500,
      "latencyMode": "Warn",
      "contentRule": { "type": "contains", "value": "Example Domain" },
      "headers": [
        { "name": "Content-Type", "contains": "text/html" }
      ],
      "contentLength": { "minBytes": 50, "maxBytes": 500000 },
      "tls": { "warnDaysRemaining": 30, "minDaysRemaining": 7 },
      "maxBodyBytes": 1048576
    }
  ],
  "notifications": {
    "enabledChannels": [ "email" ],
    "rules": { "consecutiveFailures": 2, "cooldownSeconds": 600 },
    "templates": {
      "checkFailed": {
        "emailSubject": "[{{Environment}}] FAIL: {{CheckName}}",
        "emailHtmlBody": "<h3>FAIL</h3><p><b>{{CheckName}}</b> - {{Url}}</p><p>Status: {{StatusCode}} Latency: {{LatencyMs}}ms</p><pre>{{Error}}</pre><p>{{UtcNow}}</p>",
        "smsTextBody": "FAIL {{Environment}} {{CheckName}} {{StatusCode}} {{LatencyMs}}ms {{Url}}"
      },
      "recovered": {
        "emailSubject": "[{{Environment}}] OK: {{CheckName}} recovered",
        "emailHtmlBody": "<h3>RECOVERED</h3><p>{{CheckName}} - {{Url}}</p><p>{{UtcNow}}</p>",
        "smsTextBody": "OK {{Environment}} {{CheckName}} recovered {{Url}}"
      },
      "slowResponse": {
        "emailSubject": "[{{Environment}}] SLOW: {{CheckName}}",
        "emailHtmlBody": "<h3>SLOW</h3><p>{{CheckName}} {{LatencyMs}}ms (max {{MaxLatencyMs}}ms)</p><p>{{Url}}</p><p>{{UtcNow}}</p>",
        "smsTextBody": "SLOW {{Environment}} {{CheckName}} {{LatencyMs}}ms/{{MaxLatencyMs}}ms {{Url}}"
      },
      "certExpiring": {
        "emailSubject": "[{{Environment}}] CERT: {{CheckName}} expiring",
        "emailHtmlBody": "<h3>CERT EXPIRING</h3><p>{{CheckName}} {{Url}}</p><p>Days remaining: {{CertDaysRemaining}}</p><p>{{UtcNow}}</p>",
        "smsTextBody": "CERT {{Environment}} {{CheckName}} {{CertDaysRemaining}}d {{Url}}"
      }
    },
    "email": {
      "host": "smtp.example.com",
      "port": 587,
      "enableSsl": true,
      "username": "${SMTP_USER}",
      "password": "${SMTP_PASS}",
      "from": "monitor@example.com",
      "to": [ "ops@example.com" ]
    },
    "sms": {
      "endpoint": "https://sms-gateway.example.com/messages",
      "method": "POST",
      "contentType": "application/json",
      "headers": { "Authorization": "Bearer ${SMS_TOKEN}" },
      "bodyTemplate": "{ \"to\": \"+61400111222\", \"message\": \"{{Body}}\" }"
    }
  }
}
```

---

## YAML config example

```yaml
app:
  name: WebsiteMonitor
  environment: prod
  runIntervalHintSeconds: 60
  timezone: Australia/Sydney

sqlite:
  dbPath: ./monitor.db

checks:
  - id: home
    name: Homepage
    enabled: true
    severity: Critical
    url: https://example.com/
    method: GET
    timeoutSeconds: 15
    expectedStatus:
      min: 200
      max: 299
    redirects:
      maxRedirects: 5
    maxLatencyMs: 1500
    latencyMode: Warn
    contentRule:
      type: contains
      value: Example Domain
    headers:
      - name: Content-Type
        contains: text/html
    contentLength:
      minBytes: 50
      maxBytes: 500000
    tls:
      warnDaysRemaining: 30
      minDaysRemaining: 7
    maxBodyBytes: 1048576

notifications:
  enabledChannels:
    - email
  rules:
    consecutiveFailures: 2
    cooldownSeconds: 600
  templates:
    checkFailed:
      emailSubject: "[{{Environment}}] FAIL: {{CheckName}}"
      emailHtmlBody: "<h3>FAIL</h3><p><b>{{CheckName}}</b> - {{Url}}</p><p>Status: {{StatusCode}} Latency: {{LatencyMs}}ms</p><pre>{{Error}}</pre><p>{{UtcNow}}</p>"
      smsTextBody: "FAIL {{Environment}} {{CheckName}} {{StatusCode}} {{LatencyMs}}ms {{Url}}"
    recovered:
      emailSubject: "[{{Environment}}] OK: {{CheckName}} recovered"
      emailHtmlBody: "<h3>RECOVERED</h3><p>{{CheckName}} - {{Url}}</p><p>{{UtcNow}}</p>"
      smsTextBody: "OK {{Environment}} {{CheckName}} recovered {{Url}}"
    slowResponse:
      emailSubject: "[{{Environment}}] SLOW: {{CheckName}}"
      emailHtmlBody: "<h3>SLOW</h3><p>{{CheckName}} {{LatencyMs}}ms (max {{MaxLatencyMs}}ms)</p><p>{{Url}}</p><p>{{UtcNow}}</p>"
      smsTextBody: "SLOW {{Environment}} {{CheckName}} {{LatencyMs}}ms/{{MaxLatencyMs}}ms {{Url}}"
    certExpiring:
      emailSubject: "[{{Environment}}] CERT: {{CheckName}} expiring"
      emailHtmlBody: "<h3>CERT EXPIRING</h3><p>{{CheckName}} {{Url}}</p><p>Days remaining: {{CertDaysRemaining}}</p><p>{{UtcNow}}</p>"
      smsTextBody: "CERT {{Environment}} {{CheckName}} {{CertDaysRemaining}}d {{Url}}"
  email:
    host: smtp.example.com
    port: 587
    enableSsl: true
    username: ${SMTP_USER}
    password: ${SMTP_PASS}
    from: monitor@example.com
    to:
      - ops@example.com
  sms:
    endpoint: https://sms-gateway.example.com/messages
    method: POST
    contentType: application/json
    headers:
      Authorization: "Bearer ${SMS_TOKEN}"
    bodyTemplate: "{ \"to\": \"+61400111222\", \"message\": \"{{Body}}\" }"
```

---

## SQLite schema (tables + indexes)

Created automatically at startup (`CREATE TABLE IF NOT EXISTS`).

Tables:

- `runs`
  - one row per invocation (start/end, host, version, environment, exit code)
- `check_results`
  - one row per check per run (status, latency, body size, cert days, error)
- `check_state`
  - per-check last known state and failure streak for notification rules
- `notification_events`
  - audit trail + dedupe/cooldown

Indexes:

- recent runs: `ix_runs_started`
- per-check recent results: `ix_results_check_time`
- results by run: `ix_results_run`
- notification dedupe/time: `ix_notifications_dedupe_time`

---

## Notes / constraints

- YAML support uses a minimal parser (maps/lists/scalars/comments). Keep YAML clean and consistent indentation.
- Secrets should **not** be hardcoded. Use `${ENV_VAR}` expansion.
- The app avoids logging secrets by design (templates may include `{{Error}}` but not credential values).
- SMTP auth uses basic username/password if set. For providers that disable basic auth, use an SMTP relay or provider-specific credentials.
