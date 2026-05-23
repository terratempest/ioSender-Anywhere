using Avalonia.Media;
using NumericVector3 = System.Numerics.Vector3;

namespace CNC.GCodeViewer.Avalonia.OpenGl;

internal static class ViewerLineLayerBuilder
{
    public static ViewerLineLayer? FromPoints(IReadOnlyList<NumericVector3> points, Color color, float lineWidth = 1f, string? tag = null)
    {
        if (points.Count < 2)
            return null;

        return new ViewerLineLayer
        {
            Points = points,
            Color = color,
            LineWidth = lineWidth,
            Tag = tag,
        };
    }

    public static ViewerLineLayer? FromTriangles(IReadOnlyList<NumericVector3> points, Color color, string? tag = null)
    {
        if (points.Count < 3)
            return null;

        return new ViewerLineLayer
        {
            Points = points,
            Color = color,
            PrimitiveKind = ViewerPrimitiveKind.Triangles,
            Tag = tag,
        };
    }
}
