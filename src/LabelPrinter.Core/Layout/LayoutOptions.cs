namespace LabelPrinter.Core.Layout;

/// <summary>Settings of <see cref="LabelLayoutEngine"/>; all values in printer dots.</summary>
public sealed record LayoutOptions
{
    /// <summary>Default margin: 2 mm.</summary>
    public const int DefaultMarginDots = 2 * Units.DotsPerMm;

    /// <summary>Default smallest automatic font size: 12 dots (1.5 mm).</summary>
    public const double DefaultMinFontSizeDots = 12;

    /// <summary>Empty space kept on every side of the label.</summary>
    public int MarginDots { get; init; } = DefaultMarginDots;

    /// <summary>Smallest font size tried by automatic sizing; below it the text is reported as overflowing.</summary>
    public double MinFontSizeDots { get; init; } = DefaultMinFontSizeDots;

    /// <summary>Largest font size tried by automatic sizing; null means the height inside the margins.</summary>
    public double? MaxFontSizeDots { get; init; }
}
