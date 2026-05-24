namespace CNC.Platform.Linux;

/// <summary>Filters /dev/tty* entries to CNC-relevant serial devices.</summary>
public static class LinuxSerialPortFilter
{
    public static bool IsRelevantDevice(string deviceFileName)
    {
        if (string.IsNullOrWhiteSpace(deviceFileName))
            return false;

        var name = Path.GetFileName(deviceFileName.Trim());

        if (name.StartsWith("ttyUSB", StringComparison.Ordinal)
            || name.StartsWith("ttyACM", StringComparison.Ordinal))
        {
            return true;
        }

        if (name.StartsWith("ttyS", StringComparison.Ordinal) && name.Length > 4
            && name[4..].All(char.IsDigit))
        {
            return true;
        }

        // Virtual consoles: tty0, tty1, ...
        if (name.Length >= 4
            && name.StartsWith("tty", StringComparison.Ordinal)
            && name[3..].All(char.IsDigit))
        {
            return false;
        }

        return false;
    }

    public static string NormalizeDevicePath(string portName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portName);

        var trimmed = portName.Trim();
        if (trimmed.StartsWith('/'))
            return trimmed;

        return $"/dev/{trimmed}";
    }
}
