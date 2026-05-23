using System.Globalization;

namespace CNC.Localization;

public static class LocalizedStrings
{
    private static IReadOnlyDictionary<string, string> _catalog =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public static string CultureName { get; private set; } = "en-US";

    public static string? LocaleRoot { get; private set; }

    public static bool IsLoaded => _catalog.Count > 0;

    public static event EventHandler? CultureChanged;

    public static void Load(string localeRoot, string cultureName)
    {
        LocaleRoot = localeRoot;
        CultureName = cultureName;
        _catalog = CsvLocaleLoader.LoadCulture(localeRoot, cultureName);

        if (_catalog.Count == 0 && !cultureName.Equals("en-US", StringComparison.OrdinalIgnoreCase))
            _catalog = CsvLocaleLoader.LoadCulture(localeRoot, "en-US");

        CultureChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string Get(string key, string? fallback = null)
    {
        if (_catalog.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
            return value;

        return fallback ?? key;
    }

    public static bool TryGet(string key, out string value) =>
        _catalog.TryGetValue(key, out value!) && !string.IsNullOrEmpty(value);

    public static void ApplyCulture(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }
}
