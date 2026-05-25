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

    public void ResetToDefault() => ApplyPreset(WorkspaceLayoutDefaults.PresetCompact);

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
        _builder = new SplitLayoutBuilder(_factory, OnSplitterResizeCompleted);
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
            if (chrome.LayoutNode is WorkspaceLeaf leaf)
                OnSplit(leaf, WorkspaceSplitOrientation.Horizontal);
        };
        chrome.SplitVerticalRequested += (_, _) =>
        {
            if (chrome.LayoutNode is WorkspaceLeaf leaf)
                OnSplit(leaf, WorkspaceSplitOrientation.Vertical);
        };
        chrome.JoinRequested += (_, _) =>
        {
            if (chrome.LayoutNode is WorkspaceLeaf leaf)
                OnJoin(leaf);
        };
        chrome.ChangeEditorRequested += (_, id) => OnChangeEditor(chrome, id);
        chrome.DropCompleted += (_, target) => OnDropSwap(target);
    }

    void OnSplit(WorkspaceLeaf leaf, WorkspaceSplitOrientation orientation)
    {
        if (_factory is null)
            return;

        if (!WorkspaceLayoutCommands.TrySplitLeaf(_root, leaf, orientation, out var newRoot))
            return;

        _root = newRoot;
        WorkspaceLayoutService.SaveRoot(_root);
        WorkspaceLayoutService.Persist();
        WorkspaceDragBroker.Clear();
        Rebuild();
    }

    void OnJoin(WorkspaceLeaf leaf)
    {
        if (_factory is null)
            return;

        if (!WorkspaceLayoutCommands.TryJoinLeaf(_root, leaf, out var newRoot))
            return;

        _factory.Remove(leaf);
        _root = newRoot;
        WorkspaceLayoutService.SaveRoot(_root);
        WorkspaceLayoutService.Persist();
        WorkspaceDragBroker.Clear();
        Rebuild();
    }

    void OnChangeEditor(WorkspaceRegionChrome chrome, WorkspaceEditorId id)
    {
        if (chrome.LayoutNode is not WorkspaceLeaf leaf || leaf.Editor == id || _factory is null)
            return;

        leaf.Editor = id;
        ApplyLeafToChrome(leaf, chrome);

        WorkspaceLayoutService.SaveRoot(_root);
        WorkspaceLayoutService.Persist();
        SyncActiveEditors();
    }

    void OnDropSwap(WorkspaceRegionChrome target)
    {
        if (WorkspaceDragBroker.Source is not { } source || ReferenceEquals(source, target) || _factory is null)
            return;

        if (source.LayoutNode is WorkspaceLeaf sourceLeaf
            && target.LayoutNode is WorkspaceLeaf targetLeaf)
        {
            WorkspaceLayoutCommands.SwapEditors(sourceLeaf, targetLeaf);
            ApplyLeafToChrome(sourceLeaf, source);
            ApplyLeafToChrome(targetLeaf, target);
            WorkspaceLayoutService.SaveRoot(_root);
            WorkspaceLayoutService.Persist();
            SyncActiveEditors();
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
        WorkspaceLayoutService.SaveRoot(_root);
        WorkspaceLayoutService.Persist();
    }

    void SyncActiveEditors()
    {
        var activated = new List<WorkspaceEditorId>();
        foreach (var leaf in _root.EnumerateLeaves())
        {
            var control = _factory!.GetOrCreate(leaf);
            var desc = WorkspaceEditorCatalog.Get(leaf.Editor);
            if (desc.SupportsActivation)
                WorkspaceEditorFactory.SetActivation(control, true);
            activated.Add(leaf.Editor);
        }

        _activeEditors = _root.EnumerateEditors().ToHashSet();
        ActiveEditorsChanged?.Invoke(this, activated.Distinct().ToList());
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
