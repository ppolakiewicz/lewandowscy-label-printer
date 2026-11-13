using System.Globalization;

namespace LabelPrinter.Core.Presentation;

/// <summary>The manual font size typed by the user, in millimetres (8 printer dots per mm).</summary>
public static class FontSizeInput
{
    public const double MinMm = 1;
    public const double MaxMm = 26;

    /// <summary>Shown when the typed size cannot be used.</summary>
    public const string RangeMessage = "Rozmiar czcionki: wpisz liczbę od 1 do 26 (mm), np. 5 albo 4,5.";

    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    /// <summary>Parses "5", "4,5", "4.5" or "4,5 mm"; false when not a number or outside <see cref="MinMm"/>–<see cref="MaxMm"/>.</summary>
    public static bool TryParseMm(string? text, out double mm)
    {
        mm = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim();
        if (value.EndsWith("mm", StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^2];
        }

        value = value.Replace(" ", "").Replace("\u00a0", "").Replace(',', '.');
        if (!double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed)
            || !double.IsFinite(parsed) || parsed < MinMm || parsed > MaxMm)
        {
            return false;
        }

        mm = parsed;
        return true;
    }

    public static double MmToDots(double mm) => mm * Units.DotsPerMm;

    public static double DotsToMm(double dots) => dots / Units.DotsPerMm;

    /// <summary>Polish number format with up to three decimals: 4.625 → "4,625", 5 → "5".</summary>
    public static string FormatMm(double mm) => mm.ToString("0.###", Polish);

    /// <summary>Rounds to the nearest half millimetre within the allowed range (used to prefill the manual size).</summary>
    public static double RoundToHalfMm(double mm) =>
        Math.Clamp(Math.Round(mm * 2, MidpointRounding.AwayFromZero) / 2, MinMm, MaxMm);
}
