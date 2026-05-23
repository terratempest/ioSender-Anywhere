namespace CNC.Platform.Abstractions;

public interface IExternalEditor
{
    Task OpenFileAsync(string path, CancellationToken cancellationToken = default);
}
