using System.Xml.Serialization;

namespace CNC.App.Workspace;

[XmlInclude(typeof(WorkspaceLeaf))]
[XmlInclude(typeof(WorkspaceSplit))]
public abstract class WorkspaceNode
{
    public WorkspaceLeaf AsLeaf() => this as WorkspaceLeaf
        ?? throw new InvalidOperationException("Node is not a leaf.");

    public WorkspaceSplit AsSplit() => this as WorkspaceSplit
        ?? throw new InvalidOperationException("Node is not a split.");

    public bool IsLeaf => this is WorkspaceLeaf;

    public WorkspaceEditorId? TryGetEditor() =>
        this is WorkspaceLeaf leaf ? leaf.Editor : null;

    public IEnumerable<WorkspaceEditorId> EnumerateEditors()
    {
        if (this is WorkspaceLeaf leaf)
        {
            yield return leaf.Editor;
            yield break;
        }

        if (this is WorkspaceSplit split)
        {
            foreach (var id in split.First.EnumerateEditors())
                yield return id;
            foreach (var id in split.Second.EnumerateEditors())
                yield return id;
        }
    }

    public IEnumerable<WorkspaceLeaf> EnumerateLeaves()
    {
        if (this is WorkspaceLeaf leaf)
        {
            yield return leaf;
            yield break;
        }

        if (this is WorkspaceSplit split)
        {
            foreach (var l in split.First.EnumerateLeaves())
                yield return l;
            foreach (var l in split.Second.EnumerateLeaves())
                yield return l;
        }
    }

    public WorkspaceNode Clone() => this switch
    {
        WorkspaceLeaf leaf => new WorkspaceLeaf { Editor = leaf.Editor, Id = Guid.NewGuid() },
        WorkspaceSplit split => new WorkspaceSplit
        {
            Orientation = split.Orientation,
            Ratio = split.Ratio,
            First = split.First.Clone(),
            Second = split.Second.Clone(),
        },
        _ => throw new InvalidOperationException("Unknown node type."),
    };
}
