using Avalonia.Media;
using CNC.App;
using NumericVector3 = System.Numerics.Vector3;

namespace CNC.GCodeViewer.Avalonia;

internal static class ViewerAdornmentsBuilder
{
    public static IReadOnlyList<OpenGl.ViewerLineLayer> BuildOriginAxes(GCodeViewerConfig cfg, PathBounds bounds)
    {
        if (!cfg.ShowAxes && !cfg.ShowCoordinateSystem)
            return [];

        var length = bounds.IsEmpty ? 10d : Math.Max(bounds.MaxSize * 0.15d, 10d);
        return
        [
            Axis(Colors.Red, 0, 0, 0, length, 0, 0, 2f),
            Axis(Colors.LimeGreen, 0, 0, 0, 0, length, 0, 2f),
            Axis(Colors.DeepSkyBlue, 0, 0, 0, 0, 0, length, 2f),
        ];
    }

    public static OpenGl.ViewerLineLayer? BuildViewCube(GCodeViewerConfig cfg, PathBounds bounds)
    {
        if (!cfg.ShowViewCube || bounds.IsEmpty)
            return null;

        var s = Math.Max(bounds.MaxSize * 0.08d, 5d);
        var ox = bounds.MaxX;
        var oy = bounds.MaxY;
        var oz = bounds.MaxZ;
        var color = Color.FromArgb(180, 200, 200, 200);

        var pts = new List<NumericVector3>
        {
            V(ox, oy, oz), V(ox - s, oy, oz),
            V(ox - s, oy, oz), V(ox - s, oy - s, oz),
            V(ox - s, oy - s, oz), V(ox, oy - s, oz),
            V(ox, oy - s, oz), V(ox, oy, oz),
            V(ox, oy, oz), V(ox, oy, oz - s),
            V(ox - s, oy, oz), V(ox - s, oy, oz - s),
            V(ox - s, oy - s, oz), V(ox - s, oy - s, oz - s),
            V(ox, oy - s, oz), V(ox, oy - s, oz - s),
            V(ox, oy, oz - s), V(ox - s, oy, oz - s),
            V(ox - s, oy, oz - s), V(ox - s, oy - s, oz - s),
            V(ox - s, oy - s, oz - s), V(ox, oy - s, oz - s),
        };

        return OpenGl.ViewerLineLayerBuilder.FromPoints(pts, color, 1f);
    }

    static OpenGl.ViewerLineLayer Axis(Color color, double x0, double y0, double z0, double x1, double y1, double z1, float width) =>
        OpenGl.ViewerLineLayerBuilder.FromPoints(
            [V(x0, y0, z0), V(x1, y1, z1)],
            color,
            width)!;

    static NumericVector3 V(double x, double y, double z) => new((float)x, (float)y, (float)z);
}
