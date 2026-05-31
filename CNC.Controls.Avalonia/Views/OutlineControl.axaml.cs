using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.App;
using CNC.Core;

namespace CNC.Controls.Avalonia.Views;

public partial class OutlineControl : UserControl
{
    public static readonly StyledProperty<int> FeedRateProperty =
        AvaloniaProperty.Register<OutlineControl, int>(nameof(FeedRate), 500);

    public OutlineControl() : this(null)
    {
    }

    public OutlineControl(AppConfigService? appConfig)
    {
        InitializeComponent();
        AppConfig = appConfig;
        AttachedToVisualTree += (_, _) =>
            FeedRate = AppConfig?.Base.OutlineFeedRate ?? 500;
    }

    public AppConfigService? AppConfig { get; set; }

    public int FeedRate { get => GetValue(FeedRateProperty); set => SetValue(FeedRateProperty, value); }

    private void button_Go(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not GrblViewModel model || !model.IsFileLoaded)
            return;

        var config = AppConfig;
        if (config != null && config.Base.OutlineFeedRate != FeedRate)
        {
            config.Base.OutlineFeedRate = FeedRate;
            config.Save();
        }

        if (!model.IsParserStateLive)
            GrblParserState.Get();

        var wasMetric = GrblParserState.IsMetric;
        var gcode = string.Format("G90G{0}G1F{1}\r", model.IsMetric ? 21 : 20, FeedRate);

        gcode += string.Format("X{0}Y{1}\r", model.ProgramLimits.MinX.ToInvariantString(), model.ProgramLimits.MinY.ToInvariantString(model.Format));
        gcode += string.Format("Y{0}\r", model.ProgramLimits.MaxY.ToInvariantString(model.Format));
        gcode += string.Format("X{0}\r", model.ProgramLimits.MaxX.ToInvariantString(model.Format));
        gcode += string.Format("Y{0}\r", model.ProgramLimits.MinY.ToInvariantString(model.Format));
        gcode += string.Format("X{0}\r", model.ProgramLimits.MinX.ToInvariantString(model.Format));
        if (model.IsMetric != wasMetric)
            gcode += string.Format("G{0}\r", wasMetric ? 21 : 20);

        model.ExecuteCommand(gcode);
    }
}
