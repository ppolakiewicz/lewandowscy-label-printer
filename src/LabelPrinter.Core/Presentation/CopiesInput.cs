using System.Globalization;
using LabelPrinter.Core.Epl;

namespace LabelPrinter.Core.Presentation;

/// <summary>The number of copies typed by the user.</summary>
public static class CopiesInput
{
    public const int Min = 1;
    public const int Max = EplLabels.MaxCopies;

    /// <summary>Shown when the typed number cannot be used.</summary>
    public const string RangeMessage = "Liczba kopii: wpisz liczbę od 1 do 999.";

    /// <summary>Above this many copies the UI asks for confirmation (protects against typos such as 100).</summary>
    public const int ConfirmAbove = 20;

    /// <summary>Whole number from <see cref="Min"/> to <see cref="Max"/>, surrounding spaces allowed.</summary>
    public static bool TryParse(string? text, out int copies)
    {
        copies = 0;
        var value = text?.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > 4 || !value.All(char.IsAsciiDigit)
            || !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            || parsed < Min || parsed > Max)
        {
            return false;
        }

        copies = parsed;
        return true;
    }

    /// <summary>The value after pressing +/- (an invalid text counts as <see cref="Min"/>), kept in range.</summary>
    public static int Step(string? text, int delta) =>
        Math.Clamp((TryParse(text, out var copies) ? copies : Min) + delta, Min, Max);
}
