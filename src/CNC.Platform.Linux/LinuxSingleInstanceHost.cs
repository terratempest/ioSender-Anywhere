using System.Net;
using System.Net.Sockets;
using System.Text;
using CNC.Platform.Abstractions;

namespace CNC.Platform.Linux;

public sealed class LinuxSingleInstanceHost : ISingleInstanceHost, IDisposable
{
    private readonly string _socketPath = Path.Combine(Path.GetTempPath(), "iosender.sock");
    private FileStream? _lockStream;
    private bool _isPrimary;

    public bool TryAcquire()
    {
        if (File.Exists(_socketPath))
        {
            try
            {
                using var probe = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                probe.Connect(new UnixDomainSocketEndPoint(_socketPath));
                return false;
            }
            catch (SocketException)
            {
                File.Delete(_socketPath);
            }
        }

        _lockStream = File.Open(_socketPath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        _isPrimary = true;
        return true;
    }

    public void SendMessage(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        client.Connect(new UnixDomainSocketEndPoint(_socketPath));

        if (!message.EndsWith('\n'))
            message += '\n';

        var payload = Encoding.UTF8.GetBytes(message);
        client.Send(payload);

    }

    public void Listen(Action<string> onMessage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onMessage);

        if (File.Exists(_socketPath))
        {
            File.Delete(_socketPath);
        }

        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(_socketPath));
        listener.Listen(1);

        while (!cancellationToken.IsCancellationRequested)
        {
            using var connection = listener.Accept();
            var buffer = new byte[4096];
            var received = connection.Receive(buffer);
            if (received <= 0)
            {
                continue;
            }

            var message = Encoding.UTF8.GetString(buffer, 0, received);
            if (!string.IsNullOrEmpty(message))
            {
                onMessage(message);
            }
        }
    }

    public void Dispose()
    {
        _lockStream?.Dispose();

        if (_isPrimary && File.Exists(_socketPath))
        {
            File.Delete(_socketPath);
        }
    }
}
