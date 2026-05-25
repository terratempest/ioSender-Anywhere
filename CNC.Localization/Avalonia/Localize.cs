using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace CNC.Localization.Avalonia;

public static class Localize
{
    public static readonly AttachedProperty<string?> KeyProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("Key", typeof(Localize));

    public static readonly AttachedProperty<string?> FallbackProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("Fallback", typeof(Localize));

    static Localize()
    {
        KeyProperty.Changed.AddClassHandler<Control>(OnKeyChanged);
        LocalizedStrings.CultureChanged += (_, _) => RefreshAll();
    }

    private static readonly ConditionalWeakTable<Control, string> TrackedKeys = new();

    public static string? GetKey(AvaloniaObject element) => element.GetValue(KeyProperty);
    public static void SetKey(AvaloniaObject element, string? value) => element.SetValue(KeyProperty, value);

    public static string? GetFallback(AvaloniaObject element) => element.GetValue(FallbackProperty);
    public static void SetFallback(AvaloniaObject element, string? value) => element.SetValue(FallbackProperty, value);

    public static void Set(Control control, string key, string? fallback = null)
    {
        SetKey(control, key);
        if (fallback is not null)
            SetFallback(control, fallback);
        Apply(control);
    }

    private static void OnKeyChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is string key)
        {
            TrackedKeys.AddOrUpdate(control, key);
            Apply(control);
        }
    }

    private static void RefreshAll()
    {
        // Attached properties re-apply on next open; explicit ApplyLocalization covers proof screens.
    }

    public static void Apply(Control control)
    {
        var key = GetKey(control);
        if (string.IsNullOrEmpty(key))
            return;

        var fallback = GetFallback(control) ?? ReadCurrentText(control);
        var text = ResolveDisplayText(key, fallback);

        switch (control)
        {
            case MenuItem menuItem:
                menuItem.Header = text;
                break;
            case TabItem tabItem:
                tabItem.Header = text;
                break;
            case Window window:
                window.Title = text;
                break;
            case CheckBox checkBox:
                checkBox.Content = text;
                break;
            case RadioButton radioButton:
                radioButton.Content = text;
                break;
            case Button button:
                button.Content = text;
                ApplyToolTip(button, key);
                break;
            case TextBlock textBlock:
                textBlock.Text = text;
                break;
            case ContentControl contentControl:
                contentControl.Content = text;
                break;
        }
    }

    private static string? ReadCurrentText(Control control) => control switch
    {
        MenuItem menuItem => menuItem.Header as string,
        TabItem tabItem => tabItem.Header as string,
        Window window => window.Title,
        CheckBox checkBox => checkBox.Content as string,
        RadioButton radioButton => radioButton.Content as string,
        Button button => button.Content as string,
        TextBlock textBlock => textBlock.Text,
        ContentControl contentControl => contentControl.Content as string,
        _ => null,
    };

    public static string T(string key, string fallback) => LocalizedStrings.Get(key, fallback);

    private static string ResolveDisplayText(string key, string? fallback)
    {
        if (LocalizedStrings.TryGet($"{key}:Content", out var content))
            return content;

        return LocalizedStrings.Get(key, fallback);
    }

    private static void ApplyToolTip(Control control, string key)
    {
        if (!LocalizedStrings.TryGet($"{key}:ToolTip", out var tip))
            return;

        ToolTip.SetTip(control, tip);
    }
}
