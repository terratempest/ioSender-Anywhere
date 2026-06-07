using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CNC.Controls.Avalonia.Controls;
using CNC.Localization.Avalonia;
using CNC.Controls.Avalonia.Utilities;
using CNC.Core;
using CNC.Core.Input;
using CNC.GCode;
using CncKey = CNC.Core.Input.Key;
using AvaloniaKey = Avalonia.Input.Key;

namespace CNC.Controls.Avalonia.Views;

public partial class DROControl : UserControl
{
    double _orgpos;
    bool _hasFocus;
    bool _keyboardMappingsOk;
    GrblViewModel? _subscribedModel;
    Position? _subscribedPosition;
    Position? _subscribedMachinePosition;
    readonly DROBaseControl[] _axes;
    readonly AxisBinding[] _axisBindings;
    int _lastVisibleAxisCount = -1;
    bool _refreshingAxisLayout;
    bool _axisLayoutRefreshScheduled;

    public event Action<bool>? DROEnabledChanged;

    public DROControl()
    {
        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch;
        VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch;
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += DRO_Loaded;
        Loaded += (_, _) => ApplyLocalization();
        btnHomeAll.Click += (_, _) =>
        {
            if (DataContext is GrblViewModel model)
                model.ExecuteCommand(GrblConstants.CMD_HOMING);
        };
        btnZeroAll.Click += (_, _) => AxisPositionChanged("ALL", 0d);

        _axes = [axisX, axisY, axisZ, axisA, axisB, axisC, axisU, axisV, axisW];
        _axisBindings =
        [
            new(axisX, 0, AxisFlags.X, CncKey.X, true),
            new(axisY, 1, AxisFlags.Y, CncKey.Y, true),
            new(axisZ, 2, AxisFlags.Z, CncKey.Z, true),
            new(axisA, 3, AxisFlags.A, CncKey.A, false),
            new(axisB, 4, AxisFlags.B, CncKey.B, false),
            new(axisC, 5, AxisFlags.C, CncKey.C, false),
            new(axisU, 6, AxisFlags.U, null, false),
            new(axisV, 7, AxisFlags.V, null, false),
            new(axisW, 8, AxisFlags.W, null, false)
        ];
        foreach (var axis in _axes)
        {
            axis.TxtWorkReadout.LostFocus += txtReadout_LostFocus;
            axis.TxtWorkReadout.KeyDown += txtReadout_KeyDown;
            axis.TxtWorkDisplay.PointerPressed += (_, e) => BeginAxisEdit(axis, e);
            axis.ZeroClick += (_, _) => btnZero_Click(axis.BtnZero, new RoutedEventArgs());
        }
    }

    public bool IsFocusable { get; set; }
    public new bool IsFocused => _hasFocus;

    public void EnableFocus() => IsFocusable = true;

    void OnDataContextChanged(object? sender, EventArgs e)
    {
        var model = DataContext as GrblViewModel;
        PropertyChangedSubscription.Swap(ref _subscribedModel, model, OnGrblPropertyChanged);
        PropertyChangedSubscription.Swap(ref _subscribedPosition, model?.Position, OnPositionPropertyChanged);
        PropertyChangedSubscription.Swap(ref _subscribedMachinePosition, model?.MachinePosition, OnMachinePositionPropertyChanged);
        _keyboardMappingsOk = false;

        RefreshAxisTags();
        RefreshReadouts();
        _lastVisibleAxisCount = -1;
        ScheduleAxisLayoutRefresh();
    }

