using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;


namespace CNC.Controls.Avalonia.Views;

public partial class CoordValueSetControl2 : UserControl
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<CoordValueSetControl2, double>(nameof(Value), double.NaN);

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<CoordValueSetControl2, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<CoordValueSetControl2, string>(
            nameof(Unit),
            "mm");

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<CoordValueSetControl2, bool>(nameof(IsReadOnly));

    public CoordValueSetControl2()
    {
        InitializeComponent();

        if (Design.IsDesignMode)
        {
            Label = "X axis:";
            Value = 1323.456;
            Unit = "mm";
        }
    }

    public event EventHandler<RoutedEventArgs>? Click;

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Unit
    {
        get => GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public string Text => cvValue.Text ?? string.Empty;

    void btnSet_Click(object? sender, RoutedEventArgs e)
    {
        Click?.Invoke(this, e);
    }
}