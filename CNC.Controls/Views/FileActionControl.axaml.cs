using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Controls.Avalonia.Services;
using CNC.Controls.Avalonia.ViewModels;
using CNC.Core;
using CNC.Localization.Avalonia;

namespace CNC.Controls.Avalonia.Views;

public partial class FileActionControl : UserControl
{
    readonly FileActionViewModel _viewModel;

    public FileActionControl() : this(null, null)
    {
    }

    public FileActionControl(ProgramService? program = null, CNC.Platform.Abstractions.IExternalEditor? externalEditor = null)
    {
        _viewModel = new(program, externalEditor);
        InitializeComponent();
        ApplyLocalization();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _viewModel.Model = DataContext as GrblViewModel;
    }

    private void ApplyLocalization()
    {
        Localize.Apply(BtnOpen);
        Localize.Apply(BtnReload);
        Localize.Apply(BtnEdit);
        Localize.Apply(BtnClose);
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is not { } storage)
            return;

        var path = await GCodeFilePicker.PickOpenPathAsync(storage);
        if (path != null)
            _viewModel.Open(path);
    }

    private void OnReloadClick(object? sender, RoutedEventArgs e) => _viewModel.Reload();

    private void OnCloseClick(object? sender, RoutedEventArgs e) => _viewModel.Close();

    private async void OnEditClick(object? sender, RoutedEventArgs e) => await _viewModel.EditAsync();
}
