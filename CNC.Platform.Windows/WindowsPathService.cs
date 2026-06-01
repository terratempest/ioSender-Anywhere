using CNC.Platform.Abstractions;

namespace CNC.Platform.Windows;

public sealed class WindowsPathService : IPathService
{
    const string AppDirectoryName = "ioSender";

    public string Combine(params string[] segments) => Path.Combine(segments);

    public string NormalizeConfigPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var full = string.IsNullOrWhiteSpace(appData)
            ? Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim()).Replace('/', Path.DirectorySeparatorChar))
            : Path.Combine(appData, AppDirectoryName);

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
