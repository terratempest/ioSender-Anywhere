using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using CNC.App;
using CNC.GCodeViewer.Avalonia.OpenGl;
using NumericVector3 = System.Numerics.Vector3;

namespace CNC.GCodeViewer.Avalonia.Views;

/// <summary>Cross-platform OpenGL toolpath viewport (Windows + Linux).</summary>
public class ToolpathGlControl : OpenGlControlBase
{
    const int GlBlend = 0x0BE2;
    const int GlStencilTest = 0x0B90;
    const int GlSampleAlphaToCoverage = 0x809E;
    const int GlSampleCoverage = 0x80A0;

    readonly OpenGlLineRenderer _renderer = new();
    readonly ViewerCamera _camera = new();
    ViewerScene? _scene;
    bool _sceneGpuDirty = true;
    Color _backgroundColor = Color.FromRgb(16, 16, 16);
    bool _initFailed;
    string? _initFailureMessage;
    string? _renderFailureMessage;

    bool _isMiddlePanning;
    bool _isRightRotating;
    Point _lastPanPoint;
    PathBounds _bounds;

    DispatcherTimer? _animationTimer;

    public event EventHandler? InitializationFailed;
    public event EventHandler? RenderingFailed;
    public event EventHandler? SceneApplied;
    public event EventHandler? CameraChanged;

    public GCodeViewerConfig? ViewerConfig { get; set; }

    public ViewerCamera Camera => _camera;
    public string? InitializationFailureMessage => _initFailureMessage;
    public string? RenderFailureMessage => _renderFailureMessage;

    public ToolpathGlControl()
    {
        Focusable = true;
        IsHitTestVisible = false;
        AttachedToVisualTree += OnAttachedToVisualTreeHandler;
        DetachedFromVisualTree += OnDetachedFromVisualTreeHandler;
    }

    public void HandlePointerPressed(PointerPressedEventArgs e, Visual? inputElement = null) =>
        OnPointerPressed(inputElement ?? this, e);

    public void HandlePointerMoved(PointerEventArgs e, Visual? inputElement = null) =>
        OnPointerMoved(inputElement ?? this, e);

    public void HandlePointerReleased(PointerReleasedEventArgs e, Visual? inputElement = null) =>
        OnPointerReleased(inputElement ?? this, e);

    public void HandlePointerWheelChanged(PointerWheelEventArgs e, Visual? inputElement = null) =>
        OnPointerWheelChanged(inputElement ?? this, e);

    public void SetBackground(Color backgroundColor)
    {
        _backgroundColor = backgroundColor;
        RequestNextFrameRendering();
    }

    public void SetAnimationActive(bool active)
    {
        if (active)
            StartAnimationTimer();
        else
            StopAnimationTimer();
    }

    public void SetScene(ViewerScene? scene, PathBounds bounds, bool resetView = false)
    {
        _scene = scene;
        _bounds = bounds;
        _sceneGpuDirty = true;

        if (resetView && !bounds.IsEmpty)
        {
            _camera.ResetToTopView(bounds);
            NotifyCameraChanged();
        }

        RequestNextFrameRendering();
        SceneApplied?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateDynamicLayers(ViewerLineLayer? toolMarker, ViewerLineLayer? executed)
    {
        if (_scene == null)
            return;

        var markerChanged = !ReferenceEquals(_scene.ToolMarker, toolMarker);
        var executedChanged = !ReferenceEquals(_scene.Executed, executed);
        _scene.ToolMarker = toolMarker;
        _scene.Executed = executed;
        if (markerChanged || executedChanged)
            _sceneGpuDirty = true;
        RequestNextFrameRendering();
    }

    public void ClearScene()
    {
        _scene = null;
        _bounds = default;
        _sceneGpuDirty = true;
        RequestNextFrameRendering();
    }

    public void ResetView()
    {
        _camera.ResetToTopView(_bounds);
        NotifyCameraChanged();
        RequestNextFrameRendering();
    }

    public void OrientTo(NumericVector3 faceDirection)
    {
        _camera.OrientTo(faceDirection, _bounds);
        NotifyCameraChanged();
        RequestNextFrameRendering();
    }

    public void SaveCameraToConfig()
    {
        var cfg = ViewerConfig;
        if (cfg == null)
            return;
        _camera.SaveToConfig(cfg);
        if (cfg.ViewMode < 0)
            cfg.ViewMode = 0;
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        try
        {
            _initFailed = false;
            _initFailureMessage = null;
            _renderFailureMessage = null;
            _renderer.Initialize(gl, GlVersion);
            _sceneGpuDirty = true;
            gl.ClearColor(ToGl(_backgroundColor.R), ToGl(_backgroundColor.G), ToGl(_backgroundColor.B), ToGl(_backgroundColor.A));
            gl.Disable(GlConsts.GL_DEPTH_TEST);
            gl.Disable(GlConsts.GL_CULL_FACE);
            gl.Disable(GlConsts.GL_SCISSOR_TEST);
        }
        catch (Exception ex)
        {
            _initFailed = true;
            _initFailureMessage = ex.Message;
            Dispatcher.UIThread.Post(() => InitializationFailed?.Invoke(this, EventArgs.Empty));
        }
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _renderer.Deinitialize(gl);
        _renderer.Dispose();
        _sceneGpuDirty = true;
    }

    protected override void OnOpenGlLost()
    {
        _sceneGpuDirty = true;
        base.OnOpenGlLost();
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_initFailed)
            return;

        var size = Bounds;
        var w = Math.Max(1, (int)size.Width);
        var h = Math.Max(1, (int)size.Height);
        gl.Viewport(0, 0, w, h);

        ResetFrameState(gl);
        gl.ClearColor(ToGl(_backgroundColor.R), ToGl(_backgroundColor.G), ToGl(_backgroundColor.B), ToGl(_backgroundColor.A));
        gl.Clear(GlConsts.GL_COLOR_BUFFER_BIT);

        if (_scene == null)
            return;

        var layers = _scene.AllLayers().ToList();
        if (layers.Count == 0)
            return;

        if (_sceneGpuDirty)
        {
            if (_renderer.SetScene(gl, layers))
            {
                _sceneGpuDirty = false;
            }
            else
            {
                MarkRenderFailure(_renderer.LastError ?? "OpenGL scene upload failed");
                return;
            }
        }

        var mvp = _camera.GetMvpMatrix(w, h);
        if (!_renderer.Draw(gl, mvp))
        {
            MarkRenderFailure(_renderer.LastError ?? "OpenGL draw failed");
            return;
        }

        _renderFailureMessage = null;
    }

