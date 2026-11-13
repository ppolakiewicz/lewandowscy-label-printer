using System.Text.Json;

namespace LabelPrinter.Core;

public static class PrinterConfigStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>Loads settings; a missing or unreadable file gives the defaults.</summary>
    public static PrinterConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            return new PrinterConfig();
        }

        try
        {
            return (JsonSerializer.Deserialize<PrinterConfig>(File.ReadAllText(path), Options) ?? new PrinterConfig())
                .Normalized();
        }
        catch (JsonException)
        {
            return new PrinterConfig();
        }
    }

    public static void Save(string path, PrinterConfig config)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(config, Options));
        File.Move(temp, path, overwrite: true);
    }
}
