using System.Diagnostics;
using CNC.Platform.Abstractions;

namespace CNC.Platform.Windows;

public sealed class WindowsExternalEditor : IExternalEditor
{
    public Task OpenFileAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("File not found.", path);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var notepad = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "notepad.exe");

        var startInfo = new ProcessStartInfo
        {
            FileName = File.Exists(notepad) ? notepad : path,
            Arguments = File.Exists(notepad) ? $"\"{path}\"" : null,
            UseShellExecute = !File.Exists(notepad),
        };

        _ = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start external editor.");
        return Task.CompletedTask;
    }
}
