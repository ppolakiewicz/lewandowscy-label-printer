using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace LabelPrinter.Desktop.Printing;

/// <summary>
/// Sends bytes to a Windows printer unchanged (RAW data type), so the printer receives our EPL2
/// commands directly. Works with the built-in "Generic / Text Only" driver; no Zebra driver needed.
/// </summary>
internal static class RawPrinter
{
    public static void Send(string printerName, byte[] data, string documentName)
    {
        if (!NativeMethods.OpenPrinter(printerName, out var handle, IntPtr.Zero))
        {
            throw Win32Error($"Nie można otworzyć drukarki „{printerName}”");
        }

        try
        {
            var doc = new NativeMethods.DocInfo1 { DocName = documentName, DataType = "RAW" };
            if (NativeMethods.StartDocPrinter(handle, 1, doc) == 0)
            {
                throw Win32Error("Nie można rozpocząć wydruku");
            }

            try
            {
                if (!NativeMethods.StartPagePrinter(handle))
                {
                    throw Win32Error("Nie można rozpocząć strony");
                }

                try
                {
                    if (!NativeMethods.WritePrinter(handle, data, data.Length, out int written))
                    {
                        throw Win32Error("Nie można wysłać danych do drukarki");
                    }

                    if (written != data.Length)
                    {
                        throw new IOException($"Wysłano tylko {written} z {data.Length} bajtów.");
                    }
                }
                finally
                {
                    NativeMethods.EndPagePrinter(handle);
                }
            }
            finally
            {
                NativeMethods.EndDocPrinter(handle);
            }
        }
        finally
        {
            NativeMethods.ClosePrinter(handle);
        }
    }

    private static Win32Exception Win32Error(string message)
    {
        int code = Marshal.GetLastWin32Error();
        return new Win32Exception(code, $"{message}: {new Win32Exception(code).Message}");
    }

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public sealed class DocInfo1
        {
            public string? DocName;
            public string? OutputFile;
            public string? DataType;
        }

        [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool OpenPrinter(string printerName, out IntPtr printer, IntPtr defaults);

        [DllImport("winspool.drv", SetLastError = true)]
        public static extern bool ClosePrinter(IntPtr printer);

        [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int StartDocPrinter(IntPtr printer, int level, [In] DocInfo1 docInfo);

        [DllImport("winspool.drv", SetLastError = true)]
        public static extern bool EndDocPrinter(IntPtr printer);

        [DllImport("winspool.drv", SetLastError = true)]
        public static extern bool StartPagePrinter(IntPtr printer);

        [DllImport("winspool.drv", SetLastError = true)]
        public static extern bool EndPagePrinter(IntPtr printer);

        [DllImport("winspool.drv", SetLastError = true)]
        public static extern bool WritePrinter(IntPtr printer, byte[] buffer, int count, out int written);
    }
}
