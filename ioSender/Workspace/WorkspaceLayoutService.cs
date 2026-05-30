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

        EnsureRegionIds(config.WorkspaceRoot);
        return config.WorkspaceRoot;
    }

    static void EnsureRegionIds(WorkspaceNode root)
    {
        switch (root)
        {
            case WorkspaceLeaf leaf:
                if (leaf.Id == Guid.Empty)
                    leaf.Id = Guid.NewGuid();
                break;
            case WorkspaceSplit split:
                EnsureRegionIds(split.First);
                EnsureRegionIds(split.Second);
                break;
            case WorkspaceTabGroup tabGroup:
                if (tabGroup.Id == Guid.Empty)
                    tabGroup.Id = Guid.NewGuid();
                foreach (var tab in tabGroup.Tabs)
                {
                    if (tab.Id == Guid.Empty)
                        tab.Id = Guid.NewGuid();
                }
                if (!tabGroup.Tabs.Any(t => t.Id == tabGroup.ActiveTabId))
                    tabGroup.ActiveTabId = tabGroup.Tabs.FirstOrDefault()?.Id ?? Guid.Empty;
                break;
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

    public static void SaveRoot(WorkspaceNode root, string layoutName)
    {
        AppHostContext.AppConfig.Base.WorkspaceRoot = root;
        AppHostContext.AppConfig.Base.WorkspacePreset = layoutName;
    }

    public static void ApplyPreset(string presetName)
    {
        var root = WorkspaceLayoutDefaults.GetPreset(presetName) ?? WorkspaceLayoutDefaults.Default.Clone();
        AppHostContext.AppConfig.Base.WorkspacePreset = presetName;
        AppHostContext.AppConfig.Base.WorkspaceRoot = root.Clone();
    }

    public static bool TryApplyLayout(string layoutName)
    {
        if (WorkspaceLayoutDefaults.GetPreset(layoutName) is { } preset)
        {
            AppHostContext.AppConfig.Base.WorkspacePreset = layoutName;
            AppHostContext.AppConfig.Base.WorkspaceRoot = preset.Clone();
            return true;
        }

        if (WorkspaceLayoutFileService.TryLoadByName(layoutName, out var saved)
            && WorkspaceLayoutDefaults.IsValid(saved.Root))
        {
            AppHostContext.AppConfig.Base.WorkspacePreset = saved.Name;
            AppHostContext.AppConfig.Base.WorkspaceRoot = saved.Root.Clone();
            return true;
        }

        return false;
    }

    public static string ActiveLayoutName => AppHostContext.AppConfig.Base.WorkspacePreset;

    public static void Persist() => AppHostContext.AppConfig.Save();
}
