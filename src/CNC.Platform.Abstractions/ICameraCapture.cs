namespace CNC.Platform.Abstractions;



/// <summary>Cross-platform camera preview (OpenCV backends on Windows/Linux).</summary>

public interface ICameraCapture : IDisposable

{

    /// <summary>Fires on a background thread while capturing.</summary>

    event EventHandler<CameraFrameEventArgs>? FrameCaptured;



    /// <summary>Returns device ids accepted by <see cref="Start"/> (typically "0","1",…).</summary>

    IReadOnlyList<string> ListDevices();



    /// <summary>Opens capture (device id from <see cref="ListDevices"/> or numeric index string).</summary>

    void Start(string deviceId);



    void Stop();

}


