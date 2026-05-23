using System.Diagnostics;

namespace ioSender.Services;

internal static class StartupTrace
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    public static void Mark(string message)
    {
        var line = $"[startup +{Clock.ElapsedMilliseconds,5} ms] {message}";
        Trace.WriteLine(line);
        Console.Error.WriteLine(line);
    }

    public static IDisposable Measure(string message) => new Scope(message);

    private sealed class Scope : IDisposable
    {
        private readonly string _message;
        private readonly long _start = Clock.ElapsedMilliseconds;

        public Scope(string message)
        {
            _message = message;
            Mark($"{message} started");
        }

        public void Dispose()
        {
            Mark($"{_message} completed in {Clock.ElapsedMilliseconds - _start} ms");
        }
    }
}
