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
    const double SplitterVisualThickness = 1;
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
        return BuildNode(root, editMode, wireRegion).Control;
    }

    LayoutBuildResult BuildNode(WorkspaceNode node, bool editMode, Action<WorkspaceRegionChrome>? wireRegion)
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
            var first = BuildNode(split.First, editMode, wireRegion);
            var splitter = CreateSplitter(split, GridResizeDirection.Columns, editMode);
            var second = BuildNode(split.Second, editMode, wireRegion);

            var firstDefinition = new ColumnDefinition(GetGridLength(split.First, WorkspaceLockAxis.Width, ratio, first.Constraints.MinWidth));
            var secondDefinition = new ColumnDefinition(GetGridLength(split.Second, WorkspaceLockAxis.Width, 1 - ratio, second.Constraints.MinWidth));
            ApplyColumnConstraints(firstDefinition, first.Constraints);
            ApplyColumnConstraints(secondDefinition, second.Constraints);
            grid.ColumnDefinitions.Add(firstDefinition);
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto)
            {
                MinWidth = SplitterVisualThickness,
                MaxWidth = SplitterVisualThickness,
            });
            grid.ColumnDefinitions.Add(secondDefinition);

            Grid.SetColumn(first.Control, 0);
            Grid.SetColumn(splitter, 1);
            Grid.SetColumn(second.Control, 2);
            grid.Children.Add(first.Control);
            grid.Children.Add(second.Control);
            grid.Children.Add(splitter);

            ApplyConstraints(grid, WorkspaceLayoutConstraints.ForHorizontalSplit(
                first.Constraints,
                second.Constraints,
                SplitterVisualThickness));
        }
        else
        {
            var first = BuildNode(split.First, editMode, wireRegion);
            var splitter = CreateSplitter(split, GridResizeDirection.Rows, editMode);
            var second = BuildNode(split.Second, editMode, wireRegion);

            var firstDefinition = new RowDefinition(GetGridLength(split.First, WorkspaceLockAxis.Height, ratio, first.Constraints.MinHeight));
            var secondDefinition = new RowDefinition(GetGridLength(split.Second, WorkspaceLockAxis.Height, 1 - ratio, second.Constraints.MinHeight));
            ApplyRowConstraints(firstDefinition, first.Constraints);
            ApplyRowConstraints(secondDefinition, second.Constraints);
            grid.RowDefinitions.Add(firstDefinition);
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)
            {
                MinHeight = SplitterVisualThickness,
                MaxHeight = SplitterVisualThickness,
            });
            grid.RowDefinitions.Add(secondDefinition);

            Grid.SetRow(first.Control, 0);
            Grid.SetRow(splitter, 1);
            Grid.SetRow(second.Control, 2);
            grid.Children.Add(first.Control);
            grid.Children.Add(second.Control);
            grid.Children.Add(splitter);

            ApplyConstraints(grid, WorkspaceLayoutConstraints.ForVerticalSplit(
                first.Constraints,
                second.Constraints,
                SplitterVisualThickness));
        }

        return new LayoutBuildResult(grid, WorkspaceLayoutConstraints.FromControl(grid));
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
        var line = new Border
        {
            Width = isColumnSplitter ? 1 : double.NaN,
            Height = isColumnSplitter ? double.NaN : 1,
            HorizontalAlignment = isColumnSplitter ? HorizontalAlignment.Center : HorizontalAlignment.Stretch,
            VerticalAlignment = isColumnSplitter ? VerticalAlignment.Stretch : VerticalAlignment.Center,
        };
        line.Classes.Add("workspace-splitter-line");

        var target = new Grid
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
        target.Children.Add(line);
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

    LayoutBuildResult BuildLeaf(WorkspaceLeaf leaf, bool editMode, Action<WorkspaceRegionChrome>? wireRegion)
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
        return new LayoutBuildResult(chrome, WorkspaceLayoutConstraints.FromControl(chrome));
    }

    LayoutBuildResult BuildTabGroup(WorkspaceTabGroup tabGroup, bool editMode, Action<WorkspaceRegionChrome>? wireRegion)
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
        return new LayoutBuildResult(chrome, WorkspaceLayoutConstraints.FromControl(chrome));
    }

    static double ClampRatio(double ratio) => Math.Clamp(ratio, 0.08, 0.92);

    static GridLength GetGridLength(WorkspaceNode node, WorkspaceLockAxis axis, double starValue, double minimum)
    {
        var lockedSize = WorkspaceLayoutLocks.ResolveLockedSize(node, axis);
        return lockedSize > 0
            ? new GridLength(Math.Max(lockedSize, minimum), GridUnitType.Pixel)
            : new GridLength(starValue, GridUnitType.Star);
    }

    static bool HasLockedBranch(WorkspaceSplit split)
    {
        var axis = split.Orientation == WorkspaceSplitOrientation.Horizontal
            ? WorkspaceLockAxis.Width
            : WorkspaceLockAxis.Height;
        return WorkspaceLayoutLocks.ContainsLockedSize(split.First, axis)
            || WorkspaceLayoutLocks.ContainsLockedSize(split.Second, axis);
    }

    static void ApplyColumnConstraints(ColumnDefinition definition, WorkspaceLayoutConstraints constraints)
    {
        definition.MinWidth = constraints.MinWidth;
        if (IsFinite(constraints.MaxWidth))
            definition.MaxWidth = Math.Max(constraints.MaxWidth, constraints.MinWidth);
    }

    static void ApplyRowConstraints(RowDefinition definition, WorkspaceLayoutConstraints constraints)
    {
        definition.MinHeight = constraints.MinHeight;
        if (IsFinite(constraints.MaxHeight))
            definition.MaxHeight = Math.Max(constraints.MaxHeight, constraints.MinHeight);
    }

    static void ApplyConstraints(Layoutable target, WorkspaceLayoutConstraints constraints)
    {
        target.MinWidth = constraints.MinWidth;
        target.MinHeight = constraints.MinHeight;
        if (IsFinite(constraints.MaxWidth))
            target.MaxWidth = Math.Max(constraints.MaxWidth, constraints.MinWidth);
        if (IsFinite(constraints.MaxHeight))
            target.MaxHeight = Math.Max(constraints.MaxHeight, constraints.MinHeight);
    }

    static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    static MenuItem MakeItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        return item;
    }

    readonly record struct LayoutBuildResult(Control Control, WorkspaceLayoutConstraints Constraints);

    readonly record struct WorkspaceLayoutConstraints(
        double MinWidth,
        double MinHeight,
        double MaxWidth,
        double MaxHeight)
    {
        public static WorkspaceLayoutConstraints FromControl(Layoutable control) =>
            new(
                NormalizeMin(control.MinWidth),
                NormalizeMin(control.MinHeight),
                NormalizeMax(control.MaxWidth),
                NormalizeMax(control.MaxHeight));

        public static WorkspaceLayoutConstraints ForHorizontalSplit(
            WorkspaceLayoutConstraints first,
            WorkspaceLayoutConstraints second,
            double splitterSize) =>
            new(
                first.MinWidth + splitterSize + second.MinWidth,
                Math.Max(first.MinHeight, second.MinHeight),
                SumMax(first.MaxWidth, splitterSize, second.MaxWidth),
                double.PositiveInfinity);

        public static WorkspaceLayoutConstraints ForVerticalSplit(
            WorkspaceLayoutConstraints first,
            WorkspaceLayoutConstraints second,
            double splitterSize) =>
            new(
                Math.Max(first.MinWidth, second.MinWidth),
                first.MinHeight + splitterSize + second.MinHeight,
                double.PositiveInfinity,
                SumMax(first.MaxHeight, splitterSize, second.MaxHeight));

        static double NormalizeMin(double value) =>
            IsFinite(value) && value > 0 ? value : 0;

        static double NormalizeMax(double value) =>
            IsFinite(value) && value >= 0 ? value : double.PositiveInfinity;

        static double SumMax(double first, double splitterSize, double second) =>
            IsFinite(first) && IsFinite(second)
                ? first + splitterSize + second
                : double.PositiveInfinity;

    }
}
