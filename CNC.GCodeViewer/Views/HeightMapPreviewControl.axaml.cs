using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using CNC.Core.Geometry;
using CNC.GCodeViewer.Avalonia.OpenGl;
using AvaloniaColor = Avalonia.Media.Color;

namespace CNC.GCodeViewer.Avalonia.Views;

public partial class HeightMapPreviewControl : UserControl
{
    const float BoundaryThickness = 1.25f;
    const float GridThickness = 0.5f;
    const float MarkerThickness = 1f;

    public HeightMapPreviewControl()
    {
        InitializeComponent();
        GlViewport.SetBackground(ViewerThemeColors.Current().Background);
        ViewportInput.PointerPressed += (_, e) => GlViewport.HandlePointerPressed(e, ViewportInput);
        ViewportInput.PointerMoved += (_, e) => GlViewport.HandlePointerMoved(e, ViewportInput);
        ViewportInput.PointerReleased += (_, e) => GlViewport.HandlePointerReleased(e, ViewportInput);
        ViewportInput.PointerWheelChanged += (_, e) => GlViewport.HandlePointerWheelChanged(e, ViewportInput);
    }

    public void Render(Point3D[] boundary, Point3D[] gridPoints, int sizeX, int sizeY, HeightMapSurfaceData? surface)
    {
        var layers = new List<ViewerLineLayer>();

        void AddLayer(IReadOnlyList<System.Numerics.Vector3> points, Color color, float width)
        {
            var layer = ViewerLineLayerBuilder.FromPoints(points, color, width);
            if (layer != null)
                layers.Add(layer);
        }

        if (boundary.Length >= 2)
            AddLayer(HeightMapMeshBuilder.BuildBoundaryLines(boundary), Colors.Lime, BoundaryThickness);

        if (surface == null)
        {
            if (gridPoints.Length > 0 && sizeX >= 1 && sizeY >= 1)
            {
                AddLayer(HeightMapMeshBuilder.BuildPointMarkers(gridPoints), Colors.OrangeRed, MarkerThickness);
                if (sizeX >= 2 && sizeY >= 2)
                {
                    AddLayer(
                        HeightMapMeshBuilder.BuildGridLines(gridPoints, sizeX, sizeY),
                        AvaloniaColor.FromArgb(200, 120, 120, 120),
                        GridThickness);
                }
            }
        }
        else
        {
            AddLayer(HeightMapMeshBuilder.BuildSurfaceWireframe(surface), AvaloniaColor.FromArgb(220, 90, 180, 255), 1f);
        }

        var bounds = ComputeBounds(boundary, gridPoints);
        GlViewport.SetScene(new ViewerScene { ExtraLayers = layers }, bounds, resetView: true);
    }

    static PathBounds ComputeBounds(Point3D[] boundary, Point3D[] gridPoints)
    {
        var has = false;
        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var minZ = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;
        var maxZ = double.MinValue;

        void Include(Point3D p)
        {
            has = true;
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Z < minZ) minZ = p.Z;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
            if (p.Z > maxZ) maxZ = p.Z;
        }

        foreach (var p in boundary)
            Include(p);
        foreach (var p in gridPoints)
            Include(p);

        if (!has)
            return new PathBounds();

        return new PathBounds
        {
            MinX = minX,
            MinY = minY,
            MinZ = minZ,
            MaxX = maxX,
            MaxY = maxY,
            MaxZ = maxZ,
            HasValue = true,
        };
    }

    public void ClearScene() => GlViewport.ClearScene();
}
