namespace LabelPrinter.Core;

/// <summary>Unit conversions for a 203 dpi print head (8 dots per millimetre).</summary>
public static class Units
{
    public const int DotsPerMm = 8;

    public static int MmToDots(double mm) =>
        (int)Math.Round(mm * DotsPerMm, MidpointRounding.AwayFromZero);
}
