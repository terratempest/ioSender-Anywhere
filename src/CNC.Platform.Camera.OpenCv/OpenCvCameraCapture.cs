using System.Runtime.InteropServices;

using CNC.Platform.Abstractions;

using OpenCvSharp;



namespace CNC.Platform.Camera.OpenCv;



/// <summary>OpenCV VideoCapture → BGRA frames.</summary>

public sealed class OpenCvCameraCapture : ICameraCapture

{

    VideoCapture? _capture;

    CancellationTokenSource? _cts;

    Task? _loop;



    public event EventHandler<CameraFrameEventArgs>? FrameCaptured;



    public void Dispose()

    {

        Stop();

        _capture?.Dispose();

        _capture = null;

        GC.SuppressFinalize(this);

    }



    public IReadOnlyList<string> ListDevices()

    {

        var ids = new List<string>();

        for (var i = 0; i < 8; i++)

        {

            using var cap = new VideoCapture(i);

            if (cap.IsOpened())

                ids.Add(i.ToString());

        }



        return ids;

    }



    public void Start(string deviceId) => Start(deviceId, 0, 0);

    public void Start(string deviceId, int captureWidth, int captureHeight)

    {

        Stop();



        var index = int.TryParse(deviceId, out var ix) ? ix : 0;

        _capture = new VideoCapture(index);

        if (!_capture.IsOpened())

        {

            _capture.Dispose();

            _capture = null;

            throw new InvalidOperationException($"Cannot open camera '{deviceId}'.");

        }



        if (captureWidth > 0)

            _capture.Set(VideoCaptureProperties.FrameWidth, captureWidth);

        if (captureHeight > 0)

            _capture.Set(VideoCaptureProperties.FrameHeight, captureHeight);



        _cts = new CancellationTokenSource();

        var token = _cts.Token;

        _loop = Task.Run(() => CaptureLoop(token), token);

    }



    public void Stop()

    {

        _cts?.Cancel();

        try

        {

            _loop?.Wait(TimeSpan.FromSeconds(2));

        }

        catch (AggregateException)

        {

            /* ignore */

        }



        _loop = null;

        _cts?.Dispose();

        _cts = null;



        _capture?.Release();

        _capture?.Dispose();

        _capture = null;

    }



    void CaptureLoop(CancellationToken ct)

    {

        var cap = _capture ?? throw new InvalidOperationException("No capture.");

        using var frame = new Mat();



        while (!ct.IsCancellationRequested)

        {

            if (!cap.Read(frame) || frame.Empty())

            {

                Thread.Sleep(5);

                continue;

            }



            Cv2.CvtColor(frame, frame, ColorConversionCodes.BGR2BGRA);

            var w = frame.Width;

            var h = frame.Height;

            var len = w * h * 4;

            var pixels = new byte[len];



            // Mat may include stride; copy row-by-row if Mat.Step != width*4

            var srcStride = (int)frame.Step();

            var rowBytes = w * 4;

            if (srcStride == rowBytes)

            {

                Marshal.Copy(frame.Data, pixels, 0, len);

            }

            else

            {

                var ptr = frame.Data;

                var dst = 0;

                for (var y = 0; y < h; y++)

                {

                    Marshal.Copy(IntPtr.Add(ptr, y * srcStride), pixels, dst, rowBytes);

                    dst += rowBytes;

                }

            }



            var handler = FrameCaptured;

            if (handler != null)

            {

                handler(this, new CameraFrameEventArgs

                {

                    Width = w,

                    Height = h,

                    Pixels = pixels

                });

            }

        }

    }

}


