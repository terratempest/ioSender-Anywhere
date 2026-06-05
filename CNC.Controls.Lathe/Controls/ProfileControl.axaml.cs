using System.Collections;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace CNC.Controls.Lathe;

public partial class ProfileControl : UserControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<ProfileControl, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<ProfileData?> SelectedProfileProperty =
        AvaloniaProperty.Register<ProfileControl, ProfileData?>(nameof(SelectedProfile));

    public ProfileControl()
    {
        InitializeComponent();
    }

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public ProfileData? SelectedProfile
    {
        get => GetValue(SelectedProfileProperty);
        set => SetValue(SelectedProfileProperty, value);
    }

    BaseViewModel? ViewModel => DataContext as BaseViewModel;

    void OnProfileMenuClick(object? sender, RoutedEventArgs e)
    {
        var vm = ViewModel;
        if (vm == null)
            return;

        var name = ProfileCombo.SelectedItem is ProfileData p ? p.Name?.Trim() : null;
        if (string.IsNullOrEmpty(name))
            name = vm.Profile?.Name ?? "Default";

        var canRename = vm.Profile != null && vm.Profile.Name == name;
        var canDelete = canRename && vm.Profiles.Count > 1;

        var menu = new ContextMenu
        {
            Items =
            {
                new MenuItem { Header = "New", Name = "mnuNew", IsEnabled = !canRename },
                new MenuItem { Header = "Rename", Name = "mnuRename", IsEnabled = canRename },
                new MenuItem { Header = "Delete", Name = "mnuDelete", IsEnabled = canDelete },
                new MenuItem { Header = "Save", Name = "mnuSave" },
                new Separator(),
                new MenuItem { Header = "Edit...", Name = "mnuEdit" }
            }
        };

        foreach (var item in menu.Items.OfType<MenuItem>().Where(i => i.Name != null))
            item.Click += OnProfileMenuItemClick;

        menu.Open(ProfileMenuBtn);
    }

    void OnProfileMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || ViewModel is not { } vm)
            return;

        var name = ProfileCombo.SelectedItem is ProfileData p ? p.Name?.Trim() : null;
        if (string.IsNullOrEmpty(name))
            name = "Default";

        switch (item.Name)
        {
            case "mnuNew":
                vm.Profile = vm.wz.Add(name);
                SelectedProfile = vm.Profile;
                vm.wz.SaveProfiles();
                break;

            case "mnuRename":
                if (vm.Profile != null)
                {
                    vm.wz.RenameProfile(vm.Profile, name);
                    vm.wz.SaveProfiles();
                    SelectedProfile = vm.Profile;
                }
                break;

            case "mnuDelete":
                if (vm.Profile != null && vm.wz.DeleteProfile(vm.Profile))
                {
                    vm.Profile = vm.Profiles.First();
                    SelectedProfile = vm.Profile;
                }
                break;

            case "mnuSave":
                vm.wz.SaveProfiles();
                break;

            case "mnuEdit":
                ProfileDialog.ShowFor(vm, this);
                if (vm.Profile != null)
                    SelectedProfile = vm.Profile;
                break;
        }
    }
}
