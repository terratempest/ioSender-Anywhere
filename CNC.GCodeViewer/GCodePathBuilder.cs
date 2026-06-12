using NumericVector3 = System.Numerics.Vector3;
using CNC.Core;
using CNC.Core.Geometry;
using CNC.GCode;

namespace CNC.GCodeViewer.Avalonia;

public sealed class GCodePathSegments
{
    public List<NumericVector3> Cut { get; } = [];
    public List<NumericVector3> Rapid { get; } = [];
    public List<NumericVector3> Retract { get; } = [];
}

public sealed class GCodePathBuildResult
{
    public required GCodePathSegments Segments { get; init; }
    public int MotionCount { get; init; }
}

/// <summary>Builds line segments for 3D preview from parsed G-code tokens.</summary>
public static class GCodePathBuilder
{
    const int MaxPreviewActions = 500_000;
    const int MaxPreviewPointsPerCurve = 4_096;
    const double PreviewCurveDensityMultiplier = 2d;

    public static GCodePathBuildResult Build(
        IReadOnlyList<GCodeToken> tokens,
        Point3D start,
        double arcResolution = 10d,
        double minDistance = 0.05d,
        CancellationToken cancellationToken = default)
    {
        var result = new GCodePathSegments();
        if (tokens.Count == 0)
            return new GCodePathBuildResult { Segments = result, MotionCount = 0 };

        var tokenList = tokens as List<GCodeToken> ?? tokens.ToList();
        var arcRes = ResolveArcResolution(tokenList.Count, arcResolution);
        var emu = new GCodeEmulator(translate: true, syncMachineState: false);
        emu.SetStartPosition(start);

        var point0 = start;
        var cutCount = 0;
        var motionCount = 0;
        var minDistanceSquared = minDistance > 0d ? minDistance * minDistance : 0d;
        var checkInterval = Math.Max(500, tokenList.Count / 200);

        var i = 0;
        foreach (var cmd in emu.Execute(tokenList))
        {
            if (++i > MaxPreviewActions)
                throw new InvalidOperationException("Toolpath preview exceeded the emulated command limit.");

            if (i % checkInterval == 0)
                cancellationToken.ThrowIfCancellationRequested();

            switch (cmd.Token.Command)
            {
                case Commands.G0:
                case Commands.G1:
                case Commands.G2:
                case Commands.G3:
                    motionCount++;
                    break;
            }

            switch (cmd.Token.Command)
            {
                case Commands.G0:
                    if (cmd.IsRetract)
                        AddSegment(result.Retract, point0, cmd.End);
                    else
                        AddRapidSegment(result, ref cutCount, ref point0, cmd.End);
                    point0 = cmd.End;
                    break;

                case Commands.G1:
                    AddCutSegment(result.Cut, ref cutCount, point0, cmd.End, minDistanceSquared);
                    point0 = cmd.End;
                    break;

                case Commands.G2:
                case Commands.G3:
                    if (cmd.Token is GCArc arc)
                    {
                        var plane = emu.Plane;
                        var relative = emu.DistanceMode == DistanceMode.Incremental;
                        var startArr = ToArray(point0);
                        var last = point0;
                        foreach (var p in GenerateArcPreviewPoints(arc, plane, startArr, arcRes, relative, cancellationToken))
                        {
                            AddCutSegment(result.Cut, ref cutCount, last, p, minDistanceSquared);
                            last = p;
                        }
                        point0 = cmd.End;
                    }
                    break;

                case Commands.G5:
                    if (cmd.Token is GCCubicSpline cubic)
                    {
                        var last = point0;
                        foreach (var p in LimitCurvePoints(
                                     cubic.GeneratePoints(ToArray(point0), arcRes, emu.DistanceMode == DistanceMode.Incremental),
                                     cancellationToken))
                        {
                            AddCutSegment(result.Cut, ref cutCount, last, p, minDistanceSquared);
                            last = p;
                        }
                        point0 = cmd.End;
                    }
                    break;

                case Commands.G5_1:
                    if (cmd.Token is GCQuadraticSpline quad)
                    {
                        var last = point0;
                        foreach (var p in LimitCurvePoints(
                                     quad.GeneratePoints(ToArray(point0), arcRes, emu.DistanceMode == DistanceMode.Incremental),
                                     cancellationToken))
                        {
                            AddCutSegment(result.Cut, ref cutCount, last, p, minDistanceSquared);
                            last = p;
                        }
                        point0 = cmd.End;
                    }
                    break;
            }
        }

        DecimateInPlace(result.Cut);
        DecimateInPlace(result.Rapid);
        DecimateInPlace(result.Retract);

        return new GCodePathBuildResult { Segments = result, MotionCount = motionCount };
    }

