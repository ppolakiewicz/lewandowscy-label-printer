using LabelPrinter.Core.Graphics;
using static System.FormattableString;

namespace LabelPrinter.Core.Epl;

/// <summary>Complete EPL2 jobs for 203 dpi Zebra printers (LP2844, ZT231).</summary>
public static class EplLabels
{
    public const int MaxCopies = 999;

    /// <summary>A label whose content is one full-label bitmap (label size in dots).</summary>
    public static byte[] Image(PrinterConfig config, MonoBitmap image, int copies)
    {
        var c = config.Normalized();
        var writer = Begin(c);
        writer.Graphic(0, 0, image.Shifted(c.OffsetXDots, c.OffsetYDots), c.InvertGraphics);
        writer.Command(Invariant($"P{Math.Clamp(copies, 1, MaxCopies)}"));
        return writer.ToArray();
    }

    /// <summary>
    /// Calibration label: a frame on the label edge, a millimetre ruler and a corner marker drawn as a
    /// graphic, plus the current settings in the printer's built-in font.
    /// </summary>
    public static byte[] TestLabel(PrinterConfig config)
    {
        var c = config.Normalized();
        var writer = Begin(c);
        writer.Graphic(0, 0, TestPattern(c).Shifted(c.OffsetXDots, c.OffsetYDots), c.InvertGraphics);

        int x = Math.Max(0, Units.MmToDots(6) + c.OffsetXDots);
        int y = Math.Max(0, Units.MmToDots(7) + c.OffsetYDots);
        writer.Text(x, y, 3, "TEST ETYKIETY");
        writer.Text(x, y + 28, 2, Invariant($"{c.LabelWidthMm:0.#}x{c.LabelHeightMm:0.#} mm, przerwa {c.GapMm:0.#}"));
        writer.Text(x, y + 52, 2, Invariant($"D{c.Darkness} S{c.Speed} X{c.OffsetXMm:+0.0;-0.0;0} Y{c.OffsetYMm:+0.0;-0.0;0}"));
        writer.Text(x, y + 76, 1, "Ramka = krawedz etykiety");
        writer.Command("P1");
        return writer.ToArray();
    }

    /// <summary>The graphic part of <see cref="TestLabel"/>, without offsets (also used for the preview).</summary>
    public static MonoBitmap TestPattern(PrinterConfig config)
    {
        var c = config.Normalized();
        int width = c.LabelWidthDots, height = c.LabelHeightDots;
        var bmp = new MonoBitmap(width, height);

        bmp.DrawRect(0, 0, width, height, thickness: 2);

        for (int mm = 5; mm < c.LabelWidthMm; mm += 5)
        {
            int length = Units.MmToDots(mm % 10 == 0 ? 3 : 1.5);
            bmp.FillRect(Units.MmToDots(mm) - 1, 0, 2, length);
        }

        for (int mm = 5; mm < c.LabelHeightMm; mm += 5)
        {
            int length = Units.MmToDots(mm % 10 == 0 ? 3 : 1.5);
            bmp.FillRect(0, Units.MmToDots(mm) - 1, length, 2);
        }

        // Solid square in the bottom-right corner shows the print orientation.
        int marker = Units.MmToDots(3);
        int inset = Units.MmToDots(2);
        bmp.FillRect(width - inset - marker, height - inset - marker, marker, marker);

        return bmp;
    }

    /// <summary>xa: makes the printer measure the label and gap length itself.</summary>
    public static byte[] AutoSense() => new EplWriter().Command("").Command("xa").ToArray();

    /// <summary>U: prints the printer's configuration label.</summary>
    public static byte[] PrintConfiguration() => new EplWriter().Command("").Command("U").ToArray();

    private static EplWriter Begin(PrinterConfig c)
    {
        return new EplWriter()
            .Command("") // a leading line feed ends any half-received command
            .Command(Invariant($"Q{c.LabelHeightDots},{c.GapDots}"))
            .Command(Invariant($"q{c.PrintWidthDots}"))
            .Command(Invariant($"S{c.Speed}"))
            .Command(Invariant($"D{c.Darkness}"))
            .Command(c.PrintTopFirst ? "ZT" : "ZB")
            // The label starts LeftMarginDots from the head's edge, depending on how the printer guides the roll.
            // R makes the image buffer as wide as the head (cancelling q) and moves the origin to the label's
            // left edge, so x = 0 is the label edge whatever was stored in the printer before.
            .Command(Invariant($"R{c.LeftMarginDots},0"))
            .Command("N");
    }
}
