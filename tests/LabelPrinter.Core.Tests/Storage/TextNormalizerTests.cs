using LabelPrinter.Core.Storage;

namespace LabelPrinter.Core.Tests.Storage;

public class TextNormalizerTests
{
    [Fact]
    public void FoldForSearch_RemovesPolishDiacriticsAndCase()
    {
        Assert.Equal("aacceellnnoosszzzz", TextNormalizer.FoldForSearch("ĄąĆćĘęŁłŃńÓóŚśŹźŻż"));
    }

    [Fact]
    public void FoldForSearch_CollapsesWhitespaceAndLineBreaks()
    {
        Assert.Equal("zolta lodka", TextNormalizer.FoldForSearch("  Żółta\n\tŁÓDKA "));
    }

    [Fact]
    public void FoldForSearch_OtherLatinDiacritics()
    {
        Assert.Equal("cafe uber", TextNormalizer.FoldForSearch("Café Über"));
    }

    [Fact]
    public void FoldWords_TreatsPunctuationAsWordBreaks()
    {
        Assert.Equal("maka pszenna typ 650", TextNormalizer.FoldWords("Mąka-pszenna_typ/650"));
    }

    [Fact]
    public void DedupeKey_IgnoresCaseAndWhitespaceButKeepsDiacriticsAndLines()
    {
        Assert.Equal("ala ma\nkota", TextNormalizer.DedupeKey("  Ala   MA\n\n KOTA "));
        Assert.NotEqual(TextNormalizer.DedupeKey("Łódź"), TextNormalizer.DedupeKey("Lodz"));
    }
}
