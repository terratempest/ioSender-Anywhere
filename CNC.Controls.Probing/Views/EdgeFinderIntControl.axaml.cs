using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Controls.Avalonia.Services;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Probing;

public partial class EdgeFinderIntControl : UserControl, IProbeTab
{
    volatile bool _isCancelled;
    AxisFlags _axisflags = AxisFlags.None;
    readonly double[] _af = new double[3];

    const string Instructions =
        "Click edge, corner or center in image above to select probing action.\n" +
        "Move the probe to above the position indicated by green dot before start.";

    public EdgeFinderIntControl() => InitializeComponent();

    public ProbingType ProbingType => ProbingType.EdgeFinderInternal;

    public void Activate(bool activate)
    {
        if (DataContext is not ProbingViewModel probing)
            return;

        if (activate)
        {
            probing.AllowMeasure = true;
            probing.Instructions = Instructions;
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
            GrblUi.ShowError(ProbingStrings.SelectEdgeType, "Edge finder");
            return;
        }

        if (!probing.VerifyProbe())
            return;

        if (!probing.Program.Init())
            return;

        _isCancelled = false;

        if (preview)
            probing.StartPosition.Zero();

        var xyClearance = probing.XYClearance + probing.ProbeDiameter / 2d;
        probing.Program.Add(string.Format("G91F{0}", probing.ProbeFeedRate.ToInvariantString()));

        switch (probing.ProbeEdge)
        {
            case Edge.A:
                if (!AddCorner(probing, true, true, xyClearance))
                    return;
                break;
            case Edge.B:
                if (!AddCorner(probing, false, true, xyClearance))
                    return;
                break;
            case Edge.C:
                if (!AddCorner(probing, false, false, xyClearance))
                    return;
                break;
            case Edge.D:
                if (!AddCorner(probing, true, false, xyClearance))
                    return;
                break;
            case Edge.Z:
                _axisflags = AxisFlags.Z;
                _af[GrblConstants.Z_AXIS] = 1d;
                probing.Program.AddProbingAction(AxisFlags.Z, true);
                break;
            case Edge.AD:
                AddEdge(probing, 'X', true, xyClearance);
                break;
            case Edge.AB:
                AddEdge(probing, 'Y', true, xyClearance);
                break;
            case Edge.CB:
                AddEdge(probing, 'X', false, xyClearance);
                break;
            case Edge.CD:
                AddEdge(probing, 'Y', false, xyClearance);
                break;
        }

        if (preview)
        {
            probing.PreviewText = probing.Program.ToString().Replace("G53", string.Empty);
            PreviewOnCompleted();
            probing.PreviewText += "\n; Post XY probe\n" + probing.Program.ToString().Replace("G53", string.Empty);
        }
        else
        {
            probing.Program.Execute(true);
            OnCompleted();
        }
    }

