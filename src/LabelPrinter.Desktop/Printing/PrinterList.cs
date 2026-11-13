using System.Printing;

namespace LabelPrinter.Desktop.Printing;

internal static class PrinterList
{
    /// <summary>Names of the printers installed in Windows, as accepted by OpenPrinter.</summary>
    public static IReadOnlyList<string> GetInstalled()
    {
        var names = new List<string>();
        using var server = new LocalPrintServer();
        var queues = server.GetPrintQueues(new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections });
        foreach (PrintQueue queue in queues)
        {
            names.Add(queue.FullName);
            queue.Dispose();
        }

        names.Sort(StringComparer.CurrentCultureIgnoreCase);
        return names;
    }
}
