using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using CNC.App.Workspace;
using CNC.Controls.Avalonia.Views;
using CNC.Localization.Avalonia;
using ioSender.Workspace.Editors;

namespace ioSender.Workspace.Controls;

public partial class WorkspaceRegionChrome : Border
{
    public static readonly StyledProperty<WorkspaceEditorId> EditorIdProperty =
        AvaloniaProperty.Register<WorkspaceRegionChrome, WorkspaceEditorId>(nameof(EditorId));

    public static readonly StyledProperty<bool> IsEditModeProperty =
        AvaloniaProperty.Register<WorkspaceRegionChrome, bool>(nameof(IsEditMode));

    public event EventHandler<WorkspaceRegionChrome>? DragStarted;
    public event EventHandler<WorkspaceRegionChrome>? DropCompleted;

    public event EventHandler? SplitHorizontalRequested;
    public event EventHandler? SplitVerticalRequested;
    public event EventHandler? JoinRequested;
    public event EventHandler? LockSettingsChanged;
    public event EventHandler<WorkspaceEditorId>? ChangeEditorRequested;

    bool _isDropTarget;
    ContextMenu? _editContextMenu;
    MenuItem? _lockWidthItem;
    MenuItem? _lockHeightItem;
    JogControl? _jogHeaderStatusSource;
    WorkspaceNode? _layoutNode;

    public WorkspaceRegionChrome()
    {
        InitializeComponent();
        BuildContextMenu();
        TitleBar.PointerPressed += OnTitleBarPointerPressed;
        TitleBar.PointerReleased += OnTitleBarPointerReleased;
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
    }

    public WorkspaceEditorId EditorId
    {
        get => GetValue(EditorIdProperty);
        set => SetValue(EditorIdProperty, value);
    }

    public bool IsEditMode
    {
        get => GetValue(IsEditModeProperty);
        set => SetValue(IsEditModeProperty, value);
    }

    public WorkspaceNode? LayoutNode
    {
        get => _layoutNode;
        set
        {
            _layoutNode = value;
            UpdateLockIndicators();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EditorIdProperty)
        {
            RefreshTitle();
            UpdateTitleBarVisibility();
        }
        if (change.Property == IsEditModeProperty)
        {
            UpdateEditChrome();
            UpdateTitleBarVisibility();
        }
    }

    public static void DetachEditor(Control content)
    {
        while (content.Parent is Control parent)
        {
            var detached = false;
            switch (parent)
            {
                case Panel panel:
                    panel.Children.Remove(content);
                    detached = true;
                    break;
                case ContentControl cc when ReferenceEquals(cc.Content, content):
                    cc.Content = null;
                    detached = true;
                    break;
                case Decorator decorator when ReferenceEquals(decorator.Child, content):
                    decorator.Child = null;
                    detached = true;
                    break;
            }

            if (!detached)
                break;
        }
    }

    public void ClearEditorHost()
    {
        DetachHeaderStatusSource();
        if (EditorHost.Content is WorkspaceTabGroupControl tabGroup)
            tabGroup.ReleaseActiveEditor();
        if (EditorHost.Content is Control previous)
            DetachEditor(previous);
        EditorHost.Content = null;
    }

    public void SetEditorContent(Control content)
    {
        if (EditorHost.Content is Control previous && !ReferenceEquals(previous, content))
        {
            if (previous is WorkspaceTabGroupControl tabGroup)
                tabGroup.ReleaseActiveEditor();
            DetachEditor(previous);
        }
        DetachEditor(content);
        EditorHost.Content = content;
        AttachHeaderStatusSource(content);

        var fills = WorkspaceEditorCatalog.Get(EditorId).FillsWorkspace;
        content.HorizontalAlignment = fills
            ? Avalonia.Layout.HorizontalAlignment.Stretch
            : Avalonia.Layout.HorizontalAlignment.Left;
        content.VerticalAlignment = fills
            ? Avalonia.Layout.VerticalAlignment.Stretch
            : Avalonia.Layout.VerticalAlignment.Top;

        if (fills && content is Layoutable layoutable)
        {
            layoutable.Width = double.NaN;
            layoutable.Height = double.NaN;
        }

        if (fills)
        {
            EditorScroll.HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
            EditorScroll.VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled;
            EditorHost.Margin = new Thickness(0);
        }
        else
        {
            EditorScroll.HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
            EditorScroll.VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
            EditorHost.Margin = new Thickness(2);
        }
    }

    public void RefreshTitle()
    {
        var desc = WorkspaceEditorCatalog.Get(EditorId);
        TitleText.Text = Localize.T(desc.TitleKey, desc.TitleFallback);
    }

    public void SetTitleText(string text)
    {
        TitleText.Text = text;
    }

    void UpdateEditChrome()
    {
        EditHint.IsVisible = IsEditMode;
        UpdateLockIndicators();
        Classes.Set("workspace-region-edit", IsEditMode);
        TitleBar.ContextMenu = IsEditMode ? _editContextMenu : null;
    }

    void UpdateLockIndicators()
    {
        var hasWidthLock = IsEditMode && (LayoutNode?.LockedWidth ?? 0) > 0;
        var hasHeightLock = IsEditMode && (LayoutNode?.LockedHeight ?? 0) > 0;

        WidthLockIndicator.IsVisible = hasWidthLock;
        HeightLockIndicator.IsVisible = hasHeightLock;
        LockIndicators.IsVisible = hasWidthLock || hasHeightLock;
    }

