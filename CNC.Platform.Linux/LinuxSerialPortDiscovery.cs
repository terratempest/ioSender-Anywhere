using System.IO.Ports;
using CNC.Platform.Abstractions;

namespace CNC.Platform.Linux;

public sealed class LinuxSerialPortDiscovery : ISerialPortDiscovery
{
    public IReadOnlyList<SerialPortInfo> GetPorts()
    {
        var ports = new HashSet<string>(StringComparer.Ordinal);

        foreach (var name in SerialPort.GetPortNames())
        {
            ports.Add(name);
        }

        if (Directory.Exists("/dev"))
        {
            foreach (var devicePath in Directory.EnumerateFiles("/dev", "tty*"))
            {
                var name = Path.GetFileName(devicePath);
                if (name.StartsWith("tty", StringComparison.Ordinal))
                {
                    ports.Add(devicePath);
                }
            }
        }

        return ports
            .OrderBy(static name => name, StringComparer.Ordinal)
            .Select(name => new SerialPortInfo(name, name))
            .ToList();
    }
}