    static void ResetFrameState(GlInterface gl)
    {
        gl.Disable(GlConsts.GL_DEPTH_TEST);
        gl.Disable(GlConsts.GL_CULL_FACE);
        gl.Disable(GlConsts.GL_SCISSOR_TEST);
        gl.Disable(GlBlend);
        gl.Disable(GlStencilTest);
        gl.Disable(GlSampleAlphaToCoverage);
        gl.Disable(GlSampleCoverage);
    }

    static float ToGl(byte channel) => channel / 255f;

    void MarkRenderFailure(string message)
    {
        if (string.Equals(_renderFailureMessage, message, StringComparison.Ordinal))
            return;

        _renderFailureMessage = message;
        Dispatcher.UIThread.Post(() => RenderingFailed?.Invoke(this, EventArgs.Empty));
    }

    void OnAttachedToVisualTreeHandler(object? sender, VisualTreeAttachmentEventArgs e) =>
        StartAnimationTimer();

    void OnDetachedFromVisualTreeHandler(object? sender, VisualTreeAttachmentEventArgs e) =>
        StopAnimationTimer();

    void StartAnimationTimer()
    {
        _animationTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _animationTimer.Tick -= OnAnimationTick;
        _animationTimer.Tick += OnAnimationTick;
        if (!_animationTimer.IsEnabled)
            _animationTimer.Start();
    }

    void StopAnimationTimer()
    {
        if (_animationTimer == null)
            return;
        _animationTimer.Stop();
        _animationTimer.Tick -= OnAnimationTick;
    }

    void OnAnimationTick(object? sender, EventArgs e)
    {
        if (_initFailed)
            return;

        if (_scene != null)
            RequestNextFrameRendering();
    }

    void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var inputElement = sender as Visual ?? this;
        var point = e.GetCurrentPoint(inputElement);
        if (point.Properties.IsMiddleButtonPressed)
        {
            _isMiddlePanning = true;
            _lastPanPoint = point.Position;
            e.Pointer.Capture(inputElement as IInputElement);
            e.Handled = true;
        }
        else if (point.Properties.IsRightButtonPressed)
        {
            _isRightRotating = true;
            _lastPanPoint = point.Position;
            e.Pointer.Capture(inputElement as IInputElement);
            e.Handled = true;
        }
    }

    void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var inputElement = sender as Visual ?? this;
        var point = e.GetCurrentPoint(inputElement);
        if (_isMiddlePanning)
        {
            if (!point.Properties.IsMiddleButtonPressed)
            {
                _isMiddlePanning = false;
                e.Pointer.Capture(null);
                return;
            }

            _camera.PanPixels(point.Position - _lastPanPoint, Bounds.Width);
            _lastPanPoint = point.Position;
            NotifyCameraChanged();
            RequestNextFrameRendering();
            e.Handled = true;
            return;
        }

        if (_isRightRotating)
        {
            if (!point.Properties.IsRightButtonPressed)
            {
                _isRightRotating = false;
                e.Pointer.Capture(null);
                return;
            }

            _camera.RotatePixels(point.Position - _lastPanPoint);
            _lastPanPoint = point.Position;
            NotifyCameraChanged();
            RequestNextFrameRendering();
            e.Handled = true;
        }
    }

    void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var update = e.GetCurrentPoint(sender as Visual ?? this).Properties.PointerUpdateKind;
        if (update == PointerUpdateKind.MiddleButtonReleased)
        {
            _isMiddlePanning = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
        else if (update == PointerUpdateKind.RightButtonReleased)
        {
            _isRightRotating = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var inputElement = sender as Visual ?? this;
        var pointer = e.GetPosition(inputElement);
        _camera.ZoomWheel(e.Delta.Y, pointer, inputElement.Bounds.Width, inputElement.Bounds.Height);
        NotifyCameraChanged();
        RequestNextFrameRendering();
        e.Handled = true;
    }

    void NotifyCameraChanged() => CameraChanged?.Invoke(this, EventArgs.Empty);
}
