namespace CNC.App.Workspace;

public sealed class WorkspaceSplit : WorkspaceNode
{
    public WorkspaceSplitOrientation Orientation { get; set; } = WorkspaceSplitOrientation.Horizontal;
    public double Ratio { get; set; } = 0.5;
    public WorkspaceNode First { get; set; } = new WorkspaceLeaf();
    public WorkspaceNode Second { get; set; } = new WorkspaceLeaf { Editor = WorkspaceEditorId.Dro };
}
