using CNC.Platform.Abstractions;

namespace CNC.Core;

public static class UiThread
{
    private static int? _uiThreadId;

    public static void Capture()
    {
        _uiThreadId = Environment.CurrentManagedThreadId;
    }

    public static bool IsOnUiThread =>
        _uiThreadId == null || Environment.CurrentManagedThreadId == _uiThreadId;

    public static void Invoke(System.Action action)
    {
        if (IsOnUiThread || Comms.UiDispatcher == null)
        {
            action();
            return;
        }

        Comms.UiDispatcher.InvokeAsync(action).GetAwaiter().GetResult();
    }

    public static void Post(System.Action action)
    {
        if (Comms.UiDispatcher != null)
            Comms.UiDispatcher.Post(action);
        else
            action();
    }
}
