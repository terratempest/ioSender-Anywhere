using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using CNC.Controls.Avalonia.Services;
using CNC.Controls.Avalonia.Utilities;
using CNC.Controls.Avalonia.ViewModels;
using CNC.Core;
using CNC.GCode;
using CNC.Localization.Avalonia;

namespace CNC.Controls.Avalonia.Views;

public partial class SpindleControlTouch : UserControl
{
    readonly SpindlePanelViewModel _viewModel;
    GrblViewModel? _subscribedModel;

    public static readonly StyledProperty<bool> IsSpindleStateEnabledProperty =
        AvaloniaProperty.Register<SpindleControlTouch, bool>(nameof(IsSpindleStateEnabled));

    public SpindleControlTouch() : this(null)
    {
    }

    public SpindleControlTouch(MachineCommandService? commands)
    {
        _viewModel = new SpindlePanelViewModel(commands);
        InitializeComponent();

        DataContextChanged += SpindleControlTouch_DataContextChanged;

        rbSpindleOff.CommandParameter = "M5";
        rbSpindleCW.CommandParameter = "M3{0}";
        rbSpindleCCW.CommandParameter = "M4{0}";
        cbxSpindle.SelectionChanged += cbxSpindle_SelectionChanged;

        Loaded += (_, _) =>
        {
            if (Design.IsDesignMode)
            {
                ApplyDesignPreviewValues();
                return;
            }

            ApplyLocalization();
            UpdateRpmText(force: true);
        };

        if (Design.IsDesignMode)
            ApplyDesignPreviewValues();
    }

    public bool IsSpindleStateEnabled
    {
        get => GetValue(IsSpindleStateEnabledProperty);
        set => SetValue(IsSpindleStateEnabledProperty, value);
    }

    public new bool IsFocused => txtRPM.IsFocused;

    void ApplyLocalization()
    {
        if (Design.IsDesignMode)
            return;

        Localize.Apply(LblRpm);
        Localize.Apply(rbSpindleOff);
        Localize.Apply(rbSpindleCW);
        Localize.Apply(rbSpindleCCW);
        Localize.Apply(BtnSetRpm);
    }

    void ApplyDesignPreviewValues()
    {
        txtRPM.Text = "12000";
        LblActualRpm.Text = "RPM:11500";
        LblRpmOverride.Text = "RPM % 85";
        SetSpindleStateSelection(SpindleState.CW);
    }

    void SpindleControlTouch_DataContextChanged(object? sender, EventArgs e)
    {
        PropertyChangedSubscription.Swap(
            ref _subscribedModel,
            DataContext as GrblViewModel,
            OnDataContextPropertyChanged);
        _viewModel.Model = _subscribedModel;

        UpdateRpmText(force: true);
        UpdateSpindleStateEnabled();
        UpdateSpindleStateSelection();
    }

    void OnDataContextPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GrblViewModel.GrblState) or nameof(GrblViewModel.IsJobRunning))
            UpdateSpindleStateEnabled();

        if (e.PropertyName is nameof(GrblViewModel.SpindleSetpointRPM) or nameof(GrblViewModel.IsJobRunning))
            UpdateRpmText(force: e.PropertyName == nameof(GrblViewModel.IsJobRunning));

        if (e.PropertyName == nameof(GrblViewModel.SpindleState))
            UpdateSpindleStateSelection();
    }

    void UpdateRpmText(bool force = false)
    {
        if (_viewModel.UpdateRpmText(force, txtRPM.IsFocused))
            txtRPM.Text = _viewModel.RpmText;
    }

    void UpdateSpindleStateEnabled()
    {
        _viewModel.UpdateSpindleStateEnabled();
        IsSpindleStateEnabled = _viewModel.IsSpindleStateEnabled;
    }

    void UpdateSpindleStateSelection()
    {
        if (_viewModel.Model == null)
            return;

        SetSpindleStateSelection(_viewModel.Model.SpindleState.Value);
    }

    void BtnSetRpm_Click(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.TrySetRpm(txtRPM.Text, out var normalizedRpmText))
            txtRPM.Text = normalizedRpmText;
    }

    void rbSpindle_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton rb || rb.CommandParameter is not string commandTemplate)
            return;

        SetSpindleStateSelection(rb == rbSpindleCW
            ? SpindleState.CW
            : rb == rbSpindleCCW
                ? SpindleState.CCW
                : SpindleState.Off);
        _viewModel.SelectSpindleState(commandTemplate);
    }

    void cbxSpindle_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count != 1
            || e.AddedItems[0] is not Spindle spindle
            || sender is not ComboBox combo
            || !combo.IsDropDownOpen)
            return;

        _viewModel.ChangeSpindle(spindle);
    }

    bool UseCoarseOverrideStep => RbStepCoarse.IsChecked == true;

    void BtnOvrPlus_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel.UseCoarseOverrideStep = UseCoarseOverrideStep;
        _viewModel.OverridePlus();
    }

    void BtnOvrMinus_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel.UseCoarseOverrideStep = UseCoarseOverrideStep;
        _viewModel.OverrideMinus();
    }

    void BtnOvrReset_Click(object? sender, RoutedEventArgs e) =>
        _viewModel.OverrideReset();

    void OvrStep_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton selected)
            return;

        SetOverrideStepSelection(selected == RbStepCoarse);
    }

    void SetSpindleStateSelection(SpindleState state)
    {
        rbSpindleCW.IsChecked = state == SpindleState.CW;
        rbSpindleCCW.IsChecked = state == SpindleState.CCW;
        rbSpindleOff.IsChecked = state == SpindleState.Off;
    }

    void SetOverrideStepSelection(bool coarse)
    {
        RbStepFine.IsChecked = !coarse;
        RbStepCoarse.IsChecked = coarse;
    }
}
