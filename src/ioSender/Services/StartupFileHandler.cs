using CNC.Controls.Avalonia.Services;

namespace ioSender.Services;

/// <summary>Loads G-code from startup args or single-instance IPC messages.</summary>
public static class StartupFileHandler
{
    static readonly HashSet<string> Extensions = GCodeFileService.FileTypes
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static bool TryLoad(string? path, bool allowWhileJobRunning = false)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        path = path.Trim().Trim('"');
        if (!File.Exists(path))
            return false;

        var ext = Path.GetExtension(path).TrimStart('.');
        if (ext.Length == 0 || (!Extensions.Contains(ext) && !ext.Equals("txt", StringComparison.OrdinalIgnoreCase)))
            return false;

        var model = GCodeFileService.Instance.Model;
        if (!allowWhileJobRunning && model?.IsJobRunning == true)
            return false;

        GCodeFileService.Instance.Load(path);
        return true;
    }

    public static void TryLoadFromArgs(IEnumerable<string> args, bool allowWhileJobRunning = false)
    {
        foreach (var arg in args)
            TryLoad(arg, allowWhileJobRunning);
    }

    public static void TryLoadFromIpcMessage(string message, bool allowWhileJobRunning = false)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        message = message.Trim();
        if (TryLoad(message, allowWhileJobRunning))
            return;

        foreach (var line in message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryLoad(line, allowWhileJobRunning))
                return;
        }
    }
}
