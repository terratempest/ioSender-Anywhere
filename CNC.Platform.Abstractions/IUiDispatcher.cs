namespace CNC.Platform.Abstractions;

public interface IUiDispatcher
{
    void Post(Action action);

    Task InvokeAsync(Action action);

    Task<T> InvokeAsync<T>(Func<T> func);

    /// <summary>
    /// Processes pending UI-thread work. Used by blocking comms loops on the UI thread.
    /// </summary>
    void PumpPending();
}
