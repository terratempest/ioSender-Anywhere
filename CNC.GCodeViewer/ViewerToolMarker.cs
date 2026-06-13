using NumericVector3 = System.Numerics.Vector3;
using CNC.Core;
using CNC.Core.Geometry;

namespace CNC.GCodeViewer.Avalonia;

internal enum ToolVisualizerMode
{
    None = 0,
    Cone = 1,
    Crosshair = 2
}

internal static class ViewerToolMarker
{
    const double ConeScale = 9d;
    const double ConeBaseRadius = 0.5d;
    const double ConeBaseHeight = 6d;
    const int ConeSegments = 24;

    public sealed record ConeGeometry(
        IReadOnlyList<NumericVector3> Sides,
        IReadOnlyList<NumericVector3> Base);

    public static List<NumericVector3> Build(
        PathBounds bounds,
        Point3D toolPosition,
        ToolVisualizerMode mode,
        double toolDiameter,
        bool autoScale = false,
        double cameraDistance = 100d)
    {
        if (mode == ToolVisualizerMode.None)
            return [];

        var arm = Math.Max(bounds.MaxSize * 0.04d, 2d);
        var radius = mode == ToolVisualizerMode.Cone
            ? Math.Max(toolDiameter / 2d, ConeBaseRadius)
            : Math.Max(toolDiameter / 2d, 0.5d);
        var height = mode == ToolVisualizerMode.Cone
            ? ConeBaseHeight
            : Math.Max(arm * 1.5d, 3d);
        if (autoScale && mode == ToolVisualizerMode.Cone)
        {
            var scale = Math.Max(cameraDistance / 1250d, 0.05d);
            height = Math.Max(height * scale, 0.5d);
            radius = Math.Max(radius * scale, 0.25d);
        }

        if (mode == ToolVisualizerMode.Cone)
        {
            radius *= ConeScale;
            height *= ConeScale;
        }

        return mode switch
        {
            ToolVisualizerMode.Crosshair => Crosshair(toolPosition, arm),
            ToolVisualizerMode.Cone => Cone(toolPosition, radius, height).All(),
            _ => []
        };
    }

    public static ConeGeometry BuildCone(
        Point3D toolPosition,
        double toolDiameter,
        bool autoScale = false,
        double cameraDistance = 100d)
    {
        var radius = Math.Max(toolDiameter / 2d, ConeBaseRadius);
        var height = ConeBaseHeight;
        if (autoScale)
        {
            var scale = Math.Max(cameraDistance / 1250d, 0.05d);
            height = Math.Max(height * scale, 0.5d);
            radius = Math.Max(radius * scale, 0.25d);
        }

        radius *= ConeScale;
        height *= ConeScale;
        return Cone(toolPosition, radius, height);
    }

    public static Point3D GetToolPosition(GrblViewModel? grbl)
    {
        if (grbl == null)
            return new Point3D();

        var factor = grbl.UnitFactor;
        var p = grbl.Position;
        return new Point3D(p.X * factor, p.Y * factor, p.Z * factor);
    }

    static List<NumericVector3> Crosshair(Point3D c, double arm)
    {
        return
        [
            V(c.X - arm, c.Y, c.Z), V(c.X + arm, c.Y, c.Z),
            V(c.X, c.Y - arm, c.Z), V(c.X, c.Y + arm, c.Z),
            V(c.X, c.Y, c.Z - arm), V(c.X, c.Y, c.Z + arm)
        ];
    }

    static ConeGeometry Cone(Point3D tip, double radius, double height)
    {
        var baseZ = tip.Z + height;
        var sides = new List<NumericVector3>(ConeSegments * 3);
        var baseFace = new List<NumericVector3>(ConeSegments * 3);
        var baseCenter = V(tip.X, tip.Y, baseZ);
        var tipPoint = V(tip.X, tip.Y, tip.Z);

        for (var i = 0; i < ConeSegments; i++)
        {
            var a0 = i * 2d * Math.PI / ConeSegments;
            var a1 = (i + 1) * 2d * Math.PI / ConeSegments;
            var x0 = tip.X + radius * Math.Cos(a0);
            var y0 = tip.Y + radius * Math.Sin(a0);
            var x1 = tip.X + radius * Math.Cos(a1);
            var y1 = tip.Y + radius * Math.Sin(a1);
            var p0 = V(x0, y0, baseZ);
            var p1 = V(x1, y1, baseZ);

            sides.Add(tipPoint);
            sides.Add(p0);
            sides.Add(p1);

            baseFace.Add(baseCenter);
            baseFace.Add(p1);
            baseFace.Add(p0);
        }

        return new ConeGeometry(sides, baseFace);
    }

    static NumericVector3 V(double x, double y, double z) => new((float)x, (float)y, (float)z);

    static List<NumericVector3> All(this ConeGeometry cone)
    {
        var all = new List<NumericVector3>(cone.Sides.Count + cone.Base.Count);
        all.AddRange(cone.Sides);
        all.AddRange(cone.Base);
        return all;
    }
}
