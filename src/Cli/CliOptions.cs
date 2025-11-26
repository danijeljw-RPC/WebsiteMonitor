namespace WebsiteMonitor.Cli;

public sealed class CliOptions
{
    public bool ShowHelp { get; set; }
    public bool ShowVersion { get; set; }

    public string? ConfigPath { get; set; }

    public bool GenerateJsonConfig { get; set; }
    public bool GenerateYamlConfig { get; set; }

    public bool EnableEmail { get; set; }
    public bool EnableSms { get; set; }
}
