using System.Globalization;
using System.Text;
using LabelPrinter.Core.Layout;

namespace LabelPrinter.Core.Storage;

public sealed partial class LabelTextStore
{
    /// <summary>Separator used for export (what Excel expects with Polish regional settings).</summary>
    public const char CsvSeparator = ';';

    private const string CsvDateFormat = "yyyy-MM-dd HH:mm:ss";

    private static readonly string[] CsvDateFormats =
    [
        "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd",
        "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy HH:mm", "dd.MM.yyyy", "d.M.yyyy H:mm:ss", "d.M.yyyy H:mm", "d.M.yyyy",
    ];

    private enum CsvColumn
    {
        Text,
        Mode,
        Alignment,
        Bold,
        FontSize,
        FontFamily,
        UseCount,
        LastUsed,
        Created,
        Note,
    }

    /// <summary>Header written on export, in <see cref="CsvColumn"/> order.</summary>
    private static readonly string[] CsvHeader =
    [
        "Tekst", "Tryb", "Wyrównanie", "Pogrubienie", "Rozmiar czcionki", "Czcionka",
        "Liczba użyć", "Ostatnie użycie (UTC)", "Dodano (UTC)", "Notatka",
    ];

    /// <summary>Accepted header names (folded with <see cref="HeaderKey"/>) for import.</summary>
    private static readonly Dictionary<string, CsvColumn> CsvHeaderAliases = new()
    {
        ["tekst"] = CsvColumn.Text, ["text"] = CsvColumn.Text,
        ["tryb"] = CsvColumn.Mode, ["mode"] = CsvColumn.Mode,
        ["wyrownanie"] = CsvColumn.Alignment, ["alignment"] = CsvColumn.Alignment,
        ["pogrubienie"] = CsvColumn.Bold, ["bold"] = CsvColumn.Bold,
        ["rozmiarczcionki"] = CsvColumn.FontSize, ["fontsize"] = CsvColumn.FontSize, ["fontsizedots"] = CsvColumn.FontSize,
        ["czcionka"] = CsvColumn.FontFamily, ["fontfamily"] = CsvColumn.FontFamily, ["font"] = CsvColumn.FontFamily,
        ["liczbauzyc"] = CsvColumn.UseCount, ["usecount"] = CsvColumn.UseCount,
        ["ostatnieuzycieutc"] = CsvColumn.LastUsed, ["ostatnieuzycie"] = CsvColumn.LastUsed,
        ["lastused"] = CsvColumn.LastUsed, ["lastusedutc"] = CsvColumn.LastUsed,
        ["dodanoutc"] = CsvColumn.Created, ["dodano"] = CsvColumn.Created,
        ["created"] = CsvColumn.Created, ["createdutc"] = CsvColumn.Created,
        ["notatka"] = CsvColumn.Note, ["note"] = CsvColumn.Note,
    };

