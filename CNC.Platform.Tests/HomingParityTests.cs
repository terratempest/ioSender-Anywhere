using System.Globalization;
using System.Reflection;
using CNC.Controls.Avalonia.Converters;
using CNC.Controls.Avalonia.Services;
using CNC.Core;
using CNC.GCodeViewer.Avalonia;
using CNC.Platform.Abstractions;

namespace CNC.Platform.Tests;

public sealed class HomingParityTests : IDisposable
{
    readonly FakeStreamComms _comms = new();

    public HomingParityTests()
    {
        Comms.com = _comms;
    }

    public void Dispose()
    {
        Comms.com = null;
        JobTimer.Stop();
    }

    [Fact]
    public void ClearConnectionState_resets_live_controller_homing_without_clearing_loaded_file()
    {
        var vm = new GrblViewModel
        {
            FileName = @"C:\tmp\loaded.nc",
            IsReady = true
        };
        vm.DataReceived("<Idle|MPos:1,2,3|H:1|Pn:X|A:S|FS:100,500|P:1|Bf:15,128>");

        vm.ClearConnectionState();

        Assert.Equal(@"C:\tmp\loaded.nc", vm.FileName);
        Assert.True(vm.IsFileLoaded);
        Assert.Equal(GrblStates.Unknown, vm.GrblState.State);
        Assert.Equal(0, vm.GrblState.Substate);
        Assert.False(vm.GrblState.MPG);
        Assert.Null(vm.IsMPGActive);
        Assert.Equal(HomedState.Unknown, vm.HomedState);
        Assert.Equal(Signals.Off, vm.Signals.Value);
        Assert.True(double.IsNaN(vm.Position.X));
        Assert.Equal(0d, vm.MachinePosition.X);

        var enabled = new IsHomingEnabledConverter().Convert(
            new object?[] { vm.GrblState, false, false },
            typeof(bool),
            null,
            CultureInfo.InvariantCulture);
        Assert.False((bool)enabled!);
    }

    [Fact]
    public void Coordinator_detach_resets_live_controller_homing_state()
    {
        var connection = new ConnectionService(new EmptySerialPortDiscovery(), new InlineUiDispatcher());
        SetConnectionStream(connection, _comms);
        var coordinator = new MachineConnectionCoordinator(connection);
        var vm = new GrblViewModel { IsReady = true };
        vm.DataReceived("<Idle|MPos:1,2,3|H:1|Bf:15,128>");

        Assert.True(coordinator.AttachAfterConnect(vm));

        coordinator.Detach(vm);

        Assert.False(_comms.IsOpen);
        Assert.False(vm.IsReady);
        Assert.Equal(GrblStates.Unknown, vm.GrblState.State);
        Assert.Equal(HomedState.Unknown, vm.HomedState);
    }

    [Fact]
    public void Command_router_sends_homing_when_streaming_state_allows_mdi()
    {
        var vm = new GrblViewModel { StreamingState = StreamingState.Idle };
        var router = new GrblCommandRouter();
        router.Attach(vm);

        vm.ExecuteCommand(GrblConstants.CMD_HOMING);

        Assert.Equal([GrblConstants.CMD_HOMING], _comms.Commands);
    }

    [Fact]
    public void Command_router_blocks_homing_while_streaming_job()
    {
        var vm = new GrblViewModel { StreamingState = StreamingState.Send };
        var router = new GrblCommandRouter();
        router.Attach(vm);

        vm.ExecuteCommand(GrblConstants.CMD_HOMING);

        Assert.Empty(_comms.Commands);
    }

    [Fact]
    public void Command_router_allows_unlock_when_not_actively_sending()
    {
        var vm = new GrblViewModel { StreamingState = StreamingState.Paused };
        var router = new GrblCommandRouter();
        router.Attach(vm);

        vm.ExecuteCommand(GrblConstants.CMD_UNLOCK);

        Assert.Equal([GrblConstants.CMD_UNLOCK], _comms.Commands);
    }

