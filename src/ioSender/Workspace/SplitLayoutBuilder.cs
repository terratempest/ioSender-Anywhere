using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using CNC.App.Workspace;
using ioSender.Workspace.Controls;
using ioSender.Workspace.Editors;

namespace ioSender.Workspace;

public sealed class SplitLayoutBuilder
{
    readonly WorkspaceEditorFactory _factory;
    readonly Action? _onSplitterResizeCompleted;
    readonly Dictionary<WorkspaceSplit, GridSplitter> _splitters = new();
    readonly Dictionary<WorkspaceLeaf, WorkspaceRegionChrome> _regions = new();

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
            var splitter = CreateSplitter(split, GridResizeDirection.Columns);
            var second = BuildNode(split.Second, editMode, wireRegion);

            Grid.SetColumn(first, 0);
            Grid.SetColumn(splitter, 1);
            Grid.SetColumn(second, 2);
            grid.Children.Add(first);
            grid.Children.Add(splitter);
            grid.Children.Add(second);
        }
        else
        {
            grid.RowDefinitions.Add(new RowDefinition(new GridLength(ratio, GridUnitType.Star)));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.RowDefinitions.Add(new RowDefinition(new GridLength(1 - ratio, GridUnitType.Star)));

            var first = BuildNode(split.First, editMode, wireRegion);
            var splitter = CreateSplitter(split, GridResizeDirection.Rows);
            var second = BuildNode(split.Second, editMode, wireRegion);

            Grid.SetRow(first, 0);
            Grid.SetRow(splitter, 1);
            Grid.SetRow(second, 2);
            grid.Children.Add(first);
            grid.Children.Add(splitter);
            grid.Children.Add(second);
        }

        return grid;
    }

    GridSplitter CreateSplitter(WorkspaceSplit split, GridResizeDirection direction)
    {
        var splitter = new GridSplitter
        {
            Width = direction == GridResizeDirection.Columns ? 1 : double.NaN,
            Height = direction == GridResizeDirection.Rows ? 1 : double.NaN,
            ResizeDirection = direction,
        };
        splitter.Classes.Add("workspace-splitter");

        _splitters[split] = splitter;
        splitter.DragDelta += (_, _) => UpdateRatioFromGrid(split, splitter);
        splitter.DragCompleted += (_, _) => _onSplitterResizeCompleted?.Invoke();
        return splitter;
    }

    void UpdateRatioFromGrid(WorkspaceSplit split, GridSplitter splitter)
    {
        if (splitter.Parent is not Grid grid)
            return;

        if (split.Orientation == WorkspaceSplitOrientation.Horizontal)
        {
            var col = Grid.GetColumn(splitter);
            if (col != 1)
                return;
            var total = grid.Bounds.Width;
            if (total <= 0)
                return;
            var firstCol = grid.ColumnDefinitions[0];
            var firstWidth = firstCol.ActualWidth;
            split.Ratio = ClampRatio(firstWidth / total);
        }
        else
        {
            var total = grid.Bounds.Height;
            if (total <= 0)
                return;
            var firstRow = grid.RowDefinitions[0];
            split.Ratio = ClampRatio(firstRow.ActualHeight / total);
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
