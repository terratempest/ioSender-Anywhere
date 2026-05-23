using Avalonia.Media;
using CNC.App;
using HelixToolkit.Avalonia.SharpDX;
using HelixToolkit.SharpDX;

namespace CNC.GCodeViewer.Avalonia;

/// <summary>Builds Helix line models off the UI thread.</summary>
internal static class ToolpathSceneBuilder
{
    public sealed class SceneMeshes
    {
        public LineGeometryModel3D? Cut { get; init; }
        public LineGeometryModel3D? Rapid { get; init; }
        public LineGeometryModel3D? Retract { get; init; }
        public LineGeometryModel3D? Grid { get; init; }
        public LineGeometryModel3D? GridMajor { get; init; }
        public LineGeometryModel3D? JobBox { get; init; }
        public LineGeometryModel3D? WorkBox { get; init; }
        public int LayerCount { get; init; }
    }

    public static SceneMeshes Build(
        GCodePathSegments segments,
        PathBounds bounds,
        GCodeViewerConfig cfg,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var cut = segments.Cut.Count > 1
            ? HelixLineHelper.CreateLineModel(segments.Cut, ViewerColors.ResolveCutColor(cfg), 1.5f)
            : null;
        cancellationToken.ThrowIfCancellationRequested();

        var rapid = segments.Rapid.Count > 1
            ? HelixLineHelper.CreateLineModel(segments.Rapid, ViewerColors.ResolveRapidColor(cfg), 0.75f)
            : null;
        cancellationToken.ThrowIfCancellationRequested();

        var retract = segments.Retract.Count > 1
            ? HelixLineHelper.CreateLineModel(segments.Retract, ViewerColors.ResolveRetractColor(cfg), 0.75f)
            : null;
        cancellationToken.ThrowIfCancellationRequested();

        LineGeometryModel3D? grid = null;
        LineGeometryModel3D? gridMajor = null;
        LineGeometryModel3D? jobBox = null;
        LineGeometryModel3D? workBox = null;

        if (!bounds.IsEmpty)
        {
            if (cfg.ShowGrid)
            {
                var gridLines = ViewerGridBuilder.Build(bounds);
                var color = ViewerColors.ResolveGridColor(cfg);
                if (gridLines.Minor.Count > 1)
                    grid = HelixLineHelper.CreateLineModel(gridLines.Minor, color, 0.5f);
                if (gridLines.Major.Count > 1)
                    gridMajor = HelixLineHelper.CreateLineModel(gridLines.Major, color, 1.1f);
            }

            if (cfg.ShowBoundingBox)
            {
                var job = ViewerEnvelopeBuilder.JobBox(bounds);
                if (job.Count > 1)
                    jobBox = HelixLineHelper.CreateLineModel(job, cfg.HighlightColor.ToColor(), 1f);
            }

            if (cfg.ShowWorkEnvelope)
            {
                var work = ViewerEnvelopeBuilder.WorkAreaBox();
                if (work.Count > 1)
                    workBox = HelixLineHelper.CreateLineModel(work, Colors.DarkBlue, 1f);
            }
        }

        var layerCount = 0;
        if (cut != null) layerCount++;
        if (rapid != null) layerCount++;
        if (retract != null) layerCount++;
        if (grid != null) layerCount++;
        if (gridMajor != null) layerCount++;
        if (jobBox != null) layerCount++;
        if (workBox != null) layerCount++;

        return new SceneMeshes
        {
            Cut = cut,
            Rapid = rapid,
            Retract = retract,
            Grid = grid,
            GridMajor = gridMajor,
            JobBox = jobBox,
            WorkBox = workBox,
            LayerCount = layerCount,
        };
    }
}
