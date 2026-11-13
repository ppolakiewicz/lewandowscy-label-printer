using LabelPrinter.Core.Presentation;

namespace LabelPrinter.Core.Tests.Presentation;

public class CopiesInputTests
{
    [Theory]
    [InlineData("1", 1)]
    [InlineData(" 12 ", 12)]
    [InlineData("999", 999)]
    [InlineData("007", 7)]
    public void TryParse_AcceptsValidCounts(string text, int expected)
    {
        Assert.True(CopiesInput.TryParse(text, out var copies));
        Assert.Equal(expected, copies);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("1000")]
    [InlineData("-1")]
    [InlineData("+2")]
    [InlineData("2,5")]
    [InlineData("dwa")]
    [InlineData("１２")]
    public void TryParse_RejectsInvalidCounts(string? text)
    {
        Assert.False(CopiesInput.TryParse(text, out _));
    }

    [Theory]
    [InlineData("5", 1, 6)]
    [InlineData("5", -1, 4)]
    [InlineData("1", -1, 1)]
    [InlineData("999", 1, 999)]
    [InlineData("abc", 1, 2)]
    [InlineData("", -1, 1)]
    public void Step_StaysInRange(string text, int delta, int expected)
    {
        Assert.Equal(expected, CopiesInput.Step(text, delta));
    }
}
