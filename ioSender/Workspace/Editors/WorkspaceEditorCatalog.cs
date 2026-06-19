using CNC.App.Workspace;

namespace ioSender.Workspace.Editors;

public static class WorkspaceEditorCatalog
{
    static readonly WorkspaceEditorDescriptor[] All =
    [
        Desc(WorkspaceEditorId.TabGroup, "ioSender.workspace.editor_tabgroup", "Tab Group", 200, 120, grbl: false, fillsWorkspace: true),
        Desc(WorkspaceEditorId.Program, "ioSender.workspace.editor_program", "Program", 200, 120, fillsWorkspace: true),
        Desc(WorkspaceEditorId.Viewer3D, "ioSender.workspace.editor_viewer3d", "3D View", 200, 160, fillsWorkspace: true),
        Desc(WorkspaceEditorId.Console, "ioSender.workspace.editor_console", "Console", 200, 100),
        Desc(WorkspaceEditorId.Mdi, "CNC.Controls.Avalonia.mdicontrol.grp_mdi", "MDI", 200, 100, fillsWorkspace: true),
        Desc(WorkspaceEditorId.MdiTouch, "ioSender.workspace.editor_mdiTouch", "MDI (Touch)", 360, 250, fillsWorkspace: true),
        Desc(WorkspaceEditorId.Dro, "CNC.Controls.Avalonia.drocontrol.grp_dro", "DRO", 160, 120, fillsWorkspace: true),
        Desc(WorkspaceEditorId.ProgramLimits, "ioSender.workspace.editor_programLimits", "Program limits", 216, 120, fillsWorkspace: true),
        Desc(WorkspaceEditorId.Signals, "ioSender.workspace.editor_signals", "Signals", 216, 60, fillsWorkspace: true),
        Desc(WorkspaceEditorId.Status, "ioSender.workspace.editor_status", "Status", 216, 66, fillsWorkspace: true),
        Desc(WorkspaceEditorId.Jog, "ioSender.workspace.editor_jog", "Jog", 280, 200, fillsWorkspace: true),
        Desc(WorkspaceEditorId.Outline, "ioSender.workspace.editor_outline", "Outline", 250, 125, fillsWorkspace: true),
        Desc(WorkspaceEditorId.Goto, "ioSender.workspace.editor_goto", "Goto", 250, 100, fillsWorkspace: true),
        Desc(WorkspaceEditorId.WorkParams, "ioSender.workspace.editor_workparams", "Work Parameters", 200, 72, fillsWorkspace: true),
        Desc(WorkspaceEditorId.Spindle, "ioSender.workspace.editor_spindle", "Spindle", 250, 90, fillsWorkspace: true),
        Desc(WorkspaceEditorId.SpindleTouch, "ioSender.workspace.editor_spindleTouch", "Spindle (Touch)", 220, 250, fillsWorkspace: true, headerKey: "ioSender.workspace.editor_spindle", headerFallback: "Spindle"),
        Desc(WorkspaceEditorId.SpindleLarge, "ioSender.workspace.editor_spindleLarge", "Spindle (Large)", 360, 260, fillsWorkspace: true, headerKey: "ioSender.workspace.editor_spindle", headerFallback: "Spindle"),
        Desc(WorkspaceEditorId.Coolant, "ioSender.workspace.editor_coolant", "Coolant", 250, 80, fillsWorkspace: true),
        Desc(WorkspaceEditorId.Feed, "ioSender.workspace.editor_feed", "Feed", 250, 110, fillsWorkspace: true),
        Desc(WorkspaceEditorId.FeedTouch, "ioSender.workspace.editor_feedTouch", "Feed (Touch)", 220, 200, fillsWorkspace: true, headerKey: "ioSender.workspace.editor_feed", headerFallback: "Feed"),
        Desc(WorkspaceEditorId.FeedLarge, "ioSender.workspace.editor_feedLarge", "Feed (Large)", 360, 260, fillsWorkspace: true, headerKey: "ioSender.workspace.editor_feed", headerFallback: "Feed"),
        Desc(WorkspaceEditorId.JobBar, "ioSender.workspace.editor_jobbar", "Job", 400, 40, minHeight: 36, fillsWorkspace: true),
        Desc(WorkspaceEditorId.Macros, "ioSender.workspace.editor_macros", "Macros", 300, 160, fillsWorkspace: true),
        Desc(WorkspaceEditorId.Keyboard, "ioSender.workspace.editor_keyboard", "Keyboard", 360, 220, grbl: false, fillsWorkspace: true),
        Desc(WorkspaceEditorId.NumberPad, "ioSender.workspace.editor_numberPad", "Number Pad", 220, 190, grbl: false, fillsWorkspace: true),
        Desc(WorkspaceEditorId.Probing, "ioSender.mainwindow.tab_probing", "Probing", 400, 300, activation: true),
        Desc(WorkspaceEditorId.SdCard, "ioSender.mainwindow.tab_sdCard", "SD Card", 300, 200, activation: true),
        Desc(WorkspaceEditorId.Lathe, "ioSender.mainwindow.tab_latheWizards", "Lathe Wizards", 400, 300, activation: true, grbl: false),
        Desc(WorkspaceEditorId.Offsets, "ioSender.mainwindow.tab_offsets", "Offsets", 300, 200, activation: true),
        Desc(WorkspaceEditorId.Tools, "ioSender.mainwindow.tab_tools", "Tools", 300, 200, activation: true),
        Desc(WorkspaceEditorId.GrblConfig, "ioSender.mainwindow.tab_grblConfig", "Grbl Config", 400, 300, activation: true),
        Desc(WorkspaceEditorId.AppConfig, "ioSender.mainwindow.tab_appConfig", "Application", 400, 300, grbl: false),
    ];

