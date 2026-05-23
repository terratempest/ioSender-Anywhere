using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CNC.Controls.Avalonia.Services;
using CNC.Core;
using CNC.Platform.Abstractions;

namespace CNC.Controls.Avalonia.ViewModels;

public enum PortDialogTab
{
    Serial,
    Telnet,
    Websocket
}

public sealed class PortDialogViewModel : INotifyPropertyChanged
{
    private static readonly string[] DefaultBaudRates = ["115200", "230400", "460800", "921600"];

    private readonly ISerialPortDiscovery _portDiscovery;
    private readonly ConnectionService _connectionService;
    private readonly MachineConnectionCoordinator _coordinator;
    private readonly GrblViewModel _grbl;
    private readonly int _pollInterval;
    private readonly int _resetDelay;
    private SerialPortInfo? _selectedPort;
    private string _selectedBaud = "115200";
    private string _telnetHost = "192.168.1.1";
    private string _telnetPort = "23";
    private string _websocketHost = "192.168.1.1";
    private string _websocketPort = "81";
    private PortDialogTab _selectedTab = PortDialogTab.Serial;
    private string? _errorMessage;

    public PortDialogViewModel(
        ISerialPortDiscovery portDiscovery,
        MachineConnectionCoordinator coordinator,
        GrblViewModel grbl,
        int pollInterval,
        int resetDelay = 0,
        string? savedPortParams = null)
    {
        _portDiscovery = portDiscovery;
        _connectionService = coordinator.Connection;
        _coordinator = coordinator;
        _grbl = grbl;
        _pollInterval = pollInterval;
        _resetDelay = resetDelay;
        foreach (var baud in DefaultBaudRates)
            BaudRates.Add(baud);
        RefreshPorts();
        if (!SavedPortParams.IsPlaceholder(savedPortParams))
            ApplySavedPortParams(savedPortParams!);
    }

    public string? ConnectedPortParams { get; private set; }

    public ObservableCollection<SerialPortInfo> Ports { get; } = new();
    public ObservableCollection<string> BaudRates { get; } = new();

    public int SelectedTabIndex
    {
        get => (int)_selectedTab;
        set
        {
            var tab = (PortDialogTab)value;
            if (_selectedTab == tab)
                return;
            _selectedTab = tab;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanConnect));
        }
    }

    public SerialPortInfo? SelectedPort
    {
        get => _selectedPort;
        set
        {
            if (ReferenceEquals(_selectedPort, value))
                return;
            _selectedPort = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanConnect));
        }
    }

    public string SelectedBaud
    {
        get => _selectedBaud;
        set
        {
            if (_selectedBaud == value)
                return;
            _selectedBaud = value;
            OnPropertyChanged();
        }
    }

    public string TelnetHost
    {
        get => _telnetHost;
        set
        {
            if (_telnetHost == value)
                return;
            _telnetHost = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanConnect));
        }
    }

    public string TelnetPort
    {
        get => _telnetPort;
        set
        {
            if (_telnetPort == value)
                return;
            _telnetPort = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanConnect));
        }
    }

    public string WebsocketHost
    {
        get => _websocketHost;
        set
        {
            if (_websocketHost == value)
                return;
            _websocketHost = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanConnect));
        }
    }

    public string WebsocketPort
    {
        get => _websocketPort;
        set
        {
            if (_websocketPort == value)
                return;
            _websocketPort = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanConnect));
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage == value)
                return;
            _errorMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool CanConnect => _selectedTab switch
    {
        PortDialogTab.Serial => SelectedPort != null,
        PortDialogTab.Telnet => !string.IsNullOrWhiteSpace(TelnetHost) && int.TryParse(TelnetPort, out var p) && p > 0,
        PortDialogTab.Websocket => !string.IsNullOrWhiteSpace(WebsocketHost) && int.TryParse(WebsocketPort, out var w) && w > 0,
        _ => false
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshPorts()
    {
        var previous = SelectedPort?.Name;
        Ports.Clear();
        foreach (var port in _portDiscovery.GetPorts())
            Ports.Add(port);

        SelectedPort = previous == null
            ? Ports.FirstOrDefault()
            : Ports.FirstOrDefault(p => string.Equals(p.Name, previous, StringComparison.OrdinalIgnoreCase))
              ?? Ports.FirstOrDefault();
    }

    public bool TryConnect()
    {
        ErrorMessage = null;

        try
        {
            switch (_selectedTab)
            {
                case PortDialogTab.Serial:
                    if (SelectedPort == null)
                    {
                        ErrorMessage = "Select a serial port.";
                        return false;
                    }

                    _connectionService.ConnectSerial($"{SelectedPort.Name}:{SelectedBaud},N,8,1", _resetDelay);
                    break;

                case PortDialogTab.Telnet:
                    if (!int.TryParse(TelnetPort, out var telnetPort) || telnetPort <= 0)
                    {
                        ErrorMessage = "Enter a valid telnet port.";
                        return false;
                    }

                    _connectionService.ConnectTelnet(TelnetHost.Trim(), telnetPort);
                    break;

                case PortDialogTab.Websocket:
                    if (!int.TryParse(WebsocketPort, out var wsPort) || wsPort <= 0)
                    {
                        ErrorMessage = "Enter a valid WebSocket port.";
                        return false;
                    }

                    _connectionService.ConnectWebsocket(WebsocketHost.Trim(), wsPort);
                    break;
            }

            if (!_connectionService.IsConnected)
            {
                ErrorMessage = "Connection failed.";
                return false;
            }

            if (!_coordinator.AttachAfterConnect(_grbl))
            {
                _connectionService.Disconnect();
                ErrorMessage = "Connection failed.";
                return false;
            }

            ConnectedPortParams = GetConnectedPortParams();
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return false;
        }
    }

    public void ApplySavedPortParams(string portParams)
    {
        switch (SavedPortParams.Classify(portParams))
        {
            case PortEndpointKind.Serial:
                SelectedTabIndex = (int)PortDialogTab.Serial;
                try
                {
                    var settings = PortSettingsParser.Parse(portParams);
                    _selectedBaud = settings.BaudRate.ToString();
                    OnPropertyChanged(nameof(SelectedBaud));
                    SelectedPort = Ports.FirstOrDefault(p =>
                        string.Equals(p.Name, settings.PortName, StringComparison.OrdinalIgnoreCase))
                        ?? SelectedPort;
                }
                catch
                {
                }
                break;

            case PortEndpointKind.Telnet:
                SelectedTabIndex = (int)PortDialogTab.Telnet;
                if (SavedPortParams.TryParseTelnet(portParams, out var telnetHost, out var telnetPort))
                {
                    TelnetHost = telnetHost;
                    TelnetPort = telnetPort.ToString();
                }
                break;

            case PortEndpointKind.Websocket:
                SelectedTabIndex = (int)PortDialogTab.Websocket;
                if (SavedPortParams.TryParseWebsocket(portParams, out var wsHost, out var wsPort))
                {
                    WebsocketHost = wsHost;
                    WebsocketPort = wsPort.ToString();
                }
                break;
        }
    }

    string GetConnectedPortParams() => _selectedTab switch
    {
        PortDialogTab.Serial when SelectedPort != null =>
            $"{SelectedPort.Name}:{SelectedBaud},N,8,1",
        PortDialogTab.Telnet =>
            $"{TelnetHost.Trim()}:{TelnetPort.Trim()}",
        PortDialogTab.Websocket =>
            $"ws://{WebsocketHost.Trim()}:{WebsocketPort.Trim()}",
        _ => _connectionService.PortParameters ?? string.Empty
    };

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
