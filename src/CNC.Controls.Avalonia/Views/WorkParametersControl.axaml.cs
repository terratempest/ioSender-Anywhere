using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Core;
using CNC.Localization.Avalonia;

namespace CNC.Controls.Avalonia.Views;

public partial class WorkParametersControl : UserControl
{
    public WorkParametersControl()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Localize.Apply(LblOffset);
            Localize.Apply(LblTool);
        };
    }

    public bool IsFocusedOnCombo => cbxTool.IsFocused || cbxOffset.IsFocused;

    private void cbxOffset_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 1 && sender is ComboBox { IsDropDownOpen: true } &&
            DataContext is GrblViewModel model && e.AddedItems[0] is CoordinateSystem cs)
            model.ExecuteCommand(cs.Code);
    }

    private void cbxTool_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 1 && sender is ComboBox { IsDropDownOpen: true } &&
            DataContext is GrblViewModel model && e.AddedItems[0] is Tool tool)
            model.ExecuteCommand(string.Format(GrblCommand.ToolChange, tool.Id == -1 ? 0 : tool.Id).ToString());
    }
}
