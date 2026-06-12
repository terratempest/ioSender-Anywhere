using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using CNC.App;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Avalonia.Services;

/// <summary>File streaming state machine for the Avalonia job controls.</summary>
public sealed class JobStreamingService
{
    private const string CheckModeActiveMessage = "Check mode active - cycle start will validate only.";
    private const string CheckModeRunningMessage = "Checking - validating only, no machine motion.";
    private const string CheckModeCompleteMessage = "Check complete - check mode remains active.";

    private enum StreamingHandler
    {
        Idle = 0,
        SendFile,
        FeedHold,
        ToolChange,
        AwaitAction,
        AwaitIdle,
        Previous,
        Max
    }

    private struct StreamingHandlerFn
    {
        public StreamingHandler Handler;
        public bool Count;
        public Func<StreamingState, bool, bool> Call;
    }

    private struct JobData
    {
        public int CurrBlock, LastExecuting, PendingLine, PgmEndLine, ToolChangeLine, AckPending, SerialUsed;
        public bool Started, Transferred, Complete, IsSdFile, IsChecking, HasError, Stopped, ToolChanged;
        public GCodeBlock? CurrentRow, NextRow;
    }

    private readonly Queue<string> _injectCommands = new();

    private int _serialSize = 128;
    private bool _initOk, _isActive, _useBuffering, _feedHoldEnable, _feedHoldRequested, _stopRequested;
    private volatile StreamingState _streamingState = StreamingState.NoFile;
    private GrblState _grblState;
    private GrblViewModel? _model;
    private readonly BaseConfig? _appBase;
    private JobData _job;
    private int _missed;

    private readonly StreamingHandlerFn[] _streamingHandlers = new StreamingHandlerFn[(int)StreamingHandler.Max];
    private StreamingHandlerFn _streamingHandler;

    public event Action<JobButtonState>? ButtonStateChanged;

    public JobStreamingService(BaseConfig? appBase = null)
    {
        _appBase = appBase;
        _grblState.State = GrblStates.Unknown;
        _grblState.Substate = 0;
        _grblState.MPG = false;
        _job.PgmEndLine = -1;

        _streamingHandlers[(int)StreamingHandler.Idle].Call = StreamingIdle;
        _streamingHandlers[(int)StreamingHandler.Idle].Count = false;
        _streamingHandlers[(int)StreamingHandler.SendFile].Call = StreamingSendFile;
        _streamingHandlers[(int)StreamingHandler.SendFile].Count = true;
        _streamingHandlers[(int)StreamingHandler.ToolChange].Call = StreamingToolChange;
        _streamingHandlers[(int)StreamingHandler.ToolChange].Count = false;
        _streamingHandlers[(int)StreamingHandler.FeedHold].Call = StreamingFeedHold;
        _streamingHandlers[(int)StreamingHandler.FeedHold].Count = true;
        _streamingHandlers[(int)StreamingHandler.AwaitAction].Call = StreamingAwaitAction;
        _streamingHandlers[(int)StreamingHandler.AwaitAction].Count = true;
        _streamingHandlers[(int)StreamingHandler.AwaitIdle].Call = StreamingAwaitIdle;
        _streamingHandlers[(int)StreamingHandler.AwaitIdle].Count = false;

        _streamingHandler = _streamingHandlers[(int)StreamingHandler.Previous] =
            _streamingHandlers[(int)StreamingHandler.Idle];

        for (var i = 0; i < _streamingHandlers.Length; i++)
            _streamingHandlers[i].Handler = (StreamingHandler)i;
    }

    private static GCodeFileService GCode => GCodeFileService.Instance;

    private BaseConfig? AppBase => _appBase;

    public void Attach(GrblViewModel model)
    {
        if (_model == model)
            return;

        Detach();

        _model = model;
        GCode.Model = model;
        model.PropertyChanged += OnModelPropertyChanged;
        model.OnRealtimeStatusProcessed += OnRealtimeStatusProcessed;
        model.OnCommandResponseReceived += OnResponseReceived;
        model.OnCycleStart += OnModelCycleStart;
        model.OnStop += OnModelStop;

        ApplyParserOptions();
        _streamingHandler.Call(GCode.IsLoaded ? StreamingState.Idle : StreamingState.NoFile, false);
    }

    public void Detach()
    {
        if (_model == null)
            return;

        _model.PropertyChanged -= OnModelPropertyChanged;
        _model.OnRealtimeStatusProcessed = (Action<string>)Delegate.Remove(_model.OnRealtimeStatusProcessed, OnRealtimeStatusProcessed)!;
        _model.OnCommandResponseReceived = (Action<string>)Delegate.Remove(_model.OnCommandResponseReceived, OnResponseReceived)!;
        _model.OnCycleStart -= OnModelCycleStart;
        _model.OnStop -= OnModelStop;
        _model = null;
    }

