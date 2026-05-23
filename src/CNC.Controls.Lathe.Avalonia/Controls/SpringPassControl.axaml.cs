using Avalonia;
using Avalonia.Controls;

namespace CNC.Controls.Lathe;

public partial class SpringPassControl : UserControl
{
    public static readonly StyledProperty<bool> IsPassesEnabledProperty =
        AvaloniaProperty.Register<SpringPassControl, bool>(nameof(IsPassesEnabled));

    public static readonly StyledProperty<double> PassesProperty =
        AvaloniaProperty.Register<SpringPassControl, double>(nameof(Passes));

    public SpringPassControl()
    {
        InitializeComponent();
    }

    public bool IsPassesEnabled
    {
        get => GetValue(IsPassesEnabledProperty);
        set => SetValue(IsPassesEnabledProperty, value);
    }

    public double Passes
    {
        get => GetValue(PassesProperty);
        set => SetValue(PassesProperty, value);
    }
}
