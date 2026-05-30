using Avalonia.Controls;
using CNC.App.Workspace;
using CNC.Controls.Avalonia.Services;
using CNC.Controls.Avalonia.Views;
using CNC.Controls.Config;
using CNC.Controls.Lathe;
using CNC.Controls.Probing;
using CNC.GCodeViewer.Avalonia.Views;
using ioSender.Services;
using ioSender.ViewModels;
using ioSender.Workspace.Controls;

namespace ioSender.Workspace.Editors;

public sealed class WorkspaceEditorFactory
{
    readonly Dictionary<Guid, Control> _cache = new();
    readonly Dictionary<Guid, WorkspaceEditorId> _editorByLeaf = new();
    readonly WorkspaceEditorControlFactory _controlFactory;

    public WorkspaceEditorFactory(
        object grblContext,
        object? appConfigContext = null,
        AppSession? session = null,
        ProgramService? programService = null)
    {
        _controlFactory = new WorkspaceEditorControlFactory(
            grblContext,
            appConfigContext,
            session,
            programService);
    }

    public Control GetOrCreate(WorkspaceLeaf leaf)
        => GetOrCreate(leaf.Id, leaf.Editor);

    public Control GetOrCreate(WorkspaceTabEntry tab)
        => GetOrCreate(tab.Id, tab.Editor);

    Control GetOrCreate(Guid id, WorkspaceEditorId editorId)
    {
        if (_cache.TryGetValue(id, out var existing)
            && _editorByLeaf.TryGetValue(id, out var cachedEditor)
            && cachedEditor == editorId)
            return existing;

        Remove(id);
        var control = _controlFactory.Create(editorId);
        _cache[id] = control;
        _editorByLeaf[id] = editorId;
        return control;
    }

    public IEnumerable<Control> AllCached => _cache.Values;

    public bool TryGetCached(WorkspaceLeaf leaf, out Control? control) =>
        _cache.TryGetValue(leaf.Id, out control);

    public void Invalidate(WorkspaceLeaf leaf) => Remove(leaf);

    public void Remove(WorkspaceLeaf leaf) => Remove(leaf.Id);

    public void Remove(WorkspaceTabEntry tab) => Remove(tab.Id);

    void Remove(Guid id)
    {
        if (_cache.Remove(id, out var control))
            ReleaseControl(control);
        _editorByLeaf.Remove(id);
    }

    public void ReleaseAllFromVisualTree()
    {
        foreach (var control in _cache.Values.ToList())
            ReleaseControl(control);
    }

    public void PruneToTree(WorkspaceNode root)
    {
        var live = EnumerateCacheIds(root).ToHashSet();
        foreach (var id in _cache.Keys.ToList())
        {
            if (live.Contains(id))
                continue;

            if (_cache.Remove(id, out var control))
                ReleaseControl(control);
            _editorByLeaf.Remove(id);
        }
    }

    static IEnumerable<Guid> EnumerateCacheIds(WorkspaceNode node)
    {
        switch (node)
        {
            case WorkspaceLeaf leaf:
                yield return leaf.Id;
                break;
            case WorkspaceSplit split:
                foreach (var id in EnumerateCacheIds(split.First))
                    yield return id;
                foreach (var id in EnumerateCacheIds(split.Second))
                    yield return id;
                break;
            case WorkspaceTabGroup tabGroup:
                foreach (var tab in tabGroup.Tabs)
                    yield return tab.Id;
                break;
        }
    }

    public static void ReleaseControl(Control control)
    {
        if (control is RenderControl viewer)
            viewer.Close();
        WorkspaceRegionChrome.DetachEditor(control);
    }

    internal static object ResolveGrblContext(object context) =>
        context is ViewModels.MainWindowViewModel shell
            ? shell.Grbl
            : context;

    public static void SetActivation(Control control, bool active)
    {
        switch (control)
        {
            case ProbingView probing:
                probing.Activate(active);
                break;
            case SDCardView sd:
                sd.Activate(active);
                break;
            case LatheWizardsView lathe:
                lathe.Activate(active);
                break;
            case OffsetView offsets:
                offsets.Activate(active);
                break;
            case ToolView tools:
                tools.Activate(active);
                break;
            case GrblConfigView grbl:
                grbl.Activate(active);
                break;
        }
    }
}
