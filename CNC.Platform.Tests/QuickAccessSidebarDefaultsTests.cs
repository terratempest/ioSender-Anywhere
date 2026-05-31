using CNC.App;
using CNC.App.Workspace;

namespace CNC.Platform.Tests;

public class QuickAccessSidebarDefaultsTests
{
    [Fact]
    public void EnsureDefaultTabs_seeds_jog_goto_outline_when_empty()
    {
        var config = new QuickAccessSidebarConfig();
        QuickAccessSidebarDefaults.EnsureDefaultTabs(config);

        Assert.Equal(3, config.Tabs.Count);
        Assert.Equal(WorkspaceEditorId.Jog, config.Tabs[0].EditorId);
        Assert.Equal(WorkspaceEditorId.Goto, config.Tabs[1].EditorId);
        Assert.Equal(WorkspaceEditorId.Outline, config.Tabs[2].EditorId);
        Assert.All(config.Tabs, t => Assert.NotEqual(Guid.Empty, t.Id));
    }

    [Fact]
    public void EnsureDefaultTabs_does_not_replace_existing_tabs()
    {
        var config = new QuickAccessSidebarConfig
        {
            Tabs = [new QuickAccessTabEntry { EditorId = WorkspaceEditorId.Console }],
        };
        QuickAccessSidebarDefaults.EnsureDefaultTabs(config);
        Assert.Single(config.Tabs);
        Assert.Equal(WorkspaceEditorId.Console, config.Tabs[0].EditorId);
    }

    [Fact]
    public void MigrateLegacyDockOnce_maps_dock_only_first_time()
    {
        var config = new QuickAccessSidebarConfig
        {
            Dock = QuickAccessSidebarDock.Left,
            ShowLeft = false,
            ShowRight = false,
        };
        config.MigrateLegacyDockOnce();
        Assert.True(config.ShowLeft);
        Assert.False(config.ShowRight);
        Assert.True(config.LegacySidesMigrated);

        config.ShowLeft = false;
        config.ShowRight = false;
        config.MigrateLegacyDockOnce();
        Assert.False(config.ShowLeft);
        Assert.False(config.ShowRight);
    }

    [Fact]
    public void MigrateLegacyDockOnce_does_not_run_when_sides_already_set()
    {
        var config = new QuickAccessSidebarConfig
        {
            Dock = QuickAccessSidebarDock.Right,
            ShowLeft = true,
            ShowRight = false,
        };
        config.MigrateLegacyDockOnce();
        Assert.True(config.ShowLeft);
        Assert.False(config.ShowRight);
    }

    [Fact]
    public void ResolvePopupSize_uses_stored_dimensions_when_set()
    {
        var tab = new QuickAccessTabEntry { PopupWidth = 400, PopupHeight = 300 };
        var (w, h) = QuickAccessSidebarDefaults.ResolvePopupSize(tab, 200, 100);
        Assert.Equal(400, w);
        Assert.Equal(300, h);
    }

    [Fact]
    public void ResolvePopupSize_clamps_to_minimums()
    {
        var tab = new QuickAccessTabEntry { PopupWidth = 50, PopupHeight = 50 };
        var (w, h) = QuickAccessSidebarDefaults.ResolvePopupSize(tab, 200, 100);
        Assert.Equal(200, w);
        Assert.Equal(100, h);
    }

    [Fact]
    public void Clone_preserves_sidebar_state_without_sharing_tabs()
    {
        var firstTabId = Guid.NewGuid();
        var secondTabId = Guid.NewGuid();
        var config = new QuickAccessSidebarConfig
        {
            Enabled = true,
            ShowLeft = true,
            ShowRight = false,
            Dock = QuickAccessSidebarDock.Left,
            LegacySidesMigrated = true,
            Tabs =
            [
                new QuickAccessTabEntry
                {
                    Id = firstTabId,
                    EditorId = WorkspaceEditorId.Jog,
                    PopupWidth = 320,
                    PopupHeight = 240,
                },
                new QuickAccessTabEntry
                {
                    Id = secondTabId,
                    EditorId = WorkspaceEditorId.Console,
                    PopupWidth = 480,
                    PopupHeight = 360,
                },
            ],
        };

        var clone = config.Clone();

        Assert.NotSame(config, clone);
        Assert.Equal(config.Enabled, clone.Enabled);
        Assert.Equal(config.ShowLeft, clone.ShowLeft);
        Assert.Equal(config.ShowRight, clone.ShowRight);
        Assert.Equal(config.Dock, clone.Dock);
        Assert.Equal(config.LegacySidesMigrated, clone.LegacySidesMigrated);
        Assert.Equal([WorkspaceEditorId.Jog, WorkspaceEditorId.Console], clone.Tabs.Select(t => t.EditorId).ToArray());
        Assert.Equal([firstTabId, secondTabId], clone.Tabs.Select(t => t.Id).ToArray());
        Assert.Equal([320, 480], clone.Tabs.Select(t => t.PopupWidth).ToArray());
        Assert.Equal([240, 360], clone.Tabs.Select(t => t.PopupHeight).ToArray());
        Assert.NotSame(config.Tabs, clone.Tabs);
        Assert.NotSame(config.Tabs[0], clone.Tabs[0]);

        clone.Tabs[0].EditorId = WorkspaceEditorId.Goto;
        clone.Tabs.Add(new QuickAccessTabEntry { EditorId = WorkspaceEditorId.Outline });

        Assert.Equal(WorkspaceEditorId.Jog, config.Tabs[0].EditorId);
        Assert.Equal(2, config.Tabs.Count);
    }
}
