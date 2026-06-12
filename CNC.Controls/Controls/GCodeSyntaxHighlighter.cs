namespace CNC.Controls.Avalonia.Controls;

public enum GCodeSyntaxKind
{
    Default,
    Comment,
    GCode,
    MCode,
    Macro,
    Address
}

public sealed record GCodeSyntaxRun(string Text, GCodeSyntaxKind Kind);

public static class GCodeSyntaxHighlighter
{
    static readonly HashSet<string> GCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "G0", "G00", "G1", "G01", "G2", "G02", "G3", "G03", "G4", "G04",
        "G10", "G17", "G18", "G19",
        "G20", "G21",
        "G28", "G30",
        "G38.2", "G38.3", "G38.4", "G38.5",
        "G40", "G41", "G42",
        "G43", "G43.1", "G49",
        "G53", "G54", "G55", "G56", "G57", "G58", "G59", "G59.1", "G59.2", "G59.3",
        "G61", "G61.1", "G64",
        "G73", "G76", "G80", "G81", "G82", "G83", "G84", "G85", "G86", "G87", "G88", "G89",
        "G90", "G90.1", "G91", "G91.1", "G92", "G92.1", "G92.2", "G92.3",
        "G93", "G94", "G95",
        "G96", "G97",
        "G98", "G99"
    };

    static readonly HashSet<string> MCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "M0", "M00", "M1", "M01", "M2", "M02",
        "M3", "M03", "M4", "M04", "M5", "M05",
        "M6", "M06",
        "M7", "M07", "M8", "M08", "M9", "M09",
        "M30",
        "M48", "M49",
        "M60",
        "M62", "M63", "M64", "M65",
        "M66",
        "M70", "M71", "M72", "M73",
        "M98", "M99"
    };

    static readonly HashSet<string> Macros = new(StringComparer.OrdinalIgnoreCase)
    {
        "IF", "THEN", "ELSE", "ENDIF",
        "WHILE", "ENDWHILE",
        "DO", "ENDDO",
        "REPEAT", "ENDREPEAT",
        "SUB", "ENDSUB", "CALL",
        "RETURN",
        "DEBUG", "PRINT", "MSG",
        "PROBE", "TOOLCHANGE",
        "EXISTS"
    };

    static readonly HashSet<char> AddressLetters = new("XYZABCUVWIJKRFSTHDPQLNOxyzabcuvwijkrfsthdpqlno");

    public static IReadOnlyList<GCodeSyntaxRun> Highlight(string? text, bool isCompleted = false)
    {
        if (string.IsNullOrEmpty(text))
            return Array.Empty<GCodeSyntaxRun>();

        if (isCompleted)
            return [new GCodeSyntaxRun(text, GCodeSyntaxKind.Default)];

        var runs = new List<GCodeSyntaxRun>();
        var i = 0;

        while (i < text.Length)
        {
            var c = text[i];

            if (c == '(')
            {
                var end = text.IndexOf(')', i + 1);
                var length = end >= 0 ? end - i + 1 : text.Length - i;
                runs.Add(new GCodeSyntaxRun(text.Substring(i, length), GCodeSyntaxKind.Comment));
                i += length;
                continue;
            }

            if (char.IsLetter(c))
            {
                if ((c is 'G' or 'g' or 'M' or 'm') && TryReadCommand(text, i, out var command))
                {
                    var kind = c is 'G' or 'g' ? GCodeSyntaxKind.GCode : GCodeSyntaxKind.MCode;
                    var known = kind == GCodeSyntaxKind.GCode ? GCodes.Contains(command) : MCodes.Contains(command);
                    runs.Add(new GCodeSyntaxRun(command, known ? kind : GCodeSyntaxKind.Default));
                    i += command.Length;
                    continue;
                }

                if (TryReadWord(text, i, out var word) && Macros.Contains(word))
                {
                    runs.Add(new GCodeSyntaxRun(word, GCodeSyntaxKind.Macro));
                    i += word.Length;
                    continue;
                }

                if (AddressLetters.Contains(c))
                {
                    runs.Add(new GCodeSyntaxRun(text.Substring(i, 1), GCodeSyntaxKind.Address));
                    i++;
                    continue;
                }
            }

            var start = i++;
            while (i < text.Length && text[i] != '(' && !char.IsLetter(text[i]))
                i++;
            runs.Add(new GCodeSyntaxRun(text.Substring(start, i - start), GCodeSyntaxKind.Default));
        }

        return MergeAdjacent(runs);
    }

    static bool TryReadCommand(string text, int start, out string command)
    {
        var i = start + 1;
        while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.'))
            i++;

        command = text.Substring(start, i - start);
        return command.Length > 1;
    }

    static bool TryReadWord(string text, int start, out string word)
    {
        var i = start;
        while (i < text.Length && char.IsLetter(text[i]))
            i++;

        word = text.Substring(start, i - start);
        return word.Length > 0;
    }

    static IReadOnlyList<GCodeSyntaxRun> MergeAdjacent(List<GCodeSyntaxRun> runs)
    {
        if (runs.Count < 2)
            return runs;

        var merged = new List<GCodeSyntaxRun> { runs[0] };
        for (var i = 1; i < runs.Count; i++)
        {
            var previous = merged[^1];
            var current = runs[i];
            if (previous.Kind == current.Kind)
                merged[^1] = previous with { Text = previous.Text + current.Text };
            else
                merged.Add(current);
        }

        return merged;
    }
}
