using CNC.Platform.Abstractions;

namespace CNC.Platform.Linux;

public sealed class LinuxPathService : IPathService
{
    const string AppDirectoryName = "ioSender";

    public string Combine(params string[] segments) => Path.Combine(segments);

    public string NormalizeConfigPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var full = string.IsNullOrWhiteSpace(configHome)
            ? Path.Combine(GetHomeDirectory(), ".config", AppDirectoryName)
            : Path.Combine(Environment.ExpandEnvironmentVariables(configHome.Trim()), AppDirectoryName);

        if (!full.EndsWith(Path.DirectorySeparatorChar))
            full += Path.DirectorySeparatorChar;
        return full;
    }

    public bool IsPhysicalFilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (path.Contains("://", StringComparison.Ordinal))
        {
            return false;
        }

        return path.StartsWith('/') || path.StartsWith("~/", StringComparison.Ordinal);
    }

    static string GetHomeDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
            return home;

        home = Environment.GetEnvironmentVariable("HOME");
        return string.IsNullOrWhiteSpace(home) ? "." : home;
    }
}
