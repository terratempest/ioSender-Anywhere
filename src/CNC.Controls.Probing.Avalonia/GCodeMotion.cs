using CNC.Core;
using CNC.Core.Geometry;
using CNC.GCode;

namespace CNC.Controls.Probing;

enum ArcPlane
{
    XY = 0,
    YZ = 1,
    ZX = 2
}

enum ArcDirection
{
    CW,
    CCW
}

abstract class Motion
{
    public Vector3 Start;
    public Vector3 End;
    public double Feed;

    public Vector3 Delta => End - Start;

    public abstract double Length { get; }

    public abstract Vector3 Interpolate(double ratio);

    public abstract IEnumerable<Motion> Split(double length);
}

sealed class Line : Motion
{
    public bool Rapid;

    public Line()
    {
    }

    public Line(AxisFlags axisFlags)
    {
    }

    public override double Length => Delta.Magnitude();

    public override Vector3 Interpolate(double ratio) => Start + Delta * ratio;

    public override IEnumerable<Motion> Split(double length)
    {
        if (Rapid)
        {
            yield return this;
            yield break;
        }

        var divisions = (int)Math.Ceiling(Length / length);
        if (divisions < 1)
            divisions = 1;

        var lastEnd = Start;
        for (var i = 1; i <= divisions; i++)
        {
            var end = Interpolate((double)i / divisions);
            yield return new Line
            {
                Start = lastEnd,
                End = end,
                Feed = Feed
            };
            lastEnd = end;
        }
    }
}

sealed class Arc : Motion
{
    public ArcPlane Plane;
    public ArcDirection Direction;
    public double U;
    public double V;

    public override double Length => Math.Abs(AngleSpan * Radius);

    double StartAngle
    {
        get
        {
            var startInPlane = Start.RollComponents(-(int)Plane);
            return Math.Atan2(startInPlane.Y - V, startInPlane.X - U);
        }
    }

    double EndAngle
    {
        get
        {
            var endInPlane = End.RollComponents(-(int)Plane);
            return Math.Atan2(endInPlane.Y - V, endInPlane.X - U);
        }
    }

    double AngleSpan
    {
        get
        {
            var span = EndAngle - StartAngle;
            if (Direction == ArcDirection.CW)
            {
                if (span >= 0)
                    span -= 2 * Math.PI;
            }
            else if (span <= 0)
                span += 2 * Math.PI;

            return span;
        }
    }

    double Radius
    {
        get
        {
            var startPlane = Start.RollComponents(-(int)Plane);
            var endPlane = End.RollComponents(-(int)Plane);
            return (
                Math.Sqrt(Math.Pow(startPlane.X - U, 2) + Math.Pow(startPlane.Y - V, 2)) +
                Math.Sqrt(Math.Pow(endPlane.X - U, 2) + Math.Pow(endPlane.Y - V, 2))
            ) / 2;
        }
    }

    public override Vector3 Interpolate(double ratio)
    {
        var angle = StartAngle + AngleSpan * ratio;
        var onPlane = new Vector3(U + Radius * Math.Cos(angle), V + Radius * Math.Sin(angle), 0);
        var helix = (Start + Delta * ratio).RollComponents(-(int)Plane).Z;
        return new Vector3(onPlane.X, onPlane.Y, helix).RollComponents((int)Plane);
    }

    public override IEnumerable<Motion> Split(double length)
    {
        var divisions = (int)Math.Ceiling(Length / length);
        if (divisions < 1)
            divisions = 1;

        var lastEnd = Start;
        for (var i = 1; i <= divisions; i++)
        {
            var end = Interpolate((double)i / divisions);
            yield return new Arc
            {
                Start = lastEnd,
                End = end,
                Feed = Feed,
                Direction = Direction,
                Plane = Plane,
                U = U,
                V = V
            };
            lastEnd = end;
        }
    }
}
