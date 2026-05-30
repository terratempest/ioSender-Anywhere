using CNC.App.Workspace;

namespace CNC.Platform.Tests;

public class WorkspaceLayoutLocksTests
{
    [Fact]
    public void Direct_locked_leaf_controls_parent_split_axis()
    {
        var leaf = new WorkspaceLeaf { LockedWidth = 250 };

        var lockedSize = WorkspaceLayoutLocks.ResolveLockedSize(leaf, WorkspaceLockAxis.Width);

        Assert.Equal(250, lockedSize);
    }

    [Fact]
    public void Width_lock_propagates_through_vertical_split()
    {
        var stack = new WorkspaceSplit
        {
            Orientation = WorkspaceSplitOrientation.Vertical,
            First = new WorkspaceLeaf { LockedWidth = 250 },
            Second = new WorkspaceLeaf(),
        };

        var lockedSize = WorkspaceLayoutLocks.ResolveLockedSize(stack, WorkspaceLockAxis.Width);

        Assert.Equal(250, lockedSize);
    }

    [Fact]
    public void Height_lock_propagates_through_horizontal_split()
    {
        var row = new WorkspaceSplit
        {
            Orientation = WorkspaceSplitOrientation.Horizontal,
            First = new WorkspaceLeaf { LockedHeight = 180 },
            Second = new WorkspaceLeaf(),
        };

        var lockedSize = WorkspaceLayoutLocks.ResolveLockedSize(row, WorkspaceLockAxis.Height);

        Assert.Equal(180, lockedSize);
    }

    [Fact]
    public void Same_axis_nested_split_stops_propagation_to_outer_split()
    {
        var row = new WorkspaceSplit
        {
            Orientation = WorkspaceSplitOrientation.Horizontal,
            First = new WorkspaceLeaf { LockedWidth = 250 },
            Second = new WorkspaceLeaf(),
        };

        var lockedSize = WorkspaceLayoutLocks.ResolveLockedSize(row, WorkspaceLockAxis.Width);

        Assert.Equal(0, lockedSize);
    }

    [Fact]
    public void Perpendicular_stack_uses_largest_child_lock()
    {
        var stack = new WorkspaceSplit
        {
            Orientation = WorkspaceSplitOrientation.Vertical,
            First = new WorkspaceLeaf { LockedWidth = 250 },
            Second = new WorkspaceLeaf { LockedWidth = 320 },
        };

        var lockedSize = WorkspaceLayoutLocks.ResolveLockedSize(stack, WorkspaceLockAxis.Width);

        Assert.Equal(320, lockedSize);
    }
}
