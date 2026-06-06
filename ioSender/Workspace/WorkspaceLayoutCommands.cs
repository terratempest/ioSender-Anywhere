using CNC.App.Workspace;

namespace ioSender.Workspace;

public static class WorkspaceLayoutCommands
{
    public static bool TryReplaceRegion(
        WorkspaceNode root,
        WorkspaceNode target,
        WorkspaceNode replacement,
        out WorkspaceNode newRoot)
    {
        if (ReferenceEquals(root, target))
        {
            newRoot = replacement;
            return true;
        }

        if (root is WorkspaceSplit split)
        {
            if (TryReplaceRegion(split.First, target, replacement, out var newFirst))
            {
                split.First = newFirst;
                newRoot = root;
                return true;
            }

            if (TryReplaceRegion(split.Second, target, replacement, out var newSecond))
            {
                split.Second = newSecond;
                newRoot = root;
                return true;
            }
        }

        newRoot = root;
        return false;
    }

    public static bool TrySplitRegion(
        WorkspaceNode root,
        WorkspaceNode target,
        WorkspaceSplitOrientation orientation,
        out WorkspaceNode newRoot)
    {
        return TrySplitRegion(root, target, orientation, 0.5, out newRoot);
    }

    public static bool TrySplitRegion(
        WorkspaceNode root,
        WorkspaceNode target,
        WorkspaceSplitOrientation orientation,
        double ratio,
        out WorkspaceNode newRoot)
    {
        if (ReferenceEquals(root, target))
        {
            newRoot = CreateSplit(root, orientation, ratio);
            return true;
        }

        if (root is WorkspaceSplit split)
        {
            if (TrySplitRegion(split.First, target, orientation, ratio, out var newFirst))
            {
                split.First = newFirst;
                newRoot = root;
                return true;
            }

            if (TrySplitRegion(split.Second, target, orientation, ratio, out var newSecond))
            {
                split.Second = newSecond;
                newRoot = root;
                return true;
            }
        }

        newRoot = root;
        return false;
    }

    static WorkspaceSplit CreateSplit(WorkspaceNode existing, WorkspaceSplitOrientation orientation, double ratio) =>
        new()
        {
            Orientation = orientation,
            Ratio = ClampSplitRatio(ratio),
            First = ClearLocks(existing),
            Second = ClearLocks(existing.Clone()),
        };

    static WorkspaceNode ClearLocks(WorkspaceNode node)
    {
        node.LockedWidth = 0;
        node.LockedHeight = 0;
        return node;
    }

    public static double ClampSplitRatio(double ratio) => Math.Clamp(ratio, 0.08, 0.92);

    public static bool TryJoinRegion(WorkspaceNode root, WorkspaceNode target, out WorkspaceNode newRoot)
    {
        if (TryJoinRecursive(root, target, out var joined))
        {
            newRoot = joined;
            return true;
        }

        newRoot = root;
        return false;
    }

    static bool TryJoinRecursive(WorkspaceNode node, WorkspaceNode target, out WorkspaceNode result)
    {
        if (node is WorkspaceSplit split)
        {
            if (ReferenceEquals(split.First, target))
            {
                result = split.Second.Clone();
                return true;
            }

            if (ReferenceEquals(split.Second, target))
            {
                result = split.First.Clone();
                return true;
            }

            if (TryJoinRecursive(split.First, target, out var newFirst))
            {
                split.First = newFirst;
                result = node;
                return true;
            }

            if (TryJoinRecursive(split.Second, target, out var newSecond))
            {
                split.Second = newSecond;
                result = node;
                return true;
            }
        }

        result = node;
        return false;
    }

    public static bool TrySwapRegions(WorkspaceNode root, WorkspaceNode first, WorkspaceNode second, out WorkspaceNode newRoot)
    {
        if (ReferenceEquals(first, second))
        {
            newRoot = root;
            return false;
        }

        var firstPath = FindPath(root, first);
        var secondPath = FindPath(root, second);
        if (firstPath is null || secondPath is null)
        {
            newRoot = root;
            return false;
        }

        newRoot = root;
        var firstClone = first.Clone();
        var secondClone = second.Clone();
        SetAtPath(ref newRoot, firstPath, secondClone);
        SetAtPath(ref newRoot, secondPath, firstClone);
        return true;
    }

    static List<bool>? FindPath(WorkspaceNode current, WorkspaceNode target)
    {
        if (ReferenceEquals(current, target))
            return new List<bool>();

        if (current is not WorkspaceSplit split)
            return null;

        if (FindPath(split.First, target) is { } left)
        {
            left.Insert(0, false);
            return left;
        }

        if (FindPath(split.Second, target) is { } right)
        {
            right.Insert(0, true);
            return right;
        }

        return null;
    }

    static void SetAtPath(ref WorkspaceNode root, IReadOnlyList<bool> path, WorkspaceNode replacement)
    {
        if (path.Count == 0)
        {
            root = replacement;
            return;
        }

        var node = root;
        for (var i = 0; i < path.Count - 1; i++)
        {
            var split = node.AsSplit();
            node = path[i] ? split.Second : split.First;
        }

        var parent = node.AsSplit();
        if (path[^1])
            parent.Second = replacement;
        else
            parent.First = replacement;
    }
}
