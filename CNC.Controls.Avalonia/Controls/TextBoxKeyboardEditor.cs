using Avalonia.Controls;

namespace CNC.Controls.Avalonia.Controls;

public static class TextBoxKeyboardEditor
{
    public static bool Apply(TextBox target, PopupKeyboardAction action)
    {
        if (target.IsReadOnly || !target.IsEnabled)
            return false;

        switch (action.Kind)
        {
            case PopupKeyboardActionKind.InsertText:
                InsertText(target, action.Text);
                return false;
            case PopupKeyboardActionKind.Backspace:
                Backspace(target);
                return false;
            case PopupKeyboardActionKind.Clear:
                target.Text = string.Empty;
                target.CaretIndex = 0;
                target.SelectionStart = 0;
                target.SelectionEnd = 0;
                return false;
            case PopupKeyboardActionKind.Enter:
                if (target is NumericTextBox numeric)
                    numeric.CommitText();
                return true;
            case PopupKeyboardActionKind.Escape:
                return true;
            default:
                return false;
        }
    }

    static void InsertText(TextBox target, string value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        var text = target.Text ?? string.Empty;
        var start = Math.Clamp(Math.Min(target.SelectionStart, target.SelectionEnd), 0, text.Length);
        var end = Math.Clamp(Math.Max(target.SelectionStart, target.SelectionEnd), 0, text.Length);

        target.Text = text.Remove(start, end - start).Insert(start, value);
        var caret = start + value.Length;
        target.CaretIndex = caret;
        target.SelectionStart = caret;
        target.SelectionEnd = caret;
    }

    static void Backspace(TextBox target)
    {
        var text = target.Text ?? string.Empty;
        var start = Math.Clamp(Math.Min(target.SelectionStart, target.SelectionEnd), 0, text.Length);
        var end = Math.Clamp(Math.Max(target.SelectionStart, target.SelectionEnd), 0, text.Length);

        if (end > start)
        {
            target.Text = text.Remove(start, end - start);
            target.CaretIndex = start;
            target.SelectionStart = start;
            target.SelectionEnd = start;
            return;
        }

        if (start == 0)
            return;

        target.Text = text.Remove(start - 1, 1);
        var caret = start - 1;
        target.CaretIndex = caret;
        target.SelectionStart = caret;
        target.SelectionEnd = caret;
    }
}
