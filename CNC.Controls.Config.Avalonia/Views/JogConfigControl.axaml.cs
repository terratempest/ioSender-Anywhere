using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Core;

namespace CNC.Controls.Config;

public partial class JogConfigControl : UserControl
{
    public static readonly StyledProperty<bool> ShowKeyboardWarningProperty =
        AvaloniaProperty.Register<JogConfigControl, bool>(nameof(ShowKeyboardWarning));

    public JogConfigControl() => InitializeComponent();

    public bool ShowKeyboardWarning
    {
        get => GetValue(ShowKeyboardWarningProperty);
        set => SetValue(ShowKeyboardWarningProperty, value);
    }

    void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (GrblSettings.GetString(grblHALSetting.JogStepSpeed) != null)
        {
            IsVisible = false;
            return;
        }

        ShowKeyboardWarning = !GrblInfo.IsGrblHAL;
    }
}
