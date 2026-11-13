namespace LabelPrinter.Core.Tests;

public class PrinterConfigTests
{
    [Fact]
    public void SameSettings_EqualConfigs()
    {
        var a = new PrinterConfig { PrinterName = "Zebra", OffsetXMm = 1.5 };
        var b = new PrinterConfig { PrinterName = "Zebra", OffsetXMm = 1.5 };
        Assert.True(a.SameSettings(b));
    }

    [Theory]
    [InlineData(nameof(PrinterConfig.PrinterName))]
    [InlineData(nameof(PrinterConfig.LabelWidthMm))]
    [InlineData(nameof(PrinterConfig.LabelHeightMm))]
    [InlineData(nameof(PrinterConfig.GapMm))]
    [InlineData(nameof(PrinterConfig.OffsetXMm))]
    [InlineData(nameof(PrinterConfig.OffsetYMm))]
    [InlineData(nameof(PrinterConfig.Darkness))]
    [InlineData(nameof(PrinterConfig.Speed))]
    [InlineData(nameof(PrinterConfig.PrintTopFirst))]
    [InlineData(nameof(PrinterConfig.InvertGraphics))]
    public void SameSettings_DetectsEachChange(string property)
    {
        var a = new PrinterConfig();
        var b = new PrinterConfig();
        switch (property)
        {
            case nameof(PrinterConfig.PrinterName): b.PrinterName = "X"; break;
            case nameof(PrinterConfig.LabelWidthMm): b.LabelWidthMm = 60; break;
            case nameof(PrinterConfig.LabelHeightMm): b.LabelHeightMm = 40; break;
            case nameof(PrinterConfig.GapMm): b.GapMm = 2; break;
            case nameof(PrinterConfig.OffsetXMm): b.OffsetXMm = 1; break;
            case nameof(PrinterConfig.OffsetYMm): b.OffsetYMm = 1; break;
            case nameof(PrinterConfig.Darkness): b.Darkness = 3; break;
            case nameof(PrinterConfig.Speed): b.Speed = 4; break;
            case nameof(PrinterConfig.PrintTopFirst): b.PrintTopFirst = false; break;
            case nameof(PrinterConfig.InvertGraphics): b.InvertGraphics = true; break;
        }

        Assert.False(a.SameSettings(b));
    }
}
