using System.Reflection;
using CNC.Controls.Avalonia.Services;
using CNC.Core;

namespace CNC.Platform.Tests;

public sealed class JobStreamingServiceStopTests : IDisposable
{
    readonly FakeStreamComms _comms = new();
    readonly GrblViewModel _model = new();
    readonly JobStreamingService _streaming = new();
    readonly GrblInfoSnapshot _grblInfo = GrblInfoSnapshot.Capture();

    public JobStreamingServiceStopTests()
    {
        Comms.com = _comms;
        GCodeFileService.Instance.LoadFromLines(new[] { "G0 X0", "M2" }, @"C:\tmp\stop-test.nc");
        _streaming.Attach(_model);
    }

    public void Dispose()
    {
        JobTimer.Stop();
        _model.IsJobRunning = false;
        GCodeFileService.Instance.Close();
        _streaming.Detach();
        _grblInfo.Restore();
        Comms.com = null;
    }

    [Fact]
    public void GrblHAL_stop_preserves_tool_length_reference_without_soft_reset()
    {
        GrblInfoSnapshot.SetGrblHAL(true);
        _model.DataReceived("<Idle|MPos:0,0,0|TLR:1|Bf:15,128>");
        Assert.True(_model.IsTloReferenceSet);

        _streaming.CycleStart(0);
        _model.SetGRBLState("Run", -1, true);
        _comms.Bytes.Clear();

        _streaming.StopJob(true);

        Assert.True(_model.IsTloReferenceSet);
        Assert.Contains(GrblConstants.CMD_STOP, _comms.Bytes);
        Assert.DoesNotContain(GrblConstants.CMD_RESET, _comms.Bytes);
    }

    [Fact]
    public void Streaming_ack_does_not_mark_program_row_completed()
    {
        _streaming.CycleStart(0);

        var sentRow = Assert.Single(GCodeFileService.Instance.Data.Where(row => row.Sent == "*").ToList());

        _model.OnCommandResponseReceived?.Invoke("ok");

        Assert.Equal("*", sentRow.Sent);
    }

    sealed record GrblInfoSnapshot(bool IsGrblHAL)
    {
        public static GrblInfoSnapshot Capture() => new(GrblInfo.IsGrblHAL);

        public static void SetGrblHAL(bool isGrblHAL) =>
            SetStaticProperty(nameof(GrblInfo.IsGrblHAL), isGrblHAL);

        public void Restore() => SetGrblHAL(IsGrblHAL);

        static void SetStaticProperty(string propertyName, object value)
        {
            var property = typeof(GrblInfo).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(property);
            property.SetValue(null, value);
        }
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
