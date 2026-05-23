using System.Diagnostics;

namespace CNC.Core;

public static class GrblUi
{
    public static System.Action<string, string>? NotifyError { get; set; }
    public static Func<string, string, bool>? Confirm { get; set; }
    public static System.Action<string>? SetClipboardText { get; set; }

    public static void ShowError(string message, string title = "ioSender")
    {
        if (NotifyError != null)
            NotifyError(message, title);
        else
            Debug.WriteLine($"{title}: {message}");
    }

    public static bool AskYesNo(string message, string title = "ioSender")
    {
        if (Confirm != null)
            return Confirm(message, title);
        Debug.WriteLine($"{title} [Yes assumed]: {message}");
        return true;
    }

    public static void CopyToClipboard(string text)
    {
        if (SetClipboardText != null)
            SetClipboardText(text);
        else
            Debug.WriteLine($"Clipboard: {text}");
    }
}
