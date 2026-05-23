using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.App;
using CNC.Controls.Avalonia.Services;
using CNC.Controls.Avalonia.Views;
using CNC.Controls.Config;
using CNC.Core;
using GrblConfigApp.Services;

namespace GrblConfigApp;

public partial class MainWindow : Window
{
    readonly GrblViewModel _grbl;
    readonly MachineConnectionCoordinator _coordinator;
    readonly ConnectionService _connection;
    readonly MachineConnectionInitializer _connectionInitializer = new();
    readonly GrblCommandRouter _commandRouter = new();
    readonly AppConfigService _appConfig;
    readonly PlatformServices _platform;

    public MainWindow(PlatformServices platform, AppConfigService appConfig)
    {
        _platform = platform;
        _appConfig = appConfig;
        _grbl = new GrblViewModel { PathService = platform.PathService };
        _connection = new ConnectionService(platform.SerialPortDiscovery, platform.UiDispatcher);
        _coordinator = new MachineConnectionCoordinator(_connection);
        InitializeComponent();
        DataContext = _grbl;
        Grbl.GrblViewModel = _grbl;
        configView.DataContext = _grbl;
        _commandRouter.Attach(_grbl);
        ControlsPlatformContext.CommandRouter = _commandRouter;
        Opened += OnOpened;
        Closing += OnClosing;
    }

    async void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        await ShowPortDialogAsync();
    }

    void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        configView.Activate(false);
        _connectionInitializer.Unregister();
        if (Comms.com != null)
            Comms.com.Close();
    }

    async Task ShowPortDialogAsync()
    {
        var dialog = new PortDialog(
            _platform.SerialPortDiscovery,
            _coordinator,
            _grbl,
            _appConfig.Base.PollInterval,
            _appConfig.Base.ResetDelay);

        if (await dialog.ShowDialog<bool?>(this) != true)
            return;

        if (!_connectionInitializer.Initialize(_grbl, _appConfig.Base.PollInterval))
        {
            _connectionInitializer.Unregister();
            _coordinator.Detach(_grbl);
            return;
        }

        configView.Activate(true);
    }
}
