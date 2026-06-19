using Avalonia.Controls;
using CNC.App;
using CNC.Controls.Avalonia.Controls;

namespace CNC.Platform.Tests;

public class PopupKeyboardTests
{
    [Fact]
    public void BaseConfig_uses_two_click_popup_keyboard_by_default()
    {
        Assert.Equal(PopupKeyboardTrigger.TwoClick, new BaseConfig().PopupKeyboardTrigger);
    }

    [Fact]
    public void TextBoxKeyboardEditor_inserts_at_caret()
    {
        var textBox = new TextBox { Text = "ac", CaretIndex = 1, SelectionStart = 1, SelectionEnd = 1 };

        TextBoxKeyboardEditor.Apply(textBox, PopupKeyboardAction.Insert("b"));

        Assert.Equal("abc", textBox.Text);
        Assert.Equal(2, textBox.CaretIndex);
    }

    [Fact]
    public void TextBoxKeyboardEditor_replaces_selection()
    {
        var textBox = new TextBox { Text = "axc", CaretIndex = 1, SelectionStart = 1, SelectionEnd = 2 };

        TextBoxKeyboardEditor.Apply(textBox, PopupKeyboardAction.Insert("b"));

        Assert.Equal("abc", textBox.Text);
        Assert.Equal(2, textBox.CaretIndex);
    }

    [Fact]
    public void TextBoxKeyboardEditor_backspace_removes_previous_character()
    {
        var textBox = new TextBox { Text = "abc", CaretIndex = 2, SelectionStart = 2, SelectionEnd = 2 };

        TextBoxKeyboardEditor.Apply(textBox, PopupKeyboardAction.Backspace);

        Assert.Equal("ac", textBox.Text);
        Assert.Equal(1, textBox.CaretIndex);
    }

    [Fact]
    public void TextBoxKeyboardEditor_enter_requests_close()
    {
        var textBox = new TextBox { Text = "abc" };

        var shouldClose = TextBoxKeyboardEditor.Apply(textBox, PopupKeyboardAction.Enter);

        Assert.True(shouldClose);
    }

    [Theory]
    [InlineData(PopupKeyboardTrigger.Off, 1, false)]
    [InlineData(PopupKeyboardTrigger.OneClick, 1, true)]
    [InlineData(PopupKeyboardTrigger.TwoClick, 1, false)]
    [InlineData(PopupKeyboardTrigger.TwoClick, 2, true)]
    public void PopupKeyboardService_matches_configured_click_trigger(
        PopupKeyboardTrigger trigger,
        int clickCount,
        bool expected)
    {
        var previous = PopupKeyboardService.TriggerClickCount;
        PopupKeyboardService.TriggerClickCount = () => (int)trigger;

        try
        {
            Assert.Equal(expected, PopupKeyboardService.ShouldOpenForClickCount(clickCount));
        }
        finally
        {
            PopupKeyboardService.TriggerClickCount = previous;
        }
    }

    [Fact]
    public void PopupKeyboardService_does_not_show_when_trigger_is_off()
    {
        var previous = PopupKeyboardService.TriggerClickCount;
        PopupKeyboardService.TriggerClickCount = () => (int)PopupKeyboardTrigger.Off;

        try
        {
            Assert.False(PopupKeyboardService.TryShowFor(new TextBox(), 1));
        }
        finally
        {
            PopupKeyboardService.TriggerClickCount = previous;
        }
    }
}
