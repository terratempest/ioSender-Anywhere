using CNC.Core;
using CNC.Controls.Avalonia.Services;
using CNC.Controls.Probing;
using CNC.GCode;
using System.Reflection;

namespace CNC.Platform.Tests;

public sealed class ProbingProgramCompletionTests : IDisposable
{
    readonly FakeStreamComms _comms = new();
    readonly GrblViewModel _grbl = new();
    readonly ProbingViewModel _probing = new();
    readonly GrblCommandRouter _router = new();
    readonly GrblInfoSnapshot _grblInfoSnapshot = GrblInfoSnapshot.Capture();

    public ProbingProgramCompletionTests()
    {
        Comms.com = _comms;
        _probing.Attach(_grbl);
        _router.Attach(_grbl);
        ProbingViewModel.ProbingCommand = "G38.3";
    }

    public void Dispose()
    {
        _grbl.IsJobRunning = false;
        _router.Detach();
        _probing.Detach();
        Comms.com = null;
        _grblInfoSnapshot.Restore();
    }

    [Fact]
    public async Task Probe_success_before_ok_completes_probe_step()
    {
        _probing.Program.Add("G38.3F100Z-10");

        var execute = Task.Run(() => _probing.Program.Execute(true));
        await WaitForCommandAsync("G38.3F100Z-10");

        _grbl.DataReceived("[PRB:0.000,0.000,-2.500:1]");

        Assert.True(await WaitForAsync(() => execute.IsCompleted, 3000));
        Assert.True(await execute);
        Assert.True(_probing.IsSuccess);
        Assert.Single(_probing.Positions);
        Assert.Equal(-2.5d, _probing.Positions[0].Z, 3);
        Assert.True(_grbl.IsJobRunning);
        _probing.Program.End(string.Empty);
        Assert.False(_grbl.IsJobRunning);
    }

    [Fact]
    public async Task Ok_before_probe_success_waits_for_probe_result()
    {
        _probing.Program.Add("G38.3F100Z-10");

        var execute = Task.Run(() => _probing.Program.Execute(true));
        await WaitForCommandAsync("G38.3F100Z-10");

        _grbl.OnCommandResponseReceived?.Invoke("ok");

        await Task.Delay(50);
        Assert.False(execute.IsCompleted);

        _grbl.DataReceived("[PRB:0.000,0.000,-2.750:1]");

        Assert.True(await WaitForAsync(() => execute.IsCompleted, 1000));
        Assert.True(await execute);
        Assert.Single(_probing.Positions);
        Assert.Equal(-2.75d, _probing.Positions[0].Z, 3);
        Assert.True(_grbl.IsJobRunning);
        _probing.Program.End(string.Empty);
        Assert.False(_grbl.IsJobRunning);
    }

    [Fact]
    public async Task Probe_success_without_ok_does_not_stall()
    {
        _probing.Program.Add("G38.3F100Z-10");

        var execute = Task.Run(() => _probing.Program.Execute(true));
        await WaitForCommandAsync("G38.3F100Z-10");

        _grbl.DataReceived("[PRB:0.000,0.000,-2.500:1]");

        Assert.True(await WaitForAsync(() => execute.IsCompleted, 3500));
        Assert.True(await execute);
        Assert.True(_probing.IsSuccess);
        Assert.Single(_probing.Positions);
        Assert.True(_grbl.IsJobRunning);
        _probing.Program.End(string.Empty);
        Assert.False(_grbl.IsJobRunning);
    }

    [Fact]
    public async Task Ok_before_probe_result_does_not_advance_later_command()
    {
        _probing.Program.Add("G38.3F100Z-10");
        _probing.Program.Add("G0Z1");
        _probing.Program.Add("G0X1");

        var execute = Task.Run(() => _probing.Program.Execute(true));
        await WaitForCommandAsync("G38.3F100Z-10");

        _grbl.OnCommandResponseReceived?.Invoke("ok");
        await Task.Delay(50);
        Assert.DoesNotContain("G0Z1", _comms.Commands);

        _grbl.DataReceived("[PRB:0.000,0.000,-2.500:1]");
        await WaitForCommandAsync("G0Z1");

        await Task.Delay(50);
        Assert.DoesNotContain("G0X1", _comms.Commands);

        _grbl.OnCommandResponseReceived?.Invoke("ok");
        await WaitForCommandAsync("G0X1");

        _grbl.OnCommandResponseReceived?.Invoke("ok");
        Assert.True(await WaitForAsync(() => execute.IsCompleted, 1000));
        Assert.True(await execute);
        _probing.Program.End(string.Empty);
    }

