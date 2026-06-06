using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using CNC.App.Workspace;
using ioSender.Workspace.Controls;
using ioSender.Workspace.Editors;

namespace ioSender.Workspace;

public sealed class SplitLayoutBuilder
{
    readonly WorkspaceEditorFactory _factory;
    readonly Action? _onSplitterResizeCompleted;
    readonly Action? _onLayoutPersistRequested;
    readonly Action? _onActiveEditorsChanged;
    readonly Action<WorkspaceSplitIntent>? _onSplitModeRequested;
    readonly Dictionary<WorkspaceSplit, WorkspaceGridSplitter> _splitters = new();
    readonly List<Control> _splitterMenuTargets = new();
    readonly Dictionary<WorkspaceNode, WorkspaceRegionChrome> _regions = new();
    const double SplitterHitTargetThickness = 5;
    const double SplitterOverlap = (SplitterHitTargetThickness - 1) / 2;

    public SplitLayoutBuilder(
        WorkspaceEditorFactory factory,
        Action? onSplitterResizeCompleted = null,
        Action? onLayoutPersistRequested = null,
        Action? onActiveEditorsChanged = null,
        Action<WorkspaceSplitIntent>? onSplitModeRequested = null)
    {
        _factory = factory;
        _onSplitterResizeCompleted = onSplitterResizeCompleted;
        _onLayoutPersistRequested = onLayoutPersistRequested;
        _onActiveEditorsChanged = onActiveEditorsChanged;
        _onSplitModeRequested = onSplitModeRequested;
    }

    public IReadOnlyDictionary<WorkspaceNode, WorkspaceRegionChrome> Regions => _regions;

    public Control Build(WorkspaceNode root, bool editMode, Action<WorkspaceRegionChrome>? wireRegion)
    {
        _splitters.Clear();
        _splitterMenuTargets.Clear();
        _regions.Clear();
        return BuildNode(root, editMode, wireRegion);
    }