    /// <summary>Cut moves executed up to the given source line number (job progress highlight).</summary>
    public static List<NumericVector3> BuildExecutedCut(
        IReadOnlyList<GCodeToken> tokens,
        Point3D start,
        int throughLineNumber,
        double arcResolution = 10d,
        double minDistance = 0.05d,
        CancellationToken cancellationToken = default)
    {
        var executed = new List<NumericVector3>();
        if (tokens.Count == 0 || throughLineNumber <= 0)
            return executed;

        var tokenList = tokens as List<GCodeToken> ?? tokens.ToList();
        var arcRes = ResolveArcResolution(tokenList.Count, arcResolution);
        var emu = new GCodeEmulator(translate: true, syncMachineState: false);
        emu.SetStartPosition(start);

        var point0 = start;
        var cutCount = 0;
        var minDistanceSquared = minDistance > 0d ? minDistance * minDistance : 0d;

        foreach (var cmd in emu.Execute(tokenList))
        {
            if (cmd.Token.LineNumber >= throughLineNumber)
                break;

            cancellationToken.ThrowIfCancellationRequested();

            switch (cmd.Token.Command)
            {
                case Commands.G1:
                    AddCutSegment(executed, ref cutCount, point0, cmd.End, minDistanceSquared);
                    point0 = cmd.End;
                    break;

                case Commands.G2:
                case Commands.G3:
                    if (cmd.Token is GCArc arc)
                    {
                        var plane = emu.Plane;
                        var relative = emu.DistanceMode == DistanceMode.Incremental;
                        var startArr = ToArray(point0);
                        var last = point0;
                        foreach (var p in GenerateArcPreviewPoints(arc, plane, startArr, arcRes, relative, cancellationToken))
                        {
                            AddCutSegment(executed, ref cutCount, last, p, minDistanceSquared);
                            last = p;
                        }
                        point0 = cmd.End;
                    }
                    break;

                case Commands.G5:
                    if (cmd.Token is GCCubicSpline cubic)
                    {
                        var last = point0;
                        foreach (var p in LimitCurvePoints(
                                     cubic.GeneratePoints(ToArray(point0), arcRes, emu.DistanceMode == DistanceMode.Incremental),
                                     cancellationToken))
                        {
                            AddCutSegment(executed, ref cutCount, last, p, minDistanceSquared);
                            last = p;
                        }
                        point0 = cmd.End;
                    }
                    break;

                case Commands.G5_1:
                    if (cmd.Token is GCQuadraticSpline quad)
                    {
                        var last = point0;
                        foreach (var p in LimitCurvePoints(
                                     quad.GeneratePoints(ToArray(point0), arcRes, emu.DistanceMode == DistanceMode.Incremental),
                                     cancellationToken))
                        {
                            AddCutSegment(executed, ref cutCount, last, p, minDistanceSquared);
                            last = p;
                        }
                        point0 = cmd.End;
                    }
                    break;

                case Commands.G0:
                    point0 = cmd.End;
                    cutCount = 0;
                    break;
            }
        }

        DecimateInPlace(executed);
        return executed;
    }

