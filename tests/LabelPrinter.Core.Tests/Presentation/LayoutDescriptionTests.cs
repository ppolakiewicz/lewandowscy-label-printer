using LabelPrinter.Core.Layout;
using LabelPrinter.Core.Presentation;

namespace LabelPrinter.Core.Tests.Presentation;

public class LayoutDescriptionTests
{
    private static LabelLayout Layout(int lines, double size = 36, bool overflow = false, bool wordBroken = false, bool auto = true) => new()
    {
        WidthDots = 400,
        HeightDots = 240,
        Font = new FontSpec("Arial", size, false),
        LineHeight = size,
        Lines = Enumerable.Range(0, lines).Select(i => new LayoutLine("x", 0, i * size, 10)).ToList(),
        Overflow = overflow,
        WordBroken = wordBroken,
        AutoSized = auto,
    };

    [Fact]
    public void Empty_AsksForText()
    {
        var summary = LayoutDescription.Describe(Layout(0));
        Assert.Contains("wpisz tekst", summary.Info);
        Assert.Null(summary.Warning);
        Assert.False(summary.IsSerious);
    }

    [Fact]
    public void Normal_ShowsSizeInMillimetres()
    {
        var summary = LayoutDescription.Describe(Layout(2, size: 36));
        Assert.Equal("Czcionka 4,5 mm (dobrana automatycznie), linijek: 2", summary.Info);
        Assert.Null(summary.Warning);
    }

    [Fact]
    public void FixedSize_SaysManual()
    {
        Assert.Contains("ustawiona ręcznie", LayoutDescription.Describe(Layout(1, auto: false)).Info);
    }

    [Fact]
    public void Overflow_IsSerious()
    {
        var auto = LayoutDescription.Describe(Layout(3, overflow: true));
        Assert.Equal(LayoutDescription.OverflowAuto, auto.Warning);
        Assert.True(auto.IsSerious);

        var fixedSize = LayoutDescription.Describe(Layout(3, overflow: true, auto: false));
        Assert.Equal(LayoutDescription.OverflowFixed, fixedSize.Warning);
        Assert.True(fixedSize.IsSerious);
    }

    [Fact]
    public void WordBroken_IsAWarning()
    {
        var summary = LayoutDescription.Describe(Layout(2, wordBroken: true));
        Assert.Equal(LayoutDescription.WordBroken, summary.Warning);
        Assert.False(summary.IsSerious);
    }
}
