using System.ComponentModel;
using System.Diagnostics;
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
using CNC.GCodeViewer.Avalonia.OpenGl;
using NumericVector3 = System.Numerics.Vector3;

namespace CNC.GCodeViewer.Avalonia.Views;

public partial class RenderControl : UserControl
{
    const float ToolThickness = 1.25f;
    const int MaxGlReadyWaitTicks = 80;
    static readonly TimeSpan DynamicLayerInterval = TimeSpan.FromMilliseconds(33);

    GCodePathSegments? _segments;
    GCodeExecutedPathCache? _executedPathCache;
    PathBounds _bounds;
    IReadOnlyList<GCodeToken>? _tokens;
    GCodeViewerConfig? _subscribedConfig;
    bool _renderPending;
    bool _glInitFailed;
    bool _pathBuildInProgress;
    CancellationTokenSource? _buildCts;
    int _buildVersion;
    string _statusText = "";
    DispatcherTimer? _renderWaitTimer;
    DispatcherTimer? _dynamicLayerTimer;
    bool _dynamicLayerPending;
    bool _dynamicLayerImmediate;
    int _executedLayerVersion = -1;
    ViewerLineLayer? _pendingCutLayer;
    ViewerLineLayer? _completedCutLayer;
    int _renderWaitTicks;
    Point3D? _renderStartOverride;
    Point3D? _programStart;
    ViewerThemeColors _themeColors = ViewerThemeColors.Current();

    public GCodeViewerSession? Session { get; set; }

    GCodeViewerSession ViewerSession =>
        Session ?? throw new InvalidOperationException("G-code viewer session was not assigned.");

    public RenderControl()
    {
        InitializeComponent();
        ViewCube.Camera = GlViewport.Camera;
        ViewCube.FaceClicked += OnViewCubeFaceClicked;
        GlViewport.CameraChanged += (_, _) => ViewCube.InvalidateVisual();
        ViewportInput.PointerPressed += OnViewportPointerPressed;
        ViewportInput.PointerMoved += OnViewportPointerMoved;
        ViewportInput.PointerReleased += OnViewportPointerReleased;
        ViewportInput.PointerCaptureLost += OnViewportPointerCaptureLost;
        ViewportInput.PointerWheelChanged += OnViewportPointerWheelChanged;
        GlViewport.InitializationFailed += (_, _) =>
        {
            _glInitFailed = true;
            SetStatus($"3D view unavailable - {GlViewport.InitializationFailureMessage ?? "OpenGL"}");
        };
        GlViewport.RenderingFailed += (_, _) =>
        {
            var reason = GlViewport.RenderFailureMessage;
            if (!string.IsNullOrWhiteSpace(reason))
                SetStatus($"3D view OpenGL error - {reason}");
        };
        GlViewport.SceneApplied += (_, _) =>
        {
            if (_pathBuildInProgress)
                return;
            if (_renderPending && GlViewport.Bounds.Width >= 4 && GlViewport.Bounds.Height >= 4)
                TryFlushRender();
        };
    }

    public void Close()
    {
        UnsubscribeExecutionProgress();
        _tokens = null;
        _segments = null;
        _executedPathCache = null;
        ResetExecutedLayerCache();
        _bounds = default;
        _programStart = null;
        _renderPending = false;
        StopDynamicLayerTimer();
        CancelPathBuild();
        GlViewport.SetAnimationActive(false);
        GlViewport.ClearScene();
        SetStatus("3D view — no program loaded");
    }

    public void Open(IReadOnlyList<GCodeToken> tokens, Point3D? start = null)
    {
        _tokens = tokens;
        _programStart = start;
        SubscribeExecutionProgress();
        RequestRender(start);
    }

    public void CancelPreviewBuild()
    {
#if DEBUG
        var watch = Stopwatch.StartNew();
#endif
        _renderPending = false;
        CancelPathBuild();
#if DEBUG
        watch.Stop();
        Trace.WriteLine($"G-code preview cancel requested in {watch.ElapsedMilliseconds} ms");
#endif
    }

    public void TryLoadProgram()
    {
#if DEBUG
        var watch = Stopwatch.StartNew();
#endif
        if (!ViewerSession.Settings.IsEnabled)
        {
            Close();
            SetStatus("3D view disabled in App Settings → G-code viewer");
            return;
        }

        var tokens = ViewerSession.GetProgramTokens();
        if (tokens is { Count: > 0 })
            Open(tokens);
        else
            Close();
#if DEBUG
        watch.Stop();
        Trace.WriteLine($"G-code viewer TryLoadProgram completed in {watch.ElapsedMilliseconds} ms; tokens={tokens.Count}");
#endif
    }

