using System.Xml.Serialization;
using CNC.App.Workspace;

namespace CNC.App;

public enum QuickAccessSidebarDock
{
    Left,
    Right,
}

[Serializable]
public sealed class QuickAccessSidebarConfig
{
    public bool Enabled { get; set; }
    public bool ShowLeft { get; set; }
    public bool ShowRight { get; set; }

    /// <summary>Legacy single-side dock from early builds.</summary>
    public QuickAccessSidebarDock Dock { get; set; } = QuickAccessSidebarDock.Right;

    public bool LegacySidesMigrated { get; set; }

    public List<QuickAccessTabEntry> Tabs { get; set; } = new();

    public QuickAccessSidebarConfig Clone() => new()
    {
        Enabled = Enabled,
        ShowLeft = ShowLeft,
        ShowRight = ShowRight,
        Dock = Dock,
        LegacySidesMigrated = LegacySidesMigrated,
        Tabs = Tabs.Select(t => t.Clone()).ToList(),
    };

    /// <summary>One-time migration from <see cref="Dock"/>; never re-runs after <see cref="LegacySidesMigrated"/>.</summary>
    public void MigrateLegacyDockOnce()
    {
        if (LegacySidesMigrated)
            return;

        if (!ShowLeft && !ShowRight)
        {
            ShowLeft = Dock == QuickAccessSidebarDock.Left;
            ShowRight = Dock == QuickAccessSidebarDock.Right;
        }

        LegacySidesMigrated = true;
    }
}

[Serializable]
public sealed class QuickAccessTabEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public WorkspaceEditorId EditorId { get; set; }
    public double PopupWidth { get; set; }
    public double PopupHeight { get; set; }

    public QuickAccessTabEntry Clone() => new()
    {
        Id = Id,
        EditorId = EditorId,
        PopupWidth = PopupWidth,
        PopupHeight = PopupHeight,
    };
}

public static class QuickAccessSidebarDefaults
{
    public static void EnsureDefaultTabs(QuickAccessSidebarConfig config)
    {
        if (config.Tabs.Count > 0)
            return;

        config.Tabs.Add(new QuickAccessTabEntry { EditorId = WorkspaceEditorId.Jog });
        config.Tabs.Add(new QuickAccessTabEntry { EditorId = WorkspaceEditorId.Goto });
        config.Tabs.Add(new QuickAccessTabEntry { EditorId = WorkspaceEditorId.Outline });
    }

    public static (double width, double height) ResolvePopupSize(
        QuickAccessTabEntry tab,
        double minWidth,
        double minHeight)
    {
        var width = tab.PopupWidth > 0 ? tab.PopupWidth : minWidth + 40;
        var height = tab.PopupHeight > 0 ? tab.PopupHeight : minHeight + 40;
        return (Math.Max(width, minWidth), Math.Max(height, minHeight));
    }
}
