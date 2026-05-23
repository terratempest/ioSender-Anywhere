namespace CNC.Localization;

public static class LocalePathResolver
{
    public static string? Find(string baseDirectory)
    {
        var env = Environment.GetEnvironmentVariable("IOSENDER_LOCALE_ROOT");
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            return Path.GetFullPath(env);

        var candidates = new List<string>
        {
            Path.Combine(baseDirectory, "Locale"),
            Path.Combine(baseDirectory, "..", "Locale"),
            Path.Combine(baseDirectory, "..", "..", "Locale"),
            Path.Combine(baseDirectory, "..", "..", "..", "Locale"),
        };

        var dir = new DirectoryInfo(baseDirectory);
        for (var i = 0; i < 6 && dir.Parent is not null; i++)
        {
            candidates.Add(Path.Combine(dir.Parent.FullName, "Locale"));
            dir = dir.Parent;
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var full = Path.GetFullPath(candidate);
            if (Directory.Exists(full))
                return full;
        }

        return null;
    }

    public static IEnumerable<string> ListCultures(string localeRoot)
    {
        if (!Directory.Exists(localeRoot))
            yield break;

        foreach (var dir in Directory.EnumerateDirectories(localeRoot))
        {
            var csv = Path.Combine(dir, "csv");
            if (Directory.Exists(csv))
                yield return Path.GetFileName(dir);
        }
    }
}
