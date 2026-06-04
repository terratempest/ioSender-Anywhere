using CNC.Controls.Avalonia.Services;
using CNC.Core;

namespace CNC.Platform.Tests;

public sealed class UiJogCommandQueueTests : IDisposable
{
    readonly FakeStreamComms _comms = new();

    public UiJogCommandQueueTests()
    {
        Comms.com = _comms;
    }

    public void Dispose()
    {
        Comms.com = null;
    }

    [Fact]
    public void Rapid_step_clicks_are_written_in_fifo_order()
    {
        var queue = new UiJogCommandQueue();
        var idle = State(GrblStates.Idle);
        var jog = State(GrblStates.Jog);

        Assert.True(queue.TryEnqueueStep("$J=X+", idle));
        Assert.True(queue.TryEnqueueStep("$J=Y+", jog));
        Assert.True(queue.TryEnqueueStep("$J=X-", jog));

        Assert.Equal(new[] { "$J=X+" }, _comms.Commands);

        queue.OnGrblStateChanged(jog);
        queue.OnGrblStateChanged(idle);
        Assert.Equal(new[] { "$J=X+", "$J=Y+" }, _comms.Commands);

        queue.OnResponseReceived("ok", idle);
        Assert.Equal(new[] { "$J=X+", "$J=Y+" }, _comms.Commands);

        queue.OnRealtimeStatusProcessed(idle);
        Assert.Equal(new[] { "$J=X+", "$J=Y+", "$J=X-" }, _comms.Commands);
    }

    [Fact]
    public void Queued_step_does_not_dispatch_until_idle_machine_state()
    {
        var queue = new UiJogCommandQueue();
        var idle = State(GrblStates.Idle);
        var jog = State(GrblStates.Jog);

        Assert.True(queue.TryEnqueueStep("$J=X+", idle));
        Assert.True(queue.TryEnqueueStep("$J=Y+", jog));

        queue.OnResponseReceived("ok", jog);
        Assert.Equal(new[] { "$J=X+" }, _comms.Commands);

        queue.OnRealtimeStatusProcessed(idle);
        Assert.Equal(new[] { "$J=X+", "$J=Y+" }, _comms.Commands);
    }

    [Fact]
    public void Queue_cap_rejects_21st_pending_click()
    {
        var queue = new UiJogCommandQueue();
        var idle = State(GrblStates.Idle);
        var jog = State(GrblStates.Jog);

        Assert.True(queue.TryEnqueueStep("$J=active", idle));

        for (var i = 0; i < UiJogCommandQueue.MaxPendingClicks; i++)
            Assert.True(queue.TryEnqueueStep($"$J=pending{i}", jog));

        Assert.False(queue.TryEnqueueStep("$J=rejected", jog));
        Assert.Equal(UiJogCommandQueue.MaxPendingClicks, queue.PendingCount);

        queue.OnResponseReceived("ok", idle);
        Assert.Equal(new[] { "$J=active" }, _comms.Commands);

        queue.OnRealtimeStatusProcessed(idle);

        Assert.Equal("$J=pending0", _comms.Commands[1]);
        Assert.DoesNotContain("$J=rejected", _comms.Commands);
    }

    [Fact]
    public void Stop_clears_pending_jogs_and_writes_cancel_immediately()
    {
        var queue = new UiJogCommandQueue();
        var idle = State(GrblStates.Idle);
        var jog = State(GrblStates.Jog);

        Assert.True(queue.TryEnqueueStep("$J=X+", idle));
        Assert.True(queue.TryEnqueueStep("$J=Y+", jog));

        queue.Stop();

        Assert.Equal(0, queue.PendingCount);
        Assert.Equal(new[] { GrblConstants.CMD_JOG_CANCEL }, _comms.Bytes);

        queue.OnGrblStateChanged(idle);

        Assert.Equal(new[] { "$J=X+" }, _comms.Commands);
    }

