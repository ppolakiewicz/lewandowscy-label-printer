using System.Text;
using System.Text.Json;
using LabelPrinter.Core.Layout;
using Microsoft.Data.Sqlite;

namespace LabelPrinter.Core.Storage;

/// <summary>
/// The saved-text database (SQLite): remembers every printed label, suggests texts while typing and lets the
/// user edit the list. There is one entry per distinct text (compared ignoring case and whitespace) and layout
/// mode; the entry keeps the layout settings used most recently.
/// </summary>
/// <remarks>
/// Keeps one connection open until <see cref="Dispose"/>. Calls are serialized with a lock, so the store may be
/// used from several threads, but it is meant for one process (one PC, one user).
/// </remarks>
public sealed partial class LabelTextStore : IDisposable
{
    /// <summary>Schema version written to <c>PRAGMA user_version</c>; bump it and add a migration step when the schema changes.</summary>
    public const int SchemaVersion = 1;

    private const string SelectColumns =
        "id, mode, lines, alignment, bold, font_size, font_family, note, use_count, created_utc, last_used_utc";

    private readonly SqliteConnection _connection;
    private readonly TimeProvider _time;
    private readonly Lock _lock = new();

    /// <summary>Opens (creating if needed) the database file and its folder, and creates or migrates the schema.</summary>
    /// <param name="databasePath">Database file, e.g. <c>%APPDATA%\LabelPrinter\labels.db</c>.</param>
    /// <param name="timeProvider">Clock for timestamps; <see cref="TimeProvider.System"/> by default (tests pass a fake one).</param>
    /// <exception cref="NotSupportedException">The file was created by a newer version of the program.</exception>
    public LabelTextStore(string databasePath, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        DatabasePath = Path.GetFullPath(databasePath);
        _time = timeProvider ?? TimeProvider.System;

        var directory = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false, // release the file as soon as the store is disposed
        }.ToString();

