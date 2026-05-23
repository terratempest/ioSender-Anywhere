using System.Collections.ObjectModel;
using System.Diagnostics;
using CNC.Controls.Avalonia.Services;
using CNC.Core;
using CNC.GCodeViewer.Avalonia;
using CNC.Platform.Abstractions;
using ioSender.Services;

namespace ioSender.ViewModels;

public sealed class MainWindowViewModel
{
    private static MainWindowViewModel? _current;
    private readonly ConnectionService? _connectionService;

    public static MainWindowViewModel Current =>
        _current ?? throw new InvalidOperationException("Main window view model is not initialized.");

    public static GrblViewModel Singleton => Current.Grbl;

    public MainWindowViewModel(PlatformServices platform, ConnectionService? connectionService = null)
    {
        Platform = platform;
        _connectionService = connectionService;
        Grbl = new GrblViewModel { PathService = platform.PathService };
        CNC.Core.Grbl.GrblViewModel = Grbl;
        GCodeFileService.Instance.Model = Grbl;
        GCodeViewerContext.Grbl = Grbl;
        _current = this;
    }

    public PlatformServices Platform { get; }

    public GrblViewModel Grbl { get; }

    public bool IsConnected => _connectionService?.IsConnected == true;

    public string DisconnectedStatusMessage { get; } =
        "Not connected — use Connect to open the port dialog.";

    public event System.Action? ConnectionChanged;

    public UiLayoutMode LayoutMode
    {
        get => AppHostContext.AppConfig.Base.LayoutMode;
        set
        {
            if (AppHostContext.AppConfig.Base.LayoutMode == value)
                return;
            AppHostContext.AppConfig.Base.LayoutMode = value;
            LayoutModeChanged?.Invoke();
        }
    }

    public ObservableCollection<SerialPortInfo> SerialPorts { get; } = new();

    public string StatusMessage { get; set; } = "ioSender";

    public void NotifyConnectionChanged() => ConnectionChanged?.Invoke();

    public event System.Action? LayoutModeChanged;

    public void RefreshPorts()
    {
        var sw = Stopwatch.StartNew();
        SerialPorts.Clear();
        foreach (var port in Platform.SerialPortDiscovery.GetPorts())
            SerialPorts.Add(port);
        StartupTrace.Mark($"Serial port refresh completed in {sw.ElapsedMilliseconds} ms ({SerialPorts.Count} ports)");
    }

    public void ToggleLayoutMode()
    {
        LayoutMode = LayoutMode == UiLayoutMode.Compact
            ? UiLayoutMode.Expanded
            : UiLayoutMode.Compact;
    }
}
