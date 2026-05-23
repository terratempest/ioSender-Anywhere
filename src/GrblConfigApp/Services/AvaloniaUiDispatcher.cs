using Avalonia.Threading;
using CNC.Platform.Abstractions;

namespace GrblConfigApp.Services;

public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public void Post(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }

    public Task InvokeAsync(Action action) =>
        Dispatcher.UIThread.InvokeAsync(action).GetTask();

    public Task<T> InvokeAsync<T>(Func<T> func) =>
        Dispatcher.UIThread.InvokeAsync(func).GetTask();

    public void PumpPending()
    {
        if (!Dispatcher.UIThread.CheckAccess())
            return;

        Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
    }
}
