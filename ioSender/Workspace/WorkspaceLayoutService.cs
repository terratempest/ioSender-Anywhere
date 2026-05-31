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
        var root = WorkspaceLayoutSanitizer.Sanitize(config.WorkspaceRoot);
        if (root is null || !WorkspaceLayoutDefaults.IsValid(root))
        {
            var preset = ResolvePreset(config);
            root = preset.Root.Clone();
            ApplyQuickAccessSidebar(preset);
        }

        config.WorkspaceRoot = root;
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

    static WorkspaceSavedLayout ResolvePreset(BaseConfig config)
    {
        if (WorkspaceLayoutDefaults.TryGetPresetLayout(config.WorkspacePreset, out var preset))
            return preset;

        return WorkspaceLayoutDefaults.TryGetPresetLayout(
            WorkspaceLayoutDefaults.GetPresetForLayoutMode(config.LayoutMode),
            out preset)
            ? preset
            : new WorkspaceSavedLayout { Name = WorkspaceLayoutDefaults.PresetClassic, Root = WorkspaceLayoutDefaults.Default };
    }

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
        if (!TryApplyLayout(presetName))
        {
            AppHostContext.AppConfig.Base.WorkspacePreset = WorkspaceLayoutDefaults.PresetClassic;
            AppHostContext.AppConfig.Base.WorkspaceRoot = WorkspaceLayoutDefaults.Default.Clone();
        }
    }

    public static bool TryApplyLayout(string layoutName)
    {
        if (WorkspaceLayoutDefaults.TryGetPresetLayout(layoutName, out var preset))
        {
            AppHostContext.AppConfig.Base.WorkspacePreset = preset.Name;
            AppHostContext.AppConfig.Base.WorkspaceRoot = preset.Root.Clone();
            ApplyQuickAccessSidebar(preset);
            return true;
        }

        if (WorkspaceLayoutFileService.TryLoadByName(layoutName, out var saved))
        {
            var root = WorkspaceLayoutSanitizer.Sanitize(saved.Root.Clone());
            if (root is not null && WorkspaceLayoutDefaults.IsValid(root))
            {
                AppHostContext.AppConfig.Base.WorkspacePreset = saved.Name;
                AppHostContext.AppConfig.Base.WorkspaceRoot = root;
                if (saved.QuickAccessSidebar is { } quickAccessSidebar)
                {
                    var sidebar = quickAccessSidebar.Clone();
                    sidebar.LegacySidesMigrated = true;
                    AppHostContext.AppConfig.Base.QuickAccessSidebar = sidebar;
                }

                return true;
            }
        }

        return false;
    }

    static void ApplyQuickAccessSidebar(WorkspaceSavedLayout layout)
    {
        if (layout.QuickAccessSidebar is not { } quickAccessSidebar)
            return;

        var sidebar = quickAccessSidebar.Clone();
        sidebar.LegacySidesMigrated = true;
        AppHostContext.AppConfig.Base.QuickAccessSidebar = sidebar;
    }

    public static string ActiveLayoutName => AppHostContext.AppConfig.Base.WorkspacePreset;

    public static void Persist() => AppHostContext.AppConfig.Save();
}
