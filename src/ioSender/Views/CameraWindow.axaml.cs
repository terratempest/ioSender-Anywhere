using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CNC.Platform.Abstractions;
using CNC.Platform.Camera.OpenCv;
using ioSender.Services;
using PixelFormat = Avalonia.Platform.PixelFormat;

namespace ioSender.Views;

public partial class CameraWindow : Window
{
    readonly OpenCvCameraCapture _capture = new();
    WriteableBitmap? _bitmap;

    public CameraWindow()
    {
        InitializeComponent();
        RefreshBtn.Click += (_, _) => RefreshDevices();
        StartBtn.Click += (_, _) => StartCapture();
        StopBtn.Click += (_, _) => StopCapture();
        Closing += (_, _) => Cleanup();
        _capture.FrameCaptured += OnFrameCaptured;
        Loaded += (_, _) => RefreshDevices();
    }

    void RefreshDevices()
    {
        DeviceCombo.Items.Clear();
        foreach (var id in _capture.ListDevices())
            DeviceCombo.Items.Add(id);

        if (DeviceCombo.Items.Count > 0 && DeviceCombo.SelectedIndex < 0)
            DeviceCombo.SelectedIndex = 0;

        StatusText.Text = DeviceCombo.Items.Count == 0
            ? "No cameras detected."
            : $"{DeviceCombo.Items.Count} camera(s).";
    }

    void StartCapture()
    {
        var id = DeviceCombo.SelectedItem?.ToString() ?? "0";
        try
        {
            var camera = AppHostContext.AppConfig.Base.Camera;
            _capture.Start(id, camera.CaptureWidth, camera.CaptureHeight);
            StartBtn.IsEnabled = false;
            StopBtn.IsEnabled = true;
            StatusText.Text = $"Streaming device {id}.";
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }

    void StopCapture()
    {
        _capture.Stop();
        StartBtn.IsEnabled = true;
        StopBtn.IsEnabled = false;
        PreviewImage.Source = null;
        _bitmap = null;
        StatusText.Text = "Stopped.";
    }

    void Cleanup()
    {
        _capture.FrameCaptured -= OnFrameCaptured;
        StopCapture();
        _capture.Dispose();
    }

    void OnFrameCaptured(object? sender, CameraFrameEventArgs e)
    {
        Dispatcher.UIThread.Post(() => ApplyFrame(e), DispatcherPriority.Render);
    }

    void ApplyFrame(CameraFrameEventArgs e)
    {
        if (e.Width <= 0 || e.Height <= 0 || e.Pixels.Length < e.Width * e.Height * 4)
            return;

        if (_bitmap == null
            || _bitmap.PixelSize.Width != e.Width
            || _bitmap.PixelSize.Height != e.Height)
        {
            _bitmap = new WriteableBitmap(
                new PixelSize(e.Width, e.Height),
                new Avalonia.Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Unpremul);
            PreviewImage.Source = _bitmap;
        }

        using var fb = _bitmap.Lock();
        var expected = fb.RowBytes * e.Height;
        if (e.Pixels.Length >= expected)
            Marshal.Copy(e.Pixels, 0, fb.Address, expected);
        else
        {
            var row = e.Width * 4;
            for (var y = 0; y < e.Height; y++)
                Marshal.Copy(e.Pixels, y * row, fb.Address + y * fb.RowBytes, row);
        }
    }
}
