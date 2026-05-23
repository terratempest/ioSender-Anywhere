using CNC.Core;

namespace CNC.Platform.Tests;

public sealed class SavedPortParamsTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("COMn:115200,N,8,1", true)]
    [InlineData("COM3:115200,N,8,1", false)]
    public void IsPlaceholder_detects_default_and_real_values(string? value, bool expected) =>
        Assert.Equal(expected, SavedPortParams.IsPlaceholder(value));

    [Theory]
    [InlineData("COM3:115200,N,8,1", PortEndpointKind.Serial)]
    [InlineData("192.168.1.10:23", PortEndpointKind.Telnet)]
    [InlineData("ws://192.168.1.10:81", PortEndpointKind.Websocket)]
    [InlineData("COMn:115200,N,8,1", PortEndpointKind.Unknown)]
    public void Classify_maps_endpoint_kinds(string portParams, PortEndpointKind expected) =>
        Assert.Equal(expected, SavedPortParams.Classify(portParams));

    [Fact]
    public void TryParseTelnet_splits_host_and_port()
    {
        Assert.True(SavedPortParams.TryParseTelnet("10.0.0.5:23", out var host, out var port));
        Assert.Equal("10.0.0.5", host);
        Assert.Equal(23, port);
    }

    [Fact]
    public void TryParseWebsocket_strips_scheme()
    {
        Assert.True(SavedPortParams.TryParseWebsocket("ws://grbl.local:81", out var host, out var port));
        Assert.Equal("grbl.local", host);
        Assert.Equal(81, port);
    }
}