    [Fact]
    public void Command_router_keeps_realtime_commands_outside_mdi_streaming_gate()
    {
        var vm = new GrblViewModel { StreamingState = StreamingState.Send };
        var router = new GrblCommandRouter();
        router.Attach(vm);

        vm.ExecuteCommand(GrblConstants.CMD_STATUS_REPORT_LEGACY);

        Assert.Empty(_comms.Commands);
        Assert.Equal([(byte)'?'], _comms.Bytes);
    }

    [Fact]
    public void Ready_message_preserves_homing_required_alarm()
    {
        var vm = new GrblViewModel();
        vm.SetGRBLState("Alarm", 11, true);

        MachineConnectionInitializer.ApplyReadyMessage(vm, gotStatus: true);

        Assert.Equal(HomedState.NotHomed, vm.HomedState);
        Assert.Equal("Homing cycle required, <Home> to continue", vm.Message);
    }

    [Fact]
    public void Ready_message_keeps_normal_connected_message_when_not_homing_required()
    {
        var vm = new GrblViewModel();
        vm.SetGRBLState("Idle", -1, true);

        MachineConnectionInitializer.ApplyReadyMessage(vm, gotStatus: false);

        Assert.Equal("Connected — no status report; DRO may stay blank until polling.", vm.Message);
    }

    [Fact]
    public void Work_envelope_requires_homed_state()
    {
        var oldHoming = GrblInfo.HomingEnabled;
        var oldX = GrblInfo.MaxTravel.X;
        var oldY = GrblInfo.MaxTravel.Y;
        var oldZ = GrblInfo.MaxTravel.Z;

        try
        {
            GrblInfo.HomingEnabled = true;
            GrblInfo.MaxTravel.X = 100d;
            GrblInfo.MaxTravel.Y = 50d;
            GrblInfo.MaxTravel.Z = 25d;

            var vm = new GrblViewModel();
            Assert.Empty(ViewerEnvelopeBuilder.WorkAreaBox(vm));

            vm.DataReceived("<Alarm:11|MPos:0,0,0|H:0|Bf:15,128>");
            Assert.Empty(ViewerEnvelopeBuilder.WorkAreaBox(vm));

            vm.DataReceived("<Idle|MPos:0,0,0|H:1|Bf:15,128>");
            Assert.NotEmpty(ViewerEnvelopeBuilder.WorkAreaBox(vm));
        }
        finally
        {
            GrblInfo.HomingEnabled = oldHoming;
            GrblInfo.MaxTravel.X = oldX;
            GrblInfo.MaxTravel.Y = oldY;
            GrblInfo.MaxTravel.Z = oldZ;
        }
    }

    static void SetConnectionStream(ConnectionService connection, StreamComms stream)
    {
        var field = typeof(ConnectionService).GetField("_stream", BindingFlags.Instance | BindingFlags.NonPublic);
        field!.SetValue(connection, stream);
    }

    sealed class EmptySerialPortDiscovery : ISerialPortDiscovery
    {
        public IReadOnlyList<SerialPortInfo> GetPorts() => [];
        public bool IsPortAvailable(string portName) => false;
    }

    sealed class InlineUiDispatcher : IUiDispatcher
    {
        public void Post(System.Action action) => action();
        public Task InvokeAsync(System.Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public Task<T> InvokeAsync<T>(Func<T> func) => Task.FromResult(func());
        public void PumpPending() { }
    }

    sealed class FakeStreamComms : StreamComms
    {
        public List<string> Commands { get; } = new();
        public List<byte> Bytes { get; } = new();
        public bool IsOpen { get; private set; } = true;
        public int OutCount => 0;
        public string Reply { get; private set; } = string.Empty;
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
        public void WriteCommand(string command) => Commands.Add(command);
        public string GetReply(string command) => Reply;
        public void AwaitAck() { }
        public void AwaitAck(string command) { }
        public void AwaitResponse(string command) { }
        public void AwaitResponse() { }
        public void PurgeQueue() { }
        public void Raise(string data) => DataReceived?.Invoke(data);
    }
}
