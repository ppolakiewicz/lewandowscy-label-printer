using LabelPrinter.Core.Layout;

namespace LabelPrinter.Core.Presentation;

/// <summary>What the preview says about a layout.</summary>
/// <param name="Info">Font size and number of lines.</param>
/// <param name="Warning">A problem to show prominently, or null.</param>
/// <param name="IsSerious">True when the label will not print correctly (text cut off).</param>
public sealed record LayoutSummary(string Info, string? Warning, bool IsSerious);

/// <summary>Polish description of a <see cref="LabelLayout"/> for the preview.</summary>
public static class LayoutDescription
{
    public const string OverflowAuto =
        "Tekst nie mieści się na etykiecie nawet najmniejszą czcionką – skróć go albo podziel na linijki.";

    public const string OverflowFixed =
        "Tekst nie mieści się na etykiecie przy tym rozmiarze czcionki – zmniejsz rozmiar albo wybierz „Automatyczny”.";

    public const string WordBroken =
        "Za długie słowo zostało podzielone w środku – rozważ krótszy tekst albo osobne linijki.";

    public static LayoutSummary Describe(LabelLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (layout.IsEmpty)
        {
            return new LayoutSummary("Pusta etykieta – wpisz tekst.", null, false);
        }

        var size = FontSizeInput.FormatMm(Math.Round(FontSizeInput.DotsToMm(layout.FontSizeDots), 2));
        var how = layout.AutoSized ? "dobrana automatycznie" : "ustawiona ręcznie";
        var info = $"Czcionka {size} mm ({how}), linijek: {layout.Lines.Count}";

        if (layout.Overflow)
        {
            return new LayoutSummary(info, layout.AutoSized ? OverflowAuto : OverflowFixed, true);
        }

        return layout.WordBroken ? new LayoutSummary(info, WordBroken, false) : new LayoutSummary(info, null, false);
    }
}
