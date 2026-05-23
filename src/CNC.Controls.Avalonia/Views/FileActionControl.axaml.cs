using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Controls.Avalonia.Services;
using CNC.Core;
using CNC.Localization.Avalonia;

namespace CNC.Controls.Avalonia.Views;

public partial class FileActionControl : UserControl
{
    public FileActionControl()
    {
        InitializeComponent();
        ApplyLocalization();
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
        if (!string.IsNullOrEmpty(path))
            GCodeFileService.Instance.Load(path);
    }

    private void OnReloadClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GrblViewModel vm && !string.IsNullOrEmpty(vm.FileName))
            GCodeFileService.Instance.Load(vm.FileName);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => GCodeFileService.Instance.Close();

    private async void OnEditClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GrblViewModel vm && !string.IsNullOrEmpty(vm.FileName)
            && ControlsPlatformContext.ExternalEditor != null)
            await ControlsPlatformContext.ExternalEditor.OpenFileAsync(vm.FileName);
    }
}