    [Fact]
    public void Duplicate_stop_while_cancelling_is_ignored()
    {
        var queue = new UiJogCommandQueue();
        var idle = State(GrblStates.Idle);

        Assert.True(queue.TryStartContinuous("$J=X100", idle));

        queue.Stop();
        queue.Stop();

        Assert.Equal(new[] { GrblConstants.CMD_JOG_CANCEL }, _comms.Bytes);
        Assert.Equal(UiJogQueueState.Cancelling, queue.State);
    }

    [Fact]
    public void Raw_ok_with_current_idle_does_not_advance_without_fresh_status()
    {
        var queue = new UiJogCommandQueue();
        var idle = State(GrblStates.Idle);
        var jog = State(GrblStates.Jog);

        Assert.True(queue.TryEnqueueStep("$J=X+", idle));
        Assert.True(queue.TryEnqueueStep("$J=Y+", jog));

        Assert.Equal(new[] { "$J=X+" }, _comms.Commands);

        queue.OnResponseReceived("ok", idle);
        Assert.Equal(new[] { "$J=X+" }, _comms.Commands);
        Assert.Equal(UiJogQueueState.Idle, queue.State);
    }

    [Fact]
    public void Raw_ok_then_fresh_idle_status_advances_next_step()
    {
        var queue = new UiJogCommandQueue();
        var idle = State(GrblStates.Idle);
        var jog = State(GrblStates.Jog);

        Assert.True(queue.TryEnqueueStep("$J=X+", idle));
        Assert.True(queue.TryEnqueueStep("$J=Y+", jog));

        queue.OnResponseReceived("ok", idle);
        queue.OnRealtimeStatusProcessed(idle);

        Assert.Equal(new[] { "$J=X+", "$J=Y+" }, _comms.Commands);
    }

    [Fact]
    public void Fresh_idle_status_before_delayed_ok_does_not_advance_next_step()
    {
        var queue = new UiJogCommandQueue();
        var idle = State(GrblStates.Idle);
        var jog = State(GrblStates.Jog);

        Assert.True(queue.TryEnqueueStep("$J=X+", idle));
        Assert.True(queue.TryEnqueueStep("$J=Y+", jog));

        queue.OnRealtimeStatusProcessed(idle);
        Assert.Equal(new[] { "$J=X+" }, _comms.Commands);

        queue.OnResponseReceived("ok", idle);

        Assert.Equal(new[] { "$J=X+" }, _comms.Commands);
    }

    [Fact]
    public void Jog_to_idle_advances_even_if_ok_is_suppressed()
    {
        var queue = new UiJogCommandQueue();
        var idle = State(GrblStates.Idle);
        var jog = State(GrblStates.Jog);

        Assert.True(queue.TryEnqueueStep("$J=X+", idle));
        Assert.True(queue.TryEnqueueStep("$J=Y+", jog));

        queue.OnGrblStateChanged(jog);
        queue.OnGrblStateChanged(idle);

        Assert.Equal(new[] { "$J=X+", "$J=Y+" }, _comms.Commands);
    }

    [Fact]
    public void Continuous_stop_does_not_poison_later_step_queue()
    {
        var queue = new UiJogCommandQueue();
        var idle = State(GrblStates.Idle);
        var jog = State(GrblStates.Jog);

        Assert.True(queue.TryStartContinuous("$J=X100", idle));
        queue.Stop();
        queue.OnGrblStateChanged(idle);

        Assert.True(queue.TryEnqueueStep("$J=X+", idle));
        Assert.True(queue.TryEnqueueStep("$J=Y+", jog));

        queue.OnResponseReceived("ok", idle);
        queue.OnRealtimeStatusProcessed(idle);

        Assert.Equal(new[] { "$J=X100", "$J=X+", "$J=Y+" }, _comms.Commands);
        Assert.Equal(new[] { GrblConstants.CMD_JOG_CANCEL }, _comms.Bytes);
    }

