using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Controls.Avalonia.Services;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Probing;

public partial class RotationControl : UserControl, IProbeTab
{
    bool _addAction;
    volatile bool _isCancelled;
    readonly double[] _af = new double[3];
    double _probedAngle;
    Position _p1 = new();
    Position _p2 = new();

    const string Instructions =
        "Click edge in image above to select probing action.\n" +
        "Move the probe to above the position indicated by green dot before start.";

    public RotationControl()
    {
        InitializeComponent();
    }

    public ProbingType ProbingType => ProbingType.Rotation;

    public void Activate(bool activate)
    {
        if (DataContext is not ProbingViewModel probing)
            return;

        if (activate)
        {
            if (probing.ProbeEdge is Edge.A or Edge.B or Edge.C or Edge.D)
                probing.ProbeEdge = Edge.None;

            probing.PropertyChanged += Probing_PropertyChanged;
            probing.AllowMeasure = false;
            probing.AddAction = _addAction;
            probing.Instructions = Instructions;
        }
        else
        {
            probing.PropertyChanged -= Probing_PropertyChanged;
        }
    }

    void Probing_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (DataContext is not ProbingViewModel probing)
            return;

        switch (e.PropertyName)
        {
            case nameof(ProbingViewModel.CameraPositions):
                if (probing.CameraPositions == 1 && probing.ProbeEdge == Edge.None)
                    if (ToolTip.GetTip(ActionGrid) is string tip && tip.Length > 0)
                        probing.PreviewText += tip.Replace('.', '!');

                if (probing.CanApplyTransform = probing.CameraPositions == 2 && probing.ProbeEdge != Edge.None)
                {
                    _probedAngle = GetAngle(probing);
                    OutputAngle(probing);
                }
                break;

            case nameof(ProbingViewModel.AddAction):
                if ((_addAction = probing.AddAction))
                {
                    probing.Origin = ProbeOrigin.None;
                    probing.ProbeEdge = Edge.AB;
                }
                break;

            case nameof(ProbingViewModel.ProbeEdge):
                if (probing.ProbeEdge != Edge.AB)
                    probing.AddAction = false;
                break;
        }
    }

    public void Start(bool preview = false)
    {
        if (DataContext is not ProbingViewModel probing)
            return;

        if (!probing.ValidateInput(probing.ProbeEdge == Edge.Z))
            return;

        if (probing.ProbeEdge == Edge.None)
        {
            GrblUi.ShowError(ProbingStrings.SelectEdgeType, "Rotation");
            return;
        }

        if (!probing.VerifyProbe())
            return;

        if (!probing.Program.Init())
            return;

        _probedAngle = 0d;
        _isCancelled = false;

        if (preview)
            probing.StartPosition.Zero();

        var xyClearance = probing.XYClearance + probing.ProbeRadius;
        probing.Program.Add(string.Format("G91F{0}", probing.ProbeFeedRate.ToInvariantString()));

        switch (probing.ProbeEdge)
        {
            case Edge.AD:
                _af[GrblConstants.X_AXIS] = 1.0d;
                _af[GrblConstants.Y_AXIS] = -1.0d;
                AddEdge(probing, GrblConstants.Y_AXIS, GrblConstants.X_AXIS, xyClearance);
                break;
            case Edge.AB:
                _af[GrblConstants.X_AXIS] = 1.0d;
                _af[GrblConstants.Y_AXIS] = 1.0d;
                AddEdge(probing, GrblConstants.X_AXIS, GrblConstants.Y_AXIS, xyClearance);
                if (probing.AddAction && preview)
                    AddActionEdge(probing, GrblConstants.Y_AXIS, GrblConstants.X_AXIS, xyClearance);
                break;
            case Edge.CB:
                _af[GrblConstants.X_AXIS] = -1.0d;
                _af[GrblConstants.Y_AXIS] = 1.0d;
                AddEdge(probing, GrblConstants.Y_AXIS, GrblConstants.X_AXIS, xyClearance);
                break;
            case Edge.CD:
                _af[GrblConstants.X_AXIS] = -1.0d;
                _af[GrblConstants.Y_AXIS] = -1.0d;
                AddEdge(probing, GrblConstants.X_AXIS, GrblConstants.Y_AXIS, xyClearance);
                break;
        }

        if (preview)
        {
            probing.PreviewText = probing.Program.ToString().Replace("G53", string.Empty);
            probing.Program.Clear();
            probing.PreviewText += "\n; Post XY probe\n" + probing.Program.ToString().Replace("G53", string.Empty);
        }
        else
        {
            probing.Program.Execute(true);
            OnCompleted();
        }
    }

    void AddEdge(ProbingViewModel probing, int offsetAxis, int clearanceAxis, double xyClearance)
    {
        var probeAxis = GrblInfo.AxisIndexToFlag(clearanceAxis);
        var rapidto = new Position(probing.StartPosition);

        rapidto.Values[clearanceAxis] -= xyClearance * _af[clearanceAxis];
        rapidto.Z -= probing.Depth;

        probing.Program.AddRapidToMPos(rapidto, probeAxis);
        probing.Program.AddRapidToMPos(rapidto, AxisFlags.Z);
        probing.Program.AddProbingAction(probeAxis, _af[clearanceAxis] == -1.0d);

        probing.Program.AddRapidToMPos(rapidto, probeAxis);
        rapidto.Values[offsetAxis] = probing.StartPosition.Values[offsetAxis] + probing.Offset * _af[offsetAxis];
        probing.Program.AddRapidToMPos(rapidto, GrblInfo.AxisIndexToFlag(offsetAxis));
        probing.Program.AddProbingAction(probeAxis, _af[clearanceAxis] == -1.0d);

        probing.Program.AddRapidToMPos(rapidto, probeAxis);
        probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Z);
        probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.XY);
    }

    void AddActionEdge(ProbingViewModel probing, int offsetAxis, int clearanceAxis, double xyClearance)
    {
        var probeAxis = GrblInfo.AxisIndexToFlag(clearanceAxis);
        var rapidto = new Position(probing.StartPosition);

        rapidto.Values[clearanceAxis] -= xyClearance * _af[clearanceAxis];
        rapidto.Values[offsetAxis] += probing.Offset * _af[offsetAxis];
        rapidto.Z -= probing.Depth;

        probing.Program.AddRapidToMPos(rapidto, AxisFlags.XY);
        probing.Program.AddRapidToMPos(rapidto, AxisFlags.Z);
        probing.Program.AddProbingAction(probeAxis, _af[clearanceAxis] == -1.0d);

        probing.Program.AddRapidToMPos(rapidto, probeAxis);
        probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Z);
    }

    public void Stop()
    {
        _isCancelled = true;
        if (DataContext is ProbingViewModel probing)
            probing.Program.Cancel();
    }

    void OutputAngle(ProbingViewModel probing) =>
        probing.Grbl!.Message = string.Format(
            ProbingStrings.ProbedAngle,
            Math.Round(Math.Atan(_probedAngle) * 180d / Math.PI, 3).ToInvariantString());

    void OnActionCompleted()
    {
        if (DataContext is not ProbingViewModel probing)
            return;

        if (!_isCancelled && (probing.CanApplyTransform = probing.IsSuccess && probing.Positions.Count == 1))
        {
            var p3 = new Position(probing.Positions[0]);
            var p4 = new Position(p3.X + probing.Offset * _probedAngle, p3.Y - probing.Offset, 0d);
            var pos = new Position(probing.StartPosition);

            _p1.Y += probing.ProbeOffsetY + probing.ProbeRadius;
            _p2.Y += probing.ProbeOffsetY + probing.ProbeRadius;
            p3.X += probing.ProbeOffsetX + probing.ProbeRadius;
            p4.X += probing.ProbeOffsetX + probing.ProbeRadius;
            var divisor = (_p1.X - _p2.X) * (p3.Y - p4.Y) - (_p1.Y - _p2.Y) * (p3.X - p4.X);
            pos.X = ((_p1.X * _p2.Y - _p1.Y * _p2.X) * (p3.X - p4.X) - (_p1.X - _p2.X) * (p3.X * p4.Y - p3.Y * p4.X)) / divisor;
            pos.Y = ((_p1.X * _p2.Y - _p1.Y * _p2.X) * (p3.Y - p4.Y) - (_p1.Y - _p2.Y) * (p3.X * p4.Y - p3.Y * p4.X)) / divisor;

            if (probing.CanApplyTransform = probing.GotoMachinePosition(pos, AxisFlags.XY))
            {
                switch (probing.CoordinateMode)
                {
                    case ProbingViewModel.CoordMode.G92:
                        pos.X = pos.Y = 0d;
                        probing.WaitForResponse("G92" + pos.ToString(AxisFlags.XY));
                        break;
                    case ProbingViewModel.CoordMode.G10:
                        probing.WaitForResponse(string.Format("G10L2P{0}{1}", probing.CoordinateSystem, pos.ToString(AxisFlags.XY)));
                        break;
                }
            }
        }
    }

    void OnCompleted()
    {
        if (DataContext is not ProbingViewModel probing)
            return;

        if (!_isCancelled && (probing.CanApplyTransform = probing.IsSuccess && probing.Positions.Count == 2))
        {
            _probedAngle = GetAngle(probing);

            if (probing.AddAction)
            {
                _p1 = new Position(probing.Positions[0]);
                _p2 = new Position(probing.Positions[1]);

                probing.Program.Clear();
                probing.Program.Add(string.Format("G91F{0}", probing.ProbeFeedRate.ToInvariantString()));

                switch (probing.ProbeEdge)
                {
                    case Edge.AD:
                        _af[GrblConstants.X_AXIS] = 1.0d;
                        _af[GrblConstants.Y_AXIS] = -1.0d;
                        AddActionEdge(probing, GrblConstants.X_AXIS, GrblConstants.Y_AXIS, probing.XYClearance + probing.ProbeRadius);
                        break;
                    case Edge.AB:
                        _af[GrblConstants.X_AXIS] = 1.0d;
                        _af[GrblConstants.Y_AXIS] = 1.0d;
                        AddActionEdge(probing, GrblConstants.Y_AXIS, GrblConstants.X_AXIS, probing.XYClearance + probing.ProbeRadius);
                        break;
                    case Edge.CB:
                        _af[GrblConstants.X_AXIS] = -1.0d;
                        _af[GrblConstants.Y_AXIS] = 1.0d;
                        AddActionEdge(probing, GrblConstants.X_AXIS, GrblConstants.Y_AXIS, probing.XYClearance + probing.ProbeRadius);
                        break;
                    case Edge.CD:
                        _af[GrblConstants.X_AXIS] = -1.0d;
                        _af[GrblConstants.Y_AXIS] = -1.0d;
                        AddActionEdge(probing, GrblConstants.Y_AXIS, GrblConstants.X_AXIS, probing.XYClearance + probing.ProbeRadius);
                        break;
                }

                probing.Program.Execute(true);
                OnActionCompleted();
            }
        }

        probing.Program.End(probing.CanApplyTransform ? ProbingStrings.ProbingCompleted : ProbingStrings.ProbingFailed,
            probing.Positions.Count != 2);

        if (probing.CanApplyTransform)
            OutputAngle(probing);

        if (probing.Grbl != null && !probing.Grbl.IsParserStateLive &&
            probing.CoordinateMode == ProbingViewModel.CoordMode.G92)
            probing.Grbl.ExecuteCommand(GrblConstants.CMD_GETPARSERSTATE);

        probing.Grbl!.IsJobRunning = false;
        probing.Program.OnCompleted?.Invoke(probing.CanApplyTransform);
    }

    double GetAngle(ProbingViewModel probing)
    {
        var angle = (probing.Positions[1].Y - probing.Positions[0].Y) / (probing.Positions[1].X - probing.Positions[0].X);

        if (probing.ProbeEdge is Edge.CB or Edge.AD)
            angle = -1.0d / angle;

        probing.Grbl!.Message = string.Format(
            ProbingStrings.ProbedAngle,
            Math.Round(Math.Atan(angle) * 180d / Math.PI, 3).ToInvariantString());

        return angle;
    }

    void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ProbingViewModel probing)
            probing.TryApplyRotationToGCode();
    }

    void OnStartClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ProbingViewModel probing)
            Start(probing.PreviewEnable);
    }

    void OnStopClick(object? sender, RoutedEventArgs e) => Stop();
}
