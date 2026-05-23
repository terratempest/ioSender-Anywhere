using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Lathe;

public partial class CssControl : UserControl
{
    public static readonly StyledProperty<bool> IsCssEnabledProperty =
        AvaloniaProperty.Register<CssControl, bool>(nameof(IsCssEnabled));

    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<CssControl, double>(nameof(Speed));

    public static readonly StyledProperty<string> CssUnitProperty =
        AvaloniaProperty.Register<CssControl, string>(nameof(CssUnit), "m/min");

    public static readonly StyledProperty<SpindleState> SpindleDirectionProperty =
        AvaloniaProperty.Register<CssControl, SpindleState>(nameof(SpindleDirection), SpindleState.CW);

    public static readonly StyledProperty<ObservableCollection<SpindleDirection>> SpindleDirectionsProperty =
        AvaloniaProperty.Register<CssControl, ObservableCollection<SpindleDirection>>(nameof(SpindleDirections), Spindle.Directions);

    public CssControl()
    {
        InitializeComponent();
        IsCssEnabledProperty.Changed.AddClassHandler<CssControl>((c, _) => c.UpdateLabels());
        CssUnitProperty.Changed.AddClassHandler<CssControl>((c, _) => c.UpdateLabels());
    }

    public bool IsCssEnabled
    {
        get => GetValue(IsCssEnabledProperty);
        set => SetValue(IsCssEnabledProperty, value);
    }

    public double Speed
    {
        get => GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    public string CssUnit
    {
        get => GetValue(CssUnitProperty);
        set => SetValue(CssUnitProperty, value);
    }

    public SpindleState SpindleDirection
    {
        get => GetValue(SpindleDirectionProperty);
        set => SetValue(SpindleDirectionProperty, value);
    }

    public ObservableCollection<SpindleDirection> SpindleDirections
    {
        get => GetValue(SpindleDirectionsProperty);
        set => SetValue(SpindleDirectionsProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SpindleDirectionProperty && SpindleDirCombo != null)
        {
            foreach (var item in SpindleDirections)
            {
                if (item.Dir == SpindleDirection)
                {
                    SpindleDirCombo.SelectedItem = item;
                    break;
                }
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SpindleDirCombo.SelectionChanged += (_, _) =>
        {
            if (SpindleDirCombo.SelectedItem is SpindleDirection dir)
                SpindleDirection = dir.Dir;
        };
        UpdateLabels();
    }

    private void UpdateLabels()
    {
        if (SpeedLabel == null || SpeedUnit == null)
            return;

        if (IsCssEnabled)
        {
            SpeedLabel.Text = "Speed:";
            SpeedUnit.Text = CssUnit;
        }
        else
        {
            SpeedLabel.Text = "Spindle:";
            SpeedUnit.Text = "RPM";
        }
    }
}
