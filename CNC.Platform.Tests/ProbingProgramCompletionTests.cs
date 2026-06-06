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
        Assert.False(_comms.ContainsCommand("G0Z1"));

        _grbl.DataReceived("[PRB:0.000,0.000,-2.500:1]");
        await WaitForCommandAsync("G0Z1");

        await Task.Delay(50);
        Assert.False(_comms.ContainsCommand("G0X1"));

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

        Assert.False(_comms.ContainsByte(GrblConstants.CMD_STOP));

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

        Assert.False(_comms.ContainsByte(GrblConstants.CMD_STOP));
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
        Assert.True(_comms.ContainsByte(GrblConstants.CMD_STOP));
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

        Assert.Equal(["G21", "G0X1"], _comms.CommandSnapshot());
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
        var statusRequests = _comms.ByteCount;
        Assert.True(await WaitForAsync(() => _comms.ByteCount > statusRequests, 1000));
        _grbl.DataReceived("<Run|MPos:0.000,0.000,-1.000|Pn:P|Bf:15,128>");
        statusRequests = _comms.ByteCount;
        Assert.True(await WaitForAsync(() => _comms.ByteCount > statusRequests, 1000));
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
        Assert.False(_comms.AnyCommand(command => command.StartsWith("G65P5Q", StringComparison.OrdinalIgnoreCase)));
        _probing.Program.End(string.Empty);
    }

    [Fact]
    public async Task Tool_length_completion_updates_tlo_after_controller_report()
    {
        GrblInfoSnapshot.EnableSimpleProbeProtect();
        _probing.ReferenceToolOffset = false;
        _probing.IsSuccess = true;
        _probing.Positions.Add(new Position(0d, 0d, -2.5d));
        _probing.StartPosition.Zero();
        _grbl.MachinePosition.Zero();

        var complete = Task.Run(() => InvokeToolLengthCompleted(_probing));
        await WaitForCommandContainingAsync("G43.1Z-2.5", complete);

        Assert.False(_grbl.IsToolOffsetActive);
        _grbl.DataReceived("ok");

        Assert.False(_grbl.IsToolOffsetActive);
        Assert.False(_grbl.IsToolOffsetIndicatorVisible);

        await FinishToolLengthCompletionAsync(complete);

        Assert.True(_grbl.IsToolOffsetActive);
        Assert.True(_grbl.IsToolOffsetIndicatorVisible);
        Assert.True(_comms.ContainsCommand(GrblConstants.CMD_GETPARSERSTATE));
    }

    [Fact]
    public async Task Tool_length_completion_requests_tlo_refresh_when_parser_state_is_live()
    {
        GrblInfoSnapshot.EnableSimpleProbeProtect();
        _grbl.IsParserStateLive = true;
        _probing.ReferenceToolOffset = false;
        _probing.IsSuccess = true;
        _probing.Positions.Add(new Position(0d, 0d, -2.5d));
        _probing.StartPosition.Zero();
        _grbl.MachinePosition.Zero();

        var complete = Task.Run(() => InvokeToolLengthCompleted(_probing));
        await WaitForCommandContainingAsync("G43.1Z-2.5", complete);

        _grbl.DataReceived("ok");
        await FinishToolLengthCompletionAsync(complete);

        Assert.True(_grbl.IsToolOffsetActive);
        Assert.True(_comms.ContainsCommand(GrblConstants.CMD_GETPARSERSTATE));
    }

    [Fact]
    public async Task Tool_length_completion_does_not_apply_tlo_after_g431_error()
    {
        GrblInfoSnapshot.EnableSimpleProbeProtect();
        _probing.ReferenceToolOffset = false;
        _probing.IsSuccess = true;
        _probing.Positions.Add(new Position(0d, 0d, -2.5d));
        _probing.StartPosition.Zero();
        _grbl.MachinePosition.Zero();

        var complete = Task.Run(() => InvokeToolLengthCompleted(_probing));
        await WaitForCommandContainingAsync("G43.1Z-2.5", complete);

        _grbl.DataReceived("error:1");

        Assert.True(await WaitForAsync(() => _comms.ContainsCommand("G53G0Z0"), 1000));
        Assert.False(_grbl.IsToolOffsetActive);
        Assert.False(_grbl.IsToolOffsetIndicatorVisible);

        await FinishToolLengthCompletionAsync(complete, parserTloActive: false);
        Assert.Contains("Probing failed", _probing.Message);
    }

    async Task WaitForCommandAsync(string command)
    {
        var found = await WaitForAsync(() => _comms.ContainsCommand(command), 1000);
        Assert.True(found, $"Expected command '{command}' was not sent. Commands: {string.Join(", ", _comms.CommandSnapshot())}");
    }

    async Task WaitForCommandContainingAsync(string command)
    {
        var found = await WaitForAsync(() => _comms.AnyCommand(c => c.Contains(command, StringComparison.Ordinal)), 1000);
        Assert.True(found, $"Expected command containing '{command}' was not sent. Commands: {string.Join(", ", _comms.CommandSnapshot())}");
    }

    async Task WaitForCommandContainingAsync(string command, Task task)
    {
        var found = await WaitForAsync(() => _comms.AnyCommand(c => c.Contains(command, StringComparison.Ordinal)) || task.IsCompleted, 3000);
        Assert.True(found, $"Expected command containing '{command}' was not sent. Commands: {string.Join(", ", _comms.CommandSnapshot())}");
        Assert.False(task.IsFaulted, task.Exception?.ToString());
        Assert.False(task.IsCompletedSuccessfully, $"Completion finished before command '{command}' was sent. Commands: {string.Join(", ", _comms.CommandSnapshot())}");
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

    async Task FinishToolLengthCompletionAsync(Task complete, bool parserTloActive = true)
    {
        await WaitForCommandAsync("G53G0Z0");
        var statusRequests = _comms.ByteCount;
        _grbl.DataReceived("ok");
        Assert.True(await WaitForAsync(() => _comms.ByteCount > statusRequests, 1000));
        _grbl.DataReceived("<Idle|MPos:0.000,0.000,0.000|Bf:15,128>");
        await WaitForCommandAsync(GrblConstants.CMD_GETPARSERSTATE);
        _grbl.DataReceived(parserTloActive
            ? "[GC:G0 G54 G17 G21 G90 G94 G43.1 M5 M9 T0 F0 S0]"
            : "[GC:G0 G54 G17 G21 G90 G94 G49 M5 M9 T0 F0 S0]");
        _grbl.DataReceived("ok");
        Assert.True(await WaitForAsync(() => complete.IsCompleted, 1000));
        await complete;
    }

    static void InvokeToolLengthCompleted(ProbingViewModel probing)
    {
        var control = new ToolLengthControl { DataContext = probing };
        var method = typeof(ToolLengthControl).GetMethod("OnCompleted", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        method.Invoke(control, null);
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
        readonly object _sync = new();
        readonly List<byte> _bytes = new();
        readonly List<string> _commands = new();

        public int ByteCount
        {
            get
            {
                lock (_sync)
                    return _bytes.Count;
            }
        }

        public byte[] ByteSnapshot()
        {
            lock (_sync)
                return _bytes.ToArray();
        }

        public string[] CommandSnapshot()
        {
            lock (_sync)
                return _commands.ToArray();
        }

        public bool ContainsByte(byte data)
        {
            lock (_sync)
                return _bytes.Contains(data);
        }

        public bool ContainsCommand(string command)
        {
            lock (_sync)
                return _commands.Contains(command);
        }

        public bool AnyCommand(Func<string, bool> predicate)
        {
            lock (_sync)
                return _commands.Any(predicate);
        }

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
        public void WriteByte(byte data)
        {
            lock (_sync)
                _bytes.Add(data);
        }
        public void WriteBytes(byte[] bytes, int len) { }
        public void WriteString(string data) { }
        public void WriteCommand(string command)
        {
            lock (_sync)
                _commands.Add(command);
        }
        public string GetReply(string command) => Reply;
        public void AwaitAck() { }
        public void AwaitAck(string command) { }
        public void AwaitResponse(string command) { }
        public void AwaitResponse() { }
        public void PurgeQueue() { }
        public void Raise(string data) => DataReceived?.Invoke(data);
    }
}