    [Fact]
    public async Task Latch_probe_keeps_only_final_slow_probe_position()
    {
        _probing.Program.AddProbingAction(AxisFlags.Z, true);

        var execute = Task.Run(() => _probing.Program.Execute(true));
        await WaitForCommandContainingAsync("G38.3F100Z-");

        _grbl.DataReceived("[PRB:0.000,0.000,-2.000:1]");
        _grbl.OnCommandResponseReceived?.Invoke("ok");
        await WaitForCommandContainingAsync("G0Z");

        _grbl.OnCommandResponseReceived?.Invoke("ok"); // retract ack
        await WaitForCommandContainingAsync("G38.3F25Z-");

        _grbl.DataReceived("[PRB:0.000,0.000,-2.100:1]");
        _grbl.OnCommandResponseReceived?.Invoke("ok");

        Assert.True(await WaitForAsync(() => execute.IsCompleted, 1000));
        Assert.True(await execute);
        Assert.Single(_probing.Positions);
        Assert.Equal(-2.1d, _probing.Positions[0].Z, 3);
        _probing.Program.End(string.Empty);
    }

    [Fact]
    public async Task Latch_probe_without_fast_probe_ack_still_retracts_and_slow_probes()
    {
        _probing.Program.AddProbingAction(AxisFlags.Z, true);

        var execute = Task.Run(() => _probing.Program.Execute(true));
        await WaitForCommandContainingAsync("G38.3F100Z-");

        _grbl.DataReceived("[PRB:0.000,0.000,-2.000:1]");

        await WaitForCommandContainingAsync("G0Z");
        await Task.Delay(300);
        _grbl.OnCommandResponseReceived?.Invoke("ok");
        await WaitForCommandContainingAsync("G38.3F25Z-");

        _grbl.DataReceived("[PRB:0.000,0.000,-2.100:1]");

        Assert.True(await WaitForAsync(() => execute.IsCompleted, 1000));
        Assert.True(await execute);
        Assert.Single(_probing.Positions);
        Assert.Equal(-2.1d, _probing.Positions[0].Z, 3);
        _probing.Program.End(string.Empty);
    }

    [Fact]
    public async Task Latch_retract_does_not_trigger_probe_protect_while_probe_remains_asserted()
    {
        GrblInfoSnapshot.EnableSimpleProbeProtect();
        _probing.Program.AddProbingAction(AxisFlags.Z, true);

        var execute = Task.Run(() => _probing.Program.Execute(true));
        await WaitForCommandContainingAsync("G38.3F100Z-");

        _grbl.DataReceived("[PRB:0.000,0.000,-2.000:1]");
        _grbl.OnCommandResponseReceived?.Invoke("ok");
        await WaitForCommandContainingAsync("G0Z");

        _grbl.DataReceived("<Run|MPos:0.000,0.000,-2.000|Pn:P|Bf:15,128>");
        await Task.Delay(50);

        Assert.DoesNotContain(GrblConstants.CMD_STOP, _comms.Bytes);

        _grbl.OnCommandResponseReceived?.Invoke("ok"); // retract ack
        await WaitForCommandContainingAsync("G38.3F25Z-");
        _grbl.DataReceived("[PRB:0.000,0.000,-2.100:1]");
        _grbl.OnCommandResponseReceived?.Invoke("ok");

        Assert.True(await WaitForAsync(() => execute.IsCompleted, 1000));
        Assert.True(await execute);
        _probing.Program.End(string.Empty);
    }

