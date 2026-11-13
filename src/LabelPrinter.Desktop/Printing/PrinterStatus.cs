using System.Printing;

namespace LabelPrinter.Desktop.Printing;

/// <summary>What Windows knows about a printer queue.</summary>
/// <param name="Installed">False when no installed printer has this name.</param>
/// <param name="Problem">A Polish description of a reported problem (offline, paused, error...), or null.</param>
internal sealed record PrinterState(bool Installed, string? Problem);

internal static class PrinterStatus
{
    /// <summary>
    /// Looks the printer up in Windows. If the status cannot be read the printer is reported as installed without
    /// problems, so a broken status query never blocks printing.
    /// </summary>
    public static PrinterState Check(string printerName)
    {
        try
        {
            using var server = new LocalPrintServer();
            var queues = server.GetPrintQueues(new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections });
            PrintQueue? match = null;
            foreach (PrintQueue queue in queues)
            {
                if (match is null && string.Equals(queue.FullName, printerName, StringComparison.OrdinalIgnoreCase))
                {
                    match = queue;
                }
                else
                {
                    queue.Dispose();
                }
            }

            if (match is null)
            {
                return new PrinterState(false, null);
            }

            using (match)
            {
                match.Refresh();
                return new PrinterState(true, Describe(match));
            }
        }
        catch (Exception)
        {
            return new PrinterState(true, null);
        }
    }

    private static string? Describe(PrintQueue queue)
    {
        if (queue.IsOffline || queue.IsNotAvailable)
        {
            return "drukarka jest niedostępna (wyłączona albo odłączony kabel USB)";
        }

        if (queue.IsPaused)
        {
            return "drukowanie jest wstrzymane w Windows";
        }

        if (queue.IsOutOfPaper)
        {
            return "w drukarce skończyły się etykiety";
        }

        if (queue.IsPaperJammed)
        {
            return "zacięte etykiety";
        }

        if (queue.IsDoorOpened)
        {
            return "otwarta pokrywa drukarki";
        }

        if (queue.IsInError || queue.NeedUserIntervention)
        {
            return "drukarka zgłasza błąd";
        }

        return null;
    }
}
