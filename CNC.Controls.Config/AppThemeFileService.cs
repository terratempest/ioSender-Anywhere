using System.Xml.Serialization;
using CNC.App;
using CNC.Core;

namespace CNC.Controls.Config;

public sealed record AppThemeFile(string Name, string Path);

public static class AppThemeFileService
{
    const string ThemeExtension = ".xml";

    static readonly XmlSerializer Serializer = new(typeof(AppThemeDefinition));

    public static string ThemesDirectory => Path.Combine(Resources.ConfigPath, "Themes");

    public static IReadOnlyList<AppThemeFile> LoadThemes()
    {
        if (!Directory.Exists(ThemesDirectory))
            return Array.Empty<AppThemeFile>();

        var themes = new List<AppThemeFile>();
        foreach (var file in Directory.EnumerateFiles(ThemesDirectory, "*" + ThemeExtension))
        {
            if (TryLoad(file, out var theme))
                themes.Add(new AppThemeFile(theme.Name, file));
        }

        return themes
            .Where(t => !AppTheme.IsBuiltInTheme(t.Name)
                && !t.Name.Equals(AppThemeKeys.Custom, StringComparison.OrdinalIgnoreCase))
            .GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static void LoadInto(BaseConfig config)
    {
        config.UserThemes.Clear();
        foreach (var file in LoadThemes())
        {
            if (TryLoad(file.Path, out var theme))
                config.UserThemes.Add(AppThemePalette.NormalizeColors(theme));
        }
    }

    public static bool TryLoadByName(string name, out AppThemeDefinition theme)
    {
        foreach (var file in LoadThemes())
        {
            if (file.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                && TryLoad(file.Path, out theme))
            {
                theme = AppThemePalette.NormalizeColors(theme);
                return true;
            }
        }

        theme = new AppThemeDefinition();
        return false;
    }

    public static void Save(AppThemeDefinition theme)
    {
        var normalized = AppThemePalette.NormalizeColors(theme);
        if (string.IsNullOrWhiteSpace(normalized.Name)
            || AppTheme.IsBuiltInTheme(normalized.Name)
            || normalized.Name.Equals(AppThemeKeys.Custom, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only named user themes can be saved to theme files.");
        }

        Directory.CreateDirectory(ThemesDirectory);

        var existing = LoadThemes()
            .FirstOrDefault(t => t.Name.Equals(normalized.Name, StringComparison.OrdinalIgnoreCase));
        var path = existing?.Path ?? GetUniquePath(normalized.Name);

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        Serializer.Serialize(stream, normalized);
    }

    public static bool Delete(string name)
    {
        var theme = LoadThemes()
            .FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (theme is null)
            return false;

        File.Delete(theme.Path);
        return true;
    }

    public static bool MigrateLegacyThemes(BaseConfig config)
    {
        if (config.LegacyUserThemes.Count == 0)
            return false;

        foreach (var theme in config.LegacyUserThemes)
        {
            if (string.IsNullOrWhiteSpace(theme.Name)
                || AppTheme.IsBuiltInTheme(theme.Name)
                || theme.Name.Equals(AppThemeKeys.Custom, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Save(theme);
        }

        config.LegacyUserThemes.Clear();
        LoadInto(config);
        return true;
    }

    static bool TryLoad(string path, out AppThemeDefinition theme)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            theme = (AppThemeDefinition)Serializer.Deserialize(stream)!;
            theme.Name = theme.Name.Trim();
            if (!string.IsNullOrWhiteSpace(theme.Name))
                return true;
        }
        catch
        {
        }

        theme = new AppThemeDefinition();
        return false;
    }

    static string GetUniquePath(string name)
    {
        var safeName = SanitizeFileName(name);
        var path = Path.Combine(ThemesDirectory, safeName + ThemeExtension);
        if (!File.Exists(path))
            return path;

        for (var i = 2; ; i++)
        {
            path = Path.Combine(ThemesDirectory, $"{safeName}-{i}{ThemeExtension}");
            if (!File.Exists(path))
                return path;
        }
    }

    static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var chars = name
            .Trim()
            .Select(c => invalid.Contains(c) ? '-' : c)
            .ToArray();
        var safe = new string(chars).Trim('.', ' ');
        return string.IsNullOrWhiteSpace(safe) ? "theme" : safe;
    }
}
