using CNC.App.Workspace;

namespace ioSender.Workspace;

public static class WorkspaceLayoutDefaults
{
    public const string PresetCompact = "Compact";
    public const string PresetExpanded = "Expanded";
    public const string PresetDefault = "Default";

    public static WorkspaceNode Default => Compact;

    public static WorkspaceNode Compact => new WorkspaceSplit
    {
        Orientation = WorkspaceSplitOrientation.Vertical,
        Ratio = 0.94,
        First = new WorkspaceSplit
        {
            Orientation = WorkspaceSplitOrientation.Horizontal,
            Ratio = 0.22,
            First = new WorkspaceSplit
            {
                Orientation = WorkspaceSplitOrientation.Vertical,
                Ratio = 0.42,
                First = Leaf(WorkspaceEditorId.Dro),
                Second = new WorkspaceSplit
                {
                    Orientation = WorkspaceSplitOrientation.Vertical,
                    Ratio = 0.5,
                    First = Leaf(WorkspaceEditorId.Signals),
                    Second = Leaf(WorkspaceEditorId.Status),
                },
            },
            Second = new WorkspaceSplit
            {
                Orientation = WorkspaceSplitOrientation.Horizontal,
                Ratio = 0.72,
                First = new WorkspaceSplit
                {
                    Orientation = WorkspaceSplitOrientation.Vertical,
                    Ratio = 0.42,
                    First = Leaf(WorkspaceEditorId.Program),
                    Second = Leaf(WorkspaceEditorId.Viewer3D),
                },
                Second = RightMachineStack(),
            },
        },
        Second = Leaf(WorkspaceEditorId.JobBar),
    };

    public static WorkspaceNode Expanded => new WorkspaceSplit
    {
        Orientation = WorkspaceSplitOrientation.Vertical,
        Ratio = 0.94,
        First = new WorkspaceSplit
        {
            Orientation = WorkspaceSplitOrientation.Horizontal,
            Ratio = 0.18,
            First = new WorkspaceSplit
            {
                Orientation = WorkspaceSplitOrientation.Vertical,
                Ratio = 0.38,
                First = Leaf(WorkspaceEditorId.Dro),
                Second = new WorkspaceSplit
                {
                    Orientation = WorkspaceSplitOrientation.Vertical,
                    Ratio = 0.5,
                    First = Leaf(WorkspaceEditorId.Signals),
                    Second = Leaf(WorkspaceEditorId.Status),
                },
            },
            Second = new WorkspaceSplit
            {
                Orientation = WorkspaceSplitOrientation.Horizontal,
                Ratio = 0.82,
                First = Leaf(WorkspaceEditorId.Program),
                Second = new WorkspaceSplit
                {
                    Orientation = WorkspaceSplitOrientation.Vertical,
                    Ratio = 0.22,
                    First = Leaf(WorkspaceEditorId.Jog),
                    Second = new WorkspaceSplit
                    {
                        Orientation = WorkspaceSplitOrientation.Horizontal,
                        Ratio = 0.5,
                        First = new WorkspaceSplit
                        {
                            Orientation = WorkspaceSplitOrientation.Vertical,
                            Ratio = 0.5,
                            First = Leaf(WorkspaceEditorId.Outline),
                            Second = Leaf(WorkspaceEditorId.Spindle),
                        },
                        Second = new WorkspaceSplit
                        {
                            Orientation = WorkspaceSplitOrientation.Vertical,
                            Ratio = 0.5,
                            First = Leaf(WorkspaceEditorId.Feed),
                            Second = Leaf(WorkspaceEditorId.Goto),
                        },
                    },
                },
            },
        },
        Second = Leaf(WorkspaceEditorId.JobBar),
    };

    static WorkspaceNode RightMachineStack() => new WorkspaceSplit
    {
        Orientation = WorkspaceSplitOrientation.Vertical,
        Ratio = 0.26,
        First = Leaf(WorkspaceEditorId.WorkParams),
        Second = new WorkspaceSplit
        {
            Orientation = WorkspaceSplitOrientation.Vertical,
            Ratio = 0.33,
            First = Leaf(WorkspaceEditorId.Coolant),
            Second = new WorkspaceSplit
            {
                Orientation = WorkspaceSplitOrientation.Vertical,
                Ratio = 0.5,
                First = Leaf(WorkspaceEditorId.Spindle),
                Second = Leaf(WorkspaceEditorId.Feed),
            },
        },
    };

    static WorkspaceLeaf Leaf(WorkspaceEditorId id) => new() { Editor = id };

    public static WorkspaceNode? GetPreset(string? name) => name switch
    {
        PresetCompact => Compact.Clone(),
        PresetExpanded => Expanded.Clone(),
        PresetDefault or null or "" => Compact.Clone(),
        _ => null,
    };

    public static bool IsBuiltIn(string? name) =>
        string.Equals(name, PresetCompact, StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, PresetExpanded, StringComparison.OrdinalIgnoreCase);

    public static bool IsValid(WorkspaceNode? root) =>
        root is not null && root.EnumerateEditors().Any();
}
