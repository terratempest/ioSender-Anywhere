using Avalonia;
using Avalonia.Controls;

namespace CNC.Controls.Avalonia.Views;

public partial class LimitsBaseControl : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<LimitsBaseControl, string>(nameof(Label));

    public static readonly StyledProperty<double> MinValueProperty =
        AvaloniaProperty.Register<LimitsBaseControl, double>(nameof(MinValue));

    public static readonly StyledProperty<double> MaxValueProperty =
        AvaloniaProperty.Register<LimitsBaseControl, double>(nameof(MaxValue));

    public LimitsBaseControl() => InitializeComponent();

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public double MinValue { get => GetValue(MinValueProperty); set => SetValue(MinValueProperty, value); }
    public double MaxValue { get => GetValue(MaxValueProperty); set => SetValue(MaxValueProperty, value); }
}
