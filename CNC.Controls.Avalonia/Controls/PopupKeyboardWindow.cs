using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace CNC.Controls.Avalonia.Controls;

public sealed class PopupKeyboardWindow : Window
{
    readonly TextBox _target;
    readonly PopupKeyboardControl _keyboard;
    string? _resizeEdge;
    Point _resizeStartPointer;
    PixelPoint _resizeStartPosition;
    double _startWidth;
    double _startHeight;

    public PopupKeyboardWindow(TextBox target, PopupKeyboardLayout layout)
    {
        _target = target;
        _keyboard = new PopupKeyboardControl
        {
            Layout = layout,
            UseGlobalTarget = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        _keyboard.ActionInvoked += OnKeyboardActionInvoked;

        Title = layout == PopupKeyboardLayout.Numeric ? "Number Pad" : "Keyboard";
        Width = layout == PopupKeyboardLayout.Numeric ? 320 : 620;
        Height = layout == PopupKeyboardLayout.Numeric ? 260 : 300;
        MinWidth = layout == PopupKeyboardLayout.Numeric ? 220 : 360;
        MinHeight = layout == PopupKeyboardLayout.Numeric ? 190 : 220;
        Topmost = true;
        ShowActivated = false;
        ShowInTaskbar = false;
        CanResize = false;
        WindowDecorations = WindowDecorations.None;
        Background = Brushes.Transparent;
        Content = BuildChrome();
        Opened += OnOpened;
        Closed += (_, _) => _keyboard.ActionInvoked -= OnKeyboardActionInvoked;
    }

    void OnOpened(object? sender, EventArgs e)
    {
        if (OperatingSystem.IsWindows())
            ApplyNoActivateWindowStyle();
    }

    public void PlaceNearTarget()
    {
        var targetTopLeft = _target.PointToScreen(new Point(0, 0));
        var targetBottomRight = _target.PointToScreen(new Point(_target.Bounds.Width, _target.Bounds.Height));
        var screen = Screens.ScreenFromPoint(targetTopLeft) ?? Screens.Primary;
        var area = screen?.WorkingArea;
        if (area is null)
        {
            Position = new PixelPoint(targetTopLeft.X, targetBottomRight.Y + 4);
            return;
        }

        var scale = TopLevel.GetTopLevel(_target)?.RenderScaling ?? RenderScaling;
        if (scale <= 0)
            scale = 1;

        var margin = (int)Math.Round(8 * scale);
        var popupWidth = Math.Max((int)Math.Ceiling(Width * scale), (int)Math.Ceiling(MinWidth * scale));
        var popupHeight = Math.Max((int)Math.Ceiling(Height * scale), (int)Math.Ceiling(MinHeight * scale));
        var targetWidth = Math.Max(1, targetBottomRight.X - targetTopLeft.X);
        var targetHeight = Math.Max(1, targetBottomRight.Y - targetTopLeft.Y);

        var candidates = new[]
        {
            new PixelPoint(targetTopLeft.X, targetBottomRight.Y + margin),
            new PixelPoint(targetTopLeft.X, targetTopLeft.Y - popupHeight - margin),
            new PixelPoint(targetBottomRight.X + margin, targetTopLeft.Y),
            new PixelPoint(targetTopLeft.X - popupWidth - margin, targetTopLeft.Y),
        };

        foreach (var candidate in candidates)
        {
            var clamped = ClampToWorkArea(candidate, popupWidth, popupHeight, area.Value);
            if (Fits(clamped, popupWidth, popupHeight, area.Value)
                && !OverlapsTarget(clamped, popupWidth, popupHeight, targetTopLeft, targetWidth, targetHeight))
            {
                Position = clamped;
                return;
            }
        }

        var belowSpace = area.Value.Bottom - targetBottomRight.Y - margin;
        var aboveSpace = targetTopLeft.Y - area.Value.Y - margin;
        var fallback = belowSpace >= aboveSpace
            ? new PixelPoint(targetTopLeft.X, targetBottomRight.Y + margin)
            : new PixelPoint(targetTopLeft.X, targetTopLeft.Y - popupHeight - margin);
        Position = ClampToWorkArea(fallback, popupWidth, popupHeight, area.Value);
    }

    static PixelPoint ClampToWorkArea(PixelPoint position, int width, int height, PixelRect area)
    {
        var maxX = Math.Max(area.X, area.Right - width);
        var maxY = Math.Max(area.Y, area.Bottom - height);
        return new PixelPoint(
            Math.Clamp(position.X, area.X, maxX),
            Math.Clamp(position.Y, area.Y, maxY));
    }

    static bool Fits(PixelPoint position, int width, int height, PixelRect area) =>
        position.X >= area.X
        && position.Y >= area.Y
        && position.X + width <= area.Right
        && position.Y + height <= area.Bottom;

    static bool OverlapsTarget(PixelPoint position, int width, int height, PixelPoint targetTopLeft, int targetWidth, int targetHeight) =>
        position.X < targetTopLeft.X + targetWidth
        && position.X + width > targetTopLeft.X
        && position.Y < targetTopLeft.Y + targetHeight
        && position.Y + height > targetTopLeft.Y;

    Control BuildChrome()
    {
        var title = new TextBlock
        {
            Text = Title,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0),
        };

        var close = new Button
        {
            Content = "X",
            Focusable = false,
            Width = 34,
            Height = 28,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        close.Click += (_, _) => Close();

        var header = new Border
        {
            Height = 34,
            Background = Application.Current?.FindResource("ThemeControlMidBrush") as IBrush ?? Brushes.DimGray,
            BorderBrush = Application.Current?.FindResource("ThemeBorderLowBrush") as IBrush ?? Brushes.Gray,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children =
                {
                    title,
                    close,
                },
            },
        };
        Grid.SetColumn(close, 1);
        header.PointerPressed += OnHeaderPointerPressed;

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Background = Application.Current?.FindResource("ThemeControlLowBrush") as IBrush ?? Brushes.Black,
        };
        root.Children.Add(header);
        Grid.SetRow(_keyboard, 1);
        root.Children.Add(_keyboard);
        AddResizeGrip(root, "E", Cursor.Parse("RightSide"), HorizontalAlignment.Right, VerticalAlignment.Stretch, 6, double.NaN);
        AddResizeGrip(root, "S", Cursor.Parse("BottomSide"), HorizontalAlignment.Stretch, VerticalAlignment.Bottom, double.NaN, 6);
        AddResizeGrip(root, "SE", Cursor.Parse("BottomRightCorner"), HorizontalAlignment.Right, VerticalAlignment.Bottom, 12, 12);

