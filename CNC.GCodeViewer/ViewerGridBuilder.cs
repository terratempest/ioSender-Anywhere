using NumericVector3 = System.Numerics.Vector3;

namespace CNC.GCodeViewer.Avalonia;

internal static class ViewerGridBuilder
{
    const double MinorStep = 10d;
    const double MajorStep = 100d;

    public sealed record GridLines(List<NumericVector3> Minor, List<NumericVector3> Major);

    public static GridLines Build(PathBounds bounds)
    {
        var minor = new List<NumericVector3>();
        var major = new List<NumericVector3>();
        if (bounds.IsEmpty)
            return new GridLines(minor, major);

        var spanX = Math.Max(bounds.SizeX, 20d);
        var spanY = Math.Max(bounds.SizeY, 20d);
        var span = Math.Max(spanX, spanY);
        var pad = Math.Max(MajorStep, span * 0.08d);
        var halfW = spanX / 2d + pad;
        var halfL = spanY / 2d + pad;
        var cx = bounds.Center.X;
        var cy = bounds.Center.Y;
        var z = 0d;
        var minX = Floor(cx - halfW, MinorStep);
        var maxX = Ceiling(cx + halfW, MinorStep);
        var minY = Floor(cy - halfL, MinorStep);
        var maxY = Ceiling(cy + halfL, MinorStep);

        AddGridLines(minor, major, minX, maxX, minY, maxY, z);
        return new GridLines(minor, major);
    }

    static void AddGridLines(
        List<NumericVector3> minor,
        List<NumericVector3> major,
        double minX,
        double maxX,
        double minY,
        double maxY,
        double z)
    {
        for (var y = minY; y <= maxY + 1e-6; y += MinorStep)
        {
            var target = IsMajor(y) ? major : minor;
            AddSegment(target, (float)minX, (float)y, (float)z, (float)maxX, (float)y, (float)z);
        }

        for (var x = minX; x <= maxX + 1e-6; x += MinorStep)
        {
            var target = IsMajor(x) ? major : minor;
            AddSegment(target, (float)x, (float)minY, (float)z, (float)x, (float)maxY, (float)z);
        }
    }

    static void AddSegment(List<NumericVector3> target, float x1, float y1, float z1, float x2, float y2, float z2)
    {
        target.Add(new NumericVector3(x1, y1, z1));
        target.Add(new NumericVector3(x2, y2, z2));
    }

    static double Floor(double value, double step) => Math.Floor(value / step) * step;

    static double Ceiling(double value, double step) => Math.Ceiling(value / step) * step;

    static bool IsMajor(double value) => Math.Abs(value / MajorStep - Math.Round(value / MajorStep)) < 1e-6;
}
