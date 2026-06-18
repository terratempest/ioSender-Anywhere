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
        appearanceConfig.PreviewViewerRequested += (_, _) => PreviewViewerRequested?.Invoke(this, EventArgs.Empty);
        SettingsList.SelectedIndex = 0;
        ShowSettingsPage("General");
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

    public event EventHandler? PreviewViewerRequested;

    void OnSettingsSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _ = e;

        if (sender is not SelectingItemsControl { SelectedItem: ListBoxItem { Tag: string pageId } })
            return;

        ShowSettingsPage(pageId);
    }

    void ShowSettingsPage(string pageId)
    {
        GeneralPage.IsVisible = pageId == "General";
        MachinePage.IsVisible = pageId == "Machine";
        MotionPage.IsVisible = pageId == "Motion";
        CameraPage.IsVisible = pageId == "Camera";
        DisplayPage.IsVisible = pageId == "Display";
        InputPage.IsVisible = pageId == "Input";
    }

    void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        _appConfig?.Save();
    }
}
