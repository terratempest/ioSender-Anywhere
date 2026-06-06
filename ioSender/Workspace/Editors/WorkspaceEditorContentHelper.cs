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

        editorHost.MinWidth = Math.Max(0, content.MinWidth);
        editorHost.MinHeight = Math.Max(0, content.MinHeight);
        editorHost.MaxWidth = IsFinite(content.MaxWidth) ? content.MaxWidth : double.PositiveInfinity;
        editorHost.MaxHeight = IsFinite(content.MaxHeight) ? content.MaxHeight : double.PositiveInfinity;

        var fills = WorkspaceEditorCatalog.Get(editorId).FillsWorkspace;
        if (fills && content is Layoutable layoutable)
        {
            layoutable.Width = double.NaN;
            layoutable.Height = double.NaN;
        }

        editorScroll.HorizontalScrollBarVisibility = fills
            ? ScrollBarVisibility.Disabled
            : ScrollBarVisibility.Auto;
        editorScroll.VerticalScrollBarVisibility = fills
            ? ScrollBarVisibility.Disabled
            : ScrollBarVisibility.Auto;
        editorHost.Margin = new Thickness(0);
    }

    static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
