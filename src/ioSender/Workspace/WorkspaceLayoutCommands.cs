using CNC.App.Workspace;

namespace ioSender.Workspace;

public static class WorkspaceLayoutCommands
{
    public static void SwapEditors(WorkspaceLeaf leafA, WorkspaceLeaf leafB) =>
        (leafA.Editor, leafB.Editor) = (leafB.Editor, leafA.Editor);

    public static bool TrySplitLeaf(
        WorkspaceNode root,
        WorkspaceLeaf target,
        WorkspaceSplitOrientation orientation,
        out WorkspaceNode newRoot)
    {
        if (root is WorkspaceLeaf leaf && ReferenceEquals(leaf, target))
        {
            newRoot = CreateSplit(leaf, orientation);
            return true;
        }

        if (root is WorkspaceSplit split)
        {
            if (TrySplitInBranch(split.First, target, orientation, out var newFirst))
            {
                split.First = newFirst;
                newRoot = root;
                return true;
            }

            if (TrySplitInBranch(split.Second, target, orientation, out var newSecond))
            {
                split.Second = newSecond;
                newRoot = root;
                return true;
            }
        }

        newRoot = root;
        return false;
    }

    static bool TrySplitInBranch(
        WorkspaceNode branch,
        WorkspaceLeaf target,
        WorkspaceSplitOrientation orientation,
        out WorkspaceNode newBranch)
    {
        if (branch is WorkspaceLeaf leaf && ReferenceEquals(leaf, target))
        {
            newBranch = CreateSplit(leaf, orientation);
            return true;
        }

        if (branch is WorkspaceSplit childSplit)
        {
            if (TrySplitInBranch(childSplit.First, target, orientation, out var nf))
            {
                childSplit.First = nf;
                newBranch = childSplit;
                return true;
            }

            if (TrySplitInBranch(childSplit.Second, target, orientation, out var ns))
            {
                childSplit.Second = ns;
                newBranch = childSplit;
                return true;
            }
        }

        newBranch = branch;
        return false;
    }

    static WorkspaceSplit CreateSplit(WorkspaceLeaf existing, WorkspaceSplitOrientation orientation) =>
        new()
        {
            Orientation = orientation,
            Ratio = 0.5,
            First = existing,
            Second = new WorkspaceLeaf { Editor = existing.Editor },
        };

    public static bool TryJoinLeaf(WorkspaceNode root, WorkspaceLeaf target, out WorkspaceNode newRoot)
    {
        if (TryJoinRecursive(root, target, out var joined))
        {
            newRoot = joined;
            return true;
        }

        newRoot = root;
        return false;
    }

    static bool TryJoinRecursive(WorkspaceNode node, WorkspaceLeaf target, out WorkspaceNode result)
    {
        if (node is WorkspaceSplit split)
        {
            if (split.First is WorkspaceLeaf first && ReferenceEquals(first, target))
            {
                result = split.Second.Clone();
                return true;
            }

            if (split.Second is WorkspaceLeaf second && ReferenceEquals(second, target))
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
}
