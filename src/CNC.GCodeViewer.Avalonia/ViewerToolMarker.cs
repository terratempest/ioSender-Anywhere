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
    public static List<NumericVector3> Build(PathBounds bounds, Point3D toolPosition, ToolVisualizerMode mode, double toolDiameter)
    {
        if (mode == ToolVisualizerMode.None)
            return [];

        var arm = Math.Max(bounds.MaxSize * 0.04d, 2d);
        var radius = Math.Max(toolDiameter / 2d, 0.5d);
        var height = Math.Max(arm * 1.5d, 3d);

        return mode switch
        {
            ToolVisualizerMode.Crosshair => Crosshair(toolPosition, arm),
            ToolVisualizerMode.Cone => Cone(toolPosition, radius, height),
            _ => []
        };
    }

    public static Point3D GetToolPosition()
    {
        var grbl = GCodeViewerContext.Grbl;
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

    static List<NumericVector3> Cone(Point3D tip, double radius, double height)
    {
        var baseZ = tip.Z + height;
        var lines = new List<NumericVector3>
        {
            V(tip.X, tip.Y, tip.Z), V(tip.X, tip.Y, baseZ)
        };

        const int segments = 12;
        for (var i = 0; i < segments; i++)
        {
            var a0 = i * 2d * Math.PI / segments;
            var a1 = (i + 1) * 2d * Math.PI / segments;
            var x0 = tip.X + radius * Math.Cos(a0);
            var y0 = tip.Y + radius * Math.Sin(a0);
            var x1 = tip.X + radius * Math.Cos(a1);
            var y1 = tip.Y + radius * Math.Sin(a1);
            lines.Add(V(x0, y0, baseZ));
            lines.Add(V(x1, y1, baseZ));
            lines.Add(V(tip.X, tip.Y, tip.Z));
            lines.Add(V(x0, y0, baseZ));
        }

        return lines;
    }

    static NumericVector3 V(double x, double y, double z) => new((float)x, (float)y, (float)z);
}
