using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using CNC.App.Workspace;
using ioSender.Workspace.Controls;
using ioSender.Workspace.Editors;

namespace ioSender.Workspace;

public sealed class SplitLayoutBuilder
{
    readonly WorkspaceEditorFactory _factory;
    readonly Action? _onSplitterResizeCompleted;
    readonly Dictionary<WorkspaceSplit, WorkspaceGridSplitter> _splitters = new();
    readonly Dictionary<WorkspaceLeaf, WorkspaceRegionChrome> _regions = new();
    const double SplitterHitTargetThickness = 5;
    const double SplitterOverlap = (SplitterHitTargetThickness - 1) / 2;

    public SplitLayoutBuilder(WorkspaceEditorFactory factory, Action? onSplitterResizeCompleted = null)
    {
        _factory = factory;
        _onSplitterResizeCompleted = onSplitterResizeCompleted;
    }

    public IReadOnlyDictionary<WorkspaceLeaf, WorkspaceRegionChrome> Regions => _regions;

    public Control Build(WorkspaceNode root, bool editMode, Action<WorkspaceRegionChrome>? wireRegion)
    {
        _splitters.Clear();
        _regions.Clear();
        return BuildNode(root, editMode, wireRegion);
    }

    Control BuildNode(WorkspaceNode node, bool editMode, Action<WorkspaceRegionChrome>? wireRegion)
    {
        if (node is WorkspaceLeaf leaf)
            return BuildLeaf(leaf, editMode, wireRegion);

        var split = node.AsSplit();
        var grid = new Grid();
        var ratio = ClampRatio(split.Ratio);

        if (split.Orientation == WorkspaceSplitOrientation.Horizontal)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(ratio, GridUnitType.Star)));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1 - ratio, GridUnitType.Star)));

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
            grid.RowDefinitions.Add(new RowDefinition(new GridLength(ratio, GridUnitType.Star)));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.RowDefinitions.Add(new RowDefinition(new GridLength(1 - ratio, GridUnitType.Star)));

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

    WorkspaceGridSplitter CreateSplitter(WorkspaceSplit split, GridResizeDirection direction, bool editMode)
    {
        var isColumnSplitter = direction == GridResizeDirection.Columns;
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
        };
        splitter.Classes.Add("workspace-splitter");

        _splitters[split] = splitter;
        splitter.DragDelta += (_, _) => UpdateRatioFromGrid(split, splitter);
        splitter.DragCompleted += (_, _) => _onSplitterResizeCompleted?.Invoke();
        return splitter;
    }

    public void SetEditMode(bool editMode)
    {
        foreach (var splitter in _splitters.Values)
        {
            splitter.IsEnabled = editMode;
            splitter.IsHitTestVisible = editMode;
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

    static double ClampRatio(double ratio) => Math.Clamp(ratio, 0.08, 0.92);
}
