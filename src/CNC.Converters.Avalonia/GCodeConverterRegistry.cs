namespace CNC.Converters;

public static class GCodeConverterRegistry
{
    sealed class Entry
    {
        public required Type Type { get; init; }
        public required string FileType { get; init; }
        public required string FileExtensions { get; init; }

        public bool MatchesExtension(string path)
        {
            var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
            return FileExtensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase));
        }
    }

    static readonly List<Entry> Converters = [];

    public static void Register<T>(string fileType, string fileExtensions) where T : IGCodeConverter, new()
        => Register(typeof(T), fileType, fileExtensions);

    public static void Register(Type type, string fileType, string fileExtensions)
    {
        if (!typeof(IGCodeConverter).IsAssignableFrom(type))
            return;
        Converters.Add(new Entry { Type = type, FileType = fileType, FileExtensions = fileExtensions });
    }

    public static void RegisterDefaults()
    {
        if (Converters.Count > 0)
            return;
        Register<Excellon2GCode>("Excellon files", "drl,xln");
        Register<HpglToGCode>("HPGL files", "plt");
    }

    public static IEnumerable<FilePickerPattern> OpenPatterns
    {
        get
        {
            foreach (var c in Converters)
            {
                foreach (var ext in c.FileExtensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    yield return new FilePickerPattern(c.FileType, ext);
            }
        }
    }

    public static bool TryLoad(string path, IGCodeFileTarget job, Avalonia.Controls.Window? owner)
    {
        foreach (var entry in Converters)
        {
            if (!entry.MatchesExtension(path))
                continue;
            var loader = (IGCodeConverter)Activator.CreateInstance(entry.Type)!;
            return loader.LoadFile(job, path, owner);
        }
        return false;
    }

    public readonly record struct FilePickerPattern(string Description, string Extension);
}
