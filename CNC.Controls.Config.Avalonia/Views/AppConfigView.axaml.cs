using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.App;
using CNC.Controls.Avalonia.Services;

namespace CNC.Controls.Config;

public partial class AppConfigView : UserControl
{
    readonly AppConfigService? _appConfig;

    public AppConfigView() : this(null)
    {
    }

    public AppConfigView(AppConfigService? appConfig)
    {
        _appConfig = appConfig;
        InitializeComponent();
        if (appConfig != null)
            DataContext = appConfig.Base;
        Loaded += OnLoaded;
    }

    void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not BaseConfig baseConfig)
            return;

        var mode = baseConfig.Jog.Mode;
        jogUiConfig.IsVisible = mode != JogConfig.JogMode.Keypad;
        jogConfig.IsVisible = mode != JogConfig.JogMode.UI;
    }

    void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        _appConfig?.Save();
    }
}
