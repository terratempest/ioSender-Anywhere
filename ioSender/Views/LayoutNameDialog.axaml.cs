using Avalonia.Controls;
using Avalonia.Input;
using ioSender.Workspace;

namespace ioSender.Views;

public partial class LayoutNameDialog : Window
{
    public LayoutNameDialog()
    {
        InitializeComponent();
        SaveButton.Click += (_, _) => TrySave();
        CancelButton.Click += (_, _) => Close(null);
        NameBox.KeyDown += OnNameBoxKeyDown;
        Opened += (_, _) => NameBox.Focus();
    }

    public string LayoutName => NameBox.Text?.Trim() ?? string.Empty;

    void OnNameBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            TrySave();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Close(null);
            e.Handled = true;
        }
    }

    void TrySave()
    {
        if (string.IsNullOrWhiteSpace(LayoutName))
        {
            ShowError("Enter a layout name.");
            return;
        }

        if (WorkspaceLayoutDefaults.IsBuiltIn(LayoutName))
        {
            ShowError("Classic, Touch, and XL are built-in layouts. Choose another name.");
            return;
        }

        Close(LayoutName);
    }

    void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = true;
    }
}
