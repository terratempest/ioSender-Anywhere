using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using CNC.App;
using CNC.App.Workspace;
using CNC.GCodeViewer.Avalonia.Views;
using CNC.Localization.Avalonia;
using ioSender.QuickAccess;
using ioSender.Workspace.Editors;

namespace ioSender.Workspace.Controls;

public sealed class WorkspaceTabGroupControl : UserControl
{
    readonly WorkspaceTabGroup _group;
    readonly WorkspaceEditorFactory _factory;
    readonly Action _persist;
    readonly Action _activeEditorsChanged;
    readonly Action<string> _titleChanged;
    readonly Grid _root = new();
    readonly Border _tabRail = new();
    readonly StackPanel _tabStack = new() { Orientation = Orientation.Horizontal, Spacing = 2 };
    readonly ContentControl _contentHost = new();
    readonly TextBlock _emptyPrompt = new()
    {
        Text = "Click '+' to add a panel",
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Opacity = 0.72,
    };
    Guid? _activeEditorId;
    Control? _activeEditor;
    bool _themeAppliedHooked;

    public WorkspaceTabGroupControl(
        WorkspaceTabGroup group,
        WorkspaceEditorFactory factory,
        Action persist,
        Action activeEditorsChanged,
        Action<string> titleChanged)
    {
        _group = group;
        _factory = factory;
        _persist = persist;
        _activeEditorsChanged = activeEditorsChanged;
        _titleChanged = titleChanged;

        RefreshTabRailResources();
        _tabRail.Padding = new Thickness(3, 2);
        _tabRail.Child = _tabStack;

        AttachedToVisualTree += (_, _) => HookThemeApplied();
        DetachedFromVisualTree += (_, _) => UnhookThemeApplied();

        Content = _root;
        Build();
    }

    public void ReleaseActiveEditor()
    {
        if (_activeEditor is not null)
        {
            WorkspaceEditorFactory.SetActivation(_activeEditor, false);
            WorkspaceRegionChrome.DetachEditor(_activeEditor);
        }

        _activeEditor = null;
        _activeEditorId = null;
        _contentHost.Content = null;
    }

    void Build()
    {
        EnsureValidActiveTab();
        BuildLayout();
        BuildTabs();
        UpdateTitle();
        ShowActiveContent();
    }

    void BuildLayout()
    {
        _root.Children.Clear();
        _root.RowDefinitions.Clear();
        _root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _root.RowDefinitions.Add(new RowDefinition(new GridLength(1, GridUnitType.Star)));
        _root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        WorkspaceRegionChrome.DetachEditor(_contentHost);
        WorkspaceRegionChrome.DetachEditor(_emptyPrompt);
        WorkspaceRegionChrome.DetachEditor(_tabRail);

        var contentLayer = new Grid();
        contentLayer.Children.Add(_contentHost);
        contentLayer.Children.Add(_emptyPrompt);
        Grid.SetRow(contentLayer, 1);
        _root.Children.Add(contentLayer);

        Grid.SetRow(_tabRail, _group.TabStripPlacement == WorkspaceTabStripPlacement.Top ? 0 : 2);
        _tabRail.BorderThickness = _group.TabStripPlacement == WorkspaceTabStripPlacement.Top
            ? new Thickness(0, 0, 0, 1)
            : new Thickness(0, 1, 0, 0);
        _root.Children.Add(_tabRail);
    }

    void BuildTabs()
    {
        _tabStack.Children.Clear();
        foreach (var tab in _group.Tabs)
            _tabStack.Children.Add(CreateTabButton(tab));
        _tabStack.Children.Add(CreateAddButton());
    }

    void HookThemeApplied()
    {
        if (_themeAppliedHooked)
            return;

        AppThemeKeys.ThemeApplied += OnThemeApplied;
        _themeAppliedHooked = true;
    }

    void UnhookThemeApplied()
    {
        if (!_themeAppliedHooked)
            return;

        AppThemeKeys.ThemeApplied -= OnThemeApplied;
        _themeAppliedHooked = false;
    }

    void OnThemeApplied(object? sender, EventArgs e)
    {
        RefreshTabRailResources();
        BuildTabs();
    }

    void RefreshTabRailResources()
    {
        _tabRail.Background = ResourceBrush("ThemeControlMidBrush");
        _tabRail.BorderBrush = ResourceBrush("ThemeBorderLowBrush");
    }

    Button CreateTabButton(WorkspaceTabEntry tab)
    {
        var desc = WorkspaceEditorCatalog.Get(tab.Editor);
        var title = WorkspaceEditorTitles.HeaderTitle(desc);
        var button = CreateBaseButton(title);
        button.Tag = tab;
        button.Classes.Set("active", tab.Id == _group.ActiveTabId);
        if (tab.Id == _group.ActiveTabId)
        {
            button.Background = ResourceBrush("ThemeControlHighBrush");
            button.BorderBrush = ResourceBrush("ThemeAccentBrush");
        }
        ToolTip.SetTip(button, title);

        button.Click += (_, e) =>
        {
            e.Handled = true;
            SelectTab(tab);
        };

        button.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton != MouseButton.Right)
                return;

