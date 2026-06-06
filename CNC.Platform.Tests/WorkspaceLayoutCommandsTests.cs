using CNC.App.Workspace;
using ioSender.Workspace;

namespace CNC.Platform.Tests;

public class WorkspaceLayoutCommandsTests
{
    [Fact]
    public void TrySplitRegion_uses_requested_ratio_and_clones_target()
    {
        var leaf = new WorkspaceLeaf { Editor = WorkspaceEditorId.Program };

        var result = WorkspaceLayoutCommands.TrySplitRegion(
            leaf,
            leaf,
            WorkspaceSplitOrientation.Horizontal,
            0.35,
            out var newRoot);

        Assert.True(result);
        var split = Assert.IsType<WorkspaceSplit>(newRoot);
        Assert.Equal(WorkspaceSplitOrientation.Horizontal, split.Orientation);
        Assert.Equal(0.35, split.Ratio);
        Assert.Same(leaf, split.First);
        var clonedLeaf = Assert.IsType<WorkspaceLeaf>(split.Second);
        Assert.NotSame(leaf, clonedLeaf);
        Assert.Equal(leaf.Editor, clonedLeaf.Editor);
    }

    [Fact]
    public void TrySplitRegion_clears_locks_from_both_split_branches()
    {
        var leaf = new WorkspaceLeaf
        {
            Editor = WorkspaceEditorId.Program,
            LockedWidth = 250,
            LockedHeight = 180,
        };

        WorkspaceLayoutCommands.TrySplitRegion(
            leaf,
            leaf,
            WorkspaceSplitOrientation.Horizontal,
            0.5,
            out var newRoot);

        var split = Assert.IsType<WorkspaceSplit>(newRoot);
        Assert.Equal(0, split.First.LockedWidth);
        Assert.Equal(0, split.First.LockedHeight);
        Assert.Equal(0, split.Second.LockedWidth);
        Assert.Equal(0, split.Second.LockedHeight);
    }

    [Theory]
    [InlineData(-1, 0.08)]
    [InlineData(2, 0.92)]
    public void TrySplitRegion_clamps_requested_ratio(double ratio, double expected)
    {
        var leaf = new WorkspaceLeaf();

        WorkspaceLayoutCommands.TrySplitRegion(
            leaf,
            leaf,
            WorkspaceSplitOrientation.Vertical,
            ratio,
            out var newRoot);

        var split = Assert.IsType<WorkspaceSplit>(newRoot);
        Assert.Equal(expected, split.Ratio);
    }

    [Fact]
    public void SplitVertically_maps_to_column_layout_orientation()
    {
        Assert.Equal(
            WorkspaceSplitOrientation.Horizontal,
            WorkspaceSplitIntent.Vertical.ToLayoutOrientation());
    }

    [Fact]
    public void SplitHorizontally_maps_to_row_layout_orientation()
    {
        Assert.Equal(
            WorkspaceSplitOrientation.Vertical,
            WorkspaceSplitIntent.Horizontal.ToLayoutOrientation());
    }
}
