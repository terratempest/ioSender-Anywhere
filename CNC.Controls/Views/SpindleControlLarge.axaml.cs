using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CNC.Controls.Avalonia.Controls;
using CNC.Controls.Avalonia.Services;
using CNC.Controls.Avalonia.Utilities;
using CNC.Controls.Avalonia.ViewModels;
using CNC.Core;
using CNC.GCode;
using CNC.Localization.Avalonia;

namespace CNC.Controls.Avalonia.Views;

public partial class SpindleControlLarge : UserControl
{
    readonly SpindlePanelViewModel _viewModel;
    bool _useCoarseOverrideStep;
    bool _isEditingRpm;
    GrblViewModel? _subscribedModel;

    public static readonly StyledProperty<bool> IsSpindleStateEnabledProperty =
        AvaloniaProperty.Register<SpindleControlLarge, bool>(nameof(IsSpindleStateEnabled));

    public SpindleControlLarge() : this(null)
    {
    }

    public SpindleControlLarge(MachineCommandService? commands)
    {
        _viewModel = new SpindlePanelViewModel(commands);
        InitializeComponent();

        DataContextChanged += SpindleControlLarge_DataContextChanged;
        DetachedFromVisualTree += SpindleControlLarge_DetachedFromVisualTree;
        RpmReadoutBorder.PointerPressed += RpmReadoutBorder_PointerPressed;
        PopupKeyboardService.PopupClosed += PopupKeyboardService_PopupClosed;

        RbSpindleOff.CommandParameter = "M5";
        RbSpindleCW.CommandParameter = "M3{0}";
        RbSpindleCCW.CommandParameter = "M4{0}";

        Loaded += (_, _) =>
        {
            if (Design.IsDesignMode)
            {
                ApplyDesignPreviewValues();
                return;
            }

            ApplyLocalization();
            UpdateRpmText(force: true);
            UpdateOverrideText();
            UpdateSpindleStateEnabled();
            UpdateSpindleStateSelection();
        };

        if (Design.IsDesignMode)
            ApplyDesignPreviewValues();
    }

    public bool IsSpindleStateEnabled
    {
        get => GetValue(IsSpindleStateEnabledProperty);
        set => SetValue(IsSpindleStateEnabledProperty, value);
    }

    void ApplyLocalization()
    {
        Localize.Apply(LblSpindleSpeed);
        Localize.Apply(LblSpindlePower);
    }

    void ApplyDesignPreviewValues()
    {
        SetRpmText("12000");
        TxtRpmOverride.Text = "100%";
        UpdateStepText();
        SetSpindleStateSelection(SpindleState.CW);
    }

    void SpindleControlLarge_DataContextChanged(object? sender, EventArgs e)
    {
        PropertyChangedSubscription.Swap(
            ref _subscribedModel,
            DataContext as GrblViewModel,
            OnDataContextPropertyChanged);
        _viewModel.Model = _subscribedModel;

        UpdateRpmText(force: true);
        UpdateOverrideText();
        UpdateSpindleStateEnabled();
        UpdateSpindleStateSelection();
    }

    void SpindleControlLarge_DetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e) =>
        PopupKeyboardService.PopupClosed -= PopupKeyboardService_PopupClosed;

    void PopupKeyboardService_PopupClosed(object? sender, TextBox target)
    {
        if (ReferenceEquals(target, TxtRpm))
            EndRpmEdit(commit: true);
    }

    void OnDataContextPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GrblViewModel.GrblState) or nameof(GrblViewModel.IsJobRunning))
            UpdateSpindleStateEnabled();

        if (e.PropertyName == nameof(GrblViewModel.IsJobRunning) && !CanEditRpm)
            EndRpmEdit(commit: false);

        if (e.PropertyName is nameof(GrblViewModel.RPM)
            or nameof(GrblViewModel.SpindleSetpointRPM)
            or nameof(GrblViewModel.RPMOverride)
            or nameof(GrblViewModel.IsJobRunning)
            or nameof(GrblViewModel.SpindleState))
            UpdateRpmText(force: e.PropertyName == nameof(GrblViewModel.IsJobRunning));

        if (e.PropertyName == nameof(GrblViewModel.RPMOverride))
            UpdateOverrideText();

        if (e.PropertyName == nameof(GrblViewModel.SpindleState))
        {
            if (!CanEditRpm)
                EndRpmEdit(commit: false);
            UpdateSpindleStateSelection();
        }
    }

    void UpdateRpmText(bool force = false)
    {
        if (_viewModel.Model == null || (!force && _isEditingRpm))
            return;

        SetRpmText(_viewModel.Model.RPM.ToString("####0", System.Globalization.CultureInfo.InvariantCulture));
    }

    void UpdateOverrideText()
    {
        if (_viewModel.Model == null)
            return;

        TxtRpmOverride.Text = $"{_viewModel.Model.RPMOverride:0}%";
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

    bool CanEditRpm =>
        _viewModel.Model is { IsJobRunning: false }
        && _viewModel.Model.SpindleState.Value == SpindleState.Off;

    void SetRpmText(string text)
    {
        TxtRpmDisplay.Text = text;
        if (!_isEditingRpm)
            TxtRpm.Text = text;
    }

    void RpmReadoutBorder_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!CanEditRpm)
            return;

        BeginRpmEdit();
        e.Handled = true;
    }

    void BeginRpmEdit()
    {
        _isEditingRpm = true;
        TxtRpm.Text = TxtRpmDisplay.Text;
        TxtRpmDisplay.IsVisible = false;
        TxtRpm.IsVisible = true;
        Dispatcher.UIThread.Post(() =>
        {
            TxtRpm.Focus();
            TxtRpm.CaretIndex = TxtRpm.Text?.Length ?? 0;
        });
    }

    void EndRpmEdit(bool commit)
    {
        if (!_isEditingRpm)
            return;

        _isEditingRpm = false;
        TxtRpm.IsVisible = false;
        TxtRpmDisplay.IsVisible = true;

        if (commit)
            CommitRpm();
        else
            UpdateRpmText(force: true);
    }

    void CommitRpm()
    {
        if (!CanEditRpm)
        {
            UpdateRpmText(force: true);
            return;
        }

        if (_viewModel.TrySetRpm(TxtRpm.Text, out var normalizedRpmText))
            SetRpmText(normalizedRpmText);
        else
            UpdateRpmText(force: true);
    }

    void TxtRpm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        EndRpmEdit(commit: true);
        e.Handled = true;
    }

    void TxtRpm_LostFocus(object? sender, FocusChangedEventArgs e)
    {
        if (PopupKeyboardService.IsPopupOpenFor(TxtRpm))
            return;

        EndRpmEdit(commit: true);
    }

    void RbSpindle_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button || button.CommandParameter is not string commandTemplate)
            return;

        SetSpindleStateSelection(button == RbSpindleCW
            ? SpindleState.CW
            : button == RbSpindleCCW
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

    bool UseCoarseOverrideStep => _useCoarseOverrideStep;

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
        _useCoarseOverrideStep = !_useCoarseOverrideStep;
        UpdateStepText();
    }

    void SetSpindleStateSelection(SpindleState state)
    {
        RbSpindleCW.IsChecked = state == SpindleState.CW;
        RbSpindleCCW.IsChecked = state == SpindleState.CCW;
        RbSpindleOff.IsChecked = state == SpindleState.Off;
    }

    void UpdateStepText() => BtnStep.Content = _useCoarseOverrideStep ? "X10" : "X1";
}
