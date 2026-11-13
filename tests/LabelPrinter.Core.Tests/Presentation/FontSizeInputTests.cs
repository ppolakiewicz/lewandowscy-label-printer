using LabelPrinter.Core.Presentation;

namespace LabelPrinter.Core.Tests.Presentation;

public class FontSizeInputTests
{
    [Theory]
    [InlineData("5", 5)]
    [InlineData("4,5", 4.5)]
    [InlineData("4.5", 4.5)]
    [InlineData(" 4,5 mm ", 4.5)]
    [InlineData("10MM", 10)]
    [InlineData("1", 1)]
    [InlineData("26", 26)]
    public void TryParseMm_AcceptsValidSizes(string text, double expected)
    {
        Assert.True(FontSizeInput.TryParseMm(text, out var mm));
        Assert.Equal(expected, mm, 6);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("abc")]
    [InlineData("0,5")]
    [InlineData("27")]
    [InlineData("-3")]
    [InlineData("1e1")]
    [InlineData("NaN")]
    [InlineData("4,5,6")]
    public void TryParseMm_RejectsInvalidSizes(string? text)
    {
        Assert.False(FontSizeInput.TryParseMm(text, out _));
    }

    [Fact]
    public void Conversions_UseEightDotsPerMm()
    {
        Assert.Equal(36, FontSizeInput.MmToDots(4.5));
        Assert.Equal(4.625, FontSizeInput.DotsToMm(37));
    }

    [Theory]
    [InlineData(4.625, "4,625")]
    [InlineData(5, "5")]
    [InlineData(4.5, "4,5")]
    public void FormatMm_UsesDecimalComma(double mm, string expected)
    {
        Assert.Equal(expected, FontSizeInput.FormatMm(mm));
    }

    [Theory]
    [InlineData(4.3, 4.5)]
    [InlineData(4.2, 4)]
    [InlineData(4.75, 5)]
    [InlineData(0.2, 1)]
    [InlineData(40, 26)]
    public void RoundToHalfMm_RoundsAndClamps(double mm, double expected)
    {
        Assert.Equal(expected, FontSizeInput.RoundToHalfMm(mm));
    }

    [Fact]
    public void FormattedValue_ParsesBackToTheSameSize()
    {
        var text = FontSizeInput.FormatMm(FontSizeInput.DotsToMm(37));
        Assert.True(FontSizeInput.TryParseMm(text, out var mm));
        Assert.Equal(37, FontSizeInput.MmToDots(mm), 6);
    }
}