    public static List<NumericVector3> BuildCompletedCut(
        IReadOnlyList<GCodeToken> tokens,
        Point3D start,
        IReadOnlySet<uint> completedLineNumbers,
        double arcResolution = 10d,
        double minDistance = 0.05d,
        CancellationToken cancellationToken = default)
    {
        var executed = new List<NumericVector3>();
        if (tokens.Count == 0 || completedLineNumbers.Count == 0)
            return executed;

        var tokenList = tokens as List<GCodeToken> ?? tokens.ToList();
        var arcRes = ResolveArcResolution(tokenList.Count, arcResolution);
        var emu = new GCodeEmulator(translate: true, syncMachineState: false);
        emu.SetStartPosition(start);

        var point0 = start;
        var cutCount = 0;
        var minDistanceSquared = minDistance > 0d ? minDistance * minDistance : 0d;

        foreach (var cmd in emu.Execute(tokenList))
        {
            var isCompleted = completedLineNumbers.Contains(cmd.Token.LineNumber);
            cancellationToken.ThrowIfCancellationRequested();

            switch (cmd.Token.Command)
            {
                case Commands.G1:
                    if (isCompleted)
                        AddCutSegment(executed, ref cutCount, point0, cmd.End, minDistanceSquared);
                    point0 = cmd.End;
                    break;

                case Commands.G2:
                case Commands.G3:
                    if (cmd.Token is GCArc arc)
                    {
                        if (isCompleted)
                        {
                            var plane = emu.Plane;
                            var relative = emu.DistanceMode == DistanceMode.Incremental;
                            var startArr = ToArray(point0);
                            var last = point0;
                            foreach (var p in GenerateArcPreviewPoints(arc, plane, startArr, arcRes, relative, cancellationToken))
                            {
                                AddCutSegment(executed, ref cutCount, last, p, minDistanceSquared);
                                last = p;
                            }
                        }
                        point0 = cmd.End;
                    }
                    break;

                case Commands.G5:
                    if (cmd.Token is GCCubicSpline cubic)
                    {
                        if (isCompleted)
                        {
                            var last = point0;
                            foreach (var p in LimitCurvePoints(
                                         cubic.GeneratePoints(ToArray(point0), arcRes, emu.DistanceMode == DistanceMode.Incremental),
                                         cancellationToken))
                            {
                                AddCutSegment(executed, ref cutCount, last, p, minDistanceSquared);
                                last = p;
                            }
                        }
                        point0 = cmd.End;
                    }
                    break;

                case Commands.G5_1:
                    if (cmd.Token is GCQuadraticSpline quad)
                    {
                        if (isCompleted)
                        {
                            var last = point0;
                            foreach (var p in LimitCurvePoints(
                                         quad.GeneratePoints(ToArray(point0), arcRes, emu.DistanceMode == DistanceMode.Incremental),
                                         cancellationToken))
                            {
                                AddCutSegment(executed, ref cutCount, last, p, minDistanceSquared);
                                last = p;
                            }
                        }
                        point0 = cmd.End;
                    }
                    break;

                case Commands.G0:
                    point0 = cmd.End;
                    cutCount = 0;
                    break;
            }
        }

        DecimateInPlace(executed);
        return executed;
    }

    static IEnumerable<Point3D> GenerateArcPreviewPoints(
        GCArc arc,
        GCPlane plane,
        double[] start,
        double arcResolution,
        bool isRelative,
        CancellationToken cancellationToken)
    {
        var center = arc.GetCenter(plane, start, isRelative);
        var startAngle = arc.GetStartAngle(plane, start, isRelative);
        var endAngle = arc.GetEndAngle(plane, start, isRelative);
        var sweep = ResolveSweep(arc, startAngle, endAngle);
        var turns = Math.Max(0, arc.P - 1);
        var totalSweep = sweep + Math.PI * 2d * turns;

        if (!double.IsFinite(totalSweep) || totalSweep <= 0d)
            yield break;

        var end = arc.Values.ToArray();
        if (!arc.AxisFlags.HasFlag(GCodeParser.AxisFlag[plane.AxisLinear]))
            end[plane.AxisLinear] = start[plane.AxisLinear];
        else if (isRelative)
            end[plane.AxisLinear] = start[plane.AxisLinear] + end[plane.AxisLinear];

        var radius = Math.Sqrt(
            Math.Pow(start[plane.Axis0] - center[0], 2d) +
            Math.Pow(start[plane.Axis1] - center[1], 2d));

        if (!double.IsFinite(radius) || radius <= 0d)
            yield break;

        var requestedPoints = (int)Math.Ceiling(Math.Max(
            8d,
            totalSweep * arcResolution * PreviewCurveDensityMultiplier / (Math.PI * 2d)));
        var pointCount = Math.Clamp(requestedPoints, 8, MaxPreviewPointsPerCurve);
        var direction = arc.IsClocwise ? -1d : 1d;
        var linearStart = start[plane.AxisLinear];
        var linearDelta = end[plane.AxisLinear] - linearStart;

        for (var i = 0; i < pointCount; i++)
        {
            if ((i & 0xff) == 0)
                cancellationToken.ThrowIfCancellationRequested();

            var t = (double)i / pointCount;
            var angle = startAngle + direction * totalSweep * t;
            var values = (double[])start.Clone();
            values[plane.Axis0] = Math.Cos(angle) * radius + center[0];
            values[plane.Axis1] = Math.Sin(angle) * radius + center[1];
            values[plane.AxisLinear] = linearStart + linearDelta * t;
            yield return new Point3D(values[0], values[1], values[2]);
        }

        yield return new Point3D(end[0], end[1], end[2]);
    }

