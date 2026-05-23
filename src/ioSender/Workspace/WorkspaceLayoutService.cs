using CNC.App;
using CNC.App.Workspace;
using CNC.Platform.Abstractions;
using ioSender.Services;

namespace ioSender.Workspace;

public static class WorkspaceLayoutService
{
    public static WorkspaceNode EnsureRoot()
    {
        var config = AppHostContext.AppConfig.Base;
        if (config.WorkspaceRoot is null || !WorkspaceLayoutDefaults.IsValid(config.WorkspaceRoot))
            config.WorkspaceRoot = ResolvePreset(config).Clone();

        EnsureLeafIds(config.WorkspaceRoot);
        return config.WorkspaceRoot;
    }

    static void EnsureLeafIds(WorkspaceNode root)
    {
        foreach (var leaf in root.EnumerateLeaves())
        {
            if (leaf.Id == Guid.Empty)
                leaf.Id = Guid.NewGuid();
        }
    }

    static WorkspaceNode ResolvePreset(BaseConfig config) =>
        WorkspaceLayoutDefaults.GetPreset(config.WorkspacePreset)
        ?? (config.LayoutMode == UiLayoutMode.Expanded
            ? WorkspaceLayoutDefaults.Expanded
            : WorkspaceLayoutDefaults.Compact);

    public static void SaveRoot(WorkspaceNode root)
    {
        AppHostContext.AppConfig.Base.WorkspaceRoot = root;
        AppHostContext.AppConfig.Base.WorkspacePreset = string.Empty;
    }

    public static void ApplyPreset(string presetName)
    {
        var root = WorkspaceLayoutDefaults.GetPreset(presetName) ?? WorkspaceLayoutDefaults.Default.Clone();
        AppHostContext.AppConfig.Base.WorkspacePreset = presetName;
        AppHostContext.AppConfig.Base.WorkspaceRoot = root.Clone();
    }

    public static void Persist() => AppHostContext.AppConfig.Save();
}
