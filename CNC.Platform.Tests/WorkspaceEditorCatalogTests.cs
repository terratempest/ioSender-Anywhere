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
        Assert.DoesNotContain(
            WorkspaceEditorCatalog.LayoutPickableDescriptors,
            d => d.Id == WorkspaceEditorId.SdCard);
        Assert.DoesNotContain(
            WorkspaceEditorCatalog.LayoutPickableDescriptors,
            d => d.Id == WorkspaceEditorId.Lathe);
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
        Assert.DoesNotContain(
            WorkspaceEditorCatalog.PanelPickableDescriptors,
            d => d.Id == WorkspaceEditorId.SdCard);
        Assert.DoesNotContain(
            WorkspaceEditorCatalog.PanelPickableDescriptors,
            d => d.Id == WorkspaceEditorId.Lathe);
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
        Assert.Contains(
            WorkspaceEditorCatalog.AllDescriptors,
            d => d.Id == WorkspaceEditorId.SdCard);
        Assert.Contains(
            WorkspaceEditorCatalog.AllDescriptors,
            d => d.Id == WorkspaceEditorId.Lathe);
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

    [Fact]
    public void FeedLarge_is_pickable()
    {
        Assert.Contains(
            WorkspaceEditorCatalog.LayoutPickableDescriptors,
            d => d.Id == WorkspaceEditorId.FeedLarge);
        Assert.Contains(
            WorkspaceEditorCatalog.PanelPickableDescriptors,
            d => d.Id == WorkspaceEditorId.FeedLarge);
    }

    [Fact]
    public void SpindleLarge_is_pickable()
    {
        Assert.Contains(
            WorkspaceEditorCatalog.LayoutPickableDescriptors,
            d => d.Id == WorkspaceEditorId.SpindleLarge);
        Assert.Contains(
            WorkspaceEditorCatalog.PanelPickableDescriptors,
            d => d.Id == WorkspaceEditorId.SpindleLarge);
    }

    [Theory]
    [InlineData(WorkspaceEditorId.Keyboard)]
    [InlineData(WorkspaceEditorId.NumberPad)]
    public void Popup_keyboard_panels_are_not_pickable_but_remain_registered(WorkspaceEditorId id)
    {
        var descriptor = WorkspaceEditorCatalog.Get(id);

        Assert.DoesNotContain(
            WorkspaceEditorCatalog.LayoutPickableDescriptors,
            d => d.Id == id);
        Assert.DoesNotContain(
            WorkspaceEditorCatalog.PanelPickableDescriptors,
            d => d.Id == id);
        Assert.False(descriptor.RequiresGrblDataContext);
    }
}
