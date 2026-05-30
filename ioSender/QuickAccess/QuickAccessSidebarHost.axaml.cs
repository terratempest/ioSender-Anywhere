using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using CNC.App;
using CNC.App.Workspace;
using CNC.Localization.Avalonia;
using ioSender.Workspace.Editors;

namespace ioSender.QuickAccess;

public partial class QuickAccessSidebarHost : UserControl
{
    public static readonly StyledProperty<QuickAccessSidebarDock> DockSideProperty =
        AvaloniaProperty.Register<QuickAccessSidebarHost, QuickAccessSidebarDock>(nameof(DockSide));

    readonly Dictionary<Guid, Button> _tabButtons = new();
    Button? _addButton;

    public event EventHandler? PanelCloseRequested;
    public event EventHandler<Size>? PanelResizeCompleted;
    public event EventHandler<QuickAccessTabEntry>? TabActivateRequested;
    public event EventHandler? AddTabRequested;
    public event EventHandler<QuickAccessTabEntry>? TabRemoveRequested;
    public event EventHandler<(QuickAccessTabEntry Tab, WorkspaceEditorId EditorId)>? TabEditorChangeRequested;

    public QuickAccessSidebarHost()
    {
        InitializeComponent();
        _addButton = CreateAddButton();
        PanelChrome.CloseRequested += (_, _) => PanelCloseRequested?.Invoke(this, EventArgs.Empty);
        PanelChrome.ResizeCompleted += (_, size) => PanelResizeCompleted?.Invoke(this, size);
        DockSideProperty.Changed.AddClassHandler<QuickAccessSidebarHost>((host, _) => host.UpdateFlyoutLayerSide());
        Loaded += (_, _) => UpdateFlyoutLayerSide();
    }

    public QuickAccessSidebarDock DockSide
    {
        get => GetValue(DockSideProperty);
        set => SetValue(DockSideProperty, value);
    }

    public QuickAccessPopupChrome PanelChromeControl => PanelChrome;

    public bool IsPanelOpen => PanelChrome.IsVisible;

    public void BindTabs(IReadOnlyList<QuickAccessTabEntry> tabs)
    {
        TabStack.Children.Clear();
        _tabButtons.Clear();

        foreach (var tab in tabs)
        {
            var button = CreateTabButton(tab);
            _tabButtons[tab.Id] = button;
            TabStack.Children.Add(button);
        }

        if (_addButton is not null)
            TabStack.Children.Add(_addButton);
    }

    public bool TryGetTabButton(Guid tabId, out Button? button) =>
        _tabButtons.TryGetValue(tabId, out button);

    public void ShowPanel(Button tabButton)
    {
        PositionPanel(tabButton);
        PanelChrome.IsVisible = true;
    }

    public void ClosePanel() => PanelChrome.IsVisible = false;

    void UpdateFlyoutLayerSide()
    {
        if (DockSide == QuickAccessSidebarDock.Right)
        {
            FlyoutLayer.HorizontalAlignment = HorizontalAlignment.Right;
            PanelChrome.HorizontalAlignment = HorizontalAlignment.Right;
        }
        else
        {
            FlyoutLayer.HorizontalAlignment = HorizontalAlignment.Left;
            PanelChrome.HorizontalAlignment = HorizontalAlignment.Left;
        }
    }

    void PositionPanel(Button tabButton)
    {
        var pos = tabButton.TranslatePoint(new Point(), this);
        var top = pos?.Y ?? 0;
        var rail = Bounds.Width > 1 ? Bounds.Width : 40;

        PanelChrome.Margin = DockSide == QuickAccessSidebarDock.Right
            ? new Thickness(0, top, rail, 0)
            : new Thickness(rail, top, 0, 0);
        PanelChrome.VerticalAlignment = VerticalAlignment.Top;
    }

    Button CreateAddButton()
    {
        var button = new Button
        {
            Classes = { "quickaccess-add" },
            Content = "+",
            Focusable = false,
        };
        ToolTip.SetTip(button, "Add panel");
        button.Click += (_, e) =>
        {
            e.Handled = true;
            AddTabRequested?.Invoke(this, EventArgs.Empty);
        };
        return button;
    }

    Button CreateTabButton(QuickAccessTabEntry tab)
    {
        var desc = WorkspaceEditorCatalog.Get(tab.EditorId);
        var title = Localize.T(desc.TitleKey, desc.TitleFallback);

        var label = new TextBlock
        {
            Text = title,
            TextAlignment = TextAlignment.Center,
            RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            RenderTransform = new RotateTransform(-90),
        };

        var button = new Button
        {
            Classes = { "quickaccess-tab" },
            Content = label,
            Tag = tab,
            Focusable = false,
        };
        ToolTip.SetTip(button, title);

        button.Click += (_, e) =>
        {
            e.Handled = true;
            TabActivateRequested?.Invoke(this, tab);
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

    void ShowTabContextMenu(Control anchor, QuickAccessTabEntry tab)
    {
        var menu = new ContextMenu();
        menu.Items.Add(QuickAccessPanelPicker.BuildChangePanelSubmenu(id =>
            TabEditorChangeRequested?.Invoke(this, (tab, id))));
        menu.Items.Add(new Separator());
        var remove = new MenuItem
        {
            Header = Localize.T("ioSender.quickaccess.removePanel", "Remove from sidebar"),
        };
        remove.Click += (_, _) => TabRemoveRequested?.Invoke(this, tab);
        menu.Items.Add(remove);
        menu.Open(anchor);
    }

    public void ShowAddPanelMenu(Action<WorkspaceEditorId> onSelected)
    {
        var menu = QuickAccessPanelPicker.BuildMenu(onSelected);
        if (_addButton is not null)
            menu.Open(_addButton);
    }
}
