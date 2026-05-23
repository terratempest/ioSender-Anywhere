namespace CNC.Platform.Abstractions;



/// <summary>BGRA8888 top-down frame (e.g. for WriteableBitmap).</summary>

public sealed class CameraFrameEventArgs : EventArgs

{

    public required int Width { get; init; }

    public required int Height { get; init; }

    /// <summary>BGRA8888, length Width * Height * 4.</summary>

    public required byte[] Pixels { get; init; }

}


