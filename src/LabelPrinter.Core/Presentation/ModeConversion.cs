using LabelPrinter.Core.Layout;

namespace LabelPrinter.Core.Presentation;

/// <summary>Carries the text over when the user switches between one wrapped text and separate lines.</summary>
public static class ModeConversion
{
    /// <summary>
    /// Separate lines → one text: non-empty lines trimmed and joined with a space, except after a line that ends
    /// with '-' or '_' (the wrapping engine breaks there without a space, so no space is added back).
    /// </summary>
    public static string ToAutoText(IEnumerable<string?> lines)
    {
        var result = "";
        foreach (var line in lines.Select(l => (l ?? "").Trim()).Where(l => l.Length > 0))
        {
            if (result.Length > 0 && result[^1] is not ('-' or '_'))
            {
                result += " ";
            }

            result += line;
        }

        return result;
    }

    /// <summary>
    /// One text → exactly <see cref="LabelContent.MaxLines"/> line texts (unused ones empty). When
    /// <paramref name="wrappedLines"/> (the lines shown in the preview) contain the same characters as
    /// <paramref name="text"/>, they are used, so the label looks the same after switching; otherwise the whole text
    /// goes to the first line. Lines beyond the limit are joined into the last one.
    /// </summary>
    public static IReadOnlyList<string> ToManualLines(string? text, IReadOnlyList<string>? wrappedLines)
    {
        var normalized = string.Join(' ', (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var lines = new List<string>();
        if (normalized.Length > 0)
        {
            var wrapped = wrappedLines?.Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            if (wrapped is { Count: > 0 } && WithoutSpaces(string.Concat(wrapped)) == WithoutSpaces(normalized))
            {
                lines.AddRange(wrapped);
            }
            else
            {
                lines.Add(normalized);
            }
        }

        if (lines.Count > LabelContent.MaxLines)
        {
            var rest = ToAutoText(lines.Skip(LabelContent.MaxLines - 1));
            lines = [.. lines.Take(LabelContent.MaxLines - 1), rest];
        }

        while (lines.Count < LabelContent.MaxLines)
        {
            lines.Add("");
        }

        return lines;
    }

    private static string WithoutSpaces(string text) => string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
}
