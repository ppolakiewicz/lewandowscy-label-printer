using System.Text;
using LabelPrinter.Core.Layout;
using LabelPrinter.Core.Storage;

namespace LabelPrinter.Core.Tests.Storage;

public sealed class LabelTextStoreCsvTests : StoreTestBase
{
    private static LabelContent Auto(string text) => LabelContent.FromText(text);

    private static CsvImportResult Import(LabelTextStore store, string csv, Encoding? encoding = null) =>
        store.ImportCsv(new MemoryStream((encoding ?? new UTF8Encoding(true)).GetPreamble()
            .Concat((encoding ?? Encoding.UTF8).GetBytes(csv)).ToArray()));

    private static byte[] Export(LabelTextStore store)
    {
        var stream = new MemoryStream();
        store.ExportCsv(stream);
        return stream.ToArray();
    }

    private void FillTrickyEntries(LabelTextStore store)
    {
        store.RecordUse(Auto("Zwykły tekst"));
        Time.Advance(TimeSpan.FromSeconds(10));
        var tricky = Auto("Średnik; \"cudzysłów\" i , przecinek") with { Bold = true, FontSizeDots = 37.5 };
        store.RecordUse(tricky);
        store.RecordUse(tricky);
        Time.Advance(TimeSpan.FromMinutes(3));
        store.RecordUse(new LabelContent
        {
            Mode = LayoutMode.Manual,
            Lines = ["Zażółć gęślą", "JAŹŃ", "trzecia"],
            Alignment = LabelAlignment.Left,
            FontFamily = "Times New Roman",
        });
        store.Add(Auto(" spacje dookoła "), "notatka; z \"cudzysłowem\"\ni nową linią");
    }

    [Fact]
    public void Export_IsUtf8WithBomSemicolonsAndHeader()
    {
        using var store = Open();
        FillTrickyEntries(store);

        var bytes = Export(store);

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        var text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        Assert.StartsWith(
            "Tekst;Tryb;Wyrównanie;Pogrubienie;Rozmiar czcionki;Czcionka;Liczba użyć;Ostatnie użycie (UTC);Dodano (UTC);Notatka\r\n",
            text);
        Assert.Contains("\"Średnik; \"\"cudzysłów\"\" i , przecinek\";Auto;Center;tak;37,5;Arial;2;2026-03-01 10:00:10;", text);
        Assert.Contains("\"Zażółć gęślą\nJAŹŃ\ntrzecia\";Manual;Left;nie;;Times New Roman;1;", text);
        Assert.Contains("Zwykły tekst;Auto;Center;nie;;Arial;1;2026-03-01 10:00:00;2026-03-01 10:00:00;\r\n", text);
    }

    [Fact]
    public void ExportImport_RoundTripsIntoEmptyDatabase()
    {
        using var source = Open();
        FillTrickyEntries(source);
        var csvPath = Path.Combine(Dir, "out", "etykiety.csv");
        source.ExportCsv(csvPath);

        using var target = Open(Path.Combine(Dir, "other.db"));
        var result = target.ImportCsv(csvPath);

        Assert.Equal((4, 0, 0), (result.Added, result.Updated, result.Skipped));
        Assert.Empty(result.Issues);
        foreach (var expected in source.GetAll())
        {
            var actual = Assert.Single(target.GetAll(), l => l.Text == expected.Text);
            Assert.Equal(expected.Content.Mode, actual.Content.Mode);
            Assert.Equal(expected.Content.Lines, actual.Content.Lines);
            Assert.Equal(expected.Content.Alignment, actual.Content.Alignment);
            Assert.Equal(expected.Content.Bold, actual.Content.Bold);
            Assert.Equal(expected.Content.FontSizeDots, actual.Content.FontSizeDots);
            Assert.Equal(expected.Content.FontFamily, actual.Content.FontFamily);
            Assert.Equal(expected.Note, actual.Note);
            Assert.Equal(expected.UseCount, actual.UseCount);
            Assert.Equal(expected.LastUsedUtc, actual.LastUsedUtc); // whole seconds here
            Assert.Equal(expected.CreatedUtc, actual.CreatedUtc);
        }
    }

