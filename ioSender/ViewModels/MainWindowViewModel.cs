using System.Collections.ObjectModel;
using System.Diagnostics;
using CNC.Controls.Avalonia.Services;
using CNC.Controls.Avalonia.ViewModels;
using CNC.Core;
using CNC.Platform.Abstractions;
using ioSender.Services;

namespace ioSender.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private static MainWindowViewModel? _current;
    private readonly ConnectionService? _connectionService;

    public static MainWindowViewModel Current =>
        _current ?? throw new InvalidOperationException("Main window view model is not initialized.");

    public static GrblViewModel Singleton => Current.Grbl;

    public MainWindowViewModel(AppSession session)
        : this(session.Platform, session.Connection)
    {
    }

    public MainWindowViewModel(PlatformServices platform, ConnectionService? connectionService = null)
    {
        Platform = platform;
        _connectionService = connectionService;
        Grbl = new GrblViewModel { PathService = platform.PathService };
        CNC.Core.Grbl.GrblViewModel = Grbl;
        _current = this;
    }

    public PlatformServices Platform { get; }

    public GrblViewModel Grbl { get; }

    public JogViewModel Jog => JogViewModel.Shared;

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

    bool _isProgramLoading;
    bool _isPreviewBuilding;
    int _previewBuilds;

    public bool IsProgramLoading
    {
        get => _isProgramLoading;
        set
        {
            if (_isProgramLoading == value)
                return;
            _isProgramLoading = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsProgramBusy));
        }
    }

    public bool IsPreviewBuilding
    {
        get => _isPreviewBuilding;
        set
        {
            if (_isPreviewBuilding == value)
                return;
            _isPreviewBuilding = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsProgramBusy));
        }
    }

    public bool IsProgramBusy => IsProgramLoading || IsPreviewBuilding;

    public void SetPreviewBuilding(bool isBuilding)
    {
        _previewBuilds = isBuilding
            ? _previewBuilds + 1
            : Math.Max(0, _previewBuilds - 1);
        IsPreviewBuilding = _previewBuilds > 0;
    }

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
