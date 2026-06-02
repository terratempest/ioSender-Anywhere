using System.IO.Ports;
using System.Management;
using System.Runtime.Versioning;
using CNC.Platform.Abstractions;

namespace CNC.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsSerialPortDiscovery : ISerialPortDiscovery
{
    public IReadOnlyList<SerialPortInfo> GetPorts()
    {
        var friendlyNames = TryGetFriendlyNames();
        var ports = SerialPort.GetPortNames()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase);

        return ports
            .Select(name => new SerialPortInfo(
                name,
                friendlyNames.TryGetValue(name, out var displayName) ? displayName : name))
            .ToList();
    }

    public bool IsPortAvailable(string portName)
    {
        if (string.IsNullOrWhiteSpace(portName))
            return false;

        return SerialPort.GetPortNames()
            .Any(name => string.Equals(name, portName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, string> TryGetFriendlyNames()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceID, Name FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'");

            foreach (var entry in searcher.Get().Cast<ManagementObject>())
            {
                var name = entry["Name"]?.ToString();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var start = name.LastIndexOf("(COM", StringComparison.OrdinalIgnoreCase);
                if (start < 0)
                {
                    continue;
                }

                var end = name.IndexOf(')', start);
                if (end <= start)
                {
                    continue;
                }

                var portName = name[(start + 1)..end];
                result[portName] = name;
            }
        }
        catch (ManagementException)
        {
        }
        catch (PlatformNotSupportedException)
        {
        }

        return result;
    }
}
