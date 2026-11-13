using System.Text;
using System.Text.RegularExpressions;
using LabelPrinter.Core.Storage;

namespace LabelPrinter.Core.Presentation;

/// <summary>Polish summary of a CSV import for a message box.</summary>
public static partial class CsvImportReport
{
    /// <summary>Counts, then up to <paramref name="maxIssues"/> skipped rows with the reason.</summary>
    public static string Format(CsvImportResult result, int maxIssues = 15)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Added == 0 && result.Updated == 0 && result.Skipped == 0)
        {
            return "Plik nie zawiera żadnych tekstów.";
        }

        var sb = new StringBuilder()
            .Append("Dodano nowych: ").Append(result.Added).Append('\n')
            .Append("Zaktualizowano: ").Append(result.Updated).Append('\n')
            .Append("Pominięto: ").Append(result.Skipped);
        int unchanged = result.Skipped - result.Issues.Count;
        if (unchanged > 0 && result.Issues.Count > 0)
        {
            sb.Append(" (bez zmian: ").Append(unchanged).Append(", z błędami: ").Append(result.Issues.Count).Append(')');
        }
        else if (unchanged > 0)
        {
            sb.Append(" (już były w bazie bez zmian)");
        }

        if (result.Issues.Count > 0)
        {
            sb.Append("\n\nWiersze z błędami:");
            foreach (var issue in result.Issues.Take(Math.Max(0, maxIssues)))
            {
                sb.Append("\n• wiersz ").Append(issue.Row).Append(": ").Append(TranslateIssue(issue.Message));
            }

            int more = result.Issues.Count - Math.Max(0, maxIssues);
            if (more > 0)
            {
                sb.Append("\n… i ").Append(more).Append(" więcej.");
            }
        }

        return sb.ToString();
    }

    /// <summary>Translates the (English) messages of <see cref="CsvImportIssue"/>; unknown ones are returned unchanged.</summary>
    public static string TranslateIssue(string message)
    {
        if (message == "Empty text.")
        {
            return "pusty tekst";
        }

        var match = IssuePattern().Match(message);
        if (!match.Success)
        {
            return message;
        }

        var value = match.Groups["value"].Value;
        var what = match.Groups["what"].Value switch
        {
            "Unknown mode" => "nieznany tryb",
            "Unknown alignment" => "nieznane wyrównanie",
            "Unknown bold value" => "nieznana wartość pogrubienia",
            "Invalid font size" => "nieprawidłowy rozmiar czcionki",
            "Invalid use count" => "nieprawidłowa liczba użyć",
            "Invalid date" => "nieprawidłowa data",
            _ => null,
        };
        return what is null ? message : $"{what} „{value}”";
    }

    [GeneratedRegex("^(?<what>[A-Za-z ]+) \"(?<value>.*)\"\\.$", RegexOptions.Singleline)]
    private static partial Regex IssuePattern();
}
