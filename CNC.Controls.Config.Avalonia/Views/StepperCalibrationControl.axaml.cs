using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using CNC.Controls.Avalonia.Services;
using CNC.Core;
using CNC.GCode;

namespace CNC.Controls.Config;

public partial class StepperCalibrationControl : UserControl, IGrblConfigTab
{
    const double BacklashComp = 0.5d;

    bool _runExecuted;
    bool _wasMetric;
    bool _initialDirectionPositive;
    DistanceMode _distanceMode;
    int _lastAxis;
    GrblSettingDetails? _resolutionSetting;
    GrblViewModel? _model;

    public static readonly StyledProperty<int> AxisProperty =
        AvaloniaProperty.Register<StepperCalibrationControl, int>(nameof(Axis));

    public static readonly StyledProperty<bool> CanUpdateProperty =
        AvaloniaProperty.Register<StepperCalibrationControl, bool>(nameof(CanUpdate));

    public static readonly StyledProperty<double> DistanceProperty =
        AvaloniaProperty.Register<StepperCalibrationControl, double>(nameof(Distance), 100d);

    public static readonly StyledProperty<string> DistanceUnitProperty =
        AvaloniaProperty.Register<StepperCalibrationControl, string>(nameof(DistanceUnit), string.Empty);

    public static readonly StyledProperty<double> ActualDistanceProperty =
        AvaloniaProperty.Register<StepperCalibrationControl, double>(nameof(ActualDistance), 100d);

    public static readonly StyledProperty<double> ResolutionProperty =
        AvaloniaProperty.Register<StepperCalibrationControl, double>(nameof(Resolution));

    public static readonly StyledProperty<string> ResolutionUnitProperty =
        AvaloniaProperty.Register<StepperCalibrationControl, string>(nameof(ResolutionUnit), string.Empty);

