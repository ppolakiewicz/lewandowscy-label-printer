using System.Globalization;
using System.Text;

namespace LabelPrinter.Core.Layout;

/// <summary>
/// Places the text of a <see cref="LabelContent"/> on a label: wraps it into at most
/// <see cref="LabelContent.MaxLines"/> lines, chooses the font size, aligns the lines horizontally and
/// centres the block vertically inside the margins. Pure logic; fonts are measured by an <see cref="ITextMeasurer"/>.
/// </summary>
public sealed class LabelLayoutEngine
{
    // Tolerance for floating-point widths returned by real font measurers.
    private const double Epsilon = 0.01;

    private readonly ITextMeasurer _measurer;

    public LabelLayoutEngine(ITextMeasurer measurer, LayoutOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(measurer);
        _measurer = measurer;
        Options = options ?? new LayoutOptions();
    }

    public LayoutOptions Options { get; }

    /// <summary>Lays out <paramref name="content"/> on a label of the given size in dots.</summary>
    public LabelLayout Layout(LabelContent content, int widthDots, int heightDots)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(widthDots);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(heightDots);

        var area = new Area(widthDots, heightDots, Options.MarginDots);
        double? fixedSize = content.FontSizeDots is { } requested && double.IsFinite(requested) && requested > 0 ? requested : null;
        string family = string.IsNullOrWhiteSpace(content.FontFamily) ? LabelContent.DefaultFontFamily : content.FontFamily;

        Fit fit;
        if (content.Mode == LayoutMode.Manual)
        {
            var lines = content.Lines.Select(NormalizeSpaces).Where(l => l.Length > 0).ToList();
            fit = lines.Count == 0
                ? Fit.Empty(fixedSize ?? MinSize)
                : fixedSize is { } size
                    ? FitManual(lines, family, content.Bold, size, area)
                    : SearchLargest(sz => FitManual(lines, family, content.Bold, sz, area), area);
        }
        else
        {
            var tokens = Tokenize(NormalizeSpaces(string.Join(' ', content.Lines)));
            fit = tokens.Count == 0
                ? Fit.Empty(fixedSize ?? MinSize)
                : fixedSize is { } size
                    ? FitAuto(tokens, family, content.Bold, size, area, allowWordBreak: true)
                    : SearchAuto(tokens, family, content.Bold, area);
        }

