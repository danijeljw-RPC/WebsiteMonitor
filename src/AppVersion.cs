using System.Reflection;

namespace WebsiteMonitor;

public static class AppVersion
{
    public static string GetVersionString()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetName().Name ?? "WebsiteMonitor";
        var ver = asm.GetName().Version?.ToString() ?? "0.0.0";
        return $"{name} {ver}";
    }
}