    void OnGrblPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GrblViewModel.AxisLetter)
            or nameof(GrblViewModel.AxisEnabledFlags)
            or nameof(GrblViewModel.NumAxes))
        {
            RefreshAxisTags();
            ScheduleAxisLayoutRefresh();
        }

        if (e.PropertyName is nameof(GrblViewModel.Position)
            or nameof(GrblViewModel.MachinePosition)
            or nameof(GrblViewModel.FormatSigned)
            || e.PropertyName?.StartsWith("Position.", StringComparison.Ordinal) == true
            || e.PropertyName?.StartsWith("MachinePosition.", StringComparison.Ordinal) == true)
            RefreshReadouts();
    }

    void OnPositionPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        RefreshReadouts();

    void OnMachinePositionPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        RefreshReadouts();

    void RefreshReadouts()
    {
        if (_subscribedModel is not { } model)
            return;

        var format = model.FormatSigned;
        var mp = model.MachinePosition;
        var p = model.Position;
        foreach (var binding in _axisBindings)
            UpdateAxisReadout(binding.Control, p.Values[binding.Index], mp.Values[binding.Index], format);
    }

    void UpdateAxisReadout(DROBaseControl axis, double work, double machine, string format)
    {
        if (_subscribedModel!.SuspendPositionNotifications && _hasFocus && axis.TxtWorkReadout.IsFocused)
            return;

        axis.SetReadouts(work, machine, format);
    }

    void RefreshAxisTags()
    {
        foreach (var axis in _axes)
        {
            var label = axis.Label;
            if (!string.IsNullOrEmpty(label) && label != "-")
            {
                axis.Tag = GrblInfo.AxisLetterToIndex(label);
                axis.BtnZero.Content = "0" + label;
            }
        }
    }

    void ScheduleAxisLayoutRefresh()
    {
        if (_refreshingAxisLayout || _axisLayoutRefreshScheduled)
            return;

        _axisLayoutRefreshScheduled = true;
        Dispatcher.UIThread.Post(() =>
        {
            _axisLayoutRefreshScheduled = false;
            RefreshAxisLayout();
        }, DispatcherPriority.Background);
    }

    void RefreshAxisLayout()
    {
        if (_refreshingAxisLayout)
            return;

        var visible = _axes.Where(a => a.IsVisible).ToArray();
        if (visible.Length == _lastVisibleAxisCount && AxisGrid.Children.Count == visible.Length)
            return;

        _refreshingAxisLayout = true;
        try
        {
            _lastVisibleAxisCount = visible.Length;
            AxisGrid.Children.Clear();
            AxisGrid.RowDefinitions.Clear();

            for (var i = 0; i < visible.Length; i++)
            {
                AxisGrid.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star))
                {
                    MinHeight = 36
                });
                var axis = visible[i];
                AxisGrid.Children.Add(axis);
                Grid.SetRow(axis, i);
            }
        }
        finally
        {
            _refreshingAxisLayout = false;
        }
    }

    void ApplyLocalization()
    {
        if (Design.IsDesignMode)
            return;

        Localize.Apply(btnHomeAll);
        Localize.Apply(btnZeroAll);
    }

    void DRO_Loaded(object? sender, RoutedEventArgs e)
    {
        RefreshAxisTags();
        if (Design.IsDesignMode)
            return;

        ScheduleAxisLayoutRefresh();

        if (!_keyboardMappingsOk && DataContext is GrblViewModel model)
        {
            var keyboard = model.Keyboard;
            _keyboardMappingsOk = true;

            foreach (var binding in _axisBindings)
            {
                if (!binding.AlwaysRegisterShortcut && !GrblInfo.AxisFlags.HasFlag(binding.Flag))
                    continue;

                if (binding.ShortcutKey is { } key)
                    keyboard.AddHandler(key, ModifierKeys.Control | ModifierKeys.Shift, _ => ZeroAxis(binding.Index));
                else
                    keyboard.AddFunction(_ => ZeroAxis(binding.Index), null);
            }
            keyboard.AddHandler(CncKey.D0, ModifierKeys.Control | ModifierKeys.Shift, ZeroAxes);
        }
    }

    void BeginAxisEdit(DROBaseControl axis, PointerPressedEventArgs? e = null)
    {
        if (!IsFocusable || DataContext is not GrblViewModel model || model.IsJobRunning)
        {
            if (e is not null) e.Handled = true;
            return;
        }

        model.SuspendPositionNotifications = true;
        if (axis.Tag is int index)
            _orgpos = model.Position.Values[index];

        axis.BeginWorkEdit();
        _hasFocus = true;
        DROEnabledChanged?.Invoke(true);
        if (e is not null) e.Handled = true;
    }

    void txtReadout_LostFocus(object? sender, EventArgs e)
    {
        if (sender is not NumericTextBox box)
            return;

        var axis = FindAxisForReadout(box);
        var restore = false;
        if (DataContext is GrblViewModel model)
        {
            model.SuspendPositionNotifications = false;
            if (_hasFocus && box.Tag is int index)
            {
                restore = true;
                model.Position.Values[index] = _orgpos;
            }
        }

        axis?.EndWorkEdit(restore);
        _hasFocus = false;
        DROEnabledChanged?.Invoke(false);
    }

    DROBaseControl? FindAxisForReadout(NumericTextBox box) =>
        UIUtils.FindLogicalChildren<DROBaseControl>(this).FirstOrDefault(a => a.TxtWorkReadout == box);

    void txtReadout_KeyDown(object? sender, KeyEventArgs e)
    {
        if (!_hasFocus)
        {
            e.Handled = true;
            return;
        }

        if (e.Key == AvaloniaKey.Enter && sender is NumericTextBox box && box.Tag is int idx)
        {
            box.CommitText();
            if (box.Value != _orgpos)
                AxisPositionChanged(GrblInfo.AxisIndexToLetter(idx), box.Value);
            FindAxisForReadout(box)?.EndWorkEdit(false);
            DROEnabledChanged?.Invoke(false);
        }
    }

    void btnZero_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int index)
            AxisPositionChanged(GrblInfo.AxisIndexToLetter(index), 0d);
    }

    bool ZeroAxes(CNC.Core.Input.Key _) { AxisPositionChanged("ALL", 0d); return true; }

    bool ZeroAxis(int index)
    {
        AxisPositionChanged(GrblInfo.AxisIndexToLetter(index), 0d);
        return true;
    }

    void AxisPositionChanged(string axis, double position)
    {
        if (DataContext is not GrblViewModel model)
            return;

        if (GrblParserState.IsMetric != model.IsMetric)
            position = GrblParserState.IsMetric ? position * MeasureViewModel.MM_PER_INCH : position / MeasureViewModel.MM_PER_INCH;

        if (axis == "ALL")
        {
            model.ExecuteCommand(FormatAxisPositionCommand(axis, position, GrblParserState.IsMetric, GrblInfo.AxisFlags));
        }
        else
        {
            model.ExecuteCommand(FormatAxisPositionCommand(axis, position, GrblParserState.IsMetric, GrblInfo.AxisFlags));
        }
    }

    internal static string FormatAxisPositionCommand(string axis, double position, bool isMetric, AxisFlags axisFlags)
    {
        var value = position.ToInvariantString(isMetric ? "F3" : "F4");
        if (axis != "ALL")
            return string.Format("G10L20P0{0}{1}", axis, value);

        var command = "G90G10L20P0";
        foreach (var i in axisFlags.ToIndices())
            command += GrblInfo.AxisIndexToLetter(i) + "{0}";

        return string.Format(command, value);
    }

    sealed record AxisBinding(
        DROBaseControl Control,
        int Index,
        AxisFlags Flag,
        CncKey? ShortcutKey,
        bool AlwaysRegisterShortcut);
}
