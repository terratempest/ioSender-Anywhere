using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Controls.Primitives;
using CNC.App;
using CNC.Controls.Avalonia.Services;

namespace CNC.Controls.Config;

public partial class AppConfigView : UserControl
{
    readonly AppConfigService? _appConfig;

    public AppConfigView() : this(null)
    {
    }

    public AppConfigView(AppConfigService? appConfig, IGameControllerBindingCapture? controllerCapture = null)
    {
        _appConfig = appConfig;
        InitializeComponent();
        controllerConfig.Capture = controllerCapture;
        SettingsList.SelectedIndex = 0;
        ShowSettingsPage(0);
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

    void OnSettingsSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _ = e;

        var selectedIndex = sender is SelectingItemsControl list ? list.SelectedIndex : 0;
        ShowSettingsPage(selectedIndex);
    }

    void ShowSettingsPage(int selectedIndex)
    {
        ProbingPage.IsVisible = selectedIndex == 0;
        JogPage.IsVisible = selectedIndex == 1;
        AppearancePage.IsVisible = selectedIndex == 2;
        MachinePage.IsVisible = selectedIndex == 3;
        CameraPage.IsVisible = selectedIndex == 4;
        ViewerPage.IsVisible = selectedIndex == 5;
        StripPage.IsVisible = selectedIndex == 6;
        KeyboardPage.IsVisible = selectedIndex == 7;
        ControllerPage.IsVisible = selectedIndex == 8;
    }

    void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        _appConfig?.Save();
    }
}
