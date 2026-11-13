using System.Globalization;
using System.Text;

namespace LabelPrinter.Core.Storage;

/// <summary>Text folding used for the database key and for search.</summary>
public static class TextNormalizer
{
    /// <summary>
    /// Lower-case, Polish (and other Latin) diacritics removed (ą→a, ł→l, ż→z...), every run of whitespace
    /// (including line breaks) replaced by one space, trimmed. "  Żółta\nŁÓDKA " → "zolta lodka".
    /// </summary>
    public static string FoldForSearch(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        var sb = new StringBuilder(text.Length);
        var needsDecomposition = false;
        foreach (var ch in text)
        {
            var c = MapPolish(char.ToLowerInvariant(ch));
            needsDecomposition |= c > '\u007f';
            sb.Append(c);
        }

        var folded = sb.ToString();
        if (needsDecomposition)
        {
            folded = StripMarks(folded);
        }

        return CollapseWhitespace(folded);
    }

    /// <summary>
    /// Like <see cref="FoldForSearch"/> but every character that is not a letter or digit becomes a space,
    /// so that word starts after '-', '_', '/', '(' etc. can be found by looking for " " + word.
    /// </summary>
    public static string FoldWords(string? text)
    {
        var folded = FoldForSearch(text);
        var sb = new StringBuilder(folded.Length);
        foreach (var c in folded)
        {
            sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        return CollapseWhitespace(sb.ToString());
    }

    /// <summary>
    /// The uniqueness key of a text: lower-case, whitespace inside each line collapsed to one space, lines trimmed,
    /// empty lines dropped, joined with '\n'. Diacritics are kept ("Łódź" and "Lodz" are different entries).
    /// </summary>
    public static string DedupeKey(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Select(l => CollapseWhitespace(l.ToLowerInvariant()))
            .Where(l => l.Length > 0);
        return string.Join('\n', lines);
    }

    private static char MapPolish(char c) => c switch
    {
        'ą' => 'a',
        'ć' => 'c',
        'ę' => 'e',
        'ł' => 'l',
        'ń' => 'n',
        'ó' => 'o',
        'ś' => 's',
        'ź' or 'ż' => 'z',
        _ => c,
    };

    private static string StripMarks(string text)
    {
        string decomposed;
        try
        {
            decomposed = text.Normalize(NormalizationForm.FormD);
        }
        catch (Exception e) when (e is PlatformNotSupportedException or ArgumentException)
        {
            return text;
        }

        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static string CollapseWhitespace(string text)
    {
        var sb = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = sb.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                sb.Append(' ');
                pendingSpace = false;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }
}
