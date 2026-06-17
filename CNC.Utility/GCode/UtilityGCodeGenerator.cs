using System.Globalization;

namespace CNC.Utility.GCode;

public enum UtilityUnits
{
    Metric,
    Imperial,
}

public enum UtilityOrigin
{
    LowerLeft,
    Center,
}

public enum SurfacingPassDirection
{
    AlongX,
    AlongY,
}

public enum SurfacingCutType
{
    Both,
    Climb,
    Conventional,
}

public sealed record CoolantOptions(bool Flood, bool Mist);

public sealed record SurfacingOptions(
    UtilityUnits Units,
    UtilityOrigin Origin,
    double OriginX,
    double OriginY,
    double OriginZ,
    double ToolDiameter,
    double MaterialLengthX,
    double MaterialWidthY,
    double TargetDepth,
    int StepDownPasses,
    int FinishPasses,
    double ToolEngagementPercent,
    double FeedRate,
    double PlungeFeedRate,
    double SafeZ,
    int SpindleRpm,
    CoolantOptions Coolant,
    SurfacingPassDirection PassDirection,
    SurfacingCutType CutType);

public sealed record DrillHole(double X, double Y, double Depth, double DwellSeconds = 0d);

public sealed record DrillingOptions(
    UtilityUnits Units,
    double SafeZ,
    double RetractZ,
    double FeedRate,
    double PlungeFeedRate,
    int SpindleRpm,
    CoolantOptions Coolant,
    IReadOnlyList<DrillHole> Holes);

public static class UtilityGCodeGenerator
{
    public static IReadOnlyList<string> GenerateSurfacing(SurfacingOptions options)
    {
        ValidateSurfacing(options);

        var lines = CreateHeader(options.Units, options.SpindleRpm, options.Coolant, "Surfacing");
        var radius = options.ToolDiameter / 2d;
        var stepover = options.ToolDiameter * options.ToolEngagementPercent / 100d;
        var minX = options.OriginX;
        var maxX = options.OriginX + options.MaterialLengthX;
        var minY = options.OriginY;
        var maxY = options.OriginY + options.MaterialWidthY;

        if (options.Origin == UtilityOrigin.Center)
        {
            minX = options.OriginX - options.MaterialLengthX / 2d;
            maxX = options.OriginX + options.MaterialLengthX / 2d;
            minY = options.OriginY - options.MaterialWidthY / 2d;
            maxY = options.OriginY + options.MaterialWidthY / 2d;
        }

        minX -= radius;
        maxX += radius;
        minY -= radius;
        maxY += radius;

        var passes = BuildDepths(options.TargetDepth + options.OriginZ, options.OriginZ, options.StepDownPasses, options.FinishPasses);
        foreach (var depth in passes)
            AppendSurfacingPass(lines, options, minX, maxX, minY, maxY, stepover, depth);

        AppendFooter(lines, options.SafeZ, options.Coolant);
        return lines;
    }

    public static IReadOnlyList<string> GenerateDrilling(DrillingOptions options)
    {
        ValidateDrilling(options);

        var lines = CreateHeader(options.Units, options.SpindleRpm, options.Coolant, "Drilling");
        lines.Add($"G0 Z{Format(options.SafeZ)}");

        foreach (var hole in options.Holes)
        {
            lines.Add($"G0 X{Format(hole.X)} Y{Format(hole.Y)}");
            lines.Add($"G0 Z{Format(options.RetractZ)}");
            lines.Add($"G1 Z{Format(hole.Depth)} F{Format(options.PlungeFeedRate)}");
            if (hole.DwellSeconds > 0d)
                lines.Add($"G4 P{Format(hole.DwellSeconds)}");
            lines.Add($"G1 Z{Format(options.RetractZ)} F{Format(options.FeedRate)}");
        }

        AppendFooter(lines, options.SafeZ, options.Coolant);
        return lines;
    }

    static List<string> CreateHeader(UtilityUnits units, int spindleRpm, CoolantOptions coolant, string name)
    {
        var lines = new List<string>
        {
            $"({name})",
            units == UtilityUnits.Metric ? "G21" : "G20",
            "G90",
            "G17",
            "G91.1",
            $"S{spindleRpm}",
            "M3",
        };

        if (coolant.Mist)
            lines.Add("M7");
        if (coolant.Flood)
            lines.Add("M8");

        return lines;
    }

    static void AppendFooter(List<string> lines, double safeZ, CoolantOptions coolant)
    {
        lines.Add($"G0 Z{Format(safeZ)}");
        if (coolant.Mist || coolant.Flood)
            lines.Add("M9");
        lines.Add("M5");
        lines.Add("M30");
    }

    static void AppendSurfacingPass(
        List<string> lines,
        SurfacingOptions options,
        double minX,
        double maxX,
        double minY,
        double maxY,
        double stepover,
        double depth)
    {
        var tracks = BuildTracks(
            options.PassDirection == SurfacingPassDirection.AlongX ? minY : minX,
            options.PassDirection == SurfacingPassDirection.AlongX ? maxY : maxX,
            stepover);

        var forward = options.CutType != SurfacingCutType.Conventional;
        if (options.CutType == SurfacingCutType.Both)
        {
            AppendBidirectionalSurfacingPass(lines, options, minX, maxX, minY, maxY, tracks, depth);
            return;
        }

        for (var i = 0; i < tracks.Count; i++)
        {
            AppendTrack(lines, options, minX, maxX, minY, maxY, tracks[i], depth, forward);
        }
    }

