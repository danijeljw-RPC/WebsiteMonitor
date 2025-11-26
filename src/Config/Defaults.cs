using System.Text;

namespace WebsiteMonitor.Config;

public static class Defaults
{
    public static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    // Kept as literal strings to avoid reflection when generating templates.
    public static string JsonTemplate => """
{
  "app": {
    "name": "WebsiteMonitor",
    "environment": "local",
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
      "headers": {
        "Authorization": "Bearer ${SMS_TOKEN}"
      },
      "bodyTemplate": "{ \\"to\\": \\"+61400111222\\", \\"message\\": \\"{{Body}}\\" }"
    }
  }
}
""";

    public static string YamlTemplate => """
app:
  name: WebsiteMonitor
  environment: local
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
    bodyTemplate: '{ "to": "+61400111222", "message": "{{Body}}" }'
""";
}
