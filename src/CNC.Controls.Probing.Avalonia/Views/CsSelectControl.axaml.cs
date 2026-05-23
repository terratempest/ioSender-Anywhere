using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Core;

namespace CNC.Controls.Probing;

public partial class CsSelectControl : UserControl
{
    public CsSelectControl() => InitializeComponent();

    void OnClearG92Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ProbingViewModel model)
            return;

        model.Grbl!.ExecuteCommand("G92.1");
        if (!model.Grbl.IsParserStateLive)
            model.Grbl.ExecuteCommand("$G");
    }
}
