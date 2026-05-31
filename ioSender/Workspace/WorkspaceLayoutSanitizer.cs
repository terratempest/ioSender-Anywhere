using CNC.App.Workspace;

namespace ioSender.Workspace;

public static class WorkspaceLayoutSanitizer
{
    public static WorkspaceNode? Sanitize(WorkspaceNode? root) => root switch
    {
        null => null,
        WorkspaceLeaf leaf => IsShellLevelPanel(leaf.Editor) ? null : leaf,
        WorkspaceSplit split => CollapseSplit(split),
        WorkspaceTabGroup tabGroup => SanitizeTabGroup(tabGroup),
        _ => null,
    };

    static WorkspaceNode? CollapseSplit(WorkspaceSplit split)
    {
        var first = Sanitize(split.First);
        var second = Sanitize(split.Second);

        return (first, second) switch
        {
            (null, null) => null,
            (null, not null) => second,
            (not null, null) => first,
            _ => new WorkspaceSplit
            {
                Orientation = split.Orientation,
                Ratio = split.Ratio,
                First = first!,
                Second = second!,
                LockedWidth = split.LockedWidth,
                LockedHeight = split.LockedHeight,
            },
        };
    }

    static WorkspaceNode? SanitizeTabGroup(WorkspaceTabGroup tabGroup)
    {
        var tabs = tabGroup.Tabs
            .Where(t => !IsShellLevelPanel(t.Editor))
            .ToList();

        if (tabs.Count == 0)
            return null;

        var clone = new WorkspaceTabGroup
        {
            Id = tabGroup.Id,
            TabStripPlacement = tabGroup.TabStripPlacement,
            LockedWidth = tabGroup.LockedWidth,
            LockedHeight = tabGroup.LockedHeight,
        };

        foreach (var tab in tabs)
            clone.Tabs.Add(new WorkspaceTabEntry { Id = tab.Id, Editor = tab.Editor });

        if (tabs.Any(t => t.Id == tabGroup.ActiveTabId))
            clone.ActiveTabId = tabGroup.ActiveTabId;
        else
            clone.ActiveTabId = clone.Tabs[0].Id;

        return clone;
    }

    static bool IsShellLevelPanel(WorkspaceEditorId id) =>
        id is WorkspaceEditorId.Status or WorkspaceEditorId.Signals;
}
