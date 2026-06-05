using System.Collections.Concurrent;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Probing;

public partial class ProbingProgram
{
    readonly ProbingViewModel _probing;

    GrblViewModel Grbl => _probing.Grbl!;
    readonly List<string> _program = [];
    readonly ConcurrentQueue<string> _cmdResponse = new();
    readonly CancellationToken _cancellationToken = new();

    bool _probeProtect;
    volatile bool _probeAsserted;
    volatile bool _probeConnected;
    volatile bool _isComplete;
    bool _isRunning;
    bool _cancelling;
    bool _hasPause;
    bool _probeOnCycleStart;
    bool _ignoreNextOk;
    DateTime? _ignoreNextOkUntil;
    bool _probeAckReceived;
    bool _probeResultReceived;
    bool _suppressProbeProtectForCurrentCommand;
    DateTime? _idleProbeAssertedAt;
    int _step;

    int PollInterval => _probing.Config?.PollInterval ?? 250;

    public bool Silent { get; set; }
    public Action<bool>? OnCompleted { get; set; }
    public bool IsCancelled { get; set; }

    public ProbingProgram(ProbingViewModel model)
    {
        _probing = model;
        OnCompleted = Completed;
    }

#pragma warning disable CS8601
    static void Unsubscribe(ref Action<string> source, Action<string> handler)
    {
        source -= handler;
        source ??= _ => { };
    }
#pragma warning restore CS8601

    void Completed(bool success)
    {
        if (_probing.Macro.SelectedMacro is not { Id: > 0 })
            return;

        if (success && _probing.Macro.PostJobCommands.Length > 0)
            Grbl.ExecuteMacro(_probing.Macro.PostJobCommands);
        if (_probing.Macro.RunOnce)
            _probing.Macro.Clear();
    }

    void Grbl_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(GrblViewModel.IsProbeSuccess):
                _probing.TraceWorkflow("probe:" + (Grbl.IsProbeSuccess ? "success" : "fail"));
                if (Grbl.IsProbeSuccess)
                {
                    _probing.Positions.Add(new Position(Grbl.ProbePosition, Grbl.UnitFactor));
                    _probing.NotifyProbePositionsChanged();
                    _probeResultReceived = true;
                    _idleProbeAssertedAt = null;
                    if (IsCurrentCommandProbe())
                    {
                        if (!_probeAckReceived)
                        {
                            _ignoreNextOk = true;
                            _ignoreNextOkUntil = DateTime.UtcNow.AddMilliseconds(250);
                        }
                        ResponseReceived("probe:ok");
                    }
                }
                else
                {
                    _probeResultReceived = true;
                    _idleProbeAssertedAt = null;
                    ResponseReceived("fail");
                }
                break;

            case nameof(GrblViewModel.GrblState):
                _probing.TraceWorkflow("state:" + Grbl.GrblState);
                if (Grbl.GrblState.State == GrblStates.Alarm)
                    ResponseReceived("alarm");
                break;

            case nameof(GrblViewModel.Signals):
                if (sender is GrblViewModel model)
                {
                    if (model.Signals.Value.HasFlag(Signals.CycleStart) && _probing.IsPaused && _probeOnCycleStart)
                    {
                        _probeOnCycleStart = false;
                        _probing.IsPaused = false;
                    }

                    if (model.Signals.Value.HasFlag(Signals.Probe))
                        _probing.TraceWorkflow("status:probe-asserted");

                    if (IsCurrentCommandProbe() &&
                        model.Signals.Value.HasFlag(Signals.Probe) &&
                        model.GrblState.State == GrblStates.Idle)
                    {
                        _probing.TraceWorkflow("probe:idle-asserted");
                        _idleProbeAssertedAt ??= DateTime.UtcNow;
                    }

                    if (_probeProtect &&
                        !IsCurrentCommandProbe() &&
                        !_suppressProbeProtectForCurrentCommand &&
                        model.Signals.Value.HasFlag(Signals.Probe) &&
                        model.GrblState.State == GrblStates.Run && model.GrblState.Substate != 2)
                    {
                        Comms.com?.WriteByte(GrblConstants.CMD_STOP);
                        ResponseReceived("fail");
                    }
                }
                break;

            case nameof(GrblViewModel.Message):
                if (!Silent)
                    _probing.Message = Grbl.Message;
                break;

