using LabelPrinter.Core.Graphics;

namespace LabelPrinter.Core.Tests;

public class MonoBitmapTests
{
    [Fact]
    public void PackedRows_BlackIsZero_PadsWithWhite()
    {
        var bmp = new MonoBitmap(10, 1);
        bmp[0, 0] = true;
        bmp[9, 0] = true;

        Assert.Equal(new byte[] { 0x7F, 0xBF }, bmp.ToPackedRows(blackIsZero: true));
    }

    [Fact]
    public void PackedRows_BlackIsOne_PadsWithZero()
    {
        var bmp = new MonoBitmap(10, 1);
        bmp[0, 0] = true;
        bmp[9, 0] = true;

        Assert.Equal(new byte[] { 0x80, 0x40 }, bmp.ToPackedRows(blackIsZero: false));
    }

    [Fact]
    public void PackedRows_AreRowMajor()
    {
        var bmp = new MonoBitmap(8, 2);
        bmp[7, 1] = true;

        Assert.Equal(new byte[] { 0x00, 0x01 }, bmp.ToPackedRows(blackIsZero: false));
    }

    [Fact]
    public void FillRect_IsClippedToImage()
    {
        var bmp = new MonoBitmap(4, 4);
        bmp.FillRect(-2, -2, 4, 4);

        Assert.Equal(4, bmp.CountBlack());
        Assert.True(bmp[1, 1]);
        Assert.False(bmp[2, 2]);
    }

    [Fact]
    public void DrawRect_DrawsOnlyTheBorder()
    {
        var bmp = new MonoBitmap(5, 5);
        bmp.DrawRect(0, 0, 5, 5);

        Assert.Equal(16, bmp.CountBlack());
        Assert.False(bmp[2, 2]);
    }

    [Fact]
    public void Shifted_MovesPixelsAndDropsWhatFallsOut()
    {
        var bmp = new MonoBitmap(4, 4);
        bmp[0, 0] = true;
        bmp[3, 3] = true;

        var shifted = bmp.Shifted(1, 2);

        Assert.Equal(1, shifted.CountBlack());
        Assert.True(shifted[1, 2]);
    }

    [Fact]
    public void Indexer_OutsideImage_Throws()
    {
        var bmp = new MonoBitmap(2, 2);
        Assert.Throws<ArgumentOutOfRangeException>(() => bmp[2, 0]);
    }
}
