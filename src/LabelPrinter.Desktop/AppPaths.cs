using System.IO;

namespace LabelPrinter.Desktop;

internal static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LabelPrinter");

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");
}