    [Fact]
    public async Task Idle_with_asserted_probe_during_active_probe_cleans_up_instead_of_stalling()
    {
        _probing.Program.Add("G38.3F100Z-10");

        var execute = Task.Run(() => _probing.Program.Execute(true));
        await WaitForCommandAsync("G38.3F100Z-10");

        _grbl.DataReceived("<Idle|MPos:0.000,0.000,-2.500|Pn:P|Bf:15,128>");

        Assert.True(await WaitForAsync(() => execute.IsCompleted, 1000));
        Assert.False(await execute);
        Assert.False(_probing.IsSuccess);
        Assert.False(_grbl.IsJobRunning);
        Assert.Contains(_probing.WorkflowTrace, entry => entry == "probe:idle-asserted");
    }

    [Fact]
    public async Task No_latch_tlo_probe_keeps_single_probe_position()
    {
        _probing.LatchDistance = 0d;
        _probing.Program.Add("G91F100");
        _probing.Program.AddProbingAction(AxisFlags.Z, true);

        var execute = Task.Run(() => _probing.Program.Execute(true));
        await WaitForCommandAsync("G91F100");

        _grbl.OnCommandResponseReceived?.Invoke("ok");
        await WaitForCommandAsync("G38.3F100Z-10");
        _grbl.DataReceived("[PRB:0.000,0.000,-2.500:1]");
        _grbl.OnCommandResponseReceived?.Invoke("ok");

        Assert.True(await WaitForAsync(() => execute.IsCompleted, 1000));
        Assert.True(await execute);
        Assert.Single(_probing.Positions);
        Assert.Equal(-2.5d, _probing.Positions[0].Z, 3);
        _probing.Program.End(string.Empty);
        Assert.False(_grbl.IsJobRunning);
    }

    [Fact]
    public async Task Failed_probe_result_releases_running_state()
    {
        _probing.Program.Add("G38.3F100Z-10");

        var execute = Task.Run(() => _probing.Program.Execute(true));
        await WaitForCommandAsync("G38.3F100Z-10");

        _grbl.DataReceived("[PRB:0.000,0.000,-2.500:0]");

        Assert.True(await WaitForAsync(() => execute.IsCompleted, 1000));
        Assert.False(await execute);
        Assert.False(_probing.IsSuccess);
        Assert.False(_grbl.IsJobRunning);
    }

    [Fact]
    public async Task Probe_bang_response_releases_running_state()
    {
        _probing.Program.Add("G38.3F100Z-10");

        var execute = Task.Run(() => _probing.Program.Execute(true));
        await WaitForCommandAsync("G38.3F100Z-10");

        _grbl.OnCommandResponseReceived?.Invoke("probe!");

        Assert.True(await WaitForAsync(() => execute.IsCompleted, 1000));
        Assert.False(await execute);
        Assert.False(_probing.IsSuccess);
        Assert.False(_grbl.IsJobRunning);
    }

    [Fact]
    public async Task Alarm_releases_running_state()
    {
        _probing.Program.Add("G38.3F100Z-10");

        var execute = Task.Run(() => _probing.Program.Execute(true));
        await WaitForCommandAsync("G38.3F100Z-10");

        _grbl.DataReceived("ALARM:1");

        Assert.True(await WaitForAsync(() => execute.IsCompleted, 1000));
        Assert.False(await execute);
        Assert.False(_probing.IsSuccess);
        Assert.False(_grbl.IsJobRunning);
    }

    [Fact]
    public async Task Cancel_releases_running_state()
    {
        _probing.Program.Add("G38.3F100Z-10");

        var execute = Task.Run(() => _probing.Program.Execute(true));
        await WaitForCommandAsync("G38.3F100Z-10");

        _probing.Program.Cancel();

        Assert.True(await WaitForAsync(() => execute.IsCompleted, 1000));
        Assert.False(await execute);
        Assert.False(_probing.IsSuccess);
        Assert.False(_grbl.IsJobRunning);
    }

    [Fact]
    public async Task Error_releases_running_state()
    {
        _probing.Program.Add("G38.3F100Z-10");

        var execute = Task.Run(() => _probing.Program.Execute(true));
        await WaitForCommandAsync("G38.3F100Z-10");

        _grbl.OnCommandResponseReceived?.Invoke("error:5");

        Assert.True(await WaitForAsync(() => execute.IsCompleted, 1000));
        Assert.False(await execute);
        Assert.False(_probing.IsSuccess);
        Assert.False(_grbl.IsJobRunning);
    }

