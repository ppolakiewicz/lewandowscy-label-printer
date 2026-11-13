namespace LabelPrinter.Core.Storage;

/// <summary>A CSV row that could not be imported.</summary>
/// <param name="Row">1-based row number in the file (a header, if present, is row 1).</param>
/// <param name="Message">Why the row was skipped.</param>
public sealed record CsvImportIssue(int Row, string Message);

/// <summary>Outcome of <see cref="LabelTextStore.ImportCsv(Stream)"/>.</summary>
/// <param name="Added">Rows that created a new entry.</param>
/// <param name="Updated">Rows that changed an existing entry (same text and mode).</param>
/// <param name="Skipped">Rows that changed nothing (identical to the database) or were invalid; the invalid ones are listed in <paramref name="Issues"/>.</param>
/// <param name="Issues">Invalid rows (empty text, unreadable values).</param>
public sealed record CsvImportResult(int Added, int Updated, int Skipped, IReadOnlyList<CsvImportIssue> Issues);
