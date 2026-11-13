using LabelPrinter.Core.Layout;

namespace LabelPrinter.Core.Tests;

public class LabelLayoutEngineTests
{
    // 50x30 mm label; with the default 2 mm margin the usable area is 368x208 dots.
    private const int Width = 400;
    private const int Height = 240;

    /// <summary>Every character is half the font size wide (bold: 0.6); a line is exactly the font size high.</summary>
    private sealed class MonospaceMeasurer : ITextMeasurer
    {
        public double MeasureWidth(string text, FontSpec font) => text.Length * font.SizeDots * (font.Bold ? 0.6 : 0.5);

        public double LineHeight(FontSpec font) => font.SizeDots;
    }

    private static readonly LabelLayoutEngine Engine = new(new MonospaceMeasurer());

    private static LabelLayout Auto(string text, Func<LabelContent, LabelContent>? change = null)
    {
        var content = LabelContent.FromText(text);
        return Engine.Layout(change?.Invoke(content) ?? content, Width, Height);
    }

    private static LabelLayout Manual(params string[] lines) =>
        Engine.Layout(new LabelContent { Mode = LayoutMode.Manual, Lines = lines }, Width, Height);

    private static string[] Texts(LabelLayout layout) => layout.Lines.Select(l => l.Text).ToArray();

