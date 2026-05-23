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

        queue.OnGrblStateChanged(idle);
        Assert.Equal(new[] { "$J=X+", "$J=Y+" }, _comms.Commands);

        queue.OnGrblStateChanged(idle);
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

        queue.OnGrblStateChanged(idle);
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

        queue.OnGrblStateChanged(idle);

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
    public void Repeated_idle_state_advances_without_sticking()
    {
        var queue = new UiJogCommandQueue();
        var idle = State(GrblStates.Idle);
        var jog = State(GrblStates.Jog);

        Assert.True(queue.TryEnqueueStep("$J=X+", idle));
        Assert.True(queue.TryEnqueueStep("$J=Y+", jog));

        Assert.Equal(new[] { "$J=X+" }, _comms.Commands);

        queue.OnGrblStateChanged(idle);
        Assert.Equal(new[] { "$J=X+", "$J=Y+" }, _comms.Commands);
        Assert.Equal(UiJogQueueState.Idle, queue.State);
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
