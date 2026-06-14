using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Diagnostics;
using CNC.App.Workspace;
using CNC.Controls.Avalonia.Services;
using CNC.Core;
using CNC.GCodeViewer.Avalonia;
using CNC.GCodeViewer.Avalonia.Views;
using ioSender.Services;
using ioSender.Workspace.Editors;

namespace ioSender.Workspace.Controls;

public partial class WorkspaceHost : UserControl
{
    public static readonly StyledProperty<bool> IsEditModeProperty =
        AvaloniaProperty.Register<WorkspaceHost, bool>(nameof(IsEditMode));

    public static readonly StyledProperty<bool> IsLayoutReadyProperty =
        AvaloniaProperty.Register<WorkspaceHost, bool>(nameof(IsLayoutReady));

    WorkspaceNode _root = null!;
    WorkspaceEditorFactory? _factory;
    SplitLayoutBuilder? _builder;
    readonly List<WorkspaceRegionChrome> _regionChromes = new();
    HashSet<WorkspaceEditorId> _activeEditors = new();
    ProgramService? _programService;
    WorkspaceSplitIntent? _activeSplitIntent;
    WorkspaceRegionChrome? _splitTarget;
    double _splitRatio = 0.5;
    int _toolpathRefreshVersion;
    const double SplitPreviewThickness = 2;

    public event EventHandler? LayoutChanged;
    public event EventHandler<IReadOnlyCollection<WorkspaceEditorId>>? ActiveEditorsChanged;

