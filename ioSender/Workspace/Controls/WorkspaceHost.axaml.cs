using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
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

    public event EventHandler? LayoutChanged;
    public event EventHandler<IReadOnlyCollection<WorkspaceEditorId>>? ActiveEditorsChanged;

    public WorkspaceHost()
    {
        InitializeComponent();
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

    void OnProgramChanged() => RefreshToolpathViews();

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
            SyncActiveEditors);
        var content = _builder.Build(_root, IsEditMode, chrome =>
        {
            WireRegion(chrome);
            _regionChromes.Add(chrome);
        });
        RootPanel.Children.Add(content);
        SyncActiveEditors();
        RefreshToolpathViews();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    void WireRegion(WorkspaceRegionChrome chrome)
    {
        chrome.SplitHorizontalRequested += (_, _) =>
        {
            if (chrome.LayoutNode is { } node)
                OnSplit(node, WorkspaceSplitOrientation.Horizontal);
        };
        chrome.SplitVerticalRequested += (_, _) =>
        {
            if (chrome.LayoutNode is { } node)
                OnSplit(node, WorkspaceSplitOrientation.Vertical);
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
    }

    void OnSplit(WorkspaceNode node, WorkspaceSplitOrientation orientation)
    {
        if (_factory is null)
            return;

        if (!WorkspaceLayoutCommands.TrySplitRegion(_root, node, orientation, out var newRoot))
            return;

        _root = newRoot;
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
            viewerControl.TryLoadProgram();
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
                viewer.TryLoadProgram();
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
    }
}
