using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.App;

namespace CNC.Controls.Config;

public partial class AppControllerConfigPanel : UserControl
{
    IGameControllerBindingCapture? _capture;

    public AppControllerConfigPanel()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => BindConfig();
    }

    public IGameControllerBindingCapture? Capture
    {
        get => _capture;
        set
        {
            _capture = value;
            RefreshStatus();
        }
    }

    void BindConfig()
    {
        if (DataContext is not BaseConfig config)
            return;

        config.GameController ??= new GameControllerConfig();
        config.GameController.EnsureDefaultBindings();
        BindingsList.ItemsSource = config.GameController.Bindings;
        RefreshStatus();
    }

    async void OnCaptureClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: GameControllerBinding target })
            return;

        if (_capture is null)
        {
            CaptureText.Text = "Controller capture is not available.";
            return;
        }

        CaptureText.Text = $"Press a controller input for {target.ActionLabel}.";
        var captured = await _capture.CaptureAsync();
        if (captured is null)
        {
            CaptureText.Text = "No controller input captured.";
            return;
        }

        target.InputKind = captured.InputKind;
        target.InputName = captured.InputName;
        target.Threshold = captured.Threshold;
        CaptureText.Text = $"{target.ActionLabel} bound to {target.InputDisplay}.";
    }

    void OnResetDefaultsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not BaseConfig config)
            return;

        config.GameController.ResetBindings();
        BindingsList.ItemsSource = config.GameController.Bindings;
        CaptureText.Text = "Controller bindings reset to defaults.";
    }

    void OnRefreshStatusClick(object? sender, RoutedEventArgs e) => RefreshStatus();

    void RefreshStatus()
    {
        StatusText.Text = _capture?.StatusText ?? "Controller runtime is not available.";
    }
}
