namespace LabelPrinter.Core.Layout;

/// <summary>A font: family name, size in printer dots (em size) and weight.</summary>
public readonly record struct FontSpec(string Family, double SizeDots, bool Bold);

/// <summary>
/// Measures text for the layout engine. Implemented with real fonts in the desktop app and with a fake
/// monospace font in tests. All values are in printer dots.
/// </summary>
public interface ITextMeasurer
{
    /// <summary>Advance width of <paramref name="text"/> on a single line.</summary>
    double MeasureWidth(string text, FontSpec font);

    /// <summary>Height of one line of text (distance between the tops of consecutive lines).</summary>
    double LineHeight(FontSpec font);
}
