using CNC.App.Workspace;
using ioSender.Workspace.Editors;

namespace CNC.Platform.Tests;

public class WorkspaceEditorCatalogTests
{
    [Fact]
    public void LayoutPickableDescriptors_excludes_status()
    {
        Assert.DoesNotContain(
            WorkspaceEditorCatalog.LayoutPickableDescriptors,
            d => d.Id == WorkspaceEditorId.Status);
    }

    [Fact]
    public void PanelPickableDescriptors_excludes_status()
    {
        Assert.DoesNotContain(
            WorkspaceEditorCatalog.PanelPickableDescriptors,
            d => d.Id == WorkspaceEditorId.Status);
    }

    [Fact]
    public void AllDescriptors_includes_status_for_backward_compatibility()
    {
        Assert.Contains(
            WorkspaceEditorCatalog.AllDescriptors,
            d => d.Id == WorkspaceEditorId.Status);
    }
}
