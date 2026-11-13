using System.Text;
using LabelPrinter.Core.Graphics;
using static System.FormattableString;

namespace LabelPrinter.Core.Epl;

/// <summary>Builds a raw EPL2 byte stream (commands end with a line feed).</summary>
public sealed class EplWriter
{
    private readonly MemoryStream _stream = new();

    public EplWriter Command(string command)
    {
        Write(Encoding.ASCII.GetBytes(command));
        _stream.WriteByte((byte)'\n');
        return this;
    }

    /// <summary>GW: binary graphic at (x, y). EPL expects 0 for a black dot.</summary>
    public EplWriter Graphic(int x, int y, MonoBitmap bitmap, bool invert = false)
    {
        Write(Encoding.ASCII.GetBytes(Invariant($"GW{x},{y},{bitmap.BytesPerRow},{bitmap.Height},")));
        Write(bitmap.ToPackedRows(blackIsZero: !invert));
        _stream.WriteByte((byte)'\n');
        return this;
    }

    /// <summary>A: text in one of the printer's built-in fonts (1-5, ASCII only).</summary>
    public EplWriter Text(int x, int y, int font, string text, int horizontalScale = 1, int verticalScale = 1)
    {
        return Command(Invariant($"A{x},{y},0,{font},{horizontalScale},{verticalScale},N,\"{Escape(text)}\""));
    }

    public byte[] ToArray() => _stream.ToArray();

    /// <summary>Replaces non-ASCII characters with '?' and escapes backslashes and quotes.</summary>
    public static string Escape(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char ch in text)
        {
            if (ch is '\\' or '"')
            {
                sb.Append('\\').Append(ch);
            }
            else if (ch < 32 || ch > 126)
            {
                sb.Append('?');
            }
            else
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    private void Write(byte[] bytes) => _stream.Write(bytes, 0, bytes.Length);
}