    [Fact]
    public async Task Probe_protect_does_not_stop_active_probe_move()
    {
        GrblInfoSnapshot.EnableSimpleProbeProtect();
        _probing.Program.Add("G38.3F100Z-10");

        var execute = Task.Run(() => _probing.Program.Execute(true));
        await WaitForCommandAsync("G38.3F100Z-10");

        _grbl.DataReceived("<Run|MPos:0.000,0.000,0.000|Pn:P|Bf:15,128>");
        await Task.Delay(50);

        Assert.DoesNotContain(GrblConstants.CMD_STOP, _comms.Bytes);
        Assert.False(execute.IsCompleted);

        _grbl.DataReceived("[PRB:0.000,0.000,-2.500:1]");
        _grbl.OnCommandResponseReceived?.Invoke("ok");

        Assert.True(await WaitForAsync(() => execute.IsCompleted, 1000));
        Assert.True(await execute);
        _probing.Program.End(string.Empty);
    }

    [Fact]
    public async Task Probe_protect_stops_non_probe_move()
    {
        GrblInfoSnapshot.EnableSimpleProbeProtect();
        _probing.Program.Add("G0Z-10");

        var execute = Task.Run(() => _probing.Program.Execute(true));
        await WaitForCommandAsync("G0Z-10");

        _grbl.DataReceived("<Run|MPos:0.000,0.000,0.000|Pn:P|Bf:15,128>");

        Assert.True(await WaitForAsync(() => execute.IsCompleted, 1000));
        Assert.False(await execute);
        Assert.Contains(GrblConstants.CMD_STOP, _comms.Bytes);
        Assert.False(_grbl.IsJobRunning);
    }

    [Fact]
    public async Task Probing_commands_bypass_stale_mdi_router_queue()
    {
        _grbl.ExecuteCommand("G0X99");
        await WaitForCommandAsync("G0X99");

        _probing.Program.Add("G38.3F100Z-10");
        var execute = Task.Run(() => _probing.Program.Execute(true));

        await WaitForCommandAsync("G38.3F100Z-10");
        _grbl.DataReceived("[PRB:0.000,0.000,-2.500:1]");
        _grbl.OnCommandResponseReceived?.Invoke("ok");

        Assert.True(await WaitForAsync(() => execute.IsCompleted, 1000));
        Assert.True(await execute);
        Assert.Contains(_probing.WorkflowTrace, entry => entry == "command:G38.3F100Z-10");
        _probing.Program.End(string.Empty);
    }

    [Fact]
    public async Task Internal_probing_command_does_not_block_later_mdi_command()
    {
        _probing.SendInternalCommand("G21");
        await WaitForCommandAsync("G21");

        _grbl.ExecuteCommand("G0X1");
        await WaitForCommandAsync("G0X1");

        Assert.Equal(["G21", "G0X1"], _comms.Commands);
    }

    [Fact]
    public async Task Probing_deactivation_keeps_status_polling_enabled()
    {
        _grbl.Poller.SetState(250);

        _probing.OnDeactivated();
        await WaitForCommandAsync("G90");

        Assert.True(_grbl.Poller.IsEnabled);
    }

