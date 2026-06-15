using CNC.Controls.Avalonia.Services;
using CNC.Core;

namespace CNC.Platform.Tests;

public sealed class JobStreamingServiceLineScrollTests : IDisposable
{
    readonly FakeStreamComms _comms = new();
    readonly GrblViewModel _model = new();
    readonly JobStreamingService _streaming = new();

    public JobStreamingServiceLineScrollTests()
    {
        Comms.com = _comms;
        GCodeFileService.Instance.LoadFromLines(
            new[] { "G0 X0", "G1 X1", "G1 X2", "M2" },
            @"C:\tmp\line-scroll-test.nc");
        _streaming.Attach(_model);
    }

    public void Dispose()
    {
        JobTimer.Stop();
        _model.IsJobRunning = false;
        GCodeFileService.Instance.Close();
        _streaming.Detach();
        Comms.com = null;
    }

    [Fact]
    public void Machine_line_number_scrolls_to_matching_program_block()
    {
        _streaming.CycleStart(0);
        _model.OnCommandResponseReceived?.Invoke("ok");

        var currentBlock = GCodeFileService.Instance.Data[1];

        _model.DataReceived($"<Run|MPos:0,0,0|WCO:0,0,0|Ln:{currentBlock.LineNum}|Bf:15,128>");

        Assert.Equal(1, _model.ScrollPosition);
        Assert.Equal("@", currentBlock.Sent);
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
