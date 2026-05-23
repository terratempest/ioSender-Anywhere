using CNC.Controls.Avalonia.Services;
using CNC.Core;

namespace CNC.Converters;

public sealed class GCodeFileTarget : IGCodeFileTarget
{
    readonly GCodeFileService _file;

    public GCodeFileTarget(GCodeFileService file) => _file = file;

    public static GCodeFileTarget Current => new(GCodeFileService.Instance);

    public void AddBlock(string block) => _file.AddBlock(block);

    public void AddBlock(string block, CNC.Core.Action action) => _file.AddBlock(block, action);
}
