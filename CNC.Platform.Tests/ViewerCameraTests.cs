using Avalonia;
using CNC.GCodeViewer.Avalonia.OpenGl;

namespace CNC.Platform.Tests;

public class ViewerCameraTests
{
    [Fact]
    public void ZoomAtScale_LessThanOne_ReducesViewWidth()
    {
        var camera = CreateCamera();

        camera.ZoomAtScale(0.5d, new Point(50d, 50d), 100d, 100d);

        Assert.Equal(50d, camera.ViewWidth, 6);
    }

    [Fact]
    public void ZoomAtScale_GreaterThanOne_IncreasesViewWidth()
    {
        var camera = CreateCamera();

        camera.ZoomAtScale(2d, new Point(50d, 50d), 100d, 100d);

        Assert.Equal(200d, camera.ViewWidth, 6);
    }

    [Fact]
    public void ZoomAtScale_OffCenterAnchor_MovesCameraTowardAnchor()
    {
        var camera = CreateCamera();

        camera.ZoomAtScale(0.5d, new Point(75d, 50d), 100d, 100d);

        Assert.True(camera.Position.X > 0f);
        Assert.Equal(0f, camera.Position.Y, 6);
        Assert.Equal(100f, camera.Position.Z, 6);
    }

    [Fact]
    public void PanAndZoom_KeepCameraValid()
    {
        var camera = CreateCamera();

        camera.PanPixels(new Vector(20d, -10d), 100d);
        camera.ZoomAtScale(0.75d, new Point(40d, 60d), 100d, 100d);

        Assert.True(camera.IsValid());
        Assert.InRange(camera.ViewWidth, 0.01d, 1_000_000d);
    }

    static ViewerCamera CreateCamera() => new()
    {
        Position = new System.Numerics.Vector3(0f, 0f, 100f),
        LookDirection = new System.Numerics.Vector3(0f, 0f, -100f),
        UpDirection = new System.Numerics.Vector3(0f, 1f, 0f),
        ViewWidth = 100d,
    };
}
