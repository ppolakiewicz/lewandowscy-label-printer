using System.Windows.Media;
using System.Windows.Media.Imaging;
using LabelPrinter.Core.Graphics;

namespace LabelPrinter.Desktop.Rendering;

/// <summary>Conversions between <see cref="MonoBitmap"/> and WPF images.</summary>
public static class MonoBitmapImaging
{
    /// <summary>A frozen black-and-white WPF image of <paramref name="bitmap"/> for previews (203 dpi, so it has the real size).</summary>
    public static BitmapSource ToBitmapSource(this MonoBitmap bitmap)
    {
        var source = BitmapSource.Create(
            bitmap.Width, bitmap.Height, 203, 203, PixelFormats.BlackWhite, null,
            bitmap.ToPackedRows(blackIsZero: true), bitmap.BytesPerRow);
        source.Freeze();
        return source;
    }

    /// <summary>
    /// Thresholds an image to 1 bit: a pixel is black when its luminance (over a white background) is
    /// below <paramref name="threshold"/> (0–255).
    /// </summary>
    public static MonoBitmap ToMonoBitmap(BitmapSource source, int threshold = 128)
    {
        if (source.Format != PixelFormats.Pbgra32)
        {
            source = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
        }

        int width = source.PixelWidth, height = source.PixelHeight, stride = width * 4;
        var pixels = new byte[stride * height];
        source.CopyPixels(pixels, stride, 0);

        var result = new MonoBitmap(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * stride + x * 4;
                int white = 255 - pixels[i + 3]; // premultiplied: composite over white
                int b = pixels[i] + white, g = pixels[i + 1] + white, r = pixels[i + 2] + white;
                int luminance = (299 * r + 587 * g + 114 * b) / 1000;
                if (luminance < threshold)
                {
                    result[x, y] = true;
                }
            }
        }

        return result;
    }
}
