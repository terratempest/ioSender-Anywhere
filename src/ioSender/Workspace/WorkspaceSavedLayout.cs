using CNC.App.Workspace;

namespace ioSender.Workspace;

[Serializable]
public sealed class WorkspaceSavedLayout
{
    public string Name { get; set; } = string.Empty;

    public WorkspaceNode Root { get; set; } = new WorkspaceLeaf();
}
