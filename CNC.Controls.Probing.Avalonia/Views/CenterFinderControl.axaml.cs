using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Controls.Avalonia.Services;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Probing;

public partial class CenterFinderControl : UserControl, IProbeTab
{
    enum FindMode
    {
        XY = 0,
        X,
        Y
    }

    int _pass;
    FindMode _mode = FindMode.XY;

    const string Instructions =
        "Click image above to select probing action.\n" +
        "Place the probe above the approximate center of the workpiece before start.";

    public CenterFinderControl() => InitializeComponent();

    public ProbingType ProbingType => ProbingType.CenterFinder;

    public void Activate(bool activate)
    {
        if (DataContext is not ProbingViewModel probing)
            return;

        if (activate)
        {
            probing.AllowMeasure = true;
            probing.Instructions = Instructions;
            probing.PropertyChanged += Probing_PropertyChanged;
        }
        else
        {
            probing.PropertyChanged -= Probing_PropertyChanged;
        }
    }

    void Probing_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ProbingViewModel.CameraPositions) || DataContext is not ProbingViewModel probing)
            return;

        var ok = probing.CameraPositions <= 1 ||
                 !probing.Positions[probing.CameraPositions - 1].Equals(probing.Positions[probing.CameraPositions - 2]);

        if (ok)
        {
            switch (probing.CameraPositions)
            {
                case 2:
                    ok = _mode == FindMode.Y
                        ? probing.Positions[1].X == probing.Positions[0].X && probing.Positions[1].Y > probing.Positions[0].Y
                        : probing.Positions[1].X > probing.Positions[0].X && probing.Positions[1].Y == probing.Positions[0].Y;
                    break;
                case 3:
                    ok = !probing.Positions[2].Equals(probing.Positions[0]) && probing.Positions[2].X != probing.Positions[1].X;
                    break;
                case 4:
                    ok = !probing.Positions[3].Equals(probing.Positions[0]) &&
                         !probing.Positions[3].Equals(probing.Positions[1]) &&
                         probing.Positions[3].X == probing.Positions[2].X &&
                         probing.Positions[3].Y > probing.Positions[2].Y;
                    break;
            }
        }

        if (!ok)
        {
            GrblUi.ShowError(ProbingStrings.IllegalPosition, "ioSender");
            probing.RemoveLastPosition();
        }
        else
        {
            probing.CanApplyTransform = probing.CameraPositions == (_mode == FindMode.XY ? 4 : 2);
        }
    }

    bool CreateProgram(bool preview)
    {
        if (DataContext is not ProbingViewModel probing)
            return false;

        if (probing.ProbeCenter == Center.None)
        {
            GrblUi.ShowError(ProbingStrings.SelectCenterType, "Center finder");
            return false;
        }

        if (!probing.Program.Init())
        {
            probing.Message = ProbingStrings.InitFailed + " " + probing.Message;
            return false;
        }

        if (_pass == probing.Passes)
            probing.Program.Add(string.Format("G91F{0}", probing.ProbeFeedRate.ToInvariantString()));

        if (preview)
            probing.StartPosition.Zero();

        var rapidto = new Position(probing.StartPosition);
        var xyClearance = probing.XYClearance + probing.ProbeDiameter / 2d;
        rapidto.Z -= probing.Depth;

        switch (probing.ProbeCenter)
        {
            case Center.Inside:
            {
                if (_mode != FindMode.Y)
                {
                    var rapid = probing.WorkpieceSizeX / 2d - xyClearance;
                    probing.Program.AddRapidToMPos(rapidto, AxisFlags.Z);
                    if (rapid > 1d)
                    {
                        rapidto.X -= rapid;
                        probing.Program.AddRapidToMPos(rapidto, AxisFlags.X);
                        rapidto.X = probing.StartPosition.X + rapid;
                    }

                    probing.Program.AddProbingAction(AxisFlags.X, true);
                    probing.Program.AddRapidToMPos(rapidto, AxisFlags.X);
                    probing.Program.AddProbingAction(AxisFlags.X, false);
                    probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.X);
                }

                if (_mode != FindMode.X)
                {
                    var rapid = probing.WorkpieceSizeY / 2d - xyClearance;
                    if (rapid > 1d)
                    {
                        rapidto.Y -= rapid;
                        probing.Program.AddRapidToMPos(rapidto, AxisFlags.Y);
                        rapidto.Y = probing.StartPosition.Y + rapid;
                    }

                    probing.Program.AddProbingAction(AxisFlags.Y, true);
                    probing.Program.AddRapidToMPos(rapidto, AxisFlags.Y);
                    probing.Program.AddProbingAction(AxisFlags.Y, false);
                    probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Y);
                }

                probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Z);
                break;
            }

            case Center.Outside:
            {
                rapidto.X -= probing.WorkpieceSizeX / 2d + xyClearance;
                rapidto.Y -= probing.WorkpieceSizeY / 2d + xyClearance;

                if (_mode != FindMode.Y)
                {
                    probing.Program.AddRapidToMPos(rapidto, AxisFlags.X);
                    probing.Program.AddRapidToMPos(rapidto, AxisFlags.Z);
                    probing.Program.AddProbingAction(AxisFlags.X, false);
                    probing.Program.AddRapidToMPos(rapidto, AxisFlags.X);
                    probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Z);
                    rapidto.X += probing.WorkpieceSizeX + xyClearance * 2d;
                    probing.Program.AddRapidToMPos(rapidto, AxisFlags.X);
                    probing.Program.AddRapidToMPos(rapidto, AxisFlags.Z);
                    probing.Program.AddProbingAction(AxisFlags.X, true);
                    probing.Program.AddRapidToMPos(rapidto, AxisFlags.X);
                    probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Z);
                    probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.X);
                }

                if (_mode != FindMode.X)
                {
                    probing.Program.AddRapidToMPos(rapidto, AxisFlags.Y);
                    probing.Program.AddRapidToMPos(rapidto, AxisFlags.Z);
                    probing.Program.AddProbingAction(AxisFlags.Y, false);
                    probing.Program.AddRapidToMPos(rapidto, AxisFlags.Y);
                    probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Z);
                    rapidto.Y += probing.WorkpieceSizeY + xyClearance * 2d;
                    probing.Program.AddRapidToMPos(rapidto, AxisFlags.Y);
                    probing.Program.AddRapidToMPos(rapidto, AxisFlags.Z);
                    probing.Program.AddProbingAction(AxisFlags.Y, true);
                    probing.Program.AddRapidToMPos(rapidto, AxisFlags.Y);
                }

                probing.Program.AddRapidToMPos(probing.StartPosition, AxisFlags.Z);
                break;
            }
        }

        if (probing.Passes > 1)
            probing.Message = string.Format(ProbingStrings.ProbingPass, probing.Passes - _pass + 1, probing.Passes);

        return true;
    }

    public void Start(bool preview = false)
    {
        if (DataContext is not ProbingViewModel probing)
            return;

        if (!probing.ValidateInput(false) || probing.Passes == 0)
            return;

        _mode = FindMode.XY;

        if (probing.WorkpieceSizeX <= 0d && probing.WorkpieceSizeY <= 0d)
        {
            probing.SetError(nameof(probing.WorkpieceSizeX), string.Format(ProbingStrings.WorkpieceSizeRequired, "X"));
            probing.SetError(nameof(probing.WorkpieceSizeY), string.Format(ProbingStrings.WorkpieceSizeRequired, "Y"));
            return;
        }

        if (probing.WorkpieceSizeX <= 0d)
            _mode = FindMode.Y;

        if (probing.WorkpieceSizeY <= 0d)
            _mode = FindMode.X;

        if (_mode != FindMode.Y && probing.ProbeCenter == Center.Inside &&
            probing.WorkpieceSizeX < probing.XYClearance * 2d + probing.ProbeDiameter)
        {
            probing.SetError(nameof(probing.WorkpieceSizeX), string.Format(ProbingStrings.ClearanceTooLarge, "X"));
            return;
        }

        if (_mode != FindMode.X && probing.ProbeCenter == Center.Inside &&
            probing.WorkpieceSizeY < probing.XYClearance * 2d + probing.ProbeDiameter)
        {
            probing.SetError(nameof(probing.WorkpieceSizeY), string.Format(ProbingStrings.ClearanceTooLarge, "Y"));
            return;
        }

        if (!probing.VerifyProbe())
            return;

        _pass = preview ? 1 : probing.Passes;

        if (CreateProgram(preview))
        {
            do
            {
                if (preview)
                {
                    probing.PreviewText = probing.Program.ToString().Replace("G53", string.Empty);
                    PreviewOnCompleted();
                    probing.PreviewText += "\n; Post XY probe\n" + probing.Program.ToString().Replace("G53", string.Empty);
                }
                else
                {
                    probing.Program.Execute(true);
                    if (OnCompleted())
                        probing.WaitForIdle(string.Empty);
                }
            } while (--_pass != 0 && CreateProgram(preview));
        }
    }

    public void Stop()
    {
        if (DataContext is ProbingViewModel probing)
            probing.Program.Cancel();
    }

    bool OnCompleted()
    {
        if (DataContext is not ProbingViewModel probing)
            return false;

        if (probing.IsSuccess && probing.Positions.Count != (_mode == FindMode.XY ? 4 : 2))
        {
            probing.IsSuccess = false;
            probing.Program.End(ProbingStrings.ProbingFailed, true);
            return false;
        }

        var ok = probing.IsSuccess;
        var axisflags = _mode == FindMode.XY ? AxisFlags.XY : (_mode == FindMode.X ? AxisFlags.X : AxisFlags.Y);

        if (ok)
        {
            var center = new Position(probing.StartPosition);
            double xDistance;
            double yDistance;

            center.X = _mode != FindMode.Y
                ? probing.Positions[0].X + (probing.Positions[1].X - probing.Positions[0].X) / 2d
                : 0d;
            xDistance = _mode != FindMode.Y ? Math.Abs(probing.Positions[1].X - probing.Positions[0].X) : 0d;

            if (_mode == FindMode.XY)
            {
                center.Y = probing.Positions[2].Y + (probing.Positions[3].Y - probing.Positions[2].Y) / 2d;
                yDistance = Math.Abs(probing.Positions[2].Y - probing.Positions[3].Y);
            }
            else
            {
                center.Y = _mode != FindMode.X
                    ? probing.Positions[0].Y + (probing.Positions[1].Y - probing.Positions[0].Y) / 2d
                    : 0d;
                yDistance = _mode != FindMode.X ? Math.Abs(probing.Positions[0].Y - probing.Positions[1].Y) : 0d;
            }

            switch (probing.ProbeCenter)
            {
                case Center.Inside:
                    if (_mode != FindMode.Y)
                        xDistance += probing.ProbeDiameter;
                    if (_mode != FindMode.X)
                        yDistance += probing.ProbeDiameter;
                    break;
                case Center.Outside:
                    if (_mode != FindMode.Y)
                        xDistance -= probing.ProbeDiameter;
                    if (_mode != FindMode.X)
                        yDistance -= probing.ProbeDiameter;
                    break;
            }

            ok = ok && probing.GotoMachinePosition(center, axisflags);

            if (ok && _pass == 1)
            {
                switch (probing.CoordinateMode)
                {
                    case ProbingViewModel.CoordMode.Measure:
                        center.X += probing.ProbeOffsetX;
                        center.Y += probing.ProbeOffsetY;
                        probing.Measurement.Add(new Position(center, 1d / probing.Grbl!.UnitFactor), axisflags, ProbingType);
                        break;
                    case ProbingViewModel.CoordMode.G92:
                        center.X = probing.ProbeOffsetX;
                        center.Y = probing.ProbeOffsetY;
                        probing.WaitForResponse("G92" + center.ToString(axisflags));
                        if (!probing.Grbl!.IsParserStateLive)
                            probing.Grbl.ExecuteCommand("$G");
                        break;
                    case ProbingViewModel.CoordMode.G10:
                        center.X += probing.ProbeOffsetX;
                        center.Y += probing.ProbeOffsetY;
                        probing.WaitForResponse(string.Format("G10L2P{0}{1}", probing.CoordinateSystem, center.ToString(axisflags)));
                        break;
                }

                probing.Program.End(string.Format(ProbingStrings.CenterCompleted,
                    xDistance.ToInvariantString(), yDistance.ToInvariantString()));
            }
        }

        if (!ok)
            probing.Program.End(ProbingStrings.ProbingFailed);

        probing.Program.OnCompleted?.Invoke(ok);
        return ok;
    }

    void PreviewOnCompleted()
    {
        if (DataContext is not ProbingViewModel probing)
            return;

        var axisflags = _mode == FindMode.XY ? AxisFlags.XY : (_mode == FindMode.X ? AxisFlags.X : AxisFlags.Y);
        probing.Program.Clear();
        probing.Program.AddRapidToMPos(probing.StartPosition, axisflags);

        if (probing.CoordinateMode == ProbingViewModel.CoordMode.G92)
        {
            var center = new Position { X = probing.ProbeOffsetX, Y = probing.ProbeOffsetY };
            probing.Program.Add("G92" + center.ToString(axisflags));
        }
        else
        {
            probing.Program.Add(string.Format("G10L2P{0}{1}", probing.CoordinateSystem, probing.StartPosition.ToString(axisflags)));
        }
    }

    void OnStartClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ProbingViewModel probing)
            Start(probing.PreviewEnable);
    }

    void OnCameraClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ProbingViewModel probing)
            return;

        if (probing.ProbeCenter == Center.None)
        {
            GrblUi.ShowError(ProbingStrings.SelectCenterType, "Center finder");
            return;
        }

        if (probing.Positions.Count == (_mode == FindMode.XY ? 4 : 2))
        {
            probing.IsSuccess = true;
            OnCompleted();
            probing.Positions.Clear();
            probing.CanApplyTransform = probing.PreviewEnable = false;
        }
    }

    void OnStopClick(object? sender, RoutedEventArgs e) => Stop();
}
