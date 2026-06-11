using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System.Diagnostics;
using CNC.App;
using CNC.Converters;
using CNC.Controls.Avalonia.Controls;
using CNC.Controls.Avalonia.Services;
using CNC.Controls.Avalonia.Views;
using CNC.Controls.Config;
using CNC.Controls.DragKnife;
using CNC.Controls.Lathe;
using CNC.Controls.Probing;
using CNC.Core;
using CNC.GCodeViewer.Avalonia;
using CNC.GCodeViewer.Avalonia.Views;
using CNC.Localization.Avalonia;
using ioSender.Navigation;
using ioSender.QuickAccess;
using ioSender.Services;
using ioSender.ViewModels;
using ioSender.Views;
using ioSender.Workspace;

namespace ioSender;

public partial class MainWindow : Window
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty)
        {
            UpdateWindowChromeState();
        }
    }

    void OnWindowMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    void OnWindowMaximizeClick(object? sender, RoutedEventArgs e) => ToggleMaximized();

    void OnWindowFullscreenClick(object? sender, RoutedEventArgs e) => ToggleFullscreen();

    void OnWindowCloseClick(object? sender, RoutedEventArgs e) => Close();

    void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (WindowState == WindowState.FullScreen)
            return;

        if (!e.GetCurrentPoint((Visual)sender!).Properties.IsLeftButtonPressed)
            return;

        BeginMoveDrag(e);
        e.Handled = true;
    }

    void ToggleMaximized()
    {
        if (WindowState == WindowState.FullScreen)
            return;

        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    void ToggleFullscreen()
    {
        if (WindowState == WindowState.FullScreen)
        {
            WindowState = _preFullscreenWindowState == WindowState.Maximized
                ? WindowState.Maximized
                : WindowState.Normal;
            return;
        }

        _preFullscreenWindowState = WindowState == WindowState.Maximized
            ? WindowState.Maximized
            : WindowState.Normal;
        SaveWindowPlacement();
        WindowState = WindowState.FullScreen;
    }

    void UpdateWindowChromeState()
    {
        var isFullscreen = WindowState == WindowState.FullScreen;
        var isMaximized = WindowState == WindowState.Maximized;

        BtnWindowMaximize.IsEnabled = !isFullscreen;
        IconWindowMaximize.Data = Geometry.Parse(isMaximized
            ? "M8,5 L19,5 L19,16 L16,16 L16,19 L5,19 L5,8 L8,8 Z M10,7 L10,8 L16,8 L16,14 L17,14 L17,7 Z M7,10 L7,17 L14,17 L14,10 Z"
            : "M5,5 L19,5 L19,19 L5,19 Z M7,7 L7,17 L17,17 L17,7 Z");
        ToolTip.SetTip(BtnWindowMaximize, isMaximized ? "Restore" : "Maximize");

        IconWindowFullscreen.Data = Geometry.Parse(isFullscreen
            ? "M8,4 L10,4 L10,10 L4,10 L4,8 L7,8 L7,5 L8,5 Z M14,4 L16,4 L16,7 L19,7 L19,8 L20,8 L20,10 L14,10 Z M4,14 L10,14 L10,20 L8,20 L8,17 L5,17 L5,16 L4,16 Z M14,14 L20,14 L20,16 L17,16 L17,19 L16,19 L16,20 L14,20 Z"
            : "M4,4 L10,4 L10,6 L7,6 L7,9 L5,9 L5,5 L4,5 Z M14,4 L20,4 L20,10 L18,10 L18,7 L15,7 L15,5 L19,5 L19,4 Z M5,14 L7,14 L7,17 L10,17 L10,19 L4,19 L4,18 L5,18 Z M18,14 L20,14 L20,20 L14,20 L14,18 L17,18 L17,15 L18,15 Z");
        ToolTip.SetTip(BtnWindowFullscreen, isFullscreen ? "Exit fullscreen" : "Fullscreen");
    }

    protected override void OnResized(WindowResizedEventArgs e)
    {
        base.OnResized(e);
        SaveWindowPlacement();
    }

    void RestoreWindowPlacement()
    {
        // Guard: Prevent the previewer from executing this method
        if (Avalonia.Controls.Design.IsDesignMode)
        {
            return;
        }
        
        var config = AppHostContext.AppConfig.Base;
        if (!config.KeepWindowSize)
            return;

        _restoringPlacement = true;
        try
        {
            if (config.WindowWidth == -1 || config.WindowMaximized)
            {
                _preFullscreenWindowState = WindowState.Maximized;
                WindowState = WindowState.Maximized;
                if (config.WindowFullscreen)
                    WindowState = WindowState.FullScreen;
                return;
            }

            var screen = config.WindowLeft >= 0 && config.WindowTop >= 0
                ? Screens.ScreenFromPoint(new PixelPoint(
                    (int)Math.Round(config.WindowLeft),
                    (int)Math.Round(config.WindowTop)))
                : Screens.Primary;
            var workArea = screen?.WorkingArea;
            var maxWidth = workArea?.Width ?? 3840;
            var maxHeight = workArea?.Height ?? 2160;

            Width = Math.Max(Math.Min(config.WindowWidth, maxWidth), MinWidth);
            Height = Math.Max(Math.Min(config.WindowHeight, maxHeight), MinHeight);

            if (workArea is { } area && config.WindowLeft >= 0 && config.WindowTop >= 0)
            {
                var left = Clamp((int)Math.Round(config.WindowLeft), area.X, Math.Max(area.X, area.Right - (int)Math.Round(Width)));
                var top = Clamp((int)Math.Round(config.WindowTop), area.Y, Math.Max(area.Y, area.Bottom - (int)Math.Round(Height)));
                Position = new PixelPoint(left, top);
                WindowStartupLocation = WindowStartupLocation.Manual;
            }

            _preFullscreenWindowState = WindowState.Normal;
            if (config.WindowFullscreen)
                WindowState = WindowState.FullScreen;
        }
        finally
        {
            _restoringPlacement = false;
        }
    }

    void SaveWindowPlacement()
    {
        // Guard: Prevent the previewer from executing this method
        if (Avalonia.Controls.Design.IsDesignMode)
        {
            return;
        }
        if (_restoringPlacement)
            return;

        var config = AppHostContext.AppConfig.Base;
        if (!config.KeepWindowSize)
            return;

        if (WindowState == WindowState.FullScreen)
        {
            config.WindowFullscreen = true;
            return;
        }

        config.WindowFullscreen = false;
        config.WindowMaximized = WindowState == WindowState.Maximized;

        if (WindowState != WindowState.Maximized && WindowState != WindowState.Minimized)
        {
            config.WindowWidth = Bounds.Width > 0 ? Bounds.Width : Width;
            config.WindowHeight = Bounds.Height > 0 ? Bounds.Height : Height;
            config.WindowLeft = Position.X;
            config.WindowTop = Position.Y;
        }
    }

    void BringToForeground()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        Activate();
        Topmost = true;
        Dispatcher.UIThread.Post(() =>
        {
            Topmost = false;
            Activate();
            Focus();
        }, DispatcherPriority.Loaded);
    }

    static int Clamp(int value, int min, int max) =>
        Math.Min(Math.Max(value, min), max);
}
