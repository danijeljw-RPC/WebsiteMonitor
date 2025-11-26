using WebsiteMonitor.Cli;
using WebsiteMonitor.Config;
using WebsiteMonitor.Logging;
using WebsiteMonitor.Notifications;
using WebsiteMonitor.Storage;
using WebsiteMonitor.Checks;

namespace WebsiteMonitor;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var log = new JsonConsoleLogger();

        CliOptions opt;
        try
        {
            opt = ArgsParser.Parse(args);
        }
        catch (Exception ex)
        {
            log.Error("cli_parse_failed", w =>
            {
                w.WriteString("error", ex.Message);
            });
            ArgsParser.PrintHelp(Console.Out);
            return 1;
        }

        if (opt.ShowHelp)
        {
            ArgsParser.PrintHelp(Console.Out);
            return 0;
        }

        if (opt.ShowVersion)
        {
            Console.WriteLine(AppVersion.GetVersionString());
            return 0;
        }

        try
        {
            if (opt.GenerateJsonConfig)
            {
                var path = opt.ConfigPath ?? Path.Combine(Environment.CurrentDirectory, "config.json");
                File.WriteAllText(path, Defaults.JsonTemplate, Defaults.Utf8NoBom);
                log.Info("config_template_written", w =>
                {
                    w.WriteString("format", "json");
                    w.WriteString("path", path);
                });
                return 0;
            }

            if (opt.GenerateYamlConfig)
            {
                var path = opt.ConfigPath ?? Path.Combine(Environment.CurrentDirectory, "config.yaml");
                File.WriteAllText(path, Defaults.YamlTemplate, Defaults.Utf8NoBom);
                log.Info("config_template_written", w =>
                {
                    w.WriteString("format", "yaml");
                    w.WriteString("path", path);
                });
                return 0;
            }

            var configPath = ResolveConfigPath(opt.ConfigPath);

            if (!File.Exists(configPath))
            {
                log.Error("config_error", w =>
                {
                    w.WriteString("error", $"Config not found: {configPath}");
                    w.WriteString("hint", "Run with --help, or generate a template config with --generate-yaml-config / --generate-json-config");
                });

                Console.Error.WriteLine($"Config not found: {configPath}");
                Console.Error.WriteLine("Run with --help, or generate a template config with --generate-yaml-config / --generate-json-config");
                return 1;
            }

            var cfg = ConfigLoader.Load(configPath);


            // Env var expansion (deterministic, no reflection)
            EnvVarExpander.ExpandInPlace(cfg);

            // Validate
            ConfigValidator.ValidateOrThrow(cfg);

            // Notifications enabled channels:
            var enabledChannels = NotificationChannelSelector.ResolveEnabledChannels(
                cfg.Notifications?.EnabledChannels,
                opt.EnableEmail,
                opt.EnableSms);

            var storage = new SqliteStorage(cfg.Sqlite.DbPath);
            storage.Open();
            storage.EnsureSchema();

            var runStartedUtc = DateTimeOffset.UtcNow;
            var runId = storage.InsertRunStarted(new RunStartedRow
            {
                StartedUtcUnix = runStartedUtc.ToUnixTimeSeconds(),
                Host = Environment.MachineName,
                AppVersion = AppVersion.GetVersionString(),
                Environment = cfg.App.Environment ?? "",
                AppName = cfg.App.Name ?? "WebsiteMonitor"
            });

            log.Info("run_started", w =>
            {
                w.WriteNumber("runId", runId);
                w.WriteString("appName", cfg.App.Name);
                w.WriteString("environment", cfg.App.Environment);
                w.WriteString("host", Environment.MachineName);
            });

            var executor = new CheckExecutor(log);

            var results = new List<CheckResult>();
            foreach (var check in cfg.Checks)
            {
                if (!check.Enabled) continue;

                log.Info("check_start", w =>
                {
                    w.WriteString("checkId", check.Id);
                    w.WriteString("checkName", check.Name);
                    w.WriteString("url", check.Url);
                });
                
                var res = await executor.ExecuteAsync(check);
                results.Add(res);

                storage.InsertCheckResult(runId, res);
                
                log.Info("check_end", w =>
                {
                    w.WriteString("checkId", check.Id);
                    w.WriteString("succeeded", res.Succeeded.ToString());
                    w.WriteString("statusCode", res.StatusCode.ToString());
                    w.WriteString("latencyMs", res.LatencyMs.ToString());
                    w.WriteString("severity", check.Severity);
                    w.WriteString("error", res.Error);
                });
            }

            // Plan notifications based on state changes + rules
            var planner = new NotificationPlanner(storage, log);
            var planned = planner.Plan(cfg, enabledChannels, results);

            // Send notifications
            var channels = NotificationChannelFactory.CreateChannels(enabledChannels, cfg.Notifications, log);

            foreach (var evt in planned)
            {
                foreach (var channel in channels)
                {
                    if (!channel.CanHandle(evt)) continue;

                    var sendAttempt = await channel.SendAsync(evt);

                    storage.InsertNotificationEvent(new NotificationEventRow
                    {
                        OccurredUtcUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Channel = channel.Kind,
                        EventType = evt.EventType,
                        CheckId = evt.CheckId,
                        CheckName = evt.CheckName,
                        DedupeKey = evt.DedupeKeyForChannel(channel.Kind),
                        SentTo = sendAttempt.SentTo,
                        Subject = sendAttempt.Subject,
                        Body = sendAttempt.Body,
                        Success = sendAttempt.Success,
                        Error = sendAttempt.Error
                    });



                    log.Info("notification_attempt", w =>
                    {
                        w.WriteString("channel", channel.Kind);
                        w.WriteString("eventType", evt.EventType);
                        w.WriteString("checkId", evt.CheckId);
                        w.WriteBoolean("success", sendAttempt.Success);
                        w.WriteString("error", sendAttempt.Error);
                    });
                }
            }

            // Finish run
            var finishedUtc = DateTimeOffset.UtcNow;

            var exitCode = ExitCodeCalculator.Calculate(cfg, results);

            storage.UpdateRunFinished(runId, new RunFinishedRow
            {
                FinishedUtcUnix = finishedUtc.ToUnixTimeSeconds(),
                ExitCode = exitCode,
                OverallStatus = exitCode == 0 ? "OK" : (exitCode == 2 ? "CRITICAL_FAILED" : "ERROR")
            });

            log.Info("run_finished", w =>
            {
                w.WriteString("exitCode", exitCode.ToString());
            });

            return exitCode;
        }
        catch (ConfigException cex)
        {
            log.Error("config_error", w =>
            {
                w.WriteString("error", cex.Message);
                w.WriteString("hint", "Run with --help for usage.");
            });

            Console.Error.WriteLine(cex.Message);
            Console.Error.WriteLine("Run with --help for usage.");
            return 1;
        }
        catch (Exception ex)
        {
            log.Error("fatal_error", w =>
            {
                w.WriteString("error", $"{ex.GetType().Name}: {ex.Message}");
            });
            return 1;
        }
    }

    private static string ResolveConfigPath(string? argPath)
    {
        if (!string.IsNullOrWhiteSpace(argPath))
            return Path.GetFullPath(argPath);

        var cwd = Environment.CurrentDirectory;

        var candidates = new[]
        {
            Path.Combine(cwd, "config.yaml"),
            Path.Combine(cwd, "config.yml"),
            Path.Combine(cwd, "config.json"),
        };

        foreach (var c in candidates)
            if (File.Exists(c))
                return c;

        // default used for the error message if none exist
        return candidates[0];
    }
}

file static class ExitCodeCalculator
{
    public static int Calculate(AppConfig cfg, List<CheckResult> results)
    {
        // 2 if any Critical check failed; otherwise 0.
        // (Warnings/Info do not flip exit code.)
        var anyCriticalFailed = results.Any(r =>
            !r.Succeeded &&
            string.Equals(r.Severity, "Critical", StringComparison.OrdinalIgnoreCase));

        return anyCriticalFailed ? 2 : 0;
    }
}

