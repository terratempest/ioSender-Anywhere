using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
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
    static ResourceDictionary? _customOverlay;

    public static void Apply(string? themeKey)
        => Apply(themeKey, null);

    public static void Apply(string? themeKey, BaseConfig? config)
    {
        if (Application.Current is not { } app)
            return;

        var key = NormalizeThemeKey(themeKey);
        var customTheme = FindTheme(config, key);
        var baseKey = customTheme?.BaseTheme ?? key;
        app.RequestedThemeVariant = IsLightKey(baseKey) ? ThemeVariant.Light : ThemeVariant.Dark;
        SyncHighContrastOverlay(app, IsHighContrastKey(baseKey));
        SyncCustomOverlay(app, customTheme);
        AppThemeKeys.NotifyThemeApplied();
    }

    public static string NormalizeThemeKey(string? theme) => AppThemeKeys.Normalize(theme);

    static bool IsLightKey(string key) =>
        key.Equals(Light, StringComparison.OrdinalIgnoreCase)
        || key.Equals(LightHighContrast, StringComparison.OrdinalIgnoreCase);

    static bool IsHighContrastKey(string key) =>
        key.Equals(LightHighContrast, StringComparison.OrdinalIgnoreCase)
        || key.Equals(DarkHighContrast, StringComparison.OrdinalIgnoreCase);

    public static bool IsBuiltInTheme(string key) =>
        NormalizeThemeKey(key) is var normalized
        && (normalized.Equals(Light, StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(LightHighContrast, StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(Dark, StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(DarkHighContrast, StringComparison.OrdinalIgnoreCase));

    public static bool IsLightTheme(string key) => IsLightKey(key);

    public static bool IsHighContrastTheme(string key) => IsHighContrastKey(key);

    public static AppThemeDefinition? FindTheme(BaseConfig? config, string? themeKey)
    {
        if (config == null)
            return null;

        var key = NormalizeThemeKey(themeKey);
        if (key.Equals(AppThemeKeys.Custom, StringComparison.OrdinalIgnoreCase))
            return config.CustomThemeDraft;

        var theme = config.UserThemes.FirstOrDefault(t => t.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (theme != null)
            return theme;

        if (AppThemeFileService.TryLoadByName(key, out var loaded))
        {
            config.UserThemes.Add(loaded);
            return loaded;
        }

        return null;
    }

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

    static void SyncCustomOverlay(Application app, AppThemeDefinition? theme)
    {
        var merged = app.Resources.MergedDictionaries;
        if (_customOverlay != null && merged.Contains(_customOverlay))
            merged.Remove(_customOverlay);

        _customOverlay = null;
        if (theme == null)
            return;

        _customOverlay = BuildResourceDictionary(theme);
        merged.Add(_customOverlay);
    }

    internal static ResourceDictionary BuildResourceDictionary(AppThemeDefinition theme)
    {
        var dictionary = new ResourceDictionary();
        foreach (var color in AppThemePalette.NormalizeColors(theme).Colors)
        {
            var key = color.Key;
            var avaloniaColor = AppThemePalette.ParseColor(color.Value);
            dictionary[key] = AppThemePalette.IsBrushKey(key)
                ? new SolidColorBrush(avaloniaColor)
                : avaloniaColor;
        }

        return dictionary;
    }
}

public sealed record AppThemePaletteEntry(string Key, string Label, bool IsViewerColor, bool IsBrush);

public static class AppThemePalette
{
    public static readonly AppThemePaletteEntry[] Entries =
    [
        new("ThemeBackgroundBrush", "Background", false, true),
        new("ThemeForegroundBrush", "Foreground", false, true),
        new("ThemeControlLowBrush", "Control low", false, true),
        new("ThemeControlMidBrush", "Control mid", false, true),
        new("ThemeControlHighBrush", "Control high", false, true),
        new("ThemeControlHighlightLowBrush", "Highlight low", false, true),
        new("ThemeControlHighlightMidBrush", "Highlight mid", false, true),
        new("ThemeControlHighlightHighBrush", "Highlight high", false, true),
        new("ThemeBorderLowBrush", "Border low", false, true),
        new("ThemeBorderMidBrush", "Border mid", false, true),
        new("ThemeBorderHighBrush", "Border high", false, true),
        new("ThemeAccentBrush", "Accent", false, true),
        new("ThemeAccentBrushLow", "Accent low", false, true),
        new("ThemeAccentBrushHigh", "Accent high", false, true),
        new("ThemeAccentColor", "Accent color", false, false),
        new("ThemeAccentColor1", "Accent color 1", false, false),
        new("ThemeAccentColor2", "Accent color 2", false, false),
        new("ThemeAccentColor3", "Accent color 3", false, false),
        new("IoSenderReadOnlyBrush", "Read-only", false, true),
        new("IoSenderOverlayBrush", "Overlay", false, true),
        new("IoSenderDangerBrush", "Danger", false, true),
        new("IoSenderDangerForegroundBrush", "Danger foreground", false, true),
        new("IoSenderHomedBrush", "Homed", false, true),
        new("IoSenderNotHomedBrush", "Not homed", false, true),
        new("IoSenderDroReadoutBrush", "DRO readout", false, true),
        new("IoSenderDroReadoutForegroundBrush", "DRO foreground", false, true),
        new("IoSenderJogSelectedBrush", "Jog selected", false, true),
        new("IoSenderViewerBackgroundColor", "Viewer background", true, false),
        new("IoSenderViewerCutColor", "Cut motion", true, false),
        new("IoSenderViewerRapidColor", "Rapid motion", true, false),
        new("IoSenderViewerRetractColor", "Retract motion", true, false),
        new("IoSenderViewerGridColor", "Grid", true, false),
        new("IoSenderViewerGridMinorColor", "Minor grid", true, false),
        new("IoSenderViewerGridMajorColor", "Major grid", true, false),
        new("IoSenderViewerHighlightColor", "Highlight", true, false),
        new("IoSenderViewerToolColor", "Tool marker", true, false),
        new("IoSenderViewerWorkEnvelopeColor", "Work envelope", true, false),
    ];

    static readonly Dictionary<string, string> Dark = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ThemeBackgroundBrush"] = "#FF1E1E1E",
        ["ThemeForegroundBrush"] = "#FFD4D4D4",
        ["ThemeControlLowBrush"] = "#FF252526",
        ["ThemeControlMidBrush"] = "#FF2D2D30",
        ["ThemeControlHighBrush"] = "#FF3C3C3C",
        ["ThemeControlHighlightLowBrush"] = "#FF3E3E42",
        ["ThemeControlHighlightMidBrush"] = "#FF454545",
        ["ThemeControlHighlightHighBrush"] = "#FF505050",
        ["ThemeBorderLowBrush"] = "#FF3F3F46",
        ["ThemeBorderMidBrush"] = "#FF505050",
        ["ThemeBorderHighBrush"] = "#FF6A6A6A",
        ["ThemeAccentBrush"] = "#FF3794FF",
        ["ThemeAccentBrushLow"] = "#FF264F78",
        ["ThemeAccentBrushHigh"] = "#FF4DA3FF",
        ["ThemeAccentColor"] = "#FF3794FF",
        ["ThemeAccentColor1"] = "#FF3794FF",
        ["ThemeAccentColor2"] = "#FF264F78",
        ["ThemeAccentColor3"] = "#FF4DA3FF",
        ["IoSenderReadOnlyBrush"] = "#FF2A2A2E",
        ["IoSenderOverlayBrush"] = "#99000000",
        ["IoSenderDangerBrush"] = "#FFC62828",
        ["IoSenderDangerForegroundBrush"] = "#FFF5F5F5",
        ["IoSenderHomedBrush"] = "#FF2E7D32",
        ["IoSenderNotHomedBrush"] = "#FFB71C1C",
        ["IoSenderDroReadoutBrush"] = "#FFF5F5F5",
        ["IoSenderDroReadoutForegroundBrush"] = "#FF1A1A1A",
        ["IoSenderJogSelectedBrush"] = "#FF90EE90",
        ["IoSenderViewerBackgroundColor"] = "#FF101010",
        ["IoSenderViewerCutColor"] = "#FFC95656",
        ["IoSenderViewerRapidColor"] = "#FFF4A6B2",
        ["IoSenderViewerRetractColor"] = "#FF4CAF50",
        ["IoSenderViewerGridColor"] = "#FF6A6A6A",
        ["IoSenderViewerGridMinorColor"] = "#FF353535",
        ["IoSenderViewerGridMajorColor"] = "#FF6A6A6A",
        ["IoSenderViewerHighlightColor"] = "#FF481E1E",
        ["IoSenderViewerToolColor"] = "#FFEE9D24",
        ["IoSenderViewerWorkEnvelopeColor"] = "#FF4DA3FF",
    };

    static readonly Dictionary<string, string> Light = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ThemeBackgroundBrush"] = "#FFD9DDE1",
        ["ThemeForegroundBrush"] = "#FF343A42",
        ["ThemeControlLowBrush"] = "#FFC6C9CC",
        ["ThemeControlMidBrush"] = "#FFB5B9BD",
        ["ThemeControlHighBrush"] = "#FFD3DAE2",
        ["ThemeControlHighlightLowBrush"] = "#FFBBC3CB",
        ["ThemeControlHighlightMidBrush"] = "#FFB0BAC4",
        ["ThemeControlHighlightHighBrush"] = "#FFA4AFBA",
        ["ThemeBorderLowBrush"] = "#FFB9C1CA",
        ["ThemeBorderMidBrush"] = "#FFA5AFBA",
        ["ThemeBorderHighBrush"] = "#FF8F9AA6",
        ["ThemeAccentBrush"] = "#FF3A718F",
        ["ThemeAccentBrushLow"] = "#FFC1D5DF",
        ["ThemeAccentBrushHigh"] = "#FF2F5F79",
        ["ThemeAccentColor"] = "#FF3A718F",
        ["ThemeAccentColor1"] = "#FF3A718F",
        ["ThemeAccentColor2"] = "#FFC1D5DF",
        ["ThemeAccentColor3"] = "#FF2F5F79",
        ["IoSenderReadOnlyBrush"] = "#FFCDD3D9",
        ["IoSenderOverlayBrush"] = "#55000000",
        ["IoSenderDangerBrush"] = "#FFA14B47",
        ["IoSenderDangerForegroundBrush"] = "#FFFFFFFF",
        ["IoSenderHomedBrush"] = "#FFABC9AE",
        ["IoSenderNotHomedBrush"] = "#FFD8B0B3",
        ["IoSenderDroReadoutBrush"] = "#FFEEF1F3",
        ["IoSenderDroReadoutForegroundBrush"] = "#FF2D3136",
        ["IoSenderJogSelectedBrush"] = "#FF6F9A76",
        ["IoSenderViewerBackgroundColor"] = "#FFDDE2E6",
        ["IoSenderViewerCutColor"] = "#FF902A2A",
        ["IoSenderViewerRapidColor"] = "#FF3A9EB2",
        ["IoSenderViewerRetractColor"] = "#FF2F6138",
        ["IoSenderViewerGridColor"] = "#FF111111",
        ["IoSenderViewerGridMinorColor"] = "#FFB5BFC9",
        ["IoSenderViewerGridMajorColor"] = "#FF111111",
        ["IoSenderViewerHighlightColor"] = "#FFE4D3D3",
        ["IoSenderViewerToolColor"] = "#FFBC811C",
        ["IoSenderViewerWorkEnvelopeColor"] = "#FF294E73",
    };

    static readonly Dictionary<string, string> DarkHighContrast = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ThemeBackgroundBrush"] = "#FF0B0B0B",
        ["ThemeForegroundBrush"] = "#FFF2F2F2",
        ["ThemeControlLowBrush"] = "#FF111111",
        ["ThemeControlMidBrush"] = "#FF1B1B1B",
        ["ThemeControlHighBrush"] = "#FF282828",
        ["ThemeControlHighlightLowBrush"] = "#FF303030",
        ["ThemeControlHighlightMidBrush"] = "#FF3D3D3D",
        ["ThemeControlHighlightHighBrush"] = "#FF4A4A4A",
        ["ThemeBorderLowBrush"] = "#FF777777",
        ["ThemeBorderMidBrush"] = "#FFA0A0A0",
        ["ThemeBorderHighBrush"] = "#FFD6D6D6",
        ["ThemeAccentBrush"] = "#FF66B8FF",
        ["ThemeAccentBrushLow"] = "#FF0B5794",
        ["ThemeAccentBrushHigh"] = "#FFA7D8FF",
        ["ThemeAccentColor"] = "#FF66B8FF",
        ["ThemeAccentColor1"] = "#FF66B8FF",
        ["ThemeAccentColor2"] = "#FF0B5794",
        ["ThemeAccentColor3"] = "#FFA7D8FF",
        ["IoSenderReadOnlyBrush"] = "#FF202020",
        ["IoSenderOverlayBrush"] = "#CC000000",
        ["IoSenderDangerBrush"] = "#FFFF4D4D",
        ["IoSenderDangerForegroundBrush"] = "#FF000000",
        ["IoSenderHomedBrush"] = "#FF7CFF8A",
        ["IoSenderNotHomedBrush"] = "#FFFF6E6E",
        ["IoSenderDroReadoutBrush"] = "#FFFFFFFF",
        ["IoSenderDroReadoutForegroundBrush"] = "#FF000000",
        ["IoSenderJogSelectedBrush"] = "#FF7CFF8A",
        ["IoSenderViewerBackgroundColor"] = "#FF000000",
        ["IoSenderViewerCutColor"] = "#FFFF5252",
        ["IoSenderViewerRapidColor"] = "#FFFFB3C1",
        ["IoSenderViewerRetractColor"] = "#FF77FF88",
        ["IoSenderViewerGridColor"] = "#FFB8B8B8",
        ["IoSenderViewerGridMinorColor"] = "#FF5C5C5C",
        ["IoSenderViewerGridMajorColor"] = "#FFB8B8B8",
        ["IoSenderViewerHighlightColor"] = "#FFFFD54F",
        ["IoSenderViewerToolColor"] = "#FF00E676",
        ["IoSenderViewerWorkEnvelopeColor"] = "#FF64B5FF",
    };

    static readonly Dictionary<string, string> LightHighContrast = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ThemeBackgroundBrush"] = "#FFFBFDFF",
        ["ThemeForegroundBrush"] = "#FF000000",
        ["ThemeControlLowBrush"] = "#FFFFFFFF",
        ["ThemeControlMidBrush"] = "#FFF0F2F7",
        ["ThemeControlHighBrush"] = "#FFEEEDFF",
        ["ThemeControlHighlightLowBrush"] = "#FFA9B1BA",
        ["ThemeControlHighlightMidBrush"] = "#FF919BA6",
        ["ThemeControlHighlightHighBrush"] = "#FF77838F",
        ["ThemeBorderLowBrush"] = "#FF4D5862",
        ["ThemeBorderMidBrush"] = "#FF26313B",
        ["ThemeBorderHighBrush"] = "#FF000000",
        ["ThemeAccentBrush"] = "#FF003C70",
        ["ThemeAccentBrushLow"] = "#FF75B8EA",
        ["ThemeAccentBrushHigh"] = "#FF001E3D",
        ["ThemeAccentColor"] = "#FF003C70",
        ["ThemeAccentColor1"] = "#FF003C70",
        ["ThemeAccentColor2"] = "#FF75B8EA",
        ["ThemeAccentColor3"] = "#FF001E3D",
        ["IoSenderReadOnlyBrush"] = "#FFBEC4CA",
        ["IoSenderOverlayBrush"] = "#88000000",
        ["IoSenderDangerBrush"] = "#FF8C1D18",
        ["IoSenderDangerForegroundBrush"] = "#FFFFFFFF",
        ["IoSenderHomedBrush"] = "#FF3E8F42",
        ["IoSenderNotHomedBrush"] = "#FFC64349",
        ["IoSenderDroReadoutBrush"] = "#FFFFFFFF",
        ["IoSenderDroReadoutForegroundBrush"] = "#FF000000",
        ["IoSenderJogSelectedBrush"] = "#FF0B6F22",
        ["IoSenderViewerBackgroundColor"] = "#FFFFFFFF",
        ["IoSenderViewerCutColor"] = "#FF392E2E",
        ["IoSenderViewerRapidColor"] = "#FF39A6B5",
        ["IoSenderViewerRetractColor"] = "#FF00A927",
        ["IoSenderViewerGridColor"] = "#FF000000",
        ["IoSenderViewerGridMinorColor"] = "#FFD6D6D6",
        ["IoSenderViewerGridMajorColor"] = "#FF000000",
        ["IoSenderViewerHighlightColor"] = "#FFD3D3D3",
        ["IoSenderViewerToolColor"] = "#FFCD9023",
        ["IoSenderViewerWorkEnvelopeColor"] = "#FF005EB9",
    };

    public static bool IsBrushKey(string key) =>
        Entries.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.IsBrush ?? false;

    public static AppThemeDefinition CreateTheme(string name, string baseTheme)
    {
        var colors = DefaultsFor(baseTheme)
            .Select(kv => new ThemeColorSetting { Key = kv.Key, Value = kv.Value })
            .ToList();

        return new AppThemeDefinition
        {
            Name = name,
            BaseTheme = AppThemeKeys.Normalize(baseTheme),
            Colors = new System.Collections.ObjectModel.ObservableCollection<ThemeColorSetting>(colors),
        };
    }

    public static AppThemeDefinition NormalizeColors(AppThemeDefinition theme)
    {
        var defaults = DefaultsFor(theme.BaseTheme);
        var values = theme.Colors
            .Where(c => !string.IsNullOrWhiteSpace(c.Key))
            .GroupBy(c => c.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => NormalizeHex(g.Last().Value, defaults[g.Key]), StringComparer.OrdinalIgnoreCase);

        var normalized = new AppThemeDefinition
        {
            Name = theme.Name,
            BaseTheme = theme.BaseTheme,
        };

        foreach (var entry in Entries)
        {
            normalized.Colors.Add(new ThemeColorSetting
            {
                Key = entry.Key,
                Value = values.TryGetValue(entry.Key, out var value) ? value : defaults[entry.Key],
            });
        }

        return normalized;
    }

    public static Color ParseColor(string value) => Color.Parse(NormalizeHex(value, "#FFFFFFFF"));

    static string NormalizeHex(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var text = value.Trim();
        if (!text.StartsWith('#'))
            text = "#" + text;

        if (text.Length == 7)
            text = "#FF" + text[1..];

        try
        {
            _ = Color.Parse(text);
            return text.ToUpperInvariant();
        }
        catch
        {
            return fallback;
        }
    }

    static Dictionary<string, string> DefaultsFor(string baseTheme)
    {
        var key = AppTheme.NormalizeThemeKey(baseTheme);
        if (key.Equals(AppTheme.Light, StringComparison.OrdinalIgnoreCase))
            return Light;
        if (key.Equals(AppTheme.LightHighContrast, StringComparison.OrdinalIgnoreCase))
            return LightHighContrast;
        if (key.Equals(AppTheme.DarkHighContrast, StringComparison.OrdinalIgnoreCase))
            return DarkHighContrast;

        return Dark;
    }
}