    void AddEdge(ProbingViewModel probing, char axisletter, bool negative, double xyClearance)
    {
        var axis = GrblInfo.AxisLetterToIndex(axisletter);
        _af[axis] = negative ? -1d : 1d;
        _axisflags = GrblInfo.AxisLetterToFlag(axisletter);

        var rapidto = new Position(probing.StartPosition);
        rapidto.Values[axis] -= xyClearance * _af[axis];
        rapidto.Z -= probing.Depth;

        probing.Program.AddRapidToMPos(rapidto, _axisflags);
        probing.Program.AddRapidToMPos(rapidto, AxisFlags.Z);
        probing.Program.AddProbingAction(_axisflags, negative);

        rapidto.Values[axis] = probing.StartPosition.Values[axis] - xyClearance * _af[axis];
        probing.Program.AddRapidToMPos(rapidto, _axisflags);
        probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Z);
    }

    bool AddCorner(ProbingViewModel probing, bool negx, bool negy, double xyClearance)
    {
        _af[GrblConstants.X_AXIS] = negx ? -1d : 1d;
        _af[GrblConstants.Y_AXIS] = negy ? -1d : 1d;
        _axisflags = AxisFlags.X | AxisFlags.Y;

        if (xyClearance > probing.Offset &&
            !GrblUi.AskYesNo(ProbingStrings.OffsetWarning, "ioSender"))
            return false;

        xyClearance = Math.Min(xyClearance, probing.Offset);

        var rapidto = new Position(probing.StartPosition);
        rapidto.X -= xyClearance * _af[GrblConstants.X_AXIS];
        rapidto.Y -= probing.Offset * _af[GrblConstants.Y_AXIS];
        rapidto.Z -= probing.Depth;

        probing.Program.AddRapidToMPos(rapidto, AxisFlags.X | AxisFlags.Y);
        probing.Program.AddRapidToMPos(rapidto, AxisFlags.Z);
        probing.Program.AddProbingAction(AxisFlags.X, negx);

        probing.Program.AddRapidToMPos(rapidto, AxisFlags.X);
        rapidto.X = probing.StartPosition.X - probing.Offset * _af[GrblConstants.X_AXIS];
        rapidto.Y = probing.StartPosition.Y - xyClearance * _af[GrblConstants.Y_AXIS];
        probing.Program.AddRapidToMPos(rapidto, AxisFlags.X | AxisFlags.Y);

        probing.Program.AddProbingAction(AxisFlags.Y, negy);

        probing.Program.AddRapidToMPos(rapidto, AxisFlags.Y);
        probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Z);
        return true;
    }

    public void Stop()
    {
        _isCancelled = true;
        if (DataContext is ProbingViewModel probing)
            probing.Program.Cancel();
    }

    void OnCompleted()
    {
        if (DataContext is not ProbingViewModel probing)
            return;

        var ok = probing.IsSuccess && probing.Positions.Count > 0;

        if (ok)
        {
            var p = 0;
            var pos = new Position(probing.StartPosition);

            foreach (var i in _axisflags.ToIndices())
                pos.Values[i] = probing.Positions[p++].Values[i] +
                    (i == GrblConstants.Z_AXIS ? 0d : probing.ProbeDiameter / 2d * _af[i]);

            if (double.IsNaN(pos.Z))
            {
                probing.Grbl!.IsJobRunning = false;
                probing.Program.End(ProbingStrings.PositionUnknown);
                return;
            }

            if (probing.ProbeZ && _axisflags != AxisFlags.Z)
            {
                var pz = new Position(pos);
                var xyOffset = probing.WorkpieceXYEdgeOffset == 0d
                    ? probing.ProbeDiameter / 2d
                    : probing.WorkpieceXYEdgeOffset;

                pz.X += xyOffset * _af[GrblConstants.X_AXIS];
                pz.Y += xyOffset * _af[GrblConstants.Y_AXIS];
                if ((ok = !_isCancelled && probing.GotoMachinePosition(pz, _axisflags)))
                {
                    ok = ok && !_isCancelled &&
                          probing.WaitForResponse(probing.FastProbe + "Z-" + probing.Depth.ToInvariantString());
                    ok = ok && !_isCancelled &&
                          probing.WaitForResponse(probing.RapidCommand + "Z" + probing.LatchDistance.ToInvariantString());
                    ok = ok && !_isCancelled && probing.RemoveLastPosition();
                    if ((ok = ok && !_isCancelled &&
                          probing.WaitForResponse(probing.SlowProbe + "Z-" + probing.Depth.ToInvariantString())))
                    {
                        pos.Z = probing.Grbl!.ProbePosition.Z * probing.Grbl.UnitFactor;
                        ok = !_isCancelled && probing.GotoMachinePosition(probing.StartPosition, AxisFlags.Z);
                    }
                }
            }

            ok = ok && !_isCancelled && probing.GotoMachinePosition(pos, _axisflags);

            if (probing.ProbeZ)
                _axisflags |= AxisFlags.Z;

            if (ok)
            {
                switch (probing.CoordinateMode)
                {
                    case ProbingViewModel.CoordMode.Measure:
                        pos.X -= probing.ProbeOffsetX;
                        pos.Y -= probing.ProbeOffsetY;
                        pos.Z -= probing.WorkpieceHeight + probing.TouchPlateHeight + probing.Grbl!.ToolOffset.Z;
                        probing.Measurement.Add(new Position(pos, 1d / probing.Grbl.UnitFactor), _axisflags, ProbingType);
                        break;

                    case ProbingViewModel.CoordMode.G92:
                        if ((ok = !_isCancelled && probing.GotoMachinePosition(pos, AxisFlags.Z)))
                        {
                            pos.X = probing.ProbeOffsetX;
                            pos.Y = probing.ProbeOffsetY;
                            pos.Z = probing.WorkpieceHeight + probing.TouchPlateHeight;
                            probing.WaitForResponse("G92" + pos.ToString(_axisflags));
                            if (!_isCancelled && _axisflags.HasFlag(AxisFlags.Z))
                                probing.GotoMachinePosition(probing.StartPosition, AxisFlags.Z);
                        }
                        break;

                    case ProbingViewModel.CoordMode.G10:
                        pos.X -= probing.ProbeTPOffsetX;
                        pos.Y -= probing.ProbeTPOffsetY;
                        pos.Z -= probing.WorkpieceHeight + probing.TouchPlateHeight + probing.Grbl!.ToolOffset.Z;
                        probing.WaitForResponse(string.Format("G10L2P{0}{1}", probing.CoordinateSystem, pos.ToString(_axisflags)));
                        break;
                }
            }

            if (_axisflags == AxisFlags.Z)
                probing.GotoMachinePosition(probing.StartPosition, AxisFlags.Z);

            probing.Program.End(ok ? ProbingStrings.ProbingCompleted : ProbingStrings.ProbingFailed);
        }

        if (probing.Grbl != null && !probing.Grbl.IsParserStateLive &&
            probing.CoordinateMode == ProbingViewModel.CoordMode.G92)
            probing.Grbl.ExecuteCommand(GrblConstants.CMD_GETPARSERSTATE);

        if (probing.Grbl != null)
            probing.Grbl.IsJobRunning = false;

        probing.Program.OnCompleted?.Invoke(ok);
    }

    void PreviewOnCompleted()
    {
        if (DataContext is not ProbingViewModel probing)
            return;

        var pos = new Position(probing.StartPosition);
        probing.Program.Clear();

        foreach (var i in _axisflags.ToIndices())
            pos.Values[i] = probing.StartPosition.Values[i] +
                (i == GrblConstants.Z_AXIS ? 0d : probing.ProbeDiameter / 2d * _af[i]);

        if (probing.ProbeZ && _axisflags != AxisFlags.Z)
        {
            var pz = new Position(pos);
            pz.X += probing.ProbeDiameter / 2d * _af[GrblConstants.X_AXIS];
            pz.Y += probing.ProbeDiameter / 2d * _af[GrblConstants.Y_AXIS];
            probing.Program.AddRapidToMPos(pz, _axisflags);
            probing.Program.AddProbingAction(AxisFlags.Z, true);
            probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Z);
        }

        probing.Program.AddRapidToMPos(pos, AxisFlags.Y);
        probing.Program.AddRapidToMPos(pos, AxisFlags.X);

        if (probing.ProbeZ)
            _axisflags |= AxisFlags.Z;

        if (probing.CoordinateMode == ProbingViewModel.CoordMode.G92)
        {
            probing.Program.AddRapidToMPos(pos, AxisFlags.Z);
            pos.X = probing.ProbeOffsetX;
            pos.Y = probing.ProbeOffsetY;
            pos.Z = probing.WorkpieceHeight + probing.TouchPlateHeight;
            probing.Program.Add("G92" + pos.ToString(_axisflags));
            if (_axisflags.HasFlag(AxisFlags.Z))
                probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Z);
        }
        else
        {
            pos.X += probing.ProbeOffsetX;
            pos.Y += probing.ProbeOffsetY;
            pos.Z -= probing.WorkpieceHeight + probing.TouchPlateHeight + probing.Grbl!.ToolOffset.Z;
            probing.Program.Add(string.Format("G10L2P{0}{1}", probing.CoordinateSystem, pos.ToString(_axisflags)));
        }
    }

    void OnStartClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ProbingViewModel probing)
            Start(probing.PreviewEnable);
    }

    void OnStopClick(object? sender, RoutedEventArgs e) => Stop();
}
