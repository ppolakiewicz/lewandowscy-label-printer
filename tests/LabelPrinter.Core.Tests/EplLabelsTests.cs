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

        Assert.StartsWith("\nQ240,24\nq400\nS2\nD8\nZT\nR0,0\nN\nGW0,0,50,240,", job);
        Assert.EndsWith("\nP3\n", job);
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