        _connection = new SqliteConnection(connectionString);
        try
        {
            _connection.Open();
            InitializeSchema();
        }
        catch
        {
            _connection.Dispose();
            throw;
        }
    }

    /// <summary>Full path of the database file.</summary>
    public string DatabasePath { get; }

    /// <summary>
    /// Records that a label was printed: creates the entry (UseCount 1) or, for an existing text and mode, bumps
    /// UseCount and LastUsedUtc and replaces the stored lines and layout settings with these (the latest used).
    /// The note is kept.
    /// </summary>
    /// <returns>The entry after the change.</returns>
    /// <exception cref="ArgumentException">The content has no text.</exception>
    public SavedLabel RecordUse(LabelContent content)
    {
        var normalized = NormalizeContent(content);
        var now = Now();
        lock (_lock)
        {
            using var command = _connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO labels (dedupe_key, mode, lines, text, alignment, bold, font_size, font_family,
                                    use_count, created_utc, last_used_utc, search, search_words)
                VALUES (@key, @mode, @lines, @text, @alignment, @bold, @font_size, @font_family,
                        1, @now, @now, @search, @search_words)
                ON CONFLICT (dedupe_key) DO UPDATE SET
                    mode = excluded.mode, lines = excluded.lines, text = excluded.text,
                    alignment = excluded.alignment, bold = excluded.bold, font_size = excluded.font_size,
                    font_family = excluded.font_family, search = excluded.search, search_words = excluded.search_words,
                    use_count = use_count + 1, last_used_utc = excluded.last_used_utc
                RETURNING id;
                """;
            AddContentParameters(command, normalized);
            command.Parameters.AddWithValue("@now", now.Ticks);
            var id = (long)command.ExecuteScalar()!;
            return GetCore(id)!;
        }
    }

    /// <summary>
    /// Suggestions while typing. Matches the query anywhere in the text, ignoring case, whitespace and Polish
    /// diacritics ("zolta" finds "Żółta"); several words also match in any order. Ranking: texts starting with the
    /// query, then texts with a word starting with it, then other substring matches, then any-order word matches;
    /// within a group by UseCount (desc), then LastUsedUtc (desc). An empty query gives the most used / most recent.
    /// </summary>
    /// <param name="query">What the user typed so far.</param>
    /// <param name="limit">Maximum number of results; zero or less gives none.</param>
    public IReadOnlyList<SavedLabel> Search(string? query, int limit = 10)
    {
        return limit <= 0 ? [] : Query(query, limit);
    }

    /// <summary>All entries for the editor, filtered and ordered like <see cref="Search"/> (no limit).</summary>
    /// <param name="filter">Optional search text; null or empty lists everything by UseCount, then LastUsedUtc.</param>
    public IReadOnlyList<SavedLabel> GetAll(string? filter = null) => Query(filter, null);

    /// <summary>Number of entries.</summary>
    public int Count()
    {
        lock (_lock)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM labels;";
            return Convert.ToInt32(command.ExecuteScalar());
        }
    }

    /// <summary>The entry with this id, or null.</summary>
    public SavedLabel? Get(long id)
    {
        lock (_lock)
        {
            return GetCore(id);
        }
    }

    /// <summary>Adds a new entry from the editor (UseCount 0, never used).</summary>
    /// <exception cref="ArgumentException">The content has no text.</exception>
    /// <exception cref="DuplicateLabelException">An entry with the same text and mode exists; its id is in the exception.</exception>
    public SavedLabel Add(LabelContent content, string? note = null)
    {
        var normalized = NormalizeContent(content);
        var key = DedupeKey(normalized);
        lock (_lock)
        {
            if (FindIdByKey(key) is { } existingId)
            {
                throw new DuplicateLabelException(existingId, normalized.Text);
            }

            var id = Insert(normalized, NormalizeNote(note), useCount: 0, createdUtc: Now(), lastUsedUtc: null);
            return GetCore(id)!;
        }
    }

    /// <summary>
    /// Changes an entry's text, layout and note from the editor; UseCount and the dates are kept.
    /// </summary>
    /// <param name="id">The entry to change.</param>
    /// <param name="content">New text and layout.</param>
    /// <param name="note">New note (null or blank clears it).</param>
    /// <param name="mergeIfDuplicate">
    /// What to do when another entry already has the new text and mode. False (default): throw
    /// <see cref="DuplicateLabelException"/> so the UI can ask the user. True: merge the two — the other entry gets
    /// the new content, the note (this one, or the other's if this is blank), the sum of both UseCounts, the later
    /// LastUsedUtc and the earlier CreatedUtc; the entry <paramref name="id"/> is deleted and the merged one returned.
    /// </param>
    /// <returns>The updated entry (with a different id after a merge).</returns>
    /// <exception cref="KeyNotFoundException">There is no entry <paramref name="id"/>.</exception>
    /// <exception cref="DuplicateLabelException">Collision and <paramref name="mergeIfDuplicate"/> is false.</exception>
    public SavedLabel Update(long id, LabelContent content, string? note, bool mergeIfDuplicate = false)
    {
        var normalized = NormalizeContent(content);
        var key = DedupeKey(normalized);
        note = NormalizeNote(note);
        lock (_lock)
        {
            var current = GetCore(id) ?? throw new KeyNotFoundException($"No saved label with id {id}.");
            var otherId = FindIdByKey(key);
            if (otherId is null || otherId == id)
            {
                WriteRow(id, normalized, note, current.UseCount, current.CreatedUtc, current.LastUsedUtc);
                return GetCore(id)!;
            }

            if (!mergeIfDuplicate)
            {
                throw new DuplicateLabelException(otherId.Value, normalized.Text);
            }

            var other = GetCore(otherId.Value)!;
            using var transaction = _connection.BeginTransaction();
            DeleteCore(id);
            WriteRow(
                other.Id,
                normalized,
                note ?? other.Note,
                current.UseCount + other.UseCount,
                Min(current.CreatedUtc, other.CreatedUtc),
                Max(current.LastUsedUtc, other.LastUsedUtc));
            transaction.Commit();
            return GetCore(other.Id)!;
        }
    }

    /// <summary>Deletes an entry.</summary>
    /// <returns>False if there was no such entry.</returns>
    public bool Delete(long id)
    {
        lock (_lock)
        {
            return DeleteCore(id);
        }
    }

    /// <summary>Closes the database file.</summary>
    public void Dispose() => _connection.Dispose();

    // ---- normalisation -------------------------------------------------------------------------------------

    /// <summary>
    /// The form in which content is stored: lines trimmed; Auto mode has exactly one line (line breaks become
    /// spaces); Manual mode drops empty lines; a blank font family becomes the default; a non-positive font size
    /// becomes null (auto).
    /// </summary>
    internal static LabelContent NormalizeContent(LabelContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var pieces = (content.Lines ?? [])
            .SelectMany(l => (l ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();
        if (pieces.Count == 0)
        {
            throw new ArgumentException("The label text is empty.", nameof(content));
        }

        IReadOnlyList<string> lines = content.Mode == LayoutMode.Auto ? [string.Join(' ', pieces)] : pieces;
        var fontSize = content.FontSizeDots is { } size && double.IsFinite(size) && size > 0 ? size : (double?)null;
        var fontFamily = string.IsNullOrWhiteSpace(content.FontFamily) ? LabelContent.DefaultFontFamily : content.FontFamily.Trim();
        return content with { Lines = lines, FontSizeDots = fontSize, FontFamily = fontFamily };
    }

    /// <summary>The uniqueness key: mode plus the case- and whitespace-normalised text.</summary>
    internal static string DedupeKey(LabelContent normalized) =>
        normalized.Mode + "|" + TextNormalizer.DedupeKey(normalized.Text);

    /// <summary>True if both have the same lines and layout settings.</summary>
    internal static bool SameContent(LabelContent a, LabelContent b) =>
        a.Mode == b.Mode && a.Alignment == b.Alignment && a.Bold == b.Bold && a.FontSizeDots == b.FontSizeDots
        && a.FontFamily == b.FontFamily && a.Lines.SequenceEqual(b.Lines);

    private static string? NormalizeNote(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();

    private static DateTime Min(DateTime a, DateTime b) => a <= b ? a : b;

    private static DateTime? Max(DateTime? a, DateTime? b) => a is null ? b : b is null ? a : a >= b ? a : b;

    private DateTime Now() => _time.GetUtcNow().UtcDateTime;

    // ---- SQL helpers (call under _lock) --------------------------------------------------------------------

    private void InitializeSchema()
    {
        Execute("PRAGMA journal_mode = WAL;");
        Execute("PRAGMA synchronous = NORMAL;");

        var version = Convert.ToInt32(Scalar("PRAGMA user_version;"));
        if (version > SchemaVersion)
        {
            throw new NotSupportedException(
                $"The database {DatabasePath} has schema version {version}; this program supports up to {SchemaVersion}.");
        }

        if (version < 1)
        {
            using var transaction = _connection.BeginTransaction();
            Execute(
                """
                CREATE TABLE IF NOT EXISTS labels (
                    id            INTEGER PRIMARY KEY AUTOINCREMENT,
                    dedupe_key    TEXT    NOT NULL UNIQUE,
                    mode          TEXT    NOT NULL,
                    lines         TEXT    NOT NULL,
                    text          TEXT    NOT NULL,
                    alignment     TEXT    NOT NULL,
                    bold          INTEGER NOT NULL,
                    font_size     REAL    NULL,
                    font_family   TEXT    NOT NULL,
                    note          TEXT    NULL,
                    use_count     INTEGER NOT NULL DEFAULT 0,
                    created_utc   INTEGER NOT NULL,
                    last_used_utc INTEGER NULL,
                    search        TEXT    NOT NULL,
                    search_words  TEXT    NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_labels_usage ON labels (use_count DESC, last_used_utc DESC);
                PRAGMA user_version = 1;
                """);
            transaction.Commit();
        }

        // Future migrations: if (version < 2) { ...; PRAGMA user_version = 2; }
    }

    private List<SavedLabel> Query(string? query, int? limit)
    {
        var folded = TextNormalizer.FoldForSearch(query);
        lock (_lock)
        {
            using var command = _connection.CreateCommand();
            var sql = new StringBuilder("SELECT ").Append(SelectColumns);
            if (folded.Length == 0)
            {
                sql.Append(" FROM labels ORDER BY use_count DESC, last_used_utc DESC, id DESC");
            }
            else
            {
                sql.Append(
                    """
                    , CASE
                        WHEN substr(search, 1, length(@q)) = @q THEN 0
                        WHEN @qw <> '' AND instr(' ' || search_words, ' ' || @qw) > 0 THEN 1
                        WHEN instr(search, @q) > 0 THEN 2
                        ELSE 3
                      END AS rank
                    FROM labels WHERE instr(search, @q) > 0
                    """);
                command.Parameters.AddWithValue("@q", folded);
                command.Parameters.AddWithValue("@qw", TextNormalizer.FoldWords(query));

                var tokens = folded.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().Take(8).ToArray();
                if (tokens.Length > 1)
                {
                    sql.Append(" OR (");
                    for (var i = 0; i < tokens.Length; i++)
                    {
                        sql.Append(i > 0 ? " AND " : "").Append($"instr(search, @t{i}) > 0");
                        command.Parameters.AddWithValue($"@t{i}", tokens[i]);
                    }

                    sql.Append(')');
                }

                sql.Append(" ORDER BY rank, use_count DESC, last_used_utc DESC, id DESC");
            }

            if (limit is { } max)
            {
                sql.Append(" LIMIT @limit");
                command.Parameters.AddWithValue("@limit", max);
            }

            command.CommandText = sql.Append(';').ToString();
            return ReadLabels(command);
        }
    }

    private SavedLabel? GetCore(long id)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"SELECT {SelectColumns} FROM labels WHERE id = @id;";
        command.Parameters.AddWithValue("@id", id);
        return ReadLabels(command).FirstOrDefault();
    }

    private SavedLabel? GetByKey(string key)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"SELECT {SelectColumns} FROM labels WHERE dedupe_key = @key;";
        command.Parameters.AddWithValue("@key", key);
        return ReadLabels(command).FirstOrDefault();
    }

    private long? FindIdByKey(string key)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT id FROM labels WHERE dedupe_key = @key;";
        command.Parameters.AddWithValue("@key", key);
        return command.ExecuteScalar() is long id ? id : null;
    }

    private long Insert(LabelContent normalized, string? note, int useCount, DateTime createdUtc, DateTime? lastUsedUtc)
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO labels (dedupe_key, mode, lines, text, alignment, bold, font_size, font_family, note,
                                use_count, created_utc, last_used_utc, search, search_words)
            VALUES (@key, @mode, @lines, @text, @alignment, @bold, @font_size, @font_family, @note,
                    @use_count, @created_utc, @last_used_utc, @search, @search_words)
            RETURNING id;
            """;
        AddContentParameters(command, normalized);
        AddStatParameters(command, note, useCount, createdUtc, lastUsedUtc);
        return (long)command.ExecuteScalar()!;
    }

    private void WriteRow(long id, LabelContent normalized, string? note, int useCount, DateTime createdUtc, DateTime? lastUsedUtc)
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            """
            UPDATE labels SET dedupe_key = @key, mode = @mode, lines = @lines, text = @text, alignment = @alignment,
                bold = @bold, font_size = @font_size, font_family = @font_family, note = @note, use_count = @use_count,
                created_utc = @created_utc, last_used_utc = @last_used_utc, search = @search, search_words = @search_words
            WHERE id = @id;
            """;
        AddContentParameters(command, normalized);
        AddStatParameters(command, note, useCount, createdUtc, lastUsedUtc);
        command.Parameters.AddWithValue("@id", id);
        command.ExecuteNonQuery();
    }

    private bool DeleteCore(long id)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM labels WHERE id = @id;";
        command.Parameters.AddWithValue("@id", id);
        return command.ExecuteNonQuery() > 0;
    }

    private static void AddContentParameters(SqliteCommand command, LabelContent normalized)
    {
        var text = normalized.Text;
        command.Parameters.AddWithValue("@key", DedupeKey(normalized));
        command.Parameters.AddWithValue("@mode", normalized.Mode.ToString());
        command.Parameters.AddWithValue("@lines", JsonSerializer.Serialize(normalized.Lines));
        command.Parameters.AddWithValue("@text", text);
        command.Parameters.AddWithValue("@alignment", normalized.Alignment.ToString());
        command.Parameters.AddWithValue("@bold", normalized.Bold ? 1 : 0);
        command.Parameters.AddWithValue("@font_size", (object?)normalized.FontSizeDots ?? DBNull.Value);
        command.Parameters.AddWithValue("@font_family", normalized.FontFamily);
        command.Parameters.AddWithValue("@search", TextNormalizer.FoldForSearch(text));
        command.Parameters.AddWithValue("@search_words", TextNormalizer.FoldWords(text));
    }

    private static void AddStatParameters(SqliteCommand command, string? note, int useCount, DateTime createdUtc, DateTime? lastUsedUtc)
    {
        command.Parameters.AddWithValue("@note", (object?)note ?? DBNull.Value);
        command.Parameters.AddWithValue("@use_count", Math.Max(0, useCount));
        command.Parameters.AddWithValue("@created_utc", ToUtc(createdUtc).Ticks);
        command.Parameters.AddWithValue("@last_used_utc", lastUsedUtc is { } t ? ToUtc(t).Ticks : DBNull.Value);
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    private static List<SavedLabel> ReadLabels(SqliteCommand command)
    {
        var result = new List<SavedLabel>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var linesJson = reader.GetString(2);
            IReadOnlyList<string> lines;
            try
            {
                lines = JsonSerializer.Deserialize<List<string>>(linesJson) ?? [];
            }
            catch (JsonException)
            {
                lines = [linesJson];
            }

            var content = new LabelContent
            {
                Mode = Enum.TryParse<LayoutMode>(reader.GetString(1), out var mode) ? mode : LayoutMode.Auto,
                Lines = lines,
                Alignment = Enum.TryParse<LabelAlignment>(reader.GetString(3), out var alignment) ? alignment : LabelAlignment.Center,
                Bold = reader.GetInt64(4) != 0,
                FontSizeDots = reader.IsDBNull(5) ? null : reader.GetDouble(5),
                FontFamily = reader.GetString(6),
            };

            result.Add(new SavedLabel
            {
                Id = reader.GetInt64(0),
                Content = content,
                Note = reader.IsDBNull(7) ? null : reader.GetString(7),
                UseCount = reader.GetInt32(8),
                CreatedUtc = new DateTime(reader.GetInt64(9), DateTimeKind.Utc),
                LastUsedUtc = reader.IsDBNull(10) ? null : new DateTime(reader.GetInt64(10), DateTimeKind.Utc),
            });
        }

        return result;
    }

    private void Execute(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private object? Scalar(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }
}