    public bool Activate(bool activate)
    {
        if (activate && !_initOk)
        {
            _initOk = true;
            var maxBuf = AppBase?.MaxBufferSize ?? 128;
            _serialSize = Math.Min(maxBuf, (int)(GrblInfo.SerialBufferSize * 0.9f));
            GCode.Parser.Dialect = GrblInfo.IsGrblHAL ? Dialect.GrblHAL : Dialect.Grbl;
            GCode.Parser.ExpressionsSupported = GrblInfo.ExpressionsSupported;

            if (GrblInfo.HasRTC)
                WriteCommand("$RTC=" + DateTime.Now.ToLocalTime().ToString("s"));
        }

        EnablePolling(activate);
        _isActive = activate;
        return _isActive;
    }

    public void EnablePolling(bool enable)
    {
        if (_model == null)
            return;

        if (enable)
            _model.Poller.SetState(AppBase?.PollInterval ?? 250);
        else if (_model.Poller.IsEnabled && _model.GrblState.State != GrblStates.Home)
            _model.Poller.SetState(0);
    }

    public void CycleStart(int fromBlock)
    {
        if (_model == null)
            return;

        _feedHoldRequested = false;
        _stopRequested = false;

        if (_grblState.State == GrblStates.Hold ||
            (_grblState.State == GrblStates.Run && _grblState.Substate == 1) ||
            (_grblState.State == GrblStates.Door && (_grblState.Substate == 0 || _grblState.Substate == 5)))
        {
            WriteRtByte(GrblConstants.CMD_CYCLE_START);
        }
        else if (_grblState.State == GrblStates.Idle && _model.SDRewind)
        {
            _streamingHandler.Call(StreamingState.Start, false);
            WriteRtByte(GrblConstants.CMD_CYCLE_START);
        }
        else if (_grblState.State == GrblStates.Tool)
        {
            _model.Message = string.Empty;
            _job.ToolChanged = false;
            _job.ToolChangeLine = -1;
            WriteRtByte(GrblConstants.CMD_CYCLE_START);
        }
        else if (JobTimer.IsRunning)
        {
            JobTimer.Pause = false;
            _streamingHandler.Call(StreamingState.Send, false);
        }
        else if (GCode.IsLoaded)
        {
            _model.Message = _model.RunTime = string.Empty;
            if (_job.ToolChanged)
            {
                _job.ToolChanged = false;
                if (_job.ToolChangeLine != -1)
                {
                    _job.ToolChangeLine = -1;
                    SendNextLine();
                }
            }
            else if (_model.IsSDCardJob)
            {
                WriteCommand(GrblConstants.CMD_SDCARD_RUN + _model.FileName.Substring(7));
            }
            else
            {
                _job.ToolChangeLine = -1;
                _model.BlockExecuting = fromBlock;
                _job.CurrBlock = _job.AckPending = _job.PendingLine = fromBlock;
                _model.ExecutionProgress.Reset();
                _job.SerialUsed = _missed = 0;
                _job.Started = _job.Transferred = _job.HasError = _job.ToolChanged = false;
                _job.NextRow = GCode.Data[fromBlock];
                Comms.com?.PurgeQueue();
                JobTimer.Start();
                _streamingHandler.Call(StreamingState.Send, false);
                if ((_job.IsChecking = _model.GrblState.State == GrblStates.Check))
                {
                    _model.Message = CheckModeRunningMessage;
                    SetButtons(cycleStart: false, feedHold: false, stop: true, rewind: false, cycleStartLabel: "Checking");
                }

                bool? res = null;
                var token = new CancellationToken();
                new Thread(() =>
                {
                    res = WaitFor.SingleEvent<string>(
                        token,
                        null,
                        a => _model!.OnGrblReset += a,
                        a => _model!.OnGrblReset = (Action<string>)Delegate.Remove(_model!.OnGrblReset, a)!,
                        250);
                }).Start();

                while (res == null)
                    EventUtils.DoEvents();

                SendNextLine();
            }
        }
    }

    public void FeedHold()
    {
        if (Comms.com is not { IsOpen: true })
            return;

        _feedHoldRequested = true;
        _feedHoldEnable = false;
        SetButtons(cycleStart: false, feedHold: false, stop: true, rewind: false);
        WriteRtByte(GrblConstants.CMD_FEED_HOLD);
    }

    public void StopJob(bool force)
    {
        PrepareStop();
        _streamingHandler.Call(StreamingState.Stop, true);
    }

    private void PrepareStop()
    {
        _stopRequested = true;
        _feedHoldRequested = false;
        _feedHoldEnable = false;
        _injectCommands.Clear();

        JobTimer.Stop();
        _job.Started = false;
        _job.Transferred = false;
        _job.Complete = false;
        _job.IsChecking = false;
        _job.SerialUsed = 0;
        _job.AckPending = 0;
        _job.CurrentRow = null;
        _job.NextRow = null;

        if (_model != null)
        {
            _model.IsJobRunning = false;
            _model.BlockExecuting = 0;
        }

        if (Comms.com is { IsOpen: true } comms)
            comms.PurgeQueue();

        if (GCode.IsLoaded && _model != null)
        {
            RewindFile();
            _stopRequested = true;
        }

        SetButtons(
            cycleStart: GCode.IsLoaded,
            feedHold: false,
            stop: false,
            rewind: false,
            cycleStartLabel: "Cycle Start",
            stopLabel: "Stop");
    }

