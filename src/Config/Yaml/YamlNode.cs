namespace WebsiteMonitor.Config.Yaml;

public abstract class YamlNode
{
    public sealed class Map : YamlNode
    {
        public Dictionary<string, YamlNode> Values { get; } = new(StringComparer.Ordinal);
    }

    public sealed class Seq : YamlNode
    {
        public List<YamlNode> Items { get; } = new();
    }

    public sealed class Scalar : YamlNode
    {
        public ScalarKind Kind { get; }
        public string? StringValue { get; }
        public long? IntValue { get; }
        public double? DoubleValue { get; }
        public bool? BoolValue { get; }

        public Scalar(ScalarKind kind, string? s = null, long? l = null, double? d = null, bool? b = null)
        {
            Kind = kind;
            StringValue = s;
            IntValue = l;
            DoubleValue = d;
            BoolValue = b;
        }
    }

    public enum ScalarKind
    {
        Null = 0,
        String = 1,
        Integer = 2,
        Double = 3,
        Boolean = 4
    }
}
