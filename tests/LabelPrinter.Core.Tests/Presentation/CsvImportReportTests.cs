using LabelPrinter.Core.Presentation;
using LabelPrinter.Core.Storage;
using LabelPrinter.Core.Tests.Storage;

namespace LabelPrinter.Core.Tests.Presentation;

public class CsvImportReportTests : StoreTestBase
{
    [Fact]
    public void Format_EmptyFile()
    {
        Assert.Equal("Plik nie zawiera żadnych tekstów.", CsvImportReport.Format(new CsvImportResult(0, 0, 0, [])));
    }

    [Fact]
    public void Format_CountsOnly()
    {
        var text = CsvImportReport.Format(new CsvImportResult(5, 2, 0, []));
        Assert.Equal("Dodano nowych: 5\nZaktualizowano: 2\nPominięto: 0", text);
    }

    [Fact]
    public void Format_UnchangedRows()
    {
        var text = CsvImportReport.Format(new CsvImportResult(0, 0, 3, []));
        Assert.EndsWith("Pominięto: 3 (już były w bazie bez zmian)", text);
    }

    [Fact]
    public void Format_ListsIssuesInPolish()
    {
        var result = new CsvImportResult(1, 0, 3, [new CsvImportIssue(4, "Empty text."), new CsvImportIssue(7, "Unknown mode \"xyz\".")]);
        var text = CsvImportReport.Format(result);
        Assert.Contains("Pominięto: 3 (bez zmian: 1, z błędami: 2)", text);
        Assert.Contains("• wiersz 4: pusty tekst", text);
        Assert.Contains("• wiersz 7: nieznany tryb „xyz”", text);
    }

    [Fact]
    public void Format_LimitsTheIssueList()
    {
        var issues = Enumerable.Range(1, 5).Select(i => new CsvImportIssue(i, "Empty text.")).ToList();
        var text = CsvImportReport.Format(new CsvImportResult(0, 0, 5, issues), maxIssues: 2);
        Assert.Contains("wiersz 2:", text);
        Assert.DoesNotContain("wiersz 3:", text);
        Assert.EndsWith("… i 3 więcej.", text);
    }

    [Theory]
    [InlineData("Unknown alignment \"x\".", "nieznane wyrównanie „x”")]
    [InlineData("Unknown bold value \"może\".", "nieznana wartość pogrubienia „może”")]
    [InlineData("Invalid font size \"-1\".", "nieprawidłowy rozmiar czcionki „-1”")]
    [InlineData("Invalid use count \"a\".", "nieprawidłowa liczba użyć „a”")]
    [InlineData("Invalid date \"32.13.2020\".", "nieprawidłowa data „32.13.2020”")]
    [InlineData("Something else.", "Something else.")]
    public void TranslateIssue_KnownMessages(string message, string expected)
    {
        Assert.Equal(expected, CsvImportReport.TranslateIssue(message));
    }

    [Fact]
    public void TranslateIssue_MatchesRealStoreMessages()
    {
        var store = Open();
        var csv = "Tekst;Tryb;Wyrównanie\n;Auto;Center\nAla;dziwny;Center\nOla;Auto;bok\n"u8.ToArray();
        var result = store.ImportCsv(new MemoryStream(csv));
        var translated = result.Issues.Select(i => CsvImportReport.TranslateIssue(i.Message)).ToList();
        Assert.Equal(new[] { "pusty tekst", "nieznany tryb „dziwny”", "nieznane wyrównanie „bok”" }, translated);
    }
}
