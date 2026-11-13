using System.Text.Json.Serialization;

namespace LabelPrinter.Core;

/// <summary>Printer and label settings, persisted between runs.</summary>
public sealed class PrinterConfig
{
    public const int MinDarkness = 0;
    public const int MaxDarkness = 15;
    public const int MinSpeed = 1;
    public const int MaxSpeed = 4;
    public const double MaxOffsetMm = 10;

    public string? PrinterName { get; set; }
    public double LabelWidthMm { get; set; } = 50;
    public double LabelHeightMm { get; set; } = 30;
    public double GapMm { get; set; } = 3;
    public double OffsetXMm { get; set; }
    public double OffsetYMm { get; set; }
    public int Darkness { get; set; } = 8;
    public int Speed { get; set; } = 2;

    /// <summary>EPL ZT (true) or ZB (false): which end of the image is printed first.</summary>
    public bool PrintTopFirst { get; set; } = true;

    /// <summary>Diagnostic switch: flips the bit meaning of GW graphics in case a printer expects the opposite.</summary>
    public bool InvertGraphics { get; set; }

    [JsonIgnore] public int LabelWidthDots => Units.MmToDots(LabelWidthMm);
    [JsonIgnore] public int LabelHeightDots => Units.MmToDots(LabelHeightMm);
    [JsonIgnore] public int GapDots => Units.MmToDots(GapMm);
    [JsonIgnore] public int OffsetXDots => Units.MmToDots(OffsetXMm);
    [JsonIgnore] public int OffsetYDots => Units.MmToDots(OffsetYMm);

    /// <summary>Returns a copy with every value clamped to what the LP2844 accepts.</summary>
    public PrinterConfig Normalized()
    {
        var c = (PrinterConfig)MemberwiseClone();
        c.LabelWidthMm = Math.Clamp(Finite(LabelWidthMm, 50), 10, 104);
        c.LabelHeightMm = Math.Clamp(Finite(LabelHeightMm, 30), 5, 300);
        c.GapMm = Math.Clamp(Finite(GapMm, 3), 0, 20);
        c.OffsetXMm = Math.Clamp(Finite(OffsetXMm, 0), -MaxOffsetMm, MaxOffsetMm);
        c.OffsetYMm = Math.Clamp(Finite(OffsetYMm, 0), -MaxOffsetMm, MaxOffsetMm);
        c.Darkness = Math.Clamp(Darkness, MinDarkness, MaxDarkness);
        c.Speed = Math.Clamp(Speed, MinSpeed, MaxSpeed);
        return c;
    }

    private static double Finite(double value, double fallback) => double.IsFinite(value) ? value : fallback;
}
