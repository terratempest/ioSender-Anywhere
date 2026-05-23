using System.IO.Pipes;
using System.Text;
using CNC.Platform.Abstractions;

namespace CNC.Platform.Windows;

public sealed class WindowsSingleInstanceHost : ISingleInstanceHost, IDisposable
{
    private const string PipeName = "ioSender";
    private readonly Mutex _mutex;
    private readonly bool _isPrimary;

    public WindowsSingleInstanceHost()
    {
        _mutex = new Mutex(false, PipeName, out var createdNew);
        _isPrimary = createdNew;
    }

    public bool TryAcquire() => _isPrimary;

    public void SendMessage(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
        client.Connect(3000);
        if (!message.EndsWith('\n'))
            message += '\n';

        var payload = Encoding.UTF8.GetBytes(message);
        client.Write(payload, 0, payload.Length);

    }

    public void Listen(Action<string> onMessage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onMessage);

        while (!cancellationToken.IsCancellationRequested)
        {
            using var server = new NamedPipeServerStream(
                PipeName,
                PipeDirection.In,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            try
            {
                server.WaitForConnectionAsync(cancellationToken).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                break;
            }

            using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
            var message = reader.ReadToEnd();
            if (!string.IsNullOrEmpty(message))
            {
                onMessage(message);
            }
        }
    }

    public void Dispose() => _mutex.Dispose();
}
