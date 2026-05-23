namespace CNC.Core;

public sealed class SerialPortConfigurationException : Exception
{
    public SerialPortConfigurationException(string message)
        : base(message)
    {
    }

    public SerialPortConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
