using Avalonia;
using Avalonia.Controls;

namespace CNC.Controls.Avalonia.Controls;

public static class PopupKeyboard
{
    public static readonly AttachedProperty<PopupKeyboardLayout> LayoutProperty =
        AvaloniaProperty.RegisterAttached<TextBox, PopupKeyboardLayout>(
            "Layout",
            typeof(PopupKeyboard),
            PopupKeyboardLayout.Default);

    public static PopupKeyboardLayout GetLayout(TextBox element) =>
        element.GetValue(LayoutProperty);

    public static void SetLayout(TextBox element, PopupKeyboardLayout value) =>
        element.SetValue(LayoutProperty, value);
}
