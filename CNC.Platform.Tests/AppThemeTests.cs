using System.Reflection;
using CNC.App;
using CNC.Controls.Config;

namespace CNC.Platform.Tests;

public class AppThemeTests
{
    public static TheoryData<string?, string> LegacyThemeKeys => new()
    {
        { null, AppTheme.Dark },
        { "", AppTheme.Dark },
        { "default", AppTheme.Dark },
        { "Standard", AppTheme.Dark },
        { "Dark", AppTheme.Dark },
        { "Black", AppTheme.DarkHighContrast },
        { "Darker", AppTheme.DarkHighContrast },
        { "White", AppTheme.LightHighContrast },
        { "Light (bright)", AppTheme.LightHighContrast },
        { "Light", AppTheme.Light },
        { "LightHighContrast", AppTheme.LightHighContrast },
        { "DarkHighContrast", AppTheme.DarkHighContrast },
    };

    [Theory]
    [MemberData(nameof(LegacyThemeKeys))]
    public void NormalizeThemeKey_maps_legacy_values_to_canonical_keys(string? theme, string expected)
    {
        Assert.Equal(expected, AppTheme.NormalizeThemeKey(theme));
        Assert.Equal(expected, AppThemeKeys.Normalize(theme));
    }

    [Theory]
    [InlineData("Standard", AppTheme.Dark)]
    [InlineData("Black", AppTheme.DarkHighContrast)]
    [InlineData("White", AppTheme.LightHighContrast)]
    [InlineData("Light", AppTheme.Light)]
    public void MigrateLegacyTheme_persists_canonical_theme_keys(string theme, string expected)
    {
        var config = new BaseConfig { Theme = theme };

        var migrated = InvokeMigrateLegacyTheme(config);

        Assert.Equal(expected, config.Theme);
        Assert.Equal(!string.Equals(theme, expected, StringComparison.Ordinal), migrated);
    }

    [Fact]
    public void AppearancePanel_exposes_requested_theme_options()
    {
        var field = typeof(AppAppearanceConfigPanel).GetField("ThemeEntries",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(field);
        var entries = Assert.IsType<(string Key, string Label)[]>(field.GetValue(null));

        Assert.Equal(
            [
                (AppTheme.Light, "Light"),
                (AppTheme.LightHighContrast, "Light (High Contrast)"),
                (AppTheme.Dark, "Dark"),
                (AppTheme.DarkHighContrast, "Dark (High Contrast)"),
            ],
            entries);
    }

    static bool InvokeMigrateLegacyTheme(BaseConfig config)
    {
        var method = typeof(AppConfigService).GetMethod("MigrateLegacyTheme",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        return (bool)method.Invoke(null, [config])!;
    }
}
