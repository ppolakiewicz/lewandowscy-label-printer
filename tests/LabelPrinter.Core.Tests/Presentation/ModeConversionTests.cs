using LabelPrinter.Core.Presentation;

namespace LabelPrinter.Core.Tests.Presentation;

public class ModeConversionTests
{
    [Fact]
    public void ToAutoText_JoinsNonEmptyLinesWithSpaces()
    {
        Assert.Equal("Ala ma kota", ModeConversion.ToAutoText([" Ala ", "", "ma kota", null]));
    }

    [Fact]
    public void ToAutoText_NoSpaceAfterHyphenOrUnderscore()
    {
        Assert.Equal("biało-czerwony_x y", ModeConversion.ToAutoText(["biało-", "czerwony_", "x", "y"]));
    }

    [Fact]
    public void ToAutoText_EmptyLinesGiveEmptyText()
    {
        Assert.Equal("", ModeConversion.ToAutoText(["", "  ", null]));
    }

    [Fact]
    public void ToManualLines_UsesWrappedLinesWhenTheyMatch()
    {
        var lines = ModeConversion.ToManualLines("Ala  ma kota", ["Ala ma", "kota"]);
        Assert.Equal(new[] { "Ala ma", "kota", "" }, lines);
    }

    [Fact]
    public void ToManualLines_UsesWrappedLinesSplitAfterHyphen()
    {
        var lines = ModeConversion.ToManualLines("biało-czerwony", ["biało-", "czerwony"]);
        Assert.Equal(new[] { "biało-", "czerwony", "" }, lines);
    }

    [Fact]
    public void ToManualLines_IgnoresStaleWrappedLines()
    {
        var lines = ModeConversion.ToManualLines("Nowy tekst", ["Stary", "tekst"]);
        Assert.Equal(new[] { "Nowy tekst", "", "" }, lines);
    }

    [Fact]
    public void ToManualLines_WithoutWrappedLinesPutsTextInFirstLine()
    {
        Assert.Equal(new[] { "abc", "", "" }, ModeConversion.ToManualLines(" abc ", null));
    }

    [Fact]
    public void ToManualLines_JoinsExtraLinesIntoTheLast()
    {
        var lines = ModeConversion.ToManualLines("a b c d", ["a", "b", "c", "d"]);
        Assert.Equal(new[] { "a", "b", "c d" }, lines);
    }

    [Fact]
    public void ToManualLines_EmptyTextGivesEmptyLines()
    {
        Assert.Equal(new[] { "", "", "" }, ModeConversion.ToManualLines("  ", ["x"]));
    }

    [Fact]
    public void RoundTrip_KeepsTheText()
    {
        var lines = ModeConversion.ToManualLines("Kawa-mielona 250 g", ["Kawa-", "mielona 250 g"]);
        Assert.Equal("Kawa-mielona 250 g", ModeConversion.ToAutoText(lines));
    }
}
