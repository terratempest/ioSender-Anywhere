using CNC.Core;

namespace CNC.Controls.Avalonia.Services;

public enum UiJogQueueState
{
    Idle,
    Cancelling
}

public sealed class UiJogCommandQueue
{
    public const int MaxPendingClicks = 20;

    readonly Queue<string> _pending = new();
    bool _hasActiveStep;
    bool _activeStepAcked;
    bool _activeStepSawJog;
    bool _activeStepSawIdleStatus;
    bool _hasActiveContinuous;

    public event System.Action? Changed;

    public UiJogQueueState State { get; private set; } = UiJogQueueState.Idle;
    public int PendingCount => _pending.Count;

    public bool TryEnqueueStep(string command, GrblState grblState)
    {
        if (string.IsNullOrWhiteSpace(command))
            return false;

        if (!HasOpenComms())
        {
            Clear();
            return false;
        }

        if (_hasActiveContinuous || !CanEventuallyJog(grblState.State))
            return false;

        if (_pending.Count >= MaxPendingClicks)
            return false;

        _pending.Enqueue(command);
        TryDispatchNext(grblState);
        NotifyChanged();
        return true;
    }

    public bool TryStartContinuous(string command, GrblState grblState)
    {
        if (!HasOpenComms())
        {
            Clear();
            return false;
        }

        if (_hasActiveStep || _hasActiveContinuous || _pending.Count != 0 || !CanDispatchNow(grblState.State))
            return false;

        if (Comms.com is not { IsOpen: true } comms)
            return false;

        comms.WriteCommand(command);
        _hasActiveContinuous = true;
        State = UiJogQueueState.Idle;
        NotifyChanged();
        return true;
    }

    public void Stop()
    {
        if (State == UiJogQueueState.Cancelling && _pending.Count == 0 && !_hasActiveStep && !_hasActiveContinuous)
            return;

        ClearPending();
        ClearActiveStep();
        ClearActiveContinuous();

        if (Comms.com is { IsOpen: true } comms)
            comms.WriteByte(GrblConstants.CMD_JOG_CANCEL);

        State = UiJogQueueState.Cancelling;
        NotifyChanged();
    }

    public void Clear()
    {
        ClearPending();
        ClearActiveStep();
        ClearActiveContinuous();
        State = UiJogQueueState.Idle;
        NotifyChanged();
    }

    public void OnGrblStateChanged(GrblState grblState)
    {
        if (!HasOpenComms())
        {
            Clear();
            return;
        }

        if (!CanEventuallyJog(grblState.State))
        {
            Clear();
            return;
        }

        if (_hasActiveStep && grblState.State == GrblStates.Jog)
            _activeStepSawJog = true;

        if (State == UiJogQueueState.Cancelling && CanDispatchNow(grblState.State))
        {
            ClearActiveContinuous();
            State = UiJogQueueState.Idle;
            NotifyChanged();
            return;
        }

        if (_hasActiveStep && CanDispatchNow(grblState.State) && _activeStepSawJog)
            CompleteActiveStep(grblState);

        TryDispatchNext(grblState);
        NotifyChanged();
    }

    public void OnResponseReceived(string response, GrblState grblState)
    {
        if (!_hasActiveStep)
            return;

        if (response.StartsWith("error:", StringComparison.OrdinalIgnoreCase))
        {
            Clear();
            return;
        }

        if (response != "ok")
            return;

        _activeStepAcked = true;

    }

    public void OnRealtimeStatusProcessed(GrblState grblState)
    {
        if (!_hasActiveStep)
            return;

        if (grblState.State == GrblStates.Jog)
        {
            _activeStepSawJog = true;
            return;
        }

        if (!CanDispatchNow(grblState.State) || !_activeStepAcked)
            return;

        _activeStepSawIdleStatus = true;

        if (_activeStepSawIdleStatus)
        {
            CompleteActiveStep(grblState);
            TryDispatchNext(grblState);
            NotifyChanged();
        }
    }

    static bool CanEventuallyJog(GrblStates state) => state is GrblStates.Idle or GrblStates.Jog;

    static bool CanDispatchNow(GrblStates state) => state == GrblStates.Idle;

    static bool HasOpenComms() => Comms.com is { IsOpen: true };

    bool TryDispatchNext(GrblState grblState)
    {
        if (_hasActiveStep || _pending.Count == 0 || !CanDispatchNow(grblState.State))
            return true;

        if (Comms.com is not { IsOpen: true } comms)
            return false;

        var command = _pending.Dequeue();
        comms.WriteCommand(command);
        _hasActiveStep = true;
        _activeStepAcked = false;
        _activeStepSawJog = false;
        _activeStepSawIdleStatus = false;
        State = UiJogQueueState.Idle;
        NotifyChanged();
        return true;
    }

    void CompleteActiveStep(GrblState grblState)
    {
        ClearActiveStep();
        State = CanDispatchNow(grblState.State) ? UiJogQueueState.Idle : State;
    }

    void ClearActiveStep()
    {
        _hasActiveStep = false;
        _activeStepAcked = false;
        _activeStepSawJog = false;
        _activeStepSawIdleStatus = false;
    }

    void ClearActiveContinuous() => _hasActiveContinuous = false;

    void ClearPending() => _pending.Clear();

    void NotifyChanged() => Changed?.Invoke();
}
