using System.Collections.ObjectModel;
using System.Diagnostics;
using CNC.App;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Probing;

public sealed partial class ProbingViewModel : ProbingPanelViewModel
{
    public enum CoordMode
    {
        G10 = 0,
        G92,
        Measure
    }

    int PollInterval => Config?.PollInterval ?? 250;

    bool ValidateProbeConnected => Config?.Probing.ValidateProbeConnected ?? false;

    readonly CancellationToken _cancellationToken = new();
    readonly List<Position> _positions = [];
    readonly List<Position> _machine = [];
    readonly List<string> _workflowTrace = [];
    readonly object _workflowTraceLock = new();

    string _message = string.Empty;
    string _tool = string.Empty;
    bool _isComplete;
    bool _isSuccess;
    bool _probeFixture;
    bool _hasToolTable;
    bool _hasCs9;
    bool _addAction;
    bool _isPaused;
    bool _referenceToolOffset = true;
    double _tloReferenceOffset = double.NaN;
    double _workpieceHeight;
    int _coordinateSystem;
    CoordMode _cmode = CoordMode.G10;
    ProbingType _probingType = ProbingType.None;
    DistanceMode _distanceMode = DistanceMode.Absolute;
    bool _isCancelled;
    Edge _edge = Edge.None;
    bool _probeZ;
    bool _wasZselected;
    bool _enablePreview;
    string _previewText = string.Empty;
    bool _isCorner;
    bool _allowMeasure;
    bool _wasMetric = true;
    double _workpieceXYEdgeOffset;

    public ProbingViewModel(BaseConfig? config = null)
    {
        Config = config;
        Program = new ProbingProgram(this);
        HeightMap.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(HeightMapViewModel.HasHeightMap))
                UpdateHeightMapCanApply();
        };
    }

    public ProbingProgram Program { get; }

    public BaseConfig? Config { get; }

    public string[] WorkflowTrace
    {
        get
        {
            lock (_workflowTraceLock)
                return [.. _workflowTrace];
        }
    }

    public ProbeMacroViewModel Macro { get; } = new();

    public Measurement Measurement { get; } = new();

    public Position StartPosition { get; } = new();

    public List<Position> Positions => _positions;

    public List<Position> Machine => _machine;

    public ObservableCollection<CoordinateSystem> CoordinateSystems { get; } = [];

    public bool ProbeVerified { get; set; }

#pragma warning disable CS8601
    static void Unsubscribe(ref Action<string> source, Action<string> handler)
    {
        source -= handler;
        source ??= _ => { };
    }
