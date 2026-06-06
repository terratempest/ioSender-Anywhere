using CNC.App.Workspace;

namespace ioSender.Workspace;

public enum WorkspaceSplitIntent
{
    Vertical,
    Horizontal,
}

public static class WorkspaceSplitIntentExtensions
{
    public static WorkspaceSplitOrientation ToLayoutOrientation(this WorkspaceSplitIntent intent) =>
        intent == WorkspaceSplitIntent.Vertical
            ? WorkspaceSplitOrientation.Horizontal
            : WorkspaceSplitOrientation.Vertical;
}
