using Avalonia.Media;
using HelixToolkit.Avalonia.SharpDX;
using HelixToolkit.SharpDX;

namespace CNC.GCodeViewer.Avalonia;

/// <summary>Shared SharpDX resources required before Viewport3DX can render.</summary>
public static class HelixViewportHost
{
    static DefaultEffectsManager? _effects;

    public static IEffectsManager EffectsManager =>
        _effects ??= new DefaultEffectsManager();

    public static void Configure(Viewport3DX viewport)
    {
        if (viewport.EffectsManager == null)
            viewport.EffectsManager = EffectsManager;

        viewport.Orthographic = true;

        if (viewport.Camera is not OrthographicCamera ortho)
        {
            ortho = new OrthographicCamera();
            CameraExtensions.Reset(ortho);
            viewport.Camera = ortho;
        }
    }

    public static void ApplyBackground(Viewport3DX viewport, bool blackBackground)
    {
        var color = blackBackground ? Colors.Black : Colors.White;
        viewport.Background = new SolidColorBrush(color);
        viewport.BackgroundColor = color;
    }

    public static void ShutdownResources()
    {
        _effects?.Dispose();
        _effects = null;
    }
}
