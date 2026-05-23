namespace CNC.Platform.Abstractions;

public interface IPathService
{
    string Combine(params string[] segments);

    string NormalizeConfigPath(string path);

    bool IsPhysicalFilePath(string? path);
}
