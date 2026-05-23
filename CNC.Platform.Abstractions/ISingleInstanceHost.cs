namespace CNC.Platform.Abstractions;

public interface ISingleInstanceHost
{
    bool TryAcquire();

    void SendMessage(string message);

    void Listen(Action<string> onMessage, CancellationToken cancellationToken = default);
}
