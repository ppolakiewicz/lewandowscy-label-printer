using LabelPrinter.Core.Layout;
using LabelPrinter.Core.Presentation;
using LabelPrinter.Core.Storage;

namespace LabelPrinter.Core.Tests.Presentation;

public class LabelTextFormatterTests
{
    private static SavedLabel Saved(LabelContent content, int useCount = 0) =>
        new() { Id = 1, Content = content, UseCount = useCount, CreatedUtc = DateTime.UtcNow };

    [Fact]
    public void OneLine_JoinsManualLines()
    {
        var content = new LabelContent { Mode = LayoutMode.Manual, Lines = ["Ala", " ", "ma kota"] };
        Assert.Equal("Ala ⏎ ma kota", LabelTextFormatter.OneLine(content));
    }

    [Fact]
    public void OneLine_AutoTextUnchanged()
    {
        Assert.Equal("Ala ma kota", LabelTextFormatter.OneLine(LabelContent.FromText("Ala ma kota")));
    }

    [Fact]
    public void ModeName_IsPolish()
    {
        Assert.Equal("Zawijanie", LabelTextFormatter.ModeName(LayoutMode.Auto));
        Assert.Equal("Osobne linijki", LabelTextFormatter.ModeName(LayoutMode.Manual));
    }

    [Fact]
    public void Details_DefaultsShowOnlyMode()
    {
        Assert.Equal("Zawijanie", LabelTextFormatter.Details(Saved(LabelContent.FromText("x"))));
    }

    [Fact]
    public void Details_ListsNonDefaultSettings()
    {
        var content = new LabelContent
        {
            Mode = LayoutMode.Manual,
            Lines = ["a", "b"],
            Alignment = LabelAlignment.Left,
            Bold = true,
            FontSizeDots = 36,
            FontFamily = "Verdana",
        };
        Assert.Equal(
            "Osobne linijki · do lewej · pogrubienie · 4,5 mm · Verdana · użyto 3×",
            LabelTextFormatter.Details(Saved(content, useCount: 3)));
    }

    [Fact]
    public void FormatLocal_NullIsEmpty()
    {
        Assert.Equal("", LabelTextFormatter.FormatLocal(null));
    }

    [Fact]
    public void FormatLocal_ConvertsToLocalTime()
    {
        var utc = new DateTime(2026, 3, 1, 10, 5, 0, DateTimeKind.Utc);
        Assert.Equal(
            utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture),
            LabelTextFormatter.FormatLocal(utc));
    }

    [Fact]
    public void Shorten_KeepsShortTexts()
    {
        Assert.Equal("Ala ⏎ kot", LabelTextFormatter.Shorten("Ala\nkot"));
    }

    [Fact]
    public void Shorten_CutsLongTexts()
    {
        var result = LabelTextFormatter.Shorten(new string('a', 100), 10);
        Assert.Equal(10, result.Length);
        Assert.EndsWith("…", result);
    }
}
