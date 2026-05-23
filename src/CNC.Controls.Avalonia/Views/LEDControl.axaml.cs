using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace CNC.Controls.Avalonia.Views;

public partial class LEDControl : UserControl
{
    static readonly IBrush LedOn = Brushes.Red;
    IBrush? _ledOff;

    public static readonly StyledProperty<bool> IsSetProperty =
        AvaloniaProperty.Register<LEDControl, bool>(nameof(IsSet));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<LEDControl, string>(nameof(Label), string.Empty);

    public LEDControl()
    {
        InitializeComponent();
        _ledOff = btnLED.Background;
    }

    public new bool IsSet { get => GetValue(IsSetProperty); set => SetValue(IsSetProperty, value); }
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    static LEDControl()
    {
        IsSetProperty.Changed.AddClassHandler<LEDControl>((c, e) =>
            c.btnLED.Background = (bool)e.NewValue! ? LedOn : c._ledOff);
    }
}
