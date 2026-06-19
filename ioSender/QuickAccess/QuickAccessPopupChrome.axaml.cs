using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CNC.App;
using CNC.App.Workspace;
using CNC.Localization.Avalonia;
using ioSender.Workspace.Editors;

namespace ioSender.QuickAccess;

public partial class QuickAccessPopupChrome : UserControl
{
    string? _resizeEdge;
    Point _resizeStartPointer;
    double _startWidth;
    double _startHeight;

    public event EventHandler? CloseRequested;
    public event EventHandler<Size>? ResizeCompleted;

    public QuickAccessPopupChrome()
    {
        InitializeComponent();
        WireResize(ResizeS);
        WireResize(ResizeW);
        WireResize(ResizeE);
        WireResize(ResizeSW);
        WireResize(ResizeSE);
    }

    public void ConfigureResizeGrips(QuickAccessSidebarDock dock)
    {
        var fromRight = dock == QuickAccessSidebarDock.Right;
        ResizeS.IsVisible = true;
        ResizeW.IsVisible = fromRight;
        ResizeSW.IsVisible = fromRight;
        ResizeE.IsVisible = !fromRight;
        ResizeSE.IsVisible = !fromRight;
    }

    public void SetTitle(WorkspaceEditorId editorId)
    {
        var desc = WorkspaceEditorCatalog.Get(editorId);
        TitleText.Text = WorkspaceEditorTitles.HeaderTitle(desc);
    }

    public void SetEditor(WorkspaceEditorId editorId, Control content)
    {
        WorkspaceEditorContentHelper.ApplyToScrollHost(editorId, content, EditorHost, EditorScroll);
    }

    void WireResize(Control grip)
    {
        grip.PointerPressed += OnResizePointerPressed;
        grip.PointerMoved += OnResizePointerMoved;
        grip.PointerReleased += OnResizePointerReleased;
    }

    void OnResizePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control grip || grip.Tag is not string edge)
            return;
        if (!e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed)
            return;

        _resizeEdge = edge;
        _resizeStartPointer = e.GetPosition(this);
        _startWidth = Width;
        _startHeight = Height;
        e.Pointer.Capture(grip);
        e.Handled = true;
    }

    void OnResizePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_resizeEdge is null)
            return;

        var pos = e.GetPosition(this);
        var dx = pos.X - _resizeStartPointer.X;
        var dy = pos.Y - _resizeStartPointer.Y;
        var width = _startWidth;
        var height = _startHeight;

        switch (_resizeEdge)
        {
            case "E":
                width = Math.Max(MinWidth, _startWidth + dx);
                break;
            case "W":
                width = Math.Max(MinWidth, _startWidth - dx);
                break;
            case "S":
                height = Math.Max(MinHeight, _startHeight + dy);
                break;
            case "SE":
                width = Math.Max(MinWidth, _startWidth + dx);
                height = Math.Max(MinHeight, _startHeight + dy);
                break;
            case "SW":
                width = Math.Max(MinWidth, _startWidth - dx);
                height = Math.Max(MinHeight, _startHeight + dy);
                break;
        }

        Width = width;
        Height = height;
        e.Handled = true;
    }

    void OnResizePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_resizeEdge is null)
            return;

        _resizeEdge = null;
        e.Pointer.Capture(null);
        ResizeCompleted?.Invoke(this, new Size(Width, Height));
        e.Handled = true;
    }

    void OnCloseClick(object? sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
}
