using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LabelPrinter.Core.Graphics;
using LabelPrinter.Core.Layout;

namespace LabelPrinter.Desktop.Rendering;

/// <summary>A laid-out label and its 1-bit image, ready for <c>EplLabels.Image</c> and for the preview.</summary>
public sealed record RenderedLabel(LabelLayout Layout, MonoBitmap Bitmap);

/// <summary>
/// Turns a <see cref="LabelContent"/> into a full-label 1-bit bitmap: lays it out with
/// <see cref="LabelLayoutEngine"/> and WPF fonts, draws it in black on white and thresholds the result.
/// Must be used on a WPF (STA) thread.
/// </summary>
public sealed class LabelRenderer
{
    private readonly WpfTextMeasurer _measurer = new();

    public LabelRenderer(LayoutOptions? options = null)
    {
        Engine = new LabelLayoutEngine(_measurer, options);
    }

    /// <summary>The layout engine (measuring with WPF fonts).</summary>
    public LabelLayoutEngine Engine { get; }

    /// <summary>Only the layout (no drawing), e.g. to show the chosen size or an overflow warning.</summary>
    public LabelLayout Layout(LabelContent content, int widthDots, int heightDots) =>
        Engine.Layout(content, widthDots, heightDots);

    /// <summary>Lays out and draws <paramref name="content"/> on a label of the given size in dots.</summary>
    public RenderedLabel Render(LabelContent content, int widthDots, int heightDots)
    {
        var layout = Layout(content, widthDots, heightDots);
        return new RenderedLabel(layout, RenderBitmap(layout));
    }

    /// <summary>Draws an existing layout into a 1-bit bitmap of the label size.</summary>
    public MonoBitmap RenderBitmap(LabelLayout layout)
    {
        int width = layout.WidthDots, height = layout.HeightDots;

        var visual = new DrawingVisual();
        // Crisp, hinted, non-antialiased glyphs where WPF honours it; the threshold below handles the rest.
        TextOptions.SetTextFormattingMode(visual, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(visual, TextRenderingMode.Aliased);
        TextOptions.SetTextHintingMode(visual, TextHintingMode.Fixed);
        RenderOptions.SetEdgeMode(visual, EdgeMode.Aliased);

        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            foreach (var line in layout.Lines)
            {
                dc.DrawText(_measurer.CreateText(line.Text, layout.Font, Brushes.Black), new Point(line.X, line.Y));
            }
        }

        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        return MonoBitmapImaging.ToMonoBitmap(target);
    }
}
