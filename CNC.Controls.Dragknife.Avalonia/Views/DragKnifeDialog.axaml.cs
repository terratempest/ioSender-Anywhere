using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CNC.Controls.DragKnife;

public partial class DragKnifeDialog : Window
{
    public DragKnifeDialog()
    {
        InitializeComponent();
    }

    public DragKnifeDialog(DragKnifeViewModel model) : this()
    {
        DataContext = model;
    }

    void OnOkClick(object? sender, RoutedEventArgs e) => Close(true);

    void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    public static bool Show(DragKnifeViewModel model, Window? owner)
    {
        var dialog = new DragKnifeDialog(model);
        if (owner == null)
        {
            dialog.Show();
            return true;
        }

        return dialog.ShowDialog<bool>(owner).GetAwaiter().GetResult();
    }
}
