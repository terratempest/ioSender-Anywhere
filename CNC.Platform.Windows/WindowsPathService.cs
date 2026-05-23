using CNC.Platform.Abstractions;

namespace CNC.Platform.Windows;

public sealed class WindowsPathService : IPathService
{
    public string Combine(params string[] segments) => Path.Combine(segments);

    public string NormalizeConfigPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        var full = Path.GetFullPath(expanded.Replace('/', Path.DirectorySeparatorChar));
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

        return Path.IsPathRooted(path);
    }
}
