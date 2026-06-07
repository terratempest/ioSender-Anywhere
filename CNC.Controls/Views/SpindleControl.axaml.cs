using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CNC.Controls.Avalonia.Services;
using CNC.Controls.Avalonia.Utilities;
using CNC.Controls.Avalonia.ViewModels;
using CNC.Core;
using CNC.GCode;
using CNC.Localization.Avalonia;

namespace CNC.Controls.Avalonia.Views;

public partial class SpindleControl : UserControl
{
    readonly SpindlePanelViewModel _viewModel;
    GrblViewModel? _subscribedModel;

    public static readonly StyledProperty<bool> IsSpindleStateEnabledProperty =
        AvaloniaProperty.Register<SpindleControl, bool>(nameof(IsSpindleStateEnabled));

    public SpindleControl() : this(null)
    {
    }

    public SpindleControl(MachineCommandService? commands)
    {
        _viewModel = new SpindlePanelViewModel(commands);
        InitializeComponent();

        DataContextChanged += SpindleControl_DataContextChanged;

        rbSpindleOff.Tag = "M5";
        rbSpindleCW.Tag = "M3{0}";
        rbSpindleCCW.Tag = "M4{0}";

        overrideControl.ResetCommand = GrblConstants.CMD_SPINDLE_OVR_RESET;
        overrideControl.FineMinusCommand = GrblConstants.CMD_SPINDLE_OVR_FINE_MINUS;
        overrideControl.FinePlusCommand = GrblConstants.CMD_SPINDLE_OVR_FINE_PLUS;
        overrideControl.CoarseMinusCommand = GrblConstants.CMD_SPINDLE_OVR_COARSE_MINUS;
        overrideControl.CoarsePlusCommand = GrblConstants.CMD_SPINDLE_OVR_COARSE_PLUS;
        overrideControl.CommandGenerated += OverrideControl_CommandGenerated;

        Loaded += (_, _) =>
        {
            if (Design.IsDesignMode)
                return;

            ApplyLocalization();
        };
    }

    public bool IsSpindleStateEnabled
    {
        get => GetValue(IsSpindleStateEnabledProperty);
        set => SetValue(IsSpindleStateEnabledProperty, value);
    }

    public new bool IsFocused => cvRPM.IsFocused;

    void ApplyLocalization()
    {
        Localize.Apply(lblRPM);
        Localize.Apply(lblOverride);
        Localize.Apply(rbSpindleOff);
        Localize.Apply(rbSpindleCW);
        Localize.Apply(rbSpindleCCW);
    }

    void SpindleControl_DataContextChanged(object? sender, EventArgs e)
    {
        PropertyChangedSubscription.Swap(
            ref _subscribedModel,
            DataContext as GrblViewModel,
            OnDataContextPropertyChanged);
        _viewModel.Model = _subscribedModel;

        UpdateSpindleStateEnabled();
    }

    void OnDataContextPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GrblViewModel.GrblState) or nameof(GrblViewModel.IsJobRunning))
            UpdateSpindleStateEnabled();
    }

    void UpdateSpindleStateEnabled()
    {
        _viewModel.UpdateSpindleStateEnabled();
        IsSpindleStateEnabled = _viewModel.IsSpindleStateEnabled;
    }

    void cvRPM_KeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _viewModel.Model?.IsJobRunning == true)
            return;

        if (_viewModel.TrySetRpmFromValue(cvRPM.Value))
            e.Handled = true;
    }

    void rbSpindle_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: string commandTemplate })
            return;

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

    void OverrideControl_CommandGenerated(byte[] commands, int len) =>
        _viewModel.SendOverrideCommands(commands, len);
}