    // ---- empty content ----

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" \n\t ")]
    public void Auto_EmptyText_HasNoLines(string text)
    {
        var layout = Auto(text);

        Assert.True(layout.IsEmpty);
        Assert.False(layout.Overflow);
        Assert.False(layout.WordBroken);
    }

    [Fact]
    public void Auto_NoLinesAtAll_IsEmpty()
    {
        var layout = Engine.Layout(new LabelContent(), Width, Height);

        Assert.True(layout.IsEmpty);
        Assert.Equal(Width, layout.WidthDots);
        Assert.Equal(Height, layout.HeightDots);
    }

    [Fact]
    public void Manual_OnlyEmptyLines_IsEmpty()
    {
        var layout = Manual("", "  ", "");

        Assert.True(layout.IsEmpty);
        Assert.False(layout.Overflow);
    }

    // ---- automatic size ----

    [Fact]
    public void Auto_ShortText_IsLimitedByHeight()
    {
        var layout = Auto("AB");

        Assert.Equal(208, layout.FontSizeDots);
        Assert.Equal(208, layout.LineHeight);
        var line = Assert.Single(layout.Lines);
        Assert.Equal("AB", line.Text);
        Assert.Equal(208, line.Width);
        Assert.Equal(96, line.X); // 16 + (368 - 208) / 2
        Assert.Equal(16, line.Y);
        Assert.True(layout.AutoSized);
        Assert.False(layout.Overflow);
    }

    [Fact]
    public void Auto_SingleWord_IsLimitedByWidth()
    {
        var layout = Auto("ABCDEFGHIJ");

        Assert.Equal(73, layout.FontSizeDots); // 10 chars * 0.5 * 73 = 365 <= 368
        var line = Assert.Single(layout.Lines);
        Assert.Equal(18, line.X); // 16 + 1.5, rounded
        Assert.Equal(84, line.Y); // 16 + (208 - 73) / 2, rounded
    }

    [Fact]
    public void Auto_PicksTheLargestSizeEvenIfItNeedsMoreLines()
    {
        // One line fits up to 81 dots; two lines ("AAAA" / "BBBB") up to 104 (height 2 * 104 = 208).
        var layout = Auto("AAAA BBBB");

        Assert.Equal(104, layout.FontSizeDots);
        Assert.Equal(new[] { "AAAA", "BBBB" }, Texts(layout));
    }

    [Fact]
    public void Auto_Bold_UsesWiderMeasurements()
    {
        var regular = Auto("ABCDEFGHIJ");
        var bold = Auto("ABCDEFGHIJ", c => c with { Bold = true });

        Assert.Equal(61, bold.FontSizeDots); // 10 * 0.6 * 61 = 366
        Assert.True(bold.Font.Bold);
        Assert.True(bold.FontSizeDots < regular.FontSizeDots);
    }

    [Fact]
    public void Auto_UsesTheContentFontFamily()
    {
        var layout = Auto("AB", c => c with { FontFamily = "Verdana" });

        Assert.Equal("Verdana", layout.Font.Family);
    }

    [Fact]
    public void Auto_SentenceNeverExceedsThreeLines()
    {
        var layout = Auto("Zażółć gęślą jaźń, pchnąć w tę łódź jeża lub ośm skrzyń fig");

        Assert.False(layout.Overflow);
        Assert.InRange(layout.Lines.Count, 1, LabelContent.MaxLines);
        Assert.All(layout.Lines, l => Assert.True(l.Width <= 368));
        Assert.True(layout.Lines.Count * layout.LineHeight <= 208);
    }

    [Fact]
    public void Auto_IsTheLargestSizeThatFits()
    {
        const string text = "Zażółć gęślą jaźń, pchnąć w tę łódź jeża lub ośm skrzyń fig";
        var layout = Auto(text);

        var bigger = Auto(text, c => c with { FontSizeDots = layout.FontSizeDots + 1 });

        // One dot more either overflows or only fits by breaking words, which auto sizing avoids.
        Assert.True(bigger.Overflow || bigger.WordBroken);
    }

    [Fact]
    public void Auto_JoinsMultipleEntriesAndCollapsesWhiteSpace()
    {
        var layout = Auto("  AAAA \n\t  BBBB  ");
        var joined = Engine.Layout(new LabelContent { Lines = ["AAAA", "BBBB"] }, Width, Height);

        Assert.Equal(new[] { "AAAA", "BBBB" }, Texts(layout));
        Assert.Equal(Texts(layout), Texts(joined));
        Assert.Equal(layout.FontSizeDots, joined.FontSizeDots);
    }

    // ---- wrap points ----

    [Fact]
    public void Wrap_AtSpace_DropsTheSpace()
    {
        var layout = Auto("AAAA BBBB", c => c with { FontSizeDots = 100 }); // one line would be 450 wide

        Assert.Equal(new[] { "AAAA", "BBBB" }, Texts(layout));
        Assert.False(layout.Overflow);
    }

    [Theory]
    [InlineData("AAAA-BBBB", "AAAA-", "BBBB")]
    [InlineData("AAAA_BBBB", "AAAA_", "BBBB")]
    public void Wrap_AfterHyphenOrUnderscore_KeepsItOnTheFirstLine(string text, string first, string second)
    {
        var layout = Auto(text, c => c with { FontSizeDots = 100 });

        Assert.Equal(new[] { first, second }, Texts(layout));
    }

    [Fact]
    public void Wrap_FillsLinesGreedily()
    {
        var font = new FontSpec("Arial", 10, false); // 5 dots per character

        var lines = Engine.Wrap("AB-CD-EF GH IJ", font, maxWidth: 30, allowWordBreak: false, out bool broken);

        Assert.Equal(new[] { "AB-CD-", "EF GH", "IJ" }, lines);
        Assert.False(broken);
    }

    [Fact]
    public void Wrap_SpaceAroundHyphen_IsDroppedAtTheBreak()
    {
        var font = new FontSpec("Arial", 10, false);

        var lines = Engine.Wrap("AAA - BBB", font, maxWidth: 25, allowWordBreak: false, out _);

        Assert.Equal(new[] { "AAA -", "BBB" }, lines);
    }

    [Fact]
    public void Wrap_DoesNotBreakInsideWordsUnlessAllowed()
    {
        var font = new FontSpec("Arial", 10, false);

        var whole = Engine.Wrap("ABCDEFGH XY", font, maxWidth: 20, allowWordBreak: false, out bool brokenWhole);
        var split = Engine.Wrap("ABCDEFGH XY", font, maxWidth: 20, allowWordBreak: true, out bool brokenSplit);

        Assert.Equal(new[] { "ABCDEFGH", "XY" }, whole);
        Assert.False(brokenWhole);
        Assert.Equal(new[] { "ABCD", "EFGH", "XY" }, split);
        Assert.True(brokenSplit);
    }

    [Fact]
    public void Wrap_TextFollowingABrokenWordContinuesOnItsLastPiece()
    {
        var font = new FontSpec("Arial", 10, false);

        var lines = Engine.Wrap("ABCDEF X", font, maxWidth: 20, allowWordBreak: true, out _);

        Assert.Equal(new[] { "ABCD", "EF X" }, lines);
    }

    // ---- long words and overflow ----

    [Fact]
    public void Auto_WordLongerThanALine_IsBrokenAsLastResort()
    {
        var layout = Auto(new string('A', 100));

        // At 21 dots a character is 10.5 wide: 35 per line -> 35 + 35 + 30. At 22 it would need 4 lines.
        Assert.Equal(21, layout.FontSizeDots);
        Assert.Equal(new[] { 35, 35, 30 }, layout.Lines.Select(l => l.Text.Length).ToArray());
        Assert.True(layout.WordBroken);
        Assert.False(layout.Overflow);
    }

    [Fact]
    public void Auto_DoesNotBreakWordsWhenWholeWordsFit()
    {
        var layout = Auto("ABCDEFGHIJ KLMNOPQRST");

        Assert.False(layout.WordBroken);
        Assert.Equal(new[] { "ABCDEFGHIJ", "KLMNOPQRST" }, Texts(layout));
    }

    [Fact]
    public void Auto_MoreThanThreeLinesAtMinimumSize_Overflows()
    {
        // Each 50-character word fills a line at 12 dots (300 of 368), so four lines are needed.
        var word = new string('A', 50);
        var layout = Auto(string.Join(' ', word, word, word, word));

        Assert.True(layout.Overflow);
        Assert.Equal(LayoutOptions.DefaultMinFontSizeDots, layout.FontSizeDots);
        Assert.Equal(4, layout.Lines.Count);
    }

    [Fact]
    public void Auto_TextTooLongEvenWithBrokenWords_Overflows()
    {
        var layout = Auto(new string('A', 1000));

        Assert.True(layout.Overflow);
        Assert.True(layout.WordBroken);
        Assert.Equal(12, layout.FontSizeDots);
    }

    // ---- fixed size ----

    [Fact]
    public void FixedSize_IsUsedAsIs()
    {
        var layout = Auto("AB", c => c with { FontSizeDots = 40.5 });

        Assert.Equal(40.5, layout.FontSizeDots);
        Assert.False(layout.AutoSized);
        Assert.False(layout.Overflow);
    }

    [Fact]
    public void FixedSize_TooManyLines_Overflows()
    {
        var layout = Auto("AAAAAA BBBBBB CCCCCC DDDDDD", c => c with { FontSizeDots = 100 });

        Assert.Equal(new[] { "AAAAAA", "BBBBBB", "CCCCCC", "DDDDDD" }, Texts(layout));
        Assert.True(layout.Overflow);
    }

    [Fact]
    public void FixedSize_TooHigh_Overflows()
    {
        var layout = Auto("A", c => c with { FontSizeDots = 220 });

        Assert.True(layout.Overflow);
    }

    [Fact]
    public void FixedSize_LongWord_IsBrokenAndFlagged()
    {
        var layout = Auto("ABCDEFGHIJ", c => c with { FontSizeDots = 100 }); // 7 characters per line

        Assert.Equal(new[] { "ABCDEFG", "HIJ" }, Texts(layout));
        Assert.True(layout.WordBroken);
        Assert.False(layout.Overflow);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(double.NaN)]
    public void FixedSize_Invalid_MeansAutomatic(double size)
    {
        var layout = Auto("AB", c => c with { FontSizeDots = size });

        Assert.True(layout.AutoSized);
        Assert.Equal(208, layout.FontSizeDots);
    }

    // ---- alignment and vertical centring ----

    [Fact]
    public void Center_CentresEveryLineHorizontally()
    {
        var layout = Auto("AAAAAA BBB", c => c with { FontSizeDots = 80 }); // 10 * 40 = 400 > 368: two lines

        Assert.Equal(new[] { "AAAAAA", "BBB" }, Texts(layout));
        Assert.Equal(80, layout.Lines[0].X);  // 16 + (368 - 240) / 2
        Assert.Equal(140, layout.Lines[1].X); // 16 + (368 - 120) / 2
    }

    [Fact]
    public void Left_StartsEveryLineAtTheMargin()
    {
        var layout = Auto("AAAAAA BBB", c => c with { FontSizeDots = 80, Alignment = LabelAlignment.Left });

        Assert.All(layout.Lines, l => Assert.Equal(16, l.X));
    }

    [Fact]
    public void Block_IsCentredVertically()
    {
        var layout = Auto("AAAAAA BBB", c => c with { FontSizeDots = 80 });

        // Block of 2 * 80 = 160 in 208: top at 16 + 24 = 40.
        Assert.Equal(40, layout.Lines[0].Y);
        Assert.Equal(120, layout.Lines[1].Y);
    }

    [Fact]
    public void ThreeLines_AreStackedByLineHeight()
    {
        var layout = Manual("A", "B", "C");

        Assert.Equal(69, layout.FontSizeDots); // 3 * 69 = 207 <= 208
        Assert.Equal(new[] { 17.0, 86.0, 155.0 }, layout.Lines.Select(l => l.Y).ToArray()); // top 16 + 0.5
    }

    // ---- manual mode ----

    [Fact]
    public void Manual_KeepsLinesAndIgnoresEmptyOnes()
    {
        var layout = Manual("AB", "", "  CDEF  ");

        Assert.Equal(new[] { "AB", "CDEF" }, Texts(layout));
        Assert.Equal(104, layout.FontSizeDots); // height: 2 * 104 = 208; width: 4 * 52 = 208
        Assert.False(layout.Overflow);
    }

    [Fact]
    public void Manual_DoesNotWrap()
    {
        var layout = Manual("AAAA BBBB CCCC DDDD");

        Assert.Equal(new[] { "AAAA BBBB CCCC DDDD" }, Texts(layout));
        Assert.Equal(38, layout.FontSizeDots); // 19 * 0.5 * 38 = 361
    }

    [Fact]
    public void Manual_SizeIsLimitedByTheWidestLine()
    {
        var layout = Manual("A", "ABCDEFGHIJ");

        Assert.Equal(73, layout.FontSizeDots);
    }

    [Fact]
    public void Manual_LineTooLongAtMinimumSize_Overflows()
    {
        var layout = Manual("A", new string('B', 70)); // 70 * 6 = 420 > 368 at 12 dots

        Assert.True(layout.Overflow);
        Assert.Equal(12, layout.FontSizeDots);
        Assert.False(layout.WordBroken);
    }

    [Fact]
    public void Manual_FixedSize_FlagsOverflow()
    {
        var fits = Engine.Layout(new LabelContent { Mode = LayoutMode.Manual, Lines = ["AB", "CD"], FontSizeDots = 50 }, Width, Height);
        var tooBig = Engine.Layout(new LabelContent { Mode = LayoutMode.Manual, Lines = ["AB", "CD"], FontSizeDots = 120 }, Width, Height);

        Assert.False(fits.Overflow);
        Assert.Equal(50, fits.FontSizeDots);
        Assert.True(tooBig.Overflow);
        Assert.Equal(120, tooBig.FontSizeDots);
    }

    [Fact]
    public void Manual_MoreThanThreeLines_Overflows()
    {
        var layout = Manual("A", "B", "C", "D");

        Assert.True(layout.Overflow);
    }

    // ---- options ----

    [Fact]
    public void Options_MarginIsConfigurable()
    {
        var engine = new LabelLayoutEngine(new MonospaceMeasurer(), new LayoutOptions { MarginDots = 0 });

        var layout = engine.Layout(LabelContent.FromText("AB"), Width, Height);

        Assert.Equal(240, layout.FontSizeDots);
        Assert.Equal(80, layout.Lines[0].X);
        Assert.Equal(0, layout.Lines[0].Y);
    }

    [Fact]
    public void Options_MaxFontSizeLimitsAutomaticSize()
    {
        var engine = new LabelLayoutEngine(new MonospaceMeasurer(), new LayoutOptions { MaxFontSizeDots = 50 });

        var layout = engine.Layout(LabelContent.FromText("AB"), Width, Height);

        Assert.Equal(50, layout.FontSizeDots);
    }

    [Fact]
    public void Options_MinFontSizeIsTheOverflowSize()
    {
        var engine = new LabelLayoutEngine(new MonospaceMeasurer(), new LayoutOptions { MinFontSizeDots = 20 });

        var layout = engine.Layout(LabelContent.FromText(new string('A', 1000)), Width, Height);

        Assert.True(layout.Overflow);
        Assert.Equal(20, layout.FontSizeDots);
    }
}
