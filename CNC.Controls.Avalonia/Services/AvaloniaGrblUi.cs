using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using CNC.Core;

namespace CNC.Controls.Avalonia.Services;

public static class AvaloniaGrblUi
{
    public static void Configure()
    {
        GrblUi.NotifyError = (message, title) =>
            UiThread.Invoke(() => MessageDialogs.ShowError(message, title));

        GrblUi.Confirm = (message, title) =>
        {
            var result = false;
            UiThread.Invoke(() => result = MessageDialogs.AskYesNo(message, title));
            return result;
        };

        GrblUi.SetClipboardText = text =>
        {
            UiThread.Invoke(() =>
            {
                if (GetMainWindow()?.Clipboard is { } clipboard)
                    _ = clipboard.SetTextAsync(text);
            });
        };
    }

    private static Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }
}
