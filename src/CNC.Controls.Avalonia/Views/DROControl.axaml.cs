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
        if (_subscribedModel is INotifyPropertyChanged oldNotify)
            oldNotify.PropertyChanged -= OnGrblPropertyChanged;
        if (_subscribedPosition is INotifyPropertyChanged oldPos)
            oldPos.PropertyChanged -= OnPositionPropertyChanged;
        if (_subscribedMachinePosition is INotifyPropertyChanged oldMachine)
            oldMachine.PropertyChanged -= OnMachinePositionPropertyChanged;

        _subscribedModel = DataContext as GrblViewModel;
        _subscribedPosition = _subscribedModel?.Position;
        _subscribedMachinePosition = _subscribedModel?.MachinePosition;
        _keyboardMappingsOk = false;

        if (_subscribedModel is INotifyPropertyChanged newNotify)
            newNotify.PropertyChanged += OnGrblPropertyChanged;
        if (_subscribedPosition is INotifyPropertyChanged newPos)
            newPos.PropertyChanged += OnPositionPropertyChanged;
        if (_subscribedMachinePosition is INotifyPropertyChanged newMachine)
            newMachine.PropertyChanged += OnMachinePositionPropertyChanged;

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
        UpdateAxisReadout(axisX, p.X, mp.X, format);
        UpdateAxisReadout(axisY, p.Y, mp.Y, format);
        UpdateAxisReadout(axisZ, p.Z, mp.Z, format);
        UpdateAxisReadout(axisA, p.A, mp.A, format);
        UpdateAxisReadout(axisB, p.B, mp.B, format);
        UpdateAxisReadout(axisC, p.C, mp.C, format);
        UpdateAxisReadout(axisU, p.U, mp.U, format);
        UpdateAxisReadout(axisV, p.V, mp.V, format);
        UpdateAxisReadout(axisW, p.W, mp.W, format);
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

            keyboard.AddHandler(CncKey.X, ModifierKeys.Control | ModifierKeys.Shift, ZeroX);
            keyboard.AddHandler(CncKey.Y, ModifierKeys.Control | ModifierKeys.Shift, ZeroY);
            keyboard.AddHandler(CncKey.Z, ModifierKeys.Control | ModifierKeys.Shift, ZeroZ);
            if (GrblInfo.AxisFlags.HasFlag(AxisFlags.A))
                keyboard.AddHandler(CncKey.A, ModifierKeys.Control | ModifierKeys.Shift, ZeroA);
            if (GrblInfo.AxisFlags.HasFlag(AxisFlags.B))
                keyboard.AddHandler(CncKey.B, ModifierKeys.Control | ModifierKeys.Shift, ZeroB);
            if (GrblInfo.AxisFlags.HasFlag(AxisFlags.C))
                keyboard.AddHandler(CncKey.C, ModifierKeys.Control | ModifierKeys.Shift, ZeroC);
            if (GrblInfo.AxisFlags.HasFlag(AxisFlags.U))
                keyboard.AddFunction(ZeroU, null);
            if (GrblInfo.AxisFlags.HasFlag(AxisFlags.V))
                keyboard.AddFunction(ZeroV, null);
            if (GrblInfo.AxisFlags.HasFlag(AxisFlags.W))
                keyboard.AddFunction(ZeroW, null);
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
    bool ZeroX(CNC.Core.Input.Key _) { AxisPositionChanged("X", 0d); return true; }
    bool ZeroY(CNC.Core.Input.Key _) { AxisPositionChanged("Y", 0d); return true; }
    bool ZeroZ(CNC.Core.Input.Key _) { AxisPositionChanged("Z", 0d); return true; }
    bool ZeroA(CNC.Core.Input.Key _) { AxisPositionChanged(GrblInfo.AxisIndexToLetter(3), 0d); return true; }
    bool ZeroB(CNC.Core.Input.Key _) { AxisPositionChanged(GrblInfo.AxisIndexToLetter(4), 0d); return true; }
    bool ZeroC(CNC.Core.Input.Key _) { AxisPositionChanged(GrblInfo.AxisIndexToLetter(5), 0d); return true; }
    bool ZeroU(CNC.Core.Input.Key _) { AxisPositionChanged(GrblInfo.AxisIndexToLetter(6), 0d); return true; }
    bool ZeroV(CNC.Core.Input.Key _) { AxisPositionChanged(GrblInfo.AxisIndexToLetter(7), 0d); return true; }
    bool ZeroW(CNC.Core.Input.Key _) { AxisPositionChanged(GrblInfo.AxisIndexToLetter(8), 0d); return true; }

    void AxisPositionChanged(string axis, double position)
    {
        if (DataContext is not GrblViewModel model)
            return;

        if (GrblParserState.IsMetric != model.IsMetric)
            position = GrblParserState.IsMetric ? position * MeasureViewModel.MM_PER_INCH : position / MeasureViewModel.MM_PER_INCH;

        if (axis == "ALL")
        {
            var s = "G90G10L20P0";
            foreach (var i in GrblInfo.AxisFlags.ToIndices())
                s += GrblInfo.AxisIndexToLetter(i) + "{0}";
            model.ExecuteCommand(string.Format(s, position.ToInvariantString(GrblParserState.IsMetric ? "F3" : "F4")));
        }
        else
            model.ExecuteCommand(string.Format("G10L20P0{0}{1}", axis, position.ToInvariantString(GrblParserState.IsMetric ? "F3" : "F4")));
    }
}
