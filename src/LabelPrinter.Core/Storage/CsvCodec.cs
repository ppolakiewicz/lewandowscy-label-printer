using System.Text;

namespace LabelPrinter.Core.Storage;

/// <summary>Minimal RFC 4180 CSV reader/writer with a configurable separator.</summary>
internal static class CsvCodec
{
    /// <summary>Writes one record terminated by CRLF; fields are quoted only when needed.</summary>
    public static void WriteRecord(TextWriter writer, IEnumerable<string?> fields, char separator)
    {
        var first = true;
        foreach (var field in fields)
        {
            if (!first)
            {
                writer.Write(separator);
            }

            first = false;
            writer.Write(Quote(field ?? "", separator));
        }

        writer.Write("\r\n");
    }

    /// <summary>Quotes a field if it contains the separator, a quote, a line break or leading/trailing spaces.</summary>
    public static string Quote(string field, char separator)
    {
        var needsQuotes = field.Length > 0
            && (field.IndexOfAny([separator, '"', '\r', '\n']) >= 0 || char.IsWhiteSpace(field[0]) || char.IsWhiteSpace(field[^1]));
        return needsQuotes ? "\"" + field.Replace("\"", "\"\"") + "\"" : field;
    }

    /// <summary>
    /// Guesses the separator from the first line (outside quotes): ';' unless the line has more ',' or tabs.
    /// </summary>
    public static char DetectSeparator(string text)
    {
        int semicolons = 0, commas = 0, tabs = 0;
        var inQuotes = false;
        foreach (var c in text)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (!inQuotes && (c == '\n' || c == '\r'))
            {
                break;
            }
            else if (!inQuotes)
            {
                switch (c)
                {
                    case ';': semicolons++; break;
                    case ',': commas++; break;
                    case '\t': tabs++; break;
                }
            }
        }

        if (tabs > semicolons && tabs >= commas)
        {
            return '\t';
        }

        return commas > semicolons ? ',' : ';';
    }

    /// <summary>
    /// Parses the whole text into records. Accepts CRLF, LF or CR line ends; line breaks inside quoted fields
    /// are returned as '\n'. Completely empty lines are skipped. Each record carries its 1-based starting line.
    /// </summary>
    public static List<(int Line, List<string> Fields)> Parse(string text, char separator)
    {
        var records = new List<(int, List<string>)>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var fieldWasQuoted = false;
        var line = 1;
        var recordLine = 1;
        var i = 0;

        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            i = 1;
        }

        void EndField()
        {
            fields.Add(field.ToString());
            field.Clear();
            fieldWasQuoted = false;
        }

        void EndRecord()
        {
            EndField();
            if (!(fields.Count == 1 && fields[0].Length == 0))
            {
                records.Add((recordLine, fields));
            }

            fields = [];
        }

        for (; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else if (c == '\r' || c == '\n')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }

                    field.Append('\n');
                    line++;
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            if (c == '"' && field.Length == 0 && !fieldWasQuoted)
            {
                inQuotes = true;
                fieldWasQuoted = true;
            }
            else if (c == separator)
            {
                EndField();
            }
            else if (c == '\r' || c == '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                EndRecord();
                line++;
                recordLine = line;
            }
            else
            {
                // Lenient: a quote in the middle of an unquoted field (or after a closing quote) is kept as is.
                field.Append(c);
            }
        }

        if (field.Length > 0 || fields.Count > 0 || fieldWasQuoted)
        {
            EndRecord();
        }

        return records;
    }
}
