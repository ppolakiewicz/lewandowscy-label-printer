namespace LabelPrinter.Core.Tests;

public sealed class PrinterConfigStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "label-printer-tests-" + Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_dir, "sub", "settings.json");

    [Fact]
    public void Load_MissingFile_GivesDefaults()
    {
        var config = PrinterConfigStore.Load(SettingsPath);

        Assert.Equal(50, config.LabelWidthMm);
        Assert.Equal(30, config.LabelHeightMm);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        PrinterConfigStore.Save(SettingsPath, new PrinterConfig { PrinterName = "Zebra", OffsetXMm = -1.5, Darkness = 12 });

        var config = PrinterConfigStore.Load(SettingsPath);

        Assert.Equal("Zebra", config.PrinterName);
        Assert.Equal(-1.5, config.OffsetXMm);
        Assert.Equal(12, config.Darkness);
    }

    [Fact]
    public void Load_OldFileWithoutPrintWidth_UsesLp2844Width()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, "{ \"LabelWidthMm\": 50, \"Darkness\": 9 }");

        var config = PrinterConfigStore.Load(SettingsPath);

        Assert.Equal(104, config.PrintWidthMm);
        Assert.Equal(RollGuide.Center, config.RollGuide);
        Assert.Equal(216, config.LeftMarginDots);
    }

    [Theory]
    [InlineData(RollGuide.Center)]
    [InlineData(RollGuide.LeftEdge)]
    [InlineData(RollGuide.RightEdge)]
    public void SaveThenLoad_KeepsRollGuide(RollGuide guide)
    {
        PrinterConfigStore.Save(SettingsPath, new PrinterConfig { RollGuide = guide });

        Assert.Contains($"\"{guide}\"", File.ReadAllText(SettingsPath));
        Assert.Equal(guide, PrinterConfigStore.Load(SettingsPath).RollGuide);
    }

    [Fact]
    public void Load_CorruptFile_GivesDefaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, "{ not json");

        Assert.Equal(8, PrinterConfigStore.Load(SettingsPath).Darkness);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}
