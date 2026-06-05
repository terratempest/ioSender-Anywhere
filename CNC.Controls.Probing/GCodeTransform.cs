// Portions from OpenCNCPilot (Martin Pittermann, 2018) — height-map segment splitting.

using CNC.Core;
using CNC.Controls.Avalonia.Services;
using CNC.Core.Geometry;
using CNC.GCode;

namespace CNC.Controls.Probing;

sealed class GCodeTransform
{
    readonly bool _compress;

    public GCodeTransform(bool compress = false)
    {
        _compress = compress;
    }

    static Vector3 ToAbsolute(Vector3 orig, double[] values, bool isRelative = false) =>
        isRelative ? orig + new Vector3(values[0], values[1], values[2]) : new Vector3(values[0], values[1], values[2]);

    public void ApplyHeightMap(ProbingViewModel model)
    {
        var map = model.HeightMap.Map!;
        var segmentLength = Math.Min(map.GridX, map.GridY);
        var precision = model.Grbl!.Precision;
        var file = GCodeFileService.Instance;

        var plane = new GCPlane(GrblParserState.Plane == Plane.XY ? Commands.G17 : Commands.G18, 0, false);
        var distanceMode = GrblParserState.DistanceMode;
        var position = new Position(model.Grbl.Position, model.Grbl.UnitFactor);
        var pos = new Vector3(position.X, position.Y, position.Z);
        var newToolPath = new List<GCodeToken>();
        uint lnr = 1;

        foreach (var token in file.Tokens)
        {
            switch (token.Command)
            {
                case Commands.G0:
                case Commands.G1:
                {
                    var motion = (GCLinearMotion)token;
                    if (motion.AxisFlags == AxisFlags.None)
                    {
                        newToolPath.Add(new GCLinearMotion(motion.Command, lnr++, new Vector3().ToArray(),
                            motion.AxisFlags, motion.BlockDelete));
                    }
                    else
                    {
                        var m = new Line(motion.AxisFlags)
                        {
                            Start = pos,
                            End = pos = ToAbsolute(pos, motion.Values, distanceMode == DistanceMode.Incremental),
                            Rapid = token.Command == Commands.G0
                        };

                        foreach (var subMotion in m.Split(segmentLength))
                        {
                            var target = new Vector3(
                                Math.Round(subMotion.End.X, precision),
                                Math.Round(subMotion.End.Y, precision),
                                Math.Round(subMotion.End.Z + map.InterpolateZ(subMotion.End.X, subMotion.End.Y), precision));

                            newToolPath.Add(new GCLinearMotion(motion.Command, lnr++, target.ToArray(),
                                motion.AxisFlags | AxisFlags.Z, motion.BlockDelete));
                        }
                    }
                    break;
                }

                case Commands.G2:
                case Commands.G3:
                {
                    if (plane.Plane != Plane.XY)
                        throw new Exception(ProbingStrings.HasRadiusArcs);

                    var arc = (GCArc)token;
                    GCArc? lastSegment = null;
                    var center = arc.GetCenter(plane, pos.ToArray());
                    var ijk = new double[3];
                    Array.Copy(arc.IJKvalues, ijk, 3);

                    var m = new Arc
                    {
                        Start = pos,
                        End = pos = ToAbsolute(pos, arc.Values, distanceMode == DistanceMode.Incremental),
                        Direction = token.Command == Commands.G2 ? ArcDirection.CW : ArcDirection.CCW,
                        U = center[0],
                        V = center[1],
                        Plane = ArcPlane.XY
                    };

                    foreach (var subMotion in m.Split(segmentLength))
                    {
                        if (!arc.IsRadiusMode)
                        {
                            ijk[0] = Math.Round(center[0] - subMotion.Start.X, precision);
                            ijk[1] = Math.Round(center[1] - subMotion.Start.Y, precision);
                        }

                        var target = new Vector3(
                            subMotion.End.X,
                            subMotion.End.Y,
                            subMotion.End.Z + map.InterpolateZ(subMotion.End.X, subMotion.End.Y));
                        target = target.Round(precision);

                        var axisFlags = AxisFlags.XYZ;
                        if (lastSegment != null)
                        {
                            if (lastSegment.X == target.X)
                                axisFlags &= ~AxisFlags.X;
                            if (lastSegment.Y == target.Y)
                                axisFlags &= ~AxisFlags.Y;
                            if (lastSegment.Z == target.Z)
                                axisFlags &= ~AxisFlags.Z;
                        }

                        newToolPath.Add(lastSegment = new GCArc(arc.Command, lnr++, target.ToArray(), axisFlags, ijk,
                            arc.IjkFlags, arc.R, arc.P, arc.IJKMode, arc.IsClocwise, arc.BlockDelete));
                    }
                    break;
                }

                case Commands.G17:
                case Commands.G18:
                case Commands.G19:
                    plane = (GCPlane)token;
                    newToolPath.Add(token);
                    break;

                case Commands.G90:
                case Commands.G91:
                    distanceMode = ((GCDistanceMode)token).DistanceMode;
                    newToolPath.Add(token);
                    break;

                default:
                    newToolPath.Add(token);
                    break;
            }
        }

        file.ReplaceFromTokens(newToolPath, $"Heightmap applied", _compress);
    }
}
