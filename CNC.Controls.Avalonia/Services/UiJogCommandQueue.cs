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
    bool _waitingForNextIdle;

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

        if (!CanEventuallyJog(grblState.State))
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

        if (_waitingForNextIdle || _pending.Count != 0 || !CanDispatchNow(grblState.State))
            return false;

        _pending.Enqueue(command);
        var dispatched = TryDispatchNext(grblState);
        NotifyChanged();
        return dispatched;
    }

    public void Stop()
    {
        if (State == UiJogQueueState.Cancelling && _pending.Count == 0 && !_waitingForNextIdle)
            return;

        ClearPending();

        if (Comms.com is { IsOpen: true } comms)
            comms.WriteByte(GrblConstants.CMD_JOG_CANCEL);

        State = UiJogQueueState.Cancelling;
        _waitingForNextIdle = false;
        NotifyChanged();
    }

    public void Clear()
    {
        ClearPending();
        State = UiJogQueueState.Idle;
        _waitingForNextIdle = false;
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

        if (State == UiJogQueueState.Cancelling && CanDispatchNow(grblState.State))
        {
            State = UiJogQueueState.Idle;
            NotifyChanged();
            return;
        }

        if (CanDispatchNow(grblState.State))
            _waitingForNextIdle = false;

        TryDispatchNext(grblState);
        NotifyChanged();
    }

    static bool CanEventuallyJog(GrblStates state) => state is GrblStates.Idle or GrblStates.Jog;

    static bool CanDispatchNow(GrblStates state) => state == GrblStates.Idle;

    static bool HasOpenComms() => Comms.com is { IsOpen: true };

    bool TryDispatchNext(GrblState grblState)
    {
        if (_waitingForNextIdle || _pending.Count == 0 || !CanDispatchNow(grblState.State))
            return true;

        if (Comms.com is not { IsOpen: true } comms)
            return false;

        var command = _pending.Dequeue();
        comms.WriteCommand(command);
        _waitingForNextIdle = true;
        State = UiJogQueueState.Idle;
        return true;
    }

    void ClearPending() => _pending.Clear();

    void NotifyChanged() => Changed?.Invoke();
}
