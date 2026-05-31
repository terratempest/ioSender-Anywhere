using CNC.App.Workspace;
using ioSender.Workspace;

namespace CNC.Platform.Tests;

public class WorkspaceLayoutSanitizerTests
{
    static WorkspaceLeaf Leaf(WorkspaceEditorId id) => new() { Editor = id };

    [Fact]
    public void Split_with_status_child_collapses_to_other_child()
    {
        var root = new WorkspaceSplit
        {
            First = Leaf(WorkspaceEditorId.Status),
            Second = Leaf(WorkspaceEditorId.Program),
        };

        var sanitized = WorkspaceLayoutSanitizer.Sanitize(root);

        var leaf = Assert.IsType<WorkspaceLeaf>(sanitized);
        Assert.Equal(WorkspaceEditorId.Program, leaf.Editor);
    }

    [Fact]
    public void Tab_group_drops_status_tabs()
    {
        var statusTab = new WorkspaceTabEntry { Editor = WorkspaceEditorId.Status };
        var jogTab = new WorkspaceTabEntry { Editor = WorkspaceEditorId.Jog };
        var consoleTab = new WorkspaceTabEntry { Editor = WorkspaceEditorId.Console };
        var root = new WorkspaceTabGroup
        {
            ActiveTabId = statusTab.Id,
            Tabs = [statusTab, jogTab, consoleTab],
        };

        var sanitized = Assert.IsType<WorkspaceTabGroup>(WorkspaceLayoutSanitizer.Sanitize(root));

        Assert.Equal(
            [WorkspaceEditorId.Jog, WorkspaceEditorId.Console],
            sanitized.Tabs.Select(t => t.Editor).ToArray());
        Assert.Equal(jogTab.Id, sanitized.ActiveTabId);
    }

    [Fact]
    public void Status_only_leaf_returns_null()
    {
        Assert.Null(WorkspaceLayoutSanitizer.Sanitize(Leaf(WorkspaceEditorId.Status)));
    }

    [Fact]
    public void Split_with_both_status_children_returns_null()
    {
        var root = new WorkspaceSplit
        {
            First = Leaf(WorkspaceEditorId.Status),
            Second = Leaf(WorkspaceEditorId.Status),
        };

        Assert.Null(WorkspaceLayoutSanitizer.Sanitize(root));
    }
}
