using System.IO.Ports;
using CNC.Platform.Abstractions;

namespace CNC.Platform.Linux;

public sealed class LinuxSerialPortDiscovery : ISerialPortDiscovery
{
    public IReadOnlyList<SerialPortInfo> GetPorts()
    {
        var ports = new HashSet<string>(StringComparer.Ordinal);

        foreach (var name in SerialPort.GetPortNames())
            ports.Add(LinuxSerialPortFilter.NormalizeDevicePath(name));

        if (Directory.Exists("/dev"))
        {
            foreach (var devicePath in Directory.EnumerateFiles("/dev", "tty*"))
            {
                var name = Path.GetFileName(devicePath);
                if (LinuxSerialPortFilter.IsRelevantDevice(name))
                    ports.Add(devicePath);
            }
        }

        AddPersistentSerialLinks(ports, "/dev/serial/by-id");
        AddPersistentSerialLinks(ports, "/dev/serial/by-path");

        return ports
            .OrderBy(static name => name, StringComparer.Ordinal)
            .Select(name => new SerialPortInfo(name, name))
            .ToList();
    }

    public bool IsPortAvailable(string portName)
    {
        if (string.IsNullOrWhiteSpace(portName))
            return false;

        var normalized = LinuxSerialPortFilter.NormalizeDevicePath(portName);
        if (File.Exists(normalized) && IsDevicePathAllowed(normalized))
            return true;

        var resolved = ResolvePath(normalized);
        return GetPorts().Any(port =>
            string.Equals(port.Name, normalized, StringComparison.Ordinal)
            || string.Equals(ResolvePath(port.Name), resolved, StringComparison.Ordinal));
    }

    private static bool IsDevicePathAllowed(string path) =>
        path.StartsWith("/dev/", StringComparison.Ordinal)
        && (LinuxSerialPortFilter.IsRelevantDevice(path)
            || LinuxSerialPortFilter.IsPersistentSerialPath(path));

    private static void AddPersistentSerialLinks(HashSet<string> ports, string directory)
    {
        if (!Directory.Exists(directory))
            return;

        foreach (var devicePath in Directory.EnumerateFiles(directory))
            ports.Add(devicePath);
    }

    private static string ResolvePath(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var target = new FileInfo(path).ResolveLinkTarget(returnFinalTarget: true);
                if (target != null)
                    return target.FullName;
            }

            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }
}
