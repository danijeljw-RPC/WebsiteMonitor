using System.Text;
using System.Text.Json;

namespace WebsiteMonitor.Config.Yaml;

public static class MinimalYamlParser
{
    // Supports:
    // - maps: key: value / key:
    // - sequences: - value / - key: value (inline map item start)
    // - scalars: strings, ints, doubles, bool, null
    // - comments starting with # outside quotes
    // - indentation-based nesting
    public static YamlNode Parse(string yaml)
    {
        var lines = SplitLines(yaml);
        var root = new YamlNode.Map();

        var stack = new Stack<Frame>();
        stack.Push(new Frame(-1, root));

        for (var i = 0; i < lines.Count; i++)
        {
            var raw = lines[i];
            var line = StripComment(raw);
            if (string.IsNullOrWhiteSpace(line)) continue;

            var indent = CountLeadingSpaces(line);
            var content = line.TrimStart();

            while (stack.Count > 1 && indent <= stack.Peek().Indent)
                stack.Pop();

            var parent = stack.Peek().Node;

            if (content.StartsWith("-"))
            {
                // sequence item
                if (parent is not YamlNode.Seq seq)
                    throw new FormatException($"YAML: sequence item found where parent is not a list (line {i + 1})");

                var afterDash = content.Length > 1 && content[1] == ' ' ? content.Substring(2) : content.Substring(1).TrimStart();
                if (string.IsNullOrWhiteSpace(afterDash))
                {
                    // nested item, lookahead decides map/seq
                    var child = CreateChildContainer(lines, i, indent);
                    seq.Items.Add(child);
                    stack.Push(new Frame(indent + 1, child));
                    continue;
                }

                // inline map item? "- key: value"
                var colonIx = IndexOfUnquoted(afterDash, ':');
                if (colonIx > 0)
                {
                    var itemMap = new YamlNode.Map();
                    seq.Items.Add(itemMap);

                    // push map context for further indented keys
                    stack.Push(new Frame(indent + 1, itemMap));

                    // parse first key on same line
                    ParseMapEntryInto(stack, itemMap, afterDash, lines, ref i, indent + 1);
                    continue;
                }

                seq.Items.Add(ParseScalar(afterDash));
                continue;
            }

            // mapping entry
            if (parent is not YamlNode.Map map)
                throw new FormatException($"YAML: mapping entry found where parent is not a map (line {i + 1})");

            ParseMapEntryInto(stack, map, content, lines, ref i, indent);
        }

        return root;
    }

