namespace CNC.Controls.Avalonia.Controls;

public enum PopupKeyboardActionKind
{
    InsertText,
    Backspace,
    Clear,
    Enter,
    Escape,
}

public sealed record PopupKeyboardAction(PopupKeyboardActionKind Kind, string Text = "")
{
    public static PopupKeyboardAction Insert(string text) => new(PopupKeyboardActionKind.InsertText, text);
    public static PopupKeyboardAction Backspace { get; } = new(PopupKeyboardActionKind.Backspace);
    public static PopupKeyboardAction Clear { get; } = new(PopupKeyboardActionKind.Clear);
    public static PopupKeyboardAction Enter { get; } = new(PopupKeyboardActionKind.Enter);
    public static PopupKeyboardAction Escape { get; } = new(PopupKeyboardActionKind.Escape);
}
