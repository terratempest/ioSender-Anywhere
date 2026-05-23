using Avalonia.Controls;

namespace CNC.Converters;

public interface IGCodeConverter
{
    string FileType { get; }
    string FileExtensions { get; }
    bool LoadFile(IGCodeFileTarget job, string filename, Window? owner);
}