            e.Handled = true;
            ShowTabContextMenu(button, tab);
        };

        return button;
    }

    Button CreateAddButton()
    {
        var button = CreateBaseButton("+");
        button.MinWidth = 30;
        ToolTip.SetTip(button, "Add panel");

        button.Click += (_, e) =>
        {
            e.Handled = true;
            ShowAddMenu(button);
        };

        button.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton != MouseButton.Right)
                return;

            e.Handled = true;
            ShowPlacementMenu(button);
        };

        return button;
    }

    static Button CreateBaseButton(string text) =>
        new()
        {
            Content = text,
            Padding = new Thickness(8, 2),
            MinHeight = 26,
            FontSize = 11,
            Focusable = false,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

    static IBrush? ResourceBrush(string key) =>
        Application.Current?.FindResource(key) as IBrush;

    void ShowAddMenu(Control anchor)
    {
        var menu = QuickAccessPanelPicker.BuildMenu(editorId =>
        {
            var tab = new WorkspaceTabEntry { Editor = editorId };
            _group.Tabs.Add(tab);
            _group.ActiveTabId = tab.Id;
            _persist();
            Build();
            _activeEditorsChanged();
        });
        menu.Open(anchor);
    }

    void ShowTabContextMenu(Control anchor, WorkspaceTabEntry tab)
    {
        var menu = BuildPlacementMenu();
        menu.Items.Add(new Separator());
        var remove = new MenuItem { Header = "Remove tab" };
        remove.Click += (_, _) => RemoveTab(tab);
        menu.Items.Add(remove);
        menu.Open(anchor);
    }

    void ShowPlacementMenu(Control anchor)
    {
        var menu = BuildPlacementMenu();
        menu.Open(anchor);
    }

    ContextMenu BuildPlacementMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(MakePlacementItem("Tabs on top", WorkspaceTabStripPlacement.Top));
        menu.Items.Add(MakePlacementItem("Tabs on bottom", WorkspaceTabStripPlacement.Bottom));
        return menu;
    }

    MenuItem MakePlacementItem(string header, WorkspaceTabStripPlacement placement)
    {
        var item = new MenuItem
        {
            Header = _group.TabStripPlacement == placement ? $"[x] {header}" : header,
        };
        item.Click += (_, _) =>
        {
            if (_group.TabStripPlacement == placement)
                return;

            _group.TabStripPlacement = placement;
            _persist();
            Build();
        };
        return item;
    }

    void SelectTab(WorkspaceTabEntry tab)
    {
        if (_group.ActiveTabId == tab.Id)
            return;

        _group.ActiveTabId = tab.Id;
        _persist();
        Build();
        _activeEditorsChanged();
    }

    void RemoveTab(WorkspaceTabEntry tab)
    {
        var wasActive = _group.ActiveTabId == tab.Id;
        if (wasActive)
            ReleaseActiveEditor();

        _group.Tabs.RemoveAll(t => t.Id == tab.Id);
        _factory.Remove(tab);
        EnsureValidActiveTab();
        _persist();
        Build();
        _activeEditorsChanged();
    }

    void EnsureValidActiveTab()
    {
        if (_group.Tabs.Any(t => t.Id == _group.ActiveTabId))
            return;

        _group.ActiveTabId = _group.Tabs.FirstOrDefault()?.Id ?? Guid.Empty;
    }

    void ShowActiveContent()
    {
        var active = _group.Tabs.FirstOrDefault(t => t.Id == _group.ActiveTabId);
        _emptyPrompt.IsVisible = active is null;

        if (active is null)
        {
            ReleaseActiveEditor();
            return;
        }

        if (_activeEditorId == active.Id && _activeEditor is not null)
        {
            if (ReferenceEquals(_contentHost.Content, _activeEditor))
                return;

            AttachEditor(active, _activeEditor);
            return;
        }

        ReleaseActiveEditor();

        var editor = _factory.GetOrCreate(active);
        AttachEditor(active, editor);
    }

    void AttachEditor(WorkspaceTabEntry active, Control editor)
    {
        WorkspaceRegionChrome.DetachEditor(editor);
        _contentHost.Content = editor;
        _activeEditorId = active.Id;
        _activeEditor = editor;
        WorkspaceEditorFactory.SetActivation(editor, true);

        if (editor is RenderControl viewer)
            viewer.TryLoadProgramIfVisible();
    }

    void UpdateTitle()
    {
        var active = _group.Tabs.FirstOrDefault(t => t.Id == _group.ActiveTabId);
        if (active is null)
        {
            var groupDesc = WorkspaceEditorCatalog.Get(WorkspaceEditorId.TabGroup);
            _titleChanged(WorkspaceEditorTitles.HeaderTitle(groupDesc));
            return;
        }

        var desc = WorkspaceEditorCatalog.Get(active.Editor);
        _titleChanged(WorkspaceEditorTitles.HeaderTitle(desc));
    }
}
