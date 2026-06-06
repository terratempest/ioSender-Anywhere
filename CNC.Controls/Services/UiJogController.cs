using System.ComponentModel;
using CNC.App;
using CNC.Controls.Avalonia.Config;
using CNC.Controls.Avalonia.ViewModels;
using CNC.Core;

namespace CNC.Controls.Avalonia.Services;

public enum UiJogCommandMode
{
    Selector,
    Step,
    Continuous
}

public sealed class UiJogController : IDisposable
{
    readonly UiJogCommandQueue _jogQueue = new();
    readonly JogViewModel _jogData;
    readonly Func<AppConfigService?> _appConfig;
    GrblViewModel? _model;
    bool _softLimits;
    bool _firmwareJogLimiting;
    double _limitSwitchesClearance = .5d;

    internal static Func<bool> IsGrblHALController { get; set; } = () => GrblInfo.IsGrblHAL;

    public UiJogController(JogViewModel jogData, Func<AppConfigService?> appConfig)
    {
        _jogData = jogData;
        _appConfig = appConfig;
        _jogQueue.Changed += OnQueueChanged;
    }

    public event System.Action? QueueStatusChanged;

    public int PendingCount => _jogQueue.PendingCount;

    public string QueueStatusText => PendingCount > 0 ? $"Queue: {PendingCount}" : string.Empty;

    public bool IsQueueStatusVisible => PendingCount > 0;

    public void Attach(GrblViewModel model)
    {
        if (ReferenceEquals(_model, model))
            return;

        Detach();
        _model = model;
        model.PropertyChanged += Model_PropertyChanged;
        model.OnResponseReceived += Model_ResponseReceived;
        model.OnRealtimeStatusProcessed += Model_RealtimeStatusProcessed;
        ApplyJogUnits(model);
        RefreshMachineSettings();
    }

    public void Detach()
    {
        if (_model is null)
            return;

        _model.PropertyChanged -= Model_PropertyChanged;
        _model.OnResponseReceived = (Action<string>)Delegate.Remove(_model.OnResponseReceived, Model_ResponseReceived)!;
        _model.OnRealtimeStatusProcessed = (Action<string>)Delegate.Remove(_model.OnRealtimeStatusProcessed, Model_RealtimeStatusProcessed)!;
        _model = null;
        _jogQueue.Clear();
    }

    public void ApplyJogUnits()
    {
        if (_model is { } model)
            ApplyJogUnits(model);
    }

    public void Stop() => _jogQueue.Stop();

    public bool Jog(
        string cmd,
        UiJogCommandMode mode = UiJogCommandMode.Selector)
    {
        if (_model is not { } model)
            return false;

        if (cmd == "stop")
        {
            Stop();
            return true;
        }

        if (cmd.Length < 2)
            return false;

        var axis = GrblInfo.AxisLetterToIndex(cmd[0]);
        if (axis < 0)
            return false;

        if (!CanJog(model.GrblState.State))
            return false;

        var selectedDistance = mode switch
        {
            UiJogCommandMode.Continuous => -1d,
            UiJogCommandMode.Step => StepDistance(),
            _ => _jogData.Distance
        };
        var distance = (selectedDistance == -1d ? GrblInfo.MaxTravel.Values[axis] : selectedDistance) * (cmd[1] == '-' ? -1d : 1d);
        string command;
        var hasSoftLimitBoundary = HasUsableSoftLimitBoundary(axis);

        if (hasSoftLimitBoundary && !EnsureMachinePositionKnown(axis, model))
            return false;

        if (TryClampToSoftLimitBoundary(axis, distance, model, out var position))
        {
            var units = model.IsMetric ? "G21" : "G20";
            command = string.Format("$J=G53{0}{1}{2}F{3}", units, cmd[..1], position.ToInvariantString(), Math.Ceiling(_jogData.FeedRate).ToInvariantString());
        }
        else
        {
            if (hasSoftLimitBoundary)
                return false;

            var units = model.IsMetric ? "G21" : "G20";
            command = string.Format("$J=G91{0}{1}{2}F{3}", units, cmd[..1], distance.ToInvariantString(), Math.Ceiling(_jogData.FeedRate).ToInvariantString());
        }

        return selectedDistance == -1d
            ? _jogQueue.TryStartContinuous(command, model.GrblState)
            : _jogQueue.TryEnqueueStep(command, model.GrblState);
    }

    static bool CanJog(GrblStates state) => state is GrblStates.Idle or GrblStates.Jog;

    bool TryClampToSoftLimitBoundary(int axis, double distance, GrblViewModel model, out double position)
    {
        position = 0d;

        if (!CanUseSoftLimitBoundary(axis, model))
            return false;

        var current = model.MachinePosition.Values[axis];
        var requested = current + distance;
        var min = SoftLimitMinimum(axis);
        var max = SoftLimitMaximum(axis);

        if (distance > 0d)
        {
            if (current >= max)
                return false;

            position = Math.Min(requested, max);
            return position > current;
        }

        if (distance < 0d)
        {
            if (current <= min)
                return false;

            position = Math.Max(requested, min);
            return position < current;
        }

        return false;
    }

