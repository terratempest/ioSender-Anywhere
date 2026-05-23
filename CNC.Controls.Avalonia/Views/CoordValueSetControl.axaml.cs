using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CNC.Controls.Avalonia.Views;

public partial class CoordValueSetControl : UserControl
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<CoordValueSetControl, double>(nameof(Value), double.NaN);

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<CoordValueSetControl, string>(nameof(Label), string.Empty);

    public CoordValueSetControl()
    {
        InitializeComponent();
    }

    public event EventHandler<RoutedEventArgs>? Click;

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public string Text => cvValue.Text;

    private void btnSet_Click(object? sender, RoutedEventArgs e) => Click?.Invoke(this, e);
}