    public static byte[] ToJsonUtf8(YamlNode node, bool indented)
    {
        using var ms = new MemoryStream();
        using var jw = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = indented });

        WriteNode(jw, node);
        jw.Flush();
        return ms.ToArray();
    }

    private static void WriteNode(Utf8JsonWriter jw, YamlNode node)
    {
        switch (node)
        {
            case YamlNode.Map m:
                jw.WriteStartObject();
                foreach (var kvp in m.Values)
                {
                    jw.WritePropertyName(kvp.Key);
                    WriteNode(jw, kvp.Value);
                }
                jw.WriteEndObject();
                break;

            case YamlNode.Seq s:
                jw.WriteStartArray();
                foreach (var item in s.Items)
                    WriteNode(jw, item);
                jw.WriteEndArray();
                break;

            case YamlNode.Scalar sc:
                switch (sc.Kind)
                {
                    case YamlNode.ScalarKind.Null:
                        jw.WriteNullValue();
                        break;
                    case YamlNode.ScalarKind.String:
                        jw.WriteStringValue(sc.StringValue ?? "");
                        break;
                    case YamlNode.ScalarKind.Integer:
                        jw.WriteNumberValue(sc.IntValue ?? 0);
                        break;
                    case YamlNode.ScalarKind.Double:
                        jw.WriteNumberValue(sc.DoubleValue ?? 0.0);
                        break;
                    case YamlNode.ScalarKind.Boolean:
                        jw.WriteBooleanValue(sc.BoolValue ?? false);
                        break;
                    default:
                        jw.WriteStringValue(sc.StringValue ?? "");
                        break;
                }
                break;

            default:
                jw.WriteNullValue();
                break;
        }
    }

    private static void ParseMapEntryInto(Stack<Frame> stack, YamlNode.Map map, string content, List<string> lines, ref int i, int indent)
    {
        var colon = IndexOfUnquoted(content, ':');
        if (colon <= 0)
            throw new FormatException($"YAML: expected 'key: value' (line {i + 1})");

        var key = content.Substring(0, colon).Trim();
        var rest = content.Substring(colon + 1).Trim();

        if (string.IsNullOrWhiteSpace(key))
            throw new FormatException($"YAML: empty key (line {i + 1})");

        if (string.IsNullOrWhiteSpace(rest))
        {
            // decide container type based on lookahead
            var child = CreateChildContainer(lines, i, indent);
            map.Values[key] = child;
            // next indented lines attach to this child
            // Push context so the next indented lines attach under this key.
            stack.Push(new Frame(indent, child));
            return;
        }

        map.Values[key] = ParseScalar(rest);

        // If the scalar is "{...}" or "[...]" we do NOT parse inline JSON/YAML;
        // keep it as string. Keep YAML subset minimal.
    }

    private static YamlNode CreateChildContainer(List<string> lines, int currentLineIndex, int currentIndent)
    {
        for (var j = currentLineIndex + 1; j < lines.Count; j++)
        {
            var l = StripComment(lines[j]);
            if (string.IsNullOrWhiteSpace(l)) continue;

            var indent = CountLeadingSpaces(l);
            if (indent <= currentIndent) break;

            var content = l.TrimStart();
            if (content.StartsWith("-")) return new YamlNode.Seq();
            return new YamlNode.Map();
        }

        // default to map if no children found
        return new YamlNode.Map();
    }

    private static YamlNode ParseScalar(string s)
    {
        s = s.Trim();

        if (s.Length == 0) return new YamlNode.Scalar(YamlNode.ScalarKind.String, s: "");

        if (s == "~" || s.Equals("null", StringComparison.OrdinalIgnoreCase))
            return new YamlNode.Scalar(YamlNode.ScalarKind.Null);

        if (s.Equals("true", StringComparison.OrdinalIgnoreCase))
            return new YamlNode.Scalar(YamlNode.ScalarKind.Boolean, b: true);

        if (s.Equals("false", StringComparison.OrdinalIgnoreCase))
            return new YamlNode.Scalar(YamlNode.ScalarKind.Boolean, b: false);

        // quoted string
        if ((s.StartsWith('"') && s.EndsWith('"')) || (s.StartsWith('\'') && s.EndsWith('\'')))
        {
            var inner = s.Substring(1, s.Length - 2);
            if (s[0] == '"') inner = UnescapeDoubleQuoted(inner);
            return new YamlNode.Scalar(YamlNode.ScalarKind.String, s: inner);
        }

        // number
        if (TryParseInt64(s, out var lval))
            return new YamlNode.Scalar(YamlNode.ScalarKind.Integer, l: lval);

        if (double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dval))
            return new YamlNode.Scalar(YamlNode.ScalarKind.Double, d: dval);

        return new YamlNode.Scalar(YamlNode.ScalarKind.String, s: s);
    }

    private static bool TryParseInt64(string s, out long value)
        => long.TryParse(s, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out value);

    private static string UnescapeDoubleQuoted(string s)
    {
        // Minimal YAML double-quote escaping subset.
        var sb = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            var ch = s[i];
            if (ch == '\\' && i + 1 < s.Length)
            {
                var n = s[++i];
                sb.Append(n switch
                {
                    '\\' => '\\',
                    '"' => '"',
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    _ => n
                });
                continue;
            }
            sb.Append(ch);
        }
        return sb.ToString();
    }

    private static int IndexOfUnquoted(string s, char c)
    {
        var inSingle = false;
        var inDouble = false;
        for (var i = 0; i < s.Length; i++)
        {
            var ch = s[i];
            if (ch == '\'' && !inDouble) inSingle = !inSingle;
            else if (ch == '"' && !inSingle) inDouble = !inDouble;
            else if (ch == c && !inSingle && !inDouble) return i;
        }
        return -1;
    }

    private static int CountLeadingSpaces(string s)
    {
        var n = 0;
        while (n < s.Length && s[n] == ' ') n++;
        return n;
    }

    private static string StripComment(string line)
    {
        var inSingle = false;
        var inDouble = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (ch == '\'' && !inDouble) inSingle = !inSingle;
            else if (ch == '"' && !inSingle) inDouble = !inDouble;

            if (ch == '#' && !inSingle && !inDouble)
                return line.Substring(0, i).TrimEnd();
        }

        return line.TrimEnd();
    }

    private static List<string> SplitLines(string s)
    {
        var list = new List<string>();
        using var sr = new StringReader(s);
        while (true)
        {
            var line = sr.ReadLine();
            if (line is null) break;
            list.Add(line);
        }
        return list;
    }

    private readonly record struct Frame(int Indent, YamlNode Node);
}
