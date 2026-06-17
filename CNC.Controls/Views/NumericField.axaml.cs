using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using CNC.Controls.Avalonia.Utilities;
using CNC.Core;

namespace CNC.Controls.Avalonia.Views;

public partial class NumericField : UserControl
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<NumericField, double>(nameof(Value), double.NaN);

    public static readonly StyledProperty<string> FormatProperty =
        AvaloniaProperty.Register<NumericField, string>(nameof(Format), NumericProperties.MetricFormat);

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<NumericField, string>(nameof(Label), "Label:");

    public static readonly StyledProperty<string> UnitProperty =
        AvaloniaProperty.Register<NumericField, string>(nameof(Unit), "mm");

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<NumericField, bool>(nameof(IsReadOnly));

    public static readonly StyledProperty<double> InputMinWidthProperty =
        AvaloniaProperty.Register<NumericField, double>(nameof(InputMinWidth), 64d);

    public NumericField()
    {
        InitializeComponent();
    }

    public double Value
    {
        get
        {
            var v = GetValue(ValueProperty);
            return double.IsNaN(v) ? 0d : v;
        }
        set => SetValue(ValueProperty, value);
    }

    public string Format { get => GetValue(FormatProperty); set => SetValue(FormatProperty, value); }
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    public double InputMinWidth { get => GetValue(InputMinWidthProperty); set => SetValue(InputMinWidthProperty, value); }

    public string Text => Value.ToInvariantString(data.Format);

    public void Clear() => data.Clear();
}
