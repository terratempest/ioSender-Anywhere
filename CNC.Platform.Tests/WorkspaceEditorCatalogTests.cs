using CNC.App.Workspace;
using ioSender.Workspace.Editors;

namespace CNC.Platform.Tests;

public class WorkspaceEditorCatalogTests
{
    [Fact]
    public void LayoutPickableDescriptors_excludes_shell_level_panels()
    {
        Assert.DoesNotContain(
            WorkspaceEditorCatalog.LayoutPickableDescriptors,
            d => d.Id == WorkspaceEditorId.Status);
        Assert.DoesNotContain(
            WorkspaceEditorCatalog.LayoutPickableDescriptors,
            d => d.Id == WorkspaceEditorId.Signals);
    }

    [Fact]
    public void PanelPickableDescriptors_excludes_shell_level_panels()
    {
        Assert.DoesNotContain(
            WorkspaceEditorCatalog.PanelPickableDescriptors,
            d => d.Id == WorkspaceEditorId.Status);
        Assert.DoesNotContain(
            WorkspaceEditorCatalog.PanelPickableDescriptors,
            d => d.Id == WorkspaceEditorId.Signals);
    }

    [Fact]
    public void AllDescriptors_includes_shell_level_panels_for_backward_compatibility()
    {
        Assert.Contains(
            WorkspaceEditorCatalog.AllDescriptors,
            d => d.Id == WorkspaceEditorId.Status);
        Assert.Contains(
            WorkspaceEditorCatalog.AllDescriptors,
            d => d.Id == WorkspaceEditorId.Signals);
    }

    [Fact]
    public void Macros_is_pickable_and_requires_grbl_context()
    {
        var descriptor = WorkspaceEditorCatalog.Get(WorkspaceEditorId.Macros);

        Assert.Contains(
            WorkspaceEditorCatalog.LayoutPickableDescriptors,
            d => d.Id == WorkspaceEditorId.Macros);
        Assert.Contains(
            WorkspaceEditorCatalog.PanelPickableDescriptors,
            d => d.Id == WorkspaceEditorId.Macros);
        Assert.True(descriptor.RequiresGrblDataContext);
    }
}
