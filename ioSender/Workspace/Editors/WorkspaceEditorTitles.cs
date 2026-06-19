using CNC.Localization.Avalonia;

namespace ioSender.Workspace.Editors;

public static class WorkspaceEditorTitles
{
    public static string SelectionTitle(WorkspaceEditorDescriptor descriptor) =>
        Localize.T(descriptor.TitleKey, descriptor.TitleFallback);

    public static string HeaderTitle(WorkspaceEditorDescriptor descriptor) =>
        Localize.T(
            descriptor.HeaderTitleKey ?? descriptor.TitleKey,
            descriptor.HeaderTitleFallback ?? descriptor.TitleFallback);
}
