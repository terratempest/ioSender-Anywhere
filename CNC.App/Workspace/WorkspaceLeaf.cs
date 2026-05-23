namespace CNC.App.Workspace;

public sealed class WorkspaceLeaf : WorkspaceNode
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public WorkspaceEditorId Editor { get; set; } = WorkspaceEditorId.Program;
}
