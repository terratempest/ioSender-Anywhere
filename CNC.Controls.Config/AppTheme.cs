using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using CNC.App;

namespace CNC.Controls.Config;

/// <summary>Maps persisted theme keys to Avalonia theme variants and ioSender palette overlays.</summary>
public static class AppTheme
{
    public const string Light = AppThemeKeys.Light;
    public const string LightHighContrast = AppThemeKeys.LightHighContrast;
    public const string Dark = AppThemeKeys.Dark;
    public const string DarkHighContrast = AppThemeKeys.DarkHighContrast;

    const string HighContrastPaletteUri = "avares://CNC.Controls.Config.Avalonia/Themes/IoSenderHighContrastPalette.axaml";

    static ResourceDictionary? _highContrastOverlay;

    public static void Apply(string? themeKey)
    {
        if (Application.Current is not { } app)
            return;

        var key = NormalizeThemeKey(themeKey);
        app.RequestedThemeVariant = IsLightKey(key) ? ThemeVariant.Light : ThemeVariant.Dark;
        SyncHighContrastOverlay(app, IsHighContrastKey(key));
        AppThemeKeys.NotifyThemeApplied();
    }

    public static string NormalizeThemeKey(string? theme) => AppThemeKeys.Normalize(theme);

    static bool IsLightKey(string key) =>
        key.Equals(Light, StringComparison.OrdinalIgnoreCase)
        || key.Equals(LightHighContrast, StringComparison.OrdinalIgnoreCase);

    static bool IsHighContrastKey(string key) =>
        key.Equals(LightHighContrast, StringComparison.OrdinalIgnoreCase)
        || key.Equals(DarkHighContrast, StringComparison.OrdinalIgnoreCase);

    static void SyncHighContrastOverlay(Application app, bool useHighContrast)
    {
        _highContrastOverlay ??= (ResourceDictionary)AvaloniaXamlLoader.Load(new Uri(HighContrastPaletteUri));

        var merged = app.Resources.MergedDictionaries;
        if (useHighContrast)
        {
            if (!merged.Contains(_highContrastOverlay))
                merged.Add(_highContrastOverlay);
        }
        else if (merged.Contains(_highContrastOverlay))
        {
            merged.Remove(_highContrastOverlay);
        }
    }
}
