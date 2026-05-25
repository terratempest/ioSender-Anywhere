using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CNC.App;
using CNC.Controls.Avalonia.Services;
using CNC.Controls.Avalonia.Utilities;
using CNC.Core;
using CNC.Core.Input;
using CNC.Localization.Avalonia;
using CoreKey = CNC.Core.Input.Key;

namespace CNC.Controls.Avalonia.Views;

/// <summary>Job transport buttons wired to <see cref="JobStreamingService"/> file streaming.</summary>
public partial class JobControl : UserControl, IKeyHandlerContext
{
    static bool _keyboardMappingsOk;

    readonly MachineCommandService _commands;
    readonly JobStreamingService _streaming;
    readonly BaseConfig? _appBase;
    GrblViewModel? _model;
    KeypressHandler? _keyboard;

    string IKeyHandlerContext.Name => "Job";

    object? IKeyHandlerContext.DataContext => DataContext;

    public JobControl() : this(null, null)
    {
    }

    public JobControl(BaseConfig? appBase, MachineCommandService? commands = null)
    {
        _appBase = appBase;
        _commands = commands ?? new MachineCommandService();
        _streaming = new JobStreamingService(appBase);
        InitializeComponent();
        ApplyLocalization();
        Focusable = true;

        _streaming.ButtonStateChanged += ApplyButtonState;
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;

        AddHandler(InputElement.KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(InputElement.KeyUpEvent, OnPreviewKeyUp, RoutingStrategies.Tunnel);
    }

    void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        // After a workspace rebuild, Detach() was called but DataContext didn't change,
        // so OnDataContextChanged won't re-fire. Re-attach here if needed.
        if (_model == null && DataContext is GrblViewModel vm)
        {
            GCodeFileService.Instance.Model = vm;
            _model = vm;
            _streaming.Attach(vm);
        }
        _streaming.Activate(true);
    }

