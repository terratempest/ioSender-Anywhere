using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CNC.Controls.Avalonia.Views;

public partial class ToggleControl : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ToggleControl, string>(nameof(Label), "Label");

    public static readonly StyledProperty<bool> IsCheckedProperty =
        AvaloniaProperty.Register<ToggleControl, bool>(nameof(IsChecked));

    public ToggleControl()
    {
        InitializeComponent();
    }

    public event EventHandler<RoutedEventArgs>? Click;

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool IsChecked { get => GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

    private void tsw_Click(object? sender, RoutedEventArgs e) => Click?.Invoke(this, e);
}
