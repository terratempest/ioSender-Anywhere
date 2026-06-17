using System.Collections.ObjectModel;
using CNC.Core;
using CNC.GCode;

namespace CNC.Utility.GCode;

public sealed class UtilityGCodePreview
{
    public UtilityGCodePreview(IReadOnlyList<GCodeToken> tokens, IReadOnlyList<GCodeBlock> blocks)
    {
        Tokens = tokens;
        Blocks = blocks;
    }

    public IReadOnlyList<GCodeToken> Tokens { get; }

    public IReadOnlyList<GCodeBlock> Blocks { get; }
}

public static class UtilityGCodePreviewParser
{
    public static UtilityGCodePreview Parse(IEnumerable<string> lines, string displayName)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var job = new GCodeJob();
        job.AddBlock(displayName, CNC.Core.Action.New);
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
                job.AddBlock(line.Trim(), CNC.Core.Action.Add);
        }

        job.AddBlock(string.Empty, CNC.Core.Action.End);

        return new UtilityGCodePreview(
            job.Tokens.ToList(),
            new ReadOnlyCollection<GCodeBlock>(job.Blocks.ToList()));
    }
}
