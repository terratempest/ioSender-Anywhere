using CNC.App.Workspace;

namespace ioSender.Workspace.Editors;

public sealed class WorkspaceEditorDescriptor
{
    public required WorkspaceEditorId Id { get; init; }
    public required string TitleKey { get; init; }
    public required string TitleFallback { get; init; }
    public string? HeaderTitleKey { get; init; }
    public string? HeaderTitleFallback { get; init; }
    public double MinWidth { get; init; } = 120;
    public double MinHeight { get; init; } = 80;
    public bool RequiresGrblDataContext { get; init; } = true;
    public bool SupportsActivation { get; init; }
    /// <summary>Editor stretches to fill the workspace region when resized.</summary>
    public bool FillsWorkspace { get; init; }
}