    void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _streaming.Activate(false);
        _streaming.Detach();
    }

    void ApplyLocalization()
    {
        Localize.Apply(BtnStart);
        Localize.Apply(BtnHold);
        Localize.Apply(BtnStop);
        Localize.Apply(BtnRewind);
    }

    void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is GrblViewModel vm)
        {
            GCodeFileService.Instance.Model = vm;
            _model = vm;
            _streaming.Attach(vm);
            RegisterKeyboardHandlers(vm);
        }
        else
        {
            _model = null;
            _streaming.Detach();
        }
    }

    void RegisterKeyboardHandlers(GrblViewModel model)
    {
        if (_keyboardMappingsOk)
            return;

        _keyboard = model.Keyboard;
        _keyboardMappingsOk = true;
        var ctx = (IKeyHandlerContext)this;

        _keyboard.AddHandler(CoreKey.R, ModifierKeys.Alt, StartJob, ctx);
        _keyboard.AddHandler(CoreKey.S, ModifierKeys.Alt, StopJob, ctx);
        _keyboard.AddHandler(CoreKey.H, ModifierKeys.Control, Home, ctx);
        _keyboard.AddHandler(CoreKey.U, ModifierKeys.Control, Unlock);
        _keyboard.AddHandler(CoreKey.R, ModifierKeys.Shift | ModifierKeys.Control, Reset);
        _keyboard.AddHandler((CoreKey)18, ModifierKeys.None, FeedHold, ctx);
        _keyboard.AddHandler((CoreKey)90, ModifierKeys.None, FnKeyHandler);
        _keyboard.AddHandler((CoreKey)91, ModifierKeys.None, FnKeyHandler);
        _keyboard.AddHandler((CoreKey)92, ModifierKeys.None, FnKeyHandler);
        _keyboard.AddHandler((CoreKey)93, ModifierKeys.None, FnKeyHandler);
        _keyboard.AddHandler((CoreKey)94, ModifierKeys.None, FnKeyHandler);
        _keyboard.AddHandler((CoreKey)95, ModifierKeys.None, FnKeyHandler);
        _keyboard.AddHandler((CoreKey)96, ModifierKeys.None, FnKeyHandler);
        _keyboard.AddHandler((CoreKey)97, ModifierKeys.None, FnKeyHandler);
        _keyboard.AddHandler((CoreKey)98, ModifierKeys.None, FnKeyHandler);
        _keyboard.AddHandler((CoreKey)99, ModifierKeys.None, FnKeyHandler);
        _keyboard.AddHandler((CoreKey)100, ModifierKeys.None, FnKeyHandler);
        _keyboard.AddHandler((CoreKey)101, ModifierKeys.None, FnKeyHandler);
        _keyboard.AddHandler((CoreKey)145, ModifierKeys.Control, FeedRateDown);
        _keyboard.AddHandler((CoreKey)146, ModifierKeys.Control, FeedRateUp);
        _keyboard.AddHandler((CoreKey)145, ModifierKeys.Shift | ModifierKeys.Control, FeedRateDownFine);
        _keyboard.AddHandler((CoreKey)146, ModifierKeys.Shift | ModifierKeys.Control, FeedRateUpFine);
    }

    void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (ProcessKeyPreview(e, isUp: false))
            e.Handled = true;
    }

    void OnPreviewKeyUp(object? sender, KeyEventArgs e)
    {
        if (ProcessKeyPreview(e, isUp: true))
            e.Handled = true;
    }

    bool ProcessKeyPreview(KeyEventArgs e, bool isUp)
    {
        if (_keyboard == null)
            return false;

        var info = AvaloniaKeyBridge.ToKeyEventInfo(e, isUp);
        return _keyboard.ProcessKeypress(info, allowJog: true, this);
    }

    void ApplyButtonState(JobButtonState state)
    {
        if (state.ControlEnabled is bool enabled)
            IsEnabled = enabled;

        if (state.CycleStart is bool cycleStart)
            BtnStart.IsEnabled = cycleStart;

        if (state.FeedHold is bool feedHold)
            BtnHold.IsEnabled = feedHold;

        if (state.Stop is bool stop)
            BtnStop.IsEnabled = stop;

        if (state.Rewind is bool rewind)
            BtnRewind.IsEnabled = rewind;

        if (state.CycleStartLabel != null)
            BtnStart.Content = state.CycleStartLabel;

        if (state.StopLabel != null)
            BtnStop.Content = state.StopLabel;
        else if (state.Stop == true)
            Localize.Apply(BtnStop);
    }

    void OnCycleStartClick(object? sender, RoutedEventArgs e) => _streaming.CycleStart(0);

    void OnFeedHoldClick(object? sender, RoutedEventArgs e) => _streaming.FeedHold();

    void OnStopClick(object? sender, RoutedEventArgs e) => _streaming.StopJob(true);

    void OnRewindClick(object? sender, RoutedEventArgs e) => _streaming.RewindAndRefresh();

    bool StartJob(CoreKey key)
    {
        _streaming.CycleStart(0);
        return true;
    }

    bool StopJob(CoreKey key)
    {
        _streaming.StopJob(false);
        return true;
    }

    bool FeedHold(CoreKey key)
    {
        if (_model != null && _model.GrblState.State != GrblStates.Idle)
            _streaming.FeedHold();
        return _model?.GrblState.State != GrblStates.Idle;
    }

    bool Home(CoreKey key)
    {
        if (_model != null)
            _commands.ExecuteCommand(_model, GrblConstants.CMD_HOMING);
        return true;
    }

    bool Unlock(CoreKey key)
    {
        if (_model != null)
            _commands.ExecuteCommand(_model, GrblConstants.CMD_UNLOCK);
        return true;
    }

    bool Reset(CoreKey key)
    {
        _commands.Reset();
        return true;
    }

    bool FeedRateUp(CoreKey key)
    {
        _commands.SendRealtime(GrblConstants.CMD_FEED_OVR_COARSE_PLUS);
        return true;
    }

    bool FeedRateDown(CoreKey key)
    {
        _commands.SendRealtime(GrblConstants.CMD_FEED_OVR_COARSE_MINUS);
        return true;
    }

    bool FeedRateUpFine(CoreKey key)
    {
        _commands.SendRealtime(GrblConstants.CMD_FEED_OVR_FINE_PLUS);
        return true;
    }

    bool FeedRateDownFine(CoreKey key)
    {
        _commands.SendRealtime(GrblConstants.CMD_FEED_OVR_FINE_MINUS);
        return true;
    }

    bool FnKeyHandler(CoreKey key)
    {
        if (_model == null || _model.IsJobRunning)
            return false;

        var id = (int)key - 89;
        var macros = _appBase?.Macros;
        var macro = macros?.FirstOrDefault(o => o.Id == id);
        if (macro == null)
            return false;

        if (macro.ConfirmOnExecute &&
            !MessageDialogs.AskYesNo($"Run {macro.Name} macro?", "Run macro"))
            return false;

        if (!Grbl.SendRealtimeCommand(macro.Code))
            _commands.ExecuteCommand(_model, macro.Code);
        return true;
    }
}