    public void RewindFile()
    {
        _job.Complete = false;
        if (!GCode.IsLoaded || _model == null)
            return;

        SetButtons(cycleStart: false);
        ClearBlockStatus();
        _model.ExecutionProgress.Reset();
        _model.ScrollPosition = 0;
        _job.ToolChangeLine = -1;
        _job.CurrBlock = _job.LastExecuting = _job.PendingLine = _job.AckPending = _model.BlockExecuting = 0;
        _job.PgmEndLine = GCode.Blocks - 1;
        _stopRequested = false;
        SetButtons(cycleStart: true);
    }

    public void RewindAndRefresh() { RewindFile(); _streamingHandler.Call(_streamingState, true); }

    private void OnModelStop(object? sender, EventArgs e)
    {
        JobTimer.Stop();
        _job.Stopped = true;
        _streamingHandler.Call(StreamingState.Stop, true);
    }

    private void OnModelCycleStart(object? sender, EventArgs e)
    {
        if (_isActive && JobPending)
            CycleStart(0);
    }

    private void OnRealtimeStatusProcessed(string response)
    {
        if (_model != null && JobTimer.IsRunning && !JobTimer.IsPaused)
            _model.RunTime = JobTimer.RunTime;
    }

    private bool JobPending => GCode.IsLoaded && !JobTimer.IsRunning;

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_model == null || sender is not GrblViewModel vm)
            return;

        switch (e.PropertyName)
        {
            case nameof(GrblViewModel.LineNumber):
                if (_job.CurrBlock > 0)
                {
                    var found = 0;
                    var block = _job.CurrBlock;
                    var lineNum = vm.LineNumber;
                    do
                    {
                        if (GCode.Data[block].LineNum == lineNum)
                        {
                            found = block - 1;
                            GCode.Data[block].Sent = "@";
                            break;
                        }
                    } while (--block > _job.LastExecuting);

                    while (_job.LastExecuting < found)
                        GCode.Data[++_job.LastExecuting].Sent = "ok";
                }
                break;

            case nameof(GrblViewModel.GrblState):
                GrblStateChanged(vm.GrblState);
                break;

            case nameof(GrblViewModel.StartFromBlockNum):
                CycleStart(vm.StartFromBlockNum);
                break;

            case nameof(GrblViewModel.IsMPGActive):
                _grblState.MPG = vm.IsMPGActive == true;
                vm.Poller.SetState(_grblState.MPG ? 0 : (AppBase?.PollInterval ?? 250));
                _streamingHandler.Call(_grblState.MPG ? StreamingState.Disabled : StreamingState.Idle, false);
                break;

            case nameof(GrblViewModel.ProgramEnd):
                if (!GCode.IsLoaded)
                    _streamingHandler.Call(vm.IsSDCardJob ? StreamingState.JobFinished : StreamingState.NoFile, vm.IsSDCardJob);
                else if (JobTimer.IsRunning && !_job.Complete)
                    _streamingHandler.Call(StreamingState.JobFinished, true);
                if (!vm.IsParserStateLive)
                    SendCommand(GrblConstants.CMD_GETPARSERSTATE);
                break;

            case nameof(GrblViewModel.FileName):
                _job.IsSdFile = false;
                if (string.IsNullOrEmpty(vm.FileName))
                    _job.NextRow = null;
                else
                {
                    _job.ToolChangeLine = -1;
                    _job.ToolChanged = false;
                    _job.CurrBlock = _job.PendingLine = _job.AckPending = vm.BlockExecuting = 0;
                    vm.ExecutionProgress.Reset();
                    _job.PgmEndLine = GCode.Blocks - 1;
                    if (vm.IsPhysicalFileLoaded)
                    {
                        ShowJobLoadWarnings(vm);
                        _streamingHandler.Call(GCode.IsLoaded ? StreamingState.Idle : StreamingState.NoFile, false);
                    }
                }
                break;

            case nameof(GrblViewModel.FeedHoldDisabled):
                SetButtons(feedHold: !vm.FeedHoldDisabled && _feedHoldEnable);
                break;

            case nameof(GrblViewModel.GrblReset):
                JobTimer.Stop();
                _streamingHandler.Call(StreamingState.Stop, true);
                break;
        }
    }

    private static void ShowJobLoadWarnings(GrblViewModel vm)
    {
        var parser = GCode.Parser;
        if (parser.ToolChanges > 0)
        {
            if (!GrblSettings.HasSetting(grblHALSetting.ToolChangeMode))
            {
                MessageDialogs.ShowWarning(
                    $"This job has {parser.ToolChanges} tool change(s) using M6. Only a few Grbl ports support that.",
                    "ioSender");
            }
            else if (GrblSettings.GetInteger(grblHALSetting.ToolChangeMode) > 0 && !vm.IsTloReferenceSet)
            {
                MessageDialogs.ShowWarning(
                    $"This job has {parser.ToolChanges} tool change(s). Establish tool length reference before starting.",
                    "ioSender");
            }
        }

        if (parser.HasGoPredefinedPosition && vm.IsGrblHAL && vm.HomedState != HomedState.Homed)
        {
            MessageDialogs.ShowWarning(
                "This job contains G28/G30 moves and the machine is not homed.",
                "ioSender");
        }
    }

    private void ApplyParserOptions()
    {
        var cfg = AppBase;
        if (cfg == null)
            return;

        GCodeParser.IgnoreM6 = cfg.IgnoreM6;
        GCodeParser.IgnoreM7 = cfg.IgnoreM7;
        GCodeParser.IgnoreM8 = cfg.IgnoreM8;
        GCodeParser.IgnoreG61G64 = cfg.IgnoreG61G64;
        _useBuffering = cfg.UseBuffering;
    }

    private void GrblStateChanged(GrblState newstate)
    {
        if (_model == null)
            return;

        if (_grblState.State == GrblStates.Jog)
            _model.IsJobRunning = false;

        if (!_isActive)
        {
            _grblState = newstate;
            return;
        }

        if (_feedHoldRequested)
        {
            if (newstate.State is GrblStates.Hold or GrblStates.Idle or GrblStates.Alarm)
                _feedHoldRequested = false;
            else if (!_stopRequested)
                WriteRtByte(GrblConstants.CMD_FEED_HOLD);
        }

        var leavingToolChange = _grblState.State == GrblStates.Tool &&
            newstate.State is GrblStates.Idle or GrblStates.Run or GrblStates.Hold;

        switch (newstate.State)
        {
            case GrblStates.Idle:
                _stopRequested = false;
                _streamingHandler.Call(StreamingState.Idle, true);
                break;

            case GrblStates.Check:
                _stopRequested = false;
                if (JobTimer.IsRunning || _job.IsChecking)
                {
                    _job.IsChecking = true;
                    _model.Message = CheckModeRunningMessage;
                    if (_model.StreamingState != StreamingState.Error)
                        _streamingHandler.Call(StreamingState.Send, false);
                    SetButtons(cycleStart: false, feedHold: false, stop: true, rewind: false, cycleStartLabel: "Checking", stopLabel: "Stop");
                }
                else
                    ApplyCheckModeIdleState(CheckModeActiveMessage);
                break;

            case GrblStates.Jog:
                if (_stopRequested)
                {
                    _model.IsJobRunning = false;
                    SetButtons(cycleStart: false, feedHold: false, stop: true, rewind: false, cycleStartLabel: "Cycle Start", stopLabel: "Stop");
                    WriteRtByte(GrblConstants.CMD_RESET);
                    break;
                }
                _model.IsJobRunning = !_model.IsToolChanging;
                SetButtons(cycleStart: false, feedHold: false, stop: true, rewind: false, cycleStartLabel: "Cycle Start", stopLabel: "Stop");
                break;

            case GrblStates.Run:
                if (_stopRequested)
                {
                    _model.IsJobRunning = false;
                    SetButtons(cycleStart: false, feedHold: false, stop: true, rewind: false, cycleStartLabel: "Cycle Start", stopLabel: "Stop");
                    WriteRtByte(GrblConstants.CMD_RESET);
                    break;
                }
                if (JobTimer.IsPaused)
                    JobTimer.Pause = false;
                if (_model.StreamingState != StreamingState.Error)
                    _streamingHandler.Call(StreamingState.Send, false);
                if (newstate.Substate == 1)
                    SetButtons(cycleStart: !_grblState.MPG, feedHold: false);
                else if (_grblState.Substate == 1)
                    SetButtons(cycleStart: false, feedHold: !_grblState.MPG && !(_model.FeedHoldDisabled));
                if (!GrblInfo.IsGrblHAL)
                    SetButtons(stopLabel: "Pause");
                break;

            case GrblStates.Tool:
                if (_grblState.State != GrblStates.Jog)
                {
                    if (JobTimer.IsRunning && _job.PendingLine > 0 && !_model.IsSDCardJob)
                    {
                        _job.ToolChangeLine = _job.PendingLine - 1;
                        GCode.Data[_job.ToolChangeLine].Sent = "pending";
                    }
                    _streamingHandler.Call(StreamingState.ToolChange, true);
                    if (!_grblState.MPG)
                        Comms.com?.WriteByte(GrblConstants.CMD_TOOL_ACK);
                }
                break;

            case GrblStates.Hold:
                _feedHoldRequested = false;
                _streamingHandler.Call(StreamingState.FeedHold, false);
                break;

            case GrblStates.Home:
                EnablePolling(true);
                break;

            case GrblStates.Door:
                if (newstate.Substate > 0)
                {
                    if (_streamingState == StreamingState.Send)
                        _streamingHandler.Call(StreamingState.FeedHold, false);
                    else
                        SetButtons(cycleStart: false);
                }
                else
                    SetButtons(cycleStart: true);
                break;

            case GrblStates.Alarm:
                _stopRequested = false;
                _grblState.State = newstate.State;
                _grblState.Substate = newstate.Substate;
                _streamingHandler.Call(StreamingState.Stop, false);
                break;
        }

        if (_feedHoldRequested)
            SetButtons(cycleStart: false, feedHold: false, stop: true, rewind: false);

        if (leavingToolChange)
            ControllerWorkParametersSync.Refresh(_model);

        _grblState.State = newstate.State;
        _grblState.Substate = newstate.Substate;
        _grblState.MPG = newstate.MPG;
    }

    private void OnResponseReceived(string response)
    {
        if (_streamingHandler.Count)
        {
            var wasChecking = _job.IsChecking;

            if (_job.AckPending > 0)
                _job.AckPending--;

            if (!_job.IsSdFile && (_job.IsChecking || GCode.Data[_job.PendingLine].Sent == "*"))
                _job.SerialUsed = Math.Max(0, _job.SerialUsed - GCode.Data[_job.PendingLine].Length);

            var isError = response.StartsWith("error", StringComparison.Ordinal);

            if (!(_job.IsSdFile || _job.IsChecking))
            {
                if (!_job.HasError)
                {
                    if (response != "ok")
                        GCode.Data[_job.PendingLine].Sent = response;
                    if (_job.PendingLine > 5)
                        _model!.ScrollPosition = _job.PendingLine - 5;
                }

                if (_streamingHandler.Call == StreamingAwaitAction)
                    _streamingHandler.Count = false;
            }

            if (isError)
            {
                _streamingHandler.Call(StreamingState.Error, true);
                if (_job.IsChecking && !_job.HasError)
                {
                    if (_job.PendingLine > 5)
                        _model!.ScrollPosition = _job.PendingLine - 5;
                    GCode.Data[_job.PendingLine].Sent = response;
                }
                _job.HasError = _model!.IsGrblHAL;
            }
            else if (_job.PgmEndLine == _job.PendingLine)
                _streamingHandler.Call(StreamingState.JobFinished, true);
            else if (_streamingHandler.Count && response == "ok")
                SendNextLine();

            if (_job.Transferred)
            {
                _job.Transferred = false;
                _model!.BlockExecuting = 0;
                _model.Message = wasChecking ? CheckModeCompleteMessage : "Transfer complete";
            }
            else if (_job.PendingLine != _job.PgmEndLine)
            {
                _job.PendingLine++;
                if (!_job.IsChecking || _job.PendingLine % 250 == 0)
                    _model!.BlockExecuting = _job.PendingLine;
            }
        }
        else if (response == "ok")
            _missed++;

        switch (_streamingState)
        {
            case StreamingState.Send:
                if (response == "start")
                    SendNextLine();
                break;

            case StreamingState.SendMDI:
                if (TryDequeueInject(out var cmd))
                    WriteCommand(cmd);
                if (_injectCommands.Count == 0)
                    _streamingState = StreamingState.Idle;
                break;

            case StreamingState.Reset:
                WriteCommand(GrblConstants.CMD_UNLOCK);
                _streamingState = StreamingState.AwaitResetAck;
                break;

            case StreamingState.AwaitResetAck:
                _streamingHandler.Call(GCode.IsLoaded ? StreamingState.Idle : StreamingState.NoFile, false);
                break;
        }
    }

    private void SendNextLine()
    {
        if (_feedHoldRequested || _stopRequested)
            return;

        while (_job.NextRow != null)
        {
            var line = _job.NextRow.Data;
            var sendComments = AppBase?.SendComments ?? false;

            if (_job.NextRow.IsComment && !sendComments)
            {
                line = "()";
                _job.NextRow.Length = line.Length + 1;
            }

            if (_job.SerialUsed < _serialSize - _job.NextRow.Length)
            {
                if (TryDequeueInject(out var injected))
                    WriteCommand(injected);
                else
                {
                    _job.CurrentRow = _job.NextRow;

                    if (!_job.IsChecking)
                        _job.CurrentRow.Sent = "*";

                    if (line == "%")
                    {
                        if (!(_job.Started = !_job.Started))
                            _job.PgmEndLine = _job.CurrBlock;
                    }
                    else if (_job.CurrentRow.ProgramEnd)
                        _job.PgmEndLine = _job.CurrBlock;

                    _job.NextRow = _job.PgmEndLine == _job.CurrBlock
                        ? null
                        : GCode.Data[++_job.CurrBlock];

                    _job.SerialUsed += _job.CurrentRow.Length;
                    Comms.com?.WriteString(line + '\r');
                    if (_job.CurrentRow.BreakAt)
                        Comms.com?.WriteString("M0\r");
                }

                _job.AckPending++;

                if (!_useBuffering)
                    break;
            }
            else
                break;
        }
    }

    private void SendCommand(string command)
    {
        if (command.Length == 1)
            SendRtCommand(command);
        else if (_streamingState == StreamingState.Idle ||
                 _streamingState == StreamingState.NoFile ||
                 _streamingState == StreamingState.JobFinished ||
                 _streamingState == StreamingState.ToolChange ||
                 _streamingState == StreamingState.Stop ||
                 (command == GrblConstants.CMD_UNLOCK && _streamingState != StreamingState.Send))
        {
            try
            {
                var c = command;
                GCode.Parser.ParseBlock(ref c, true);
                _injectCommands.Enqueue(command);
                if (_streamingState != StreamingState.SendMDI)
                {
                    _streamingState = StreamingState.SendMDI;
                    OnResponseReceived("go");
                }
            }
            catch
            {
            }
        }
    }

    private void SendRtCommand(string command)
    {
        var b = Convert.ToInt32(command[0]);
        if (b > 255)
        {
            b = b switch
            {
                8222 => GrblConstants.CMD_SAFETY_DOOR,
                8225 => GrblConstants.CMD_STATUS_REPORT_ALL,
                710 => GrblConstants.CMD_OPTIONAL_STOP_TOGGLE,
                8240 => GrblConstants.CMD_SINGLE_BLOCK_TOGGLE,
                _ => b
            };
        }

        if (b <= 255)
            Comms.com?.WriteByte((byte)b);
    }

    private bool TryDequeueInject(out string command)
    {
        if (_injectCommands.Count > 0)
        {
            command = _injectCommands.Dequeue();
            return true;
        }

        command = string.Empty;
        return false;
    }

    private void WriteRtByte(byte cmd) =>
        Comms.com?.WriteByte(GrblLegacy.ConvertRTCommand(cmd));

    private void WriteCommand(string command) => Comms.com?.WriteCommand(command);

    private void ClearBlockStatus()
    {
        foreach (var row in GCode.Data)
        {
            if (row.Sent != string.Empty)
                row.Sent = string.Empty;
        }
    }

    private void SetStreamingHandler(StreamingHandler handler)
    {
        if (handler == StreamingHandler.Previous)
            _streamingHandler = _streamingHandlers[(int)StreamingHandler.Previous];
        else if (_streamingHandler.Handler != handler)
        {
            if (handler == StreamingHandler.Idle)
                _streamingHandler = _streamingHandlers[(int)StreamingHandler.Previous] =
                    _streamingHandlers[(int)StreamingHandler.Idle];
            else
            {
                _streamingHandlers[(int)StreamingHandler.Previous] = _streamingHandler;
                _streamingHandler = _streamingHandlers[(int)handler];
                if (handler == StreamingHandler.AwaitAction)
                    _streamingHandler.Count = true;
            }
        }
    }

    private bool CommitState(StreamingState newState, bool changed)
    {
        if (!changed || _model == null)
            return true;

        _model.StreamingState = _streamingState = newState;
        return true;
    }

    private bool StreamingToolChange(StreamingState newState, bool always)
    {
        var changed = _streamingState != newState;

        switch (newState)
        {
            case StreamingState.ToolChange:
                _model!.IsJobRunning = false;
                SetButtons(cycleStart: true, feedHold: false, stop: true);
                if (JobTimer.IsRunning)
                    JobTimer.Pause = true;
                break;

            case StreamingState.Idle:
            case StreamingState.Send:
                if (JobTimer.IsRunning)
                {
                    _model!.IsJobRunning = true;
                    JobTimer.Pause = false;
                    if (_job.ToolChangeLine >= 0)
                        GCode.Data[_job.ToolChangeLine].Sent = "ok";
                    SetStreamingHandler(StreamingHandler.SendFile);
                }
                else
                    SetStreamingHandler(StreamingHandler.Previous);

                _job.ToolChanged = true;
                ControllerWorkParametersSync.Refresh(_model);
                break;

            case StreamingState.Error:
                SetStreamingHandler(StreamingHandler.Previous);
                break;

            case StreamingState.Stop:
                SetStreamingHandler(StreamingHandler.Idle);
                break;
        }

        if (_streamingHandler.Handler != StreamingHandler.ToolChange)
            return _streamingHandler.Call(newState, true);

        return CommitState(newState, changed);
    }

    private bool StreamingFeedHold(StreamingState newState, bool always)
    {
        var changed = _streamingState != newState;

        if (always || changed)
        {
            switch (newState)
            {
                case StreamingState.Halted:
                case StreamingState.FeedHold:
                    SetButtons(cycleStart: true, feedHold: false,
                        stop: _model!.IsJobRunning || _model.IsSDCardJob,
                        rewind: true,
                        cycleStartLabel: "Resume",
                        stopLabel: GrblInfo.IsGrblHAL ? null : "Stop");
                    _streamingHandler.Count = _job.CurrentRow != null;
                    break;

                case StreamingState.Send:
                case StreamingState.Error:
                case StreamingState.Idle:
                    SetStreamingHandler(StreamingHandler.Previous);
                    break;

                case StreamingState.Stop:
                    SetStreamingHandler(StreamingHandler.Idle);
                    break;

                case StreamingState.JobFinished:
                    SetStreamingHandler(StreamingHandler.SendFile);
                    break;
            }
        }

        if (_streamingHandler.Handler != StreamingHandler.FeedHold)
            return _streamingHandler.Call(newState, true);

        return CommitState(newState, changed);
    }

    private bool StreamingSendFile(StreamingState newState, bool always)
    {
        var changed = _streamingState != newState;

        if (changed || always)
        {
            switch (newState)
            {
                case StreamingState.Idle:
                    if (_streamingState == StreamingState.Error)
                    {
                        SetButtons(cycleStart: !GrblInfo.IsGrblHAL, feedHold: false, stop: true);
                        SetStreamingHandler(StreamingHandler.AwaitAction);
                    }
                    else
                        changed = false;
                    break;

                case StreamingState.Send:
                    if (!_model!.IsJobRunning)
                        _model.IsJobRunning = true;
                    _feedHoldEnable = true;
                    SetButtons(cycleStart: false, feedHold: _feedHoldEnable && !_model.FeedHoldDisabled, stop: true, rewind: false, cycleStartLabel: "Cycle Start");
                    break;

                case StreamingState.Error:
                case StreamingState.Halted:
                    _feedHoldEnable = false;
                    break;

                case StreamingState.FeedHold:
                    SetStreamingHandler(StreamingHandler.FeedHold);
                    break;

                case StreamingState.ToolChange:
                    SetStreamingHandler(StreamingHandler.ToolChange);
                    break;

                case StreamingState.JobFinished:
                    if (_grblState.State == GrblStates.Idle || _grblState.State == GrblStates.Check)
                        newState = StreamingState.Idle;
                    _job.Complete = _job.Transferred = true;
                    _job.AckPending = _job.CurrBlock = 0;
                    _job.CurrentRow = _job.NextRow = null;
                    SetStreamingHandler(StreamingHandler.AwaitIdle);
                    break;

                case StreamingState.Stop:
                    if (GrblInfo.IsGrblHAL || _stopRequested || always)
                        SetStreamingHandler(StreamingHandler.Idle);
                    else
                    {
                        newState = StreamingState.Paused;
                        SetStreamingHandler(StreamingHandler.AwaitAction);
                    }
                    break;
            }
        }

        if (_streamingHandler.Handler != StreamingHandler.SendFile)
            return _streamingHandler.Call(newState, true);

        return CommitState(newState, changed);
    }

    private bool StreamingAwaitAction(StreamingState newState, bool always)
    {
        var changed = _streamingState != newState || newState == StreamingState.Idle;

        if (changed || always)
        {
            switch (newState)
            {
                case StreamingState.Idle:
                    SetButtons(cycleStart: !GrblInfo.IsGrblHAL);
                    break;

                case StreamingState.Stop:
                    if (GrblInfo.IsGrblHAL)
                    {
                        if (_model is { GrblReset: false })
                        {
                            Comms.com?.WriteByte(GrblConstants.CMD_STOP);
                            if (!_model.IsParserStateLive)
                                SendCommand(GrblConstants.CMD_GETPARSERSTATE);
                        }
                    }
                    else if (_grblState.State == GrblStates.Run)
                        Comms.com?.WriteByte(GrblConstants.CMD_RESET);

                    newState = StreamingState.Idle;
                    SetStreamingHandler(StreamingHandler.AwaitIdle);
                    break;

                case StreamingState.Paused:
                    SetButtons(cycleStart: true, feedHold: false, stop: true, rewind: true, cycleStartLabel: "Resume", stopLabel: "Stop");
                    if (_job.AckPending == 0)
                        _streamingHandler.Count = false;
                    break;

                case StreamingState.Send:
                    SetStreamingHandler(StreamingHandler.SendFile);
                    SendNextLine();
                    break;

                case StreamingState.JobFinished:
                    SetStreamingHandler(StreamingHandler.SendFile);
                    break;
            }
        }

        if (_streamingHandler.Handler != StreamingHandler.AwaitAction)
            return _streamingHandler.Call(newState, true);

        return CommitState(newState, changed);
    }

    private bool StreamingAwaitIdle(StreamingState newState, bool always)
    {
        var changed = _streamingState != newState || newState == StreamingState.Idle;

        if (changed || always)
        {
            switch (newState)
            {
                case StreamingState.Idle:
                    var wasChecking = _job.IsChecking;
                    if (_model != null)
                        _model.RunTime = JobTimer.RunTime;
                    JobTimer.Stop();
                    _job.IsChecking = false;
                    if (_model != null)
                        _model.IsJobRunning = false;
                    RewindFile();
                    if (wasChecking && _grblState.State == GrblStates.Check)
                        ApplyCheckModeIdleState(CheckModeCompleteMessage);
                    SetStreamingHandler(StreamingHandler.Idle);
                    break;

                case StreamingState.Error:
                case StreamingState.Halted:
                    SetButtons(cycleStart: !GrblInfo.IsGrblHAL, feedHold: false, stop: true);
                    break;

                case StreamingState.Send:
                    _feedHoldEnable = true;
                    SetButtons(cycleStart: false, feedHold: _feedHoldEnable && !(_model?.FeedHoldDisabled ?? false), stop: true, rewind: false, cycleStartLabel: "Cycle Start");
                    break;

                case StreamingState.FeedHold:
                    SetStreamingHandler(StreamingHandler.FeedHold);
                    break;

                case StreamingState.Stop:
                    SetStreamingHandler(StreamingHandler.Idle);
                    break;
            }
        }

        if (_streamingHandler.Handler != StreamingHandler.AwaitIdle)
            return _streamingHandler.Call(newState, true);

        return CommitState(newState, changed);
    }

    private bool StreamingIdle(StreamingState newState, bool always)
    {
        var changed = _streamingState != newState || newState == StreamingState.Idle;
        var model = _model;

        if (changed || always)
        {
            switch (newState)
            {
                case StreamingState.Disabled:
                    SetButtons(controlEnabled: false);
                    break;

                case StreamingState.JobFinished:
                    if (model?.IsSDCardJob == true && _grblState.State == GrblStates.Check)
                        SetStreamingHandler(StreamingHandler.SendFile);
                    break;

                case StreamingState.Idle:
                case StreamingState.NoFile:
                    if (model == null)
                        break;

                    if (_grblState.State == GrblStates.Check)
                        ApplyCheckModeIdleState(model.Message == CheckModeCompleteMessage ? model.Message : CheckModeActiveMessage);
                    else
                    {
                        SetButtons(
                            controlEnabled: !_grblState.MPG,
                            cycleStart: GCode.IsLoaded || (model.IsSDCardJob && model.SDRewind),
                            stop: false,
                            feedHold: false,
                            rewind: false,
                            cycleStartLabel: "Cycle Start");
                        _feedHoldEnable = !_grblState.MPG;
                        model.IsJobRunning = JobTimer.IsRunning;
                    }
                    break;

                case StreamingState.Send:
                    if (model == null)
                        break;

                    if (!string.IsNullOrEmpty(model.FileName) && !_grblState.MPG)
                        model.IsJobRunning = true;
                    if (JobTimer.IsRunning)
                        SetStreamingHandler(StreamingHandler.SendFile);
                    else
                    {
                        SetButtons(cycleStart: false, stop: true, feedHold: !_grblState.MPG && !model.FeedHoldDisabled, rewind: false, cycleStartLabel: "Cycle Start");
                        _feedHoldEnable = !_grblState.MPG;
                    }
                    break;

                case StreamingState.Start:
                    _job.IsSdFile = true;
                    break;

                case StreamingState.Error:
                case StreamingState.Halted:
                    SetButtons(cycleStart: !_grblState.MPG, feedHold: false, stop: !_grblState.MPG, rewind: newState == StreamingState.Halted, cycleStartLabel: newState == StreamingState.Halted ? "Resume" : "Cycle Start");
                    break;

                case StreamingState.FeedHold:
                    SetStreamingHandler(StreamingHandler.FeedHold);
                    break;

                case StreamingState.ToolChange:
                    SetStreamingHandler(StreamingHandler.ToolChange);
                    break;

                case StreamingState.Stop:
                    if (model == null)
                        break;

                    SetButtons(
                        feedHold: false,
                        cycleStart: !(_grblState.MPG || _grblState.State == GrblStates.Alarm) && GCode.IsLoaded,
                        stop: false,
                        rewind: false,
                        cycleStartLabel: "Cycle Start");
                    _feedHoldEnable = !(_grblState.MPG || _grblState.State == GrblStates.Alarm);
                    model.IsJobRunning = false;
                    _job.CurrentRow = _job.NextRow = null;
                    if (model.IsSDCardJob && !GCode.IsLoaded)
                        model.FileName = string.Empty;

                    if (!_grblState.MPG && !_job.Stopped)
                    {
                        if (GrblInfo.IsGrblHAL &&
                            _grblState.State is not GrblStates.Home and not GrblStates.Alarm)
                        {
                            if (!model.GrblReset)
                            {
                                Comms.com?.WriteByte(GrblConstants.CMD_STOP);
                                if (!model.IsParserStateLive)
                                    SendCommand(GrblConstants.CMD_GETPARSERSTATE);
                            }
                        }
                        else if (_grblState.State == GrblStates.Hold && !model.GrblReset)
                            Comms.com?.WriteByte(GrblConstants.CMD_RESET);
                    }

                    _job.Stopped = false;
                    if (JobTimer.IsRunning)
                    {
                        always = false;
                        model.StreamingState = _streamingState =
                            _streamingState == StreamingState.Error ? StreamingState.Idle : newState;
                        SetStreamingHandler(StreamingHandler.AwaitIdle);
                    }
                    else if (_grblState.State != GrblStates.Alarm)
                        return _streamingHandler.Call(StreamingState.Idle, true);
                    break;
            }
        }

        if (_streamingHandler.Handler != StreamingHandler.Idle)
            return _streamingHandler.Call(newState, always);

        return CommitState(newState, changed);
    }

    private void SetButtons(
        bool? controlEnabled = null,
        bool? cycleStart = null,
        bool? feedHold = null,
        bool? stop = null,
        bool? rewind = null,
        string? cycleStartLabel = null,
        string? stopLabel = null)
    {
        ButtonStateChanged?.Invoke(new JobButtonState
        {
            ControlEnabled = controlEnabled,
            CycleStart = cycleStart,
            FeedHold = feedHold,
            Stop = stop,
            Rewind = rewind,
            CycleStartLabel = cycleStartLabel,
            StopLabel = stopLabel
        });
    }

    private void ApplyCheckModeIdleState(string message)
    {
        if (_model == null)
            return;

        SetButtons(
            controlEnabled: !_grblState.MPG,
            cycleStart: !_grblState.MPG && (GCode.IsLoaded || (_model.IsSDCardJob && _model.SDRewind)),
            feedHold: false,
            stop: false,
            rewind: false,
            cycleStartLabel: "Check Program",
            stopLabel: "Stop");
        _feedHoldEnable = !_grblState.MPG;
        _model.IsJobRunning = false;
        _model.Message = message;
    }
}

public sealed class JobButtonState
{
    public bool? ControlEnabled { get; init; }
    public bool? CycleStart { get; init; }
    public bool? FeedHold { get; init; }
    public bool? Stop { get; init; }
    public bool? Rewind { get; init; }
    public string? CycleStartLabel { get; init; }
    public string? StopLabel { get; init; }
}
