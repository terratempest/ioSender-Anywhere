using System.Diagnostics;
using CNC.Platform.Abstractions;

namespace CNC.Platform.Linux;

public sealed class LinuxExternalEditor : IExternalEditor
{
    public Task OpenFileAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("File not found.", path);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo
        {
            FileName = "xdg-open",
            Arguments = $"\"{path}\"",
            UseShellExecute = false,
        };

        _ = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start external editor.");
        return Task.CompletedTask;
    }
}