    [Fact]
    public void Import_OwnExportAgain_SkipsEverything()
    {
        using var store = Open();
        FillTrickyEntries(store);
        Time.Advance(TimeSpan.FromMilliseconds(1234)); // sub-second timestamps are not exported
        store.RecordUse(Auto("Zwykły tekst"));

        var result = store.ImportCsv(new MemoryStream(Export(store)));

        Assert.Equal(0, result.Added);
        Assert.Equal(0, result.Updated);
        Assert.Equal(4, result.Skipped);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Import_MergesByTextAndMode_AndCounts()
    {
        using var store = Open();
        var ser = store.Add(Auto("Ser żółty"), "stara notatka");
        for (var i = 0; i < 5; i++)
        {
            store.RecordUse(Auto("Ser żółty"));
        }

        var mleko = store.RecordUse(Auto("Mleko") with { Bold = true });

        var csv =
            "Tekst;Pogrubienie;Liczba użyć;Notatka\r\n" +
            "SER  ŻÓŁTY;tak;2;nowa notatka\r\n" +   // updated: bold + note; use count stays 5
            "Nowy tekst;nie;3;\r\n" +               // added
            ";tak;1;\r\n" +                         // invalid: empty text
            "Mleko;tak;1;\r\n" +                    // unchanged
            "Nowy tekst;nie;3;\r\n" +               // duplicate of a row above: unchanged
            "Inny;może;1;\r\n";                     // invalid bold value

        var result = Import(store, csv);

        Assert.Equal(1, result.Added);
        Assert.Equal(1, result.Updated);
        Assert.Equal(4, result.Skipped);
        Assert.Equal(new[] { 4, 7 }, result.Issues.Select(i => i.Row));

        var updated = store.Get(ser.Id)!;
        Assert.True(updated.Content.Bold);
        Assert.Equal("nowa notatka", updated.Note);
        Assert.Equal(5, updated.UseCount);
        Assert.Equal("SER  ŻÓŁTY", updated.Text); // the imported spelling wins
        Assert.Equal(ser.CreatedUtc, updated.CreatedUtc);
        Assert.Equal(mleko.UseCount, store.Get(mleko.Id)!.UseCount);
        var added = Assert.Single(store.Search("nowy"));
        Assert.Equal(3, added.UseCount);
        Assert.Null(added.LastUsedUtc);
        Assert.Equal(3, store.Count());
    }

    [Fact]
    public void Import_MissingColumns_KeepStoredLayout()
    {
        using var store = Open();
        var saved = store.RecordUse(Auto("Ser") with { Bold = true, FontSizeDots = 20, Alignment = LabelAlignment.Left });
        store.Update(saved.Id, saved.Content, "notka");

        var result = Import(store, "Tekst\r\nSer\r\n");

        Assert.Equal(1, result.Skipped);
        var after = store.Get(saved.Id)!;
        Assert.True(after.Content.Bold);
        Assert.Equal(20.0, after.Content.FontSizeDots);
        Assert.Equal(LabelAlignment.Left, after.Content.Alignment);
        Assert.Equal("notka", after.Note);
    }

    [Fact]
    public void Import_LineBreakWithoutMode_IsManual()
    {
        using var store = Open();

        Import(store, "Tekst\r\n\"Linia 1\r\nLinia 2\"\r\n");

        var saved = Assert.Single(store.GetAll());
        Assert.Equal(LayoutMode.Manual, saved.Content.Mode);
        Assert.Equal(new[] { "Linia 1", "Linia 2" }, saved.Content.Lines);
    }

    [Fact]
    public void Import_PlainListWithoutHeader()
    {
        using var store = Open();

        var result = Import(store, "Ser\nMleko\n\nMasło\n", new UTF8Encoding(false));

        Assert.Equal(3, result.Added);
        Assert.Equal(new[] { "Masło", "Mleko", "Ser" }, store.GetAll().Select(l => l.Text).Order().ToArray());
    }

    [Fact]
    public void Import_CommaSeparatedEnglishHeader()
    {
        using var store = Open();

        var result = Import(store, "text,mode,alignment,bold,font size,font family,use count,last used,note\n" +
            "\"Hello, world\",Manual,Left,1,\"12,5\",Verdana,4,2025-12-24 18:30:00,x\n");

        Assert.Equal(1, result.Added);
        var saved = Assert.Single(store.GetAll());
        Assert.Equal("Hello, world", saved.Text);
        Assert.Equal(LayoutMode.Manual, saved.Content.Mode);
        Assert.Equal(LabelAlignment.Left, saved.Content.Alignment);
        Assert.True(saved.Content.Bold);
        Assert.Equal(12.5, saved.Content.FontSizeDots);
        Assert.Equal("Verdana", saved.Content.FontFamily);
        Assert.Equal(4, saved.UseCount);
        Assert.Equal(new DateTime(2025, 12, 24, 18, 30, 0, DateTimeKind.Utc), saved.LastUsedUtc);
        Assert.Equal("x", saved.Note);
    }

    [Fact]
    public void Import_Windows1250FromExcel()
    {
        using var store = Open();
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var bytes = Encoding.GetEncoding(1250).GetBytes("Tekst;Tryb\r\nŻółta łódź;ręczny\r\n");

        var result = store.ImportCsv(new MemoryStream(bytes));

        Assert.Equal(1, result.Added);
        var saved = Assert.Single(store.Search("zolta lodz"));
        Assert.Equal("Żółta łódź", saved.Text);
        Assert.Equal(LayoutMode.Manual, saved.Content.Mode);
    }

    [Fact]
    public void Import_InvalidValues_ReportedAndNothingElseLost()
    {
        using var store = Open();

        var result = Import(store, "Tekst;Tryb;Rozmiar czcionki;Ostatnie użycie (UTC)\r\n" +
            "A;dziwny;;\r\n" +
            "B;Auto;-3;\r\n" +
            "C;Auto;;wczoraj\r\n" +
            "D;Auto;10;\r\n");

        Assert.Equal(1, result.Added);
        Assert.Equal(3, result.Skipped);
        Assert.Equal(new[] { 2, 3, 4 }, result.Issues.Select(i => i.Row));
        Assert.Equal("D", Assert.Single(store.GetAll()).Text);
    }

    [Fact]
    public void Import_EmptyFile_NoChanges()
    {
        using var store = Open();

        var result = Import(store, "");

        Assert.Equal((0, 0, 0), (result.Added, result.Updated, result.Skipped));
        Assert.Empty(result.Issues);
        Assert.Equal(0, store.Count());
    }
}
