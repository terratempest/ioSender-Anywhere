using System.Xml.Serialization;
using CNC.App;
using CNC.App.Workspace;
using CNC.Platform.Abstractions;

namespace ioSender.Workspace;

public static class WorkspaceLayoutDefaults
{
    public const string PresetClassic = "Classic";
    public const string PresetTouch = "Touch";
    public const string PresetXL = "XL";
    public const string PresetDefault = "Default";

    const string LegacyPresetCompact = "Compact";
    const string LegacyPresetExpanded = "Expanded";
    const string ResourcePrefix = "ioSender.DefaultLayouts.";

    static readonly XmlSerializer Serializer = new(typeof(WorkspaceSavedLayout));

    public static WorkspaceNode Default => GetPreset(PresetClassic) ?? new WorkspaceLeaf { Editor = WorkspaceEditorId.Program };

    public static WorkspaceNode? GetPreset(string? name) =>
        TryGetPresetLayout(name, out var layout) ? layout.Root.Clone() : null;

    public static bool TryGetPresetLayout(string? name, out WorkspaceSavedLayout layout)
    {
        var presetName = NormalizePresetName(name);
        if (presetName is null || !IsBundledPreset(presetName))
        {
            layout = new WorkspaceSavedLayout();
            return false;
        }

        if (TryLoadBundledLayout(presetName, out layout))
        {
            layout.Name = presetName;
            return true;
        }

        layout = new WorkspaceSavedLayout();
        return false;
    }

    public static string GetPresetForLayoutMode(UiLayoutMode mode) =>
        mode == UiLayoutMode.Expanded ? PresetXL : PresetClassic;

    public static bool IsBuiltIn(string? name) =>
        string.Equals(name, PresetClassic, StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, PresetTouch, StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, PresetXL, StringComparison.OrdinalIgnoreCase);

    public static bool IsPackagedLayoutName(string? name) =>
        IsBuiltIn(name)
        || string.Equals(name, "ioSender (classic)", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "ioSender (Touch)", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "ioSender (XL)", StringComparison.OrdinalIgnoreCase);

    public static bool IsValid(WorkspaceNode? root) => root is not null && HasRegion(root);

    static string? NormalizePresetName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || string.Equals(name, PresetDefault, StringComparison.OrdinalIgnoreCase))
            return PresetClassic;
        if (string.Equals(name, LegacyPresetCompact, StringComparison.OrdinalIgnoreCase))
            return PresetClassic;
        if (string.Equals(name, LegacyPresetExpanded, StringComparison.OrdinalIgnoreCase))
            return PresetXL;
        if (string.Equals(name, PresetClassic, StringComparison.OrdinalIgnoreCase))
            return PresetClassic;
        if (string.Equals(name, PresetTouch, StringComparison.OrdinalIgnoreCase))
            return PresetTouch;
        if (string.Equals(name, PresetXL, StringComparison.OrdinalIgnoreCase))
            return PresetXL;
        return null;
    }

    static bool IsBundledPreset(string name) =>
        name is PresetClassic or PresetTouch or PresetXL;

    static bool TryLoadBundledLayout(string presetName, out WorkspaceSavedLayout layout)
    {
        try
        {
            using var stream = OpenBundledLayout(presetName);
            if (stream is not null)
            {
                layout = (WorkspaceSavedLayout)Serializer.Deserialize(stream)!;
                if (IsValid(layout.Root))
                    return true;
            }
        }
        catch
        {
        }

        layout = new WorkspaceSavedLayout();
        return false;
    }

    static Stream? OpenBundledLayout(string presetName)
    {
        var resourceName = ResourcePrefix + presetName + ".xml";
        var assembly = typeof(WorkspaceLayoutDefaults).Assembly;
        var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is not null)
            return stream;

        var path = Path.Combine(AppContext.BaseDirectory, "DefaultLayouts", presetName + ".xml");
        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    static bool HasRegion(WorkspaceNode node) => node switch
    {
        WorkspaceLeaf => true,
        WorkspaceTabGroup => true,
        WorkspaceSplit split => HasRegion(split.First) || HasRegion(split.Second),
        _ => false,
    };
}