    static WorkspaceEditorDescriptor Desc(
        WorkspaceEditorId id,
        string key,
        string fallback,
        double minW,
        double minH,
        bool activation = false,
        bool grbl = true,
        double minHeight = 0,
        bool fillsWorkspace = false,
        string? headerKey = null,
        string? headerFallback = null) =>
        new()
        {
            Id = id,
            TitleKey = key,
            TitleFallback = fallback,
            HeaderTitleKey = headerKey,
            HeaderTitleFallback = headerFallback,
            MinWidth = minW,
            MinHeight = minHeight > 0 ? minHeight : minH,
            RequiresGrblDataContext = grbl,
            SupportsActivation = activation,
            FillsWorkspace = fillsWorkspace,
        };

    public static IReadOnlyList<WorkspaceEditorDescriptor> AllDescriptors => All;

    /// <summary>Editors assignable as content panels (excludes shell-level pages and layout containers).</summary>
    public static IEnumerable<WorkspaceEditorDescriptor> PanelPickableDescriptors =>
        All.Where(d => d.Id is not (
            WorkspaceEditorId.TabGroup
            or
            WorkspaceEditorId.Probing
            or WorkspaceEditorId.SdCard
            or WorkspaceEditorId.Lathe
            or WorkspaceEditorId.Offsets
            or WorkspaceEditorId.GrblConfig
            or WorkspaceEditorId.AppConfig
            or WorkspaceEditorId.Signals
            or WorkspaceEditorId.Status
            or WorkspaceEditorId.Keyboard
            or WorkspaceEditorId.NumberPad));

    /// <summary>Entries assignable in the Home workspace layout picker.</summary>
    public static IEnumerable<WorkspaceEditorDescriptor> LayoutPickableDescriptors =>
        All.Where(d => d.Id is not (
            WorkspaceEditorId.Probing
            or WorkspaceEditorId.SdCard
            or WorkspaceEditorId.Lathe
            or WorkspaceEditorId.Offsets
            or WorkspaceEditorId.GrblConfig
            or WorkspaceEditorId.AppConfig
            or WorkspaceEditorId.Signals
            or WorkspaceEditorId.Status
            or WorkspaceEditorId.Keyboard
            or WorkspaceEditorId.NumberPad));

    public static WorkspaceEditorDescriptor Get(WorkspaceEditorId id) =>
        All.First(d => d.Id == id);
}
