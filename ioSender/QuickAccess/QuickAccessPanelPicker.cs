using Avalonia.Controls;
using CNC.App.Workspace;
using CNC.Localization.Avalonia;
using ioSender.Workspace.Editors;

namespace ioSender.QuickAccess;

public static class QuickAccessPanelPicker
{
    public static void ShowPicker(
        Control anchor,
        Action<WorkspaceEditorId> onSelected,
        bool placeAtPointer = false)
    {
        var menu = BuildMenu(onSelected);
        if (placeAtPointer)
            menu.Open(anchor);
        else
            menu.Open(anchor);
    }

    public static ContextMenu BuildMenu(Action<WorkspaceEditorId> onSelected)
    {
        var menu = new ContextMenu();
        foreach (var desc in WorkspaceEditorCatalog.PanelPickableDescriptors)
        {
            var id = desc.Id;
            var item = new MenuItem
            {
                Header = Localize.T(desc.TitleKey, desc.TitleFallback),
            };
            item.Click += (_, _) =>
            {
                menu.Close();
                onSelected(id);
            };
            menu.Items.Add(item);
        }

        return menu;
    }

    public static MenuItem BuildChangePanelSubmenu(Action<WorkspaceEditorId> onSelected)
    {
        var change = new MenuItem
        {
            Header = Localize.T("ioSender.quickaccess.changePanel", "Change panel"),
        };
        foreach (var desc in WorkspaceEditorCatalog.PanelPickableDescriptors)
        {
            var id = desc.Id;
            change.Items.Add(new MenuItem
            {
                Header = Localize.T(desc.TitleKey, desc.TitleFallback),
                Command = null,
            });
            if (change.Items[^1] is MenuItem pick)
                pick.Click += (_, _) => onSelected(id);
        }

        return change;
    }
}
