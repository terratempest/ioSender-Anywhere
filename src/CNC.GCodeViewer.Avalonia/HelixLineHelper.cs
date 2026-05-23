using Avalonia.Media;
using HelixToolkit.Avalonia.SharpDX;
using HelixToolkit.SharpDX;
using NumericVector3 = System.Numerics.Vector3;

namespace CNC.GCodeViewer.Avalonia;

internal static class HelixLineHelper
{
    public static LineGeometryModel3D CreateLineModel(IReadOnlyList<NumericVector3> segmentPoints, Color color, float thickness = 1f)
    {
        var builder = new LineBuilder();
        for (var i = 0; i + 1 < segmentPoints.Count; i += 2)
            builder.AddLine(segmentPoints[i], segmentPoints[i + 1]);

        var geometry = builder.ToLineGeometry3D();
        geometry.UpdateVertices();
        geometry.UpdateBounds();

        return new LineGeometryModel3D
        {
            Geometry = geometry,
            Color = color,
            Thickness = thickness,
            IsHitTestVisible = false
        };
    }

    public static void AddLineModel(Viewport3DX viewport, IList<LineGeometryModel3D> registry, LineGeometryModel3D model)
    {
        registry.Add(model);
        viewport.Items.Add(model);
    }

    public static void ClearManaged(Viewport3DX viewport, IList<LineGeometryModel3D> registry)
    {
        foreach (var item in registry)
            viewport.Items.Remove(item);
        registry.Clear();
    }
}
