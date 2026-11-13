namespace LabelPrinter.Core.Layout;

/// <summary>One laid-out line. <see cref="X"/>/<see cref="Y"/> are the top-left corner of its line box in dots.</summary>
public sealed record LayoutLine(string Text, double X, double Y, double Width);

/// <summary>Result of <see cref="LabelLayoutEngine.Layout"/>: where each line goes and at which font size.</summary>
public sealed record LabelLayout
{
    /// <summary>Label width in dots.</summary>
    public required int WidthDots { get; init; }

    /// <summary>Label height in dots.</summary>
    public required int HeightDots { get; init; }

    /// <summary>The font used for every line (family, chosen size in dots, bold).</summary>
    public required FontSpec Font { get; init; }

    /// <summary>Chosen font size in dots (same as <c>Font.SizeDots</c>).</summary>
    public double FontSizeDots => Font.SizeDots;

    /// <summary>Line height in dots at the chosen size.</summary>
    public required double LineHeight { get; init; }

    /// <summary>The lines, top to bottom; empty when the content is empty.</summary>
    public required IReadOnlyList<LayoutLine> Lines { get; init; }

    /// <summary>
    /// The text does not fit inside the margins in at most <see cref="LabelContent.MaxLines"/> lines
    /// (even at the minimum size, or at the size that was set). The UI should warn.
    /// </summary>
    public bool Overflow { get; init; }

    /// <summary>A word had to be broken in the middle because it is longer than a line.</summary>
    public bool WordBroken { get; init; }

    /// <summary>The font size was chosen automatically (not set by the user).</summary>
    public bool AutoSized { get; init; }

    /// <summary>There is nothing to print.</summary>
    public bool IsEmpty => Lines.Count == 0;
}
