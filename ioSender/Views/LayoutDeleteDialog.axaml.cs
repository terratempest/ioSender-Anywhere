using Avalonia.Controls;
using Avalonia.Input;

namespace ioSender.Views;

public partial class LayoutDeleteDialog : Window
{
    public LayoutDeleteDialog()
        : this("selected")
    {
    }

    public LayoutDeleteDialog(string layoutName)
    {
        InitializeComponent();
        MessageText.Text = $"Delete the \"{layoutName}\" layout?";
        DeleteButton.Click += (_, _) => Close(true);
        CancelButton.Click += (_, _) => Close(false);
        KeyDown += OnKeyDown;
    }

    void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close(false);
            e.Handled = true;
        }
    }
}