    public bool TryLoadProgramIfVisible()
    {
        if (!IsPreviewEligible)
            return false;

        TryLoadProgram();
        return true;
    }

    bool IsPreviewEligible =>
        IsVisible
        && IsEffectivelyVisible
        && VisualRoot is not null
        && Bounds.Width >= 4
        && Bounds.Height >= 4;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SubscribeConfig();
        SubscribeGrbl();
        AppThemeKeys.ThemeApplied += OnAppThemeApplied;
        RefreshThemeColors(rebuild: false);
        GlViewport.ViewerConfig = ViewerSession.Settings;
        GlViewport.SetAnimationActive(true);
        SetStatus(_glInitFailed ? "3D view unavailable — OpenGL" : "3D view — ready");
        TryLoadProgramIfVisible();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        GlViewport.SaveCameraToConfig();
        GlViewport.SetAnimationActive(false);
        StopRenderWaitTimer();
        CancelPathBuild();
        AppThemeKeys.ThemeApplied -= OnAppThemeApplied;
        UnsubscribeConfig();
        UnsubscribeGrbl();
        UnsubscribeExecutionProgress();
        base.OnDetachedFromVisualTree(e);
    }

    void RequestRender(Point3D? start = null)
    {
        _renderStartOverride = start;
        _renderPending = true;
        if (_pathBuildInProgress)
            CancelPathBuild();
        if (_glInitFailed)
            return;
        TryFlushRender();
        if (_renderPending && !_pathBuildInProgress)
            StartRenderWaitTimer();
    }

    void TryFlushRender()
    {
        if (!_renderPending || _tokens == null || _glInitFailed)
            return;

        if (_pathBuildInProgress)
            return;

        var viewportSize = GlViewport.Bounds;
        if (viewportSize.Width < 4 || viewportSize.Height < 4)
        {
            SetStatus($"3D view — sizing ({viewportSize.Width:F0}×{viewportSize.Height:F0})");
            return;
        }

        StopRenderWaitTimer();
        var tokens = _tokens;
        var tokenCount = tokens.Count;
        var start = _renderStartOverride ?? new Point3D();
        _renderStartOverride = null;
        _programStart ??= start;
        CancelPathBuild();
        _pathBuildInProgress = true;
        _buildCts = new CancellationTokenSource();
        var cts = _buildCts;
        var ct = cts.Token;
        var buildVersion = ++_buildVersion;
        var theme = _themeColors;
        SetStatus($"3D view — building toolpath ({tokenCount:N0} tokens)…");

        ViewerSession.SetPreviewBuilding?.Invoke(true);

        var buildTask = Task.Run(() =>
        {
            try
            {
#if DEBUG
                var buildWatch = Stopwatch.StartNew();
#endif
                var cfg = ViewerSession.Settings;
                var built = GCodePathBuilder.Build(tokens, start, cfg.ArcResolution, cfg.MinDistance, ct);
#if DEBUG
                var pathBuildMs = buildWatch.ElapsedMilliseconds;
#endif
                var bounds = PathBounds.FromSegments(built.Segments);
                var scene = ToolpathSceneBuilder.Build(built.Segments, bounds, cfg, theme, ViewerSession.Grbl, ct);
#if DEBUG
                var sceneBuildMs = buildWatch.ElapsedMilliseconds - pathBuildMs;
                buildWatch.Stop();
                Trace.WriteLine($"G-code preview build completed in {buildWatch.ElapsedMilliseconds} ms; path={pathBuildMs} ms; scene={sceneBuildMs} ms; tokens={tokenCount}; motions={built.MotionCount}");
#endif
                Dispatcher.UIThread.Post(() =>
                {
                    if (ReferenceEquals(_buildCts, cts))
                    {
                        _pathBuildInProgress = false;
                        _buildCts = null;
                    }

                    if (!IsCurrentBuild(buildVersion))
                    {
                        ViewerSession.SetPreviewBuilding?.Invoke(false);
                        return;
                    }

                    try
                    {
                        if (!_renderPending)
                        {
                            ViewerSession.SetPreviewBuilding?.Invoke(false);
                            return;
                        }

                        _renderPending = false;
#if DEBUG
                        var applyWatch = Stopwatch.StartNew();
#endif
                        ApplyScene(built.Segments, built.ExecutedPathCache, bounds, built.MotionCount, scene);
#if DEBUG
                        applyWatch.Stop();
                        Trace.WriteLine($"G-code preview UI apply completed in {applyWatch.ElapsedMilliseconds} ms; tokens={tokenCount}; motions={built.MotionCount}");
#endif
                        ViewerSession.SetPreviewBuilding?.Invoke(false);
                    }
                    catch (OperationCanceledException)
                    {
                        ViewerSession.SetPreviewBuilding?.Invoke(false);
                    }
                    catch (Exception)
                    {
                        _renderPending = false;
                        ViewerSession.SetPreviewBuilding?.Invoke(false);
                        SetStatus("3D view — build failed");
                    }
                }, DispatcherPriority.ApplicationIdle);
            }
            catch (OperationCanceledException)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (ReferenceEquals(_buildCts, cts))
                    {
                        _pathBuildInProgress = false;
                        _buildCts = null;
                    }
                    ViewerSession.SetPreviewBuilding?.Invoke(false);
                });
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (ReferenceEquals(_buildCts, cts))
                    {
                        _pathBuildInProgress = false;
                        _buildCts = null;
                    }

                    if (!IsCurrentBuild(buildVersion))
                    {
                        ViewerSession.SetPreviewBuilding?.Invoke(false);
                        return;
                    }

                    _renderPending = false;
                    ViewerSession.SetPreviewBuilding?.Invoke(false);
                    SetStatus($"3D view — build failed: {ex.GetType().Name}");
                });
            }
        }, ct);
        _ = buildTask.ContinueWith(_ => cts.Dispose(), TaskScheduler.Default);
    }

    void CancelPathBuild()
    {
        if (_buildCts == null)
        {
            _pathBuildInProgress = false;
            return;
        }

        _buildVersion++;
        _pathBuildInProgress = false;
        _buildCts.Cancel();
        _buildCts = null;
    }

    bool IsCurrentBuild(int buildVersion) => buildVersion == _buildVersion;

    void ApplyScene(
        GCodePathSegments segments,
        GCodeExecutedPathCache executedPathCache,
        PathBounds bounds,
        int motionCount,
        ViewerScene scene)
    {
#if DEBUG
        var watch = Stopwatch.StartNew();
#endif
        _segments = segments;
        _executedPathCache = executedPathCache;
        ResetExecutedLayerCache();
        _bounds = bounds;
        EnrichScene(scene);
        PushSceneToViewport(scene, bounds, resetView: true);

        var pointCount = segments.Cut.Count + segments.Rapid.Count + segments.Retract.Count;
        if (scene.LayerCount == 0)
            SetStatus($"3D view — {motionCount} motions, 0 path points");
        else
        {
            SetStatus($"3D view — {motionCount:N0} motions, {pointCount:N0} preview points, {scene.LayerCount} layers");
            OverlayText.Text = $"Program: {motionCount:N0} motion commands";
        }
#if DEBUG
        watch.Stop();
        Trace.WriteLine($"G-code viewer ApplyScene completed in {watch.ElapsedMilliseconds} ms; motions={motionCount}; points={pointCount}");
#endif
    }

    void EnrichScene(ViewerScene scene)
    {
        var cfg = ViewerSession.Settings;
        scene.OriginAxes = ViewerAdornmentsBuilder.BuildOriginAxes(cfg, _bounds);
        scene.ViewCube = null;
        UpdateCutColorLayers(cfg);
        scene.PendingCut = _pendingCutLayer;
        scene.Executed = _completedCutLayer;
        ViewCube.IsVisible = cfg.ShowViewCube;
        scene.ToolMarkerLayers = BuildToolMarker(cfg);
        GlViewport.SetBackground(_themeColors.Background);
        OverlayPanel.IsVisible = cfg.ShowTextOverlay;
    }

    void PushSceneToViewport(ViewerScene scene, PathBounds bounds, bool resetView)
    {
        GlViewport.SetScene(scene, bounds, resetView);
        UpdateDynamicLayers();
    }

    void UpdateDynamicLayers()
    {
        if (_segments == null)
            return;
        var cfg = ViewerSession.Settings;
        UpdateCutColorLayers(cfg);
        GlViewport.UpdateDynamicLayers(BuildToolMarker(cfg), _pendingCutLayer, _completedCutLayer);
    }

    void QueueDynamicLayerUpdate(bool immediate = false)
    {
        if (_segments == null)
            return;

        _dynamicLayerPending = true;
        _dynamicLayerImmediate |= immediate;

        if (immediate)
        {
            FlushDynamicLayerUpdate();
            return;
        }

        _dynamicLayerTimer ??= new DispatcherTimer(DynamicLayerInterval, DispatcherPriority.Background, OnDynamicLayerTimerTick);
        if (!_dynamicLayerTimer.IsEnabled)
            _dynamicLayerTimer.Start();
    }

    void OnDynamicLayerTimerTick(object? sender, EventArgs e)
    {
        if (!_dynamicLayerPending)
        {
            StopDynamicLayerTimer();
            return;
        }

        FlushDynamicLayerUpdate();
    }

    void FlushDynamicLayerUpdate()
    {
        if (!_dynamicLayerPending && !_dynamicLayerImmediate)
            return;

        _dynamicLayerPending = false;
        _dynamicLayerImmediate = false;
        UpdateDynamicLayers();
    }

    void StopDynamicLayerTimer()
    {
        if (_dynamicLayerTimer == null)
            return;

        _dynamicLayerTimer.Stop();
    }

    void UpdateCutColorLayers(GCodeViewerConfig cfg)
    {
        if (_segments == null || _tokens == null || _executedPathCache == null || !cfg.RenderExecuted)
        {
            ClearCutColorLayers();
            return;
        }

        var progress = ViewerSession.ExecutionProgress;
        if (!progress.HasCompletedLines)
        {
            ClearCutColorLayers();
            return;
        }

        if (_executedLayerVersion == progress.CompletedVersion)
            return;

        var completedStartEntryIndex = ResolveCompletedStartEntryIndex(progress);
        var completedEntryIndex = ResolveCompletedEntryIndex(progress);
        if (completedEntryIndex > progress.CompletedEntryIndex)
            progress.SetCompletedEntryBoundary(completedEntryIndex);

        var cut = ViewerColors.ResolveCutColor(cfg, _themeColors);
        var completedLines = progress.SnapshotCompletedLineNumbers();
        var split = _executedPathCache.BuildCutSplit(completedStartEntryIndex, progress.CompletedEntryIndex, completedLines);
        _pendingCutLayer = ViewerLineLayerBuilder.FromPoints(split.Pending, cut, 1.5f);
        _completedCutLayer = ViewerLineLayerBuilder.FromPoints(split.Completed, Dim(cut), 1.5f);
        _executedLayerVersion = progress.CompletedVersion;
    }

    int ResolveCompletedStartEntryIndex(GCodeExecutionProgress progress)
    {
        if (_executedPathCache == null || progress.StartLineNumber == 0)
            return 0;

        return _executedPathCache.TryGetFirstEntryIndexAtOrAfterLine(progress.StartLineNumber, out var startEntryIndex)
            ? startEntryIndex
            : 0;
    }

    int ResolveCompletedEntryIndex(GCodeExecutionProgress progress)
    {
        if (_executedPathCache == null || progress.CurrentExecutingLineNumber == 0)
            return progress.CompletedEntryIndex;

        var currentLine = progress.CurrentExecutingLineNumber;
        if (!_executedPathCache.TryGetFirstEntryIndex(currentLine, out var currentEntryIndex) &&
            !_executedPathCache.TryGetFirstEntryIndexAtOrAfterLine(currentLine, out currentEntryIndex))
            return progress.CompletedEntryIndex;

        var boundary = currentEntryIndex - 1;
        return Math.Max(progress.CompletedEntryIndex, boundary);
    }

    void ClearCutColorLayers()
    {
        _pendingCutLayer = null;
        _completedCutLayer = null;
        _executedLayerVersion = -1;
    }

    void ResetExecutedLayerCache()
    {
        ClearCutColorLayers();
        _dynamicLayerPending = false;
        _dynamicLayerImmediate = false;
    }

    static Color Dim(Color color) =>
        Color.FromArgb(color.A, (byte)(color.R / 2), (byte)(color.G / 2), (byte)(color.B / 2));

    static Color Lighten(Color color)
    {
        const double amount = 0.2d;
        return Color.FromArgb(
            color.A,
            LightenChannel(color.R),
            LightenChannel(color.G),
            LightenChannel(color.B));

        static byte LightenChannel(byte channel) =>
            (byte)Math.Clamp(Math.Round(channel + (255 - channel) * amount), 0, 255);
    }

    IReadOnlyList<ViewerLineLayer> BuildToolMarker(GCodeViewerConfig cfg)
    {
        if (_segments == null || _bounds.IsEmpty)
            return [];

        var mode = (ToolVisualizerMode)Math.Clamp(cfg.ToolVisualizer, 0, 2);
        if (mode == ToolVisualizerMode.None)
            return [];

        var toolPos = ResolveToolMarkerPosition();
        var camDist = GlViewport.Camera.DistanceTo(new NumericVector3(
            (float)toolPos.X, (float)toolPos.Y, (float)toolPos.Z));
        var markerBounds = BoundsIncluding(toolPos);
        var color = ViewerColors.ResolveToolColor(cfg, _themeColors);
        if (mode == ToolVisualizerMode.Cone)
        {
            var cone = ViewerToolMarker.BuildCone(toolPos, cfg.ToolDiameter, cfg.ToolAutoScale, camDist);
            var sideLayer = ViewerLineLayerBuilder.FromTriangles(cone.Sides, color, tag: "tool-marker");
            var baseLayer = ViewerLineLayerBuilder.FromTriangles(cone.Base, Lighten(color), tag: "tool-marker-base");
            var layers = new List<ViewerLineLayer>(2);
            if (sideLayer != null)
                layers.Add(sideLayer);
            if (baseLayer != null)
                layers.Add(baseLayer);
            return layers;
        }

        var points = ViewerToolMarker.Build(markerBounds, toolPos, mode, cfg.ToolDiameter, cfg.ToolAutoScale, camDist);
        var layer = ViewerLineLayerBuilder.FromPoints(points, color, ToolThickness, tag: "tool-marker");
        return layer == null ? [] : [layer];
    }

    Point3D ResolveToolMarkerPosition()
    {
        if (Session?.Grbl != null)
            return ViewerToolMarker.GetToolPosition(ViewerSession.Grbl);

        return _programStart ?? ViewerToolMarker.GetToolPosition(ViewerSession.Grbl);
    }

    PathBounds BoundsIncluding(Point3D point)
    {
        if (_bounds.IsEmpty)
            return new PathBounds
            {
                MinX = point.X,
                MinY = point.Y,
                MinZ = point.Z,
                MaxX = point.X,
                MaxY = point.Y,
                MaxZ = point.Z,
                HasValue = true,
            };

        return new PathBounds
        {
            MinX = Math.Min(_bounds.MinX, point.X),
            MinY = Math.Min(_bounds.MinY, point.Y),
            MinZ = Math.Min(_bounds.MinZ, point.Z),
            MaxX = Math.Max(_bounds.MaxX, point.X),
            MaxY = Math.Max(_bounds.MaxY, point.Y),
            MaxZ = Math.Max(_bounds.MaxZ, point.Z),
            HasValue = true,
        };
    }

    void RebuildStaticLayers()
    {
        if (_segments == null || _glInitFailed)
            return;

        var cfg = ViewerSession.Settings;
        var scene = ToolpathSceneBuilder.Build(_segments, _bounds, cfg, _themeColors, ViewerSession.Grbl);
        EnrichScene(scene);
        PushSceneToViewport(scene, _bounds, resetView: false);
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
        if (_pathBuildInProgress)
            return;

        if (!_renderPending)
        {
            StopRenderWaitTimer();
            return;
        }

        _renderWaitTicks++;
        TryFlushRender();

        if (!_renderPending)
            StopRenderWaitTimer();
        else if (_renderWaitTicks >= MaxGlReadyWaitTicks)
        {
            StopRenderWaitTimer();
            _renderPending = false;
            SetStatus("3D view — OpenGL not ready (check GPU / drivers)");
        }
    }

    void SetStatus(string text)
    {
        if (string.Equals(_statusText, text, StringComparison.Ordinal))
            return;
        _statusText = text;
        StatusText.Text = text;
    }

    void SubscribeConfig()
    {
        var cfg = Session?.Settings;
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
        var grbl = Session?.Grbl;
        if (grbl == null)
            return;
        grbl.PropertyChanged -= OnGrblPropertyChanged;
        grbl.PropertyChanged += OnGrblPropertyChanged;
    }

    void UnsubscribeGrbl()
    {
        var grbl = Session?.Grbl;
        if (grbl == null)
            return;
        grbl.PropertyChanged -= OnGrblPropertyChanged;
    }

    void SubscribeExecutionProgress()
    {
        if (Session == null)
            return;

        ViewerSession.ExecutionProgress.Changed -= OnExecutionProgressChanged;
        ViewerSession.ExecutionProgress.Changed += OnExecutionProgressChanged;
    }

    void UnsubscribeExecutionProgress()
    {
        if (Session == null)
            return;

        ViewerSession.ExecutionProgress.Changed -= OnExecutionProgressChanged;
    }

    void OnExecutionProgressChanged(object? sender, EventArgs e)
    {
        var progress = ViewerSession.ExecutionProgress;
        QueueDynamicLayerUpdate(!progress.HasCompletedLines || progress.CompletedVersion == 0);
    }

    void OnViewerConfigChanged(object? sender, PropertyChangedEventArgs e)
    {
        var cfg = ViewerSession.Settings;
        GlViewport.SetBackground(_themeColors.Background);
        OverlayPanel.IsVisible = cfg.ShowTextOverlay;
        ViewCube.IsVisible = cfg.ShowViewCube;

        if (e.PropertyName is nameof(GCodeViewerConfig.IsEnabled))
        {
            TryLoadProgramIfVisible();
            return;
        }

        if (_tokens == null)
            return;

        if (e.PropertyName is nameof(GCodeViewerConfig.ArcResolution)
            or nameof(GCodeViewerConfig.MinDistance)
            or nameof(GCodeViewerConfig.CutMotionColor)
            or nameof(GCodeViewerConfig.RapidMotionColor)
            or nameof(GCodeViewerConfig.RetractMotionColor)
            or nameof(GCodeViewerConfig.BlackBackground)
            or nameof(GCodeViewerConfig.RenderExecuted))
        {
            RequestRender();
            return;
        }

        if (e.PropertyName is nameof(GCodeViewerConfig.ToolVisualizer)
            or nameof(GCodeViewerConfig.ToolDiameter)
            or nameof(GCodeViewerConfig.ToolOriginColor)
            or nameof(GCodeViewerConfig.ToolAutoScale))
        {
            QueueDynamicLayerUpdate();
            return;
        }

        if (e.PropertyName is nameof(GCodeViewerConfig.ShowGrid)
            or nameof(GCodeViewerConfig.ShowBoundingBox)
            or nameof(GCodeViewerConfig.ShowWorkEnvelope)
            or nameof(GCodeViewerConfig.ShowAxes)
            or nameof(GCodeViewerConfig.ShowCoordinateSystem)
            or nameof(GCodeViewerConfig.ShowViewCube)
            or nameof(GCodeViewerConfig.GridColor)
            or nameof(GCodeViewerConfig.HighlightColor))
        {
            RebuildStaticLayers();
        }
    }

    void OnAppThemeApplied(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() => RefreshThemeColors(rebuild: true), DispatcherPriority.Background);

    void RefreshThemeColors(bool rebuild)
    {
        _themeColors = ViewerThemeColors.Current();
        GlViewport.SetBackground(_themeColors.Background);
        if (rebuild)
            RebuildStaticLayers();
        else
            QueueDynamicLayerUpdate();
    }

    void OnGrblPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GrblViewModel.Position)
            or nameof(GrblViewModel.MachinePosition)
            or nameof(GrblViewModel.WorkPositionOffset)
            or nameof(GrblViewModel.BlockExecuting))
            QueueDynamicLayerUpdate();
        else if (e.PropertyName is nameof(GrblViewModel.HomedState))
            RebuildStaticLayers();
    }

    void OnResetViewClick(object? sender, RoutedEventArgs e) => GlViewport.ResetView();

    void OnViewCubeFaceClicked(object? sender, ViewCubeFace face)
    {
        var dir = face switch
        {
            ViewCubeFace.Up => NumericVector3.UnitZ,
            ViewCubeFace.Down => -NumericVector3.UnitZ,
            ViewCubeFace.Left => NumericVector3.UnitY,
            ViewCubeFace.Right => -NumericVector3.UnitY,
            ViewCubeFace.Front => NumericVector3.UnitX,
            ViewCubeFace.Back => -NumericVector3.UnitX,
            _ => NumericVector3.UnitZ,
        };
        GlViewport.OrientTo(dir);
    }

    void OnViewportPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        ViewportInput.Focus();
        GlViewport.HandlePointerPressed(e, ViewportInput);
    }

    void OnViewportPointerMoved(object? sender, PointerEventArgs e) =>
        GlViewport.HandlePointerMoved(e, ViewportInput);

    void OnViewportPointerReleased(object? sender, PointerReleasedEventArgs e) =>
        GlViewport.HandlePointerReleased(e, ViewportInput);

    void OnViewportPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) =>
        GlViewport.HandlePointerCaptureLost(e);

    void OnViewportPointerWheelChanged(object? sender, PointerWheelEventArgs e) =>
        GlViewport.HandlePointerWheelChanged(e, ViewportInput);
}

