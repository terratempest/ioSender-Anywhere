namespace CNC.App.Workspace;

public enum WorkspaceTabStripPlacement
{
    Bottom,
    Top,
}

public sealed class WorkspaceTabGroup : WorkspaceNode
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public List<WorkspaceTabEntry> Tabs { get; set; } = new();

    public Guid ActiveTabId { get; set; }

    public WorkspaceTabStripPlacement TabStripPlacement { get; set; } = WorkspaceTabStripPlacement.Bottom;
}

public sealed class WorkspaceTabEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public WorkspaceEditorId Editor { get; set; } = WorkspaceEditorId.Program;
}
