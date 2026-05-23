using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace CNC.Controls.Config;

/// <summary>Maps persisted theme keys to Avalonia theme variants and ioSender palette overlays.</summary>
public static class AppTheme
{
    const string StandardPaletteUri = "avares://CNC.Controls.Config.Avalonia/Themes/IoSenderPalette.axaml";
    const string BlackPaletteUri = "avares://CNC.Controls.Config.Avalonia/Themes/IoSenderBlackPalette.axaml";

    static ResourceDictionary? _blackOverlay;

    public static void Apply(string? themeKey)
    {
        if (Application.Current is not { } app)
            return;

        var key = NormalizeThemeKey(themeKey);
        app.RequestedThemeVariant = IsLightKey(key) ? ThemeVariant.Light : ThemeVariant.Dark;
        SyncBlackOverlay(app, key == "Black");
    }

    public static string NormalizeThemeKey(string? theme)
    {
        if (string.IsNullOrWhiteSpace(theme)
            || theme.Equals("default", StringComparison.OrdinalIgnoreCase))
            return "Standard";

        return theme switch
        {
            "Dark" => "Standard",
            _ => theme,
        };
    }

    static bool IsLightKey(string key) =>
        key.Equals("Light", StringComparison.OrdinalIgnoreCase)
        || key.Equals("White", StringComparison.OrdinalIgnoreCase);

    static void SyncBlackOverlay(Application app, bool useBlack)
    {
        _blackOverlay ??= (ResourceDictionary)AvaloniaXamlLoader.Load(new Uri(BlackPaletteUri));

        var merged = app.Resources.MergedDictionaries;
        if (useBlack)
        {
            if (!merged.Contains(_blackOverlay))
                merged.Add(_blackOverlay);
        }
        else if (merged.Contains(_blackOverlay))
        {
            merged.Remove(_blackOverlay);
        }
    }
}
