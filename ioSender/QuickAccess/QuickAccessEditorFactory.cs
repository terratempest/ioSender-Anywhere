using Avalonia.Controls;
using CNC.App;
using CNC.App.Workspace;
using CNC.GCodeViewer.Avalonia.Views;
using ioSender.Services;
using ioSender.Workspace.Controls;
using ioSender.Workspace.Editors;

namespace ioSender.QuickAccess;

public sealed class QuickAccessEditorFactory
{
    readonly Dictionary<Guid, Control> _cache = new();
    readonly Dictionary<Guid, WorkspaceEditorId> _editorByTab = new();
    readonly WorkspaceEditorControlFactory _controlFactory;

    public QuickAccessEditorFactory(object grblContext, AppSession? session = null)
    {
        _controlFactory = new WorkspaceEditorControlFactory(grblContext, session: session);
    }

    public Control GetOrCreate(QuickAccessTabEntry tab)
    {
        if (_cache.TryGetValue(tab.Id, out var existing)
            && _editorByTab.TryGetValue(tab.Id, out var cached)
            && cached == tab.EditorId)
            return existing;

        RemoveTab(tab.Id);
        var control = _controlFactory.Create(tab.EditorId);
        _cache[tab.Id] = control;
        _editorByTab[tab.Id] = tab.EditorId;
        return control;
    }

    public void ChangeEditor(Guid tabId, WorkspaceEditorId editorId)
    {
        if (_editorByTab.TryGetValue(tabId, out var current) && current == editorId)
            return;

        RemoveTab(tabId);
    }

    public void RemoveTab(Guid tabId)
    {
        if (_cache.Remove(tabId, out var control))
            WorkspaceEditorFactory.ReleaseControl(control);
        _editorByTab.Remove(tabId);
    }

    public void PruneToTabs(IEnumerable<Guid> liveTabIds)
    {
        var live = liveTabIds.ToHashSet();
        foreach (var id in _cache.Keys.ToList())
        {
            if (live.Contains(id))
                continue;
            RemoveTab(id);
        }
    }
}
