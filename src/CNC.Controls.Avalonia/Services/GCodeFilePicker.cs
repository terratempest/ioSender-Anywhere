using Avalonia.Platform.Storage;

namespace CNC.Controls.Avalonia.Services;

public static class GCodeFilePicker
{
    public static FilePickerFileType[] FileTypes { get; } =
    [
        new FilePickerFileType("G-code")
        {
            Patterns = ["*.nc", "*.ngc", "*.gcode", "*.tap", "*.cnc", "*.txt"]
        },
        new FilePickerFileType("All files") { Patterns = ["*.*"] }
    ];

    public static async Task<string?> PickOpenPathAsync(IStorageProvider storage, string title = "Open G-code")
    {
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = FileTypes
        });

        return files.FirstOrDefault()?.TryGetLocalPath();
    }
}
