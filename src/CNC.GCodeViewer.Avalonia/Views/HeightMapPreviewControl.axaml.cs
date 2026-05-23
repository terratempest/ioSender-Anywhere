using Avalonia.Controls;
using Avalonia.Media;
using CNC.Core.Geometry;
using HelixToolkit.Avalonia.SharpDX;
using HelixToolkit.SharpDX;
using AvaloniaColor = Avalonia.Media.Color;

namespace CNC.GCodeViewer.Avalonia.Views;

public partial class HeightMapPreviewControl : UserControl
{
    readonly List<LineGeometryModel3D> _lineModels = [];

    const float BoundaryThickness = 1.25f;
    const float GridThickness = 0.5f;
    const float MarkerThickness = 1f;

    public HeightMapPreviewControl()
    {
        HelixThemeLoader.EnsureApplied();
        InitializeComponent();
        AttachedToVisualTree += (_, _) => ApplyViewportChrome();
    }

    public void Render(Point3D[] boundary, Point3D[] gridPoints, int sizeX, int sizeY, HeightMapSurfaceData? surface)
    {
        ClearScene();
        ApplyViewportChrome();

        if (boundary.Length >= 2)
        {
            var boundaryLines = HeightMapMeshBuilder.BuildBoundaryLines(boundary);
            if (boundaryLines.Count > 1)
            {
                HelixLineHelper.AddLineModel(
                    Viewport,
                    _lineModels,
                    HelixLineHelper.CreateLineModel(boundaryLines, Colors.Lime, BoundaryThickness));
            }
        }

        if (surface == null)
        {
            if (gridPoints.Length > 0 && sizeX >= 1 && sizeY >= 1)
            {
                var markers = HeightMapMeshBuilder.BuildPointMarkers(gridPoints);
                if (markers.Count > 1)
                {
                    HelixLineHelper.AddLineModel(
                        Viewport,
                        _lineModels,
                        HelixLineHelper.CreateLineModel(markers, Colors.OrangeRed, MarkerThickness));
                }

                if (sizeX >= 2 && sizeY >= 2)
                {
                    var gridLines = HeightMapMeshBuilder.BuildGridLines(gridPoints, sizeX, sizeY);
                    if (gridLines.Count > 1)
                    {
                        HelixLineHelper.AddLineModel(
                            Viewport,
                            _lineModels,
                            HelixLineHelper.CreateLineModel(gridLines, AvaloniaColor.FromArgb(200, 120, 120, 120), GridThickness));
                    }
                }
            }
        }
        else
        {
            var surfaceLines = HeightMapMeshBuilder.BuildSurfaceWireframe(surface);
            if (surfaceLines.Count > 1)
            {
                HelixLineHelper.AddLineModel(
                    Viewport,
                    _lineModels,
                    HelixLineHelper.CreateLineModel(surfaceLines, AvaloniaColor.FromArgb(220, 90, 180, 255), 1f));
            }

            if (boundary.Length >= 2)
            {
                var boundaryLines = HeightMapMeshBuilder.BuildBoundaryLines(boundary);
                if (boundaryLines.Count > 1)
                {
                    HelixLineHelper.AddLineModel(
                        Viewport,
                        _lineModels,
                        HelixLineHelper.CreateLineModel(boundaryLines, Colors.Lime, BoundaryThickness));
                }
            }
        }

        if (_lineModels.Count > 0)
            Viewport.ZoomExtents();
    }

    public void ClearScene()
    {
        HelixLineHelper.ClearManaged(Viewport, _lineModels);
    }

    void ApplyViewportChrome()
    {
        HelixViewportHost.Configure(Viewport);
        HelixViewportHost.ApplyBackground(Viewport, blackBackground: true);
        Viewport.ShowCoordinateSystem = false;
        Viewport.ShowViewCube = false;
    }
}
