using System.Text;
using LabelPrinter.Core.Epl;
using LabelPrinter.Core.Graphics;

namespace LabelPrinter.Core.Tests;

public class EplWriterTests
{
    [Fact]
    public void Text_EscapesQuotesAndBackslashes()
    {
        var bytes = new EplWriter().Text(1, 2, 3, "a\"b\\c").ToArray();

        Assert.Equal("A1,2,0,3,1,1,N,\"a\\\"b\\\\c\"\n", Encoding.ASCII.GetString(bytes));
    }

    [Fact]
    public void Escape_ReplacesNonAscii()
    {
        Assert.Equal("???te", EplWriter.Escape("Żółte"));
    }

    [Fact]
    public void Graphic_WritesHeaderThenBinaryRowsBlackAsZero()
    {
        var bmp = new MonoBitmap(8, 2);
        bmp.FillRect(0, 0, 8, 2);

        var bytes = new EplWriter().Graphic(5, 6, bmp).ToArray();

        var expected = Encoding.ASCII.GetBytes("GW5,6,1,2,").Concat(new byte[] { 0x00, 0x00, (byte)'\n' });
        Assert.Equal(expected, bytes);
    }

    [Fact]
    public void Graphic_Inverted_WritesBlackAsOne()
    {
        var bmp = new MonoBitmap(8, 1);
        bmp.FillRect(0, 0, 8, 1);

        var bytes = new EplWriter().Graphic(0, 0, bmp, invert: true).ToArray();

        Assert.Equal(0xFF, bytes[^2]);
    }
}