    void AttachHeaderStatusSource(Control content)
    {
        DetachHeaderStatusSource();

        if (content is not JogControl jog)
        {
            UpdateHeaderStatus(string.Empty, false);
            return;
        }

        _jogHeaderStatusSource = jog;
        jog.HeaderStatusChanged += OnJogHeaderStatusChanged;
        RefreshJogHeaderStatus();
    }

    void DetachHeaderStatusSource()
    {
        if (_jogHeaderStatusSource != null)
            _jogHeaderStatusSource.HeaderStatusChanged -= OnJogHeaderStatusChanged;
        _jogHeaderStatusSource = null;
        UpdateHeaderStatus(string.Empty, false);
    }

    void OnJogHeaderStatusChanged() => RefreshJogHeaderStatus();

    void RefreshJogHeaderStatus()
    {
        if (_jogHeaderStatusSource == null)
        {
            UpdateHeaderStatus(string.Empty, false);
            return;
        }

        UpdateHeaderStatus(_jogHeaderStatusSource.HeaderStatusText, _jogHeaderStatusSource.IsHeaderStatusVisible);
    }

    void UpdateHeaderStatus(string text, bool visible)
    {
        HeaderStatusText.Text = text;
        HeaderStatusText.IsVisible = visible;
    }

    void UpdateTitleBarVisibility()
    {
        TitleBar.IsVisible = true;
    }

    void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsEditMode)
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        WorkspaceDragBroker.Source = this;
        DragStarted?.Invoke(this, this);
        e.Pointer.Capture(TitleBar);
        e.Handled = true;
    }

    void OnTitleBarPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!IsEditMode)
            return;

        if (_isDropTarget && WorkspaceDragBroker.Source is { } source && !ReferenceEquals(source, this))
            DropCompleted?.Invoke(this, this);

        ClearDropTarget();
        WorkspaceDragBroker.Clear();
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (!IsEditMode || WorkspaceDragBroker.Source is not { } source || ReferenceEquals(source, this))
            return;

        _isDropTarget = true;
        Classes.Add("workspace-drop-target");
    }

    void OnPointerExited(object? sender, PointerEventArgs e) => ClearDropTarget();

    public void ClearDropTarget()
    {
        if (!_isDropTarget)
            return;
        _isDropTarget = false;
        Classes.Remove("workspace-drop-target");
    }

    public void BuildContextMenu()
    {
        var menu = new ContextMenu();
        menu.Opened += (_, _) => UpdateLockMenuItems();
        menu.Items.Add(MakeItem("Split horizontally", () => RequestSplit(SplitHorizontalRequested)));
        menu.Items.Add(MakeItem("Split vertically", () => RequestSplit(SplitVerticalRequested)));
        menu.Items.Add(MakeItem("Join with neighbor", () => RequestJoin()));
        menu.Items.Add(new Separator());
        _lockWidthItem = MakeItem(string.Empty, ToggleLockedWidth);
        _lockHeightItem = MakeItem(string.Empty, ToggleLockedHeight);
        menu.Items.Add(_lockWidthItem);
        menu.Items.Add(_lockHeightItem);
        menu.Items.Add(new Separator());

        var change = new MenuItem { Header = "Change panel" };
        foreach (var desc in WorkspaceEditorCatalog.LayoutPickableDescriptors)
        {
            var id = desc.Id;
            change.Items.Add(MakeItem(
                Localize.T(desc.TitleKey, desc.TitleFallback),
                () => ChangeEditorRequested?.Invoke(this, id)));
        }
        menu.Items.Add(change);
        _editContextMenu = menu;
        ContextMenu = null;
        TitleBar.ContextMenu = IsEditMode ? _editContextMenu : null;
        UpdateLockMenuItems();
    }

    void RequestSplit(EventHandler? handler)
    {
        CloseTitleBarMenu();
        Dispatcher.UIThread.Post(
            () => handler?.Invoke(this, EventArgs.Empty),
            DispatcherPriority.Loaded);
    }

    void RequestJoin()
    {
        CloseTitleBarMenu();
        Dispatcher.UIThread.Post(
            () => JoinRequested?.Invoke(this, EventArgs.Empty),
            DispatcherPriority.Loaded);
    }

    void CloseTitleBarMenu()
    {
        if (TitleBar.ContextMenu is { } menu)
            menu.Close();
    }

    void ToggleLockedWidth()
    {
        if (LayoutNode is not { } node)
            return;

        node.LockedWidth = node.LockedWidth > 0 ? 0 : Bounds.Width;
        UpdateLockMenuItems();
        CloseTitleBarMenu();
        Dispatcher.UIThread.Post(
            () => LockSettingsChanged?.Invoke(this, EventArgs.Empty),
            DispatcherPriority.Loaded);
    }

    void ToggleLockedHeight()
    {
        if (LayoutNode is not { } node)
            return;

        node.LockedHeight = node.LockedHeight > 0 ? 0 : Bounds.Height;
        UpdateLockMenuItems();
        CloseTitleBarMenu();
        Dispatcher.UIThread.Post(
            () => LockSettingsChanged?.Invoke(this, EventArgs.Empty),
            DispatcherPriority.Loaded);
    }

    void UpdateLockMenuItems()
    {
        if (_lockWidthItem is not null)
            _lockWidthItem.Header = FormatLockHeader("Lock width", LayoutNode?.LockedWidth ?? 0);
        if (_lockHeightItem is not null)
            _lockHeightItem.Header = FormatLockHeader("Lock height", LayoutNode?.LockedHeight ?? 0);
        UpdateLockIndicators();
    }

    static string FormatLockHeader(string label, double value) =>
        value > 0
            ? $"[x] {label} ({Math.Round(value)} px)"
            : label;

    static MenuItem MakeItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }
}
