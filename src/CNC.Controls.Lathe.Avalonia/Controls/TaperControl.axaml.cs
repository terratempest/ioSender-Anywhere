using Avalonia;
using Avalonia.Controls;
using CNC.Controls.Avalonia.Controls;

namespace CNC.Controls.Lathe;

public partial class TaperControl : UserControl
{
    public Action<double>? OnValueChanged;
    public Action<bool>? OnTaperEnabledChanged;

    public static readonly StyledProperty<bool> IsTaperEnabledProperty =
        AvaloniaProperty.Register<TaperControl, bool>(nameof(IsTaperEnabled));

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<TaperControl, double>(nameof(Value));

    public TaperControl()
    {
        InitializeComponent();
    }

    public bool IsTaperEnabled
    {
        get => GetValue(IsTaperEnabledProperty);
        set => SetValue(IsTaperEnabledProperty, value);
    }

    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        TaperBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == NumericTextBox.ValueProperty)
                OnValueChanged?.Invoke(double.IsNaN(Value) ? 0d : Value);
        };
        ChkTaper.IsCheckedChanged += (_, _) =>
            OnTaperEnabledChanged?.Invoke(ChkTaper.IsChecked == true);
    }
}
