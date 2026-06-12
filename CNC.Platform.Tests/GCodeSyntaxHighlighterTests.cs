using CNC.Controls.Avalonia.Controls;

namespace CNC.Platform.Tests;

public class GCodeSyntaxHighlighterTests
{
    [Fact]
    public void Highlights_g_code_and_address_letters_without_number_coloring()
    {
        var runs = GCodeSyntaxHighlighter.Highlight("G02 X218.5 Y112.3 I5.0");

        AssertRun(runs, "G02", GCodeSyntaxKind.GCode);
        AssertRun(runs, "X", GCodeSyntaxKind.Address);
        AssertRun(runs, "218.5 ", GCodeSyntaxKind.Default);
        AssertRun(runs, "Y", GCodeSyntaxKind.Address);
        AssertRun(runs, "112.3 ", GCodeSyntaxKind.Default);
        AssertRun(runs, "I", GCodeSyntaxKind.Address);
        AssertRun(runs, "5.0", GCodeSyntaxKind.Default);
    }

    [Fact]
    public void Highlights_m_code_and_spindle_address()
    {
        var runs = GCodeSyntaxHighlighter.Highlight("M03 S12000");

        AssertRun(runs, "M03", GCodeSyntaxKind.MCode);
        AssertRun(runs, "S", GCodeSyntaxKind.Address);
        AssertRun(runs, "12000", GCodeSyntaxKind.Default);
    }

    [Fact]
    public void Highlights_parenthesized_comment()
    {
        var runs = GCodeSyntaxHighlighter.Highlight("(comment)");

        var run = Assert.Single(runs);
        Assert.Equal("(comment)", run.Text);
        Assert.Equal(GCodeSyntaxKind.Comment, run.Kind);
    }

    [Fact]
    public void Highlights_macro_words()
    {
        var runs = GCodeSyntaxHighlighter.Highlight("IF THEN CALL");

        AssertRun(runs, "IF", GCodeSyntaxKind.Macro);
        AssertRun(runs, "THEN", GCodeSyntaxKind.Macro);
        AssertRun(runs, "CALL", GCodeSyntaxKind.Macro);
    }

    [Fact]
    public void Highlights_case_insensitively()
    {
        var runs = GCodeSyntaxHighlighter.Highlight("g02 x1 m03 if");

        AssertRun(runs, "g02", GCodeSyntaxKind.GCode);
        AssertRun(runs, "x", GCodeSyntaxKind.Address);
        AssertRun(runs, "m03", GCodeSyntaxKind.MCode);
        AssertRun(runs, "if", GCodeSyntaxKind.Macro);
    }

    [Fact]
    public void Completed_line_returns_single_default_run()
    {
        var runs = GCodeSyntaxHighlighter.Highlight("G02 X1 (done)", isCompleted: true);

        var run = Assert.Single(runs);
        Assert.Equal("G02 X1 (done)", run.Text);
        Assert.Equal(GCodeSyntaxKind.Default, run.Kind);
    }

    static void AssertRun(IReadOnlyList<GCodeSyntaxRun> runs, string text, GCodeSyntaxKind kind)
    {
        Assert.Contains(runs, run => run.Text == text && run.Kind == kind);
    }
}
