using System.ComponentModel;

using Avalonia;

using Avalonia.Controls;

using Avalonia.Input;

using Avalonia.Interactivity;

using Avalonia.Media;

using Avalonia.Threading;

using CNC.App;

using CNC.Core;

using CNC.Core.Geometry;

using CNC.GCode;

using HelixToolkit.Avalonia.SharpDX;

using HelixToolkit.SharpDX;

using NumericVector3 = System.Numerics.Vector3;



namespace CNC.GCodeViewer.Avalonia.Views;



public partial class RenderControl : UserControl

{

    const float CutThickness = 1.5f;

    const float RapidThickness = 0.75f;

    const float GridThickness = 0.5f;

    const float MajorGridThickness = 1.1f;

    const float EnvelopeThickness = 1f;

    const float ToolThickness = 1.25f;

    const float AxisThickness = 2f;

    const int MaxRenderHostWaitTicks = 80;



    readonly List<LineGeometryModel3D> _lineModels = [];

    Viewport3DX? _viewport;

    GCodePathSegments? _segments;

    PathBounds _bounds;

    IReadOnlyList<GCodeToken>? _tokens;

    GCodeViewerConfig? _subscribedConfig;

    bool _renderPending;

    bool _viewportInitScheduled;

    bool _viewportInitFailed;

    bool _pathBuildInProgress;

    CancellationTokenSource? _buildCts;

    int _buildVersion;

    bool _isMiddlePanning;

    Point _lastPanPoint;

    bool _isRightRotating;

    Point _lastRotatePoint;

    string _statusText = "";

    DispatcherTimer? _renderWaitTimer;

    int _renderWaitTicks;



    public RenderControl()

    {

        InitializeComponent();

    }



    public void Close()

    {

        _tokens = null;

        _segments = null;

        _bounds = default;

        _renderPending = false;

        CancelPathBuild();

        if (_viewport != null)

            ClearScene();

        SetStatus("3D view — no program loaded");

    }



    public void Open(IReadOnlyList<GCodeToken> tokens, Point3D? start = null)

    {

        _tokens = tokens as GCodeToken[] ?? tokens.ToArray();

        RequestRender(start);

    }



    public void TryLoadProgram()

