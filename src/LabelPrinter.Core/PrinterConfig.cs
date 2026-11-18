using System.Text.Json.Serialization;

namespace LabelPrinter.Core;

/// <summary>Where the printer holds a roll narrower than its print head.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RollGuide>))]
public enum RollGuide
{
    /// <summary>In the middle of the head (LP2844).</summary>
    Center,

    /// <summary>Against the edge where the head's dots start (x = 0).</summary>
    LeftEdge,

    /// <summary>Against the opposite edge of the head.</summary>
    RightEdge,
}

/// <summary>Printer and label settings, persisted between runs.</summary>
public sealed class PrinterConfig
{
    public const int MinDarkness = 0;
    public const int MaxDarkness = 15;
    public const int MinSpeed = 1;
    public const int MaxSpeed = 4;
    public const double MaxOffsetMm = 10;
    public const double MinPrintWidthMm = 20;
    public const double MaxPrintWidthMm = 220;

    /// <summary>LP2844: 832 dots at 203 dpi = 104 mm.</summary>
    public const double DefaultPrintWidthMm = 104;

    public string? PrinterName { get; set; }
    public double LabelWidthMm { get; set; } = 50;
    public double LabelHeightMm { get; set; } = 30;

    /// <summary>
    /// Full print width of the head. A narrower label sits where <see cref="RollGuide"/> says; the print area
    /// is moved there by <see cref="LeftMarginDots"/>.
    /// </summary>
    public double PrintWidthMm { get; set; } = DefaultPrintWidthMm;

    /// <summary>Where the roll runs on the head. Settings saved before this option existed mean the LP2844's centre.</summary>
    public RollGuide RollGuide { get; set; } = RollGuide.Center;
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
    [JsonIgnore] public int PrintWidthDots => Units.MmToDots(PrintWidthMm);

    /// <summary>Distance from the head's left edge to the label's left edge, depending on <see cref="RollGuide"/>.</summary>
    [JsonIgnore]
    public int LeftMarginDots
    {
        get
        {
            int free = Math.Max(0, PrintWidthDots - LabelWidthDots);
            return RollGuide switch
            {
                RollGuide.LeftEdge => 0,
                RollGuide.RightEdge => free,
                _ => free / 2,
            };
        }
    }

    /// <summary>Same as <see cref="LeftMarginDots"/> in millimetres (for display).</summary>
    [JsonIgnore] public double LeftMarginMm => (double)LeftMarginDots / Units.DotsPerMm;

    [JsonIgnore] public int GapDots => Units.MmToDots(GapMm);
    [JsonIgnore] public int OffsetXDots => Units.MmToDots(OffsetXMm);
    [JsonIgnore] public int OffsetYDots => Units.MmToDots(OffsetYMm);

    /// <summary>Returns a copy with every value clamped to what the printer accepts.</summary>
    public PrinterConfig Normalized()
    {
        var c = (PrinterConfig)MemberwiseClone();
        c.PrintWidthMm = Math.Clamp(Finite(PrintWidthMm, DefaultPrintWidthMm), MinPrintWidthMm, MaxPrintWidthMm);
        c.LabelWidthMm = Math.Clamp(Finite(LabelWidthMm, 50), 10, c.PrintWidthMm);
        c.LabelHeightMm = Math.Clamp(Finite(LabelHeightMm, 30), 5, 300);
        c.GapMm = Math.Clamp(Finite(GapMm, 3), 0, 20);
        c.OffsetXMm = Math.Clamp(Finite(OffsetXMm, 0), -MaxOffsetMm, MaxOffsetMm);
        c.OffsetYMm = Math.Clamp(Finite(OffsetYMm, 0), -MaxOffsetMm, MaxOffsetMm);
        c.Darkness = Math.Clamp(Darkness, MinDarkness, MaxDarkness);
        c.Speed = Math.Clamp(Speed, MinSpeed, MaxSpeed);
        c.RollGuide = Enum.IsDefined(RollGuide) ? RollGuide : RollGuide.Center;
        return c;
    }

    /// <summary>True when every stored setting equals the other's (used to detect unsaved changes).</summary>
    public bool SameSettings(PrinterConfig other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return PrinterName == other.PrinterName
            && LabelWidthMm.Equals(other.LabelWidthMm)
            && LabelHeightMm.Equals(other.LabelHeightMm)
            && PrintWidthMm.Equals(other.PrintWidthMm)
            && RollGuide == other.RollGuide
            && GapMm.Equals(other.GapMm)
            && OffsetXMm.Equals(other.OffsetXMm)
            && OffsetYMm.Equals(other.OffsetYMm)
            && Darkness == other.Darkness
            && Speed == other.Speed
            && PrintTopFirst == other.PrintTopFirst
            && InvertGraphics == other.InvertGraphics;
    }

    private static double Finite(double value, double fallback) => double.IsFinite(value) ? value : fallback;
}
