using Avalonia.Media;
using NumericVector3 = System.Numerics.Vector3;

namespace CNC.GCodeViewer.Avalonia.OpenGl;

public enum ViewerPrimitiveKind
{
    Lines,
    Triangles
}

/// <summary>CPU-side geometry batch for OpenGL.</summary>
public sealed class ViewerLineLayer
{
    public required IReadOnlyList<NumericVector3> Points { get; init; }
    public required Color Color { get; init; }
    public ViewerPrimitiveKind PrimitiveKind { get; init; } = ViewerPrimitiveKind.Lines;
    public float LineWidth { get; init; } = 1f;
    public string? Tag { get; init; }
}

/// <summary>Scene graph data for the toolpath viewer (OS-neutral).</summary>
public sealed class ViewerScene
{
    public ViewerLineLayer? Cut { get; init; }
    public ViewerLineLayer? Rapid { get; init; }
    public ViewerLineLayer? Retract { get; init; }
    public ViewerLineLayer? Grid { get; init; }
    public ViewerLineLayer? GridMajor { get; init; }
    public ViewerLineLayer? JobBox { get; init; }
    public ViewerLineLayer? WorkBox { get; init; }
    /// <summary>Pending cuts while completed cuts are shown in a darker color.</summary>
    public ViewerLineLayer? PendingCut { get; set; }
    /// <summary>Completed cuts while job runs.</summary>
    public ViewerLineLayer? Executed { get; set; }
    public ViewerLineLayer? ViewCube { get; set; }
    public ViewerLineLayer? ToolMarker { get; set; }
    public IReadOnlyList<ViewerLineLayer> ToolMarkerLayers { get; set; } = [];
    public IReadOnlyList<ViewerLineLayer> OriginAxes { get; set; } = [];
    /// <summary>Additional layers (e.g. height-map preview) beyond the standard slots.</summary>
    public IReadOnlyList<ViewerLineLayer> ExtraLayers { get; set; } = [];

    public int LayerCount
    {
        get
        {
            var n = 0;
            if (Cut != null && PendingCut == null && Executed == null) n++;
            if (Rapid != null) n++;
            if (Retract != null) n++;
            if (Grid != null) n++;
            if (GridMajor != null) n++;
            if (JobBox != null) n++;
            if (WorkBox != null) n++;
            if (PendingCut != null) n++;
            if (Executed != null) n++;
            if (ViewCube != null) n++;
            if (ToolMarker != null) n++;
            n += ToolMarkerLayers.Count;
            n += OriginAxes.Count;
            n += ExtraLayers.Count;
            return n;
        }
    }

    public IEnumerable<ViewerLineLayer> AllLayers(bool includeToolMarker = true)
    {
        if (Grid != null) yield return Grid;
        if (GridMajor != null) yield return GridMajor;
        if (Cut != null && PendingCut == null && Executed == null) yield return Cut;
        if (Rapid != null) yield return Rapid;
        if (Retract != null) yield return Retract;
        if (JobBox != null) yield return JobBox;
        if (WorkBox != null) yield return WorkBox;
        if (PendingCut != null) yield return PendingCut;
        if (Executed != null) yield return Executed;
        if (ViewCube != null) yield return ViewCube;
        foreach (var axis in OriginAxes)
            yield return axis;
        if (includeToolMarker && ToolMarker != null) yield return ToolMarker;
        if (includeToolMarker)
            foreach (var marker in ToolMarkerLayers)
                yield return marker;
        foreach (var extra in ExtraLayers)
            yield return extra;
    }
}
