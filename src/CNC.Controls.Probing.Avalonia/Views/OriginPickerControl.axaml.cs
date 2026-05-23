using Avalonia;
using Avalonia.Controls;

namespace CNC.Controls.Probing;

public partial class OriginPickerControl : UserControl
{
    public static readonly StyledProperty<ProbeOrigin> ValueProperty =
        AvaloniaProperty.Register<OriginPickerControl, ProbeOrigin>(nameof(Value));

    public ProbeOrigin Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public OriginPickerControl() => InitializeComponent();
}
