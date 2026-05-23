namespace CNC.GCodeViewer.Avalonia;

/// <summary>Probe grid heights for 3D mesh (decoupled from probing assembly).</summary>
public sealed class HeightMapSurfaceData
{
    public int SizeX { get; init; }
    public int SizeY { get; init; }
    public double MinX { get; init; }
    public double MinY { get; init; }
    public double DeltaX { get; init; }
    public double DeltaY { get; init; }
    public double MinHeight { get; init; }
    public double MaxHeight { get; init; }
    public double?[,] Points { get; init; } = null!;
}
