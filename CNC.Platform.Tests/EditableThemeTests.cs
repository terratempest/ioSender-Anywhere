using System.Xml.Serialization;
using Avalonia;
using Avalonia.Media;
using CNC.App;
using CNC.Controls.Config;
using CNC.Core;
using CNC.GCodeViewer.Avalonia;

namespace CNC.Platform.Tests;

public class EditableThemeTests
{
    [Fact]
    public void BaseConfig_serializes_custom_draft_without_named_user_themes()
    {
        var config = new BaseConfig
        {
            Theme = "Shop Theme",
            CustomThemeDraft = AppThemePalette.CreateTheme(AppThemeKeys.Custom, AppTheme.Dark),
            UseSystemAccentColor = false,
        };
        config.CustomThemeDraft.UseSystemAccentColor = false;
        config.CustomThemeDraft.Colors.First(c => c.Key == "ThemeBackgroundBrush").Value = "#FF010203";
        var userTheme = AppThemePalette.CreateTheme("Shop Theme", AppTheme.Light);
        userTheme.Colors.First(c => c.Key == "IoSenderViewerCutColor").Value = "#FF112233";
        config.UserThemes.Add(userTheme);

        var xml = Serialize(config);
        var deserialized = DeserializeBaseConfig(xml);

        Assert.Equal("Shop Theme", deserialized.Theme);
        Assert.False(deserialized.UseSystemAccentColor);
        Assert.False(deserialized.CustomThemeDraft.UseSystemAccentColor);
        Assert.Equal("#FF010203", deserialized.CustomThemeDraft.Colors.First(c => c.Key == "ThemeBackgroundBrush").Value);
        Assert.Empty(deserialized.UserThemes);
        Assert.Empty(deserialized.LegacyUserThemes);
        Assert.DoesNotContain("<UserThemes>", xml);
        Assert.DoesNotContain("#FF112233", xml);
    }

    [Fact]
    public void BaseConfig_missing_system_accent_flag_defaults_to_true()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-16"?>
            <BaseConfig xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
              <Theme>Dark</Theme>
            </BaseConfig>
            """;

        var deserialized = DeserializeBaseConfig(xml);

        Assert.True(deserialized.UseSystemAccentColor);
        Assert.True(deserialized.CustomThemeDraft.UseSystemAccentColor);
    }

    [Fact]
    public void AppThemeDefinition_round_trips_system_accent_flag()
    {
        var theme = AppThemePalette.CreateTheme("Shop Theme", AppTheme.Light);
        theme.UseSystemAccentColor = false;

        var deserialized = DeserializeThemeDefinition(Serialize(theme));

        Assert.False(deserialized.UseSystemAccentColor);
    }

    [Fact]
    public void AppThemeDefinition_missing_system_accent_flag_defaults_to_true()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-16"?>
            <AppThemeDefinition xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
              <Name>Shop Theme</Name>
              <BaseTheme>Light</BaseTheme>
            </AppThemeDefinition>
            """;

        var deserialized = DeserializeThemeDefinition(xml);

