using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Controls.Avalonia.Controls;
using CNC.Controls.Avalonia.ViewModels;
using CNC.GCode;

namespace CNC.Controls.Avalonia.Views;

public partial class MacroEditor : Window
{
    public MacroEditor()
    {
        InitializeComponent();
        PopupKeyboardService.Attach(this);
    }

    public MacroEditor(ObservableCollection<Macro> macros) : this()
    {
        DataContext = new MacroEditorViewModel(macros);
    }

    void OnAddClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MacroEditorViewModel vm)
            vm.AddMacro();
    }

    void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MacroEditorViewModel vm)
            vm.DeleteSelected();
    }

    void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MacroEditorViewModel vm)
            vm.Commit();
        Close(true);
    }

    void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    public static async Task<bool> ShowAsync(ObservableCollection<Macro> macros, Window? owner)
    {
        var dialog = new MacroEditor(macros);
        if (owner == null)
        {
            dialog.Show();
            return false;
        }

        return await dialog.ShowDialog<bool>(owner);
    }
}
