using System.Globalization;
using System.Windows;
using System.Windows.Media;
using LabelPrinter.Core.Layout;

namespace LabelPrinter.Desktop.Rendering;

/// <summary>
/// Measures text with WPF fonts. One WPF unit is one printer dot (the label is rendered at 96 dpi with
/// one pixel per dot), and text uses <see cref="TextFormattingMode.Display"/> metrics, as when drawing.
/// </summary>
public sealed class WpfTextMeasurer : ITextMeasurer
{
    private static readonly CultureInfo TextCulture = CultureInfo.GetCultureInfo("pl-PL");

    private readonly Dictionary<(string Family, bool Bold), Typeface> _typefaces = new();

    /// <inheritdoc />
    public double MeasureWidth(string text, FontSpec font) =>
        text.Length == 0 ? 0 : CreateText(text, font, Brushes.Black).WidthIncludingTrailingWhitespace;

    /// <inheritdoc />
    public double LineHeight(FontSpec font) => CreateText("ĄÉgj", font, Brushes.Black).Height;

    /// <summary>Formats <paramref name="text"/> as one line, exactly as it is measured and drawn.</summary>
    public FormattedText CreateText(string text, FontSpec font, Brush foreground) =>
        new(text, TextCulture, FlowDirection.LeftToRight, GetTypeface(font), font.SizeDots, foreground,
            numberSubstitution: null, TextFormattingMode.Display, pixelsPerDip: 1.0);

    private Typeface GetTypeface(FontSpec font)
    {
        var key = (font.Family, font.Bold);
        if (!_typefaces.TryGetValue(key, out var typeface))
        {
            typeface = new Typeface(
                new FontFamily(font.Family),
                FontStyles.Normal,
                font.Bold ? FontWeights.Bold : FontWeights.Normal,
                FontStretches.Normal);
            _typefaces[key] = typeface;
        }

        return typeface;
    }
}
