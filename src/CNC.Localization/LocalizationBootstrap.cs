namespace CNC.Localization;

public static class LocalizationBootstrap
{
    /// <summary>Loads locale CSV from <paramref name="baseDirectory"/>; honors <c>-locale</c> then config culture.</summary>
    public static string Initialize(string[] args, string? configCulture, string baseDirectory) =>
        Configure(args, configCulture, baseDirectory);

    public static string Configure(string[] args, string? configCulture, string baseDirectory)
    {
        var explicitCulture = ResolveCulture(args, configCulture);
        var culture = explicitCulture ?? "en-US";
        var localeRoot = LocalePathResolver.Find(baseDirectory);

        if (localeRoot is not null)
            LocalizedStrings.Load(localeRoot, culture);

        if (explicitCulture is not null)
            LocalizedStrings.ApplyCulture(culture);

        return culture;
    }

    public static string? ResolveCulture(string[] args, string? configCulture)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "-locale", StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        if (!string.IsNullOrWhiteSpace(configCulture))
            return configCulture.Trim();

        return null;
    }
}
