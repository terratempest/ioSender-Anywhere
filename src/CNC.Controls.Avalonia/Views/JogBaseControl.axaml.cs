using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using CNC.Controls.Avalonia.Config;
using CNC.Controls.Avalonia.Controls;
using CNC.Controls.Avalonia.Services;
using CNC.Controls.Avalonia.Utilities;
using CNC.Controls.Avalonia.ViewModels;
using CNC.Core;
using CNC.Localization.Avalonia;
using CNC.Core.Input;
using Key = CNC.Core.Input.Key;

namespace CNC.Controls.Avalonia.Views;

public partial class JogBaseControl : UserControl
{
    public static readonly StyledProperty<string> QueueStatusTextProperty =
        AvaloniaProperty.Register<JogBaseControl, string>(nameof(QueueStatusText), string.Empty);

    public static readonly StyledProperty<bool> IsQueueStatusVisibleProperty =
        AvaloniaProperty.Register<JogBaseControl, bool>(nameof(IsQueueStatusVisible));

    const Key Xplus = Key.J, Xminus = Key.H, Yplus = Key.K, Yminus = Key.L, Zplus = Key.I, Zminus = Key.M, Aplus = Key.U, Aminus = Key.N;
    const double FeedColumnWidth = 72;
    const double PadGap = 4;
    const double MinCellSize = 24;

    string _mode = "G21";
    bool _softLimits;
    KeypressHandler? _keyboard;
    GrblViewModel? _subscribedModel;
    readonly UiJogCommandQueue _jogQueue = new();
    static bool _keyboardMappingsOk;
    static bool _jogDataHandlersHooked;
    static readonly JogViewModel SharedJogData = new();

    public event System.Action? QueueStatusChanged;

    static JogBaseControl()
    {
        SharedJogData.SetMetric(true);
    }

