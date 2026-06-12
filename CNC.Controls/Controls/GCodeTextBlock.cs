using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace CNC.Controls.Avalonia.Controls;

public sealed class GCodeTextBlock : TextBlock
{
    static readonly IBrush DefaultBrush = Brush.Parse("#D4D4D4");
    static readonly IBrush CompletedBrush = Brush.Parse("#808080");
    static readonly IBrush CommentBrush = Brush.Parse("#6A9955");
    static readonly IBrush GCodeBrush = Brush.Parse("#4FC1FF");
    static readonly IBrush MCodeBrush = Brush.Parse("#C586F7");
    static readonly IBrush MacroBrush = Brush.Parse("#4EC9B0");
    static readonly IBrush AddressBrush = Brush.Parse("#FFB86C");

    public static readonly StyledProperty<string?> SourceTextProperty =
        AvaloniaProperty.Register<GCodeTextBlock, string?>(nameof(SourceText));

    public static readonly StyledProperty<bool> IsCompletedProperty =
        AvaloniaProperty.Register<GCodeTextBlock, bool>(nameof(IsCompleted));

    public string? SourceText
    {
        get => GetValue(SourceTextProperty);
        set => SetValue(SourceTextProperty, value);
    }

    public bool IsCompleted
    {
        get => GetValue(IsCompletedProperty);
        set => SetValue(IsCompletedProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SourceTextProperty || change.Property == IsCompletedProperty)
            UpdateInlines();
    }

    void UpdateInlines()
    {
        Inlines ??= new InlineCollection();
        Inlines.Clear();

        foreach (var run in GCodeSyntaxHighlighter.Highlight(SourceText, IsCompleted))
        {
            Inlines.Add(new Run(run.Text)
            {
                Foreground = IsCompleted ? CompletedBrush : BrushFor(run.Kind),
                FontStyle = run.Kind == GCodeSyntaxKind.Comment && !IsCompleted ? FontStyle.Italic : FontStyle.Normal,
                FontWeight = !IsCompleted && IsBold(run.Kind) ? FontWeight.Bold : FontWeight.Normal
            });
        }
    }

    static bool IsBold(GCodeSyntaxKind kind) =>
        kind is GCodeSyntaxKind.GCode or GCodeSyntaxKind.MCode or GCodeSyntaxKind.Macro or GCodeSyntaxKind.Address;

    static IBrush BrushFor(GCodeSyntaxKind kind) => kind switch
    {
        GCodeSyntaxKind.Comment => CommentBrush,
        GCodeSyntaxKind.GCode => GCodeBrush,
        GCodeSyntaxKind.MCode => MCodeBrush,
        GCodeSyntaxKind.Macro => MacroBrush,
        GCodeSyntaxKind.Address => AddressBrush,
        _ => DefaultBrush
    };
}
