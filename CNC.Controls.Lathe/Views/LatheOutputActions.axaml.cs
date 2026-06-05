using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Core;

namespace CNC.Controls.Lathe;

public partial class LatheOutputActions : UserControl
{
    public static readonly StyledProperty<BaseViewModel?> WizardModelProperty =
        AvaloniaProperty.Register<LatheOutputActions, BaseViewModel?>(nameof(WizardModel));

    public static readonly StyledProperty<string> SourceLabelProperty =
        AvaloniaProperty.Register<LatheOutputActions, string>(nameof(SourceLabel), "lathe.nc");

    public static readonly StyledProperty<System.Action?> CalculateActionProperty =
        AvaloniaProperty.Register<LatheOutputActions, System.Action?>(nameof(CalculateAction));

    public LatheOutputActions()
    {
        InitializeComponent();
    }

    public BaseViewModel? WizardModel
    {
        get => GetValue(WizardModelProperty);
        set => SetValue(WizardModelProperty, value);
    }

    public string SourceLabel
    {
        get => GetValue(SourceLabelProperty);
        set => SetValue(SourceLabelProperty, value);
    }

    public System.Action? CalculateAction
    {
        get => GetValue(CalculateActionProperty);
        set => SetValue(CalculateActionProperty, value);
    }

    void OnCalculateClick(object? sender, RoutedEventArgs e) => CalculateAction?.Invoke();

    void OnLoadProgramClick(object? sender, RoutedEventArgs e)
    {
        if (WizardModel != null)
            LatheGCodeActions.LoadIntoProgram(WizardModel, SourceLabel);
    }

    async void OnSaveFileClick(object? sender, RoutedEventArgs e)
    {
        if (WizardModel != null)
            await LatheGCodeActions.SaveToFileAsync(this, WizardModel);
    }
}
