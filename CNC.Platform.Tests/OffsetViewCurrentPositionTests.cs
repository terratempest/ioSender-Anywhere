using System.Reflection;
using CNC.Controls.Avalonia.Views;
using CNC.Core;
using CNC.GCode;

namespace CNC.Platform.Tests;

public sealed class OffsetViewCurrentPositionTests : IDisposable
{
    readonly FakeStreamComms _comms = new();
    readonly AxisFlags _axisFlags;

    public OffsetViewCurrentPositionTests()
    {
        Comms.com = _comms;
        _axisFlags = GrblInfo.AxisFlags;
        SetAxisFlags(AxisFlags.XYZ);
    }

    public void Dispose()
    {
        SetAxisFlags(_axisFlags);
        Comms.com = null;
    }

    [Fact]
    public async Task Current_position_request_updates_offset_from_status()
    {
        var view = CreateViewWithCommsSubscription();

        var request = view.RequestCurrentPositionAsync(250);
        _comms.Raise("<Idle|MPos:1.000,2.000,3.000|WCO:0.000,0.000,0.000>");

        Assert.True(await request);
        Assert.Equal(1d, view.Offset.X);
        Assert.Equal(2d, view.Offset.Y);
        Assert.Equal(3d, view.Offset.Z);
    }

    [Fact]
    public async Task Current_position_request_timeout_leaves_offset_unchanged()
    {
        var view = CreateViewWithCommsSubscription();
        view.Offset.X = 10d;
        view.Offset.Y = 20d;
        view.Offset.Z = 30d;

        var result = await view.RequestCurrentPositionAsync(10);

        Assert.False(result);
        Assert.Equal(10d, view.Offset.X);
        Assert.Equal(20d, view.Offset.Y);
        Assert.Equal(30d, view.Offset.Z);
    }

    [Fact]
    public async Task Repeated_current_position_clicks_share_one_in_flight_request()
    {
        var view = CreateViewWithCommsSubscription();

        var first = view.RequestCurrentPositionAsync(250);
        var second = view.RequestCurrentPositionAsync(250);

        Assert.Same(first, second);
        Assert.Single(_comms.Bytes);

        _comms.Raise("<Idle|MPos:4.000,5.000,6.000|WCO:0.000,0.000,0.000>");

        Assert.True(await first);
        Assert.Equal(4d, view.Offset.X);
        Assert.Equal(5d, view.Offset.Y);
        Assert.Equal(6d, view.Offset.Z);
    }

    OffsetView CreateViewWithCommsSubscription()
    {
        var view = new OffsetView();
        var field = typeof(OffsetView).GetField("_parameters", BindingFlags.Instance | BindingFlags.NonPublic);
        var parameters = Assert.IsType<GrblViewModel>(field?.GetValue(view));
        _comms.DataReceived += parameters.DataReceived;
        return view;
    }

    static void SetAxisFlags(AxisFlags flags)
    {
        typeof(GrblInfo)
            .GetProperty(nameof(GrblInfo.AxisFlags), BindingFlags.Static | BindingFlags.Public)!
            .SetValue(null, flags);
    }

    sealed class FakeStreamComms : StreamComms
    {
        public List<byte> Bytes { get; } = new();
        public bool IsOpen { get; set; } = true;
        public int OutCount => 0;
        public string Reply => string.Empty;
        public Comms.StreamType StreamType => Comms.StreamType.Serial;
        public Comms.State CommandState { get; set; } = Comms.State.ACK;
        public bool EventMode { get; set; } = true;
        public Action<int>? ByteReceived { get; set; }
        public event DataReceivedHandler? DataReceived;

        public void Close() => IsOpen = false;
        public int ReadByte() => -1;
        public void WriteByte(byte data) => Bytes.Add(data);
        public void WriteBytes(byte[] bytes, int len) { }
        public void WriteString(string data) { }
        public void WriteCommand(string command) { }
        public string GetReply(string command) => string.Empty;
        public void AwaitAck() { }
        public void AwaitAck(string command) { }
        public void AwaitResponse(string command) { }
        public void AwaitResponse() { }
        public void PurgeQueue() { }
        public void Raise(string data) => DataReceived?.Invoke(data);
    }
}
