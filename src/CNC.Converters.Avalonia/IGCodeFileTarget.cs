using CNC.Core;

namespace CNC.Converters;

public interface IGCodeFileTarget
{
    void AddBlock(string block);
    void AddBlock(string block, CNC.Core.Action action);
}
