using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CNC.Controls.Avalonia.Services;
using CNC.Core;

namespace CNC.Controls.Lathe;

/// <summary>Shared actions for lathe wizard generated G-code.</summary>
public static class LatheGCodeActions
{
    public static bool TryGetGeneratedLines(BaseViewModel model, out IReadOnlyList<string> lines)
    {
        lines = model.gCode
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        return lines.Count > 0;
    }

    public static bool LoadIntoProgram(BaseViewModel model, string sourceLabel)
    {
        if (!TryGetGeneratedLines(model, out var lines))
            return false;

        GCodeFileService.Instance.LoadFromLines(lines, sourceLabel);
        return true;
    }

    public static async Task<bool> SaveToFileAsync(Control owner, BaseViewModel model)
    {
        if (!TryGetGeneratedLines(model, out var lines))
            return false;

        var topLevel = TopLevel.GetTopLevel(owner);
        if (topLevel?.StorageProvider is not { } storage)
            return false;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save lathe G-code",
            DefaultExtension = "nc",
            FileTypeChoices =
            [
                new FilePickerFileType("G-code")
                {
                    Patterns = ["*.nc", "*.ngc", "*.gcode", "*.tap", "*.cnc"]
                }
            ]
        });

        var path = file?.TryGetLocalPath();
        if (string.IsNullOrEmpty(path))
            return false;

        await File.WriteAllLinesAsync(path, lines);
        return true;
    }
}