    public WorkspaceHost()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, OnHostPointerPressed, RoutingStrategies.Tunnel, true);
        KeyDown += OnKeyDown;
    }

    public bool IsEditMode
    {
        get => GetValue(IsEditModeProperty);
        set => SetValue(IsEditModeProperty, value);
    }

    public bool IsLayoutReady
    {
        get => GetValue(IsLayoutReadyProperty);
        set => SetValue(IsLayoutReadyProperty, value);
    }

    public WorkspaceEditorFactory Factory => _factory ?? throw new InvalidOperationException("Workspace not initialized.");

    public WorkspaceNode CurrentRoot => _root.Clone();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is not null)
            InitializeWorkspaceIfNeeded();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        InitializeWorkspaceIfNeeded();
        if (IsLayoutReady)
            Rebuild();
    }

    void InitializeWorkspaceIfNeeded()
    {
        if (_factory is not null || DataContext is null)
            return;

        _root = WorkspaceLayoutService.EnsureRoot();
        _programService = AppHostContext.Session.Program;
        _factory = new WorkspaceEditorFactory(DataContext, session: AppHostContext.Session, programService: _programService);
        _programService.ProgramChanged += OnProgramChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_programService != null)
            _programService.ProgramChanged -= OnProgramChanged;
        base.OnDetachedFromVisualTree(e);
    }

    void OnProgramChanged()
    {
        QueueToolpathRefresh();
    }

    public void ApplyPreset(string presetName)
    {
        ApplyLayout(presetName);
    }

    public void ApplyLayout(string layoutName)
    {
        WorkspaceLayoutService.TryApplyLayout(layoutName);
        _root = WorkspaceLayoutService.EnsureRoot();
        Rebuild();
        WorkspaceLayoutService.Persist();
    }

    public void ResetToDefault() => ApplyPreset(WorkspaceLayoutDefaults.PresetClassic);

    public void Rebuild()
    {
        if (_factory is null)
            return;

        using var _ = StartupTrace.Measure("Workspace rebuild");
        _factory.PruneToTree(_root);
        foreach (var chrome in _regionChromes)
            chrome.ClearEditorHost();
        _factory.ReleaseAllFromVisualTree();
        _regionChromes.Clear();
        RootPanel.Children.Clear();
        _builder = new SplitLayoutBuilder(
            _factory,
            OnSplitterResizeCompleted,
            PersistCurrentLayout,
            SyncActiveEditors,
            BeginSplitMode);
        var content = _builder.Build(_root, IsEditMode, chrome =>
        {
            WireRegion(chrome);
            _regionChromes.Add(chrome);
        });
        RootPanel.Children.Add(content);
        SyncActiveEditors();
        QueueToolpathRefresh();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    void WireRegion(WorkspaceRegionChrome chrome)
    {
        chrome.SplitHorizontalRequested += (_, _) =>
        {
            BeginSplitMode(WorkspaceSplitIntent.Horizontal);
        };
        chrome.SplitVerticalRequested += (_, _) =>
        {
            BeginSplitMode(WorkspaceSplitIntent.Vertical);
        };
        chrome.JoinRequested += (_, _) =>
        {
            if (chrome.LayoutNode is { } node)
                OnJoin(node);
        };
        chrome.LockSettingsChanged += (_, _) =>
        {
            PersistCurrentLayout();
            Rebuild();
        };
        chrome.ChangeEditorRequested += (_, id) => OnChangeEditor(chrome, id);
        chrome.DropCompleted += (_, target) => OnDropSwap(target);
        chrome.AddHandler(PointerEnteredEvent, OnRegionPointerEntered, RoutingStrategies.Tunnel, true);
        chrome.AddHandler(PointerMovedEvent, OnRegionPointerMoved, RoutingStrategies.Tunnel, true);
        chrome.AddHandler(PointerExitedEvent, OnRegionPointerExited, RoutingStrategies.Tunnel, true);
        chrome.AddHandler(PointerPressedEvent, OnRegionPointerPressed, RoutingStrategies.Tunnel, true);
    }

    void BeginSplitMode(WorkspaceSplitIntent intent)
    {
        if (!IsEditMode)
            return;

        WorkspaceDragBroker.Clear();
        _activeSplitIntent = intent;
        _splitTarget = null;
        _splitRatio = 0.5;
        SplitPreviewLine.IsVisible = false;
        SetRegionSplitMode(true);
        Focus();
    }

    void CancelSplitMode()
    {
        _activeSplitIntent = null;
        _splitTarget = null;
        _splitRatio = 0.5;
        SplitPreviewLine.IsVisible = false;
        SetRegionSplitMode(false);
    }

    void SetRegionSplitMode(bool isSplitMode)
    {
        foreach (var chrome in _regionChromes)
            chrome.IsSplitMode = isSplitMode;
    }

    void OnRegionPointerEntered(object? sender, PointerEventArgs e)
    {
        if (_activeSplitIntent is null || sender is not WorkspaceRegionChrome chrome)
            return;

        UpdateSplitPreview(chrome, e.GetPosition(chrome));
    }

    void OnRegionPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_activeSplitIntent is null || sender is not WorkspaceRegionChrome chrome)
            return;

        UpdateSplitPreview(chrome, e.GetPosition(chrome));
    }

    void OnRegionPointerExited(object? sender, PointerEventArgs e)
    {
        if (_activeSplitIntent is null || !ReferenceEquals(sender, _splitTarget))
            return;

        _splitTarget = null;
        SplitPreviewLine.IsVisible = false;
    }

    void OnRegionPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_activeSplitIntent is not { } intent || sender is not WorkspaceRegionChrome chrome)
            return;

        var properties = e.GetCurrentPoint(chrome).Properties;
        if (properties.IsRightButtonPressed)
        {
            CancelSplitMode();
            e.Handled = true;
            return;
        }

        if (!properties.IsLeftButtonPressed)
            return;

        UpdateSplitPreview(chrome, e.GetPosition(chrome));
        if (chrome.LayoutNode is { } node)
            OnSplit(node, intent.ToLayoutOrientation(), _splitRatio);

        e.Handled = true;
    }

    void OnHostPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_activeSplitIntent is null)
            return;

        if (!e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            return;

        CancelSplitMode();
        e.Handled = true;
    }

    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_activeSplitIntent is null || e.Key != Key.Escape)
            return;

        CancelSplitMode();
        e.Handled = true;
    }

    void UpdateSplitPreview(WorkspaceRegionChrome chrome, Point localPoint)
    {
        if (_activeSplitIntent is not { } intent)
            return;

        var width = chrome.Bounds.Width;
        var height = chrome.Bounds.Height;
        if (width <= 0 || height <= 0)
            return;

        _splitTarget = chrome;
        _splitRatio = WorkspaceLayoutCommands.ClampSplitRatio(
            intent == WorkspaceSplitIntent.Vertical
                ? localPoint.X / width
                : localPoint.Y / height);

        if (chrome.TranslatePoint(new Point(0, 0), RootPanel) is not { } origin)
            return;

        if (intent == WorkspaceSplitIntent.Vertical)
        {
            SplitPreviewLine.Width = SplitPreviewThickness;
            SplitPreviewLine.Height = height;
            SplitPreviewLine.Margin = new Thickness(
                origin.X + width * _splitRatio - SplitPreviewThickness / 2,
                origin.Y,
                0,
                0);
        }
        else
        {
            SplitPreviewLine.Width = width;
            SplitPreviewLine.Height = SplitPreviewThickness;
            SplitPreviewLine.Margin = new Thickness(
                origin.X,
                origin.Y + height * _splitRatio - SplitPreviewThickness / 2,
                0,
                0);
        }

        SplitPreviewLine.IsVisible = true;
    }

    void OnSplit(WorkspaceNode node, WorkspaceSplitOrientation orientation, double ratio)
    {
        if (_factory is null)
            return;

        if (!WorkspaceLayoutCommands.TrySplitRegion(_root, node, orientation, ratio, out var newRoot))
            return;

        _root = newRoot;
        CancelSplitMode();
        PersistCurrentLayout();
        WorkspaceDragBroker.Clear();
        Rebuild();
    }

    void OnJoin(WorkspaceNode node)
    {
        if (_factory is null)
            return;

        if (!WorkspaceLayoutCommands.TryJoinRegion(_root, node, out var newRoot))
            return;

        _root = newRoot;
        PersistCurrentLayout();
        WorkspaceDragBroker.Clear();
        Rebuild();
    }

    void OnChangeEditor(WorkspaceRegionChrome chrome, WorkspaceEditorId id)
    {
        if (chrome.LayoutNode is not { } node || _factory is null)
            return;

        if (node is WorkspaceLeaf leaf && leaf.Editor == id)
            return;
        if (node is WorkspaceTabGroup && id == WorkspaceEditorId.TabGroup)
            return;

        WorkspaceNode replacement = id == WorkspaceEditorId.TabGroup
            ? new WorkspaceTabGroup()
            : new WorkspaceLeaf { Editor = id };

        if (!WorkspaceLayoutCommands.TryReplaceRegion(_root, node, replacement, out var newRoot))
            return;

        _root = newRoot;
        PersistCurrentLayout();
        Rebuild();
    }

    void OnDropSwap(WorkspaceRegionChrome target)
    {
        if (WorkspaceDragBroker.Source is not { } source || ReferenceEquals(source, target) || _factory is null)
            return;

        if (source.LayoutNode is { } sourceNode
            && target.LayoutNode is { } targetNode
            && WorkspaceLayoutCommands.TrySwapRegions(_root, sourceNode, targetNode, out var newRoot))
        {
            _root = newRoot;
            PersistCurrentLayout();
            Rebuild();
        }

        WorkspaceDragBroker.Clear();
    }

    void ApplyLeafToChrome(WorkspaceLeaf leaf, WorkspaceRegionChrome chrome)
    {
        chrome.EditorId = leaf.Editor;
        var content = _factory!.GetOrCreate(leaf);
        chrome.SetEditorContent(content);
        if (content is RenderControl viewerControl)
            viewerControl.TryLoadProgramIfVisible();
        chrome.RefreshTitle();
    }

    void OnSplitterResizeCompleted()
    {
        PersistCurrentLayout();
    }

    void PersistCurrentLayout()
    {
        WorkspaceLayoutService.SaveRoot(_root);
        WorkspaceLayoutService.Persist();
    }

    void SyncActiveEditors()
    {
        var activated = _root.EnumerateEditors().Distinct().ToList();
        foreach (var leaf in _root.EnumerateLeaves())
        {
            var control = _factory!.GetOrCreate(leaf);
            var desc = WorkspaceEditorCatalog.Get(leaf.Editor);
            if (desc.SupportsActivation)
                WorkspaceEditorFactory.SetActivation(control, true);
        }

        _activeEditors = _root.EnumerateEditors().ToHashSet();
        ActiveEditorsChanged?.Invoke(this, activated);
    }

    void RefreshToolpathViews()
    {
        if (_factory is null)
            return;

        foreach (var control in _factory.AllCached)
        {
            if (control is RenderControl viewer)
                viewer.TryLoadProgramIfVisible();
        }
    }

    void QueueToolpathRefresh()
    {
        var version = ++_toolpathRefreshVersion;
        Dispatcher.UIThread.Post(() =>
        {
            if (version != _toolpathRefreshVersion)
                return;

#if DEBUG
            var watch = Stopwatch.StartNew();
#endif
            RefreshToolpathViews();
#if DEBUG
            watch.Stop();
            Trace.WriteLine($"Workspace queued toolpath refresh completed in {watch.ElapsedMilliseconds} ms");
#endif
        }, DispatcherPriority.ApplicationIdle);
    }

    public void CancelToolpathViews()
    {
        _toolpathRefreshVersion++;
        if (_factory is null)
            return;

        foreach (var control in _factory.AllCached)
        {
            if (control is RenderControl viewer)
                viewer.CancelPreviewBuild();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != IsEditModeProperty || !IsLayoutReady)
            return;

        foreach (var chrome in _regionChromes)
            chrome.IsEditMode = IsEditMode;
        _builder?.SetEditMode(IsEditMode);
        if (!IsEditMode)
            CancelSplitMode();
    }
}
