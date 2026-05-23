using CNC.Core;
using CNC.Platform.Abstractions;

namespace CNC.Controls.Avalonia.Services;

public enum ConnectionKind
{
    Serial,
    Telnet,
    Websocket
}

public sealed class ConnectionService
{
    private readonly ISerialPortDiscovery _portDiscovery;
    private readonly IUiDispatcher _uiDispatcher;
    private StreamComms? _stream;

    public ConnectionService(ISerialPortDiscovery portDiscovery, IUiDispatcher uiDispatcher)
    {
        ArgumentNullException.ThrowIfNull(portDiscovery);
        ArgumentNullException.ThrowIfNull(uiDispatcher);
        _portDiscovery = portDiscovery;
        _uiDispatcher = uiDispatcher;
    }

    public StreamComms? Stream => _stream;

    public ConnectionKind? ActiveKind { get; private set; }

    public bool IsConnected => _stream is { IsOpen: true };

    public string? PortParameters { get; private set; }

    public StreamComms ConnectSerial(string portParameters, int resetDelay = 0)
    {
        Disconnect();
        PortParameters = portParameters;
        ActiveKind = ConnectionKind.Serial;
        _stream = new SerialStream(portParameters, resetDelay, _portDiscovery, _uiDispatcher);
        return _stream;
    }

    public StreamComms ConnectTelnet(string host, int port)
    {
        Disconnect();
        var endpoint = $"{host}:{port}";
        PortParameters = endpoint;
        ActiveKind = ConnectionKind.Telnet;
        _stream = new TelnetStream(endpoint, _uiDispatcher);
        return _stream;
    }

    public StreamComms ConnectWebsocket(string host, int port)
    {
        Disconnect();
        var endpoint = $"ws://{host}:{port}";
        PortParameters = endpoint;
        ActiveKind = ConnectionKind.Websocket;
        _stream = new WebsocketStream(endpoint, _uiDispatcher);
        return _stream;
    }

    public StreamComms Connect(string portParameters, int resetDelay = 0) =>
        ConnectSerial(portParameters, resetDelay);

    /// <summary>
    /// Opens a connection from a persisted <c>PortParams</c> string (serial, telnet, or WebSocket).
    /// </summary>
    public bool TryConnectFromPortParams(string portParams, int resetDelay = 0)
    {
        if (SavedPortParams.IsPlaceholder(portParams))
            return false;

        try
        {
            switch (SavedPortParams.Classify(portParams))
            {
                case PortEndpointKind.Serial:
                    ConnectSerial(portParams.Trim(), resetDelay);
                    break;

                case PortEndpointKind.Telnet:
                    if (!SavedPortParams.TryParseTelnet(portParams, out var telnetHost, out var telnetPort))
                        return false;
                    ConnectTelnet(telnetHost, telnetPort);
                    break;

                case PortEndpointKind.Websocket:
                    if (!SavedPortParams.TryParseWebsocket(portParams, out var wsHost, out var wsPort))
                        return false;
                    ConnectWebsocket(wsHost, wsPort);
                    break;

                default:
                    return false;
            }

            return IsConnected;
        }
        catch
        {
            Disconnect();
            return false;
        }
    }

    public void Disconnect()
    {
        if (_stream == null)
            return;

        try
        {
            if (ReferenceEquals(Comms.com, _stream))
                Comms.com = null;
            _stream.Close();
        }
        catch
        {
        }

        _stream = null;
        PortParameters = null;
        ActiveKind = null;
    }
}