            case nameof(GrblViewModel.GrblReset):
                ResponseReceived("fail");
                break;
        }
    }

    void Probing_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProbingViewModel.IsPaused) && _hasPause && _isRunning && !_probing.IsPaused)
            ResponseReceived("ok");
        else if (e.PropertyName == nameof(ProbingViewModel.IsPaused) && _hasPause && _isRunning && _probing.IsPaused && Grbl.ResponseLogVerbose)
            Grbl.ResponseLog.Add("PM:paused");
    }

    public void Clear() => _program.Clear();

    void ProbeCheck(string data)
    {
        if (!data.StartsWith('<'))
            return;

        var pos = data.IndexOf("|Pn:", StringComparison.Ordinal);
        if (pos <= 0)
            return;

        var elements = (data.Substring(pos + 4).TrimEnd('>') + "|").Split('|');
        _probeAsserted = elements[0].Contains('P');
        _probeConnected = !elements[0].Contains('O');
    }

    public bool IsProbeReady(bool getStatus = true)
    {
        var grbl = _probing.Grbl;
        if (grbl == null)
            return false;

        if (getStatus)
        {
            bool? res = null;
            _probeAsserted = _probeConnected = true;

            new Thread(() =>
            {
                res = WaitFor.SingleEvent<string>(
                    _cancellationToken,
                    null,
                    a => grbl.OnGrblReset += a,
                    a => Unsubscribe(ref grbl.OnGrblReset, a),
                    PollInterval * 2 + 50);
            }).Start();

            while (res == null)
                EventUtils.DoEvents();

            res = null;

            new Thread(() =>
            {
                res = WaitFor.SingleEvent<string>(
                    _cancellationToken,
                    ProbeCheck,
                    a => grbl.OnResponseReceived += a,
                    a => Unsubscribe(ref grbl.OnResponseReceived, a),
                    PollInterval * 5);
            }).Start();

            while (res == null)
                EventUtils.DoEvents();
        }
        else
        {
            _probeAsserted = grbl.Signals.Value.HasFlag(Signals.Probe);
            _probeConnected = !grbl.Signals.Value.HasFlag(Signals.ProbeDisconnected);
        }

        var ok = true;
        if (ok && !_probeConnected)
        {
            _probing.Message = ProbingStrings.NoProbe;
            ok = false;
        }

        if (ok && _probeAsserted)
        {
            _probing.Message = ProbingStrings.ProbeAsserted;
            ok = false;
        }

        return ok;
    }

    public bool Init(bool checkProbe = true)
    {
        bool? res = null;
        var grbl = _probing.Grbl;
        var com = Comms.com;
        if (grbl == null || com == null)
            return false;

        IsCancelled = false;
        _probing.Message = string.Empty;

        grbl.Poller.SetState(0);

        if (grbl.GrblError != 0)
        {
            new Thread(() =>
            {
                res = WaitFor.AckResponse<string>(
                    _cancellationToken,
                    null,
                    a => grbl.OnResponseReceived += a,
                    a => Unsubscribe(ref grbl.OnResponseReceived, a),
                    1000, () => grbl.ExecuteCommand(""));
            }).Start();

            while (res == null)
                EventUtils.DoEvents();

            res = null;
        }

        new Thread(() =>
        {
            res = WaitFor.SingleEvent<string>(
                _cancellationToken,
                null,
                a => grbl.OnResponseReceived += a,
                a => Unsubscribe(ref grbl.OnResponseReceived, a),
                PollInterval * 5,
                () => com.WriteByte(GrblInfo.IsGrblHAL
                    ? GrblConstants.CMD_STATUS_REPORT_ALL
                    : GrblLegacy.ConvertRTCommand(GrblConstants.CMD_STATUS_REPORT)));
        }).Start();

        while (res == null)
            EventUtils.DoEvents();

        grbl.Poller.SetState(PollInterval);

        if (grbl.GrblState.State == GrblStates.Alarm)
        {
            _probing.Message = GrblAlarms.GetMessage(grbl.GrblState.Substate.ToString());
            res = false;
        }

        if (res == true && checkProbe)
            res = IsProbeReady(false);

        if (res == true && !(grbl.GrblState.State == GrblStates.Idle || grbl.GrblState.State == GrblStates.Tool))
        {
            _probing.Message = ProbingStrings.FailedNotIdle;
            res = false;
        }

        if (res == true && !grbl.IsMachinePositionKnown)
        {
            _probing.Message = ProbingStrings.FailedNoPos;
            res = false;
        }

        _probing.StartPosition.Set(grbl.MachinePosition);
        if (grbl.UnitFactor != 1d)
            _probing.StartPosition.Scale(grbl.UnitFactor);

        _hasPause = _probeOnCycleStart = false;
        _program.Clear();

        return res == true;
    }

    public void AddProbingAction(AxisFlags axis, bool negative)
    {
        var axisLetter = axis.ToString();
        _program.Add(_probing.FastProbe + axisLetter + (negative ? "-" : "") + _probing.ProbeDistance.ToInvariantString());
        if (_probing.LatchDistance > 0d)
        {
            _program.Add("!" + _probing.RapidCommand + axisLetter + (negative ? "" : "-") + _probing.LatchDistance.ToInvariantString());
            _program.Add(_probing.SlowProbe + axisLetter + (negative ? "-" : "") +
                         Math.Max(_probing.LatchDistance * 1.5d, 2d / _probing.Grbl!.UnitFactor).ToInvariantString());
        }
    }

    public void AddMessage(string msg) => _program.Add("#" + msg);

    public void Add(string cmd) => _program.Add(cmd);

    public void AddRapid(string cmd) => _program.Add(_probing.RapidCommand + cmd);

    public void AddPause()
    {
        _hasPause = true;
        _program.Add("pause");
    }

    public void AddRapidToMPos(string cmd) => _program.Add("G53" + _probing.RapidCommand + cmd);

    public void AddRapidToMPos(Position pos, AxisFlags axisflags) =>
        _program.Add("G53" + _probing.RapidCommand + pos.ToString(axisflags));

    public void ClearResponseQueue()
    {
        Comms.com?.PurgeQueue();
        while (_cmdResponse.TryDequeue(out _))
        {
        }
    }

    public void Cancel()
    {
        IsCancelled = true;
        Comms.com?.WriteByte(GrblInfo.IsGrblHAL ? GrblConstants.CMD_STOP : GrblConstants.CMD_RESET);
        if (!_isComplete)
            ResponseReceived("cancel");
    }

    public void End(string message, bool forceMessage = false)
    {
        if (_isRunning)
        {
            var grbl = _probing.Grbl;
            if (grbl == null)
                return;

            grbl.PropertyChanged -= Grbl_PropertyChanged;
            Unsubscribe(ref grbl.OnCommandResponseReceived, ResponseReceived);
            Unsubscribe(ref grbl.OnResponseReceived, TraceResponseReceived);
            if (_hasPause)
                _probing.PropertyChanged -= Probing_PropertyChanged;
            _isRunning = grbl.IsJobRunning = false;
            _probing.SendInternalCommand(_probing.DistanceMode == DistanceMode.Absolute ? "G90" : "G91");
        }

        if (!_isComplete || _probing.IsSuccess || forceMessage)
            _probing.Message = message;
        _isComplete = true;
    }

    public bool Execute(bool go)
    {
        _ = go;
        _probing.TraceWorkflow("wait:Execute");
        _isComplete = false;
        _probing.ClearExeStatus();

        if (_program.Count == 0)
            return _probing.IsSuccess;

        _step = 0;
        _probing.Positions.Clear();
        _probing.Machine.Clear();
        _probing.NotifyProbePositionsChanged();

        var grbl = _probing.Grbl;
        var com = Comms.com;
        if (grbl == null || com == null)
            return false;

        com.PurgeQueue();

        if (!_isRunning)
        {
            _isRunning = true;
            _probeProtect = GrblInfo.HasSimpleProbeProtect;
            ClearResponseQueue();
            grbl.OnCommandResponseReceived += ResponseReceived;
            grbl.OnResponseReceived += TraceResponseReceived;
            grbl.PropertyChanged += Grbl_PropertyChanged;
            if (_hasPause)
                _probing.PropertyChanged += Probing_PropertyChanged;
        }

        if (_probing.Macro.PreJobCommands.Length > 0)
            _program.InsertRange(0, _probing.Macro.PreJobCommands);

        _cancelling = false;
        _ignoreNextOk = false;
        _ignoreNextOkUntil = null;
        _probeAckReceived = false;
        _probeResultReceived = false;
        _suppressProbeProtectForCurrentCommand = false;
        _idleProbeAssertedAt = null;
        grbl.IsJobRunning = true;

        if (_probing.Message == string.Empty)
            _probing.Message = ProbingStrings.Probing;

        ExecuteCurrentCommand(grbl);

        while (!_isComplete)
        {
            EventUtils.DoEvents();

            if (IsCurrentCommandProbe() &&
                _idleProbeAssertedAt is { } idleProbeAssertedAt &&
                !_probeResultReceived &&
                DateTime.UtcNow - idleProbeAssertedAt >= TimeSpan.FromMilliseconds(Math.Max(PollInterval * 2, 250)))
            {
                _idleProbeAssertedAt = null;
                ResponseReceived("fail");
            }

            if (!_cmdResponse.TryDequeue(out var response))
                continue;

            if (_ignoreNextOk && response == "ok")
            {
                if (_ignoreNextOkUntil == null || DateTime.UtcNow <= _ignoreNextOkUntil.Value)
                {
                    _ignoreNextOk = false;
                    _ignoreNextOkUntil = null;
                    continue;
                }

                _ignoreNextOk = false;
                _ignoreNextOkUntil = null;
            }

            if (response == "probe:ok")
                response = "ok";

            if (grbl.ResponseLogVerbose)
                grbl.ResponseLog.Add("PM:" + response);

            if (IsCurrentCommandProbe())
            {
                if (response == "ok" && !_probeResultReceived)
                {
                    _probeAckReceived = true;
                    continue;
                }

                if (response == "ok")
                    _probeAckReceived = false;
            }

            if (response == "ok")
            {
                if (++_step < _program.Count)
                {
                    _suppressProbeProtectForCurrentCommand = false;

                    if (_program[_step].StartsWith('#'))
                    {
                        grbl.Message = _program[_step][1..];
                        if (++_step == _program.Count)
                            break;
                    }

                    if (_program[_step].StartsWith('!'))
                    {
                        _program[_step] = _program[_step][1..];
                        _probing.RemoveLastPosition();
                        _suppressProbeProtectForCurrentCommand = true;
                    }

                    if (_program[_step] == "pause")
                    {
                        _probing.IsPaused = true;
                        _probeOnCycleStart = !grbl.Signals.Value.HasFlag(Signals.CycleStart);
                    }
                    else
                    {
                        ExecuteCurrentCommand(grbl);
                    }
                }
            }

            if (_step == _program.Count || response != "ok")
            {
                _probing.IsSuccess = _step == _program.Count && response == "ok";
                if (!_probing.IsSuccess)
                    End(grbl.GrblState.State == GrblStates.Alarm
                        ? ProbingStrings.FailedAlarm
                        : ProbingStrings.FailedCancelled);
                _isComplete = _probing.IsCompleted = true;
            }
        }

        if (_probing.Message == ProbingStrings.Probing)
            _probing.Message = string.Empty;

        return _probing.IsSuccess;
    }

    public bool ProbeZ(double x, double y)
    {
        _probing.WaitForIdle();

        if (!Init(false))
            return false;

        AddRapid(string.Format("G90X{0}Y{1}", x.ToInvariantString(_probing.Grbl!.Format), y.ToInvariantString(_probing.Grbl.Format)));
        AddMessage(ProbingStrings.ProbingAtX0Y0);
        Add(string.Format("G91F{0}", _probing.ProbeFeedRate.ToInvariantString()));
        AddProbingAction(AxisFlags.Z, true);

        return Execute(true) && _probing.Positions.Count == 1;
    }

    void ResponseReceived(string response)
    {
        _probing.TraceWorkflow("command-response:" + response);
        if (!_cancelling)
            _cmdResponse.Enqueue(response);

        if (response == "cancel")
            _cancelling = true;
    }

    void TraceResponseReceived(string response) =>
        _probing.TraceWorkflow("response:" + response);

    void ExecuteCurrentCommand(GrblViewModel grbl)
    {
        _probeAckReceived = false;
        _probeResultReceived = false;
        _idleProbeAssertedAt = null;
        _probing.TraceWorkflow("command:" + _program[_step]);
        _probing.SendInternalCommand(_program[_step]);
    }

    bool IsCurrentCommandProbe() =>
        _isRunning && _step >= 0 && _step < _program.Count && IsProbingCommand(_program[_step]);

    static bool IsProbingCommand(string command) =>
        command.Contains("G38.", StringComparison.OrdinalIgnoreCase);

    public override string ToString() => string.Join('\n', _program);
}
