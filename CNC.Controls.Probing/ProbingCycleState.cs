using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Probing;

/// <summary>Per-run probing state shared between program build and completion.</summary>
public sealed class ProbingCycleState
{
    public AxisFlags AxisFlags { get; set; } = AxisFlags.None;
    public double[] Af { get; } = new double[3];
    public CenterFindMode CenterMode { get; set; } = CenterFindMode.XY;
    public int CenterPass { get; set; } = 1;
    public double ProbedAngle { get; set; }
    public Position RotationP1 { get; } = new();
    public Position RotationP2 { get; } = new();
    public Position HeightMapOrigin { get; } = new();
    public volatile bool Cancelled;
}
