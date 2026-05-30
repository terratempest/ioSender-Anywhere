using CNC.App.Workspace;

namespace CNC.Platform.Tests;

public class WorkspaceTabGroupTests
{
    [Fact]
    public void EnumerateEditors_includes_tab_group_children()
    {
        var root = new WorkspaceSplit
        {
            First = new WorkspaceLeaf { Editor = WorkspaceEditorId.Program },
            Second = new WorkspaceTabGroup
            {
                Tabs =
                [
                    new WorkspaceTabEntry { Editor = WorkspaceEditorId.Jog },
                    new WorkspaceTabEntry { Editor = WorkspaceEditorId.Console },
                ],
            },
        };

        Assert.Equal(
            [WorkspaceEditorId.Program, WorkspaceEditorId.Jog, WorkspaceEditorId.Console],
            root.EnumerateEditors().ToArray());
    }

    [Fact]
    public void Clone_preserves_tab_group_state_with_new_ids()
    {
        var active = new WorkspaceTabEntry { Editor = WorkspaceEditorId.Console };
        var inactive = new WorkspaceTabEntry { Editor = WorkspaceEditorId.Jog };
        var group = new WorkspaceTabGroup
        {
            Id = Guid.NewGuid(),
            ActiveTabId = active.Id,
            TabStripPlacement = WorkspaceTabStripPlacement.Top,
            Tabs = [inactive, active],
        };

        var clone = Assert.IsType<WorkspaceTabGroup>(group.Clone());

        Assert.NotEqual(group.Id, clone.Id);
        Assert.Equal(WorkspaceTabStripPlacement.Top, clone.TabStripPlacement);
        Assert.Equal([WorkspaceEditorId.Jog, WorkspaceEditorId.Console], clone.Tabs.Select(t => t.Editor).ToArray());
        Assert.All(clone.Tabs, t => Assert.DoesNotContain(group.Tabs, original => original.Id == t.Id));
        Assert.Equal(WorkspaceEditorId.Console, clone.Tabs.Single(t => t.Id == clone.ActiveTabId).Editor);
    }

    [Fact]
    public void Empty_tab_group_has_no_editors_but_clones()
    {
        var group = new WorkspaceTabGroup();

        var clone = Assert.IsType<WorkspaceTabGroup>(group.Clone());

        Assert.Empty(group.EnumerateEditors());
        Assert.Empty(clone.Tabs);
        Assert.Equal(Guid.Empty, clone.ActiveTabId);
        Assert.Equal(WorkspaceTabStripPlacement.Bottom, clone.TabStripPlacement);
    }
}
