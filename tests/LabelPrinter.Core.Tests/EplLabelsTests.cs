using System.Text;
using LabelPrinter.Core.Epl;
using LabelPrinter.Core.Graphics;

namespace LabelPrinter.Core.Tests;

public class EplLabelsTests
{
    private static string AsText(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    [Fact]
    public void Image_DefaultConfig_Uses50x30mmLabelAnd3mmGap()
    {
        var job = AsText(EplLabels.Image(new PrinterConfig(), new MonoBitmap(400, 240), copies: 3));

        Assert.StartsWith("\nQ240,24\nq832\nS2\nD8\nZT\nR216,0\nN\nGW0,0,50,240,", job);
        Assert.EndsWith("\nP3\n", job);
    }

    [Theory]
    [InlineData(50, 104, 216)]  // 50 mm label on the LP2844: print from 27 mm to 77 mm
    [InlineData(100, 150, 200)] // 100 mm label on a 150 mm head: print from 25 mm to 125 mm
    [InlineData(104, 104, 0)]   // full-width label
    public void Image_CentersPrintAreaOnTheHead(double labelMm, double headMm, int marginDots)
    {
        var config = new PrinterConfig { LabelWidthMm = labelMm, PrintWidthMm = headMm };
        var job = AsText(EplLabels.Image(config, new MonoBitmap(config.LabelWidthDots, 240), copies: 1));

        Assert.Contains($"\nq{config.Normalized().PrintWidthDots}\n", job);
        Assert.Contains($"\nR{marginDots},0\nN\n", job);
    }

    [Theory]
    [InlineData(RollGuide.Center, 216)]    // LP2844: 27-77 mm
    [InlineData(RollGuide.LeftEdge, 0)]    // 0-50 mm
    [InlineData(RollGuide.RightEdge, 432)] // 54-104 mm
    public void Image_MovesPrintAreaToTheRoll(RollGuide guide, int marginDots)
    {
        var config = new PrinterConfig { RollGuide = guide };

        var label = AsText(EplLabels.Image(config, new MonoBitmap(400, 240), copies: 1));
        var test = AsText(EplLabels.TestLabel(config));

        Assert.Contains($"\nq832\n", label);
        Assert.Contains($"\nR{marginDots},0\nN\n", label);
        Assert.Contains($"\nR{marginDots},0\nN\n", test);
    }

    [Fact]
    public void Normalized_UnknownRollGuide_MeansCenter()
    {
        var c = new PrinterConfig { RollGuide = (RollGuide)7 }.Normalized();

        Assert.Equal(RollGuide.Center, c.RollGuide);
        Assert.Equal(216, c.LeftMarginDots);
    }

    [Fact]
    public void TestLabel_UsesTheSameCenteredPrintArea()
    {
        var job = AsText(EplLabels.TestLabel(new PrinterConfig()));

        Assert.Contains("\nR216,0\nN\n", job);
    }

    [Fact]
    public void Normalized_LabelNotWiderThanPrintWidth()
    {
        var c = new PrinterConfig { LabelWidthMm = 80, PrintWidthMm = 56 }.Normalized();

        Assert.Equal(56, c.LabelWidthMm);
        Assert.Equal(0, c.LeftMarginDots);
    }

    [Fact]
    public void Image_ClampsCopies()
    {
        var job = AsText(EplLabels.Image(new PrinterConfig(), new MonoBitmap(400, 240), copies: 0));

        Assert.EndsWith("\nP1\n", job);
    }

    [Fact]
    public void Image_AppliesOffsetToTheBitmap()
    {
        var config = new PrinterConfig { OffsetXMm = 1 }; // 8 dots
        var image = new MonoBitmap(400, 240);
        image[0, 0] = true;

        var job = EplLabels.Image(config, image, copies: 1);

        int dataStart = AsText(job).IndexOf("GW0,0,50,240,", StringComparison.Ordinal) + "GW0,0,50,240,".Length;
        Assert.Equal(0xFF, job[dataStart]);     // dots 0-7 white
        Assert.Equal(0x7F, job[dataStart + 1]); // dot 8 black
    }

    [Fact]
    public void TestLabel_ContainsFrameGraphicTextAndSinglePrint()
    {
        var job = AsText(EplLabels.TestLabel(new PrinterConfig { Darkness = 11, Speed = 3 }));

        Assert.Contains("GW0,0,50,240,", job);
        Assert.Contains("\"TEST ETYKIETY\"", job);
        Assert.Contains("D11 S3 X0 Y0", job);
        Assert.EndsWith("\nP1\n", job);
    }

    [Fact]
    public void TestPattern_HasFrameOnTheLabelEdge()
    {
        var bmp = EplLabels.TestPattern(new PrinterConfig());

        Assert.Equal(400, bmp.Width);
        Assert.Equal(240, bmp.Height);
        Assert.True(bmp[0, 120]);
        Assert.True(bmp[399, 120]);
        Assert.True(bmp[200, 239]);
        Assert.False(bmp[200, 120]);
    }

    [Fact]
    public void Normalized_ClampsOutOfRangeValues()
    {
        var c = new PrinterConfig { Darkness = 20, Speed = 0, OffsetXMm = 50, LabelWidthMm = double.NaN }.Normalized();

        Assert.Equal(15, c.Darkness);
        Assert.Equal(1, c.Speed);
        Assert.Equal(10, c.OffsetXMm);
        Assert.Equal(50, c.LabelWidthMm);
    }
}