    {

        if (!GCodeViewerContext.Settings.IsEnabled)

        {

            Close();

            SetStatus("3D view disabled in App Settings → G-code viewer");

            return;

        }



        var tokens = GCodeViewerContext.GetProgramTokens?.Invoke();

        if (tokens is { Count: > 0 })

            Open(tokens);

        else

            Close();

    }



    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)

    {

        base.OnAttachedToVisualTree(e);

        SubscribeConfig();

        SubscribeGrbl();

        ScheduleViewportInit();

    }



    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)

    {

        StopRenderWaitTimer();

        CancelPathBuild();

        UnsubscribeConfig();

        UnsubscribeGrbl();

        base.OnDetachedFromVisualTree(e);

    }



    void ScheduleViewportInit()

    {

        if (_viewport != null || _viewportInitFailed || _viewportInitScheduled)

            return;



        _viewportInitScheduled = true;

        Dispatcher.UIThread.Post(() =>

        {

            _viewportInitScheduled = false;

            if (!TryEnsureViewport())

                return;

            ApplyViewportChrome();

            _viewport!.SizeChanged += OnViewportSizeChanged;

            TryLoadProgram();

        }, DispatcherPriority.Loaded);

    }



    void OnViewportSizeChanged(object? sender, SizeChangedEventArgs e)

    {

        if (_renderPending && e.NewSize.Width >= 4 && e.NewSize.Height >= 4)

            TryFlushRender();

    }



    bool TryEnsureViewport()

    {

        if (_viewport != null)

            return true;

        if (_viewportInitFailed)

            return false;



        try

        {

            HelixThemeLoader.EnsureApplied();



            _viewport = new Viewport3DX

            {

                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,

                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch,

                Orthographic = true,

                Background = new SolidColorBrush(Color.Parse("#101010")),

                ShowViewCube = false,

                ShowCoordinateSystem = false,

                ModelUpDirection = new NumericVector3(0f, 0f, 1f),

                RotateAroundMouseDownPoint = true,

                ZoomAroundMouseDownPoint = true,

            };

            _viewport.OnRendered += OnViewportRendered;

            _viewport.AddHandler(PointerPressedEvent, OnViewportPointerPressed, RoutingStrategies.Tunnel, true);

            _viewport.AddHandler(PointerMovedEvent, OnViewportPointerMoved, RoutingStrategies.Tunnel, true);

            _viewport.AddHandler(PointerReleasedEvent, OnViewportPointerReleased, RoutingStrategies.Tunnel, true);

            _viewport.AddHandler(PointerWheelChangedEvent, OnViewportPointerWheelChanged, RoutingStrategies.Tunnel);

            _viewport.Items.Add(new AmbientLight3D());



            ViewportHost.Children.Insert(0, _viewport);

            HelixViewportHost.Configure(_viewport);

            SetStatus("3D view — ready");

            return true;

        }

        catch (Exception ex)

        {

            _viewportInitFailed = true;

            SetStatus($"3D view unavailable: {ex.GetType().Name}");

            return false;

        }

    }



    void RequestRender(Point3D? start = null)

    {

        _renderStartOverride = start;

        _renderPending = true;

        if (_pathBuildInProgress)

            CancelPathBuild();

        if (!TryEnsureViewport())

            return;

        TryFlushRender();

        if (_renderPending)

            StartRenderWaitTimer();

    }



    Point3D? _renderStartOverride;



    void TryFlushRender()

    {

        if (!_renderPending || _tokens == null || _viewport == null)

            return;



        if (_pathBuildInProgress)

            CancelPathBuild();



        HelixViewportHost.Configure(_viewport);

        if (_viewport.EffectsManager == null)

        {

            SetStatus("3D view — graphics not ready");

            return;

        }



        if (_viewport.RenderHost == null)

        {

            SetStatus("3D view — initializing renderer…");

            return;

        }



        var viewportSize = _viewport.Bounds;

        if (viewportSize.Width < 4 || viewportSize.Height < 4)

        {

            SetStatus($"3D view — sizing ({viewportSize.Width:F0}×{viewportSize.Height:F0})");

            return;

        }



        StopRenderWaitTimer();

        var tokens = _tokens;

        var tokenCount = tokens.Count;

        var start = _renderStartOverride ?? ViewerToolMarker.GetToolPosition();

        _renderStartOverride = null;

        CancelPathBuild();

        _pathBuildInProgress = true;

        _buildCts = new CancellationTokenSource();

        var cts = _buildCts;

        var ct = cts.Token;

        var buildVersion = ++_buildVersion;

        SetStatus($"3D view — building toolpath ({tokenCount:N0} tokens)…");



        var buildTask = Task.Run(() =>

        {

            try

            {

                var cfg = GCodeViewerContext.Settings;

                var built = GCodePathBuilder.Build(tokens, start, cfg.ArcResolution, cfg.MinDistance, ct);

                var bounds = PathBounds.FromSegments(built.Segments);

                Dispatcher.UIThread.Post(() =>

                {

                    if (!IsCurrentBuild(buildVersion, tokens) || _viewport == null)

                        return;

                    try

                    {

                        var meshes = ToolpathSceneBuilder.Build(built.Segments, bounds, GCodeViewerContext.Settings);

                        if (!IsCurrentBuild(buildVersion, tokens) || _viewport == null)

                            return;

                        _pathBuildInProgress = false;

                        if (ReferenceEquals(_buildCts, cts))

                            _buildCts = null;

                        if (!_renderPending)

                            return;



                        _renderPending = false;

                        ApplyScene(built.Segments, bounds, built.MotionCount, meshes);

                    }

                    catch (OperationCanceledException)

                    {

                        if (IsCurrentBuild(buildVersion, tokens))

                            _pathBuildInProgress = false;

                    }

                    catch (Exception ex)

                    {

                        if (!IsCurrentBuild(buildVersion, tokens))

                            return;

                        _pathBuildInProgress = false;

                        _renderPending = false;

                        if (ReferenceEquals(_buildCts, cts))

                            _buildCts = null;

                        SetStatus($"3D view — build failed: {ex.GetType().Name}");

                    }

                }, DispatcherPriority.Background);

            }

            catch (OperationCanceledException)

            {

                Dispatcher.UIThread.Post(() =>

                {

                    if (IsCurrentBuild(buildVersion, tokens))

                        _pathBuildInProgress = false;

                });

            }

            catch (Exception ex)

            {

                Dispatcher.UIThread.Post(() =>

                {

                    if (!IsCurrentBuild(buildVersion, tokens))

                        return;

                    _pathBuildInProgress = false;

                    _renderPending = false;

                    if (ReferenceEquals(_buildCts, cts))

                        _buildCts = null;

                    SetStatus($"3D view — build failed: {ex.GetType().Name}");

                });

            }

        }, ct);

        _ = buildTask.ContinueWith(_ => cts.Dispose(), TaskScheduler.Default);

    }



    void CancelPathBuild()

    {

        _buildVersion++;

        _pathBuildInProgress = false;

        if (_buildCts == null)

            return;

        _buildCts.Cancel();

        _buildCts = null;

    }

    bool IsCurrentBuild(int buildVersion, IReadOnlyList<GCodeToken> tokens) =>
        buildVersion == _buildVersion && ReferenceEquals(_tokens, tokens);



    void ApplyScene(

        GCodePathSegments segments,

        PathBounds bounds,

        int motionCount,

        ToolpathSceneBuilder.SceneMeshes meshes)

    {

        if (_viewport == null)

            return;



        ClearScene();

        ApplyViewportChrome();



        _segments = segments;

        _bounds = bounds;



        AttachMesh(meshes.Cut);

        AttachMesh(meshes.Rapid);

        AttachMesh(meshes.Retract);

        AttachMesh(meshes.Grid);

        AttachMesh(meshes.GridMajor);

        AttachMesh(meshes.JobBox);

        AttachMesh(meshes.WorkBox);

        AddOriginAxes(GCodeViewerContext.Settings);



        UpdateToolMarker();



        var pointCount = segments.Cut.Count + segments.Rapid.Count + segments.Retract.Count;



        if (meshes.LayerCount == 0)

        {

            SetStatus($"3D view — {motionCount} motions, 0 path points");

            ResetToTopView(bounds);

            return;

        }



        SetStatus($"3D view — {motionCount:N0} motions, {pointCount:N0} preview points, {meshes.LayerCount} layers");

        OverlayText.Text = $"Program: {motionCount:N0} motion commands";



        _viewport.InvalidateSceneGraph();

        _viewport.InvalidateRender();

        Dispatcher.UIThread.Post(() => ResetToTopView(_bounds), DispatcherPriority.Render);

    }



    void AttachMesh(LineGeometryModel3D? model)

    {

        if (model == null || _viewport == null)

            return;

        HelixLineHelper.AddLineModel(_viewport, _lineModels, model);

    }



    void StartRenderWaitTimer()

    {

        _renderWaitTicks = 0;

        _renderWaitTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };

        _renderWaitTimer.Tick -= OnRenderWaitTick;

        _renderWaitTimer.Tick += OnRenderWaitTick;

        if (!_renderWaitTimer.IsEnabled)

            _renderWaitTimer.Start();

    }



    void StopRenderWaitTimer()

    {

        if (_renderWaitTimer == null)

            return;

        _renderWaitTimer.Stop();

        _renderWaitTimer.Tick -= OnRenderWaitTick;

    }



    void OnRenderWaitTick(object? sender, EventArgs e)

    {

        if (!_renderPending)

        {

            StopRenderWaitTimer();

            return;

        }



        _renderWaitTicks++;

        TryFlushRender();



        if (!_renderPending)

            StopRenderWaitTimer();

        else if (_renderWaitTicks >= MaxRenderHostWaitTicks)

        {

            StopRenderWaitTimer();

            _renderPending = false;

            SetStatus("3D view — renderer did not start (check GPU / drivers)");

        }

    }



    void SetStatus(string text)

    {

        if (string.Equals(_statusText, text, StringComparison.Ordinal))

            return;

        _statusText = text;

        StatusText.Text = text;

    }



    void AddPathLines(GCodeViewerConfig cfg)

    {

        if (_segments == null || _viewport == null)

            return;



        if (_segments.Cut.Count > 1)

        {

            HelixLineHelper.AddLineModel(

                _viewport,

                _lineModels,

                HelixLineHelper.CreateLineModel(_segments.Cut, ViewerColors.ResolveCutColor(cfg), CutThickness));

        }



        if (_segments.Rapid.Count > 1)

        {

            HelixLineHelper.AddLineModel(

                _viewport,

                _lineModels,

                HelixLineHelper.CreateLineModel(_segments.Rapid, ViewerColors.ResolveRapidColor(cfg), RapidThickness));

        }



        if (_segments.Retract.Count > 1)

        {

            HelixLineHelper.AddLineModel(

                _viewport,

                _lineModels,

                HelixLineHelper.CreateLineModel(_segments.Retract, ViewerColors.ResolveRetractColor(cfg), RapidThickness));

        }

    }



    void AddAdorners(GCodeViewerConfig cfg)

    {

        if (_segments == null || _bounds.IsEmpty || _viewport == null)

            return;



        if (cfg.ShowGrid)

        {

            var grid = ViewerGridBuilder.Build(_bounds);

            var gridColor = ViewerColors.ResolveGridColor(cfg);

            if (grid.Minor.Count > 1)

            {

                HelixLineHelper.AddLineModel(

                    _viewport,

                    _lineModels,

                    HelixLineHelper.CreateLineModel(grid.Minor, gridColor, GridThickness));

            }

            if (grid.Major.Count > 1)

            {

                HelixLineHelper.AddLineModel(

                    _viewport,

                    _lineModels,

                    HelixLineHelper.CreateLineModel(grid.Major, gridColor, MajorGridThickness));

            }

        }



        if (cfg.ShowBoundingBox)

        {

            var job = ViewerEnvelopeBuilder.JobBox(_bounds);

            if (job.Count > 1)

            {

                HelixLineHelper.AddLineModel(

                    _viewport,

                    _lineModels,

                    HelixLineHelper.CreateLineModel(job, cfg.HighlightColor.ToColor(), EnvelopeThickness));

            }

        }



        if (cfg.ShowWorkEnvelope)

        {

            var work = ViewerEnvelopeBuilder.WorkAreaBox();

            if (work.Count > 1)

            {

                HelixLineHelper.AddLineModel(

                    _viewport,

                    _lineModels,

                    HelixLineHelper.CreateLineModel(work, Colors.DarkBlue, EnvelopeThickness));

            }

        }



        AddOriginAxes(cfg);

        UpdateToolMarker(cfg);

    }

    void AddOriginAxes(GCodeViewerConfig cfg)

    {

        if (!cfg.ShowAxes || _viewport == null)

            return;

        var length = _bounds.IsEmpty ? 10d : Math.Max(_bounds.MaxSize * 0.15d, 10d);

        AddAxis(Colors.Red, new NumericVector3(0f, 0f, 0f), new NumericVector3((float)length, 0f, 0f));

        AddAxis(Colors.LimeGreen, new NumericVector3(0f, 0f, 0f), new NumericVector3(0f, (float)length, 0f));

        AddAxis(Colors.DeepSkyBlue, new NumericVector3(0f, 0f, 0f), new NumericVector3(0f, 0f, (float)length));

    }

    void AddAxis(Color color, NumericVector3 from, NumericVector3 to)

    {

        if (_viewport == null)

            return;

        HelixLineHelper.AddLineModel(

            _viewport,

            _lineModels,

            HelixLineHelper.CreateLineModel([from, to], color, AxisThickness));

    }



    void UpdateToolMarker(GCodeViewerConfig? cfg = null)

    {

        if (_viewport == null)

            return;



        RemoveToolMarker();

        cfg ??= GCodeViewerContext.Settings;



        if (_segments == null || _bounds.IsEmpty)

            return;



        var mode = (ToolVisualizerMode)Math.Clamp(cfg.ToolVisualizer, 0, 2);

        if (mode == ToolVisualizerMode.None)

            return;



        var toolPos = ViewerToolMarker.GetToolPosition();

        var lines = ViewerToolMarker.Build(_bounds, toolPos, mode, cfg.ToolDiameter);

        if (lines.Count < 2)

            return;



        var model = HelixLineHelper.CreateLineModel(lines, cfg.ToolOriginColor.ToColor(), ToolThickness);

        model.Tag = "tool-marker";

        HelixLineHelper.AddLineModel(_viewport, _lineModels, model);

    }



    void RemoveToolMarker()

    {

        if (_viewport == null)

            return;



        for (var i = _lineModels.Count - 1; i >= 0; i--)

        {

            if (_lineModels[i].Tag as string != "tool-marker")

                continue;

            _viewport.Items.Remove(_lineModels[i]);

            _lineModels.RemoveAt(i);

        }

    }



    void ClearScene()

    {

        if (_viewport != null)

            HelixLineHelper.ClearManaged(_viewport, _lineModels);

    }



    void ApplyViewportChrome()

    {

        if (_viewport == null)

            return;



        HelixViewportHost.Configure(_viewport);



        var cfg = GCodeViewerContext.Settings;

        _viewport.ShowCoordinateSystem = cfg.ShowCoordinateSystem;

        _viewport.ShowViewCube = cfg.ShowViewCube;

        HelixViewportHost.ApplyBackground(_viewport, cfg.BlackBackground);

        OverlayPanel.IsVisible = cfg.ShowTextOverlay;

    }



    void SubscribeConfig()

    {

        var cfg = GCodeViewerContext.AppConfig?.Base.GCodeViewer;

        if (cfg == null || ReferenceEquals(cfg, _subscribedConfig))

            return;



        UnsubscribeConfig();

        _subscribedConfig = cfg;

        cfg.PropertyChanged += OnViewerConfigChanged;

    }



    void UnsubscribeConfig()

    {

        if (_subscribedConfig == null)

            return;

        _subscribedConfig.PropertyChanged -= OnViewerConfigChanged;

        _subscribedConfig = null;

    }



    void SubscribeGrbl()

    {

        var grbl = GCodeViewerContext.Grbl;

        if (grbl == null)

            return;

        grbl.PropertyChanged -= OnGrblPropertyChanged;

        grbl.PropertyChanged += OnGrblPropertyChanged;

    }



    void UnsubscribeGrbl()

    {

        if (GCodeViewerContext.Grbl == null)

            return;

        GCodeViewerContext.Grbl.PropertyChanged -= OnGrblPropertyChanged;

    }



    void OnViewerConfigChanged(object? sender, PropertyChangedEventArgs e)

    {

        ApplyViewportChrome();



        if (e.PropertyName is nameof(GCodeViewerConfig.IsEnabled))

        {

            TryLoadProgram();

            return;

        }



        if (_tokens == null || _viewport == null)

            return;



        if (e.PropertyName is nameof(GCodeViewerConfig.ArcResolution)

            or nameof(GCodeViewerConfig.MinDistance)

            or nameof(GCodeViewerConfig.CutMotionColor)

            or nameof(GCodeViewerConfig.RapidMotionColor)

            or nameof(GCodeViewerConfig.RetractMotionColor)

            or nameof(GCodeViewerConfig.BlackBackground))

        {

            RequestRender();

            return;

        }



        if (e.PropertyName is nameof(GCodeViewerConfig.ToolVisualizer)

            or nameof(GCodeViewerConfig.ToolDiameter)

            or nameof(GCodeViewerConfig.ToolOriginColor))

        {

            UpdateToolMarker();

            _viewport.InvalidateRender();

            return;

        }



        if (e.PropertyName is nameof(GCodeViewerConfig.ShowGrid)

            or nameof(GCodeViewerConfig.ShowBoundingBox)

            or nameof(GCodeViewerConfig.ShowWorkEnvelope)

            or nameof(GCodeViewerConfig.ShowAxes)

            or nameof(GCodeViewerConfig.GridColor)

            or nameof(GCodeViewerConfig.HighlightColor))

        {

            if (_segments != null)

            {

                ClearScene();

                AddPathLines(GCodeViewerContext.Settings);

                AddAdorners(GCodeViewerContext.Settings);

                _viewport.InvalidateSceneGraph();

                _viewport.InvalidateRender();

                Dispatcher.UIThread.Post(() => _viewport?.ZoomExtents(), DispatcherPriority.Render);

            }

        }

    }



    void OnGrblPropertyChanged(object? sender, PropertyChangedEventArgs e)

    {

        if (e.PropertyName is nameof(GrblViewModel.Position) or nameof(GrblViewModel.WorkPositionOffset))

            UpdateToolMarker();

    }



    void OnViewportRendered(object? sender, EventArgs e)

    {

        if (_renderPending)

            TryFlushRender();

    }

    void OnViewportPointerPressed(object? sender, PointerPressedEventArgs e)

    {

        if (_viewport == null)

            return;

        var point = e.GetCurrentPoint(_viewport);

        if (point.Properties.IsMiddleButtonPressed)

        {

            _isMiddlePanning = true;

            _lastPanPoint = point.Position;

            e.Pointer.Capture(_viewport);

            e.Handled = true;

        }
        else if (point.Properties.IsRightButtonPressed)

        {

            _isRightRotating = true;

            _lastRotatePoint = point.Position;

            e.Pointer.Capture(_viewport);

            e.Handled = true;

        }

    }

    void OnViewportPointerMoved(object? sender, PointerEventArgs e)

    {

        if (_viewport == null)

            return;

        var point = e.GetCurrentPoint(_viewport);

        if (_isMiddlePanning)

        {

            if (!point.Properties.IsMiddleButtonPressed)

            {

                _isMiddlePanning = false;

                e.Pointer.Capture(null);

                return;

            }

            PanCamera(point.Position - _lastPanPoint);

            _lastPanPoint = point.Position;

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

            RotateCamera(point.Position - _lastRotatePoint);

            _lastRotatePoint = point.Position;

            e.Handled = true;

            return;

        }

    }

    void OnViewportPointerReleased(object? sender, PointerReleasedEventArgs e)

    {

        if (_viewport == null)

            return;

        var update = e.GetCurrentPoint(_viewport).Properties.PointerUpdateKind;

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

    void OnViewportPointerWheelChanged(object? sender, PointerWheelEventArgs e)

    {

        if (_viewport?.Camera is not OrthographicCamera camera)

            return;

        var scale = e.Delta.Y > 0d ? 0.88d : 1.14d;

        camera.Width = Math.Clamp(camera.Width * scale, 0.01d, 1_000_000d);

        _viewport.InvalidateRender();

        e.Handled = true;

    }

    void PanCamera(Vector delta)

    {

        if (_viewport?.Camera is not ProjectionCamera camera)

            return;

        var width = _viewport.Bounds.Width;

        if (width <= 0d)

            return;

        var unitsPerPixel = camera is OrthographicCamera ortho

            ? ortho.Width / width

            : Math.Max(_bounds.MaxSize, 1d) / width;

        var look = NumericVector3.Normalize(camera.LookDirection);

        var up = NumericVector3.Normalize(camera.UpDirection);

        var right = NumericVector3.Normalize(NumericVector3.Cross(look, up));

        var move = right * (float)(-delta.X * unitsPerPixel) + up * (float)(delta.Y * unitsPerPixel);

        camera.Position += move;

        _viewport.InvalidateRender();

    }

    void RotateCamera(Vector delta)
    {
        if (_viewport?.Camera is not ProjectionCamera camera)
            return;

        var target = camera.Position + camera.LookDirection;
        var offset = camera.Position - target;
        if (offset.LengthSquared() <= 1e-8f)
            return;

        var look = NumericVector3.Normalize(camera.LookDirection);
        var up = NumericVector3.Normalize(camera.UpDirection);
        var right = NumericVector3.Normalize(NumericVector3.Cross(look, up));
        var yaw = (float)(-delta.X * 0.01d);
        var pitch = (float)(-delta.Y * 0.01d);
        var yawRotation = System.Numerics.Quaternion.CreateFromAxisAngle(new NumericVector3(0f, 0f, 1f), yaw);
        var pitchRotation = System.Numerics.Quaternion.CreateFromAxisAngle(right, pitch);
        var rotation = System.Numerics.Quaternion.Normalize(pitchRotation * yawRotation);

        var newOffset = NumericVector3.Transform(offset, rotation);
        var newUp = NumericVector3.Normalize(NumericVector3.Transform(up, rotation));

        camera.Position = target + newOffset;
        camera.LookDirection = target - camera.Position;
        camera.UpDirection = newUp;

        _viewport.InvalidateRender();
    }


    void ResetToTopView(PathBounds bounds)
    {
        if (_viewport?.Camera is not OrthographicCamera camera)
        {
            _viewport?.ZoomExtents();
            return;
        }

        var center = bounds.IsEmpty
            ? new NumericVector3(0f, 0f, 0f)
            : new NumericVector3((float)bounds.Center.X, (float)bounds.Center.Y, (float)bounds.Center.Z);
        var span = bounds.IsEmpty ? 100d : Math.Max(bounds.MaxSize, 20d);
        var distance = Math.Max(span * 2d, 100d);
        var viewWidth = Math.Max(span * 1.25d, 20d);

        camera.Position = center + new NumericVector3(0f, 0f, (float)distance);
        camera.LookDirection = new NumericVector3(0f, 0f, (float)-distance);
        camera.UpDirection = new NumericVector3(0f, 1f, 0f);
        camera.Width = viewWidth;

        _viewport.InvalidateSceneGraph();
        _viewport.InvalidateRender();
    }



    void OnResetViewClick(object? sender, RoutedEventArgs e) => ResetToTopView(_bounds);

}