    static void AppendBidirectionalSurfacingPass(
        List<string> lines,
        SurfacingOptions options,
        double minX,
        double maxX,
        double minY,
        double maxY,
        IReadOnlyList<double> tracks,
        double depth)
    {
        if (tracks.Count == 0)
            return;

        lines.Add($"G0 Z{Format(options.SafeZ)}");

        var positive = true;
        if (options.PassDirection == SurfacingPassDirection.AlongX)
        {
            lines.Add($"G0 X{Format(minX)} Y{Format(tracks[0])}");
            lines.Add($"G1 Z{Format(depth)} F{Format(options.PlungeFeedRate)}");

            for (var i = 0; i < tracks.Count; i++)
            {
                var endX = positive ? maxX : minX;
                lines.Add($"G1 X{Format(endX)} F{Format(options.FeedRate)}");

                if (i < tracks.Count - 1)
                    AppendAlongXConnector(lines, endX, tracks[i + 1], positive, tracks[i + 1] - tracks[i], options.FeedRate);

                positive = !positive;
            }

            return;
        }

        lines.Add($"G0 X{Format(tracks[0])} Y{Format(minY)}");
        lines.Add($"G1 Z{Format(depth)} F{Format(options.PlungeFeedRate)}");

        for (var i = 0; i < tracks.Count; i++)
        {
            var endY = positive ? maxY : minY;
            lines.Add($"G1 Y{Format(endY)} F{Format(options.FeedRate)}");

            if (i < tracks.Count - 1)
                AppendAlongYConnector(lines, tracks[i + 1], endY, positive, tracks[i + 1] - tracks[i], options.FeedRate);

            positive = !positive;
        }
    }

    static void AppendAlongXConnector(List<string> lines, double x, double y, bool rightSide, double trackSpacing, double feedRate)
    {
        var command = rightSide ? "G3" : "G2";
        lines.Add($"{command} X{Format(x)} Y{Format(y)} I0 J{Format(trackSpacing / 2d)} F{Format(feedRate)}");
    }

    static void AppendAlongYConnector(List<string> lines, double x, double y, bool topSide, double trackSpacing, double feedRate)
    {
        var command = topSide ? "G2" : "G3";
        lines.Add($"{command} X{Format(x)} Y{Format(y)} I{Format(trackSpacing / 2d)} J0 F{Format(feedRate)}");
    }

    static void AppendTrack(
        List<string> lines,
        SurfacingOptions options,
        double minX,
        double maxX,
        double minY,
        double maxY,
        double track,
        double depth,
        bool positive)
    {
        lines.Add($"G0 Z{Format(options.SafeZ)}");

        if (options.PassDirection == SurfacingPassDirection.AlongX)
        {
            var startX = positive ? minX : maxX;
            var endX = positive ? maxX : minX;
            lines.Add($"G0 X{Format(startX)} Y{Format(track)}");
            lines.Add($"G1 Z{Format(depth)} F{Format(options.PlungeFeedRate)}");
            lines.Add($"G1 X{Format(endX)} F{Format(options.FeedRate)}");
            return;
        }

        var startY = positive ? minY : maxY;
        var endY = positive ? maxY : minY;
        lines.Add($"G0 X{Format(track)} Y{Format(startY)}");
        lines.Add($"G1 Z{Format(depth)} F{Format(options.PlungeFeedRate)}");
        lines.Add($"G1 Y{Format(endY)} F{Format(options.FeedRate)}");
    }

    static List<double> BuildDepths(double targetDepth, double zeroZ, int stepDownPasses, int finishPasses)
    {
        if (targetDepth == zeroZ)
        {
            var zeroDepths = new List<double>(1 + finishPasses) { zeroZ };
            for (var pass = 0; pass < finishPasses; pass++)
                zeroDepths.Add(zeroZ);

            return zeroDepths;
        }

        var depths = new List<double>(stepDownPasses + finishPasses);
        for (var pass = 1; pass <= stepDownPasses; pass++)
            depths.Add(zeroZ + (targetDepth - zeroZ) * pass / stepDownPasses);

        for (var pass = 0; pass < finishPasses; pass++)
            depths.Add(targetDepth);

        return depths;
    }

    static List<double> BuildTracks(double min, double max, double stepover)
    {
        var tracks = new List<double>();
        const double tolerance = 0.000001d;
        for (var value = min; value <= max + tolerance; value += stepover)
            tracks.Add(value);

        if (tracks.Count == 0 || tracks[^1] < max - tolerance)
            tracks.Add(tracks.Count == 0 ? min : tracks[^1] + stepover);

        return tracks;
    }

    static void ValidateSurfacing(SurfacingOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.ToolDiameter);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaterialLengthX);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaterialWidthY);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.StepDownPasses);
        ArgumentOutOfRangeException.ThrowIfNegative(options.FinishPasses);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.FeedRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.PlungeFeedRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.SpindleRpm);

        if (options.TargetDepth > 0d)
            throw new ArgumentOutOfRangeException(nameof(options), "Target depth must be zero or negative.");
        if (options.ToolEngagementPercent <= 0d || options.ToolEngagementPercent > 100d)
            throw new ArgumentOutOfRangeException(nameof(options), "Tool engagement must be greater than 0 through 100.");
    }

    static void ValidateDrilling(DrillingOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.FeedRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.PlungeFeedRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.SpindleRpm);
        if (options.Holes.Count == 0)
            throw new ArgumentException("At least one drill hole is required.", nameof(options));
    }

    static string Format(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
