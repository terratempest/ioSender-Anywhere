using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Core;
using CNC.Localization.Avalonia;

namespace CNC.Controls.Avalonia.Views;

public partial class ConsoleControl : UserControl
{
    public ConsoleControl()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Localize.Apply(ChkVerbose);
            Localize.Apply(ChkFilterRt);
            Localize.Apply(ChkShowAllRt);
            Localize.Apply(BtnClear);
        };
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GrblViewModel vm)
            vm.ResponseLog.Clear();
    }
}
