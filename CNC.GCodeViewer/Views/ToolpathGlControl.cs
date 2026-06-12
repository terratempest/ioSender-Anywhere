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
    const double MinTouchDistance = 1d;

    readonly OpenGlLineRenderer _renderer = new();
    readonly ViewerCamera _camera = new();
    readonly Dictionary<IPointer, Point> _activeTouches = [];
    ViewerScene? _scene;
    bool _sceneGpuDirty = true;
    bool _dynamicGpuDirty = true;
    Color _backgroundColor = Color.FromRgb(16, 16, 16);
    bool _initFailed;
    string? _initFailureMessage;
    string? _renderFailureMessage;

    bool _isMiddlePanning;
    bool _isRightRotating;
    Point _lastPanPoint;
    Point? _lastTouchMidpoint;
    double? _lastTouchDistance;
    PathBounds _bounds;

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
    }

    public void HandlePointerPressed(PointerPressedEventArgs e, Visual? inputElement = null) =>
        OnPointerPressed(inputElement ?? this, e);

    public void HandlePointerMoved(PointerEventArgs e, Visual? inputElement = null) =>
        OnPointerMoved(inputElement ?? this, e);

    public void HandlePointerReleased(PointerReleasedEventArgs e, Visual? inputElement = null) =>
        OnPointerReleased(inputElement ?? this, e);

    public void HandlePointerCaptureLost(PointerCaptureLostEventArgs e) =>
        ProcessPointerCaptureLost(e);

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
            RequestNextFrameRendering();
    }

    public void SetScene(ViewerScene? scene, PathBounds bounds, bool resetView = false)
    {
        _scene = scene;
        _bounds = bounds;
        _sceneGpuDirty = true;
        _dynamicGpuDirty = true;

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
            _dynamicGpuDirty = true;
        RequestNextFrameRendering();
    }

    public void ClearScene()
    {
        _scene = null;
        _bounds = default;
        _sceneGpuDirty = true;
        _dynamicGpuDirty = true;
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
            _dynamicGpuDirty = true;
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
        _dynamicGpuDirty = true;
    }

    protected override void OnOpenGlLost()
    {
        _sceneGpuDirty = true;
        _dynamicGpuDirty = true;
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

        var staticLayers = StaticLayers(_scene).ToList();
        var dynamicLayers = DynamicLayers(_scene).ToList();
        if (staticLayers.Count == 0 && dynamicLayers.Count == 0)
            return;

        if (_sceneGpuDirty)
        {
            if (_renderer.SetScene(gl, staticLayers, dynamicLayers))
            {
                _sceneGpuDirty = false;
                _dynamicGpuDirty = false;
            }
            else
            {
                MarkRenderFailure(_renderer.LastError ?? "OpenGL scene upload failed");
                return;
            }
        }
        else if (_dynamicGpuDirty)
        {
            if (_renderer.SetDynamicLayers(gl, dynamicLayers))
            {
                _dynamicGpuDirty = false;
            }
            else
            {
                MarkRenderFailure(_renderer.LastError ?? "OpenGL dynamic layer upload failed");
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

    static IEnumerable<ViewerLineLayer> StaticLayers(ViewerScene scene)
    {
        if (scene.Grid != null) yield return scene.Grid;
        if (scene.GridMajor != null) yield return scene.GridMajor;
        if (scene.Cut != null) yield return scene.Cut;
        if (scene.Rapid != null) yield return scene.Rapid;
        if (scene.Retract != null) yield return scene.Retract;
        if (scene.JobBox != null) yield return scene.JobBox;
        if (scene.WorkBox != null) yield return scene.WorkBox;
        if (scene.ViewCube != null) yield return scene.ViewCube;
        foreach (var axis in scene.OriginAxes)
            yield return axis;
        foreach (var extra in scene.ExtraLayers)
            yield return extra;
    }

    static IEnumerable<ViewerLineLayer> DynamicLayers(ViewerScene scene)
    {
        if (scene.Executed != null) yield return scene.Executed;
        if (scene.ToolMarker != null) yield return scene.ToolMarker;
    }

    static float ToGl(byte channel) => channel / 255f;

    void MarkRenderFailure(string message)
    {
        if (string.Equals(_renderFailureMessage, message, StringComparison.Ordinal))
            return;

        _renderFailureMessage = message;
        Dispatcher.UIThread.Post(() => RenderingFailed?.Invoke(this, EventArgs.Empty));
    }

    void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var inputElement = sender as Visual ?? this;
        var point = e.GetCurrentPoint(inputElement);
        if (e.Pointer.Type == PointerType.Touch)
        {
            _activeTouches[e.Pointer] = point.Position;
            ResetTouchGestureReference();
            e.Pointer.Capture(inputElement as IInputElement);
            e.Handled = true;
            return;
        }

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
        if (e.Pointer.Type == PointerType.Touch)
        {
            if (!_activeTouches.ContainsKey(e.Pointer))
                return;

            _activeTouches[e.Pointer] = point.Position;
            ApplyTouchGesture(inputElement.Bounds.Width, inputElement.Bounds.Height);
            e.Handled = true;
            return;
        }

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
        if (e.Pointer.Type == PointerType.Touch)
        {
            if (_activeTouches.Remove(e.Pointer))
            {
                ResetTouchGestureReference();
                e.Pointer.Capture(null);
                e.Handled = true;
            }

            return;
        }

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

    void ProcessPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Touch)
            return;

        if (_activeTouches.Remove(e.Pointer))
            ResetTouchGestureReference();
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

    void ApplyTouchGesture(double viewportWidth, double viewportHeight)
    {
        if (_activeTouches.Count == 0)
        {
            ResetTouchGestureReference();
            return;
        }

        if (_activeTouches.Count == 1)
        {
            var current = _activeTouches.Values.First();
            if (_lastTouchMidpoint.HasValue)
            {
                _camera.RotatePixels(current - _lastTouchMidpoint.Value);
                NotifyCameraChanged();
                RequestNextFrameRendering();
            }

            _lastTouchMidpoint = current;
            _lastTouchDistance = null;
            return;
        }

        var touches = _activeTouches.Values.Take(2).ToArray();
        var midpoint = new Point(
            (touches[0].X + touches[1].X) * 0.5d,
            (touches[0].Y + touches[1].Y) * 0.5d);
        var distance = Distance(touches[0], touches[1]);
        var changed = false;

        if (_lastTouchMidpoint.HasValue)
        {
            _camera.PanPixels(midpoint - _lastTouchMidpoint.Value, viewportWidth);
            changed = true;
        }

        if (_lastTouchDistance is > MinTouchDistance && distance > MinTouchDistance)
        {
            _camera.ZoomAtScale(_lastTouchDistance.Value / distance, midpoint, viewportWidth, viewportHeight);
            changed = true;
        }

        _lastTouchMidpoint = midpoint;
        _lastTouchDistance = distance;

        if (!changed)
            return;

        NotifyCameraChanged();
        RequestNextFrameRendering();
    }

    void ResetTouchGestureReference()
    {
        _lastTouchMidpoint = null;
        _lastTouchDistance = null;
    }

    static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    void NotifyCameraChanged() => CameraChanged?.Invoke(this, EventArgs.Empty);
}
