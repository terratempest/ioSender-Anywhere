using ioSender.Workspace.Controls;

namespace ioSender.Workspace;

/// <summary>Pointer-drag state for swapping workspace panels (edit mode).</summary>
public static class WorkspaceDragBroker
{
    public static WorkspaceRegionChrome? Source { get; set; }

    public static bool IsDragging => Source is not null;

    public static void Clear() => Source = null;
}
