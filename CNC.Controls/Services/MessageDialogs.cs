using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;

namespace CNC.Controls.Avalonia.Services;

public static class MessageDialogs
{
    public static void ShowError(string message, string title) =>
        ShowOk(message, title);

    public static void ShowWarning(string message, string title = "ioSender") =>
        ShowOk(message, title);

    static void ShowOk(string message, string title)
    {
        var owner = GetMainWindow();
        var dlg = CreateDialog(title, owner);
        var ok = new Button { Content = "OK", Width = 72 };
        ok.Click += (_, _) => dlg.Close();
        dlg.Content = BuildContent(message, ok);
        if (owner != null)
            dlg.ShowDialog(owner);
        else
            dlg.Show();
    }

    public static bool AskYesNo(string message, string title)
    {
        var owner = GetMainWindow();
        var dlg = CreateDialog(title, owner);
        var result = false;
        var yes = new Button { Content = "Yes", Width = 72, Margin = new Thickness(0, 0, 8, 0) };
        var no = new Button { Content = "No", Width = 72 };
        yes.Click += (_, _) => { result = true; dlg.Close(); };
        no.Click += (_, _) => dlg.Close();
        dlg.Content = BuildContent(message, yes, no);
        if (owner != null)
            dlg.ShowDialog(owner);
        else
            dlg.Show();

        return result;
    }

    private static Window CreateDialog(string title, Window? owner) =>
        new()
        {
            Title = title,
            Width = 420,
            MinHeight = 120,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = owner != null
                ? WindowStartupLocation.CenterOwner
                : WindowStartupLocation.CenterScreen
        };

    private static Control BuildContent(string message, params Button[] buttons)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        foreach (var button in buttons)
            row.Children.Add(button);

        return new StackPanel
        {
            Margin = new Thickness(16),
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 12)
                },
                row
            }
        };
    }

    private static Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            return desktop.MainWindow;
        return null;
    }
}
