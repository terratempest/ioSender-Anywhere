using System.ComponentModel;
using System.Globalization;

using Avalonia;

using Avalonia.Controls;
using Avalonia.Controls.Primitives;

using Avalonia.Interactivity;

using CNC.Core;

using CNC.GCode;

using CNC.Localization.Avalonia;



namespace CNC.Controls.Avalonia.Views;



public partial class SpindleControl : UserControl

{

    GrblViewModel? _subscribedModel;



    public static readonly StyledProperty<bool> IsSpindleStateEnabledProperty =

        AvaloniaProperty.Register<SpindleControl, bool>(nameof(IsSpindleStateEnabled));



    public SpindleControl()

    {

        InitializeComponent();

        DataContextChanged += SpindleControl_DataContextChanged;

        rbSpindleOff.CommandParameter = "M5";

        rbSpindleCW.CommandParameter = "M3{0}";

        rbSpindleCCW.CommandParameter = "M4{0}";
        cbxSpindle.SelectionChanged += cbxSpindle_SelectionChanged;
        Loaded += (_, _) =>
        {
            ApplyLocalization();
            UpdateRpmText(force: true);
        };

    }



    void ApplyLocalization()

    {

        Localize.Apply(LblRpm);

        Localize.Apply(rbSpindleOff);

        Localize.Apply(rbSpindleCW);

        Localize.Apply(rbSpindleCCW);

        Localize.Apply(BtnSetRpm);

    }



    public bool IsSpindleStateEnabled

    {

        get => GetValue(IsSpindleStateEnabledProperty);

        set => SetValue(IsSpindleStateEnabledProperty, value);

    }



    public new bool IsFocused => txtRPM.IsFocused;



    void SpindleControl_DataContextChanged(object? sender, EventArgs e)

    {

        if (_subscribedModel is INotifyPropertyChanged oldPc)

            oldPc.PropertyChanged -= OnDataContextPropertyChanged;

        _subscribedModel = DataContext as GrblViewModel;

        if (_subscribedModel is INotifyPropertyChanged newPc)

            newPc.PropertyChanged += OnDataContextPropertyChanged;

        UpdateRpmText(force: true);

        UpdateSpindleStateEnabled();

    }



    void OnDataContextPropertyChanged(object? sender, PropertyChangedEventArgs e)

    {

        if (e.PropertyName is nameof(GrblViewModel.GrblState) or nameof(GrblViewModel.IsJobRunning))

            UpdateSpindleStateEnabled();

        if (e.PropertyName is nameof(GrblViewModel.SpindleSetpointRPM) or nameof(GrblViewModel.IsJobRunning))

            UpdateRpmText(force: e.PropertyName == nameof(GrblViewModel.IsJobRunning));

    }



    void UpdateRpmText(bool force = false)

    {

        if (DataContext is not GrblViewModel model)

            return;

        if (!force && txtRPM.IsFocused)

            return;

        txtRPM.Text = model.SpindleSetpointRPM.ToString("####0", CultureInfo.InvariantCulture);

    }



    void UpdateSpindleStateEnabled()

    {

        if (DataContext is GrblViewModel p)

            IsSpindleStateEnabled = !p.IsJobRunning || p.GrblState.State is GrblStates.Hold or GrblStates.Door;

    }



    void BtnSetRpm_Click(object? sender, RoutedEventArgs e)

    {

        if (DataContext is GrblViewModel model && TryGetEnteredRpm(out var rpm))

        {

            model.SpindleSetpointRPM = rpm;

            txtRPM.Text = rpm.ToString("####0", CultureInfo.InvariantCulture);

            model.ExecuteCommand("S" + rpm.ToInvariantString());

        }

    }



    bool TryGetEnteredRpm(out double rpm)

    {

        var text = txtRPM.Text?.Trim() ?? string.Empty;

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out rpm) ||

               double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out rpm);

    }



    void rbSpindle_Click(object? sender, RoutedEventArgs e)

    {

        if (DataContext is not GrblViewModel p || sender is not ToggleButton rb)

            return;



        if (p.GrblState.State == GrblStates.Hold)

            p.ExecuteCommand(((char)GrblConstants.CMD_SPINDLE_OVR_STOP).ToString());

        else

        {

            var rpm = p.ProgrammedRPM == 0d ? "S" + p.SpindleSetpointRPM.ToInvariantString() : "";

            p.ExecuteCommand(string.Format((string)rb.CommandParameter!, rpm));

        }

    }



    void cbxSpindle_SelectionChanged(object? sender, SelectionChangedEventArgs e)

    {

        if (DataContext is not GrblViewModel model || e.AddedItems.Count != 1 || sender is not ComboBox combo || !combo.IsDropDownOpen)

            return;



        if (model.GrblError != 0)

            model.ExecuteCommand("");



        if (GrblInfo.IsGrblHAL && GrblInfo.Build < 20240812)

            model.ExecuteCommand(string.Format(GrblCommand.SpindleChange, ((Spindle)e.AddedItems[0]!).SpindleId));

        else

            model.ExecuteCommand(string.Format(GrblCommand.SpindleChange, ((Spindle)e.AddedItems[0]!).SpindleNum));

    }



    bool UseCoarseOverrideStep => RbStepCoarse.IsChecked == true;



    void BtnOvrPlus_Click(object? sender, RoutedEventArgs e) =>

        SendOverride(UseCoarseOverrideStep ? GrblConstants.CMD_SPINDLE_OVR_COARSE_PLUS : GrblConstants.CMD_SPINDLE_OVR_FINE_PLUS);



    void BtnOvrMinus_Click(object? sender, RoutedEventArgs e) =>

        SendOverride(UseCoarseOverrideStep ? GrblConstants.CMD_SPINDLE_OVR_COARSE_MINUS : GrblConstants.CMD_SPINDLE_OVR_FINE_MINUS);



    void BtnOvrReset_Click(object? sender, RoutedEventArgs e) =>

        SendOverride(GrblConstants.CMD_SPINDLE_OVR_RESET);



    void OvrStep_Click(object? sender, RoutedEventArgs e)

    {

        if (sender is not ToggleButton selected)
            return;

        RbStepFine.IsChecked = selected == RbStepFine;
        RbStepCoarse.IsChecked = selected == RbStepCoarse;

    }



    static void SendOverride(byte command) => Comms.com?.WriteByte(command);

}


