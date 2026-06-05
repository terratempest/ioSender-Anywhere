using System.Reflection;
using CNC.Controls.Avalonia.Services;
using CNC.Core;

namespace CNC.Platform.Tests;

public sealed class ResetTloLedSyncTests : IDisposable
{
    readonly GrblViewModel _vm = new();
    readonly GrblInfoSnapshot _grblInfo = GrblInfoSnapshot.Capture();

    public ResetTloLedSyncTests()
    {
        Comms.com = new ParameterFakeComms();
        GrblInfoSnapshot.SetGrblHAL(true);
    }

    public void Dispose()
    {
        Comms.com = null;
        _grblInfo.Restore();
    }

    [Fact]
    public void Grbl_welcome_clears_tool_offset_led_immediately()
    {
        _vm.DataReceived("[TLO:0.000,0.000,12.345]");
        Assert.True(_vm.IsToolOffsetActive);

        _vm.DataReceived("Grbl 1.1f ['$' for help]");

        Assert.False(_vm.IsToolOffsetActive);
        Assert.True(_vm.IsToolOffsetIndicatorVisible);
    }

    [Fact]
    public void Refresh_noops_when_not_grbl_hal()
    {
        GrblInfoSnapshot.SetGrblHAL(false);
        _vm.DataReceived("[TLO:0.000,0.000,12.345]");
        Assert.True(_vm.IsToolOffsetActive);

        ControllerWorkParametersSync.Refresh(_vm);

        Assert.True(_vm.IsToolOffsetActive);
    }

    [Fact]
    public void Tlo_zero_report_after_active_offset_clears_led()
    {
        _vm.DataReceived("[TLO:0.000,0.000,12.345]");
        Assert.True(_vm.IsToolOffsetActive);

        _vm.DataReceived("[TLO:0.000,0.000,0.000]");

        Assert.False(_vm.IsToolOffsetActive);
        Assert.True(_vm.IsToolOffsetIndicatorVisible);
    }

    sealed record GrblInfoSnapshot(bool IsGrblHAL)
    {
        public static GrblInfoSnapshot Capture() => new(GrblInfo.IsGrblHAL);

        public static void SetGrblHAL(bool isGrblHal) =>
            SetStaticProperty(nameof(GrblInfo.IsGrblHAL), isGrblHal);

        public void Restore() => SetGrblHAL(IsGrblHAL);

        static void SetStaticProperty(string propertyName, object value)
        {
            var property = typeof(GrblInfo).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(property);
            property.SetValue(null, value);
        }
    }

    sealed class ParameterFakeComms : StreamComms
    {
        public bool IsOpen { get; set; } = true;
        public int OutCount => 0;
        public string Reply { get; private set; } = string.Empty;
        public Comms.StreamType StreamType => Comms.StreamType.Serial;
        public Comms.State CommandState { get; set; } = Comms.State.ACK;
        public bool EventMode { get; set; } = true;
        public Action<int>? ByteReceived { get; set; }
#pragma warning disable CS0067
        public event DataReceivedHandler? DataReceived;
#pragma warning restore CS0067

        public void Close() => IsOpen = false;
        public int ReadByte() => -1;
        public void WriteByte(byte data) { }
        public void WriteBytes(byte[] bytes, int len) { }
        public void WriteString(string data) { }
        public void WriteCommand(string command) { }
        public string GetReply(string command) => Reply;
        public void AwaitAck() { }
        public void AwaitAck(string command) { }
        public void AwaitResponse(string command) { }
        public void AwaitResponse() { }
        public void PurgeQueue() { }
    }
}