        Assert.True(deserialized.UseSystemAccentColor);
    }

    [Fact]
    public void BaseConfig_deserializes_legacy_user_themes_for_file_migration()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-16"?>
            <BaseConfig xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
              <Theme>Shop Theme</Theme>
              <UserThemes>
                <AppThemeDefinition>
                  <Name>Shop Theme</Name>
                  <BaseTheme>Light</BaseTheme>
                  <Colors>
                    <ThemeColorSetting>
                      <Key>IoSenderViewerCutColor</Key>
                      <Value>#FF112233</Value>
                    </ThemeColorSetting>
                  </Colors>
                </AppThemeDefinition>
              </UserThemes>
            </BaseConfig>
            """;

        var deserialized = DeserializeBaseConfig(xml);

        var legacyTheme = Assert.Single(deserialized.LegacyUserThemes);
        Assert.Equal("Shop Theme", legacyTheme.Name);
        Assert.Equal(AppTheme.Light, legacyTheme.BaseTheme);
        Assert.Equal("#FF112233", legacyTheme.Colors.First(c => c.Key == "IoSenderViewerCutColor").Value);
        Assert.Empty(deserialized.UserThemes);
    }

    [Fact]
    public void AppThemeFileService_saves_themes_under_config_path()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "iosender-theme-test-" + Guid.NewGuid().ToString("N"));
        var originalConfigPath = Resources.ConfigPath;
        var originalPath = Resources.Path;

        try
        {
            Resources.Path = Resources.ConfigPath = EnsureTrailingSeparator(tempRoot);
            var theme = AppThemePalette.CreateTheme("Shop Theme", AppTheme.Light);
            theme.UseSystemAccentColor = false;
            theme.Colors.First(c => c.Key == "IoSenderViewerCutColor").Value = "#FF112233";

            AppThemeFileService.Save(theme);

            var expectedPath = Path.Combine(tempRoot, "Themes", "Shop Theme.xml");
            Assert.True(File.Exists(expectedPath));
            Assert.True(AppThemeFileService.TryLoadByName("Shop Theme", out var loaded));
            Assert.False(loaded.UseSystemAccentColor);
            Assert.Equal("#FF112233", loaded.Colors.First(c => c.Key == "IoSenderViewerCutColor").Value);
        }
        finally
        {
            Resources.ConfigPath = originalConfigPath;
            Resources.Path = originalPath;
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void AppThemeFileService_migrates_legacy_user_themes_to_files()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "iosender-theme-migrate-test-" + Guid.NewGuid().ToString("N"));
        var originalConfigPath = Resources.ConfigPath;
        var originalPath = Resources.Path;

        try
        {
            Resources.Path = Resources.ConfigPath = EnsureTrailingSeparator(tempRoot);
            var config = new BaseConfig();
            var theme = AppThemePalette.CreateTheme("Shop Theme", AppTheme.Dark);
            theme.Colors.First(c => c.Key == "ThemeBackgroundBrush").Value = "#FF010203";
            config.LegacyUserThemes.Add(theme);

            Assert.True(AppThemeFileService.MigrateLegacyThemes(config));

            Assert.Empty(config.LegacyUserThemes);
            var loaded = Assert.Single(config.UserThemes);
            Assert.Equal("Shop Theme", loaded.Name);
            Assert.True(File.Exists(Path.Combine(tempRoot, "Themes", "Shop Theme.xml")));
        }
        finally
        {
            Resources.ConfigPath = originalConfigPath;
            Resources.Path = originalPath;
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void AppTheme_find_theme_resolves_custom_and_named_user_theme()
    {
        var config = new BaseConfig
        {
            CustomThemeDraft = AppThemePalette.CreateTheme(AppThemeKeys.Custom, AppTheme.Dark),
        };
        var userTheme = AppThemePalette.CreateTheme("Mill", AppTheme.DarkHighContrast);
        userTheme.UseSystemAccentColor = false;
        config.UserThemes.Add(userTheme);

        Assert.Same(config.CustomThemeDraft, AppTheme.FindTheme(config, AppThemeKeys.Custom));
        Assert.Same(userTheme, AppTheme.FindTheme(config, "Mill"));
        Assert.False(AppTheme.FindTheme(config, "Mill")!.UseSystemAccentColor);
        Assert.Null(AppTheme.FindTheme(config, AppTheme.Light));
    }

    [Fact]
    public void AppTheme_build_resource_dictionary_adds_custom_colors()
    {
        var userTheme = AppThemePalette.CreateTheme("Mill", AppTheme.Dark);
        userTheme.Colors.First(c => c.Key == "IoSenderViewerCutColor").Value = "#FF112233";

        var dictionary = AppTheme.BuildResourceDictionary(userTheme);

        Assert.True(dictionary.TryGetValue("IoSenderViewerCutColor", out var value));
        var color = Assert.IsType<Color>(value);
        Assert.Equal(Color.FromArgb(255, 17, 34, 51), color);
    }

    [Fact]
    public void AppTheme_build_accent_resource_dictionary_adds_all_accent_keys()
    {
        var dictionary = AppTheme.BuildAccentResourceDictionary(
            Color.FromRgb(1, 2, 3),
            Color.FromRgb(4, 5, 6),
            Color.FromRgb(7, 8, 9));

        foreach (var key in new[]
        {
            "ThemeAccentBrush",
            "ThemeAccentBrushLow",
            "ThemeAccentBrushHigh",
            "ThemeAccentColor",
            "ThemeAccentColor1",
            "ThemeAccentColor2",
            "ThemeAccentColor3",
        })
            Assert.True(dictionary.ContainsKey(key), key);

        Assert.Equal(Color.FromRgb(1, 2, 3), Assert.IsType<SolidColorBrush>(dictionary["ThemeAccentBrush"]).Color);
        Assert.Equal(Color.FromRgb(4, 5, 6), Assert.IsType<Color>(dictionary["ThemeAccentColor2"]));
        Assert.Equal(Color.FromRgb(7, 8, 9), Assert.IsType<Color>(dictionary["ThemeAccentColor3"]));
    }

    [Fact]
    public void AppTheme_build_custom_accent_dictionary_overrides_fluent_system_accent_keys()
    {
        var theme = AppThemePalette.CreateTheme("Mill", AppTheme.Dark);
        theme.UseSystemAccentColor = false;
        theme.Colors.First(c => c.Key == AppThemePalette.EditableAccentKey).Value = "#FF112233";

        var dictionary = AppTheme.BuildCustomAccentResourceDictionary(theme);

        Assert.Equal(Color.FromRgb(17, 34, 51), Assert.IsType<SolidColorBrush>(dictionary["ThemeAccentBrush"]).Color);
        Assert.Equal(Color.FromRgb(17, 34, 51), Assert.IsType<Color>(dictionary["ThemeAccentColor"]));
        Assert.Equal(Color.FromRgb(17, 34, 51), Assert.IsType<Color>(dictionary["SystemAccentColor"]));
        Assert.Equal(Color.FromRgb(11, 22, 33), Assert.IsType<Color>(dictionary["SystemAccentColorDark1"]));
        Assert.Equal(Color.FromRgb(88, 100, 112), Assert.IsType<Color>(dictionary["SystemAccentColorLight1"]));
    }

    [Fact]
    public void AppThemePalette_entries_expose_one_accent_picker()
    {
        var accentEntries = AppThemePalette.Entries
            .Where(e => AppThemePalette.IsAccentKey(e.Key))
            .ToArray();

        var entry = Assert.Single(accentEntries);
        Assert.Equal(AppThemePalette.EditableAccentKey, entry.Key);
    }

    [Fact]
    public void AppThemePalette_normalization_preserves_system_accent_flag()
    {
        var theme = AppThemePalette.CreateTheme("Mill", AppTheme.Dark);
        theme.UseSystemAccentColor = false;

        var normalized = AppThemePalette.NormalizeColors(theme);

        Assert.False(normalized.UseSystemAccentColor);
    }

    [Theory]
    [InlineData(AppTheme.Dark)]
    [InlineData(AppTheme.Light)]
    [InlineData(AppTheme.DarkHighContrast)]
    [InlineData(AppTheme.LightHighContrast)]
    public void AppThemePalette_uses_blue_defaults_for_custom_accent_mode(string baseTheme)
    {
        var theme = AppThemePalette.CreateTheme("Mill", baseTheme);
        var colors = theme.Colors.ToDictionary(c => c.Key, c => c.Value, StringComparer.OrdinalIgnoreCase);

        Assert.Equal("#FF3794FF", colors[AppThemePalette.EditableAccentKey]);
        Assert.DoesNotContain("ThemeAccentBrush", colors.Keys);
        Assert.DoesNotContain("ThemeAccentColor2", colors.Keys);
    }

    [Fact]
    public void ViewerColors_use_theme_values()
    {
        var theme = new ViewerThemeColors(
            Color.FromRgb(1, 1, 1),
            Color.FromRgb(2, 2, 2),
            Color.FromRgb(3, 3, 3),
            Color.FromRgb(4, 4, 4),
            Color.FromRgb(5, 5, 5),
            Color.FromRgb(6, 6, 6),
            Color.FromRgb(7, 7, 7),
            Color.FromRgb(8, 8, 8),
            Color.FromRgb(9, 9, 9),
            Color.FromRgb(10, 10, 10));
        var cfg = new GCodeViewerConfig
        {
            CutMotionColor = new CNC.Core.UiColor(200, 0, 0),
            RapidMotionColor = new CNC.Core.UiColor(200, 0, 0),
            RetractMotionColor = new CNC.Core.UiColor(200, 0, 0),
            GridColor = new CNC.Core.UiColor(200, 0, 0),
            HighlightColor = new CNC.Core.UiColor(200, 0, 0),
            ToolOriginColor = new CNC.Core.UiColor(200, 0, 0),
        };

        Assert.Equal(theme.Cut, ViewerColors.ResolveCutColor(cfg, theme));
        Assert.Equal(theme.Rapid, ViewerColors.ResolveRapidColor(cfg, theme));
        Assert.Equal(theme.Retract, ViewerColors.ResolveRetractColor(cfg, theme));
        Assert.Equal(theme.Grid, ViewerColors.ResolveGridColor(cfg, theme));
        Assert.Equal(theme.Highlight, ViewerColors.ResolveHighlightColor(cfg, theme));
        Assert.Equal(theme.Tool, ViewerColors.ResolveToolColor(cfg, theme));
        Assert.Equal(theme.WorkEnvelope, theme.WorkEnvelope);
    }

    static string Serialize(BaseConfig config)
    {
        var serializer = new XmlSerializer(typeof(BaseConfig));
        using var writer = new StringWriter();
        serializer.Serialize(writer, config);
        return writer.ToString();
    }

    static string Serialize(AppThemeDefinition theme)
    {
        var serializer = new XmlSerializer(typeof(AppThemeDefinition));
        using var writer = new StringWriter();
        serializer.Serialize(writer, theme);
        return writer.ToString();
    }

    static BaseConfig DeserializeBaseConfig(string xml)
    {
        var serializer = new XmlSerializer(typeof(BaseConfig));
        using var reader = new StringReader(xml);
        return (BaseConfig)serializer.Deserialize(reader)!;
    }

    static AppThemeDefinition DeserializeThemeDefinition(string xml)
    {
        var serializer = new XmlSerializer(typeof(AppThemeDefinition));
        using var reader = new StringReader(xml);
        return (AppThemeDefinition)serializer.Deserialize(reader)!;
    }

    static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
}
