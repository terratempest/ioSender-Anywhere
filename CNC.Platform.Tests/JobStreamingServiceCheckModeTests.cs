using CNC.Controls.Avalonia.Services;
using CNC.Core;

namespace CNC.Platform.Tests;

public sealed class JobStreamingServiceCheckModeTests : IDisposable
{
    const string CheckActiveMessage = "Check mode active - cycle start will validate only.";
    const string CheckCompleteMessage = "Check complete - check mode remains active.";

    readonly FakeStreamComms _comms = new();
    readonly GrblViewModel _model = new();
    readonly JobStreamingService _streaming = new();
    readonly List<JobButtonState> _buttonStates = new();

    public JobStreamingServiceCheckModeTests()
    {
        Comms.com = _comms;
        _streaming.ButtonStateChanged += _buttonStates.Add;
        _streaming.Attach(_model);
        _streaming.Activate(true);
        GCodeFileService.Instance.LoadFromLines(new[] { "G0 X0", "M2" }, @"C:\tmp\check-mode-test.nc");
    }

    public void Dispose()
    {
        JobTimer.Stop();
        _model.IsJobRunning = false;
        _streaming.Activate(false);
        GCodeFileService.Instance.Close();
        _streaming.Detach();
        Comms.com = null;
    }

    [Fact]
    public void Check_state_without_active_job_is_presented_as_validation_mode()
    {
        _model.SetGRBLState("Check", -1, true);

        Assert.False(_model.IsJobRunning);
        Assert.Equal(CheckActiveMessage, _model.Message);
        Assert.Equal("Check Program", LastStateWith(s => s.CycleStartLabel).CycleStartLabel);
        Assert.True(LastStateWith(s => s.CycleStart).CycleStart);
        Assert.False(LastStateWith(s => s.Stop).Stop);
        Assert.False(LastStateWith(s => s.FeedHold).FeedHold);
    }

    [Fact]
    public void Check_mode_validation_completion_clears_running_state_and_keeps_check_mode_visible()
    {
        _model.SetGRBLState("Check", -1, true);

        _streaming.CycleStart(0);
        Assert.True(_model.IsJobRunning);
        Assert.Equal("Checking", LastStateWith(s => s.CycleStartLabel).CycleStartLabel);

        for (var i = 0; i < 10 && JobTimer.IsRunning; i++)
            _model.OnCommandResponseReceived?.Invoke("ok");

        Assert.False(JobTimer.IsRunning);
        Assert.False(_model.IsJobRunning);
        Assert.Equal(CheckCompleteMessage, _model.Message);
        Assert.Equal("Check Program", LastStateWith(s => s.CycleStartLabel).CycleStartLabel);
        Assert.True(LastStateWith(s => s.CycleStart).CycleStart);
        Assert.False(LastStateWith(s => s.Stop).Stop);
    }

    JobButtonState LastStateWith<T>(Func<JobButtonState, T?> selector)
    {
        return _buttonStates.Last(state => selector(state) is not null);
    }

    sealed class FakeStreamComms : StreamComms
    {
        public List<string> Commands { get; } = new();
        public List<string> WrittenStrings { get; } = new();
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
        public void WriteString(string data) => WrittenStrings.Add(data);
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
