using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Probing;

public partial class ToolLengthControl : UserControl, IProbeTab
{
    bool _probeFixture;

    const string InitFailed = "Axes must be homed before probing the fixture!";
    const string ProbingFailed = "Probing failed";
    const string ProbingCompleted = "Probing completed.";

    public ToolLengthControl() => InitializeComponent();

    public ProbingType ProbingType => ProbingType.ToolLength;

    public void Activate(bool activate)
    {
        if (DataContext is not ProbingViewModel probing)
            return;

        if (activate)
        {
            probing.Instructions = string.Empty;
            probing.ProbeFixture = _probeFixture;
            if (!probing.Grbl!.IsParserStateLive)
                probing.SendInternalCommand(probing.Grbl.IsGrblHAL
                    ? GrblConstants.CMD_GETPARSERSTATE
                    : GrblConstants.CMD_GETNGCPARAMETERS);
        }
        else
        {
            _probeFixture = probing.ProbeFixture;
            probing.ProbeFixture = false;
        }
    }

    public void Start(bool preview = false)
    {
        _ = preview;
        if (DataContext is not ProbingViewModel probing)
            return;

        if (!probing.ValidateInput(true))
            return;

        var grbl = probing.Grbl;
        if (grbl == null)
            return;

        if (probing.ProbeFixture && !grbl.AxisHomed.Value.HasFlag(AxisFlags.X | AxisFlags.Y | AxisFlags.Z))
        {
            GrblUi.ShowError(InitFailed, "Probing");
            return;
        }

        if (!probing.VerifyProbe())
            return;

        var checkProbe = probing.Config?.Probing.CheckProbeStatus != false || !probing.ProbeFixture;
        if (!probing.Program.Init(checkProbe))
            return;

        probing.Program.Add(string.Format("G91F{0}", probing.ProbeFeedRate.ToInvariantString()));

        if (probing.ProbeFixture)
        {
            var g59_3Parameters = GrblWorkParameters.GetCoordinateSystem("G59.3");
            if (g59_3Parameters == null)
            {
                probing.Message = "G59.3 coordinate system unavailable.";
                return;
            }

            var g59_3 = new Position(g59_3Parameters);
            var g59_3Z = g59_3.Z;
            g59_3.Z = Math.Min(grbl.HomePosition.Z, g59_3.Z + probing.Depth);
            probing.Program.AddRapidToMPos(grbl.HomePosition, AxisFlags.Z);
            probing.Program.AddRapidToMPos(g59_3, AxisFlags.X | AxisFlags.Y);
            probing.Program.AddRapidToMPos(g59_3, AxisFlags.Z);
            g59_3.Z = g59_3Z;
        }

        probing.Program.AddProbingAction(AxisFlags.Z, true);
        probing.Program.Execute(true);
        OnCompleted();
    }

    public void Stop()
    {
        if (DataContext is ProbingViewModel probing)
            probing.Program.Cancel();
    }

    void OnCompleted()
    {
        if (DataContext is not ProbingViewModel probing)
            return;

        var grbl = probing.Grbl;
        if (grbl == null)
            return;

        var ok = probing.IsSuccess && probing.Positions.Count == 1;

        if (ok)
        {
            var pos = new Position(probing.Positions[0]);

            if (probing.ReferenceToolOffset)
            {
                probing.TloReference = pos.Z;
                ok = probing.WaitForResponse("G49");
            }

            if (probing.AddAction)
            {
                ok = probing.GotoMachinePosition(pos, AxisFlags.Z);

                if (probing.ProbeFixture)
                    pos.Z = probing.FixtureHeight;
                else
                    pos.Z = probing.WorkpieceHeight + probing.TouchPlateHeight;

                if (probing.CoordinateMode == ProbingViewModel.CoordMode.G92)
                    probing.WaitForResponse("G92" + pos.ToString(AxisFlags.Z));
                else
                    probing.WaitForResponse(string.Format("G10L20P{0}{1}", probing.CoordinateSystem, pos.ToString(AxisFlags.Z)));
            }
            else if (!probing.ReferenceToolOffset)
            {
                var tlo = pos.Z;

                if (!probing.ProbeFixture)
                {
                    if (!double.IsNaN(probing.TloReference))
                        tlo -= probing.TloReference;
                }
                else if ((ok = probing.WaitForWcoUpdate() && probing.WaitForResponse(GrblConstants.CMD_GETNGCPARAMETERS)))
                    tlo = tlo - (grbl.WorkPositionOffset.Z - grbl.ToolOffset.Z) - probing.FixtureHeight;

                if (ok)
                {
                    ok = probing.WaitForResponse("G43.1Z" + tlo.ToInvariantString(grbl.Format));
                }
            }

            if (probing.ProbeFixture)
            {
                probing.GotoMachinePosition(grbl.HomePosition, AxisFlags.Z);
                probing.GotoMachinePosition(probing.StartPosition, AxisFlags.X | AxisFlags.Y);
            }

            probing.GotoMachinePosition(probing.StartPosition, AxisFlags.Z);
        }

        if (probing.ReferenceToolOffset)
        {
            probing.ReferenceToolOffset = !ok;
            probing.SendInternalCommand("$TLR");
        }

        RefreshToolOffsetState(probing, grbl);

        probing.Program.End(ok ? ProbingCompleted : ProbingFailed, probing.Positions.Count != 1);
        probing.Program.OnCompleted?.Invoke(ok);
    }

    void OnClearToolOffsetClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ProbingViewModel model)
            return;

        var grbl = model.Grbl;
        if (grbl == null)
            return;

        model.ReferenceToolOffset = model.CanReferenceToolOffset &&
            !(grbl.IsTloReferenceSet && !double.IsNaN(grbl.TloReference));
        if (model.WaitForResponse("G49"))
        {
            RefreshToolOffsetState(model, grbl);
        }
    }

    static void RefreshToolOffsetState(ProbingViewModel probing, GrblViewModel grbl)
    {
        if (GrblInfo.IsGrblHAL)
            probing.WaitForResponse(GrblConstants.CMD_GETPARSERSTATE);
        else
            GrblParserState.Get(true);
    }

    void OnStartClick(object? sender, RoutedEventArgs e) => Start();

    void OnStopClick(object? sender, RoutedEventArgs e) => Stop();
}