    /// <summary>
    /// Writes all entries as CSV for Excel: UTF-8 with BOM, separator ';', CRLF rows, RFC 4180 quoting, a header row
    /// (Tekst; Tryb; Wyrównanie; Pogrubienie; Rozmiar czcionki; Czcionka; Liczba użyć; Ostatnie użycie (UTC);
    /// Dodano (UTC); Notatka). Manual-mode lines are joined with '\n' inside the quoted text field. Mode and alignment
    /// are written as <c>Auto</c>/<c>Manual</c> and <c>Center</c>/<c>Left</c>, bold as <c>tak</c>/<c>nie</c>, font
    /// size with a decimal comma (empty = automatic), dates as <c>yyyy-MM-dd HH:mm:ss</c> in UTC.
    /// </summary>
    /// <param name="stream">Destination; left open.</param>
    public void ExportCsv(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var labels = GetAllById();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true);
        CsvCodec.WriteRecord(writer, CsvHeader, CsvSeparator);
        foreach (var label in labels)
        {
            var c = label.Content;
            CsvCodec.WriteRecord(
                writer,
                [
                    c.Text,
                    c.Mode.ToString(),
                    c.Alignment.ToString(),
                    c.Bold ? "tak" : "nie",
                    c.FontSizeDots?.ToString("0.###", CultureInfo.InvariantCulture).Replace('.', ','),
                    c.FontFamily,
                    label.UseCount.ToString(CultureInfo.InvariantCulture),
                    label.LastUsedUtc?.ToString(CsvDateFormat, CultureInfo.InvariantCulture),
                    label.CreatedUtc.ToString(CsvDateFormat, CultureInfo.InvariantCulture),
                    label.Note,
                ],
                CsvSeparator);
        }
    }

    /// <summary>Writes all entries to a CSV file (see <see cref="ExportCsv(Stream)"/>), creating its folder.</summary>
    public void ExportCsv(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(path);
        ExportCsv(stream);
    }

    /// <summary>Imports a CSV file (see <see cref="ImportCsv(Stream)"/>).</summary>
    public CsvImportResult ImportCsv(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.OpenRead(path);
        return ImportCsv(stream);
    }

    /// <summary>
    /// Imports entries from CSV, merging by the same rule as <see cref="RecordUse"/> (text ignoring case and
    /// whitespace, plus mode), in one transaction.
    /// <list type="bullet">
    /// <item>Encoding: UTF-8 (with or without BOM), UTF-16 with BOM, otherwise Windows-1250 (what Polish Excel saves
    /// as plain "CSV").</item>
    /// <item>Separator: detected from the first line (';', ',' or tab).</item>
    /// <item>Columns are found by header name (Polish names from the export or English ones: text, mode, alignment,
    /// bold, font size, font family, use count, last used, created, note); only the text column is required. A file
    /// without a recognisable header is read in the export's column order, so a plain one-column list of texts works.</item>
    /// <item>Missing mode: Manual if the text has line breaks, otherwise Auto.</item>
    /// <item>A new text is added. For an existing one, the layout columns present in the file replace the stored
    /// ones, a present note column replaces the note (empty clears it), UseCount becomes the larger of the two, and
    /// LastUsedUtc the later; CreatedUtc is kept.</item>
    /// </list>
    /// </summary>
    /// <param name="stream">Source; left open.</param>
    /// <returns>Counts of added, updated and skipped rows; skipped are unchanged duplicates and invalid rows.</returns>
    public CsvImportResult ImportCsv(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var text = DecodeCsv(stream);
        var separator = CsvCodec.DetectSeparator(text);
        var records = CsvCodec.Parse(text, separator);
        var issues = new List<CsvImportIssue>();
        int added = 0, updated = 0, skipped = 0;
        if (records.Count == 0)
        {
            return new CsvImportResult(0, 0, 0, issues);
        }

        var columns = new Dictionary<CsvColumn, int>();
        for (var i = 0; i < records[0].Fields.Count; i++)
        {
            if (CsvHeaderAliases.TryGetValue(HeaderKey(records[0].Fields[i]), out var column))
            {
                columns.TryAdd(column, i);
            }
        }

        var firstDataRecord = 1;
        if (!columns.ContainsKey(CsvColumn.Text))
        {
            columns = Enum.GetValues<CsvColumn>().ToDictionary(c => c, c => (int)c);
            firstDataRecord = 0;
        }

        var now = Now();
        lock (_lock)
        {
            using var transaction = _connection.BeginTransaction();
            foreach (var (line, fields) in records.Skip(firstDataRecord))
            {
                // Null = the column is not in the file (or the row is shorter): keep the stored value.
                string? Field(CsvColumn column) =>
                    columns.TryGetValue(column, out var index) && index < fields.Count ? fields[index] : null;

                if (!TryParseRow(Field, out var row, out var error))
                {
                    issues.Add(new CsvImportIssue(line, error));
                    skipped++;
                    continue;
                }

                var mode = row.Mode ?? (row.Text.Contains('\n') ? LayoutMode.Manual : LayoutMode.Auto);
                LabelContent probe;
                try
                {
                    probe = NormalizeContent(new LabelContent { Mode = mode, Lines = row.Text.Split('\n') });
                }
                catch (ArgumentException)
                {
                    issues.Add(new CsvImportIssue(line, "Empty text."));
                    skipped++;
                    continue;
                }

                var existing = GetByKey(DedupeKey(probe));
                var baseContent = existing?.Content ?? new LabelContent();
                var content = NormalizeContent(baseContent with
                {
                    Mode = mode,
                    Lines = probe.Lines,
                    Alignment = row.Alignment ?? baseContent.Alignment,
                    Bold = row.Bold ?? baseContent.Bold,
                    FontSizeDots = row.HasFontSize ? row.FontSizeDots : baseContent.FontSizeDots,
                    FontFamily = row.FontFamily ?? baseContent.FontFamily,
                });

                if (existing is null)
                {
                    Insert(content, NormalizeNote(row.Note), row.UseCount ?? 0, row.CreatedUtc ?? now, row.LastUsedUtc);
                    added++;
                    continue;
                }

                var note = row.HasNote ? NormalizeNote(row.Note) : existing.Note;
                var useCount = Math.Max(existing.UseCount, row.UseCount ?? 0);
                var lastUsed = Max(existing.LastUsedUtc, row.LastUsedUtc);
                if (SameContent(existing.Content, content) && note == existing.Note && useCount == existing.UseCount
                    && lastUsed == existing.LastUsedUtc)
                {
                    skipped++;
                    continue;
                }

                WriteRow(existing.Id, content, note, useCount, existing.CreatedUtc, lastUsed);
                updated++;
            }

            transaction.Commit();
        }

        return new CsvImportResult(added, updated, skipped, issues);
    }

    private List<SavedLabel> GetAllById()
    {
        lock (_lock)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM labels ORDER BY id;";
            return ReadLabels(command);
        }
    }

    private sealed class CsvRow
    {
        public string Text { get; set; } = "";
        public LayoutMode? Mode { get; set; }
        public LabelAlignment? Alignment { get; set; }
        public bool? Bold { get; set; }
        public bool HasFontSize { get; set; }
        public double? FontSizeDots { get; set; }
        public string? FontFamily { get; set; }
        public int? UseCount { get; set; }
        public DateTime? LastUsedUtc { get; set; }
        public DateTime? CreatedUtc { get; set; }
        public bool HasNote { get; set; }
        public string? Note { get; set; }
    }

    private static bool TryParseRow(Func<CsvColumn, string?> field, out CsvRow row, out string error)
    {
        row = new CsvRow { Text = (field(CsvColumn.Text) ?? "").Replace("\r\n", "\n").Replace('\r', '\n') };
        error = "";

        var mode = field(CsvColumn.Mode)?.Trim();
        if (!string.IsNullOrEmpty(mode))
        {
            row.Mode = TextNormalizer.FoldForSearch(mode) switch
            {
                "auto" or "automatyczny" or "0" => LayoutMode.Auto,
                "manual" or "reczny" or "1" => LayoutMode.Manual,
                _ => null,
            };
            if (row.Mode is null)
            {
                error = $"Unknown mode \"{mode}\".";
                return false;
            }
        }

        var alignment = field(CsvColumn.Alignment)?.Trim();
        if (!string.IsNullOrEmpty(alignment))
        {
            row.Alignment = TextNormalizer.FoldForSearch(alignment) switch
            {
                "center" or "srodek" or "do srodka" or "wysrodkowane" or "0" => LabelAlignment.Center,
                "left" or "lewo" or "do lewej" or "1" => LabelAlignment.Left,
                _ => null,
            };
            if (row.Alignment is null)
            {
                error = $"Unknown alignment \"{alignment}\".";
                return false;
            }
        }

        var bold = field(CsvColumn.Bold)?.Trim();
        if (!string.IsNullOrEmpty(bold))
        {
            row.Bold = TextNormalizer.FoldForSearch(bold) switch
            {
                "tak" or "t" or "1" or "true" or "yes" or "y" or "prawda" or "x" => true,
                "nie" or "n" or "0" or "false" or "no" or "falsz" => false,
                _ => null,
            };
            if (row.Bold is null)
            {
                error = $"Unknown bold value \"{bold}\".";
                return false;
            }
        }

        var fontSize = field(CsvColumn.FontSize);
        if (fontSize is not null)
        {
            row.HasFontSize = true;
            fontSize = fontSize.Trim();
            if (fontSize.Length > 0)
            {
                if (!TryParseNumber(fontSize, out var size) || size <= 0)
                {
                    error = $"Invalid font size \"{fontSize}\".";
                    return false;
                }

                row.FontSizeDots = size;
            }
        }

        var fontFamily = field(CsvColumn.FontFamily)?.Trim();
        row.FontFamily = string.IsNullOrEmpty(fontFamily) ? null : fontFamily;

        var useCount = field(CsvColumn.UseCount)?.Trim();
        if (!string.IsNullOrEmpty(useCount))
        {
            if (!TryParseNumber(useCount, out var count) || count < 0 || count > int.MaxValue)
            {
                error = $"Invalid use count \"{useCount}\".";
                return false;
            }

            row.UseCount = (int)Math.Round(count);
        }

        if (!TryParseDate(field(CsvColumn.LastUsed), out var lastUsed))
        {
            error = $"Invalid date \"{field(CsvColumn.LastUsed)}\".";
            return false;
        }

        if (!TryParseDate(field(CsvColumn.Created), out var created))
        {
            error = $"Invalid date \"{field(CsvColumn.Created)}\".";
            return false;
        }

        row.LastUsedUtc = lastUsed;
        row.CreatedUtc = created;

        var note = field(CsvColumn.Note);
        row.HasNote = note is not null;
        row.Note = note;
        return true;
    }

    private static bool TryParseNumber(string value, out double number)
    {
        var normalized = value.Replace(" ", "").Replace(" ", "").Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
            && double.IsFinite(number);
    }

    private static bool TryParseDate(string? value, out DateTime? date)
    {
        date = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        const DateTimeStyles styles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
        if (DateTime.TryParseExact(value.Trim(), CsvDateFormats, CultureInfo.InvariantCulture, styles, out var parsed)
            || DateTime.TryParse(value.Trim(), CultureInfo.InvariantCulture, styles, out parsed))
        {
            date = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            return true;
        }

        return false;
    }

    /// <summary>Folds a header cell for alias lookup: no diacritics, lower case, letters only ("Liczba użyć" → "liczbauzyc").</summary>
    private static string HeaderKey(string header) =>
        string.Concat(TextNormalizer.FoldForSearch(header).Where(char.IsLetter));

    private static string DecodeCsv(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var bytes = buffer.ToArray();

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1250).GetString(bytes);
        }
    }
}