        return Position(fit, family, content.Bold, content.Alignment, area, autoSized: fixedSize is null);
    }

    /// <summary>
    /// Greedy wrapping of <paramref name="text"/> at <paramref name="font"/> into lines no wider than
    /// <paramref name="maxWidth"/>, without a line limit. Lines break after a space (the space is dropped)
    /// or after '-' / '_' (which stay at the end of the line). With <paramref name="allowWordBreak"/> a word
    /// wider than a line is split between characters; otherwise it is left on its own, too wide line.
    /// </summary>
    public IReadOnlyList<string> Wrap(string text, FontSpec font, double maxWidth, bool allowWordBreak, out bool wordBroken) =>
        Wrap(Tokenize(NormalizeSpaces(text)), font, maxWidth, allowWordBreak, out wordBroken);

    private double MinSize => Math.Max(1, Options.MinFontSizeDots);

    private Fit SearchAuto(List<Token> tokens, string family, bool bold, Area area)
    {
        // Prefer whole words; break inside a word only when no size fits otherwise.
        var fit = SearchLargest(size => FitAuto(tokens, family, bold, size, area, allowWordBreak: false), area);
        if (!fit.Overflow)
        {
            return fit;
        }

        return SearchLargest(size => FitAuto(tokens, family, bold, size, area, allowWordBreak: true), area);
    }

    /// <summary>Binary search for the largest whole size that fits; the minimum size (overflowing) if none does.</summary>
    private Fit SearchLargest(Func<double, Fit> fitAt, Area area)
    {
        int min = (int)Math.Ceiling(MinSize);
        int max = (int)Math.Floor(Options.MaxFontSizeDots ?? area.Height);
        max = Math.Max(min, max);

        var best = fitAt(min);
        if (best.Overflow)
        {
            return best;
        }

        int lo = min, hi = max; // invariant: fitAt(lo) fits
        while (lo < hi)
        {
            int mid = lo + (hi - lo + 1) / 2;
            var candidate = fitAt(mid);
            if (candidate.Overflow)
            {
                hi = mid - 1;
            }
            else
            {
                lo = mid;
                best = candidate;
            }
        }

        return best;
    }

    private Fit FitAuto(List<Token> tokens, string family, bool bold, double size, Area area, bool allowWordBreak)
    {
        var font = new FontSpec(family, size, bold);
        var lines = Wrap(tokens, font, area.Width, allowWordBreak: false, out _);
        bool broken = false;
        if (allowWordBreak && lines.Any(l => Width(l, font) > area.Width + Epsilon))
        {
            lines = Wrap(tokens, font, area.Width, allowWordBreak: true, out broken);
        }

        return Check(lines, font, area, broken);
    }

    private Fit FitManual(List<string> lines, string family, bool bold, double size, Area area) =>
        Check(lines, new FontSpec(family, size, bold), area, wordBroken: false);

    private Fit Check(IReadOnlyList<string> lines, FontSpec font, Area area, bool wordBroken)
    {
        double lineHeight = _measurer.LineHeight(font);
        var widths = lines.Select(l => Width(l, font)).ToList();
        bool overflow = lines.Count > LabelContent.MaxLines
            || lines.Count * lineHeight > area.Height + Epsilon
            || widths.Any(w => w > area.Width + Epsilon);
        return new Fit(font.SizeDots, lineHeight, lines, widths, overflow, wordBroken);
    }

    private static LabelLayout Position(Fit fit, string family, bool bold, LabelAlignment alignment, Area area, bool autoSized)
    {
        double blockHeight = fit.Lines.Count * fit.LineHeight;
        double top = area.Margin + (area.Height - blockHeight) / 2;
        var lines = new List<LayoutLine>(fit.Lines.Count);
        for (int i = 0; i < fit.Lines.Count; i++)
        {
            double width = fit.Widths[i];
            double x = alignment == LabelAlignment.Left ? area.Margin : area.Margin + (area.Width - width) / 2;
            lines.Add(new LayoutLine(fit.Lines[i], Math.Round(x, MidpointRounding.AwayFromZero), Math.Round(top + i * fit.LineHeight, MidpointRounding.AwayFromZero), width));
        }

        return new LabelLayout
        {
            WidthDots = area.LabelWidth,
            HeightDots = area.LabelHeight,
            Font = new FontSpec(family, fit.Size, bold),
            LineHeight = fit.LineHeight,
            Lines = lines,
            Overflow = fit.Overflow,
            WordBroken = fit.WordBroken,
            AutoSized = autoSized,
        };
    }

    private List<string> Wrap(List<Token> tokens, FontSpec font, double maxWidth, bool allowWordBreak, out bool wordBroken)
    {
        var lines = new List<string>();
        wordBroken = false;
        string? current = null;
        bool spaceBefore = false;

        foreach (var token in tokens)
        {
            if (current != null)
            {
                string candidate = spaceBefore ? current + " " + token.Text : current + token.Text;
                if (Width(candidate, font) <= maxWidth + Epsilon)
                {
                    current = candidate;
                    spaceBefore = token.SpaceAfter;
                    continue;
                }

                lines.Add(current);
            }

            current = token.Text;
            if (allowWordBreak && Width(current, font) > maxWidth + Epsilon)
            {
                var pieces = SplitWord(current, font, maxWidth);
                wordBroken |= pieces.Count > 1;
                lines.AddRange(pieces.Take(pieces.Count - 1));
                current = pieces[^1];
            }

            spaceBefore = token.SpaceAfter;
        }

        if (current != null)
        {
            lines.Add(current);
        }

        return lines;
    }

    /// <summary>Splits a word into pieces that fit <paramref name="maxWidth"/>, each at least one character long.</summary>
    private List<string> SplitWord(string word, FontSpec font, double maxWidth)
    {
        var pieces = new List<string>();
        var elements = TextElements(word);
        var piece = new StringBuilder();
        foreach (var element in elements)
        {
            if (piece.Length > 0 && Width(piece.ToString() + element, font) > maxWidth + Epsilon)
            {
                pieces.Add(piece.ToString());
                piece.Clear();
            }

            piece.Append(element);
        }

        pieces.Add(piece.ToString());
        return pieces;
    }

    private static List<string> TextElements(string text)
    {
        var result = new List<string>();
        var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            result.Add(enumerator.GetTextElement());
        }

        return result;
    }

    private double Width(string text, FontSpec font) => _measurer.MeasureWidth(text, font);

    /// <summary>Collapses every run of white space (including line breaks and tabs) to one space and trims.</summary>
    private static string NormalizeSpaces(string text)
    {
        var sb = new StringBuilder(text.Length);
        bool pendingSpace = false;
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = sb.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                sb.Append(' ');
                pendingSpace = false;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Splits normalized text into unbreakable pieces. A piece ends at a space (<see cref="Token.SpaceAfter"/>)
    /// or right after '-' / '_' (kept in the piece).
    /// </summary>
    private static List<Token> Tokenize(string text)
    {
        var tokens = new List<Token>();
        var current = new StringBuilder();
        foreach (char c in text)
        {
            if (c == ' ')
            {
                if (current.Length > 0)
                {
                    tokens.Add(new Token(current.ToString(), SpaceAfter: true));
                    current.Clear();
                }
                else if (tokens.Count > 0)
                {
                    tokens[^1] = tokens[^1] with { SpaceAfter = true };
                }

                continue;
            }

            current.Append(c);
            if (c is '-' or '_')
            {
                tokens.Add(new Token(current.ToString(), SpaceAfter: false));
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(new Token(current.ToString(), SpaceAfter: false));
        }

        return tokens;
    }

    private readonly record struct Token(string Text, bool SpaceAfter);

    /// <summary>Label size and the usable area inside the margins.</summary>
    private readonly record struct Area(int LabelWidth, int LabelHeight, int Margin)
    {
        public double Width => Math.Max(1, LabelWidth - 2 * Margin);
        public double Height => Math.Max(1, LabelHeight - 2 * Margin);
    }

    private sealed record Fit(
        double Size, double LineHeight, IReadOnlyList<string> Lines, IReadOnlyList<double> Widths, bool Overflow, bool WordBroken)
    {
        public static Fit Empty(double size) => new(size, 0, [], [], Overflow: false, WordBroken: false);
    }
}