#pragma warning restore CS8601

    public void ClearWorkflowTrace()
    {
        lock (_workflowTraceLock)
            _workflowTrace.Clear();
    }

    public void TraceWorkflow(string entry)
    {
        lock (_workflowTraceLock)
        {
            if (_workflowTrace.Count >= 512)
                _workflowTrace.RemoveAt(0);
            _workflowTrace.Add(entry);
        }
    }

    public bool SendInternalCommand(string command)
    {
        var com = Comms.com;
        if (com == null || command == null)
            return false;

        TraceWorkflow("command:" + command);

        if (command.Length == 0)
        {
            Grbl?.ExecuteCommand(command);
            return true;
        }

        if (command.Length == 1)
        {
            com.WriteByte(GrblLegacy.ConvertRTCommand((byte)command[0]));
            return true;
        }

        com.WriteCommand(command);
        return true;
    }

    public string FastProbe => string.Format(ProbingCommand + "F{0}", ProbeFeedRate.ToInvariantString());

    public string SlowProbe => string.Format(ProbingCommand + "F{0}", LatchFeedRate.ToInvariantString());

    public static string ProbingCommand { get; set; } = "G38.3";

    public string RapidCommand => RapidsFeedRate == 0d ? "G0" : "G1F" + RapidsFeedRate.ToInvariantString();

    public string Message
    {
        get => _message;
        set
        {
            _message = value;
            OnPropertyChanged();
            if (!string.IsNullOrEmpty(_message) && Grbl?.ResponseLogVerbose == true)
                Grbl.ResponseLog.Add(_message);
            if (Grbl != null)
            {
                Program.Silent = true;
                Grbl.Message = _message;
                Program.Silent = false;
            }
        }
    }

    public bool IsCompleted
    {
        get => _isComplete;
        set { _isComplete = value; OnPropertyChanged(); }
    }

    public bool IsSuccess
    {
        get => _isSuccess;
        set { _isSuccess = value; OnPropertyChanged(); }
    }

    public bool IsPaused
    {
        get => _isPaused;
        set { _isPaused = value; OnPropertyChanged(); }
    }

    public int CameraPositions
    {
        get => _positions.Count;
        set => OnPropertyChanged();
    }

    public ProbingType ProbingType
    {
        get => _probingType;
        set
        {
            if (_probingType == value)
                return;
            _probingType = value;
            OnPropertyChanged();
            NotifySidebarVisibility();
        }
    }

    public void NotifySidebarVisibility()
    {
        OnPropertyChanged(nameof(ProbeDiameterEnable));
        OnPropertyChanged(nameof(TouchPlateHeightEnable));
        OnPropertyChanged(nameof(FixtureHeightEnable));
        OnPropertyChanged(nameof(XYOffsetEnable));
        OnPropertyChanged(nameof(OffsetEnable));
        OnPropertyChanged(nameof(ShowClearancesSection));
        OnPropertyChanged(nameof(TouchPlateXYEnabled));
        OnPropertyChanged(nameof(ShowTouchPlateSection));
    }

    public bool ShowTouchPlateSection =>
        TouchPlateHeightEnable || FixtureHeightEnable;

    public bool AllowMeasure
    {
        get => _allowMeasure;
        set { _allowMeasure = value; OnPropertyChanged(); }
    }

    public Edge ProbeEdge
    {
        get => _edge;
        set
        {
            if (value == Edge.Z)
            {
                ClearErrors();
                if (!_probeZ && !ProbeZ)
                {
                    _wasZselected = true;
                    ProbeZ = true;
                }
            }
            else if (_edge == Edge.Z)
            {
                if (_wasZselected)
                {
                    _wasZselected = false;
                    ProbeZ = false;
                }
            }

            _edge = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProbeVerticalEdge));
            ProbeCorner = _edge is Edge.A or Edge.B or Edge.C or Edge.D;
            NotifySidebarVisibility();
            if (_probingType == ProbingType.Rotation && value != Edge.AB)
                AddAction = false;
        }
    }

    public bool ProbeZ
    {
        get => _probeZ;
        set
        {
            _probeZ = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProbeVerticalEdge));
            NotifySidebarVisibility();
        }
    }

    public bool PreviewEnable
    {
        get => _enablePreview;
        set
        {
            if (_enablePreview == value)
                return;
            _enablePreview = value;
            OnPropertyChanged();
            if (!_enablePreview)
            {
                PreviewText = string.Empty;
                if (_positions.Count > 0)
                {
                    _positions.Clear();
                    OnPropertyChanged(nameof(CameraPositions));
                }
            }
        }
    }

    public string PreviewText
    {
        get => _previewText;
        set { _previewText = value; OnPropertyChanged(); }
    }

    public bool ProbeCorner
    {
        get => _isCorner;
        private set
        {
            _isCorner = value;
            OnPropertyChanged();
            NotifySidebarVisibility();
        }
    }

    public bool ShowClearancesSection => XYOffsetEnable || OffsetEnable;

    public bool ProbeVerticalEdge => _edge is not Edge.None and not Edge.Z;

    public bool IsProbeEdgeNegativeX => _edge is Edge.B or Edge.C or Edge.CB;

    public bool IsProbeEdgeNegativeY => _edge is Edge.C or Edge.D or Edge.CD;

    public double ProbeTPOffsetX => TouchPlateIsXY && IsProbeEdgeNegativeX ? -ProbeOffsetX : ProbeOffsetX;

    public double ProbeTPOffsetY => TouchPlateIsXY && IsProbeEdgeNegativeY ? -ProbeOffsetY : ProbeOffsetY;

    public double WorkpieceXYEdgeOffset
    {
        get => _workpieceXYEdgeOffset;
        set { _workpieceXYEdgeOffset = value; OnPropertyChanged(); }
    }

    public bool TouchPlateXYEnabled => _probingType != ProbingType.HeightMap;

    public bool OffsetEnable =>
        (_probingType is ProbingType.EdgeFinderInternal or ProbingType.EdgeFinderExternal && _isCorner)
        || _probingType == ProbingType.Rotation;

    public bool XYOffsetEnable =>
        (_probingType is ProbingType.EdgeFinderInternal or ProbingType.EdgeFinderExternal && _edge is not Edge.None and not Edge.Z)
        || _probingType is ProbingType.CenterFinder or ProbingType.Rotation or ProbingType.HeightMap;

    public bool ProbeDiameterEnable =>
        _probingType == ProbingType.CenterFinder
        || (_probingType is ProbingType.EdgeFinderInternal or ProbingType.EdgeFinderExternal && _edge != Edge.Z);

    public string Tool
    {
        get => _tool;
        set { _tool = value; OnPropertyChanged(); }
    }

    public bool ProbeFixture
    {
        get => _probeFixture;
        set
        {
            _probeFixture = value;
            OnPropertyChanged();
            NotifySidebarVisibility();
            if (_probeFixture)
                AddAction = false;
        }
    }

    public bool HasToolTable
    {
        get => _hasToolTable;
        set { _hasToolTable = value; OnPropertyChanged(); }
    }

    public bool HasCoordinateSystem9
    {
        get => _hasCs9;
        set { _hasCs9 = value; OnPropertyChanged(); }
    }

    public bool ReferenceToolOffset
    {
        get => _referenceToolOffset && CanReferenceToolOffset;
        set
        {
            _referenceToolOffset = value;
            OnPropertyChanged();
            NotifySidebarVisibility();
        }
    }

    public bool CanReferenceToolOffset => GrblInfo.Build >= 20200805 && GrblInfo.IsGrblHAL;

    public double TloReference
    {
        get => Grbl?.IsTloReferenceSet == true ? _tloReferenceOffset : double.NaN;
        set { _tloReferenceOffset = value; OnPropertyChanged(); }
    }

    public bool AddAction
    {
        get => _addAction;
        set
        {
            _addAction = value;
            OnPropertyChanged();
            if (_addAction && ProbingType == ProbingType.Rotation)
            {
                Origin = ProbeOrigin.None;
                ProbeEdge = Edge.AB;
            }
        }
    }

    public double WorkpieceHeight
    {
        get => _workpieceHeight;
        set { _workpieceHeight = value; OnPropertyChanged(); }
    }

    public int CoordinateSystem
    {
        get => _coordinateSystem;
        set { _coordinateSystem = value; OnPropertyChanged(); }
    }

    public CoordMode CoordinateMode
    {
        get => _cmode;
        set { _cmode = value; OnPropertyChanged(); }
    }

    public DistanceMode DistanceMode
    {
        get => _distanceMode;
        set => _distanceMode = value;
    }

    public bool FixtureHeightEnable => ProbingType == ProbingType.ToolLength && _probeFixture;

    public bool TouchPlateHeightEnable => ProbingType switch
    {
        ProbingType.ToolLength => !_probeFixture,
        ProbingType.EdgeFinderExternal or ProbingType.EdgeFinderInternal => _probeZ,
        ProbingType.HeightMap => true,
        _ => false
    };

    public bool CanReferenceToolOffsetBinding => CanReferenceToolOffset;

    public void OnActivated()
    {
        if (Grbl == null)
            return;

        if (CoordinateSystems.Count == 0)
        {
            foreach (var cs in GrblWorkParameters.CoordinateSystems)
            {
                if (cs.Id is > 0 and < 9)
                    CoordinateSystems.Add(new CoordinateSystem(cs.Code, "0"));
                if (cs.Id == 9)
                    HasCoordinateSystem9 = true;
            }

            HasToolTable = GrblInfo.NumTools > 0;
        }

        if (Comms.com is { IsOpen: true })
        {
            if (GrblInfo.IsGrblHAL)
            {
                GrblParserState.Get();
                GrblWorkParameters.Get();
            }
            else
                GrblParserState.Get(true);
        }

        if (!(_wasMetric = GrblParserState.IsMetric))
            WaitForResponse("G21");

        ProbeVerified = !ValidateProbeConnected;
        DistanceMode = GrblParserState.DistanceMode;
        Tool = Grbl.Tool == GrblConstants.NO_TOOL ? "0" : Grbl.Tool;
        var workCs = GrblWorkParameters.GetCoordinateSystem(Grbl.WorkCoordinateSystem);
        var csid = workCs?.Id ?? 1;
        CoordinateSystem = csid == 0 || csid >= 9 ? 1 : csid;

        if (Grbl.IsTloReferenceSet && !double.IsNaN(Grbl.TloReference))
        {
            TloReference = Grbl.TloReference;
            ReferenceToolOffset = false;
        }
        else
        {
            ReferenceToolOffset &= CanReferenceToolOffset;
        }

        ProbingCommand = GrblInfo.ReportProbeResult ? "G38.3" : "G38.2";
        Grbl.Poller.SetState(PollInterval);
    }

    public void OnDeactivated()
    {
        if (Grbl == null || Grbl.IsGCLock)
            return;

        if (Grbl.GrblError != 0)
            WaitForResponse("");

        if (!_wasMetric)
            WaitForResponse("G20");

        WaitForResponse(DistanceMode == DistanceMode.Absolute ? "G90" : "G91");
        Grbl.Poller.SetState(PollInterval);
        Message = string.Empty;
    }

    public bool RemoveLastPosition()
    {
        if (_positions.Count == 0)
            return false;
        _positions.RemoveAt(_positions.Count - 1);
        NotifyProbePositionsChanged();
        return true;
    }

    public void ClearExeStatus()
    {
        _isComplete = _isSuccess = false;
    }

    public bool VerifyProbe()
    {
        if (ProbeVerified || Grbl!.Signals.Value.HasFlag(Signals.Probe))
            return true;

        new ProbeVerifyWindow(this).ShowBlocking();

        if (!ProbeVerified)
            ProbeVerified = GrblUi.AskYesNo(ProbingStrings.NoVerifyContinue, "ioSender");

        if (ProbeVerified)
            Message = ProbingStrings.VerifyStart;

        return ProbeVerified;
    }

    public bool WaitForResponse(string command)
    {
        TraceWorkflow("wait:WaitForResponse");
        bool? res = null;
        var grbl = Grbl;
        if (grbl == null)
            return false;

        var t = new Thread(() =>
        {
            res = WaitFor.AckResponse<string>(
                _cancellationToken,
                null,
                a => grbl.OnResponseReceived += a,
                a => Unsubscribe(ref grbl.OnResponseReceived, a),
                5000, () => SendInternalCommand(command));
        });
        t.Start();

        while (res == null)
            EventUtils.DoEvents();

        return res == true;
    }

    public bool WaitForIdle(string command = "")
    {
        TraceWorkflow("wait:WaitForIdle");
        bool? res = null;
        var grbl = Grbl;
        var com = Comms.com;
        if (grbl == null || com == null)
            return false;

        if (command != string.Empty)
        {
            if (grbl.ResponseLogVerbose)
                grbl.ResponseLog.Add(command);

            com.PurgeQueue();

            new Thread(() =>
            {
                res = WaitFor.AckResponse<string>(
                    _cancellationToken,
                    null,
                    a => grbl.OnResponseReceived += a,
                    a => Unsubscribe(ref grbl.OnResponseReceived, a),
                    1000, () => SendInternalCommand(command));
            }).Start();

            while (res == null)
                EventUtils.DoEvents();
        }

        res = null;

        new Thread(() =>
        {
            res = WaitFor.SingleEvent<string>(
                _cancellationToken,
                null,
                a => grbl.OnRealtimeStatusProcessed += a,
                a => Unsubscribe(ref grbl.OnRealtimeStatusProcessed, a),
                1100);
        }).Start();

        while (res == null)
            EventUtils.DoEvents();

        if (grbl.GrblState.State == GrblStates.Alarm)
            res = null;

        if (grbl.GrblState.State is not GrblStates.Idle and not GrblStates.Alarm)
        {
            var timer = Stopwatch.StartNew();

            if (res == true)
                res = null;

            while (res == null)
            {
                new Thread(() =>
                {
                    res = WaitFor.SingleEvent<string>(
                        _cancellationToken,
                        null,
                        a => grbl.OnResponseReceived += a,
                        a => Unsubscribe(ref grbl.OnResponseReceived, a),
                        5000);
                }).Start();

                while (res == null)
                    EventUtils.DoEvents();

                if (timer.Elapsed.TotalSeconds > 120)
                    break;

                if (grbl.GrblState.State != GrblStates.Idle)
                    res = null;
            }

            timer.Stop();
        }

        Program.ClearResponseQueue();
        return res == true;
    }

    public bool WaitForWcoUpdate()
    {
        TraceWorkflow("wait:WaitForWcoUpdate");
        bool? res = null;
        var grbl = Grbl;
        if (grbl == null)
            return false;

        if (grbl.Poller.IsEnabled)
        {
            new Thread(() =>
            {
                res = WaitFor.SingleEvent<string>(
                    _cancellationToken,
                    null,
                    a => grbl.OnWCOUpdated += a,
                    a => Unsubscribe(ref grbl.OnWCOUpdated, a),
                    PollInterval * 35);
            }).Start();
        }
        else
        {
            return true;
        }

        while (res == null)
            EventUtils.DoEvents();

        return res == true;
    }

    public bool GotoMachinePosition(Position pos, AxisFlags axisflags)
    {
        TraceWorkflow("wait:GotoMachinePosition");
        bool? res = null;
        var wait = true;
        var running = false;
        var grbl = Grbl;
        var com = Comms.com;
        if (grbl == null || com == null)
            return false;

        var command = "G53" + RapidCommand + pos.ToString(axisflags);

        com.PurgeQueue();
        grbl.Poller.SetState(0);
        _isCancelled = false;

        new Thread(() =>
        {
            res = WaitFor.AckResponse<string>(
                _cancellationToken,
                null,
                a => grbl.OnResponseReceived += a,
                a => Unsubscribe(ref grbl.OnResponseReceived, a),
                1000, () => SendInternalCommand(command));
        }).Start();

        while (res == null)
            EventUtils.DoEvents();

        if (res == true)
        {
            var timer = Stopwatch.StartNew();
            while (wait && !_isCancelled)
            {
                if (timer.Elapsed.TotalSeconds > 120)
                {
                    TraceWorkflow("timeout:GotoMachinePosition");
                    _isCancelled = true;
                    break;
                }

                res = null;

                new Thread(() =>
                {
                    res = WaitFor.SingleEvent<string>(
                        _cancellationToken,
                        null,
                        a => grbl.OnRealtimeStatusProcessed += a,
                        a => Unsubscribe(ref grbl.OnRealtimeStatusProcessed, a),
                        400, () => com.WriteByte(GrblLegacy.ConvertRTCommand(GrblConstants.CMD_STATUS_REPORT)));
                }).Start();

                while (res == null)
                    EventUtils.DoEvents();

                wait = res != true;
                running |= grbl.GrblState.State == GrblStates.Run;

                var i = 0;
                var axes = (int)axisflags;
                while (axes != 0 && !wait)
                {
                    if ((axes & 0x01) != 0)
                    {
                        var delta = Math.Abs(pos.Values[i] - grbl.MachinePosition.Values[i] * grbl.UnitFactor);
                        wait = delta > Math.Max(0.003d, GrblInfo.TravelResolution.Values[i] * 2d);
                        var deltaMax = delta;
                        if (wait && grbl.GrblState.State == GrblStates.Idle && (running || deltaMax < 0.01d))
                        {
                            wait = false;
                            _isCancelled = true;
                        }
                    }

                    i++;
                    axes >>= 1;
                }

                if (wait)
                    Thread.Sleep(PollInterval);
            }
            timer.Stop();
        }

        grbl.Poller.SetState(PollInterval);
        return !_isCancelled && !wait;
    }

    public bool ValidateInput(bool zOnly)
    {
        ClearErrors();

        if (!zOnly && XYClearance + ProbeDiameter / 2d > ProbeDistance)
        {
            SetError(nameof(XYClearance), ProbingStrings.ErrorProbingDistance);
            SetError(nameof(ProbeDistance), ProbingStrings.ErrorProbingDistance);
        }

        if (LatchDistance >= ProbeDistance)
        {
            SetError(nameof(LatchDistance), ProbingStrings.ErrorLatchDistance);
            SetError(nameof(ProbeDistance), ProbingStrings.ErrorLatchDistance);
        }

        return !HasErrors;
    }
}
