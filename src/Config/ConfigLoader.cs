using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebsiteMonitor.Config.Yaml;

namespace WebsiteMonitor.Config;

public static class ConfigLoader
{
    // Source generation for config types (AOT/trimming friendly)
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static AppConfig Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ConfigException("Config path was empty");

        if (!File.Exists(path))
            throw new ConfigException($"Config not found: {path}");

        var ext = Path.GetExtension(path).ToLowerInvariant();

        return ext switch
        {
            ".json" => LoadJson(path),
            ".yaml" or ".yml" => LoadYaml(path),
            _ => throw new ConfigException($"Unsupported config extension: {ext} (use .json, .yaml, or .yml)")
        };
    }

    private static AppConfig LoadJson(string path)
    {
        var json = File.ReadAllText(path, Encoding.UTF8);
        try
        {
            var cfg = JsonSerializer.Deserialize(json, AppConfigJsonContext.Default.AppConfig);
            return cfg ?? throw new ConfigException("Config deserialized to null");
        }
        catch (JsonException jex)
        {
            throw new ConfigException($"Invalid JSON config: {jex.Message}");
        }
    }

    private static AppConfig LoadYaml(string path)
    {
        var text = File.ReadAllText(path, Encoding.UTF8);

        YamlNode root;
        try
        {
            root = MinimalYamlParser.Parse(text);
        }
        catch (Exception ex)
        {
            throw new ConfigException($"Invalid YAML config: {ex.Message}");
        }

        // Convert YAML node tree to JSON bytes deterministically (no reflection-heavy serializers)
        var jsonBytes = MinimalYamlParser.ToJsonUtf8(root, indented: false);

        try
        {
            var cfg = JsonSerializer.Deserialize(jsonBytes, AppConfigJsonContext.Default.AppConfig);
            return cfg ?? throw new ConfigException("Config deserialized to null");
        }
        catch (JsonException jex)
        {
            throw new ConfigException($"YAML->JSON config invalid for schema: {jex.Message}");
        }
    }
}

[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(AppSection))]
[JsonSerializable(typeof(SqliteSection))]
[JsonSerializable(typeof(CheckConfig))]
[JsonSerializable(typeof(ExpectedStatusConfig))]
[JsonSerializable(typeof(RedirectConfig))]
[JsonSerializable(typeof(ContentRuleConfig))]
[JsonSerializable(typeof(LoginConfig))]
[JsonSerializable(typeof(TlsConfig))]
[JsonSerializable(typeof(ExpectedHeaderConfig))]
[JsonSerializable(typeof(ContentLengthConfig))]
[JsonSerializable(typeof(NotificationsConfig))]
[JsonSerializable(typeof(NotificationRulesConfig))]
[JsonSerializable(typeof(NotificationTemplatesConfig))]
[JsonSerializable(typeof(NotificationTemplate))]
[JsonSerializable(typeof(EmailSettings))]
[JsonSerializable(typeof(SmsSettings))]
internal sealed partial class AppConfigJsonContext : JsonSerializerContext
{
}
