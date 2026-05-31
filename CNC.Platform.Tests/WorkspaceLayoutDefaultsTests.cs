using CNC.App.Workspace;
using ioSender.Workspace;

namespace CNC.Platform.Tests;

public class WorkspaceLayoutDefaultsTests
{
    [Theory]
    [InlineData("Classic")]
    [InlineData("Touch")]
    [InlineData("XL")]
    public void GetPreset_loads_bundled_layouts(string name)
    {
        var root = WorkspaceLayoutDefaults.GetPreset(name);

        Assert.NotNull(root);
        Assert.True(WorkspaceLayoutDefaults.IsValid(root));
        Assert.NotEmpty(root!.EnumerateEditors());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Default")]
    [InlineData("Compact")]
    public void GetPreset_maps_default_and_legacy_compact_to_classic(string? name)
    {
        var expected = WorkspaceLayoutDefaults.GetPreset("Classic");
        var actual = WorkspaceLayoutDefaults.GetPreset(name);

        Assert.Equal(EditorSequence(expected), EditorSequence(actual));
    }

    [Fact]
    public void GetPreset_maps_legacy_expanded_to_xl()
    {
        var expected = WorkspaceLayoutDefaults.GetPreset("XL");
        var actual = WorkspaceLayoutDefaults.GetPreset("Expanded");

        Assert.Equal(EditorSequence(expected), EditorSequence(actual));
    }

    [Theory]
    [InlineData("Classic", true)]
    [InlineData("Touch", true)]
    [InlineData("XL", true)]
    [InlineData("Compact", false)]
    [InlineData("Expanded", false)]
    [InlineData("Custom", false)]
    public void IsBuiltIn_only_protects_current_builtin_names(string name, bool expected)
    {
        Assert.Equal(expected, WorkspaceLayoutDefaults.IsBuiltIn(name));
    }

    [Theory]
    [InlineData("ioSender (classic)")]
    [InlineData("ioSender (Touch)")]
    [InlineData("ioSender (XL)")]
    public void IsPackagedLayoutName_filters_original_import_names(string name)
    {
        Assert.True(WorkspaceLayoutDefaults.IsPackagedLayoutName(name));
    }

    static WorkspaceEditorId[] EditorSequence(WorkspaceNode? root) =>
        Assert.IsAssignableFrom<WorkspaceNode>(root).EnumerateEditors().ToArray();
}