    bool CanUseSoftLimitBoundary(int axis, GrblViewModel model)
    {
        return HasUsableSoftLimitBoundary(axis) && IsMachinePositionKnown(axis, model);
    }

    bool HasUsableSoftLimitBoundary(int axis)
    {
        if (!_softLimits)
            return false;

        var maxTravel = GrblInfo.MaxTravel.Values[axis];

        if (!double.IsFinite(maxTravel) || maxTravel <= 0d || !double.IsFinite(_limitSwitchesClearance))
            return false;

        var min = SoftLimitMinimum(axis);
        var max = SoftLimitMaximum(axis);
        return double.IsFinite(min) && double.IsFinite(max) && min <= max;
    }

    static bool IsMachinePositionKnown(int axis, GrblViewModel model)
    {
        var axisFlag = GrblInfo.AxisIndexToFlag(axis);
        var canTrustMachinePosition =
            model.IsMachinePosition ||
            (model.WorkPosition.IsSet(axisFlag) && model.WorkPositionOffset.IsSet(axisFlag));

        return canTrustMachinePosition && double.IsFinite(model.MachinePosition.Values[axis]);
    }

    bool EnsureMachinePositionKnown(int axis, GrblViewModel model)
    {
        if (IsMachinePositionKnown(axis, model))
            return true;

        if (Comms.com is not { IsOpen: true } comms)
            return false;

        using var positionReceived = new ManualResetEventSlim(false);

        void OnStatus(string _)
        {
            if (IsMachinePositionKnown(axis, model))
                positionReceived.Set();
        }

        model.OnRealtimeStatusProcessed += OnStatus;
        try
        {
            var command = GrblInfo.IsGrblHAL
                ? GrblConstants.CMD_STATUS_REPORT_ALL
                : GrblLegacy.ConvertRTCommand(GrblConstants.CMD_STATUS_REPORT_ALL);
            comms.WriteByte(command);

            if (IsMachinePositionKnown(axis, model))
                return true;

            positionReceived.Wait(250);
            return IsMachinePositionKnown(axis, model);
        }
        finally
        {
            model.OnRealtimeStatusProcessed = (Action<string>)Delegate.Remove(model.OnRealtimeStatusProcessed, OnStatus)!;
        }
    }

    double SoftLimitMinimum(int axis)
    {
        var maxTravel = GrblInfo.MaxTravel.Values[axis];

        if (GrblInfo.ForceSetOrigin && GrblInfo.HomingDirection.HasFlag(GrblInfo.AxisIndexToFlag(axis)))
            return 0d;

        return -maxTravel + _limitSwitchesClearance;
    }

    double SoftLimitMaximum(int axis)
    {
        var maxTravel = GrblInfo.MaxTravel.Values[axis];

        if (GrblInfo.ForceSetOrigin)
            return GrblInfo.HomingDirection.HasFlag(GrblInfo.AxisIndexToFlag(axis))
                ? maxTravel - _limitSwitchesClearance
                : 0d;

        return -_limitSwitchesClearance;
    }

    double StepDistance() => _jogData.StepSize == JogViewModel.JogStep.Continuous
        ? _jogData.Distance1
        : _jogData.Distance;

    void ApplyJogUnits(GrblViewModel model)
    {
        _jogData.SetMetric(model.IsMetric, _appConfig()?.Base);
    }

    public void RefreshMachineSettings()
    {
        _firmwareJogLimiting = IsGrblHALController() && GrblSettings.GetInteger(grblHALSetting.SoftLimitJogging) == 1;
        _softLimits = GrblSettings.GetInteger(GrblSetting.SoftLimitsEnable) == 1 && !_firmwareJogLimiting;
        _limitSwitchesClearance = GrblSettings.GetDouble(GrblSetting.HomingPulloff);
    }

    void Model_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not GrblViewModel model)
            return;

        if (e.PropertyName == nameof(GrblViewModel.IsMetric))
            ApplyJogUnits(model);

        if (e.PropertyName == nameof(GrblViewModel.GrblState))
            _jogQueue.OnGrblStateChanged(model.GrblState);
    }

    void Model_ResponseReceived(string response)
    {
        if (_model is { } model)
            _jogQueue.OnResponseReceived(response, model.GrblState);
    }

    void Model_RealtimeStatusProcessed(string status)
    {
        if (_model is { } model)
            _jogQueue.OnRealtimeStatusProcessed(model.GrblState);
    }

    void OnQueueChanged() => QueueStatusChanged?.Invoke();

    public void Dispose()
    {
        Detach();
        _jogQueue.Changed -= OnQueueChanged;
    }
}