    [Fact]
    public void Rapid_continuous_restart_can_be_stopped_by_center_button()
    {
        var queue = new UiJogCommandQueue();
        var idle = State(GrblStates.Idle);

        Assert.True(queue.TryStartContinuous("$J=X100", idle));
        queue.Stop();
        queue.OnGrblStateChanged(idle);

        Assert.True(queue.TryStartContinuous("$J=X100", idle));
        queue.Stop();

        Assert.Equal(new[] { "$J=X100", "$J=X100" }, _comms.Commands);
        Assert.Equal(new[] { GrblConstants.CMD_JOG_CANCEL, GrblConstants.CMD_JOG_CANCEL }, _comms.Bytes);
    }

    [Fact]
    public void Active_continuous_jog_blocks_step_queue_until_stopped_and_idle()
    {
        var queue = new UiJogCommandQueue();
        var idle = State(GrblStates.Idle);

        Assert.True(queue.TryStartContinuous("$J=X100", idle));
        Assert.False(queue.TryEnqueueStep("$J=Y+", idle));

        queue.Stop();
        queue.OnGrblStateChanged(idle);

        Assert.True(queue.TryEnqueueStep("$J=Y+", idle));
        Assert.Equal(new[] { "$J=X100", "$J=Y+" }, _comms.Commands);
    }

    [Fact]
    public void Active_continuous_jog_blocks_second_continuous_start()
    {
        var queue = new UiJogCommandQueue();
        var idle = State(GrblStates.Idle);

        Assert.True(queue.TryStartContinuous("$J=X100", idle));
        Assert.False(queue.TryStartContinuous("$J=Y100", idle));

        Assert.Equal(new[] { "$J=X100" }, _comms.Commands);
    }

    [Fact]
    public void Non_joggable_state_clears_active_continuous_jog()
    {
        var queue = new UiJogCommandQueue();
        var idle = State(GrblStates.Idle);

        Assert.True(queue.TryStartContinuous("$J=X100", idle));

        queue.OnGrblStateChanged(State(GrblStates.Alarm));

        Assert.Equal(UiJogQueueState.Idle, queue.State);
        Assert.True(queue.TryStartContinuous("$J=Y100", idle));
        Assert.Equal(new[] { "$J=X100", "$J=Y100" }, _comms.Commands);
    }

    [Fact]
    public void Error_after_active_step_clears_pending_queue()
    {
        var queue = new UiJogCommandQueue();
        var idle = State(GrblStates.Idle);
        var jog = State(GrblStates.Jog);

        Assert.True(queue.TryEnqueueStep("$J=X+", idle));
        Assert.True(queue.TryEnqueueStep("$J=Y+", jog));

        queue.OnResponseReceived("error:9", jog);

        Assert.Equal(0, queue.PendingCount);
        Assert.Equal(UiJogQueueState.Idle, queue.State);
        Assert.Equal(new[] { "$J=X+" }, _comms.Commands);
    }

    [Fact]
    public void Changed_fires_when_pending_count_decrements_during_dispatch()
    {
        var queue = new UiJogCommandQueue();
        var idle = State(GrblStates.Idle);
        var jog = State(GrblStates.Jog);

        Assert.True(queue.TryEnqueueStep("$J=X+", idle));
        Assert.True(queue.TryEnqueueStep("$J=Y+", jog));
        Assert.Equal(1, queue.PendingCount);

        var counts = new List<int>();
        queue.Changed += () => counts.Add(queue.PendingCount);

        queue.OnResponseReceived("ok", idle);
        Assert.Empty(counts);

        queue.OnRealtimeStatusProcessed(idle);

        Assert.Equal(0, queue.PendingCount);
        Assert.Contains(0, counts);
    }

    [Fact]
    public void Queue_status_count_excludes_active_step()
    {
        var queue = new UiJogCommandQueue();
        var idle = State(GrblStates.Idle);
        var jog = State(GrblStates.Jog);

        Assert.True(queue.TryEnqueueStep("$J=X+", idle));
        Assert.True(queue.TryEnqueueStep("$J=Y+", jog));
        Assert.Equal(1, queue.PendingCount);

        queue.OnResponseReceived("ok", idle);
        queue.OnRealtimeStatusProcessed(idle);

        Assert.Equal(new[] { "$J=X+", "$J=Y+" }, _comms.Commands);
        Assert.Equal(0, queue.PendingCount);

        queue.OnResponseReceived("ok", idle);
        queue.OnRealtimeStatusProcessed(idle);

        Assert.Equal(0, queue.PendingCount);
    }

