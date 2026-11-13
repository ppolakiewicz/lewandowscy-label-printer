using LabelPrinter.Core.Epl;
using LabelPrinter.Core.Layout;
using LabelPrinter.Core.Presentation;

namespace LabelPrinter.Desktop.Printing;

/// <summary>How a status message should look.</summary>
public enum StatusKind
{
    Info,
    Success,
    Warning,
    Error,
}

/// <summary>Result of <see cref="LabelPrintService.Print"/>: a Polish message for the status bar.</summary>
internal sealed record PrintOutcome(bool Success, string Message, StatusKind Kind)
{
    public static PrintOutcome Fail(string message) => new(false, message, StatusKind.Error);
}

/// <summary>
/// Prints a label with the current settings: renders it, sends EPL to the printer and records the text in the
/// database. Used by the main window and the database window. UI thread only (rendering uses WPF).
/// </summary>
internal sealed class LabelPrintService(AppState state)
{
    /// <summary>Lays the content out on the current label size (e.g. to warn about overflow before printing).</summary>
    public LabelLayout Layout(LabelContent content) =>
        state.Renderer.Layout(content, state.Config.LabelWidthDots, state.Config.LabelHeightDots);

    /// <summary>True when a printer is chosen in the settings.</summary>
    public bool HasPrinter => !string.IsNullOrWhiteSpace(state.Config.PrinterName);

    public PrintOutcome Print(LabelContent content, int copies)
    {
        if (content.IsEmpty)
        {
            return PrintOutcome.Fail("Wpisz tekst etykiety.");
        }

        if (copies < CopiesInput.Min || copies > CopiesInput.Max)
        {
            return PrintOutcome.Fail(CopiesInput.RangeMessage);
        }

        var config = state.Config;
        var printer = config.PrinterName;
        if (string.IsNullOrWhiteSpace(printer))
        {
            return PrintOutcome.Fail("Nie wybrano drukarki – otwórz „Ustawienia drukarki…” i wybierz drukarkę.");
        }

        var printerState = PrinterStatus.Check(printer);
        if (!printerState.Installed)
        {
            return PrintOutcome.Fail(
                $"Drukarka „{printer}” nie jest zainstalowana w Windows. Wybierz właściwą w „Ustawienia drukarki…”.");
        }

        byte[] job;
        try
        {
            var rendered = state.Renderer.Render(content, config.LabelWidthDots, config.LabelHeightDots);
            job = EplLabels.Image(config, rendered.Bitmap, copies);
        }
        catch (Exception ex)
        {
            return PrintOutcome.Fail("Nie udało się przygotować etykiety: " + ex.Message);
        }

        try
        {
            RawPrinter.Send(printer, job, "Etykieta – " + LabelTextFormatter.Shorten(content.Text));
        }
        catch (Exception ex)
        {
            return PrintOutcome.Fail($"Błąd drukowania na „{printer}”: {ex.Message}");
        }

        var message = $"{DateTime.Now:HH:mm:ss} – wysłano do drukarki: {copies} × „{LabelTextFormatter.Shorten(content.Text)}”.";
        var kind = StatusKind.Success;
        if (printerState.Problem is { } problem)
        {
            message += $" Uwaga: Windows zgłasza, że {problem} – wydruk czeka w kolejce.";
            kind = StatusKind.Warning;
        }

        if (state.Store is { } store)
        {
            try
            {
                store.RecordUse(content);
            }
            catch (Exception ex)
            {
                message += " Nie zapisano tekstu w bazie: " + ex.Message;
                kind = StatusKind.Warning;
            }
        }

        return new PrintOutcome(true, message, kind);
    }
}
