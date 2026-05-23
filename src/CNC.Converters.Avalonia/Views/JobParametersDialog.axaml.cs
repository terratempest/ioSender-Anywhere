using System.IO;
using System.Xml.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using CNC.Core;

namespace CNC.Converters;

public partial class JobParametersDialog : Window
{
    const string Suffix = "Conversion.xml";

    public JobParametersDialog()
    {
        InitializeComponent();
    }

    public JobParametersDialog(JobParametersViewModel model) : this()
    {
        DataContext = model;
        Title = model.Profile + " conversion parameters";
        Opened += OnOpened;
    }

    void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is not JobParametersViewModel vm)
            return;

        try
        {
            var path = Core.Resources.ConfigPath + vm.Profile + Suffix;
            using var reader = new StreamReader(path);
            var settings = (JobParametersViewModel)new XmlSerializer(typeof(JobParametersViewModel)).Deserialize(reader)!;
            Copy.Properties(settings, vm);
        }
        catch
        {
        }
    }

    public bool SaveSettings()
    {
        if (DataContext is not JobParametersViewModel settings)
            return false;

        try
        {
            var path = Core.Resources.ConfigPath + settings.Profile + Suffix;
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            new XmlSerializer(typeof(JobParametersViewModel)).Serialize(fs, settings);
            return true;
        }
        catch (Exception ex)
        {
            var ok = new Button { Content = "OK", Width = 72 };
            var dlg = new Window
            {
                Title = "ioSender",
                Width = 360,
                SizeToContent = SizeToContent.Height,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Thickness(16),
                    Children =
                    {
                        new TextBlock { Text = ex.Message, TextWrapping = TextWrapping.Wrap },
                        ok
                    }
                }
            };
            ok.Click += (_, _) => dlg.Close();
            dlg.ShowDialog(this);
            return false;
        }
    }

    void OnOkClick(object? sender, RoutedEventArgs e)
    {
        SaveSettings();
        Close(true);
    }

    void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    public static bool Show(JobParametersViewModel model, Window? owner)
    {
        var dialog = new JobParametersDialog(model);
        if (owner == null)
        {
            dialog.Show();
            return true;
        }

        return dialog.ShowDialog<bool>(owner).GetAwaiter().GetResult();
    }
}
