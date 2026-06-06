using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using CNC.App;

namespace CNC.Controls.Config;

public partial class AppAppearanceConfigPanel : UserControl
{
    static readonly (string Key, string Label)[] ThemeEntries =
    [
        (AppTheme.Light, "Light"),
        (AppTheme.LightHighContrast, "Light (High Contrast)"),
        (AppTheme.Dark, "Dark"),
        (AppTheme.DarkHighContrast, "Dark (High Contrast)"),
    ];

    BaseConfig? _config;
    bool _binding;

    public event EventHandler? PreviewViewerRequested;

    public AppAppearanceConfigPanel()
    {
        InitializeComponent();
        UseSystemAccentCheck.PropertyChanged += (_, e) =>
        {
            if (e.Property == ToggleButton.IsCheckedProperty)
                OnUseSystemAccentChanged();
        };
        DataContextChanged += (_, _) => BindThemes();
    }

    void BindThemes()
    {
        _config = DataContext as BaseConfig;
        if (_config == null)
            return;

        _config.Themes.Clear();
        AppThemeFileService.LoadInto(_config);

        foreach (var (key, label) in ThemeEntries)
            _config.Themes[key] = label;

        _config.Themes[AppThemeKeys.Custom] = "Custom";
        foreach (var theme in _config.UserThemes.Where(t => !string.IsNullOrWhiteSpace(t.Name)))
            _config.Themes[theme.Name] = theme.Name;

        ThemeCombo.Items.Clear();
        foreach (var (key, label) in ThemeEntries)
            ThemeCombo.Items.Add(new ThemeItem(key, label));
        ThemeCombo.Items.Add(new ThemeItem(AppThemeKeys.Custom, "Custom"));
        foreach (var theme in _config.UserThemes.Where(t => !string.IsNullOrWhiteSpace(t.Name)))
            ThemeCombo.Items.Add(new ThemeItem(theme.Name, theme.Name));

        var themeKey = ResolveSelectedThemeKey();
        _config.Theme = themeKey;
        ThemeCombo.SelectedItem = FindThemeItem(themeKey);
        RefreshSystemAccentCheck();
        RebuildColorRows();
    }

    string ResolveSelectedThemeKey()
    {
        if (_config == null)
            return AppTheme.Dark;

        var theme = AppTheme.NormalizeThemeKey(_config.Theme);
        if (AppTheme.IsBuiltInTheme(theme) || theme.Equals(AppThemeKeys.Custom, StringComparison.OrdinalIgnoreCase))
            return theme;

        return _config.UserThemes.Any(t => t.Name.Equals(theme, StringComparison.OrdinalIgnoreCase))
            ? theme
            : AppTheme.Dark;
    }

