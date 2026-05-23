using Avalonia.Controls;

using CNC.App;



namespace CNC.Controls.Config;



public partial class AppAppearanceConfigPanel : UserControl

{

    static readonly (string Key, string Label)[] ThemeEntries =

    [

        ("Standard", "Dark"),

        ("Black", "Darker"),

        ("Light", "Light"),

        ("White", "Light (bright)"),

    ];



    BaseConfig? _config;



    public AppAppearanceConfigPanel()

    {

        InitializeComponent();

        DataContextChanged += (_, _) => BindThemes();

    }



    void BindThemes()

    {

        _config = DataContext as BaseConfig;

        if (_config == null)

            return;



        _config.Themes.Clear();

        foreach (var (key, label) in ThemeEntries)

            _config.Themes[key] = label;



        ThemeCombo.Items.Clear();

        foreach (var (key, label) in ThemeEntries)

            ThemeCombo.Items.Add(new ThemeItem(key, label));



        var theme = NormalizeThemeKey(_config.Theme);

        _config.Theme = theme;

        ThemeCombo.SelectedItem = FindThemeItem(theme);

    }



    void OnThemeSelectionChanged(object? sender, SelectionChangedEventArgs e)

    {

        if (_config == null || ThemeCombo.SelectedItem is not ThemeItem item)

            return;



        _config.Theme = item.Key;

        AppTheme.Apply(item.Key);

    }



    static string NormalizeThemeKey(string? theme) => AppTheme.NormalizeThemeKey(theme);



    ThemeItem? FindThemeItem(string key)

    {

        foreach (var item in ThemeCombo.Items)

        {

            if (item is ThemeItem ti && ti.Key.Equals(key, StringComparison.OrdinalIgnoreCase))

                return ti;

        }



        return ThemeCombo.Items.Count > 0 ? ThemeCombo.Items[0] as ThemeItem : null;

    }



    sealed record ThemeItem(string Key, string Label)

    {

        public override string ToString() => Label;

    }

}