        return new Border
        {
            BorderBrush = Application.Current?.FindResource("ThemeBorderLowBrush") as IBrush ?? Brushes.Gray,
            BorderThickness = new Thickness(1),
            Child = root,
        };
    }

    void AddResizeGrip(
        Grid root,
        string edge,
        Cursor cursor,
        HorizontalAlignment horizontal,
        VerticalAlignment vertical,
        double width,
        double height)
    {
        var grip = new Border
        {
            Tag = edge,
            Background = Brushes.Transparent,
            Cursor = cursor,
            HorizontalAlignment = horizontal,
            VerticalAlignment = vertical,
            Width = width,
            Height = height,
        };
        Grid.SetRowSpan(grip, 2);
        grip.PointerPressed += OnResizePointerPressed;
        grip.PointerMoved += OnResizePointerMoved;
        grip.PointerReleased += OnResizePointerReleased;
        root.Children.Add(grip);
    }

    void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        BeginMoveDrag(e);
        e.Handled = true;
    }

    void OnResizePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control grip || grip.Tag is not string edge)
            return;
        if (!e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed)
            return;

        _resizeEdge = edge;
        _resizeStartPointer = e.GetPosition(this);
        _resizeStartPosition = Position;
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

        if (_resizeEdge.Contains('E'))
            Width = Math.Max(MinWidth, _startWidth + dx);
        if (_resizeEdge.Contains('S'))
            Height = Math.Max(MinHeight, _startHeight + dy);
        Position = _resizeStartPosition;
        e.Handled = true;
    }

    void OnResizePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_resizeEdge is null)
            return;

        _resizeEdge = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    void OnKeyboardActionInvoked(object? sender, PopupKeyboardAction action)
    {
        var shouldClose = TextBoxKeyboardEditor.Apply(_target, action);
        if (action.Kind == PopupKeyboardActionKind.Enter)
            TopLevel.GetTopLevel(_target)?.Focus();
        if (shouldClose)
            Close();
    }

    const int GWL_EXSTYLE = -20;
    const int WS_EX_NOACTIVATE = 0x08000000;

    void ApplyNoActivateWindowStyle()
    {
        var handle = TryGetPlatformHandle();
        if (handle is null || handle.Handle == IntPtr.Zero)
            return;

        var exStyle = GetWindowLongPtr(handle.Handle, GWL_EXSTYLE);
        SetWindowLongPtr(handle.Handle, GWL_EXSTYLE, exStyle | WS_EX_NOACTIVATE);
    }

    static IntPtr GetWindowLongPtr(IntPtr hwnd, int index) =>
        IntPtr.Size == 8
            ? GetWindowLongPtr64(hwnd, index)
            : new IntPtr(GetWindowLong32(hwnd, index));

    static IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value) =>
        IntPtr.Size == 8
            ? SetWindowLongPtr64(hwnd, index, value)
            : new IntPtr(SetWindowLong32(hwnd, index, value.ToInt32()));

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}