    void OnThemeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_binding || _config == null || ThemeCombo.SelectedItem is not ThemeItem item)
            return;

        _config.Theme = item.Key;
        EnsureSelectedCustomTheme();
        AppTheme.Apply(item.Key, _config);
        RefreshSystemAccentCheck();
        RebuildColorRows();
    }

    void RebuildColorRows()
    {
        if (_config == null)
            return;

        _binding = true;
        MainColorsPanel.Children.Clear();
        ViewerColorsPanel.Children.Clear();

        var colors = CurrentThemeColors();
        foreach (var entry in AppThemePalette.Entries)
        {
            var target = entry.IsViewerColor ? ViewerColorsPanel : MainColorsPanel;
            target.Children.Add(CreateColorRow(entry, colors));
        }

        ViewerPreview.InvalidateVisual();
        _binding = false;
    }

    Control CreateColorRow(AppThemePaletteEntry entry, Dictionary<string, string> colors)
    {
        var color = AppThemePalette.ParseColor(colors[entry.Key]);
        var systemAccent = UsesSystemAccentForCurrentTheme() && AppThemePalette.IsAccentKey(entry.Key);
        var picker = new Button
        {
            Width = 42,
            Height = 28,
            Padding = new Thickness(0),
            Background = new SolidColorBrush(color),
            BorderBrush = new SolidColorBrush(Color.FromRgb(96, 96, 96)),
            IsEnabled = !systemAccent,
            Opacity = systemAccent ? 0.65 : 1,
            Tag = entry.Key,
        };
        picker.Click += OnColorButtonClick;

        Grid.SetColumn(picker, 1);

        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("150,Auto"),
            Children =
            {
                new TextBlock
                {
                    Text = entry.Label,
                    FontSize = 12,
                    Opacity = systemAccent ? 0.65 : 1,
                    VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
                },
                picker,
            },
        };
    }

    async void OnColorButtonClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_binding || _config == null || sender is not Button button || button.Tag is not string key)
            return;

        if (UsesSystemAccentForCurrentTheme() && AppThemePalette.IsAccentKey(key))
            return;

        var colors = CurrentThemeColors();
        var original = AppThemePalette.ParseColor(colors[key]);
        var color = await PromptColor(key, original);
        if (color == null)
            return;

        button.Background = new SolidColorBrush(color.Value);
        ApplyColorChange(key, color.Value);
    }

    void ApplyColorChange(string key, Color color)
    {
        if (_config == null)
            return;

        var theme = EnsureEditableTheme();
        SetThemeColor(theme, key, ToHex(color));
        AppTheme.Apply(_config.Theme, _config);
        ViewerPreview.InvalidateVisual();
    }

    async Task<Color?> PromptColor(string key, Color original)
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        var colorView = new ColorView
        {
            Width = 360,
            Height = 420,
            Color = original,
            IsAlphaEnabled = true,
            IsAlphaVisible = true,
            IsComponentTextInputVisible = true,
            IsHexInputVisible = true,
        };
        var ok = new Button { Content = "OK", MinWidth = 80, IsDefault = true };
        var cancel = new Button { Content = "Cancel", MinWidth = 80, IsCancel = true };
        var window = new Window
        {
            Title = "Edit Color",
            Width = 400,
            Height = 530,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(12),
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = key, FontSize = 12, Opacity = 0.8 },
                    colorView,
                    new StackPanel
                    {
                        Orientation = global::Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { ok, cancel },
                    },
                },
            },
        };

        Color? result = null;
        ok.Click += (_, _) =>
        {
            result = colorView.Color;
            window.Close();
        };
        cancel.Click += (_, _) => window.Close();

        if (owner != null)
            await window.ShowDialog(owner);
        else
            window.Show();

        return result;
    }

    AppThemeDefinition EnsureEditableTheme()
    {
        if (_config == null)
            throw new InvalidOperationException("Appearance settings are not bound.");

        var selected = AppTheme.NormalizeThemeKey(_config.Theme);
        if (AppTheme.IsBuiltInTheme(selected))
        {
            _config.CustomThemeDraft = AppThemePalette.CreateTheme(AppThemeKeys.Custom, selected);
            _config.CustomThemeDraft.UseSystemAccentColor = _config.UseSystemAccentColor;
            _config.Theme = AppThemeKeys.Custom;
            RefreshThemeComboSelection();
            RefreshSystemAccentCheck();
            return _config.CustomThemeDraft;
        }

        EnsureSelectedCustomTheme();
        return AppTheme.FindTheme(_config, _config.Theme) ?? _config.CustomThemeDraft;
    }

    void EnsureSelectedCustomTheme()
    {
        if (_config == null)
            return;

        if (_config.Theme.Equals(AppThemeKeys.Custom, StringComparison.OrdinalIgnoreCase)
            && _config.CustomThemeDraft.Colors.Count == 0)
        {
            _config.CustomThemeDraft = AppThemePalette.CreateTheme(AppThemeKeys.Custom, AppTheme.Dark);
        }
    }

    Dictionary<string, string> CurrentThemeColors()
    {
        if (_config == null)
            return AppThemePalette.CreateTheme(AppThemeKeys.Custom, AppTheme.Dark)
                .Colors.ToDictionary(c => c.Key, c => c.Value, StringComparer.OrdinalIgnoreCase);

        var selected = AppTheme.NormalizeThemeKey(_config.Theme);
        var theme = AppTheme.IsBuiltInTheme(selected)
            ? AppThemePalette.CreateTheme(selected, selected)
            : AppTheme.FindTheme(_config, selected) ?? AppThemePalette.CreateTheme(AppThemeKeys.Custom, AppTheme.Dark);

        var values = AppThemePalette.NormalizeColors(theme)
            .Colors.ToDictionary(c => c.Key, c => c.Value, StringComparer.OrdinalIgnoreCase);

        var useSystemAccent = AppTheme.IsBuiltInTheme(selected)
            ? _config.UseSystemAccentColor
            : UsesSystemAccentForTheme(theme);
        if (useSystemAccent)
            OverlaySystemAccentValues(selected, values);

        return values;
    }

    void SetThemeColor(AppThemeDefinition theme, string key, string value)
    {
        var setting = theme.Colors.FirstOrDefault(c => c.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (setting == null)
        {
            theme.Colors.Add(new ThemeColorSetting { Key = key, Value = value });
            return;
        }

        setting.Value = value;
    }

    void RefreshThemeComboSelection()
    {
        _binding = true;
        ThemeCombo.SelectedItem = FindThemeItem(_config?.Theme ?? AppTheme.Dark);
        _binding = false;
    }

    void RefreshSystemAccentCheck()
    {
        if (_config == null)
            return;

        _binding = true;
        UseSystemAccentCheck.IsChecked = UsesSystemAccentForCurrentTheme();
        _binding = false;
    }

    void OnUseSystemAccentChanged()
    {
        if (_binding || _config == null)
            return;

        SetUseSystemAccentForCurrentTheme(UseSystemAccentCheck.IsChecked == true);
        AppTheme.Apply(_config.Theme, _config);
        RebuildColorRows();
    }

    bool UsesSystemAccentForCurrentTheme()
    {
        if (_config == null)
            return true;

        var selected = AppTheme.NormalizeThemeKey(_config.Theme);
        var theme = AppTheme.IsBuiltInTheme(selected) ? null : AppTheme.FindTheme(_config, selected);
        return AppTheme.ShouldUseSystemAccent(_config, theme);
    }

    bool UsesSystemAccentForTheme(AppThemeDefinition? theme) =>
        AppTheme.ShouldUseSystemAccent(_config, theme);

    void SetUseSystemAccentForCurrentTheme(bool value)
    {
        if (_config == null)
            return;

        var selected = AppTheme.NormalizeThemeKey(_config.Theme);
        if (AppTheme.IsBuiltInTheme(selected))
        {
            _config.UseSystemAccentColor = value;
            return;
        }

        var theme = AppTheme.FindTheme(_config, selected);
        if (theme == null)
        {
            _config.UseSystemAccentColor = value;
            return;
        }

        theme.UseSystemAccentColor = value;
        if (selected.Equals(AppThemeKeys.Custom, StringComparison.OrdinalIgnoreCase))
            _config.CustomThemeDraft.UseSystemAccentColor = value;
    }

    void OverlaySystemAccentValues(string selectedTheme, Dictionary<string, string> values)
    {
        if (Application.Current is not { } app)
            return;

        if (app.TryGetResource(AppThemePalette.EditableAccentKey, app.ActualThemeVariant, out var systemAccent))
        {
            values[AppThemePalette.EditableAccentKey] = systemAccent switch
            {
                Color color => ToHex(color),
                SolidColorBrush brush => ToHex(brush.Color),
                _ => values[AppThemePalette.EditableAccentKey],
            };
        }

        _ = selectedTheme;
    }

    void OnResetColorsClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_config == null)
            return;

        var selected = AppTheme.NormalizeThemeKey(_config.Theme);
        if (AppTheme.IsBuiltInTheme(selected))
            return;

        var theme = AppTheme.FindTheme(_config, selected);
        if (theme == null)
            return;

        var reset = AppThemePalette.CreateTheme(theme.Name, theme.BaseTheme);
        reset.UseSystemAccentColor = theme.UseSystemAccentColor;
        theme.Colors = reset.Colors;
        AppTheme.Apply(_config.Theme, _config);
        RebuildColorRows();
    }

    void OnPreviewViewerClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        PreviewViewerRequested?.Invoke(this, EventArgs.Empty);
    }

    async void OnSaveAsThemeClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_config == null)
            return;

        var name = await PromptThemeName();
        if (string.IsNullOrWhiteSpace(name))
            return;

        name = name.Trim();
        if (AppTheme.IsBuiltInTheme(name) || name.Equals(AppThemeKeys.Custom, StringComparison.OrdinalIgnoreCase))
            return;

        var source = AppTheme.IsBuiltInTheme(_config.Theme)
            ? AppThemePalette.CreateTheme(name, _config.Theme)
            : AppTheme.FindTheme(_config, _config.Theme)?.Clone() ?? _config.CustomThemeDraft.Clone();

        source.Name = name;
        source.UseSystemAccentColor = UsesSystemAccentForCurrentTheme();
        var saved = AppThemePalette.NormalizeColors(source);
        AppThemeFileService.Save(saved);
        AppThemeFileService.LoadInto(_config);
        _config.Theme = name;
        BindThemes();
        AppTheme.Apply(name, _config);
    }

    async Task<string?> PromptThemeName()
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        var textBox = new TextBox { Width = 240, PlaceholderText = "Theme name" };
        var ok = new Button { Content = "Save", MinWidth = 80, IsDefault = true };
        var cancel = new Button { Content = "Cancel", MinWidth = 80, IsCancel = true };
        var window = new Window
        {
            Title = "Save Theme",
            Width = 330,
            Height = 145,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(12),
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "Name:" },
                    textBox,
                    new StackPanel
                    {
                        Orientation = global::Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { ok, cancel },
                    },
                },
            },
        };

        string? result = null;
        ok.Click += (_, _) =>
        {
            result = textBox.Text;
            window.Close();
        };
        cancel.Click += (_, _) => window.Close();

        if (owner != null)
            await window.ShowDialog(owner);
        else
            window.Show();

        return result;
    }

    ThemeItem? FindThemeItem(string key)
    {
        foreach (var item in ThemeCombo.Items)
        {
            if (item is ThemeItem ti && ti.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                return ti;
        }

        return ThemeCombo.Items.Count > 0 ? ThemeCombo.Items[0] as ThemeItem : null;
    }

    static string ToHex(Color color) => $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    sealed record ThemeItem(string Key, string Label)
    {
        public override string ToString() => Label;
    }
}
