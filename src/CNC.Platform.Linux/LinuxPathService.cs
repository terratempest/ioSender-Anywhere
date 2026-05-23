using CNC.Platform.Abstractions;

namespace CNC.Platform.Linux;

public sealed class LinuxPathService : IPathService
{
    public string Combine(params string[] segments) => Path.Combine(segments);

    public string NormalizeConfigPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        var normalized = expanded.Replace('\\', '/');

        if (normalized.StartsWith('~'))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            normalized = Path.Combine(home, normalized[1..].TrimStart('/'));
        }

        var full = Path.GetFullPath(normalized);
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
}