    static double ResolveSweep(GCArc arc, double startAngle, double endAngle)
    {
        if (startAngle == endAngle)
            return Math.PI * 2d;

        if (endAngle == 0d)
            endAngle = Math.PI * 2d;

        if (!arc.IsClocwise && endAngle < startAngle)
            return Math.PI * 2d - startAngle + endAngle;
        if (arc.IsClocwise && endAngle > startAngle)
            return Math.PI * 2d - endAngle + startAngle;
        return Math.Abs(endAngle - startAngle);
    }

    static IEnumerable<Point3D> LimitCurvePoints(IReadOnlyList<Point3D> points, CancellationToken cancellationToken)
    {
        if (points.Count <= MaxPreviewPointsPerCurve)
        {
            for (var i = 0; i < points.Count; i++)
            {
                if ((i & 0xff) == 0)
                    cancellationToken.ThrowIfCancellationRequested();
                yield return points[i];
            }

            yield break;
        }

        var stride = (double)(points.Count - 1) / (MaxPreviewPointsPerCurve - 1);
        var lastIndex = -1;

        for (var i = 0; i < MaxPreviewPointsPerCurve; i++)
        {
            if ((i & 0xff) == 0)
                cancellationToken.ThrowIfCancellationRequested();

            var index = (int)Math.Round(i * stride);
            if (index <= lastIndex)
                index = lastIndex + 1;
            if (index >= points.Count)
                index = points.Count - 1;

            yield return points[index];
            lastIndex = index;
        }
    }

    static void DecimateInPlace(List<NumericVector3> points)
    {
        if (points.Count <= PathDecimator.MaxVerticesPerLayer)
            return;

        var decimated = PathDecimator.DecimateSegmentPairs(points);
        points.Clear();
        points.AddRange(decimated);
    }

    static double ResolveArcResolution(int tokenCount, double configured)
    {
        if (!double.IsFinite(configured) || configured < 1d)
            configured = 10d;

        if (tokenCount < 50_000)
            return configured;
        if (tokenCount < 200_000)
            return Math.Min(configured, 6d);
        return Math.Min(configured, 4d);
    }

    static void AddRapidSegment(GCodePathSegments result, ref int cutCount, ref Point3D point0, Point3D point)
    {
        if (cutCount > 1)
        {
            var last = result.Cut[^1];
            AddSegment(result.Cut, last, ToVector3(point));
        }

        AddSegment(result.Rapid, point0, point);
        cutCount = 0;
        point0 = point;
    }

    static void AddCutSegment(List<NumericVector3> cut, ref int cutCount, Point3D from, Point3D to, double minDistanceSquared)
    {
        if (minDistanceSquared > 0d && cutCount > 0)
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var dz = to.Z - from.Z;
            if (dx * dx + dy * dy + dz * dz < minDistanceSquared)
                return;
        }

        if (cutCount == 0 || cut[^1] != ToVector3(to))
            AddSegment(cut, from, to);
        cutCount++;
    }

    static void AddSegment(List<NumericVector3> target, Point3D from, Point3D to) =>
        AddSegment(target, ToVector3(from), ToVector3(to));

    static void AddSegment(List<NumericVector3> target, NumericVector3 from, NumericVector3 to)
    {
        if (from == to)
            return;
        target.Add(from);
        target.Add(to);
    }

    static double[] ToArray(Point3D p) => [p.X, p.Y, p.Z];

    static NumericVector3 ToVector3(Point3D p) => new((float)p.X, (float)p.Y, (float)p.Z);
}
