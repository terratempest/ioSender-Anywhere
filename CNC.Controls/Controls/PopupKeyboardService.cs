using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace CNC.Controls.Avalonia.Controls;

public static class PopupKeyboardService
{
    static readonly ConditionalWeakTable<Window, PopupKeyboardSession> Sessions = new();

    public static event EventHandler<TextBox>? PopupClosed;

    public static Func<int> TriggerClickCount { get; set; } = () => 2;

    public static bool IsPopupOpenFor(TextBox target)
    {
        var topLevel = TopLevel.GetTopLevel(target);
        if (topLevel is not Window window || !Sessions.TryGetValue(window, out var session))
            return false;

        return session.IsPopupOpenFor(target);
    }

    public static void Attach(Window window)
    {
        if (Sessions.TryGetValue(window, out _))
            return;

        var session = new PopupKeyboardSession(window);
        Sessions.Add(window, session);
    }

    sealed class PopupKeyboardSession
    {
        readonly Window _window;
        PopupKeyboardWindow? _popupWindow;
        TextBox? _target;

        public PopupKeyboardSession(Window window)
        {
            _window = window;
            _window.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
            _window.AddHandler(InputElement.GotFocusEvent, OnGotFocus, RoutingStrategies.Tunnel);
            _window.Closed += OnWindowClosed;
        }

        void OnWindowClosed(object? sender, EventArgs e)
        {
            ClosePopup();
            _window.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
            _window.RemoveHandler(InputElement.GotFocusEvent, OnGotFocus);
            _window.Closed -= OnWindowClosed;
        }

        void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            ClosePopupOnClickAway(e.Source);

            var triggerClickCount = TriggerClickCount();
            if (triggerClickCount <= 0 || e.ClickCount != triggerClickCount)
                return;

            var textBox = FindTextBox(e.Source);
            if (textBox is null || textBox.IsReadOnly || !textBox.IsEnabled)
                return;

            var layout = ResolveLayout(textBox);
            if (layout == PopupKeyboardLayout.None)
                return;
            if (layout == PopupKeyboardLayout.Default)
                layout = PopupKeyboardLayout.Regular;

            ShowPopup(textBox, layout);
            e.Handled = true;
        }

        void OnGotFocus(object? sender, FocusChangedEventArgs e)
        {
            var textBox = FindTextBox(e.Source);
            if (textBox is null || textBox.IsReadOnly || !textBox.IsEnabled)
                return;
            if (ResolveLayout(textBox) == PopupKeyboardLayout.None)
                return;

            PopupKeyboardTarget.Current = textBox;
        }

        void ShowPopup(TextBox target, PopupKeyboardLayout layout)
        {
            ClosePopup();
            target.Focus();
            _target = target;
            _popupWindow = new PopupKeyboardWindow(target, layout);
            _popupWindow.Closed += OnPopupClosed;
            _popupWindow.Show(_window);
            _popupWindow.PlaceNearTarget();
        }

        void OnPopupClosed(object? sender, EventArgs e)
        {
            if (sender is not PopupKeyboardWindow closedWindow || !ReferenceEquals(closedWindow, _popupWindow))
                return;

            var target = _target;
            closedWindow.Closed -= OnPopupClosed;
            _popupWindow = null;
            _target = null;
            RaisePopupClosed(target);
        }

        void ClosePopupOnClickAway(object? source)
        {
            if (_popupWindow is null || _target is null)
                return;
            if (IsWithin(source, _target))
                return;

            ClosePopup();
        }

        void ClosePopup()
        {
            var target = _target;
            if (_popupWindow is not null)
            {
                _popupWindow.Closed -= OnPopupClosed;
                _popupWindow.Close();
            }

            _popupWindow = null;
            _target = null;
            RaisePopupClosed(target);
        }

        public bool IsPopupOpenFor(TextBox target) =>
            _popupWindow is not null && ReferenceEquals(_target, target);
    }

    static void RaisePopupClosed(TextBox? target)
    {
        if (target is not null)
            PopupClosed?.Invoke(null, target);
    }

    static TextBox? FindTextBox(object? source)
    {
        if (source is TextBox textBox)
            return textBox;
        return source is global::Avalonia.Visual visual
            ? visual.FindAncestorOfType<TextBox>()
            : null;
    }

    static bool IsWithin(object? source, Control target)
    {
        if (ReferenceEquals(source, target))
            return true;
        return source is global::Avalonia.Visual visual
            && visual.GetVisualAncestors().OfType<Control>().Any(control => ReferenceEquals(control, target));
    }

    static PopupKeyboardLayout ResolveLayout(TextBox textBox)
    {
        var layout = PopupKeyboard.GetLayout(textBox);
        if (layout != PopupKeyboardLayout.Default)
            return layout;
        return textBox is NumericTextBox
            ? PopupKeyboardLayout.Numeric
            : PopupKeyboardLayout.Regular;
    }
}
