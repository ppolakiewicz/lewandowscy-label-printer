namespace LabelPrinter.Core.Graphics;

/// <summary>A 1-bit image in printer dots; <c>true</c> is a black (printed) dot.</summary>
public sealed class MonoBitmap
{
    private readonly bool[] _pixels;

    public MonoBitmap(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
        _pixels = new bool[width * height];
    }

    public int Width { get; }
    public int Height { get; }
    public int BytesPerRow => (Width + 7) / 8;

    public bool this[int x, int y]
    {
        get
        {
            CheckBounds(x, y);
            return _pixels[y * Width + x];
        }
        set
        {
            CheckBounds(x, y);
            _pixels[y * Width + x] = value;
        }
    }

    public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    /// <summary>Sets a pixel; coordinates outside the image are ignored.</summary>
    public void SetPixel(int x, int y, bool black = true)
    {
        if (Contains(x, y))
        {
            _pixels[y * Width + x] = black;
        }
    }

    /// <summary>Fills a rectangle, clipped to the image.</summary>
    public void FillRect(int x, int y, int width, int height, bool black = true)
    {
        int x0 = Math.Max(0, x), y0 = Math.Max(0, y);
        int x1 = Math.Min(Width, x + width), y1 = Math.Min(Height, y + height);
        for (int yy = y0; yy < y1; yy++)
        {
            for (int xx = x0; xx < x1; xx++)
            {
                _pixels[yy * Width + xx] = black;
            }
        }
    }

    public void DrawRect(int x, int y, int width, int height, int thickness = 1)
    {
        FillRect(x, y, width, thickness);
        FillRect(x, y + height - thickness, width, thickness);
        FillRect(x, y, thickness, height);
        FillRect(x + width - thickness, y, thickness, height);
    }

    /// <summary>Returns a same-size copy with the content moved by (dx, dy); what falls outside is lost.</summary>
    public MonoBitmap Shifted(int dx, int dy)
    {
        var result = new MonoBitmap(Width, Height);
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (_pixels[y * Width + x])
                {
                    result.SetPixel(x + dx, y + dy);
                }
            }
        }

        return result;
    }

    public int CountBlack() => _pixels.Count(p => p);

    /// <summary>
    /// Packs the image row by row, most significant bit first. With <paramref name="blackIsZero"/> a black
    /// dot is a 0 bit (EPL GW and WPF BlackWhite); otherwise it is a 1 bit. Row padding is always white.
    /// </summary>
    public byte[] ToPackedRows(bool blackIsZero)
    {
        int stride = BytesPerRow;
        var data = new byte[stride * Height];
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < stride * 8; x++)
            {
                bool black = x < Width && _pixels[y * Width + x];
                if (black != blackIsZero)
                {
                    data[y * stride + x / 8] |= (byte)(0x80 >> (x % 8));
                }
            }
        }

        return data;
    }

    private void CheckBounds(int x, int y)
    {
        if (!Contains(x, y))
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"Pixel ({x}, {y}) is outside {Width}x{Height}.");
        }
    }
}