    Control BuildNode(WorkspaceNode node, bool editMode, Action<WorkspaceRegionChrome>? wireRegion)
    {
        if (node is WorkspaceLeaf leaf)
            return BuildLeaf(leaf, editMode, wireRegion);

        if (node is WorkspaceTabGroup tabGroup)
            return BuildTabGroup(tabGroup, editMode, wireRegion);

        var split = node.AsSplit();
        var grid = new Grid();
        var ratio = ClampRatio(split.Ratio);

        if (split.Orientation == WorkspaceSplitOrientation.Horizontal)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GetGridLength(split.First, WorkspaceLockAxis.Width, ratio)));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GetGridLength(split.Second, WorkspaceLockAxis.Width, 1 - ratio)));

            var first = BuildNode(split.First, editMode, wireRegion);
            var splitter = CreateSplitter(split, GridResizeDirection.Columns, editMode);
            var second = BuildNode(split.Second, editMode, wireRegion);

            Grid.SetColumn(first, 0);
            Grid.SetColumn(splitter, 1);
            Grid.SetColumn(second, 2);
            grid.Children.Add(first);
            grid.Children.Add(second);
            grid.Children.Add(splitter);
        }
        else
        {
            grid.RowDefinitions.Add(new RowDefinition(GetGridLength(split.First, WorkspaceLockAxis.Height, ratio)));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.RowDefinitions.Add(new RowDefinition(GetGridLength(split.Second, WorkspaceLockAxis.Height, 1 - ratio)));

            var first = BuildNode(split.First, editMode, wireRegion);
            var splitter = CreateSplitter(split, GridResizeDirection.Rows, editMode);
            var second = BuildNode(split.Second, editMode, wireRegion);

            Grid.SetRow(first, 0);
            Grid.SetRow(splitter, 1);
            Grid.SetRow(second, 2);
            grid.Children.Add(first);
            grid.Children.Add(second);
            grid.Children.Add(splitter);
        }

        return grid;
    }

    Control CreateSplitter(WorkspaceSplit split, GridResizeDirection direction, bool editMode)
    {
        var isColumnSplitter = direction == GridResizeDirection.Columns;
        var hasLockedBranch = HasLockedBranch(split);
        if (hasLockedBranch)
        {
            var lockedTarget = CreateLockedSplitterMenuTarget(isColumnSplitter, editMode);
            _splitterMenuTargets.Add(lockedTarget);
            return lockedTarget;
        }

        var splitter = new WorkspaceGridSplitter
        {
            Width = isColumnSplitter ? SplitterHitTargetThickness : double.NaN,
            Height = isColumnSplitter ? double.NaN : SplitterHitTargetThickness,
            MinWidth = isColumnSplitter ? SplitterHitTargetThickness : 0,
            MinHeight = isColumnSplitter ? 0 : SplitterHitTargetThickness,
            MaxWidth = isColumnSplitter ? SplitterHitTargetThickness : double.PositiveInfinity,
            MaxHeight = isColumnSplitter ? double.PositiveInfinity : SplitterHitTargetThickness,
            HorizontalAlignment = isColumnSplitter ? HorizontalAlignment.Center : HorizontalAlignment.Stretch,
            VerticalAlignment = isColumnSplitter ? VerticalAlignment.Stretch : VerticalAlignment.Center,
            Margin = isColumnSplitter
                ? new Thickness(-SplitterOverlap, 0)
                : new Thickness(0, -SplitterOverlap),
            ResizeDirection = direction,
            IsEnabled = editMode,
            IsHitTestVisible = editMode,
            ContextMenu = editMode ? BuildSplitterContextMenu() : null,
        };
        splitter.Classes.Add("workspace-splitter");
        AttachSplitterContextMenu(splitter);

        _splitters[split] = splitter;
        _splitterMenuTargets.Add(splitter);
        splitter.DragDelta += (_, _) => UpdateRatioFromGrid(split, splitter);
        splitter.DragCompleted += (_, _) => _onSplitterResizeCompleted?.Invoke();
        return splitter;
    }

    Control CreateLockedSplitterMenuTarget(bool isColumnSplitter, bool editMode)
    {
        var target = new Border
        {
            Width = isColumnSplitter ? SplitterHitTargetThickness : double.NaN,
            Height = isColumnSplitter ? double.NaN : SplitterHitTargetThickness,
            MinWidth = isColumnSplitter ? SplitterHitTargetThickness : 0,
            MinHeight = isColumnSplitter ? 0 : SplitterHitTargetThickness,
            MaxWidth = isColumnSplitter ? SplitterHitTargetThickness : double.PositiveInfinity,
            MaxHeight = isColumnSplitter ? double.PositiveInfinity : SplitterHitTargetThickness,
            HorizontalAlignment = isColumnSplitter ? HorizontalAlignment.Center : HorizontalAlignment.Stretch,
            VerticalAlignment = isColumnSplitter ? VerticalAlignment.Stretch : VerticalAlignment.Center,
            Margin = isColumnSplitter
                ? new Thickness(-SplitterOverlap, 0)
                : new Thickness(0, -SplitterOverlap),
            Background = Avalonia.Media.Brushes.Transparent,
            IsHitTestVisible = editMode,
            ContextMenu = editMode ? BuildSplitterContextMenu() : null,
        };
        target.Classes.Add("workspace-splitter");
        AttachSplitterContextMenu(target);
        return target;
    }

    void AttachSplitterContextMenu(Control target)
    {
        target.AddHandler(
            InputElement.PointerPressedEvent,
            OnSplitterPointerPressed,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
    }

    void OnSplitterPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control target || target.ContextMenu is null)
            return;

        if (!e.GetCurrentPoint(target).Properties.IsRightButtonPressed)
            return;

        target.ContextMenu.Open(target);
        e.Handled = true;
    }

    ContextMenu BuildSplitterContextMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(MakeItem("Split Vertically", () => _onSplitModeRequested?.Invoke(WorkspaceSplitIntent.Vertical)));
        menu.Items.Add(MakeItem("Split Horizontally", () => _onSplitModeRequested?.Invoke(WorkspaceSplitIntent.Horizontal)));
        return menu;
    }

    public void SetEditMode(bool editMode)
    {
        foreach (var splitter in _splitters.Values)
        {
            splitter.IsEnabled = editMode;
            splitter.IsHitTestVisible = editMode;
        }

        foreach (var target in _splitterMenuTargets)
        {
            target.IsHitTestVisible = editMode;
            target.ContextMenu = editMode ? BuildSplitterContextMenu() : null;
        }
    }

    void UpdateRatioFromGrid(WorkspaceSplit split, WorkspaceGridSplitter splitter)
    {
        if (splitter.Parent is not Grid grid)
            return;

        if (split.Orientation == WorkspaceSplitOrientation.Horizontal)
        {
            var col = Grid.GetColumn(splitter);
            if (col != 1)
                return;
            var firstCol = grid.ColumnDefinitions[0];
            var secondCol = grid.ColumnDefinitions[2];
            var firstWidth = firstCol.ActualWidth;
            var total = firstWidth + secondCol.ActualWidth;
            if (total <= 0)
                return;
            split.Ratio = ClampRatio(firstWidth / total);
        }
        else
        {
            var firstRow = grid.RowDefinitions[0];
            var secondRow = grid.RowDefinitions[2];
            var firstHeight = firstRow.ActualHeight;
            var total = firstHeight + secondRow.ActualHeight;
            if (total <= 0)
                return;
            split.Ratio = ClampRatio(firstHeight / total);
        }
    }

    WorkspaceRegionChrome BuildLeaf(WorkspaceLeaf leaf, bool editMode, Action<WorkspaceRegionChrome>? wireRegion)
    {
        var chrome = new WorkspaceRegionChrome
        {
            EditorId = leaf.Editor,
            LayoutNode = leaf,
            IsEditMode = editMode,
        };
        chrome.RefreshTitle();

        var editor = _factory.GetOrCreate(leaf);
        WorkspaceRegionChrome.DetachEditor(editor);
        chrome.SetEditorContent(editor);

        _regions[leaf] = chrome;
        wireRegion?.Invoke(chrome);
        return chrome;
    }

    WorkspaceRegionChrome BuildTabGroup(WorkspaceTabGroup tabGroup, bool editMode, Action<WorkspaceRegionChrome>? wireRegion)
    {
        var chrome = new WorkspaceRegionChrome
        {
            EditorId = WorkspaceEditorId.TabGroup,
            LayoutNode = tabGroup,
            IsEditMode = editMode,
        };
        chrome.RefreshTitle();

        var control = new WorkspaceTabGroupControl(
            tabGroup,
            _factory,
            () => _onLayoutPersistRequested?.Invoke(),
            () => _onActiveEditorsChanged?.Invoke(),
            chrome.SetTitleText);
        chrome.SetEditorContent(control);

        _regions[tabGroup] = chrome;
        wireRegion?.Invoke(chrome);
        return chrome;
    }

    static double ClampRatio(double ratio) => Math.Clamp(ratio, 0.08, 0.92);

    static GridLength GetGridLength(WorkspaceNode node, WorkspaceLockAxis axis, double starValue)
    {
        var lockedSize = WorkspaceLayoutLocks.ResolveLockedSize(node, axis);
        return lockedSize > 0
            ? new GridLength(lockedSize, GridUnitType.Pixel)
            : new GridLength(starValue, GridUnitType.Star);
    }

    static bool HasLockedBranch(WorkspaceSplit split)
    {
        var axis = split.Orientation == WorkspaceSplitOrientation.Horizontal
            ? WorkspaceLockAxis.Width
            : WorkspaceLockAxis.Height;
        return WorkspaceLayoutLocks.ResolveLockedSize(split.First, axis) > 0
            || WorkspaceLayoutLocks.ResolveLockedSize(split.Second, axis) > 0;
    }

    static MenuItem MakeItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }
}
