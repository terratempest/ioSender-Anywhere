using Avalonia;
using Avalonia.Controls;
using CNC.App;
using CNC.App.Workspace;
using ioSender.Workspace.Controls;
using ioSender.Workspace.Editors;

namespace ioSender.QuickAccess;

public sealed class QuickAccessSidebarController
{
    readonly QuickAccessSidebarHost _leftHost;
    readonly QuickAccessSidebarHost _rightHost;
    readonly Border _backdrop;
    readonly QuickAccessEditorFactory _editorFactory;
    QuickAccessSidebarHost? _openHost;
    Guid? _openTabId;
    Control? _openEditor;
    QuickAccessTabEntry? _openTab;

    public QuickAccessSidebarController(
        QuickAccessSidebarHost leftHost,
        QuickAccessSidebarHost rightHost,
        Border backdrop,
        object grblContext)
    {
        _leftHost = leftHost;
        _rightHost = rightHost;
        _backdrop = backdrop;
        _editorFactory = new QuickAccessEditorFactory(grblContext);

        WireHost(_leftHost);
        WireHost(_rightHost);
    }

    void WireHost(QuickAccessSidebarHost host)
    {
        host.TabActivateRequested += OnTabActivateRequested;
        host.AddTabRequested += OnAddTabRequested;
        host.TabRemoveRequested += OnTabRemoveRequested;
        host.TabEditorChangeRequested += OnTabEditorChangeRequested;
        host.PanelCloseRequested += (_, _) => ClosePanel();
        host.PanelResizeCompleted += OnPanelResizeCompleted;
    }

    public void ClosePanel()
    {
        _leftHost.ClosePanel();
        _rightHost.ClosePanel();
        HideBackdrop();
        ReleaseOpenResources();
    }

    public void ApplyConfig(QuickAccessSidebarConfig config)
    {
        if (config.Tabs.Count == 0 && (config.ShowLeft || config.ShowRight))
            QuickAccessSidebarDefaults.EnsureDefaultTabs(config);

        config.Enabled = config.ShowLeft || config.ShowRight;
        _editorFactory.PruneToTabs(config.Tabs.Select(t => t.Id));

        _leftHost.IsVisible = config.ShowLeft;
        _rightHost.IsVisible = config.ShowRight;
        _leftHost.IsHitTestVisible = config.ShowLeft;
        _rightHost.IsHitTestVisible = config.ShowRight;

        _leftHost.DockSide = QuickAccessSidebarDock.Left;
        _rightHost.DockSide = QuickAccessSidebarDock.Right;
        _leftHost.BindTabs(config.Tabs);
        _rightHost.BindTabs(config.Tabs);

        if (!config.ShowLeft && !config.ShowRight)
            ClosePanel();
    }

    void OnAddTabRequested(object? sender, EventArgs e)
    {
        if (sender is not QuickAccessSidebarHost host)
            return;

        host.ShowAddPanelMenu(OnPanelPickedForNewTab);
    }

    void OnPanelPickedForNewTab(WorkspaceEditorId editorId)
    {
        var config = QuickAccessSidebarService.Config;
        var tab = new QuickAccessTabEntry { EditorId = editorId };
        config.Tabs.Add(tab);
        QuickAccessSidebarService.Persist();
        ApplyConfig(config);

        var host = PickHostForPanel();
        if (host is not null)
            ShowPanel(tab, host);
    }

    QuickAccessSidebarHost? PickHostForPanel()
    {
        var config = QuickAccessSidebarService.Config;
        if (config.ShowRight)
            return _rightHost;
        if (config.ShowLeft)
            return _leftHost;
        return null;
    }

    void OnTabEditorChangeRequested(object? sender, (QuickAccessTabEntry Tab, WorkspaceEditorId EditorId) args)
    {
        args.Tab.EditorId = args.EditorId;
        _editorFactory.ChangeEditor(args.Tab.Id, args.EditorId);
        QuickAccessSidebarService.Persist();
        ApplyConfig(QuickAccessSidebarService.Config);

        if (_openTabId == args.Tab.Id && sender is QuickAccessSidebarHost host)
            ShowPanel(args.Tab, host);
    }

    void OnTabRemoveRequested(object? sender, QuickAccessTabEntry tab)
    {
        if (_openTabId == tab.Id)
            ClosePanel();

        var config = QuickAccessSidebarService.Config;
        config.Tabs.RemoveAll(t => t.Id == tab.Id);
        _editorFactory.RemoveTab(tab.Id);
        QuickAccessSidebarService.Persist();
        ApplyConfig(config);
    }

    void OnTabActivateRequested(object? sender, QuickAccessTabEntry tab)
    {
        if (sender is not QuickAccessSidebarHost host)
            return;

        if (_openTabId == tab.Id && host.IsPanelOpen)
        {
            ClosePanel();
            return;
        }

        ShowPanel(tab, host);
    }

    void ShowPanel(QuickAccessTabEntry tab, QuickAccessSidebarHost host)
    {
        if (!host.TryGetTabButton(tab.Id, out var tabButton) || tabButton is null)
            return;

        if (_openHost is not null && _openHost != host)
            _openHost.ClosePanel();

        ReleaseOpenResources();

        var desc = WorkspaceEditorCatalog.Get(tab.EditorId);
        var (width, height) = QuickAccessSidebarDefaults.ResolvePopupSize(
            tab, desc.MinWidth, desc.MinHeight);

        var editor = _editorFactory.GetOrCreate(tab);
        WorkspaceRegionChrome.DetachEditor(editor);

        var chrome = host.PanelChromeControl;
        chrome.Width = width;
        chrome.Height = height;
        chrome.MinWidth = desc.MinWidth;
        chrome.MinHeight = desc.MinHeight;
        chrome.ConfigureResizeGrips(host.DockSide);
        chrome.SetTitle(tab.EditorId);
        chrome.SetEditor(tab.EditorId, editor);

        _openHost = host;
        _openEditor = editor;
        _openTabId = tab.Id;
        _openTab = tab;

        WorkspaceEditorFactory.SetActivation(editor, true);
        host.ShowPanel(tabButton);
        ShowBackdrop();
    }

    void OnPanelResizeCompleted(object? sender, Size size)
    {
        if (_openTab is null)
            return;

        _openTab.PopupWidth = size.Width;
        _openTab.PopupHeight = size.Height;
        QuickAccessSidebarService.Persist();
    }

    void ShowBackdrop()
    {
        _backdrop.IsVisible = true;
        _backdrop.IsHitTestVisible = true;
    }

    void HideBackdrop()
    {
        _backdrop.IsVisible = false;
        _backdrop.IsHitTestVisible = false;
    }

    void ReleaseOpenResources()
    {
        if (_openEditor is not null)
        {
            WorkspaceEditorFactory.SetActivation(_openEditor, false);
            WorkspaceRegionChrome.DetachEditor(_openEditor);
            _openEditor = null;
        }

        _openHost = null;
        _openTabId = null;
        _openTab = null;
    }
}
