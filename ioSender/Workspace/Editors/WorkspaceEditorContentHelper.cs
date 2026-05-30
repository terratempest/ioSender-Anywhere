using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using CNC.App.Workspace;
using ioSender.Workspace.Controls;

namespace ioSender.Workspace.Editors;

public static class WorkspaceEditorContentHelper
{
    public static void ApplyToScrollHost(
        WorkspaceEditorId editorId,
        Control content,
        ContentPresenter editorHost,
        ScrollViewer editorScroll)
    {
        if (editorHost.Content is Control previous && !ReferenceEquals(previous, content))
            WorkspaceRegionChrome.DetachEditor(previous);
        WorkspaceRegionChrome.DetachEditor(content);
        editorHost.Content = content;

        var fills = WorkspaceEditorCatalog.Get(editorId).FillsWorkspace;
        content.HorizontalAlignment = fills ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
        content.VerticalAlignment = fills ? VerticalAlignment.Stretch : VerticalAlignment.Top;

        if (fills && content is Layoutable layoutable)
        {
            layoutable.Width = double.NaN;
            layoutable.Height = double.NaN;
        }

        if (fills)
        {
            editorScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            editorScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            editorHost.Margin = new Thickness(0);
        }
        else
        {
            editorScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            editorScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            editorHost.Margin = new Thickness(2);
        }
    }
}
