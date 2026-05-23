using CNC.Controls.Avalonia.Services;
using CNC.Core;
using CNC.Core.Geometry;
using CNC.GCode;

namespace CNC.Controls.Probing;

sealed class GCodeRotate
{
    static Vector3 ToAbsolute(Vector3 orig, double[] values, bool isRelative = false) =>
        isRelative ? orig + new Vector3(values[0], values[1], values[2]) : new Vector3(values[0], values[1], values[2]);

    public void ApplyRotation(double angle, Vector3 offset, bool compress = false)
    {
        var file = GCodeFileService.Instance;
        uint g53lnr = 0;
        var precision = file.Parser.Decimals;
        var plane = new GCPlane(GrblParserState.Plane == Plane.XY ? Commands.G17 : Commands.G18, 0, false);
        var distanceMode = GrblParserState.DistanceMode;
        var pos = new Vector3(
            Grbl.GrblViewModel.Position.X,
            Grbl.GrblViewModel.Position.Y,
            Grbl.GrblViewModel.Position.Z);
        var toolPath = new List<GCodeToken>
        {
            new GCComment(Commands.Comment, 0,
                string.Format("{0} degree rotation applied", Math.Round(angle * 180d / Math.PI, 1).ToInvariantString()))
        };

        foreach (var token in file.Tokens)
        {
            switch (token.Command)
            {
                case Commands.G0:
                case Commands.G1:
                {
                    var motion = (GCLinearMotion)token;
                    if (motion.AxisFlags != AxisFlags.None && g53lnr != token.LineNumber)
                    {
                        var target = ToAbsolute(pos, motion.Values);
                        if (distanceMode == DistanceMode.Incremental)
                        {
                            if (!motion.AxisFlags.HasFlag(AxisFlags.X))
                                target = new Vector3(0d, target.Y, 0d);
                            if (!motion.AxisFlags.HasFlag(AxisFlags.Y))
                                target = new Vector3(target.X, 0d, 0d);
                            target = target.RotateZ(0d, 0d, angle).Round(precision);
                        }
                        else
                            target = target.RotateZ(offset.X, offset.Y, angle).Round(precision);

                        if (target.X != pos.X)
                            motion.AxisFlags |= AxisFlags.X;
                        if (target.Y != pos.Y)
                            motion.AxisFlags |= AxisFlags.Y;

                        pos = distanceMode == DistanceMode.Incremental ? pos + target : target;
                        toolPath.Add(new GCLinearMotion(motion.Command, motion.LineNumber, target.ToArray(),
                            motion.AxisFlags, motion.BlockDelete));
                    }
                    else
                    {
                        g53lnr = 0;
                        toolPath.Add(new GCLinearMotion(motion.Command, motion.LineNumber, motion.Values,
                            motion.AxisFlags, motion.BlockDelete));
                    }
                    break;
                }

                case Commands.G2:
                case Commands.G3:
                {
                    if (plane.Plane != Plane.XY)
                        throw new Exception(ProbingStrings.HasG17G18Arcs);

                    var arc = (GCArc)token;
                    if (arc.IsRadiusMode)
                        throw new Exception(ProbingStrings.HasRadiusArcs);

                    var target = ToAbsolute(pos, arc.Values).RotateZ(offset.X, offset.Y, angle).Round(precision);
                    var targetijk = arc.IsRadiusMode
                        ? new Vector3(double.NaN, double.NaN, double.NaN)
                        : new Vector3(arc.IJKvalues[0], arc.IJKvalues[1], arc.IJKvalues[2]).RotateZ(0d, 0d, angle)
                            .Round(precision);

                    if (pos.X != target.X)
                        arc.AxisFlags |= AxisFlags.X;
                    if (pos.Y != target.Y)
                        arc.AxisFlags |= AxisFlags.Y;

                    pos = target;
                    toolPath.Add(new GCArc(arc.Command, arc.LineNumber, pos.ToArray(), arc.AxisFlags,
                        targetijk.ToArray(), arc.IjkFlags, arc.R, arc.P, arc.IJKMode, arc.IsClocwise, arc.BlockDelete));
                    break;
                }

                case Commands.G5:
                {
                    var spline = (GCCubicSpline)token;
                    pos = new Vector3(spline.X, spline.Y, 0d).RotateZ(offset.X, offset.Y, angle).Round(precision);
                    var ij = new Vector3(spline.I, spline.J, 0d).RotateZ(offset.X, offset.Y, angle).Round(precision);
                    var pq = new Vector3(spline.P, spline.Q, 0d).RotateZ(offset.X, offset.Y, angle).Round(precision);
                    toolPath.Add(new GCCubicSpline(spline.Command, spline.LineNumber, pos.ToArray(), spline.AxisFlags,
                        [ij.X, ij.Y, pq.X, pq.Y], spline.BlockDelete));
                    break;
                }

                case Commands.G5_1:
                {
                    var spline = (GCQuadraticSpline)token;
                    pos = new Vector3(spline.X, spline.Y, 0d).RotateZ(offset.X, offset.Y, angle).Round(precision);
                    var ij = new Vector3(spline.I, spline.J, 0d).RotateZ(offset.X, offset.Y, angle).Round(precision);
                    toolPath.Add(new GCQuadraticSpline(spline.Command, spline.LineNumber, pos.ToArray(), spline.AxisFlags,
                        [ij.X, ij.Y], spline.BlockDelete));
                    break;
                }

                case Commands.G17:
                case Commands.G18:
                case Commands.G19:
                    plane = (GCPlane)token;
                    toolPath.Add(token);
                    break;

                case Commands.G53:
                    g53lnr = token.LineNumber;
                    toolPath.Add(token);
                    break;

                case Commands.G73:
                case Commands.G81:
                case Commands.G82:
                case Commands.G83:
                case Commands.G84:
                case Commands.G85:
                case Commands.G86:
                case Commands.G89:
                {
                    var drill = (GCCannedDrill)token;
                    var target = ToAbsolute(pos, drill.Values).RotateZ(offset.X, offset.Y, angle);
                    if (pos.X != target.X)
                        drill.AxisFlags |= AxisFlags.X;
                    if (pos.Y != target.Y)
                        drill.AxisFlags |= AxisFlags.Y;

                    pos = distanceMode == DistanceMode.Incremental
                        ? new Vector3(pos.X + target.X * drill.L, pos.Y + target.Y * drill.L, pos.Z + target.Z)
                        : target;

                    toolPath.Add(new GCCannedDrill(drill.Command, drill.LineNumber, target.Round(precision).ToArray(),
                        drill.AxisFlags, drill.R, drill.L, drill.P, drill.Q, drill.BlockDelete));
                    break;
                }

                case Commands.G90:
                case Commands.G91:
                    distanceMode = ((GCDistanceMode)token).DistanceMode;
                    toolPath.Add(token);
                    break;

                default:
                    toolPath.Add(token);
                    break;
            }
        }

        var degrees = Math.Round(angle * 180d / Math.PI, 1).ToInvariantString();
        file.ReplaceFromTokens(toolPath, $"{degrees} degree rotation applied", compress);
    }
}
