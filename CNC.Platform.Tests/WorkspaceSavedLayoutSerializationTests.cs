using System.Xml.Serialization;
using CNC.App;
using CNC.App.Workspace;
using ioSender.Workspace;

namespace CNC.Platform.Tests;

public class WorkspaceSavedLayoutSerializationTests
{
    static readonly XmlSerializer Serializer = new(typeof(WorkspaceSavedLayout));

    [Fact]
    public void Serialization_round_trips_quick_access_sidebar_state()
    {
        var layout = new WorkspaceSavedLayout
        {
            Name = "Machine setup",
            Root = new WorkspaceLeaf { Editor = WorkspaceEditorId.Program },
            QuickAccessSidebar = new QuickAccessSidebarConfig
            {
                Enabled = true,
                ShowLeft = false,
                ShowRight = true,
                Dock = QuickAccessSidebarDock.Right,
                LegacySidesMigrated = true,
                Tabs =
                [
                    new QuickAccessTabEntry
                    {
                        Id = Guid.NewGuid(),
                        EditorId = WorkspaceEditorId.Jog,
                        PopupWidth = 300,
                        PopupHeight = 250,
                    },
                    new QuickAccessTabEntry
                    {
                        Id = Guid.NewGuid(),
                        EditorId = WorkspaceEditorId.Outline,
                        PopupWidth = 420,
                        PopupHeight = 360,
                    },
                ],
            },
        };

        var deserialized = Deserialize(Serialize(layout));

        Assert.Equal("Machine setup", deserialized.Name);
        Assert.IsType<WorkspaceLeaf>(deserialized.Root);
        Assert.NotNull(deserialized.QuickAccessSidebar);
        Assert.True(deserialized.QuickAccessSidebar.Enabled);
        Assert.False(deserialized.QuickAccessSidebar.ShowLeft);
        Assert.True(deserialized.QuickAccessSidebar.ShowRight);
        Assert.Equal(QuickAccessSidebarDock.Right, deserialized.QuickAccessSidebar.Dock);
        Assert.True(deserialized.QuickAccessSidebar.LegacySidesMigrated);
        Assert.Equal([WorkspaceEditorId.Jog, WorkspaceEditorId.Outline],
            deserialized.QuickAccessSidebar.Tabs.Select(t => t.EditorId).ToArray());
        Assert.Equal([300, 420],
            deserialized.QuickAccessSidebar.Tabs.Select(t => t.PopupWidth).ToArray());
        Assert.Equal([250, 360],
            deserialized.QuickAccessSidebar.Tabs.Select(t => t.PopupHeight).ToArray());
    }

    [Fact]
    public void Deserialization_accepts_legacy_layout_without_quick_access_sidebar()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-16"?>
            <WorkspaceSavedLayout xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
              <Name>Legacy</Name>
              <Root xsi:type="WorkspaceLeaf">
                <LockedWidth>0</LockedWidth>
                <LockedHeight>0</LockedHeight>
                <Id>11111111-1111-1111-1111-111111111111</Id>
                <Editor>Program</Editor>
              </Root>
            </WorkspaceSavedLayout>
            """;

        var deserialized = Deserialize(xml);

        Assert.Equal("Legacy", deserialized.Name);
        var root = Assert.IsType<WorkspaceLeaf>(deserialized.Root);
        Assert.Equal(WorkspaceEditorId.Program, root.Editor);
        Assert.Null(deserialized.QuickAccessSidebar);
    }

    static string Serialize(WorkspaceSavedLayout layout)
    {
        using var writer = new StringWriter();
        Serializer.Serialize(writer, layout);
        return writer.ToString();
    }

    static WorkspaceSavedLayout Deserialize(string xml)
    {
        using var reader = new StringReader(xml);
        return (WorkspaceSavedLayout)Serializer.Deserialize(reader)!;
    }
}