    public JogBaseControl()
    {
        InitializeComponent();
        JogData = SharedJogData;
        Focusable = true;
        Loaded += (_, _) => WireControls();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is GrblViewModel model)
                ApplyJogUnits(model);
        };
        Unloaded += (_, _) => DetachModelHandlers();
        _jogQueue.Changed += UpdateQueueStatus;
        UpdateQueueStatus();
    }

    void WireControls()
    {
        // ToggleButton derives from Button in Avalonia; exclude option chips and jog axes.
        foreach (var btn in this.GetVisualDescendants().OfType<Button>())
        {
            if (btn is JogButton or ToggleButton)
                continue;
            btn.Click -= Button_Click;
            btn.Click += Button_Click;
        }

        foreach (var btn in this.GetVisualDescendants().OfType<JogButton>())
        {
            btn.JogStart -= JogButton_JogStart;
            btn.JogEnd -= JogButton_JogEnd;
            btn.JogStart += JogButton_JogStart;
            btn.JogEnd += JogButton_JogEnd;
        }
    }

    public JogViewModel JogData { get; }

    public string QueueStatusText
    {
        get => GetValue(QueueStatusTextProperty);
        set => SetValue(QueueStatusTextProperty, value);
    }

    public bool IsQueueStatusVisible
    {
        get => GetValue(IsQueueStatusVisibleProperty);
        set => SetValue(IsQueueStatusVisibleProperty, value);
    }

    void UpdateQueueStatus()
    {
        var count = _jogQueue.PendingCount;
        QueueStatusText = count > 0 ? $"Queue: {count}" : string.Empty;
        IsQueueStatusVisible = count > 0;
        QueueStatusChanged?.Invoke();
    }

    void JogData_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(JogViewModel.Distance) or nameof(JogViewModel.StepSize)))
            return;
        if (DataContext is not GrblViewModel model)
            return;
        if (JogData.Distance < 0)
            return;
        if (JogDefaults.Mode == JogConfigMode.UI ||
            (JogDefaults.LinkStepJogToUI && JogData.StepSize != JogViewModel.JogStep.Step3))
            model.JogStep = JogData.Distance;
    }

    void Model_IsMetricChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GrblViewModel.IsMetric) && sender is GrblViewModel model)
            ApplyJogUnits(model);
    }

    void ApplyJogUnits(GrblViewModel model)
    {
        _mode = model.IsMetric ? "G21" : "G20";
        JogData.SetMetric(model.IsMetric);
    }

    static bool TryGetTagIndex(object? tag, out int index)
    {
        switch (tag)
        {
            case int i:
                index = i;
                return true;
            case string s when int.TryParse(s, out index):
                return true;
            default:
                index = 0;
                return false;
        }
    }

    void Model_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not GrblViewModel model)
            return;

        if (e.PropertyName == nameof(GrblViewModel.GrblState))
            _jogQueue.OnGrblStateChanged(model.GrblState);
    }

    void JogControl_Loaded(object? sender, RoutedEventArgs e)
    {
        ApplyLocalization();
        WireControls();
        ApplyPadLayout();

        if (DataContext is not GrblViewModel model)
            return;

        ApplyJogUnits(model);
        AttachModelHandlers(model);

        _softLimits = !(GrblInfo.IsGrblHAL && GrblSettings.GetInteger(grblHALSetting.SoftLimitJogging) == 1) &&
                      GrblSettings.GetInteger(GrblSetting.SoftLimitsEnable) == 1;
        _limitSwitchesClearance = GrblSettings.GetDouble(GrblSetting.HomingPulloff);

        if (!_jogDataHandlersHooked)
        {
            _jogDataHandlersHooked = true;
            JogData.PropertyChanged += JogData_PropertyChanged;
        }

        _keyboard = model.Keyboard;

        if (_keyboardMappingsOk)
            return;

        _keyboardMappingsOk = true;

        if (JogDefaults.Mode == JogConfigMode.UI)
        {
            _keyboard.AddHandler(Key.PageUp, ModifierKeys.None, CursorJogZplus, false);
            _keyboard.AddHandler(Key.PageUp, ModifierKeys.None, KeyJogCancel, true);
            _keyboard.AddHandler(Key.PageDown, ModifierKeys.None, CursorJogZminus, false);
            _keyboard.AddHandler(Key.PageDown, ModifierKeys.None, KeyJogCancel, true);
            _keyboard.AddHandler(Key.Left, ModifierKeys.None, CursorJogXminus, false);
            _keyboard.AddHandler(Key.Left, ModifierKeys.None, KeyJogCancel, true);
            _keyboard.AddHandler(Key.Up, ModifierKeys.None, CursorJogYplus, false);
            _keyboard.AddHandler(Key.Up, ModifierKeys.None, KeyJogCancel, true);
            _keyboard.AddHandler(Key.Right, ModifierKeys.None, CursorJogXplus, false);
            _keyboard.AddHandler(Key.Right, ModifierKeys.None, KeyJogCancel, true);
            _keyboard.AddHandler(Key.Down, ModifierKeys.None, CursorJogYminus, false);
            _keyboard.AddHandler(Key.Down, ModifierKeys.None, KeyJogCancel, true);
        }

        _keyboard.AddHandler(Xplus, ModifierKeys.Control | ModifierKeys.Shift, KeyJogXplus, false);
        _keyboard.AddHandler(Xplus, ModifierKeys.Control | ModifierKeys.Shift, KeyJogCancel, true);
        _keyboard.AddHandler(Xminus, ModifierKeys.Control | ModifierKeys.Shift, KeyJogXminus, false);
        _keyboard.AddHandler(Xminus, ModifierKeys.Control | ModifierKeys.Shift, KeyJogCancel, true);
        _keyboard.AddHandler(Yplus, ModifierKeys.Control | ModifierKeys.Shift, KeyJogYplus, false);
        _keyboard.AddHandler(Yplus, ModifierKeys.Control | ModifierKeys.Shift, KeyJogCancel, true);
        _keyboard.AddHandler(Yminus, ModifierKeys.Control | ModifierKeys.Shift, KeyJogYminus, false);
        _keyboard.AddHandler(Yminus, ModifierKeys.Control | ModifierKeys.Shift, KeyJogCancel, true);
        _keyboard.AddHandler(Zplus, ModifierKeys.Control | ModifierKeys.Shift, KeyJogZplus, false);
        _keyboard.AddHandler(Zplus, ModifierKeys.Control | ModifierKeys.Shift, KeyJogCancel, true);
        _keyboard.AddHandler(Zminus, ModifierKeys.Control | ModifierKeys.Shift, KeyJogZminus, false);
        _keyboard.AddHandler(Zminus, ModifierKeys.Control | ModifierKeys.Shift, KeyJogCancel, true);

        if (GrblInfo.AxisLetterToIndex('A') >= 0)
        {
            _keyboard.AddHandler(Aplus, ModifierKeys.Control | ModifierKeys.Shift, KeyJogAplus, false);
            _keyboard.AddHandler(Aplus, ModifierKeys.Control | ModifierKeys.Shift, KeyJogCancel, true);
            _keyboard.AddHandler(Aminus, ModifierKeys.Control | ModifierKeys.Shift, KeyJogAminus, false);
            _keyboard.AddHandler(Aminus, ModifierKeys.Control | ModifierKeys.Shift, KeyJogCancel, true);
        }
        if (GrblInfo.AxisLetterToIndex('B') >= 0) { _keyboard.AddFunction(KeyJogBplus, null); _keyboard.AddFunction(KeyJogBminus, null); }
        if (GrblInfo.AxisLetterToIndex('C') >= 0) { _keyboard.AddFunction(KeyJogCplus, null); _keyboard.AddFunction(KeyJogCminus, null); }
        if (GrblInfo.AxisLetterToIndex('U') >= 0) { _keyboard.AddFunction(KeyJogUplus, null); _keyboard.AddFunction(KeyJogUminus, null); }
        if (GrblInfo.AxisLetterToIndex('V') >= 0) { _keyboard.AddFunction(KeyJogVplus, null); _keyboard.AddFunction(KeyJogVminus, null); }
        if (GrblInfo.AxisLetterToIndex('W') >= 0) { _keyboard.AddFunction(KeyJogWplus, null); _keyboard.AddFunction(KeyJogWminus, null); }

        if (JogDefaults.Mode != JogConfigMode.Keypad)
        {
            _keyboard.AddHandler(Key.End, ModifierKeys.None, EndJog, false);
            _keyboard.AddHandler(Key.NumPad0, ModifierKeys.Control, JogStep0);
            _keyboard.AddHandler(Key.NumPad1, ModifierKeys.Control, JogStep1);
            _keyboard.AddHandler(Key.NumPad2, ModifierKeys.Control, JogStep2);
            _keyboard.AddHandler(Key.NumPad3, ModifierKeys.Control, JogStep3);
            _keyboard.AddHandler(Key.NumPad4, ModifierKeys.Control, JogFeed0);
            _keyboard.AddHandler(Key.NumPad5, ModifierKeys.Control, JogFeed1);
            _keyboard.AddHandler(Key.NumPad6, ModifierKeys.Control, JogFeed2);
            _keyboard.AddHandler(Key.NumPad7, ModifierKeys.Control, JogFeed3);
            _keyboard.AddHandler(Key.NumPad2, ModifierKeys.None, FeedDec);
            _keyboard.AddHandler(Key.NumPad4, ModifierKeys.None, StepDec);
            _keyboard.AddHandler(Key.NumPad6, ModifierKeys.None, StepInc);
            _keyboard.AddHandler(Key.NumPad8, ModifierKeys.None, FeedInc);
        }
    }

    void AttachModelHandlers(GrblViewModel model)
    {
        if (ReferenceEquals(_subscribedModel, model))
            return;

        DetachModelHandlers();
        _subscribedModel = model;
        model.PropertyChanged += Model_IsMetricChanged;
        model.PropertyChanged += Model_PropertyChanged;
    }

    void DetachModelHandlers()
    {
        if (_subscribedModel == null)
            return;

        _subscribedModel.PropertyChanged -= Model_IsMetricChanged;
        _subscribedModel.PropertyChanged -= Model_PropertyChanged;
        _subscribedModel = null;
        _jogQueue.Clear();
    }

    double _limitSwitchesClearance = .5d, _position;

    bool KeyJogCancel(Key _)
    {
        if (JogData.StepSize == JogViewModel.JogStep.Continuous)
        {
            if (Comms.com is { } comms)
            {
                while (comms.OutCount != 0) { }
                comms.WriteByte(GrblConstants.CMD_JOG_CANCEL);
            }
        }
        return true;
    }

    bool KeyJogXplus(Key _) { if (_keyboard!.CanJog2 && !_keyboard.IsRepeating) JogCommand(GrblInfo.LatheModeEnabled ? "Z+" : "X+"); return true; }
    bool KeyJogXminus(Key _) { if (_keyboard!.CanJog2 && !_keyboard.IsRepeating) JogCommand(GrblInfo.LatheModeEnabled ? "Z-" : "X-"); return true; }
    bool KeyJogYplus(Key _) { if (_keyboard!.CanJog2 && !_keyboard.IsRepeating) JogCommand(GrblInfo.LatheModeEnabled ? "X-" : "Y+"); return true; }
    bool KeyJogYminus(Key _) { if (_keyboard!.CanJog2 && !_keyboard.IsRepeating) JogCommand(GrblInfo.LatheModeEnabled ? "X+" : "Y-"); return true; }
    bool KeyJogZplus(Key _) { if (_keyboard!.CanJog2 && !_keyboard.IsRepeating && !GrblInfo.LatheModeEnabled) JogCommand("Z+"); return true; }
    bool KeyJogZminus(Key _) { if (_keyboard!.CanJog2 && !_keyboard.IsRepeating && !GrblInfo.LatheModeEnabled) JogCommand("Z-"); return true; }
    bool KeyJogAplus(Key _) { if (_keyboard!.CanJog2 && !_keyboard.IsRepeating) JogCommand("A+"); return true; }
    bool KeyJogAminus(Key _) { if (_keyboard!.CanJog2 && !_keyboard.IsRepeating) JogCommand("A-"); return true; }
    bool KeyJogBplus(Key _) { if (_keyboard!.CanJog2 && !_keyboard.IsRepeating) JogCommand("B+"); return true; }
    bool KeyJogBminus(Key _) { if (_keyboard!.CanJog2 && !_keyboard.IsRepeating) JogCommand("B-"); return true; }
    bool KeyJogCplus(Key _) { if (_keyboard!.CanJog2 && !_keyboard.IsRepeating) JogCommand("C+"); return true; }
    bool KeyJogCminus(Key _) { if (_keyboard!.CanJog2 && !_keyboard.IsRepeating) JogCommand("C-"); return true; }
    bool KeyJogUplus(Key _) { if (_keyboard!.CanJog2 && !_keyboard.IsRepeating) JogCommand("U+"); return true; }
    bool KeyJogUminus(Key _) { if (_keyboard!.CanJog2 && !_keyboard.IsRepeating) JogCommand("U-"); return true; }
    bool KeyJogVplus(Key _) { if (_keyboard!.CanJog2 && !_keyboard.IsRepeating) JogCommand("V+"); return true; }
    bool KeyJogVminus(Key _) { if (_keyboard!.CanJog2 && !_keyboard.IsRepeating) JogCommand("V-"); return true; }
    bool KeyJogWplus(Key _) { if (_keyboard!.CanJog2 && !_keyboard.IsRepeating) JogCommand("W+"); return true; }
    bool KeyJogWminus(Key _) { if (_keyboard!.CanJog2 && !_keyboard.IsRepeating) JogCommand("W-"); return true; }

    bool CursorJogXplus(Key _) { if (_keyboard!.CanJog && !_keyboard.IsRepeating) JogCommand(GrblInfo.LatheModeEnabled ? "Z+" : "X+"); return true; }
    bool CursorJogXminus(Key _) { if (_keyboard!.CanJog && !_keyboard.IsRepeating) JogCommand(GrblInfo.LatheModeEnabled ? "Z-" : "X-"); return true; }
    bool CursorJogYplus(Key _) { if (_keyboard!.CanJog && !_keyboard.IsRepeating) JogCommand(GrblInfo.LatheModeEnabled ? "X-" : "Y+"); return true; }
    bool CursorJogYminus(Key _) { if (_keyboard!.CanJog && !_keyboard.IsRepeating) JogCommand(GrblInfo.LatheModeEnabled ? "X+" : "Y-"); return true; }
    bool CursorJogZplus(Key _) { if (_keyboard!.CanJog && !_keyboard.IsRepeating && !GrblInfo.LatheModeEnabled) JogCommand("Z+"); return true; }
    bool CursorJogZminus(Key _) { if (_keyboard!.CanJog && !_keyboard.IsRepeating && !GrblInfo.LatheModeEnabled) JogCommand("Z-"); return true; }

    void ApplyLocalization()
    {
        Localize.Apply(LblDistance);
        Localize.Apply(LblFeedRate);
        Localize.Apply(RbContinuous);
    }

    void OnSizeChanged(object? sender, SizeChangedEventArgs e) => ApplyPadLayout();

    void ApplyPadLayout()
    {
        if (BodyGrid.Bounds.Width <= 0 || BodyGrid.Bounds.Height <= 0)
            return;

        var padW = Math.Max(0, BodyGrid.Bounds.Width - FeedColumnWidth - BodyGrid.Margin.Left - BodyGrid.Margin.Right - 6);
        var padH = Math.Max(0, BodyGrid.Bounds.Height - BodyGrid.Margin.Top - BodyGrid.Margin.Bottom);
        var coreCols = 3 + (ZColumn.IsVisible ? 1 : 0);
        var extraCols = 0;
        foreach (var child in ExtraAxesHost.Children)
            if (child is Control c && c.IsVisible)
                extraCols++;
        var totalCols = Math.Max(1, coreCols + extraCols);
        const int rows = 3;
        var gapTotalW = PadGap * Math.Max(0, totalCols - 1);
        var gapTotalH = PadGap * (rows - 1);

        var maxCellW = (padW - gapTotalW) / totalCols;
        var maxCellH = (padH - gapTotalH) / rows;
        var cell = Math.Min(maxCellW, maxCellH);
        if (!double.IsFinite(cell) || cell < 1)
            cell = 1;

        JogPadHost.MaxWidth = padW;
        JogPadHost.MaxHeight = padH;

        ApplySquarePad(MillPad, cell, PadGap);
        ApplySquarePad(LathePad, cell, PadGap);
        ZSpacerMill.Height = Math.Max(0, cell + PadGap);
        ZSpacerLathe.Height = Math.Max(0, cell + PadGap);

        foreach (var axisGrid in ExtraAxesHost.Children.OfType<Grid>())
        {
            if (!axisGrid.IsVisible)
                continue;
            foreach (var jb in axisGrid.Children.OfType<JogButton>())
                SetSquare(jb, cell);
            foreach (var spacer in axisGrid.Children.OfType<Border>().Where(b => Grid.GetRow(b) == 1))
                spacer.Height = Math.Max(0, cell + PadGap);
        }
    }

    static void ApplySquarePad(Grid pad, double cell, double gap)
    {
        if (!pad.IsVisible)
            return;

        foreach (var jb in pad.Children.OfType<JogButton>())
            SetSquare(jb, cell);

        foreach (var btn in pad.Children.OfType<Button>())
        {
            if (btn.Tag?.ToString() == "stop")
                SetSquare(btn, cell);
        }

        foreach (var nested in pad.Children.OfType<Grid>())
        {
            foreach (var jb in nested.Children.OfType<JogButton>())
                SetSquare(jb, cell);
        }

        pad.Margin = new Thickness(0);
        foreach (var child in pad.Children)
        {
            if (child is not Control c)
                continue;
            var col = Grid.GetColumn(c);
            var row = Grid.GetRow(c);
            var m = new Thickness(
                col > 0 ? gap / 2 : 0,
                row > 0 ? gap / 2 : 0,
                gap / 2,
                gap / 2);
            c.Margin = m;
        }
    }

    static void SetSquare(Control control, double size)
    {
        control.Width = size;
        control.Height = size;
        control.HorizontalAlignment = HorizontalAlignment.Center;
        control.VerticalAlignment = VerticalAlignment.Center;
    }

    void DistanceOption_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton rb && TryGetTagIndex(rb.Tag, out var index))
        {
            rb.IsChecked = true;
            var step = (JogViewModel.JogStep)index;
            if (JogData.StepSize != step)
                JogData.StepSize = step;
        }
    }

    void FeedOption_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton rb && TryGetTagIndex(rb.Tag, out var index))
        {
            rb.IsChecked = true;
            var feed = (JogViewModel.JogFeed)index;
            if (JogData.Feed != feed)
                JogData.Feed = feed;
        }
    }

    bool EndJog(Key key)
    {
        if (!_keyboard!.IsRepeating && _keyboard.IsJogging)
            JogCommand("stop");
        return _keyboard.IsJogging;
    }

    bool JogStep0(Key _) { JogData.StepSize = JogViewModel.JogStep.Step0; return true; }
    bool JogStep1(Key _) { JogData.StepSize = JogViewModel.JogStep.Step1; return true; }
    bool JogStep2(Key _) { JogData.StepSize = JogViewModel.JogStep.Step2; return true; }
    bool JogStep3(Key _) { JogData.StepSize = JogViewModel.JogStep.Step3; return true; }
    bool JogFeed0(Key _) { JogData.Feed = JogViewModel.JogFeed.Feed0; return true; }
    bool JogFeed1(Key _) { JogData.Feed = JogViewModel.JogFeed.Feed1; return true; }
    bool JogFeed2(Key _) { JogData.Feed = JogViewModel.JogFeed.Feed2; return true; }
    bool JogFeed3(Key _) { JogData.Feed = JogViewModel.JogFeed.Feed3; return true; }
    bool FeedDec(Key _) { JogData.FeedDec(); return true; }
    bool FeedInc(Key _) { JogData.FeedInc(); return true; }
    bool StepDec(Key _) { JogData.StepDec(); return true; }
    bool StepInc(Key _) { JogData.StepInc(); return true; }

    static bool CanJog(GrblStates state) => state is GrblStates.Idle or GrblStates.Jog;

    bool JogCommand(string cmd)
    {
        if (DataContext is not GrblViewModel model)
            return false;

        if (cmd == "stop")
        {
            _jogQueue.Stop();
            return true;
        }

        if (cmd.Length < 2)
            return false;

        var axis = GrblInfo.AxisLetterToIndex(cmd[0]);
        if (axis < 0)
            return false;

        if (!CanJog(model.GrblState.State))
            return false;

        var distance = (JogData.Distance == -1 ? GrblInfo.MaxTravel.Values[axis] : JogData.Distance) * (cmd[1] == '-' ? -1d : 1d);

        if (_softLimits)
        {
            _position = distance + model.MachinePosition.Values[axis];

            if (GrblInfo.ForceSetOrigin)
            {
                if (!GrblInfo.HomingDirection.HasFlag(GrblInfo.AxisIndexToFlag(axis)))
                {
                    if (_position > 0d) _position = 0d;
                    else if (_position < (-GrblInfo.MaxTravel.Values[axis] + _limitSwitchesClearance))
                        _position = -GrblInfo.MaxTravel.Values[axis] + _limitSwitchesClearance;
                }
                else
                {
                    if (_position < 0d) _position = 0d;
                    else if (_position > (GrblInfo.MaxTravel.Values[axis] - _limitSwitchesClearance))
                        _position = GrblInfo.MaxTravel.Values[axis] - _limitSwitchesClearance;
                }
            }
            else
            {
                if (_position > -_limitSwitchesClearance) _position = -_limitSwitchesClearance;
                else if (_position < -(GrblInfo.MaxTravel.Values[axis] - _limitSwitchesClearance))
                    _position = -(GrblInfo.MaxTravel.Values[axis] - _limitSwitchesClearance);
            }

            if (_position == 0d)
                return false;

            var mode = model.IsMetric ? "G21" : "G20";
            cmd = string.Format("$J=G53{0}{1}{2}F{3}", mode, cmd[..1], _position.ToInvariantString(), Math.Ceiling(JogData.FeedRate).ToInvariantString());
        }
        else
        {
            var mode = model.IsMetric ? "G21" : "G20";
            cmd = string.Format("$J=G91{0}{1}{2}F{3}", mode, cmd[..1], distance.ToInvariantString(), Math.Ceiling(JogData.FeedRate).ToInvariantString());
        }

        return JogData.Distance == -1
            ? _jogQueue.TryStartContinuous(cmd, model.GrblState)
            : _jogQueue.TryEnqueueStep(cmd, model.GrblState);
    }

    void JogButton_JogStart(object? sender, EventArgs e)
    {
        if (sender is not JogButton btn)
            return;
        if (!JogCommand(JogAxisCommand(btn)))
            ReleasePointerCapture(sender, e);
    }

    static void ReleasePointerCapture(object? sender, EventArgs e)
    {
        if (e is PointerEventArgs pe && sender is Control control && pe.Pointer.Captured == control)
            pe.Pointer.Capture(null);
    }

    static string JogAxisCommand(Control btn)
    {
        if ((string?)btn.Tag == "stop")
            return "stop";
        if (btn.Tag is string tag)
            return tag;
        if (btn is Button { Content: { } content })
            return content.ToString() ?? "";
        return "";
    }

    void JogButton_JogEnd(object? sender, EventArgs e)
    {
        if (JogData.Distance == -1)
            JogCommand("stop");
    }

    void Button_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && !JogCommand(JogAxisCommand(btn)))
            ReleasePointerCapture(sender, e);
    }
}
