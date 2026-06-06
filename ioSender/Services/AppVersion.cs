using System.Reflection;

namespace ioSender.Services;

internal static class AppVersion
{
    public static string DisplayVersion => FormatVersion(GetVersion());

    private static string GetVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(AppVersion).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "-";
    }

    private static string FormatVersion(string version)
    {
        var display = version.Split('+', 2)[0];
        return display.Length == 0
            ? "-"
            : char.ToUpperInvariant(display[0]) + display[1..];
    }
}
