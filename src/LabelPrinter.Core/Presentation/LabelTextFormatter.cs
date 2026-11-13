using System.Globalization;
using LabelPrinter.Core.Layout;
using LabelPrinter.Core.Storage;

namespace LabelPrinter.Core.Presentation;

/// <summary>How saved labels are shown in lists (suggestions, the database window), in Polish.</summary>
public static class LabelTextFormatter
{
    /// <summary>Put between the lines of a multi-line text shown on one row.</summary>
    public const string LineSeparator = " ⏎ ";

    /// <summary>The text on one row: lines joined with <see cref="LineSeparator"/>.</summary>
    public static string OneLine(LabelContent content) =>
        string.Join(LineSeparator, content.Text.Split('\n'));

    /// <summary>Short Polish name of a layout mode.</summary>
    public static string ModeName(LayoutMode mode) => mode == LayoutMode.Manual ? "Osobne linijki" : "Zawijanie";

    /// <summary>Layout settings that differ from the defaults, plus the use count, e.g. "Osobne linijki · pogrubienie · użyto 3×".</summary>
    public static string Details(SavedLabel label)
    {
        var c = label.Content;
        var parts = new List<string> { ModeName(c.Mode) };
        if (c.Alignment == LabelAlignment.Left)
        {
            parts.Add("do lewej");
        }

        if (c.Bold)
        {
            parts.Add("pogrubienie");
        }

        if (c.FontSizeDots is { } size)
        {
            parts.Add(FontSizeInput.FormatMm(FontSizeInput.DotsToMm(size)) + " mm");
        }

        if (!string.Equals(c.FontFamily, LabelContent.DefaultFontFamily, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add(c.FontFamily);
        }

        if (label.UseCount > 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"użyto {label.UseCount}×"));
        }

        return string.Join(" · ", parts);
    }

    /// <summary>A UTC time shown in local time as "yyyy-MM-dd HH:mm"; empty for null.</summary>
    public static string FormatLocal(DateTime? utc) =>
        utc is { } value
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            : "";

    /// <summary>Shortens a text for messages: one row, at most <paramref name="max"/> characters (with "…").</summary>
    public static string Shorten(string text, int max = 40)
    {
        var oneLine = string.Join(LineSeparator, text.Split('\n'));
        return oneLine.Length <= max ? oneLine : oneLine[..Math.Max(1, max - 1)].TrimEnd() + "…";
    }
}