    [Fact]
    public async Task Tlo_style_probe_then_return_retract_completes_with_idle_probe_asserted()
    {
        _probing.LatchDistance = 0d;
        _probing.StartPosition.Set(new Position(0d, 0d, 0d));
        _grbl.DataReceived("<Idle|MPos:0.000,0.000,0.000|Bf:15,128>");
        _probing.Program.Add("G91F100");
        _probing.Program.AddProbingAction(AxisFlags.Z, true);

        var probe = Task.Run(() => _probing.Program.Execute(true));
        await WaitForCommandAsync("G91F100");
        _grbl.OnCommandResponseReceived?.Invoke("ok");
        await WaitForCommandAsync("G38.3F100Z-10");

        _grbl.DataReceived("[PRB:0.000,0.000,-2.500:1]");
        _grbl.OnCommandResponseReceived?.Invoke("ok");
        _grbl.DataReceived("<Idle|MPos:0.000,0.000,-2.500|Pn:P|Bf:15,128>");

        Assert.True(await WaitForAsync(() => probe.IsCompleted, 1000));
        Assert.True(await probe);

        var retract = Task.Run(() => _probing.GotoMachinePosition(_probing.StartPosition, AxisFlags.Z));
        await WaitForCommandAsync("G53G0Z0");
        _grbl.DataReceived("ok");
        var statusRequests = _comms.Bytes.Count;
        Assert.True(await WaitForAsync(() => _comms.Bytes.Count > statusRequests, 1000));
        _grbl.DataReceived("<Run|MPos:0.000,0.000,-1.000|Pn:P|Bf:15,128>");
        statusRequests = _comms.Bytes.Count;
        Assert.True(await WaitForAsync(() => _comms.Bytes.Count > statusRequests, 1000));
        _grbl.DataReceived("<Idle|MPos:0.000,0.000,0.000|Bf:15,128>");

        Assert.True(await WaitForAsync(() => retract.IsCompleted, 1000));
        Assert.True(await retract);
        _probing.Program.End(string.Empty);
        Assert.False(_grbl.IsJobRunning);
        Assert.Contains(_probing.WorkflowTrace, entry => entry == "wait:GotoMachinePosition");
    }

    [Fact]
    public async Task Tool_length_probe_program_does_not_select_probe()
    {
        _probing.LatchDistance = 0d;
        _probing.Program.Add("G91F100");
        _probing.Program.AddProbingAction(AxisFlags.Z, true);

        var execute = Task.Run(() => _probing.Program.Execute(true));
        await WaitForCommandAsync("G91F100");

        _grbl.OnCommandResponseReceived?.Invoke("ok");
        await WaitForCommandAsync("G38.3F100Z-10");
        _grbl.DataReceived("[PRB:0.000,0.000,-2.500:1]");
        _grbl.OnCommandResponseReceived?.Invoke("ok");

        Assert.True(await WaitForAsync(() => execute.IsCompleted, 3000));
        Assert.DoesNotContain(_comms.Commands, command => command.StartsWith("G65P5Q", StringComparison.OrdinalIgnoreCase));
        _probing.Program.End(string.Empty);
    }

    async Task WaitForCommandAsync(string command)
    {
        var found = await WaitForAsync(() => _comms.Commands.Contains(command), 1000);
        Assert.True(found, $"Expected command '{command}' was not sent. Commands: {string.Join(", ", _comms.Commands)}");
    }

    async Task WaitForCommandContainingAsync(string command)
    {
        var found = await WaitForAsync(() => _comms.Commands.Any(c => c.Contains(command, StringComparison.Ordinal)), 1000);
        Assert.True(found, $"Expected command containing '{command}' was not sent. Commands: {string.Join(", ", _comms.Commands)}");
    }

    static async Task<bool> WaitForAsync(Func<bool> predicate, int timeoutMs)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        while (!cts.IsCancellationRequested)
        {
            if (predicate())
                return true;
            await Task.Delay(10, CancellationToken.None);
        }

        return predicate();
    }

    sealed record GrblInfoSnapshot(bool IsGrblHAL, int Build, bool HasSimpleProbeProtect)
    {
        public static GrblInfoSnapshot Capture() =>
            new(GrblInfo.IsGrblHAL, GrblInfo.Build, GrblInfo.HasSimpleProbeProtect);

        public static void EnableSimpleProbeProtect()
        {
            SetStaticProperty(nameof(GrblInfo.IsGrblHAL), true);
            SetStaticProperty(nameof(GrblInfo.Build), 20200924);
            SetStaticProperty(nameof(GrblInfo.HasSimpleProbeProtect), true);
        }

        public void Restore()
        {
            SetStaticProperty(nameof(GrblInfo.IsGrblHAL), IsGrblHAL);
            SetStaticProperty(nameof(GrblInfo.Build), Build);
            SetStaticProperty(nameof(GrblInfo.HasSimpleProbeProtect), HasSimpleProbeProtect);
        }

        static void SetStaticProperty(string propertyName, object value)
        {
            var property = typeof(GrblInfo).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(property);
            property.SetValue(null, value);
        }
    }

    sealed class FakeStreamComms : StreamComms
    {
        public List<byte> Bytes { get; } = new();
        public List<string> Commands { get; } = new();
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