    [Fact]
    public void Pending_count_steps_down_by_one_for_each_completed_step()
    {
        var queue = new UiJogCommandQueue();
        var idle = State(GrblStates.Idle);
        var jog = State(GrblStates.Jog);

        Assert.True(queue.TryEnqueueStep("$J=0", idle));
        Assert.True(queue.TryEnqueueStep("$J=1", jog));
        Assert.True(queue.TryEnqueueStep("$J=2", jog));

        var counts = new List<int>();
        queue.Changed += () => counts.Add(queue.PendingCount);

        queue.OnResponseReceived("ok", idle);
        queue.OnRealtimeStatusProcessed(idle);
        queue.OnResponseReceived("ok", idle);
        queue.OnRealtimeStatusProcessed(idle);
        queue.OnResponseReceived("ok", idle);
        queue.OnRealtimeStatusProcessed(idle);

        Assert.Contains(1, counts);
        Assert.Contains(0, counts);
    }

    [Fact]
    public void Same_status_cycle_cannot_drain_two_pending_steps()
    {
        var queue = new UiJogCommandQueue();
        var idle = State(GrblStates.Idle);
        var jog = State(GrblStates.Jog);

        Assert.True(queue.TryEnqueueStep("$J=0", idle));
        Assert.True(queue.TryEnqueueStep("$J=1", jog));
        Assert.True(queue.TryEnqueueStep("$J=2", jog));

        queue.OnGrblStateChanged(State(GrblStates.Jog));
        queue.OnGrblStateChanged(idle);
        queue.OnRealtimeStatusProcessed(idle);
        queue.OnResponseReceived("ok", idle);

        Assert.Equal(new[] { "$J=0", "$J=1" }, _comms.Commands);
        Assert.Equal(1, queue.PendingCount);
    }

    [Fact]
    public void Five_step_burst_dispatches_one_command_per_completion_cycle()
    {
        var queue = new UiJogCommandQueue();
        var idle = State(GrblStates.Idle);
        var jog = State(GrblStates.Jog);

        for (var i = 0; i < 5; i++)
            Assert.True(queue.TryEnqueueStep($"$J={i}", i == 0 ? idle : jog));

        Assert.Equal(new[] { "$J=0" }, _comms.Commands);
        Assert.Equal(4, queue.PendingCount);

        for (var i = 1; i < 5; i++)
        {
            queue.OnResponseReceived("ok", idle);
            queue.OnRealtimeStatusProcessed(idle);

            Assert.Equal(i + 1, _comms.Commands.Count);
            Assert.Equal(4 - i, queue.PendingCount);
        }

        queue.OnResponseReceived("ok", idle);
        queue.OnRealtimeStatusProcessed(idle);

        Assert.Equal(5, _comms.Commands.Count);
        Assert.Equal(0, queue.PendingCount);
    }

    [Fact]
    public void Alarm_or_closed_comms_clears_queue_and_returns_to_idle()
    {
        var queue = new UiJogCommandQueue();
        var idle = State(GrblStates.Idle);
        var jog = State(GrblStates.Jog);

        Assert.True(queue.TryEnqueueStep("$J=X+", idle));
        Assert.True(queue.TryEnqueueStep("$J=Y+", jog));

        queue.OnGrblStateChanged(State(GrblStates.Alarm));

        Assert.Equal(UiJogQueueState.Idle, queue.State);
        Assert.Equal(0, queue.PendingCount);

        _comms.IsOpen = false;
        Assert.False(queue.TryEnqueueStep("$J=Z+", idle));
        Assert.Equal(new[] { "$J=X+" }, _comms.Commands);
    }

    static GrblState State(GrblStates state) => new() { State = state };

    sealed class FakeStreamComms : StreamComms
    {
        public List<string> Commands { get; } = new();
        public List<byte> Bytes { get; } = new();
        public bool IsOpen { get; set; } = true;
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
