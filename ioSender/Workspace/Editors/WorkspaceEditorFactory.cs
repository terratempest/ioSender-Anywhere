using Avalonia.Controls;
using CNC.App.Workspace;
using CNC.Controls.Avalonia.Views;
using CNC.Controls.Config;
using CNC.Controls.Lathe;
using CNC.Controls.Probing;
using CNC.Core;
using CNC.GCodeViewer.Avalonia.Views;
using ioSender.Services;
using ioSender.ViewModels;
using ioSender.Workspace.Controls;

namespace ioSender.Workspace.Editors;

public sealed class WorkspaceEditorFactory
{
    readonly Dictionary<Guid, Control> _cache = new();
    readonly Dictionary<Guid, WorkspaceEditorId> _editorByLeaf = new();
    readonly object _grblContext;
    readonly object? _appConfigContext;

    public WorkspaceEditorFactory(object grblContext, object? appConfigContext = null)
    {
        _grblContext = ResolveGrblContext(grblContext);
        _appConfigContext = appConfigContext ?? AppHostContext.AppConfig.Base;
    }

    public Control GetOrCreate(WorkspaceLeaf leaf)
    {
        if (_cache.TryGetValue(leaf.Id, out var existing)
            && _editorByLeaf.TryGetValue(leaf.Id, out var cachedEditor)
            && cachedEditor == leaf.Editor)
            return existing;

        Remove(leaf);
        var control = Create(leaf.Editor);
        var desc = WorkspaceEditorCatalog.Get(leaf.Editor);
        if (desc.RequiresGrblDataContext)
            control.DataContext = _grblContext;

        _cache[leaf.Id] = control;
        _editorByLeaf[leaf.Id] = leaf.Editor;
        return control;
    }

    public IEnumerable<Control> AllCached => _cache.Values;

    public bool TryGetCached(WorkspaceLeaf leaf, out Control? control) =>
        _cache.TryGetValue(leaf.Id, out control);

    public void Invalidate(WorkspaceLeaf leaf) => Remove(leaf);

    public void Remove(WorkspaceLeaf leaf)
    {
        if (_cache.Remove(leaf.Id, out var control))
            ReleaseControl(control);
        _editorByLeaf.Remove(leaf.Id);
    }

    /// <summary>Detaches all cached editors before the workspace visual tree is torn down.</summary>
    public void ReleaseAllFromVisualTree()
    {
        foreach (var control in _cache.Values.ToList())
            ReleaseControl(control);
    }

    public void PruneToTree(WorkspaceNode root)
    {
        var live = root.EnumerateLeaves().Select(l => l.Id).ToHashSet();
        foreach (var id in _cache.Keys.ToList())
        {
            if (live.Contains(id))
                continue;

            if (_cache.Remove(id, out var control))
                ReleaseControl(control);
            _editorByLeaf.Remove(id);
        }
    }

    static void ReleaseControl(Control control)
    {
        if (control is RenderControl viewer)
            viewer.Close();
        WorkspaceRegionChrome.DetachEditor(control);
    }

    Control Create(WorkspaceEditorId id) => id switch
    {
        WorkspaceEditorId.Program => new GCodeListControl
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        },
        WorkspaceEditorId.Viewer3D => new RenderControl
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        },
        WorkspaceEditorId.Console => new ConsoleControl(),
        WorkspaceEditorId.Mdi => new MDIControl
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        },
        WorkspaceEditorId.MdiTouch => new MDITouchControl
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        },
        WorkspaceEditorId.Dro => new DROControl(),
        WorkspaceEditorId.Signals => new SignalsControl(),
        WorkspaceEditorId.Status => new StatusControl
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
        },
        WorkspaceEditorId.Jog => new JogControl { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch },
        WorkspaceEditorId.Outline => new OutlineControl(),
        WorkspaceEditorId.Goto => new GotoControl(),
        WorkspaceEditorId.WorkParams => new WorkParametersControl
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        },
        WorkspaceEditorId.Spindle => new SpindleControl
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        },
        WorkspaceEditorId.Coolant => new CoolantControl
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        },
        WorkspaceEditorId.Feed => new FeedControl
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
        },
        WorkspaceEditorId.JobBar => new JobBarEditorControl(),
        WorkspaceEditorId.Probing => new ProbingView(),
        WorkspaceEditorId.SdCard => new SDCardView(),
        WorkspaceEditorId.Lathe => new LatheWizardsView(),
        WorkspaceEditorId.Offsets => new OffsetView(),
        WorkspaceEditorId.Tools => new ToolView(),
        WorkspaceEditorId.GrblConfig => new GrblConfigView(),
        WorkspaceEditorId.AppConfig => CreateAppConfig(),
        _ => new TextBlock { Text = id.ToString() },
    };

    Control CreateAppConfig()
    {
        var view = new AppConfigView { DataContext = _appConfigContext };
        return view;
    }

    static object ResolveGrblContext(object context) =>
        context is MainWindowViewModel shell
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