    public StepperCalibrationControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => EnsureModel();
        AxisProperty.Changed.AddClassHandler<StepperCalibrationControl>((c, _) => c.UpdateAxisDetails());
        DistanceProperty.Changed.AddClassHandler<StepperCalibrationControl>((c, e) =>
        {
            c.CanUpdate = false;
            if (e.NewValue is double distance)
                c.ActualDistance = distance;
        });
        ActualDistanceProperty.Changed.AddClassHandler<StepperCalibrationControl>((c, e) =>
        {
            if (e.NewValue is double actual)
                c.OnActualDistanceChanged(actual);
        });
        cbxAxis.SelectionChanged += OnAxisSelectionChanged;
    }

    public GrblConfigType GrblConfigType => GrblConfigType.StepperCalibration;

    public int PollInterval { get; set; } = 200;

    public int Axis
    {
        get => GetValue(AxisProperty);
        set => SetValue(AxisProperty, value);
    }

    public bool CanUpdate
    {
        get => GetValue(CanUpdateProperty);
        set => SetValue(CanUpdateProperty, value);
    }

    public double Distance
    {
        get => GetValue(DistanceProperty);
        set => SetValue(DistanceProperty, value);
    }

    public string DistanceUnit
    {
        get => GetValue(DistanceUnitProperty);
        set => SetValue(DistanceUnitProperty, value);
    }

    public double ActualDistance
    {
        get => GetValue(ActualDistanceProperty);
        set => SetValue(ActualDistanceProperty, value);
    }

    public double Resolution
    {
        get => GetValue(ResolutionProperty);
        set => SetValue(ResolutionProperty, value);
    }

    public string ResolutionUnit
    {
        get => GetValue(ResolutionUnitProperty);
        set => SetValue(ResolutionUnitProperty, value);
    }

    public void Activate(bool activate)
    {
        if (!EnsureModel() || _model is not { } model)
            return;

        if (activate)
        {
            model.PropertyChanged += Model_PropertyChanged;
            Axis = _lastAxis == 0 ? 1 : 0;
            UpdateAxisDetails();
            SyncAxisCombo();

            if (Comms.com is { IsOpen: true })
            {
                if (GrblInfo.IsGrblHAL)
                {
                    GrblParserState.Get();
                    GrblWorkParameters.Get();
                }
                else
                    GrblParserState.Get(true);
            }

            _wasMetric = GrblParserState.IsMetric;
            _distanceMode = GrblParserState.DistanceMode;
            ActualDistance = Distance;
        }
        else
        {
            _lastAxis = Axis;
            model.PropertyChanged -= Model_PropertyChanged;

            if (!_wasMetric)
                model.ExecuteCommand("G20");

            model.ExecuteCommand(_distanceMode == DistanceMode.Absolute ? "G90" : "G91");
        }

        model.Poller.SetState(activate ? PollInterval : 0);
    }

    void OnLoaded(object? sender, RoutedEventArgs e)
    {
        EnsureModel();
        txtWarnings.Text = WarningsText;
        txtInstructions.Text = InstructionsText;
        SyncAxisCombo();
    }

    bool EnsureModel()
    {
        if (_model == null && DataContext is GrblViewModel vm)
            _model = vm;
        return _model != null;
    }

    void OnAxisSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (cbxAxis.SelectedItem is Axis axis)
            Axis = axis.Index;
    }

    void SyncAxisCombo()
    {
        if (_model == null)
            return;

        cbxAxis.ItemsSource = _model.Axes;
        cbxAxis.SelectedItem = FindAxis(Axis);
    }

    Axis? FindAxis(int index)
    {
        if (_model == null)
            return null;

        foreach (var axis in _model.Axes)
        {
            if (axis.Index == index)
                return axis;
        }

        return _model.Axes.Count > 0 ? _model.Axes[0] : null;
    }

    void UpdateAxisDetails()
    {
        if (_model == null || _model.Axes.Count == 0)
            return;

        var axisIndex = Axis;
        if (axisIndex < 0 || axisIndex >= _model.Axes.Count)
            axisIndex = 0;

        var letter = _model.Axes[axisIndex].Letter;
        var index = GrblInfo.AxisLetterToIndex(letter);

        CanUpdate = false;
        var travel = GrblSettings.Get(GrblSetting.MaxTravelBase + index);
        _resolutionSetting = GrblSettings.Get(GrblSetting.TravelResolutionBase + index);
        if (travel == null || _resolutionSetting?.Value is not { } resolution)
            return;

        DistanceUnit = travel.Unit ?? string.Empty;
        Resolution = double.Parse(resolution);
        ResolutionUnit = _resolutionSetting.Unit ?? string.Empty;
        cbxAxis.SelectedItem = FindAxis(axisIndex);
    }

    void OnActualDistanceChanged(double measured)
    {
        if (_resolutionSetting?.Value is not { } resolution)
            return;

        if (!CanUpdate)
            Resolution = double.Parse(resolution);
        else if (!double.IsInfinity(measured) && !double.IsNaN(measured))
            Resolution = Math.Round(double.Parse(resolution) / measured * Distance, GrblInfo.IsGrblHAL ? 6 : 3);
    }

    void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(GrblViewModel.GrblState) || _model == null)
            return;

        switch (_model.GrblState.State)
        {
            case GrblStates.Run:
                _runExecuted = true;
                break;

            case GrblStates.Idle:
                CanUpdate = _runExecuted && Distance != 0d;
                _runExecuted = false;
                break;

            default:
                _runExecuted = false;
                break;
        }
    }

    void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || _model is not { } model || _resolutionSetting == null)
            return;

        var tag = btn.Tag?.ToString();
        if (tag == "stop")
        {
            if (Comms.com is { } comms)
                comms.WriteByte(GrblInfo.IsGrblHAL ? GrblConstants.CMD_STOP : GrblConstants.CMD_RESET);
            return;
        }

        if (tag == "save")
        {
            _resolutionSetting.Value = Resolution.ToInvariantString();
            if (GrblSettings.Save())
                ActualDistance = Distance;
            return;
        }

        var distance = Distance;
        var directionPositive = tag == "+";

        if (!CanUpdate)
            _initialDirectionPositive = directionPositive;

        if (_initialDirectionPositive != directionPositive)
            distance += BacklashComp;

        model.ExecuteCommand(string.Format("G21G91G0{0}{1}", GrblInfo.AxisIndexToLetter(Axis),
            (directionPositive ? distance : -distance).ToInvariantString()));

        if (Distance != distance)
            model.ExecuteCommand(string.Format("G21G91G0{0}{1}", GrblInfo.AxisIndexToLetter(Axis),
                (directionPositive ? -BacklashComp : BacklashComp).ToInvariantString()));
    }

    static string WarningsText =>
        "Make sure there is room for the axis to move and avoid collision. Return move will add 0.5mm overshoot to remove any backlash from subsequent measurements.";

    static string InstructionsText =>
        "1. Select the axis to calibrate, Resolution is set from the underlying setting.\n" +
        "2. Jog to the starting positition in the same direction as the first \"Go\" move and mark it.\n" +
        "3. Enter the Distance to measure, Measured distance is linked to Distance so will be equal.\n" +
        "4. Press the appropriate \"Go\" button to move the entered distance, measure and enter the value in the Measured field.\n" +
        "5. Press the opposite \"Go\" button to move back to the origin.\n" +
        "This step can be skipped but avoids repositioning manually to the marked start position if multiple measurements are to be performed.\n" +
        "6. Press the \"Save\" button to update the setting.\n" +
        "7. Repeat from step 4 (or 3). until the measured distance matches the commanded distance with the required accuracy.";
}
