using Avalonia.Media;
using CNC.App;
using CNC.Core;
using CNC.GCodeViewer.Avalonia.OpenGl;

namespace CNC.GCodeViewer.Avalonia;

/// <summary>Builds viewer line layers off the UI thread.</summary>
internal static class ToolpathSceneBuilder
{
    public static ViewerScene Build(
        GCodePathSegments segments,
        PathBounds bounds,
        GCodeViewerConfig cfg,
        ViewerThemeColors theme,
        GrblViewModel? grbl = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var cut = segments.Cut.Count > 1
            ? ViewerLineLayerBuilder.FromPoints(segments.Cut, ViewerColors.ResolveCutColor(cfg, theme), 1.5f)
            : null;
        cancellationToken.ThrowIfCancellationRequested();

        var rapid = segments.Rapid.Count > 1
            ? ViewerLineLayerBuilder.FromPoints(segments.Rapid, ViewerColors.ResolveRapidColor(cfg, theme), 0.75f)
            : null;
        cancellationToken.ThrowIfCancellationRequested();

        var retract = segments.Retract.Count > 1
            ? ViewerLineLayerBuilder.FromPoints(segments.Retract, ViewerColors.ResolveRetractColor(cfg, theme), 0.75f)
            : null;
        cancellationToken.ThrowIfCancellationRequested();

        ViewerLineLayer? grid = null;
        ViewerLineLayer? gridMajor = null;
        ViewerLineLayer? jobBox = null;
        ViewerLineLayer? workBox = null;

        if (!bounds.IsEmpty)
        {
            if (cfg.ShowGrid)
            {
                var gridLines = ViewerGridBuilder.Build(bounds);
                var color = ViewerColors.ResolveGridColor(cfg, theme);
                if (gridLines.Minor.Count > 1)
                    grid = ViewerLineLayerBuilder.FromPoints(gridLines.Minor, IsDefaultGridColor(cfg) ? theme.GridMinor : HalfBrightness(color), 0.5f);
                if (gridLines.Major.Count > 1)
                    gridMajor = ViewerLineLayerBuilder.FromPoints(gridLines.Major, IsDefaultGridColor(cfg) ? theme.GridMajor : color, 1.25f);
            }

            if (cfg.ShowBoundingBox)
            {
                var job = ViewerEnvelopeBuilder.JobBox(bounds);
                if (job.Count > 1)
                    jobBox = ViewerLineLayerBuilder.FromPoints(job, ViewerColors.ResolveHighlightColor(cfg, theme), 1f);
            }

            if (cfg.ShowWorkEnvelope)
            {
                var work = ViewerEnvelopeBuilder.WorkAreaBox(grbl);
                if (work.Count > 1)
                    workBox = ViewerLineLayerBuilder.FromPoints(work, theme.WorkEnvelope, 1f);
            }
        }

        return new ViewerScene
        {
            Cut = cut,
            Rapid = rapid,
            Retract = retract,
            Grid = grid,
            GridMajor = gridMajor,
            JobBox = jobBox,
            WorkBox = workBox,
        };
    }

    static Color HalfBrightness(Color color) =>
        Color.FromArgb(color.A, (byte)(color.R / 2), (byte)(color.G / 2), (byte)(color.B / 2));

    static bool IsDefaultGridColor(GCodeViewerConfig cfg) =>
        cfg.GridColor.R == UiColor.Gray.R
        && cfg.GridColor.G == UiColor.Gray.G
        && cfg.GridColor.B == UiColor.Gray.B
        && cfg.GridColor.A == UiColor.Gray.A;
}
