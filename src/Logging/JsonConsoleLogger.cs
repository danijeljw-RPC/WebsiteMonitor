// using System.Text.Json;

// namespace WebsiteMonitor.Logging;

// public sealed class JsonConsoleLogger
// {
//     private readonly object _lock = new();
//     private static readonly Stream _stdout = Console.OpenStandardOutput();

//     public void Info(string evt, Action<Utf8JsonWriter>? writeData = null) => Write("info", evt, writeData);
//     public void Warn(string evt, Action<Utf8JsonWriter>? writeData = null) => Write("warn", evt, writeData);
//     public void Error(string evt, Action<Utf8JsonWriter>? writeData = null) => Write("error", evt, writeData);

//     private void Write(string level, string evt, Action<Utf8JsonWriter>? writeData)
//     {
//         lock (_lock)
//         {
//             using var jw = new Utf8JsonWriter(Console.OpenStandardOutput(), new JsonWriterOptions
//             {
//                 Indented = false,
//                 SkipValidation = false
//             });

//             jw.WriteStartObject();
//             jw.WriteString("ts", DateTimeOffset.UtcNow.ToString("O"));
//             jw.WriteString("lvl", level);
//             jw.WriteString("evt", evt);

//             if (writeData is not null)
//             {
//                 jw.WritePropertyName("data");
//                 jw.WriteStartObject();
//                 writeData(jw);
//                 jw.WriteEndObject();
//             }

//             jw.WriteEndObject();
//             jw.Flush();

//             _stdout.WriteByte((byte)'\n');
//             _stdout.Flush();
//         }
//     }
// }
using System.Buffers;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace WebsiteMonitor.Logging;

public sealed class JsonConsoleLogger
{
    private static readonly Stream Stdout = Console.OpenStandardOutput();
    private static readonly object Gate = new();

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping // avoids \\u002B in +00:00 etc
    };

    public void Info(string evt, Action<Utf8JsonWriter>? writeData = null) => Write("info", evt, writeData);
    public void Warn(string evt, Action<Utf8JsonWriter>? writeData = null) => Write("warn", evt, writeData);
    public void Error(string evt, Action<Utf8JsonWriter>? writeData = null) => Write("error", evt, writeData);

    private static void Write(string level, string evt, Action<Utf8JsonWriter>? writeData)
    {
        // Build the entire log line in memory, then write once (no interleaving, no invalid JSON ops).
        var buffer = new ArrayBufferWriter<byte>(256);

        using (var jw = new Utf8JsonWriter(buffer, WriterOptions))
        {
            jw.WriteStartObject();
            jw.WriteString("ts", DateTimeOffset.UtcNow.ToString("O"));
            jw.WriteString("lvl", level);
            jw.WriteString("evt", evt);

            if (writeData is not null)
            {
                jw.WritePropertyName("data");
                jw.WriteStartObject();
                writeData(jw);
                jw.WriteEndObject();
            }

            jw.WriteEndObject();
            jw.Flush();
        }

        // append newline byte
        var nl = buffer.GetSpan(1);
        nl[0] = (byte)'\n';
        buffer.Advance(1);

        lock (Gate)
        {
            Stdout.Write(buffer.WrittenSpan);
            Stdout.Flush();
        }
    }
}
