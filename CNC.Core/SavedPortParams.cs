namespace CNC.Core;

public enum PortEndpointKind
{
    Unknown,
    Serial,
    Telnet,
    Websocket
}

/// <summary>
/// Classifies and parses persisted <see cref="CNC.App.BaseConfig.PortParams"/> connection strings.
/// </summary>
public static class SavedPortParams
{
    public const string DefaultPlaceholder = "COMn:115200,N,8,1";

    public static bool IsPlaceholder(string? portParams) =>
        string.IsNullOrWhiteSpace(portParams)
        || string.Equals(portParams.Trim(), DefaultPlaceholder, StringComparison.OrdinalIgnoreCase);

    public static PortEndpointKind Classify(string portParams)
    {
        if (IsPlaceholder(portParams))
            return PortEndpointKind.Unknown;

        var trimmed = portParams.Trim();
        if (trimmed.StartsWith("ws://", StringComparison.OrdinalIgnoreCase))
            return PortEndpointKind.Websocket;

        if (trimmed.Length > 0 && char.IsDigit(trimmed[0]))
            return PortEndpointKind.Telnet;

        return PortEndpointKind.Serial;
    }

    public static bool TryParseTelnet(string portParams, out string host, out int port)
    {
        host = string.Empty;
        port = 0;

        if (Classify(portParams) != PortEndpointKind.Telnet)
            return false;

        return TryParseHostPort(portParams.Trim(), out host, out port);
    }

    public static bool TryParseWebsocket(string portParams, out string host, out int port)
    {
        host = string.Empty;
        port = 0;

        var trimmed = portParams.Trim();
        if (!trimmed.StartsWith("ws://", StringComparison.OrdinalIgnoreCase))
            return false;

        return TryParseHostPort(trimmed["ws://".Length..], out host, out port);
    }

    static bool TryParseHostPort(string hostPort, out string host, out int port)
    {
        host = string.Empty;
        port = 0;

        var colon = hostPort.LastIndexOf(':');
        if (colon <= 0 || colon >= hostPort.Length - 1)
            return false;

        host = hostPort[..colon].Trim();
        if (string.IsNullOrWhiteSpace(host))
            return false;

        return int.TryParse(hostPort[(colon + 1)..], out port) && port > 0;
    }
}
