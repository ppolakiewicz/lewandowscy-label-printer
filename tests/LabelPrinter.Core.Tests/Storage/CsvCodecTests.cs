using LabelPrinter.Core.Storage;

namespace LabelPrinter.Core.Tests.Storage;

public class CsvCodecTests
{
    [Theory]
    [InlineData("abc", "abc")]
    [InlineData("", "")]
    [InlineData("a;b", "\"a;b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("two\nlines", "\"two\nlines\"")]
    [InlineData(" padded", "\" padded\"")]
    [InlineData("a,b", "a,b")]
    public void Quote_OnlyWhenNeeded(string field, string expected)
    {
        Assert.Equal(expected, CsvCodec.Quote(field, ';'));
    }

    [Fact]
    public void WriteThenParse_RoundTrips()
    {
        string[][] rows =
        [
            ["Tekst", "Notatka"],
            ["a;b", "say \"hi\""],
            ["line1\nline2", ""],
            ["Żółć", " x "],
        ];
        var writer = new StringWriter();
        foreach (var row in rows)
        {
            CsvCodec.WriteRecord(writer, row, ';');
        }

        var parsed = CsvCodec.Parse(writer.ToString(), ';');

        Assert.Equal(rows.Length, parsed.Count);
        for (var i = 0; i < rows.Length; i++)
        {
            Assert.Equal(rows[i], parsed[i].Fields);
        }
    }

    [Fact]
    public void Parse_HandlesBomLineEndingsEmptyLinesAndRowNumbers()
    {
        var parsed = CsvCodec.Parse("\uFEFFa;b\r\n\r\n\"x\r\ny\";\nlast", ';');

        Assert.Equal(3, parsed.Count);
        Assert.Equal(new[] { "a", "b" }, parsed[0].Fields);
        Assert.Equal(1, parsed[0].Line);
        Assert.Equal(new[] { "x\ny", "" }, parsed[1].Fields);
        Assert.Equal(3, parsed[1].Line);
        Assert.Equal(new[] { "last" }, parsed[2].Fields);
        Assert.Equal(5, parsed[2].Line);
    }

    [Theory]
    [InlineData("a;b;c\n1,5;2", ';')]
    [InlineData("a,b,c\n1;2", ',')]
    [InlineData("a\tb\n", '\t')]
    [InlineData("\"x;y\",b,c", ',')]
    [InlineData("tylko tekst", ';')]
    public void DetectSeparator_UsesFirstLine(string text, char expected)
    {
        Assert.Equal(expected, CsvCodec.DetectSeparator(text));
    }
}
