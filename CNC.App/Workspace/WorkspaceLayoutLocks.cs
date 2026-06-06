namespace CNC.App.Workspace;

public enum WorkspaceLockAxis
{
    Width,
    Height,
}

public static class WorkspaceLayoutLocks
{
    public static double ResolveLockedSize(WorkspaceNode node, WorkspaceLockAxis axis)
    {
        var direct = axis == WorkspaceLockAxis.Width
            ? node.LockedWidth
            : node.LockedHeight;
        if (direct > 0)
            return direct;

        if (node is not WorkspaceSplit split)
            return 0;

        if (SplitControlsAxis(split, axis))
            return 0;

        return Math.Max(
            ResolveLockedSize(split.First, axis),
            ResolveLockedSize(split.Second, axis));
    }

    public static bool ContainsLockedSize(WorkspaceNode node, WorkspaceLockAxis axis)
    {
        var direct = axis == WorkspaceLockAxis.Width
            ? node.LockedWidth
            : node.LockedHeight;
        if (direct > 0)
            return true;

        return node is WorkspaceSplit split
            && (ContainsLockedSize(split.First, axis) || ContainsLockedSize(split.Second, axis));
    }

    public static bool SplitControlsAxis(WorkspaceSplit split, WorkspaceLockAxis axis) =>
        axis == WorkspaceLockAxis.Width
            ? split.Orientation == WorkspaceSplitOrientation.Horizontal
            : split.Orientation == WorkspaceSplitOrientation.Vertical;
}
