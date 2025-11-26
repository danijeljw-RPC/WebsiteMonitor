using WebsiteMonitor.Config;

namespace WebsiteMonitor.Cli;

public static class ArgsParser
{
    public static CliOptions Parse(string[] args)
    {
        var opt = new CliOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];

            if (a is "-h" or "--help")
            {
                opt.ShowHelp = true;
                continue;
            }

            if (a is "-v" or "--version")
            {
                opt.ShowVersion = true;
                continue;
            }

            if (a is "-email")
            {
                opt.EnableEmail = true;
                continue;
            }

            if (a is "-sms")
            {
                opt.EnableSms = true;
                continue;
            }

            if (a.StartsWith("--config=", StringComparison.OrdinalIgnoreCase))
            {
                opt.ConfigPath = a.Substring("--config=".Length).Trim();
                continue;
            }

            if (a.Equals("--config", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length) throw new ConfigException("Missing value for --config");
                opt.ConfigPath = args[++i].Trim();
                continue;
            }

            if (a.Equals("--generate-json-config", StringComparison.OrdinalIgnoreCase))
            {
                opt.GenerateJsonConfig = true;
                continue;
            }

            if (a.Equals("--generate-yaml-config", StringComparison.OrdinalIgnoreCase))
            {
                opt.GenerateYamlConfig = true;
                continue;
            }

            throw new ConfigException($"Unknown argument: {a}");
        }

        if (opt.GenerateJsonConfig && opt.GenerateYamlConfig)
            throw new ConfigException("Choose only one of --generate-json-config or --generate-yaml-config");

        return opt;
    }

    public static void PrintHelp(TextWriter w)
    {
        w.WriteLine("WebsiteMonitor - cron-friendly website monitor (.NET 10 / AOT-friendly)");
        w.WriteLine();
        w.WriteLine("Usage:");
        w.WriteLine("  WebsiteMonitor [options]");
        w.WriteLine();
        w.WriteLine("Options:");
        w.WriteLine("  -h, --help                    Show help and exit (0)");
        w.WriteLine("  -v, --version                 Show version and exit (0)");
        w.WriteLine("  --config=<path>               Path to config.json/config.yaml/config.yml");
        w.WriteLine("  --generate-json-config        Write a default JSON config template and exit (0)");
        w.WriteLine("  --generate-yaml-config        Write a default YAML config template and exit (0)");
        w.WriteLine("  -email                        Enable Email notifications (SMTP) this run");
        w.WriteLine("  -sms                          Enable SMS notifications (HTTP API) this run");
        w.WriteLine();
        w.WriteLine("Exit codes:");
        w.WriteLine("  0 = OK (or warnings only)");
        w.WriteLine("  2 = Critical check failed");
        w.WriteLine("  1 = internal error");
    }
}
